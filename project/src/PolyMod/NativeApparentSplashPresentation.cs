using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Stomp/Tentacles in these two callbacks control only source animation.
    // MoveAction and AttackAction remain entirely native: no borrowed damage,
    // victim hit animation, action-budget change, or exchange arrival effect.
    internal static class NativeApparentSplashPresentation
    {
        const string BeginType="MoveReaction+__c__DisplayClass2_0",EndType="MoveReaction+__c__DisplayClass2_1";
        const int Stomp=40,Tentacles=44,Burn=14,MaxCues=128;
        internal sealed class Cue
        {
            internal object Closure=null!,Reaction=null!,Action=null!,State=null!,Map=null!,Client=null!,Player=null!,Unit=null!,Rendered=null!,From=null!,To=null!;
            internal LedgerUnit Row=null!;internal byte Viewer;internal Guid Epoch;internal long Generation;internal bool Actual,Stomps,Tentacles,Electric,Finished,Boarding,ProfileReady;
        }
        internal sealed record Scope(Cue Cue,Scope? Previous,long Generation,bool Allowed,bool Ending);
        [ThreadStatic] static Scope? Current;
        [ThreadStatic] static bool NativeSource;
        static readonly List<Cue> Cues=new();
        // Value-only retirement markers reject delayed callbacks after teardown
        // without retaining any old GameState, Unity object or ledger graph.
        static readonly List<(long Closure,long Action,long Tile)> Retired=new();
        static long Generation;static int Notes;
        internal static void Clear()
        {
            foreach(var cue in Cues){if(Retired.Count>=MaxCues)Retired.RemoveAt(0);Retired.Add((Native.Pointer(cue.Closure),Native.Pointer(cue.Action),Native.Pointer(cue.To)));}
            Cues.Clear();Current=null;NativeSource=false;Generation++;Notes=0;
        }
        internal static void Install(Harmony h)
        {
            var type=typeof(NativeApparentSplashPresentation);
            Native.Hook(h,Native.M(BeginType,"_Execute_b__0"),type,nameof(BeforeStart),null,nameof(Finished));
            Native.Hook(h,Native.M(EndType,"_Execute_b__1"),type,nameof(BeforeEnd),null,nameof(Finished));
            Native.Hook(h,Native.M("EmbarkReaction","Execute","Il2CppSystem.Action"),type,nameof(BeforeEmbark),null,nameof(Finished));
            Native.Hook(h,Native.M("UnitDataExtensions","HasAbility","UnitState","Polytopia.Data.UnitAbility+Type"),type,null,nameof(Ability));
            Native.Hook(h,Native.M("ReactionUtils","RegularStompDamageAnimation","Tile"),type,nameof(Source));
            Native.Hook(h,Native.M("ReactionUtils","ElectricStompDamageAnimation","Tile"),type,nameof(Source));
            Native.Hook(h,Native.M("Tile","SpawnAreaDamage"),type,nameof(Area));
            Native.Hook(h,Native.M("MapRenderer","OnDestroy"),type,nameof(MapDestroying));
            // Generated fields are pinned wrapper properties, never raw offsets.
            foreach(string field in new[]{"unitState","__4__this","fromTile","toTile","targetTile","shouldAnimate"})_=Native.M(BeginType,"get_"+field);
            _=Native.M(EndType,"get_field_Public___c__DisplayClass2_0_0");
            _=Native.M("Polytopia.Data.UnitData","get_weapon");
            if(Native.Int(Enum.Parse(Native.T("Polytopia.Data.UnitAbility+Type"),"Stomp"))!=Stomp||
                Native.Int(Enum.Parse(Native.T("Polytopia.Data.UnitAbility+Type"),"Tentacles"))!=Tentacles||
                Native.Int(Enum.Parse(Native.T("Polytopia.Data.UnitData+WeaponEnum"),"Burn"))!=Burn)
                throw new InvalidOperationException("Native source splash constants changed.");
        }
        static bool SameAt(object a,object b)=>Native.Int(Native.Get(a,"X"))==Native.Int(Native.Get(b,"X"))&&Native.Int(Native.Get(a,"Y"))==Native.Int(Native.Get(b,"Y"));
        static bool Active(object? value)=>Native.UnityAlive(value)&&(bool)Native.Get(Native.Get(value!,"gameObject")!,"activeInHierarchy")!;
        static bool Has(object data,int ability)=>Native.Items(Native.Get(data,"unitAbilities")!).Any(v=>Native.Int(v)==ability);
        static object? TileData(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static bool SameAppearance(LedgerAppearance a,LedgerAppearance b)=>
            (a with{Effects=Array.Empty<string>(),Passenger=null})==(b with{Effects=Array.Empty<string>(),Passenger=null})&&a.Effects.SequenceEqual(b.Effects)&&
            (a.Passenger is null?b.Passenger is null:b.Passenger is not null&&SameAppearance(a.Passenger,b.Passenger));
        static bool Context(object state,object map,object client,object player,byte viewer)=>
            NativeRegroupSession.Ready(state)&&Native.Same(state,Native.Static("GameManager","GameState"))&&
            Native.Same(client,Native.Static("GameManager","Client"))&&Native.Same(player,Native.Static("GameManager","LocalPlayer"))&&
            Native.Same(Native.Get(client,"GameState"),state)&&
            // An enrolled Local/Hotseat recap is the player's authorized view of
            // earlier actions. Ready(state) still binds this exact current
            // state to that client/session; detached replay/spectators stay out.
            !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!&&
            !(bool)Native.Static("HotSeatOverlay","IsActive")!&&
            (bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!&&Active(map)&&Native.Same(map,Native.Static("MapRenderer","Current"));
        static bool Root(Cue c)
        {
            object?[] lookup={c.Row.NativeId,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),c.State,lookup)!||!Native.Same(lookup[1],c.Unit)||
                Native.Int(Native.Get(c.Unit,"health"))<=0||Convert.ToByte(Native.Get(c.Unit,"owner"))!=Convert.ToByte(Native.Get(c.Action,"PlayerId")))return false;
            var toData=Native.Get(c.To,"Data");var at=Native.Get(c.Unit,"coordinates")!;
            if(toData is null||!Native.Same(toData,TileData(c.State,at))||!Native.Same(Native.Get(toData,"unit"),c.Unit)||
                !SameAt(at,Native.Get(c.To,"Coordinates")!))return false;
            if(c.Boarding&&!c.ProfileReady)
            {
                var rendered=Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),c.Map,c.Row.NativeId);
                if(!Active(rendered)||!Native.Same(Native.Get(rendered!,"UnitState"),c.Unit)||!Native.Same(Native.Get(c.To,"unit"),rendered))return false;
                c.Rendered=rendered!;Configure(c);
            }
            // The backend is already at its destination. The visual may still
            // be attached to fromTile while the native movement tween runs.
            return Active(c.Rendered)&&Native.Same(Native.Get(c.Rendered,"UnitState"),c.Unit)&&
                Native.Same(c.Rendered,Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),c.Map,c.Row.NativeId))&&
                (Native.Same(Native.Get(c.From,"unit"),c.Rendered)||Native.Same(Native.Get(c.To,"unit"),c.Rendered));
        }
        static bool CurrentCue(Cue c)
        {
            if(c.Generation!=Generation||NativeGameStateEnvelope.Get(c.State)?.Epoch!=c.Epoch||!Context(c.State,c.Map,c.Client,c.Player,c.Viewer)||!Active(c.From)||!Active(c.To)||!Root(c))return false;
            if(!Native.Same(Native.Get(c.Reaction,"action"),c.Action))return false;
            if(!c.Boarding&&(!Native.Same(Native.Get(c.Closure,"__4__this"),c.Reaction)||
                !Native.Same(Native.Get(c.Closure,"unitState"),c.Unit)||!Native.Same(Native.Get(c.Closure,"fromTile"),c.From)||!Native.Same(Native.Get(c.Closure,"toTile"),c.To)))return false;
            var row=NativeRegroupSession.ReadIdentity(c.State,c.Row.NativeId);
            if(row is not {Alive:true,Independent:true,Appearance:not null}||row.LogicalId!=c.Row.LogicalId||row.Kind!=c.Row.Kind||!SameAppearance(row.Appearance,c.Row.Appearance!))return false;
            var data=Native.Get(c.To,"Data")!;
            if(!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,c.Viewer)!||(bool)Native.Get(c.To,"IsHidden")!||
                !Native.Same(c.Unit,Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),data,c.State,c.Viewer,false))||
                (bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),c.Rendered)!)return false;
            return (MimicPresentationUi.MapView(c.Rendered)==true)==c.Actual;
        }
        static Cue? Capture(object closure)
        {
            var unit=Native.Get(closure,"unitState");var reaction=Native.Get(closure,"__4__this");
            var state=Native.Static("GameManager","GameState");var map=Native.Static("MapRenderer","Current");
            var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");
            if(unit is null||reaction is null||state is null||map is null||client is null||player is null)return null;
            byte viewer=Convert.ToByte(Native.Get(player,"Id"));if(!Context(state,map,client,player,viewer))return null;
            var action=Native.Get(reaction,"action");if(action is null)return null;
            var row=NativeRegroupSession.ReadIdentity(state,Convert.ToUInt32(Native.Get(unit,"id")));
            if(row is not {Alive:true,Independent:true,Appearance:not null}||Convert.ToUInt32(Native.Get(action,"UnitId"))!=row.NativeId)return null;
            var from=Native.Get(closure,"fromTile");var to=Native.Get(closure,"toTile");
            var rendered=Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,row.NativeId);
            if(!Active(from)||!Active(to)||!Active(rendered))return null;
            var path=Native.Get(action,"Path");int count=path is null?0:Native.Int(Native.Get(path,"Count"));
            if(count<2||count>16384)return null;var route=Native.Items(path!).ToArray();
            // Native MoveReaction reads Path[Count-1] for fromTile and Path[0]
            // for toTile (the pathfinder builds its route backwards).
            if(!SameAt(route[^1],Native.Get(from!,"Coordinates")!)||!SameAt(route[0],Native.Get(to!,"Coordinates")!))return null;
            var cue=new Cue{Closure=closure,Reaction=reaction,Action=action,State=state,Map=map,Client=client,Player=player,Unit=unit,Rendered=rendered!,From=from!,To=to!,
                Row=row,Viewer=viewer,Generation=Generation,Epoch=NativeGameStateEnvelope.Get(state)!.Epoch};
            Configure(cue);return cue;
        }
        static void Configure(Cue c)
        {
            c.Actual=MimicPresentationUi.MapView(c.Rendered)==true;
            var data=c.Actual?Native.Get(c.Unit,"UnitData")!:RecruitmentHooks.Data(c.State,c.Row.Appearance!.Form);
            // Native pushes skip arrival passives. A private actual Mimic view
            // has no Stomp even if its operative movement carrier copied one.
            bool passive=(c.Boarding||Native.Int(Native.Get(c.Action,"Reason"))!=2)&&(!c.Actual||c.Row.Kind==LedgerActualKind.Genuine);
            c.Stomps=passive&&Has(data,Stomp);c.Tentacles=passive&&Has(data,Tentacles);
            c.Electric=Native.Int(Native.Get(data,"weapon"))==Burn;c.ProfileReady=true;
        }
        static Cue? CaptureEmbark(object reaction)
        {
            var state=Native.Static("GameManager","GameState");var map=Native.Static("MapRenderer","Current");
            var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");
            if(state is null||map is null||client is null||player is null)return null;
            byte viewer=Convert.ToByte(Native.Get(player,"Id"));if(!Context(state,map,client,player,viewer))return null;
            var action=Native.Get(reaction,"action");if(action is null)return null;
            var at=Native.Get(action,"Coordinates")!;var data=TileData(state,at);var unit=data is null?null:Native.Get(data,"unit");
            if(unit is null)return null;
            var row=NativeRegroupSession.ReadIdentity(state,Convert.ToUInt32(Native.Get(unit,"id")));
            if(row is not {Alive:true,Independent:true,Appearance:not null})return null;
            var tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,at);if(!Active(tile)||!Native.Same(Native.Get(tile!,"Data"),data))return null;
            // Native EmbarkReaction first rebuilds the Tile's rendered Unit.
            // Bind the new wrapper only afterwards, when it asks HasAbility.
            return new Cue{Closure=reaction,Reaction=reaction,Action=action,State=state,Map=map,Client=client,Player=player,Unit=unit,From=tile!,To=tile!,
                Row=row,Viewer=viewer,Generation=Generation,Epoch=NativeGameStateEnvelope.Get(state)!.Epoch,Boarding=true};
        }
        public static void BeforeEmbark(object __instance,out Scope? __state)
        {
            __state=null;
            try
            {
                var cue=Cues.FirstOrDefault(c=>c.Boarding&&Native.Same(c.Reaction,__instance));bool duplicate=cue is not null;
                cue??=CaptureEmbark(__instance);if(cue is null)return;
                if(!duplicate){if(Cues.Count>=MaxCues)Cues.RemoveAt(0);Cues.Add(cue);}
                __state=new(cue,Current,Generation,!duplicate,false);Current=__state;cue.Finished=true;
            }
            catch(Exception e){Note(e);}
        }
        public static void BeforeStart(object __instance,out Scope? __state)
        {
            __state=null;
            try
            {
                var cue=Cues.FirstOrDefault(c=>Native.Same(c.Closure,__instance));bool duplicate=cue is not null;
                cue??=Capture(__instance);if(cue is null)return;
                if(!duplicate){if(Cues.Count>=MaxCues)Cues.RemoveAt(0);Cues.Add(cue);}
                __state=new(cue,Current,Generation,!duplicate&&CurrentCue(cue),false);Current=__state;
            }
            catch(Exception e){Note(e);}
        }
        public static void BeforeEnd(object __instance,out Scope? __state)
        {
            __state=null;
            try
            {
                var closure=Native.Get(__instance,"field_Public___c__DisplayClass2_0_0");
                var cue=Cues.FirstOrDefault(c=>Native.Same(c.Closure,closure));
                if(cue is null)
                {
                    if(closure is null)return;var reaction=Native.Get(closure,"__4__this");var action=reaction is null?null:Native.Get(reaction,"action");var tile=Native.Get(closure,"toTile");
                    if(action is null||tile is null||!Retired.Contains((Native.Pointer(closure),Native.Pointer(action),Native.Pointer(tile))))return;
                    // This exact native callback captured a projected bool before
                    // Clear. Suppress only its old source area; do not interrupt
                    // its native completion delegate or other reaction work.
                    cue=new Cue{Unit=Native.Get(closure,"unitState")!,To=tile};
                    __state=new(cue,Current,Generation,false,true);Current=__state;return;
                }
                bool available=!cue.Finished;cue.Finished=true;
                __state=new(cue,Current,Generation,available&&CurrentCue(cue),true);Current=__state;
            }
            catch(Exception e){Note(e);}
        }
        public static void Ability(object __0,object __1,ref bool __result)
        {
            int ability=Native.Int(__1);if(ability is not (Stomp or Tentacles)||Current is not Scope s||!Native.Same(__0,s.Cue.Unit))return;
            try{__result=s.Allowed&&CurrentCue(s.Cue)&&(ability==Stomp?s.Cue.Stomps:s.Cue.Tentacles);}
            catch(Exception e){__result=false;Note(e);}
        }
        public static bool Source(object __0)
        {
            if(NativeSource||Current is not Scope s||!Native.Same(__0,s.Cue.To))return true;
            try
            {
                if(s.Ending||!s.Allowed||!CurrentCue(s.Cue)||!(s.Cue.Stomps||s.Cue.Tentacles))return false;
                NativeSource=true;
                Native.Invoke(Native.M("ReactionUtils",s.Cue.Electric?"ElectricStompDamageAnimation":"RegularStompDamageAnimation","Tile"),null,__0);
            }
            catch(Exception e){Note(e);}
            finally{NativeSource=false;}
            return false;
        }
        public static bool Area(object __instance)
        {
            if(Current is not Scope s||!Native.Same(__instance,s.Cue.To))return true;
            try{return (s.Ending||s.Cue.Boarding)&&s.Allowed&&CurrentCue(s.Cue)&&(s.Cue.Stomps||s.Cue.Tentacles);}
            catch(Exception e){Note(e);return false;}
        }
        public static Exception? Finished(Exception? __exception,Scope? __state)
        {if(__state is not null)Current=__state.Generation==Generation?__state.Previous:null;return __exception;}
        public static void MapDestroying(object __instance)=>Clear();
        static void Note(Exception e){try{if(Notes++<16)Entry.Note("apparent-splash-presentation",e.ToString());}catch{/* Visual diagnostics cannot interrupt native action completion. */}}
    }
}
