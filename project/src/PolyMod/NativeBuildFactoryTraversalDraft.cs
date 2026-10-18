using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using J=Deception.PolyModAdapter.NativeBuildRecordJournalDraft;
using A=Deception.PolyModAdapter.NativeBuildLeafProbeAdapter;
using B=Deception.PolyModAdapter.NativeStateBridge;
namespace Deception.PolyModAdapter
{
    // No installation. One shared observer owner must forward exact native factory callbacks here.
    internal static class NativeBuildFactoryTraversalDraft
    {
        [ThreadStatic] static Scope? Current;
        internal static int CurrentDepth=>Current is null?0:1;
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Build factory traversal: "+why);}
        static bool Same(B.Call a,B.Call b)=>a.Size==b.Size&&a.Operation==b.Operation&&a.Version==b.Version&&a.Site==b.Site&&a.State==b.State&&a.Stream==b.Stream&&a.MethodInfo==b.MethodInfo;
        internal static Scope Open(J.Trial trial,C.Scope parent,C.Buffer input,J.Kind kind)=>new(trial,parent,input,kind);
        internal static bool MatchesPending(B.Call call,object value,object stream)=>Current?.CanPrepare(call,value,stream)==true;
        internal static bool OwnsCallback(B.Call call)=>Current?.Owns(call)==true;
        internal static int Prepare(B.Call call,object value,object stream,ref B.Plan plan)=>(Current??throw new InvalidDataException("Missing Build traversal.")).Prepare(call,value,stream,ref plan);
        internal static void Finish(B.Call call,B.Plan plan)=>(Current??throw new InvalidDataException("Missing Build traversal.")).Finish(call,plan);
        internal static void Cleanup(B.Call call,B.Plan plan,int outcome)=>(Current??throw new InvalidDataException("Missing Build traversal.")).Cleanup(call,plan,outcome);
        public static void BeforeCommand(object __0,out object? __state)=>Before(J.Kind.Command,__0,out __state);
        public static void BeforeAction(object __0,out object? __state)=>Before(J.Kind.Action,__0,out __state);
        static void Before(J.Kind kind,object discriminator,out object? state)
        {
            state=null;var scope=Current;if(scope is null)return;
            try{var frame=new Frame(scope,kind);state=frame;scope.Before(frame,Convert.ToUInt16(discriminator));}catch(Exception e){scope.Fail(e);}
        }
        public static void AfterFactory(object? __result,bool __runOriginal,object? __state)
        {if(__state is Frame frame&&!frame.Owner.Closed)try{frame.Owner.Returned(frame,__result,__runOriginal);}catch(Exception e){frame.Owner.Fail(e);}}
        public static Exception? FactoryFinished(Exception? __exception,object? __state)
        {if(__state is Frame frame&&!frame.Owner.Closed)try{frame.Owner.After(frame,__exception);}catch(Exception e){frame.Owner.Fail(e);}return __exception;}
        internal sealed class Frame
        {internal readonly Scope Owner;internal readonly J.Kind Kind;internal bool Returned,Closed;internal Frame(Scope owner,J.Kind kind){Owner=owner;Kind=kind;}}
        internal sealed class Scope:IDisposable
        {
            readonly C.Scope Parent;readonly C.Buffer Input;readonly J.Kind Kind;readonly J.FactoryReader Reader;
            readonly int Thread=Environment.CurrentManagedThreadId;
            Frame? Active;Exception? Failure;object? Value;A.Invocation? Invocation;B.Call EnteredCall;B.Plan EnteredPlan;
            bool Entered,Completed;internal bool Closed;int BeforeCount,ReturnCount,AfterCount,LeafCount;
            internal Scope(J.Trial trial,C.Scope parent,C.Buffer input,J.Kind kind)
            {
                Require(Current is null&&A.CurrentDepth==0&&NativeTrainLeafProbeAdapter.CurrentDepth==0&&NativeTrainFactoryTraversal.CurrentDepth==0,"Another private factory/leaf traversal is active.");
                Parent=parent;Input=input;Kind=kind;
                Reader=trial.BeginFactoryReader(parent,input,kind,new((uint)kind,NativeTrainLeafProbeAdapter.NewCookie(),123,C.Direction.Read));Current=this;
            }
            internal void Fail(Exception e){Interlocked.CompareExchange(ref Failure,e,null);Parent.Root.Faulted=true;}
            void Check(bool cleanup=false)
            {
                Require(!Closed&&Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(Current,this),"Stale/off-thread Build traversal.");
                Parent.Check();if(!cleanup)Require(!Completed&&Volatile.Read(ref Failure) is null&&!Parent.Root.Faulted,"Failed/completed Build traversal.");
            }
            internal void Before(Frame frame,ushort discriminator)
            {Check();Require(ReferenceEquals(frame.Owner,this)&&frame.Kind==Kind&&BeforeCount==0&&Active is null&&!Entered,"Repeated/wrong factory prefix.");Active=frame;BeforeCount++;Reader.Before(frame.Kind,discriminator);}
            internal void Returned(Frame frame,object? value,bool originalRan)
            {Check();Require(ReferenceEquals(Active,frame)&&!frame.Returned&&!frame.Closed,"Wrong/repeated factory return.");Reader.ObserveReturn(value,originalRan);Value=Reader.Ticket!.Carrier.Value;frame.Returned=true;ReturnCount++;}
            internal void After(Frame frame,Exception? error)
            {
                Check(true);Require(ReferenceEquals(Active,frame)&&!frame.Closed,"Wrong/repeated factory finalizer.");
                try{Reader.After(error);}finally{frame.Closed=true;Active=null;AfterCount++;}
            }
            internal bool CanPrepare(B.Call call,object value,object stream)
            {
                if(Closed||Completed||Thread!=Environment.CurrentManagedThreadId||!ReferenceEquals(Current,this)||Parent.Root.Faulted||
                    Volatile.Read(ref Failure) is not null||Entered||Invocation is not null||Active is not null||
                    BeforeCount!=1||ReturnCount!=1||AfterCount!=1||LeafCount!=0||!Reader.Ready||Reader.Ticket is null||Value is null)return false;
                return call.Size==40&&call.Site==(uint)Kind&&call.Operation==1&&call.Version==123&&
                    call.State==new IntPtr(Native.Pointer(value))&&call.Stream==new IntPtr(Native.Pointer(stream))&&
                    Native.Same(Value,value)&&Reader.Window.Reader.Matches(stream)&&Reader.Ticket.Carrier.Identity.Matches(value);
            }
            internal bool Owns(B.Call call)=>!Closed&&Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(Current,this)&&Entered&&Same(call,EnteredCall);
            internal int Prepare(B.Call call,object value,object stream,ref B.Plan plan)
            {
                Check();Require(CanPrepare(call,value,stream)&&plan.Cookie==0&&plan.Replacement==IntPtr.Zero,"No exact completed Build factory ticket.");
                Entered=true;EnteredCall=call;
                try{Invocation=A.OpenFactory(Parent,Reader.Ticket!,Input);return A.Prepare(call,ref plan);}
                catch(Exception e){Fail(e);throw;}finally{EnteredPlan=plan;}
            }
            void Callback(B.Call call,B.Plan plan)
            {Check(true);Require(Owns(call)&&plan.Cookie==EnteredPlan.Cookie&&plan.Replacement==EnteredPlan.Replacement,"Build factory callback stamp changed.");}
            internal void Finish(B.Call call,B.Plan plan)
            {Callback(call,plan);try{Require(Invocation is not null,"Factory adapter missing.");A.Finish(call,plan);}catch(Exception e){Fail(e);throw;}}
            internal void Cleanup(B.Call call,B.Plan plan,int outcome)
            {
                Callback(call,plan);var errors=new List<Exception>();
                try
                {
                    if(Invocation is not null)
                    {
                        try{A.Cleanup(call,plan,outcome);if(outcome==0)Invocation.RequireCompleted();}catch(Exception e){Fail(e);errors.Add(e);}
                        try{Invocation.Dispose();}catch(Exception e){Fail(e);errors.Add(e);}
                    }
                    if(outcome!=0)Fail(new InvalidDataException("Build leaf outcome "+outcome));
                    if(outcome==0&&errors.Count==0&&Volatile.Read(ref Failure) is null)LeafCount++;
                }
                finally{Invocation=null;Entered=false;}
                if(errors.Count!=0)throw new AggregateException(errors);
            }
            internal J.Carrier Complete(object result)
            {
                Check();Require(BeforeCount==1&&ReturnCount==1&&AfterCount==1&&LeafCount==1&&Active is null&&!Entered&&Invocation is null&&
                    Value is not null&&Native.Same(Value,result),"Native helper did not return the observed completed Build leaf.");
                Reader.Complete(result);Completed=true;return Reader.Ticket!.Carrier;
            }
            public void Dispose()
            {
                if(Closed)return;Check(true);var errors=new List<Exception>();if(!Completed)Fail(new InvalidDataException("Build helper incomplete."));
                try
                {
                    if(Invocation is not null)try{Invocation.Dispose();}catch(Exception e){Fail(e);errors.Add(e);}
                    Invocation=null;Entered=false;
                    if(Active is not null){Active.Closed=true;Active=null;}
                    try{Reader.Dispose();}catch(Exception e){Fail(e);errors.Add(e);}
                }
                finally{Closed=true;Current=null;Value=null;}
                if(errors.Count!=0)throw new AggregateException(errors);
            }
        }
    }
}
