using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace XenRGB.Effects.VisualEditor;

public class VisualEffectModel
{
    public int Version { get; set; } = 1;
    public List<VisualLayer> Layers { get; set; } = new();
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CircleLayer), "circle")]
[JsonDerivedType(typeof(RectangleLayer), "rectangle")]
[JsonDerivedType(typeof(GradientLayer), "gradient")]
[JsonDerivedType(typeof(ColorLayer), "color")]
public abstract class VisualLayer
{
    public string Name { get; set; } = "Layer";
    public double X { get; set; } = 0.5;
    public double Y { get; set; } = 0.5;
    public double Opacity { get; set; } = 1.0;
    public AnimationConfig Animation { get; set; } = new();
}

public class AnimationConfig
{
    public string Type { get; set; } = "none";
    public double Direction { get; set; } = 0;
    public double Speed { get; set; } = 0;
    public double Amplitude { get; set; } = 0;
    public double Frequency { get; set; } = 0;
    public double RotationSpeed { get; set; } = 0;
}

public class CircleLayer : VisualLayer
{
    public double Radius { get; set; } = 0.2;
    public string Color { get; set; } = "#FF0000";
}

public class RectangleLayer : VisualLayer
{
    public double Width { get; set; } = 0.2;
    public double Height { get; set; } = 0.2;
    public double Rotation { get; set; } = 0;
    public string Color { get; set; } = "#00FF00";
}

public class ColorLayer : VisualLayer
{
    public string Color { get; set; } = "#0000FF";
}

public class GradientLayer : VisualLayer
{
    public string StartColor { get; set; } = "#FF0000";
    public string EndColor { get; set; } = "#0000FF";
    public double Angle { get; set; } = 0;
}
