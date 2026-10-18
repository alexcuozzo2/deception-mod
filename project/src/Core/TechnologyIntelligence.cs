using System;
using System.Collections.Generic;
using System.Linq;

namespace Deception.Core
{
    // A slot is a technology and all its tribe replacements (Fishing/Aquafishing,
    // Ramming/Aquaculture/Sledding, etc.). Public tree data establishes aliases;
    // private research only enters the calculation after the met/alive filter.
    public sealed record ResearchTechnology(string Id,string Slot,int Tier);
    public sealed record ResearchOpponent(byte PlayerId,bool Met,bool Alive,string[] KnownSlots);
    public sealed record TechnologyIntelCell(string TechId,string Slot,int Tier,byte[] Researchers,int Income);
    public sealed record TechnologyIntelProjection(TechnologyIntelCell[] Cells,int Income);
    public static class TechnologyIntelligence
    {
        public static IReadOnlyDictionary<string,string> Slots(IEnumerable<string> technologyIds,
            IEnumerable<(string Source,string Target)> overrides,IEnumerable<string> preferredIds)
        {
            var ids=technologyIds.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            if(ids.Length==0||ids.Length>256||ids.Any(string.IsNullOrEmpty))throw new RuleViolation("Invalid research registry");
            var parent=ids.ToDictionary(x=>x,x=>x,StringComparer.Ordinal);
            string Root(string id){while(parent[id]!=id)id=parent[id];return id;}
            foreach(var pair in overrides)
            {
                if(!parent.ContainsKey(pair.Source)||!parent.ContainsKey(pair.Target))throw new RuleViolation("Unknown technology override");
                var a=Root(pair.Source);var b=Root(pair.Target);if(a!=b)parent[b]=a;
            }
            var preferred=new HashSet<string>(preferredIds,StringComparer.Ordinal);
            var result=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(var group in ids.GroupBy(Root))
            {
                var primary=group.Where(preferred.Contains).ToArray();
                if(primary.Length>1)throw new RuleViolation("Technology overrides merge distinct base slots");
                string slot=primary.FirstOrDefault()??group.First();
                foreach(var id in group)result.Add(id,slot);
            }
            return result;
        }
        public static TechnologyIntelProjection Project(byte viewer,IEnumerable<ResearchTechnology> technologies,
            IEnumerable<string> viewerResearch,IEnumerable<ResearchOpponent> opponents,bool tradeSecrets)
        {
            if(viewer is 0 or 255)throw new RuleViolation("Invalid research viewer");
            var tech=technologies.ToArray();var enemy=opponents.ToArray();
            if(tech.Length>256||tech.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Count()!=tech.Length||
                tech.Select(x=>x.Slot).Distinct(StringComparer.Ordinal).Count()!=tech.Length||
                tech.Any(x=>string.IsNullOrEmpty(x.Id)||string.IsNullOrEmpty(x.Slot)||x.Tier<1||x.Tier>3))
                throw new RuleViolation("Invalid viewer research tree");
            if(enemy.Length>254||enemy.Any(x=>x.PlayerId is 0 or 255)||enemy.Select(x=>x.PlayerId).Distinct().Count()!=enemy.Length)
                throw new RuleViolation("Invalid research opponents");
            var known=new HashSet<string>(viewerResearch,StringComparer.Ordinal);
            var eligible=enemy.Where(x=>x.PlayerId!=viewer&&x.Met&&x.Alive)
                .Select(x=>(x.PlayerId,Known:new HashSet<string>(x.KnownSlots,StringComparer.Ordinal))).ToArray();
            var cells=tech.Select(t=>
            {
                byte[] researchers=eligible.Where(p=>p.Known.Contains(t.Slot)).Select(p=>p.PlayerId).OrderBy(x=>x).ToArray();
                // Each met living opponent contributes the tier (1/2/3) for this missing slot.
                // Repeated/aliased research inside one opponent remains deduplicated.
                int income=tradeSecrets&&!known.Contains(t.Slot)?checked(t.Tier*researchers.Length):0;
                return new TechnologyIntelCell(t.Id,t.Slot,t.Tier,researchers,income);
            }).ToArray();
            return new(cells,cells.Sum(c=>c.Income));
        }
    }
}
