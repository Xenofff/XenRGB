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
    private const int ThreadPowerThrottling = 9;

    private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
    private const uint PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION = 0x4;

    private const uint THREAD_POWER_THROTTLING_CURRENT_VERSION = 1;
    private const uint THREAD_POWER_THROTTLING_EXECUTION_SPEED = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct THREAD_POWER_THROTTLING_STATE
    {
        public uint Version;
        public uint ControlMask;
        public uint StateMask;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(
        IntPtr hProcess,
        int processInformationClass,
        ref PROCESS_POWER_THROTTLING_STATE processInformation,
        uint processInformationSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetThreadInformation(
        IntPtr hThread,
        int threadInformationClass,
        ref THREAD_POWER_THROTTLING_STATE threadInformation,
        uint threadInformationSize);

    /// <summary>
    /// Configures the process to run in Windows Efficiency Mode (EcoQoS) and BelowNormal priority.
    /// This routes background rendering to E-cores and allows the CPU to drop down to minimum idle clock (0.8 GHz).
    /// </summary>
    public static void EnableEcoQoS()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
            return;

        try
        {
            // Set process priority class to BelowNormal so Windows scheduler allows CPU C-state downclocking
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch { }

        try
        {
            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED | PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION,
                StateMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED | PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION
            };

            // Use Win32 GetCurrentProcess() pseudo-handle which has PROCESS_ALL_ACCESS
            IntPtr hProcess = GetCurrentProcess();
            SetProcessInformation(
                hProcess,
                ProcessPowerThrottling,
                ref state,
                (uint)Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
        }
        catch
        {
            // Ignore if OS does not support or policy denies
        }
    }

    /// <summary>
    /// Configures the calling thread to execute under Windows EcoQoS efficiency mode.
    /// </summary>
    public static void EnableThreadEcoQoS()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
            return;

        try
        {
            var state = new THREAD_POWER_THROTTLING_STATE
            {
                Version = THREAD_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = THREAD_POWER_THROTTLING_EXECUTION_SPEED,
                StateMask = THREAD_POWER_THROTTLING_EXECUTION_SPEED
            };

            IntPtr hThread = GetCurrentThread();
            SetThreadInformation(
                hThread,
                ThreadPowerThrottling,
                ref state,
                (uint)Marshal.SizeOf<THREAD_POWER_THROTTLING_STATE>());
        }
        catch { }
    }
}
