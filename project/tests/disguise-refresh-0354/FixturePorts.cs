// Synthetic audited native seams. This is not a Unity/IL2CPP game test.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace HarmonyLib
{
    public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo m){}}
    public sealed class Harmony
    {
        public List<string> Hooks=new();
        public void Patch(MethodInfo m,HarmonyMethod? pre,HarmonyMethod? post,object? t,HarmonyMethod? final,object? il)=>Hooks.Add(m.DeclaringType!.Name+"."+m.Name);
    }
}
namespace Il2CppSystem
{
    public class Object
    {
        static long Next;public IntPtr Pointer{get;}=new(++Next);
    }
}
namespace UnityEngine
{
    public class Object:Il2CppSystem.Object
    {public bool Destroyed;public static implicit operator bool(Object? o)=>o is not null&&!o.Destroyed;}
    public sealed class GameObject:Object{public bool activeInHierarchy{get;set;}=true;}
}
namespace Polytopia.Data
{
    public sealed class UnitData:Il2CppSystem.Object
    {
        public enum Type{warrior=1,polytaur=15,dcp_mimic_form_warrior=1026,dcp_mimic_form_polytaur=1045}
        public Type type{get;set;}
    }
}
public static class EnumCache<T>where T:struct,Enum
{public static string GetName(T t)=>t.ToString();public static T GetType(string s)=>Enum.Parse<T>(s);}
public sealed class WorldCoordinates:Il2CppSystem.Object
{public int X{get;set;}public int Y{get;set;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
public sealed class UnitState:Il2CppSystem.Object
{
    public uint id{get;set;}public byte owner{get;set;}=1;
    public Polytopia.Data.UnitData.Type type{get;set;}
    public Polytopia.Data.UnitData UnitData{get;set;}=new();
    public WorldCoordinates coordinates{get;set;}=new(0,0);
    public int health=10,Investment=3;public bool moved,attacked;public object Effects=new();
}
public sealed class TileData:Il2CppSystem.Object{public UnitState? unit{get;set;}}
public sealed class MapData:Il2CppSystem.Object{public Dictionary<(int,int),TileData> Tiles=new();}
public static class MapDataExtensions{public static TileData? GetTile(MapData m,WorldCoordinates at)=>m.Tiles.GetValueOrDefault((at.X,at.Y));}
public sealed class PlayerState:Il2CppSystem.Object{public byte Id{get;set;}=1;public int Tribe=13,Skin=0,Color=7;}
public sealed class ClientBase:Il2CppSystem.Object{public bool IsReplay{get;set;}public bool IsSpectating{get;set;}}
public sealed class GameState:Il2CppSystem.Object
{
    public MapData Map{get;set;}=new();public Il2CppSystem.Object GameLogicData{get;set;}=new();
    public Dictionary<uint,UnitState> Units=new();public bool TryGetUnit(uint id,out UnitState? unit)=>Units.TryGetValue(id,out unit);
}
public static class GameManager
{public static GameState? GameState{get;set;}public static ClientBase? Client{get;set;}public static PlayerState? LocalPlayer{get;set;}}
public class WorldObject:UnityEngine.Object
{public UnityEngine.GameObject gameObject{get;set;}=new();public Tile? Tile{get;set;}}
public sealed class Unit:WorldObject
{
    public UnitState UnitState{get;set;}=new();public Polytopia.Data.UnitData UnitData{get;set;}=new();
    public bool Visible=true,Invisible;public int DestroyCalls,PoolReturns;public string Failure="";public Action? Reenter;
    public bool GetVisible()=>Visible;public bool IsInvisibleForLocalPlayer(MapRenderContext context)=>Invisible;
    public void Destroy()
    {
        DestroyCalls++;Reenter?.Invoke();if(Failure=="before")throw new Exception("Injected native retirement error");
        if(Failure=="retains")return;
        MapRenderer.Current!.Units.Remove(UnitState.id);
        if(ReferenceEquals(Tile?.Unit,this))Tile!.Unit=null;
        if(ReferenceEquals(MapRenderer.Current.Selected,this))MapRenderer.Current.Selected=null;
        PoolReturns++;Destroyed=true;gameObject.activeInHierarchy=false;
        if(Failure=="after")throw new Exception("Injected error after native retirement");
    }
}
public sealed class Tile:UnityEngine.Object
{
    public UnityEngine.GameObject gameObject{get;set;}=new();
    public WorldCoordinates Coordinates{get;set;}=new(0,0);public TileData Data{get;set;}=new();public Unit? Unit{get;set;}
    public bool Hidden;public int OriginalRenders,Creates;public bool TileIsHidden(MapRenderContext context)=>Hidden;
    public void RenderUnit(MapRenderContext context)
    {
        if(MapRenderer.Callbacks)Deception.PolyModAdapter.NativeDisguiseRefresh.BeforeTile(this,context);
        OriginalRenders++;
        // Native code compares the old visual's shared UnitState to tile state,
        // not its separately cached UnitData. This recreates the observed gap.
        if(Unit is {Destroyed:false} old&&(Data.unit is null||old.UnitState.id!=Data.unit.id||old.UnitState.type!=Data.unit.type||old.UnitState.health==0))old.Destroy();
        if(Data.unit is not UnitState current)return;
        if(Unit is null||Unit.Destroyed)
        {
            Unit=MapRenderer.Current!.GetUnitInstance(current.id);
            if(Unit is null||Unit.Destroyed)
            {
                Unit=new(){UnitState=current,UnitData=current.UnitData,Tile=this};Creates++;
                MapRenderer.Current.Units.Add(current.id,Unit);
            }
        }
        Unit.Tile=this;
    }
}
public sealed class MapRenderer:UnityEngine.Object
{
    public UnityEngine.GameObject gameObject{get;set;}=new();public static MapRenderer? Current{get;set;}
    public static bool Callbacks;public Dictionary<uint,Unit> Units=new();public Dictionary<(int,int),Tile> Tiles=new();
    public Unit? Selected;
    public Unit? GetUnitInstance(uint id)=>Units.GetValueOrDefault(id);
    public Tile? GetTileInstance(WorldCoordinates at)=>Tiles.GetValueOrDefault((at.X,at.Y));
    public void Refresh(MapRenderContext context,IEnumerable<Tile> renderOrder)
    {
        // MapRenderer.Refresh runs this entire native position prepass BEFORE
        // rendering any tile. Both sides of a Swap have correct backlinks.
        foreach(var view in Units.Values.ToArray())
        {
            var destination=GetTileInstance(view.UnitState.coordinates)!;
            if(destination.Unit!=view){view.Tile=destination;destination.Unit=view;}
        }
        foreach(var tile in renderOrder)tile.RenderUnit(context);
    }
    public void OnDestroy()=>Deception.PolyModAdapter.NativeDisguiseRefresh.MapDestroying(this);
}
public sealed class MapRenderContext:Il2CppSystem.Object
{
    public MapData mapData{get;set;}=new();public Il2CppSystem.Object gld{get;set;}=new();
    public MapRenderer? unitRegistry{get;set;}public byte localPlayerId{get;set;}=1;
}
namespace Deception.PolyModAdapter
{
    internal static class Entry{public static List<(string Kind,string Text)> Notes=new();public static void Note(string kind,string text)=>Notes.Add((kind,text));}
    internal static class RecruitmentHooks
    {
        public static bool Registered=true;
        public static Dictionary<string,string> Forms=new(){{"dcp_mimic_form_warrior","warrior"},{"dcp_mimic_form_polytaur","polytaur"}};
    }
    internal static class NativeRegroupSession{public static bool IsReady=true;public static bool Ready(object state)=>IsReady&&ReferenceEquals(state,GameManager.GameState);}
}
