using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        static (World W,UnitRecord I,UnitRecord H) Solo(bool disguised=false)
        {
            var w=World();var i=Add(w,ActualKind.Mimic,"mimic","warrior");
            var h=Add(w,ActualKind.Genuine,"centipede",disguised?"warrior":"centipede",x:2);
            return (w,i,h);
        }
        static UnitRecord AddSegment(World w,long? parent=null,int x=8)
        {
            var s=Add(w,ActualKind.Genuine,"segment","segment",x:x);
            if(parent.HasValue) w.SegmentParents.Add(s.LogicalId,parent.Value);
            return s;
        }
        static string Flags(UnitRecord u) => JsonSerializer.Serialize(u.Allowance);
        static void BothOutcomes(ExchangeKind kind,Action<bool> test) { test(false);test(true); }
        static void AmendmentTests()
        {
            string captured=File.ReadAllText(Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE") ?? "tests/native-tree.captured.json");
            Test("M0-063-captured-all-sixteen-tribes-preserved",new[]{"DEC-001","DEC-196"},()=> {
                var before=JsonNode.Parse(captured)!;var result=TechTree.AddDeception(captured);var after=JsonNode.Parse(result.Json)!;
                Assert(result.PlayableTribes.Length==16 && result.TribeAudit.Count(t=>t.Category=="Human")==12 && result.TribeAudit.Count(t=>t.Category=="Special")==4);
                Assert(result.TribeAudit.Where(t=>t.Category=="Special").Select(t=>t.Tribe).SequenceEqual(new[]{"aquarion","cymanti","elyrion","polaris"}));
                Assert(TechTree.Canonical(before["tribeData"])==TechTree.Canonical(after["tribeData"]));
                foreach(var a in result.TribeAudit) Assert(a.RootIndex==0 && a.StartingTech.Contains(a.Root) && a.ExtendedBranches.Where(id=>id!=TechTree.DeceptionId).SequenceEqual(a.NativeBranches) && a.ExtendedBranches[3]==TechTree.DeceptionId);
                Captures.Add(new{scope="CAPTURED REGISTRY DATA; NOT NATIVE RESEARCH EXECUTION",result.TribeAudit});
            });
            Test("M0-064-captured-native-fields-and-prices-unchanged",new[]{"DEC-196"},()=> {
                var a=JsonNode.Parse(captured)!;var b=JsonNode.Parse(TechTree.AddDeception(captured).Json)!;
                foreach(var p in a.AsObject()) if(p.Key!="techData") Assert(TechTree.Canonical(p.Value)==TechTree.Canonical(b[p.Key]));
                foreach(var p in a["techData"]!.AsObject())
                {
                    if(p.Key!="basic") Assert(TechTree.Canonical(p.Value)==TechTree.Canonical(b["techData"]![p.Key]));
                    else foreach(var q in p.Value!.AsObject()) if(q.Key!="techUnlocks") Assert(TechTree.Canonical(q.Value)==TechTree.Canonical(b["techData"]!["basic"]![q.Key]));
                }
            });
            Test("M0-065-legacy-root-prerequisite-regression",new[]{"DEC-001"},()=> {
                var input=JsonNode.Parse(captured)!;var imperius=input["tribeData"]!["imperius"]!;
                var starting=imperius["startingTech"]!.AsArray().Select(x=>x!.GetValue<string>()).ToArray();
                Assert(starting.Contains("basic") && !starting.Contains("dcp_root_imperius"));
                // Reproduce the exact data invariant missed by M0.1 (not the native command).
                string oldPrerequisite="dcp_root_imperius";Assert(!starting.Contains(oldPrerequisite));
                var fixedTree=TechTree.AddDeception(captured).TribeAudit.Single(x=>x.Tribe=="imperius");
                Assert(starting.Contains(fixedTree.Root) && fixedTree.ExtendedBranches.SequenceEqual(new[]{"organization","climbing","fishing",TechTree.DeceptionId,"hunting","riding"}));
            });
            Test("M0-066-captured-special-root-replacements-preserved",new[]{"DEC-196"},()=> {
                var a=TechTree.AddDeception(captured).TribeAudit;
                Assert(a.Single(t=>t.Tribe=="aquarion").NativeBranches.Contains("aquafishing"));
                Assert(a.Single(t=>t.Tribe=="elyrion").NativeBranches.Contains("forestmagic"));
                Assert(a.Single(t=>t.Tribe=="polaris").NativeBranches.Contains("frostwork"));
            });
            Test("M0-067-reentry-on-assigned-custom-index",new[]{"DEC-001"},()=> {
                var a=JsonNode.Parse(TechTree.AddDeception(captured).Json)!;a["techData"]![TechTree.DeceptionId]!["idx"]=1000;
                string original=a.ToJsonString();TechTree.ValidateReentry(original);Assert(original==a.ToJsonString());
            });
            Test("M0-068-reentry-rejects-duplicate-edge",new[]{"DEC-001"},()=> {
                var a=JsonNode.Parse(TechTree.AddDeception(captured).Json)!;a["techData"]!["basic"]!["techUnlocks"]!.AsArray().Add(TechTree.DeceptionId);
                Reject(()=>TechTree.ValidateReentry(a.ToJsonString()),"Conflicting");
            });
            Test("M0-069-never-grant-missing-root-to-hide-error",new[]{"DEC-001","DEC-196"},()=> {
                var a=JsonNode.Parse(captured)!;a["tribeData"]!["imperius"]!["startingTech"]=new JsonArray("organization");string before=a.ToJsonString();
                Reject(()=>TechTree.AddDeception(before),"not starting");Assert(before==a.ToJsonString());
            });
            Test("M0-070-unaudited-native-root-override-rejected",new[]{"DEC-001"},()=> {
                var a=JsonNode.Parse(captured)!;a["techData"]!["another_root"]=JsonNode.Parse(a["techData"]!["basic"]!.ToJsonString());
                a["tribeData"]!["imperius"]!["techOverrides"]=new JsonObject{["basic"]="another_root"};
                Reject(()=>TechTree.AddDeception(a.ToJsonString()),"separate audit");
            });
            Test("M0-071-special-city-recruit-without-propaganda",new[]{"DEC-197"},()=> {
                var w=World();var p=w.Players[1];p.OrdinaryTribe=false;p.TribeId="fixture-polaris";p.NativeRosterForms=new(){"mooni"};p.UnlockedRecruitmentForms=new(){"mooni"};
                var r=Rules.RecruitMimic(w,"r",1,"city1","mooni",N);Assert(r.State.Units[r.UnitId!.Value].ActualHp==1 && r.State.Players[1].Stars==19);
            });
            Test("M0-072-propaganda-does-not-grant-foreign-roster",new[]{"DEC-197","DEC-199"},()=> {
                var p=World().Players[1];p.Technologies.Add("propaganda");p.UnlockedRecruitmentForms.Add("centipede");
                Assert(!Rules.CanRecruitAppearance(p,N.Profile("centipede"),N));Assert(!Rules.CanRecruitAppearance(p,N.Profile("mooni"),N));
            });
            Test("A11-003-special-tribe-super-recruitment-excluded",new[]{"A11"},()=> {
                var w=World();var p=w.Players[1];p.OrdinaryTribe=false;p.TribeId="fixture-cymanti";p.NativeRosterForms=new(){"centipede"};p.Technologies.Add("propaganda");
                string before=Codec.Encode(w);Reject(()=>Rules.RecruitMimic(w,"r",1,"city1","centipede",N),"unavailable");
                Assert(Codec.Encode(w)==before && !Rules.CanRecruitAppearance(p,N.Profile("centipede"),N));
            });
            Test("M0-074-segment-never-direct-picker-even-propaganda",new[]{"DEC-198"},()=> {
                var p=World().Players[1];p.NativeRosterForms.Add("segment");p.UnlockedRecruitmentForms.Add("segment");p.Technologies.Add("propaganda");Assert(!Rules.CanRecruitAppearance(p,N.Profile("segment"),N));
            });
            Test("M0-075-new-solo-head-regroup-both-outcomes",new[]{"DEC-181","DEC-091"},()=> BothOutcomes(ExchangeKind.Regroup,swap=> {
                var (w,i,h)=Solo();h.Allowance.EndTurn();string flags=Flags(h);var r=Rules.Exchange(w,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Regroup,swap,N).State;
                Assert(r.Units[i.LogicalId].Appearance.Body=="centipede" && Flags(r.Units[h.LogicalId])==flags && r.SegmentParents.Count==0);
                Assert(r.Units[i.LogicalId].Allowance.ActionSpent);Assert(r.Units[i.LogicalId].Position==(swap?h.Position:i.Position));
            }));
            Test("M0-076-formerly-linked-solo-disguise-both-outcomes",new[]{"DEC-182"},()=> BothOutcomes(ExchangeKind.Disguise,swap=> {
                var (w,i,h)=Solo();var s=AddSegment(w,h.LogicalId);h.Allowance.EndTurn();
                var unlinked=Rules.ReceiveNativeAttachmentSnapshot(w,"unlink",new(),N).State;string flags=Flags(unlinked.Units[h.LogicalId]);
                var r=Rules.Exchange(unlinked,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Disguise,swap,N).State;
                Assert(r.Units[h.LogicalId].Appearance.Body=="warrior" && Flags(r.Units[h.LogicalId])==flags && r.Units.ContainsKey(s.LogicalId));
            }));
            Test("M0-077-linked-head-blocks-regroup-keep-and-swap",new[]{"DEC-183","DEC-091"},()=> BothOutcomes(ExchangeKind.Regroup,swap=> {
                var (w,i,h)=Solo();AddSegment(w,h.LogicalId);string b=Codec.Encode(w);
                Reject(()=>Rules.Exchange(w,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Regroup,swap,N),"standalone");Assert(Codec.Encode(w)==b);
            }));
            Test("M0-078-linked-head-blocks-disguise-keep-and-swap",new[]{"DEC-184","DEC-091"},()=> BothOutcomes(ExchangeKind.Disguise,swap=> {
                var (w,i,h)=Solo();var s=AddSegment(w,h.LogicalId);AddSegment(w,s.LogicalId,x:10);string b=Codec.Encode(w);
                Reject(()=>Rules.Exchange(w,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Disguise,swap,N),"standalone");Assert(Codec.Encode(w)==b);
            }));
            Test("M0-079-attached-segment-never-exchange-participant",new[]{"DEC-185"},()=> {
                foreach(var kind in new[]{ExchangeKind.Regroup,ExchangeKind.Disguise}) foreach(bool swap in new[]{false,true}) {
                    var (w,i,h)=Solo();var s=AddSegment(w,h.LogicalId);s.Position=new Tile(1,1);string b=Codec.Encode(w);
                    Reject(()=>Rules.Exchange(w,"e",1,i.LogicalId,s.LogicalId,kind,swap,N),"standalone");Assert(Codec.Encode(w)==b);
                }
            });
            Test("M0-080-unpromoted-isolated-segment-not-a-head",new[]{"DEC-186"},()=> {
                var w=World();var i=Add(w,ActualKind.Mimic,"mimic","warrior");var s=AddSegment(w);s.Position=new Tile(2,0);s.Allowance.EndTurn();
                Reject(()=>Rules.Exchange(w,"e",1,i.LogicalId,s.LogicalId,ExchangeKind.Regroup,false,N),"standalone");
                string flags=Flags(s);s.ActualBody="centipede";s.Structure=BodyStructure.CentipedeHead;s.Appearance.Body="centipede";s.Appearance.Structure=BodyStructure.CentipedeHead;
                // Synthetic native promotion receipt: not an implementation of promotion.
                var r=Rules.Exchange(w,"e2",1,i.LogicalId,s.LogicalId,ExchangeKind.Regroup,false,N).State;Assert(Flags(r.Units[s.LogicalId])==flags);
            });
            Test("M0-081-illegal-linked-disguise-rejected-not-migrated",new[]{"DEC-187","DEC-200"},()=> {
                var (w,i,h)=Solo();AddSegment(w,h.LogicalId);h.IndependentAppearance=true;h.Appearance.Body="warrior";h.Appearance.Structure=BodyStructure.SingleBody;
                string raw=JsonSerializer.Serialize(w,Codec.Options);Reject(()=>Codec.Decode(raw),"legacy mask");
                Reject(()=>Rules.Exchange(w,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Regroup,false,N));Assert(raw==JsonSerializer.Serialize(w,Codec.Options));
            });
            Test("M0-082-ordinary-real-unit-wearing-head-remains-eligible",new[]{"DEC-188"},()=> {
                var w=World();var i=Add(w,ActualKind.Mimic,"mimic","warrior");var p=Add(w,ActualKind.Genuine,"warrior","centipede",x:2);
                var r=Rules.Exchange(w,"e",1,i.LogicalId,p.LogicalId,ExchangeKind.Regroup,true,N).State;
                Assert(r.Units[i.LogicalId].Appearance.Body=="centipede" && r.Units[p.LogicalId].ActualBody=="warrior");
                Rules.Exchange(w,"e2",1,i.LogicalId,p.LogicalId,ExchangeKind.Disguise,false,N);
            });
            Test("M0-083-solo-actual-head-copies-current-warrior-mask",new[]{"DEC-188","DEC-199"},()=> {
                var (w,i,h)=Solo(true);var r=Rules.Exchange(w,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Regroup,false,N).State;
                Assert(r.Units[i.LogicalId].Appearance.Body=="warrior");
            });
            Test("M0-084-fake-head-cannot-own-native-segments",new[]{"DEC-189"},()=> {
                var w=World();var fake=Add(w,ActualKind.Mimic,"mimic","centipede");var s=AddSegment(w);string b=Codec.Encode(w);
                Reject(()=>Rules.ReceiveNativeAttachmentSnapshot(w,"grow",new(){{s.LogicalId,fake.LogicalId}},N),"endpoint");Assert(Codec.Encode(w)==b);
                var c=CapabilityResolver.Resolve(fake,N);Assert(!c.CanDamage && c.Attack==0 && c.Defence==0 && fake.ActualHp==1);
            });
            Test("M0-085-disguised-warrior-does-not-gain-native-growth",new[]{"DEC-189"},()=> {
                var w=World();var a=Add(w,ActualKind.Genuine,"warrior","centipede");var body=AddSegment(w);
                Reject(()=>Rules.ReceiveNativeAttachmentSnapshot(w,"grow",new(){{body.LogicalId,a.LogicalId}},N),"endpoint");
                var r=Rules.GenuineAttackCommit(w,"attack",1,a.LogicalId,true,Spent(a),N).State;Assert(!r.Units[a.LogicalId].IndependentAppearance && r.Units[a.LogicalId].Appearance.Body=="warrior");
            });
            Test("M0-086-real-attack-reveals-before-genuine-growth",new[]{"DEC-190"},()=> {
                var (w,i,h)=Solo(true);var body=AddSegment(w);var attacked=Rules.GenuineAttackCommit(w,"attack",1,h.LogicalId,true,Spent(h),N).State;
                Assert(!attacked.Units[h.LogicalId].IndependentAppearance && attacked.Units[h.LogicalId].Appearance.Body=="centipede");
                var grown=Rules.ReceiveNativeAttachmentSnapshot(attacked,"grow",new(){{body.LogicalId,h.LogicalId}},N).State;
                Reject(()=>Rules.Exchange(grown,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Regroup,false,N),"standalone");
                Assert(grown.Units.Count==w.Units.Count && grown.Units[h.LogicalId].Allowance.ActionSpent);
            });
            Test("M0-087-nonattack-topology-cleans-mask-only",new[]{"DEC-191"},()=> {
                var (w,i,h)=Solo(true);var s=AddSegment(w);h.Allowance.EndTurn();h.ActualStatuses.Add("frozen");string flags=Flags(h);
                var r=Rules.ReceiveNativeAttachmentSnapshot(w,"grow",new(){{s.LogicalId,h.LogicalId}},N).State;var a=r.Units[h.LogicalId];
                Assert(!a.IndependentAppearance && a.Appearance.Body=="centipede" && a.ActualHp==h.ActualHp && a.PermanentOwner==h.PermanentOwner);
                Assert(Flags(a)==flags && a.HomeCity==h.HomeCity && a.Position==h.Position && a.ActualStatuses.Contains("frozen") && r.Units.Count==w.Units.Count);
                Assert(r.Masks[2].AccessibleTiles.SetEquals(w.Masks[2].AccessibleTiles));
            });
            Test("M0-088-last-link-loss-restores-eligibility-not-actions",new[]{"DEC-192"},()=> {
                var (w,i,h)=Solo();AddSegment(w,h.LogicalId);h.Allowance.EndTurn();h.ActualStatuses.Add("frozen");
                var r=Rules.ReceiveNativeAttachmentSnapshot(w,"unlink",new(),N).State;
                var a=Rules.Exchange(r,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Regroup,false,N).State;
                Assert(a.Units[h.LogicalId].Allowance.ActionSpent && a.Units[h.LogicalId].ActualStatuses.Contains("frozen"));
            });
            Test("M0-089-revalidate-stale-solo-selection-before-commit",new[]{"DEC-193"},()=> {
                foreach(bool swap in new[]{false,true}) {var (w,i,h)=Solo();CompoundBodies.RequireExchangeParticipant(w,h,N);var s=AddSegment(w,h.LogicalId);string b=Codec.Encode(w);
                Reject(()=>Rules.Exchange(w,"stale",1,i.LogicalId,h.LogicalId,ExchangeKind.Regroup,swap,N),"standalone");Assert(b==Codec.Encode(w));}
            });
            Test("M0-090-rejection-no-identity-or-visibility-signal",new[]{"DEC-194"},()=> {
                var (w,i,h)=Solo();AddSegment(w,h.LogicalId);string before=Observation.Capture(w,2),state=Codec.Encode(w);
                for(int n=0;n<3;n++)Reject(()=>Rules.Exchange(w,"bad"+n,1,i.LogicalId,h.LogicalId,ExchangeKind.Disguise,false,N));
                Assert(before==Observation.Capture(w,2) && state==Codec.Encode(w));
            });
            Test("M0-091-nearby-unlinked-body-not-counted",new[]{"DEC-195"},()=> {
                var (w,i,h)=Solo();AddSegment(w,null,x:3);Assert(!CompoundBodies.HasAttachedBodies(w,h.LogicalId));
                Rules.Exchange(w,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Regroup,false,N);
            });
            Test("M0-092-hidden-distant-linked-body-still-counted",new[]{"DEC-195"},()=> {
                var (w,i,h)=Solo();var s=AddSegment(w,h.LogicalId,x:19);s.PermanentOwner=2;s.Rogue=true;s.ReturnOwner=2;s.Commander=null;
                w.Masks[1].DetectedUnits.Remove(s.LogicalId);w.Masks[1].AccessibleTiles.Remove(s.Position.Key);
                Assert(CompoundBodies.HasAttachedBodies(w,h.LogicalId));Reject(()=>Rules.Exchange(w,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Regroup,false,N),"standalone");
            });
            Test("M0-093-owned-foreign-solo-copy-without-propaganda",new[]{"DEC-199"},()=> {
                var (w,i,h)=Solo();w.Players[1].Technologies.Remove("propaganda");Assert(!w.Players[1].Technologies.Contains("propaganda") && !w.Players[1].NativeRosterForms.Contains("centipede"));
                var r=Rules.Exchange(w,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Regroup,false,N).State;
                Assert(r.Units[i.LogicalId].Appearance.Body=="centipede" && !Rules.CanRecruitAppearance(r.Players[1],N.Profile("centipede"),N));
            });
            Test("M0-094-model-topology-rosters-and-flags-roundtrip",new[]{"DEC-200"},()=> {
                var (w,i,h)=Solo();var body=AddSegment(w,h.LogicalId);Add(w,ActualKind.Genuine,"warrior","centipede",x:6);
                w.Players[2].OrdinaryTribe=false;w.Players[2].TribeId="fixture-cymanti";w.Players[2].NativeRosterForms.Add("centipede");
                var loaded=Codec.Decode(Codec.Encode(w));Assert(Codec.Encode(loaded)==Codec.Encode(w));Assert(loaded.SegmentParents[body.LogicalId]==h.LogicalId);
                Assert(Observation.Capture(loaded,1)==Observation.Capture(w,1));Reject(()=>Rules.Exchange(loaded,"e",1,i.LogicalId,h.LogicalId,ExchangeKind.Regroup,false,N));
            });
            Test("M0-095-missing-native-harvest-not-invented",new[]{"DEC-197"},()=> {
                var w=World();w.Players[1].OrdinaryTribe=false;var u=Add(w,ActualKind.Mimic,"mimic","warrior");w.Resources[u.Position.Key]=Resource(u);
                var n=new FixtureNative();n.UnavailableHarvests.Add((1,ResourceKind.Fruit));string b=Codec.Encode(w);
                Reject(()=>Rules.Smuggle(w,"s",1,u.LogicalId,n),"native harvesting");Assert(Codec.Encode(w)==b);
            });
            Test("M0-096-topology-cycle-rejected",new[]{"DEC-193","DEC-200"},()=> {
                var (w,i,h)=Solo();var a=AddSegment(w,h.LogicalId);var b=AddSegment(w,a.LogicalId,x:9);w.SegmentParents[a.LogicalId]=b.LogicalId;Reject(()=>Codec.Encode(w),"Cyclic");
            });
            Test("M0-097-model-v1-not-silently-migrated",new[]{"DEC-200"},()=> {var w=World();w.SchemaVersion=1;Reject(()=>Codec.Encode(w),"schema");});
            Test("M0-098-ineligible-segment-costume-rejected",new[]{"DEC-189"},()=> {var w=World();Add(w,ActualKind.Mimic,"mimic","segment");Reject(()=>Codec.Encode(w),"appearance");});
            Test("M0-099-fake-head-cannot-do-real-attack",new[]{"DEC-189"},()=> {var w=World();var a=Add(w,ActualKind.Mimic,"mimic","centipede");Reject(()=>Rules.GenuineAttackCommit(w,"a",1,a.LogicalId,true,Spent(a),N));});
            Test("M0-100-native-topology-receipt-idempotent",new[]{"DEC-200"},()=> {
                var (w,i,h)=Solo(true);var s=AddSegment(w);var links=new Dictionary<long,long>{{s.LogicalId,h.LogicalId}};
                var a=Rules.ReceiveNativeAttachmentSnapshot(w,"grow",links,N);var b=Rules.ReceiveNativeAttachmentSnapshot(a.State,"grow",links,N);
                Assert(b.Replayed && Codec.Encode(a.State)==Codec.Encode(b.State));
            });
        }
    }
}
