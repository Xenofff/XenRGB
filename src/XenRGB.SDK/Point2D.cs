using System;

namespace XenRGB.SDK;

/// <summary>
/// Represents a 2D point, typically in normalized [0..1] canvas coordinates.
/// </summary>
public readonly struct Point2D : IEquatable<Point2D>
{
    public float X { get; }
    public float Y { get; }

    public static readonly Point2D Zero = new(0f, 0f);
    public static readonly Point2D Center = new(0.5f, 0.5f);
    public static readonly Point2D One = new(1f, 1f);

    public Point2D(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float DistanceTo(Point2D other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public float DistanceSquaredTo(Point2D other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        return dx * dx + dy * dy;
    }

    public static Point2D Lerp(Point2D a, Point2D b, float t)
    {
        return new Point2D(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t
        );
    }

    public Point2D Clamp(float minX = 0f, float maxX = 1f, float minY = 0f, float maxY = 1f)
    {
        return new Point2D(
            Math.Clamp(X, minX, maxX),
            Math.Clamp(Y, minY, maxY)
        );
    }

    public override string ToString() => $"({X:F3}, {Y:F3})";

    public bool Equals(Point2D other) => MathF.Abs(X - other.X) < 0.00001f && MathF.Abs(Y - other.Y) < 0.00001f;

    public override bool Equals(object? obj) => obj is Point2D other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(Point2D left, Point2D right) => left.Equals(right);

    public static bool operator !=(Point2D left, Point2D right) => !left.Equals(right);
}
