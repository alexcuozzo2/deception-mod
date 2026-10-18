// Actual production reward/plan/API adapters, with an explicit managed native boundary.
// This fixture does not execute Polytopia, Harmony patches, or online transport.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
namespace HarmonyLib
{
 public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo method){}}
 public sealed class Harmony{public void Patch(MethodInfo method,HarmonyMethod? prefix,HarmonyMethod? postfix,object? transpiler,HarmonyMethod? finalizer,object? il=null){}}
}
namespace Il2CppSystem
{
 public sealed class Action:Object
 {public int Calls;public void Invoke(){Calls++;Fault.Check("completion");}}
 public class Object
 {
  static long Next;public IntPtr Pointer{get;}
  public Object(){Pointer=new(++Next);}protected Object(IntPtr pointer){Pointer=pointer;}
 }
}
public struct WorldCoordinates
{public int X{get;set;}public int Y{get;set;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
public static class Fault
{
 public static string At="";public static int Skip;public static Action<string>? Observe;
 public static void Check(string point){Observe?.Invoke(point);if(At==point&&Skip--<=0){At="";throw new System.IO.IOException("Injected "+point);}}
 public static void Reset(){At="";Skip=0;Observe=null;}
}
public sealed class PlayerState:Il2CppSystem.Object
{
 sealed class Treasury{internal int Value=20;}Treasury Bank=new();
 public byte Id{get;set;}public bool Alive{get;set;}=true;
 public int Currency{get{Fault.Check("get-currency");return Bank.Value;}set{Bank.Value=value;Fault.Check("currency");}}
 public PlayerState(){}PlayerState(PlayerState player):base(player.Pointer){Id=player.Id;Alive=player.Alive;Bank=player.Bank;}
 public PlayerState Wrapper()=>new(this);
}
public static class PlayerExtensions{public static bool IsAlive(PlayerState player,GameState state)=>player.Alive;}
public sealed class TileData:Il2CppSystem.Object
{
 public WorldCoordinates coordinates{get;set;}public bool Lighthouse{get;set;}=true;
 public HashSet<byte> Visible=new(){1,2};public bool GetExplored(byte actor)=>Visible.Contains(actor);
}
public sealed class MapData:Il2CppSystem.Object{public List<TileData> Tiles=new();}
public static class MapDataExtensions
{public static TileData? GetTile(MapData map,WorldCoordinates at)=>map.Tiles.SingleOrDefault(t=>t.coordinates.X==at.X&&t.coordinates.Y==at.Y);}
public static class ExploreLightHouseTask
{
 public static bool ShouldUseLighthouseTasks(GameState state)=>state.Lighthouses;
 public static bool EvaluateTile(TileData tile,GameState state){Fault.Check("lighthouse");return state.Lighthouses&&tile.Lighthouse;}
}
public sealed class GameState:Il2CppSystem.Object
{
 public enum State{Started,Ended}public State CurrentState{get;set;}=State.Started;
 public uint CurrentTurn{get;set;}=4;public byte CurrentPlayer{get;set;}=1;
 public MapData Map{get;set;}=new();public List<PlayerState> Players=new();public bool Lighthouses=true,Rewrap;
 public bool TryGetPlayer(byte id,out PlayerState? player){player=Players.SingleOrDefault(p=>p.Id==id);if(Rewrap&&player is not null)player=player.Wrapper();return player is not null;}
 public bool UnitMoved,UnitAttacked,CityExhausted;public int Actions,Effects,Sounds;
}
namespace Deception.PolyModAdapter
{
 internal static class RecruitmentHooks{internal static bool Registered=true;}
 internal static class NativeRegroupSession
 {
  internal static bool Enabled=true;internal static HashSet<object> Owned=new();internal static List<string> Faults=new();
  internal static bool Ready(object state)=>Enabled&&Owned.Contains(state);
  internal static void Fault(string reason)=>Faults.Add(reason);
 }
 internal static class NativeExchangeCommands
 {
  internal static object? Executing;internal static bool LiveExecution(object state)=>ReferenceEquals(Executing,state);
  internal static NativeExchangeIntent? Get(object action){Fault.Check("attachment");return ((BuildAction)action).Intent;}
  internal static void ValidateBase(object action,NativeExchangeIntent intent)
  {
   var a=(BuildAction)action;
   if(a.PlayerId!=intent.Actor||a.Type!=NativeExchangeCatalog.Id(intent.Kind)||a.Coordinates.X!=intent.Initiator.X||a.Coordinates.Y!=intent.Initiator.Y)
    throw new System.IO.InvalidDataException("Fixture carrier differs from complete intent");
  }
 }
 internal static class Entry{internal static List<string> Notes=new();internal static void Note(string tag,string message)=>Notes.Add(tag+":"+message);}
 internal static class NativeGameStateEnvelope
 {
  internal sealed class Snapshot
  {internal long Revision{get;}readonly byte[] Data;internal Snapshot(long revision,byte[] bytes){Revision=revision;Data=(byte[])bytes.Clone();}internal byte[] Bytes()=>(byte[])Data.Clone();}
  internal static Dictionary<object,Snapshot> Values=new();internal static Snapshot? Get(object state)=>Values.GetValueOrDefault(state);
  internal static void Set(object state,long revision,byte[] bytes)
  {NativeIdentityLedgerCodec.Decode(bytes,revision);Values[state]=new(revision,bytes);Fault.Check("ledger");}
 }
 internal static class NativeLedgerState
 {internal static void Validate(object state,long revision,byte[] bytes){NativeIdentityLedgerCodec.Decode(bytes,revision);Fault.Check("validate");}}
 internal sealed record NativeExchangeCommitResult(bool AlreadyCommitted,long BeforeRevision,long AfterRevision,NativeExchangePlan Plan);
}
public sealed class BuildAction:Il2CppSystem.Object
{public NativeExchangeIntent? Intent;public byte PlayerId{get;set;}public string Type{get;set;}="";public WorldCoordinates Coordinates{get;set;}}
public sealed class Client:Il2CppSystem.Object{public GameState? GameState{get;set;}}
public static class GameManager{public static GameState? GameState{get;set;}public static Client? Client{get;set;}}
public static class ResourceEvents
{
 public static readonly List<byte> Refreshed=new();public static readonly Dictionary<byte,int> Wallets=new();
 public static void RefreshWallets(byte actor)
 {Refreshed.Add(actor);Fault.Check("wallet");var state=GameManager.GameState!;Wallets[actor]=state.Players.Single(p=>p.Id==actor).Currency;}
}
