using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XenRGB.Core.Canvas;
using XenRGB.Core.Devices;
using XenRGB.Core.Engine;
using XenRGB.Devices.Mock;
using XenRGB.Devices.OpenRgb;
using XenRGB.Effects.BuiltIn;
using XenRGB.SDK;
using Xunit;

namespace XenRGB.Tests;

public class SpatialEffectTests
{
    [Fact]
    public void Requirement1_LedInCenterOfCanvas_GetsCenterColor()
    {
        var canvas = new SpatialCanvas();
        var placement = new CanvasDevicePlacement("dev1", "Keyboard", 0.5f, 0.5f, 0.4f, 0.2f);
        canvas.AddOrUpdatePlacement(placement);

        var centerLed = new LedCoordinate(0, 0.5f, 0.5f, "CenterKey");
        var dev = new MockRgbDevice("dev1", "Keyboard", RgbDeviceType.Keyboard, new[] { centerLed });

        var mapped = canvas.GetMappedLeds(dev);
        Assert.Single(mapped);
        Assert.Equal(0.5f, mapped[0].CanvasPoint.X, 0.0001f);
        Assert.Equal(0.5f, mapped[0].CanvasPoint.Y, 0.0001f);

        var effect = new CottonCandyEffect();
        effect.Initialize();

        var ledColor = effect.Evaluate(mapped[0].CanvasPoint, 1.0);
        var expectedCenterColor = effect.Evaluate(Point2D.Center, 1.0);

        Assert.Equal(expectedCenterColor, ledColor);
    }

    [Fact]
    public void Requirement2_LedsAtDifferentCoordinates_ReceiveDifferentColors_FromSpatialEffect()
    {
        var effect = new RainbowEffect();
        effect.Initialize();

        var ptA = new Point2D(0.1f, 0.2f);
        var ptB = new Point2D(0.8f, 0.9f);

        var colorA = effect.Evaluate(ptA, 0.0);
        var colorB = effect.Evaluate(ptB, 0.0);

        Assert.NotEqual(colorA, colorB);
    }

    [Fact]
    public void Requirement3_RotationTransform_RotatesPointCorrectly()
    {
        var placement = new CanvasDevicePlacement("dev1", "Device", 0.5f, 0.5f, 0.4f, 0.2f, 90f);

        // Local right edge (1.0, 0.5) with width 0.4 rotated 90 deg clockwise gives (0.5, 0.7)
        var pt = placement.TransformLocalToCanvas(1.0f, 0.5f);

        Assert.Equal(0.5f, pt.X, 0.0001f);
        Assert.Equal(0.7f, pt.Y, 0.0001f);
    }

    [Fact]
    public void Requirement4_ScaleTransform_ScalesOffsetCorrectly()
    {
        var placement = new CanvasDevicePlacement("dev1", "Device", 0.5f, 0.5f, 0.8f, 0.4f);

        var topLeft = placement.TransformLocalToCanvas(0.0f, 0.0f);
        Assert.Equal(0.1f, topLeft.X, 0.0001f);
        Assert.Equal(0.3f, topLeft.Y, 0.0001f);

        var bottomRight = placement.TransformLocalToCanvas(1.0f, 1.0f);
        Assert.Equal(0.9f, bottomRight.X, 0.0001f);
        Assert.Equal(0.7f, bottomRight.Y, 0.0001f);
    }

    [Fact]
    public void Requirement5_FlipTransform_FlipsHorizontalAndVertical()
    {
        var normalPlacement = new CanvasDevicePlacement("dev1", "Device", 0.5f, 0.5f, 0.4f, 0.2f);
        var flippedPlacement = new CanvasDevicePlacement("dev1", "Device", 0.5f, 0.5f, 0.4f, 0.2f)
        {
            FlipHorizontal = true,
            FlipVertical = true
        };

        var normalPt = normalPlacement.TransformLocalToCanvas(0.0f, 0.0f);
        var flippedPt = flippedPlacement.TransformLocalToCanvas(0.0f, 0.0f);

        Assert.Equal(0.3f, normalPt.X, 0.0001f);
        Assert.Equal(0.4f, normalPt.Y, 0.0001f);

        Assert.Equal(0.7f, flippedPt.X, 0.0001f);
        Assert.Equal(0.6f, flippedPt.Y, 0.0001f);
    }

    [Fact]
    public void Requirement6_RainbowEffect_GivesDifferentHuesForDifferentCoordinates()
    {
        var rainbow = new RainbowEffect();
        rainbow.Initialize();

        var colorLeft = rainbow.Evaluate(new Point2D(0.1f, 0.5f), 1.0);
        var colorRight = rainbow.Evaluate(new Point2D(0.9f, 0.5f), 1.0);

        Assert.NotEqual(colorLeft, colorRight);

        colorLeft.ToHsv(out float hueLeft, out _, out _);
        colorRight.ToHsv(out float hueRight, out _, out _);

        float hueDiff = MathF.Abs(hueLeft - hueRight);
        if (hueDiff > 180f) hueDiff = 360f - hueDiff;

        Assert.True(hueDiff > 20f, $"Expected hue difference between keys, but got {hueDiff}");
    }

    [Fact]
    public void Requirement7_BiohazardEffect_GivesDifferentBrightnessForDifferentCoordinates()
    {
        var biohazard = new BiohazardEffect();
        biohazard.Initialize();

        // Sample across multiple distinct LED coordinates
        var c1 = biohazard.Evaluate(new Point2D(0.2f, 0.3f), 1.5);
        var c2 = biohazard.Evaluate(new Point2D(0.7f, 0.8f), 1.5);
        var c3 = biohazard.Evaluate(new Point2D(0.4f, 0.6f), 1.5);

        // At least two of them should have different colors/intensities due to spatial hash & wave position
        bool hasVariation = c1 != c2 || c2 != c3 || c1 != c3;
        Assert.True(hasVariation, "Biohazard should yield spatial variation across different LED positions");
    }

    [Fact]
    public void Requirement8_CottonCandyEffect_ChangesOverTime()
    {
        var effect = new CottonCandyEffect();
        effect.Initialize();

        var pt = new Point2D(0.4f, 0.5f);
        var colorT0 = effect.Evaluate(pt, 0.0);
        var colorT3 = effect.Evaluate(pt, 3.5);

        Assert.NotEqual(colorT0, colorT3);
    }

    [Fact]
    public void Requirement9_PlainColorEffect_RemainsIdenticalAcrossCoordinates()
    {
        var plain = new PlainColorEffect();
        plain.Initialize();

        var c1 = plain.Evaluate(new Point2D(0.1f, 0.1f), 0.0);
        var c2 = plain.Evaluate(new Point2D(0.5f, 0.5f), 10.0);
        var c3 = plain.Evaluate(new Point2D(0.9f, 0.9f), 25.0);

        Assert.Equal(c1, c2);
        Assert.Equal(c2, c3);
    }

    [Fact]
    public async Task Requirement10_RgbEngine_PerformsPerLedSpatialRendering()
    {
        var canvas = new SpatialCanvas();
        var placement = new CanvasDevicePlacement("kb1", "Keyboard", 0.5f, 0.5f, 0.6f, 0.3f);
        canvas.AddOrUpdatePlacement(placement);

        var leds = new List<LedCoordinate>
        {
            new(0, 0.0f, 0.0f, "Key_Esc"),
            new(1, 1.0f, 1.0f, "Key_Enter")
        };
        var device = new MockRgbDevice("kb1", "Keyboard", RgbDeviceType.Keyboard, leds);

        using var engine = new RgbEngine(canvas);
        engine.RegisterDevice(device);

        var rainbow = new RainbowEffect();
        rainbow.Initialize();
        engine.ActiveEffect = rainbow;

        engine.Start();
        await Task.Delay(100);

        // Verify that LED 0 and LED 1 have different colors evaluated from spatial canvas
        Assert.NotEqual(device.CurrentColors[0], device.CurrentColors[1]);
    }

    [Fact]
    public async Task OpenRgbProvider_KeyboardLeds_HaveDistinct2DRowCoordinates()
    {
        var client = new SpatialProtocolMockClient { ServerProtocolVersion = 4 };
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write((uint)0);
        writer.Write((int)5); // Keyboard
        WriteString(writer, "HyperX Alloy Origins Core");
        WriteString(writer, "HyperX");
        WriteString(writer, "Description");
        WriteString(writer, "v1");
        WriteString(writer, "SN123");
        WriteString(writer, "USB");

        writer.Write((ushort)0); // modes = 0
        writer.Write((int)0);
        writer.Write((ushort)0); // zones = 0

        writer.Write((ushort)87); // 87 LEDs
        for (int i = 0; i < 87; i++)
        {
            WriteString(writer, $"Key {i}");
            writer.Write((uint)i);
        }


        // Device-level colors block (always present per spec, even if num_colors == 0)
        writer.Write((ushort)0); // num_colors = 0
        client.ControllerData = ms.ToArray();

        using var provider = new OpenRgbProvider(client, "127.0.0.1", 6742);
        var devices = await provider.DiscoverDevicesAsync();

        Assert.Single(devices);
        var kb = devices[0];
        Assert.Equal(87, kb.LedCoordinates.Count);

        // Key in Row 0 (Esc, index 0) vs Key in Row 3 (Caps, index 48)
        var keyRow0 = kb.LedCoordinates[0];
        var keyRow3 = kb.LedCoordinates[48];

        Assert.NotEqual(keyRow0.LocalY, keyRow3.LocalY);
        Assert.True(keyRow0.LocalY < keyRow3.LocalY, "Row 0 should have smaller Y than Row 3");
    }

    private static void WriteString(BinaryWriter writer, string str)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
        writer.Write((ushort)(bytes.Length + 1));
        writer.Write(bytes);
        writer.Write((byte)0);
    }

    private sealed class SpatialProtocolMockClient : IOpenRgbClient
    {
        public bool IsConnected { get; set; } = true;
        public uint ServerProtocolVersion { get; set; } = 4;
        public uint ControllerCount { get; set; } = 1;
        public byte[] ControllerData { get; set; } = Array.Empty<byte>();

        public Task<bool> ConnectAsync(string host, int port, System.Threading.CancellationToken ct = default) => Task.FromResult(true);
        public Task<uint> GetControllerCountAsync(System.Threading.CancellationToken ct = default) => Task.FromResult(ControllerCount);
        public Task<byte[]> GetControllerDataRawAsync(uint controllerIndex, System.Threading.CancellationToken ct = default) => Task.FromResult(ControllerData);
        public Task SendUpdateLedsAsync(uint controllerIndex, ReadOnlyMemory<uint> bgrColors, System.Threading.CancellationToken ct = default) => Task.CompletedTask;
        public Task RequestRescanAsync(System.Threading.CancellationToken ct = default) => Task.CompletedTask;
        public void Disconnect() => IsConnected = false;
        public void Dispose() { }
    }
}
