using System;
using System.Linq;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Stay on the audited GameState/UnitState overloads: the native inline
    // PathFinderSettings ABI must never be intercepted by this runtime.
    internal static class NativeRogueTactics
    {
        internal static Func<object,object,object?>? QuerySource;
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("PathFinder","GetPath","GameState","WorldCoordinates","WorldCoordinates","System.Int32","UnitState"),typeof(NativeRogueTactics),null,nameof(Path));
            Native.Hook(h,Native.M("PathFinder","GetMoveOptions","GameState","WorldCoordinates","System.Int32","UnitState"),typeof(NativeRogueTactics),null,nameof(Options));
        }
        static int Distance(object a,object b)=>Math.Max(Math.Abs(Native.Int(Native.Get(a,"X"))-Native.Int(Native.Get(b,"X"))),Math.Abs(Native.Int(Native.Get(a,"Y"))-Native.Int(Native.Get(b,"Y"))));
        static bool Relevant(object state,object unit)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return false;
            if(!Deception.Core.NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Units.Any(x=>x.Alive&&x.Rogue is not null))return false;
            return !(bool)Native.Invoke(Native.M("UnitDataExtensions","HasAbility","UnitState","Polytopia.Data.UnitAbility+Type"),null,unit,Native.EnumValue("Polytopia.Data.UnitAbility+Type","sneak"))!;
        }
        static bool Controlled(object state,object unit,object at,object? source=null)
        {
            object self=source??QuerySource?.Invoke(state,unit)??unit;
            bool rogue=NativeRogueRules.IsRogue(state,unit);byte actor=Convert.ToByte(Native.Get(unit,"owner"));
            var area=Native.Invoke(Native.M("MapDataExtensions","GetArea","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),null,Native.Get(state,"Map"),at,1,true,false)!;
            foreach(var tile in Native.Items(area))
            {
                if(!NativeSpyNetwork.Accessible(state,tile,actor))continue;
                var other=Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),tile,state,actor,false);
                if(other is not null&&!Native.Same(other,self)&&(rogue||NativeRogueRules.IsRogue(state,other)))return true;
            }
            return false;
        }
        // Joint disguise search calls the same rule for each interior tile.
        // Its detached appearance uses the exact live body for self-exclusion.
        internal static bool NeedsControlCheck(object state,object unit)=>Relevant(state,unit);
        internal static bool CanContinue(object state,object unit,object origin,object at,object source,bool knownRelevant=false)
            =>Distance(origin,at)==0||!knownRelevant&&!Relevant(state,unit)||!Controlled(state,unit,at,source);
        internal static bool Allowed(object state,object unit,object origin,object path)
        {
            var steps=Native.Items(path).ToArray();if(steps.Length<2)return true;
            // ReconstructPath returns destination first in the pinned engine.
            // Normalize defensively for detached native callers as well.
            if(Distance(steps[0],origin)>Distance(steps[^1],origin))Array.Reverse(steps);
            for(int i=0;i<steps.Length-1;i++)
                if(Distance(steps[i],origin)!=0&&Controlled(state,unit,steps[i]))return false;
            return true;
        }
        public static void Path(object __0,object __1,object __4,ref object? __result)
        {
            // Native GetPath returns null for an unreachable destination.
            // Preserve that absence before consulting or filtering Rogue ZOC.
            if(__result is not null&&Relevant(__0,__4)&&!Allowed(__0,__4,__1,__result))__result=Native.NewList("WorldCoordinates",Array.Empty<object>());
        }
        public static void Options(object __0,object __1,int __2,object __3,ref object? __result)
        {
            if(__result is null||!Relevant(__0,__3))return;
            var before=Native.Items(__result).ToArray();
            var after=before.Where(at=>Distance(at,__1)==0||Native.Invoke(Native.M("PathFinder","GetPath","GameState","WorldCoordinates","WorldCoordinates","System.Int32","UnitState"),null,__0,__1,at,__2,__3) is object path&&Native.Items(path).Any()).ToArray();
            if(after.Length!=before.Length)__result=Native.NewList("WorldCoordinates",after);
        }
    }
}
