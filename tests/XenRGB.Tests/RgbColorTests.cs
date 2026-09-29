using System;
using XenRGB.SDK;
using Xunit;

namespace XenRGB.Tests;

public class RgbColorTests
{
    [Fact]
    public void Lerp_Halfway_InterpolatesCorrectly()
    {
        var black = new RgbColor(0, 0, 0);
        var white = new RgbColor(200, 100, 50);

        var mid = RgbColor.Lerp(black, white, 0.5f);

        Assert.Equal(100, mid.R);
        Assert.Equal(50, mid.G);
        Assert.Equal(25, mid.B);
    }

    [Fact]
    public void FromHsv_PrimaryColors_ConvertsAccurately()
    {
        var red = RgbColor.FromHsv(0f, 1f, 1f);
        Assert.Equal(255, red.R);
        Assert.Equal(0, red.G);
        Assert.Equal(0, red.B);

        var green = RgbColor.FromHsv(120f, 1f, 1f);
        Assert.Equal(0, green.R);
        Assert.Equal(255, green.G);
        Assert.Equal(0, green.B);

        var blue = RgbColor.FromHsv(240f, 1f, 1f);
        Assert.Equal(0, blue.R);
        Assert.Equal(0, blue.G);
        Assert.Equal(255, blue.B);
    }

    [Fact]
    public void ToHsv_RoundtripsSuccessfully()
    {
        var original = RgbColor.FromHsv(180f, 0.8f, 0.9f);
        original.ToHsv(out float h, out float s, out float v);

        Assert.InRange(h, 179f, 181f);
        Assert.InRange(s, 0.79f, 0.81f);
        Assert.InRange(v, 0.89f, 0.91f);
    }

    [Theory]
    [InlineData("#FF0000", 255, 0, 0)]
    [InlineData("#00FF00", 0, 255, 0)]
    [InlineData("#0000FF", 0, 0, 255)]
    [InlineData("#FFAA33", 255, 170, 51)]
    public void FromHex_ValidHex_ParsesCorrectly(string hex, byte r, byte g, byte b)
    {
        var color = RgbColor.FromHex(hex);
        Assert.Equal(r, color.R);
        Assert.Equal(g, color.G);
        Assert.Equal(b, color.B);
    }

    [Fact]
    public void WithBrightness_Half_HalvesComponents()
    {
        var color = new RgbColor(200, 100, 50);
        var scaled = color.WithBrightness(0.5f);

        Assert.Equal(100, scaled.R);
        Assert.Equal(50, scaled.G);
        Assert.Equal(25, scaled.B);
    }
}
