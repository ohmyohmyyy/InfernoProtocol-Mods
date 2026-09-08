using System;

namespace InfernoProtocol.BetterUI;

internal static class GeneTreeLayout
{
    internal static float Height(int count) => (Math.Max(1, (count + 1) / 2) - 1) * 160 + 130;
    internal static (float X, float Y) Position(int index, int count)
    {
        float top = Height(count) - 130;
        return ((index % 2 == 0 ? -1 : 1) * (145 + (index / 2 % 2) * 45), top / 2 - (index / 2) * 160);
    }
    internal static int Navigate(int selected, int count, int direction)
    {
        if (count < 1) return -1;
        selected = Math.Clamp(selected, 0, count - 1);
        var origin = Position(selected, count);
        float ax = Math.Abs(direction) == 2 ? 0 : Math.Sign(direction);
        float ay = direction == 2 ? -1 : direction == -2 ? 1 : 0;
        int best = selected; float score = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var p = Position(i, count);
            float dx = p.X - origin.X, dy = p.Y - origin.Y;
            float along = dx * ax + dy * ay;
            if (along < 1) continue;
            float candidate = along + Math.Abs(dx * ay - dy * ax) * 3;
            if (candidate < score) { score = candidate; best = i; }
        }
        return best;
    }
}
