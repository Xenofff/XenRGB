using System;
using System.Runtime.InteropServices;

namespace XenRGB.Core.Automation;

/// <summary>
/// Monitors Windows display state transitions (Display On / Off / Dimmed).
/// </summary>
public sealed class WindowsMonitorStateProvider : IMonitorStateProvider
{
    public bool IsMonitorOn { get; private set; } = true;
    public event EventHandler<bool>? MonitorStateChanged;

    private static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new("6fe69556-704a-47a0-8f24-c1019f6160c4");
    private IntPtr _hPowerNotify = IntPtr.Zero;

    public WindowsMonitorStateProvider()
    {
        // Default to on
        IsMonitorOn = true;
    }

    /// <summary>
    /// Can be called with a Window handle (HWND) to hook Win32 power notifications.
    /// </summary>
    public void HookWindowHandle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || _hPowerNotify != IntPtr.Zero)
            return;

        try
        {
            var guid = GUID_CONSOLE_DISPLAY_STATE;
            _hPowerNotify = RegisterPowerSettingNotification(hwnd, ref guid, 0);
        }
        catch
        {
            // Ignore if notification cannot be registered
        }
    }

    /// <summary>
    /// Invoked when a WM_POWERBROADCAST (PBT_POWERSETTINGCHANGE) message is received by the application window.
    /// </summary>
    public void ProcessPowerBroadcastMessage(IntPtr wParam, IntPtr lParam)
    {
        const int PBT_POWERSETTINGCHANGE = 0x8013;
        if (wParam.ToInt32() == PBT_POWERSETTINGCHANGE && lParam != IntPtr.Zero)
        {
            try
            {
                var setting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(lParam);
                if (setting.PowerSetting == GUID_CONSOLE_DISPLAY_STATE && setting.DataLength >= 4)
                {
                    IntPtr pData = IntPtr.Add(lParam, Marshal.SizeOf<POWERBROADCAST_SETTING>());
                    int state = Marshal.ReadInt32(pData);
                    // 0: Off, 1: On, 2: Dimmed
                    bool isOn = state != 0;
                    SetMonitorState(isOn);
                }
            }
            catch
            {
                // Safety guard against corrupted broadcast
            }
        }
    }

    public void SetMonitorState(bool isOn)
    {
        if (IsMonitorOn != isOn)
        {
            IsMonitorOn = isOn;
            MonitorStateChanged?.Invoke(this, isOn);
        }
    }

    public void Dispose()
    {
        if (_hPowerNotify != IntPtr.Zero)
        {
            UnregisterPowerSettingNotification(_hPowerNotify);
            _hPowerNotify = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public uint DataLength;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid PowerSettingGuid, uint Flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr Handle);
}
