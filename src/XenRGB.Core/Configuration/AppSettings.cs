using System.Collections.Generic;

namespace XenRGB.Core.Configuration;

/// <summary>
/// Root persistent application settings for XenRGB.
/// </summary>
public sealed class AppSettings
{
    public string OpenRgbHost { get; set; } = "127.0.0.1";
    public int OpenRgbPort { get; set; } = 6742;
    public bool AutoConnectOpenRgb { get; set; } = true;
    public int TargetFps { get; set; } = 15;
    public bool AutoStartWithWindows { get; set; } = false;
    public bool StartMinimizedToTray { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public List<string> WledDeviceIps { get; set; } = new();
    public string ActiveProfileId { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public List<UserProfile> Profiles { get; set; } = new();
}
