using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static partial class BlackoutTests
 {
  static TileData DiscoveryCity(GameState s,int x=2,int y=2,ushort radius=2)
  {var c=Tile(s,x,y);c.owner=2;c.improvement=new(){type=Polytopia.Data.ImprovementData.Type.city,borderSize=radius};return c;}
  static void CoverDiscovery(GameState s,params (int X,int Y)[] cells)
  {
   foreach(var at in cells)Tile(s,at.X,at.Y).explorers!.Remove(2);
   Write(s,Read(s) with{Blackouts=cells.Select(a=>new LedgerBlackout(2,a.X,a.Y)).OrderBy(a=>a.X).ThenBy(a=>a.Y).ToArray()});
  }
  static ScoutMoveAction RewardScout(GameState s,bool covered=false)
  {
   s.CurrentPlayer=2;var c=DiscoveryCity(s,1,2);if(covered)CoverDiscovery(s,(1,2),(2,1));
   var reward=new CityRewardAction{PlayerId=2,Coordinates=c.coordinates,Reward="Explorer"};s.ActionStack.Add(reward);reward.Execute(s);
   return Native.Items(s.ActionStack).OfType<ScoutMoveAction>().Single();
  }
  static void RunQueuedSource(GameState s,SourceAction a,bool run=true)
  {
   NativeBlackoutDiscovery.BeforeSource(a,s,a.GetType().GetMethod("Execute")!,out var source,run);Exception? error=null;
   try{if(run)a.Execute(s);NativeBlackoutDiscovery.AfterSource(run,source);}catch(Exception e){error=e;throw;}
   finally{var result=NativeBlackoutDiscovery.SourceFinished(error,source);if(error is null&&result is not null)throw result;}
  }
  static void NativeDiscoveryCases()
  {
   foreach(string occupant in new[]{"empty","enemy","own-range-two","rogue","covered"})Test("native-explorer-has-no-body-"+occupant,()=>
   {
    var s=Make();Cast(s,2,1);var a=RewardScout(s,occupant=="covered");a.NextTarget=new(2,2);
    if(occupant is not "empty" and not "covered"){var body=Add(s,77,2,2,occupant=="own-range-two"?(byte)2:(byte)1);body.UnitData.Sight=2;if(occupant=="rogue")body.effects.Add(UnitEffect.Frozen);}
    Check(!s.TryGetUnit(a.UnitId,out _),"native reward id has no UnitState");RunQueuedSource(s,a);
    Check(Read(s).Discoveries!.Contains(new(2,2,1)),"explorer grants precise native child despite unrelated occupant");Execute(s,Pending(s,2,2,1));
    Check(Tile(s,2,1).GetExplored(2),"explorer rediscovers ordinary fog");if(occupant!="covered")Check(!Tile(s,2,1).GetExplored(3),"other viewer's fog remains");
   });
   Test("native-explorer-mountain-footprint",()=>
   {
    var s=Make();s.CurrentPlayer=2;CoverDiscovery(s,(4,2),(5,2));var a=RewardScout(s);a.NextTarget=new(2,2);Tile(s,2,2).terrain=4;RunQueuedSource(s,a);
    Check(Read(s).Discoveries!.Contains(new(2,4,2))&&!Read(s).Discoveries!.Contains(new(2,5,2)),"native mountain radius two, not beyond");Execute(s,Pending(s,2,4,2));Check(Tile(s,4,2).GetExplored(2)&&!Tile(s,5,2).GetExplored(2),"only chosen native footprint clears");
   });
   Test("native-explorer-continuation-clears-next-footprint",()=>
   {
    var s=Make();s.CurrentPlayer=2;CoverDiscovery(s,(2,1),(4,1));var a=RewardScout(s);a.NextTarget=new(2,2);RunQueuedSource(s,a);Execute(s,Pending(s,2,2,1));
    var next=Native.Items(s.ActionStack).OfType<ScoutMoveAction>().Single(x=>!ReferenceEquals(x,a));next.NextTarget=new(4,2);RunQueuedSource(s,next);Execute(s,Pending(s,2,4,1));
    Check(Tile(s,2,1).GetExplored(2)&&Tile(s,4,1).GetExplored(2)&&!s.TryGetUnit(next.UnitId,out _),"queued continuation retains visual id, independently grants second sight");
   });
   Test("native-explorer-no-target-no-permit",()=>
   {
    var s=Make();Cast(s,2,1);var a=RewardScout(s);RunQueuedSource(s,a);Check(a.RemainingMoves==0&&Read(s).Discoveries!.Length==0,"native exhausted/no-target route creates no discovery authority");
   });
   Test("native-discovery-inert-unadopted-context-reads-no-state-fields",()=>
   {
    var a=new ExpandCityAction{PlayerId=2};NativeBlackoutDiscovery.BeforeSource(a,new object(),typeof(ExpandCityAction).GetMethod("Execute")!,out var source);
    Check(source is not null&&!source.Active&&source.Map is null,"unadopted native state creates an inert mask without dereferencing Map/Turn/Queue");NativeBlackoutDiscovery.SourceFinished(null,source);
   });
   foreach(string invalid in new[]{"detached","wrong-turn","missing-player","bad-from","retired","suppressed"})Test("native-explorer-source-guard-"+invalid,()=>
   {
    var s=Make();Cast(s,2,1);var a=RewardScout(s);a.NextTarget=new(2,2);
    switch(invalid){case"detached":s.ActionStack.Remove(a);break;case"wrong-turn":s.CurrentPlayer=3;break;case"missing-player":s.CurrentPlayer=a.PlayerId=4;break;case"bad-from":a.From=new(99,99);break;case"retired":NativeRegroupSession.Owned.Clear();break;}
    NativeBlackoutDiscovery.BeforeSource(a,s,typeof(ScoutMoveAction).GetMethod("Execute")!,out var scope,invalid!="suppressed");
    try{Check(scope is not null&&!scope.Active,"invalid native explorer cannot open scope");}finally{NativeBlackoutDiscovery.SourceFinished(null,scope);}
    Check(Read(s).Discoveries!.Length==0&&!Tile(s,2,1).GetExplored(2),"invalid source preserves fog");
   });
   foreach(string invalid in new[]{"wrong-to","detached-tile","wrong-player","wide-flat-range"})Test("native-explorer-footprint-guard-"+invalid,()=>
   {
    var s=Make();Cast(s,2,1);var a=RewardScout(s);a.To=new(2,2);NativeBlackoutDiscovery.BeforeSource(a,s,typeof(ScoutMoveAction).GetMethod("Execute")!,out var scope);
    try{s.TryGetPlayer(invalid=="wrong-player"?(byte)3:(byte)2,out var p);var tile=invalid=="wrong-to"?Tile(s,3,2):Tile(s,2,2);if(invalid=="detached-tile")tile=(TileData)tile.MemberwiseClone();ActionUtils.ExploreFromTile(s,p!,tile,invalid=="wide-flat-range"?2:1,true);NativeBlackoutDiscovery.AfterSource(true,scope);}
    finally{NativeBlackoutDiscovery.SourceFinished(null,scope);}
    Check(Read(s).Discoveries!.Length==0,"only exact native To/player/range can produce permit");
   });
   foreach(string reward in new[]{"Explorer","BorderGrowth"})Test("native-covered-city-reward-discovery-"+reward,()=>
   {
    var s=Make();Cast(s,2,1);var command=Reward(s,reward);Tile(s,2,1).improvement!.borderSize=1;Check(IssueReward(s,command),"A54 pending owned reward valid under Blackout");
    var action=Native.Items(s.ActionStack).OfType<CityRewardAction>().Single();action.Execute(s);
    SourceAction child;if(reward=="Explorer"){var a=Native.Items(s.ActionStack).OfType<ScoutMoveAction>().Single();a.NextTarget=new(2,2);child=a;}else child=Native.Items(s.ActionStack).OfType<ExpandCityAction>().Single();
    RunQueuedSource(s,child);Check(Read(s).Discoveries!.Contains(new(2,2,1)),"legitimate reward child can reveal covered center");Execute(s,Pending(s,2,2,1));Check(Tile(s,2,1).GetExplored(2)&&!Tile(s,2,1).GetExplored(3),"reward restores owner only");
   });
   foreach(bool centerCovered in new[]{false,true})Test("native-border-direct-children-"+centerCovered,()=>
   {
    var s=Make();s.CurrentPlayer=2;var city=DiscoveryCity(s);CoverDiscovery(s,centerCovered?new[]{(2,1),(2,2),(5,2)}:new[]{(2,1),(5,2)});
    var a=new ExpandCityAction{PlayerId=2,Coordinates=city.coordinates};s.ActionStack.Add(a);RunQueuedSource(s,a);
    Check(Read(s).Discoveries!.Contains(new(2,2,1))&&!Read(s).Discoveries!.Contains(new(2,5,2)),"direct child admitted inside actual expanded border only");Execute(s,Pending(s,2,2,1));Check(Tile(s,2,1).GetExplored(2)&&!Tile(s,5,2).GetExplored(2),"native border rediscovery, no global mask deletion");
   });
   Test("native-border-rejects-old-wrong-actor-outside-children",()=>
   {
    var s=Make();s.CurrentPlayer=2;var city=DiscoveryCity(s);CoverDiscovery(s,(2,1),(3,1),(5,2));var old=new ExploreAction{PlayerId=2,Coordinates=new(3,1)};s.ActionStack.Add(old);
    var a=new ExpandCityAction{PlayerId=2,Coordinates=city.coordinates};s.ActionStack.Add(a);
    NativeBlackoutDiscovery.BeforeSource(a,s,typeof(ExpandCityAction).GetMethod("Execute")!,out var source);
    s.ActionStack.Add(new ExploreAction{PlayerId=2,Coordinates=new(2,1)});s.ActionStack.Add(new ExploreAction{PlayerId=3,Coordinates=new(3,1)});s.ActionStack.Add(new ExploreAction{PlayerId=2,Coordinates=new(5,2)});
    NativeBlackoutDiscovery.AfterSource(true,source);NativeBlackoutDiscovery.SourceFinished(null,source);
    Check(Read(s).Discoveries!.SequenceEqual(new[]{new LedgerDiscovery(2,2,1)}),"only fresh matching actor child within native city area is permitted");
   });
   foreach(string invalid in new[]{"detached","foreign-city","not-city","wrong-turn","retired","suppressed"})Test("native-border-source-guard-"+invalid,()=>
   {
    var s=Make();s.CurrentPlayer=2;var city=DiscoveryCity(s);CoverDiscovery(s,(2,1));var a=new ExpandCityAction{PlayerId=2,Coordinates=city.coordinates};if(invalid!="detached")s.ActionStack.Add(a);
    switch(invalid){case"foreign-city":city.owner=3;break;case"not-city":city.improvement=null;break;case"wrong-turn":s.CurrentPlayer=3;break;case"retired":NativeRegroupSession.Owned.Clear();break;}
    NativeBlackoutDiscovery.BeforeSource(a,s,typeof(ExpandCityAction).GetMethod("Execute")!,out var source,invalid!="suppressed");
    s.ActionStack.Add(new ExploreAction{PlayerId=2,Coordinates=new(2,1)});NativeBlackoutDiscovery.AfterSource(invalid!="suppressed",source);NativeBlackoutDiscovery.SourceFinished(null,source);
    Check(Read(s).Discoveries!.Length==0,"rejected border source cannot authorize child");
   });
   foreach(string change in new[]{"map","turn","actor","owner","city","improvement","queue","prefix","clear"})Test("native-border-stale-completion-"+change,()=>
   {
    var s=Make();s.CurrentPlayer=2;var city=DiscoveryCity(s);CoverDiscovery(s,(2,1));var a=new ExpandCityAction{PlayerId=2,Coordinates=city.coordinates};s.ActionStack.Add(a);NativeBlackoutDiscovery.BeforeSource(a,s,typeof(ExpandCityAction).GetMethod("Execute")!,out var source);a.Execute(s);
    switch(change){case"map":s.Map=(MapData)s.Map.MemberwiseClone();break;case"turn":s.CurrentTurn++;break;case"actor":a.PlayerId=3;break;case"owner":city.owner=3;break;case"city":s.Map.Tiles.Values=s.Map.Tiles.Values.Select(t=>ReferenceEquals(t,city)?(TileData)t.MemberwiseClone():t).ToArray();break;case"improvement":city.improvement=(ImprovementState)city.improvement!.MemberwiseClone();break;case"queue":var q=new Il2CppSystem.Collections.Generic.List<ActionBase>();foreach(var item in Native.Items(s.ActionStack).Cast<ActionBase>())q.Add(item);s.ActionStack=q;break;case"prefix":s.ActionStack.Remove(a);s.ActionStack.Add(a);break;default:NativeBlackoutDiscovery.Clear();break;}
    NativeBlackoutDiscovery.AfterSource(true,source);NativeBlackoutDiscovery.SourceFinished(null,source);Check(Read(s).Discoveries!.Length==0,"stale border completion cannot publish discovery");
   });
   foreach(bool explorer in new[]{false,true})Test("native-discovery-failed-source-"+explorer,()=>
   {
    var s=Make();s.CurrentPlayer=2;var city=DiscoveryCity(s);CoverDiscovery(s,(2,1));SourceAction a=explorer?new ScoutMoveAction{PlayerId=2,From=new(1,2),NextTarget=new(2,2),UnitId=901}:new ExpandCityAction{PlayerId=2,Coordinates=city.coordinates};s.ActionStack.Add(a);a.Body=_=>throw new InvalidOperationException("native discovery failed");
    Exception? caught=null;try{RunQueuedSource(s,a);}catch(Exception e){caught=e;}
    Check(caught?.Message=="native discovery failed"&&Read(s).Discoveries!.Length==0,"original failure preserved and no permit published");var child=Pending(s,2,2,1);bool valid=true;NativeBlackoutDiscovery.AfterValid(child,s,ref valid);Check(!valid,"stranded child cannot clear fog after failure");
   });
   Test("native-discovery-denied-nested-source-masks-outer",()=>
   {
    var s=Make();s.CurrentPlayer=2;var city=DiscoveryCity(s);CoverDiscovery(s,(2,1));var outer=new RuleAreaAction{PlayerId=2,Coordinates=city.coordinates};s.ActionStack.Add(outer);NativeBlackoutDiscovery.BeforeSource(outer,s,typeof(RuleAreaAction).GetMethod("Execute")!,out var source);
    var detached=new ScoutMoveAction{PlayerId=2,From=new(1,2),To=new(2,2)};NativeBlackoutDiscovery.BeforeSource(detached,s,typeof(ScoutMoveAction).GetMethod("Execute")!,out var denied);
    s.TryGetPlayer(2,out var p);ActionUtils.ExploreTile(s,p!,Tile(s,2,1),true);NativeBlackoutDiscovery.AfterSource(true,denied);NativeBlackoutDiscovery.SourceFinished(null,denied);Check(!Native.Items(s.ActionStack).OfType<ExploreAction>().Any(),"denied nested action cannot borrow outer explicit territory reveal");
    outer.Execute(s);NativeBlackoutDiscovery.AfterSource(true,source);NativeBlackoutDiscovery.SourceFinished(null,source);Check(Read(s).Discoveries!.Contains(new(2,2,1)),"valid outer native scope restored after nested denial");
   });
   Test("native-rule-area-covered-own-city-uses-helper",()=>
   {
    var s=Make();s.CurrentPlayer=2;var city=DiscoveryCity(s);CoverDiscovery(s,(2,1),(2,2));var a=new RuleAreaAction{PlayerId=2,Coordinates=city.coordinates};s.ActionStack.Add(a);RunQueuedSource(s,a);Execute(s,Pending(s,2,2,1));Check(Tile(s,2,1).GetExplored(2),"queued legitimate territory action still uses helper discovery");
   });
   Test("native-discovery-old-footprint-finalizer-cannot-enter-new-source",()=>
   {
    var s=Make();s.CurrentPlayer=2;var city=DiscoveryCity(s);CoverDiscovery(s,(2,1));var a=new RuleAreaAction{PlayerId=2,Coordinates=city.coordinates};s.ActionStack.Add(a);s.TryGetPlayer(2,out var p);
    NativeBlackoutDiscovery.BeforeSource(a,s,typeof(RuleAreaAction).GetMethod("Execute")!,out var source);NativeBlackoutDiscovery.BeforeFootprint(s,p!,city,1,out var first);NativeBlackoutDiscovery.BeforeFootprint(s,p!,Tile(s,8,5),1,out var old);
    NativeBlackoutDiscovery.Clear();var next=new ScoutMoveAction{PlayerId=2,From=new(1,2)};s.ActionStack.Add(next);NativeBlackoutDiscovery.BeforeSource(next,s,typeof(ScoutMoveAction).GetMethod("Execute")!,out var replacement);
    NativeBlackoutDiscovery.FootprintFinished(null,old);ActionUtils.ExploreTile(s,p!,Tile(s,2,1),true);Check(replacement!.Footprint is null&&!Native.Items(s.ActionStack).OfType<ExploreAction>().Any(),"retired helper finalizer never restores old footprint into another source");
    NativeBlackoutDiscovery.SourceFinished(null,replacement);NativeBlackoutDiscovery.FootprintFinished(null,first);NativeBlackoutDiscovery.SourceFinished(null,source);
   });
   Test("native-border-saved-permit-completes-in-new-state",()=>
   {
    var s=Make();s.CurrentPlayer=2;var city=DiscoveryCity(s);CoverDiscovery(s,(2,1));var a=new ExpandCityAction{PlayerId=2,Coordinates=city.coordinates};s.ActionStack.Add(a);RunQueuedSource(s,a);var saved=NativeGameStateEnvelope.Get(s)!;
    var clone=Make();clone.CurrentPlayer=2;CoverDiscovery(clone,(2,1));NativeGameStateEnvelope.Set(clone,saved.Revision,saved.Bytes());NativeBlackout.Bind(clone);var child=new ExploreAction{PlayerId=2,Coordinates=new(2,1)};clone.ActionStack.Add(child);Execute(clone,child);Check(Tile(clone,2,1).GetExplored(2)&&!Tile(s,2,1).GetExplored(2),"native saved child plus persisted permit rediscovers only reconstructed state");
   });
  }
 }
}
