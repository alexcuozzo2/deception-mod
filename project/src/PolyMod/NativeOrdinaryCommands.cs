using System;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native GetUnitActions puts Disband behind an outer CanAttack test. Our
    // offense bans must remain active there: append just the eligible native
    // command, rather than enabling the other powers behind that same test.
    internal static class NativeOrdinaryCommands
    {
        internal static void Install(Harmony h)
        {
            var p=typeof(NativeOrdinaryCommands);
            Native.Hook(h,Native.M("CommandUtils","GetUnitActions","GameState","PlayerState","TileData","System.Boolean"),p,null,nameof(DisbandMenu));
            Native.Hook(h,Native.M("DisbandCommand","IsValid","GameState","System.String&"),p,null,nameof(DisbandValid));
            Native.Hook(h,Native.M("DisbandCommand","Execute","GameState"),p,nameof(DisbandExecute));
            Native.Hook(h,Native.M("DisbandUnitAction","Execute","GameState"),p,nameof(DisbandAction));
            Native.Hook(h,Native.M("CaptureCommand","Execute","GameState"),p,nameof(CaptureExecute));
        }
        internal static bool Custom(object? unit)=>unit is not null&&RecruitmentHooks.Registered&&
            (RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(unit,"type")!))||SpyCatalog.IsSpyId(Native.EnumName(Native.Get(unit,"type")!))||FoggerCatalog.IsFoggerId(Native.EnumName(Native.Get(unit,"type")!)));
        static object? Tile(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static object? Body(object state,object at)=>Tile(state,at) is object tile?Native.Get(tile,"unit"):null;
        static object? Player(object state,byte id)
        {
            object?[] args={id,null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!?args[1]:null;
        }
        static bool Relevant(object state,object? unit)=>unit is not null&&(Custom(unit)||NativeRogueRules.IsRogue(state,unit));
        static bool Exact(object state,object unit,byte actor)
        {
            if(!RecruitmentHooks.Registered||!NativeRegroupSession.Ready(state)||
                Native.EnumName(Native.Get(state,"CurrentState")!)!="started"||Native.Int(Native.Get(unit,"health"))<=0||
                NativeRogueRules.IsRogue(state,unit)||!NativeRogueRules.CanCommand(state,unit,actor))return false;
            object at=Native.Get(unit,"coordinates")!;var tile=Tile(state,at);
            if(tile is null||!Native.Same(Native.Get(tile,"unit"),unit)||
                !(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,actor)!)return false;
            object?[] args={Convert.ToUInt32(Native.Get(unit,"id")),null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,args)!&&Native.Same(args[1],unit);
        }
        internal static bool CanDisband(object state,object unit,byte actor)
        {
            if(!Exact(state,unit,actor)||Player(state,actor) is not object player)return false;
            OrdinaryActionEligibility.EnterDisband(unit,out var scope);
            try
            {
                // Mirrors the audited native menu's outer readiness check.
                // CanDisband itself requires BOTH flags unused plus research.
                return (bool)Native.Invoke(Native.M("UnitState","CanAttack"),unit)!&&
                    (bool)Native.Invoke(Native.M("UnitDataExtensions","CanDisband","UnitState","GameState","PlayerState"),null,unit,state,player)!;
            }
            finally{OrdinaryActionEligibility.ExitPredicate(null,scope);}
        }
        static bool CommandAllowed(object command,object state)
        {
            var unit=Body(state,Native.Get(command,"Coordinates")!);
            return unit is not null&&CanDisband(state,unit,Convert.ToByte(Native.Get(command,"PlayerId")));
        }
        public static void DisbandMenu(object __0,object __1,object __2,ref object __result)
        {
            try
            {
                var unit=Native.Get(__2,"unit");if(!Custom(unit))return;
                byte actor=Convert.ToByte(Native.Get(__1,"Id"));
                if(!Native.Same(Tile(__0,Native.Get(__2,"coordinates")!),__2)||!CanDisband(__0,unit!,actor))return;
                var existing=Native.Items(__result).ToArray();
                if(existing.Any(x=>{try{Native.As(x,"DisbandCommand");return true;}catch{return false;}}))return;
                object command=Activator.CreateInstance(Native.T("DisbandCommand"),new[]{(object)actor,Native.Get(unit!,"coordinates")!})!;
                object?[] args={__0,null};
                if(!(bool)Native.Invoke(Native.M("DisbandCommand","IsValid","GameState","System.String&"),command,args)!)return;
                // Native lists may be cached/shared; own the returned copy.
                __result=Native.NewList("CommandBase",existing.Append(command));
            }
            catch(Exception e){Entry.Note("ordinary-disband-menu",e.Message);}
        }
        public static void DisbandValid(object __instance,object __0,ref string __1,ref bool __result)
        {
#if DECEPTION_WINDOWS_PORT_CANDIDATE
            if(NativeWindowsHooks.Enabled)
            {__instance=NativeWindowsHooks.ValidationReceiver(__instance);if(__instance.GetType().Name!="DisbandCommand")return;}
#endif
            if(!__result)return;
            var unit=Body(__0,Native.Get(__instance,"Coordinates")!);
            if(!Relevant(__0,unit))return;
            if(!CommandAllowed(__instance,__0)){__result=false;__1="This unit cannot disband now.";}
        }
        public static bool DisbandExecute(object __instance,object __0)
        {
            var unit=Body(__0,Native.Get(__instance,"Coordinates")!);
            if(!Relevant(__0,unit))return true;
            object?[] args={__0,null};
            return CommandAllowed(__instance,__0)&&
                (bool)Native.Invoke(Native.M("DisbandCommand","IsValid","GameState","System.String&"),__instance,args)!;
        }
        public static bool DisbandAction(object __instance,object __0)
        {
            // Elimination/wipe children are native unpaid removals, not the
            // player's Disband action; never prevent genuine elimination.
            if(!(bool)Native.Get(__instance,"GetPaid")!)return true;
            var unit=Body(__0,Native.Get(__instance,"Coordinates")!);
            if(!Relevant(__0,unit))return true;
            return Native.Items(Native.Get(__0,"ActionStack")!).Any(x=>Native.Same(x,__instance))&&CommandAllowed(__instance,__0);
        }
        public static bool CaptureExecute(object __instance,object __0)
        {
            var unit=Body(__0,Native.Get(__instance,"Coordinates")!);
            if(!Relevant(__0,unit))return true;
            object?[] args={__0,null};
            return Exact(__0,unit!,Convert.ToByte(Native.Get(__instance,"PlayerId")))&&
                (bool)Native.Invoke(Native.M("CaptureCommand","IsValid","GameState","System.String&"),__instance,args)!;
        }
    }
}
