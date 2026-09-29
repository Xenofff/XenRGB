using System;

namespace XenRGB.Core.Automation;

/// <summary>
/// Source of user input inactivity duration.
/// </summary>
public interface IUserInactivityProvider
{
    TimeSpan GetInactivityDuration();
}
