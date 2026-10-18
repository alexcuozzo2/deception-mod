using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        // Structural JSON policy only. Pin's output is not evidence that the
        // installed loader honors explicit idx values or allocates native IDs.
        static void NativeRegistryIdTests()
        {
            string fixture=Environment.GetEnvironmentVariable("DECEPTION_TREE_FIXTURE")??"tests/native-tree.synthetic.json";
            string captured=Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE")??"tests/native-tree.captured.json";
            string project=Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(fixture))!)!;
            string[] sections={"techData","unitData","improvementData"};
            string[] baselineForms={"amphibian","archer","battlesled","catapult","ciru","defender","doomux","exida",
                "hexapod","icearcher","icefortress","kiton","knight","mantis","mermaidarcher","mermaiddefender",
                "mermaidswordsman","mermaidwarrior","mindbender","phychi","rider","shaman","siren","swordsman","tridention","warrior"};
            JsonNode? Clone(JsonNode? value)=>value is null?null:JsonNode.Parse(value.ToJsonString());
            JsonObject Registry(bool technologies=false)
            {
                var native=JsonNode.Parse(File.ReadAllText(fixture))!.AsObject();
                // The small tree fixture has only one skeletal unit. Reuse the
                // captured source profiles for the reviewed carrier catalog.
                native["unitData"]=Clone(JsonNode.Parse(File.ReadAllText(captured))!["unitData"]);
                native["tribeData"]!["aquarion"]!["unitOverrides"]=Clone(JsonNode.Parse(File.ReadAllText(captured))!["tribeData"]!["aquarion"]!["unitOverrides"]);
                var improvements=new JsonObject();int index=70;
                foreach(string id in new[]{"harvestfruit","fishing","hunting"})improvements[id]=new JsonObject{
                    ["idx"]=index++,["cost"]=2,["hidden"]=false,["prefab"]=id,
                    ["improvementAbilities"]=new JsonArray("fixture-native-action"),
                    ["terrainRequirements"]=new JsonArray(new JsonObject{["resource"]="fruit"}),
                    ["creates"]=new JsonArray(),["growthRewards"]=new JsonArray(),["growthRate"]=0,
                    ["unknownNativeField"]=new JsonObject{["preserve"]="verbatim"}};
                native["improvementData"]=improvements;
                native["fixtureUnrelated"]=new JsonObject{["idx"]=-1,["ordered"]=new JsonArray(3,1,2)};
                string json=TechTree.AddDeception(native.ToJsonString()).Json;
                json=RecruitmentCatalog.Add(json).Json;json=SmuggleCatalog.Add(json);json=NativeExchangeCatalog.Add(json);
                var result=JsonNode.Parse(json)!.AsObject();
                if(technologies)
                {
                    var tech=result["techData"]!.AsObject();var reordered=new JsonObject{
                        ["dcp_camouflage"]=new JsonObject{["idx"]=-1,["cost"]=2,["fixture"]="second new ID"},
                        ["dcp_misdirection"]=new JsonObject{["idx"]=-1,["cost"]=2,["fixture"]="first new ID"}};
                    foreach(var pair in tech)reordered[pair.Key]=Clone(pair.Value);
                    result["techData"]=reordered;
                }
                return result;
            }
            JsonObject Pinned(bool technologies=false)=>JsonNode.Parse(NativeRegistryIds.Pin(Registry(technologies).ToJsonString()))!.AsObject();
            JsonNode? ReverseObjects(JsonNode? value)
            {
                if(value is JsonObject obj){var reversed=new JsonObject();foreach(var pair in obj.Reverse())reversed[pair.Key]=ReverseObjects(pair.Value);return reversed;}
                if(value is JsonArray array)return new JsonArray(array.Select(ReverseObjects).ToArray());
                return Clone(value);
            }
            void RejectRegistry(JsonObject value,string context)
            {
                string untouched=value.ToJsonString();Reject(()=>NativeRegistryIds.Pin(untouched));
                Assert(value.ToJsonString()==untouched,"Rejected input changed: "+context);
                Reject(()=>NativeRegistryIds.Validate(untouched));
            }

            Test("A11-011-allocation-tail-preserves-original-shared-counter",new[]{"A11","DEC-173"},()=> {
                var input=Registry(true);string before=input.ToJsonString();var tail=NativeRegistryIds.AllocationTail(before);
                Assert(tail.Select(x=>x.Id).SequenceEqual(new[]{"dcp_misdirection","dcp_camouflage","dcp_propaganda","dcp_intelligence","dcp_espionage"}.Concat(new[]{"dcp_mimic_form_boomchi","dcp_mimic_form_bunny","dcp_mimic_form_centipede","dcp_mimic_form_dagger","dcp_mimic_form_firedragon","dcp_mimic_form_giant","dcp_mimic_form_mermaiddagger","dcp_mimic_form_polytaur","dcp_mimic_form_babydragon","dcp_mimic_form_dragonegg","dcp_mimic_form_transportship","dcp_mimic_form_scoutship","dcp_mimic_form_rammership","dcp_mimic_form_bombership","dcp_mimic_form_juggernaut","dcp_mimic_form_pirate","dcp_mimic_form_cloak_boat","dcp_mimic_form_cloak","dcp_mimic_form_mermaidcloak"}).Concat(new[]{"dcp_exchange_upgrade_scout","dcp_exchange_upgrade_rammer","dcp_exchange_upgrade_bomber","dcp_mimic_form_raychi","dcp_mimic_form_jelly","dcp_mimic_form_shark","dcp_mimic_form_aquapult","dcp_mimic_form_island","dcp_spy","dcp_mimic_form_dcp_spy","dcp_exchange_shed_disguise","dcp_exchange_subvert","dcp_exchange_blackout","dcp_exchange_feign_heal","dcp_exchange_feign_boost","dcp_exchange_feign_swarm","dcp_exchange_feign_recover","dcp_mimic_form_mooni","dcp_mimic_form_moth","dcp_mimic_form_crab","dcp_mimic_form_gaami","dcp_mimic_form_bugegg","dcp_mimic_form_larva","dcp_exchange_blackmail","dcp_mermimic","dcp_spymaid","dcp_mimic_form_dcp_spymaid","dcp_motherfogger","dcp_mimic_form_dcp_motherfogger","dcp_exchange_lighthouse_secret"})));
                Assert(input.ToJsonString()==before,"Tail calculation changed input");
                var tailNodes=tail.Select(e=>(e,Node:Clone(input[e.Section]![e.Id])!)).ToArray();
                foreach(var e in tail)input[e.Section]!.AsObject().Remove(e.Id);
                int counter=1000;
                foreach(string section in sections)foreach(var pair in input[section]!.AsObject())
                    if(pair.Value!["idx"]!.GetValue<int>()==-1)pair.Value["idx"]=counter++;
                Assert(counter==1033,"New technology entered the historical main batch");
                foreach(var item in tailNodes){item.Node["idx"]=counter++;input[item.e.Section]![item.e.Id]=item.Node;}
                Assert(counter==1087);NativeRegistryIds.Validate(input.ToJsonString());
            });
            Test("A11-012-allocation-refuses-missing-reservation-and-preallocated-tail",new[]{"A11","DEC-173"},()=> {
                Reject(()=>NativeRegistryIds.AllocationTail(Registry().ToJsonString()),"tail identity");
                var input=Registry(true);input["techData"]!["dcp_camouflage"]!["idx"]=1034;
                Reject(()=>NativeRegistryIds.AllocationTail(input.ToJsonString()),"tail identity");
            });
            Test("A11-013-tail-order-independent-of-json-and-required-flag",new[]{"A11","DEC-173"},()=> {
                var input=Registry(true);var expected=NativeRegistryIds.AllocationTail(input.ToJsonString());
                var reversed=NativeRegistryIds.AllocationTail(ReverseObjects(input)!.ToJsonString());
                Assert(expected.SequenceEqual(reversed)&&expected.Count==54&&expected.Count(x=>x.Required)==53);
            });
            Test("M2-060-registry-known-ids-match-observed-baseline",new[]{"DEC-171","DEC-173","DEC-197"},()=> {
                var entries=NativeRegistryIds.Entries;var required=entries.Where(x=>x.Index<=1032).OrderBy(x=>x.Index).ToArray();
                string[] ids=new[]{"dcp_deception"}.Concat(baselineForms.Select(x=>"dcp_mimic_form_"+x)).Concat(new[]{
                    "dcp_mimic","dcp_smuggle_fruit","dcp_smuggle_fish","dcp_smuggle_animal","dcp_exchange_regroup","dcp_exchange_disguise"}).ToArray();
                Assert(required.Length==33&&entries.Count==87&&required.Select(x=>x.Id).SequenceEqual(ids),"Historical registry identities changed");
                Assert(required.Select(x=>x.Index).SequenceEqual(Enumerable.Range(1000,33)),"Historical numeric assignments changed");
                Assert(required.All(x=>x.Section==(x.Index==1000?"techData":x.Index<=1027?"unitData":"improvementData")));
                Assert(required.All(x=>x.EnumType=="Polytopia.Data."+(x.Section=="techData"?"TechData":x.Section=="unitData"?"UnitData":"ImprovementData")+"+Type"));
                var evidence=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"evidence","observed-native-registry-0310.json")))!;
                Assert(evidence["version"]!.GetValue<string>()=="0.3.10"&&evidence["source"]!.GetValue<string>()==
                    "run-output/full-branch-implementation/candidate-0310/final-logs/launcher/bepinex.log");
                Assert(evidence["source_sha256"]!.GetValue<string>()=="5ceb58ee5708fa8ef3cfd2e7d78c98a9e95e42a75abf8546d826c956096e3b16");
                var observed=evidence["mappings"]!.AsArray();Assert(observed.Count==33);
                foreach(var row in required)Assert(observed.Count(x=>x!["type"]!.GetValue<string>()==row.EnumType&&
                    x["id"]!.GetValue<string>()==row.Id&&x["index"]!.GetValue<int>()==row.Index)==1,"Observed mapping mismatch: "+row.Id);
                Assert(entries.Select(x=>(x.EnumType,x.Id)).Distinct().Count()==87&&entries.Select(x=>x.Index).Distinct().Count()==87);
                Assert(entries is IList<NativeRegistryId> list&&list.IsReadOnly,"Published policy table is mutable");
            });
            Test("CLOAK-003-appended-identities-preserve-all-prior-carriers",new[]{"DEC-171","DEC-173"},()=> {
                // Literal historical order is independent of current catalog sorting.
                string[] priorTail={"dcp_misdirection","dcp_camouflage","dcp_propaganda","dcp_intelligence","dcp_espionage",
                    "dcp_mimic_form_boomchi","dcp_mimic_form_bunny","dcp_mimic_form_centipede","dcp_mimic_form_dagger",
                    "dcp_mimic_form_firedragon","dcp_mimic_form_giant","dcp_mimic_form_mermaiddagger","dcp_mimic_form_polytaur",
                    "dcp_mimic_form_babydragon","dcp_mimic_form_dragonegg","dcp_mimic_form_transportship","dcp_mimic_form_scoutship",
                    "dcp_mimic_form_rammership","dcp_mimic_form_bombership","dcp_mimic_form_juggernaut","dcp_mimic_form_pirate","dcp_mimic_form_cloak_boat"};
                var entries=NativeRegistryIds.Entries;var pinned=Pinned(true);
                foreach(var old in priorTail.Select((id,n)=>(id,index:1033+n))) {
                    var row=entries.Single(x=>x.Id==old.id);Assert(row.Index==old.index,"Prior tail ID changed: "+old.id);
                    Assert(pinned[row.Section]![row.Id]!["idx"]!.GetValue<int>()==old.index);
                }
                foreach(var added in new[]{("cloak",1055),("mermaidcloak",1056)}) {
                    var row=entries.Single(x=>x.Id==RecruitmentCatalog.Variant(added.Item1));
                    Assert(row.Index==added.Item2&&row.Required&&row.Section=="unitData"&&row.EnumType=="Polytopia.Data.UnitData+Type");
                    Assert(pinned["unitData"]![row.Id]!["idx"]!.GetValue<int>()==added.Item2);
                }
                Assert(entries.Where(x=>x.Section=="unitData"&&x.Index<=1056).OrderBy(x=>x.Index).TakeLast(2).Select(x=>x.Id).SequenceEqual(new[]{RecruitmentCatalog.Variant("cloak"),RecruitmentCatalog.Variant("mermaidcloak")}));
                foreach(var added in new[]{("scout",1057),("rammer",1058),("bomber",1059)}) {
                    var row=entries.Single(x=>x.Id=="dcp_exchange_upgrade_"+added.Item1);
                    Assert(row.Index==added.Item2&&row.Required&&row.Section=="improvementData"&&row.EnumType=="Polytopia.Data.ImprovementData+Type");
                    Assert(pinned[row.Section]![row.Id]!["idx"]!.GetValue<int>()==added.Item2);
                }
            });
            Test("M2-061-registry-pinning-preserves-every-other-json-field",new[]{"DEC-171","DEC-196","DEC-197"},()=> {
                var before=Registry();string input=before.ToJsonString();var after=JsonNode.Parse(NativeRegistryIds.Pin(input))!;
                NativeRegistryIds.Validate(after.ToJsonString());Assert(before.ToJsonString()==input);
                foreach(var entry in NativeRegistryIds.Entries.Where(x=>x.Required))
                {
                    Assert(before[entry.Section]![entry.Id]!["idx"]!.GetValue<int>()==-1);
                    Assert(after[entry.Section]![entry.Id]!["idx"]!.GetValue<int>()==entry.Index);
                    after[entry.Section]![entry.Id]!["idx"]=-1;
                }
                Assert(TechTree.Canonical(after)==TechTree.Canonical(before),"Pin changed content beyond owned idx fields");
                Assert(before["techData"]!["basic"]!["idx"]!.GetValue<int>()==0);
            });
            Test("REGISTRY-0361-001-new-water-spy-and-shed-ids-append",new[]{"DEC-171","DEC-173"},()=> {
                var pinned=Pinned(true);string[] unitIds={"dcp_mimic_form_raychi","dcp_mimic_form_jelly","dcp_mimic_form_shark","dcp_mimic_form_aquapult","dcp_mimic_form_island","dcp_spy","dcp_mimic_form_dcp_spy"};
                foreach(var row in unitIds.Select((id,n)=>(id,index:1060+n))) {
                    var entry=NativeRegistryIds.Entries.Single(e=>e.Id==row.id);
                    Assert(entry.Index==row.index&&entry.Required&&entry.Section=="unitData"&&entry.EnumType=="Polytopia.Data.UnitData+Type");
                    Assert(pinned["unitData"]![row.id]!["idx"]!.GetValue<int>()==row.index);
                }
                var shed=NativeRegistryIds.Entries.Single(e=>e.Id=="dcp_exchange_shed_disguise");
                Assert(shed.Index==1067&&shed.Required&&shed.Section=="improvementData"&&shed.EnumType=="Polytopia.Data.ImprovementData+Type");
                Assert(pinned[shed.Section]![shed.Id]!["idx"]!.GetValue<int>()==1067);
                Assert(NativeRegistryIds.Entries.OrderBy(e=>e.Index).Select(e=>e.Index).SequenceEqual(Enumerable.Range(1000,87)));
            });
            Test("M2-062-registry-new-technologies-do-not-renumber-existing-identities",new[]{"DEC-171","DEC-173"},()=> {
                var old=Pinned();var next=Pinned(true);NativeRegistryIds.Validate(old.ToJsonString());NativeRegistryIds.Validate(next.ToJsonString());
                foreach(var entry in NativeRegistryIds.Entries.Where(x=>x.Required))Assert(
                    TechTree.Canonical(old[entry.Section]![entry.Id])==TechTree.Canonical(next[entry.Section]![entry.Id]),"New technology shifted "+entry.Id);
                Assert(next["techData"]!["dcp_misdirection"]!["idx"]!.GetValue<int>()==1033&&
                    next["techData"]!["dcp_camouflage"]!["idx"]!.GetValue<int>()==1034);
                Assert(NativeRegistryIds.Entries.Where(x=>!x.Required).Select(x=>(x.Id,x.Index)).OrderBy(x=>x.Index)
                    .SequenceEqual(new[]{("dcp_misdirection",1033)}));
                foreach(string id in new[]{"dcp_misdirection"})
                {var one=Pinned(true);one["techData"]!.AsObject().Remove(id);NativeRegistryIds.Validate(one.ToJsonString());}
            });
            Test("M2-063-registry-order-independent-and-repeatable",new[]{"DEC-171","DEC-173"},()=> {
                var source=Registry(true);string expected=TechTree.Canonical(JsonNode.Parse(NativeRegistryIds.Pin(source.ToJsonString())));
                var reversed=ReverseObjects(source)!;string actual=NativeRegistryIds.Pin(reversed.ToJsonString());
                Assert(TechTree.Canonical(JsonNode.Parse(actual))==expected,"Object insertion order changed an assignment");
                for(int i=0;i<3;i++){NativeRegistryIds.Validate(actual);actual=NativeRegistryIds.Pin(actual);Assert(TechTree.Canonical(JsonNode.Parse(actual))==expected);}
                var mixed=JsonNode.Parse(actual)!;mixed["unitData"]!["dcp_mimic_form_rider"]!["idx"]=-1;
                mixed["techData"]!["dcp_camouflage"]!["idx"]=-1;
                Assert(TechTree.Canonical(JsonNode.Parse(NativeRegistryIds.Pin(mixed.ToJsonString())))==expected);
                Reject(()=>NativeRegistryIds.Validate(mixed.ToJsonString()));
            });
            Test("M2-064-registry-rejects-renumbered-own-ids",new[]{"DEC-173","DEC-179"},()=> {
                foreach(var entry in NativeRegistryIds.Entries)
                {
                    var wrong=Pinned(true);wrong[entry.Section]![entry.Id]!["idx"]=entry.Index+2;
                    RejectRegistry(wrong,entry.Id+" shifted by two");
                    var nativeZero=Pinned(true);nativeZero[entry.Section]![entry.Id]!["idx"]=0;RejectRegistry(nativeZero,entry.Id+" reused native zero");
                }
                Reject(()=>NativeRegistryIds.Validate(Registry().ToJsonString()));
            });
            Test("M2-065-registry-rejects-unknown-own-and-missing-historical-ids",new[]{"DEC-173","DEC-179"},()=> {
                foreach(var entry in NativeRegistryIds.Entries.Where(x=>x.Required))
                {var missing=Pinned();missing[entry.Section]!.AsObject().Remove(entry.Id);RejectRegistry(missing,entry.Id+" absent");}
                foreach(string section in sections)foreach(int index in new[]{-1,5000})
                {var unknown=Pinned();unknown[section]!["dcp_future_unassigned"]=new JsonObject{["idx"]=index};RejectRegistry(unknown,section+" unknown own identity");}
                var misplaced=Pinned();misplaced["techData"]!["dcp_mimic"]=new JsonObject{["idx"]=1027};RejectRegistry(misplaced,"own identity in wrong enum");
            });
            Test("M2-066-registry-rejects-foreign-allocation-and-reserved-collisions",new[]{"DEC-173","DEC-179"},()=> {
                foreach(string section in sections)foreach(int index in new[]{-1,-2,int.MinValue})
                {var bad=Pinned();bad[section]!["foreign_new"]=new JsonObject{["idx"]=index};RejectRegistry(bad,"foreign unassigned "+section);}
                foreach(var entry in NativeRegistryIds.Entries)
                {var collision=Pinned();collision[entry.Section]!["foreign_collision"]=new JsonObject{["idx"]=entry.Index};RejectRegistry(collision,entry.Id+" reserved numeric identity");}
            });
            Test("M2-067-registry-preserves-unreserved-native-ids-with-enum-scope",new[]{"DEC-173","DEC-197"},()=> {
                var input=Registry(true);
                // Native enum spaces already reuse numbers. Only a collision in
                // the same enum is a collision with this explicit table.
                input["techData"]!["foreign_existing"]=new JsonObject{["idx"]=1021,["name"]="preserve unit-enum number in tech enum"};
                input["unitData"]!["foreign_existing"]=new JsonObject{["idx"]=1033,["name"]="preserve tech-enum number in unit enum"};
                input["improvementData"]!["foreign_existing"]=new JsonObject{["idx"]=1000,["name"]="preserve tech-enum number in improvement enum"};
                var result=JsonNode.Parse(NativeRegistryIds.Pin(ReverseObjects(input)!.ToJsonString()))!;NativeRegistryIds.Validate(result.ToJsonString());
                foreach(string section in sections)Assert(TechTree.Canonical(input[section]!["foreign_existing"])==TechTree.Canonical(result[section]!["foreign_existing"]));
                foreach(var entry in NativeRegistryIds.Entries)Assert(result[entry.Section]![entry.Id]!["idx"]!.GetValue<int>()==entry.Index);
            });
            Test("M2-068-registry-requires-integer-index-and-object-shape",new[]{"DEC-173","DEC-179"},()=> {
                foreach(string text in new[]{"null","true","\"1000\"","1000.5","2147483648","{}","[]"})
                    foreach(var target in new[]{("techData","dcp_deception"),("unitData","warrior")})
                    {var bad=Pinned();bad[target.Item1]![target.Item2]!["idx"]=JsonNode.Parse(text);RejectRegistry(bad,"invalid idx "+text);}
                foreach(var target in new[]{("techData","dcp_deception"),("unitData","warrior")})
                {var missing=Pinned();missing[target.Item1]![target.Item2]!.AsObject().Remove("idx");RejectRegistry(missing,"missing idx");}
                foreach(string section in sections)
                {
                    var missing=Pinned();missing.Remove(section);RejectRegistry(missing,"missing section");
                    var malformed=Pinned();malformed[section]=new JsonArray();RejectRegistry(malformed,"array registry");
                    var entry=Pinned();entry[section]!["foreign_invalid"]=new JsonArray();RejectRegistry(entry,"array entry");
                }
                foreach(string json in new[]{"null","[]","true"})Reject(()=>NativeRegistryIds.Pin(json));
            });
        }
    }
}
