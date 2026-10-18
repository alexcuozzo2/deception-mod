using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Deception.Core
{
    // The same ordinary PolyMod GLD patch is loaded by PolyMod and consumed by
    // these guards. Production must configure it from the archive's Mod.File.
    public static class ContentDefinitions
    {
        static JsonObject? patch;
        public static void Configure(string json)
        {
            var parsed=JsonNode.Parse(json) as JsonObject ?? throw new RuleViolation("Invalid Deception content patch");
            foreach(string section in new[]{"techData","unitData","tribeData"})
                if(parsed[section] is not JsonObject)throw new RuleViolation("Missing content section: "+section);
            patch=parsed;
        }
        static JsonObject Patch
        {
            get
            {
                if(patch is null)
                {
#if DECEPTION_CONTENT_FIXTURE
                    using var stream=typeof(ContentDefinitions).Assembly.GetManifestResourceStream("Deception.Content.patch.json")
                        ??throw new RuleViolation("Missing fixture content patch");
                    using var reader=new StreamReader(stream);Configure(reader.ReadToEnd());
#else
                    throw new RuleViolation("Deception archive content was not configured");
#endif
                }
                return patch!;
            }
        }
        public static JsonObject CloneRow(string section,string id)
            =>JsonNode.Parse((Patch[section]?[id] as JsonObject ??throw new RuleViolation("Missing content definition: "+section+"/"+id)).ToJsonString())!.AsObject();

        public static JsonObject UnitFromNative(JsonObject source,string id,bool mimic)
        {
            var result=JsonNode.Parse(source.ToJsonString())!.AsObject();
            foreach(string key in new[]{"upgradesFrom","promotesTo","growthRate","attractsTo"})result.Remove(key);
            if(mimic)result.Remove("embarksTo");
            foreach(var field in CloneRow("unitData",id))
                result[field.Key]=field.Value is null?null:JsonNode.Parse(field.Value.ToJsonString());
            return result;
        }

        // First load only: PolyMod already merged patch.json before its processing
        // callback. Validate its contribution, then reconstruct privately in the
        // historical registration order so saved numeric identities cannot drift.
        public static string NormalizeInput(string json)
        {
            var root=JsonNode.Parse(json)!.AsObject();
            foreach(string section in new[]{"techData","unitData"})
                foreach(var entry in Patch[section]!.AsObject())
                {
                    var actual=root[section]?[entry.Key] as JsonObject ??throw new RuleViolation("Loader omitted content: "+entry.Key);
                    var normalized=JsonNode.Parse(actual.ToJsonString())!.AsObject();normalized["idx"]=-1;
                    var expected=CloneRow(section,entry.Key);expected["idx"]=-1;
                    if(TechTree.Canonical(normalized)!=TechTree.Canonical(expected))throw new RuleViolation("Conflicting loader content: "+entry.Key);
                    root[section]!.AsObject().Remove(entry.Key);
                }
            AquarionCatalog.RemoveValidatedOverrides(root);
            return root.ToJsonString();
        }
    }
}
