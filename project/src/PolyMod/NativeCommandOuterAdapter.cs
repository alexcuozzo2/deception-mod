using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using B=Deception.PolyModAdapter.NativeCommandOuterAbi;
using R=Deception.PolyModAdapter.NativeTrainCommandRecord;
using F=Deception.Core.NativeTrainCommandFrame;
using W=Deception.PolyModAdapter.NativeLedgerOwnership.WeakIdentity;
namespace Deception.PolyModAdapter
{
    // Explicit private invocation only. This is not a production format router.
    // An active caller-owned Invocation roots input/result wrappers THROUGH
    // native return. Cleanup closes the journal but never releases those roots.
    internal static class NativeCommandOuterAdapter
    {
        [ThreadStatic] static Invocation? Current;
        sealed record Mark(W Identity,int Thread);
        static readonly object Gate=new();static readonly Dictionary<long,Mark> Known=new();
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Private outer command: "+why);}
        static bool Empty(B.Plan p)=>p.Cookie==0&&p.Handled.Result==IntPtr.Zero&&p.Handled.Version==0&&p.Handled.Boolean==0;
        static bool Equal(B.Value a,B.Value b)=>a.Result==b.Result&&a.Version==b.Version&&a.Boolean==b.Boolean;
        static bool Equal(B.Call a,B.Call b)=>a.Size==b.Size&&a.Operation==b.Operation&&a.Version==b.Version&&a.Reserved==b.Reserved&&
            a.Input==b.Input&&a.CommandOut==b.CommandOut&&a.VersionOut==b.VersionOut&&a.MethodInfo==b.MethodInfo;
        static void Remember(object value)
        {
            object native=Native.As(value,"Il2CppSystem.Object");
            Require(Native.Same(native,value)&&Native.Pointer(native)!=0,"Input normalization changed identity.");
            lock(Gate)
            {
                int thread=Environment.CurrentManagedThreadId;
                foreach(var deadKey in Known.Where(x=>x.Value.Thread==thread&&x.Value.Identity.Target() is null).Select(x=>x.Key).ToArray())Known.Remove(deadKey);
                long key=Native.Pointer(native);
                if(Known.TryGetValue(key,out var old)){Require(old.Thread==thread&&old.Identity.Matches(native),"Known input identity/thread collision.");return;}
                Require(Known.Count<2048,"Known private input capacity reached.");Known.Add(key,new(new(native),thread));
            }
        }
        static void Unscoped(B.Call call)
        {
            if(call.Operation==0)C.RejectUnscopedCarrierPointer(call.Input.ToInt64());
            lock(Gate)if(Known.TryGetValue(call.Input.ToInt64(),out var mark))
            {
                Require(mark.Thread==Environment.CurrentManagedThreadId,"Known private input crossed threads.");
                Require(mark.Identity.Target() is null,"Known private input has no current exact outer invocation.");
            }
        }
        internal static int ActiveCount=>Current is null?0:1;
        internal static Invocation Write(C.Trial trial,C.Carrier carrier,Guid epoch)=>new(trial,carrier,null,epoch);
        internal static Invocation Read(C.Trial trial,byte[] bytes,Guid epoch)=>new(trial,null,bytes,epoch);
        internal static int Prepare(B.Call call,ref B.Plan plan)
        {
            Require(Empty(plan),"Native plan is not empty.");
            if(Current is null){Unscoped(call);return 0;}
            return Current.Prepare(call,ref plan);
        }
        internal static void Finish(B.Call call,B.Plan plan,B.Value result,int ran)
        {
            if(plan.Cookie==0){Require(Empty(plan)&&ran==1,"Unscoped plan changed.");Unscoped(call);return;}
            (Current??throw new InvalidDataException("Outer invocation expired before Finish.")).Finish(call,plan,result,ran);
        }
        internal static void Cleanup(B.Call call,B.Plan plan,int outcome)
        {
            // A rejected Prepare that never acquired an invocation has no journal.
            if(plan.Cookie==0){Require(Empty(plan),"Empty cleanup plan changed.");return;}
            (Current??throw new InvalidDataException("Outer invocation expired before Cleanup.")).Cleanup(call,plan,outcome);
        }
        internal sealed class Invocation:IDisposable
        {
            readonly C.Trial Trial;readonly C.Carrier? Carrier;readonly Guid Epoch;readonly byte[]? InputBytes;
            readonly C.Scope Root;readonly ulong Cookie;readonly uint Operation;readonly int Thread=Environment.CurrentManagedThreadId;
            internal readonly object Input;
            object? ResultRoot;byte[]? OutputBytes;C.Carrier? ResultCarrier;NativePaidBirthIntent? ResultIntent;
            B.Call EnteredCall;B.Value ResultValue;bool Entered,Finished,Cleaned,Closed,Acknowledged;
            internal Invocation(C.Trial trial,C.Carrier? carrier,byte[]? bytes,Guid epoch)
            {
                Require(Current is null&&epoch!=Guid.Empty,"Nested invocation or empty epoch.");trial.Check();
                Trial=trial;Carrier=carrier;Epoch=epoch;Operation=carrier is null?1u:0u;Cookie=NativeTrainLeafProbeAdapter.NewCookie();
                if(carrier is not null){Require(ReferenceEquals(carrier.Trial,trial)&&carrier.Kind==P.Kind.Command,"Foreign/non-command carrier.");_=trial.RequireCarrier(carrier.Value,carrier.Kind);Input=Native.As(carrier.Value,"CommandBase");}
                else {Require(bytes is not null&&bytes.Length<=F.MaxFrameBytes,"Missing/oversized input.");InputBytes=(byte[])bytes!.Clone();Input=NativeStateProbe.NativeBytes(InputBytes);}
                Remember(Input);
                var direction=Operation==0?C.Direction.Write:C.Direction.Read;
                var buffer=trial.NewBuffer(Operation==0?null:Array.Empty<byte>());
                Root=trial.Begin(buffer,new(0,Cookie,F.Version,direction),P.Wire.DcpbV1);
                Current=this;
            }
            void Check(){Require(!Closed&&Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(Current,this),"Stale/off-thread invocation.");Trial.Check();}
            void Match(B.Call call)
            {Require(call.Size==48&&call.Operation==Operation&&call.Reserved==0&&call.Input==new IntPtr(Native.Pointer(Input))&&
                call.Version==(Operation==0?F.Version:0)&&
                (Operation==0?call.CommandOut==IntPtr.Zero&&call.VersionOut==IntPtr.Zero:call.CommandOut!=IntPtr.Zero&&call.VersionOut!=IntPtr.Zero),"Outer native call differs from its armed input/operation/version.");}
            internal int Prepare(B.Call call,ref B.Plan plan)
            {
                Check();Root.Check();Match(call);Require(!Entered,"Repeated outer Prepare.");
                Entered=true;EnteredCall=call;plan.Cookie=Cookie;
                if(Operation==0)
                {
                    OutputBytes=R.Write(Trial,Carrier!,Epoch,Root);ResultRoot=NativeStateProbe.NativeBytes(OutputBytes);
                    Remember(ResultRoot);ResultValue=new(){Result=new IntPtr(Native.Pointer(ResultRoot))};
                }
                else
                {
                    Require(NativeStateProbe.ManagedBytes(Input).SequenceEqual(InputBytes!),"Private input changed after arming.");
                    ResultCarrier=R.Read(Trial,InputBytes!,Epoch,Root);ResultRoot=ResultCarrier.Value;
                    ResultIntent=P.Get(ResultRoot,P.Kind.Command);Remember(ResultRoot);
                    ResultValue=new(){Result=new IntPtr(Native.Pointer(ResultRoot)),Version=F.Version,Boolean=1};
                }
                plan.Handled=ResultValue;return 1;
            }
            void Prepared(B.Call call,B.Plan plan)
            {Check();Require(Entered&&!Cleaned&&Equal(call,EnteredCall)&&plan.Cookie==Cookie,"Outer callback lost exact call/cookie.");}
            internal void Finish(B.Call call,B.Plan plan,B.Value result,int ran)
            {
                Prepared(call,plan);Root.Check();Require(!Finished&&ran==0&&Equal(plan.Handled,ResultValue)&&Equal(result,ResultValue)&&ResultRoot is not null,"Outer result changed.");
                Require(new IntPtr(Native.Pointer(ResultRoot!))==result.Result,"Result native identity changed.");Root.Finish();Finished=true;
            }
            internal void Cleanup(B.Call call,B.Plan plan,int outcome)
            {
                Prepared(call,plan);Cleaned=true;
                Root.Close(outcome==0&&Finished?0:1);
                // ResultRoot deliberately stays rooted until caller return/ack.
            }
            internal object RequireReturned(object returned,int version=0,bool success=true)
            {
                Check();Require(Entered&&Finished&&Cleaned&&Root.Root.Closed&&!Root.Root.Faulted&&!Acknowledged&&success&&
                    version==(Operation==0?0:F.Version)&&Native.Same(returned,ResultRoot),"Native return was not the exact completed outer result: "+ReturnEvidence(returned,version,success));
                if(Operation==0)Require(NativeStateProbe.ManagedBytes(returned).SequenceEqual(OutputBytes!),"Returned byte array changed.");
                else Require(P.Get(returned,P.Kind.Command)==ResultIntent,"Returned intent changed.");
                Acknowledged=true;return returned;
            }
            // Observation only; caller keeps this invocation and both native
            // wrappers rooted. Captures pre/post-GC return evidence without
            // admitting a carrier, changing a flag or weakening acknowledgement.
            internal string ReturnEvidence(object? returned,int version=0,bool success=true)
            {
                Check();return System.Text.Json.JsonSerializer.Serialize(new{
                    operation=Operation,version,expectedVersion=Operation==0?0:F.Version,success,
                    entered=Entered,finished=Finished,cleaned=Cleaned,rootClosed=Root.Root.Closed,rootFaulted=Root.Root.Faulted,acknowledged=Acknowledged,
                    returned=returned is null?0:Native.Pointer(returned),retained=ResultRoot is null?0:Native.Pointer(ResultRoot),
                    planned=ResultValue.Result.ToInt64(),plannedVersion=ResultValue.Version,plannedBoolean=ResultValue.Boolean});
            }
            public void Dispose()
            {
                if(Closed)return;Check();
                try
                {
                    if(!Cleaned){Cleaned=true;Root.Close(1);}
                    // A missing caller acknowledgement does not publish a result;
                    // private carrier ownership still belongs to Trial until its
                    // disposal. Its already-completed root is not replayed.
                }
                finally{Closed=true;Current=null;GC.KeepAlive(ResultRoot);GC.KeepAlive(Input);}
            }
        }
    }
}
