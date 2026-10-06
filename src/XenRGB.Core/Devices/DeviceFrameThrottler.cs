using System;
using XenRGB.SDK;

namespace XenRGB.Core.Devices;

/// <summary>
/// Device-level dirty-checking and throttling boundary.
/// Compares rendered LED buffers against previously transmitted frames without heap allocations.
/// Supports optional periodic heartbeat transmission (e.g., for WLED realtime UDP timeout refresh).
/// </summary>
public sealed class DeviceFrameThrottler
{
    private readonly TimeSpan? _heartbeatInterval;
    private readonly TimeProvider _timeProvider;
    private readonly int _colorDeltaThreshold;

    private RgbColor[]? _lastFrame;
    private bool _hasTransmitted;
    private long _lastTransmittedTimestamp;

    public TimeSpan? HeartbeatInterval => _heartbeatInterval;
    public bool HasTransmitted => _hasTransmitted;

    public long GeneratedFrames { get; private set; }
    public long SkippedFrames { get; private set; }
    public long SentFrames { get; private set; }

    public DeviceFrameThrottler(TimeSpan? heartbeatInterval = null, TimeProvider? timeProvider = null, int colorDeltaThreshold = 1)
    {
        _heartbeatInterval = heartbeatInterval;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _colorDeltaThreshold = Math.Max(1, colorDeltaThreshold);
    }

    /// <summary>
    /// Evaluates whether the current frame should be transmitted to the hardware.
    /// Returns true if this is the first frame, or if any color has changed,
    /// or if the heartbeat interval has elapsed.
    /// Allocates zero heap memory.
    /// </summary>
    public bool ShouldTransmit(ReadOnlySpan<RgbColor> currentFrame)
    {
        GeneratedFrames++;
        if (!_hasTransmitted || _lastFrame == null || _lastFrame.Length != currentFrame.Length)
        {
            return true;
        }

        bool hasChange = false;
        if (_colorDeltaThreshold <= 1)
        {
            hasChange = !currentFrame.SequenceEqual(_lastFrame);
        }
        else
        {
            for (int i = 0; i < currentFrame.Length; i++)
            {
                var c1 = currentFrame[i];
                var c2 = _lastFrame[i];
                if (Math.Abs(c1.R - c2.R) >= _colorDeltaThreshold ||
                    Math.Abs(c1.G - c2.G) >= _colorDeltaThreshold ||
                    Math.Abs(c1.B - c2.B) >= _colorDeltaThreshold)
                {
                    hasChange = true;
                    break;
                }
            }
        }

        if (hasChange)
        {
            return true;
        }

        // If identical, check if heartbeat interval has elapsed
        if (_heartbeatInterval.HasValue && _heartbeatInterval.Value > TimeSpan.Zero)
        {
            long elapsedTicks = _timeProvider.GetTimestamp() - _lastTransmittedTimestamp;
            double elapsedMs = (double)elapsedTicks / _timeProvider.TimestampFrequency * 1000.0;
            if (elapsedMs >= _heartbeatInterval.Value.TotalMilliseconds)
            {
                return true;
            }
        }

        SkippedFrames++;
        return false;
    }

    /// <summary>
    /// Records that the frame was successfully transmitted to the hardware.
    /// Caches the transmitted colors and updates the timestamp without per-frame allocations.
    /// </summary>
    public void MarkTransmitted(ReadOnlySpan<RgbColor> transmittedFrame)
    {
        SentFrames++;
        if (_lastFrame == null || _lastFrame.Length != transmittedFrame.Length)
        {
            _lastFrame = new RgbColor[transmittedFrame.Length];
        }

        transmittedFrame.CopyTo(_lastFrame);
        _hasTransmitted = true;
        _lastTransmittedTimestamp = _timeProvider.GetTimestamp();
    }

    /// <summary>
    /// Resets the dirty state (e.g. on power toggle, device reconnect, or frame reset).
    /// The next frame will be transmitted regardless of previous color values.
    /// </summary>
    public void Reset()
    {
        _hasTransmitted = false;
        _lastTransmittedTimestamp = 0;
        if (_lastFrame != null)
        {
            Array.Clear(_lastFrame, 0, _lastFrame.Length);
        }
    }
}
