using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;
using XenRGB.App.Services;
using XenRGB.SDK;

namespace XenRGB.App.Views;

public sealed partial class EffectsPage : Page
{
    private readonly AppCoordinator _coord = AppCoordinator.Instance;

    public EffectsPage()
    {
        InitializeComponent();
        Loaded += (s, e) => RefreshUi();
    }

    private void RefreshUi()
    {
        EffectsGallery.ItemsSource = _coord.Effects.AllEffects.ToList();

        var active = _coord.Engine.ActiveEffect;
        if (active != null)
        {
            ActiveEffectTitle.Text = active.Name;
            ActiveEffectCategory.Text = active.Manifest.Category ?? "Effect";
            ActiveEffectDescription.Text = active.Description;

            RebuildParametersUi(active);
        }
    }

    private void RebuildParametersUi(IEffect effect)
    {
        ParametersContainer.Children.Clear();

        if (effect.Parameters.Count == 0)
        {
            ParametersContainer.Children.Add(new TextBlock
            {
                Text = "No configurable parameters for this effect.",
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"]
            });
            return;
        }

        foreach (var param in effect.Parameters)
        {
            var card = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            card.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            labelPanel.Children.Add(new TextBlock { Text = param.DisplayName, FontWeight = Microsoft.UI.Text.FontWeights.Medium });
            if (!string.IsNullOrEmpty(param.Description))
            {
                labelPanel.Children.Add(new TextBlock
                {
                    Text = param.Description,
                    FontSize = 11,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                });
            }
            Grid.SetColumn(labelPanel, 0);
            card.Children.Add(labelPanel);

            if (param.Type == EffectParameterType.Float)
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
                float min = param.MinValue ?? 0f;
                float max = param.MaxValue ?? 1f;
                float step = param.Step ?? 0.05f;
                float current = effect.GetParameter(param.Name) is float f ? f : (float)(param.DefaultValue ?? 1f);

                var readout = new TextBlock { Text = $"{current:F2}", Width = 45, VerticalAlignment = VerticalAlignment.Center };
                var slider = new Slider
                {
                    Width = 260,
                    Minimum = min,
                    Maximum = max,
                    StepFrequency = step,
                    Value = current
                };

                slider.ValueChanged += (s, e) =>
                {
                    effect.SetParameter(param.Name, (float)slider.Value);
                    readout.Text = $"{slider.Value:F2}";
                };

                panel.Children.Add(slider);
                panel.Children.Add(readout);
                Grid.SetColumn(panel, 1);
                card.Children.Add(panel);
            }
            else if (param.Type == EffectParameterType.Color)
            {
                var colorPicker = new ColorPicker
                {
                    IsAlphaEnabled = false,
                    IsHexInputVisible = true,
                    Width = 300
                };

                RgbColor initialColor = RgbColor.White;
                var currentVal = effect.GetParameter(param.Name);
                if (currentVal is RgbColor rgb) initialColor = rgb;
                else if (currentVal is string hex) initialColor = RgbColor.FromHex(hex);
                else if (param.DefaultValue is RgbColor def) initialColor = def;
                else if (param.DefaultValue is string defHex) initialColor = RgbColor.FromHex(defHex);

                colorPicker.Color = Color.FromArgb(255, initialColor.R, initialColor.G, initialColor.B);

                colorPicker.ColorChanged += (s, args) =>
                {
                    var newRgb = new RgbColor(args.NewColor.R, args.NewColor.G, args.NewColor.B);
                    effect.SetParameter(param.Name, newRgb);
                };

                Grid.SetColumn(colorPicker, 1);
                card.Children.Add(colorPicker);
            }
            else if (param.Type == EffectParameterType.Boolean)
            {
                var toggle = new ToggleSwitch
                {
                    IsOn = effect.GetParameter(param.Name) is bool b ? b : (bool)(param.DefaultValue ?? false)
                };
                toggle.Toggled += (s, e) =>
                {
                    effect.SetParameter(param.Name, toggle.IsOn);
                };

                Grid.SetColumn(toggle, 1);
                card.Children.Add(toggle);
            }

            ParametersContainer.Children.Add(card);
        }
    }

    private void ActivateEffect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string effectId)
        {
            _coord.SelectEffect(effectId);
            RefreshUi();
        }
    }

    private async void ReloadEffectsButton_Click(object sender, RoutedEventArgs e)
    {
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        string rootEffectsDir = Path.Combine(appDir, "effects");
        if (Directory.Exists(rootEffectsDir))
        {
            await _coord.Effects.LoadEffectsFromDirectoryAsync(rootEffectsDir);
        }

        string appDataEffects = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XenRGB", "effects");
        if (Directory.Exists(appDataEffects))
        {
            await _coord.Effects.LoadEffectsFromDirectoryAsync(appDataEffects);
        }

        RefreshUi();
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        string effectsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XenRGB", "effects");
        if (!Directory.Exists(effectsDir))
        {
            Directory.CreateDirectory(effectsDir);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = effectsDir,
            UseShellExecute = true
        });
    }
}
