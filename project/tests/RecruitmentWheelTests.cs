using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        static void RecruitmentWheelTests()
        {
            var ctx=new RecruitmentContext(100,200,1,7,4,6);
            var w=RecruitmentCatalog.Variant("warrior");var r=RecruitmentCatalog.Variant("rider");
            HashSet<string> Set(params string[] ids)=>new(ids,StringComparer.Ordinal);
            RecruitmentWheel Open(params string[] choices){var x=new RecruitmentWheel(ctx);Assert(x.Show(ctx,choices));return x;}
            void T(string id,Action action)=>Test(id,new[]{"UI-A02","DEC-003","DEC-197"},action);
            T("MW-001-no-automatic-purchase-opening",()=>{var x=new RecruitmentWheel(ctx);Assert(x.Stage==RecruitmentStage.Opening&&x.Selected is null);});
            T("MW-002-one-choice-still-opens-wheel",()=>{var x=Open(w);Assert(x.Stage==RecruitmentStage.Appearances&&x.Choices.SequenceEqual(new[]{w})&&x.Selected is null);});
            T("MW-003-two-choices-direct-selection",()=>{var x=Open(w,r);Assert(x.Choose(ctx,r,Set(w,r))&&x.Selected==r&&x.Stage==RecruitmentStage.Choosing);});
            T("MW-004-empty-roster-closes-without-selection",()=>{var x=new RecruitmentWheel(ctx);Assert(!x.Show(ctx,Array.Empty<string>())&&x.Stage==RecruitmentStage.Dismissed&&x.Selected is null);});
            T("MW-005-duplicate-click-no-second-command",()=>{var x=Open(w);Assert(x.Choose(ctx,w,Set(w)));Assert(!x.Choose(ctx,w,Set(w)));});
            T("MW-006-stale-city-cannot-select",()=>{var x=Open(w);Assert(!x.Choose(ctx with{CityX=5},w,Set(w))&&x.Selected is null);});
            T("MW-007-stale-turn-cannot-select",()=>{var x=Open(w);Assert(!x.Choose(ctx with{Turn=8},w,Set(w)));});
            T("MW-008-stale-game-cannot-select",()=>{var x=Open(w);Assert(!x.Choose(ctx with{Game=101},w,Set(w)));});
            T("MW-009-hotseat-player-change-cannot-select",()=>{var x=Open(w);Assert(!x.Choose(ctx with{Player=201,PlayerId=2},w,Set(w)));});
            T("MW-010-roster-revalidated-at-click",()=>{var x=Open(w,r);Assert(!x.Choose(ctx,r,Set(w))&&x.Selected is null);});
            T("MW-011-injected-nonvisible-choice-refused",()=>{var x=Open(w);Assert(!x.Choose(ctx,r,Set(w,r)));});
            T("MW-012-back-no-choice-and-no-command",()=>{var x=Open(w,r);Assert(x.Back(ctx)&&x.Selected is null&&x.Stage==RecruitmentStage.Returning);});
            T("MW-013-stale-back-refused",()=>{var x=Open(w);Assert(!x.Back(ctx with{CityY=7}));});
            T("MW-014-click-after-back-refused",()=>{var x=Open(w);Assert(x.Back(ctx));Assert(!x.Choose(ctx,w,Set(w)));});
            T("MW-015-back-after-selection-refused",()=>{var x=Open(w);Assert(x.Choose(ctx,w,Set(w)));Assert(!x.Back(ctx));});
            T("MW-016-cancel-removes-private-ui-state",()=>{var x=Open(w,r);x.Dismiss();Assert(x.Choices.Length==0&&x.Selected is null&&!x.Matches(ctx));});
            T("MW-017-click-after-cancel-refused",()=>{var x=Open(w);x.Dismiss();Assert(!x.Choose(ctx,w,Set(w)));});
            T("MW-018-old-session-cannot-reopen",()=>{var x=Open(w);x.Dismiss();Assert(!x.Show(ctx,new[]{w}));});
            T("MW-019-opening-context-revalidated",()=>{var x=new RecruitmentWheel(ctx);Assert(!x.Show(ctx with{Turn=8},new[]{w}));});
            T("MW-020-duplicate-forms-deduplicated",()=>{var x=Open(w,w,r);Assert(x.Choices.SequenceEqual(new[]{w,r}));});
            T("MW-021-deception-plus-own-roster-required",()=>{Assert(RecruitmentEligibility.Permitted(true,"warrior",Set("warrior")));Assert(!RecruitmentEligibility.Permitted(false,"warrior",Set("warrior")));});
            T("MW-022-foreign-roster-not-unlocked-by-deception",()=>{Assert(!RecruitmentEligibility.Permitted(true,"hexapod",Set("warrior","rider")));});
            T("MW-023-no-appearance-self-unlock",()=>{Assert(!RecruitmentEligibility.Permitted(true,w,Set(w)));});
            T("MW-024-back-does-not-unlock-extra-forms",()=>{var x=Open(w);Assert(x.Back(ctx));var y=Open(w);Assert(y.Choices.SequenceEqual(new[]{w}));});
            string input=File.ReadAllText(Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE")??"tests/native-tree.captured.json");
            RecruitmentMutation Make()=>RecruitmentCatalog.Add(TechTree.AddDeception(input).Json);
            T("MW-025-ordinary-basic-roster-warrior-only",()=>{var f=RecruitmentEligibility.Choices(Make().Forms,Set("warrior"));Assert(f.Length==1&&f[0].NativeId=="warrior");});
            T("MW-026-riding-unlocks-rider-not-knight",()=>{var f=RecruitmentEligibility.Choices(Make().Forms,Set("warrior","rider"));Assert(f.Select(x=>x.NativeId).SequenceEqual(new[]{"warrior","rider"}));});
            T("MW-027-tribe-overrides-preserved-in-choices",()=>{var f=RecruitmentEligibility.Choices(Make().Forms,Set("mermaidwarrior","amphibian"));Assert(f.Length==2&&f[0].NativeId=="mermaidwarrior"&&!f.Any(x=>x.NativeId=="warrior"));});
            T("MW-028-concrete-types-native-registered",()=>{var m=Make();var a=JsonNode.Parse(m.Json)!;var ids=a["techData"]![TechTree.DeceptionId]!["unitUnlocks"]!.AsArray().Select(n=>n!.GetValue<string>()).ToHashSet();Assert(m.Forms.All(f=>ids.Contains(f.VariantId)));});
            T("MW-029-native-training-visibility-not-ui-visibility",()=>{var m=Make();var a=JsonNode.Parse(m.Json)!;Assert(m.Forms.All(f=>!a["unitData"]![f.VariantId]!["hidden"]!.GetValue<bool>()));});
            T("MW-030-registry-cannot-self-grant-original-tech",()=>{var a=JsonNode.Parse(Make().Json)!;var b=JsonNode.Parse(input)!;AquarionCatalog.RemoveValidatedOverrides(a.AsObject());Assert(TechTree.Canonical(a["tribeData"])==TechTree.Canonical(b["tribeData"]));Assert(a["techData"]![TechTree.DeceptionId]!["techUnlocks"]!.AsArray().Count==2);});
        }
    }
}
