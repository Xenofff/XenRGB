using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using XenRGB.App.Services;
using XenRGB.Core.Devices;

namespace XenRGB.App.Views;

public sealed partial class DevicesPage : Page
{
    private readonly AppCoordinator _coord = AppCoordinator.Instance;
    public ObservableCollection<DeviceViewModel> DeviceViewModels { get; } = new();

    public DevicesPage()
    {
        InitializeComponent();
        DevicesList.ItemsSource = DeviceViewModels;

        _coord.DevicesChanged += OnCoordinatorDevicesChanged;
        Loaded += (s, e) => RefreshDevices();
    }

    private void OnCoordinatorDevicesChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RefreshDevices);
    }

    private void RefreshDevices()
    {
        DeviceViewModels.Clear();
        foreach (var dev in _coord.AllDevices)
        {
            DeviceViewModels.Add(new DeviceViewModel(dev));
        }

        EmptyStateCard.Visibility = DeviceViewModels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DevicesList.Visibility = DeviceViewModels.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdateStatusBanner();
    }

    private void UpdateStatusBanner()
    {
        var openRgb = _coord.OpenRgbProvider;
        if (openRgb.IsConnected)
        {
            int devCount = openRgb.Devices.Count;
            if (devCount > 0)
            {
                BackendStatusInfoBar.Severity = InfoBarSeverity.Success;
                BackendStatusInfoBar.Title = "OpenRGB Connected";
                BackendStatusInfoBar.Message = $"Connected to OpenRGB server at {openRgb.Host}:{openRgb.Port}. {devCount} device(s) active.";
            }
            else
            {
                BackendStatusInfoBar.Severity = InfoBarSeverity.Informational;
                BackendStatusInfoBar.Title = "OpenRGB Connected (No Devices Found)";
                BackendStatusInfoBar.Message = $"Connected to OpenRGB server at {openRgb.Host}:{openRgb.Port}, but OpenRGB reported 0 supported devices. Make sure your devices appear inside the OpenRGB desktop application.";
            }
        }
        else
        {
            BackendStatusInfoBar.Severity = InfoBarSeverity.Warning;
            BackendStatusInfoBar.Title = "OpenRGB Server Offline";
            BackendStatusInfoBar.Message = $"OpenRGB server is not running at {openRgb.Host}:{openRgb.Port}. Launch OpenRGB and start the SDK Server (Settings -> OpenRGB Server -> Start Server), then click 'Scan Devices'.";
        }
    }

    private async void AddWledButton_Click(object sender, RoutedEventArgs e)
    {
        WledIpTextBox.Text = string.Empty;
        AddWledDialog.XamlRoot = this.XamlRoot;
        var result = await AddWledDialog.ShowAsync();

        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(WledIpTextBox.Text))
        {
            string ip = WledIpTextBox.Text.Trim();
            if (!_coord.ConfigService.Settings.WledDeviceIps.Contains(ip))
            {
                _coord.ConfigService.Settings.WledDeviceIps.Add(ip);
                await _coord.ConfigService.SaveAsync();
            }

            try
            {
                await _coord.WledProvider.AddDeviceByIp(ip);
            }
            catch { }
        }
    }

    private async void ScanDevicesButton_Click(object sender, RoutedEventArgs e)
    {
        ScanDevicesButton.IsEnabled = false;
        ScanButtonText.Text = "Scanning...";
        ScanProgressRing.IsActive = true;
        ScanProgressRing.Visibility = Visibility.Visible;

        try
        {
            await _coord.OpenRgbProvider.RescanAndDiscoverAsync();
            await _coord.WledProvider.DiscoverDevicesAsync();
            RefreshDevices();
        }
        finally
        {
            ScanProgressRing.IsActive = false;
            ScanProgressRing.Visibility = Visibility.Collapsed;
            ScanButtonText.Text = "Scan Devices";
            ScanDevicesButton.IsEnabled = true;
        }
    }
}

public sealed class DeviceViewModel : INotifyPropertyChanged
{
    private readonly IRgbDevice _device;

    public string Name => _device.Name;
    public string ProviderId => _device.ProviderId.Replace("-provider", "").ToUpperInvariant();
    public string DetailsText => $"{_device.DeviceType} • {_device.LedCount} LEDs • {_device.ConnectionState}";

    public bool IsEnabled
    {
        get => _device.IsEnabled;
        set
        {
            if (_device.IsEnabled != value)
            {
                _device.IsEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public double BrightnessPercent
    {
        get => _device.Brightness * 100.0;
        set
        {
            float norm = (float)(value / 100.0);
            if (Math.Abs(_device.Brightness - norm) > 0.001f)
            {
                _device.Brightness = norm;
                OnPropertyChanged();
            }
        }
    }

    public DeviceViewModel(IRgbDevice device)
    {
        _device = device;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? prop = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
