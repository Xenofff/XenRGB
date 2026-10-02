using System.Text.Json;
using XenRGB.Effects.VisualEditor;
using XenRGB.SDK;
using Xunit;

namespace XenRGB.Tests;

public class VisualEffectTests
{
    [Fact]
    public void CanSerializeAndDeserializeVisualEffect()
    {
        var model = new VisualEffectModel();
        model.Layers.Add(new CircleLayer { Name = "C1", Radius = 0.5, Color = "#FF0000" });
        model.Layers.Add(new RectangleLayer { Name = "R1", Width = 0.5, Color = "#00FF00" });
        
        string json = JsonSerializer.Serialize(model);
        
        var restored = JsonSerializer.Deserialize<VisualEffectModel>(json);
        
        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Layers.Count);
        Assert.IsType<CircleLayer>(restored.Layers[0]);
        Assert.IsType<RectangleLayer>(restored.Layers[1]);
    }
    
    [Fact]
    public void Adapter_EvaluatesCircleCorrectly()
    {
        var model = new VisualEffectModel();
        model.Layers.Add(new CircleLayer { X = 0.5, Y = 0.5, Radius = 0.2, Color = "#FF0000" });
        
        var adapter = new VisualEffectAdapter("test", "test", model);
        
        // Inside circle
        var c1 = adapter.Evaluate(new Point2D(0.5f, 0.5f), 0);
        Assert.Equal(255, c1.R);
        Assert.Equal(0, c1.G);
        Assert.Equal(0, c1.B);
        
        // Outside circle
        var c2 = adapter.Evaluate(new Point2D(0.1f, 0.1f), 0);
        Assert.Equal(0, c2.R);
    }
    
    [Fact]
    public void Adapter_EvaluatesAnimationCorrectly()
    {
        var model = new VisualEffectModel();
        model.Layers.Add(new CircleLayer { 
            X = 0.5, Y = 0.5, Radius = 0.2, Color = "#FF0000",
            Animation = new AnimationConfig { Type = "move", Direction = 0, Speed = 50 } 
        });
        
        var adapter = new VisualEffectAdapter("test", "test", model);
        
        // At t=0, circle is at 0.5, 0.5. Evaluate at 0.5, 0.5 should hit.
        var c1 = adapter.Evaluate(new Point2D(0.5f, 0.5f), 0);
        Assert.Equal(255, c1.R);
        
        // At t=1, speed=50 => dist = 50*1*0.01 = 0.5. Circle moved by 0.5 to X=1.0. (Wrap around keeps it at 0)
        // Wait, wrap around logic: layerX = layerX - Math.Floor(layerX). 
        // 1.0 - Math.Floor(1.0) = 0.
        // We evaluate at 0.0, 0.5 => should hit.
        var c2 = adapter.Evaluate(new Point2D(0.0f, 0.5f), 1.0);
        Assert.Equal(255, c2.R);
        
        // Original spot should now be black (dist = 0.5, which is > 0.2)
        var c3 = adapter.Evaluate(new Point2D(0.5f, 0.5f), 1.0);
        Assert.Equal(0, c3.R);
    }
}
