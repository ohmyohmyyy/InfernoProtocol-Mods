using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InfernoProtocol.Renovator;

internal sealed class RenovatorHomePanel:IDisposable
{
    private readonly GameObject _canvas,_panel;
    private readonly RectTransform[] _cards=new RectTransform[2];
    private readonly Image[] _backgrounds=new Image[2],_bars=new Image[2];
    private readonly TextMeshProUGUI[] _titles=new TextMeshProUGUI[2],_descriptions=new TextMeshProUGUI[2];
    private readonly TextMeshProUGUI _target,_hint;
    private readonly RectTransform _close;
    private readonly Image _closeBackground;
    private readonly Action _finish,_move,_cancel;
    private readonly Color _accent=new(.57f,.84f,.74f),_card=new(.065f,.095f,.10f,.98f),_hover=new(.105f,.155f,.15f,.98f),_muted=new(.62f,.73f,.72f);
    private int _selection,_hoverIndex=-1;
    private bool _canFinish,_controller;

    internal bool Open {get;private set;}
    internal bool Controller=>_controller;

    internal RenovatorHomePanel(Action finish,Action move,Action cancel)
    {
        _finish=finish; _move=move; _cancel=cancel;
        _canvas=new GameObject("RenovatorToolsUI",Il2CppType.Of<RectTransform>(),Il2CppType.Of<Canvas>(),Il2CppType.Of<CanvasScaler>(),Il2CppType.Of<GraphicRaycaster>());
        var canvas=_canvas.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=32205;
        var scaler=_canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight=.5f;

        var panel=Box("ToolList",_canvas.transform,.69f,.285f,.965f,.715f,new Color(.025f,.043f,.045f,.945f)); _panel=panel.gameObject; panel.raycastTarget=true;
        var rect=panel.rectTransform; rect.anchorMin=rect.anchorMax=new Vector2(1,.5f); rect.pivot=new Vector2(1,.5f); rect.sizeDelta=new Vector2(470,464); rect.anchoredPosition=new Vector2(-32,0);
        Box("EdgeAccent",panel.transform,0,0,.008f,1,new Color(_accent.r,_accent.g,_accent.b,.72f));
        Text(panel.transform,.065f,.91f,.93f,.95f,"RENOVATOR",12,_muted);
        Text(panel.transform,.065f,.84f,.93f,.91f,"Choose a tool",27,Color.white);
        _target=Text(panel.transform,.065f,.785f,.93f,.835f,"",14,_accent);
        Box("Rule",panel.transform,.065f,.765f,.93f,.768f,new Color(_accent.r,_accent.g,_accent.b,.22f));

        AddCard(0,.065f,.555f,.93f,.735f,"Surface finishes","Wallpaper, tile, wood and patterned materials");
        AddCard(1,.065f,.345f,.93f,.525f,"Move & rotate","Reposition, align, raise and rotate this object");
        _closeBackground=Box("Close",panel.transform,.065f,.205f,.93f,.285f,_card); _close=_closeBackground.rectTransform;
        Text(_closeBackground.transform,0,0,1,1,"Close",16,Color.white).alignment=TextAlignmentOptions.Center;
        _hint=Text(panel.transform,.065f,.055f,.93f,.17f,"",11,_muted); _hint.alignment=TextAlignmentOptions.Center;
        Hide();
    }

    private void AddCard(int index,float x0,float y0,float x1,float y1,string title,string description)
    {
        var card=Box("Tool"+index,_panel.transform,x0,y0,x1,y1,_card); _cards[index]=card.rectTransform; _backgrounds[index]=card;
        _bars[index]=Box("Selection",card.transform,0,0,.012f,1,_accent);
        _titles[index]=Text(card.transform,.055f,.53f,.94f,.86f,title,19,Color.white);
        _descriptions[index]=Text(card.transform,.055f,.17f,.94f,.52f,description,12,_muted);
    }

    internal void Show(string target,bool canFinish)
    {
        _target.text="TARGET  /  "+target; _canFinish=canFinish; _selection=canFinish?0:1;
        _controller=Gamepad.current?.rightStickButton.isPressed==true; _hoverIndex=-1; Open=true; _panel.SetActive(true); Refresh();
    }

    internal void Hide() { Open=false; _panel.SetActive(false); }

    private void Refresh(int hover=-1)
    {
        for(int i=0;i<2;i++)
        {
            bool enabled=i!=0||_canFinish,selected=i==_selection;
            _backgrounds[i].color=i==hover&&enabled?_hover:_card;
            _bars[i].gameObject.SetActive(selected&&enabled);
            _titles[i].color=!enabled?new Color(.38f,.46f,.45f):selected?_accent:Color.white;
            _descriptions[i].color=enabled?_muted:new Color(.31f,.38f,.37f);
            if(i==0) _descriptions[i].text=enabled?"Wallpaper, tile, wood and patterned materials":"Not available for this type of object";
        }
        _hint.text=_controller?"D-PAD CHOOSE  /  A OPEN  /  B CLOSE":"CLICK A TOOL  /  ARROWS + ENTER  /  ESC CLOSE";
    }

    private void Select(int delta)
    {
        int next=_selection;
        do next=(next+delta+2)%2; while(next==0&&!_canFinish);
        _selection=next; _hoverIndex=-1; Refresh();
    }

    private void Activate()
    {
        Hide();
        if(_selection==0&&_canFinish) _finish(); else _move();
    }

    internal void Tick()
    {
        var keyboard=Keyboard.current; var gamepad=Gamepad.current; var mouse=Mouse.current;
        if(keyboard?.escapeKey.wasPressedThisFrame==true||gamepad?.buttonEast.wasPressedThisFrame==true) { Hide(); _cancel(); return; }
        if(keyboard?.upArrowKey.wasPressedThisFrame==true||gamepad?.dpad.up.wasPressedThisFrame==true) Select(-1);
        if(keyboard?.downArrowKey.wasPressedThisFrame==true||gamepad?.dpad.down.wasPressedThisFrame==true) Select(1);
        if(keyboard?.enterKey.wasPressedThisFrame==true||gamepad?.buttonSouth.wasPressedThisFrame==true) { Activate(); return; }
        bool pad=gamepad?.dpad.ReadValue().sqrMagnitude>.1f;
        bool desktop=mouse?.delta.ReadValue().sqrMagnitude>1||keyboard?.anyKey.wasPressedThisFrame==true;
        if(pad&&!_controller) { _controller=true; Refresh(); } else if(desktop&&_controller) { _controller=false; Refresh(); }
        if(mouse==null) return;
        Vector2 point=mouse.position.ReadValue(); int hover=-1;
        for(int i=0;i<2;i++) if((i!=0||_canFinish)&&Hit(_cards[i],point)) hover=i;
        if(hover!=_hoverIndex) { _hoverIndex=hover; Refresh(hover); }
        _closeBackground.color=Hit(_close,point)?_hover:_card;
        if(!mouse.leftButton.wasPressedThisFrame) return;
        if(Hit(_close,point)) { Hide(); _cancel(); return; }
        if(hover>=0) { _selection=hover; Activate(); }
    }

    private static bool Hit(RectTransform rect,Vector2 point)=>RectTransformUtility.RectangleContainsScreenPoint(rect,point,null);
    private static void Place(RectTransform rect,float x0,float y0,float x1,float y1) { rect.anchorMin=new Vector2(x0,y0); rect.anchorMax=new Vector2(x1,y1); rect.offsetMin=rect.offsetMax=Vector2.zero; }
    private static Image Box(string name,Transform parent,float x0,float y0,float x1,float y1,Color color)
    {
        var go=new GameObject(name,Il2CppType.Of<RectTransform>(),Il2CppType.Of<CanvasRenderer>(),Il2CppType.Of<Image>()); go.transform.SetParent(parent,false);
        var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false; Place(image.rectTransform,x0,y0,x1,y1); return image;
    }
    private static TextMeshProUGUI Text(Transform parent,float x0,float y0,float x1,float y1,string text,int size,Color color)
    {
        var go=new GameObject("Text",Il2CppType.Of<RectTransform>(),Il2CppType.Of<CanvasRenderer>(),Il2CppType.Of<TextMeshProUGUI>()); go.transform.SetParent(parent,false);
        var label=go.GetComponent<TextMeshProUGUI>(); Place(label.rectTransform,x0,y0,x1,y1); label.text=text; label.fontSize=size; label.fontSizeMax=size; label.fontSizeMin=size*.8f; label.enableAutoSizing=true; label.enableWordWrapping=true; label.color=color; label.raycastTarget=false; label.alignment=TextAlignmentOptions.MidlineLeft; label.overflowMode=TextOverflowModes.Ellipsis; return label;
    }
    public void Dispose() { if(_canvas!=null) UnityEngine.Object.Destroy(_canvas); }
}
