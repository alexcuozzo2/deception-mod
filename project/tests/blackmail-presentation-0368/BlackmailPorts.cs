using System;
using System.Linq;
using Deception.Core;
public static class HotSeatOverlay { public static bool IsActive {get;set;} }
public sealed class City:UnityEngine.Component
{
    public CityRenderer cityRenderer {get;set;}=new();
    public CityStatusDisplay cityOverlay {get;set;}=new();
    public Tile? Tile {get;set;}public PlayerState? Owner {get;set;}
}
public sealed class CityStatusDisplay:UnityEngine.Component
{
    public City? city {get;set;}public CityStatusNameContainer nameContainer {get;set;}=new();
    public CityStatusProgressBar progressBar {get;set;}=new();
    public CityStatusDisplay(){progressBar.gameObject.SetActive(false);}
    public void ReturnToPool(){}
}
public sealed class CityStatusProgressBar:UnityEngine.Component
{public Il2CppSystem.Collections.Generic.List<CityStatusProgressSegment> segments {get;set;}=new();}
public sealed class CityStatusProgressSegment:UnityEngine.Component
{public UnityEngine.SpriteRenderer background {get;set;}=new();public UnityEngine.SpriteRenderer dot {get;set;}=new();}
public sealed class CityNameLabel:UnityEngine.Component
{public UnityEngine.Renderer renderer {get;set;}=new UnityEngine.SpriteRenderer();}
public sealed class CityStatusNameContainer:UnityEngine.Component
{
    public UnityEngine.SpriteRenderer bg {get;set;}=new();public CityNameLabel label {get;set;}=new();
    public City? city {get;set;}public string Text="";public bool OmitWork;public Action? During;
    public UnityEngine.Transform workContainer {get;set;}=new();
    public void SetCity(City value)
    {
        Deception.PolyModAdapter.NativeBlackmailEmbassyUi.BeforeCity(this,value,out var scope);Exception? error=null;
        try{city=value;During?.Invoke();if(OmitWork)workContainer.gameObject.SetActive(false);else SetWork(TileDataExtensions.CalculateWork(value.Tile!.Data!,GameManager.GameState,GameManager.LocalPlayer,1));}
        catch(Exception e){error=e;throw;}finally{Deception.PolyModAdapter.NativeBlackmailEmbassyUi.Finished(error,scope);}
    }
    public void SetWork(int value){Text=value.ToString();workContainer.gameObject.SetActive(value>0);}
}
public static class TileDataExtensions
{
    public static int NativeEmbassyIncome;
    public static int CalculateWork(TileData tile,GameState state,PlayerState player,int level)
    {int value=tile.owner==player.Id?5:NativeEmbassyIncome;Deception.PolyModAdapter.NativeBlackmailEmbassyUi.Income(tile,state,player,true,ref value);return value;}
}
public sealed class CityRenderer:UnityEngine.Component
{
    public WorldCoordinates Coordinates {get;set;}=new();public bool dataChanged {get;set;}
    public UnityEngine.GameObject city {get;set;}=new("Native city");
    public Il2CppSystem.Collections.Generic.List<CityPlot> plots {get;set;}=new();
    public Il2CppSystem.Collections.Generic.List<byte> PlayerEmbassies {get;set;}=new();
    public int RngCalls;
}
public sealed class CityPlot:UnityEngine.Component
{
    public int floors {get;set;}public float scale {get;set;}=1;public float pixelsPerUnit {get;set;}=100;
    public Il2CppSystem.Collections.Generic.List<PolytopiaSpriteRenderer> houses {get;set;}=new();
    public PolytopiaSpriteRenderer AddNativeHouse(string name,float height=.3f)
    {
        var r=new PolytopiaSpriteRenderer();r.transform.SetParent(transform,false);r.transform.localPosition=new(0,floors*.2f,0);
        r.SortingLayer=7;r.SortingOrder=10+floors;r.gameObject.name=name;
        var bottom=transform.TransformPoint(new(-.12f,floors*.2f,0));var top=transform.TransformPoint(new(.12f,floors*.2f+height,0));
        r.Bounds=new(new((bottom.x+top.x)/2,(bottom.y+top.y)/2,(bottom.z+top.z)/2),new(Math.Abs(top.x-bottom.x),Math.Abs(top.y-bottom.y),Math.Abs(top.z-bottom.z)));
        houses.Add(r);floors++;return r;
    }
}
namespace Deception.PolyModAdapter
{
    // Gameplay is verified by the separate real NativeBlackmail transaction
    // fixture. This port exposes only offers/records, and counts UI re-queries.
    internal static class NativeBlackmail
    {
        internal const int BaseCost=2;internal static bool ReadingGrossIncome;
        internal static LedgerBlackmail[] Records=Array.Empty<LedgerBlackmail>();
        internal static bool Allowed=true,IncomePaused;internal static int Queries,AllQueries,ContractQueries;internal static uint Offered;
        internal static bool TryOffer(object state,byte actor,object unit,out NativeExchangeIntent? intent,out string reason)
            =>TryOffer(state,actor,unit,out intent,out _,out reason);
        internal static bool TryOffer(object state,byte actor,object unit,out NativeExchangeIntent? intent,out int cost,out string reason)
        {
            Queries++;reason="";var body=(UnitState)unit;var game=(GameState)state;
            cost=BaseCost;
            bool ok=Allowed&&game.Players[actor].Espionage&&actor==game.CurrentPlayer&&body.owner!=actor&&game.Players[actor].Currency>=cost&&!Records.Any(x=>x.Blackmailer==actor&&x.UnitId==body.id&&x.LastAppliedTurn==game.CurrentTurn);
            intent=ok?NativeExchangeCommands.Intent(NativeExchangeKind.Blackmail,++Offered,0):null;return ok;
        }
        internal static LedgerBlackmail[] Contracts(object state){ContractQueries++;return Records.ToArray();}
        internal static LedgerBlackmail[] All(object state){AllQueries++;return IncomePaused?Array.Empty<LedgerBlackmail>():Records.ToArray();}
        internal static object? ResolveUnit(object state,LedgerBlackmail row)=>((GameState)state).Units.TryGetValue((uint)row.UnitId,out var body)?body:null;
        internal static LedgerBlackmail[] ForUnit(object state,object unit)=>Records.Where(r=>r.UnitId==((UnitState)unit).id).ToArray();
        internal static LedgerBlackmail[] At(object state,object tile)
        {var t=(TileData)tile;return Records.Where(r=>r.X==t.coordinates.X&&r.Y==t.coordinates.Y&&r.Victim==t.owner).ToArray();}
        internal static void Set(params LedgerBlackmail[] records){Records=records;NativeGameStateEnvelope.Invalidate();}
        internal static void Reset(){Records=Array.Empty<LedgerBlackmail>();Allowed=true;IncomePaused=false;Queries=AllQueries=ContractQueries=0;Offered=0;NativeGameStateEnvelope.Invalidate();}
    }
}
