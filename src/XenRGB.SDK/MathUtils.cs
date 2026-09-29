using System;

namespace XenRGB.SDK;

/// <summary>
/// Mathematical and procedural noise utilities for creating dynamic RGB effects.
/// </summary>
public static class MathUtils
{
    private static readonly int[] Permutation = new int[512];

    static MathUtils()
    {
        // Deterministic pseudo-random permutation table for 2D Perlin noise
        int[] p = {
            151,160,137,91,90,15,131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,
            8,99,37,240,21,10,23,190,6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,
            35,11,32,57,177,33,88,237,149,56,87,174,20,125,136,171,168,68,175,74,165,71,
            134,139,48,27,166,77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,
            55,46,245,40,244,102,143,54,65,25,63,161,1,216,80,73,209,76,132,187,208,89,
            18,169,200,196,135,130,116,188,159,86,164,100,109,198,173,186,3,64,52,217,226,
            250,124,123,5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,
            189,28,42,223,183,170,213,119,248,152,2,44,154,163,70,221,153,101,155,167,43,
            172,9,129,22,39,253,19,98,108,110,79,113,224,232,178,185,112,104,218,246,97,
            228,251,34,242,193,238,210,144,12,191,179,162,241,81,51,145,235,249,14,239,
            107,49,192,214,31,181,199,106,157,184,84,204,176,115,121,50,45,127,4,150,254,
            138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180
        };

        for (int i = 0; i < 256; i++)
        {
            Permutation[i] = p[i];
            Permutation[256 + i] = p[i];
        }
    }

    /// <summary>
    /// Computes 2D Perlin noise in the range [-1, 1].
    /// </summary>
    public static float Perlin2D(float x, float y)
    {
        int xi = (int)MathF.Floor(x) & 255;
        int yi = (int)MathF.Floor(y) & 255;

        float xf = x - MathF.Floor(x);
        float yf = y - MathF.Floor(y);

        float u = Fade(xf);
        float v = Fade(yf);

        int aa = Permutation[Permutation[xi] + yi];
        int ab = Permutation[Permutation[xi] + yi + 1];
        int ba = Permutation[Permutation[xi + 1] + yi];
        int bb = Permutation[Permutation[xi + 1] + yi + 1];

        float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
        float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);

        return Lerp(x1, x2, v);
    }

    /// <summary>
    /// Normalized Perlin noise in the range [0, 1].
    /// </summary>
    public static float Perlin2DNormalized(float x, float y)
    {
        return (Perlin2D(x, y) + 1f) * 0.5f;
    }

    public static float SineWave(float time, float speed, float frequency, float phase = 0f)
    {
        return (MathF.Sin((time * speed + phase) * MathF.PI * 2f * frequency) + 1f) * 0.5f;
    }

    public static float SmoothStep(float edge0, float edge1, float x)
    {
        float t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    public static float TriangleWave(float x)
    {
        float t = x - MathF.Floor(x);
        return t < 0.5f ? t * 2f : (1f - t) * 2f;
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float Lerp(float a, float b, float t) => a + t * (b - a);

    private static float Grad(int hash, float x, float y)
    {
        int h = hash & 7;
        float u = h < 4 ? x : y;
        float v = h < 4 ? y : x;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }
}
