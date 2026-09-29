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
        _tray = new SystemTrayService(_hwnd);
        _tray.OpenRequested += OnTrayOpenRequested;
        _tray.ExitRequested += OnTrayExitRequested;

        // Subclass window procedure for Win32 tray messages and power broadcasts
        _subclassDelegate = new SUBCLASSPROC(WindowSubclassProc);
        SetWindowSubclass(_hwnd, _subclassDelegate, 1001, IntPtr.Zero);

        // Hook monitor power broadcast notifications
        _coord.MonitorProvider.HookWindowHandle(_hwnd);

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

        if (uMsg == SystemTrayService.WM_TRAYICON)
        {
            _tray?.ProcessWindowMessage((int)uMsg, wParam, lParam);
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
    private static extern bool SetWindowSubclass(IntPtr hWnd, SUBCLASSPROC pfnSubclass, uint uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
