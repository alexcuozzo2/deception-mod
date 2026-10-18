using System;
using System.Linq;
using System.Text.Json.Nodes;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        // Synthetic registry contracts only. Exact native harvest metadata and
        // population effects must also be verified in the installed game.
        static void SmuggleTests()
        {
            JsonObject Fixture()
            {
                var improvements=new JsonObject();
                foreach(var id in new[]{"harvestfruit","fishing","hunting"})
                    improvements[id]=new JsonObject {
                        ["idx"]=7,["cost"]=2,["hidden"]=false,["prefab"]=id,
                        ["improvementAbilities"]=new JsonArray("manual","fixture-special-effect"),
                        ["terrainRequirements"]=new JsonArray(new JsonObject{["resource"]="fruit"}),["shouldSuggestUnlock"]=true,
                        ["creates"]=new JsonArray("fixture-unit"),
                        ["growthRewards"]=new JsonArray("fixture-population"),["growthRate"]=3,
                        ["unknownNativeField"]=new JsonObject{["preserve"]="exact"}
                    };
                improvements["farm"]=new JsonObject{["cost"]=5,["fixture"]=true};
                return new JsonObject {
                    ["improvementData"]=improvements,
                    ["techData"]=new JsonObject{["organization"]=new JsonObject{["improvementUnlocks"]=new JsonArray("harvestfruit")}},
                    ["tribeData"]=new JsonObject{["fixture"]=new JsonObject{["startingTech"]=new JsonArray("organization")}},
                    ["unitData"]=new JsonObject{["warrior"]=new JsonObject{["health"]=100}}
                };
            }
            Test("M2-001-smuggle-registry-preserves-native-data",new[]{"DEC-093","DEC-197"},()=> {
                var before=Fixture();string original=before.ToJsonString();
                var after=JsonNode.Parse(SmuggleCatalog.Add(original))!.AsObject();
                Assert(before.ToJsonString()==original,"Registration mutated caller data");
                foreach(var entry in before)
                    if(entry.Key!="improvementData")Assert(TechTree.Canonical(entry.Value)==TechTree.Canonical(after[entry.Key]),entry.Key+" changed");
                foreach(var entry in before["improvementData"]!.AsObject())
                    Assert(TechTree.Canonical(entry.Value)==TechTree.Canonical(after["improvementData"]![entry.Key]),entry.Key+" changed");
                var added=after["improvementData"]!.AsObject().Select(x=>x.Key).Except(before["improvementData"]!.AsObject().Select(x=>x.Key)).OrderBy(x=>x).ToArray();
                Assert(added.SequenceEqual(new[]{"dcp_smuggle_animal","dcp_smuggle_fish","dcp_smuggle_fruit"}),"Unexpected command carrier or unlock");
                Assert(SmuggleCatalog.Harvests.Select(x=>(x.Id,x.NativeHarvest,x.Resource)).SequenceEqual(new[]{
                    ("dcp_smuggle_fruit","harvestfruit","fruit"),("dcp_smuggle_fish","fishing","fish"),("dcp_smuggle_animal","hunting","game")}));
            });
            Test("M2-002-smuggle-carriers-cannot-run-native-building-effects",new[]{"DEC-093","DEC-172"},()=> {
                var after=JsonNode.Parse(SmuggleCatalog.Add(Fixture().ToJsonString()))!;
                foreach(var id in new[]{"dcp_smuggle_fruit","dcp_smuggle_fish","dcp_smuggle_animal"})
                {
                    var carrier=after["improvementData"]![id]!;
                    Assert(carrier["hidden"]!.GetValue<bool>()&&carrier["idx"]!.GetValue<int>()==-1&&carrier["cost"]!.GetValue<int>()==2);
                    Assert(carrier["improvementAbilities"]!.AsArray().Count==0&&carrier["creates"]!.AsArray().Count==0&&
                        carrier["growthRewards"]!.AsArray().Count==0&&carrier["growthRate"]!.GetValue<int>()==0,
                        "A carrier retained native exhaustion/reveal/spawn/growth effects");
                    Assert(carrier["terrainRequirements"]!.AsArray().Count==0&&!carrier["shouldSuggestUnlock"]!.GetValue<bool>(),"Hidden carrier can intercept native resource lookup or research suggestions");
                    Assert(carrier["unknownNativeField"]!["preserve"]!.GetValue<string>()=="exact","Unrelated native metadata was discarded");
                }
            });
            Test("M2-003-smuggle-reentry-validates-owned-carriers",new[]{"DEC-171","DEC-179"},()=> {
                var valid=JsonNode.Parse(SmuggleCatalog.Add(Fixture().ToJsonString()))!;
                int index=1000;foreach(var h in SmuggleCatalog.Harvests)valid["improvementData"]![h.Id]!["idx"]=index++;
                SmuggleCatalog.ValidateReentry(valid.ToJsonString());
                foreach(string field in new[]{"cost","hidden","improvementAbilities","creates","growthRate"})
                {
                    var changed=JsonNode.Parse(valid.ToJsonString())!;var carrier=changed["improvementData"]!["dcp_smuggle_fruit"]!;
                    if(field=="hidden")carrier[field]=false;
                    else if(field is "improvementAbilities" or "creates")carrier[field]=new JsonArray("unsafe");
                    else carrier[field]=99;
                    Reject(()=>SmuggleCatalog.ValidateReentry(changed.ToJsonString()));
                }
                var unknown=JsonNode.Parse(valid.ToJsonString())!;
                unknown["improvementData"]!["dcp_smuggle_unknown"]=new JsonObject();
                Reject(()=>SmuggleCatalog.ValidateReentry(unknown.ToJsonString()));
            });
            Test("M2-004-smuggle-conflict-and-native-source-drift-fail-closed",new[]{"DEC-179","DEC-197"},()=> {
                var original=Fixture();var registered=SmuggleCatalog.Add(original.ToJsonString());
                Reject(()=>SmuggleCatalog.Add(registered));
                foreach(var h in SmuggleCatalog.Harvests)
                {
                    var missing=Fixture();missing["improvementData"]!.AsObject().Remove(h.NativeHarvest);
                    string untouched=missing.ToJsonString();Reject(()=>SmuggleCatalog.Add(untouched));Assert(missing.ToJsonString()==untouched);
                    var changed=Fixture();changed["improvementData"]![h.NativeHarvest]!["cost"]=3;
                    Reject(()=>SmuggleCatalog.Add(changed.ToJsonString()));
                    var changedReentry=JsonNode.Parse(registered)!;changedReentry["improvementData"]![h.NativeHarvest]!["cost"]=3;
                    Reject(()=>SmuggleCatalog.ValidateReentry(changedReentry.ToJsonString()));
                }
                var collision=Fixture();collision["improvementData"]!["dcp_smuggle_unrecognized"]=new JsonObject();
                Reject(()=>SmuggleCatalog.Add(collision.ToJsonString()));
            });
        }
    }
}
