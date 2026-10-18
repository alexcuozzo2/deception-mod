using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Fake native surface only; actual production claim/envelope source is
    // compiled unchanged alongside this model. No actual wrapper/library load.
    public class ClaimObject
    {
        static long Next=100;
        public IntPtr Pointer {get;}
        public ClaimObject(){Pointer=new IntPtr(Interlocked.Increment(ref Next));}
        public ClaimObject(IntPtr pointer){Pointer=pointer;}
    }
    public sealed class ClaimState:ClaimObject
    {
        public int Version=>123;
        public ClaimState(){}
        public ClaimState(IntPtr pointer):base(pointer){}
    }
    public sealed class ClaimWeak
    {
        object? Value;
        public static Action? OnTarget,OnConstruct;
        public ClaimWeak(ClaimObject value,bool resurrection)
        {Value=value;var callback=OnConstruct;OnConstruct=null;callback?.Invoke();}
        public object? Target {get{var callback=OnTarget;OnTarget=null;callback?.Invoke();return Value;}}
        public bool IsAlive=>Value is not null;
        internal void Expire()=>Value=null;
    }
    internal static class Native
    {
        internal static Type T(string name)=>name switch
        {"GameState"=>typeof(ClaimState),"Il2CppSystem.Object"=>typeof(ClaimObject),"Il2CppSystem.WeakReference"=>typeof(ClaimWeak),_=>throw new MissingMemberException(name)};
        internal static MethodInfo M(string owner,string name,params string[] args)=>throw new InvalidOperationException("Native invocation forbidden in this managed model");
        internal static object? Invoke(MethodInfo method,object? target,params object?[] args)=>throw new InvalidOperationException("Native invocation forbidden");
        internal static object? Get(object value,string name)=>value.GetType().GetProperty(name)!.GetValue(value);
        internal static void Set(object value,string name,object? argument)=>throw new InvalidOperationException("Native mutation forbidden");
        internal static int Int(object? value)=>Convert.ToInt32(value);
        internal static long Pointer(object value)=>((ClaimObject)value).Pointer.ToInt64();
        internal static bool Same(object? a,object? b)=>a is not null&&b is not null&&Pointer(a)==Pointer(b);
    }
    internal static class NativeLedgerOwnership
    {
        internal sealed class Context
        {internal Guid Epoch;internal bool Faulted,Retired;internal int Thread=Environment.CurrentManagedThreadId;}
        internal static Context Current=null!;
        internal static readonly HashSet<long> Published=new(),Adopted=new();
        internal static Context PrivateContext(object state)
        {
            if(Current.Retired||Current.Faulted||Current.Thread!=Environment.CurrentManagedThreadId||Published.Contains(Native.Pointer(state))||Adopted.Contains(Native.Pointer(state)))throw new InvalidDataException("Mock native ownership rejected");
            return Current;
        }
        internal static void RequirePrivateContext(Context expected,object state)
        {if(!ReferenceEquals(PrivateContext(state),expected))throw new InvalidDataException("Mock context replaced");}
        internal static void BeforeRead(object state)=>NativeGameStateEnvelope.RejectPrivateAccess(state);
        internal static void BeforeWrite(object state)=>NativeGameStateEnvelope.RejectPrivateAccess(state);
        internal static void BeforeFrame(long revision,byte[] payload){}
        internal static void BeforeLegacyRead(){}
    }
    internal static class NativeFreshSession
    {
        internal static bool Enabled=>false;
        internal static int TransportCalls;
        internal static void BeforeTransport(object state,bool reading){TransportCalls++;}
        internal static void BeforeFrame(object state,long revision,byte[] payload){}
        internal static void BeforePublish(object state,bool reading,long revision,byte[] payload){}
        internal static void FrameCompleted(object state,bool reading){}
    }
    // This suite exercises private diagnostic claims only. Production Local
    // lifecycle is explicitly inactive and never lends an enrollment token.
    internal static class NativeRegroupSession
    {
        internal static bool Enabled=>false;
        internal sealed class Transport {}
        internal static Transport? BeforeTransport(object state,bool reading)=>null;
        static void Inactive(Transport? token){if(token is not null)throw new InvalidOperationException("Production lifecycle token in private fixture.");}
        internal static void BeforeLegacyRead(object state,Transport? token)=>Inactive(token);
        internal static void BeforeFrame(object state,bool reading,Transport? token,long revision,byte[] payload)=>Inactive(token);
        internal static void Completed(object state,bool reading,bool framed,Transport? token)=>Inactive(token);
        internal static void Failed(Transport? token,string stage)=>Inactive(token);
    }
    internal static class NativeRegistryOrder {internal static void RequireCompatible(){}}
    internal static class NativeStateBridge
    {
        internal struct Call {internal uint Size,Site;internal int Operation,Version;internal IntPtr State,Stream,MethodInfo;}
        internal struct Plan {internal ulong Cookie;internal IntPtr Replacement;}
        internal static void Install(bool commands)=>throw new InvalidOperationException("Install forbidden");
    }
    internal static class NativeStateProbe
    {
        internal sealed class Buffer:IDisposable
        {
            internal static int Allocations;
            internal object W=>throw new InvalidOperationException();internal object R=>throw new InvalidOperationException();
            internal long Position=>0;internal long Length=>0;
            internal Buffer(){Allocations++;throw new InvalidOperationException("Buffer allocation forbidden in guarded tests");}
            internal Buffer(byte[] value):this(){}
            internal byte[] Snapshot()=>throw new InvalidOperationException();public void Dispose(){}
        }
        internal static object NativeBytes(byte[] bytes)=>throw new InvalidOperationException();
        internal static byte[] ManagedBytes(object bytes)=>throw new InvalidOperationException();
    }
    internal static partial class NativeGameStateEnvelope
    {
        internal static void MockReset(Guid epoch)
        {
            States.Clear();Operations.Clear();PrivateClaims.Clear();NextOperation=0;Installed=true;
            ValidatePayload=p=>{NativeIdentityLedgerCodec.Decode(p,0);return true;};ValidateState=null;
            ClaimWeak.OnTarget=null;ClaimWeak.OnConstruct=null;NativeStateProbe.Buffer.Allocations=0;NativeFreshSession.TransportCalls=0;
            NativeLedgerOwnership.Current=new(){Epoch=epoch};NativeLedgerOwnership.Published.Clear();NativeLedgerOwnership.Adopted.Clear();
        }
        internal static void MockValidator(Action<object,long,byte[]>? validator)=>ValidateState=validator;
        internal static void MockPayloadValidator(Func<byte[],bool> validator)=>ValidatePayload=validator;
        internal static void MockExpire(object state)=>((ClaimWeak)PrivateClaims[Native.Pointer(state)].WeakState).Expire();
        internal static void MockReplaceBinding(object state,long revision,byte[] payload)
        {var old=States[Native.Pointer(state)];States[Native.Pointer(state)]=new(old.WeakState,new(revision,payload));}
        internal static void MockOperation(object state)
        {var call=new NativeStateBridge.Call{State=new IntPtr(Native.Pointer(state))};Operations[1]=new(call,state,new object());}
    }
}

namespace Deception.PrivateClaimMocks
{
    using Deception.PolyModAdapter;
    using Envelope=Deception.PolyModAdapter.NativeGameStateEnvelope;
    public static class Harness
    {
        static readonly Guid Epoch=new("c0111d03-e29c-4313-ae44-cd1c3526c225"),Other=new("a1111d03-e29c-4313-ae44-cd1c3526c225");
        static int Assertions,Failed;static readonly List<object> Results=new();
        static byte[] Bytes(Guid? epoch=null,ulong next=1)=>NativeIdentityLedgerCodec.Encode(new(epoch??Epoch,next,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>()),0);
        static void Check(bool value,string reason){Assertions++;if(!value)throw new InvalidOperationException(reason);}
        static void Reject(Action action,string reason)
        {bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,reason);}
        static void Case(string name,Action body)
        {Envelope.MockReset(Epoch);try{body();Results.Add(new{id=name,status="PASS"});}catch(Exception error){Failed++;Results.Add(new{id=name,status="FAIL",error=error.ToString()});}}
        static ClaimState Bound(byte[]? payload=null){var state=new ClaimState();Envelope.Set(state,0,payload??Bytes());return state;}
        static void PublicRejects(ClaimState state)
        {
            Reject(()=>Envelope.Get(state),"Public Get");Reject(()=>Envelope.Set(state,0,Bytes()),"Public Set");Reject(()=>Envelope.Forget(state),"Public Forget");
            Reject(()=>Envelope.NativeBytes(state,123),"NativeBytes before buffer");Check(NativeStateProbe.Buffer.Allocations==0,"No private buffer");
            foreach(int operation in new[]{0,1})
            {
                var call=new NativeStateBridge.Call{Size=40,Site=0,Operation=operation,Version=123,State=state.Pointer,Stream=new IntPtr(55)};var plan=new NativeStateBridge.Plan();
                Reject(()=>Envelope.Prepare(call,ref plan),"Native Prepare blocked");Check(plan.Cookie==0&&Envelope.AllocatedOperations==0&&NativeFreshSession.TransportCalls==0,"Before cookie/stream/session callback");
            }
        }
        public static int Run()
        {
            Case("stage-publish-exactly-once-private-only",()=>
            {
                var s=Bound();var c=Envelope.ClaimPrivate(s);PublicRejects(s);var initial=c.Read(s);var input=Bytes(next:2);var staged=c.Stage(s,1,input);
                input[0]^=1;Check(c.Read(s).Bytes().SequenceEqual(initial.Bytes()),"Stage does not publish");input[0]^=1;c.Publish(s,staged,1,input);
                Check(c.Read(s).Revision==1&&c.Read(s).Bytes().SequenceEqual(input),"Exact staged publication");PublicRejects(s);
                Reject(()=>c.Publish(s,staged,1,input),"Second publish");Reject(()=>c.Stage(s,2,input),"Second stage after commit");c.Retire(s);Check(c.RetiredWithoutBinding,"Retired binding removed");PublicRejects(s);
            });
            Case("wrong-admission-epoch",()=>{var s=Bound(Bytes(Other));Reject(()=>Envelope.ClaimPrivate(s),"Wrong initial epoch");Check(Envelope.Get(s) is not null,"Rejected admission preserves binding");});
            Case("wrong-candidate-epoch-and-revision",()=>{var s=Bound();var c=Envelope.ClaimPrivate(s);Reject(()=>c.Stage(s,1,Bytes(Other)),"Wrong stage epoch");Reject(()=>c.Stage(s,2,Bytes()),"Revision jump");Check(c.Read(s).Revision==0,"Unchanged");});
            Case("wrong-staged-owner-payload-and-base",()=>
            {var a=Bound();var b=Bound();var x=Envelope.ClaimPrivate(a);var y=Envelope.ClaimPrivate(b);var p=x.Stage(a,1,Bytes());Reject(()=>y.Publish(b,p,1,Bytes()),"Other claim staged token");Reject(()=>x.Publish(a,p,1,Bytes(next:2)),"Changed staged payload");Envelope.MockReplaceBinding(a,0,Bytes());Reject(()=>x.Publish(a,p,1,Bytes()),"Equal-byte replacement is not same base");});
            Case("reentrant-set-cannot-replace-claim",()=>
            {var s=Bound();Envelope.PrivateClaim? claim=null;bool first=true;Envelope.MockValidator((_,_,_)=>{if(first){first=false;claim=Envelope.ClaimPrivate(s);}});Reject(()=>Envelope.Set(s,0,Bytes(next:2)),"Set rechecks after validator");Check(claim is not null&&claim.Read(s).Bytes().SequenceEqual(Bytes()),"Claim retains original binding");});
            Case("reentrant-get-cannot-read-newly-claimed-binding",()=>
            {var s=Bound();Envelope.PrivateClaim? claim=null;ClaimWeak.OnTarget=()=>claim=Envelope.ClaimPrivate(s);Reject(()=>Envelope.Get(s),"Get rechecks after weak read");Check(claim is not null&&claim.Read(s).Revision==0,"Claim intact");});
            Case("weak-construction-reentry-cannot-replace-claim",()=>
            {var s=Bound();Envelope.PrivateClaim? claim=null;ClaimWeak.OnConstruct=()=>claim=Envelope.ClaimPrivate(s);Reject(()=>Envelope.Set(s,0,Bytes(next:2)),"Set rechecks after weak construction");Check(claim is not null&&claim.Read(s).Bytes().SequenceEqual(Bytes()),"Original survives");});
            Case("quarantine-preserves-retirement-and-tombstone",()=>
            {var s=Bound();var c=Envelope.ClaimPrivate(s);var p=c.Stage(s,1,Bytes());c.Quarantine();Reject(()=>c.Read(s),"Faulted read");Reject(()=>c.Publish(s,p,1,Bytes()),"Faulted publish");c.Retire(s);PublicRejects(s);Envelope.BeforePrivateContextChange();Check(c.RetiredWithoutBinding,"Fault cleanup permitted");});
            Case("context-fault-and-retire-do-not-release-receiver",()=>
            {var s=Bound();var c=Envelope.ClaimPrivate(s);NativeLedgerOwnership.Current.Faulted=true;Reject(()=>c.Read(s),"Context fault");c.Retire(s);NativeLedgerOwnership.Current.Retired=true;PublicRejects(s);});
            Case("context-change-barrier-and-replaced-context",()=>
            {var s=Bound();var c=Envelope.ClaimPrivate(s);Reject(Envelope.BeforePrivateContextChange,"Active claim pins generation");NativeLedgerOwnership.Current=new(){Epoch=Epoch};Reject(()=>c.Read(s),"Equal-epoch different generation");c.Retire(s);Envelope.BeforePrivateContextChange();});
            Case("wrong-thread-and-wrong-state",()=>
            {var s=Bound();var c=Envelope.ClaimPrivate(s);Reject(()=>c.Read(new ClaimState()),"Wrong state");Exception? error=null;var thread=new Thread(()=>{try{c.Read(s);}catch(Exception e){error=e;}});thread.Start();thread.Join();Check(error is InvalidDataException,"Wrong thread");c.Retire(s);});
            Case("published-or-adopted-receivers-rejected",()=>
            {var s=Bound();NativeLedgerOwnership.Published.Add(Native.Pointer(s));Reject(()=>Envelope.ClaimPrivate(s),"Published");NativeLedgerOwnership.Published.Clear();NativeLedgerOwnership.Adopted.Add(Native.Pointer(s));Reject(()=>Envelope.ClaimPrivate(s),"Previously adopted");});
            Case("active-native-transport-rejected",()=>{var s=Bound();Envelope.MockOperation(s);Reject(()=>Envelope.ClaimPrivate(s),"Existing native operation");});
            Case("candidate-validator-fault-cannot-publish",()=>
            {var s=Bound();var c=Envelope.ClaimPrivate(s);var p=c.Stage(s,1,Bytes());Envelope.MockValidator((_,_,_)=>c.Quarantine());Reject(()=>c.Publish(s,p,1,Bytes()),"Recheck after state validation");c.Retire(s);Check(c.RetiredWithoutBinding,"Cleanup after validator fault");});
            Case("staged-bytes-not-caller-mutation-validated",()=>
            {
                var s=Bound();var c=Envelope.ClaimPrivate(s);var input=Bytes(next:2);var staged=c.Stage(s,1,input);bool first=true;
                Envelope.MockPayloadValidator(bytes=>{if(first){first=false;Bytes(next:3).CopyTo(input,0);}return true;});
                Envelope.MockValidator((_,revision,bytes)=>{if(NativeIdentityLedgerCodec.Decode(bytes,revision).NextLogicalId!=2)throw new InvalidDataException("Validated different bytes");});
                c.Publish(s,staged,1,input);Check(NativeIdentityLedgerCodec.Decode(c.Read(s).Bytes(),1).NextLogicalId==2,"Published exactly the immutable validated candidate");
            });
            Case("expired-weak-tombstone-allows-new-identity",()=>
            {var s=Bound();var c=Envelope.ClaimPrivate(s);c.Retire(s);Envelope.MockExpire(s);var replacement=new ClaimState(s.Pointer);Envelope.Set(replacement,0,Bytes());Check(Envelope.Get(replacement) is not null,"Dead prior target does not own reused pointer");});
            var result=new{scope="Actual claim/envelope source with managed native/ownership model only; no native weak handles, transport, game or adoption calls",tests=Results.Count,assertions=Assertions,failed=Failed,results=Results};
            File.WriteAllText(Environment.GetEnvironmentVariable("DCP_PRIVATE_CLAIM_RESULT")??throw new InvalidOperationException("Missing result path"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));return Failed==0?0:1;
        }
    }
}
