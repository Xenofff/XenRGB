using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XenRGB.Devices.OpenRgb;

/// <summary>
/// Low-level async client for the OpenRGB SDK network protocol over TCP (port 6742).
/// Supports protocol versions 0-6.
/// </summary>
public sealed class OpenRgbClient : IOpenRgbClient
{
    private const uint Magic = 0x4247524F; // "ORGB" in little-endian

    public const int DefaultPort = 6742;

    public const uint CommandRequestControllerCount = 0;
    public const uint CommandRequestControllerData = 1;
    public const uint CommandAck = 10;
    public const uint CommandRequestProtocolVersion = 40;
    public const uint CommandSetClientName = 50;
    public const uint CommandSetServerName = 51;
    public const uint CommandSetClientFlags = 52;
    public const uint CommandSetServerFlags = 53;
    public const uint CommandRequestRescanDevices = 140;
    public const uint CommandUpdateLeds = 1050;

    /// <summary>
    /// Packet IDs that the server may push to the client without a prior request.
    /// We must drain/skip these instead of treating them as a response to our last command.
    /// </summary>
    private static readonly HashSet<uint> _unsolicitedCommandIds = new()
    {
        CommandAck,            // 10  - ACK for SetClientName, etc.
        CommandSetServerName,  // 51  - server name push (v6)
        CommandSetServerFlags, // 53  - server flags push (v6)
        100, // NET_PACKET_ID_DEVICE_LIST_UPDATED
        101, // NET_PACKET_ID_DETECTION_STARTED
        102, // NET_PACKET_ID_DETECTION_PROGRESS_CHANGED
        103, // NET_PACKET_ID_DETECTION_COMPLETE
    };

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _networkLock = new(1, 1);
    private bool _isDisposed;

    /// <summary>
    /// For protocol v6 the count response also contains a list of unique device IDs.
    /// This list is used as the "device index" when requesting controller data.
    /// </summary>
    private uint[]? _controllerUniqueIds;

    public bool IsConnected => !_isDisposed && _tcp?.Connected == true;
    public uint ServerProtocolVersion { get; private set; } = 4;

    /// <summary>
    /// Returns the device ID to use in the packet header for the given zero-based controller index.
    /// In protocol v0-5 this is just the index; in v6 it is the unique ID returned by the server.
    /// </summary>
    public uint GetDeviceId(uint controllerIndex)
    {
        if (ServerProtocolVersion >= 6 && _controllerUniqueIds != null
            && controllerIndex < (uint)_controllerUniqueIds.Length)
        {
            return _controllerUniqueIds[controllerIndex];
        }
        return controllerIndex;
    }

    public async Task<bool> ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        if (_isDisposed) return false;

        try
        {
            await _networkLock.WaitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }

        try
        {
            DisconnectInternal();
            _controllerUniqueIds = null;
            _tcp = new TcpClient();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));
            using var reg = timeoutCts.Token.Register(() =>
            {
                try { _tcp?.Close(); } catch { }
            });

            await _tcp.ConnectAsync(host, port, timeoutCts.Token).ConfigureAwait(false);
            _stream = _tcp.GetStream();

            // 1. Negotiate protocol version (propose the maximum we support: v6)
            const uint proposedVersion = 6;
            byte[] versionPayload = BitConverter.GetBytes(proposedVersion);
            await SendPacketInternalAsync(0, CommandRequestProtocolVersion, versionPayload, timeoutCts.Token).ConfigureAwait(false);

            // Read the protocol version reply, skipping any unsolicited push packets.
            var reply = await ReadExpectedPacketAsync(CommandRequestProtocolVersion, timeoutCts.Token).ConfigureAwait(false);
            if (reply.Data.Length >= 4)
            {
                uint serverVer = BitConverter.ToUInt32(reply.Data, 0);
                ServerProtocolVersion = Math.Min(proposedVersion, serverVer);
            }
            else
            {
                ServerProtocolVersion = 0;
            }

            // 2. Set Client Name (server will ACK or push SET_SERVER_NAME/SET_SERVER_FLAGS in v6)
            byte[] nameBytes = Encoding.ASCII.GetBytes("XenRGB\0");
            await SendPacketInternalAsync(0, CommandSetClientName, nameBytes, timeoutCts.Token).ConfigureAwait(false);

            // 3. Drain any immediate unsolicited packets (SET_SERVER_NAME, SET_SERVER_FLAGS, ACKs).
            //    Give the server up to 500 ms to push these before we continue.
            await DrainUnsolicitedAsync(timeoutCts.Token, maxWaitMs: 500).ConfigureAwait(false);

            return true;
        }
        catch
        {
            DisconnectInternal();
            return false;
        }
        finally
        {
            try { _networkLock.Release(); } catch { }
        }
    }

    public async Task<uint> GetControllerCountAsync(CancellationToken ct = default)
    {
        await _networkLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await SendPacketInternalAsync(0, CommandRequestControllerCount, Array.Empty<byte>(), ct).ConfigureAwait(false);
            var reply = await ReadExpectedPacketAsync(CommandRequestControllerCount, ct).ConfigureAwait(false);

            if (reply.Data.Length < 4)
                return 0;

            uint count = BitConverter.ToUInt32(reply.Data, 0);

            // Protocol v6: response is count (4 bytes) + unique_id[count] (4 bytes each)
            if (ServerProtocolVersion >= 6)
            {
                int expectedLen = 4 + (int)count * 4;
                if (reply.Data.Length >= expectedLen && count > 0)
                {
                    var ids = new uint[count];
                    for (int i = 0; i < (int)count; i++)
                    {
                        ids[i] = BitConverter.ToUInt32(reply.Data, 4 + i * 4);
                    }
                    _controllerUniqueIds = ids;
                }
                else
                {
                    _controllerUniqueIds = null;
                }
            }

            return count;
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
            uint deviceId = GetDeviceId(controllerIndex);
            byte[] versionPayload = BitConverter.GetBytes(ServerProtocolVersion);
            await SendPacketInternalAsync(deviceId, CommandRequestControllerData, versionPayload, ct).ConfigureAwait(false);
            var reply = await ReadExpectedPacketAsync(CommandRequestControllerData, ct).ConfigureAwait(false);
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
            uint deviceId = GetDeviceId(controllerIndex);
            int colorCount = bgrColors.Length;
            int payloadSize = 4 + 2 + (colorCount * 4); // data_size(4) + num_leds(2) + colors(4 * N)
            byte[] payload = new byte[payloadSize];

            BitConverter.TryWriteBytes(payload.AsSpan(0, 4), (uint)payloadSize);
            BitConverter.TryWriteBytes(payload.AsSpan(4, 2), (ushort)colorCount);

            for (int i = 0; i < colorCount; i++)
            {
                BitConverter.TryWriteBytes(payload.AsSpan(6 + i * 4, 4), bgrColors.Span[i]);
            }

            await SendPacketInternalAsync(deviceId, CommandUpdateLeds, payload, ct).ConfigureAwait(false);
        }
        finally
        {
            _networkLock.Release();
        }
    }

    public async Task RequestRescanAsync(CancellationToken ct = default)
    {
        if (!IsConnected) return;
        await _networkLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await SendPacketInternalAsync(0, CommandRequestRescanDevices, Array.Empty<byte>(), ct).ConfigureAwait(false);
            // Rescan has no defined response; drain ACKs or detection-started packets.
            await DrainUnsolicitedAsync(ct, maxWaitMs: 200).ConfigureAwait(false);
        }
        catch
        {
            // Rescan request may not be supported on older servers
        }
        finally
        {
            _networkLock.Release();
        }
    }

    /// <summary>
    /// Reads packets until the expected command ID is received, discarding unsolicited push packets in between.
    /// </summary>
    private async Task<(uint DeviceId, uint CommandId, byte[] Data)> ReadExpectedPacketAsync(
        uint expectedCommandId, CancellationToken ct)
    {
        const int maxSkips = 20; // guard against infinite loops
        for (int i = 0; i < maxSkips; i++)
        {
            var pkt = await ReadPacketInternalAsync(ct).ConfigureAwait(false);
            if (pkt.CommandId == expectedCommandId)
                return pkt;

            // Silently discard known unsolicited or irrelevant push packets.
            if (_unsolicitedCommandIds.Contains(pkt.CommandId))
                continue;

            // Unknown unexpected packet - return it anyway so the caller can handle it.
            return pkt;
        }

        throw new InvalidDataException($"Did not receive expected packet ID={expectedCommandId} after {maxSkips} packets.");
    }

    /// <summary>
    /// Drains any unsolicited packets waiting in the TCP buffer by polling DataAvailable.
    /// </summary>
    private async Task DrainUnsolicitedAsync(CancellationToken ct, int maxWaitMs = 300)
    {
        if (_stream == null) return;

        var deadline = Environment.TickCount64 + maxWaitMs;
        while (Environment.TickCount64 < deadline && !ct.IsCancellationRequested)
        {
            if (_stream.DataAvailable)
            {
                try
                {
                    var pkt = await ReadPacketInternalAsync(ct).ConfigureAwait(false);
                    if (!_unsolicitedCommandIds.Contains(pkt.CommandId))
                    {
                        // Unexpected non-unsolicited packet appeared spontaneously - stop draining.
                        break;
                    }
                }
                catch
                {
                    break;
                }
            }
            else
            {
                await Task.Delay(20, ct).ConfigureAwait(false);
            }
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

    public void Disconnect()
    {
        DisconnectInternal();
    }

    private void DisconnectInternal()
    {
        try { _stream?.Dispose(); } catch { }
        _stream = null;
        try { _tcp?.Dispose(); } catch { }
        _tcp = null;
        _controllerUniqueIds = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        DisconnectInternal();
        try { _networkLock.Dispose(); } catch { }
    }
}
