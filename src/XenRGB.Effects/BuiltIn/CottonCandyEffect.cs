using System;
using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.BuiltIn;

/// <summary>
/// Organic spatial cotton candy effect with floating translucent cyan/magenta circles
/// blending over a multi-color atmospheric background.
/// </summary>
public sealed class CottonCandyEffect : EffectBase
{
    private struct Circle
    {
        public float X;
        public float BaseY;
        public float Radius;
        public float SpeedX;
        public float BobSpeed;
        public float BobPhase;
        public bool IsColor1;
    }

    private static readonly Circle[] Circles = new Circle[]
    {
        new() { X = 0.1f, BaseY = 0.25f, Radius = 0.18f, SpeedX = 0.08f, BobSpeed = 1.2f, BobPhase = 0.0f, IsColor1 = true },
        new() { X = 0.4f, BaseY = 0.70f, Radius = 0.22f, SpeedX = 0.11f, BobSpeed = 1.0f, BobPhase = 1.5f, IsColor1 = false },
        new() { X = 0.7f, BaseY = 0.35f, Radius = 0.16f, SpeedX = 0.07f, BobSpeed = 1.5f, BobPhase = 3.0f, IsColor1 = true },
        new() { X = 0.9f, BaseY = 0.80f, Radius = 0.20f, SpeedX = 0.10f, BobSpeed = 0.9f, BobPhase = 4.2f, IsColor1 = false },
        new() { X = 0.25f, BaseY = 0.50f, Radius = 0.19f, SpeedX = -0.09f, BobSpeed = 1.1f, BobPhase = 0.8f, IsColor1 = false },
        new() { X = 0.55f, BaseY = 0.20f, Radius = 0.17f, SpeedX = -0.12f, BobSpeed = 1.4f, BobPhase = 2.1f, IsColor1 = true },
        new() { X = 0.85f, BaseY = 0.65f, Radius = 0.21f, SpeedX = -0.08f, BobSpeed = 0.8f, BobPhase = 3.7f, IsColor1 = false },
        new() { X = 0.05f, BaseY = 0.85f, Radius = 0.16f, SpeedX = -0.10f, BobSpeed = 1.3f, BobPhase = 5.0f, IsColor1 = true },
    };

    public override string Id => "cotton-candy";
    public override string Name => "Cotton Candy";
    public override string Description => "A calm, smooth flowing atmospheric scene with drifting cyan and magenta clouds over a rich gradient.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "cotton-candy",
        Name = "Cotton Candy",
        Version = "2.0.0",
        Author = "XenRGB",
        Description = "A calm, smooth flowing atmospheric scene with drifting cyan and magenta clouds over a rich gradient.",
        Category = "Atmospheric",
        DefaultParameters = new Dictionary<string, object>
        {
            ["bg_color_1"] = "#0B0C2A",
            ["bg_color_2"] = "#3A0CA3",
            ["bg_color_3"] = "#003B73",
            ["color_1"] = "#00F0FF",
            ["color_2"] = "#FF007F",
            ["speed"] = 1.0f
        }
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Color("bg_color_1", "Background Color 1", RgbColor.FromHex("#0B0C2A"), "Primary background ambient color."),
        EffectParameter.Color("bg_color_2", "Background Color 2", RgbColor.FromHex("#3A0CA3"), "Secondary background gradient color."),
        EffectParameter.Color("bg_color_3", "Background Color 3", RgbColor.FromHex("#003B73"), "Tertiary background gradient color."),
        EffectParameter.Color("color_1", "Color 1", RgbColor.FromHex("#00F0FF"), "Primary cloud color (Cyan)."),
        EffectParameter.Color("color_2", "Color 2", RgbColor.FromHex("#FF007F"), "Secondary cloud color (Magenta)."),
        EffectParameter.Float("speed", "Speed", 1.0f, 0.1f, 3.0f, 0.05f, "Animation progression speed.")
    };

    public override RgbColor Evaluate(Point2D position, double time)
    {
        var bg1 = GetColor("bg_color_1", RgbColor.FromHex("#0B0C2A"));
        var bg2 = GetColor("bg_color_2", RgbColor.FromHex("#3A0CA3"));
        var bg3 = GetColor("bg_color_3", RgbColor.FromHex("#003B73"));
        var col1 = GetColor("color_1", RgbColor.FromHex("#00F0FF"));
        var col2 = GetColor("color_2", RgbColor.FromHex("#FF007F"));
        float speed = GetFloat("speed", 1.0f);

        // 1. Background gradient calculation across canvas
        float bgT = Math.Clamp(position.X * 0.7f + position.Y * 0.3f, 0f, 1f);
        RgbColor currentBg = bgT < 0.5f
            ? RgbColor.Lerp(bg1, bg2, bgT * 2f)
            : RgbColor.Lerp(bg2, bg3, (bgT - 0.5f) * 2f);

        // 2. Translucent circles sampling
        float sumInfluence1 = 0f;
        float sumInfluence2 = 0f;

        float animTime = (float)time * speed;

        for (int i = 0; i < Circles.Length; i++)
        {
            ref readonly var c = ref Circles[i];

            // Deterministic position at given time
            float cx;
            if (c.SpeedX > 0)
            {
                cx = ((c.X + animTime * c.SpeedX) % 1.5f) - 0.25f;
            }
            else
            {
                cx = 1.25f - ((1.25f - c.X - animTime * c.SpeedX) % 1.5f);
            }

            float cy = c.BaseY + MathF.Sin(animTime * c.BobSpeed + c.BobPhase) * 0.08f;

            float dx = position.X - cx;
            float dy = position.Y - cy;
            float distSq = dx * dx + dy * dy;
            float rSq = c.Radius * c.Radius;

            if (distSq < rSq)
            {
                float dist = MathF.Sqrt(distSq);
                float t = dist / c.Radius;
                // Organic smoothstep falloff: 1.0 at center, 0.0 at edge
                float influence = (1f - t * t) * 0.85f;

                if (c.IsColor1)
                    sumInfluence1 += influence;
                else
                    sumInfluence2 += influence;
            }
        }

        // 3. Blend circles over background
        float totalCircleInfluence = Math.Clamp(sumInfluence1 + sumInfluence2, 0f, 1f);
        if (totalCircleInfluence > 0.001f)
        {
            float w1 = sumInfluence1 / (sumInfluence1 + sumInfluence2);
            RgbColor circleColor = RgbColor.Lerp(col2, col1, w1);
            return RgbColor.Lerp(currentBg, circleColor, totalCircleInfluence);
        }

        return currentBg;
    }

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        return Evaluate(new Point2D(x, y), context.TotalTime);
    }
}
