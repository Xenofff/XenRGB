using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.Custom;

public sealed class PlainColorCustomEffect : EffectBase
{
    public override string Id => "plain-color";
    public override string Name => "Plain Color";
    public override string Description => "Applies a solid configurable color across all devices on the canvas.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "plain-color",
        Name = "Plain Color",
        Version = "1.0.0",
        Author = "XenRGB",
        Description = "Applies a solid configurable color across all devices on the canvas."
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Color("color", "Color", RgbColor.FromHex("#00A2FF"), "Target color"),
        EffectParameter.Float("brightness", "Brightness", 1.0f, 0.0f, 1.0f, 0.05f, "Overall brightness")
    };

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        var col = GetColor("color", RgbColor.FromHex("#00A2FF"));
        float bri = GetFloat("brightness", 1.0f);
        return bri < 0.999f ? col.WithBrightness(bri) : col;
    }
}
