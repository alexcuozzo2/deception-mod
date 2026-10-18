using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using U=Polytopia.Data.UnitData.Type;
namespace Deception.Tests {
 public static class ShedTests {
 static int Assertions;static readonly List<string> Cases=new();static void Check(bool b,string why){Assertions++;if(!b)throw new Exception(why);}static void Test(string n,Action body){try{body();Cases.Add(n);}catch(Exception e){throw new Exception(n,e);}}
 sealed record Rig(GameState State,UnitState Unit,TileData Tile);
 static LedgerAppearance Art(string f,ushort hp=100)=>new(f,new("Elyrion","DarkElf"),new("Imperius","Default"),new("Elyrion","DarkElf"),57,hp,hp,false,ApparentConcealment.Visible,-1,new[]{"Poison"});
 static Rig Make(string real="mimic",string form="warrior",bool water=false,bool invisible=false){
  Fault.At="";RecruitmentHooks.Reset();NativeGameStateEnvelope.Frames.Clear();NativeRegroupSession.Enabled=NativeRegroupSession.Admitted=true;NativeRegroupSession.Faults.Clear();
  var s=new GameState();NativeRegroupSession.Owned=s;var tile=new TileData{IsWater=water,coordinates=new(2,3)};s.Map.Tiles.Add(tile);
  bool mimic=real=="mimic";string type=mimic?RecruitmentCatalog.Variant(form):real;var d=RecruitmentHooks.Definitions[type];var u=new UnitState{id=7,type=d.type,UnitData=d,coordinates=tile.coordinates,health=mimic?(ushort)10:(ushort)70,moved=true,attacked=true};
  u.effects.Add(UnitEffect.Frozen);u.effects.Add(UnitEffect.Poisoned);u.effects.Add(UnitEffect.DoubleReady);if(invisible)u.effects.Add(UnitEffect.Invisible);tile.unit=u;
  if(NativeMimicTransport.IsVessel(u)){var cargo=RecruitmentHooks.Definitions[mimic?RecruitmentCatalog.Variant("warrior"):"warrior"];u.passengerUnit=new UnitState{id=6,type=cargo.type,UnitData=cargo,health=mimic?(ushort)10:(ushort)65,moved=true,attacked=true};}
  var art=Art(form);if(RecruitmentCatalog.NavalCopyOnly.Contains(form))art=art with{Passenger=Art("warrior")};if(invisible)art=art with{Concealment=ApparentConcealment.Hidden,Effects=new[]{"Invisible","Poison"}};
  var row=new LedgerUnit(8,7,true,mimic?LedgerActualKind.Mimic:LedgerActualKind.Genuine,true,art,PaidInvestmentKind.Known,13,u.passengerUnit is null?null:new(6));
  var ledger=new NativeIdentityLedger(Guid.Parse("a3ca461f-fd6b-44be-8b87-d4483d12f50b"),9,1,new[]{row},Array.Empty<LedgerReceipt>());NativeGameStateEnvelope.Set(s,4,NativeIdentityLedgerCodec.Encode(ledger,4));return new(s,u,tile);
 }
 static NativeIdentityLedger Read(Rig r){var p=NativeGameStateEnvelope.Get(r.State)!;return NativeIdentityLedgerCodec.Decode(p.Bytes(),p.Revision);}
 static void Change(Rig r,Func<LedgerUnit,LedgerUnit> f){var p=NativeGameStateEnvelope.Get(r.State)!;var l=Read(r);NativeGameStateEnvelope.Set(r.State,p.Revision,NativeIdentityLedgerCodec.Encode(l with{Units=l.Units.Select(f).ToArray()},p.Revision));}
 static NativeExchangeIntent Offer(Rig r){Check(NativeShedDisguise.TryOffer(r.State,7,out var i,out string why),why);return i!;}
 static bool Commit(Rig r,NativeExchangeIntent i,out NativeExchangeCommitResult? result,out string why)=>NativeExchangeCommit.TryCommit(r.State,i,out result,out why);
 static string Snapshot(Rig r)=>TransportStateDump.Dump(r.State)+"/"+Convert.ToBase64String(NativeGameStateEnvelope.Get(r.State)!.Bytes());
 public static int Run(){try{
 foreach(string real in new[]{"warrior","cloak","cloak_boat","firedragon","dcp_spy","dcp_spymaid"})foreach(bool hidden in new[]{false,true})Test("genuine-"+real+"-hidden-"+hidden,()=>{
  var r=Make(real,real=="cloak_boat"?"transportship":"warrior",real=="cloak_boat",hidden);var before=r.Unit;var type=before.type;var data=before.UnitData;var cargo=before.passengerUnit;int stars=r.State.Stars;var i=Offer(r);
  Check(Commit(r,i,out var done,out string why),why);var row=Read(r).Units.Single();Check(!row.Independent&&row.Appearance==null&&row.PaidStars==13,"genuine identity unmasked without changing investment");
  Check(ReferenceEquals(before,r.Tile.unit)&&before.type==type&&ReferenceEquals(before.UnitData,data)&&ReferenceEquals(before.passengerUnit,cargo),"genuine native body/profile/cargo unchanged");
  Check(before.health==70&&before.moved&&before.attacked&&before.xp==4&&r.State.Stars==stars,"native health/allowance/currency retained");
  Check(before.effects.Contains(UnitEffect.Invisible)==(hidden&&(real=="cloak"||real=="cloak_boat")),"only native actual Cloak readiness retained");Check(before.effects.Contains(UnitEffect.DoubleReady)&&before.effects.Contains(UnitEffect.Frozen),"other native statuses retained");
  string after=Snapshot(r);Check(Commit(r,i,out done,out why)&&done!.AlreadyCommitted&&Snapshot(r)==after,"exact duplicate is receipt no-op");Check(!NativeShedDisguise.TryOffer(r.State,7,out _,out _),"unmasked genuine offers no second shed");
 });
 foreach(string species in new[]{"dcp_spy","dcp_spymaid"})foreach(string fault in new[]{"ledger","validation"})Test("A50-hidden-spy-shed-rollback-"+species+"-"+fault,()=>{
  var r=Make(species,invisible:true);var intent=Offer(r);var before=Snapshot(r);var effects=r.Unit.effects;Fault.At=fault;
  Check(!Commit(r,intent,out _,out _),"failed Spy shed refused");Check(Snapshot(r)==before&&ReferenceEquals(effects,r.Unit.effects)&&r.Unit.effects.Contains(UnitEffect.Invisible),"failed free ability preserves hidden native body and mask");
 });
 foreach(string form in new[]{"warrior","firedragon","transportship","scoutship","bombership"})foreach(bool water in new[]{false,true})Test("mimic-"+form+"-water-"+water,()=>{
  var r=Make(form:form,water:water,invisible:true);var i=Offer(r);var old=r.Unit;var row=Read(r).Units.Single();int stars=r.State.Stars;
  Check(Commit(r,i,out var result,out string why),why);var next=Read(r).Units.Single();var actual=r.Tile.unit!;
  Check(next.LogicalId==row.LogicalId&&next.Kind==LedgerActualKind.Mimic&&next.Independent&&next.PaidStars==row.PaidStars,"same actual Mimic identity/investment");
  Check(NativeShedDisguise.Exposed(next.Appearance),"public appearance naked Mimic");Check(actual.health==10&&actual.UnitData.health==10&&actual.UnitData.attack==0,"true one-HP zero-attack body");
  Check(actual.moved&&actual.attacked&&r.State.Stars==stars&&actual.effects.Contains(UnitEffect.DoubleReady)&&actual.effects.Contains(UnitEffect.Frozen),"shed never refreshes allowance or spends stars/status");
  Check(!actual.effects.Contains(UnitEffect.Invisible),"borrowed invisibility removed from naked body");
  if(water){Check(next.Appearance!.Form=="transportship"&&next.Appearance.Passenger!.Form==RecruitmentCatalog.MenuId,"water has plain Raft with exposed Mimic passenger");Check(actual.passengerUnit?.type==U.dcp_mimic&&next.Transport?.PassengerNativeId==actual.passengerUnit.id,"actual cargo refit matches exposed appearance");}
  else Check(actual.type==U.dcp_mimic&&actual.passengerUnit==null&&next.Appearance!.Form==RecruitmentCatalog.MenuId&&next.Transport==null,"land has naked single-body Mimic");
  Check(!NativeShedDisguise.TryOffer(r.State,next.NativeId,out _,out _),"exposed Mimic cannot repeatedly shed");
  string after=Snapshot(r);Check(Commit(r,i,out result,out why)&&result!.AlreadyCommitted&&Snapshot(r)==after,"repeat effect-free");
 });
 foreach(string fault in new[]{"type","data","tile","counter","ledger","validation"})Test("rollback-"+fault,()=>{
  var r=Make(form:"transportship",water:true);var i=Offer(r);var before=Snapshot(r);Fault.At=fault;Check(!Commit(r,i,out _,out _),"fault must reject");Check(Snapshot(r)==before,"atomic rollback of body/cargo/ledger/counter");
 });
 foreach(string bad in new[]{"turn","actor","owner","moved","mask","dead","linked","unexplored","not-owned","ended","wrong-root"})Test("reject-"+bad,()=>{
  var r=Make();var i=Offer(r);switch(bad){case "turn":r.State.CurrentTurn++;break;case "actor":r.State.CurrentPlayer=2;break;case "owner":r.Unit.owner=2;break;case "moved":r.Unit.coordinates=new(0,0);break;case "mask":Change(r,row=>row with{Appearance=row.Appearance! with{SourceColor=8}});break;case "dead":r.Unit.health=0;break;case "linked":r.Unit.leader=2;break;case "unexplored":r.Tile.Explored=false;break;case "not-owned":NativeRegroupSession.Admitted=false;break;case "ended":r.State.CurrentState=GameState.State.Ended;break;case "wrong-root":r.Tile.unit=null;break;}
  string before=Snapshot(r);Check(!Commit(r,i,out _,out _),"changed precondition denied");Check(Snapshot(r)==before,"denial no mutation");
 });
 Test("city-water-tile-native-city-exception",()=>{var r=Make(form:"firedragon",water:true);r.Tile.City=true;var i=Offer(r);Check(Commit(r,i,out _,out var why),why);Check(r.Tile.unit!.type==U.dcp_mimic&&Read(r).Units.Single().Transport==null,"native city endpoint remains land body");});
 foreach(string real in new[]{"warrior","mimic"})foreach(bool water in new[]{false,true})foreach(bool moved in new[]{false,true})foreach(bool attacked in new[]{false,true})Test($"allowance-{real}-water-{water}-moved-{moved}-attacked-{attacked}",()=>{
  var r=Make(real,form:"firedragon",water:water);r.Unit.moved=moved;r.Unit.attacked=attacked;var i=Offer(r);
  Check(Commit(r,i,out _,out var why),why);var body=r.Tile.unit!;
  Check(body.moved==moved&&body.attacked==attacked,"shed preserves each original movement/attack flag combination");
  Check(body.effects.Contains(UnitEffect.DoubleReady)&&body.effects.Contains(UnitEffect.Frozen),"readiness/status effects remain native");
  if(body.passengerUnit is UnitState cargo)Check(cargo.moved==moved&&cargo.attacked==attacked,"new exposed cargo keeps its true allowance");
 });
 foreach(string source in new[]{"warrior","firedragon","transportship","bombership"})Test("exposed-mimic-landing-from-"+source,()=>{
  var r=Make(form:source,water:true);var i=Offer(r);Check(Commit(r,i,out _,out var why),why);var boat=r.Tile.unit!;var cargo=boat.passengerUnit!;uint counter=r.State.CurrentUnitId;ulong logical=Read(r).Units.Single().LogicalId;int stars=r.State.Stars;
  r.Tile.IsWater=false;var action=new DisembarkAction{PlayerId=1,Coordinates=r.Tile.coordinates};NativeMimicTransport.ActionScope? scope=null;
  try {Check(!NativeMimicTransport.BeforeDisembark(action,r.State,out scope),"owned Mimic landing uses private transport adapter");Check(scope is not null&&scope.Completed,"landing committed exactly once");}
  finally {NativeMimicTransport.Finished(null,scope);}
  var row=Read(r).Units.Single();
  Check(ReferenceEquals(cargo,r.Tile.unit)&&cargo.type==U.dcp_mimic&&cargo.passengerUnit==null,"landing restores exact exposed cargo object");
  Check(row.LogicalId==logical&&row.NativeId==cargo.id&&row.Transport==null&&row.Appearance!.Form==RecruitmentCatalog.MenuId,"stable identity now binds naked land root");
  Check(cargo.health==10&&cargo.UnitData.health==10&&cargo.UnitData.attack==0&&cargo.moved&&cargo.attacked,"native landing costs stay exhausted with true Mimic stats");
  Check(counter==r.State.CurrentUnitId&&stars==r.State.Stars,"landing does not allocate or charge again");
 });
 foreach(string change in new[]{"epoch","sequence","revision","swap","partner"})Test("malformed-request-"+change,()=>{
  var r=Make();var i=Offer(r);i=change switch{"epoch"=>i with{Epoch=Guid.NewGuid()},"sequence"=>i with{Sequence=i.Sequence+1},"revision"=>i with{Revision=i.Revision+1},"swap"=>i with{Swap=true},"partner"=>i with{Partner=i.Partner with{NativeId=9}},_=>i};
  string before=Snapshot(r);Check(!Commit(r,i,out _,out _),"forged or stale complete intent is rejected");Check(Snapshot(r)==before,"invalid request leaves state untouched");
 });
 Test("water-wrapper-reserves-logical-allocator-before-next-intent",()=>{
  var r=Make(form:"firedragon",water:true);r.State.CurrentUnitId=100;
  var second=new UnitState{id=8,type=U.warrior,UnitData=RecruitmentHooks.Definitions["warrior"],health=70,coordinates=new(3,3)};r.State.Map.Tiles.Add(new TileData{coordinates=second.coordinates,unit=second});
  var snap=NativeGameStateEnvelope.Get(r.State)!;var before=Read(r);var row=new LedgerUnit(9,8,true,LedgerActualKind.Genuine,true,Art("rider"),PaidInvestmentKind.Known,2);
  NativeGameStateEnvelope.Set(r.State,snap.Revision,NativeIdentityLedgerCodec.Encode(before with{NextLogicalId=10,Units=before.Units.Append(row).ToArray()},snap.Revision));
  var first=Offer(r);Check(Commit(r,first,out _,out var why),why);var after=Read(r);
  Check(r.Tile.unit!.id>100&&after.NextLogicalId==(ulong)r.State.CurrentUnitId+2,"new water root reserves logical identities above native counter immediately");
  Check(NativeShedDisguise.TryOffer(r.State,second.id,out var next,out why),why);Check(next!.NextLogicalId==after.NextLogicalId&&next.Sequence==first.Sequence+1,"following complete intent uses already-reserved allocator and next sequence");
  Check(Math.Max(after.NextLogicalId,(ulong)r.State.CurrentUnitId+2)==after.NextLogicalId,"normal reconciliation allocator floor cannot stale the following intent");
  Check(Commit(r,next,out _,out why),why);Check(Read(r).NextLogicalId==after.NextLogicalId&&Read(r).Units.Single(x=>x.NativeId==8).Appearance==null,"following single-unit intent commits without allocator churn");
 });
 foreach(bool propaganda in new[]{false,true})Test("exposed-boat-cannot-feign-new-mask-propaganda-"+propaganda,()=>{
  var r=Make(form:"firedragon",water:true);var i=Offer(r);Check(Commit(r,i,out _,out var why),why);r.State.Player.Propaganda=propaganda;
  foreach(var kind in NativeApparentUpgrades.Kinds)r.State.Player.Upgrades.Add(NativeApparentUpgrades.Form(kind));
  string before=Snapshot(r);foreach(var kind in NativeApparentUpgrades.Kinds)Check(!NativeApparentUpgrades.TryOffer(r.State,r.Tile.unit!.id,kind,out _,out _),"bare Mimic boat cannot invent a naval disguise");
  Check(Snapshot(r)==before,"refused upgrades neither charge nor change naked body/ledger");
 });
 Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases.Count,assertions=Assertions,tests=Cases,nativeGame="NOT RUN"}));return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
