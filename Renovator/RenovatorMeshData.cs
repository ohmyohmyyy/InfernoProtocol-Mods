using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Rendering;

namespace InfernoProtocol.Renovator;

internal sealed class RenovatorMeshData
{
    internal Vector3[] Vertices,Normals;
    internal Vector2[] UV;
    internal Vector4[] Tangents;
    internal Color32[] Colors;
    internal readonly Vector4[][] ExtraUV=new Vector4[8][];
    internal int[][] Triangles;
    internal RenovatorMeshData(Mesh mesh)
    {
        int count=mesh.vertexCount;
        if(count<=0||count>250000) throw new InvalidOperationException("Surface mesh size is unsupported.");
        Vertices=new Vector3[count]; Normals=new Vector3[count]; UV=new Vector2[count];
        Triangles=new int[mesh.subMeshCount][];
        if(mesh.isReadable)
        {
            var v=mesh.vertices; var n=mesh.normals; var uv=mesh.uv;
            for(int i=0;i<count;i++) { Vertices[i]=v[i]; Normals[i]=n.Length==count?n[i]:Vector3.up; UV[i]=uv.Length==count?uv[i]:Vector2.zero; }
            if(mesh.HasVertexAttribute(VertexAttribute.Tangent)) { var values=mesh.tangents; Tangents=new Vector4[count]; for(int i=0;i<count;i++) Tangents[i]=values[i]; }
            if(mesh.HasVertexAttribute(VertexAttribute.Color)) { var values=mesh.colors32; Colors=new Color32[count]; for(int i=0;i<count;i++) Colors[i]=values[i]; }
            for(int channel=1;channel<8;channel++)
            {
                var attribute=(VertexAttribute)((int)VertexAttribute.TexCoord0+channel);
                if(!mesh.HasVertexAttribute(attribute)) continue;
                var values=new Il2CppSystem.Collections.Generic.List<Vector4>(); mesh.GetUVs(channel,values);
                if(values.Count!=count) continue;
                ExtraUV[channel]=new Vector4[count]; for(int i=0;i<count;i++) ExtraUV[channel][i]=values[i];
            }
            for(int s=0;s<Triangles.Length;s++) { var t=mesh.GetTriangles(s); Triangles[s]=new int[t.Length]; for(int i=0;i<t.Length;i++) Triangles[s][i]=t[i]; }
            return;
        }
        // A bounded, one-time GPU read per decorated piece, never during Update.
        var streams=new Dictionary<int,byte[]>();
        float Read(VertexAttribute attribute,int vertex,int component)
        {
            if(!mesh.HasVertexAttribute(attribute)||component>=mesh.GetVertexAttributeDimension(attribute)) return 0;
            int stream=mesh.GetVertexAttributeStream(attribute),stride=mesh.GetVertexBufferStride(stream);
            if(!streams.TryGetValue(stream,out var bytes))
            {
                var buffer=mesh.GetVertexBuffer(stream);
                if(buffer==null) throw new InvalidOperationException("Surface vertex buffer is unavailable.");
                try { bytes=ReadBuffer(buffer,checked(count*stride)); } finally { buffer.Dispose(); }
                streams.Add(stream,bytes);
            }
            int offset=vertex*stride+mesh.GetVertexAttributeOffset(attribute);
            var format=mesh.GetVertexAttributeFormat(attribute);
            return format switch {
                VertexAttributeFormat.Float32=>BitConverter.ToSingle(bytes,offset+component*4),
                VertexAttributeFormat.Float16=>(float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(bytes,offset+component*2)),
                VertexAttributeFormat.SNorm8=>Math.Max(-1,(sbyte)bytes[offset+component]/127f),
                VertexAttributeFormat.UNorm8=>bytes[offset+component]/255f,
                VertexAttributeFormat.SNorm16=>Math.Max(-1,BitConverter.ToInt16(bytes,offset+component*2)/32767f),
                VertexAttributeFormat.UNorm16=>BitConverter.ToUInt16(bytes,offset+component*2)/65535f,
                _=>throw new InvalidOperationException("Unsupported vertex format: "+format)
            };
        }
        for(int i=0;i<count;i++)
        {
            Vertices[i]=new Vector3(Read(VertexAttribute.Position,i,0),Read(VertexAttribute.Position,i,1),Read(VertexAttribute.Position,i,2));
            Normals[i]=new Vector3(Read(VertexAttribute.Normal,i,0),Read(VertexAttribute.Normal,i,1),Read(VertexAttribute.Normal,i,2));
            UV[i]=new Vector2(Read(VertexAttribute.TexCoord0,i,0),Read(VertexAttribute.TexCoord0,i,1));
        }
        if(mesh.HasVertexAttribute(VertexAttribute.Tangent))
        {
            Tangents=new Vector4[count]; for(int i=0;i<count;i++) Tangents[i]=new Vector4(Read(VertexAttribute.Tangent,i,0),Read(VertexAttribute.Tangent,i,1),Read(VertexAttribute.Tangent,i,2),Read(VertexAttribute.Tangent,i,3));
        }
        if(mesh.HasVertexAttribute(VertexAttribute.Color))
        {
            Colors=new Color32[count]; for(int i=0;i<count;i++) Colors[i]=new Color(Read(VertexAttribute.Color,i,0),Read(VertexAttribute.Color,i,1),Read(VertexAttribute.Color,i,2),Read(VertexAttribute.Color,i,3));
        }
        for(int channel=1;channel<8;channel++)
        {
            var attribute=(VertexAttribute)((int)VertexAttribute.TexCoord0+channel);
            if(!mesh.HasVertexAttribute(attribute)) continue;
            int dimension=mesh.GetVertexAttributeDimension(attribute); ExtraUV[channel]=new Vector4[count];
            for(int i=0;i<count;i++) ExtraUV[channel][i]=new Vector4(Read(attribute,i,0),Read(attribute,i,1),dimension>2?Read(attribute,i,2):0,dimension>3?Read(attribute,i,3):0);
        }
        var indexBuffer=mesh.GetIndexBuffer();
        if(indexBuffer==null) throw new InvalidOperationException("Surface index buffer is unavailable.");
        int size=mesh.indexFormat==IndexFormat.UInt32?4:2;
        byte[] indices;
        try { indices=ReadBuffer(indexBuffer,checked(indexBuffer.count*indexBuffer.stride)); } finally { indexBuffer.Dispose(); }
        for(int s=0;s<Triangles.Length;s++)
        {
            var sub=mesh.GetSubMesh(s);
            if(sub.topology!=MeshTopology.Triangles||sub.indexCount%3!=0) throw new InvalidOperationException("Unsupported surface topology.");
            var output=new int[sub.indexCount];
            for(int i=0;i<output.Length;i++)
            {
                int offset=checked((sub.indexStart+i)*size);
                int index=checked((size==4?(int)BitConverter.ToUInt32(indices,offset):BitConverter.ToUInt16(indices,offset))+sub.baseVertex);
                if(index<0||index>=count) throw new InvalidOperationException("Invalid surface index.");
                output[i]=index;
            }
            Triangles[s]=output;
        }
        var bounds=new Bounds(Vertices[0],Vector3.zero);
        foreach(var v in Vertices) { if(!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z)) throw new InvalidOperationException("Invalid surface vertices."); bounds.Encapsulate(v); }
        if(bounds.size.sqrMagnitude<.000001f) throw new InvalidOperationException("Surface readback returned empty geometry.");
    }
    private static byte[] ReadBuffer(GraphicsBuffer buffer,int length)
    {
        if(length<=0||length>32000000) throw new InvalidOperationException("Surface buffer exceeds safety limit.");
        var native=new Il2CppStructArray<byte>(length);
        buffer.GetData(native.Cast<Il2CppSystem.Array>());
        var bytes=new byte[length]; for(int i=0;i<length;i++) bytes[i]=native[i]; return bytes;
    }
}
