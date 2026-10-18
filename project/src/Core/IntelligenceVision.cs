using System;
using System.Collections.Generic;
using System.Linq;

namespace Deception.Core
{
    public readonly record struct VisionTile(int X,int Y,bool InspectorAccess,bool SubjectAccess);
    public readonly record struct VisionMark(int X,int Y);

    // Selection-based information only. No unit authenticity, discovery writes
    // or remembered exploration enters this value projection. Native callers
    // must supply current effective access and a currently visible selection.
    public static class IntelligenceVision
    {
        public static int? Subject(int inspector,int selectedOwner,int? rememberedRogueOwner=null)
        {
            int owner=rememberedRogueOwner??selectedOwner;
            return inspector>0&&owner>0&&owner<255&&owner!=inspector?owner:null;
        }
        public static VisionMark[] Fog(bool researched,int inspector,int? subject,bool selectionVisible,IEnumerable<VisionTile> tiles)
        {
            if(!researched||!selectionVisible||subject is not int owner||Subject(inspector,owner)!=owner)
                return Array.Empty<VisionMark>();
            ArgumentNullException.ThrowIfNull(tiles);
            var cells=tiles.ToArray();
            if(cells.Length>4096||cells.Select(t=>(t.X,t.Y)).Distinct().Count()!=cells.Length)
                throw new RuleViolation("Invalid Intelligence tile census.");
            return cells.Where(t=>t.InspectorAccess&&!t.SubjectAccess).Select(t=>new VisionMark(t.X,t.Y)).ToArray();
        }
    }
}
