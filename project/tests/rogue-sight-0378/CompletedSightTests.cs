using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using U=Polytopia.Data.UnitData.Type;

namespace Deception.Tests
{
 public static class CompletedSightTests
 {
  static int Cases,Assertions;
  static void Check(bool value,string message){Assertions++;if(!value)throw new Exception(message);}
  static void Case(string name,Action test){try{test();Cases++;}catch(Exception e){throw new Exception(name,e);}}
  static readonly Guid Epoch=Guid.Parse("78787878-7878-7878-7878-787878787878");
  static byte[] RowBytes(LedgerUnit row)=>NativeIdentityLedgerCodec.Encode(new(Epoch,100,1,new[]{row},Array.Empty<LedgerReceipt>()),0);
  static LedgerAppearance Mask()=>new("warrior",new("Imperius","Default"),new("Imperius","Default"),new("Imperius","Default"),0x113355,30,100,false,ApparentConcealment.Visible,-1,new[]{"Poisoned"});
  sealed class World
  {
   internal readonly GameState State=new();internal readonly PlayerState Player=new(){Id=1};
   internal readonly UnitState Actor,Victim,Spy,Other;
   internal readonly TileData Origin,Destination,Pending,SpyTile,OtherTile;
   internal readonly MoveAction Move=new();internal readonly LedgerUnit OtherRow,SpyRow;
   internal World(U victim=U.warrior,bool maid=false)
   {
    NativeRogueSight.Clear();NativeRegroupSession.Enabled=true;NativeRegroupSession.Faulted=false;NativeRegroupSession.States.Clear();NativeRegroupSession.Faults.Clear();
    NativeGameStateEnvelope.Values.Clear();NativeGameStateEnvelope.Writes=0;NativeGameStateEnvelope.FailNextSet=false;
    State.Players[1]=Player;State.Players[2]=new(){Id=2};NativeRegroupSession.States.Add(State);
    Actor=Body(1,U.polytaur,1,new(4,4));Actor.health=120;Actor.UnitData.health=150;Actor.attacked=true;
    Victim=Body(2,victim,2,new(5,4));Spy=Body(3,maid?U.dcp_spymaid:U.dcp_spy,1,new(2,4));Spy.health=70;Spy.moved=Spy.attacked=true;
    Other=Body(4,U.warrior,2,new(2,5));Other.health=60;Other.effects.Add(UnitEffect.Frozen);
    Origin=Tile(Actor.coordinates,Actor,true);Destination=Tile(Victim.coordinates,Victim,false);Pending=Tile(new(6,4),null,false);SpyTile=Tile(Spy.coordinates,Spy,true);OtherTile=Tile(Other.coordinates,Other,true);
    SpyRow=new(4,3,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,8);
    OtherRow=new(5,4,true,LedgerActualKind.Genuine,true,Mask(),PaidInvestmentKind.Known,2);
    var actor=new LedgerUnit(2,1,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,2,Rogue:new(2,1,4,new[]{new LedgerSight(4,4)}));
    var victimRow=new LedgerUnit(3,2,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,2);
    var ledger=new NativeIdentityLedger(Epoch,7,1,new[]{actor,victimRow,SpyRow,OtherRow},Array.Empty<LedgerReceipt>());
    NativeGameStateEnvelope.Set(State,0,NativeIdentityLedgerCodec.Encode(ledger,0));NativeGameStateEnvelope.Writes=0;
   }
   internal static UnitState Body(uint id,U type,byte owner,WorldCoordinates at)=>new(){id=id,type=type,owner=owner,coordinates=at,UnitData=new(){type=type}};
   TileData Tile(WorldCoordinates at,UnitState? body,bool explored){var t=new TileData{coordinates=at,unit=body};if(explored)t.Seen.Add(1);State.Map.Tiles.Add(t);return t;}
   internal NativeIdentityLedger Ledger(){var s=NativeGameStateEnvelope.Get(State)!;return NativeIdentityLedgerCodec.Decode(s.Bytes(),s.Revision);}
   internal LedgerUnit Row(uint id)=>Ledger().Units.Single(x=>x.NativeId==id);
   internal NativeRogueSight.Source? Begin(bool kill=true)
   {
    if(kill){Victim.health=0;Destination.unit=null;}
    State.ActionStack.Add(Move);NativeRogueSight.BeforeMove(Move,State,out var source);Check(source is not null,"exact queued Rogue source absent");return source;
   }
   internal void Walk(bool pending=true)
   {
    Origin.unit=null;Destination.unit=Actor;Actor.coordinates=Destination.coordinates;Actor.moved=true;
    NativeRogueSight.BeforeFootprint(State,Player,Destination,1,out var footprint);Check(footprint is not null,"completed mover footprint absent");
    Visit(Destination,false);if(pending)Visit(Pending,true);NativeRogueSight.FootprintFinished(null,footprint);
   }
   void Visit(TileData tile,bool queued)
   {
    NativeRogueSight.BeforeTile(State,Player,tile,queued,out var visit);
    if(queued&&!tile.GetExplored(1))State.ActionStack.Add(new ExploreAction{PlayerId=1,Coordinates=tile.coordinates});else tile.Seen.Add(1);
    NativeRogueSight.AfterTile(true,visit);NativeRogueSight.TileFinished(null,visit);
   }
   internal Exception? Finish(NativeRogueSight.Source? source,bool executed=true,Exception? error=null)
   {
    // Absent only in the exact retained old-source control. The production
    // implementation must record __runOriginal before its finalizer publishes.
    typeof(NativeRogueSight).GetMethod("AfterMove",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static)?.Invoke(null,new object?[]{executed,source});
    return NativeRogueSight.SourceFinished(error,source);
   }
   internal void UnrelatedPreserved()
   {
    Check(RowBytes(Row(3)).SequenceEqual(RowBytes(SpyRow))&&Native.Same(SpyTile.unit,Spy),"original genuine Spy row/body changed");
    Check((Spy.type is U.dcp_spy or U.dcp_spymaid)&&Spy.health==70&&Spy.owner==1&&Spy.moved&&Spy.attacked,"Spy native type/health/owner/flags changed");
    var row=Row(4);Check(row.LogicalId==OtherRow.LogicalId&&row.Kind==OtherRow.Kind&&row.Independent&&row.PaidStars==2&&row.Appearance is not null&&
     NativeExchangeIntentCodec.AppearanceFingerprint(row.Appearance).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(OtherRow.Appearance!)),"unrelated independent disguise changed");
    Check(Native.Same(OtherTile.unit,Other)&&Other.type==U.warrior&&Other.health==60&&Other.effects.SequenceEqual(new[]{UnitEffect.Frozen})&&Other.owner==2,"unrelated actual body/status changed");
   }
  }
  public static int Run()
  {
   try
   {
    foreach(U victim in new[]{U.warrior,U.polytaur,U.dcp_spy,U.dcp_spymaid})foreach(bool maid in new[]{false,true})Case("completed kill advance retires "+victim+" and preserves Spymaid="+maid,()=>{
     var w=new World(victim,maid);var source=w.Begin();var before=NativeGameStateEnvelope.Get(w.State)!;
     bool rejected=false;try{NativeLedgerState.Validate(w.State,before.Revision,before.Bytes());}catch(InvalidDataException e){rejected=e.Message=="Private identity has no native body.";}
     Check(rejected,"strict original payload must reproduce the reported post-kill missing-body fault");
     w.Walk();var result=w.Finish(source);Check(result is null&&NativeRegroupSession.Faults.Count==0,"successful real reconciliation must prevent sight-publication session fault: "+result);
     var victimRow=w.Row(2);Check(!victimRow.Alive&&!victimRow.Independent&&victimRow.Appearance is null&&victimRow.Rogue is null,"removed victim not retired");
     var r=w.Row(1).Rogue!;Check(r.Owner==2&&r.Commander==1&&r.AcquiredTurn==4&&r.Sight!.SequenceEqual(new[]{new LedgerSight(5,4)})&&r.PendingSight!.SequenceEqual(new[]{new LedgerSight(6,4)}),"surviving Rogue footprint/remembered owner changed");
     Check(NativeGameStateEnvelope.Writes==1&&NativeGameStateEnvelope.Get(w.State)!.Revision==1,"death reconciliation and sight must publish atomically once");
     Check(w.Actor.health==120&&w.Actor.owner==1&&w.Actor.type==U.polytaur&&w.Actor.moved&&w.Actor.attacked&&!w.Pending.GetExplored(1),"sight merge changed real stats or speculative fog");w.UnrelatedPreserved();
     NativeLedgerState.Validate(w.State,1,NativeGameStateEnvelope.Get(w.State)!.Bytes());
    });
    Case("pending exploration after kill and serialized snapshot",()=>{
     var w=new World();var source=w.Begin();w.Walk();Check(w.Finish(source) is null,"move fault");var saved=NativeGameStateEnvelope.Get(w.State)!;
     NativeRogueSight.Clear();NativeGameStateEnvelope.Values[w.State]=new(saved.Revision,saved.Bytes());
     var explore=w.State.ActionStack.OfType<ExploreAction>().Single();NativeRogueSight.BeforeExplore(explore,w.State,out var e);w.Pending.Seen.Add(1);NativeRogueSight.AfterExplore(true,e);
     Check(NativeRogueSight.ExploreFinished(null,e) is null&&!NativeRegroupSession.Faulted,"legitimate post-load queued exploration faulted");
     Check(w.Row(1).Rogue!.PendingSight is null&&w.Row(1).Rogue!.Sight!.SequenceEqual(new[]{new LedgerSight(5,4),new LedgerSight(6,4)})&&!w.Row(2).Alive,"queued sight lost death retirement");w.UnrelatedPreserved();
    });
    Case("second kill advance keeps original Spy and Rogue lineage",()=>{
     var w=new World();var first=w.Begin();w.Walk(false);Check(w.Finish(first) is null,"first kill faulted");
     w.Other.health=0;w.OtherTile.unit=null;var move=new MoveAction{UnitId=1};w.State.ActionStack.Add(move);NativeRogueSight.BeforeMove(move,w.State,out var source);
     Check(source is not null,"second completed action source absent");w.Destination.unit=null;w.OtherTile.unit=w.Actor;w.Actor.coordinates=w.OtherTile.coordinates;
     NativeRogueSight.BeforeFootprint(w.State,w.Player,w.OtherTile,1,out var f);NativeRogueSight.BeforeTile(w.State,w.Player,w.OtherTile,false,out var v);w.OtherTile.Seen.Add(1);NativeRogueSight.AfterTile(true,v);NativeRogueSight.TileFinished(null,v);NativeRogueSight.FootprintFinished(null,f);
     Check(w.Finish(source) is null&&!NativeRegroupSession.Faulted,"second native kill caused stale-victim publication fault");
     Check(!w.Row(2).Alive&&!w.Row(4).Alive&&w.Row(4).Appearance is null&&!w.Row(4).Independent,"both actual deaths must retire without retaining independent mask");
     Check(w.Row(1).Rogue!.Sight!.SequenceEqual(new[]{new LedgerSight(2,5)})&&w.Row(1).Rogue!.Owner==2&&w.Actor.owner==1&&w.Actor.type==U.polytaur,"second kill changed Rogue command/origin or retained stale first footprint");
     Check(RowBytes(w.Row(3)).SequenceEqual(RowBytes(w.SpyRow))&&Native.Same(w.SpyTile.unit,w.Spy)&&w.Spy.type==U.dcp_spy&&w.Spy.health==70,"chain altered original genuine Spy");
     Check(NativeGameStateEnvelope.Get(w.State)!.Revision==2&&NativeGameStateEnvelope.Writes==2,"each completed distinct sight source publishes once");
    });
    Case("unchanged sight still reconciles prior death without extra revision",()=>{
     var w=new World();w.Victim.health=0;w.Destination.unit=null;var c=new NativeRogueSight.Collection();
     using(NativeRogueSight.ExchangeSource(w.State,1,w.Origin,c))
     {
      NativeRogueSight.BeforeFootprint(w.State,w.Player,w.Origin,1,out var f);NativeRogueSight.BeforeTile(w.State,w.Player,w.Origin,false,out var v);
      NativeRogueSight.AfterTile(true,v);NativeRogueSight.TileFinished(null,v);NativeRogueSight.FootprintFinished(null,f);
     }
     NativeRogueSight.CompleteCollection(w.State,c);
     Check(!w.Row(2).Alive&&NativeGameStateEnvelope.Writes==1&&NativeGameStateEnvelope.Get(w.State)!.Revision==0,"death-only reconciliation must publish at native revision cadence");
     Check(w.Row(1).Rogue!.Sight!.SequenceEqual(new[]{new LedgerSight(4,4)}),"unchanged legitimate footprint changed");w.UnrelatedPreserved();
    });
    foreach(string stale in new[]{"replacement","actor-removed","reacquired","owner","turn","generation","unready"})Case("stale completed source refuses publication "+stale,()=>{
     var w=new World();var source=w.Begin();w.Walk();var before=NativeGameStateEnvelope.Get(w.State)!;
     if(stale=="replacement")w.Destination.unit=World.Body(1,U.polytaur,1,w.Destination.coordinates);
     if(stale=="actor-removed")w.Destination.unit=null;
     if(stale=="reacquired"){var l=w.Ledger();l=l with{Units=l.Units.Select(r=>r.NativeId==1?r with{Rogue=r.Rogue! with{AcquiredTurn=5}}:r).ToArray()};NativeGameStateEnvelope.Values[w.State]=new(before.Revision,NativeIdentityLedgerCodec.Encode(l,before.Revision));before=NativeGameStateEnvelope.Get(w.State)!;}
     if(stale=="owner")w.Actor.owner=2;if(stale=="turn")w.State.CurrentTurn++;if(stale=="generation")NativeRogueSight.Clear();if(stale=="unready")NativeRegroupSession.States.Clear();
     w.Finish(source);Check(ReferenceEquals(before,NativeGameStateEnvelope.Get(w.State))&&NativeGameStateEnvelope.Writes==0,"stale actor published sight/death changes");
    });
    foreach(bool executed in new[]{false,true})Case("native skipped or failed move has no publication executed="+executed,()=>{
     var w=new World();var source=w.Begin();w.Walk();var before=NativeGameStateEnvelope.Get(w.State)!;var error=executed?new InvalidOperationException("native move failed"):null;
     Check(ReferenceEquals(w.Finish(source,executed,error),error),"native failure identity changed");Check(ReferenceEquals(before,NativeGameStateEnvelope.Get(w.State))&&NativeGameStateEnvelope.Writes==0&&NativeRegroupSession.Faults.Count==0,"failed/skipped move published or invented fault");
    });
    foreach(string failure in new[]{"duplicate-native-id","cached-profile","envelope-set"})Case("real validation/publication failure retains session fault "+failure,()=>{
     var w=new World();var source=w.Begin();w.Walk();var before=NativeGameStateEnvelope.Get(w.State)!;
     if(failure=="duplicate-native-id")w.Other.id=w.Spy.id;if(failure=="cached-profile")w.Spy.UnitData.type=U.warrior;if(failure=="envelope-set")NativeGameStateEnvelope.FailNextSet=true;
     var result=w.Finish(source);Check(result is not null&&NativeRegroupSession.Faulted&&NativeRegroupSession.Faults.Count==1,"invalid native state must remain a failed session, not be silently normalized");
     Check(ReferenceEquals(before,NativeGameStateEnvelope.Get(w.State))&&NativeGameStateEnvelope.Writes==0,"failed reconciliation/publication changed original snapshot");
    });
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,nativeGame="NOT RUN",realLedgerState=true}));return 0;
   }
   catch(Exception e){Console.WriteLine(e);Console.WriteLine("{\"passed\":false}");return 1;}
   finally{NativeRogueSight.Clear();}
  }
 }
}
