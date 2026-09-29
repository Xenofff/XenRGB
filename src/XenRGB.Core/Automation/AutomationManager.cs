using System;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.Core.Configuration;

namespace XenRGB.Core.Automation;

/// <summary>
/// Orchestrates monitor power state, user inactivity, Good Night mode, and manual overrides.
/// </summary>
public sealed class AutomationManager : IDisposable
{
    private readonly IMonitorStateProvider _monitorProvider;
    private readonly IUserInactivityProvider _inactivityProvider;
    private readonly Timer _pollTimer;
    private readonly object _stateLock = new();

    private CancellationTokenSource? _monitorOffDelayCts;

    public AutomationConfig Config { get; set; }

    public bool IsLightingActive { get; private set; } = true;
    public bool IsGoodNightActive { get; private set; } = false;
    public bool IsManuallyTurnedOff { get; private set; } = false;
    public bool IsMonitorOff { get; private set; } = false;
    public bool IsInactive { get; private set; } = false;

    public event EventHandler<bool>? LightingStateChanged;
    public event EventHandler? AutomationStatusChanged;

    public string StatusDescription
    {
        get
        {
            if (IsGoodNightActive) return "Good Night Mode (Lighting Off)";
            if (IsManuallyTurnedOff) return "Lighting Off (Manual)";
            if (IsMonitorOff) return "Lighting Off (Monitor Standby)";
            if (IsInactive) return "Lighting Off (Inactivity Timeout)";
            return IsLightingActive ? "Lighting Active" : "Lighting Off";
        }
    }

    public AutomationManager(
        IMonitorStateProvider monitorProvider,
        IUserInactivityProvider inactivityProvider,
        AutomationConfig? config = null)
    {
        _monitorProvider = monitorProvider ?? throw new ArgumentNullException(nameof(monitorProvider));
        _inactivityProvider = inactivityProvider ?? throw new ArgumentNullException(nameof(inactivityProvider));
        Config = config ?? new AutomationConfig();

        _monitorProvider.MonitorStateChanged += OnMonitorStateChanged;

        // Poll inactivity every 3 seconds
        _pollTimer = new Timer(OnPollCheck, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }

    public void TurnOn()
    {
        lock (_stateLock)
        {
            IsGoodNightActive = false;
            IsManuallyTurnedOff = false;
            IsInactive = false;
            CancelMonitorOffDelay();
            SetLightingState(true);
        }
    }

    public void TurnOff()
    {
        lock (_stateLock)
        {
            IsGoodNightActive = false;
            IsManuallyTurnedOff = true;
            CancelMonitorOffDelay();
            SetLightingState(false);
        }
    }

    public void GoodNight()
    {
        lock (_stateLock)
        {
            IsGoodNightActive = true;
            IsManuallyTurnedOff = false;
            CancelMonitorOffDelay();
            SetLightingState(false);
        }
    }

    private void OnMonitorStateChanged(object? sender, bool isMonitorOn)
    {
        lock (_stateLock)
        {
            IsMonitorOff = !isMonitorOn;

            if (!isMonitorOn)
            {
                // Monitor turned off
                if (!Config.TurnOffOnMonitorOff || !IsLightingActive)
                    return;

                CancelMonitorOffDelay();

                if (Config.MonitorOffDelaySeconds <= 0)
                {
                    SetLightingState(false);
                }
                else
                {
                    _monitorOffDelayCts = new CancellationTokenSource();
                    var token = _monitorOffDelayCts.Token;
                    int delayMs = Config.MonitorOffDelaySeconds * 1000;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(delayMs, token).ConfigureAwait(false);
                            lock (_stateLock)
                            {
                                if (!token.IsCancellationRequested && IsMonitorOff)
                                {
                                    SetLightingState(false);
                                }
                            }
                        }
                        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
                        {
                            // Delay was cancelled or token disposed
                        }
                    }, token);
                }
            }
            else
            {
                // Monitor turned on
                CancelMonitorOffDelay();

                if (Config.RestoreOnMonitorOn && !IsGoodNightActive && !IsManuallyTurnedOff)
                {
                    SetLightingState(true);
                }
            }

            AutomationStatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnPollCheck(object? state)
    {
        lock (_stateLock)
        {
            if (IsGoodNightActive || IsManuallyTurnedOff)
                return;

            var idleDuration = _inactivityProvider.GetInactivityDuration();

            if (Config.InactivityTimeoutMinutes > 0)
            {
                var timeout = TimeSpan.FromMinutes(Config.InactivityTimeoutMinutes);
                if (idleDuration >= timeout)
                {
                    if (!IsInactive && IsLightingActive)
                    {
                        IsInactive = true;
                        SetLightingState(false);
                    }
                    return;
                }
            }

            // User has become active again (< 2.5 seconds of idle)
            if (IsInactive && idleDuration < TimeSpan.FromSeconds(2.5))
            {
                IsInactive = false;
                if (!IsMonitorOff && !IsGoodNightActive && !IsManuallyTurnedOff)
                {
                    SetLightingState(true);
                }
            }
        }
    }

    private void CancelMonitorOffDelay()
    {
        _monitorOffDelayCts?.Cancel();
        _monitorOffDelayCts?.Dispose();
        _monitorOffDelayCts = null;
    }

    private void SetLightingState(bool active)
    {
        if (IsLightingActive != active)
        {
            IsLightingActive = active;
            LightingStateChanged?.Invoke(this, active);
            AutomationStatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        _pollTimer.Dispose();
        CancelMonitorOffDelay();
        _monitorProvider.MonitorStateChanged -= OnMonitorStateChanged;
    }
}
