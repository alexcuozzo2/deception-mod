// M1B: real native registrations + a private two-stage wheel, still a gameplay prototype.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    internal static class RecruitmentHooks
    {
        internal const string UT="Polytopia.Data.UnitData+Type", TT="Polytopia.Data.TechData+Type", GLD="Polytopia.Data.GameLogicData";
        internal static readonly Dictionary<string,RecruitmentForm> Forms=new(StringComparer.Ordinal);
        static IReadOnlyDictionary<string,NativeAppearanceRoster>? AllResearch;
        static MethodInfo GetUnits=null!,GetUnit=null!,HasTech=null!;
        internal static MethodInfo ClickedSpawn=null!,TrainValid=null!;
        [ThreadStatic] static int ReadNativeRoster;
        [ThreadStatic] static int CityMenuDepth;
        internal static bool Ready { get; private set; }
        internal static bool Registered { get; private set; }
        public static void Clear()
        {Registered=false;AllResearch=null;Forms.Clear();RecruitmentWheelUi.Clear();MimicPresentationState.Clear();MimicPresentationUi.Clear();MimicControls.Clear();OrdinaryActionEligibility.Clear();}
        public static void Register(RecruitmentForm[] forms,IReadOnlyDictionary<string,NativeAppearanceRoster> rosters)
        {Forms.Clear();foreach(var form in forms)Forms.Add(form.VariantId,form);Forms.Add(RecruitmentCatalog.MenuId,new(RecruitmentCatalog.MenuId,RecruitmentCatalog.MenuId,"Mimic",1,1,0,0,false));Forms.Add(AquarionCatalog.MimicId,new(AquarionCatalog.MimicId,AquarionCatalog.MimicId,"Mermimic",1,1,0,0,true));AllResearch=rosters;Registered=true;}
        internal static void ValidateRosterReentry(string json)=>NativeRecruitmentRosterReentry.Validate(json,AllResearch);
        public static void Install(Harmony h)
        {
            GetUnits=Native.M(GLD,"GetUnlockedUnits","PlayerState","GameState","System.Boolean");
            GetUnit=Native.M(GLD,"GetUnitData",UT);
            HasTech=Native.M("PlayerExtensions","HasTech","PlayerState",TT);
            // This is also the native city-menu terrain gate. Resolve it before
            // installing hooks; do not approximate coastal/flooded eligibility.
            Native.M("CommandValidation","HasUnitTerrain","GameState","WorldCoordinates","Polytopia.Data.UnitData");
            ClickedSpawn=Native.M("InteractionBar","ClickedSpawnUnit","CommandBase","Polytopia.Data.UnitData");
            TrainValid=Native.M("TrainCommand","IsValid","GameState","System.String&");
            if(Native.T("TrainCommand").GetConstructor(new[]{typeof(byte),Native.T(UT),Native.T("WorldCoordinates")}) is null)
                throw new MissingMethodException("TrainCommand native constructor changed");
            Native.Hook(h,Native.M(GLD,"IsUnlocked",UT,"PlayerState"),typeof(RecruitmentHooks),nameof(UnitUnlocked));
            Native.Hook(h,GetUnits,typeof(RecruitmentHooks),null,nameof(FilterUnlockedUnits));
            Native.Hook(h,TrainValid,typeof(RecruitmentHooks),nameof(GuardTrainValidity));
            Native.Hook(h,Native.M("InteractionBar","AddTrainUnitButtons","Tile"),typeof(RecruitmentHooks),nameof(EnterCityMenu),null,nameof(ExitCityMenu));
            Native.Hook(h,Native.M("CommandUtils","GetTrainableUnits","GameState","PlayerState","TileData","System.Boolean"),typeof(RecruitmentHooks),null,nameof(FilterTrainChoices));
            if(!NativeExecutionTrigger.Requested)
            {
            Native.Hook(h,Native.M("TrainCommand","Execute","GameState"),typeof(RecruitmentHooks),nameof(GuardTrainCommand));
            Native.Hook(h,Native.M("TrainAction","Execute","GameState"),typeof(RecruitmentHooks),nameof(GuardTrainAction),nameof(TrainActionReturned));
            }
            Native.Hook(h,Native.M("UnitState","CanAttack"),typeof(RecruitmentHooks),nameof(NoAttack));
            Native.Hook(h,Native.M("UnitDataExtensions","CanAttackAnything","UnitState","System.Int32","GameState"),typeof(RecruitmentHooks),nameof(NoAttackAny));
            Native.Hook(h,Native.M("UnitDataExtensions","CanBePromoted","UnitState","GameState"),typeof(RecruitmentHooks),nameof(NoPromotion));
            Native.Hook(h,Native.M("BattleHelpers","CanDefenderRetaliate","GameState","UnitState","UnitState"),typeof(RecruitmentHooks),nameof(NoRetaliation));
            Native.Hook(h,Native.M("MoveCommand","IsValid","GameState","System.String&"),typeof(RecruitmentHooks),null,nameof(NoPrototypeBoarding));
            RecruitmentWheelUi.Install(h);
            Ready=true;
        }
        public static void EnterCityMenu(out int __state){__state=CityMenuDepth;CityMenuDepth++;}
        public static Exception? ExitCityMenu(Exception? __exception,int __state){CityMenuDepth=__state;return __exception;}
        internal static string Id(object data)=>Native.EnumName(Native.Get(data,"type")!);
        static bool IsUnit(object? unit)=>unit is not null&&Registered&&RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(unit,"type")!));
        internal static bool OwnsDeception(object player)=>Registered&&(bool)Native.Invoke(HasTech,null,player,Native.EnumValue(TT,TechTree.DeceptionId))!;
        internal static object Data(object state,string id)=>Native.Invoke(GetUnit,Native.Get(state,"GameLogicData"),Native.EnumValue(UT,id))!;
        static HashSet<string> RosterFrom(object gld,object player,IEnumerable<object> items)
        {
            var tribe=Native.Invoke(Native.M(GLD,"GetTribeData","PolytopiaBackendBase.Common.TribeType"),gld,Native.Get(player,"tribe"));
            var applyOverride=Native.M(GLD,"GetOverride","Polytopia.Data.UnitData","Polytopia.Data.TribeData");
            return items.Where(d=>!RecruitmentCatalog.IsMimicId(Id(d)))
                .Select(d=>Native.Invoke(applyOverride,gld,d,tribe)!).Select(Id)
                .Where(s=>!RecruitmentCatalog.IsMimicId(s)).ToHashSet(StringComparer.Ordinal);
        }
        internal static HashSet<string> NativeRoster(object state,object player)
        {
            ReadNativeRoster++;
            try
            {
                var gld=Native.Get(state,"GameLogicData")!;
                return RosterFrom(gld,player,Native.Items(Native.Invoke(GetUnits,gld,player,state,false)!));
            }
            finally{ReadNativeRoster--;}
        }
        internal static HashSet<string> AppearanceRoster(object state,object player)
        {
            if(!OwnsDeception(player))return new(StringComparer.Ordinal);
            object?[] args={Native.Get(player,"Id"),null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!||!Native.Same(player,args[1]))
                throw new InvalidOperationException("Appearance roster player does not belong to the supplied state.");
            if(!(bool)Native.Invoke(HasTech,null,player,Native.EnumValue(TT,TechTree.PropagandaId))!)return NativeRoster(state,player);
            string tribe=Native.EnumName(Native.Get(player,"tribe")!);
            if(AllResearch is null||!AllResearch.TryGetValue(tribe,out var roster))throw new InvalidOperationException("No captured native appearance roster for "+tribe);
            // Current reviewed native rosters have no eligible Unique forms.
            // New native data needs its board uniqueness adapter before admission.
            if(roster.RequiresUniqueCheck.Count!=0)throw new InvalidOperationException("Native appearance roster now requires a uniqueness adapter.");
            return roster.Supported.ToHashSet(StringComparer.Ordinal);
        }
        internal static bool CitySupportsAppearance(object state,object coordinates,string form)
            =>(bool)Native.Invoke(Native.M("CommandValidation","HasUnitTerrain","GameState","WorldCoordinates","Polytopia.Data.UnitData"),
                null,state,coordinates,Data(state,form))!;
        internal static HashSet<string> CityAppearanceRoster(object state,object player,object coordinates)
            =>AppearanceRoster(state,player).Where(form=>CitySupportsAppearance(state,coordinates,form)).ToHashSet(StringComparer.Ordinal);
        // Never mutate a native cached return-list in place. Build a fresh list, preserving
        // every original entry. Only custom forms may use Propaganda's separate roster.
        public static void FilterUnlockedUnits(object __instance,object __0,object __1,bool __2,ref Il2CppSystem.Collections.Generic.List<Polytopia.Data.UnitData> __result)
        {
            if(!Registered||__result is null)return;
            try
            {
                var original=Native.Items(__result).ToArray();
                var entries=original.Where(d=>!RecruitmentCatalog.IsMimicId(Id(d))).ToList();
                if(ReadNativeRoster==0&&OwnsDeception(__0))
                {
                    var roster=AppearanceRoster(__1,__0);
                    entries.Add(Native.Invoke(GetUnit,__instance,Native.EnumValue(UT,NativeAquarionUnits.MimicMenu(__0)))!);
                    if(CityMenuDepth==0) // The normal city wheel shows ONE category, never 26 variants.
                        foreach(var f in RecruitmentEligibility.Choices(Forms.Values,roster))
                            entries.Add(Native.Invoke(GetUnit,__instance,Native.EnumValue(UT,f.VariantId))!);
                }
                __result=(Il2CppSystem.Collections.Generic.List<Polytopia.Data.UnitData>)Native.NewList("Polytopia.Data.UnitData",entries);
            }
            catch(Exception e)
            {
                Entry.Note("unlocked-list-error",e.ToString());
                // Reject only custom content if a roster lookup fails, not ordinary training.
                __result=(Il2CppSystem.Collections.Generic.List<Polytopia.Data.UnitData>)Native.NewList("Polytopia.Data.UnitData",Native.Items(__result).Where(d=>!RecruitmentCatalog.IsMimicId(Id(d))).ToArray());
            }
        }
        public static bool UnitUnlocked(object __0,object __1,ref bool __result)
        {
            if(!Registered)return true;
            string id=Native.EnumName(__0);
            if(!RecruitmentCatalog.IsMimicId(id))return true;
            __result=false;
            if(ReadNativeRoster>0)return false;
            try
            {
                if(!OwnsDeception(__1))return false;
                if(RecruitmentCatalog.IsMenuId(id)){__result=true;return false;}
                var state=NativeExecutionProbe.PaidUnlockState(id,__1)??Native.Static("GameManager","GameState");
                __result=state is not null&&Forms.TryGetValue(id,out var form)&&
                    RecruitmentEligibility.Permitted(true,form.NativeId,AppearanceRoster(state,__1));
            }
            catch(Exception e){Entry.Note("recruitment-unlock-error",e.ToString());}
            return false;
        }
        internal static object NewCommand(object player,object coordinates,string variant)
            =>Activator.CreateInstance(Native.T("TrainCommand"),new[]{
                (object)(byte)Native.Int(Native.Get(player,"Id")),Native.EnumValue(UT,variant),coordinates})!;
        internal static bool RecruitmentStateReady(object state,out string reason)
        {
            // Eligibility must not enroll a state. A real local session binds
            // its ledger at creation/read boundaries before any custom birth.
            bool ready=!NativeRegroupSession.Enabled||NativeRegroupSession.Ready(state);
            reason=ready?"":"Deception recruitment is unavailable in this game session.";
            return ready;
        }
        internal static bool CheckEligibility(object state,object command,out string reason)
        {
            reason="Mimic recruitment is not available.";
            if(!RecruitmentStateReady(state,out reason))return false;
            // Private serializer trials cannot carry live custom Train history.
            // Reject before the native purchase/spawn instead of letting a later
            // serialization rejection undo an animated, apparently paid unit.
            if(NativeTrainLeafProbeTrigger.Requested)
            {reason="Mimic recruitment is unavailable during serializer diagnostics. Relaunch the game normally.";return false;}
            // Live paid birth is still unavailable in this fresh-session slice.
            // Only the exact private diagnostic validity scope may proceed;
            // a resumed framed match never acquires that authority.
            if(NativeFreshSession.BlockMimicRecruitment(state)&&!NativeExecutionProbe.AllowPaidEligibility(state,command))return false;
            string id=Native.EnumName(Native.Get(command,"Type")!);
            if(!Registered||!Forms.TryGetValue(id,out var form))return false;
            if(RecruitmentCatalog.FieldCopyOnly.Contains(form.NativeId,StringComparer.Ordinal)||RecruitmentCatalog.NavalCopyOnly.Contains(form.NativeId,StringComparer.Ordinal))
            {reason="This form is available through Regroup only.";return false;}
            object?[] args={Native.Get(command,"PlayerId"),null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!||args[1] is null)return false;
            if(!OwnsDeception(args[1]!)){reason="Research Deception before recruiting a Mimic.";return false;}
            if(!RecruitmentEligibility.Permitted(true,form.NativeId,AppearanceRoster(state,args[1]!)))
            {reason="This appearance requires its native research or Propaganda and an eligible tribe form.";return false;}
            // Check the apparent source as well as the native carrier. This is
            // read-only and uses the command's exact city, including replayed
            // TrainAction, rather than the currently selected UI city. Native
            // purchase validity still owns funds, occupancy and city support.
            if(!CitySupportsAppearance(state,Native.Get(command,"Coordinates")!,form.NativeId))
            {reason="This city does not have the terrain required for that appearance.";return false;}
            reason="";return true;
        }
        public static bool GuardTrainValidity(object __instance,object __0,ref string __1,ref bool __result)
        {
            string id=Native.EnumName(Native.Get(__instance,"Type")!);
            if(Registered&&(SpyCatalog.IsSpyId(id)||FoggerCatalog.IsFoggerId(id))&&!RecruitmentStateReady(__0,out __1)){__result=false;return false;}
            if(!Registered||!id.StartsWith(RecruitmentCatalog.Prefix,StringComparison.Ordinal))return true;
            try
            {
                if(CheckEligibility(__0,__instance,out var why))return true; // Native city/funds/support checks STILL RUN.
                __1=why;__result=false;return false;
            }
            catch(Exception e){__1="Mimic eligibility could not be verified.";__result=false;Entry.Note("train-validity-error",e.ToString());return false;}
        }
        internal static bool NativeValid(object state,object command,out string reason)
        {
            object?[] args={state,null};bool valid=(bool)Native.Invoke(TrainValid,command,args)!;
            reason=(string?)args[1]??(valid?"":"The native city, funds, occupancy or support check failed.");return valid;
        }
        public static void FilterTrainChoices(object __result)
        {
            if(!Registered||__result is null)return;
            var commands=Native.Items(__result).ToArray();var remove=Native.M(__result.GetType(),"RemoveAt","System.Int32");
            for(int i=commands.Length-1;i>=0;i--)
            {
                string id=Native.EnumName(Native.Get(commands[i],"Type")!);
                if(id.StartsWith(RecruitmentCatalog.Prefix,StringComparison.Ordinal)||(RecruitmentCatalog.IsMenuId(id)&&CityMenuDepth==0))
                    Native.Invoke(remove,__result,i);
            }
        }
        public static bool GuardTrainCommand(object __instance,object __0)
        {
            if(!Registered)return true;string id=Native.EnumName(Native.Get(__instance,"Type")!);
            if((SpyCatalog.IsSpyId(id)||FoggerCatalog.IsFoggerId(id)))return RecruitmentStateReady(__0,out _);
            if(RecruitmentCatalog.IsMenuId(id)){Entry.Note("bare-recruitment-blocked","Choose an appearance; menu anchor is never spawned.");return false;}
            if(!id.StartsWith(RecruitmentCatalog.Prefix,StringComparison.Ordinal))return true;
            try
            {
                if(NativeValid(__0,__instance,out var reason)){Entry.Note("mimic-command-accepted",id+"; native validity passed; native action owns payment/spawn.");return true;}
                Entry.Note("mimic-command-rejected",id+": "+reason);return false;
            }
            catch(Exception e){Entry.Note("mimic-command-error",e.ToString());return false;}
        }
        public static bool GuardTrainAction(object __instance,object __0,out bool __state)
        {
            __state=false;if(!Registered)return true;string id=Native.EnumName(Native.Get(__instance,"Type")!);
            if((SpyCatalog.IsSpyId(id)||FoggerCatalog.IsFoggerId(id)))
            {
                if(RecruitmentStateReady(__0,out _))return true;
                Native.Set(__instance,"trainActionAborted",true);return false;
            }
            if(!RecruitmentCatalog.IsMimicId(id))return true;
            try
            {
                if(!RecruitmentCatalog.IsMenuId(id)&&CheckEligibility(__0,__instance,out var why)&&Native.Int(Native.Get(__instance,"Cost"))==RecruitmentCatalog.Cost)
                {__state=true;return true;}
                Native.Set(__instance,"trainActionAborted",true);
                Entry.Note("mimic-action-rejected","Menu-only type, wrong native cost, or no longer eligible: "+id);return false;
            }
            catch(Exception e){Native.Set(__instance,"trainActionAborted",true);Entry.Note("mimic-action-error",e.ToString());return false;}
        }
        public static void TrainActionReturned(object __instance,bool __state)
        {
            if(!__state)return;
            Entry.Note("mimic-action-returned",Native.EnumName(Native.Get(__instance,"Type")!)+"; aborted="+Native.Get(__instance,"trainActionAborted"));
        }
        public static bool NoAttack(object __instance,ref bool __result)
        {if(!IsUnit(__instance)||OrdinaryActionEligibility.AllowsNativeReadiness(__instance))return true;__result=false;return false;}
        public static bool NoAttackAny(object __0,ref bool __result)
        {if(!IsUnit(__0))return true;__result=false;return false;}
        public static bool NoPromotion(object __0,ref bool __result)
        {if(!IsUnit(__0))return true;__result=false;return false;}
        public static bool NoRetaliation(object __2,ref bool __result)
        {if(!IsUnit(__2))return true;__result=false;return false;}
        public static void NoPrototypeBoarding(object __instance,object __0,ref string __1,ref bool __result)
        {
            if(!Registered||!__result)return;
            object?[] args={Native.Get(__instance,"UnitId"),null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),__0,args)!||args[1] is null)return;
            string id=Native.EnumName(Native.Get(args[1]!,"type")!);
            if(!Forms.TryGetValue(id,out var form)||form.NativeWaterTravel)return;
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(__0,"Map"),Native.Get(__instance,"To"));
            if(tile is not null&&(bool)Native.Get(tile,"IsWater")!)
            {
                if(NativeMimicTransport.CanHandleMovement(__0,args[1]!,tile))return;
                __result=false;__1="This Mimic transport transition is not currently available.";
            }
        }
    }
}
