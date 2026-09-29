using System;
using System.IO;
using System.Text;
using System.Threading;
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

public class OpenRgbAutoReconnectTests
{
    private sealed class FakeOpenRgbClient : IOpenRgbClient
    {
        public bool ShouldFailConnect { get; set; }
        public bool IsConnected { get; set; }
        public uint ServerProtocolVersion => 4;
        public int ConnectCallCount { get; private set; }
        public int UpdateLedsCallCount { get; private set; }
        public bool FailUpdateLeds { get; set; }
        public uint ControllerCount { get; set; } = 1;

        public Task<bool> ConnectAsync(string host, int port, CancellationToken ct = default)
        {
            ConnectCallCount++;
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(ct);
            }

            if (ShouldFailConnect)
            {
                IsConnected = false;
                return Task.FromResult(false);
            }

            IsConnected = true;
            return Task.FromResult(true);
        }

        public Task<uint> GetControllerCountAsync(CancellationToken ct = default)
        {
            return Task.FromResult(ControllerCount);
        }

        public Task<byte[]> GetControllerDataRawAsync(uint controllerIndex, CancellationToken ct = default)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            writer.Write((uint)0); // total size placeholder
            writer.Write((int)5); // deviceType = Keyboard (5)
            WriteString(writer, "Test Keyboard");
            WriteString(writer, "Test Vendor");   // vendor (protocol v1+)
            WriteString(writer, "Test Description");
            WriteString(writer, "1.0");
            WriteString(writer, "12345");
            WriteString(writer, "USB");

            writer.Write((ushort)0); // mode count = 0
            writer.Write((int)0);    // active mode

            writer.Write((ushort)0); // zone count = 0

            writer.Write((ushort)1); // led count = 1
            WriteString(writer, "Key 1");
            writer.Write((uint)0);   // led_value (present for protocol < 6)

            // Device-level colors block (always present per spec)
            writer.Write((ushort)0); // num_colors = 0

            return Task.FromResult(ms.ToArray());
        }

        private static void WriteString(BinaryWriter writer, string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str + "\0");
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        public Task SendUpdateLedsAsync(uint controllerIndex, ReadOnlyMemory<uint> bgrColors, CancellationToken ct = default)
        {
            if (FailUpdateLeds)
            {
                IsConnected = false;
                throw new IOException("Simulated OpenRGB socket disconnection");
            }
            UpdateLedsCallCount++;
            return Task.CompletedTask;
        }

        public void Disconnect()
        {
            IsConnected = false;
        }

        public void Dispose()
        {
            IsConnected = false;
        }
    }

    [Fact]
    public async Task OpenRgb_UnavailableAtStartup_LaterBecomesAvailable_ReconnectSucceeds()
    {
        var client = new FakeOpenRgbClient { ShouldFailConnect = true };
        using var provider = new OpenRgbProvider(client, "127.0.0.1", 6742, TimeSpan.FromMilliseconds(50));

        // 1. Startup: OpenRGB server is not running
        await provider.InitializeAsync();
        Assert.False(provider.IsConnected);

        // 2. OpenRGB server starts up
        client.ShouldFailConnect = false;

        // 3. Trigger reconnect attempt
        bool reconnected = await provider.TryConnectAndDiscoverAsync();
        Assert.True(reconnected);
        Assert.True(provider.IsConnected);

        var devices = await provider.DiscoverDevicesAsync();
        Assert.Single(devices);
        Assert.Equal(DeviceConnectionState.Connected, devices[0].ConnectionState);
    }

    [Fact]
    public async Task OpenRgb_DisconnectsDuringOperation_ReconnectSucceeds()
    {
        var client = new FakeOpenRgbClient { ShouldFailConnect = false };
        using var provider = new OpenRgbProvider(client, "127.0.0.1", 6742, TimeSpan.FromMilliseconds(50));

        await provider.InitializeAsync();
        var devices = await provider.DiscoverDevicesAsync();
        var device = (OpenRgbDevice)devices[0];

        Assert.Equal(DeviceConnectionState.Connected, device.ConnectionState);

        // 1. Server disconnects/crashes during LED update
        client.FailUpdateLeds = true;
        await device.UpdateLedsAsync(new RgbColor[] { RgbColor.Red });

        Assert.Equal(DeviceConnectionState.Error, device.ConnectionState);

        // 2. Server recovers
        client.FailUpdateLeds = false;
        client.IsConnected = false; // TCP needs reconnect

        bool reconnected = await provider.TryConnectAndDiscoverAsync();
        Assert.True(reconnected);
        Assert.Equal(DeviceConnectionState.Connected, device.ConnectionState);

        // 3. Resumes output
        await device.UpdateLedsAsync(new RgbColor[] { RgbColor.Blue });
        Assert.True(client.UpdateLedsCallCount > 0);
    }

    [Fact]
    public async Task RepeatedReconnectAttempts_DoNotCreateDuplicateConnections()
    {
        var client = new FakeOpenRgbClient { ShouldFailConnect = false };
        using var provider = new OpenRgbProvider(client, "127.0.0.1", 6742, TimeSpan.FromMilliseconds(50));

        await provider.InitializeAsync();

        // Fire 5 concurrent reconnect / discover calls
        var tasks = new Task<bool>[5];
        for (int i = 0; i < 5; i++)
        {
            tasks[i] = provider.TryConnectAndDiscoverAsync();
        }

        await Task.WhenAll(tasks);

        var devices = await provider.DiscoverDevicesAsync();
        // Controller device should not be duplicated in the provider's device list
        Assert.Single(devices);
    }

    [Fact]
    public async Task CancellationDuringReconnect_DoesNotThrow()
    {
        var client = new FakeOpenRgbClient();
        using var provider = new OpenRgbProvider(client, "127.0.0.1", 6742, TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        // Should return cleanly without throwing unhandled exception
        bool result = await provider.TryConnectAndDiscoverAsync(cts.Token);
        Assert.False(result);

        // Disposal while background loop is active must shut down cleanly
        provider.StartReconnectLoop();
        provider.Dispose(); // Must not throw
    }

    [Fact]
    public async Task OtherProvidersContinueWorking_WhileOpenRgbIsUnavailable()
    {
        var canvas = new SpatialCanvas();
        using var engine = new RgbEngine(canvas);

        var client = new FakeOpenRgbClient();
        var openRgbDevice = new OpenRgbDevice(
            client,
            0,
            "openrgb-0",
            "OpenRGB Keyboard",
            RgbDeviceType.Keyboard,
            new LedCoordinate[] { new(0, 0.2f, 0.5f, "LED1") });

        // OpenRGB device is disconnected / in error
        openRgbDevice.ConnectionState = DeviceConnectionState.Error;

        var mockDevice = new MockRgbDevice(
            "mock-leds",
            "Mock Strip",
            RgbDeviceType.LedStrip,
            new LedCoordinate[] { new(0, 0.8f, 0.5f, "LED1") });

        canvas.AddOrUpdatePlacement(new CanvasDevicePlacement("openrgb-0", "OpenRGB", 0.2f, 0.5f, 0.3f, 0.3f));
        canvas.AddOrUpdatePlacement(new CanvasDevicePlacement("mock-leds", "Mock", 0.8f, 0.5f, 0.3f, 0.3f));

        engine.RegisterDevice(openRgbDevice);
        engine.RegisterDevice(mockDevice);

        var effect = new PlainColorEffect();
        effect.Initialize();
        engine.ActiveEffect = effect;

        engine.Start();

        // Render frames
        await Task.Delay(200);

        // Engine continues smoothly:
        Assert.True(engine.RenderedFrames > 0);
        // Mock device continues operating and receives transmissions
        Assert.True(mockDevice.TransmissionCount > 0);
        // OpenRGB was skipped without crashing engine loop
        Assert.Equal(0, client.UpdateLedsCallCount);
    }

    [Fact]
    public async Task OpenRgb_DelayedHardwareDiscovery_PicksUpDeviceWhenReady()
    {
        // Simulate OpenRGB opening port 6742 immediately with 0 devices while USB scanning is underway
        var client = new FakeOpenRgbClient
        {
            ShouldFailConnect = false,
            ControllerCount = 0
        };

        using var provider = new OpenRgbProvider(client, "127.0.0.1", 6742, TimeSpan.FromMilliseconds(50));
        await provider.InitializeAsync();

        Assert.True(provider.IsConnected);
        Assert.Empty(provider.Devices);
        Assert.Contains("scanning devices", provider.ConnectionStatusMessage);

        var discoveredTcs = new TaskCompletionSource<IRgbDevice>(TaskCreationOptions.RunContinuationsAsynchronously);
        provider.DeviceDiscovered += (s, e) => discoveredTcs.TrySetResult(e.Device);

        // Simulate OpenRGB finishing detection of HyperX Alloy Origins Core (87 LEDs)
        client.ControllerCount = 1;

        // Background loop should poll and discover the device
        var discoveredDevice = await Task.WhenAny(discoveredTcs.Task, Task.Delay(2000));
        Assert.Same(discoveredTcs.Task, discoveredDevice);

        var device = await discoveredTcs.Task;
        Assert.NotNull(device);
        Assert.Equal("openrgb-0-test-keyboard", device.Id);
        Assert.Single(provider.Devices);
        Assert.Contains("1 device(s)", provider.ConnectionStatusMessage);
    }

    [Fact]
    public async Task OpenRgbDevice_InFlightFrameDropping_DropsStaleFramesDuringSlowTransmission()
    {
        var delayTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new SlowFakeOpenRgbClient(delayTcs.Task);

        var device = new OpenRgbDevice(
            client,
            0,
            "openrgb-0-hyperx",
            "HyperX Alloy Origins Core",
            RgbDeviceType.Keyboard,
            new LedCoordinate[] { new(0, 0.5f, 0.5f, "LED1") });

        // First frame: initiates transmission and awaits slow I/O
        var frame1 = new RgbColor[] { RgbColor.Red };
        var task1 = device.UpdateLedsAsync(frame1);

        // Wait a few milliseconds to ensure frame 1 has acquired the in-flight lock
        await Task.Delay(20);

        // Second frame: arrives while frame 1 is still in-flight
        var frame2 = new RgbColor[] { RgbColor.Blue };
        var task2 = device.UpdateLedsAsync(frame2);
        await task2; // Should return immediately because frame 2 is dropped as stale

        Assert.Equal(1, client.SendAttempts);

        // Now complete the slow transmission of frame 1
        delayTcs.SetResult();
        await task1;

        Assert.Equal(1, device.TransmissionCount);

        // Third frame: should now successfully transmit
        var frame3 = new RgbColor[] { RgbColor.Green };
        await device.UpdateLedsAsync(frame3);

        Assert.Equal(2, device.TransmissionCount);
        Assert.Equal(2, client.SendAttempts);
    }

    private sealed class SlowFakeOpenRgbClient : IOpenRgbClient
    {
        private readonly Task _delayTask;
        public int SendAttempts { get; private set; }
        public bool IsConnected => true;
        public uint ServerProtocolVersion => 4;

        public SlowFakeOpenRgbClient(Task delayTask)
        {
            _delayTask = delayTask;
        }

        public Task<bool> ConnectAsync(string host, int port, CancellationToken ct = default) => Task.FromResult(true);
        public Task<uint> GetControllerCountAsync(CancellationToken ct = default) => Task.FromResult(1u);
        public Task<byte[]> GetControllerDataRawAsync(uint controllerIndex, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
        public async Task SendUpdateLedsAsync(uint controllerIndex, ReadOnlyMemory<uint> bgrColors, CancellationToken ct = default)
        {
            SendAttempts++;
            await _delayTask;
        }
        public Task RequestRescanAsync(CancellationToken ct = default) => Task.CompletedTask;
        public void Disconnect() { }
        public void Dispose() { }
    }
}
