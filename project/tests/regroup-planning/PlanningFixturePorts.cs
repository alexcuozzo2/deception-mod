using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
namespace Deception.PolyModAdapter
{
    internal enum Tribe { None,Imperius,Bardur }
    internal enum Skin { None,Default }
    internal enum StatePhase { Started, Ended }
    internal enum ActionType { Move,KillUnit }
    internal class NativeAction {public ActionType Kind{get;set;}=ActionType.Move;}
    internal sealed class KillAction:NativeAction {public KillAction(){Kind=ActionType.KillUnit;}public Point Coordinates{get;set;}=new();}
    internal sealed class Point {public int X{get;set;}public int Y{get;set;}}
    internal enum UnitEffect { Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8 }
    internal sealed class Player {public byte Id{get;set;}=1;public Tribe tribe{get;set;}=Tribe.Imperius;public Tribe _climate{get;set;}=Tribe.None;public int tribeMix{get;set;}=0;public int color{get;set;}=123456;public bool HasDeception{get;set;}=true;public bool HasPropaganda{get;set;}=true;public Dictionary<byte,DiplomacyRelation> relations{get;set;}=new();}
    internal sealed class DiplomacyRelation {public int State{get;set;}}
    internal sealed class Data {public string type{get;set;}="";public int health{get;set;}=100;public int attack{get;set;}=0;public int defence{get;set;}=0;public int range{get;set;}=0;public int growthRate{get;set;}=0;}
    internal sealed class Unit
    {
        static long Next;public long Pointer{get;}=++Next;
        public bool moved{get;set;}public bool attacked{get;set;}public bool Leader{get;set;}public bool Follower{get;set;}
        public ushort createdTurn{get;set;}public uint id{get;set;}public string type{get;set;}="";public byte owner{get;set;}=1;
        public Point coordinates{get;set;}=new();public int health{get;set;}public int promotionLevel{get;set;}public Data UnitData{get;set;}=new();
        public List<UnitEffect> effects{get;set;}=new();public Unit? passengerUnit{get;set;}public Tribe birthClimate{get;set;}=Tribe.Imperius;public Skin birthClimateSkinType{get;set;}=Skin.Default;
    }
    internal sealed class Tile {public bool IsLand{get;set;}=true;public bool IsWater=>!IsLand;public bool Wetland{get;set;}public string Terrain{get;set;}="field";public bool City{get;set;}public byte owner{get;set;}=1;public bool Explored{get;set;}=true;public Unit? unit{get;set;}public Point coordinates{get;set;}=new();}
    internal sealed class Map {public Tile[] Tiles{get;set;}=Array.Empty<Tile>();}
    internal sealed class State {public StatePhase CurrentState{get;set;}=StatePhase.Started;public uint CurrentTurn{get;set;}=3;public byte CurrentPlayer{get;set;}=1;public HashSet<string> AllowedTerrain{get;set;}=new(){"field"};public NativeGameStateEnvelope.Snapshot? Ledger{get;set;}public int Version{get;set;}=123;public uint CurrentUnitId{get;set;}public Map Map{get;set;}=new();public Player Player{get;set;}=new();public object GameLogicData{get;}=new();public List<NativeAction> ActionStack{get;}=new();public Unit? LookupOverride{get;set;}}
    internal sealed record Method(string Owner,string Name);
    internal static class Native
    {
        internal static System.Collections.Generic.IEnumerable<object> Items(object list)=>((System.Collections.IEnumerable)list).Cast<object>();
        internal static object NewList(string type,System.Collections.Generic.IEnumerable<object> values)=>values.Select(v=>(UnitEffect)v).ToList();
        internal static bool SpecificTerrain;
        internal static Type T(string name)=>name switch{"GameState"=>typeof(State),"GameState+State"=>typeof(StatePhase),"ActionType"=>typeof(ActionType),"PolytopiaBackendBase.Common.TribeType"=>typeof(Tribe),"PolytopiaBackendBase.Common.SkinType"=>typeof(Skin),"UnitEffect"=>typeof(UnitEffect),_=>throw new Exception("Unknown fixture type: "+name)};
        internal static object? Get(object o,string name)=>name=="Length"&&o is Array a?a.Length:o.GetType().GetProperty(name)!.GetValue(o);
        internal static object EnumValue(string type,string name)=>name;
        internal static void Set(object o,string name,object? value)=>o.GetType().GetProperty(name)!.SetValue(o,value);
        internal static object DetachedClone(object o,string type)=>typeof(object).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.Invoke(o,null)!;
        internal static int Int(object? o)=>Convert.ToInt32(o);
        internal static long Pointer(object o)=>o is Unit u?u.Pointer:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
        internal static bool Same(object? a,object? b)=>a is not null&&b is not null&&ReferenceEquals(a,b);
        internal static object As(object o,string type)=>type=="KillUnitAction"&&o is KillAction?o:throw new InvalidDataException("Wrong concrete native action type");
        internal static string EnumName(object value)=>value.ToString()!.ToLowerInvariant();
        internal static Method M(string type,string name,params string[] args)=>new(type,name);
        internal static Method M(Type type,string name,params string[] args)=>new(type.FullName!,name);
        internal static object? Invoke(Method method,object? target,params object?[] args)
        {
            if(method.Name=="TryGetValue"){bool found=((Dictionary<byte,DiplomacyRelation>)target!).TryGetValue((byte)args[0]!,out var relation);args[1]=relation;return found;}
            if(method.Name=="get_Item")return target is Array a?a.GetValue((int)args[0]!):target!.GetType().GetProperty("Item")!.GetValue(target,new[]{args[0]});
            return (method.Owner,method.Name) switch
            {
                ("GameState","TryGetPlayer")=>TryPlayer((State)target!,args),
                ("GameState","TryGetUnit")=>TryUnit((State)target!,args),
                ("MapDataExtensions","GetTile")=>((Map)args[0]!).Tiles.FirstOrDefault(t=>t.coordinates.X==((Point)args[1]!).X&&t.coordinates.Y==((Point)args[1]!).Y),
                ("ActionBase","GetActionType")=>((NativeAction)target!).Kind,
                ("PlayerExtensions","GetVisualTribeType")=>((Player)args[0]!).tribe,
                ("PlayerState","GetVisualSkin")=>Skin.Default,
                ("UnitState","HasLeader")=>((Unit)target!).Leader,("UnitState","HasFollower")=>((Unit)target!).Follower,
                ("UnitState","HasEffect")=>((Unit)target!).effects.Contains((UnitEffect)args[0]!),
                ("PlayerExtensions","HasTech")=>(string)args[1]! == TechTree.PropagandaId?((Player)args[0]!).HasPropaganda:((Player)args[0]!).HasDeception,
                ("TileData","GetExplored")=>((Tile)target!).Explored,
                ("TileData","IsWetland")=>((Tile)target!).Wetland,
                ("TileData","HasImprovement")=>((Tile)target!).City,
                ("TileData","matchesAnyTerrain")=>((HashSet<string>)args[0]!).Contains(((Tile)target!).Terrain),
                ("UnitDataExtensions","GetAllowedTerrain")=>SpecificTerrain&&((Unit)args[0]!).type=="rider"?new HashSet<string>{"field"}:((State)args[1]!).AllowedTerrain,
                ("WorldCoordinates","IsAdjacent")=>Adjacent((Point)args[0]!, (Point)args[1]!),
                ("UnitDataExtensions","GetMaxHealth")=>((Unit)args[0]!).UnitData.health+50*((Unit)args[0]!).promotionLevel,
                _=>throw new Exception("Unknown fixture call "+method)
            };
        }
        static bool Adjacent(Point a,Point b)=>(a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)<3;
        static bool TryUnit(State state,object?[] args){if(state.LookupOverride is not null){args[1]=state.LookupOverride;return true;}var graph=NativeLedgerState.Graph(state);bool found=graph.TryGetValue(Convert.ToUInt32(args[0]),out var body);args[1]=body?.Unit;return found;}
        static bool TryPlayer(State state,object?[] args){args[1]=state.Player;return Convert.ToByte(args[0])==1;}
    }
    internal static class RecruitmentHooks
    {
        internal const string GLD="Unused",UT="UnitType",TT="TechType";internal static bool Registered=true;
        internal static Dictionary<string,RecruitmentForm> Forms=new();
        internal static Dictionary<string,Data> Profiles=new();
        internal static void Initialize(){foreach(string f in RecruitmentCatalog.CarrierForms){var id=RecruitmentCatalog.Variant(f);Forms[id]=new(f,id,f,10,1,0,0,false);Profiles[f]=new(){type=f,health=f=="babydragon"?150:f=="firedragon"?200:100,growthRate=NativeApparentGrowth.IsGrowing(f)?NativeApparentGrowth.Duration(f):0};Profiles[id]=new(){type=id,health=10};}}
        internal static Data Data(object state,string form)=>Profiles[form];
    }
    internal static class NativeGameStateEnvelope
    {
        internal static Snapshot? Get(object state)=>((State)state).Ledger;
        internal sealed class Snapshot{internal long Revision{get;}readonly byte[] Payload;internal Snapshot(long revision,byte[] bytes){Revision=revision;Payload=(byte[])bytes.Clone();}internal byte[] Bytes()=>(byte[])Payload.Clone();}
    }
    internal static class NativeFreshSession {internal static bool Enabled=>false;}
    internal static class NativeRegroupSession {internal static bool Production=true;internal static bool Enabled=>Production;}
    internal static class NativeEnvelopeProbe {internal static bool ValidatePayload(byte[] payload)=>false;}
    // This older fixture tests two-unit Regroup/Disguise plans only. Never
    // silently accept a single-unit upgrade through this unexercised boundary.
    internal static class NativeShedDisguise
    {internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason){plan=null;reason="Shed belongs to its dedicated command fixture.";return false;}}
    internal static class NativeApparentUpgrades
    {
        internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason)
        {plan=null;reason="Upgrade planning is outside this exchange fixture";throw new NotSupportedException(reason);}
    }
}

namespace Deception.PolyModAdapter
{
    // Land-only fixture compatibility. Production coastal adapters are compiled
    // directly in tests/mimic-transport-0357, including all eight coast outcomes.
    internal static class NativeMimicTransport
    {
        internal static bool CanOccupyExchange(object data,object tile)=>!Native.SpecificTerrain||((Data)data).type!="rider"||((Tile)tile).Terrain=="field";
        internal static bool HasNativeEmbarkFamily(object state,object player)=>true;
        internal static bool IsVessel(object unit)=>IsVesselData(((Unit)unit).UnitData);
        internal static bool IsVesselData(object data)=>RecruitmentCatalog.NavalCopyOnly.Contains(((Data)data).type.Replace(RecruitmentCatalog.Prefix,""));
        internal static bool WaterEndpoint(object tile)=>!((Tile)tile).City&&(!((Tile)tile).IsLand||((Tile)tile).Wetland);
        internal static bool WaterEndpoint(object tile,bool wasBoat)=>((Tile)tile).Wetland?wasBoat:WaterEndpoint(tile);
        internal static bool NaturallyAquatic(object data)=>new[]{"firedragon","jellyfish","shark","livingisland"}.Contains(((Data)data).type.Replace(RecruitmentCatalog.Prefix,""));
        internal static string StandardVessel(string form)=>form=="giant"?"juggernaut":"transportship";
        internal static LedgerAppearance InterpretAppearance(object state,LedgerAppearance appearance,object tile)
        {
            if(((Tile)tile).Wetland)return appearance;
            if(!WaterEndpoint(tile))return appearance.Passenger??appearance;
            if(appearance.Passenger is not null||NaturallyAquatic(RecruitmentHooks.Data(state,appearance.Form)))return appearance;
            return appearance with{Form=StandardVessel(appearance.Form),Passenger=appearance};
        }
    }
}
