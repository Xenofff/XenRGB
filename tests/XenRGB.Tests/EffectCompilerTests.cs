using System.Collections.Generic;
using XenRGB.Effects.Compiler;
using XenRGB.SDK;
using Xunit;

namespace XenRGB.Tests;

public class EffectCompilerTests
{
    [Fact]
    public void CompileEffectSource_ValidSource_CompilesAndInstantiates()
    {
        string source = @"
using System.Collections.Generic;
using XenRGB.SDK;

public class TestCustomEffect : EffectBase
{
    public override string Id => ""test-custom"";
    public override string Name => ""Test Custom"";
    public override string Description => ""Dynamically compiled test effect."";
    public override EffectManifest Manifest => new EffectManifest { Id = ""test-custom"", Name = ""Test Custom"" };
    public override IReadOnlyList<EffectParameter> Parameters => new List<EffectParameter>();

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        return new RgbColor(255, 128, 64);
    }
}
";

        var (effect, error) = EffectCompiler.CompileEffectSource(source);

        Assert.Null(error);
        Assert.NotNull(effect);
        Assert.Equal("test-custom", effect.Id);
        Assert.Equal("Test Custom", effect.Name);

        var color = effect.RenderPixel(0.5f, 0.5f, new EffectContext(0.033f, 1.0f));
        Assert.Equal(255, color.R);
        Assert.Equal(128, color.G);
        Assert.Equal(64, color.B);
    }

    [Fact]
    public void CompileEffectSource_SyntaxError_ReturnsDetailedErrorMessage()
    {
        string invalidSource = @"
public class IncompleteEffect : {
";

        var (effect, error) = EffectCompiler.CompileEffectSource(invalidSource);

        Assert.Null(effect);
        Assert.NotNull(error);
        Assert.Contains("Line", error);
    }

    [Fact]
    public void CompileEffectSource_ClassWithoutIEffect_ReturnsError()
    {
        string source = @"
public class JustAHelperClass
{
    public int Calculate() => 42;
}
";

        var (effect, error) = EffectCompiler.CompileEffectSource(source);

        Assert.Null(effect);
        Assert.NotNull(error);
        Assert.Contains("No class implementing IEffect", error);
    }
}
