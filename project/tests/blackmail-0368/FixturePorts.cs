// Exact managed signatures, synthetic native objects. Production NativeApi,
// Blackmail planning/commit/event adapter and persistence codecs are compiled.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
namespace HarmonyLib { public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo m){}}public sealed class Harmony{public List<string> Hooks=new();public void Patch(MethodInfo m,HarmonyMethod? prefix,HarmonyMethod? postfix,object? transpiler,HarmonyMethod? finalizer,object? il=null)=>Hooks.Add(m.DeclaringType!.Name+"."+m.Name);}}
namespace Il2CppSystem {public class Object{static long Next;public IntPtr Pointer{get;}public Object(){Pointer=new(++Next);}protected Object(IntPtr pointer){Pointer=pointer;}}}
public struct WorldCoordinates{public int X{get;set;}public int Y{get;set;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
namespace Polytopia.Data {public sealed class ImprovementData{public enum Type{city}}public sealed class TechData{public enum Type{dcp_espionage}}}
public static class EnumCache<T>where T:struct,Enum{public static string GetName(T v)=>v.ToString();public static T GetType(string s)=>Enum.Parse<T>(s);}
public sealed class PlayerState:Il2CppSystem.Object
{
 public Il2CppSystem.Collections.Generic.Dictionary<byte,DiplomacyRelation> relations{get;set;}=new();public byte Id{get;set;}public bool Alive{get;set;}=true;public bool Espionage{get;set;}=true;
 public HashSet<byte> Known=new(){1,2,3,4};sealed class Bank{internal int Stars=20;}Bank Vault=new();public PlayerState(){}PlayerState(PlayerState p):base(p.Pointer){Id=p.Id;Alive=p.Alive;Espionage=p.Espionage;Vault=p.Vault;Known=p.Known;relations=p.relations;}public PlayerState Wrapper()=>new(this);
 public int Currency{get{Fault.Check("get-currency");return Vault.Stars;}set{Vault.Stars=value;Fault.Check("currency"+Id);}}
}
public static class PlayerExtensions
{
 public static bool IsAlive(PlayerState p,GameState s)=>p.Alive;
 public static bool HasTech(PlayerState p,Polytopia.Data.TechData.Type tech)=>p.Espionage;
}
public sealed class UnitState:Il2CppSystem.Object
{
 public uint id{get;set;}public byte owner{get;set;}=2;public WorldCoordinates home{get;set;}public WorldCoordinates coordinates{get;set;}
 public UnitState? passengerUnit{get;set;}public bool Hidden;public ushort health{get;set;}=100;
}
public sealed class TileData:Il2CppSystem.Object
{
 public WorldCoordinates coordinates{get;set;}public byte owner{get;set;}=2;public bool City{get;set;}=true;public HashSet<byte> Visible=new(){1,2,3,4};
 public UnitState? unit{get;set;}public int Income=6;
 public UnitState? GetUnit(GameState s,byte actor,bool includeInvisible)=>GetExplored(actor)&&unit is not null&&(!unit.Hidden||includeInvisible||unit.owner==actor)?unit:null;
 public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>City;
 public bool GetExplored(byte p)=>Visible.Contains(p);
}
public static class TileDataExtensions{public static int CalculateWork(TileData t,GameState s)=>t.Income;}
public sealed class MapData:Il2CppSystem.Object{public List<TileData> Tiles=new();}
public static class MapDataExtensions{public static TileData? GetTile(MapData m,WorldCoordinates at)=>m.Tiles.SingleOrDefault(t=>t.coordinates.X==at.X&&t.coordinates.Y==at.Y);}
public enum CommandType{Build=1,EndTurn=15,Resign=35}
public enum ActionType{EndCommand,Meet}
public class ActionBase:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public bool Valid=true;public virtual bool IsValid(GameState s)=>Valid;public virtual ActionType GetActionType()=>ActionType.EndCommand;}
public sealed class EndCommandAction:ActionBase{public CommandType commandType{get;set;}=CommandType.EndTurn;public void Execute(GameState s){}}
public sealed class CaptureCityAction:ActionBase{public void Execute(GameState s){}}
public sealed class DestroyImprovementAction:ActionBase{public void Execute(GameState s){}}
public sealed class PeaceRequestResponseAction:ActionBase{public bool Accepted{get;set;}public byte SenderId{get;set;}public void Execute(GameState s){}}
public sealed class WipePlayerAction:ActionBase{public void Execute(GameState s){}}
public sealed class IncreaseCurrencyAction:ActionBase{public int Amount;public void Execute(GameState s){s.TryGetPlayer(PlayerId,out var p);p!.Currency+=Amount;}}
public sealed class GameState:Il2CppSystem.Object
{
 public enum State{Started,Ended}public State CurrentState{get;set;}=State.Started;
 public uint CurrentTurn{get;set;}=4;public byte CurrentPlayer{get;set;}=1;
 public MapData Map{get;set;}=new();public List<PlayerState> Players=new();Il2CppSystem.Collections.Generic.List<ActionBase> stack=new();public Il2CppSystem.Collections.Generic.List<ActionBase> ActionStack{get=>stack;set{stack=value;Fault.Check("contact-attach");}}
 public bool Rewrap;public bool TryGetPlayer(byte id,out PlayerState? p){p=Players.SingleOrDefault(p=>p.Id==id);if(Rewrap&&p is not null)p=p.Wrapper();return p is not null;}
 public bool TryGetUnit(uint id,out UnitState? unit){unit=Map.Tiles.Select(t=>t.unit).Where(u=>u is not null).SelectMany(u=>u!.passengerUnit is null?new[]{u}:new[]{u,u.passengerUnit}).SingleOrDefault(u=>u!.id==id);return unit is not null;}
 public bool UnitMoved,UnitAttacked,CityExhausted;
 public bool StrictLedgerBoundary;public int PreparedBoundaries;
}
public static class Fault{public static string At="";public static void Check(string point){if(At==point){At="";throw new System.IO.IOException("Injected "+point);}}}
namespace Deception.PolyModAdapter
{
 internal static class RecruitmentHooks{internal const string TT="Polytopia.Data.TechData+Type";internal static bool Registered=true;}
 internal static class NativeRegroupSession
 {internal static bool Enabled=true;internal static HashSet<object> Owned=new();internal static bool Ready(object s)=>Enabled&&Owned.Contains(s);internal static List<string> Faults=new();internal static void Fault(string why)=>Faults.Add(why);
  internal static void Prepare(object state)
  {
   var s=(GameState)state;if(!s.StrictLedgerBoundary)return;
   if(!Ready(s))throw new System.IO.InvalidDataException("Unowned fixture boundary");
   s.PreparedBoundaries++;var before=NativeGameStateEnvelope.Get(s)!;var ledger=NativeIdentityLedgerCodec.Decode(before.Bytes(),before.Revision);
   var roots=s.Map.Tiles.Select(t=>t.unit).Where(u=>u is not null).ToDictionary(u=>u!.id,u=>u!);
   // Narrow boundary port for genuine death/boarding/landing only. This copies
   // the production lineage rules, not native command execution or enrollment.
   var rows=ledger.Units.Select(original=>{
    var row=original;if(!row.Alive)return row;
    var boat=roots.Values.SingleOrDefault(u=>u.passengerUnit?.id==row.NativeId);
    if(boat is not null){if(row.Independent)throw new System.IO.InvalidDataException("Independent boarding requires explicit transition");return row with{NativeId=boat.id,Transport=new LedgerTransport(row.NativeId)};}
    if(!roots.ContainsKey(row.NativeId)&&row.Transport is LedgerTransport cargo&&roots.ContainsKey(cargo.PassengerNativeId))
    {if(row.Independent)throw new System.IO.InvalidDataException("Independent landing requires explicit transition");row=row with{NativeId=cargo.PassengerNativeId,Transport=null};}
    return roots.ContainsKey(row.NativeId)?row:row with{Alive=false,Independent=false,Appearance=null,Transport=null,Rogue=null};
   }).ToArray();
   var bytes=NativeIdentityLedgerCodec.Encode(ledger with{Units=rows},before.Revision);NativeLedgerState.Validate(s,before.Revision,bytes);
   if(!rows.SequenceEqual(ledger.Units))NativeGameStateEnvelope.Set(s,before.Revision,bytes);
  }
 }
 internal static class NativeExchangeCommands{internal static object? Executing;internal static bool LiveExecution(object s)=>ReferenceEquals(Executing,s);}
 internal static class NativeGameStateEnvelope
 {
  internal sealed class Snapshot{internal long Revision{get;}readonly byte[] Data;internal Snapshot(long rev,byte[] data){Revision=rev;Data=(byte[])data.Clone();}internal int Reads;internal byte[] Bytes(){Reads++;return(byte[])Data.Clone();}}
  internal static Dictionary<object,Snapshot> Values=new();internal static Snapshot? Get(object s)=>Values.GetValueOrDefault(s);
  internal static void Set(object s,long r,byte[] b){NativeIdentityLedgerCodec.Decode(b,r);Values[s]=new(r,b);Fault.Check("ledger");}
 }
 internal static class NativeLedgerState
 {internal static void Validate(object state,long r,byte[] b)
  {var ledger=NativeIdentityLedgerCodec.Decode(b,r);var s=(GameState)state;
   if(s.StrictLedgerBoundary){var roots=s.Map.Tiles.Select(t=>t.unit).Where(u=>u is not null).ToDictionary(u=>u!.id,u=>u!);
    foreach(var row in ledger.Units){bool exists=s.TryGetUnit(row.NativeId,out _);
     if(!row.Alive){if(exists)throw new System.IO.InvalidDataException("Retired identity still has a native body");continue;}
     if(!roots.TryGetValue(row.NativeId,out var root))throw new System.IO.InvalidDataException("Private identity has no native root");
     if((root.passengerUnit is not null)!=(row.Transport is not null)||root.passengerUnit is not null&&
      (root.passengerUnit.id!=row.Transport!.PassengerNativeId||root.passengerUnit.owner!=root.owner||root.passengerUnit.passengerUnit is not null))
      throw new System.IO.InvalidDataException("Actual vessel/passenger binding is missing or stale");
    }
   }Fault.Check("validate");
  }
 }
 internal sealed record NativeExchangeCommitResult(bool AlreadyCommitted,long BeforeRevision,long AfterRevision,NativeExchangePlan Plan);
}

namespace Il2CppSystem.Collections.Generic
{
 public class List<T>:System.Collections.Generic.IEnumerable<T>{readonly Il2CppSystem.Object Handle=new();readonly System.Collections.Generic.List<T> Values=new();public IntPtr Pointer=>Handle.Pointer;public int Count=>Values.Count;public T this[int index]=>Values[index];public void Add(T value){Values.Add(value);Fault.Check("contact-stage");}public bool Remove(T v)=>Values.Remove(v);public void RemoveAt(int i)=>Values.RemoveAt(i);public void Clear()=>Values.Clear();public System.Collections.Generic.IEnumerator<T> GetEnumerator()=>Values.GetEnumerator();System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();}
}
public sealed class MeetAction:ActionBase
{
 public byte OtherPlayerId{get;set;}public WorldCoordinates Coordinates{get;set;}
 public MeetAction(byte playerId,byte otherPlayerId,WorldCoordinates coordinates){PlayerId=playerId;OtherPlayerId=otherPlayerId;Coordinates=coordinates;Fault.Check("contact-create");}
 public override ActionType GetActionType()=>ActionType.Meet;
 public override bool IsValid(GameState s){Fault.Check("contact-valid");return s.TryGetPlayer(PlayerId,out var p)&&!p!.Known.Contains(OtherPlayerId);}
 public void Execute(GameState s){s.TryGetPlayer(PlayerId,out var p);p!.Known.Add(OtherPlayerId);s.ActionStack.Add(new IncreaseCurrencyAction{PlayerId=PlayerId,Amount=7});}
}

namespace Il2CppSystem.Collections.Generic
{
 public sealed class Dictionary<K,V>:Il2CppSystem.Object where K:notnull{readonly System.Collections.Generic.Dictionary<K,V> Items=new();public int Count=>Items.Count;public V this[K k]{get=>Items[k];set=>Items[k]=value;}public bool ContainsKey(K k)=>Items.ContainsKey(k);public bool Remove(K k)=>Items.Remove(k);}
}
public enum DiplomacyRelationState{Neutral=0,Peace=1,War=2,CoolDown=3}
public sealed class DiplomacyRelation:Il2CppSystem.Object{public DiplomacyRelationState State{get;set;}public int LastAttackTurn{get;set;}=-10;public int PreviousAttackTurn{get;set;}=-10;public int EmbassyLevel{get;set;}=2;}
public sealed class DestroyEmbassyAction:ActionBase
{
 public byte OpponentId{get;set;}public DestroyEmbassyAction(byte a,byte b){PlayerId=a;OpponentId=b;}
 public void Execute(GameState s){s.TryGetPlayer(OpponentId,out var p);if(p!.relations.ContainsKey(PlayerId))p.relations[PlayerId].EmbassyLevel=0;}
}
public static class PlayerDiplomacyExtensions
{
 public static DiplomacyRelation Get(PlayerState p,byte id){if(!p.relations.ContainsKey(id))p.relations[id]=new();return p.relations[id];}
 public static void ModifyDiplomacyRelationsFromAttack(PlayerState a,byte b,GameState s)
 {var ar=Get(a,b);ar.PreviousAttackTurn=ar.LastAttackTurn;ar.LastAttackTurn=(int)s.CurrentTurn;Fault.Check("hostile-actor");s.TryGetPlayer(b,out var victim);var br=Get(victim!,a.Id);br.PreviousAttackTurn=(int)s.CurrentTurn;Fault.Check("hostile-victim");s.ActionStack.Add(new DestroyEmbassyAction(a.Id,b));Fault.Check("hostile-first-child");s.ActionStack.Add(new DestroyEmbassyAction(b,a.Id));Fault.Check("hostile-second-child");}
}
