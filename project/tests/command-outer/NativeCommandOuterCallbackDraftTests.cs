// Isolated CLR/native-POD boundary proof. No game native code is executed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Deception.Core;
using Deception.PolyModAdapter;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using A=Deception.PolyModAdapter.NativeCommandOuterAdapter;
using B=Deception.PolyModAdapter.NativeCommandOuterAbi;
using R=Deception.PolyModAdapter.NativeTrainCommandRecord;
using F=Deception.Core.NativeTrainCommandFrame;
namespace Il2CppSystem {public class Object {}}
public class CommandBase:ContextObject {}
public sealed class OuterByteArray:ContextObject
{internal readonly byte[] Bytes;internal OuterByteArray(byte[] bytes){Bytes=(byte[])bytes.Clone();}}
namespace Deception.Tests
{
    public static class NativeCommandOuterCallbackDraftTests
    {
        static readonly Guid Epoch=new("a144a273-cd26-49da-821f-1c28106a35f9");
        static readonly List<object> Results=new();static int Failed,Assertions;
        static void Check(bool value,string reason="Outer callback invariant failed")
        {Assertions++;if(!value)throw new Exception(reason);}
        static void Reject(Action call)
        {try{call();}catch(Exception e)when(e is InvalidDataException or AggregateException){Assertions++;return;}throw new Exception("Expected rejection");}
        static void Test(string id,Action test)
        {
            RecordMock.Reset();
            try{test();Check(C.CurrentDepth==0&&C.ActiveReservations==0&&!C.HasMutationPermit&&A.ActiveCount==0&&NativeTrainLeafProbeAdapter.CurrentDepth==0,"Leaked invocation/journal/authority");Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}
            catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e);}
            finally{RecordMock.Reset();}
        }
        static NativePaidBirthIntent Intent()=>new(Epoch,1,0,1,1,1,NativePaidBirthIntentCodec.Warrior,4,5,20,3);
        static C.Carrier Carrier(C.Trial trial,bool bound=true)
        {var c=trial.NewCarrier(P.Kind.Command);trial.Mutate(c,v=>((TrainCommand)v).Type=bound?ContextUnitType.MimicWarrior:ContextUnitType.Warrior);if(bound)P.Set(c.Value,c.Kind,Intent());return c;}
        static byte[] Wire()
        {using var trial=new C.Trial(Array.Empty<object>());return R.Write(trial,Carrier(trial),Epoch);}
        static object ResultRoot(A.Invocation inv)=>typeof(A.Invocation).GetField("ResultRoot",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(inv)!;
        static void NoBindings()=>Check(RecordMock.FactoryResults.All(x=>P.Get(x,P.Kind.Command) is null),"Failed outer retained child attachment");
        static B.Call Call(A.Invocation inv,bool read)=>new(){Size=48,Operation=read?1u:0u,Version=read?0:123,Input=new(Native.Pointer(inv.Input)),CommandOut=read?new(911):IntPtr.Zero,VersionOut=read?new(912):IntPtr.Zero,MethodInfo=new(991)};
        // Each callback uses the actual Cdecl pointer, not a direct managed call.
        static unsafe int Prepare(IntPtr c,IntPtr p)=>((delegate* unmanaged[Cdecl]<IntPtr,IntPtr,int>)(void*)Marshal.GetFunctionPointerForDelegate(B.PrepareRoot))(c,p);
        static unsafe int Finish(IntPtr c,IntPtr p,IntPtr v,int ran)=>((delegate* unmanaged[Cdecl]<IntPtr,IntPtr,IntPtr,int,int>)(void*)Marshal.GetFunctionPointerForDelegate(B.FinishRoot))(c,p,v,ran);
        static unsafe int Cleanup(IntPtr c,IntPtr p,int outcome)=>((delegate* unmanaged[Cdecl]<IntPtr,IntPtr,int,int>)(void*)Marshal.GetFunctionPointerForDelegate(B.CleanupRoot))(c,p,outcome);
        sealed class Pod<T>:IDisposable where T:struct
        {
            readonly IntPtr Allocation;readonly int Size=Marshal.SizeOf<T>();internal readonly IntPtr Pointer;
            internal Pod(T value){Allocation=Marshal.AllocHGlobal(Size+32);Pointer=Allocation+16;for(int i=0;i<Size+32;i++)Marshal.WriteByte(Allocation,i,0xA7);Value=value;}
            internal T Value{get=>Marshal.PtrToStructure<T>(Pointer);set=>Marshal.StructureToPtr(value,Pointer,false);}
            public void Dispose(){try{for(int i=0;i<16;i++)Check(Marshal.ReadByte(Allocation,i)==0xA7&&Marshal.ReadByte(Allocation,Size+16+i)==0xA7,"POD canary overwritten");}finally{Marshal.FreeHGlobal(Allocation);}}
        }
        sealed class Harness:IDisposable
        {
            internal readonly Pod<B.Call> C;internal readonly Pod<B.Plan> P=new(default);internal readonly Pod<B.Value> V=new(default);
            internal Harness(B.Call call){C=new(call);}
            internal int Prepare()=>NativeCommandOuterCallbackDraftTests.Prepare(C.Pointer,P.Pointer);
            internal int Finish(int ran=0){V.Value=P.Value.Handled;return NativeCommandOuterCallbackDraftTests.Finish(C.Pointer,P.Pointer,V.Pointer,ran);}
            internal int Cleanup(int outcome=0)=>NativeCommandOuterCallbackDraftTests.Cleanup(C.Pointer,P.Pointer,outcome);
            public void Dispose(){V.Dispose();P.Dispose();C.Dispose();}
        }
        static void Committed(Harness h,A.Invocation inv,bool read)
        {Check(h.Prepare()==1&&h.P.Value.Cookie!=0);Check(h.Finish()==0);Check(h.Cleanup()==0);object result=ResultRoot(inv);Check(ReferenceEquals(inv.RequireReturned(result,read?123:0),result));}
        public static int Run()
        {
            Test("outer-abi-sizes-offsets-and-native-callback-pointer-types",()=>
            {
                Check(Marshal.SizeOf<B.Call>()==48&&Marshal.SizeOf<B.Value>()==16&&Marshal.SizeOf<B.Plan>()==24&&Marshal.SizeOf<B.Config>()==88);
                foreach(var item in new[]{("Size",0),("Operation",4),("Version",8),("Reserved",12),("Input",16),("CommandOut",24),("VersionOut",32),("MethodInfo",40)})Check(Marshal.OffsetOf<B.Call>(item.Item1).ToInt32()==item.Item2);
                Check(Marshal.OffsetOf<B.Plan>("Handled").ToInt32()==8&&Marshal.OffsetOf<B.Value>("Version").ToInt32()==8&&Marshal.OffsetOf<B.Value>("Boolean").ToInt32()==12);
                foreach(var d in new Delegate[]{B.PrepareRoot,B.FinishRoot,B.CleanupRoot})Check(d.GetType().GetCustomAttribute<UnmanagedFunctionPointerAttribute>()!.CallingConvention==CallingConvention.Cdecl&&Marshal.GetFunctionPointerForDelegate(d)!=IntPtr.Zero);
            });
            Test("outer-write-handled-owned-frame-and-input-attachment",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var carrier=Carrier(trial);using var inv=A.Write(trial,carrier,Epoch);using var h=new Harness(Call(inv,false));Committed(h,inv,false);
                byte[] frame=NativeStateProbe.ManagedBytes(ResultRoot(inv));Check(F.Decode(frame).Epoch==Epoch&&P.Get(carrier.Value,P.Kind.Command)==Intent());
                Check(h.P.Value.Handled.Version==0&&h.P.Value.Handled.Boolean==0&&C.ActiveReservations==0);
                Reject(()=>inv.RequireReturned(ResultRoot(inv)));
            });
            Test("outer-read-reservation-persists-prepare-finish-until-cleanup",()=>
            {
                byte[] wire=Wire();using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,wire,Epoch);using var h=new Harness(Call(inv,true));
                Check(h.Prepare()==1&&C.ActiveReservations==1&&C.CurrentDepth==1);object result=ResultRoot(inv);Check(P.Get(result,P.Kind.Command)==Intent());
                Reject(()=>P.Forget(result,P.Kind.Command));Reject(()=>P.Set(result,P.Kind.Command,Intent()));Reject(()=>inv.RequireReturned(result,123));
                Check(h.Finish()==0&&C.ActiveReservations==1);Reject(()=>P.Forget(result,P.Kind.Command));Reject(()=>inv.RequireReturned(result,123));
                Check(h.Cleanup()==0&&C.ActiveReservations==0);Check(Native.Same(inv.RequireReturned(result,123),result));
            });
            Test("outer-read-finish-result-mismatch-rolls-back-parent-binding",()=>
            {
                byte[] wire=Wire();using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,wire,Epoch);using var h=new Harness(Call(inv,true));Check(h.Prepare()==1);
                var v=h.P.Value.Handled;v.Result=new(72);h.V.Value=v;Check(Finish(h.C.Pointer,h.P.Pointer,h.V.Pointer,0)==1&&C.ActiveReservations==1);
                Check(h.Cleanup(3)==0&&C.ActiveReservations==0);NoBindings();Reject(()=>inv.RequireReturned(ResultRoot(inv),123));
            });
            Test("outer-read-cleanup-buffer-failure-retires-earlier-binding",()=>
            {
                byte[] wire=Wire();using var trial=new C.Trial(Array.Empty<object>());RecordMock.FailReadDispose=true;
                using var inv=A.Read(trial,wire,Epoch);using var h=new Harness(Call(inv,true));Check(h.Prepare()==1&&P.Get(ResultRoot(inv),P.Kind.Command)==Intent());
                Check(h.Finish()==0&&h.Cleanup()==1);Check(C.ActiveReservations==0);NoBindings();Reject(()=>inv.RequireReturned(ResultRoot(inv),123));RecordMock.FailReadDispose=false;
            });
            Test("outer-write-result-root-retained-through-native-return-ack",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Write(trial,Carrier(trial),Epoch);using var h=new Harness(Call(inv,false));Check(h.Prepare()==1);
                var weak=ObserveRoot(inv);Check(h.Finish()==0&&h.Cleanup()==0);Collect();Check(weak.TryGetTarget(out object? value)&&Native.Pointer(value!)==h.P.Value.Handled.Result.ToInt64(),"Cleanup released native return root");
                Check(ReferenceEquals(inv.RequireReturned(value!),value));GC.KeepAlive(inv);
            });
            Test("outer-unacknowledged-complete-read-is-trial-owned-until-dispose",()=>
            {
                byte[] wire=Wire();var trial=new C.Trial(Array.Empty<object>());object result;
                using(var inv=A.Read(trial,wire,Epoch)){using var h=new Harness(Call(inv,true));Check(h.Prepare()==1&&h.Finish()==0&&h.Cleanup()==0);result=ResultRoot(inv);}
                Check(P.Get(result,P.Kind.Command)==Intent()&&trial.RequireCarrier(result,P.Kind.Command).FromFactory);trial.Dispose();Check(P.Get(result,P.Kind.Command) is null);
            });
            Test("outer-missing-finish-cleanup-or-return-never-acknowledges",()=>
            {
                byte[] wire=Wire();foreach(int missing in new[]{0,1,2})
                {using var trial=new C.Trial(Array.Empty<object>());using(var inv=A.Read(trial,wire,Epoch)){using var h=new Harness(Call(inv,true));Check(h.Prepare()==1);if(missing!=0)Check(h.Finish()==0);if(missing==2)Check(h.Cleanup(3)==0);Reject(()=>inv.RequireReturned(ResultRoot(inv),123));}NoBindings();Check(C.ActiveReservations==0);}
            });
            Test("outer-malformed-frame-before-factory-retains-cleanup-cookie",()=>
            {
                byte[] wire=Wire();foreach(byte[] bad in new[]{Array.Empty<byte>(),wire[..^1],wire.Concat(new byte[]{0}).ToArray(),new byte[80]})
                {RecordMock.Reset();using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,bad,Epoch);using var h=new Harness(Call(inv,true));Check(h.Prepare()==2&&h.P.Value.Cookie!=0&&RecordMock.FactoryCalls==0);Check(h.Cleanup(1)==0&&C.CurrentDepth==0);Reject(()=>inv.RequireReturned(new TrainCommand(),123));}
            });
            Test("outer-wrong-frame-epoch-precedes-factory",()=>
            {byte[] wire=Wire();RecordMock.Reset();using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,wire,Guid.NewGuid());using var h=new Harness(Call(inv,true));Check(h.Prepare()==2&&h.P.Value.Cookie!=0&&RecordMock.FactoryCalls==0&&h.Cleanup(1)==0);});
            Test("outer-checksummed-unknown-tag-and-intent-mismatch-roll-back",()=>
            {
                byte[] wire=Wire();foreach(int field in new[]{13,5}){byte[] body=F.Decode(wire).CopyBody();body[field]=255;var bad=F.Encode(Epoch,body);RecordMock.Reset();using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,bad,Epoch);using var h=new Harness(Call(inv,true));Check(h.Prepare()==2&&h.P.Value.Cookie!=0&&RecordMock.FactoryCalls==1);Check(h.Cleanup(1)==0);NoBindings();}
            });
            Test("outer-checksummed-tail-retains-partial-prepare-cookie-and-retires-binding",()=>
            {var bad=F.Encode(Epoch,F.Decode(Wire()).CopyBody().Concat(new byte[]{99}).ToArray());using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,bad,Epoch);using var h=new Harness(Call(inv,true));Check(h.Prepare()==2&&h.P.Value.Cookie!=0&&C.ActiveReservations==1);Check(h.Cleanup(1)==0);NoBindings();});
            foreach(string mode in new[]{"factory-null","factory-throw","original-throw","missing-leaf-callback"})Test("outer-prepare-partial-cleanup-"+mode,()=>
            {
                byte[] wire=Wire();RecordMock.Reset();RecordMock.NullFactory=mode=="factory-null";RecordMock.ThrowFactory=mode=="factory-throw";RecordMock.ThrowOriginal=mode=="original-throw";RecordMock.SkipHook=mode=="missing-leaf-callback";
                using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,wire,Epoch);using var h=new Harness(Call(inv,true));Check(h.Prepare()==2&&h.P.Value.Cookie!=0&&h.Cleanup(1)==0);NoBindings();
            });
            Test("outer-private-input-mutation-after-arm-rejects-before-factory",()=>
            {byte[] wire=Wire();RecordMock.Reset();using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,wire,Epoch);((OuterByteArray)inv.Input).Bytes[^1]^=1;using var h=new Harness(Call(inv,true));Check(h.Prepare()==2&&h.P.Value.Cookie!=0&&RecordMock.FactoryCalls==0&&h.Cleanup(1)==0);});
            Test("outer-original-caller-byte-mutation-does-not-alias-armed-input",()=>
            {byte[] wire=Wire();using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,wire,Epoch);wire[^1]^=1;using var h=new Harness(Call(inv,true));Committed(h,inv,true);});
            Test("outer-exact-call-methodinfo-and-plan-identity-required",()=>
            {
                byte[] wire=Wire();foreach(string field in new[]{"method","slot","input","cookie","handled"})
                {using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,wire,Epoch);using var h=new Harness(Call(inv,true));Check(h.Prepare()==1);var oldCall=h.C.Value;var oldPlan=h.P.Value;var c=oldCall;var p=oldPlan;
                if(field=="method")c.MethodInfo=new(882);if(field=="slot")c.VersionOut=new(883);if(field=="input")c.Input=new(884);if(field=="cookie")p.Cookie++;if(field=="handled")p.Handled.Boolean=0;h.C.Value=c;h.P.Value=p;
                Check(h.Finish()==1);h.C.Value=oldCall;h.P.Value=oldPlan;Check(h.Cleanup(3)==0);NoBindings();}
            });
            Test("outer-duplicate-callbacks-denied-and-failure-cleanup-retires",()=>
            {
                byte[] wire=Wire();using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,wire,Epoch);using var h=new Harness(Call(inv,true));Check(h.Prepare()==1);Check(h.Prepare()==2);Check(h.Finish()==0&&h.Finish()==1);Check(h.Cleanup(3)==0&&h.Cleanup(3)==1);NoBindings();
            });
            Test("outer-off-thread-known-prepare-finish-cleanup-reject-before-weak-access",()=>
            {
                byte[] wire=Wire();using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,wire,Epoch);using var h=new Harness(Call(inv,true));Check(h.Prepare()==1);int before=NativeLedgerOwnership.TargetReads;
                Task.Run(()=>{using var other=new Harness(h.C.Value);Check(other.Prepare()==2);other.P.Value=h.P.Value;Check(other.Finish()==1&&other.Cleanup(3)==1);}).GetAwaiter().GetResult();
                Check(NativeLedgerOwnership.TargetReads==before&&C.ActiveReservations==1);Check(h.Cleanup(3)==0);NoBindings();
            });
            Test("outer-invalid-pod-contained-and-never-enters-record",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Write(trial,Carrier(trial),Epoch);var good=Call(inv,false);
                foreach(string field in new[]{"size","operation","reserved","version","out-slot","input"})
                {var c=good;if(field=="size")c.Size=47;if(field=="operation")c.Operation=9;if(field=="reserved")c.Reserved=1;if(field=="version")c.Version=124;if(field=="out-slot")c.CommandOut=new(1);if(field=="input")c.Input=new(777);using var h=new Harness(c);Check(h.Prepare()==2&&h.P.Value.Cookie==0&&h.Cleanup(1)==(field is "size" or "operation" or "reserved" or "out-slot"?1:0));}
                Check(RecordMock.LeafCalls==0);Check(Prepare(IntPtr.Zero,IntPtr.Zero)==2&&Finish(IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,0)==1&&Cleanup(IntPtr.Zero,IntPtr.Zero,0)==1);
            });
            Test("outer-unknown-genuine-unscoped-pass-preserves-call-and-result",()=>
            {
                foreach(bool reading in new[]{false,true}){object value=reading?new OuterByteArray(new byte[]{123,0,0,0}):new TrainCommand();var c=new B.Call{Size=48,Operation=reading?1u:0u,Version=reading?0:int.MinValue,Input=new(Native.Pointer(value)),CommandOut=reading?new(891):IntPtr.Zero,VersionOut=reading?new(892):IntPtr.Zero,MethodInfo=new(999)};using var h=new Harness(c);Check(h.Prepare()==0&&h.P.Value.Cookie==0);h.V.Value=new(){Result=new(32),Version=-123,Boolean=165};Check(Finish(h.C.Pointer,h.P.Pointer,h.V.Pointer,1)==0&&h.Cleanup()==0);Check(h.C.Value.Version==c.Version&&h.V.Value.Boolean==165&&h.V.Value.Version==-123);GC.KeepAlive(value);}
            });
            Test("outer-previously-armed-unscoped-read-and-write-denied",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var carrier=Carrier(trial);object input;B.Call call;
                using(var inv=A.Write(trial,carrier,Epoch)){call=Call(inv,false);input=inv.Input;}using(var h=new Harness(call)){Check(h.Prepare()==2&&h.P.Value.Cookie==0&&h.Cleanup(1)==0);}GC.KeepAlive(input);
                byte[] wire=Wire();using(var inv=A.Read(trial,wire,Epoch)){call=Call(inv,true);input=inv.Input;}using(var h=new Harness(call)){Check(h.Prepare()==2&&h.Cleanup(1)==0);}GC.KeepAlive(input);
            });
            Test("outer-never-armed-context-private-and-tagged-unscoped-denied",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var carrier=Carrier(trial);var call=new B.Call{Size=48,Version=123,Input=new(Native.Pointer(carrier.Value)),MethodInfo=new(990)};using var h=new Harness(call);Check(h.Prepare()==2,"Known private/tagged command reached raw outer fallback");
            });
            foreach(string kind in new[]{"private-genuine","tagged-only","protected"})Test("outer-never-armed-"+kind+"-unscoped-denied",()=>
            {
                var value=new TrainCommand{Type=ContextUnitType.MimicWarrior};
                using var trial=new C.Trial(kind=="protected"?new object[]{value}:Array.Empty<object>());
                object receiver=kind=="private-genuine"?Carrier(trial,false).Value:value;
                if(kind=="tagged-only")P.Set(value,P.Kind.Command,Intent());
                try{using var h=new Harness(new(){Size=48,Version=123,Input=new(Native.Pointer(receiver))});Check(h.Prepare()==2,"Never-armed known carrier reached raw outer fallback");}
                finally{if(kind=="tagged-only")P.Forget(value,P.Kind.Command);}
            });
            Test("outer-never-armed-off-thread-rejects-before-weak-target",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var carrier=Carrier(trial,false);int before=NativeLedgerOwnership.TargetReads;
                Task.Run(()=>{using var h=new Harness(new(){Size=48,Version=123,Input=new(Native.Pointer(carrier.Value))});Check(h.Prepare()==2);}).GetAwaiter().GetResult();
                Check(NativeLedgerOwnership.TargetReads==before);
            });
            Test("outer-denial-query-expired-weak-does-not-claim-reused-address",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var carrier=Carrier(trial,false);long key=Native.Pointer(carrier.Value);NativeLedgerOwnership.ExpiredKeys.Add(key);
                try{using var h=new Harness(new(){Size=48,Version=123,Input=new(key)});Check(h.Prepare()==0&&h.Finish(1)==0&&h.Cleanup()==0);}
                finally{NativeLedgerOwnership.ExpiredKeys.Remove(key);}
            });
            Test("outer-denial-query-weak-failure-is-contained",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var carrier=Carrier(trial,false);NativeLedgerOwnership.ThrowTargetOnce=Native.Pointer(carrier.Value);
                try{using var h=new Harness(new(){Size=48,Version=123,Input=new(Native.Pointer(carrier.Value))});Check(h.Prepare()==2&&h.P.Value.Cookie==0&&h.Cleanup(1)==0);}
                finally{NativeLedgerOwnership.ThrowTargetOnce=0;}
            });
            Test("outer-denial-query-partial-registration-key-is-reject-only",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var carrier=Carrier(trial,false);long key=Native.Pointer(carrier.Value);
                var marks=(System.Collections.IDictionary)typeof(C).GetField("DispatchSeen",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;object value=marks[key]!;marks.Remove(key);
                try{using var h=new Harness(new(){Size=48,Version=123,Input=new(key)});Check(h.Prepare()==2&&h.P.Value.Cookie==0);}
                finally{marks.Add(key,value);}
            });
            Test("outer-missing-prepare-or-finish-with-original-ran-cannot-ack",()=>
            {
                byte[] wire=Wire();using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,wire,Epoch);using var h=new Harness(Call(inv,true));Check(h.Finish()==1);Check(h.Prepare()==1&&h.Finish(1)==1&&h.Cleanup(3)==0);NoBindings();
            });
            Test("outer-return-scalar-identity-and-payload-checks",()=>
            {
                byte[] wire=Wire();using var trial=new C.Trial(Array.Empty<object>());using var inv=A.Read(trial,wire,Epoch);using var h=new Harness(Call(inv,true));Check(h.Prepare()==1&&h.Finish()==0&&h.Cleanup()==0);object result=ResultRoot(inv);
                Reject(()=>inv.RequireReturned(result,122));Reject(()=>inv.RequireReturned(result,123,false));Reject(()=>inv.RequireReturned(new TrainCommand(),123));Check(ReferenceEquals(inv.RequireReturned(result,123),result));
            });
            Test("outer-retired-result-remains-denied-unscoped",()=>
            {
                byte[] wire=Wire();object result;var trial=new C.Trial(Array.Empty<object>());
                using(var inv=A.Read(trial,wire,Epoch)){using var h=new Harness(Call(inv,true));Committed(h,inv,true);result=ResultRoot(inv);}trial.Dispose();
                using var call=new Harness(new(){Size=48,Version=123,Input=new(Native.Pointer(result))});Check(call.Prepare()==2&&P.Get(result,P.Kind.Command) is null);GC.KeepAlive(result);
            });
            string path=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT")??throw new Exception("Missing output");File.WriteAllText(path,JsonSerializer.Serialize(new{passed=Results.Count-Failed,failed=Failed,assertions=Assertions,results=Results},new JsonSerializerOptions{WriteIndented=true}));return Failed==0?0:1;
        }
        [MethodImpl(MethodImplOptions.NoInlining)] static WeakReference<object> ObserveRoot(A.Invocation inv)=>new(ResultRoot(inv));
        [MethodImpl(MethodImplOptions.NoInlining)] static void Collect(){GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();}
    }
}
