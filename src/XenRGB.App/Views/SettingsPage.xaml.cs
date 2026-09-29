using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XenRGB.App.Services;

namespace XenRGB.App.Views;

public sealed partial class SettingsPage : Page
{
    private readonly AppCoordinator _coord = AppCoordinator.Instance;
    private bool _isInitializing = true;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        var settings = _coord.ConfigService.Settings;

        OpenRgbHostBox.Text = settings.OpenRgbHost;
        OpenRgbPortBox.Text = settings.OpenRgbPort.ToString();

        FpsComboBox.SelectedIndex = settings.TargetFps switch
        {
            15 => 0,
            60 => 2,
            _ => 1 // 30 FPS
        };

        MinimizeToTrayToggle.IsOn = settings.MinimizeToTrayOnClose;
        _isInitializing = false;
    }

    private async void ConnectOpenRgb_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(OpenRgbPortBox.Text, out int port))
        {
            _coord.ConfigService.Settings.OpenRgbHost = OpenRgbHostBox.Text.Trim();
            _coord.ConfigService.Settings.OpenRgbPort = port;
            await _coord.ConfigService.SaveAsync();

            await _coord.OpenRgbProvider.DiscoverDevicesAsync();
        }
    }

    private void FpsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing) return;

        int fps = FpsComboBox.SelectedIndex switch
        {
            0 => 15,
            2 => 60,
            _ => 30
        };

        _coord.Engine.TargetFps = fps;
        _coord.ConfigService.Settings.TargetFps = fps;
        _ = _coord.ConfigService.SaveAsync();
    }

    private void MinimizeToTrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        _coord.ConfigService.Settings.MinimizeToTrayOnClose = MinimizeToTrayToggle.IsOn;
        _ = _coord.ConfigService.SaveAsync();
    }
}
