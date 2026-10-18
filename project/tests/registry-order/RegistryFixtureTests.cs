using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Deception.Core;
using Deception.PolyModAdapter;
using Newtonsoft.Json.Linq;
namespace Deception.Tests
{
    public static class RegistryFixtureTests
    {
        static int Tests,Assertions;static readonly List<string> Names=new();
        static void Check(bool ok,string reason){Assertions++;if(!ok)throw new Exception(reason);}
        static void Reject(Action action,string reason){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,reason);}
        static void Test(string name,Action action){Reset();action();Tests++;Names.Add(name);}
        static void Reset()
        {
            FixtureLoader.Reset();Entry.Notes.Clear();
            foreach(var field in typeof(NativeRegistryOrder).GetFields(BindingFlags.Static|BindingFlags.NonPublic))
                if(!field.IsInitOnly)field.SetValue(null,field.FieldType.IsValueType?Activator.CreateInstance(field.FieldType):null);
            NativeRegistryOrder.Install(new HarmonyLib.Harmony(),typeof(FixtureLoader));
        }
        static JObject Registry()
        {
            string native=File.ReadAllText(Environment.GetEnvironmentVariable("CAPTURED_TREE")!);
            var source=JsonNode.Parse(native)!;var improvements=new JsonObject();int index=70;
            foreach(string id in new[]{"harvestfruit","fishing","hunting"})improvements[id]=new JsonObject{["idx"]=index++,["cost"]=2};
            source["improvementData"]=improvements;
            string json=TechTree.AddDeception(source.ToJsonString()).Json;json=RecruitmentCatalog.Add(json).Json;json=SmuggleCatalog.Add(json);
            json=NativeExchangeCatalog.Add(json);var ready=JsonNode.Parse(json)!;ready["techData"]!["dcp_misdirection"]=new JsonObject{["idx"]=-1};return JObject.Parse(ready.ToJsonString());
        }
        static void ArmRun(JObject json){NativeRegistryOrder.Arm(json);FixtureLoader.Run(json);NativeRegistryOrder.Complete(json);}
        static void VerifyMapping(JObject json)
        {
            NativeRegistryIds.Validate(json.ToString());
            foreach(var e in NativeRegistryIds.Entries)Check(FixtureLoader.Mapping[(e.EnumType,e.Id)]==e.Index,"Wrong saved index: "+e.Id);
            Check(FixtureLoader.Counter==1087,"Tail counter did not finish after Lighthouse Secret ID1086");
            Check(FixtureLoader.FirstHandlerCount.All(x=>x.Value==1),"First-pass unit handler duplicated");
            Check(RecruitmentCatalog.CarrierForms.All(f=>FixtureLoader.PrefabNames[FixtureLoader.Mapping[("Polytopia.Data.UnitData+Type",RecruitmentCatalog.Variant(f))]]==f),"Wrong final prefab association");
            Check(FixtureLoader.HandlerCount.All(x=>x.Value==1),"Unit prefab handler duplicated");
            Check(RecruitmentCatalog.CarrierForms.All(f=>FixtureLoader.HandlerCount[RecruitmentCatalog.Variant(f)]==1),"A carrier prefab handler was skipped");
            Check(FixtureLoader.Batches.Count==10,"Expected unchanged historical batches with Lighthouse Secret appended");
            Check(FixtureLoader.Batches[0].Length==33&&FixtureLoader.Batches[1].Length==5&&FixtureLoader.Batches[2].Length==19&&FixtureLoader.Batches[3].Length==3&&FixtureLoader.Batches[4].Length==7&&FixtureLoader.Batches[5].Length==7&&FixtureLoader.Batches[6].Length==6&&FixtureLoader.Batches[7].Length==1&&FixtureLoader.Batches[8].Length==5&&FixtureLoader.Batches[9].SequenceEqual(new[]{"dcp_exchange_lighthouse_secret"}),"Wrong per-stage allocation counts");
            Check(FixtureLoader.Batches[1].SequenceEqual(NativeRegistryIds.Entries.Where(e=>e.Index>=1033&&e.Index<=1037).Select(e=>e.Id)),"Historical technology tail order changed");
            Check(FixtureLoader.Batches[2].SequenceEqual(NativeRegistryIds.Entries.Where(e=>e.Index>=1038&&e.Index<=1056).Select(e=>e.Id)),"Field/naval/Cloak carrier tail order changed");
            Check(FixtureLoader.Batches[3].SequenceEqual(new[]{"dcp_exchange_upgrade_scout","dcp_exchange_upgrade_rammer","dcp_exchange_upgrade_bomber"}),"Private upgrade tail order changed");
            Check(FixtureLoader.Batches[4].SequenceEqual(new[]{"dcp_mimic_form_raychi","dcp_mimic_form_jelly","dcp_mimic_form_shark","dcp_mimic_form_aquapult","dcp_mimic_form_island","dcp_spy","dcp_mimic_form_dcp_spy"}),"New city forms and Spy allocation order changed");
            Check(FixtureLoader.Batches[5].SequenceEqual(new[]{"dcp_exchange_shed_disguise","dcp_exchange_subvert","dcp_exchange_blackout","dcp_exchange_feign_heal","dcp_exchange_feign_boost","dcp_exchange_feign_swarm","dcp_exchange_feign_recover"}),"Specialist commands must append to Shed without moving prior IDs");
            Check(FixtureLoader.Batches[6].SequenceEqual(new[]{"dcp_mimic_form_mooni","dcp_mimic_form_moth","dcp_mimic_form_crab","dcp_mimic_form_gaami","dcp_mimic_form_bugegg","dcp_mimic_form_larva"}),"Special forms must follow specialist commands");
            Check(FixtureLoader.Batches[7].SequenceEqual(new[]{"dcp_exchange_blackmail"}),"Blackmail must append at1080 without moving prior IDs");
            Check(FixtureLoader.HandlerCount[SpyCatalog.Id]==1&&FixtureLoader.FirstHandlerCount[SpyCatalog.Id]==1&&FixtureLoader.PrefabNames[1065]=="warrior"&&FixtureLoader.PrefabNames[1066]==SpyCatalog.Id,"Spy source/carrier prefab handlers diverged");
            Check(FixtureLoader.Batches[8].SequenceEqual(new[]{"dcp_mermimic","dcp_spymaid","dcp_mimic_form_dcp_spymaid","dcp_motherfogger","dcp_mimic_form_dcp_motherfogger"}),"Fogger must follow the unchanged Aquarion forms after Blackmail");
            foreach(var expected in new[]{(Id:"dcp_mermimic",Index:1081,Prefab:"mermaidwarrior"),(Id:"dcp_spymaid",Index:1082,Prefab:"mermaidwarrior"),(Id:"dcp_mimic_form_dcp_spymaid",Index:1083,Prefab:"dcp_spymaid")})
            {
                Check(FixtureLoader.Mapping[("Polytopia.Data.UnitData+Type",expected.Id)]==expected.Index,"Aquarion allocation moved: "+expected.Id);
                Check(FixtureLoader.PrefabNames[expected.Index]==expected.Prefab,"Aquarion prefab source mismatch: "+expected.Id);
                Check(FixtureLoader.FirstHandlerCount[expected.Id]==1&&FixtureLoader.HandlerCount[expected.Id]==1,"Aquarion prefab handler skipped or repeated: "+expected.Id);
            }
            foreach(var expected in new[]{(Id:"dcp_motherfogger",Index:1084,Prefab:"warrior"),(Id:"dcp_mimic_form_dcp_motherfogger",Index:1085,Prefab:"dcp_motherfogger")})
            {
                Check(FixtureLoader.Mapping[("Polytopia.Data.UnitData+Type",expected.Id)]==expected.Index,"Fogger allocation did not append: "+expected.Id);
                Check(FixtureLoader.PrefabNames[expected.Index]==expected.Prefab,"Fogger prefab source mismatch: "+expected.Id);
                Check(FixtureLoader.FirstHandlerCount[expected.Id]==1&&FixtureLoader.HandlerCount[expected.Id]==1,"Fogger prefab handler skipped or repeated: "+expected.Id);
            }
            Check(NativeRegistryOrder.AllowLocalGame(),"Successful registry did not allow Local game");NativeRegistryOrder.RequireCompatible();
        }
        public static int Run()
        {
            try
            {
                Test("actual-adapter-nine-batches-stable-ids-handler-once",()=>{var j=Registry();ArmRun(j);VerifyMapping(j);});
                Test("actual-adapter-reentry-never-reruns-handler",()=>{var j=Registry();ArmRun(j);int calls=FixtureLoader.Invocations;NativeRegistryOrder.Arm(j);FixtureLoader.Run(j);NativeRegistryOrder.Complete(j);Check(calls==FixtureLoader.Invocations,"Reentry invoked loader");VerifyMapping(j);});
                Test("actual-adapter-cloned-reentry-no-duplicate-handler",()=>{var j=Registry();ArmRun(j);var clone=j.DeepClone();int calls=FixtureLoader.Invocations;ArmRun(clone);Check(calls==FixtureLoader.Invocations,"Cloned reentry invoked loader");VerifyMapping(clone);});
                Test("actual-adapter-restores-all-tail-nodes-after-unit-tail-failure",()=>{
                    var j=Registry();var before=JsonNode.Parse(j.ToString())!;FixtureLoader.FailAfter="dcp_mimic_form_centipede";
                    Reject(()=>ArmRun(j),"Synthetic native tail failure did not escape");
                    foreach(var entry in NativeRegistryIds.Entries.Where(x=>x.Index>=1033))Check(TechTree.Canonical(j.Node[entry.Section]![entry.Id])==TechTree.Canonical(before[entry.Section]![entry.Id]),"Tail entry not restored: "+entry.Id);
                    Check(!NativeRegistryOrder.AllowLocalGame(),"Faulted registry allowed Local game");Reject(NativeRegistryOrder.RequireCompatible,"Faulted registry allowed state transport");
                    Check(FixtureLoader.HandlerCount.Values.All(x=>x==1),"Failure repeated a native handler");int calls=FixtureLoader.Invocations;
                    Reject(()=>NativeRegistryOrder.Arm(j),"Faulted registry could be rearmed");Check(FixtureLoader.Invocations==calls,"Fault recovery reran native mapping");
                });
                Test("actual-adapter-restores-all-tail-nodes-after-improvement-tail-failure",()=>{
                    var j=Registry();var before=JsonNode.Parse(j.ToString())!;FixtureLoader.FailAfter="dcp_exchange_upgrade_rammer";
                    Reject(()=>ArmRun(j),"Synthetic upgrade-tail failure did not escape");
                    foreach(var entry in NativeRegistryIds.Entries.Where(x=>x.Index>=1033))Check(TechTree.Canonical(j.Node[entry.Section]![entry.Id])==TechTree.Canonical(before[entry.Section]![entry.Id]),"Tail entry not restored after improvement failure: "+entry.Id);
                    Check(FixtureLoader.Invocations==4,"Failure did not exercise the historical upgrade allocation batch");
                    Check(!NativeRegistryOrder.AllowLocalGame(),"Faulted final registry enabled Local game");
                    Reject(NativeRegistryOrder.RequireCompatible,"Faulted final registry allowed state transport");
                    Check(FixtureLoader.HandlerCount.Values.All(x=>x==1),"Improvement-tail failure repeated a unit handler");
                });
                foreach(var failure in new[]{("dcp_spy",5),("dcp_exchange_shed_disguise",6),("dcp_exchange_blackout",6),("dcp_mimic_form_larva",7),("dcp_exchange_blackmail",8),("dcp_mermimic",9),("dcp_spymaid",9),("dcp_mimic_form_dcp_spymaid",9),("dcp_motherfogger",9),("dcp_mimic_form_dcp_motherfogger",9)})
                Test("actual-adapter-restores-tail-after-"+failure.Item1+"-failure",()=>{
                    var j=Registry();var before=JsonNode.Parse(j.ToString())!;FixtureLoader.FailAfter=failure.Item1;
                    Reject(()=>ArmRun(j),"Appended allocation failure was swallowed");
                    foreach(var entry in NativeRegistryIds.Entries.Where(x=>x.Index>=1033))
                        Check(TechTree.Canonical(j.Node[entry.Section]![entry.Id])==TechTree.Canonical(before[entry.Section]![entry.Id]),"Later failure did not restore tail: "+entry.Id);
                    Check(FixtureLoader.Invocations==failure.Item2,"Failure missed its appended allocation batch");
                    Check(!NativeRegistryOrder.AllowLocalGame(),"Partially loaded appended registry enabled game");
                    Reject(NativeRegistryOrder.RequireCompatible,"Partial appended registry allowed state transport");
                    Check(FixtureLoader.HandlerCount.Values.All(x=>x==1),"Later failure repeated a handler");
                    int calls=FixtureLoader.Invocations;Reject(()=>NativeRegistryOrder.Arm(j),"Faulted appended registry could be rearmed");
                    Check(FixtureLoader.Invocations==calls,"Faulted rearm restarted loader");
                });
                Test("actual-adapter-restores-all-tail-nodes-after-main-failure",()=>{
                    var j=Registry();var before=JsonNode.Parse(j.ToString())!;FixtureLoader.FailAfter="dcp_mimic_form_rider";Reject(()=>ArmRun(j),"Main phase failure swallowed");
                    foreach(var e in NativeRegistryIds.Entries.Where(x=>x.Index>=1033))Check(TechTree.Canonical(j.Node[e.Section]![e.Id])==TechTree.Canonical(before[e.Section]![e.Id]),"Unrun tail not restored");Check(!NativeRegistryOrder.AllowLocalGame(),"Partial main mapping enabled game");
                });
                Test("actual-adapter-partial-staging-restores-exact-tail",()=>{
                    var j=Registry();var before=JsonNode.Parse(j.ToString())!;JObject.FailRemoveAt=7;
                    Reject(()=>ArmRun(j),"Partial staging fault was swallowed");
                    foreach(var e in NativeRegistryIds.Entries.Where(x=>x.Index>=1033))Check(TechTree.Canonical(j.Node[e.Section]![e.Id])==TechTree.Canonical(before[e.Section]![e.Id]),"Partially staged tail not restored");
                    Check(FixtureLoader.Invocations==0&&!NativeRegistryOrder.AllowLocalGame(),"Staging failure executed loader or enabled game");
                });
                Test("actual-adapter-preallocated-first-entry-rejected-before-loader",()=>{var j=Registry();j.Node["unitData"]!["dcp_mimic_form_giant"]!["idx"]=1043;Reject(()=>NativeRegistryOrder.Arm(j),"First mapping accepted preallocation");Check(FixtureLoader.Invocations==0,"Invalid registry ran handlers");});
                Test("actual-adapter-unknown-custom-carrier-rejected-before-loader",()=>{var j=Registry();j.Node["unitData"]!["dcp_mimic_form_unknown"]=new JsonObject{["idx"]=-1};Reject(()=>NativeRegistryOrder.Arm(j),"Unknown custom carrier accepted");Check(FixtureLoader.Invocations==0,"Unknown registry ran handlers");});
                RegistryPublicationTests.Run(Test,Check);
                File.WriteAllText(Environment.GetEnvironmentVariable("REGISTRY_TEST_OUTPUT")!,JsonSerializer.Serialize(new{tests=Tests,assertions=Assertions,failed=0,names=Names,scope="Actual production NativeRegistryOrder and NativeRegistryPublication with managed JSON/loader fixtures; no native mapping, Unity, install or game execution"}));return 0;
            }catch(Exception e){File.WriteAllText(Environment.GetEnvironmentVariable("REGISTRY_TEST_OUTPUT")!,JsonSerializer.Serialize(new{tests=Tests,assertions=Assertions,failed=1,error=e.ToString()}));return 1;}
        }
    }
}
