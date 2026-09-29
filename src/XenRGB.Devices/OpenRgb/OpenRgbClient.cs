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
///
/// Design contract:
///   Every request-response exchange owns the _networkLock for its duration.
///   SendUpdateLedsAsync opportunistically drains the ACK that OpenRGB v6 sends in
///   response to every UpdateLeds command (zero-wait, so it never slows the hot path).
///   ReadExpectedAsync tolerates residual push packets via a large skip limit (512).
/// </summary>
public sealed class OpenRgbClient : IOpenRgbClient
{
    private const uint Magic = 0x4247524F; // "ORGB" little-endian

    public const int DefaultPort = 6742;

    public const uint CommandRequestControllerCount  = 0;
    public const uint CommandRequestControllerData   = 1;
    public const uint CommandAck                     = 10;
    public const uint CommandRequestProtocolVersion  = 40;
    public const uint CommandSetClientName           = 50;
    public const uint CommandSetServerName           = 51;
    public const uint CommandSetClientFlags          = 52;
    public const uint CommandSetServerFlags          = 53;
    public const uint CommandRequestRescanDevices    = 140;
    public const uint CommandUpdateLeds              = 1050;

    // Push-packet IDs the server sends at any time without a prior client request.
    // ReadExpectedAsync skips these to find the actual response.
    private static readonly HashSet<uint> _pushPacketIds = new()
    {
        CommandAck,            // 10  - ACK for any command (v6 sends for UpdateLeds)
        CommandSetServerName,  // 51  - server name (v6 push)
        CommandSetServerFlags, // 53  - server flags (v6 push)
        100, // NET_PACKET_ID_DEVICE_LIST_UPDATED
        101, // NET_PACKET_ID_DETECTION_STARTED
        102, // NET_PACKET_ID_DETECTION_PROGRESS_CHANGED
        103, // NET_PACKET_ID_DETECTION_COMPLETE
    };

    private TcpClient?     _tcp;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _networkLock = new(1, 1);
    private bool _isDisposed;

    // v6 unique controller IDs received in the controller-count response.
    private uint[]? _controllerUniqueIds;

    public bool IsConnected => !_isDisposed && _tcp?.Connected == true;
    public uint ServerProtocolVersion { get; private set; } = 4;

    /// <summary>
    /// Returns the device ID to use in packet headers.
    /// v0-5: sequential index.  v6: unique ID assigned by the server.
    /// </summary>
    public uint GetDeviceId(uint controllerIndex)
    {
        if (ServerProtocolVersion >= 6 && _controllerUniqueIds != null
            && controllerIndex < (uint)_controllerUniqueIds.Length)
            return _controllerUniqueIds[controllerIndex];
        return controllerIndex;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<bool> ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        if (_isDisposed) return false;
        try { await _networkLock.WaitAsync(ct).ConfigureAwait(false); }
        catch { return false; }

        try
        {
            DisconnectInternal();
            _controllerUniqueIds = null;
            _tcp = new TcpClient();

            using var toCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            toCts.CancelAfter(TimeSpan.FromSeconds(3));
            using var reg = toCts.Token.Register(() => { try { _tcp?.Close(); } catch { } });

            await _tcp.ConnectAsync(host, port, toCts.Token).ConfigureAwait(false);
            _stream = _tcp.GetStream();

            // 1. Propose protocol version (max supported: v6).
            const uint proposed = 6;
            await SendRawAsync(0, CommandRequestProtocolVersion,
                BitConverter.GetBytes(proposed), toCts.Token).ConfigureAwait(false);
            var verReply = await ReadExpectedAsync(CommandRequestProtocolVersion, toCts.Token).ConfigureAwait(false);
            ServerProtocolVersion = verReply.Data.Length >= 4
                ? Math.Min(proposed, BitConverter.ToUInt32(verReply.Data, 0))
                : 0u;

            // 2. Announce client name.
            // v6 server responds with SET_SERVER_NAME (51), SET_SERVER_FLAGS (53), ACK (10).
            await SendRawAsync(0, CommandSetClientName,
                Encoding.ASCII.GetBytes("XenRGB\0"), toCts.Token).ConfigureAwait(false);

            // 3. Drain those server-initiated push packets (poll up to 500 ms).
            await DrainPushPacketsAsync(toCts.Token, maxWaitMs: 500).ConfigureAwait(false);

            return true;
        }
        catch { DisconnectInternal(); return false; }
        finally { try { _networkLock.Release(); } catch { } }
    }

    public async Task<uint> GetControllerCountAsync(CancellationToken ct = default)
    {
        await _networkLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await SendRawAsync(0, CommandRequestControllerCount, Array.Empty<byte>(), ct).ConfigureAwait(false);
            // ReadExpectedAsync skips any residual push packets (up to 512).
            var reply = await ReadExpectedAsync(CommandRequestControllerCount, ct).ConfigureAwait(false);

            if (reply.Data.Length < 4) return 0;
            uint count = BitConverter.ToUInt32(reply.Data, 0);

            // v6: count response contains count + unique_id[count].
            if (ServerProtocolVersion >= 6)
            {
                int needed = 4 + (int)count * 4;
                if (reply.Data.Length >= needed && count > 0)
                {
                    var ids = new uint[count];
                    for (int i = 0; i < (int)count; i++)
                        ids[i] = BitConverter.ToUInt32(reply.Data, 4 + i * 4);
                    _controllerUniqueIds = ids;
                }
                else { _controllerUniqueIds = null; }
            }

            return count;
        }
        finally { _networkLock.Release(); }
    }

    public async Task<byte[]> GetControllerDataRawAsync(uint controllerIndex, CancellationToken ct = default)
    {
        await _networkLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            uint devId = GetDeviceId(controllerIndex);
            await SendRawAsync(devId, CommandRequestControllerData,
                BitConverter.GetBytes(ServerProtocolVersion), ct).ConfigureAwait(false);
            var reply = await ReadExpectedAsync(CommandRequestControllerData, ct).ConfigureAwait(false);
            return reply.Data;
        }
        finally { _networkLock.Release(); }
    }

    /// <summary>
    /// Sends an LED update frame.
    ///
    /// In protocol v6, OpenRGB sends an ACK (ID=10) in response to every UpdateLeds command.
    /// We perform a zero-wait opportunistic drain immediately after sending: if the ACK is
    /// already in the TCP receive buffer (DataAvailable == true) we consume it right now.
    /// If it has not arrived yet we skip without blocking - we NEVER sleep here.
    ///
    /// This keeps the buffer clean across the 30fps hot path without adding latency.
    /// Any ACKs that slip through are tolerated by ReadExpectedAsync's 512-skip limit.
    /// </summary>
    public async Task SendUpdateLedsAsync(uint controllerIndex, ReadOnlyMemory<uint> bgrColors, CancellationToken ct = default)
    {
        await _networkLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            uint devId     = GetDeviceId(controllerIndex);
            int colorCount = bgrColors.Length;
            // Payload layout: data_size(4) + num_leds(2) + colors(4*N)
            int payloadSize = 4 + 2 + (colorCount * 4);
            byte[] payload = new byte[payloadSize];
            BitConverter.TryWriteBytes(payload.AsSpan(0, 4), (uint)payloadSize);
            BitConverter.TryWriteBytes(payload.AsSpan(4, 2), (ushort)colorCount);
            for (int i = 0; i < colorCount; i++)
                BitConverter.TryWriteBytes(payload.AsSpan(6 + i * 4, 4), bgrColors.Span[i]);

            await SendRawAsync(devId, CommandUpdateLeds, payload, ct).ConfigureAwait(false);

            // Zero-wait ACK drain (v6 only).
            if (ServerProtocolVersion >= 6)
                await DrainOneIfAvailableAsync(ct).ConfigureAwait(false);
        }
        finally { _networkLock.Release(); }
    }

    public async Task RequestRescanAsync(CancellationToken ct = default)
    {
        if (!IsConnected) return;
        await _networkLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await SendRawAsync(0, CommandRequestRescanDevices, Array.Empty<byte>(), ct).ConfigureAwait(false);
            await DrainOneIfAvailableAsync(ct).ConfigureAwait(false);
        }
        catch { /* server may not support rescan */ }
        finally { _networkLock.Release(); }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Reads packets in a loop, skipping known push packets, until the expected command
    /// ID is received.  maxSkips=512 absorbs up to 512 consecutive push packets (e.g.
    /// ACKs accumulated between heartbeat periods) before giving up.
    /// </summary>
    private async Task<(uint DeviceId, uint CommandId, byte[] Data)> ReadExpectedAsync(
        uint expectedCmd, CancellationToken ct)
    {
        const int maxSkips = 512;
        for (int i = 0; i < maxSkips; i++)
        {
            var pkt = await ReadPacketAsync(ct).ConfigureAwait(false);
            if (pkt.CommandId == expectedCmd) return pkt;
            if (_pushPacketIds.Contains(pkt.CommandId)) continue; // known push - skip
            return pkt; // unknown unexpected packet - return as-is
        }
        throw new InvalidDataException(
            $"OpenRGB: expected cmd={expectedCmd} not received after {maxSkips} skips.");
    }

    /// <summary>
    /// Zero-wait drain: if a packet is already sitting in the TCP receive buffer
    /// (DataAvailable == true), read and discard exactly one packet.  No sleep, no loop.
    /// Safe to call on the LED hot path.  MUST be called inside _networkLock.
    /// </summary>
    private async Task DrainOneIfAvailableAsync(CancellationToken ct)
    {
        if (_stream == null || !_stream.DataAvailable) return;
        try { await ReadPacketAsync(ct).ConfigureAwait(false); }
        catch { /* ignore; socket may have just disconnected */ }
    }

    /// <summary>
    /// Polling drain used only during ConnectAsync (startup / reconnect).
    /// Reads and discards push packets for up to maxWaitMs milliseconds.
    /// MUST be called inside _networkLock.
    /// </summary>
    private async Task DrainPushPacketsAsync(CancellationToken ct, int maxWaitMs)
    {
        if (_stream == null) return;
        long deadline = Environment.TickCount64 + maxWaitMs;
        while (Environment.TickCount64 < deadline && !ct.IsCancellationRequested)
        {
            if (_stream.DataAvailable)
            {
                try
                {
                    var pkt = await ReadPacketAsync(ct).ConfigureAwait(false);
                    if (!_pushPacketIds.Contains(pkt.CommandId)) break; // non-push: stop
                }
                catch { break; }
            }
            else { await Task.Delay(10, ct).ConfigureAwait(false); }
        }
    }

    private async Task SendRawAsync(uint devId, uint cmdId, byte[] data, CancellationToken ct)
    {
        if (_stream == null) throw new InvalidOperationException("Not connected");

        byte[] hdr = new byte[16];
        BitConverter.TryWriteBytes(hdr.AsSpan(0,  4), Magic);
        BitConverter.TryWriteBytes(hdr.AsSpan(4,  4), devId);
        BitConverter.TryWriteBytes(hdr.AsSpan(8,  4), cmdId);
        BitConverter.TryWriteBytes(hdr.AsSpan(12, 4), (uint)data.Length);

        await _stream.WriteAsync(hdr,  ct).ConfigureAwait(false);
        if (data.Length > 0)
            await _stream.WriteAsync(data, ct).ConfigureAwait(false);
        await _stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private async Task<(uint DeviceId, uint CommandId, byte[] Data)> ReadPacketAsync(CancellationToken ct)
    {
        if (_stream == null) throw new InvalidOperationException("Not connected");

        byte[] hdr = new byte[16];
        await ReadExactAsync(_stream, hdr, ct).ConfigureAwait(false);

        uint magic = BitConverter.ToUInt32(hdr, 0);
        if (magic != Magic) throw new InvalidDataException($"Bad OpenRGB magic: 0x{magic:X8}");

        uint devId = BitConverter.ToUInt32(hdr, 4);
        uint cmdId = BitConverter.ToUInt32(hdr, 8);
        uint len   = BitConverter.ToUInt32(hdr, 12);

        byte[] data = new byte[len];
        if (len > 0) await ReadExactAsync(_stream, data, ct).ConfigureAwait(false);

        return (devId, cmdId, data);
    }

    private static async Task ReadExactAsync(Stream s, byte[] buf, CancellationToken ct)
    {
        int read = 0;
        while (read < buf.Length)
        {
            int n = await s.ReadAsync(buf.AsMemory(read, buf.Length - read), ct).ConfigureAwait(false);
            if (n == 0) throw new EndOfStreamException();
            read += n;
        }
    }

    public void Disconnect() => DisconnectInternal();

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