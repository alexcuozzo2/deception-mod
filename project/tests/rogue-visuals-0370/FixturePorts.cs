using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Deception.PolyModAdapter;
using Deception.Core;
public class Identity {static long Next;public IntPtr Pointer{get;}=new(++Next);}
namespace HarmonyLib
{
 public sealed class HarmonyMethod {public MethodInfo Method;public HarmonyMethod(MethodInfo m){Method=m;}}
 public sealed class Harmony {public readonly List<(MethodInfo Method,HarmonyMethod? Prefix,HarmonyMethod? Finalizer)> Hooks=new();public void Patch(MethodInfo m,HarmonyMethod? p,HarmonyMethod? q,object? t,HarmonyMethod? f,object? i)=>Hooks.Add((m,p,f));}
}
namespace UnityEngine
{
 public class Object:Identity {public bool Destroyed;public static implicit operator bool(Object? o)=>o is not null&&!o.Destroyed;}
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
}
namespace Il2CppSystem
{
 public sealed class ValueTuple<T1,T2> {public T1 Item1{get;set;}public T2 Item2{get;set;}public ValueTuple(T1 a,T2 b){Item1=a;Item2=b;}}
}
namespace Il2CppSystem.Collections.Generic {public class List<T>:System.Collections.Generic.List<T>{}}
namespace Polytopia.Data {public sealed class GameLogicData:Identity {} public static class ImprovementData {public enum Type{city,farm}} public static class UnitData {public enum Type{warrior,rider,dcp_spy,dcp_spymaid,dcp_mimic_spy,raft,cloak}}}
public enum UnitEffect{Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8}
public static class EnumCache<T> where T:struct,Enum {public static T GetType(string n)=>Enum.Parse<T>(n);public static string GetName(T v)=>v.ToString();}
public readonly record struct WorldCoordinates(int X,int Y);
public sealed class UnitState:Identity
{
 public uint id{get;set;}=1;public byte owner{get;set;}=1;public Polytopia.Data.UnitData.Type type{get;set;}=Polytopia.Data.UnitData.Type.dcp_spy;
 // Native Subvert acquisition clears moved/attacked; model that completed state.
 public int health{get;set;}=100;public bool moved{get;set;}public bool attacked{get;set;}public WorldCoordinates coordinates{get;set;}=new(2,3);
 public Il2CppSystem.Collections.Generic.List<UnitEffect> effects{get;set;}=new();public bool Leader,Follower,Rogue,CanCommand=true;
 public UnitState? passengerUnit{get;set;}public bool HasLeader()=>Leader;public bool HasFollower()=>Follower;
 public bool HasEffect(UnitEffect e)=>e==UnitEffect.Frozen&&FakeFrozenRead||effects.Contains(e);public static bool FakeFrozenRead;
 public bool CanAttack(){bool b=false;return NativeSpyActions.NoAttack(this,ref b)?!attacked:b;}
}
public sealed class PlayerState:Identity {public byte Id{get;set;}=1;public int color;public string tribe="imperius",skin="default";}
public sealed class TileData:Identity {public WorldCoordinates coordinates{get;set;}=new(2,3);public UnitState? unit{get;set;}public bool City=true;public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>City&&t==Polytopia.Data.ImprovementData.Type.city;public bool Accessible=true;public bool GetExplored(byte b)=>Accessible;}
public sealed class MapData:Identity {public readonly Dictionary<WorldCoordinates,TileData> Tiles=new();}
public static class MapDataExtensions {public static TileData? GetTile(MapData m,WorldCoordinates c)=>m.Tiles.GetValueOrDefault(c);}
public sealed class GameState:Identity
{
 public enum State{Started,Finished}public State CurrentState{get;set;}=State.Started;public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=3;public ushort CurrentCommand{get;set;}
 public MapData Map{get;set;}=new();public Polytopia.Data.GameLogicData GameLogicData{get;set;}=new();public readonly Dictionary<byte,PlayerState> Players=new();public bool TryGetPlayer(byte id,out PlayerState? p)=>Players.TryGetValue(id,out p);public readonly Dictionary<uint,UnitState> Units=new();
 public bool TryGetUnit(uint id,out UnitState? unit)=>Units.TryGetValue(id,out unit);
}
public sealed class ActionManager:Identity {public bool IsProcessing{get;set;}}
public sealed class ClientBase:Identity {public GameState? GameState{get;set;}public bool IsReady{get;set;}=true;public bool IsReplay{get;set;}public bool IsSpectating{get;set;}public bool IsWaitingForCommand{get;set;}public ActionManager ActionManager{get;set;}=new();}
public static class GameManager
{
 public static GameState? GameState{get;set;}public static ClientBase? Client{get;set;}public static PlayerState? LocalPlayer{get;set;}public static bool Viewing=true;
 public static bool IsPlayerViewing(byte id)=>Viewing&&id==LocalPlayer?.Id;
 public static void Update(){}
}
public sealed class MapRenderer:UnityEngine.Object {public static MapRenderer? Current{get;set;}public readonly Dictionary<uint,Unit> Units=new();public Unit? GetUnitInstance(uint id)=>Units.GetValueOrDefault(id);public readonly Dictionary<WorldCoordinates,Tile> Tiles=new();public Tile? GetTileInstance(WorldCoordinates at)=>Tiles.GetValueOrDefault(at);}
public sealed class MapRenderContext:Identity
{
 public MapData? mapData{get;set;}public Identity? gld{get;set;}public MapRenderer? unitRegistry{get;set;}public byte localPlayerId{get;set;}=1;public bool hideOutlines{get;set;}
}
public sealed class SkinVisualsTransientData:Identity {public PlayerState? Applied;public void SetupForUnit(Polytopia.Data.GameLogicData g,UnitState u,PlayerState p){object owner=p;NativeRogueVisuals.BeforeSkin(g,u,ref owner);Applied=(PlayerState)owner;}}
public sealed class SkinVisualsReference:UnityEngine.Object
{
 public bool Outline;public float OverlayStrength,TeamStrength;public UnityEngine.Color Color;public int Colors,Outlines;
}
public static class SkinVisualsRenderer
{
 public static void ColorizeUnit(SkinVisualsReference s,UnityEngine.Color c,float strength,UnityEngine.Color team,float teamStrength)
 {NativeSpyReadinessPresentation.BeforeColor(s,ref strength,ref teamStrength);s.Color=c;s.OverlayStrength=strength;s.TeamStrength=teamStrength;s.Colors++;}
 public static void ShowOutline(SkinVisualsReference s,bool show){NativeSpyReadinessPresentation.BeforeOutline(s,ref show);s.Outline=show;s.Outlines++;}
}
public sealed class Unit:UnityEngine.Object
{
 public UnitStatusDisplay? statusDisplay{get;set;}public SkinVisualsTransientData? LastSkin;public int Updates;public Action? Updated;
 public void UpdateObject(){Updates++;var state=GameManager.GameState!;var skin=new SkinVisualsTransientData();skin.SetupForUnit(state.GameLogicData,UnitState,state.Players[UnitState.owner]);LastSkin=skin;UpdateObject(new(){mapData=state.Map,gld=state.GameLogicData,unitRegistry=MapRenderer.Current,localPlayerId=GameManager.LocalPlayer!.Id},skin);Updated?.Invoke();}
 public void UpdateStatusDisplay(MapRenderContext ctx)
 {
  NativeRogueVisuals.BeforeStatusUpdate(this,out var f);
  try
  {
   if(statusDisplay is null)return;
   // Native body inlines RefreshColor after SetState; no RefreshColor call.
   // SetState's readiness Owner query must still see the command controller.
   statusDisplay.ReadyOwner=Owner;var color=ClientPlayerExtensions.GetPlayerColor(Owner,GameManager.GameState!);statusDisplay.Color=color;
  }finally{NativeRogueVisuals.Finished(null,f);}
 }
 public PlayerState Owner {get{object p=GameManager.GameState!.Players[UnitState.owner];NativeRogueVisuals.Owner(this,ref p);return (PlayerState)p;}}public UnitState UnitState{get;set;}=new();public SkinVisualsReference skinVisuals{get;set;}=new();public bool Visible=true,Invisible;public Action? DuringRender;public bool Throw;
 public bool GetVisible()=>Visible;public bool IsInvisibleForLocalPlayer()=>Invisible;
 public static Il2CppSystem.ValueTuple<UnityEngine.Color,float> GetOverlayColorAndStrength(bool ready,Il2CppSystem.Collections.Generic.List<UnitEffect> effects)
 {if(OverlayThrows)throw new Exception("overlay-failure");bool poison=effects.Contains(UnitEffect.Poisoned);return new(new(poison?0:1,1,poison?0:1,1),ready?(poison?0.3f:0):0.5f);}
 public static bool OverlayThrows;
 public void UpdateObject(MapRenderContext ctx,SkinVisualsTransientData transient)
 {
  NativeSpyReadinessPresentation.BeforeUpdate(this,ctx,out long frame);Exception? error=null;
  try
  {
   DuringRender?.Invoke();if(Throw)throw new Exception("native-render-failure");
   UpdateStatusDisplay(ctx);
   bool can=!UnitState.moved||UnitDataExtensions.CanAttackAnything(UnitState,1,GameManager.GameState!);
   var overlay=GetOverlayColorAndStrength(can,UnitState.effects);
   SkinVisualsRenderer.ColorizeUnit(skinVisuals,overlay.Item1,overlay.Item2,new(1,1,1,1),can?0:0.5f);
   SkinVisualsRenderer.ShowOutline(skinVisuals,can&&GameManager.IsPlayerViewing(UnitState.owner)&&!ctx.hideOutlines);
  }
  catch(Exception e){error=e;throw;}
  finally{NativeSpyReadinessPresentation.Finished(error,frame);}
 }
}
public sealed class GameLogicData:Identity {} public static class ImprovementData {public enum Type{city,farm}} public static class UnitDataExtensions
{
 public static bool CanAttackAnything(UnitState u,int range,GameState s){bool b=false;return NativeSpyActions.NoAttackAny(u,ref b)?!u.attacked:b;}
 public static bool CanBePromoted(UnitState u,GameState s)=>true;
}
public static class BattleHelpers {public static bool CanDefenderRetaliate(GameState s,UnitState a,UnitState d)=>true;}
namespace Deception.PolyModAdapter
{
 internal static class NativeGameStateEnvelope
 {
  internal sealed record Snapshot(long Revision,byte[] Data){internal byte[] Bytes()=>(byte[])Data.Clone();}
  static Snapshot? Current;static object? State;
  internal static Snapshot? Get(object state)=>ReferenceEquals(State,state)?Current:null;
  internal static void Publish(GameState s){State=s;var l=new NativeIdentityLedger(new Guid("00112233-4455-6677-8899-aabbccddeeff"),100,1,NativeRogueRules.Rows.Values.OrderBy(x=>x.LogicalId).ToArray(),Array.Empty<LedgerReceipt>());Current=new(0,NativeIdentityLedgerCodec.Encode(l,0));}
 }
 internal static class Entry {internal static readonly List<string> Notes=new();internal static void Note(string a,string b)=>Notes.Add(a+":"+b);}
 internal static class RecruitmentHooks {internal static bool Registered=true;}
 internal static class NativeRegroupSession {internal static bool Enabled=>Available;internal static bool Available=true;internal static int Reads;internal static bool Ready(object s){Reads++;return Available;}}
 internal static class NativeRogueRules {internal static readonly Dictionary<uint,LedgerUnit> Rows=new();internal static LedgerUnit? Row(object s,object u)=>Rows.GetValueOrDefault(((UnitState)u).id);internal static bool IsRogue(object s,object u)=>Row(s,u)?.Rogue is not null;internal static bool CanCommand(object s,object u,byte a){var state=(GameState)s;var unit=(UnitState)u;return state.CurrentPlayer==a&&unit.owner==a&&unit.CanCommand;}}
 internal static class NativeBlackout {internal static bool Blocked;internal static bool Covered(object s,byte p,NativeSpyNetwork.Position at)=>Blocked;}
}

public sealed class UnitStatusDisplay:UnityEngine.Object
{
 public PlayerState? ReadyOwner;public UnityEngine.Color Color;
 public Unit? unit{get;set;}public PlayerState? Applied;public Action? Inside;public bool Throw;
 public void SetUnit(Unit u){NativeRogueVisuals.BeforeStatusUnit(u,out var f);try{unit=u;Inside?.Invoke();if(Throw)throw new Exception("native-badge");Applied=u.Owner;}finally{NativeRogueVisuals.Finished(null,f);}}
 public void RefreshColor(){NativeRogueVisuals.BeforeStatusColor(this,out var f);try{Inside?.Invoke();if(Throw)throw new Exception("native-badge");Applied=unit?.Owner;}finally{NativeRogueVisuals.Finished(null,f);}}
}
public sealed class Tile:UnityEngine.Object
{
 public TileData? Data{get;set;}public bool Fire,Rainbow;public int Stops,Spawns,RainbowStops,RainbowSpawns;
 public void SpawnFire(float delay,string name){if(!NativeRogueVisuals.Fire(this))return;Fire=true;Spawns++;}
 public void StopFire(bool instant){Fire=false;Stops++;}
 public void SpawnRainbowFire(float delay){if(!NativeRogueVisuals.Fire(this))return;Rainbow=true;RainbowSpawns++;}
 public void StopRainbowFire(bool instant){Rainbow=false;RainbowStops++;}
}
public static class ClientPlayerExtensions
{
 public static UnityEngine.Color GetPlayerColor(PlayerState p,GameState state){object owner=p;NativeRogueVisuals.BadgeColor(ref owner,state);return new(((PlayerState)owner).color,0,0,1);}
}
