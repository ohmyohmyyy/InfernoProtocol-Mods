using System;

namespace InfernoProtocol.FieldQuests;

// Deterministic UV patterns. No asset, renderer or random-state mutation.
public static class FinishPattern
{
    public readonly struct Ink
    {
        public readonly float R, G, B;
        public Ink(float r, float g, float b) { R = r; G = g; B = b; }
    }
    private static float Hash(int x, int y)
    {
        uint n = unchecked((uint)(x * 374761393 + y * 668265263));
        n = unchecked((n ^ (n >> 13)) * 1274126177u);
        return (n ^ (n >> 16)) / (float)uint.MaxValue;
    }
    private static float Noise(float x, float y)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float u = x - ix, v = y - iy;
        u = u * u * (3 - 2 * u); v = v * v * (3 - 2 * v);
        float a = Hash(ix, iy) * (1 - u) + Hash(ix + 1, iy) * u;
        float b = Hash(ix, iy + 1) * (1 - u) + Hash(ix + 1, iy + 1) * u;
        return a * (1 - v) + b * v;
    }
    private static float Frac(float x) => x - MathF.Floor(x);
    public static Ink Sample(int finish, float u, float v)
    {
        if (finish == 1)
        {
            float n = Noise(u * 15, v * 15) * .7f + Noise(u * 37, v * 37) * .3f;
            return n < .36f ? new Ink(.08f,.12f,.07f) : n < .5f ? new Ink(.22f,.32f,.13f) :
                n < .64f ? new Ink(.43f,.49f,.25f) : new Ink(.68f,.65f,.43f);
        }
        if (finish == 2)
        {
            float stripe = Frac((u + v * .55f) * 9);
            if (stripe < .055f) return new Ink(.82f,.91f,.94f);
            if (Frac(v * 8) < .04f) return new Ink(.09f,.16f,.23f);
            return stripe < .38f ? new Ink(.09f,.32f,.65f) : new Ink(.025f,.08f,.18f);
        }
        if (finish == 3)
        {
            float x = u * 12, y = v * 12, first = 100, second = 100;
            int cx = (int)MathF.Floor(x), cy = (int)MathF.Floor(y);
            for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++)
            {
                float dx = cx+a+Hash(cx+a,cy+b)-x, dy = cy+b+Hash(cx+a+71,cy+b+39)-y;
                float d = dx*dx + dy*dy;
                if (d < first) { second = first; first = d; } else if (d < second) second = d;
            }
            float edge = MathF.Sqrt(second) - MathF.Sqrt(first);
            return edge < .045f ? new Ink(.96f,.38f,.06f) : edge < .095f ? new Ink(.42f,.065f,.02f) : new Ink(.065f,.05f,.045f);
        }
        if (finish == 4)
        {
            float a = Frac((u + v) * 14), b = Frac((u - v) * 14);
            if (a < .065f || b < .065f) return new Ink(.75f,.56f,.24f);
            if (a < .12f || b < .12f) return new Ink(.08f,.055f,.025f);
            return new Ink(.32f,.21f,.10f);
        }
        if (finish == 5)
        {
            float n = Noise(MathF.Floor(u * 48) / 3, MathF.Floor(v * 48) / 3);
            return n < .35f ? new Ink(.13f,.21f,.29f) : n < .51f ? new Ink(.46f,.57f,.64f) :
                n < .66f ? new Ink(.73f,.80f,.83f) : new Ink(.93f,.95f,.91f);
        }
        if (finish == 6)
        {
            if (Frac(v * 6) < .075f) return new Ink(.72f,.74f,.69f);
            return Frac((u + v) * 10) < .48f ? new Ink(.95f,.70f,.045f) : new Ink(.055f,.06f,.055f);
        }
        if (finish == 7)
        {
            float stripe = Frac(u * 13 + MathF.Sin(v * 28) * .24f + Noise(u*9,v*19)*1.4f);
            return stripe < .26f ? new Ink(.06f,.045f,.025f) : stripe < .36f ? new Ink(.84f,.68f,.35f) : new Ink(.62f,.30f,.085f);
        }
        if (finish == 8)
        {
            float lane = Frac(u * 5);
            if (lane < .12f) return new Ink(.88f,.86f,.78f);
            if (lane < .31f) return ((int)(u*80) + (int)(v*80)) % 2 == 0 ? new Ink(.83f,.81f,.74f) : new Ink(.035f,.035f,.045f);
            return new Ink(.56f,.025f,.055f);
        }
        if (finish == 9)
        {
            float wave = Frac(v * 12 + MathF.Sin(u * 25) * .35f + MathF.Sin(u * 49) * .10f);
            return wave < .10f ? new Ink(.86f,.93f,.86f) : wave < .39f ? new Ink(.055f,.57f,.61f) :
                wave < .52f ? new Ink(.08f,.26f,.36f) : new Ink(.025f,.09f,.17f);
        }
        if (finish == 10)
        {
            float height = Noise(u*7,v*7) * .75f + Noise(u*19,v*19) * .25f;
            float contour = Frac(height * 23);
            return contour < .14f ? new Ink(.78f,.84f,.55f) : contour < .23f ? new Ink(.29f,.44f,.30f) : new Ink(.055f,.15f,.12f);
        }
        if (finish == 11)
        {
            float a = Frac((u+v)*18), b = Frac((u-v)*18);
            if (a < .16f) return b < .16f ? new Ink(.91f,.72f,.30f) : new Ink(.53f,.36f,.12f);
            if (b < .16f) return new Ink(.74f,.57f,.23f);
            return ((int)(u*100)+(int)(v*100))%2 == 0 ? new Ink(.22f,.055f,.31f) : new Ink(.11f,.025f,.18f);
        }
        if (finish == 12)
        {
            float x = u*16, y = v*16;
            int ix = (int)x, iy = (int)y;
            float n = Hash(ix*2 + (Frac(x)+Frac(y) > 1 ? 1 : 0), iy);
            return n < .25f ? new Ink(.23f,.17f,.12f) : n < .5f ? new Ink(.52f,.35f,.21f) :
                n < .75f ? new Ink(.73f,.57f,.36f) : new Ink(.88f,.79f,.58f);
        }
        return new Ink(1, 1, 1);
    }
}
