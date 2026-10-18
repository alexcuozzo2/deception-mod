using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using WeakIdentity=Deception.PolyModAdapter.NativeLedgerOwnership.WeakIdentity;
using NativeBuffer=Deception.PolyModAdapter.NativeStateProbe.Buffer;
using Kind=Deception.PolyModAdapter.NativePaidBirthCommands.Kind;
using Wire=Deception.PolyModAdapter.NativePaidBirthCommands.Wire;

namespace Deception.PolyModAdapter
{
    // DRAFT, PRIVATE DIAGNOSTIC ONLY. No hooks or wire discovery. The explicit
    // native constructors below allocate only private buffers/Train receivers.
    // Factory admission candidate: only an exact witnessed GetCommand/GetAction
    // return inside an owned reader window may extend constructor provenance.
    // No native observer hooks are installed by this file.
    internal static class NativeTrainStreamContext
    {
        internal enum Direction { Write=0,Read=1 }
        internal readonly record struct Call(uint Site,ulong Cookie,int Version,Direction Direction);
        const int MaxStreams=8192,MaxCarriers=512,MaxEntries=1024,MaxDepth=32;
        static readonly object Gate=new();
        static readonly Dictionary<long,WeakIdentity> SeenStreams=new();
        // Dispatcher candidate only. Persistent entries retain native WEAK
        // identities; a numeric address alone never authorizes a private call.
        [Flags] internal enum DispatchOwnership
        {
            Unrelated=0,PrivateCarrier=1,ProtectedCarrier=2,TaggedCarrier=4,
            OwnedStream=8,OwnedBaseStream=16,Retired=32,ReservedCarrier=64,
            FactoryPending=128,OffOwnerThread=256,IncompleteRegistration=512
        }
        sealed class DispatchMark
        {
            internal readonly WeakIdentity Identity;internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal DispatchOwnership Flags;
            internal DispatchMark(object value,DispatchOwnership flags){Identity=new(value);Flags=flags;}
        }
        const int MaxDispatchMarks=24576;
        static readonly Dictionary<long,DispatchMark> DispatchSeen=new();
        static void RememberDispatch(object value,DispatchOwnership flags)
        {
            lock(Gate)
            {
                int thread=Environment.CurrentManagedThreadId;
                foreach(long key in DispatchSeen.Where(x=>x.Value.Thread==thread&&x.Value.Identity.Target() is null).Select(x=>x.Key).ToArray())DispatchSeen.Remove(key);
                long pointer=Pointer(value);Require(pointer!=0,"Missing dispatch identity.");
                if(DispatchSeen.TryGetValue(pointer,out var old))
                {
                    Require(old.Thread==thread&&old.Identity.Matches(value),"Cross-thread or colliding dispatch identity.");
                    Require((flags&DispatchOwnership.PrivateCarrier)==0||(old.Flags&DispatchOwnership.ProtectedCarrier)==0,
                        "An ever-protected carrier cannot acquire private constructor/factory authority.");
                    old.Flags|=flags;return;
                }
                Require(DispatchSeen.Count<MaxDispatchMarks,"Live dispatch tombstone bound reached.");
                DispatchSeen.Add(pointer,new(value,flags));
            }
        }
        static void RetireDispatch(params WeakIdentity?[] identities)
        {
            lock(Gate)foreach(var identity in identities)
                if(identity is not null&&DispatchSeen.TryGetValue(identity.Key,out var mark))
                {
                    Require(mark.Thread==Environment.CurrentManagedThreadId,"Off-thread dispatch retirement.");
                    object? value=identity.Target();
                    if(value is not null&&mark.Identity.Matches(value))mark.Flags|=DispatchOwnership.Retired;
                }
        }
        internal static void RememberTaggedCarrier(object value,Kind kind)
            =>RememberDispatch(Typed(value,kind),DispatchOwnership.TaggedCarrier);
        // READ-ONLY: no pruning, retirement, stream reads/seeks, attachment Get
        // or pointer reconstruction. Off-thread known keys reject BEFORE weak
        // native access. Dead weak targets do not claim a newly reused address.
        static DispatchOwnership InspectIdentity(object value,out bool recorded)
        {
            long key=Pointer(value);Require(key!=0,"Null dispatch receiver/stream.");
            lock(Gate)
            {
                recorded=DispatchSeen.TryGetValue(key,out var mark);
                if(!recorded)return DispatchOwnership.Unrelated;
                if(mark!.Thread!=Environment.CurrentManagedThreadId)return mark.Flags|DispatchOwnership.OffOwnerThread;
                object? target=mark.Identity.Target();
                try
                {
                    if(target is null)return DispatchOwnership.Unrelated;
                    Require(Native.Same(target,value),"Live dispatch pointer collision.");return mark.Flags;
                }
                finally{GC.KeepAlive(target);GC.KeepAlive(value);}
            }
        }
        // Rejection evidence only for an unscoped raw command pointer. This
        // neither reconstructs a wrapper nor grants ownership or a wire mode.
        // Unknown/dead identities leave native legacy behavior untouched.
        internal static void RejectUnscopedCarrierPointer(long pointer)
        {
            if(pointer==0)return;
            lock(Gate)
            {
                bool recorded=DispatchSeen.TryGetValue(pointer,out var mark);
                if(recorded)
                {
                    Require(mark!.Thread==Environment.CurrentManagedThreadId,
                        "Known unscoped carrier crossed threads.");
                    object? target=mark.Identity.Target();
                    try
                    {
                        Require(target is null,"Known carrier requires an exact private outer invocation.");
                    }
                    finally{GC.KeepAlive(target);}
                }
                // Older publication registries can precede DispatchSeen on a
                // failed admission. Their keys are deny-only, never authority.
                Require(recorded||!CarrierSeen.ContainsKey(pointer),
                    "Incomplete carrier registration cannot use legacy outer fallback.");
                Require(!Reserved.Keys.Any(x=>x.Item2==pointer)&&!FactoryReserved.Keys.Any(x=>x.Item2==pointer),
                    "Reserved carrier cannot use legacy outer fallback.");
            }
        }
        internal static DispatchOwnership InspectForDispatch(object value,Kind kind,object stream)
        {
            value=Typed(value,kind);var flags=InspectIdentity(value,out bool carrierRecorded);
            if((flags&DispatchOwnership.OffOwnerThread)!=0)return flags;
            lock(Gate)
            {
                var key=(kind,Pointer(value));
                // A failed constructor/factory/mark publication may have
                // reached the older registry first. Its address is rejection
                // evidence only; it never authorizes a private call.
                if(!carrierRecorded&&CarrierSeen.ContainsKey(key.Item2))flags|=DispatchOwnership.PrivateCarrier|DispatchOwnership.IncompleteRegistration;
                if(Reserved.ContainsKey(key))flags|=DispatchOwnership.ReservedCarrier;
                if(FactoryReserved.ContainsKey(key))flags|=DispatchOwnership.FactoryPending;
            }
            var streamFlags=InspectIdentity(stream,out bool streamRecorded);
            lock(Gate)if(!streamRecorded&&SeenStreams.ContainsKey(Pointer(stream)))
                streamFlags|=DispatchOwnership.OwnedStream|DispatchOwnership.IncompleteRegistration;
            flags|=streamFlags;
            // A known stream is already denied without exact scope; do not
            // touch a disposed stream's native BaseStream merely to deny it.
            if(streamFlags!=DispatchOwnership.Unrelated)return flags;
            object underlying=Underlying(stream);var baseFlags=InspectIdentity(underlying,out bool baseRecorded);
            lock(Gate)if(!baseRecorded&&SeenStreams.ContainsKey(Pointer(underlying)))
                baseFlags|=DispatchOwnership.OwnedBaseStream|DispatchOwnership.IncompleteRegistration;
            return flags|baseFlags;
        }
        internal static void RejectUnscopedStream(object stream)
        {
            var flags=InspectIdentity(stream,out bool recorded);
            lock(Gate)Require(flags==DispatchOwnership.Unrelated&&(recorded||!SeenStreams.ContainsKey(Pointer(stream))),
                "Known or incompletely registered stream requires an exact private invocation.");
            object underlying=Underlying(stream);flags=InspectIdentity(underlying,out recorded);
            lock(Gate)Require(flags==DispatchOwnership.Unrelated&&(recorded||!SeenStreams.ContainsKey(Pointer(underlying))),
                "Owned BaseStream alias requires an exact private invocation.");
        }
        internal static int DispatchMarkCount{get{lock(Gate)return DispatchSeen.Count;}}

        static readonly Dictionary<(Kind,long),Entry> Reserved=new();
        static readonly Dictionary<long,WeakIdentity> CarrierSeen=new();
        static readonly Dictionary<(Kind,long),FactoryTicket> FactoryReserved=new();
        [ThreadStatic] static Scope? Current;
        [ThreadStatic] static Entry? MutationPermit;
        [ThreadStatic] static bool CleanupPermit;
        static void Require(bool ok,string reason)
        {if(!ok)throw new InvalidDataException("Train stream context: "+reason);}
        static Kind RecordKind(uint site)=>site switch
        {3=>Kind.Command,4=>Kind.Action,_=>throw new InvalidDataException("Train leaf site must be3 or4.")};
        static string Owner(Kind kind)=>kind switch
        {Kind.Command=>"TrainCommand",Kind.Action=>"TrainAction",_=>throw new InvalidDataException("Unknown Train carrier kind.")};
        static object Typed(object value,Kind kind)=>Native.As(value,Owner(kind));
        static long Pointer(object value)=>Native.Pointer(value);
        static object Underlying(object stream)=>Native.Get(stream,"BaseStream")??throw new InvalidDataException("Native stream has no BaseStream.");
        static void Validate(Call call,Wire wire)
        {
            Require(call.Cookie!=0&&call.Site is 0 or 3 or 4&&call.Direction is Direction.Read or Direction.Write,"Invalid operation stamp.");
            Require(wire is Wire.LegacyNative or Wire.DcpbV1,"Unknown explicit Train wire capability.");
            Require(call.Version==NativeStateFrame.NativeVersion,"First private stage supports only native123.");
        }
        static void Remember(params object[] objects)
        {
            lock(Gate)
            {
                foreach(long key in SeenStreams.Where(x=>x.Value.Target() is null).Select(x=>x.Key).ToArray())SeenStreams.Remove(key);
                foreach(object value in objects)
                {
                    long key=Pointer(value);Require(key!=0,"Missing stream identity.");
                    if(SeenStreams.TryGetValue(key,out var old))
                    {Require(old.Matches(value),"Live stream pointer collision.");continue;}
                    Require(SeenStreams.Count<MaxStreams,"Live stream tombstone bound reached.");SeenStreams.Add(key,new(value));
                }
            }
        }
        static bool WasOwned(object value)
        {
            lock(Gate)
            {
                long key=Pointer(value);if(!SeenStreams.TryGetValue(key,out var old))return false;
                if(old.Matches(value))return true;SeenStreams.Remove(key);return false;
            }
        }
        // An unknown external stream has native legacy semantics only. A known
        // owned stream or ANY other wrapper over its BaseStream needs an active
        // exact registration; retirement must not silently downgrade to Legacy.
        internal static Wire UnscopedMode(object stream)
        {
            Require(!WasOwned(stream)&&!WasOwned(Underlying(stream)),"Owned/retired stream has no exact active capability.");
            return Wire.LegacyNative;
        }
        // Required by the separate attachment patch. Native field mutation paths
        // also call this gate before changing a reserved carrier. No claim is
        // made that an unhooked helper intercepts arbitrary native setters.
        internal static void BeforeCarrierMutation(object value,Kind kind)
        {
            value=Typed(value,kind);var key=(kind,Pointer(value));
            lock(Gate)
            {
                Require(!FactoryReserved.ContainsKey(key),"Factory receiver is reserved before its first leaf admission.");
                if(!Reserved.TryGetValue(key,out var entry))return;
                Require(ReferenceEquals(MutationPermit,entry)&&entry.Root.Thread==Environment.CurrentManagedThreadId&&
                    !entry.Released&&(CleanupPermit?entry.Root.Closing:ReferenceEquals(Current,entry.Parent)),
                    "Carrier mutation conflicts with an exclusive outer read/write.");
                Require(entry.Carrier.Identity.Matches(value),"Reserved carrier identity changed.");
            }
        }
        static void Permitted(Entry entry,bool cleanup,Action action)
        {
            Require(MutationPermit is null,"Nested mutation permit rejected.");
            MutationPermit=entry;CleanupPermit=cleanup;
            try{action();}finally{CleanupPermit=false;MutationPermit=null;}
        }
        static void All(List<Exception> errors,Action action)
        {try{action();}catch(Exception error){errors.Add(error);}}
        static void Throw(List<Exception> errors)
        {if(errors.Count>0)throw new AggregateException("Train context cleanup failed; discard this private trial.",errors);}

        internal sealed class Carrier
        {
            internal readonly Trial Trial;internal readonly object Value;internal readonly Kind Kind;
            internal readonly WeakIdentity Identity;internal readonly bool FromFactory;
            internal Carrier(Trial trial,object value,Kind kind,bool fromFactory=false){Trial=trial;Value=value;Kind=kind;Identity=new(value);FromFactory=fromFactory;}
        }
        internal sealed class Buffer:IDisposable
        {
            internal readonly Trial Trial;internal readonly NativeBuffer Native;
            internal readonly WeakIdentity Reader,Base;internal readonly WeakIdentity? Writer;
            internal Root? Assigned;internal bool Disposed;
            internal Buffer(Trial trial,byte[]? input)
            {
                Trial=trial;Native=input is null?new NativeBuffer():new NativeBuffer((byte[])input.Clone());
                try
                {
                    Reader=new(Native.R);Base=new(Underlying(Native.R));
                    if(input is null){Writer=new(Native.W);Remember(Native.R,Native.W,Underlying(Native.R));}
                    else Remember(Native.R,Underlying(Native.R)); // Native input MemoryStream is read-only.
                    RememberDispatch(Native.R,DispatchOwnership.OwnedStream);
                    if(input is null)RememberDispatch(Native.W,DispatchOwnership.OwnedStream);
                    RememberDispatch(Underlying(Native.R),DispatchOwnership.OwnedBaseStream);
                }
                catch{Native.Dispose();throw;}
            }
            internal object Stream(Direction direction)
            {
                Require(!Disposed&&direction is Direction.Read or Direction.Write,"Private buffer is retired or direction is invalid.");
                Require(direction==Direction.Read||Writer is not null,"Input buffer cannot become a staging writer.");
                return direction==Direction.Read?Native.R:Native.W;
            }
            internal byte[] Snapshot(){Require(!Disposed,"Private buffer is retired.");return Native.Snapshot();}
            public void Dispose()
            {
                if(Disposed)return;Disposed=true;
                try{RetireDispatch(Reader,Writer,Base);}finally{Native.Dispose();}
            }
        }
        internal sealed class Trial:IDisposable
        {
            readonly int Thread=Environment.CurrentManagedThreadId;
            readonly List<WeakIdentity> Protected=new();readonly List<Carrier> Carriers=new();readonly List<Buffer> Buffers=new();
            internal readonly HashSet<Scope> Active=new();bool Disposed;
            internal Trial(IEnumerable<object> protectedCarriers)
            {
                ArgumentNullException.ThrowIfNull(protectedCarriers);
                foreach(object value in protectedCarriers)
                {
                    Require(Protected.Count<8192,"Protected census bound exceeded.");Protected.Add(new(value));
                    RememberDispatch(value,DispatchOwnership.ProtectedCarrier);
                }
            }
            internal void Check(){Require(!Disposed&&Thread==Environment.CurrentManagedThreadId,"Private trial is retired or off its owner thread.");}
            internal Buffer NewBuffer(byte[]? input=null)
            {Check();Require(Buffers.Count<MaxEntries,"Private buffer bound reached.");var buffer=new Buffer(this,input);Buffers.Add(buffer);return buffer;}
            internal Carrier NewCarrier(Kind kind)
            {
                Check();Require(Carriers.Count<MaxCarriers,"Private receiver bound reached.");
                object value=Native.T(Owner(kind)).GetConstructor(Type.EmptyTypes)!.Invoke(Array.Empty<object>());
                Require(Pointer(value)!=0&&!Protected.Any(x=>x.Matches(value))&&!Carriers.Any(x=>x.Identity.Matches(value)),
                    "Fresh native constructor did not produce a new private carrier.");
                lock(Gate)
                {
                    foreach(long key in CarrierSeen.Where(p=>p.Value.Target() is null).Select(p=>p.Key).ToArray())CarrierSeen.Remove(key);
                    Require(!CarrierSeen.ContainsKey(Pointer(value))&&CarrierSeen.Count<MaxStreams,"Constructor returned an already-owned native identity or capacity was reached.");
                    CarrierSeen.Add(Pointer(value),new(value));
                    RememberDispatch(value,DispatchOwnership.PrivateCarrier);
                }
                var carrier=new Carrier(this,value,kind);Carriers.Add(carrier);return carrier;
            }
            internal void CheckFactoryCandidate(object value,Kind kind)
            {
                Check();value=Typed(value,kind);
                Require(Pointer(value)!=0&&!Protected.Any(x=>x.Matches(value))&&!Carriers.Any(x=>x.Identity.Matches(value)),
                    "Factory returned a protected or previously owned carrier.");
                Require(NativePaidBirthCommands.Get(value,kind) is null,"Factory returned an already-bound carrier.");
            }
            internal Carrier ClaimFactory(FactoryTicket ticket,object value,Kind kind,Scope scope,object stream,Call leaf)
            {
                Check();ticket.RequireClaim(value,kind,scope,stream,leaf);CheckFactoryCandidate(value,kind);
                Require(Carriers.Count<MaxCarriers,"Private receiver bound reached.");
                var carrier=new Carrier(this,Typed(value,kind),kind,true);Carriers.Add(carrier);
                ticket.Consumed=true;return carrier;
            }
            internal Carrier RequireCarrier(object value,Kind kind)
            {
                Check();value=Typed(value,kind);
                Require(!Protected.Any(x=>x.Matches(value)),"Protected source/history carrier cannot be a private receiver.");
                return Carriers.SingleOrDefault(x=>x.Kind==kind&&x.Identity.Matches(value))
                    ??throw new InvalidDataException("Train receiver was not constructed by this private trial.");
            }
            internal void Mutate(Carrier carrier,Action<object> mutation)
            {RequireCarrier(carrier.Value,carrier.Kind);BeforeCarrierMutation(carrier.Value,carrier.Kind);mutation(carrier.Value);}
            internal Scope Begin(Buffer buffer,Call owner,Wire wire,Scope? parent=null)
            {
                Check();Validate(owner,wire);Require(ReferenceEquals(Current,parent),"Explicit parent is not the current operation.");
                Require(parent is null||ReferenceEquals(parent.Trial,this),"Cross-trial nesting rejected.");
                Require(parent is null||!parent.Root.Faulted,"Cannot nest into a failed outer operation.");
                Require(parent?.BuildFactoryWindow is null,"Cannot nest inside an active Build factory window.");
                Require(Active.Count<MaxDepth&&!Active.Any(s=>s.Call.Site==owner.Site&&s.Call.Cookie==owner.Cookie),"Duplicate cookie or context-depth bound reached.");
                var scope=new Scope(this,buffer,owner,wire,parent);Active.Add(scope);Current=scope;return scope;
            }
            public void Dispose()
            {
                if(Disposed)return;Check();Require(Active.Count==0,"Cannot retire a trial with active native operations.");Disposed=true;
                var errors=new List<Exception>();foreach(var buffer in Buffers)All(errors,buffer.Dispose);
                foreach(var carrier in Carriers)
                {
                    All(errors,()=>RetireDispatch(carrier.Identity));
                    All(errors,()=>NativePaidBirthCommands.Forget(carrier.Value,carrier.Kind));
                }
                Buffers.Clear();Carriers.Clear();Protected.Clear();Throw(errors);
            }
        }
        // Shared transaction participant; the Train context does not depend on
        // Build attachment stores or grant Build construction authority.
        internal interface IBuildLease
        {
            Scope Parent { get; }
            Buffer Buffer { get; }
            Call Call { get; }
            bool IsClosed { get; }
            void BeforeRootDecision();
            void RollbackPublished();
            void Release();
        }
        internal sealed class Root
        {
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal readonly List<Entry> Entries=new();internal readonly HashSet<Buffer> Buffers=new();
            internal readonly List<FactoryTicket> FactoryTickets=new();
            internal readonly List<IBuildLease> BuildDrafts=new();
            internal bool Faulted,Closing,Closed;
        }
        sealed record Alias(Buffer Buffer,WeakIdentity Stream,WeakIdentity Base,Direction Direction,int Version,Call? LeafOwner);
        internal sealed class Scope:IDisposable
        {
            internal readonly Trial Trial;internal readonly Call Call;internal readonly Wire Wire;
            internal readonly Scope? Parent;internal readonly Root Root;
            readonly List<Alias> Aliases=new();readonly HashSet<Leaf> Leaves=new();
            internal FactoryReader? FactoryWindow;internal BuildReaderWindow? BuildFactoryWindow;bool Finished,Closed;
            internal Scope(Trial trial,Buffer buffer,Call call,Wire wire,Scope? parent)
            {Trial=trial;Call=call;Wire=wire;Parent=parent;Root=parent?.Root??new Root();Register(buffer);}
            internal void Check()
            {Trial.Check();Require(!Closed&&!Root.Closed&&!Root.Closing&&ReferenceEquals(Current,this),"Wrong, closed or noncurrent parent scope.");}
            void Register(Buffer buffer,Call? leafOwner=null)
            {
                Require(ReferenceEquals(buffer.Trial,Trial)&&!buffer.Disposed,"Stream was not privately constructed by this trial.");
                Require(buffer.Assigned is null||ReferenceEquals(buffer.Assigned,Root),"An owned buffer cannot cross outer operations.");
                object stream=buffer.Stream(Call.Direction);
                Require(!Aliases.Any(x=>x.Stream.Matches(stream)),"Duplicate exact stream alias.");
                Aliases.Add(new(buffer,new(stream),new(Underlying(stream)),Call.Direction,Call.Version,leafOwner));
                buffer.Assigned=Root;Root.Buffers.Add(buffer);
            }
            internal void RegisterStaging(Buffer buffer){Check();Require(!Finished,"Operation already finished.");Register(buffer);}
            internal void RequireBuildBuffer(Buffer buffer,BuildReaderWindow? factory=null,IBuildLease? pending=null)
            {
                Check();Require((factory is null&&BuildFactoryWindow is null)||(factory is not null&&ReferenceEquals(factory,BuildFactoryWindow)),"Wrong Build reader window.");
                if(factory is not null)factory.RequireBuffer(buffer,pending);
                Check();Require(!Finished&&!Root.Faulted&&FactoryWindow is null&&!Root.Entries.Any(x=>!x.Closed)&&!Root.BuildDrafts.Any(x=>!x.IsClosed&&!ReferenceEquals(x,pending)),
                    "Build buffer requires an idle active private scope.");
                Require(ReferenceEquals(buffer.Trial,Trial)&&!buffer.Disposed&&ReferenceEquals(buffer.Assigned,Root),"Build buffer is outside this root.");
                object stream=buffer.Stream(Call.Direction);
                Require(Aliases.Any(a=>ReferenceEquals(a.Buffer,buffer)&&a.LeafOwner is null&&a.Direction==Call.Direction&&a.Version==Call.Version&&
                    a.Stream.Matches(stream)&&a.Base.Matches(Underlying(stream))),"Build buffer lacks an exact parent registration.");
            }
            internal void RegisterLeafStaging(Buffer buffer,Call leaf){Check();Require(!Finished,"Operation already finished.");Register(buffer,leaf);}
            internal Wire Resolve(object stream,Call leaf)
            {_=ResolveBuffer(stream,leaf);return Wire;}
            Buffer ResolveBuffer(object stream,Call leaf)
            {
                Check();Validate(leaf,Wire);_=RecordKind(leaf.Site);
                Require((leaf.Site!=Call.Site||leaf.Cookie!=Call.Cookie)&&leaf.Direction==Call.Direction&&leaf.Version==Call.Version,
                    "Leaf and parent identities/direction/version do not match.");
                var alias=Aliases.SingleOrDefault(x=>x.Direction==leaf.Direction&&x.Version==leaf.Version&&
                    !x.Buffer.Disposed&&x.Stream.Matches(stream)&&x.Base.Matches(Underlying(stream))&&
                    (x.LeafOwner is null||x.LeafOwner.Value==leaf));
                Require(alias is not null,"No exact stream capability; parent fallback is forbidden.");return alias!.Buffer;
            }
            internal FactoryReader BeginFactoryReader(object reader,Call exactParent)
            {
                Check();Require(!Finished&&!Root.Faulted&&Call.Direction==Direction.Read&&exactParent==Call,
                    "Factory reader requires the exact active read operation stamp.");
                Require(FactoryWindow is null&&BuildFactoryWindow is null,"Reader window already open.");
                var alias=Aliases.SingleOrDefault(x=>x.LeafOwner is null&&!x.Buffer.Disposed&&x.Direction==Direction.Read&&
                    x.Version==Call.Version&&x.Stream.Matches(reader)&&x.Base.Matches(Underlying(reader)));
                Require(alias is not null,"Factory reader is not an exact owned parent alias.");
                var window=new FactoryReader(this,reader);FactoryWindow=window;return window;
            }
            internal Leaf BeginLeaf(object value,object stream,Call leaf,FactoryTicket? factory=null)
            {
                Check();Leaf? result=null;
                try
                {
                    Require(!Finished&&!Root.Faulted&&BuildFactoryWindow is null&&!Root.BuildDrafts.Any(x=>!x.IsClosed),"Outer operation already finished, failed, or has an active Build leaf.");var buffer=ResolveBuffer(stream,leaf);
                    Kind kind=RecordKind(leaf.Site);
                    var carrier=factory is null?Trial.RequireCarrier(value,kind):Trial.ClaimFactory(factory,value,kind,this,stream,leaf);
                    Require(factory is not null||!carrier.FromFactory||leaf.Direction==Direction.Write,
                        "Factory receiver read requires its one-use positive return ticket.");
                    Require(Root.Entries.Count+Root.BuildDrafts.Count<MaxEntries&&!Root.Entries.Any(x=>x.Call.Site==leaf.Site&&x.Call.Cookie==leaf.Cookie),
                        "Repeated leaf cookie or journal bound reached.");
                    var entry=new Entry(this,leaf,carrier,stream,buffer);
                    lock(Gate)
                    {
                        var key=(kind,Pointer(value));Require(!Reserved.ContainsKey(key),"Carrier already reserved by an outer operation.");
                        Reserved.Add(key,entry);
                    }
                    try{Root.Entries.Add(entry);}
                    catch
                    {
                        lock(Gate)
                        {
                            var key=(kind,Pointer(value));
                            if(Reserved.TryGetValue(key,out var held)&&ReferenceEquals(held,entry))Reserved.Remove(key);
                            entry.Released=true;
                        }
                        throw;
                    }
                    if(factory is not null)
                    {
                        lock(Gate)
                        {
                            var key=(kind,Pointer(value));
                            Require(FactoryReserved.TryGetValue(key,out var held)&&ReferenceEquals(held,factory),"Factory reservation lost before leaf handoff.");
                            FactoryReserved.Remove(key);factory.Transferred=true;
                        }
                    }
                    result=new Leaf(entry);Leaves.Add(result);
                    result.Prepare();return result;
                }
                catch(Exception original)
                {
                    Root.Faulted=true;
                    try{result?.Close(1);}catch(Exception cleanup){throw new AggregateException(original,cleanup);}
                    throw;
                }
            }
            // Candidate only: Build shares the already-owned root/stream but
            // never enters Train's Kind, wire parser or factory-ticket system.
            internal void EnlistBuildDraft(IBuildLease lease,BuildReaderWindow? factory=null)
            {
                Require((factory is null&&BuildFactoryWindow is null)||(factory is not null&&ReferenceEquals(factory,BuildFactoryWindow)),"Wrong Build factory enlistment window.");
                if(factory is not null)factory.RequireBuffer(lease.Buffer,null);
                Check();Require(!Finished&&!Root.Faulted&&FactoryWindow is null&&!Root.Entries.Any(x=>!x.Closed)&&!Root.BuildDrafts.Any(x=>!x.IsClosed),"Build enlistment after completion/fault or inside Train factory window.");
                var leaf=lease.Call;object stream=lease.Buffer.Stream(leaf.Direction);
                Require(ReferenceEquals(lease.Parent,this)&&leaf.Site is 1 or 2&&leaf.Cookie!=0&&
                    leaf.Version==Call.Version&&leaf.Version==123&&leaf.Direction==Call.Direction&&
                    (leaf.Site!=Call.Site||leaf.Cookie!=Call.Cookie),"Build leaf differs from exact parent.");
                Require(Aliases.Any(a=>ReferenceEquals(a.Buffer,lease.Buffer)&&!a.Buffer.Disposed&&a.LeafOwner is null&&
                    a.Direction==leaf.Direction&&a.Version==leaf.Version&&a.Stream.Matches(stream)&&a.Base.Matches(Underlying(stream))),
                    "Build needs its exact registered owned stream; aliases are not authority.");
                Require(Root.BuildDrafts.Count+Root.Entries.Count<MaxEntries&&!Root.BuildDrafts.Any(x=>x.Call.Site==leaf.Site&&x.Call.Cookie==leaf.Cookie),
                    "Build journal capacity or repeated leaf cookie.");Root.BuildDrafts.Add(lease);
                if(factory is not null)factory.Pending=lease;
            }
            internal BuildReaderWindow BeginBuildFactoryReader(Buffer input,Call exactParent)
            {
                Check();Require(exactParent==Call&&Call.Direction==Direction.Read&&Call.Version==123,"Build factory requires exact 123 read parent.");
                RequireBuildBuffer(input);var window=new BuildReaderWindow(this,input);BuildFactoryWindow=window;return window;
            }
            internal void LeafClosed(Leaf leaf)=>Leaves.Remove(leaf);
            internal void Finish()
            {Check();Require(Leaves.Count==0&&FactoryWindow is null&&BuildFactoryWindow is null&&!Root.Faulted&&Root.BuildDrafts.Where(x=>ReferenceEquals(x.Parent,this)).All(x=>x.IsClosed),"Cannot finish with active/failed leaves or reader window.");Finished=true;}
            internal void Close(int outcome)
            {
                if(Closed)return;Check();Require(Leaves.Count==0&&FactoryWindow is null&&BuildFactoryWindow is null&&Root.BuildDrafts.Where(x=>ReferenceEquals(x.Parent,this)).All(x=>x.IsClosed),"Native leaf and reader-window cleanup must precede parent cleanup.");
                if(outcome!=0||!Finished)Root.Faulted=true;
                Closed=true;Aliases.Clear();Trial.Active.Remove(this);
                var errors=new List<Exception>();
                try
                {
                    if(Parent is null)
                    {
                        Root.Closing=true;
                        foreach(var build in Root.BuildDrafts)All(errors,build.BeforeRootDecision);
                        // Buffer cleanup is part of the outcome, before deciding
                        // whether completed read attachments may survive.
                        foreach(var buffer in Root.Buffers)All(errors,buffer.Dispose);
                        if(errors.Count!=0)Root.Faulted=true;
                        if(Root.Faulted)
                            for(int i=Root.BuildDrafts.Count-1;i>=0;i--)All(errors,Root.BuildDrafts[i].RollbackPublished);
                        if(Root.Faulted)
                            for(int i=Root.Entries.Count-1;i>=0;i--)
                            {var entry=Root.Entries[i];All(errors,()=>Permitted(entry,true,()=>entry.Read?.RollbackPublished()));}
                        foreach(var entry in Root.Entries)
                        {
                            All(errors,()=>Permitted(entry,true,()=>entry.Read?.Dispose()));
                            lock(Gate)
                            {
                                var key=(entry.Carrier.Kind,entry.Carrier.Identity.Key);
                                if(Reserved.TryGetValue(key,out var held)&&ReferenceEquals(held,entry))Reserved.Remove(key);
                                entry.Released=true;
                            }
                        }
                        lock(Gate)
                        {
                            foreach(var ticket in Root.FactoryTickets)
                            {
                                var key=(ticket.Kind,ticket.Identity.Key);
                                if(FactoryReserved.TryGetValue(key,out var held)&&ReferenceEquals(held,ticket))FactoryReserved.Remove(key);
                                ticket.Retired=true;
                                if(!ticket.Transferred)All(errors,()=>RetireDispatch(ticket.Identity));
                            }
                        }
                        foreach(var build in Root.BuildDrafts)build.Release();
                        Root.BuildDrafts.Clear();Root.FactoryTickets.Clear();Root.Entries.Clear();Root.Buffers.Clear();Root.Closed=true;
                    }
                }
                finally{Current=Parent;}
                Throw(errors);
            }
            public void Dispose()=>Close(Finished?0:1);
        }
        // The future typed outer/leaf adapter creates this only after its own
        // parent state/receiver policy has admitted the enclosing operation.
        // This window proves exact stream/cookie/thread association, not that
        // an arbitrary parent GameState is unpublished or safe to deserialize.
        // Separate Build stream capability. Train Kind, tickets and discriminator remain unchanged.
        internal sealed class BuildReaderWindow
        {
            internal readonly Scope Parent;internal readonly Buffer Buffer;internal readonly Call Stamp;
            internal readonly WeakIdentity Reader,Base;internal IBuildLease? Pending;
            bool Closed;
            internal BuildReaderWindow(Scope parent,Buffer buffer)
            {Parent=parent;Buffer=buffer;Stamp=parent.Call;object reader=buffer.Stream(Direction.Read);Reader=new(reader);Base=new(Underlying(reader));}
            internal void Check(bool cleanup=false)
            {
                Parent.Check();Require(!Closed&&ReferenceEquals(Parent.BuildFactoryWindow,this)&&Parent.Call==Stamp,"Stale Build factory stream window.");
                if(!cleanup)Require(!Parent.Root.Faulted,"Build factory root failed.");
            }
            internal void RequireBuffer(Buffer buffer,IBuildLease? pending)
            {
                Check();Require(ReferenceEquals(buffer,Buffer)&&ReferenceEquals(Pending,pending)&&!buffer.Disposed&&
                    Reader.Matches(buffer.Stream(Direction.Read))&&Base.Matches(Underlying(buffer.Stream(Direction.Read))),"Build window buffer or pending lease differs.");
            }
            internal long Position()
            {
                Check();object target=Base.Target()??throw new InvalidDataException("Build factory stream expired.");object? stream=null;
                try{stream=Native.As(target,"Il2CppSystem.IO.Stream");Require(stream is not null&&Pointer(stream)==Base.Key&&Native.Same(target,stream),"Build factory stream cast changed identity.");return Convert.ToInt64(Native.Get(stream!,"Position"));}
                finally{GC.KeepAlive(stream);GC.KeepAlive(target);}
            }
            internal void Close(bool completed)
            {
                if(Closed)return;Check(true);Require(Pending is null||Pending.IsClosed,"Build factory leaf cleanup must precede window cleanup.");
                if(!completed)Parent.Root.Faulted=true;Closed=true;Parent.BuildFactoryWindow=null;
            }
        }
        internal sealed class FactoryReader:IDisposable
        {
            internal readonly Scope Parent;internal readonly Call Stamp;
            internal readonly WeakIdentity Reader,Base;
            internal readonly List<FactoryTicket> Tickets=new();
            FactoryCall? Active;bool Finished,Closed;
            internal bool FactoryActive=>Active is not null;
            internal FactoryReader(Scope parent,object reader)
            {Parent=parent;Stamp=parent.Call;Reader=new(reader);Base=new(Underlying(reader));}
            internal long Position()
            {
                Check();
                object target=Base.Target()??throw new InvalidDataException("Factory base stream expired.");
                object? stream=null;
                try
                {
                    // Native WeakReference.Target returns an Object wrapper,
                    // even when the native target is a MemoryStream. Cast that
                    // live target, never reconstruct it from its saved address.
                    stream=Native.As(target,"Il2CppSystem.IO.Stream");
                    Require(stream is not null&&Pointer(stream)==Base.Key&&Native.Same(target,stream),
                        "Factory base stream cast changed native identity.");
                    return Convert.ToInt64(Native.Get(stream!,"Position"));
                }
                finally{GC.KeepAlive(stream);GC.KeepAlive(target);}
            }
            internal void Check(bool cleanup=false)
            {
                Parent.Check();Require(!Closed&&ReferenceEquals(Parent.FactoryWindow,this)&&Stamp==Parent.Call,
                    "Factory reader window is stale or not current.");
                if(!cleanup)Require(!Finished&&!Parent.Root.Faulted,"Factory reader already finished or its root failed.");
            }
            internal FactoryCall BeginFactory(Kind kind,ushort nativeDiscriminator)
            {
                Check();Require(nativeDiscriminator==5&&kind is Kind.Command or Kind.Action,
                    "Only audited native Train command/action factories are admitted.");
                Require(Active is null&&!Tickets.Any(t=>!t.Consumed),"Previous factory return is still awaiting its first leaf.");
                var call=new FactoryCall(this,kind);Active=call;return call;
            }
            internal void RequireCall(FactoryCall call)
            {Check();Require(ReferenceEquals(Active,call),"Factory callback does not match the active typed call.");}
            internal void ReleaseCall(FactoryCall call,bool failed)
            {Check(true);Require(ReferenceEquals(Active,call),"Factory cleanup does not match its call.");Active=null;if(failed)Parent.Root.Faulted=true;}
            internal void Finish()
            {Check();Require(Active is null&&Tickets.All(t=>t.Consumed&&t.Transferred),"Factory return did not complete its first leaf admission.");Finished=true;}
            internal void Close(int outcome)
            {
                if(Closed)return;Check(true);Require(Active is null,"Factory native call cleanup must precede reader cleanup.");
                if(outcome!=0||!Finished)Parent.Root.Faulted=true;
                Closed=true;Parent.FactoryWindow=null;
                // Pending receiver barriers stay in the ROOT journal until
                // outer cleanup, even if this nested reader has already failed.
            }
            public void Dispose()=>Close(Finished?0:1);
        }
        internal sealed class FactoryCall:IDisposable
        {
            readonly FactoryReader Reader;readonly Kind Kind;bool ReturnedOnce,Closed;
            internal FactoryCall(FactoryReader reader,Kind kind){Reader=reader;Kind=kind;}
            // Invoke ONLY from the future successful typed native factory-return
            // seam. No caller-supplied "is private" predicate is accepted here.
            // The mock suite cannot prove that this callback came from native.
            internal FactoryTicket Returned(object value)
            {
                Reader.RequireCall(this);
                try
                {
                    Require(!Closed&&!ReturnedOnce,"Factory return was already observed.");
                    ReturnedOnce=true;value=Typed(value,Kind);Reader.Parent.Trial.CheckFactoryCandidate(value,Kind);
                    var ticket=new FactoryTicket(Reader,value,Kind);
                    lock(Gate)
                    {
                        foreach(long key in CarrierSeen.Where(p=>p.Value.Target() is null).Select(p=>p.Key).ToArray())CarrierSeen.Remove(key);
                        long pointer=Pointer(value);
                        Require(!CarrierSeen.ContainsKey(pointer)&&!Reserved.ContainsKey((Kind,pointer))&&!FactoryReserved.ContainsKey((Kind,pointer)),
                            "Factory returned an already-witnessed or reserved native identity.");
                        Require(CarrierSeen.Count<MaxStreams&&Reader.Parent.Root.FactoryTickets.Count<MaxEntries,
                            "Factory admission capacity reached.");
                        CarrierSeen.Add(pointer,ticket.Identity);
                        RememberDispatch(value,DispatchOwnership.PrivateCarrier);
                        try
                        {
                            FactoryReserved.Add((Kind,pointer),ticket);
                            Reader.Parent.Root.FactoryTickets.Add(ticket);Reader.Tickets.Add(ticket);
                        }
                        catch
                        {
                            FactoryReserved.Remove((Kind,pointer));
                            Reader.Parent.Root.FactoryTickets.Remove(ticket);Reader.Tickets.Remove(ticket);ticket.Retired=true;
                            // Keep the conservative weak tombstone if publication fails.
                            throw;
                        }
                    }
                    return ticket;
                }
                catch{Reader.Parent.Root.Faulted=true;throw;}
            }
            internal void Close(int outcome)
            {
                if(Closed)return;Reader.ReleaseCall(this,outcome!=0||!ReturnedOnce);Closed=true;
            }
            public void Dispose()=>Close(ReturnedOnce?0:1);
        }
        internal sealed class FactoryTicket
        {
            internal readonly FactoryReader Reader;internal readonly Kind Kind;internal readonly WeakIdentity Identity;
            internal readonly long Position;
            internal bool Consumed,Transferred,Retired;
            internal FactoryTicket(FactoryReader reader,object value,Kind kind)
            {Reader=reader;Kind=kind;Identity=new(value);Position=reader.Position();}
            internal void RequireClaim(object value,Kind kind,Scope scope,object stream,Call leaf)
            {
                Reader.Check();
                Require(!Consumed&&!Retired&&!Reader.FactoryActive&&ReferenceEquals(scope,Reader.Parent)&&leaf.Direction==Direction.Read&&
                    leaf.Version==Reader.Stamp.Version&&kind==Kind&&RecordKind(leaf.Site)==kind&&
                    Reader.Reader.Matches(stream)&&Reader.Base.Matches(Underlying(stream))&&Identity.Matches(value)&&
                    Convert.ToInt64(Native.Get(Underlying(stream),"Position"))==Position,
                    "Factory ticket does not match this first leaf receiver/reader/version/kind.");
                lock(Gate)Require(FactoryReserved.TryGetValue((kind,Identity.Key),out var held)&&ReferenceEquals(held,this),
                    "Factory ticket has no exclusive pending receiver barrier.");
            }
        }
        internal sealed class Entry
        {
            internal readonly Scope Parent;internal readonly Root Root;internal readonly Call Call;
            internal readonly Carrier Carrier;internal readonly WeakIdentity Stream;
            internal WeakIdentity NativeStream;internal Buffer NativeBuffer;internal bool HasReplacement;
            internal NativePaidBirthCommands.ReadRecord? Read;internal NativePaidBirthCommands.WriteRecord? Write;
            internal bool Released,Finished,Closed,OriginalAdmitted;
            internal Entry(Scope parent,Call call,Carrier carrier,object stream,Buffer buffer)
            {Parent=parent;Root=parent.Root;Call=call;Carrier=carrier;Stream=new(stream);NativeStream=Stream;NativeBuffer=buffer;}
            internal void Admission(object value,Kind kind)=>RequireToken(value,kind,false);
            internal void CleanupAdmission(object value,Kind kind)=>RequireToken(value,kind,true);
            void RequireToken(object value,Kind kind,bool allowFailed)
            {
                Require(!Released&&!Closed&&ReferenceEquals(Current,Parent)&&Root.Thread==Environment.CurrentManagedThreadId&&
                    (allowFailed||!Root.Faulted)&&kind==Carrier.Kind&&Carrier.Identity.Matches(value),"Private receiver token is not current or its root failed.");
                Parent.Trial.RequireCarrier(value,kind);
                lock(Gate)Require(Reserved.TryGetValue((kind,Carrier.Identity.Key),out var held)&&ReferenceEquals(held,this),"Exclusive carrier reservation is absent.");
            }
        }
        internal sealed class Leaf:IDisposable
        {
            readonly Entry Entry;internal Leaf(Entry entry){Entry=entry;}
            internal void Prepare()
            {
                var e=Entry;e.Admission(e.Carrier.Value,e.Carrier.Kind);
                if(e.Call.Direction==Direction.Read)
                    Permitted(e,false,()=>e.Read=new(e.Carrier.Value,e.Carrier.Kind,e.Parent.Wire,e.Call.Version,e.Admission));
                else e.Write=new(e.Carrier.Value,e.Carrier.Kind,e.Parent.Wire,e.Call.Version);
            }
            internal void RegisterReplacement(Buffer buffer)
            {
                var e=Entry;e.Admission(e.Carrier.Value,e.Carrier.Kind);
                Require(!e.Finished&&!e.HasReplacement,"Repeated/late native replacement stream.");
                try
                {
                    e.Parent.RegisterLeafStaging(buffer,e.Call);
                    e.NativeStream=new(buffer.Stream(e.Call.Direction));e.NativeBuffer=buffer;e.HasReplacement=true;
                }
                catch{e.Root.Faulted=true;throw;}
            }
            // Call immediately before handing the exact receiver/stream to the
            // original native serializer. No native original is invoked here.
            internal void RequireOriginal(object value,object stream,Call call)
            {
                var e=Entry;e.Admission(value,e.Carrier.Kind);
                Require(!e.OriginalAdmitted,"Native original was already admitted for this leaf.");
                Require(call==e.Call&&e.NativeStream.Matches(stream),"Native leaf call changed after Prepare.");
                Require(call.Direction==Direction.Read||e.HasReplacement,"Writer must use its owned staging replacement.");
                _=e.Parent.Resolve(stream,call);
                e.OriginalAdmitted=true;
            }
            internal void FinishRead()
            {
                var e=Entry;e.Admission(e.Carrier.Value,e.Carrier.Kind);Require(e.Read is not null&&!e.Finished&&e.OriginalAdmitted,"Wrong/repeated/unadmitted read finish.");
                try
                {
                    Require(e.NativeStream.Matches(e.NativeBuffer.Stream(Direction.Read)),"Read stream changed before Finish.");
                    Permitted(e,false,()=>{e.Read!.Finish(e.NativeBuffer.Native.ReadExact);e.Read.Accept();});e.Finished=true;
                }
                catch{e.Root.Faulted=true;throw;}
            }
            internal byte[] FinishWrite()
            {
                var e=Entry;e.Admission(e.Carrier.Value,e.Carrier.Kind);Require(e.Write is not null&&!e.Finished&&e.OriginalAdmitted,"Wrong/repeated/unadmitted write finish.");
                try{byte[] bytes=e.Write!.Finish();e.Finished=true;return bytes;}
                catch{e.Root.Faulted=true;throw;}
            }
            internal void Close(int outcome)
            {
                var e=Entry;if(e.Closed)return;e.CleanupAdmission(e.Carrier.Value,e.Carrier.Kind);
                if(outcome!=0||!e.Finished)e.Root.Faulted=true;
                try{Permitted(e,false,()=>e.Read?.Dispose());}
                catch{e.Root.Faulted=true;throw;}
                finally{e.Closed=true;e.Parent.LeafClosed(this);}
                // Reservation and accepted ReadRecord deliberately remain in
                // the ROOT journal until the outermost outcome is known.
            }
            public void Dispose()=>Close(Entry.Finished?0:1);
        }
        internal static int ActiveReservations {get{lock(Gate)return Reserved.Count+FactoryReserved.Count;}}
        internal static int CurrentDepth {get{int count=0;for(var scope=Current;scope is not null;scope=scope.Parent)count++;return count;}}
        internal static bool HasMutationPermit=>MutationPermit is not null||CleanupPermit;
    }
}
