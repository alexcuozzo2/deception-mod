using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        static void RecruitmentTests()
        {
            string input=File.ReadAllText(Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE")??"tests/native-tree.captured.json");
            string BaseTree()=>TechTree.AddDeception(input).Json;
            RecruitmentMutation Make()=>RecruitmentCatalog.Add(BaseTree());
            Test("M1-001-correct-deception-localization-key",new[]{"DEC-001"},()=>
                Assert(RecruitmentCatalog.NativeTerm(RecruitmentCatalog.EscapeTerm("technology_names_",TechTree.DeceptionId))=="technology.names.dcp_deception"));
            Test("M1-002-old-localization-key-does-not-match",new[]{"DEC-001"},()=>
                Assert(RecruitmentCatalog.NativeTerm("technology_names_dcp_deception")!="technology.names.dcp_deception"));
            Test("M1-003-supported-forms-have-reviewed-source-definitions",new[]{"DEC-003","DEC-197"},()=> {
                var r=Make();Assert(r.Forms.Length==59);Assert(r.Forms.Select(f=>f.NativeId).Distinct().Count()==59);
                Assert(r.Forms.Where(f=>!SpyCatalog.IsSpyId(f.NativeId)&&!FoggerCatalog.IsFoggerId(f.NativeId)).All(f=>JsonNode.Parse(input)!["unitData"]![f.NativeId] is JsonObject));
                Assert(r.Forms.Single(f=>f.NativeId==SpyCatalog.Id).Label=="Spy"&&JsonNode.Parse(r.Json)!["unitData"]![SpyCatalog.Id] is JsonObject);
            });
            Test("M1-004-original-registries-preserved",new[]{"DEC-001","DEC-196"},()=> {
                var before=JsonNode.Parse(input)!;var after=JsonNode.Parse(Make().Json)!;
                AquarionCatalog.RemoveValidatedOverrides(after.AsObject());
                Assert(TechTree.Canonical(before["tribeData"])==TechTree.Canonical(after["tribeData"]));
                foreach(var pair in before["unitData"]!.AsObject())Assert(TechTree.Canonical(pair.Value)==TechTree.Canonical(after["unitData"]![pair.Key]));
                foreach(var pair in before["techData"]!.AsObject())if(pair.Key!="basic")Assert(TechTree.Canonical(pair.Value)==TechTree.Canonical(after["techData"]![pair.Key]));
                Assert(after["techData"]!["basic"]!["idx"]!.GetValue<int>()==0);
            });
            Test("M1-005-prototype-hp-and-no-offense-data",new[]{"DEC-003","DEC-008"},()=> {
                var after=JsonNode.Parse(Make().Json)!;
                foreach(var pair in after["unitData"]!.AsObject().Where(p=>RecruitmentCatalog.IsMimicId(p.Key))) {
                    Assert(pair.Value!["health"]!.GetValue<int>()==10&&pair.Value["cost"]!.GetValue<int>()==(RecruitmentCatalog.NavalCopyOnly.Contains(pair.Key.Replace("dcp_mimic_form_", ""))?0:1));
                    Assert(pair.Value["attack"]!.GetValue<int>()==0&&pair.Value["defence"]!.GetValue<int>()==0&&pair.Value["range"]!.GetValue<int>()==0);
                }
            });
            Test("M1-006-native-registry-includes-concrete-forms",new[]{"DEC-003"},()=> {
                var after=JsonNode.Parse(Make().Json)!;var d=after["techData"]![TechTree.DeceptionId]!;
                var ids=d["unitUnlocks"]!.AsArray().Select(x=>x!.GetValue<string>()).ToArray();
                Assert(ids.Length==60&&ids[0]==RecruitmentCatalog.MenuId);
                Assert(RecruitmentCatalog.CarrierForms.All(f=>ids.Contains(RecruitmentCatalog.Variant(f))));
                Assert(d["techUnlocks"]!.AsArray().Count==2&&d["cost"]!.GetValue<int>()==1);
            });
            Test("M1-007-appearance-native-prefab-not-ordinary-identity",new[]{"DEC-003"},()=> {
                var r=Make();var after=JsonNode.Parse(r.Json)!;
                foreach(var form in r.Forms) {
                    var u=after["unitData"]![form.VariantId]!;Assert(form.VariantId!=form.NativeId);
                    Assert(u["prefab"]!.GetValue<string>()==form.NativeId&&!u["hidden"]!.GetValue<bool>());
                    Assert(u["idx"]!.GetValue<int>()==-1);
                }
                Assert(!after["unitData"]![RecruitmentCatalog.MenuId]!["hidden"]!.GetValue<bool>());
            });
            Test("M1-008-dangerous-passives-not-borrowed",new[]{"DEC-008"},()=> {
                var after=JsonNode.Parse(Make().Json)!;
                var prohibited=new[]{"heal","convert","poison","explode","infiltrate","swarm","autofreeze","eat","grow","doubleattack","persist","splash","stomp","tentacles"};
                foreach(var pair in after["unitData"]!.AsObject().Where(p=>RecruitmentCatalog.IsMimicId(p.Key))) {
                    var skills=pair.Value!["unitAbilities"]!.AsArray().Select(x=>x!.GetValue<string>()).ToArray();
                    Assert(!skills.Intersect(prohibited).Any());
                }
                Assert(after["unitData"]![RecruitmentCatalog.Variant("warrior")]!["unitAbilities"]!.AsArray().Any(x=>x!.GetValue<string>()=="land"));
            });
            Test("M1-009-source-movement-and-traversal-preserved",new[]{"DEC-003","DEC-197"},()=> {
                var before=JsonNode.Parse(input)!;var r=Make();var after=JsonNode.Parse(r.Json)!;
                foreach(var f in r.Forms)Assert(after["unitData"]![f.VariantId]!["movement"]!.GetValue<int>()==((SpyCatalog.IsSpyId(f.NativeId)||FoggerCatalog.IsFoggerId(f.NativeId))?after:before)["unitData"]![f.NativeId]!["movement"]!.GetValue<int>());
                Assert(r.Forms.Single(x=>x.NativeId=="amphibian").NativeWaterTravel);
                Assert(r.Forms.Single(x=>x.NativeId=="phychi").NativeWaterTravel);
                Assert(!r.Forms.Single(x=>x.NativeId=="warrior").NativeWaterTravel);
            });
            Test("M1-010-no-automatic-growth-or-promotion",new[]{"DEC-008","DEC-189"},()=> {
                var after=JsonNode.Parse(Make().Json)!;
                foreach(var p in after["unitData"]!.AsObject().Where(p=>RecruitmentCatalog.IsMimicId(p.Key))) {
                    Assert(p.Value!["promotionLimit"]!.GetValue<int>()==0);
                    Assert(p.Value["upgradesFrom"] is null&&p.Value["promotesTo"] is null&&p.Value["growthRate"] is null);
                }
            });
            Test("M1-011-unimplemented-forms-explicitly-deferred",new[]{"DEC-197"},()=> {
                var r=Make();Assert(r.DeferredForms.SequenceEqual(RecruitmentCatalog.Deferred));
                Assert(r.DeferredForms.Length==0&&r.Forms.Any(f=>f.NativeId=="mooni")&&r.Forms.Any(f=>f.NativeId=="moth"));
                Assert(!r.DeferredForms.Contains("cloak")&&!r.DeferredForms.Contains("mermaidcloak"));
                Assert(!r.Forms.Select(f=>f.NativeId).Intersect(r.DeferredForms).Any());
                Assert(!r.Forms.Any(f=>new[]{"segment","battleship"}.Contains(f.NativeId)));
            });
            Test("M1-012-duplicate-identifiers-rejected",new[]{"DEC-179"},()=>Reject(()=>RecruitmentCatalog.Add(Make().Json),"already"));
            Test("M1-013-reentry-accepts-assigned-indices",new[]{"DEC-171"},()=> {
                var after=JsonNode.Parse(Make().Json)!;int idx=1000;
                foreach(var pair in after["unitData"]!.AsObject().Where(p=>RecruitmentCatalog.IsMimicId(p.Key)))pair.Value!["idx"]=idx++;
                RecruitmentCatalog.ValidateReentry(after.ToJsonString());
            });
            Test("M1-014-reentry-rejects-prototype-tampering",new[]{"DEC-179"},()=> {
                var after=JsonNode.Parse(Make().Json)!;after["unitData"]![RecruitmentCatalog.Variant("warrior")]!["attack"]=20;
                Reject(()=>RecruitmentCatalog.ValidateReentry(after.ToJsonString()),"invariants");
            });
            Test("M1-015-pinned-health-scale-guard",new[]{"DEC-179"},()=> {
                var b=JsonNode.Parse(BaseTree())!;b["unitData"]!["warrior"]!["health"]=10;
                Reject(()=>RecruitmentCatalog.Add(b.ToJsonString()),"scale");
            });
            Test("M1-016-research-cue-policy",new[]{"DEC-001"},()=> {
                Assert(ResearchFeedbackPolicy.NeedsFallback(true,true,true,true,false,false));
                Assert(!ResearchFeedbackPolicy.NeedsFallback(false,true,true,true,false,false));
                Assert(!ResearchFeedbackPolicy.NeedsFallback(true,false,true,true,false,false));
                Assert(!ResearchFeedbackPolicy.NeedsFallback(true,true,false,true,false,false));
                Assert(!ResearchFeedbackPolicy.NeedsFallback(true,true,true,false,false,false));
                Assert(!ResearchFeedbackPolicy.NeedsFallback(true,true,true,true,true,false));
                Assert(!ResearchFeedbackPolicy.NeedsFallback(true,true,true,true,false,true));
            });
            Test("M1-017-variant-localization-literal-underscores",new[]{"DEC-003"},()=> {
                foreach(var id in RecruitmentCatalog.Supported.Select(RecruitmentCatalog.Variant).Append(RecruitmentCatalog.MenuId))
                    Assert(RecruitmentCatalog.NativeTerm(RecruitmentCatalog.EscapeTerm("unit_names_",id))=="unit.names."+id);
            });
            Test("M1-018-all-tribes-preserve-native-starters",new[]{"DEC-196"},()=> {
                var r=Make();var a=JsonNode.Parse(r.Json)!;var b=JsonNode.Parse(input)!;AquarionCatalog.RemoveValidatedOverrides(a.AsObject());
                foreach(var tribe in b["tribeData"]!.AsObject().Where(t=>TechTree.IsPlayable(t.Value!.AsObject()))) {
                    Assert(TechTree.Canonical(tribe.Value)==TechTree.Canonical(a["tribeData"]![tribe.Key]));
                }
                Assert(TechTree.AddDeception(input).PlayableTribes.Length==16);
            });
            Test("M1-019-own-roster-override-fixture",new[]{"DEC-197"},()=> {
                var b=JsonNode.Parse(input)!;
                Assert(b["tribeData"]!["aquarion"]!["unitOverrides"]!["warrior"]!.GetValue<string>()=="mermaidwarrior");
                Assert(b["tribeData"]!["cymanti"]!["unitOverrides"]!["rider"]!.GetValue<string>()=="hexapod");
                Assert(RecruitmentCatalog.Supported.Contains("mermaidwarrior")&&RecruitmentCatalog.Supported.Contains("hexapod"));
            });
            Test("A14-001-field-carriers-not-city-recruitment",new[]{"DEC-031","DEC-035"},()=> {
                var r=Make();var native=JsonNode.Parse(input)!;
                Assert(RecruitmentCatalog.FieldCopyOnly.All(f=>r.Forms.Any(x=>x.NativeId==f)));
                var all=r.Forms.Select(f=>f.NativeId).ToHashSet(StringComparer.Ordinal);
                Assert(!RecruitmentEligibility.Choices(r.Forms,all).Any(f=>RecruitmentCatalog.FieldCopyOnly.Contains(f.NativeId)));
                foreach(string f in RecruitmentCatalog.FieldCopyOnly)Assert(!RecruitmentEligibility.Permitted(true,f,all));
                foreach(var roster in NativeRecruitmentRosters.AllResearchRosters(input,all,RecruitmentCatalog.Deferred).Values)
                    Assert(!roster.Supported.Intersect(RecruitmentCatalog.FieldCopyOnly).Any());
            });
            Test("A50-001-mimic-carriers-have-dash-without-scout-or-terrain",new[]{"DEC-040"},()=> {
                var r=Make();var after=JsonNode.Parse(r.Json)!;var before=JsonNode.Parse(input)!;
                foreach(string id in RecruitmentCatalog.CarrierForms.Select(RecruitmentCatalog.Variant).Append(RecruitmentCatalog.MenuId).Append(AquarionCatalog.MimicId)) {
                    var abilities=after["unitData"]![id]!["unitAbilities"]!.AsArray();
                    Assert(!abilities.Select(x=>x!.GetValue<string>()).Intersect(new[]{"scout","autofreeze","autoflood","algae"}).Any());
                    Assert(abilities.Count(x=>x!.GetValue<string>()=="dash")==1);
                }
                foreach(string f in new[]{"defender","catapult","giant"})
                    Assert(!before["unitData"]![f]!["unitAbilities"]!.AsArray().Any(x=>x!.GetValue<string>()=="dash"));
            });
            Test("NAV-007-naval-carriers-append-without-city-recruitment-or-offense",new[]{"DEC-003","DEC-008","DEC-173"},()=> {
                var r=Make();var native=JsonNode.Parse(input)!;var added=JsonNode.Parse(r.Json)!;
                string[] expected={"transportship","scoutship","rammership","bombership","juggernaut","pirate","cloak_boat"};
                Assert(RecruitmentCatalog.NavalCopyOnly.SequenceEqual(expected));
                var all=r.Forms.Select(x=>x.NativeId).ToHashSet(StringComparer.Ordinal);
                foreach(var item in expected.Select((form,n)=>(form,n))) {
                    string id=RecruitmentCatalog.Variant(item.form);var data=added["unitData"]![id]!;
                    Assert(NativeRegistryIds.Entries.Single(e=>e.Id==id).Index==1048+item.n);
                    Assert(data["cost"]!.GetValue<int>()==0&&data["health"]!.GetValue<int>()==10&&data["attack"]!.GetValue<int>()==0&&data["defence"]!.GetValue<int>()==0);
                    Assert(data["movement"]!.GetValue<int>()==native["unitData"]![item.form]!["movement"]!.GetValue<int>());
                    Assert(!RecruitmentEligibility.Permitted(true,item.form,all));
                }
                Assert(RecruitmentCatalog.Supported.Length==38&&!RecruitmentEligibility.Choices(r.Forms,all).Any(f=>expected.Contains(f.NativeId)));
                foreach(var roster in NativeRecruitmentRosters.AllResearchRosters(input,all,RecruitmentCatalog.Deferred).Values)
                    Assert(!roster.Supported.Intersect(expected).Any());
            });
            Test("CLOAK-001-carriers-preserve-one-hp-and-no-infiltrate",new[]{"DEC-003","DEC-008"},()=> {
                var source=JsonNode.Parse(input)!;var added=JsonNode.Parse(Make().Json)!;
                foreach(string form in new[]{"cloak","mermaidcloak"}) {
                    var native=source["unitData"]![form]!;var carrier=added["unitData"]![RecruitmentCatalog.Variant(form)]!;
                    Assert(native["health"]!.GetValue<int>()==50&&native["unitAbilities"]!.AsArray().Any(x=>x!.GetValue<string>()=="infiltrate"),"Fixture no longer describes a native Cloak");
                    Assert(carrier["health"]!.GetValue<int>()==10&&carrier["cost"]!.GetValue<int>()==1,"Cloak Mimic changed actual HP or purchase price");
                    Assert(carrier["attack"]!.GetValue<int>()==0&&carrier["defence"]!.GetValue<int>()==0&&carrier["range"]!.GetValue<int>()==0,"Cloak carrier gained offensive capability");
                    Assert(carrier["movement"]!.GetValue<int>()==native["movement"]!.GetValue<int>());
                    Assert(!carrier["unitAbilities"]!.AsArray().Any(x=>x!.GetValue<string>()=="infiltrate"),"Cloak carrier gained genuine city infiltration");
                    Assert(TechTree.Canonical(native)==TechTree.Canonical(added["unitData"]![form]),"Genuine Cloak definition was modified");
                    var corrupt=JsonNode.Parse(added.ToJsonString())!;
                    corrupt["unitData"]![RecruitmentCatalog.Variant(form)]!["unitAbilities"]!.AsArray().Add("infiltrate");
                    Reject(()=>RecruitmentCatalog.ValidateReentry(corrupt.ToJsonString()),"definition changed");
                }
            });
            Test("M1-020-source-is-unchanged-on-rejection",new[]{"DEC-179"},()=> {
                var b=JsonNode.Parse(BaseTree())!;b["unitData"]!.AsObject().Remove("warrior");string original=b.ToJsonString();
                Reject(()=>RecruitmentCatalog.Add(original));Assert(b.ToJsonString()==original);
            });
            Test("SPY-001-genuine-definition-and-private-carrier-remain-distinct",new[]{"DEC-003","DEC-008"},()=> {
                var after=JsonNode.Parse(Make().Json)!;var spy=after["unitData"]![SpyCatalog.Id]!;
                Assert(new[]{SpyCatalog.Id,SpyCatalog.AquarionId}.All(id=>{
                    var data=after["unitData"]![id]!;
                    return data["cost"]!.GetValue<int>()==5&&data["health"]!.GetValue<int>()==50&&data["defence"]!.GetValue<int>()==10&&data["movement"]!.GetValue<int>()==2;
                }),"Genuine Spy and Spymaid must both cost 5 stars and have 5 HP, 1 defense and 2 movement.");
                Assert(spy["attack"]!.GetValue<int>()==0&&spy["range"]!.GetValue<int>()==0&&spy["promotionLimit"]!.GetValue<int>()==0);
                Assert(spy["unitAbilities"]!.AsArray().Select(x=>x!.GetValue<string>()).SequenceEqual(new[]{"scout"}));
                Assert(!spy["hidden"]!.GetValue<bool>()&&!spy["independent"]!.GetValue<bool>()&&spy["prefab"]!.GetValue<string>()=="warrior");
                var unlocks=after["techData"]![TechTree.EspionageId]!["unitUnlocks"]!.AsArray();
                Assert(unlocks.Count==1&&unlocks[0]!.GetValue<string>()==SpyCatalog.Id);
                var carrier=after["unitData"]![RecruitmentCatalog.Variant(SpyCatalog.Id)]!;
                Assert(carrier["cost"]!.GetValue<int>()==1&&carrier["health"]!.GetValue<int>()==10&&carrier["defence"]!.GetValue<int>()==0);
                Assert(carrier["attack"]!.GetValue<int>()==0&&carrier["range"]!.GetValue<int>()==0&&carrier["prefab"]!.GetValue<string>()==SpyCatalog.Id);
                Assert(carrier["unitAbilities"]!.AsArray().Select(x=>x!.GetValue<string>()).SequenceEqual(new[]{"dash"}));
                var aquatic=after["unitData"]![SpyCatalog.AquarionId]!;
                Assert(aquatic["unitAbilities"]!.AsArray().Select(x=>x!.GetValue<string>()).SequenceEqual(new[]{"scout","swim","amphibious"}));
            });
            Test("SPY-002-reentry-rejects-definition-and-unlock-tampering",new[]{"DEC-171","DEC-179"},()=> {
                string valid=Make().Json;RecruitmentCatalog.ValidateReentry(valid);
                foreach(string field in new[]{"health","defence","cost","movement","attack","range"}) {
                    var bad=JsonNode.Parse(valid)!;bad["unitData"]![SpyCatalog.Id]![field]=bad["unitData"]![SpyCatalog.Id]![field]!.GetValue<int>()+1;
                    Reject(()=>RecruitmentCatalog.ValidateReentry(bad.ToJsonString()),"Spy definition changed");
                }
                foreach(var skill in new[]{"convert","heal","infiltrate","dash","hide"}) {
                    var bad=JsonNode.Parse(valid)!;bad["unitData"]![SpyCatalog.Id]!["unitAbilities"]!.AsArray().Add(skill);
                    Reject(()=>RecruitmentCatalog.ValidateReentry(bad.ToJsonString()),"Spy definition changed");
                }
                var wrong=JsonNode.Parse(valid)!;wrong["techData"]![TechTree.EspionageId]!["unitUnlocks"]=new JsonArray("warrior");
                Reject(()=>RecruitmentCatalog.ValidateReentry(wrong.ToJsonString()),"Spy research unlock changed");
                var assigned=JsonNode.Parse(valid)!;assigned["unitData"]![SpyCatalog.Id]!["idx"]=1065;
                RecruitmentCatalog.ValidateReentry(assigned.ToJsonString());Assert(Make().Json==valid,"Reentry validation contaminated source generation");
            });
            Test("AQUA-0371-native-replacements-and-strict-reentry",new[]{"DEC-003","DEC-171"},()=> {
                var valid=Make().Json;var a=JsonNode.Parse(valid)!;var overrides=a["tribeData"]!["aquarion"]!["unitOverrides"]!;
                Assert(overrides[RecruitmentCatalog.MenuId]!.GetValue<string>()==AquarionCatalog.MimicId);
                Assert(overrides[SpyCatalog.Id]!.GetValue<string>()==SpyCatalog.AquarionId);
                foreach(var id in new[]{AquarionCatalog.MimicId,SpyCatalog.AquarionId}) {
                    var data=a["unitData"]![id]!;var abilities=data["unitAbilities"]!.AsArray().Select(x=>x!.GetValue<string>()).ToArray();
                    Assert(abilities.Contains("swim")&&abilities.Contains("amphibious")&&!abilities.Contains("carry"));
                    Assert(data["prefab"]!.GetValue<string>()=="mermaidwarrior"&&data["movement"]!.GetValue<int>()==(id==SpyCatalog.AquarionId?2:1));
                    var bad=JsonNode.Parse(valid)!;bad["unitData"]![id]!["unitAbilities"]!.AsArray().Add("fly");Reject(()=>RecruitmentCatalog.ValidateReentry(bad.ToJsonString()));
                }
                Assert(!a["unitData"]![SpyCatalog.AquarionId]!["unitAbilities"]!.AsArray().Any(x=>x!.GetValue<string>()=="dash"));
                var wrong=JsonNode.Parse(valid)!;wrong["tribeData"]!["aquarion"]!["unitOverrides"]![SpyCatalog.Id]=SpyCatalog.Id;Reject(()=>RecruitmentCatalog.ValidateReentry(wrong.ToJsonString()));
                Assert(RecruitmentCatalog.Label(AquarionCatalog.MimicId)=="Mermimic"&&RecruitmentCatalog.Label(SpyCatalog.AquarionId)=="Spymaid");
                Assert(RecruitmentCatalog.Variant(AquarionCatalog.MimicId)==AquarionCatalog.MimicId&&SpyCatalog.IsSpyId(SpyCatalog.AquarionId));
            });
            Test("CITY-0361-001-five-water-forms-retain-mobility-without-real-powers",new[]{"DEC-003","DEC-008"},()=> {
                var before=JsonNode.Parse(input)!;var after=JsonNode.Parse(Make().Json)!;
                foreach(string form in new[]{"raychi","jelly","shark","aquapult","island"}) {
                    var native=before["unitData"]![form]!;var carrier=after["unitData"]![RecruitmentCatalog.Variant(form)]!;
                    Assert(RecruitmentCatalog.Supported.Contains(form)&&!RecruitmentCatalog.Deferred.Contains(form)&&!RecruitmentCatalog.NavalCopyOnly.Contains(form));
                    Assert(carrier["cost"]!.GetValue<int>()==1&&carrier["health"]!.GetValue<int>()==10&&carrier["attack"]!.GetValue<int>()==0&&carrier["defence"]!.GetValue<int>()==0&&carrier["range"]!.GetValue<int>()==0);
                    Assert(carrier["movement"]!.GetValue<int>()==native["movement"]!.GetValue<int>()&&TechTree.Canonical(carrier["movementTerrain"])==TechTree.Canonical(native["movementTerrain"]));
                    var abilities=carrier["unitAbilities"]!.AsArray().Select(x=>x!.GetValue<string>()).ToArray();
                    Assert(abilities.Contains("swim")&&abilities.Contains("water")&&abilities.Contains("dash"));
                    Assert(!abilities.Intersect(new[]{"tentacles","stomp","algae","poison","drench","explode","surprise"}).Any());
                    Assert(TechTree.Canonical(native)==TechTree.Canonical(after["unitData"]![form]));
                }
            });
        }
    }
}
