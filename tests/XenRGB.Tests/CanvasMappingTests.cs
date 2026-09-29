using System;
using System.Collections.Generic;
using XenRGB.Core.Canvas;
using XenRGB.Core.Devices;
using XenRGB.Devices.Mock;
using XenRGB.SDK;
using Xunit;

namespace XenRGB.Tests;

public class CanvasMappingTests
{
    [Fact]
    public void TransformLocalToCanvas_CenterPoint_MapsToPlacementCenter()
    {
        var placement = new CanvasDevicePlacement("dev1", "Device 1", 0.5f, 0.5f, 0.4f, 0.2f);
        var pt = placement.TransformLocalToCanvas(0.5f, 0.5f);

        Assert.Equal(0.5f, pt.X, 0.0001f);
        Assert.Equal(0.5f, pt.Y, 0.0001f);
    }

    [Fact]
    public void TransformLocalToCanvas_Corner_MapsToCalculatedBounds()
    {
        var placement = new CanvasDevicePlacement("dev1", "Device 1", 0.5f, 0.5f, 0.4f, 0.2f);
        // Top-left is local (0, 0): X = 0.5 - 0.2 = 0.3, Y = 0.5 - 0.1 = 0.4
        var topLeft = placement.TransformLocalToCanvas(0.0f, 0.0f);

        Assert.Equal(0.3f, topLeft.X, 0.0001f);
        Assert.Equal(0.4f, topLeft.Y, 0.0001f);

        // Bottom-right is local (1, 1): X = 0.5 + 0.2 = 0.7, Y = 0.5 + 0.1 = 0.6
        var bottomRight = placement.TransformLocalToCanvas(1.0f, 1.0f);

        Assert.Equal(0.7f, bottomRight.X, 0.0001f);
        Assert.Equal(0.6f, bottomRight.Y, 0.0001f);
    }

    [Fact]
    public void TransformLocalToCanvas_90DegreeRotation_RotatesPoint()
    {
        var placement = new CanvasDevicePlacement("dev1", "Device 1", 0.5f, 0.5f, 0.4f, 0.2f, 90f);
        // Local right edge (1.0, 0.5) with width 0.4, unrotated would be (+0.2, 0)
        // Rotated 90 deg clockwise: (0, +0.2)
        var rightEdge = placement.TransformLocalToCanvas(1.0f, 0.5f);

        Assert.Equal(0.5f, rightEdge.X, 0.0001f);
        Assert.Equal(0.7f, rightEdge.Y, 0.0001f);
    }

    [Fact]
    public void SpatialCanvas_GetMappedLeds_MapsAllDeviceLeds()
    {
        var canvas = new SpatialCanvas();
        var placement = new CanvasDevicePlacement("mock-keyboard", "Keyboard", 0.5f, 0.5f, 0.4f, 0.2f);
        canvas.AddOrUpdatePlacement(placement);

        var leds = new List<LedCoordinate>
        {
            new(0, 0f, 0f, "Esc"),
            new(1, 0.5f, 0.5f, "H"),
            new(2, 1f, 1f, "Enter")
        };
        var device = new MockRgbDevice("mock-keyboard", "Keyboard", RgbDeviceType.Keyboard, leds);

        var mapped = canvas.GetMappedLeds(device);

        Assert.Equal(3, mapped.Length);
        Assert.Equal(0, mapped[0].LedIndex);
        Assert.Equal(0.3f, mapped[0].CanvasPoint.X, 0.001f);
        Assert.Equal(0.4f, mapped[0].CanvasPoint.Y, 0.001f);

        Assert.Equal(1, mapped[1].LedIndex);
        Assert.Equal(0.5f, mapped[1].CanvasPoint.X, 0.001f);
        Assert.Equal(0.5f, mapped[1].CanvasPoint.Y, 0.001f);
    }
}
