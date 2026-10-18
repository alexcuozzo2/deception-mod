using System;
using System.Collections.Generic;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native Tile.RenderUnit compares two UnitState references. Regroup changes
    // that same state in place, so it cannot notice the old Unit prefab/data.
    // Retire only the proven stale visual through native Unit.Destroy; the
    // original tile renderer then constructs the current carrier normally.
    internal static class NativeDisguiseRefresh
    {
        static object? Map,Game;
        static bool Retiring;
        readonly record struct Attempt(long View,long State,uint Id,string Form,long Data);
        static readonly HashSet<Attempt> Failed=new();
        static int Traces;
        internal static void Clear(){Map=null;Game=null;Retiring=false;Failed.Clear();Traces=0;}
        internal static void Install(Harmony harmony)
        {
            foreach(var method in new[]{Native.M("Tile","RenderUnit","MapRenderContext"),
                Native.M("Tile","get_Coordinates"),Native.M("Tile","get_Data"),Native.M("Tile","get_Unit"),
                Native.M("Tile","TileIsHidden","MapRenderContext"),Native.M("WorldObject","get_Tile"),
                Native.M("Unit","get_UnitData"),Native.M("Unit","get_UnitState"),Native.M("Unit","Destroy"),
                Native.M("Unit","GetVisible"),Native.M("Unit","IsInvisibleForLocalPlayer","MapRenderContext"),
                Native.M("MapRenderContext","get_mapData"),Native.M("MapRenderContext","get_gld"),
                Native.M("MapRenderContext","get_unitRegistry"),Native.M("MapRenderContext","get_localPlayerId"),
                Native.M("MapRenderer","get_Current"),Native.M("MapRenderer","GetUnitInstance","System.UInt32"),
                Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),Native.M("MapRenderer","OnDestroy"),
                Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),
                Native.M("GameState","TryGetUnit","System.UInt32","UnitState&")}){_=method;}
            Native.Hook(harmony,Native.M("Tile","RenderUnit","MapRenderContext"),typeof(NativeDisguiseRefresh),nameof(BeforeTile));
            Native.Hook(harmony,Native.M("MapRenderer","OnDestroy"),typeof(NativeDisguiseRefresh),nameof(MapDestroying));
        }
        public static void MapDestroying(object __instance){if(Native.Same(Map,__instance))Clear();}
        static bool SameCoordinates(object a,object b)=>Native.Int(Native.Get(a,"X"))==Native.Int(Native.Get(b,"X"))&&
            Native.Int(Native.Get(a,"Y"))==Native.Int(Native.Get(b,"Y"));
        static bool Active(object value)=>Native.UnityAlive(value)&&
            (bool)Native.Get(Native.Get(value,"gameObject")!,"activeInHierarchy")!;
        static bool RegisteredTile(object map,object tile)=>Active(tile)&&
            Native.Same(Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,Native.Get(tile,"Coordinates")),tile);
        public static void BeforeTile(object __instance,object __0)
        {
            if(Retiring||!RecruitmentHooks.Registered||__0 is null)return;
            object? candidate=null;Attempt? attempt=null;
            try
            {
                var game=Native.Static("GameManager","GameState");var map=Native.Static("MapRenderer","Current");
                var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");
                if(game is null||!Active(map!)||client is null||player is null||
                    (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!||
                    !Native.Same(Native.Get(__0,"mapData"),Native.Get(game,"Map"))||
                    !Native.Same(Native.Get(__0,"gld"),Native.Get(game,"GameLogicData"))||
                    !Native.Same(Native.Get(__0,"unitRegistry"),map)||
                    Native.Int(Native.Get(__0,"localPlayerId"))!=Native.Int(Native.Get(player,"Id")))return;
                if(!Native.Same(Map,map)||!Native.Same(Game,game)){Clear();Map=map;Game=game;}
                if(Failed.Count>=256||!RegisteredTile(map!,__instance))return;
                var tileData=Native.Get(__instance,"Data");var state=tileData is null?null:Native.Get(tileData,"unit");
                if(state is null)return;
                string current=Native.EnumName(Native.Get(state,"type")!);
                if(!RecruitmentHooks.Forms.ContainsKey(current))return;
                uint id=Convert.ToUInt32(Native.Get(state,"id"));
                candidate=Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,id);
                if(!Active(candidate!)||!Native.Same(Native.Get(candidate!,"UnitState"),state))return;
                var previousData=Native.Get(candidate!,"UnitData");
                if(previousData is null)return;
                string previous=Native.EnumName(Native.Get(previousData,"type")!);
                if(previous==current||!RecruitmentHooks.Forms.ContainsKey(previous))return;
                attempt=new(Native.Pointer(candidate!),Native.Pointer(state),id,current,Native.Pointer(previousData));
                if(Failed.Contains(attempt.Value))return;
                // The destination may still point to the partner's old visual
                // during Swap. Locate by ID, and validate the candidate's own
                // prior tile backlink instead of retiring that unrelated view.
                var previousTile=Native.Get(candidate!,"Tile");
                var at=Native.Get(__instance,"Coordinates")!;
                object?[] lookup={id,null};
                if(!NativeRegroupSession.Ready(game)||!RegisteredTile(map!,previousTile!)||
                    !Native.Same(Native.Get(previousTile!,"Unit"),candidate)||
                    !(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),game,lookup)!||
                    !Native.Same(lookup[1],state)||!SameCoordinates(Native.Get(state,"coordinates")!,at)||
                    !Native.Same(Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(game,"Map"),at),tileData)||
                    Native.EnumName(Native.Get(Native.Get(state,"UnitData")!,"type")!)!=current||
                    (bool)Native.Invoke(Native.M("Tile","TileIsHidden","MapRenderContext"),__instance,__0)!||
                    !(bool)Native.Invoke(Native.M("Unit","GetVisible"),candidate)!||
                    (bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer","MapRenderContext"),candidate,__0)!)return;
                // Latch before native teardown. A failed teardown must never
                // repeat on every render. Normal native rendering still runs.
                Failed.Add(attempt.Value);Retiring=true;
                try
                {
                    Native.Invoke(Native.M("Unit","Destroy"),candidate);
                    if(Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,id),candidate)||
                        Native.Same(Native.Get(previousTile!,"Unit"),candidate))
                        throw new InvalidOperationException("Native stale visual retirement retained a registry or tile reference.");
                    Failed.Remove(attempt.Value);
                    if(Traces++<24)Entry.Note("mimic-disguise-prefab-refresh","unit="+id+" "+previous+" -> "+current+
                        "; native visual retired; original Tile.RenderUnit creates the current form.");
                }
                finally{Retiring=false;}
            }
            catch(Exception error)
            {
                if(attempt is Attempt failed)Failed.Add(failed);
                if(Traces++<24)Entry.Note("mimic-disguise-prefab-refresh-error",error.ToString());
            }
        }
    }
}
