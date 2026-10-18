using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deception.Core;
using Deception.PolyModAdapter;

// Native-shaped ports only. This gate compiles the production NativeApi,
// NativeLedgerState, NativeGameStateSnapshot and NativeRogueSight unchanged.
namespace HarmonyLib
{
 public sealed class HarmonyMethod {public HarmonyMethod(MethodInfo method){}}
 public sealed class Harmony {public readonly List<string> Hooks=new();public void Patch(MethodInfo method,HarmonyMethod? prefix,HarmonyMethod? postfix,object? transpiler,HarmonyMethod? finalizer,object? il=null)=>Hooks.Add(method.DeclaringType!.Name+"."+method.Name);}
}
namespace Il2CppSystem
{
 public class Object {static long Next;public IntPtr Pointer{get;}=new(++Next);public T Cast<T>()where T:class=>this as T??throw new InvalidCastException();}
}
namespace PolytopiaBackendBase.Common {public enum TribeType{None,Imperius,Elyrion,Aquarion}public enum SkinType{Default}}
public enum UnitEffect {Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8}
public static class EnumCache<T>where T:struct,Enum {public static string GetName(T value)=>value.ToString();public static T GetType(string name)=>Enum.Parse<T>(name,true);}
public readonly record struct WorldCoordinates(int X,int Y);
namespace Polytopia.Data
{
 public class UnitData:Il2CppSystem.Object
 {
  public enum Type {warrior,polytaur,dcp_spy,dcp_spymaid,dcp_mimic_form_warrior}
  public Type type{get;set;}public int health{get;set;}=100;public int attack{get;set;}=20;public int defence{get;set;}=10;public int range{get;set;}=1;
  public List<int> unitAbilities{get;set;}=new();public int Sight{get;set;}=1;
 }
 public class ImprovementData {public enum Type{None=0,City=1}}
 public class GameLogicData:Il2CppSystem.Object { }
}
public sealed class PlayerState:Il2CppSystem.Object
{public byte Id{get;set;}=1;public int color{get;set;}=0x112233;public PolytopiaBackendBase.Common.TribeType tribe{get;set;}=PolytopiaBackendBase.Common.TribeType.Imperius;}
public sealed class UnitState:Il2CppSystem.Object
{
 public uint id{get;set;}public byte owner{get;set;}=1;public int health{get;set;}=100;public int promotionLevel{get;set;}
 public Polytopia.Data.UnitData.Type type{get;set;}public WorldCoordinates coordinates{get;set;}public Polytopia.Data.UnitData UnitData{get;set;}=new();
 public UnitState? passengerUnit{get;set;}public List<UnitEffect> effects{get;set;}=new();public bool moved{get;set;}public bool attacked{get;set;}
 public bool HasLeader()=>false;public bool HasFollower()=>false;public bool IsInvisible(GameState state,byte player)=>effects.Contains(UnitEffect.Invisible);
}
public sealed class TileData:Il2CppSystem.Object
{
 public WorldCoordinates coordinates{get;set;}public UnitState? unit{get;set;}public HashSet<byte> Seen=new();public bool IsWater{get;set;}
 public bool GetExplored(byte player)=>Seen.Contains(player);public bool IsWetland()=>false;public bool HasImprovement(Polytopia.Data.ImprovementData.Type type)=>false;
 public UnitState? GetUnit(GameState state,byte player,bool ignoreFog)=>!ignoreFog&&!GetExplored(player)||unit?.IsInvisible(state,player)==true?null:unit;
}
public sealed class TileArray:Il2CppSystem.Object
{readonly List<TileData> Values=new();public int Length=>Values.Count;public int Count=>Values.Count;public TileData this[int index]=>Values[index];public void Add(TileData tile)=>Values.Add(tile);public IEnumerable<TileData> All=>Values;}
public sealed class MapData:Il2CppSystem.Object {public TileArray Tiles{get;set;}=new();}
public static class MapDataExtensions {public static TileData? GetTile(MapData map,WorldCoordinates coordinates)=>map.Tiles.All.SingleOrDefault(t=>t.coordinates==coordinates);}
public static class UnitDataExtensions
{public static int GetSightRange(Polytopia.Data.UnitData data)=>data.Sight;public static int GetMaxHealth(UnitState body,GameState state)=>body.UnitData.health+body.promotionLevel*50;}
public sealed class GameState:Il2CppSystem.Object
{
 public int Version{get;set;}=123;public uint CurrentUnitId{get;set;}=5;public MapData Map{get;set;}=new();public Polytopia.Data.GameLogicData GameLogicData{get;set;}=new();
 public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=4;public List<object> ActionStack{get;set;}=new();public Dictionary<byte,PlayerState> Players=new();
 public bool TryGetUnit(uint id,out UnitState? unit){unit=Map.Tiles.All.Select(t=>t.unit).SingleOrDefault(u=>u?.id==id);return unit is not null;}
 public bool TryGetPlayer(byte id,out PlayerState? player)=>Players.TryGetValue(id,out player);
}
public sealed class MoveAction:Il2CppSystem.Object
{public byte PlayerId{get;set;}=1;public uint UnitId{get;set;}=1;public void Execute(GameState state){}}
public sealed class ExploreAction:Il2CppSystem.Object
{public byte PlayerId{get;set;}=1;public WorldCoordinates Coordinates{get;set;}public void Execute(GameState state){}}
public static class BattleHelpers {public static bool CanDefenderRetaliate(GameState state,UnitState attacker,UnitState defender)=>true;}
public static class ActionUtils
{public static void ExploreFromTile(GameState state,PlayerState player,TileData tile,int range,bool queued){}public static void ExploreTile(GameState state,PlayerState player,TileData tile,bool queued){}}

namespace Deception.PolyModAdapter
{
 internal static class Entry {internal static void Note(string kind,string message){}}
 internal static class NativeRegroupSession
 {
  internal static bool Enabled=true,Faulted;internal static readonly HashSet<object> States=new();internal static readonly List<string> Faults=new();
  internal static bool Ready(object state)=>Enabled&&!Faulted&&States.Contains(state);
  internal static void Fault(string reason){Faulted=true;Faults.Add(reason);}
 }
 internal static partial class NativeGameStateEnvelope
 {
  internal static readonly Dictionary<object,Snapshot> Values=new();internal static int Writes;internal static bool FailNextSet;
  internal static Snapshot? Get(object state)=>Values.GetValueOrDefault(state);
  internal static void Set(object state,long revision,byte[] payload)
  {
   // Production Set validates the entire graph before publishing. Omitting
   // this call in the old sight fixture concealed the dead-victim regression.
   NativeLedgerState.Validate(state,revision,payload);
   if(FailNextSet){FailNextSet=false;throw new InvalidDataException("Injected publication failure");}
   Values[state]=new(revision,payload);Writes++;
  }
 }
 internal static class NativeRogueRules
 {
  internal static LedgerUnit? Row(object state,object unit){var snapshot=NativeGameStateEnvelope.Get(state);return snapshot is null?null:NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Units.SingleOrDefault(r=>r.NativeId==((UnitState)unit).id);}
  internal static bool CanCommand(object state,object unit,byte actor){var s=(GameState)state;var u=(UnitState)unit;var rogue=Row(state,unit)?.Rogue;return s.CurrentPlayer==actor&&u.owner==actor&&(rogue is null||rogue.Commander==actor&&rogue.AcquiredTurn==s.CurrentTurn);}
 }
 internal static class NativeSpyNetwork
 {
  internal sealed record Position(int X,int Y);internal static Position At(object coordinates){var p=(WorldCoordinates)coordinates;return new(p.X,p.Y);}
  internal static bool Accessible(object state,object tile,byte actor)=>((TileData)tile).GetExplored(actor);
 }
 internal static class NativeBlackout {internal static bool Covered(object state,byte actor,NativeSpyNetwork.Position at)=>false;}
 internal static class NativeAquarionUnits {internal static bool IsAquarion(object player)=>((PlayerState)player).tribe==PolytopiaBackendBase.Common.TribeType.Aquarion;}
 internal static class NativeFreshSession {internal static bool Enabled=false;}
 internal static class NativeEnvelopeProbe {internal static bool ValidatePayload(byte[] payload)=>false;}
 internal static class RecruitmentHooks
 {
  internal const string GLD="Polytopia.Data.GameLogicData";
  internal static readonly Dictionary<string,RecruitmentForm> Forms=new(){["dcp_mimic_form_warrior"]=new("warrior","dcp_mimic_form_warrior","Warrior",10,1,20,10,false)};
  internal static object Data(object state,string form)=>new Polytopia.Data.UnitData{type=Enum.Parse<Polytopia.Data.UnitData.Type>(form),health=form.StartsWith("dcp_mimic_form_",StringComparison.Ordinal)?10:100};
 }
}
