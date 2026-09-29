using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.Core.Canvas;

namespace XenRGB.Core.Configuration;

/// <summary>
/// Fast, robust JSON configuration storage located in %APPDATA%/XenRGB/settings.json.
/// </summary>
public sealed class JsonConfigurationService : IConfigurationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    public string ConfigFilePath { get; }

    public AppSettings Settings { get; private set; } = new();

    public UserProfile ActiveProfile
    {
        get
        {
            var profile = Settings.Profiles.FirstOrDefault(p => p.Id == Settings.ActiveProfileId);
            if (profile != null)
                return profile;

            if (Settings.Profiles.Count > 0)
            {
                Settings.ActiveProfileId = Settings.Profiles[0].Id;
                return Settings.Profiles[0];
            }

            var defaultProfile = CreateDefaultProfile();
            Settings.Profiles.Add(defaultProfile);
            Settings.ActiveProfileId = defaultProfile.Id;
            return defaultProfile;
        }
    }

    public JsonConfigurationService(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            ConfigFilePath = customPath;
        }
        else
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "XenRGB");
            ConfigFilePath = Path.Combine(folder, "settings.json");
        }
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = await File.ReadAllTextAsync(ConfigFilePath, ct).ConfigureAwait(false);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded != null)
                {
                    Settings = loaded;
                }
            }

            if (Settings.Profiles.Count == 0)
            {
                var defaultProfile = CreateDefaultProfile();
                Settings.Profiles.Add(defaultProfile);
                Settings.ActiveProfileId = defaultProfile.Id;
            }
            else if (string.IsNullOrWhiteSpace(Settings.ActiveProfileId) ||
                     Settings.Profiles.All(p => p.Id != Settings.ActiveProfileId))
            {
                Settings.ActiveProfileId = Settings.Profiles[0].Id;
            }
        }
        catch
        {
            // Fallback to fresh defaults on corrupt or unreadable file
            Settings = new AppSettings();
            var def = CreateDefaultProfile();
            Settings.Profiles.Add(def);
            Settings.ActiveProfileId = def.Id;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            string? dir = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(Settings, JsonOptions);
            await File.WriteAllTextAsync(ConfigFilePath, json, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public void SetActiveProfile(string profileId)
    {
        if (Settings.Profiles.Any(p => p.Id == profileId))
        {
            Settings.ActiveProfileId = profileId;
        }
    }

    private static UserProfile CreateDefaultProfile()
    {
        var profile = new UserProfile
        {
            Id = "default-profile",
            Name = "Default Profile",
            SelectedEffectId = "cotton-candy",
            MasterBrightness = 1.0f,
            Automation = new AutomationConfig
            {
                TurnOffOnMonitorOff = true,
                RestoreOnMonitorOn = true,
                InactivityTimeoutMinutes = 15,
                MonitorOffDelaySeconds = 10
            }
        };

        // Add default sample placements for mock/common layout:
        // Keyboard in center-bottom, Desk Strip along bottom, Monitor/Backlight at top
        profile.Placements.Add(new CanvasDevicePlacement("mock-keyboard", "RGB Keyboard", 0.5f, 0.65f, 0.45f, 0.22f));
        profile.Placements.Add(new CanvasDevicePlacement("mock-strip", "Desk LED Strip", 0.5f, 0.92f, 0.90f, 0.05f));
        profile.Placements.Add(new CanvasDevicePlacement("mock-motherboard", "Motherboard RGB", 0.80f, 0.35f, 0.20f, 0.25f));

        return profile;
    }
}
