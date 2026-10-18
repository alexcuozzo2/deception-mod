using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Deception.Core;

namespace Deception.Tests
{
    public sealed class FixtureNative : INativeRules
    {
        public string EvidenceId => "SYNTHETIC-M0-NATIVE-STUB-NOT-A-GAME-MEASUREMENT";
        public HashSet<string> Disabled { get; } = new();
        public HashSet<Tile> Blocked { get; } = new();
        public readonly Dictionary<string,FormProfile> Forms = new() {
            ["warrior"]=new("warrior",RecruitmentKind.OrdinaryCity,"basic",10,1,2,2,2,true),
            ["rider"]=new("rider",RecruitmentKind.OrdinaryCity,"riding",10,2,2,2,1,true),
            ["giant"]=new("giant",RecruitmentKind.SuperUnit,"",40,1,2,5,4,true),
            ["catapult"]=new("catapult",RecruitmentKind.OrdinaryCity,"mathematics",10,1,2,4,0,true),
            ["healer"]=new("healer",RecruitmentKind.OrdinaryCity,"philosophy",10,1,2,0,1,false),
            ["raft"]=new("raft",RecruitmentKind.Ship,"fishing",10,2,2,0,1,false),
            ["bomber"]=new("bomber",RecruitmentKind.Ship,"navigation",10,2,2,3,2,true),
            ["rammer"]=new("rammer",RecruitmentKind.Ship,"ramming",10,3,2,3,3,true),
            ["dagger"]=new("dagger",RecruitmentKind.SpawnOnly,"diplomacy",10,1,2,2,1,true),
            ["mimic"]=new("mimic",RecruitmentKind.BareMimic,"deception",1,1,1,0,0,false),
            ["centipede"]=new("centipede",RecruitmentKind.SuperUnit,"",20,2,2,4,3,true,Structure:BodyStructure.CentipedeHead),
            ["segment"]=new("segment",RecruitmentKind.SpawnOnly,"",10,1,1,2,2,true,Structure:BodyStructure.CentipedeSegment),
            ["mooni"]=new("mooni",RecruitmentKind.SpecialCity,"frost",10,1,2,0,1,false,"ice","visible")
        };
        public FormProfile Profile(string formId) => Forms.TryGetValue(formId,out var form) ? form : throw new RuleViolation("Unknown native profile "+formId);
        public bool HasFormAdapter(string formId) => Forms.ContainsKey(formId) && !Disabled.Contains(formId);
        public TurnAllowance RecruitmentAllowance(int player,long playerTurn) => new() { Player=player,PlayerTurn=playerTurn,MovementSpent=true,ActionSpent=true };
        public bool CanExchangeOccupy(UnitRecord unit,Tile destination,AppearanceRecord appearance) => !Blocked.Contains(destination);
        public int Distance(Tile a,Tile b) => Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y); // Fixture metric only!
        public HashSet<(int,ResourceKind)> UnavailableHarvests { get; } = new();
        public bool CanHarvest(PlayerRecord player,ResourceRecord resource) => !UnavailableHarvests.Contains((player.Id,resource.Kind));
        public int HarvestPopulation(PlayerRecord player,ResourceRecord resource) => 2; // Fixture yield only!
    }
    public class FakeNativeJson { public new virtual string ToString() => "{\"native\":true}"; }
    public static partial class KernelTests
    {
        static readonly FixtureNative N=new();
        static readonly List<object> Results=new();
        static List<object> Captures=new();
        static int Count,Failed;
        static void Assert(bool ok,string message="Assertion failed") { if(!ok) throw new Exception(message); }
        static void Reject(Action action,string? part=null)
        {
            try { action(); } catch(RuleViolation e) { if(part is not null) Assert(e.Message.Contains(part),e.Message);return; }
            throw new Exception("Expected rule rejection");
        }
        static void Test(string id,string[] cases,Action test)
        {
            Count++;Captures=new();
            try { test();Results.Add(new{id,relatedDesignCases=cases,scope="MODEL SUBCHECK ONLY",status="PASS",captures=Captures});Console.WriteLine("PASS "+id); }
            catch(Exception e) { Failed++;Results.Add(new{id,relatedDesignCases=cases,scope="MODEL SUBCHECK ONLY",status="FAIL",error=e.ToString(),captures=Captures});Console.WriteLine("FAIL "+id+": "+e); }
        }
        static void Capture(World w,string step)
        {
            Captures.Add(new{step,actual=JsonNode.Parse(Codec.Encode(w)),
                p1=JsonNode.Parse(Observation.Capture(w,1)),p2=JsonNode.Parse(Observation.Capture(w,2))});
        }
        static World World()
        {
            var w=new World();
            for(int p=1;p<=2;p++)
            {
                w.Players[p]=new PlayerRecord {Id=p,OrdinaryTribe=true,TribeId="fixture-ordinary",Stars=20,Turn=7,
                    NativeRosterForms=new(){"warrior","rider","giant","catapult","healer"},
                    Technologies=new(){"deception","propaganda","organization","fishing","hunting"},
                    UnlockedRecruitmentForms=new(){"warrior","rider","healer"}};
                var mask=new ObservationMask();
                for(int x=-5;x<=20;x++)for(int y=-5;y<=20;y++)mask.AccessibleTiles.Add(new Tile(x,y).Key);
                w.Masks[p]=mask;
                w.Cities["city"+p]=new CityRecord{Id="city"+p,Owner=p,Position=new Tile(p==1?0:10,0),SupportCapacity=20};
            }
            return w;
        }
        static UnitRecord Add(World w,ActualKind kind,string actual,string apparent,int owner=1,int x=1,int y=0)
        {
            long id=w.NextLogicalId++;
            var real=N.Profile(actual);var app=N.Profile(apparent);
            var u=new UnitRecord{LogicalId=id,Kind=kind,ActualBody=actual,Structure=real.Structure,ActualHp=kind==ActualKind.Mimic?1:real.MaxHp,
                ActualMaxHp=kind==ActualKind.Mimic?1:real.MaxHp,PermanentOwner=owner,Commander=owner,HomeCity="city"+owner,
                Position=new Tile(x,y),IndependentAppearance=kind==ActualKind.Mimic || actual!=apparent,
                Appearance=new AppearanceRecord{Body=apparent,Structure=app.Structure,Hp=app.MaxHp,MaxHp=app.MaxHp},ActualInvestment=kind==ActualKind.Mimic?3:5,
                Allowance=new TurnAllowance{Player=owner,PlayerTurn=w.Players[owner].Turn}};
            w.Units[id]=u;w.Cities[u.HomeCity].SupportUsed++;
            foreach(var mask in w.Masks.Values) mask.DetectedUnits.Add(id);
            return u;
        }
        static ResourceRecord Resource(UnitRecord unit,ResourceKind kind=ResourceKind.Fruit,int? territory=null) =>
            new(){Position=unit.Position,Kind=kind,HarvestTechnology="organization",TerritoryOwner=territory};
        static TurnAllowance Spent(UnitRecord u) { var b=u.Allowance.Copy();b.EndTurn();return b; }
        public static int Run()
        {
            Count=Failed=0;Results.Clear();
            BranchAmendmentTests();CounterintelligenceTests();NativeRecruitmentRosterTests();NativeRecruitmentRosterReentryTests();
            Test("A11-001-revised-tree-topology",new[]{"A11"},()=> {
                Assert(Rules.Branch.Length==5);Assert(Rules.Branch.Single(t=>t.Id=="propaganda").Parent=="deception" && Rules.Branch.Single(t=>t.Id=="propaganda").Tier==2);
                Assert(Rules.Branch.Single(t=>t.Id=="intelligence").Parent=="deception" && Rules.Branch.Single(t=>t.Id=="intelligence").Tier==2);
                Assert(Rules.Branch.Single(t=>t.Id=="espionage").Parent=="propaganda" && Rules.Branch.Single(t=>t.Id=="counterintelligence").Parent=="intelligence");
                Assert(Rules.Branch.Single(t=>t.Parent==null).Id=="deception");
            });
            Test("M0-002-fixed-custom-values",new[]{"DEC-002","DEC-003"},()=> {
                Assert(Rules.MimicCost==1 && Rules.MimicHp==1 && Rules.SmuggleCost==2 && Rules.ExchangeRange==1);
                Assert(Rules.Branch.Select(t=>t.Tier).SequenceEqual(new[]{1,2,2,3,3}));
            });
            Test("M0-003-recruit-actual-and-observation",new[]{"DEC-003"},()=> {
                var w=World();w.Players[1].Stars=1;Capture(w,"before");
                var result=Rules.RecruitMimic(w,"recruit",1,"city1","warrior",N);var a=result.State;var u=a.Units[result.UnitId!.Value];
                // Simulates the separate native visibility/discovery callback.
                foreach(var mask in a.Masks.Values) mask.DetectedUnits.Add(u.LogicalId);
                Assert(a.Players[1].Stars==0 && a.Cities["city1"].SupportUsed==1 && u.ActualHp==1 && u.ActualBody=="mimic" && u.ActualInvestment==1);
                Assert(u.Allowance.ActionSpent && u.Allowance.MovementSpent && u.IndependentAppearance);
                Assert(Observation.Project(a,u,2)!.Public.Body=="warrior" && Observation.Project(a,u,2)!.Private==null);
                Assert(Observation.Project(a,u,1)!.Private!.MimicBadge);Capture(a,"recruited + synthetic discovery");
                Assert(w.Players[1].Stars==1 && w.Units.Count==0,"Input mutated");
            });
            Test("M0-004-cancel-is-no-commit",new[]{"DEC-004"},()=> { var w=World();string s=Codec.Encode(w);var picker=N.Profile("rider");Assert(picker.Id=="rider");Assert(Codec.Encode(w)==s);Capture(w,"private picker cancelled"); });
            Test("M0-005-unresearched-denied",new[]{"DEC-003"},()=> {var w=World();w.Players[1].Technologies.Remove("deception");Reject(()=>Rules.RecruitMimic(w,"r",1,"city1","warrior",N));Assert(w.Units.Count==0);});
            Test("M0-006-propaganda-is-not-real-tech",new[]{"DEC-005"},()=> {var w=World();var p=w.Players[1];p.Technologies.Remove("propaganda");Assert(!Rules.CanRecruitAppearance(p,N.Profile("catapult"),N));p.Technologies.Add("propaganda");Assert(Rules.CanRecruitAppearance(p,N.Profile("catapult"),N));Assert(!p.Technologies.Contains("mathematics"));});
            Test("A11-002-super-and-spawn-recruitment-excluded",new[]{"A11"},()=> {var p=World().Players[1];p.NativeRosterForms.UnionWith(new[]{"raft","bomber","dagger","mimic"});p.Technologies.Add("propaganda");foreach(string f in new[]{"raft","bomber","dagger","mimic"})Assert(!Rules.CanRecruitAppearance(p,N.Profile(f),N));Assert(!Rules.CanRecruitAppearance(p,N.Profile("giant"),N));p.Technologies.Remove("propaganda");p.UnlockedRecruitmentForms.Add("giant");Assert(!Rules.CanRecruitAppearance(p,N.Profile("giant"),N));});
            Test("M0-008-unimplemented-form-not-offered",new[]{"DEC-006","DEC-179"},()=> {var n=new FixtureNative();n.Disabled.Add("mooni");var p=World().Players[1];p.NativeRosterForms.Add("mooni");p.Technologies.Add("propaganda");Assert(!Rules.CanRecruitAppearance(p,n.Profile("mooni"),n));});
            Test("M0-009-mimic-mobility-not-weapon",new[]{"DEC-007","A44"},()=> {
                var w=World();var u=Add(w,ActualKind.Mimic,"mimic","rider");
                foreach(string form in new[]{"rider","mooni"}){u.Appearance.Body=form;var c=CapabilityResolver.Resolve(u,N);
                    Assert(c.Movement==N.Profile(form).Movement && !c.CanDamage && c.Attack==0 && c.Defence==0);
                    Assert(c.Sight==N.Profile("mimic").Sight && c.TerrainProfile=="none","Mimic borrowed sight or terrain powers");}
                Capture(w,"Mimic retains movement but no Scout or terrain passive");});
            Test("M0-010-positive-damage-kills-once",new[]{"DEC-008"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","giant");var a=Rules.PositiveDamage(w,"damage",u.LogicalId,1,N).State;Assert(!a.Units[u.LogicalId].Alive && a.Units[u.LogicalId].ActualHp==0);Assert(a.Cities["city1"].SupportUsed==0);Assert(Observation.Project(a,a.Units[u.LogicalId],2)==null);var b=Rules.PositiveDamage(a,"damage",u.LogicalId,1,N);Assert(b.Replayed && b.State.Cities["city1"].SupportUsed==0);Capture(a,"dead, not a living naked Mimic");});
            Test("M0-011-genuine-mobility-remains-actual",new[]{"DEC-009","A44"},()=> {var w=World();var u=Add(w,ActualKind.Genuine,"giant","rider");var c=CapabilityResolver.Resolve(u,N);Assert(c.Movement==N.Profile("giant").Movement && c.Attack==N.Profile("giant").Attack);
                u.Appearance.Body="mooni";Assert(CapabilityResolver.Resolve(u,N).TerrainProfile==N.Profile("giant").TerrainProfile,"Mask granted real terrain power");
                u.ActualBody="mooni";u.Appearance.Body="warrior";Assert(CapabilityResolver.Resolve(u,N).TerrainProfile=="ice","Mask removed genuine underlying terrain power");});
            Test("M0-012-mode-toggle-without-timing",new[]{"DEC-010","DEC-014"},()=> {var w=World();var u=Add(w,ActualKind.Genuine,"giant","warrior");string s=Codec.Encode(w);var panel=new Selection();panel.Click(w,1,u.LogicalId);Assert(panel.Mode==PanelMode.Actual);panel.Click(w,1,u.LogicalId);Assert(panel.Mode==PanelMode.Appearance);panel.Click(w,1,u.LogicalId);Assert(panel.Mode==PanelMode.Actual);Assert(Codec.Encode(w)==s);});
            Test("M0-013-mode-target-and-reset",new[]{"DEC-011"},()=> {var w=World();var a=Add(w,ActualKind.Genuine,"giant","warrior");var b=Add(w,ActualKind.Mimic,"mimic","warrior",x:2);var panel=new Selection();panel.Click(w,1,a.LogicalId);panel.Click(w,1,a.LogicalId);panel.Click(w,1,b.LogicalId,true);Assert(panel.Unit==a.LogicalId && panel.Mode==PanelMode.Appearance);panel.Click(w,1,b.LogicalId);Assert(panel.Mode==PanelMode.Actual && panel.InspectionMode==PanelMode.Appearance);panel.Reset();Assert(panel.Unit==null);});
            Test("M0-014-enemy-readiness-equivalence",new[]{"DEC-012","DEC-178"},()=> {var w=World();var u=Add(w,ActualKind.Genuine,"warrior","warrior");var a=Observation.Capture(w,2);u.Allowance.EndTurn();Assert(Observation.Capture(w,2)==a);Capture(w,"exhaustion not public");});
            Test("M0-015-same-type-independent",new[]{"DEC-013"},()=> {var w=World();var u=Add(w,ActualKind.Genuine,"warrior","warrior");u.IndependentAppearance=true;var s=new Selection();s.Click(w,1,u.LogicalId);s.Click(w,1,u.LogicalId);Assert(s.Mode==PanelMode.Appearance);Assert(Codec.Decode(Codec.Encode(w)).Units[u.LogicalId].IndependentAppearance);});
            Test("M0-016-positive-damage-reveals-genuine",new[]{"DEC-017"},()=> {var w=World();var u=Add(w,ActualKind.Genuine,"giant","warrior");var a=Rules.PositiveDamage(w,"d",u.LogicalId,1,N).State;var t=a.Units[u.LogicalId];Assert(t.ActualHp==39 && !t.IndependentAppearance && t.Appearance.Body=="giant" && t.Appearance.Hp==39);Capture(a,"positive damage");});
            Test("M0-017-zero-loss-preserves-mask",new[]{"DEC-018"},()=> {var w=World();var u=Add(w,ActualKind.Genuine,"giant","warrior");var a=Rules.PositiveDamage(w,"zero",u.LogicalId,0,N).State;Assert(a.Units[u.LogicalId].IndependentAppearance && a.Units[u.LogicalId].ActualHp==40);});
            Test("M0-018-nondamaging-status-preserves-mask",new[]{"DEC-019"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");var a=Rules.ApplyNondamagingStatus(w,"freeze",u.LogicalId,"frozen").State;Assert(a.Units[u.LogicalId].ActualHp==1 && a.Units[u.LogicalId].Appearance.Body=="warrior" && a.Units[u.LogicalId].ActualStatuses.Contains("frozen"));});
            Test("M0-019-deep-snapshot-not-shared",new[]{"DEC-022"},()=> {var a=new AppearanceRecord{Body="warrior",Hp=6,MaxHp=10,Statuses=new(){"poison"}};var b=a.Copy();b.Hp=10;b.Statuses.Clear();Assert(a.Hp==6 && a.Statuses.Count==1);});
            Test("M0-020-feign-heal-actual-full",new[]{"DEC-023"},()=> {var w=World();var caster=Add(w,ActualKind.Mimic,"mimic","healer");var t=Add(w,ActualKind.Genuine,"warrior","warrior",x:2);t.Appearance.Hp=4;t.IndependentAppearance=true;var a=Rules.Heal(w,"heal",1,caster.LogicalId,t.LogicalId,4,true,true,Spent(caster),N).State;Assert(a.Units[t.LogicalId].ActualHp==10 && a.Units[t.LogicalId].Appearance.Hp==8 && a.Units[caster.LogicalId].Allowance.ActionSpent);Capture(a,"feint heals only appearance");});
            Test("M0-021-real-heal-clamps-independently",new[]{"DEC-024"},()=> {var w=World();var caster=Add(w,ActualKind.Genuine,"healer","warrior");var t=Add(w,ActualKind.Genuine,"warrior","warrior",x:2);t.ActualHp=4;t.Appearance.Hp=8;t.IndependentAppearance=true;var a=Rules.Heal(w,"heal",1,caster.LogicalId,t.LogicalId,4,false,true,Spent(caster),N).State;Assert(a.Units[t.LogicalId].ActualHp==8 && a.Units[t.LogicalId].Appearance.Hp==10 && a.Units[t.LogicalId].IndependentAppearance);Assert(!a.Units[caster.LogicalId].IndependentAppearance);Capture(a,"real 8/10; apparent 10/10");
                var selfWorld=World();var self=Add(selfWorld,ActualKind.Genuine,"healer","warrior");self.ActualHp=2;self.Appearance.Hp=9;
                var healed=Rules.Heal(selfWorld,"self",1,self.LogicalId,self.LogicalId,4,false,true,Spent(self),N).State.Units[self.LogicalId];
                Assert(healed.ActualHp==6 && healed.Appearance.Hp==6 && !healed.IndependentAppearance);});
            Test("M0-022-one-budget-between-menus",new[]{"DEC-027"},()=> {var w=World();var c=Add(w,ActualKind.Genuine,"healer","healer");c.IndependentAppearance=true;var t=Add(w,ActualKind.Genuine,"warrior","warrior",x:2);t.Appearance.Hp=1;var a=Rules.Heal(w,"h1",1,c.LogicalId,t.LogicalId,1,true,true,Spent(c),N).State;Reject(()=>Rules.Heal(a,"h2",1,c.LogicalId,t.LogicalId,1,false,true,Spent(c),N));});
            Test("M0-023-real-healing-mimic-stays-one",new[]{"DEC-028"},()=> {var w=World();var c=Add(w,ActualKind.Genuine,"healer","healer");var t=Add(w,ActualKind.Mimic,"mimic","warrior",x:2);t.Appearance.Hp=4;var a=Rules.Heal(w,"h",1,c.LogicalId,t.LogicalId,4,false,true,Spent(c),N).State;Assert(a.Units[t.LogicalId].ActualHp==1 && a.Units[t.LogicalId].Appearance.Hp==8);});
            Test("M0-024-regroup-exhausted-partner-preserved",new[]{"DEC-032","DEC-035","DEC-037"},()=> {var w=World();var i=Add(w,ActualKind.Mimic,"mimic","warrior");var p=Add(w,ActualKind.Genuine,"giant","rider",x:2);p.Allowance.EndTurn();var state=JsonSerializer.Serialize(p.Allowance);var a=Rules.Exchange(w,"e",1,i.LogicalId,p.LogicalId,ExchangeKind.Regroup,true,N).State;Assert(a.Units[i.LogicalId].Appearance.Body=="rider" && a.Units[i.LogicalId].Position==p.Position);Assert(JsonSerializer.Serialize(a.Units[p.LogicalId].Allowance)==state && a.Units[p.LogicalId].ActualBody=="giant");Capture(a,"regroup swap");});
            Test("M0-025-frozen-partner-remains-frozen",new[]{"DEC-038"},()=> {var w=World();var i=Add(w,ActualKind.Mimic,"mimic","warrior");var p=Add(w,ActualKind.Genuine,"giant","giant",x:2);p.ActualStatuses.Add("frozen");var a=Rules.Exchange(w,"e",1,i.LogicalId,p.LogicalId,ExchangeKind.Disguise,true,N).State;Assert(a.Units[p.LogicalId].ActualStatuses.Contains("frozen") && !a.Units[p.LogicalId].Allowance.ActionSpent);});
            Test("M0-026-frozen-initiator-rejected",new[]{"DEC-039"},()=> {var w=World();var i=Add(w,ActualKind.Mimic,"mimic","warrior");var p=Add(w,ActualKind.Genuine,"giant","giant",x:2);i.ActualStatuses.Add("frozen");string s=Codec.Encode(w);Reject(()=>Rules.Exchange(w,"e",1,i.LogicalId,p.LogicalId,ExchangeKind.Regroup,true,N));Assert(s==Codec.Encode(w));});
            Test("A50-002-moved-mimic-can-exchange-once",new[]{"A44"},()=> {
                foreach(var kind in new[]{ExchangeKind.Regroup,ExchangeKind.Disguise})foreach(bool swap in new[]{false,true})
                {var w=World();var i=Add(w,ActualKind.Mimic,"mimic","rider");var p=Add(w,ActualKind.Genuine,"warrior","warrior",x:2);
                 i.Allowance.MovementSpent=true;string before=Codec.Encode(w);var result=Rules.Exchange(w,"move-then-exchange",1,i.LogicalId,p.LogicalId,kind,swap,N).State;
                 Assert(Codec.Encode(w)==before&&result.Units[i.LogicalId].Allowance.ActionSpent&&result.Units[i.LogicalId].Allowance.MovementSpent);
                 Reject(()=>Rules.Exchange(result,"second-exchange",1,i.LogicalId,p.LogicalId,kind,swap,N));}
            });
            Test("A50-003-moved-mimic-can-feign-support-once",new[]{"A44"},()=> {
                var w=World();var c=Add(w,ActualKind.Mimic,"mimic","healer");var t=Add(w,ActualKind.Genuine,"warrior","warrior",x:2);t.Appearance.Hp=1;
                c.Allowance.MovementSpent=true;string before=Codec.Encode(w);var result=Rules.Heal(w,"move-then-heal",1,c.LogicalId,t.LogicalId,1,true,true,Spent(c),N).State;
                Assert(Codec.Encode(w)==before&&result.Units[t.LogicalId].Appearance.Hp==2&&result.Units[c.LogicalId].Allowance.ActionSpent&&result.Units[c.LogicalId].Allowance.MovementSpent);
                Reject(()=>Rules.Heal(result,"second-heal",1,c.LogicalId,t.LogicalId,1,true,true,Spent(c),N));
                var genuine=World();var g=Add(genuine,ActualKind.Genuine,"healer","healer");var target=Add(genuine,ActualKind.Genuine,"warrior","warrior",x:2);target.Appearance.Hp=1;g.Allowance.MovementSpent=true;
                Assert(Rules.Heal(genuine,"native-dash-heal",1,g.LogicalId,target.LogicalId,1,true,true,Spent(g),N).State.Units[target.LogicalId].Appearance.Hp==2,"Native-authorized genuine support was removed");
            });
            Test("M0-027-invalid-exchange-is-atomic",new[]{"DEC-043","DEC-044"},()=> {var w=World();var i=Add(w,ActualKind.Mimic,"mimic","warrior");var p=Add(w,ActualKind.Genuine,"giant","giant",x:2);var n=new FixtureNative();n.Blocked.Add(p.Position);string s=Codec.Encode(w);Reject(()=>Rules.Exchange(w,"e",1,i.LogicalId,p.LogicalId,ExchangeKind.Regroup,true,n));Assert(s==Codec.Encode(w));});
            Test("M0-028-range-not-increased-by-propaganda",new[]{"DEC-002"},()=> {var w=World();w.Players[1].Technologies.Add("propaganda");var i=Add(w,ActualKind.Mimic,"mimic","warrior");var p=Add(w,ActualKind.Genuine,"giant","giant",x:3);Reject(()=>Rules.Exchange(w,"e",1,i.LogicalId,p.LogicalId,ExchangeKind.Regroup,false,N),"out of range");});
            Test("M0-029-keep-still-spends-initiator",new[]{"DEC-031"},()=> {var w=World();var i=Add(w,ActualKind.Mimic,"mimic","warrior");var p=Add(w,ActualKind.Genuine,"giant","rider",x:2);var a=Rules.Exchange(w,"e",1,i.LogicalId,p.LogicalId,ExchangeKind.Regroup,false,N).State;Assert(a.Units[i.LogicalId].Position==i.Position && a.Units[i.LogicalId].Allowance.ActionSpent && !a.Units[p.LogicalId].Allowance.ActionSpent);});
            Test("M0-030-smuggle-native-home-and-price",new[]{"DEC-093"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");w.Resources[u.Position.Key]=Resource(u);var a=Rules.Smuggle(w,"s",1,u.LogicalId,N).State;Assert(a.Players[1].Stars==18 && a.Cities["city1"].Population==2 && a.Resources.Count==0 && a.Units[u.LogicalId].Allowance.ActionSpent && a.Units[u.LogicalId].Allowance.MovementSpent);Capture(a,"harvest committed");});
            Test("A50-004-dash-smuggle-spends-one-action",new[]{"A50"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","rider");u.Allowance.MovementSpent=true;w.Resources[u.Position.Key]=Resource(u);string s=Codec.Encode(w);var done=Rules.Smuggle(w,"s",1,u.LogicalId,N).State;Assert(Codec.Encode(w)==s&&done.Players[1].Stars==18&&done.Cities["city1"].Population==2&&done.Units[u.LogicalId].Allowance.MovementSpent&&done.Units[u.LogicalId].Allowance.ActionSpent);Reject(()=>Rules.Smuggle(done,"second-smuggle",1,u.LogicalId,N));});
            Test("M0-032-delivery-can-immediately-smuggle",new[]{"DEC-094"},()=> {var w=World();var a=Add(w,ActualKind.Mimic,"mimic","rider",x:1);var b=Add(w,ActualKind.Mimic,"mimic","warrior",x:2);w.Resources[b.Position.Key]=Resource(b);var delivered=Rules.Exchange(w,"deliver",1,b.LogicalId,a.LogicalId,ExchangeKind.Regroup,true,N).State;Assert(delivered.Units[a.LogicalId].Allowance.BothUnused);var done=Rules.Smuggle(delivered,"harvest",1,a.LogicalId,N).State;Assert(done.Cities["city1"].Population==2);Capture(done,"exchange delivered ready Mimic then Smuggle");});
            Test("M0-033-home-owned-by-enemy-kept",new[]{"DEC-099","DEC-100"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");w.Cities["city1"].Owner=2;w.Resources[u.Position.Key]=Resource(u);var a=Rules.Smuggle(w,"s",1,u.LogicalId,N).State;Assert(a.Cities["city1"].Population==2 && a.Players[1].Stars==18 && a.PopulationNotices.Single().Owner==2 && a.PopulationNotices.Single().DeferredToOwnerTurn);});
            Test("M0-034-live-native-home-not-origin",new[]{"DEC-098"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");u.HomeCity="city2";w.Cities["city1"].SupportUsed--;w.Cities["city2"].SupportUsed++;w.Resources[u.Position.Key]=Resource(u);var a=Rules.Smuggle(w,"s",1,u.LogicalId,N).State;Assert(a.Cities["city1"].Population==0 && a.Cities["city2"].Population==2);});
            Test("M0-035-missing-home-rejects-atomically",new[]{"DEC-103"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");u.HomeCity="absent";w.Resources[u.Position.Key]=Resource(u);string s=Codec.Encode(w);Reject(()=>Rules.Smuggle(w,"s",1,u.LogicalId,N),"home");Assert(Codec.Encode(w)==s);});
            Test("M0-036-treaty-ordinary-blocked-rogue-allowed",new[]{"DEC-101"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");w.Players[1].Treaties.Add(2);w.Resources[u.Position.Key]=Resource(u,territory:2);Reject(()=>Rules.Smuggle(w,"s",1,u.LogicalId,N));u.Rogue=true;u.ReturnOwner=2;u.PermanentOwner=2;u.HomeCity="city2";w.Cities["city1"].SupportUsed--;w.Cities["city2"].SupportUsed++;var a=Rules.Smuggle(w,"s",1,u.LogicalId,N).State;Assert(a.Cities["city2"].Population==2 && a.Players[1].Treaties.Contains(2));});
            Test("M0-037-starfish-is-not-smuggle",new[]{"DEC-104"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");w.Resources[u.Position.Key]=Resource(u,ResourceKind.Starfish);Reject(()=>Rules.Smuggle(w,"s",1,u.LogicalId,N));});
            Test("M0-038-real-tech-required-for-harvest",new[]{"DEC-093","DEC-104"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");w.Resources[u.Position.Key]=Resource(u);w.Players[1].Technologies.Remove("organization");w.Players[1].Technologies.Add("propaganda");Reject(()=>Rules.Smuggle(w,"s",1,u.LogicalId,N));});
            Test("M0-039-smuggle-exactly-once-after-serialization",new[]{"DEC-172"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");w.Resources[u.Position.Key]=Resource(u);var a=Rules.Smuggle(w,"s",1,u.LogicalId,N).State;var b=Rules.Smuggle(Codec.Decode(Codec.Encode(a)),"s",1,u.LogicalId,N);Assert(b.Replayed && Codec.Encode(a)==Codec.Encode(b.State));});
            Test("M0-040-conflicting-command-id-rejected",new[]{"DEC-172","DEC-180"},()=> {var w=World();var a=Add(w,ActualKind.Genuine,"giant","giant");var b=Add(w,ActualKind.Genuine,"giant","giant",x:2);var done=Rules.PositiveDamage(w,"d",a.LogicalId,1,N).State;Reject(()=>Rules.PositiveDamage(done,"d",b.LogicalId,1,N),"different payload");});
            Test("M0-041-recruit-retry-keeps-original-id",new[]{"DEC-003","DEC-172"},()=> {var w=World();var first=Rules.RecruitMimic(w,"r",1,"city1","warrior",N);var second=Rules.RecruitMimic(first.State,"r",1,"city1","warrior",N);Assert(second.Replayed && first.UnitId==second.UnitId && second.State.Units.Count==1);});
            Test("M0-042-fog-blocks-inspection-and-command",new[]{"DEC-142","DEC-177"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");w.Resources[u.Position.Key]=Resource(u);w.Masks[1].AccessibleTiles.Remove(u.Position.Key);Assert(Observation.Project(w,u,1)==null);Reject(()=>Rules.Smuggle(w,"s",1,u.LogicalId,N));});
            Test("M0-043-fog-persists-in-model-save",new[]{"DEC-174"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");w.Masks[1].AccessibleTiles.Remove(u.Position.Key);var a=Codec.Decode(Codec.Encode(w));Assert(!Observation.Accessible(a,1,u.Position) && Observation.Accessible(a,2,u.Position));});
            Test("M0-044-rogue-old-owner-truth-not-readiness",new[]{"DEC-112","DEC-178"},()=> {var w=World();var u=Add(w,ActualKind.Genuine,"giant","warrior",owner:2);u.Rogue=true;u.ReturnOwner=2;u.Commander=1;u.Allowance.Player=1;var old=Observation.Project(w,u,2)!;Assert(old.Private!.Body=="giant" && old.Private.Readiness==null && !old.Private.CanCommand);Assert(Observation.Project(w,u,1)!.Private!.Readiness!=null);});
            Test("M0-045-support-rejects-rogue-recipient",new[]{"DEC-114"},()=> {var w=World();var c=Add(w,ActualKind.Genuine,"healer","healer");var t=Add(w,ActualKind.Genuine,"warrior","warrior",x:2);t.Rogue=true;t.ReturnOwner=2;Reject(()=>Rules.Heal(w,"h",1,c.LogicalId,t.LogicalId,4,true,true,Spent(c),N),"Rogue");});
            Test("M0-046-genuine-upgrade-preserves-mask",new[]{"DEC-160"},()=> {var w=World();var u=Add(w,ActualKind.Genuine,"warrior","warrior");u.ActualVessel="raft";u.Appearance.Vessel="raft";u.IndependentAppearance=true;var a=Rules.GenuineUpgrade(w,"upgrade",1,u.LogicalId,"bomber",5,true,u.Allowance.Copy()).State;Assert(a.Units[u.LogicalId].ActualVessel=="bomber" && a.Units[u.LogicalId].Appearance.Vessel=="raft" && a.Units[u.LogicalId].IndependentAppearance && a.Players[1].Stars==15);Capture(a,"actual Bomber, apparent Raft");});
            Test("M0-047-upgrade-rejects-refresh",new[]{"DEC-160","DEC-171"},()=> {var w=World();var u=Add(w,ActualKind.Genuine,"warrior","warrior");u.ActualVessel="raft";u.Allowance.MovementSpent=true;var after=u.Allowance.Copy();after.MovementSpent=false;Reject(()=>Rules.GenuineUpgrade(w,"up",1,u.LogicalId,"bomber",5,true,after),"refreshed");});
            Test("M0-048-mimic-cannot-buy-real-warship",new[]{"DEC-073"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");Reject(()=>Rules.GenuineUpgrade(w,"up",1,u.LogicalId,"bomber",5,true,u.Allowance.Copy()));});
            Test("M0-049-mimic-enemy-city-rule-not-old-prohibition",new[]{"DEC-105","DEC-110"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");Assert(Rules.CanCapture(u,true));Assert(!Rules.CanCapture(u,false));u.Rogue=true;u.ReturnOwner=1;Assert(!Rules.CanCapture(u,true));});
            Test("M0-050-genuine-vs-mimic-public-equivalence",new[]{"DEC-178"},()=> {var a=World();Add(a,ActualKind.Genuine,"warrior","warrior");var b=World();var m=Add(b,ActualKind.Mimic,"mimic","warrior");m.Allowance.EndTurn();Assert(Observation.Capture(a,2)==Observation.Capture(b,2));Assert(Observation.Capture(a,1)!=Observation.Capture(b,1));Capture(a,"genuine-world");Capture(b,"mimic-world");});
            Test("M0-051-state-rejects-mimic-two-hp",new[]{"DEC-008"},()=> {var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");u.ActualHp=u.ActualMaxHp=2;Reject(()=>Codec.Encode(w));});
            Test("M0-052-state-rejects-future-schema",new[]{"DEC-171"},()=> {var w=World();w.SchemaVersion=99;Reject(()=>Codec.Encode(w));});
            Test("M0-053-acquisition-and-followups-persist",new[]{"DEC-107","DEC-171"},()=> {var w=World();var u=Add(w,ActualKind.Genuine,"warrior","warrior");u.LastAcquiredTurn[1]=7;u.Allowance.ActionSpent=true;u.Allowance.NativeFollowUps.Add("fixture-persist-1");var a=Codec.Decode(Codec.Encode(w));Assert(a.Units[u.LogicalId].LastAcquiredTurn[1]==7 && a.Units[u.LogicalId].Allowance.NativeFollowUps.Single()=="fixture-persist-1");});
            Test("M0-054-mixed-command-deterministic-model-replay",new[]{"DEC-173","DEC-180"},()=> {var w=World();var i=Add(w,ActualKind.Mimic,"mimic","warrior");var p=Add(w,ActualKind.Mimic,"mimic","rider",x:2);w.Resources[i.Position.Key]=Resource(i);var a=Rules.Exchange(w,"e",1,i.LogicalId,p.LogicalId,ExchangeKind.Disguise,true,N).State;var uninterrupted=Rules.Smuggle(a,"s",1,p.LogicalId,N).State;var reloaded=Rules.Smuggle(Codec.Decode(Codec.Encode(a)),"s",1,p.LogicalId,N).State;Assert(Codec.Encode(uninterrupted)==Codec.Encode(reloaded));Assert(Observation.Capture(uninterrupted,2)==Observation.Capture(reloaded,2));Capture(uninterrupted,"uninterrupted");Capture(reloaded,"save inserted");});
            Test("A40-001-actual-invisibility-survives-public-mask",new[]{"UI-A40"},()=> {
                var w=World();var u=Add(w,ActualKind.Genuine,"healer","rider");u.ActualStatuses.Add("invisible");
                string before=Codec.Encode(w);Assert(CapabilityResolver.Resolve(u,N).ConcealmentProfile=="hidden");
                Assert(Observation.Project(w,u,1)!.Public.Body=="rider"&&Observation.Project(w,u,1)!.Public.Concealment=="hidden");
                Assert(Codec.Encode(w)==before);w.Masks[2].DetectedUnits.Remove(u.LogicalId);Assert(Observation.Project(w,u,2) is null);
            });
            Test("A40-002-regroup-copies-effective-hidden-appearance",new[]{"UI-A40"},()=> {
                var w=World();var i=Add(w,ActualKind.Mimic,"mimic","warrior");var p=Add(w,ActualKind.Genuine,"healer","rider",x:2);p.ActualStatuses.Add("invisible");
                string before=Codec.Encode(w);var a=Rules.Exchange(w,"a40-copy",1,i.LogicalId,p.LogicalId,ExchangeKind.Regroup,false,N).State;
                Assert(a.Units[i.LogicalId].Appearance.Body=="rider"&&a.Units[i.LogicalId].Appearance.Concealment=="hidden");
                Assert(a.Units[i.LogicalId].ActualStatuses.Count==0&&!CapabilityResolver.Resolve(a.Units[i.LogicalId],N).CanDamage);
                Assert(Codec.Encode(w)==before&&Codec.Decode(Codec.Encode(a)).Units[i.LogicalId].Appearance.Concealment=="hidden");
            });
            Test("A40-003-disguise-preserves-recipient-native-invisibility",new[]{"UI-A40"},()=> {
                var w=World();var i=Add(w,ActualKind.Mimic,"mimic","warrior");var p=Add(w,ActualKind.Genuine,"healer","healer",x:2);p.ActualStatuses.Add("invisible");
                var a=Rules.Exchange(w,"a40-give",1,i.LogicalId,p.LogicalId,ExchangeKind.Disguise,true,N).State;var next=a.Units[p.LogicalId];
                Assert(next.Appearance.Body=="warrior"&&next.Appearance.Concealment=="hidden"&&next.ActualStatuses.SequenceEqual(p.ActualStatuses));
                Assert(!next.Allowance.ActionSpent&&a.Units[i.LogicalId].Allowance.ActionSpent);
            });
            Test("A40-004-visible-and-apparent-only-statuses-remain-independent",new[]{"UI-A40"},()=> {
                var w=World();var u=Add(w,ActualKind.Genuine,"warrior","rider");Assert(CapabilityResolver.Resolve(u,N).ConcealmentProfile=="visible");
                u.Appearance.Statuses.Add("invisible");u.Appearance.Concealment="hidden";Assert(!CapabilityResolver.ActualInvisible(u));
                Assert(u.ActualStatuses.Count==0&&CapabilityResolver.EffectiveAppearance(u).Concealment=="hidden");
            });
            TreeTests();
            AmendmentTests();
            RecruitmentTests();
            RecruitmentWheelTests();
            SmuggleTests();
            StateFrameTests();
            NativeIdentityLedgerTests();
            NativeExchangeIntentTests();
            NativePaidBirthIntentTests();
            NativeRegistryIdTests();
            PresentationTests.Run(Test);
            SelectionPresentationTests();
            Test("M0-061-il2cpp-newslot-json-dispatch",new[]{"DEC-179"},()=> {object proxy=new FakeNativeJson();Assert(proxy.ToString()!=NativeJson.Read(proxy));Assert(NativeJson.Read(proxy)=="{\"native\":true}");});
            Test("M0-062-json-object-fallback-rejected",new[]{"DEC-179"},()=> {Reject(()=>NativeJson.Read(new object()),"fallback");});
            string output=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT") ?? Path.Combine(Environment.CurrentDirectory,"model-test-results.json");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output,JsonSerializer.Serialize(new {suite="Deception M1D model presentation + recruitment registry",scope="Executable managed model/registry subchecks; NOT native acceptance",
                nativeAdapter=N.EvidenceId,runtime=Environment.Version.ToString(),is64Bit=Environment.Is64BitProcess,
                total=Count,failed=Failed,passed=Count-Failed,tests=Results},Codec.Options));
            Console.WriteLine("MODEL TESTS: "+(Count-Failed)+"/"+Count+" passed. Native game acceptance cases are NOT RUN.");
            return Failed==0?0:1;
        }
        static void TreeTests()
        {
            string fixture=File.ReadAllText(Environment.GetEnvironmentVariable("DECEPTION_TREE_FIXTURE") ?? "tests/native-tree.synthetic.json");
            Test("M0-055-native-root-identity-and-starting-prerequisite",new[]{"DEC-001","DEC-196"},()=> {
                var before=JsonNode.Parse(fixture)!;var change=TechTree.AddDeception(fixture);var after=JsonNode.Parse(change.Json)!;
                Assert(change.ModifiedRoots.SequenceEqual(new[]{"basic"}));
                Assert(after["techData"]!["basic"]!["idx"]!.GetValue<int>()==0);
                Assert(after["techData"]!.AsObject().Count==before["techData"]!.AsObject().Count+5);
                Assert(!after["techData"]!.AsObject().Any(x=>x.Key.StartsWith(TechTree.RootPrefix)));
                Assert(TechTree.Canonical(before["tribeData"])==TechTree.Canonical(after["tribeData"]));
                foreach(var p in before["techData"]!.AsObject()) if(p.Key!="basic")
                    Assert(TechTree.Canonical(p.Value)==TechTree.Canonical(after["techData"]![p.Key]));
                foreach(var tribe in change.TribeAudit)
                    Assert(tribe.Root=="basic" && tribe.RootIndex==0 && tribe.StartingTech.Contains("basic") && tribe.ExtendedBranches.Length==6);
                Assert(TechTree.Canonical(before["unitData"])==TechTree.Canonical(after["unitData"]));
                Assert(after["techData"]![TechTree.DeceptionId]!["cost"]!.GetValue<int>()==1);
                Assert(after["techData"]![TechTree.DeceptionId]!["unitUnlocks"]!.AsArray().Count==0);
            });
            Test("M0-056-special-native-branch-override-preserved",new[]{"DEC-001","DEC-196"},()=> {
                var a=TechTree.AddDeception(fixture);var aqua=a.TribeAudit.Single(t=>t.Tribe=="aquarion");
                Assert(aqua.NativeBranches.Contains("aquafishing") && !aqua.NativeBranches.Contains("fishing"));
                Assert(aqua.ExtendedBranches.Contains(TechTree.DeceptionId));
            });
            Test("M0-057-duplicate-root-rejected",new[]{"DEC-001"},()=> {string first=TechTree.AddDeception(fixture).Json;Reject(()=>TechTree.AddDeception(first),"already exist");});
            Test("M0-058-non-five-root-contract-rejected",new[]{"DEC-001","DEC-179"},()=> {var a=JsonNode.Parse(fixture)!;a["techData"]!["basic"]!["techUnlocks"]!.AsArray().RemoveAt(0);Reject(()=>TechTree.AddDeception(a.ToJsonString()),"five");});
            Test("M0-059-unknown-registry-category-rejected",new[]{"DEC-001","DEC-179"},()=> {var a=JsonNode.Parse(fixture)!;a["tribeData"]!["imperius"]!["category"]=9;Reject(()=>TechTree.AddDeception(a.ToJsonString()),"category");});
            Test("M0-060-root-override-cycle-rejected",new[]{"DEC-001"},()=> {var a=JsonNode.Parse(fixture)!;a["tribeData"]!["bardur"]!["techOverrides"]=new JsonObject{["basic"]="cycle",["cycle"]="basic"};Reject(()=>TechTree.AddDeception(a.ToJsonString()),"Cyclic");});
        }
    }
}
