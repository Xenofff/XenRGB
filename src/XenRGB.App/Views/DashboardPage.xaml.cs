using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using XenRGB.App.Services;
using XenRGB.Core.Canvas;
using XenRGB.Core.Devices;

namespace XenRGB.App.Views;

public sealed partial class DashboardPage : Page
{
    private readonly AppCoordinator _coord = AppCoordinator.Instance;
    private readonly DispatcherTimer _ledRefreshTimer = new();
    private bool _isInitializing = true;

    private CanvasDevicePlacement? _selectedPlacement;
    private bool _isDragging;
    private Point _dragStartPos;
    private float _origPlacementX;
    private float _origPlacementY;

    // Cache of UI elements representing each device placement on the Canvas
    private readonly Dictionary<string, (Border Container, ItemsControl LedDots)> _deviceVisuals = new();

    public DashboardPage()
    {
        InitializeComponent();

        _coord.StateChanged += OnCoordinatorStateChanged;
        _coord.DevicesChanged += OnCoordinatorDevicesChanged;
        _coord.Canvas.CanvasChanged += OnCanvasChanged;

        _ledRefreshTimer.Interval = TimeSpan.FromMilliseconds(1000); // 1 FPS UI preview — imperceptible for ambient lighting, minimal CPU wake cost
        _ledRefreshTimer.Tick += OnLedRefreshTick;

        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        UpdateStatusUi();
        RebuildCanvasVisuals();
        _ledRefreshTimer.Start();
        _isInitializing = false;
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _ledRefreshTimer.Stop();
    }

    private void OnCoordinatorStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateStatusUi);
    }

    private void OnCoordinatorDevicesChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(RebuildCanvasVisuals);
    }

    private void OnCanvasChanged(object? sender, EventArgs e)
    {
        if (!_isDragging)
        {
            DispatcherQueue.TryEnqueue(RebuildCanvasVisuals);
        }
    }

    private void UpdateStatusUi()
    {
        bool isActive = _coord.Engine.IsLightingEnabled;
        if (_coord.Automation.IsGoodNightActive)
        {
            StatusDot.Background = new SolidColorBrush(Color.FromArgb(255, 139, 92, 246)); // Purple/Moon
            StatusText.Text = "Good Night Mode (Lighting Off)";
        }
        else if (isActive)
        {
            StatusDot.Background = new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)); // Green
            StatusText.Text = "Lighting Active";
        }
        else
        {
            StatusDot.Background = new SolidColorBrush(Color.FromArgb(255, 156, 163, 175)); // Gray
            StatusText.Text = _coord.Automation.StatusDescription;
        }

        ActiveEffectText.Text = $"Effect: {_coord.Engine.ActiveEffect?.Name ?? "None"}";
        MasterBrightnessSlider.Value = _coord.Engine.MasterBrightness * 100f;
        BrightnessLabel.Text = $"{(int)(_coord.Engine.MasterBrightness * 100f)}%";
    }

    private void RebuildCanvasVisuals()
    {
        SpatialSurface.Children.Clear();
        _deviceVisuals.Clear();

        double canvasWidth = SpatialSurface.ActualWidth > 0 ? SpatialSurface.ActualWidth : 800;
        double canvasHeight = SpatialSurface.ActualHeight > 0 ? SpatialSurface.ActualHeight : 420;

        foreach (var placement in _coord.Canvas.Placements)
        {
            var device = _coord.AllDevices.FirstOrDefault(d => d.Id == placement.DeviceId);
            if (device == null)
                continue;

            double w = placement.Width * canvasWidth;
            double h = placement.Height * canvasHeight;
            double x = (placement.X * canvasWidth) - (w / 2.0);
            double y = (placement.Y * canvasHeight) - (h / 2.0);

            var border = new Border
            {
                Width = Math.Max(w, 40),
                Height = Math.Max(h, 24),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(220, 24, 30, 42)),
                BorderBrush = placement == _selectedPlacement
                    ? new SolidColorBrush(Color.FromArgb(255, 59, 130, 246))
                    : new SolidColorBrush(Color.FromArgb(120, 75, 85, 99)),
                BorderThickness = new Thickness(placement == _selectedPlacement ? 2 : 1),
                Tag = placement
            };

            var grid = new Grid();

            // Label
            var text = new TextBlock
            {
                Text = placement.DeviceName,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.LightGray),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(4, 3, 4, 0)
            };
            grid.Children.Add(text);

            // LED Dots Container
            var ledDotsControl = new ItemsControl
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 16, 6, 6)
            };

            var panelTemplate = new ItemsPanelTemplate();
            // Wrap panel or uniform dots
            grid.Children.Add(ledDotsControl);

            border.Child = grid;

            // Rotation
            if (Math.Abs(placement.RotationDegrees) > 0.01f)
            {
                border.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
                border.RenderTransform = new RotateTransform { Angle = placement.RotationDegrees };
            }

            Canvas.SetLeft(border, Math.Clamp(x, 0, canvasWidth - w));
            Canvas.SetTop(border, Math.Clamp(y, 0, canvasHeight - h));

            SpatialSurface.Children.Add(border);
            _deviceVisuals[placement.DeviceId] = (border, ledDotsControl);
        }
    }

    private void OnLedRefreshTick(object? sender, object e)
    {
        try
        {
            if (!_coord.Engine.IsLightingEnabled || Visibility != Visibility.Visible) return;

            foreach (var (devId, (border, _)) in _deviceVisuals)
            {
                var dev = _coord.AllDevices.FirstOrDefault(d => d.Id == devId);
                if (dev == null || dev.CurrentColors == null || dev.CurrentColors.Length == 0)
                    continue;

                // Sample an average or center color to glow the border background
                var midColor = dev.CurrentColors[dev.CurrentColors.Length / 2];
                var targetColor = Color.FromArgb(160, midColor.R, midColor.G, midColor.B);

                if (border.Background is SolidColorBrush currentBrush && currentBrush.Color == targetColor)
                    continue;

                border.Background = new SolidColorBrush(targetColor);
            }
        }
        catch
        {
            // Ignore transient render tick errors
        }
    }

    private void SpatialSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RebuildCanvasVisuals();
    }

    private void SpatialSurface_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(SpatialSurface);
        Border? hit = null;
        var elem = e.OriginalSource as FrameworkElement;
        while (elem != null)
        {
            if (elem is Border b && b.Tag is CanvasDevicePlacement)
            {
                hit = b;
                break;
            }
            elem = VisualTreeHelper.GetParent(elem) as FrameworkElement;
        }

        if (hit != null && hit.Tag is CanvasDevicePlacement placement)
        {
            _selectedPlacement = placement;
            _isDragging = true;
            _dragStartPos = pt.Position;
            _origPlacementX = placement.X;
            _origPlacementY = placement.Y;

            SpatialSurface.CapturePointer(e.Pointer);
            UpdateInspector();
            
            if (_deviceVisuals.TryGetValue(placement.DeviceId, out var visuals))
            {
                visuals.Container.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 59, 130, 246));
                visuals.Container.BorderThickness = new Thickness(2);
            }
            e.Handled = true;
        }
        else
        {
            _selectedPlacement = null;
            UpdateInspector();
            RebuildCanvasVisuals();
        }
    }

    private void SpatialSurface_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _selectedPlacement == null) return;

        var pt = e.GetCurrentPoint(SpatialSurface).Position;
        double dx = pt.X - _dragStartPos.X;
        double dy = pt.Y - _dragStartPos.Y;

        double canvasWidth = SpatialSurface.ActualWidth > 0 ? SpatialSurface.ActualWidth : 800;
        double canvasHeight = SpatialSurface.ActualHeight > 0 ? SpatialSurface.ActualHeight : 420;

        float normDx = (float)(dx / canvasWidth);
        float normDy = (float)(dy / canvasHeight);

        _selectedPlacement.X = Math.Clamp(_origPlacementX + normDx, 0.05f, 0.95f);
        _selectedPlacement.Y = Math.Clamp(_origPlacementY + normDy, 0.05f, 0.95f);

        if (_deviceVisuals.TryGetValue(_selectedPlacement.DeviceId, out var visuals))
        {
            double w = _selectedPlacement.Width * canvasWidth;
            double h = _selectedPlacement.Height * canvasHeight;
            double x = (_selectedPlacement.X * canvasWidth) - (w / 2.0);
            double y = (_selectedPlacement.Y * canvasHeight) - (h / 2.0);
            Canvas.SetLeft(visuals.Container, Math.Clamp(x, 0, canvasWidth - w));
            Canvas.SetTop(visuals.Container, Math.Clamp(y, 0, canvasHeight - h));
        }

        _coord.Canvas.AddOrUpdatePlacement(_selectedPlacement);
        UpdateInspectorValues();
    }

    private void SpatialSurface_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            SpatialSurface.ReleasePointerCapture(e.Pointer);
            _ = _coord.SaveCurrentProfileAsync();
        }
    }

    private void UpdateInspector()
    {
        if (_selectedPlacement == null)
        {
            InspectorPanel.Visibility = Visibility.Collapsed;
            return;
        }

        InspectorPanel.Visibility = Visibility.Visible;
        SelectedDeviceName.Text = _selectedPlacement.DeviceName;

        var dev = _coord.AllDevices.FirstOrDefault(d => d.Id == _selectedPlacement.DeviceId);
        SelectedDeviceDetails.Text = dev != null ? $"{dev.DeviceType} • {dev.LedCount} LEDs" : "Simulated Device";

        UpdateInspectorValues();
    }

    private void UpdateInspectorValues()
    {
        if (_selectedPlacement == null) return;

        SliderPosX.Value = _selectedPlacement.X * 100f;
        SliderPosY.Value = _selectedPlacement.Y * 100f;
        SliderRotation.Value = _selectedPlacement.RotationDegrees;
    }

    private void SliderPosition_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_selectedPlacement == null || _isDragging) return;

        _selectedPlacement.X = (float)(SliderPosX.Value / 100.0);
        _selectedPlacement.Y = (float)(SliderPosY.Value / 100.0);
        _coord.Canvas.AddOrUpdatePlacement(_selectedPlacement);
    }

    private void SliderRotation_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_selectedPlacement == null) return;

        _selectedPlacement.RotationDegrees = (float)SliderRotation.Value;
        _coord.Canvas.AddOrUpdatePlacement(_selectedPlacement);
    }

    private void RemoveSelectedDevice_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPlacement != null)
        {
            _coord.Canvas.RemovePlacement(_selectedPlacement.DeviceId);
            _selectedPlacement = null;
            UpdateInspector();
            _ = _coord.SaveCurrentProfileAsync();
        }
    }

    private void TurnOnButton_Click(object sender, RoutedEventArgs e) => _coord.Automation.TurnOn();
    private void TurnOffButton_Click(object sender, RoutedEventArgs e) => _coord.Automation.TurnOff();
    private void GoodNightButton_Click(object sender, RoutedEventArgs e) => _coord.Automation.GoodNight();

    private void MasterBrightnessSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isInitializing || BrightnessLabel == null || MasterBrightnessSlider == null) return;
        float factor = (float)(MasterBrightnessSlider.Value / 100.0);
        if (_coord?.Engine != null)
        {
            _coord.Engine.MasterBrightness = factor;
        }
        BrightnessLabel.Text = $"{(int)MasterBrightnessSlider.Value}%";
    }

    private void ResetLayout_Click(object sender, RoutedEventArgs e)
    {
        _coord.Canvas.Clear();
        foreach (var dev in _coord.AllDevices)
        {
            _coord.Canvas.AddOrUpdatePlacement(new CanvasDevicePlacement(dev.Id, dev.Name, 0.5f, 0.5f, 0.3f, 0.15f));
        }
        _ = _coord.SaveCurrentProfileAsync();
    }

    private void SaveLayout_Click(object sender, RoutedEventArgs e)
    {
        _ = _coord.SaveCurrentProfileAsync();
    }
}
