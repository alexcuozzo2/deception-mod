// Synthetic native boundary for the real NativeApi and Lighthouse Secret UI.
// The fixture performs no game execution, network requests or currency writes.
using System;
using System.Collections.Generic;
using System.Reflection;
using Deception.Core;

namespace HarmonyLib
{
 public sealed class HarmonyMethod { public MethodInfo Method{get;} public HarmonyMethod(MethodInfo method){Method=method;} }
 public sealed class Harmony
 {
  public readonly List<string> Hooks=new();
  public readonly List<(MethodInfo Target,HarmonyMethod? Prefix,HarmonyMethod? Postfix,HarmonyMethod? Finalizer)> Patches=new();
  public void Patch(MethodInfo target,HarmonyMethod? pre,HarmonyMethod? post,object? transpiler,HarmonyMethod? final,object? extra=null)
  {Hooks.Add(target.DeclaringType!.Name+"."+target.Name);Patches.Add((target,pre,post,final));}
 }
}
namespace Il2CppSystem
{
 public class Object {static long Next;public IntPtr Pointer{get;set;}=new(++Next);}
}
public sealed class NList<T>:Il2CppSystem.Object
{
 readonly List<T> Values=new();public int Count=>Values.Count;public T this[int index]{get=>Values[index];set=>Values[index]=value;}
 public NList(){}public NList(IEnumerable<T> values){Values.AddRange(values);}public void Add(T value)=>Values.Add(value);
 public bool Remove(T value)=>Values.Remove(value);public void Clear()=>Values.Clear();
}
namespace UnityEngine
{
 public class Object:Il2CppSystem.Object {public bool Dead;public static implicit operator bool(Object? value)=>value is not null&&!value.Dead;}
 public sealed class GameObject:Object {public bool activeInHierarchy{get;set;}=true;}
 public class Component:Object {public GameObject gameObject{get;set;}=new();}
 public readonly record struct Vector2(float x,float y);
 public enum TouchPhase {Began,Moved,Stationary,Ended,Canceled}
 public sealed class Touch {public TouchPhase phase{get;set;}=TouchPhase.Ended;public Vector2 position{get;set;}=new(10,20);}
 public static class Time {public static int frameCount{get;set;}=1;}
}
public readonly record struct WorldCoordinates(int X,int Y);
public sealed class PlayerState:Il2CppSystem.Object {public byte Id{get;set;}=1;public bool Alive{get;set;}=true;public int Currency{get;set;}=10;}
public static class PlayerExtensions {public static bool IsAlive(PlayerState player,GameState state)=>player.Alive;}
public sealed class TileData:Il2CppSystem.Object
{
 public WorldCoordinates coordinates{get;set;}=new(2,3);public HashSet<byte> Explored{get;}=new(){1};public bool IsLighthouse{get;set;}=true;
 public bool GetExplored(byte actor)=>Explored.Contains(actor);
}
public sealed class MapData:Il2CppSystem.Object {public Dictionary<WorldCoordinates,TileData> Tiles{get;}=new();}
public static class MapDataExtensions {public static TileData? GetTile(MapData map,WorldCoordinates at)=>map.Tiles.GetValueOrDefault(at);}
public sealed class GameState:Il2CppSystem.Object
{
 public enum State{Started,Ended}public State CurrentState{get;set;}=State.Started;
 public NList<PlayerState> PlayerStates{get;set;}=new();public MapData Map{get;set;}=new();
 public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=3;public int CurrentCommand{get;set;}=4;
 public bool TryGetPlayer(byte actor,out PlayerState? player)
 {player=null;for(int i=0;i<PlayerStates.Count;i++)if(PlayerStates[i].Id==actor){player=PlayerStates[i];return true;}return false;}
}
public sealed class ActionManager:Il2CppSystem.Object {public bool IsProcessing{get;set;}}
public class CommandBase:Il2CppSystem.Object {public NativeExchangeIntent? Intent{get;set;}}
public class ClientBase:Il2CppSystem.Object
{
 public GameState? GameState{get;set;}public bool IsReady{get;set;}=true;public bool IsReplay{get;set;}public bool IsSpectating{get;set;}
 public bool IsRecap{get;set;}public bool IsWaitingForCommand{get;set;}public ActionManager ActionManager{get;set;}=new();
 public readonly List<CommandBase> Sent=new();public bool ThrowOnSend;public int SendCalls;public Action<CommandBase>? Sending;
 public void SendCommand(CommandBase command){SendCalls++;if(ThrowOnSend)throw new InvalidOperationException("Synthetic send failure");Sending?.Invoke(command);Sent.Add(command);}
}
public sealed class GameManager
{
 public static GameState? GameState{get;set;}public static ClientBase? Client{get;set;}public static PlayerState? LocalPlayer{get;set;}
 public static bool Viewing{get;set;}=true;public static bool IsPlayerViewing(byte actor)=>Viewing&&LocalPlayer?.Id==actor;public void Update(){}
}
public sealed class Tile:UnityEngine.Component {public TileData? Data{get;set;}=new();public bool IsHidden{get;set;}}
public sealed class MapRenderer:UnityEngine.Component
{
 public static MapRenderer? Current{get;set;}public bool IsRendered{get;set;}=true;public Dictionary<WorldCoordinates,Tile> Tiles{get;}=new();
 public Tile? GetTileInstance(WorldCoordinates at)=>Tiles.GetValueOrDefault(at);
}
public sealed class ClientInteraction:UnityEngine.Component
{
 public bool wasDownOverUI{get;set;}public bool ValidTap{get;set;}=true;public bool ThrowOnTap;
 public int ProcessCalls,ReleaseCalls,SelectCalls;public void Process(){ProcessCalls++;}
 public void OnRelease(Tile tile,int index){ReleaseCalls++;}public void SelectTile(Tile tile){SelectCalls++;}
 public bool IsTouchValidTap(int index){if(ThrowOnTap)throw new InvalidOperationException("Synthetic tap failure");return ValidTap;}
}
public static class PolytopiaInput
{
 public static NList<UnityEngine.Touch> CurrentTouches{get;set;}=new();public static bool OutsideWindow{get;set;}public static bool OverUi{get;set;}
 public static int UiHitTests;public static bool IsPositionOverUIObject(UnityEngine.Vector2 position,int index){UiHitTests++;return OverUi;}
}
public static class PopupManager {public static bool PopupShowing{get;set;}}
public static class CameraController {public static bool IsDragging{get;set;}public static bool IsBlocked{get;set;}}
public static class HotSeatOverlay {public static bool IsActive{get;set;}}
public static class ExploreLightHouseTask
{
 public static bool UseTasks=true,AllowTile=true;public static int TaskChecks,TileChecks;
 public static bool ShouldUseLighthouseTasks(GameState state){TaskChecks++;return UseTasks;}
 public static bool EvaluateTile(TileData tile,GameState state){TileChecks++;return AllowTile&&tile.IsLighthouse;}
}
namespace Deception.PolyModAdapter
{
 internal static class RecruitmentHooks {internal static bool Registered=true;}
 internal static class NativeRegroupSession
 {
  internal static bool Enabled=true,DispatchAllowed=true;internal static readonly HashSet<object> States=new();
  internal static bool Ready(object state)=>States.Contains(state);internal static bool CanDispatch(object client)=>DispatchAllowed;
 }
 internal static class MimicRegroupUi {internal static bool Choosing;}
 internal static class NativeSpecialActionUi {internal static bool Choosing;}
 internal static class NativeLighthouseSecret
 {
  internal static bool OfferAllowed=true,NullOffer,ThrowOnOffer;
  internal static readonly List<(object State,byte Actor,object Tile)> Calls=new();
  internal static NativeExchangeIntent? LastOffer;
  internal static bool TryOffer(object state,byte actor,object tile,out NativeExchangeIntent? offer,out string reason)
  {
   Calls.Add((state,actor,tile));offer=null;reason="";if(ThrowOnOffer)throw new InvalidOperationException("Synthetic offer failure");
   if(!OfferAllowed){reason="Synthetic offer rejected";return false;}if(NullOffer)return true;
   var at=((TileData)tile).coordinates;var target=new NativeExchangeUnit(0,0,at.X,at.Y);
   offer=new(new Guid("00000000-0000-0000-0000-000000000001"),1,0,((GameState)state).CurrentTurn,actor,(NativeExchangeKind)13,false,NativePartnerReference.Existing,1,target,target,new byte[32]);
   LastOffer=offer;return true;
  }
 }
 internal static class NativeExchangeCommands
 {
  internal static bool AllowValid=true,ThrowOnCreate,ThrowOnValid;
  internal static readonly List<CommandBase> Created=new(),Forgotten=new();
  internal static readonly List<(object Command,object State,bool Dispatch)> Validated=new();
  internal static object Create(NativeExchangeIntent offer)
  {if(ThrowOnCreate)throw new InvalidOperationException("Synthetic create failure");var command=new CommandBase(){Intent=offer};Created.Add(command);return command;}
  internal static bool Valid(object command,object state,bool dispatch,out string reason)
  {Validated.Add((command,state,dispatch));if(ThrowOnValid)throw new InvalidOperationException("Synthetic validation failure");reason=AllowValid?"":"Synthetic command rejected";return AllowValid;}
  internal static void Forget(object command)=>Forgotten.Add((CommandBase)command);
 }
}
namespace Deception.Tests
{
 public static class FixtureReset
 {
  public static void Reset()
  {
   Deception.PolyModAdapter.NativeLighthouseSecretUi.Clear();
   GameManager.GameState=null;GameManager.Client=null;GameManager.LocalPlayer=null;GameManager.Viewing=true;MapRenderer.Current=null;
   UnityEngine.Time.frameCount=1;PolytopiaInput.CurrentTouches=new();PolytopiaInput.OutsideWindow=false;PolytopiaInput.OverUi=false;PolytopiaInput.UiHitTests=0;
   PopupManager.PopupShowing=false;CameraController.IsDragging=false;CameraController.IsBlocked=false;HotSeatOverlay.IsActive=false;
   ExploreLightHouseTask.UseTasks=true;ExploreLightHouseTask.AllowTile=true;ExploreLightHouseTask.TaskChecks=0;ExploreLightHouseTask.TileChecks=0;
   Deception.PolyModAdapter.RecruitmentHooks.Registered=true;Deception.PolyModAdapter.NativeRegroupSession.Enabled=true;
   Deception.PolyModAdapter.NativeRegroupSession.DispatchAllowed=true;Deception.PolyModAdapter.NativeRegroupSession.States.Clear();
   Deception.PolyModAdapter.MimicRegroupUi.Choosing=false;Deception.PolyModAdapter.NativeSpecialActionUi.Choosing=false;
   Deception.PolyModAdapter.NativeLighthouseSecret.OfferAllowed=true;Deception.PolyModAdapter.NativeLighthouseSecret.NullOffer=false;
   Deception.PolyModAdapter.NativeLighthouseSecret.ThrowOnOffer=false;Deception.PolyModAdapter.NativeLighthouseSecret.Calls.Clear();Deception.PolyModAdapter.NativeLighthouseSecret.LastOffer=null;
   Deception.PolyModAdapter.NativeExchangeCommands.AllowValid=true;Deception.PolyModAdapter.NativeExchangeCommands.ThrowOnCreate=false;
   Deception.PolyModAdapter.NativeExchangeCommands.ThrowOnValid=false;Deception.PolyModAdapter.NativeExchangeCommands.Created.Clear();
   Deception.PolyModAdapter.NativeExchangeCommands.Validated.Clear();Deception.PolyModAdapter.NativeExchangeCommands.Forgotten.Clear();
  }
 }
}
