using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Deception.Core
{
    public sealed record TribeTreeAudit(string Tribe, string Category, string Root, int RootIndex,
        string[] StartingTech, string[] NativeBranches, string[] ExtendedBranches);
    public sealed record TreeMutation(string Json, string[] PlayableTribes, string[] ModifiedRoots,
        string[] OriginalBranches, TribeTreeAudit[] TribeAudit);
    public static class TechTree
    {
        public const string DeceptionId="dcp_deception";
        public const string PropagandaId="dcp_propaganda",CamouflageId="dcp_camouflage",
            IntelligenceId="dcp_intelligence",EspionageId="dcp_espionage";
        // Keep the registered numeric identity; the visible technology and its
        // place in the branch changed in A52. New online peers share one build.
        public const string CounterintelligenceId=CamouflageId;
        public static readonly IReadOnlyList<(string Id,string? Parent,int Tier)> Branch=System.Array.AsReadOnly(new[]{
            (DeceptionId,(string?)null,1),(PropagandaId,DeceptionId,2),(IntelligenceId,DeceptionId,2),
            (EspionageId,PropagandaId,3),(CounterintelligenceId,IntelligenceId,3)});
        public static bool IsBranchId(string id)=>Branch.Any(t=>t.Id==id);
        public const string RootPrefix="dcp_root_"; // REJECT legacy M0.1 IDs. Never generate these.
        static JsonObject Object(JsonNode? node,string label) => node as JsonObject ?? throw new RuleViolation("Expected object: "+label);
        static JsonArray Array(JsonNode? node,string label) => node as JsonArray ?? throw new RuleViolation("Expected array: "+label);
        static string[] Strings(JsonNode? node,string label) => Array(node,label).Select(x=>x?.GetValue<string>() ?? throw new RuleViolation("Null ID in "+label)).ToArray();
        public static int Category(JsonObject tribe)
        {
            if(tribe["category"] is JsonValue v)
            {
                if(v.TryGetValue<int>(out var n) && n>=0 && n<=2) return n;
                if(v.TryGetValue<string>(out var s))
                {
                    if(s.Equals("hidden",StringComparison.OrdinalIgnoreCase)) return 0;
                    if(s.Equals("human",StringComparison.OrdinalIgnoreCase)) return 1;
                    if(s.Equals("special",StringComparison.OrdinalIgnoreCase)) return 2;
                }
            }
            throw new RuleViolation("Unknown native tribe category; refusing an unaudited exclusion");
        }
        public static bool IsHuman(JsonObject tribe) => Category(tribe)==1;
        public static bool IsPlayable(JsonObject tribe) => Category(tribe)!=0;
        public static string ResolveOverride(JsonObject techs,JsonObject tribe,string id)
        {
            var overrides=tribe["techOverrides"] as JsonObject;
            var seen=new HashSet<string>(StringComparer.Ordinal);
            while(overrides is not null && overrides[id] is JsonNode target)
            {
                if(!seen.Add(id)) throw new RuleViolation("Cyclic native technology override");
                id=target.GetValue<string>();
            }
            if(techs[id] is not JsonObject) throw new RuleViolation("Missing effective technology: "+id);
            return id;
        }
        public static TreeMutation AddDeception(string input)
        {
            // All mutation is on a private tree. Do not change a player's starting
            // research or fabricate an unresearched grouping technology.
            var root=Object(JsonNode.Parse(input),"game logic");
            var techs=Object(root["techData"],"techData");
            var tribes=Object(root["tribeData"],"tribeData");
            if(Branch.Any(t=>techs.ContainsKey(t.Id)) || techs.Any(x=>x.Key.StartsWith(RootPrefix,StringComparison.Ordinal)))
                throw new RuleViolation("Deception IDs already exist; refusing a duplicate/conflicting install");
            var basic=Object(techs["basic"],"basic");
            if(basic["idx"]?.GetValue<int>()!=0 || basic["cost"]?.GetValue<int>()!=0)
                throw new RuleViolation("Native Basic root identity/cost differs from the pinned build");
            string[] original=Strings(basic["techUnlocks"],"native roots");
            if(original.Length!=5 || original.Distinct(StringComparer.Ordinal).Count()!=5)
                throw new RuleViolation("Expected five distinct original roots; inspect competing tree modification");
            // UI-A64: PolyMod lays root slots clockwise from the right in 60-degree steps.
            // Keep native order, inserting Deception at the left between Fishing and Hunting.
            int deceptionIndex=System.Array.IndexOf(original,"fishing")+1;
            if(deceptionIndex==0 || deceptionIndex>=original.Length || original[deceptionIndex]!="hunting")
                throw new RuleViolation("Expected adjacent native Fishing and Hunting roots");
            var audits=new List<TribeTreeAudit>();
            foreach(var pair in tribes.OrderBy(x=>x.Key,StringComparer.Ordinal))
            {
                var tribe=Object(pair.Value,pair.Key);int category=Category(tribe);
                if(category==0) continue; // Native hidden none/nature entries are not playable tribes.
                string rootId=ResolveOverride(techs,tribe,"basic");
                if(rootId!="basic") throw new RuleViolation("Native basic override requires separate audit: "+pair.Key);
                string[] starting=Strings(tribe["startingTech"],pair.Key+".startingTech");
                if(!starting.Contains("basic",StringComparer.Ordinal))
                    throw new RuleViolation("Native Basic root is not starting research for "+pair.Key);
                if(tribe["techOverrides"] is JsonObject map && Branch.Any(t=>map.ContainsKey(t.Id)))
                    throw new RuleViolation("Conflicting Deception override: "+pair.Key);
                string[] branches=original.Select(id=>ResolveOverride(techs,tribe,id)).ToArray();
                if(branches.Distinct(StringComparer.Ordinal).Count()!=5)
                    throw new RuleViolation("Expected five distinct effective branches: "+pair.Key);
                audits.Add(new(pair.Key,category==1?"Human":"Special",rootId,0,starting,
                    branches,branches.Take(deceptionIndex).Concat(new[]{DeceptionId}).Concat(branches.Skip(deceptionIndex)).ToArray()));
            }
            if(audits.Count==0) throw new RuleViolation("No playable tribes found in native registry");
            // A01 all-tribe scope lets us extend the existing shared root in place.
            // This retains idx=0, the prerequisite that native player state owns.
            // All tribe overrides, starting advantages, costs, units and native
            // branches are otherwise untouched. Native cost is the tier input;
            // native city-count and discount rules still determine the price.
            foreach(var node in Branch)
            {
                var definition=ContentDefinitions.CloneRow("techData",node.Id);
                // Unlock publication follows historical unit registration below.
                definition["unitUnlocks"]=new JsonArray();techs[node.Id]=definition;
            }
            Array(basic["techUnlocks"],"native roots").Insert(deceptionIndex,DeceptionId);
            return new(root.ToJsonString(),audits.Select(a=>a.Tribe).ToArray(),new[]{"basic"},original,audits.ToArray());
        }
        public static void ValidateReentry(string input)
        {
            // PolyMod can process its already-modified JSON again; never append a
            // second edge. Foreign/conflicting data is not silently accepted.
            var root=Object(JsonNode.Parse(input),"game logic");
            var techs=Object(root["techData"],"techData");
            foreach(var node in Branch)
            {
                var data=Object(techs[node.Id],node.Id);
                if(data["cost"]?.GetValue<int>()!=node.Tier ||
                    !Strings(data["techUnlocks"],node.Id+" children").SequenceEqual(Branch.Where(t=>t.Parent==node.Id).Select(t=>t.Id)) ||
                    Strings(data["unitUnlocks"],node.Id+" units").Length!=0)
                    throw new RuleViolation("Conflicting Deception branch node during GLD reload: "+node.Id);
            }
            var children=Array(techs["basic"]?["techUnlocks"],"native roots");
            if(children.Count!=6 || children.Count(x=>x?.GetValue<string>()==DeceptionId)!=1)
                throw new RuleViolation("Conflicting Deception root during GLD reload");
            for(int i=children.Count-1;i>=0;i--) if(children[i]?.GetValue<string>()==DeceptionId) children.RemoveAt(i);
            foreach(var node in Branch)techs.Remove(node.Id);
            AddDeception(root.ToJsonString()); // Validate unchanged native identities and all-tribe rules.
        }
        public static string Canonical(JsonNode? node)
        {
            if(node is JsonObject obj) return "{"+string.Join(",",obj.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>JsonSerializer.Serialize(p.Key)+":"+Canonical(p.Value)))+"}";
            if(node is JsonArray arr) return "["+string.Join(",",arr.Select(Canonical))+"]";
            return node?.ToJsonString() ?? "null";
        }
    }
}
