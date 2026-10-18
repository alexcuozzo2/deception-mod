using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using U=Polytopia.Data.UnitData.Type;
using T=Polytopia.Data.TerrainData.Type;

namespace Deception.Tests
{
 public static partial class PathPreferenceTests
 {
  static void SpyMovementCases()
  {
   foreach(string form in new[]{SpyCatalog.Id,SpyCatalog.AquarionId})foreach(string barrier in new[]{"enemy","zoc"})
   Test("cloak-spy-mask-sheds-incompatible-"+form+"-"+barrier,()=>{
    var r=Make(form,genuine:"cloak");r.Water.Blocked=true;r.Land.Cost=3;r.U.effects.Add(UnitEffect.Invisible);
    Edit(r,x=>x with{Appearance=x.Appearance! with{Concealment=ApparentConcealment.Hidden,Effects=new[]{"Invisible"}}});
    var dest=new TileData{coordinates=new(3,1)};r.S.Map.Tiles.Add(dest);
    if(barrier=="enemy")r.Land.unit=new(){id=99,owner=2,coordinates=r.Land.coordinates};
    else r.S.Map.Tiles.Add(new(){coordinates=new(2,0),Blocked=true,unit=new(){id=99,owner=2,coordinates=new(2,0)}});
    Check(Cmd(r,dest).IsValid(r.S,out _),"actual Cloak must keep its own route");
    var action=Cmd(r,dest).Execute(r.S);
    Check(action.Path.Count==3&&!Row(r).Independent&&Row(r).Appearance is null,"Spy mask borrowed native Hide traversal or ZOC immunity");
    Check(r.U.type==U.cloak&&r.U.HasEffect(UnitEffect.Invisible),"shed Spy mask changed real Cloak identity/concealment");
   });
   foreach(string form in new[]{SpyCatalog.Id,SpyCatalog.AquarionId})Test("cloak-spy-prefers-compatible-detour-"+form,()=>{
    var r=Make(form,genuine:"cloak");r.Water.Blocked=true;r.Land.Cost=3;r.U.effects.Add(UnitEffect.Invisible);
    Edit(r,x=>x with{Appearance=x.Appearance! with{Concealment=ApparentConcealment.Hidden,Effects=new[]{"Invisible"}}});
    r.Land.unit=new(){id=99,owner=2,coordinates=r.Land.coordinates};
    var dest=new TileData{coordinates=new(4,1)};r.S.Map.Tiles.AddRange(new[]{dest,new(){coordinates=new(3,1)},new(){coordinates=new(0,0),Cost=1},new(){coordinates=new(1,-1),Cost=1},new(){coordinates=new(2,-1),Cost=1},new(){coordinates=new(3,-1),Cost=1},new(){coordinates=new(4,0),Cost=1}});
    var action=Cmd(r,dest).Execute(r.S);
    Check(Native.Items(action.Path).Cast<WorldCoordinates>().Any(c=>c.Y==-1),"detached search chose occupied or ZOC shortcut for Spy mask");
    Check(Row(r).Independent&&Row(r).Appearance?.Form==form,"safe Spy detour should retain disguise");
   });
   foreach(string form in new[]{SpyCatalog.Id,SpyCatalog.AquarionId})foreach(bool mimic in new[]{false,true})foreach(string barrier in new[]{"enemy","zoc"})
   Test("hidden-spy-physical-movement-"+form+"-"+mimic+"-"+barrier,()=>{
    var r=Make(form,genuine:mimic?null:form);r.Water.Blocked=true;r.Land.Cost=3;r.U.effects.Add(UnitEffect.Invisible);
    Edit(r,x=>x with{Appearance=x.Appearance! with{Concealment=ApparentConcealment.Hidden,Effects=new[]{"Invisible"}}});
    var dest=new TileData{coordinates=new(3,1)};r.S.Map.Tiles.Add(dest);
    if(barrier=="enemy")r.Land.unit=new(){id=99,owner=2,coordinates=r.Land.coordinates};
    else r.S.Map.Tiles.Add(new(){coordinates=new(2,0),Blocked=true,unit=new(){id=99,owner=2,coordinates=new(2,0)}});
    var frame=NativeGameStateEnvelope.Get(r.S)!.Bytes();var native=TransportStateDump.Dump(r.S);
    Check(!Has(Options(r),dest)&&!Cmd(r,dest).IsValid(r.S,out _),"hidden Spy gained non-Spy traversal or ZOC immunity");
    Stable(r,frame,native);
   });
   Test("hidden-mimic-exposed-fallback-respects-zoc",()=>{
    var r=Make();r.Water.Blocked=true;r.Land.Cost=3;r.U.effects.Add(UnitEffect.Invisible);
    var dest=new TileData{coordinates=new(3,1)};r.S.Map.Tiles.AddRange(new[]{dest,new(){coordinates=new(2,0),Blocked=true,unit=new(){id=99,owner=2,coordinates=new(2,0)}}});
    Check(!Has(Options(r),dest)&&!Cmd(r,dest).IsValid(r.S,out _),"exposed fallback retained borrowed Invisible and crossed ZOC");
    Check(r.U.HasEffect(UnitEffect.Invisible)&&Row(r).Appearance?.Form=="island","hypothetical fallback mutated actual concealment");
   });
   foreach(bool boat in new[]{false,true})Test("exposed-fallback-effects-isolated-"+boat,()=>{
    var r=Make("warrior",boat?T.water:T.field);if(boat)Wrap(r);r.S.Player.Water=r.S.Player.Ocean=true;r.U.effects.Add(UnitEffect.Invisible);r.U.effects.Add(UnitEffect.Poisoned);
    if(r.U.passengerUnit is object)r.U.passengerUnit.effects.Add(UnitEffect.Invisible);
    int observed=0;PathFinder.During=(s,u)=>{if(ReferenceEquals(u,r.U))return;observed++;Check(!u.effects.Contains(UnitEffect.Invisible)&&u.effects.Contains(UnitEffect.Poisoned),"fallback must remove only borrowed concealment");Check(u.passengerUnit is null||!u.passengerUnit.effects.Contains(UnitEffect.Invisible),"exposed fallback cargo retained borrowed concealment");};
    Options(r);PathFinder.During=null;
    Check(observed>0&&r.U.effects.Contains(UnitEffect.Invisible),"detached fallback missing or real effect mutated");
   });
   foreach(string change in new[]{"dispose","clear","frame","owner","turn","map","root","nested"})Test("spy-profile-scope-bound-"+change,()=>{
    var r=Make(SpyCatalog.Id,genuine:"cloak");var profile=(UnitState)NativeDisguiseMovement.Profile(r.S,r.U,Row(r).Appearance!);
    // Inject legacy/corrupt Hide data solely to exercise scope cleanup; current Spy catalog has no Hide.
    profile.UnitData.unitAbilities.Add(UnitAbility.Hide);
    using var scope=NativeSpyMovementProfile.Begin(r.S,r.U,profile);Check(scope is not null&&scope.Valid,"detached Spy scope rejected valid bound source");
    Check(!UnitDataExtensions.HasAbility(profile,Polytopia.Data.UnitAbility.Type.hide)&&!UnitDataExtensions.HasAbility(profile.UnitData,Polytopia.Data.UnitAbility.Type.hide),"detached profile still has physical Hide");
    Check(UnitDataExtensions.HasAbility(r.U,Polytopia.Data.UnitAbility.Type.hide),"hypothetical Spy scope changed real Cloak");
    if(change=="nested"){
     var second=(UnitState)NativeDisguiseMovement.Profile(r.S,r.U,Row(r).Appearance!);
     using(var inner=NativeSpyMovementProfile.Begin(r.S,r.U,second)){Check(UnitDataExtensions.HasAbility(profile,Polytopia.Data.UnitAbility.Type.hide),"nested profile leaked outer actor scope");Check(!UnitDataExtensions.HasAbility(second,Polytopia.Data.UnitAbility.Type.hide),"nested profile not scoped");}
     Check(!UnitDataExtensions.HasAbility(profile,Polytopia.Data.UnitAbility.Type.hide),"outer profile not restored");scope!.Dispose();
    }else if(change=="dispose")scope!.Dispose();else if(change=="clear")NativeCloakMovement.Clear();else if(change=="frame"){var f=NativeGameStateEnvelope.Get(r.S)!;NativeGameStateEnvelope.Set(r.S,f.Revision,f.Bytes());}
    else if(change=="owner")r.U.owner=2;else if(change=="turn")r.S.CurrentTurn++;else if(change=="map")r.S.Map=new();else r.Source.unit=null;
    Check(UnitDataExtensions.HasAbility(profile,Polytopia.Data.UnitAbility.Type.hide)&&UnitDataExtensions.HasAbility(profile.UnitData,Polytopia.Data.UnitAbility.Type.hide),"closed/stale profile scope leaked Hide suppression");
   });
  }
 }
}
