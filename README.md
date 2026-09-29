# XenRGB

A lightweight, modern, open-source RGB lighting controller for Windows.

XenRGB is designed as a small, fast, clean, and extensible alternative to heavyweight commercial RGB software. It focuses on what actually matters: low resource consumption, OpenRGB hardware support, WLED Wi-Fi devices, a 2D spatial canvas, and an open-source C# effect SDK.

---

## Features

- **Native Windows & WinUI 3**: Built with modern C# and .NET 9. Native Fluent Design with Mica backdrop and dark mode. Zero Electron, zero Chromium, zero web wrappers.
- **Ultra-Low Idle Resource Usage**: When RGB lighting is turned off, animation threads pause completely, resulting in **0.0% CPU usage** and minimal memory consumption.
- **OpenRGB Integration**: Directly connects to the OpenRGB SDK network service (port 6742) to drive motherboards, RAM, GPUs, keyboards, and mice without vendor-specific bloat.
- **WLED Wi-Fi Integration**: Communicates with ESP8266/ESP32 WLED strips over the local network via low-latency realtime UDP streaming (DRGB port 21324) and HTTP JSON API.
- **2D Spatial Desktop Canvas**: Arrange physical RGB devices on a logical 2D canvas. Effects operate in unified spatial coordinates so colors and animations flow seamlessly across devices (e.g. from keyboard to desk LED strip).
- **Extensible Open-Source Effect SDK**: Effects are written as human-readable C# source files (`effect.cs` + `manifest.json`). XenRGB dynamically compiles effects using Roslyn at runtime.
- **Display & Inactivity Automation**:
  - Automatically turns off lighting when displays turn off or sleep.
  - Configurable inactivity timeout (e.g., 15 minutes).
  - Automatically restores lighting when displays wake up.
- **Instant Controls & Good Night**:
  - **Turn On**: Immediately restores lighting.
  - **Turn Off**: Disables all lighting.
  - **Good Night**: Shuts off all lighting while keeping the PC running 24/7.
- **Windows System Tray**: Native tray icon with quick actions (Turn On, Turn Off, Good Night, Open, Exit) and minimize-to-tray support.

---

## Built-In Effects

1. **Cotton Candy**: Smooth, slow-flowing blue, purple, and pastel pink waves.
2. **Plain Color**: Configurable solid color and brightness across all devices.
3. **Biohazard**: Radioactive green pulses and organic bio-luminescent surges.
4. **Rainbow**: Continuous spectral rainbow wave sweeping across the canvas.

---

## Solution Architecture

```text
XenRGB/
├── src/
│   ├── XenRGB.SDK/       # Pure contracts, RgbColor, Point2D, MathUtils, IEffect
│   ├── XenRGB.Core/      # Device abstractions, Spatial Canvas, Automation, Config, RgbEngine
│   ├── XenRGB.Devices/   # OpenRGB, WLED (UDP/HTTP), and Virtual Simulator providers
│   ├── XenRGB.Effects/   # Built-in effects and Roslyn runtime C# compiler/loader
│   └── XenRGB.App/       # WinUI 3 desktop application & System Tray integration
│
├── effects/              # Drop-in open-source effect packages
│   ├── PlainColor/
│   ├── CottonCandy/
│   ├── Biohazard/
│   └── Rainbow/
│
├── tests/
│   └── XenRGB.Tests/     # Comprehensive unit tests (25/25 passing)
│
├── docs/
│   └── effect-sdk.md     # Effect developer documentation
│
└── README.md
```

---

## Prerequisites

- Windows 10 (version 1809+) or Windows 11 (64-bit)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

---

## Building and Running

### 1. Build the Solution
```powershell
dotnet build XenRGB.sln
```

### 2. Run All Unit Tests
```powershell
dotnet test
```

### 3. Run the XenRGB Application
```powershell
dotnet run --project src/XenRGB.App/XenRGB.App.csproj
```

Or run the built executable directly:
```text
src\XenRGB.App\bin\Debug\net9.0-windows10.0.26100.0\win-x64\XenRGB.App.exe
```

---

## Creating Custom Effects

Check out the [Effect SDK Guide](docs/effect-sdk.md) for full details.

To add a new effect:
1. Open XenRGB and go to the **Effects** tab.
2. Click **"Open Effects Folder"**.
3. Create a folder with your `manifest.json` and `effect.cs`:
   ```csharp
   using System;
   using System.Collections.Generic;
   using XenRGB.SDK;

   public class CustomWaveEffect : EffectBase
   {
       public override string Id => "custom-wave";
       public override string Name => "Custom Wave";
       public override string Description => "A custom wave effect.";
       public override EffectManifest Manifest => new() { Id = "custom-wave", Name = "Custom Wave" };
       public override IReadOnlyList<EffectParameter> Parameters => new List<EffectParameter>();

       public override RgbColor RenderPixel(float x, float y, EffectContext context)
       {
           float wave = (MathF.Sin(x * 6f + context.TotalTime * 2f) + 1f) * 0.5f;
           return RgbColor.FromHsv(wave * 360f, 1f, 1f);
       }
   }
   ```
4. Click **"Reload Effects"** in the app. Your effect is compiled and available immediately!

---

## License

MIT License. Open-source and free forever.
