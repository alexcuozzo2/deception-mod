using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Deception.Core
{
    // Command carriers only. They are hidden from native city/build menus and
    // are never placed as buildings. The Smuggle action adapter owns validation
    // and uses native resource/population subactions at commitment.
    public static class SmuggleCatalog
    {
        public const string Prefix="dcp_smuggle_";
        public const int Cost=2;
        public static readonly (string Id,string NativeHarvest,string Resource)[] Harvests={
            (Prefix+"fruit","harvestfruit","fruit"),
            (Prefix+"fish","fishing","fish"),
            (Prefix+"animal","hunting","game")
        };
        static JsonObject Clone(JsonObject value)=>JsonNode.Parse(value.ToJsonString())!.AsObject();
        static JsonObject Carrier(JsonObject source)
        {
            if(source["cost"]?.GetValue<int>()!=Cost)
                throw new RuleViolation("Pinned native harvest cost changed");
            var copy=Clone(source);
            copy["idx"]=-1;copy["hidden"]=true;copy["cost"]=Cost;
            copy["improvementAbilities"]=new JsonArray();
            copy["creates"]=new JsonArray();
            copy["growthRewards"]=new JsonArray();
            copy["growthRate"]=0;
            // Native resource lookup scans unlocked improvement terrain rules;
            // hidden alone does not exclude a command carrier from that scan.
            copy["terrainRequirements"]=new JsonArray();
            copy["shouldSuggestUnlock"]=false;
            return copy;
        }
        public static string Add(string input)
        {
            var root=JsonNode.Parse(input)!.AsObject();
            var improvements=root["improvementData"] as JsonObject
                ??throw new RuleViolation("Missing native improvement registry for Smuggle");
            if(improvements.Any(p=>p.Key.StartsWith(Prefix,StringComparison.Ordinal)))
                throw new RuleViolation("Smuggle command identifiers already exist");
            foreach(var h in Harvests)
            {
                var source=improvements[h.NativeHarvest] as JsonObject
                    ??throw new RuleViolation("Missing native harvest definition: "+h.NativeHarvest);
                if(source["cost"]?.GetValue<int>()!=Cost)
                    throw new RuleViolation("Pinned harvest cost changed: "+h.NativeHarvest);
                improvements[h.Id]=Carrier(source);
            }
            return root.ToJsonString();
        }
        public static void ValidateReentry(string input)
        {
            var root=JsonNode.Parse(input)!.AsObject();
            var improvements=root["improvementData"]!.AsObject();
            string[] names=improvements.Select(p=>p.Key).Where(p=>p.StartsWith(Prefix,StringComparison.Ordinal)).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            if(!names.SequenceEqual(Harvests.Select(h=>h.Id).OrderBy(p=>p,StringComparer.Ordinal)))
                throw new RuleViolation("Conflicting Smuggle command registry");
            foreach(var h in Harvests)
            {
                var source=improvements[h.NativeHarvest] as JsonObject
                    ??throw new RuleViolation("Missing native harvest definition: "+h.NativeHarvest);
                var actual=Clone(improvements[h.Id]!.AsObject());actual["idx"]=-1;
                if(TechTree.Canonical(actual)!=TechTree.Canonical(Carrier(source)))
                    throw new RuleViolation("Smuggle command metadata changed: "+h.Id);
            }
        }
    }
}
