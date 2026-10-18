using System;
using System.Text.Json.Nodes;

namespace Deception.Core
{
    public static class FoggerCatalog
    {
        public const string Id="dcp_motherfogger";
        public const int Cost=5,Health=100;
        public static bool IsFoggerId(string id)=>id==Id;
        static JsonObject Definition(JsonObject warrior)
        {
            var unit=ContentDefinitions.UnitFromNative(warrior,Id,false);
            return unit;
        }
        internal static void Add(JsonObject root)
        {
            var units=root["unitData"]!.AsObject();
            if(units.ContainsKey(Id))throw new RuleViolation("Fogger already registered.");
            var unlocks=root["techData"]![TechTree.CounterintelligenceId]!["unitUnlocks"]!.AsArray();
            if(unlocks.Count!=0)throw new RuleViolation("Counterintelligence has unexpected unlocks.");
            units[Id]=Definition(units["warrior"]!.AsObject());unlocks.Add(Id);
        }
        internal static void RemoveValidated(JsonObject root)
        {
            var units=root["unitData"]!.AsObject();
            var actual=JsonNode.Parse(units[Id]?.ToJsonString()??throw new RuleViolation("Fogger missing."))!.AsObject();actual["idx"]=-1;
            if(TechTree.Canonical(actual)!=TechTree.Canonical(Definition(units["warrior"]!.AsObject())))
                throw new RuleViolation("Fogger definition changed during registry reentry.");
            var unlocks=root["techData"]![TechTree.CounterintelligenceId]!["unitUnlocks"]!.AsArray();
            if(unlocks.Count!=1||unlocks[0]?.GetValue<string>()!=Id)throw new RuleViolation("Fogger research unlock changed.");
            units.Remove(Id);unlocks.Clear();
        }
    }
}
