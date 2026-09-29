using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.BuiltIn;

/// <summary>
/// A solid uniform color applied across all devices regardless of spatial location.
/// </summary>
public sealed class PlainColorEffect : EffectBase
{
    public override string Id => "plain-color";
    public override string Name => "Plain Color";
    public override string Description => "Applies a configurable solid color and brightness across all devices.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "plain-color",
        Name = "Plain Color",
        Version = "2.0.0",
        Author = "XenRGB",
        Description = "Applies a configurable solid color and brightness across all devices.",
        Category = "Basic",
        DefaultParameters = new Dictionary<string, object>
        {
            ["color"] = "#00A2FF",
            ["brightness"] = 1.0f
        }
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Color("color", "Color", RgbColor.FromHex("#00A2FF"), "The solid color applied to all LEDs."),
        EffectParameter.Float("brightness", "Brightness", 1.0f, 0.0f, 1.0f, 0.05f, "Overall brightness of the color.")
    };

    public override RgbColor Evaluate(Point2D position, double time)
    {
        var color = GetColor("color", RgbColor.FromHex("#00A2FF"));
        float brightness = GetFloat("brightness", 1.0f);
        return brightness < 0.999f ? color.WithBrightness(brightness) : color;
    }

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        return Evaluate(new Point2D(x, y), context.TotalTime);
    }
}
