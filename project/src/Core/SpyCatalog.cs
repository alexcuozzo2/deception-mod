using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Deception.Core
{
    public static class SpyCatalog
    {
        public const string Id="dcp_spy";
        public const string AquarionId="dcp_spymaid";
        public static bool IsSpyId(string id)=>id==Id||id==AquarionId;
        public const int Cost=5,Health=50;
        static JsonObject Definition(JsonObject warrior,bool amphibious=false)
        {
            var unit=JsonNode.Parse(warrior.ToJsonString())!.AsObject();
            unit["idx"]=-1;unit["prefab"]=amphibious?"mermaidwarrior":"warrior";unit["cost"]=Cost;unit["health"]=Health;
            unit["attack"]=0;unit["defence"]=10;unit["range"]=0;unit["movement"]=2;unit["promotionLimit"]=0;
            unit["hidden"]=false;unit["independent"]=false;
            foreach(string key in new[]{"upgradesFrom","promotesTo","growthRate","attractsTo"})unit.Remove(key);
            unit["unitAbilities"]=amphibious?new JsonArray("scout","swim","amphibious"):new JsonArray("scout");
            return unit;
        }
        internal static void Add(JsonObject root)
        {
            var units=root["unitData"]!.AsObject();
            if(units.ContainsKey(Id)||units.ContainsKey(AquarionId))throw new RuleViolation("Spy already registered.");
            var unlocks=root["techData"]![TechTree.EspionageId]!["unitUnlocks"]!.AsArray();
            if(unlocks.Count!=0)throw new RuleViolation("Espionage has unexpected existing unlocks.");
            units[Id]=Definition(units["warrior"]!.AsObject());unlocks.Add(Id);
            units[AquarionId]=Definition(units["mermaidwarrior"]!.AsObject(),true);
        }
        internal static void RemoveValidated(JsonObject root)
        {
            var units=root["unitData"]!.AsObject();
            var actual=JsonNode.Parse(units[Id]?.ToJsonString()??throw new RuleViolation("Spy definition missing."))!.AsObject();
            actual["idx"]=-1;
            if(TechTree.Canonical(actual)!=TechTree.Canonical(Definition(units["warrior"]!.AsObject())))
                throw new RuleViolation("Spy definition changed during registry reentry.");
            var unlocks=root["techData"]![TechTree.EspionageId]!["unitUnlocks"]!.AsArray();
            if(unlocks.Count!=1||unlocks[0]?.GetValue<string>()!=Id)throw new RuleViolation("Spy research unlock changed.");
            var aquatic=JsonNode.Parse(units[AquarionId]?.ToJsonString()??throw new RuleViolation("Spymaid definition missing."))!.AsObject();aquatic["idx"]=-1;
            if(TechTree.Canonical(aquatic)!=TechTree.Canonical(Definition(units["mermaidwarrior"]!.AsObject(),true)))throw new RuleViolation("Spymaid definition changed during registry reentry.");
            units.Remove(Id);units.Remove(AquarionId);unlocks.Clear();
        }
    }
}
