using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        Trace.Listeners.Add(new ConsoleTraceListener());
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

        var device = new NativeJsDevice(hxDev, scriptContent, "HyperX Alloy Origins Core", XenRGB.Core.Devices.RgbDeviceType.Keyboard);
        Console.WriteLine($"Created NativeJsDevice: Name={device.Name}, Type={device.DeviceType}, LedCount={device.LedCount}");
        Console.WriteLine($"Sample coords: LED 0 (Esc)=({device.LedCoordinates[0].LocalX:F3}, {device.LedCoordinates[0].LocalY:F3})");
        Console.WriteLine($"Sample coords: LED 16 (`)=({device.LedCoordinates[16].LocalX:F3}, {device.LedCoordinates[16].LocalY:F3})");
        Console.WriteLine($"Sample coords: LED 33 (Tab)=({device.LedCoordinates[33].LocalX:F3}, {device.LedCoordinates[33].LocalY:F3})");
        Console.WriteLine($"Sample coords: LED 50 (Caps)=({device.LedCoordinates[50].LocalX:F3}, {device.LedCoordinates[50].LocalY:F3})");
        Console.WriteLine($"Sample coords: LED 64 (LShift)=({device.LedCoordinates[64].LocalX:F3}, {device.LedCoordinates[64].LocalY:F3})");
        Console.WriteLine($"Sample coords: LED 79 (LCtrl)=({device.LedCoordinates[79].LocalX:F3}, {device.LedCoordinates[79].LocalY:F3})");

        var testColors = new XenRGB.SDK.RgbColor[device.LedCount];
        for (int i = 0; i < testColors.Length; i++)
        {
            testColors[i] = new XenRGB.SDK.RgbColor(255, 0, (byte)(i * 2));
        }

        device.UpdateLedsAsync(new ReadOnlyMemory<XenRGB.SDK.RgbColor>(testColors)).GetAwaiter().GetResult();
        Console.WriteLine("NativeJsDevice Keyboard UpdateLedsAsync SUCCESS!");

        var msiDev = devices.FirstOrDefault(d => d.Attributes.VendorId == 0x1462 && d.Attributes.ProductId == 0x7D25);
        if (msiDev != null)
        {
            msiDev.OpenDevice();
            string msiPath = @"e:\Projects\XenRGB\src\XenRGB.Devices\NativeJs\Plugins\Msi\185 Byte Motherboard Controller\MSI_185Byte_Controller.js";
            string msiScript = File.ReadAllText(msiPath);
            var mbDevice = new NativeJsDevice(msiDev, msiScript, "MSI Pro Z690-A", XenRGB.Core.Devices.RgbDeviceType.Motherboard);
            Console.WriteLine($"Created Motherboard NativeJsDevice: Name={mbDevice.Name}, Type={mbDevice.DeviceType}, LedCount={mbDevice.LedCount}");
            Console.WriteLine($"Sample Fan 1 LED 0 (Top): ({mbDevice.LedCoordinates[6].LocalX:F3}, {mbDevice.LedCoordinates[6].LocalY:F3})");
            Console.WriteLine($"Sample Fan 1 LED 3 (Right): ({mbDevice.LedCoordinates[9].LocalX:F3}, {mbDevice.LedCoordinates[9].LocalY:F3})");
            Console.WriteLine($"Sample Fan 1 LED 6 (Bottom): ({mbDevice.LedCoordinates[12].LocalX:F3}, {mbDevice.LedCoordinates[12].LocalY:F3})");
            Console.WriteLine($"Sample Fan 1 LED 9 (Left): ({mbDevice.LedCoordinates[15].LocalX:F3}, {mbDevice.LedCoordinates[15].LocalY:F3})");
            var mbColors = new XenRGB.SDK.RgbColor[mbDevice.LedCount];
            // Onboard LEDs (0..5): Red
            for (int i = 0; i < 6; i++) mbColors[i] = new XenRGB.SDK.RgbColor(255, 0, 0);

            // ARGB Header 1 LEDs (6..65): Rainbow cycle across LEDs!
            // E.g. LED 6 = Red, LED 7 = Orange, LED 8 = Yellow, LED 9 = Green, LED 10 = Cyan, LED 11 = Blue, LED 12 = Magenta...
            for (int k = 0; k < 60; k++)
            {
                float hue = (float)(k % 12) / 12f * 360f;
                mbColors[6 + k] = XenRGB.SDK.RgbColor.FromHsv(hue, 1f, 1f);
            }

            // ARGB Header 2 LEDs (66..125):
            for (int k = 0; k < 60; k++)
            {
                float hue = (float)(k % 12) / 12f * 360f;
                mbColors[66 + k] = XenRGB.SDK.RgbColor.FromHsv(hue, 1f, 1f);
            }

            // 12V RGB (126)
            if (mbColors.Length > 126) mbColors[126] = new XenRGB.SDK.RgbColor(0, 0, 255);

            mbDevice.UpdateLedsAsync(new ReadOnlyMemory<XenRGB.SDK.RgbColor>(mbColors)).GetAwaiter().GetResult();
            Console.WriteLine("NativeJsDevice Motherboard UpdateLedsAsync with Per-LED Rainbow SUCCESS!");
        }
    }
}
