using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
using Deception.PolyModAdapter;
using L=Il2CppSystem.Collections.Generic;
namespace HarmonyLib {public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo m){}}public sealed class Harmony{public readonly List<MethodInfo> Hooks=new();public void Patch(MethodInfo m,HarmonyMethod? p,HarmonyMethod? q,object? t,HarmonyMethod? f,object? i)=>Hooks.Add(m);}}
namespace Il2CppSystem {public class Object{static long Next;public IntPtr Pointer{get;}=new(++Next);public T Cast<T>()where T:class=>this as T??throw new InvalidCastException();}}
namespace Il2CppSystem.Collections.Generic {public class List<T>:Il2CppSystem.Object {readonly System.Collections.Generic.List<T> Items=new();public int Count=>Items.Count;public T this[int i]=>Items[i];public void Add(T x)=>Items.Add(x);public void RemoveAt(int i)=>Items.RemoveAt(i);public bool Remove(T x)=>Items.Remove(x);}}
public readonly record struct WorldCoordinates(int X,int Y);
public enum UnitEffect{Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8}
public static class EnumCache<T>where T:struct,Enum{public static T GetType(string n)=>Enum.Parse<T>(n,true);public static string GetName(T v)=>v.ToString();}
namespace Polytopia.Data
{
 public class UnitData:Il2CppSystem.Object{public enum Type{warrior,dcp_spy,dcp_spymaid,dcp_motherfogger,dcp_mimic,dcp_mermimic,dcp_mimic_form_warrior,dcp_mimic_form_giant,raft,transportship,cloak}public Type type{get;set;}public int Cost=2;}
 public static class UnitAbility{public enum Type{dash=1,convert=10,hide=5}}
 public static class ImprovementData{public enum Type{city}}
 public static class TechData{public enum Type{freespirit}}
}
public sealed class UnitState:Il2CppSystem.Object
{
 public bool CopiedCloakHide;
 public uint id{get;set;}=1;public byte owner{get;set;}=1;public Polytopia.Data.UnitData.Type type{get;set;}
 public int health{get;set;}=100;public bool moved{get;set;}public bool attacked{get;set;}public bool Dash,Leader,Follower;
 public WorldCoordinates coordinates{get;set;}=new(2,3);public WorldCoordinates home{get;set;}=new(0,0);public UnitState? passengerUnit{get;set;}
 public Polytopia.Data.UnitData UnitData{get;set;}=new();public L.List<UnitEffect> effects{get;set;}=new();
 public bool HasLeader()=>Leader;public bool HasFollower()=>Follower;public bool HasEffect(UnitEffect e)=>Native.Items(effects).Cast<UnitEffect>().Contains(e);
 public bool CanMove()=>!moved&&!HasEffect(UnitEffect.Frozen);
 public bool CanAttack(){bool r=false;if(!RecruitmentHooks.NoAttack(this,ref r)||!NativeSpyActions.NoAttack(this,ref r))return r;return !attacked&&!HasEffect(UnitEffect.Frozen)&&(!moved||Dash);}
 public int GetFullCost(GameState s){int r=0;return NativeInvestment.DisbandCost(this,s,ref r)?UnitData.Cost+(passengerUnit?.GetFullCost(s)??0):r;}
 public void MakeExhauseted(GameState s){moved=attacked=true;}
}
public sealed class PlayerState:Il2CppSystem.Object{public byte Id{get;set;}=1;public bool FreeSpirit,Peace,Cooldown;public int Currency{get;set;}=20;}
public sealed class TileData:Il2CppSystem.Object{public WorldCoordinates coordinates{get;set;}=new(2,3);public UnitState? unit{get;set;}public byte owner{get;set;}public bool City=true,Explored=true;public bool GetExplored(byte id)=>Explored;public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>City;}
public sealed class MapData:Il2CppSystem.Object{public readonly Dictionary<WorldCoordinates,TileData> Tiles=new();}
public static class MapDataExtensions{public static TileData? GetTile(MapData m,WorldCoordinates at)=>m.Tiles.GetValueOrDefault(at);}
public sealed class GameState:Il2CppSystem.Object
{
 public enum State{Started,Ended}public State CurrentState{get;set;}=State.Started;public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=4;public ushort CurrentCommand{get;set;}
 public MapData Map{get;set;}=new();public readonly Dictionary<byte,PlayerState> Players=new();public L.List<ActionBase> ActionStack{get;set;}=new();public bool Pending;public bool HasFlaggedNeedForUpdateRoutes{get;set;}
 public bool TryGetPlayer(byte id,out PlayerState? p)=>Players.TryGetValue(id,out p);public bool TryGetUnit(uint id,out UnitState? u){u=Map.Tiles.Values.Select(t=>t.unit).FirstOrDefault(u=>u?.id==id);return u is not null;}
 public void Drain(){while(ActionStack.Count>0){var a=ActionStack[ActionStack.Count-1];a.Execute(this);ActionStack.Remove(a);}}
}
public static class UnitDataExtensions
{
 public static bool CanDisband(UnitState u,GameState s,PlayerState p){bool r=false;OrdinaryActionEligibility.EnterDisband(u,out var frame);try{if(!NativeRogueRules.Ban(u,s,ref r))return r;return !u.moved&&!u.attacked&&p.FreeSpirit&&!u.HasLeader();}finally{OrdinaryActionEligibility.ExitPredicate(null,frame);}}
 public static bool CanCapture(UnitState u,GameState s,TileData t,bool ignoreFlags,bool sameTile)
 {bool r=false;OrdinaryActionEligibility.EnterCapture(u,out var frame);try{if(!NativeRogueRules.Ban(u,s,ref r))return r;return u.owner!=255&&(ignoreFlags||u.CanMove()&&u.CanAttack())&&(!sameTile||u.coordinates==t.coordinates)&&!u.HasLeader()&&!s.Players[u.owner].Peace&&!s.Players[u.owner].Cooldown&&t.City&&t.owner!=u.owner;}finally{OrdinaryActionEligibility.ExitPredicate(null,frame);}}
 public static bool CanExamineRuins(UnitState u,GameState s,TileData t){bool r=false;OrdinaryActionEligibility.EnterRuins(u,out var frame);try{if(!NativeRogueRules.Ban(u,s,ref r))return r;return !t.City&&u.CanMove()&&u.CanAttack();}finally{OrdinaryActionEligibility.ExitPredicate(null,frame);}}
 public static bool CanAttackAnything(UnitState u,int range,GameState s){bool b=false;return RecruitmentHooks.NoAttackAny(u,ref b)&&NativeSpyActions.NoAttackAny(u,ref b)&&u.CanAttack();}
 public static bool CanBePromoted(UnitState u,GameState s)=>true;
 public static bool HasAbility(UnitState u,Polytopia.Data.UnitAbility.Type a)=>a==Polytopia.Data.UnitAbility.Type.hide&&(u.CopiedCloakHide||u.type==Polytopia.Data.UnitData.Type.cloak);
}
public static class BattleHelpers{public static bool CanDefenderRetaliate(GameState s,UnitState a,UnitState d){bool b=false;return RecruitmentHooks.NoRetaliation(d,ref b)&&NativeSpyActions.NoRetaliation(d,ref b);}}
public class CommandBase:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public virtual bool IsValid(GameState s,out string error){error="native";return PlayerId==s.CurrentPlayer&&!s.Pending;}public virtual void Execute(GameState s){s.CurrentCommand++;s.ActionStack.Add(new EndCommandAction{PlayerId=PlayerId});}}
public class ActionBase:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public virtual void Execute(GameState s){}}
public sealed class EndCommandAction:ActionBase{}
public sealed class CaptureCommand:CommandBase
{
 public WorldCoordinates Coordinates{get;set;}public uint UnitId{get;set;}=1;
 public override bool IsValid(GameState s,out string error)=>base.IsValid(s,out error)&&s.TryGetUnit(UnitId,out var u)&&MapDataExtensions.GetTile(s.Map,Coordinates) is TileData t&&UnitDataExtensions.CanCapture(u!,s,t,false,true);
 public override void Execute(GameState s){if(!NativeOrdinaryCommands.CaptureExecute(this,s))return;base.Execute(s);var t=MapDataExtensions.GetTile(s.Map,Coordinates)!;t.unit!.MakeExhauseted(s);s.ActionStack.Add(new CaptureCityAction{PlayerId=PlayerId,Tile=t});}
}
public sealed class CaptureCityAction:ActionBase{public TileData Tile=null!;public override void Execute(GameState s){Tile.owner=PlayerId;Tile.unit!.home=Tile.coordinates;}}
public sealed class DisbandCommand:CommandBase
{
 public WorldCoordinates Coordinates{get;set;}public DisbandCommand(){}public DisbandCommand(byte p,WorldCoordinates at){PlayerId=p;Coordinates=at;}
 public override bool IsValid(GameState s,out string error){bool r=base.IsValid(s,out error);NativeOrdinaryCommands.DisbandValid(this,s,ref error,ref r);return r;}
 public override void Execute(GameState s){if(!NativeOrdinaryCommands.DisbandExecute(this,s))return;base.Execute(s);s.ActionStack.Add(new DisbandUnitAction{PlayerId=PlayerId,Coordinates=Coordinates,GetPaid=true});}
}
public sealed class DisbandUnitAction:ActionBase
{
 public WorldCoordinates Coordinates{get;set;}public bool GetPaid{get;set;}public bool ThrowBeforeCost;public Action? During;
 public override void Execute(GameState s)
 {
  bool run=NativeOrdinaryCommands.DisbandAction(this,s);NativeInvestment.BeforeDisband(this,s,out var frame);Exception? error=null;
  try{if(!run)return;During?.Invoke();if(ThrowBeforeCost)throw new InvalidOperationException("native-disband");var t=MapDataExtensions.GetTile(s.Map,Coordinates);if(t?.unit is not UnitState u)return;if(GetPaid)for(int n=u.GetFullCost(s)/2;n>0;n--)s.ActionStack.Add(new IncreaseCurrencyAction{PlayerId=PlayerId,Source=Coordinates,Amount=1});t.unit=null;}
  catch(Exception e){error=e;throw;}finally{NativeInvestment.DisbandFinished(error,frame);}
 }
}
public sealed class IncreaseCurrencyAction:ActionBase{public WorldCoordinates Source{get;set;}public int Amount{get;set;}public override void Execute(GameState s){s.Players[PlayerId].Currency+=Amount;}}
public sealed class TrainAction:ActionBase{public Polytopia.Data.UnitData.Type Type{get;set;}public WorldCoordinates Coordinates{get;set;}public int Cost{get;set;}}
public sealed class UpgradeAction:ActionBase{public WorldCoordinates Coordinates{get;set;}public int Cost{get;set;}}
public sealed class StartTurnAction:ActionBase{public override void Execute(GameState s){NativeRogueRules.AfterStart(this,s);}}
public static class CommandUtils
{
 public static L.List<CommandBase> GetUnitActions(GameState s,PlayerState p,TileData t,bool unavailable)
 {
  var native=new L.List<CommandBase>();var u=t.unit;
  if(u is not null&&u.owner==p.Id){if(UnitDataExtensions.CanCapture(u,s,t,false,true))native.Add(new CaptureCommand{PlayerId=p.Id,Coordinates=t.coordinates,UnitId=u.id});if(u.CanAttack()&&s.CurrentPlayer==p.Id&&UnitDataExtensions.CanDisband(u,s,p))native.Add(new DisbandCommand(p.Id,t.coordinates));}
  LastNative=native;object result=native;NativeOrdinaryCommands.DisbandMenu(s,p,t,ref result);return (L.List<CommandBase>)result;
 }
 public static L.List<CommandBase>? LastNative;
}
namespace Deception.PolyModAdapter
{
 internal static class Entry{internal static readonly System.Collections.Generic.List<string> Notes=new();internal static void Note(string a,string b)=>Notes.Add(a+":"+b);}
 internal static class NativeRegroupSession{internal static bool Enabled=true;internal static readonly HashSet<object> States=new();internal static bool Ready(object s)=>Enabled&&States.Contains(s);internal static void Prepare(object s){}internal static void Fault(string e)=>throw new Exception(e);}
 internal static class NativeGameStateEnvelope
 {internal sealed record Snapshot(long Revision,byte[] Data){internal byte[] Bytes()=>Data.ToArray();}static readonly Dictionary<object,Snapshot> Values=new();internal static Snapshot? Get(object s)=>Values.GetValueOrDefault(s);internal static void Set(object s,long r,byte[] b){NativeIdentityLedgerCodec.Decode(b,r);Values[s]=new(r,b.ToArray());}}
 internal static class NativeLedgerOwnership{internal sealed class WeakIdentity{readonly WeakReference<object> Weak;internal WeakIdentity(object s)=>Weak=new(s);internal bool Matches(object s)=>Weak.TryGetTarget(out var t)&&Native.Same(t,s);}}
 internal static class NativeLedgerState{internal sealed record Body(object Unit,bool Root);internal static Dictionary<uint,Body> Graph(object s)=>((GameState)s).Map.Tiles.Values.Where(t=>t.unit!=null).ToDictionary(t=>t.unit!.id,t=>new Body(t.unit!,true));}
 internal static class NativeFeignSupportEffects{internal sealed class Scope{}internal static Scope BeginEndTurn(object s)=>new();internal static Exception? EndTurnFinished(Scope s,Exception? e)=>e;}
 // Inactive dependencies of real RecruitmentHooks, unrelated to ordinary actions.
 internal static class RecruitmentWheelUi{internal static void Clear(){}internal static void Install(HarmonyLib.Harmony h){}}
 internal static class MimicPresentationState{internal static void Clear(){}}
 internal static class MimicPresentationUi{internal static void Clear(){}}
 internal static class MimicControls{internal static void Clear(){}}
 internal static class NativeRecruitmentRosterReentry{internal static void Validate(string j,IReadOnlyDictionary<string,NativeAppearanceRoster>? a){}}
 internal static class NativeAquarionUnits{internal static string MimicMenu(object p)=>RecruitmentCatalog.MenuId;}
 internal static class NativeExecutionProbe{internal static object? PaidUnlockState(string s,object p)=>null;internal static bool AllowPaidEligibility(object s,object c)=>false;}
 internal static class NativeFreshSession{internal static bool BlockMimicRecruitment(object s)=>false;}
 internal static class NativeExecutionTrigger{internal static bool Requested=>false;}
 internal static class NativeTrainLeafProbeTrigger{internal static bool Requested=>false;}
 internal static class NativeMimicTransport{internal static bool CanHandleMovement(object c,object s,object tile)=>true;}
}
