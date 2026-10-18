using System;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests {
 public static partial class BlackoutTests {
  static GameState Fogger(bool ready=true,bool disguise=false) {var s=Make(disguise:disguise);if(ready)Tile(s,1,1).unit!.effects.Add(UnitEffect.DoubleReady);return s;}
  static void CheckReady(GameState s,bool yes){var u=Tile(s,1,1).unit!;Check(NativeFoggerActions.Ready(s,u,1)==yes,"genuine remaining action");Check(NativeSpyNetwork.TryFoggerReachable(s,1,1,out _,out _)==yes,"native range matches action allowance");}
  public static int FoggerRun(){try{
   foreach(bool moved in new[]{false,true})Test("Fogger-two-Blackouts-"+moved,()=>{
    var s=Fogger();var u=Tile(s,1,1).unit!;u.moved=moved;u.effects.Add(UnitEffect.Boosted);u.effects.Add(UnitEffect.Poisoned);var effects=u.effects;
    CheckReady(s,true);Cast(s,2,1);Check(u.moved&&!u.attacked&&!u.effects.Contains(UnitEffect.DoubleReady),"first consumes native DoubleReady and movement only");
    Check(!u.effects.Contains(UnitEffect.Boosted)&&u.effects.Contains(UnitEffect.Poisoned)&&effects.Contains(UnitEffect.DoubleReady),"native exhaustion consumes boost, preserves poison and detached preimage");CheckReady(s,true);
    Cast(s,3,1);Check(u.moved&&u.attacked,"second exhausts both flags");CheckReady(s,false);Check(!NativeBlackout.TryOffer(s,1,2,2,out _,out _),"third shot unavailable");
    Check(Read(s).Receipts.Length==2&&Read(s).Blackouts!.Length==4&&u.health==100,"two independent fog targets; no damage");
   });
   Test("Fogger-single-remaining-action-does-not-regrant-DoubleReady",()=>{var s=Fogger(false);Cast(s,2,1);CheckReady(s,false);Check(Tile(s,1,1).unit!.attacked,"no fabricated native second allowance");});
   Test("Fogger-native-turn-reset-restores-two-allowances",()=>{var s=Fogger();Cast(s,2,1);Cast(s,3,1);var u=Tile(s,1,1).unit!;s.CurrentTurn++;u.moved=u.attacked=false;u.effects.Add(UnitEffect.DoubleReady);Cast(s,2,2);CheckReady(s,true);Cast(s,3,2);CheckReady(s,false);Check(Read(s).Receipts.Length==4,"separate persisted receipts in native-reset turn");});
   Test("Fogger-first-receipt-retry-does-not-spend-second",()=>{var s=Fogger();var i=Offer(s,2,1);Check(NativeBlackout.TryCommit(s,i,out _,out _),"first accepted");var snap=NativeGameStateEnvelope.Get(s)!;var effects=Tile(s,1,1).unit!.effects;Check(NativeBlackout.TryCommit(s,i,out var retry,out _)&&retry!.AlreadyCommitted,"duplicate admitted only as receipt");Check(ReferenceEquals(snap,NativeGameStateEnvelope.Get(s))&&ReferenceEquals(effects,Tile(s,1,1).unit!.effects),"receipt changes nothing");CheckReady(s,true);Cast(s,3,1);CheckReady(s,false);});
   Test("Fogger-first-shot-native-fields-and-ledger-survive-reconstruction",()=>{
    var s=Fogger();var first=Offer(s,2,1);Check(NativeBlackout.TryCommit(s,first,out _,out _),"first");var saved=NativeGameStateEnvelope.Get(s)!;var clone=Fogger(false);var u=Tile(clone,1,1).unit!;u.moved=true;u.attacked=false;
    // Native UnitState persistence stores moved/attacked/effects. The real mod
    // ledger codec stores receipts/fog; reconstruct both independently here.
    NativeGameStateEnvelope.Set(clone,saved.Revision,saved.Bytes());Tile(clone,2,1).explorers=new();Tile(clone,2,1).explorers!.Add(1);NativeBlackout.Bind(clone);
    Check(NativeBlackout.TryCommit(clone,first,out var retry,out _)&&retry!.AlreadyCommitted,"saved receipt idempotent");Cast(clone,3,1);CheckReady(clone,false);Check(!Tile(s,1,1).unit!.attacked,"source state remains independent");
   });
   foreach(string fault in new[]{"exhaust","fog","validate","ledger"})foreach(bool second in new[]{false,true})Test("Fogger-atomic-budget-rollback-"+fault+"-"+second,()=>{
    var s=Fogger();if(second)Cast(s,2,1);var u=Tile(s,1,1).unit!;u.effects.Add(UnitEffect.Boosted);var effects=u.effects;bool moved=u.moved;var snapshot=NativeGameStateEnvelope.Get(s)!;var i=Offer(s,3,1);Fault.At=fault;
    Check(!NativeBlackout.TryCommit(s,i,out _,out _),"fault denied");Check(ReferenceEquals(effects,u.effects)&&u.moved==moved&&!u.attacked&&u.effects.Contains(UnitEffect.DoubleReady)==!second&&u.effects.Contains(UnitEffect.Boosted),"exact native allowance restored");
    Check(snapshot.Revision==NativeGameStateEnvelope.Get(s)!.Revision&&snapshot.Bytes().SequenceEqual(NativeGameStateEnvelope.Get(s)!.Bytes())&&Tile(s,3,1).GetExplored(2),"exact fog/ledger preimages");Cast(s,3,1);CheckReady(s,!second);
   });
   foreach(var type in new[]{UnitType.dcp_spy,UnitType.dcp_spymaid,UnitType.warrior,UnitType.dcp_mimic_form_dcp_motherfogger,UnitType.transportship})Test("Fogger-genuine-type-only-"+type,()=>{
    var s=Fogger(disguise:true);var u=Tile(s,1,1).unit!;u.type=type;u.passengerUnit=new(){type=UnitType.dcp_motherfogger};Write(s,Read(s) with{Units=Read(s).Units.Select(r=>r with{Appearance=Mask() with{Form=FoggerCatalog.Id,Effects=new[]{"DoubleReady"}}}).ToArray()});
    Check(!NativeBlackout.TryOffer(s,1,2,1,out _,out _)&&!u.moved&&!u.attacked,"disguise/cargo/DoubleReady never grants real Blackout");
   });
   Test("Fogger-disguised-as-Spy-retains-real-Blackout-only",()=>{var s=Fogger(disguise:true);Write(s,Read(s) with{Units=Read(s).Units.Select(r=>r with{Appearance=Mask() with{Form=SpyCatalog.Id}}).ToArray()});Check(!NativeSpyNetwork.TryReachable(s,1,1,out _,out _),"apparent Spy supplies no Subvert network");Cast(s,2,1);Check(Read(s).Units.Single().Appearance is null,"first real action sheds disguise");CheckReady(s,true);});
   Test("Fogger-apparent-DoubleReady-cannot-refund-real-last-shot",()=>{var s=Fogger(false,true);Write(s,Read(s) with{Units=Read(s).Units.Select(r=>r with{Appearance=Mask() with{Effects=new[]{"DoubleReady"}}}).ToArray()});Cast(s,2,1);CheckReady(s,false);});
   foreach(string invalid in new[]{"frozen","dead","attacked","offturn","foreign","linked","stale-root","stale-coordinates","unregistered","finished","unbound"})Test("Fogger-guard-"+invalid,()=>{
    var s=Fogger();var u=Tile(s,1,1).unit!;switch(invalid){case"frozen":u.effects.Add(UnitEffect.Frozen);break;case"dead":u.health=0;break;case"attacked":u.attacked=true;break;case"offturn":s.CurrentPlayer=2;break;case"foreign":u.owner=2;break;case"linked":u.Linked=true;break;case"stale-root":Tile(s,1,1).unit=new(){id=8,type=UnitType.warrior};break;case"stale-coordinates":u.coordinates=new(2,2);break;case"unregistered":RecruitmentHooks.Registered=false;break;case"finished":s.CurrentState=GameState.State.Ended;break;case"unbound":NativeRegroupSession.Owned.Clear();break;}
    var snap=NativeGameStateEnvelope.Get(s)!;Check(!NativeBlackout.TryOffer(s,1,2,1,out _,out _),"invalid source unavailable");Check(ReferenceEquals(snap,NativeGameStateEnvelope.Get(s))&&u.effects.Contains(UnitEffect.DoubleReady),"offer retains allowance and persistence");
   });
   Test("Fogger-native-diagonal-range-two-no-neighbor-extension",()=>{var s=Fogger();Add(s,2,3,1,type:UnitType.dcp_motherfogger);Check(NativeBlackout.TryOffer(s,1,3,3,out _,out _)&&!NativeBlackout.TryOffer(s,1,4,1,out _,out _),"exact native radius regardless of nearby ally");});
   Test("Fogger-second-target-revalidates-remaining-native-action",()=>{var s=Fogger();Cast(s,2,1);var i=Offer(s,3,1);Tile(s,1,1).unit!.attacked=true;Check(!NativeBlackout.TryCommit(s,i,out _,out _)&&Tile(s,3,1).GetExplored(2),"stale second shot cannot bypass exhaustion");});
   Test("Fogger-rogue-turn-lifetime-still-bounds-Blackout",()=>{var s=Fogger();Write(s,Read(s) with{Units=Read(s).Units.Select(r=>r with{Rogue=new(2,1,s.CurrentTurn)}).ToArray()});Cast(s,2,1);CheckReady(s,true);s.CurrentTurn++;CheckReady(s,false);});
   Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases.Count,assertions=Assertions,tests=Cases,scope="Production Fogger action budget, Blackout transaction, native-network guards and real ledger codecs; native game NOT RUN"}));return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
 public static class FoggerActionTests{public static int Run()=>BlackoutTests.FoggerRun();}
}
