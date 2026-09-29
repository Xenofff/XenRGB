using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.Core.Devices;
using XenRGB.SDK;

namespace XenRGB.Devices.Mock;

/// <summary>
/// Virtual RGB device for testing, simulation, and canvas visualization.
/// </summary>
public sealed class MockRgbDevice : IRgbDevice
{
    public string Id { get; }
    public string Name { get; }
    public string ProviderId => "mock-provider";
    public RgbDeviceType DeviceType { get; }
    public int LedCount => LedCoordinates.Count;
    public IReadOnlyList<LedCoordinate> LedCoordinates { get; }
    public RgbColor[] CurrentColors { get; }
    public DeviceConnectionState ConnectionState { get; set; } = DeviceConnectionState.Connected;

    public bool IsEnabled { get; set; } = true;
    public float Brightness { get; set; } = 1.0f;
    public bool IsPoweredOn { get; private set; } = true;

    private readonly DeviceFrameThrottler _throttler;
    public DeviceFrameThrottler Throttler => _throttler;
    public long TransmissionCount { get; private set; }

    public MockRgbDevice(
        string id,
        string name,
        RgbDeviceType type,
        IReadOnlyList<LedCoordinate> coordinates,
        TimeSpan? heartbeatInterval = null,
        TimeProvider? timeProvider = null)
    {
        Id = id;
        Name = name;
        DeviceType = type;
        LedCoordinates = coordinates;
        CurrentColors = new RgbColor[coordinates.Count];
        _throttler = new DeviceFrameThrottler(heartbeatInterval, timeProvider);
    }

    public Task UpdateLedsAsync(ReadOnlyMemory<RgbColor> colors, CancellationToken ct = default)
    {
        if (!IsEnabled || ConnectionState != DeviceConnectionState.Connected)
            return Task.CompletedTask;

        int copyCount = Math.Min(colors.Length, CurrentColors.Length);
        if (!_throttler.ShouldTransmit(colors.Span[..copyCount]))
            return Task.CompletedTask;

        colors.Span[..copyCount].CopyTo(CurrentColors.AsSpan(0, copyCount));
        _throttler.MarkTransmitted(colors.Span[..copyCount]);
        TransmissionCount++;
        return Task.CompletedTask;
    }

    public Task SetPowerAsync(bool on, CancellationToken ct = default)
    {
        IsPoweredOn = on;
        _throttler.Reset();
        if (!on)
        {
            Array.Fill(CurrentColors, RgbColor.Black);
        }
        return Task.CompletedTask;
    }

    public void Dispose() { }
}
