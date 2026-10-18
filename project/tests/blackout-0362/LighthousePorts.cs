// Native semantics audited from the pinned GameLogicAssembly + Intel image.
using System;
using System.Linq;
using Deception.PolyModAdapter;
public sealed class ImprovementState:Il2CppSystem.Object
{
 public short DisplayedPopulation{get;set;}=4;public int RenderRefreshes;public Polytopia.Data.ImprovementData.Type type{get;set;}public short population{get;set;}public short xp{get;set;}public ushort level{get;set;}=1;public ushort borderSize{get;set;}=1;
 public void AddPopulation(short change){population=checked((short)(population+change));xp=checked((short)(xp+change));Fault.Check("population");}
}
public class TaskBase:Il2CppSystem.Object{public bool IsStarted{get;set;}public bool IsCompleted{get;set;}}
public static class ScoreSheet{public static int exploreValue{get;}=5;}
public static class ScoreExtensions{public static int CalculateImprovementScore(GameState state,TileData tile)=>tile.improvement!.xp*10+tile.improvement.level*100;}
public static class PlayerExtensions{public static WorldCoordinates GetCurrentCapitalCoordinates(PlayerState player,GameState state)=>player.Capital;}
public sealed class DecreasePopulationAction:ActionBase
{
 public WorldCoordinates Target{get;set;}public DecreasePopulationAction(byte playerId,WorldCoordinates target,int delay){PlayerId=playerId;Target=target;}
 public void Execute(GameState state){NativeBlackoutLighthouse.BeforeDecrease(this,state,out var trace);Exception? error=null;try{var tile=MapDataExtensions.GetTile(state.Map,Target)!;if(!tile.HasImprovement(Polytopia.Data.ImprovementData.Type.city))return;state.TryGetPlayer(tile.owner,out var p);int before=ScoreExtensions.CalculateImprovementScore(state,tile);tile.improvement!.AddPopulation(-1);ActionUtils.RemoveScore(p!,before-ScoreExtensions.CalculateImprovementScore(state,tile));NativeBlackoutLighthouse.AfterDecrease(trace);}catch(Exception e){error=e;throw;}finally{NativeBlackoutLighthouse.DecreaseFinished(error,trace);}}
}
public sealed class IncreasePopulationAction:ActionBase
{
 public WorldCoordinates Source{get;set;}public WorldCoordinates Target{get;set;}public IncreasePopulationAction(byte actor,WorldCoordinates source,WorldCoordinates target){PlayerId=actor;Source=source;Target=target;}
 public void Execute(GameState s){var t=MapDataExtensions.GetTile(s.Map,Target)!;int before=ScoreExtensions.CalculateImprovementScore(s,t);t.improvement!.AddPopulation(1);s.ActionStack.Add(new IncreaseScoreAction(PlayerId,ScoreExtensions.CalculateImprovementScore(s,t)-before,Target));}
}
public sealed class IncreaseScoreAction:ActionBase
{
 public WorldCoordinates Source{get;set;}public int Amount{get;set;}public IncreaseScoreAction(byte actor,int amount,WorldCoordinates at){PlayerId=actor;Amount=amount;Source=at;}
 public void Execute(GameState s){s.TryGetPlayer(PlayerId,out var p);ActionUtils.AddScore(s,p!,Amount);}
}
public sealed class ImprovementLevelUpAction:ActionBase
{
 public WorldCoordinates Coordinates{get;set;}public ImprovementLevelUpAction(byte actor,WorldCoordinates at){PlayerId=actor;Coordinates=at;}
 public void Execute(GameState s)=>MapDataExtensions.GetTile(s.Map,Coordinates)!.improvement!.level++;
}
public sealed class TaskCompletedAction:ActionBase{public TaskCompletedAction(byte actor){PlayerId=actor;}}
public static class ExploreLightHouseTask
{
 public static bool ShouldUseLighthouseTasks(GameState s)=>s.Lighthouses;
 public static bool EvaluateTile(TileData t,GameState s)=>t.coordinates.X is 0 or 11&&t.coordinates.Y is 0 or 7;
 public static int Count(GameState s,byte actor)=>s.Map.Tiles.Values.Count(t=>EvaluateTile(t,s)&&t.GetExplored(actor));
}
public static partial class ActionUtils
{
 public static void RemoveScore(PlayerState p,int amount){p.score=unchecked(p.score-(uint)amount);Fault.Check("score");}
 public static void AddScore(GameState s,PlayerState p,int amount){p.score=unchecked(p.score+(uint)amount);}
 public static void OnRevealLighthouseTile(GameState s,PlayerState p,WorldCoordinates at)
 {
  if(!ExploreLightHouseTask.ShouldUseLighthouseTasks(s))return;
  var task=Native.Items(p.tasks).Cast<TaskBase>().Single();if(!task.IsCompleted&&ExploreLightHouseTask.Count(s,p.Id)==4){task.IsCompleted=true;s.ActionStack.Add(new TaskCompletedAction(p.Id));}task.IsStarted=true;
  if(MapDataExtensions.GetTile(s.Map,p.Capital) is not null)s.ActionStack.Add(new IncreasePopulationAction(p.Id,at,p.Capital));s.ActionStack.Add(new ImprovementLevelUpAction(p.Id,at));Fault.Check("lighthouse-reveal");
 }
}

// Client dispatch creates this only for queued native actions. An inline logic
// Execute call cannot refresh the native city population display.
public sealed class DecreasePopulationReaction
{
 readonly DecreasePopulationAction Action;public DecreasePopulationReaction(DecreasePopulationAction action){Action=action;}
 public void Execute(GameState state){var city=MapDataExtensions.GetTile(state.Map,Action.Target)!.improvement!;city.DisplayedPopulation=city.population;city.RenderRefreshes++;}
}
