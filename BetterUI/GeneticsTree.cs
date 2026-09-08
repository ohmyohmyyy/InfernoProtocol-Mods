using System;
using System.Collections.Generic;
using Game.UI;
using Game.PlayerOperations;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InfernoProtocol.BetterUI;

// Read-only projection of the game's populated genetics fields. Never writes gene data.
internal sealed class GeneticsTree : IDisposable
{
    private sealed class Gene
    {
        internal int Id, Stage;
        internal string Name, Description;
        internal Sprite Icon;
        internal Vector2 Position;
        internal Image Halo;
        internal Image Glow;
        internal TextMeshProUGUI Caption;
    }
    private readonly List<Gene> _genes = new();
    private readonly List<InputActionMap> _maps = new();
    private readonly List<InputAction> _disabledActions = new();
    private GameObject _canvas, _drawing;
    private RectTransform _viewport, _space;
    private TextMeshProUGUI _name, _description, _stage, _count, _hint;
    private Image _detailIcon;
    private RectTransform _closeButton, _resetButton;
    private CanvasGroup _appearance;
    private readonly List<RectTransform> _sparks = new();
    private Sprite _soft;
    private Vector2 _focusTarget;
    private bool _focusing, _usingGamepad;
    private int _hovered = -1;
    private float _treeHeight;
    private CanvasGroup _nativeGroup;
    private bool _addedGroup, _nativeInteractable, _nativeBlocks, _look, _cursor, _active, _failed, _release;
    private float _nativeAlpha, _refresh, _opened, _repeat, _zoom = 1;
    private CursorLockMode _lock;
    private int _selected, _held;
    private Sprite _disc, _ring;
    private Vector2 _drag;
    private bool _dragging;
    private Color Accent => BetterUITheme.Accent;
    private readonly Color _ink = new(.88f, .96f, .96f);

    internal void Tick(bool enabled)
    {
        try
        {
            if (_release && Released()) RestoreInput();
            // The native isActive getter dereferences its singleton before the
            // gameplay UI exists. Startup/scene teardown is normal, not a fault.
            var native = GeneticFeaturesUI.instance;
            var content = native != null ? native.scollViewContent : null;
            bool ready = native != null && content != null;
            bool show = enabled && BetterUIPlugin.GeneticsEnabled.Value && !_failed && ready &&
                native.gameObject.activeInHierarchy && content.gameObject.activeInHierarchy && Application.isFocused;
            if (!show) { if (_active) Close(); return; }
            if (_release) return;
            if (!_active) Open();
            if (!_active) return;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            PlayerInputDispatcher.SetEnableLookInput(false);
            foreach (var map in _maps) if (map.enabled) map.Disable();
            if (Time.unscaledTime >= _refresh) { ReadGenes(); _refresh = Time.unscaledTime + .75f; }
            Input();
            if (_active) Animate();
        }
        catch (Exception e)
        {
            _failed = true; Close();
            BetterUIPlugin.ModLog.LogWarning("Genetics tree disabled for this session; native genetics restored: " + e);
        }
    }
    private void Open()
    {
        var native = GeneticFeaturesUI.instance;
        if (native == null || native.scollViewContent == null) return;
        _active = true; _opened = Time.unscaledTime;
        _lock = Cursor.lockState; _cursor = Cursor.visible;
        _look = PlayerInputDispatcher.actions?.look?.enabled == true;
        var maps = PlayerInputDispatcher.inputActionsAsset?.actionMaps;
        if (maps != null) foreach (InputActionMap map in maps) if (map.enabled)
        {
            foreach (InputAction action in map.actions) if (!action.enabled) _disabledActions.Add(action);
            _maps.Add(map); map.Disable();
        }
        _nativeGroup = native.GetComponent<CanvasGroup>();
        _addedGroup = _nativeGroup == null;
        if (_addedGroup) _nativeGroup = native.gameObject.AddComponent<CanvasGroup>();
        _nativeAlpha = _nativeGroup.alpha; _nativeInteractable = _nativeGroup.interactable; _nativeBlocks = _nativeGroup.blocksRaycasts;
        Build(); ReadGenes();
        _nativeGroup.alpha = 0; _nativeGroup.interactable = false; _nativeGroup.blocksRaycasts = false;
        BetterUIPlugin.ModLog.LogInfo("Genetics tree opened with " + _genes.Count + " native gene fields.");
        _refresh = Time.unscaledTime + .75f;
    }
    private void ReadGenes()
    {
        var native = GeneticFeaturesUI.instance;
        if (native == null || native.scollViewContent == null) return;
        var fields = native.scollViewContent.GetComponentsInChildren<GeneticFeaturesFieldUI>(false);
        var next = new List<Gene>();
        foreach (var field in fields)
        {
            if (field == null || !field.gameObject.activeInHierarchy) continue;
            int id = (int)field.attachedFeature;
            if (next.Exists(g => g.Id == id)) continue;
            next.Add(new Gene { Id = id, Stage = field.attachedStage, Name = field.title?.text ?? field.attachedFeature.ToString(),
                Description = field.description?.text ?? "", Icon = field.icon?.sprite });
        }
        next.Sort((a,b) => a.Id.CompareTo(b.Id));
        bool same = next.Count == _genes.Count;
        for (int i = 0; same && i < next.Count; i++) same = next[i].Id == _genes[i].Id && next[i].Stage == _genes[i].Stage &&
            next[i].Name == _genes[i].Name && next[i].Description == _genes[i].Description && next[i].Icon == _genes[i].Icon;
        if (same && _drawing != null) return;
        int keep = _genes.Count > 0 ? _genes[_selected].Id : -1;
        _genes.Clear(); _genes.AddRange(next);
        _selected = Math.Max(0, _genes.FindIndex(g => g.Id == keep));
        Draw(); Select(_selected, true);
    }
    private void Build()
    {
        _disc ??= Circle(false); _ring ??= Circle(true);
        _canvas = new GameObject("BetterUI_GeneticsTree", Il2CppType.Of<RectTransform>(), Il2CppType.Of<Canvas>(), Il2CppType.Of<CanvasScaler>(), Il2CppType.Of<GraphicRaycaster>());
        _canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.GetComponent<Canvas>().sortingOrder = 32100;
        var scaler = _canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        // One contained surface, with the world still visible around it.
        var shade = Box("Backdrop", _canvas.transform,0,0,1,1,new Color(.008f,.016f,.024f,.38f)); shade.raycastTarget=true;
        var bg = Box("Genome", _canvas.transform,.16f,.15f,.84f,.85f,new Color(.025f,.043f,.055f,.95f));
        bg.sprite=BetterUITheme.RoundedSprite; bg.type=Image.Type.Sliced;
        _appearance=bg.gameObject.AddComponent<CanvasGroup>(); _appearance.alpha=0;
        Label("Brand", bg.transform,.035f,.895f,.55f,.975f,"Your genetics",30,_ink);
        _count = Label("Count",bg.transform,.035f,.853f,.55f,.905f,"",16,new Color(.52f,.7f,.71f));
        _closeButton=Box("CloseHit",bg.transform,.905f,.895f,.975f,.975f,Color.clear).rectTransform;
        Label("Close",_closeButton,0,0,1,1,"×",30,_ink).alignment=TextAlignmentOptions.Center;
        _resetButton=Box("RecenterHit",bg.transform,.71f,.91f,.895f,.965f,new Color(Accent.r,Accent.g,Accent.b,.08f)).rectTransform;
        Label("Recenter",_resetButton,0,0,1,1,"Recenter",16,new Color(.6f,.8f,.8f)).alignment=TextAlignmentOptions.Center;
        var well = Box("TreeViewport",bg.transform,.025f,.12f,.69f,.835f,Color.clear);
        _viewport = well.rectTransform; well.gameObject.AddComponent<RectMask2D>();
        var space = Box("TreeSpace",well.transform,.5f,.5f,.5f,.5f,Color.clear);
        _space = space.rectTransform; _space.sizeDelta = new Vector2(1100,760);
        Box("DetailRule",bg.transform,.71f,.22f,.711f,.76f,new Color(Accent.r,Accent.g,Accent.b,.12f));
        _detailIcon = Box("GeneIcon",bg.transform,.76f,.68f,.85f,.80f,Color.white); _detailIcon.preserveAspect = true;
        _name = Label("GeneName",bg.transform,.755f,.565f,.965f,.68f,"",25,_ink);
        _stage = Label("Stage",bg.transform,.755f,.50f,.965f,.56f,"",15,Accent);
        _description = Label("Description",bg.transform,.755f,.19f,.965f,.485f,"",19,new Color(.67f,.8f,.81f));
        _description.alignment=TextAlignmentOptions.TopLeft;
        _hint = Label("Controls",bg.transform,.035f,.025f,.965f,.10f,"",15,new Color(.5f,.65f,.69f));
        _zoom = 1; _dragging = false; _hovered=-1; _held=0;
        _usingGamepad=Gamepad.current!=null;
    }
    private void Draw()
    {
        if (_drawing != null) { _drawing.SetActive(false); UnityEngine.Object.Destroy(_drawing); }
        _sparks.Clear();
        _drawing = Box("Branches",_space,0,0,1,1,Color.clear).gameObject;
        var root = _drawing.transform;
        int rows = Math.Max(1, (_genes.Count+1)/2);
        _treeHeight=GeneTreeLayout.Height(_genes.Count);
        Vector2 previousA = default, previousB = default;
        for (int i=0; i<=rows*12; i++)
        {
            float y = -_treeHeight/2+i*(_treeHeight/(rows*12)); float wave = Mathf.Sin(i*Mathf.PI/12)*18;
            var a = new Vector2(wave,y); var b = new Vector2(-wave,y);
            if (i>0) { Line(root,previousA,a,1.6f,new Color(Accent.r,Accent.g,Accent.b,.38f)); Line(root,previousB,b,1.6f,new Color(.32f,.62f,.69f,.3f)); }
            if (i%3==0) Line(root,a,b,1,new Color(Accent.r,Accent.g,Accent.b,.22f));
            previousA=a; previousB=b;
        }
        for (int i=0;i<_genes.Count;i++)
        {
            Gene gene = _genes[i]; float side = i%2==0?-1:1;
            var position=GeneTreeLayout.Position(i,_genes.Count);
            float y=position.Y;
            gene.Position = new Vector2(position.X,position.Y);
            Vector2 start = new Vector2(side*22,y-35), end=gene.Position;
            Vector2 prev=start;
            for(int s=1;s<=12;s++)
            {
                float t=s/12f, inv=1-t;
                Vector2 point = inv*inv*start+2*inv*t*new Vector2(end.x*.7f,y-35)+t*t*end;
                Line(root,prev,point,1.5f,new Color(Accent.r,Accent.g,Accent.b,.3f)); prev=point;
            }
            _soft ??= SoftGlow();
            gene.Glow=At("Bloom",root,end,new Vector2(135,135),new Color(Accent.r,Accent.g,Accent.b,.13f)); gene.Glow.sprite=_soft;
            gene.Halo=At("Halo",root,end,new Vector2(82,82),Accent); gene.Halo.sprite=_ring;
            var core=At("Gene",root,end,new Vector2(73,73),new Color(.045f,.085f,.10f)); core.sprite=_disc;
            var icon=At("Icon",root,end,new Vector2(44,44),Color.white); icon.sprite=gene.Icon; icon.preserveAspect=true; icon.enabled=gene.Icon!=null;
            var title=Label("Name",root,0,0,0,0,gene.Name,17,_ink);
            title.rectTransform.anchorMin=title.rectTransform.anchorMax=new Vector2(.5f,.5f);
            title.rectTransform.sizeDelta=new Vector2(180,48); title.rectTransform.anchoredPosition=end+new Vector2(0,-66);
            title.alignment=TextAlignmentOptions.Center; gene.Caption=title;
        }
        for(int i=0;i<2;i++) { var spark=At("Life",root,Vector2.zero,new Vector2(5,5),new Color(Accent.r,Accent.g,Accent.b,.5f)); spark.sprite=_disc; _sparks.Add(spark.rectTransform); }
        _count.text = _genes.Count==1?"1 discovered gene":$"{_genes.Count} discovered genes";
        if (_genes.Count==0) { _name.text="Room to grow"; _description.text="Your discovered genes will bloom here as you explore."; _stage.text="Your journey has just begun"; _detailIcon.enabled=false; }
    }
    private void Select(int index,bool focus)
    {
        if (_genes.Count==0) return;
        _selected=Mathf.Clamp(index,0,_genes.Count-1);
        Gene gene=_genes[_selected]; _name.text=gene.Name; _description.text=gene.Description;
        _stage.text="●  Part of you";
        _detailIcon.sprite=gene.Icon; _detailIcon.enabled=gene.Icon!=null;
        if(focus) { _focusTarget=new Vector2(0,-gene.Position.y*_zoom+20); _focusing=true; }
    }
    private void Input()
    {
        var k=Keyboard.current; var g=Gamepad.current; var m=Mouse.current;
        if(m!=null&&(m.delta.ReadValue().sqrMagnitude>4||m.leftButton.wasPressedThisFrame||Mathf.Abs(m.scroll.ReadValue().y)>.1f)) _usingGamepad=false;
        if(g!=null&&(g.dpad.ReadValue().sqrMagnitude>.1f||g.rightStick.ReadValue().sqrMagnitude>.08f||g.buttonNorth.wasPressedThisFrame||g.leftTrigger.isPressed||g.rightTrigger.isPressed)) _usingGamepad=true;
        _hint.text=_usingGamepad?"D-pad  Browse    ·    Stick  Pan    ·    LT / RT  Zoom    ·    Y  Recenter    ·    B  Close":"Click a gene    ·    Drag to explore    ·    Scroll to zoom    ·    Arrows to browse    ·    Esc to close";
        if(Time.unscaledTime-_opened<.25f) return;
        if(k?.escapeKey.wasPressedThisFrame==true||g?.buttonEast.wasPressedThisFrame==true) { GeneticFeaturesUI.Hide(); Close(); return; }
        int nav=(k?.downArrowKey.isPressed==true||g?.dpad.down.isPressed==true?2:0)-(k?.upArrowKey.isPressed==true||g?.dpad.up.isPressed==true?2:0)+
            (k?.rightArrowKey.isPressed==true||g?.dpad.right.isPressed==true?1:0)-(k?.leftArrowKey.isPressed==true||g?.dpad.left.isPressed==true?1:0);
        if(nav!=0&&(nav!=_held||Time.unscaledTime>_repeat)) { Navigate(nav); _repeat=Time.unscaledTime+(nav!=_held?.3f:.14f); } _held=nav;
        if(k?.homeKey.wasPressedThisFrame==true||g?.buttonNorth.wasPressedThisFrame==true) { Zoom(1-_zoom); Select(_selected,true); }
        if(g!=null) { var stick=g.rightStick.ReadValue(); if(stick.sqrMagnitude>.05f) { _focusing=false; _space.anchoredPosition+=stick*Mathf.Min(Time.unscaledDeltaTime,.05f)*420; } Zoom((g.rightTrigger.ReadValue()-g.leftTrigger.ReadValue())*Mathf.Min(Time.unscaledDeltaTime,.05f)*.7f); }
        if(m!=null)
        {
            var point=m.position.ReadValue();
            if(m.leftButton.wasPressedThisFrame&&RectTransformUtility.RectangleContainsScreenPoint(_closeButton,point,null)) { GeneticFeaturesUI.Hide(); Close(); return; }
            if(m.leftButton.wasPressedThisFrame&&RectTransformUtility.RectangleContainsScreenPoint(_resetButton,point,null)) { Zoom(1-_zoom); Select(_selected,true); }
            bool inside=RectTransformUtility.RectangleContainsScreenPoint(_viewport,point,null);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_viewport,point,null,out Vector2 local);
            if(inside) Zoom(Mathf.Clamp(m.scroll.ReadValue().y,-120,120)*.001f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_space,point,null,out Vector2 treePoint);
            _hovered=inside&&!_usingGamepad?HitGene(treePoint):-1;
            if(inside&&m.leftButton.wasPressedThisFrame)
            {
                int hit=HitGene(treePoint);
                if(hit>=0) Select(hit,false); else { _dragging=true; _focusing=false; _drag=local; }
            }
            if(_dragging&&m.leftButton.isPressed) { _space.anchoredPosition+=local-_drag; _drag=local; }
            else _dragging=false;
        }
        float limit=Math.Max(120,_treeHeight*.5f*_zoom);
        var pan=_space.anchoredPosition; _space.anchoredPosition=new Vector2(Mathf.Clamp(pan.x,-450*_zoom,450*_zoom),Mathf.Clamp(pan.y,-limit,limit));
    }
    private void Zoom(float delta)
    {
        if(Mathf.Abs(delta)<.0001f) return;
        _focusing=false;
        float next=Mathf.Clamp(_zoom+delta,.55f,1.6f);
        _space.anchoredPosition*=next/_zoom; _zoom=next; _space.localScale=new Vector3(next,next,1);
    }
    private int HitGene(Vector2 point)
    {
        for(int i=0;i<_genes.Count;i++)
        {
            Vector2 d=point-_genes[i].Position;
            if(d.sqrMagnitude<48*48||(Mathf.Abs(d.x)<90&&d.y<-42&&d.y>-90)) return i;
        }
        return -1;
    }
    private void Navigate(int direction)
    {
        if(_genes.Count==0) return;
        int best=GeneTreeLayout.Navigate(_selected,_genes.Count,direction);
        if(best!=_selected) Select(best,true);
    }
    private void Animate()
    {
        bool reduced=BetterUIPlugin.GeneticsReducedMotion.Value;
        float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
        _appearance.alpha=reduced?1:Mathf.Clamp01((Time.unscaledTime-_opened)/.22f);
        if(_focusing)
        {
            _space.anchoredPosition=reduced?_focusTarget:Vector2.Lerp(_space.anchoredPosition,_focusTarget,1-Mathf.Exp(-14*dt));
            if((_space.anchoredPosition-_focusTarget).sqrMagnitude<.25f) _focusing=false;
        }
        for(int i=0;i<_genes.Count;i++)
        {
            var gene=_genes[i]; bool selected=i==_selected, hover=i==_hovered;
            float breathe=reduced?0:Mathf.Sin((Time.unscaledTime-_opened)*1.7f)*.018f;
            float target=selected?1.06f+breathe:hover?1.035f:1;
            gene.Halo.rectTransform.localScale=Vector3.Lerp(gene.Halo.rectTransform.localScale,Vector3.one*target,reduced?1:1-Mathf.Exp(-12*dt));
            gene.Halo.color=selected?new Color(Accent.r,Accent.g,Accent.b,.9f):hover?new Color(Accent.r,Accent.g,Accent.b,.65f):new Color(Accent.r,Accent.g,Accent.b,.25f);
            gene.Glow.color=new Color(Accent.r,Accent.g,Accent.b,selected?.23f+breathe:hover?.14f:.04f);
            gene.Caption.color=selected?_ink:hover?Color.Lerp(_ink,Accent,.25f):new Color(.56f,.71f,.73f);
        }
        for(int i=0;i<_sparks.Count;i++)
        {
            _sparks[i].gameObject.SetActive(!reduced);
            if(reduced) continue;
            float t=Mathf.Repeat((Time.unscaledTime-_opened)*.065f+i*.5f,1);
            float y=(t-.5f)*_treeHeight;
            float x=Mathf.Sin(t*Mathf.PI*Math.Max(1,(_genes.Count+1)/2))*18*(i==0?1:-1);
            _sparks[i].anchoredPosition=new Vector2(x,y);
        }
    }
    private void Close()
    {
        if(!_active) return; _active=false; _release=true;
        if(_nativeGroup!=null) { _nativeGroup.alpha=_nativeAlpha; _nativeGroup.interactable=_nativeInteractable; _nativeGroup.blocksRaycasts=_nativeBlocks; if(_addedGroup) UnityEngine.Object.Destroy(_nativeGroup); }
        _nativeGroup=null;
        if(_canvas!=null) UnityEngine.Object.Destroy(_canvas);
        _canvas=null; _drawing=null; _genes.Clear();
    }
    private static bool Released()
    {
        var k=Keyboard.current; var g=Gamepad.current;
        return !(k?.anyKey.isPressed==true||Mouse.current?.leftButton.isPressed==true||g?.buttonEast.isPressed==true||g?.buttonNorth.isPressed==true||g?.dpad.ReadValue().sqrMagnitude>.01f||g?.leftTrigger.isPressed==true||g?.rightTrigger.isPressed==true);
    }
    private void RestoreInput()
    {
        foreach(var map in _maps) map?.Enable(); _maps.Clear();
        foreach(var action in _disabledActions) action?.Disable(); _disabledActions.Clear();
        PlayerInputDispatcher.SetEnableLookInput(_look); Cursor.visible=_cursor; Cursor.lockState=_lock; _release=false;
    }
    public void Dispose()
    {
        Close(); if(_release) RestoreInput();
        foreach(var sprite in new[]{_disc,_ring,_soft}) if(sprite!=null) { UnityEngine.Object.Destroy(sprite.texture); UnityEngine.Object.Destroy(sprite); }
        _disc=null; _ring=null; _soft=null;
    }
    private static Image Box(string name,Transform parent,float x0,float y0,float x1,float y1,Color color)
    {
        var go=new GameObject(name,Il2CppType.Of<RectTransform>(),Il2CppType.Of<CanvasRenderer>(),Il2CppType.Of<Image>()); go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false;
        image.rectTransform.anchorMin=new Vector2(x0,y0); image.rectTransform.anchorMax=new Vector2(x1,y1); image.rectTransform.offsetMin=image.rectTransform.offsetMax=Vector2.zero; return image;
    }
    private static Image At(string name,Transform parent,Vector2 pos,Vector2 size,Color color)
    {
        var image=Box(name,parent,.5f,.5f,.5f,.5f,color); image.rectTransform.sizeDelta=size; image.rectTransform.anchoredPosition=pos; return image;
    }
    private static void Line(Transform parent,Vector2 a,Vector2 b,float width,Color color)
    {
        var image=At("Strand",parent,(a+b)*.5f,new Vector2(Vector2.Distance(a,b),width),color);
        image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);
    }
    private static TextMeshProUGUI Label(string name,Transform parent,float x0,float y0,float x1,float y1,string text,int size,Color color)
    {
        var go=new GameObject(name,Il2CppType.Of<RectTransform>(),Il2CppType.Of<CanvasRenderer>(),Il2CppType.Of<TextMeshProUGUI>());
        go.transform.SetParent(parent,false);
        var tmp=go.GetComponent<TextMeshProUGUI>();
        tmp.rectTransform.anchorMin=new Vector2(x0,y0); tmp.rectTransform.anchorMax=new Vector2(x1,y1);
        tmp.rectTransform.offsetMin=tmp.rectTransform.offsetMax=Vector2.zero;
        tmp.text=text; tmp.fontSize=size; tmp.fontSizeMin=size*.8f; tmp.fontSizeMax=size; tmp.enableAutoSizing=true;
        tmp.color=color; tmp.raycastTarget=false; tmp.alignment=TextAlignmentOptions.MidlineLeft; tmp.enableWordWrapping=true; tmp.overflowMode=TextOverflowModes.Ellipsis; return tmp;
    }
    private static Sprite Circle(bool ring)
    {
        const int size=96; var pixels=new Color32[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
        {
            float r=new Vector2(x-47.5f,y-47.5f).magnitude;
            float alpha=Mathf.Clamp01(46-r)*(ring?Mathf.Clamp01(r-42):1);
            pixels[y*size+x]=new Color(1,1,1,alpha);
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false); texture.SetPixels32(pixels); texture.Apply(false,true);
        return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f));
    }
    private static Sprite SoftGlow()
    {
        const int size=96; var pixels=new Color32[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
        {
            float r=new Vector2(x-47.5f,y-47.5f).magnitude/47;
            float a=Mathf.Clamp01(1-r); a=a*a*(3-2*a);
            pixels[y*size+x]=new Color(1,1,1,a);
        }
        var texture=new Texture2D(size,size,TextureFormat.RGBA32,false); texture.SetPixels32(pixels); texture.Apply(false,true);
        return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f));
    }
}
