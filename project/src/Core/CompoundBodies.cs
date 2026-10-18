// A01 / v1.6. These are model rules, not native attachment/promotion hooks.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Deception.Core
{
    public static class CompoundBodies
    {
        public const string PrivateRejection="Only standalone Centipede heads can Regroup or Disguise.";
        public static bool HasAttachedBodies(World w,long headId)
        {
            // Graph edges, not tile proximity, apparent masks or current visibility.
            foreach(var edge in w.SegmentParents)
            {
                long at=edge.Value;var seen=new HashSet<long>();
                while(true)
                {
                    if(at==headId) return true;
                    if(!seen.Add(at)) throw new RuleViolation("Cyclic native attachment graph");
                    if(!w.SegmentParents.TryGetValue(at,out long parent)) break;
                    at=parent;
                }
            }
            return false;
        }
        public static void ValidateGraph(World w,bool allowMaskCleanup=false)
        {
            foreach(var u in w.Units.Values)
            {
                if(!Enum.IsDefined(typeof(BodyStructure),u.Structure) || !Enum.IsDefined(typeof(BodyStructure),u.Appearance.Structure))
                    throw new RuleViolation("Unknown body structure");
                if(u.Kind!=ActualKind.Genuine && u.Structure!=BodyStructure.SingleBody)
                    throw new RuleViolation("A Mimic/Spy cannot acquire a genuine Centipede structure");
                if(u.IndependentAppearance && u.Appearance.Structure==BodyStructure.CentipedeSegment)
                    throw new RuleViolation("A copied Centipede appearance cannot be a body segment");
            }
            foreach(var edge in w.SegmentParents)
            {
                if(!w.Units.TryGetValue(edge.Key,out var child) || !w.Units.TryGetValue(edge.Value,out var parent) ||
                    !child.Alive || !parent.Alive || child.Kind!=ActualKind.Genuine || parent.Kind!=ActualKind.Genuine ||
                    child.Structure!=BodyStructure.CentipedeSegment || parent.Structure==BodyStructure.SingleBody)
                    throw new RuleViolation("Invalid native head/body attachment endpoint");
                var seen=new HashSet<long>{edge.Key};long at=edge.Value;
                while(true)
                {
                    if(!seen.Add(at)) throw new RuleViolation("Cyclic native attachment graph");
                    if(!w.SegmentParents.TryGetValue(at,out long next)) break;
                    at=next;
                }
                if(!w.Units.TryGetValue(at,out var root) || root.Structure!=BodyStructure.CentipedeHead)
                    throw new RuleViolation("Connected native bodies have no genuine head");
            }
            foreach(var u in w.Units.Values)
                if(u.IndependentAppearance && (u.Structure==BodyStructure.CentipedeSegment ||
                    (u.Structure==BodyStructure.CentipedeHead && HasAttachedBodies(w,u.LogicalId))))
                {
                    if(allowMaskCleanup && u.Structure==BodyStructure.CentipedeHead) continue;
                    throw new RuleViolation("Illegal legacy mask on connected head/body; migration not implemented");
                }
        }
        public static void RequireExchangeParticipant(World w,UnitRecord unit,INativeRules native)
        {
            if(native.Profile(unit.ActualBody).Structure!=unit.Structure)
                throw new RuleViolation("Actual native identity/topology role mismatch");
            if(unit.Structure==BodyStructure.CentipedeSegment ||
               (unit.Structure==BodyStructure.CentipedeHead && HasAttachedBodies(w,unit.LogicalId)))
                throw new RuleViolation(PrivateRejection);
        }
        public static void RequireCopyable(AppearanceRecord appearance,INativeRules native)
        {
            // The adapter must resolve appearance.Body to the real native form ID;
            // a misleading mask never substitutes actual identity for apparent identity.
            var shape=native.Profile(appearance.Body).Structure;
            if(shape==BodyStructure.CentipedeSegment || shape!=appearance.Structure)
                throw new RuleViolation("Invalid apparent body shape; only the solo-head Centipede form is copyable");
        }
    }
}
