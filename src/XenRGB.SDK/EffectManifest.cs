using System.Collections.Generic;

namespace XenRGB.SDK;

/// <summary>
/// Metadata describing an effect, serializable to/from manifest.json.
/// </summary>
public sealed class EffectManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
    public string Author { get; set; } = "XenRGB";
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; } = "General";
    public Dictionary<string, object>? DefaultParameters { get; set; }
}
