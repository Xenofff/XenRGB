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

    public MockRgbDevice(string id, string name, RgbDeviceType type, IReadOnlyList<LedCoordinate> coordinates)
    {
        Id = id;
        Name = name;
        DeviceType = type;
        LedCoordinates = coordinates;
        CurrentColors = new RgbColor[coordinates.Count];
    }

    public Task UpdateLedsAsync(ReadOnlyMemory<RgbColor> colors, CancellationToken ct = default)
    {
        int copyCount = Math.Min(colors.Length, CurrentColors.Length);
        colors.Span[..copyCount].CopyTo(CurrentColors.AsSpan(0, copyCount));
        return Task.CompletedTask;
    }

    public Task SetPowerAsync(bool on, CancellationToken ct = default)
    {
        IsPoweredOn = on;
        if (!on)
        {
            Array.Fill(CurrentColors, RgbColor.Black);
        }
        return Task.CompletedTask;
    }

    public void Dispose() { }
}
