using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        static void BranchAmendmentTests()
        {
            Test("A11-004-regroup-with-deception-only",new[]{"A11"},()=> {
                var w=World();w.Players[1].Technologies.Clear();w.Players[1].Technologies.Add("deception");
                var a=Add(w,ActualKind.Mimic,"mimic","warrior");var b=Add(w,ActualKind.Genuine,"rider","rider",x:2);
                var result=Rules.Exchange(w,"regroup",1,a.LogicalId,b.LogicalId,ExchangeKind.Regroup,false,N).State;
                Assert(result.Units[a.LogicalId].Appearance.Body=="rider"&&result.Units[a.LogicalId].Allowance.ActionSpent);
                w.Players[1].Technologies.Clear();Reject(()=>Rules.Exchange(w,"missing",1,a.LogicalId,b.LogicalId,ExchangeKind.Regroup,false,N),"tech");
            });
            Test("A11-005-disguise-now-needs-propaganda",new[]{"A11"},()=> {
                var w=World();w.Players[1].Technologies.Clear();w.Players[1].Technologies.UnionWith(new[]{"deception","camouflage"});
                var a=Add(w,ActualKind.Mimic,"mimic","warrior");var b=Add(w,ActualKind.Genuine,"rider","rider",x:2);
                Reject(()=>Rules.Exchange(w,"missing",1,a.LogicalId,b.LogicalId,ExchangeKind.Disguise,false,N),"tech");
                w.Players[1].Technologies.Add("propaganda");var result=Rules.Exchange(w,"disguise",1,a.LogicalId,b.LogicalId,ExchangeKind.Disguise,false,N).State;
                Assert(result.Units[b.LogicalId].Appearance.Body=="warrior");
            });
            Test("A11-006-five-native-nodes-and-reentry",new[]{"A11"},()=> {
                string input=File.ReadAllText(Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE")??"tests/native-tree.captured.json");
                var output=TechTree.AddDeception(input);var root=JsonNode.Parse(output.Json)!;TechTree.ValidateReentry(output.Json);
                foreach(var node in TechTree.Branch)
                {
                    var data=root["techData"]![node.Id]!;Assert(data["cost"]!.GetValue<int>()==node.Tier);
                    Assert(data["techUnlocks"]!.AsArray().Select(x=>x!.GetValue<string>()).SequenceEqual(TechTree.Branch.Where(x=>x.Parent==node.Id).Select(x=>x.Id)));
                }
                root["techData"]![TechTree.PropagandaId]!["cost"]=3;
                Reject(()=>TechTree.ValidateReentry(root.ToJsonString()),"Conflicting");
            });
            Test("A11-007-intelligence-clips-to-own-access",new[]{"A11","DEC-154"},()=> {
                var tiles=new[]{new VisionTile(1,1,true,false),new VisionTile(2,1,true,true),new VisionTile(3,1,false,false),new VisionTile(4,1,false,true)};
                Assert(IntelligenceVision.Fog(true,1,2,true,tiles).SequenceEqual(new[]{new VisionMark(1,1)}));
            });
            Test("A11-008-intelligence-selection-and-research-gates",new[]{"A11","DEC-154"},()=> {
                var tiles=new[]{new VisionTile(1,1,true,false)};
                Assert(IntelligenceVision.Fog(false,1,2,true,tiles).Length==0);
                Assert(IntelligenceVision.Fog(true,1,2,false,tiles).Length==0);
                Assert(IntelligenceVision.Fog(true,1,null,true,tiles).Length==0);
                Assert(IntelligenceVision.Fog(true,1,1,true,tiles).Length==0);
            });
            Test("A11-009-intelligence-follows-effective-mask",new[]{"A11","DEC-154"},()=> {
                var tiles=new[]{new VisionTile(1,1,true,false)};
                Assert(IntelligenceVision.Fog(true,1,2,true,tiles).Length==1);
                tiles[0]=tiles[0] with{SubjectAccess=true};Assert(IntelligenceVision.Fog(true,1,2,true,tiles).Length==0);
                tiles[0]=tiles[0] with{SubjectAccess=false,InspectorAccess=false};Assert(IntelligenceVision.Fog(true,1,2,true,tiles).Length==0);
            });
            Test("A11-010-intelligence-rogue-original-owner",new[]{"A11","DEC-155"},()=> {
                Assert(IntelligenceVision.Subject(1,1,2)==2&&IntelligenceVision.Subject(1,2,1) is null);
                Assert(IntelligenceVision.Subject(1,255) is null&&IntelligenceVision.Subject(1,0) is null);
                var duplicate=new[]{new VisionTile(1,1,true,false),new VisionTile(1,1,true,false)};
                Reject(()=>IntelligenceVision.Fog(true,1,2,true,duplicate),"census");
            });
        }
    }
}
