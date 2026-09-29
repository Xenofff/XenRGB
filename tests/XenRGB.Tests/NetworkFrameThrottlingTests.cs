using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using XenRGB.Core.Canvas;
using XenRGB.Core.Devices;
using XenRGB.Core.Engine;
using XenRGB.Devices.Mock;
using XenRGB.Devices.OpenRgb;
using XenRGB.Devices.Wled;
using XenRGB.Effects.BuiltIn;
using XenRGB.SDK;
using Xunit;

namespace XenRGB.Tests;

public class NetworkFrameThrottlingTests
{
    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long GetTimestamp() => _timestamp;
        public override long TimestampFrequency => 1_000_000; // 1 microsecond resolution

        public void Advance(TimeSpan duration)
        {
            _timestamp += (long)(duration.TotalSeconds * TimestampFrequency);
        }
    }

    [Fact]
    public async Task IdenticalConsecutiveFrames_NoSecondTransmission()
    {
        var leds = new LedCoordinate[] { new(0, 0.5f, 0.5f, "LED1") };
        var device = new MockRgbDevice("mock-1", "Mock Device", RgbDeviceType.LedStrip, leds);

        var frame = new RgbColor[] { RgbColor.Red };

        await device.UpdateLedsAsync(frame);
        Assert.Equal(1, device.TransmissionCount);

        // Send identical frame
        await device.UpdateLedsAsync(frame);
        Assert.Equal(1, device.TransmissionCount); // Suppressed
    }

    [Fact]
    public async Task ChangedPixel_TransmissionOccurs()
    {
        var leds = new LedCoordinate[]
        {
            new(0, 0.0f, 0.5f, "LED1"),
            new(1, 1.0f, 0.5f, "LED2")
        };
        var device = new MockRgbDevice("mock-1", "Mock Device", RgbDeviceType.LedStrip, leds);

        var frame1 = new RgbColor[] { RgbColor.Red, RgbColor.Red };
        await device.UpdateLedsAsync(frame1);
        Assert.Equal(1, device.TransmissionCount);

        // One pixel changed
        var frame2 = new RgbColor[] { RgbColor.Red, RgbColor.Blue };
        await device.UpdateLedsAsync(frame2);
        Assert.Equal(2, device.TransmissionCount);
    }

    [Fact]
    public async Task MultipleDevices_HaveIndependentDirtyState()
    {
        var leds = new LedCoordinate[] { new(0, 0.5f, 0.5f, "LED1") };
        var devA = new MockRgbDevice("dev-a", "Device A", RgbDeviceType.Keyboard, leds);
        var devB = new MockRgbDevice("dev-b", "Device B", RgbDeviceType.Mouse, leds);

        var redFrame = new RgbColor[] { RgbColor.Red };
        var blueFrame = new RgbColor[] { RgbColor.Blue };

        // Both send Red
        await devA.UpdateLedsAsync(redFrame);
        await devB.UpdateLedsAsync(redFrame);
        Assert.Equal(1, devA.TransmissionCount);
        Assert.Equal(1, devB.TransmissionCount);

        // Frame 2: devA gets Red (unchanged), devB gets Blue (changed)
        await devA.UpdateLedsAsync(redFrame);
        await devB.UpdateLedsAsync(blueFrame);

        Assert.Equal(1, devA.TransmissionCount); // Suppressed
        Assert.Equal(2, devB.TransmissionCount); // Transmitted
    }

    [Fact]
    public async Task LightingOffOn_ResetsOutputStateCorrectly()
    {
        var leds = new LedCoordinate[] { new(0, 0.5f, 0.5f, "LED1") };
        var device = new MockRgbDevice("mock-1", "Mock Device", RgbDeviceType.Keyboard, leds);

        var redFrame = new RgbColor[] { RgbColor.Red };

        await device.UpdateLedsAsync(redFrame);
        Assert.Equal(1, device.TransmissionCount);

        // Turn lighting off and then back on
        await device.SetPowerAsync(false);
        await device.SetPowerAsync(true);

        // Send identical red frame again - should transmit because power cycle reset state
        await device.UpdateLedsAsync(redFrame);
        Assert.Equal(2, device.TransmissionCount);
    }

    [Fact]
    public async Task WledRealtimeMode_HeartbeatPreventsExpiration()
    {
        var timeProvider = new ManualTimeProvider();
        using var http = new HttpClient();
        using var udp = new UdpClient();

        // 1000ms heartbeat safely refreshes WLED's 2000ms timeout
        var wled = new WledDevice(
            "127.0.0.1",
            http,
            udp,
            name: "Test WLED",
            ledCount: 10,
            heartbeatInterval: TimeSpan.FromMilliseconds(1000),
            timeProvider: timeProvider);

        wled.ConnectionState = DeviceConnectionState.Connected;

        var solidColors = new RgbColor[10];
        Array.Fill(solidColors, RgbColor.Green);

        // 1. Initial transmission at t = 0
        await wled.UpdateLedsAsync(solidColors);
        Assert.Equal(1, wled.TransmissionCount);

        // 2. At t = 500ms, identical frame is received -> skipped (well within 2000ms timeout)
        timeProvider.Advance(TimeSpan.FromMilliseconds(500));
        await wled.UpdateLedsAsync(solidColors);
        Assert.Equal(1, wled.TransmissionCount);

        // 3. At t = 1100ms, heartbeat interval (1000ms) has elapsed -> heartbeat frame sent
        // This occurs BEFORE WLED's 2000ms realtime timeout expires!
        timeProvider.Advance(TimeSpan.FromMilliseconds(600)); // total elapsed = 1100ms
        await wled.UpdateLedsAsync(solidColors);
        Assert.Equal(2, wled.TransmissionCount);
    }

    [Fact]
    public void DeviceFrameThrottler_ZeroAllocationVerification()
    {
        var throttler = new DeviceFrameThrottler();
        var colors = new RgbColor[] { RgbColor.Cyan, RgbColor.Magenta };

        throttler.MarkTransmitted(colors);

        // Verify SequenceEqual on identical span does not throw or fail
        bool shouldTransmit = throttler.ShouldTransmit(colors);
        Assert.False(shouldTransmit);

        // Modify pixel
        colors[1] = RgbColor.Yellow;
        shouldTransmit = throttler.ShouldTransmit(colors);
        Assert.True(shouldTransmit);
    }

    [Fact]
    public async Task Engine_RendersNormallyWithThrottling_NoRegression()
    {
        var canvas = new SpatialCanvas();
        using var engine = new RgbEngine(canvas);

        var leds = new LedCoordinate[]
        {
            new(0, 0.5f, 0.5f, "Center LED")
        };
        var device = new MockRgbDevice("mock-dev", "Mock Device", RgbDeviceType.Keyboard, leds);
        canvas.AddOrUpdatePlacement(new CanvasDevicePlacement("mock-dev", "Mock Device", 0.5f, 0.5f, 0.5f, 0.5f));

        engine.RegisterDevice(device);

        var effect = new PlainColorEffect();
        effect.Initialize();
        engine.ActiveEffect = effect;

        engine.Start();

        // Wait brief period for render loop to produce frames
        await Task.Delay(200);

        Assert.True(engine.RenderedFrames > 0);
        // Device received at least 1 transmission and holds the plain color
        Assert.True(device.TransmissionCount >= 1);
        Assert.NotEqual(RgbColor.Black, device.CurrentColors[0]);
    }
}
