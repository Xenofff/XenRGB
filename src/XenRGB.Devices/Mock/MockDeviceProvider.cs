using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.Core.Devices;

namespace XenRGB.Devices.Mock;

/// <summary>
/// Provides realistic mock RGB hardware (Keyboard, Desk LED Strip, Motherboard).
/// </summary>
public sealed class MockDeviceProvider : IDeviceProvider
{
    private readonly List<IRgbDevice> _devices = new();

    public string ProviderId => "mock-provider";
    public string DisplayName => "Virtual Hardware Simulator";
    public bool IsConnected { get; private set; } = true;

    public event EventHandler<DeviceEventArgs>? DeviceDiscovered;
    public event EventHandler<DeviceEventArgs>? DeviceRemoved;

    public MockDeviceProvider()
    {
        _devices.Add(CreateKeyboard());
        _devices.Add(CreateDeskStrip());
        _devices.Add(CreateMotherboard());
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        IsConnected = true;
        foreach (var device in _devices)
        {
            DeviceDiscovered?.Invoke(this, new DeviceEventArgs(device));
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IRgbDevice>> DiscoverDevicesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<IRgbDevice>>(_devices.AsReadOnly());
    }

    private static MockRgbDevice CreateKeyboard()
    {
        var leds = new List<LedCoordinate>();
        int index = 0;

        // 6 rows x 15 columns grid layout
        int rows = 6;
        int cols = 15;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float x = (c + 0.5f) / cols;
                float y = (r + 0.5f) / rows;
                leds.Add(new LedCoordinate(index++, x, y, $"Key_R{r}_C{c}"));
            }
        }

        return new MockRgbDevice("mock-keyboard", "Virtual RGB Keyboard", RgbDeviceType.Keyboard, leds);
    }

    private static MockRgbDevice CreateDeskStrip()
    {
        var leds = new List<LedCoordinate>();
        int count = 60;
        for (int i = 0; i < count; i++)
        {
            float x = (i + 0.5f) / count;
            float y = 0.5f;
            leds.Add(new LedCoordinate(i, x, y, $"Strip_LED_{i + 1}"));
        }

        return new MockRgbDevice("mock-strip", "Virtual Desk LED Strip", RgbDeviceType.LedStrip, leds);
    }

    private static MockRgbDevice CreateMotherboard()
    {
        var leds = new List<LedCoordinate>();
        // IO Shield (left column)
        for (int i = 0; i < 4; i++)
        {
            leds.Add(new LedCoordinate(i, 0.15f, 0.2f + i * 0.15f, $"IO_Shield_{i + 1}"));
        }
        // Chipset Heatsink (bottom right)
        for (int i = 0; i < 4; i++)
        {
            leds.Add(new LedCoordinate(4 + i, 0.7f + (i % 2) * 0.15f, 0.7f + (i / 2) * 0.15f, $"Chipset_{i + 1}"));
        }
        // Audio line (bottom left)
        for (int i = 0; i < 4; i++)
        {
            leds.Add(new LedCoordinate(8 + i, 0.15f, 0.75f + i * 0.06f, $"Audio_{i + 1}"));
        }

        return new MockRgbDevice("mock-motherboard", "Virtual Motherboard RGB", RgbDeviceType.Motherboard, leds);
    }

    public void Dispose()
    {
        foreach (var device in _devices)
        {
            DeviceRemoved?.Invoke(this, new DeviceEventArgs(device));
            device.Dispose();
        }
        _devices.Clear();
    }
}
