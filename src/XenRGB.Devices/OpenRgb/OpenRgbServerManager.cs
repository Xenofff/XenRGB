using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace XenRGB.Devices.OpenRgb;

/// <summary>
/// Small runtime service layer for managing OpenRGB SDK server.
/// </summary>
public static class OpenRgbServerManager
{
    public static async Task<bool> EnsureServerRunningAsync(string host, int port, CancellationToken ct = default)
    {
        // 1. Check if port is already open (Server already running)
        if (await IsPortOpenAsync(host, port, ct))
        {
            Debug.WriteLine("OpenRGB server already available");
            return true;
        }

        // 2. Check if process is already running but port isn't open yet
        if (IsProcessRunning("OpenRGB"))
        {
            Debug.WriteLine("OpenRGB process already running, waiting for server");
            return await WaitForPortAndDetectionAsync(host, port, TimeSpan.FromSeconds(40), ct);
        }

        // 3. Try to find and start OpenRGB process
        string exePath = FindOpenRgbExe();
        if (string.IsNullOrEmpty(exePath))
        {
            Debug.WriteLine("OpenRGB executable not found, attempting to download...");
            bool downloaded = await DownloadOpenRgbAsync(ct);
            if (!downloaded)
            {
                Debug.WriteLine("Failed to download OpenRGB automatically.");
                return false;
            }
            
            exePath = FindOpenRgbExe();
            if (string.IsNullOrEmpty(exePath))
            {
                Debug.WriteLine("OpenRGB executable still not found after download.");
                return false;
            }
        }

        Debug.WriteLine("starting OpenRGB server");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--server",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = Path.GetDirectoryName(exePath)
            };
            Process.Start(psi);
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] OpenRGB process started");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start OpenRGB: {ex.Message}");
            return false;
        }

        // 4. Wait for port AND give OpenRGB time to finish USB device detection
        Debug.WriteLine("waiting for OpenRGB SDK + device detection");
        bool ready = await WaitForPortAndDetectionAsync(host, port, TimeSpan.FromSeconds(40), ct);
        if (ready)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] OpenRGB ready (port + detection grace period complete)");
        }
        else
        {
            Debug.WriteLine("timeout/error waiting for OpenRGB server");
        }

        return ready;
    }

    private static bool IsProcessRunning(string name)
    {
        var processes = Process.GetProcessesByName(name);
        return processes.Length > 0;
    }

    private static string FindOpenRgbExe()
    {
        // Search locations
        string baseLocal = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenRGB");
        if (Directory.Exists(baseLocal))
        {
            var files = Directory.GetFiles(baseLocal, "OpenRGB.exe", SearchOption.AllDirectories);
            if (files.Length > 0)
                return files[0];
        }

        string[] searchPaths = 
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "OpenRGB", "OpenRGB.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "OpenRGB", "OpenRGB.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenRGB", "OpenRGB.exe")
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return string.Empty;
    }

    private static async Task<bool> DownloadOpenRgbAsync(CancellationToken ct)
    {
        try
        {
            string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpenRGB");
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            string zipPath = Path.Combine(targetDir, "OpenRGB.zip");
            string downloadUrl = "https://codeberg.org/OpenRGB/OpenRGB/releases/download/release_1.0/OpenRGB_1.0_Windows_64_81bbe18.zip";
            
            Debug.WriteLine($"Downloading OpenRGB from {downloadUrl}");
            using var httpClient = new System.Net.Http.HttpClient();
            var response = await httpClient.GetAsync(downloadUrl, ct);
            response.EnsureSuccessStatusCode();
            
            using (var fs = new FileStream(zipPath, FileMode.Create))
            {
                await response.Content.CopyToAsync(fs, ct);
            }
            
            Debug.WriteLine($"Extracting OpenRGB...");
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, targetDir, true);
            File.Delete(zipPath);
            
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to download OpenRGB: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> IsPortOpenAsync(string host, int port, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            var connectTask = client.ConnectAsync(host, port);
            var delayTask = Task.Delay(500, ct);
            var completed = await Task.WhenAny(connectTask, delayTask);
            
            if (completed == connectTask && client.Connected)
            {
                return true;
            }
        }
        catch
        {
            // Ignored, connection failed or refused
        }
        return false;
    }

    /// <summary>
    /// Waits for the TCP port to open, then waits an additional grace period
    /// for OpenRGB to finish USB device detection before allowing XenRGB to
    /// establish a persistent TCP connection.
    ///
    /// This grace period is critical: if a persistent SDK client connects during
    /// OpenRGB's USB detection phase, internal mutex contention or TCP backpressure
    /// blocks the detection thread, preventing HID devices (e.g. HyperX) from being found.
    /// </summary>
    private static async Task<bool> WaitForPortAndDetectionAsync(
        string host, int port, TimeSpan timeout, CancellationToken ct)
    {
        var start = DateTime.UtcNow;
        
        // Phase 1: Wait for port to become available
        while (DateTime.UtcNow - start < timeout && !ct.IsCancellationRequested)
        {
            if (await IsPortOpenAsync(host, port, ct))
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] TCP {port} became available, waiting for device detection to complete...");
                
                // Phase 2: Detection grace period.
                // OpenRGB's initial USB scan typically takes 5-15 seconds.
                // We wait up to 20 seconds, probing with brief (non-persistent) TCP connections
                // to check if devices have appeared.
                var detectionStart = DateTime.UtcNow;
                var detectionTimeout = TimeSpan.FromSeconds(20);
                
                while (DateTime.UtcNow - detectionStart < detectionTimeout && !ct.IsCancellationRequested)
                {
                    uint deviceCount = await ProbeControllerCountAsync(host, port, ct);
                    if (deviceCount > 0)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] OpenRGB reports {deviceCount} device(s), detection complete");
                        return true;
                    }
                    
                    await Task.Delay(1000, ct);
                }
                
                // Detection grace period expired — proceed anyway (devices may appear later via hotplug)
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Diagnostics] Detection grace period expired, proceeding with 0 devices");
                return true;
            }
            await Task.Delay(500, ct);
        }
        return false;
    }

    /// <summary>
    /// Opens a brief, non-persistent TCP connection to OpenRGB, sends GetControllerCount,
    /// reads the response, and immediately disconnects. This minimal probe does NOT
    /// interfere with OpenRGB's USB detection because the connection is ephemeral.
    /// </summary>
    private static async Task<uint> ProbeControllerCountAsync(string host, int port, CancellationToken ct)
    {
        try
        {
            using var tcp = new TcpClient();
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(2000);
            
            await tcp.ConnectAsync(host, port, connectCts.Token);
            var stream = tcp.GetStream();
            
            // Send protocol version request first (required handshake)
            const uint cmdProtocolVersion = 40;
            const uint cmdControllerCount = 0;
            
            // Send RequestProtocolVersion
            byte[] versionPayload = BitConverter.GetBytes((uint)6);
            await SendPacketAsync(stream, 0, cmdProtocolVersion, versionPayload, connectCts.Token);
            await ReadPacketAsync(stream, connectCts.Token); // read version reply
            
            // Send SetClientName
            const uint cmdSetClientName = 50;
            byte[] namePayload = System.Text.Encoding.ASCII.GetBytes("XenRGB-Probe\0");
            await SendPacketAsync(stream, 0, cmdSetClientName, namePayload, connectCts.Token);
            
            // Send GetControllerCount
            await SendPacketAsync(stream, 0, cmdControllerCount, Array.Empty<byte>(), connectCts.Token);
            var (_, _, data) = await ReadPacketAsync(stream, connectCts.Token);
            
            if (data.Length >= 4)
            {
                return BitConverter.ToUInt32(data, 0);
            }
        }
        catch
        {
            // Probe failed — not fatal, will retry
        }
        return 0;
    }

    private static async Task SendPacketAsync(NetworkStream stream, uint deviceId, uint commandId, byte[] payload, CancellationToken ct)
    {
        // OpenRGB packet: "ORGB" (4 bytes) + device_id (4) + command_id (4) + data_length (4) + data
        byte[] header = new byte[16 + payload.Length];
        header[0] = (byte)'O'; header[1] = (byte)'R'; header[2] = (byte)'G'; header[3] = (byte)'B';
        BitConverter.TryWriteBytes(header.AsSpan(4), deviceId);
        BitConverter.TryWriteBytes(header.AsSpan(8), commandId);
        BitConverter.TryWriteBytes(header.AsSpan(12), (uint)payload.Length);
        if (payload.Length > 0)
            payload.CopyTo(header, 16);
        
        await stream.WriteAsync(header, ct);
        await stream.FlushAsync(ct);
    }

    private static async Task<(uint DeviceId, uint CommandId, byte[] Data)> ReadPacketAsync(NetworkStream stream, CancellationToken ct)
    {
        byte[] header = new byte[16];
        int read = 0;
        while (read < 16)
        {
            int n = await stream.ReadAsync(header.AsMemory(read, 16 - read), ct);
            if (n == 0) throw new IOException("Connection closed");
            read += n;
        }
        
        uint deviceId = BitConverter.ToUInt32(header, 4);
        uint commandId = BitConverter.ToUInt32(header, 8);
        uint dataLen = BitConverter.ToUInt32(header, 12);
        
        byte[] data = Array.Empty<byte>();
        if (dataLen > 0 && dataLen < 1_000_000)
        {
            data = new byte[dataLen];
            read = 0;
            while (read < (int)dataLen)
            {
                int n = await stream.ReadAsync(data.AsMemory(read, (int)dataLen - read), ct);
                if (n == 0) throw new IOException("Connection closed");
                read += n;
            }
        }
        
        return (deviceId, commandId, data);
    }
}
