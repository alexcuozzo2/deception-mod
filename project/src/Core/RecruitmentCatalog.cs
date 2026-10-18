// Native recruitment checkpoint only. This is NOT the complete apparent-state adapter.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Deception.Core
{
    public sealed record RecruitmentForm(string NativeId,string VariantId,string Label,int ApparentHp,int Movement,
        int ApparentAttack,int ApparentDefense,bool NativeWaterTravel);
    public sealed record RecruitmentMutation(string Json,RecruitmentForm[] Forms,string[] DeferredForms);
    public static class RecruitmentCatalog
    {
        public const string MenuId="dcp_mimic";
        public const string Prefix="dcp_mimic_form_";
        public const int Cost=1;
        public const int NativeHealth=10; // This pinned registry uses 10 integer health units per displayed HP.
        public static readonly string[] Supported={
            "warrior","rider","knight","defender","archer","catapult","mindbender","swordsman",
            "amphibian","tridention","battlesled","icefortress","icearcher","hexapod","doomux","phychi",
            "kiton","exida","shaman","mermaidwarrior","mermaidarcher","mermaidswordsman","mermaiddefender",
            "siren","ciru","mantis","cloak","mermaidcloak","raychi","jelly","shark","aquapult","island","mooni","moth",SpyCatalog.Id,SpyCatalog.AquarionId,FoggerCatalog.Id};
        // Field copying is broader than city recruitment. These independent
        // bodies use explicit appearance adapters; dragon timers live in the ledger. A native
        // Centipede still must pass the separate solo-head graph check.
        public static readonly string[] FieldCopyOnly={
            "giant","bunny","polytaur","firedragon","dagger","mermaiddagger","boomchi","centipede","dragonegg","babydragon","crab","gaami","bugegg","larva"};
        public static readonly string[] NavalCopyOnly={"transportship","scoutship","rammership","bombership","juggernaut","pirate","cloak_boat"};
        public static IEnumerable<string> CarrierForms=>Supported.Concat(FieldCopyOnly).Concat(NavalCopyOnly);
        // These forms need native passive/visibility/naval adapters. Their omission is an OPEN coverage gap,
        // not a change to the all-tribe / city-origin release specification.
        public static readonly string[] Deferred=Array.Empty<string>();
        static readonly HashSet<string> SafeAbilities=new(StringComparer.Ordinal){
            "escape","fortify","stiff","static","skate","sneak","creep","swim","fly","land","amphibious","carry","water"};
        public static bool IsMenuId(string id)=>id==MenuId||id==AquarionCatalog.MimicId;
        public static string Variant(string source)=>IsMenuId(source)?source:Prefix+source;
        public static bool IsMimicId(string id)=>IsMenuId(id) || id.StartsWith(Prefix,StringComparison.Ordinal);
        public static string EscapeTerm(string prefix,string id)=>prefix+id.Replace("_","__",StringComparison.Ordinal);
        public static string NativeTerm(string archiveKey)=>archiveKey.Replace("_",".",StringComparison.Ordinal).Replace("..","_",StringComparison.Ordinal);
        public static string Label(string id)=>id switch {
            MenuId=>"Mimic",AquarionCatalog.MimicId=>"Mermimic",SpyCatalog.AquarionId=>"Spymaid",SpyCatalog.Id=>"Spy",FoggerCatalog.Id=>"Fogger","jelly"=>"Jelly","island"=>"Living Island","mermaidcloak"=>"Mermaid Cloak","mermaidwarrior"=>"Mermaid Warrior","mermaidarcher"=>"Mermaid Archer","mermaidswordsman"=>"Mermaid Swordsman",
            "transportship"=>"Raft","scoutship"=>"Scout","rammership"=>"Rammer","bombership"=>"Bomber","cloak_boat"=>"Dinghy",
            "dragonegg"=>"Dragon Egg","babydragon"=>"Baby Dragon","firedragon"=>"Fire Dragon",
            "mermaiddefender"=>"Mermaid Defender","mindbender"=>"Mind Bender","icefortress"=>"Ice Fortress","icearcher"=>"Ice Archer",
            _=>CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id)};
        static JsonObject Clone(JsonObject o)=>JsonNode.Parse(o.ToJsonString())!.AsObject();
        static int Int(JsonObject o,string name)=>o[name]?.GetValue<int>() ?? throw new RuleViolation("Missing unit field "+name);
        static string[] Strings(JsonNode? n)=>n is JsonArray a?a.Select(x=>x!.GetValue<string>()).ToArray():System.Array.Empty<string>();
        static JsonObject Prototype(JsonObject source,string native,bool hidden)
        {
            var u=Clone(source);
            u["idx"]=-1;u["prefab"]=native;u["cost"]=NavalCopyOnly.Contains(native,StringComparer.Ordinal)?0:Cost;u["health"]=NativeHealth;
            u["attack"]=0;u["defence"]=0;u["range"]=0;u["promotionLimit"]=0;u["independent"]=false;u["hidden"]=hidden;
            u.Remove("upgradesFrom");u.Remove("promotesTo");u.Remove("growthRate");
            u.Remove("embarksTo");u.Remove("attractsTo");
            // A50: Dash belongs to every Mimic, independently of its disguise.
            // Disguises still grant no Scout sight or map-changing passives. Public appearance
            // information still reads the original source definition.
            u["unitAbilities"]=new JsonArray(Strings(source["unitAbilities"]).Where(SafeAbilities.Contains).Append("dash").Distinct(StringComparer.Ordinal).Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray());
            return u;
        }
        public static RecruitmentMutation Add(string treeJson)
        {
            var root=JsonNode.Parse(treeJson)!.AsObject();var units=root["unitData"]!.AsObject();
            if(units.Any(p=>IsMimicId(p.Key)))throw new RuleViolation("Mimic identifiers already present");
            if(units["warrior"] is not JsonObject warrior || Int(warrior,"health")!=100)
                throw new RuleViolation("Pinned health scale / Warrior definition not present");
            SpyCatalog.Add(root);FoggerCatalog.Add(root);
            var forms=new List<RecruitmentForm>();
            foreach(var id in CarrierForms.OrderBy(s=>s,StringComparer.Ordinal))
            {
                var u=units[id] as JsonObject ?? throw new RuleViolation("Missing reviewed source form: "+id);
                if(!FieldCopyOnly.Contains(id,StringComparer.Ordinal) && !NavalCopyOnly.Contains(id,StringComparer.Ordinal) && (u["hidden"]?.GetValue<bool>()==true || u["independent"]?.GetValue<bool>()==true))
                    throw new RuleViolation("Reviewed form is hidden/independent: "+id);
                if(Int(u,"health")%10!=0)throw new RuleViolation("Unexpected native health scale: "+id);
                var skills=Strings(u["unitAbilities"]);
                units[Variant(id)]=Prototype(u,id,false);
                forms.Add(new(id,Variant(id),Label(id),Int(u,"health")/10,Int(u,"movement"),
                    Int(u,"attack"),Int(u,"defence"),skills.Contains("swim")||skills.Contains("fly")));
            }
            units[MenuId]=Prototype(warrior,"warrior",false);
            units[AquarionCatalog.MimicId]=Prototype(units["mermaidwarrior"]!.AsObject(),"mermaidwarrior",false);
            AquarionCatalog.AddOverrides(root);
            var tech=root["techData"]![TechTree.DeceptionId]!.AsObject();
            if(tech["unitUnlocks"]!.AsArray().Count!=0)throw new RuleViolation("Unexpected existing Deception unlocks");
            // Registration and permission are separate: the native validator must be able to
            // find every concrete type under Deception. Hooks then enforce native-roster
            // membership and hide concrete variants from the ordinary city/AI menu.
            tech["unitUnlocks"]=new JsonArray(new[]{MenuId}.Concat(forms.Select(f=>f.VariantId))
                .Select(s=>(JsonNode?)JsonValue.Create(s)).ToArray());
            return new(root.ToJsonString(),forms.ToArray(),Deferred.ToArray());
        }
        public static void ValidateReentry(string json)
        {
            var root=JsonNode.Parse(json)!.AsObject();var units=root["unitData"]!.AsObject();
            var unlocks=Strings(root["techData"]![TechTree.DeceptionId]!["unitUnlocks"]);
            if(!unlocks.SequenceEqual(new[]{MenuId}.Concat(CarrierForms.OrderBy(s=>s,StringComparer.Ordinal).Select(Variant))))throw new RuleViolation("Recruitment unlocks changed");
            foreach(var id in CarrierForms.Select(Variant).Append(MenuId).Append(AquarionCatalog.MimicId))
            {
                var u=units[id] as JsonObject ?? throw new RuleViolation("Missing prototype "+id);
                if(Int(u,"health")!=NativeHealth || Int(u,"attack")!=0 || Int(u,"defence")!=0 || Int(u,"cost")!=(NavalCopyOnly.Any(n=>Variant(n)==id)?0:Cost) || Int(u,"range")!=0)
                    throw new RuleViolation("Prototype invariants changed: "+id);
            }
            SpyCatalog.RemoveValidated(root);FoggerCatalog.RemoveValidated(root);
            AquarionCatalog.RemoveValidatedOverrides(root);
            foreach(var id in units.Select(x=>x.Key).Where(IsMimicId).ToArray())units.Remove(id);
            root["techData"]![TechTree.DeceptionId]!["unitUnlocks"]=new JsonArray();
            TechTree.ValidateReentry(root.ToJsonString());
            // Reconstruct expected prototypes from unchanged source definitions; indices may have been assigned by PolyMod.
            var expected=JsonNode.Parse(Add(root.ToJsonString()).Json)!;
            var actual=JsonNode.Parse(json)!;
            foreach(var id in CarrierForms.Select(Variant).Append(MenuId).Append(AquarionCatalog.MimicId))
            {
                var a=Clone(actual["unitData"]![id]!.AsObject());var b=Clone(expected["unitData"]![id]!.AsObject());
                a["idx"]=-1;b["idx"]=-1;
                if(TechTree.Canonical(a)!=TechTree.Canonical(b))throw new RuleViolation("Prototype definition changed: "+id);
            }
        }
    }
    public static class ResearchFeedbackPolicy
    {
        public static bool NeedsFallback(bool custom,bool local,bool owned,bool originalSucceeded,bool heardNativeCue,bool alreadyCompleted)
            =>custom&&local&&owned&&originalSucceeded&&!heardNativeCue&&!alreadyCompleted;
    }
}
