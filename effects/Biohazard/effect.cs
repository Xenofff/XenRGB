using System;
using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.Custom;

public sealed class BiohazardCustomEffect : EffectBase
{
    public override string Id => "biohazard";
    public override string Name => "Biohazard";
    public override string Description => "Radioactive matrix of independently pulsing particles and sweeping toxic energy waves.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "biohazard",
        Name = "Biohazard",
        Version = "2.0.0",
        Author = "XenRGB",
        Description = "Radioactive matrix of independently pulsing particles and sweeping toxic energy waves.",
        Category = "Animated"
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Color("bg_color", "Background Color", RgbColor.FromHex("#031F08"), "Ambient dark toxic background color."),
        EffectParameter.Color("color_1", "Color 1", RgbColor.FromHex("#00FF44"), "Active radioactive green glow color."),
        EffectParameter.Float("speed", "Speed", 1.0f, 0.1f, 4.0f, 0.05f, "Animation and pulse progression speed.")
    };

    public override RgbColor Evaluate(Point2D position, double time)
    {
        var bgColor = GetColor("bg_color", RgbColor.FromHex("#031F08"));
        var color1 = GetColor("color_1", RgbColor.FromHex("#00FF44"));
        float speed = GetFloat("speed", 1.0f);

        float animTime = (float)time * speed;

        int ix = (int)(position.X * 1000f);
        int iy = (int)(position.Y * 1000f);
        int hash = (ix * 73856093) ^ (iy * 19349663);
        float phase = ((hash & 0xFFFF) / 65535f) * MathF.PI * 2f;
        float freq = 0.85f + (((hash >> 16) & 0x7FFF) / 32767f) * 0.7f;

        float flicker = 0.5f + 0.5f * MathF.Sin(animTime * 2.5f * freq + phase);
        float pointBrightness = 0.50f + 0.50f * (flicker * flicker);

        float wave1X = (animTime * 0.35f) % 2.4f - 0.7f;
        float dist1 = MathF.Abs(position.X - wave1X);
        float wave1 = MathF.Exp(-dist1 * dist1 * 50f);

        float wave2Y = (animTime * 0.25f + 0.6f) % 2.2f - 0.6f;
        float dist2 = MathF.Abs(position.Y - wave2Y);
        float wave2 = MathF.Exp(-dist2 * dist2 * 40f) * 0.7f;

        float waveTotal = Math.Clamp(wave1 + wave2, 0f, 1f);

        float intensity = Math.Clamp(pointBrightness * 0.60f + waveTotal * 0.70f, 0f, 1f);
        return RgbColor.Lerp(bgColor, color1, intensity);
    }

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        return Evaluate(new Point2D(x, y), context.TotalTime);
    }
}
