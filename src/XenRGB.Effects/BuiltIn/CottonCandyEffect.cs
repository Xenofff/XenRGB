using System;
using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.BuiltIn;

/// <summary>
/// Faithful port of SignalRGB's Cotton Candy effect by Alex Krastev.
/// Semi-transparent cyan/magenta circles drift across a multi-zone dark purple/blue background.
///
/// The original effect uses a 320×200 HTML canvas with ~20-30 translucent circles
/// (radius 40-65px, alpha 0x77 ≈ 47%) floating in random directions.
/// This port maps that behavior to XenRGB's normalized 0..1 spatial coordinate system
/// using deterministic pseudo-random positioning seeded per-circle to ensure
/// consistent visuals across all devices on the spatial canvas.
/// </summary>
public sealed class CottonCandyEffect : EffectBase
{
    // ── Circle state ──────────────────────────────────────────────────────────
    private struct Blob
    {
        public float X, Y;          // 0..320, 0..200
        public float Radius;        // 40..65
        public float VX, VY;        // Base velocity in pixels per second at speed=10
        public bool IsColor1;       
        public float Lifetime;      
    }

    // SignalRGB uses up to ~30 circles. We use a fixed pool for determinism.
    private const int MaxBlobs = 30;
    private const int InitialBlobs = 20;
    private readonly Blob[] _blobs = new Blob[MaxBlobs];
    private int _blobCount;

    // Deterministic PRNG so all LEDs see the same blob positions
    private uint _rngState;
    private float _lastTime;
    private bool _initialized;

    public override string Id => "cotton-candy";
    public override string Name => "Cotton Candy";
    public override string Description => "This effect is sure to make your setup look sweet.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "cotton-candy",
        Name = "Cotton Candy",
        Version = "3.0.0",
        Author = "Alex Krastev (SignalRGB), ported by XenRGB",
        Description = "This effect is sure to make your setup look sweet.",
        Category = "Atmospheric",
        DefaultParameters = new Dictionary<string, object>
        {
            ["bg_color_1"] = "#540074",
            ["bg_color_2"] = "#004a79",
            ["bg_color_3"] = "#730073",
            ["color_1"] = "#00FFFF",
            ["color_2"] = "#FF00FF",
            ["speed"] = 30f
        }
    };

    public override IReadOnlyList<EffectParameter> Parameters { get; } = new List<EffectParameter>
    {
        EffectParameter.Color("bg_color_1", "Background Color 1", RgbColor.FromHex("#540074"), "Primary background fill."),
        EffectParameter.Color("bg_color_2", "Background Color 2", RgbColor.FromHex("#004a79"), "Bottom-right background zone."),
        EffectParameter.Color("bg_color_3", "Background Color 3", RgbColor.FromHex("#730073"), "Top-left background zone."),
        EffectParameter.Color("color_1", "Color 1", RgbColor.FromHex("#00FFFF"), "Primary blob color (Cyan)."),
        EffectParameter.Color("color_2", "Color 2", RgbColor.FromHex("#FF00FF"), "Secondary blob color (Magenta)."),
        EffectParameter.Float("speed", "Speed", 30f, 0f, 100f, 1f, "Animation speed (0–100).")
    };

    // ── Deterministic PRNG (xorshift32) ───────────────────────────────────────
    private float NextFloat()
    {
        _rngState ^= _rngState << 13;
        _rngState ^= _rngState >> 17;
        _rngState ^= _rngState << 5;
        return (_rngState & 0x7FFFFFFFu) / (float)0x7FFFFFFF;
    }

    private int NextInt(int max)
    {
        return (int)(NextFloat() * max) % max;
    }

    // ── Initialization ────────────────────────────────────────────────────────
    protected override void OnInitialize()
    {
        _rngState = 0xDEADBEEF;
        _blobCount = 0;
        _lastTime = 0f;
        _initialized = false;

        // Spawn initial blobs randomly across the canvas (like SignalRGB's InitFill)
        for (int i = 0; i < InitialBlobs && _blobCount < MaxBlobs; i++)
        {
            SpawnInitBlob();
        }
        _initialized = true;
    }

    private void SpawnInitBlob()
    {
        var b = new Blob
        {
            X = NextFloat() * 320f,
            Y = NextFloat() * 200f,
            Radius = NextFloat() * 25f + 40f,
            IsColor1 = NextInt(3) != 1,
            Lifetime = 1f
        };

        // In SignalRGB: speed per frame is 0.1 * (speed/10).
        // At 60 FPS, base speed (when speed/10 is 1) is 6.0 pixels/sec.
        int dir = NextInt(4);
        float baseSpeed = 6.0f;
        switch (dir)
        {
            case 0: b.VX = -baseSpeed; b.VY = 0; break;
            case 1: b.VX = baseSpeed; b.VY = 0; break;
            case 2: b.VX = 0; b.VY = baseSpeed; break;
            default: b.VX = 0; b.VY = -baseSpeed; break;
        }

        if (_blobCount < MaxBlobs)
            _blobs[_blobCount++] = b;
    }

    // Spawn from right edge, drifting left (like SignalRGB's Fill)
    private void SpawnRightBlob()
    {
        var b = new Blob
        {
            X = 320f + NextFloat() * 50f,
            Y = NextFloat() * 20f,
            Radius = NextFloat() * 25f + 40f,
            IsColor1 = NextInt(3) != 1,
            Lifetime = 1f,
            VX = -6.0f,
            VY = (NextInt(2) == 1 ? 1f : -1f) * 6.0f * 0.5f * NextFloat() // average of Math.random() per frame
        };

        if (_blobCount < MaxBlobs)
            _blobs[_blobCount++] = b;
    }

    // Spawn from left edge, drifting right (like SignalRGB's Fill2)
    private void SpawnLeftBlob()
    {
        var b = new Blob
        {
            X = NextFloat() * 50f - 75f,
            Y = NextFloat() * 200f,
            Radius = NextFloat() * 25f + 40f,
            IsColor1 = NextInt(3) != 1,
            Lifetime = 1f,
            VX = 6.0f,
            VY = (NextInt(2) == 1 ? 1f : -1f) * 6.0f * 0.5f * NextFloat()
        };

        if (_blobCount < MaxBlobs)
            _blobs[_blobCount++] = b;
    }

    // ── Frame update (called once per engine tick) ────────────────────────────
    public override void Update(EffectContext context)
    {
        if (!_initialized)
        {
            OnInitialize();
        }

        float speedMult = GetFloat("speed", 30f) / 10f;
        float dt = context.DeltaTime;

        // Move existing blobs
        for (int i = 0; i < _blobCount; i++)
        {
            _blobs[i].X += _blobs[i].VX * speedMult * dt;
            _blobs[i].Y += _blobs[i].VY * speedMult * dt;

            // Kill blobs that drifted far off-canvas (SignalRGB bounds: -100..450, -100..300)
            if (_blobs[i].X > 450f || _blobs[i].X < -100f ||
                _blobs[i].Y > 300f || _blobs[i].Y < -100f)
            {
                _blobs[i].Lifetime = 0;
            }
        }

        // Remove dead blobs (compact array)
        int writeIdx = 0;
        for (int i = 0; i < _blobCount; i++)
        {
            if (_blobs[i].Lifetime > 0)
            {
                if (writeIdx != i)
                    _blobs[writeIdx] = _blobs[i];
                writeIdx++;
            }
        }
        _blobCount = writeIdx;

        // Spawn new blobs to maintain population (like original: if fills.length < 30)
        // Use a seeded PRNG based on time for deterministic spawning
        _rngState = (uint)(context.TotalTime * 1000) ^ 0xCAFEBABE;
        while (_blobCount < MaxBlobs)
        {
            SpawnRightBlob();
            if (_blobCount < MaxBlobs)
                SpawnLeftBlob();
        }

        _lastTime = context.TotalTime;
        OnUpdate(context);
    }

    // ── Per-LED evaluation ────────────────────────────────────────────────────
    public override RgbColor Evaluate(Point2D position, double time)
    {
        var bg1 = GetColor("bg_color_1", RgbColor.FromHex("#540074"));
        var bg2 = GetColor("bg_color_2", RgbColor.FromHex("#004a79"));
        var bg3 = GetColor("bg_color_3", RgbColor.FromHex("#730073"));
        var col1 = GetColor("color_1", RgbColor.FromHex("#00FFFF"));
        var col2 = GetColor("color_2", RgbColor.FromHex("#FF00FF"));

        // ── Background ───────────────────────────────────────────────────────
        // SignalRGB draws 3 overlapping rectangles:
        //   bg1: full canvas (0,0)-(320,200)
        //   bg2: (200,75)-(320,200)  → normalized: x > 0.625, y > 0.375
        //   bg3: (0,0)-(100,100)     → normalized: x < 0.3125, y < 0.5
        //
        // Since there's no alpha blending on the rects, later rects overwrite.
        // bg2 is drawn AFTER bg1, bg3 is drawn AFTER bg2.
        // So priority: bg3 (if in zone) > bg2 (if in zone) > bg1

        // Map normalized XenRGB coords (0..1) to SignalRGB canvas (0..320, 0..200)
        float px = position.X * 320f;
        float py = position.Y * 200f;

        RgbColor bg;
        if (px < 100f && py < 100f)
        {
            bg = bg3;  // top-left zone
        }
        else if (px > 200f && py > 75f)
        {
            bg = bg2;  // bottom-right zone
        }
        else
        {
            bg = bg1;  // everything else
        }

        // ── Blobs (translucent circles) ───────────────────────────────────────
        // Each circle is drawn with hex alpha "77" = 0x77 = 119/255 ≈ 46.7% opacity.
        // Multiple overlapping circles accumulate via standard alpha compositing.
        const float blobAlpha = 119f / 255f;  // 0x77

        float rOut = bg.R / 255f;
        float gOut = bg.G / 255f;
        float bOut = bg.B / 255f;

        for (int i = 0; i < _blobCount; i++)
        {
            ref readonly var blob = ref _blobs[i];
            if (blob.Lifetime <= 0) continue;

            float dx = px - blob.X;
            float dy = py - blob.Y;
            float distSq = dx * dx + dy * dy;
            float rSq = blob.Radius * blob.Radius;

            if (distSq < rSq)
            {
                // Inside the circle — apply alpha composite (source-over)
                var blobColor = blob.IsColor1 ? col1 : col2;
                float srcR = blobColor.R / 255f;
                float srcG = blobColor.G / 255f;
                float srcB = blobColor.B / 255f;

                rOut = srcR * blobAlpha + rOut * (1f - blobAlpha);
                gOut = srcG * blobAlpha + gOut * (1f - blobAlpha);
                bOut = srcB * blobAlpha + bOut * (1f - blobAlpha);
            }
        }

        return new RgbColor(
            (byte)Math.Clamp(rOut * 255f + 0.5f, 0f, 255f),
            (byte)Math.Clamp(gOut * 255f + 0.5f, 0f, 255f),
            (byte)Math.Clamp(bOut * 255f + 0.5f, 0f, 255f)
        );
    }

    public override RgbColor RenderPixel(float x, float y, EffectContext context)
    {
        return Evaluate(new Point2D(x, y), context.TotalTime);
    }
}
