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

    public void send_report(object[] packet, int length)
    {
        if (packet == null || length <= 0) return;

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

    public void write(object[] packet, int length)
    {
        send_report(packet, length);
    }

    public void pause(int ms)
    {
        // Thread.Sleep(ms);
        // Note: For XenRGB we might ignore pause or do it asynchronously if possible, 
        // but for compatibility we'll let it block the JS thread (it's isolated anyway).
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
}
