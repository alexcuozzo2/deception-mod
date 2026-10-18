using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static partial class BlackoutTests
 {
  static void SubvertSightCases()
  {
   Test("subvert-production-sight-merge-saves-real-footprint",()=>{
    var s=SubvertBoard();var target=Tile(s,2,1).unit!;var effects=target.effects;
    var i=SubvertOffer(s);Check(NativeSubvert.TryCommit(s,i,out var result,out var why),why);
    var row=Read(s).Units.Single(u=>u.NativeId==9);var expected=MapDataExtensions.GetAreaSorted(s.Map,target.coordinates,2,true,true).Values.Select(t=>new LedgerSight(t.coordinates.X,t.coordinates.Y)).OrderBy(p=>p.X).ThenBy(p=>p.Y).ToArray();
    Check(row.Rogue!.Sight!.SequenceEqual(expected)&&row.Rogue.PendingSight is null,"actual completed local footprint retained through real plan merge");
    Check(ReferenceEquals(effects,target.effects)&&target.health==100,"Rogue is ledger state, no invented native effect or HP change");
    Check(result!.Plan.CandidatePayload.SequenceEqual(NativeGameStateEnvelope.Get(s)!.Bytes()),"committed sight is in exact framed receipt payload");
   });
   Test("subvert-pending-sight-completes-after-native-explore",()=>{
    var s=SubvertBoard();Tile(s,3,1).explorers=new();Write(s,Read(s) with{Blackouts=new[]{new LedgerBlackout(1,3,1)}});
    var i=SubvertOffer(s);Check(NativeSubvert.TryCommit(s,i,out _,out var why),why);
    var rogue=Read(s).Units.Single(u=>u.NativeId==9).Rogue!;var at=new LedgerSight(3,1);
    Check(rogue.PendingSight!.Contains(at)&&!rogue.Sight!.Contains(at)&&!Tile(s,3,1).GetExplored(1),"queued footprint is not prematurely revealed");
    Execute(s,Pending(s,1,3,1));rogue=Read(s).Units.Single(u=>u.NativeId==9).Rogue!;
    Check(rogue.Sight!.Contains(at)&&rogue.PendingSight is null,"actual native completion moves pending to retained sight");
    Check(Read(s).Discoveries!.Length==0&&Read(s).Blackouts!.Length==0,"completed entitled discovery removes only its mask/permit");
   });
   Test("sight-merge-keeps-original-plan-and-receipt-immutable",()=>{
    var s=SubvertBoard();var i=SubvertOffer(s);Check(NativeSubvert.TryPlan(s,i,out var plan,out var why),why);var before=plan!.OriginalPayload;
    var next=plan.CandidateLedger;next=next with{Units=next.Units.Select(r=>r.LogicalId==9?r with{Rogue=r.Rogue! with{Sight=new[]{new LedgerSight(2,1)},PendingSight=new[]{new LedgerSight(3,1)}}}:r).ToArray()};
    plan.MergeDiscovery(next);Check(plan.OriginalPayload.SequenceEqual(before)&&NativeExchangeIntentCodec.Encode(plan.Intent).SequenceEqual(NativeExchangeIntentCodec.Encode(i)),"before payload and intent unchanged");
    Check(plan.CandidateLedger.Units.Single(r=>r.LogicalId==9).Rogue!.Sight!.Single()==new LedgerSight(2,1),"only candidate receives authorized footprint");
    Check(Tile(s,2,1).unit!.owner==2&&!Tile(s,1,1).unit!.attacked,"plan-only merge makes no native changes");
   });
   foreach(string mutation in new[]{"owner","commander","turn","history","appearance","identity","kind","epoch","receipt","sequence","blackout","unrelated-sight"})
    Test("sight-merge-rejects-non-sight-"+mutation,()=>{
     var s=SubvertBoard();Write(s,Read(s) with{Units=Read(s).Units.Select(r=>r.LogicalId==2?r with{Rogue=new LedgerRogue(2,1,s.CurrentTurn)}:r).ToArray()});
     var i=SubvertOffer(s);Check(NativeSubvert.TryPlan(s,i,out var plan,out var why),why);var bytes=plan!.CandidatePayload;var next=plan.CandidateLedger;
     var r=next.Units.Single(r=>r.LogicalId==9);var changed=r with{Rogue=r.Rogue! with{Sight=new[]{new LedgerSight(2,1)}}};
     changed=mutation switch{
      "owner"=>changed with{Rogue=changed.Rogue! with{Owner=3}},"commander"=>changed with{Rogue=changed.Rogue! with{Commander=3}},
      "turn"=>changed with{Rogue=changed.Rogue! with{AcquiredTurn=s.CurrentTurn+1}},"history"=>changed with{Acquisitions=new[]{new LedgerAcquisition(1,s.CurrentTurn+1)}},
      "appearance"=>changed with{Independent=true,Appearance=Mask()},"identity"=>changed with{NativeId=40},"kind"=>changed with{Kind=LedgerActualKind.Mimic,Independent=true,Appearance=Mask()},_=>changed};
     next=next with{Units=next.Units.Select(x=>x.LogicalId==9?changed:mutation=="unrelated-sight"&&x.LogicalId==2?x with{Rogue=x.Rogue! with{Sight=new[]{new LedgerSight(3,1)}}}:x).ToArray()};
     next=mutation switch{"epoch"=>next with{Epoch=Guid.NewGuid()},"receipt"=>next with{Receipts=Array.Empty<LedgerReceipt>()},"sequence"=>next with{NextSequence=next.NextSequence+1},"blackout"=>next with{Blackouts=new[]{new LedgerBlackout(1,3,1)}},_=>next};
     bool rejected=false;try{plan.MergeDiscovery(next);}catch(System.IO.InvalidDataException){rejected=true;}
     Check(rejected,"unrelated immutable candidate state cannot piggyback on sight");Check(plan.CandidatePayload.SequenceEqual(bytes),"failed merge leaves candidate bytes exact");
    });
   foreach(string stage in new[]{"sight","validate-after-sight"})Test("subvert-staged-sight-rollback-"+stage,()=>{
    var s=SubvertBoard();Tile(s,3,1).explorers=new();var target=Tile(s,2,1).unit!;target.moved=target.attacked=true;var i=SubvertOffer(s);
    var snapshot=NativeGameStateEnvelope.Get(s)!.Bytes();var queue=s.ActionStack;Fault.At=stage;
    Check(!NativeSubvert.TryCommit(s,i,out _,out _),"injected discovery-stage failure");
    Check(target.owner==2&&target.moved&&target.attacked&&!Tile(s,1,1).unit!.attacked,"true ownership and both allowances rolled back");
    Check(NativeGameStateEnvelope.Get(s)!.Bytes().SequenceEqual(snapshot)&&ReferenceEquals(queue,s.ActionStack)&&!Tile(s,3,1).GetExplored(1),"queue, ledger and unrevealed fog unchanged");
    var retry=SubvertOffer(s);Check(NativeSubvert.TryCommit(s,retry,out _,out var why),why);
   });
  }
 }
}
