using System;
using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.BuiltIn;

/// <summary>
/// Toxic radioactive green animated pulses and organic biohazard waves.
/// </summary>
public sealed class BiohazardEffect : EffectBase
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
        Description = "Green-themed radioactive toxic pulses and organic bio-luminescent surges.",
        Category = "Animated",
        DefaultParameters = new Dictionary<string, object>
        {
            ["speed"] = 1.0f,
            ["brightness"] = 1.0f,
            ["intensity"] = 0.8f
        }
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Float("speed", "Pulse Speed", 1.0f, 0.1f, 4.0f, 0.1f, "Speed of the radioactive pulses."),
        EffectParameter.Float("brightness", "Brightness", 1.0f, 0.0f, 1.0f, 0.05f, "Overall lighting brightness."),
        EffectParameter.Float("intensity", "Glow Intensity", 0.8f, 0.2f, 2.0f, 0.05f, "Sharpness and concentration of toxic glow.")
    };

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        float speed = GetFloat("speed", 1.0f);
        float brightness = GetFloat("brightness", 1.0f);
        float intensity = GetFloat("intensity", 0.8f);

        float time = context.TotalTime * speed;

        // Radial pulse from center + organic noise
        float dx = x - 0.5f;
        float dy = y - 0.5f;
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        // Ring pulses traveling outwards
        float pulse = MathF.Sin(dist * 12f - time * 3f);
        pulse = (pulse + 1f) * 0.5f; // 0..1

        // Add 2D procedural noise for organic fluid turbulence
        float noise = MathUtils.Perlin2DNormalized(x * 3f + time * 0.2f, y * 3f - time * 0.3f);
        float combined = (pulse * 0.6f + noise * 0.4f) * intensity;
        combined = Math.Clamp(combined, 0f, 1f);

        RgbColor color;
        if (combined < 0.6f)
        {
            float t = combined / 0.6f;
            color = RgbColor.Lerp(DeepToxic, ToxicGreen, t);
        }
        else
        {
            float t = (combined - 0.6f) / 0.4f;
            color = RgbColor.Lerp(ToxicGreen, AcidLime, t);
        }

        return brightness < 0.999f ? color.WithBrightness(brightness) : color;
    }
}
