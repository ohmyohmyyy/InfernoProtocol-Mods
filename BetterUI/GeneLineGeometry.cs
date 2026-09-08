using System;

namespace InfernoProtocol.BetterUI;

internal static class GeneLineGeometry
{
    // Returns a perpendicular half-width; zero-length segments have no quad.
    internal static bool Normal(float ax, float ay, float bx, float by, float width, out float nx, out float ny)
    {
        float dx=bx-ax, dy=by-ay, lengthSquared=dx*dx+dy*dy;
        nx=ny=0;
        if(lengthSquared<.00001f||width<=0) return false;
        float scale=width*.5f/MathF.Sqrt(lengthSquared);
        nx=-dy*scale; ny=dx*scale;
        return true;
    }
}
