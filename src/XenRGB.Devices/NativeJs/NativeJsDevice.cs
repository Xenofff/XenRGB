using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidLibrary;
using Jint;
using XenRGB.Core.Devices;
using XenRGB.SDK;

namespace XenRGB.Devices.NativeJs;

public class NativeJsDevice : IRgbDevice
{
    public string Id { get; }
    public string Name { get; }
    public RgbDeviceType Type { get; }
    public IReadOnlyList<LedCoordinate> Leds { get; }

    public DeviceConnectionState ConnectionState { get; set; } = DeviceConnectionState.Connected;
    public bool IsEnabled { get; set; } = true;
    public RgbColor[] CurrentColors { get; }

    public DeviceFrameThrottler Throttler { get; }
    public long TransmissionCount { get; private set; }

    private readonly IHidDevice _hidDevice;
    private readonly Engine _engine;
    private readonly DeviceContext _context;
    private readonly byte[] _rgbBuffer;
    private readonly Action _renderFunc;

    public NativeJsDevice(IHidDevice hidDevice, string scriptContent, string name, RgbDeviceType type, int ledCount)
    {
        _hidDevice = hidDevice;
        Id = $"js-{hidDevice.VendorId:X4}-{hidDevice.ProductId:X4}-{hidDevice.DevicePath}";
        Name = name;
        Type = type;

        var coords = new List<LedCoordinate>();
        for (int i = 0; i < ledCount; i++)
        {
            coords.Add(new LedCoordinate(i, (float)i / Math.Max(1, ledCount - 1), 0.5f, $"LED {i + 1}"));
        }
        Leds = coords;
        LedCount = ledCount;
        CurrentColors = new RgbColor[ledCount];
        _rgbBuffer = new byte[ledCount * 3];

        Throttler = new DeviceFrameThrottler(TimeSpan.FromMilliseconds(1000.0 / 30.0));

        _context = new DeviceContext(hidDevice, msg => Debug.WriteLine($"[JS {Name}] {msg}"));
        
        _engine = new Engine(cfg => cfg.AllowClr());
        _engine.SetValue("device", _context);
        _engine.SetValue("LightingMode", "");
        _engine.SetValue("forcedColor", "");

        // Execute the plugin script
        _engine.Execute(scriptContent);

        // Try to bind Render function
        _renderFunc = () =>
        {
            try
            {
                _engine.Invoke("Render");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JS Error] {ex.Message}");
            }
        };

        // Call Initialize if it exists
        try
        {
            var init = _engine.GetValue("Initialize");
            if (!init.IsUndefined())
            {
                _engine.Invoke("Initialize");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[JS Init Error] {ex.Message}");
        }
    }

    public int LedCount { get; }

    public Task SetPowerAsync(bool isOn, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public Task UpdateLedsAsync(ReadOnlyMemory<RgbColor> colors, CancellationToken ct = default)
    {
        var span = colors.Span;
        int count = Math.Min(LedCount, span.Length);

        bool dirty = Throttler.ShouldTransmit(span);
        if (!dirty) return Task.CompletedTask;

        for (int i = 0; i < count; i++)
        {
            CurrentColors[i] = span[i];
            _rgbBuffer[i * 3 + 0] = span[i].R;
            _rgbBuffer[i * 3 + 1] = span[i].G;
            _rgbBuffer[i * 3 + 2] = span[i].B;
        }

        _context.UpdateColors(_rgbBuffer);

        // Run Render in background or wait? Wait is fine for now since it's just HID write
        _renderFunc();
        TransmissionCount++;

        return Task.CompletedTask;
    }
}
