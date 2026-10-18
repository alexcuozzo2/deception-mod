using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // UI is not authority. Every concrete native command validator/executor
    // with a source tile is guarded, including AI and stale queued commands.
    // Automatic actions/retaliation remain separate from issued orders.
    internal static class NativeBlackoutAccess
    {
        internal static readonly string[] Commands={"AttackCommand","BoostCommand","BreakIceCommand","BreakPeaceCommand","BuildCommand","CaptureCommand",
            "CityRewardCommand","ClearTileEffectCommand","DecomposeCommand","DestroyCommand","DestroyEmbassyCommand","DisbandCommand","DisembarkCommand",
            "EstablishEmbassyCommand","ExamineRuinsCommand","ExplodeCommand","FloodCommand","FreezeAreaCommand","HealOthersCommand","HideCommand",
            "MoveCommand","PeaceTreatyCommand","PromoteCommand","RecoverCommand","StayCommand","SwarmCommand","TrainCommand","UpgradeCommand"};
        internal static void Install(Harmony h)
        {
            foreach(string command in Commands)
            {
                Native.Hook(h,Native.M(command,"IsValid","GameState","System.String&"),typeof(NativeBlackoutAccess),nameof(BeforeValid));
                Native.Hook(h,Native.M(command,"Execute","GameState"),typeof(NativeBlackoutAccess),nameof(BeforeExecute));
            }
            Native.Hook(h,Native.M("BattleHelpers","CanDefenderRetaliate","GameState","UnitState","UnitState"),typeof(NativeBlackoutAccess),null,nameof(AfterRetaliation));
        }
        internal static bool Allowed(object command,object state,Type type)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return true;
            byte actor=Convert.ToByte(Native.Get(command,"PlayerId"));
            string field=type.Name=="AttackCommand"?"Origin":type.Name=="MoveCommand"?"From":"Coordinates";
            object at=Native.Get(command,field)!;
            if(!NativeBlackout.Covered(state,actor,NativeSpyNetwork.At(at)))return true;
            // A mandatory city reward settles an existing owner-only native
            // trigger. Blackout must not prevent Execute from popping that
            // trigger, otherwise the same choice reopens forever. Keep the
            // native current-player/pending-trigger/reward checks in charge;
            // this exception neither explores the tile nor grants unit orders.
            if(type.Name!="CityRewardCommand"||actor is 0 or byte.MaxValue||
                actor!=Native.Int(Native.Get(state,"CurrentPlayer")))return false;
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
            return tile is not null&&Native.Int(Native.Get(tile,"owner"))==actor&&
                (bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),tile,
                    Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!;
        }
        public static bool BeforeValid(object __instance,object __0,ref string __1,ref bool __result,MethodBase __originalMethod)
        {
            Type type=__originalMethod.DeclaringType!;
#if DECEPTION_WINDOWS_PORT_CANDIDATE
            if(NativeWindowsHooks.Enabled&&type.Name=="DisbandCommand")
            {
                __instance=NativeWindowsHooks.ValidationReceiver(__instance);type=__instance.GetType();
                // EndTurn shares this native body but has no source tile.
                // Preserve its original validation; it is not a blackout order.
                if(type.Name=="EndTurnCommand")return true;
            }
#endif
            if(Allowed(__instance,__0,type))return true;
            __result=false;__1="The tile is not visible.";return false;
        }
        public static bool BeforeExecute(object __instance,object __0,MethodBase __originalMethod)=>Allowed(__instance,__0,__originalMethod.DeclaringType!);
        public static void AfterRetaliation(object __0,object __1,object __2,ref bool __result)
        {
            if(!__result)return;
            // __1 is attacking unit, __2 defender (pinned native signature).
            // A covered defender may retaliate; only attacker visibility matters.
            byte side=Convert.ToByte(Native.Get(__2,"owner"));
            object at=Native.Get(__1,"coordinates")!;
            if(NativeBlackout.Covered(__0,side,NativeSpyNetwork.At(at)))__result=false;
        }
    }
}
