using System;
using XenRGB.Core.Automation;
using XenRGB.Core.Configuration;
using Xunit;

namespace XenRGB.Tests;

public class TestMonitorProvider : IMonitorStateProvider
{
    public bool IsMonitorOn { get; private set; } = true;
    public event EventHandler<bool>? MonitorStateChanged;

    public void TriggerStateChange(bool on)
    {
        IsMonitorOn = on;
        MonitorStateChanged?.Invoke(this, on);
    }

    public void Dispose() { }
}

public class TestInactivityProvider : IUserInactivityProvider
{
    public TimeSpan CurrentInactivity { get; set; } = TimeSpan.Zero;
    public TimeSpan GetInactivityDuration() => CurrentInactivity;
}

public class AutomationTests
{
    [Fact]
    public void ManualControls_TurnOff_SetsStateToOff()
    {
        var monitor = new TestMonitorProvider();
        var inactivity = new TestInactivityProvider();
        using var manager = new AutomationManager(monitor, inactivity);

        Assert.True(manager.IsLightingActive);

        manager.TurnOff();
        Assert.False(manager.IsLightingActive);
        Assert.True(manager.IsManuallyTurnedOff);

        manager.TurnOn();
        Assert.True(manager.IsLightingActive);
        Assert.False(manager.IsManuallyTurnedOff);
    }

    [Fact]
    public void ManualControls_GoodNight_KeepsOffUntilTurnOn()
    {
        var monitor = new TestMonitorProvider();
        var inactivity = new TestInactivityProvider();
        using var manager = new AutomationManager(monitor, inactivity);

        manager.GoodNight();
        Assert.False(manager.IsLightingActive);
        Assert.True(manager.IsGoodNightActive);

        // Monitor turning on should NOT override Good Night mode
        monitor.TriggerStateChange(true);
        Assert.False(manager.IsLightingActive);
        Assert.True(manager.IsGoodNightActive);

        // TurnOn restores
        manager.TurnOn();
        Assert.True(manager.IsLightingActive);
        Assert.False(manager.IsGoodNightActive);
    }

    [Fact]
    public void MonitorState_InstantOff_TurnsLightingOff()
    {
        var monitor = new TestMonitorProvider();
        var inactivity = new TestInactivityProvider();
        var config = new AutomationConfig
        {
            TurnOffOnMonitorOff = true,
            RestoreOnMonitorOn = true,
            MonitorOffDelaySeconds = 0 // Instant for test
        };
        using var manager = new AutomationManager(monitor, inactivity, config);

        Assert.True(manager.IsLightingActive);

        monitor.TriggerStateChange(false);
        Assert.False(manager.IsLightingActive);
        Assert.True(manager.IsMonitorOff);

        monitor.TriggerStateChange(true);
        Assert.True(manager.IsLightingActive);
        Assert.False(manager.IsMonitorOff);
    }
}
