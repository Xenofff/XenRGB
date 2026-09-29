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
