using System;
using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.VisualEditor;

public class VisualEffectAdapter : EffectBase
{
    private readonly VisualEffectModel _model;
    private readonly string _id;
    private readonly string _name;

    public override string Id => _id;
    public override string Name => _name;
    public override string Description => "Visual Editor Effect";
    public override EffectManifest Manifest => new EffectManifest { Id = _id, Name = _name };
    public override IReadOnlyList<EffectParameter> Parameters => Array.Empty<EffectParameter>();

    public VisualEffectAdapter(string id, string name, VisualEffectModel model)
    {
        _id = id;
        _name = name;
        _model = model;
    }

    public override RgbColor Evaluate(Point2D position, double time)
    {
        RgbColor finalColor = RgbColor.Black;
        
        foreach (var layer in _model.Layers)
        {
            RgbColor layerColor = RgbColor.Black;
            double alpha = layer.Opacity;
            bool hit = false;

            double animX = 0;
            double animY = 0;

            if (layer.Animation.Type == "move")
            {
                double rad = layer.Animation.Direction * Math.PI / 180.0;
                double dist = layer.Animation.Speed * time * 0.01;
                animX = Math.Cos(rad) * dist;
                animY = Math.Sin(rad) * dist;
                
                if (layer.Animation.Frequency > 0)
                {
                    double wave = Math.Sin(time * layer.Animation.Frequency * Math.PI * 2) * (layer.Animation.Amplitude * 0.01);
                    animX += Math.Cos(rad + Math.PI/2) * wave;
                    animY += Math.Sin(rad + Math.PI/2) * wave;
                }
            }

            double layerX = layer.X + animX;
            double layerY = layer.Y + animY;

            // Wrap around 0..1 roughly
            layerX = layerX - Math.Floor(layerX);
            layerY = layerY - Math.Floor(layerY);

            if (layer is ColorLayer cl)
            {
                layerColor = RgbColor.FromHex(cl.Color);
                hit = true;
            }
            else if (layer is CircleLayer circle)
            {
                double dx = position.X - layerX;
                double dy = position.Y - layerY;
                double distSq = dx * dx + dy * dy;
                if (distSq <= circle.Radius * circle.Radius)
                {
                    layerColor = RgbColor.FromHex(circle.Color);
                    hit = true;
                }
            }
            else if (layer is RectangleLayer rect)
            {
                double dx = Math.Abs(position.X - layerX);
                double dy = Math.Abs(position.Y - layerY);
                if (dx <= rect.Width / 2 && dy <= rect.Height / 2)
                {
                    layerColor = RgbColor.FromHex(rect.Color);
                    hit = true;
                }
            }
            else if (layer is GradientLayer grad)
            {
                double t = Math.Clamp(position.X, 0, 1);
                var start = RgbColor.FromHex(grad.StartColor);
                var end = RgbColor.FromHex(grad.EndColor);
                layerColor = Interpolate(start, end, (float)t);
                hit = true;
            }

            if (hit && alpha > 0)
            {
                finalColor = Blend(finalColor, layerColor, alpha);
            }
        }
        
        return finalColor;
    }
    
    private RgbColor Blend(RgbColor bg, RgbColor fg, double fgAlpha)
    {
        byte r = (byte)Math.Clamp(bg.R * (1 - fgAlpha) + fg.R * fgAlpha, 0, 255);
        byte g = (byte)Math.Clamp(bg.G * (1 - fgAlpha) + fg.G * fgAlpha, 0, 255);
        byte b = (byte)Math.Clamp(bg.B * (1 - fgAlpha) + fg.B * fgAlpha, 0, 255);
        return new RgbColor(r, g, b);
    }
    
    private RgbColor Interpolate(RgbColor c1, RgbColor c2, float t)
    {
        byte r = (byte)(c1.R + (c2.R - c1.R) * t);
        byte g = (byte)(c1.G + (c2.G - c1.G) * t);
        byte b = (byte)(c1.B + (c2.B - c1.B) * t);
        return new RgbColor(r, g, b);
    }
}
