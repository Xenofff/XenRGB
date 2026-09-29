using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using XenRGB.Core.Devices;
using XenRGB.SDK;

namespace XenRGB.Core.Canvas;

public readonly struct MappedLed
{
    public int LedIndex { get; }
    public Point2D CanvasPoint { get; }

    public MappedLed(int ledIndex, Point2D canvasPoint)
    {
        LedIndex = ledIndex;
        CanvasPoint = canvasPoint;
    }
}

/// <summary>
/// Manages device placements in 2D virtual space and maps physical LEDs to normalized canvas coordinates.
/// </summary>
public sealed class SpatialCanvas
{
    private readonly ConcurrentDictionary<string, CanvasDevicePlacement> _placements = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, MappedLed[]> _cache = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler? CanvasChanged;

    public IReadOnlyList<CanvasDevicePlacement> Placements => _placements.Values.ToList();

    public void AddOrUpdatePlacement(CanvasDevicePlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        _placements[placement.DeviceId] = placement;
        _cache.TryRemove(placement.DeviceId, out _);
        CanvasChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool RemovePlacement(string deviceId)
    {
        if (_placements.TryRemove(deviceId, out _))
        {
            _cache.TryRemove(deviceId, out _);
            CanvasChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return false;
    }

    public CanvasDevicePlacement? GetPlacement(string deviceId)
    {
        return _placements.TryGetValue(deviceId, out var p) ? p : null;
    }

    public void Clear()
    {
        _placements.Clear();
        _cache.Clear();
        CanvasChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Computes and caches canvas coordinates for each LED on the specified device.
    /// </summary>
    public MappedLed[] GetMappedLeds(IRgbDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (_cache.TryGetValue(device.Id, out var cached))
            return cached;

        if (!_placements.TryGetValue(device.Id, out var placement) || !placement.IsEnabled)
        {
            // Device has no placement or is disabled on canvas: return empty
            return Array.Empty<MappedLed>();
        }

        var leds = device.LedCoordinates;
        var mapped = new MappedLed[leds.Count];

        for (int i = 0; i < leds.Count; i++)
        {
            var coord = leds[i];
            var canvasPt = placement.TransformLocalToCanvas(coord.LocalX, coord.LocalY);
            mapped[i] = new MappedLed(coord.Index, canvasPt);
        }

        _cache[device.Id] = mapped;
        return mapped;
    }

    /// <summary>
    /// Loads an existing list of placements, replacing current placements.
    /// </summary>
    public void LoadPlacements(IEnumerable<CanvasDevicePlacement> placements)
    {
        _placements.Clear();
        _cache.Clear();
        foreach (var p in placements)
        {
            _placements[p.DeviceId] = p;
        }
        CanvasChanged?.Invoke(this, EventArgs.Empty);
    }
}
