using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Deception.Core;
using Deception.PolyModAdapter;

namespace Deception.Tests
{
    internal static class RegistryPublicationTests
    {
        static readonly string[] Sections={"techData","unitData","improvementData","tribeData"};

        // A native-shaped reflected string indexer. Keep section objects by exact
        // identity; JSON serialization is only for the real Core validators.
        sealed class Root
        {
            readonly Dictionary<string,JsonNode?> Values=new(StringComparer.Ordinal);
            internal readonly List<string> Writes=new();
            internal string? FailSection;
            internal bool FailAfterWrite;
            internal Root(JsonObject json){foreach(var p in json)Values.Add(p.Key,p.Value);}
            public object? this[string key]
            {
                get=>Values.GetValueOrDefault(key);
                set
                {
                    Writes.Add(key);
                    bool fail=FailSection==key;if(fail)FailSection=null;
                    if(fail&&!FailAfterWrite)throw new InvalidOperationException("Injected setter failure before "+key);
                    Values[key]=(JsonNode?)value;
                    if(fail)throw new InvalidOperationException("Injected setter failure after "+key);
                }
            }
            internal JsonObject Json()
            {
                var result=new JsonObject();
                foreach(var p in Values)result[p.Key]=p.Value is null?null:JsonNode.Parse(p.Value.ToJsonString());
                return result;
            }
            internal void Remove(string key)=>Values.Remove(key);
        }

        static (Root Live,Root Candidate) Pair()
        {
            var source=JsonNode.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("CAPTURED_TREE")!))!.AsObject();
            var improvements=new JsonObject();int index=70;
            foreach(string id in new[]{"harvestfruit","fishing","hunting"})improvements[id]=new JsonObject{["idx"]=index++,["cost"]=2};
            source["improvementData"]=improvements;
            source["unrelatedPublicationSentinel"]=new JsonObject{["nested"]=new JsonArray(7,"untouched")};
            string candidate=RecruitmentCatalog.Add(TechTree.AddDeception(source.ToJsonString()).Json).Json;
            candidate=NativeExchangeCatalog.Add(SmuggleCatalog.Add(candidate));
            return(new Root(source),new Root(JsonNode.Parse(candidate)!.AsObject()));
        }

        // Installed native GetUnlockedUnits (0x70cef1--0x70cf3e) resolves
        // each researched unlock through the tribe's UnitData override before
        // returning it. CanTrainUnit tests membership in that resulting list.
        static string[] NativeUnlocked(Root live,string tribe,params string[] researched)
        {
            var json=live.Json();var overrides=json["tribeData"]![tribe]?["unitOverrides"];
            return researched.SelectMany(id=>json["techData"]![id]!["unitUnlocks"]!.AsArray())
                .Select(n=>n!.GetValue<string>()).Select(id=>overrides?[id]?.GetValue<string>()??id)
                .Where(id=>json["unitData"]![id]?["hidden"]?.GetValue<bool>()!=true).ToArray();
        }

        internal static void Run(Action<string,Action> test,Action<bool,string> check)
        {
            void Reject(Action action,string reason)
            {bool rejected=false;try{action();}catch{rejected=true;}check(rejected,reason);}
            void Restored(Root live,Dictionary<string,object?> originals,string before)
            {
                foreach(var p in originals)check(ReferenceEquals(live[p.Key],p.Value),"Original section identity not restored: "+p.Key);
                check(live.Json().ToJsonString()==before,"Rollback did not restore exact original root contents");
            }
            Dictionary<string,object?> Original(Root live)=>Sections.Concat(new[]{"resourceData","unrelatedPublicationSentinel"}).ToDictionary(k=>k,k=>live[k]);

            test("publication-real-core-candidate-publishes-four-sections",()=>{
                var (live,candidate)=Pair();var before=live.Json();var untouched=live["resourceData"];var sentinel=live["unrelatedPublicationSentinel"];
                new NativeRegistryPublication(live,candidate).Apply();
                foreach(string section in Sections)check(ReferenceEquals(live[section],candidate[section]),"Missing published section: "+section);
                check(live.Writes.SequenceEqual(Sections),"Publication wrote a wrong or extra root section");
                check(ReferenceEquals(live["resourceData"],untouched)&&ReferenceEquals(live["unrelatedPublicationSentinel"],sentinel),"Publication replaced unrelated sections");
                var actual=live.Json();var tribes=actual["tribeData"]!.AsObject();
                check(tribes["aquarion"]!["unitOverrides"]![RecruitmentCatalog.MenuId]!.GetValue<string>()==AquarionCatalog.MimicId,"Mermimic override absent from live root");
                check(tribes["aquarion"]!["unitOverrides"]![SpyCatalog.Id]!.GetValue<string>()==SpyCatalog.AquarionId,"Spymaid override absent from live root");
                AquarionCatalog.RemoveValidatedOverrides(actual);
                check(TechTree.Canonical(actual["tribeData"])==TechTree.Canonical(before["tribeData"]),"Publication changed other native tribe fields");
                foreach(var unit in before["unitData"]!.AsObject())check(TechTree.Canonical(actual["unitData"]![unit.Key])==TechTree.Canonical(unit.Value),"Publication changed original native unit: "+unit.Key);
            });
            test("publication-spy-recruitment-requires-research",()=>{
                var (live,candidate)=Pair();new NativeRegistryPublication(live,candidate).Apply();
                foreach(string tribe in new[]{"aquarion","imperius"})
                    check(!NativeUnlocked(live,tribe,"basic").Any(SpyCatalog.IsSpyId),"Unresearched tribe received a Spy: "+tribe);
            });
            test("publication-native-aquarion-roster-and-train-membership-use-spymaid",()=>{
                var (live,candidate)=Pair();new NativeRegistryPublication(live,candidate).Apply();
                var aquatic=NativeUnlocked(live,"aquarion",TechTree.EspionageId);
                check(aquatic.SequenceEqual(new[]{SpyCatalog.AquarionId}),"Aquarion researched roster did not replace base Spy");
                check(aquatic.Contains(SpyCatalog.AquarionId)&&!aquatic.Contains(SpyCatalog.Id),"Native train-membership species is incorrect");
                foreach(var tribe in live.Json()["tribeData"]!.AsObject().Where(t=>t.Key!="aquarion"))
                    check(NativeUnlocked(live,tribe.Key,TechTree.EspionageId).SequenceEqual(new[]{SpyCatalog.Id}),"Ordinary tribe received Spymaid: "+tribe.Key);
                check(live.Json()["techData"]![TechTree.EspionageId]!["unitUnlocks"]!.AsArray().Single()!.GetValue<string>()==SpyCatalog.Id,"Shared Espionage unlock was globally changed");
            });
            test("publication-live-root-validates-reentry-without-mutation",()=>{
                var (live,candidate)=Pair();new NativeRegistryPublication(live,candidate).Apply();string json=live.Json().ToJsonString();
                RecruitmentCatalog.ValidateReentry(json);SmuggleCatalog.ValidateReentry(json);NativeExchangeCatalog.ValidateReentry(json);
                check(live.Json().ToJsonString()==json,"Reentry validation mutated the published root");
                var retry=new NativeRegistryPublication(live,new Root(live.Json()));retry.Apply();
                RecruitmentCatalog.ValidateReentry(live.Json().ToJsonString());check(live.Json().ToJsonString()==json,"Published reentry changed registry content");
            });
            test("publication-old-three-section-mutation-is-detected",()=>{
                var (live,candidate)=Pair();foreach(string section in Sections.Take(3))live[section]=candidate[section];
                check(NativeUnlocked(live,"aquarion",TechTree.EspionageId).SequenceEqual(new[]{SpyCatalog.Id}),"Old-publication control did not reproduce wrong native Spy roster");
                Reject(()=>RecruitmentCatalog.ValidateReentry(live.Json().ToJsonString()),"Missing live Aquarion overrides escaped reentry validation");
                check(candidate.Json()["tribeData"]!["aquarion"]!["unitOverrides"]![SpyCatalog.Id]!.GetValue<string>()==SpyCatalog.AquarionId,"Control failed to distinguish valid candidate from faulty live root");
            });
            test("publication-downstream-failure-restores-exact-originals",()=>{
                var (live,candidate)=Pair();var originals=Original(live);string before=live.Json().ToJsonString();var p=new NativeRegistryPublication(live,candidate);
                try{p.Apply();RecruitmentCatalog.ValidateReentry(live.Json().ToJsonString());throw new InvalidOperationException("Native mapping failure after publication");}
                catch{p.Rollback();}
                Restored(live,originals,before);
            });
            foreach(string section in Sections)foreach(bool after in new[]{false,true})
            test("publication-setter-failure-"+(after?"after-":"before-")+section+"-restores-exact-originals",()=>{
                var (live,candidate)=Pair();var originals=Original(live);string before=live.Json().ToJsonString();var p=new NativeRegistryPublication(live,candidate);
                live.FailSection=section;live.FailAfterWrite=after;Reject(p.Apply,"Injected setter failure was swallowed");p.Rollback();Restored(live,originals,before);
            });
            foreach(string section in Sections)
            test("publication-missing-candidate-"+section+"-fails-before-any-write",()=>{
                var (live,candidate)=Pair();var originals=Original(live);string before=live.Json().ToJsonString();candidate.Remove(section);
                Reject(()=>new NativeRegistryPublication(live,candidate),"Missing candidate section accepted");check(live.Writes.Count==0,"Constructor failure partially published root");Restored(live,originals,before);
            });
        }
    }
}
