using System;
using System.Linq;
using Deception.PolyModAdapter;
using U=Polytopia.Data.UnitData.Type;
using A=Polytopia.Data.UnitAbility.Type;
namespace Deception.Tests {
 public static partial class OrdinaryActionTests {
 static bool EmbarkHide(UnitState u){bool result=UnitDataExtensions.HasAbility(u,A.hide);NativeSpyActions.EmbarkAbility(u,A.hide,ref result);return result;}
 static void SpyStealthCases(){
  Test("A50-spy-safe-hook-ABI",()=>{var h=new HarmonyLib.Harmony();NativeSpyActions.Install(h);Check(h.Hooks.Count==7&&h.Hooks.All(m=>!m.GetParameters().Any(p=>p.ParameterType.BaseType?.FullName=="Il2CppSystem.ValueType")),"all new hooks use native-safe reference arguments");});
  foreach(U species in new[]{U.dcp_spy,U.dcp_spymaid})Test("A50-spy-boarding-keeps-native-raft-"+species,()=>{
   var w=new World(species);NativeSpyActions.Clear();Check(!EmbarkHide(w.Unit),"Spy has no intrinsic Hide");w.Unit.CopiedCloakHide=true;Check(EmbarkHide(w.Unit),"Cloak disguise grants Hide outside boarding");
   var action=new EmbarkAction{Coordinates=w.Tile.coordinates};NativeSpyActions.BeforeEmbark(action,w.State,out var scope);
   Check(!EmbarkHide(w.Unit)&&EmbarkHide(new UnitState{type=U.cloak}),"only actual boarding Spy suppresses cloak-only vessel choice");
   NativeSpyActions.EmbarkFinished(null,scope);Check(EmbarkHide(w.Unit),"scope leaves ordinary Hide intact");
   NativeSpyActions.BeforeEmbark(action,w.State,out scope);NativeSpyActions.Clear();NativeSpyActions.EmbarkFinished(null,scope);Check(EmbarkHide(w.Unit),"late finalizer cannot restore retired scope");
  });
  Test("A50-spy-boarding-nested-and-failure-unwind",()=>{
   var w=new World();w.Unit.CopiedCloakHide=true;NativeSpyActions.Clear();var action=new EmbarkAction{Coordinates=w.Tile.coordinates};NativeSpyActions.BeforeEmbark(action,w.State,out var outer);
   var next=new EmbarkAction{Coordinates=new(99,99)};NativeSpyActions.BeforeEmbark(next,w.State,out var inner);Check(EmbarkHide(w.Unit),"nested foreign boarding masks outer override");
   var error=new Exception("native");Check(ReferenceEquals(error,NativeSpyActions.EmbarkFinished(error,inner))&&!EmbarkHide(w.Unit),"exception restores exact outer scope");NativeSpyActions.EmbarkFinished(null,outer);Check(EmbarkHide(w.Unit),"outer cleanup complete");
  });
  foreach(U species in new[]{U.dcp_spy,U.dcp_spymaid})foreach(string outcome in new[]{"success","cancelled","invalid","failure","clear"})Test("A50-spy-recover-reveal-"+species+"-"+outcome,()=>{
   var w=new World(species);NativeSpyActions.Clear();w.Unit.health=40;w.Unit.effects.Add(UnitEffect.Invisible);var command=new RecoverCommand{Coordinates=w.Tile.coordinates,Throw=outcome=="failure"};if(outcome=="invalid")w.Unit.moved=true;
   NativeSpyActions.BeforeRecover(command,w.State,out var scope,outcome!="cancelled");Check(w.Unit.HasEffect(UnitEffect.Invisible)&&w.State.ActionStack.Count==0,"validation does not reveal or enqueue");
   if(outcome is "cancelled" or "invalid"){NativeSpyActions.AfterRecover(w.State,scope,false);Check(w.Unit.HasEffect(UnitEffect.Invisible)&&w.State.ActionStack.Count==0,"invalid/cancelled original leaves hide intact");return;}
   try{command.Execute(w.State);}catch(InvalidOperationException){Check(w.Unit.HasEffect(UnitEffect.Invisible)&&!Native.Items(w.State.ActionStack).OfType<RevealAction>().Any(),"throwing native command never runs reveal postfix");return;}
   if(outcome=="clear")NativeSpyActions.Clear();NativeSpyActions.AfterRecover(w.State,scope);
   var actions=Native.Items(w.State.ActionStack).ToArray();if(outcome=="clear"){Check(!actions.OfType<RevealAction>().Any(),"retired scope cannot append new action");return;}
   Check(actions.OfType<RevealAction>().Count()==1&&!actions.OfType<RevealAction>().Single().Show&&actions.OfType<HealAction>().Count()==1,"one native self-reveal with original Heal command retained");
   Check(w.Unit.HasEffect(UnitEffect.Invisible),"only native queued reveal changes hide");w.State.Drain();Check(!w.Unit.HasEffect(UnitEffect.Invisible)&&w.Unit.health==60&&w.Unit.moved&&w.Unit.attacked,"native queue reveals, heals and spends once");
  });
  Test("A50-allied-healing-and-nonspy-recovery-do-not-force-reveal",()=>{
   var w=new World(U.cloak);w.Unit.health=40;w.Unit.effects.Add(UnitEffect.Invisible);var c=new RecoverCommand{Coordinates=w.Tile.coordinates};NativeSpyActions.BeforeRecover(c,w.State,out var scope);c.Execute(w.State);NativeSpyActions.AfterRecover(w.State,scope);w.State.Drain();Check(w.Unit.HasEffect(UnitEffect.Invisible),"genuine cloak native recovery unchanged");
   w.Unit.type=U.dcp_spy;new HealAction{Coordinates=w.Tile.coordinates}.Execute(w.State);Check(w.Unit.HasEffect(UnitEffect.Invisible),"ally healing is not caster ability and cannot expose Spy");
  });
 }
 }
}
