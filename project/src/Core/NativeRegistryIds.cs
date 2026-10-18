using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Deception.Core
{
    public sealed record NativeRegistryId(string Section,string EnumType,string Id,int Index,bool Required);

    // Explicit save-compatible IDs. Baseline observed in PolyMod's own0310
    // Created mapping log; full provenance: evidence/observed-native-registry-0310.json.
    // Never derive existing IDs from current insertion order or the latest GLD.
    public static class NativeRegistryIds
    {
        static readonly NativeRegistryId[] Table={
            new("techData","Polytopia.Data.TechData+Type","dcp_deception",1000,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_amphibian",1001,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_archer",1002,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_battlesled",1003,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_catapult",1004,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_ciru",1005,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_defender",1006,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_doomux",1007,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_exida",1008,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_hexapod",1009,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_icearcher",1010,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_icefortress",1011,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_kiton",1012,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_knight",1013,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_mantis",1014,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_mermaidarcher",1015,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_mermaiddefender",1016,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_mermaidswordsman",1017,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_mermaidwarrior",1018,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_mindbender",1019,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_phychi",1020,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_rider",1021,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_shaman",1022,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_siren",1023,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_swordsman",1024,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_tridention",1025,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_warrior",1026,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic",1027,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_smuggle_fruit",1028,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_smuggle_fish",1029,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_smuggle_animal",1030,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_regroup",1031,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_disguise",1032,true),
            new("techData","Polytopia.Data.TechData+Type","dcp_misdirection",1033,false),
            new("techData","Polytopia.Data.TechData+Type","dcp_camouflage",1034,true),
            // A11 adds these after all previously observed identities. The old
            // diagnostic Misdirection1033 stays reserved; never reuse its value.
            new("techData","Polytopia.Data.TechData+Type","dcp_propaganda",1035,true),
            new("techData","Polytopia.Data.TechData+Type","dcp_intelligence",1036,true),
            new("techData","Polytopia.Data.TechData+Type","dcp_espionage",1037,true),
            // A14 field-only copies append after every previously saved ID.
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_boomchi",1038,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_bunny",1039,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_centipede",1040,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_dagger",1041,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_firedragon",1042,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_giant",1043,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_mermaiddagger",1044,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_polytaur",1045,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_babydragon",1046,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_dragonegg",1047,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_transportship",1048,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_scoutship",1049,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_rammership",1050,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_bombership",1051,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_juggernaut",1052,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_pirate",1053,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_cloak_boat",1054,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_cloak",1055,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_mermaidcloak",1056,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_upgrade_scout",1057,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_upgrade_rammer",1058,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_upgrade_bomber",1059,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_raychi",1060,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_jelly",1061,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_shark",1062,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_aquapult",1063,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_island",1064,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_spy",1065,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_dcp_spy",1066,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_shed_disguise",1067,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_subvert",1068,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_blackout",1069,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_feign_heal",1070,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_feign_boost",1071,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_feign_swarm",1072,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_feign_recover",1073,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_mooni",1074,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_moth",1075,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_crab",1076,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_gaami",1077,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_bugegg",1078,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_larva",1079,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_blackmail",1080,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mermimic",1081,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_spymaid",1082,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_dcp_spymaid",1083,true),
            new("unitData","Polytopia.Data.UnitData+Type",FoggerCatalog.Id,1084,true),
            new("unitData","Polytopia.Data.UnitData+Type","dcp_mimic_form_dcp_motherfogger",1085,true),
            new("improvementData","Polytopia.Data.ImprovementData+Type","dcp_exchange_lighthouse_secret",1086,true),

        };
        public static IReadOnlyList<NativeRegistryId> Entries {get;}=Array.AsReadOnly(Table);
        // The loader shares one automatic counter across enum categories.
        // All newer entries follow the historical unit/improvement batches.
        // The loader adapter maps each contiguous section batch exactly once.
        public static IReadOnlyList<NativeRegistryId> AllocationTail(string json)
        {
            _=Pin(json);var root=JsonNode.Parse(json)!;
            var tail=Table.Where(x=>x.Index>=1033).OrderBy(x=>x.Index).ToArray();
            for(int i=0;i<tail.Length;i++)
            {
                var e=tail[i];
                if((e.Section!="techData"&&e.Section!="unitData"&&e.Section!="improvementData")||e.Index!=1033+i||root[e.Section]?[e.Id] is not JsonObject node||node["idx"]?.GetValue<int>()!=-1)
                    throw new RuleViolation("Missing, preallocated or discontinuous tail identity: "+e.Id);
            }
            return Array.AsReadOnly(tail);
        }
        public static string Pin(string json)
        {
            var root=JsonNode.Parse(json) as JsonObject??throw new RuleViolation("Missing native registry root.");
            foreach(var section in Table.Select(x=>x.Section).Distinct(StringComparer.Ordinal))
            {
                var registry=root[section] as JsonObject??throw new RuleViolation("Missing native registry section: "+section);
                var known=Table.Where(x=>x.Section==section).ToDictionary(x=>x.Id,StringComparer.Ordinal);
                var reserved=known.Values.Select(x=>x.Index).ToHashSet();
                foreach(var pair in registry)
                {
                    var node=pair.Value as JsonObject??throw new RuleViolation("Invalid native registry entry: "+pair.Key);
                    if(node["idx"] is not JsonValue value||!value.TryGetValue<int>(out int index))
                        throw new RuleViolation("Missing numeric registry ID: "+pair.Key);
                    if(known.TryGetValue(pair.Key,out var item))
                    {
                        if(index!=-1&&index!=item.Index)throw new RuleViolation("Existing Deception registry ID changed: "+pair.Key);
                        node["idx"]=item.Index;
                    }
                    else
                    {
                        if(pair.Key.StartsWith("dcp_",StringComparison.Ordinal))throw new RuleViolation("Unassigned Deception registry ID: "+pair.Key);
                        if(index<0||reserved.Contains(index))throw new RuleViolation("Foreign registry allocation conflicts with Deception: "+pair.Key);
                    }
                }
                foreach(var item in known.Values.Where(x=>x.Required))
                    if(!registry.ContainsKey(item.Id))throw new RuleViolation("Missing historical Deception registry identity: "+item.Id);
            }
            return root.ToJsonString();
        }
        public static void Validate(string json)
        {
            if(TechTree.Canonical(JsonNode.Parse(Pin(json)))!=TechTree.Canonical(JsonNode.Parse(json)))
                throw new RuleViolation("Deception registry IDs have not been pinned.");
        }
    }
}
