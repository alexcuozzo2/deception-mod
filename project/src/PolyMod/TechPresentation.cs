using System;
using System.Linq;
using System.Reflection;
using Deception.Core;
using HarmonyLib;
using Polytopia.Data;

namespace Deception.PolyModAdapter
{
    // Presentation and authoritative unlocks are intentionally separate. Concrete
    // forms MUST stay registered for native training, but are not separate rewards
    // in Deception's technology popup. Never edit the live TechData or its list.
    internal static class TechPresentation
    {
        static MethodInfo CloneNativeObject=null!;
        [ThreadStatic] static PreviewScope? Current;
        static int Generation;
        internal sealed class PreviewScope
        {
            internal PreviewScope? Prior;
            internal object? State,Registry,Player;
            internal string? Species;
            internal int Epoch;
        }
        internal static void Clear(){Generation++;Current=null;}
        static PreviewScope Begin(ref TechData data,string view,object? player,out bool compacted)
        {
            // Every hooked entry creates a scope, including unrelated native
            // tech. Its children cannot borrow an outer Aquarion preview.
            var scope=new PreviewScope{Prior=Current,Epoch=Generation};Current=scope;compacted=false;
            try
            {
                player??=Native.Static("GameManager","LocalPlayer");
                compacted=Compact(ref data,view,player);
                if(!compacted||player is null||!NativeAquarionUnits.IsAquarion(player))return scope;
                var game=Native.Static("GameManager","GameState");if(game is null)return scope;
                string tech=Native.EnumName(Native.Get(data,"type")!);
                if(tech!=TechTree.DeceptionId&&tech!=TechTree.EspionageId)return scope;
                scope.State=game;scope.Registry=Native.Get(game,"GameLogicData");scope.Player=player;
                scope.Species=tech==TechTree.DeceptionId?AquarionCatalog.MimicId:SpyCatalog.AquarionId;
            }
            catch(Exception e){Entry.Note("mimic-tech-preview-error",view+" scope: "+e);}
            return scope;
        }
        internal static object FinalUnit(object data,object? player)
        {
            var scope=Current;
            if(scope?.Species is null||scope.Epoch!=Generation||!RecruitmentHooks.Registered||
                player is null||!Native.Same(player,scope.Player)||!Native.Same(Native.Static("GameManager","GameState"),scope.State))return data;
            try
            {
                if(!Native.Same(Native.Get(scope.State!,"GameLogicData"),scope.Registry)||!NativeAquarionUnits.IsAquarion(player))return data;
                string id=RecruitmentHooks.Id(data);
                // Only the base category belonging to this particular technology
                // can be corrected. A genuine/acquired Spy outside the scope,
                // another tech's unit or a native Warrior remains untouched.
                string source=scope.Species==SpyCatalog.AquarionId?SpyCatalog.Id:RecruitmentCatalog.MenuId;
                if(id!=source)return data;
                var replacement=RecruitmentHooks.Data(scope.State!,scope.Species);
                if(RecruitmentHooks.Id(replacement)!=scope.Species)throw new InvalidOperationException("Final technology species differs.");
                return replacement;
            }
            catch(Exception e){Entry.Note("mimic-tech-preview-error","Final unit: "+e);return data;}
        }
        public static void BeginPopup(ref TechData __0,out PreviewScope __state)=>__state=Begin(ref __0,"TechPopupContent",null,out _);
        public static void BeginLegacy(ref TechData __0,out PreviewScope __state)=>__state=Begin(ref __0,"TechPopup",null,out _);
        public static void BeginTree(ref TechData __0,object __1,ref bool __2,out PreviewScope __state)
        {__state=Begin(ref __0,"TechItem",__1,out bool compacted);if(compacted&&Native.EnumName(Native.Get(__0,"type")!)==TechTree.DeceptionId)__2=true;}
        public static Exception? EndPreview(Exception? __exception,PreviewScope? __state)
        {if(__state is not null&&__state.Epoch==Generation&&ReferenceEquals(Current,__state))Current=__state.Prior;return __exception;}

        public static void Install(Harmony h)
        {
            // Select the IL2CPP Object method, NOT System.Object.MemberwiseClone:
            // cloning only the managed wrapper would alias the live native object.
            var owner=Native.T("Il2CppSystem.Object");
            CloneNativeObject=owner.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly)
                .Single(m=>m.Name=="MemberwiseClone"&&m.GetParameters().Length==0&&m.ReturnType==owner);
            Native.Hook(h,Native.M("TechPopupContent","CreateTechDataContent","Polytopia.Data.TechData"),typeof(TechPresentation),nameof(BeginPopup),null,nameof(EndPreview));
            Native.Hook(h,Native.M("TechPopup","SetTechData","Polytopia.Data.TechData"),typeof(TechPresentation),nameof(BeginLegacy),null,nameof(EndPreview));
            Native.Hook(h,Native.M("TechItem","GetUnlockItems","Polytopia.Data.TechData","PlayerState","System.Boolean"),typeof(TechPresentation),nameof(BeginTree),null,nameof(EndPreview));
        }
        static bool Compact(ref TechData data,string view,object? player=null)
        {
            if(!RecruitmentHooks.Registered||data is null)return false;
            string tech=Native.EnumName(Native.Get(data,"type")!);
            player??=Native.Static("GameManager","LocalPlayer");
            bool aquarion=player is not null&&NativeAquarionUnits.IsAquarion(player);
            if(tech!=TechTree.DeceptionId&&(tech!=TechTree.EspionageId||!aquarion))return false;
            try
            {
                object liveList=Native.Get(data,"unitUnlocks")??throw new InvalidOperationException("Missing native tech-unlock list.");
                object[] units=Native.Items(liveList).ToArray();
                object[] category=units.Where(u=>tech==TechTree.DeceptionId?RecruitmentCatalog.IsMenuId(RecruitmentHooks.Id(u)):SpyCatalog.IsSpyId(RecruitmentHooks.Id(u))).Take(1).ToArray();
                if(category.Length!=1)throw new InvalidOperationException("Unit category is absent from its registered tech unlocks.");
                if(aquarion)
                {
                    var game=Native.Static("GameManager","GameState")??throw new InvalidOperationException("Technology preview state is absent.");
                    string species=tech==TechTree.DeceptionId?AquarionCatalog.MimicId:SpyCatalog.AquarionId;
                    category=new[]{RecruitmentHooks.Data(game,species)};
                    if(RecruitmentHooks.Id(category[0])!=species)throw new InvalidOperationException("Aquarion technology preview unit differs.");
                }
                var display=(TechData)Native.As(Native.Invoke(CloneNativeObject,data)!,"Polytopia.Data.TechData");
                if(Native.Same(display,data))throw new InvalidOperationException("Display clone aliases live native TechData; refusing mutation.");
                // Only the clone receives a new list. IDs, cost, other unlocks and
                // all authoritative UnitData records are preserved, not re-created.
                Native.Set(display,"unitUnlocks",Native.NewList("Polytopia.Data.UnitData",category));
                data=display;
                Entry.Note("mimic-tech-preview",view+": one "+RecruitmentHooks.Id(category[0])+" category icon; "+units.Length+" native unlock entries retained in source data.");
                return true;
            }
            catch(Exception e)
            {
                // Keep ordinary game behavior and do not damage the live registry
                // on an unsupported display API. The report records this failure.
                Entry.Note("mimic-tech-preview-error",view+": "+e);return false;
            }
        }
        public static void PopupContent(ref TechData __0){Compact(ref __0,"TechPopupContent");}
        public static void LegacyPopup(ref TechData __0){Compact(ref __0,"TechPopup");}
        public static void UnlockIcons(ref TechData __0,ref bool __2)
        {if(Compact(ref __0,"TechItem"))__2=true;}
        public static void UnlockIconsForPlayer(ref TechData __0,object __1,ref bool __2)
        {if(Compact(ref __0,"TechItem",__1))__2=true;}
    }
}
