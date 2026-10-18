using System;
using System.IO;
using System.Linq;
using System.Threading;
using Deception.Core;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;

namespace Deception.PolyModAdapter
{
    // DRAFT ONLY. No install, trigger, fallback, state access or execution hook.
    // The lead's future ABI3 dispatcher may route ONLY exact admitted calls to
    // these methods. All methods may throw; the rooted bridge callback must
    // contain managed errors and return POD rejection before native raises.
    internal static class NativeTrainLeafProbeAdapter
    {
        const int MaxRecordBytes=4096;
        static long Next;
        [ThreadStatic] static Invocation? Current;
        internal static int CurrentDepth=>Current is null?0:1;
        internal static bool MatchesInvocation(NativeStateBridge.Call call)=>Current?.MatchesForDispatch(call)==true;
        internal static ulong NewCookie()
        {long value=Interlocked.Increment(ref Next);Require(value>0,"Diagnostic cookie allocator exhausted.");return checked((ulong)value);}
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Private Train leaf: "+why);}
        static uint Site(P.Kind kind)=>kind switch{P.Kind.Command=>3u,P.Kind.Action=>4u,_=>throw new InvalidDataException("Invalid Train kind.")};
        static bool Same(NativeStateBridge.Call a,NativeStateBridge.Call b)=>a.Size==b.Size&&a.Site==b.Site&&
            a.Operation==b.Operation&&a.Version==b.Version&&a.State==b.State&&a.Stream==b.Stream&&a.MethodInfo==b.MethodInfo;
        internal static Invocation Open(C.Scope parent,object value,P.Kind kind,C.Buffer buffer,C.Direction direction,
            C.FactoryTicket? ticket=null)=>new(parent,value,kind,buffer,direction,ticket);
        internal static int Prepare(NativeStateBridge.Call call,ref NativeStateBridge.Plan plan)
            =>(Current??throw new InvalidDataException("No exact private Train invocation.")).Prepare(call,ref plan);
        internal static void Finish(NativeStateBridge.Call call,NativeStateBridge.Plan plan)
            =>(Current??throw new InvalidDataException("No exact private Train invocation.")).Finish(call,plan);
        internal static void Cleanup(NativeStateBridge.Call call,NativeStateBridge.Plan plan,int outcome)
            =>(Current??throw new InvalidDataException("No exact private Train invocation.")).Cleanup(call,plan,outcome);

        internal sealed class Invocation:IDisposable
        {
            readonly C.Scope Parent;readonly object Value;readonly P.Kind Kind;readonly C.Buffer Destination;
            readonly C.Direction Direction;readonly C.FactoryTicket? Ticket;readonly int Thread=Environment.CurrentManagedThreadId;
            readonly ulong Cookie;readonly object Stream;
            C.Leaf? Leaf;C.Buffer? Stage;NativeStateBridge.Call EnteredCall;IntPtr Replacement;
            bool Entered,Finished,Cleaned,Closed;
            internal bool Completed=>Entered&&Finished&&Cleaned;
            internal Invocation(C.Scope parent,object value,P.Kind kind,C.Buffer buffer,C.Direction direction,C.FactoryTicket? ticket)
            {
                Require(Current is null,"Nested direct invocations require a separate audited driver.");
                parent.Check();Require(parent.Call.Direction==direction&&parent.Call.Version==NativeStateFrame.NativeVersion,
                    "Parent direction/version mismatch.");
                Require(direction is C.Direction.Read or C.Direction.Write,"Invalid direction.");
                _=Site(kind);Parent=parent;Value=value;Kind=kind;Destination=buffer;Direction=direction;Ticket=ticket;
                Stream=buffer.Stream(direction);Cookie=NewCookie();Current=this;
            }
            void Check(){Require(!Closed&&Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(Current,this),"Stale/off-thread invocation.");Parent.Check();}
            C.Call Stamp=>new(Site(Kind),Cookie,NativeStateFrame.NativeVersion,Direction);
            internal bool MatchesForDispatch(NativeStateBridge.Call call)
            {
                if(Closed||Thread!=Environment.CurrentManagedThreadId||!ReferenceEquals(Current,this)||Parent.Root.Faulted)return false;
                return call.Size==40&&call.Site==Site(Kind)&&call.Operation==(int)Direction&&call.Version==NativeStateFrame.NativeVersion&&
                    call.State==new IntPtr(Native.Pointer(Value))&&call.Stream==new IntPtr(Native.Pointer(Stream));
            }
            void Match(NativeStateBridge.Call call)
            {
                Require(call.Size==40&&call.Site==Site(Kind)&&call.Operation==(int)Direction&&call.Version==NativeStateFrame.NativeVersion&&
                    call.State==new IntPtr(Native.Pointer(Value))&&call.Stream==new IntPtr(Native.Pointer(Stream)),
                    "Native callback does not match the exact private receiver/stream/site/version/direction.");
            }
            internal int Prepare(NativeStateBridge.Call call,ref NativeStateBridge.Plan plan)
            {
                Check();Match(call);Require(!Entered&&plan.Cookie==0&&plan.Replacement==IntPtr.Zero,"Repeated Prepare or nonempty plan.");
                Entered=true;EnteredCall=call;plan.Cookie=Cookie; // Cleanup authority exists before allocating the leaf.
                try
                {
                    Leaf=Parent.BeginLeaf(Value,Stream,Stamp,Ticket);
                    if(Direction==C.Direction.Write)
                    {
                        Stage=Parent.Trial.NewBuffer();Leaf.RegisterReplacement(Stage);
                        Replacement=new IntPtr(Native.Pointer(Stage.Stream(Direction)));plan.Replacement=Replacement;
                        Leaf.RequireOriginal(Value,Stage.Stream(Direction),Stamp);return 1;
                    }
                    Leaf.RequireOriginal(Value,Stream,Stamp);return 0;
                }
                catch{Parent.Root.Faulted=true;throw;}
            }
            void Prepared(NativeStateBridge.Call call,NativeStateBridge.Plan plan)
            {
                Check();Require(Entered&&!Cleaned&&Same(call,EnteredCall)&&plan.Cookie==Cookie&&plan.Replacement==Replacement,
                    "Callback call/plan changed or cleanup already ran.");
            }
            internal void Finish(NativeStateBridge.Call call,NativeStateBridge.Plan plan)
            {
                Prepared(call,plan);Require(!Finished&&Leaf is not null,"Repeated or failed-Prepare Finish.");
                try
                {
                    if(Direction==C.Direction.Read)Leaf!.FinishRead();
                    else
                    {
                        byte[] native=Stage!.Snapshot(),suffix=Leaf!.FinishWrite();
                        Require(native.Length<=MaxRecordBytes&&suffix.Length<=MaxRecordBytes-native.Length,"Native Train record exceeds the private limit.");
                        // Publication targets only an admitted private parent buffer.
                        // Any write/cleanup failure poisons that root; discard its bytes.
                        Destination.Native.Bytes(native.Concat(suffix).ToArray());
                    }
                    Finished=true;
                }
                catch{Parent.Root.Faulted=true;throw;}
            }
            internal void Cleanup(NativeStateBridge.Call call,NativeStateBridge.Plan plan,int outcome)
            {
                Prepared(call,plan);Require(outcome is >=0 and <=5,"Unknown bridge cleanup outcome.");
                Cleaned=true; // Never repeat cleanup if its own callback fails.
                if(outcome!=0||!Finished)Parent.Root.Faulted=true;
                Leaf?.Close(outcome==0&&Finished?0:1);
            }
            internal void RequireCompleted()
            {Check();Require(Completed&&!Parent.Root.Faulted,"Native bridge did not finish and clean exactly this private invocation.");}
            public void Dispose()
            {
                if(Closed)return;Check();
                try
                {
                    if(!Cleaned)
                    {
                        Parent.Root.Faulted=true;Cleaned=true;Leaf?.Close(1);
                    }
                }
                finally{Closed=true;Current=null;}
            }
        }
    }
}
