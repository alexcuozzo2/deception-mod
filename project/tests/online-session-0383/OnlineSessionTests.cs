using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using Deception.Core;
using Deception.PolyModAdapter;
using HarmonyLib;
namespace Deception.Tests
{
 public static class OnlineSessionTests
 {
  static int Assertions;static readonly List<string> Results=new(),Failures=new();
  static void Check(bool value,string why){Assertions++;if(!value)throw new Exception(why);}
  static void Reject(Action action,string contains="")
  {try{action();}catch(InvalidDataException e){Check(e.Message.Contains(contains),"Unexpected rejection: "+e.Message);return;}throw new Exception("Expected rejection: "+contains);}
  static void Reset()
  {
   NativeRegroupSession.FixtureReset();NativeMenuPreviewScope.Finished(null,0);
   var t=typeof(NativeOnlineSession);const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
   t.GetField("Current",flags)!.SetValue(null,null);t.GetField("Pending",flags)!.SetValue(null,null);t.GetField("Active",flags)!.SetValue(null,null);t.GetField("Executing",flags)!.SetValue(null,null);
   foreach(var field in t.GetFields(flags))if(field.GetValue(null) is IDictionary dictionary)dictionary.Clear();
   t.GetProperty("Enabled",flags)!.SetValue(null,false);NativeOnlinePreview.Available=true;Harmony.Targets.Clear();
   NativeOnlineSession.Install(new Harmony());if(!NativeOnlineSession.Enabled)throw new Exception("Fixture failed to install real online adapter");
  }
  static void Test(string name,Action run)
  {try{Reset();run();Results.Add(name);}catch(Exception e){Failures.Add(name+": "+e);}}
  static LedgerAppearance Appearance()=>new("warrior",new("imperius","default"),new("imperius","default"),new("imperius","default"),2,70,100,false,ApparentConcealment.Visible,-1,new[]{"Poisoned"});
  static byte[] Payload(Guid epoch,string? contract=NativeOnlinePreview.Contract,long revision=0)
  {
   var row=new LedgerUnit(1,17,true,LedgerActualKind.Mimic,true,Appearance(),PaidInvestmentKind.Known,3);
   return NativeIdentityLedgerCodec.Encode(new(epoch,2,1,new[]{row},Array.Empty<LedgerReceipt>(),OnlineContract:contract),revision);
  }
  static byte[] Frame(Guid epoch,string? contract=NativeOnlinePreview.Contract,long revision=0,bool prefixed=false)
  {
   var frame=NativeStateFrame.Encode(BitConverter.GetBytes(123),Payload(epoch,contract,revision),revision,123);
   return prefixed?BitConverter.GetBytes(123).Concat(frame).ToArray():frame;
  }
  static NativeRegroupSession.Transport Read(GameState state,Guid epoch,string? contract=NativeOnlinePreview.Contract,long revision=0)
  {
   var token=NativeRegroupSession.BeforeTransport(state,true)??throw new Exception("Missing real read token");
   var bytes=Payload(epoch,contract,revision);NativeRegroupSession.BeforeFrame(state,true,token,revision,bytes);
   NativeGameStateEnvelope.Set(state,revision,bytes);NativeRegroupSession.Completed(state,true,true,token);return token;
  }
  static object Open(ClientBase client,bool bytes=false)
  {
   NativeRegroupSession.BeforeOpen(client,client.gameId,out var local);
   Check(local is null,"Remote client acquired Local enrollment");
   object? token;if(bytes)NativeOnlineSession.BeforeOpenBytes(client,client.gameId,out token);else NativeOnlineSession.BeforeOpen(client,client.gameId,out token);
   Check(token is not null&&!NativeRegroupSession.CanDispatch(client),"Opening remote client already dispatches");return token!;
  }
  static void Roles(ClientBase client,GameState state)
  {client.initialGameState=state;client.currentGameState=state;client.ActionManager=new(){GameState=state};}
  static (ClientBase Client,GameState State,object Open) Live()
  {
   var c=new RemoteClient{clientType=ClientBase.ClientType.Remote};var token=Open(c);var state=new GameState();Read(state,c.gameId.Value);Roles(c,state);NativeOnlineSession.Opened(c);Publish(c);return(c,state,token);
  }
  static void Publish(ClientBase client){GameManager.Client=client;NativeOnlineSession.Published(client);}
  static void Generate(GameState state)
  {NativeOnlineSession.BeforeGeneration(state,null,-1,out var token);Check(token is not null,"Generator did not receive creation witness");NativeOnlineSession.AfterGeneration(state,token,true);NativeOnlineSession.GenerationFinished(null,token);}
  public static int Run()
  {
   Assertions=0;Results.Clear();Failures.Clear();
   Test("ONLINE-001-install-exact-native-lifecycle-hooks",()=>{
    Check(Harmony.Targets.Count==13,"Unexpected online lifecycle hook count");
    Check(Harmony.Targets.Count(x=>x.Name=="CreateSession")==1&&Harmony.Targets.Any(x=>x.Name=="Generate"),"Initial bytes or generator hook absent");
    Check(Harmony.Targets.All(x=>x.ReturnType==typeof(void)||x.Name=="CreateSession"||x.Name=="ExecuteCommand"&&x.ReturnType==typeof(bool)),"Unexpected lifecycle native return shape");
   });
   Test("ONLINE-002-initial-source-first-write-includes-post-generator-starters",()=>{
    var epoch=Guid.NewGuid();var state=new GameState();using(var scope=NativeOnlineSession.BeginCreation(epoch)){
     Generate(state);Check(NativeGameStateEnvelope.Get(state) is null&&NativeLedgerState.Bootstraps==0,"Generator bootstrapped before starting units");state.StartingUnits.AddRange(new uint[]{7,21});
     var write=NativeRegroupSession.BeforeTransport(state,false);var frame=NativeGameStateEnvelope.Get(state)!;var ledger=NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision);
     Check(write?.IsOnline==true&&ledger.Epoch==epoch&&ledger.OnlineContract==NativeOnlinePreview.Contract,"Creation frame lacks exact online contract");
     Check(ledger.Units.Select(x=>x.NativeId).SequenceEqual(new uint[]{7,21})&&NativeLedgerState.Bootstraps==1,"First frame omitted native starters");
     NativeRegroupSession.BeforeFrame(state,false,write,frame.Revision,frame.Bytes());NativeRegroupSession.Completed(state,false,true,write);
     Check(NativeRegroupSession.Ready(state),"Scoped source lost owned ledger");
    }Check(!NativeRegroupSession.Ready(state)&&NativeGameStateEnvelope.Get(state) is null&&!NativeOnlineSession.HasScope,"Creation disposal retained shadow frame");
   });
   Test("ONLINE-003-unwitnessed-source-writer-denied",()=>{using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());var state=new GameState();Reject(()=>NativeRegroupSession.BeforeTransport(state,false),"generator witness");Check(NativeLedgerState.Bootstraps==0&&NativeGameStateEnvelope.Get(state) is null,"Arbitrary writer bootstrapped");});
   Test("ONLINE-004-generator-witness-is-exact-state-not-shared-map",()=>{using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());var state=new GameState();Generate(state);var other=new GameState{Map=state.Map,Settings=state.Settings};Reject(()=>NativeRegroupSession.BeforeTransport(other,false),"generator witness");Check(NativeRegroupSession.BeforeTransport(state,false)?.IsOnline==true,"Rejected foreign writer stole witness");});
   Test("ONLINE-005-replaced-map-before-first-write-denied",()=>{using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());var state=new GameState();Generate(state);state.Map=new();Reject(()=>NativeRegroupSession.BeforeTransport(state,false),"generator witness");Check(NativeLedgerState.Bootstraps==0,"Changed map bootstrapped");});
   Test("ONLINE-006-generator-must-complete-original",()=>{using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());var state=new GameState();NativeOnlineSession.BeforeGeneration(state,null,-1,out var token);Reject(()=>NativeOnlineSession.AfterGeneration(state,token,false),"did not complete");Reject(()=>NativeRegroupSession.BeforeTransport(state,false),"generator witness");});
   Test("ONLINE-007-failed-generator-faults-only-scope",()=>{var epoch=Guid.NewGuid();var scope=NativeOnlineSession.BeginCreation(epoch);var state=new GameState();NativeOnlineSession.BeforeGeneration(state,null,-1,out var token);var error=new IOException("native generation");Check(ReferenceEquals(error,NativeOnlineSession.GenerationFinished(error,token)),"Generator replaced native exception");Reject(()=>NativeRegroupSession.BeforeTransport(state,false),"faulted");Check(ReferenceEquals(error,NativeOnlineSession.Finish(scope,error))&&!NativeOnlineSession.HasScope,"Failure cleanup leaked scope");});
   Test("ONLINE-008-generator-reentry-denied",()=>{using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());var state=new GameState();Generate(state);Reject(()=>NativeOnlineSession.BeforeGeneration(new GameState(),null,-1,out _),"Unexpected online generator");Check(NativeRegroupSession.BeforeTransport(state,false)?.IsOnline==true,"Reentry replaced original source");});
   Test("ONLINE-009-generator-nondefault-callback-denied",()=>{using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());Reject(()=>NativeOnlineSession.BeforeGeneration(new GameState(),new Il2CppSystem.Action(),-1,out _),"Unexpected online generator");});
   Test("ONLINE-010-generator-nondefault-seed-denied",()=>{using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());Reject(()=>NativeOnlineSession.BeforeGeneration(new GameState(),null,6,out _),"Unexpected online generator");});
   Test("ONLINE-011-generator-version-denied",()=>{using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());Reject(()=>NativeOnlineSession.BeforeGeneration(new GameState{Version=122},null,-1,out _),"Unexpected online generator");});
   Test("ONLINE-012-creation-registration-failure-removes-incomplete-frame",()=>{using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());var state=new GameState();Generate(state);NativeBlackout.Fail=true;try{NativeRegroupSession.BeforeTransport(state,false);throw new Exception("Expected bind failure");}catch(InvalidOperationException){Check(NativeGameStateEnvelope.Get(state) is null&&!NativeRegroupSession.Ready(state),"Failed initial registration left usable ledger");}});
   Test("ONLINE-013-detached-framed-read-write-and-sibling",()=>{
    var epoch=Guid.NewGuid();var a=new GameState();var b=new GameState();using(var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch,prefixed:true),"fixture")){
     var read=Read(a,epoch,revision:3);Read(b,epoch,revision:3);Check(NativeRegroupSession.Ready(a)&&NativeRegroupSession.Ready(b),"Framed detached siblings not ready");
     var write=NativeRegroupSession.BeforeTransport(b,false);NativeRegroupSession.BeforeFrame(b,false,write,3,Payload(epoch,revision:3));Check(write?.IsOnline==true&&NativeLedgerState.Bootstraps==0,"Detached writer bootstrapped or missed owner");
     Reject(()=>read.Check(b,true),"changed state");Reject(()=>read.Check(a,false),"direction");
    }Check(NativeGameStateEnvelope.Get(a) is null&&NativeGameStateEnvelope.Get(b) is null&&!NativeRegroupSession.Ready(a),"Detached disposal retained siblings");
   });
   Test("ONLINE-014-wrong-build-before-detached-read-denied",()=>{var epoch=Guid.NewGuid();Reject(()=>NativeOnlineSession.BeginDetached(epoch,Frame(epoch,new string('b',64)),"fixture"),"contract");Check(!NativeOnlineSession.HasScope&&NativeGameStateEnvelope.Frames.Count==0,"Wrong build created authority");});
   Test("ONLINE-015-wrong-epoch-before-detached-read-denied",()=>{Reject(()=>NativeOnlineSession.BeginDetached(Guid.NewGuid(),Frame(Guid.NewGuid()),"fixture"),"different native match");Check(!NativeOnlineSession.HasScope,"Wrong epoch leaked scope");});
   Test("ONLINE-016-missing-frame-before-detached-read-denied",()=>{Reject(()=>NativeOnlineSession.BeginDetached(Guid.NewGuid(),BitConverter.GetBytes(123),"fixture"),"Missing version123");Check(!NativeOnlineSession.HasScope,"Unframed input gained scope");});
   Test("ONLINE-017-local-ledger-contract-cannot-enter-online",()=>{var epoch=Guid.NewGuid();Reject(()=>NativeOnlineSession.BeginDetached(epoch,Frame(epoch,null),"fixture"),"contract");});
   Test("ONLINE-018-wrong-frame-during-scoped-native-read-denied",()=>{var epoch=Guid.NewGuid();using var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"fixture");var state=new GameState();var token=NativeRegroupSession.BeforeTransport(state,true);Reject(()=>NativeRegroupSession.BeforeFrame(state,true,token,0,Payload(Guid.NewGuid())),"different game");Reject(()=>NativeRegroupSession.BeforeFrame(state,true,token,0,Payload(epoch,new string('c',64))),"different game");Check(NativeGameStateEnvelope.Get(state) is null,"Rejected frame published");});
   Test("ONLINE-019-online-legacy-read-never-bootstraps",()=>{var epoch=Guid.NewGuid();using var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"fixture");var state=new GameState();var token=NativeRegroupSession.BeforeTransport(state,true);Reject(()=>NativeRegroupSession.BeforeLegacyRead(state,token),"unbound legacy");Reject(()=>NativeRegroupSession.Completed(state,true,false,token),"Legacy enrollment");Check(NativeLedgerState.Bootstraps==0&&NativeGameStateEnvelope.Get(state) is null,"Online legacy state bootstrapped");});
   Test("ONLINE-020-read-receiver-must-be-unbound",()=>{var epoch=Guid.NewGuid();using var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"fixture");var state=new GameState();Read(state,epoch);Reject(()=>NativeRegroupSession.BeforeTransport(state,true),"already enrolled");});
   Test("ONLINE-021-detached-finalizer-preserves-error-cleans-frame",()=>{var epoch=Guid.NewGuid();var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"fixture");var state=new GameState();var token=Read(state,epoch);var error=new IOException("native decode");Check(ReferenceEquals(error,NativeOnlineSession.Finish(scope,error))&&!NativeOnlineSession.HasScope&&NativeGameStateEnvelope.Get(state) is null,"Detached finalizer failed cleanup");Reject(()=>token.Check(state,true),"expired");});
   Test("ONLINE-022-nested-same-game-restores-outer-authority",()=>{var epoch=Guid.NewGuid();using var outer=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"outer");var a=new GameState();var token=Read(a,epoch);var b=new GameState();using(var inner=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"inner")){Read(b,epoch);Reject(()=>token.Check(a,true),"no longer current");}token.Check(a,true);Check(NativeRegroupSession.Ready(a)&&NativeGameStateEnvelope.Get(b) is null,"Nested scope damaged outer/retained inner");});
   Test("ONLINE-023-nested-wrong-game-denied",()=>{var epoch=Guid.NewGuid();using var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"outer");var other=Guid.NewGuid();Reject(()=>NativeOnlineSession.BeginDetached(other,Frame(other),"inner"),"changed game");Check(NativeOnlineSession.HasScope,"Wrong game removed original scope");});
   Test("ONLINE-024-nested-creation-authority-denied",()=>{var epoch=Guid.NewGuid();using var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"outer");Reject(()=>NativeOnlineSession.BeginCreation(epoch),"creation authority");});
   Test("ONLINE-025-cross-thread-scope-and-token-denied",()=>{var epoch=Guid.NewGuid();using var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"main");var state=new GameState();var token=Read(state,epoch);Exception? a=null,b=null;var thread=new Thread(()=>{try{NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"worker");}catch(Exception e){a=e;}try{token.Check(state,true);}catch(Exception e){b=e;}});thread.Start();thread.Join();Check(a is InvalidDataException&&b is InvalidDataException,"Thread crossing gained session authority");Check(NativeRegroupSession.Ready(state),"Worker rejection faulted valid caller");});
   Test("ONLINE-026-nested-cleanup-must-be-lifo",()=>{var epoch=Guid.NewGuid();var outer=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"outer");var inner=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"inner");Reject(()=>outer.Dispose(),"cleanup order");inner.Dispose();outer.Dispose();outer.Dispose();Check(!NativeOnlineSession.HasScope,"LIFO cleanup or idempotency failed");});
   Test("ONLINE-027-scope-depth-is-bounded",()=>{var epoch=Guid.NewGuid();var scopes=new List<IDisposable>();try{for(int i=0;i<8;i++)scopes.Add(NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"nested"));Reject(()=>NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"overflow"),"nesting bound");}finally{for(int i=scopes.Count-1;i>=0;i--)scopes[i].Dispose();}Check(!NativeOnlineSession.HasScope,"Bound rejection leaked scopes");});
   Test("ONLINE-028-live-open-adopt-all-native-roles-dispatch",()=>{var(c,s,_)=Live();Check(NativeRegroupSession.Ready(s)&&NativeRegroupSession.CanDispatch(c)&&NativeRegroupSession.BeforeDispatch(c),"Live open failed dispatch");var sibling=new GameState();Read(sibling,c.gameId.Value);Check(NativeOnlineSession.BeforeAdopt(c,sibling),"Framed adoption rejected");c.targetGameState=sibling;c.lastTurnGameState=sibling;c.ActionManager!.GameState=sibling;NativeOnlineSession.AfterClient(c,true);Check(NativeOnlineSession.BeforeClient(c)&&NativeRegroupSession.Ready(sibling)&&NativeLedgerState.Bootstraps==0,"Native sibling roles invalidated online session");});
   Test("ONLINE-029-live-create-from-bytes-completion",()=>{var c=new RemoteClient{clientType=ClientBase.ClientType.Remote};var token=Open(c,true);var state=new GameState();Read(state,c.gameId.Value);Roles(c,state);NativeOnlineSession.Created(token,0);Check(!NativeRegroupSession.CanDispatch(c),"Creation authorized dispatch before publication");Publish(c);Check(NativeRegroupSession.CanDispatch(c)&&NativeRegroupSession.Ready(state),"Remote bytes creation did not finish");});
   Test("ONLINE-030-live-create-failure-does-not-enable-dispatch",()=>{var c=new RemoteClient{clientType=ClientBase.ClientType.Remote};var token=Open(c,true);NativeOnlineSession.Created(token,1);Check(!NativeRegroupSession.CanDispatch(c),"Failed native creation remained usable");Reject(()=>NativeRegroupSession.BeforeTransport(new GameState(),true),"faulted");});
   Test("ONLINE-031-live-open-native-exception-faults",()=>{var c=new RemoteClient{clientType=ClientBase.ClientType.Remote};var token=Open(c);var error=new IOException("native open");Check(ReferenceEquals(error,NativeOnlineSession.OpenFinished(error,token))&&!NativeRegroupSession.CanDispatch(c),"Open exception was swallowed or usable");Reject(()=>NativeRegroupSession.BeforeTransport(new GameState(),true),"faulted");});
   Test("ONLINE-032-live-unframed-adoption-denied",()=>{var(c,s,_)=Live();Check(!NativeOnlineSession.BeforeAdopt(c,new GameState())&&NativeOnlineSession.SessionFaulted&&!NativeRegroupSession.CanDispatch(c),"Unframed adoption did not stop commands");Check(NativeRegroupSession.ReadAppearance(s,17)?.Health==70,"Fault erased previously accepted apparent facts");});
   Test("ONLINE-033-live-completion-requires-initial-current-manager",()=>{var c=new RemoteClient{clientType=ClientBase.ClientType.Remote};Open(c);var s=new GameState();Read(s,c.gameId.Value);c.currentGameState=s;Reject(()=>NativeOnlineSession.Opened(c),"required states");Check(!NativeRegroupSession.CanDispatch(c),"Incomplete roles dispatched");Reject(()=>NativeRegroupSession.BeforeTransport(new GameState(),true),"faulted");});
   Test("ONLINE-034-live-rejects-unowned-action-manager-state",()=>{var(c,s,_)=Live();c.ActionManager!.GameState=new GameState();Check(!NativeOnlineSession.BeforeClient(c)&&NativeOnlineSession.SessionFaulted,"Unframed action manager gained authority");Check(NativeRegroupSession.ReadAppearance(s,17)?.Form=="warrior","Role failure erased valid public art");});
   Test("ONLINE-035-out-of-turn-apparent-data-remains-readable",()=>{var(c,s,_)=Live();c.LocalPlayerId=1;s.CurrentPlayer=2;s.CurrentTurn=18;Check(NativeRegroupSession.ReadAppearance(s,17)?.Health==70&&NativeRegroupSession.ReadIdentity(s,17)?.Kind==LedgerActualKind.Mimic,"Other player's turn dropped apparent frame");Check(NativeLedgerState.Reconciliations>0,"Fixture never traversed real roles");});
   Test("ONLINE-036-remote-fault-stops-writes-keeps-immutable-apparent-facts",()=>{var(c,s,_)=Live();var original=NativeGameStateEnvelope.Get(s)!;NativeRegroupSession.Fault("secret body details");Check(!NativeRegroupSession.Ready(s)&&!NativeRegroupSession.CanDispatch(c),"Fault kept action authority");var appearance=NativeRegroupSession.ReadAppearance(s,17)!;appearance.Effects[0]="Swift";Check(NativeRegroupSession.ReadAppearance(s,17)?.Effects.Single()=="Poisoned"&&ReferenceEquals(original,NativeGameStateEnvelope.Get(s)),"Presentation read edited or dropped immutable frame");Reject(()=>NativeRegroupSession.BeforeTransport(s,false),"faulted");Check(Entry.Notes.All(x=>!x.Contains("secret body details")),"Online fault leaked hidden details");});
   Test("ONLINE-037-transport-failure-faults-owner-preserves-other-frame",()=>{var(c,s,_)=Live();var bad=new GameState();var token=NativeRegroupSession.BeforeTransport(bad,true);NativeRegroupSession.Failed(token,"checksum");Check(!NativeRegroupSession.CanDispatch(c)&&NativeOnlineSession.SessionFaulted&&NativeRegroupSession.ReadAppearance(s,17)?.Health==70,"Transport failure lost accepted public appearance or kept command authority");});
   Test("ONLINE-038-detached-fault-does-not-fault-live-owner",()=>{var(c,s,_)=Live();using(var scope=NativeOnlineSession.BeginDetached(c.gameId.Value,Frame(c.gameId.Value),"shadow")){var shadow=new GameState();Read(shadow,c.gameId.Value);NativeRegroupSession.Fault("shadow failure");Check(!NativeRegroupSession.Ready(shadow),"Faulted shadow stayed ready");}Check(NativeRegroupSession.Ready(s)&&NativeRegroupSession.CanDispatch(c)&&!NativeOnlineSession.SessionFaulted,"Detached failure poisoned live client");});
   Test("ONLINE-039-live-state-cannot-borrow-shadow-writer",()=>{var(c,s,_)=Live();using(var scope=NativeOnlineSession.BeginDetached(c.gameId.Value,Frame(c.gameId.Value),"shadow")){Reject(()=>NativeRegroupSession.BeforeTransport(s,false),"exact state ownership");Check(NativeRegroupSession.ReadAppearance(s,17) is null,"Detached operation borrowed live presentation facts");}Check(NativeRegroupSession.ReadAppearance(s,17)?.Health==70,"Rejected cross-owner write changed accepted public facts");});
   Test("ONLINE-040-shadow-cannot-be-adopted-by-live-client",()=>{var(c,s,_)=Live();var shadow=new GameState();using(var scope=NativeOnlineSession.BeginDetached(c.gameId.Value,Frame(c.gameId.Value),"shadow")){Read(shadow,c.gameId.Value);Check(!NativeOnlineSession.BeforeAdopt(c,shadow),"Live client adopted active shadow");}Check(!NativeRegroupSession.Ready(shadow)&&NativeGameStateEnvelope.Get(shadow) is null,"Shadow remained framed after scope");Check(!NativeOnlineSession.BeforeAdopt(c,shadow),"Disposed shadow escaped into live client");Check(NativeRegroupSession.ReadAppearance(s,17)?.Health==70,"Shadow escape attempt erased live appearance");});
   Test("ONLINE-041-reconnect-same-game-new-native-client",()=>{var(c,s,_)=Live();var epoch=c.gameId.Value;NativeOnlineSession.Ended(c);Check(!NativeRegroupSession.Ready(s),"Ended client retained authority");var replacement=new RemoteClient{clientType=ClientBase.ClientType.Remote,gameId=new(epoch)};Open(replacement);var fresh=new GameState();Read(fresh,epoch);Roles(replacement,fresh);NativeOnlineSession.Opened(replacement);Publish(replacement);Check(NativeRegroupSession.CanDispatch(replacement)&&NativeRegroupSession.Ready(fresh)&&!NativeRegroupSession.Ready(s),"Reconnect reused old native state or failed fresh frame");Check(NativeRegroupSession.ReadAppearance(fresh,17)?.Health==70,"Reconnect lost persistent appearance");Reject(()=>NativeRegroupSession.BeforeTransport(s,false));});
   Test("ONLINE-042-reopen-faulted-same-client-with-fresh-framed-state",()=>{var(c,s,_)=Live();NativeRegroupSession.Fault("network reset");var token=Open(c);var fresh=new GameState();Read(fresh,c.gameId.Value);Roles(c,fresh);NativeOnlineSession.Opened(c);Publish(c);Check(NativeRegroupSession.CanDispatch(c)&&NativeRegroupSession.Ready(fresh)&&!NativeRegroupSession.Ready(s),"Faulted generation remained owner after reopen");});
   Test("ONLINE-043-wrong-match-on-reconnect-denied",()=>{var(c,s,_)=Live();var previous=c.gameId.Value;NativeOnlineSession.Ended(c);var next=new RemoteClient{clientType=ClientBase.ClientType.Remote};Open(next);var fresh=new GameState();var token=NativeRegroupSession.BeforeTransport(fresh,true);Reject(()=>NativeRegroupSession.BeforeFrame(fresh,true,token,0,Payload(previous)),"different game");Check(NativeGameStateEnvelope.Get(fresh) is null&&!NativeRegroupSession.Ready(s),"Foreign reconnect resurrected state");});
   Test("ONLINE-044-client-replacement-and-gameid-mutation-revoke-observation",()=>{var(c,s,_)=Live();c.gameId=new(Guid.NewGuid());Check(NativeRegroupSession.ReadAppearance(s,17) is null&&!NativeRegroupSession.CanDispatch(c),"Changed gameId retained private read");GameManager.Client=new(){clientType=ClientBase.ClientType.Remote};Check(NativeRegroupSession.ReadIdentity(s,17) is null,"Wrong active client retained private read");});
   Test("ONLINE-045-remote-replay-and-spectator-not-enrolled",()=>{foreach(bool replay in new[]{true,false}){var c=new RemoteClient{clientType=ClientBase.ClientType.Remote,IsReplay=replay,IsSpectating=!replay};GameManager.Client=c;NativeOnlineSession.BeforeOpen(c,c.gameId,out var token);Check(token is null&&NativeOnlineSession.Snapshot(new GameState()) is null,"Unsupported observer acquired online authority");}});
   Test("ONLINE-046-fake-preview-cannot-open-online",()=>{var c=new RemoteClient{clientType=ClientBase.ClientType.Remote};GameManager.Client=c;GameManager.instance!.fakeSession=new();Reject(()=>NativeOnlineSession.BeforeOpen(c,c.gameId,out _),"preview");Check(!NativeOnlineSession.CanDispatch(),"Fake session dispatched");});
   Test("ONLINE-047-count-scope-does-not-enroll-local-ledger",()=>{var epoch=Guid.NewGuid();Check(NativeOnlineSession.BeginCount(Frame(epoch,null)) is null&&!NativeOnlineSession.HasScope,"Ordinary local count gained online authority");using var scope=NativeOnlineSession.BeginCount(Frame(epoch));Check(scope is not null&&NativeOnlineSession.HasScope,"Framed online count did not open detached scope");});
   Test("ONLINE-048-ordinary-local-regression-through-integrated-online-adapter",()=>{var c=new ClientBase();GameManager.Client=c;var settings=new GameSettings();NativeRegroupSession.BeforeCreate(c,settings,c.gameId,typeof(ClientBase).GetMethod("CreateSession",new[]{typeof(GameSettings),typeof(Il2CppSystem.Guid)})!,out var token);var state=new GameState{Settings=settings};NativeRegroupSession.AfterLocal(123,settings,state,true);Roles(c,state);NativeRegroupSession.Opened(c);NativeRegroupSession.AfterCreate(token,0);NativeRegroupSession.CreationFinished(null,token);Check(NativeRegroupSession.Ready(state)&&NativeRegroupSession.CanDispatch(c)&&NativeLedgerState.Bootstraps==1,"Enabled online adapter broke genuine local lifecycle");Check(NativeIdentityLedgerCodec.Decode(NativeGameStateEnvelope.Get(state)!.Bytes(),0).OnlineContract is null,"Local source falsely became online");});
   Test("ONLINE-049-open-before-publication-cannot-expose-pending-facts",()=>{
    var old=new ClientBase();GameManager.Client=old;var c=new RemoteClient();Open(c);var state=new GameState();Read(state,c.gameId.Value);Roles(c,state);NativeOnlineSession.Opened(c);
    Check(ReferenceEquals(GameManager.Client,old)&&!NativeRegroupSession.CanDispatch(c),"Pending open published itself or dispatched");
    Check(NativeOnlineSession.Snapshot(state,true) is null,"Pending online presentation frame leaked before SetRemoteClient");
    Check(NativeRegroupSession.ReadAppearance(state,17) is null&&NativeRegroupSession.ReadIdentity(state,17) is null,"Integrated read fallback bypassed pending-client presentation fence");
    Publish(c);Check(NativeRegroupSession.CanDispatch(c)&&NativeRegroupSession.ReadAppearance(state,17)?.Health==70,"Exact publication did not release accepted facts");
   });
   Test("ONLINE-050-pending-other-match-preserves-current-live-ownership",()=>{
    var(old,state,_)=Live();var pending=new RemoteClient();Open(pending);var future=new GameState();Read(future,pending.gameId.Value);Roles(pending,future);NativeOnlineSession.Opened(pending);
    Check(NativeRegroupSession.CanDispatch(old)&&NativeRegroupSession.ReadAppearance(state,17)?.Health==70,"Pending different match retired active client too early");
    Check(NativeRegroupSession.BeforeTransport(state,false)?.IsOnline==true,"Pending match stole current live writer");
    Publish(pending);Check(!NativeRegroupSession.CanDispatch(old)&&NativeRegroupSession.ReadIdentity(state,17) is null&&NativeRegroupSession.CanDispatch(pending),"Published replacement kept previous authority");
   });
   Test("ONLINE-051-failed-pending-open-keeps-current-presentation",()=>{
    var(old,state,_)=Live();var pending=new RemoteClient();var token=Open(pending);var error=new IOException("pending open failed");NativeOnlineSession.OpenFinished(error,token);
    Check(NativeRegroupSession.ReadAppearance(state,17)?.Health==70&&NativeRegroupSession.CanDispatch(old),"Failed pending match damaged active client");
    NativeOnlineSession.Ended(pending);Check(NativeRegroupSession.CanDispatch(old)&&!NativeOnlineSession.SessionFaulted,"Retiring failed pending match retired live client");
   });
   Test("ONLINE-052-publication-requires-exact-active-pending-client",()=>{
    var pending=new RemoteClient();Open(pending);var s=new GameState();Read(s,pending.gameId.Value);Roles(pending,s);NativeOnlineSession.Opened(pending);GameManager.Client=new RemoteClient();
    Reject(()=>NativeOnlineSession.Published(pending),"did not select");Check(!NativeRegroupSession.CanDispatch(pending),"Unselected publication gained command authority");Reject(()=>NativeRegroupSession.BeforeTransport(new GameState(),true),"faulted");
   });
   Test("ONLINE-053-cross-thread-live-open-denied",()=>{
    var c=new RemoteClient();Exception? error=null;var thread=new Thread(()=>{try{NativeOnlineSession.BeforeOpen(c,c.gameId,out _);}catch(Exception e){error=e;}});thread.Start();thread.Join();Check(error is InvalidDataException&&!NativeOnlineSession.CanDispatch(c),"Worker thread enrolled live client");
   });
   Test("ONLINE-054-failed-pending-remote-does-not-fault-active-local",()=>{
    var c=new ClientBase();GameManager.Client=c;var settings=new GameSettings();NativeRegroupSession.BeforeCreate(c,settings,c.gameId,typeof(ClientBase).GetMethod("CreateSession",new[]{typeof(GameSettings),typeof(Il2CppSystem.Guid)})!,out var local);
    var state=new GameState{Settings=settings};NativeRegroupSession.AfterLocal(123,settings,state,true);Roles(c,state);NativeRegroupSession.Opened(c);NativeRegroupSession.AfterCreate(local,0);NativeRegroupSession.CreationFinished(null,local);
    var pending=new RemoteClient();var token=Open(pending);NativeOnlineSession.OpenFinished(new IOException("network open failure"),token);
    Check(NativeRegroupSession.Ready(state)&&NativeRegroupSession.CanDispatch(c),"Failed remote open retired active Local state");
    Check(!NativeRegroupSession.SessionFaulted,"Failed pending online match poisoned unrelated Local presentation fault guard");
   });
   Test("ONLINE-055-disposed-shadow-cannot-escape-as-unframed-writer",()=>{
    var epoch=Guid.NewGuid();var state=new GameState();using(var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"shadow")){Read(state,epoch);}
    Check(NativeGameStateEnvelope.Get(state) is null&&!NativeRegroupSession.Ready(state),"Disposed shadow retained frame");
    Reject(()=>NativeRegroupSession.BeforeTransport(state,false));
   });
   Test("ONLINE-056-new-local-retires-old-online-and-pending-generations",()=>{
    var(old,oldState,_)=Live();var pending=new RemoteClient();Open(pending);var future=new GameState();Read(future,pending.gameId.Value);NativeOnlineSession.Fault("pending failure");
    var local=new ClientBase();GameManager.Client=local;var settings=new GameSettings();NativeRegroupSession.BeforeCreate(local,settings,local.gameId,typeof(ClientBase).GetMethod("CreateSession",new[]{typeof(GameSettings),typeof(Il2CppSystem.Guid)})!,out var token);
    var state=new GameState{Settings=settings};NativeRegroupSession.AfterLocal(123,settings,state,true);Roles(local,state);NativeRegroupSession.Opened(local);NativeRegroupSession.AfterCreate(token,0);NativeRegroupSession.CreationFinished(null,token);
    Check(NativeRegroupSession.CanDispatch(local)&&!NativeRegroupSession.SessionFaulted&&NativeRegroupSession.Ready(state),"New Local match inherited remote failure");
    Check(NativeRegroupSession.ReadAppearance(oldState,17) is null&&NativeRegroupSession.ReadAppearance(future,17) is null&&!NativeRegroupSession.CanDispatch(old),"Local switch retained online presentation/dispatch authority");
    Reject(()=>NativeRegroupSession.BeforeTransport(oldState,false));Reject(()=>NativeRegroupSession.BeforeTransport(future,false));
   });
   Test("ONLINE-057-disposed-shadow-cannot-become-native-menu-preview",()=>{
    var epoch=Guid.NewGuid();var state=new GameState();using(var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"summary-out")){Read(state,epoch);}
    NativeMenuPreviewScope.Before(out var old);try{Reject(()=>NativeRegroupSession.BeforeTransport(state,false));Reject(()=>NativeRegroupSession.BeforeTransport(state,true));}finally{NativeMenuPreviewScope.Finished(null,old);}
    Check(!NativeRegroupSession.Ready(state),"Disposed shadow escaped through preview scope");
   });
   Test("ONLINE-058-disposed-shadow-cannot-borrow-saved-summary-authority",()=>{
    var epoch=Guid.NewGuid();var state=new GameState();using(var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"summary-out")){Read(state,epoch);}
    NativeRegroupSession.BeforeSummary(epoch+".state",out var summary);try{Reject(()=>NativeRegroupSession.BeforeTransport(state,true));}finally{NativeRegroupSession.SummaryFinished(null,summary);}
    Check(NativeGameStateEnvelope.Get(state) is null,"Saved summary resurrected shadow frame");
   });
   Test("ONLINE-059-online-frame-cannot-migrate-through-local-open",()=>{
    var c=new ClientBase();GameManager.Client=c;NativeRegroupSession.BeforeOpen(c,c.gameId,out _);var state=new GameState();var token=NativeRegroupSession.BeforeTransport(state,true);
    Reject(()=>NativeRegroupSession.BeforeFrame(state,true,token,0,Payload(c.gameId.Value)));Check(NativeGameStateEnvelope.Get(state) is null&&!NativeRegroupSession.Ready(state),"Local open adopted online compatibility state");
   });
   Test("ONLINE-060-disposed-shadow-cannot-be-generated-into-fresh-source",()=>{
    var epoch=Guid.NewGuid();var state=new GameState();using(var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"old")){Read(state,epoch);}
    using var creation=NativeOnlineSession.BeginCreation(epoch);Reject(()=>NativeOnlineSession.BeforeGeneration(state,null,-1,out _));Check(NativeLedgerState.Bootstraps==0,"Disposed shadow re-bootstrapped through creation");
   });
   Test("ONLINE-061-pending-execution-fault-belongs-to-pending-owner",()=>{
    var(old,state,_)=Live();var pending=new RemoteClient();Open(pending);var future=new GameState();var read=Read(future,pending.gameId.Value);Roles(pending,future);NativeOnlineSession.BeforeExecution(pending.ActionManager!,out var execution);
    Check(execution is not null,"Pending native execution lacks its state owner");try{NativeRegroupSession.Fault("pending action failure");}finally{NativeOnlineSession.ExecutionFinished(null,execution);}
    Check(NativeRegroupSession.CanDispatch(old)&&NativeRegroupSession.ReadAppearance(state,17)?.Health==70&&!NativeRegroupSession.SessionFaulted,"Pending action failure poisoned published state");
    Reject(()=>read.Check(future,true),"faulted");Check(NativeOnlineSession.CanDispatch(old),"Execution cleanup retained pending command authority");
   });
   Test("ONLINE-062-old-local-writer-and-local-fault-ignore-pending-owner",()=>{
    var c=new ClientBase();GameManager.Client=c;var settings=new GameSettings();NativeRegroupSession.BeforeCreate(c,settings,c.gameId,typeof(ClientBase).GetMethod("CreateSession",new[]{typeof(GameSettings),typeof(Il2CppSystem.Guid)})!,out var local);
    var state=new GameState{Settings=settings};NativeRegroupSession.AfterLocal(123,settings,state,true);Roles(c,state);NativeRegroupSession.Opened(c);NativeRegroupSession.AfterCreate(local,0);NativeRegroupSession.CreationFinished(null,local);
    var pending=new RemoteClient();Open(pending);var future=new GameState();var read=Read(future,pending.gameId.Value);Roles(pending,future);
    var write=NativeRegroupSession.BeforeTransport(state,false);Check(write is not null&&!write.IsOnline,"Pending remote open stole exact Local writer");
    NativeRegroupSession.Fault("actual local command fault");Check(!NativeRegroupSession.CanDispatch(c)&&NativeRegroupSession.SessionFaulted,"Actual Local fault was incorrectly routed to pending remote");
    read.Check(future,true);Check(NativeRegroupSession.Ready(future),"Local failure faulted unrelated pending frame");
   });
   Test("ONLINE-063-native-execution-exception-preserved-and-scope-restored",()=>{
    var(old,state,_)=Live();var pending=new RemoteClient();Open(pending);var future=new GameState();Read(future,pending.gameId.Value);Roles(pending,future);NativeOnlineSession.BeforeExecution(pending.ActionManager!,out var execution);var error=new IOException("native action failed");
    Check(ReferenceEquals(error,NativeOnlineSession.ExecutionFinished(error,execution)),"Execution finalizer swallowed native failure");
    Check(NativeRegroupSession.CanDispatch(old)&&NativeRegroupSession.ReadAppearance(state,17)?.Health==70,"Execution exception faulted wrong owner");
    NativeRegroupSession.Fault("later active fault");Check(!NativeRegroupSession.CanDispatch(old),"Execution finalizer left stale pending fault routing");
   });
   Test("ONLINE-064-pending-remote-allows-exact-local-framed-clone",()=>{
    var c=new ClientBase();GameManager.Client=c;var settings=new GameSettings();NativeRegroupSession.BeforeCreate(c,settings,c.gameId,typeof(ClientBase).GetMethod("CreateSession",new[]{typeof(GameSettings),typeof(Il2CppSystem.Guid)})!,out var local);
    var state=new GameState{Settings=settings};NativeRegroupSession.AfterLocal(123,settings,state,true);Roles(c,state);NativeRegroupSession.Opened(c);NativeRegroupSession.AfterCreate(local,0);NativeRegroupSession.CreationFinished(null,local);
    var pending=new RemoteClient();Open(pending);var sibling=new GameState();var token=NativeRegroupSession.BeforeTransport(sibling,true);Check(token?.IsOnline==true,"Fixture did not enter pending-read arbitration");
    var bytes=Payload(c.gameId.Value,null);NativeRegroupSession.BeforeFrame(sibling,true,token,0,bytes);Check(!token!.IsOnline,"Exact Local frame remained assigned to pending remote");NativeGameStateEnvelope.Set(sibling,0,bytes);NativeRegroupSession.Completed(sibling,true,true,token);
    Check(NativeRegroupSession.Ready(sibling)&&NativeRegroupSession.CanDispatch(c)&&NativeRegroupSession.BeforeTransport(sibling,false)?.IsOnline==false,"Local clone lost its original authority");
    var bad=new GameState();var wrong=NativeRegroupSession.BeforeTransport(bad,true);Reject(()=>NativeRegroupSession.BeforeFrame(bad,true,wrong,0,Payload(Guid.NewGuid(),null)));Reject(()=>NativeRegroupSession.BeforeFrame(bad,true,wrong,0,Payload(c.gameId.Value)));Reject(()=>NativeRegroupSession.BeforeFrame(bad,true,wrong,0,Payload(pending.gameId.Value,new string('b',64))));
    var future=new GameState();Read(future,pending.gameId.Value);Check(NativeRegroupSession.Ready(future)&&NativeRegroupSession.ReadAppearance(future,17) is null,"Arbitration admitted foreign facts or prevented real pending read");
   });
   Test("ONLINE-065-detached-scope-cannot-borrow-published-local",()=>{
    var c=new ClientBase();GameManager.Client=c;var settings=new GameSettings();NativeRegroupSession.BeforeCreate(c,settings,c.gameId,typeof(ClientBase).GetMethod("CreateSession",new[]{typeof(GameSettings),typeof(Il2CppSystem.Guid)})!,out var local);
    var state=new GameState{Settings=settings};NativeRegroupSession.AfterLocal(123,settings,state,true);Roles(c,state);NativeRegroupSession.Opened(c);NativeRegroupSession.AfterCreate(local,0);NativeRegroupSession.CreationFinished(null,local);var before=NativeGameStateEnvelope.Get(state);
    var epoch=Guid.NewGuid();using(var scope=NativeOnlineSession.BeginDetached(epoch,Frame(epoch),"shadow")){
     Check(!NativeRegroupSession.Ready(state)&&NativeRegroupSession.ReadIdentity(state,17) is null,"Detached scope borrowed Local readiness/read view");Reject(()=>NativeRegroupSession.Prepare(state));Reject(()=>NativeRegroupSession.BeforeTransport(state,false));Reject(()=>NativeOnlineSession.BeforeExecution(c.ActionManager!,out _));
    }Check(ReferenceEquals(before,NativeGameStateEnvelope.Get(state))&&NativeRegroupSession.Ready(state)&&NativeRegroupSession.CanDispatch(c),"Scope rejection changed Local frame or authority");
   });
   Test("ONLINE-066-detached-scope-cannot-prepare-execute-published-live",()=>{
    var(c,state,_)=Live();var before=NativeGameStateEnvelope.Get(state);using(var scope=NativeOnlineSession.BeginDetached(c.gameId.Value,Frame(c.gameId.Value),"shadow")){
     Check(!NativeRegroupSession.Ready(state)&&NativeRegroupSession.ReadAppearance(state,17) is null&&!NativeRegroupSession.CanDispatch(c),"Detached scope borrowed published live authority");Reject(()=>NativeRegroupSession.Prepare(state));Reject(()=>NativeOnlineSession.BeforeExecution(c.ActionManager!,out _));
    }Check(ReferenceEquals(before,NativeGameStateEnvelope.Get(state))&&NativeRegroupSession.CanDispatch(c)&&NativeRegroupSession.ReadAppearance(state,17)?.Health==70,"Detached rejection changed live frame or command authority");
   });
   Test("ONLINE-067-generated-source-executes-starting-actions-before-first-frame",()=>{
    var epoch=Guid.NewGuid();using var scope=NativeOnlineSession.BeginCreation(epoch);var state=new GameState();Generate(state);state.StartingUnits.AddRange(new uint[]{7,21});var manager=new ActionManager{GameState=state};
    NativeOnlineSession.BeforeExecution(manager,out var execution);Check(execution is not null,"Exact generated source could not execute native starting actions");
    Native.Invoke(Native.M("ActionManager","Update"),manager);Check(NativeOnlineSession.ExecutionFinished(null,execution) is null&&manager.Updates==1,"Preframe execution did not finish exactly once");
    Check(NativeGameStateEnvelope.Get(state) is null&&NativeLedgerState.Bootstraps==0,"Starting execution created a premature frame");
    var writer=NativeRegroupSession.BeforeTransport(state,false);var frame=NativeGameStateEnvelope.Get(state)!;var ledger=NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision);
    Check(writer?.IsOnline==true&&ledger.Epoch==epoch&&ledger.OnlineContract==NativeOnlinePreview.Contract&&ledger.Units.Select(x=>x.NativeId).SequenceEqual(new uint[]{7,21}),"First post-start frame lost its source contract or starting graph");
   });
   Test("ONLINE-068-preframe-execution-rejects-wrong-source",()=>{
    using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());var source=new GameState();Generate(source);var manager=new ActionManager{GameState=new GameState()};Reject(()=>NativeOnlineSession.BeforeExecution(manager,out _));Check(manager.Updates==0&&NativeLedgerState.Bootstraps==0,"Foreign source executed or bootstrapped");
   });
   Test("ONLINE-069-preframe-execution-rejects-shared-map-impostor",()=>{
    using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());var source=new GameState();Generate(source);var manager=new ActionManager{GameState=new GameState{Map=source.Map,Settings=source.Settings}};Reject(()=>NativeOnlineSession.BeforeExecution(manager,out _));Check(manager.Updates==0&&NativeGameStateEnvelope.Get(source) is null,"Shared-map impostor gained source authority");
   });
   Test("ONLINE-070-preframe-execution-requires-completed-generation",()=>{
    using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());var source=new GameState();var manager=new ActionManager{GameState=source};Reject(()=>NativeOnlineSession.BeforeExecution(manager,out _));NativeOnlineSession.BeforeGeneration(source,null,-1,out var generation);Reject(()=>NativeOnlineSession.BeforeExecution(manager,out _));Check(manager.Updates==0&&NativeLedgerState.Bootstraps==0,"Unstarted or unfinished generator permitted execution");NativeOnlineSession.AfterGeneration(source,generation,true);NativeOnlineSession.GenerationFinished(null,generation);
   });
   Test("ONLINE-071-preframe-execution-rejects-replaced-generated-map",()=>{
    using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());var source=new GameState();Generate(source);source.Map=new();var manager=new ActionManager{GameState=source};Reject(()=>NativeOnlineSession.BeforeExecution(manager,out _));Check(manager.Updates==0&&NativeLedgerState.Bootstraps==0,"Changed generated map permitted execution");
   });
   Test("ONLINE-072-preframe-execution-failure-prevents-first-frame",()=>{
    using var scope=NativeOnlineSession.BeginCreation(Guid.NewGuid());var source=new GameState();Generate(source);var manager=new ActionManager{GameState=source};NativeOnlineSession.BeforeExecution(manager,out var execution);var error=new IOException("native starting actions failed");Check(ReferenceEquals(error,NativeOnlineSession.ExecutionFinished(error,execution)),"Starting action failure lost original exception");Reject(()=>NativeRegroupSession.BeforeTransport(source,false),"faulted");Check(NativeGameStateEnvelope.Get(source) is null&&NativeLedgerState.Bootstraps==0,"Failed starting actions acquired first frame");
   });
   Test("ONLINE-073-unsupported-preview-cannot-dispatch-remote",()=>{
    typeof(NativeOnlineSession).GetProperty("Enabled",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,false);NativeOnlinePreview.Available=false;NativeOnlineSession.Install(new Harmony());
    var client=new RemoteClient();GameManager.Client=client;Check(!NativeOnlineSession.Enabled&&!NativeRegroupSession.CanDispatch(client)&&!NativeRegroupSession.BeforeDispatch(client),"Unsupported preview retained native Remote command passthrough");
   });
   Test("ONLINE-074-faulted-local-does-not-disable-published-online",()=>{
    var local=new ClientBase();GameManager.Client=local;var settings=new GameSettings();NativeRegroupSession.BeforeCreate(local,settings,local.gameId,typeof(ClientBase).GetMethod("CreateSession",new[]{typeof(GameSettings),typeof(Il2CppSystem.Guid)})!,out var token);
    var state=new GameState{Settings=settings};NativeRegroupSession.AfterLocal(123,settings,state,true);Roles(local,state);NativeRegroupSession.Opened(local);NativeRegroupSession.AfterCreate(token,0);NativeRegroupSession.CreationFinished(null,token);NativeRegroupSession.Fault("local fault");Check(NativeRegroupSession.SessionFaulted,"Fixture did not fault original Local generation");
    var(remote,online,_)=Live();Check(NativeRegroupSession.CanDispatch(remote)&&NativeRegroupSession.Ready(online)&&!NativeRegroupSession.SessionFaulted,"Stale Local fault disabled successfully published Remote client");
    GameManager.Client=local;Check(NativeRegroupSession.SessionFaulted&&!NativeRegroupSession.CanDispatch(local),"Online switch erased or bypassed original Local fault");
   });
   Console.WriteLine(JsonSerializer.Serialize(new{passed=Failures.Count==0,cases=Results.Count,assertions=Assertions,results=Results,failures=Failures,nativeGame="NOT RUN",scope="Real online and integrated local lifecycle, real immutable Snapshot and Core frame/ledger codecs; offline pinned Preview and synthetic native body/loader ports"}));return Failures.Count==0?0:1;
  }
 }
}
