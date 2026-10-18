using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine.EventSystems {public class BaseEventData{}}
public class UIButtonBase:UnityEngine.Object
{
    public sealed class ButtonAction:Identity
    {
        readonly Action<int,UnityEngine.EventSystems.BaseEventData> Action;
        public ButtonAction(Action<int,UnityEngine.EventSystems.BaseEventData> a){Action=a;}
        public static implicit operator ButtonAction(Action<int,UnityEngine.EventSystems.BaseEventData> a)=>new(a);
        public void Click()=>Action(0,new());
    }
    public GameObject gameObject{get;}=new("native-button");public ButtonAction? OnClicked{get;set;}public ButtonAction? OnClickedWhenDisabled{get;set;}
    public bool buttonActive{get;set;}public bool ButtonEnabled{get;set;}
    public void SetActive(bool active)=>gameObject.SetActive(active);
}
public sealed class UIRoundButton:UIButtonBase
{
    public string text{get;set;}="";public float Cost{get;set;}public Sprite? Icon;public bool NativeSize;
    public UnityEngine.UI.Image icon{get;}=new();
    public void SetSprite(Sprite sprite,bool nativeSize){if(Deception.PolyModAdapter.BranchArtAssets.Fail)throw new Exception("icon-failure");Icon=sprite;NativeSize=nativeSize;}
}
namespace UnityEngine.UI {public sealed class Image {public bool preserveAspect{get;set;}}}
public sealed class NativeButtons:List<UIRoundButton>{readonly Identity Native=new();public IntPtr Pointer=>Native.Pointer;}
public class InteractionBar:UnityEngine.Object
{
    public GameObject gameObject{get;}=new("bar");public Unit? unit{get;set;}public Tile? tile{get;set;}public NativeButtons buttons{get;set;}=new();
    public InteractionBar(){gameObject.SetActive(true);}
    public UIRoundButton CreateRoundBottomBarButton(string text,bool bottom){var b=new UIRoundButton{text=text};buttons.Add(b);return b;}
    public void RefreshTileOptions(){}public void RefreshUnitOptions(){}public void RefreshPCBarInternal(){}
    public void ClearButtons(){}public void Reset(){}public void DeInit(){}public void OnDestroy(){}
}
namespace Deception.PolyModAdapter
{
    internal static class BranchArtAssets
    {internal static bool Fail;internal static readonly Sprite Icon=new();internal static object Get(string id,bool unlock)=>Icon;}
}
