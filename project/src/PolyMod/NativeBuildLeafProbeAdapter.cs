using System;
using System.IO;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using J=Deception.PolyModAdapter.NativeBuildRecordJournalDraft;
using B=Deception.PolyModAdapter.NativeStateBridge;

namespace Deception.PolyModAdapter
{
    // Constructor records or explicit factory-ticket private readers. The POD bridge contains
    // every exception and is the sole caller of the raw native original.
    internal static class NativeBuildLeafProbeAdapter
    {
        [ThreadStatic] static Invocation? Current;
        internal static int CurrentDepth=>Current is null?0:1;
        internal static bool MatchesInvocation(B.Call call)=>Current?.Matches(call)==true;
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Private Build leaf: "+why);}
        static bool Same(B.Call a,B.Call b)=>a.Size==b.Size&&a.Site==b.Site&&a.Operation==b.Operation&&
            a.Version==b.Version&&a.State==b.State&&a.Stream==b.Stream&&a.MethodInfo==b.MethodInfo;
        internal static Invocation Open(C.Scope parent,J.Carrier carrier,C.Buffer destination,C.Direction direction)
            =>new(parent,carrier,destination,direction);
        internal static Invocation OpenFactory(C.Scope parent,J.FactoryTicket ticket,C.Buffer destination)
            =>new(parent,ticket.Carrier,destination,C.Direction.Read,ticket);
        internal static int Prepare(B.Call call,ref B.Plan plan)=>(Current??throw new InvalidDataException("No Build invocation.")).Prepare(call,ref plan);
        internal static void Finish(B.Call call,B.Plan plan)=>(Current??throw new InvalidDataException("No Build invocation.")).Finish(call,plan);
        internal static void Cleanup(B.Call call,B.Plan plan,int outcome)=>(Current??throw new InvalidDataException("No Build invocation.")).Cleanup(call,plan,outcome);

        internal sealed class Invocation:IDisposable
        {
            readonly C.Scope Parent;readonly J.Carrier Carrier;readonly C.Buffer Destination;
            readonly C.Direction Direction;readonly object Stream;readonly ulong Cookie;readonly J.FactoryTicket? Ticket;
            readonly int Thread=Environment.CurrentManagedThreadId;
            C.Buffer? Stage;J.Lease? Lease;B.Call EnteredCall;IntPtr Replacement;
            bool Entered,Finished,Cleaned,Closed;
            internal Invocation(C.Scope parent,J.Carrier carrier,C.Buffer destination,C.Direction direction,J.FactoryTicket? ticket=null)
            {
                Require(Current is null&&NativeTrainLeafProbeAdapter.CurrentDepth==0,"Nested private leaf invocation.");
                parent.Check();carrier.Trial.RequireCarrier(carrier);
                Require(direction is C.Direction.Read or C.Direction.Write&&parent.Call.Direction==direction&&
                    parent.Call.Version==123&&ReferenceEquals(carrier.Trial.Streams,parent.Trial),"Parent direction/version/trial differs.");
                parent.RequireBuildBuffer(destination,ticket?.Reader.Window,ticket?.Lease);
                Parent=parent;Carrier=carrier;Destination=destination;Direction=direction;Stream=destination.Stream(direction);
                Ticket=ticket;Cookie=ticket?.Reader.Call.Cookie??NativeTrainLeafProbeAdapter.NewCookie();Current=this;
            }
            void Check(){Require(!Closed&&Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(Current,this),"Stale/off-thread Build invocation.");Parent.Check();}
            C.Call Stamp=>new((uint)Carrier.Kind,Cookie,123,Direction);
            internal bool Matches(B.Call call)=>!Closed&&Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(Current,this)&&!Parent.Root.Faulted&&
                call.Size==40&&call.Site==(uint)Carrier.Kind&&call.Operation==(int)Direction&&call.Version==123&&
                call.State==new IntPtr(Native.Pointer(Carrier.Value))&&call.Stream==new IntPtr(Native.Pointer(Stream));
            internal int Prepare(B.Call call,ref B.Plan plan)
            {
                Check();Require(Matches(call)&&!Entered&&plan.Cookie==0&&plan.Replacement==IntPtr.Zero,"Wrong/repeated Build prepare.");
                Entered=true;EnteredCall=call;plan.Cookie=Cookie;
                try
                {
                    Parent.RequireBuildBuffer(Destination,Ticket?.Reader.Window,Ticket?.Lease);
                    if(Direction==C.Direction.Write)
                    {
                        Require(Destination.Native.Position==Destination.Native.Length,"Build destination is not at its append position.");
                        Stage=Parent.Trial.NewBuffer();Parent.RegisterStaging(Stage);
                        Lease=Carrier.Trial.Write(Parent,Carrier,Stage,Stamp);
                        Replacement=new IntPtr(Native.Pointer(Stage.Stream(Direction)));plan.Replacement=Replacement;
                        Lease.RequireOriginal(Carrier.Value,Stage.Stream(Direction),Stamp);return 1;
                    }
                    Lease=Ticket is null?Carrier.Trial.Read(Parent,Carrier,Destination,Stamp):Ticket.Claim(Carrier.Value,Stream,Stamp);
                    Lease.RequireOriginal(Carrier.Value,Stream,Stamp);return 0;
                }
                catch{Parent.Root.Faulted=true;throw;}
            }
            void Prepared(B.Call call,B.Plan plan)
            {Check();Require(Entered&&!Cleaned&&Same(call,EnteredCall)&&plan.Cookie==Cookie&&plan.Replacement==Replacement,"Build callback stamp changed or cleanup repeated.");}
            internal void Finish(B.Call call,B.Plan plan)
            {
                Prepared(call,plan);Require(!Finished&&Lease is not null,"Repeated/failed Build Finish.");
                try
                {
                    if(Direction==C.Direction.Read)Lease!.FinishRead();
                    else
                    {
                        Lease!.FinishWrite();byte[] bytes=Stage!.Snapshot();
                        Require(bytes.Length<=4096&&Destination.Native.Position==Destination.Native.Length,"Build staged bytes/append position differ.");
                        Destination.Native.Bytes(bytes);
                    }
                    Finished=true;
                }
                catch{Parent.Root.Faulted=true;throw;}
            }
            internal void Cleanup(B.Call call,B.Plan plan,int outcome)
            {
                Prepared(call,plan);Require(outcome is >=0 and <=5,"Unknown cleanup outcome.");Cleaned=true;
                if(outcome!=0||!Finished)Parent.Root.Faulted=true;
                Lease?.Close(outcome==0&&Finished?0:1);
            }
            internal void RequireCompleted()
            {Check();Require(Entered&&Finished&&Cleaned&&!Parent.Root.Faulted,"Build original/Finish/cleanup did not complete.");}
            public void Dispose()
            {
                if(Closed)return;Check();
                try{if(!Cleaned){Parent.Root.Faulted=true;Cleaned=true;Lease?.Close(1);}}
                finally{Closed=true;Current=null;}
            }
        }
    }
}
