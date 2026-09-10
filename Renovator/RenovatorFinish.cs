using System;
using System.Collections.Generic;
using Game.Effects;
using UnityEngine;

namespace InfernoProtocol.Renovator;

internal sealed class RenovatorFinish:IDisposable
{
    private readonly Renderer _renderer;
    private readonly Material[] _original, _owned;
    private readonly string[] _maps;
    private RenovatorMesh _mesh;
    private bool _attached;
    internal RenovatorFinish(APlacable piece)
    {
        _renderer=piece.objectRenderer;
        if(_renderer==null) throw new InvalidOperationException("This piece has no supported renderer.");
        var materials=_renderer.sharedMaterials;
        Material highlight=EffectManager.highlightMaterial;
        int surfaceSlots=piece.objectMesh!=null?piece.objectMesh.subMeshCount:materials.Length;
        _original=new Material[materials.Length]; _owned=new Material[materials.Length]; _maps=new string[materials.Length];
        int count=0;
        try
        {
            for(int i=0;i<materials.Length;i++)
            {
                var original=materials[i]; _original[i]=original;
                if(original==null) continue;
                // A highlight can be instantiated during the hammer equip frame.
                // Never capture an extra highlight pass as wallpaper material.
                if(i>=surfaceSlots&&highlight!=null&&original.shader==highlight.shader) continue;
                // Do not turn transparent glass, foliage or cutout materials opaque.
                if((original.HasProperty("_Surface")&&original.GetFloat("_Surface")>0)||
                   (original.HasProperty("_SurfaceType")&&original.GetFloat("_SurfaceType")>0)||
                   (original.HasProperty("_AlphaClip")&&original.GetFloat("_AlphaClip")>0)||
                   (original.HasProperty("_AlphaCutoffEnable")&&original.GetFloat("_AlphaCutoffEnable")>0)||
                   original.IsKeywordEnabled("_ALPHATEST_ON")||original.IsKeywordEnabled("_ALPHABLEND_ON")) continue;
                string map=original.HasProperty("_BaseColorMap")?"_BaseColorMap":original.HasProperty("_BaseMap")?"_BaseMap":original.HasProperty("_MainTex")?"_MainTex":null;
                if(map==null) continue;
                _maps[i]=map;
                _owned[i]=new Material(original); _owned[i].name="Renovator_Owned";
                // Native wood/stone maps use the old atlas. Do not project those
                // over the wallpaper with the new surface coordinates.
                foreach(string property in new[]{"_BumpMap","_NormalMap","_MaskMap","_MetallicGlossMap","_OcclusionMap","_ParallaxMap","_DetailAlbedoMap","_DetailNormalMap"})
                    if(_owned[i].HasProperty(property)) _owned[i].SetTexture(property,null);
                foreach(string keyword in new[]{"_NORMALMAP","_PARALLAXMAP","_DETAIL_MULX2"}) _owned[i].DisableKeyword(keyword);
                count++;
            }
            if(count==0) throw new InvalidOperationException("This piece has no supported opaque surface material.");
            _mesh=new RenovatorMesh(piece,_owned);
        }
        catch { Dispose(); throw; }
    }
    internal void Apply(Texture2D texture,int pattern,int scale)
    {
        if(_renderer==null) throw new InvalidOperationException("The building piece was removed.");
        if(pattern==0) { Restore(); return; }
        var current=_renderer.sharedMaterials;
        if(current.Length!=_original.Length) throw new InvalidOperationException("The game's material layout changed; close and reopen Renovator.");
        for(int i=0;i<current.Length;i++)
            if(current[i]!=_original[i]&&current[i]!=_owned[i]) throw new InvalidOperationException("Another effect is changing this piece. Try again when it finishes.");
        for(int i=0;i<current.Length;i++)
        {
            if(_owned[i]==null) continue;
            var material=_owned[i]; material.SetTexture(_maps[i],texture); material.SetTextureScale(_maps[i],Vector2.one*RenovatorPatterns.Repeats[scale]); material.SetTextureOffset(_maps[i],Vector2.zero);
            foreach(string property in new[]{"_BaseColor","_Color"}) if(material.HasProperty(property)) { Color color=material.GetColor(property); material.SetColor(property,new Color(1,1,1,color.a)); }
            current[i]=material;
        }
        _mesh.Apply(); _renderer.sharedMaterials=current; _attached=true;
    }
    internal void Restore()
    {
        _mesh?.Restore();
        if(!_attached||_renderer==null) return;
        var current=_renderer.sharedMaterials;
        for(int i=0;i<current.Length&&i<_owned.Length;i++) if(_owned[i]!=null&&current[i]==_owned[i]) current[i]=_original[i];
        _renderer.sharedMaterials=current; _attached=false;
    }
    public void Dispose()
    {
        Restore();
        _mesh?.Dispose(); _mesh=null;
        foreach(var material in _owned) if(material!=null) UnityEngine.Object.Destroy(material);
    }
}
