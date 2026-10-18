using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
    internal static class CoastalPlacementTests
    {
        sealed record Rig(GameState State,UnitState A,UnitState B,TileData Water,TileData Land,NativeExchangeIntent Intent);
        static LedgerAppearance Art(string form)=>new(form,new("Elyrion","DarkElf"),new("Imperius","Default"),new("Elyrion","DarkElf"),57,100,100,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
        static Rig Make(NativeExchangeKind kind,bool swap,bool mimicPartner=false,string form="shaman",bool embark=false)
        {
            Fault.At="";RecruitmentHooks.Reset();NativeRegroupSession.Enabled=NativeRegroupSession.Admitted=true;
            var state=new GameState{CurrentUnitId=4};NativeRegroupSession.Owned=state;
            state.Player.tribe=PolytopiaBackendBase.Common.TribeType.Cymanti;state.GameLogicData.Tribe.HasNativeEmbark=embark;
            var water=new TileData{coordinates=new(1,1),IsWater=true};var land=new TileData{coordinates=new(2,1)};
            state.Map.Tiles.Add(water);state.Map.Tiles.Add(land);
            UnitState Unit(uint id,string name,TileData tile){var data=RecruitmentHooks.Definitions[name];var unit=new UnitState{id=id,type=data.type,UnitData=data,health=RecruitmentCatalog.IsMimicId(name)?(ushort)10:(ushort)100,coordinates=tile.coordinates};tile.unit=unit;return unit;}
            var a=Unit(3,RecruitmentCatalog.Variant("moth"),water);var b=Unit(4,mimicPartner?RecruitmentCatalog.Variant(form):form,land);b.moved=b.attacked=true;
            var rows=new[]{new LedgerUnit(1,3,true,LedgerActualKind.Mimic,true,Art("moth"),PaidInvestmentKind.Known,3),new LedgerUnit(2,4,true,mimicPartner?LedgerActualKind.Mimic:LedgerActualKind.Genuine,mimicPartner,mimicPartner?Art(form):null,PaidInvestmentKind.Known,3)};
            var ledger=new NativeIdentityLedger(Guid.Parse("8d87f77c-9509-447f-a320-593dadfbcb9d"),6,1,rows,Array.Empty<LedgerReceipt>());NativeGameStateEnvelope.Set(state,4,NativeIdentityLedgerCodec.Encode(ledger,4));
            var source=kind==NativeExchangeKind.Disguise?rows[0].Appearance!:rows[1].Appearance??NativeLedgerState.Capture(state,new(b,true),form,false);
            var intent=new NativeExchangeIntent(ledger.Epoch,1,4,state.CurrentTurn,1,kind,swap,NativePartnerReference.Existing,6,new(1,3,1,1),new(2,4,2,1),NativeExchangeIntentCodec.AppearanceFingerprint(source));
            return new(state,a,b,water,land,intent);
        }
        internal static void Run(Action<Action> test,Action<bool,string> check)
        {
            foreach(var tribe in new[]{PolytopiaBackendBase.Common.TribeType.Cymanti,PolytopiaBackendBase.Common.TribeType.Polaris,PolytopiaBackendBase.Common.TribeType.Aquarion})
            foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})foreach(bool swap in new[]{false,true})foreach(bool mimic in new[]{false,true})test(()=>
            {
                var r=Make(kind,swap,mimic);r.State.Player.tribe=tribe;string before=TransportStateDump.Dump(r.State);var saved=NativeGameStateEnvelope.Get(r.State)!;
                bool expected=!swap||mimic;bool allowed=NativeExchangePlanning.TryPlan(r.State,r.Intent,out var plan,out var why);
                check(allowed==expected,$"{tribe} fairy/{kind}/swap{swap}/mimic{mimic}: {why}");
                check(TransportStateDump.Dump(r.State)==before&&ReferenceEquals(saved,NativeGameStateEnvelope.Get(r.State)),"Coastal preview mutated state");
                if(!allowed){check(why.Contains("native passenger-vessel"),"Wrong rejection guard: "+why);check(!NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out _)&&TransportStateDump.Dump(r.State)==before,"Rejected swap changed real bodies");return;}
                check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out why),"Accepted coast failed commit: "+why);
                var frame=NativeGameStateEnvelope.Get(r.State)!;var ledger=NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision);var a=ledger.Units.Single(x=>x.LogicalId==1);var b=ledger.Units.Single(x=>x.LogicalId==2);
                var bodyA=r.State.Map.Tiles.Select(t=>t.unit!).Single(x=>x.id==a.NativeId);var bodyB=r.State.Map.Tiles.Select(t=>t.unit!).Single(x=>x.id==b.NativeId);
                check(bodyA.health==10&&bodyA.UnitData.attack==0&&bodyA.moved&&bodyA.attacked,"Initiator gained stats or action allowance");
                check(bodyB.moved&&bodyB.attacked&&bodyB.health==(mimic?10:100),"Partner spent flags or actual health changed");
                check(!r.State.Player.MovementResearched&&r.State.Stars==21&&r.State.CitySupport==1,"Placement granted research or changed economy/support");
                if(!swap&&!mimic)check(ReferenceEquals(r.Land.unit,r.B)&&r.B.type==Polytopia.Data.UnitData.Type.shaman&&b.Transport is null,"Keep fabricated a genuine Shaman raft");
                if(kind==NativeExchangeKind.Regroup&&!swap)check(a.Appearance is {Form:"transportship",Passenger:{Form:"shaman"}}&&bodyA.passengerUnit==r.A&&a.Transport?.PassengerNativeId==3,"Water copy lacks coherent custom vessel/passenger");
                if(kind==NativeExchangeKind.Disguise)check(b.Appearance is {Form:"moth",Passenger:null},"Disguise failed to preserve transferred flight appearance");
                byte[] bytes=frame.Bytes();check(NativeIdentityLedgerCodec.Encode(NativeIdentityLedgerCodec.Decode(bytes,frame.Revision),frame.Revision).SequenceEqual(bytes),"Coastal mask/cargo did not roundtrip");
                string done=TransportStateDump.Dump(r.State);check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out var duplicate,out why)&&duplicate!.AlreadyCommitted&&TransportStateDump.Dump(r.State)==done,"Duplicate coastal action was not idempotent");
            });
            foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})foreach(string tribe in new[]{"Imperius","Elyrion"})test(()=>
            {
                var r=Make(kind,true,embark:true);r.State.Player.tribe=Enum.Parse<PolytopiaBackendBase.Common.TribeType>(tribe);
                check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out var why),"Unresearched native-family coastal swap rejected: "+why);
                check(r.Water.unit!.type==Polytopia.Data.UnitData.Type.transportship&&ReferenceEquals(r.Water.unit.passengerUnit,r.B)&&r.B.health==100,"Native-family conversion replaced actual passenger");
                check(!r.State.Player.MovementResearched,"Native-family placement granted water movement research");
            });
            foreach(string form in new[]{"moth","amphibian"})test(()=>
            {
                var r=Make(NativeExchangeKind.Disguise,true,form:form);check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out var why),"Naturally aquatic/flying actual body needs no boat family: "+why);
                check(ReferenceEquals(r.Water.unit,r.B)&&r.B.passengerUnit is null,"Aquatic/flying partner was fabricated into a vessel");
            });
            foreach(string form in new[]{"shark","island"})foreach(bool wet in new[]{false,true})test(()=>
            {
                // Actual aquatic-only partner starts on water. The flying Mimic is on a dry/flooded bank.
                var r=Make(NativeExchangeKind.Regroup,true,form:form);r.Water.IsWater=false;r.Water.Wetland=wet;r.Land.IsWater=true;
                bool allowed=NativeExchangePlanning.TryPlan(r.State,r.Intent,out _,out var why);check(allowed==wet,"Aquatic-only body dry/flood endpoint: "+why);
                if(wet)check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out why)&&ReferenceEquals(r.Water.unit,r.B),"Flooded aquatic-only swap failed: "+why);
                else check(!NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out _),"Genuine aquatic-only body was placed on dry land");
            });
            foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})test(()=>
            {
                var r=Make(kind,true,embark:true);r.Land.Terrain="mountain";
                check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out var why),"Mountain placement wrongly requires movement research: "+why);
                check(!r.State.Player.MovementResearched,"Mountain placement acquired Climbing");
            });
            foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})test(()=>
            {
                var r=Make(kind,false);r.Water.Terrain="ocean";
                check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out var why),"Ocean Keep incorrectly requires Sailing: "+why);
                check(!r.State.Player.MovementResearched&&ReferenceEquals(r.Land.unit,r.B),"Ocean Keep granted movement or fabricated a Shaman raft");
            });
            foreach(string fault in new[]{"counter","ledger","sight-accept"})test(()=>
            {
                var r=Make(NativeExchangeKind.Regroup,false);string before=TransportStateDump.Dump(r.State);byte[] bytes=NativeGameStateEnvelope.Get(r.State)!.Bytes();Fault.At=fault;
                check(!NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out _),"Missing coastal Keep fault "+fault);
                check(TransportStateDump.Dump(r.State)==before&&NativeGameStateEnvelope.Get(r.State)!.Bytes().SequenceEqual(bytes),"Coastal Keep rollback lost original bodies/ledger: "+fault);
            });
        }
    }
}
