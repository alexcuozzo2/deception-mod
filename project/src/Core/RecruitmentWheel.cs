using System;
using System.Collections.Generic;
using System.Linq;

namespace Deception.Core
{
    public enum RecruitmentStage { Opening, Appearances, Choosing, Returning, Dismissed }
    public readonly record struct RecruitmentContext(long Game,long Player,int PlayerId,uint Turn,int CityX,int CityY);
    // Only UI state lives here. There is intentionally no currency debit, unit spawn,
    // research grant or gameplay command in this controller.
    public sealed class RecruitmentWheel
    {
        public RecruitmentContext Context { get; }
        public RecruitmentStage Stage { get; private set; }=RecruitmentStage.Opening;
        public string? Selected { get; private set; }
        public string[] Choices { get; private set; }=Array.Empty<string>();
        public RecruitmentWheel(RecruitmentContext context){Context=context;}
        public bool Matches(RecruitmentContext context)=>Stage!=RecruitmentStage.Dismissed&&Context==context;
        public bool Show(RecruitmentContext context,IEnumerable<string> choices)
        {
            if(!Matches(context)||Stage!=RecruitmentStage.Opening)return false;
            Choices=choices.Distinct(StringComparer.Ordinal).ToArray();
            if(Choices.Length==0){Dismiss();return false;}
            Stage=RecruitmentStage.Appearances;return true;
        }
        public bool Choose(RecruitmentContext context,string variant,ISet<string> currentlyEligible)
        {
            if(!Matches(context)||Stage!=RecruitmentStage.Appearances||!Choices.Contains(variant,StringComparer.Ordinal)||!currentlyEligible.Contains(variant))return false;
            Selected=variant;Stage=RecruitmentStage.Choosing;return true;
        }
        public bool Back(RecruitmentContext context)
        {
            if(!Matches(context)||Stage!=RecruitmentStage.Appearances)return false;
            Selected=null;Stage=RecruitmentStage.Returning;return true;
        }
        public void Dismiss(){Stage=RecruitmentStage.Dismissed;Selected=null;Choices=Array.Empty<string>();}
    }
    public static class RecruitmentEligibility
    {
        public static bool Permitted(bool ownsDeception,string source,ISet<string> nativeRoster)
            =>ownsDeception&&!RecruitmentCatalog.IsMimicId(source)&&!RecruitmentCatalog.FieldCopyOnly.Contains(source,StringComparer.Ordinal)&&!RecruitmentCatalog.NavalCopyOnly.Contains(source,StringComparer.Ordinal)&&nativeRoster.Contains(source);
        public static RecruitmentForm[] Choices(IEnumerable<RecruitmentForm> supported,ISet<string> nativeRoster)
            =>supported.Where(f=>Permitted(true,f.NativeId,nativeRoster))
                .OrderBy(f=>f.NativeId=="warrior"||f.NativeId=="mermaidwarrior"?0:1)
                .ThenBy(f=>f.Label,StringComparer.Ordinal).ToArray();
    }
}
