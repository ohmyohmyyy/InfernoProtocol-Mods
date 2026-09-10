using System;

namespace InfernoProtocol.Renovator;

internal static class RenovatorMapping
{
    // One tile per metre. Coordinates are piece-local: moving/rotating a piece
    // does not make its finish slide, and disconnected planks share one origin.
    internal static (float U,float V) Project(float x,float y,float z,int axis) =>
        axis==0?(z,y):axis==1?(x,z):(x,y);
    internal static (float U,float V) Surface(float x,float y,float z,float nx,float ny,float nz)
    {
        int axis=Axis(nx,ny,nz);
        float length=MathF.Sqrt(nx*nx+ny*ny+nz*nz);
        if(length<.00001f) return Project(x,y,z,2);
        // Canonical orientation makes opposing faces agree. Project an axis
        // onto the face to keep the repeat size correct on sloping roofs.
        float sign=(axis==0?nx:axis==1?ny:-nz)>0?-1:1;
        nx=nx/length*sign; ny=ny/length*sign; nz=nz/length*sign;
        float dot=axis==0?nz:nx;
        float ux=(axis==0?0:1)-nx*dot,uy=-ny*dot,uz=(axis==0?1:0)-nz*dot;
        length=MathF.Sqrt(ux*ux+uy*uy+uz*uz);
        ux/=length; uy/=length; uz/=length;
        float vx=ny*uz-nz*uy,vy=nz*ux-nx*uz,vz=nx*uy-ny*ux;
        return (x*ux+y*uy+z*uz,x*vx+y*vy+z*vz);
    }
    internal static int Axis(float x,float y,float z)
    {
        x=MathF.Abs(x); y=MathF.Abs(y); z=MathF.Abs(z);
        return x>=y&&x>=z?0:y>=z?1:2;
    }
}
