// Synthetic boundaries with production NativeApi and preview callbacks unchanged.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Deception.Core;
using Deception.PolyModAdapter;
namespace HarmonyLib{public sealed class HarmonyMethod{public MethodInfo Method;public HarmonyMethod(MethodInfo m){Method=m;}}public sealed class Harmony{public List<MethodInfo> Patched=new();public void Patch(MethodInfo m,HarmonyMethod? p,HarmonyMethod? q,object? t,HarmonyMethod? f,object? il){Patched.Add(m);}}}
namespace Il2CppSystem{public class Object{static long Next;public IntPtr Pointer{get;private set;}=new(++Next);public static bool Alias;public static int Clones;public new Object MemberwiseClone(){Clones++;if(Alias)return this;var c=(Object)base.MemberwiseClone();c.Pointer=new(++Next);return c;}}}
namespace Il2CppSystem.Collections.Generic{public class List<T>:System.Collections.Generic.List<T>{}}
namespace UnityEngine{public class Object:Il2CppSystem.Object{public bool Alive=true;public static implicit operator bool(Object? o)=>o is not null&&o.Alive;}}
public static class EnumCache<T>where T:struct,Enum{public static string GetName(T t)=>t.ToString();public static T GetType(string s)=>Enum.Parse<T>(s,true);}
public enum UnitType{warrior,defender,archer,catapult,firedragon,transportship,dcp_mimic,dcp_mimic_form_warrior}
public enum OverlayType{Walk=0,Disembark=1,Attack=2,Kill=3}
public enum UnitEffect{Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8}
[StructLayout(LayoutKind.Sequential)]public struct WorldCoordinates{public int X{get;set;}public int Y{get;set;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
[StructLayout(LayoutKind.Sequential)]public struct BattleResults{public int attackDamage,retaliationDamage;public bool shouldMoveToDefeatedEnemyTile,shouldResetPacifistTask;}
namespace Polytopia.Data{public sealed class UnitData:Il2CppSystem.Object{public UnitType type{get;set;}public ushort health{get;set;}=100;public int Attack=20,Defence=20,Range=1;public bool NoRetaliate;}}
public sealed class UnitState:Il2CppSystem.Object{public uint id{get;set;}public byte owner{get;set;}public UnitType type{get;set;}public Polytopia.Data.UnitData UnitData{get;set;}=new();public ushort health{get;set;}=100;public ushort promotionLevel{get;set;}public UnitState? passengerUnit{get;set;}public Il2CppSystem.Collections.Generic.List<UnitEffect> effects{get;set;}=new();public WorldCoordinates coordinates{get;set;}public bool moved{get;set;}public bool attacked{get;set;}}
public sealed class MapData:Il2CppSystem.Object{}
public sealed class GameState:Il2CppSystem.Object{public MapData Map{get;set;}=new();public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=3;public bool Ready=true;public Guid Epoch=Guid.NewGuid();public Dictionary<uint,UnitState> Units=new();public Dictionary<uint,LedgerUnit> Rows=new();public Dictionary<string,Polytopia.Data.UnitData> Data=new();public bool TryGetUnit(uint id,out UnitState? unit)=>Units.TryGetValue(id,out unit);}
public sealed class PlayerState:Il2CppSystem.Object{public byte Id{get;set;}=1;}
public sealed class ActionManager:Il2CppSystem.Object{public bool IsProcessing{get;set;}}
public sealed class ClientBase:Il2CppSystem.Object{public GameState GameState{get;set;}=null!;public ActionManager ActionManager{get;set;}=new();public bool IsReplay{get;set;}public bool IsSpectating{get;set;}public bool IsWaitingForCommand{get;set;}}
public static class GameManager{public static GameState? GameState{get;set;}public static ClientBase? Client{get;set;}public static PlayerState? LocalPlayer{get;set;}public static bool Viewing=true;public static bool IsPlayerViewing(byte p)=>Viewing;}
public sealed class MapRenderer:UnityEngine.Object{public static MapRenderer? Current{get;set;}public Dictionary<uint,Unit> Units=new();public Unit? GetUnitInstance(uint id)=>Units.GetValueOrDefault(id);}
public sealed class Unit:UnityEngine.Object
{
 public UnitState UnitState{get;set;}=null!;public Il2CppSystem.Collections.Generic.List<Unit> overlayUnits{get;set;}=new();public bool Visible=true,Invisible,Death,ActualFocus;public int Shows;public List<bool> DeathAttempts=new();public bool GetVisible()=>Visible;public bool IsInvisibleForLocalPlayer()=>Invisible;
 public ushort Health{get{ushort health=UnitState.health;if(BattleHelpers.Enable)NativeCombatPreview.Health(this,ref health);return health;}}
 public void ShowDeathIndicator(){bool allow=!BattleHelpers.Enable||NativeCombatPreview.Death(this);DeathAttempts.Add(allow);if(allow){Death=true;Shows++;}}
 public void HideDeathIndicator(){Death=false;}
 public void ClearUnitOverlays(){foreach(var u in overlayUnits)u.HideDeathIndicator();overlayUnits.Clear();}
 public bool ThrowOverlay;public Action? DuringOverlay;
 public void UpdateOverlays()
 {
  NativeCombatPreview.Scope? scope=null;bool run=!BattleHelpers.Enable||NativeCombatPreview.BeforeOverlays(this,out scope);Exception? error=null;
  try{if(!run)return;ClearUnitOverlays();DuringOverlay?.Invoke();if(ThrowOverlay)throw new InvalidOperationException("native overlays failed");
   foreach(var target in MapRenderer.Current!.Units.Values.Where(u=>u.UnitState.owner!=UnitState.owner&&u.Visible&&!u.Invisible))
   {var r=BattleHelpers.GetBattleResults(GameManager.GameState!,UnitState,target.UnitState,true,true);LastRetaliation=r.retaliationDamage;LastOwnLethal=r.retaliationDamage>=UnitState.health;target.Tile.Unit=target;target.Tile.ShowOverlay(LastOwnLethal?OverlayType.Kill:OverlayType.Attack);
    // The actual native method reads this raw HP field, not Unit.Health.
    if(r.attackDamage>=target.UnitState.health){target.ShowDeathIndicator();overlayUnits.Add(target);}}
   NativeCombatPreview.AfterOverlays(true,scope);
  }catch(Exception e){error=e;throw;}finally{NativeCombatPreview.Finished(error,scope);}
 }
 public Tile Tile=new();public int LastRetaliation;public bool LastOwnLethal;
}
public sealed class Tile:UnityEngine.Object{public Unit? Unit{get;set;}public List<OverlayType> Writes=new();public Action? BeforeWrite;public void ShowOverlay(OverlayType type){if(!BattleHelpers.Enable||NativeCombatPreview.TileOverlay(this,ref type)){BeforeWrite?.Invoke();Writes.Add(type);}}}
public sealed class DamageIndicator:UnityEngine.Object{public bool Shown;public List<(float Damage,float Health)> Writes=new();public void Show(Unit unit,float damage,float health,float delay){if(!BattleHelpers.Enable||NativeCombatPreview.Indicator(unit,ref damage,ref health)){Shown=true;Writes.Add((damage,health));}}}
public sealed class ClientInteraction:Il2CppSystem.Object
{
 public Unit? selectedUnit{get;set;}public DamageIndicator Indicator=new(),OwnIndicator=new();public BattleResults Last;public ushort TargetHealth,ActorHealth;public bool ThrowHover;public Action? DuringHover;
 public void StartHoverUnit(Unit target)
 {
  NativeCombatPreview.Scope? scope=null;bool run=!BattleHelpers.Enable||NativeCombatPreview.BeforeHover(this,target,out scope);Exception? error=null;
  try{if(!run)return;DuringHover?.Invoke();if(ThrowHover)throw new InvalidOperationException("native hover failed");Last=BattleHelpers.GetBattleResults(GameManager.GameState!,selectedUnit!.UnitState,target.UnitState,false,true);TargetHealth=target.Health;ActorHealth=selectedUnit.Health;Indicator.Show(target,Last.attackDamage,TargetHealth,0);OwnIndicator.Show(selectedUnit,Last.retaliationDamage,ActorHealth,0.5f);}
  catch(Exception e){error=e;throw;}finally{NativeCombatPreview.Finished(error,scope);}
 }
}
public static class UnitDataExtensions
{
 public static int GetMaxHealth(UnitState u,GameState s){int n=0;if(!NativeCombatPreview.Maximum(u,s,ref n))return n;return u.passengerUnit is null?u.UnitData.health+(u.promotionLevel>0?50:0):GetMaxHealth(u.passengerUnit,s);}
}
public static class BattleHelpers
{
 public static UnitState? SeenAttacker,SeenDefender;public static bool Throw;public static Action? During;public static bool Enable=true;public static int Calls;public static bool FogRetaliation=true;
 public static BattleResults GetBattleResults(GameState s,UnitState attacker,UnitState defender,bool peace,bool simple)
 {
  Calls++;NativeCombatPreview.Calculation? scope=null;Exception? error=null;
  try{BattleResults pre=default;bool run=!Enable||NativeCombatPreview.BeforeBattle(s,ref attacker,ref defender,simple,ref pre,out scope);if(!run)return pre;SeenAttacker=attacker;SeenDefender=defender;During?.Invoke();if(Throw)throw new InvalidOperationException("native calculation failed");
   // Deterministic combat-boundary stand-in: form, false HP/max, effects,
   // veteran/cargo and native retaliation range all influence the result.
   int max=UnitDataExtensions.GetMaxHealth(defender,s);int defence=defender.UnitData.Defence*(defender.effects.Contains(UnitEffect.Poisoned)?1:2);
   int attackerMax=UnitDataExtensions.GetMaxHealth(attacker,s);int hit=attacker.UnitData.Attack==0?0:attacker.UnitData.Attack*attacker.health/(attackerMax==0?1:attackerMax)+100-defence;
   int distance=Math.Abs(attacker.coordinates.X-defender.coordinates.X)+Math.Abs(attacker.coordinates.Y-defender.coordinates.Y);
   int retaliation=FogRetaliation&&!defender.UnitData.NoRetaliate&&!defender.type.ToString().StartsWith("dcp_mimic")&&distance<=defender.UnitData.Range&&hit<defender.health?
    defender.UnitData.Attack*defender.health/max*(defender.effects.Contains(UnitEffect.Boosted)?2:1):0;
   var result=new BattleResults{attackDamage=hit,retaliationDamage=retaliation};if(Enable)NativeCombatPreview.AfterBattle(result,scope);return result;
  }catch(Exception e){error=e;throw;}finally{if(Enable)NativeCombatPreview.BattleFinished(error,scope);}
 }
}
namespace Deception.PolyModAdapter
{
 internal static class RecruitmentHooks{internal static object Data(object s,string form)=>((GameState)s).Data[form];}
 internal static class Entry{internal static int Errors;internal static void Note(string kind,string detail){Errors++;}}
 internal static partial class NativeGameStateEnvelope
 {
  static readonly Dictionary<GameState,Snapshot> Cache=new();
  internal static Snapshot Get(object state){var s=(GameState)state;var bytes=NativeIdentityLedgerCodec.Encode(new(s.Epoch,3,1,s.Rows.Values.OrderBy(x=>x.LogicalId).ToArray(),Array.Empty<LedgerReceipt>()),0);if(!Cache.TryGetValue(s,out var old)||!old.Bytes().SequenceEqual(bytes))Cache[s]=old=new Snapshot(0,bytes);return old;}
 }
 internal static class NativeRegroupSession{internal static bool Ready(object s)=>((GameState)s).Ready;internal static LedgerUnit? ReadIdentity(object s,uint id)=>NativeGameStateEnvelope.Get(s).ReadIdentity(id);}
 internal sealed record MimicProjection(object State,LedgerUnit Identity);
 internal static class MimicPresentationUi{internal static bool? MapView(object unit){var u=(Unit)unit;var s=GameManager.GameState!;var row=NativeRegroupSession.ReadIdentity(s,u.UnitState.id);return u.UnitState.owner==GameManager.LocalPlayer!.Id||row?.Rogue?.Owner==GameManager.LocalPlayer!.Id?u.ActualFocus:null;}}
 internal static class MimicPresentationState{internal static bool TryProject(object body,out MimicProjection p){var s=GameManager.GameState!;var u=(UnitState)body;var row=NativeRegroupSession.ReadIdentity(s,u.id);p=new(s,row!);return row is{Alive:true,Independent:true,Appearance:not null}&&s.Units.GetValueOrDefault(u.id)==u;}}
}
