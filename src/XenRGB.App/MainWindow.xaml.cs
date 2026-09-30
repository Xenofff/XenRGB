using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;
using XenRGB.App.Services;

namespace XenRGB.App;

public sealed partial class MainWindow : Window
{
    private readonly AppCoordinator _coord;
    private SystemTrayService? _tray;
    private IntPtr _hwnd;
    private SUBCLASSPROC? _subclassDelegate;
    private bool _isExiting;

    public MainWindow()
    {
        InitializeComponent();

        _coord = new AppCoordinator();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        _hwnd = WindowNative.GetWindowHandle(this);

        // Size and center window
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(1100, 750));

        try
        {
            appWindow.SetIcon("Assets/AppIcon.ico");
        }
        catch { }

        // Setup system tray
        try
        {
            _tray = new SystemTrayService(_hwnd);
            _tray.OpenRequested += OnTrayOpenRequested;
            _tray.ExitRequested += OnTrayExitRequested;
        }
        catch { }

        // Subclass window procedure for Win32 tray messages and power broadcasts
        try
        {
            _subclassDelegate = new SUBCLASSPROC(WindowSubclassProc);
            SetWindowSubclass(_hwnd, _subclassDelegate, (UIntPtr)1001, IntPtr.Zero);
        }
        catch { }

        // Hook monitor power broadcast notifications
        try
        {
            _coord.MonitorProvider.HookWindowHandle(_hwnd);
        }
        catch { }

        // Handle window close -> minimize to tray if configured
        appWindow.Closing += OnAppWindowClosing;

        // Navigate to MainPage
        RootFrame.Navigate(typeof(MainPage));

        // Start coordinator in background
        _ = _coord.InitializeAsync();
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_isExiting) return;

        if (_coord.ConfigService.Settings.MinimizeToTrayOnClose)
        {
            args.Cancel = true;
            sender.Hide();
            _tray?.UpdateTooltip("XenRGB - Running in background");
        }
    }

    private void OnTrayOpenRequested()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Show();
            SetForegroundWindow(_hwnd);
        });
    }

    private void OnTrayExitRequested()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _isExiting = true;
            _tray?.Dispose();
            _coord.Dispose();
            Application.Current.Exit();
        });
    }

    private IntPtr WindowSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        const uint WM_POWERBROADCAST = 0x0218;
        const uint WM_GETMINMAXINFO = 0x0024;

        if (uMsg == SystemTrayService.WM_TRAYICON)
        {
            _tray?.ProcessWindowMessage((int)uMsg, wParam, lParam);
            return IntPtr.Zero;
        }

        if (uMsg == WM_GETMINMAXINFO)
        {
            var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
            uint dpi = GetDpiForWindow(hWnd);
            float scale = (dpi == 0 ? 96f : dpi) / 96f;
            minMaxInfo.ptMinTrackSize.X = (int)(1000 * scale);
            minMaxInfo.ptMinTrackSize.Y = (int)(700 * scale);
            Marshal.StructureToPtr(minMaxInfo, lParam, false);
            return IntPtr.Zero;
        }

        if (uMsg == WM_POWERBROADCAST)
        {
            _coord.MonitorProvider.ProcessPowerBroadcastMessage(wParam, lParam);
        }

        return DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    private delegate IntPtr SUBCLASSPROC(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public System.Drawing.Point ptReserved;
        public System.Drawing.Point ptMaxSize;
        public System.Drawing.Point ptMaxPosition;
        public System.Drawing.Point ptMinTrackSize;
        public System.Drawing.Point ptMaxTrackSize;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
