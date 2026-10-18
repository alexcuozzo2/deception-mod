using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
using Deception.PolyModAdapter;
namespace HarmonyLib {public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo m){}}public sealed class Harmony{public List<string> Hooks=new();public void Patch(MethodInfo m,HarmonyMethod? prefix,HarmonyMethod? postfix,object? transpiler,HarmonyMethod? finalizer,object? ilmanipulator=null){Hooks.Add(m.DeclaringType!.Name+"."+m.Name);}}}
namespace Il2CppSystem {public class Object {static long Next;public IntPtr Pointer{get;}=new(++Next);public T Cast<T>()where T:class=>this as T??throw new InvalidCastException();}}
public struct WorldCoordinates{public int X{get;set;}public int Y{get;set;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
namespace Polytopia.Data {public class UnitData:Il2CppSystem.Object{public int Sight=1;}}
public sealed class PlayerState:Il2CppSystem.Object{public byte Id{get;set;}=1;}
public sealed class UnitState:Il2CppSystem.Object{public uint id{get;set;}public byte owner{get;set;}=1;public int health{get;set;}=100;public WorldCoordinates coordinates{get;set;}public Polytopia.Data.UnitData UnitData{get;set;}=new();public bool Invisible;public bool IsInvisible(GameState s,byte p)=>Invisible;}
public sealed class TileData:Il2CppSystem.Object
{
 public WorldCoordinates coordinates{get;set;}public UnitState? unit{get;set;}public HashSet<byte> Seen=new();
 public bool GetExplored(byte p)=>Seen.Contains(p);
 public UnitState? GetUnit(GameState state,byte p,bool ignoreFog)=>!ignoreFog&&!Seen.Contains(p)||unit?.Invisible==true?null:unit;
}
public sealed class MapData:Il2CppSystem.Object{public readonly Dictionary<WorldCoordinates,TileData> Tiles=new();}
public static class MapDataExtensions{public static TileData? GetTile(MapData m,WorldCoordinates c)=>m.Tiles.GetValueOrDefault(c);}
public static class UnitDataExtensions{public static int GetSightRange(Polytopia.Data.UnitData d)=>d.Sight;}
public sealed class GameState:Il2CppSystem.Object
{
 public MapData Map{get;set;}=new();public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=4;public List<object> ActionStack{get;set;}=new();
 public bool TryGetUnit(uint id,out UnitState? unit){unit=Map.Tiles.Values.Select(t=>t.unit).SingleOrDefault(u=>u?.id==id);return unit is not null;}
}
public sealed class MoveAction:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public uint UnitId{get;set;}=1;public void Execute(GameState s){}}
public sealed class ExploreAction:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public WorldCoordinates Coordinates{get;set;}public void Execute(GameState s){}}
public static class BattleHelpers{public static bool CanDefenderRetaliate(GameState s,UnitState attacker,UnitState defender)=>true;}
public static class ActionUtils{public static void ExploreFromTile(GameState s,PlayerState p,TileData t,int range,bool queued){}public static void ExploreTile(GameState s,PlayerState p,TileData t,bool queued){}}
namespace Deception.PolyModAdapter
{
 internal static class Entry{internal static void Note(string kind,string text){}}
 internal static class NativeRegroupSession{internal static bool Enabled=true;internal static readonly HashSet<object> States=new();internal static readonly List<string> Faults=new();internal static bool Ready(object state)=>Enabled&&States.Contains(state);internal static void Fault(string why)=>Faults.Add(why);}
 internal static class NativeGameStateEnvelope
 {
  internal sealed record Snapshot(long Revision,byte[] Payload){internal byte[] Bytes()=>(byte[])Payload.Clone();}
  internal static Dictionary<object,Snapshot> Values=new();internal static Snapshot? Get(object state)=>Values.GetValueOrDefault(state);
  internal static void Set(object state,long revision,byte[] data){NativeIdentityLedgerCodec.Decode(data,revision);Values[state]=new(revision,(byte[])data.Clone());}
 }
 // Retained sight-only boundary. The separate rogue-sight-0378 gate compiles
 // real NativeLedgerState and validates the full native graph at every Set.
 internal static class NativeLedgerState
 {internal static (long Revision,byte[] Payload,bool Changed) Reconcile(object state,NativeGameStateEnvelope.Snapshot snapshot)=>(snapshot.Revision,snapshot.Bytes(),false);}
 internal static class NativeRogueRules
 {
  internal static LedgerUnit? Row(object state,object unit){var s=NativeGameStateEnvelope.Get(state);return s is null?null:NativeIdentityLedgerCodec.Decode(s.Bytes(),s.Revision).Units.SingleOrDefault(r=>r.NativeId==((UnitState)unit).id);}
  internal static bool CanCommand(object state,object unit,byte actor){var s=(GameState)state;var u=(UnitState)unit;var r=Row(s,u)?.Rogue;return s.CurrentPlayer==actor&&u.owner==actor&&(r is null||r.Commander==actor&&r.AcquiredTurn==s.CurrentTurn);}
 }
 internal static class NativeSpyNetwork
 {
  internal sealed record Position(int X,int Y);internal static Position At(object c){var p=(WorldCoordinates)c;return new(p.X,p.Y);}
  internal static bool Accessible(object state,object tile,byte actor)=>((TileData)tile).GetExplored(actor)&&!NativeBlackout.Covered(state,actor,At(((TileData)tile).coordinates));
 }
 internal static class NativeBlackout
 {internal static readonly HashSet<(object,byte,int,int)> Cells=new();internal static bool Covered(object state,byte p,NativeSpyNetwork.Position at)=>Cells.Contains((state,p,at.X,at.Y));}
}
