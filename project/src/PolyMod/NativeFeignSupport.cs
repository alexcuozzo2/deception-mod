using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // One persisted Build intent carries the complete area operation. No fake
    // Heal/BoostAction is executed, and no live body is temporarily overwritten.
    internal static class NativeFeignSupport
    {
        internal static readonly NativeExchangeKind[] Kinds={NativeExchangeKind.FeignHeal,NativeExchangeKind.FeignBoost,NativeExchangeKind.FeignSwarm,NativeExchangeKind.FeignRecover};
        internal static bool IsKind(NativeExchangeKind kind)=>Kinds.Contains(kind);
        internal static string Caption(NativeExchangeKind kind)=>kind switch{NativeExchangeKind.FeignHeal=>"Feign Heal",NativeExchangeKind.FeignBoost=>"Feign Boost",NativeExchangeKind.FeignSwarm=>"Feign Swarm",NativeExchangeKind.FeignRecover=>"Feign Recover",_=>throw new InvalidDataException("Unknown support feint.")};
        internal static void Install(Harmony harmony)=>NativeFeignSupportEffects.Install(harmony);
        internal static void Clear()=>NativeFeignSupportEffects.Clear();
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException(why);}
        static bool Ability(object data,int value)=>Native.Items(Native.Get(data,"unitAbilities")!).Any(x=>Native.Int(x)==value);
        internal static bool Has(LedgerAppearance a,string effect)=>a.Effects.Contains(effect,StringComparer.Ordinal);
        // Actual Mimics have Dash; an ordinary disguised body retains its
        // existing support timing. Do not query CanAttack here: Mimics and
        // genuine Spies have a separate no-damaging-attacks guard.
        internal static bool Ready(object unit,bool allowMimicDash=false)=>((allowMimicDash&&RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(unit,"type")!)))||
            !(bool)Native.Get(unit,"moved")!)&&!(bool)Native.Get(unit,"attacked")!&&
            !Native.Items(Native.Get(unit,"effects")!).Any(e=>Native.Int(e)==0);
        internal static LedgerAppearance Heal(LedgerAppearance a,int amount)
        {
            // Native123 cleansing requires Poisoned, then removes Poisoned and
            // Charmed instead of restoring HP. Charmed alone is not cleansed.
            // Neither ordinary Heal nor Recover removes Frozen in this build.
            bool cleanse=Has(a,"Poisoned");
            var next=a with{Health=cleanse?a.Health:(ushort)Math.Min(a.Maximum,a.Health+amount),
                Effects=cleanse?a.Effects.Where(x=>x is not ("Poisoned" or "Charmed")).ToArray():a.Effects};
            if(cleanse&&a.Passenger is LedgerAppearance cargo)
                next=next with{Passenger=cargo with{Effects=cargo.Effects.Where(x=>x is not ("Poisoned" or "Charmed")).ToArray()}};
            return next;
        }
        internal static LedgerAppearance Effect(LedgerAppearance a,string effect,bool add)=>a with{Effects=add?
            a.Effects.Append(effect).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray():a.Effects.Where(x=>x!=effect).ToArray()};
        internal static bool Recipient(object state,object unit,LedgerUnit? row,byte actor)=>row is not null&&row.Alive&&row.Rogue is null&&
            Native.Int(Native.Get(unit,"health"))>0&&NativeRogueRules.RememberedOwner(state,unit)==actor&&Native.Int(Native.Get(unit,"owner"))==actor;
        internal static LedgerAppearance Appearance(object state,NativeLedgerState.Body body,LedgerUnit row)=>row.Appearance??
            NativeLedgerState.Capture(state,body,Native.EnumName(Native.Get(body.Unit,"type")!),false);
        internal static object Tile(object state,object unit)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,
            Native.Get(state,"Map"),Native.Get(unit,"coordinates"))??throw new InvalidDataException("Support unit tile is absent.");
        internal static int RecoveryAmount(object state,object tile,object unit,LedgerAppearance a)
        {
            // Native territory/peace multiplier and maximum are evaluated on
            // a detached apparent profile, never a temporarily altered unit.
            object Profile(object source,LedgerAppearance appearance)
            {
                object copy=Native.DetachedClone(source,"UnitState"),data=Native.DetachedClone(RecruitmentHooks.Data(state,appearance.Form),"Polytopia.Data.UnitData");
                Native.Set(data,"health",appearance.Maximum);Native.Set(copy,"UnitData",data);
                Native.Set(copy,"type",Native.EnumValue(RecruitmentHooks.UT,appearance.Form));Native.Set(copy,"health",appearance.Health);Native.Set(copy,"promotionLevel",(ushort)0);
                if(appearance.Passenger is LedgerAppearance passenger)Native.Set(copy,"passengerUnit",Profile(Native.Get(source,"passengerUnit")??source,passenger));
                else Native.Set(copy,"passengerUnit",null);
                return copy;
            }
            object view=Native.DetachedClone(tile,"TileData");Native.Set(view,"unit",Profile(unit,a));
            return Native.Int(Native.Invoke(Native.M("ActionUtils","GetHealAmount","GameState","TileData"),null,state,view));
        }
        internal static bool TryOffer(object state,uint id,NativeExchangeKind kind,out NativeExchangeIntent? intent,out string reason)
        {
            intent=null;reason="";
            try
            {
                Require(kind!=NativeExchangeKind.FeignRecover,"Feign Recover is no longer available.");
                Require(IsKind(kind)&&NativeRegroupSession.Ready(state),"Support feints need an active match.");
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Support ledger is absent.");
                byte[] bytes=snapshot.Bytes();var ledger=NativeIdentityLedgerCodec.Decode(bytes,snapshot.Revision);
                var row=ledger.Units.SingleOrDefault(x=>x.Alive&&x.NativeId==id);
                Require(row?.Appearance is not null,"Only an independent appearance can feign support.");
                object?[] args={id,null};Require((bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,args)!&&args[1] is not null,"Support caster is absent.");
                object unit=args[1]!,at=Native.Get(unit,"coordinates")!;
                var reference=new NativeExchangeUnit(row!.LogicalId,id,Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")));
                var request=new NativeExchangeIntent(ledger.Epoch,ledger.NextSequence,snapshot.Revision,Convert.ToUInt32(Native.Get(state,"CurrentTurn")),
                    Convert.ToByte(Native.Get(state,"CurrentPlayer")),kind,false,NativePartnerReference.Existing,ledger.NextLogicalId,reference,reference,SHA256.HashData(bytes));
                if(!TryPlan(state,request,out _,out reason))return false;intent=request;return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason)
        {
            plan=null;reason="";
            try
            {
                Require(IsKind(intent.Kind)&&Native.T("GameState").IsInstanceOfType(state),"Unknown support feint.");
                var request=NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(intent));
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Support ledger is absent.");
                byte[] original=snapshot.Bytes();long revision=snapshot.Revision;var ledger=NativeIdentityLedgerCodec.Decode(original,revision);
                if(NativeExchangeIntentCodec.AlreadyCommitted(ledger,revision,request)){plan=new(state,request,true,revision,revision,original,original);return true;}
                // Retain the historical kind/receipt codec for old saves and
                // idempotent acknowledgments, but never execute a new Recover.
                Require(request.Kind!=NativeExchangeKind.FeignRecover,"Feign Recover is no longer available.");
                NativeLedgerState.Validate(state,revision,original);
                Require(NativeRegroupSession.Ready(state)&&RecruitmentHooks.Registered&&
                    Equals(Native.Get(state,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started"))&&
                    Native.Int(Native.Get(state,"CurrentPlayer"))==request.Actor&&Convert.ToUInt32(Native.Get(state,"CurrentTurn"))==request.Turn,"Support turn or commander changed.");
                Require(SHA256.HashData(original).SequenceEqual(request.SourceAppearanceHash),"A support recipient or appearance changed.");
                var row=ledger.Units.SingleOrDefault(x=>x.LogicalId==request.Initiator.LogicalId);
                Require(row is not null&&row.Alive&&row.NativeId==request.Initiator.NativeId&&row.Independent&&row.Appearance is not null,"Support source identity changed.");
                var graph=NativeLedgerState.Graph(state);Require(graph.TryGetValue(row!.NativeId,out var body)&&body.Root,"Support caster is not a map root.");
                object unit=body!.Unit,at=Native.Get(unit,"coordinates")!;var a=row.Appearance!;
                Require(NativeRogueRules.CanCommand(state,unit,request.Actor)&&Native.Int(Native.Get(unit,"health"))>0&&
                    !(bool)Native.Invoke(Native.M("UnitState","HasLeader"),unit)!&&!(bool)Native.Invoke(Native.M("UnitState","HasFollower"),unit)!,"Support caster is foreign, dead or linked.");
                Require(Native.Int(Native.Get(at,"X"))==request.Initiator.X&&Native.Int(Native.Get(at,"Y"))==request.Initiator.Y,"Support caster moved.");
                object tile=Tile(state,unit);Require(Native.Same(Native.Get(tile,"unit"),unit)&&(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,request.Actor)!,"Support caster tile is inaccessible.");
                Require(Ready(unit,allowMimicDash:true),"Support needs the body's unspent native action.");
                Require(!Has(a,"Frozen"),"A frozen appearance cannot perform support.");
                object data=RecruitmentHooks.Data(state,a.Form);
                bool heal=request.Kind==NativeExchangeKind.FeignHeal,recover=request.Kind==NativeExchangeKind.FeignRecover;
                Require(recover||Ability(data,heal?9:request.Kind==NativeExchangeKind.FeignBoost?24:53),"The current appearance has no represented support ability.");
                var rows=ledger.Units.ToDictionary(x=>x.LogicalId);int changed=0;
                IEnumerable<object> candidates=recover?new[]{tile}:Native.Items(Native.Invoke(Native.M("MapDataExtensions","GetArea","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),null,Native.Get(state,"Map"),at,1,true,false)!);
                foreach(object targetTile in candidates)
                {
                    if(Native.Get(targetTile,"unit") is not object target)continue;
                    uint targetId=Convert.ToUInt32(Native.Get(target,"id"));
                    var recipient=ledger.Units.SingleOrDefault(x=>x.Alive&&x.NativeId==targetId);
                    if(!Recipient(state,target,recipient,request.Actor)||!graph.TryGetValue(targetId,out var targetBody)||!targetBody.Root||
                        !Native.Same(targetBody.Unit,target)||(bool)Native.Invoke(Native.M("UnitState","HasLeader"),target)!||(bool)Native.Invoke(Native.M("UnitState","HasFollower"),target)!)continue;
                    LedgerAppearance appearance=Appearance(state,targetBody,recipient!),next;
                    if(heal||recover)
                    {
                        if(appearance.Health>=appearance.Maximum&&!Has(appearance,"Poisoned"))continue;
                        next=Heal(appearance,recover?RecoveryAmount(state,targetTile,target,appearance):40);
                    }
                    else
                    {
                        if(Has(appearance,"Boosted")||Has(appearance,"Swift"))continue;
                        next=Effect(appearance,"Swift",true);
                    }
                    rows[recipient!.LogicalId]=recipient with{Independent=true,Appearance=next};changed++;
                }
                Require(changed>0,"No eligible apparent support recipients.");
                // A genuine Spy exposes itself when using its disguise's active
                // support ability too. A failed offer must not reveal the body.
                var source=rows[row.LogicalId];
                rows[row.LogicalId]=source with{Appearance=NativeDisguiseConcealment.ActiveAbilityAppearance(source.Appearance!,unit)};
                long after=checked(revision+1);var receipt=new LedgerReceipt(request.Sequence,NativeExchangeIntentCodec.Operation(request.Sequence),NativeExchangeIntentCodec.Fingerprint(request),revision,after,new[]{row.LogicalId});
                var candidate=ledger with{NextSequence=checked(ledger.NextSequence+1),Units=rows.Values.OrderBy(x=>x.LogicalId).ToArray(),Receipts=ledger.Receipts.Concat(new[]{receipt}).ToArray()};
                byte[] payload=NativeIdentityLedgerCodec.Encode(candidate,after);
                // An offered command must already have a valid full apparent
                // profile, including newly captured same-type recipient masks.
                // Reject before queueing or spending the physical allowance.
                NativeLedgerState.Validate(state,after,payload);
                plan=new(state,request,false,revision,after,original,payload,a:unit,tileA:tile);return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason)
        {
            result=null;reason="";NativeExchangePlan? plan=null;bool changed=false;object? moved=null,attacked=null,effects=null;
            try
            {
                // Called only inside NativeExchangeCommit's live/trial guard.
                Require(TryPlan(state,intent,out plan,out var why),why);
                if(plan!.AlreadyCommitted){result=new(true,plan.BeforeRevision,plan.AfterRevision,plan);return true;}
                object caster=plan.A!;moved=Native.Get(caster,"moved");attacked=Native.Get(caster,"attacked");effects=Native.Get(caster,"effects")!;changed=true;
                Native.Set(caster,"effects",NativeDisguiseConcealment.ActiveAbilityEffects(effects,caster));
                // Native HealOthers/Boost/Swarm/Recover all set these two flags;
                // they do not grant another budget or remove real status lists.
                Native.Set(caster,"moved",true);Native.Set(caster,"attacked",true);
                NativeLedgerState.Validate(state,plan.AfterRevision,plan.CandidatePayload);NativeGameStateEnvelope.Set(state,plan.AfterRevision,plan.CandidatePayload);
                NativeFeignSupportReaction.Record(plan);result=new(false,plan.BeforeRevision,plan.AfterRevision,plan);return true;
            }
            catch(Exception e)
            {
                if(changed&&plan?.A is object unit)try{Native.Set(unit,"moved",moved);Native.Set(unit,"attacked",attacked);Native.Set(unit,"effects",effects);NativeGameStateEnvelope.Set(state,plan.BeforeRevision,plan.OriginalPayload);}
                    catch(Exception rollback){NativeRegroupSession.Fault("Support rollback failed: "+rollback.Message);reason=new AggregateException(e,rollback).Message;return false;}
                reason=e.Message;return false;
            }
        }
    }
}
