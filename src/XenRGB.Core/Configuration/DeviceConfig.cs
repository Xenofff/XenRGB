namespace XenRGB.Core.Configuration;

public sealed class DeviceConfig
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public float Brightness { get; set; } = 1.0f;
    public string? CustomIpAddress { get; set; }
}
