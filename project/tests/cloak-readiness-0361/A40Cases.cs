using System;
using System.IO;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using U=Polytopia.Data.UnitData.Type;

namespace Deception.Tests
{
 public static partial class CloakReadinessTests
 {
  static (GameState,UnitState) ReadyInfiltrator(U actual=U.cloak,string form="rider")
  {
   var(s,u)=Make(actual,form,true);u.effects.Remove(UnitEffect.Frozen);u.effects.Add(UnitEffect.Poisoned);
   s.Map.Tiles[(1,0)]=new(){City=true,owner=2};return(s,u);
  }
  static void Drain(GameState s)
  {
   while(s.ActionStack.Count>0)
   {var action=s.ActionStack.Last();if(action is InfiltrateAction infiltrate)infiltrate.Execute(s);s.ActionStack.Remove(action);}
  }
  static void A40Cases()
  {
   Test("A40-retired-visual-suppression-registers-no-hooks",()=>{
    var harmony=new HarmonyLib.Harmony();NativeDisguiseConcealmentVisual.Install(harmony);
    Check(harmony.Patches==0,"no HasEffect or SetVisible suppression survives");
   });
   foreach(U actual in new[]{U.cloak,U.mermaidcloak,U.cloak_boat})Test("A40-native-owner-alpha-and-opponent-hide-"+actual,()=>{
    var(s,u)=Make(actual,"rider",true);var v=Visual(s,u);var c=Context(s);
    GameManager.Client!.Player=s.Players[1];c.localPlayerId=1;v.SetVisible(!StateHidden(s,u,1),c);
    Check(v.Visible&&v.Alpha==0.35f&&Effect(u,UnitEffect.Invisible),"native Invisible pulse alpha reaches owner's disguise without suppression");
    GameManager.Client.Player=s.Players[2];c.localPlayerId=2;v.SetVisible(!StateHidden(s,u),c);
    Check(!v.Visible&&Effect(u,UnitEffect.Invisible),"native hostile concealment hides the entire public body");
    var action=new RevealAction();NativeDisguiseConcealment.BeforeReveal(action,s,out var scope);action.Execute(s);NativeDisguiseConcealment.AfterEffect(scope,true);
    v.SetVisible(!StateHidden(s,u),c);Check(v.Visible&&v.Alpha==1f&&Row(s).Appearance!.Concealment==ApparentConcealment.Visible,"native detection restores normal opacity and retains rider mask");
   });
   Test("A40-native-peace-access-does-not-change-underlying-hide",()=>{
    var(s,u)=Make(earned:true);s.Players[2].Peace.Add(1);Check(!StateHidden(s,u)&&Effect(u,UnitEffect.Invisible),"native friendly visibility does not consume actual Hide");
    s.Players[2].Peace.Clear();Check(StateHidden(s,u),"ending friendly access restores native enemy concealment");
   });
   Test("A40-no-visual-state-borrowing-during-clear-or-replay",()=>{
    var(s,u)=Make(earned:true);var v=Visual(s,u);var c=Context(s);s.Owned=false;GameManager.Client!.IsReplay=true;
    NativeDisguiseConcealmentVisual.Clear();NativeDisguiseConcealment.Clear();v.SetVisible(true,c);
    Check(Effect(u,UnitEffect.Invisible)&&v.Alpha==0.35f&&u.health==70&&!u.attacked&&!u.moved,"native detached/replay effect reads are never rewritten");
   });
   foreach(U actual in new[]{U.cloak,U.mermaidcloak,U.cloak_boat})foreach(string form in new[]{"warrior","rider","dragon","cloak"})Test("A40-effective-hidden-mask-"+actual+"-"+form,()=>{
    var(s,u)=Make(actual,form,true);var a=Row(s).Appearance!;var effects=u.effects;var bytes=NativeGameStateEnvelope.Get(s)!.Bytes();
    var effective=NativeDisguiseConcealment.EffectiveAppearance(a,u);
    Check(effective.Form==form&&effective.Concealment==ApparentConcealment.Hidden&&effective.Effects.SequenceEqual(new[]{"Invisible","Poisoned"}),"public copied condition includes actual Cloak concealment");
    Check(a.Concealment==ApparentConcealment.Visible&&a.Effects.SequenceEqual(new[]{"Poisoned"})&&ReferenceEquals(effects,u.effects)&&bytes.SequenceEqual(NativeGameStateEnvelope.Get(s)!.Bytes()),"inspection does not mutate original snapshot, native effects or ledger");
    Check(ReferenceEquals(effective,NativeDisguiseConcealment.EffectiveAppearance(effective,u)),"already effective immutable state is reused");
   });
   Test("A40-old-visible-mask-save-is-immediately-hidden",()=>{
    var(s,u)=Make(earned:true);var bytes=NativeGameStateEnvelope.Get(s)!.Bytes();var loaded=NativeIdentityLedgerCodec.Decode(bytes,0);var other=new GameState();
    other.Players[1]=s.Players[1];other.Players[2]=s.Players[2];var native=new UnitState{id=1,type=U.cloak};native.effects.Add(UnitEffect.Invisible);other.Map.Tiles[(0,0)]=new(){unit=native};
    NativeGameStateEnvelope.Set(other,0,NativeIdentityLedgerCodec.Encode(loaded,0));GameManager.GameState=other;
    Check(StateHidden(other,native)&&NativeDisguiseConcealment.EffectiveAppearance(loaded.Units[0].Appearance!,native).Concealment==ApparentConcealment.Hidden,"old serialized visible mask immediately respects native loaded hide");
    Check(bytes.SequenceEqual(NativeGameStateEnvelope.Get(other)!.Bytes())&&u.type==U.cloak,"read does not publish a migration or borrow a sibling body");
   });
   Test("A40-effective-appearance-keeps-independent-cargo",()=>{
    var(s,u)=Make(U.cloak_boat,"raft",true);u.passengerUnit=new(){id=9,type=U.cloak};u.passengerUnit.effects.Add(UnitEffect.Invisible);
    var a=Mask("raft") with{Passenger=Mask("rider")};var value=NativeDisguiseConcealment.EffectiveAppearance(a,u);
    Check(value.Concealment==ApparentConcealment.Hidden&&value.Passenger!.Concealment==ApparentConcealment.Hidden&&value.Passenger.Form=="rider","public vessel and actual Cloak cargo derive their respective concealment");
    Check(a.Passenger!.Concealment==ApparentConcealment.Visible&&u.passengerUnit.id==9&&u.passengerUnit.type==U.cloak,"borrowed actual/apparent cargo untouched");
   });
   foreach(U actual in new[]{U.warrior,U.dcp_mimic_cloak})Test("A40-effective-mask-does-not-promote-other-body-"+actual,()=>{
    var(s,u)=Make(actual,"warrior",true);var a=Row(s).Appearance!;
    Check(ReferenceEquals(a,NativeDisguiseConcealment.EffectiveAppearance(a,u))&&!UnitDataExtensions.HasAbility(u,Polytopia.Data.UnitAbility.Type.Infiltrate),"genuine weapon never inferred from Invisible or costume");
   });
   Test("A40-effective-mask-rejects-malformed-status",()=>{
    var(s,u)=Make(earned:true);var a=Mask() with{Concealment=ApparentConcealment.Hidden};bool rejected=false;
    try{NativeDisguiseConcealment.EffectiveAppearance(a,u);}catch(InvalidDataException){rejected=true;}
    Check(rejected&&u.effects.Contains(UnitEffect.Invisible),"normalization does not launder invalid snapshot state");
   });
   foreach(U actual in new[]{U.cloak,U.mermaidcloak,U.cloak_boat})Test("A40-committed-infiltrate-consumes-real-cloak-"+actual,()=>{
    var(s,u)=ReadyInfiltrator(actual);var command=new AttackCommand();Check(command.IsValid(s,out _),"actual native command is ready before shedding");
    command.Execute(s);Check(!Row(s).Independent&&Row(s).Appearance is null&&u.HasEffect(UnitEffect.Invisible)&&s.ActionStack.Single() is InfiltrateAction,"same command sheds once and queues real Infiltrate without deleting earned hide");
    Check(u.type==actual&&u.health==70&&!u.moved&&u.attacked&&u.HasEffect(UnitEffect.Poisoned),"native command spends its normal action without a separate reset");
    Drain(s);Check(s.Map.Tiles[(0,0)].unit is null&&s.Infiltrations==1&&s.InfiltrationOwner==1&&s.Map.Tiles[(1,0)].Infiltrated,"native action consumes Cloak and queues/applies its normal city consequence for commander");
    command.Execute(s);Drain(s);Check(s.Infiltrations==1,"no second action or duplicate consequence");
   });
   Test("A40-rogue-cloak-sheds-with-authority-and-investment-intact",()=>{
    var(s,u)=ReadyInfiltrator();Put(s,Row(s) with{Rogue=new LedgerRogue(2,1,4)});new AttackCommand().Execute(s);
    Check(Row(s) is {Independent:false,PaidStars:8,Rogue.Owner:2,Rogue.Commander:1}&&u.owner==1,"shed retains actual Rogue command/original-owner and paid identity");
    Drain(s);Check(s.Infiltrations==1&&s.InfiltrationOwner==1,"native action still belongs to commander");
   });
   foreach(string reason in new[]{"frozen","spent","moved","wrong-actor","wrong-id","removed-source","out-of-range","unexplored","not-city","unowned-city","own-city","peace","capture","city-effect"})Test("A40-invalid-native-infiltrate-preserves-mask-"+reason,()=>{
    var(s,u)=ReadyInfiltrator();var command=new AttackCommand();var tile=s.Map.Tiles[(1,0)];
    if(reason=="frozen")u.effects.Add(UnitEffect.Frozen);if(reason=="spent")u.attacked=true;if(reason=="moved")u.moved=true;
    if(reason=="wrong-actor")command.PlayerId=2;if(reason=="wrong-id")command.UnitId=9;if(reason=="removed-source")s.Map.Tiles[(0,0)].unit=null;
    if(reason=="out-of-range")command.Target=new(5,0);if(reason=="unexplored")tile.Explored=false;if(reason=="not-city")tile.City=false;if(reason=="unowned-city")tile.owner=0;
    if(reason=="own-city")tile.owner=1;if(reason=="peace")s.Players[1].Peace.Add(2);if(reason=="capture")tile.BeingCaptured=true;if(reason=="city-effect")tile.Infiltrated=true;
    var snapshot=NativeGameStateEnvelope.Get(s);var effects=u.effects;bool moved=u.moved,attacked=u.attacked;
    NativeDisguiseLifecycle.BeforeActive(command,s,out _);command.Execute(s);
    Check(ReferenceEquals(snapshot,NativeGameStateEnvelope.Get(s))&&Row(s).Independent&&ReferenceEquals(effects,u.effects)&&u.moved==moved&&u.attacked==attacked,"native invalid command neither sheds nor changes readiness");
    Check(s.ActionStack.Count==0&&s.Infiltrations==0,"no native action is fabricated for invalid target");
   });
   foreach(U actual in new[]{U.warrior,U.dcp_mimic_cloak})Test("A40-costume-cannot-dispatch-infiltrate-"+actual,()=>{
    var(s,u)=ReadyInfiltrator(actual,"cloak");var command=new AttackCommand();NativeDisguiseLifecycle.BeforeActive(command,s,out _);command.Execute(s);
    Check(Row(s).Independent&&s.ActionStack.Count==0&&u.HasEffect(UnitEffect.Invisible)&&!u.attacked,"hidden Cloak copy does not grant actual weapon/action");
   });
   Test("A40-earlier-native-command-cancellation-does-not-shed",()=>{
    var(s,u)=ReadyInfiltrator();var snapshot=NativeGameStateEnvelope.Get(s);var effects=u.effects;
    NativeDisguiseLifecycle.BeforeActive(new AttackCommand(),s,out bool scope,false);
    Check(!scope&&ReferenceEquals(snapshot,NativeGameStateEnvelope.Get(s))&&ReferenceEquals(effects,u.effects)&&Row(s).Independent,"an earlier rejecting command prefix cannot trigger a cosmetic commit");
    Check(s.ActionStack.Count==0&&!u.attacked,"cancellation never queues or consumes an action");
   });
   Test("A40-native-detected-cloak-eligibility-is-not-replaced",()=>{
    var(s,u)=ReadyInfiltrator();u.effects.Remove(UnitEffect.Invisible);var command=new AttackCommand();
    Check(command.IsValid(s,out _),"native CanAttack does not require a counterfeit extra Invisible test");
    command.Execute(s);Drain(s);Check(s.Infiltrations==1,"real eligible Cloak still follows its native command timing");
   });
   Test("A40-disguise-publication-failure-rolls-back-before-native-action",()=>{
    var(s,u)=ReadyInfiltrator();var snapshot=NativeGameStateEnvelope.Get(s);var effects=u.effects;NativeLedgerState.Fail=true;bool rejected=false;
    try{new AttackCommand().Execute(s);}catch(InvalidDataException){rejected=true;}finally{NativeLedgerState.Fail=false;}
    Check(rejected&&ReferenceEquals(snapshot,NativeGameStateEnvelope.Get(s))&&ReferenceEquals(effects,u.effects)&&Row(s).Independent,"failed authoritative publication restores original mask/effect ownership");
    Check(!u.attacked&&!u.moved&&u.HasEffect(UnitEffect.Invisible)&&s.ActionStack.Count==0&&NativeRegroupSession.Faults==1,"nothing queues/spends before successful shed publication");
   });
  }
 }
}
