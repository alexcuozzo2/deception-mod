// Native behavior ports are deliberately independent of the production route
// checker. Production movement, water policy, ledger codec and Reveal compile.
using System;using System.Collections.Generic;using System.Linq;using System.Reflection;using System.Runtime.InteropServices;
using Deception.Core;using Deception.PolyModAdapter;
namespace HarmonyLib{
 [AttributeUsage(AttributeTargets.Method)]public sealed class HarmonyPriority:Attribute{public int priority;public HarmonyPriority(int p){priority=p;}}
 public static class Priority{public const int High=600;}
 public sealed class HarmonyMethod{public MethodInfo Method;public HarmonyMethod(MethodInfo m){Method=m;}}
 public sealed class Harmony{public List<MethodInfo> Hooks=new();public void Patch(MethodInfo m,HarmonyMethod? p,HarmonyMethod? q,object? t,HarmonyMethod? f,object? il)=>Hooks.Add(m);}}
namespace Il2CppSystem{
 public class Object{static long Next;public IntPtr Pointer{get;private set;}=new(++Next);public new Object MemberwiseClone(){var c=(Object)base.MemberwiseClone();c.Pointer=new(++Next);return c;}}
 public class ValueType:Object{}
}
namespace Il2CppSystem.Collections.Generic{public class List<T>:System.Collections.Generic.List<T>{}}
public enum UnitEffect{Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8}
public static class EnumCache<T>where T:struct,Enum{public static string GetName(T x)=>x.ToString();public static T GetType(string x)=>Enum.Parse<T>(x,true);}
[StructLayout(LayoutKind.Sequential)]public readonly record struct WorldCoordinates(int X,int Y);
namespace Polytopia.Data{
 public class UnitData:Il2CppSystem.Object{public enum Type{warrior,rider,knight,firedragon,island,shark,transportship,cloak,cloak_boat,dcp_mimic_form_warrior}public Type type{get;set;}public int Movement=1;public bool Fly,Swim,Vehicle,Wetland;public Il2CppSystem.Collections.Generic.List<UnitAbility.Type> unitAbilities{get;set;}=new();}
 public class UnitAbility{public enum Type{Swim=10,Fly=13,AutoFreeze=20,AutoFlood=45,Algae=51}}
 public class TerrainData:Il2CppSystem.Object{public enum Type{Field=3,Water=1,Ocean=2,Mountain=4,Wetland=7}public Type type{get;set;}}
 public class ImprovementData:Il2CppSystem.Object{public enum Type{City=1,Port=8,Bridge=48}public Type type{get;set;}}
 public class ImprovementAbility{public enum Type{Bridge=13}}
 public class GameLogicData:Il2CppSystem.Object{
  public Dictionary<string,UnitData> Forms=new();public Il2CppSystem.Collections.Generic.List<TerrainData> GetUnlockedMovements(PlayerState p){var l=new Il2CppSystem.Collections.Generic.List<TerrainData>{new(){type=TerrainData.Type.Field}};if(p.Water)l.Add(new(){type=TerrainData.Type.Water});if(p.Ocean)l.Add(new(){type=TerrainData.Type.Ocean});return l;}
  public bool TryGetData(ImprovementData.Type t,out ImprovementData d){d=new(){type=t};return true;}}
}
public class UnitState:Il2CppSystem.Object{
 public uint id{get;set;}=1;public byte owner{get;set;}=1;public int health{get;set;}=100;public ushort promotionLevel{get;set;}
 public Polytopia.Data.UnitData.Type type{get;set;}public Polytopia.Data.UnitData UnitData{get;set;}=new();public WorldCoordinates coordinates{get;set;}
 public bool moved{get;set;}=true;public bool attacked{get;set;}public Il2CppSystem.Collections.Generic.List<UnitEffect> effects{get;set;}=new();public UnitState? passengerUnit{get;set;}}
public class PlayerState:Il2CppSystem.Object{public byte Id{get;set;}=1;public bool Water=true,Ocean=true;}
public class ImprovementState:Il2CppSystem.Object{public Polytopia.Data.ImprovementData.Type type{get;set;}}
public class TileData:Il2CppSystem.Object{
 public WorldCoordinates coordinates{get;set;}public UnitState? unit{get;set;}public Polytopia.Data.TerrainData.Type terrain{get;set;}=Polytopia.Data.TerrainData.Type.Field;
 public ImprovementState? improvement{get;set;}public int Cost=10;public bool Blocked,Finish=true,Flooded,City,Explored=true;
 public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>t==Polytopia.Data.ImprovementData.Type.City?City:improvement?.type==t;public bool IsWetland()=>Flooded||terrain==Polytopia.Data.TerrainData.Type.Wetland;
 public int GetMovementCost(MapData map,TileData from,PathFinderSettings settings){PathFinder.CostCalls++;if(PathFinder.ThrowCost)throw new Exception("cost failure");PathFinder.CheckedEdges.Add((from.coordinates,coordinates));return Cost;}}
public class MapData:Il2CppSystem.Object{public Dictionary<WorldCoordinates,TileData> Tiles=new();}
public class GameState:Il2CppSystem.Object{
 public MapData Map{get;set;}=new();public Polytopia.Data.GameLogicData GameLogicData{get;set;}=new();public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=4;
 public PlayerState Player=new();public bool Ready=true;public Dictionary<uint,UnitState> Units=new();public Il2CppSystem.Collections.Generic.List<ActionBase> ActionStack{get;set;}=new();
 public bool TryGetUnit(uint id,out UnitState? u)=>Units.TryGetValue(id,out u);public bool TryGetPlayer(byte id,out PlayerState? p){p=id==Player.Id?Player:null;return p is not null;}}
public static class MapDataExtensions{public static TileData? GetTile(MapData m,WorldCoordinates c)=>m.Tiles.GetValueOrDefault(c);}
public static class ImprovementDataExtensions{public static bool HasAbility(Polytopia.Data.ImprovementData d,Polytopia.Data.ImprovementAbility.Type a)=>d.type==Polytopia.Data.ImprovementData.Type.Bridge;}
public static class UnitDataExtensions{
 public static bool HasAbility(UnitState u,Polytopia.Data.UnitAbility.Type a)=>a==Polytopia.Data.UnitAbility.Type.Fly?u.UnitData.Fly:u.UnitData.Swim;
 public static int GetMovement(UnitState u,GameState s){int n=u.UnitData.Movement+(u.effects.Contains(UnitEffect.Boosted)?1:0)+(u.effects.Contains(UnitEffect.Swift)||u.effects.Contains(UnitEffect.Bubble)?1:0);return n>0&&u.effects.Contains(UnitEffect.Poisoned)?Math.Max(1,n-1):n;}
 public static Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> GetAllowedTerrain(UnitState u,GameState s){var x=s.GameLogicData.GetUnlockedMovements(s.Player);if(u.UnitData.Fly||u.UnitData.Swim)x.Add(new(){type=Polytopia.Data.TerrainData.Type.Water});if(u.UnitData.Fly)x.Add(new(){type=Polytopia.Data.TerrainData.Type.Ocean});if(u.UnitData.Wetland)x.Add(new(){type=Polytopia.Data.TerrainData.Type.Wetland});return x;}}
public class PathFinderSettings:Il2CppSystem.ValueType{
 public UnitState unit{get;set;}=null!;public GameState gameState{get;set;}=null!;
 public static PathFinderSettings CreateForUnit(UnitState u,GameState s){PathFinder.Profiles.Add(u);return new(){unit=u,gameState=s};}}
public static class PathFinder{
 public static List<UnitState> Profiles=new();public static List<(WorldCoordinates,WorldCoordinates)> CheckedEdges=new();public static int CostCalls;public static bool ThrowCost;
 public static bool IsTileAccessible(TileData dest,TileData from,PathFinderSettings settings){var d=settings.unit.UnitData;if(dest.Blocked||!dest.Explored)return false;
  if(dest.improvement?.type==Polytopia.Data.ImprovementData.Type.Bridge||dest.City||dest.IsWetland()&&d.Wetland)return true;
  bool water=dest.terrain is Polytopia.Data.TerrainData.Type.Water or Polytopia.Data.TerrainData.Type.Ocean;
  if(d.Fly)return true;if(d.Swim&&!d.Vehicle)return water;
  if(d.Vehicle)return water||ActionUtils.WillUnitEmbark(settings.unit,dest,settings.gameState)==ActionUtils.EmbarkStatus.Disembark;
  return !water||ActionUtils.WillUnitEmbark(settings.unit,dest,settings.gameState)==ActionUtils.EmbarkStatus.Embark;}
 public static bool IsAllowedToFinishOnTile(PathFinderSettings s,TileData t)=>t.Finish&&t.unit is null;
 public static Il2CppSystem.Collections.Generic.List<WorldCoordinates> GetPath(GameState s,WorldCoordinates f,WorldCoordinates t,int n,UnitState u)=>new();
 public static Il2CppSystem.Collections.Generic.List<WorldCoordinates> GetMoveOptions(GameState s,WorldCoordinates f,int n,UnitState u)=>new();}
public static class ActionUtils{
 public enum EmbarkStatus{None=0,Embark=1,Disembark=2}
 public static EmbarkStatus WillUnitEmbark(UnitState u,TileData t,GameState s){bool water=t.terrain is Polytopia.Data.TerrainData.Type.Water or Polytopia.Data.TerrainData.Type.Ocean||t.IsWetland();
  if(u.UnitData.Vehicle&&(!water||t.City))return EmbarkStatus.Disembark;
  if(!u.UnitData.Fly&&!u.UnitData.Swim&&!u.UnitData.Vehicle&&water&&t.improvement?.type==Polytopia.Data.ImprovementData.Type.Port&&s.Player.Water)return EmbarkStatus.Embark;return EmbarkStatus.None;}
 public static void PerformAttack(GameState s,byte p,WorldCoordinates a,WorldCoordinates b,int d){}}
public class ActionBase:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public bool Valid=true;public int GetActionType()=>0;public bool IsValid(GameState s)=>Valid;}
public class MoveAction:ActionBase{
 public enum MoveReason{Command=0,Attack=1,Push=2}
 public uint UnitId{get;set;}=1;public MoveReason Reason{get;set;}public Il2CppSystem.Collections.Generic.List<WorldCoordinates> Path{get;set;}=new();public bool Skip,NoMove,Throw;public Action<GameState,UnitState>? During;public int Passives;public bool Pending,NativeBorrowedFlood;
 public void Execute(GameState s){NativeDisguiseMovement.BeforeMove(this,s,out var scope);NativeApparentTerrain.BeforeMove(this,s,out var terrain);Exception? error=null;try{if(!Skip){var u=s.Units[UnitId];Pending=NativeDisguiseMovement.IsShedding(s,u);if(!NoMove){s.Map.Tiles[u.coordinates].unit=null;u.coordinates=Path[0];s.Map.Tiles[u.coordinates].unit=u;}During?.Invoke(s,u);bool flood=false;NativeApparentTerrain.Ability(u,Polytopia.Data.UnitAbility.Type.AutoFlood,ref flood);NativeBorrowedFlood=flood;if(!NativeDisguiseMovement.IsShedding(s,u))Passives++;if(Throw)throw new Exception("native move failed");}NativeDisguiseMovement.AfterMove(!Skip,scope);}catch(Exception e){error=e;}finally{error=NativeApparentTerrain.MoveFinished(error,terrain);error=NativeDisguiseMovement.Finished(error,scope);}if(error is not null)throw error;}}
public class MoveCommand:ActionBase{public uint UnitId{get;set;}public WorldCoordinates From{get;set;}public WorldCoordinates To{get;set;}public bool IsValid(GameState s,ref string e)=>Valid;}
// Compile genuine Reveal unchanged; unrelated lifecycle entrypoints are unused.
public sealed class HealAction:ActionBase{public WorldCoordinates Coordinates{get;set;}public ushort HealAmount{get;set;}public ushort FixedHealAmount{get;set;}}
namespace Deception.PolyModAdapter{
 internal static class RecruitmentHooks{internal const string GLD="Polytopia.Data.GameLogicData";internal static bool Registered=true;internal static object Data(object s,string f)=>((GameState)s).GameLogicData.Forms[f];}
 internal static class NativeRegroupSession{internal static bool Enabled=true;internal static int Faults,Reads;internal static bool Ready(object s)=>((GameState)s).Ready;internal static void Prepare(object s){}internal static void Fault(string s){Faults++;}internal static LedgerUnit? ReadIdentity(object s,uint id){Reads++;var snap=NativeGameStateEnvelope.Get(s);return snap is null?null:NativeIdentityLedgerCodec.Decode(snap.Bytes(),snap.Revision).Units.SingleOrDefault(r=>r.Alive&&r.NativeId==id);}}
 internal static class NativeGameStateEnvelope{internal sealed record Snapshot(long Revision,byte[] Payload){internal byte[] Bytes()=>(byte[])Payload.Clone();}internal static Dictionary<GameState,Snapshot> States=new();internal static Snapshot? Get(object s)=>States.GetValueOrDefault((GameState)s);internal static void Set(object s,long r,byte[] bytes)=>States[(GameState)s]=new(r,(byte[])bytes.Clone());}
 internal static class NativeLedgerState{internal static bool Fail;internal static void Validate(object s,long r,byte[] p){if(Fail)throw new System.IO.InvalidDataException("injected validation failure");NativeIdentityLedgerCodec.Validate(NativeIdentityLedgerCodec.Decode(p,r),r);}}
 internal static class NativeFeignSupport{internal static object Tile(object s,object u)=>((GameState)s).Map.Tiles[((UnitState)u).coordinates];internal static bool Has(LedgerAppearance a,string e)=>a.Effects.Contains(e);internal static int RecoveryAmount(object s,object t,object u,LedgerAppearance a)=>40;internal static LedgerAppearance Heal(LedgerAppearance a,int amount)=>a;}
}
