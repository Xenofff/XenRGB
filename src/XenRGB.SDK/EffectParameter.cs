using System;

namespace XenRGB.SDK;

public enum EffectParameterType
{
    Float,
    Int,
    Boolean,
    Color,
    String
}

/// <summary>
/// Defines a configurable parameter for an RGB effect.
/// </summary>
public sealed class EffectParameter
{
    public string Name { get; }
    public string DisplayName { get; }
    public EffectParameterType Type { get; }
    public object DefaultValue { get; }
    public float? MinValue { get; }
    public float? MaxValue { get; }
    public float? Step { get; }
    public string? Description { get; }

    public EffectParameter(
        string name,
        string displayName,
        EffectParameterType type,
        object defaultValue,
        float? minValue = null,
        float? maxValue = null,
        float? step = null,
        string? description = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DisplayName = displayName ?? name;
        Type = type;
        DefaultValue = defaultValue;
        MinValue = minValue;
        MaxValue = maxValue;
        Step = step;
        Description = description;
    }

    public static EffectParameter Float(string name, string displayName, float defaultValue, float min = 0f, float max = 1f, float step = 0.05f, string? description = null) =>
        new(name, displayName, EffectParameterType.Float, defaultValue, min, max, step, description);

    public static EffectParameter Int(string name, string displayName, int defaultValue, int min = 0, int max = 100, string? description = null) =>
        new(name, displayName, EffectParameterType.Int, defaultValue, min, max, 1f, description);

    public static EffectParameter Bool(string name, string displayName, bool defaultValue, string? description = null) =>
        new(name, displayName, EffectParameterType.Boolean, defaultValue, description: description);

    public static EffectParameter Color(string name, string displayName, RgbColor defaultValue, string? description = null) =>
        new(name, displayName, EffectParameterType.Color, defaultValue, description: description);
}
