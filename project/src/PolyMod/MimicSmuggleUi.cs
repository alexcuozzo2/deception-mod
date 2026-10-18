using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native action widgets and native build confirmation/dispatch. Authoritative
    // eligibility and resource mutation belong exclusively to the command adapter.
    internal static class MimicSmuggleUi
    {
        const string IT="Polytopia.Data.ImprovementData+Type";
        const string Description="Real action. Harvest this resource for your home city. Can be used after moving. Spends this unit's remaining action.";
        sealed class Control
        {
            internal object Bar=null!,Unit=null!,Button=null!,Buttons=null!,Command=null!;
            internal object? Callback;
            internal bool Active,HandedToNative;
        }
        static readonly Dictionary<long,Control> Controls=new();
        static readonly HashSet<string> Errors=new(StringComparer.Ordinal);
        internal sealed class PopupScope { internal PopupScope? Previous; internal bool Active; internal object? Popup; }
        [ThreadStatic] static PopupScope? CurrentPopup;
        public static void Install(Harmony h)
        {
            foreach(string name in new[]{"ClearButtons","Reset","DeInit","OnDestroy"})
                Native.Hook(h,Native.M("InteractionBar",name),typeof(MimicSmuggleUi),nameof(NativeClear));
            Native.Hook(h,Native.M("InteractionBar","ClickedImprovement","BuildCommand"),typeof(MimicSmuggleUi),nameof(BeforeClicked),null,nameof(AfterClicked));
            Native.Hook(h,Native.M("PopupManager","GetIconPopup"),typeof(MimicSmuggleUi),null,nameof(PopupCreated));
            Native.Hook(h,Native.M("UIUtils","GetImprovementSprite",IT,"PolytopiaBackendBase.Common.TribeType","PolytopiaBackendBase.Common.SkinType","SpriteAtlasManager"),typeof(MimicSmuggleUi),nameof(SpriteAlias));
            Native.Hook(h,Native.M("UIUtils","GetImprovementSprite","SkinVisualsTransientData",IT,"SpriteAtlasManager"),typeof(MimicSmuggleUi),nameof(TransientSpriteAlias));
            _=Native.M("BasicPopup","SetHeader","System.String");_=Native.M("BasicPopup","SetDescription","System.String");
            _=Native.M("Tile","get_Resource");_=Native.M("Resource","get_Sprite");
        }
        public static void BeforeClicked(object __0,out PopupScope __state)
        {
            __state=new PopupScope{Previous=CurrentPopup,Active=MimicSmuggleActions.IsSmuggle(__0)};
            CurrentPopup=__state;
        }
        public static Exception? AfterClicked(Exception? __exception,PopupScope __state)
        {
            try
            {
                if(__exception is null&&__state.Active&&__state.Popup is not null)
                {
                    Native.Invoke(Native.M("BasicPopup","SetHeader","System.String"),__state.Popup,"Smuggle");
                    Native.Invoke(Native.M("BasicPopup","SetDescription","System.String"),__state.Popup,Description);
                }
            }
            catch(Exception e){Note(e);}
            finally{CurrentPopup=__state.Previous;}
            return __exception;
        }
        public static void PopupCreated(object __result)
        {if(CurrentPopup?.Active==true)CurrentPopup.Popup=__result;}
        static Polytopia.Data.ImprovementData.Type Alias(Polytopia.Data.ImprovementData.Type value)
        {
            string id=Native.EnumName(value);
            var h=SmuggleCatalog.Harvests.FirstOrDefault(x=>x.Id==id);
            return h.Id is null?value:(Polytopia.Data.ImprovementData.Type)Native.EnumValue(IT,h.NativeHarvest);
        }
        public static void SpriteAlias(ref Polytopia.Data.ImprovementData.Type __0)=>__0=Alias(__0);
        public static void TransientSpriteAlias(ref Polytopia.Data.ImprovementData.Type __1)=>__1=Alias(__1);
        static bool Owned(Control c)
        {
            if(!Native.UnityAlive(c.Bar)||!Native.UnityAlive(c.Button))return false;
            var buttons=Native.Get(c.Bar,"buttons");
            return buttons is not null&&Native.Same(buttons,c.Buttons)&&Native.Items(buttons).Any(x=>Native.Same(x,c.Button))&&
                (string?)Native.Get(c.Button,"text")=="Smuggle"&&
                (c.Callback is null?Native.Get(c.Button,"OnClicked") is null:Native.Same(Native.Get(c.Button,"OnClicked"),c.Callback));
        }
        internal static object? OwnedButton(object bar)=>Controls.TryGetValue(Native.Pointer(bar),out var c)&&Owned(c)?c.Button:null;
        static bool Valid(Control c)
        {
            if(!MimicPresentationUi.CanSmuggleView(c.Unit)||!Native.Same(Native.Get(c.Bar,"unit"),c.Unit))return false;
            var state=Native.Static("GameManager","GameState");if(state is null)return false;
            string reason="";bool valid=false;
            MimicSmuggleActions.GuardValidity(c.Command,state,ref reason,ref valid);return valid;
        }
        public static void Sync(object bar,object? unit,bool allowed)
        {
            try
            {
                long key=Native.Pointer(bar);
                if(!allowed||unit is null){Remove(bar);return;}
                if(Controls.TryGetValue(key,out var existing))
                {
                    if(Owned(existing)&&Valid(existing))return;
                    Remove(bar);
                }
                var state=Native.Static("GameManager","GameState");var player=Native.Static("GameManager","LocalPlayer");
                var unitState=Native.Get(unit,"UnitState");var coordinates=unitState is null?null:Native.Get(unitState,"coordinates");
                if(state is null||player is null||coordinates is null){return;}
                var data=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),coordinates);
                if(data is null){return;}
                if(!MimicSmuggleActions.TryCreate(state,player,data,out var command,out _))
                {return;}
                var renderer=Native.Static("MapRenderer","instance");
                var tile=renderer is null?null:Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),renderer,coordinates);
                var resource=tile is null?null:Native.Get(tile,"Resource");var sprite=resource is null?null:Native.Get(resource,"Sprite");
                if(sprite is null){MimicSmuggleActions.ForgetOffer(command!);return;}
                // Native AddUnitActionButtons uses forceToBottom=true. The false
                // branch instead belongs to QuickActions; adding that widget to
                // bar.buttons too would leave a destroyed entry in its other owner.
                var button=Native.Invoke(Native.M("InteractionBar","CreateRoundBottomBarButton","System.String","System.Boolean"),bar,"Smuggle",true)!;
                var c=new Control{Bar=bar,Unit=unit,Button=button,Buttons=Native.Get(bar,"buttons")!,Command=command!};Controls[key]=c;
                if(c.Buttons is null||!Native.Items(c.Buttons).Any(x=>Native.Same(x,button)))
                    throw new InvalidOperationException("Native Smuggle unit button was not registered in its bottom-bar owner.");
                Native.Invoke(Native.M("UIRoundButton","SetSprite","UnityEngine.Sprite","System.Boolean"),button,sprite,true);
                Native.Set(button,"Cost",2f);Native.Set(button,"buttonActive",true);Native.Set(button,"ButtonEnabled",true);
                c.Active=true;
                c.Callback=Native.ButtonCallback(()=>
                {
                    try
                    {
                        if(!Controls.TryGetValue(key,out var current)||!ReferenceEquals(current,c)||!c.Active||!Owned(c)||!Valid(c))return;
                        c.HandedToNative=true;
                        Native.Invoke(Native.M("InteractionBar","ClickedImprovement","BuildCommand"),bar,c.Command);
                    }
                    catch(Exception e){Note(e);}
                });
                Native.Set(button,"OnClicked",c.Callback);Native.Set(button,"OnClickedWhenDisabled",null);
                Native.Invoke(Native.M("UIButtonBase","SetActive","System.Boolean"),button,true);
                Native.Invoke(Native.M("InteractionBar","SetupButtonNavigation"),bar);
                MimicPresentationUi.ReflowBar(bar);
                Entry.Note("smuggle-button-ready","Native resource sprite, round action button and 2-star cost.");
            }
            catch(Exception e){Note(e);Remove(bar);}
        }
        static void Disable(Control c)
        {
            c.Active=false;
            Native.Set(c.Button,"OnClicked",null);Native.Set(c.Button,"OnClickedWhenDisabled",null);c.Callback=null;
            Native.Set(c.Button,"ButtonEnabled",false);Native.Set(c.Button,"buttonActive",false);
            Native.Invoke(Native.M("UIButtonBase","SetActive","System.Boolean"),c.Button,false);
        }
        static void Forget(Control c)
        {if(!c.HandedToNative)MimicSmuggleActions.ForgetOffer(c.Command);}
        public static void Remove(object bar,bool reflow=true)
        {
            long key=Native.Pointer(bar);if(!Controls.Remove(key,out var c))return;
            try
            {
                if(!Owned(c))return;
                Disable(c);
                if(!Owned(c))return;
                Native.Invoke(Native.M(c.Buttons.GetType(),"Remove","UIRoundButton"),c.Buttons,c.Button);
                Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(c.Button,"gameObject"));
                Native.Invoke(Native.M("InteractionBar","SetupButtonNavigation"),bar);
                if(reflow)MimicPresentationUi.ReflowBar(bar);
            }
            catch(Exception e){Note(e);}
            finally{Forget(c);}
        }
        public static void NativeClear(object __instance)
        {
            if(!Controls.Remove(Native.Pointer(__instance),out var c))return;
            try{if(Owned(c))Disable(c);}catch(Exception e){Note(e);}finally{Forget(c);}
        }
        public static void Clear()
        {foreach(var c in Controls.Values.ToArray())Remove(c.Bar,false);Controls.Clear();Errors.Clear();}
        static void Note(Exception e)
        {if(Errors.Count<24&&Errors.Add(e.ToString()))Entry.Note("smuggle-ui-error",e.ToString());}
    }
}
