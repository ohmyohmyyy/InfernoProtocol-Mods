using System;
using Game.PlayerOperations;
using UnityEngine;

namespace InfernoProtocol.Renovator;

/// <summary>
/// The Building Hammer's idle target wash is not EffectManager.highlightMaterial.
/// Player.UpdateLookPlacable renders Player._lookPlaceMaterial over the selected
/// mesh. Mutating that shared runtime material makes already-bound copies vanish
/// as well, while the backup lets us restore the game exactly on unload.
/// </summary>
internal sealed class RenovatorLookMaterialOverride:IDisposable
{
    private Material _material,_backup;
    private int[] _colors=Array.Empty<int>(),_floats=Array.Empty<int>();
    private static readonly int[] ColorProperties={Shader.PropertyToID("_BaseColor"),Shader.PropertyToID("_Color"),Shader.PropertyToID("_EmissionColor"),Shader.PropertyToID("_OutlineColor"),Shader.PropertyToID("_TintColor")};
    private static readonly int[] FloatProperties={Shader.PropertyToID("_Alpha"),Shader.PropertyToID("_Opacity"),Shader.PropertyToID("_Intensity"),Shader.PropertyToID("_EmissionIntensity")};

    internal void Update(bool enabled)
    {
        if(!enabled) { Restore(); return; }
        Material current=null;
        try { current=Player._lookPlaceMaterial; } catch { return; }
        if(current==null) return;
        if(_material!=current)
        {
            Restore();
            _material=current;
            _backup=new Material(current) {name="Renovator_LookPlaceMaterial_Backup"};
            _colors=Supported(current,ColorProperties);
            _floats=Supported(current,FloatProperties);
        }
        MakeInvisible();
    }

    private static int[] Supported(Material material,int[] candidates)
    {
        int count=0;
        foreach(int property in candidates) if(material.HasProperty(property)) count++;
        if(count==0) return Array.Empty<int>();
        var result=new int[count]; int index=0;
        foreach(int property in candidates) if(material.HasProperty(property)) result[index++]=property;
        return result;
    }

    private void MakeInvisible()
    {
        // The game can restore this shared material when the hammer is re-equipped,
        // so maintenance remains in LateUpdate. Avoid the expensive property writes
        // when it is already hidden, which is the normal case for nearly every frame.
        foreach(int property in _colors)
            if(_material.GetColor(property)!=Color.clear) _material.SetColor(property,Color.clear);
        foreach(int property in _floats)
            if(_material.GetFloat(property)!=0) _material.SetFloat(property,0);
        if(_material.IsKeywordEnabled("_EMISSION")) _material.DisableKeyword("_EMISSION");
    }

    private void Restore()
    {
        if(_material!=null&&_backup!=null) _material.CopyPropertiesFromMaterial(_backup);
        if(_backup!=null) UnityEngine.Object.Destroy(_backup);
        _material=null; _backup=null; _colors=Array.Empty<int>(); _floats=Array.Empty<int>();
    }

    public void Dispose()=>Restore();
}
