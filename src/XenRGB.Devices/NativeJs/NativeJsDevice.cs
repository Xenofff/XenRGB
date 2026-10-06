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

    public NativeJsDevice(IHidDevice hidDevice, string scriptContent, string name, RgbDeviceType type)
    {
        _hidDevice = hidDevice;
        Id = $"js-{hidDevice.Attributes.VendorId:X4}-{hidDevice.Attributes.ProductId:X4}-{hidDevice.DevicePath}";
        Name = name;
        DeviceType = type;

        _context = new DeviceContext(hidDevice, msg => Debug.WriteLine($"[JS {Name}] {msg}"));
        _engine = new Engine(cfg => cfg.AllowClr());
        _context.Engine = _engine;

        _engine.SetValue("device", _context);
        _engine.SetValue("LightingMode", "");
        _engine.SetValue("forcedColor", "");
        _engine.SetValue("advancedMode", false);
        _engine.SetValue("shutdownColor", "#000000");
        _engine.Execute("var systeminfo = { GetBiosInfo: function() { return { version: '1.0' }; }, GetMotherboardInfo: function() { return { model: 'Generic Motherboard', manufacturer: 'Generic' }; }, GetMotherboardName: function() { return 'Generic Motherboard'; } };");
        _engine.Execute("var Assert = { fail: function() {}, softIsDefined: function() {} };");
        _engine.Execute("var permissions = { setCallback: function() {}, permissions: function() { return { 'lighting': true }; } };");
        _engine.Execute("var console = { log: function() {}, warn: function() {}, error: function() {}, info: function() {} };");

        // Execute the plugin script
        _engine.Execute(scriptContent);

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
            Debug.WriteLine($"[JS Init Error {Name}] {ex.Message}");
        }

        // Try to bind Render function
        _renderFunc = () =>
        {
            try
            {
                _engine.Invoke("Render");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JS Render Error {Name}] {ex.Message}");
            }
        };

        // Query LED Layout after script initialization
        var names = new List<string>();
        try
        {
            var res = _engine.Invoke("LedNames");
            if (res.IsArray())
            {
                var arr = res.AsArray();
                for (uint i = 0; i < arr.Length; i++)
                    names.Add(arr.Get(i.ToString()).AsString());
            }
        }
        catch
        {
            try
            {
                var vLedNames = _engine.GetValue("vLedNames");
                if (vLedNames.IsArray())
                {
                    var arr = vLedNames.AsArray();
                    for (uint i = 0; i < arr.Length; i++)
                        names.Add(arr.Get(i.ToString()).AsString());
                }
            }
            catch { }
        }

        var positions = new List<(int x, int y)>();
        try
        {
            var res = _engine.Invoke("LedPositions");
            if (res.IsArray())
            {
                var arr = res.AsArray();
                for (uint i = 0; i < arr.Length; i++)
                {
                    var pt = arr.Get(i.ToString()).AsArray();
                    int px = (int)pt.Get("0").AsNumber();
                    int py = (int)pt.Get("1").AsNumber();
                    positions.Add((px, py));
                }
            }
        }
        catch
        {
            try
            {
                var vLedPositions = _engine.GetValue("vLedPositions");
                if (vLedPositions.IsArray())
                {
                    var arr = vLedPositions.AsArray();
                    for (uint i = 0; i < arr.Length; i++)
                    {
                        var pt = arr.Get(i.ToString()).AsArray();
                        int px = (int)pt.Get("0").AsNumber();
                        int py = (int)pt.Get("1").AsNumber();
                        positions.Add((px, py));
                    }
                }
            }
            catch { }
        }

        int width = 1, height = 1;
        try
        {
            var szRes = _engine.Invoke("Size");
            if (szRes.IsArray())
            {
                var arr = szRes.AsArray();
                width = Math.Max(1, (int)arr.Get("0").AsNumber());
                height = Math.Max(1, (int)arr.Get("1").AsNumber());
            }
        }
        catch { }

        int totalLeds = Math.Max(names.Count, positions.Count);
        if (totalLeds == 0) totalLeds = 1;

        var coords = new List<LedCoordinate>();
        var mappings = new List<(int x, int y, string name, int index)>();

        if (DeviceType == RgbDeviceType.Motherboard)
        {
            // Onboard LEDs (e.g. 6 LEDs on Pro Z690-A)
            for (int i = 0; i < totalLeds; i++)
            {
                string ledName = i < names.Count ? names[i] : $"Onboard LED {i + 1}";
                int px = i < positions.Count ? positions[i].x : i;
                int py = i < positions.Count ? positions[i].y : 0;
                float normX = 0.1f + (float)i * 0.04f;
                float normY = 0.5f;

                coords.Add(new LedCoordinate(i, normX, normY, ledName));
                mappings.Add((px, py, ledName, i));
            }

            int currIdx = coords.Count;

            // ARGB Header 1 (e.g. Cooler / JRainbow1) - 60 LEDs
            for (int k = 0; k < 60; k++)
            {
                float normX = 0.35f + ((float)k / 60f) * 0.55f;
                float normY = 0.25f;
                coords.Add(new LedCoordinate(currIdx, normX, normY, $"ARGB 1 (Cooler) - LED {k + 1}"));
                currIdx++;
            }

            // ARGB Header 2 (e.g. Fans / JRainbow2) - 60 LEDs
            for (int k = 0; k < 60; k++)
            {
                float normX = 0.35f + ((float)k / 60f) * 0.55f;
                float normY = 0.75f;
                coords.Add(new LedCoordinate(currIdx, normX, normY, $"ARGB 2 (Fans) - LED {k + 1}"));
                currIdx++;
            }

            // 12V RGB Header
            coords.Add(new LedCoordinate(currIdx, 0.1f, 0.8f, "12V RGB Header"));
            currIdx++;

            totalLeds = currIdx;
        }
        else
        {
            // Direct matrix devices (Keyboards, etc.)
            for (int i = 0; i < totalLeds; i++)
            {
                string ledName = i < names.Count ? names[i] : $"LED {i + 1}";
                int px = i < positions.Count ? positions[i].x : i;
                int py = i < positions.Count ? positions[i].y : 0;

                float normX = width > 1 ? (float)px / (width - 1) : 0.5f;
                float normY = height > 1 ? (float)py / (height - 1) : 0.5f;

                coords.Add(new LedCoordinate(i, normX, normY, ledName));
                mappings.Add((px, py, ledName, i));
            }
        }

        LedCoordinates = coords;
        LedCount = totalLeds;
        CurrentColors = new RgbColor[totalLeds];
        _rgbBuffer = new byte[totalLeds * 3];

        _context.SetLedMapping(mappings);

        Throttler = new DeviceFrameThrottler(TimeSpan.FromMilliseconds(1000.0 / 30.0));
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

        _renderFunc();
        TransmissionCount++;

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _hidDevice?.CloseDevice();
    }
}
