using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static partial class BlackmailTests
 {
  static NativeExchangeIntent Quote(GameState s,int expected,byte actor=1,int index=0)
  {
   var before=Snap(s);int money=P(s,actor).Currency;
   Check(NativeBlackmail.TryOffer(s,actor,Target(s,index),out var request,out int cost,out var why),why);
   Check(cost==expected,"offer reports the flat two-star price");
   Check(NativeBlackmail.TryPlan(s,request!,out var plan,out why)&&plan!.StarCost==expected,"execution plan binds the same price as the displayed quote");
   Check(ReferenceEquals(before,Snap(s))&&P(s,actor).Currency==money,"price queries never publish or debit");
   return request!;
  }
  static void EscalatingCostTests()
  {
   Test("BM-COST-001-every-layer-costs-two-exact-affordability",()=>{
    var s=Make();
    for(int level=0;level<6;level++)
    {
     const int cost=2;s.CurrentTurn=(uint)(4+level);P(s,1).Currency=cost-1;var before=Snap(s);
     Check(!NativeBlackmail.TryOffer(s,1,Target(s),out _,out int _,out _)&&ReferenceEquals(before,Snap(s))&&P(s,1).Currency==cost-1,"one star cannot buy any layer");
     P(s,1).Currency=cost;var request=Quote(s,cost);
     Check(Commit(s,request,out var committed,out var why),why);DrainEmbassies(s);
     Check(committed!.Plan.StarCost==cost&&P(s,1).Currency==0&&Read(s).Blackmails!.Single().Layers==level+1,"each exact-price purchase adds one layer and debits exactly two");
     before=Snap(s);P(s,1).Currency=20;
     Check(!NativeBlackmail.TryOffer(s,1,Target(s),out _,out _,out _)&&ReferenceEquals(before,Snap(s)),"ample currency does not bypass the one-purchase-per-turn limit");
    }
   });
   Test("BM-COST-002-flat-price-across-actors-and-logical-units",()=>{
    var s=Make();Buy(s);s.CurrentTurn++;Buy(s);s.CurrentTurn++;
    Quote(s,2);Quote(s,2,index:1);
    s.CurrentPlayer=3;var request=Quote(s,2,actor:3);Check(Commit(s,request,out _,out var why),why);DrainEmbassies(s);
    Check(P(s,1).Currency==16&&P(s,3).Currency==18&&Read(s).Blackmails!.Sum(r=>r.Layers)==3,"another player's first level costs two despite an existing two-layer contract on the same unit");
    s.CurrentPlayer=1;Quote(s,2);
   });
   Test("BM-COST-003-paused-contract-level-survives-income-loss",()=>{
    var s=Make();Buy(s);s.CurrentTurn++;Buy(s);s.CurrentTurn++;
    City(s).Income=1;Check(NativeBlackmail.All(s).Single().Layers==1&&NativeBlackmail.Contracts(s).Single().Layers==2,"effective payment and contractual level differ");
    Check(!NativeBlackmail.TryOffer(s,1,Target(s),out _,out _,out _),"no purchase while the shared city cap is exhausted");
    City(s).Income=0;Check(NativeBlackmail.All(s).Length==0&&NativeBlackmail.Contracts(s).Single().Layers==2,"zero output pauses both levels without erasing them");
    City(s).Income=6;var request=Quote(s,2);Check(Commit(s,request,out _,out var why),why);DrainEmbassies(s);
    Check(P(s,1).Currency==14&&Read(s).Blackmails!.Single().Layers==3,"restored income permits the next two-star purchase without resetting stacked income");
   });
   Test("BM-COST-004-saved-existing-levels-keep-flat-price",()=>{
    var old=Make();Rows(old,new LedgerBlackmail(1,2,2,3,2,UnitId:1,Layers:4,LastAppliedTurn:3));var saved=Snap(old);
    var s=Make();NativeGameStateEnvelope.Set(s,saved.Revision,saved.Bytes());P(s,1).Currency=2;
    var request=Quote(s,2);Check(Commit(s,request,out _,out var why),why);DrainEmbassies(s);
    Check(Read(s).Blackmails!.Single().Layers==5&&P(s,1).Currency==0,"saved four-layer contracts add one layer for exactly two without resetting or charging old purchases");
    var purchased=Snap(s);var loaded=Make();NativeGameStateEnvelope.Set(loaded,purchased.Revision,purchased.Bytes());P(loaded,1).Currency=0;var loadedBefore=Snap(loaded);
    Check(Commit(loaded,request,out var replay,out why)&&replay!.AlreadyCommitted&&P(loaded,1).Currency==0&&ReferenceEquals(loadedBefore,Snap(loaded)),"durable retry is free even with no money for the next level");
    loaded.CurrentTurn++;P(loaded,1).Currency=2;var next=Quote(loaded,2);Check(Commit(loaded,next,out _,out why),why);DrainEmbassies(loaded);
    Check(Read(loaded).Blackmails!.Single().Layers==6&&P(loaded,1).Currency==0,"reloaded fifth layer still buys its sixth for exactly two");
   });
   Test("BM-COST-005-stale-layer-quote-rejected-despite-flat-price",()=>{
    var s=Make();Buy(s);s.CurrentTurn++;var request=Quote(s,2);
    var ledger=Read(s);Write(s,ledger with{Blackmails=ledger.Blackmails!.Select(r=>r with{Layers=2}).ToArray()});var before=Snap(s);
    Check(!Commit(s,request,out _,out _)&&P(s,1).Currency==18&&ReferenceEquals(before,Snap(s)),"same-revision layer change invalidates the old exact ledger quote");
    request=Quote(s,2);Check(Commit(s,request,out _,out var why),why);DrainEmbassies(s);
    Check(P(s,1).Currency==16&&Read(s).Blackmails!.Single().Layers==3,"fresh quote charges two while preserving the updated level");
   });
   foreach(string failure in new[]{"currency1","ledger","validate"})Test("BM-COST-006-flat-price-rollback-"+failure,()=>{
    var s=Make();Buy(s);s.CurrentTurn++;var request=Quote(s,2);var before=Snap(s);Fault.At=failure;
    Check(!Commit(s,request,out _,out _)&&P(s,1).Currency==18&&Snap(s).Revision==before.Revision&&Snap(s).Bytes().SequenceEqual(before.Bytes()),"failed repeat purchase restores currency and exact one-layer ledger");
    Check(Commit(s,request,out _,out var why),why);DrainEmbassies(s);
    Check(P(s,1).Currency==16&&Read(s).Blackmails!.Single().Layers==2,"retry after rollback charges exactly two once");
    var committed=Snap(s);s.CurrentTurn++;
    Check(Commit(s,request,out var replay,out _)&&replay!.AlreadyCommitted&&P(s,1).Currency==16&&ReferenceEquals(committed,Snap(s)),"replay on a later turn neither charges nor raises the level again");
   });
   Test("BM-COST-007-transport-preserves-layers-and-flat-price",()=>{
    var s=Make();Buy(s);var land=Target(s);var boat=new UnitState{id=201,owner=land.owner,coordinates=land.coordinates,home=new(31,31),passengerUnit=land};City(s).unit=boat;
    var ledger=Read(s);Write(s,ledger with{Units=ledger.Units.Select(r=>r.LogicalId==1?r with{NativeId=201,Transport=new LedgerTransport(101)}:r).ToArray()});s.CurrentTurn++;
    var request=Quote(s,2);Check(Commit(s,request,out _,out var why),why);DrainEmbassies(s);
    Check(Read(s).Blackmails!.Single().UnitId==1&&Read(s).Blackmails!.Single().Layers==2&&P(s,1).Currency==16,"new native boat body preserves existing logical layers and still charges two");
   });
   Test("BM-COST-008-legacy-city-lien-does-not-inflate-unit-price",()=>{
    var s=Make();Write(s,Read(s) with{Blackmails=new[]{new LedgerBlackmail(1,2,2,3,2)}});
    var request=Quote(s,2);Check(Commit(s,request,out _,out var why),why);DrainEmbassies(s);
    Check(P(s,1).Currency==18&&Read(s).Blackmails!.Single().UnitId==1&&Read(s).Blackmails!.Single().Layers==1,"inactive legacy city-only Blackmail is replaced by a new two-star unit contract");
   });
  }
 }
}
