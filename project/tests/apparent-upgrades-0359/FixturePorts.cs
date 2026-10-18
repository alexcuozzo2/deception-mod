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
{public class List<T>:Il2CppSystem.Object{readonly System.Collections.Generic.List<T> Values=new();public int Count=>Values.Count;public T this[int n]=>Values[n];public void Add(T item)=>Values.Add(item);public void Remove(T item)=>Values.Remove(item);}}
namespace Polytopia.Data
{
    public sealed class ImprovementData { public enum Type {city} }
    public sealed class UnitData:Il2CppSystem.Object
    {
        public enum Type{warrior=2,rider=3,giant=12,dragonegg=17,babydragon=18,firedragon=19,amphibian=20,dagger=37,cloak=38,cloak_boat=39,pirate=40,transportship=43,scoutship=44,juggernaut=45,rammership=46,bombership=47,
            dcp_mimic_form_warrior=1000,dcp_mimic_form_giant=1001,dcp_mimic_form_dragonegg=1002,dcp_mimic_form_babydragon=1046,dcp_mimic_form_firedragon=1042,dcp_mimic_form_transportship=1048,dcp_mimic_form_scoutship=1049,dcp_mimic_form_rammership=1050,dcp_mimic_form_bombership=1051,dcp_mimic_form_juggernaut=1052,dcp_mimic_form_pirate=1053,dcp_mimic_form_cloak_boat=1054}
        public UnitData? upgradesFrom{get;set;}public Type type{get;set;}public int health{get;set;}=100;public int cost{get;set;}=3;
        public int attack{get;set;}public int defence{get;set;}public int range{get;set;}
        public Il2CppSystem.Collections.Generic.List<UnitAbility> unitAbilities{get;set;}=new();
    }
}
public enum Technology{dcp_deception,dcp_camouflage,dcp_propaganda,sailing,ramming,navigation}
public enum UnitAbility{Carry=11,Fly=13,Stiff=38,Protect=39}
public enum UnitEffect { Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8 }
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
    public bool HasLeader()=>leader!=0;public bool HasFollower()=>follower!=0;public bool HasEffect(UnitEffect e)=>Deception.PolyModAdapter.Native.Items(effects).Contains(e);
    public void MakeExhauseted(GameState state){moved=attacked=true;effects.Remove(UnitEffect.Boosted);effects.Remove(UnitEffect.DoubleReady);Fault.Check("exhaust");}
}
public sealed partial class TileData:Il2CppSystem.Object
{
    UnitState? Unit;
    public WorldCoordinates coordinates{get;set;}=new();public bool IsWater{get;set;}public bool Port{get;set;}public bool City{get;set;}public bool Wetland{get;set;}public byte owner{get;set;}=1;public bool Explored{get;set;}=true;public bool IsLand=>!IsWater;
    public UnitState? unit{get=>Unit;set{Unit=value;Fault.Check("tile");}}
    public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>City;public bool IsWetland()=>Wetland;public bool GetExplored(byte player)=>Explored;public bool matchesAnyTerrain(HashSet<string> terrain)=>terrain.Contains(IsWater?"water":"field");
}
public sealed class MapData:Il2CppSystem.Object{public List<TileData> Tiles{get;}=new();}
public static class MapDataExtensions{public static TileData? GetTile(MapData map,WorldCoordinates at)=>map.Tiles.SingleOrDefault(t=>t.coordinates.X==at.X&&t.coordinates.Y==at.Y);}
public sealed class PlayerState:Il2CppSystem.Object {public byte Id{get;set;}=1;public Dictionary<byte,DiplomacyRelation> relations{get;set;}=new();public bool Researched{get;set;}=true;public HashSet<string> Upgrades{get;set;}=new();public bool Propaganda{get;set;}public PolytopiaBackendBase.Common.SkinType GetVisualSkin()=>PolytopiaBackendBase.Common.SkinType.Default;}
public sealed class DiplomacyRelation{public int State{get;set;}}
public static class PlayerExtensions{public static bool HasTech(PlayerState p,Technology t)=>t==Technology.dcp_propaganda?p.Propaganda:p.Researched;}
public static class UnitDataExtensions{public static bool IsAquatic(Polytopia.Data.UnitData data)=>data.type==Polytopia.Data.UnitData.Type.amphibian;public static HashSet<string> GetAllowedTerrain(UnitState u,GameState s)=>new(){Deception.PolyModAdapter.NativeMimicTransport.IsVessel(u)?"water":"field"};}
public sealed class GameState:Il2CppSystem.Object
{
    public enum State{Started,Ended};public State CurrentState{get;set;}=State.Started;public PlayerState Player{get;set;}=new();public bool TryGetPlayer(byte id,out PlayerState? player){player=Player;return id==Player.Id;}
    uint Counter=7;public int Version{get;set;}=123;public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=4;public long CurrentCommand{get;set;}
    public uint CurrentUnitId{get=>Counter;set{Counter=value;Fault.Check("counter");}}public MapData Map{get;set;}=new();public List<object> ActionStack{get;set;}=new();
    public GameLogicData GameLogicData{get;set;}=new();public bool TryGetUnit(uint id,out UnitState? unit){unit=Map.Tiles.SelectMany(t=>t.unit is UnitState u?new[]{u,u.passengerUnit}:Array.Empty<UnitState?>()).SingleOrDefault(u=>u?.id==id);return unit is not null;}public int Stars{get;set;}=21;public int Score{get;set;}=300;public int CitySupport{get;set;}=1;
}
public sealed class GameLogicData:Il2CppSystem.Object{public Il2CppSystem.Collections.Generic.List<Polytopia.Data.UnitData> GetUnlockedUpgradesForUnit(PlayerState player,GameState state,Polytopia.Data.UnitData source){var list=new Il2CppSystem.Collections.Generic.List<Polytopia.Data.UnitData>();foreach(var d in Deception.PolyModAdapter.RecruitmentHooks.Definitions.Values)if(ReferenceEquals(d.upgradesFrom,source)&&player.Upgrades.Contains(d.type.ToString()))list.Add(d);return list;}}
public static class ActionUtils
{
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
                string name=type.ToString();bool mimic=RecruitmentCatalog.IsMimicId(name);string form=mimic?name[RecruitmentCatalog.Prefix.Length..]:name;
                bool boat=RecruitmentCatalog.NavalCopyOnly.Contains(form);var d=new Polytopia.Data.UnitData{type=type,health=mimic?10:form=="giant"?400:form is "babydragon" or "pirate"?150:form=="firedragon"?200:100,cost=boat?0:mimic?RecruitmentCatalog.Cost:3};
                if(boat)d.unitAbilities.Add(UnitAbility.Carry);if(form is "transportship" or "bombership" or "juggernaut" or "cloak_boat")d.unitAbilities.Add(UnitAbility.Stiff);if(form=="firedragon")d.unitAbilities.Add(UnitAbility.Fly);Definitions[name]=d;if(mimic)Forms[name]=new(form,name,form,10,1,0,0,false);
            }
            foreach(var name in new[]{"scoutship","rammership","bombership"})Definitions[name].upgradesFrom=Definitions["transportship"];
        }
    }
    internal static class NativeRegroupSession
    {
        internal static bool Enabled=true,Admitted=true;internal static object? Owned;internal static List<string> Faults=new();internal static int Prepares;
        internal static bool Ready(object state)=>Enabled&&Admitted&&ReferenceEquals(Owned,state);
        internal static LedgerUnit? ReadIdentity(object state,uint id){var s=NativeGameStateEnvelope.Get(state);return !Ready(state)||s is null?null:NativeIdentityLedgerCodec.Decode(s.Bytes(),s.Revision).Units.SingleOrDefault(r=>r.Alive&&r.NativeId==id);}
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
        // Original-owner Rogue capture is covered by the real ledger fixture.
        internal static LedgerAppearance CapturePublic(object state,Body body,string form,LedgerUnit? row)=>Capture(state,body,form,false);
        internal static LedgerAppearance ExposedAppearance(object state,object unit,LedgerUnit row)=>new(RecruitmentCatalog.MenuId,new("Imperius","Default"),new("Imperius","Default"),new("Imperius","Default"),1,10,10,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
        internal static LedgerAppearance Capture(object state,Body b,string form,bool legacy){var u=(UnitState)b.Unit;var cargo=u.passengerUnit is null?null:Capture(state,new(u.passengerUnit,false),u.passengerUnit.type.ToString(),false);return new(form,new("Elyrion","DarkElf"),new("Imperius","Default"),new("Elyrion","DarkElf"),57,u.health,(ushort)(cargo?.Maximum??u.UnitData.health),false,ApparentConcealment.Visible,-1,Native.Items(u.effects).Select(x=>x.ToString()!).ToArray(),Passenger:cargo);}
        internal static void Validate(object state,long revision,byte[] bytes)
        {
            Fault.Check("validation");var game=(GameState)state;var ledger=NativeIdentityLedgerCodec.Decode(bytes,revision);
            foreach(var row in ledger.Units.Where(x=>x.Alive))
            {
                var root=game.Map.Tiles.Select(t=>t.unit).SingleOrDefault(u=>u?.id==row.NativeId)??throw new InvalidDataException("Missing root");
                if(row.Transport is null){if(root.passengerUnit is not null)throw new InvalidDataException("Unexpected actual cargo");}
                else if(root.passengerUnit?.id!=row.Transport.PassengerNativeId)throw new InvalidDataException("Wrong actual cargo");
                if(row.Kind==LedgerActualKind.Mimic&&(root.health!=10||root.UnitData.health!=10||root.UnitData.attack!=0||root.promotionLevel!=0))throw new InvalidDataException("Mimic gained actual health/offense");
            }
        }
    }
    internal static class NativeDisguiseGrowthAudio
    {
        internal static List<(object Action,object State,LedgerAppearance Before,LedgerUnit After)> Recorded=new();
        internal static void Record(object action,object state,LedgerAppearance before,LedgerUnit after)
        {
            if(after.Appearance is LedgerAppearance a&&before.Passenger is null&&a.Passenger is null&&
                ((before.Form=="dragonegg"&&a.Form=="babydragon")||(before.Form=="babydragon"&&a.Form=="firedragon")))Recorded.Add((action,state,before,after));
        }
        internal static void Cancel(object action,object state)=>Recorded.RemoveAll(c=>ReferenceEquals(c.Action,action)&&ReferenceEquals(c.State,state));
    }
    internal static class Entry{internal static void Note(string category,string value){}}
    internal static class NativeGrowthSight
    {
        internal sealed class Pending:IDisposable
        {
            readonly GameState State;readonly List<object> Before;bool Accepted;
            internal Pending(GameState game,byte owner,IReadOnlyList<object> units)
            {State=game;Before=game.ActionStack;State.ActionStack=new(Before);foreach(var unit in units)State.ActionStack.Add(((UnitState)unit).id);}
            internal void Accept(){Fault.Check("sight-accept");Accepted=true;}
            internal void Rollback(){if(!Accepted)State.ActionStack=Before;}
            public void Dispose(){if(!Accepted)Rollback();}
        }
        internal static Pending Stage(object state,byte owner,IReadOnlyList<object> units)
        {var p=new Pending((GameState)state,owner,units);try{Fault.Check("sight-stage");return p;}catch{p.Rollback();throw;}}
    }
}

namespace Deception.PolyModAdapter
{
    internal static class TransportStateDump
    {
        internal static string Body(UnitState u)=>string.Join("|",u.Pointer,u.id,u.type,u.UnitData.Pointer,u.owner,u.health,u.promotionLevel,u.xp,u.birthClimate,u.birthClimateSkinType,u.createdTurn,u.coordinates,u.previousTurnEndCoordinates,u.home,u.moved,u.attacked,u.direction,u.flipped,u.effects.Pointer,string.Join(",",Native.Items(u.effects)),u.passengerUnit is null?"":Body(u.passengerUnit));
        internal static string Dump(GameState s)=>string.Join(";",s.Map.Tiles.Select(t=>t.unit is null?"empty":Body(t.unit)))+"/"+s.CurrentUnitId+"/"+s.Stars+"/"+s.Score+"/"+s.CitySupport+"/"+string.Join(",",s.ActionStack);
    }
    internal static partial class NativeExchangeCommands{internal static bool LiveExecution(object state)=>NativeRegroupSession.Ready(state);}
    internal static class NativeExchangeConsequences
    {
        internal static Pending Capture(object state,NativeExchangePlan plan)=>new((GameState)state,plan);
        internal sealed class Pending:IDisposable
        {
            readonly GameState State;readonly NativeExchangePlan Plan;readonly List<object> Stack;bool Accepted;
            internal Pending(GameState state,NativeExchangePlan plan){State=state;Plan=plan;Stack=state.ActionStack;}
            internal void StageFinalSources(Action<string> fault){State.ActionStack=new(Stack){((UnitState)Plan.CommittedA!).id,((UnitState)Plan.CommittedB!).id};Fault.Check("sight-stage");}
            internal void MergeDiscovery(){} // Existing fixture has no discovery provenance port.
            internal void AssertPending(){Fault.Check("sight-pending");}
            internal void Accept(){Fault.Check("sight-accept");Accepted=true;}
            internal void Rollback(){if(!Accepted)State.ActionStack=Stack;}
            public void Dispose(){if(!Accepted)Rollback();}
        }
    }
}
