using System;
using System.Runtime.InteropServices;

namespace XenRGB.Core.Automation;

/// <summary>
/// Measures user idle duration via Windows user32!GetLastInputInfo.
/// </summary>
public sealed class WindowsInactivityProvider : IUserInactivityProvider
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public TimeSpan GetInactivityDuration()
    {
        try
        {
            var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (GetLastInputInfo(ref lii))
            {
                uint currentTick = (uint)Environment.TickCount;
                uint idleTicks = currentTick - lii.dwTime;
                return TimeSpan.FromMilliseconds(idleTicks);
            }
        }
        catch
        {
            // Fallback if P/Invoke is restricted
        }

        return TimeSpan.Zero;
    }
}
