using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Deception.PolyModAdapter;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using J=Deception.PolyModAdapter.NativeBuildRecordJournalDraft;
using A=Deception.PolyModAdapter.NativeBuildLeafProbeAdapter;
using D=Deception.PolyModAdapter.NativeBuildLeafDispatcher;
using B=Deception.PolyModAdapter.NativeStateBridge;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
namespace Deception.PolyModAdapter
{
    // POD callback/clock stand-ins only. The real adapter/router/journal run.
    internal static class NativeStateBridge
    {
        internal struct Call {internal uint Size;internal int Operation,Version;internal uint Site;internal IntPtr State,Stream,MethodInfo;}
        internal struct Plan {internal ulong Cookie;internal IntPtr Replacement;}
    }
    internal static class NativeTrainLeafProbeAdapter
    {static long Next;internal static int CurrentDepth=>0;internal static ulong NewCookie()=>checked((ulong)System.Threading.Interlocked.Increment(ref Next));}
}
namespace Deception.Tests
{
    public static class NativeBuildLeafAdapterTests
    {
        static readonly List<object> Results=new();static int Failed;
        static void Check(bool x){if(!x)throw new Exception("Build adapter invariant failed.");}
        static void Reject(Action action){try{action();}catch(InvalidDataException){return;}catch(AggregateException){return;}throw new Exception("Expected rejection.");}
        static void Test(string id,Action action)
        {
            try{action();Check(A.CurrentDepth==0&&C.CurrentDepth==0&&J.ActiveReservations==0&&C.ActiveReservations==0);Results.Add(new{id,status="PASS"});}
            catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});}
        }
        static C.Call Parent(C.Direction d)=>new(0,NativeTrainLeafProbeAdapter.NewCookie(),123,d);
        static B.Call Call(object value,object stream,int op=0,int version=123)=>new(){Size=40,Site=value is BuildAction?2u:1u,Operation=op,Version=version,State=new(Native.Pointer(value)),Stream=new(Native.Pointer(stream)),MethodInfo=new(9876)};
        static byte[] Payload(){using var m=new MemoryStream();using var w=new BinaryWriter(m);w.Write(MimicSmuggleActions.PayloadMagic);w.Write((byte)1);w.Write((uint)42);return m.ToArray();}
        static J.Carrier Smuggle(J.Trial t){var c=t.NewCommand();t.Mutate(c,v=>{((BuildCommand)v).Type=BuildType.Smuggle;MimicSmuggleActions.PublishPrivateJournal(v,42);});return c;}
        static void Read(C.Scope scope,J.Carrier c,C.Buffer buffer,bool malformed=false)
        {
            using var invoke=A.Open(scope,c,buffer,C.Direction.Read);var call=Call(c.Value,buffer.Stream(C.Direction.Read),1);var plan=new B.Plan();
            Check(D.Prepare(call,c.Value,buffer.Stream(C.Direction.Read),ref plan)==0&&plan.Cookie!=0);
            ((BuildCommand)c.Value).Type=BuildType.Smuggle;
            if(malformed){Reject(()=>D.Finish(call,plan));D.Cleanup(call,plan,3);Reject(invoke.RequireCompleted);}
            else{D.Finish(call,plan);D.Cleanup(call,plan,0);invoke.RequireCompleted();}
        }
        public static int Run()
        {
            Test("staged-writer-publishes-only-after-finish",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=Smuggle(b);var dest=t.NewBuffer();
                using var root=t.Begin(dest,Parent(C.Direction.Write),P.Wire.DcpbV1);
                using(var invoke=A.Open(root,c,dest,C.Direction.Write))
                {
                    var call=Call(c.Value,dest.Stream(C.Direction.Write));var plan=new B.Plan();Check(D.Prepare(call,c.Value,dest.Stream(C.Direction.Write),ref plan)==1);
                    var stage=ContextWriter.All[plan.Replacement.ToInt64()];stage.BaseStream.Bytes.WriteByte(99);Check(dest.Native.Length==0);
                    D.Finish(call,plan);Check(dest.Snapshot().SequenceEqual(new byte[]{99}.Concat(Payload())));D.Cleanup(call,plan,0);invoke.RequireCompleted();
                }
                root.Finish();
            });
            Test("reader-binding-survives-successful-root",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewAction();var buffer=t.NewBuffer(Payload());
                using(var root=t.Begin(buffer,Parent(C.Direction.Read),P.Wire.DcpbV1)){Read(root,c,buffer);root.Finish();}
                Check(MimicSmuggleActions.HasPayload(c.Value));
            });
            Test("later-read-failure-rolls-back-earlier-attachment",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var first=b.NewCommand();var bad=b.NewAction();var buffer=t.NewBuffer(Payload().Concat(new byte[9]).ToArray());
                using(var root=t.Begin(buffer,Parent(C.Direction.Read),P.Wire.DcpbV1)){Read(root,first,buffer);Read(root,bad,buffer,true);}
                Check(!MimicSmuggleActions.HasPayload(first.Value)&&!MimicSmuggleActions.HasPayload(bad.Value));
            });
            Test("native-failure-before-finish-leaves-destination-empty",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=Smuggle(b);var dest=t.NewBuffer();using var root=t.Begin(dest,Parent(C.Direction.Write),P.Wire.DcpbV1);
                using var invoke=A.Open(root,c,dest,C.Direction.Write);var call=Call(c.Value,dest.Stream(C.Direction.Write));var plan=new B.Plan();D.Prepare(call,c.Value,dest.Stream(C.Direction.Write),ref plan);
                ContextWriter.All[plan.Replacement.ToInt64()].BaseStream.Bytes.WriteByte(88);D.Cleanup(call,plan,4);Reject(invoke.RequireCompleted);Check(dest.Native.Length==0&&MimicSmuggleActions.HasPayload(c.Value));
            });
            Test("partial-prepare-keeps-cookie-and-cleans-once",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=Smuggle(b);var dest=t.NewBuffer();using var root=t.Begin(dest,Parent(C.Direction.Read),P.Wire.DcpbV1);
                using var invoke=A.Open(root,c,dest,C.Direction.Read);var call=Call(c.Value,dest.Stream(C.Direction.Read),1);var plan=new B.Plan();Reject(()=>D.Prepare(call,c.Value,dest.Stream(C.Direction.Read),ref plan));Check(plan.Cookie!=0);
                D.Cleanup(call,plan,1);Reject(()=>D.Cleanup(call,plan,1));Check(MimicSmuggleActions.HasPayload(c.Value));
            });
            Test("known-unscoped-carrier-denied-before-stream-read",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var stream=new ContextReader(new ContextStream(Array.Empty<byte>()));var call=Call(c.Value,stream,1);var plan=new B.Plan();Reject(()=>D.Prepare(call,c.Value,stream,ref plan));Check(plan.Cookie==0&&stream.BaseStream.Position==0);
            });
            Test("owned-stream-and-base-alias-denied-with-unrelated-carrier",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());var buffer=t.NewBuffer();var value=new BuildCommand();var stream=buffer.Stream(C.Direction.Write);var plan=new B.Plan();Reject(()=>D.Prepare(Call(value,stream),value,stream,ref plan));
                var alias=new ContextWriter(((ContextWriter)stream).BaseStream);Reject(()=>D.Prepare(Call(value,alias),value,alias,ref plan));Check(buffer.Native.Length==0&&plan.Cookie==0);
            });
            Test("ordinary-unrelated-legacy-preserves-version-and-plan",()=>
            {
                var c=new BuildCommand();var stream=new ContextWriter(new ContextStream(null));var call=Call(c,stream,0,-42);var plan=new B.Plan();Check(D.Prepare(call,c,stream,ref plan)==0&&plan.Cookie==0&&plan.Replacement==IntPtr.Zero&&call.Version==-42);D.Finish(call,plan);D.Cleanup(call,plan,0);
            });
            Test("custom-unrelated-no-payload-is-not-legacy",()=>
            {var c=new BuildCommand{Type=BuildType.Smuggle};var stream=new ContextWriter(new ContextStream(null));var plan=new B.Plan();Reject(()=>D.Prepare(Call(c,stream),c,stream,ref plan));Check(plan.Cookie==0);});
            Test("off-thread-known-carrier-denied-before-weak-access",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var stream=new ContextWriter(new ContextStream(null));int reads=NativeLedgerOwnership.TargetReads;
                Task.Run(()=>{var plan=new B.Plan();Reject(()=>D.Prepare(Call(c.Value,stream),c.Value,stream,ref plan));}).GetAwaiter().GetResult();Check(NativeLedgerOwnership.TargetReads==reads);
            });
            Test("missing-native-detour-cannot-report-completion",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var dest=t.NewBuffer();using var root=t.Begin(dest,Parent(C.Direction.Write),P.Wire.DcpbV1);
                using(var invoke=A.Open(root,c,dest,C.Direction.Write))Reject(invoke.RequireCompleted);Check(root.Root.Faulted);
            });
            Test("foreign-destination-does-not-acquire-invocation",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var dest=t.NewBuffer();var foreign=t.NewBuffer();using var root=t.Begin(dest,Parent(C.Direction.Write),P.Wire.DcpbV1);
                Reject(()=>A.Open(root,c,foreign,C.Direction.Write));Check(A.CurrentDepth==0);root.Finish();
            });
            string path=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT")!;
            File.WriteAllText(path,JsonSerializer.Serialize(new{passed=Results.Count-Failed,failed=Failed,results=Results}));return Failed==0?0:1;
        }
    }
}
