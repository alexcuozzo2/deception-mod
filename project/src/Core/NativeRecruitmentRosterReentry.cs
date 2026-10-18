// Run AFTER the existing complete registry reentry validators, before mapping admission.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Deception.Core
{
    public static class NativeRecruitmentRosterReentry
    {
        public static void Validate(string validatedReentryJson,IReadOnlyDictionary<string,NativeAppearanceRoster>? retained)
        {
            if(retained is null||retained.Count!=16)throw new InvalidDataException("No exact retained 16-tribe appearance roster for reentry.");
            if(validatedReentryJson is null||validatedReentryJson.Length>8*1024*1024)
                throw new InvalidDataException("Missing/oversize registry reentry input.");
            // JsonNode creates a fully private managed tree. Exactly the one
            // validated owned root edge is removed. No player/native JSON write,
            // source-definition normalization or retained-roster replacement.
            var copy=JsonNode.Parse(validatedReentryJson) as JsonObject??throw new InvalidDataException("Expected registry object.");
            var children=copy["techData"]?["basic"]?["techUnlocks"] as JsonArray??throw new InvalidDataException("Missing native Basic children.");
            int found=-1;
            for(int i=0;i<children.Count;i++)
            {
                if(children[i] is not JsonValue value||!value.TryGetValue<string>(out var id))
                    throw new InvalidDataException("Invalid native Basic child ID.");
                if(id!=TechTree.DeceptionId)continue;
                if(found>=0)throw new InvalidDataException("Duplicated owned Basic edge during roster reentry.");
                found=i;
            }
            if(found<0)throw new InvalidDataException("Missing owned Basic edge during roster reentry.");
            children.RemoveAt(found);
            var rebuilt=NativeRecruitmentRosters.AllResearchRosters(copy.ToJsonString());
            if(retained.Count!=rebuilt.Count)throw new InvalidDataException("Playable roster domain changed during reentry.");
            foreach(var pair in rebuilt)
            {
                if(!retained.TryGetValue(pair.Key,out var prior)||prior is null||!Same(prior,pair.Value))
                    throw new InvalidDataException("Native appearance roster changed during reentry: "+pair.Key);
            }
        }
        static bool Same(NativeAppearanceRoster a,NativeAppearanceRoster b)=>
            a.Tribe==b.Tribe&&a.Supported.SequenceEqual(b.Supported,StringComparer.Ordinal)&&
            a.Deferred.SequenceEqual(b.Deferred,StringComparer.Ordinal)&&
            a.EffectiveTechnologies.SequenceEqual(b.EffectiveTechnologies,StringComparer.Ordinal)&&
            a.RequiresUniqueCheck.SequenceEqual(b.RequiresUniqueCheck,StringComparer.Ordinal)&&
            a.Excluded.SequenceEqual(b.Excluded);
    }
}
