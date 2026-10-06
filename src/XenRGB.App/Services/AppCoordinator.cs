using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.Core.Automation;
using XenRGB.Core.Canvas;
using XenRGB.Core.Configuration;
using XenRGB.Core.Devices;
using XenRGB.Core.Engine;
using XenRGB.Devices.Mock;
using XenRGB.Devices.OpenRgb;
using XenRGB.Devices.Wled;
using XenRGB.Effects;
using XenRGB.SDK;

namespace XenRGB.App.Services;

public enum OpenRgbStartupState
{
    None,
    Starting,
    Connecting,
    Discovering,
    Connected,
    Failed
}

/// <summary>
/// Central application coordinator uniting configuration, engine, canvas, devices, and automation.
/// </summary>
public sealed class AppCoordinator : IDisposable
{
    private static AppCoordinator? _instance;
    public static AppCoordinator Instance => _instance ?? throw new InvalidOperationException("AppCoordinator not initialized.");

    public JsonConfigurationService ConfigService { get; }
    public SpatialCanvas Canvas { get; }
    public RgbEngine Engine { get; }
    public EffectRegistry Effects { get; }
    public AutomationManager Automation { get; }
    public WindowsMonitorStateProvider MonitorProvider { get; }

    public MockDeviceProvider MockProvider { get; }
    public OpenRgbProvider OpenRgbProvider { get; }
    public WledProvider WledProvider { get; }
    public XenRGB.Devices.NativeJs.NativeJsProvider NativeJsProvider { get; }

    public List<IRgbDevice> AllDevices { get; } = new();

    public event EventHandler? DevicesChanged;
    public event EventHandler? StateChanged;
    public event EventHandler<OpenRgbStartupState>? OpenRgbStateChanged;

    public AppCoordinator()
    {
        _instance = this;

        ConfigService = new JsonConfigurationService();
        Canvas = new SpatialCanvas();
        Engine = new RgbEngine(Canvas);
        Effects = new EffectRegistry();

        MonitorProvider = new WindowsMonitorStateProvider();
        var inactivityProvider = new WindowsInactivityProvider();
        Automation = new AutomationManager(MonitorProvider, inactivityProvider, ConfigService.ActiveProfile.Automation);

        MockProvider = new MockDeviceProvider();
        OpenRgbProvider = new OpenRgbProvider(ConfigService.Settings.OpenRgbHost, ConfigService.Settings.OpenRgbPort);
        WledProvider = new WledProvider(ConfigService.Settings.WledDeviceIps);
        NativeJsProvider = new XenRGB.Devices.NativeJs.NativeJsProvider();

        // Wire automation state changes to RGB engine
        Automation.LightingStateChanged += (s, active) =>
        {
            Engine.SetLightingEnabled(active);
            StateChanged?.Invoke(this, EventArgs.Empty);
        };

        Automation.AutomationStatusChanged += (s, e) =>
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    public async Task InitializeAsync()
    {
        // 1. Load config
        await ConfigService.LoadAsync();
        var profile = ConfigService.ActiveProfile;

        // Sync automation config
        Automation.Config = profile.Automation;

        // 2. Load canvas placements from profile
        if (profile.Placements.Count > 0)
        {
            Canvas.LoadPlacements(profile.Placements);
        }

        // 3. Scan user effect directories
        string appDir = AppDomain.CurrentDomain.BaseDirectory;
        string rootEffectsDir = Path.Combine(appDir, "effects");
        if (Directory.Exists(rootEffectsDir))
        {
            await Effects.LoadEffectsFromDirectoryAsync(rootEffectsDir);
        }

        string appDataEffects = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XenRGB", "effects");
        if (Directory.Exists(appDataEffects))
        {
            await Effects.LoadEffectsFromDirectoryAsync(appDataEffects);
        }

        // 4. Set active effect and parameters
        var activeEffect = Effects.GetEffect(profile.SelectedEffectId) ?? Effects.AllEffects.FirstOrDefault();
        if (activeEffect != null)
        {
            activeEffect.Initialize(profile.EffectParameters);
            Engine.ActiveEffect = activeEffect;
        }

        Engine.MasterBrightness = profile.MasterBrightness;

        // 5. Initialize real hardware providers (MockProvider is excluded from production startup)
        // WireProvider(OpenRgbProvider);
        WireProvider(WledProvider);
        WireProvider(NativeJsProvider);

        OpenRgbProvider.DevicesUpdated += (s, e) =>
        {
            SyncProviderDevices(OpenRgbProvider.Devices);
            if (OpenRgbProvider.IsConnected && OpenRgbProvider.Devices.Count > 0)
            {
                OpenRgbStateChanged?.Invoke(this, OpenRgbStartupState.Connected);
            }
        };

        if (ConfigService.Settings.AutoConnectOpenRgb)
        {
            // _ = TryConnectOpenRgbAsync();
        }
        _ = Task.Run(async () =>
        {
            try
            {
                await NativeJsProvider.InitializeAsync();
                await NativeJsProvider.DiscoverDevicesAsync();
            }
            catch { }
        });

        if (ConfigService.Settings.WledDeviceIps.Count > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await WledProvider.InitializeAsync();
                }
                catch { }
            });
        }

        // 6. Start the engine loop
        Engine.Start();
    }

    public async Task TryConnectOpenRgbAsync()
    {
        try
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] Onboarding state changed: Starting");
            OpenRgbStateChanged?.Invoke(this, OpenRgbStartupState.Starting);
            bool serverRunning = await OpenRgbServerManager.EnsureServerRunningAsync(
                ConfigService.Settings.OpenRgbHost, 
                ConfigService.Settings.OpenRgbPort);

            if (!serverRunning)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] Onboarding state changed: Failed (server not running)");
                OpenRgbStateChanged?.Invoke(this, OpenRgbStartupState.Failed);
                return;
            }

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] Onboarding state changed: Connecting");
            OpenRgbStateChanged?.Invoke(this, OpenRgbStartupState.Connecting);
            
            // Hook into discovery internally or just rely on the first loop
            // OpenRgbProvider's TryConnectAndDiscoverAsync takes a bit, so we can say Discovering right away
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] Onboarding state changed: Discovering");
            OpenRgbStateChanged?.Invoke(this, OpenRgbStartupState.Discovering);

            await OpenRgbProvider.InitializeAsync();
            
            if (OpenRgbProvider.IsConnected)
            {
                if (OpenRgbProvider.Devices.Count > 0)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] Onboarding state changed: Connected (Devices found)");
                    OpenRgbStateChanged?.Invoke(this, OpenRgbStartupState.Connected);
                }
            }
            else
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] Onboarding state changed: Failed (Not connected)");
                OpenRgbStateChanged?.Invoke(this, OpenRgbStartupState.Failed);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] Onboarding state changed: Failed (Exception: {ex.Message})");
            OpenRgbStateChanged?.Invoke(this, OpenRgbStartupState.Failed);
        }
    }

    /// <summary>
    /// Explicitly enables simulated mock hardware devices for unit testing or offline UI development.
    /// </summary>
    public async Task EnableMockDevicesAsync()
    {
        WireProvider(MockProvider);
        await MockProvider.InitializeAsync();
    }

    private void SyncProviderDevices(IReadOnlyList<IRgbDevice> providerDevices)
    {
        lock (AllDevices)
        {
            foreach (var dev in providerDevices)
            {
                var existing = AllDevices.FirstOrDefault(d => d.Id == dev.Id);
                if (existing == null)
                {
                    AllDevices.Add(dev);
                    Engine.RegisterDevice(dev);
                    if (Canvas.GetPlacement(dev.Id) == null)
                    {
                        Canvas.AddOrUpdatePlacement(CreateDefaultPlacement(dev));
                    }
                }
                else
                {
                    if (Canvas.GetPlacement(dev.Id) == null)
                    {
                        Canvas.AddOrUpdatePlacement(CreateDefaultPlacement(dev));
                    }
                }
            }
        }

        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void WireProvider(IDeviceProvider provider)
    {
        provider.DeviceDiscovered += (s, e) =>
        {
            lock (AllDevices)
            {
                if (!AllDevices.Any(d => d.Id == e.Device.Id))
                {
                    AllDevices.Add(e.Device);
                    Engine.RegisterDevice(e.Device);

                    // Ensure device has a default canvas placement if not already present
                    if (Canvas.GetPlacement(e.Device.Id) == null)
                    {
                        var defaultPlacement = CreateDefaultPlacement(e.Device);
                        Canvas.AddOrUpdatePlacement(defaultPlacement);
                    }
                }
            }
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        };

        provider.DeviceRemoved += (s, e) =>
        {
            lock (AllDevices)
            {
                AllDevices.RemoveAll(d => d.Id == e.Device.Id);
                Engine.UnregisterDevice(e.Device.Id);
            }
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        };
    }

    private CanvasDevicePlacement CreateDefaultPlacement(IRgbDevice device)
    {
        return device.DeviceType switch
        {
            RgbDeviceType.Keyboard => new CanvasDevicePlacement(device.Id, device.Name, 0.5f, 0.65f, 0.45f, 0.22f),
            RgbDeviceType.LedStrip => new CanvasDevicePlacement(device.Id, device.Name, 0.5f, 0.92f, 0.90f, 0.05f),
            RgbDeviceType.Motherboard => new CanvasDevicePlacement(device.Id, device.Name, 0.80f, 0.35f, 0.20f, 0.25f),
            RgbDeviceType.Mouse => new CanvasDevicePlacement(device.Id, device.Name, 0.80f, 0.65f, 0.12f, 0.15f),
            RgbDeviceType.Lightbar => new CanvasDevicePlacement(device.Id, device.Name, 0.5f, 0.10f, 0.60f, 0.06f),
            _ => new CanvasDevicePlacement(device.Id, device.Name, 0.5f, 0.5f, 0.25f, 0.15f)
        };
    }

    public void SelectEffect(string effectId)
    {
        var effect = Effects.GetEffect(effectId);
        if (effect != null)
        {
            effect.Initialize();
            Engine.ActiveEffect = effect;
            ConfigService.ActiveProfile.SelectedEffectId = effectId;
            StateChanged?.Invoke(this, EventArgs.Empty);
            _ = SaveCurrentProfileAsync();
        }
    }

    public async Task SaveCurrentProfileAsync()
    {
        var profile = ConfigService.ActiveProfile;
        profile.MasterBrightness = Engine.MasterBrightness;
        profile.SelectedEffectId = Engine.ActiveEffect?.Id ?? "cotton-candy";
        profile.Placements = Canvas.Placements.ToList();
        profile.Automation = Automation.Config;

        await ConfigService.SaveAsync().ConfigureAwait(false);
    }

    public void Dispose()
    {
        Engine.Dispose();
        Automation.Dispose();
        MonitorProvider.Dispose();
        MockProvider.Dispose();
        OpenRgbProvider.Dispose();
        WledProvider.Dispose();
        NativeJsProvider.Dispose();
    }
}
