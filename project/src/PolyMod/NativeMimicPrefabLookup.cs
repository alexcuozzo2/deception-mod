using System;
using HarmonyLib;
using UnitType=Polytopia.Data.UnitData.Type;

namespace Deception.PolyModAdapter
{
    // PolyMod's prefab aliases parse TitleCase strings as compiled enums, so
    // e.g. "firedragon" becomes missing "Firedragon" instead of FireDragon.
    // Native prefab keys also combine type + tribe*1000 + skin*1000000, which
    // cannot safely represent our save-compatible custom type IDs above1000.
    // Resolve only the presentation lookup argument to the genuine native
    // form. UnitState/UnitData retain their actual custom type and identity.
    internal static class NativeMimicPrefabLookup
    {
        internal static void Install(Harmony harmony)
        {
            var lookup=Native.M("PrefabManager","GetPrefab",RecruitmentHooks.UT,
                "PolytopiaBackendBase.Common.TribeType","PolytopiaBackendBase.Common.SkinType");
            Native.RequireReturn(lookup,Native.T("Unit"));
            Native.Hook(harmony,lookup,typeof(NativeMimicPrefabLookup),nameof(BeforeLookup));
        }
        public static void BeforeLookup(ref UnitType __0)
        {
            if(!RecruitmentHooks.Registered)return;
            string id=Native.EnumName(__0);
            if(id==Deception.Core.AquarionCatalog.MimicId||id==Deception.Core.SpyCatalog.AquarionId){__0=(UnitType)Native.EnumValue(RecruitmentHooks.UT,"mermaidwarrior");return;}
            if((id==Deception.Core.SpyCatalog.Id||id==Deception.Core.FoggerCatalog.Id)||id==Deception.Core.RecruitmentCatalog.MenuId){__0=(UnitType)Native.EnumValue(RecruitmentHooks.UT,"warrior");return;}
            if(!RecruitmentHooks.Forms.TryGetValue(id,out var form))return;
            // EnumCache understands native content IDs, including mixed-case
            // compiled names. Its round-trip check rejects missing mappings.
            // Assign only after complete resolution; leave tribe/skin and the
            // original native renderer, visibility, pooling and cache intact.
            var native=(UnitType)Native.EnumValue(RecruitmentHooks.UT,(form.NativeId==Deception.Core.SpyCatalog.Id||form.NativeId==Deception.Core.FoggerCatalog.Id)?"warrior":form.NativeId==Deception.Core.SpyCatalog.AquarionId?"mermaidwarrior":form.NativeId);
            if(Deception.Core.RecruitmentCatalog.IsMimicId(form.NativeId)||native==__0)
                throw new InvalidOperationException("Mimic prefab source is not a genuine native form.");
            __0=native;
        }
    }
}
