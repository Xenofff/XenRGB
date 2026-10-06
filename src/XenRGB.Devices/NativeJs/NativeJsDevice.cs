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
    public string ProviderId => "native-js";
    public string Id { get; }
    public string Name { get; }
    public RgbDeviceType DeviceType { get; }
    public IReadOnlyList<LedCoordinate> LedCoordinates { get; }

    public DeviceConnectionState ConnectionState { get; set; } = DeviceConnectionState.Connected;
    public bool IsEnabled { get; set; } = true;
    public float Brightness { get; set; } = 1.0f;
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
        Id = $"js-{hidDevice.Attributes.VendorId:X4}-{hidDevice.Attributes.ProductId:X4}-{hidDevice.DevicePath}";
        Name = name;
        DeviceType = type;

        var coords = new List<LedCoordinate>();
        for (int i = 0; i < ledCount; i++)
        {
            coords.Add(new LedCoordinate(i, (float)i / Math.Max(1, ledCount - 1), 0.5f, $"LED {i + 1}"));
        }
        LedCoordinates = coords;
        LedCount = ledCount;
        CurrentColors = new RgbColor[ledCount];
        _rgbBuffer = new byte[ledCount * 3];

        Throttler = new DeviceFrameThrottler(TimeSpan.FromMilliseconds(1000.0 / 30.0));

        _context = new DeviceContext(hidDevice, msg => Debug.WriteLine($"[JS {Name}] {msg}"));
        
        _engine = new Engine(cfg => cfg.AllowClr());
        _engine.SetValue("device", _context);
        _engine.SetValue("LightingMode", "");
        _engine.SetValue("forcedColor", "");
        _engine.Execute("var systeminfo = { GetBiosInfo: function() { return { version: '1.0' }; }, GetMotherboardInfo: function() { return { model: 'Generic Motherboard', manufacturer: 'Generic' }; }, GetMotherboardName: function() { return 'Generic Motherboard'; } };");
        _engine.Execute("var Assert = { fail: function() {}, softIsDefined: function() {} };");
        _engine.Execute("var permissions = { setCallback: function() {}, permissions: function() { return { 'lighting': true }; } };");
        _engine.Execute("var console = { log: function() {}, warn: function() {}, error: function() {}, info: function() {} };");

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

    public void Dispose()
    {
        _hidDevice?.CloseDevice();
    }
}
