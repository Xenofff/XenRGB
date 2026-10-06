using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using HidLibrary;
using Jint;
using Jint.Native;

namespace XenRGB.Devices.NativeJs;

public class DeviceContext
{
    private readonly IHidDevice _hidDevice;
    private readonly Action<string> _logAction;

    public Engine? Engine { get; set; }

    public DeviceContext(IHidDevice hidDevice, Action<string> logAction)
    {
        _hidDevice = hidDevice;
        _logAction = logAction;
    }

    public void log(object message)
    {
        if (message == null) return;
        _logAction?.Invoke(message.ToString() ?? "");
    }

    public void log(object message, object options)
    {
        if (message == null) return;
        _logAction?.Invoke(message.ToString() ?? "");
    }

    private byte[]? BuildBuffer(object packetObj, int length)
    {
        if (packetObj == null || length <= 0) return null;
        var packet = packetObj as object[];
        if (packet == null && packetObj is JsArray jsArr)
        {
            var list = new object[(int)jsArr.Length];
            for (int i = 0; i < list.Length; i++)
            {
                var val = jsArr.Get(i.ToString());
                list[i] = val.IsNumber() ? val.AsNumber() : 0.0;
            }
            packet = list;
        }
        if (packet == null) return null;

        byte[] buffer = new byte[length];
        for (int i = 0; i < length && i < packet.Length; i++)
        {
            if (packet[i] is double d) buffer[i] = (byte)d;
            else if (packet[i] is int val) buffer[i] = (byte)val;
            else if (packet[i] is IConvertible c) buffer[i] = c.ToByte(null);
            else buffer[i] = 0;
        }
        return buffer;
    }

    public int send_report(object packetObj, int length)
    {
        byte[]? buffer = BuildBuffer(packetObj, length);
        if (buffer != null && _hidDevice != null)
        {
            return _hidDevice.WriteFeatureData(buffer) ? length : -1;
        }
        return -1;
    }

    private int _lastReadSize = 185; // Default to 185 for MSI

    public int write(object packetObj, int length)
    {
        byte[]? buffer = BuildBuffer(packetObj, length);
        if (buffer != null && _hidDevice != null)
        {
            return _hidDevice.Write(buffer) ? length : -1;
        }
        return -1;
    }

    public object[] read(object packetObj, int length)
    {
        _lastReadSize = length;
        object[] res = new object[length];
        for (int i = 0; i < length; i++) res[i] = 0.0;
        if (length > 6) res[6] = 1.0; // EZ LED enabled
        return res;
    }

    public object[] get_report(object packetObj, int length)
    {
        _lastReadSize = length == 200 ? 185 : length;
        object[] res = new object[length];
        for (int i = 0; i < length; i++) res[i] = 0.0;
        return res;
    }

    public void clearReadBuffer() { }

    public int getLastReadSize() { return _lastReadSize; }

    public void pause(int ms)
    {
        Thread.Sleep(ms);
    }

    // Colors & LED Mapping
    private byte[] _currentColors = Array.Empty<byte>(); // Format: R,G,B for each LED
    private readonly Dictionary<(int x, int y), int> _xyToLedIndex = new();
    private readonly Dictionary<string, int> _nameToLedIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _subdeviceToLedIndex = new(StringComparer.OrdinalIgnoreCase);

    public void SetLedMapping(IEnumerable<(int x, int y, string name, int index)> leds)
    {
        _xyToLedIndex.Clear();
        _nameToLedIndex.Clear();
        foreach (var led in leds)
        {
            _xyToLedIndex[(led.x, led.y)] = led.index;
            if (!string.IsNullOrEmpty(led.name))
            {
                _nameToLedIndex[led.name] = led.index;
            }
        }
    }

    public void UpdateColors(byte[] rgbBuffer)
    {
        _currentColors = rgbBuffer;
    }

    public int[] subdeviceColor(string subdeviceName, int row, int col)
    {
        if (!string.IsNullOrEmpty(subdeviceName) && _subdeviceToLedIndex.TryGetValue(subdeviceName, out int ledIdx))
        {
            int offset = ledIdx * 3;
            if (offset + 2 < _currentColors.Length)
            {
                return new int[] { _currentColors[offset], _currentColors[offset + 1], _currentColors[offset + 2] };
            }
        }

        if (_currentColors.Length >= 3)
            return new int[] { _currentColors[0], _currentColors[1], _currentColors[2] };
        return new int[] { 0, 0, 0 };
    }

    public int[] color(string ledName)
    {
        if (!string.IsNullOrEmpty(ledName) && _nameToLedIndex.TryGetValue(ledName, out int ledIdx))
        {
            int offset = ledIdx * 3;
            if (offset + 2 < _currentColors.Length)
            {
                return new int[] { _currentColors[offset], _currentColors[offset + 1], _currentColors[offset + 2] };
            }
        }

        if (_currentColors.Length >= 3)
            return new int[] { _currentColors[0], _currentColors[1], _currentColors[2] };
        return new int[] { 0, 0, 0 };
    }

    public int[] color(int x, int y)
    {
        if (_xyToLedIndex.TryGetValue((x, y), out int ledIdx))
        {
            int offset = ledIdx * 3;
            if (offset + 2 < _currentColors.Length)
            {
                return new int[] { _currentColors[offset], _currentColors[offset + 1], _currentColors[offset + 2] };
            }
        }

        if (_currentColors.Length >= 3)
            return new int[] { _currentColors[0], _currentColors[1], _currentColors[2] };
        return new int[] { 0, 0, 0 };
    }

    public void setSubdeviceName(string subdevice, string name) { }

    public void setSubdeviceSize(string subdevice, int width, int height) { }

    public void addProperty(string propertyJson) { }

    public void setName(string name) { }
    public void SetLedLimit(int limit) { }
    public void addChannel(object a, object b)
    {
        string channelName = a?.ToString() ?? "";
        int count = 60;
        if (b is int c) count = c;
        else if (b is double d) count = (int)d;
        else if (int.TryParse(b?.ToString(), out int parsed)) count = parsed;

        int channelLeds = Math.Min(60, Math.Max(40, count));
        _channels[channelName] = new ChannelStub(this, channelName, channelLeds);
    }

    private readonly Dictionary<string, ChannelStub> _channels = new(StringComparer.OrdinalIgnoreCase);

    public void createSubdevice(string name)
    {
        if (!string.IsNullOrEmpty(name) && !_subdeviceToLedIndex.ContainsKey(name))
        {
            _subdeviceToLedIndex[name] = _subdeviceToLedIndex.Count;
        }
    }

    public void setSubdeviceLeds(string name, object names, object positions) { }
    
    public object channel(object ch)
    {
        string name = ch?.ToString() ?? "";
        if (_channels.TryGetValue(name, out var existing))
            return existing;

        var stub = new ChannelStub(this, name, 60);
        _channels[name] = stub;
        return stub;
    }

    public int GetChannelLedOffset(string name)
    {
        if (name.Contains("2") || name.Contains("JRainbow2", StringComparison.OrdinalIgnoreCase))
            return 66; // Header 2 starts after 6 onboard + 60 header 1 LEDs

        // Default: Header 1 starts at 6 (after 6 onboard LEDs)
        return 6;
    }

    public JsArray createColorArray(object color, int count, string type)
    {
        var arr = Engine != null ? new JsArray(Engine) : new JsArray(new Engine());
        int r = 0, g = 0, b = 0;
        if (color is string hex && hex.Length >= 7 && hex.StartsWith("#"))
        {
            try
            {
                r = Convert.ToInt32(hex.Substring(1, 2), 16);
                g = Convert.ToInt32(hex.Substring(3, 2), 16);
                b = Convert.ToInt32(hex.Substring(5, 2), 16);
            }
            catch { }
        }
        else if (color is object[] rgb && rgb.Length >= 3)
        {
            r = Convert.ToInt32(rgb[0]);
            g = Convert.ToInt32(rgb[1]);
            b = Convert.ToInt32(rgb[2]);
        }

        for (int i = 0; i < count; i++)
        {
            arr.Push(r);
            arr.Push(g);
            arr.Push(b);
        }
        return arr;
    }

    public object getChannelPulseColor(object ch) { return new object[] { 0.0, 0.0, 0.0 }; }
    public void removeSubdevice(string name) { }
    public void setControllableLeds(object names, object positions) { }
    public void setSize(object size) { }

    public int productId() { return _hidDevice != null ? (int)_hidDevice.Attributes.ProductId : 0; }
    public int vendorId() { return _hidDevice != null ? (int)_hidDevice.Attributes.VendorId : 0; }

    public class ChannelStub
    {
        private readonly DeviceContext _context;
        private readonly string _name;
        private readonly int _ledCount;

        public ChannelStub(DeviceContext context, string name, int ledCount = 60)
        {
            _context = context;
            _name = name;
            _ledCount = ledCount;
        }

        public int LedCount() => _ledCount;
        public bool shouldPulseColors() => false;

        public JsArray getColors(string type)
        {
            var arr = _context.Engine != null ? new JsArray(_context.Engine) : new JsArray(new Engine());
            int offset = _context.GetChannelLedOffset(_name);

            for (int i = 0; i < _ledCount; i++)
            {
                int byteIdx = (offset + i) * 3;
                if (byteIdx + 2 < _context._currentColors.Length)
                {
                    arr.Push((int)_context._currentColors[byteIdx]);
                    arr.Push((int)_context._currentColors[byteIdx + 1]);
                    arr.Push((int)_context._currentColors[byteIdx + 2]);
                }
                else if (_context._currentColors.Length >= 3)
                {
                    arr.Push((int)_context._currentColors[0]);
                    arr.Push((int)_context._currentColors[1]);
                    arr.Push((int)_context._currentColors[2]);
                }
                else
                {
                    arr.Push(0);
                    arr.Push(0);
                    arr.Push(0);
                }
            }
            return arr;
        }
    }

    public void notify(string title, string message, int severity)
    {
        Debug.WriteLine($"[JS Notify] {title}: {message}");
    }
}
