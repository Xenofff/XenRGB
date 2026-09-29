namespace XenRGB.SDK;

/// <summary>
/// Runtime context passed to effects during update and render cycles.
/// </summary>
public sealed class EffectContext
{
    public float DeltaTime { get; }
    public float TotalTime { get; }
    public long FrameIndex { get; }

    public EffectContext(float deltaTime, float totalTime, long frameIndex = 0)
    {
        DeltaTime = deltaTime;
        TotalTime = totalTime;
        FrameIndex = frameIndex;
    }
}
