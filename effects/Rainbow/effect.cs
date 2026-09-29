using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.Custom;

public sealed class RainbowCustomEffect : EffectBase
{
    public override string Id => "rainbow";
    public override string Name => "Rainbow";
    public override string Description => "Smooth continuous spectral rainbow animation sweeping across all devices.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "rainbow",
        Name = "Rainbow",
        Version = "1.0.0",
        Author = "XenRGB",
        Description = "Smooth continuous spectral rainbow animation sweeping across all devices."
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Float("speed", "Cycle Speed", 1.0f, 0.1f, 5.0f, 0.1f, "Cycle speed"),
        EffectParameter.Float("brightness", "Brightness", 1.0f, 0.0f, 1.0f, 0.05f, "Overall brightness")
    };

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        float speed = GetFloat("speed", 1.0f);
        float brightness = GetFloat("brightness", 1.0f);

        float spatialProgress = (x + y * 0.5f) * 0.7f;
        float timeOffset = context.TotalTime * speed * 0.25f;
        float hue = ((spatialProgress - timeOffset) % 1.0f + 1.0f) % 1.0f * 360f;

        var color = RgbColor.FromHsv(hue, 1.0f, 1.0f);
        return brightness < 0.999f ? color.WithBrightness(brightness) : color;
    }
}
