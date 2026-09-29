using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace XenRGB.SDK;

/// <summary>
/// Convenient base class for implementing XenRGB effects.
/// </summary>
public abstract class EffectBase : IEffect
{
    private readonly ConcurrentDictionary<string, object> _paramValues = new(StringComparer.OrdinalIgnoreCase);

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract EffectManifest Manifest { get; }
    public abstract IReadOnlyList<EffectParameter> Parameters { get; }

    public virtual void Initialize(IReadOnlyDictionary<string, object>? parameters = null)
    {
        _paramValues.Clear();

        // Seed with default values defined by parameters
        foreach (var def in Parameters)
        {
            _paramValues[def.Name] = def.DefaultValue;
        }

        // Apply manifest defaults if any
        if (Manifest.DefaultParameters != null)
        {
            foreach (var kvp in Manifest.DefaultParameters)
            {
                _paramValues[kvp.Key] = kvp.Value;
            }
        }

        // Apply runtime overrides
        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                _paramValues[kvp.Key] = kvp.Value;
            }
        }

        OnInitialize();
    }

    public virtual void SetParameter(string name, object value)
    {
        _paramValues[name] = value;
        OnParameterChanged(name, value);
    }

    public virtual object? GetParameter(string name)
    {
        return _paramValues.TryGetValue(name, out var val) ? val : null;
    }

    public float GetFloat(string name, float fallback = 0f)
    {
        if (_paramValues.TryGetValue(name, out var obj))
        {
            if (obj is float f) return f;
            if (obj is double d) return (float)d;
            if (obj is int i) return i;
            if (float.TryParse(obj.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    public int GetInt(string name, int fallback = 0)
    {
        if (_paramValues.TryGetValue(name, out var obj))
        {
            if (obj is int i) return i;
            if (obj is float f) return (int)f;
            if (obj is double d) return (int)d;
            if (int.TryParse(obj.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    public bool GetBool(string name, bool fallback = false)
    {
        if (_paramValues.TryGetValue(name, out var obj))
        {
            if (obj is bool b) return b;
            if (bool.TryParse(obj.ToString(), out var parsed)) return parsed;
        }
        return fallback;
    }

    public RgbColor GetColor(string name, RgbColor fallback = default)
    {
        if (_paramValues.TryGetValue(name, out var obj))
        {
            if (obj is RgbColor color) return color;
            if (obj is string str) return RgbColor.FromHex(str);
        }
        return fallback;
    }

    public virtual void Update(EffectContext context)
    {
        OnUpdate(context);
    }

    public abstract RgbColor RenderPixel(float x, float y, EffectContext context);

    public virtual void Reset()
    {
        OnReset();
    }

    protected virtual void OnInitialize() { }
    protected virtual void OnParameterChanged(string name, object value) { }
    protected virtual void OnUpdate(EffectContext context) { }
    protected virtual void OnReset() { }
}
