using System.Collections.Generic;
using XenRGB.Effects.BuiltIn;
using XenRGB.SDK;
using Xunit;

namespace XenRGB.Tests;

public class EffectRenderingTests
{
    [Fact]
    public void PlainColor_CustomColorAndBrightness_RendersExpected()
    {
        var effect = new PlainColorEffect();
        effect.Initialize(new Dictionary<string, object>
        {
            ["color"] = "#FF8800",
            ["brightness"] = 0.5f
        });

        var context = new EffectContext(0.033f, 1.0f);
        var color = effect.RenderPixel(0.5f, 0.5f, context);

        Assert.Equal(128, color.R);
        Assert.Equal(68, color.G);
        Assert.Equal(0, color.B);
    }

    [Fact]
    public void CottonCandy_RendersNonZero_AndChangesOverTime()
    {
        var effect = new CottonCandyEffect();
        effect.Initialize();

        var ctx1 = new EffectContext(0.033f, 0.0f);
        var ctx2 = new EffectContext(0.033f, 5.0f);

        var col1 = effect.RenderPixel(0.2f, 0.2f, ctx1);
        var col2 = effect.RenderPixel(0.2f, 0.2f, ctx2);

        // Should produce visible lighting (not black)
        Assert.True(col1.R > 0 || col1.G > 0 || col1.B > 0);
        Assert.True(col2.R > 0 || col2.G > 0 || col2.B > 0);

        // Changes over time
        Assert.NotEqual(col1, col2);
    }

    [Fact]
    public void Biohazard_RendersGreenSpectrum()
    {
        var effect = new BiohazardEffect();
        effect.Initialize();

        var ctx = new EffectContext(0.033f, 1.0f);
        var col = effect.RenderPixel(0.5f, 0.5f, ctx);

        // Green channel should dominate in biohazard
        Assert.True(col.G >= col.R);
        Assert.True(col.G >= col.B);
    }

    [Fact]
    public void Rainbow_RendersFullHueRangeAcrossCanvas()
    {
        var effect = new RainbowEffect();
        effect.Initialize();

        var ctx = new EffectContext(0.033f, 0.0f);
        var colLeft = effect.RenderPixel(0.0f, 0.0f, ctx);
        var colRight = effect.RenderPixel(0.9f, 0.9f, ctx);

        Assert.NotEqual(colLeft, colRight);
    }
}
