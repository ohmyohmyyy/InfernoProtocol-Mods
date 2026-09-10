using System;
using System.IO;
using InfernoProtocol.Renovator;

internal static class PatternPreview
{
    internal static void Write(string path)
    {
        const int tile=256,columns=6,width=columns*tile;
        int rows=(RenovatorPatterns.Catalog.Length+columns-1)/columns,height=rows*tile;
        using var file=File.Create(path); using var writer=new BinaryWriter(file);
        writer.Write((ushort)0x4d42); writer.Write(54+width*height*3); writer.Write(0); writer.Write(54);
        writer.Write(40); writer.Write(width); writer.Write(height); writer.Write((ushort)1); writer.Write((ushort)24);
        writer.Write(0); writer.Write(width*height*3); writer.Write(2835); writer.Write(2835); writer.Write(0); writer.Write(0);
        for(int y=height-1;y>=0;y--) for(int x=0;x<width;x++)
        {
            int catalogIndex=(y/tile)*columns+x/tile;
            (float R,float G,float B) c=catalogIndex<RenovatorPatterns.Catalog.Length?RenovatorPatterns.Sample(RenovatorPatterns.Catalog[catalogIndex],(x%tile)/128f,(y%tile)/128f):(.04f,.04f,.04f);
            if(x%tile<3||y%tile<3) c=(.1f,.1f,.1f);
            writer.Write((byte)(c.B*255)); writer.Write((byte)(c.G*255)); writer.Write((byte)(c.R*255));
        }
    }
}
