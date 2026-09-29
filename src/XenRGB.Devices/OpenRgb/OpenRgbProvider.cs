using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.Core.Devices;

namespace XenRGB.Devices.OpenRgb;

/// <summary>
/// Device provider connecting to local or network OpenRGB instance.
/// Includes a resilient background auto-reconnect loop to handle startup latency and server crashes.
/// </summary>
public sealed class OpenRgbProvider : IDeviceProvider
{
    private readonly IOpenRgbClient _client;
    private readonly List<OpenRgbDevice> _devices = new();
    private readonly object _devicesLock = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly CancellationTokenSource _loopCts = new();
    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _reconnectInterval;

    private Task? _reconnectLoopTask;
    private readonly object _loopTaskLock = new();
    private bool _isDisposed;

    public string ProviderId => "openrgb-provider";
    public string DisplayName => "OpenRGB";
    public bool IsConnected => _client.IsConnected;
    public TimeSpan ReconnectInterval => _reconnectInterval;

    public event EventHandler<DeviceEventArgs>? DeviceDiscovered;
    public event EventHandler<DeviceEventArgs>? DeviceRemoved;

    public OpenRgbProvider(string host = "127.0.0.1", int port = 6742, TimeSpan reconnectInterval = default)
        : this(new OpenRgbClient(), host, port, reconnectInterval)
    {
    }

    public OpenRgbProvider(IOpenRgbClient client, string host = "127.0.0.1", int port = 6742, TimeSpan reconnectInterval = default)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _host = host;
        _port = port;
        _reconnectInterval = reconnectInterval > TimeSpan.Zero ? reconnectInterval : TimeSpan.FromSeconds(10);
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        try
        {
            await TryConnectAndDiscoverAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Do not throw at startup if OpenRGB is not running
        }
        finally
        {
            StartReconnectLoop();
        }
    }

    public void StartReconnectLoop()
    {
        if (_isDisposed) return;

        lock (_loopTaskLock)
        {
            if (_reconnectLoopTask != null || _isDisposed)
                return;

            _reconnectLoopTask = Task.Run(ReconnectLoopAsync);
        }
    }

    private async Task ReconnectLoopAsync()
    {
        using var timer = new PeriodicTimer(_reconnectInterval);

        while (!_loopCts.IsCancellationRequested && !_isDisposed)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(_loopCts.Token).ConfigureAwait(false))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (_loopCts.IsCancellationRequested || _isDisposed)
                break;

            try
            {
                bool needsReconnect = !_client.IsConnected || HasDisconnectedOrErrorDevices();
                if (needsReconnect)
                {
                    await TryConnectAndDiscoverAsync(_loopCts.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Silently continue polling to survive crashes and transient network drops
            }
        }
    }

    public async Task<bool> TryConnectAndDiscoverAsync(CancellationToken ct = default)
    {
        if (_isDisposed) return false;

        try
        {
            await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }

        try
        {
            if (!_client.IsConnected)
            {
                bool ok = await _client.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
                if (!ok)
                {
                    MarkDevicesDisconnected();
                    return false;
                }
            }

            return await DiscoverOrRefreshDevicesAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch
        {
            _client.Disconnect();
            MarkDevicesDisconnected();
            return false;
        }
        finally
        {
            try { _connectLock.Release(); } catch { }
        }
    }

    public async Task<IReadOnlyList<IRgbDevice>> DiscoverDevicesAsync(CancellationToken ct = default)
    {
        await TryConnectAndDiscoverAsync(ct).ConfigureAwait(false);
        lock (_devicesLock)
        {
            return _devices.Cast<IRgbDevice>().ToList().AsReadOnly();
        }
    }

    private async Task<bool> DiscoverOrRefreshDevicesAsync(CancellationToken ct)
    {
        uint count = await _client.GetControllerCountAsync(ct).ConfigureAwait(false);
        var newlyDiscovered = new List<OpenRgbDevice>();

        for (uint i = 0; i < count; i++)
        {
            try
            {
                byte[] raw = await _client.GetControllerDataRawAsync(i, ct).ConfigureAwait(false);
                var parsed = ParseControllerData(i, raw);
                if (parsed == null) continue;

                lock (_devicesLock)
                {
                    var existing = _devices.FirstOrDefault(d => d.Id == parsed.Id);
                    if (existing != null)
                    {
                        existing.ConnectionState = DeviceConnectionState.Connected;
                        existing.Throttler.Reset();
                    }
                    else
                    {
                        _devices.Add(parsed);
                        newlyDiscovered.Add(parsed);
                    }
                }
            }
            catch
            {
                // Continue with other controllers
            }
        }

        // Notify for any newly discovered devices outside lock
        foreach (var dev in newlyDiscovered)
        {
            DeviceDiscovered?.Invoke(this, new DeviceEventArgs(dev));
        }

        return true;
    }

    private bool HasDisconnectedOrErrorDevices()
    {
        lock (_devicesLock)
        {
            for (int i = 0; i < _devices.Count; i++)
            {
                if (_devices[i].ConnectionState != DeviceConnectionState.Connected)
                    return true;
            }
            return false;
        }
    }

    private void MarkDevicesDisconnected()
    {
        lock (_devicesLock)
        {
            foreach (var dev in _devices)
            {
                dev.ConnectionState = DeviceConnectionState.Disconnected;
            }
        }
    }

    private OpenRgbDevice? ParseControllerData(uint index, byte[] data)
    {
        if (data == null || data.Length < 16)
            return null;

        using var ms = new MemoryStream(data);
        using var reader = new BinaryReader(ms);

        try
        {
            _ = reader.ReadUInt32(); // total size
            int rawType = reader.ReadInt32();
            string name = ReadLengthPrefixedString(reader);
            string vendor = ReadLengthPrefixedString(reader);
            string description = ReadLengthPrefixedString(reader);
            string version = ReadLengthPrefixedString(reader);
            string serial = ReadLengthPrefixedString(reader);
            string location = ReadLengthPrefixedString(reader);

            // Skip modes
            ushort modeCount = reader.ReadUInt16();
            int activeMode = reader.ReadInt32();
            for (int m = 0; m < modeCount; m++)
            {
                ReadLengthPrefixedString(reader); // mode name
                reader.ReadInt32(); // value
                reader.ReadUInt32(); // flags
                reader.ReadUInt32(); // speed_min
                reader.ReadUInt32(); // speed_max
                reader.ReadUInt32(); // colors_min
                reader.ReadUInt32(); // colors_max
                reader.ReadUInt32(); // speed
                reader.ReadUInt32(); // direction
                reader.ReadUInt32(); // color_mode
                ushort numColors = reader.ReadUInt16();
                reader.ReadBytes(numColors * 4); // colors
            }

            // Zones
            ushort zoneCount = reader.ReadUInt16();
            int totalZoneLeds = 0;
            for (int z = 0; z < zoneCount; z++)
            {
                ReadLengthPrefixedString(reader); // zone name
                int zType = reader.ReadInt32(); // zone type (0 = single, 1 = linear, 2 = matrix)
                uint zLedsMin = reader.ReadUInt32();
                uint zLedsMax = reader.ReadUInt32();
                uint zLedsCount = reader.ReadUInt32();
                totalZoneLeds += (int)zLedsCount;

                ushort matrixLen = reader.ReadUInt16();
                if (matrixLen > 0)
                {
                    reader.ReadBytes(matrixLen);
                }
            }

            // LEDs
            ushort ledCount = reader.ReadUInt16();
            var ledCoordinates = new List<LedCoordinate>();

            for (int l = 0; l < ledCount; l++)
            {
                string ledName = ReadLengthPrefixedString(reader);
                reader.ReadUInt32(); // value

                // Calculate normalized coordinate
                float localX = ledCount > 1 ? (float)l / (ledCount - 1) : 0.5f;
                float localY = 0.5f;

                ledCoordinates.Add(new LedCoordinate(l, localX, localY, ledName));
            }

            if (ledCoordinates.Count == 0 && totalZoneLeds > 0)
            {
                for (int l = 0; l < totalZoneLeds; l++)
                {
                    float localX = totalZoneLeds > 1 ? (float)l / (totalZoneLeds - 1) : 0.5f;
                    ledCoordinates.Add(new LedCoordinate(l, localX, 0.5f, $"LED {l + 1}"));
                }
            }

            if (ledCoordinates.Count == 0)
            {
                // Default single LED
                ledCoordinates.Add(new LedCoordinate(0, 0.5f, 0.5f, "Main LED"));
            }

            var deviceType = MapDeviceType(rawType);
            string devId = $"openrgb-{index}-{name.ToLowerInvariant().Replace(' ', '-')}";
            string displayName = string.IsNullOrWhiteSpace(vendor) ? name : $"{vendor} {name}";

            return new OpenRgbDevice(_client, index, devId, displayName, deviceType, ledCoordinates);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadLengthPrefixedString(BinaryReader reader)
    {
        ushort len = reader.ReadUInt16();
        if (len == 0) return string.Empty;
        byte[] bytes = reader.ReadBytes(len);
        // Trim trailing null characters
        return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
    }

    private static RgbDeviceType MapDeviceType(int openRgbType) => openRgbType switch
    {
        0 => RgbDeviceType.Motherboard,
        1 => RgbDeviceType.Memory,
        2 => RgbDeviceType.GraphicsCard,
        3 => RgbDeviceType.Cooler,
        4 => RgbDeviceType.LedStrip,
        5 => RgbDeviceType.Keyboard,
        6 => RgbDeviceType.Mouse,
        7 => RgbDeviceType.Mousepad,
        8 => RgbDeviceType.Headset,
        11 => RgbDeviceType.Lightbar,
        _ => RgbDeviceType.Other
    };

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try { _loopCts.Cancel(); } catch { }
        try { _reconnectLoopTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _loopCts.Dispose();
        _connectLock.Dispose();

        _client.Dispose();
        lock (_devicesLock)
        {
            foreach (var dev in _devices)
            {
                DeviceRemoved?.Invoke(this, new DeviceEventArgs(dev));
                dev.Dispose();
            }
            _devices.Clear();
        }
    }
}
