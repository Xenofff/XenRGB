using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XenRGB.Devices.OpenRgb;

/// <summary>
/// Low-level async client for the OpenRGB SDK network protocol over TCP (port 6742).
/// </summary>
public sealed class OpenRgbClient : IDisposable
{
    private const uint Magic = 0x4247524F; // "ORGB" in little-endian
    public const int DefaultPort = 6742;

    public const uint CommandRequestProtocolVersion = 0;
    public const uint CommandSetClientName = 1;
    public const uint CommandRequestControllerCount = 100;
    public const uint CommandRequestControllerData = 101;
    public const uint CommandUpdateLeds = 1050;

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _networkLock = new(1, 1);

    public bool IsConnected => _tcp?.Connected == true;
    public uint ServerProtocolVersion { get; private set; }

    public async Task<bool> ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        await _networkLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            DisconnectInternal();
            _tcp = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));
            using var reg = timeoutCts.Token.Register(() => _tcp?.Close());

            await _tcp.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
            _stream = _tcp.GetStream();

            // 1. Negotiate protocol version (we request v4)
            byte[] versionPayload = BitConverter.GetBytes((uint)4);
            await SendPacketInternalAsync(0, CommandRequestProtocolVersion, versionPayload, ct).ConfigureAwait(false);
            var reply = await ReadPacketInternalAsync(ct).ConfigureAwait(false);
            if (reply.Data.Length >= 4)
            {
                ServerProtocolVersion = BitConverter.ToUInt32(reply.Data, 0);
            }

            // 2. Set Client Name
            byte[] nameBytes = Encoding.ASCII.GetBytes("XenRGB\0");
            await SendPacketInternalAsync(0, CommandSetClientName, nameBytes, ct).ConfigureAwait(false);

            return true;
        }
        catch
        {
            DisconnectInternal();
            return false;
        }
        finally
        {
            _networkLock.Release();
        }
    }

    public async Task<uint> GetControllerCountAsync(CancellationToken ct = default)
    {
        await _networkLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await SendPacketInternalAsync(0, CommandRequestControllerCount, Array.Empty<byte>(), ct).ConfigureAwait(false);
            var reply = await ReadPacketInternalAsync(ct).ConfigureAwait(false);
            return reply.Data.Length >= 4 ? BitConverter.ToUInt32(reply.Data, 0) : 0;
        }
        finally
        {
            _networkLock.Release();
        }
    }

    public async Task<byte[]> GetControllerDataRawAsync(uint controllerIndex, CancellationToken ct = default)
    {
        await _networkLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            byte[] versionPayload = BitConverter.GetBytes(ServerProtocolVersion);
            await SendPacketInternalAsync(controllerIndex, CommandRequestControllerData, versionPayload, ct).ConfigureAwait(false);
            var reply = await ReadPacketInternalAsync(ct).ConfigureAwait(false);
            return reply.Data;
        }
        finally
        {
            _networkLock.Release();
        }
    }

    public async Task SendUpdateLedsAsync(uint controllerIndex, ReadOnlyMemory<uint> bgrColors, CancellationToken ct = default)
    {
        await _networkLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            int colorCount = bgrColors.Length;
            int payloadSize = 4 + 2 + (colorCount * 4); // data_size(4) + num_leds(2) + colors(4 * N)
            byte[] payload = new byte[payloadSize];

            BitConverter.TryWriteBytes(payload.AsSpan(0, 4), (uint)payloadSize);
            BitConverter.TryWriteBytes(payload.AsSpan(4, 2), (ushort)colorCount);

            for (int i = 0; i < colorCount; i++)
            {
                BitConverter.TryWriteBytes(payload.AsSpan(6 + i * 4, 4), bgrColors.Span[i]);
            }

            await SendPacketInternalAsync(controllerIndex, CommandUpdateLeds, payload, ct).ConfigureAwait(false);
        }
        finally
        {
            _networkLock.Release();
        }
    }

    private async Task SendPacketInternalAsync(uint deviceId, uint commandId, byte[] data, CancellationToken ct)
    {
        if (_stream == null) throw new InvalidOperationException("Not connected");

        byte[] header = new byte[16];
        BitConverter.TryWriteBytes(header.AsSpan(0, 4), Magic);
        BitConverter.TryWriteBytes(header.AsSpan(4, 4), deviceId);
        BitConverter.TryWriteBytes(header.AsSpan(8, 4), commandId);
        BitConverter.TryWriteBytes(header.AsSpan(12, 4), (uint)data.Length);

        await _stream.WriteAsync(header, ct).ConfigureAwait(false);
        if (data.Length > 0)
        {
            await _stream.WriteAsync(data, ct).ConfigureAwait(false);
        }
        await _stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private async Task<(uint DeviceId, uint CommandId, byte[] Data)> ReadPacketInternalAsync(CancellationToken ct)
    {
        if (_stream == null) throw new InvalidOperationException("Not connected");

        byte[] header = new byte[16];
        await ReadExactAsync(_stream, header, ct).ConfigureAwait(false);

        uint magic = BitConverter.ToUInt32(header, 0);
        if (magic != Magic)
        {
            throw new InvalidDataException($"Invalid OpenRGB magic: 0x{magic:X8}");
        }

        uint deviceId = BitConverter.ToUInt32(header, 4);
        uint commandId = BitConverter.ToUInt32(header, 8);
        uint dataLength = BitConverter.ToUInt32(header, 12);

        byte[] data = new byte[dataLength];
        if (dataLength > 0)
        {
            await ReadExactAsync(_stream, data, ct).ConfigureAwait(false);
        }

        return (deviceId, commandId, data);
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), ct).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException();
            totalRead += read;
        }
    }

    private void DisconnectInternal()
    {
        _stream?.Dispose();
        _stream = null;
        _tcp?.Dispose();
        _tcp = null;
    }

    public void Dispose()
    {
        DisconnectInternal();
        _networkLock.Dispose();
    }
}
