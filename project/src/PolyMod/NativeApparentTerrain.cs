using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native MoveAction owns the timing, path, action queue and terrain checks.
    // A disguise supplies no terrain abilities. The existing narrow movement
    // scope reads the genuine body's native profile; true Mimics have none.
    // Stomp/Tentacles, AutoHeal and ordinary step-on effects stay native.
    internal static class NativeApparentTerrain
    {
        const int AutoFreeze=20,AutoFlood=45,Algae=51,FreezeArea=17;
        internal sealed record Scope(object State,object Unit,LedgerUnit Row,bool Ice,bool FreezeUnits,bool Flood,bool Algae,
            Scope? Previous,long Generation,HashSet<long>? PriorActions=null);
        [ThreadStatic] static Scope? Moving,Freezing,Flooding;
        static long Generation;
        internal static Func<object,object,bool>? PendingDisguiseShed;
        internal static void Clear(){Moving=Freezing=Flooding=null;Generation++;}
        internal static void Install(Harmony h)
        {
            var t=typeof(NativeApparentTerrain);
            Native.Hook(h,Native.M("MoveAction","Execute","GameState"),t,nameof(BeforeMove),nameof(AfterMove),nameof(MoveFinished));
            Native.Hook(h,Native.M("UnitDataExtensions","HasAbility","UnitState","Polytopia.Data.UnitAbility+Type"),t,null,nameof(Ability));
            Native.Hook(h,Native.M("FreezeAreaAction","Execute","GameState"),t,nameof(BeforeFreeze),null,nameof(FreezeFinished));
            Native.Hook(h,Native.M("TileData","IsFreezable","GameState","PlayerState","System.Boolean"),t,null,nameof(TileFreezable));
            Native.Hook(h,Native.M("UnitState","IsFreezable","GameState","PlayerState"),t,null,nameof(UnitFreezable));
            Native.Hook(h,Native.M("FloodTileAction","Execute","GameState"),t,nameof(BeforeFlood),null,nameof(FloodFinished));
            Native.Hook(h,Native.M("ActionUtils","CheckBubbleStep","TileData","UnitState","GameState"),t,nameof(BubbleStep));
        }
        static bool Has(object data,int ability)=>Native.Items(Native.Get(data,"unitAbilities")!).Any(x=>Native.Int(x)==ability);
        static object? Tile(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static bool Root(object state,object unit)=>Tile(state,Native.Get(unit,"coordinates")!) is object tile&&Native.Same(Native.Get(tile,"unit"),unit);
        static Scope? Profile(object state,object? unit,Scope? previous)
        {
            if(unit is null||!NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(state)||!Root(state,unit)||Native.Int(Native.Get(unit,"health"))<=0)return null;
            if(PendingDisguiseShed?.Invoke(state,unit)==true)return null;
            var row=NativeRegroupSession.ReadIdentity(state,Convert.ToUInt32(Native.Get(unit,"id")));
            if(row is not {Alive:true,Independent:true,Appearance:not null})return null;
            var actual=Native.Get(unit,"UnitData")!;
            bool genuine=row.Kind==LedgerActualKind.Genuine;
            bool freeze=genuine&&Has(actual,AutoFreeze);
            return new(state,unit,row,freeze,freeze,genuine&&Has(actual,AutoFlood),genuine&&Has(actual,Algae),previous,Generation);
        }
        static bool Live(Scope s,object state)=>s.Generation==Generation&&Native.Same(s.State,state)&&NativeRegroupSession.Ready(state);
        static IEnumerable<object> Actions(object state)
        {
            var stack=Native.Get(state,"ActionStack")!;
            int count=Native.Int(Native.Get(stack,"Count"));
            if(count<0||count>16384)throw new InvalidDataException("Terrain action queue exceeds its audited bound.");
            return Native.Items(stack);
        }
        public static void BeforeMove(object __instance,object __0,out Scope? __state)
        {
            __state=null;
            // Native Push also skips this whole passive block. Regroup/Disguise
            // exchanges never call MoveAction and gain no movement terrain.
            if(Native.Int(Native.Get(__instance,"Reason"))==2||!NativeRegroupSession.Ready(__0))return;
            object?[] args={Convert.ToUInt32(Native.Get(__instance,"UnitId")),null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),__0,args)!)return;
            var scope=Profile(__0,args[1],Moving);if(scope is null)return;
            __state=scope with{PriorActions=Actions(__0).Select(Native.Pointer).ToHashSet()};Moving=__state;
        }
        public static void Ability(object __0,object __1,ref bool __result)
        {
            int ability=Native.Int(__1);
            if(ability is not (AutoFreeze or AutoFlood or Algae)||Moving is not Scope s||!Live(s,s.State)||!Native.Same(__0,s.Unit))return;
            __result=ability==AutoFreeze?s.Ice||s.FreezeUnits:ability==AutoFlood?s.Flood:s.Algae;
        }
        public static void AfterMove(object __instance,object __0,Scope? __state)
        {
            if(__state is not Scope s||!Live(s,__0)||s.FreezeUnits||!s.Ice)return;
            // The native action has a serialized FreezeUnits field. Persist
            // the borrowed-terrain restriction before its queue can execute,
            // even if a subsequent native action removes the moving source.
            foreach(var queued in Actions(__0))
            {
                if(s.PriorActions!.Contains(Native.Pointer(queued))||Native.Int(Native.Invoke(Native.M("ActionBase","GetActionType"),queued))!=FreezeArea)continue;
                var freeze=Native.As(queued,"FreezeAreaAction");
                if(Convert.ToByte(Native.Get(freeze,"PlayerId"))!=Convert.ToByte(Native.Get(s.Unit,"owner"))||
                    Native.Int(Native.Get(freeze,"Radius"))!=1||(bool)Native.Get(freeze,"OnlyOwnedTiles")!||
                    !SameAt(Native.Get(freeze,"Coordinates")!,Native.Get(s.Unit,"coordinates")!))continue;
                Native.Set(freeze,"FreezeUnits",false);
            }
        }
        static bool SameAt(object a,object b)=>Native.Int(Native.Get(a,"X"))==Native.Int(Native.Get(b,"X"))&&Native.Int(Native.Get(a,"Y"))==Native.Int(Native.Get(b,"Y"));
        static Scope? AtAction(object action,object state,Scope? previous)
        {
            var tile=Tile(state,Native.Get(action,"Coordinates")!);var unit=tile is null?null:Native.Get(tile,"unit");
            if(unit is null||Convert.ToByte(Native.Get(action,"PlayerId"))!=Convert.ToByte(Native.Get(unit,"owner")))return null;
            return Profile(state,unit,previous);
        }
        public static void BeforeFreeze(object __instance,object __0,out Scope? __state)
        {
            __state=null;
            // Active FreezeArea commands reveal a genuine source beforehand;
            // only the native one-tile movement passive retains its mask here.
            if(Native.Int(Native.Get(__instance,"Radius"))!=1||(bool)Native.Get(__instance,"OnlyOwnedTiles")!)return;
            var s=AtAction(__instance,__0,Freezing);if(s is null||!(s.Ice||s.FreezeUnits))return;
            __state=s;Freezing=s;
        }
        public static void TileFreezable(object __0,ref bool __result)
        {if(__result&&Freezing is Scope s&&Live(s,__0)&&!s.Ice)__result=false;}
        public static void UnitFreezable(object __0,ref bool __result)
        {if(__result&&Freezing is Scope s&&Live(s,__0)&&!s.FreezeUnits)__result=false;}
        public static void BeforeFlood(object __instance,object __0,out Scope? __state)
        {
            __state=null;var s=AtAction(__instance,__0,Flooding);
            if(s is null||!s.Flood||s.Row.Kind==LedgerActualKind.Genuine&&Has(Native.Get(s.Unit,"UnitData")!,AutoFlood))return;
            __state=s;Flooding=s;
        }
        public static bool BubbleStep(object __0,object __1,object __2)
        {
            // FloodTile's tile/route updates remain native. Its embedded bubble
            // grant is a unit status and is forbidden for borrowed flooding.
            return Flooding is not Scope s||!Live(s,__2)||!Native.Same(__1,s.Unit)||!Native.Same(Native.Get(__0,"unit"),s.Unit);
        }
        public static Exception? MoveFinished(Exception? __exception,Scope? __state)
        {if(__state is not null)Moving=__state.Generation==Generation?__state.Previous:null;return __exception;}
        public static Exception? FreezeFinished(Exception? __exception,Scope? __state)
        {if(__state is not null)Freezing=__state.Generation==Generation?__state.Previous:null;return __exception;}
        public static Exception? FloodFinished(Exception? __exception,Scope? __state)
        {if(__state is not null)Flooding=__state.Generation==Generation?__state.Previous:null;return __exception;}
    }
}
