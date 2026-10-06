using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using HidLibrary;
using Jint;

namespace XenRGB.Devices.NativeJs;

public class DeviceContext
{
    private readonly IHidDevice _hidDevice;
    private readonly Action<string> _logAction;

    public DeviceContext(IHidDevice hidDevice, Action<string> logAction)
    {
        _hidDevice = hidDevice;
        _logAction = logAction;
    }

    public void log(object message)
    {
        if (message == null) return;
        _logAction?.Invoke(message.ToString());
    }

    public void log(object message, object options)
    {
        if (message == null) return;
        _logAction?.Invoke(message.ToString());
    }

    public void send_report(object packetObj, int length)
    {
        if (packetObj == null || length <= 0) return;

        // Try to handle JS array
        var packet = packetObj as object[];

        // Convert the boxed JS array (double/int elements) to byte[]
        byte[] buffer = new byte[length];
        for (int i = 0; i < length && i < packet.Length; i++)
        {
            if (packet[i] is double d)
                buffer[i] = (byte)d;
            else if (packet[i] is IConvertible c)
                buffer[i] = c.ToByte(null);
            else
                buffer[i] = 0;
        }

        // HID report. Often needs a report ID at index 0. If length matches exactly, we just send it.
        // SignalRGB usually formats the array with Report ID at index 0.
        _hidDevice.Write(buffer);
    }

    public void write(object packetObj, int length)
    {
        send_report(packetObj, length);
    }

    public object[] read(object packetObj, int length)
    {
        // Stub: return a dummy array
        object[] res = new object[length];
        for (int i = 0; i < length; i++) res[i] = 0.0;
        return res;
    }

    public object[] get_report(object packetObj, int length)
    {
        // Stub: return a dummy array
        object[] res = new object[length];
        for (int i = 0; i < length; i++) res[i] = 0.0;
        return res;
    }

    public void clearReadBuffer() { }

    public int getLastReadSize() { return 0; }

    public void pause(int ms)
    {
        System.Threading.Thread.Sleep(ms);
    }

    // Colors mapping
    private byte[] _currentColors = Array.Empty<byte>(); // Format: R,G,B for each LED

    public void UpdateColors(byte[] rgbBuffer)
    {
        _currentColors = rgbBuffer;
    }

    public int[] subdeviceColor(string subdeviceName, int row, int col)
    {
        // Stub: In SignalRGB, this gets the color assigned to a specific LED/zone.
        // For XenRGB MVP, we just return a mapping or dummy color, or the single mapped color.
        // We will implement real mapping later.
        if (_currentColors.Length >= 3)
            return new int[] { _currentColors[0], _currentColors[1], _currentColors[2] };
        return new int[] { 0, 0, 0 };
    }

    public int[] color(string ledName)
    {
        if (_currentColors.Length >= 3)
            return new int[] { _currentColors[0], _currentColors[1], _currentColors[2] };
        return new int[] { 0, 0, 0 };
    }

    public int[] color(int x, int y)
    {
        if (_currentColors.Length >= 3)
            return new int[] { _currentColors[0], _currentColors[1], _currentColors[2] };
        return new int[] { 0, 0, 0 };
    }

    public void setSubdeviceName(string subdevice, string name)
    {
        // Stub for mapping
    }

    public void setSubdeviceSize(string subdevice, int width, int height)
    {
        // Stub for mapping
    }

    public void addProperty(string propertyJson)
    {
        // Stub
    }

    // Missing Stubs
    public void SetLedLimit(int limit) { }
    public void addChannel(object a, object b) { }
    public void createSubdevice(string name) { }
    public void setSubdeviceLeds(string name, object names, object positions) { }
    public object channel(object ch) { return new ChannelStub(); }
    public object createColorArray(object color, int count, string type) { return new object[count * 3]; }
    public object getChannelPulseColor(object ch) { return new object[] {0.0, 0.0, 0.0}; }
    public void removeSubdevice(string name) { }
    public void setControllableLeds(object names, object positions) { }
    public void setSize(object size) { }
    public string productId() { return "0x" + _hidDevice.Attributes.ProductId.ToString("X4"); }

    public class ChannelStub {
        public int LedCount() { return 1; }
        public object getColors(string type) { return new object[3]; }
    }

    public void notify(string title, string message, int severity)
    {
        // Dummy implementation to avoid JS errors
        System.Diagnostics.Debug.WriteLine($"[JS Notify] {title}: {message}");
    }
}
