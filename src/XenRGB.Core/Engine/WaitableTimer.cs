using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace XenRGB.Core.Engine;

/// <summary>
/// Provides a high-precision sleep that does NOT raise the global Windows timer resolution.
///
/// Unlike Thread.Sleep() which forces the system timer to ~1 ms globally
/// (preventing CPU C-state transitions and keeping frequency at turbo levels),
/// this uses CreateWaitableTimerExW + SetWaitableTimerEx with a tolerable delay.
///
/// The "tolerable delay" tells the kernel it's OK to coalesce this timer wake
/// with other system activity, allowing the CPU to stay in deep C-states (C3/C6)
/// and drop to idle frequencies (0.8 GHz).
/// </summary>
public static class WaitableTimer
{
    // Timer creation flags
    private const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x00000002;

    // Timer access rights
    private const uint TIMER_ALL_ACCESS = 0x1F0003;

    // Wait return values
    private const uint WAIT_OBJECT_0 = 0x00000000;
    private const uint WAIT_TIMEOUT = 0x00000102;
    private const uint INFINITE = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWaitableTimerExW(
        IntPtr lpTimerAttributes,
        string? lpTimerName,
        uint dwFlags,
        uint dwDesiredAccess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetWaitableTimerEx(
        IntPtr hTimer,
        ref long lpDueTime,
        int lPeriod,
        IntPtr pfnCompletionRoutine,
        IntPtr lpArgToCompletionRoutine,
        IntPtr wakeContext,
        uint tolerableDelay);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetWaitableTimer(
        IntPtr hTimer,
        ref long lpDueTime,
        int lPeriod,
        IntPtr pfnCompletionRoutine,
        IntPtr lpArgToCompletionRoutine,
        bool fResume);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [ThreadStatic]
    private static IntPtr _threadTimer;

    [ThreadStatic]
    private static bool _timerCreated;

    /// <summary>
    /// Gets or creates a per-thread waitable timer handle.
    /// Using thread-static avoids contention and handle creation overhead on the hot path.
    /// </summary>
    private static IntPtr GetOrCreateTimer()
    {
        if (_timerCreated && _threadTimer != IntPtr.Zero)
            return _threadTimer;

        // Standard waitable timer allows timer coalescing and CPU C-state power saving (C3/C6+).
        // CREATE_WAITABLE_TIMER_HIGH_RESOLUTION prevents CPU low-power states per Microsoft docs.
        IntPtr handle = CreateWaitableTimerExW(
            IntPtr.Zero,
            null,
            0,
            TIMER_ALL_ACCESS);

        _threadTimer = handle;
        _timerCreated = true;
        return handle;
    }

    /// <summary>
    /// Sleeps for the specified number of milliseconds without raising the global timer resolution.
    ///
    /// Uses SetWaitableTimerEx with a tolerable delay of 32 ms, which tells Windows
    /// "I need to wake up in ~sleepMs, but it's OK to be up to 32 ms late."
    /// This allows the CPU to remain in deep C-states and drop to idle frequency.
    ///
    /// For RGB at 25 FPS (40 ms/frame), ±32 ms tolerance means effective 16-33 FPS,
    /// which is imperceptible for ambient lighting effects.
    /// </summary>
    public static void SleepWithTolerance(long sleepMs, uint toleranceMs = 32)
    {
        if (sleepMs <= 0)
            return;

        if (!OperatingSystem.IsWindowsVersionAtLeast(10))
        {
            // Fallback for very old OS — Thread.Sleep is the only option
            Thread.Sleep((int)sleepMs);
            return;
        }

        IntPtr timer = GetOrCreateTimer();
        if (timer == IntPtr.Zero)
        {
            // CreateWaitableTimerExW failed — fallback
            Thread.Sleep((int)sleepMs);
            return;
        }

        // Negative value = relative time in 100-nanosecond intervals
        long dueTime = -sleepMs * 10_000L;

        bool set = SetWaitableTimerEx(
            timer,
            ref dueTime,
            0,                  // no periodic repeat
            IntPtr.Zero,        // no completion routine
            IntPtr.Zero,        // no arg
            IntPtr.Zero,        // no wake context (REASON_CONTEXT)
            toleranceMs);

        if (!set)
        {
            // SetWaitableTimerEx not supported — try legacy SetWaitableTimer
            set = SetWaitableTimer(
                timer,
                ref dueTime,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                false);
        }

        if (set)
        {
            WaitForSingleObject(timer, (uint)(sleepMs + toleranceMs + 100));
        }
        else
        {
            // All native methods failed — last resort
            Thread.Sleep((int)sleepMs);
        }
    }
}
