namespace XenRGB.Core.Configuration;

public sealed class AutomationConfig
{
    /// <summary>
    /// Enable automatic lighting shutoff when monitors turn off.
    /// </summary>
    public bool TurnOffOnMonitorOff { get; set; } = true;

    /// <summary>
    /// Enable automatic lighting restore when monitors turn back on.
    /// </summary>
    public bool RestoreOnMonitorOn { get; set; } = true;

    /// <summary>
    /// Inactivity timeout in minutes before turning off RGB (0 = disabled).
    /// </summary>
    public int InactivityTimeoutMinutes { get; set; } = 15;

    /// <summary>
    /// Delay in seconds before turning off RGB after monitors turn off.
    /// </summary>
    public int MonitorOffDelaySeconds { get; set; } = 10;
}
