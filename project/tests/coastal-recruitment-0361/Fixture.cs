using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
using Polytopia.Data;
namespace HarmonyLib {
 public sealed class HarmonyMethod { public HarmonyMethod(MethodInfo method){} }
 public sealed class Harmony { public void Patch(MethodInfo a,HarmonyMethod? b,HarmonyMethod? c,object? d,HarmonyMethod? e,object? f){} }
}
namespace Il2CppSystem.Collections.Generic { public class List<T>:System.Collections.Generic.List<T> {} }
public class Identity { static long Next; public IntPtr Pointer {get;}=new(++Next); }
public struct WorldCoordinates { public int X {get;set;} public int Y {get;set;} public WorldCoordinates(int x,int y){X=x;Y=y;} }
public static class EnumCache<T> where T:struct,Enum { public static T GetType(string id)=>Enum.Parse<T>(id); public static string GetName(T id)=>id.ToString(); }
namespace PolytopiaBackendBase.Common { public enum TribeType {imperius,aquarion,cymanti} }
namespace Polytopia.Data {
 public class UnitData:Identity {
  public enum Type { none,scout,warrior,rider,knight,defender,catapult,archer,mindbender,swordsman,giant,bunny,polytaur,dragonegg,babydragon,firedragon,amphibian,tridention,mooni,battlesled,icefortress,icearcher,crab,gaami,hexapod,doomux,phychi,kiton,exida,centipede,segment,raychi,shaman,dagger,cloak,cloak_boat,pirate,bombership,scoutship,transportship,rammership,juggernaut,mermaidwarrior,mermaidarcher,mermaidswordsman,mermaiddefender,mermaidcloak,mermaiddagger,jelly,shark,siren,aquapult,boomchi,island,ciru,mantis,bugegg,moth,larva,dcp_spy,dcp_mimic,dcp_mimic_form_none,dcp_mimic_form_scout,dcp_mimic_form_warrior,dcp_mimic_form_rider,dcp_mimic_form_knight,dcp_mimic_form_defender,dcp_mimic_form_catapult,dcp_mimic_form_archer,dcp_mimic_form_mindbender,dcp_mimic_form_swordsman,dcp_mimic_form_giant,dcp_mimic_form_bunny,dcp_mimic_form_polytaur,dcp_mimic_form_dragonegg,dcp_mimic_form_babydragon,dcp_mimic_form_firedragon,dcp_mimic_form_amphibian,dcp_mimic_form_tridention,dcp_mimic_form_mooni,dcp_mimic_form_battlesled,dcp_mimic_form_icefortress,dcp_mimic_form_icearcher,dcp_mimic_form_crab,dcp_mimic_form_gaami,dcp_mimic_form_hexapod,dcp_mimic_form_doomux,dcp_mimic_form_phychi,dcp_mimic_form_kiton,dcp_mimic_form_exida,dcp_mimic_form_centipede,dcp_mimic_form_segment,dcp_mimic_form_raychi,dcp_mimic_form_shaman,dcp_mimic_form_dagger,dcp_mimic_form_cloak,dcp_mimic_form_cloak_boat,dcp_mimic_form_pirate,dcp_mimic_form_bombership,dcp_mimic_form_scoutship,dcp_mimic_form_transportship,dcp_mimic_form_rammership,dcp_mimic_form_juggernaut,dcp_mimic_form_mermaidwarrior,dcp_mimic_form_mermaidarcher,dcp_mimic_form_mermaidswordsman,dcp_mimic_form_mermaiddefender,dcp_mimic_form_mermaidcloak,dcp_mimic_form_mermaiddagger,dcp_mimic_form_jelly,dcp_mimic_form_shark,dcp_mimic_form_siren,dcp_mimic_form_aquapult,dcp_mimic_form_boomchi,dcp_mimic_form_island,dcp_mimic_form_ciru,dcp_mimic_form_mantis,dcp_mimic_form_bugegg,dcp_mimic_form_moth,dcp_mimic_form_larva,dcp_mimic_form_dcp_spy,dcp_mermimic,dcp_spymaid,dcp_mimic_form_dcp_spymaid,dcp_motherfogger,dcp_mimic_form_dcp_motherfogger }
  public Type type {get;set;} public string[] movementTerrain {get;set;}=Array.Empty<string>();
 }
 public class TechData { public enum Type {dcp_deception,dcp_propaganda} }
 public class TribeData:Identity { public PolytopiaBackendBase.Common.TribeType type {get;set;} }
 public class GameLogicData:Identity {
  public readonly Dictionary<UnitData.Type,UnitData> Data=new();
  public UnitData GetUnitData(UnitData.Type type)=>Data[type];
  public Il2CppSystem.Collections.Generic.List<UnitData> GetUnlockedUnits(PlayerState player,GameState state,bool upgrades) {
   var result=new Il2CppSystem.Collections.Generic.List<UnitData>();foreach(var id in player.Unlocked)result.Add(GetUnitData(Enum.Parse<UnitData.Type>(id)));return result;
  }
  public TribeData GetTribeData(PolytopiaBackendBase.Common.TribeType tribe)=>new(){type=tribe};
  public UnitData GetOverride(UnitData data,TribeData tribe)=>data;
 }
}
public class PlayerState:Identity {
 public byte Id {get;set;}=1;public int Currency {get;set;}=3;public PolytopiaBackendBase.Common.TribeType tribe {get;set;}
 public bool Deception=true,Propaganda;public HashSet<string> Unlocked=new();
}
public class GameState:Identity {
 public bool SessionReady=true;
 public GameLogicData GameLogicData {get;set;}=new();public PlayerState Player {get;set;}=new();
 public Dictionary<(int,int),string[]> Neighbors=new(); public bool CityOwned=true,Occupied,Support=true;public int ValidationCalls;
 public bool TryGetPlayer(byte id,out PlayerState? player){player=id==Player.Id?Player:null;return player!=null;}
}
public static class PlayerExtensions { public static bool HasTech(PlayerState player,TechData.Type tech)=>tech==TechData.Type.dcp_deception?player.Deception:player.Propaganda; }
public static class GameManager { public static GameState? GameState {get;set;} }
public static class CommandValidation {
 public static int Calls;public static UnitData? LastData;public static WorldCoordinates LastCity;public static bool Fail;
 public static bool HasUnitTerrain(GameState state,WorldCoordinates city,UnitData data){
  Calls++;LastData=data;LastCity=city;if(Fail)throw new InvalidOperationException("Synthetic native terrain failure");
  return data.movementTerrain.Length==0||(state.Neighbors.TryGetValue((city.X,city.Y),out var neighbors)&&neighbors.Intersect(data.movementTerrain).Any());
 }
}
public class TrainCommand:Identity {
 public byte PlayerId {get;set;} public UnitData.Type Type {get;set;} public WorldCoordinates Coordinates {get;set;}
 public TrainCommand(byte p,UnitData.Type t,WorldCoordinates c){PlayerId=p;Type=t;Coordinates=c;}
 public bool IsValid(GameState state,out string reason){state.ValidationCalls++;reason="native gate";if(Type.ToString().StartsWith(RecruitmentCatalog.Prefix)&&!Deception.PolyModAdapter.RecruitmentHooks.CheckEligibility(state,this,out reason))return false;return state.CityOwned&&!state.Occupied&&state.Support&&state.Player.Currency>=(RecruitmentCatalog.IsMimicId(Type.ToString())?RecruitmentCatalog.Cost:3);}
}
public class TrainAction:TrainCommand {public bool trainActionAborted {get;set;} public int Cost {get;set;}=RecruitmentCatalog.Cost;public TrainAction(byte p,UnitData.Type t,WorldCoordinates c):base(p,t,c){} }
namespace Deception.PolyModAdapter {
 internal static class NativeRegroupSession {public static bool Enabled=true;public static bool Ready(object state)=>((GameState)state).SessionReady;}
 internal static class Entry {public static void Note(string kind,string message){} }
 internal static class RecruitmentWheelUi {public static void Clear(){} public static void Install(HarmonyLib.Harmony h){} }
 internal static class MimicPresentationState {public static void Clear(){} }
 internal static class MimicPresentationUi {public static void Clear(){} }
 internal static class MimicControls {public static void Clear(){} }
 internal static class OrdinaryActionEligibility {public static void Clear(){} public static bool AllowsNativeReadiness(object u)=>false;}
 internal static class NativeMimicTransport {public static bool CanHandleMovement(object s,object u,object t)=>false;}
 internal static class NativeExecutionTrigger {public static bool Requested;}
 internal static class NativeTrainLeafProbeTrigger {public static bool Requested;}
 internal static class NativeFreshSession {public static bool Block;public static bool BlockMimicRecruitment(object state)=>Block;}
 internal static class NativeExecutionProbe {public static object? PaidUnlockState(string id,object player)=>null;public static bool AllowPaidEligibility(object s,object c)=>false;}
}
