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
        static int rosterAssertions;
        static void RosterRequire(bool value,string message){rosterAssertions++;if(!value)throw new Exception(message);}
        static void RosterEqual(IEnumerable<string> actual,string expected,string name)=>RosterRequire(actual.SequenceEqual(
            expected.Split(',',StringSplitOptions.RemoveEmptyEntries).OrderBy(x=>x,StringComparer.Ordinal)),name+": "+string.Join(",",actual));
        static void RosterReject(Action action,string name)
        {
            try{action();}catch(InvalidDataException){rosterAssertions++;return;}
            catch(JsonException){rosterAssertions++;return;}
            throw new Exception("Accepted invalid registry: "+name);
        }
        static void NativeRecruitmentRosterTests()
        {
            string input=File.ReadAllText(Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE")!);
            JsonObject Copy()=>JsonNode.Parse(input)!.AsObject();

            void Test(string name,Action body)=>KernelTests.Test(name,new[]{"A11"},body);
            IReadOnlyDictionary<string,NativeAppearanceRoster> R(JsonObject d)=>NativeRecruitmentRosters.AllResearchRosters(d.ToJsonString());
            string Fingerprint(IReadOnlyDictionary<string,NativeAppearanceRoster> d)=>JsonSerializer.Serialize(d);
            var baseline=NativeRecruitmentRosters.AllResearchRosters(input);
            const string ordinary="warrior,rider,knight,defender,archer,catapult,mindbender,swordsman,cloak,dcp_spy,dcp_motherfogger";
            Test("A11-R01 captured 16-tribe golden supported and deferred rosters",()=>{
                RosterRequire(baseline.Count==16,"all playable tribes");
                foreach(var row in baseline.Values)
                {
                    switch(row.Tribe)
                    {
                        case "aquarion":
                            RosterEqual(row.Supported,"amphibian,tridention,mermaidwarrior,mermaidarcher,mermaidswordsman,mermaiddefender,siren,mermaidcloak,shark,jelly,aquapult,dcp_spymaid,dcp_motherfogger","Aquarion");
                            RosterEqual(row.Deferred,"","Aquarion deferred");break;
                        case "polaris":
                            RosterEqual(row.Supported,"warrior,rider,knight,defender,catapult,mindbender,swordsman,battlesled,icefortress,icearcher,cloak,mooni,dcp_spy,dcp_motherfogger","Polaris");
                            RosterEqual(row.Deferred,"","Polaris deferred");break;
                        case "cymanti":
                            RosterEqual(row.Supported,"warrior,hexapod,kiton,shaman,phychi,mantis,exida,doomux,raychi,island,moth,dcp_spy,dcp_motherfogger","Cymanti");
                            RosterEqual(row.Deferred,"","Cymanti deferred");break;
                        default:RosterEqual(row.Supported,ordinary,row.Tribe);RosterEqual(row.Deferred,"",row.Tribe+" deferred");break;
                    }
                }
            });
            Test("CLOAK-002-tribe-rosters-and-research-gate-initial-choices",()=>{
                var forms=RecruitmentCatalog.Add(TechTree.AddDeception(input).Json).Forms;
                foreach(var row in baseline.Values) {
                    var allResearch=row.Supported.ToHashSet(StringComparer.Ordinal);
                    string expected=row.Tribe=="aquarion"?"mermaidcloak":"cloak";
                    bool nativeCloak=row.Tribe!="cymanti";
                    RosterRequire(RecruitmentEligibility.Permitted(true,expected,allResearch)==nativeCloak,"Native tribe Cloak entitlement: "+row.Tribe);
                    RosterRequire(!RecruitmentEligibility.Permitted(false,expected,allResearch),"Deception remains required: "+row.Tribe);
                    RosterRequire(!RecruitmentEligibility.Permitted(true,expected,new HashSet<string>(StringComparer.Ordinal){"warrior","mermaidwarrior"}),"Unresearched Cloak offered without Propaganda: "+row.Tribe);
                    var choices=RecruitmentEligibility.Choices(forms,allResearch).Select(f=>f.NativeId).ToArray();
                    RosterRequire(choices.Contains(expected)==nativeCloak,"All-research Propaganda choices lost native replacement: "+row.Tribe);
                    RosterRequire(!choices.Contains(expected=="cloak"?"mermaidcloak":"cloak"),"Foreign tribe Cloak offered: "+row.Tribe);
                }
                RosterRequire(baseline["cymanti"].Supported.Contains("moth"),"Native Moth replacement is available through its own tribe");
                var removed=Copy();removed["techData"]!["diplomacy"]!["unitUnlocks"]=new JsonArray();
                RosterRequire(R(removed).Values.All(row=>!row.Supported.Intersect(new[]{"cloak","mermaidcloak"}).Any()),"Registration alone granted Cloak recruitment after native unlock was removed");
            });
            Test("A11-R02 effective special tree edges replace original edges",()=>{
                var a=baseline["aquarion"].EffectiveTechnologies;
                RosterRequire(a.Contains("aquafishing")&&a.Contains("marinelife")&&a.Contains("spearing"),"Aquarion replacement branches");
                RosterRequire(!a.Contains("fishing")&&!a.Contains("sailing")&&!a.Contains("chivalry")&&!a.Contains("riding2"),"Aquarion originals/orphans absent");
                var p=baseline["polaris"].EffectiveTechnologies;
                RosterRequire(p.Contains("frostwork")&&p.Contains("sledding")&&p.Contains("polarwarfare"),"Polaris reachable replacement descendants");
                var c=baseline["cymanti"].EffectiveTechnologies;
                RosterRequire(c.Contains("rituals")&&c.Contains("mysticism")&&!c.Contains("philosophy")&&!c.Contains("hydrology"),"Cymanti effective traversal");
            });
            Test("CITY-0361-002-tribe-and-live-roster-gates-remain-separate",()=>{
                var forms=RecruitmentCatalog.Add(TechTree.AddDeception(input).Json).Forms;
                foreach(var row in baseline.Values) {
                    var all=row.Supported.ToHashSet(StringComparer.Ordinal);string spy=row.Tribe=="aquarion"?SpyCatalog.AquarionId:SpyCatalog.Id;
                    RosterRequire(all.Contains(spy),"All-research custom Spy missing: "+row.Tribe);
                    foreach(string form in new[]{"raychi","jelly","shark","aquapult","island"}) {
                        bool allowed=form is "raychi" or "island"?row.Tribe=="cymanti":row.Tribe=="aquarion";
                        RosterRequire(RecruitmentEligibility.Permitted(true,form,all)==allowed,"Tribe-specific water form: "+row.Tribe+"/"+form);
                        RosterRequire(!RecruitmentEligibility.Permitted(false,form,all),"Deception required: "+form);
                        RosterRequire(!RecruitmentEligibility.Permitted(true,form,new HashSet<string>{"warrior"}),"Live roster is not replaced by registration: "+form);
                    }
                    RosterRequire(!RecruitmentEligibility.Permitted(true,SpyCatalog.Id,new HashSet<string>{"warrior"}),"Unresearched Spy does not follow from a Warrior unlock");
                    RosterRequire(RecruitmentEligibility.Choices(forms,all).Any(f=>f.NativeId==spy),"Propaganda roster omitted Spy");
                    RosterRequire(all.Contains("island")==(row.Tribe=="cymanti"),"Living Island follows the Cymanti native override");
                }
                var unlocked=Copy();unlocked["techData"]!["riding"]!["unitUnlocks"]!.AsArray().Add("island");
                RosterRequire(R(unlocked)["imperius"].Supported.Contains("island"),"A real native city unlock should enable a reviewed Island form");
            });
            Test("A11-R03 registered or starting-only forms do not imply recruitment",()=>{
                RosterRequire(RecruitmentCatalog.Supported.Contains("ciru"),"Ciru registered in reviewed catalog");
                RosterRequire(baseline.Values.All(x=>!x.Supported.Contains("ciru")),"Ciru has no reachable native unlock");
                var d=Copy();d["tribeData"]!["cymanti"]!["startingUnit"]="ciru";
                d["tribeData"]!["cymanti"]!["startingTech"]!.AsArray().Add("pescetism");
                RosterRequire(Fingerprint(R(d))==Fingerprint(baseline),"Starting advantages do not union orphan tech into all-research graph");
            });
            Test("A11-R04 native override is one lookup rather than transitive",()=>{
                var d=Copy();d["tribeData"]!["aquarion"]!["unitOverrides"]!["amphibian"]="hexapod";
                var map=d["tribeData"]!["imperius"]!["techOverrides"] as JsonObject;
                if(map is null)d["tribeData"]!["imperius"]!["techOverrides"]=map=new JsonObject();
                map["hunting"]="forestmagic";map["forestmagic"]="aquabase";
                var r=R(d);RosterRequire(r["aquarion"].Supported.Contains("amphibian")&&!r["aquarion"].Supported.Contains("hexapod"),"Unit override once");
                RosterRequire(r["imperius"].EffectiveTechnologies.Contains("forestmagic")&&!r["imperius"].EffectiveTechnologies.Contains("aquabase"),"Tech override once");
            });
            Test("A11-R05 hidden filter applies after tribe override",()=>{
                var d=Copy();d["unitData"]!["warrior"]!["hidden"]=true;
                var r=R(d);RosterRequire(!r["imperius"].Supported.Contains("warrior")&&r["aquarion"].Supported.Contains("mermaidwarrior"),"Visible replacement of hidden source survives");
                var expanded=NativeRecruitmentRosters.AllResearchRosters(input,RecruitmentCatalog.Supported,RecruitmentCatalog.Deferred);
                RosterRequire(expanded["aquarion"].Supported.Contains("jelly"),"Native hidden Bomber replacement is visible; policy overload explicitly reviewed for test");
            });
            Test("A11-R06 independent source restrictions remain stronger than allowlist",()=>{
                foreach(bool flag in new[]{false,true})
                {
                    var d=Copy();if(flag)d["unitData"]!["rider"]!["independent"]=true;
                    else d["unitData"]!["rider"]!["unitAbilities"]!.AsArray().Add("independent");
                    var row=R(d)["imperius"];RosterRequire(!row.Supported.Contains("rider")&&row.Excluded.Any(x=>x.Form=="rider"&&x.Reason=="independent"),"Independent rejection");
                }
            });
            Test("A11-R07 supers and spawned-grown forms cannot become initial choices",()=>{
                string[] forbidden={"giant","crab","dragonegg","gaami","centipede","babydragon","firedragon","dagger","segment","bugegg","larva"};
                var d=Copy();foreach(string id in forbidden){d["unitData"]![id]!["hidden"]=false;d["techData"]!["basic"]!["unitUnlocks"]!.AsArray().Add(id);}
                var r=NativeRecruitmentRosters.AllResearchRosters(d.ToJsonString(),RecruitmentCatalog.Supported.Concat(forbidden),RecruitmentCatalog.Deferred);
                foreach(var row in r.Values)RosterRequire(!row.Supported.Intersect(forbidden).Any(),"Forbidden initial form for "+row.Tribe);
                foreach(string tribe in new[]{"imperius","aquarion","elyrion","polaris","cymanti"})RosterRequire(r[tribe].Excluded.Any(x=>x.Reason=="city-super-unit"),"Super explicitly classified "+tribe);
            });
            Test("A11-R08 direct ship and carrier exclusions do not ban amphibious or static units",()=>{
                var d=Copy();d["unitData"]!["transportship"]!["hidden"]=false;
                var r=NativeRecruitmentRosters.AllResearchRosters(d.ToJsonString(),RecruitmentCatalog.Supported.Append("transportship"),RecruitmentCatalog.Deferred);
                RosterRequire(r.Values.All(x=>!x.Supported.Contains("transportship")),"Direct ships forbidden");
                RosterRequire(r["polaris"].Supported.Contains("icefortress")&&r["cymanti"].Supported.Contains("doomux"),"Static does not mean super");
                RosterRequire(r["aquarion"].Supported.Contains("amphibian")&&r["aquarion"].Supported.Contains("aquapult"),"Amphibious/water abilities are not a blanket exclusion");
            });
            Test("A11-R09 unknown reachable ordinary forms are explicit coverage gaps",()=>{
                var d=Copy();d["unitData"]!["nativefixture"]=JsonNode.Parse(d["unitData"]!["warrior"]!.ToJsonString());
                d["techData"]!["basic"]!["unitUnlocks"]!.AsArray().Add("nativefixture");
                var row=R(d)["imperius"];RosterRequire(!row.Supported.Contains("nativefixture")&&!row.Deferred.Contains("nativefixture"),"Unknown not auto-authorized");
                RosterRequire(row.Excluded.Any(x=>x.Form=="nativefixture"&&x.Reason=="outside-reviewed-city-catalog"),"Unknown gap reported");
                d=Copy();d["techData"]!["riding"]!["unitUnlocks"]!.AsArray().Add("ciru");
                RosterRequire(R(d)["imperius"].Supported.Contains("ciru"),"A genuine reachable allowed unlock changes the result");
            });
            Test("A11-R10 unique needs a separate runtime board check",()=>{
                RosterRequire(baseline.Values.All(x=>x.RequiresUniqueCheck.Count==0),"No current eligible Unique in capture");
                var d=Copy();d["unitData"]!["knight"]!["unitAbilities"]!.AsArray().Add("unique");
                var row=R(d)["imperius"];RosterRequire(row.Supported.Contains("knight")&&row.RequiresUniqueCheck.SequenceEqual(new[]{"knight"}),"No invented current board uniqueness result");
            });
            Test("A11-R11 duplicate edges and finite cycles cannot duplicate choices",()=>{
                var d=Copy();d["techData"]!["basic"]!["techUnlocks"]!.AsArray().Add("hunting");
                d["techData"]!["farming"]!["techUnlocks"]!.AsArray().Add("basic");
                d["techData"]!["basic"]!["unitUnlocks"]!.AsArray().Add("warrior");
                RosterRequire(Fingerprint(R(d))==Fingerprint(baseline),"Visited effective graph and choice sets");
            });
            Test("A11-R12 malformed references flags arrays and duplicate JSON keys fail closed",()=>{
                foreach(Action<JsonObject> mutate in new Action<JsonObject>[] {
                    d=>d["techData"]!["basic"]!["techUnlocks"]!.AsArray().Add("absent"),
                    d=>d["unitData"]!.AsObject().Remove("warrior"),
                    d=>d["tribeData"]!["aquarion"]!["unitOverrides"]!["rider"]="absent",
                    d=>d["tribeData"]!["aquarion"]!["techOverrides"]!["fishing"]="absent",
                    d=>d["unitData"]!["warrior"]!["hidden"]="false",
                    d=>d["techData"]!["basic"]!["unitUnlocks"]=null,
                    d=>d["tribeData"]!["imperius"]!["category"]=3,
                    d=>d["techData"]!["basic"]!["idx"]=99})
                {var d=Copy();mutate(d);RosterReject(()=>R(d),"malformed field");}
                RosterReject(()=>NativeRecruitmentRosters.AllResearchRosters(input.Replace("\"tribeData\":", "\"tribeData\":{},\"tribeData\":")),"duplicate root key");
            });
            Test("A11-R13 catalog and captured identity mismatches fail closed",()=>{
                RosterReject(()=>NativeRecruitmentRosters.AllResearchRosters(input,new[]{"warrior"},new[]{"warrior"}),"policy overlap");
                RosterReject(()=>NativeRecruitmentRosters.AllResearchRosters(input,new[]{"Warrior"},Array.Empty<string>()),"case mismatch");
                var d=Copy();d["tribeData"]!.AsObject().Remove("polaris");RosterReject(()=>R(d),"missing tribe");
                d=Copy();d["techData"]!["basic"]!["techUnlocks"]!.AsArray().Add("dcp_deception");
                RosterReject(()=>R(d),"use captured native input before adding Deception");
            });
            Test("A11-R14 dictionary ordering and independent calls preserve canonical immutable results",()=>{
                JsonNode? Reverse(JsonNode? n)
                {
                    if(n is JsonObject o){var q=new JsonObject();foreach(var p in o.Reverse())q[p.Key]=Reverse(p.Value);return q;}
                    if(n is JsonArray a){var q=new JsonArray();foreach(var v in a)q.Add(Reverse(v));return q;}
                    return n is null?null:JsonNode.Parse(n.ToJsonString());
                }
                RosterRequire(Fingerprint(R(Reverse(Copy())!.AsObject()))==Fingerprint(baseline),"Object insertion order independence");
                try{((IDictionary<string,NativeAppearanceRoster>)baseline).Clear();throw new Exception("Mutable dictionary");}catch(NotSupportedException){rosterAssertions++;}
                try{((IList<string>)baseline["imperius"].Supported)[0]="rider";throw new Exception("Mutable roster");}catch(NotSupportedException){rosterAssertions++;}
                RosterRequire(Fingerprint(NativeRecruitmentRosters.AllResearchRosters(input))==Fingerprint(baseline),"Independent defensive result");
                RosterRequire(File.ReadAllText(Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE")!)==input,"Capture unchanged");
            });
        }
    }
}
