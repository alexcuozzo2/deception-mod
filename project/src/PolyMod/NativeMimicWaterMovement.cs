using System;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Early Mimic recruitment must not gain unresearched water through Swim.
    // Flight keeps native traversal; placement/exchange queries stay native.
    internal static class NativeMimicWaterMovement
    {
        const int MaxTiles=4096;
        const string Terrain="Polytopia.Data.TerrainData";
        [ThreadStatic] static Scope? Current;
        static long Generation;
        // Only the movement adapter can admit its exact, short-lived detached
        // real-body query. Ordinary detached/foreign units remain rejected.
        internal static Func<object,object,bool>? DetachedQuery;
        internal static Func<object,object,object,object?,object?>? ExtendOptions;
        internal static Func<object,object,object,object,object?,object?>? ExtendPath;
        internal static Func<object,object,object,object,bool>? FallbackRoute;
        internal static void Clear(){System.Threading.Interlocked.Increment(ref Generation);Current=null;}
        internal sealed class Scope
        {
            internal readonly Scope? Previous;
            internal readonly object State,Origin;
            internal readonly object? Unit,Map;
            internal readonly long GenerationToken;
            internal readonly byte Owner;
            internal readonly bool Mimic,Bound,Shallow,Deep,Swim,Wetland,Detached;
            internal bool Closed;
            internal Scope(object state,object? unit,object origin,bool apparentQuery=false)
            {
                Previous=Current;State=state;Unit=unit;Origin=origin;GenerationToken=System.Threading.Volatile.Read(ref Generation);
                Mimic=unit is not null&&(apparentQuery||RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(unit,"type")!)));
                if(!Mimic||unit is null)return;
                Map=Native.Get(state,"Map");
                if(!RecruitmentHooks.Registered||!NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(state))return;
                Detached=!apparentQuery&&DetachedQuery?.Invoke(state,unit)==true;
                if(!apparentQuery&&!Detached)
                {
                    uint id=Convert.ToUInt32(Native.Get(unit!,"id"));
                    object?[] bodies={id,null};
                    if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,bodies)!||!Native.Same(bodies[1],unit))return;
                    var tile=Tile(state,origin);
                    if(tile is null||!Native.Same(Native.Get(tile,"unit"),unit)||!At(origin,Native.Get(unit,"coordinates")!))return;
                    var row=NativeRegroupSession.ReadIdentity(state,id);
                    if(row is not {Alive:true,Independent:true,Kind:LedgerActualKind.Mimic}||row.NativeId!=id)return;
                }
                byte owner=Owner=Convert.ToByte(Native.Get(unit,"owner"));object?[] players={owner,null};
                if(!(bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,players)!||players[1] is not object player||Convert.ToByte(Native.Get(player,"Id"))!=owner)return;
                // An exposed Mermimic uses its native Mermaid Warrior-derived
                // amphibious rules. The extra ordinary Mimic boat-tech filter
                // must not erase those native permissions. All native terrain
                // and route predicates still run for this exact profile.
                if(Native.EnumName(Native.Get(unit,"type")!)==AquarionCatalog.MimicId)
                {Shallow=Deep=true;Bound=true;return;}
                bool flying=(bool)Native.Invoke(Native.M("UnitDataExtensions","HasAbility","UnitState","Polytopia.Data.UnitAbility+Type"),null,unit,Native.EnumValue("Polytopia.Data.UnitAbility+Type","fly"))!;
                if(flying){Shallow=Deep=true;Bound=true;return;}
                Swim=(bool)Native.Invoke(Native.M("UnitDataExtensions","HasAbility","UnitState","Polytopia.Data.UnitAbility+Type"),null,unit,Native.EnumValue("Polytopia.Data.UnitAbility+Type","swim"))!;
                var movements=Native.Invoke(Native.M(RecruitmentHooks.GLD,"GetUnlockedMovements","PlayerState"),Native.Get(state,"GameLogicData"),player)!;
                var terrain=Bounded(movements).Select(t=>Native.EnumName(Native.Get(t,"type")!)).ToArray();
                Shallow=terrain.Contains("water",StringComparer.Ordinal);Deep=terrain.Contains("ocean",StringComparer.Ordinal);Bound=true;
                if(!Shallow||!Deep)
                {
                    var nativeTerrain=Native.Invoke(Native.M("UnitDataExtensions","GetAllowedTerrain","UnitState","GameState"),null,unit,state)!;
                    Wetland=Bounded(nativeTerrain).Any(t=>Native.EnumName(Native.Get(t,"type")!)=="wetland");
                }
            }
            internal bool Restricted=>Mimic&&(!Bound||!Shallow||!Deep);
            internal bool Valid=>GenerationToken==System.Threading.Volatile.Read(ref Generation)&&Bound&&NativeRegroupSession.Enabled&&NativeRegroupSession.Ready(State)&&
                Native.Same(Map,Native.Get(State,"Map"))&&Convert.ToByte(Native.Get(Unit!,"owner"))==Owner&&
                At(Origin,Native.Get(Unit!,"coordinates")!)&&(Detached?DetachedQuery?.Invoke(State,Unit!)==true:Tile(State,Origin) is object tile&&Native.Same(Native.Get(tile,"unit"),Unit));
            internal bool Allows(object at)
            {
                if(!Bound)return false;
                var tile=Tile(State,at);if(tile is null)return false;
                // Native cities are accessible before their raw terrain check;
                // a flooded/coastal city still admits the real land fallback.
                if((bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),tile,
                    Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!)return true;
                if(AllowsTerrain(Native.EnumName(Native.Get(tile,"terrain")!)))return true;
                // Native pathfinding accepts flooded/algae Wetland and bridge
                // terrain alternatives before its ordinary terrain comparison.
                // Keep those alternatives; a port alone is not a bridge.
                return (Wetland&&(bool)Native.Invoke(Native.M("TileData","IsWetland"),tile)!)||Bridge(State,tile);
            }
            internal bool AllowsTerrain(string terrain)=>terrain switch{"water"=>Shallow,"ocean"=>Deep,_=>true};
        }
        // Read-only water research check for a detached apparent profile. This
        // does not publish Current or make a detached body valid for any live
        // path/command hook; all authoritative callers retain exact root checks.
        internal static bool AllowsApparentRoute(object state,object profile,object[] reversePath)
        {
            if(reversePath.Length==0||reversePath.Length>MaxTiles)return false;
            var scope=new Scope(state,profile,reversePath[^1],apparentQuery:true);
            // Native IsTileAccessible admits cities before raw terrain. This
            // comparison still checks native accessibility/cost separately;
            // avoid a false reveal for a city built over water/flooded land.
            return scope.Bound&&reversePath.All(at=>scope.Allows(at)||Tile(state,at) is object tile&&
                (bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),tile,
                    Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!);
        }
        internal static void Install(Harmony h)
        {
            // These overloads contain only reference wrappers, Int32, and the
            // genuine eight-byte WorldCoordinates struct. Never hook settings.
            Native.Hook(h,Native.M("PathFinder","GetMoveOptions","GameState","WorldCoordinates","System.Int32","UnitState"),typeof(NativeMimicWaterMovement),nameof(BeforeOptions),nameof(AfterOptions),nameof(Finished));
            Native.Hook(h,Native.M("PathFinder","GetPath","GameState","WorldCoordinates","WorldCoordinates","System.Int32","UnitState"),typeof(NativeMimicWaterMovement),nameof(BeforePath),nameof(AfterPath),nameof(Finished));
            Native.Hook(h,Native.M("UnitDataExtensions","GetAllowedTerrain","UnitState","GameState"),typeof(NativeMimicWaterMovement),null,nameof(AfterTerrain));
            Native.Hook(h,Native.M("MoveCommand","IsValid","GameState","System.String&"),typeof(NativeMimicWaterMovement),null,nameof(AfterMoveValid));
        }
        static object? Tile(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static bool Bridge(object state,object tile)
        {
            var improvement=Native.Get(tile,"improvement");if(improvement is null)return false;
            object?[] args={Native.Get(improvement,"type"),null};
            return (bool)Native.Invoke(Native.M(RecruitmentHooks.GLD,"TryGetData","Polytopia.Data.ImprovementData+Type","Polytopia.Data.ImprovementData&"),Native.Get(state,"GameLogicData"),args)!&&args[1] is object data&&
                (bool)Native.Invoke(Native.M("ImprovementDataExtensions","HasAbility","Polytopia.Data.ImprovementData","Polytopia.Data.ImprovementAbility+Type"),null,data,Native.EnumValue("Polytopia.Data.ImprovementAbility+Type","bridge"))!;
        }
        static bool At(object a,object b)=>Native.Int(Native.Get(a,"X"))==Native.Int(Native.Get(b,"X"))&&Native.Int(Native.Get(a,"Y"))==Native.Int(Native.Get(b,"Y"));
        static object[] Bounded(object list)
        {
            int count=Native.Int(Native.Get(list,"Count"));
            if(count<0||count>MaxTiles)throw new InvalidOperationException("Mimic movement exceeds the map bound.");
            return Native.Items(list).ToArray();
        }
        static object Empty()=>Native.NewList("WorldCoordinates",Array.Empty<object>());
        static bool Begin(object state,object origin,object? unit,out Scope __state,ref object? result)
        {
            __state=new Scope(state,unit,origin);Current=__state;
            // An exchanged early Island may legally occupy locked water. It
            // cannot start a move there until that terrain is researched.
            if(!__state.Mimic||(__state.Valid&&__state.Allows(origin)))return true;
            result=Empty();return false;
        }
        public static bool BeforeOptions(object __0,object __1,object? __3,out Scope __state,ref object? __result)=>Begin(__0,__1,__3,out __state,ref __result);
        public static bool BeforePath(object __0,object __1,object __2,object? __4,out Scope __state,ref object? __result)
        {
            bool run=Begin(__0,__1,__4,out __state,ref __result);
            if(run&&__state.Mimic&&!__state.Allows(__2)){__result=Empty();return false;}
            return run;
        }
        public static Exception? Finished(Exception? __exception,Scope? __state)
        {
            if(__state is not null&&!__state.Closed)
            {__state.Closed=true;if(ReferenceEquals(Current,__state))Current=__state.GenerationToken==System.Threading.Volatile.Read(ref Generation)?__state.Previous:null;}
            return __exception;
        }
        public static void AfterTerrain(object __0,object __1,ref object? __result)
        {
            var scope=Current;
            if(__result is null||scope is null||!scope.Restricted||!Native.Same(scope.State,__1)||!Native.Same(scope.Unit,__0))return;
            var original=Bounded(__result);
            bool valid=scope.Valid;
            var allowed=original.Where(t=>valid&&scope.AllowsTerrain(Native.EnumName(Native.Get(t,"type")!))).ToArray();
            if(allowed.Length!=original.Length)__result=Native.NewList(Terrain,allowed);
        }
        static bool Route(Scope scope,object destination,object? path)
        {
            if(path is null||!scope.Valid||!scope.Allows(scope.Origin)||!scope.Allows(destination))return false;
            var steps=Bounded(path);return steps.Length>0&&steps.All(scope.Allows);
        }
        public static void AfterPath(object __2,Scope __state,ref object? __result)
        {
            if(__state.Mimic&&(!__state.Valid||(__state.Restricted&&!Route(__state,__2,__result))))__result=Empty();
            if(__state.Mimic&&__state.Valid&&!__state.Detached&&ExtendPath is not null)
                __result=ExtendPath(__state.State,__state.Origin,__2,__state.Unit!,__result);
        }
        public static void AfterOptions(object __0,object __1,int __2,object __3,Scope __state,ref object? __result)
        {
            if(__result is null||!__state.Mimic)return;
            if(!__state.Restricted&&__state.Valid){if(!__state.Detached&&ExtendOptions is not null)__result=ExtendOptions(__0,__1,__3,__result);return;}
            var original=Bounded(__result);
            bool valid=__state.Valid;
            // Ordinary land appearances have no Swim shortcut. Their native
            // allowed-terrain search already excludes locked water; avoid one
            // extra path search per land option on every hover.
            var checkedRoutes=new System.Collections.Generic.Dictionary<(int,int),bool>();
            bool PathAllowed(object at)
            {
                if(!__state.Swim||At(at,__1))return true;
                var key=(Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")));
                if(!checkedRoutes.TryGetValue(key,out bool allowed))checkedRoutes[key]=allowed=Route(__state,at,
                    Native.Invoke(Native.M("PathFinder","GetPath","GameState","WorldCoordinates","WorldCoordinates","System.Int32","UnitState"),null,__0,__1,at,__2,__3));
                return allowed;
            }
            var allowed=original.Where(at=>valid&&__state.Allows(__1)&&__state.Allows(at)&&
                PathAllowed(at)).ToArray();
            if(allowed.Length!=original.Length)__result=Native.NewList("WorldCoordinates",allowed);
            if(__state.Valid&&!__state.Detached&&ExtendOptions is not null)
                __result=ExtendOptions(__0,__1,__3,__result);
        }
        public static void AfterMoveValid(object __instance,object __0,ref bool __result,ref string __1)
        {
            if(!__result)return;
            object?[] bodies={Convert.ToUInt32(Native.Get(__instance,"UnitId")),null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),__0,bodies)!||bodies[1] is not object unit)return;
            var scope=new Scope(__0,unit,Native.Get(__instance,"From")!);
            if(!scope.Restricted)return;
            var destination=Native.Get(__instance,"To")!;
            int movement=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMovement","UnitState","GameState"),null,unit,__0));
            if(scope.Bound&&Convert.ToByte(Native.Get(__instance,"PlayerId"))==Convert.ToByte(Native.Get(unit,"owner"))&&
                (Route(scope,destination,Native.Invoke(Native.M("PathFinder","GetPath","GameState","WorldCoordinates","WorldCoordinates","System.Int32","UnitState"),null,__0,scope.Origin,destination,movement,unit))||
                 FallbackRoute?.Invoke(__0,unit,scope.Origin,destination)==true))return;
            __result=false;__1="This appearance requires water movement research for this route.";
        }
    }
}
