using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.Custom;

public sealed class RainbowCustomEffect : EffectBase
{
    public override string Id => "rainbow";
    public override string Name => "Rainbow";
    public override string Description => "A smooth, continuous spectral rainbow animation flowing across all devices.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "rainbow",
        Name = "Rainbow",
        Version = "2.0.0",
        Author = "XenRGB",
        Description = "A smooth, continuous spectral rainbow animation flowing across all devices.",
        Category = "Animated"
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Float("speed", "Speed", 1.0f, 0.1f, 5.0f, 0.1f, "Speed of the spectral color cycle.")
    };

    public override RgbColor Evaluate(Point2D position, double time)
    {
        float speed = GetFloat("speed", 1.0f);

        float spatialProgress = position.X * 0.8f + position.Y * 0.5f;
        float timeOffset = (float)time * speed * 0.25f;

        float hue = ((spatialProgress - timeOffset) % 1.0f + 1.0f) % 1.0f * 360f;
        return RgbColor.FromHsv(hue, 1.0f, 1.0f);
    }

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        return Evaluate(new Point2D(x, y), context.TotalTime);
    }
}
