using System;
using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.Custom;

public sealed class CottonCandyCustomEffect : EffectBase
{
    private static readonly RgbColor SoftBlue = RgbColor.FromHex("#0096FF");
    private static readonly RgbColor DeepPurple = RgbColor.FromHex("#7B2CBF");
    private static readonly RgbColor PastelPink = RgbColor.FromHex("#E056FD");

    public override string Id => "cotton-candy";
    public override string Name => "Cotton Candy";
    public override string Description => "Smooth flowing blue and purple gradient animation with gentle organic movement.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "cotton-candy",
        Name = "Cotton Candy",
        Version = "1.0.0",
        Author = "XenRGB",
        Description = "Smooth flowing blue and purple gradient animation with gentle organic movement."
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Float("speed", "Flow Speed", 0.5f, 0.05f, 3.0f, 0.05f, "Animation speed"),
        EffectParameter.Float("brightness", "Brightness", 1.0f, 0.0f, 1.0f, 0.05f, "Overall brightness"),
        EffectParameter.Float("scale", "Scale", 1.2f, 0.2f, 4.0f, 0.1f, "Wave spatial scale")
    };

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        float speed = GetFloat("speed", 0.5f);
        float brightness = GetFloat("brightness", 1.0f);
        float scale = GetFloat("scale", 1.2f);

        float time = context.TotalTime * speed * 0.4f;

        float wave1 = MathF.Sin((x * scale + time) * MathF.PI);
        float wave2 = MathF.Cos((y * scale * 0.8f - time * 0.6f) * MathF.PI);
        float blend = (wave1 + wave2) * 0.25f + 0.5f;
        blend = Math.Clamp(blend, 0f, 1f);

        RgbColor color = blend < 0.5f
            ? RgbColor.Lerp(SoftBlue, DeepPurple, blend * 2f)
            : RgbColor.Lerp(DeepPurple, PastelPink, (blend - 0.5f) * 2f);

        return brightness < 0.999f ? color.WithBrightness(brightness) : color;
    }
}
