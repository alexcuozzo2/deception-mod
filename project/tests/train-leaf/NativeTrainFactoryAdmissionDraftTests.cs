// SYNTHETIC CONTEXT FIXTURE ONLY. These are CLR objects, not IL2CPP objects.
// Compiled in isolation with NativeApi, the context, and patched attachment draft.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;

public class ContextObject
{static long Next=100;public IntPtr Pointer {get;set;}=new(++Next);}
public enum ContextUnitType { Warrior=1,MimicWarrior=1000,MimicRider=1001 }
public static class EnumCache<T> where T:struct,Enum
{
    public static string GetName(T value)=>Convert.ToInt32(value) switch
    {1000=>NativePaidBirthIntentCodec.Warrior,1001=>NativePaidBirthIntentCodec.Rider,_=>"warrior"};
}
public sealed class ContextCoordinates {public int X{get;set;}public int Y{get;set;}}
public class TrainCommand:ContextObject
{public ContextUnitType Type{get;set;}=ContextUnitType.Warrior;public byte PlayerId{get;set;}=1;public ContextCoordinates Coordinates{get;set;}=new(){X=4,Y=5};}
public sealed class TrainAction:TrainCommand
{public int Cost{get;set;}=3;public bool trainActionAborted{get;set;}public ContextCoordinates LookCoordinates{get;set;}=new(){X=-1,Y=-1};}
namespace Il2CppSystem.IO
{public abstract class Stream:ContextObject {public abstract long Position{get;set;}}}
public sealed class ContextStream:Il2CppSystem.IO.Stream
{public readonly MemoryStream Bytes;public override long Position {get=>Bytes.Position;set=>Bytes.Position=value;}public ContextStream(byte[]? input){Bytes=input is null?new():new(input,false);}}
// Model the native WeakReference.Target wrapper, which has no Position member.
public sealed class ContextWeakStream:ContextObject
{
    readonly ContextStream Target;internal static bool ThrowCast,ChangeIdentity;
    internal ContextWeakStream(ContextStream target){Target=target;Pointer=target.Pointer;}
    public T Cast<T>() where T:class
    {
        if(ThrowCast)throw new InvalidCastException("Synthetic native stream cast failure.");
        return (T)(object)(ChangeIdentity?new ContextStream(null):Target);
    }
}
public sealed class ContextReader:ContextObject
{public ContextStream BaseStream{get;}public ContextReader(ContextStream stream){BaseStream=stream;}}
public sealed class ContextWriter:ContextObject
{public static readonly Dictionary<long,ContextWriter> All=new();public ContextStream BaseStream{get;}public ContextWriter(ContextStream stream){if(!stream.Bytes.CanWrite)throw new InvalidOperationException("Read-only writer.");BaseStream=stream;All[Pointer.ToInt64()]=this;}}
namespace Deception.PolyModAdapter
{
    internal static class NativeLedgerOwnership
    {
        internal static long ThrowMatchOnce,ThrowTargetOnce;internal static int TargetReads;internal static readonly HashSet<long> ExpiredKeys=new();
        internal sealed class WeakIdentity
        {
            readonly WeakReference<object> Weak;internal readonly long Key;
            internal WeakIdentity(object value){Weak=new(value);Key=Native.Pointer(value);}
            internal object? Target(){System.Threading.Interlocked.Increment(ref TargetReads);if(ThrowTargetOnce==Key){ThrowTargetOnce=0;throw new InvalidOperationException("Synthetic weak target failure.");}return !ExpiredKeys.Contains(Key)&&Weak.TryGetTarget(out var value)?value is ContextStream stream?new ContextWeakStream(stream):value:null;}
            internal bool Matches(object value)
            {
                if(ThrowMatchOnce==Key){ThrowMatchOnce=0;throw new InvalidOperationException("Synthetic weak access failure.");}
                return Target() is object target&&Native.Same(target,value);
            }
        }
    }
    internal static class NativeStateProbe
    {
        internal sealed class Buffer:IDisposable
        {
            readonly ContextStream Stream;ContextReader? Reader;ContextWriter? Writer;
            internal bool FailDispose,AttemptedDispose;
            internal Buffer(byte[]? bytes=null){Stream=new(bytes);}
            internal object R=>Reader??=new(Stream);internal object W=>Writer??=new(Stream);
            internal byte[] Snapshot()=>Stream.Bytes.ToArray();
            internal void Bytes(byte[] bytes)=>Stream.Bytes.Write(bytes,0,bytes.Length);
            internal long Position=>Stream.Bytes.Position;internal long Length=>Stream.Bytes.Length;
            internal byte[] ReadExact(int length)
            {byte[] bytes=new byte[length];int count=Stream.Bytes.Read(bytes,0,length);if(count!=length)throw new InvalidDataException("Short synthetic read.");return bytes;}
            public void Dispose(){AttemptedDispose=true;Stream.Bytes.Dispose();if(FailDispose)throw new IOException("Synthetic buffer cleanup failure.");}
        }
    }
}
namespace Deception.Tests
{
    public static class NativeTrainFactoryAdmissionDraftTests
    {
        static readonly List<object> Results=new();static int Failed;
        static void Check(bool value,string why="Context invariant failed."){if(!value)throw new Exception(why);}
        static void Reject(Action action){try{action();}catch(InvalidDataException){return;}catch(AggregateException){return;}throw new Exception("Expected rejection.");}
        static void Test(string id,Action action)
        {
            try{action();Check(C.ActiveReservations==0&&C.CurrentDepth==0&&!C.HasMutationPermit,"Leaked reservation/context/permit.");Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}
            catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e);}
        }
        static NativePaidBirthIntent Intent(ulong sequence=1)=>new(new Guid("a144a273-cd26-49da-821f-1c28106a35f9"),sequence,0,1,1,1,NativePaidBirthIntentCodec.Warrior,4,5,20,3);
        static C.Call Stamp(uint site,ulong cookie,C.Direction dir=C.Direction.Read)=>new(site,cookie,123,dir);
        static void SetBase(object value)=>((TrainCommand)value).Type=ContextUnitType.MimicWarrior;
        static byte[] Payload()=>P.EncodeExtension(true,P.Wire.DcpbV1,123,NativePaidBirthIntentCodec.Encode(Intent()));
        static void Read(C.Scope scope,C.Carrier value,C.Buffer buffer,ulong cookie,byte[] bytes)
        {
            var stamp=Stamp(value.Kind==P.Kind.Command?3u:4u,cookie);object stream=buffer.Stream(C.Direction.Read);
            using var leaf=scope.BeginLeaf(value.Value,stream,stamp);
            var staged=scope.Trial.NewBuffer(bytes);leaf.RegisterReplacement(staged);
            leaf.RequireOriginal(value.Value,staged.Stream(C.Direction.Read),stamp);
            SetBase(value.Value);leaf.FinishRead();
        }
        static object Fresh(P.Kind kind)=>kind==P.Kind.Command?new TrainCommand():new TrainAction();
        static C.FactoryTicket Observe(C.FactoryReader reader,P.Kind kind,object value)
        {using var call=reader.BeginFactory(kind,5);return call.Returned(value);}
        static void FactoryRead(C.Scope scope,C.FactoryReader reader,object value,P.Kind kind,C.Buffer buffer,ulong cookie,byte[]? bytes=null)
        {
            var ticket=Observe(reader,kind,value);var stamp=Stamp(kind==P.Kind.Command?3u:4u,cookie);
            using var leaf=scope.BeginLeaf(value,buffer.Stream(C.Direction.Read),stamp,ticket);
            var staged=scope.Trial.NewBuffer(bytes??Payload());leaf.RegisterReplacement(staged);
            leaf.RequireOriginal(value,staged.Stream(C.Direction.Read),stamp);SetBase(value);leaf.FinishRead();
        }
        static void FactoryTests()
        {
            Test("factory-opaque-weak-stream-cursor-is-preserved",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(new byte[8]);
                using var scope=trial.Begin(b,Stamp(0,180),P.Wire.DcpbV1);
                var stream=((ContextReader)b.Stream(C.Direction.Read)).BaseStream;stream.Position=3;
                using var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),scope.Call);
                var ticket=Observe(reader,P.Kind.Command,Fresh(P.Kind.Command));
                Check(ticket.Position==3&&stream.Position==3,"Weak wrapper cast must not move the native cursor.");
            });
            foreach(string mode in new[]{"expired","changed-identity","failed-cast"})Test("factory-weak-stream-"+mode+"-rejects",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(new byte[8]);
                using var scope=trial.Begin(b,Stamp(0,181),P.Wire.DcpbV1);
                using var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),scope.Call);
                long key=Native.Pointer(((ContextReader)b.Stream(C.Direction.Read)).BaseStream);
                if(mode=="expired")NativeLedgerOwnership.ExpiredKeys.Add(key);
                ContextWeakStream.ChangeIdentity=mode=="changed-identity";ContextWeakStream.ThrowCast=mode=="failed-cast";
                try
                {
                    Exception? error=null;try{Observe(reader,P.Kind.Command,Fresh(P.Kind.Command));}catch(Exception e){error=e;}
                    Check(mode=="failed-cast"?error is InvalidCastException:error is InvalidDataException,"Invalid weak stream was admitted.");
                    Check(C.ActiveReservations==0,"Failed cursor stamp retained a carrier reservation.");
                }
                finally{ContextWeakStream.ChangeIdentity=false;ContextWeakStream.ThrowCast=false;NativeLedgerOwnership.ExpiredKeys.Remove(key);}
            });
            foreach(var kind in new[]{P.Kind.Command,P.Kind.Action})Test("factory-positive-"+kind+"-one-use-before-retirement",()=>
            {
                object value=Fresh(kind);using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(Payload());
                using(var scope=trial.Begin(b,Stamp(0,200),P.Wire.DcpbV1))
                {
                    using(var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,200)))
                    {
                        var ticket=Observe(reader,kind,value);Check(C.ActiveReservations==1&&P.Get(value,kind) is null);
                        Reject(()=>P.Forget(value,kind));Reject(()=>P.Set(value,kind,Intent()));
                        var stamp=Stamp(kind==P.Kind.Command?3u:4u,201);
                        using(var leaf=scope.BeginLeaf(value,b.Stream(C.Direction.Read),stamp,ticket))
                        {leaf.RequireOriginal(value,b.Stream(C.Direction.Read),stamp);SetBase(value);leaf.FinishRead();}
                        Check(ticket.Consumed&&ticket.Transferred&&P.Get(value,kind)==Intent());reader.Finish();
                    }
                    Check(C.ActiveReservations==1);Reject(()=>P.Forget(value,kind));scope.Finish();
                }
                Check(P.Get(value,kind)==Intent()&&C.ActiveReservations==0);
            });
            Test("factory-protected-native-wrapper-alias-preserves-binding",()=>
            {
                var source=new TrainCommand{Type=ContextUnitType.MimicWarrior};var alias=new TrainCommand{Pointer=source.Pointer,Type=source.Type};P.Set(source,P.Kind.Command,Intent());
                try
                {
                    using(var trial=new C.Trial(new[]{source}))
                    {var b=trial.NewBuffer(Payload());using var scope=trial.Begin(b,Stamp(0,210),P.Wire.DcpbV1);using var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,210));Reject(()=>Observe(reader,P.Kind.Command,alias));Check(C.ActiveReservations==0&&P.Get(source,P.Kind.Command)==Intent());}
                    Check(P.Get(source,P.Kind.Command)==Intent());
                }
                finally{P.Forget(source,P.Kind.Command);}
            });
            Test("factory-bound-unknown-result-never-enters-cleanup-authority",()=>
            {
                var value=new TrainAction{Type=ContextUnitType.MimicWarrior};P.Set(value,P.Kind.Action,Intent());
                try
                {
                    using(var trial=new C.Trial(Array.Empty<object>()))
                    {var b=trial.NewBuffer(Payload());using var scope=trial.Begin(b,Stamp(0,220),P.Wire.DcpbV1);using var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,220));Reject(()=>Observe(reader,P.Kind.Action,value));Check(P.Get(value,P.Kind.Action)==Intent()&&C.ActiveReservations==0);}
                    Check(P.Get(value,P.Kind.Action)==Intent());
                }
                finally{P.Forget(value,P.Kind.Action);}
            });
            Test("factory-off-thread-callback-and-mutation-cannot-touch-receiver",()=>
            {
                var value=new TrainCommand();using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(Payload());
                using var scope=trial.Begin(b,Stamp(0,230),P.Wire.DcpbV1);using var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,230));
                using(var call=reader.BeginFactory(P.Kind.Command,5))
                {System.Threading.Tasks.Task.Run(()=>Reject(()=>call.Returned(value))).GetAwaiter().GetResult();Check(C.ActiveReservations==0);call.Close(1);}
            });
            Test("factory-exact-parent-reader-stamp-and-direction-required",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(Payload());var stream=(ContextReader)b.Stream(C.Direction.Read);
                using(var scope=trial.Begin(b,Stamp(0,240),P.Wire.DcpbV1))
                {
                    Reject(()=>scope.BeginFactoryReader(new ContextReader(stream.BaseStream),Stamp(0,240)));
                    Reject(()=>scope.BeginFactoryReader(stream,Stamp(0,241)));
                    using(var reader=scope.BeginFactoryReader(stream,Stamp(0,240)))
                    {Reject(()=>reader.BeginFactory(P.Kind.Command,1));Reject(()=>reader.BeginFactory((P.Kind)99,5));reader.Finish();}
                    scope.Finish();
                }
                var w=trial.NewBuffer();using var writing=trial.Begin(w,Stamp(0,242,C.Direction.Write),P.Wire.DcpbV1);
                Reject(()=>writing.BeginFactoryReader(w.Stream(C.Direction.Write),Stamp(0,242,C.Direction.Write)));writing.Finish();
            });
            foreach(string mismatch in new[]{"reader-alias","version","kind","cursor"})Test("factory-ticket-rejects-"+mismatch+"-before-forget",()=>
            {
                var value=new TrainAction();using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(Payload());
                using var scope=trial.Begin(b,Stamp(0,250),P.Wire.DcpbV1);using var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,250));
                var ticket=Observe(reader,P.Kind.Action,value);object stream=b.Stream(C.Direction.Read);var stamp=Stamp(4,251);
                if(mismatch=="reader-alias")stream=new ContextReader(((ContextReader)stream).BaseStream);
                if(mismatch=="version")stamp=new(4,251,122,C.Direction.Read);
                if(mismatch=="kind")stamp=Stamp(3,251);
                if(mismatch=="cursor")((ContextReader)stream).BaseStream.Position=1;
                Reject(()=>scope.BeginLeaf(value,stream,stamp,ticket));Check(!ticket.Consumed&&P.Get(value,P.Kind.Action) is null&&C.ActiveReservations==1);
            });
            Test("factory-stale-window-ticket-cannot-retire-later-binding",()=>
            {
                var value=new TrainCommand();C.FactoryTicket ticket;
                using(var trial=new C.Trial(Array.Empty<object>()))
                {
                    var b=trial.NewBuffer(Payload());using var scope=trial.Begin(b,Stamp(0,260),P.Wire.DcpbV1);
                    using var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,260));ticket=Observe(reader,P.Kind.Command,value);
                }
                Check(ticket.Retired&&C.ActiveReservations==0);SetBase(value);P.Set(value,P.Kind.Command,Intent());
                try
                {
                    using(var trial=new C.Trial(Array.Empty<object>()))
                    {var b=trial.NewBuffer(Payload());using var scope=trial.Begin(b,Stamp(0,261),P.Wire.DcpbV1);Reject(()=>scope.BeginLeaf(value,b.Stream(C.Direction.Read),Stamp(3,262),ticket));Check(P.Get(value,P.Kind.Command)==Intent());}
                    Check(P.Get(value,P.Kind.Command)==Intent());
                }
                finally{P.Forget(value,P.Kind.Command);}
            });
            Test("factory-reused-ticket-fails-and-outer-rolls-back-first-read",()=>
            {
                var value=new TrainCommand();using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(Payload());
                using(var scope=trial.Begin(b,Stamp(0,270),P.Wire.DcpbV1))
                using(var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,270)))
                {
                    var ticket=Observe(reader,P.Kind.Command,value);var stamp=Stamp(3,271);
                    using(var leaf=scope.BeginLeaf(value,b.Stream(C.Direction.Read),stamp,ticket))
                    {leaf.RequireOriginal(value,b.Stream(C.Direction.Read),stamp);SetBase(value);leaf.FinishRead();}
                    Reject(()=>scope.BeginLeaf(value,b.Stream(C.Direction.Read),Stamp(3,272),ticket));Check(P.Get(value,P.Kind.Command)==Intent());
                }
                Check(P.Get(value,P.Kind.Command) is null);
            });
            Test("factory-nested-parent-failure-retires-earlier-successful-child",()=>
            {
                var one=new TrainCommand();var two=new TrainAction();using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(Payload());
                using(var outer=trial.Begin(b,Stamp(0,280),P.Wire.DcpbV1))
                {
                    using(var inner=trial.Begin(b,Stamp(0,281),P.Wire.DcpbV1,outer))
                    {using(var reader=inner.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,281))){FactoryRead(inner,reader,one,P.Kind.Command,b,282);reader.Finish();}inner.Finish();}
                    Check(C.ActiveReservations==1&&P.Get(one,P.Kind.Command)==Intent());Reject(()=>P.Forget(one,P.Kind.Command));
                    using(var reader=outer.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,280)))
                    {Reject(()=>FactoryRead(outer,reader,two,P.Kind.Action,b,283,new byte[]{1,2}));}
                }
                Check(P.Get(one,P.Kind.Command) is null&&P.Get(two,P.Kind.Action) is null);
            });
            Test("factory-read-binding-awaits-successful-outer-buffer-disposal",()=>
            {
                var value=new TrainCommand();using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(Payload());var scope=trial.Begin(b,Stamp(0,290),P.Wire.DcpbV1);
                using(var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,290))){FactoryRead(scope,reader,value,P.Kind.Command,b,291);reader.Finish();}
                scope.Finish();b.Native.FailDispose=true;Reject(()=>scope.Close(0));Check(P.Get(value,P.Kind.Command) is null&&C.ActiveReservations==0);
            });
            Test("factory-retired-native-identity-cannot-be-readmitted-by-another-trial",()=>
            {
                var value=new TrainCommand();
                using(var trial=new C.Trial(Array.Empty<object>()))
                {var b=trial.NewBuffer(Payload());using var scope=trial.Begin(b,Stamp(0,300),P.Wire.DcpbV1);using var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,300));_=Observe(reader,P.Kind.Command,value);}
                using(var trial=new C.Trial(Array.Empty<object>()))
                {var b=trial.NewBuffer(Payload());using var scope=trial.Begin(b,Stamp(0,301),P.Wire.DcpbV1);using var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,301));Reject(()=>Observe(reader,P.Kind.Command,new TrainCommand{Pointer=value.Pointer}));Check(C.ActiveReservations==0);}
                GC.KeepAlive(value);
            });
            Test("factory-native-failure-without-return-cleans-the-reader-window",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(Payload());using var scope=trial.Begin(b,Stamp(0,310),P.Wire.DcpbV1);
                using var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,310));using var call=reader.BeginFactory(P.Kind.Command,5);
                call.Close(1);Reject(reader.Finish);Check(C.ActiveReservations==0);
            });
            Test("factory-cannot-claim-another-trials-known-constructor-receiver",()=>
            {
                using var owner=new C.Trial(Array.Empty<object>());var carrier=owner.NewCarrier(P.Kind.Command);
                using(var trial=new C.Trial(Array.Empty<object>()))
                {
                    var b=trial.NewBuffer(Payload());using var scope=trial.Begin(b,Stamp(0,320),P.Wire.DcpbV1);
                    using var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),Stamp(0,320));
                    Reject(()=>Observe(reader,P.Kind.Command,carrier.Value));Check(P.Get(carrier.Value,carrier.Kind) is null&&C.ActiveReservations==0);
                }
                owner.Mutate(carrier,SetBase);P.Set(carrier.Value,carrier.Kind,Intent());Check(P.Get(carrier.Value,carrier.Kind)==Intent());
            });
        }
        public static int Run()
        {
            Test("context-exact-alias-nested-legacy-and-retirement",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var buffer=trial.NewBuffer(new byte[]{0});object stream=buffer.Stream(C.Direction.Read);
                using(var outer=trial.Begin(buffer,Stamp(0,1),P.Wire.DcpbV1))
                {
                    Check(outer.Resolve(stream,Stamp(3,2))==P.Wire.DcpbV1);
                    using(var inner=trial.Begin(buffer,Stamp(0,3),P.Wire.LegacyNative,outer))
                    {Check(inner.Resolve(stream,Stamp(3,4))==P.Wire.LegacyNative);Reject(()=>outer.Resolve(stream,Stamp(3,5)));inner.Finish();}
                    Check(outer.Resolve(stream,Stamp(3,6))==P.Wire.DcpbV1);
                    var unknown=new ContextReader(new ContextStream(null));Reject(()=>outer.Resolve(unknown,Stamp(3,7)));
                    Check(C.UnscopedMode(unknown)==P.Wire.LegacyNative);outer.Finish();
                }
                Reject(()=>C.UnscopedMode(stream));Reject(()=>C.UnscopedMode(new ContextReader(((ContextReader)stream).BaseStream)));
            });
            Test("context-source-alias-rejected-before-binding-retirement",()=>
            {
                var source=new TrainCommand{Type=ContextUnitType.MimicWarrior};P.Set(source,P.Kind.Command,Intent());
                using(var trial=new C.Trial(new[]{source}))
                {var b=trial.NewBuffer(new byte[]{0});using var scope=trial.Begin(b,Stamp(0,10),P.Wire.DcpbV1);Reject(()=>scope.BeginLeaf(source,b.Stream(C.Direction.Read),Stamp(3,11)));Check(P.Get(source,P.Kind.Command)==Intent());}
                Check(P.Get(source,P.Kind.Command)==Intent());P.Forget(source,P.Kind.Command);
            });
            Test("context-accepted-leaf-exclusive-until-root-accept",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var carrier=trial.NewCarrier(P.Kind.Command);var b=trial.NewBuffer(Payload());
                using(var scope=trial.Begin(b,Stamp(0,20),P.Wire.DcpbV1))
                {
                    Read(scope,carrier,b,21,Payload());Check(P.Get(carrier.Value,carrier.Kind)==Intent());Check(C.ActiveReservations==1);
                    Reject(()=>P.Forget(carrier.Value,carrier.Kind));Reject(()=>P.Set(carrier.Value,carrier.Kind,Intent()));
                    System.Threading.Tasks.Task.Run(()=>Reject(()=>P.Forget(carrier.Value,carrier.Kind))).GetAwaiter().GetResult();
                    Reject(()=>trial.Mutate(carrier,SetBase));scope.Finish();
                }
                Check(C.ActiveReservations==0&&P.Get(carrier.Value,carrier.Kind)==Intent());P.Forget(carrier.Value,carrier.Kind);
            });
            Test("context-later-sibling-failure-rolls-back-earlier-leaf",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var one=trial.NewCarrier(P.Kind.Command);var two=trial.NewCarrier(P.Kind.Action);var b=trial.NewBuffer(Payload());
                using(var scope=trial.Begin(b,Stamp(0,30),P.Wire.DcpbV1))
                {Read(scope,one,b,31,Payload());Reject(()=>Read(scope,two,b,32,new byte[]{1,2}));Check(C.ActiveReservations==2);}
                Check(P.Get(one.Value,one.Kind) is null&&P.Get(two.Value,two.Kind) is null);
            });
            Test("context-successful-nested-journal-retains-outer-token",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var one=trial.NewCarrier(P.Kind.Command);var b=trial.NewBuffer(Payload());
                using(var outer=trial.Begin(b,Stamp(0,40),P.Wire.DcpbV1))
                {
                    using(var inner=trial.Begin(b,Stamp(0,41),P.Wire.DcpbV1,outer)){Read(inner,one,b,42,Payload());inner.Finish();}
                    Check(P.Get(one.Value,one.Kind)==Intent()&&C.ActiveReservations==1);Reject(()=>P.Forget(one.Value,one.Kind));
                    // No outer Finish: native outer failure must retire inner success.
                }
                Check(P.Get(one.Value,one.Kind) is null);
            });
            Test("context-staging-alias-is-exact-leaf-owned",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var c=trial.NewCarrier(P.Kind.Command);trial.Mutate(c,SetBase);P.Set(c.Value,c.Kind,Intent());
                var b=trial.NewBuffer();var stage=trial.NewBuffer();using(var scope=trial.Begin(b,Stamp(0,50,C.Direction.Write),P.Wire.DcpbV1))
                {
                    var call=Stamp(3,51,C.Direction.Write);using(var leaf=scope.BeginLeaf(c.Value,b.Stream(C.Direction.Write),call))
                    {
                        Reject(()=>leaf.RequireOriginal(c.Value,b.Stream(C.Direction.Write),call));leaf.RegisterReplacement(stage);
                        Reject(()=>scope.Resolve(stage.Stream(C.Direction.Write),Stamp(3,52,C.Direction.Write)));
                        leaf.RequireOriginal(c.Value,stage.Stream(C.Direction.Write),call);Check(leaf.FinishWrite().SequenceEqual(Payload()));
                    }
                    scope.Finish();
                }
                Check(P.Get(c.Value,c.Kind)==Intent());
            });
            Test("context-buffer-cleanup-failure-rolls-back-all-reads",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var c=trial.NewCarrier(P.Kind.Command);var b=trial.NewBuffer(Payload());var extra=trial.NewBuffer(Payload());
                var scope=trial.Begin(b,Stamp(0,60),P.Wire.DcpbV1);scope.RegisterStaging(extra);Read(scope,c,b,61,Payload());scope.Finish();b.Native.FailDispose=true;
                Reject(()=>scope.Close(0));Check(b.Native.AttemptedDispose&&extra.Native.AttemptedDispose);Check(P.Get(c.Value,c.Kind) is null);
            });
            Test("context-rollback-failure-attempts-others-and-releases-tokens",()=>
            {
                var trial=new C.Trial(Array.Empty<object>());var one=trial.NewCarrier(P.Kind.Command);var two=trial.NewCarrier(P.Kind.Action);var b=trial.NewBuffer(Payload());
                var scope=trial.Begin(b,Stamp(0,70),P.Wire.DcpbV1);Read(scope,one,b,71,Payload());Read(scope,two,b,72,Payload());
                NativeLedgerOwnership.ThrowMatchOnce=two.Identity.Key;Reject(()=>scope.Close(1));
                Check(P.Get(one.Value,one.Kind) is null&&C.ActiveReservations==0);trial.Dispose();Check(P.Get(two.Value,two.Kind) is null);
            });
            Test("context-same-carrier-reentry-rejects-and-root-rolls-back",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var c=trial.NewCarrier(P.Kind.Command);var b=trial.NewBuffer(Payload());
                using(var scope=trial.Begin(b,Stamp(0,80),P.Wire.DcpbV1))
                {Read(scope,c,b,81,Payload());Reject(()=>scope.BeginLeaf(c.Value,b.Stream(C.Direction.Read),Stamp(3,82)));Check(P.Get(c.Value,c.Kind)==Intent());}
                Check(P.Get(c.Value,c.Kind) is null);
            });
            Test("context-failed-root-stops-already-admitted-sibling",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var one=trial.NewCarrier(P.Kind.Command);var two=trial.NewCarrier(P.Kind.Action);var b=trial.NewBuffer(Payload());
                using var scope=trial.Begin(b,Stamp(0,90),P.Wire.DcpbV1);
                var first=scope.BeginLeaf(one.Value,b.Stream(C.Direction.Read),Stamp(3,91));
                var second=scope.BeginLeaf(two.Value,b.Stream(C.Direction.Read),Stamp(4,92));first.Close(1);
                Reject(()=>second.RegisterReplacement(trial.NewBuffer(Payload())));
                Reject(()=>second.RequireOriginal(two.Value,b.Stream(C.Direction.Read),Stamp(4,92)));
                Reject(second.FinishRead);second.Close(1);
                Check(P.Get(one.Value,one.Kind) is null&&P.Get(two.Value,two.Kind) is null);
            });
            foreach(var kind in new[]{P.Kind.Command,P.Kind.Action})Test("context-factory-like-new-"+kind+"-requires-positive-admission",()=>
            {
                object fresh=kind==P.Kind.Command?new TrainCommand():new TrainAction();SetBase(fresh);P.Set(fresh,kind,Intent());
                try
                {
                    using(var trial=new C.Trial(Array.Empty<object>()))
                    {
                        var b=trial.NewBuffer(Payload());using var scope=trial.Begin(b,Stamp(0,100),P.Wire.DcpbV1);
                        // Newness and absence from the protected census are not an admission ticket.
                        Reject(()=>scope.BeginLeaf(fresh,b.Stream(C.Direction.Read),Stamp(kind==P.Kind.Command?3u:4u,101)));
                        Check(P.Get(fresh,kind)==Intent(),"Unknown receiver binding retired before positive admission.");
                        Check(C.ActiveReservations==0,"Unknown factory receiver acquired a journal reservation.");
                    }
                    Check(P.Get(fresh,kind)==Intent(),"Trial cleanup claimed an unadmitted factory receiver.");
                }
                finally{P.Forget(fresh,kind);}
            });
            FactoryTests();
            string? output=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT");if(output is not null)
                File.WriteAllText(output,JsonSerializer.Serialize(new{scope="SYNTHETIC CLR IDENTITY/STREAM CONTEXT ONLY; NO NATIVE EXECUTION",failed=Failed,results=Results},new JsonSerializerOptions{WriteIndented=true}));
            return Failed==0?0:1;
        }
    }
}
