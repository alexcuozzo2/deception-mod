using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Deception.Core
{
    // Inert native Build identities, never buildings or ordinary harvest offers.
    // No research/unlock/button is added until authoritative exchanges are ready.
    public static class NativeExchangeCatalog
    {
        public const string Prefix="dcp_exchange_",Regroup=Prefix+"regroup",Disguise=Prefix+"disguise";
        public static bool IsSingle(NativeExchangeKind kind)=>IsUpgrade(kind)||kind==NativeExchangeKind.ShedDisguise||IsSupport(kind);
        public static bool IsSupport(NativeExchangeKind kind)=>kind is NativeExchangeKind.FeignHeal or NativeExchangeKind.FeignBoost or NativeExchangeKind.FeignSwarm or NativeExchangeKind.FeignRecover;
        public static bool IsUpgrade(NativeExchangeKind kind)=>kind is NativeExchangeKind.UpgradeScout or NativeExchangeKind.UpgradeRammer or NativeExchangeKind.UpgradeBomber;
        public static string UpgradeTarget(NativeExchangeKind kind)=>kind switch {NativeExchangeKind.UpgradeScout=>"scoutship",NativeExchangeKind.UpgradeRammer=>"rammership",NativeExchangeKind.UpgradeBomber=>"bombership",_=>throw new RuleViolation("Not an apparent upgrade")};
        public static string Id(NativeExchangeKind kind)=>kind switch {NativeExchangeKind.ShedDisguise=>Prefix+"shed_disguise",NativeExchangeKind.Regroup=>Regroup,NativeExchangeKind.Disguise=>Disguise,
            NativeExchangeKind.Subvert=>Prefix+"subvert",NativeExchangeKind.Blackout=>Prefix+"blackout",
            NativeExchangeKind.Blackmail=>Prefix+"blackmail",
            NativeExchangeKind.LighthouseSecret=>Prefix+"lighthouse_secret",
            NativeExchangeKind.FeignHeal=>Prefix+"feign_heal",NativeExchangeKind.FeignBoost=>Prefix+"feign_boost",
            NativeExchangeKind.FeignSwarm=>Prefix+"feign_swarm",NativeExchangeKind.FeignRecover=>Prefix+"feign_recover",
            NativeExchangeKind.UpgradeScout=>Prefix+"upgrade_scout",NativeExchangeKind.UpgradeRammer=>Prefix+"upgrade_rammer",NativeExchangeKind.UpgradeBomber=>Prefix+"upgrade_bomber",_=>throw new RuleViolation("Unknown exchange kind")};
        static JsonObject Carrier(JsonObject source)
        {
            var copy=JsonNode.Parse(source.ToJsonString())!.AsObject();copy["idx"]=-1;copy["hidden"]=true;copy["cost"]=0;
            copy["improvementAbilities"]=new JsonArray();copy["creates"]=new JsonArray();copy["growthRewards"]=new JsonArray();
            copy["growthRate"]=0;copy["terrainRequirements"]=new JsonArray();copy["shouldSuggestUnlock"]=false;return copy;
        }
        public static string Add(string input)
        {
            var root=JsonNode.Parse(input)!.AsObject();var registry=root["improvementData"]!.AsObject();
            if(registry.Any(x=>x.Key.StartsWith(Prefix,StringComparison.Ordinal)))throw new RuleViolation("Exchange carrier IDs already exist");
            var source=registry["harvestfruit"]!.AsObject();foreach(var kind in Enum.GetValues<NativeExchangeKind>())registry[Id(kind)]=Carrier(source);return root.ToJsonString();
        }
        public static void ValidateReentry(string input)
        {
            var registry=JsonNode.Parse(input)!["improvementData"]!.AsObject();
            if(!registry.Select(x=>x.Key).Where(x=>x.StartsWith(Prefix,StringComparison.Ordinal)).OrderBy(x=>x,StringComparer.Ordinal)
                .SequenceEqual(Enum.GetValues<NativeExchangeKind>().Select(Id).OrderBy(x=>x,StringComparer.Ordinal)))throw new RuleViolation("Conflicting exchange carrier registry");
            foreach(string id in Enum.GetValues<NativeExchangeKind>().Select(Id))
            {
                var actual=JsonNode.Parse(registry[id]!.ToJsonString())!.AsObject();actual["idx"]=-1;
                if(TechTree.Canonical(actual)!=TechTree.Canonical(Carrier(registry["harvestfruit"]!.AsObject())))throw new RuleViolation("Exchange carrier metadata changed");
            }
        }
    }
}
