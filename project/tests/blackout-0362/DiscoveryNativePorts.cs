// Native-boundary contracts, audited from the pinned x64 image in
// run-output/blackout-discovery-0397/native-audit. These are not game tests.
using System.Linq;
using Deception.PolyModAdapter;
public sealed class ScoutMoveAction:SourceAction
{
 public WorldCoordinates From{get;set;}public WorldCoordinates To{get;set;}public WorldCoordinates? NextTarget;
 public uint RemainingMoves{get;set;}=12;
 public override void Execute(GameState s)
 {
  // Native reward explorers reserve an ID and queue continuations, but never
  // insert or look up a UnitState. Selection/RNG stays outside this fixture.
  if(NextTarget is not WorldCoordinates next){To=From;RemainingMoves=0;return;}
  var tile=MapDataExtensions.GetTile(s.Map,next)!;To=tile.coordinates;
  if(RemainingMoves>1)s.ActionStack.Add(new ScoutMoveAction{PlayerId=PlayerId,UnitId=UnitId,From=To,RemainingMoves=RemainingMoves-1});
  s.TryGetPlayer(PlayerId,out var player);ActionUtils.ExploreFromTile(s,player!,tile,tile.terrain==4?2:1,true);Body?.Invoke(s);
 }
}
public sealed class ExpandCityAction:SourceAction
{
 public override void Execute(GameState s)
 {
  var city=MapDataExtensions.GetTile(s.Map,Coordinates)!;
  // The native final loop directly allocates ExploreAction children. It does
  // not call ExploreFromTile/ExploreTile: the old fixture hid this distinction.
  foreach(var tile in Native.Items(MapDataExtensions.GetArea(s.Map,Coordinates,city.improvement!.borderSize,true,true)).Cast<TileData>())
   if(!tile.GetExplored(PlayerId))s.ActionStack.Add(new ExploreAction{PlayerId=PlayerId,Coordinates=tile.coordinates});
  Body?.Invoke(s);
 }
}
public sealed class RuleAreaAction:SourceAction
{
 public override void Execute(GameState s)
 {
  var city=MapDataExtensions.GetTile(s.Map,Coordinates)!;s.TryGetPlayer(PlayerId,out var player);
  foreach(var tile in Native.Items(MapDataExtensions.GetArea(s.Map,Coordinates,city.improvement!.borderSize,true,true)).Cast<TileData>())
   ActionUtils.ExploreTile(s,player!,tile,true);
  Body?.Invoke(s);
 }
}
