// Exercises the real Blackout route scope/read adapter. Topology is an explicit
// small native-boundary double, not a reimplementation or a native game test.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static partial class BlackoutTests
 {
  static GameState RouteMap()
  {
   var s=Make();s.TryGetPlayer(2,out var p);p!.Capital=new(1,1);
   foreach(var at in new[]{(1,1),(2,1),(3,1),(4,1)}){var t=Tile(s,at.Item1,at.Item2);t.owner=2;t.terrain=1;}
   Tile(s,1,1).improvement=new(){type=Polytopia.Data.ImprovementData.Type.city};
   Tile(s,3,1).improvement=new(){type=Polytopia.Data.ImprovementData.Type.city};
   return s;
  }
  static void Cover(GameState s,params (int X,int Y)[] cells)
  {
   foreach(var at in cells)Tile(s,at.X,at.Y).explorers!.Remove(2);
   Write(s,Read(s) with{Blackouts=cells.Select(a=>new LedgerBlackout(2,a.X,a.Y)).OrderBy(a=>a.X).ThenBy(a=>a.Y).ToArray()});
  }
  static bool NativeConnection(GameState s,TileData destination,byte owner=2)
  {
   s.TryGetPlayer(owner,out var player);if(player is null)return false;
   var start=MapDataExtensions.GetTile(s.Map,player.Capital);
   bool Allowed(TileData t)=>t.owner==owner&&t.terrain==1&&t.GetExplored(owner);
   if(start is null||!start.HasImprovement(Polytopia.Data.ImprovementData.Type.city)||!Allowed(start)||
      !destination.HasImprovement(Polytopia.Data.ImprovementData.Type.city))return false;
   var seen=new HashSet<TileData>{start};var work=new Queue<TileData>();work.Enqueue(start);
   while(work.Count>0){var t=work.Dequeue();if(ReferenceEquals(t,destination))return true;
    foreach(var n in s.Map.Tiles.Values.Where(n=>Math.Abs(n.coordinates.X-t.coordinates.X)+Math.Abs(n.coordinates.Y-t.coordinates.Y)==1))
     if(Allowed(n)&&seen.Add(n))work.Enqueue(n);}
   return false;
  }
  static T Routing<T>(GameState s,Func<T> body,bool run=true)
  {
   NativeBlackout.BeforeRoutes(s,run,out var scope);Exception? error=null;
   try{return body();}catch(Exception e){error=e;throw;}finally{Check(ReferenceEquals(NativeBlackout.RoutesFinished(error,scope),error),"finalizer preserves original exception");}
  }
  static void FogUnchanged(GameState s,TileData tile,object? explorers,object snapshot,int actions)
  {
   Check(!tile.GetExplored(2)&&NativeBlackout.Covered(s,2,NativeSpyNetwork.At(tile.coordinates)),"outside routing tile remains covered");
   Check(ReferenceEquals(tile.explorers,explorers)&&ReferenceEquals(NativeGameStateEnvelope.Get(s),snapshot)&&s.ActionStack.Count==actions,"routing edits no explorer, ledger or action queue");
  }
  public static int RunConnections()
  {
   try
   {
    Test("route-hook-registered-without-value-type-ABI",()=>{var h=new HarmonyLib.Harmony();NativeBlackout.InstallRoutes(h);Check(h.Hooks.Count(x=>x=="MapDataExtensions.UpdateRoutes")==1,"one synchronous route hook installed");Check(!h.Hooks.Any(x=>x=="PathFinder.IsTileAccessible"),"no unsafe settings-value hook");});
    Test("committed-blackout-preserves-owner-city-connection",()=>
    {
     var s=RouteMap();var city=Tile(s,3,1);Check(NativeConnection(s,city),"native fixture initially connected");Cast(s,3,1);
     var mask=city.explorers;var snapshot=NativeGameStateEnvelope.Get(s)!;int actions=s.ActionStack.Count;
     Check(!NativeConnection(s,city),"unpatched native explored gate demonstrates regression");
     Check(Routing(s,()=>NativeConnection(s,city)),"covered owner city is routable without changing topology");
     FogUnchanged(s,city,mask,snapshot,actions);Add(s,88,3,1,1);Check(city.GetUnit(s,2,false) is null,"hidden enemy still cannot be seen");
    });
    foreach(string target in new[]{"capital","destination","both"})Test("covered-city-route-"+target,()=>
    {
     var s=RouteMap();Cover(s,target=="both"?new[]{(1,1),(3,1)}:target=="capital"?new[]{(1,1)}:new[]{(3,1)});
     Check(Routing(s,()=>NativeConnection(s,Tile(s,3,1))),"Blackout does not sever city route");
     Check(!Tile(s,target=="destination"?3:1,1).GetExplored(2),"fog remains outside native calculation");
    });
    foreach(string change in new[]{"road-removed","road-terrain","city-owner","capital-owner","city-removed","capital-moved"})Test("real-network-change-still-disconnects-"+change,()=>
    {
     var s=RouteMap();Cover(s,(3,1));Check(Routing(s,()=>NativeConnection(s,Tile(s,3,1))),"initial covered city connected");
     switch(change){case"road-removed":Tile(s,2,1).terrain=0;break;case"road-terrain":Tile(s,2,1).terrain=2;break;case"city-owner":Tile(s,3,1).owner=1;break;case"capital-owner":Tile(s,1,1).owner=1;break;case"city-removed":Tile(s,3,1).improvement=null;break;case"capital-moved":s.TryGetPlayer(2,out var p);p!.Capital=new(7,7);break;}
     Check(!Routing(s,()=>NativeConnection(s,Tile(s,3,1))),"actual route rule, not cached connection, controls outcome");Check(!Tile(s,3,1).GetExplored(2),"network changes do not reveal city");
    });
    Test("roads-restored-reconnect-covered-city",()=>{var s=RouteMap();Cover(s,(3,1));Tile(s,2,1).terrain=0;Check(!Routing(s,()=>NativeConnection(s,Tile(s,3,1))),"broken path stays broken");Tile(s,2,1).terrain=1;Check(Routing(s,()=>NativeConnection(s,Tile(s,3,1))),"newly valid connection recomputed");});
    foreach(string guard in new[]{"foreign","neutral","removed-city","uncovered-unexplored","ordinary-covered-road","detached-tile","wrong-player","inactive-session","disabled","missing-frame","skipped-native"})Test("route-visibility-exemption-guard-"+guard,()=>
    {
     var s=RouteMap();var city=Tile(s,3,1);Cover(s,(3,1));object? copy=null;byte player=2;
     switch(guard){case"foreign":city.owner=1;break;case"neutral":city.owner=0;break;case"removed-city":case"ordinary-covered-road":city.improvement=null;break;case"uncovered-unexplored":Write(s,Read(s) with{Blackouts=Array.Empty<LedgerBlackout>()});break;case"detached-tile":copy=city.MemberwiseClone();break;case"wrong-player":player=3;city.explorers!.Remove(3);break;case"inactive-session":NativeRegroupSession.Owned.Clear();break;case"disabled":NativeRegroupSession.Enabled=false;break;case"missing-frame":NativeGameStateEnvelope.Values.Clear();break;}
     try{Check(!Routing(s,()=>((TileData?)copy??city).GetExplored(player),guard!="skipped-native"),"guard refuses synthetic exploration");}
     finally{NativeRegroupSession.Enabled=true;}
    });
    foreach(string change in new[]{"map","frame","turn","session","clear","owner","tile-replaced"})Test("route-scope-stale-"+change,()=>
    {
     var s=RouteMap();var city=Tile(s,3,1);Cover(s,(3,1));
     Check(!Routing(s,()=>{switch(change){case"map":s.Map=new();break;case"frame":Write(s,Read(s));break;case"turn":s.CurrentTurn++;break;case"session":NativeRegroupSession.Owned.Clear();break;case"clear":NativeBlackout.Clear();break;case"owner":city.owner=1;break;case"tile-replaced":s.Map.Tiles.Values=s.Map.Tiles.Values.Select(t=>ReferenceEquals(t,city)?(TileData)city.MemberwiseClone():t).ToArray();break;}return city.GetExplored(2);}),"changed scope never authorizes old tile");
     Check(!city.GetExplored(2),"stale completion cannot leave city explored");
    });
    Test("nested-route-scopes-isolate-states-and-restore",()=>
    {
     var a=RouteMap();var b=RouteMap();Cover(a,(3,1));Cover(b,(3,1));var ta=Tile(a,3,1);var tb=Tile(b,3,1);
     Check(Routing(a,()=>{Check(ta.GetExplored(2)&&!tb.GetExplored(2),"outer scope is state-bound");Routing(b,()=>{Check(tb.GetExplored(2)&&!ta.GetExplored(2),"inner scope replaces outer authority");return 0;});return ta.GetExplored(2)&&!tb.GetExplored(2);}),"outer restored after nested call");
     Check(!ta.GetExplored(2)&&!tb.GetExplored(2),"both scopes unwind");
    });
    Test("nested-suppressed-or-failed-setup-masks-outer",()=>
    {
     var s=RouteMap();Cover(s,(3,1));var tile=Tile(s,3,1);
     Routing(s,()=>{Check(!Routing(s,()=>tile.GetExplored(2),false),"suppressed nested route cannot inherit caller permission");
      NativeBlackout.BeforeRoutes(new object(),true,out var inert);try{Check(!tile.GetExplored(2),"invalid nested setup masks outer scope");}finally{NativeBlackout.RoutesFinished(null,inert);}
      Check(tile.GetExplored(2),"outer restored after invalid setup");return 0;});Check(!tile.GetExplored(2),"outer closes");
    });
    Test("route-finalizer-preserves-exception-and-cleans-scope",()=>{var s=RouteMap();Cover(s,(3,1));var error=new InvalidOperationException("native failed");try{Routing<int>(s,()=>throw error);}catch(Exception e){Check(ReferenceEquals(e,error),"native failure not replaced");}Check(!Tile(s,3,1).GetExplored(2),"error unwinds permission");});
    Test("closed-token-cannot-reactivate-through-nesting",()=>{var s=RouteMap();Cover(s,(3,1));NativeBlackout.BeforeRoutes(s,true,out var a);NativeBlackout.BeforeRoutes(s,true,out var b);NativeBlackout.RoutesFinished(null,a);NativeBlackout.RoutesFinished(null,b);Check(!Tile(s,3,1).GetExplored(2),"already closed outer remains inert");NativeBlackout.Clear();});
    var result=new{passed=true,cases=Cases.Count,assertions=Assertions,tests=Cases,scope="Production Blackout adapter with explicit synchronous route topology ports; no native detour or game execution."};Console.WriteLine(JsonSerializer.Serialize(result));return 0;
   }
   catch(Exception e){Console.WriteLine(e);return 1;}
  }
 }
}
