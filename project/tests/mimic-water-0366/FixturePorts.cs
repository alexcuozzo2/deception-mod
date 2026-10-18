// Synthetic native boundaries; production NativeApi, movement adapters and Core
// are compiled unchanged. The fixture invokes their exact hook callbacks.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Deception.Core;
using Deception.PolyModAdapter;
using Terrain=Polytopia.Data.TerrainData;
using Tech=Polytopia.Data.TechData;
namespace HarmonyLib{[AttributeUsage(AttributeTargets.Method)]public sealed class HarmonyPriority:Attribute{public HarmonyPriority(int p){}}public static class Priority{public const int First=800,Last=0;}public sealed class HarmonyMethod{public MethodInfo Method;public HarmonyMethod(MethodInfo m){Method=m;}}public sealed class Harmony{public List<MethodInfo> Patched=new();public void Patch(MethodInfo m,HarmonyMethod? p,HarmonyMethod? q,object? t,HarmonyMethod? f,object? il){Patched.Add(m);}}}
namespace Il2CppSystem.Collections.Generic{public class List<T>:System.Collections.Generic.List<T>{public static int Created;public List(){Created++;}}public class Dictionary<K,V>:System.Collections.Generic.Dictionary<K,V>where K:notnull{}}
public class Ptr{static long Next;public IntPtr Pointer{get;}=new(++Next);}
public static class EnumCache<T>where T:struct,Enum{public static string GetName(T t)=>t.ToString();public static T GetType(string s)=>Enum.Parse<T>(s,true);}
[StructLayout(LayoutKind.Sequential)]public struct WorldCoordinates:IEquatable<WorldCoordinates>{public int x,y;public int X=>x;public int Y=>y;public WorldCoordinates(int x,int y){this.x=x;this.y=y;}public bool Equals(WorldCoordinates v)=>v.x==x&&v.y==y;public override bool Equals(object? o)=>o is WorldCoordinates v&&Equals(v);public override int GetHashCode()=>HashCode.Combine(x,y);}
public enum UnitType{warrior,island,amphibian,firedragon,cloak,dcp_mimic,dcp_mimic_form_warrior,dcp_mimic_form_island,dcp_mimic_form_amphibian,dcp_mimic_form_firedragon,dcp_mimic_form_transportship,dcp_mimic_form_cloak}
namespace PolytopiaBackendBase.Common{public enum TribeType{Imperius,Aquarion,Polaris,Cymanti}}
namespace Polytopia.Data
{
 public sealed class TerrainData:Ptr{public enum Type{None=0,Water=1,Ocean=2,Field=3,Mountain=4,Forest=5,Ice=6,Wetland=7,Mangrove=8}public Type type{get;set;}}
 public sealed class TechData:Ptr{public enum Type{Fishing=10,Sailing=13,Frostwork=30,IceFishing=42,AquaBase=45,MarineLife=46,AquaFishing=47,Propaganda=1001}public Type type{get;set;}}
 public sealed class TribeData:Ptr{public Il2CppSystem.Collections.Generic.Dictionary<TechData.Type,TechData> techOverrides{get;set;}=new();}
 public sealed class GameLogicData:Ptr{
  public Dictionary<PolytopiaBackendBase.Common.TribeType,TribeData> Tribes=new();public TribeData GetTribeData(PolytopiaBackendBase.Common.TribeType t)=>Tribes[t];public bool TryGetData(ImprovementData.Type type,out ImprovementData data){data=new(){type=type,Bridge=type is ImprovementData.Type.Bridge or ImprovementData.Type.Pontoon};return true;}
  public Il2CppSystem.Collections.Generic.List<TerrainData> GetUnlockedMovements(PlayerState p){var r=new Il2CppSystem.Collections.Generic.List<TerrainData>();foreach(var t in new[]{TerrainData.Type.Field,TerrainData.Type.Forest,TerrainData.Type.Ice,TerrainData.Type.Wetland})r.Add(new(){type=t});if(p.Techs.Contains(TechData.Type.Fishing)||p.Techs.Contains(TechData.Type.AquaBase))r.Add(new(){type=TerrainData.Type.Water});if(p.Techs.Contains(TechData.Type.Sailing)||p.Techs.Contains(TechData.Type.MarineLife))r.Add(new(){type=TerrainData.Type.Ocean});return r;}
 }
 public sealed class UnitAbility{public enum Type{Swim=10,Fly=13}}
 public sealed class ImprovementData:Ptr{public enum Type{City=0,Port=1,Bridge=48,Pontoon=49}public Type type{get;set;}public bool Bridge;}
 public sealed class ImprovementAbility{public enum Type{Bridge=13}}
}
public sealed class PlayerState:Ptr{public byte Id{get;set;}=1;public PolytopiaBackendBase.Common.TribeType tribe{get;set;}public HashSet<Tech.Type> Techs=new();}
public static class PlayerExtensions{public static bool HasTech(PlayerState p,Tech.Type t)=>p.Techs.Contains(t);}
public sealed class UnitState:Ptr{public uint id{get;set;}=1;public byte owner{get;set;}=1;public UnitType type{get;set;}=UnitType.dcp_mimic_form_island;public WorldCoordinates coordinates{get;set;}=new(0,0);public int health=10,Movement=4;public bool moved,attacked;public Il2CppSystem.Collections.Generic.List<Terrain> Allowed=new();}
public sealed class ImprovementState:Ptr{public Polytopia.Data.ImprovementData.Type type{get;set;}}
public static class ImprovementDataExtensions{public static bool HasAbility(Polytopia.Data.ImprovementData data,Polytopia.Data.ImprovementAbility.Type ability)=>data.Bridge;}
public sealed class TileData:Ptr{public WorldCoordinates coordinates{get;set;}public UnitState? unit{get;set;}public Terrain.Type terrain{get;set;}=Terrain.Type.Field;public ImprovementState? improvement{get;set;}public bool Flooded,Port,City;public byte Owner=1;public bool IsWater=>terrain is Terrain.Type.Water or Terrain.Type.Ocean;public bool IsWetland()=>Flooded||terrain==Terrain.Type.Wetland;public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>t==Polytopia.Data.ImprovementData.Type.City?City:improvement?.type==t;public bool HasOpponentCity(byte p)=>City&&Owner!=0&&Owner!=p;}
public sealed class MapData:Ptr{public Dictionary<WorldCoordinates,TileData> Tiles=new();}
public sealed class GameState:Ptr{public MapData Map{get;set;}=new();public Polytopia.Data.GameLogicData GameLogicData{get;set;}=new();public Dictionary<byte,PlayerState> Players=new();public Dictionary<uint,LedgerUnit> Rows=new();public bool Owned=true;public bool TryGetPlayer(byte p,out PlayerState? player)=>Players.TryGetValue(p,out player);public bool TryGetUnit(uint id,out UnitState? u){u=Map.Tiles.Values.Select(t=>t.unit).FirstOrDefault(x=>x?.id==id);return u is not null;}}
public static class MapDataExtensions{public static TileData? GetTile(MapData map,WorldCoordinates at)=>map.Tiles.GetValueOrDefault(at);}
public static class UnitDataExtensions
{
 public static int GetMovement(UnitState u,GameState s)=>u.Movement;
 public static bool HasAbility(UnitState u,Polytopia.Data.UnitAbility.Type ability)=>ability switch{Polytopia.Data.UnitAbility.Type.Fly=>u.type is UnitType.firedragon or UnitType.dcp_mimic_form_firedragon,Polytopia.Data.UnitAbility.Type.Swim=>u.type is UnitType.island or UnitType.amphibian or UnitType.dcp_mimic_form_island or UnitType.dcp_mimic_form_amphibian or UnitType.dcp_mimic_form_transportship,_=>false};
 public static Il2CppSystem.Collections.Generic.List<Terrain> GetAllowedTerrain(UnitState u,GameState s){object? result=u.Allowed;NativeMimicWaterMovement.AfterTerrain(u,s,ref result);return (Il2CppSystem.Collections.Generic.List<Terrain>)result!;}
}
public static class PathFinder
{
 public static int Paths,OptionCalls;public static bool ThrowPath,CloakFilter,CloakFirst;public static Action? During;
 public static Func<GameState,WorldCoordinates,WorldCoordinates,int,UnitState?,Il2CppSystem.Collections.Generic.List<WorldCoordinates>>? NativeRoute;
 public static Il2CppSystem.Collections.Generic.List<WorldCoordinates>? CachedOptions;
 static Il2CppSystem.Collections.Generic.List<WorldCoordinates> List(IEnumerable<WorldCoordinates> c){var r=new Il2CppSystem.Collections.Generic.List<WorldCoordinates>();r.AddRange(c);return r;}
 static Il2CppSystem.Collections.Generic.List<WorldCoordinates> Route(GameState s,WorldCoordinates from,WorldCoordinates to,int distance,UnitState? u)
 {
  var allowed=u is null?null:UnitDataExtensions.GetAllowedTerrain(u,s);var queue=new Queue<WorldCoordinates>();queue.Enqueue(from);var parent=new Dictionary<WorldCoordinates,WorldCoordinates>();var cost=new Dictionary<WorldCoordinates,int>{{from,0}};
  while(queue.Count>0){var p=queue.Dequeue();if(p.Equals(to)){var path=new List<WorldCoordinates>{p};while(!p.Equals(from)){p=parent[p];path.Add(p);}return List(path);}if(cost[p]>=distance)continue;
   foreach(var next in new[]{new WorldCoordinates(p.x-1,p.y),new WorldCoordinates(p.x+1,p.y),new WorldCoordinates(p.x,p.y-1),new WorldCoordinates(p.x,p.y+1)})
   {if(cost.ContainsKey(next)||!s.Map.Tiles.TryGetValue(next,out var tile))continue;
    // Native terrain alternatives can admit a port or flooded tile. The
    // production path guard must still reject forbidden water in that route.
    if(allowed is not null&&!tile.Port&&tile.improvement is not {type:Polytopia.Data.ImprovementData.Type.Bridge or Polytopia.Data.ImprovementData.Type.Pontoon}&&!allowed.Any(t=>t.type==tile.terrain||tile.IsWetland()&&t.type==Terrain.Type.Wetland))continue;
    cost[next]=cost[p]+1;parent[next]=p;queue.Enqueue(next);}}
  return List(Array.Empty<WorldCoordinates>());
 }
 public static Il2CppSystem.Collections.Generic.List<WorldCoordinates> GetPath(GameState s,WorldCoordinates from,WorldCoordinates to,int max,UnitState? u)
 {
  Paths++;object? result=null;NativeMimicWaterMovement.Scope? scope=null;Exception? error=null;
  try{bool run=NativeMimicWaterMovement.BeforePath(s,from,to,u,out scope,ref result);if(run){During?.Invoke();if(ThrowPath)throw new InvalidOperationException("native path failed");result=NativeRoute is null?Route(s,from,to,max,u):NativeRoute(s,from,to,max,u);}NativeMimicWaterMovement.AfterPath(to,scope,ref result);return (Il2CppSystem.Collections.Generic.List<WorldCoordinates>)result!;}
  catch(Exception e){error=e;throw;}finally{NativeMimicWaterMovement.Finished(error,scope);}
 }
 public static Il2CppSystem.Collections.Generic.List<WorldCoordinates> GetMoveOptions(GameState s,WorldCoordinates from,int max,UnitState? u)
 {
  OptionCalls++;object? result=null;NativeMimicWaterMovement.Scope? scope=null;Exception? error=null;
  try{bool run=NativeMimicWaterMovement.BeforeOptions(s,from,u,out scope,ref result);if(run){During?.Invoke();result=CachedOptions??List(s.Map.Tiles.Keys.Where(at=>Route(s,from,at,max,u).Count>0));}
   if(CloakFilter&&CloakFirst)NativeCloakMovement.AfterOptions(s,from,u!,ref result);
   NativeMimicWaterMovement.AfterOptions(s,from,max,u!,scope,ref result);
   if(CloakFilter&&!CloakFirst)NativeCloakMovement.AfterOptions(s,from,u!,ref result);
   return (Il2CppSystem.Collections.Generic.List<WorldCoordinates>)result!;}
  catch(Exception e){error=e;throw;}finally{NativeMimicWaterMovement.Finished(error,scope);}
 }
}
public sealed class MoveCommand:Ptr{public uint UnitId{get;set;}=1;public byte PlayerId{get;set;}=1;public WorldCoordinates From{get;set;}=new(0,0);public WorldCoordinates To{get;set;}=new(1,0);public bool IsValid(GameState s,ref string error)=>true;}
namespace Deception.PolyModAdapter
{
 internal static class RecruitmentHooks{internal const string TT="Polytopia.Data.TechData+Type",GLD="Polytopia.Data.GameLogicData";internal static bool Registered=true;}
 internal static class NativeGameStateEnvelope{internal static object Frame=new();internal static object? Get(object state)=>Frame;}internal static class NativeRegroupSession{internal static bool Enabled=true;internal static int ReadyCalls,Reads;internal static bool Ready(object s){ReadyCalls++;return ((GameState)s).Owned;}internal static LedgerUnit? ReadIdentity(object s,uint id){Reads++;return ((GameState)s).Rows.GetValueOrDefault(id);}}
}
