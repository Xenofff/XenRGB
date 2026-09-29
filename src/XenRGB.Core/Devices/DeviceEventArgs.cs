using System;

namespace XenRGB.Core.Devices;

public sealed class DeviceEventArgs : EventArgs
{
    public IRgbDevice Device { get; }

    public DeviceEventArgs(IRgbDevice device)
    {
        Device = device;
    }
}
