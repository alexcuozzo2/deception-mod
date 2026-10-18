using System;
using System.IO;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // UpgradeAction creates a replacement using Action.PlayerId, then copies
    // the old physical id/home/actions. Native cloneState does not copy owner.
    // Owner-end growth can therefore rebuild a Rogue for its remembered owner
    // even though command ownership must remain with its current commander.
    internal static class NativeRogueUpgrade
    {
        [ThreadStatic] static Scope? Current;
        static long Generation;
        internal sealed record Scope(object State,object Tile,object Original,object TargetType,
            uint Id,ulong Logical,byte Owner,byte Commander,uint AcquiredTurn,bool Discovery,Scope? Parent,long Epoch)
        {
            internal NativeRogueSight.Collection? Sight;
            internal IDisposable? Source;
        }
        internal static void Install(Harmony harmony)
        {
            var type=typeof(NativeRogueUpgrade);
            Native.Hook(harmony,Native.M("UpgradeAction","Execute","GameState"),type,nameof(BeforeUpgrade),null,nameof(Finished));
            Native.Hook(harmony,Native.M("UnitState","cloneState","UnitState"),type,null,nameof(AfterClone));
            Native.Hook(harmony,Native.M("ActionUtils","ExploreFromTile","GameState","PlayerState","TileData","System.Int32","System.Boolean"),type,nameof(Discover));
        }
        internal static void Clear()
        {
            for(var scope=Current;scope is not null;scope=scope.Parent){scope.Source?.Dispose();scope.Source=null;}
            Current=null;Generation++;
        }
        static bool SameRogue(LedgerUnit? row,Scope scope)=>row is {Alive:true,Kind:LedgerActualKind.Genuine,Rogue:not null}&&
            row.LogicalId==scope.Logical&&row.NativeId==scope.Id&&row.Rogue.Owner==scope.Owner&&
            row.Rogue.Commander==scope.Commander&&row.Rogue.AcquiredTurn==scope.AcquiredTurn;
        public static void BeforeUpgrade(object __instance,object __0,out Scope? __state)
        {
            __state=null;if(!NativeRegroupSession.Ready(__0))return;
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,
                Native.Get(__0,"Map"),Native.Get(__instance,"Coordinates"));
            if(tile is null||Native.Get(tile,"unit") is not object unit)return;
            var row=NativeRogueRules.Row(__0,unit);
            if(row is not {Alive:true,Kind:LedgerActualKind.Genuine,Rogue:not null})return;
            byte actor=Convert.ToByte(Native.Get(__instance,"PlayerId"));
            if(actor!=row.Rogue.Commander&&actor!=row.Rogue.Owner)return;
            if(Convert.ToByte(Native.Get(unit,"owner"))!=row.Rogue.Commander||Native.Int(Native.Get(unit,"health"))<=0)return;
            __state=new(__0,tile,unit,Native.Get(__instance,"Type")!,
                Convert.ToUInt32(Native.Get(unit,"id")),row.LogicalId,row.Rogue.Owner,row.Rogue.Commander,row.Rogue.AcquiredTurn,
                actor==row.Rogue.Commander&&NativeRogueRules.CanCommand(__0,unit,actor)&&NativeSpyNetwork.Accessible(__0,tile,actor),Current,Generation);
            Current=__state;
            if(__state.Discovery)
            {
                __state.Sight=new();
                __state.Source=NativeRogueSight.ExchangeSource(__0,actor,tile,__state.Sight);
            }
        }
        // Native TrainUnit installs the new body on the tile before cloneState.
        // Restore immediately, before native discovery or other upgrade
        // postfixes reconcile the root/cargo owner invariant.
        public static void AfterClone(object __instance,object __0)
        {
            var scope=Current;
            if(scope is null||scope.Epoch!=Generation||!NativeRegroupSession.Ready(scope.State)||!Native.Same(__0,scope.Original)||
                !Native.Same(Native.Get(scope.Tile,"unit"),__instance)||
                Convert.ToUInt32(Native.Get(__instance,"id"))!=scope.Id||
                !Equals(Native.Get(__instance,"type"),scope.TargetType)||
                !SameRogue(NativeRogueRules.Row(scope.State,__instance),scope))return;
            NativeRogueRules.SetOwner(__instance,scope.Commander);
        }
        // This is the sole discovery call in the audited native upgrade body.
        // A real, currently commandable upgrade retains native local sight;
        // an idle owner-end growth cannot refresh either player's fog.
        public static bool Discover(object __0,object __1,object __2)
        {
            var scope=Current;
            if(scope is null||scope.Epoch!=Generation||!Native.Same(scope.State,__0)||!Native.Same(scope.Tile,__2))return true;
            return scope.Discovery&&Convert.ToByte(Native.Get(__1,"Id"))==scope.Commander;
        }
        public static Exception? Finished(Exception? __exception,Scope? __state)
        {
            if(__state is not Scope scope)return __exception;
            try
            {
                if(scope.Epoch!=Generation||!NativeRegroupSession.Ready(scope.State))return __exception;
                var unit=Native.Get(scope.Tile,"unit");
                // Never restore a retired body or overwrite a distinct arrival.
                // A native failed/no-op upgrade may still leave the old body.
                if(unit is null||Convert.ToUInt32(Native.Get(unit,"id"))!=scope.Id||
                    !SameRogue(NativeRogueRules.Row(scope.State,unit),scope))
                    throw new InvalidDataException("Rogue upgrade changed its physical identity or authority.");
                if(!Native.Same(unit,scope.Original)&&!Equals(Native.Get(unit,"type"),scope.TargetType))
                    throw new InvalidDataException("Rogue upgrade produced an unexpected replacement.");
                NativeRogueRules.SetOwner(unit,scope.Commander);
                scope.Source?.Dispose();scope.Source=null;
                if(__exception is null&&scope.Sight is not null)NativeRogueSight.CompleteCollection(scope.State,scope.Sight);
            }
            catch(Exception e)
            {
                NativeRegroupSession.Fault("Rogue upgrade ownership: "+e.Message);
                return __exception??e;
            }
            finally
            {
                scope.Source?.Dispose();scope.Source=null;
                if(ReferenceEquals(Current,scope))Current=scope.Epoch==Generation?scope.Parent:null;
            }
            return __exception;
        }
    }
}
