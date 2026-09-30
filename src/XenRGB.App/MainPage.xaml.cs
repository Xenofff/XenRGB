using System;
using Microsoft.UI.Xaml.Controls;
using XenRGB.App.Views;

namespace XenRGB.App;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    private void NavView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Select Dashboard by default on launch
        NavView.SelectedItem = NavView.MenuItems[0];

        // Subscribe to OpenRGB startup state
        Services.AppCoordinator.Instance.OpenRgbStateChanged += AppCoordinator_OpenRgbStateChanged;
    }

    private void AppCoordinator_OpenRgbStateChanged(object? sender, Services.OpenRgbStartupState e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e)
            {
                case Services.OpenRgbStartupState.Starting:
                    StartupOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                    StartupTitle.Text = GetString("StartupTitle_Starting");
                    StartupMessage.Text = GetString("StartupMessage_Starting");
                    StartupProgress.IsActive = true;
                    StartupErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    RetryButton.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    break;
                case Services.OpenRgbStartupState.Connecting:
                    StartupOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                    StartupTitle.Text = GetString("StartupTitle_Connecting");
                    StartupMessage.Text = GetString("StartupMessage_Connecting");
                    break;
                case Services.OpenRgbStartupState.Discovering:
                    StartupOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                    StartupTitle.Text = GetString("StartupTitle_Discovering");
                    StartupMessage.Text = GetString("StartupMessage_Discovering");
                    break;
                case Services.OpenRgbStartupState.Connected:
                    StartupOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
                    break;
                case Services.OpenRgbStartupState.Failed:
                    StartupOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                    StartupTitle.Text = GetString("StartupTitle_Failed");
                    StartupMessage.Text = GetString("StartupMessage_Failed");
                    StartupProgress.IsActive = false;
                    StartupErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                    RetryButton.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                    break;
            }
        });
    }

    private string GetString(string key)
    {
        try
        {
            var loader = new Microsoft.Windows.ApplicationModel.Resources.ResourceManager().MainResourceMap;
            return loader.TryGetValue($"Resources/{key}")?.ValueAsString ?? "";
        }
        catch
        {
            return "";
        }
    }

    private void ContinueButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        StartupOverlay.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;
    }

    private void RetryButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // Re-trigger auto connect if clicked
        if (Services.AppCoordinator.Instance.ConfigService.Settings.AutoConnectOpenRgb)
        {
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await Services.AppCoordinator.Instance.TryConnectOpenRgbAsync();
            });
        }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            ContentFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
        {
            Type pageType = tag switch
            {
                "dashboard" => typeof(DashboardPage),
                "devices" => typeof(DevicesPage),
                "effects" => typeof(EffectsPage),
                "automation" => typeof(AutomationPage),
                _ => typeof(DashboardPage)
            };

            ContentFrame.Navigate(pageType);
        }
    }
}
