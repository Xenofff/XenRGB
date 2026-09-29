using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.Core.Devices;

namespace XenRGB.Devices.OpenRgb;

/// <summary>
/// Device provider connecting to local or network OpenRGB instance.
/// </summary>
public sealed class OpenRgbProvider : IDeviceProvider
{
    private readonly OpenRgbClient _client = new();
    private readonly List<IRgbDevice> _devices = new();
    private readonly string _host;
    private readonly int _port;

    public string ProviderId => "openrgb-provider";
    public string DisplayName => "OpenRGB";
    public bool IsConnected => _client.IsConnected;

    public event EventHandler<DeviceEventArgs>? DeviceDiscovered;
    public event EventHandler<DeviceEventArgs>? DeviceRemoved;

    public OpenRgbProvider(string host = "127.0.0.1", int port = 6742)
    {
        _host = host;
        _port = port;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        bool connected = await _client.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
        if (connected)
        {
            await DiscoverDevicesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<IRgbDevice>> DiscoverDevicesAsync(CancellationToken ct = default)
    {
        if (!_client.IsConnected)
        {
            bool ok = await _client.ConnectAsync(_host, _port, ct).ConfigureAwait(false);
            if (!ok) return Array.Empty<IRgbDevice>();
        }

        uint count = await _client.GetControllerCountAsync(ct).ConfigureAwait(false);
        _devices.Clear();

        for (uint i = 0; i < count; i++)
        {
            try
            {
                byte[] raw = await _client.GetControllerDataRawAsync(i, ct).ConfigureAwait(false);
                var device = ParseControllerData(i, raw);
                if (device != null)
                {
                    _devices.Add(device);
                    DeviceDiscovered?.Invoke(this, new DeviceEventArgs(device));
                }
            }
            catch
            {
                // Continue with other controllers if one fails
            }
        }

        return _devices.AsReadOnly();
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
        _client.Dispose();
        foreach (var dev in _devices)
        {
            DeviceRemoved?.Invoke(this, new DeviceEventArgs(dev));
            dev.Dispose();
        }
        _devices.Clear();
    }
}
