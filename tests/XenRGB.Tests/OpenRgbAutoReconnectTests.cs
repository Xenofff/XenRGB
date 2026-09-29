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
            WriteString(writer, "Test Vendor");
            WriteString(writer, "Test Description");
            WriteString(writer, "1.0");
            WriteString(writer, "12345");
            WriteString(writer, "USB");

            writer.Write((ushort)0); // mode count
            writer.Write((int)0); // active mode

            writer.Write((ushort)0); // zone count
            writer.Write((ushort)1); // led count
            WriteString(writer, "Key 1");
            writer.Write((uint)0);

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
}
