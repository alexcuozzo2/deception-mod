using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
using Deception.PolyModAdapter;
namespace HarmonyLib
{
 public sealed class HarmonyMethod {public MethodInfo Method;public HarmonyMethod(MethodInfo m){Method=m;}}
 public sealed class Harmony {public readonly List<MethodInfo> Targets=new();public void Patch(MethodInfo m,HarmonyMethod? p,HarmonyMethod? q,object? a,HarmonyMethod? f,object? b){Targets.Add(m);}}
}
namespace Il2CppSystem {public class Object{static long Next;public IntPtr Pointer{get;}=new(++Next);}}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays {public sealed class Il2CppReferenceArray<T>:Il2CppSystem.Object{}}
namespace PolytopiaBackendBase.Common {public enum SkinType{Default,Special}public enum TribeType{Imperius,Cymanti,Aquarion}}
namespace Polytopia.Data
{
 public sealed class UnitData:Il2CppSystem.Object
 {
  public enum Type{warrior,cloak,transportship,dcp_mimic,dcp_mermimic,dcp_mimic_form_cloak,dcp_mimic_form_transportship,dcp_mimic_form_dcp_spy,dcp_spy,dcp_spymaid}
  public Type type{get;set;}public string? OverrideName;
  public string displayName=>OverrideName??"unit.names."+type;
 }
}
public static class EnumCache<T> where T:struct,Enum {public static string GetName(T t)=>t.ToString();public static T GetType(string name)=>Enum.Parse<T>(name,true);}
public sealed class WorldCoordinates:Il2CppSystem.Object{public int X{get;set;}=2;public int Y{get;set;}=3;}
public sealed class UnitState:Il2CppSystem.Object
{
 public uint id{get;set;}=17;public byte owner{get;set;}=1;public int health{get;set;}=10;
 public bool moved{get;set;}public bool attacked{get;set;}public int investment{get;set;}=9;
 public Polytopia.Data.UnitData.Type type{get=>UnitData.type;set=>UnitData.type=value;}
 public Polytopia.Data.UnitData UnitData{get;set;}=new();public WorldCoordinates coordinates{get;set;}=new();
 public UnitState? passengerUnit{get;set;}
}
public sealed class TileData:Il2CppSystem.Object{public UnitState? unit{get;set;}public bool Explored=true;}
public sealed class MapData:Il2CppSystem.Object{public TileData Tile=new();public WorldCoordinates At=new();}
public static class MapDataExtensions{public static TileData? GetTile(MapData m,WorldCoordinates p)=>p.X==m.At.X&&p.Y==m.At.Y?m.Tile:null;}
public sealed class PlayerState:Il2CppSystem.Object
{
 public byte Id{get;set;}=1;public PolytopiaBackendBase.Common.TribeType tribe{get;set;}=PolytopiaBackendBase.Common.TribeType.Imperius;
 public PolytopiaBackendBase.Common.SkinType Skin;public PolytopiaBackendBase.Common.SkinType GetVisualSkin()=>Skin;
}
public sealed class GameState:Il2CppSystem.Object
{
 public MapData Map{get;set;}=new();public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=4;
 public bool Started=true,FreeSpirit=true;public int Commands,Actions,Stars=21;public LedgerUnit? Identity;
}
public sealed class ClientBase:Il2CppSystem.Object{public bool IsReplay{get;set;}public bool IsSpectating{get;set;}}
public static class GameManager
{
 public static GameState? GameState{get;set;}public static PlayerState? LocalPlayer{get;set;}public static ClientBase? Client{get;set;}
 public static bool Viewing=true;public static bool IsPlayerViewing(byte p)=>Viewing&&LocalPlayer?.Id==p;
}
public class CommandBase:Il2CppSystem.Object {public byte PlayerId{get;set;}=1;}
public sealed class DisbandCommand:CommandBase{public WorldCoordinates Coordinates{get;set;}=new();}
public sealed class CommandProxy:CommandBase
{
 public DisbandCommand Value=new();public new IntPtr Pointer=>Value.Pointer;
 public T Cast<T>() where T:class=>Value as T??throw new InvalidCastException();
}
public static class Localization
{
 public static string Locale="en";
 public static string GetSkinned(PolytopiaBackendBase.Common.SkinType skin,PolytopiaBackendBase.Common.TribeType tribe,string term,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>? args)
 {
  NativeDisbandPresentation.NameTerm(skin,tribe,ref term);
  string? label=term switch{"unit.names.dcp_mimic"=>"Mimic","unit.names.dcp_mermimic"=>"Mermimic","unit.names.dcp_spy"=>"Spy","unit.names.dcp_spymaid"=>"Spymaid","unit.names.warrior"=>"Warrior","unit.names.cloak"=>"Cloak","unit.names.transportship"=>"Raft","action.disband"=>"Disband ",_=>null};
  return label is null?term:Locale=="en"?label:"localized:"+label;
 }
}
// Native-shaped formatter port, transcribed from audited GetShortInfo's
// Disband branch. It supplies the native prefix and localization independently
// of the production patch. This fixture does not execute a game popup or GPU.
public static class CommandExtensions
{
 public static Action? BeforeName;public static bool Throw;
 public static string GetShortInfo(CommandBase command,PolytopiaBackendBase.Common.SkinType skin)
 {
  NativeDisbandPresentation.Begin(command,out var frame);Exception? error=null;
  try
  {
   BeforeName?.Invoke();if(Throw)throw new InvalidOperationException("native formatter failure");
   var disband=command as DisbandCommand??(command as CommandProxy)?.Value;
   if(disband is null)return "native other action";
   var state=GameManager.GameState!;var unit=MapDataExtensions.GetTile(state.Map,disband.Coordinates)!.unit!;
   var owner=GameManager.LocalPlayer!;
   string name=Localization.GetSkinned(owner.GetVisualSkin(),owner.tribe,unit.UnitData.displayName,null);
   return Localization.GetSkinned(skin,default,"action.disband",null)+name;
  }
  catch(Exception e){error=e;throw;}
  finally{NativeDisbandPresentation.Finished(error,frame);}
 }
}
namespace Deception.PolyModAdapter
{
 internal static class Entry{internal static List<string> Notes=new();internal static void Note(string a,string b)=>Notes.Add(a+":"+b);}
 internal static class NativeRegroupSession
 {internal static bool Enabled=true;internal static LedgerUnit? ReadIdentity(object s,uint id)=>Enabled&&((GameState)s).Identity?.NativeId==id?((GameState)s).Identity:null;}
 // Existing ordinary-action authorization is independently covered by the
 // real-source ordinary-actions-0377 suite; this is its read-only port here.
 internal static class NativeOrdinaryCommands
 {
  internal static bool CanDisband(object state,object body,byte actor)
  {
   var s=(GameState)state;var u=(UnitState)body;
   return NativeRegroupSession.Enabled&&s.Started&&s.FreeSpirit&&s.CurrentPlayer==actor&&u.owner==actor&&u.health>0&&
    !u.moved&&!u.attacked&&s.Map.Tile.unit==u&&s.Map.Tile.Explored&&s.Identity?.Rogue is null;
  }
 }
}
