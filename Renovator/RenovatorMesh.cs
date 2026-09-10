using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace InfernoProtocol.Renovator;

internal sealed class RenovatorMesh:IDisposable
{
    private readonly struct CacheKey:IEquatable<CacheKey>
    {
        private readonly int _mesh,_x,_y,_z;
        private readonly ulong _surfaces;
        internal CacheKey(Mesh mesh,Vector3 scale,ulong surfaces)
        { _mesh=mesh.GetInstanceID(); _x=Mathf.RoundToInt(scale.x*10000); _y=Mathf.RoundToInt(scale.y*10000); _z=Mathf.RoundToInt(scale.z*10000); _surfaces=surfaces; }
        public bool Equals(CacheKey other)=>_mesh==other._mesh&&_x==other._x&&_y==other._y&&_z==other._z&&_surfaces==other._surfaces;
        public override bool Equals(object obj)=>obj is CacheKey other&&Equals(other);
        public override int GetHashCode()=>HashCode.Combine(_mesh,_x,_y,_z,_surfaces);
    }
    private sealed class CacheEntry
    {
        internal readonly Mesh Mesh;
        internal int Users;
        internal CacheEntry(Mesh mesh) { Mesh=mesh; Users=1; }
    }
    private static readonly Dictionary<CacheKey,CacheEntry> Cache=new();
    private readonly MeshFilter _filter;
    private readonly Mesh _original;
    private readonly CacheKey _cacheKey;
    private CacheEntry _cacheEntry;
    private Mesh _owned;
    internal RenovatorMesh(APlacable piece,Material[] materials)
    {
        _filter=piece.objectMeshFilter;
        if(_filter==null||_filter.sharedMesh==null) throw new InvalidOperationException("This piece has no supported surface mesh.");
        _original=_filter.sharedMesh;
        var t=_filter.transform;
        var scale=new Vector3(t.TransformVector(Vector3.right).magnitude,t.TransformVector(Vector3.up).magnitude,t.TransformVector(Vector3.forward).magnitude);
        if(_original.subMeshCount>64) throw new InvalidOperationException("Surface meshes with more than 64 sections are unsupported.");
        ulong surfaces=0;
        for(int s=0;s<_original.subMeshCount;s++) if(s<materials.Length&&materials[s]!=null) surfaces|=1UL<<s;
        _cacheKey=new CacheKey(_original,scale,surfaces);
        if(Cache.TryGetValue(_cacheKey,out var cached)&&cached.Mesh!=null)
        {
            cached.Users++; _cacheEntry=cached; _owned=cached.Mesh; return;
        }
        if(cached!=null) Cache.Remove(_cacheKey);
        try
        {
            var data=new RenovatorMeshData(_original);
            var source=data.Vertices; var normals=data.Normals; var oldUv=data.UV;
            var vertices=new List<Vector3>(); var outputNormals=new List<Vector3>(); var uv=new List<Vector2>();
            var tangents=data.Tangents!=null?new List<Vector4>():null;
            var colors=data.Colors!=null?new List<Color32>():null;
            var extra=new List<Vector4>[8]; for(int channel=1;channel<8;channel++) if(data.ExtraUV[channel]!=null) extra[channel]=new List<Vector4>();
            var indices=new List<int[]>();
            for(int s=0;s<_original.subMeshCount;s++)
            {
                if(_original.GetTopology(s)!=MeshTopology.Triangles) throw new InvalidOperationException("Unsupported surface topology.");
                var tris=data.Triangles[s]; var output=new int[tris.Length];
                bool decorate=s<materials.Length&&materials[s]!=null;
                for(int i=0;i<tris.Length;i+=3)
                {
                    Vector3 a=Vector3.Scale(source[tris[i]],scale),b=Vector3.Scale(source[tris[i+1]],scale),c=Vector3.Scale(source[tris[i+2]],scale);
                    Vector3 face=Vector3.Cross(b-a,c-a).normalized;
                    for(int j=0;j<3;j++)
                    {
                        int index=tris[i+j]; Vector3 v=source[index],p=Vector3.Scale(v,scale);
                        var mapped=RenovatorMapping.Surface(p.x,p.y,p.z,face.x,face.y,face.z);
                        output[i+j]=vertices.Count; vertices.Add(v);
                        outputNormals.Add(normals.Length==source.Length?normals[index]:Vector3.Cross(source[tris[i+1]]-source[tris[i]],source[tris[i+2]]-source[tris[i]]).normalized);
                        uv.Add(decorate?new Vector2(mapped.U,mapped.V):oldUv.Length==source.Length?oldUv[index]:Vector2.zero);
                        tangents?.Add(data.Tangents[index]); colors?.Add(data.Colors[index]);
                        for(int channel=1;channel<8;channel++) extra[channel]?.Add(data.ExtraUV[channel][index]);
                    }
                }
                indices.Add(output);
            }
            _owned=new Mesh {name="Renovator_SurfaceMapping",indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            _owned.vertices=vertices.ToArray(); _owned.normals=outputNormals.ToArray(); _owned.uv=uv.ToArray();
            if(tangents!=null) _owned.tangents=tangents.ToArray();
            if(colors!=null) _owned.colors32=colors.ToArray();
            for(int channel=1;channel<8;channel++) if(extra[channel]!=null)
            {
                var values=extra[channel]; var native=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector4>(values.Count);
                for(int i=0;i<values.Count;i++) native[i]=values[i]; _owned.SetUVs(channel,native);
            }
            _owned.subMeshCount=indices.Count;
            for(int s=0;s<indices.Count;s++) _owned.SetTriangles(indices[s],s);
            _owned.RecalculateBounds(); if(tangents==null) _owned.RecalculateTangents();
            _cacheEntry=new CacheEntry(_owned); Cache.Add(_cacheKey,_cacheEntry);
        }
        catch { Dispose(); throw; }
    }
    internal void Apply()
    {
        if(_filter==null) throw new InvalidOperationException("The building piece was removed.");
        if(_filter.sharedMesh!=_original&&_filter.sharedMesh!=_owned) throw new InvalidOperationException("Another effect changed the building mesh.");
        _filter.sharedMesh=_owned;
    }
    internal void Restore() { if(_filter!=null&&_owned!=null&&_filter.sharedMesh==_owned) _filter.sharedMesh=_original; }
    public void Dispose()
    {
        Restore();
        if(_cacheEntry!=null)
        {
            _cacheEntry.Users--;
            if(_cacheEntry.Users<=0)
            {
                Cache.Remove(_cacheKey);
                if(_cacheEntry.Mesh!=null) UnityEngine.Object.Destroy(_cacheEntry.Mesh);
            }
            _cacheEntry=null;
        }
        else if(_owned!=null) UnityEngine.Object.Destroy(_owned);
        _owned=null;
    }
}
