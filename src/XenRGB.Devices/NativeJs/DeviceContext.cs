using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using HidLibrary;
using Jint;
using Jint.Native;

namespace XenRGB.Devices.NativeJs;

public class DeviceContext
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(
        IntPtr hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        IntPtr lpOverlapped);

    [DllImport("hid.dll", SetLastError = true)]
    private static extern bool HidD_SetFeature(
        IntPtr HidDeviceObject,
        byte[] ReportBuffer,
        uint ReportBufferLength);

    private static readonly FieldInfo? _denseField = typeof(JsArray).GetField("_dense", BindingFlags.NonPublic | BindingFlags.Instance);

    private readonly IHidDevice _hidDevice;
    private readonly IntPtr _fallbackWriteHandle;
    private IntPtr WriteHandle => (_hidDevice as HidDevice)?.WriteHandle ?? _fallbackWriteHandle;
    private readonly Action<string> _logAction;
    private readonly byte[] _reusablePacketBuffer = new byte[2048];

    public Engine? Engine { get; set; }

    public DeviceContext(IHidDevice hidDevice, Action<string> logAction)
    {
        _hidDevice = hidDevice;
        _logAction = logAction;

        if (hidDevice is HidDevice hd)
        {
            _fallbackWriteHandle = hd.WriteHandle;
        }

        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 32; y++)
            {
                _gridMap[x, y] = -1;
            }
        }
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

    private int BuildBuffer(object packetObj, int length)
    {
        if (packetObj == null || length <= 0) return 0;
        int targetLen = Math.Min(length, _reusablePacketBuffer.Length);

        if (packetObj is JsArray jsArr)
        {
            if (_denseField?.GetValue(jsArr) is JsValue[] dense)
            {
                int count = Math.Min(targetLen, dense.Length);
                for (int i = 0; i < count; i++)
                {
                    _reusablePacketBuffer[i] = (byte)dense[i].AsNumber();
                }
                if (count < targetLen)
                {
                    Array.Clear(_reusablePacketBuffer, count, targetLen - count);
                }
                return targetLen;
            }

            uint jsCount = Math.Min((uint)targetLen, jsArr.Length);
            for (uint i = 0; i < jsCount; i++)
            {
                var val = jsArr[i];
                _reusablePacketBuffer[i] = val.IsNumber() ? (byte)val.AsNumber() : (byte)0;
            }
            if (jsCount < targetLen)
            {
                Array.Clear(_reusablePacketBuffer, (int)jsCount, targetLen - (int)jsCount);
            }
            return targetLen;
        }

        if (packetObj is object[] packet)
        {
            int count = Math.Min(targetLen, packet.Length);
            for (int i = 0; i < count; i++)
            {
                if (packet[i] is double d) _reusablePacketBuffer[i] = (byte)d;
                else if (packet[i] is int val) _reusablePacketBuffer[i] = (byte)val;
                else if (packet[i] is IConvertible c) _reusablePacketBuffer[i] = c.ToByte(null);
                else _reusablePacketBuffer[i] = 0;
            }
            if (count < targetLen)
            {
                Array.Clear(_reusablePacketBuffer, count, targetLen - count);
            }
            return targetLen;
        }

        return 0;
    }

    public int send_report(object packetObj, int length)
    {
        int bytes = BuildBuffer(packetObj, length);
        if (bytes <= 0) return -1;

        IntPtr handle = WriteHandle;
        if (handle != IntPtr.Zero)
        {
            if (HidD_SetFeature(handle, _reusablePacketBuffer, (uint)bytes))
                return bytes;
        }

        if (_hidDevice != null)
        {
            byte[] fallback = new byte[bytes];
            Array.Copy(_reusablePacketBuffer, fallback, bytes);
            return _hidDevice.WriteFeatureData(fallback) ? bytes : -1;
        }
        return -1;
    }

    private int _lastReadSize = 185; // Default to 185 for MSI

    public int write(object packetObj, int length)
    {
        int bytes = BuildBuffer(packetObj, length);
        if (bytes <= 0) return -1;

        IntPtr handle = WriteHandle;
        if (handle != IntPtr.Zero)
        {
            if (WriteFile(handle, _reusablePacketBuffer, (uint)bytes, out _, IntPtr.Zero))
                return bytes;
        }

        if (_hidDevice != null)
        {
            byte[] fallback = new byte[bytes];
            Array.Copy(_reusablePacketBuffer, fallback, bytes);
            return _hidDevice.Write(fallback) ? bytes : -1;
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
        // Use WaitableTimer instead of Thread.Sleep to avoid raising global timer resolution
        XenRGB.Core.Engine.WaitableTimer.SleepWithTolerance(ms);
    }

    // Colors & LED Mapping
    private byte[] _currentColors = Array.Empty<byte>(); // Format: R,G,B for each LED
    private readonly int[,] _gridMap = new int[64, 32];
    private readonly Dictionary<(int x, int y), int> _xyToLedIndex = new();
    private readonly Dictionary<string, int> _nameToLedIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _subdeviceToLedIndex = new(StringComparer.OrdinalIgnoreCase);

    public void SetLedMapping(IEnumerable<(int x, int y, string name, int index)> leds)
    {
        _xyToLedIndex.Clear();
        _nameToLedIndex.Clear();
        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 32; y++)
            {
                _gridMap[x, y] = -1;
            }
        }

        foreach (var led in leds)
        {
            _xyToLedIndex[(led.x, led.y)] = led.index;
            if ((uint)led.x < 64 && (uint)led.y < 32)
            {
                _gridMap[led.x, led.y] = led.index;
            }
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

    private readonly int[] _reusableColor = new int[3];

    public int[] subdeviceColor(string subdeviceName, int row, int col)
    {
        if (!string.IsNullOrEmpty(subdeviceName) && _subdeviceToLedIndex.TryGetValue(subdeviceName, out int ledIdx))
        {
            int offset = ledIdx * 3;
            if (offset + 2 < _currentColors.Length)
            {
                _reusableColor[0] = _currentColors[offset];
                _reusableColor[1] = _currentColors[offset + 1];
                _reusableColor[2] = _currentColors[offset + 2];
                return _reusableColor;
            }
        }

        if (_currentColors.Length >= 3)
        {
            _reusableColor[0] = _currentColors[0];
            _reusableColor[1] = _currentColors[1];
            _reusableColor[2] = _currentColors[2];
            return _reusableColor;
        }

        _reusableColor[0] = 0; _reusableColor[1] = 0; _reusableColor[2] = 0;
        return _reusableColor;
    }

    public int[] color(string ledName)
    {
        if (!string.IsNullOrEmpty(ledName) && _nameToLedIndex.TryGetValue(ledName, out int ledIdx))
        {
            int offset = ledIdx * 3;
            if (offset + 2 < _currentColors.Length)
            {
                _reusableColor[0] = _currentColors[offset];
                _reusableColor[1] = _currentColors[offset + 1];
                _reusableColor[2] = _currentColors[offset + 2];
                return _reusableColor;
            }
        }

        if (_currentColors.Length >= 3)
        {
            _reusableColor[0] = _currentColors[0];
            _reusableColor[1] = _currentColors[1];
            _reusableColor[2] = _currentColors[2];
            return _reusableColor;
        }

        _reusableColor[0] = 0; _reusableColor[1] = 0; _reusableColor[2] = 0;
        return _reusableColor;
    }

    public int[] color(int x, int y)
    {
        int ledIdx = ((uint)x < 64 && (uint)y < 32) ? _gridMap[x, y] : -1;
        if (ledIdx < 0 && !_xyToLedIndex.TryGetValue((x, y), out ledIdx))
        {
            if (_currentColors.Length >= 3)
            {
                _reusableColor[0] = _currentColors[0];
                _reusableColor[1] = _currentColors[1];
                _reusableColor[2] = _currentColors[2];
                return _reusableColor;
            }
            _reusableColor[0] = 0; _reusableColor[1] = 0; _reusableColor[2] = 0;
            return _reusableColor;
        }

        int offset = ledIdx * 3;
        if (offset + 2 < _currentColors.Length)
        {
            _reusableColor[0] = _currentColors[offset];
            _reusableColor[1] = _currentColors[offset + 1];
            _reusableColor[2] = _currentColors[offset + 2];
            return _reusableColor;
        }

        if (_currentColors.Length >= 3)
        {
            _reusableColor[0] = _currentColors[0];
            _reusableColor[1] = _currentColors[1];
            _reusableColor[2] = _currentColors[2];
            return _reusableColor;
        }

        _reusableColor[0] = 0; _reusableColor[1] = 0; _reusableColor[2] = 0;
        return _reusableColor;
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
