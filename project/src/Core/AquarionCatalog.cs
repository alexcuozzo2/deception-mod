using System.Text.Json.Nodes;

namespace Deception.Core
{
    public static class AquarionCatalog
    {
        public const string MimicId="dcp_mermimic";
        internal static void AddOverrides(JsonObject root)
        {
            var map=root["tribeData"]!["aquarion"]!["unitOverrides"]!.AsObject();
            if(map.ContainsKey(RecruitmentCatalog.MenuId)||map.ContainsKey(SpyCatalog.Id))throw new RuleViolation("Aquarion custom overrides already present.");
            map[RecruitmentCatalog.MenuId]=MimicId;map[SpyCatalog.Id]=SpyCatalog.AquarionId;
        }
        internal static void RemoveValidatedOverrides(JsonObject root)
        {
            var map=root["tribeData"]!["aquarion"]!["unitOverrides"]!.AsObject();
            if(map[RecruitmentCatalog.MenuId]?.GetValue<string>()!=MimicId||map[SpyCatalog.Id]?.GetValue<string>()!=SpyCatalog.AquarionId)
                throw new RuleViolation("Aquarion custom overrides changed.");
            map.Remove(RecruitmentCatalog.MenuId);map.Remove(SpyCatalog.Id);
        }
    }
}
