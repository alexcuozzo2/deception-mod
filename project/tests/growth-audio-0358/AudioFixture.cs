// Synthetic native ABI and presentation seams; no Unity/IL2CPP execution.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
namespace HarmonyLib
{
    public sealed class HarmonyMethod{public MethodInfo Method;public HarmonyMethod(MethodInfo m){Method=m;}}
    public sealed class Harmony
    {
        public List<string> Hooks=new();
        public void Patch(MethodInfo m,HarmonyMethod? pre,HarmonyMethod? post,object? t,HarmonyMethod? final,object? il)=>Hooks.Add(m.DeclaringType!.Name+"."+m.Name+":"+pre?.Method.Name+":"+post?.Method.Name);
    }
}
namespace Il2CppSystem
{
    public class Object{static long Next;public IntPtr Pointer{get;}=new(++Next);}
    public class Action:Object{}
}
namespace UnityEngine
{
    public class Object:Il2CppSystem.Object
    {public bool Destroyed;public static implicit operator bool(Object? o)=>o is not null&&!o.Destroyed;}
    public class GameObject:Object{public bool activeInHierarchy{get;set;}=true;}
    public class Component:Object{public GameObject gameObject{get;set;}=new();}
}
namespace PolytopiaBackendBase.Common{public enum SkinType{Default=0}}
namespace Polytopia.Data{public class UnitData{public enum Type{warrior,transportship,dragonegg,babydragon,firedragon,dcp_mimic_form_babydragon,dcp_mimic_form_firedragon}}}
public static class EnumCache<T> where T:struct,Enum
{public static string GetName(T v)=>v.ToString();public static T GetType(string s)=>Enum.Parse<T>(s);}
public enum SFXTypes{SpawnEnemy=23,Grow=24,Promote=27}
public sealed class WorldCoordinates:Il2CppSystem.Object
{public int X{get;}public int Y{get;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
public class UnitState:Il2CppSystem.Object
{
    public uint id{get;set;}=9;public byte owner{get;set;}=1;public ushort health{get;set;}=10;
    public WorldCoordinates coordinates{get;set;}=new(2,3);public Polytopia.Data.UnitData.Type type{get;set;}=Polytopia.Data.UnitData.Type.dcp_mimic_form_babydragon;
}
public class MapData:Il2CppSystem.Object{public Dictionary<(int,int),TileData> Tiles=new();}
public static class MapDataExtensions
{public static TileData? GetTile(MapData map,WorldCoordinates at)=>map.Tiles.GetValueOrDefault((at.X,at.Y));}
public class TileData:Il2CppSystem.Object
{
    public UnitState? unit{get;set;}public bool Explored=true,Invisible;public int GetUnitCalls;public bool? LastIgnore;
    public bool GetExplored(byte viewer)=>Explored;
    public UnitState? GetUnit(GameState state,byte viewer,bool ignoreInvisible){GetUnitCalls++;LastIgnore=ignoreInvisible;return Invisible&&!ignoreInvisible?null:unit;}
}
public class GameState:Il2CppSystem.Object
{
    public List<ActionBase> ActionStack{get;set;}=new();public uint CurrentTurn{get;set;}=8;public MapData Map{get;set;}=new();public Dictionary<uint,UnitState> Units=new();
    public bool TryGetUnit(uint id,out UnitState? unit)=>Units.TryGetValue(id,out unit);
}
public class Tile:UnityEngine.Component{public TileData Data{get;set;}=new();public bool IsHidden{get;set;}}
public class MapRenderer:UnityEngine.Component
{
    public static MapRenderer? Current{get;set;}public Dictionary<(int,int),Tile> Tiles=new();
    public Tile? GetTileInstance(WorldCoordinates at)=>Tiles.GetValueOrDefault((at.X,at.Y));public void OnDestroy(){}
}
public class ClientBase:Il2CppSystem.Object
{public bool IsReplay{get;set;}public bool IsSpectating{get;set;}public bool IsRecap{get;set;}}
public class PlayerState:Il2CppSystem.Object{public byte Id{get;set;}=1;}
public static class GameManager
{
    public static GameState? GameState{get;set;}public static ClientBase? Client{get;set;}public static PlayerState? LocalPlayer{get;set;}
    public static HashSet<byte> LocalIds=new(){1};public static bool IsPlayerLocal(byte playerId)=>LocalIds.Contains(playerId);
}
public static class HotSeatOverlay{public static bool IsActive{get;set;}}
public sealed class AudioManager:UnityEngine.Object
{
    public static AudioManager? instance{get;set;}=new();public static bool Enabled=true,Throw;public static System.Action? DuringEnabled,DuringPlay;
    public sealed record Played(SFXTypes Cue,int X,int Y,PolytopiaBackendBase.Common.SkinType Skin,float Volume,float Pitch);
    public static List<Played> Calls=new();
    public static bool ShouldPlaySoundEffects(){DuringEnabled?.Invoke();return Enabled;}
    public static void PlaySFXAtTile(SFXTypes id,WorldCoordinates coordinates,PolytopiaBackendBase.Common.SkinType skin,float volume,float pitch)
    {Calls.Add(new(id,coordinates.X,coordinates.Y,skin,volume,pitch));DuringPlay?.Invoke();if(Throw)throw new Exception("injected sound failure");}
}
public enum ActionType{Other=1,Upgrade=16}
public class ActionBase:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public bool Valid=true;public virtual ActionType GetActionType()=>ActionType.Other;public virtual bool IsValid(GameState state)=>Valid;}
public class EndTurnAction:ActionBase{}
public class DisembarkAction:ActionBase{}
public class UpgradeAction:ActionBase
{public Polytopia.Data.UnitData.Type Type{get;set;}=Polytopia.Data.UnitData.Type.babydragon;public WorldCoordinates Coordinates{get;set;}=new(2,3);public override ActionType GetActionType()=>ActionType.Upgrade;}
public class EndTurnReaction:Il2CppSystem.Object{public EndTurnAction action{get;set;}=new();public void Execute(Il2CppSystem.Action callback){}}
public class DisembarkReaction:Il2CppSystem.Object{public DisembarkAction action{get;set;}=new();public void Execute(Il2CppSystem.Action callback){}}
public class UpgradeReaction:Il2CppSystem.Object{public UpgradeAction action{get;set;}=new();public void Execute(Il2CppSystem.Action callback){}}
namespace Deception.PolyModAdapter
{
    internal static class Entry{public static List<string> Notes=new();public static void Note(string kind,string value)=>Notes.Add(kind+":"+value);}
    internal static class NativeRegroupSession
    {public static bool Allowed=true;public static GameState? Owned;public static bool Ready(object state)=>Allowed&&ReferenceEquals(Owned,state);}
    internal static class NativeGameStateEnvelope
    {
        public sealed record Frame(long Revision,byte[] Payload){public byte[] Bytes()=>(byte[])Payload.Clone();}
        public static Dictionary<object,Frame> Frames=new();public static Frame? Get(object state)=>Frames.GetValueOrDefault(state);
        public static void Put(GameState state,NativeIdentityLedger ledger)=>Frames[state]=new(0,NativeIdentityLedgerCodec.Encode(ledger,0));
    }
}
