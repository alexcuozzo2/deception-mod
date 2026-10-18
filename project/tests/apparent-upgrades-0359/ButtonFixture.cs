using System;
namespace UnityEngine
{
 public class GameObject:Object{}
 public class Sprite:Object{}
}
namespace PolytopiaBackendBase.Common{public enum SkinType{Default}}
public class Unit:UnityEngine.Object{public UnitState UnitState{get;set;}=new();}
public class UIUnitRenderer:UnityEngine.Object{public UnityEngine.GameObject gameObject{get;}=new();}
public static class UIUtils{public static UIUnitRenderer GetUIUnitRenderer(Polytopia.Data.UnitData data,PlayerState player,PolytopiaBackendBase.Common.SkinType skin)=>new();}
namespace UnityEngine.EventSystems{public class BaseEventData:Il2CppSystem.Object{}}
public class UIButtonBase:UnityEngine.Object
{
 public sealed class ButtonAction:Il2CppSystem.Object
 {
  readonly Action<int,UnityEngine.EventSystems.BaseEventData> Handler;public ButtonAction(Action<int,UnityEngine.EventSystems.BaseEventData> handler){Handler=handler;}
  public static ButtonAction op_Implicit(Action<int,UnityEngine.EventSystems.BaseEventData> handler)=>new(handler);public void Invoke()=>Handler(0,new());
 }
 public bool Active;public void SetActive(bool b){Active=b;}
}
public class UIRoundButton:UIButtonBase
{
 public string text{get;set;}="";public float Cost{get;set;}=-1;public bool buttonActive{get;set;}public bool ButtonEnabled{get;set;}
 public UIButtonBase.ButtonAction? OnClicked{get;set;}public UIButtonBase.ButtonAction? OnClickedWhenDisabled{get;set;}
 public UnityEngine.GameObject gameObject{get;}=new();public UIUnitRenderer? Renderer;public UnityEngine.Sprite? Sprite;
 public void SetUnitRenderer(UIUnitRenderer renderer){Renderer=renderer;}public void SetSprite(UnityEngine.Sprite sprite,bool preserve){Sprite=sprite;}
}
public class InteractionBar:UnityEngine.Object
{
 public Unit? unit{get;set;}public Il2CppSystem.Collections.Generic.List<UIRoundButton> buttons{get;set;}=new();public int Navigation;
 public UIRoundButton CreateRoundBottomBarButton(string text,bool active){var b=new UIRoundButton{text=text};buttons.Add(b);return b;}
 public void SetupButtonNavigation(){Navigation++;}public void ClearButtons(){}public void Reset(){}public void DeInit(){}public void OnDestroy(){}
}
namespace Deception.PolyModAdapter{internal static class UnitArtAssets{internal static readonly UnityEngine.Sprite Mask=new();internal static object Icon()=>Mask;}}
