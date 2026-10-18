using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static partial class LighthouseSecretTests
 {
  static BuildAction Action(NativeExchangeIntent intent)=>new(){Intent=intent,PlayerId=intent.Actor,Type=NativeExchangeCatalog.Id(intent.Kind),Coordinates=new(intent.Initiator.X,intent.Initiator.Y)};
  static void Present(GameState state){GameManager.GameState=state;GameManager.Client=new(){GameState=state};}
  static void PresentationTests()
  {
   Test("LIGHTHOUSE-UI-001-committed-wallet-refreshed-with-one-completion",()=>{
    var s=Make();Present(s);ResourceEvents.Wallets[1]=20;ResourceEvents.Wallets[2]=20;var intent=SeedHistorical(s);var before=StateBytes(s);var done=new Il2CppSystem.Action();
    NativeLighthouseSecretPresentation.Complete(Action(intent),done);
    Check(done.Calls==1&&ResourceEvents.Refreshed.SequenceEqual(new byte[]{1})&&ResourceEvents.Wallets[1]==1020&&ResourceEvents.Wallets[2]==20,"completion must refresh committed actor wallet exactly once");
    Check(s.Actions==0&&s.Effects==0&&s.Sounds==0&&Entry.Notes.Count==0,"successful silent presentation uses no effect queue or error path");Unchanged(s,before,"presentation wrote authoritative reward twice");
   });
   Test("LIGHTHOUSE-UI-002-repeated-presentation-never-adds-currency",()=>{
    var s=Make();Present(s);var intent=SeedHistorical(s);var before=StateBytes(s);var first=new Il2CppSystem.Action();var duplicate=new Il2CppSystem.Action();
    NativeLighthouseSecretPresentation.Complete(Action(intent),first);NativeLighthouseSecretPresentation.Complete(Action(intent),duplicate);
    Check(first.Calls==1&&duplicate.Calls==1&&ResourceEvents.Wallets[1]==1020,"each native presentation completes once with the same committed wallet");Unchanged(s,before,"repeat animation caused extra reward");
   });
   foreach(string invalid in new[]{"no-intent","other-kind","no-state","no-client","foreign-client","unowned","disabled","missing-ledger","uncommitted","foreign-epoch","wrong-carrier-actor","wrong-carrier-type","wrong-carrier-position","altered-intent"})
   Test("LIGHTHOUSE-UI-003-stale-or-missing-context-"+invalid,()=>{
    var s=Make();Present(s);var request=invalid=="uncommitted"?HistoricalIntent(s):SeedHistorical(s);var action=Action(request);ResourceEvents.Wallets[1]=20;
    switch(invalid){case"no-intent":action.Intent=null;break;case"other-kind":action.Intent=request with{Kind=NativeExchangeKind.Blackmail};break;case"no-state":GameManager.GameState=null;break;case"no-client":GameManager.Client=null;break;case"foreign-client":GameManager.Client!.GameState=Make();break;case"unowned":NativeRegroupSession.Owned.Remove(s);break;case"disabled":NativeRegroupSession.Enabled=false;break;case"missing-ledger":NativeGameStateEnvelope.Values.Remove(s);break;case"uncommitted":break;case"foreign-epoch":Write(s,Ledger(s) with{Epoch=Guid.NewGuid()});break;case"wrong-carrier-actor":action.PlayerId=2;break;case"wrong-carrier-type":action.Type="unrelated";break;case"wrong-carrier-position":action.Coordinates=new(11,7);break;case"altered-intent":action.Intent=request with{SourceAppearanceHash=new byte[32]};break;}
    var before=NativeGameStateEnvelope.Get(s);int money=Player(s).Currency;var done=new Il2CppSystem.Action();NativeLighthouseSecretPresentation.Complete(action,done);
    Check(done.Calls==1&&ResourceEvents.Refreshed.Count==0&&ResourceEvents.Wallets[1]==20,"stale presentation must complete without refreshing another wallet");
    Check(ReferenceEquals(before,NativeGameStateEnvelope.Get(s))&&Player(s).Currency==money,"stale presentation altered persistent reward state");
   });
   foreach(string failure in new[]{"wallet","attachment"})Test("LIGHTHOUSE-UI-004-refresh-failure-still-completes-"+failure,()=>{
    var s=Make();Present(s);var request=SeedHistorical(s);var before=StateBytes(s);var done=new Il2CppSystem.Action();Fault.At=failure;
    NativeLighthouseSecretPresentation.Complete(Action(request),done);Fault.Reset();
    Check(done.Calls==1&&Entry.Notes.Count==1&&Entry.Notes[0].StartsWith("lighthouse-wallet-refresh-error:"),"refresh failure must be contained and complete animation exactly once");Unchanged(s,before,"UI failure rolled back or repeated already committed reward");
   });
   Test("LIGHTHOUSE-UI-005-completion-exception-is-not-retried",()=>{
    var s=Make();Present(s);var request=SeedHistorical(s);var before=StateBytes(s);var done=new Il2CppSystem.Action();Fault.At="completion";bool threw=false;
    try{NativeLighthouseSecretPresentation.Complete(Action(request),done);}catch(System.IO.IOException){threw=true;}
    Fault.Reset();Check(threw&&done.Calls==1&&ResourceEvents.Refreshed.Count==1,"completion callback exception retried or swallowed");Unchanged(s,before,"completion failure changed committed reward");
   });
  }
 }
}
