using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.Core.Devices;
using XenRGB.SDK;

namespace XenRGB.Devices.Wled;

/// <summary>
/// Hardware representation for a WLED device running on an ESP8266/ESP32.
/// Supports both HTTP JSON control and realtime DRGB/WARLS UDP streaming (port 21324).
/// </summary>
public sealed class WledDevice : IRgbDevice
{
    private const int WledUdpPort = 21324;
    private readonly HttpClient _http;
    private readonly UdpClient _udp;
    private readonly byte[] _udpBuffer;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public string IpAddress { get; }
    public string Id { get; }
    public string Name { get; private set; }
    public string ProviderId => "wled-provider";
    public RgbDeviceType DeviceType => RgbDeviceType.LedStrip;
    public int LedCount { get; private set; }
    public IReadOnlyList<LedCoordinate> LedCoordinates { get; private set; }
    public RgbColor[] CurrentColors { get; private set; }
    public DeviceConnectionState ConnectionState { get; set; } = DeviceConnectionState.Connecting;

    public bool IsEnabled { get; set; } = true;
    public float Brightness { get; set; } = 1.0f;

    public WledDevice(string ipAddress, HttpClient http, UdpClient udp, string? name = null, int ledCount = 60)
    {
        IpAddress = ipAddress;
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _udp = udp ?? throw new ArgumentNullException(nameof(udp));

        Id = $"wled-{ipAddress.Replace('.', '-')}";
        Name = name ?? $"WLED ({ipAddress})";
        LedCount = ledCount;

        CurrentColors = new RgbColor[LedCount];
        _udpBuffer = new byte[2 + LedCount * 3];
        _udpBuffer[0] = 2; // Protocol 2: DRGB
        _udpBuffer[1] = 2; // Timeout: 2 seconds

        LedCoordinates = GenerateStripCoordinates(LedCount);
    }

    public async Task RefreshStateAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _http.GetStringAsync($"http://{IpAddress}/json", ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(res);
            var root = doc.RootElement;

            if (root.TryGetProperty("info", out var info))
            {
                if (info.TryGetProperty("name", out var nameProp))
                {
                    Name = nameProp.GetString() ?? Name;
                }

                if (info.TryGetProperty("leds", out var ledsProp) &&
                    ledsProp.TryGetProperty("count", out var countProp))
                {
                    int detectedCount = countProp.GetInt32();
                    if (detectedCount > 0 && detectedCount != LedCount)
                    {
                        UpdateLedCount(detectedCount);
                    }
                }
            }

            ConnectionState = DeviceConnectionState.Connected;
        }
        catch
        {
            ConnectionState = DeviceConnectionState.Error;
        }
    }

    private void UpdateLedCount(int newCount)
    {
        LedCount = newCount;
        CurrentColors = new RgbColor[LedCount];
        LedCoordinates = GenerateStripCoordinates(LedCount);
    }

    public async Task UpdateLedsAsync(ReadOnlyMemory<RgbColor> colors, CancellationToken ct = default)
    {
        if (!IsEnabled || ConnectionState != DeviceConnectionState.Connected)
            return;

        int count = Math.Min(colors.Length, LedCount);
        for (int i = 0; i < count; i++)
        {
            CurrentColors[i] = colors.Span[i];
        }

        // Send via realtime UDP (DRGB mode) for lowest latency
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            int requiredLength = 2 + count * 3;
            byte[] packet = _udpBuffer;
            if (packet.Length < requiredLength)
            {
                packet = new byte[requiredLength];
            }

            packet[0] = 2; // DRGB mode
            packet[1] = 2; // Timeout: 2 seconds

            for (int i = 0; i < count; i++)
            {
                var c = colors.Span[i];
                packet[2 + i * 3] = c.R;
                packet[2 + i * 3 + 1] = c.G;
                packet[2 + i * 3 + 2] = c.B;
            }

            await _udp.SendAsync(packet.AsMemory(0, requiredLength), IpAddress, WledUdpPort).ConfigureAwait(false);
        }
        catch
        {
            // UDP failure shouldn't crash app
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task SetPowerAsync(bool on, CancellationToken ct = default)
    {
        try
        {
            string payload = on ? "{\"on\":true}" : "{\"on\":false}";
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _http.PostAsync($"http://{IpAddress}/json/state", content, ct).ConfigureAwait(false);
        }
        catch
        {
            // Ignore power error
        }
    }

    private static IReadOnlyList<LedCoordinate> GenerateStripCoordinates(int count)
    {
        var list = new List<LedCoordinate>(count);
        for (int i = 0; i < count; i++)
        {
            float x = count > 1 ? (float)i / (count - 1) : 0.5f;
            list.Add(new LedCoordinate(i, x, 0.5f, $"LED {i + 1}"));
        }
        return list;
    }

    public void Dispose()
    {
        _sendLock.Dispose();
    }
}
