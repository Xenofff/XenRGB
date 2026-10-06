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
        foreach (var dev in _devices)
        {
            DeviceRemoved?.Invoke(this, new DeviceEventArgs(dev));
            dev.Dispose();
        }
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
                var engine = new Engine(cfg => cfg.AllowClr());
                
                // Inject SignalRGB global variables
                engine.SetValue("LightingMode", "");
                engine.SetValue("forcedColor", "");
                engine.SetValue("advancedMode", false);
                engine.SetValue("shutdownColor", "#000000");
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

                if (vid == 0 || pids.Count == 0) continue;

                foreach (var pid in pids)
                {
                    // Find all matching HID devices with this VID & PID
                    var candidates = hidDevices.Where(h => h.Attributes.VendorId == vid && h.Attributes.ProductId == pid).ToList();
                    if (candidates.Count == 0) continue;

                    // Check if already added
                    if (_devices.Any(d => d.Id.Contains($"{vid:X4}-{pid:X4}")))
                        continue;

                    IHidDevice? match = null;
                    var validateVal = engine.GetValue("Validate");
                    if (!validateVal.IsUndefined())
                    {
                        foreach (var cand in candidates)
                        {
                            int iface = 0;
                            var miMatch = System.Text.RegularExpressions.Regex.Match(cand.DevicePath, @"mi_([0-9a-fA-F]{2})");
                            if (miMatch.Success)
                            {
                                iface = Convert.ToInt32(miMatch.Groups[1].Value, 16);
                            }

                            try
                            {
                                var res = engine.Invoke("Validate", new { @interface = iface });
                                if (res.IsBoolean() && res.AsBoolean())
                                {
                                    match = cand;
                                    break;
                                }
                            }
                            catch { }
                        }
                    }

                    if (match == null)
                    {
                        match = candidates.FirstOrDefault();
                    }

                    if (match != null)
                    {
                        string jsDevType = "";
                        try { jsDevType = engine.Invoke("DeviceType").AsString(); }
                        catch { try { jsDevType = engine.GetValue("DeviceType").AsString(); } catch { } }

                        RgbDeviceType type = jsDevType.ToLowerInvariant() switch
                        {
                            "keyboard" => RgbDeviceType.Keyboard,
                            "mouse" => RgbDeviceType.Mouse,
                            "mousepad" => RgbDeviceType.LedStrip,
                            "motherboard" => RgbDeviceType.Motherboard,
                            "gpu" => RgbDeviceType.Motherboard,
                            "cooler" => RgbDeviceType.LedStrip,
                            "lightbar" => RgbDeviceType.Lightbar,
                            _ => (devName.Contains("Keyboard", StringComparison.OrdinalIgnoreCase) ||
                                  devName.Contains("Alloy", StringComparison.OrdinalIgnoreCase) ||
                                  devName.Contains("Origins", StringComparison.OrdinalIgnoreCase))
                                 ? RgbDeviceType.Keyboard
                                 : RgbDeviceType.Motherboard
                        };

                        match.OpenDevice();
                        
                        var device = new NativeJsDevice(match, code, devName, type);
                        discovered.Add(device);
                        _devices.Add(device);

                        DeviceDiscovered?.Invoke(this, new DeviceEventArgs(device));
                        Debug.WriteLine($"[JS Provider] Discovered {devName} (LEDs: {device.LedCount}) on {match.DevicePath}");
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
