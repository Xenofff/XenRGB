using System;
using System.Collections.Generic;
using XenRGB.SDK;

namespace XenRGB.Effects.Custom;

/// <summary>
/// Faithful port of SignalRGB's Cotton Candy effect by Alex Krastev.
/// Semi-transparent cyan/magenta circles drift across a multi-zone dark purple/blue background.
/// </summary>
public sealed class CottonCandyCustomEffect : EffectBase
{
    private struct Blob
    {
        public float X, Y;
        public float Radius;
        public float VX, VY;
        public bool IsColor1;
        public float Lifetime;
    }

    private const int MaxBlobs = 30;
    private const int InitialBlobs = 20;
    private readonly Blob[] _blobs = new Blob[MaxBlobs];
    private int _blobCount;
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
        Category = "Atmospheric"
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

    protected override void OnInitialize()
    {
        _rngState = 0xDEADBEEF;
        _blobCount = 0;
        _lastTime = 0f;
        _initialized = false;

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
            VY = (NextInt(2) == 1 ? 1f : -1f) * 6.0f * 0.5f * NextFloat()
        };

        if (_blobCount < MaxBlobs)
            _blobs[_blobCount++] = b;
    }

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

    public override void Update(EffectContext context)
    {
        if (!_initialized)
        {
            OnInitialize();
        }

        float speedMult = GetFloat("speed", 30f) / 10f;
        float dt = context.DeltaTime;

        for (int i = 0; i < _blobCount; i++)
        {
            _blobs[i].X += _blobs[i].VX * speedMult * dt;
            _blobs[i].Y += _blobs[i].VY * speedMult * dt;

            if (_blobs[i].X > 450f || _blobs[i].X < -100f ||
                _blobs[i].Y > 300f || _blobs[i].Y < -100f)
            {
                _blobs[i].Lifetime = 0;
            }
        }

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

    public override RgbColor Evaluate(Point2D position, double time)
    {
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

        const float blobAlpha = 119f / 255f;

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
