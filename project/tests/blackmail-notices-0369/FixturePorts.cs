// Exact managed signatures, synthetic native objects. Production NativeApi,
// Blackmail planning/commit/event adapter and persistence codecs are compiled.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
namespace HarmonyLib { public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo m){}}public sealed class Harmony{public List<string> Hooks=new();public void Patch(MethodInfo m,HarmonyMethod? prefix,HarmonyMethod? postfix,object? transpiler,HarmonyMethod? finalizer,object? il=null)=>Hooks.Add(m.DeclaringType!.Name+"."+m.Name);}}
namespace Il2CppSystem {public class Object{static long Next;public IntPtr Pointer{get;}public Object(){Pointer=new(++Next);}protected Object(IntPtr pointer){Pointer=pointer;}}}
public struct WorldCoordinates{public int X{get;set;}public int Y{get;set;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
namespace Polytopia.Data {public sealed class ImprovementData:Il2CppSystem.Object{public enum Type{city,farm}public Type type{get;set;}=Type.city;}public sealed class TechData{public enum Type{dcp_espionage}}}
public static class EnumCache<T>where T:struct,Enum{public static string GetName(T v)=>v.ToString();public static T GetType(string s)=>Enum.Parse<T>(s);}
public sealed class PlayerState:Il2CppSystem.Object
{
 public byte Id{get;set;}public bool Alive{get;set;}=true;public bool Espionage{get;set;}=true;
 sealed class Bank{internal int Stars=20;}Bank Vault=new();public PlayerState(){}PlayerState(PlayerState p):base(p.Pointer){Id=p.Id;Alive=p.Alive;Espionage=p.Espionage;Vault=p.Vault;}public PlayerState Wrapper()=>new(this);
 public int Currency{get{Fault.Check("get-currency");return Vault.Stars;}set{Vault.Stars=value;Fault.Check("currency"+Id);}}
}
public static class PlayerExtensions
{
 public static bool IsAlive(PlayerState p,GameState s)=>p.Alive;
 public static bool HasTech(PlayerState p,Polytopia.Data.TechData.Type tech)=>p.Espionage;
}
public sealed class UnitState:Il2CppSystem.Object
{
 public uint id{get;set;}public byte owner{get;set;}=2;public WorldCoordinates home{get;set;}public WorldCoordinates coordinates{get;set;}
 public UnitState? passengerUnit{get;set;}public bool Hidden;public ushort health{get;set;}=100;
 public bool HasLeader()=>false;public bool HasFollower()=>false;
}
public sealed class TileData:Il2CppSystem.Object
{
 public ImprovementState improvement{get;set;}=new();public int Income=5;public WorldCoordinates coordinates{get;set;}public byte owner{get;set;}=2;public bool City{get;set;}=true;public HashSet<byte> Visible=new(){1,2,3,4};
 public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>City;
 public bool GetExplored(byte p)=>Visible.Contains(p);
 public UnitState? unit{get;set;}
 public UnitState? GetUnit(GameState s,byte actor,bool includeInvisible)=>GetExplored(actor)&&unit is not null&&(!unit.Hidden||includeInvisible||unit.owner==actor)?unit:null;
}
public static class TileDataExtensions{public static int CalculateWork(TileData t,GameState s)=>t.Income;}
public sealed class MapData:Il2CppSystem.Object{public List<TileData> Tiles=new();}
public static class MapDataExtensions{public static TileData? GetTile(MapData m,WorldCoordinates at)=>m.Tiles.SingleOrDefault(t=>t.coordinates.X==at.X&&t.coordinates.Y==at.Y);}
public enum CommandType{Build=1,EndTurn=15,Resign=35}
public class ActionBase:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public bool Valid=true;public virtual bool IsValid(GameState s)=>Valid;}
public sealed class EndCommandAction:ActionBase{public CommandType commandType{get;set;}=CommandType.EndTurn;public void Execute(GameState s){}}
public sealed class CaptureCityAction:ActionBase{public void Execute(GameState s){}}
public sealed class DestroyImprovementAction:ActionBase{public void Execute(GameState s){}}
public sealed class WipePlayerAction:ActionBase{public void Execute(GameState s){}}
public sealed class IncreaseCurrencyAction:ActionBase{public int Amount;public void Execute(GameState s){s.TryGetPlayer(PlayerId,out var p);p!.Currency+=Amount;}}
public sealed class GameState:Il2CppSystem.Object
{
 public enum State{Started,Ended}public State CurrentState{get;set;}=State.Started;
 public uint CurrentTurn{get;set;}=4;public byte CurrentPlayer{get;set;}=1;
 public MapData Map{get;set;}=new();public List<PlayerState> Players=new();public List<ActionBase> ActionStack{get;set;}=new();
 public bool TryGetUnit(uint id,out UnitState? unit){unit=Map.Tiles.Select(t=>t.unit).Where(u=>u is not null).SelectMany(u=>u!.passengerUnit is null?new[]{u}:new[]{u,u.passengerUnit}).SingleOrDefault(u=>u!.id==id);return unit is not null;}
 public bool Rewrap;public bool TryGetPlayer(byte id,out PlayerState? p){p=Players.SingleOrDefault(p=>p.Id==id);if(Rewrap&&p is not null)p=p.Wrapper();return p is not null;}
 public bool UnitMoved,UnitAttacked,CityExhausted;
}
public static class Fault{public static string At="";public static void Check(string point){if(At==point){At="";throw new System.IO.IOException("Injected "+point);}}}
namespace Deception.PolyModAdapter
{
 internal static class Entry{internal static List<string> Errors=new();internal static void Note(string kind,string why)=>Errors.Add(why);}
 internal static class RecruitmentHooks{internal const string TT="Polytopia.Data.TechData+Type";internal static bool Registered=true;}
 internal static class NativeRegroupSession
 {internal static bool Enabled=true;internal static HashSet<object> Owned=new();internal static bool Ready(object s)=>Enabled&&Owned.Contains(s);internal static List<string> Faults=new();internal static void Fault(string why)=>Faults.Add(why);internal static void Prepare(object s){}}
 internal static class NativeExchangeCommands{internal static object? Executing;internal static bool LiveExecution(object s)=>ReferenceEquals(Executing,s);}
 internal static class NativeGameStateEnvelope
 {
  internal sealed class Snapshot{internal long Revision{get;}readonly byte[] Data;internal Snapshot(long rev,byte[] data){Revision=rev;Data=(byte[])data.Clone();}internal int Reads;internal byte[] Bytes(){Reads++;return(byte[])Data.Clone();}}
  internal static Dictionary<object,Snapshot> Values=new();internal static Snapshot? Get(object s)=>Values.GetValueOrDefault(s);
  internal static void Set(object s,long r,byte[] b){NativeIdentityLedgerCodec.Decode(b,r);Values[s]=new(r,b);Fault.Check("ledger");}
 }
 internal static class NativeLedgerState{internal static void Validate(object s,long r,byte[] b){NativeIdentityLedgerCodec.Decode(b,r);Fault.Check("validate");}}
 internal sealed record NativeExchangeCommitResult(bool AlreadyCommitted,long BeforeRevision,long AfterRevision,NativeExchangePlan Plan);
}

namespace PolytopiaBackendBase.Common{public enum SkinType{Default}}
public sealed class ImprovementState:Il2CppSystem.Object{public string name{get;set;}="Bardur City";}
namespace UnityEngine
{
 public class Object:Il2CppSystem.Object{public bool Dead;public static implicit operator bool(Object? o)=>o is not null&&!o.Dead;}
 public class GameObject:Object{public bool activeInHierarchy{get;set;}=true;public void SetActive(bool active)=>activeInHierarchy=active;}
 public class Component:Object{public GameObject gameObject{get;set;}=new();}
 public sealed class Transform:Component{}
}
public sealed class Client:Il2CppSystem.Object{public GameState? GameState{get;set;}public bool IsReady{get;set;}=true;public bool IsReplay{get;set;}public bool IsSpectating{get;set;}public bool IsRecap{get;set;}public bool IsWaitingForCommand{get;set;}public ClientActionManager ActionManager{get;set;}=new();}
public class GameManager
{
 public static GameState? GameState{get;set;}public static Client? Client{get;set;}public static PlayerState? LocalPlayer{get;set;}public static bool Viewing=true;public void Update(){}
 public static bool IsPlayerViewing(byte viewer)=>Viewing&&LocalPlayer?.Id==viewer;
}
public static class HotSeatOverlay{public static bool IsActive{get;set;}}
public sealed class Tile:UnityEngine.Component{public TileData Data{get;set;}=new();public bool IsHidden{get;set;}public City? improvement{get;set;}}
public sealed class City:UnityEngine.Component{public Tile Tile{get;set;}=new();public PlayerState Owner{get;set;}=new();public ImprovementState State{get;set;}=new();}
public sealed class MapRenderer:UnityEngine.Component
{
 public static MapRenderer? Current{get;set;}public List<Tile> Tiles=new();public Tile? GetTileInstance(WorldCoordinates at)=>Tiles.SingleOrDefault(t=>t.Data.coordinates.X==at.X&&t.Data.coordinates.Y==at.Y);
}

public sealed class ClientActionManager:Il2CppSystem.Object{public bool IsProcessing{get;set;}public bool IsRecap{get;set;}}
public static class ClientPlayerExtensions{public static string GetLocalizedTribeName(PlayerState p,GameState s)=>Names.GetValueOrDefault(p.Id,"Tribe "+p.Id);public static Dictionary<byte,string> Names=new(){[1]="Imperius",[3]="Cymanti",[4]="Elyrion"};}
namespace Il2CppSystem{public sealed class Action{}}
public class PopupBase:UnityEngine.Component
{
 public string Header{get;set;}="";public string Description{get;set;}="";
 public void Show(){Fault.Check("show-before");PopupManager.BeforeShow?.Invoke();if(!PopupManager.IgnoreShow){PopupManager.Current=this;PopupManager.PopupShowing=true;PopupManager.Shows++;gameObject.SetActive(true);}Fault.Check("show-after");}
 public void Hide(){Fault.Check("hide-before");PopupManager.Hides++;if(ReferenceEquals(PopupManager.Current,this)){PopupManager.Current=null;PopupManager.PopupShowing=false;}gameObject.SetActive(false);Fault.Check("hide-after");}
}
public sealed class PopupButtons:Il2CppSystem.Object{}
public sealed class BasicPopup:PopupBase
{
 public PopupButtons buttonData{get;set;}=new();
 public BasicPopup SetHeader(string value){Header=value;Fault.Check("header");return this;}
 public BasicPopup SetDescription(string value){Description=value;Fault.Check("description");return this;}
 public void SetDefaultOkbutton(Il2CppSystem.Action? callback){buttonData=new();PopupManager.DuringBuild?.Invoke();Fault.Check("buttons");}
}
public class PopupManager:Il2CppSystem.Object
{
 public static PopupBase? Current;public static bool PopupShowing{get;set;}public static int Shows,Hides;public static bool IgnoreShow;
 public static Action? BeforeShow,DuringBuild;public static BasicPopup Pool=new();
 public static PopupBase? GetCurrentPopup()=>Current;public static BasicPopup GetBasicPopup(){Fault.Check("factory");return Pool;}
 public static void Dismiss(){Current=null;PopupShowing=false;}
}
namespace UnityEngine
{
 public static class PlayerPrefs
 {
  public static Dictionary<string,int> Values=new();public static int Reads,Saves;public static Action? OnRead;
  public static int GetInt(string key,int fallback){Reads++;OnRead?.Invoke();Fault.Check("prefs-read");return Values.GetValueOrDefault(key,fallback);}
  public static void SetInt(string key,int value){Fault.Check("prefs-write");Values[key]=value;}
  public static void Save(){Fault.Check("prefs-save");Saves++;}
 }
}
// Contacts have a separate real combined economy gate, inactive here.
namespace Deception.PolyModAdapter{internal static class NativeBlackmailContacts{internal sealed class Pending{internal void Attach(){}internal void Accept(){}internal void Rollback(){}}internal static Pending? Prepare(object state,byte victim,LedgerBlackmail[] rows)=>null;}}

// Hostility is validated by the parent's combined native transaction gate.
public sealed class PeaceRequestResponseAction:ActionBase{public void Execute(GameState state){}}
namespace Deception.PolyModAdapter{internal static class NativeBlackmailHostility{internal static bool IsPeace(object state,byte a,byte b)=>false;internal static Pending Prepare(object state,byte actor,byte victim)=>new();internal sealed class Pending{internal void Attach(){}internal void Accept(){}internal void Rollback(){}}}}
