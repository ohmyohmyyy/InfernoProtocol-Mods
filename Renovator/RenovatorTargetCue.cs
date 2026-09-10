using System;
using UnityEngine;

namespace InfernoProtocol.Renovator;

internal sealed class RenovatorTargetCue:IDisposable
{
    private static readonly int[] Path={0,1,3,2,0,4,5,1,5,7,3,7,6,2,6,4};
    private GameObject _object;
    private LineRenderer _line;
    private Material _material;
    internal void Show(APlacable piece)
    {
        var filter=piece?.objectMeshFilter;
        var renderer=piece?.objectRenderer;
        if((filter==null||filter.sharedMesh==null)&&renderer==null) { Hide(); return; }
        if(_object==null)
        {
            Shader shader=Shader.Find("Sprites/Default");
            if(shader==null) return;
            _object=new GameObject("RenovatorTargetCue");
            _line=_object.AddComponent<LineRenderer>();
            _material=new Material(shader) {name="Renovator_TargetCue"};
            _line.sharedMaterial=_material; _line.useWorldSpace=true; _line.loop=false;
            _line.positionCount=Path.Length; _line.startWidth=.012f; _line.endWidth=.012f;
            _line.numCornerVertices=2; _line.numCapVertices=2;
            Color color=new(.58f,.86f,.75f,.78f); _line.startColor=color; _line.endColor=color;
        }
        _object.SetActive(true);
        if(filter!=null&&filter.sharedMesh!=null)
        {
            Bounds b=filter.sharedMesh.bounds; Vector3 min=b.min,max=b.max;
            const float pad=.008f; min-=Vector3.one*pad; max+=Vector3.one*pad;
            Transform t=filter.transform;
            for(int i=0;i<Path.Length;i++) _line.SetPosition(i,t.TransformPoint(Corner(Path[i],min,max)));
        }
        else
        {
            Bounds b=renderer.bounds; Vector3 min=b.min-Vector3.one*.008f,max=b.max+Vector3.one*.008f;
            for(int i=0;i<Path.Length;i++) _line.SetPosition(i,Corner(Path[i],min,max));
        }
    }
    private static Vector3 Corner(int index,Vector3 min,Vector3 max)=>new(
        (index&1)==0?min.x:max.x,(index&2)==0?min.y:max.y,(index&4)==0?min.z:max.z);
    internal void Hide() { if(_object!=null) _object.SetActive(false); }
    public void Dispose()
    {
        if(_object!=null) UnityEngine.Object.Destroy(_object);
        if(_material!=null) UnityEngine.Object.Destroy(_material);
        _object=null; _line=null; _material=null;
    }
}
