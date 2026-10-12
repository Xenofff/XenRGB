using System;
using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.BuiltIn;

/// <summary>
/// Faithful port of SignalRGB's Cotton Candy effect by Alex Krastev.
/// Semi-transparent cyan/magenta clouds drift seamlessly across a multi-zone dark purple/blue background.
///
/// Features smooth continuous particle recycling from all 4 canvas boundaries and soft
/// quadratic edge falloff for continuous, organic, non-jerking ambient lighting.
/// </summary>
public sealed class CottonCandyEffect : EffectBase
{
    private struct Blob
    {
        public float X, Y;          // 0..320, 0..200
        public float Radius;        // 50..75px
        public float VX, VY;        // Velocity in pixels/sec
        public bool IsColor1;
    }

    private const int MaxBlobs = 40;
    private readonly Blob[] _blobs = new Blob[MaxBlobs];
    private uint _rngState = 0xDEADBEEF;
    private float _lastTime;
    private bool _initialized;

    public override string Id => "cotton-candy";
    public override string Name => "Cotton Candy";
    public override string Description => "This effect is sure to make your setup look sweet.";

    public override EffectManifest Manifest { get; } = new()
    {
        Id = "cotton-candy",
        Name = "Cotton Candy",
        Version = "3.1.0",
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
        if (_rngState == 0) _rngState = 0xDEADBEEF;
        return (_rngState & 0x7FFFFFFFu) / (float)0x7FFFFFFF;
    }

    private int NextInt(int max) => (int)(NextFloat() * max) % max;

    protected override void OnInitialize()
    {
        _rngState = 0xDEADBEEF;
        _lastTime = 0f;

        for (int i = 0; i < MaxBlobs; i++)
        {
            SpawnBlob(i, randomOnCanvas: true);
        }
        _initialized = true;
    }

    protected override void OnReset()
    {
        OnInitialize();
    }

    private void SpawnBlob(int index, bool randomOnCanvas)
    {
        float x, y, vx, vy;
        float baseSpeed = 7.0f;
        int dir = NextInt(4);

        if (randomOnCanvas)
        {
            x = NextFloat() * 320f;
            y = NextFloat() * 200f;
            switch (dir)
            {
                case 0: vx = -baseSpeed; vy = (NextFloat() - 0.5f) * 4f; break;
                case 1: vx = baseSpeed; vy = (NextFloat() - 0.5f) * 4f; break;
                case 2: vx = (NextFloat() - 0.5f) * 4f; vy = -baseSpeed; break;
                default: vx = (NextFloat() - 0.5f) * 4f; vy = baseSpeed; break;
            }
        }
        else
        {
            // Spawn seamlessly from one of the 4 outer canvas boundaries
            switch (dir)
            {
                case 0: // Right edge -> moving Left
                    x = 320f + NextFloat() * 30f;
                    y = NextFloat() * 200f;
                    vx = -baseSpeed;
                    vy = (NextFloat() - 0.5f) * 4f;
                    break;
                case 1: // Left edge -> moving Right
                    x = -NextFloat() * 30f;
                    y = NextFloat() * 200f;
                    vx = baseSpeed;
                    vy = (NextFloat() - 0.5f) * 4f;
                    break;
                case 2: // Bottom edge -> moving Up
                    x = NextFloat() * 320f;
                    y = 200f + NextFloat() * 30f;
                    vx = (NextFloat() - 0.5f) * 4f;
                    vy = -baseSpeed;
                    break;
                default: // Top edge -> moving Down
                    x = NextFloat() * 320f;
                    y = -NextFloat() * 30f;
                    vx = (NextFloat() - 0.5f) * 4f;
                    vy = baseSpeed;
                    break;
            }
        }

        _blobs[index] = new Blob
        {
            X = x,
            Y = y,
            Radius = NextFloat() * 25f + 50f, // 50..75px
            IsColor1 = NextInt(3) != 1,
            VX = vx,
            VY = vy
        };
    }

    public override void Update(EffectContext context)
    {
        if (!_initialized)
        {
            OnInitialize();
        }

        float speedMult = GetFloat("speed", 30f) / 10f;
        float dt = (_lastTime > 0f && context.TotalTime > _lastTime)
            ? Math.Clamp(context.TotalTime - _lastTime, 0.001f, 0.1f)
            : Math.Clamp(context.DeltaTime, 0.001f, 0.1f);

        for (int i = 0; i < MaxBlobs; i++)
        {
            _blobs[i].X += _blobs[i].VX * speedMult * dt;
            _blobs[i].Y += _blobs[i].VY * speedMult * dt;

            // Seamless boundary respawn when completely off canvas
            if (_blobs[i].X > 400f || _blobs[i].X < -80f ||
                _blobs[i].Y > 280f || _blobs[i].Y < -80f)
            {
                SpawnBlob(i, randomOnCanvas: false);
            }
        }

        _lastTime = context.TotalTime;
        OnUpdate(context);
    }

    public override RgbColor Evaluate(Point2D position, double time)
    {
        float fTime = (float)time;
        if (!_initialized || Math.Abs(fTime - _lastTime) > 0.001f)
        {
            float dt = _lastTime > 0f ? Math.Clamp(fTime - _lastTime, 0.001f, 0.1f) : 0.033f;
            Update(new EffectContext(dt, fTime));
        }

        var bg1 = GetColor("bg_color_1", RgbColor.FromHex("#540074"));
        var bg2 = GetColor("bg_color_2", RgbColor.FromHex("#004a79"));
        var bg3 = GetColor("bg_color_3", RgbColor.FromHex("#730073"));
        var col1 = GetColor("color_1", RgbColor.FromHex("#00FFFF"));
        var col2 = GetColor("color_2", RgbColor.FromHex("#FF00FF"));

        float px = position.X * 320f;
        float py = position.Y * 200f;

        RgbColor bg;
        if (px < 100f && py < 100f)
        {
            bg = bg3;
        }
        else if (px > 200f && py > 75f)
        {
            bg = bg2;
        }
        else
        {
            bg = bg1;
        }

        const float maxBlobAlpha = 119f / 255f;

        float rOut = bg.R / 255f;
        float gOut = bg.G / 255f;
        float bOut = bg.B / 255f;

        for (int i = 0; i < MaxBlobs; i++)
        {
            ref readonly var blob = ref _blobs[i];
            float dx = px - blob.X;
            float dy = py - blob.Y;
            float distSq = dx * dx + dy * dy;
            float rSq = blob.Radius * blob.Radius;

            if (distSq < rSq)
            {
                // Soft quadratic falloff from center to edge (prevents hard popping/jerking)
                float normDistSq = distSq / rSq;
                float falloff = 1f - normDistSq;
                float alpha = maxBlobAlpha * (falloff * falloff);

                var blobColor = blob.IsColor1 ? col1 : col2;
                float srcR = blobColor.R / 255f;
                float srcG = blobColor.G / 255f;
                float srcB = blobColor.B / 255f;

                rOut = srcR * alpha + rOut * (1f - alpha);
                gOut = srcG * alpha + gOut * (1f - alpha);
                bOut = srcB * alpha + bOut * (1f - alpha);
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
        if (!_initialized || Math.Abs(context.TotalTime - _lastTime) > 0.0001f)
        {
            Update(context);
        }
        return Evaluate(new Point2D(x, y), context.TotalTime);
    }
}
