using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Recomputed for offers, targeting and commit. Only the initiating genuine
    // specialist's range-two area counts; no neighboring unit extends it.
    internal static class NativeSpyNetwork
    {
        internal const int MaxTiles=8192;
        internal readonly record struct Position(int X,int Y);
        static void Require(bool yes,string why){if(!yes)throw new InvalidDataException(why);}
        internal static Position At(object value)=>new(Native.Int(Native.Get(value,"X")),Native.Int(Native.Get(value,"Y")));
        internal static Dictionary<Position,object> Tiles(object state)
        {
            object map=Native.Get(state,"Map")??throw new InvalidDataException("Spy network map is absent.");
            object array=Native.Get(map,"Tiles")??throw new InvalidDataException("Spy network tiles are absent.");
            int count=Native.Int(Native.Get(array,"Length"));Require(count>0&&count<=MaxTiles,"Spy network map exceeds its native bound.");
            var item=Native.M(array.GetType(),"get_Item","System.Int32");var result=new Dictionary<Position,object>();
            for(int i=0;i<count;i++)
            {
                object tile=Native.Invoke(item,array,i)??throw new InvalidDataException("Spy network has a null tile.");
                var at=At(Native.Get(tile,"coordinates")!);
                Require(result.TryAdd(at,tile),"Spy network has duplicate coordinates.");
            }
            return result;
        }
        internal static object? Unit(object state,uint id)
        {
            object?[] args={id,null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,args)!?args[1]:null;
        }
        internal static bool Accessible(object state,object tile,byte actor)=>
            !NativeBlackout.Covered(state,actor,At(Native.Get(tile,"coordinates")!))&&
            (bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,actor)!;
        internal static bool Node(object state,object tile,object unit,byte actor)
            =>Node(state,tile,unit,actor,false);
        internal static bool FoggerNode(object state,object tile,object unit,byte actor)
            =>Node(state,tile,unit,actor,true);
        static bool Node(object state,object tile,object unit,byte actor,bool fogger)
        {
            if(!RecruitmentHooks.Registered)return false;
            string id=Native.EnumName(Native.Get(unit,"type")!);
            if(!(fogger?FoggerCatalog.IsFoggerId(id):SpyCatalog.IsSpyId(id))||
                Native.Int(Native.Get(unit,"health"))<=0||!NativeRogueRules.CanCommand(state,unit,actor)||
                !Native.Same(Native.Get(tile,"unit"),unit)||At(Native.Get(tile,"coordinates")!)!=At(Native.Get(unit,"coordinates")!)||
                !Accessible(state,tile,actor))return false;
            // Never discover a node from an appearance, a passenger, stale
            // selection, a foreign unit table, or a compound follower.
            uint nativeId=Convert.ToUInt32(Native.Get(unit,"id"));
            return Native.Same(unit,Unit(state,nativeId))&&!(bool)Native.Invoke(Native.M("UnitState","HasLeader"),unit)!&&
                !(bool)Native.Invoke(Native.M("UnitState","HasFollower"),unit)!;
        }
        internal static bool ActionReady(object state,object unit,byte actor)
        {
            // A genuine Spy chooses movement OR a specialist action. Check both
            // flags even during native move processing, before attack exhaustion
            // has been refreshed; a disguise cannot grant the real Spy Dash.
            return NativeRogueRules.CanCommand(state,unit,actor)&&!(bool)Native.Get(unit,"moved")!&&!(bool)Native.Get(unit,"attacked")!&&
                !(bool)Native.Invoke(Native.M("UnitState","HasEffect","UnitEffect"),unit,Enum.Parse(Native.T("UnitEffect"),"Frozen"))!;
        }
        internal static bool TryReachable(object state,byte actor,uint sourceId,out object[] targets,out string reason)
            =>TryReachable(state,actor,sourceId,false,out targets,out reason);
        internal static bool TryFoggerReachable(object state,byte actor,uint sourceId,out object[] targets,out string reason)
            =>TryReachable(state,actor,sourceId,true,out targets,out reason);
        static bool TryReachable(object state,byte actor,uint sourceId,bool fogger,out object[] targets,out string reason)
        {
            targets=Array.Empty<object>();reason="";
            try
            {
                Require(NativeRegroupSession.Ready(state)&&RecruitmentHooks.Registered,"Spy network session is unavailable.");
                Require(Native.Int(Native.Get(state,"CurrentPlayer"))==actor&&
                    Equals(Native.Get(state,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started")),"Spy network is outside the commanding turn.");
                var tiles=Tiles(state);
                object source=Unit(state,sourceId)??throw new InvalidDataException("Initiating Spy is absent.");
                var origin=At(Native.Get(source,"coordinates")!);
                Require(tiles.TryGetValue(origin,out var sourceTile)&&Node(state,sourceTile,source,actor,fogger),"Initiating unit is not an accessible genuine specialist.");
                Require(fogger?NativeFoggerActions.Ready(state,source,actor):ActionReady(state,source,actor),"Initiating unit has no specialist action.");
                var area=new HashSet<Position>();
                // Use the same native diagonal geometry as before, exactly once.
                // An allied, Rogue, moved or exhausted Spy is never a range node.
                object nativeArea=Native.Invoke(Native.M("MapDataExtensions","GetAreaSorted","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),
                    null,Native.Get(state,"Map"),Native.Get(sourceTile!,"coordinates"),2,true,true)!;
                int count=Native.Int(Native.Get(nativeArea,"Length"));Require(count is >0 and <=25,"Native Spy range exceeds its bound.");
                var item=Native.M(nativeArea.GetType(),"get_Item","System.Int32");
                for(int i=0;i<count;i++)
                {
                    object tile=Native.Invoke(item,nativeArea,i)??throw new InvalidDataException("Native Spy range contains a null tile.");
                    var p=At(Native.Get(tile,"coordinates")!);
                    Require(tiles.TryGetValue(p,out var authoritativeTile)&&Native.Same(authoritativeTile,tile),"Native Spy range contains a foreign tile.");
                    area.Add(p);
                }
                targets=area.OrderBy(p=>p.Y).ThenBy(p=>p.X).Select(p=>tiles[p]).ToArray();return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
    }
}
