// Synthetic native surface. Actual production Context and FogSource are invoked;
// no Unity rendering, native hook, event, game object, or IL2CPP call is made.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.IO;
using Deception.Core;
using Deception.PolyModAdapter;
public class Identity {static long Next=100;public IntPtr Pointer{get;}=new(++Next);public T Cast<T>() where T:class=>(T)(object)this;}
namespace UnityEngine
{
 public class Object:Identity {public bool Destroyed;public static implicit operator bool(Object? o)=>o is not null&&!o.Destroyed;}
 public sealed class Sprite:Object {}
 public class Renderer:Object {public int sortingLayerID{get;set;}public int sortingOrder{get;set;}public bool Active,Enabled;}
 public sealed class SpriteRenderer:Renderer {public Sprite? sprite{get;set;}}
}
public sealed class PolytopiaSpriteRenderer:UnityEngine.Object
{
 public UnityEngine.Sprite? Sprite{get;set;}public int SortingLayer{get;set;}public int SortingOrder{get;set;}
 public UnityEngine.SpriteRenderer? Raw;public bool ThrowRawRead,Active;public int InitCalls;
 public UnityEngine.SpriteRenderer? spriteRenderer=>ThrowRawRead?throw new Exception("Raw renderer must not be read for cached visual."):Raw;
 public void Init(){InitCalls++;throw new Exception("Native Init is not a read-only visual query.");}
}
namespace Polytopia.Data {public static class TechData {public enum Type{dcp_intelligence}}public static class ImprovementData {public enum Type{city}}}
public static class EnumCache<T> where T:struct,Enum {public static T GetType(string name)=>Enum.Parse<T>(name);public static string GetName(T value)=>value.ToString();}
public sealed class WorldCoordinates {public int X{get;set;}public int Y{get;set;}}
public sealed class PlayerState:Identity {public byte Id{get;set;}public bool Intelligence=true;}
public static class PlayerExtensions {public static bool HasTech(PlayerState p,Polytopia.Data.TechData.Type t)=>p.Intelligence;}
public sealed class UnitState:Identity {public uint id{get;set;}=9;public byte owner{get;set;}=2;public WorldCoordinates coordinates{get;set;}=new(){X=2,Y=3};public int Health=50;public bool Moved,Attacked;}
public sealed class Unit:UnityEngine.Object {public UnitState UnitState{get;set;}=new();public bool Visible=true,Invisible;public bool GetVisible()=>Visible;public bool IsInvisibleForLocalPlayer()=>Invisible;}
public sealed class TileData:Identity
{
 public WorldCoordinates coordinates{get;set;}=new();public UnitState? unit{get;set;}public byte owner{get;set;}=2;public bool City;
 public HashSet<byte> Explorers=new(){1};public bool GetExplored(byte player)=>Explorers.Contains(player);
 public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>City;
}
public sealed class Tile:UnityEngine.Object {public TileData Data{get;set;}=new();public Unit? unit{get;set;}public bool IsHidden{get;set;}}
public sealed class MapData:Identity {public Dictionary<(int,int),TileData> Tiles=new();}
public static class MapDataExtensions {public static TileData? GetTile(MapData m,WorldCoordinates c)=>m.Tiles.GetValueOrDefault((c.X,c.Y));}
public sealed class MapRenderer:UnityEngine.Object
{
 public static MapRenderer? Current{get;set;}public Dictionary<(int,int),Tile> Tiles=new();public Dictionary<uint,Unit> Units=new();
 public Tile? GetTileInstance(WorldCoordinates c)=>Tiles.GetValueOrDefault((c.X,c.Y));public Unit? GetUnitInstance(uint id)=>Units.GetValueOrDefault(id);
}
public sealed class GameState:Identity
{
 public MapData Map{get;set;}=new();public Dictionary<uint,UnitState> Units=new();public Dictionary<byte,PlayerState> Players=new();
 public bool TryGetUnit(uint id,out UnitState? u)=>Units.TryGetValue(id,out u);public bool TryGetPlayer(byte id,out PlayerState? p)=>Players.TryGetValue(id,out p);
}
public sealed class ClientBase:Identity {public bool IsReplay{get;set;}public bool IsSpectating{get;set;}public bool IsReady{get;set;}=true;public GameState GameState{get;set;}=new();}
public static class GameManager
{
 public static ClientBase Client{get;set;}=new();public static GameState GameState{get;set;}=new();public static PlayerState LocalPlayer{get;set;}=new(){Id=1};
 public static bool Viewing=true;public static bool IsPlayerViewing(byte id)=>Viewing&&LocalPlayer.Id==id;
}
public static class PopupManager {public static bool PopupShowing{get;set;}}
public sealed class ClientInteraction:Identity {public Unit? selectedUnit{get;set;}public Tile? selectedTile{get;set;}}
namespace Deception.PolyModAdapter
{
 internal static class Entry {internal static readonly List<(string Kind,string Message)> Notes=new();internal static void Note(string k,string m)=>Notes.Add((k,m));}
 internal static class RecruitmentHooks {internal static bool Registered=true;internal const string TT="Polytopia.Data.TechData+Type",GLD="GameLogicData";}
 internal static class NativeLedgerOwnership
 {
  internal sealed class WeakIdentity
  {readonly WeakReference<object> Value;internal WeakIdentity(object o){Value=new(o);}internal object? Target()=>Value.TryGetTarget(out var o)?o:null;}
 }
}
namespace Deception.Tests
{
 public static class IntelligenceSelectionTests
 {
  static readonly Type Overlay=typeof(NativeIntelligenceOverlay);static readonly BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
  static readonly List<object> Results=new();static int Failed;
  sealed class Case
  {
   internal readonly GameState State=new();internal readonly MapRenderer Map=new();internal readonly Unit Unit=new();internal readonly Tile Tile=new();internal readonly ClientInteraction Interaction=new();
   internal readonly PlayerState Viewer=new(){Id=1},Enemy=new(){Id=2};internal readonly ClientBase Client=new();
   internal Case()
   {
    NativeIntelligenceOverlay.Clear();Entry.Notes.Clear();GameManager.Viewing=true;RecruitmentHooks.Registered=true;PopupManager.PopupShowing=false;
    State.Players[1]=Viewer;State.Players[2]=Enemy;State.Units[Unit.UnitState.id]=Unit.UnitState;
    Tile.Data.coordinates=Unit.UnitState.coordinates;Tile.Data.unit=Unit.UnitState;Tile.unit=Unit;
    State.Map.Tiles[(2,3)]=Tile.Data;Map.Tiles[(2,3)]=Tile;Map.Units[Unit.UnitState.id]=Unit;Interaction.selectedUnit=Unit;
    Client.GameState=State;GameManager.GameState=State;GameManager.Client=Client;GameManager.LocalPlayer=Viewer;MapRenderer.Current=Map;
    Arm();
   }
   internal void Arm(){Overlay.GetField("Selection",Hidden)!.SetValue(null,new NativeLedgerOwnership.WeakIdentity(Interaction));Overlay.GetField("Client",Hidden)!.SetValue(null,new NativeLedgerOwnership.WeakIdentity(Client));}
   internal string Snapshot()=>JsonSerializer.Serialize(new{unit=Unit.UnitState.Pointer.ToInt64(),Unit.UnitState.owner,Unit.UnitState.Health,Unit.UnitState.Moved,Unit.UnitState.Attacked,
    x=Unit.UnitState.coordinates.X,y=Unit.UnitState.coordinates.Y,tile=Tile.Data.Pointer.ToInt64(),occupant=Tile.Data.unit?.Pointer.ToInt64(),tileOwner=Tile.Data.owner,
    explored=Tile.Data.Explorers.OrderBy(x=>x).ToArray(),Tile.IsHidden,selectedTile=Interaction.selectedTile?.Pointer.ToInt64(),selectedUnit=Interaction.selectedUnit?.Pointer.ToInt64()});
   internal bool Context(out object?[] args)
   {args=new object?[]{null,null,null,(byte)0,(byte)0};string before=Snapshot();bool r=(bool)Overlay.GetMethod("Context",Hidden)!.Invoke(null,args)!;Check(Snapshot()==before,"Context mutated native-owned data");return r;}
   internal bool Context()=>Context(out _);
   internal void City(){Interaction.selectedUnit=null;Interaction.selectedTile=Tile;Tile.Data.City=true;}
  }
  static void Check(bool yes,string why="Selection invariant failed"){if(!yes)throw new Exception(why);}
  static void Test(string id,Action body)
  {try{body();NativeIntelligenceOverlay.Clear();Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e);}}
  static string FogSnapshot(PolytopiaSpriteRenderer fog)=>JsonSerializer.Serialize(new
  {
   fog.Destroyed,fog.Active,fog.InitCalls,fog.ThrowRawRead,fog.SortingLayer,fog.SortingOrder,
   cached=fog.Sprite?.Pointer.ToInt64(),cachedDead=fog.Sprite?.Destroyed,raw=fog.Raw?.Pointer.ToInt64(),rawDead=fog.Raw?.Destroyed,
   rawActive=fog.Raw?.Active,rawEnabled=fog.Raw?.Enabled,rawLayer=fog.Raw?.sortingLayerID,rawOrder=fog.Raw?.sortingOrder,
   rawSprite=fog.Raw?.sprite?.Pointer.ToInt64(),rawSpriteDead=fog.Raw?.sprite?.Destroyed
  });
  static (object Sprite,int Layer,int Order) Fog(PolytopiaSpriteRenderer fog)
  {
   string before=FogSnapshot(fog);
   try{return((object,int,int))Overlay.GetMethod("FogSource",Hidden)!.Invoke(null,new object[]{fog})!;}
   finally{Check(FogSnapshot(fog)==before,"Fog query mutated native renderer, activity, sorting or sprite state.");}
  }
  static void RejectFog(PolytopiaSpriteRenderer fog,string reason)
  {
   try{_=Fog(fog);}catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException actual&&actual.Message==reason){return;}
   throw new Exception("Missing exact fog rejection: "+reason);
  }
  public static int Run()
  {
   Test("intelligence-native-unit-selection-null-selectedTile",()=>{var c=new Case();Check(c.Interaction.selectedTile is null);Check(c.Context(out var a));Check(ReferenceEquals(a[0],c.State)&&ReferenceEquals(a[1],c.Map)&&ReferenceEquals(a[2],c.Enemy)&&(byte)a[3]! ==1&&(byte)a[4]! ==2);});
   Test("intelligence-unit-ignores-unrelated-selectedTile",()=>{var c=new Case();c.Interaction.selectedTile=new(){Data=new(){City=true,owner=3}};Check(c.Context(out var a)&&(byte)a[4]! ==2);});
   Test("intelligence-city-keeps-selectedTile-path",()=>{var c=new Case();c.City();Check(c.Context(out var a)&&(byte)a[4]! ==2);});
   Test("intelligence-city-requires-authoritative-city",()=>{var c=new Case();c.City();c.Tile.Data.City=false;Check(!c.Context());c.Tile.Data.City=true;c.Interaction.selectedTile=new(){Data=c.Tile.Data};Check(!c.Context());});
   Test("intelligence-stale-moved-unit-rejects",()=>{var c=new Case();c.Unit.UnitState.coordinates=new(){X=4,Y=3};Check(!c.Context());});
   Test("intelligence-consistent-current-movement-follows-unit",()=>{var c=new Case();var at=new WorldCoordinates{X=4,Y=3};c.Unit.UnitState.coordinates=at;var next=new Tile{Data=new(){coordinates=at,unit=c.Unit.UnitState},unit=c.Unit};c.Tile.Data.unit=null;c.Tile.unit=null;c.State.Map.Tiles[(4,3)]=next.Data;c.Map.Tiles[(4,3)]=next;Check(c.Context());});
   Test("intelligence-native-registry-replacement-rejects",()=>{var c=new Case();c.State.Units[c.Unit.UnitState.id]=new(){id=c.Unit.UnitState.id};Check(!c.Context());});
   Test("intelligence-renderer-registry-replacement-rejects",()=>{var c=new Case();c.Map.Units[c.Unit.UnitState.id]=new(){UnitState=c.Unit.UnitState};Check(!c.Context());});
   Test("intelligence-authoritative-occupant-replacement-rejects",()=>{var c=new Case();c.Tile.Data.unit=new(){id=9};Check(!c.Context());});
   Test("intelligence-rendered-occupant-replacement-rejects",()=>{var c=new Case();c.Tile.unit=new(){UnitState=c.Unit.UnitState};Check(!c.Context());});
   Test("intelligence-rendered-tile-data-alias-rejects",()=>{var c=new Case();c.Map.Tiles[(2,3)]=new(){unit=c.Unit,Data=new(){coordinates=c.Unit.UnitState.coordinates,unit=c.Unit.UnitState}};Check(!c.Context());});
   Test("intelligence-hidden-and-unexplored-selection-reject",()=>{var c=new Case();c.Tile.IsHidden=true;Check(!c.Context());c.Tile.IsHidden=false;c.Tile.Data.Explorers.Clear();Check(!c.Context());c.City();Check(!c.Context());});
   Test("intelligence-invisible-unit-does-not-use-city-fallback",()=>{var c=new Case();c.Interaction.selectedTile=c.Tile;c.Tile.Data.City=true;c.Unit.Invisible=true;Check(!c.Context());c.Unit.Invisible=false;c.Unit.Visible=false;Check(!c.Context());c.Unit.Visible=true;c.Unit.Destroyed=true;Check(!c.Context());});
   Test("intelligence-subject-affiliation-is-native-unit-owner",()=>{var c=new Case();c.Tile.Data.owner=1;Check(c.Context(out var a)&&(byte)a[4]! ==2);foreach(byte id in new byte[]{0,1,255}){c.Unit.UnitState.owner=id;Check(!c.Context());}});
   Test("intelligence-preserves-view-research-popup-guards",()=>{var c=new Case();c.Viewer.Intelligence=false;Check(!c.Context());c.Viewer.Intelligence=true;GameManager.Viewing=false;Check(!c.Context());GameManager.Viewing=true;PopupManager.PopupShowing=true;Check(!c.Context());PopupManager.PopupShowing=false;c.Client.IsSpectating=true;Check(!c.Context());c.Client.IsSpectating=false;c.Client.IsReplay=true;Check(!c.Context());});
   Test("intelligence-stale-client-state-and-map-reject",()=>{var c=new Case();c.Client.GameState=new();Check(!c.Context());c.Client.GameState=c.State;GameManager.Client=new(){GameState=c.State};Check(!c.Context());GameManager.Client=c.Client;MapRenderer.Current=new();Check(!c.Context());});
   Test("intelligence-clear-and-duplication-suppress-context",()=>{var c=new Case();NativeIntelligenceOverlay.BeforeDuplicate();Check(!c.Context());NativeIntelligenceOverlay.AfterDuplicate(null);Check(c.Context());NativeIntelligenceOverlay.Clear();Check(!c.Context());});
   Test("intelligence-fixed-wait-reason-does-not-spam",()=>{var c=new Case();((HashSet<string>)Overlay.GetField("WaitReasons",Hidden)!.GetValue(null)!).Clear();c.Unit.Visible=false;for(int i=0;i<100;i++)Check(!c.Context());Check(Entry.Notes.Count(n=>n.Kind=="intelligence-overlay-wait")==1);});
   Test("intelligence-fog-cached-visual-does-not-read-raw",()=>{var sprite=new UnityEngine.Sprite();var fog=new PolytopiaSpriteRenderer{Sprite=sprite,SortingLayer=-43,SortingOrder=192,ThrowRawRead=true};var v=Fog(fog);Check(ReferenceEquals(v.Sprite,sprite)&&v.Layer==-43&&v.Order==192);});
   Test("intelligence-fog-inactive-uninitialized-raw-visual",()=>{var sprite=new UnityEngine.Sprite();var raw=new UnityEngine.SpriteRenderer{sprite=sprite,sortingLayerID=-12,sortingOrder=147};var fog=new PolytopiaSpriteRenderer{Raw=raw,SortingLayer=0,SortingOrder=0};var v=Fog(fog);Check(ReferenceEquals(v.Sprite,sprite)&&v.Layer==-12&&v.Order==147&&!fog.Active&&!raw.Active&&!raw.Enabled&&fog.InitCalls==0);});
   Test("intelligence-fog-destroyed-cache-uses-live-raw",()=>{var sprite=new UnityEngine.Sprite();var fog=new PolytopiaSpriteRenderer{Sprite=new(){Destroyed=true},SortingLayer=99,SortingOrder=99,Raw=new(){sprite=sprite,sortingLayerID=3,sortingOrder=7}};var v=Fog(fog);Check(ReferenceEquals(v.Sprite,sprite)&&v.Layer==3&&v.Order==7);});
   Test("intelligence-fog-cache-survives-native-raw-retirement",()=>{var sprite=new UnityEngine.Sprite();var fog=new PolytopiaSpriteRenderer{Sprite=sprite,SortingLayer=8,SortingOrder=17,Raw=new(){Destroyed=true}};var v=Fog(fog);Check(ReferenceEquals(v.Sprite,sprite)&&v.Layer==8&&v.Order==17);});
   Test("intelligence-fog-missing-both-sources-rejects",()=>RejectFog(new(),"Native fog has no cached sprite or live source renderer."));
   Test("intelligence-fog-destroyed-raw-rejects",()=>RejectFog(new(){Raw=new(){Destroyed=true,sprite=new()}},"Native fog has no cached sprite or live source renderer."));
   Test("intelligence-fog-missing-or-destroyed-raw-sprite-rejects",()=>{var fog=new PolytopiaSpriteRenderer{Raw=new()};RejectFog(fog,"Native fog source sprite is unavailable.");fog.Raw.sprite=new(){Destroyed=true};RejectFog(fog,"Native fog source sprite is unavailable.");});
   Test("intelligence-fog-destroyed-native-wrapper-rejects",()=>RejectFog(new(){Destroyed=true,Sprite=new(),Raw=new(){sprite=new()}},"Native fog sprite renderer is unavailable."));
   File.WriteAllText(Environment.GetEnvironmentVariable("DCP_INTELLIGENCE_SELECTION_RESULT")!,JsonSerializer.Serialize(new{passed=Results.Count-Failed,failed=Failed,results=Results},new JsonSerializerOptions{WriteIndented=true}));return Failed==0?0:1;
  }
 }
}
