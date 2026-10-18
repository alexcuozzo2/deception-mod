using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static partial class BlackmailTests
 {
  static void UnitBlackmailTests()
  {
   void Reject(GameState s,byte actor,UnitState unit,string why)
   {int bank=P(s,actor).Currency;var before=Snap(s);Check(!NativeBlackmail.TryOffer(s,actor,unit,out _,out _),why);Check(P(s,actor).Currency==bank&&ReferenceEquals(before,Snap(s)),"rejected offer must not debit or publish");}
   TileData Move(GameState s,int index,int x,int y,byte territory=0)
   {var unit=Target(s,index);City(s,index).unit=null;var tile=new TileData{coordinates=new(x,y),owner=territory,City=false,unit=unit};unit.coordinates=tile.coordinates;s.Map.Tiles.Add(tile);return tile;}
   void Remap(GameState s,uint id,LedgerTransport? transport)
   {var l=Read(s);Write(s,l with{Units=l.Units.Select(r=>r.LogicalId==1?r with{NativeId=id,Transport=transport}:r).ToArray()});}
   Test("UNIT-BM-001-repeat-turn-stacks-at-flat-cost",()=>{
    var s=Make();Buy(s);Reject(s,1,Target(s),"same actor cannot buy twice in one turn");
    s.CurrentTurn++;Buy(s);var row=Read(s).Blackmails!.Single();
    Check(row.Layers==2&&row.CreatedTurn==4&&row.LastAppliedTurn==5&&row.UnitId==1&&P(s,1).Currency==16,"next turn adds exactly one paid layer to stable unit identity");
    Check(Read(s).Receipts.Length==2&&Read(s).Receipts.All(r=>r.City is null&&r.Affected.SequenceEqual(new ulong[]{1})),"each setup has its own exact target receipt");
    Reject(s,1,Target(s),"second layer retains same-turn purchase lock");
   });
   Test("UNIT-BM-002-other-actors-stack-on-the-same-unit",()=>{
    var s=Make();Buy(s);s.CurrentPlayer=3;Buy(s,3);var rows=NativeBlackmail.ForUnit(s,Target(s));
    Check(rows.Length==2&&rows.Sum(r=>r.Layers)==2&&rows.All(r=>r.UnitId==1&&r.Victim==2),"one unit supports independent actor contracts");
    Check(P(s,1).Currency==18&&P(s,3).Currency==18,"both actors pay their own setup costs");
    Reject(s,3,Target(s),"other actor's own same-turn repeat remains blocked");
   });
   Test("UNIT-BM-003-city-cap-across-all-actors-and-all-units",()=>{
    var s=Make();City(s).Income=6;Target(s,1).home=Target(s).home;Target(s,2).home=Target(s).home;Target(s,2).owner=2;
    foreach(byte actor in new byte[]{1,3,4}){s.CurrentPlayer=actor;Buy(s,actor,0);Buy(s,actor,1);}
    Check(NativeBlackmail.At(s,City(s)).Sum(r=>r.Layers)==6&&NativeBlackmail.All(s).Length==6,"six total contracts exhaust a shared six-star supporting city");
    Reject(s,4,Target(s,2),"unused target cannot bypass capacity consumed by other units and actors");
    s.CurrentTurn++;Reject(s,4,Target(s),"a new turn does not bypass home-city capacity");
    Check(Read(s).Blackmails!.Sum(r=>r.Layers)==6,"rejected seventh purchase leaves every layer intact");
   });
   foreach(string hidden in new[]{"fog","cloak"})Test("UNIT-BM-004-hidden-target-"+hidden,()=>{
    var s=Make();if(hidden=="fog")City(s).Visible.Remove(1);else Target(s).Hidden=true;
    Reject(s,1,Target(s),"must see the unit itself; explored ground is insufficient for hidden Cloak");
   });
   foreach(byte territory in new byte[]{0,1,3})Test("UNIT-BM-005-visible-target-unseen-home-territory-"+territory,()=>{
    var s=Make();var home=City(s);var tile=Move(s,0,9,9,territory);home.Visible.Remove(1);var unit=tile.unit!;
    Check(NativeBlackmail.TryOffer(s,1,unit,out var request,out var why),why);Check(Commit(s,request!,out _,out why),why);DrainEmbassies(s);
    var row=Read(s).Blackmails!.Single();Check(row.Victim==2&&row.X==2&&row.Y==3&&row.UnitId==1,"unit owner/home—not occupied territory—binds the obligation");
    Check(P(s,1).Currency==18&&!home.GetExplored(1),"unseen supporting city need not be discovered or revealed by the purchase");
   });
   foreach(string home in new[]{"missing","unset-home","not-city","neutral-city","foreign-city","zero-income","negative-income"})Test("UNIT-BM-006-ineligible-home-"+home,()=>{
    var s=Make();switch(home){case"missing":Target(s).home=new(31,31);break;case"unset-home":Target(s).home=default;break;case"not-city":City(s).City=false;break;case"neutral-city":City(s).owner=0;break;case"foreign-city":City(s).owner=3;break;case"zero-income":City(s).Income=0;break;case"negative-income":City(s).Income=-8;break;}
    Reject(s,1,Target(s),"target needs a real owned city with surplus native production");
   });
   Test("UNIT-BM-006b-real-origin-city-at-zero-coordinate-is-valid",()=>{
    var s=Make();City(s).coordinates=new(0,0);Target(s).coordinates=new(0,0);Target(s).home=new(0,0);Buy(s);
    Check(Read(s).Blackmails!.Single().X==0&&Read(s).Blackmails![0].Y==0,"a real city at map origin is not confused with missing home");
   });
   Test("UNIT-BM-007-income-shrink-pauses-layers-and-regrowth-restores",()=>{
    var s=Make();Buy(s);s.CurrentTurn=5;Buy(s);var bank=P(s,1).Currency;
    City(s).Income=1;Check(NativeBlackmail.All(s).Single().Layers==1&&Read(s).Blackmails!.Single().Layers==2,"production loss caps effective payout without erasing the paid contract");
    Reject(s,1,Target(s),"capacity loss cannot unlock a same-turn purchase");
    City(s).Income=0;Check(NativeBlackmail.All(s).Length==0&&NativeBlackmail.Contracts(s).Single().Layers==2&&NativeBlackmail.ContractsAt(s,City(s)).Single().Layers==2&&Read(s).Blackmails!.Single().Layers==2,"zero production retains public compromise contracts while pausing payment");
    s.CurrentPlayer=2;P(s,2).Currency=0;Marker(s);Check(P(s,2).Currency==0&&Read(s).Blackmails!.Single().LastPaidTurn==5,"zero payout settles once without negative treasury");
    City(s).Income=6;s.CurrentPlayer=1;Check(NativeBlackmail.All(s).Single().Layers==2&&P(s,1).Currency==bank,"regrowth restores existing paid layers without charging again");
    Reject(s,1,Target(s),"restoring production does not clear the last-applied turn");
    s.CurrentTurn=6;Marker(s);Check(P(s,1).Currency==bank+2,"next own turn receives the restored two-star income");
   });
   Test("UNIT-BM-008-income-shrink-shared-priority-remains-bounded",()=>{
    var s=Make();Target(s,1).home=Target(s).home;Buy(s);Buy(s,1,1);s.CurrentPlayer=3;Buy(s,3);City(s).Income=2;
    var active=NativeBlackmail.All(s);Check(active.Sum(r=>r.Layers)==2&&Read(s).Blackmails!.Sum(r=>r.Layers)==3,"all actors/units share the reduced capacity");
    Check(active.All(r=>r.Blackmailer==1),"same-created-turn capacity ordering is deterministic by actor then unit");
    City(s).Income=6;Check(NativeBlackmail.All(s).Sum(r=>r.Layers)==3,"paused actor resumes with original purchase preserved");
   });
   Test("UNIT-BM-009-rogue-commander-does-not-reassign-owner-or-home",()=>{
    var s=Make();Buy(s);var unit=Target(s);unit.owner=3;var l=Read(s);Write(s,l with{Units=l.Units.Select(r=>r.LogicalId==1?r with{Rogue=new LedgerRogue(2,3,4)}:r).ToArray()});
    Check(NativeBlackmail.All(s).Single().Victim==2&&ReferenceEquals(NativeBlackmail.ResolveUnit(s,Read(s).Blackmails![0]),unit),"temporary command preserves debtor and physical target");
    s.CurrentTurn=5;Buy(s);Check(Read(s).Blackmails!.Single().Layers==2&&P(s,1).Currency==16,"third party can blackmail original enemy despite temporary commander");
    s.CurrentPlayer=2;Marker(s);Check(P(s,2).Currency==18&&P(s,3).Currency==20,"permanent owner pays; Rogue commander does not");
    unit.owner=2;l=Read(s);Write(s,l with{Units=l.Units.Select(r=>r.LogicalId==1?r with{Rogue=null}:r).ToArray()});
    Check(NativeBlackmail.All(s).Single().Layers==2,"returning Rogue retains its contracts");
   });
   Test("UNIT-BM-010-own-rogue-is-not-an-enemy-target",()=>{
    var s=Make();City(s).owner=1;Target(s).owner=3;var l=Read(s);Write(s,l with{Units=l.Units.Select(r=>r.LogicalId==1?r with{Rogue=new LedgerRogue(1,3,4)}:r).ToArray()});
    Reject(s,1,Target(s),"temporary foreign commander cannot turn one's own unit into a Blackmail victim");
   });
   foreach(string loss in new[]{"death","disband","zero-health","conversion","home-transfer"})Test("UNIT-BM-011-lien-ends-"+loss,()=>{
    var s=Make();Buy(s);var target=Target(s);switch(loss){case"death":case"disband":City(s).unit=null;break;case"zero-health":target.health=0;break;case"conversion":target.owner=3;break;case"home-transfer":target.home=City(s,1).coordinates;break;}
    Check(NativeBlackmail.All(s).Length==0&&NativeBlackmail.ForUnit(s,target).Length==0,"read-side income and unit marks stop immediately before reconciliation");
    Marker(s,1,CommandType.Build);Check(Read(s).Blackmails!.Length==0,"next ordinary command completion durably removes inactive contract");
    s.CurrentTurn++;Marker(s);Check(P(s,1).Currency==18,"dead/disbanded/reassigned unit cannot produce another star");
   });
   foreach(byte converter in new byte[]{1,3})Test("UNIT-BM-011b-permanent-conversion-clears-all-layers-"+converter,()=>{
    var s=Make();Buy(s);s.CurrentPlayer=3;Buy(s,3);s.CurrentTurn++;Buy(s,3);s.CurrentPlayer=1;Buy(s);
    var target=Target(s);var before=Snap(s);var expected=NativeIdentityLedgerCodec.Encode(Read(s) with{Blackmails=Array.Empty<LedgerBlackmail>()},before.Revision);
    Check(Read(s).Blackmails!.Length==2&&Read(s).Blackmails!.All(r=>r.Layers==2),"two blackmailers each hold two layers before conversion");
    target.owner=converter;
    Check(NativeBlackmail.All(s).Length==0&&NativeBlackmail.Contracts(s).Length==0&&NativeBlackmail.ForUnit(s,target).Length==0&&NativeBlackmail.At(s,City(s)).Length==0,"permanent conversion immediately removes income and public indicators for every layer and blackmailer");
    Check(ReferenceEquals(before,Snap(s))&&Read(s).Blackmails!.Sum(r=>r.Layers)==4,"read-side filtering does not mutate the pending native conversion ledger");
    Marker(s,1,CommandType.Build);
    Check(Read(s).Blackmails!.Length==0&&Snap(s).Revision==before.Revision&&Snap(s).Bytes().SequenceEqual(expected),"ordinary command completion durably removes all converted-unit contracts without changing purchase history");
    Check(P(s,1).Currency==16&&P(s,3).Currency==16&&P(s,2).Currency==20,"conversion neither refunds setup fees nor transfers another income payment");
    var saved=Snap(s);var loaded=Make();loaded.CurrentTurn=s.CurrentTurn;
    foreach(var p in loaded.Players)p.Currency=P(s,p.Id).Currency;
    NativeGameStateEnvelope.Set(loaded,saved.Revision,saved.Bytes());
    Check(Target(loaded).owner==2&&NativeBlackmail.All(loaded).Length==0,"restoring the original owner after reload cannot revive removed contracts");
    Marker(loaded,converter,CommandType.Build);loaded.CurrentTurn++;
    foreach(byte actor in new byte[]{1,2,3}){loaded.CurrentPlayer=actor;Marker(loaded);}
    Check(Read(loaded).Blackmails!.Length==0&&P(loaded,1).Currency==16&&P(loaded,3).Currency==16&&P(loaded,2).Currency==20,"convert-back and all affected players' later turns remain free of the old income and deductions");
   });
   Test("UNIT-BM-012-wrapper-and-passenger-remap-retains-logical-contract",()=>{
    var s=Make();Buy(s);var land=Target(s);var row=Read(s).Blackmails!.Single();
    var boat=new UnitState{id=201,owner=2,coordinates=land.coordinates,home=new(31,31),passengerUnit=land};City(s).unit=boat;
    Check(ReferenceEquals(NativeBlackmail.ResolveUnit(s,row),boat)&&NativeBlackmail.All(s).Single().UnitId==1,"pre-reconcile boarding follows exact passenger into root; ignores wrapper home");
    Marker(s,1,CommandType.Build);Check(Read(s).Blackmails!.Length==1,"boarding command cannot prune the passenger's lien");
    Remap(s,201,new LedgerTransport(101));Check(NativeBlackmail.ForUnit(s,boat).Single().UnitId==1,"reconciled wrapper presents the original logical lien");
    City(s).unit=land;Check(ReferenceEquals(NativeBlackmail.ResolveUnit(s,row),land)&&NativeBlackmail.All(s).Length==1,"pre-reconcile landing resolves exact recorded passenger after wrapper disappears");
    Marker(s,1,CommandType.Build);Remap(s,101,null);Check(NativeBlackmail.ForUnit(s,land).Single().UnitId==1,"landing retains rather than duplicates contract");
    s.CurrentTurn++;Marker(s);Check(P(s,1).Currency==19,"one surviving contract pays exactly once after both transitions");
   });
   foreach(string stale in new[]{"home","native-id","income","hidden-cloak","target-health","target-position","unit-owner"})Test("UNIT-BM-013-stale-target-atomic-"+stale,()=>{
    var s=Make();var request=Offer(s);switch(stale){case"home":Target(s).home=City(s,1).coordinates;break;case"native-id":Target(s).id=999;break;case"income":City(s).Income++;break;case"hidden-cloak":Target(s).Hidden=true;break;case"target-health":Target(s).health=0;break;case"target-position":Move(s,0,9,9);break;case"unit-owner":Target(s).owner=3;break;}
    var before=Snap(s);Check(!Commit(s,request,out _,out _),"stale unit/city preimage must be rejected");
    Check(P(s,1).Currency==20&&ReferenceEquals(before,Snap(s))&&Read(s).Blackmails!.Length==0,"failed stale commitment cannot debit or publish");
   });
   Test("UNIT-BM-014-layer-save-load-purchase-and-payment-idempotence",()=>{
    var s=Make();Buy(s);s.CurrentTurn=5;var request=Buy(s);s.CurrentPlayer=2;Marker(s);var saved=Snap(s);
    var restored=Make();restored.CurrentTurn=5;restored.CurrentPlayer=2;foreach(var p in restored.Players)p.Currency=P(s,p.Id).Currency;NativeGameStateEnvelope.Set(restored,saved.Revision,saved.Bytes());
    Marker(restored);Check(P(restored,2).Currency==18&&Read(restored).Blackmails!.Single().Layers==2,"loaded payment stamp suppresses duplicate stacked debit");
    restored.CurrentPlayer=1;Reject(restored,1,Target(restored),"loaded last-applied turn prevents another purchase");
    var before=Snap(restored);Check(Commit(restored,request,out var replay,out _)&&replay!.AlreadyCommitted&&ReferenceEquals(before,Snap(restored))&&P(restored,1).Currency==16,"old exact purchase retry neither buys nor charges a third layer");
    restored.CurrentTurn=6;Marker(restored);Check(P(restored,1).Currency==18,"next loaded own turn collects both persistent layers");
   });
   foreach(int killed in new[]{0,1})Test("UNIT-BM-015-native-boundary-reconciles-death-"+killed,()=>{
    var s=Make();Buy(s);s.StrictLedgerBoundary=true;City(s,killed).unit=null;var prior=Snap(s);bool rejected=false;
    try{NativeLedgerState.Validate(s,prior.Revision,prior.Bytes());}catch(System.IO.InvalidDataException){rejected=true;}
    Check(rejected,"native-style validator rejects a live ledger identity whose body was removed");
    Marker(s,1,CommandType.Build);
    Check(s.PreparedBoundaries==1&&!Read(s).Units.Single(r=>r.LogicalId==(ulong)killed+1).Alive,"completed action reconciles the deleted body before Blackmail publishes");
    Check(Read(s).Blackmails!.Length==(killed==0?0:1)&&Snap(s).Revision==prior.Revision&&P(s,1).Currency==18,"target death removes lien; unrelated death keeps it, without extra command revision or charge");
   });
   Test("UNIT-BM-016-native-boundary-reconciles-boarding-and-landing",()=>{
    var s=Make();Buy(s);s.StrictLedgerBoundary=true;var land=Target(s);var prior=Snap(s);
    var boat=new UnitState{id=201,owner=land.owner,coordinates=land.coordinates,home=new(31,31),passengerUnit=land};City(s).unit=boat;
    bool rejected=false;try{NativeLedgerState.Validate(s,prior.Revision,prior.Bytes());}catch(System.IO.InvalidDataException){rejected=true;}
    Check(rejected,"native-style validation refuses ledger identity pointing at a passenger instead of root");
    Marker(s,1,CommandType.Build);var row=Read(s).Units.Single(r=>r.LogicalId==1);
    Check(row.NativeId==201&&row.Transport?.PassengerNativeId==101&&Read(s).Blackmails!.Single().UnitId==1,"boarding remaps root/cargo before cleanup, preserving logical contract");
    City(s).unit=land;prior=Snap(s);rejected=false;
    try{NativeLedgerState.Validate(s,prior.Revision,prior.Bytes());}catch(System.IO.InvalidDataException){rejected=true;}
    Check(rejected,"native-style validation refuses the missing old wrapper after landing");
    Marker(s,1,CommandType.Build);row=Read(s).Units.Single(r=>r.LogicalId==1);
    Check(row.NativeId==101&&row.Transport is null&&s.PreparedBoundaries==2&&Read(s).Blackmails!.Single().UnitId==1,"landing restores exact passenger lineage before Blackmail validation");
    Check(Snap(s).Revision==prior.Revision&&P(s,1).Currency==18,"physical transitions neither charge nor create feature revisions");
   });
   Test("UNIT-BM-017-stale-epoch-cannot-trigger-reconciliation",()=>{
    var s=Make();Buy(s);s.StrictLedgerBoundary=true;var scope=Start(s);
    Write(s,Read(s) with{Epoch=Guid.NewGuid()});var before=Snap(s);
    Check(NativeBlackmail.Finished(null,scope)is null&&s.PreparedBoundaries==0,"completed old-match scope cannot reconcile a replacement ledger");
    Check(ReferenceEquals(before,Snap(s))&&P(s,1).Currency==18,"foreign epoch remains untouched");
   });
  }
 }
}
