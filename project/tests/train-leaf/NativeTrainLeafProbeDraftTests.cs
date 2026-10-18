// CLR control-flow mocks only. No game library, native original or detour.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using A=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;
using B=Deception.PolyModAdapter.NativeStateBridge;
namespace Deception.PolyModAdapter
{
    internal static class NativeStateBridge
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Call
        {internal uint Size;internal int Operation,Version;internal uint Site;internal IntPtr State,Stream,MethodInfo;}
        [StructLayout(LayoutKind.Sequential)] internal struct Plan
        {internal ulong Cookie;internal IntPtr Replacement;}
    }
}
namespace Deception.Tests
{
    public static class NativeTrainLeafProbeDraftTests
    {
        static readonly List<object> Results=new();static int Failed;
        static void Check(bool ok,string why="Leaf adapter invariant failed"){if(!ok)throw new Exception(why);}
        static void Reject(Action action){try{action();}catch(InvalidDataException){return;}catch(AggregateException){return;}throw new Exception("Expected rejection.");}
        static void Test(string id,Action action)
        {
            try{action();Check(C.ActiveReservations==0&&C.CurrentDepth==0&&!C.HasMutationPermit&&A.CurrentDepth==0,"Cleanup leaked");Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}
            catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e);}
        }
        static C.Call Parent(C.Direction dir)=>new(0,A.NewCookie(),123,dir);
        static NativePaidBirthIntent Intent()=>new(new Guid("a144a273-cd26-49da-821f-1c28106a35f9"),1,0,1,1,1,NativePaidBirthIntentCodec.Warrior,4,5,20,3);
        static byte[] Payload()=>P.EncodeExtension(true,P.Wire.DcpbV1,123,NativePaidBirthIntentCodec.Encode(Intent()));
        static B.Call Call(object value,P.Kind kind,C.Buffer b,C.Direction dir)=>new(){Size=40,Site=kind==P.Kind.Command?3u:4u,Operation=(int)dir,Version=123,
            State=new(Native.Pointer(value)),Stream=new(Native.Pointer(b.Stream(dir))),MethodInfo=new(91)};
        static void SetBase(object value)=>((TrainCommand)value).Type=ContextUnitType.MimicWarrior;
        static void WriteOriginal(B.Plan plan,byte[] bytes)=>ContextWriter.All[plan.Replacement.ToInt64()].BaseStream.Bytes.Write(bytes,0,bytes.Length);
        static void Read(C.Scope scope,object value,P.Kind kind,C.Buffer b,C.FactoryTicket? ticket=null)
        {
            using var invocation=A.Open(scope,value,kind,b,C.Direction.Read,ticket);var call=Call(value,kind,b,C.Direction.Read);var plan=new B.Plan();
            Check(A.Prepare(call,ref plan)==0&&plan.Replacement==IntPtr.Zero);SetBase(value);A.Finish(call,plan);A.Cleanup(call,plan,0);invocation.RequireCompleted();
        }
        static void RecordedPriceTests()
        {
            // A recorded price is immutable evidence, independent of today's
            // purchase price. Keep the older three-star record fixtures intact.
            foreach(int price in new[]{1,3})
            {
                Test("abi3-recorded-price-"+price+"-roundtrip",()=>
                {
                    var intent=Intent() with{ExpectedPrice=price};
                    using var trial=new C.Trial(Array.Empty<object>());var source=trial.NewCarrier(P.Kind.Action);
                    trial.Mutate(source,value=>{SetBase(value);((TrainAction)value).Cost=price;});
                    P.Set(source.Value,source.Kind,intent);
                    var record=new P.WriteRecord(source.Value,source.Kind,P.Wire.DcpbV1,123);
                    byte[] bytes=record.Finish();
                    Check(bytes.SequenceEqual(P.EncodeExtension(true,P.Wire.DcpbV1,123,NativePaidBirthIntentCodec.Encode(intent))));
                    var target=trial.NewCarrier(P.Kind.Action);trial.Mutate(target,value=>((TrainAction)value).Cost=price);
                    var buffer=trial.NewBuffer(bytes);
                    using(var scope=trial.Begin(buffer,Parent(C.Direction.Read),P.Wire.DcpbV1))
                    {Read(scope,target.Value,target.Kind,buffer);scope.Finish();}
                    Check(P.Get(source.Value,source.Kind)==intent&&P.Get(target.Value,target.Kind)==intent);
                    Check(((TrainAction)source.Value).Cost==price&&((TrainAction)target.Value).Cost==price);
                });
                int wrong=price==1?3:1;
                Test("abi3-recorded-price-"+price+"-rejects-cost-"+wrong,()=>
                {
                    var intent=Intent() with{ExpectedPrice=price};
                    using var trial=new C.Trial(Array.Empty<object>());var action=trial.NewCarrier(P.Kind.Action);
                    trial.Mutate(action,value=>{SetBase(value);((TrainAction)value).Cost=wrong;});
                    Reject(()=>P.Set(action.Value,action.Kind,intent));Check(P.Get(action.Value,action.Kind) is null);
                    var command=trial.NewCarrier(P.Kind.Command);trial.Mutate(command,SetBase);P.Set(command.Value,command.Kind,intent);
                    Reject(()=>P.TransferToAction(command.Value,action.Value));
                    Check(P.Get(command.Value,command.Kind)==intent&&P.Get(action.Value,action.Kind) is null);
                    var buffer=trial.NewBuffer(P.EncodeExtension(true,P.Wire.DcpbV1,123,NativePaidBirthIntentCodec.Encode(intent)));
                    using(var scope=trial.Begin(buffer,Parent(C.Direction.Read),P.Wire.DcpbV1))
                    {Reject(()=>Read(scope,action.Value,action.Kind,buffer));Check(scope.Root.Faulted);Reject(scope.Finish);}
                    Check(P.Get(action.Value,action.Kind) is null&&((TrainAction)action.Value).Cost==wrong);
                });
                Test("abi3-recorded-price-"+price+"-rejects-midwrite-repricing",()=>
                {
                    var intent=Intent() with{ExpectedPrice=price};
                    using var trial=new C.Trial(Array.Empty<object>());var action=trial.NewCarrier(P.Kind.Action);
                    trial.Mutate(action,value=>{SetBase(value);((TrainAction)value).Cost=price;});P.Set(action.Value,action.Kind,intent);
                    var record=new P.WriteRecord(action.Value,action.Kind,P.Wire.DcpbV1,123);
                    trial.Mutate(action,value=>((TrainAction)value).Cost=wrong);Reject(()=>record.Finish());
                    Check(P.Get(action.Value,action.Kind)==intent,"Rejected repricing must not rewrite the original intent.");
                });
            }
        }
        public static int Run()
        {
            Test("abi3-pod-layout",()=>Check(Marshal.SizeOf<B.Call>()==40&&Marshal.SizeOf<B.Plan>()==16&&Marshal.OffsetOf<B.Call>(nameof(B.Call.Site)).ToInt32()==12));
            foreach(var kind in new[]{P.Kind.Command,P.Kind.Action})
            {
                Test("abi3-staged-bound-writer-"+kind,()=>
                {
                    using var trial=new C.Trial(Array.Empty<object>());var value=trial.NewCarrier(kind);trial.Mutate(value,SetBase);P.Set(value.Value,kind,Intent());
                    var b=trial.NewBuffer();b.Native.Bytes(new byte[]{9,8});using var scope=trial.Begin(b,Parent(C.Direction.Write),P.Wire.DcpbV1);
                    using(var i=A.Open(scope,value.Value,kind,b,C.Direction.Write))
                    {
                        var c=Call(value.Value,kind,b,C.Direction.Write);var p=new B.Plan();Check(A.Prepare(c,ref p)==1);
                        Check(p.Replacement!=c.Stream&&b.Snapshot().SequenceEqual(new byte[]{9,8}));WriteOriginal(p,new byte[]{2,4,6});
                        A.Finish(c,p);A.Cleanup(c,p,0);i.RequireCompleted();Check(b.Snapshot().SequenceEqual(new byte[]{9,8,2,4,6}.Concat(Payload())));
                    }
                    Check(P.Get(value.Value,kind)==Intent());scope.Finish();
                });
                Test("abi3-bound-reader-journal-"+kind,()=>
                {
                    using var trial=new C.Trial(Array.Empty<object>());var value=trial.NewCarrier(kind);var b=trial.NewBuffer(Payload());
                    using(var scope=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1))
                    {Read(scope,value.Value,kind,b);Check(P.Get(value.Value,kind)==Intent()&&C.ActiveReservations==1);Reject(()=>P.Forget(value.Value,kind));scope.Finish();}
                    Check(P.Get(value.Value,kind)==Intent());
                });
                Test("abi3-legacy-native-writer-parity-"+kind,()=>
                {
                    using var trial=new C.Trial(Array.Empty<object>());var value=trial.NewCarrier(kind);var b=trial.NewBuffer();using var scope=trial.Begin(b,Parent(C.Direction.Write),P.Wire.LegacyNative);
                    using(var i=A.Open(scope,value.Value,kind,b,C.Direction.Write))
                    {var c=Call(value.Value,kind,b,C.Direction.Write);var p=new B.Plan();Check(A.Prepare(c,ref p)==1);WriteOriginal(p,new byte[]{11,22});A.Finish(c,p);A.Cleanup(c,p,0);i.RequireCompleted();}
                    Check(b.Snapshot().SequenceEqual(new byte[]{11,22}));scope.Finish();
                });
            }
            Test("abi3-exact-call-guard-before-binding-retirement",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Command);trial.Mutate(v,SetBase);P.Set(v.Value,v.Kind,Intent());var b=trial.NewBuffer(Payload());
                using var scope=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1);using var i=A.Open(scope,v.Value,v.Kind,b,C.Direction.Read);
                var correct=Call(v.Value,v.Kind,b,C.Direction.Read);var wrong=correct;var p=new B.Plan();wrong.State=new(9999);Reject(()=>A.Prepare(wrong,ref p));
                wrong=correct;wrong.Stream=new(8888);Reject(()=>A.Prepare(wrong,ref p));wrong=correct;wrong.Site=4;Reject(()=>A.Prepare(wrong,ref p));
                wrong=correct;wrong.Version=124;Reject(()=>A.Prepare(wrong,ref p));wrong=correct;wrong.Operation=0;Reject(()=>A.Prepare(wrong,ref p));
                Check(p.Cookie==0&&P.Get(v.Value,v.Kind)==Intent()&&C.ActiveReservations==0);
            });
            Test("abi3-callback-plan-and-methodinfo-must-remain-exact",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Command);var b=trial.NewBuffer(new byte[]{0});
                using var scope=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1);using var i=A.Open(scope,v.Value,v.Kind,b,C.Direction.Read);
                var c=Call(v.Value,v.Kind,b,C.Direction.Read);var p=new B.Plan();A.Prepare(c,ref p);var bad=p;bad.Cookie++;Reject(()=>A.Finish(c,bad));
                var wrong=c;wrong.MethodInfo=new(92);Reject(()=>A.Finish(wrong,p));A.Cleanup(c,p,2);Reject(i.RequireCompleted);Reject(()=>A.Cleanup(c,p,2));
            });
            Test("abi3-malformed-read-does-not-publish-and-poisons-parent",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Action);var b=trial.NewBuffer(new byte[]{1,2,3});
                using var scope=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1);using var i=A.Open(scope,v.Value,v.Kind,b,C.Direction.Read);
                var c=Call(v.Value,v.Kind,b,C.Direction.Read);var p=new B.Plan();A.Prepare(c,ref p);SetBase(v.Value);Reject(()=>A.Finish(c,p));A.Cleanup(c,p,3);
                Check(P.Get(v.Value,v.Kind) is null);Reject(i.RequireCompleted);Reject(scope.Finish);
            });
            Test("abi3-original-failure-discards-staged-writer",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Command);var b=trial.NewBuffer();b.Native.Bytes(new byte[]{42});
                using var scope=trial.Begin(b,Parent(C.Direction.Write),P.Wire.LegacyNative);using var i=A.Open(scope,v.Value,v.Kind,b,C.Direction.Write);
                var c=Call(v.Value,v.Kind,b,C.Direction.Write);var p=new B.Plan();A.Prepare(c,ref p);WriteOriginal(p,new byte[]{1,2});A.Cleanup(c,p,4);
                Check(b.Snapshot().SequenceEqual(new byte[]{42}));Reject(i.RequireCompleted);
            });
            Test("abi3-missing-hook-and-abandoned-call-cannot-complete",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Action);var b=trial.NewBuffer();using var scope=trial.Begin(b,Parent(C.Direction.Write),P.Wire.LegacyNative);
                using(var i=A.Open(scope,v.Value,v.Kind,b,C.Direction.Write)){Reject(i.RequireCompleted);}
                Check(scope.Root.Faulted);Reject(scope.Finish);
            });
            Test("abi3-off-thread-callback-has-no-private-authority",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Command);var b=trial.NewBuffer(Payload());
                using var scope=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1);using var i=A.Open(scope,v.Value,v.Kind,b,C.Direction.Read);var c=Call(v.Value,v.Kind,b,C.Direction.Read);
                System.Threading.Tasks.Task.Run(()=>{var p=new B.Plan();Reject(()=>A.Prepare(c,ref p));Check(p.Cookie==0);}).GetAwaiter().GetResult();Check(C.ActiveReservations==0);
            });
            Test("abi3-factory-ticket-enters-same-leaf-journal",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(Payload());var parent=Parent(C.Direction.Read);var v=new TrainAction();
                using(var scope=trial.Begin(b,parent,P.Wire.DcpbV1))
                {
                    using(var reader=scope.BeginFactoryReader(b.Stream(C.Direction.Read),parent))
                    {
                        C.FactoryTicket ticket;using(var call=reader.BeginFactory(P.Kind.Action,5))ticket=call.Returned(v);
                        Read(scope,v,P.Kind.Action,b,ticket);Check(ticket.Consumed&&ticket.Transferred);reader.Finish();
                    }
                    // Deliberately no parent Finish: accepted factory binding must roll back.
                }
                Check(P.Get(v,P.Kind.Action) is null);
            });
            Test("abi3-later-read-failure-retires-earlier-success",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var a=trial.NewCarrier(P.Kind.Command);var z=trial.NewCarrier(P.Kind.Action);var b=trial.NewBuffer(Payload());var bad=trial.NewBuffer(new byte[]{255});
                using(var scope=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1))
                {
                    scope.RegisterStaging(bad);Read(scope,a.Value,a.Kind,b);Reject(()=>Read(scope,z.Value,z.Kind,bad));
                    Check(P.Get(a.Value,a.Kind)==Intent());
                }
                Check(P.Get(a.Value,a.Kind) is null&&P.Get(z.Value,z.Kind) is null);
            });
            Test("abi3-bound-writer-refuses-legacy-loss-before-original",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Command);trial.Mutate(v,SetBase);P.Set(v.Value,v.Kind,Intent());var b=trial.NewBuffer();
                using var scope=trial.Begin(b,Parent(C.Direction.Write),P.Wire.LegacyNative);using var i=A.Open(scope,v.Value,v.Kind,b,C.Direction.Write);
                var c=Call(v.Value,v.Kind,b,C.Direction.Write);var p=new B.Plan();Reject(()=>A.Prepare(c,ref p));A.Cleanup(c,p,1);Check(b.Snapshot().Length==0&&P.Get(v.Value,v.Kind)==Intent());
            });
            RecordedPriceTests();
            string path=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT")??throw new Exception("Missing fixture output");
            File.WriteAllText(path,JsonSerializer.Serialize(new{passed=Results.Count-Failed,failed=Failed,results=Results},new JsonSerializerOptions{WriteIndented=true}));return Failed==0?0:1;
        }
    }
}
