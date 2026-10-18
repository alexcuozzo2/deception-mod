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
        static int reentryAssertions;
        static void ReentryRequire(bool b,string message){reentryAssertions++;if(!b)throw new Exception(message);}
        static void ReentryReject(Action action,string name)
        {try{action();}catch(InvalidDataException){reentryAssertions++;return;}throw new Exception("Accepted "+name);}
        static void NativeRecruitmentRosterReentryTests()
        {
            string native=File.ReadAllText(Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE")!);
            var retained=NativeRecruitmentRosters.AllResearchRosters(native);
            string retainedBefore=JsonSerializer.Serialize(retained);
            string modified=RecruitmentCatalog.Add(TechTree.AddDeception(native).Json).Json;

            void Test(string name,Action action)=>KernelTests.Test(name,new[]{"A11"},action);
            JsonObject Copy()=>JsonNode.Parse(modified)!.AsObject();
            void Check(JsonObject d)=>NativeRecruitmentRosterReentry.Validate(d.ToJsonString(),retained);
            Test("A11-RR01 actual modified registry matches retained native rosters",()=>{
                RecruitmentCatalog.ValidateReentry(modified);
                NativeRecruitmentRosterReentry.Validate(modified,retained);reentryAssertions++;
                ReentryRequire(JsonSerializer.Serialize(retained)==retainedBefore,"Retained values changed");
                ReentryRequire(modified==RecruitmentCatalog.Add(TechTree.AddDeception(native).Json).Json,"Input representation changed");
            });
            Test("A11-RR02 reachable tribe override passes old validation but fails retained comparison",()=>{
                var d=Copy();d["tribeData"]!["imperius"]!["unitOverrides"]=new JsonObject{["rider"]="hexapod"};
                RecruitmentCatalog.ValidateReentry(d.ToJsonString());reentryAssertions++;
                ReentryReject(()=>Check(d),"foreign Rider replacement");
                ReentryRequire(JsonSerializer.Serialize(retained)==retainedBefore,"Mismatch replaced retained roster");
            });
            Test("A11-RR03 reachable unit and technology unlock changes reject",()=>{
                var d=Copy();d["techData"]!["riding"]!["unitUnlocks"]=new JsonArray();ReentryReject(()=>Check(d),"removed Rider entitlement");
                d=Copy();d["techData"]!["mathematics"]!["unitUnlocks"]!.AsArray().Add("ciru");ReentryReject(()=>Check(d),"new Ciru entitlement");
                d=Copy();d["techData"]!["forestry"]!["techUnlocks"]=new JsonArray();ReentryReject(()=>Check(d),"removed Mathematics branch");
                d=Copy();d["tribeData"]!["polaris"]!["techOverrides"]!["aquaculture"]="aquaculture";ReentryReject(()=>Check(d),"changed Polaris descendants");
            });
            Test("A11-RR04 hidden independent and unique policy changes reject",()=>{
                foreach(string field in new[]{"hidden","independent","unique"})
                {
                    var d=Copy();if(field=="hidden")d["unitData"]!["rider"]!["hidden"]=true;
                    else d["unitData"]!["rider"]!["unitAbilities"]!.AsArray().Add(field);
                    ReentryReject(()=>Check(d),field+" policy changed");
                }
            });
            Test("A11-RR05 native object-property order and registered custom indices are irrelevant",()=>{
                JsonNode? Reverse(JsonNode? n)
                {
                    if(n is JsonObject o){var r=new JsonObject();foreach(var p in o.Reverse())r[p.Key]=Reverse(p.Value);return r;}
                    if(n is JsonArray a){var r=new JsonArray();foreach(var p in a)r.Add(Reverse(p));return r;}
                    return n is null?null:JsonNode.Parse(n.ToJsonString());
                }
                var d=Reverse(Copy())!.AsObject();
                foreach(var n in TechTree.Branch)d["techData"]![n.Id]!["idx"]=1200+n.Tier;
                foreach(var p in d["unitData"]!.AsObject().Where(x=>RecruitmentCatalog.IsMimicId(x.Key)))p.Value!["idx"]=1500;
                Check(d);reentryAssertions++;ReentryRequire(JsonSerializer.Serialize(retained)==retainedBefore,"Order comparison changed retained values");
            });
            Test("A11-RR06 only one exact owned Basic edge may be stripped",()=>{
                var d=Copy();var children=d["techData"]!["basic"]!["techUnlocks"]!.AsArray();
                children.Add(TechTree.DeceptionId);ReentryReject(()=>Check(d),"duplicate edge");
                d=Copy();children=d["techData"]!["basic"]!["techUnlocks"]!.AsArray();
                children.Remove(children.Single(x=>x!.GetValue<string>()==TechTree.DeceptionId));ReentryReject(()=>Check(d),"missing edge");
                d=Copy();d["techData"]!["basic"]!["techUnlocks"]!.AsArray().Add(TechTree.PropagandaId);ReentryReject(()=>Check(d),"another reachable own branch");
                ReentryReject(()=>NativeRecruitmentRosterReentry.Validate(modified,null),"missing retained state");
            });
            Test("A11-RR07 compare every retained row and field, not just selectable set",()=>{
                var original=retained["imperius"];
                var variants=new[]{
                    new NativeAppearanceRoster("bardur",original.Supported,original.Deferred,original.EffectiveTechnologies,original.RequiresUniqueCheck,original.Excluded),
                    new NativeAppearanceRoster(original.Tribe,original.Supported.Skip(1),original.Deferred,original.EffectiveTechnologies,original.RequiresUniqueCheck,original.Excluded),
                    new NativeAppearanceRoster(original.Tribe,original.Supported,new[]{"cloak"},original.EffectiveTechnologies,original.RequiresUniqueCheck,original.Excluded),
                    new NativeAppearanceRoster(original.Tribe,original.Supported,original.Deferred,original.EffectiveTechnologies.Skip(1),original.RequiresUniqueCheck,original.Excluded),
                    new NativeAppearanceRoster(original.Tribe,original.Supported,original.Deferred,original.EffectiveTechnologies,new[]{"rider"},original.Excluded),
                    new NativeAppearanceRoster(original.Tribe,original.Supported,original.Deferred,original.EffectiveTechnologies,original.RequiresUniqueCheck,
                        original.Excluded.Select((x,i)=>i==0?x with{Reason="different-reason"}:x))};
                foreach(var variant in variants)
                {
                    var changed=new Dictionary<string,NativeAppearanceRoster>(retained,StringComparer.Ordinal){["imperius"]=variant};
                    ReentryReject(()=>NativeRecruitmentRosterReentry.Validate(modified,changed),"retained field differs");
                }
                var keys=new Dictionary<string,NativeAppearanceRoster>(retained,StringComparer.Ordinal);keys.Remove("imperius");keys.Add("foreign",original);
                ReentryReject(()=>NativeRecruitmentRosterReentry.Validate(modified,keys),"retained tribe key differs");
            });
            Test("A11-RR08 comparison failure leaves input and retained values untouched",()=>{
                var d=Copy();d["unitData"]!["warrior"]!["hidden"]=true;string before=d.ToJsonString();ReentryReject(()=>Check(d),"hidden Warrior");
                ReentryRequire(d.ToJsonString()==before,"Input JSON nodes changed");ReentryRequire(JsonSerializer.Serialize(retained)==retainedBefore,"Retained values changed");
                NativeRecruitmentRosterReentry.Validate(modified,retained);reentryAssertions++;
            });
        }
    }
}
