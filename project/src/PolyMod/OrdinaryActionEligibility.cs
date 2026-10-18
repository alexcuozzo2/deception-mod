using System;
using System.Collections.Generic;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Audited native CanCapture/CanExamineRuins call CanAttack as readiness,
    // not offense. Only the exact predicate body may read native readiness;
    // native timing/terrain/ownership and the independent Rogue ban still win.
    internal static class OrdinaryActionEligibility
    {
        internal sealed class Scope
        {
            internal Scope? Previous;
            internal long UnitPointer;
            internal string Form="";
            internal string Predicate="";
            internal bool ReadinessConsulted;
            internal bool? NativeResult;
            internal int Generation;
        }

        [ThreadStatic] static Scope? Current;
        static int Generation;
        static readonly HashSet<string> Observed=new(StringComparer.Ordinal);
        static readonly object ObservationLock=new();
        const int MaxObservations=96;

        internal static void Install(Harmony harmony)
        {
            NativeSpyActions.AllowOrdinaryReadiness=AllowsNativeReadiness;
            var capture=Native.M("UnitDataExtensions","CanCapture","UnitState","GameState","TileData","System.Boolean","System.Boolean");
            var ruins=Native.M("UnitDataExtensions","CanExamineRuins","UnitState","GameState","TileData");
            var disband=Native.M("UnitDataExtensions","CanDisband","UnitState","GameState","PlayerState");
            Native.RequireReturn(capture,typeof(bool));
            Native.RequireReturn(ruins,typeof(bool));
            Native.RequireReturn(disband,typeof(bool));
            Native.Hook(harmony,capture,typeof(OrdinaryActionEligibility),nameof(EnterCapture),nameof(ObserveResult),nameof(ExitPredicate));
            Native.Hook(harmony,ruins,typeof(OrdinaryActionEligibility),nameof(EnterRuins),nameof(ObserveResult),nameof(ExitPredicate));
            Native.Hook(harmony,disband,typeof(OrdinaryActionEligibility),nameof(EnterDisband),nameof(ObserveResult),nameof(ExitPredicate));
        }

        internal static void Clear()
        {
            Generation++;Current=null;
            lock(ObservationLock)Observed.Clear();
        }

        // Called only by RecruitmentHooks.NoAttack before its usual false
        // override. Returning true here means RUN native CanAttack, not grant
        // an action: native spent/frozen/other readiness checks still execute.
        internal static bool AllowsNativeReadiness(object unitState)
        {
            var scope=Current;
            if(scope is null || scope.UnitPointer==0 || unitState is null)return false;
            try
            {
                if(Native.Pointer(unitState)!=scope.UnitPointer)return false;
                scope.ReadinessConsulted=true;
                return true;
            }
            catch
            {
                // A failed identity check retains the existing offense guard.
                NoteOnce("scope-identity-unavailable");
                return false;
            }
        }

        internal static void EnterCapture(object __0,out Scope __state)
            =>Enter(__0,"CanCapture",out __state);
        internal static void EnterRuins(object __0,out Scope __state)
            =>Enter(__0,"CanExamineRuins",out __state);
        internal static void EnterDisband(object __0,out Scope __state)
            =>Enter(__0,"CanDisband",out __state);

        static void Enter(object unitState,string predicate,out Scope state)
        {
            state=new Scope{Previous=Current,Predicate=predicate,Generation=Generation};
            // Even an ordinary-unit nested query masks the caller's scope.
            // It cannot borrow a surrounding Mimic's readiness exception.
            Current=state;
            try
            {
                if(!RecruitmentHooks.Registered || unitState is null)return;
                string id=Native.EnumName(Native.Get(unitState,"type")!);
                if(!RecruitmentCatalog.IsMimicId(id)&&!SpyCatalog.IsSpyId(id)&&!FoggerCatalog.IsFoggerId(id))return;
                state.UnitPointer=Native.Pointer(unitState);
                state.Form=id;
            }
            catch
            {
                state.UnitPointer=0;
                NoteOnce("scope-lookup-unavailable");
            }
        }

        // Observe by value. No predicate result, native unit or game field is
        // changed; the command's normal validation/execution still owns costs,
        // ownership, territory, timing, support and genuine refund calculation.
        internal static void ObserveResult(bool __result,Scope? __state)
        {
            if(__state is not null)__state.NativeResult=__result;
        }

        internal static Exception? ExitPredicate(Exception? __exception,Scope? __state)
        {
            if(__state is null)return __exception;
            if(__state.Generation==Generation&&ReferenceEquals(Current,__state))Current=__state.Previous;
            if(__state.Form.Length!=0)
            {
                string outcome=__exception is not null?"exception":
                    __state.NativeResult.HasValue?(__state.NativeResult.Value?"true":"false"):"not-returned";
                NoteOnce(__state.Form+"; "+__state.Predicate+"; native="+outcome+
                    "; readiness-consulted="+(__state.ReadinessConsulted?"true":"false"));
            }
            return __exception;
        }

        static void NoteOnce(string observation)
        {
            // At most one note per form/predicate/outcome/readiness combination,
            // additionally capped per registration/session. No unit IDs, player
            // information, exception dumps or game-state payloads are recorded.
            try
            {
                lock(ObservationLock)
                {
                    if(Observed.Count>=MaxObservations || !Observed.Add(observation))return;
                }
                Entry.Note("ordinary-action-native-predicate",observation);
            }
            catch
            {
                // Diagnostic failure must not replace a native return/exception.
            }
        }
    }
}
