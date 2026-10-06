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
            var mbColors = new XenRGB.SDK.RgbColor[mbDevice.LedCount];
            Array.Fill(mbColors, new XenRGB.SDK.RgbColor(0, 255, 200));
            mbDevice.UpdateLedsAsync(new ReadOnlyMemory<XenRGB.SDK.RgbColor>(mbColors)).GetAwaiter().GetResult();
            Console.WriteLine("NativeJsDevice Motherboard UpdateLedsAsync SUCCESS!");
        }
    }
}
