using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Deception.Core;
public class Identity {static long Next;public IntPtr Pointer{get;}=new(++Next);public T Cast<T>() where T:class=>(T)(object)this;}
namespace HarmonyLib
{
 public sealed class HarmonyMethod{public MethodInfo Method;public HarmonyMethod(MethodInfo m){Method=m;}}
 public sealed class Harmony {public static List<MethodInfo> Targets=new();public void Patch(MethodInfo m,HarmonyMethod? p,HarmonyMethod? q,object? t,HarmonyMethod? f,object? il){Targets.Add(m);} }
}
namespace Il2CppSystem {public sealed class Action{} public sealed class Guid {public System.Guid Value;public Guid(System.Guid value){Value=value;}public override string ToString()=>Value.ToString("D");}}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays {public sealed class Il2CppStructArray<T>{}}
namespace PolytopiaBackendBase.Game {public enum StateUpdateReason {Unknown=0,GameCreated=2,ValidCommand=10,StateReset=17}public enum GameType{Local,Hotseat,Multiplayer}}
public enum UnitType {warrior,dcp_mimic_form_warrior}
public static class EnumCache<T> where T:Enum {public static string GetName(T x)=>x.ToString();}
public sealed class GameSettings:Identity {}
public sealed class TrainAction:Identity {public UnitType Type{get;set;}=UnitType.dcp_mimic_form_warrior;public int Cost{get;set;}=RecruitmentCatalog.Cost;public bool trainActionAborted{get;set;}}
public sealed class MapData:Identity {}
public sealed class MapGeneratorSettings:Identity {}
public sealed class MapGenerator {public void Generate(GameState state,MapGeneratorSettings settings,Il2CppSystem.Action? done,int seed){} }
public sealed class GameState:Identity {public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=7;public int Version{get;set;}=123;public GameSettings Settings{get;set;}=new();public MapData Map{get;set;}=new();public List<uint> StartingUnits=new();}
public sealed class CommandBase:Identity {}
public class ActionManager {public GameState? GameState{get;set;}public int Updates;public void Update(){Updates++;}public virtual bool ExecuteCommand(CommandBase command,out string reason){reason="";return true;}}
public class ClientBase:Identity
{
 public enum ClientType {Local=0,Remote=1,PassAndPlay=2,Replay=3}
 public ClientType clientType{get;set;}=ClientType.Local;public bool IsReplay{get;set;}public bool IsSpectating{get;set;}
 public Il2CppSystem.Guid gameId{get;set;}=new(System.Guid.NewGuid());
 public GameState? initialGameState{get;set;}public GameState? currentGameState{get;set;}public GameState? targetGameState{get;set;}public GameState? lastTurnGameState{get;set;}
 public GameState? GameState=>currentGameState;public ActionManager? ActionManager{get;set;}
 public byte LocalPlayerId{get;set;}=1;
 public void OpenSession(Il2CppSystem.Guid guid,PolytopiaBackendBase.Game.GameType type){}
 public void UpdateGameStateImmediate(GameState state,PolytopiaBackendBase.Game.StateUpdateReason reason){}
 public void CreateOrResetActionManager(ushort id){} public void PrepareSession(){} public void SaveSession(string name,bool a,bool b){}
 public void PrepareAndValidateSaveState(){} public void SessionOpened(){} public void EndSession(){}
 public int CreateSession(GameSettings settings,Il2CppSystem.Guid guid)=>0;
 public int CreateSession(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> bytes,Il2CppSystem.Guid guid)=>0;
}
public sealed class RemoteClient:ClientBase {public RemoteClient(){clientType=ClientType.Remote;}}
public sealed class GameManager {public static void SetRemoteClient(RemoteClient client){Client=client;}public static GameManager? instance{get;set;}=new();public object? fakeSession{get;set;}public static ClientBase? Client{get;set;}}
public sealed class SelectTribePopup {public void SetBackground(){}}
namespace Deception.PolyModAdapter
{
 internal static class Entry {internal static List<string> Notes=new();internal static void Note(string k,string m)=>Notes.Add(k+":"+m);}
 internal static class NativeExecutionTrigger {internal static bool Requested=>false;}
 internal static class RecruitmentHooks
 {
  internal static Dictionary<string,object> Forms=new(){{"dcp_mimic_form_warrior",new()}};
  internal static object Data(object state,string rule)=>Forms[rule];
 }
 internal static class NativeBlackout {internal static bool Fail;internal static int Binds;internal static void Bind(object state){Binds++;if(Fail)throw new InvalidOperationException("Injected binding failure");}}
 internal static class NativeLedgerOwnership
 {
  internal sealed class WeakIdentity {readonly WeakReference<object> Value;internal WeakIdentity(object o){Value=new(o);}internal object? Target()=>Value.TryGetTarget(out var o)?o:null;internal bool Matches(object o)=>Target() is object target&&Native.Same(target,o);}
 }
 // Graph witnesses are inert native-boundary ports. Real session callbacks,
 // frame authorization, immutable Snapshot and all Core codecs execute.
 internal static class NativeLedgerState
 {
  internal sealed record Body(object Unit,bool Root);
  internal static Dictionary<uint,Body> Graph(object state)=>new();
  internal static int Validations,Reconciliations,Bootstraps;internal static bool FailValidate;
  internal static void Validate(object state,long revision,byte[] payload){Validations++;if(FailValidate)throw new InvalidOperationException("Injected validation failure");NativeIdentityLedgerCodec.Decode(payload,revision);}
  internal static NativeIdentityLedger Bootstrap(object state,Guid epoch){Bootstraps++;var ids=((GameState)state).StartingUnits;return new(epoch,(ulong)ids.Count+1,1,ids.Select((id,i)=>new LedgerUnit((ulong)i+1,id,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0)).ToArray(),Array.Empty<LedgerReceipt>());}
  internal static (bool Changed,long Revision,byte[] Payload) Reconcile(object state,NativeGameStateEnvelope.Snapshot snapshot){Reconciliations++;return(false,snapshot.Revision,snapshot.Bytes());}
 }
 internal static partial class NativeGameStateEnvelope
 {
  internal static readonly Dictionary<object,Snapshot> Frames=new();internal static int Reads,Writes;
  internal static Snapshot? Get(object state){Reads++;return Frames.GetValueOrDefault(state);}
  internal static void Set(object state,long revision,byte[] payload){Writes++;Frames[state]=new(revision,payload);}
  internal static void Forget(object state)=>Frames.Remove(state);
 }
 internal static partial class NativeRegroupSession
 {
  internal static void FixtureReset()
  {
   Enabled=true;Current=null;SummaryScope=null;SummaryStates.Clear();States.Clear();Buying=null;OnSessionEnded=null;NativeGameStateEnvelope.Frames.Clear();
   NativeGameStateEnvelope.Reads=NativeGameStateEnvelope.Writes=0;NativeLedgerState.Validations=NativeLedgerState.Reconciliations=NativeLedgerState.Bootstraps=0;
   NativeLedgerState.FailValidate=false;NativeBlackout.Fail=false;NativeBlackout.Binds=0;Entry.Notes.Clear();GameManager.instance=new();GameManager.Client=null;
  }
  internal static bool FixtureOwned(object state)=>Find(state) is not null;
 }
}
