using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.Core.Devices;

namespace XenRGB.Devices.Wled;

/// <summary>
/// Device provider managing WLED-powered ESP32/ESP8266 LED strips over Wi-Fi/LAN.
/// </summary>
public sealed class WledProvider : IDeviceProvider
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly UdpClient _udp = new();
    private readonly ConcurrentDictionary<string, WledDevice> _devices = new(StringComparer.OrdinalIgnoreCase);

    public string ProviderId => "wled-provider";
    public string DisplayName => "WLED (Wi-Fi)";
    public bool IsConnected => true;

    public event EventHandler<DeviceEventArgs>? DeviceDiscovered;
    public event EventHandler<DeviceEventArgs>? DeviceRemoved;

    public WledProvider(IEnumerable<string>? initialIps = null)
    {
        if (initialIps != null)
        {
            foreach (var ip in initialIps)
            {
                _ = AddDeviceByIp(ip);
            }
        }
    }

    public async Task<WledDevice> AddDeviceByIp(string ipAddress)
    {
        ipAddress = ipAddress.Trim();
        if (_devices.TryGetValue(ipAddress, out var existing))
        {
            return existing;
        }

        var device = new WledDevice(ipAddress, _http, _udp);
        _devices[ipAddress] = device;

        await device.RefreshStateAsync().ConfigureAwait(false);
        DeviceDiscovered?.Invoke(this, new DeviceEventArgs(device));
        return device;
    }

    public bool RemoveDevice(string ipAddress)
    {
        if (_devices.TryRemove(ipAddress, out var device))
        {
            DeviceRemoved?.Invoke(this, new DeviceEventArgs(device));
            device.Dispose();
            return true;
        }
        return false;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await DiscoverDevicesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<IRgbDevice>> DiscoverDevicesAsync(CancellationToken ct = default)
    {
        var tasks = _devices.Values.Select(d => d.RefreshStateAsync(ct));
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return _devices.Values.ToList().AsReadOnly();
    }

    public void Dispose()
    {
        foreach (var dev in _devices.Values)
        {
            dev.Dispose();
        }
        _devices.Clear();
        _http.Dispose();
        _udp.Dispose();
    }
}
