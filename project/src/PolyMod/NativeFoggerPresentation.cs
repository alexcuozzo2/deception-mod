using System;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Classification here is presentation-only. Fogger is never a Spy:
    // native stealth, Subvert and action budgets use their separate catalogs.
    internal static class NativeFoggerPresentation
    {
        internal static bool IsForm(string? form)=>form==FoggerCatalog.Id;
        internal static bool IsFogger(MimicProjection p)=>!p.ActualIsMimic&&IsForm(p.ActualNativeId);
        internal static bool ApparentBadge(MimicProjection p)=>IsForm(p.NativeId)||IsForm(p.Appearance?.Passenger?.Form);
        internal static bool CustomForm(string form)=>SpyCatalog.IsSpyId(form)||IsForm(form);
        internal static bool CustomActual(MimicProjection p)=>NativeSpyPresentation.IsSpy(p)||IsFogger(p);
        internal static string ApparentBadgeForm(MimicProjection p)=>CustomForm(p.NativeId)?p.NativeId:p.Appearance?.Passenger?.Form??p.NativeId;
        internal static bool CustomBadge(MimicProjection p)=>NativeSpyPresentation.ApparentSpyBadge(p)||ApparentBadge(p);
        internal static void Install(Harmony h)=>Native.Hook(h,Native.M("UnitPopup","set_UnitData","Polytopia.Data.UnitData"),typeof(NativeFoggerPresentation),null,nameof(PopupPopulated));
        public static void PopupPopulated(object __instance)
        {
            try
            {
                var data=Native.Get(__instance,"data");
                if(data is null||!IsForm(Native.EnumName(Native.Get(data,"type")!)))return;
                // Definition popups have no live-unit projection owner. Match the
                // fitted enlargement used for the map unit's actual/apparent info.
                if(Native.Get(__instance,"unit") is null)
                {
                    var renderer=Native.Get(__instance,"unitRenderer");var player=Native.Get(__instance,"player");
                    var parent=renderer is null?null:Native.Get(Native.Get(renderer,"rectTransform")!,"parent");
                    if(Native.UnityAlive(renderer)&&player is not null&&Native.UnityAlive(parent))
                        MimicControls.FitSpyPopup(data,player,renderer!,parent!);
                }
                MimicAbilityTags.ShowFogger(__instance,()=>Native.UnityAlive(__instance)&&Native.Same(Native.Get(__instance,"data"),data));
            }
            catch(Exception e){Entry.Note("fogger-ability-tag-error",e.ToString());}
        }
    }
}
