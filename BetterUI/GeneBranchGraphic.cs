using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace InfernoProtocol.BetterUI;

// A single maskable graphic for the entire static DNA/branch network.
public sealed class GeneBranchGraphic : MaskableGraphic
{
    private struct Segment { internal Vector2 A, B; internal float Width; internal Color32 Color; }
    private readonly List<Segment> _segments = new(1024);
    public GeneBranchGraphic(IntPtr pointer) : base(pointer) { }
    [HideFromIl2Cpp]
    internal void InitializeMasking()
    {
        // Injected IL2CPP graphics cannot rely on native constructor defaults.
        // In particular m_Maskable=false leaves the custom mesh outside the
        // parent RectMask2D registry while ordinary Images still clip normally.
        if(m_Corners==null) m_Corners=new Il2CppStructArray<Vector3>(4);
        if(m_OnCullStateChanged==null) m_OnCullStateChanged=new CullStateChangedEvent();
        maskable=true;
        RecalculateClipping();
        RecalculateMasking();
    }
    [HideFromIl2Cpp]
    internal void AddSegment(Vector2 a, Vector2 b, float width, Color color)
    {
        if ((b-a).sqrMagnitude < .00001f) return;
        _segments.Add(new Segment { A=a, B=b, Width=width, Color=color });
    }
    public override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        for (int i=0; i<_segments.Count; i++)
        {
            var line=_segments[i];
            if(!GeneLineGeometry.Normal(line.A.x,line.A.y,line.B.x,line.B.y,line.Width,out float nx,out float ny)) continue;
            Vector2 normal=new Vector2(nx,ny);
            int first=helper.currentVertCount;
            helper.AddVert(line.A-normal,line.Color,Vector4.zero);
            helper.AddVert(line.A+normal,line.Color,Vector4.zero);
            helper.AddVert(line.B+normal,line.Color,Vector4.zero);
            helper.AddVert(line.B-normal,line.Color,Vector4.zero);
            helper.AddTriangle(first,first+1,first+2);
            helper.AddTriangle(first,first+2,first+3);
        }
    }
}
