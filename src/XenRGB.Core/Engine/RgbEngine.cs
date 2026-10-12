using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XenRGB.Core.Canvas;
using XenRGB.Core.Devices;
using XenRGB.SDK;

namespace XenRGB.Core.Engine;

/// <summary>
/// Core rendering and distribution engine for XenRGB.
/// Operates at zero CPU overhead when lighting is turned off.
/// </summary>
public sealed class RgbEngine : IDisposable
{
    private readonly SpatialCanvas _canvas;
    private readonly List<IRgbDevice> _devices = new();
    private IRgbDevice[] _deviceSnapshot = Array.Empty<IRgbDevice>();
    private readonly Dictionary<string, RgbColor[]> _deviceBuffers = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private IEffect? _activeEffect;
    private float _masterBrightness = 1.0f;
    private bool _isLightingEnabled = true;
    private int _targetFps = 15;

    private Thread? _renderThread;
    private readonly CancellationTokenSource _cts = new();
    private readonly ManualResetEventSlim _wakeEvent = new(true);
    private bool _isDisposed;

    // Frame metrics
    public long RenderedFrames { get; private set; }
    public float CurrentFps { get; private set; }
    public bool IsLightingEnabled => _isLightingEnabled;

    public float MasterBrightness
    {
        get => _masterBrightness;
        set => _masterBrightness = Math.Clamp(value, 0f, 1f);
    }

    public int TargetFps
    {
        get => _targetFps;
        set => _targetFps = Math.Clamp(value, 10, 120);
    }

    public IEffect? ActiveEffect
    {
        get => _activeEffect;
        set
        {
            lock (_lock)
            {
                _activeEffect = value;
                _activeEffect?.Reset();
                Console.WriteLine($"[DIAG] Effect selected: {_activeEffect?.GetType().Name ?? "null"}");
            }
        }
    }

    public RgbEngine(SpatialCanvas canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
    }

    public void Start()
    {
        if (_renderThread != null)
            return;

        PowerEfficiencyHelper.EnableEcoQoS();

        _renderThread = new Thread(RenderLoop)
        {
            Name = "XenRGB.EngineLoop",
            IsBackground = true,
            Priority = ThreadPriority.Lowest
        };
        _renderThread.Start();
    }

    public void RegisterDevice(IRgbDevice device)
    {
        lock (_lock)
        {
            if (!_devices.Contains(device))
            {
                _devices.Add(device);
                _deviceSnapshot = _devices.ToArray();
            }
        }
    }

    public void UnregisterDevice(string deviceId)
    {
        lock (_lock)
        {
            _devices.RemoveAll(d => d.Id == deviceId);
            _deviceBuffers.Remove(deviceId);
            _deviceSnapshot = _devices.ToArray();
        }
    }

    public void SetLightingEnabled(bool enabled)
    {
        lock (_lock)
        {
            if (_isLightingEnabled == enabled)
                return;

            _isLightingEnabled = enabled;

            if (_isLightingEnabled)
            {
                foreach (var device in _devices)
                {
                    _ = device.SetPowerAsync(true);
                }

                // Wake up engine thread immediately
                _wakeEvent.Set();
            }
            else
            {
                _wakeEvent.Reset();
                // Send black frame to all devices immediately
                SendBlackFrameToAll();
            }
        }
    }

    private void SendBlackFrameToAll()
    {
        IRgbDevice[] currentDevices;
        lock (_lock)
        {
            currentDevices = _deviceSnapshot;
        }

        foreach (var device in currentDevices)
        {
            if (device.ConnectionState != DeviceConnectionState.Connected)
                continue;

            try
            {
                var blackArray = new RgbColor[device.LedCount];
                Array.Fill(blackArray, RgbColor.Black);

                _ = device.UpdateLedsAsync(blackArray);
                _ = device.SetPowerAsync(false);
            }
            catch
            {
                // Ignore errors during power off
            }
        }
    }

    private void RenderLoop()
    {
        PowerEfficiencyHelper.EnableThreadEcoQoS();

        var sw = Stopwatch.StartNew();
        var fpsSw = Stopwatch.StartNew();
        long fpsFrameCount = 0;
        float lastTotalSeconds = 0f;

        while (!_cts.IsCancellationRequested)
        {
            // If lighting is disabled, keep devices off and send keepalive black frames every 1000ms.
            // This prevents keyboard watchdogs from reverting to default hardware effects, while consuming virtually 0% CPU.
            if (!_isLightingEnabled)
            {
                bool woken = _wakeEvent.Wait(1000, _cts.Token);
                if (woken)
                {
                    sw.Restart();
                    lastTotalSeconds = 0f;
                    continue;
                }

                SendBlackFrameToAll();
                continue;
            }

            float currentSeconds = (float)sw.Elapsed.TotalSeconds;
            float deltaTime = currentSeconds - lastTotalSeconds;
            lastTotalSeconds = currentSeconds;

            if (deltaTime <= 0f) deltaTime = 0.001f;

            var context = new EffectContext(deltaTime, currentSeconds, RenderedFrames);

            RenderFrame(context);

            RenderedFrames++;
            fpsFrameCount++;

            if (fpsSw.ElapsedMilliseconds >= 1000)
            {
                CurrentFps = fpsFrameCount * 1000f / fpsSw.ElapsedMilliseconds;
                fpsFrameCount = 0;
                fpsSw.Restart();
            }

            // Frame rate limit — use WaitableTimerEx with tolerance so the OS
            // does NOT raise the global timer resolution to ~1 ms.
            // Thread.Sleep() would prevent CPU C-state transitions (C3/C6+),
            // keeping the CPU frequency at turbo levels (2.4–3.5 GHz) even at 0% load.
            // Tolerance = half the frame interval: at 15 FPS (66 ms/frame) we use 32 ms.
            // This allows the OS to batch this timer with other activity and keep the CPU
            // in C6 for longer stretches, reducing power from 16-20W to 6-8W target.
            long targetDelayMs = 1000L / _targetFps;
            long elapsedFrameMs = (long)((sw.Elapsed.TotalSeconds - currentSeconds) * 1000.0);
            long sleepTime = targetDelayMs - elapsedFrameMs;

            if (sleepTime > 2)
            {
                uint toleranceMs = (uint)Math.Min(sleepTime / 2, 32);
                WaitableTimer.SleepWithTolerance(sleepTime, toleranceMs);
            }
        }
    }

    private void RenderFrame(EffectContext context)
    {
        IEffect? effect;
        IRgbDevice[] devices;
        float masterBrightness;

        lock (_lock)
        {
            if (!_isLightingEnabled)
                return;

            effect = _activeEffect;
            devices = _deviceSnapshot;
            masterBrightness = _masterBrightness;
        }

        if (effect == null || devices.Length == 0 || masterBrightness <= 0.001f)
            return;

        // Update effect animation state once per frame
        effect.Update(context);

        // Render each device
        for (int d = 0; d < devices.Length; d++)
        {
            var device = devices[d];
            if (!device.IsEnabled || device.ConnectionState != DeviceConnectionState.Connected)
                continue;

            var mappedLeds = _canvas.GetMappedLeds(device);
            if (mappedLeds.Length == 0)
                continue;

            int count = Math.Min(device.LedCount, mappedLeds.Length);

            // Reusable per-device buffer
            if (!_deviceBuffers.TryGetValue(device.Id, out var colors) || colors.Length < count)
            {
                colors = new RgbColor[Math.Max(count, device.LedCount)];
                _deviceBuffers[device.Id] = colors;
            }

            float deviceBrightness = device.Brightness * masterBrightness;

            for (int i = 0; i < count; i++)
            {
                var pt = mappedLeds[i].CanvasPoint;
                var rawColor = effect.Evaluate(pt, context.TotalTime);

                colors[i] = deviceBrightness < 0.999f
                    ? rawColor.WithBrightness(deviceBrightness)
                    : rawColor;
            }

            // Push frame to device asynchronously using struct ReadOnlyMemory slice
            _ = device.UpdateLedsAsync(new ReadOnlyMemory<RgbColor>(colors, 0, count));
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _cts.Cancel();
        _wakeEvent.Set();
        _renderThread?.Join(500);

        _wakeEvent.Dispose();
        _cts.Dispose();
    }
}
