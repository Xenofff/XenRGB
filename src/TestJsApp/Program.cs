using System;
using System.IO;
using System.Threading.Tasks;
using Jint;
using Jint.Runtime;
using XenRGB.Devices.NativeJs;

class Stub : DeviceContext {
    public Stub() : base(null, s => { }) { }
    
    public new void send_report(object packetObj, int length)
    {
        var packet = packetObj as object[];
        Console.WriteLine($"send_report called, length={length}, isArray={packet != null}, actualLength={(packet?.Length ?? 0)}");
        if (packet != null && packet.Length > 0) {
            Console.WriteLine($"First byte: {packet[0]}");
        }
    }
}

class Program {
    static void Main() {
        var engine = new Engine();
        var stub = new Stub();
        engine.SetValue("device", stub);
        engine.SetValue("LightingMode", "");
        engine.SetValue("forcedColor", "");
        engine.Execute("var systeminfo = { GetBiosInfo: function() { return { version: '1.0'}; }, GetMotherboardInfo: function() { return { model: 'Board', manufacturer: 'MSI'}; }, GetMotherboardName: function() { return 'Board'; } };");
        engine.Execute("var Assert = { fail: function() {}, softIsDefined: function() {} };");
        engine.Execute("var permissions = { setCallback: function() {}, permissions: function() { return { 'lighting': true }; } };");
        engine.Execute("var console = { log: function() {}, warn: function() {}, error: function() {}, info: function() {} };");
        
        string path = @"e:\Projects\XenRGB\src\XenRGB.Devices\NativeJs\Plugins\Msi\185 Byte Motherboard Controller\MSI_185Byte_Controller.js";
        string scriptContent = File.ReadAllText(path);
        scriptContent = System.Text.RegularExpressions.Regex.Replace(scriptContent, @"export function ", "function ");
        scriptContent = System.Text.RegularExpressions.Regex.Replace(scriptContent, @"export const ", "const ");
        scriptContent = System.Text.RegularExpressions.Regex.Replace(scriptContent, @"export var ", "var ");
        scriptContent = System.Text.RegularExpressions.Regex.Replace(scriptContent, @"export let ", "let ");
        scriptContent = System.Text.RegularExpressions.Regex.Replace(scriptContent, @"^import\s+.*$", "", System.Text.RegularExpressions.RegexOptions.Multiline);
        
        engine.Execute(scriptContent);
        
        try {
            engine.Invoke("Initialize");
            Console.WriteLine("Init Success");
            engine.Invoke("Render");
            Console.WriteLine("Render Success");
        } catch (JavaScriptException ex) {
            Console.WriteLine("JS EX: " + ex.Message + "\n" + ex.JavaScriptStackTrace);
        } catch (Exception ex) {
            Console.WriteLine("EX: " + ex.ToString());
        }
    }
}
