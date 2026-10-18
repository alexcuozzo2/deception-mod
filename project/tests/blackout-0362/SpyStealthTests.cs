using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests { public static partial class BlackoutTests {
 static void SpyStealthCases(){
  foreach(var species in new[]{UnitType.dcp_spy,UnitType.dcp_spymaid})Test("A52-Spy-Blackout-removed-"+species,()=>{var s=SubvertBoard();var u=Tile(s,1,1).unit!;u.type=species;u.effects.Add(UnitEffect.Invisible);var before=NativeGameStateEnvelope.Get(s)!;Check(!NativeBlackout.TryOffer(s,1,2,1,out _,out _),"genuine Spy cannot Blackout");Check(u.effects.Contains(UnitEffect.Invisible)&&!u.attacked&&!u.moved&&ReferenceEquals(before,NativeGameStateEnvelope.Get(s)),"removed ability cannot expose, spend or publish");});
  foreach(var species in new[]{UnitType.dcp_spy,UnitType.dcp_spymaid})foreach(bool subvert in new[]{true})foreach(string outcome in (subvert?new[]{"success","spent","stale","validate","ledger","sight"}:new[]{"success","spent","stale","validate","ledger"}))Test("A50-spy-ability-reveal-"+species+"-"+subvert+"-"+outcome,()=>{
   var s=SubvertBoard();var unit=Tile(s,1,1).unit!;unit.type=unit.UnitData.type=species;unit.effects.Add(UnitEffect.Invisible);unit.effects.Add(UnitEffect.Poisoned);
   Write(s,Read(s) with{Units=Read(s).Units.Select(r=>r.NativeId==1?r with{Appearance=Mask() with{Concealment=ApparentConcealment.Hidden,Effects=new[]{"Invisible"}}}:r).ToArray()});
   var intent=subvert?SubvertOffer(s):Offer(s,2,1);var effects=unit.effects;var before=NativeGameStateEnvelope.Get(s)!;
   Check(unit.effects.Contains(UnitEffect.Invisible)&&!unit.moved&&!unit.attacked,"offer does not expose or spend hidden caster");
   if(outcome=="spent")unit.attacked=true;else if(outcome=="stale")s.CurrentTurn++;else if(outcome!="success")Fault.At=outcome;
   bool ok=subvert?NativeSubvert.TryCommit(s,intent,out _,out _):NativeBlackout.TryCommit(s,intent,out _,out _);
   if(outcome=="success"){
    Check(ok&&!unit.effects.Contains(UnitEffect.Invisible)&&unit.effects.Contains(UnitEffect.Poisoned)&&effects.Contains(UnitEffect.Invisible),"committed action exposes actual Spy in new owned effects list");
    Check(unit.moved&&unit.attacked&&!Read(s).Units.Single(r=>r.NativeId==1).Independent,"committed action spends allowance and sheds mask");
    var snapshot=NativeGameStateEnvelope.Get(s)!;var after=unit.effects;
    Check(subvert?NativeSubvert.TryCommit(s,intent,out var duplicate,out _)&&duplicate!.AlreadyCommitted:NativeBlackout.TryCommit(s,intent,out duplicate,out _)&&duplicate!.AlreadyCommitted,"duplicate returns receipt");
    Check(ReferenceEquals(snapshot,NativeGameStateEnvelope.Get(s))&&ReferenceEquals(after,unit.effects),"duplicate does not apply concealment or spend twice");
   }else Check(!ok&&ReferenceEquals(effects,unit.effects)&&unit.effects.Contains(UnitEffect.Invisible)&&Read(s).Units.Single(r=>r.NativeId==1).Appearance!.Concealment==ApparentConcealment.Hidden&&NativeGameStateEnvelope.Get(s)!.Bytes().SequenceEqual(before.Bytes()),"rejection/rollback retains actual hide and hidden mask");
  });
 }
}}
