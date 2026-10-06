using System;
using System.IO;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace XenRGB.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        XenRGB.Core.Engine.PowerEfficiencyHelper.EnableEcoQoS();

        UnhandledException += (sender, args) =>
        {
            try
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XenRGB");
                Directory.CreateDirectory(dir);
                var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [WinUI UnhandledException] Message: {args.Message}\nException: {args.Exception}\nStackTrace:\n{args.Exception?.StackTrace}\n\n";
                File.AppendAllText(System.IO.Path.Combine(dir, "crash.log"), text);
            }
            catch { }
        };

        try
        {
            var configSvc = new XenRGB.Core.Configuration.JsonConfigurationService();
            configSvc.LoadAsync().GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(configSvc.Settings.Language))
            {
                Microsoft.Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = configSvc.Settings.Language;
            }
        }
        catch { }

        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
