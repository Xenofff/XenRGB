using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace XenRGB.Devices.OpenRgb;

/// <summary>
/// Low-level async client for the OpenRGB SDK network protocol over TCP (port 6742).
/// Supports protocol versions 0-6.
///
/// Synchronization model:
///
///   _writeLock
///       Serializes all writes to the TCP stream.
///       UpdateLeds uses ONLY this lock and never reads from TCP.
///
///   _requestLock
///       Serializes request/response exchanges because there is only one TCP
///       response stream. A request owns this lock while sending its request
///       and reading the corresponding response.
///
/// TCP is full-duplex, so an UpdateLeds write can happen while the request
/// side is reading ACKs/responses.
///
/// IMPORTANT:
///   SendUpdateLedsAsync is deliberately write-only. It never calls
///   DataAvailable, Socket.Available, ReadPacketAsync or any other read.
/// </summary>
public sealed class OpenRgbClient : IOpenRgbClient
{
    private const uint Magic = 0x4247524F; // "ORGB" little-endian

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
    public const uint CommandResizeZone = 1000;
    public const uint CommandUpdateLeds = 1050;

    // Push-packet IDs the server can send without a prior client request.
    private static readonly HashSet<uint> _pushPacketIds = new()
    {
        CommandAck,            // 10 - ACK for commands
        CommandSetServerName,  // 51 - server name
        CommandSetServerFlags, // 53 - server flags
        100,                   // NET_PACKET_ID_DEVICE_LIST_UPDATED
        101,                   // NET_PACKET_ID_DETECTION_STARTED
        102,                   // NET_PACKET_ID_DETECTION_PROGRESS_CHANGED
        103,                   // NET_PACKET_ID_DETECTION_COMPLETE
    };

    private TcpClient? _tcp;
    private NetworkStream? _stream;

    // Serializes writes only.
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // Serializes request/response operations only.
    //
    // There must never be two operations simultaneously reading the response
    // stream, otherwise one request can consume another request's response.
    private readonly SemaphoreSlim _requestLock = new(1, 1);

    private bool _isDisposed;

    // Maximum time to wait for the relevant synchronization lock.
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(8);

    // Maximum time for request/response network reads.
    private static readonly TimeSpan _readTimeout = TimeSpan.FromSeconds(5);

    // v6 unique controller IDs received in the controller-count response.
    private uint[]? _controllerUniqueIds;

    public bool IsConnected =>
        !_isDisposed &&
        _tcp?.Connected == true;

    public uint ServerProtocolVersion { get; private set; } = 4;

    /// <summary>
    /// Returns the device ID to use in packet headers.
    /// v0-5: sequential index.
    /// v6: unique ID assigned by the server.
    /// </summary>
    public uint GetDeviceId(uint controllerIndex)
    {
        uint[]? ids = _controllerUniqueIds;

        if (ServerProtocolVersion >= 6 &&
            ids != null &&
            controllerIndex < (uint)ids.Length)
        {
            return ids[controllerIndex];
        }

        return controllerIndex;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<bool> ConnectAsync(
        string host,
        int port,
        CancellationToken ct = default)
    {
        if (_isDisposed)
            return false;

        // Connect/disconnect changes the whole connection state, so acquire
        // both locks in a fixed order: request -> write.
        if (!await TryAcquireLockAsync(_requestLock, ct).ConfigureAwait(false))
            return false;

        try
        {
            if (!await TryAcquireLockAsync(_writeLock, ct).ConfigureAwait(false))
                return false;

            try
            {
                DisconnectInternal();

                _controllerUniqueIds = null;

                _tcp = new TcpClient
                {
                    NoDelay = true,
                    ReceiveTimeout = 5000,
                    SendTimeout = 5000
                };

                using var connectCts =
                    CancellationTokenSource.CreateLinkedTokenSource(ct);

                connectCts.CancelAfter(TimeSpan.FromSeconds(3));

                using var registration = connectCts.Token.Register(() =>
                {
                    try
                    {
                        _tcp?.Close();
                    }
                    catch
                    {
                        // Ignore socket shutdown errors during cancellation.
                    }
                });

                await _tcp.ConnectAsync(
                    host,
                    port,
                    connectCts.Token).ConfigureAwait(false);
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] TCP Connected to {host}:{port}");

                _stream = _tcp.GetStream();

                // 1. Propose protocol version.
                const uint proposed = 6;

                await SendRawAsync(
                    0,
                    CommandRequestProtocolVersion,
                    BitConverter.GetBytes(proposed),
                    connectCts.Token).ConfigureAwait(false);

                var verReply = await ReadExpectedAsync(
                    CommandRequestProtocolVersion,
                    connectCts.Token).ConfigureAwait(false);

                ServerProtocolVersion =
                    verReply.Data.Length >= 4
                        ? Math.Min(
                            proposed,
                            BitConverter.ToUInt32(verReply.Data, 0))
                        : 0u;

                // 2. Announce client name.
                await SendRawAsync(
                    0,
                    CommandSetClientName,
                    Encoding.ASCII.GetBytes("XenRGB\0"),
                    connectCts.Token).ConfigureAwait(false);

                // 3. Drain initial server push packets.
                //
                // This happens only during connection setup and is deliberately
                // separate from the 30 FPS LED update path.
                await DrainPushPacketsAsync(
                    connectCts.Token,
                    maxWaitMs: 500).ConfigureAwait(false);

                return true;
            }
            catch
            {
                DisconnectInternal();
                return false;
            }
            finally
            {
                _writeLock.Release();
            }
        }
        finally
        {
            _requestLock.Release();
        }
    }

    public async Task<uint> GetControllerCountAsync(
        CancellationToken ct = default)
    {
        if (!await TryAcquireLockAsync(_requestLock, ct).ConfigureAwait(false))
        {
            throw new TimeoutException(
                "OpenRGB: timed out waiting for request lock in GetControllerCountAsync.");
        }

        try
        {
            using var ioCts = CreateReadIoCts(ct);

            // Request/response writes are serialized with UpdateLeds writes.
            if (!await TryAcquireLockAsync(_writeLock, ioCts.Token)
                    .ConfigureAwait(false))
            {
                throw new TimeoutException(
                    "OpenRGB: timed out waiting for write lock in GetControllerCountAsync.");
            }

            try
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] OpenRgbClient: Sending CommandRequestControllerCount");
                await SendRawAsync(
                    0,
                    CommandRequestControllerCount,
                    Array.Empty<byte>(),
                    ioCts.Token).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            // IMPORTANT:
            // We intentionally release _writeLock before reading.
            //
            // TCP is full-duplex. While this request waits for its response,
            // UpdateLeds is allowed to continue writing LED frames.
            var reply = await ReadExpectedAsync(
                CommandRequestControllerCount,
                ioCts.Token).ConfigureAwait(false);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] OpenRgbClient: Received CommandRequestControllerCount reply");

            if (reply.Data.Length < 4)
                return 0;

            uint count = BitConverter.ToUInt32(reply.Data, 0);

            // v6: response contains count + unique_id[count].
            if (ServerProtocolVersion >= 6)
            {
                int needed = checked(4 + (int)count * 4);

                if (reply.Data.Length >= needed && count > 0)
                {
                    var ids = new uint[count];

                    for (int i = 0; i < (int)count; i++)
                    {
                        ids[i] = BitConverter.ToUInt32(
                            reply.Data,
                            4 + i * 4);
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
            _requestLock.Release();
        }
    }

    public async Task<byte[]> GetControllerDataRawAsync(
        uint controllerIndex,
        CancellationToken ct = default)
    {
        if (!await TryAcquireLockAsync(_requestLock, ct).ConfigureAwait(false))
        {
            throw new TimeoutException(
                "OpenRGB: timed out waiting for request lock in GetControllerDataRawAsync.");
        }

        try
        {
            using var ioCts = CreateReadIoCts(ct);

            uint devId = GetDeviceId(controllerIndex);

            if (!await TryAcquireLockAsync(_writeLock, ioCts.Token)
                    .ConfigureAwait(false))
            {
                throw new TimeoutException(
                    "OpenRGB: timed out waiting for write lock in GetControllerDataRawAsync.");
            }

            try
            {
                await SendRawAsync(
                    devId,
                    CommandRequestControllerData,
                    BitConverter.GetBytes(ServerProtocolVersion),
                    ioCts.Token).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            // Keep request lock while reading the response so no other
            // request can consume this response.
            var reply = await ReadExpectedAsync(
                CommandRequestControllerData,
                ioCts.Token).ConfigureAwait(false);

            return reply.Data;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    /// <summary>
    /// Sends an LED update frame.
    ///
    /// This is deliberately WRITE-ONLY.
    ///
    /// There are no TCP reads here:
    /// - no DataAvailable
    /// - no Socket.Available
    /// - no ACK drain
    /// - no ReadPacketAsync
    ///
    /// OpenRGB v6 ACKs are consumed by the request/response reader during
    /// the next request operation.
    /// </summary>
    private int _clientLogCounter;

    public async Task SendUpdateLedsAsync(
        uint controllerIndex,
        ReadOnlyMemory<uint> bgrColors,
        CancellationToken ct = default)
    {
        bool isDiagFrame = Interlocked.Increment(ref _clientLogCounter) % 30 == 0;

        if (!await TryAcquireLockAsync(_writeLock, ct).ConfigureAwait(false))
            return;

        try
        {
            // IMPORTANT:
            // Do not create an artificial 5-second I/O cancellation token here.
            //
            // A partially cancelled TCP write can leave application-level
            // framing ambiguous if the same connection is reused.
            //
            // The caller's cancellation token is the only cancellation source
            // for the hot-path write.
            uint devId = GetDeviceId(controllerIndex);

            int colorCount = bgrColors.Length;

            // Payload:
            //   data_size (4)
            //   num_leds  (2)
            //   colors    (4 * N)
            int payloadSize = checked(4 + 2 + colorCount * 4);

            byte[] payload = new byte[payloadSize];

            BitConverter.TryWriteBytes(
                payload.AsSpan(0, 4),
                (uint)payloadSize);

            BitConverter.TryWriteBytes(
                payload.AsSpan(4, 2),
                (ushort)colorCount);

            ReadOnlySpan<uint> colors = bgrColors.Span;

            for (int i = 0; i < colorCount; i++)
            {
                BitConverter.TryWriteBytes(
                    payload.AsSpan(6 + i * 4, 4),
                    colors[i]);
            }

            if (isDiagFrame && colorCount > 0)
            {
                Console.WriteLine($"[DIAG] OpenRGB UpdateLeds: device={controllerIndex}, leds={colorCount}, payload={payloadSize} bytes, sample=0x{colors[0]:X8}");
            }

            await SendRawAsync(
                devId,
                CommandUpdateLeds,
                payload,
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller explicitly cancelled the frame.
            // Do not tear down the whole connection here.
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task ResizeZoneAsync(
        uint controllerIndex,
        int zoneIndex,
        int newSize,
        CancellationToken ct = default)
    {
        if (!await TryAcquireLockAsync(_writeLock, ct).ConfigureAwait(false))
            return;

        try
        {
            uint devId = GetDeviceId(controllerIndex);
            
            byte[] payload = new byte[8];
            BitConverter.TryWriteBytes(payload.AsSpan(0, 4), (uint)zoneIndex);
            BitConverter.TryWriteBytes(payload.AsSpan(4, 4), (uint)newSize);

            await SendRawAsync(
                devId,
                CommandResizeZone,
                payload,
                ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RequestRescanAsync(
        CancellationToken ct = default)
    {
        if (!IsConnected)
            return;

        if (!await TryAcquireLockAsync(_writeLock, ct).ConfigureAwait(false))
            return;

        try
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] OpenRgbClient: Sending CommandRequestRescanDevices");
            await SendRawAsync(
                0,
                CommandRequestRescanDevices,
                Array.Empty<byte>(),
                ct).ConfigureAwait(false);
        }
        catch
        {
            // Server may not support rescan.
        }
        finally
        {
            _writeLock.Release();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Read side
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads packets until the expected command is found.
    ///
    /// Known push packets, including v6 UpdateLeds ACKs, are discarded.
    ///
    /// This method must only be called while _requestLock is held.
    /// </summary>
    private async Task<(uint DeviceId, uint CommandId, byte[] Data)>
        ReadExpectedAsync(
            uint expectedCmd,
            CancellationToken ct)
    {
        const int maxSkips = 4096;

        for (int i = 0; i < maxSkips; i++)
        {
            var pkt = await ReadPacketAsync(ct).ConfigureAwait(false);

            if (pkt.CommandId == expectedCmd)
                return pkt;

            if (_pushPacketIds.Contains(pkt.CommandId))
                continue;

            return pkt;
        }

        throw new InvalidDataException(
            $"OpenRGB: expected cmd={expectedCmd} not received after {maxSkips} skips.");
    }

    /// <summary>
    /// Startup-only drain.
    ///
    /// This is not part of the 30 FPS render path.
    /// </summary>
    public async Task DrainPushPacketsAsync(
        CancellationToken ct,
        int maxWaitMs)
    {
        if (_stream == null)
            return;

        long deadline = Environment.TickCount64 + maxWaitMs;

        while (Environment.TickCount64 < deadline &&
               !ct.IsCancellationRequested)
        {
            if (_stream.DataAvailable)
            {
                try
                {
                    var pkt = await ReadPacketAsync(ct)
                        .ConfigureAwait(false);

                    if (!_pushPacketIds.Contains(pkt.CommandId))
                        break;
                }
                catch
                {
                    break;
                }
            }
            else
            {
                await Task.Delay(10, ct).ConfigureAwait(false);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TCP primitives
    // ─────────────────────────────────────────────────────────────────────────

    private async Task SendRawAsync(
        uint devId,
        uint cmdId,
        byte[] data,
        CancellationToken ct)
    {
        NetworkStream? stream = _stream;

        if (stream == null)
            throw new InvalidOperationException("Not connected");

        byte[] hdr = new byte[16];

        BitConverter.TryWriteBytes(
            hdr.AsSpan(0, 4),
            Magic);

        BitConverter.TryWriteBytes(
            hdr.AsSpan(4, 4),
            devId);

        BitConverter.TryWriteBytes(
            hdr.AsSpan(8, 4),
            cmdId);

        BitConverter.TryWriteBytes(
            hdr.AsSpan(12, 4),
            (uint)data.Length);

        await stream.WriteAsync(hdr, ct).ConfigureAwait(false);

        if (data.Length > 0)
        {
            await stream.WriteAsync(data, ct).ConfigureAwait(false);
        }

        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private async Task<(uint DeviceId, uint CommandId, byte[] Data)>
        ReadPacketAsync(CancellationToken ct)
    {
        NetworkStream? stream = _stream;

        if (stream == null)
            throw new InvalidOperationException("Not connected");

        byte[] hdr = new byte[16];

        await ReadExactAsync(
            stream,
            hdr,
            ct).ConfigureAwait(false);

        uint magic = BitConverter.ToUInt32(hdr, 0);

        if (magic != Magic)
        {
            throw new InvalidDataException(
                $"Bad OpenRGB magic: 0x{magic:X8}");
        }

        uint devId = BitConverter.ToUInt32(hdr, 4);
        uint cmdId = BitConverter.ToUInt32(hdr, 8);
        uint len = BitConverter.ToUInt32(hdr, 12);

        // Defensive protocol sanity check.
        // OpenRGB packets should not allocate arbitrary amounts of memory.
        const uint maxPacketSize = 16 * 1024 * 1024;

        if (len > maxPacketSize)
        {
            throw new InvalidDataException(
                $"OpenRGB packet too large: {len} bytes.");
        }

        byte[] data = new byte[len];

        if (len > 0)
        {
            await ReadExactAsync(
                stream,
                data,
                ct).ConfigureAwait(false);
        }

        return (devId, cmdId, data);
    }

    private static async Task ReadExactAsync(
        Stream stream,
        byte[] buffer,
        CancellationToken ct)
    {
        int read = 0;

        while (read < buffer.Length)
        {
            int n = await stream.ReadAsync(
                buffer.AsMemory(read, buffer.Length - read),
                ct).ConfigureAwait(false);

            if (n == 0)
                throw new EndOfStreamException();

            read += n;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lock helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static async Task<bool> TryAcquireLockAsync(
        SemaphoreSlim semaphore,
        CancellationToken ct)
    {
        try
        {
            using var timeoutCts =
                CancellationTokenSource.CreateLinkedTokenSource(ct);

            timeoutCts.CancelAfter(_lockTimeout);

            await semaphore.WaitAsync(
                timeoutCts.Token).ConfigureAwait(false);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private static CancellationTokenSource CreateReadIoCts(
        CancellationToken ct)
    {
        var cts =
            CancellationTokenSource.CreateLinkedTokenSource(ct);

        cts.CancelAfter(_readTimeout);

        return cts;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Connection lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public void Disconnect()
    {
        DisconnectInternal();
    }

    private void DisconnectInternal()
    {
        try
        {
            _stream?.Dispose();
        }
        catch
        {
            // Ignore disposal errors.
        }

        _stream = null;

        try
        {
            _tcp?.Dispose();
        }
        catch
        {
            // Ignore disposal errors.
        }

        _tcp = null;
        _controllerUniqueIds = null;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        DisconnectInternal();

        try
        {
            _writeLock.Dispose();
        }
        catch
        {
            // Ignore disposal errors.
        }

        try
        {
            _requestLock.Dispose();
        }
        catch
        {
            // Ignore disposal errors.
        }
    }
}