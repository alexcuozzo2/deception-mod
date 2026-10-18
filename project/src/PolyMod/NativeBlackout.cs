using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Blackout changes ordinary native exploration and separately records why
    // a later passive refresh must not discover it. It has no expiry or stacks.
    internal static class NativeBlackout
    {
        sealed record Binding(NativeLedgerOwnership.WeakIdentity State,NativeLedgerOwnership.WeakIdentity Tile,byte[] Players);
        static readonly Dictionary<long,Binding> CoveredTiles=new();
        sealed class Mask
        {
            internal readonly HashSet<(byte,int,int)> Cells;
            internal Mask(NativeGameStateEnvelope.Snapshot snapshot)=>Cells=new((NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Blackouts??Array.Empty<LedgerBlackout>()).Select(x=>(x.Player,x.X,x.Y)));
        }
        static ConditionalWeakTable<NativeGameStateEnvelope.Snapshot,Mask> Masks=new();
        [ThreadStatic] static RouteQuery? Routing;
        static long RouteGeneration;
        internal sealed class RouteQuery
        {
            internal readonly RouteQuery? Previous;
            internal readonly long Generation;
            internal object? State,Map,Frame;
            internal uint Turn;
            internal bool Closed;
            internal RouteQuery(RouteQuery? previous,long generation){Previous=previous;Generation=generation;}
        }
        internal static Action<NativeExchangePlan,byte[]>? Presentation;
        static void Require(bool yes,string why){if(!yes)throw new InvalidDataException(why);}
        internal static object ExplorerList(IEnumerable<object> items)
        {
            // System.Byte is forwarded by more than one loaded assembly. Use
            // its CLR type directly, not Native.T's unique-wrapper lookup.
            var type=Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(typeof(byte));
            object list=Activator.CreateInstance(type)!;var add=Native.M(type,"Add","System.Byte");
            foreach(var value in items)Native.Invoke(add,list,Convert.ToByte(value));return list;
        }
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("TileData","SetExplored","System.Byte","System.Boolean"),typeof(NativeBlackout),nameof(BeforeSetExplored));
            Native.Hook(h,Native.M("TileData","GetExplored","System.Byte"),typeof(NativeBlackout),null,nameof(AfterGetExplored));
            InstallRoutes(h);
            NativeBlackoutLighthouse.Install(h);NativeBlackoutDiscovery.Install(h);NativeBlackoutAccess.Install(h);NativeBlackoutUi.Install(h);
            NativeRegroupSession.OnSessionEnded+=Clear;
        }
        internal static void InstallRoutes(Harmony h)
        {
            string tiles=Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(Native.T("TileData")).FullName!;
            Native.Hook(h,Native.M("MapDataExtensions","UpdateRoutes","GameState",tiles),
                typeof(NativeBlackout),nameof(BeforeRoutes),null,nameof(RoutesFinished));
        }
        internal static void Clear(){CoveredTiles.Clear();Masks=new();RouteGeneration++;Routing=null;NativeBlackoutLighthouse.Clear();NativeBlackoutDiscovery.Clear();NativeBlackoutUi.Clear();}
        public static void BeforeRoutes(object __0,bool __runOriginal,out RouteQuery __state)
        {
            // Even an unrelated/nested calculation masks the caller's scope.
            // Install the inert token before any fallible native reads so that
            // failed setup cannot accidentally expose the outer calculation.
            __state=new(Routing,RouteGeneration);Routing=__state;
            try
            {
                if(!__runOriginal||!NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(__0))return;
                var frame=NativeGameStateEnvelope.Get(__0);if(frame is null)return;
                object? map=Native.Get(__0,"Map");if(map is null)return;
                uint turn=Convert.ToUInt32(Native.Get(__0,"CurrentTurn"));
                __state.State=__0;__state.Map=map;__state.Frame=frame;__state.Turn=turn;
            }
            catch{ /* Inert scope: preserve the original native calculation. */ }
        }
        public static Exception? RoutesFinished(Exception? __exception,RouteQuery? __state)
        {
            if(__state is not null)
            {
                __state.Closed=true;
                if(ReferenceEquals(Routing,__state))Routing=__state.Generation==RouteGeneration?__state.Previous:null;
            }
            return __exception;
        }
        static bool OwnedCityRoute(object state,object tile,byte player)
        {
            // Native UpdateRoutes is synchronous and has no presentation or
            // discovery side effects. Its router calls GetExplored before its
            // city/road/port checks. Let only that calculation use an owner's
            // existing city; never reveal the tile or freeze old connections.
            var q=Routing;if(q is null||q.Closed||q.Generation!=RouteGeneration||q.State is null||player is 0 or byte.MaxValue)return false;
            try
            {
                return Native.Same(state,q.State)&&NativeRegroupSession.Enabled&&NativeRegroupSession.Ready(state)&&
                    ReferenceEquals(q.Frame,NativeGameStateEnvelope.Get(state))&&Native.Same(q.Map,Native.Get(state,"Map"))&&
                    q.Turn==Convert.ToUInt32(Native.Get(state,"CurrentTurn"))&&Convert.ToByte(Native.Get(tile,"owner"))==player&&
                    Native.Same(tile,Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,q.Map,Native.Get(tile,"coordinates")))&&
                    (bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),tile,Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!;
            }
            catch{return false;}
        }
        internal static bool Covered(object state,byte player,NativeSpyNetwork.Position at)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return false;
            return Masks.GetValue(snapshot,s=>new(s)).Cells.Contains((player,at.X,at.Y));
        }
        internal static void Bind(object state)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return;
            var marks=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Blackouts??Array.Empty<LedgerBlackout>();
            // Replace this state's index, retire expired native handles; never
            // evict a different live state to make room for a new binding.
            foreach(var key in CoveredTiles.Where(p=>p.Value.State.Target() is null||p.Value.Tile.Target() is null||p.Value.State.Matches(state)).Select(p=>p.Key).ToArray())CoveredTiles.Remove(key);
            if(marks.Length==0)return;
            var tiles=NativeSpyNetwork.Tiles(state);
            foreach(var group in marks.GroupBy(m=>new NativeSpyNetwork.Position(m.X,m.Y)))
            {
                Require(tiles.TryGetValue(group.Key,out var tile),"Blackout references an absent native tile.");
                long key=Native.Pointer(tile!);
                Require(!CoveredTiles.TryGetValue(key,out var old)||old.State.Matches(state),"Covered tile aliases another native state.");
                Require(CoveredTiles.Count<NativeSpyNetwork.MaxTiles*128,"Too many live covered tile bindings.");
                CoveredTiles[key]=new(new(state),new(tile!),group.Select(x=>x.Player).ToArray());
            }
        }
        public static bool BeforeSetExplored(object __instance,byte __0,bool __1)
        {
            if(!__1||!CoveredTiles.TryGetValue(Native.Pointer(__instance),out var binding))return true;
            if(!binding.Tile.Matches(__instance))return true;
            object? state=binding.State.Target();if(state is null)return true;
            return !binding.Players.Contains(__0)||NativeBlackoutDiscovery.MayWrite(state,__instance,__0);
        }
        public static void AfterGetExplored(object __instance,byte __0,ref bool __result)
        {
            if(!CoveredTiles.TryGetValue(Native.Pointer(__instance),out var binding)||!binding.Tile.Matches(__instance))return;
            object? state=binding.State.Target();
            if(state is null||!binding.Players.Contains(__0))return;
            if(OwnedCityRoute(state,__instance,__0)){__result=true;return;}
            if(__result&&!NativeBlackoutDiscovery.MayWrite(state,__instance,__0))__result=false;
        }
        internal static bool TryOffer(object state,uint id,int x,int y,out NativeExchangeIntent? intent,out string reason)
        {
            intent=null;reason="";
            try
            {
                Require(NativeRegroupSession.Ready(state),"Blackout needs an active owned match.");
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Blackout ledger is absent.");
                var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
                var row=ledger.Units.SingleOrDefault(r=>r.Alive&&r.NativeId==id);
                Require(row is not null&&row.Kind==LedgerActualKind.Genuine,"Blackout needs a genuine Fogger identity.");
                object unit=NativeSpyNetwork.Unit(state,id)??throw new InvalidDataException("Blackout source is absent.");
                var at=NativeSpyNetwork.At(Native.Get(unit,"coordinates")!);byte actor=Convert.ToByte(Native.Get(state,"CurrentPlayer"));
                var source=new NativeExchangeUnit(row!.LogicalId,id,at.X,at.Y);
                var offered=new NativeExchangeIntent(ledger.Epoch,ledger.NextSequence,snapshot.Revision,Convert.ToUInt32(Native.Get(state,"CurrentTurn")),actor,
                    NativeExchangeKind.Blackout,false,NativePartnerReference.Existing,ledger.NextLogicalId,source,source with{X=x,Y=y},SHA256.HashData(snapshot.Bytes()));
                if(!TryPlan(state,offered,out _,out reason))return false;intent=offered;return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason)
        {
            plan=null;reason="";
            try
            {
                Require(intent.Kind==NativeExchangeKind.Blackout,"Invalid Blackout intent.");
                var request=NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(intent));
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Blackout ledger is absent.");
                var original=snapshot.Bytes();var ledger=NativeIdentityLedgerCodec.Decode(original,snapshot.Revision);
                if(NativeExchangeIntentCodec.AlreadyCommitted(ledger,snapshot.Revision,request))
                {plan=new(state,request,true,snapshot.Revision,snapshot.Revision,original,original);return true;}
                NativeLedgerState.Validate(state,snapshot.Revision,original);
                Require(SHA256.HashData(original).SequenceEqual(request.SourceAppearanceHash),"Blackout state changed after targeting.");
                Require(Convert.ToUInt32(Native.Get(state,"CurrentTurn"))==request.Turn,"Blackout turn changed.");
                var row=ledger.Units.SingleOrDefault(r=>r.Alive&&r.LogicalId==request.Initiator.LogicalId&&r.NativeId==request.Initiator.NativeId);
                Require(row?.Kind==LedgerActualKind.Genuine,"Blackout source identity changed.");
                Require(NativeSpyNetwork.TryFoggerReachable(state,request.Actor,request.Initiator.NativeId,out var targets,out var why),why);
                object unit=NativeSpyNetwork.Unit(state,request.Initiator.NativeId)!;
                var at=NativeSpyNetwork.At(Native.Get(unit,"coordinates")!);
                Require(at==new NativeSpyNetwork.Position(request.Initiator.X,request.Initiator.Y),"Initiating Fogger moved after targeting.");
                object? target=targets.SingleOrDefault(t=>NativeSpyNetwork.At(Native.Get(t,"coordinates")!)==new NativeSpyNetwork.Position(request.Partner.X,request.Partner.Y));
                Require(target is not null,"Blackout tile is outside the initiating Fogger's range.");
                // Never inspect any target unit, enemy mask, owner or relation
                // for eligibility. Empty and caster-unexplored cells qualify.
                var players=Native.Items(Native.Get(state,"PlayerStates")!).Select(p=>Convert.ToByte(Native.Get(p,"Id"))).ToArray();
                Require(players.Length is >0 and <=256&&players.Distinct().Count()==players.Length&&players.Contains(request.Actor),"Blackout player census is invalid.");
                var added=players.Where(p=>p!=request.Actor&&p!=0&&p!=byte.MaxValue).Select(p=>new LedgerBlackout(p,request.Partner.X,request.Partner.Y)).ToArray();
                var marks=(ledger.Blackouts??Array.Empty<LedgerBlackout>()).Concat(added).Distinct().OrderBy(m=>m.Player).ThenBy(m=>m.X).ThenBy(m=>m.Y).ToArray();
                var discovery=(ledger.Discoveries??Array.Empty<LedgerDiscovery>()).Where(d=>!added.Any(m=>m.Player==d.Player&&m.X==d.X&&m.Y==d.Y)).ToArray();
                long after=checked(snapshot.Revision+1);
                var receipt=new LedgerReceipt(request.Sequence,NativeExchangeIntentCodec.Operation(request.Sequence),NativeExchangeIntentCodec.Fingerprint(request),snapshot.Revision,after,new[]{row!.LogicalId});
                var candidate=ledger with{NextSequence=checked(ledger.NextSequence+1),Blackouts=marks,Discoveries=discovery,
                    Units=ledger.Units.Select(r=>r.LogicalId==row.LogicalId?r with{Independent=false,Appearance=null}:r).ToArray(),Receipts=ledger.Receipts.Append(receipt).ToArray()};
                NativeBlackoutLighthouse.PrepareForget(state,target!,request.Actor);
                var sourceTile=NativeSpyNetwork.Tiles(state)[at];
                plan=new(state,request,false,snapshot.Revision,after,original,NativeIdentityLedgerCodec.Encode(candidate,after),a:unit,tileA:sourceTile,tileB:target,
                    finalA:new(at.X,at.Y),finalB:new(request.Partner.X,request.Partner.Y));return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason)
        {
            result=null;reason="";NativeExchangePlan? plan=null;object? explorers=null,effects=null;bool moved=false,attacked=false,changed=false;NativeBlackoutLighthouse.Forget? lighthouse=null;
            try
            {
                // Outer NativeExchangeCommit owns live native-action authority
                // and reentry protection, as for other custom Build intents.
                Require(TryPlan(state,intent,out plan,out var why),why);
                if(plan!.AlreadyCommitted){result=new(true,plan.BeforeRevision,plan.AfterRevision,plan);return true;}
                object unit=plan.A!,target=plan.TileB!;explorers=Native.Get(target,"explorers");effects=Native.Get(unit,"effects");
                moved=(bool)Native.Get(unit,"moved")!;attacked=(bool)Native.Get(unit,"attacked")!;
                var visualObservers=explorers is null?Array.Empty<byte>():Native.Items(explorers).Select(Convert.ToByte).Distinct().ToArray();
                object fog=ExplorerList(explorers is null?Array.Empty<object>():Native.Items(explorers).Where(p=>Convert.ToByte(p)==intent.Actor));
                object staged=Native.NewList("UnitEffect",Native.Items(effects!));
                lighthouse=NativeBlackoutLighthouse.PrepareForget(state,target,intent.Actor);
                Require(TryPlan(state,intent,out var fresh,out why)&&fresh is not null&&!fresh.AlreadyCommitted&&Native.Same(fresh.A,unit)&&Native.Same(fresh.TileB,target)&&
                    fresh.OriginalPayload.SequenceEqual(plan.OriginalPayload)&&fresh.CandidatePayload.SequenceEqual(plan.CandidatePayload),"Blackout changed during staging: "+why);
                changed=true;Native.Set(unit,"effects",staged);NativeFoggerActions.SpendBlackout(state,unit);
                Native.Set(unit,"effects",NativeDisguiseConcealment.WithConcealment(Native.Get(unit,"effects")!,null,null!));
                lighthouse?.Apply();Native.Set(target,"explorers",fog);
                NativeLedgerState.Validate(state,plan.AfterRevision,plan.CandidatePayload);NativeGameStateEnvelope.Set(state,plan.AfterRevision,plan.CandidatePayload);Bind(state);NativeBlackoutLighthouse.RecordCommit(state,target,intent.Actor,visualObservers,lighthouse);
                try{Presentation?.Invoke(plan,visualObservers);}catch(Exception visual){Entry.Note("blackout-presentation-record-error",visual.Message);}
                result=new(false,plan.BeforeRevision,plan.AfterRevision,plan);return true;
            }
            catch(Exception e)
            {
                if(changed&&plan is not null)
                {
                    try{lighthouse?.Rollback();Native.Set(plan.A!,"effects",effects);Native.Set(plan.A!,"moved",moved);Native.Set(plan.A!,"attacked",attacked);Native.Set(plan.TileB!,"explorers",explorers);NativeGameStateEnvelope.Set(state,plan.BeforeRevision,plan.OriginalPayload);Bind(state);}
                    catch(Exception rollback){NativeRegroupSession.Fault("Blackout rollback failed: "+rollback.Message);reason=new AggregateException(e,rollback).Message;return false;}
                }
                reason=e.Message;return false;
            }
        }
    }
}
