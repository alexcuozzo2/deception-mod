using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native UI calls share BattleHelpers with real actions and AI. Projection
    // exists only inside the two audited synchronous display entrypoints, on
    // detached native objects; authoritative combat bodies are never edited.
    internal static class NativeCombatPreview
    {
        const int MaxTargets=4096;
        [ThreadStatic] static Scope? Current;
        [ThreadStatic] static Calculation? Computing;
        static long Generation;
        static bool FailureLogged;
        static void Fail(Scope? scope,Exception error){if(scope is not null)scope.Failed=true;if(!FailureLogged){FailureLogged=true;Entry.Note("combat-preview-unavailable",error.Message);}}
        internal sealed class Scope
        {
            internal readonly Scope? Previous;
            internal readonly object State,Client,Map,World,Attacker,Body;
            internal readonly object? Snapshot;
            internal readonly object? Interaction,Hover;
            internal readonly byte Viewer;
            internal readonly uint Turn;
            internal readonly long Epoch;
            internal readonly bool Overlays,Bound;
            internal readonly Dictionary<uint,ProfileView> Profiles=new();
            internal readonly Dictionary<uint,Prediction> Targets=new();
            internal Prediction? Last;
            internal bool Closed,Failed;
            internal Scope(Scope? previous){Previous=previous;State=Client=Map=World=Attacker=Body=null!;Epoch=Generation;Current=this;}
            internal Scope(Scope? previous,object state,object client,object map,object attacker,object body,byte viewer,bool overlays,object? interaction,object? hover)
            {Previous=previous;Bound=true;State=state;Client=client;Map=map;World=Native.Get(state,"Map")!;Snapshot=NativeGameStateEnvelope.Get(state);Attacker=attacker;Body=body;Viewer=viewer;Overlays=overlays;Interaction=interaction;Hover=hover;Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));Epoch=Generation;Current=this;}
            internal bool Valid=>Bound&&!Closed&&!Failed&&Epoch==Generation&&NativeRegroupSession.Ready(State)&&
                Snapshot is not null&&ReferenceEquals(Snapshot,NativeGameStateEnvelope.Get(State))&&Native.Same(State,Native.Static("GameManager","GameState"))&&Native.Same(World,Native.Get(State,"Map"))&&
                Native.Same(Client,Native.Static("GameManager","Client"))&&Native.Same(Map,Native.Static("MapRenderer","Current"))&&
                Native.Static("GameManager","LocalPlayer") is object player&&Native.Int(Native.Get(player,"Id"))==Viewer&&
                Native.Same(Native.Get(Client,"GameState"),State)&&
                (bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,Viewer)!&&
                Convert.ToUInt32(Native.Get(State,"CurrentTurn"))==Turn&&
                Supported(Client)&&(!Overlays||(Native.Int(Native.Get(State,"CurrentPlayer"))==Viewer&&Native.Int(Native.Get(Body,"owner"))==Viewer))&&Exact(State,Map,Attacker,Body)&&Visible(Attacker)&&
                (Interaction is null||Native.Same(Native.Get(Interaction,"selectedUnit"),Attacker));
        }
        internal sealed class ProfileView
        {
            internal readonly object Unit,Body,Display;
            internal readonly LedgerAppearance? Appearance;
            internal readonly List<(object Body,int Maximum)> Profiles;
            internal readonly bool Apparent;
            internal int Health=>Appearance?.Health??Native.Int(Native.Get(Body,"health"));
            internal ProfileView(object unit,object body,object display,LedgerAppearance? appearance,List<(object,int)> profiles)
            {Unit=unit;Body=body;Display=display;Appearance=appearance;Apparent=appearance is not null;Profiles=profiles;}
            internal bool Valid(Scope s)=>s.Valid&&Exact(s.State,s.Map,Unit,Body)&&Visible(Unit)&&UsesAppearance(Unit,Body,s.State)==Apparent;
        }
        internal sealed class Prediction
        {
            internal readonly ProfileView Attacker,Defender;
            internal int Damage,Retaliation;
            internal bool Calculated;
            internal Prediction(ProfileView attacker,ProfileView defender){Attacker=attacker;Defender=defender;}
            internal bool Lethal=>Calculated&&Damage>=Defender.Health;
            internal bool Valid(Scope s)=>Attacker.Valid(s)&&Defender.Valid(s);
        }
        internal sealed class Calculation
        {
            internal readonly Scope Scope;internal readonly Prediction Prediction;internal readonly Calculation? Previous;
            internal bool Closed;
            internal Calculation(Scope scope,Prediction prediction){Scope=scope;Prediction=prediction;Previous=Computing;Computing=this;}
        }
        internal static void Clear(){Generation++;Current=null;Computing=null;FailureLogged=false;}
        internal static void Install(Harmony h)
        {
            var t=typeof(NativeCombatPreview);
            Native.Hook(h,Native.M("ClientInteraction","StartHoverUnit","Unit"),t,nameof(BeforeHover),null,nameof(Finished));
            Native.Hook(h,Native.M("Unit","UpdateOverlays"),t,nameof(BeforeOverlays),nameof(AfterOverlays),nameof(Finished));
            Native.Hook(h,Native.M("BattleHelpers","GetBattleResults","GameState","UnitState","UnitState","System.Boolean","System.Boolean"),t,nameof(BeforeBattle),nameof(AfterBattle),nameof(BattleFinished));
            Native.Hook(h,Native.M("UnitDataExtensions","GetMaxHealth","UnitState","GameState"),t,nameof(Maximum));
            Native.Hook(h,Native.M("Unit","get_Health"),t,null,nameof(Health));
            Native.Hook(h,Native.M("Unit","ShowDeathIndicator"),t,nameof(Death));
            Native.Hook(h,Native.M("DamageIndicator","Show","Unit","System.Single","System.Single","System.Single"),t,nameof(Indicator));
            Native.Hook(h,Native.M("Tile","ShowOverlay","OverlayType"),t,nameof(TileOverlay));
        }
        static bool Supported(object client)
        {
            var manager=Native.Get(client,"ActionManager");
            return manager is not null&&!(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!;
        }
        static bool Visible(object unit)=>Native.UnityAlive(unit)&&(bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!&&
            !(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!;
        static bool Exact(object state,object map,object unit,object body)
        {
            if(!Native.UnityAlive(unit)||!Native.UnityAlive(map)||!Native.Same(Native.Get(unit,"UnitState"),body)||Native.Int(Native.Get(body,"health"))<=0)return false;
            uint id=Convert.ToUInt32(Native.Get(body,"id"));object?[] found={id,null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,found)!&&Native.Same(found[1],body)&&
                Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,id),unit);
        }
        static void Begin(object? attacker,bool overlays,object? interaction,object? hover,out Scope? scope)
        {
            var previous=Current;scope=new Scope(previous);var state=Native.Static("GameManager","GameState");var client=Native.Static("GameManager","Client");
            var player=Native.Static("GameManager","LocalPlayer");var map=Native.Static("MapRenderer","Current");
            if(attacker is null||state is null||client is null||player is null||map is null||!Supported(client))return;
            byte viewer=Convert.ToByte(Native.Get(player,"Id"));var body=Native.Get(attacker,"UnitState");
            if(body is null||(overlays&&(Native.Int(Native.Get(body,"owner"))!=viewer||Native.Int(Native.Get(state,"CurrentPlayer"))!=viewer))||
                !Native.Same(Native.Get(client,"GameState"),state)||!Exact(state,map,attacker,body)||!Visible(attacker)||
                !(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!)return;
            scope=new(previous,state,client,map,attacker,body,viewer,overlays,interaction,hover);
        }
        public static bool BeforeHover(object __instance,object __0,out Scope? __state)
        {__state=null;try{Begin(Native.Get(__instance,"selectedUnit"),false,__instance,__0,out __state);return true;}catch(Exception e){Fail(__state,e);return false;}}
        public static bool BeforeOverlays(object __instance,out Scope? __state)
        {__state=null;try{Begin(__instance,true,null,null,out __state);return true;}catch(Exception e){Fail(__state,e);return false;}}
        // MapView returns null for an enemy Mimic rendered by its native
        // apparent carrier. Only an authorized explicit actual view uses truth.
        static bool UsesAppearance(object unit,object body,object state)=>
            MimicPresentationState.TryProject(body,out var p)&&Native.Same(p.State,state)&&
            p.Identity is {Alive:true,Independent:true,Appearance:not null}&&MimicPresentationUi.MapView(unit)!=true;
        static ProfileView View(Scope s,object unit,object body)
        {
            uint id=Convert.ToUInt32(Native.Get(body,"id"));
            if(s.Profiles.TryGetValue(id,out var old))
            {
                if(!Native.Same(old.Body,body)||!old.Valid(s))throw new InvalidOperationException("Displayed combat profile changed.");
                return old;
            }
            if(s.Profiles.Count>=MaxTargets)throw new InvalidOperationException("Combat preview exceeds map bound.");
            LedgerAppearance? appearance=null;
            var identity=NativeRegroupSession.ReadIdentity(s.State,id);
            bool projected=MimicPresentationState.TryProject(body,out _);
            if((identity is {Independent:true}&&!projected)||(identity is null&&RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(body,"type")!))))
                throw new InvalidOperationException("Independent combat appearance is unavailable.");
            if(UsesAppearance(unit,body,s.State))
            {
                if(!MimicPresentationState.TryProject(body,out var p))throw new InvalidOperationException("Displayed combat identity vanished.");
                appearance=p.Identity!.Appearance!;
            }
            var profiles=new List<(object,int)>();var display=appearance is null?body:Profile(s.State,body,appearance,profiles,0);
            var result=new ProfileView(unit,body,display,appearance,profiles);s.Profiles.Add(id,result);return result;
        }
        static Prediction? Target(Scope s,object body)
        {
            uint id=Convert.ToUInt32(Native.Get(body,"id"));
            if(s.Targets.TryGetValue(id,out var old))
            {if(!Native.Same(old.Defender.Body,body)||!old.Valid(s))throw new InvalidOperationException("Combat target changed.");return old;}
            var unit=Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),s.Map,id);
            if(unit is null||(!s.Overlays&&!Native.Same(unit,s.Hover))||!Exact(s.State,s.Map,unit,body)||!Visible(unit))return null;
            var attacker=View(s,s.Attacker,s.Body);var defender=View(s,unit,body);
            if(!attacker.Apparent&&!defender.Apparent&&!Candidate(s,s.Body)&&!Candidate(s,body))return null;
            var prediction=new Prediction(attacker,defender);s.Targets.Add(id,prediction);return prediction;
        }
        static object Profile(object state,object source,LedgerAppearance a,List<(object,int)> profiles,int depth)
        {
            if(depth>1||a.Health==0||a.Maximum<a.Health)throw new InvalidOperationException("Invalid apparent battle profile.");
            var data=Native.DetachedClone(RecruitmentHooks.Data(state,a.Form),"Polytopia.Data.UnitData");
            var body=Native.DetachedClone(source,"UnitState");
            Native.Set(data,"health",a.Maximum);Native.Set(body,"UnitData",data);Native.Set(body,"type",Native.Get(data,"type"));
            Native.Set(body,"health",a.Health);Native.Set(body,"promotionLevel",(ushort)(a.Veteran?1:0));
            Native.Set(body,"effects",Native.NewList("UnitEffect",a.Effects.Select(x=>Enum.Parse(Native.T("UnitEffect"),x,false))));
            Native.Set(body,"passengerUnit",null);profiles.Add((body,a.Maximum));
            if(a.Passenger is LedgerAppearance cargo)
                Native.Set(body,"passengerUnit",Profile(state,Native.Get(source,"passengerUnit")??source,cargo,profiles,depth+1));
            return body;
        }
        public static bool BeforeBattle(object __0,ref UnitState __1,ref UnitState __2,bool __4,ref BattleResults __result,out Calculation? __state)
        {
            __state=null;var s=Current;
            // simpleData is the native display mode. Real command/reaction/AI
            // callers without a matching UI frame retain their real arguments.
            if(!__4||Computing is not null||s is null||!s.Bound||!Native.Same(s.State,__0)||!Native.Same(s.Body,__1))return true;
            try
            {
                if(!s.Valid){__result=default;return false;}
                s.Last=null;var target=Target(s,__2);if(target is null)return true;
                __state=new(s,target);__1=(UnitState)target.Attacker.Display;__2=(UnitState)target.Defender.Display;return true;
            }
            catch(Exception e){Fail(s,e);__result=default;return false;}
        }
        public static void AfterBattle(BattleResults __result,Calculation? __state)
        {
            if(__state is null)return;
            try
            {
                var p=__state.Prediction;var s=__state.Scope;if(!p.Valid(s)){s.Failed=true;return;}
                p.Damage=__result.attackDamage;p.Retaliation=__result.retaliationDamage;p.Calculated=true;s.Last=p;
                // When apparent HP is lower than truth, native inline raw HP
                // omits this marker entirely. Publish its correct first state
                // before native overlay/indicator writes, not a later refresh.
                if(s.Overlays&&p.Lethal&&p.Damage<Native.Int(Native.Get(p.Defender.Body,"health")))
                {
                    var list=Native.Get(s.Attacker,"overlayUnits")!;
                    if(Native.Int(Native.Get(list,"Count"))>=MaxTargets)throw new InvalidOperationException("Native overlay bound exceeded.");
                    Native.Invoke(Native.M("Unit","ShowDeathIndicator"),p.Defender.Unit);
                    Native.Invoke(Native.M(list.GetType(),"Add","Unit"),list,p.Defender.Unit);
                }
            }
            catch(Exception e){Fail(__state.Scope,e);}
        }
        public static Exception? BattleFinished(Exception? __exception,Calculation? __state)
        {
            if(__state is not null&&!__state.Closed){__state.Closed=true;if(ReferenceEquals(Computing,__state))Computing=__state.Scope.Epoch==Generation?__state.Previous:null;}
            return __exception;
        }
        // The retained-sight adapter needs the real Rogue identity solely for
        // its visibility scope. Calculation still receives the detached body.
        internal static object ActualBody(object state,object body)
        {
            var c=Computing;
            if(c is null||!c.Prediction.Valid(c.Scope)||!Native.Same(c.Scope.State,state))return body;
            if(Native.Same(c.Prediction.Defender.Display,body))return c.Prediction.Defender.Body;
            return Native.Same(c.Prediction.Attacker.Display,body)?c.Prediction.Attacker.Body:body;
        }
        public static bool Maximum(object __0,object __1,ref int __result)
        {
            var c=Computing;if(c is null||!c.Scope.Valid||!Native.Same(c.Scope.State,__1))return true;
            foreach(var p in c.Prediction.Attacker.Profiles.Concat(c.Prediction.Defender.Profiles))if(Native.Same(p.Body,__0)){__result=p.Maximum;return false;}return true;
        }
        public static void Health(object __instance,ref ushort __result)
        {
            var s=Current;if(s is null||!s.Valid)return;
            var body=Native.Get(__instance,"UnitState");if(body is null)return;
            if(s.Profiles.TryGetValue(Convert.ToUInt32(Native.Get(body,"id")),out var p)&&Native.Same(p.Unit,__instance)&&p.Apparent&&p.Valid(s))__result=checked((ushort)p.Health);
        }
        static bool Candidate(Scope s,object body)=>
            NativeRegroupSession.ReadIdentity(s.State,Convert.ToUInt32(Native.Get(body,"id"))) is {Alive:true,Independent:true,Appearance:not null}||
            RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(body,"type")!));
        public static bool Death(object __instance)
        {
            var s=Current;if(s is null||!s.Overlays||!s.Bound)return true;if(!s.Valid)return false;
            var body=Native.Get(__instance,"UnitState");if(body is null)return true;
            if(!s.Targets.TryGetValue(Convert.ToUInt32(Native.Get(body,"id")),out var p))return !Candidate(s,body)&&!Candidate(s,s.Body);
            return Native.Same(p.Defender.Unit,__instance)&&p.Valid(s)&&p.Lethal;
        }
        public static bool Indicator(object __0,ref float __1,ref float __2)
        {
            var s=Current;if(s is not {Bound:true})return true;if(!s.Valid)return false;
            var p=s.Last;if(p is null)return !Candidate(s,Native.Get(__0,"UnitState")!)&&!Candidate(s,s.Body);if(!p.Valid(s))return false;
            if(Native.Same(p.Defender.Unit,__0)){__1=p.Damage;__2=p.Defender.Health;}
            else if(Native.Same(p.Attacker.Unit,__0)){__1=p.Retaliation;__2=p.Attacker.Health;}
            return true;
        }
        // UpdateOverlays compares raw live attacker HP inline. Correct the
        // native Attack/Kill choice at its first visual sink, before rendering.
        public static bool TileOverlay(object __instance,ref OverlayType __0)
        {
            var s=Current;if(s is not {Bound:true,Overlays:true}||(__0!=OverlayType.Attack&&__0!=OverlayType.Kill))return true;
            if(!s.Valid)return false;var p=s.Last;
            if(p is null)
            {var target=Native.Get(__instance,"Unit");return !Candidate(s,s.Body)&&(target is null||!Candidate(s,Native.Get(target,"UnitState")!));}
            if(!Native.Same(Native.Get(__instance,"Unit"),p.Defender.Unit))return true;
            if(!p.Valid(s))return false;
            __0=p.Retaliation>=p.Attacker.Health?OverlayType.Kill:OverlayType.Attack;return true;
        }
        public static void AfterOverlays(bool __runOriginal,Scope? __state)
        {
            var s=__state;if(!__runOriginal||s is null)return;
            try{RepairOverlays(s);}catch(Exception e){Fail(s,e);}
            if(s.Failed)try{Native.Invoke(Native.M("Unit","ClearUnitOverlays"),s.Attacker);}catch(Exception e){Fail(s,e);}
        }
        static void RepairOverlays(Scope s)
        {
            if(!s.Valid||s.Targets.Count==0)return;
            var list=Native.Get(s.Attacker,"overlayUnits")!;
            if(Native.Int(Native.Get(list,"Count"))>MaxTargets)throw new InvalidOperationException("Native overlay bound exceeded.");
            // Native unconditionally adds even when our ShowDeathIndicator
            // prefix refused its raw-HP marker. Remove only bookkeeping here;
            // every visible state was already decided before the first write.
            foreach(var p in s.Targets.Values.Where(p=>p.Calculated&&p.Valid(s)&&!p.Lethal))
                foreach(var unit in Native.Items(list).Where(x=>Native.Same(x,p.Defender.Unit)).ToArray())
                    Native.Invoke(Native.M(list.GetType(),"Remove","Unit"),list,unit);
        }
        public static Exception? Finished(Exception? __exception,Scope? __state)
        {
            if(__state is not null&&!__state.Closed){__state.Closed=true;if(ReferenceEquals(Current,__state))Current=__state.Epoch==Generation?__state.Previous:null;}
            return __exception;
        }
    }
}
