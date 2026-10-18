using System;
using System.Collections.Generic;
using System.Reflection;
using Deception.PolyModAdapter;
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
namespace Polytopia.Data {public static class UnitData {public enum Type{warrior,rider,dcp_spy,dcp_spymaid,dcp_motherfogger,dcp_mimic_spy,raft,cloak}}}
public enum UnitEffect{Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8}
public static class EnumCache<T> where T:struct,Enum {public static T GetType(string n)=>Enum.Parse<T>(n);public static string GetName(T v)=>v.ToString();}
public readonly record struct WorldCoordinates(int X,int Y);
public sealed class UnitState:Identity
{
 public uint id{get;set;}=1;public byte owner{get;set;}=1;public Polytopia.Data.UnitData.Type type{get;set;}=Polytopia.Data.UnitData.Type.dcp_spy;
 public int health{get;set;}=100;public bool moved{get;set;}public bool attacked{get;set;}public WorldCoordinates coordinates{get;set;}=new(2,3);
 public Il2CppSystem.Collections.Generic.List<UnitEffect> effects{get;set;}=new();public bool Leader,Follower,Rogue,CanCommand=true;
 public UnitState? passengerUnit{get;set;}public bool HasLeader()=>Leader;public bool HasFollower()=>Follower;
 public bool HasEffect(UnitEffect e)=>e==UnitEffect.Frozen&&FakeFrozenRead||effects.Contains(e);public static bool FakeFrozenRead;
 public bool CanAttack(){bool b=false;return NativeSpyActions.NoAttack(this,ref b)?!attacked:b;}
}
public sealed class PlayerState:Identity {public byte Id{get;set;}=1;}
public sealed class TileData:Identity {public WorldCoordinates coordinates{get;set;}=new(2,3);public UnitState? unit{get;set;}public bool Accessible=true;public bool GetExplored(byte b)=>Accessible;}
public sealed class MapData:Identity {public readonly Dictionary<WorldCoordinates,TileData> Tiles=new();}
public static class MapDataExtensions {public static TileData? GetTile(MapData m,WorldCoordinates c)=>m.Tiles.GetValueOrDefault(c);}
public sealed class GameState:Identity
{
 public enum State{Started,Finished}public State CurrentState{get;set;}=State.Started;public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=3;
 public MapData Map{get;set;}=new();public Identity GameLogicData{get;set;}=new();public readonly Dictionary<uint,UnitState> Units=new();
 public bool TryGetUnit(uint id,out UnitState? unit)=>Units.TryGetValue(id,out unit);
}
public sealed class ClientBase:Identity {public GameState? GameState{get;set;}public bool IsReady{get;set;}=true;public bool IsReplay{get;set;}public bool IsSpectating{get;set;}}
public static class GameManager
{
 public static GameState? GameState{get;set;}public static ClientBase? Client{get;set;}public static PlayerState? LocalPlayer{get;set;}public static bool Viewing=true;
 public static bool IsPlayerViewing(byte id)=>Viewing&&id==LocalPlayer?.Id;
}
public sealed class MapRenderer:UnityEngine.Object {public static MapRenderer? Current{get;set;}public readonly Dictionary<uint,Unit> Units=new();public Unit? GetUnitInstance(uint id)=>Units.GetValueOrDefault(id);}
public sealed class MapRenderContext:Identity
{
 public MapData? mapData{get;set;}public Identity? gld{get;set;}public MapRenderer? unitRegistry{get;set;}public byte localPlayerId{get;set;}=1;public bool hideOutlines{get;set;}
}
public sealed class SkinVisualsTransientData:Identity {}
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
 public UnitState UnitState{get;set;}=new();public SkinVisualsReference skinVisuals{get;set;}=new();public bool Visible=true,Invisible;public bool? NativeReady;public Action? DuringRender;public bool Throw;
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
   bool can=NativeReady??(!UnitState.moved||UnitDataExtensions.CanAttackAnything(UnitState,1,GameManager.GameState!));
   var overlay=GetOverlayColorAndStrength(can,UnitState.effects);
   SkinVisualsRenderer.ColorizeUnit(skinVisuals,overlay.Item1,overlay.Item2,new(1,1,1,1),can?0:0.5f);
   SkinVisualsRenderer.ShowOutline(skinVisuals,can&&GameManager.IsPlayerViewing(UnitState.owner)&&!ctx.hideOutlines);
  }
  catch(Exception e){error=e;throw;}
  finally{NativeSpyReadinessPresentation.Finished(error,frame);}
 }
}
public static class UnitDataExtensions
{
 public static bool CanAttackAnything(UnitState u,int range,GameState s){bool b=false;return NativeSpyActions.NoAttackAny(u,ref b)?!u.attacked:b;}
 public static bool CanBePromoted(UnitState u,GameState s)=>true;
}
public static class BattleHelpers {public static bool CanDefenderRetaliate(GameState s,UnitState a,UnitState d)=>true;}
namespace Deception.PolyModAdapter
{
 internal static class Entry {internal static readonly List<string> Notes=new();internal static void Note(string a,string b)=>Notes.Add(a+":"+b);}
 internal static class RecruitmentHooks {internal static bool Registered=true;}
 internal static class NativeRegroupSession {internal static bool Enabled=>Available;internal static bool Available=true;internal static int Reads;internal static bool Ready(object s){Reads++;return Available;}}
 internal static class NativeRogueRules {internal static bool IsRogue(object s,object u)=>((UnitState)u).Rogue;internal static bool CanCommand(object s,object u,byte a){var state=(GameState)s;var unit=(UnitState)u;return state.CurrentPlayer==a&&unit.owner==a&&unit.CanCommand;}}
 internal static class NativeBlackout {internal static bool Blocked;internal static bool Covered(object s,byte p,NativeSpyNetwork.Position at)=>Blocked;}
}
