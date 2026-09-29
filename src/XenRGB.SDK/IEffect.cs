using System;
using System.Collections.Generic;

namespace XenRGB.SDK;

/// <summary>
/// Common contract for all XenRGB effects (both built-in and external community effects).
/// </summary>
public interface IEffect
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    EffectManifest Manifest { get; }
    IReadOnlyList<EffectParameter> Parameters { get; }

    /// <summary>
    /// Initializes or re-initializes effect with parameter values.
    /// </summary>
    void Initialize(IReadOnlyDictionary<string, object>? parameters = null);

    /// <summary>
    /// Sets an individual parameter by name.
    /// </summary>
    void SetParameter(string name, object value);

    /// <summary>
    /// Gets the current value of a parameter.
    /// </summary>
    object? GetParameter(string name);

    /// <summary>
    /// Updates effect internal time/state once per frame before rendering LEDs.
    /// </summary>
    void Update(EffectContext context);

    /// <summary>
    /// Evaluates the RGB color at normalized 2D canvas coordinates (Point2D: 0..1, 0..1) at the given time in seconds.
    /// </summary>
    RgbColor Evaluate(Point2D position, double time);

    /// <summary>
    /// Evaluates the RGB color at normalized 2D canvas coordinates (x: 0..1, y: 0..1).
    /// </summary>
    RgbColor RenderPixel(float x, float y, EffectContext context);

    /// <summary>
    /// Resets animation state and timers.
    /// </summary>
    void Reset();
}
