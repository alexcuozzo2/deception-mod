using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using Deception.Core;
using Deception.PolyModAdapter;
using Reason=PolytopiaBackendBase.Game.StateUpdateReason;
namespace Deception.Tests
{
 public static class SessionTests
 {
  static int Assertions;static List<string> Results=new();
  static void Check(bool ok,string why){Assertions++;if(!ok)throw new Exception(why);}
  static void Test(string name,Action run){NativeRegroupSession.FixtureReset();try{run();Results.Add(name);}catch(Exception e){throw new Exception(name,e);}finally{NativeMenuPreviewScope.Finished(null,0);}}
  static void Reject(Action run,string reason){try{run();}catch(InvalidDataException e){Check(e.Message.Contains(reason),"Unexpected rejection: "+e.Message);return;}throw new Exception("No rejection: "+reason);}
  static (ClientBase Client,GameSettings Settings,NativeRegroupSession.Session Session) Create(bool hotseat=true)
  {
   var client=new ClientBase{clientType=hotseat?ClientBase.ClientType.PassAndPlay:ClientBase.ClientType.Local};GameManager.Client=client;var settings=new GameSettings();
   NativeRegroupSession.BeforeCreate(client,settings,client.gameId,typeof(ClientBase).GetMethod("CreateSession",new[]{typeof(GameSettings),typeof(Il2CppSystem.Guid)})!,out var token);
   Check(token is NativeRegroupSession.Session,"Real settings creation was not enrolled");return(client,settings,(NativeRegroupSession.Session)token!);
  }
  static GameState Source(ClientBase client,GameSettings settings,bool hotseat=true)
  {
   var state=new GameState{Settings=settings};
   if(hotseat){Generate(state);Check(NativeRegroupSession.BeforeTransport(state,false)!=null,"Hotseat initial writer rejected");Check(NativeRegroupSession.BeforeAdopt(client,state,Reason.GameCreated),"Hotseat native adoption rejected");}
   else NativeRegroupSession.AfterLocal(123,settings,state,true);
   return state;
  }
  static void Generate(GameState state)
  {
   NativeRegroupSession.BeforeGeneration(state,null,-1,out var token);
   Check(token!=null,"Exact native generator did not capture candidate");
   try{NativeRegroupSession.AfterGeneration(state,token,true);NativeRegroupSession.GenerationFinished(null,token);}
   catch(Exception e){NativeRegroupSession.GenerationFinished(e,token);throw;}
  }
  static void Publish(ClientBase c,GameState s,NativeRegroupSession.Session token)
  {
   c.initialGameState=s;c.currentGameState=s;c.ActionManager=new(){GameState=s};
   NativeRegroupSession.Opened(c);NativeRegroupSession.AfterCreate(token,0);NativeRegroupSession.CreationFinished(null,token);
  }
  static byte[] Payload(Guid epoch)=>NativeIdentityLedgerCodec.Encode(new(epoch,1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>()),0);
  static void FramedSibling(GameState state,Guid epoch)
  {
   var token=NativeRegroupSession.BeforeTransport(state,true);var bytes=Payload(epoch);NativeRegroupSession.BeforeFrame(state,true,token,0,bytes);
   NativeGameStateEnvelope.Set(state,0,bytes);NativeRegroupSession.Completed(state,true,true,token);
  }
  public static int Run()
  {
   try
   {
    foreach(bool hot in new[]{false,true})Test("SESSION-001-native-settings-create-"+(hot?"hotseat":"local"),()=>{
     var(c,g,t)=Create(hot);Check(!NativeRegroupSession.CanDispatch(c),"Opening permits command");var s=Source(c,g,hot);Publish(c,s,t);
     Check(NativeRegroupSession.Ready(s)&&NativeRegroupSession.CanDispatch(c),"Completed session missing ready ledger");Check(!t.CreationActive&&!t.Creating&&!t.Opening&&NativeLedgerState.Bootstraps==1,"Create scope or bootstrap count differs");
    });
    Test("SESSION-002-local-hotseat-local-replacement-retires-and-cleans",()=>{
     int cleans=0;NativeRegroupSession.OnSessionEnded=()=>cleans++;var(c,g,t)=Create(false);var old=Source(c,g,false);Publish(c,old,t);var oldRead=NativeRegroupSession.BeforeTransport(new GameState(),true);
     var(h,hg,ht)=Create();var hs=Source(h,hg);Publish(h,hs,ht);Check(t.Retired&&cleans==1&&!NativeRegroupSession.Ready(old)&&NativeRegroupSession.Ready(hs),"Hotseat borrowed Local state or missed cleanup");
     Reject(()=>oldRead!.Check(new GameState(),true),"changed session");var(l,lg,lt)=Create(false);var ls=Source(l,lg,false);Publish(l,ls,lt);Check(ht.Retired&&cleans==2&&!NativeRegroupSession.Ready(hs)&&NativeRegroupSession.Ready(ls),"Local replacement retained Hotseat authority");
    });
    Test("SESSION-003-framed-hotseat-siblings-retain-epoch",()=>{
     var(c,g,t)=Create();var s=Source(c,g);Publish(c,s,t);var child=new GameState();FramedSibling(child,t.Epoch);Check(NativeRegroupSession.Ready(child)&&NativeRegroupSession.Ready(s),"Framed sibling lost ownership");
     c.targetGameState=child;NativeRegroupSession.AfterClient(c,true);Check(!t.Faulted&&NativeRegroupSession.BeforeTransport(child,false)!=null,"Adopted sibling cannot save");
    });
    Test("SESSION-A44-role-aliases-reconcile-once-per-native-state",()=>{
     var(c,g,t)=Create();var s=Source(c,g);Publish(c,s,t);c.targetGameState=s;c.lastTurnGameState=s;
     // A fresh managed wrapper for the same native object must also dedupe.
     c.ActionManager!.GameState=new GameState{Pointer=s.Pointer};
     int reconciles=NativeLedgerState.Reconciliations,validates=NativeLedgerState.Validations;
     NativeRegroupSession.AfterClient(c,true);
     Check(!t.Faulted&&NativeLedgerState.Reconciliations-reconciles==1&&NativeLedgerState.Validations-validates==1,"Aliased native roles repeated reconciliation or rejected a wrapper");
     NativeRegroupSession.AfterClient(c,true);
     Check(NativeLedgerState.Reconciliations-reconciles==2&&NativeLedgerState.Validations-validates==2,"Later boundary reused stale validation");
     var distinct=new GameState();FramedSibling(distinct,t.Epoch);c.targetGameState=distinct;
     reconciles=NativeLedgerState.Reconciliations;validates=NativeLedgerState.Validations;NativeRegroupSession.AfterClient(c,true);
     Check(!t.Faulted&&NativeLedgerState.Reconciliations-reconciles==2&&NativeLedgerState.Validations-validates==2,"Distinct states sharing identical ledger bytes were collapsed");
     NativeGameStateEnvelope.Set(distinct,0,Payload(Guid.NewGuid()));NativeRegroupSession.AfterClient(c,true);
     Check(t.Faulted,"Later corrupt distinct role escaped validation");
    });
    foreach(bool framed in new[]{false,true})Test("SESSION-004-hotseat-open-"+(framed?"framed":"legacy"),()=>{
     var c=new ClientBase{clientType=ClientBase.ClientType.PassAndPlay};GameManager.Client=c;NativeRegroupSession.BeforeOpen(c,c.gameId,out var opened);var t=(NativeRegroupSession.Session)opened!;var s=new GameState();var tok=NativeRegroupSession.BeforeTransport(s,true);
     if(framed){var bytes=Payload(t.Epoch);NativeRegroupSession.BeforeFrame(s,true,tok,0,bytes);NativeGameStateEnvelope.Set(s,0,bytes);}else NativeRegroupSession.BeforeLegacyRead(s,tok);
     NativeRegroupSession.Completed(s,true,framed,tok);c.initialGameState=s;c.currentGameState=s;c.ActionManager=new(){GameState=s};NativeRegroupSession.Opened(c);NativeRegroupSession.OpenFinished(null,t);
     Check(NativeRegroupSession.Ready(s)&&NativeRegroupSession.CanDispatch(c),"Opened Hotseat never became ready");Check(NativeLedgerState.Bootstraps==(framed?0:1),"Framed save was re-bootstrapped");
    });
    Test("SESSION-005-hotseat-create-from-bytes-uses-read-window",()=>{
     var c=new ClientBase{clientType=ClientBase.ClientType.PassAndPlay};GameManager.Client=c;NativeRegroupSession.BeforeCreate(c,new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>(),c.gameId,typeof(ClientBase).GetMethods().Single(m=>m.Name=="CreateSession"&&m.GetParameters()[0].ParameterType!=typeof(GameSettings)),out var opened);
     var t=(NativeRegroupSession.Session)opened!;Check(t.Opening&&!t.Creating&&!t.CreationActive,"Bytes creation received fresh-source authority");var s=new GameState();FramedSibling(s,t.Epoch);Publish(c,s,t);Check(NativeRegroupSession.Ready(s)&&NativeLedgerState.Bootstraps==0,"Bytes load lost original framed ledger");
    });
    Test("SESSION-006-hotseat-dispatch-unenrolled-is-denied",()=>{var c=new ClientBase{clientType=ClientBase.ClientType.PassAndPlay};GameManager.Client=c;Check(!NativeRegroupSession.CanDispatch(c)&&!NativeRegroupSession.BeforeDispatch(c),"Unenrolled Hotseat dispatched");});
    foreach(string bad in new[]{"settings","reason","version","frame","scope","replay","spectate","client","mode","guid"})Test("SESSION-007-source-reject-"+bad,()=>{
     var(c,g,t)=Create();var s=new GameState{Settings=bad=="settings"?new():g};Reason reason=Reason.GameCreated;
     if(bad=="reason")reason=Reason.ValidCommand;if(bad=="version")s.Version=122;if(bad=="frame")NativeGameStateEnvelope.Set(s,0,Payload(t.Epoch));if(bad=="scope")NativeRegroupSession.CreationFinished(null,t);
     if(bad=="replay")c.IsReplay=true;if(bad=="spectate")c.IsSpectating=true;if(bad=="client")GameManager.Client=new();if(bad=="mode")c.clientType=ClientBase.ClientType.Local;if(bad=="guid")c.gameId=new(Guid.NewGuid());
     Check(!NativeRegroupSession.BeforeAdopt(c,s,reason)&&t.Faulted&&!NativeRegroupSession.FixtureOwned(s),"Invalid source was bound: "+bad);Check(NativeLedgerState.Bootstraps==0,"Rejected context bootstrapped source: "+bad);
    });
    foreach(bool registration in new[]{false,true})Test("SESSION-008-validation-registration-failure-"+registration,()=>{
     var(c,g,t)=Create();var s=new GameState{Settings=g};Generate(s);NativeLedgerState.FailValidate=!registration;NativeBlackout.Fail=registration;
     try{NativeRegroupSession.BeforeTransport(s,false);throw new Exception("Missing registration failure");}catch(InvalidOperationException){NativeRegroupSession.Fault("Injected transport failure");}
     Check(t.Faulted,"Registration failure remained ready");Check(!NativeRegroupSession.FixtureOwned(s)&&NativeGameStateEnvelope.Get(s)==null&&t.Source==null,"Failed registration left owned/frame/source residue");
    });
    Test("SESSION-009-no-second-unframed-source-bootstrap",()=>{
     var(c,g,t)=Create();var first=Source(c,g);Check(NativeRegroupSession.BeforeAdopt(c,first,Reason.GameCreated),"Repeated exact owned source rejected");var second=new GameState{Settings=g};Check(!NativeRegroupSession.BeforeAdopt(c,second,Reason.GameCreated)&&NativeLedgerState.Bootstraps==1&&!NativeRegroupSession.FixtureOwned(second),"Second source borrowed creation window");
    });
    Test("SESSION-010-create-exception-closes-source-window",()=>{
     var(c,g,t)=Create();var e=new IOException("native creation error");Check(ReferenceEquals(e,NativeRegroupSession.CreationFinished(e,t))&&!t.CreationActive&&t.Faulted,"Creation exception leaked authority or replaced error");Check(!NativeRegroupSession.BeforeAdopt(c,new GameState{Settings=g},Reason.GameCreated),"Failed create reopened");
    });
    Test("SESSION-011-hotseat-open-exception-stays-faulted",()=>{var(c,_,t)=Create();var e=new IOException("native open error");Check(ReferenceEquals(e,NativeRegroupSession.OpenFinished(e,t))&&t.Faulted&&!NativeRegroupSession.CanDispatch(c),"Open failure did not close commands");});
    Test("SESSION-012-hotseat-real-end-retires-once",()=>{var(c,g,t)=Create();var s=Source(c,g);Publish(c,s,t);int calls=0;NativeRegroupSession.OnSessionEnded=()=>calls++;NativeRegroupSession.Ended(c);NativeRegroupSession.Ended(c);Check(t.Retired&&calls==1&&!NativeRegroupSession.Ready(s),"End failed exact cleanup");Reject(()=>NativeRegroupSession.BeforeTransport(s,false),"retired/framed");});
    Test("SESSION-013-hotseat-thread-boundary-stays-closed",()=>{var(c,g,t)=Create();var s=new GameState{Settings=g};bool allowed=true;var thread=new Thread(()=>allowed=NativeRegroupSession.BeforeAdopt(c,s,Reason.GameCreated));thread.Start();thread.Join();Check(!allowed&&NativeLedgerState.Bootstraps==0&&t.Faulted,"Cross-thread source gained authority");});
    Test("SESSION-014-fake-client-create-rejected-before-generation",()=>{var c=new ClientBase{clientType=ClientBase.ClientType.PassAndPlay};GameManager.Client=c;GameManager.instance!.fakeSession=new();Reject(()=>NativeRegroupSession.BeforeCreate(c,new GameSettings(),c.gameId,typeof(ClientBase).GetMethod("CreateSession",new[]{typeof(GameSettings),typeof(Il2CppSystem.Guid)})!,out _),"fake session");Check(!NativeRegroupSession.Ready(new GameState()),"Fake source gained ownership");});
    Test("SESSION-015-preview-remains-unbound-under-hotseat",()=>{var(c,g,t)=Create();var s=Source(c,g);Publish(c,s,t);var preview=new GameState();NativeMenuPreviewScope.Before(out var old);try{var tok=NativeRegroupSession.BeforeTransport(preview,true);NativeRegroupSession.Completed(preview,true,false,tok);Check(tok==null&&!NativeRegroupSession.Ready(preview)&&NativeLedgerState.Bootstraps==1&&!t.Faulted,"Preview became Hotseat source");Reject(()=>NativeRegroupSession.BeforeTransport(s,true),"enrolled or framed");}finally{NativeMenuPreviewScope.Finished(null,old);}});
    Test("SESSION-016-post-open-legacy-and-foreign-frame-denied",()=>{var(c,g,t)=Create();var s=Source(c,g);Publish(c,s,t);var other=new GameState();var tok=NativeRegroupSession.BeforeTransport(other,true);Reject(()=>NativeRegroupSession.BeforeLegacyRead(other,tok),"already-open framed match");Reject(()=>NativeRegroupSession.BeforeFrame(other,true,tok,0,Payload(Guid.NewGuid())),"different match Guid");});
    Test("SESSION-017-old-frame-does-not-cross-new-client",()=>{var(c,g,t)=Create();var old=Source(c,g);Publish(c,old,t);var(h,hg,ht)=Create();Check(!NativeRegroupSession.BeforeAdopt(h,old,Reason.GameCreated)&&ht.Faulted&&!NativeRegroupSession.Ready(old),"Old source crossed epoch");});
    Test("SESSION-018-same-client-open-reentry-denied",()=>{var(c,g,t)=Create();var s=Source(c,g);Publish(c,s,t);Reject(()=>NativeRegroupSession.BeforeOpen(c,c.gameId,out _),"opened twice");Check(NativeRegroupSession.Ready(s),"Rejected reentry changed prior ownership");});
    Test("SESSION-019-remote-replay-spectating-creation-not-enrolled",()=>{
     foreach(var mode in new[]{ClientBase.ClientType.Remote,ClientBase.ClientType.Replay}){var c=new ClientBase{clientType=mode};GameManager.Client=c;NativeRegroupSession.BeforeOpen(c,c.gameId,out var token);Check(token==null,"Remote/replay enrolled");}
     foreach(bool replay in new[]{true,false}){var c=new ClientBase{clientType=ClientBase.ClientType.PassAndPlay,IsReplay=replay,IsSpectating=!replay};GameManager.Client=c;NativeRegroupSession.BeforeOpen(c,c.gameId,out var token);Check(token==null,"Replay/spectator enrolled");}
    });
    Test("SESSION-020-paid-missing-ledger-aborts-spawn-reaction",()=>{var(c,_,t)=Create();var action=new TrainAction();Check(!NativeRegroupSession.BeforePaidTrain(action,new GameState(),out var token)&&token==null&&action.trainActionAborted&&t.Faulted,"Unready paid Mimic allowed native null-body reaction");});
    Test("SESSION-021-already-aborted-paid-action-remains-native-noop",()=>{var action=new TrainAction{trainActionAborted=true};Check(NativeRegroupSession.BeforePaidTrain(action,new GameState(),out var token)&&token==null&&action.trainActionAborted,"Already aborted native action changed");});
    Test("SESSION-022-ordinary-native-train-not-enrolled-or-aborted",()=>{var action=new TrainAction{Type=UnitType.warrior,Cost=2};Check(NativeRegroupSession.BeforePaidTrain(action,new GameState(),out var token)&&token==null&&!action.trainActionAborted&&NativeLedgerState.Bootstraps==0,"Native warrior train was modified");});
    Test("SESSION-023-cleanup-failure-does-not-borrow-old-generation",()=>{var(c,g,t)=Create();var s=Source(c,g);Publish(c,s,t);NativeRegroupSession.OnSessionEnded=()=>throw new Exception("cleanup");var(h,hg,ht)=Create();var hs=Source(h,hg);Publish(h,hs,ht);Check(t.Retired&&!NativeRegroupSession.Ready(s)&&NativeRegroupSession.Ready(hs)&&Entry.Notes.Any(x=>x.Contains("cleanup-failed")),"Cleanup error retained prior state authority");});
    Test("SESSION-024-real-hotseat-order-includes-starters-before-first-write",()=>{
     var(c,g,t)=Create();var source=new GameState{Settings=g};Generate(source);
     Check(!NativeRegroupSession.Ready(source)&&NativeGameStateEnvelope.Get(source)==null&&NativeLedgerState.Bootstraps==0,"Generator prematurely published an incomplete graph");
     source.StartingUnits.AddRange(new uint[]{1,2});var write=NativeRegroupSession.BeforeTransport(source,false);var snapshot=NativeGameStateEnvelope.Get(source)!;
     Check(write!=null&&NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Units.Where(x=>x.Alive).Select(x=>x.NativeId).OrderBy(x=>x).SequenceEqual(new uint[]{1,2})&&t.Generated==null,"First writer omitted later starting-unit births or kept authority");
     var initial=new GameState{Settings=g};var read=NativeRegroupSession.BeforeTransport(initial,true);NativeRegroupSession.BeforeFrame(initial,true,read,snapshot.Revision,snapshot.Bytes());NativeGameStateEnvelope.Set(initial,snapshot.Revision,snapshot.Bytes());NativeRegroupSession.Completed(initial,true,true,read);
     c.initialGameState=initial;Check(NativeRegroupSession.Ready(initial)&&NativeIdentityLedgerCodec.Decode(NativeGameStateEnvelope.Get(initial)!.Bytes(),NativeGameStateEnvelope.Get(initial)!.Revision).Units.Any(x=>x.Alive&&x.NativeId==2),"Initial sibling before GameCreated lost framed starters");
     Check(NativeRegroupSession.BeforeAdopt(c,source,Reason.GameCreated),"GameCreated could not use previously enrolled source");Publish(c,source,t);Check(NativeLedgerState.Bootstraps==1&&NativeRegroupSession.CanDispatch(c),"Native sequence duplicated source enrollment or stayed closed");
    });
    Test("SESSION-025-unwitnessed-first-writer-with-same-settings-denied",()=>{
     var(c,g,t)=Create();var source=new GameState{Settings=g};Generate(source);var foreign=new GameState{Settings=g,Map=source.Map};Reject(()=>NativeRegroupSession.BeforeTransport(foreign,false),"Unenrolled state");
     Check(NativeLedgerState.Bootstraps==0&&!NativeRegroupSession.FixtureOwned(foreign),"Another first writer borrowed matching settings/map");Check(NativeRegroupSession.BeforeTransport(source,false)!=null,"Foreign writer changed exact captured source");
    });
    Test("SESSION-026-gamecreated-cannot-bootstrap-without-first-serialization",()=>{var(c,g,t)=Create();var source=new GameState{Settings=g};Generate(source);Check(!NativeRegroupSession.BeforeAdopt(c,source,Reason.GameCreated)&&NativeLedgerState.Bootstraps==0&&!NativeRegroupSession.FixtureOwned(source),"Late GameCreated remained a bootstrap fallback");});
    foreach(bool skipped in new[]{false,true})Test("SESSION-027-failed-or-skipped-generator-no-candidate-"+skipped,()=>{
     var(c,g,t)=Create();var source=new GameState{Settings=g};NativeRegroupSession.BeforeGeneration(source,null,-1,out var token);Exception error=new IOException("Native generation failed");
     if(skipped){try{NativeRegroupSession.AfterGeneration(source,token,false);throw new Exception("Skipped generator accepted");}catch(InvalidDataException e){error=e;}}
     Check(ReferenceEquals(error,NativeRegroupSession.GenerationFinished(error,token))&&t.Generated==null&&t.Generating==null&&t.Faulted,"Failed generation retained candidate or replaced exception");Check(!NativeRegroupSession.Ready(source)&&NativeLedgerState.Bootstraps==0,"Failed generation produced ledger");
    });
    foreach(string change in new[]{"map","settings","scope","fault","ended","guid","mode","client"})Test("SESSION-028-captured-first-writer-reject-"+change,()=>{
     var(c,g,t)=Create();var source=new GameState{Settings=g};Generate(source);
     if(change=="map")source.Map=new();if(change=="settings")source.Settings=new();if(change=="scope")NativeRegroupSession.CreationFinished(null,t);if(change=="fault")NativeRegroupSession.Fault("injected");if(change=="ended")NativeRegroupSession.Ended(c);if(change=="guid")c.gameId=new(Guid.NewGuid());if(change=="mode")c.clientType=ClientBase.ClientType.Local;if(change=="client")GameManager.Client=new();
     try{var token=NativeRegroupSession.BeforeTransport(source,false);Check(token==null,"Changed captured source received transport authority");}catch(InvalidDataException){Check(true,"Captured source rejected");}
     Check(NativeLedgerState.Bootstraps==0&&!NativeRegroupSession.Ready(source)&&NativeGameStateEnvelope.Get(source)==null,"Changed creation context gained ledger: "+change);
    });
    Test("SESSION-029-second-generator-cannot-replace-completed-source",()=>{
     var(c,g,t)=Create();var source=new GameState{Settings=g};Generate(source);var old=t.Generated;var second=new GameState{Settings=g};Reject(()=>NativeRegroupSession.BeforeGeneration(second,null,-1,out _),"already has a generator/source");Check(t.Generated==null&&NativeLedgerState.Bootstraps==0&&t.Faulted,"Second generator retained or overwrote candidate");
    });
    Test("SESSION-030-nested-unrelated-generator-cannot-nominate-source",()=>{
     var(c,g,t)=Create();var source=new GameState{Settings=g};NativeRegroupSession.BeforeGeneration(source,null,-1,out var token);var unrelated=new GameState();NativeRegroupSession.BeforeGeneration(unrelated,null,-1,out var other);NativeRegroupSession.AfterGeneration(unrelated,other,true);NativeRegroupSession.GenerationFinished(null,other);
     Check(other==null&&ReferenceEquals(t.Generating,token)&&t.Generated==null,"Unrelated nested generator replaced source witness");NativeRegroupSession.AfterGeneration(source,token,true);NativeRegroupSession.GenerationFinished(null,token);Check(NativeRegroupSession.BeforeTransport(source,false)!=null&&!NativeRegroupSession.Ready(unrelated),"Exact outer generation failed after unrelated work");
    });
    Test("SESSION-031-nested-same-settings-generator-fails-closed",()=>{
     var(c,g,t)=Create();var source=new GameState{Settings=g};NativeRegroupSession.BeforeGeneration(source,null,-1,out var token);Reject(()=>NativeRegroupSession.BeforeGeneration(new GameState{Settings=g},null,-1,out _),"already has a generator/source");NativeRegroupSession.GenerationFinished(new IOException("nested failed"),token);Check(t.Generated==null&&t.Generating==null&&t.Faulted,"Nested same-settings replacement left authority");
    });
    Test("SESSION-032-preview-generator-never-nominates-match-source",()=>{
     var(c,g,t)=Create();var source=new GameState{Settings=g};NativeMenuPreviewScope.Before(out var depth);try{NativeRegroupSession.BeforeGeneration(source,null,-1,out var token);NativeRegroupSession.AfterGeneration(source,token,true);NativeRegroupSession.GenerationFinished(null,token);Check(token==null&&t.Generated==null&&!t.Faulted,"Preview generation became gameplay source");}finally{NativeMenuPreviewScope.Finished(null,depth);}Reject(()=>NativeRegroupSession.BeforeTransport(source,false),"Unenrolled state");
    });
    foreach(bool callback in new[]{false,true})Test("SESSION-033-nonnative-generator-arguments-rejected-"+callback,()=>{var(c,g,t)=Create();var source=new GameState{Settings=g};Reject(()=>NativeRegroupSession.BeforeGeneration(source,callback?new object():null,callback?-1:4,out _),"generator arguments differ");Check(t.Generated==null&&t.Generating==null&&NativeLedgerState.Bootstraps==0,"Unverified generator nominated root");});
    Test("SESSION-034-generator-body-cannot-write-before-completion",()=>{var(c,g,t)=Create();var source=new GameState{Settings=g};NativeRegroupSession.BeforeGeneration(source,null,-1,out var token);Reject(()=>NativeRegroupSession.BeforeTransport(source,false),"Unenrolled state");NativeRegroupSession.GenerationFinished(new IOException("unfinished"),token);Check(NativeLedgerState.Bootstraps==0&&t.Generated==null,"In-progress generator bootstrapped");});
    Test("SESSION-035-generation-return-must-match-prefix-state",()=>{var(c,g,t)=Create();var source=new GameState{Settings=g};NativeRegroupSession.BeforeGeneration(source,null,-1,out var token);try{NativeRegroupSession.AfterGeneration(new GameState{Settings=g},token,true);throw new Exception("Changed return accepted");}catch(InvalidDataException e){NativeRegroupSession.GenerationFinished(e,token);}Check(t.Generated==null&&t.Generating==null&&t.Faulted,"Changed generation return gained candidate");});
    Test("SESSION-036-create-error-revokes-completed-generation",()=>{var(c,g,t)=Create();var source=new GameState{Settings=g};Generate(source);var error=new IOException("creator failed after generation");NativeRegroupSession.CreationFinished(error,t);Check(t.Generated==null&&!t.CreationActive&&t.Faulted,"Interrupted creator retained candidate");Reject(()=>NativeRegroupSession.BeforeTransport(source,false),"retired, or faulted");});
    foreach(bool active in new[]{false,true})foreach(bool framed in new[]{true,false})Test("SUMMARY-001-detached-list-"+active+"-"+framed,()=>{
     NativeRegroupSession.Session? live=null;if(active){var(c,g,t)=Create();Publish(c,Source(c,g),t);live=t;}
     var epoch=Guid.NewGuid();NativeRegroupSession.BeforeSummary("/native/"+epoch+".state",out var scope);
     var states=new List<GameState>();try{for(int i=0;i<3;i++){var s=new GameState();states.Add(s);var token=NativeRegroupSession.BeforeTransport(s,true);Check(token!=null&&token.IsSummary,"Summary borrowed live token");if(framed){var bytes=Payload(epoch);NativeRegroupSession.BeforeFrame(s,true,token,0,bytes);NativeGameStateEnvelope.Set(s,0,bytes);}else NativeRegroupSession.BeforeLegacyRead(s,token);NativeRegroupSession.Completed(s,true,framed,token);Check(!NativeRegroupSession.Ready(s)&&!NativeRegroupSession.FixtureOwned(s)&&NativeGameStateEnvelope.Get(s)==null,"Summary gained gameplay/frame authority");}
      var write=NativeRegroupSession.BeforeTransport(states[1],false);Check(write!=null&&write.IsSummary,"Native summary's current-state serialization rejected");NativeRegroupSession.Completed(states[1],false,false,write);
      // Native GameStateSummary.FromGameStateByteArray decodes the raw
      // current-state bytes into a fourth GameState before extracting fields.
      var summaryState=new GameState();states.Add(summaryState);var summaryToken=NativeRegroupSession.BeforeTransport(summaryState,true);NativeRegroupSession.BeforeLegacyRead(summaryState,summaryToken);NativeRegroupSession.Completed(summaryState,true,false,summaryToken);Check(!NativeRegroupSession.Ready(summaryState)&&NativeGameStateEnvelope.Get(summaryState)==null,"Native summary reparse became enrolled");
     }finally{NativeRegroupSession.SummaryFinished(null,scope);}
     foreach(var s in states)Reject(()=>NativeRegroupSession.BeforeTransport(s,false),"Detached saved summary");
     Check(live==null||!live.Faulted,"Listing faulted live match");Check(NativeLedgerState.Bootstraps==(active?1:0),"Listing bootstrapped legacy data");
    });
    Test("SUMMARY-002-rejected-file-does-not-fault-live-match",()=>{
     var(c,g,t)=Create();var live=Source(c,g);Publish(c,live,t);var epoch=Guid.NewGuid();NativeRegroupSession.BeforeSummary(epoch+".state",out var scope);var s=new GameState();var token=NativeRegroupSession.BeforeTransport(s,true);
     try{Reject(()=>NativeRegroupSession.BeforeFrame(s,true,token,0,Payload(Guid.NewGuid())),"different match Guid");NativeRegroupSession.TransportFault("native rejected corrupt foreign file");NativeRegroupSession.Failed(token,"cleanup/1");Check(!t.Faulted&&NativeRegroupSession.Ready(live),"Summary rejection disabled live match");}finally{NativeRegroupSession.SummaryFinished(new IOException("bad file"),scope);}
     NativeRegroupSession.TransportFault("real gameplay transport failed");Check(t.Faulted,"Ordinary transport stopped failing closed");
    });
    foreach(bool write in new[]{false,true})Test("SUMMARY-003-cannot-touch-live-state-"+write,()=>{
     var(c,g,t)=Create();var live=Source(c,g);Publish(c,live,t);var frame=NativeGameStateEnvelope.Get(live);NativeRegroupSession.BeforeSummary(t.Epoch+".state",out var scope);try{Reject(()=>NativeRegroupSession.BeforeTransport(live,!write),"enrolled or framed gameplay state");Check(ReferenceEquals(frame,NativeGameStateEnvelope.Get(live)),"Summary erased live envelope");}finally{NativeRegroupSession.SummaryFinished(null,scope);}
    });
    Test("SUMMARY-004-writer-needs-completed-read",()=>{
     NativeRegroupSession.BeforeSummary(Guid.NewGuid()+".state",out var scope);try{Reject(()=>NativeRegroupSession.BeforeTransport(new GameState(),false),"completed detached read");var s=new GameState();NativeRegroupSession.BeforeTransport(s,true);Reject(()=>NativeRegroupSession.BeforeTransport(s,false),"completed detached read");Reject(()=>NativeRegroupSession.BeforeTransport(s,true),"already used");}finally{NativeRegroupSession.SummaryFinished(null,scope);}
    });
    Test("SUMMARY-005-exception-cleans-published-envelope-and-token",()=>{
     var epoch=Guid.NewGuid();NativeRegroupSession.BeforeSummary(epoch+".state",out var scope);var s=new GameState();var token=NativeRegroupSession.BeforeTransport(s,true);NativeGameStateEnvelope.Set(s,0,Payload(epoch));var e=new IOException("after decode");Check(ReferenceEquals(e,NativeRegroupSession.SummaryFinished(e,scope))&&NativeGameStateEnvelope.Get(s)==null,"Failed summary retained payload or replaced native error");Reject(()=>token!.Check(s,true),"ended or changed");Reject(()=>NativeRegroupSession.BeforeTransport(s,true),"Detached saved summary");
    });
    Test("SUMMARY-006-nesting-restores-exact-outer-and-epoch",()=>{
     var a=Guid.NewGuid();var b=Guid.NewGuid();NativeRegroupSession.BeforeSummary(a+".state",out var outer);var s=new GameState();var t=NativeRegroupSession.BeforeTransport(s,true);NativeRegroupSession.BeforeSummary(b+".state",out var inner);try{Reject(()=>t!.Check(s,true),"ended or changed");var n=new GameState();var nt=NativeRegroupSession.BeforeTransport(n,true);NativeRegroupSession.BeforeFrame(n,true,nt,0,Payload(b));}finally{NativeRegroupSession.SummaryFinished(null,inner);}NativeRegroupSession.BeforeFrame(s,true,t,0,Payload(a));NativeRegroupSession.SummaryFinished(null,outer);
    });
    Test("SUMMARY-007-scope-thread-confined",()=>{
     var a=Guid.NewGuid();NativeRegroupSession.BeforeSummary(a+".state",out var scope);var s=new GameState();var t=NativeRegroupSession.BeforeTransport(s,true);bool rejected=false;var thread=new Thread(()=>{try{t!.Check(s,true);}catch(InvalidDataException){rejected=true;}});thread.Start();thread.Join();NativeRegroupSession.SummaryFinished(null,scope);Check(rejected,"Summary scope crossed thread");
    });
    Test("SUMMARY-008-list-then-genuine-open-preserves-original-epoch",()=>{
     var c=new ClientBase{clientType=ClientBase.ClientType.PassAndPlay};var epoch=c.gameId.Value;NativeRegroupSession.BeforeSummary(epoch+".state",out var scope);var detached=new GameState();var st=NativeRegroupSession.BeforeTransport(detached,true);NativeRegroupSession.BeforeFrame(detached,true,st,0,Payload(epoch));NativeGameStateEnvelope.Set(detached,0,Payload(epoch));NativeRegroupSession.Completed(detached,true,true,st);NativeRegroupSession.SummaryFinished(null,scope);
     GameManager.Client=c;NativeRegroupSession.BeforeOpen(c,c.gameId,out var open);var actual=new GameState();FramedSibling(actual,epoch);c.initialGameState=actual;c.currentGameState=actual;c.ActionManager=new(){GameState=actual};NativeRegroupSession.Opened(c);Check(NativeRegroupSession.Ready(actual)&&NativeRegroupSession.CanDispatch(c)&&!NativeRegroupSession.Ready(detached),"Legitimate open borrowed summary or failed after listing");Check(NativeLedgerState.Bootstraps==0,"Open lost saved ledger");
    });
    foreach(string path in new[]{"Hotseat.state","bad.state", "00000000-0000-0000-0000-000000000000.state", "305c932c-8953-4c7a-8641-7f496ceb759e.tmp"})Test("SUMMARY-009-bad-name-"+path,()=>Reject(()=>NativeRegroupSession.BeforeSummary(path,out _),path.StartsWith("00000000")?"empty match Guid":"filename lacks"));
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Results.Count,assertions=Assertions,results=Results,nativeGame="NOT RUN",scope="Production session and paid-preflight callbacks with real immutable ledger codec; synthetic native client/graph boundaries"}));return 0;
   }catch(Exception e){Console.WriteLine(e);return 1;}
  }
 }
}
