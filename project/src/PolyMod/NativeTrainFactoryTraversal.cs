using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using A=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;
using B=Deception.PolyModAdapter.NativeStateBridge;

namespace Deception.PolyModAdapter
{
    // DRAFT: observer callbacks only. The lead must explicitly install them on
    // the exact native factories; no hook installation or automatic activation.
    // A missing/failed observer cannot authorize a read on an owned stream.
    internal static class NativeTrainFactoryTraversal
    {
        [ThreadStatic] static Scope? Current;
        internal static int CurrentDepth=>Current is null?0:1;
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Train factory traversal: "+why);}
        static uint Site(P.Kind kind)=>kind switch{P.Kind.Command=>3u,P.Kind.Action=>4u,_=>throw new InvalidDataException("Unknown factory kind.")};
        static bool Same(B.Call a,B.Call b)=>a.Size==b.Size&&a.Operation==b.Operation&&a.Version==b.Version&&a.Site==b.Site&&
            a.State==b.State&&a.Stream==b.Stream&&a.MethodInfo==b.MethodInfo;
        internal static Scope Open(C.Scope parent,C.Buffer input,P.Kind kind)=>new(parent,input,kind);
        internal static bool MatchesPending(B.Call call,object value,object stream)=>Current?.CanPrepare(call,value,stream)==true;
        internal static bool OwnsCallback(B.Call call)=>Current?.Owns(call)==true;
        internal static int Prepare(B.Call call,object value,object stream,ref B.Plan plan)
            =>(Current??throw new InvalidDataException("Missing factory traversal.")).Prepare(call,value,stream,ref plan);
        internal static void Finish(B.Call call,B.Plan plan)=>(Current??throw new InvalidDataException("Missing factory traversal.")).Finish(call,plan);
        internal static void Cleanup(B.Call call,B.Plan plan,int outcome)=>(Current??throw new InvalidDataException("Missing factory traversal.")).Cleanup(call,plan,outcome);

        // These are void, non-mutating Harmony observers. They never suppress
        // the original, replace its return, or throw for native control flow.
        public static void BeforeCommand(object __0,out object? __state)=>Before(P.Kind.Command,__0,out __state);
        public static void BeforeAction(object __0,out object? __state)=>Before(P.Kind.Action,__0,out __state);
        static void Before(P.Kind kind,object discriminator,out object? state)
        {
            state=null;var scope=Current;if(scope is null)return;
            try{var frame=new FactoryFrame(scope,kind);state=frame;scope.Before(frame,Convert.ToUInt16(discriminator));}
            catch(Exception e){scope.Fail(e);}
        }
        public static void AfterFactory(object? __result,bool __runOriginal,object? __state)
        {
            if(__state is not FactoryFrame frame||frame.Owner.IsClosed)return;
            try{frame.Owner.Returned(frame,__result,__runOriginal);}
            catch(Exception e){frame.Owner.Fail(e);}
        }
        public static Exception? FactoryFinished(Exception? __exception,object? __state)
        {
            if(__state is FactoryFrame frame&&!frame.Owner.IsClosed)
            {
                try{frame.Owner.FactoryClosed(frame,__exception);}
                catch(Exception e){frame.Owner.Fail(e);}
            }
            return __exception; // Preserve original native/Harmony error identity.
        }
        internal sealed class FactoryFrame
        {
            internal readonly Scope Owner;internal readonly P.Kind Kind;
            internal C.FactoryCall? Call;internal bool Returned,Closed;
            internal FactoryFrame(Scope owner,P.Kind kind){Owner=owner;Kind=kind;}
        }
        internal sealed class Scope:IDisposable
        {
            readonly C.Scope Parent;readonly C.Buffer Input;readonly P.Kind Kind;readonly object Reader;
            readonly C.FactoryReader Window;readonly int Thread=Environment.CurrentManagedThreadId;
            Exception? Failure;FactoryFrame? ActiveFactory;C.FactoryTicket? Ticket;object? Value;
            A.Invocation? Invocation;B.Call EnteredCall;B.Plan EnteredPlan;
            bool Entered,Finished,Closed;int BeforeCount,ReturnCount,CloseCount,LeafCount;
            internal bool IsClosed=>Closed;
            internal Scope(C.Scope parent,C.Buffer input,P.Kind kind)
            {
                Require(Current is null&&A.CurrentDepth==0,"Nested active helper/leaf traversal is unsupported.");
                parent.Check();_=Site(kind);
                Require(parent.Call.Direction==C.Direction.Read&&parent.Call.Version==123&&ReferenceEquals(input.Trial,parent.Trial),"Wrong read parent/buffer/version.");
                Parent=parent;Input=input;Kind=kind;Reader=input.Stream(C.Direction.Read);
                Window=parent.BeginFactoryReader(Reader,parent.Call);Current=this;
            }
            internal void Fail(Exception error)
            {Interlocked.CompareExchange(ref Failure,error,null);Parent.Root.Faulted=true;}
            void Check(bool cleanup=false)
            {
                Require(!Closed&&Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(Current,this),"Retired, foreign-thread or noncurrent traversal.");
                Parent.Check();if(!cleanup)Require(!Finished&&Volatile.Read(ref Failure) is null&&!Parent.Root.Faulted,"Failed or finished traversal.");
            }
            internal void Before(FactoryFrame frame,ushort discriminator)
            {
                Check();Require(ReferenceEquals(frame.Owner,this)&&frame.Kind==Kind&&discriminator==5,"Unexpected factory kind/discriminator.");
                Require(BeforeCount==0&&ActiveFactory is null&&Ticket is null&&!Entered&&A.CurrentDepth==0,"Repeated or nested factory invocation.");
                ActiveFactory=frame;BeforeCount++;frame.Call=Window.BeginFactory(Kind,discriminator);
            }
            internal void Returned(FactoryFrame frame,object? result,bool originalRan)
            {
                Check();Require(ReferenceEquals(ActiveFactory,frame)&&!frame.Closed&&!frame.Returned&&frame.Call is not null&&originalRan&&result is not null,"Unmatched, missing or replaced native factory return.");
                Ticket=frame.Call!.Returned(result!);Value=result;frame.Returned=true;ReturnCount++;
            }
            internal void FactoryClosed(FactoryFrame frame,Exception? originalError)
            {
                Check(true);Require(ReferenceEquals(ActiveFactory,frame)&&!frame.Closed,"Duplicate or mismatched factory finalizer.");
                if(originalError is not null)Fail(originalError);
                if(!frame.Returned)Fail(new InvalidDataException("Factory return was not observed."));
                try{frame.Call?.Close(Volatile.Read(ref Failure) is null?0:1);}
                finally{frame.Closed=true;ActiveFactory=null;CloseCount++;}
            }
            internal bool CanPrepare(B.Call call,object value,object stream)
            {
                if(Closed||Finished||Thread!=Environment.CurrentManagedThreadId||!ReferenceEquals(Current,this)||
                    Volatile.Read(ref Failure) is not null||Parent.Root.Faulted||Entered||Invocation is not null||ActiveFactory is not null||
                    BeforeCount!=1||ReturnCount!=1||CloseCount!=1||LeafCount!=0||Ticket is null||Value is null)return false;
                return call.Size==40&&call.Site==Site(Kind)&&call.Operation==1&&call.Version==123&&
                    call.State==new IntPtr(Native.Pointer(value))&&call.Stream==new IntPtr(Native.Pointer(stream))&&
                    Native.Same(Value,value)&&Native.Same(Reader,stream)&&Ticket.Identity.Matches(value);
            }
            internal bool Owns(B.Call call)=>!Closed&&Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(Current,this)&&Entered&&Same(call,EnteredCall);
            internal int Prepare(B.Call call,object value,object stream,ref B.Plan plan)
            {
                Check();Require(CanPrepare(call,value,stream)&&plan.Cookie==0&&plan.Replacement==IntPtr.Zero,"No exact completed factory ticket for this leaf.");
                Entered=true;EnteredCall=call;
                try{Invocation=A.Open(Parent,value,Kind,Input,C.Direction.Read,Ticket);return A.Prepare(call,ref plan);}
                catch(Exception e){Fail(e);throw;}
                finally{EnteredPlan=plan;}
            }
            void MatchCallback(B.Call call,B.Plan plan)
            {Check(true);Require(Owns(call)&&plan.Cookie==EnteredPlan.Cookie&&plan.Replacement==EnteredPlan.Replacement,"Factory callback call/plan changed.");}
            internal void Finish(B.Call call,B.Plan plan)
            {MatchCallback(call,plan);try{Require(Invocation is not null,"Factory leaf was not opened.");A.Finish(call,plan);}catch(Exception e){Fail(e);throw;}}
            internal void Cleanup(B.Call call,B.Plan plan,int outcome)
            {
                MatchCallback(call,plan);var errors=new List<Exception>();
                try
                {
                    if(Invocation is not null)
                    {
                        try{A.Cleanup(call,plan,outcome);if(outcome==0)Invocation.RequireCompleted();}
                        catch(Exception e){Fail(e);errors.Add(e);}
                        try{Invocation.Dispose();}catch(Exception e){Fail(e);errors.Add(e);}
                    }
                    if(outcome!=0)Fail(new InvalidDataException("Factory leaf native outcome "+outcome));
                    if(outcome==0&&errors.Count==0&&Volatile.Read(ref Failure) is null)LeafCount++;
                }
                finally{Invocation=null;Entered=false;Ticket=null;}
                if(errors.Count!=0)throw new AggregateException("Factory leaf cleanup failed.",errors);
            }
            internal void Complete(object result)
            {
                Check();Require(BeforeCount==1&&ReturnCount==1&&CloseCount==1&&LeafCount==1&&ActiveFactory is null&&!Entered&&Invocation is null&&Ticket is null&&
                    Value is not null&&Native.Same(Value,result),"Native helper did not return exactly its observed, completed Train leaf.");
                Window.Finish();Finished=true;
            }
            public void Dispose()
            {
                if(Closed)return;Check(true);var errors=new List<Exception>();
                if(!Finished)Fail(new InvalidDataException("Helper traversal did not complete."));
                try
                {
                    if(Invocation is not null)try{Invocation.Dispose();}catch(Exception e){Fail(e);errors.Add(e);}
                    Invocation=null;Entered=false;
                    if(ActiveFactory is not null)
                    {
                        try{ActiveFactory.Call?.Close(1);}catch(Exception e){Fail(e);errors.Add(e);}
                        ActiveFactory.Closed=true;ActiveFactory=null;
                    }
                    try{Window.Close(Finished&&Volatile.Read(ref Failure) is null?0:1);}catch(Exception e){Fail(e);errors.Add(e);}
                }
                finally{Closed=true;Current=null;Ticket=null;Value=null;}
                if(errors.Count!=0)throw new AggregateException("Factory traversal cleanup failed.",errors);
            }
        }
    }
}
