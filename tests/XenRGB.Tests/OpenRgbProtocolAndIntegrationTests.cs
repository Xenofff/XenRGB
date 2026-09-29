using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using XenRGB.Core.Canvas;
using XenRGB.Core.Devices;
using XenRGB.Core.Engine;
using XenRGB.Devices.OpenRgb;
using XenRGB.Effects.BuiltIn;
using XenRGB.SDK;

namespace XenRGB.Tests;

public class OpenRgbProtocolAndIntegrationTests
{
    private sealed class ProtocolMockClient : IOpenRgbClient
    {
        public bool IsConnected { get; set; } = true;
        public uint ServerProtocolVersion { get; set; } = 4;
        public bool RescanCalled { get; private set; }
        public uint ControllerCount { get; set; } = 1;
        public byte[] ControllerData { get; set; } = Array.Empty<byte>();

        public Task<bool> ConnectAsync(string host, int port, CancellationToken ct = default)
        {
            IsConnected = true;
            return Task.FromResult(true);
        }

        public Task<uint> GetControllerCountAsync(CancellationToken ct = default)
        {
            return Task.FromResult(ControllerCount);
        }

        public Task<byte[]> GetControllerDataRawAsync(uint controllerIndex, CancellationToken ct = default)
        {
            return Task.FromResult(ControllerData);
        }

        public Task SendUpdateLedsAsync(uint controllerIndex, ReadOnlyMemory<uint> bgrColors, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task RequestRescanAsync(CancellationToken ct = default)
        {
            RescanCalled = true;
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
    public void OpenRgbClient_Constants_MatchOfficialProtocolSpecification()
    {
        // Must match OpenRGB NetworkProtocol.h exactly
        Assert.Equal(0u, OpenRgbClient.CommandRequestControllerCount);
        Assert.Equal(1u, OpenRgbClient.CommandRequestControllerData);
        Assert.Equal(40u, OpenRgbClient.CommandRequestProtocolVersion);
        Assert.Equal(50u, OpenRgbClient.CommandSetClientName);
        Assert.Equal(140u, OpenRgbClient.CommandRequestRescanDevices);
        Assert.Equal(1050u, OpenRgbClient.CommandUpdateLeds);
    }

    [Fact]
    public async Task LiveOpenRgbServer_IfRunning_DiscoversRealDevices()
    {
        using var provider = new OpenRgbProvider("127.0.0.1", 6742);
        bool connected = await provider.TryConnectAndDiscoverAsync();
        if (!connected) return; // Skip if OpenRGB server not running

        Assert.True(provider.IsConnected);
        var devices = provider.Devices;
        Console.WriteLine($"[LIVE TEST] Discovered {devices.Count} OpenRGB device(s):");
        foreach (var dev in devices)
        {
            Console.WriteLine($"[LIVE TEST] -> Device: {dev.Name}, Type: {dev.DeviceType}, LEDs: {dev.LedCount}");
        }
    }

    [Fact]
    public async Task LiveOpenRgbServer_IfRunning_CanSendUpdateLeds()
    {
        using var provider = new OpenRgbProvider("127.0.0.1", 6742);
        bool connected = await provider.TryConnectAndDiscoverAsync();
        if (!connected) return;

        var devices = provider.Devices;
        if (devices.Count == 0) return;

        var dev = devices[0] as OpenRgbDevice;
        Assert.NotNull(dev);
        var colors = new XenRGB.SDK.RgbColor[dev.LedCount];
        Array.Fill(colors, new XenRGB.SDK.RgbColor(0, 255, 0)); // Green

        await dev.UpdateLedsAsync(colors);
        Assert.Equal(DeviceConnectionState.Connected, dev.ConnectionState);
        Assert.True(dev.TransmissionCount > 0);
        Console.WriteLine($"[LIVE TEST] Successfully sent {dev.LedCount} green LEDs to {dev.Name}!");
    }

    [Fact]
    public async Task OpenRgbProvider_WhenDisconnected_RecordsClearErrorMessage()
    {
        var client = new ProtocolMockClient();
        using var provider = new OpenRgbProvider(client, "127.0.0.1", 6742);

        // Disconnect client
        client.Disconnect();

        bool ok = await provider.TryConnectAndDiscoverAsync();
        // Since connect succeeds in mock, let client fail connect
        var failingClient = new FailingConnectMockClient();
        using var failingProvider = new OpenRgbProvider(failingClient, "127.0.0.1", 6742);

        bool failResult = await failingProvider.TryConnectAndDiscoverAsync();
        Assert.False(failResult);
        Assert.NotNull(failingProvider.LastError);
        Assert.Contains("Cannot connect to OpenRGB server", failingProvider.LastError);
        Assert.Contains("6742", failingProvider.ConnectionStatusMessage);
    }

    [Fact]
    public async Task OpenRgbProvider_RescanAndDiscover_CallsRequestRescanOnClient()
    {
        var client = new ProtocolMockClient();
        using var provider = new OpenRgbProvider(client, "127.0.0.1", 6742);

        client.ControllerData = CreateValidControllerData(protocolVersion: 4, name: "Keyboard", ledsCount: 10);
        await provider.InitializeAsync();

        var devices = await provider.RescanAndDiscoverAsync();
        Assert.True(client.RescanCalled);
        Assert.Single(devices);
        Assert.Equal("HyperX Keyboard", devices[0].Name);
    }

    [Fact]
    public async Task OpenRgbProvider_ParsesProtocolV3ModesAndProtocolV4SegmentsCorrectly()
    {
        var client = new ProtocolMockClient { ServerProtocolVersion = 4 };
        using var provider = new OpenRgbProvider(client, "127.0.0.1", 6742);

        client.ControllerData = CreateValidControllerData(protocolVersion: 4, name: "RGB Gaming Keyboard", ledsCount: 87);

        var devices = await provider.DiscoverDevicesAsync();
        Assert.Single(devices);
        var dev = devices[0];
        Assert.Equal(87, dev.LedCount);
        Assert.Equal(RgbDeviceType.Keyboard, dev.DeviceType);
        Assert.Equal("HyperX RGB Gaming Keyboard", dev.Name);
    }

    [Fact]
    public async Task OpenRgbProvider_WithRgbEngineAndCanvas_DiscoversAndRendersColorToDevice()
    {
        var client = new ProtocolMockClient { ServerProtocolVersion = 4 };
        client.ControllerData = CreateValidControllerData(protocolVersion: 4, name: "RGB Gaming Keyboard", ledsCount: 87);

        using var provider = new OpenRgbProvider(client, "127.0.0.1", 6742);
        var canvas = new SpatialCanvas();
        using var engine = new RgbEngine(canvas);

        var devices = await provider.DiscoverDevicesAsync();
        Assert.Single(devices);
        var dev = devices[0];

        // Register to canvas and engine
        canvas.AddOrUpdatePlacement(new CanvasDevicePlacement(dev.Id, dev.Name, 0.5f, 0.5f, 0.5f, 0.5f));
        engine.RegisterDevice(dev);

        // Apply Plain Color effect
        var effect = new PlainColorEffect();
        effect.Initialize();
        engine.ActiveEffect = effect;

        engine.Start();
        await Task.Delay(200);

        Assert.Equal(87, dev.CurrentColors.Length);
        Assert.NotEqual(RgbColor.Black, dev.CurrentColors[0]);
    }

    private static byte[] CreateValidControllerData(uint protocolVersion, string name, int ledsCount)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write((uint)0); // total size
        writer.Write((int)5);  // rawType = 5 (Keyboard)
        WriteString(writer, name);
        if (protocolVersion >= 1)
        {
            WriteString(writer, "HyperX");
        }
        WriteString(writer, "Gaming Keyboard Description");
        WriteString(writer, "v1.0.0");
        WriteString(writer, "SN-987654");
        WriteString(writer, "HID_DEVICE");

        // Modes count: 1
        writer.Write((ushort)1);
        writer.Write((int)0); // active mode
        // Mode 0:
        WriteString(writer, "Direct");
        if (protocolVersion < 6) writer.Write((int)0); // mode_value (removed in v6)
        writer.Write((uint)0); // mode_flags
        writer.Write((uint)0); // speed_min
        writer.Write((uint)0); // speed_max
        if (protocolVersion >= 3)
        {
            writer.Write((uint)0);   // brightness_min
            writer.Write((uint)100); // brightness_max
        }
        writer.Write((uint)0); // colors_min
        writer.Write((uint)0); // colors_max
        writer.Write((uint)0); // speed
        if (protocolVersion >= 3)
        {
            writer.Write((uint)100); // brightness
        }
        writer.Write((uint)0);   // direction
        writer.Write((uint)0);   // color_mode
        writer.Write((ushort)0); // num_colors in mode = 0

        // Zones count: 1
        writer.Write((ushort)1);
        WriteString(writer, "Keys Zone");
        writer.Write((int)2);           // zone type = matrix
        writer.Write((uint)ledsCount);  // leds_min
        writer.Write((uint)ledsCount);  // leds_max
        writer.Write((uint)ledsCount);  // leds_count
        writer.Write((ushort)0);        // matrixLen = 0 (no matrix map)

        if (protocolVersion >= 4)
        {
            writer.Write((ushort)0); // num_segments = 0
        }
        if (protocolVersion >= 5)
        {
            writer.Write((uint)0); // zone_flags
        }
        // Protocol v6: per-zone mode data
        if (protocolVersion >= 6)
        {
            writer.Write((int)0);    // zone_active_mode
            writer.Write((ushort)0); // zone_num_modes = 0
            WriteString(writer, "Keys Zone Display Name"); // zone_display_name
        }

        // LEDs
        writer.Write((ushort)ledsCount);
        for (int i = 0; i < ledsCount; i++)
        {
            WriteString(writer, $"Key {i + 1}");
            if (protocolVersion < 6)
            {
                writer.Write((uint)i); // led_value (removed in v6)
            }
        }

        // Device-level colors block (always present per spec, even for protocol v0)
        writer.Write((ushort)0); // num_colors = 0

        // Protocol v5: LED alternate display names + controller flags
        if (protocolVersion >= 5)
        {
            writer.Write((ushort)0); // num_led_display_names = 0
            writer.Write((uint)0);   // controller flags
        }

        // Protocol v6: display_name + configuration
        if (protocolVersion >= 6)
        {
            WriteString(writer, "HyperX Alloy Origins Core"); // display_name
            writer.Write((uint)0); // configuration_len = 0 (empty JSON config)
        }

        return ms.ToArray();
    }
    private static void WriteString(BinaryWriter writer, string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str + "\0");
        writer.Write((ushort)bytes.Length);
        writer.Write(bytes);
    }

    private sealed class FailingConnectMockClient : IOpenRgbClient
    {
        public bool IsConnected => false;
        public uint ServerProtocolVersion => 4;
        public Task<bool> ConnectAsync(string host, int port, CancellationToken ct = default) => Task.FromResult(false);
        public Task<uint> GetControllerCountAsync(CancellationToken ct = default) => Task.FromResult(0u);
        public Task<byte[]> GetControllerDataRawAsync(uint controllerIndex, CancellationToken ct = default) => Task.FromResult(Array.Empty<byte>());
        public Task SendUpdateLedsAsync(uint controllerIndex, ReadOnlyMemory<uint> bgrColors, CancellationToken ct = default) => Task.CompletedTask;
        public void Disconnect() { }
        public void Dispose() { }
    }
}
