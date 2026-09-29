using System;

namespace XenRGB.Core.Automation;

/// <summary>
/// Source of monitor display power state notifications.
/// </summary>
public interface IMonitorStateProvider : IDisposable
{
    bool IsMonitorOn { get; }
    event EventHandler<bool>? MonitorStateChanged;
}
