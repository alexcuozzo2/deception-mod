using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using K=Deception.Core.NativeExchangeKind;
using U=Polytopia.Data.UnitData.Type;
namespace Deception.Tests
{
 public static partial class SupportTests
 {
  static void GeneralCases()
  {
   foreach(bool mimic in new[]{false,true})foreach(string setup in new[]{"wounded","poisoned","poisoned-wounded","charmed","veteran"})
   Test($"same-type-heal-complete-state-{mimic}-{setup}",()=>{
    var r=Start("mindbender",mimic,independent:false);r.T.health=60;r.T.effects.Add(UnitEffect.Frozen);r.T.effects.Add(UnitEffect.Petrified);r.T.birthClimate=TribeType.Imperius;r.T.birthClimateSkinType=SkinType.Default;
    if(setup.StartsWith("poisoned",StringComparison.Ordinal)){r.T.effects.Add(UnitEffect.Poisoned);r.T.effects.Add(UnitEffect.Charmed);if(setup=="poisoned")r.T.health=100;}
    if(setup=="charmed")r.T.effects.Add(UnitEffect.Charmed);
    if(setup=="veteran"){r.T.health=110;r.T.promotionLevel=1;}
    var old=Row(r);var before=NativeLedgerState.Capture(r.S,new(r.T,true),"warrior",false);string truth=Body(r.T);var intent=Offer(r);Check(!Row(r).Independent&&Body(r.T)==truth,"offer cannot materialize recipient disguise or change true body");Commit(r,intent);var row=Row(r);var a=row.Appearance!;
    int expected=setup=="poisoned"?100:setup=="poisoned-wounded"?60:setup=="veteran"?150:100;
    Check(a.Form=="warrior"&&a.Health==expected&&a.Maximum==(setup=="veteran"?150:100)&&a.Veteran==(setup=="veteran"),"new false-health overlay preserves native type and veteran maximum");
    Check(a.TribeVisual==before.TribeVisual&&a.ClimateVisual==before.ClimateVisual&&a.BirthVisual==before.BirthVisual&&a.SourceColor==before.SourceColor&&a.Concealment==before.Concealment,"first false support keeps faithful public cosmetic profile");
    Check(a.Effects.Contains("Frozen")&&a.Effects.Contains("Petrified")&&!a.Effects.Contains("Poisoned")&&a.Effects.Contains("Charmed")== (setup=="charmed"),"native poison-gated cleanse preserves other apparent statuses");
    Check(row.Independent&&row.Kind==LedgerActualKind.Genuine&&row.LogicalId==old.LogicalId&&row.NativeId==old.NativeId&&row.PaidStars==old.PaidStars&&Body(r.T)==truth,"same-type disguise changes neither identity, investment nor any true native field");
    var frame=NativeGameStateEnvelope.Get(r.S)!;var loaded=NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision);Check(NativeIdentityLedgerCodec.Encode(loaded,frame.Revision).SequenceEqual(frame.Bytes())&&loaded.Units.Single(x=>x.NativeId==2).Independent,"independent equal-type mask persists canonically");
    NativeDisguiseLifecycle.BeforeDamage(r.S,r.T.coordinates,out var damage);r.T.health-=10;NativeDisguiseLifecycle.AfterDamage(r.S,damage);Check(!Row(r).Independent&&Row(r).Appearance is null&&r.T.health==(ushort)(before.Health-10),"positive real damage removes false-health overlay and uses real HP");
   });
   foreach(K kind in new[]{K.FeignBoost,K.FeignSwarm})Test("same-type-swift-independent-"+kind,()=>{
    var r=Start("shaman",independent:false);r.T.health=70;r.T.moved=r.T.attacked=true;r.T.effects.Add(UnitEffect.Poisoned);r.T.effects.Add(UnitEffect.Bubble);string trueBody=Body(r.T);Commit(r,Offer(r,kind));var a=Row(r).Appearance!;
    Check(a.Form=="warrior"&&a.Health==70&&a.Maximum==100&&a.Effects.SequenceEqual(new[]{"Bubble","Poisoned","Swift"}),"false buff starts from complete true public profile and adds native Swift6");Check(Body(r.T)==trueBody,"false buff grants no actual effect, heal or refreshed action");
    var restored=NativeIdentityLedgerCodec.Decode(NativeGameStateEnvelope.Get(r.S)!.Bytes(),1);Check(restored.Units.Single(x=>x.NativeId==2).Appearance!.Effects.Contains("Swift"),"Swift persists through codec");
    NativeFeignSupportEffects.BeforeScope(r.S,typeof(EndTurnCommand).GetMethod("Execute")!,out var turn);NativeFeignSupportEffects.FinishScope(null,turn);NativeFeignSupportEffects.Flush(r.S);Check(Row(r).Appearance!.Effects.Contains("Swift"),"no invented turn-boundary Swarm expiry");
    Scope(r,"BoostAction",()=>NativeFeignSupportEffects.Removed(r.T,UnitEffect.Swift));Check(!Row(r).Appearance!.Effects.Contains("Swift")&&Body(r.T)==trueBody,"received-attack native clear removes only apparent Swift");
   });
   foreach(K kind in new[]{K.FeignHeal,K.FeignSwarm})Test("new-mask-native-vessel-cargo-"+kind,()=>{
    var r=Start(kind==K.FeignHeal?"mindbender":"shaman",independent:false);var cargo=new UnitState{id=3,type=U.giant,UnitData=RecruitmentHooks.Definitions["giant"],health=260,promotionLevel=1,birthClimate=TribeType.Imperius};cargo.effects.Add(UnitEffect.Charmed);
    r.T.type=U.transportship;r.T.UnitData=RecruitmentHooks.Definitions["transportship"];r.T.passengerUnit=cargo;r.T.health=310;r.S.CurrentUnitId=4;
    Edit(r,x=>x.NativeId==2?x with{Transport=new(cargo.id)}:x);string truth=Body(r.T)+Body(cargo);Commit(r,Offer(r,kind));var a=Row(r).Appearance!;
    Check(a.Form=="transportship"&&a.Health==(kind==K.FeignHeal?350:310)&&a.Maximum==450&&a.Passenger is {Form:"giant",Health:260,Maximum:450,Veteran:true},"same-type vessel overlay faithfully captures independent cargo and passenger-derived maximum");
    Check(a.Passenger!.Effects.SequenceEqual(new[]{"Charmed"})&&a.Effects.Contains("Swift")==(kind==K.FeignSwarm),"root false support does not contaminate cargo state");Check(Body(r.T)+Body(cargo)==truth,"true vessel/cargo bodies retain their health, effects, identity and actions");
   });
   foreach(bool poisoned in new[]{false,true})Test("removed-recover-does-not-cleanse-"+poisoned,()=>{
    var r=Start();var effects=poisoned?new[]{"Charmed","Poisoned"}:new[]{"Charmed"};Edit(r,x=>x.NativeId==1?x with{Appearance=Art("warrior",60,100,effects)}:x);r.C.effects.Add(UnitEffect.Poisoned);r.C.effects.Add(UnitEffect.Charmed);string truth=Truth(r);var bytes=NativeGameStateEnvelope.Get(r.S)!.Bytes();
    Check(!NativeFeignSupport.TryOffer(r.S,1,K.FeignRecover,out _,out _),"Recovery still offered for false status");Check(Truth(r)==truth&&bytes.SequenceEqual(NativeGameStateEnvelope.Get(r.S)!.Bytes()),"Removed recovery changed true or apparent poison/HP");
   });
   foreach(K kind in new[]{K.FeignHeal,K.FeignSwarm})Test("recipient-mimic-remains-one-real-hp-"+kind,()=>{
    var r=Start(kind==K.FeignHeal?"mindbender":"shaman");r.T.type=U.dcp_mimic_form_giant;r.T.UnitData=RecruitmentHooks.Definitions[RecruitmentCatalog.Variant("giant")];r.T.health=10;Edit(r,x=>x.NativeId==2?x with{Kind=LedgerActualKind.Mimic}:x);string truth=Body(r.T);Commit(r,Offer(r,kind));var a=Row(r).Appearance!;
    Check(a.Form=="giant"&&a.Health==(kind==K.FeignHeal?100:60)&&a.Effects.Contains("Swift")== (kind==K.FeignSwarm),"recipient Mimic receives only apparent support operation");Check(Body(r.T)==truth&&r.T.health==10&&r.T.UnitData.health==10,"no true Mimic healing/status/offense/action changes");
   });
   foreach(string fail in new[]{"candidate-validation","ledger"})Test("same-type-fake-support-atomic-"+fail,()=>{
    var r=Start("shaman",independent:false);var intent=Offer(r,K.FeignSwarm);var old=NativeGameStateEnvelope.Get(r.S)!.Bytes();string truth=Truth(r);Fault.At=fail;Check(!NativeFeignSupport.TryCommit(r.S,intent,out _,out _),"native/profile publication failure rejected");Check(old.SequenceEqual(NativeGameStateEnvelope.Get(r.S)!.Bytes())&&Truth(r)==truth&&!Row(r).Independent,"new recipient overlay and caster flags roll back exactly");Check(NativeRegroupSession.Faults.Count==0,"successful rollback does not manufacture session fault");
   });
   Test("invalid-new-native-status-denied-before-offer",()=>{
    var r=Start("shaman",independent:false);r.T.effects.Add((UnitEffect)99);string truth=Truth(r);var bytes=NativeGameStateEnvelope.Get(r.S)!.Bytes();Check(!NativeFeignSupport.TryOffer(r.S,1,K.FeignSwarm,out _,out _),"unrepresentable public native status cannot become offered action");Check(bytes.SequenceEqual(NativeGameStateEnvelope.Get(r.S)!.Bytes())&&Truth(r)==truth&&NativeRegroupSession.Faults.Count==0,"offer rejection is pure and leaves match active");
   });
   Test("candidate-validation-runs-before-offer",()=>{
    var r=Start("shaman",independent:false);string truth=Truth(r);var bytes=NativeGameStateEnvelope.Get(r.S)!.Bytes();Fault.At="candidate-validation";Check(!NativeFeignSupport.TryOffer(r.S,1,K.FeignSwarm,out _,out var why)&&why.Contains("candidate-validation"),"full native candidate validation occurs during offer");Check(bytes.SequenceEqual(NativeGameStateEnvelope.Get(r.S)!.Bytes())&&Truth(r)==truth&&NativeRegroupSession.Faults.Count==0,"invalid candidate never queues or consumes any action");
   });
   foreach(string effect in new[]{"Swift","Boosted"})Test("unmasked-native-buff-not-stackable-"+effect,()=>{var r=Start("shaman",independent:false);r.T.effects.Add(Enum.Parse<UnitEffect>(effect));Check(!NativeFeignSupport.TryOffer(r.S,1,K.FeignSwarm,out _,out _),"truthful first appearance preserves already-native buff eligibility");Check(!Row(r).Independent,"ineligible recipient gets no pointless disguise");});
   Test("unregistered-genuine-recipient-is-not-invented-by-query",()=>{var r=Start("shaman",independent:false);var frame=NativeGameStateEnvelope.Get(r.S)!;NativeGameStateEnvelope.Set(r.S,frame.Revision,NativeIdentityLedgerCodec.Encode(Ledger(r) with{Units=Ledger(r).Units.Where(x=>x.NativeId!=2).ToArray()},frame.Revision));var bytes=NativeGameStateEnvelope.Get(r.S)!.Bytes();Check(!NativeFeignSupport.TryOffer(r.S,1,K.FeignSwarm,out _,out _),"query cannot enroll an unbound recipient");Check(bytes.SequenceEqual(NativeGameStateEnvelope.Get(r.S)!.Bytes()),"no allocation of new ledger identity during UI offer");});
  }
 }
}
