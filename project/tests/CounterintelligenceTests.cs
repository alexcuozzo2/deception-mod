using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        static void CounterintelligenceTests()
        {
            string input=File.ReadAllText(Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE")!);
            string Added()=>RecruitmentCatalog.Add(TechTree.AddDeception(input).Json).Json;
            Test("A52-001-five-node-topology-and-stable-tech-identities",new[]{"A52"},()=> {
                Assert(TechTree.Branch.Select(x=>(x.Id,x.Parent,x.Tier)).SequenceEqual(new[]{
                    (TechTree.DeceptionId,(string?)null,1),(TechTree.PropagandaId,(string?)TechTree.DeceptionId,2),
                    (TechTree.IntelligenceId,(string?)TechTree.DeceptionId,2),(TechTree.EspionageId,(string?)TechTree.PropagandaId,3),
                    (TechTree.CounterintelligenceId,(string?)TechTree.IntelligenceId,3)}));
                Assert(TechTree.CounterintelligenceId=="dcp_camouflage"&&NativeRegistryIds.Entries.Single(x=>x.Id==TechTree.CounterintelligenceId).Index==1034);
            });
            Test("A52-002-fogger-stats-unlock-and-independent-native-budget",new[]{"A52"},()=> {
                var root=JsonNode.Parse(Added())!;var unit=root["unitData"]![FoggerCatalog.Id]!;
                Assert(unit["cost"]!.GetValue<int>()==5&&unit["health"]!.GetValue<int>()==100&&unit["defence"]!.GetValue<int>()==10&&unit["attack"]!.GetValue<int>()==0);
                Assert(unit["movement"]!.GetValue<int>()==2&&unit["range"]!.GetValue<int>()==2);
                Assert(unit["unitAbilities"]!.AsArray().Select(x=>x!.GetValue<string>()).SequenceEqual(new[]{"fly","dash","scout","doubleattack"}));
                Assert(root["techData"]![TechTree.CounterintelligenceId]!["unitUnlocks"]!.AsArray().Single()!.GetValue<string>()==FoggerCatalog.Id);
                Assert(!SpyCatalog.IsSpyId(FoggerCatalog.Id)&&!RecruitmentCatalog.IsMimicId(FoggerCatalog.Id));
            });
            Test("A52-003-fogger-mimic-only-borrows-movement",new[]{"A52","DEC-008"},()=> {
                var root=JsonNode.Parse(Added())!;var unit=root["unitData"]![RecruitmentCatalog.Variant(FoggerCatalog.Id)]!;
                var abilities=unit["unitAbilities"]!.AsArray().Select(x=>x!.GetValue<string>()).ToArray();
                Assert(unit["health"]!.GetValue<int>()==10&&unit["attack"]!.GetValue<int>()==0&&unit["defence"]!.GetValue<int>()==0);
                Assert(abilities.Contains("fly")&&abilities.Contains("dash")&&!abilities.Intersect(new[]{"scout","doubleattack","hide","infiltrate"}).Any());
                Assert(NativeRegistryIds.Entries.Single(x=>x.Id==FoggerCatalog.Id).Index==1084&&NativeRegistryIds.Entries.Single(x=>x.Id==RecruitmentCatalog.Variant(FoggerCatalog.Id)).Index==1085);
            });
            Test("A52-004-fogger-all-tribes-have-research-gated-roster",new[]{"A52"},()=> {
                foreach(var roster in NativeRecruitmentRosters.AllResearchRosters(input).Values)Assert(roster.Supported.Contains(FoggerCatalog.Id));
                var unlocked=new System.Collections.Generic.HashSet<string>();
                Assert(!RecruitmentEligibility.Permitted(true,FoggerCatalog.Id,unlocked));unlocked.Add(FoggerCatalog.Id);
                Assert(RecruitmentEligibility.Permitted(true,FoggerCatalog.Id,unlocked));
            });
            Test("A52-005-fogger-reentry-rejects-altered-stats-and-unlock",new[]{"A52"},()=> {
                string added=Added();RecruitmentCatalog.ValidateReentry(added);
                var root=JsonNode.Parse(added)!;root["unitData"]![FoggerCatalog.Id]!["attack"]=10;Reject(()=>RecruitmentCatalog.ValidateReentry(root.ToJsonString()),"Fogger");
                root=JsonNode.Parse(added)!;root["techData"]![TechTree.CounterintelligenceId]!["unitUnlocks"]!.AsArray().Clear();Reject(()=>RecruitmentCatalog.ValidateReentry(root.ToJsonString()),"Fogger");
            });
        }
    }
}
