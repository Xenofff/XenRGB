using XenRGB.SDK;

namespace XenRGB.Core.Devices;

/// <summary>
/// Physical or logical layout coordinate of a specific LED on a device.
/// Coordinates (LocalX, LocalY) are normalized in the device's local bounding box [0..1].
/// </summary>
public sealed class LedCoordinate
{
    public int Index { get; }
    public float LocalX { get; }
    public float LocalY { get; }
    public string? Name { get; }

    public LedCoordinate(int index, float localX, float localY, string? name = null)
    {
        Index = index;
        LocalX = localX;
        LocalY = localY;
        Name = name;
    }

    public Point2D LocalPoint => new(LocalX, LocalY);

    public override string ToString() => $"LED[{Index}]: ({LocalX:F2}, {LocalY:F2}) - {Name ?? "LED"}";
}
