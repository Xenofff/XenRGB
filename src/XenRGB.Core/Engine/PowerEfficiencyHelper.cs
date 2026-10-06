using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XenRGB.Core.Engine;

/// <summary>
/// Configures Windows EcoQoS (Power Throttling) and process priority
/// so the CPU can downclock to idle state (e.g. 0.8 GHz) instead of holding turbo frequencies.
/// </summary>
public static class PowerEfficiencyHelper
{
    private const int ProcessPowerThrottling = 4;
    private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess,
        int processInformationClass,
        ref PROCESS_POWER_THROTTLING_STATE processInformation,
        uint processInformationSize);

    public static void EnableEcoQoS()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
            return;

        try
        {
            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED
            };

            IntPtr handle = Process.GetCurrentProcess().Handle;
            SetProcessInformation(
                handle,
                ProcessPowerThrottling,
                ref state,
                (uint)Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
        }
        catch
        {
            // Ignore if OS does not support or policy denies
        }
    }
}
