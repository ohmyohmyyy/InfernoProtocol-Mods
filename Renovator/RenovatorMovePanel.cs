using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace InfernoProtocol.Renovator;

internal enum RenovatorMoveCommand
{
    Forward,Back,Left,Right,Up,Down,RotateLeft,RotateRight,
    ToggleSnap,CycleStep,CycleAngle,Reset,Apply,Cancel
}

internal sealed class RenovatorMovePanel:IDisposable
{
    private readonly GameObject _canvas,_panel;
    private readonly RectTransform _panelRect;
    private readonly Action<RenovatorMoveCommand> _command;
    private readonly TextMeshProUGUI _name,_position,_rotation,_status,_hint,_cancelLabel;
    private readonly RectTransform[] _buttons=new RectTransform[13];
    private readonly Image[] _backgrounds=new Image[13];
    private readonly TextMeshProUGUI[] _labels=new TextMeshProUGUI[13];
    private readonly Image _cancelBackground;
    private readonly Color _accent=new(.57f,.84f,.74f),_button=new(.075f,.115f,.12f,.94f),_hover=new(.12f,.18f,.18f,.97f),_muted=new(.62f,.73f,.72f);
    private bool _blocked,_snap,_hasInputHint,_controllerHint,_mouseLookHint,_cancelHovered;
    private int _heldButton=-1,_hoveredButton=-1;
    private float _mouseRepeat;

    internal bool Open {get;private set;}

    internal RenovatorMovePanel(Action<RenovatorMoveCommand> command)
    {
        _command=command;
        _canvas=new GameObject("RenovatorMoveUI",Il2CppType.Of<RectTransform>(),Il2CppType.Of<Canvas>(),Il2CppType.Of<CanvasScaler>(),Il2CppType.Of<GraphicRaycaster>());
        var canvas=_canvas.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=32210;
        var scaler=_canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight=.5f;

        var panel=Box("MoveWorkspace",_canvas.transform,.75f,.31f,.978f,.69f,new Color(.018f,.034f,.036f,.91f));
        _panel=panel.gameObject; _panelRect=panel.rectTransform; panel.raycastTarget=true;
        _panelRect.anchorMin=_panelRect.anchorMax=new Vector2(1,.5f); _panelRect.pivot=new Vector2(1,.5f); _panelRect.sizeDelta=new Vector2(420,410); _panelRect.anchoredPosition=new Vector2(-24,0);
        Box("EdgeAccent",panel.transform,0,0,.007f,1,new Color(_accent.r,_accent.g,_accent.b,.78f));
        Text(panel.transform,.055f,.93f,.94f,.97f,"RENOVATOR  /  PLACE",11,_muted);
        _name=Text(panel.transform,.055f,.85f,.94f,.93f,"",23,Color.white);
        Box("HeaderRule",panel.transform,.055f,.83f,.94f,.834f,new Color(_accent.r,_accent.g,_accent.b,.28f));
        _status=Text(panel.transform,.055f,.75f,.94f,.82f,"",12,_muted); _status.alignment=TextAlignmentOptions.Center;
        _position=Text(panel.transform,.055f,.695f,.68f,.745f,"",12,_muted);
        _rotation=Text(panel.transform,.68f,.695f,.94f,.745f,"",12,_muted); _rotation.alignment=TextAlignmentOptions.MidlineRight;

        AddButton(0,"Forward",.20f,.59f,.35f,.655f,"I  UP");
        AddButton(2,"Left",.045f,.515f,.19f,.58f,"J  LEFT");
        AddButton(1,"Back",.20f,.515f,.35f,.58f,"K  DOWN");
        AddButton(3,"Right",.36f,.515f,.505f,.58f,"L  RIGHT");
        AddButton(4,"Up",.55f,.59f,.745f,.655f,"R  HEIGHT +");
        AddButton(5,"Down",.755f,.59f,.95f,.655f,"F  HEIGHT -");
        AddButton(6,"RotateLeft",.55f,.515f,.745f,.58f,"Q  ROTATE <");
        AddButton(7,"RotateRight",.755f,.515f,.95f,.58f,"E  ROTATE >");
        AddButton(8,"Snap",.045f,.415f,.34f,.49f,"G  PRECISION");
        AddButton(9,"Step",.35f,.415f,.65f,.49f,"Z  STEP");
        AddButton(10,"Angle",.66f,.415f,.95f,.49f,"C  ANGLE");
        AddButton(11,"Reset",.045f,.33f,.95f,.395f,"HOME  CLEAR FINE ADJUSTMENTS");

        AddButton(12,"Apply",.51f,.205f,.95f,.30f,"LMB  Place");
        _cancelBackground=Box("Cancel",panel.transform,.045f,.205f,.49f,.30f,_button); _cancel=_cancelBackground.rectTransform;
        _cancelLabel=Text(_cancelBackground.transform,0,0,1,1,"ESC  Cancel",15,Color.white); _cancelLabel.alignment=TextAlignmentOptions.Center;
        _hint=Text(panel.transform,.045f,.045f,.95f,.18f,"",10,_muted); _hint.alignment=TextAlignmentOptions.Center;
        Hide();
    }

    private readonly RectTransform _cancel;

    private void AddButton(int index,string name,float x0,float y0,float x1,float y1,string label)
    {
        var image=Box(name,_panel.transform,x0,y0,x1,y1,index==12?_accent:_button);
        _buttons[index]=image.rectTransform; _backgrounds[index]=image;
        _labels[index]=Text(image.transform,0,0,1,1,label,14,index==12?new Color(.025f,.07f,.07f):Color.white); _labels[index].alignment=TextAlignmentOptions.Center;
    }

    internal void Show(string item,bool blocked,string message)
    {
        Open=true; _blocked=blocked; _heldButton=-1; _hoveredButton=-1; _cancelHovered=false; _panel.SetActive(true); SetText(_name,item);
        SetText(_status,message??"Changes preview live. Apply to keep them.");
        _status.color=blocked?new Color(.96f,.57f,.43f):_muted;
        Refresh(Vector3.zero,0,true,.1f,5);
    }

    internal void Refresh(Vector3 position,float yaw,bool snap,float step,float angle,bool structureSnapped=false,bool canPlace=true)
    {
        bool styleChanged=_snap!=snap; _snap=snap;
        SetText(_position,$"XYZ  {position.x:0.00}  {position.y:0.00}  {position.z:0.00}");
        SetText(_rotation,$"YAW  {yaw:0.#} deg");
        SetLabel(8,$"{(_controllerHint?"X  ":"G  ")}Precision: {(snap?"step":"free")}");
        SetLabel(9,$"{(_controllerHint?"L3  ":"Z  ")}Step: {step:0.##} m");
        SetLabel(10,$"{(_controllerHint?"R3  ":"C  ")}Angle: {angle:0.#} deg");
        SetLabel(11,_controllerHint?"Y  Clear adjustments":"HOME  Clear adjustments");
        SetLabel(12,_blocked?"Close":_controllerHint?"A  Place":"LMB  Place");
        if(!_blocked)
        {
            SetText(_status,!canPlace?"BLOCKED  /  Aim at a valid building surface":structureSnapped?"SNAPPED  /  Connected to a compatible piece":"FREE PLACEMENT  /  Compatible edges snap automatically");
            SetColor(_status,!canPlace?new Color(.96f,.57f,.43f):structureSnapped?_accent:_muted);
        }
        if(styleChanged) ResetColors();
    }

    internal void SetInputHint(bool controller,bool mouseLook)
    {
        if(_hasInputHint&&controller==_controllerHint&&mouseLook==_mouseLookHint) return;
        _hasInputHint=true; _controllerHint=controller; _mouseLookHint=mouseLook;
        SetLabel(0,controller?"D-PAD UP":"I  Up");
        SetLabel(1,controller?"D-PAD DOWN":"K  Down");
        SetLabel(2,controller?"D-PAD LEFT":"J  Left");
        SetLabel(3,controller?"D-PAD RIGHT":"L  Right");
        SetLabel(4,controller?"RT  Height +":"R  Height +");
        SetLabel(5,controller?"LT  Height -":"F  Height -");
        SetLabel(6,controller?"LB  Rotate":"Q  Rotate <");
        SetLabel(7,controller?"RB  Rotate":"E  Rotate >");
        SetText(_cancelLabel,controller?"B  Cancel":"ESC  Cancel");
        SetLabel(12,_blocked?"Close":controller?"A  Place":"LMB  Place");
        SetText(_hint,controller
            ?"STICKS WALK + AIM  /  D-PAD PRECISE NUDGE\nTRIGGERS HEIGHT  /  BUMPERS ROTATE  /  A APPLY  /  B BACK"
            :mouseLook
                ?"MOUSE AIM + WASD MATCH BUILD MODE  /  LMB PLACE  /  ESC CANCEL\nI-J-K-L NUDGE  /  R-F HEIGHT  /  Q-E ROTATE  /  HOLD ALT FOR PANEL"
                :"RELEASE ALT TO AIM LIKE BUILD MODE\nI-J-K-L PRECISE NUDGE  /  CLICK OR HOLD CONTROLS");
    }

    internal void SetBlocked(string message)
    {
        _blocked=true; _heldButton=-1; SetText(_status,message);
        _status.color=new Color(.96f,.57f,.43f); ResetColors();
    }

    internal void SetPlacementIssue(string message)
    {
        if(_blocked) return;
        SetText(_status,message); SetColor(_status,new Color(.96f,.57f,.43f));
    }

    private void SetLabel(int index,string value)
    {
        SetText(_labels[index],value);
    }

    internal void Tick()
    {
        var mouse=Mouse.current; if(mouse==null) return;
        Vector2 point=mouse.position.ReadValue();
        bool cancelHovered=Hit(_cancel,point);
        int hovered=-1;
        for(int i=0;i<_buttons.Length;i++)
        {
            bool enabled=!_blocked||i==12;
            if(enabled&&Hit(_buttons[i],point)) hovered=i;
        }
        if(hovered!=_hoveredButton||cancelHovered!=_cancelHovered)
        { _hoveredButton=hovered; _cancelHovered=cancelHovered; ResetColors(); }
        if(mouse.leftButton.wasReleasedThisFrame||!mouse.leftButton.isPressed||hovered!=_heldButton) _heldButton=-1;
        if(_heldButton>=0&&Time.unscaledTime>=_mouseRepeat)
        {
            _command((RenovatorMoveCommand)_heldButton);
            _mouseRepeat=Time.unscaledTime+.085f;
            return;
        }
        if(!mouse.leftButton.wasPressedThisFrame) return;
        if(Hit(_cancel,point)) { _command(RenovatorMoveCommand.Cancel); return; }
        if(_blocked) { if(Hit(_buttons[12],point)) _command(RenovatorMoveCommand.Cancel); return; }
        if(hovered>=0)
        {
            _command((RenovatorMoveCommand)hovered);
            if(hovered<8) { _heldButton=hovered; _mouseRepeat=Time.unscaledTime+.32f; }
        }
    }

    private void ResetColors()
    {
        Color darkText=new(.025f,.07f,.07f);
        for(int i=0;i<_backgrounds.Length;i++)
        {
            bool disabled=_blocked&&i!=12;
            _backgrounds[i].color=disabled?new Color(.05f,.065f,.067f,.9f):i==12?(_blocked?new Color(.11f,.14f,.14f):_accent):i==8&&_snap?_accent:_button;
            _labels[i].color=disabled?new Color(.36f,.43f,.42f):i==12&&_blocked?Color.white:(i==12||i==8&&_snap)?darkText:Color.white;
        }
        _cancelBackground.color=_button;
        if(_cancelHovered) _cancelBackground.color=_hover;
        if(_hoveredButton>=0)
            _backgrounds[_hoveredButton].color=_hoveredButton==12?new Color(.70f,.91f,.80f):_hover;
    }

    internal void Hide() { Open=false; _heldButton=-1; _hoveredButton=-1; _cancelHovered=false; _panel.SetActive(false); }
    private static void SetText(TextMeshProUGUI label,string value) { if(label.text!=value) label.text=value; }
    private static void SetColor(TextMeshProUGUI label,Color value) { if(label.color!=value) label.color=value; }
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
