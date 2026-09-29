using System;
using System.Collections.Generic;
using XenRGB.Core.Canvas;

namespace XenRGB.Core.Configuration;

/// <summary>
/// A user profile storing canvas layout, active effect, effect parameters, brightness, and automation.
/// </summary>
public sealed class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Default Profile";
    public string SelectedEffectId { get; set; } = "cotton-candy";
    public float MasterBrightness { get; set; } = 1.0f;
    public Dictionary<string, object> EffectParameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<CanvasDevicePlacement> Placements { get; set; } = new();
    public List<DeviceConfig> Devices { get; set; } = new();
    public AutomationConfig Automation { get; set; } = new();
}
