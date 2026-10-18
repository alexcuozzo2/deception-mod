using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
    public static class MenuPreviewTests
    {
        static int Assertions;static readonly List<string> Results=new();
        static void Check(bool ok,string why){Assertions++;if(!ok)throw new Exception(why);}
        static void Test(string name,Action action){try{NativeMenuPreviewScope.Finished(null,0);action();Check(!NativeMenuPreviewScope.Unbound(new GameState(),false),"Preview scope leaked after case");Results.Add(name);}catch(Exception e){throw new Exception(name,e);}finally{NativeMenuPreviewScope.Finished(null,0);}}
        static void Reject(Action action,string contains)
        {try{action();}catch(InvalidDataException e){Check(e.Message.Contains(contains,StringComparison.Ordinal),"Wrong denial reason: "+e.Message);return;}throw new Exception("Required denial missing: "+contains);}
        static (GameState State,ClientBase Client,NativeRegroupSession.Session Session) Start(bool opening=false)
        {
            var state=new GameState();var client=new ClientBase();var session=NativeRegroupSession.FixtureStart(client,client.gameId.Value,opening);
            return(state,client,session);
        }
        static void Scope(Action action)
        {NativeMenuPreviewScope.Before(out int old);try{action();}finally{NativeMenuPreviewScope.Finished(null,old);}}
        public static int Run()
        {
            try
            {
                Test("PREVIEW-001-exact-no-argument-producer-hook-and-finalizer",()=>{
                    var h=new HarmonyLib.Harmony();NativeMenuPreviewScope.Install(h);
                    Check(h.Calls==1&&h.Target!.DeclaringType==typeof(SelectTribePopup)&&h.Target.Name=="SetBackground"&&h.Target.GetParameters().Length==0,"Preview scope installed a broad or different producer hook");
                    Check(h.Prefix?.Method.Name=="Before"&&h.Postfix is null&&h.Finalizer?.Method.Name=="Finished","Scope is not balanced by an exception finalizer");
                });
                Test("PREVIEW-002-outside-scope-does-not-inspect-envelope",()=>{
                    var(s,_,_)=Start();int before=NativeGameStateEnvelope.Reads;Check(!NativeMenuPreviewScope.Unbound(s,false)&&!NativeMenuPreviewScope.Unbound(s,true),"Outside scope gained preview authority");Check(NativeGameStateEnvelope.Reads==before,"Outside scope performed unnecessary binding query");
                });
                Test("PREVIEW-003-unbound-template-scope-is-balanced",()=>{
                    var(s,_,_)=Start();NativeMenuPreviewScope.Before(out int old);Check(old==0&&NativeMenuPreviewScope.Unbound(s,false),"Scoped packaged preview rejected");Check(NativeMenuPreviewScope.Finished(null,old) is null,"Success finalizer manufactured exception");
                });
                Test("PREVIEW-004-nested-scope-restores-exact-prior-depth",()=>{
                    var(s,_,_)=Start();NativeMenuPreviewScope.Before(out int outer);NativeMenuPreviewScope.Before(out int inner);Check(outer==0&&inner==1,"Nested scope lost saved state");NativeMenuPreviewScope.Finished(null,inner);Check(NativeMenuPreviewScope.Unbound(s,false),"Inner finalizer revoked outer producer");NativeMenuPreviewScope.Finished(null,outer);
                });
                Test("PREVIEW-005-error-finalizer-preserves-original-exception",()=>{
                    var(s,_,_)=Start();NativeMenuPreviewScope.Before(out int old);var expected=new IOException("native snapshot failed");Check(ReferenceEquals(expected,NativeMenuPreviewScope.Finished(expected,old)),"Finalizer changed native failure");
                });
                Test("PREVIEW-006-inner-error-does-not-leak-or-revoke-outer",()=>{
                    var(s,_,_)=Start();Scope(()=>{NativeMenuPreviewScope.Before(out int old);var expected=new Exception("inner");Check(ReferenceEquals(expected,NativeMenuPreviewScope.Finished(expected,old))&&NativeMenuPreviewScope.Unbound(s,false),"Inner exception corrupted nesting");});
                });
                Test("PREVIEW-007-scope-does-not-cross-thread",()=>{
                    var(s,_,_)=Start();Scope(()=>{bool result=true;var thread=new Thread(()=>result=NativeMenuPreviewScope.Unbound(s,false));thread.Start();thread.Join();Check(!result&&NativeMenuPreviewScope.Unbound(s,false),"Thread-static preview authority leaked");});
                });
                foreach(bool reading in new[]{true,false})Test("PREVIEW-008-unbound-template-transport-"+(reading?"read":"write"),()=>{
                    var(s,_,session)=Start();int writes=NativeGameStateEnvelope.Writes;Scope(()=>{var token=NativeRegroupSession.BeforeTransport(s,reading);if(reading)NativeRegroupSession.BeforeLegacyRead(s,token);Check(token is null,"Preview gained gameplay transport token");NativeRegroupSession.Completed(s,reading,false,token);NativeRegroupSession.Failed(token,"synthetic cleanup");});
                    Check(!session.Faulted&&NativeGameStateEnvelope.Get(s) is null&&NativeGameStateEnvelope.Writes==writes&&NativeLedgerState.Bootstraps==0,"Packaged map enrolled or faulted active game");
                });
                Test("PREVIEW-009-active-match-legacy-denial-retained-outside-producer",()=>{
                    var(s,_,_)=Start();var token=NativeRegroupSession.BeforeTransport(s,true);Check(token is not null,"Outside producer lost exact active lifecycle token");Reject(()=>NativeRegroupSession.BeforeLegacyRead(s,token),"already-open framed match");
                });
                Test("PREVIEW-010-initial-opening-legacy-window-retained",()=>{
                    var(s,_,_)=Start(true);var token=NativeRegroupSession.BeforeTransport(s,true);NativeRegroupSession.BeforeLegacyRead(s,token);Check(token is not null&&token.LegacyAllowed,"Initial legitimate migration window changed");
                });
                foreach(bool reading in new[]{false,true})Test("PREVIEW-011-enrolled-match-rejected-inside-producer-"+reading,()=>{
                    var(s,c,session)=Start();NativeRegroupSession.FixtureEnroll(s,c.gameId.Value);var original=NativeGameStateEnvelope.Get(s);int writes=NativeGameStateEnvelope.Writes;
                    Scope(()=>Reject(()=>NativeRegroupSession.BeforeTransport(s,reading),"enrolled or framed match state"));Check(ReferenceEquals(original,NativeGameStateEnvelope.Get(s))&&NativeGameStateEnvelope.Writes==writes&&!session.Faulted,"Scope changed protected enrolled state");
                });
                foreach(bool reading in new[]{false,true})Test("PREVIEW-012-framed-unenrolled-rejected-inside-producer-"+reading,()=>{
                    var(s,c,_)=Start();NativeGameStateEnvelope.Set(s,0,NativeIdentityLedgerCodec.Encode(new(c.gameId.Value,1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>()),0));
                    Scope(()=>Reject(()=>NativeRegroupSession.BeforeTransport(s,reading),"enrolled or framed match state"));
                });
                Test("PREVIEW-013-enrolled-with-missing-frame-is-still-rejected",()=>{
                    var(s,c,_)=Start();NativeRegroupSession.FixtureEnroll(s,c.gameId.Value);NativeGameStateEnvelope.Frames.Remove(s);Scope(()=>Reject(()=>NativeRegroupSession.BeforeTransport(s,true),"enrolled or framed match state"));
                });
                Test("PREVIEW-014-framed-input-with-preview-null-token-rejected",()=>{
                    var(s,c,_)=Start();byte[] payload=NativeIdentityLedgerCodec.Encode(new(c.gameId.Value,1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>()),0);
                    Scope(()=>{var token=NativeRegroupSession.BeforeTransport(s,true);Reject(()=>NativeRegroupSession.BeforeFrame(s,true,token,0,payload),"exact Local lifecycle boundary");});Check(NativeGameStateEnvelope.Get(s) is null,"Framed preview was adopted before frame authorization");
                });
                Test("PREVIEW-015-gameplay-frame-remains-exact-token-and-epoch-bound",()=>{
                    var(s,c,_)=Start();var token=NativeRegroupSession.BeforeTransport(s,true);byte[] payload=NativeIdentityLedgerCodec.Encode(new(c.gameId.Value,1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>()),0);
                    NativeRegroupSession.BeforeFrame(s,true,token,0,payload);Reject(()=>NativeRegroupSession.BeforeFrame(new GameState(),true,token,0,payload),"changed session/state/direction");Reject(()=>NativeRegroupSession.BeforeFrame(s,false,token,0,payload),"changed session/state/direction");
                    byte[] foreign=NativeIdentityLedgerCodec.Encode(new(Guid.NewGuid(),1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>()),0);Reject(()=>NativeRegroupSession.BeforeFrame(s,true,token,0,foreign),"different match Guid");
                });
                Test("PREVIEW-016-unbound-preview-does-not-validate-active-fake-client",()=>{
                    var(s,_,session)=Start();GameManager.instance!.fakeSession=new();Scope(()=>Check(NativeRegroupSession.BeforeTransport(s,true) is null,"UI producer borrowed active game validation"));Check(!session.Faulted,"Read-only preview faulted gameplay epoch");Reject(()=>NativeRegroupSession.BeforeTransport(s,true),"fake session");
                });
                Test("PREVIEW-017-unbound-preview-does-not-repair-or-borrow-faulted-session",()=>{
                    var(s,_,session)=Start();session.Faulted=true;Scope(()=>Check(NativeRegroupSession.BeforeTransport(s,true) is null,"Faulted gameplay prevented unrelated packaged preview"));Check(session.Faulted,"Preview silently repaired existing gameplay fault");Reject(()=>NativeRegroupSession.BeforeTransport(s,true),"retired, or faulted");
                });
                Test("PREVIEW-018-paid-action-reentry-remains-forbidden",()=>{
                    var(s,_,_)=Start();NativeRegroupSession.FixtureBuying(true);try{Scope(()=>Reject(()=>NativeRegroupSession.BeforeTransport(s,true),"unfinished paid action"));}finally{NativeRegroupSession.FixtureBuying(false);}
                });
                Test("PREVIEW-019-exception-denial-finally-revokes-scope",()=>{
                    var(s,c,_)=Start();NativeRegroupSession.FixtureEnroll(s,c.gameId.Value);try{Scope(()=>NativeRegroupSession.BeforeTransport(s,true));throw new Exception("Expected rejection");}catch(InvalidDataException){ }Check(!NativeMenuPreviewScope.Unbound(new GameState(),false),"Rejected preview left future reads authorized");
                });
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Results.Count,assertions=Assertions,results=Results,nativeGame="NOT RUN",scope="Actual preview scope + production transport/frame lifecycle; synthetic graph/envelope/native boundaries"}));return 0;
            }
            catch(Exception e){Console.WriteLine(e);return 1;}
        }
    }
}
