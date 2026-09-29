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
    private string _host;
    private int _port;
    private readonly TimeSpan _reconnectInterval;

    private Task? _reconnectLoopTask;
    private readonly object _loopTaskLock = new();
    private bool _isDisposed;

    public string ProviderId => "openrgb-provider";
    public string DisplayName => "OpenRGB";
    public bool IsConnected => _client.IsConnected;
    public TimeSpan ReconnectInterval => _reconnectInterval;
    public string Host => _host;
    public int Port => _port;
    public string ConnectionStatusMessage { get; private set; } = "Disconnected (OpenRGB server not running)";
    public string? LastError { get; private set; }

    public IReadOnlyList<IRgbDevice> Devices
    {
        get
        {
            lock (_devicesLock)
            {
                return _devices.Cast<IRgbDevice>().ToList().AsReadOnly();
            }
        }
    }

    public event EventHandler<DeviceEventArgs>? DeviceDiscovered;
    public event EventHandler<DeviceEventArgs>? DeviceRemoved;
    public event EventHandler? DevicesUpdated;

    public void UpdateEndpoint(string host, int port)
    {
        _host = host;
        _port = port;
    }

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
        while (!_loopCts.IsCancellationRequested && !_isDisposed)
        {
            int devCount;
            lock (_devicesLock) devCount = _devices.Count;

            TimeSpan delay;
            if (!_client.IsConnected)
            {
                // Quick retry when disconnected from OpenRGB server
                delay = TimeSpan.FromSeconds(2);
            }
            else if (devCount == 0)
            {
                // Rapid polling while OpenRGB is scanning hardware devices at startup
                delay = TimeSpan.FromSeconds(1);
            }
            else
            {
                // Standard heartbeat interval when devices are connected
                delay = _reconnectInterval;
            }

            if (delay > _reconnectInterval)
            {
                delay = _reconnectInterval;
            }

            try
            {
                await Task.Delay(delay, _loopCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (_loopCts.IsCancellationRequested || _isDisposed)
                break;

            try
            {
                lock (_devicesLock) devCount = _devices.Count;
                bool needsReconnect = !_client.IsConnected || devCount == 0 || HasDisconnectedOrErrorDevices();
                if (needsReconnect)
                {
                    await TryConnectAndDiscoverAsync(_loopCts.Token).ConfigureAwait(false);
                }
                else
                {
                    // Light verification: check if OpenRGB detected new devices or if server disconnected
                    try
                    {
                        uint currentCount = await _client.GetControllerCountAsync(_loopCts.Token).ConfigureAwait(false);
                        if (currentCount != (uint)devCount)
                        {
                            await TryConnectAndDiscoverAsync(_loopCts.Token).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _client.Disconnect();
                        MarkDevicesDisconnected();
                        LastError = $"OpenRGB communication error: {ex.Message}";
                        ConnectionStatusMessage = $"Disconnected ({LastError})";
                    }
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
                    LastError = $"Cannot connect to OpenRGB server at {_host}:{_port}. Please make sure OpenRGB is running with the SDK Server enabled (port {_port}).";
                    ConnectionStatusMessage = $"Disconnected ({LastError})";
                    return false;
                }
            }

            bool discovered = await DiscoverOrRefreshDevicesAsync(ct).ConfigureAwait(false);
            if (discovered)
            {
                LastError = null;
                int count;
                lock (_devicesLock) count = _devices.Count;
                ConnectionStatusMessage = count == 0
                    ? $"Connected to {_host}:{_port} (v{_client.ServerProtocolVersion}, scanning devices...)"
                    : $"Connected to {_host}:{_port} (v{_client.ServerProtocolVersion}, {count} device(s))";
            }
            return discovered;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _client.Disconnect();
            MarkDevicesDisconnected();
            LastError = $"OpenRGB communication error: {ex.Message}";
            ConnectionStatusMessage = $"Disconnected ({LastError})";
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

    public async Task<IReadOnlyList<IRgbDevice>> RescanAndDiscoverAsync(CancellationToken ct = default)
    {
        if (_isDisposed) return Devices;

        if (!_client.IsConnected)
        {
            await TryConnectAndDiscoverAsync(ct).ConfigureAwait(false);
        }
        else
        {
            try
            {
                await _client.RequestRescanAsync(ct).ConfigureAwait(false);
                await Task.Delay(250, ct).ConfigureAwait(false);
            }
            catch { }

            await TryConnectAndDiscoverAsync(ct).ConfigureAwait(false);
        }

        return Devices;
    }

    private async Task<bool> DiscoverOrRefreshDevicesAsync(CancellationToken ct)
    {
        uint count = await _client.GetControllerCountAsync(ct).ConfigureAwait(false);
        var newlyDiscovered = new List<OpenRgbDevice>();
        var updatedDevices = new List<OpenRgbDevice>();

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
                        updatedDevices.Add(existing);
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

        lock (_devicesLock)
        {
            foreach (var dev in _devices)
            {
                if (dev.ControllerIndex >= count && dev.ConnectionState == DeviceConnectionState.Connected)
                {
                    dev.ConnectionState = DeviceConnectionState.Disconnected;
                    updatedDevices.Add(dev);
                }
            }
        }

        // Notify for any newly discovered devices outside lock
        foreach (var dev in newlyDiscovered)
        {
            DeviceDiscovered?.Invoke(this, new DeviceEventArgs(dev));
        }

        if (newlyDiscovered.Count > 0 || updatedDevices.Count > 0)
        {
            DevicesUpdated?.Invoke(this, EventArgs.Empty);
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
        bool stateChanged = false;
        lock (_devicesLock)
        {
            foreach (var dev in _devices)
            {
                if (dev.ConnectionState != DeviceConnectionState.Disconnected)
                {
                    dev.ConnectionState = DeviceConnectionState.Disconnected;
                    stateChanged = true;
                }
            }
        }

        if (stateChanged)
        {
            DevicesUpdated?.Invoke(this, EventArgs.Empty);
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
            uint protocolVersion = _client.ServerProtocolVersion;

            _ = reader.ReadUInt32(); // total size
            int rawType = reader.ReadInt32();
            string name = ReadLengthPrefixedString(reader);
            string vendor = protocolVersion >= 1 ? ReadLengthPrefixedString(reader) : string.Empty;
            string description = ReadLengthPrefixedString(reader);
            string version = ReadLengthPrefixedString(reader);
            string serial = ReadLengthPrefixedString(reader);
            string location = ReadLengthPrefixedString(reader);

            // Modes
            ushort modeCount = reader.ReadUInt16();
            int activeMode = reader.ReadInt32();
            for (int m = 0; m < modeCount; m++)
            {
                SkipModeData(reader, protocolVersion);
            }

            // Zones
            ushort zoneCount = reader.ReadUInt16();
            int totalZoneLeds = 0;
            for (int z = 0; z < zoneCount; z++)
            {
                ReadLengthPrefixedString(reader); // zone name
                reader.ReadInt32();  // zone type
                reader.ReadUInt32(); // leds_min
                reader.ReadUInt32(); // leds_max
                uint zLedsCount = reader.ReadUInt32();
                totalZoneLeds += (int)zLedsCount;

                ushort matrixLen = reader.ReadUInt16();
                if (matrixLen > 0)
                {
                    reader.ReadBytes(matrixLen); // matrix map data (height + width + data)
                }

                if (protocolVersion >= 4)
                {
                    ushort numSegments = reader.ReadUInt16();
                    for (int s = 0; s < numSegments; s++)
                    {
                        ReadLengthPrefixedString(reader); // segment name
                        reader.ReadInt32();  // segment type
                        reader.ReadUInt32(); // start_idx
                        reader.ReadUInt32(); // leds_count
                        if (protocolVersion >= 6)
                        {
                            ushort segMatLen = reader.ReadUInt16();
                            if (segMatLen > 0) reader.ReadBytes(segMatLen); // segment matrix map
                            reader.ReadUInt32(); // segment flags
                        }
                    }
                }

                if (protocolVersion >= 5)
                {
                    reader.ReadUInt32(); // zone flags
                }

                // Protocol v6: per-zone mode data
                if (protocolVersion >= 6)
                {
                    reader.ReadInt32();  // zone_active_mode
                    ushort zoneNumModes = reader.ReadUInt16();
                    for (int m = 0; m < zoneNumModes; m++)
                    {
                        SkipModeData(reader, protocolVersion);
                    }
                    ReadLengthPrefixedString(reader); // zone_display_name
                }
            }

            var deviceType = MapDeviceType(rawType);

            // LEDs
            ushort ledCount = reader.ReadUInt16();
            var ledCoordinates = new List<LedCoordinate>();

            for (int l = 0; l < ledCount; l++)
            {
                string ledName = ReadLengthPrefixedString(reader);
                if (protocolVersion < 6)
                {
                    reader.ReadUInt32(); // led_value (removed in v6)
                }

                // Calculate normalized coordinate
                float localX, localY;
                if (deviceType == RgbDeviceType.Keyboard)
                {
                    (localX, localY) = GetKeyboardLedCoordinate(l, ledCount, ledName);
                }
                else
                {
                    localX = ledCount > 1 ? (float)l / (ledCount - 1) : 0.5f;
                    localY = 0.5f;
                }

                ledCoordinates.Add(new LedCoordinate(l, localX, localY, ledName));
            }

            // Device-level colors block (always present, even if num_colors == 0)
            ushort numColors = reader.ReadUInt16();
            if (numColors > 0)
            {
                reader.ReadBytes(numColors * 4); // RGBColor[num_colors]
            }

            // Protocol v5: LED alternate display names + controller flags
            if (protocolVersion >= 5)
            {
                ushort numDisplayNames = reader.ReadUInt16();
                for (int d = 0; d < numDisplayNames; d++)
                {
                    ReadLengthPrefixedString(reader); // led_display_name
                }
                reader.ReadUInt32(); // controller flags
            }

            // Protocol v6: display_name + configuration
            if (protocolVersion >= 6)
            {
                ReadLengthPrefixedString(reader); // display_name
                uint configLen = reader.ReadUInt32();
                if (configLen > 0)
                {
                    reader.ReadBytes((int)configLen); // configuration JSON
                }
            }

            if (ledCoordinates.Count == 0 && totalZoneLeds > 0)
            {
                for (int l = 0; l < totalZoneLeds; l++)
                {
                    float localX, localY;
                    if (deviceType == RgbDeviceType.Keyboard)
                    {
                        (localX, localY) = GetKeyboardLedCoordinate(l, totalZoneLeds, null);
                    }
                    else
                    {
                        localX = totalZoneLeds > 1 ? (float)l / (totalZoneLeds - 1) : 0.5f;
                        localY = 0.5f;
                    }
                    ledCoordinates.Add(new LedCoordinate(l, localX, localY, $"LED {l + 1}"));
                }
            }

            if (ledCoordinates.Count == 0)
            {
                // Default single LED
                ledCoordinates.Add(new LedCoordinate(0, 0.5f, 0.5f, "Main LED"));
            }

            string devId = $"openrgb-{index}-{name.ToLowerInvariant().Replace(' ', '-')}";
            string displayName = string.IsNullOrWhiteSpace(vendor) ? name : $"{vendor} {name}";

            return new OpenRgbDevice(_client, index, devId, displayName, deviceType, ledCoordinates);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Skips all fields of one Mode Data block according to the negotiated protocol version.
    /// Per the official OpenRGB SDK spec:
    ///   mode_name (string), mode_value (int32, &lt;v6 only), mode_flags (uint32),
    ///   speed_min, speed_max (uint32), brightness_min, brightness_max (uint32, v3+),
    ///   colors_min, colors_max, speed (uint32), brightness (uint32, v3+),
    ///   direction, color_mode (uint32), num_colors (uint16), colors (4*N bytes)
    /// </summary>
    private static void SkipModeData(BinaryReader reader, uint protocolVersion)
    {
        ReadLengthPrefixedString(reader); // mode name
        if (protocolVersion < 6) reader.ReadInt32(); // mode_value (removed in v6)
        reader.ReadUInt32(); // mode_flags
        reader.ReadUInt32(); // speed_min
        reader.ReadUInt32(); // speed_max
        if (protocolVersion >= 3)
        {
            reader.ReadUInt32(); // brightness_min
            reader.ReadUInt32(); // brightness_max
        }
        reader.ReadUInt32(); // colors_min
        reader.ReadUInt32(); // colors_max
        reader.ReadUInt32(); // speed
        if (protocolVersion >= 3)
        {
            reader.ReadUInt32(); // brightness
        }
        reader.ReadUInt32(); // direction
        reader.ReadUInt32(); // color_mode
        ushort numModeColors = reader.ReadUInt16();
        if (numModeColors > 0)
        {
            reader.ReadBytes(numModeColors * 4); // mode colors
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

    private static (float x, float y) GetKeyboardLedCoordinate(int index, int totalCount, string? ledName)
    {
        if (totalCount == 87)
        {
            // Standard 87-key TKL layout (e.g. HyperX Alloy Origins Core)
            if (index < 16) return ((index + 0.5f) / 16f, 0.08f);
            if (index < 32) return ((index - 16 + 0.5f) / 16f, 0.25f);
            if (index < 48) return ((index - 32 + 0.5f) / 16f, 0.42f);
            if (index < 61) return ((index - 48 + 0.5f) / 13.5f, 0.58f);
            if (index < 75) return ((index - 61 + 0.5f) / 14.5f, 0.75f);
            return ((index - 75 + 0.5f) / 13f, 0.92f);
        }

        int rows = 6;
        int row = Math.Clamp((int)(index / (float)totalCount * rows), 0, rows - 1);
        int cols = (int)MathF.Ceiling(totalCount / (float)rows);
        if (cols <= 0) cols = 1;
        int col = index % cols;
        return ((col + 0.5f) / cols, (row + 0.5f) / rows);
    }

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
