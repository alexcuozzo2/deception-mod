// Synthetic audited native queue seams. No Unity, game process or native actions.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
namespace HarmonyLib
{
 public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo method){}}
 public sealed class Harmony{public void Patch(MethodInfo m,HarmonyMethod? a,HarmonyMethod? b,object? c,HarmonyMethod? d,object? e){}}
}
namespace Il2CppSystem
{
 public class Object
 {
  static long Next;public IntPtr Pointer{get;}=new(++Next);
  public T Cast<T>()where T:class=>this as T??throw new InvalidCastException("Wrong native action type");
 }
}
namespace Il2CppSystem.Collections.Generic
{
 public sealed class List<T>:Il2CppSystem.Object
 {
  readonly System.Collections.Generic.List<T> Values=new();
  public int Count=>Values.Count;public T this[int i]{get=>Values[i];set=>Values[i]=value;}
  public void Add(T value)=>Values.Add(value);public void RemoveAt(int i)=>Values.RemoveAt(i);
 }
}
namespace Polytopia.Data
{public sealed class UnitData:Il2CppSystem.Object{public int type{get;set;}=1047;public int Sight=2;}}
public sealed class WorldCoordinates:Il2CppSystem.Object
{public int X{get;set;}public int Y{get;set;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
public sealed class UnitState:Il2CppSystem.Object
{
 public uint id{get;set;}=7;public byte owner{get;set;}=1;public int health{get;set;}=10;
 public int type{get;set;}=1047;public Polytopia.Data.UnitData UnitData{get;set;}=new();
 public WorldCoordinates coordinates{get;set;}=new(0,0);public bool Root=true;
 public int Investment=3,CreatedTurn=2;public bool Moved=true,Attacked=true;public object Effects=new();
}
public sealed class TileData:Il2CppSystem.Object
{
 public WorldCoordinates coordinates{get;set;}=new(0,0);public UnitState? unit{get;set;}public int terrain{get;set;}
 public HashSet<byte> Seen=new(),Covered=new();public bool GetExplored(byte owner)=>Seen.Contains(owner)&&!Covered.Contains(owner);
}
public sealed class TileArray:Il2CppSystem.Object
{public TileData[] Values=Array.Empty<TileData>();public int Length=>Values.Length;public TileData this[int i]=>Values[i];}
public sealed class MapData:Il2CppSystem.Object
{public Dictionary<(int,int),TileData> Tiles=new();public Dictionary<uint,UnitState> Graph=new();public string AreaFailure="";}
public static class MapDataExtensions
{
 public static TileData? GetTile(MapData map,WorldCoordinates at)=>map.Tiles.GetValueOrDefault((at.X,at.Y));
 public static TileArray GetAreaSorted(MapData map,WorldCoordinates at,int radius,bool center,bool corners)
 {
  if(!center||!corners)throw new InvalidOperationException("Wrong audited area flags");
  if(map.AreaFailure=="oversize")return new(){Values=Enumerable.Repeat(map.Tiles[(0,0)],26).ToArray()};
  if(map.AreaFailure=="empty")return new();
  var items=map.Tiles.Values.Where(t=>Math.Abs(t.coordinates.X-at.X)<=radius&&Math.Abs(t.coordinates.Y-at.Y)<=radius)
   .OrderBy(t=>t.coordinates.X).ThenBy(t=>t.coordinates.Y).ToArray();
  if(map.AreaFailure=="foreign")items[0]=new(){coordinates=items[0].coordinates};
  if(map.AreaFailure=="null")items[0]=null!;
  return new(){Values=items};
 }
}
public sealed class PlayerState:Il2CppSystem.Object{public byte Id{get;set;}=1;}
public class ActionBase:Il2CppSystem.Object{public string Kind="prefix";}
public sealed class ExploreAction:ActionBase
{public byte PlayerId{get;set;}public WorldCoordinates Coordinates{get;set;}=new(0,0);}
public sealed class GameState:Il2CppSystem.Object
{
 public int Version{get;set;}=123;public uint CurrentTurn{get;set;}=8;public byte CurrentPlayer{get;set;}=1;
 public MapData Map{get;set;}=new();public Dictionary<byte,PlayerState> Players=new(){{1,new()},{2,new(){Id=2}}};
 public bool TryGetPlayer(byte id,out PlayerState? player)=>Players.TryGetValue(id,out player);
 Il2CppSystem.Collections.Generic.List<ActionBase> Stack=new();bool Dirty;
 public string Failure="";public int Stars=23,Score=100,CurrentUnitId=40,Births,Deaths;
 public Il2CppSystem.Collections.Generic.List<ActionBase> ActionStack
 {
  get=>Stack;
  set{if(Failure=="stack-before"){Failure="";throw new IOException("stack before");}Stack=value;if(Failure=="stack-after"){Failure="";throw new IOException("stack after");}}
 }
 public bool HasFlaggedNeedForUpdateRoutes
 {
  get=>Dirty;
  set{if(Failure=="dirty-before"){Failure="";throw new IOException("dirty before");}Dirty=value;if(Failure=="dirty-after"){Failure="";throw new IOException("dirty after");}}
 }
}
public static class UnitDataExtensions{public static int GetSightRange(Polytopia.Data.UnitData data)=>data.Sight;}
public static class ActionUtils
{
 public static string Failure="";public static int Calls,ExecuteCalls,FailOnCall=1;
 public static System.Collections.Generic.List<int> Ranges=new();
 public static void ExploreFromTile(GameState state,PlayerState owner,TileData tile,int range,bool visible)
 {
  Calls++;Ranges.Add(range);if(!visible)throw new InvalidOperationException("Wrong native exploration flag");
  string fail=Calls==FailOnCall?Failure:"";
  if(fail=="before")throw new IOException("before explore staging");
  int radius=tile.terrain==4?2:range;
  var area=MapDataExtensions.GetAreaSorted(state.Map,tile.coordinates,radius,true,true).Values;
  if(fail=="reverse")Array.Reverse(area);
  foreach(var target in area.Reverse())
  {
   if(target.GetExplored(owner.Id)||target.Covered.Contains(owner.Id))continue;
   ActionBase action=new ExploreAction{PlayerId=fail=="owner"?(byte)2:owner.Id,Coordinates=fail=="coordinate"?new(99,99):target.coordinates};
   if(fail=="type")action=new();
   if(fail=="reuse")action=state.ActionStack[0];
   if(fail=="skip")continue;
   state.ActionStack.Add(action);
   if(fail=="after-one")throw new IOException("after appended child");
   if(fail=="fog")target.Seen.Add(owner.Id);
  }
  if(fail=="prefix")state.ActionStack[0]=new();
 }
}
namespace Deception.PolyModAdapter
{
 internal static class NativeSpyNetwork{internal readonly record struct Position(int X,int Y);}
 internal static class NativeBlackout{internal static bool Covered(object state,byte owner,NativeSpyNetwork.Position at)=>((GameState)state).Map.Tiles[(at.X,at.Y)].Covered.Contains(owner);}
 internal static class NativeRegroupSession
 {public static bool Enabled=true;public static GameState? Owned;public static bool Ready(object state)=>Enabled&&ReferenceEquals(Owned,state);}
 internal static class NativeLedgerState
 {
  internal sealed record Body(object Unit,bool Root);
  internal static Dictionary<uint,Body> Graph(object state)=>((GameState)state).Map.Graph.ToDictionary(p=>p.Key,p=>new Body(p.Value,p.Value.Root));
 }
}
