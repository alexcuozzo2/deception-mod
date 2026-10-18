using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
    public static class UpgradeTests
    {
        static int Cases,Assertions;
        static void Check(bool ok,string why){Assertions++;if(!ok)throw new Exception(why);}
        static void Case(Action action){Cases++;action();}
        sealed record Rig(GameState State,UnitState Unit,TileData Tile);
        static LedgerAppearance Art(string form,ushort health=80,ushort maximum=100)=>new(form,new("Elyrion","DarkElf"),new("Imperius","Default"),new("Elyrion","DarkElf"),57,health,maximum,false,ApparentConcealment.Visible,-1,new[]{"Poison"});
        static Rig Make(string real="mimic")
        {
            Fault.At="";RecruitmentHooks.Reset();NativeGameStateEnvelope.Frames.Clear();NativeRegroupSession.Enabled=NativeRegroupSession.Admitted=true;NativeRegroupSession.Faults.Clear();
            var state=new GameState();NativeRegroupSession.Owned=state;
            foreach(string target in new[]{"scoutship","rammership","bombership"})state.Player.Upgrades.Add(target);
            var tile=new TileData{IsWater=true,coordinates=new(2,3)};state.Map.Tiles.Add(tile);
            bool mimic=real=="mimic";string type=mimic?RecruitmentCatalog.Variant("transportship"):real;
            var data=RecruitmentHooks.Definitions[type];var unit=new UnitState{id=7,type=data.type,UnitData=data,coordinates=tile.coordinates,health=mimic?(ushort)10:(ushort)70};
            unit.effects.Add(UnitEffect.Poisoned);unit.effects.Add(UnitEffect.Frozen);unit.effects.Add(UnitEffect.DoubleReady);tile.unit=unit;
            if(mimic||RecruitmentCatalog.NavalCopyOnly.Contains(real))
            {var cargoData=RecruitmentHooks.Definitions[mimic?RecruitmentCatalog.Variant("warrior"):"warrior"];unit.passengerUnit=new UnitState{id=6,type=cargoData.type,UnitData=cargoData,health=mimic?(ushort)10:(ushort)65,moved=true,attacked=true};}
            var apparent=Art("transportship",80,200) with{Passenger=Art("firedragon",150,200)};
            var row=new LedgerUnit(8,7,true,mimic?LedgerActualKind.Mimic:LedgerActualKind.Genuine,true,apparent,PaidInvestmentKind.Known,real=="bombership"?18:3,unit.passengerUnit is null?null:new(6));
            var ledger=new NativeIdentityLedger(Guid.Parse("a3ca461f-fd6b-44be-8b87-d4483d12f50b"),9,1,new[]{row},Array.Empty<LedgerReceipt>());
            NativeGameStateEnvelope.Set(state,4,NativeIdentityLedgerCodec.Encode(ledger,4));return new(state,unit,tile);
        }
        static NativeIdentityLedger Read(Rig r){var s=NativeGameStateEnvelope.Get(r.State)!;return NativeIdentityLedgerCodec.Decode(s.Bytes(),s.Revision);}
        static void Change(Rig r,Func<LedgerUnit,LedgerUnit> f){var old=NativeGameStateEnvelope.Get(r.State)!;var l=Read(r);NativeGameStateEnvelope.Set(r.State,old.Revision,NativeIdentityLedgerCodec.Encode(l with{Units=l.Units.Select(f).ToArray()},old.Revision));}
        static NativeExchangeIntent Offer(Rig r,NativeExchangeKind kind=NativeExchangeKind.UpgradeScout)
        {Check(NativeApparentUpgrades.TryOffer(r.State,r.Unit.id,kind,out var i,out var why),"Offer: "+why);return i!;}
        static bool Commit(Rig r,NativeExchangeIntent i,out NativeExchangeCommitResult? result,out string why)=>NativeExchangeCommit.TryCommit(r.State,i,out result,out why);
        static string Snapshot(Rig r)=>TransportStateDump.Dump(r.State)+Convert.ToHexString(NativeGameStateEnvelope.Get(r.State)!.Bytes());
        static BuildReaction Reaction(Rig r,NativeExchangeIntent intent)
        {
            GameManager.GameState=r.State;GameManager.LocalPlayer=r.State.Player;GameManager.Client=new();
            MapRenderer.Current=new(){Visual=new(){Data=r.Tile}};UpgradeAction.Executions=0;UpgradeReaction.Calls=0;UpgradeReaction.Mode="delay";UpgradeReaction.Pending=null;UpgradeReaction.Last=null;
            return new(){action=new(){Intent=intent}};
        }
        static InteractionBar Bar(Rig r)
        {
            NativeApparentUpgradeUi.Clear();MimicRegroupUi.Choosing=false;MimicPresentationUi.Allowed=true;
            GameManager.GameState=r.State;GameManager.LocalPlayer=r.State.Player;GameManager.Client=new();
            var bar=new InteractionBar{unit=new Unit{UnitState=r.Unit}};NativeApparentUpgradeUi.Sync(bar,bar.unit,true);return bar;
        }
        public static int Run()
        {
            try
            {
                foreach(string real in new[]{"mimic","firedragon","transportship","bombership","juggernaut"})foreach(var kind in NativeApparentUpgrades.Kinds)Case(()=>
                {
                    var r=Make(real);string before=TransportStateDump.Body(r.Unit);string? cargo=r.Unit.passengerUnit is null?null:TransportStateDump.Body(r.Unit.passengerUnit);var old=Read(r).Units[0];
                    var intent=Offer(r,kind);string beforePlan=Snapshot(r);Check(NativeExchangePlanning.TryPlan(r.State,intent,out var plan,out var error),error);
                    Check(Snapshot(r)==beforePlan&&plan!.B is null&&plan.A==r.Unit,"Planning mutated native state or invented partner");
                    Check(Commit(r,intent,out var result,out var why),why);var row=Read(r).Units[0];
                    Check(row.Appearance!.Form==NativeApparentUpgrades.Form(kind)&&row.Appearance.Health==80&&row.Appearance.Maximum==200,"Wrong native vessel health/form transition");
                    Check(NativeExchangeIntentCodec.AppearanceFingerprint(row.Appearance.Passenger!).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(old.Appearance!.Passenger!))&&row.PaidStars==old.PaidStars&&row.Transport==old.Transport,"Cargo/timer/investment changed");
                    Check(r.State.Stars==21&&r.State.Score==300&&r.State.CitySupport==1&&r.State.CurrentUnitId==7&&r.Unit.health==(real=="mimic"?10:70),"Upgrade bought a real vessel, health, support or score");
                    Check(r.Unit.passengerUnit is null?cargo is null:cargo==TransportStateDump.Body(r.Unit.passengerUnit),"Actual passenger mutated");
                    if(real!="mimic")Check(TransportStateDump.Body(r.Unit)==before&&r.State.ActionStack.Count==0,"Genuine body or sight changed");
                    else Check(r.Unit.type.ToString()==RecruitmentCatalog.Variant(NativeApparentUpgrades.Form(kind))&&r.Unit.UnitData.health==10&&r.Unit.UnitData.attack==0&&r.Unit.effects.Count==3&&r.State.ActionStack.Count==1,"Mimic operative profile, effects or sight wrong");
                    var frame=NativeGameStateEnvelope.Get(r.State)!;Check(frame.Revision==5&&Read(r).NextSequence==2&&Read(r).Receipts.Single().Affected.SequenceEqual(new ulong[]{8}),"Single-unit history missing");
                    string after=Snapshot(r);Check(Commit(r,intent,out result,out why)&&result!.AlreadyCommitted&&Snapshot(r)==after,"Duplicate commit changed state");
                });
                foreach(string passenger in new[]{"giant","warrior"})foreach(var kind in NativeApparentUpgrades.Kinds)Case(()=>{
                    var r=Make("firedragon");ushort maximum=(ushort)(passenger=="giant"?400:150);
                    var cargo=Art(passenger,120,maximum) with{Veteran=passenger=="warrior"};
                    Change(r,row=>row with{Appearance=Art("transportship",80,maximum) with{Passenger=cargo}});
                    string realBefore=TransportStateDump.Body(r.Unit);Check(Commit(r,Offer(r,kind),out _,out var why),why);var a=Read(r).Units[0].Appearance!;
                    Check(a.Maximum==maximum&&a.Health==80&&NativeExchangeIntentCodec.AppearanceFingerprint(a.Passenger!).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(cargo)),"All ordinary upgrade forms inherit wounded/veteran cargo maximum");
                    Check(TransportStateDump.Body(r.Unit)==realBefore,"Apparent high-HP cargo upgrade changed the real dragon");
                });
                Case(()=>{
                    var r=Make("firedragon");RecruitmentHooks.Definitions["bombership"].unitAbilities.Add(UnitAbility.Protect);
                    var cargo=Read(r).Units[0].Appearance!.Passenger!;Check(Commit(r,Offer(r,NativeExchangeKind.UpgradeBomber),out _,out var why),why);var a=Read(r).Units[0].Appearance!;
                    Check(a.Maximum==100&&a.Health==80&&NativeExchangeIntentCodec.AppearanceFingerprint(a.Passenger!).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(cargo))&&cargo.Maximum==200,"Synthetic Protect39 upgrade retains hull maximum and independent cargo");
                });
                foreach(bool moved in new[]{false,true})foreach(bool attacked in new[]{false,true})Case(()=>
                {var r=Make();r.Unit.moved=moved;r.Unit.attacked=attacked;var i=Offer(r);Check(Commit(r,i,out _,out var why),why);Check(r.Unit.moved==moved&&r.Unit.attacked==attacked&&r.Unit.HasEffect(UnitEffect.Frozen)&&r.Unit.HasEffect(UnitEffect.DoubleReady),"Native upgrade changed flags/status");});
                foreach(var kind in NativeApparentUpgrades.Kinds)Case(()=>
                {var r=Make();r.State.Player.Upgrades.Clear();Check(!NativeApparentUpgrades.TryOffer(r.State,7,kind,out _,out _),"Locked tech granted upgrade");r.State.Player.Propaganda=true;Check(Commit(r,Offer(r,kind),out _,out var why),why);});
                foreach(byte owner in new byte[]{0,2,255})Case(()=>
                {var r=Make();r.Tile.owner=owner;Check(!NativeApparentUpgrades.TryOffer(r.State,7,NativeExchangeKind.UpgradeScout,out _,out _),"Foreign/neutral/allied territory upgraded");});
                foreach(string mutation in new[]{"owner","turn","moved-coordinate","replacement","unexplored","linked","dead","not-ready","ended","mask","tech"})Case(()=>
                {
                    var r=Make();var intent=Offer(r);
                    switch(mutation){case "owner":r.Unit.owner=2;break;case "turn":r.State.CurrentTurn++;break;case "moved-coordinate":r.Unit.coordinates=new(3,3);break;case "replacement":r.Unit.id=9;break;case "unexplored":r.Tile.Explored=false;break;case "linked":r.Unit.leader=9;break;case "dead":r.Unit.health=0;break;case "not-ready":NativeRegroupSession.Admitted=false;break;case "ended":r.State.CurrentState=GameState.State.Ended;break;case "mask":Change(r,row=>row with{Appearance=row.Appearance! with{SourceColor=13}});break;case "tech":r.State.Player.Upgrades.Clear();break;}
                    string before=Snapshot(r);Check(!Commit(r,intent,out _,out _),"Stale upgrade accepted: "+mutation);Check(Snapshot(r)==before,"Rejected upgrade mutated: "+mutation);
                });
                Case(()=>{var r=Make("transportship");Change(r,row=>row with{Independent=false,Appearance=null});Check(!NativeApparentUpgrades.TryOffer(r.State,7,NativeExchangeKind.UpgradeScout,out _,out _),"Ordinary Raft gained free upgrade");});
                foreach(string fault in new[]{"type","data","sight-stage","ledger","sight-accept"})Case(()=>
                {var r=Make();var i=Offer(r);var before=Snapshot(r);var stack=r.State.ActionStack;Fault.At=fault;Check(!Commit(r,i,out _,out _),"Missing injected fault: "+fault);Check(Snapshot(r)==before&&ReferenceEquals(stack,r.State.ActionStack),"Partial upgrade rollback: "+fault);});
                Case(()=>
                {
                    var r=Make("firedragon");var first=Offer(r);Check(Commit(r,first,out _,out var why),why);
                    Change(r,row=>row with{Appearance=row.Appearance! with{Form="transportship",Maximum=200}});var next=Offer(r,NativeExchangeKind.UpgradeBomber);
                    string before=Snapshot(r);Check(Commit(r,first,out var duplicate,out why)&&duplicate!.AlreadyCommitted&&Snapshot(r)==before,"Old duplicate repeated after apparent Raft reset");
                    Check(Commit(r,next,out _,out why)&&Read(r).NextSequence==3&&Read(r).Units[0].Appearance!.Form=="bombership",why);
                });
                Case(()=>
                {var r=Make();var old=Read(r).Units[0];Change(r,row=>row with{Appearance=row.Appearance! with{Passenger=Art("dragonegg") with{GrowthTurns=2,GrowthDueTurn=7}}});var i=Offer(r);Check(Commit(r,i,out _,out var why),why);Check(Read(r).Units[0].Appearance!.Passenger!.GrowthDueTurn==7,"Upgrade reset growing cargo deadline");});
                foreach(string denial in new[]{"uncommitted","base-mismatch","state","player","map","unready","snapshot","unexplored","visual-missing","visual-data","hidden","invisible","replacement"})Case(()=>
                {
                    var r=Make("firedragon");var intent=Offer(r);if(denial!="uncommitted")Check(Commit(r,intent,out _,out var why),why);
                    var reaction=Reaction(r,intent);int completed=0;
                    switch(denial)
                    {
                        case "base-mismatch":reaction.action.Valid=false;break;
                        case "state":GameManager.GameState=null;break;
                        case "player":GameManager.LocalPlayer=null;break;
                        case "map":MapRenderer.Current!.Alive=false;break;
                        case "unready":NativeRegroupSession.Admitted=false;break;
                        case "snapshot":NativeGameStateEnvelope.Frames.Clear();break;
                        case "unexplored":r.Tile.Explored=false;break;
                        case "visual-missing":MapRenderer.Current!.Visual=null;break;
                        case "visual-data":MapRenderer.Current!.Visual!.Data=new();break;
                        case "hidden":MapRenderer.Current!.Visual!.IsHidden=true;break;
                        case "invisible":r.Tile.Invisible=true;break;
                        case "replacement":r.Tile.unit=new(){id=8};break;
                    }
                    string before=TransportStateDump.Dump(r.State);
                    Check(!NativeApparentUpgradeUi.BeforeReaction(reaction,new Il2CppSystem.Action(()=>completed++)),"Private reaction fell through into native Build: "+denial);
                    Check(completed==1&&UpgradeReaction.Calls==0&&UpgradeAction.Executions==0&&GameManager.Client!.Sent==0&&TransportStateDump.Dump(r.State)==before,"Denied reaction leaked VFX, duplicated callback or mutated body: "+denial);
                });
                foreach(string mode in new[]{"delay","twice","throw","callback-throw"})Case(()=>
                {
                    var r=Make("firedragon");var intent=Offer(r);Check(Commit(r,intent,out _,out var why),why);var reaction=Reaction(r,intent);UpgradeReaction.Mode=mode;int completed=0;
                    string before=Snapshot(r);Check(!NativeApparentUpgradeUi.BeforeReaction(reaction,new Il2CppSystem.Action(()=>completed++)),"Appearance reaction ran Build default");
                    Check(UpgradeReaction.Calls==1&&UpgradeReaction.Last!.Type==Polytopia.Data.UnitData.Type.scoutship&&UpgradeReaction.Last.Cost==0&&UpgradeAction.Executions==0&&GameManager.Client!.Sent==0&&Snapshot(r)==before,"Reaction executed authoritative upgrade or changed ledger");
                    Check(completed==(mode=="delay"?0:1),"Native delayed callback timing changed");UpgradeReaction.Pending!.Invoke();UpgradeReaction.Pending.Invoke();
                    Check(completed==1&&r.Tile.IncludeHidden==false,"Reaction callback duplicated or bypassed invisibility");
                });
                Case(()=>{
                    var r=Make("firedragon");var bar=Bar(r);var buttons=NativeApparentUpgradeUi.OwnedButtons(bar).Cast<UIRoundButton>().ToArray();
                    Check(buttons.Select(b=>b.text).SequenceEqual(new[]{"Feign Scout","Feign Rammer","Feign Bomber","Shed Disguise"}),"single-unit buttons retain fixed semantic order");
                    Check(buttons.All(b=>b.Cost<0&&b.ButtonEnabled&&b.buttonActive&&b.Active),"free single-unit actions hide cost decoration and keep enabled native buttons");
                    Check(ReferenceEquals(buttons.Last().Sprite,UnitArtAssets.Mask)&&buttons.Last().Renderer==null,"Shed uses mask action icon only");
                    NativeApparentUpgradeUi.Sync(bar,bar.unit,true);Check(buttons.SequenceEqual(NativeApparentUpgradeUi.OwnedButtons(bar).Cast<UIRoundButton>()),"idle sync does not replace or reorder buttons");
                    r.State.CurrentCommand++;NativeApparentUpgradeUi.Sync(bar,bar.unit,true);Check(buttons.Select(b=>b.text).SequenceEqual(NativeApparentUpgradeUi.OwnedButtons(bar).Cast<UIRoundButton>().Select(b=>b.text)),"command refresh keeps order");
                });
                Case(()=>{
                    var r=Make("firedragon");var bar=Bar(r);var button=NativeApparentUpgradeUi.OwnedButtons(bar).Cast<UIRoundButton>().Last();
                    Change(r,row=>row with{Appearance=row.Appearance! with{SourceColor=99}});string before=Snapshot(r);button.OnClicked!.Invoke();
                    var sent=GameManager.Client!.Last?.Intent;Check(GameManager.Client.Sent==1&&sent?.Kind==NativeExchangeKind.ShedDisguise&&!sent.Swap&&sent.Initiator==sent.Partner,"Shed sends one canonical single-unit intent");
                    Check(sent!.SourceAppearanceHash.SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(Read(r).Units.Single().Appearance!)),"click obtains current appearance instead of cached offer");
                    Check(Snapshot(r)==before&&UpgradeAction.Executions==0,"button does not mutate authoritative body or execute native paid upgrade");
                });
                foreach(string denial in new[]{"disabled","targeting","foreign","selection","view","removed","stale-turn"})Case(()=>{
                    var r=Make("firedragon");var bar=Bar(r);var old=NativeApparentUpgradeUi.OwnedButtons(bar).Cast<UIRoundButton>().Last().OnClicked!;
                    switch(denial){case "disabled":NativeApparentUpgradeUi.Sync(bar,bar.unit,false);break;case "targeting":MimicRegroupUi.Choosing=true;break;case "foreign":r.Unit.owner=2;break;case "selection":bar.unit=new();break;case "view":MimicPresentationUi.Allowed=false;break;case "removed":NativeApparentUpgradeUi.NativeClear(bar);break;case "stale-turn":r.State.CurrentPlayer=2;break;}
                    string before=Snapshot(r);old.Invoke();Check(GameManager.Client!.Sent==0&&Snapshot(r)==before,"invalidated Shed callback sends nothing: "+denial);
                    if(denial=="disabled")Check(!NativeApparentUpgradeUi.OwnedButtons(bar).Any(),"disabled selection removes all custom single-unit buttons");
                });
                Case(()=>{
                    var r=Make("firedragon");var bar=Bar(r);MimicRegroupUi.Choosing=true;NativeApparentUpgradeUi.Sync(bar,bar.unit,true);Check(!NativeApparentUpgradeUi.OwnedButtons(bar).Any(),"targeting removes single-unit controls");
                    MimicRegroupUi.Choosing=false;r.Unit.owner=2;NativeApparentUpgradeUi.Sync(bar,bar.unit,true);Check(!NativeApparentUpgradeUi.OwnedButtons(bar).Any(),"foreign unit has no Shed offer even if outer gate was stale");
                });
                Case(()=>{
                    var r=Make("firedragon");Check(NativeShedDisguise.TryOffer(r.State,7,out var intent,out var why),why);var reaction=Reaction(r,intent!);int completed=0;
                    Check(NativeApparentUpgradeUi.BeforeReaction(reaction,new Il2CppSystem.Action(()=>completed++))&&completed==0&&UpgradeReaction.Calls==0,"Shed reaction is delegated to single-endpoint Regroup animation");
                });
                foreach(bool propaganda in new[]{false,true})Case(()=>{
                    var r=Make();r.State.Player.Propaganda=propaganda;Change(r,row=>row with{Appearance=row.Appearance! with{Passenger=Art(RecruitmentCatalog.MenuId,10,10)}});var before=Snapshot(r);
                    foreach(var kind in NativeApparentUpgrades.Kinds)Check(!NativeApparentUpgrades.TryOffer(r.State,r.Unit.id,kind,out _,out _),"exposed Mimic boat must Regroup before feigning any ship, even with Propaganda");
                    var bar=Bar(r);Check(!NativeApparentUpgradeUi.OwnedButtons(bar).Any(),"bare boat displays no Feign or Shed button");
                    Check(Snapshot(r)==before,"denied apparent upgrades do not change currency, body or ledger");
                });
                NativeApparentUpgradeUi.Clear();
                var result=new{passed=true,cases=Cases,assertions=Assertions,scope="production planning/commit with synthetic native ports; no game rendering"};Console.WriteLine(JsonSerializer.Serialize(result));return 0;
            }
            catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases,assertions=Assertions,error=e.ToString()}));return 1;}
        }
    }
}
