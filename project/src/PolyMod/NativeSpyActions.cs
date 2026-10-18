using System;
using System.Linq;
using System.IO;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Real Spy movement, Scout sight, price and recruitment use native unit data.
    // Its zero attack does not permit a nominal attack or damaging retaliation.
    // A genuine armed vessel carrying a Spy keeps the vessel's native weapon.
    internal static class NativeSpyActions
    {
        // Installed by the ordinary predicate adapter. No general UI/attack
        // query receives this exception, and native readiness remains final.
        internal static Func<object,bool>? AllowOrdinaryReadiness;
        [ThreadStatic] static Embark? Boarding;
        static long Generation;
        internal sealed record Embark(object? Unit,Embark? Previous,long Epoch);
        internal sealed record Recovery(object State,object Unit,object Stack,object[] Prefix,byte Owner,uint Turn,long Epoch);
        internal static void Clear(){Boarding=null;Generation++;}
        static bool Spy(object? unit)=>unit is not null&&RecruitmentHooks.Registered&&
            SpyCatalog.IsSpyId(Native.EnumName(Native.Get(unit,"type")!));
        static bool NonCombat(object? unit)=>Spy(unit)||NativeFoggerActions.IsFogger(unit);
        internal static void Install(Harmony h)
        {
            var patch=typeof(NativeSpyActions);
            Native.Hook(h,Native.M("UnitState","CanAttack"),patch,nameof(NoAttack));
            Native.Hook(h,Native.M("RecoverCommand","Execute","GameState"),patch,nameof(BeforeRecover),nameof(AfterRecover));
            Native.Hook(h,Native.M("EmbarkAction","Execute","GameState"),patch,nameof(BeforeEmbark),null,nameof(EmbarkFinished));
            Native.Hook(h,Native.M("UnitDataExtensions","HasAbility","UnitState","Polytopia.Data.UnitAbility+Type"),patch,null,nameof(EmbarkAbility));
            Native.Hook(h,Native.M("UnitDataExtensions","CanAttackAnything","UnitState","System.Int32","GameState"),patch,nameof(NoAttackAny));
            Native.Hook(h,Native.M("BattleHelpers","CanDefenderRetaliate","GameState","UnitState","UnitState"),patch,nameof(NoRetaliation));
            Native.Hook(h,Native.M("UnitDataExtensions","CanBePromoted","UnitState","GameState"),patch,nameof(NoPromotion));
        }
        public static void BeforeEmbark(object __instance,object __0,out Embark? __state)
        {
            __state=null;
            object? unit=null;
            if(NativeRegroupSession.Enabled&&NativeRegroupSession.Ready(__0))
            {
                var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(__0,"Map"),Native.Get(__instance,"Coordinates"));
                var candidate=tile is null?null:Native.Get(tile,"unit");
                if(Spy(candidate))unit=candidate;
            }
            if(unit is null&&Boarding is null)return;
            __state=new(unit,Boarding,Generation);Boarding=__state;
        }
        public static void EmbarkAbility(object __0,object __1,ref bool __result)
        {
            // Native boarding maps Hide to the Cloak-only Dinghy. A Spy wearing a
            // Cloak disguise must still use its ordinary physical vessel.
            if(__result&&Native.Int(__1)==5&&Boarding is {Unit:object unit} scope&&scope.Epoch==Generation&&Native.Same(__0,unit))__result=false;
        }
        public static Exception? EmbarkFinished(Exception? __exception,Embark? __state)
        {if(__state is not null&&ReferenceEquals(Boarding,__state))Boarding=__state.Epoch==Generation?__state.Previous:null;return __exception;}
        static bool SameAt(object a,object b)=>Native.Int(Native.Get(a,"X"))==Native.Int(Native.Get(b,"X"))&&Native.Int(Native.Get(a,"Y"))==Native.Int(Native.Get(b,"Y"));
        public static void BeforeRecover(object __instance,object __0,out Recovery? __state,bool __runOriginal=true)
        {
            __state=null;if(!__runOriginal||!NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(__0))return;
            var at=Native.Get(__instance,"Coordinates")!;
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(__0,"Map"),at);
            var unit=tile is null?null:Native.Get(tile,"unit");
            if(!Spy(unit)||!Native.Items(Native.Get(unit!,"effects")!).Any(e=>Native.Int(e)==3))return;
            object?[] valid={__0,null};if(!(bool)Native.Invoke(Native.M("RecoverCommand","IsValid","GameState","System.String&"),__instance,valid)!)return;
            byte owner=Convert.ToByte(Native.Get(unit!,"owner"));
            if(owner!=Native.Int(Native.Get(__instance,"PlayerId"))||owner!=Native.Int(Native.Get(__0,"CurrentPlayer")))return;
            var stack=Native.Get(__0,"ActionStack")!;
            __state=new(__0,unit!,stack,Native.Items(stack).ToArray(),owner,Convert.ToUInt32(Native.Get(__0,"CurrentTurn")),Generation);
        }
        public static void AfterRecover(object __0,Recovery? __state,bool __runOriginal=true)
        {
            if(!__runOriginal||__state is not Recovery s||s.Epoch!=Generation||!Native.Same(__0,s.State)||!NativeRegroupSession.Ready(__0))return;
            if(Native.Int(Native.Get(s.Unit,"owner"))!=s.Owner||Native.Int(Native.Get(__0,"CurrentPlayer"))!=s.Owner||Convert.ToUInt32(Native.Get(__0,"CurrentTurn"))!=s.Turn)return;
            var stack=Native.Get(__0,"ActionStack")!;var entries=Native.Items(stack).ToArray();
            if(!Native.Same(stack,s.Stack)||entries.Length<s.Prefix.Length+2||s.Prefix.Where((a,i)=>!Native.Same(a,entries[i])).Any())
                throw new InvalidDataException("Spy recovery changed its native action queue.");
            var heal=Native.As(entries[^1],"HealAction");var at=Native.Get(s.Unit,"coordinates")!;
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(__0,"Map"),at);
            if(tile is null||!Native.Same(Native.Get(tile,"unit"),s.Unit)||Native.Int(Native.Get(heal,"PlayerId"))!=s.Owner||
                !SameAt(Native.Get(heal,"Coordinates")!,at)||Native.Int(Native.Get(heal,"FixedHealAmount"))!=0)
                throw new InvalidDataException("Spy recovery lost its exact native Heal action.");
            // The same self-reveal constructor used by native CaptureCommand
            // has no detecting unit, popup or movement. Keep it a native queued
            // action so save/load and replay retain both hide state and cue.
            var reveal=Activator.CreateInstance(Native.T("RevealAction"),new object[]{s.Owner,at,false})!;
            var staged=Native.NewList("ActionBase",entries.Append(reveal));
            try{Native.Set(__0,"ActionStack",staged);}
            catch{if(Native.Same(Native.Get(__0,"ActionStack"),staged))Native.Set(__0,"ActionStack",stack);throw;}
        }
        public static bool NoAttack(object __instance,ref bool __result)
        {if(!NonCombat(__instance)||AllowOrdinaryReadiness?.Invoke(__instance)==true)return true;__result=false;return false;}
        public static bool NoAttackAny(object __0,ref bool __result)
        {if(!NonCombat(__0))return true;__result=false;return false;}
        public static bool NoPromotion(object __0,ref bool __result)
        {if(!NonCombat(__0))return true;__result=false;return false;}
        public static bool NoRetaliation(object __2,ref bool __result)
        {if(!NonCombat(__2))return true;__result=false;return false;}
    }
}
