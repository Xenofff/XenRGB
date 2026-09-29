using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XenRGB.App.Services;

namespace XenRGB.App.Views;

public sealed partial class AutomationPage : Page
{
    private readonly AppCoordinator _coord = AppCoordinator.Instance;
    private bool _isInitializing = true;

    public AutomationPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        _coord.Automation.AutomationStatusChanged += (s, e) => DispatcherQueue.TryEnqueue(UpdateGoodNightBadge);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        var cfg = _coord.Automation.Config;

        MonitorOffToggle.IsOn = cfg.TurnOffOnMonitorOff;
        RestoreOnWakeToggle.IsOn = cfg.RestoreOnMonitorOn;
        DelaySlider.Value = cfg.MonitorOffDelaySeconds;
        DelayLabel.Text = $"{cfg.MonitorOffDelaySeconds}s";

        InactivitySlider.Value = cfg.InactivityTimeoutMinutes;
        InactivityLabel.Text = cfg.InactivityTimeoutMinutes == 0 ? "Disabled" : $"{cfg.InactivityTimeoutMinutes} min";

        UpdateGoodNightBadge();
        _isInitializing = false;
    }

    private void UpdateGoodNightBadge()
    {
        bool isGoodNight = _coord.Automation.IsGoodNightActive;
        GoodNightBadge.Visibility = isGoodNight ? Visibility.Visible : Visibility.Collapsed;
        ToggleGoodNightButton.Content = isGoodNight ? "Resume Normal Lighting" : "Activate Good Night";
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var cfg = _coord.Automation.Config;
        cfg.TurnOffOnMonitorOff = MonitorOffToggle.IsOn;
        cfg.RestoreOnMonitorOn = RestoreOnWakeToggle.IsOn;
        cfg.MonitorOffDelaySeconds = (int)DelaySlider.Value;
        DelayLabel.Text = $"{(int)DelaySlider.Value}s";

        _ = _coord.SaveCurrentProfileAsync();
    }

    private void InactivitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing) return;

        int mins = (int)InactivitySlider.Value;
        _coord.Automation.Config.InactivityTimeoutMinutes = mins;
        InactivityLabel.Text = mins == 0 ? "Disabled" : $"{mins} min";

        _ = _coord.SaveCurrentProfileAsync();
    }

    private void ToggleGoodNight_Click(object sender, RoutedEventArgs e)
    {
        if (_coord.Automation.IsGoodNightActive)
        {
            _coord.Automation.TurnOn();
        }
        else
        {
            _coord.Automation.GoodNight();
        }
        UpdateGoodNightBadge();
    }
}
