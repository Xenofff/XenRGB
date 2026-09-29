using System;
using System.Threading.Tasks;
using XenRGB.Core.Devices;
using XenRGB.Devices.Mock;
using XenRGB.SDK;
using Xunit;

namespace XenRGB.Tests;

public class DeviceProviderTests
{
    [Fact]
    public async Task MockDeviceProvider_DiscoversBuiltInSimulatedHardware()
    {
        using var provider = new MockDeviceProvider();
        await provider.InitializeAsync();
        var devices = await provider.DiscoverDevicesAsync();

        Assert.Equal(3, devices.Count);
        Assert.Contains(devices, d => d.DeviceType == RgbDeviceType.Keyboard);
        Assert.Contains(devices, d => d.DeviceType == RgbDeviceType.LedStrip);
        Assert.Contains(devices, d => d.DeviceType == RgbDeviceType.Motherboard);
    }

    [Fact]
    public async Task MockRgbDevice_UpdateLeds_StoresColorsInMemory()
    {
        using var provider = new MockDeviceProvider();
        var devices = await provider.DiscoverDevicesAsync();
        var keyboard = devices[0];

        var testColors = new RgbColor[]
        {
            new(255, 0, 0),
            new(0, 255, 0),
            new(0, 0, 255)
        };

        await keyboard.UpdateLedsAsync(testColors);

        Assert.Equal(255, keyboard.CurrentColors[0].R);
        Assert.Equal(255, keyboard.CurrentColors[1].G);
        Assert.Equal(255, keyboard.CurrentColors[2].B);
    }

    [Fact]
    public async Task MockRgbDevice_SetPowerOff_ClearsColorsToBlack()
    {
        using var provider = new MockDeviceProvider();
        var devices = await provider.DiscoverDevicesAsync();
        var keyboard = devices[0];

        await keyboard.UpdateLedsAsync(new RgbColor[] { new(255, 255, 255) });
        await keyboard.SetPowerAsync(false);

        Assert.Equal(RgbColor.Black, keyboard.CurrentColors[0]);
    }

    [Fact]
    public async Task OpenRgbProvider_UnreachableHost_FailsGracefullyWithoutThrowing()
    {
        // Port 59999 should not have OpenRGB running
        using var provider = new XenRGB.Devices.OpenRgb.OpenRgbProvider("127.0.0.1", 59999);
        
        // DiscoverDevicesAsync should handle socket exception gracefully and return empty list
        var devices = await provider.DiscoverDevicesAsync();

        Assert.Empty(devices);
        Assert.False(provider.IsConnected);
    }

    [Fact]
    public void MockRgbDevice_ToggleEnable_PreservesState()
    {
        var leds = new LedCoordinate[] { new(0, 0.5f, 0.5f, "LED1") };
        var device = new MockRgbDevice("test-dev", "Test Device", RgbDeviceType.LedStrip, leds);

        Assert.True(device.IsEnabled);
        device.IsEnabled = false;
        Assert.False(device.IsEnabled);
        device.IsEnabled = true;
        Assert.True(device.IsEnabled);
    }
}
