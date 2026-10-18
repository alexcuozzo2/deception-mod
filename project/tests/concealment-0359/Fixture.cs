using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using Deception.Core;
namespace HarmonyLib {public sealed class HarmonyMethod {public HarmonyMethod(MethodInfo m){}} public sealed class Harmony {public int Patches;public void Patch(MethodInfo m,HarmonyMethod? p,HarmonyMethod? q,object? t,HarmonyMethod? f,object? il){Patches++;}}}
public class PointerObject {static long Next;public IntPtr Pointer{get;}=new(++Next);}
namespace Il2CppSystem.Collections.Generic {public sealed class List<T>:PointerObject {readonly System.Collections.Generic.List<T> Values=new();public int Count=>Values.Count;public T this[int i]=>Values[i];public void Add(T value)=>Values.Add(value);public void Remove(T value)=>Values.Remove(value);public bool Contains(T value)=>Values.Contains(value);}}
namespace Polytopia.Data {public sealed class UnitAbility {public enum Type{Dash=1,Hide=5,Infiltrate=35,Scout=3,Creep=23}}public sealed class UnitData {public enum Type{warrior,cloak,mermaidcloak,cloak_boat,dcp_mimic_cloak,rider,dcp_spy,dcp_spymaid}}}
public static class EnumCache<T>where T:struct,Enum {public static T GetType(string s)=>Enum.Parse<T>(s);public static string GetName(T v)=>v.ToString();}
public enum UnitEffect { Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8 }
public sealed class WorldCoordinates {public int X{get;set;}public int Y{get;set;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
public sealed class UnitState:PointerObject {public uint id{get;set;}public byte owner{get;set;}=1;public Polytopia.Data.UnitData.Type type{get;set;}=Polytopia.Data.UnitData.Type.warrior;public WorldCoordinates coordinates{get;set;}=new(0,0);public UnitState? passengerUnit{get;set;}public int health{get;set;}=70;public bool moved{get;set;}public bool attacked{get;set;}public Il2CppSystem.Collections.Generic.List<UnitEffect> effects{get;set;}=new();public bool HasEffect(UnitEffect effect)=>effects.Contains(effect);public bool CanAttack()=>!attacked&&!effects.Contains(UnitEffect.Frozen)&&(!moved||UnitDataExtensions.HasAbility(this,Polytopia.Data.UnitAbility.Type.Dash));public bool IsInvisible(GameState state,byte viewer)=>state.TryGetPlayer(viewer,out var p)&&IsInvisible(p!);public bool IsInvisible(PlayerState viewer)=>effects.Contains(UnitEffect.Invisible)&&owner!=viewer.Id&&!viewer.HasPeaceWith(owner);public bool IsDetectingHiddenUnits(GameState s)=>false;}
public sealed class PlayerState:PointerObject {public byte Id{get;set;}public HashSet<byte> Peace=new();public bool HasPeaceWith(byte owner)=>Peace.Contains(owner);}
public static class PlayerDiplomacyExtensions {public static bool HasPeaceWith(PlayerState player,byte owner)=>player.HasPeaceWith(owner);}
public sealed class TileData {public UnitState? unit{get;set;}public byte owner=2;public bool City,Explored=true,BeingCaptured,Infiltrated;}
public sealed class MapData:PointerObject {public Dictionary<(int,int),TileData> Tiles=new();}
public sealed class GameState:PointerObject {public PointerObject GameLogicData{get;}=new();public MapData Map{get;set;}=new();public Dictionary<byte,PlayerState> Players=new();public bool Owned=true;public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn=4;public int Infiltrations;public byte InfiltrationOwner;public System.Collections.Generic.List<object> ActionStack{get;}=new();public bool TryGetPlayer(byte id,out PlayerState? player)=>Players.TryGetValue(id,out player);public bool TryGetUnit(uint id,out UnitState? unit){unit=Map.Tiles.Values.Select(x=>x.unit).FirstOrDefault(u=>u?.id==id);return unit is not null;}}
public static class MapDataExtensions {public static TileData? GetTile(MapData map,WorldCoordinates c)=>map.Tiles.GetValueOrDefault((c.X,c.Y));}
public class ActionBase:PointerObject {public byte PlayerId{get;set;}=1;public virtual int GetActionType()=>0;}
public sealed class MoveAction:ActionBase {public uint UnitId{get;set;}=1;public int Reason{get;set;}public System.Collections.Generic.List<WorldCoordinates> Path{get;set;}=new(){new(1,0),new(0,0)};public override int GetActionType()=>6;}
public sealed class MoveCommand {public uint UnitId{get;set;}public void Execute(GameState s){}}
public sealed class HideAction:ActionBase {public WorldCoordinates Coordinates{get;set;}=new(0,0);public void Execute(GameState s){var u=MapDataExtensions.GetTile(s.Map,Coordinates)!.unit!;u.attacked=true;if(!u.effects.Contains(UnitEffect.Invisible))u.effects.Add(UnitEffect.Invisible);}}
public sealed class RevealAction {public WorldCoordinates Coordinates{get;set;}=new(0,0);public void Execute(GameState s)=>MapDataExtensions.GetTile(s.Map,Coordinates)!.unit!.effects.Remove(UnitEffect.Invisible);}
public sealed class HideCommand {public WorldCoordinates Coordinates{get;set;}=new(0,0);public bool IsValid(GameState s,out string e){e="";return true;}}
public static class UnitDataExtensions {public static bool HasAbility(UnitState u,Polytopia.Data.UnitAbility.Type a)=>a!=Polytopia.Data.UnitAbility.Type.Dash&&(u.type is Polytopia.Data.UnitData.Type.cloak or Polytopia.Data.UnitData.Type.mermaidcloak or Polytopia.Data.UnitData.Type.cloak_boat);}
public sealed class ClientBase:PointerObject {public bool IsReplay{get;set;}public bool IsSpectating{get;set;}public PlayerState? Player;public PlayerState? GetCurrentLocalPlayer()=>Player;}
public static class GameManager {public static PlayerState? LocalPlayer=>Client?.Player;public static GameState? GameState{get;set;}public static ClientBase? Client{get;set;}}
public static class UnitManager {public static void UpdateInvisibleUnitIndicatorIcons(){}}
namespace Deception.PolyModAdapter {
 internal static class NativeRegroupSession {internal static bool Enabled=true;internal static int Faults;internal static bool Ready(object s)=>((GameState)s).Owned;internal static void Prepare(object s){}internal static void Fault(string x){Faults++;}internal static LedgerUnit? ReadIdentity(object s,uint id)=>Ready(s)?NativeIdentityLedgerCodec.Decode(NativeGameStateEnvelope.Get(s)!.Bytes(),0).Units.FirstOrDefault(r=>r.NativeId==id):null;}
 internal static class NativeGameStateEnvelope {internal sealed record Snapshot(byte[] Data,long Revision){public byte[] Bytes()=>(byte[])Data.Clone();}static Dictionary<object,Snapshot> Data=new();internal static Snapshot? Get(object s)=>Data.GetValueOrDefault(s);internal static void Set(object s,long r,byte[] b)=>Data[s]=new((byte[])b.Clone(),r);}
 internal static class NativeLedgerState {internal static bool Fail;internal static void Validate(object s,long r,byte[] b){if(Fail)throw new InvalidDataException("injected validation");NativeIdentityLedgerCodec.Validate(NativeIdentityLedgerCodec.Decode(b,r),r);}}
}
