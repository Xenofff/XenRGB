using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.Core.Devices;
using XenRGB.SDK;

namespace XenRGB.Devices.OpenRgb;

/// <summary>
/// RGB device backed by an OpenRGB controller.
/// </summary>
public sealed class OpenRgbDevice : IRgbDevice
{
    private readonly IOpenRgbClient _client;
    private readonly DeviceFrameThrottler _throttler;
    public uint ControllerIndex { get; }

    public string Id { get; }
    public string Name { get; }
    public string ProviderId => "openrgb-provider";
    public RgbDeviceType DeviceType { get; }
    public int LedCount { get; }
    public IReadOnlyList<LedCoordinate> LedCoordinates { get; }
    public RgbColor[] CurrentColors { get; }
    public DeviceConnectionState ConnectionState { get; set; } = DeviceConnectionState.Connected;

    public bool IsEnabled { get; set; } = true;
    public float Brightness { get; set; } = 1.0f;

    public DeviceFrameThrottler Throttler => _throttler;
    public long TransmissionCount { get; private set; }

    private readonly uint[] _bgrBuffer;

    public OpenRgbDevice(
        IOpenRgbClient client,
        uint controllerIndex,
        string id,
        string name,
        RgbDeviceType deviceType,
        IReadOnlyList<LedCoordinate> coordinates,
        TimeSpan? heartbeatInterval = null,
        TimeProvider? timeProvider = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ControllerIndex = controllerIndex;
        Id = id;
        Name = name;
        DeviceType = deviceType;
        LedCoordinates = coordinates;
        LedCount = coordinates.Count;
        CurrentColors = new RgbColor[LedCount];
        _bgrBuffer = new uint[LedCount];
        _throttler = new DeviceFrameThrottler(heartbeatInterval, timeProvider);
    }

    public async Task UpdateLedsAsync(ReadOnlyMemory<RgbColor> colors, CancellationToken ct = default)
    {
        if (!IsEnabled || ConnectionState != DeviceConnectionState.Connected)
            return;

        int count = Math.Min(colors.Length, LedCount);
        if (!_throttler.ShouldTransmit(colors.Span[..count]))
            return;

        for (int i = 0; i < count; i++)
        {
            var c = colors.Span[i];
            CurrentColors[i] = c;
            // OpenRGB expects 0x00BBGGRR format
            _bgrBuffer[i] = (uint)(c.R | (c.G << 8) | (c.B << 16));
        }

        try
        {
            await _client.SendUpdateLedsAsync(ControllerIndex, _bgrBuffer.AsMemory(0, count), ct).ConfigureAwait(false);
            _throttler.MarkTransmitted(colors.Span[..count]);
            TransmissionCount++;
        }
        catch
        {
            ConnectionState = DeviceConnectionState.Error;
            _throttler.Reset();
        }
    }

    public async Task SetPowerAsync(bool on, CancellationToken ct = default)
    {
        _throttler.Reset();
        if (!on)
        {
            Array.Fill(CurrentColors, RgbColor.Black);
            Array.Fill(_bgrBuffer, 0u);
            try
            {
                await _client.SendUpdateLedsAsync(ControllerIndex, _bgrBuffer.AsMemory(0, LedCount), ct).ConfigureAwait(false);
            }
            catch
            {
                // Ignore power off errors
            }
        }
    }

    public void Dispose() { }
}
