using System;
using System.Runtime.InteropServices;
using XenRGB.App.Services;

namespace XenRGB.App.Services;

/// <summary>
/// Native Windows notification area (System Tray) manager with zero external dependencies.
/// </summary>
public sealed class SystemTrayService : IDisposable
{
    private const int WM_USER = 0x0400;
    public const int WM_TRAYICON = WM_USER + 101;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;

    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONDBLCLK = 0x0203;

    private const uint TPM_BOTTOMALIGN = 0x0020;
    private const uint TPM_RIGHTALIGN = 0x0008;
    private const uint TPM_RETURNCMD = 0x0100;

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint MF_GRAYED = 0x00000001;

    private const int CMD_STATUS = 1001;
    private const int CMD_TURN_ON = 1002;
    private const int CMD_TURN_OFF = 1003;
    private const int CMD_GOOD_NIGHT = 1004;
    private const int CMD_OPEN = 1005;
    private const int CMD_EXIT = 1006;

    private readonly IntPtr _hwnd;
    private readonly NOTIFYICONDATA _nid;
    private bool _isAdded;
    private IntPtr _hIcon = IntPtr.Zero;

    public event Action? OpenRequested;
    public event Action? ExitRequested;

    public SystemTrayService(IntPtr hwnd)
    {
        _hwnd = hwnd;

        // Load application icon from process or shell
        _hIcon = GetAppIcon();

        _nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "XenRGB - RGB Lighting Controller"
        };

        _isAdded = Shell_NotifyIcon(NIM_ADD, ref _nid);
    }

    public void UpdateTooltip(string text)
    {
        if (!_isAdded) return;
        var nid = _nid;
        nid.uFlags = NIF_TIP;
        nid.szTip = text.Length > 127 ? text.Substring(0, 127) : text;
        Shell_NotifyIcon(NIM_MODIFY, ref nid);
    }

    public void ProcessWindowMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != WM_TRAYICON) return;

        int mouseMsg = (int)lParam;
        if (mouseMsg == WM_LBUTTONUP || mouseMsg == WM_LBUTTONDBLCLK)
        {
            OpenRequested?.Invoke();
        }
        else if (mouseMsg == WM_RBUTTONUP)
        {
            ShowContextMenu();
        }
    }

    public void ShowContextMenu()
    {
        GetCursorPos(out POINT pt);
        SetForegroundWindow(_hwnd);

        IntPtr hMenu = CreatePopupMenu();
        try
        {
            var coord = AppCoordinator.Instance;
            bool isLightingOn = coord.Engine.IsLightingEnabled;
            string statusText = isLightingOn ? "● Lighting On" : "○ Lighting Off";
            if (coord.Automation.IsGoodNightActive) statusText = "☾ Good Night Active";

            AppendMenu(hMenu, MF_STRING | MF_GRAYED, (UIntPtr)CMD_STATUS, statusText);
            AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, string.Empty);
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_TURN_ON, "Turn On");
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_TURN_OFF, "Turn Off");
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_GOOD_NIGHT, "Good Night");
            AppendMenu(hMenu, MF_SEPARATOR, UIntPtr.Zero, string.Empty);
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_OPEN, "Open XenRGB");
            AppendMenu(hMenu, MF_STRING, (UIntPtr)CMD_EXIT, "Exit");

            uint cmd = TrackPopupMenuEx(hMenu, TPM_RETURNCMD | TPM_BOTTOMALIGN | TPM_RIGHTALIGN, pt.X, pt.Y, _hwnd, IntPtr.Zero);

            switch (cmd)
            {
                case CMD_TURN_ON:
                    coord.Automation.TurnOn();
                    break;
                case CMD_TURN_OFF:
                    coord.Automation.TurnOff();
                    break;
                case CMD_GOOD_NIGHT:
                    coord.Automation.GoodNight();
                    break;
                case CMD_OPEN:
                    OpenRequested?.Invoke();
                    break;
                case CMD_EXIT:
                    ExitRequested?.Invoke();
                    break;
            }
        }
        finally
        {
            DestroyMenu(hMenu);
        }
    }

    private static IntPtr GetAppIcon()
    {
        try
        {
            IntPtr hModule = GetModuleHandle(null);
            IntPtr icon = LoadIcon(hModule, (IntPtr)1);
            if (icon != IntPtr.Zero) return icon;
        }
        catch { }

        // Fallback default app icon
        return LoadIcon(IntPtr.Zero, (IntPtr)32512); // IDI_APPLICATION
    }

    public void Dispose()
    {
        if (_isAdded)
        {
            var nid = _nid;
            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _isAdded = false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lpTPMParams);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
