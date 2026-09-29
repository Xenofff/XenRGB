using System;
using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.BuiltIn;

/// <summary>
/// Smooth flowing blue/purple/pink gradient with slow, relaxing transitions.
/// </summary>
public sealed class CottonCandyEffect : EffectBase
{
    private static readonly RgbColor SoftBlue = RgbColor.FromHex("#0096FF");
    private static readonly RgbColor DeepPurple = RgbColor.FromHex("#7B2CBF");
    private static readonly RgbColor PastelPink = RgbColor.FromHex("#E056FD");

    public override string Id => "cotton-candy";
    public override string Name => "Cotton Candy";
    public override string Description => "A calm, smooth flowing blue and purple animation with gentle organic transitions.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "cotton-candy",
        Name = "Cotton Candy",
        Version = "1.0.0",
        Author = "XenRGB",
        Description = "A calm, smooth flowing blue and purple animation with gentle organic transitions.",
        Category = "Atmospheric",
        DefaultParameters = new Dictionary<string, object>
        {
            ["speed"] = 0.5f,
            ["brightness"] = 1.0f,
            ["scale"] = 1.2f
        }
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Float("speed", "Flow Speed", 0.5f, 0.05f, 3.0f, 0.05f, "Animation progression speed."),
        EffectParameter.Float("brightness", "Brightness", 1.0f, 0.0f, 1.0f, 0.05f, "Overall lighting brightness."),
        EffectParameter.Float("scale", "Wave Scale", 1.2f, 0.2f, 4.0f, 0.1f, "Spatial scale of the color clouds.")
    };

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        float speed = GetFloat("speed", 0.5f);
        float brightness = GetFloat("brightness", 1.0f);
        float scale = GetFloat("scale", 1.2f);

        float time = context.TotalTime * speed * 0.4f;

        // Smooth sinusoidal 2D flow
        float wave1 = MathF.Sin((x * scale + time) * MathF.PI);
        float wave2 = MathF.Cos((y * scale * 0.8f - time * 0.6f) * MathF.PI);
        float wave3 = MathF.Sin(((x + y) * scale * 0.5f + time * 0.4f) * MathF.PI);

        float blend = (wave1 + wave2 + wave3) / 3f * 0.5f + 0.5f;
        blend = MathUtils.SmoothStep(0.05f, 0.95f, blend);

        RgbColor color;
        if (blend < 0.5f)
        {
            float t = blend * 2f;
            color = RgbColor.Lerp(SoftBlue, DeepPurple, t);
        }
        else
        {
            float t = (blend - 0.5f) * 2f;
            color = RgbColor.Lerp(DeepPurple, PastelPink, t);
        }

        return brightness < 0.999f ? color.WithBrightness(brightness) : color;
    }
}
