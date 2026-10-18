using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using U=Polytopia.Data.UnitData.Type;
using A=Polytopia.Data.UnitAbility.Type;
namespace Deception.Tests {
 public static partial class CloakReadinessTests {
 static void SpyCases()
 {
  foreach(U species in new[]{U.dcp_spy,U.dcp_spymaid})foreach(bool masked in new[]{false,true})Test("A65-spy-native-move-visible-"+species+"-masked-"+masked,()=>{
   var(s,u)=Make(species);u.effects.Remove(UnitEffect.Frozen);if(!masked)Put(s,Row(s) with{Independent=false,Appearance=null});
   Check(!StateHidden(s,u)&&!u.effects.Contains(UnitEffect.Invisible),"recruited body starts visible");
   NativeDisguiseConcealment.BeforeMove(new MoveCommand{UnitId=1},s,out var scope);Check(!HideAbility(u),"native move cannot queue intrinsic Spy Hide");NativeDisguiseConcealment.MoveFinished(null,scope);
   s.ActionStack.Add(new MoveAction());var hide=new HideAction();Check(!NativeDisguiseConcealment.BeforeHide(hide,s,out var effect)&&effect is null,"even pending movement cannot admit Spy Hide");u.moved=true;
   Check(u.moved&&!StateHidden(s,u)&&!u.effects.Contains(UnitEffect.Invisible),"moving Spy remains visible to opponent");
   Check(!UnitDataExtensions.HasAbility(u,A.Dash)&&!UnitDataExtensions.HasAbility(u,A.Infiltrate),"Spy gains neither Dash nor Infiltrate");
   if(masked)Check(Row(s).Appearance!.Concealment==ApparentConcealment.Visible&&Row(s).Appearance!.Form=="warrior","ordinary disguise remains visible");
   u.moved=u.attacked=false;s.CurrentTurn++;Check(!StateHidden(s,u),"next turn remains visible");
  });
  foreach(U species in new[]{U.dcp_spy,U.dcp_spymaid})Test("A50-spy-stationary-hide-denied-"+species,()=>{
   var(s,u)=Make(species);bool valid=true;string reason="";NativeDisguiseConcealment.AfterHideValid(new HideCommand(),s,ref valid,ref reason);
   Check(!valid&&reason.Contains("cannot hide"),"standalone Hide is not a Spy ability");
   Check(!NativeDisguiseConcealment.BeforeHide(new HideAction(),s,out var effect)&&effect is null&&!u.attacked&&!u.effects.Contains(UnitEffect.Invisible),"direct Hide action without movement witness has no effect");
  });
  foreach(U species in new[]{U.dcp_spy,U.dcp_spymaid})Test("A65-spy-legacy-effect-and-active-reveal-"+species,()=>{
   var(s,u)=Make(species,earned:true);var old=u.effects;
   var effective=NativeDisguiseConcealment.EffectiveAppearance(Mask(),u);
   Check(effective.Concealment==ApparentConcealment.Visible&&!effective.Effects.Contains("Invisible"),"legacy Spy effect does not grant intrinsic concealment to a new mask");
   var passive=(Il2CppSystem.Collections.Generic.List<UnitEffect>)NativeDisguiseConcealment.WithConcealment(old,Mask(),u);
   Check(!passive.Contains(UnitEffect.Invisible)&&old.Contains(UnitEffect.Invisible),"receiving visible disguise drops legacy concealment without mutating old list");
   var active=(Il2CppSystem.Collections.Generic.List<UnitEffect>)NativeDisguiseConcealment.ActiveAbilityEffects(old,u);
   var a=NativeDisguiseConcealment.ActiveAbilityAppearance(Mask() with{Concealment=ApparentConcealment.Hidden,Effects=new[]{"Invisible","Poisoned"}},u);
   Check(!active.Contains(UnitEffect.Invisible)&&active.Contains(UnitEffect.Frozen)&&old.Contains(UnitEffect.Invisible),"ability stages only Invisible removal in separate list");
   Check(a.Form=="warrior"&&a.Concealment==ApparentConcealment.Visible&&!a.Effects.Contains("Invisible")&&a.Effects.Contains("Poisoned"),"feint appearance reveals without changing unrelated apparent status");
   Check(ReferenceEquals(old,u.effects)&&!u.moved&&!u.attacked,"helpers do not apply speculative unit effects or flags");
  });
  foreach(string form in new[]{"cloak"})foreach(string situation in new[]{"move","standalone","spent","wrong-unit","wrong-owner","wrong-path","push","intervening"})Test("A50-mimic-passive-hide-budget-"+form+"-"+situation,()=>{
   var(s,u)=Make(U.dcp_mimic_cloak,form);u.attacked=situation=="spent";
   if(situation!="standalone"){
    var move=new MoveAction{UnitId=situation=="wrong-unit"?2u:1u,PlayerId=(byte)(situation=="wrong-owner"?2:1),Reason=situation=="push"?2:0};
    if(situation=="wrong-path")move.Path=new(){new(2,0),new(1,0)};s.ActionStack.Add(move);if(situation=="intervening")s.ActionStack.Add(new ActionBase());
   }
   NativeDisguiseConcealment.BeforeMove(new MoveCommand{UnitId=1},s,out var movement);Check(HideAbility(u),"copied concealment queues native hide");NativeDisguiseConcealment.MoveFinished(null,movement);
   var action=new HideAction();Check(NativeDisguiseConcealment.BeforeHide(action,s,out var effect),"copied concealment valid");action.Execute(s);NativeDisguiseConcealment.AfterEffect(effect,true);
   Check(u.effects.Contains(UnitEffect.Invisible)&&Row(s).Appearance!.Concealment==ApparentConcealment.Hidden,"native hiding and snapshot mirror completed");
   Check(u.attacked==(situation!="move"),"only an unspent Mimic's immediately pending real move retains Dash action");
  });
  foreach(string form in new[]{"dcp_spy","dcp_spymaid"})Test("A65-mimic-spy-mask-stays-visible-"+form,()=>{
   var(s,u)=Make(U.dcp_mimic_cloak,form);s.ActionStack.Add(new MoveAction());
   NativeDisguiseConcealment.BeforeMove(new MoveCommand{UnitId=1},s,out var scope);Check(!HideAbility(u),"Spy mask cannot queue Hide");NativeDisguiseConcealment.MoveFinished(null,scope);
   bool valid=true;string reason="";NativeDisguiseConcealment.AfterHideValid(new HideCommand(),s,ref valid,ref reason);Check(!valid,"Spy mask rejects standalone Hide");
   Check(!NativeDisguiseConcealment.BeforeHide(new HideAction(),s,out var effect)&&effect is null,"Spy mask rejects pending-move Hide");
   Check(!StateHidden(s,u)&&!u.attacked&&Row(s).Appearance!.Concealment==ApparentConcealment.Visible,"rejection preserves visibility and action budget");
  });
  foreach(U species in new[]{U.dcp_spy,U.dcp_spymaid})Test("A65-spy-cloak-mask-borrows-hide-"+species,()=>{
   var(s,u)=Make(species,"cloak");
   NativeDisguiseConcealment.BeforeMove(new MoveCommand{UnitId=1},s,out var scope);Check(HideAbility(u),"Cloak disguise still lends native Hide");NativeDisguiseConcealment.MoveFinished(null,scope);
   var hide=new HideAction();Check(NativeDisguiseConcealment.BeforeHide(hide,s,out var effect),"Cloak disguise admits Hide");hide.Execute(s);NativeDisguiseConcealment.AfterEffect(effect,true);
   Check(StateHidden(s,u)&&u.attacked&&Row(s).Appearance!.Concealment==ApparentConcealment.Hidden,"borrowed Cloak concealment works with native action cost");
   var shed=(Il2CppSystem.Collections.Generic.List<UnitEffect>)NativeDisguiseConcealment.WithConcealment(u.effects,null,u);Check(!shed.Contains(UnitEffect.Invisible),"shedding Cloak disguise does not retain intrinsic Spy hide");
  });
  Test("A50-actual-cloak-active-helper-preserves-infiltrate-readiness",()=>{
   var(s,u)=Make(earned:true);var a=NativeDisguiseConcealment.EffectiveAppearance(Mask(),u);
   Check(ReferenceEquals(u.effects,NativeDisguiseConcealment.ActiveAbilityEffects(u.effects,u))&&ReferenceEquals(a,NativeDisguiseConcealment.ActiveAbilityAppearance(a,u)),"Spy-specific explicit reveal does not change actual Cloak semantics");
  });
 }
 }
}
