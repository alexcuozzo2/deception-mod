// Synthetic IL2CPP boundaries; no native renderer/game execution claim.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deception.Core;
namespace HarmonyLib
{
    public sealed class HarmonyMethod{public readonly MethodInfo Method;public HarmonyMethod(MethodInfo m){Method=m;}}
    public sealed class Harmony{public List<string> Hooks=new();public void Patch(MethodInfo m,HarmonyMethod? pre,HarmonyMethod? post,object? transpiler,HarmonyMethod? final,object? il)=>Hooks.Add(m.DeclaringType!.Name+"."+m.Name);}
}
namespace Il2CppSystem{public class Object{static long Next;public IntPtr Pointer{get;private set;}=new(++Next);public new Object MemberwiseClone(){var c=(Object)base.MemberwiseClone();c.Pointer=new(++Next);return c;}}}
namespace Il2CppSystem.Collections.Generic
{public class List<T>:Il2CppSystem.Object{readonly System.Collections.Generic.List<T> Values=new();public int Count=>Values.Count;public T this[int n]=>Values[n];public void Add(T item)=>Values.Add(item);public void Remove(T item)=>Values.Remove(item);public bool Contains(T item)=>Values.Contains(item);}}
namespace Polytopia.Data
{
    public sealed class ImprovementData { public enum Type {city} }
    public sealed class UnitData:Il2CppSystem.Object
    {
        public enum Type{dcp_spy=1127,dcp_spymaid=1128,dcp_mimic=1027,warrior=2,rider=3,mindbender=10,shaman=22,giant=12,dragonegg=17,babydragon=18,firedragon=19,amphibian=20,dagger=37,cloak=38,cloak_boat=39,pirate=40,transportship=43,scoutship=44,juggernaut=45,rammership=46,bombership=47,
            dcp_mimic_form_mindbender=1073,dcp_mimic_form_shaman=1074,dcp_mimic_form_warrior=1000,dcp_mimic_form_giant=1001,dcp_mimic_form_dragonegg=1002,dcp_mimic_form_babydragon=1046,dcp_mimic_form_firedragon=1042,dcp_mimic_form_transportship=1048,dcp_mimic_form_scoutship=1049,dcp_mimic_form_rammership=1050,dcp_mimic_form_bombership=1051,dcp_mimic_form_juggernaut=1052,dcp_mimic_form_pirate=1053,dcp_mimic_form_cloak_boat=1054}
        public UnitData? upgradesFrom{get;set;}public Type type{get;set;}public int health{get;set;}=100;public int cost{get;set;}=3;
        public int attack{get;set;}public int defence{get;set;}public int range{get;set;}
        public Il2CppSystem.Collections.Generic.List<UnitAbility> unitAbilities{get;set;}=new();
    }
}
public enum Technology{dcp_deception,dcp_camouflage,dcp_propaganda}
public enum UnitAbility{HealOthers=9,Carry=11,Fly=13,Boost=24,Stiff=38,Protect=39,Swarm=53}
public enum UnitEffect{Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8}
public enum TribeType{Imperius,Elyrion}
public enum SkinType{Default,DarkElf}
public enum GridDirection{North,South}
public static class EnumCache<T>where T:struct,Enum{public static string GetName(T x)=>x.ToString();public static T GetType(string x)=>Enum.Parse<T>(x);}
public sealed class WorldCoordinates:Il2CppSystem.Object
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
    public bool CanMove()=>!moved&&!HasEffect(UnitEffect.Frozen);public bool CanAttack()=>!type.ToString().StartsWith("dcp_mimic",StringComparison.Ordinal)&&!attacked&&!HasEffect(UnitEffect.Frozen);public void AddEffect(UnitEffect e){if(!effects.Contains(e))effects.Add(e);}public void RemoveEffect(UnitEffect e)=>effects.Remove(e);public bool HasLeader()=>leader!=0;public bool HasFollower()=>follower!=0;public bool HasEffect(UnitEffect e)=>Deception.PolyModAdapter.Native.Items(effects).Contains(e);
    public void MakeExhauseted(GameState state){moved=attacked=true;effects.Remove(UnitEffect.Boosted);effects.Remove(UnitEffect.DoubleReady);Fault.Check("exhaust");}
}
public sealed partial class TileData:Il2CppSystem.Object
{
    UnitState? Unit;
    public WorldCoordinates coordinates{get;set;}=new();public bool IsWater{get;set;}public bool Port{get;set;}public bool City{get;set;}public bool Wetland{get;set;}public byte owner{get;set;}=1;public bool Explored{get;set;}=true;public bool IsLand=>!IsWater;
    public UnitState? unit{get=>Unit;set{Unit=value;Fault.Check("tile");}}
    public UnitState? GetUnit(GameState s,byte p,bool useFog)=>Explored?unit:null;public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>City;public bool IsWetland()=>Wetland;public bool GetExplored(byte player)=>Explored;public bool matchesAnyTerrain(HashSet<string> terrain)=>terrain.Contains(IsWater?"water":"field");
}
public sealed class MapData:Il2CppSystem.Object{public List<TileData> Tiles{get;}=new();}
public static class MapDataExtensions{public static Il2CppSystem.Collections.Generic.List<TileData> GetArea(MapData map,WorldCoordinates at,int radius,bool diagonals,bool center){var l=new Il2CppSystem.Collections.Generic.List<TileData>();foreach(var t in map.Tiles)if(Math.Max(Math.Abs(t.coordinates.X-at.X),Math.Abs(t.coordinates.Y-at.Y))<=radius&&(center||t.coordinates.X!=at.X||t.coordinates.Y!=at.Y))l.Add(t);return l;}public static TileData? GetTile(MapData map,WorldCoordinates at)=>map.Tiles.SingleOrDefault(t=>t.coordinates.X==at.X&&t.coordinates.Y==at.Y);}
public sealed class PlayerState:Il2CppSystem.Object {public byte Id{get;set;}=1;public Dictionary<byte,DiplomacyRelation> relations{get;set;}=new();public bool Researched{get;set;}=true;public HashSet<string> Upgrades{get;set;}=new();public bool Propaganda{get;set;}}
public sealed class DiplomacyRelation{public int State{get;set;}}
public static class PlayerExtensions{public static bool HasTech(PlayerState p,Technology t)=>t==Technology.dcp_propaganda?p.Propaganda:p.Researched;}
public static class UnitDataExtensions{public static bool CanRecover(UnitState u,GameState state)=>u.CanMove()&&u.CanAttack()&&(u.health<u.UnitData.health||u.HasEffect(UnitEffect.Poisoned));public static bool IsAquatic(Polytopia.Data.UnitData data)=>data.type==Polytopia.Data.UnitData.Type.amphibian;public static HashSet<string> GetAllowedTerrain(UnitState u,GameState s)=>new(){Deception.PolyModAdapter.NativeMimicTransport.IsVessel(u)?"water":"field"};}
public sealed class GameState:Il2CppSystem.Object
{
    public enum State{Started,Ended};public State CurrentState{get;set;}=State.Started;public PlayerState Player{get;set;}=new();public bool TryGetPlayer(byte id,out PlayerState? player){player=Player;return id==Player.Id;}
    uint Counter=7;public int Version{get;set;}=123;public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=4;
    public uint CurrentUnitId{get=>Counter;set{Counter=value;Fault.Check("counter");}}public MapData Map{get;set;}=new();public List<object> ActionStack{get;set;}=new();
    public GameLogicData GameLogicData{get;set;}=new();public bool TryGetUnit(uint id,out UnitState? unit){unit=Map.Tiles.SelectMany(t=>t.unit is UnitState u?new[]{u,u.passengerUnit}:Array.Empty<UnitState?>()).SingleOrDefault(u=>u?.id==id);return unit is not null;}public int Stars{get;set;}=21;public int Score{get;set;}=300;public int CitySupport{get;set;}=1;
}
public sealed class GameLogicData:Il2CppSystem.Object{public Il2CppSystem.Collections.Generic.List<Polytopia.Data.UnitData> GetUnlockedUpgradesForUnit(PlayerState player,GameState state,Polytopia.Data.UnitData source){var list=new Il2CppSystem.Collections.Generic.List<Polytopia.Data.UnitData>();foreach(var d in Deception.PolyModAdapter.RecruitmentHooks.Definitions.Values)if(ReferenceEquals(d.upgradesFrom,source)&&player.Upgrades.Contains(d.type.ToString()))list.Add(d);return list;}}
public static class ActionUtils
{
    public static ushort GetHealAmount(GameState state,TileData tile)=>(ushort)Math.Min(tile.unit!.UnitData.health-tile.unit.health,tile.owner==tile.unit.owner?40:20);public static void PerformAttack(GameState state,byte actor,WorldCoordinates source,WorldCoordinates target,int damage){}
    public static int WillUnitEmbark(UnitState unit,TileData tile,GameState state)
    {bool boat=Deception.PolyModAdapter.NativeMimicTransport.IsVessel(unit);return boat&&!tile.IsWater?2:!boat&&tile.IsWater&&tile.Port?1:0;}
}
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
            Definitions.Clear();Forms.Clear();foreach(var type in Enum.GetValues<Polytopia.Data.UnitData.Type>())
            {
                string name=type.ToString();bool mimic=RecruitmentCatalog.IsMimicId(name);string form=mimic&&name!=RecruitmentCatalog.MenuId?name[RecruitmentCatalog.Prefix.Length..]:name;
                bool boat=RecruitmentCatalog.NavalCopyOnly.Contains(form);var d=new Polytopia.Data.UnitData{type=type,health=mimic?10:form=="giant"?400:form is "babydragon" or "pirate"?150:form=="firedragon"?200:100,cost=boat?0:mimic?RecruitmentCatalog.Cost:3};
                if(form=="mindbender")d.unitAbilities.Add(UnitAbility.HealOthers);if(form=="shaman"){d.unitAbilities.Add(UnitAbility.Boost);d.unitAbilities.Add(UnitAbility.Swarm);}if(boat)d.unitAbilities.Add(UnitAbility.Carry);if(form is "transportship" or "bombership" or "juggernaut" or "cloak_boat")d.unitAbilities.Add(UnitAbility.Stiff);if(form=="firedragon")d.unitAbilities.Add(UnitAbility.Fly);Definitions[name]=d;if(mimic)Forms[name]=new(form,name,form,10,1,0,0,false);
            }
            foreach(var name in new[]{"scoutship","rammership","bombership"})Definitions[name].upgradesFrom=Definitions["transportship"];
        }
    }
    internal static class NativeRegroupSession
    {
        internal static bool Enabled=true,Admitted=true;internal static object? Owned;internal static List<string> Faults=new();internal static int Prepares;
        internal static bool Ready(object state)=>Enabled&&Admitted&&ReferenceEquals(Owned,state);
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
        internal static Dictionary<uint,Body> Graph(object state){var result=new Dictionary<uint,Body>();foreach(var tile in ((GameState)state).Map.Tiles){if(tile.unit is not UnitState u)continue;result.Add(u.id,new(u,true));if(u.passengerUnit is UnitState cargo)result.Add(cargo.id,new(cargo,false));}return result;}
        internal static LedgerAppearance Capture(object state,Body b,string form,bool legacy){var u=(UnitState)b.Unit;var cargo=u.passengerUnit is null?null:Capture(state,new(u.passengerUnit,false),u.passengerUnit.type.ToString(),false);return new(form,new("Elyrion","DarkElf"),new("Imperius","Default"),new(u.birthClimate.ToString(),u.birthClimateSkinType.ToString()),57,u.health,(ushort)(cargo is not null&&!u.UnitData.unitAbilities.Contains(UnitAbility.Protect)?cargo.Maximum:u.UnitData.health+u.promotionLevel*50),u.promotionLevel==1,u.HasEffect(UnitEffect.Invisible)?ApparentConcealment.Hidden:ApparentConcealment.Visible,-1,Native.Items(u.effects).Select(x=>Enum.GetName(typeof(UnitEffect),x)??throw new InvalidDataException("Unknown native visual enum value.")).OrderBy(x=>x,StringComparer.Ordinal).ToArray(),Passenger:cargo);}
        internal static void Validate(object state,long revision,byte[] bytes)
        {
            if(revision>(NativeGameStateEnvelope.Get(state)?.Revision??0))Fault.Check("candidate-validation");
            Fault.Check("validation");var game=(GameState)state;var ledger=NativeIdentityLedgerCodec.Decode(bytes,revision);
            void Appearance(LedgerAppearance a){foreach(var effect in a.Effects)if(!Enum.GetNames<UnitEffect>().Contains(effect,StringComparer.Ordinal))throw new InvalidDataException("Unknown native visual profile: "+effect);if((a.Concealment==ApparentConcealment.Hidden)!=a.Effects.Contains("Invisible"))throw new InvalidDataException("Concealment mismatch");if(a.Passenger is not null)Appearance(a.Passenger);}
            foreach(var row in ledger.Units.Where(x=>x.Alive))
            {
                if(row.Appearance is not null)Appearance(row.Appearance);
                var root=game.Map.Tiles.Select(t=>t.unit).SingleOrDefault(u=>u?.id==row.NativeId)??throw new InvalidDataException("Missing root");
                if(row.Transport is null){if(root.passengerUnit is not null)throw new InvalidDataException("Unexpected actual cargo");}
                else if(root.passengerUnit?.id!=row.Transport.PassengerNativeId)throw new InvalidDataException("Wrong actual cargo");
                if(row.Kind==LedgerActualKind.Mimic&&(root.health!=10||root.UnitData.health!=10||root.UnitData.attack!=0||root.promotionLevel!=0))throw new InvalidDataException("Mimic gained actual health/offense");
            }
        }
    }
    internal static class Entry{internal static void Note(string category,string value){}}
    internal static class NativeRogueRules{internal static byte RememberedOwner(object state,object unit){var u=(UnitState)unit;var snap=NativeGameStateEnvelope.Get(state)!;var row=NativeIdentityLedgerCodec.Decode(snap.Bytes(),snap.Revision).Units.SingleOrDefault(x=>x.Alive&&x.NativeId==u.id);return row?.Rogue?.Owner??u.owner;}internal static bool CanCommand(object state,object unit,byte actor)=>((UnitState)unit).owner==actor;}
    internal static class NativeMimicTransport{internal static bool IsVessel(object u)=>RecruitmentCatalog.NavalCopyOnly.Contains(((UnitState)u).type.ToString());}
    internal sealed class NativeExchangePlan{
       internal object State;internal NativeExchangeIntent Intent;internal bool AlreadyCommitted;internal long BeforeRevision,AfterRevision;internal byte[] OriginalPayload,CandidatePayload;internal object? A,TileA;
       internal NativeIdentityLedger OriginalLedger=>NativeIdentityLedgerCodec.Decode(OriginalPayload,BeforeRevision);internal NativeIdentityLedger CandidateLedger=>NativeIdentityLedgerCodec.Decode(CandidatePayload,AfterRevision);
       internal NativeExchangePlan(object state,NativeExchangeIntent intent,bool committed,long beforeRevision,long afterRevision,byte[] before,byte[] after,object? a=null,object? tileA=null){State=state;Intent=intent;AlreadyCommitted=committed;BeforeRevision=beforeRevision;AfterRevision=afterRevision;OriginalPayload=(byte[])before.Clone();CandidatePayload=(byte[])after.Clone();A=a;TileA=tileA;}}
    internal sealed record NativeExchangeCommitResult(bool AlreadyCommitted,long BeforeRevision,long AfterRevision,NativeExchangePlan Plan);
    internal static class TransportStateDump{internal static string Dump(GameState s)=>string.Join(";",s.Map.Tiles.Select(t=>t.unit is not UnitState u?"none":string.Join("|",u.id,u.health,u.type,u.moved,u.attacked,u.effects.Pointer,string.Join(",",Native.Items(u.effects)),u.UnitData.Pointer,u.owner)))+s.Stars;}
}
public sealed class ClientBase:Il2CppSystem.Object{public bool IsReplay{get;set;}public bool IsSpectating{get;set;}}
public static class GameManager{public static GameState? GameState{get;set;}public static PlayerState? LocalPlayer{get;set;}public static ClientBase Client{get;set;}=new();public static bool Viewing=true;public static bool IsPlayerViewing(byte player)=>Viewing&&LocalPlayer?.Id==player;}
public static class TileDataExtensions{public static Il2CppSystem.Collections.Generic.List<TileData> GetHealOptions(TileData t,byte p,GameState s,bool c)=>new();public static Il2CppSystem.Collections.Generic.List<TileData> GetBoostOptions(TileData t,byte p,GameState s)=>new();}
public class SupportAction:Il2CppSystem.Object{public byte PlayerId{get;set;}=1;public WorldCoordinates Coordinates{get;set;}=new();public void Execute(GameState state){} }
public class BoostAction:SupportAction{public BoostAction(){}public BoostAction(byte p,WorldCoordinates c){PlayerId=p;Coordinates=c;}} public class FreezeUnitAction:SupportAction{} public class PoisonUnitAction:SupportAction{} public class EndTurnCommand:SupportAction{public new void Execute(GameState state){}}
public sealed class HealAction:SupportAction{public HealAction(){}public HealAction(byte p,WorldCoordinates c,ushort amount){PlayerId=p;Coordinates=c;FixedHealAmount=amount;}public ushort FixedHealAmount{get;set;}public ushort HealAmount{get;set;}}
public class Active:SupportAction{public WorldCoordinates Origin{get;set;}=new();public bool IsValid(GameState s,out string why){why="";return true;}}
public class AttackCommand:Active{} public class BoostCommand:Active{} public class ExplodeCommand:Active{} public class FreezeAreaCommand:Active{} public class HealOthersCommand:Active{} public class SwarmCommand:Active{} public class ConvertAction:Active{}
// Production NativeDisguiseConcealmentEffects owns ability reveal and rollback.
namespace UnityEngine{public class Object:Il2CppSystem.Object{public bool Destroyed;public static implicit operator bool(Object? x)=>x is not null&&!x.Destroyed;}}
namespace Il2CppSystem{public sealed class Action:Object{readonly System.Action Callback;public int Calls;public Action(System.Action a){Callback=a;}public void Invoke(){Calls++;Callback();}public static implicit operator Action(System.Action a)=>new(a);}}
public sealed class Unit:UnityEngine.Object{public UnitState UnitState{get;set;}=null!;public bool Visible=true,Invisible;public bool GetVisible()=>Visible;public bool IsInvisibleForLocalPlayer()=>Invisible;}
public sealed class Tile:UnityEngine.Object{public Unit? unit{get;set;}public TileData Data{get;set;}=null!;public bool IsHidden{get;set;}}
public sealed class MapRenderer:UnityEngine.Object{public static MapRenderer? Current{get;set;}public Dictionary<string,Tile> Tiles=new();public Unit? GetUnitInstance(uint id)=>Tiles.Values.Select(t=>t.unit).SingleOrDefault(u=>u?.UnitState.id==id);public Tile? GetTileInstance(WorldCoordinates at)=>Tiles.GetValueOrDefault(at.ToString());}
public sealed class BuildAction:Il2CppSystem.Object{public Deception.Core.NativeExchangeIntent? Intent{get;set;}public bool Invalid;}
public sealed class BuildReaction:Il2CppSystem.Object{public BuildAction action{get;set;}=new();public void Execute(Il2CppSystem.Action callback)=>callback.Invoke();}
public static class VisualEvents{public static List<string> Played=new();public static bool DoubleCallback,Throw,Delay;public static List<Il2CppSystem.Action> Waiting=new();public static void Play(string name,Il2CppSystem.Action callback){Played.Add(name);if(Throw)throw new Exception("reaction failed");if(Delay){Waiting.Add(callback);return;}callback.Invoke();if(DoubleCallback)callback.Invoke();}}
public class HealOthersAction:SupportAction{public HealOthersAction(byte owner,WorldCoordinates at){PlayerId=owner;Coordinates=at;}}
public class BoostOthersAction:SupportAction{public BoostOthersAction(byte owner,WorldCoordinates at){PlayerId=owner;Coordinates=at;}}
public class HealOthersReaction:Il2CppSystem.Object{public HealOthersReaction(HealOthersAction action){}public void Execute(Il2CppSystem.Action c)=>VisualEvents.Play("heal-source",c);}
public class BoostOthersReaction:Il2CppSystem.Object{public BoostOthersReaction(BoostOthersAction action){}public void Execute(Il2CppSystem.Action c)=>VisualEvents.Play("boost-source",c);}
public class HealReaction:Il2CppSystem.Object{readonly HealAction Action;public HealReaction(HealAction a){Action=a;}public void Execute(Il2CppSystem.Action c)=>VisualEvents.Play("heal-"+Action.HealAmount,c);}
public class BoostReaction:Il2CppSystem.Object{public BoostReaction(BoostAction a){}public void Execute(Il2CppSystem.Action c)=>VisualEvents.Play("boost-target",c);}
namespace Deception.PolyModAdapter{internal static class NativeExchangeCommands{internal static bool IsExchange(object action)=>((BuildAction)action).Intent!=null;internal static NativeExchangeIntent? Get(object action)=>((BuildAction)action).Intent;internal static void ValidateBase(object action,NativeExchangeIntent i){if(((BuildAction)action).Invalid)throw new Exception("bad carrier");}}}
