using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    internal static class NativeSubvert
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
        static NativeExchangeUnit Reference(object unit,ulong logical)
        {var at=Native.Get(unit,"coordinates")!;return new(logical,Convert.ToUInt32(Native.Get(unit,"id")),Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")));}
        internal static bool TryOffer(object state,uint source,uint target,out NativeExchangeIntent? intent,out string reason)
        {
            intent=null;reason="";
            try
            {
                Require(NativeRegroupSession.Ready(state),"No active match.");
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Missing identity ledger.");
                var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);var graph=NativeLedgerState.Graph(state);
                var a=ledger.Units.SingleOrDefault(x=>x.Alive&&x.NativeId==source);var b=ledger.Units.SingleOrDefault(x=>x.Alive&&x.NativeId==target);
                Require(a is not null&&b is not null&&graph.TryGetValue(source,out _)&&graph.TryGetValue(target,out _),"The selected units changed.");
                var request=new NativeExchangeIntent(ledger.Epoch,ledger.NextSequence,snapshot.Revision,Convert.ToUInt32(Native.Get(state,"CurrentTurn")),
                    Convert.ToByte(Native.Get(state,"CurrentPlayer")),NativeExchangeKind.Subvert,false,NativePartnerReference.Existing,ledger.NextLogicalId,
                    Reference(graph[source].Unit,a!.LogicalId),Reference(graph[target].Unit,b!.LogicalId),SHA256.HashData(snapshot.Bytes()));
                if(!TryPlan(state,request,out _,out reason))return false;intent=request;return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason)
        {
            plan=null;reason="";
            try
            {
                Require(intent.Kind==NativeExchangeKind.Subvert&&!intent.Swap,"Invalid Subvert request.");
                var request=NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(intent));
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Missing identity ledger.");
                byte[] original=snapshot.Bytes();var ledger=NativeIdentityLedgerCodec.Decode(original,snapshot.Revision);
                if(NativeExchangeIntentCodec.AlreadyCommitted(ledger,snapshot.Revision,request))
                {plan=new(state,request,true,snapshot.Revision,snapshot.Revision,original,original);return true;}
                Require(NativeRegroupSession.Ready(state)&&RecruitmentHooks.Registered&&Equals(Native.Get(state,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started")),"No active Subvert match.");
                Require(SHA256.HashData(original).SequenceEqual(request.SourceAppearanceHash)&&Convert.ToUInt32(Native.Get(state,"CurrentTurn"))==request.Turn&&Native.Int(Native.Get(state,"CurrentPlayer"))==request.Actor,"Subvert state changed.");
                NativeLedgerState.Validate(state,snapshot.Revision,original);
                var graph=NativeLedgerState.Graph(state);
                LedgerUnit Resolve(NativeExchangeUnit r)
                {
                    var row=ledger.Units.SingleOrDefault(x=>x.Alive&&x.LogicalId==r.LogicalId&&x.NativeId==r.NativeId);
                    Require(row is not null&&graph.TryGetValue(r.NativeId,out var body)&&body.Root,"Subvert identity changed.");
                    Require(Reference(graph[r.NativeId].Unit,r.LogicalId)==r,"Subvert position changed.");return row!;
                }
                var a=Resolve(request.Initiator);var b=Resolve(request.Partner);object source=graph[a.NativeId].Unit,target=graph[b.NativeId].Unit;
                Require(NativeSpyNetwork.TryReachable(state,request.Actor,a.NativeId,out var tiles,out var why),why);
                var tile=tiles.SingleOrDefault(t=>Native.Same(Native.Get(t,"unit"),target));
                Require(tile is not null&&NativeSpyNetwork.Accessible(state,tile,request.Actor),"Target is outside the initiating Spy's accessible range.");
                var visible=Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),tile,state,request.Actor,false);
                Require(Native.Same(visible,target)&&Native.Int(Native.Get(target,"health"))>0,"Target is not detected.");
                byte permanent=b.Rogue?.Owner??Convert.ToByte(Native.Get(target,"owner"));
                Require(permanent!=request.Actor&&!NativeRogueRules.CanCommand(state,target,request.Actor),"Target is already owned or commanded.");
                Require(!(b.Acquisitions??Array.Empty<LedgerAcquisition>()).Any(x=>x.Player==request.Actor&&x.Turn==request.Turn),"This physical unit has already been acquired this turn.");
                object?[] pa={request.Actor,null};Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,pa)!&&pa[1] is not null,"Commander is absent.");
                Require(!(bool)Native.Invoke(Native.M("PlayerDiplomacyExtensions","HasPeaceWith","PlayerState","System.Byte"),null,pa[1],permanent)!,"Treaty partners cannot be Subverted.");
                // Native linked entities need one coherent ownership operation;
                // target only independently commandable root bodies.
                Require(!(bool)Native.Invoke(Native.M("UnitState","HasLeader"),target)!&&!(bool)Native.Invoke(Native.M("UnitState","HasFollower"),target)!,"A linked body cannot be acquired independently.");
                var history=(b.Acquisitions??Array.Empty<LedgerAcquisition>()).Where(x=>x.Player!=request.Actor).Append(new LedgerAcquisition(request.Actor,request.Turn)).OrderBy(x=>x.Player).ToArray();
                var nextA=a with{Independent=false,Appearance=null};
                var nextB=b with{Rogue=new(permanent,request.Actor,request.Turn),Acquisitions=history};
                long after=checked(snapshot.Revision+1);
                var receipt=new LedgerReceipt(request.Sequence,NativeExchangeIntentCodec.Operation(request.Sequence),NativeExchangeIntentCodec.Fingerprint(request),snapshot.Revision,after,new[]{a.LogicalId,b.LogicalId}.OrderBy(x=>x).ToArray());
                var candidate=ledger with{NextSequence=checked(ledger.NextSequence+1),Units=ledger.Units.Select(x=>x.LogicalId==a.LogicalId?nextA:x.LogicalId==b.LogicalId?nextB:x).ToArray(),Receipts=ledger.Receipts.Append(receipt).ToArray()};
                var origin=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),Native.Get(source,"coordinates"));
                plan=new(state,request,false,snapshot.Revision,after,original,NativeIdentityLedgerCodec.Encode(candidate,after),a:source,b:target,tileA:origin,tileB:tile);return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason)
        {
            result=null;reason="";NativeExchangePlan? plan=null;
            object? effects=null,stack=null,cargo=null;bool am=false,aa=false,bm=false,ba=false,cm=false,ca=false,dirty=false,changed=false;byte owner=0;
            try
            {
                Require(TryPlan(state,intent,out plan,out var why),why);
                if(plan!.AlreadyCommitted){result=new(true,plan.BeforeRevision,plan.AfterRevision,plan);return true;}
                var a=plan.A!;var b=plan.B!;
                am=(bool)Native.Get(a,"moved")!;aa=(bool)Native.Get(a,"attacked")!;bm=(bool)Native.Get(b,"moved")!;ba=(bool)Native.Get(b,"attacked")!;
                owner=Convert.ToByte(Native.Get(b,"owner"));effects=Native.Get(a,"effects");stack=Native.Get(state,"ActionStack");
                cargo=Native.Get(b,"passengerUnit");if(cargo is not null){cm=(bool)Native.Get(cargo,"moved")!;ca=(bool)Native.Get(cargo,"attacked")!;}
                dirty=(bool)Native.Get(state,"HasFlaggedNeedForUpdateRoutes")!;
                var staged=Native.NewList("ActionBase",Native.Items(stack!).ToArray());
                Require(TryPlan(state,intent,out var fresh,out why)&&fresh is not null&&fresh.CandidatePayload.SequenceEqual(plan.CandidatePayload),"Subvert changed before commitment: "+why);
                changed=true;Native.Set(a,"moved",true);Native.Set(a,"attacked",true);
                Native.Set(a,"effects",NativeDisguiseConcealment.WithConcealment(effects!,null,null!));
                NativeRogueRules.SetOwner(b,intent.Actor);Native.Set(b,"moved",false);Native.Set(b,"attacked",false);
                if(cargo is not null){Native.Set(cargo,"moved",false);Native.Set(cargo,"attacked",false);}
                Native.Set(state,"ActionStack",staged);
                NativeLedgerState.Validate(state,plan.AfterRevision,plan.CandidatePayload);
                NativeGameStateEnvelope.Set(state,plan.AfterRevision,plan.CandidatePayload);
                // Acquiring control grants actual local sight; no apparent
                // Spy authentication or sight from other units is borrowed.
                object?[] args={intent.Actor,null};Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args);
                int sight=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetSightRange","Polytopia.Data.UnitData"),null,Native.Get(b,"UnitData")));
                var discovery=new NativeBlackoutDiscovery.Collection();var rogueSight=new NativeRogueSight.Collection();
                using(NativeRogueSight.ExchangeSource(state,intent.Actor,plan.TileB!,rogueSight))
                using(NativeBlackoutDiscovery.ExchangeSource(state,intent.Actor,plan.TileB!,discovery))
                    Native.Invoke(Native.M("ActionUtils","ExploreFromTile","GameState","PlayerState","TileData","System.Int32","System.Boolean"),null,state,args[1],plan.TileB,sight,true);
                plan.MergeDiscovery(rogueSight.Merge(discovery.Merge(plan.CandidateLedger)));
                NativeLedgerState.Validate(state,plan.AfterRevision,plan.CandidatePayload);
                NativeGameStateEnvelope.Set(state,plan.AfterRevision,plan.CandidatePayload);
                Native.Set(state,"HasFlaggedNeedForUpdateRoutes",true);
                result=new(false,plan.BeforeRevision,plan.AfterRevision,plan);return true;
            }
            catch(Exception e)
            {
                if(changed&&plan is not null)
                {
                    Native.Set(plan.A!,"moved",am);Native.Set(plan.A!,"attacked",aa);Native.Set(plan.A!,"effects",effects);
                    NativeRogueRules.SetOwner(plan.B!,owner);Native.Set(plan.B!,"moved",bm);Native.Set(plan.B!,"attacked",ba);
                    if(cargo is not null){Native.Set(cargo,"moved",cm);Native.Set(cargo,"attacked",ca);}
                    Native.Set(state,"HasFlaggedNeedForUpdateRoutes",dirty);
                    Native.Set(state,"ActionStack",stack);NativeGameStateEnvelope.Set(state,plan.BeforeRevision,plan.OriginalPayload);
                }
                reason=e.Message;return false;
            }
        }
    }
}
