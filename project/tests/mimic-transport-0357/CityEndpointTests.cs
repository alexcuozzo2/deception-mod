using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;

namespace Deception.Tests
{
    internal static class CityEndpointTests
    {
        // Exercise the real exchange planner/commit boundary. Genuine Warrior
        // traversal remains its actual form even when its appearance is Cloak.
        internal static void Run(Action<Action> test,Action<bool,string> check)
        {
            foreach(string role in new[]{"mimic","genuine-cloak","warrior-disguise"})
            foreach(string cloak in new[]{"cloak","mermaidcloak"})
            foreach(bool swap in new[]{false,true})test(()=>
            {
                foreach(byte cityOwner in new byte[]{0,1,2})
                {
                    Fault.At="";RecruitmentHooks.Reset();NativeRegroupSession.Enabled=NativeRegroupSession.Admitted=true;
                    var state=new GameState{CurrentUnitId=4};NativeRegroupSession.Owned=state;
                    var aTile=new TileData{coordinates=new(1,1)};var bTile=new TileData{coordinates=new(2,1)};
                    state.Map.Tiles.Add(aTile);state.Map.Tiles.Add(bTile);
                    UnitState Unit(uint id,string form,TileData tile)
                    {
                        var data=RecruitmentHooks.Definitions[form];
                        var unit=new UnitState{id=id,type=data.type,UnitData=data,health=RecruitmentCatalog.IsMimicId(form)?(ushort)10:(ushort)100,coordinates=tile.coordinates};
                        tile.unit=unit;return unit;
                    }
                    var a=Unit(3,RecruitmentCatalog.Variant("warrior"),aTile);
                    var b=Unit(4,role=="warrior-disguise"?"warrior":cloak,bTile);
                    LedgerAppearance Art(string form)=>new(form,new("Elyrion","DarkElf"),new("Imperius","Default"),new("Elyrion","DarkElf"),57,100,100,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
                    var source=Art(cloak);var rows=new[]{
                        new LedgerUnit(1,a.id,true,LedgerActualKind.Mimic,true,Art("warrior"),PaidInvestmentKind.Known,3),
                        new LedgerUnit(2,b.id,true,LedgerActualKind.Genuine,role=="warrior-disguise",role=="warrior-disguise"?source:null,PaidInvestmentKind.Known,2)};
                    var ledger=new NativeIdentityLedger(Guid.Parse("ea8e4dd9-caa6-431a-8e85-54ea082c517f"),5,1,rows,Array.Empty<LedgerReceipt>());
                    NativeGameStateEnvelope.Set(state,4,NativeIdentityLedgerCodec.Encode(ledger,4));
                    if(role!="warrior-disguise")source=NativeLedgerState.Capture(state,new(b,true),cloak,false);
                    var intent=new NativeExchangeIntent(ledger.Epoch,1,4,state.CurrentTurn,1,NativeExchangeKind.Regroup,swap,NativePartnerReference.Existing,5,
                        new(1,a.id,a.coordinates.X,a.coordinates.Y),new(2,b.id,b.coordinates.X,b.coordinates.Y),NativeExchangeIntentCodec.AppearanceFingerprint(source));
                    // Target the final square of the participant under test.
                    var target=role=="mimic"?(swap?bTile:aTile):(swap?aTile:bTile);target.City=true;target.owner=cityOwner;
                    string before=TransportStateDump.Dump(state);var snapshot=NativeGameStateEnvelope.Get(state)!;byte[] saved=snapshot.Bytes();
                    bool expected=cityOwner!=2||role=="warrior-disguise";
                    bool allowed=NativeExchangePlanning.TryPlan(state,intent,out var plan,out string why);
                    string context=$"{role}/{cloak}/{(swap?"swap":"keep")}/cityOwner{cityOwner}";
                    check(allowed==expected,"Wrong ordinary city endpoint eligibility: "+context+": "+why);
                    check(TransportStateDump.Dump(state)==before&&ReferenceEquals(NativeGameStateEnvelope.Get(state),snapshot),"Endpoint planning changed authoritative state: "+context);
                    if(allowed)
                    {
                        string type=role=="mimic"?RecruitmentCatalog.Variant(cloak):role=="warrior-disguise"?"warrior":cloak;
                        check((role=="mimic"?plan!.FinalTypeA:plan!.FinalTypeB)==type,"Endpoint used appearance instead of the operative form: "+context);
                    }
                    else
                    {
                        check(why.Contains("Cloak's ordinary movement",StringComparison.Ordinal),"Cloak endpoint test failed for an unrelated guard: "+context+": "+why);
                        check(!NativeExchangeCommit.TryCommit(state,intent,out _,out _),"Commit bypassed rejected Cloak endpoint: "+context);
                        check(TransportStateDump.Dump(state)==before&&NativeGameStateEnvelope.Get(state)!.Bytes().SequenceEqual(saved),"Rejected endpoint published a partial exchange: "+context);
                    }
                }
            });
        }
    }
}
