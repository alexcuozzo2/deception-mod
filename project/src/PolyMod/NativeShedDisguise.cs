using System;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // A complete persisted single-body command. Removing a mask never restores
    // allowance, spends stars, changes genuine investment, or grants powers.
    internal static class NativeShedDisguise
    {
        internal static Func<object,object,Action?>? PreparePresentation;
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
        internal static bool Exposed(LedgerAppearance? a)=>a is not null&&(RecruitmentCatalog.IsMenuId(a.Form)||a.Passenger is not null&&RecruitmentCatalog.IsMenuId(a.Passenger.Form));
        internal static bool TryOffer(object state,uint id,out NativeExchangeIntent? intent,out string reason)
        {
            intent=null;reason="";
            try
            {
                Require(NativeRegroupSession.Ready(state),"Shed Disguise needs an active owned match.");
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Missing private identity ledger.");
                var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
                var row=ledger.Units.SingleOrDefault(x=>x.Alive&&x.NativeId==id);
                Require(row is not null&&row.Independent&&row.Appearance is not null,"This unit has no disguise.");
                object?[] args={id,null};
                Require((bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,args)!&&args[1] is not null,"Unit is absent.");
                object unit=args[1]!,at=Native.Get(unit,"coordinates")!;
                var reference=new NativeExchangeUnit(row!.LogicalId,id,Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")));
                var offered=new NativeExchangeIntent(ledger.Epoch,ledger.NextSequence,snapshot.Revision,Convert.ToUInt32(Native.Get(state,"CurrentTurn")),
                    Convert.ToByte(Native.Get(unit,"owner")),NativeExchangeKind.ShedDisguise,false,NativePartnerReference.Existing,ledger.NextLogicalId,
                    reference,reference,NativeExchangeIntentCodec.AppearanceFingerprint(row.Appearance!));
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
                Require(intent.Kind==NativeExchangeKind.ShedDisguise&&Native.T("GameState").IsInstanceOfType(state),"Invalid shed request.");
                var request=NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(intent));
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Missing private identity ledger.");
                byte[] original=snapshot.Bytes();long revision=snapshot.Revision;
                var ledger=NativeIdentityLedgerCodec.Decode(original,revision);
                if(NativeExchangeIntentCodec.AlreadyCommitted(ledger,revision,request))
                {plan=new(state,request,true,revision,revision,original,original);return true;}
                NativeLedgerState.Validate(state,revision,original);
                Require(NativeRegroupSession.Ready(state)&&RecruitmentHooks.Registered,"Shed Disguise session is unavailable.");
                Require(Equals(Native.Get(state,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started"))&&
                    Native.Int(Native.Get(state,"CurrentPlayer"))==request.Actor&&Convert.ToUInt32(Native.Get(state,"CurrentTurn"))==request.Turn,"The shed turn or owner changed.");
                var row=ledger.Units.SingleOrDefault(x=>x.LogicalId==request.Initiator.LogicalId);
                Require(row is not null&&row.Alive&&row.NativeId==request.Initiator.NativeId&&row.Independent&&row.Appearance is not null,"The disguised identity changed.");
                Require(!(row!.Kind==LedgerActualKind.Mimic&&Exposed(row.Appearance)),"This Mimic is already exposed.");
                Require(NativeExchangeIntentCodec.AppearanceFingerprint(row.Appearance!).SequenceEqual(request.SourceAppearanceHash),"The disguise changed.");
                var graph=NativeLedgerState.Graph(state);
                Require(graph.TryGetValue(row.NativeId,out var body)&&body.Root,"Only an independent map root can shed.");
                object unit=body!.Unit,at=Native.Get(unit,"coordinates")!;
                Require(Native.Int(Native.Get(unit,"owner"))==request.Actor&&Native.Int(Native.Get(unit,"health"))>0&&
                    !(bool)Native.Invoke(Native.M("UnitState","HasLeader"),unit)!&&!(bool)Native.Invoke(Native.M("UnitState","HasFollower"),unit)!,"The unit is foreign, dead, or linked.");
                Require(Native.Int(Native.Get(at,"X"))==request.Initiator.X&&Native.Int(Native.Get(at,"Y"))==request.Initiator.Y,"The unit moved.");
                var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
                Require(tile is not null&&Native.Same(Native.Get(tile,"unit"),unit)&&
                    (bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,request.Actor)!,"The exact unit tile is inaccessible.");
                uint counter=Convert.ToUInt32(Native.Get(state,"CurrentUnitId")),wrapper=0;
                string type=Native.EnumName(Native.Get(unit,"type")!);LedgerUnit next;
                if(row.Kind==LedgerActualKind.Mimic)
                {
                    // This public form keeps the existing identity schema and
                    // the actual one-HP carrier. A water endpoint wraps it in a
                    // plain Raft; its cargo remains the exposed Mimic.
                    // Shedding reveals the real body's cosmetics, not the old
                    // costume or a temporary Rogue commander's team colors.
                    var naked=NativeLedgerState.ExposedAppearance(state,unit,row);
                    // Flood is dual use. Preserve an existing ordinary boat
                    // while removing its mask; an amphibious Mimic instead
                    // restores its original cargo body even on open water.
                    if(NativeMimicTransport.ExposedBoat(tile!,row)&&NativeMimicTransport.Wetland(tile!))
                        naked=NativeMimicTransport.EmbarkAppearance(state,naked);
                    var outcome=NativeExchangePlanning.FinalOutcome(state,unit,tile!,row with{Appearance=naked},ref counter);
                    next=outcome.Row;type=outcome.Type;wrapper=outcome.Wrapper;
                }
                else next=row with{Independent=false,Appearance=null};
                long after=checked(revision+1);
                var receipt=new LedgerReceipt(request.Sequence,NativeExchangeIntentCodec.Operation(request.Sequence),NativeExchangeIntentCodec.Fingerprint(request),revision,after,new[]{row.LogicalId});
                var candidate=ledger with{NextLogicalId=Math.Max(ledger.NextLogicalId,(ulong)counter+2),NextSequence=checked(ledger.NextSequence+1),Units=ledger.Units.Select(x=>x.LogicalId==row.LogicalId?next:x).ToArray(),Receipts=ledger.Receipts.Concat(new[]{receipt}).ToArray()};
                byte[] payload=NativeIdentityLedgerCodec.Encode(candidate,after);
                plan=new(state,request,false,revision,after,original,payload,a:unit,tileA:tile,carrierA:row.Kind==LedgerActualKind.Mimic?type:null,
                    typeA:type,idA:next.NativeId,wrapperA:wrapper,counter:counter);return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason,bool exhaustTransport=false)
        {
            // Caller owns exact live-action/detached-trial authorization and the
            // shared reentry guard. Stage all native conversion before publish.
            result=null;reason="";NativeExchangePlan? plan=null;NativeMimicTransport.Pending? coast=null;
            object? type=null,data=null,effects=null,tileUnit=null,moved=null,attacked=null;uint counter=0;bool changed=false;
            try
            {
                Require(TryPlan(state,intent,out plan,out var why),why);
                if(plan!.AlreadyCommitted){result=new(true,plan.BeforeRevision,plan.AfterRevision,plan);return true;}
                object unit=plan.A!;type=Native.Get(unit,"type");data=Native.Get(unit,"UnitData");effects=Native.Get(unit,"effects");tileUnit=Native.Get(plan.TileA!,"unit");
                counter=Convert.ToUInt32(Native.Get(state,"CurrentUnitId"));moved=Native.Get(unit,"moved");attacked=Native.Get(unit,"attacked");
                var next=plan.CandidateLedger.Units.Single(x=>x.LogicalId==intent.Initiator.LogicalId);
                if(plan.FinalMimicCarrierA is not null&&(NativeMimicTransport.IsVessel(unit)||NativeMimicTransport.IsVesselData(RecruitmentHooks.Data(state,plan.FinalTypeA!))))
                    coast=NativeMimicTransport.StageConversion(state,unit,plan.TileA!,plan.FinalTypeA!,plan.AllocatedWrapperA,exhaustTransport,
                        next.Appearance?.Passenger is not null?NativeMimicTransport.ExposedForm(next):null);
                Require(TryPlan(state,intent,out var fresh,out why)&&fresh is not null&&!fresh.AlreadyCommitted&&Native.Same(fresh.A,unit)&&
                    fresh.OriginalPayload.SequenceEqual(plan.OriginalPayload)&&fresh.CandidatePayload.SequenceEqual(plan.CandidatePayload),"Shed request changed during staging: "+why);
                Action? present=null;
                try{present=PreparePresentation?.Invoke(state,unit);}catch(Exception e){Entry.Note("shed-presentation-capture-error",e.Message);}
                changed=true;coast?.Apply();object final=coast?.FinalUnit??unit;plan.CommittedA=final;
                if(coast is null&&plan.FinalMimicCarrierA is string carrier)
                {Native.Set(final,"type",Native.EnumValue(RecruitmentHooks.UT,carrier));Native.Set(final,"UnitData",RecruitmentHooks.Data(state,carrier));}
                // An actual Raft-shaped fallback can land from a swimming
                // carrier without a physical wrapper. Its native shore action
                // still exhausts the Mimic; voluntary shedding remains free.
                if(exhaustTransport&&plan.FinalMimicCarrierA is not null)
                {Native.Set(final,"moved",true);Native.Set(final,"attacked",true);}
                Native.Set(final,"effects",NativeDisguiseConcealment.ActiveAbilityEffects(
                    NativeDisguiseConcealment.WithConcealment(Native.Get(final,"effects")!,next.Appearance,final),final));
                Native.Set(plan.TileA!,"unit",final);Native.Set(state,"CurrentUnitId",plan.FinalCounter);
                NativeLedgerState.Validate(state,plan.AfterRevision,plan.CandidatePayload);
                NativeGameStateEnvelope.Set(state,plan.AfterRevision,plan.CandidatePayload);
                coast?.PrepareAcceptance();coast?.Accept();result=new(false,plan.BeforeRevision,plan.AfterRevision,plan);
                try{present?.Invoke();}catch(Exception e){Entry.Note("shed-presentation-complete-error",e.Message);}return true;
            }
            catch(Exception e)
            {
                if(changed&&plan?.A is object unit)
                {
                    try
                    {
                        coast?.Rollback();Native.Set(unit,"type",type);Native.Set(unit,"UnitData",data);Native.Set(unit,"effects",effects);Native.Set(unit,"moved",moved);Native.Set(unit,"attacked",attacked);
                        Native.Set(plan.TileA!,"unit",tileUnit);Native.Set(state,"CurrentUnitId",counter);plan.CommittedA=unit;
                        NativeGameStateEnvelope.Set(state,plan.BeforeRevision,plan.OriginalPayload);
                    }
                    catch(Exception rollback){NativeRegroupSession.Fault("Shed Disguise rollback failed: "+rollback.Message);reason=new AggregateException(e,rollback).Message;return false;}
                }
                reason=e.Message;return false;
            }
            finally{coast?.Dispose();}
        }
    }
}
