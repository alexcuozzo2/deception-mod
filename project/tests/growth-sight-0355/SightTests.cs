using System;
using System.Linq;
using System.Text.Json;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static class SightTests
 {
  static int Cases,Assertions;
  static void Check(bool condition,string message){Assertions++;if(!condition)throw new Exception(message);}
  static void Case(Action test){Cases++;test();}
  sealed record Rig(GameState State,UnitState Unit,Il2CppSystem.Collections.Generic.List<ActionBase> Original,ActionBase[] Prefix);
  static Rig Make(int sight=2,bool mountain=false,bool dirty=false)
  {
   ActionUtils.Calls=ActionUtils.ExecuteCalls=0;ActionUtils.Failure="";ActionUtils.FailOnCall=1;ActionUtils.Ranges.Clear();NativeRegroupSession.Enabled=true;
   var state=new GameState();NativeRegroupSession.Owned=state;
   for(int x=-2;x<=2;x++)for(int y=-2;y<=2;y++)state.Map.Tiles.Add((x,y),new(){coordinates=new(x,y)});
   var unit=new UnitState();unit.UnitData.Sight=sight;state.Map.Tiles[(0,0)].unit=unit;state.Map.Tiles[(0,0)].Seen.Add(1);
   state.Map.Tiles[(0,0)].terrain=mountain?4:0;state.Map.Graph.Add(unit.id,unit);
   var prefix=new[]{new ActionBase{Kind="deferred routes"},new ActionBase{Kind="current EndTurn"},new ActionBase{Kind="previous child"}};
   foreach(var action in prefix)state.ActionStack.Add(action);state.HasFlaggedNeedForUpdateRoutes=dirty;
   return new(state,unit,state.ActionStack,prefix);
  }
  static bool Throws(Action test){try{test();return false;}catch{return true;}}
  static object[] Units(Rig rig)=>new object[]{rig.Unit};
  static string Real(Rig r)=>$"{r.Unit.Pointer}/{r.Unit.id}/{r.Unit.owner}/{r.Unit.health}/{r.Unit.type}/{r.Unit.UnitData.Pointer}/{r.Unit.coordinates.X}/{r.Unit.coordinates.Y}/{r.Unit.Moved}/{r.Unit.Attacked}/{r.Unit.Investment}/{r.Unit.CreatedTurn}/{r.Unit.Effects.GetHashCode()}/{r.State.Stars}/{r.State.Score}/{r.State.CurrentUnitId}/{r.State.Births}/{r.State.Deaths}/"+
   string.Join(";",r.State.Map.Tiles.Values.OrderBy(t=>t.coordinates.X).ThenBy(t=>t.coordinates.Y).Select(t=>$"{t.coordinates.X},{t.coordinates.Y}:{string.Join(',',t.Seen.OrderBy(x=>x))}"));
  static void Restored(Rig r,bool dirty,string real)
  {
   Check(ReferenceEquals(r.State.ActionStack,r.Original)&&r.Original.Count==r.Prefix.Length,"Original queue reference/count was not restored");
   Check(Enumerable.Range(0,r.Prefix.Length).All(i=>ReferenceEquals(r.Original[i],r.Prefix[i])),"Original queue identities/order changed");
   Check(r.State.HasFlaggedNeedForUpdateRoutes==dirty,"Original route flag was not restored");
   Check(Real(r)==real&&ActionUtils.ExecuteCalls==0,"Queue staging changed fog, unit identity/health/actions, birth counter, investment, stars or score");
  }
  public static int Run()
  {
   try
   {
    foreach(int range in new[]{1,2})foreach(bool mountain in new[]{false,true})Case(()=>{
     var r=Make(range,mountain);string real=Real(r);int count=range==2||mountain?24:8;
     using(var pending=NativeGrowthSight.Stage(r.State,1,Units(r)))
     {
      Check(pending.AddedCount==count&&r.State.ActionStack.Count==r.Prefix.Length+count,"Wrong operative range/mountain footprint");
      Check(!ReferenceEquals(r.State.ActionStack,r.Original)&&r.State.HasFlaggedNeedForUpdateRoutes,"No owned cloned queue or route invalidation");
      Check(ActionUtils.Calls==1&&ActionUtils.Ranges.Single()==range,"Native ExploreFromTile did not retain actual data range");
      Check(Real(r)==real&&r.Original.Count==3,"Staging executed children or mutated original stack/state");
      var expected=MapDataExtensions.GetAreaSorted(r.State.Map,r.Unit.coordinates,mountain?2:range,true,true).Values.Reverse().Where(t=>!t.GetExplored(1)).ToArray();
      Check(expected.Select((t,i)=>r.State.ActionStack[i+3] is ExploreAction e&&e.PlayerId==1&&ReferenceEquals(e.Coordinates,t.coordinates)).All(x=>x),"Native reverse footprint order/owner differs");
      pending.Accept();
     }
     Check(r.State.ActionStack.Count==count+3&&ActionUtils.ExecuteCalls==0,"Accepted queue was disposed or executed");
     // Native owner transition retains older-owner Explore children. Its
     // current action is removed by index; StartTurn is appended separately.
     r.State.ActionStack.Add(new(){Kind="StartTurn2"});r.State.CurrentPlayer=2;r.State.ActionStack.RemoveAt(1);
     Check(r.State.ActionStack.Count==count+3&&Enumerable.Range(2,count).All(i=>r.State.ActionStack[i] is ExploreAction e&&e.PlayerId==1),"End-turn prefix removal lost queued old-owner children");
    });
    foreach(bool dirty in new[]{false,true})Case(()=>{
     var r=Make(dirty:dirty);string real=Real(r);using(var pending=NativeGrowthSight.Stage(r.State,1,Units(r))){pending.AssertPending();}
     Restored(r,dirty,real);
    });
    Case(()=>{
     var r=Make();string real=Real(r);using(var pending=NativeGrowthSight.Stage(r.State,1,Array.Empty<object>())){Check(pending.AddedCount==0&&ActionUtils.Calls==0,"Empty phase queued sight");pending.Accept();}
     Restored(r,false,real);
    });
    Case(()=>{
     var r=Make();foreach(var tile in r.State.Map.Tiles.Values)tile.Seen.Add(1);string real=Real(r);
     using var pending=NativeGrowthSight.Stage(r.State,1,Units(r));Check(pending.AddedCount==0&&ActionUtils.Calls==1&&r.State.HasFlaggedNeedForUpdateRoutes&&Real(r)==real,"Fully explored growth did not retain native zero-child/dirty behavior");pending.Accept();
    });
    Case(()=>{
     var r=Make();var b=new UnitState{id=8,coordinates=new(1,0)};r.State.Map.Graph.Add(b.id,b);r.State.Map.Tiles[(1,0)].unit=b;r.State.Map.Tiles[(1,0)].Seen.Add(1);string real=Real(r);
     using var pending=NativeGrowthSight.Stage(r.State,1,new object[]{r.Unit,b});
     var aTiles=MapDataExtensions.GetAreaSorted(r.State.Map,r.Unit.coordinates,2,true,true).Values.Where(t=>!t.GetExplored(1));
     var bTiles=MapDataExtensions.GetAreaSorted(r.State.Map,b.coordinates,2,true,true).Values.Where(t=>!t.GetExplored(1));
     int expected=aTiles.Count()+bTiles.Count();var children=Enumerable.Range(3,expected).Select(i=>(ExploreAction)r.State.ActionStack[i]).ToArray();
     Check(pending.AddedCount==expected&&ActionUtils.Calls==2,"Multiple changed sources not queued");
     Check(children.Select(e=>e.Pointer).Distinct().Count()==expected&&children.Select(e=>(e.Coordinates.X,e.Coordinates.Y)).Distinct().Count()<expected,"Overlap was deduplicated or action objects reused");
     Check(Real(r)==real&&b.health==10&&b.Moved&&b.Attacked,"Multi-source stage changed bodies/state");pending.Accept();
    });
    foreach(bool sourceCovered in new[]{false,true})foreach(bool rollback in new[]{false,true})Case(()=>{
     var r=Make();r.State.Map.Tiles[(2,0)].Covered.Add(1);r.State.Map.Tiles[(2,1)].Covered.Add(1);
     if(sourceCovered)r.State.Map.Tiles[(0,0)].Covered.Add(1);string real=Real(r);
     using(var pending=NativeGrowthSight.Stage(r.State,1,Units(r)))
     {
      int expected=sourceCovered?0:22;
      Check(pending.AddedCount==expected&&r.State.ActionStack.Count==r.Prefix.Length+expected,"Blackout blocked discovery children were counted or covered-source sight ran");
      Check(ActionUtils.Calls==(sourceCovered?0:1)&&Real(r)==real,"Passive growth changed hidden tiles or explored from covered body");
      Check(!Enumerable.Range(3,r.State.ActionStack.Count-3).Select(i=>r.State.ActionStack[i]).OfType<ExploreAction>().Any(e=>r.State.Map.Tiles[(e.Coordinates.X,e.Coordinates.Y)].Covered.Contains(1)),"Passive growth queued a forbidden Blackout reveal");
      if(!rollback)pending.Accept();
     }
     if(rollback||sourceCovered)Restored(r,false,real);
     else Check(r.State.ActionStack.Count==25&&r.State.Map.Tiles[(2,0)].Covered.Contains(1),"Accepted normal sight lost queue or cleared Blackout");
    });
    Case(()=>{
     var r=Make();foreach(var tile in r.State.Map.Tiles.Values)if(tile.unit is null)tile.Covered.Add(1);string real=Real(r);
     using var pending=NativeGrowthSight.Stage(r.State,1,Units(r));
     Check(pending.AddedCount==0&&r.State.ActionStack.Count==3&&ActionUtils.Calls==1,"All-covered footprint should retain a valid zero-child native stage");
     Check(Real(r)==real,"Zero-child growth affected fog/body");pending.Accept();
    });
    foreach(string denial in new[]{"disabled","unowned","version","wrong-actor","missing-player","wrong-player-id","wrong-unit-owner","dead","foreign-unit","not-root","missing-root","missing-tile","backlink","unexplored-source","data-type","range0","range3","duplicate-source","null-source","oversize-area","empty-area","foreign-area","null-area","large-prefix","large-sources"})Case(()=>{
     var r=Make();object[] units=Units(r);byte owner=1;
     switch(denial)
     {
      case "disabled":NativeRegroupSession.Enabled=false;break;case "unowned":NativeRegroupSession.Owned=new();break;
      case "version":r.State.Version=122;break;case "wrong-actor":owner=2;break;case "missing-player":r.State.Players.Remove(1);break;case "wrong-player-id":r.State.Players[1].Id=2;break;
      case "wrong-unit-owner":r.Unit.owner=2;break;case "dead":r.Unit.health=0;break;case "foreign-unit":units=new object[]{new UnitState()};break;
      case "not-root":r.Unit.Root=false;break;case "missing-root":r.State.Map.Graph.Clear();break;case "missing-tile":r.Unit.coordinates=new(99,99);break;
      case "backlink":r.State.Map.Tiles[(0,0)].unit=new();break;case "unexplored-source":r.State.Map.Tiles[(0,0)].Seen.Clear();break;
      case "data-type":r.Unit.UnitData.type=19;break;case "range0":r.Unit.UnitData.Sight=0;break;case "range3":r.Unit.UnitData.Sight=3;break;
      case "duplicate-source":units=new object[]{r.Unit,r.Unit};break;case "null-source":units=new object[]{null!};break;
      case "oversize-area":r.State.Map.AreaFailure="oversize";break;case "empty-area":r.State.Map.AreaFailure="empty";break;case "foreign-area":r.State.Map.AreaFailure="foreign";break;case "null-area":r.State.Map.AreaFailure="null";break;
      case "large-prefix":for(int i=3;i<16385;i++)r.Original.Add(new());break;case "large-sources":units=Enumerable.Repeat((object)r.Unit,4097).ToArray();break;
     }
     string real=Real(r);int count=r.Original.Count;
     Check(Throws(()=>NativeGrowthSight.Stage(r.State,owner,units)),"Rejected context was accepted: "+denial);
     Check(ReferenceEquals(r.State.ActionStack,r.Original)&&r.Original.Count==count&&!r.State.HasFlaggedNeedForUpdateRoutes&&Real(r)==real&&ActionUtils.Calls==0,"Rejected context changed native state: "+denial);
    });
    foreach(bool dirty in new[]{false,true})foreach(string failure in new[]{"stack-before","stack-after","dirty-before","dirty-after","before","after-one","owner","coordinate","type","reuse","skip","reverse","prefix"})Case(()=>{
     var r=Make(dirty:dirty);string real=Real(r);
     if(failure.StartsWith("stack")||failure.StartsWith("dirty"))r.State.Failure=failure;else ActionUtils.Failure=failure;
     Check(Throws(()=>NativeGrowthSight.Stage(r.State,1,Units(r))),"Injected stage failure not rejected: "+failure);Restored(r,dirty,real);
    });
    Case(()=>{
     var r=Make();var b=new UnitState{id=8,coordinates=new(1,0)};r.State.Map.Graph.Add(b.id,b);r.State.Map.Tiles[(1,0)].unit=b;r.State.Map.Tiles[(1,0)].Seen.Add(1);string real=Real(r);
     ActionUtils.Failure="after-one";ActionUtils.FailOnCall=2;
     Check(Throws(()=>NativeGrowthSight.Stage(r.State,1,new object[]{r.Unit,b}))&&ActionUtils.Calls==2,"Second-source partial append failure absent");Restored(r,false,real);
    });
    Case(()=>{
     var r=Make();ActionUtils.Failure="fog";
     Check(Throws(()=>NativeGrowthSight.Stage(r.State,1,Units(r))),"Unexpected native fog execution not detected");
     Check(ReferenceEquals(r.State.ActionStack,r.Original)&&!r.State.HasFlaggedNeedForUpdateRoutes,"Fog-contract violation lost original queue/dirty state");
     // Unexpected native fog writes are a fatal contract violation; this
     // helper intentionally has no fog setter and does not claim to undo them.
    });
    Case(()=>{
     var r=Make();using var pending=NativeGrowthSight.Stage(r.State,1,Units(r));pending.Accept();
     Check(Throws(pending.Rollback),"Committed queue rolled back after accept");
    });
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,scope="synthetic-native-growth-sight-queue"}));return 0;
   }
   catch(Exception error){Console.WriteLine(error);return 1;}
  }
 }
}
