using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using Tile=Deception.PolyModAdapter.Tile;
using Unit=Deception.PolyModAdapter.Unit;
using State=Deception.PolyModAdapter.State;
namespace Deception.Tests
{
    public static class PlanningFixtureTests
    {
        static int Tests,Assertions;static List<string> Passed=new();
        static readonly Guid Epoch=Guid.Parse("d6cbe84d-68fc-4c05-83d0-9838c9c8e2d5");
        static void Check(bool value,string reason){Assertions++;if(!value)throw new Exception(reason);}
        static void Test(string name,Action run){run();Tests++;Passed.Add(name);}
        static (State State,Unit A,Unit B) Setup(string partner="rider")
        {
            RecruitmentHooks.Initialize();
            foreach(string f in new[]{"centipede","segment"}){string id=RecruitmentCatalog.Variant(f);RecruitmentHooks.Forms[id]=new(f,id,f,10,1,0,0,false);RecruitmentHooks.Profiles[f]=new(){type=f};RecruitmentHooks.Profiles[id]=new(){type=id,health=10};}
            var a=new Unit{id=3,type=RecruitmentCatalog.Variant("warrior"),health=10,coordinates=new(){X=3,Y=2},UnitData=RecruitmentHooks.Data(null!,RecruitmentCatalog.Variant("warrior"))};
            var b=new Unit{id=4,type=partner,health=RecruitmentCatalog.IsMimicId(partner)?10:100,coordinates=new(){X=4,Y=2},UnitData=RecruitmentHooks.Data(null!,partner)};
            var s=new State{CurrentUnitId=4,Map=new(){Tiles=new[]{new Tile{unit=a,coordinates=a.coordinates},new Tile{unit=b,coordinates=b.coordinates}}}};
            var ledger=NativeLedgerState.Bootstrap(s,Epoch);s.Ledger=new(0,NativeIdentityLedgerCodec.Encode(ledger,0));return(s,a,b);
        }
        static NativeExchangeIntent Intent(State s,Unit a,Unit b,bool swap,NativeExchangeKind kind=NativeExchangeKind.Regroup)
        {
            var l=NativeIdentityLedgerCodec.Decode(s.Ledger!.Bytes(),0);var ar=l.Units.Single(x=>x.NativeId==a.id);var br=l.Units.SingleOrDefault(x=>x.NativeId==b.id);
            var source=kind==NativeExchangeKind.Disguise?ar.Appearance!:br?.Appearance??NativeLedgerState.Capture(s,new(b,true),b.type,false);
            return new(l.Epoch,l.NextSequence,0,s.CurrentTurn,s.CurrentPlayer,kind,swap,br is null?NativePartnerReference.Allocate:NativePartnerReference.Existing,l.NextLogicalId,
                new(ar.LogicalId,a.id,a.coordinates.X,a.coordinates.Y),new(br?.LogicalId??l.NextLogicalId,b.id,b.coordinates.X,b.coordinates.Y),NativeExchangeIntentCodec.AppearanceFingerprint(source));
        }
        static NativeExchangePlan Plan(State s,Unit a,Unit b,bool swap,NativeExchangeKind kind=NativeExchangeKind.Regroup)
        {
            string before=JsonSerializer.Serialize(s);var request=Intent(s,a,b,swap,kind);
            Check(NativeExchangePlanning.TryPlan(s,request,out var plan,out var why),why);
            Check(JsonSerializer.Serialize(s)==before,"Read-only planner changed native inputs or ledger");
            Check(plan!.FinalA==new NativeExchangePosition(swap?b.coordinates.X:a.coordinates.X,swap?b.coordinates.Y:a.coordinates.Y),"Wrong initiating endpoint");
            Check(plan.FinalB==new NativeExchangePosition(swap?a.coordinates.X:b.coordinates.X,swap?a.coordinates.Y:b.coordinates.Y),"Wrong partner endpoint");
            Check(plan.CandidateLedger.Units.Single(x=>x.NativeId==(kind==NativeExchangeKind.Regroup?a.id:b.id)).Appearance!.Form==
                (kind==NativeExchangeKind.Regroup?b.type:"warrior"),"Wrong copied appearance");return plan;
        }
        static void Reject(State s,NativeExchangeIntent request,string reason)
        {string before=JsonSerializer.Serialize(s);Check(!NativeExchangePlanning.TryPlan(s,request,out _,out _),reason);Check(JsonSerializer.Serialize(s)==before,"Rejected planner changed native inputs");}
        public static int Run()
        {
            try
            {
                Test("initiator-move-and-partner-readiness-matrix",()=>{
                    foreach(bool moved in new[]{false,true})foreach(bool partnerMoved in new[]{false,true})foreach(bool partnerAttacked in new[]{false,true})foreach(bool swap in new[]{false,true})
                    {var(s,a,b)=Setup();a.moved=moved;b.moved=partnerMoved;b.attacked=partnerAttacked;
                     foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})Plan(s,a,b,swap,kind);
                     Check(a.moved==moved&&!a.attacked&&b.moved==partnerMoved&&b.attacked==partnerAttacked,"Dash eligibility cannot refresh either participant");}
                });
                Test("growing-partners-keep-swap-preserve-age-and-wounds",()=>{
                    foreach(string form in new[]{"dragonegg","babydragon"})foreach(bool swap in new[]{false,true}){
                        var(s,a,b)=Setup(form);b.createdTurn=1;b.health=60;b.moved=b.attacked=true;
                        var plan=Plan(s,a,b,swap);var copied=plan.CandidateLedger.Units.Single(x=>x.NativeId==a.id).Appearance!;
                        Check(copied.GrowthTurns==1&&copied.GrowthDueTurn==4&&copied.Health==60,"growth age/wounds lost by Regroup");
                        Check(plan.FinalMimicCarrierA==RecruitmentCatalog.Variant(form)&&a.health==10,"copy became genuine dragon");
                    }
                });
                Test("owned-city-and-plain-endpoint-matrix",()=>{
                    foreach(bool cityA in new[]{false,true})foreach(bool cityB in new[]{false,true})foreach(bool swap in new[]{false,true})
                    {var(s,a,b)=Setup();s.Map.Tiles[0].City=cityA;s.Map.Tiles[1].City=cityB;b.moved=b.attacked=true;Plan(s,a,b,swap);Check(s.Player.relations.Count==0,"Query inserted a relation");}
                });
                Test("frozen-partner-is-allowed-and-preserved",()=>{
                    foreach(bool swap in new[]{false,true}){var(s,a,b)=Setup();b.effects.Add(UnitEffect.Frozen);var plan=Plan(s,a,b,swap);Check(plan.CandidateLedger.Units.Single(x=>x.NativeId==a.id).Appearance!.Effects.Contains("Frozen"),"Apparent freeze not captured");Check(b.effects.SequenceEqual(new[]{UnitEffect.Frozen}),"Partner freeze changed");}
                });
                Test("spent-or-frozen-initiator-is-rejected",()=>{
                    foreach(bool frozen in new[]{false,true}){var(s,a,b)=Setup();if(frozen)a.effects.Add(UnitEffect.Frozen);else a.attacked=true;Reject(s,Intent(s,a,b,true),"Unavailable initiator was allowed");}
                });
                Test("city-diplomacy-is-separate-from-partner-readiness",()=>{
                    foreach(int relation in new[]{0,1,2,3})foreach(bool swap in new[]{false,true}){var(s,a,b)=Setup();s.Map.Tiles[1].City=true;s.Map.Tiles[1].owner=2;s.Player.relations[2]=new(){State=relation};b.moved=b.attacked=true;
                        if(relation==1||relation==3)Reject(s,Intent(s,a,b,swap),"Peace/cooldown city allowed");else Plan(s,a,b,swap);}
                });
                Test("physical-placement-does-not-grant-or-require-movement-research",()=>{var(s,a,b)=Setup();s.Map.Tiles[1].Terrain="mountain";Plan(s,a,b,true);Check(!s.AllowedTerrain.Contains("mountain"),"Placement granted Climbing movement");s.AllowedTerrain.Add("mountain");Plan(s,a,b,true);});
                Test("diagonal-adjacency-and-out-of-range",()=>{var(s,a,b)=Setup();b.coordinates.Y=3;Plan(s,a,b,true);b.coordinates.Y=4;Reject(s,Intent(s,a,b,true),"Distant partner allowed");});
                Test("enemy-partner-and-inaccessible-tile-rejected",()=>{var(s,a,b)=Setup();var intent=Intent(s,a,b,true);b.owner=2;Reject(s,intent,"Enemy partner allowed");b.owner=1;s.Map.Tiles[1].Explored=false;Reject(s,intent,"Inaccessible partner allowed");});
                Test("linked-or-carried-partner-rejected",()=>{foreach(string condition in new[]{"leader","follower","passenger"}){var(s,a,b)=Setup();var intent=Intent(s,a,b,true);if(condition=="leader")b.Leader=true;if(condition=="follower")b.Follower=true;if(condition=="passenger")b.passengerUnit=new(){id=2};Reject(s,intent,"Compound partner allowed");}});
                Test("solo-centipede-versus-attached-and-orphan-segment",()=>{
                    foreach(bool swap in new[]{false,true}){
                        var(s,a,b)=Setup("centipede");Plan(s,a,b,swap);var request=Intent(s,a,b,swap);b.Follower=true;Reject(s,request,"Linked centipede head allowed");
                        var(t,c,d)=Setup("segment");Reject(t,Intent(t,c,d,swap),"Orphan body segment treated as a head");
                    }
                });
                Test("cancelled-stale-location-or-appearance-rejected",()=>{var(s,a,b)=Setup();var intent=Intent(s,a,b,false);b.health=50;Reject(s,intent,"Stale appearance allowed");b.health=100;b.coordinates.X++;Reject(s,intent,"Moved target accepted");});
                Test("keep-and-swap-have-independent-legality",()=>{
                    // A hypothetical body-specific native terrain lookup tests the planner's two destinations separately.
                    var(s,a,b)=Setup();s.Map.Tiles[0].Terrain="only-rider";s.AllowedTerrain.Add("only-rider");Native.SpecificTerrain=true;
                    try{Plan(s,a,b,false);Reject(s,Intent(s,a,b,true),"Swap ignored true partner traversal");}finally{Native.SpecificTerrain=false;}
                });
                Test("native-birth-spent-genuine-partner-needs-no-refresh",()=>{
                    foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})foreach(bool swap in new[]{false,true})foreach(bool tracked in new[]{false,true})
                    {
                        var(s,a,b)=Setup();b.createdTurn=(ushort)s.CurrentTurn;b.moved=b.attacked=true;
                        if(!tracked){var ledger=NativeIdentityLedgerCodec.Decode(s.Ledger!.Bytes(),0);s.Ledger=new(0,NativeIdentityLedgerCodec.Encode(ledger with{Units=ledger.Units.Where(x=>x.NativeId!=b.id).ToArray()},0));}
                        var request=Intent(s,a,b,swap,kind);string before=JsonSerializer.Serialize(s);
                        Check(NativeExchangePlanning.TryPlan(s,request,out var plan,out var reason),reason);
                        Check(request.PartnerReference==(tracked?NativePartnerReference.Existing:NativePartnerReference.Allocate),"New genuine birth requires nonexistent prior identity");
                        Check(plan!.CandidateLedger.Units.Any(x=>x.Alive&&x.NativeId==b.id)&&b.moved&&b.attacked&&!a.attacked,"Partner omitted or ready Mimic/target flags changed");
                        Check(before==JsonSerializer.Serialize(s),"Read-only newborn eligibility mutated native state/ledger");
                    }
                });
                Test("newborn-mimic-target-allowed-initiator-stays-exhausted",()=>{
                    foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})foreach(bool swap in new[]{false,true})
                    {
                        var(s,a,b)=Setup(RecruitmentCatalog.Variant("warrior"));b.health=10;b.createdTurn=(ushort)s.CurrentTurn;b.moved=b.attacked=true;
                        s.Ledger=new(0,NativeIdentityLedgerCodec.Encode(NativeLedgerState.Bootstrap(s,Epoch),0));
                        Check(NativeExchangePlanning.TryPlan(s,Intent(s,a,b,swap,kind),out _,out var reason),reason);
                        a.createdTurn=(ushort)s.CurrentTurn;a.moved=a.attacked=true;
                        Reject(s,Intent(s,a,b,swap,kind),"Birth-turn initiator gained an exchange action");
                        Check(a.moved&&a.attacked&&b.moved&&b.attacked,"Birth readiness was refreshed by eligibility");
                    }
                });
                Test("camouflage-immediate-research-preserves-existing-and-birth-readiness",()=>{
                    foreach(bool newborn in new[]{false,true})foreach(bool moved in new[]{false,true})
                    {
                        var(s,a,b)=Setup();a.moved=moved;a.attacked=newborn;if(newborn)a.createdTurn=(ushort)s.CurrentTurn;
                        s.Player.HasPropaganda=false;var request=Intent(s,a,b,false,NativeExchangeKind.Disguise);Reject(s,request,"Disguise before Propaganda accepted");
                        uint turn=s.CurrentTurn;var binding=s.Ledger;s.Player.HasPropaganda=true;
                        bool allowed=NativeExchangePlanning.TryPlan(s,request,out _,out var reason);Check(allowed==!newborn,"Research did not respect existing action state: "+reason);
                        Check(a.moved==moved&&a.attacked==newborn&&s.CurrentTurn==turn&&ReferenceEquals(binding,s.Ledger),"Research eligibility refreshed flags/turn/persistent binding");
                    }
                });
                var result=new{tests=Tests,assertions=Assertions,failed=0,names=Passed,scope="Production NativeExchangePlanning and NativeLedgerState with explicit synthetic native ports; no game/native execution"};
                File.WriteAllText(Environment.GetEnvironmentVariable("PLANNING_TEST_OUTPUT")!,JsonSerializer.Serialize(result));return 0;
            }catch(Exception e){File.WriteAllText(Environment.GetEnvironmentVariable("PLANNING_TEST_OUTPUT")!,JsonSerializer.Serialize(new{tests=Tests,assertions=Assertions,failed=1,error=e.ToString()}));return 1;}
        }
    }
}
