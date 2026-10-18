// A11: captured native research graph. Membership is separate from native city/action authorization.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Deception.Core
{
    public sealed record NativeRosterExclusion(string Form,string Reason);
    public sealed class NativeAppearanceRoster
    {
        public string Tribe { get; }
        public IReadOnlyList<string> Supported { get; }
        public IReadOnlyList<string> Deferred { get; }
        public IReadOnlyList<string> EffectiveTechnologies { get; }
        public IReadOnlyList<string> RequiresUniqueCheck { get; }
        public IReadOnlyList<NativeRosterExclusion> Excluded { get; }
        internal NativeAppearanceRoster(string tribe,IEnumerable<string> supported,IEnumerable<string> deferred,
            IEnumerable<string> tech,IEnumerable<string> unique,IEnumerable<NativeRosterExclusion> excluded)
        {
            Tribe=tribe;Supported=Freeze(supported);Deferred=Freeze(deferred);
            EffectiveTechnologies=Freeze(tech);RequiresUniqueCheck=Freeze(unique);
            Excluded=Array.AsReadOnly(excluded.OrderBy(x=>x.Form,StringComparer.Ordinal).ToArray());
        }
        static IReadOnlyList<string> Freeze(IEnumerable<string> values)=>Array.AsReadOnly(
            values.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray());
    }

    public static class NativeRecruitmentRosters
    {
        static readonly string[] Playable={"aimo","aquarion","bardur","cymanti","elyrion","hoodrick","imperius",
            "kickoo","luxidoor","oumaji","polaris","quetzali","vengir","xinxi","yadakk","zebasi"};
        static readonly HashSet<string> KnownSuper=new(StringComparer.Ordinal){"giant","crab","dragonegg","gaami","centipede"};
        static readonly HashSet<string> SpawnedOrGrown=new(StringComparer.Ordinal){"bunny","scout","polytaur","dagger",
            "mermaiddagger","babydragon","firedragon","segment","bugegg","larva"};
        static readonly HashSet<string> Ships=new(StringComparer.Ordinal){"cloak_boat","pirate","bombership","scoutship",
            "transportship","rammership","juggernaut"};

        public static IReadOnlyDictionary<string,NativeAppearanceRoster> AllResearchRosters(string nativeJson)
            =>AllResearchRosters(nativeJson,RecruitmentCatalog.Supported,RecruitmentCatalog.Deferred);

        // Explicit policy overload supports reviewed catalog evolution. Membership never
        // bypasses native reachability, hidden/independent checks or A11 super exclusion.
        public static IReadOnlyDictionary<string,NativeAppearanceRoster> AllResearchRosters(string nativeJson,
            IEnumerable<string> supportedForms,IEnumerable<string> deferredForms)
        {
            if(nativeJson is null || nativeJson.Length>8*1024*1024)throw Bad("Missing/oversize native registry");
            var supported=Policy(supportedForms);var deferred=Policy(deferredForms);
            if(supported.Overlaps(deferred))throw Bad("Supported and Deferred policy overlap");
            using var document=JsonDocument.Parse(nativeJson,new JsonDocumentOptions{MaxDepth=64});
            var root=Object(document.RootElement,"root");
            var tribes=Object(Required(root,"tribeData"),"tribeData");
            var techs=Object(Required(root,"techData"),"techData");
            var units=Object(Required(root,"unitData"),"unitData");
            if(techs.Count>512 || units.Count>1024 || tribes.Count>64)throw Bad("Registry exceeds reviewed bounds");
            var basic=Object(Resolve(techs,"basic"),"basic");
            if(!basic.TryGetValue("idx",out var index) || !index.TryGetInt32(out int n) || n!=0)
                throw Bad("Native Basic identity differs");
            var selected=tribes.Where(x=>Category(Object(x.Value,x.Key))!=0).ToArray();
            if(!selected.Select(x=>x.Key).OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(Playable))
                throw Bad("Playable tribe set differs from the captured 16-tribe registry");

            var result=new Dictionary<string,NativeAppearanceRoster>(StringComparer.Ordinal);
            foreach(var pair in selected.OrderBy(x=>x.Key,StringComparer.Ordinal))
            {
                var tribe=Object(pair.Value,pair.Key);
                var techOverrides=OptionalObject(tribe,"techOverrides");
                var unitOverrides=OptionalObject(tribe,"unitOverrides");
                var super=new HashSet<string>(KnownSuper,StringComparer.Ordinal){Override(unitOverrides,"giant")};
                var queue=new Queue<string>();queue.Enqueue("basic");
                var visited=new HashSet<string>(StringComparer.Ordinal);
                var candidates=new HashSet<string>(StringComparer.Ordinal);
                int popped=0;
                while(queue.Count!=0)
                {
                    if(++popped>200)throw Bad("Technology traversal exceeds reviewed native bound: "+pair.Key);
                    string raw=queue.Dequeue();
                    Resolve(techs,raw); // Native children are resolved TechData references before override.
                    string effective=Override(techOverrides,raw);
                    var technology=Object(Resolve(techs,effective),effective);
                    if(!visited.Add(effective))continue;
                    foreach(string next in Strings(technology,"techUnlocks"))queue.Enqueue(next);
                    foreach(string source in Strings(technology,"unitUnlocks"))
                    {
                        Resolve(units,source);
                        string form=Override(unitOverrides,source);
                        Resolve(units,form);candidates.Add(form);
                    }
                }
                var ready=new List<string>();var later=new List<string>();var unique=new List<string>();
                var excluded=new List<NativeRosterExclusion>();
                foreach(string form in candidates.OrderBy(x=>x,StringComparer.Ordinal))
                {
                    var data=Object(Resolve(units,form),form);
                    var abilities=new HashSet<string>(Strings(data,"unitAbilities"),StringComparer.Ordinal);
                    string? reason=super.Contains(form)?"city-super-unit":
                        SpawnedOrGrown.Contains(form)?"spawn-only-or-grown":
                        Bool(data,"hidden")?"hidden":
                        Bool(data,"independent")||abilities.Contains("independent")?"independent":
                        Ships.Contains(form)||abilities.Contains("carry")?"ship-or-carrier":
                        RecruitmentCatalog.FieldCopyOnly.Contains(form,StringComparer.Ordinal)?"field-copy-only":null;
                    if(reason is null && supported.Contains(form))ready.Add(form);
                    else if(reason is null && deferred.Contains(form))later.Add(form);
                    else excluded.Add(new(form,reason??"outside-reviewed-city-catalog"));
                    if(reason is null && (supported.Contains(form)||deferred.Contains(form)) && abilities.Contains("unique"))
                        unique.Add(form);
                }
                // Our genuine Spy is the same city-recruited addition for every tribe.
                string spy=pair.Key=="aquarion"?SpyCatalog.AquarionId:SpyCatalog.Id;
                if(supported.Contains(spy))ready.Add(spy);
                if(supported.Contains(FoggerCatalog.Id))ready.Add(FoggerCatalog.Id);
                result.Add(pair.Key,new(pair.Key,ready,later,visited,unique,excluded));
            }
            return new ReadOnlyDictionary<string,NativeAppearanceRoster>(result);
        }

        static InvalidDataException Bad(string message)=>new("A11 roster: "+message);
        static HashSet<string> Policy(IEnumerable<string> values)
        {
            if(values is null)throw Bad("Missing policy");var set=new HashSet<string>(StringComparer.Ordinal);
            foreach(string value in values)if(!set.Add(Id(value)))throw Bad("Duplicate policy form: "+value);
            return set;
        }
        static string Id(string? value)
        {
            if(value is not null&&(SpyCatalog.IsSpyId(value)||FoggerCatalog.IsFoggerId(value)))return value; // Our one reviewed custom city unit.
            if(string.IsNullOrEmpty(value) || value.Length>96 || value[0]<'a' || value[0]>'z' ||
                value.Any(c=>!(c>='a'&&c<='z')&&!(c>='0'&&c<='9')&&c!='_') || value.StartsWith("dcp_",StringComparison.Ordinal))
                throw Bad("Expected captured native lowercase ID");
            return value;
        }
        static Dictionary<string,JsonElement> Object(JsonElement element,string name)
        {
            if(element.ValueKind!=JsonValueKind.Object)throw Bad("Expected object: "+name);
            var result=new Dictionary<string,JsonElement>(StringComparer.Ordinal);
            foreach(var field in element.EnumerateObject())if(!result.TryAdd(field.Name,field.Value))throw Bad("Duplicate key: "+name+"."+field.Name);
            return result;
        }
        static Dictionary<string,JsonElement> OptionalObject(Dictionary<string,JsonElement> data,string field)
            =>data.TryGetValue(field,out var v)?Object(v,field):new(StringComparer.Ordinal);
        static JsonElement Required(Dictionary<string,JsonElement> data,string field)
            =>data.TryGetValue(field,out var v)?v:throw Bad("Missing "+field);
        static JsonElement Resolve(Dictionary<string,JsonElement> data,string id)
            =>data.TryGetValue(Id(id),out var v)&&v.ValueKind==JsonValueKind.Object?v:throw Bad("Unresolved native ID: "+id);
        static string Override(Dictionary<string,JsonElement> map,string id)
            =>map.TryGetValue(id,out var v)?Id(v.ValueKind==JsonValueKind.String?v.GetString():null):id;
        static IEnumerable<string> Strings(Dictionary<string,JsonElement> data,string field)
        {
            if(!data.TryGetValue(field,out var value))yield break; // Native omitted lists default empty.
            if(value.ValueKind!=JsonValueKind.Array)throw Bad("Expected array: "+field);
            foreach(var v in value.EnumerateArray())yield return Id(v.ValueKind==JsonValueKind.String?v.GetString():null);
        }
        static bool Bool(Dictionary<string,JsonElement> data,string field)
            =>!data.TryGetValue(field,out var v)?false:v.ValueKind==JsonValueKind.True?true:
                v.ValueKind==JsonValueKind.False?false:throw Bad("Expected Boolean: "+field);
        static int Category(Dictionary<string,JsonElement> tribe)
        {
            var value=Required(tribe,"category");
            if(value.ValueKind==JsonValueKind.Number&&value.TryGetInt32(out int n)&&n>=0&&n<=2)return n;
            if(value.ValueKind==JsonValueKind.String)return value.GetString() switch{
                "hidden"=>0,"human"=>1,"special"=>2,_=>throw Bad("Unknown tribe category")};
            throw Bad("Unknown tribe category");
        }
    }
}
