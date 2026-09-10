using System;
using Game.Data;
using Game.PlayerOperations;
using UnityEngine;

namespace InfernoProtocol.Renovator;

/// <summary>
/// Borrows the game's own placement preview for an existing piece. The original
/// remains alive for save/network identity, but is hidden from rendering and snap
/// collision until the preview is accepted or cancelled.
/// </summary>
internal sealed class RenovatorPlacementPreview:IDisposable
{
    private APlacable _source;
    private Renderer[] _renderers=Array.Empty<Renderer>();
    private Collider[] _colliders=Array.Empty<Collider>();
    private bool[] _rendererStates=Array.Empty<bool>(),_colliderStates=Array.Empty<bool>();
    private bool _started;

    internal bool Active=>_started&&Player._placablePreviewTransform!=null;
    internal Transform Transform=>Active?Player._placablePreviewTransform:null;
    internal APlacable Placable=>Active?Player._placablePrefabPreview:null;
    internal bool IsSnapped=>Active&&Player._isSnapped;
    internal bool CanPlace=>Active&&Player._canPlace;

    internal bool Begin(APlacable source,out string error)
    {
        error=null;
        if(source==null) { error="The selected object is no longer available."; return false; }
        try
        {
            ItemDatabaseEntry entry=ItemDatabase.GetItemInfo(source.placableItem);
            if(entry==null||!entry.isPlaceable) { error="This object has no native placement preview."; return false; }

            Player.BeginPreviewPlacable(entry);
            if(Player._placablePreviewTransform==null||Player._placablePrefabPreview==null)
                throw new InvalidOperationException("The game did not create its placement preview.");

            _source=source;
            _started=true;
            Player._placablePreviewRotation=source.transform.rotation;
            Player._rawRotationY=source.transform.eulerAngles.y;
            Player._snappedDisplayY=source.transform.eulerAngles.y;
            Player._placablePreviewTransform.SetPositionAndRotation(source.transform.position,source.transform.rotation);
            HideSource();
            return true;
        }
        catch(Exception exception)
        {
            error="Native placement preview unavailable: "+exception.Message;
            End(false,Vector3.zero,Quaternion.identity);
            return false;
        }
    }

    private void HideSource()
    {
        _renderers=_source.GetComponentsInChildren<Renderer>(true);
        _rendererStates=new bool[_renderers.Length];
        for(int i=0;i<_renderers.Length;i++)
        {
            Renderer renderer=_renderers[i];
            if(renderer==null) continue;
            _rendererStates[i]=renderer.enabled;
            renderer.enabled=false;
        }

        _colliders=_source.GetComponentsInChildren<Collider>(true);
        _colliderStates=new bool[_colliders.Length];
        for(int i=0;i<_colliders.Length;i++)
        {
            Collider collider=_colliders[i];
            if(collider==null) continue;
            _colliderStates[i]=collider.enabled;
            collider.enabled=false;
        }
        Physics.SyncTransforms();
    }

    internal void End(bool apply,Vector3 position,Quaternion rotation)
    {
        if(_source!=null&&apply) _source.transform.SetPositionAndRotation(position,rotation);
        if(_started)
        {
            try { Player.EndPreviewPlacable(false); }
            catch(Exception exception) { RenovatorPlugin.Logger.LogDebug("Native move-preview cleanup deferred: "+exception.Message); }
        }
        RestoreSource();
        _source=null; _started=false;
        Physics.SyncTransforms();
    }

    private void RestoreSource()
    {
        for(int i=0;i<_renderers.Length;i++) if(_renderers[i]!=null) _renderers[i].enabled=_rendererStates[i];
        for(int i=0;i<_colliders.Length;i++) if(_colliders[i]!=null) _colliders[i].enabled=_colliderStates[i];
        _renderers=Array.Empty<Renderer>(); _rendererStates=Array.Empty<bool>();
        _colliders=Array.Empty<Collider>(); _colliderStates=Array.Empty<bool>();
    }

    public void Dispose()=>End(false,Vector3.zero,Quaternion.identity);
}
