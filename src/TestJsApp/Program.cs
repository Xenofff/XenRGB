using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HidLibrary;
using Jint;
using Jint.Native;
using XenRGB.Devices.NativeJs;

class Program
{
    static void Main()
    {
        var devices = HidDevices.Enumerate().ToList();
        var hxDev = devices.FirstOrDefault(d => d.Attributes.VendorId == 0x0951 && d.Attributes.ProductId == 0x16E6 && d.DevicePath.Contains("mi_02"));
        if (hxDev == null)
        {
            hxDev = devices.FirstOrDefault(d => d.Attributes.VendorId == 0x0951 && d.Attributes.ProductId == 0x16E6);
        }

        if (hxDev == null)
        {
            Console.WriteLine("HyperX keyboard not found!");
            return;
        }

        Console.WriteLine($"Found HyperX device: {hxDev.DevicePath}");
        hxDev.OpenDevice();

        var engine = new Engine(cfg => cfg.AllowClr());
        var context = new DeviceContext(hxDev, s => Console.WriteLine($"[LOG] {s}"));
        context.Engine = engine;

        engine.SetValue("device", context);
        engine.SetValue("LightingMode", "");
        engine.SetValue("forcedColor", "");
        engine.SetValue("advancedMode", false);
        engine.SetValue("shutdownColor", "#000000");
        engine.Execute("var systeminfo = { GetBiosInfo: function() { return { version: '1.0' }; }, GetMotherboardInfo: function() { return { model: 'Generic Motherboard', manufacturer: 'Generic' }; }, GetMotherboardName: function() { return 'Generic Motherboard'; } };");
        engine.Execute("var Assert = { fail: function() {}, softIsDefined: function() {} };");
        engine.Execute("var permissions = { setCallback: function() {}, permissions: function() { return { 'lighting': true }; } };");
        engine.Execute("var console = { log: function() {}, warn: function() {}, error: function() {}, info: function() {} };");

        string path = @"e:\Projects\XenRGB\src\XenRGB.Devices\NativeJs\Plugins\HyperX\HyperX_Alloy_Origins_Core.js";
        string scriptContent = File.ReadAllText(path);
        scriptContent = scriptContent.Replace("export function ", "function ");
        scriptContent = scriptContent.Replace("export const ", "const ");
        scriptContent = scriptContent.Replace("export var ", "var ");
        scriptContent = scriptContent.Replace("export let ", "let ");
        scriptContent = System.Text.RegularExpressions.Regex.Replace(scriptContent, @"^import\s+.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);

        engine.Execute(scriptContent);

        engine.Invoke("Initialize");
        Console.WriteLine("HyperX Init SUCCESS!");

        // Read layout
        var namesVal = engine.Invoke("LedNames").AsArray();
        var posVal = engine.Invoke("LedPositions").AsArray();
        Console.WriteLine($"HyperX vLedNames count: {namesVal.Length}, vLedPositions count: {posVal.Length}");

        var mappings = new List<(int x, int y, string name, int index)>();
        byte[] testColors = new byte[(int)namesVal.Length * 3];

        for (uint i = 0; i < namesVal.Length; i++)
        {
            string name = namesVal.Get(i.ToString()).AsString();
            var pt = posVal.Get(i.ToString()).AsArray();
            int x = (int)pt.Get("0").AsNumber();
            int y = (int)pt.Get("1").AsNumber();
            mappings.Add((x, y, name, (int)i));

            // Set different colors for different keys (e.g. gradient across keys)
            testColors[i * 3 + 0] = (byte)((i * 3) % 256); // R
            testColors[i * 3 + 1] = (byte)((i * 7) % 256); // G
            testColors[i * 3 + 2] = (byte)((i * 11) % 256); // B
        }

        context.SetLedMapping(mappings);
        context.UpdateColors(testColors);

        engine.Invoke("Render");
        Console.WriteLine("HyperX Render with per-key mapping SUCCESS!");
    }
}
