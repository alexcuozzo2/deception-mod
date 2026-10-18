using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using L=Il2CppSystem.Collections.Generic;
public enum UnitType{warrior,polytar,knight,dcp_spy,dcp_spymaid,transportship,dcp_mimic_form_warrior,mooni,mindbender,cloak}
public enum UnitEffect { Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8 }
public enum ImprovementEffect{None,Infiltrated}
public static class EnumCache<T>where T:struct,Enum{public static string GetName(T x)=>x.ToString();public static T GetType(string x)=>Enum.Parse<T>(x,true);}
namespace Polytopia.Data
{
 public sealed class UnitData:Il2CppSystem.Object{public UnitType type{get;set;}public int Sight{get;set;}=2;public int attack{get;set;}=20;public int defence{get;set;}=20;public int range{get;set;}=1;}
 public sealed class UnitAbility{public enum Type{None,Sneak,Convert,Persist,Dash,Fortify=22,Infiltrate=35}}
 public sealed class ImprovementData{public enum Type{None,City,Farm}}
}
public sealed class ImprovementState:Il2CppSystem.Object{public bool Wall,Infiltrated;public bool HasEffect(ImprovementEffect e)=>Infiltrated;}
public sealed class UnitState:Il2CppSystem.Object
{
 public uint id{get;set;}public byte owner{get;set;}=1;public UnitType type{get;set;}public ushort health{get;set;}=100;
 public UnitState? passengerUnit{get;set;}public bool moved{get;set;}public bool attacked{get;set;}public bool Linked{get;set;}public WorldCoordinates coordinates{get;set;}public WorldCoordinates home{get;set;}public Polytopia.Data.UnitData UnitData{get;set;}=new();
 public L.List<UnitEffect> effects{get;set;}=new();public System.Collections.Generic.HashSet<Polytopia.Data.UnitAbility.Type> Abilities=new();
 public ushort kills{get;set;}
 public bool CanAttack()=>health>0&&!attacked&&!HasEffect(UnitEffect.Frozen)&&(!moved||Abilities.Contains(Polytopia.Data.UnitAbility.Type.Dash))&&UnitData.attack>0&&!SpyCatalog.IsSpyId(type.ToString())&&!RecruitmentCatalog.IsMimicId(type.ToString());
 public bool HasActivePeaceTreaty(GameState state,PlayerState player){bool r=owner==player.Id||player.Treaty;if(Harness.Patched&&!NativeRogueRules.CombatRelation(this,state,player,ref r))return r;return r;}
 public int NativeFullCost=20;public int GetFullCost(GameState s){int result=0;return NativeInvestment.DisbandCost(this,s,ref result)?NativeFullCost:result;}
 public bool HasLeader()=>Linked;public bool HasFollower()=>false;public bool HasEffect(UnitEffect e)=>effects.Contains(e);
 public void AddEffect(UnitEffect e){if(!effects.Contains(e))effects.Add(e);NativeFeignSupportEffects.Added(this,e);}public void RemoveEffect(UnitEffect e){effects.Remove(e);NativeFeignSupportEffects.Removed(this,e);}
 public void MakeExhauseted(GameState state){moved=attacked=true;RemoveEffect(UnitEffect.Boosted);RemoveEffect(UnitEffect.DoubleReady);}
}
public sealed class TileData:Il2CppSystem.Object
{
 public WorldCoordinates coordinates{get;set;}public UnitState? unit{get;set;}public byte owner{get;set;}public int terrain{get;set;}public ImprovementState? improvement{get;set;}
 public L.List<byte> explorers{get;set;}=new();public UnitState? GetUnit(GameState state,byte actor,bool force)=>unit is not null&&GetExplored(actor)&&!unit.HasEffect(UnitEffect.Invisible)?unit:null;
 public bool GetExplored(byte player)=>explorers.Contains(player);public bool IsBeingCaptured(GameState state)=>unit is not null&&unit.owner!=owner&&owner!=0;
 public bool HasImprovement(Polytopia.Data.ImprovementData.Type type){bool value=improvement is not null&&type==Polytopia.Data.ImprovementData.Type.City;NativeRogueAccounting.CityFortification(this,type,ref value);return value;}
}
public sealed class MapData:Il2CppSystem.Object{public NativeArray<TileData> Tiles{get;set;}=new();}
public sealed class PlayerState:Il2CppSystem.Object{public byte Id{get;set;}public int Currency{get;set;}=37;public bool Treaty{get;set;}public bool TerrainDefence{get;set;}public int KillCount;uint Casualties;public uint casualities{get=>Casualties;set{if(Harness.FailCasualtyOwner==Id){Harness.FailCasualtyOwner=0;throw new Exception("casualty setter failed");}Casualties=value;}}}
public sealed class GameState:Il2CppSystem.Object
{
 public enum State{Started,Ended}public State CurrentState{get;set;}=State.Started;public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=4;
 public MapData Map{get;set;}=new();public L.List<PlayerState> PlayerStates{get;set;}=new();public L.List<ActionBase> ActionStack{get;set;}=new();public bool HasFlaggedNeedForUpdateRoutes{get;set;}
 public void Drain(){int n=0;while(ActionStack.Count>0){if(++n>32)throw new Exception("queue loop");var a=ActionStack[ActionStack.Count-1];a.Execute(this);ActionStack.Remove(a);}}
 public bool TryGetPlayer(byte id,out PlayerState? player){player=Native.Items(PlayerStates).Cast<PlayerState>().SingleOrDefault(p=>p.Id==id);return player is not null;}
 public bool TryGetUnit(uint id,out UnitState? u){u=Map.Tiles.Values.Select(t=>t.unit).SingleOrDefault(x=>x?.id==id);return u is not null;}
}
public static class MapDataExtensions
{
 public static TileData? GetTile(MapData map,WorldCoordinates at)=>map.Tiles.Values.SingleOrDefault(t=>t.coordinates.X==at.X&&t.coordinates.Y==at.Y);
 public static NativeArray<TileData> GetAreaSorted(MapData map,WorldCoordinates at,int radius,bool diagonal,bool include)=>GetArea(map,at,radius,diagonal,include);
 public static NativeArray<TileData> GetArea(MapData map,WorldCoordinates at,int radius,bool diagonal,bool include)=>new(){Values=map.Tiles.Values.Where(t=>Math.Max(Math.Abs(t.coordinates.X-at.X),Math.Abs(t.coordinates.Y-at.Y))<=radius&&(include||t.coordinates.X!=at.X||t.coordinates.Y!=at.Y)).ToArray()};
 // Exact installed semantics: roots counted by home, independent of owner.
 public static int GetCityUnitCount(MapData map,WorldCoordinates at)=>map.Tiles.Values.Count(t=>t.unit is UnitState u&&u.home.X==at.X&&u.home.Y==at.Y);
}
public static class PlayerExtensions
{
 public static int CountUnits(PlayerState player,GameState state)=>state.Map.Tiles.Values.Count(t=>t.unit?.owner==player.Id);
 public static int CountUnits(PlayerState player,GameState state,out L.List<UnitState> units){units=new();foreach(var t in state.Map.Tiles.Values)if(t.unit?.owner==player.Id)units.Add(t.unit);return units.Count;}
 public static void ModifyAggressionsFromAttack(PlayerState a,PlayerState b,TileData tile,GameState state){}
}
public static class PlayerDiplomacyExtensions{public static void ModifyDiplomacyRelationsFromAttack(PlayerState p,byte opponent,GameState s){if(NativeRogueRules.Diplomacy())Harness.DiplomacyCalls++;}public static bool HasPeaceWith(PlayerState p,byte opponent)=>p.Treaty;}
public static class UnitDataExtensions
{
 public static bool HasAbility(UnitState unit,Polytopia.Data.UnitAbility.Type ability)=>unit.Abilities.Contains(ability);
 public static int GetSightRange(Polytopia.Data.UnitData data)=>data.Sight;
 public static bool CanCapture(UnitState u,GameState s,TileData t,bool a,bool b)=>true;public static bool CanExamineRuins(UnitState u,GameState s,TileData t)=>true;public static bool CanDisband(UnitState u,GameState s,PlayerState p)=>true;
 public static L.List<WorldCoordinates> GetAttackOptionsAtPosition(GameState s,byte p,WorldCoordinates c,int range,bool a,UnitState u,bool b)
 {var list=new L.List<WorldCoordinates>();foreach(var t in GetTiles(s,c,range))if(t.GetUnit(s,p,false) is UnitState v&&v.owner!=p&&!v.HasActivePeaceTreaty(s,Player(s,p)))list.Add(t.coordinates);object r=list;NativeRogueRules.AttackOptions(s,p,c,range,u,ref r);return (L.List<WorldCoordinates>)r;}
 static TileData[] GetTiles(GameState s,WorldCoordinates c,int range)=>MapDataExtensions.GetArea(s.Map,c,range,true,false).Values;
 static PlayerState Player(GameState s,byte p){s.TryGetPlayer(p,out var result);return result!;}
 // Audited native control flow. This port deliberately keeps city, terrain
 // and poison separate so the actual detour cannot pass by zeroing defence.
 public static int GetDefenceBonus(UnitState unit,GameState state)
 {
  var t=MapDataExtensions.GetTile(state.Map,unit.coordinates);int result=10;
  if(t is not null){if(state.TryGetPlayer(unit.owner,out var p)&&p!.TerrainDefence&&t.terrain!=0)result=15;
   if(t.HasImprovement(Polytopia.Data.ImprovementData.Type.City)&&t.owner==unit.owner&&HasAbility(unit,Polytopia.Data.UnitAbility.Type.Fortify))result=t.improvement!.Wall?40:15;}
  if(unit.HasEffect(UnitEffect.Poisoned))result>>=1;return result;
 }
}
public static class TileDataExtensions{public static int CalculateWork(TileData t,GameState s,PlayerState p,int x)=>3;}
public struct BattleResults{public int attackDamage,retaliationDamage;}
public static class BattleHelpers
 {public static BattleResults GetBattleResults(GameState s,UnitState a,UnitState b,bool peace,bool actual)
 {NativeRogueRules.Battle(s,a,b,ref peace);s.TryGetPlayer(a.owner,out var p);if(peace&&b.HasActivePeaceTreaty(s,p!))return default;int damage=a.UnitData.attack;bool retaliation=b.UnitData.attack>0&&!SpyCatalog.IsSpyId(b.type.ToString())&&!b.HasEffect(UnitEffect.Frozen)&&a.health>0&&UnitDataExtensions.GetAttackOptionsAtPosition(s,b.owner,b.coordinates,b.UnitData.range,true,b,false).Contains(a.coordinates);return new(){attackDamage=damage,retaliationDamage=damage>=b.health||!retaliation?0:b.UnitData.defence};}}
public static class Harness{public static bool Patched=true;public static byte FailCasualtyOwner;public static int DiplomacyCalls,Battles;public static Action? DuringCommand;public static bool ThrowCommand;public static void Reset(){Patched=true;FailCasualtyOwner=0;DiplomacyCalls=Battles=0;DuringCommand=null;ThrowCommand=false;}}
public static class PathFinder
{
 public static L.List<WorldCoordinates> GetPath(GameState s,WorldCoordinates from,WorldCoordinates to,int range,UnitState unit)
 {var steps=new L.List<WorldCoordinates>();int x=to.X,y=to.Y;while(x!=from.X||y!=from.Y){steps.Add(new(x,y));x+=Math.Sign(from.X-x);y+=Math.Sign(from.Y-y);}steps.Add(from);object result=steps;NativeRogueTactics.Path(s,from,unit,ref result);return (L.List<WorldCoordinates>)result;}
 public static L.List<WorldCoordinates> GetMoveOptions(GameState s,WorldCoordinates from,int range,UnitState u)=>new();
}
public class ActionBase:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public WorldCoordinates Origin{get;set;}public WorldCoordinates Target{get;set;}public virtual void Execute(GameState s){}}
public sealed class StartTurnAction:ActionBase{public override void Execute(GameState s){}}public sealed class ConvertAction:ActionBase{public override void Execute(GameState s){}}
public sealed class AttackAction:ActionBase{public int Damage;public override void Execute(GameState s){NativeRogueRules.BeforeAttack(this,s,out var scope);try{ActionUtils.PerformAttack(s,PlayerId,Origin,Target,Damage);}finally{NativeRogueRules.AttackFinished(null,scope);}}}
public sealed class KillUnitAction:ActionBase{public override void Execute(GameState s){var t=MapDataExtensions.GetTile(s.Map,Target)!;NativeRogueAccounting.BeforeDeath(s,t,out var scope);try{ActionUtils.KillUnit(s,t);}finally{NativeRogueAccounting.OwnersFinished(null,scope);}}}public sealed class InfiltrateAction:ActionBase{public override void Execute(GameState s){}}
public sealed class EndTurnCommand:ActionBase{public override void Execute(GameState s){}}public sealed class AttackCommand:ActionBase
 {public uint UnitId{get;set;}
 public bool IsValid(GameState s,ref string? error){var a=(UnitState?)NativeRogueRules.At(s,Origin);return a is not null&&a.id==UnitId&&s.CurrentPlayer==PlayerId&&a.owner==PlayerId&&a.CanAttack()&&UnitDataExtensions.GetAttackOptionsAtPosition(s,PlayerId,Origin,a.UnitData.range,false,a,false).Contains(Target);}
 public override void Execute(GameState s){NativeRogueRules.BeforeAttack(this,s,out var scope);try{Harness.DuringCommand?.Invoke();if(Harness.ThrowCommand)throw new Exception("native command error");var a=(UnitState)NativeRogueRules.At(s,Origin)!;var b=(UnitState)NativeRogueRules.At(s,Target)!;s.TryGetPlayer(PlayerId,out var p);BattleResults results=default;
 // Exact audited order: direct protection check precedes BattleHelpers.
 if(!b.HasActivePeaceTreaty(s,p!)){Harness.Battles++;results=BattleHelpers.GetBattleResults(s,a,b,true,false);PlayerDiplomacyExtensions.ModifyDiplomacyRelationsFromAttack(p!,b.owner,s);}
 a.moved=true;a.attacked=!(a.Abilities.Contains(Polytopia.Data.UnitAbility.Type.Persist)&&results.attackDamage>=b.health);if(results.retaliationDamage>0)s.ActionStack.Add(new AttackAction{PlayerId=b.owner,Origin=Target,Target=Origin,Damage=results.retaliationDamage});s.ActionStack.Add(new AttackAction{PlayerId=PlayerId,Origin=Origin,Target=Target,Damage=results.attackDamage});}finally{NativeRogueRules.AttackFinished(null,scope);}}}
public sealed class WipePlayerAction:ActionBase
{public override void Execute(GameState s){foreach(var t in s.Map.Tiles.Values)if(t.unit?.owner==PlayerId)s.ActionStack.Add(new DisbandUnitAction{PlayerId=PlayerId,Target=t.coordinates});}}
public sealed class DisbandUnitAction:ActionBase{public WorldCoordinates Coordinates{get;set;}public bool GetPaid{get;set;}public override void Execute(GameState s)=>ActionUtils.KillUnit(s,MapDataExtensions.GetTile(s.Map,Target)!);}
public sealed class DecreaseScoreAction:ActionBase{}
public sealed class IncreaseCurrencyAction:ActionBase{public WorldCoordinates Source{get;set;}public int Amount{get;set;}}
public sealed class TrainAction:ActionBase{public WorldCoordinates Coordinates{get;set;}public UnitType Type{get;set;}public int Cost{get;set;}public override void Execute(GameState s){}}
public sealed class UpgradeAction:ActionBase{public WorldCoordinates Coordinates{get;set;}public int Cost{get;set;}public override void Execute(GameState s){}}
public sealed class DisbandCommand:ActionBase{public WorldCoordinates Coordinates{get;set;}public override void Execute(GameState s){}}
public static class ActionUtils
 {
  public static void PerformAttack(GameState s,byte actor,WorldCoordinates from,WorldCoordinates target,int damage)
  {NativeRogueAccounting.BeforeDamage(s,target,damage,out var scope);var a=(UnitState?)NativeRogueRules.At(s,from);var b=(UnitState?)NativeRogueRules.At(s,target);if(b is null)return;b.health=(ushort)Math.Max(0,b.health-damage);if(b.health==0){if(a is not null){a.kills++;s.TryGetPlayer(a.owner,out var p);p!.KillCount++;}s.TryGetPlayer(b.owner,out var v);v!.casualities++;s.ActionStack.Add(new KillUnitAction{PlayerId=actor,Target=target});}NativeRogueAccounting.AfterDamage(scope,true);}
  public static void KillUnit(GameState state,TileData tile){var u=tile.unit;if(u is null)return;state.ActionStack.Add(new DecreaseScoreAction{PlayerId=u.owner});tile.unit=null;}
 }

public static class Fault{public static string At="";public static void Check(string at){if(At==at){At="";throw new System.IO.IOException("Injected "+at);}}}
namespace Deception.PolyModAdapter
{
 internal static class NativeBlackout{internal static bool Covered(object state,byte player,NativeSpyNetwork.Position at){var s=NativeGameStateEnvelope.Get(state);return s is not null&&(NativeIdentityLedgerCodec.Decode(s.Bytes(),s.Revision).Blackouts??Array.Empty<LedgerBlackout>()).Any(x=>x.Player==player&&x.X==at.X&&x.Y==at.Y);}}
 // Stateless appearance value helper boundary; the event collection, finalizer
 // ordering, ledger update and cancellation are the real production adapter.
 internal static class NativeFeignSupport
 {
  internal static bool Has(LedgerAppearance a,string effect)=>a.Effects.Contains(effect);
  internal static LedgerAppearance Effect(LedgerAppearance a,string effect,bool add)=>a with{Effects=(add?a.Effects.Append(effect):a.Effects.Where(x=>x!=effect)).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray()};
  internal static bool Recipient(object state,object unit,LedgerUnit? row,byte actor)=>row?.Rogue is null&&Native.Int(Native.Get(unit,"owner"))==actor;
  internal static bool Ready(object unit)=>!(bool)Native.Get(unit,"attacked")!;
 }
}
