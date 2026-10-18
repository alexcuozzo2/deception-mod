using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Deception.Core;
using Deception.PolyModAdapter;

namespace Deception.Tests
{
    public static class ContentLifecycleTests
    {
        static int assertions;
        static void Check(bool condition,string message){assertions++;if(!condition)throw new Exception(message);}
        static void Reject(Action action,string message){assertions++;try{action();}catch(RuleViolation){return;}throw new Exception(message);}
        static JsonNode Clone(JsonNode node)=>JsonNode.Parse(node.ToJsonString())!;
        // This patch has ordinary object/scalar/replacement-array nodes only.
        // Mirrors supported JsonMerger behavior for those nodes, without claiming
        // coverage of unrelated additive/removal or template syntax.
        static void Merge(JsonObject target,JsonObject patch)
        {
            foreach(var pair in patch)
                if(pair.Value is JsonObject obj&&target[pair.Key] is JsonObject prior)Merge(prior,obj);
                else target[pair.Key]=pair.Value is null?null:Clone(pair.Value);
        }
        static string Generate(string input)=>NativeExchangeDiagnosticResearch.Add(NativeExchangeCatalog.Add(
            SmuggleCatalog.Add(RecruitmentCatalog.Add(TechTree.AddDeception(input).Json).Json)));
        static void Validate(string input)
        {RecruitmentCatalog.ValidateReentry(input);SmuggleCatalog.ValidateReentry(input);NativeExchangeCatalog.ValidateReentry(input);NativeExchangeDiagnosticResearch.ValidateReentry(input);}
        static string Allocate(string input)
        {
            var root=JsonNode.Parse(input)!.AsObject();var tail=NativeRegistryIds.AllocationTail(input);
            var saved=tail.Select(e=>(entry:e,node:Clone(root[e.Section]![e.Id]!))).ToArray();
            foreach(var entry in tail)root[entry.Section]!.AsObject().Remove(entry.Id);
            int counter=1000;
            foreach(string section in new[]{"techData","unitData","improvementData"})
                foreach(var pair in root[section]!.AsObject())
                    if(pair.Value!["idx"]!.GetValue<int>()==-1)pair.Value["idx"]=counter++;
            Check(counter==1033,"Historical first batch order changed");
            foreach(var item in saved){item.node["idx"]=counter++;root[item.entry.Section]![item.entry.Id]=item.node;}
            Check(counter==1087,"Tail allocation count changed");
            NativeRegistryIds.Validate(root.ToJsonString());return root.ToJsonString();
        }
        public static int Run()
        {
            string project=Environment.GetEnvironmentVariable("DECEPTION_CONTENT_PROJECT")!;
            var patch=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"content/patch.json")))!.AsObject();
            ContentDefinitions.Configure(patch.ToJsonString());
            foreach(string fixture in new[]{"native-tree.captured.json","native-tree.synthetic.json"})
            {
                var native=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"tests",fixture)))!.AsObject();
                var captured=JsonNode.Parse(File.ReadAllText(Path.Combine(project,"tests/native-tree.captured.json")))!;
                native["unitData"]=Clone(captured["unitData"]!);
                native["tribeData"]!["aquarion"]!["unitOverrides"]=Clone(captured["tribeData"]!["aquarion"]!["unitOverrides"]!);
                var improvements=new JsonObject();int idx=70;
                foreach(string id in new[]{"harvestfruit","fishing","hunting"})improvements[id]=new JsonObject{
                    ["idx"]=idx++,["cost"]=2,["prefab"]=id,["hidden"]=false,
                    ["improvementAbilities"]=new JsonArray("native"),["terrainRequirements"]=new JsonArray(),
                    ["creates"]=new JsonArray(),["growthRewards"]=new JsonArray(),["growthRate"]=0,
                    ["unknownNativeField"]=new JsonObject{["preserve"]="exact"}};
                native["improvementData"]=improvements;
                string pristine=native.ToJsonString();string expected=Generate(pristine);
                Merge(native,patch);
                string normalized=ContentDefinitions.NormalizeInput(native.ToJsonString());
                Check(TechTree.Canonical(JsonNode.Parse(normalized))==TechTree.Canonical(JsonNode.Parse(pristine)),"Normalization changed native definitions");
                string generated=Generate(normalized);Validate(generated);
                Check(generated==expected,"Patch normalization changed historical property ordering");
                string allocated=Allocate(generated);Validate(allocated);
                Check(TechTree.Canonical(JsonNode.Parse(allocated))==TechTree.Canonical(JsonNode.Parse(NativeRegistryIds.Pin(expected))),"Mapped IDs differ from saved identity contract");
                // Reprocessing completed GLD without a patch is idempotent.
                Check(NativeRegistryIds.Pin(allocated)==allocated,"Completed GLD pin was not idempotent");
                // A freshly read GLD in a process with existing mappings is NOT
                // the same as a completed GLD despite sharing static tech IDs.
                var fresh=JsonNode.Parse(pristine)!.AsObject();Merge(fresh,patch);
                Check(fresh["improvementData"]!["dcp_exchange_regroup"] is null,"Fresh patch acquired dynamic marker");
                string rebuilt=NativeRegistryIds.Pin(Generate(ContentDefinitions.NormalizeInput(fresh.ToJsonString())));
                Validate(rebuilt);NativeRegistryIds.Validate(rebuilt);
                Check(TechTree.Canonical(JsonNode.Parse(rebuilt))==TechTree.Canonical(JsonNode.Parse(allocated)),"Fresh reload changed definitions or saved IDs");
                // A repeated patch resets its owned idx fields; validate values,
                // restore accepted IDs, then the adapter must check native enums.
                var repeated=JsonNode.Parse(allocated)!.AsObject();Merge(repeated,patch);
                Check(repeated["improvementData"]!["dcp_exchange_regroup"] is not null,"Processed GLD lost dynamic marker");
                Validate(repeated.ToJsonString());
                Reject(()=>NativeRegistryIds.Validate(repeated.ToJsonString()),"Patch-reset IDs incorrectly treated as mapped");
                var restored=NativeRegistryIds.Pin(repeated.ToJsonString());NativeRegistryIds.Validate(restored);Validate(restored);
                Check(TechTree.Canonical(JsonNode.Parse(restored))==TechTree.Canonical(JsonNode.Parse(allocated)),"Repeated patch changed mapped registry");
                repeated["unitData"]!["dcp_mimic"]!["idx"]=9999;
                Reject(()=>NativeRegistryIds.Pin(repeated.ToJsonString()),"Foreign positive index was silently accepted");
                var bad=JsonNode.Parse(pristine)!.AsObject();Merge(bad,patch);bad["unitData"]!["dcp_mimic"]!["health"]=20;
                Reject(()=>ContentDefinitions.NormalizeInput(bad.ToJsonString()),"Changed static content was silently accepted");
            }
            Console.WriteLine("{\"scope\":\"managed GLD lifecycle only\",\"assertions\":"+assertions+",\"failed\":0}");return 0;
        }
    }
}
