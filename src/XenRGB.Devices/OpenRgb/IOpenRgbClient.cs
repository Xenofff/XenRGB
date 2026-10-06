using System;
using System.Threading;
using System.Threading.Tasks;

namespace XenRGB.Devices.OpenRgb;

/// <summary>
/// Abstraction for communicating with the OpenRGB SDK server.
/// </summary>
public interface IOpenRgbClient : IDisposable
{
    bool IsConnected { get; }
    uint ServerProtocolVersion { get; }

    Task<bool> ConnectAsync(string host, int port, CancellationToken ct = default);
    Task<uint> GetControllerCountAsync(CancellationToken ct = default);
    Task<byte[]> GetControllerDataRawAsync(uint controllerIndex, CancellationToken ct = default);
    Task SendUpdateLedsAsync(uint controllerIndex, ReadOnlyMemory<uint> bgrColors, CancellationToken ct = default);
    Task ResizeZoneAsync(uint controllerIndex, int zoneIndex, int newSize, CancellationToken ct = default) => Task.CompletedTask;
    Task SetCustomModeAsync(uint controllerIndex, CancellationToken ct = default) => Task.CompletedTask;
    Task RequestRescanAsync(CancellationToken ct = default) => Task.CompletedTask;
    Task DrainPushPacketsAsync(CancellationToken ct, int maxWaitMs) => Task.CompletedTask;
    void Disconnect();
}
