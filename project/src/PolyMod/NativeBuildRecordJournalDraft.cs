using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using W=Deception.PolyModAdapter.NativeLedgerOwnership.WeakIdentity;

namespace Deception.PolyModAdapter
{
    // PRIVATE CANDIDATE. Constructor provenance remains the default; explicit
    // positive factory tickets add private readers, never live/history admission.
    // Requires the separately reviewed Train-root and attachment-access patch.
    internal static class NativeBuildRecordJournalDraft
    {
        internal enum Kind { Command=1,Action=2 }
        const int Limit=512;
        static readonly object Gate=new();
        static readonly Dictionary<long,Lease> Reserved=new();
        sealed record Mark(W Identity,int Thread);
        static readonly Dictionary<long,Mark> Seen=new();
        [ThreadStatic] static Lease? Permit;
        static void Require(bool ok,string reason){if(!ok)throw new InvalidDataException("Build journal draft: "+reason);}
        static string TypeName(Kind kind)=>kind switch{Kind.Command=>"BuildCommand",Kind.Action=>"BuildAction",_=>throw new InvalidDataException("Unknown Build kind.")};
        static void Attempt(List<Exception> errors,Action action){try{action();}catch(Exception error){errors.Add(error);}}
        static void Throw(List<Exception> errors){if(errors.Count!=0)throw new AggregateException("Discard the private Build trial after cleanup failure.",errors);}
        internal static int ActiveReservations{get{lock(Gate)return Reserved.Count;}}
        // This gate grants no authority. In the candidate the exact existing
        // attachment mutators call it BEFORE removing/replacing a binding.
        internal static void BeforeMutation(object value)
        {
            lock(Gate)
            {
                if(!Reserved.TryGetValue(Native.Pointer(value),out var lease))return;
                Require(lease.Thread==Environment.CurrentManagedThreadId,"Off-thread reserved mutation.");
                Require(ReferenceEquals(Permit,lease)&&!lease.Released&&lease.Carrier.Identity.Matches(value),"Reserved Build attachment mutation.");
            }
        }
        internal static void RejectUnscopedPointer(long pointer)
        {
            lock(Gate)
            {
                Require(!Reserved.ContainsKey(pointer),"Reserved Build cannot use unscoped fallback.");
                if(!Seen.TryGetValue(pointer,out var mark))return;
                Require(mark.Thread==Environment.CurrentManagedThreadId,"Known Build cannot cross threads.");
                Require(mark.Identity.Target() is null,"Known private/protected/retired Build cannot use unscoped fallback.");
            }
        }
        static void Remember(object value)
        {
            lock(Gate)
            {
                int thread=Environment.CurrentManagedThreadId;
                foreach(long key in Seen.Where(p=>p.Value.Thread==thread&&p.Value.Identity.Target() is null).Select(p=>p.Key).ToArray())Seen.Remove(key);
                long keyValue=Native.Pointer(value);Require(keyValue!=0,"Missing native identity.");
                if(Seen.TryGetValue(keyValue,out var old))
                {Require(old.Thread==thread&&old.Identity.Matches(value),"Weak identity collision.");return;}
                Require(Seen.Count<8192,"Known Build identity capacity reached.");Seen.Add(keyValue,new(new(value),thread));
            }
        }
        internal sealed class Carrier
        {
            internal readonly Trial Trial;internal readonly object Value;internal readonly Kind Kind;internal readonly W Identity;
            internal bool ReadAttempted;internal readonly bool FromFactory;
            internal Carrier(Trial trial,object value,Kind kind,bool fromFactory=false){Trial=trial;Value=value;Kind=kind;Identity=new(value);FromFactory=fromFactory;}
        }
        internal sealed class Trial:IDisposable
        {
            internal readonly C.Trial Streams;
            readonly int Thread=Environment.CurrentManagedThreadId;
            readonly List<W> Protected=new();readonly List<Carrier> Carriers=new();bool Disposed;
            internal Trial(C.Trial streams,IEnumerable<object> protectedBuildCarriers)
            {
                Streams=streams;Streams.Check();ArgumentNullException.ThrowIfNull(protectedBuildCarriers);
                foreach(object value in protectedBuildCarriers)
                {Require(Protected.Count<8192,"Protected census bound exceeded.");Protected.Add(new(value));Remember(value);}
            }
            internal void Check(){Require(!Disposed&&Thread==Environment.CurrentManagedThreadId,"Stale/off-thread Build trial.");Streams.Check();}
            // Default constructor admission. The separate observed factory-return
            // path below requires its exact reader window; no Admit(object).
            internal Carrier[] SnapshotOwnedCarriers(){Check();return Carriers.ToArray();}
            internal Carrier NewCommand()=>Construct(Kind.Command);
            internal Carrier NewAction()=>Construct(Kind.Action);
            Carrier Construct(Kind kind)
            {
                Check();Require(Carriers.Count<Limit,"Private Build bound exceeded.");
                object value=Native.T(TypeName(kind)).GetConstructor(Type.EmptyTypes)!.Invoke(Array.Empty<object>());
                Require(Native.Pointer(value)!=0&&!Protected.Any(p=>p.Matches(value))&&!Carriers.Any(p=>p.Identity.Matches(value)),"Constructor returned a protected/owned identity.");
                lock(Gate)
                {
                    if(Seen.TryGetValue(Native.Pointer(value),out var old))
                    {Require(old.Thread==Thread,"Off-thread reused constructor identity.");Require(old.Identity.Target() is null,"Constructor returned an ever-known live identity.");}
                }
                Require(NativeBuildAttachmentPortDraft.Capture(value) is null,"Fresh constructor returned a bound carrier.");
                Remember(value);var carrier=new Carrier(this,value,kind);Carriers.Add(carrier);return carrier;
            }
            internal void RequireCarrier(Carrier carrier)
            {
                Check();Require(ReferenceEquals(carrier.Trial,this)&&Carriers.Contains(carrier)&&carrier.Identity.Matches(carrier.Value)&&
                    !Protected.Any(p=>p.Matches(carrier.Value)),"Private Build constructor/factory authority is absent.");
            }
            internal void Mutate(Carrier carrier,Action<object> mutation)
            {RequireCarrier(carrier);BeforeMutation(carrier.Value);mutation(carrier.Value);}
            internal Lease Read(C.Scope parent,Carrier carrier,C.Buffer buffer,C.Call call)=>Start(parent,carrier,buffer,call,true);
            internal Lease Write(C.Scope parent,Carrier carrier,C.Buffer buffer,C.Call call)=>Start(parent,carrier,buffer,call,false);
            Lease Start(C.Scope parent,Carrier carrier,C.Buffer buffer,C.Call call,bool reading,FactoryTicket? ticket=null)
            {
                parent.Check();
                try
                {
                RequireCarrier(carrier);
                Require(ReferenceEquals(parent.Trial,Streams)&&ReferenceEquals(buffer.Trial,Streams),"Foreign stream root.");
                Require(call.Site==(uint)carrier.Kind&&call.Direction==(reading?C.Direction.Read:C.Direction.Write)&&call.Version==123&&call.Cookie!=0,"Wrong Build leaf stamp.");
                if(reading)Require((!carrier.FromFactory||ticket is not null)&&!carrier.ReadAttempted&&NativeBuildAttachmentPortDraft.IsUnbound(carrier.Value),"Read requires an unused, unbound constructor-owned receiver.");
                var lease=new Lease(parent,carrier,buffer,call,reading);
                lock(Gate)
                {Require(!Reserved.ContainsKey(carrier.Identity.Key),"Build already has a read/write reservation.");Reserved.Add(carrier.Identity.Key,lease);}
                try{parent.EnlistBuildDraft(lease,ticket?.Reader.Window);if(reading)carrier.ReadAttempted=true;return lease;}
                catch{lease.Release();throw;}
                }
                catch{parent.Root.Faulted=true;throw;}
            }
            internal FactoryReader BeginFactoryReader(C.Scope parent,C.Buffer input,Kind kind,C.Call leaf)
                =>new(this,parent,input,kind,leaf);
            internal FactoryTicket RegisterFactoryReturn(FactoryReader reader,object value)
            {
                Check();reader.RequireReturn();Kind kind=reader.Kind;
                object typed=Native.As(value,TypeName(kind));
                Require(Native.T(TypeName(kind)).IsInstanceOfType(typed)&&(kind!=Kind.Command||!Native.T("BuildAction").IsInstanceOfType(typed)),"Factory returned wrong concrete Build kind.");
                Require(Native.Pointer(typed)!=0&&Native.Same(typed,value),"Factory wrapper cast changed identity.");
                Require(!Protected.Any(p=>p.Matches(typed))&&!Carriers.Any(p=>p.Identity.Matches(typed)),"Factory returned protected or owned Build.");
                lock(Gate){if(Seen.TryGetValue(Native.Pointer(typed),out var old)){Require(old.Thread==Thread,"Off-thread known factory identity.");Require(old.Identity.Target() is null,"Factory returned ever-known live Build.");}}
                Require(NativeBuildAttachmentPortDraft.IsUnbound(typed)&&Carriers.Count<Limit,"Factory returned bound Build or capacity reached.");
                long position=reader.Window.Position();var carrier=new Carrier(this,typed,kind,true);var ticket=new FactoryTicket(reader,carrier,position);
                // Remember before cleanup authority; a failure leaves a denial-only weak tombstone.
                Remember(typed);Carriers.Add(carrier);
                try{ticket.Lease=Start(reader.Parent,carrier,reader.Input,reader.Call,true,ticket);return ticket;}
                catch{reader.Parent.Root.Faulted=true;throw;}
            }
            public void Dispose()
            {
                if(Disposed)return;Check();lock(Gate)Require(!Reserved.Values.Any(p=>ReferenceEquals(p.Carrier.Trial,this)),"Build root must close before its trial.");
                Disposed=true;var errors=new List<Exception>();
                foreach(var carrier in Carriers)Attempt(errors,()=>NativeBuildAttachmentPortDraft.ForgetOwned(carrier.Value));
                Carriers.Clear();Protected.Clear();Throw(errors);
            }
        }
        // Positive native factory evidence, scoped separately from constructor admission.
        internal sealed class FactoryReader:IDisposable
        {
            internal readonly Trial Trial;internal readonly C.Scope Parent;internal readonly C.Buffer Input;
            internal readonly Kind Kind;internal readonly C.Call Call;internal readonly C.BuildReaderWindow Window;
            internal FactoryTicket? Ticket;bool Begun,Returned,Finalized,Completed,Closed;
            internal FactoryReader(Trial trial,C.Scope parent,C.Buffer input,Kind kind,C.Call call)
            {
                trial.Check();parent.Check();Require(ReferenceEquals(trial.Streams,parent.Trial)&&ReferenceEquals(input.Trial,parent.Trial)&&
                    kind is Kind.Command or Kind.Action&&call.Site==(uint)kind&&call.Cookie!=0&&call.Version==123&&call.Direction==C.Direction.Read,"Wrong Build factory trial/kind/stamp.");
                Trial=trial;Parent=parent;Input=input;Kind=kind;Call=call;Window=parent.BeginBuildFactoryReader(input,parent.Call);
            }
            internal void Check(bool cleanup=false)
            {Require(!Closed,"Build factory reader closed.");Trial.Check();Window.Check(cleanup);if(!cleanup)Require(!Completed,"Build factory reader completed.");}
            internal void Before(Kind kind,ushort discriminator)
            {
                Check();try{Require(!Begun&&!Returned&&!Finalized&&kind==Kind&&discriminator==1,"Wrong/repeated Build factory prefix.");Begun=true;}
                catch{Parent.Root.Faulted=true;throw;}
            }
            internal void RequireReturn(){Check();Require(Begun&&!Returned&&!Finalized&&Ticket is null,"Factory has no single pending return.");}
            internal void ObserveReturn(object? value,bool originalRan)
            {
                Check();try{RequireReturn();Require(originalRan&&value is not null,"Factory return is missing or replaced.");Ticket=Trial.RegisterFactoryReturn(this,value!);Returned=true;}
                catch{Parent.Root.Faulted=true;throw;}
            }
            internal void After(Exception? originalError)
            {
                Check(true);try{Require(Begun&&!Finalized,"Wrong/repeated Build factory finalizer.");Finalized=true;
                    if(originalError is not null||!Returned||Ticket is null){Parent.Root.Faulted=true;throw new InvalidDataException("Factory original/return did not complete.",originalError);}}
                catch{Parent.Root.Faulted=true;throw;}
            }
            internal bool Ready=>!Closed&&!Completed&&Begun&&Returned&&Finalized&&Ticket is not null&&!Parent.Root.Faulted;
            internal void Complete(object value)
            {
                Check();Require(Ready&&Ticket!.Consumed&&Ticket.Lease.IsClosed&&Ticket.Carrier.Identity.Matches(value),"Build helper did not complete its exact returned leaf.");Completed=true;
            }
            public void Dispose()
            {
                if(Closed)return;Check(true);var errors=new List<Exception>();
                if(!Completed)Parent.Root.Faulted=true;
                // Even a factory that never reaches Deserialize has a root-owned lease.
                if(Ticket is not null&&!Ticket.Lease.IsClosed)Attempt(errors,()=>Ticket.Lease.Close(1));
                Attempt(errors,()=>Window.Close(Completed&&!Parent.Root.Faulted));Closed=true;Throw(errors);
            }
        }
        internal sealed class FactoryTicket
        {
            internal readonly FactoryReader Reader;internal readonly Carrier Carrier;internal readonly long Position;
            internal Lease Lease=null!;internal bool Consumed;
            internal FactoryTicket(FactoryReader reader,Carrier carrier,long position){Reader=reader;Carrier=carrier;Position=position;}
            internal Lease Claim(object value,object stream,C.Call call)
            {
                Reader.Check();try
                {
                    Require(Reader.Ready&&ReferenceEquals(Reader.Ticket,this)&&!Consumed&&call==Reader.Call&&Carrier.Identity.Matches(value)&&Reader.Window.Reader.Matches(stream)&&
                        Reader.Window.Base.Matches(Native.Get(stream,"BaseStream")!)&&Reader.Window.Position()==Position,"Wrong/stale/repeated Build factory leaf claim.");
                    Reader.Parent.RequireBuildBuffer(Reader.Input,Reader.Window,Lease);
                    lock(Gate)Require(Reserved.TryGetValue(Carrier.Identity.Key,out var held)&&ReferenceEquals(held,Lease),"Build pending factory reservation lost.");
                    Consumed=true;return Lease;
                }
                catch{Reader.Parent.Root.Faulted=true;throw;}
            }
        }
        internal sealed class Lease:IDisposable,C.IBuildLease
        {
            internal readonly C.Scope Parent;internal readonly Carrier Carrier;internal readonly C.Buffer Buffer;internal readonly C.Call Call;
            internal readonly bool Reading;internal readonly int Thread=Environment.CurrentManagedThreadId;
            readonly W Stream,Base;readonly NativeBuildAttachmentPortDraft.Snapshot? Initial;
            NativeBuildAttachmentPortDraft.Snapshot? Published;
            byte[]? BaseBefore;long OriginalStart;bool Entered,Finished,Closed;internal bool Released;
            internal bool IsClosed=>Closed;
            C.Scope C.IBuildLease.Parent=>Parent;
            C.Buffer C.IBuildLease.Buffer=>Buffer;
            C.Call C.IBuildLease.Call=>Call;
            bool C.IBuildLease.IsClosed=>IsClosed;
            void C.IBuildLease.BeforeRootDecision()=>BeforeRootDecision();
            void C.IBuildLease.RollbackPublished()=>RollbackPublished();
            void C.IBuildLease.Release()=>Release();
            internal Lease(C.Scope parent,Carrier carrier,C.Buffer buffer,C.Call call,bool reading)
            {
                Parent=parent;Carrier=carrier;Buffer=buffer;Call=call;Reading=reading;
                object stream=buffer.Stream(call.Direction);Stream=new(stream);Base=new(Native.Get(stream,"BaseStream")!);
                Initial=reading?null:NativeBuildAttachmentPortDraft.Capture(carrier.Value);
                if(!reading)BaseBefore=NativeBuildAttachmentPortDraft.BaseStamp(carrier.Value);
            }
            void Check(bool rootClosing=false,bool allowFailed=false)
            {
                Require(Thread==Environment.CurrentManagedThreadId&&!Released,"Expired/off-thread Build lease.");
                Carrier.Trial.RequireCarrier(Carrier);
                if(!rootClosing){Parent.Check();Require((allowFailed||!Parent.Root.Faulted)&&!Closed,"Build leaf or root already closed/failed.");}
                else Require(Parent.Root.Closing&&!Parent.Root.Closed,"Wrong Build root cleanup phase.");
                lock(Gate)Require(Reserved.TryGetValue(Carrier.Identity.Key,out var held)&&ReferenceEquals(held,this),"Build reservation lost.");
            }
            void Mutation(Action action)
            {Require(Permit is null,"Nested Build mutation permit.");Permit=this;try{action();}finally{Permit=null;}}
            internal void RequireOriginal(object value,object stream,C.Call call)
            {
                Check();Require(!Entered&&call==Call&&Carrier.Identity.Matches(value)&&Stream.Matches(stream)&&Base.Matches(Native.Get(stream,"BaseStream")!),"Wrong/repeated original Build leaf.");
                OriginalStart=Buffer.Native.Position;Require(OriginalStart>=0&&OriginalStart<=Buffer.Native.Length&&(Reading||OriginalStart==Buffer.Native.Length),"Invalid/nonappend original stream position.");Entered=true;
            }
            // Call only AFTER the raw native reader succeeded. Native fields are
            // decoded by the original, then exactly the existing suffix is read.
            internal void FinishRead()
            {
                Check();try
                {
                Require(Reading&&Entered&&!Finished,"Wrong/repeated Build read completion.");
                Require(NativeBuildAttachmentPortDraft.IsUnbound(Carrier.Value),"Unexpected attachment before read publication.");
                Require(Buffer.Native.Position>=OriginalStart&&Buffer.Native.Position-OriginalStart<=4096,"Native Build reader exceeded its record bound.");
                Mutation(()=>Published=NativeBuildAttachmentPortDraft.ReadPublish(Carrier.Value,length=>
                {Require(length>=0&&length<=4096-(Buffer.Native.Position-OriginalStart),"Build suffix exceeded its record bound.");return Buffer.Native.ReadExact(length);}));BaseBefore=NativeBuildAttachmentPortDraft.BaseStamp(Carrier.Value);Finished=true;
                }
                catch{Parent.Root.Faulted=true;throw;}
            }
            // Call only AFTER native Serialize wrote its base into this exact
            // private buffer. These bytes remain private until the ROOT commits.
            internal void FinishWrite()
            {
                Check();try
                {
                Require(!Reading&&Entered&&!Finished,"Wrong/repeated Build write completion.");
                NativeBuildAttachmentPortDraft.RequireSame(Carrier.Value,Initial);
                Require(BaseBefore!.SequenceEqual(NativeBuildAttachmentPortDraft.BaseStamp(Carrier.Value)),"Build base changed while writing.");
                byte[] suffix=Initial?.CopyBytes()??Array.Empty<byte>();
                Require(Buffer.Native.Position==Buffer.Native.Length&&Buffer.Native.Length>=OriginalStart&&Buffer.Native.Length-OriginalStart<=4096-suffix.Length,"Build record bound exceeded or writer moved behind its append position.");Buffer.Native.Bytes(suffix);Finished=true;
                }
                catch{Parent.Root.Faulted=true;throw;}
            }
            internal void BeforeRootDecision()
            {
                Check(true);Require(Closed,"Unclosed Build leaf at root completion.");if(Parent.Root.Faulted)return;Require(Finished,"Unfinished Build leaf at root completion.");
                NativeBuildAttachmentPortDraft.RequireSame(Carrier.Value,Reading?Published:Initial);
                Require(BaseBefore!.SequenceEqual(NativeBuildAttachmentPortDraft.BaseStamp(Carrier.Value)),"Build base changed before outer completion.");
            }
            internal void RollbackPublished()
            {Check(true);if(Reading&&Published is not null)Mutation(()=>NativeBuildAttachmentPortDraft.ForgetExact(Carrier.Value,Published));}
            // Local bookkeeping only: no native I/O, callbacks or fallible port.
            internal void Release()
            {
                lock(Gate)
                {
                    if(Released)return;
                    if(Reserved.TryGetValue(Carrier.Identity.Key,out var held)&&ReferenceEquals(held,this))Reserved.Remove(Carrier.Identity.Key);
                    Released=true;
                }
            }
            internal void Close(int outcome)
            {
                if(Closed)return;Check(false,true);if(outcome!=0||!Finished)Parent.Root.Faulted=true;Closed=true;
            }
            public void Dispose()=>Close(Finished?0:1);
        }
    }
}
