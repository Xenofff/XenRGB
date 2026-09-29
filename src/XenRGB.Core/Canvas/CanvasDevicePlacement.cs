using System;
using XenRGB.SDK;

namespace XenRGB.Core.Canvas;

/// <summary>
/// Logical placement of an RGB device on the 2D spatial canvas.
/// All coordinates and dimensions are normalized in the [0..1] range.
/// </summary>
public sealed class CanvasDevicePlacement
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Center X coordinate on the canvas (0.0 to 1.0).
    /// </summary>
    public float X { get; set; } = 0.5f;

    /// <summary>
    /// Center Y coordinate on the canvas (0.0 to 1.0).
    /// </summary>
    public float Y { get; set; } = 0.5f;

    /// <summary>
    /// Normalized width on the canvas (0.0 to 1.0).
    /// </summary>
    public float Width { get; set; } = 0.3f;

    /// <summary>
    /// Normalized height on the canvas (0.0 to 1.0).
    /// </summary>
    public float Height { get; set; } = 0.15f;

    /// <summary>
    /// Rotation in degrees (clockwise).
    /// </summary>
    public float RotationDegrees { get; set; } = 0f;

    public bool FlipHorizontal { get; set; } = false;
    public bool FlipVertical { get; set; } = false;

    public bool IsEnabled { get; set; } = true;

    public CanvasDevicePlacement() { }

    public CanvasDevicePlacement(string deviceId, string deviceName, float x, float y, float width, float height, float rotationDegrees = 0f)
    {
        DeviceId = deviceId;
        DeviceName = deviceName;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        RotationDegrees = rotationDegrees;
        IsEnabled = true;
    }

    /// <summary>
    /// Transforms a device's normalized local LED coordinate (0..1, 0..1)
    /// to the global normalized 2D canvas coordinate (0..1, 0..1).
    /// </summary>
    public Point2D TransformLocalToCanvas(float localX, float localY)
    {
        // Convert local [0..1] to centered relative offset [-0.5..0.5] * dimension
        float rx = (localX - 0.5f) * Width;
        float ry = (localY - 0.5f) * Height;

        if (FlipHorizontal) rx = -rx;
        if (FlipVertical) ry = -ry;

        // Apply rotation if needed
        if (MathF.Abs(RotationDegrees) > 0.001f)
        {
            float rad = RotationDegrees * (MathF.PI / 180f);
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);

            float rotatedX = rx * cos - ry * sin;
            float rotatedY = rx * sin + ry * cos;

            rx = rotatedX;
            ry = rotatedY;
        }

        return new Point2D(X + rx, Y + ry);
    }

    /// <summary>
    /// Clones this placement configuration.
    /// </summary>
    public CanvasDevicePlacement Clone()
    {
        return new CanvasDevicePlacement(DeviceId, DeviceName, X, Y, Width, Height, RotationDegrees)
        {
            IsEnabled = IsEnabled,
            FlipHorizontal = FlipHorizontal,
            FlipVertical = FlipVertical
        };
    }
}
