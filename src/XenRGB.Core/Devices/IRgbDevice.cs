using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.SDK;

namespace XenRGB.Core.Devices;

/// <summary>
/// Abstraction for an RGB hardware device (e.g. OpenRGB controller, WLED node, virtual mock).
/// </summary>
public interface IRgbDevice : IDisposable
{
    string Id { get; }
    string Name { get; }
    string ProviderId { get; }
    RgbDeviceType DeviceType { get; }
    int LedCount { get; }
    IReadOnlyList<LedCoordinate> LedCoordinates { get; }
    RgbColor[] CurrentColors { get; }
    DeviceConnectionState ConnectionState { get; }

    bool IsEnabled { get; set; }
    float Brightness { get; set; }

    Task UpdateLedsAsync(ReadOnlyMemory<RgbColor> colors, CancellationToken ct = default);
    Task SetPowerAsync(bool on, CancellationToken ct = default);
}
