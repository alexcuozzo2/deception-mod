// Synthetic IL2CPP boundaries; no native renderer/game execution claim.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using Deception.Core;
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPriority:Attribute{public HarmonyPriority(int p){}}
    public static class Priority{public const int First=800,High=600,Last=0;}
    public sealed class HarmonyMethod{public readonly MethodInfo Method;public HarmonyMethod(MethodInfo m){Method=m;}}
    public sealed class Harmony{public List<string> Hooks=new();public void Patch(MethodInfo m,HarmonyMethod? pre,HarmonyMethod? post,object? transpiler,HarmonyMethod? final,object? il)=>Hooks.Add(m.DeclaringType!.Name+"."+m.Name);}
}
namespace Il2CppSystem{public class ValueType:Object{} public class Object{static long Next;public IntPtr Pointer{get;private set;}=new(++Next);public new Object MemberwiseClone(){var c=(Object)base.MemberwiseClone();c.Pointer=new(++Next);return c;}}}
namespace Il2CppSystem.Collections.Generic
{public class List<T>:Il2CppSystem.Object{readonly System.Collections.Generic.List<T> Values=new();public int Count=>Values.Count;public T this[int n]=>Values[n];public void Add(T item)=>Values.Add(item);public void Remove(T item)=>Values.Remove(item);public bool Contains(T item)=>Values.Contains(item);}}
namespace Polytopia.Data
{
    public sealed class ImprovementData { public enum Type {city,port,bridge} public Type type{get;set;} }
    public sealed class ImprovementAbility { public enum Type {bridge=13} }
    public sealed class UnitAbility { public enum Type {sneak=4,swim=10,fly=13} }
    public sealed class TerrainData:Il2CppSystem.Object { public enum Type {field,water,ocean,wetland} public Type type{get;set;} }
    public sealed class UnitData:Il2CppSystem.Object
    {
        public enum Type{dcp_mermimic=1081,dcp_mimic=1027,warrior=2,rider=3,giant=12,dragonegg=17,babydragon=18,firedragon=19,amphibian=20,dagger=37,cloak=38,cloak_boat=39,island=57,pirate=40,transportship=43,scoutship=44,juggernaut=45,rammership=46,bombership=47,
            dcp_mimic_form_island=1064,dcp_mimic_form_cloak=1055,dcp_mimic_form_warrior=1000,dcp_mimic_form_giant=1001,dcp_mimic_form_dragonegg=1002,dcp_mimic_form_babydragon=1046,dcp_mimic_form_firedragon=1042,dcp_mimic_form_transportship=1048,dcp_mimic_form_scoutship=1049,dcp_mimic_form_rammership=1050,dcp_mimic_form_bombership=1051,dcp_mimic_form_juggernaut=1052,dcp_mimic_form_pirate=1053,dcp_mimic_form_cloak_boat=1054}
        public UnitData? upgradesFrom{get;set;}public Type type{get;set;}public int health{get;set;}=100;public int cost{get;set;}=3;
        public string[] Terrains=Array.Empty<string>();public int movement{get;set;}public int attack{get;set;}public int defence{get;set;}public int range{get;set;}
        public Il2CppSystem.Collections.Generic.List<global::UnitAbility> unitAbilities{get;set;}=new();
    }
}
public enum Technology{dcp_deception,dcp_camouflage,dcp_propaganda}
public enum UnitAbility{Scout=3,Sneak=4,Swim=10,Carry=11,Fly=13,Stiff=38,Protect=39,Amphibious=47}
public enum UnitEffect { Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8 }
public enum TribeType{Imperius,Elyrion}
public enum SkinType{Default,DarkElf}
public enum GridDirection{North,South}
public static class EnumCache<T>where T:struct,Enum{public static string GetName(T x)=>x.ToString();public static T GetType(string x)=>Enum.Parse<T>(x);}
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
public struct WorldCoordinates
{public int X{get;set;}public int Y{get;set;}public WorldCoordinates(int x=0,int y=0){X=x;Y=y;}public override string ToString()=>X+","+Y;public static bool IsAdjacent(WorldCoordinates a,WorldCoordinates b)=>Math.Max(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y))==1;}
public static class Fault{public static string At="";public static void Check(string at){if(At!=at)return;At="";throw new IOException("Injected "+at);}}
public sealed class UnitState:Il2CppSystem.Object
{
    Polytopia.Data.UnitData.Type Type;Polytopia.Data.UnitData Data=new();WorldCoordinates Coordinates=new();
    public uint id{get;set;}public uint leader{get;set;}public uint follower{get;set;}public byte owner{get;set;}=1;
    public TribeType birthClimate{get;set;}=TribeType.Elyrion;public SkinType birthClimateSkinType{get;set;}=SkinType.DarkElf;
    public Polytopia.Data.UnitData.Type type{get=>Type;set{Type=value;Fault.Check("type");}}
    public Polytopia.Data.UnitData UnitData{get=>Data;set{Data=value;Fault.Check("data");}}
    public WorldCoordinates previousTurnEndCoordinates{get;set;}=new(5,6);public WorldCoordinates coordinates{get=>Coordinates;set{Coordinates=value;Fault.Check("coordinates");}}
    public WorldCoordinates home{get;set;}=new(8,9);public UnitState? passengerUnit{get;set;}
    public ushort health{get;set;}=10;public ushort promotionLevel{get;set;}public ushort xp{get;set;}=4;
    public bool moved{get;set;}public bool attacked{get;set;}public GridDirection direction{get;set;}=GridDirection.South;public bool flipped{get;set;}=true;public ushort createdTurn{get;set;}=2;
    public Il2CppSystem.Collections.Generic.List<UnitEffect> effects{get;set;}=new();public bool isTemporaryExplorerUnit{get;set;}
    public bool HasLeader()=>leader!=0;public bool HasFollower()=>follower!=0;public bool HasEffect(UnitEffect e)=>Deception.PolyModAdapter.Native.Items(effects).Contains(e);
    public void MakeExhauseted(GameState state){moved=attacked=true;effects.Remove(UnitEffect.Boosted);effects.Remove(UnitEffect.DoubleReady);Fault.Check("exhaust");}
}
public sealed partial class TileData:Il2CppSystem.Object
{
    UnitState? Unit;
    public WorldCoordinates coordinates{get;set;}=new();public bool IsWater{get;set;}public bool Port{get;set;}public bool City{get;set;}public bool Wetland{get;set;}public byte owner{get;set;}=1;public bool Explored{get;set;}=true;public bool IsLand=>!IsWater;
    public UnitState? unit{get=>Unit;set{Unit=value;Fault.Check("tile");}}
    public Polytopia.Data.TerrainData.Type terrain{get;set;}=Polytopia.Data.TerrainData.Type.field;public ImprovementState? improvement{get;set;}
    public bool HasOpponentCity(byte actor)=>City&&owner!=0&&owner!=actor;public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>t==Polytopia.Data.ImprovementData.Type.city?City:improvement?.type==t;public bool IsWetland()=>Wetland;public bool GetExplored(byte player)=>Explored; public int Cost=10;public bool Blocked;public int GetMovementCost(MapData map,TileData from,PathFinderSettings settings)=>Cost;public UnitState? GetUnit(GameState state,byte actor,bool flag)=>Explored?unit:null;public bool matchesAnyTerrain(Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> allowed)=>Deception.PolyModAdapter.Native.Items(allowed).Cast<Polytopia.Data.TerrainData>().Any(t=>t.type==terrain||Wetland&&t.type==Polytopia.Data.TerrainData.Type.wetland);
}
public sealed class MapData:Il2CppSystem.Object{public List<TileData> Tiles{get;}=new();}
public static class MapDataExtensions{public static object GetArea(MapData m,WorldCoordinates c,int n,bool a,bool b)=>Deception.PolyModAdapter.Native.NewList("TileData",m.Tiles.Where(t=>Math.Max(Math.Abs(t.coordinates.X-c.X),Math.Abs(t.coordinates.Y-c.Y))<=n).Cast<object>());public static TileData? GetTile(MapData map,WorldCoordinates at)=>map.Tiles.SingleOrDefault(t=>t.coordinates.X==at.X&&t.coordinates.Y==at.Y);}
public sealed class PlayerState:Il2CppSystem.Object {public bool Water{get;set;}public bool Ocean{get;set;}public byte Id{get;set;}=1;public Dictionary<byte,DiplomacyRelation> relations{get;set;}=new();public bool Researched{get;set;}=true;public HashSet<string> Upgrades{get;set;}=new();public bool Propaganda{get;set;}}
public sealed class DiplomacyRelation{public int State{get;set;}}
public static class PlayerExtensions{public static bool HasTech(PlayerState p,Technology t)=>t==Technology.dcp_propaganda?p.Propaganda:p.Researched;}
public static class UnitDataExtensions
{
 public static bool IsAquatic(Polytopia.Data.UnitData d)=>d.unitAbilities.Contains(UnitAbility.Swim);
 public static bool HasAbility(UnitState u,Polytopia.Data.UnitAbility.Type a)=>u.UnitData.unitAbilities.Contains((UnitAbility)(int)a);
 public static int GetMovement(UnitState u,GameState s)=>u.UnitData.movement;
 public static int GetSightRange(Polytopia.Data.UnitData d){int result=d.unitAbilities.Contains(UnitAbility.Scout)?2:1;Deception.PolyModAdapter.NativeMimicMovement.Sight(d,ref result);return result;}
 public static int GetMaxHealth(UnitState u,GameState s)=>u.passengerUnit is null?u.UnitData.health+u.promotionLevel*50:GetMaxHealth(u.passengerUnit,s);
 public static Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> GetAllowedTerrain(UnitState u,GameState s)
 {
  var a=new Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData>();bool fly=HasAbility(u,Polytopia.Data.UnitAbility.Type.fly),swim=HasAbility(u,Polytopia.Data.UnitAbility.Type.swim);
  foreach(var t in Enum.GetValues<Polytopia.Data.TerrainData.Type>())if(fly||u.UnitData.Terrains.Contains(t.ToString())||swim&&t is Polytopia.Data.TerrainData.Type.water or Polytopia.Data.TerrainData.Type.ocean||t==Polytopia.Data.TerrainData.Type.water&&s.Player.Water||t==Polytopia.Data.TerrainData.Type.ocean&&s.Player.Ocean)a.Add(new(){type=t});
  object? result=a;Deception.PolyModAdapter.NativeMimicWaterMovement.AfterTerrain(u,s,ref result);return (Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData>)result!;
 }
}
public sealed class ImprovementState:Il2CppSystem.Object { public Polytopia.Data.ImprovementData.Type type{get;set;} }
public static class ImprovementDataExtensions { public static bool HasAbility(Polytopia.Data.ImprovementData d,Polytopia.Data.ImprovementAbility.Type a)=>d.type==Polytopia.Data.ImprovementData.Type.bridge; }
// Native control-flow ports preserve reverse paths, native costs/early-stop,
// generated GetPath wrapper hooks and accepted queued MoveAction ordering.
public sealed class PathFinderSettings:Il2CppSystem.ValueType {public UnitState Unit=null!;public GameState State=null!; public static PathFinderSettings CreateForUnit(UnitState u,GameState s)=>new(){Unit=u,State=s};}
public static class PathFinder {
 public static int Queries; public static bool ThrowQuery,CarrierDetour,NullForNoPath;public static Action<GameState,UnitState>? During;
 // Installed GetPath returns null on rejected endpoints/search exhaustion.
 // Retain old fixture mode for its unrelated synthetic route assertions.
 static object? NoPath()=>NullForNoPath?null:Deception.PolyModAdapter.Native.NewList("WorldCoordinates",Array.Empty<object>());
 static bool Same(WorldCoordinates a,WorldCoordinates b)=>a.X==b.X&&a.Y==b.Y;
 public static bool IsTileAccessible(TileData to,TileData from,PathFinderSettings p){if(to.Blocked||!to.Explored||to.unit is not null)return false;if(to.City||p.Unit.UnitData.unitAbilities.Contains(UnitAbility.Fly))return true;
  if(to.IsWetland()&&Deception.PolyModAdapter.Native.Items(UnitDataExtensions.GetAllowedTerrain(p.Unit,p.State)).Cast<Polytopia.Data.TerrainData>().Any(x=>x.type==Polytopia.Data.TerrainData.Type.wetland))return true;
  if(p.Unit.UnitData.unitAbilities.Contains(UnitAbility.Amphibious))return true;
  if(p.Unit.UnitData.unitAbilities.Contains(UnitAbility.Swim)&&!p.Unit.UnitData.unitAbilities.Contains(UnitAbility.Carry))return to.IsWater||to.IsWetland();
  if(p.Unit.UnitData.unitAbilities.Contains(UnitAbility.Carry))return to.IsWater||ActionUtils.WillUnitEmbark(p.Unit,to,p.State)==ActionUtils.EmbarkStatus.Disembark;
  return !to.IsWater||ActionUtils.WillUnitEmbark(p.Unit,to,p.State)==ActionUtils.EmbarkStatus.Embark;}
 public static bool IsAllowedToFinishOnTile(PathFinderSettings p,TileData t)=>!t.Blocked&&t.Explored&&t.unit is null&&
  !(p.Unit.type==Polytopia.Data.UnitData.Type.cloak&&t.HasOpponentCity(p.Unit.owner))&&
  !(t.HasOpponentCity(p.Unit.owner)&&p.State.Player.relations.TryGetValue(t.owner,out var r)&&r.State is 1 or 3);
 static object? NativePath(GameState s,WorldCoordinates f,WorldCoordinates t,int n,UnitState u){Queries++;if(ThrowQuery)throw new Exception("query failed");During?.Invoke(s,u);
  var target=MapDataExtensions.GetTile(s.Map,t);var origin=MapDataExtensions.GetTile(s.Map,f);var settings=PathFinderSettings.CreateForUnit(u,s);if(target is null||origin is null||n<=0||!IsAllowedToFinishOnTile(settings,target))return NoPath();
  if(CarrierDetour&&u.type==Polytopia.Data.UnitData.Type.dcp_mimic_form_cloak&&t.X==2&&t.Y==1)return Deception.PolyModAdapter.Native.NewList("WorldCoordinates",new object[]{t,new WorldCoordinates(1,2),f});
  var queue=new Queue<(TileData,int,List<WorldCoordinates>)>();queue.Enqueue((origin,0,new(){f}));var seen=new HashSet<TileData>{origin};
  while(queue.Count>0){var (tile,cost,route)=queue.Dequeue();if(cost>=n*10)continue;foreach(var next in s.Map.Tiles.Where(x=>WorldCoordinates.IsAdjacent(x.coordinates,tile.coordinates))){if(seen.Contains(next)||!IsTileAccessible(next,tile,settings))continue;
   var allowed=UnitDataExtensions.GetAllowedTerrain(u,s);if(!next.City&&!next.IsWetland()&&!u.UnitData.unitAbilities.Contains(UnitAbility.Swim)&&!Deception.PolyModAdapter.Native.Items(allowed).Cast<Polytopia.Data.TerrainData>().Any(x=>x.type==next.terrain))continue;
   var path=route.Concat(new[]{next.coordinates}).ToList();if(Same(next.coordinates,t)){path.Reverse();return Deception.PolyModAdapter.Native.NewList("WorldCoordinates",path.Cast<object>());}seen.Add(next);queue.Enqueue((next,cost+next.Cost,path));}}
  return NoPath();}
 public static Il2CppSystem.Collections.Generic.List<WorldCoordinates> GetPath(GameState s,WorldCoordinates f,WorldCoordinates t,int n,UnitState u){object? result=null;Deception.PolyModAdapter.NativeMimicWaterMovement.Scope? scope=null;Exception? error=null;
  try{if(Deception.PolyModAdapter.NativeMimicWaterMovement.BeforePath(s,f,t,u,out scope,ref result))result=NativePath(s,f,t,n,u);Deception.PolyModAdapter.NativeMimicWaterMovement.AfterPath(t,scope,ref result);object checkedResult=result!;Deception.PolyModAdapter.NativeRogueTactics.Path(s,f,u,ref checkedResult);result=checkedResult;return (Il2CppSystem.Collections.Generic.List<WorldCoordinates>)result!;}catch(Exception e){error=e;throw;}finally{Deception.PolyModAdapter.NativeMimicWaterMovement.Finished(error,scope);}}
 public static Il2CppSystem.Collections.Generic.List<WorldCoordinates> GetMoveOptions(GameState s,WorldCoordinates f,int n,UnitState u){object? result=null;Deception.PolyModAdapter.NativeMimicWaterMovement.Scope? scope=null;Exception? error=null;
  try{if(Deception.PolyModAdapter.NativeMimicWaterMovement.BeforeOptions(s,f,u,out scope,ref result)){var options=s.Map.Tiles.Where(x=>Deception.PolyModAdapter.Native.Items(NativePath(s,f,x.coordinates,n,u)).Any()).Select(x=>(object)x.coordinates);result=Deception.PolyModAdapter.Native.NewList("WorldCoordinates",options);}Deception.PolyModAdapter.NativeMimicWaterMovement.AfterOptions(s,f,n,u,scope,ref result);Deception.PolyModAdapter.NativeCloakMovement.AfterOptions(s,f,u,ref result);object checkedResult=result!;Deception.PolyModAdapter.NativeRogueTactics.Options(s,f,n,u,ref checkedResult);result=checkedResult;return (Il2CppSystem.Collections.Generic.List<WorldCoordinates>)result!;}catch(Exception e){error=e;throw;}finally{Deception.PolyModAdapter.NativeMimicWaterMovement.Finished(error,scope);}}
}
public class ActionBase:Il2CppSystem.Object {public byte PlayerId{get;set;}=1;public bool Valid=true; public bool IsValid(GameState s)=>Valid;}
public sealed class MoveAction:ActionBase {public enum MoveReason{Command=0,Attack=1,Push=2}public MoveReason Reason{get;set;}public uint UnitId{get;set;}public Il2CppSystem.Collections.Generic.List<WorldCoordinates> Path{get;set;}=new();public bool Skip,Fail,NoMove;public int CoastChildren;public int Sight;public Action<GameState,UnitState>? During;
 public void Execute(GameState s){Deception.PolyModAdapter.NativeMimicMovement.BeforeWaterMove(this,s,out var waterScope);Deception.PolyModAdapter.NativeDisguiseMovement.BeforeMove(this,s,out var scope);Exception? error=null;try{if(!Skip){s.TryGetUnit(UnitId,out var u);if(!NoMove){MapDataExtensions.GetTile(s.Map,u!.coordinates)!.unit=null;u.coordinates=Path[0];MapDataExtensions.GetTile(s.Map,u.coordinates)!.unit=u;}During?.Invoke(s,u!);Sight=UnitDataExtensions.GetSightRange(u!.UnitData);if(ActionUtils.WillUnitEmbark(u!,MapDataExtensions.GetTile(s.Map,u!.coordinates)!,s)!=ActionUtils.EmbarkStatus.None)CoastChildren++;if(Fail)throw new Exception("move failed");}Deception.PolyModAdapter.NativeMimicMovement.AfterWaterMove(!Skip,waterScope);Deception.PolyModAdapter.NativeDisguiseMovement.AfterMove(!Skip,scope);}catch(Exception e){error=e;}finally{error=Deception.PolyModAdapter.NativeDisguiseMovement.Finished(error,scope);}if(error is not null)throw error;}}
public sealed class MoveCommand:ActionBase { public uint UnitId{get;set;}public WorldCoordinates From{get;set;}=new();public WorldCoordinates To{get;set;}=new(); public bool IsValid(GameState s,out string why){why="";s.TryGetUnit(UnitId,out var u);bool valid=Valid&&u is not null&&u.owner==PlayerId&&PlayerId==s.CurrentPlayer&&!u.moved&&!u.effects.Contains(UnitEffect.Frozen)&&MapDataExtensions.GetTile(s.Map,From)?.unit==u&&Deception.PolyModAdapter.Native.Items(PathFinder.GetPath(s,From,To,UnitDataExtensions.GetMovement(u,s),u)).Any();Deception.PolyModAdapter.NativeMimicWaterMovement.AfterMoveValid(this,s,ref valid,ref why);Deception.PolyModAdapter.NativeCloakMovement.AfterMoveValid(this,s,ref valid,ref why);return valid;}
 public MoveAction Execute(GameState s){if(!IsValid(s,out var why))throw new Exception("invalid command "+why);s.TryGetUnit(UnitId,out var u);var action=new MoveAction{UnitId=UnitId,PlayerId=PlayerId,Path=PathFinder.GetPath(s,From,To,UnitDataExtensions.GetMovement(u!,s),u!)};u!.moved=true;s.ActionStack.Add(action);action.Execute(s);s.ActionStack.Remove(action);return action;}}

public sealed class GameState:Il2CppSystem.Object
{
    public enum State{Started,Ended};public State CurrentState{get;set;}=State.Started;public PlayerState Player{get;set;}=new();public bool TryGetPlayer(byte id,out PlayerState? player){player=Player;return id==Player.Id;}
    uint Counter=7;public int Version{get;set;}=123;public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=4;
    public uint CurrentUnitId{get=>Counter;set{Counter=value;Fault.Check("counter");}}public MapData Map{get;set;}=new();public List<object> ActionStack{get;set;}=new();
    public GameLogicData GameLogicData{get;set;}=new();public bool TryGetUnit(uint id,out UnitState? unit){unit=Map.Tiles.SelectMany(t=>t.unit is UnitState u?new[]{u,u.passengerUnit}:Array.Empty<UnitState?>()).SingleOrDefault(u=>u?.id==id);return unit is not null;}public int Stars{get;set;}=21;public int Score{get;set;}=300;public int CitySupport{get;set;}=1;
}
public sealed class GameLogicData:Il2CppSystem.Object{
 public bool TryGetData(Polytopia.Data.ImprovementData.Type t,out Polytopia.Data.ImprovementData d){d=new(){type=t};return true;}
 public Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData> GetUnlockedMovements(PlayerState p){var a=new Il2CppSystem.Collections.Generic.List<Polytopia.Data.TerrainData>();a.Add(new(){type=Polytopia.Data.TerrainData.Type.field});if(p.Water)a.Add(new(){type=Polytopia.Data.TerrainData.Type.water});if(p.Ocean)a.Add(new(){type=Polytopia.Data.TerrainData.Type.ocean});return a;}
public Il2CppSystem.Collections.Generic.List<Polytopia.Data.UnitData> GetUnlockedUpgradesForUnit(PlayerState player,GameState state,Polytopia.Data.UnitData source){var list=new Il2CppSystem.Collections.Generic.List<Polytopia.Data.UnitData>();foreach(var d in Deception.PolyModAdapter.RecruitmentHooks.Definitions.Values)if(ReferenceEquals(d.upgradesFrom,source)&&player.Upgrades.Contains(d.type.ToString()))list.Add(d);return list;}}
public static class ActionUtils {
 public enum EmbarkStatus{None=0,Embark=1,Disembark=2}
 public static EmbarkStatus WillUnitEmbark(UnitState u,TileData t,GameState s){bool boat=Deception.PolyModAdapter.NativeMimicTransport.IsVessel(u),aquatic=UnitDataExtensions.IsAquatic(u.UnitData)||u.UnitData.unitAbilities.Contains(UnitAbility.Fly);
  var result=boat&&(t.City||!t.IsWater&&!t.IsWetland())?EmbarkStatus.Disembark:!boat&&!aquatic&&(t.IsWater||t.IsWetland())&&t.Port&&s.Player.Water?EmbarkStatus.Embark:EmbarkStatus.None;
  Deception.PolyModAdapter.NativeMimicMovement.Transport(u,t,s,ref result);return result;}}
public sealed class EmbarkAction:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public WorldCoordinates Coordinates{get;set;}=new();public void Execute(GameState game){}}
public sealed class DisembarkAction:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public WorldCoordinates Coordinates{get;set;}=new();public void Execute(GameState game){}}
namespace Deception.PolyModAdapter
{
    internal static class RecruitmentHooks
    {
        internal const string UT="Polytopia.Data.UnitData+Type",TT="Technology",GLD="GameLogicData";internal static bool Registered=true;internal static Dictionary<string,RecruitmentForm> Forms=new();
        internal static Dictionary<string,Polytopia.Data.UnitData> Definitions=new();
        internal static object Data(object state,string name)=>Definitions[name];
        internal static void Reset()
        {
            Definitions.Clear();Forms.Clear();
            // Movement and carrier abilities are derived from the captured native tree and actual Core mutation.
            var mutation=RecruitmentCatalog.Add(TechTree.AddDeception(File.ReadAllText(Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE")!)).Json);
            var units=JsonNode.Parse(mutation.Json)!["unitData"]!;
            foreach(var type in Enum.GetValues<Polytopia.Data.UnitData.Type>())
            {
                string name=type.ToString();var source=units[name]!;var d=new Polytopia.Data.UnitData{Terrains=source["movementTerrain"]?.AsArray().Select(x=>x!.GetValue<string>()).ToArray()??new[]{"field","wetland"},type=type,health=source["health"]!.GetValue<int>(),cost=source["cost"]!.GetValue<int>(),movement=source["movement"]!.GetValue<int>(),attack=source["attack"]!.GetValue<int>(),defence=source["defence"]!.GetValue<int>(),range=source["range"]!.GetValue<int>()};
                foreach(var a in source["unitAbilities"]!.AsArray().Select(x=>x!.GetValue<string>()))
                    if(a is "scout" or "fly" or "swim" or "carry" or "stiff" or "protect" or "amphibious")d.unitAbilities.Add(Enum.Parse<global::UnitAbility>(a,true));
                Definitions[name]=d;
                if(RecruitmentCatalog.IsMimicId(name))Forms[name]=RecruitmentCatalog.IsMenuId(name)?new(name,name,RecruitmentCatalog.Label(name),1,d.movement,0,0,false):mutation.Forms.Single(f=>f.VariantId==name);
            }
            foreach(var name in new[]{"scoutship","rammership","bombership"})Definitions[name].upgradesFrom=Definitions["transportship"];
        }
    }
    internal static class NativeRegroupSession
    {
        internal static bool Enabled=true,Admitted=true;internal static object? Owned;internal static List<string> Faults=new();internal static int Prepares;
        internal static bool Ready(object state)=>Enabled&&Admitted&&ReferenceEquals(Owned,state);
        internal static LedgerUnit? ReadIdentity(object state,uint id){if(!Ready(state))return null;var p=NativeGameStateEnvelope.Get(state);return p is null?null:NativeIdentityLedgerCodec.Decode(p.Bytes(),p.Revision).Units.SingleOrDefault(u=>u.Alive&&u.NativeId==id);}
        internal static void Prepare(object state){if(!Ready(state))throw new InvalidDataException("Unready fixture state");Prepares++;}
        internal static void Fault(string message)=>Faults.Add(message);
    }
    internal static class NativeGameStateEnvelope
    {
        internal sealed class Snapshot{internal long Revision{get;}readonly byte[] Value;internal Snapshot(long r,byte[] b){Revision=r;Value=(byte[])b.Clone();}internal byte[] Bytes()=>(byte[])Value.Clone();}
        internal static byte[] NativeBytes(object state,int version)=>System.Text.Encoding.UTF8.GetBytes(TransportStateDump.Dump((GameState)state));
        internal static Dictionary<object,Snapshot> Frames=new();internal static Snapshot? Get(object state)=>Frames.GetValueOrDefault(state);
        internal static void Set(object state,long revision,byte[] bytes){Frames[state]=new(revision,bytes);Fault.Check("ledger");}
    }
    internal static class NativeLedgerState
    {
        internal sealed record Body(object Unit,bool Root);
        internal static LedgerAppearance ExposedAppearance(object state,object unit,LedgerUnit row)
        {var raw=Capture(state,new(unit,true),row.AmphibiousMimic?AquarionCatalog.MimicId:RecruitmentCatalog.MenuId,false);return raw with{Health=10,Maximum=10,Veteran=false,Concealment=ApparentConcealment.Visible,GrowthTurns=-1,GrowthDueTurn=-1,Effects=Array.Empty<string>(),Passenger=null};}
        internal static LedgerAppearance CapturePublic(object state,Body body,string form,LedgerUnit? row)
        {return row is {Kind:LedgerActualKind.Mimic}?ExposedAppearance(state,body.Unit,row):Capture(state,body,form,false);}
        internal static Dictionary<uint,Body> Graph(object state){var result=new Dictionary<uint,Body>();foreach(var tile in ((GameState)state).Map.Tiles){if(tile.unit is not UnitState u)continue;result.Add(u.id,new(u,true));if(u.passengerUnit is UnitState cargo)result.Add(cargo.id,new(cargo,false));}return result;}
        internal static LedgerAppearance Capture(object state,Body b,string form,bool legacy){var u=(UnitState)b.Unit;var cargo=u.passengerUnit is null?null:Capture(state,new(u.passengerUnit,false),u.passengerUnit.type.ToString(),false);return new(form,new("Elyrion","DarkElf"),new("Imperius","Default"),new("Elyrion","DarkElf"),57,u.health,(ushort)(cargo?.Maximum??u.UnitData.health),false,ApparentConcealment.Visible,-1,Native.Items(u.effects).Select(x=>x.ToString()!).ToArray(),Passenger:cargo);}
        internal static void Validate(object state,long revision,byte[] bytes)
        {
            Fault.Check("validation");var game=(GameState)state;var ledger=NativeIdentityLedgerCodec.Decode(bytes,revision);
            foreach(var row in ledger.Units.Where(x=>x.Alive))
            {
                var root=game.Map.Tiles.Select(t=>t.unit).SingleOrDefault(u=>u?.id==row.NativeId)??throw new InvalidDataException("Missing root");
                if(row.Transport is null){if(root.passengerUnit is not null)throw new InvalidDataException("Unexpected actual cargo");}
                else if(root.passengerUnit?.id!=row.Transport.PassengerNativeId)throw new InvalidDataException("Wrong actual cargo");
                if(row.Kind==LedgerActualKind.Mimic&&(root.health!=10||root.UnitData.health!=10||root.UnitData.attack!=0||root.promotionLevel!=0))throw new InvalidDataException("Mimic gained actual health/offense");
            }
        }
    }
    internal static class NativeDisguiseGrowthAudio
    {
        internal static List<(object Action,object State,LedgerAppearance Before,LedgerUnit After)> Recorded=new();
        internal static void Record(object action,object state,LedgerAppearance before,LedgerUnit after)
        {
            if(after.Appearance is LedgerAppearance a&&before.Passenger is null&&a.Passenger is null&&
                ((before.Form=="dragonegg"&&a.Form=="babydragon")||(before.Form=="babydragon"&&a.Form=="firedragon")))Recorded.Add((action,state,before,after));
        }
        internal static void Cancel(object action,object state)=>Recorded.RemoveAll(c=>ReferenceEquals(c.Action,action)&&ReferenceEquals(c.State,state));
    }
    internal static class Entry{internal static void Note(string category,string value){}}
    internal static class NativeGrowthSight
    {
        internal sealed class Pending:IDisposable
        {
            readonly GameState State;readonly List<object> Before;bool Accepted;
            internal Pending(GameState game,byte owner,IReadOnlyList<object> units)
            {State=game;Before=game.ActionStack;State.ActionStack=new(Before);foreach(var unit in units)State.ActionStack.Add(((UnitState)unit).id);}
            internal void Accept(){Fault.Check("sight-accept");Accepted=true;}
            internal void Rollback(){if(!Accepted)State.ActionStack=Before;}
            public void Dispose(){if(!Accepted)Rollback();}
        }
        internal static Pending Stage(object state,byte owner,IReadOnlyList<object> units)
        {var p=new Pending((GameState)state,owner,units);try{Fault.Check("sight-stage");return p;}catch{p.Rollback();throw;}}
    }
}

namespace Deception.PolyModAdapter
{
    internal static class TransportStateDump
    {
        internal static string Body(UnitState u)=>string.Join("|",u.Pointer,u.id,u.type,u.UnitData.Pointer,u.owner,u.health,u.promotionLevel,u.xp,u.birthClimate,u.birthClimateSkinType,u.createdTurn,u.coordinates,u.previousTurnEndCoordinates,u.home,u.moved,u.attacked,u.direction,u.flipped,u.effects.Pointer,string.Join(",",Native.Items(u.effects)),u.passengerUnit is null?"":Body(u.passengerUnit));
        internal static string Dump(GameState s)=>string.Join(";",s.Map.Tiles.Select(t=>t.unit is null?"empty":Body(t.unit)))+"/"+s.CurrentUnitId+"/"+s.Stars+"/"+s.Score+"/"+s.CitySupport+"/"+string.Join(",",s.ActionStack);
    }
    internal static partial class NativeExchangeCommands{internal static bool LiveExecution(object state)=>NativeRegroupSession.Ready(state);}
    internal static class NativeExchangeConsequences
    {
        internal static Pending Capture(object state,NativeExchangePlan plan)=>new((GameState)state,plan);
        internal sealed class Pending:IDisposable
        {
            readonly GameState State;readonly NativeExchangePlan Plan;readonly List<object> Stack;bool Accepted;
            internal Pending(GameState state,NativeExchangePlan plan){State=state;Plan=plan;Stack=state.ActionStack;}
            internal void StageFinalSources(Action<string> fault){State.ActionStack=new(Stack){((UnitState)Plan.CommittedA!).id,((UnitState)Plan.CommittedB!).id};Fault.Check("sight-stage");}
            internal void MergeDiscovery(){} // Existing fixture has no discovery provenance port.
            internal void AssertPending(){Fault.Check("sight-pending");}
            internal void Accept(){Fault.Check("sight-accept");Accepted=true;}
            internal void Rollback(){if(!Accepted)State.ActionStack=Stack;}
            public void Dispose(){if(!Accepted)Rollback();}
        }
    }
}


public static class GameManager {public static GameState? GameState{get;set;}public static object? Client{get;set;}}

namespace Deception.PolyModAdapter {internal static class NativeDisguiseLifecycle {internal static void Reveal(object s,object u){var p=NativeGameStateEnvelope.Get(s)!;var l=NativeIdentityLedgerCodec.Decode(p.Bytes(),p.Revision);var row=l.Units.Single(x=>x.NativeId==((UnitState)u).id);l=l with{Units=l.Units.Select(x=>x.LogicalId==row.LogicalId?x with{Independent=false,Appearance=null}:x).ToArray()};NativeGameStateEnvelope.Set(s,p.Revision,NativeIdentityLedgerCodec.Encode(l,p.Revision));}}}

namespace Deception.PolyModAdapter {
 internal static class NativeRogueRules {internal static bool IsRogue(object state,object? unit)=>unit is not null&&NativeRegroupSession.ReadIdentity(state,((UnitState)unit).id)?.Rogue is not null;}
 internal static class NativeSpyNetwork {internal static bool Accessible(object state,object tile,byte actor)=>((TileData)tile).Explored;}
}
