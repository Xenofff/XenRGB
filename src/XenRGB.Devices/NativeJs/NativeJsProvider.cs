using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HidLibrary;
using Jint;
using XenRGB.Core.Devices;

namespace XenRGB.Devices.NativeJs;

public class NativeJsProvider : IDeviceProvider
{
    public string ProviderId => "native-js";
    public string DisplayName => "Native JS Plugins (SignalRGB)";
    public bool IsConnected { get; private set; }

    public event EventHandler<DeviceEventArgs>? DeviceDiscovered;
    public event EventHandler<DeviceEventArgs>? DeviceRemoved;

    private readonly List<IRgbDevice> _devices = new();
    private readonly string _pluginsPath;

    public NativeJsProvider()
    {
        _pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NativeJs", "Plugins");
    }

    public void Dispose()
    {
        _devices.Clear();
    }

    public Task InitializeAsync(CancellationToken ct = default)
    {
        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IRgbDevice>> DiscoverDevicesAsync(CancellationToken ct = default)
    {
        var discovered = new List<IRgbDevice>();

        if (!Directory.Exists(_pluginsPath))
        {
            Debug.WriteLine($"[JS] Plugin directory not found: {_pluginsPath}");
            return Task.FromResult<IReadOnlyList<IRgbDevice>>(_devices);
        }

        // Only search for MSI and HyperX for now as requested
        var jsFiles = Directory.GetFiles(_pluginsPath, "*.js", SearchOption.AllDirectories)
            .Where(f => f.Contains("Msi", StringComparison.OrdinalIgnoreCase) || f.Contains("HyperX", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var hidDevices = HidDevices.Enumerate().ToList();

        foreach (var file in jsFiles)
        {
            string code = File.ReadAllText(file);
            
            // Jint doesn't support 'export' keyword out of the box for normal scripts, strip it
            code = code.Replace("export function ", "function ");
            code = code.Replace("export const ", "const ");
            code = code.Replace("export var ", "var ");
            code = code.Replace("export let ", "let ");
            code = System.Text.RegularExpressions.Regex.Replace(code, @"^import\s+.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);

            try
            {
                var engine = new Engine();
                
                // Inject SignalRGB global variables
                engine.SetValue("LightingMode", "");
                engine.SetValue("forcedColor", "");
                engine.Execute("var systeminfo = { GetBiosInfo: function() { return { version: '1.0' }; }, GetMotherboardInfo: function() { return { model: 'Generic Motherboard', manufacturer: 'Generic' }; }, GetMotherboardName: function() { return 'Generic Motherboard'; } };");
                engine.Execute("var Assert = { fail: function() {}, softIsDefined: function() {} };");
                engine.Execute("var permissions = { setCallback: function() {}, permissions: function() { return { 'lighting': true }; } };");
                engine.Execute("var console = { log: function() {}, warn: function() {}, error: function() {}, info: function() {} };");
                
                engine.Execute(code);

                var vendorIdVal = engine.GetValue("VendorId");
                var productIdVal = engine.GetValue("ProductId");
                var nameVal = engine.GetValue("Name");

                if (vendorIdVal.IsUndefined() || productIdVal.IsUndefined())
                    continue; // Not a valid device plugin

                int vid = 0;
                try { vid = (int)vendorIdVal.AsNumber(); } catch { try { vid = (int)engine.Invoke("VendorId").AsNumber(); } catch { } }

                var pids = new List<int>();
                try { pids.Add((int)productIdVal.AsNumber()); } catch { 
                    try { 
                        var res = engine.Invoke("ProductId");
                        if (res.IsNumber()) pids.Add((int)res.AsNumber());
                        else if (res.IsArray())
                        {
                            var arr = res.AsArray();
                            for (uint i = 0; i < arr.Length; i++)
                            {
                                var val = arr.Get(i.ToString());
                                if (val.IsNumber()) pids.Add((int)val.AsNumber());
                                else if (val.IsString())
                                {
                                    string s = val.AsString();
                                    if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                                        pids.Add(Convert.ToInt32(s, 16));
                                    else if (int.TryParse(s, out int parsed))
                                        pids.Add(parsed);
                                }
                            }
                        }
                    } catch { }
                }

                string devName = "Unknown JS Device";
                try { devName = nameVal.AsString(); } catch { try { devName = engine.Invoke("Name").AsString(); } catch { } }
                
                System.Diagnostics.Debug.WriteLine($"[JS Provider] Parsed {Path.GetFileName(file)}: VID={vid:X4}, PIDs=[{string.Join(",", pids.Select(p => p.ToString("X4")))}], Name={devName}");

                if (vid == 0 || pids.Count == 0) continue;

                foreach (var pid in pids)
                {
                    // Find matching HID device
                    var match = hidDevices.FirstOrDefault(h => h.Attributes.VendorId == vid && h.Attributes.ProductId == pid);
                    if (match != null)
                    {
                        // Check if we already added it
                        if (_devices.Any(d => d.Id.Contains($"{vid:X4}-{pid:X4}")))
                            continue;

                        // Retrieve LedNames to get the LED count
                        int ledCount = 1;
                        try {
                            var namesRes = engine.Invoke("LedNames");
                            if (namesRes.IsArray()) ledCount = (int)namesRes.AsArray().Length;
                        } catch { }

                        // Also check vLedNames array directly if it exists
                        if (ledCount == 1)
                        {
                            try {
                                var vLedNames = engine.GetValue("vLedNames");
                                if (vLedNames.IsArray()) ledCount = (int)vLedNames.AsArray().Length;
                            } catch { }
                        }

                        if (ledCount <= 0) ledCount = 1;

                        RgbDeviceType type = RgbDeviceType.Motherboard;
                        if (devName.Contains("Keyboard", StringComparison.OrdinalIgnoreCase)) type = RgbDeviceType.Keyboard;

                        match.OpenDevice();
                        
                        var device = new NativeJsDevice(match, code, devName, type, ledCount);
                        discovered.Add(device);
                        _devices.Add(device);

                        DeviceDiscovered?.Invoke(this, new DeviceEventArgs(device));
                        Debug.WriteLine($"[JS Provider] Discovered {devName} on {match.DevicePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JS Provider] Failed to parse plugin {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return Task.FromResult<IReadOnlyList<IRgbDevice>>(_devices);
    }
}
