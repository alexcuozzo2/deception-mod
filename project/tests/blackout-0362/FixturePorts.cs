// Explicit synthetic native boundary. Real Blackout/network/discovery/access
// adapters and real persistence codecs are compiled into this fixture.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deception.Core;
using Deception.PolyModAdapter;
namespace HarmonyLib {public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo m){}}public sealed class Harmony{public List<string> Hooks=new();public void Patch(MethodInfo m,HarmonyMethod? prefix,HarmonyMethod? postfix,object? transpiler,HarmonyMethod? finalizer,object? ilmanipulator=null){Hooks.Add(m.DeclaringType!.Name+"."+m.Name);}}}
namespace Il2CppSystem {public class Object {static long Next;public IntPtr Pointer{get;private set;}=new(++Next);public new Object MemberwiseClone(){var copy=(Object)base.MemberwiseClone();copy.Pointer=new(++Next);return copy;}public T Cast<T>()where T:class=>this as T??throw new InvalidCastException();}public sealed class WeakReference:Object{readonly System.WeakReference<object> Weak;public WeakReference(object o,bool resurrect){Weak=new(o);}public object? Target=>Weak.TryGetTarget(out var o)?o:null;public bool IsAlive=>Target is not null;}}
namespace Il2CppSystem.Collections.Generic {public class List<T>:Il2CppSystem.Object{readonly System.Collections.Generic.List<T> Inner=new();public int Count=>Inner.Count;public T this[int i]=>Inner[i];public void Add(T value)=>Inner.Add(value);public bool Remove(T value)=>Inner.Remove(value);public bool Contains(T value)=>Inner.Contains(value);}}
public class NativeArray<T>:Il2CppSystem.Object {public T[] Values{get;set;}=Array.Empty<T>();public int Length=>Values.Length;public int Count=>Length;public T this[int i]=>Values[i];}
public struct WorldCoordinates{public int X{get;set;}public int Y{get;set;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
public enum UnitType{warrior,dcp_spy,dcp_spymaid,dcp_motherfogger,dcp_mimic_form_dcp_motherfogger,dcp_mimic_form_dcp_spy,transportship,scoutship}
public enum UnitEffect { Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8 }
public static class EnumCache<T>where T:struct,Enum{public static string GetName(T x)=>x.ToString();public static T GetType(string x)=>Enum.Parse<T>(x);}
namespace Polytopia.Data{public sealed class ImprovementData{public enum Type{none,city,lighthouse}}public sealed class UnitData:Il2CppSystem.Object{public UnitType type{get;set;}public int Sight{get;set;}=2;}}
public sealed class UnitState:Il2CppSystem.Object
{
 public uint id{get;set;}public byte owner{get;set;}=1;public UnitType type{get;set;}=UnitType.dcp_spy;public ushort health{get;set;}=100;
 public UnitState? passengerUnit{get;set;}public bool moved{get;set;}public bool attacked{get;set;}public bool Linked{get;set;}public WorldCoordinates coordinates{get;set;}public Polytopia.Data.UnitData UnitData{get;set;}=new();
 public Il2CppSystem.Collections.Generic.List<UnitEffect> effects{get;set;}=new();public bool HasLeader()=>Linked;public bool HasFollower()=>false;public bool HasEffect(UnitEffect e)=>effects.Contains(e);
 public void MakeExhauseted(GameState state){moved=attacked=true;effects.Remove(UnitEffect.Boosted);effects.Remove(UnitEffect.DoubleReady);Fault.Check("exhaust");}
}
public sealed class TileData:Il2CppSystem.Object
{
 public byte owner{get;set;}public ImprovementState? improvement{get;set;}public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>improvement?.type==t;public WorldCoordinates coordinates{get;set;}public UnitState? unit{get;set;}public UnitState? GetUnit(GameState state,byte actor,bool force)=>unit is not null&&!unit.HasEffect(UnitEffect.Invisible)&&GetExplored(actor)?unit:null;public int terrain{get;set;}
 Il2CppSystem.Collections.Generic.List<byte>? Explorers=new();public Il2CppSystem.Collections.Generic.List<byte>? explorers{get=>Explorers;set{Explorers=value;Fault.Check("fog");}}
 public bool GetExplored(byte player){bool yes=explorers?.Contains(player)==true;NativeBlackout.AfterGetExplored(this,player,ref yes);return yes;}
 public void SetExplored(byte player,bool explored){if(!NativeBlackout.BeforeSetExplored(this,player,explored))return;explorers??=new();if(explored){if(!explorers.Contains(player))explorers.Add(player);}else explorers.Remove(player);}
}
public sealed class MapData:Il2CppSystem.Object{public NativeArray<TileData> Tiles{get;set;}=new();}
public sealed class PlayerState:Il2CppSystem.Object{public byte Id{get;set;}public uint score{get;set;}=10000;public WorldCoordinates Capital{get;set;}=new(-1,-1);Il2CppSystem.Collections.Generic.List<TaskBase> Tasks=new();public Il2CppSystem.Collections.Generic.List<TaskBase> tasks{get=>Tasks;set{Fault.Check("tasks-before");Tasks=value;Fault.Check("tasks-after");}}public int Currency{get;set;}=37;public bool Treaty{get;set;}}
public sealed class GameState:Il2CppSystem.Object
{
 public bool Lighthouses{get;set;}public enum State{Started,Ended}public State CurrentState{get;set;}=State.Started;public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=4;
 public MapData Map{get;set;}=new();public Il2CppSystem.Collections.Generic.List<PlayerState> PlayerStates{get;set;}=new();Il2CppSystem.Collections.Generic.List<ActionBase> Stack=new();public Il2CppSystem.Collections.Generic.List<ActionBase> ActionStack{get=>Stack;set{Fault.Check("queue-before");Stack=value;Fault.Check("queue-after");}}
 public bool HasFlaggedNeedForUpdateRoutes{get;set;}public bool TryGetPlayer(byte id,out PlayerState? player){player=Native.Items(PlayerStates).Cast<PlayerState>().SingleOrDefault(p=>p.Id==id);return player is not null;}public bool TryGetUnit(uint id,out UnitState? u){u=Map.Tiles.Values.Select(t=>t.unit).SingleOrDefault(x=>x?.id==id);return u is not null;}
}
public static class MapDataExtensions
{
 public static void UpdateRoutes(GameState state,Il2CppSystem.Collections.Generic.List<TileData> tiles){}
 public static TileData? GetTile(MapData map,WorldCoordinates at)=>map.Tiles.Values.SingleOrDefault(t=>t.coordinates.X==at.X&&t.coordinates.Y==at.Y);
 public static Il2CppSystem.Collections.Generic.List<TileData> GetArea(MapData map,WorldCoordinates at,int radius,bool diagonal,bool include){var result=new Il2CppSystem.Collections.Generic.List<TileData>();foreach(var t in GetAreaSorted(map,at,radius,diagonal,include).Values)result.Add(t);return result;}
 public static NativeArray<TileData> GetAreaSorted(MapData map,WorldCoordinates at,int radius,bool diagonal,bool include)=>new(){Values=map.Tiles.Values.Where(t=>Math.Max(Math.Abs(t.coordinates.X-at.X),Math.Abs(t.coordinates.Y-at.Y))<=radius&&(include||t.coordinates.X!=at.X||t.coordinates.Y!=at.Y)).ToArray()};
}
public static class PlayerDiplomacyExtensions{public static bool HasPeaceWith(PlayerState player,byte opponent)=>player.Treaty;}
public static class UnitDataExtensions{public static int GetSightRange(Polytopia.Data.UnitData data)=>data.Sight;}
public class ActionBase:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;}
public sealed class ExploreAction:ActionBase
{
 public WorldCoordinates Coordinates{get;set;}public bool IsValid(GameState state)=>!MapDataExtensions.GetTile(state.Map,Coordinates)!.GetExplored(PlayerId);
 public void Execute(GameState state){var tile=MapDataExtensions.GetTile(state.Map,Coordinates)!;tile.SetExplored(PlayerId,true);if(state.Lighthouses){state.HasFlaggedNeedForUpdateRoutes=true;state.ActionStack.Add(new IncreaseScoreAction(PlayerId,ScoreSheet.exploreValue,Coordinates));if(ExploreLightHouseTask.EvaluateTile(tile,state)){state.TryGetPlayer(PlayerId,out var p);ActionUtils.OnRevealLighthouseTile(state,p!,Coordinates);}}}
}
public class SourceAction:ActionBase{public uint UnitId{get;set;}public WorldCoordinates Coordinates{get;set;}public WorldCoordinates Origin{get;set;}public Action<GameState>? Body;public virtual void Execute(GameState s)=>Body?.Invoke(s);}
public sealed class MoveAction:SourceAction{public override void Execute(GameState s)=>base.Execute(s);}public sealed class TrainAction:SourceAction{public override void Execute(GameState s)=>base.Execute(s);}public sealed class ConvertAction:SourceAction{public override void Execute(GameState s)=>base.Execute(s);}public sealed class EstablishEmbassyAction:SourceAction{public override void Execute(GameState s)=>base.Execute(s);}public sealed class RevealCapitalAction:SourceAction{public override void Execute(GameState s)=>base.Execute(s);}
public static partial class ActionUtils
{
 public static void ExploreFromTile(GameState state,PlayerState player,TileData tile,int sight,bool queue)
 {
  NativeBlackoutDiscovery.BeforeFootprint(state,player,tile,sight,out var scope);NativeRogueSight.BeforeFootprint(state,player,tile,sight,out var rogue);
  try{foreach(var t in MapDataExtensions.GetAreaSorted(state.Map,tile.coordinates,tile.terrain==4?2:sight,true,true).Values)ExploreTile(state,player,t,queue);}
  finally{NativeRogueSight.FootprintFinished(null,rogue);NativeBlackoutDiscovery.FootprintFinished(null,scope);}
 }
 public static void ExploreTile(GameState state,PlayerState player,TileData tile,bool queue)
 {
  bool run=NativeBlackoutDiscovery.BeforeTile(state,player,tile,queue,out var scope);NativeRogueSight.BeforeTile(state,player,tile,queue,out var rogue);Exception? error=null;
  try{if(!run)return;if(!tile.GetExplored(player.Id)){if(queue)state.ActionStack.Add(new ExploreAction{PlayerId=player.Id,Coordinates=tile.coordinates});else {if(state.Lighthouses)ActionUtils.AddScore(state,player,ScoreSheet.exploreValue);tile.SetExplored(player.Id,true);}}NativeBlackoutDiscovery.AfterTile(scope);NativeRogueSight.AfterTile(true,rogue);Fault.Check("sight");}
  catch(Exception e){error=e;throw;}finally{NativeRogueSight.TileFinished(error,rogue);NativeBlackoutDiscovery.TileFinished(error,scope);}
 }
}
public class CommandBase:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public WorldCoordinates Coordinates{get;set;}public WorldCoordinates Origin{get;set;}public WorldCoordinates From{get;set;}}
public sealed class AttackCommand:CommandBase{}public sealed class MoveCommand:CommandBase{}public sealed class TrainCommand:CommandBase{}public sealed class BuildCommand:CommandBase{}public sealed partial class CityRewardCommand:CommandBase{}
public static class Fault{public static string At="";public static void Check(string at){if(At==at){At="";throw new IOException("Injected "+at);}}}
namespace Deception.PolyModAdapter
{
 internal static class Entry{internal static void Note(string kind,string text){}}
 internal static class RecruitmentHooks{internal static bool Registered=true;}
 internal static class NativeRegroupSession{internal static Action? OnSessionEnded;internal static bool Enabled=true;internal static HashSet<object> Owned=new();internal static bool Ready(object state)=>Enabled&&Owned.Contains(state);internal static List<string> Faults=new();internal static void Fault(string reason)=>Faults.Add(reason);}
 internal static class NativeGameStateEnvelope
 {
  internal sealed class Snapshot{internal long Revision{get;}readonly byte[] Payload;internal Snapshot(long r,byte[] p){Revision=r;Payload=(byte[])p.Clone();}internal byte[] Bytes()=>(byte[])Payload.Clone();}
  internal static Dictionary<object,Snapshot> Values=new();internal static Snapshot? Get(object state)=>Values.GetValueOrDefault(state);
  internal static void Set(object state,long revision,byte[] data){NativeIdentityLedgerCodec.Decode(data,revision);Values[state]=new(revision,data);Fault.Check("ledger");}
 }
 internal static class NativeLedgerOwnership{internal sealed class WeakIdentity{readonly System.WeakReference<object> Weak;internal WeakIdentity(object o){Weak=new(o);}internal object? Target()=>Weak.TryGetTarget(out var o)?o:null;internal bool Matches(object o)=>Native.Same(Target(),o);}}
 // This retained Blackout fixture has no native death history; completed-death reconciliation is covered by rogue-sight-0378 with the real ledger.
 internal static class NativeLedgerState{internal static (long Revision,byte[] Payload,bool Changed) Reconcile(object state,NativeGameStateEnvelope.Snapshot snapshot)=>(snapshot.Revision,snapshot.Bytes(),false);internal sealed record Body(object Unit,bool Root);internal static Dictionary<uint,Body> Graph(object state)=>((GameState)state).Map.Tiles.Values.Where(t=>t.unit is not null).ToDictionary(t=>t.unit!.id,t=>new Body(t.unit!,true));internal static void Validate(object state,long revision,byte[] bytes){var ledger=NativeIdentityLedgerCodec.Decode(bytes,revision);Fault.Check("validate");if(ledger.Units.Any(r=>r.Rogue?.Sight is not null))Fault.Check("validate-after-sight");}}
 internal static class NativeRogueRules
 {
  internal static LedgerUnit? Row(object state,object unit){var snap=NativeGameStateEnvelope.Get(state);return snap is null?null:NativeIdentityLedgerCodec.Decode(snap.Bytes(),snap.Revision).Units.SingleOrDefault(x=>x.Alive&&x.NativeId==((UnitState)unit).id);}
  internal static void SetOwner(object unit,byte owner){var u=(UnitState)unit;u.owner=owner;if(u.passengerUnit is not null)u.passengerUnit.owner=owner;}
  internal static bool CanCommand(object state,object unit,byte actor)
  {
   var s=(GameState)state;var u=(UnitState)unit;if(s.CurrentPlayer!=actor||u.owner!=actor)return false;
   var snap=NativeGameStateEnvelope.Get(s);var row=snap is null?null:NativeIdentityLedgerCodec.Decode(snap.Bytes(),snap.Revision).Units.SingleOrDefault(x=>x.Alive&&x.NativeId==u.id);
   return row?.Rogue is not LedgerRogue r||r.Commander==actor&&r.AcquiredTurn==s.CurrentTurn;
  }
 }
 internal sealed record NativeExchangeCommitResult(bool AlreadyCommitted,long BeforeRevision,long AfterRevision,NativeExchangePlan Plan);

}
