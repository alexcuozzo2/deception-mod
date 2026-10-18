// Native-boundary regression: the production hooks run against explicit
// native command/action contracts audited in blackout-fixes-0392/native-audit.
using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;

public sealed class UpgradeAction:SourceAction
{
 public UnitType Type{get;set;}=UnitType.scoutship;
 public int Sight{get;set;}=2;
 public override void Execute(GameState s)
 {
  var tile=MapDataExtensions.GetTile(s.Map,Coordinates)!;var old=tile.unit!;
  // Native TrainUnit + cloneState replaces the object but preserves its id.
  tile.unit=new(){id=old.id,owner=PlayerId,type=Type,coordinates=Coordinates,UnitData=new(){type=Type,Sight=Sight},moved=old.moved,attacked=old.attacked};
  s.TryGetPlayer(PlayerId,out var player);ActionUtils.ExploreFromTile(s,player!,tile,Sight,true);
 }
}
public sealed class CityRewardAction:ActionBase
{
 public WorldCoordinates Coordinates{get;set;}public string Reward{get;set;}="";
 public void Execute(GameState state)
 {
  // Audited reward branches: Explorer reserves a visual id without training;
  // BorderGrowth increments borderSize before queuing ExpandCityAction.
  if(Reward=="Explorer")state.ActionStack.Add(new ScoutMoveAction{PlayerId=PlayerId,UnitId=901,From=Coordinates});
  else if(Reward=="BorderGrowth"){MapDataExtensions.GetTile(state.Map,Coordinates)!.improvement!.borderSize++;state.ActionStack.Add(new ExpandCityAction{PlayerId=PlayerId,Coordinates=Coordinates});}
 }
}
public sealed partial class CityRewardCommand
{
 public string Reward{get;set;}="Giant";
 public static readonly Dictionary<GameState,(byte Owner,WorldCoordinates At)> Pending=new();
 public bool IsValid(GameState state,out string reason)
 {
  reason="";return PlayerId==state.CurrentPlayer&&Pending.TryGetValue(state,out var p)&&p.Owner==PlayerId&&
   p.At.X==Coordinates.X&&p.At.Y==Coordinates.Y&&Reward is "Giant" or "Park" or "Explorer" or "BorderGrowth";
 }
 public void Execute(GameState state)
 {Pending.Remove(state);state.ActionStack.Add(new CityRewardAction{PlayerId=PlayerId,Coordinates=Coordinates,Reward=Reward});}
}
namespace Deception.Tests
{
 public static partial class BlackoutTests
 {
  static CityRewardCommand Reward(GameState s,string reward="Giant")
  {
   s.CurrentPlayer=2;var tile=Tile(s,2,1);tile.owner=2;tile.improvement=new(){type=Polytopia.Data.ImprovementData.Type.city};
   CityRewardCommand.Pending[s]=(2,new(2,1));return new(){PlayerId=2,Coordinates=new(2,1),Reward=reward};
  }
  static bool IssueReward(GameState s,CityRewardCommand command)
  {
   string why="";bool valid=false;var method=typeof(CityRewardCommand).GetMethod("IsValid")!;
   if(NativeBlackoutAccess.BeforeValid(command,s,ref why,ref valid,method))valid=command.IsValid(s,out why);
   if(!valid||!NativeBlackoutAccess.BeforeExecute(command,s,typeof(CityRewardCommand).GetMethod("Execute")!))return false;
   command.Execute(s);return true;
  }
  static void OwnershipUpgradeCases()
  {
   foreach(string reward in new[]{"Giant","Park"})Test("covered-owned-city-settles-native-reward-"+reward,()=>
   {
    var s=Make();Cast(s,2,1);var command=Reward(s,reward);Add(s,24,2,1,1);var before=NativeGameStateEnvelope.Get(s)!;
    Check(IssueReward(s,command),"owned city's pending reward reaches native validation/execution despite siege and Blackout");
    Check(!CityRewardCommand.Pending.ContainsKey(s)&&Native.Items(s.ActionStack).OfType<CityRewardAction>().Single().Reward==reward,"native command pops trigger and queues exact reward");
    Check(!IssueReward(s,command)&&Native.Items(s.ActionStack).OfType<CityRewardAction>().Count()==1,"settled reward cannot be issued twice");
    Check(ReferenceEquals(before,NativeGameStateEnvelope.Get(s))&&!Tile(s,2,1).GetExplored(2)&&Tile(s,2,1).GetUnit(s,2,false) is null,"reward exception never reveals fogged enemy or edits ledger");
   });
   foreach(string invalid in new[]{"foreign-city","neutral-city","missing-city","changed-owner","wrong-turn","invalid-reward","no-pending","wrong-pending-tile"})Test("covered-city-reward-retains-authority-"+invalid,()=>
   {
    var s=Make();Cast(s,2,1);var c=Reward(s);var tile=Tile(s,2,1);
    switch(invalid){case"foreign-city":case"changed-owner":tile.owner=1;break;case"neutral-city":tile.owner=0;break;case"missing-city":tile.improvement=null;break;case"wrong-turn":s.CurrentPlayer=3;break;case"invalid-reward":c.Reward="Unknown";break;case"no-pending":CityRewardCommand.Pending.Remove(s);break;case"wrong-pending-tile":CityRewardCommand.Pending[s]=(2,new(2,2));break;}
    bool hadPending=CityRewardCommand.Pending.ContainsKey(s);Check(!IssueReward(s,c),"Blackout exception does not authorize invalid owner/turn/pending/reward");
    Check(CityRewardCommand.Pending.ContainsKey(s)==hadPending&&!Native.Items(s.ActionStack).OfType<CityRewardAction>().Any(),"failed reward leaves trigger/queue untouched");
    Check(!tile.GetExplored(2),"failed command leaves fog untouched");
   });
   Test("owned-covered-city-does-not-grant-other-orders",()=>
   {
    var s=Make();Cast(s,2,1);Reward(s);foreach(CommandBase c in new CommandBase[]{new BuildCommand{Coordinates=new(2,1)},new TrainCommand{Coordinates=new(2,1)},new MoveCommand{From=new(2,1)},new AttackCommand{Origin=new(2,1)}})
    {c.PlayerId=2;Check(!NativeBlackoutAccess.Allowed(c,s,c.GetType()),"only mandatory reward is exempt, not "+c.GetType().Name);}
   });
   Test("upgrade-discovery-hook-is-installed",()=>{var h=new HarmonyLib.Harmony();NativeBlackoutDiscovery.Install(h);Check(h.Hooks.Count(x=>x=="UpgradeAction.Execute")==1,"actual UpgradeAction Execute source must be hooked once");});
   foreach(bool moved in new[]{false,true})Test("real-raft-to-scout-clears-covered-native-footprint-"+moved,()=>
   {
    var s=Make();Cast(s,3,1);s.CurrentPlayer=2;var raft=Add(s,20,1,2,2,UnitType.transportship);raft.UnitData.Sight=1;raft.moved=moved;raft.attacked=moved;
    var upgrade=new UpgradeAction{PlayerId=2,Coordinates=new(1,2)};RefreshSource(s,upgrade,()=>upgrade.Execute(s));
    Check(Tile(s,1,2).unit!.id==raft.id&&!ReferenceEquals(Tile(s,1,2).unit,raft),"native replacement retains identity");
    Check(Read(s).Discoveries!.Contains(new(2,3,1))&&!Tile(s,3,1).GetExplored(2),"upgraded real sight publishes permit before queued reveal");
    Execute(s,Pending(s,2,3,1));Check(Tile(s,3,1).GetExplored(2)&&!Tile(s,3,1).GetExplored(3),"Scout's radius-two native exploration clears only its owner's Blackout");
    Check(Tile(s,1,2).unit!.moved==moved&&Tile(s,1,2).unit!.attacked==moved,"discovery does not refund movement or action budget");
   });
   foreach(string denied in new[]{"not-queued","covered-source","foreign-source","missing-source","wrong-turn","retired-session"})Test("upgrade-discovery-source-guard-"+denied,()=>
   {
    var s=Make();Cast(s,3,1);s.CurrentPlayer=2;Add(s,20,1,2,2,UnitType.transportship);var a=new UpgradeAction{PlayerId=2,Coordinates=new(1,2)};
    if(denied!="not-queued")s.ActionStack.Add(a);
    switch(denied){case"covered-source":Write(s,Read(s) with{Blackouts=Read(s).Blackouts!.Append(new(2,1,2)).OrderBy(m=>m.Player).ThenBy(m=>m.X).ThenBy(m=>m.Y).ToArray()});break;case"foreign-source":Tile(s,1,2).unit!.owner=1;break;case"missing-source":Tile(s,1,2).unit=null;break;case"wrong-turn":s.CurrentPlayer=3;break;case"retired-session":NativeRegroupSession.Owned.Clear();break;}
    NativeBlackoutDiscovery.BeforeSource(a,s,typeof(UpgradeAction).GetMethod("Execute")!,out var scope);
    try{Check(scope is null||!scope.Active,"unapproved upgrade cannot open discovery authority");}finally{NativeBlackoutDiscovery.SourceFinished(null,scope);}
    Check(Read(s).Discoveries!.Length==0&&!Tile(s,3,1).GetExplored(2),"guard denial preserves Blackout");
   });
   Test("upgrade-sight-uses-final-real-range",()=>
   {
    var s=Make();Cast(s,3,1);s.CurrentPlayer=2;Add(s,20,1,2,2,UnitType.transportship);var a=new UpgradeAction{PlayerId=2,Coordinates=new(1,2),Sight=1};
    RefreshSource(s,a,()=>a.Execute(s));Check(Read(s).Discoveries!.Length==0&&!Tile(s,3,1).GetExplored(2),"one-range actual upgrade cannot borrow a Scout costume's sight");
   });
   Test("failed-upgrade-does-not-publish-discovery",()=>
   {
    var s=Make();Cast(s,3,1);s.CurrentPlayer=2;Add(s,20,1,2,2,UnitType.transportship);var a=new UpgradeAction{PlayerId=2,Coordinates=new(1,2)};
    s.ActionStack.Add(a);NativeBlackoutDiscovery.BeforeSource(a,s,typeof(UpgradeAction).GetMethod("Execute")!,out var scope);a.Execute(s);
    var error=new InvalidOperationException("native upgrade aborted");Check(ReferenceEquals(NativeBlackoutDiscovery.SourceFinished(error,scope),error),"native exception retained");
    Check(Read(s).Discoveries!.Length==0&&!Tile(s,3,1).GetExplored(2),"failed source publishes no permit");
    var explore=Pending(s,2,3,1);bool valid=explore.IsValid(s);NativeBlackoutDiscovery.AfterValid(explore,s,ref valid);Check(!valid,"stranded child from failed source cannot clear fog");
   });
   Test("upgrade-source-scope-cleans-up-after-success",()=>
   {
    var s=Make();Cast(s,3,1);s.CurrentPlayer=2;Add(s,20,1,2,2,UnitType.transportship);var a=new UpgradeAction{PlayerId=2,Coordinates=new(1,2)};
    RefreshSource(s,a,()=>a.Execute(s));Write(s,Read(s) with{Discoveries=Array.Empty<LedgerDiscovery>()});int count=s.ActionStack.Count;
    s.TryGetPlayer(2,out var p);ActionUtils.ExploreFromTile(s,p!,Tile(s,1,2),2,true);
    Check(s.ActionStack.Count==count&&Read(s).Discoveries!.Length==0,"later static sight cannot reuse completed upgrade authority");
   });
  }
 }
}
