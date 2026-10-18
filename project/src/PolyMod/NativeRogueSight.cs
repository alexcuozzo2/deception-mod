using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // C13 stores only a Rogue source's actual, completed native footprint. It
    // never reads a player's whole explored map or writes tile fog.
    internal static class NativeRogueSight
    {
        [ThreadStatic] static Source? Current;
        [ThreadStatic] static Footprint? Capturing;
        [ThreadStatic] static Retaliation? Defending;
        static long Generation;
        // Combat previews use a detached apparent defender. Resolve only its
        // retained-sight identity; the battle still receives apparent stats.
        internal static Func<object,object,object>? ResolvePresentationBody;
        internal sealed class Update
        {
            internal readonly ulong Id;internal readonly byte Commander;internal readonly uint Turn;
            internal readonly HashSet<LedgerSight> Seen=new(),Pending=new();
            internal Update(LedgerUnit row){Id=row.LogicalId;Commander=row.Rogue!.Commander;Turn=row.Rogue.AcquiredTurn;}
        }
        internal sealed class Collection
        {
            internal readonly Dictionary<ulong,Update> Updates=new();
            internal NativeIdentityLedger Merge(NativeIdentityLedger ledger)=>ledger with{Units=ledger.Units.Select(row=>
            {
                if(!row.Alive||row.Rogue is not LedgerRogue r||!Updates.TryGetValue(row.LogicalId,out var u)||r.Commander!=u.Commander||r.AcquiredTurn!=u.Turn)return row;
                return row with{Rogue=r with{Sight=Sorted(u.Seen),PendingSight=Sorted(u.Pending.Except(u.Seen))}};
            }).ToArray()};
        }
        internal sealed class Source:IDisposable
        {
            internal readonly object State;internal readonly byte Actor;internal readonly Collection Collection;
            internal readonly Source? Previous;internal readonly long Epoch;internal readonly bool Publish;
            internal readonly LedgerUnit? Identity;internal readonly object? SourceUnit,Action,Map;
            internal readonly uint Turn;
            internal bool Executed;
            bool Retired;
            internal bool Active=>!Retired;
            internal Source(object state,byte actor,Collection collection,bool publish,LedgerUnit? identity=null,object? unit=null,object? action=null)
            {Identity=identity;SourceUnit=unit;Action=action;Map=Native.Get(state,"Map");Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));State=state;Actor=actor;Collection=collection;Publish=publish;Previous=Current;Epoch=Generation;Current=this;}
            public void Dispose(){if(Retired)return;Retired=true;if(ReferenceEquals(Current,this))Current=Epoch==Generation?Previous:null;}
        }
        internal sealed record Footprint(object State,byte Actor,object Unit,Update Update,Collection Collection,bool Publish,Footprint? Previous,long Epoch);
        internal sealed record TileVisit(Footprint Foot,object Tile,LedgerSight At,bool Queued,HashSet<long> OldQueue){internal bool Executed;}
        internal sealed record Exploration(object State,object Action,byte Actor,LedgerSight At,long Epoch){internal bool Executed;}
        internal sealed record Retaliation(object State,object Defender,byte Observer,HashSet<LedgerSight> Sight,Retaliation? Previous,long Epoch);
        static LedgerSight At(object coordinates)=>new(Native.Int(Native.Get(coordinates,"X")),Native.Int(Native.Get(coordinates,"Y")));
        static LedgerSight[]? Sorted(IEnumerable<LedgerSight> values){var a=values.Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y).ToArray();return a.Length==0?null:a;}
        static object? Tile(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static bool ExactTile(object state,object tile)=>Native.Same(Tile(state,Native.Get(tile,"coordinates")!),tile);
        static bool Root(object state,object unit)=>Tile(state,Native.Get(unit,"coordinates")!) is object tile&&Native.Same(Native.Get(tile,"unit"),unit);
        static bool Queued(object state,object action)=>Native.Items(Native.Get(state,"ActionStack")!).Any(x=>Native.Same(x,action));
        static LedgerUnit? Active(object state,object? unit,byte actor)
        {
            if(unit is null||!NativeRegroupSession.Ready(state)||!Root(state,unit)||Native.Int(Native.Get(unit,"health"))<=0||!NativeRogueRules.CanCommand(state,unit,actor))return null;
            return NativeRogueRules.Row(state,unit) is {Alive:true,Rogue:not null} row?row:null;
        }
        internal static bool IsIdle(object state,object unit)=>NativeRegroupSession.Ready(state)&&Root(state,unit)&&NativeRogueRules.Row(state,unit) is {Alive:true,Rogue:not null} row&&
            !NativeRogueRules.CanCommand(state,unit,row.Rogue.Commander);
        internal static void Clear(){Current=null;Capturing=null;Defending=null;Generation++;}
        internal static void Install(Harmony h)
        {
            var t=typeof(NativeRogueSight);
            Native.Hook(h,Native.M("MoveAction","Execute","GameState"),t,nameof(BeforeMove),nameof(AfterMove),nameof(SourceFinished));
            Native.Hook(h,Native.M("ActionUtils","ExploreFromTile","GameState","PlayerState","TileData","System.Int32","System.Boolean"),t,nameof(BeforeFootprint),null,nameof(FootprintFinished));
            Native.Hook(h,Native.M("ActionUtils","ExploreTile","GameState","PlayerState","TileData","System.Boolean"),t,nameof(BeforeTile),nameof(AfterTile),nameof(TileFinished));
            Native.Hook(h,Native.M("ExploreAction","Execute","GameState"),t,nameof(BeforeExplore),nameof(AfterExplore),nameof(ExploreFinished));
            Native.Hook(h,Native.M("BattleHelpers","CanDefenderRetaliate","GameState","UnitState","UnitState"),t,nameof(BeforeRetaliation),null,nameof(RetaliationFinished));
            Native.Hook(h,Native.M("TileData","GetExplored","System.Byte"),t,null,nameof(Explored));
            Native.Hook(h,Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),t,nameof(GetUnit));
        }
        internal static IDisposable ExchangeSource(object state,byte actor,object sourceTile,Collection collection,LedgerUnit? identity=null)
        {
            if(!ExactTile(state,sourceTile)||!NativeSpyNetwork.Accessible(state,sourceTile,actor))throw new InvalidDataException("Rogue sight exchange source is inaccessible.");
            return new Source(state,actor,collection,false,identity,Native.Get(sourceTile,"unit"));
        }
        public static void BeforeMove(object __instance,object __0,out Source? __state)
        {
            __state=null;if(!NativeRegroupSession.Ready(__0)||!Queued(__0,__instance))return;
            byte actor=Convert.ToByte(Native.Get(__instance,"PlayerId"));object?[] args={Convert.ToUInt32(Native.Get(__instance,"UnitId")),null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),__0,args)!)return;
            var unit=args[1];var identity=Active(__0,unit,actor);if(identity is null)return;
            var tile=Tile(__0,Native.Get(unit!,"coordinates")!);if(tile is null||!NativeSpyNetwork.Accessible(__0,tile,actor))return;
            __state=new(__0,actor,new(),true,identity,unit,__instance);
        }
        static bool CompletedMove(Source source)
        {
            if(!source.Active||source.Action is null||source.SourceUnit is null||source.Identity is not LedgerUnit before||
                source.Epoch!=Generation||!source.Executed||!NativeRegroupSession.Ready(source.State)||
                !Native.Same(source.Map,Native.Get(source.State,"Map"))||source.Turn!=Convert.ToUInt32(Native.Get(source.State,"CurrentTurn"))||
                !Queued(source.State,source.Action)||!Root(source.State,source.SourceUnit)||
                !NativeRogueRules.CanCommand(source.State,source.SourceUnit,source.Actor)||Native.Int(Native.Get(source.SourceUnit,"health"))<=0)return false;
            var current=NativeRogueRules.Row(source.State,source.SourceUnit);
            return current is {Alive:true,Rogue:not null}&&current.LogicalId==before.LogicalId&&current.NativeId==before.NativeId&&
                current.Rogue.Owner==before.Rogue!.Owner&&current.Rogue.Commander==before.Rogue.Commander&&current.Rogue.AcquiredTurn==before.Rogue.AcquiredTurn;
        }
        public static void AfterMove(bool __runOriginal,Source? __state)
        {if(__state is not null)__state.Executed=__runOriginal;}
        public static Exception? SourceFinished(Exception? __exception,Source? __state)
        {
            if(__state is null)return __exception;
            try{if(__exception is null&&__state.Publish&&CompletedMove(__state))Publish(__state.State,__state.Collection);}
            catch(Exception e){NativeRegroupSession.Fault("Rogue local sight publication failed: "+e.Message);return __exception??e;}
            finally{__state.Dispose();}return __exception;
        }
        public static void BeforeFootprint(object __0,object __1,object __2,int __3,out Footprint? __state)
        {
            __state=null;if(!ExactTile(__0,__2))return;
            byte actor=Convert.ToByte(Native.Get(__1,"Id"));object? unit=Native.Get(__2,"unit");
            var source=Current;bool explicitSource=source is not null&&source.Epoch==Generation&&Native.Same(source.State,__0)&&source.Actor==actor;
            var row=Active(__0,unit,actor);
            // A coast exchange stages the new wrapper before publishing its
            // ledger. Only the caller's exact candidate/body pair may bridge it.
            if(explicitSource&&source!.Identity is LedgerUnit candidate&&unit is not null&&Native.Same(source.SourceUnit,unit)&&
                candidate.Alive&&candidate.NativeId==Convert.ToUInt32(Native.Get(unit,"id"))&&candidate.Rogue is LedgerRogue rogue&&
                rogue.Commander==actor&&rogue.AcquiredTurn==Convert.ToUInt32(Native.Get(__0,"CurrentTurn"))&&
                Native.Int(Native.Get(__0,"CurrentPlayer"))==actor&&Native.Int(Native.Get(unit,"owner"))==actor&&
                Native.Int(Native.Get(unit,"health"))>0&&NativeRegroupSession.Ready(__0)&&Root(__0,unit))row=candidate;
            if(row is null||!explicitSource)return;
            int range=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetSightRange","Polytopia.Data.UnitData"),null,Native.Get(unit!,"UnitData")));
            if(__3!=range||range is <1 or >2)return;
            var collection=source!.Collection;var update=new Update(row);collection.Updates[row.LogicalId]=update;
            __state=new(__0,actor,unit!,update,collection,false,Capturing,Generation);Capturing=__state;
        }
        public static void BeforeTile(object __0,object __1,object __2,bool __3,out TileVisit? __state)
        {
            __state=null;if(Capturing is not Footprint f||f.Epoch!=Generation||!Native.Same(f.State,__0)||f.Actor!=Convert.ToByte(Native.Get(__1,"Id"))||!ExactTile(__0,__2))return;
            var queue=Native.Items(Native.Get(__0,"ActionStack")!).Select(Native.Pointer).ToHashSet();if(queue.Count>16384)throw new InvalidDataException("Rogue sight queue exceeds its bound.");
            if(f.Update.Seen.Count+f.Update.Pending.Count>=8192)throw new InvalidDataException("Rogue footprint exceeds its bound.");
            __state=new(f,__2,At(Native.Get(__2,"coordinates")!),__3,queue);
        }
        public static void AfterTile(bool __runOriginal,TileVisit? __state){if(__state is not null)__state.Executed=__runOriginal;}
        public static Exception? TileFinished(Exception? __exception,TileVisit? __state)
        {
            if(__exception is not null||__state is not TileVisit v||!v.Executed||v.Foot.Epoch!=Generation)return __exception;
            if((bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),v.Tile,v.Foot.Actor)!)v.Foot.Update.Seen.Add(v.At);
            else if(v.Queued&&Native.Items(Native.Get(v.Foot.State,"ActionStack")!).Any(a=>!v.OldQueue.Contains(Native.Pointer(a))&&IsExplore(a,v.Foot.Actor,v.At)))v.Foot.Update.Pending.Add(v.At);
            return __exception;
        }
        static bool IsExplore(object action,byte actor,LedgerSight at)
        {try{var e=Native.As(action,"ExploreAction");return Convert.ToByte(Native.Get(e,"PlayerId"))==actor&&At(Native.Get(e,"Coordinates")!)==at;}catch{return false;}}
        public static Exception? FootprintFinished(Exception? __exception,Footprint? __state)
        {
            if(__state is null)return __exception;
            try{if(__exception is null&&__state.Epoch==Generation&&__state.Publish)Publish(__state.State,__state.Collection);}
            catch(Exception e){NativeRegroupSession.Fault("Rogue footprint failed: "+e.Message);return __exception??e;}
            finally{if(ReferenceEquals(Capturing,__state))Capturing=__state.Epoch==Generation?__state.Previous:null;}return __exception;
        }
        internal static void CompleteCollection(object state,Collection collection)
        {
            if(!NativeRegroupSession.Ready(state))throw new InvalidDataException("Rogue sight completion lost its bound state.");
            Publish(state,collection);
        }
        static void Publish(object state,Collection collection)
        {
            if(collection.Updates.Count==0)return;
            if(!NativeRegroupSession.Ready(state))throw new InvalidDataException("Rogue sight publication lost its bound state.");
            var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Missing Rogue sight ledger.");
            // A lethal native attack removes its victim before its follow-up
            // MoveAction explores the destination. Reconcile that completed
            // native history before adding sight, then publish one validated
            // snapshot. Otherwise an unrelated dead row stops the whole match.
            // This method is called only at completed action boundaries, never
            // from a renderer or during a staged exchange footprint.
            var reconciled=NativeLedgerState.Reconcile(state,snapshot);
            var ledger=NativeIdentityLedgerCodec.Decode(reconciled.Payload,reconciled.Revision);
            var candidate=collection.Merge(ledger);
            bool sightChanged=!NativeIdentityLedgerCodec.Encode(candidate,reconciled.Revision).SequenceEqual(reconciled.Payload);
            if(!reconciled.Changed&&!sightChanged)return;
            long revision=sightChanged?checked(reconciled.Revision+1):reconciled.Revision;
            NativeGameStateEnvelope.Set(state,revision,NativeIdentityLedgerCodec.Encode(candidate,revision));
        }
        public static void BeforeExplore(object __instance,object __0,out Exploration? __state)
        {
            __state=null;if(!NativeRegroupSession.Ready(__0)||!Queued(__0,__instance))return;
            byte actor=Convert.ToByte(Native.Get(__instance,"PlayerId"));var at=At(Native.Get(__instance,"Coordinates")!);
            var snapshot=NativeGameStateEnvelope.Get(__0);if(snapshot is null)return;
            if(!NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Units.Any(r=>r.Alive&&r.Rogue?.Commander==actor&&r.Rogue.PendingSight?.Contains(at)==true))return;
            __state=new(__0,__instance,actor,at,Generation);
        }
        public static void AfterExplore(bool __runOriginal,Exploration? __state){if(__state is not null)__state.Executed=__runOriginal;}
        public static Exception? ExploreFinished(Exception? __exception,Exploration? __state)
        {
            if(__exception is not null||__state is not Exploration e||!e.Executed||e.Epoch!=Generation||!NativeRegroupSession.Ready(e.State))return __exception;
            try
            {
                var tile=Tile(e.State,Native.Get(e.Action,"Coordinates")!);if(tile is null||!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,e.Actor)!)return null;
                var snapshot=NativeGameStateEnvelope.Get(e.State)!;var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
                var rows=ledger.Units.Select(r=>r.Alive&&r.Rogue is LedgerRogue rogue&&rogue.Commander==e.Actor&&rogue.PendingSight?.Contains(e.At)==true?
                    r with{Rogue=rogue with{Sight=Sorted((rogue.Sight??Array.Empty<LedgerSight>()).Append(e.At)),PendingSight=Sorted(rogue.PendingSight.Where(p=>p!=e.At))}}:r).ToArray();
                var candidate=ledger with{Units=rows};if(NativeIdentityLedgerCodec.Encode(candidate,snapshot.Revision).SequenceEqual(snapshot.Bytes()))return null;
                long revision=checked(snapshot.Revision+1);NativeGameStateEnvelope.Set(e.State,revision,NativeIdentityLedgerCodec.Encode(candidate,revision));
            }
            catch(Exception ex){NativeRegroupSession.Fault("Rogue queued sight failed: "+ex.Message);return ex;}return null;
        }
        public static void BeforeRetaliation(object __0,object __1,object __2,out Retaliation? __state)
        {
            var body=ResolvePresentationBody?.Invoke(__0,__2)??__2;
            __state=null;if(!IsIdle(__0,body))return;
            var row=NativeRogueRules.Row(__0,body)!;byte observer=Convert.ToByte(Native.Get(body,"owner"));
            __state=new(__0,body,observer,(row.Rogue!.Sight??Array.Empty<LedgerSight>()).ToHashSet(),Defending,Generation);Defending=__state;
        }
        static bool Bound(Retaliation r,object tile,byte observer)=>r.Epoch==Generation&&observer==r.Observer&&NativeRegroupSession.Ready(r.State)&&Root(r.State,r.Defender)&&ExactTile(r.State,tile);
        public static void Explored(object __instance,byte __0,ref bool __result)
        {if(Defending is Retaliation r&&Bound(r,__instance,__0))__result=r.Sight.Contains(At(Native.Get(__instance,"coordinates")!))&&!NativeBlackout.Covered(r.State,__0,NativeSpyNetwork.At(Native.Get(__instance,"coordinates")!));}
        public static bool GetUnit(object __instance,object __0,byte __1,ref bool __2,ref object? __result)
        {
            if(Defending is not Retaliation r||!Native.Same(r.State,__0)||!Bound(r,__instance,__1))return true;
            if(!r.Sight.Contains(At(Native.Get(__instance,"coordinates")!))||NativeBlackout.Covered(__0,__1,NativeSpyNetwork.At(Native.Get(__instance,"coordinates")!))){__result=null;return false;}
            // Native ignoreFog bypasses only its raw explorers-list read. It
            // still calls UnitState.IsInvisible(state, observer) afterward.
            __2=true;return true;
        }
        public static Exception? RetaliationFinished(Exception? __exception,Retaliation? __state)
        {if(__state is not null&&ReferenceEquals(Defending,__state))Defending=__state.Epoch==Generation?__state.Previous:null;return __exception;}
    }
}
