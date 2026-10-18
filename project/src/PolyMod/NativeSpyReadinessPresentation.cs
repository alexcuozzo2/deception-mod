using System;
using System.Reflection;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native uses CanAttackAnything as its remaining-action display test.
    // An unmoved Spy or an acquired Rogue can have a real specialist action.
    // Correct only the native render sinks; attack/command queries stay false.
    internal static class NativeSpyReadinessPresentation
    {
        [ThreadStatic] static Scope? Current;
        [ThreadStatic] static int Depth;
        static int Generation;
        static bool Noted;
        static MethodInfo? ReadyOverlay;
        sealed record Scope(object Visual,object Unit,object Skin,object Context,object State,
            object Map,object Client,object Player,int Depth,int Generation,Scope? Previous);

        internal static void Clear(){Current=null;Depth=0;Generation++;Noted=false;}
        static void Note(Exception e){if(!Noted){Noted=true;Entry.Note("spy-readiness-presentation",e.Message);}}
        static MethodInfo Overlay()=>ReadyOverlay??=Native.M("Unit","GetOverlayColorAndStrength","System.Boolean",
            Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(Native.T("UnitEffect")).FullName!);
        internal static void Install(Harmony h)
        {
            _=Overlay();_=Native.M("MapRenderContext","get_hideOutlines");
            Native.Hook(h,Native.M("Unit","UpdateObject","MapRenderContext","SkinVisualsTransientData"),
                typeof(NativeSpyReadinessPresentation),nameof(BeforeUpdate),null,nameof(Finished));
            Native.Hook(h,Native.M("SkinVisualsRenderer","ColorizeUnit","SkinVisualsReference","UnityEngine.Color","System.Single","UnityEngine.Color","System.Single"),
                typeof(NativeSpyReadinessPresentation),nameof(BeforeColor));
            Native.Hook(h,Native.M("SkinVisualsRenderer","ShowOutline","SkinVisualsReference","System.Boolean"),
                typeof(NativeSpyReadinessPresentation),nameof(BeforeOutline));
        }
        public static void BeforeUpdate(object __instance,object __0,out long __state)
        {
            __state=((long)Generation<<32)|(uint)Depth;Depth++;
            try
            {
                // Ordinary units retain native rendering; the cached Rogue row only
                // decides whether this render needs a cosmetic scope.
                if(!RecruitmentHooks.Registered||!Native.UnityAlive(__instance))return;
                var body=Native.Get(__instance,"UnitState");
                if(body is null)return;
                var state=Native.Static("GameManager","GameState");var map=Native.Static("MapRenderer","Current");
                var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");
                var skin=Native.Get(__instance,"skinVisuals");
                if(state is null||map is null||client is null||player is null||skin is null||
                    (!SpyCatalog.IsSpyId(Native.EnumName(Native.Get(body,"type")!))&&!NativeFoggerActions.IsFogger(body)&&!NativeRogueRules.IsRogue(state,body)))return;
                var scope=new Scope(__instance,body,skin,__0,state,map,client,player,Depth,Generation,Current);
                if(Valid(scope))Current=scope;
            }
            catch(Exception e){Note(e);}
        }
        static bool Valid(Scope s)
        {
            if(s.Generation!=Generation||s.Depth!=Depth||!RecruitmentHooks.Registered||!Native.UnityAlive(s.Visual)||!Native.UnityAlive(s.Map)||!Native.UnityAlive(s.Skin)||
                !Native.Same(s.State,Native.Static("GameManager","GameState"))||!Native.Same(s.Client,Native.Static("GameManager","Client"))||
                !Native.Same(s.Player,Native.Static("GameManager","LocalPlayer"))||!Native.Same(s.Map,Native.Static("MapRenderer","Current"))||
                !Native.Same(Native.Get(s.Client,"GameState"),s.State)||!(bool)Native.Get(s.Client,"IsReady")!||
                (bool)Native.Get(s.Client,"IsReplay")!||(bool)Native.Get(s.Client,"IsSpectating")!||
                !Native.Same(Native.Get(s.Visual,"UnitState"),s.Unit)||!Native.Same(Native.Get(s.Visual,"skinVisuals"),s.Skin)||
                Native.Int(Native.Get(s.Unit,"health"))<=0||
                !Native.Same(Native.Get(s.Context,"mapData"),Native.Get(s.State,"Map"))||
                !Native.Same(Native.Get(s.Context,"gld"),Native.Get(s.State,"GameLogicData"))||
                !Native.Same(Native.Get(s.Context,"unitRegistry"),s.Map)||
                !(bool)Native.Invoke(Native.M("Unit","GetVisible"),s.Visual)!||
                (bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),s.Visual)!)return false;
            byte actor=Convert.ToByte(Native.Get(s.Player,"Id"));
            if(Native.Int(Native.Get(s.Context,"localPlayerId"))!=actor||Native.Int(Native.Get(s.Unit,"owner"))!=actor||
                !(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,actor)!||
                !Equals(Native.Get(s.State,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started"))||
                !NativeRegroupSession.Ready(s.State))return false;
            uint id=Convert.ToUInt32(Native.Get(s.Unit,"id"));
            if(!Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),s.Map,id),s.Visual))return false;
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,
                Native.Get(s.State,"Map"),Native.Get(s.Unit,"coordinates"));
            if(tile is null||(bool)Native.Get(s.Unit,"attacked")!)return false;
            bool spy=SpyCatalog.IsSpyId(Native.EnumName(Native.Get(s.Unit,"type")!));
            if(spy){if((bool)Native.Get(s.Unit,"moved")!||!NativeSpyNetwork.Node(s.State,tile,s.Unit,actor))return false;}
            else if(NativeFoggerActions.IsFogger(s.Unit))
            {if(!NativeFoggerActions.Ready(s.State,s.Unit,actor)||!NativeSpyNetwork.FoggerNode(s.State,tile,s.Unit,actor))return false;}
            else if(!NativeRogueRules.IsRogue(s.State,s.Unit)||!NativeRogueRules.CanCommand(s.State,s.Unit,actor)||
                !Native.Same(Native.Get(tile,"unit"),s.Unit)||!Native.Same(NativeSpyNetwork.Unit(s.State,id),s.Unit)||
                !NativeSpyNetwork.Accessible(s.State,tile,actor)||
                (bool)Native.Invoke(Native.M("UnitState","HasLeader"),s.Unit)!)return false;
            // Exact ActionReady predicate, reading true effects directly: other
            // display scopes may substitute apparent HasEffect results here.
            foreach(var effect in Native.Items(Native.Get(s.Unit,"effects")!))if(Native.Int(effect)==0)return false;
            return true;
        }
        static Scope? ForSkin(object skin)=>Current is Scope s&&Native.Same(s.Skin,skin)&&Valid(s)?s:null;
        // Pinned Windows Unit.GetOverlayColorAndStrength(true, effects), without
        // passing its Color/float ValueTuple through generated IL2CPP interop.
        // Later native tests override earlier ones in precisely this order.
        internal static float WindowsReadyStrength(object effects)
        {
            int mask=0;
            foreach(var effect in Native.Items(effects))
            {int value=Native.Int(effect);if(value>=0&&value<=6)mask|=1<<value;}
            float strength=0;
            if((mask&(1<<2))!=0)strength=0.5f; // Boosted
            if((mask&(1<<6))!=0)strength=0.4f; // Swift
            if((mask&(1<<1))!=0)strength=0.3f; // Poisoned
            if((mask&(1<<0))!=0)strength=0.5f; // Frozen
            if((mask&(1<<5))!=0)strength=0.6f; // Petrified
            return strength;
        }
        public static void BeforeColor(object __0,ref float __2,ref float __4)
        {
            try
            {
                var scope=ForSkin(__0);if(scope is null)return;
#if DECEPTION_WINDOWS_PORT_CANDIDATE
                if(NativeWindowsHooks.Enabled)
                {
                    __2=WindowsReadyStrength(Native.Get(scope.Unit,"effects")!);__4=0;return;
                }
#endif
                // Native's readiness boolean changes only overlay strength.
                // Retain its status color, and remove the separate spent tint.
                // This wrapped tuple is invoked through its generated wrapper,
                // never hooked by value (which would have an unsafe native ABI).
                object ready=Native.Invoke(Overlay(),null,true,Native.Get(scope.Unit,"effects"))!;
                float strength=Convert.ToSingle(Native.Get(ready,"Item2"));
                if(!float.IsFinite(strength))throw new InvalidOperationException("Native ready overlay is not finite.");
                __2=strength;__4=0;
            }
            catch(Exception e){Note(e);}
        }
        public static void BeforeOutline(object __0,ref bool __1)
        {
            try{var scope=ForSkin(__0);if(scope is not null&&!(bool)Native.Get(scope.Context,"hideOutlines")!)__1=true;}
            catch(Exception e){Note(e);}
        }
        public static Exception? Finished(Exception? __exception,long __state)
        {
            if((int)(__state>>32)==Generation)
            {
                int prior=unchecked((int)(uint)__state);
                if(Current is Scope s&&s.Depth==prior+1)Current=s.Previous;
                Depth=prior;
            }
            return __exception;
        }
    }
}
