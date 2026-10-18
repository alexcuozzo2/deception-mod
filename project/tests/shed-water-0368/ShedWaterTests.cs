using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using U=Polytopia.Data.UnitData.Type;
using T=Polytopia.Data.TerrainData.Type;
namespace Deception.Tests
{
 public static class ShedWaterTests
 {
  static int Assertions;static readonly List<string> Cases=new();
  static void Check(bool value,string reason){Assertions++;if(!value)throw new Exception(reason);}
  static void Test(string name,Action body){try{body();Cases.Add(name);}catch(Exception e){throw new Exception(name,e);}}
  sealed record Rig(GameState State,UnitState Original,TileData Tile);
  static LedgerAppearance Art(string form){var d=RecruitmentHooks.Definitions[form];return new(form,new("Elyrion","DarkElf"),new("Imperius","Default"),new("Elyrion","DarkElf"),57,(ushort)d.health,(ushort)d.health,false,ApparentConcealment.Visible,-1,Array.Empty<string>());}
  static Rig Make(T terrain=T.water,string form="firedragon",string? genuine=null,bool moved=false,bool attacked=false)
  {
   Fault.At="";RecruitmentHooks.Reset();NativeGameStateEnvelope.Frames.Clear();NativeMimicWaterMovement.Clear();MimicPresentationState.Clear();
   NativeRegroupSession.Enabled=NativeRegroupSession.Admitted=RecruitmentHooks.Registered=true;NativeRegroupSession.Faults.Clear();
   var s=new GameState();NativeRegroupSession.Owned=GameManager.GameState=s;MimicPresentationState.ReadIdentity=NativeRegroupSession.ReadIdentity;
   var tile=new TileData{coordinates=new(2,3),terrain=terrain,IsWater=terrain is T.water or T.ocean,Wetland=terrain==T.wetland};s.Map.Tiles.Add(tile);
   var d=RecruitmentHooks.Definitions[genuine??RecruitmentCatalog.Variant(form)];var u=new UnitState{id=7,type=d.type,UnitData=d,health=genuine is null?(ushort)10:(ushort)70,coordinates=tile.coordinates,moved=moved,attacked=attacked};tile.unit=u;
   var row=new LedgerUnit(8,7,true,genuine is null?LedgerActualKind.Mimic:LedgerActualKind.Genuine,true,Art(form),PaidInvestmentKind.Known,13);
   var ledger=new NativeIdentityLedger(Guid.Parse("a3ca461f-fd6b-44be-8b87-d4483d12f50b"),9,1,new[]{row},Array.Empty<LedgerReceipt>());
   NativeGameStateEnvelope.Set(s,4,NativeIdentityLedgerCodec.Encode(ledger,4));return new(s,u,tile);
  }
  static NativeIdentityLedger Ledger(Rig r){var p=NativeGameStateEnvelope.Get(r.State)!;return NativeIdentityLedgerCodec.Decode(p.Bytes(),p.Revision);}
  static LedgerUnit Row(Rig r)=>Ledger(r).Units.Single(x=>x.LogicalId==8);
  static string Snapshot(Rig r)=>TransportStateDump.Dump(r.State)+"/"+Convert.ToBase64String(NativeGameStateEnvelope.Get(r.State)!.Bytes());
  static NativeExchangeIntent Offer(Rig r){Check(NativeShedDisguise.TryOffer(r.State,r.Tile.unit!.id,out var i,out string why),why);return i!;}
  static void Shed(Rig r){var i=Offer(r);Check(NativeExchangeCommit.TryCommit(r.State,i,out _,out string why),why);}
  static bool Options(Rig r)
  {
   var u=r.Tile.unit!;object? result=Native.NewList("WorldCoordinates",new object[]{r.Tile.coordinates});NativeMimicWaterMovement.Scope? scope=null;
   try{bool run=NativeMimicWaterMovement.BeforeOptions(r.State,r.Tile.coordinates,u,out scope,ref result);if(run)NativeMimicWaterMovement.AfterOptions(r.State,r.Tile.coordinates,UnitDataExtensions.GetMovement(u,r.State),u,scope,ref result);return run&&Native.Items(result!).Any();}
   finally{NativeMimicWaterMovement.Finished(null,scope);}
  }
  static bool Command(Rig r,bool nativeValid=true)
  {
   var u=r.Tile.unit!;var destination=new TileData{coordinates=new(3,3),terrain=r.Tile.terrain,IsWater=r.Tile.IsWater,Wetland=r.Tile.Wetland};r.State.Map.Tiles.RemoveAll(t=>t.coordinates.X==3&&t.coordinates.Y==3);r.State.Map.Tiles.Add(destination);
   PathFinder.Result=(Il2CppSystem.Collections.Generic.List<WorldCoordinates>)Native.NewList("WorldCoordinates",new object[]{destination.coordinates,r.Tile.coordinates});
   var command=new MoveCommand{UnitId=u.id,PlayerId=u.owner,From=r.Tile.coordinates,To=destination.coordinates};string why="native denial";
   NativeMimicWaterMovement.AfterMoveValid(command,r.State,ref nativeValid,ref why);return nativeValid;
  }
  public static int Run(){try
  {
   foreach(var form in new[]{"babydragon","firedragon"})foreach(var terrain in new[]{T.water,T.ocean,T.wetland})Test($"flying-{form}-{terrain}-shed-loses-flight",()=>{
    var r=Make(terrain,form);Check(UnitDataExtensions.HasAbility(r.Original,Polytopia.Data.UnitAbility.Type.fly),"captured native source actually flies");Check(Options(r)&&Command(r),"borrowed flight crosses water without water research");
    bool waterMode=terrain!=T.wetland;
    Check(MimicPresentationState.TryProject(r.Original,out var before)&&before.ActualMimicBoat==waterMode&&before.NativeId==form&&!before.Exposed,"raw water uses boat art; flooded land retains its prior land body");
    Check(r.Original.passengerUnit==null&&Row(r).Transport==null,"boat artwork alone does not invent a carrier/cargo");
    var i=Offer(r);Check(NativeExchangeCommit.TryCommit(r.State,i,out _,out var why),why);var boat=r.Tile.unit!;var row=Row(r);
    Check((waterMode?boat.type==U.dcp_mimic_form_transportship&&boat.passengerUnit==r.Original:boat==r.Original&&boat.passengerUnit==null)&&r.Original.type==U.dcp_mimic,"raw water wraps; flood preserves original exposed land body");
    Check(!UnitDataExtensions.HasAbility(boat,Polytopia.Data.UnitAbility.Type.fly)&&!UnitDataExtensions.HasAbility(r.Original,Polytopia.Data.UnitAbility.Type.fly),"neither raft nor original cargo retains borrowed flight");
    Check(boat.UnitData.movement==(waterMode?RecruitmentHooks.Definitions["transportship"].movement:1)&&r.Original.UnitData.movement==1,"post-shed movement follows retained native land or raft mode");
    Check(boat.health==10&&boat.UnitData.health==10&&boat.UnitData.attack==0&&row.LogicalId==8&&row.PaidStars==13,"true one HP/no attack/investment/logical identity retained");
    Check(waterMode?row.Transport?.PassengerNativeId==r.Original.id&&row.Appearance?.Passenger?.Form==RecruitmentCatalog.MenuId:row.Transport==null&&row.Appearance?.Form==RecruitmentCatalog.MenuId,"native and apparent passenger lineage matches retained mode");
    Check(MimicPresentationState.TryProject(boat,out var after)&&after.Exposed&&after.ActualMimicBoat==waterMode&&after.ApparentMimicBoat==waterMode&&after.ApparentHealth==1&&after.ActualHealth==1,"public and private exposed art match retained mode with healthy 1 HP");
    bool movable=terrain==T.wetland;Check(Options(r)==movable&&Command(r)==movable,"locked raw water strands raft; native Wetland alternative remains legal");
    Check(!Command(r,false),"custom policy never turns a native denied command valid");
    var snap=NativeGameStateEnvelope.Get(r.State)!;NativeGameStateEnvelope.Set(r.State,snap.Revision,NativeIdentityLedgerCodec.Encode(Ledger(r),snap.Revision));NativeMimicWaterMovement.Clear();MimicPresentationState.Clear();
    Check(Options(r)==movable&&(waterMode?Row(r).Appearance?.Passenger?.Form:Row(r).Appearance?.Form)==RecruitmentCatalog.MenuId,"ledger encode/decode plus scope clear does not restore flight");
    string committed=Snapshot(r);Check(NativeExchangeCommit.TryCommit(r.State,i,out var duplicate,out why)&&duplicate!.AlreadyCommitted&&Snapshot(r)==committed,"duplicate receipt neither allocates another raft nor restores profile");
   });
   foreach(var terrain in new[]{T.water,T.ocean})foreach(var unlock in new[]{0,1,2})Test($"shed-{terrain}-owner-research-{unlock}",()=>{
    var r=Make(terrain);Shed(r);r.State.Player.Water=unlock>=1;r.State.Player.Ocean=unlock==2;bool permitted=terrain==T.water?unlock>=1:unlock==2;
    Check(Options(r)==permitted&&Command(r)==permitted,"plain raft follows exact actual owner shallow/deep unlocks");Check(!UnitDataExtensions.HasAbility(r.Tile.unit!,Polytopia.Data.UnitAbility.Type.fly),"research never restores flight");
   });
   foreach(bool moved in new[]{false,true})foreach(bool attacked in new[]{false,true})Test($"allowance-{moved}-{attacked}",()=>{
    var r=Make(moved:moved,attacked:attacked);Shed(r);var u=r.Tile.unit!;Check(u.moved==moved&&u.attacked==attacked&&u.passengerUnit!.moved==moved&&u.passengerUnit.attacked==attacked,"shed preserves native allowance on root and cargo");
    Check(!Options(r),"existing readiness cannot bypass missing water research");
   });
   foreach(var fault in new[]{"type","data","tile","counter","ledger","validation"})Test("failed-shed-"+fault,()=>{
    var r=Make();var intent=Offer(r);string before=Snapshot(r);Fault.At=fault;Check(!NativeExchangeCommit.TryCommit(r.State,intent,out _,out _),"injected failure must reject");Check(Snapshot(r)==before,"failed transaction restores native and ledger state");Check(Options(r)&&UnitDataExtensions.HasAbility(r.Original,Polytopia.Data.UnitAbility.Type.fly),"failed shed retains legitimate preexisting flight");
   });
   foreach(var terrain in new[]{T.field,T.water})Test("genuine-dragon-shed-"+terrain,()=>{
    var r=Make(terrain,"warrior","firedragon");var real=r.Original;var data=real.UnitData;Shed(r);
    Check(ReferenceEquals(r.Tile.unit,real)&&ReferenceEquals(real.UnitData,data)&&real.type==U.firedragon&&real.health==70&&real.passengerUnit==null,"genuine Dragon keeps actual body and actual wounds");
    Check(UnitDataExtensions.HasAbility(real,Polytopia.Data.UnitAbility.Type.fly)&&Options(r)&&Command(r),"genuine real flight is never borrowed or removed");Check(!Row(r).Independent&&Row(r).Appearance==null,"only the genuine disguise is removed");
   });
   Test("genuine-warrior-never-borrows-dragon-flight",()=>{
    var r=Make(T.field,"firedragon","warrior");Check(!UnitDataExtensions.HasAbility(r.Original,Polytopia.Data.UnitAbility.Type.fly),"genuine Warrior has no Fly while masked");Check(!Command(r,false),"native illegal Warrior route is not granted");Shed(r);Check(r.Original.type==U.warrior&&r.Original.UnitData.movement==1&&!UnitDataExtensions.HasAbility(r.Original,Polytopia.Data.UnitAbility.Type.fly),"genuine Warrior shedding changes no movement profile");
   });
   foreach(bool city in new[]{false,true})Test("land-and-city-shed-"+city,()=>{
    var r=Make(city?T.water:T.field);r.Tile.City=city;Shed(r);Check(r.Tile.unit!.type==U.dcp_mimic&&r.Tile.unit.passengerUnit==null&&Row(r).Transport==null,"ordinary land/native-city endpoint stays naked land body");Check(r.Tile.unit.UnitData.movement==1&&!UnitDataExtensions.HasAbility(r.Tile.unit,Polytopia.Data.UnitAbility.Type.fly),"naked land body loses borrowed movement");
   });
   Test("exposed-boat-lands-as-original-naked-mimic",()=>{
    var r=Make();Shed(r);var original=r.Original;r.Tile.IsWater=false;r.Tile.terrain=T.field;var a=new DisembarkAction{Coordinates=r.Tile.coordinates};NativeMimicTransport.ActionScope? scope=null;
    try{Check(!NativeMimicTransport.BeforeDisembark(a,r.State,out scope)&&scope is {Completed:true},"production Mimic landing completes");}finally{NativeMimicTransport.Finished(null,scope);}
    Check(ReferenceEquals(r.Tile.unit,original)&&original.type==U.dcp_mimic&&original.UnitData.movement==1&&Row(r).Transport==null,"original cargo identity returns with its own ground movement");Check(!UnitDataExtensions.HasAbility(original,Polytopia.Data.UnitAbility.Type.fly)&&Options(r),"landing cannot restore old Dragon movement");
   });
   foreach(bool swap in new[]{false,true})Test("stranded-boat-regroups-into-flight-swap-"+swap,()=>{
    var r=Make();Shed(r);Check(!Options(r),"exposed boat begins stranded with no water research");
    var donor=new UnitState{id=9,type=U.firedragon,UnitData=RecruitmentHooks.Definitions["firedragon"],health=200,moved=true,attacked=true,coordinates=new(3,3)};
    var donorTile=new TileData{coordinates=donor.coordinates,terrain=T.water,IsWater=true,unit=donor};r.State.Map.Tiles.Add(donorTile);r.State.CurrentUnitId=9;
    var snap=NativeGameStateEnvelope.Get(r.State)!;var ledger=Ledger(r);var donorRow=new LedgerUnit(10,9,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,10);
    ledger=ledger with{NextLogicalId=11,Units=ledger.Units.Append(donorRow).ToArray()};NativeGameStateEnvelope.Set(r.State,snap.Revision,NativeIdentityLedgerCodec.Encode(ledger,snap.Revision));
    var source=NativeLedgerState.Capture(r.State,new(donor,true),"firedragon",false);var row=Row(r);var at=r.Tile.unit!.coordinates;
    var intent=new NativeExchangeIntent(ledger.Epoch,ledger.NextSequence,snap.Revision,r.State.CurrentTurn,1,NativeExchangeKind.Regroup,swap,NativePartnerReference.Existing,ledger.NextLogicalId,new(row.LogicalId,row.NativeId,at.X,at.Y),new(donorRow.LogicalId,donorRow.NativeId,3,3),NativeExchangeIntentCodec.AppearanceFingerprint(source));
    Check(NativeExchangeCommit.TryCommit(r.State,intent,out _,out string why),why);var finalTile=swap?donorTile:r.Tile;var final=finalTile.unit!;var finalRig=new Rig(r.State,r.Original,finalTile);
    Check(final==r.Original&&final.type==U.dcp_mimic_form_firedragon&&final.passengerUnit==null&&Row(r).Transport==null,"new flying disguise restores original single-body identity without boat cargo");
    Check(UnitDataExtensions.HasAbility(final,Polytopia.Data.UnitAbility.Type.fly)&&Options(finalRig),"legitimate new Regroup restores borrowed water traversal without research");
    Check(final.moved&&final.attacked&&donor.moved&&donor.attacked,"Regroup spends only the Mimic action and does not refresh exhausted donor");
    Check(MimicPresentationState.TryProject(final,out var projected)&&projected.ActualMimicBoat&&projected.NativeId=="firedragon"&&!projected.Exposed,"actual over-water boat artwork and public Dragon resume independently");
   });
   foreach(var denied in new[]{"wrong-state","untracked","foreign-owner","dead","stale-turn"})Test("denial-"+denied,()=>{
    var r=Make();var intent=Offer(r);switch(denied){case"wrong-state":NativeRegroupSession.Admitted=false;break;case"untracked":NativeGameStateEnvelope.Frames.Clear();break;case"foreign-owner":r.Original.owner=2;break;case"dead":r.Original.health=0;break;case"stale-turn":r.State.CurrentTurn++;break;}
    var type=r.Original.type;Check(!NativeExchangeCommit.TryCommit(r.State,intent,out _,out _),"unauthorized/stale shed must fail");Check(r.Tile.unit==r.Original&&r.Original.passengerUnit==null&&r.Original.type==type,"rejected shed creates no water wrapper");
   });
   Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases.Count,assertions=Assertions,tests=Cases,nativeGame="NOT RUN"}));return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
