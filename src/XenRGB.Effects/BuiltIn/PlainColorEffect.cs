using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.BuiltIn;

/// <summary>
/// A static or solid color effect covering the whole canvas.
/// </summary>
public sealed class PlainColorEffect : EffectBase
{
    public override string Id => "plain-color";
    public override string Name => "Plain Color";
    public override string Description => "Applies a configurable solid color and brightness across the entire canvas.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "plain-color",
        Name = "Plain Color",
        Version = "1.0.0",
        Author = "XenRGB",
        Description = "Applies a configurable solid color and brightness across the entire canvas.",
        Category = "Basic",
        DefaultParameters = new Dictionary<string, object>
        {
            ["color"] = "#00A2FF",
            ["brightness"] = 1.0f
        }
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Color("color", "Color", RgbColor.FromHex("#00A2FF"), "The solid color applied to the canvas."),
        EffectParameter.Float("brightness", "Brightness", 1.0f, 0.0f, 1.0f, 0.05f, "Overall brightness of the color.")
    };

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        var color = GetColor("color", RgbColor.FromHex("#00A2FF"));
        float brightness = GetFloat("brightness", 1.0f);
        return brightness < 0.999f ? color.WithBrightness(brightness) : color;
    }
}
