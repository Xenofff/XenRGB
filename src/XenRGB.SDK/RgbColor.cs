using System;
using System.Globalization;

namespace XenRGB.SDK;

/// <summary>
/// Represents a 24-bit RGB color with mathematical and conversion utilities.
/// </summary>
public readonly struct RgbColor : IEquatable<RgbColor>
{
    public byte R { get; }
    public byte G { get; }
    public byte B { get; }

    public static readonly RgbColor Black = new(0, 0, 0);
    public static readonly RgbColor White = new(255, 255, 255);
    public static readonly RgbColor Red = new(255, 0, 0);
    public static readonly RgbColor Green = new(0, 255, 0);
    public static readonly RgbColor Blue = new(0, 0, 255);
    public static readonly RgbColor Yellow = new(255, 255, 0);
    public static readonly RgbColor Cyan = new(0, 255, 255);
    public static readonly RgbColor Magenta = new(255, 0, 255);

    public RgbColor(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    public static RgbColor FromRgb(byte r, byte g, byte b) => new(r, g, b);

    public static RgbColor FromRgb(int r, int g, int b) =>
        new((byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));

    public static RgbColor FromRgb(float r, float g, float b) =>
        new((byte)Math.Clamp((int)(r * 255f + 0.5f), 0, 255),
            (byte)Math.Clamp((int)(g * 255f + 0.5f), 0, 255),
            (byte)Math.Clamp((int)(b * 255f + 0.5f), 0, 255));

    /// <summary>
    /// Creates an RGB color from HSV (Hue: 0..360, Saturation: 0..1, Value/Brightness: 0..1).
    /// </summary>
    public static RgbColor FromHsv(float hue, float saturation, float value)
    {
        hue = (hue % 360f + 360f) % 360f;
        saturation = Math.Clamp(saturation, 0f, 1f);
        value = Math.Clamp(value, 0f, 1f);

        if (saturation <= 0.0001f)
        {
            byte v = (byte)Math.Round(value * 255f);
            return new RgbColor(v, v, v);
        }

        float h = hue / 60f;
        int i = (int)MathF.Floor(h);
        float f = h - i;
        float p = value * (1f - saturation);
        float q = value * (1f - saturation * f);
        float t = value * (1f - saturation * (1f - f));

        return i switch
        {
            0 => FromRgb(value, t, p),
            1 => FromRgb(q, value, p),
            2 => FromRgb(p, value, t),
            3 => FromRgb(p, q, value),
            4 => FromRgb(t, p, value),
            _ => FromRgb(value, p, q)
        };
    }

    /// <summary>
    /// Converts this RGB color to HSV (Hue: 0..360, Saturation: 0..1, Value: 0..1).
    /// </summary>
    public void ToHsv(out float hue, out float saturation, out float value)
    {
        float r = R / 255f;
        float g = G / 255f;
        float b = B / 255f;

        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float delta = max - min;

        value = max;
        saturation = max <= 0.0001f ? 0f : delta / max;

        if (delta <= 0.0001f)
        {
            hue = 0f;
            return;
        }

        if (MathF.Abs(max - r) < 0.0001f)
        {
            hue = 60f * (((g - b) / delta) % 6f);
        }
        else if (MathF.Abs(max - g) < 0.0001f)
        {
            hue = 60f * (((b - r) / delta) + 2f);
        }
        else
        {
            hue = 60f * (((r - g) / delta) + 4f);
        }

        if (hue < 0f)
            hue += 360f;
    }

    /// <summary>
    /// Linearly interpolates between two colors.
    /// </summary>
    public static RgbColor Lerp(RgbColor a, RgbColor b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new RgbColor(
            (byte)Math.Round(a.R + (b.R - a.R) * t),
            (byte)Math.Round(a.G + (b.G - a.G) * t),
            (byte)Math.Round(a.B + (b.B - a.B) * t)
        );
    }

    /// <summary>
    /// Scales the color by a brightness factor (0.0 to 1.0).
    /// </summary>
    public RgbColor WithBrightness(float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        return new RgbColor(
            (byte)Math.Round(R * factor),
            (byte)Math.Round(G * factor),
            (byte)Math.Round(B * factor)
        );
    }

    /// <summary>
    /// Blends a foreground color over this color with an alpha factor (0.0 to 1.0).
    /// </summary>
    public RgbColor Blend(RgbColor foreground, float alpha)
    {
        return Lerp(this, foreground, alpha);
    }

    /// <summary>
    /// Parses a hex color string like "#RRGGBB", "RRGGBB", "#RGB", or "RGB".
    /// </summary>
    public static RgbColor FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return Black;

        hex = hex.Trim().TrimStart('#');

        if (hex.Length == 3)
        {
            byte r = byte.Parse(string.Concat(hex[0], hex[0]), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(string.Concat(hex[1], hex[1]), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(string.Concat(hex[2], hex[2]), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new RgbColor(r, g, b);
        }

        if (hex.Length >= 6)
        {
            byte r = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new RgbColor(r, g, b);
        }

        return Black;
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public uint ToRgba32() => (uint)((R << 24) | (G << 16) | (B << 8) | 0xFF);

    public uint ToBgr32() => (uint)(B | (G << 8) | (R << 16));

    public override string ToString() => $"RgbColor({R}, {G}, {B})";

    public bool Equals(RgbColor other) => R == other.R && G == other.G && B == other.B;

    public override bool Equals(object? obj) => obj is RgbColor other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(R, G, B);

    public static bool operator ==(RgbColor left, RgbColor right) => left.Equals(right);

    public static bool operator !=(RgbColor left, RgbColor right) => !left.Equals(right);
}
