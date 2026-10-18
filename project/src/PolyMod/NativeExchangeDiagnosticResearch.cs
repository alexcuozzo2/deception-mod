using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Deception.PolyModAdapter
{
    // Reserved historical identity1033. Always register it before new branch
    // technologies so the loader counter stays compatible. No root edge,
    // starting research, reward or menu entry exposes this retired technology.
    internal static class NativeExchangeDiagnosticResearch
    {
        internal static bool Enabled=>Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXCHANGE_PROBE")=="1";
        internal static string Add(string json)
        {
            var root=JsonNode.Parse(json)!.AsObject();var techs=root["techData"]!.AsObject();
            foreach(string id in new[]{"dcp_misdirection"})
            {
                if(techs.ContainsKey(id))throw new InvalidOperationException("Conflicting diagnostic research identity: "+id);
                techs[id]=new JsonObject{["idx"]=-1,["cost"]=2,["techUnlocks"]=new JsonArray(),["unitUnlocks"]=new JsonArray()};
            }
            return root.ToJsonString();
        }
        internal static void ValidateReentry(string json)
        {
            var root=JsonNode.Parse(json)!.AsObject();var techs=root["techData"]!.AsObject();
            foreach(string id in new[]{"dcp_misdirection"})
            {
                if(techs[id] is not JsonObject node||node["cost"]?.GetValue<int>()!=2||
                    node["techUnlocks"] is not JsonArray children||children.Count!=0||
                    node["unitUnlocks"] is not JsonArray units||units.Count!=0||
                    techs.Any(pair=>pair.Value?["techUnlocks"] is JsonArray edges&&edges.Any(edge=>edge?.GetValue<string>()==id)))
                    throw new InvalidOperationException("Diagnostic research identities must remain unlinked and unchanged.");
            }
        }
    }
}
