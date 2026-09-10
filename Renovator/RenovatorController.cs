using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Game.LevelOperations;
using Game.Effects;
using Game.PlayerOperations;
using Game.UI;
using Il2CppInterop.Runtime.Attributes;
using SaveSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InfernoProtocol.Renovator;

public sealed class RenovatorChoice
{
    public int Pattern {get;set;}
    public int Scale {get;set;}=1;
}

public sealed class RenovatorController:MonoBehaviour
{
    private static readonly float[] MoveSteps={.05f,.1f,.25f,.5f,1f};
    private static readonly float[] AngleSteps={1f,5f,15f,45f,90f};
    private static readonly float[] FreeSpeeds={.35f,.7f,1.35f,2.5f,4f};
    private static readonly Dictionary<Items,bool> Eligibility=new();
    private readonly Dictionary<int,APlacable> _pieces=new();
    private readonly Dictionary<int,RenovatorFinish> _applied=new();
    private readonly HashSet<int> _pendingRestore=new();
    private readonly HashSet<int> _failed=new();
    private readonly List<int> _completed=new();
    private readonly List<string> _staleChoices=new();
    private Dictionary<string,RenovatorChoice> _saved=new();
    private readonly Texture2D[] _textures=new Texture2D[RenovatorPatterns.Names.Length];
    private readonly Texture2D[] _thumbnails=new Texture2D[RenovatorPatterns.Names.Length];
    private readonly RenovatorTargetCue _cue=new();
    private readonly RenovatorLookMaterialOverride _lookMaterialOverride=new();
    private readonly RenovatorPlacementPreview _placementPreview=new();
    private readonly List<InputActionMap> _maps=new();
    private readonly List<InputAction> _disabled=new();
    private RenovatorHomePanel _homeUi;
    private RenovatorPanel _ui;
    private RenovatorMovePanel _moveUi;
    private GameObject _inputBlocker;
    private APlacable _target;
    private APlacable _moveTarget;
    private Vector3 _moveStartPosition;
    private Quaternion _moveStartRotation;
    private Vector3 _movePrecisionOffset,_nativeBasePosition,_lastPreviewPosition;
    private Quaternion _nativeBaseRotation,_lastPreviewRotation;
    private string _file;
    private float _nextTarget,_nextWorld,_nextRestore,_nextMoveUiRefresh,_nextInputMaintenance,_nextHighlightMaintenance,_opened,_moveRepeat;
    private int _moveDirection,_moveStepIndex,_moveAngleIndex,_nativePlacementFrame=-1;
    private bool _scanned,_faulted,_reported,_release,_look,_move,_cursor,_inputCaptured,_moveBlocked,_moveController,_moveMouseLook,_previewPoseReady,_previewPoseApplied;
    private bool _readyThisFrame,_hammerThisFrame,_highlightSuppression,_moveInputStateKnown,_moveInputAllowsLook;
    private CursorLockMode _lock;

    public RenovatorController(IntPtr pointer):base(pointer) { }
    internal bool Editing=>_inputCaptured;

    internal void Register(APlacable piece)
    {
        if(piece==null||!Eligible(piece.placableItem)) return;
        int id=piece.GetInstanceID();
        bool first=!_pieces.ContainsKey(id);
        _pieces[id]=piece;
        if(first) _pendingRestore.Add(id);
    }

    internal void Forget(APlacable piece)
    {
        if(piece==null) return;
        if(piece==_moveTarget) CancelMove();
        if(piece==_target) SetTarget(null);
        int id=piece.GetInstanceID();
        if(_applied.Remove(id,out var finish)) finish.Dispose();
        _pieces.Remove(id); _pendingRestore.Remove(id); _failed.Remove(id);
    }

    internal bool SuppressHammerHighlight(Renderer renderer)
    {
        if(renderer==null||!RenovatorPlugin.Enabled.Value||!Hammer()) return false;
        try { EffectManager.UnhighlightRenderer(renderer); } catch { }
        return true;
    }

    private bool Ready()=>RenovatorPlugin.Enabled.Value&&Player.isLocalPlayerLoaded&&Player.localPlayer!=null&&Player.localPlayer.IsServer&&!Player.localPlayer.isDead;
    private bool Hammer()=>Player.localPlayer!=null&&Player.localPlayer.currentItemData.item==Items.BuildingHammer;
    private static bool CanMove(APlacable piece)=>piece!=null&&piece.placableId!=0&&piece.transform!=null;
    private static bool CanRenovate(APlacable piece)=>piece!=null&&piece.placableId!=0&&piece.objectRenderer!=null&&Eligible(piece.placableItem);
    private static bool Eligible(Items item)
    {
        if(Eligibility.TryGetValue(item,out bool eligible)) return eligible;
        eligible=RenovatorPatterns.Eligible(item.ToString()); Eligibility[item]=eligible; return eligible;
    }
    private static string Key(APlacable piece)=>piece.interiorIndex+":"+piece.placableId+":"+piece.placableItem;

    private void SetTarget(APlacable piece)
    {
        if(piece==_target) return;
        _target=piece;
        if(piece==null) _cue.Hide(); else _cue.Show(piece);
    }

    private void Update()
    {
        try
        {
            float now=Time.unscaledTime;
            if(_release&&Released()) ReleaseInput();
            _readyThisFrame=Ready(); _hammerThisFrame=_readyThisFrame&&Hammer();
            if(!_readyThisFrame)
            {
                if(_homeUi?.Open==true) CloseHome();
                if(_ui?.Open==true) Close(false);
                if(_moveUi?.Open==true) CancelMove();
                SetTarget(null); _ui?.Prompt(false);
                if(!Player.isLocalPlayerLoaded&&_file!=null) ResetWorld();
                return;
            }

            if(now>=_nextWorld)
            {
                _nextWorld=now+1;
                string file=SaveManager.GetSaveFile("Renovator.v1.json");
                if(file!=_file)
                {
                    ResetWorld();
                    if(string.IsNullOrWhiteSpace(file)) return;
                    _file=file;
                    try
                    {
                        // One-time compatibility bridge for finishes saved by the local prototype.
                        if(!File.Exists(file)) foreach(string legacyName in new[]{"Rennovator.v1.json","Wallpaper.v1.json"})
                        {
                            string legacy=SaveManager.GetSaveFile(legacyName);
                            if(string.IsNullOrWhiteSpace(legacy)||!File.Exists(legacy)) continue;
                            File.Copy(legacy,file);
                            RenovatorPlugin.Logger.LogInfo("Existing surface finishes migrated to Renovator.");
                            break;
                        }
                        _saved=File.Exists(file)?JsonSerializer.Deserialize<Dictionary<string,RenovatorChoice>>(File.ReadAllText(file)):new();
                        if(_saved==null) throw new InvalidDataException("Empty wallpaper save");
                        _staleChoices.Clear();
                        foreach(var pair in _saved)
                        {
                            RenovatorChoice choice=pair.Value;
                            if(choice==null||!RenovatorPatterns.StoredChoiceValid(choice.Pattern,choice.Scale)) throw new InvalidDataException("Invalid wallpaper choice");
                            if(!RenovatorPatterns.Available(choice.Pattern)) _staleChoices.Add(pair.Key);
                        }
                        foreach(string key in _staleChoices) _saved.Remove(key);
                        if(_staleChoices.Count>0)
                        {
                            WriteChoices(_saved);
                            RenovatorPlugin.Logger.LogInfo("Removed "+_staleChoices.Count+" retired surface finish preference(s).");
                        }
                    }
                    catch(Exception e)
                    {
                        _faulted=true;
                        RenovatorPlugin.Logger.LogError("Renovator preferences could not be read; preserving file and disabling writes: "+e.Message);
                    }
                }
            }
            if(_file==null||_faulted) return;

            if(!_scanned)
            {
                _scanned=true;
                foreach(var piece in UnityEngine.Object.FindObjectsOfType<APlacable>(false)) Register(piece);
            }
            if(_pendingRestore.Count>0&&now>=_nextRestore)
            {
                _nextRestore=now+.1f;
                RestorePending();
            }

            if(_moveUi?.Open==true)
            {
                if(!Application.isFocused||!_hammerThisFrame||_moveTarget==null||Player.mainCamera==null||!_moveTarget.gameObject.activeInHierarchy||PlayerInventoryUI.isOpen||(!_moveBlocked&&!_placementPreview.Active))
                { CancelMove(); return; }
                UpdateMoveControlMode();
                MaintainMoveInput();
                if(now-_opened>.25f) MoveTick();
                return;
            }

            if(_homeUi?.Open==true)
            {
                if(!Application.isFocused||!_hammerThisFrame||(!CanMove(_target)&&!CanRenovate(_target))||Player.mainCamera==null||!_target.gameObject.activeInHierarchy||PlayerInventoryUI.isOpen||Vector3.Distance(Player.mainCamera.transform.position,_target.transform.position)>5)
                { CloseHome(); return; }
                MaintainInputLock();
                if(now-_opened>.25f) _homeUi.Tick();
                return;
            }

            if(_ui?.Open==true)
            {
                if(!Application.isFocused||!_hammerThisFrame||!CanRenovate(_target)||Player.mainCamera==null||!_target.gameObject.activeInHierarchy||PlayerInventoryUI.isOpen||Vector3.Distance(Player.mainCamera.transform.position,_target.transform.position)>5)
                { Close(false); return; }
                MaintainInputLock();
                if(now-_opened>.25f) _ui.Tick();
                return;
            }

            // Cross-mod scene checks are deliberately not part of the idle path. On large
            // worlds, repeatedly searching the full hierarchy for an optional UI caused
            // severe frame-time spikes whenever Renovator itself was closed.
            if(_release||!_hammerThisFrame||!Application.isFocused||Cursor.visible||PlayerInventoryUI.isOpen||SkillWheelManager.isOpen)
            { SetTarget(null); _ui?.Prompt(false); return; }

            if(now>=_nextTarget)
            {
                _nextTarget=now+.08f;
                APlacable next=null;
                Camera camera=Player.mainCamera;
                if(camera!=null&&Physics.Raycast(camera.transform.position,camera.transform.forward,out RaycastHit hit,2.7f,~0,QueryTriggerInteraction.Ignore))
                {
                    var piece=hit.collider.GetComponentInParent<APlacable>();
                    if(piece!=null&&(piece.objectMeshFilter!=null||piece.objectRenderer!=null))
                    {
                        next=piece;
                        if(CanRenovate(piece)) Register(piece);
                    }
                }
                SetTarget(next);
                bool actionable=CanMove(_target)||CanRenovate(_target);
                if(actionable) EnsureUI();
                _ui?.Prompt(actionable);
            }

            if((CanMove(_target)||CanRenovate(_target))&&(Keyboard.current?.f6Key.wasPressedThisFrame==true||Mouse.current?.middleButton.wasPressedThisFrame==true||Gamepad.current?.rightStickButton.wasPressedThisFrame==true)&&!OtherEditorOpen())
                OpenHome();
        }
        catch(Exception e)
        {
            if(_homeUi?.Open==true) CloseHome();
            if(_ui?.Open==true) Close(false);
            if(_moveUi?.Open==true) CancelMove();
            _nextTarget=Time.unscaledTime+2;
            if(!_reported) { RenovatorPlugin.Logger.LogWarning("Renovator waiting/recovering: "+e); _reported=true; }
        }
    }

    private void LateUpdate()
    {
        try { if(_moveUi?.Open==true&&!_moveBlocked&&_nativePlacementFrame!=Time.frameCount) FollowPlacementPreview(); }
        catch(Exception e) { if(!_reported) { RenovatorPlugin.Logger.LogDebug("Move follow deferred: "+e.Message); _reported=true; } }
        try
        {
            bool suppress=_readyThisFrame&&_hammerThisFrame;
            float now=Time.unscaledTime;
            if(suppress!=_highlightSuppression||now>=_nextHighlightMaintenance)
            {
                _highlightSuppression=suppress; _nextHighlightMaintenance=now+(suppress?.1f:.5f);
                _lookMaterialOverride.Update(suppress);
            }
        }
        catch(Exception e) { if(!_reported) { RenovatorPlugin.Logger.LogDebug("Hammer highlight suppression deferred: "+e.Message); _reported=true; } }
    }

    private void EnsureHomeUI()=>_homeUi??=new RenovatorHomePanel(OpenFinishFromHome,OpenMoveFromHome,CloseHome);
    private void EnsureUI()=>_ui??=new RenovatorPanel(Thumbnail,Preview,Save,BackFromFinish);
    private void EnsureMoveUI()=>_moveUi??=new RenovatorMovePanel(HandleMoveCommand);

    private void CaptureInput()
    {
        if(_inputCaptured) return;
        _look=PlayerInputDispatcher.actions?.look?.enabled==true; _move=PlayerInputDispatcher.actions?.move?.enabled==true; _cursor=Cursor.visible; _lock=Cursor.lockState;
        var maps=PlayerInputDispatcher.inputActionsAsset?.actionMaps;
        if(maps!=null) foreach(InputActionMap map in maps) if(map.enabled)
        {
            foreach(InputAction action in map.actions) if(!action.enabled) _disabled.Add(action);
            _maps.Add(map); map.Disable();
        }
        _inputBlocker=new GameObject("Renovator_InputBlocker");
        _inputBlocker.hideFlags=HideFlags.HideAndDontSave;
        _inputCaptured=true; _nextInputMaintenance=0; MaintainInputLock(true);
    }

    private void MaintainInputLock(bool force=false)
    {
        float now=Time.unscaledTime;
        if(!force&&now<_nextInputMaintenance) return;
        _nextInputMaintenance=now+.25f;
        if(Cursor.lockState!=CursorLockMode.None) Cursor.lockState=CursorLockMode.None;
        if(!Cursor.visible) Cursor.visible=true;
        PlayerInputDispatcher.SetEnableLookInput(false);
        var maps=PlayerInputDispatcher.inputActionsAsset?.actionMaps;
        if(maps!=null) foreach(InputActionMap map in maps) if(map.enabled) map.Disable();
    }

    private void UpdateMoveControlMode()
    {
        var gamepad=Gamepad.current; var mouse=Mouse.current; var keyboard=Keyboard.current;
        bool padActive=gamepad!=null&&(
            gamepad.leftStick.ReadValue().sqrMagnitude>.02f||gamepad.rightStick.ReadValue().sqrMagnitude>.02f||gamepad.dpad.ReadValue().sqrMagnitude>.01f||
            gamepad.buttonSouth.wasPressedThisFrame||gamepad.buttonEast.wasPressedThisFrame||gamepad.buttonWest.wasPressedThisFrame||gamepad.buttonNorth.wasPressedThisFrame||
            gamepad.leftShoulder.isPressed||gamepad.rightShoulder.isPressed||gamepad.leftStickButton.wasPressedThisFrame||gamepad.rightStickButton.wasPressedThisFrame||
            gamepad.leftTrigger.ReadValue()>.1f||gamepad.rightTrigger.ReadValue()>.1f);
        bool mouseActive=mouse!=null&&(mouse.delta.ReadValue().sqrMagnitude>16f||mouse.leftButton.isPressed||mouse.rightButton.isPressed||mouse.middleButton.isPressed||mouse.scroll.ReadValue().sqrMagnitude>.01f);
        bool keyboardActive=keyboard!=null&&(keyboard.iKey.isPressed||keyboard.jKey.isPressed||keyboard.kKey.isPressed||keyboard.lKey.isPressed||
            keyboard.rKey.isPressed||keyboard.fKey.isPressed||keyboard.qKey.isPressed||keyboard.eKey.isPressed||keyboard.leftAltKey.isPressed);
        if(padActive) _moveController=true;
        else if(mouseActive||keyboardActive) _moveController=false;
        // Normal mouse movement aims the placement preview exactly like build mode.
        // Alt temporarily releases the cursor for the optional clickable controls.
        _moveMouseLook=!_moveController&&keyboard?.leftAltKey.isPressed!=true;
        _moveUi?.SetInputHint(_moveController,_moveMouseLook);
    }

    private void MaintainMoveInput()
    {
        InputAction moveAction=PlayerInputDispatcher.actions?.move,lookAction=PlayerInputDispatcher.actions?.look;
        bool allowLook=_moveController||_moveMouseLook;
        float now=Time.unscaledTime;
        bool changed=!_moveInputStateKnown||_moveInputAllowsLook!=allowLook;
        if(!changed&&now<_nextInputMaintenance) return;
        _moveInputStateKnown=true; _moveInputAllowsLook=allowLook; _nextInputMaintenance=now+.25f;
        var maps=PlayerInputDispatcher.inputActionsAsset?.actionMaps;
        if(maps!=null) foreach(InputActionMap map in maps) foreach(InputAction action in map.actions)
            if(action.enabled&&action!=moveAction&&action!=lookAction) action.Disable();
        if(moveAction?.enabled==false) moveAction.Enable();
        if(lookAction!=null&&lookAction.enabled!=allowLook) { if(allowLook) lookAction.Enable(); else lookAction.Disable(); }
        PlayerInputDispatcher.SetEnableLookInput(allowLook);
        CursorLockMode desiredLock=allowLook?CursorLockMode.Locked:CursorLockMode.None;
        if(Cursor.lockState!=desiredLock) Cursor.lockState=desiredLock;
        if(Cursor.visible==allowLook) Cursor.visible=!allowLook;
    }

    private static bool OtherEditorOpen()
    {
        // This only runs on the single frame where the player asks to open Renovator.
        // The shared Renovator_InputBlocker/Harmony gate handles conflicts after opening.
        if(GameObject.Find("Artistic_InputBlocker")!=null) return true;
        GameObject lights=GameObject.Find("BetterLights_Canvas");
        return lights?.transform.Find("Picker")?.gameObject.activeInHierarchy==true;
    }

    internal Texture2D Texture(int pattern)
    {
        if(_textures[pattern]!=null) return _textures[pattern];
        _textures[pattern]=GenerateTexture(pattern,512,true,4,"Renovator_Surface_");
        return _textures[pattern];
    }

    private Texture2D Thumbnail(int pattern)
    {
        if(_thumbnails[pattern]!=null) return _thumbnails[pattern];
        _thumbnails[pattern]=GenerateTexture(pattern,128,false,1,"Renovator_Thumbnail_");
        return _thumbnails[pattern];
    }

    private static Texture2D GenerateTexture(int pattern,int size,bool mipmaps,int anisotropy,string prefix)
    {
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
        {
            var c=RenovatorPatterns.Sample(pattern,(x+.5f)/size,(y+.5f)/size);
            pixels[y*size+x]=new Color(c.R,c.G,c.B,1);
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,mipmaps)
        {name=prefix+pattern,wrapMode=TextureWrapMode.Repeat,filterMode=mipmaps?FilterMode.Trilinear:FilterMode.Bilinear,anisoLevel=anisotropy};
        texture.SetPixels32(pixels); texture.Apply(mipmaps,true);
        return texture;
    }

    [HideFromIl2Cpp]
    private RenovatorFinish Finish(APlacable piece)
    {
        int id=piece.GetInstanceID();
        if(!_applied.TryGetValue(id,out var finish)) { finish=new RenovatorFinish(piece); _applied.Add(id,finish); }
        return finish;
    }

    private void RestorePending()
    {
        const int maxFinishRestores=4;
        int finishRestores=0;
        _completed.Clear(); _staleChoices.Clear();
        foreach(int id in _pendingRestore)
        {
            if(!_pieces.TryGetValue(id,out var piece)||piece==null)
            { _completed.Add(id); _pieces.Remove(id); _failed.Remove(id); continue; }
            if(!piece.gameObject.activeInHierarchy||piece.objectRenderer==null) continue;
            if(_applied.ContainsKey(id)||_failed.Contains(id)) { _completed.Add(id); continue; }

            string key=Key(piece);
            if(!_saved.TryGetValue(key,out var choice)||choice.Pattern==0) { _completed.Add(id); continue; }
            if(!piece.loadedFromSave)
            {
                _staleChoices.Add(key); _completed.Add(id);
                continue;
            }
            // Surface remapping can be expensive for the first instance of a mesh.
            // Spread loaded-base restoration across frames instead of hitching once.
            if(finishRestores>=maxFinishRestores) continue;
            finishRestores++;
            try
            {
                Finish(piece).Apply(Texture(choice.Pattern),choice.Pattern,choice.Scale);
                _completed.Add(id);
            }
            catch(Exception e)
            {
                if(_applied.Remove(id,out var bad)) bad.Dispose();
                _failed.Add(id); _completed.Add(id);
                RenovatorPlugin.Logger.LogWarning("Renovator restore skipped "+piece.placableItem+": "+e.Message);
            }
        }
        foreach(int id in _completed) _pendingRestore.Remove(id);

        if(_staleChoices.Count==0) return;
        var updated=new Dictionary<string,RenovatorChoice>(_saved);
        foreach(string key in _staleChoices) updated.Remove(key);
        try
        {
            WriteChoices(updated); _saved=updated;
            RenovatorPlugin.Logger.LogInfo("Renovator removed "+_staleChoices.Count+" stale placed-piece preference(s).");
        }
        catch(Exception e)
        {
            _faulted=true;
            RenovatorPlugin.Logger.LogError("Stale wallpaper preferences could not be cleaned safely; writes disabled: "+e.Message);
        }
    }

    private void OpenHome()
    {
        if(!CanMove(_target)&&!CanRenovate(_target)) return;
        EnsureHomeUI();
        CaptureInput();
        string name=_target.placableItem.ToString().Replace('_',' ');
        _homeUi.Show(name,CanRenovate(_target));
        _ui?.Prompt(false); _opened=Time.unscaledTime;
    }

    private void OpenFinishFromHome()
    {
        if(!CanRenovate(_target)) { CloseHome(); return; }
        OpenFinish();
    }

    private void BackFromFinish()
    {
        Close(false,false);
        if(CanReturnHome()) ShowHomeWithoutCapture();
        else _release=true;
    }

    private void OpenMoveFromHome()
    {
        if(!CanMove(_target)) { CloseHome(); return; }
        BeginMove(true);
    }

    private void CloseHome()
    {
        _homeUi?.Hide();
        if(_inputCaptured) _release=true;
    }

    private void ShowHomeWithoutCapture()
    {
        EnsureHomeUI();
        string name=_target.placableItem.ToString().Replace('_',' ');
        _homeUi.Show(name,CanRenovate(_target));
        _opened=Time.unscaledTime;
    }

    private bool CanReturnHome()=>CanMove(_target)&&Hammer()&&Application.isFocused&&!PlayerInventoryUI.isOpen&&Player.mainCamera!=null&&
        _target.gameObject.activeInHierarchy&&Vector3.Distance(Player.mainCamera.transform.position,_target.transform.position)<=5;

    private void OpenFinish()
    {
        if(!CanRenovate(_target)) return;
        EnsureUI();
        CaptureInput();
        _saved.TryGetValue(Key(_target),out var choice);
        _ui.Show(choice?.Pattern??0,choice?.Scale??1); _opened=Time.unscaledTime;
        try { Finish(_target); }
        catch(Exception e) { _ui.Message(e.Message); RenovatorPlugin.Logger.LogWarning(_target.placableItem+": "+e.Message); }
    }

    private void BeginMove(bool inputCaptured)
    {
        if(!CanMove(_target)) return;
        if(!inputCaptured) CaptureInput();
        EnsureMoveUI();
        _moveTarget=_target; _moveStartPosition=_target.transform.position; _moveStartRotation=_target.transform.rotation;
        _moveStepIndex=Math.Clamp(RenovatorPlugin.MoveStepIndex.Value,0,MoveSteps.Length-1);
        _moveAngleIndex=Math.Clamp(RenovatorPlugin.AngleStepIndex.Value,0,AngleSteps.Length-1);
        bool storageBlocked=HasStoredItems(_moveTarget);
        string previewError=null;
        bool previewReady=!storageBlocked&&_placementPreview.Begin(_moveTarget,out previewError);
        _moveBlocked=storageBlocked||!previewReady; _moveDirection=0; _nextMoveUiRefresh=0;
        _movePrecisionOffset=Vector3.zero; _previewPoseReady=false; _previewPoseApplied=false;
        _nativePlacementFrame=-1; _moveInputStateKnown=false; _nextInputMaintenance=0;
        _moveController=_homeUi?.Controller==true||Gamepad.current?.rightStickButton.isPressed==true;
        _moveMouseLook=!_moveController;
        string name=_moveTarget.placableItem.ToString().Replace('_',' ');
        string message=storageBlocked?"Storage contains items. Empty it before moving.":!previewReady?previewError:"Aim exactly as you do while building. Compatible edges snap automatically.";
        _moveUi.Show(name,_moveBlocked,message);
        _moveUi.SetInputHint(_moveController,_moveMouseLook);
        RefreshMove(); _ui?.Prompt(false); _opened=Time.unscaledTime;
    }

    private static bool HasStoredItems(APlacable piece)
    {
        if(piece==null||ItemStash.allStashes==null) return false;
        foreach(ItemStash stash in ItemStash.allStashes)
        {
            if(stash==null||stash.attachedPlacable!=piece||stash.inventory==null) continue;
            for(int i=0;i<stash.inventory.Count;i++) if(stash.inventory[i].count>0) return true;
        }
        return false;
    }

    private void MoveTick()
    {
        if(!_moveController&&!_moveMouseLook) _moveUi.Tick();
        if(_moveUi?.Open!=true) return;
        var keyboard=Keyboard.current; var gamepad=Gamepad.current; var mouse=Mouse.current;
        if(keyboard?.escapeKey.wasPressedThisFrame==true||gamepad?.buttonEast.wasPressedThisFrame==true) { CancelMove(true); return; }
        if(keyboard?.enterKey.wasPressedThisFrame==true||gamepad?.buttonSouth.wasPressedThisFrame==true||(_moveMouseLook&&mouse?.leftButton.wasPressedThisFrame==true))
        { if(_moveBlocked) CancelMove(true); else ApplyMove(); return; }
        if(_moveBlocked) return;
        if(keyboard?.gKey.wasPressedThisFrame==true||gamepad?.buttonWest.wasPressedThisFrame==true) HandleMoveCommand(RenovatorMoveCommand.ToggleSnap);
        if(keyboard?.homeKey.wasPressedThisFrame==true||gamepad?.buttonNorth.wasPressedThisFrame==true) HandleMoveCommand(RenovatorMoveCommand.Reset);
        if(keyboard?.zKey.wasPressedThisFrame==true||gamepad?.leftStickButton.wasPressedThisFrame==true) HandleMoveCommand(RenovatorMoveCommand.CycleStep);
        if(keyboard?.cKey.wasPressedThisFrame==true||gamepad?.rightStickButton.wasPressedThisFrame==true) HandleMoveCommand(RenovatorMoveCommand.CycleAngle);

        if(RenovatorPlugin.MoveSnap.Value)
        {
            int direction=(keyboard?.iKey.isPressed==true||gamepad?.dpad.up.isPressed==true?1:0)|
                (keyboard?.kKey.isPressed==true||gamepad?.dpad.down.isPressed==true?2:0)|
                (keyboard?.jKey.isPressed==true||gamepad?.dpad.left.isPressed==true?4:0)|
                (keyboard?.lKey.isPressed==true||gamepad?.dpad.right.isPressed==true?8:0)|
                (keyboard?.rKey.isPressed==true||(gamepad?.rightTrigger.ReadValue()??0)>.55f?16:0)|
                (keyboard?.fKey.isPressed==true||(gamepad?.leftTrigger.ReadValue()??0)>.55f?32:0)|
                (keyboard?.qKey.isPressed==true||gamepad?.leftShoulder.isPressed==true?64:0)|
                (keyboard?.eKey.isPressed==true||gamepad?.rightShoulder.isPressed==true?128:0);
            if(direction!=0&&(direction!=_moveDirection||Time.unscaledTime>=_moveRepeat))
            {
                if((direction&1)!=0) HandleMoveCommand(RenovatorMoveCommand.Forward);
                if((direction&2)!=0) HandleMoveCommand(RenovatorMoveCommand.Back);
                if((direction&4)!=0) HandleMoveCommand(RenovatorMoveCommand.Left);
                if((direction&8)!=0) HandleMoveCommand(RenovatorMoveCommand.Right);
                if((direction&16)!=0) HandleMoveCommand(RenovatorMoveCommand.Up);
                if((direction&32)!=0) HandleMoveCommand(RenovatorMoveCommand.Down);
                if((direction&64)!=0) HandleMoveCommand(RenovatorMoveCommand.RotateLeft);
                if((direction&128)!=0) HandleMoveCommand(RenovatorMoveCommand.RotateRight);
                _moveRepeat=Time.unscaledTime+(direction!=_moveDirection?.28f:.11f);
            }
            _moveDirection=direction;
        }
        else
        {
            _moveDirection=0;
            Vector2 axis=gamepad?.dpad.ReadValue()??Vector2.zero;
            axis+=new Vector2((keyboard?.lKey.isPressed==true?1:0)-(keyboard?.jKey.isPressed==true?1:0),(keyboard?.iKey.isPressed==true?1:0)-(keyboard?.kKey.isPressed==true?1:0));
            if(axis.sqrMagnitude>1) axis.Normalize();
            float vertical=(keyboard?.rKey.isPressed==true?1:0)-(keyboard?.fKey.isPressed==true?1:0)+(gamepad?.rightTrigger.ReadValue()??0)-(gamepad?.leftTrigger.ReadValue()??0);
            float turn=(keyboard?.eKey.isPressed==true?1:0)-(keyboard?.qKey.isPressed==true?1:0)+(gamepad?.rightShoulder.isPressed==true?1:0)-(gamepad?.leftShoulder.isPressed==true?1:0);
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f),speed=FreeSpeeds[_moveStepIndex];
            bool changed=false;
            if(axis.sqrMagnitude>.001f) { Translate(CameraDirection(axis.x,axis.y,false)*speed*dt); changed=true; }
            if(Mathf.Abs(vertical)>.01f) { Translate(Vector3.up*vertical*speed*dt); changed=true; }
            if(Mathf.Abs(turn)>.01f) { Rotate(turn*AngleSteps[_moveAngleIndex]*4*dt); changed=true; }
            if(changed) RefreshMove();
        }
    }

    // Managed UI callback. Exposing its Renovator-owned enum to IL2CPP while this
    // assembly is still being registered causes a misleading assembly lookup error.
    [HideFromIl2Cpp]
    private void HandleMoveCommand(RenovatorMoveCommand command)
    {
        if(_moveUi?.Open!=true) return;
        if(_moveBlocked)
        {
            if(command is RenovatorMoveCommand.Cancel or RenovatorMoveCommand.Apply) CancelMove(true);
            return;
        }
        float step=MoveSteps[_moveStepIndex],angle=AngleSteps[_moveAngleIndex];
        switch(command)
        {
            case RenovatorMoveCommand.Forward: Translate(CameraDirection(0,1,true)*step); break;
            case RenovatorMoveCommand.Back: Translate(CameraDirection(0,-1,true)*step); break;
            case RenovatorMoveCommand.Left: Translate(CameraDirection(-1,0,true)*step); break;
            case RenovatorMoveCommand.Right: Translate(CameraDirection(1,0,true)*step); break;
            case RenovatorMoveCommand.Up: Translate(Vector3.up*step); break;
            case RenovatorMoveCommand.Down: Translate(Vector3.down*step); break;
            case RenovatorMoveCommand.RotateLeft: Rotate(-angle); break;
            case RenovatorMoveCommand.RotateRight: Rotate(angle); break;
            case RenovatorMoveCommand.ToggleSnap: RenovatorPlugin.MoveSnap.Value=!RenovatorPlugin.MoveSnap.Value; break;
            case RenovatorMoveCommand.CycleStep: _moveStepIndex=(_moveStepIndex+1)%MoveSteps.Length; RenovatorPlugin.MoveStepIndex.Value=_moveStepIndex; break;
            case RenovatorMoveCommand.CycleAngle: _moveAngleIndex=(_moveAngleIndex+1)%AngleSteps.Length; RenovatorPlugin.AngleStepIndex.Value=_moveAngleIndex; break;
            case RenovatorMoveCommand.Reset:
                _movePrecisionOffset=Vector3.zero;
                if(_placementPreview.Active)
                {
                    Player._placablePreviewRotation=_moveStartRotation;
                    Player._rawRotationY=_moveStartRotation.eulerAngles.y;
                    Player._snappedDisplayY=_moveStartRotation.eulerAngles.y;
                    _previewPoseReady=false; _previewPoseApplied=false;
                }
                break;
            case RenovatorMoveCommand.Apply: ApplyMove(); return;
            case RenovatorMoveCommand.Cancel: CancelMove(true); return;
        }
        RefreshMove();
    }

    private Vector3 CameraDirection(float x,float z,bool cardinal)
    {
        Camera camera=Player.mainCamera;
        Vector3 forward=camera!=null?camera.transform.forward:Vector3.forward; forward.y=0;
        if(forward.sqrMagnitude<.001f) forward=Vector3.forward; else forward.Normalize();
        Vector3 right=new(forward.z,0,-forward.x);
        Vector3 direction=right*x+forward*z;
        if(!cardinal) return direction;
        if(Mathf.Abs(direction.x)>Mathf.Abs(direction.z)) return new Vector3(Mathf.Sign(direction.x),0,0);
        return new Vector3(0,0,Mathf.Sign(direction.z));
    }

    private void Translate(Vector3 delta)
    {
        if(_moveTarget==null||!_placementPreview.Active) return;
        _movePrecisionOffset+=delta;
        if(_movePrecisionOffset.sqrMagnitude>400) _movePrecisionOffset=_movePrecisionOffset.normalized*20;
        ApplyPreviewOffset();
    }

    private void FollowPlacementPreview()
    {
        if(!_placementPreview.Active) return;
        Transform preview=_placementPreview.Transform;
        Vector3 observedPosition=preview.position;
        Quaternion observedRotation=preview.rotation;
        bool stillOurPose=_previewPoseApplied&&(observedPosition-_lastPreviewPosition).sqrMagnitude<.000001f&&Quaternion.Angle(observedRotation,_lastPreviewRotation)<.001f;
        if(!_previewPoseReady||!stillOurPose)
        {
            _nativeBasePosition=observedPosition;
            _nativeBaseRotation=observedRotation;
            _previewPoseReady=true;
        }
        ApplyPreviewOffset();
        RefreshMove(false);
    }

    internal void AfterNativePlacementUpdate()
    {
        if(_moveUi?.Open!=true||_moveBlocked||!_placementPreview.Active) return;
        try
        {
            // A postfix guarantees the observed transform is the game's fresh result,
            // before another system can overwrite Renovator's fine adjustment.
            _previewPoseReady=false; _previewPoseApplied=false;
            _nativePlacementFrame=Time.frameCount;
            FollowPlacementPreview();
        }
        catch(Exception exception)
        {
            if(!_reported) { RenovatorPlugin.Logger.LogWarning("Native placement offset deferred: "+exception.Message); _reported=true; }
        }
    }

    private void ApplyPreviewOffset()
    {
        Transform preview=_placementPreview.Transform;
        if(preview==null) return;
        if(!_previewPoseReady)
        {
            _nativeBasePosition=preview.position; _nativeBaseRotation=preview.rotation; _previewPoseReady=true;
        }
        _lastPreviewPosition=_nativeBasePosition+_movePrecisionOffset;
        _lastPreviewRotation=_nativeBaseRotation;
        bool changed=(preview.position-_lastPreviewPosition).sqrMagnitude>.0000001f||Quaternion.Angle(preview.rotation,_lastPreviewRotation)>.0001f;
        if(changed) preview.SetPositionAndRotation(_lastPreviewPosition,_lastPreviewRotation);
        _previewPoseApplied=true;
        if(changed) Physics.SyncTransforms();
    }

    private void Rotate(float degrees)
    {
        if(_moveTarget==null||!_placementPreview.Active) return;
        Player._placablePreviewRotation=Quaternion.AngleAxis(degrees,Vector3.up)*Player._placablePreviewRotation;
        Player._rawRotationY+=degrees;
        Player._snappedDisplayY+=degrees;
        _previewPoseReady=false; _previewPoseApplied=false;
    }

    private void RefreshMove(bool force=true)
    {
        if(_moveTarget==null||_moveUi?.Open!=true) return;
        float now=Time.unscaledTime;
        if(!force&&now<_nextMoveUiRefresh) return;
        _nextMoveUiRefresh=now+.05f;
        APlacable cueTarget=_placementPreview.Active?_placementPreview.Placable:_moveTarget;
        if(cueTarget!=null) _cue.Show(cueTarget);
        Transform shown=_placementPreview.Active?_placementPreview.Transform:_moveTarget.transform;
        _moveUi.Refresh(shown.position,shown.eulerAngles.y,RenovatorPlugin.MoveSnap.Value,MoveSteps[_moveStepIndex],AngleSteps[_moveAngleIndex],_placementPreview.IsSnapped,_placementPreview.CanPlace);
    }

    private void ApplyMove()
    {
        if(_moveTarget==null||_moveBlocked) { CancelMove(); return; }
        if(_placementPreview.Active&&!_placementPreview.CanPlace)
        {
            _moveUi.SetPlacementIssue("That position is blocked. Aim at a valid surface or snap point.");
            return;
        }
        if(HasStoredItems(_moveTarget))
        {
            _placementPreview.End(false,Vector3.zero,Quaternion.identity);
            _moveTarget.transform.SetPositionAndRotation(_moveStartPosition,_moveStartRotation); Physics.SyncTransforms();
            _moveBlocked=true; _moveUi.SetBlocked("Items were added to this storage. Move cancelled; empty it first."); RefreshMove();
            return;
        }
        if(_placementPreview.Active)
        {
            Transform preview=_placementPreview.Transform;
            Vector3 position=preview.position; Quaternion rotation=preview.rotation;
            _placementPreview.End(true,position,rotation);
        }
        Physics.SyncTransforms();
        RenovatorPlugin.Logger.LogInfo("Moved "+_moveTarget.placableItem+" / "+Key(_moveTarget));
        _moveUi.Hide(); _moveTarget=null; _moveBlocked=false; _moveMouseLook=false; _previewPoseReady=false; _previewPoseApplied=false;
        _nativePlacementFrame=-1; _moveInputStateKnown=false; _release=true;
    }

    private void CancelMove(bool returnHome=false)
    {
        _placementPreview.End(false,Vector3.zero,Quaternion.identity);
        if(_moveTarget!=null&&!_moveBlocked)
        {
            _moveTarget.transform.SetPositionAndRotation(_moveStartPosition,_moveStartRotation); Physics.SyncTransforms();
            _cue.Show(_moveTarget);
        }
        _moveUi?.Hide(); _moveTarget=null; _moveBlocked=false; _moveMouseLook=false; _previewPoseReady=false; _previewPoseApplied=false;
        _nativePlacementFrame=-1; _moveInputStateKnown=false;
        if(returnHome&&CanReturnHome()) ShowHomeWithoutCapture();
        else _release=true;
    }

    private void Preview(int pattern,int scale)
    {
        if(!CanRenovate(_target)||!RenovatorPatterns.Valid(pattern,scale)) return;
        try { Finish(_target).Apply(pattern==0?null:Texture(pattern),pattern,scale); _ui.Message(null); }
        catch(Exception e) { _ui.Message(e.Message); }
    }

    private void Save(int pattern,int scale)
    {
        if(!CanRenovate(_target)||_faulted||!RenovatorPatterns.Valid(pattern,scale)) return;
        try
        {
            Finish(_target).Apply(pattern==0?null:Texture(pattern),pattern,scale);
            var updated=new Dictionary<string,RenovatorChoice>(_saved);
            if(pattern==0) updated.Remove(Key(_target));
            else updated[Key(_target)]=new RenovatorChoice {Pattern=pattern,Scale=scale};
            WriteChoices(updated); _saved=updated;
            if(pattern==0) ReleaseFinish(_target);
            RenovatorPlugin.Logger.LogInfo("Renovator saved for "+Key(_target)+": "+RenovatorPatterns.Names[pattern]);
            Close(true);
        }
        catch(Exception e) { _ui.Message("Not saved: "+e.Message); }
    }

    [HideFromIl2Cpp]
    private void WriteChoices(Dictionary<string,RenovatorChoice> choices)
    {
        string temp=_file+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(choices));
        if(File.Exists(_file)) File.Replace(temp,_file,_file+".bak"); else File.Move(temp,_file);
    }

    private void Close(bool committed,bool releaseInput=true)
    {
        if(_ui?.Open!=true) return;
        if(!committed&&_target!=null&&_applied.TryGetValue(_target.GetInstanceID(),out var finish))
        {
            try
            {
                if(_saved.TryGetValue(Key(_target),out var choice)) finish.Apply(Texture(choice.Pattern),choice.Pattern,choice.Scale);
                else { finish.Restore(); ReleaseFinish(_target); }
            }
            catch(Exception e) { RenovatorPlugin.Logger.LogWarning("Renovator preview cleanup: "+e.Message); }
        }
        _ui.Hide(); if(releaseInput) _release=true;
    }

    private void ReleaseFinish(APlacable piece)
    {
        if(piece!=null&&_applied.Remove(piece.GetInstanceID(),out var finish)) finish.Dispose();
    }

    private static bool Released()
    {
        var g=Gamepad.current;
        return !(Keyboard.current?.anyKey.isPressed==true||Mouse.current?.leftButton.isPressed==true||Mouse.current?.middleButton.isPressed==true||Mouse.current?.rightButton.isPressed==true||
            g?.buttonSouth.isPressed==true||g?.buttonEast.isPressed==true||g?.buttonWest.isPressed==true||g?.buttonNorth.isPressed==true||
            g?.leftStickButton.isPressed==true||g?.rightStickButton.isPressed==true||g?.leftShoulder.isPressed==true||g?.rightShoulder.isPressed==true||
            (g?.leftTrigger.ReadValue()??0)>.1f||(g?.rightTrigger.ReadValue()??0)>.1f||(g?.leftStick.ReadValue().sqrMagnitude??0)>.01f||(g?.rightStick.ReadValue().sqrMagnitude??0)>.01f||g?.dpad.ReadValue().sqrMagnitude>.01f);
    }

    private void ReleaseInput()
    {
        foreach(var map in _maps) map?.Enable(); _maps.Clear();
        foreach(var action in _disabled) action?.Disable(); _disabled.Clear();
        if(!_move) PlayerInputDispatcher.actions?.move?.Disable();
        if(!_look) PlayerInputDispatcher.actions?.look?.Disable();
        PlayerInputDispatcher.SetEnableLookInput(_look); Cursor.visible=_cursor; Cursor.lockState=_lock;
        if(_inputBlocker!=null) UnityEngine.Object.Destroy(_inputBlocker); _inputBlocker=null;
        _release=false; _inputCaptured=false; _moveInputStateKnown=false; _nextInputMaintenance=0;
    }

    private void ResetWorld()
    {
        if(_moveUi?.Open==true) CancelMove();
        if(_homeUi?.Open==true) CloseHome();
        Close(false);
        foreach(var finish in _applied.Values) finish.Dispose();
        _applied.Clear(); _pieces.Clear(); _pendingRestore.Clear(); _failed.Clear(); _saved.Clear();
        SetTarget(null); _file=null; _faulted=false; _scanned=false; _reported=false;
    }

    public void Cleanup()
    {
        ResetWorld();
        if(_release) ReleaseInput();
        _placementPreview.Dispose(); _lookMaterialOverride.Dispose(); _cue.Dispose(); _homeUi?.Dispose(); _homeUi=null; _ui?.Dispose(); _ui=null; _moveUi?.Dispose(); _moveUi=null;
        foreach(var texture in _textures) if(texture!=null) UnityEngine.Object.Destroy(texture);
        foreach(var texture in _thumbnails) if(texture!=null) UnityEngine.Object.Destroy(texture);
        Array.Clear(_textures,0,_textures.Length);
        Array.Clear(_thumbnails,0,_thumbnails.Length);
    }

    private void OnDestroy()=>Cleanup();
}
