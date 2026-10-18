using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using UnitType=Polytopia.Data.UnitData.Type;
namespace Deception.Tests
{
    public static class TransportTests
    {
        static int Cases,Assertions;
        static void Check(bool ok,string why){Assertions++;if(!ok)throw new Exception(why);}
        static void Case(Action test){Cases++;test();}
        sealed record Rig(GameState State,TileData Tile,UnitState Unit);
        static LedgerAppearance Art(string form="warrior",ushort health=80,ushort maximum=100)=>new(form,new("Elyrion","DarkElf"),new("Imperius","Default"),new("Elyrion","DarkElf"),57,health,maximum,false,ApparentConcealment.Visible,-1,new[]{"Poison"});
        static Rig Make(bool genuine=false,bool masked=true,string form="warrior")
        {
            NativeDisguiseGrowthAudio.Recorded.Clear();Fault.At="";RecruitmentHooks.Reset();NativeGameStateEnvelope.Frames.Clear();NativeRegroupSession.Enabled=NativeRegroupSession.Admitted=true;NativeRegroupSession.Prepares=0;NativeRegroupSession.Faults.Clear();
            var game=new GameState();NativeRegroupSession.Owned=game;var tile=new TileData{IsWater=true,Port=true,coordinates=new(2,3)};game.Map.Tiles.Add(tile);
            string name=genuine?form:RecruitmentCatalog.Variant(form);var data=RecruitmentHooks.Definitions[name];var unit=new UnitState{id=7,type=data.type,UnitData=data,coordinates=tile.coordinates,health=genuine?(ushort)80:(ushort)10,moved=true};unit.effects.Add(UnitEffect.Poisoned);tile.unit=unit;
            var ledger=new NativeIdentityLedger(Guid.Parse("a3ca461f-fd6b-44be-8b87-d4483d12f50b"),9,1,
                new[]{new LedgerUnit(8,7,true,genuine?LedgerActualKind.Genuine:LedgerActualKind.Mimic,!genuine||masked,!genuine||masked?Art(form,80,(ushort)(form=="giant"?400:100)):null,PaidInvestmentKind.Known,3)},Array.Empty<LedgerReceipt>());
            NativeGameStateEnvelope.Set(game,4,NativeIdentityLedgerCodec.Encode(ledger,4));return new(game,tile,unit);
        }
        static NativeIdentityLedger Read(Rig rig){var s=NativeGameStateEnvelope.Get(rig.State)!;return NativeIdentityLedgerCodec.Decode(s.Bytes(),s.Revision);}
        static string Body(UnitState unit)=>string.Join("|",unit.Pointer,unit.id,unit.type,unit.UnitData.Pointer,unit.owner,unit.health,unit.promotionLevel,unit.xp,unit.birthClimate,unit.birthClimateSkinType,unit.createdTurn,unit.coordinates,unit.previousTurnEndCoordinates,unit.home,unit.moved,unit.attacked,unit.direction,unit.flipped,unit.effects.Pointer,string.Join(",",Native.Items(unit.effects)),unit.passengerUnit?.Pointer);
        static string Snapshot(Rig rig)=>Body(rig.Unit)+"/"+Body(rig.Tile.unit!)+"/"+rig.State.CurrentUnitId+"/"+rig.State.Stars+"/"+rig.State.Score+"/"+rig.State.CitySupport+"/"+Convert.ToHexString(NativeGameStateEnvelope.Get(rig.State)!.Bytes());
        static void Hook(Rig r,bool embark,Action? native=null)
        {
            NativeMimicTransport.ActionScope? scope=null;Exception? error=null;
            try
            {
                bool original=embark?NativeMimicTransport.BeforeEmbark(new EmbarkAction{Coordinates=r.Tile.coordinates},r.State,out scope):NativeMimicTransport.BeforeDisembark(new DisembarkAction{Coordinates=r.Tile.coordinates},r.State,out scope);
                if(original)native?.Invoke();NativeMimicTransport.AfterTransport(scope);
            }
            catch(Exception e){error=e;throw;}
            finally{NativeMimicTransport.Finished(error,scope);}
        }
        static bool Throws(Action action){try{action();return false;}catch{return true;}}
        static void NativeBoard(Rig r)
        {
            uint original=r.State.CurrentUnitId;var vessel=RecruitmentHooks.Definitions[NativeMimicTransport.StandardVessel(r.Unit.type.ToString())];
            var unit=new UnitState{id=original+1,UnitData=vessel,type=vessel.type,passengerUnit=r.Unit,health=r.Unit.health,owner=r.Unit.owner,coordinates=r.Tile.coordinates,home=r.Unit.home,moved=true,attacked=true,effects=r.Unit.effects};
            r.Tile.unit=unit;r.State.CurrentUnitId+=2;r.State.ActionStack.Add("native-arrival");
        }
        public static int Run()
        {
            try
            {
                Case(()=>{var h=new HarmonyLib.Harmony();NativeMimicTransport.Install(h);Check(h.Hooks.SequenceEqual(new[]{"EmbarkAction.Execute","DisembarkAction.Execute"}),"Wrong native action boundaries");});
                Case(()=>{
                    var r=Make();string actual=Body(r.Unit);var original=Read(r).Units[0];Hook(r,true);var boat=r.Tile.unit!;var row=Read(r).Units[0];
                    Check(boat.type==UnitType.dcp_mimic_form_transportship&&boat.health==10&&boat.UnitData.attack==0,"Mimic boarding granted real ship capabilities");
                    Check(ReferenceEquals(boat.passengerUnit,r.Unit)&&Body(r.Unit)==actual,"Boarding replaced or mutated actual passenger");
                    Check(row.LogicalId==original.LogicalId&&row.NativeId==8&&row.Transport?.PassengerNativeId==7&&row.PaidStars==3&&row.InvestmentKind==PaidInvestmentKind.Known,"Boarding lost stable identity/investment");
                    Check(row.Appearance is {Form:"transportship",Health:80,Maximum:100,Passenger:{Form:"warrior",Health:80}},"Independent apparent vessel/cargo incorrect");
                    Check(boat.moved&&boat.attacked&&boat.home==r.Unit.home&&boat.createdTurn==r.Unit.createdTurn&&boat.birthClimate==r.Unit.birthClimate,"Boarding reset allowance/home/birth history");
                    Check(r.State.CurrentUnitId==8&&Read(r).NextLogicalId==10&&Read(r).NextSequence==1&&r.State.Stars==21&&r.State.Score==300&&r.State.CitySupport==1,"Boarding invented ID/command/payment/support changes");
                    Check(boat.UnitData.cost+r.Unit.UnitData.cost==1&&!ReferenceEquals(boat.effects,r.Unit.effects),"Boat duplicates paid value or shares mutable effects");
                    byte[] stored=NativeGameStateEnvelope.Get(r.State)!.Bytes();Check(NativeIdentityLedgerCodec.Encode(NativeIdentityLedgerCodec.Decode(stored,4),4).SequenceEqual(stored),"Afloat private state does not roundtrip");
                    r.Tile.IsWater=false;Hook(r,false);row=Read(r).Units[0];Check(ReferenceEquals(r.Tile.unit,r.Unit)&&row.NativeId==7&&row.Transport is null&&row.Appearance is {Form:"warrior",Health:80,Passenger:null},"Landing failed to restore actual cargo and separate mask");
                    Check(r.Unit.health==10&&r.Unit.xp==4&&r.Unit.createdTurn==2&&r.Unit.moved&&r.Unit.attacked&&r.State.CurrentUnitId==8,"Landing reset identity/action history/health");
                });
                foreach(bool moved in new[]{false,true})foreach(bool attacked in new[]{false,true})Case(()=>{
                    var r=Make();r.Unit.moved=moved;r.Unit.attacked=attacked;string prior=Body(r.Unit);
                    using var p=NativeMimicTransport.StageConversion(r.State,r.Unit,r.Tile,RecruitmentCatalog.Variant("transportship"),8,false);
                    Check(Body(r.Unit)==prior&&r.Tile.unit==r.Unit&&r.State.CurrentUnitId==7,"Read-only conversion staging published state");p.Apply();r.Tile.unit=(UnitState)p.FinalUnit;r.State.CurrentUnitId=8;p.PrepareAcceptance();p.Accept();
                    var boat=r.Tile.unit!;Check(boat.moved==moved&&boat.attacked==attacked,"Coast conversion refreshed/spent partner allowance");
                    r.Tile.IsWater=false;using var land=NativeMimicTransport.StageConversion(r.State,boat,r.Tile,RecruitmentCatalog.Variant("warrior"),0,false);land.Apply();r.Tile.unit=(UnitState)land.FinalUnit;land.PrepareAcceptance();land.Accept();
                    Check(r.Unit.moved==moved&&r.Unit.attacked==attacked&&r.Unit.health==10&&r.Unit.home.ToString()=="8,9","Coast landing changed partner allowance/support");
                });
                foreach(string fault in new[]{"type","data","coordinates","tile","counter","sight-stage","validation","ledger","sight-accept"})Case(()=>{
                    var r=Make();string before=Snapshot(r);var stack=r.State.ActionStack;Fault.At=fault;Check(Throws(()=>Hook(r,true)),"Missing boarding fault "+fault);
                    Check(Snapshot(r)==before&&ReferenceEquals(r.State.ActionStack,stack),"Boarding rollback was incomplete: "+fault);
                });
                foreach(string fault in new[]{"type","data","coordinates","tile","counter","sight-stage","validation","ledger","sight-accept"})Case(()=>{
                    var r=Make();Hook(r,true);r.Tile.IsWater=false;string before=Snapshot(r);var stack=r.State.ActionStack;Fault.At=fault;Check(Throws(()=>Hook(r,false)),"Missing landing fault "+fault);
                    Check(Snapshot(r)==before&&ReferenceEquals(r.State.ActionStack,stack),"Landing rollback was incomplete: "+fault);
                });
                Case(()=>{var r=Make();using var p=NativeMimicTransport.StageConversion(r.State,r.Unit,r.Tile,RecruitmentCatalog.Variant("transportship"),8,false);r.Unit.attacked=true;Check(Throws(p.Apply),"Changed action state passed staged conversion");});
                Case(()=>{var r=Make();using var p=NativeMimicTransport.StageConversion(r.State,r.Unit,r.Tile,RecruitmentCatalog.Variant("transportship"),8,false);r.Unit.effects.Add(UnitEffect.Frozen);Check(Throws(p.Apply),"In-place status change passed staged conversion");});
                Case(()=>{var r=Make();r.Unit.leader=2;Check(Throws(()=>NativeMimicTransport.StageConversion(r.State,r.Unit,r.Tile,RecruitmentCatalog.Variant("transportship"),8,false)),"Linked source boarded");});
                Case(()=>{var r=Make();Check(Throws(()=>NativeMimicTransport.StageConversion(r.State,r.Unit,r.Tile,RecruitmentCatalog.Variant("transportship"),7,false)),"Unreserved wrapper ID accepted");});
                Case(()=>{var r=Make();Check(Throws(()=>NativeMimicTransport.StageConversion(r.State,r.Unit,new TileData(),RecruitmentCatalog.Variant("transportship"),8,false)),"Foreign destination accepted");});
                Case(()=>{var r=Make();RecruitmentHooks.Definitions[RecruitmentCatalog.Variant("transportship")].cost=3;Check(Throws(()=>Hook(r,true))&&r.Tile.unit==r.Unit,"Boat duplicated paid investment");});
                Case(()=>{var r=Make();r.Unit.owner=2;string before=Snapshot(r);Check(Throws(()=>Hook(r,true))&&Snapshot(r)==before,"Wrong owner transported");});
                Case(()=>{var r=Make();r.State.CurrentPlayer=2;string before=Snapshot(r);Check(Throws(()=>Hook(r,true))&&Snapshot(r)==before,"Wrong current player transported");});
                Case(()=>{var r=Make();RecruitmentHooks.Definitions["juggernaut"].unitAbilities.Add(UnitAbility.Protect);var a=Art("giant",100,400);var vessel=NativeMimicTransport.EmbarkAppearance(r.State,a);Check(vessel.Form=="juggernaut"&&vessel.Health==100&&vessel.Maximum==100&&vessel.Passenger==a,"Synthetic Protect39 hull does not retain its own maximum");Check(NativeMimicTransport.LandAppearance(r.State,vessel with{Health=50}).Health==100,"Synthetic Protect39 landing overwrote independent passenger wounds");});
                Case(()=>{var r=Make();var a=Art() with{Veteran=true,Maximum=150};var vessel=NativeMimicTransport.EmbarkAppearance(r.State,a);Check(!vessel.Veteran&&vessel.Maximum==150&&vessel.Passenger!.Maximum==150&&vessel.Passenger.Veteran,"Cargo-derived max health/veteran lost");Check(NativeMimicTransport.LandAppearance(r.State,vessel with{Form="scoutship",Health=50,Maximum=150}).Health==50,"Ordinary apparent landing did not carry boat wounds");});
                foreach(string form in new[]{"giant","warrior","firedragon"})Case(()=>{
                    var r=Make();ushort maximum=(ushort)(form=="giant"?400:form=="firedragon"?200:150);
                    var a=Art(form,80,maximum) with{Veteran=form=="warrior"};var vessel=NativeMimicTransport.EmbarkAppearance(r.State,a);
                    Check(vessel.Health==80&&vessel.Maximum==maximum&&vessel.Passenger==a&&!vessel.Veteran,"Ordinary hull must inherit wounded/veteran passenger HP: "+form);
                    var landed=NativeMimicTransport.LandAppearance(r.State,vessel with{Health=60});
                    Check(landed.Health==60&&landed.Maximum==maximum&&landed.Veteran==a.Veteran&&a.Health==80,"Ordinary landing carries hull wounds without mutating retained passenger value: "+form);
                    Check(!Native.Items(RecruitmentHooks.Definitions[vessel.Form].unitAbilities).Contains(UnitAbility.Protect),"Ordinary source must have no synthetic Protect39");
                });
                foreach(string form in new[]{"giant","warrior"})Case(()=>{
                    var r=Make(genuine:true,masked:false,form:form);r.Unit.health=(ushort)(form=="giant"?275:130);r.Unit.promotionLevel=(ushort)(form=="warrior"?1:0);
                    ushort hp=r.Unit.health,promotion=r.Unit.promotionLevel;string before=Body(r.Unit);
                    using var board=NativeMimicTransport.StageConversion(r.State,r.Unit,r.Tile,NativeMimicTransport.StandardVessel(form),8,false);
                    board.Apply();r.Tile.unit=(UnitState)board.FinalUnit;r.State.CurrentUnitId=8;board.PrepareAcceptance();board.Accept();
                    var vessel=r.Tile.unit!;Check(vessel.health==hp&&ReferenceEquals(vessel.passengerUnit,r.Unit)&&Body(r.Unit)==before,"Real conversion must carry actual wounds and retain cargo history");
                    vessel.health=(ushort)(hp-20);r.Tile.IsWater=false;
                    using var land=NativeMimicTransport.StageConversion(r.State,vessel,r.Tile,form,0,false);land.Apply();r.Tile.unit=(UnitState)land.FinalUnit;land.PrepareAcceptance();land.Accept();
                    Check(ReferenceEquals(r.Tile.unit,r.Unit)&&r.Unit.health==hp-20&&r.Unit.promotionLevel==promotion,"Real no-Protect landing must restore exact cargo with vessel wounds");
                });
                Case(()=>{var r=Make();var a=Art("dragonegg") with{GrowthTurns=1,GrowthDueTurn=5};var vessel=NativeMimicTransport.EmbarkAppearance(r.State,a);Check(vessel.GrowthDueTurn==-1&&vessel.Passenger!.GrowthDueTurn==5&&NativeMimicTransport.LandAppearance(r.State,vessel).GrowthDueTurn==5,"Boarding erased/restarted growing passenger timer");});
                Case(()=>{
                    var r=Make(genuine:true,masked:false,form:"cloak");r.Unit.effects.Add(UnitEffect.Invisible);r.Unit.effects.Add(UnitEffect.Boosted);
                    var originalEffects=r.Unit.effects;var values=Native.Items(originalEffects).ToArray();
                    Hook(r,true,()=>NativeBoard(r));var boat=r.Tile.unit!;var row=Read(r).Units[0];
                    Check(boat.type==UnitType.cloak_boat&&ReferenceEquals(boat.passengerUnit,r.Unit),"Genuine hidden Cloak did not use its native Dinghy/passenger");
                    Check(ReferenceEquals(boat.effects,originalEffects)&&Native.Items(boat.effects).SequenceEqual(values),"Unmasked boarding replaced native effects or stripped genuine Cloak invisibility");
                    Check(!row.Independent&&row.Appearance is null&&row.Transport?.PassengerNativeId==r.Unit.id,"Unmasked Cloak acquired a private disguise on boarding");
                    r.Tile.IsWater=false;var landedEffects=new Il2CppSystem.Collections.Generic.List<UnitEffect>();foreach(var value in values)landedEffects.Add((UnitEffect)value);
                    Hook(r,false,()=>{r.Tile.unit=boat.passengerUnit!;r.Tile.unit.coordinates=r.Tile.coordinates;r.Tile.unit.moved=r.Tile.unit.attacked=true;r.Tile.unit.effects=landedEffects;});
                    Check(ReferenceEquals(r.Tile.unit,r.Unit)&&ReferenceEquals(r.Unit.effects,landedEffects)&&Native.Items(r.Unit.effects).SequenceEqual(values),"Unmasked landing replaced native effects or stripped genuine Cloak invisibility");
                    Check(Native.Items(originalEffects).SequenceEqual(values),"Cloak native effects preimage was modified");
                    row=Read(r).Units[0];Check(!row.Independent&&row.Appearance is null&&row.Transport is null&&row.NativeId==r.Unit.id,"Unmasked Cloak landing acquired a disguise or lost lineage");
                });
                foreach(bool masked in new[]{false,true})Case(()=>{
                    var r=Make(genuine:true,masked:masked);Hook(r,true,()=>NativeBoard(r));var row=Read(r).Units[0];Check(row.LogicalId==8&&row.NativeId==8&&row.Transport?.PassengerNativeId==7&&Read(r).Units.Length==1,"Genuine native boarding created duplicate logical cargo");
                    Check(row.Independent==masked&&(row.Appearance is null)==!masked&&r.State.ActionStack.Contains("native-arrival"),"Genuine native behavior/mask lost");
                    var boat=r.Tile.unit!;r.Tile.IsWater=false;Hook(r,false,()=>{r.Tile.unit=boat.passengerUnit!;r.Tile.unit.coordinates=r.Tile.coordinates;r.Tile.unit.moved=r.Tile.unit.attacked=true;r.State.ActionStack.Add("native-growth-and-score");});
                    row=Read(r).Units[0];Check(row.NativeId==7&&row.Transport is null&&row.Independent==masked&&r.State.ActionStack.Contains("native-growth-and-score"),"Genuine native disembark/growth behavior lost");
                });
                foreach(string form in new[]{"amphibian","firedragon"})foreach(bool disguiseAtSea in new[]{false,true})Case(()=>
                {
                    var r=Make(genuine:true);var art=Art(form,80,(ushort)(form=="firedragon"?200:100));
                    void SetMask(){var ledger=Read(r);NativeGameStateEnvelope.Set(r.State,4,NativeIdentityLedgerCodec.Encode(ledger with{Units=new[]{ledger.Units[0] with{Appearance=art}}},4));}
                    if(!disguiseAtSea)SetMask();
                    Hook(r,true,()=>NativeBoard(r));var boat=r.Tile.unit!;
                    if(disguiseAtSea)SetMask();
                    var afloat=Read(r).Units[0];
                    Check(afloat.Transport?.PassengerNativeId==7&&ReferenceEquals(boat.passengerUnit,r.Unit),"Naturally aquatic mask lost real passenger");
                    Check(afloat.Appearance is {Passenger:null}&&afloat.Appearance.Form==form,"Real boarding incorrectly wrapped an independent swimming/flying mask");
                    r.Tile.IsWater=false;
                    Hook(r,false,()=>{r.Tile.unit=boat.passengerUnit!;r.Tile.unit.coordinates=r.Tile.coordinates;r.Tile.unit.moved=r.Tile.unit.attacked=true;r.State.ActionStack.Add("native-growth-and-score");});
                    var landed=Read(r).Units[0];
                    Check(landed.Transport is null&&landed.NativeId==7&&ReferenceEquals(r.Tile.unit,r.Unit),"Masked genuine landing lost original physical identity");
                    Check(landed.Appearance is {Passenger:null}&&landed.Appearance.Form==form&&landed.Appearance.Health==80,"Genuine landing replaced an unwrapped swimming/flying mask");
                    Check(r.Unit.type==UnitType.warrior&&r.Unit.health==80&&r.Unit.xp==4,"Mask transport changed genuine form/health/history");
                    Check(r.Unit.moved&&r.Unit.attacked&&r.State.ActionStack.Contains("native-growth-and-score"),"Mask transport bypassed genuine native consequences");
                    Check(NativeRegroupSession.Faults.Count==0,"Independent real/apparent transport faulted the session");
                });
                Case(()=>
                {
                    var r=Make(genuine:true);Hook(r,true,()=>NativeBoard(r));var boat=r.Tile.unit!;
                    var baby=Art("babydragon",120,150) with{GrowthTurns=0,GrowthDueTurn=1};var ledger=Read(r);
                    NativeGameStateEnvelope.Set(r.State,4,NativeIdentityLedgerCodec.Encode(ledger with{Units=new[]{ledger.Units[0] with{Appearance=baby}}},4));
                    r.Tile.IsWater=false;Hook(r,false,()=>{r.Tile.unit=boat.passengerUnit!;r.Tile.unit.coordinates=r.Tile.coordinates;r.Unit.moved=r.Unit.attacked=true;});
                    var landed=Read(r).Units[0].Appearance!;
                    Check(landed.Form=="babydragon"&&landed.GrowthDueTurn==1&&landed.Health==120,"Landing advanced an apparent root that was not cargo");
                    Check(r.Unit.type==UnitType.warrior&&r.Unit.health==80&&NativeRegroupSession.Faults.Count==0,"Unwrapped growth mask altered actual landing");
                });
                Case(()=>
                {
                    var r=Make(genuine:true);r.Unit.health=100;
                    var ledger=Read(r);var dragon=Art("firedragon",200,200);
                    NativeGameStateEnvelope.Set(r.State,4,NativeIdentityLedgerCodec.Encode(ledger with{Units=new[]{ledger.Units[0] with{Appearance=dragon}}},4));
                    Hook(r,true,()=>NativeBoard(r));var raft=r.Tile.unit!;var row=Read(r).Units[0];
                    Check(raft.type==UnitType.transportship&&raft.health==100&&raft.UnitData.health==100,"Dragon disguise supplied health/type to genuine Warrior raft");
                    Check(ReferenceEquals(raft.passengerUnit,r.Unit)&&r.Unit.type==UnitType.warrior&&r.Unit.health==100&&r.Unit.UnitData.health==100,"Embarkation replaced genuine Warrior cargo/data");
                    Check(row.Appearance is {Form:"firedragon",Health:200,Maximum:200,Passenger:null}&&row.Transport?.PassengerNativeId==r.Unit.id,"Real raft and independent flying disguise merged");
                    r.Tile.IsWater=false;Hook(r,false,()=>{r.Tile.unit=r.Unit;r.Unit.moved=r.Unit.attacked=true;});
                    Check(r.Unit.health==100&&Read(r).Units[0].Appearance!.Health==200&&Read(r).Units[0].Transport is null,"Landing merged real/disguised HP");
                });
                Case(()=>{var r=Make();Check(NativeMimicTransport.CanHandleMovement(r.State,r.Unit,r.Tile),"Valid native port boarding unavailable");NativeRegroupSession.Admitted=false;Check(!NativeMimicTransport.CanHandleMovement(r.State,r.Unit,r.Tile),"Unready movement admitted");});
                Case(()=>{var r=Make();NativeRegroupSession.Admitted=false;string before=Snapshot(r);Check(Throws(()=>Hook(r,true))&&Snapshot(r)==before,"Unready Mimic fell through to native converter");});
                Case(()=>{var r=Make();r.Tile.City=true;Check(!NativeMimicTransport.WaterEndpoint(r.Tile),"City on water did not prefer landing");r.Tile.City=false;r.Tile.IsWater=false;r.Tile.Wetland=true;Check(NativeMimicTransport.WaterEndpoint(r.Tile),"Wetland omitted from water endpoint");});
                Case(()=>{var r=Make();var a=Art("firedragon",100,200);Check(ReferenceEquals(NativeMimicTransport.InterpretAppearance(r.State,a,r.Tile),a),"Flying disguise invented passenger wrapper");});
                foreach(bool genuine in new[]{false,true})foreach(bool due in new[]{false,true})Case(()=>
                {
                    var r=Make(genuine:genuine);var ledger=Read(r);var art=Art("dragonegg",80,100) with{GrowthTurns=due?0:2,GrowthDueTurn=due?4:6};
                    var row=ledger.Units[0] with{Appearance=art};ledger=ledger with{Units=new[]{row}};
                    NativeGameStateEnvelope.Set(r.State,4,NativeIdentityLedgerCodec.Encode(ledger,4));
                    var source=RecruitmentHooks.Definitions[genuine?"dragonegg":RecruitmentCatalog.Variant("dragonegg")];r.Unit.type=source.type;r.Unit.UnitData=source;
                    Hook(r,true,genuine?()=>NativeBoard(r):null);var boat=r.Tile.unit!;Check(Read(r).Units[0].Appearance!.Passenger!.Form=="dragonegg","Boarding advanced egg passenger");
                    r.Tile.IsWater=false;Hook(r,false,genuine?()=>{r.Tile.unit=boat.passengerUnit!;r.Tile.unit.coordinates=r.Tile.coordinates;r.Tile.unit.moved=r.Tile.unit.attacked=true;r.State.ActionStack.Add("native-growth-pending");}:null);
                    var landed=Read(r).Units[0].Appearance!;Check(landed.Form==(due?"babydragon":"dragonegg")&&landed.GrowthDueTurn==(due?7:6),"Ordinary landing failed exact due phase/deadline");
                    Check(NativeDisguiseGrowthAudio.Recorded.Count==(due?1:0),"Only completed overdue landing queues one growth cue");
                    if(due)Check(NativeDisguiseGrowthAudio.Recorded[0].Action is DisembarkAction&&NativeDisguiseGrowthAudio.Recorded[0].Before.Form=="dragonegg"&&NativeDisguiseGrowthAudio.Recorded[0].After.Appearance!.Form=="babydragon","Landing sound did not bind native Disembark and unwrapped phase");
                    Check(landed.Health==(due?130:80)&&landed.Maximum==(due?150:100),"Ordinary landing lost apparent wound deficit");
                    Check(genuine?r.Unit.type==UnitType.dragonegg&&r.State.ActionStack.Contains("native-growth-pending"):r.Unit.type==(due?UnitType.dcp_mimic_form_babydragon:UnitType.dcp_mimic_form_dragonegg)&&r.Unit.health==10,"Landing replaced genuine cargo/growth or made real dragon");
                });
                Case(()=>{var r=Make();var egg=Art("dragonegg",80,100) with{GrowthTurns=0,GrowthDueTurn=2};var ship=NativeMimicTransport.EmbarkAppearance(r.State,egg);r.Tile.IsWater=false;
                    Check(NativeMimicTransport.LandAppearance(r.State,ship).Form=="dragonegg","Pure coastal unwrap hatched passenger");Check(NativeMimicTransport.InterpretAppearance(r.State,ship,r.Tile).GrowthDueTurn==2,"Coast conversion erased overdue timer");});
                Case(()=>{var r=Make();var ledger=Read(r);var art=Art("babydragon",120,150) with{GrowthTurns=0,GrowthDueTurn=1};ledger=ledger with{Units=new[]{ledger.Units[0] with{Appearance=art}}};NativeGameStateEnvelope.Set(r.State,4,NativeIdentityLedgerCodec.Encode(ledger,4));
                    var d=RecruitmentHooks.Definitions[RecruitmentCatalog.Variant("babydragon")];r.Unit.type=d.type;r.Unit.UnitData=d;Hook(r,true);r.Tile.IsWater=false;Hook(r,false);var landed=Read(r).Units[0].Appearance!;
                    Check(landed.Form=="firedragon"&&landed.Health==170&&landed.Maximum==200,"Baby ordinary landing did not preserve deficit");Check(landed.GrowthDueTurn==-1,"Adult landing retained growth deadline");Check(NativeDisguiseGrowthAudio.Recorded.Count==1&&NativeDisguiseGrowthAudio.Recorded[0].After.Appearance!.Form=="firedragon","Adult landing missing one phase cue");Check(r.Unit.health==10&&r.Unit.type==UnitType.dcp_mimic_form_firedragon,"Adult landing granted real HP or wrong carrier");});
                CoastTests.Run(Case,Check);
                CityEndpointTests.Run(Case,Check);
                CoastalPlacementTests.Run(Case,Check);
                var result=new{passed=true,cases=Cases,assertions=Assertions,scope="synthetic-native-transport-boundary"};var json=JsonSerializer.Serialize(result);Console.WriteLine(json);var path=Environment.GetEnvironmentVariable("TRANSPORT_TEST_OUTPUT");if(!string.IsNullOrEmpty(path))File.WriteAllText(path,json);return 0;
            }
            catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases,assertions=Assertions,error=e.ToString()}));return 1;}
        }
    }
}
