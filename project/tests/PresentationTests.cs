// UI-A06 model checks. No native input, renderer or game execution is implied.
using System;
using Deception.Core;

namespace Deception.Tests
{
    public static class PresentationTests
    {
        static void Check(bool value) { if(!value)throw new Exception("Presentation model assertion failed"); }
        public static void Run(Action<string,string[],Action> test)
        {
            test("M1-061-explicit-inspection-apparent-actual-apparent",new[]{"DEC-010","DEC-014"},()=> {
                var s=new MimicInspectionSession();var c=new InspectionContext(10,1,7,3);
                Check(!s.Toggle(c,true));s.Select(c,true);s.Select(c,true);Check(!s.ActualFor(c,true));
                Check(s.Toggle(c,true)&&s.ActualFor(c,true));s.Select(c,true);Check(s.ActualFor(c,true));
                Check(s.Toggle(c,true)&&!s.ActualFor(c,true));
            });
            test("M1-062-inspection-context-is-complete",new[]{"DEC-011","DEC-177"},()=> {
                var c=new InspectionContext(10,1,7,3);
                foreach(var next in new[]{new InspectionContext(11,1,7,3),new InspectionContext(10,2,7,3),
                    new InspectionContext(10,1,8,3),new InspectionContext(10,1,7,4)})
                {
                    var s=new MimicInspectionSession();s.Select(c,true);s.Toggle(c,true);Check(s.ActualFor(c,true));
                    Check(!s.Toggle(next,true));Check(!s.ActualFor(c,true));Check(s.StoredActualFor(c,true));
                    s.Select(next,true);
                    Check(s.ActualFor(next,true)==(next.World==c.World&&next.Viewer==c.Viewer&&next.UnitId==c.UnitId));
                }
            });
            test("M1-063-inspection-authorization-and-reset",new[]{"DEC-011","DEC-014","DEC-177"},()=> {
                var c=new InspectionContext(10,1,7,3);var s=new MimicInspectionSession();
                s.Select(c,true);s.Toggle(c,true);Check(!s.ActualFor(c,false));
                Check(!s.StoredActualFor(c,false)&&s.StoredActualFor(c,true));
                s.Select(c,false);Check(!s.ActualFor(c,true));s.Select(c,true);Check(s.ActualFor(c,true));
                Check(!s.Toggle(c,false));s.Select(c,true);Check(s.ActualFor(c,true));
                s.Reset();Check(!s.ActualFor(c,true)&&!s.StoredActualFor(c,true));
                s.Select(c,true);Check(!s.ActualFor(c,true));
            });
            test("M1-064-target-click-is-not-inspection",new[]{"DEC-011","DEC-177"},()=> {
                var c=new InspectionContext(10,1,7,3);var other=new InspectionContext(10,1,7,4);
                var s=new MimicInspectionSession();s.Select(c,true);s.Toggle(c,true);
                Check(!s.Toggle(c,true,true));s.Select(c,true,true);Check(s.ActualFor(c,true));
                Check(!s.Toggle(other,true,true));s.Select(other,true,true);Check(s.ActualFor(c,true));Check(!s.ActualFor(other,true));
                Check(s.ActualFor(c,true));
            });
            test("M1-067-apparent-health-scale-and-validation",new[]{"DEC-022","DEC-028"},()=> {
                var full=ApparentHealth.Fresh(40);Check(full.Current==40&&full.IsFull&&full.ToNativeStored()==400);
                var injured=new ApparentHealth(6,10);Check(!injured.IsFull&&injured.ToNativeStored()==60);
                foreach(var invalid in new[]{new ApparentHealth(-1,10),new ApparentHealth(11,10),new ApparentHealth(0,0)})
                {bool rejected=false;try{invalid.Validate();}catch(RuleViolation){rejected=true;}Check(rejected);}
                Check(injured.Current==6&&full.Current==40);
            });
            test("M1-068-session-choice-survives-selection-clear",new[]{"A06-05"},()=> {
                var a=new InspectionContext(10,1,7,3);var b=new InspectionContext(10,1,7,4);var s=new MimicInspectionSession();
                s.Select(a,true);Check(s.Toggle(a,true));s.Select(b,true);
                Check(!s.ActualFor(a,true)&&s.StoredActualFor(a,true)&&!s.ActualFor(b,true));
                Check(s.Toggle(b,true)&&s.StoredActualFor(a,true)&&s.StoredActualFor(b,true));
                Check(s.Toggle(b,true)&&!s.StoredActualFor(b,true)&&s.StoredActualFor(a,true));
                s.ClearSelection();Check(!s.ActualFor(a,true)&&s.StoredActualFor(a,true));
                Check(!s.Toggle(a,true));s.Select(a,true);Check(s.ActualFor(a,true));
            });
            test("M1-069-session-turn-memory-rejects-stale-hold",new[]{"A06-07","A06-12"},()=> {
                var oldTurn=new InspectionContext(10,1,7,3);var nextTurn=oldTurn with{Turn=8};var s=new MimicInspectionSession();
                s.Select(oldTurn,true);Check(s.Toggle(oldTurn,true));
                Check(s.StoredActualFor(nextTurn,true)&&!s.ActualFor(nextTurn,true));
                s.Select(nextTurn,true);Check(s.ActualFor(nextTurn,true));
                Check(!s.Toggle(oldTurn,true)&&s.StoredActualFor(nextTurn,true));
                Check(!s.Toggle(nextTurn,true));s.Select(nextTurn,true);
                Check(s.Toggle(nextTurn,true)&&!s.StoredActualFor(nextTurn,true));
            });
            test("M1-070-session-viewer-world-isolation-and-reset",new[]{"A06-08","A06-09","A06-10"},()=> {
                var a=new InspectionContext(10,1,7,3);var b=a with{Viewer=2};var anotherWorld=a with{World=11};
                var s=new MimicInspectionSession();s.Select(a,true);Check(s.Toggle(a,true));
                s.Select(b,true);Check(!s.ActualFor(b,true)&&s.StoredActualFor(a,true));Check(s.Toggle(b,true));
                s.Select(a,true);Check(s.Toggle(a,true)&&!s.StoredActualFor(a,true)&&s.StoredActualFor(b,true));
                Check(!s.ActualFor(b,false)&&!s.StoredActualFor(b,false)&&s.StoredActualFor(b,true));
                s.Select(anotherWorld,true);Check(!s.ActualFor(anotherWorld,true));
                s.Reset();s.Select(b,true);Check(!s.ActualFor(b,true)&&!s.StoredActualFor(a,true));
            });
        }
    }
    public static partial class KernelTests
    {
        static void SelectionPresentationTests()
        {
            Test("M1-065-mimic-inspection-independent-from-commands",new[]{"DEC-010","DEC-027"},()=> {
                var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");u.Appearance.Hp=4;
                string before=Codec.Encode(w),enemy=Observation.Capture(w,2);var s=new Selection();
                s.Click(w,1,u.LogicalId);Assert(s.Mode==PanelMode.Actual&&s.InspectionMode==PanelMode.Appearance);
                s.Click(w,1,u.LogicalId);Assert(s.InspectionMode==PanelMode.Appearance);
                Assert(s.ToggleInspection(w,1,u.LogicalId)&&s.Mode==PanelMode.Actual&&s.InspectionMode==PanelMode.Actual);
                Assert(s.ToggleInspection(w,1,u.LogicalId)&&s.Mode==PanelMode.Actual&&s.InspectionMode==PanelMode.Appearance);
                Assert(Codec.Encode(w)==before&&Observation.Capture(w,2)==enemy);
                Assert(u.ActualHp==1&&u.Appearance.Hp==4&&CapabilityResolver.Resolve(u,N).CanDamage==false);
            });
            Test("M1-066-mimic-private-selection-access-loss",new[]{"DEC-011","DEC-014","DEC-177"},()=> {
                var w=World();var u=Add(w,ActualKind.Mimic,"mimic","warrior");var s=new Selection();
                s.Click(w,1,u.LogicalId);Assert(s.ToggleInspection(w,1,u.LogicalId)&&s.InspectionMode==PanelMode.Actual);
                Assert(!s.ToggleInspection(w,2,u.LogicalId));s.Click(w,2,u.LogicalId);Assert(s.Unit==null);
                Assert(!s.StoredActualFor(w,2,u.LogicalId));s.Click(w,1,u.LogicalId);Assert(s.InspectionMode==PanelMode.Actual);
                w.Masks[1].AccessibleTiles.Remove(u.Position.Key);s.Click(w,1,u.LogicalId);Assert(s.Unit==null);
                Assert(!s.StoredActualFor(w,1,u.LogicalId));
                w.Masks[1].AccessibleTiles.Add(u.Position.Key);s.Click(w,1,u.LogicalId);Assert(s.InspectionMode==PanelMode.Actual);
            });
            Test("M1-071-selection-action-keeps-view-without-refreshing-budget",new[]{"A06-05","A06-06","A06-16"},()=> {
                var w=World();var a=Add(w,ActualKind.Mimic,"mimic","warrior");var b=Add(w,ActualKind.Mimic,"mimic","rider",x:2);
                var s=new Selection();s.Click(w,1,a.LogicalId);Assert(s.ToggleInspection(w,1,a.LogicalId));
                a.Allowance.EndTurn();string afterAction=Codec.Encode(w);
                s.ClearSelection();s.Click(w,1,b.LogicalId);
                Assert(s.InspectionMode==PanelMode.Appearance&&s.StoredActualFor(w,1,a.LogicalId));
                s.Click(w,1,a.LogicalId);Assert(s.InspectionMode==PanelMode.Actual&&s.Mode==PanelMode.Actual);
                Assert(a.Allowance.MovementSpent&&a.Allowance.ActionSpent&&Codec.Encode(w)==afterAction);
            });
            Test("M1-072-selection-turn-persists-reload-defaults",new[]{"A06-07","A06-08","A06-16"},()=> {
                var w=World();var a=Add(w,ActualKind.Mimic,"mimic","warrior");var s=new Selection();
                s.Click(w,1,a.LogicalId);Assert(s.ToggleInspection(w,1,a.LogicalId));w.Players[1].Turn++;
                Assert(!s.ToggleInspection(w,1,a.LogicalId)&&s.StoredActualFor(w,1,a.LogicalId));
                s.Click(w,1,a.LogicalId);Assert(s.InspectionMode==PanelMode.Actual);
                string saved=Codec.Encode(w);var reloaded=Codec.Decode(saved);s.Click(reloaded,1,a.LogicalId);
                Assert(s.InspectionMode==PanelMode.Appearance&&!s.StoredActualFor(reloaded,1,a.LogicalId));
                Assert(Codec.Encode(w)==saved&&Codec.Encode(reloaded)==saved);
                s.Reset();s.Click(w,1,a.LogicalId);Assert(s.InspectionMode==PanelMode.Appearance);
            });
        }
    }
}
