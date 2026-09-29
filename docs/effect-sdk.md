# XenRGB Effect SDK Documentation

The **XenRGB Effect SDK** allows developers and community members to write lightweight, modern RGB lighting effects in C#.

Unlike proprietary RGB ecosystems that lock effects behind compiled binary DLLs or web-view wrappers, **XenRGB effects are pure source code**. Every effect consists of an `effect.cs` file and a `manifest.json` metadata file.

---

## 1. Directory Structure

Each effect is placed inside its own directory (e.g. in `effects/` or in `%APPDATA%\XenRGB\effects\`):

```text
CottonCandy/
├── effect.cs         # C# source code implementing IEffect or extending EffectBase
├── manifest.json     # Metadata and default parameter values
└── README.md         # Description and parameter documentation
```

When XenRGB launches or when you click **"Reload Effects"** in the app, XenRGB automatically parses and compiles your source code using Microsoft Roslyn directly in memory.

---

## 2. Manifest Schema (`manifest.json`)

```json
{
  "id": "my-effect",
  "name": "My Custom Effect",
  "version": "1.0.0",
  "author": "YourName",
  "description": "A smooth animated wave flowing across the spatial canvas.",
  "category": "Animated",
  "defaultParameters": {
    "speed": 1.0,
    "brightness": 1.0,
    "accentColor": "#00A2FF"
  }
}
```

---

## 3. Implementing an Effect (`effect.cs`)

Inheriting from `XenRGB.SDK.EffectBase` is the simplest way to build an effect:

```csharp
using System;
using System.Collections.Generic;
using XenRGB.SDK;

public class MyCustomEffect : EffectBase
{
    public override string Id => "my-effect";
    public override string Name => "My Custom Effect";
    public override string Description => "A smooth animated wave flowing across the spatial canvas.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "my-effect",
        Name = "My Custom Effect",
        Version = "1.0.0",
        Author = "YourName"
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Float("speed", "Speed", defaultValue: 1.0f, min: 0.1f, max: 5.0f, step: 0.1f),
        EffectParameter.Float("brightness", "Brightness", defaultValue: 1.0f, min: 0.0f, max: 1.0f, step: 0.05f),
        EffectParameter.Color("accentColor", "Accent Color", defaultValue: RgbColor.FromHex("#00A2FF"))
    };

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        // 1. Read parameter values (type-safe helpers)
        float speed = GetFloat("speed", 1.0f);
        float brightness = GetFloat("brightness", 1.0f);
        RgbColor accent = GetColor("accentColor", RgbColor.FromHex("#00A2FF"));

        // 2. Compute color from 2D coordinates (x: 0..1, y: 0..1) and time
        float wave = MathF.Sin((x + context.TotalTime * speed) * MathF.PI * 2f);
        float factor = (wave + 1f) * 0.5f;

        // 3. Return interpolated color with brightness applied
        return accent.WithBrightness(factor * brightness);
    }
}
```

---

## 4. Key SDK Types

### `RgbColor`
A lightweight 24-bit RGB struct (`byte R`, `byte G`, `byte B`).
- `RgbColor.FromRgb(byte r, byte g, byte b)`
- `RgbColor.FromHsv(float hue, float saturation, float value)`
- `RgbColor.FromHex("#RRGGBB")`
- `RgbColor.Lerp(RgbColor a, RgbColor b, float t)`
- `color.WithBrightness(float factor)`
- `color.Blend(RgbColor overlay, float alpha)`

### `EffectContext`
Passed to `Update(context)` and `RenderPixel(x, y, context)` on every frame:
- `context.DeltaTime`: Seconds elapsed since the previous frame.
- `context.TotalTime`: Total elapsed seconds of the effect session.
- `context.FrameIndex`: Monotonically increasing frame counter.

### `MathUtils`
- `MathUtils.Perlin2D(float x, float y)`: Procedural 2D Perlin noise in `[-1, 1]`.
- `MathUtils.Perlin2DNormalized(float x, float y)`: Procedural 2D Perlin noise in `[0, 1]`.
- `MathUtils.SmoothStep(float edge0, float edge1, float x)`: Smooth Hermite interpolation.
- `MathUtils.SineWave(float time, float speed, float frequency, float phase)`

---

## 5. Live Testing and Hot-Reloading

1. Open XenRGB and go to the **Effects** tab.
2. Click **"Open Effects Folder"**.
3. Create a new folder with your `manifest.json` and `effect.cs`.
4. Return to XenRGB and click **"Reload Effects"**.
5. Your effect will immediately compile and appear in the gallery. If there are syntax errors, XenRGB will report the exact line number and compilation diagnostic!
