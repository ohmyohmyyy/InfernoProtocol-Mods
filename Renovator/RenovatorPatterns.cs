using System;

namespace InfernoProtocol.Renovator;

internal static class RenovatorPatterns
{
    internal static readonly string[] Names={"Original", "Sage linen", "Deco gold", "Blue porcelain", "Warm terrazzo", "Botanical", "Parquet", "Rose stripe", "Midnight stars", "Checkerboard", "Subway ivory", "Terracotta", "Ocean scallops", "Gingham", "Diamond inlay", "Daisy meadow", "Walnut boards", "Pearl mosaic", "Art deco fan", "Forest toile", "Herringbone", "Sunburst", "Cloud lattice", "Slate hex", "Wildflower", "Copper circuit", "Woven cream", "Burgundy damask", "Seaside tile", "Moss stone"};
    // Catalog order is independent from persistent pattern IDs. Never compact the
    // IDs: doing so would turn existing saved finishes into different wallpapers.
    internal static readonly int[] Catalog={0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,20,21,22,23,24,25,26,28,29};
    internal static readonly float[] Repeats={.5f,1,2,4};
    internal static bool Eligible(string name) => name=="Wall"||name=="WallWithWindow"||
        name.StartsWith("Wooden_Wall_",StringComparison.Ordinal)||name.StartsWith("Stone_Wall_",StringComparison.Ordinal)||
        name.StartsWith("Wooden_Floor_",StringComparison.Ordinal)||name.StartsWith("Stone_Floor_",StringComparison.Ordinal)||
        name.StartsWith("Wooden_Roof_",StringComparison.Ordinal)||name.StartsWith("Stone_Roof_",StringComparison.Ordinal);
    internal static bool Available(int pattern)=>Array.IndexOf(Catalog,pattern)>=0;
    internal static bool StoredChoiceValid(int pattern,int scale)=>pattern>=0&&pattern<Names.Length&&scale>=0&&scale<Repeats.Length;
    internal static bool Valid(int pattern,int scale)=>Available(pattern)&&scale>=0&&scale<Repeats.Length;
    private static float F(float v)=>v-MathF.Floor(v);
    private static uint Hash(int x,int y) { unchecked { uint v=(uint)(x*374761393+y*668265263); v=(v^(v>>13))*1274126177; return v^(v>>16); } }
    private const float Tau=6.28318530718f;
    private static float Noise(int x,int y,int period)=> (Hash((x%period+period)%period,(y%period+period)%period)%65536)/65535f;
    private static float Smooth(float edge,float value,float width=.005f) { float t=Math.Clamp((edge-value)/width+.5f,0,1); return t*t*(3-2*t); }
    private static (float R,float G,float B) Mix((float R,float G,float B) a,(float R,float G,float B) b,float t)=>(a.R+(b.R-a.R)*t,a.G+(b.G-a.G)*t,a.B+(b.B-a.B)*t);
    private static (float R,float G,float B) Shade((float R,float G,float B) c,float s)=>(Math.Clamp(c.R+s,0,1),Math.Clamp(c.G+s,0,1),Math.Clamp(c.B+s,0,1));
    private static float Border(float x,float y)=>MathF.Min(MathF.Min(x,1-x),MathF.Min(y,1-y));
    internal static (float R,float G,float B) Sample(int pattern,float u,float v)
    {
        u=F(u); v=F(v);
        switch(pattern)
        {
            case 1: { float weave=.009f*(MathF.Sin(Tau*u*96)+MathF.Sin(Tau*v*96))+.006f*MathF.Sin(Tau*(u*3+v*2)); return Shade((.49f,.57f,.48f),weave); }
            case 2: { float a=MathF.Abs(F(u*2+v*2)-.5f),b=MathF.Abs(F(u*2-v*2)-.5f); float line=MathF.Max(Smooth(.009f,a),Smooth(.009f,b)); float inset=MathF.Max(Smooth(.005f,MathF.Abs(a-.055f)),Smooth(.005f,MathF.Abs(b-.055f))); return Mix((.11f,.20f,.21f),(.68f,.55f,.33f),MathF.Max(line,inset*.7f)); }
            case 3: { float x=F(u*2)-.5f,y=F(v*2)-.5f,r=MathF.Sqrt(x*x+y*y),angle=MathF.Atan2(y,x); float petals=Smooth(.205f+.058f*MathF.Cos(angle*8),r); float ring=Smooth(.008f,MathF.Abs(r-.34f)); var c=Mix((.88f,.88f,.80f),(.23f,.40f,.53f),MathF.Max(petals,ring)); c=Mix(c,(.88f,.88f,.80f),Smooth(.06f,r)); return Mix(c,(.65f,.68f,.63f),Smooth(.012f,Border(x+.5f,y+.5f))); }
            case 4: { float px=u*14,py=v*14; int cx=(int)px,cy=(int)py; var c=(.84f,.80f,.72f); for(int j=-1;j<=1;j++) for(int i=-1;i<=1;i++) { int gx=cx+i,gy=cy+j; float seed=Noise(gx,gy,14),dx=px-gx-.15f-.7f*seed,dy=py-gy-.15f-.7f*Noise(gx+7,gy+9,14); float angle=seed*Tau,rx=dx*MathF.Cos(angle)-dy*MathF.Sin(angle),ry=dx*MathF.Sin(angle)+dy*MathF.Cos(angle); float d=MathF.Abs(rx)+MathF.Abs(ry)*(.65f+seed); float mask=Smooth(.10f+seed*.17f,d,.04f); c=Mix(c,seed<.33f?(.47f,.54f,.48f):seed<.66f?(.68f,.44f,.34f):(.95f,.90f,.80f),mask); } return c; }
            case 5: { float x=F(u*2+((int)(v*2)%2)*.5f)-.5f,y=F(v*2); float stem=.045f*MathF.Sin(y*Tau); var c=Mix((.83f,.84f,.73f),(.39f,.49f,.35f),Smooth(.006f,MathF.Abs(x-stem))); for(int i=0;i<4;i++) { float side=i%2==0?-1:1,dx=(x-stem)*side-.095f,dy=y-(.18f+i*.18f); float along=(dx+dy)*.7071f,across=(dy-dx)*.7071f; float shape=along*along/(.135f*.135f)+across*across/(.05f*.05f); c=Mix(c,i%2==0?(.38f,.50f,.36f):(.50f,.60f,.43f),Smooth(1,shape,.14f)); } return c; }
            case 6: { int tx=(int)(u*4),ty=(int)(v*4); bool vertical=(tx+ty)%2==0; float x=F(u*4),y=F(v*4),across=vertical?x:y,along=vertical?y:x; int plank=(int)(across*3); float grain=.016f*MathF.Sin(Tau*(across*30+.15f*MathF.Sin(along*Tau)))+.022f*(Noise(tx*3+plank,ty,12)-.5f); var c=Shade((.53f,.36f,.22f),grain); return Mix(c,(.27f,.19f,.12f),Smooth(.014f,MathF.Min(Border(x,y),MathF.Min(F(across*3),1-F(across*3))/3))); }
            case 7: { float x=MathF.Abs(F(u*4)-.5f); var c=Mix((.89f,.83f,.75f),(.70f,.51f,.50f),Smooth(.18f,x)); return Mix(c,(.64f,.50f,.38f),Smooth(.006f,MathF.Abs(x-.215f))); }
            case 8: { float x=MathF.Abs(F(u*3+((int)(v*4)%2)*.5f)-.5f),y=MathF.Abs(F(v*4)-.5f); float star=MathF.Pow(x/.10f,.55f)+MathF.Pow(y/.18f,.55f); return Mix((.12f,.17f,.26f),(.75f,.66f,.45f),Smooth(1,star,.10f)); }
            case 9: { var c=((int)(u*4)+(int)(v*4))%2==0?(.83f,.82f,.74f):(.23f,.27f,.27f); return Mix(c,(.52f,.53f,.48f),Smooth(.007f,Border(F(u*4),F(v*4)))); }
            case 10: { float x=F(u*3+((int)(v*6)%2)*.5f),y=F(v*6),edge=MathF.Min(MathF.Min(x,1-x),MathF.Min(y,1-y)*.5f); var c=Shade((.85f,.85f,.77f),.018f*Smooth(.045f,edge,.04f)); return Mix(c,(.62f,.63f,.58f),Smooth(.011f,edge)); }
            case 11: { float x=F(u*4),y=F(v*4); float shade=(Noise((int)(u*4),(int)(v*4),4)-.5f)*.08f+.004f*MathF.Sin(Tau*u*13)*MathF.Sin(Tau*v*11); return Mix(Shade((.64f,.38f,.26f),shade),(.64f,.58f,.47f),Smooth(.017f,Border(x,y))); }
            case 12: { float px=u*3,py=v*6; float line=0; for(int row=-1;row<=1;row++) { int gy=(int)py+row; float x=F(px+(gy%2)*.5f)-.5f,y=(py-gy)*.5f; if(y<0||y>.52f) continue; float r=MathF.Sqrt(x*x+y*y); line=MathF.Max(line,Smooth(.009f,MathF.Abs(r-.5f))); } return Mix((.23f,.43f,.45f),(.72f,.75f,.60f),line); }
            case 13: { float a=Smooth(.25f,MathF.Abs(F(u*4)-.5f)),b=Smooth(.25f,MathF.Abs(F(v*4)-.5f)); var c=Mix((.87f,.84f,.75f),(.50f,.61f,.47f),(a+b)*.38f+a*b*.24f); return Shade(c,.005f*(MathF.Sin(Tau*u*96)+MathF.Sin(Tau*v*96))); }
            case 14: { float d=MathF.Abs(F(u*3)-.5f)+MathF.Abs(F(v*3)-.5f); var c=Mix((.82f,.80f,.72f),(.63f,.52f,.34f),Smooth(.19f,d)); return Mix(c,(.25f,.33f,.34f),Smooth(.15f,d)); }
            case 15: { float x=F(u*3+((int)(v*4)%2)*.5f)-.5f,y=(F(v*4)-.5f)*.75f,r=MathF.Sqrt(x*x+y*y); float petal=.12f+.028f*MathF.Cos(8*MathF.Atan2(y,x)); var c=Mix((.42f,.54f,.40f),(.91f,.86f,.73f),Smooth(petal,r)); return Mix(c,(.76f,.61f,.30f),Smooth(.033f,r)); }
            case 16: { int board=(int)(u*5); float x=F(u*5),y=F(v+(board%2)*.5f); float grain=.012f*MathF.Sin(Tau*(x*22+.25f*MathF.Sin(y*Tau)))+.012f*MathF.Sin(Tau*(x*47+y*2))+(Noise(board,0,5)-.5f)*.07f; return Mix(Shade((.39f,.27f,.19f),grain),(.22f,.16f,.12f),Smooth(.008f,MathF.Min(MathF.Min(x,1-x),MathF.Min(y,1-y)*5))); }
            case 17: { float x=F(u*10),y=F(v*10); float shade=(Noise((int)(u*10),(int)(v*10),10)-.5f)*.10f; return Mix(Shade((.75f,.81f,.78f),shade),(.59f,.65f,.62f),Smooth(.028f,Border(x,y))); }
            case 18: { float x=F(u*3)-.5f,y=F(v*3); float r=MathF.Sqrt(x*x+y*y),arc=MathF.Max(Smooth(.010f,MathF.Abs(r-.28f)),Smooth(.009f,MathF.Abs(r-.43f))); float rays=MathF.Max(Smooth(.008f,MathF.Abs(x-y*.58f)),Smooth(.008f,MathF.Abs(x+y*.58f))); float fan=MathF.Max(arc*(y<.54f?1:0),rays*(y<.47f?1:0)); return Mix((.09f,.16f,.18f),(.72f,.57f,.31f),fan); }
            case 19: { float x=F(u*3+((int)(v*3)%2)*.5f)-.5f,y=F(v*3); float trunk=Smooth(.010f,MathF.Abs(x+.035f*MathF.Sin(y*Tau))); var c=Mix((.88f,.85f,.73f),(.28f,.42f,.32f),trunk*(y<.7f?1:0)); for(int i=0;i<4;i++) { float cy=.16f+i*.14f,side=i%2==0?-1:1,dx=x-side*(.09f+.025f*i),dy=y-cy; float leaf=dx*dx/(.105f*.105f)+dy*dy/(.052f*.052f); c=Mix(c,i%3==0?(.35f,.49f,.35f):(.42f,.54f,.39f),Smooth(1,leaf,.13f)); } float crown=x*x/(.19f*.19f)+(y-.75f)*(y-.75f)/(.18f*.18f); return Mix(c,(.25f,.39f,.30f),Smooth(1,crown,.12f)); }
            case 20: { float px=u*8,py=v*8; int gx=(int)px,gy=(int)py; float x=F(px),y=F(py); bool slash=(gx+gy)%2==0; float across=slash?F(x+y):F(x-y),along=slash?F((x-y)*.5f):F((x+y)*.5f); float grain=.018f*MathF.Sin(Tau*(along*8+across*.3f))+(Noise(gx,gy,8)-.5f)*.055f; var c=Shade(slash?(.52f,.35f,.21f):(.59f,.41f,.25f),grain); return Mix(c,(.25f,.18f,.12f),Smooth(.024f,MathF.Min(across,1-across))); }
            case 21: { float x=F(u*3+((int)(v*3)%2)*.5f)-.5f,y=F(v*3)-.5f,r=MathF.Sqrt(x*x+y*y),a=MathF.Atan2(y,x); float rays=.5f+.5f*MathF.Cos(a*16); float ray=Smooth(.48f,r,.07f)*Smooth(.58f,rays,.13f); float ring=Smooth(.012f,MathF.Abs(r-.29f)); var c=Mix((.84f,.77f,.61f),(.73f,.45f,.22f),MathF.Max(ray*.75f,ring)); return Mix(c,(.34f,.25f,.22f),Smooth(.065f,r)); }
            case 22: { float waveA=MathF.Abs(MathF.Sin(Tau*(u*3+v*2))),waveB=MathF.Abs(MathF.Sin(Tau*(u*3-v*2))); float lattice=MathF.Max(Smooth(.075f,waveA),Smooth(.075f,waveB)); float mist=.012f*MathF.Sin(Tau*v*12)+.008f*MathF.Sin(Tau*u*9); return Mix(Shade((.67f,.78f,.79f),mist),(.89f,.88f,.79f),lattice); }
            case 23: { float px=u*4,py=v*6; int row=(int)py; float x=F(px+(row%2)*.5f)-.5f,y=F(py)-.5f; float hex=MathF.Max(MathF.Abs(x)*.8660254f+MathF.Abs(y)*.5f,MathF.Abs(y)); float edge=Smooth(.035f,MathF.Abs(hex-.43f)); float shade=(Noise((int)(px+(row%2)*.5f),row,12)-.5f)*.09f; return Mix(Shade((.28f,.34f,.37f),shade),(.55f,.59f,.58f),edge); }
            case 24: { float px=u*6,py=v*6; int gx=(int)px,gy=(int)py; float ox=.18f+.64f*Noise(gx,gy,6),oy=.18f+.64f*Noise(gx+11,gy+5,6),x=F(px)-ox,y=F(py)-oy,r=MathF.Sqrt(x*x+y*y),a=MathF.Atan2(y,x); float petals=.10f+.035f*MathF.Cos(a*6); var baseColor=Shade((.76f,.78f,.62f),.012f*MathF.Sin(Tau*(u*15+v*7))); var petal=(gx+gy)%3==0?(.86f,.59f,.55f):(gx+gy)%3==1?(.76f,.76f,.88f):(.92f,.84f,.63f); var c=Mix(baseColor,petal,Smooth(petals,r,.025f)); return Mix(c,(.55f,.39f,.20f),Smooth(.032f,r)); }
            case 25: { float x=F(u*8),y=F(v*8); int gx=(int)(u*8),gy=(int)(v*8); bool horizontal=(gx+gy)%2==0; float trace=Smooth(.035f,MathF.Abs((horizontal?y:x)-.5f)); float junction=Smooth(.11f,MathF.Sqrt((x-.5f)*(x-.5f)+(y-.5f)*(y-.5f))); float fine=.009f*MathF.Sin(Tau*(u*48+v*12)); var c=Shade((.07f,.19f,.19f),fine); c=Mix(c,(.67f,.38f,.19f),MathF.Max(trace,junction)); return Mix(c,(.86f,.65f,.32f),Smooth(.042f,MathF.Abs(MathF.Sqrt((x-.5f)*(x-.5f)+(y-.5f)*(y-.5f))-.075f))); }
            case 26: { float x=F(u*8),y=F(v*8); int gx=(int)(u*8),gy=(int)(v*8); bool over=(gx+gy)%2==0; float thread=.018f*(MathF.Sin(Tau*(over?x:y)*12)+MathF.Sin(Tau*(over?y:x)*3)); float bevel=.035f*(.5f-MathF.Abs((over?y:x)-.5f)); var c=Shade(over?(.82f,.77f,.65f):(.73f,.68f,.57f),thread+bevel); return Mix(c,(.48f,.44f,.37f),Smooth(.016f,Border(x,y))); }
            case 27: { float x=MathF.Abs(F(u*2)-.5f),y=MathF.Abs(F(v*3)-.5f); float heart=(x/.20f)*(x/.20f)+((y-.12f)/.17f)*((y-.12f)/.17f); float curl=MathF.Abs(MathF.Sqrt((x-.17f)*(x-.17f)+(y-.19f)*(y-.19f))-.12f); float stem=Smooth(.008f,MathF.Abs(x-.025f*MathF.Sin(y*Tau*2))); float motif=MathF.Max(Smooth(1,heart,.12f),MathF.Max(Smooth(.012f,curl),stem)); return Mix((.30f,.07f,.11f),(.63f,.38f,.33f),motif*.72f); }
            case 28: { float x=F(u*4)-.5f,y=F(v*4)-.5f,r=MathF.Sqrt(x*x+y*y),a=MathF.Atan2(y,x); float wave=Smooth(.018f,MathF.Abs(y-.10f*MathF.Sin(Tau*(x*1.5f+.25f)))); float compass=Smooth(.012f,MathF.Abs(r-(.22f+.035f*MathF.Cos(a*8)))); float grout=Smooth(.018f,Border(x+.5f,y+.5f)); var c=Mix((.82f,.84f,.76f),(.24f,.52f,.57f),MathF.Max(wave,compass)); return Mix(c,(.58f,.61f,.57f),grout); }
            case 29: { float px=u*5,py=v*5; int gx=(int)px,gy=(int)py; float x=F(px+((gy%2)*.35f)),y=F(py),warp=.045f*MathF.Sin(Tau*(y+Noise(gx,gy,10))); float edge=MathF.Min(MathF.Min(x+warp,1-x-warp),MathF.Min(y,1-y)); float shade=(Noise(gx,gy,10)-.5f)*.13f+.012f*MathF.Sin(Tau*(x*4+y*3)); var stone=Shade((.39f,.43f,.38f),shade); var mortar=Mix((.24f,.28f,.25f),(.31f,.39f,.28f),Noise(gx+3,gy+7,10)); return Mix(stone,mortar,Smooth(.035f,edge,.018f)); }
            default:return (.25f,.29f,.3f);
        }
    }
}
