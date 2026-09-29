using System;
using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.Custom;

public sealed class BiohazardCustomEffect : EffectBase
{
    private static readonly RgbColor DeepToxic = RgbColor.FromHex("#022B08");
    private static readonly RgbColor ToxicGreen = RgbColor.FromHex("#00FF44");
    private static readonly RgbColor AcidLime = RgbColor.FromHex("#A6FF00");

    public override string Id => "biohazard";
    public override string Name => "Biohazard";
    public override string Description => "Green-themed radioactive toxic pulses and organic bio-luminescent surges.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "biohazard",
        Name = "Biohazard",
        Version = "1.0.0",
        Author = "XenRGB",
        Description = "Green-themed radioactive toxic pulses and organic bio-luminescent surges."
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Float("speed", "Pulse Speed", 1.0f, 0.1f, 4.0f, 0.1f, "Pulse speed"),
        EffectParameter.Float("brightness", "Brightness", 1.0f, 0.0f, 1.0f, 0.05f, "Overall brightness"),
        EffectParameter.Float("intensity", "Glow Intensity", 0.8f, 0.2f, 2.0f, 0.05f, "Toxic glow sharpness")
    };

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        float speed = GetFloat("speed", 1.0f);
        float brightness = GetFloat("brightness", 1.0f);
        float intensity = GetFloat("intensity", 0.8f);

        float time = context.TotalTime * speed;
        float dx = x - 0.5f;
        float dy = y - 0.5f;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        float pulse = (MathF.Sin(dist * 12f - time * 3f) + 1f) * 0.5f;
        float noise = MathUtils.Perlin2DNormalized(x * 3f + time * 0.2f, y * 3f - time * 0.3f);
        float combined = Math.Clamp((pulse * 0.6f + noise * 0.4f) * intensity, 0f, 1f);

        RgbColor color = combined < 0.6f
            ? RgbColor.Lerp(DeepToxic, ToxicGreen, combined / 0.6f)
            : RgbColor.Lerp(ToxicGreen, AcidLime, (combined - 0.6f) / 0.4f);

        return brightness < 0.999f ? color.WithBrightness(brightness) : color;
    }
}
