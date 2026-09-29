using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace XenRGB.Core.Devices;

/// <summary>
/// Hardware provider responsible for discovering, connecting to, and managing RGB devices.
/// </summary>
public interface IDeviceProvider : IDisposable
{
    string ProviderId { get; }
    string DisplayName { get; }
    bool IsConnected { get; }

    event EventHandler<DeviceEventArgs>? DeviceDiscovered;
    event EventHandler<DeviceEventArgs>? DeviceRemoved;

    Task InitializeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<IRgbDevice>> DiscoverDevicesAsync(CancellationToken ct = default);
}
