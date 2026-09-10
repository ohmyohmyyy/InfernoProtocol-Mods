using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InfernoProtocol.Renovator;

internal sealed class RenovatorPanel:IDisposable
{
    private readonly GameObject _canvas,_panel,_prompt;
    private readonly RectTransform _panelRect,_previous,_next,_apply,_cancel;
    private readonly RectTransform[] _cards=new RectTransform[9];
    private readonly Image[] _backgrounds=new Image[9];
    private readonly Image[] _selectionBars=new Image[9];
    private readonly RawImage[] _swatches=new RawImage[9];
    private readonly TextMeshProUGUI[] _names=new TextMeshProUGUI[9];
    private readonly Image[] _sizes=new Image[4];
    private readonly TextMeshProUGUI[] _sizeLabels=new TextMeshProUGUI[4];
    private readonly Image _previousBackground,_nextBackground,_applyBackground,_cancelBackground;
    private readonly TextMeshProUGUI _pageLabel,_hint,_status,_selected;
    private readonly Func<int,Texture2D> _texture;
    private readonly Action<int,int> _preview,_save;
    private readonly Action _close;
    private int _pattern,_scale,_nav,_page,_hover=-1;
    private bool _controller,_promptVisible;
    private float _repeat;

    private readonly Color _accent=new(.57f,.84f,.74f);
    private readonly Color _card=new(.065f,.095f,.10f,.98f);
    private readonly Color _cardHover=new(.105f,.155f,.15f,.98f);
    private readonly Color _button=new(.09f,.135f,.14f,.98f);
    private readonly Color _muted=new(.62f,.73f,.72f);

    internal bool Open {get;private set;}

    internal RenovatorPanel(Func<int,Texture2D> texture,Action<int,int> preview,Action<int,int> save,Action close)
    {
        _preview=preview; _save=save; _close=close; _texture=texture;
        _canvas=new GameObject("RenovatorUI",Il2CppType.Of<RectTransform>(),Il2CppType.Of<Canvas>(),Il2CppType.Of<CanvasScaler>(),Il2CppType.Of<GraphicRaycaster>());
        _canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        _canvas.GetComponent<Canvas>().sortingOrder=32200;
        var scaler=_canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);
        scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight=.5f;

        var prompt=Box("RenovatorPrompt",_canvas.transform,.385f,.105f,.615f,.153f,new Color(.015f,.035f,.038f,.86f));
        _prompt=prompt.gameObject;
        Box("PromptAccent",prompt.transform,0,0,.008f,1,_accent);
        Text(prompt.transform,.05f,0,.95f,1,"RENOVATOR  /  R3  /  F6  /  MIDDLE MOUSE",14,_accent).alignment=TextAlignmentOptions.Center;

        var panel=Box("RenovatorPicker",_canvas.transform,.61f,.14f,.96f,.86f,new Color(.025f,.043f,.045f,.945f));
        _panel=panel.gameObject; _panelRect=panel.rectTransform; panel.raycastTarget=true;
        _panelRect.anchorMin=_panelRect.anchorMax=new Vector2(1,.5f);
        _panelRect.pivot=new Vector2(1,.5f);
        _panelRect.sizeDelta=new Vector2(548,792);
        _panelRect.anchoredPosition=new Vector2(-28,0);
        Box("EdgeAccent",panel.transform,0,0,.006f,1,new Color(_accent.r,_accent.g,_accent.b,.72f));
        Text(panel.transform,.055f,.948f,.94f,.976f,"SURFACE FINISHES",12,_muted);
        Text(panel.transform,.055f,.897f,.94f,.95f,"Renovator",28,Color.white);
        _selected=Text(panel.transform,.055f,.846f,.94f,.89f,"",16,_accent);
        Box("HeaderRule",panel.transform,.055f,.827f,.945f,.829f,new Color(_accent.r,_accent.g,_accent.b,.26f));

        for(int i=0;i<9;i++)
        {
            float x=.045f+(i%3)*.305f, y=.656f-(i/3)*.164f;
            var card=Box("Finish"+i,panel.transform,x,y,x+.29f,y+.146f,_card);
            _cards[i]=card.rectTransform; _backgrounds[i]=card;
            var swatch=new GameObject("Pattern",Il2CppType.Of<RectTransform>(),Il2CppType.Of<CanvasRenderer>(),Il2CppType.Of<RawImage>());
            swatch.transform.SetParent(card.transform,false);
            var raw=swatch.GetComponent<RawImage>(); raw.raycastTarget=false;
            _swatches[i]=raw;
            Place(raw.rectTransform,.04f,.285f,.96f,.95f);
            _selectionBars[i]=Box("Selection",card.transform,.04f,.255f,.96f,.275f,_accent);
            _names[i]=Text(card.transform,.04f,.015f,.96f,.245f,RenovatorPatterns.Names[i],14,Color.white);
            _names[i].alignment=TextAlignmentOptions.Center;
        }

        _previousBackground=Box("PreviousPage",panel.transform,.29f,.286f,.40f,.332f,_button);
        _previous=_previousBackground.rectTransform;
        Text(_previousBackground.transform,0,0,1,1,"<",20,_accent).alignment=TextAlignmentOptions.Center;
        _pageLabel=Text(panel.transform,.405f,.286f,.595f,.332f,"",14,_muted);
        _pageLabel.alignment=TextAlignmentOptions.Center;
        _nextBackground=Box("NextPage",panel.transform,.60f,.286f,.71f,.332f,_button);
        _next=_nextBackground.rectTransform;
        Text(_nextBackground.transform,0,0,1,1,">",20,_accent).alignment=TextAlignmentOptions.Center;

        Text(panel.transform,.055f,.224f,.27f,.265f,"PATTERN SIZE",12,_muted);
        string[] labels={"Large","Medium","Small","Fine"};
        for(int i=0;i<4;i++)
        {
            float x=.285f+i*.167f;
            _sizes[i]=Box("Size"+i,panel.transform,x,.221f,x+.156f,.266f,_button);
            _sizeLabels[i]=Text(_sizes[i].transform,0,0,1,1,labels[i],13,Color.white);
            _sizeLabels[i].alignment=TextAlignmentOptions.Center;
        }

        _status=Text(panel.transform,.055f,.148f,.945f,.207f,"",14,_muted);
        Box("FooterRule",panel.transform,.055f,.139f,.945f,.141f,new Color(_accent.r,_accent.g,_accent.b,.18f));
        _cancelBackground=Box("Cancel",panel.transform,.055f,.063f,.49f,.124f,_button);
        _cancel=_cancelBackground.rectTransform;
        Text(_cancelBackground.transform,0,0,1,1,"Back",17,Color.white).alignment=TextAlignmentOptions.Center;
        _applyBackground=Box("Apply",panel.transform,.51f,.063f,.945f,.124f,_accent);
        _apply=_applyBackground.rectTransform;
        Text(_applyBackground.transform,0,0,1,1,"Apply finish",17,new Color(.025f,.07f,.07f)).alignment=TextAlignmentOptions.Center;
        _hint=Text(panel.transform,.055f,.015f,.945f,.052f,"",11,_muted);
        _hint.alignment=TextAlignmentOptions.Center;

        Hide(); Prompt(false);
    }

    internal void Show(int pattern,int scale)
    {
        int catalogIndex=Array.IndexOf(RenovatorPatterns.Catalog,pattern);
        if(catalogIndex<0) { catalogIndex=0; pattern=0; }
        _pattern=pattern; _scale=scale; _page=catalogIndex/9; _nav=0; _hover=-1;
        _controller=Gamepad.current?.rightStickButton.isPressed==true;
        Open=true; _panel.SetActive(true); Prompt(false); Message(null); Refresh();
    }

    internal void Hide() { Open=false; _panel.SetActive(false); }
    internal void Prompt(bool show)
    {
        bool visible=show&&!Open;
        if(visible==_promptVisible) return;
        _promptVisible=visible; _prompt.SetActive(visible);
    }
    internal void Message(string message)
    {
        _status.text=message??"Previewing on the selected surface. Apply to save.";
        _status.color=message==null?_muted:new Color(.95f,.68f,.52f);
    }

    private void Refresh()
    {
        for(int i=0;i<9;i++)
        {
            int catalogIndex=_page*9+i;
            bool visible=catalogIndex<RenovatorPatterns.Catalog.Length;
            _cards[i].gameObject.SetActive(visible);
            if(!visible) continue;
            int index=RenovatorPatterns.Catalog[catalogIndex];
            bool selected=index==_pattern;
            _backgrounds[i].color=i==_hover?_cardHover:_card;
            _selectionBars[i].gameObject.SetActive(selected);
            _names[i].color=selected?_accent:Color.white;
            _swatches[i].texture=_texture(index);
            _names[i].text=RenovatorPatterns.Names[index];
        }
        _selected.text="PREVIEWING  /  "+RenovatorPatterns.Names[_pattern];
        _pageLabel.text="PAGE "+(_page+1)+" OF "+((RenovatorPatterns.Catalog.Length+8)/9);
        for(int i=0;i<4;i++)
        {
            bool selected=i==_scale&&_pattern!=0;
            _sizes[i].color=selected?_accent:_button;
            _sizeLabels[i].color=_pattern==0?new Color(.42f,.5f,.48f):selected?new Color(.025f,.07f,.07f):Color.white;
        }
        _hint.text=_controller?"D-PAD BROWSE  /  X SIZE  /  A APPLY  /  B BACK":"CLICK PREVIEW  /  WHEEL PAGE  /  ENTER APPLY  /  ESC BACK";
        _previousBackground.color=_page==0?new Color(.045f,.065f,.067f):_button;
        _nextBackground.color=_page==(RenovatorPatterns.Catalog.Length-1)/9?new Color(.045f,.065f,.067f):_button;
    }

    private void Select(int catalogIndex)
    {
        catalogIndex=Math.Clamp(catalogIndex,0,RenovatorPatterns.Catalog.Length-1);
        int index=RenovatorPatterns.Catalog[catalogIndex];
        if(index==_pattern) return;
        _pattern=index; _page=catalogIndex/9; Refresh(); _preview(_pattern,_scale);
    }

    private void Size(int scale)
    {
        if(_pattern==0||scale==_scale) return;
        _scale=scale; Refresh(); _preview(_pattern,_scale);
    }

    private void Page(int delta)
    {
        int next=Math.Clamp(_page+delta,0,(RenovatorPatterns.Catalog.Length-1)/9);
        if(next==_page) return;
        _page=next; _hover=-1; Refresh();
    }

    internal void Tick()
    {
        var keyboard=Keyboard.current; var gamepad=Gamepad.current; var mouse=Mouse.current;
        if(keyboard?.escapeKey.wasPressedThisFrame==true||gamepad?.buttonEast.wasPressedThisFrame==true) { _close(); return; }
        if(keyboard?.enterKey.wasPressedThisFrame==true||gamepad?.buttonSouth.wasPressedThisFrame==true) { _save(_pattern,_scale); return; }

        bool pad=gamepad?.dpad.ReadValue().sqrMagnitude>.1f||gamepad?.buttonWest.wasPressedThisFrame==true;
        bool desktop=mouse?.delta.ReadValue().sqrMagnitude>1||keyboard?.anyKey.wasPressedThisFrame==true;
        if(pad&&!_controller) { _controller=true; Refresh(); }
        else if(desktop&&_controller) { _controller=false; Refresh(); }

        if(keyboard?.xKey.wasPressedThisFrame==true||gamepad?.buttonWest.wasPressedThisFrame==true) Size((_scale+1)%4);
        if(keyboard?.pageUpKey.wasPressedThisFrame==true) Page(-1);
        if(keyboard?.pageDownKey.wasPressedThisFrame==true) Page(1);
        int nav=(keyboard?.rightArrowKey.isPressed==true||gamepad?.dpad.right.isPressed==true?1:0)-
            (keyboard?.leftArrowKey.isPressed==true||gamepad?.dpad.left.isPressed==true?1:0)+
            (keyboard?.downArrowKey.isPressed==true||gamepad?.dpad.down.isPressed==true?3:0)-
            (keyboard?.upArrowKey.isPressed==true||gamepad?.dpad.up.isPressed==true?3:0);
        if(nav!=0&&(nav!=_nav||Time.unscaledTime>=_repeat))
        {
            int current=Array.IndexOf(RenovatorPatterns.Catalog,_pattern);
            int start=_page==current/9?current:_page*9;
            Select(RenovatorNavigation.Move(start,nav,RenovatorPatterns.Catalog.Length));
            _repeat=Time.unscaledTime+(nav!=_nav?.3f:.15f);
        }
        _nav=nav;

        if(mouse==null) return;
        Vector2 point=mouse.position.ReadValue();
        _applyBackground.color=Hit(_apply,point)?new Color(.70f,.91f,.80f):_accent;
        _cancelBackground.color=Hit(_cancel,point)?_cardHover:_button;
        int hover=-1;
        for(int i=0;i<9;i++) if(_cards[i].gameObject.activeSelf&&Hit(_cards[i],point)) hover=i;
        if(hover!=_hover) { _hover=hover; Refresh(); }
        float scroll=mouse.scroll.ReadValue().y;
        if(Hit(_panelRect,point)&&scroll!=0) Page(scroll>0?-1:1);
        if(!mouse.leftButton.wasPressedThisFrame) return;
        if(Hit(_cancel,point)) { _close(); return; }
        if(Hit(_apply,point)) { _save(_pattern,_scale); return; }
        for(int i=0;i<4;i++) if(Hit(_sizes[i].rectTransform,point)) { Size(i); return; }
        if(Hit(_previous,point)) { Page(-1); return; }
        if(Hit(_next,point)) { Page(1); return; }
        for(int i=0;i<9;i++) if(_cards[i].gameObject.activeSelf&&Hit(_cards[i],point)) { Select(_page*9+i); return; }
    }

    private static bool Hit(RectTransform rect,Vector2 point)=>RectTransformUtility.RectangleContainsScreenPoint(rect,point,null);
    private static void Place(RectTransform rect,float x0,float y0,float x1,float y1)
    { rect.anchorMin=new Vector2(x0,y0); rect.anchorMax=new Vector2(x1,y1); rect.offsetMin=rect.offsetMax=Vector2.zero; }

    private static Image Box(string name,Transform parent,float x0,float y0,float x1,float y1,Color color)
    {
        var go=new GameObject(name,Il2CppType.Of<RectTransform>(),Il2CppType.Of<CanvasRenderer>(),Il2CppType.Of<Image>());
        go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false;
        Place(image.rectTransform,x0,y0,x1,y1);
        return image;
    }

    private static TextMeshProUGUI Text(Transform parent,float x0,float y0,float x1,float y1,string text,int size,Color color)
    {
        var go=new GameObject("Text",Il2CppType.Of<RectTransform>(),Il2CppType.Of<CanvasRenderer>(),Il2CppType.Of<TextMeshProUGUI>());
        go.transform.SetParent(parent,false);
        var label=go.GetComponent<TextMeshProUGUI>(); Place(label.rectTransform,x0,y0,x1,y1);
        label.text=text; label.fontSize=size; label.fontSizeMax=size; label.fontSizeMin=size*.8f; label.enableAutoSizing=true;
        label.enableWordWrapping=true; label.color=color; label.raycastTarget=false;
        label.alignment=TextAlignmentOptions.MidlineLeft; label.overflowMode=TextOverflowModes.Ellipsis;
        return label;
    }

    public void Dispose() { if(_canvas!=null) UnityEngine.Object.Destroy(_canvas); }
}
