using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // A read-only native action-bar control. Turn/action readiness is deliberately
    // irrelevant: current authorized observers may inspect while waiting.
    internal static class NativeSurveillanceUi
    {
        sealed record Control(object Bar,object Buttons,object Button,object Callback,object Interaction,
            NativeIntelligenceOverlay.ControlContext Context);
        static readonly Dictionary<long,Control> Controls=new();
        static readonly HashSet<string> Errors=new();
        static object? Interaction;static bool Syncing;
        internal static void Install(Harmony h)
        {
            foreach(var method in new[]{Native.M("ClientInteraction","SelectUnit","Unit"),Native.M("ClientInteraction","SelectTile","Tile")})
                Native.Hook(h,method,typeof(NativeSurveillanceUi),null,nameof(Selected));
            foreach(string name in new[]{"RefreshTileOptions","RefreshUnitOptions","RefreshPCBarInternal"})
                Native.Hook(h,Native.M("InteractionBar",name),typeof(NativeSurveillanceUi),null,nameof(Refreshed));
            foreach(string name in new[]{"ClearButtons","Reset","DeInit","OnDestroy"})
                Native.Hook(h,Native.M("InteractionBar",name),typeof(NativeSurveillanceUi),nameof(NativeClear));
            foreach(string name in new[]{"ClearSelection","DeselectUnit","OnSelectionCleared","OnTurnStarted","OnDisable"})
                Native.Hook(h,Native.M("ClientInteraction",name),typeof(NativeSurveillanceUi),nameof(Clear));
            Native.Hook(h,Native.M("GameManager","Update"),typeof(NativeSurveillanceUi),null,nameof(Tick));
        }
        static void Error(Exception e){if(Errors.Count<12&&Errors.Add(e.Message))Entry.Note("surveillance-ui-error",e.ToString());}
        static bool Owned(Control c)=>Native.UnityAlive(c.Bar)&&Native.UnityAlive(c.Button)&&Native.Same(Native.Get(c.Bar,"buttons"),c.Buttons)&&
            Native.Items(c.Buttons).Any(b=>Native.Same(b,c.Button))&&Native.Same(Native.Get(c.Button,"OnClicked"),c.Callback)&&(string?)Native.Get(c.Button,"text")=="Surveillance";
        static bool Context(object bar,out NativeIntelligenceOverlay.ControlContext context)
        {
            context=default;
            if(Interaction is null||!Native.UnityAlive(bar)||!(bool)Native.Get(Native.Get(bar,"gameObject")!,"activeInHierarchy")!||
                !Native.Same(Native.Invoke(Native.M("ClientInteraction","GetInteractionBar"),Interaction),bar)||
                !NativeIntelligenceOverlay.TryControlContext(Interaction,out context))return false;
            object? unit=Native.Get(Interaction,"selectedUnit"),tile=Native.Get(Interaction,"selectedTile");
            return unit is not null?Native.Same(unit,Native.Get(bar,"unit")):Native.Same(tile,Native.Get(bar,"tile"));
        }
        internal static object? OwnedButton(object bar)=>Controls.TryGetValue(Native.Pointer(bar),out var c)&&Owned(c)?c.Button:null;
        public static void Selected(object __instance){Clear();Interaction=__instance;Tick();}
        public static void Refreshed(object __instance)=>Sync(__instance);
        public static void Tick()
        {
            try
            {
                if(Interaction is null)return;
                var bar=Native.Invoke(Native.M("ClientInteraction","GetInteractionBar"),Interaction);
                if(bar is not null)Sync(bar);
            }
            catch(Exception e){Error(e);Clear();}
        }
        static void Sync(object bar)
        {
            if(Syncing)return;Syncing=true;
            try
            {
                long key=Native.Pointer(bar);
                if(!Context(bar,out var context)){Remove(bar);return;}
                if(Controls.TryGetValue(key,out var existing))
                {
                    if(Owned(existing)&&existing.Context==context)return;
                    Remove(bar);
                }
                // Control construction occurs only after current selection and
                // authority are established. Native factory appends immediately.
                if(Controls.Count>=8)ClearControls();
                object buttons=Native.Get(bar,"buttons")!,button=Native.Invoke(Native.M("InteractionBar","CreateRoundBottomBarButton","System.String","System.Boolean"),bar,"Surveillance",true)!;
                try
                {
                    Native.Invoke(Native.M("UIRoundButton","SetSprite","UnityEngine.Sprite","System.Boolean"),button,BranchArtAssets.Get(TechTree.IntelligenceId,true),true);
                    // Use native SetNativeSize + FitImageContentInParent, then
                    // preserve the normalized tech sprite's aspect in that fit.
                    Native.Set(Native.Get(button,"icon")!,"preserveAspect",true);
                    Control? control=null;object callback=Native.ButtonCallback(()=>{if(control is not null)Click(control);});
                    control=new(bar,buttons,button,callback,Interaction!,context);
                    Native.Set(button,"OnClicked",callback);Native.Set(button,"OnClickedWhenDisabled",null);Native.Set(button,"Cost",-1f);
                    Native.Set(button,"buttonActive",true);Native.Set(button,"ButtonEnabled",true);Native.Invoke(Native.M("UIButtonBase","SetActive","System.Boolean"),button,true);
                    Controls[key]=control;MimicPresentationUi.ReflowBar(bar);
                }
                catch
                {
                    if(Native.UnityAlive(bar)&&Native.Same(buttons,Native.Get(bar,"buttons"))&&Native.Items(buttons).Any(b=>Native.Same(b,button)))
                        Native.Invoke(Native.M(buttons.GetType(),"Remove","UIRoundButton"),buttons,button);
                    Destroy(button);throw;
                }
            }
            catch(Exception e){Error(e);Remove(bar);}
            finally{Syncing=false;}
        }
        static void Click(Control c)
        {
            try
            {
                if(!Controls.TryGetValue(Native.Pointer(c.Bar),out var owned)||!ReferenceEquals(c,owned)||!Owned(c)||
                    !Native.Same(Interaction,c.Interaction)||!Context(c.Bar,out var context)||context!=c.Context)return;
                NativeIntelligenceOverlay.TryToggleControl(c.Interaction,c.Context);
            }
            catch(Exception e){Error(e);}
        }
        static void Destroy(object button)
        {if(Native.UnityAlive(button))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(button,"gameObject"));}
        static void Remove(object bar)
        {
            if(!Controls.Remove(Native.Pointer(bar),out var c)||!Owned(c))return;
            Native.Set(c.Button,"OnClicked",null);Native.Set(c.Button,"OnClickedWhenDisabled",null);Native.Set(c.Button,"ButtonEnabled",false);
            Native.Invoke(Native.M(c.Buttons.GetType(),"Remove","UIRoundButton"),c.Buttons,c.Button);Destroy(c.Button);MimicPresentationUi.ReflowBar(bar);
        }
        public static void NativeClear(object __instance)
        {
            if(!Controls.Remove(Native.Pointer(__instance),out var c)||!Owned(c))return;
            Native.Set(c.Button,"OnClicked",null);Native.Set(c.Button,"OnClickedWhenDisabled",null);
        }
        static void ClearControls(){foreach(var c in Controls.Values.ToArray())Remove(c.Bar);}
        public static void Clear(){Interaction=null;ClearControls();}
    }
}
