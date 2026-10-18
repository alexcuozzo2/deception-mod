using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Deception.Core;
using Deception.PolyModAdapter;

namespace Deception.Tests
{
    // Retained recipe name: A60 removes A59's purchasable native egg. These
    // regressions exercise the current complete registry composition instead.
    public static class ElyrionEggTests
    {
        static int Checks;
        static readonly List<string> Cases=new();
        static string Captured="";
        static void Check(bool ok,string why){Checks++;if(!ok)throw new Exception(why);}
        static void Reject(Action action,string why)
        {try{action();}catch(RuleViolation){Checks++;return;}throw new Exception("Accepted "+why);}
        static void Test(string name,Action action){action();Cases.Add(name);}
        static JsonObject Parse(string input)=>JsonNode.Parse(input)!.AsObject();
        static string[] Strings(JsonNode? value)=>value is JsonArray a?a.Select(x=>x!.GetValue<string>()).ToArray():Array.Empty<string>();
        static string Compose(string input)
        {
            var tree=TechTree.AddDeception(input);var recruitment=RecruitmentCatalog.Add(tree.Json);
            return NativeExchangeDiagnosticResearch.Add(NativeExchangeCatalog.Add(SmuggleCatalog.Add(recruitment.Json)));
        }
        static void Validate(string input)
        {
            RecruitmentCatalog.ValidateReentry(input);SmuggleCatalog.ValidateReentry(input);
            NativeExchangeCatalog.ValidateReentry(input);NativeExchangeDiagnosticResearch.ValidateReentry(input);
            NativeRecruitmentRosterReentry.Validate(input,NativeRecruitmentRosters.AllResearchRosters(Captured));
        }
        static string[] Research(JsonObject root,JsonObject tribe,bool all)
        {
            var tech=root["techData"]!.AsObject();var queue=new Queue<string>();
            foreach(var start in Strings(tribe["startingTech"]))queue.Enqueue(start);
            var visited=new HashSet<string>(StringComparer.Ordinal);
            while(queue.Count!=0)
            {
                var id=TechTree.ResolveOverride(tech,tribe,queue.Dequeue());
                if(!visited.Add(id))continue;
                if(all)foreach(var child in Strings(tech[id]!["techUnlocks"]))queue.Enqueue(child);
            }
            return visited.OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        }
        static bool EggUnlocked(JsonObject root,JsonObject tribe,bool all)=>
            Research(root,tribe,all).Any(id=>Strings(root["techData"]![id]!["unitUnlocks"]).Contains("dragonegg"));
        public static int Run()
        {
            try
            {
                Checks=0;Cases.Clear();
                string capturedPath=Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE")
                    ??throw new Exception("Missing captured native tree fixture");
                var captured=Parse(File.ReadAllText(capturedPath));
                captured["improvementData"]=JsonNode.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(capturedPath)!,
                    "elyrion-eggs-0396","native-improvements.captured.json")));
                Captured=captured.ToJsonString();var final=Compose(Captured);var registry=Parse(final);
                Test("native_egg_remains_hidden_cost_ten_and_exact_native_definition",()=>
                {
                    var egg=registry["unitData"]!["dragonegg"]!;
                    Check(egg["cost"]!.GetValue<int>()==10&&egg["hidden"]!.GetValue<bool>(),"removed egg purchase must restore native cost and visibility");
                    Check(TechTree.Canonical(egg)==TechTree.Canonical(captured["unitData"]!["dragonegg"]),"native egg definition changed");
                    Check(egg["idx"]!.GetValue<int>()==17&&egg["growthRate"]!.GetValue<int>()==3,"native identity or growth timer changed");
                });
                Test("forest_magic_has_no_added_egg_unlock_or_other_mutation",()=>
                {
                    Check(TechTree.Canonical(registry["techData"]!["forestmagic"])==TechTree.Canonical(captured["techData"]!["forestmagic"]),"Forest Magic mutated");
                    Check(!registry["techData"]!.AsObject().Any(t=>Strings(t.Value?["unitUnlocks"]).Contains("dragonegg")),"genuine egg appears in a recruitment unlock");
                });
                Test("all_sixteen_tribes_cannot_recruit_eggs_from_start_or_full_research",()=>
                {
                    int count=0;
                    foreach(var tribe in registry["tribeData"]!.AsObject().Where(p=>TechTree.IsPlayable(p.Value!.AsObject())))
                    {
                        count++;Check(!EggUnlocked(registry,tribe.Value!.AsObject(),false),tribe.Key+" starting egg recruitment");
                        Check(!EggUnlocked(registry,tribe.Value!.AsObject(),true),tribe.Key+" full research egg recruitment");
                    }
                    Check(count==16,"all playable native tribes covered");
                });
                Test("elyrion_starting_research_skins_and_city_reward_override_unchanged",()=>
                {
                    var before=captured["tribeData"]!["elyrion"]!;var after=registry["tribeData"]!["elyrion"]!;
                    Check(TechTree.Canonical(before)==TechTree.Canonical(after),"Elyrion tribe/skin/starting research changed");
                    Check(after["unitOverrides"]!["giant"]!.GetValue<string>()=="dragonegg","native super reward identity changed");
                    Check(Strings(after["startingTech"]).Contains("forestmagic")&&Strings(after["skins"]).Length>0,"native starting gate or skins absent");
                });
                Test("native_egg_dragon_and_super_unit_bodies_remain_unchanged",()=>
                {
                    foreach(string id in new[]{"dragonegg","babydragon","firedragon","giant"})
                        Check(TechTree.Canonical(captured["unitData"]![id])==TechTree.Canonical(registry["unitData"]![id]),"changed native growth/reward type "+id);
                    foreach(var item in captured["improvementData"]!.AsObject())
                        Check(TechTree.Canonical(item.Value)==TechTree.Canonical(registry["improvementData"]![item.Key]),"changed native improvement/reward definition "+item.Key);
                });
                foreach(string id in new[]{FoggerCatalog.Id,SpyCatalog.Id,SpyCatalog.AquarionId})Test("five_star_genuine_recruit_"+id,()=>
                {
                    var unit=registry["unitData"]![id]!;bool fogger=id==FoggerCatalog.Id,aquatic=id==SpyCatalog.AquarionId;
                    Check(unit["cost"]!.GetValue<int>()==5,"genuine recruitment cost must be exactly five");
                    Check(unit["health"]!.GetValue<int>()==(fogger?100:50)&&unit["defence"]!.GetValue<int>()==10&&unit["attack"]!.GetValue<int>()==0,"reprice changed genuine health/defence/attack");
                    Check(unit["movement"]!.GetValue<int>()==2&&unit["range"]!.GetValue<int>()==(fogger?2:0),"genuine movement/range differs from current balance");
                    Check(!unit["hidden"]!.GetValue<bool>()&&!unit["independent"]!.GetValue<bool>()&&unit["promotionLimit"]!.GetValue<int>()==0,"reprice changed recruitment/body flags");
                    var skills=fogger?new[]{"fly","dash","scout","doubleattack"}:aquatic?new[]{"scout","swim","amphibious"}:new[]{"scout"};
                    Check(Strings(unit["unitAbilities"]).SequenceEqual(skills),"reprice changed genuine abilities");
                });
                Test("five_star_recruits_keep_research_and_aquarion_override",()=>
                {
                    Check(Strings(registry["techData"]![TechTree.CounterintelligenceId]!["unitUnlocks"]).SequenceEqual(new[]{FoggerCatalog.Id}),"Fogger research gate changed");
                    Check(Strings(registry["techData"]![TechTree.EspionageId]!["unitUnlocks"]).SequenceEqual(new[]{SpyCatalog.Id}),"Spy research gate changed");
                    Check(registry["tribeData"]!["aquarion"]!["unitOverrides"]![SpyCatalog.Id]!.GetValue<string>()==SpyCatalog.AquarionId,"Spymaid native override changed");
                });
                Test("all_mimic_carriers_keep_price_health_and_zero_damage",()=>
                {
                    foreach(string source in RecruitmentCatalog.CarrierForms)
                    {
                        var unit=registry["unitData"]![RecruitmentCatalog.Variant(source)]!;
                        int cost=RecruitmentCatalog.NavalCopyOnly.Contains(source,StringComparer.Ordinal)?0:1;
                        Check(unit["cost"]!.GetValue<int>()==cost&&unit["health"]!.GetValue<int>()==10&&unit["attack"]!.GetValue<int>()==0&&unit["defence"]!.GetValue<int>()==0&&unit["range"]!.GetValue<int>()==0,"Mimic must cost one star, retain one HP and have no offense: "+source);
                    }
                    foreach(string id in new[]{RecruitmentCatalog.MenuId,AquarionCatalog.MimicId})
                        Check(registry["unitData"]![id]!["cost"]!.GetValue<int>()==1&&registry["unitData"]![id]!["health"]!.GetValue<int>()==10,"Mimic menu body changed");
                });
                Test("egg_mimic_remains_existing_field_copy_only_form",()=>
                {
                    Check(RecruitmentCatalog.FieldCopyOnly.Contains("dragonegg")&&!RecruitmentCatalog.Supported.Contains("dragonegg"),"egg entered Mimic city-recruitment policy");
                    var egg=registry["unitData"]!["dcp_mimic_form_dragonegg"]!;
                    Check(egg["prefab"]!.GetValue<string>()=="dragonegg"&&egg["growthRate"] is null,"field-copy identity or apparent-growth policy changed");
                    Check(egg["cost"]!.GetValue<int>()==1&&egg["health"]!.GetValue<int>()==10,"native egg removal changed its Mimic carrier");
                });
                Test("complete_production_catalog_order_and_strict_roster_reentry",()=>
                {
                    var before=final;Validate(final);Validate(final);
                    Check(final==before,"reentry mutated the caller registry");
                    var rosters=NativeRecruitmentRosters.AllResearchRosters(Captured);
                    Check(rosters.Count==16&&rosters.Values.All(r=>!r.Supported.Contains("dragonegg")),"Mimic native appearance rosters changed");
                    Check(TechTree.Canonical(Parse(Compose(Captured)))==TechTree.Canonical(registry),"repeat native composition is nondeterministic");
                });
                foreach(string id in new[]{FoggerCatalog.Id,SpyCatalog.Id,SpyCatalog.AquarionId})
                    foreach(string field in new[]{"cost","health","unitAbilities"})Test("reentry_rejects_changed_"+id+"_"+field,()=>
                    {
                        var changed=Parse(final);
                        changed["unitData"]![id]![field]=field=="unitAbilities"?new JsonArray("infiltrate"):JsonValue.Create(field=="cost"?8:999);
                        Reject(()=>Validate(changed.ToJsonString()),id+" "+field);
                    });
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases.Count,assertions=Checks,tests=Cases}));return 0;
            }
            catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases.Count,assertions=Checks,tests=Cases,error=e.ToString()}));return 1;}
        }
    }
}
