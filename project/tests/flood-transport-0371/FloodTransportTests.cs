using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
    public static class FloodTransportTests
    {
        static int Assertions;static readonly List<string> Cases=new();
        static void Check(bool b,string why){Assertions++;if(!b)throw new Exception(why);}
        static void Test(string id,Action action){try{action();Cases.Add(id);}catch(Exception e){throw new Exception(id,e);}}
        sealed record Rig(GameState State,TileData Tile,UnitState Land);
        static Rig Make()
        {
            RecruitmentHooks.Reset();Fault.At="";NativeGameStateEnvelope.Frames.Clear();NativeRegroupSession.Enabled=NativeRegroupSession.Admitted=true;NativeRegroupSession.Faults.Clear();
            var state=new GameState();NativeRegroupSession.Owned=state;
            var tile=new TileData{IsWater=true,Port=true,coordinates=new(2,3)};state.Map.Tiles.Add(tile);
            var data=RecruitmentHooks.Definitions[RecruitmentCatalog.Variant("warrior")];
            var unit=new UnitState{id=7,type=data.type,UnitData=data,coordinates=tile.coordinates,health=10,moved=true};unit.effects.Add(UnitEffect.Poisoned);tile.unit=unit;
            var a=new LedgerAppearance("warrior",new("Elyrion","DarkElf"),new("Imperius","Default"),new("Elyrion","DarkElf"),57,80,100,false,ApparentConcealment.Visible,-1,new[]{"Poisoned"});
            var ledger=new NativeIdentityLedger(Guid.Parse("a3ca461f-fd6b-44be-8b87-d4483d12f50b"),9,1,new[]{new LedgerUnit(8,7,true,LedgerActualKind.Mimic,true,a,PaidInvestmentKind.Known,3)},Array.Empty<LedgerReceipt>());
            NativeGameStateEnvelope.Set(state,4,NativeIdentityLedgerCodec.Encode(ledger,4));return new(state,tile,unit);
        }
        static LedgerUnit Row(Rig r){var s=NativeGameStateEnvelope.Get(r.State)!;return NativeIdentityLedgerCodec.Decode(s.Bytes(),s.Revision).Units.Single();}
        static bool Child(Rig r,bool board)
        {
            NativeMimicTransport.ActionScope? scope=null;Exception? error=null;
            try{bool original=board?NativeMimicTransport.BeforeEmbark(new EmbarkAction{Coordinates=r.Tile.coordinates},r.State,out scope):NativeMimicTransport.BeforeDisembark(new DisembarkAction{Coordinates=r.Tile.coordinates},r.State,out scope);NativeMimicTransport.AfterTransport(scope);return original;}
            catch(Exception e){error=e;throw;}
            finally{NativeMimicTransport.Finished(error,scope);}
        }
        public static int Run()
        {
            try
            {
                // Match the incident: native Disembark child arrives at a
                // flooded land tile after the ship has already moved there.
                foreach(bool city in new[]{false,true})foreach(bool rawWater in new[]{false,true})
                Test($"queued-flooded-disembark-preserves-cargo-{city}-{rawWater}",()=>
                {
                    var r=Make();Child(r,true);var boat=r.Tile.unit!;r.Tile.Wetland=true;r.Tile.City=city;r.Tile.IsWater=rawWater;
                    var old=NativeGameStateEnvelope.Get(r.State)!;byte[] bytes=old.Bytes();string actual=TransportStateDump.Dump(r.State);
                    Check(!Child(r,false),"Mimic flooded landing must consume the obsolete native child without executing it");
                    Check(r.Tile.unit==boat&&boat.passengerUnit==r.Land&&Row(r).NativeId==boat.id&&Row(r).Transport?.PassengerNativeId==7,"boat/cargo identity was split on flood");
                    Check(actual==TransportStateDump.Dump(r.State)&&bytes.SequenceEqual(NativeGameStateEnvelope.Get(r.State)!.Bytes()),"flood transition changed native or persistent state");
                    NativeLedgerState.Validate(r.State,old.Revision,bytes);
                    Check(NativeRegroupSession.Faults.Count==0,"no-op flood child faulted the match");
                });
                foreach(bool city in new[]{false,true})foreach(bool rawWater in new[]{false,true})
                Test($"queued-flooded-embark-preserves-land-{city}-{rawWater}",()=>
                {
                    var r=Make();r.Tile.Wetland=true;r.Tile.City=city;r.Tile.IsWater=rawWater;var old=NativeGameStateEnvelope.Get(r.State)!;string actual=TransportStateDump.Dump(r.State);
                    Check(!Child(r,true),"Mimic flood boarding child must be suppressed");
                    Check(r.Tile.unit==r.Land&&r.Land.passengerUnit==null&&Row(r).Transport==null&&Row(r).Appearance?.Form=="warrior","flood invented vessel mode");
                    Check(actual==TransportStateDump.Dump(r.State)&&old.Bytes().SequenceEqual(NativeGameStateEnvelope.Get(r.State)!.Bytes())&&NativeRegroupSession.Faults.Count==0,"land flood no-op must retain every field and session");
                });
                foreach(bool boat in new[]{false,true})foreach(bool city in new[]{false,true})
                Test($"wetland-mode-resolution-{boat}-{city}",()=>
                {
                    var r=Make();r.Tile.Wetland=true;r.Tile.City=city;
                    Check(NativeMimicTransport.WaterEndpoint(r.Tile,boat)==boat,"wetland mode must come from existing body");
                    var land=Row(r).Appearance!;var a=boat?NativeMimicTransport.EmbarkAppearance(r.State,land):land;
                    Check(ReferenceEquals(a,NativeMimicTransport.InterpretAppearance(r.State,a,r.Tile)),"wetland changed independent apparent mode");
                });
                Test("flood-no-op-then-dry-landing-retains-original-identity",()=>
                {
                    var r=Make();Child(r,true);r.Tile.IsWater=false;r.Tile.Wetland=true;Child(r,false);r.Tile.Wetland=false;Child(r,false);
                    Check(r.Tile.unit==r.Land&&Row(r).NativeId==7&&Row(r).LogicalId==8&&Row(r).Transport==null,"subsequent dry landing failed original cargo identity");
                    Check(r.Land.health==10&&r.Land.moved&&r.Land.attacked&&r.Land.xp==4&&Row(r).PaidStars==3&&Row(r).Appearance?.Form=="warrior","landing changed HP/history/investment or disguise");
                });
                Test("normal-port-boarding-still-wraps",()=>{var r=Make();Check(!Child(r,true)&&r.Tile.unit!=r.Land&&r.Tile.unit!.passengerUnit==r.Land,"normal port boarding regressed");});
                Test("flood-child-does-not-bypass-owner-guard",()=>{var r=Make();r.Tile.Wetland=true;r.Land.owner=2;bool threw=false;try{Child(r,true);}catch{threw=true;}Check(threw&&r.Tile.unit==r.Land&&Row(r).Transport==null,"wrong owner gained flood transition authorization");});
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases.Count,assertions=Assertions,tests=Cases,nativeGame="NOT RUN"}));return 0;
            }
            catch(Exception e){Console.Error.WriteLine(e);return 1;}
        }
    }
}
