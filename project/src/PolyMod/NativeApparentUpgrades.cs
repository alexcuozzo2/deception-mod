using System;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // An appearance upgrade is a complete single-unit private Build intent.
    // Native UpgradeAction must not execute: it trains a genuine replacement,
    // changes investment/score and explores from the purchased vessel.
    internal static class NativeApparentUpgrades
    {
        [ThreadStatic] static bool Applying;
        internal static bool IsKind(NativeExchangeKind kind)=>NativeExchangeCatalog.IsUpgrade(kind);
        internal static string Form(NativeExchangeKind kind)=>NativeExchangeCatalog.UpgradeTarget(kind);
        internal static readonly NativeExchangeKind[] Kinds={NativeExchangeKind.UpgradeScout,NativeExchangeKind.UpgradeRammer,NativeExchangeKind.UpgradeBomber};
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
        static object Player(object state,byte actor)
        {
            object?[] args={actor,null};
            Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&args[1] is not null,"The upgrade owner is absent.");
            return args[1]!;
        }
        static bool Ability(object data,int kind)=>Native.Get(data,"unitAbilities") is object values&&Native.Items(values).Any(x=>Native.Int(x)==kind);
        static bool OwnTerritory(object tile,byte actor)=>Native.Int(Native.Get(tile,"owner"))==actor;
        static bool Technology(object state,object player,string target)
        {
            if((bool)Native.Invoke(Native.M("PlayerExtensions","HasTech","PlayerState",RecruitmentHooks.TT),null,player,
                Native.EnumValue(RecruitmentHooks.TT,TechTree.PropagandaId))!)return true;
            // Native uses the exact upgradesFrom data-reference relation and
            // the owner's real unlocked roster, including tribe substitutions.
            var options=Native.Invoke(Native.M(RecruitmentHooks.GLD,"GetUnlockedUpgradesForUnit","PlayerState","GameState","Polytopia.Data.UnitData"),
                Native.Get(state,"GameLogicData"),player,state,RecruitmentHooks.Data(state,"transportship"))!;
            return Native.Items(options).Any(x=>Native.EnumName(Native.Get(x,"type")!)==target);
        }
        internal static bool TryOffer(object state,uint unitId,NativeExchangeKind kind,out NativeExchangeIntent? intent,out string reason)
        {
            intent=null;reason="";
            try
            {
                Require(IsKind(kind)&&NativeRegroupSession.Ready(state),"Appearance upgrades need an active owned match.");
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Missing private upgrade ledger.");
                var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
                var row=ledger.Units.SingleOrDefault(x=>x.Alive&&x.NativeId==unitId);
                Require(row is not null&&row.Independent&&row.Appearance is not null,"This unit has no independent appearance.");
                object?[] args={unitId,null};
                Require((bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,args)!&&args[1] is not null,"Upgrade unit is absent.");
                object unit=args[1]!,at=Native.Get(unit,"coordinates")!;
                var reference=new NativeExchangeUnit(row!.LogicalId,unitId,Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")));
                var offered=new NativeExchangeIntent(ledger.Epoch,ledger.NextSequence,snapshot.Revision,Convert.ToUInt32(Native.Get(state,"CurrentTurn")),
                    Convert.ToByte(Native.Get(unit,"owner")),kind,false,NativePartnerReference.Existing,ledger.NextLogicalId,reference,reference,
                    NativeExchangeIntentCodec.AppearanceFingerprint(row.Appearance!));
                if(!TryPlan(state,offered,out _,out reason))return false;
                intent=offered;return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason)
        {
            plan=null;reason="";
            try
            {
                Require(IsKind(intent.Kind)&&Native.T("GameState").IsInstanceOfType(state),"Invalid appearance upgrade request.");
                var request=NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(intent));
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Missing private upgrade ledger.");
                byte[] original=snapshot.Bytes();long revision=snapshot.Revision;
                var ledger=NativeIdentityLedgerCodec.Decode(original,revision);
                if(NativeExchangeIntentCodec.AlreadyCommitted(ledger,revision,request))
                {plan=new(state,request,true,revision,revision,original,original);return true;}
                NativeLedgerState.Validate(state,revision,original);
                Require(NativeRegroupSession.Ready(state)&&RecruitmentHooks.Registered,"Appearance upgrade session is unavailable.");
                Require(Equals(Native.Get(state,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started"))&&
                    Native.Int(Native.Get(state,"CurrentPlayer"))==request.Actor&&Convert.ToUInt32(Native.Get(state,"CurrentTurn"))==request.Turn,"The upgrade turn or owner changed.");
                var row=ledger.Units.SingleOrDefault(x=>x.LogicalId==request.Initiator.LogicalId);
                Require(row is not null&&row.Alive&&row.NativeId==request.Initiator.NativeId&&row.Independent&&row.Appearance is not null,"The upgrade identity changed.");
                var appearance=row!.Appearance!;
                Require(!NativeShedDisguise.Exposed(appearance),"An exposed Mimic must Regroup before feigning a naval upgrade.");
                Require(appearance.Form=="transportship"&&appearance.Passenger is not null,"Only an independent Raft appearance can feign a naval upgrade.");
                Require(NativeExchangeIntentCodec.AppearanceFingerprint(appearance).SequenceEqual(request.SourceAppearanceHash),"The source appearance changed.");
                var graph=NativeLedgerState.Graph(state);
                Require(graph.TryGetValue(row.NativeId,out var body)&&body.Root,"The upgrade unit is not a map root.");
                object unit=body!.Unit,at=Native.Get(unit,"coordinates")!;
                Require(Native.Int(Native.Get(unit,"owner"))==request.Actor&&Native.Int(Native.Get(unit,"health"))>0&&
                    !(bool)Native.Invoke(Native.M("UnitState","HasLeader"),unit)!&&!(bool)Native.Invoke(Native.M("UnitState","HasFollower"),unit)!,"The upgrade unit is foreign, dead, or linked.");
                Require(Native.Int(Native.Get(at,"X"))==request.Initiator.X&&Native.Int(Native.Get(at,"Y"))==request.Initiator.Y,"The upgrade unit moved.");
                var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
                Require(tile is not null&&Native.Same(Native.Get(tile,"unit"),unit)&&OwnTerritory(tile,request.Actor)&&
                    (bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,request.Actor)!,"Feign upgrades require the owner's accessible territory.");
                string target=Form(request.Kind);object data=RecruitmentHooks.Data(state,target),raft=RecruitmentHooks.Data(state,"transportship");
                Require(Ability(data,11)&&Native.Same(Native.Get(data,"upgradesFrom"),raft),"This native vessel is not a Raft upgrade.");
                Require(Technology(state,Player(state,request.Actor),target),"Research this naval unit or Propaganda first.");
                int max=Ability(data,39)?Native.Int(Native.Get(data,"health")):appearance.Passenger!.Maximum;
                Require(max>0&&max<=ushort.MaxValue&&appearance.Health<=max,"The native upgrade's apparent health profile is unsupported.");
                var next=appearance with{Form=target,Maximum=(ushort)max};
                string? carrier=null;
                if(row.Kind==LedgerActualKind.Mimic)
                {
                    carrier=RecruitmentCatalog.Variant(target);object native=RecruitmentHooks.Data(state,carrier);
                    Require(RecruitmentHooks.Forms.TryGetValue(carrier,out var form)&&form.NativeId==target&&
                        Native.Int(Native.Get(native,"health"))==10&&Native.Int(Native.Get(native,"attack"))==0&&Native.Int(Native.Get(native,"defence"))==0&&Native.Int(Native.Get(native,"range"))==0,
                        "The upgrade carrier violates real Mimic rules.");
                }
                long after=checked(revision+1);
                var receipt=new LedgerReceipt(request.Sequence,NativeExchangeIntentCodec.Operation(request.Sequence),NativeExchangeIntentCodec.Fingerprint(request),revision,after,new[]{row.LogicalId});
                var candidate=ledger with{NextSequence=checked(ledger.NextSequence+1),Units=ledger.Units.Select(x=>x.LogicalId==row.LogicalId?x with{Appearance=next}:x).ToArray(),Receipts=ledger.Receipts.Concat(new[]{receipt}).ToArray()};
                byte[] payload=NativeIdentityLedgerCodec.Encode(candidate,after);
                plan=new(state,request,false,revision,after,original,payload,a:unit,tileA:tile,carrierA:carrier,typeA:carrier??Native.EnumName(Native.Get(unit,"type")!),counter:Convert.ToUInt32(Native.Get(state,"CurrentUnitId")));
                return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason)
        {
            result=null;reason="";bool entered=false,changed=false;NativeExchangePlan? plan=null;
            object? beforeType=null,beforeData=null;NativeGrowthSight.Pending? sight=null;
            try
            {
                // NativeExchangeCommit dispatches only after its existing exact
                // live-action or detached-trial authorization and reentry guard.
                Require(!Applying,"Reentrant apparent upgrade action.");
                Require(TryPlan(state,intent,out plan,out var why),why);
                if(plan!.AlreadyCommitted){result=new(true,plan.BeforeRevision,plan.AfterRevision,plan);return true;}
                object unit=plan.A!;beforeType=Native.Get(unit,"type");beforeData=Native.Get(unit,"UnitData");
                Applying=true;entered=true;changed=true;
                if(plan.FinalMimicCarrierA is string carrier)
                {
                    Native.Set(unit,"type",Native.EnumValue(RecruitmentHooks.UT,carrier));Native.Set(unit,"UnitData",RecruitmentHooks.Data(state,carrier));
                    // Only Mimics gain operative sight from their current form.
                    sight=NativeGrowthSight.Stage(state,intent.Actor,new[]{unit});
                }
                NativeLedgerState.Validate(state,plan.AfterRevision,plan.CandidatePayload);
                NativeGameStateEnvelope.Set(state,plan.AfterRevision,plan.CandidatePayload);
                sight?.Accept();result=new(false,plan.BeforeRevision,plan.AfterRevision,plan);return true;
            }
            catch(Exception e)
            {
                if(changed&&plan?.A is object unit)
                {
                    try
                    {
                        sight?.Rollback();Native.Set(unit,"type",beforeType);Native.Set(unit,"UnitData",beforeData);
                        NativeGameStateEnvelope.Set(state,plan.BeforeRevision,plan.OriginalPayload);
                    }
                    catch(Exception rollback){NativeRegroupSession.Fault("Apparent upgrade rollback failed: "+rollback.Message);reason=new AggregateException(e,rollback).Message;return false;}
                }
                reason=e.Message;return false;
            }
            finally{sight?.Dispose();if(entered)Applying=false;}
        }
    }
}
