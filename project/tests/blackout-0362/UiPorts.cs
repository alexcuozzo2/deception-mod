using System;
using System.Collections.Generic;
using System.Linq;
using Deception.PolyModAdapter;
namespace UnityEngine{public class Object:Il2CppSystem.Object{public bool Alive{get;set;}=true;public static implicit operator bool(Object? value)=>value?.Alive==true;}}
public sealed class Unit:UnityEngine.Object{public UnitState UnitState{get;set;}=null!;public bool CanMoveIntoFog{get;set;}}
public sealed class Tile:UnityEngine.Object
{
 public TileData Data{get;set;}=null!;public WorldCoordinates Coordinates=>Data.coordinates;
 // Installed Tile.get_IsHidden queries native exploration, including when no
 // Deception binding exists. A replay can explicitly turn native fog off.
 public bool IsHidden=>!GameManager.ReplayFogDisabled&&GameManager.LocalPlayer is PlayerState p&&!Data.GetExplored(p.Id);
 public int Presses,Releases,Puffs;public bool FogPressed;
 public void OnPress(){if(IsHidden){Presses++;FogPressed=true;}}
 public void OnRelease(){if(IsHidden){Releases++;FogPressed=false;}}
 public void SpawnPuff()=>Puffs++;
}
public static class AudioUtils{public static string GetCloudForCoordinate(WorldCoordinates at)=>"cloud-"+Math.Abs(at.X+at.Y)%3;}
public static class AudioManager
{
 public static readonly List<(string Sound,WorldCoordinates At)> Played=new();
 public static void PlaySFXAtTile(string sound,WorldCoordinates at)=>Played.Add((sound,at));
}
public sealed class GameObject:UnityEngine.Object{public bool activeInHierarchy{get;set;}=true;}
public sealed class ClientBase:Il2CppSystem.Object{public GameState? GameState{get;set;}public bool IsReady{get;set;}=true;}
public sealed class MapRenderer:UnityEngine.Object
{
 public static MapRenderer? Current{get;set;}public bool shouldRefresh{get;set;}
 public GameObject gameObject{get;}=new();public bool IsRendered{get;private set;}
 public bool TileSlotsReady{get;private set;}public bool SkipRender,FailRender;public Action? DuringRender;public int NativeRefreshes;
 // Audited native IsRendered means an allocated nonempty array. RenderMap
 // allocates before filling tile slots; RefreshIfNeeded has no readiness guard.
 public void RenderMap(MapData data)
 {
  NativeBlackoutUi.BeforeRender(this,data,!SkipRender,out var scope);Exception? error=null;
  try{if(!SkipRender){IsRendered=true;TileSlotsReady=false;DuringRender?.Invoke();if(FailRender)throw new InvalidOperationException("native render failed");TileSlotsReady=true;}NativeBlackoutUi.AfterRender(scope,!SkipRender);}
  catch(Exception e){error=e;}
  error=NativeBlackoutUi.RenderFinished(error,scope);if(error is not null)throw error;
 }
 public void RefreshIfNeeded(){if(!shouldRefresh)return;if(!TileSlotsReady)throw new NullReferenceException("MapRendererInner.ReRenderAllTiles: unfilled tile slots");NativeRefreshes++;shouldRefresh=false;}
 public void Clear(){NativeBlackoutUi.RendererClearing(this);IsRendered=false;TileSlotsReady=false;}
 public void OnDestroy(){NativeBlackoutUi.RendererClearing(this);Alive=false;}
}
public sealed class GameManager:UnityEngine.Object
{public static GameManager? instance{get;set;}public static GameState? GameState{get;set;}public static ClientBase? Client{get;set;}public static PlayerState? LocalPlayer{get;set;}public static bool ReplayFogDisabled;public void Update(){}}
public static class FogUiHooks
{
 public static HashSet<string> Installed=new();
 public static void Reset(){NativeBlackoutUi.Clear();var h=new HarmonyLib.Harmony();NativeBlackoutUi.Install(h);Installed=new(h.Hooks);AudioManager.Played.Clear();GameManager.ReplayFogDisabled=false;}
 public static bool Tile(string name,Tile? tile)=>!Installed.Contains("ClientInteraction."+name)||NativeBlackoutUi.BeforeTile(tile);
 public static bool Unit(Unit? unit)=>!Installed.Contains("ClientInteraction.SelectUnit")||NativeBlackoutUi.BeforeUnit(unit);
}
public sealed class ClientInteraction:UnityEngine.Object
{
 public Unit? selectedUnit{get;set;}public Tile? selectedTile{get;set;}public int Clears,MoveAttempts,AcceptedMoves;
 public void OnPress(Tile tile)=>tile.OnPress();
 public void OnRelease(Tile tile,int touchIndex){tile.OnRelease();SelectTile(tile);}
 public void SelectTile(Tile? tile)
 {
  if(!FogUiHooks.Tile(nameof(SelectTile),tile))return;
  if(tile is null){ClearSelection();return;}
  // Native SelectTile's own-unit movement branch precedes its cloud branch.
  // Native command access stays separate, including a stale covered source.
  if(selectedUnit is Unit own&&own.UnitState.owner==GameManager.LocalPlayer!.Id&&own.CanMoveIntoFog)
  {
   MoveAttempts++;var command=new MoveCommand{PlayerId=own.UnitState.owner,From=own.UnitState.coordinates};
   if(NativeBlackoutAccess.Allowed(command,GameManager.GameState!,typeof(MoveCommand)))AcceptedMoves++;
   return;
  }
  selectedTile=null;
  if(tile.IsHidden)
  {
   tile.SpawnPuff();AudioManager.PlaySFXAtTile(AudioUtils.GetCloudForCoordinate(tile.Coordinates),tile.Coordinates);ClearSelection();return;
  }
  if(tile.Data.GetUnit(GameManager.GameState!,GameManager.LocalPlayer!.Id,false) is UnitState body)SelectUnit(new(){UnitState=body});
  else SelectTileInternal(tile);
 }
 public void SelectTileInternal(Tile? tile){if(FogUiHooks.Tile(nameof(SelectTileInternal),tile))selectedTile=tile;}
 public void SelectUnit(Unit? unit){if(FogUiHooks.Unit(unit))selectedUnit=unit;}
 public void UpdateAfterMapRefresh(){}public void ClearSelection(){Clears++;selectedUnit=null;selectedTile=null;}
}
