// Synthetic routing/lifetime tests only. No game/native original or detour.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Deception.Core;
using Deception.PolyModAdapter;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using A=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;
using D=Deception.PolyModAdapter.NativeTrainLeafDispatcher;
using B=Deception.PolyModAdapter.NativeStateBridge;
namespace Deception.Tests
{
 public static class NativeTrainDispatchDraftTests
 {
  static readonly List<object> Results=new();static int Failed;
  static void Check(bool yes,string why="Dispatcher invariant failed"){if(!yes)throw new Exception(why);}
  static void Reject(Action body){try{body();}catch(InvalidDataException){return;}catch(AggregateException){return;}throw new Exception("Expected rejection.");}
  static void Test(string id,Action body)
  {
   try{body();Check(C.ActiveReservations==0&&C.CurrentDepth==0&&!C.HasMutationPermit&&A.CurrentDepth==0,"Cleanup leaked");Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}
   catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e);}
  }
  static object Fresh(P.Kind kind)=>kind==P.Kind.Command?new TrainCommand():new TrainAction();
  static B.Call Call(object value,P.Kind kind,object stream,int operation=1,int version=123)=>new()
  {Size=40,Site=kind==P.Kind.Command?3u:4u,Operation=operation,Version=version,State=new(Native.Pointer(value)),Stream=new(Native.Pointer(stream)),MethodInfo=new(7531)};
  static C.Call Parent(C.Direction d)=>new(0,A.NewCookie(),123,d);
  static NativePaidBirthIntent Intent()=>new(new Guid("a144a273-cd26-49da-821f-1c28106a35f9"),1,0,1,1,1,NativePaidBirthIntentCodec.Warrior,4,5,20,3);
  static byte[] Payload()=>P.EncodeExtension(true,P.Wire.DcpbV1,123,NativePaidBirthIntentCodec.Encode(Intent()));
  static ContextReader Reader()=>new(new(new byte[]{7,8,9}));
  static void SetBase(object value)=>((TrainCommand)value).Type=ContextUnitType.MimicWarrior;
  static void Denied(object value,P.Kind kind,object stream,int operation=1,int version=123)
  {var call=Call(value,kind,stream,operation,version);var plan=new B.Plan();Reject(()=>D.Prepare(call,value,stream,ref plan));Check(plan.Cookie==0&&plan.Replacement==IntPtr.Zero);D.Cleanup(call,plan,1);}
  static void PrivateRead(C.Scope scope,object value,P.Kind kind,C.Buffer buffer,C.FactoryTicket? ticket=null)
  {
   object stream=buffer.Stream(C.Direction.Read);var call=Call(value,kind,stream);var plan=new B.Plan();
   using var operation=A.Open(scope,value,kind,buffer,C.Direction.Read,ticket);
   Check(D.Classify(call,value,stream)==D.Route.ExactPrivate);Check(D.Prepare(call,value,stream,ref plan)==0&&plan.Cookie!=0);
   SetBase(value);D.Finish(call,plan);D.Cleanup(call,plan,0);operation.RequireCompleted();
  }
  public static int Run()
  {
   foreach(var kind in new[]{P.Kind.Command,P.Kind.Action})foreach(int op in new[]{0,1})
    Test("train-dispatch-genuine-legacy-"+kind+"-"+op,()=>
    {
     object value=Fresh(kind);object stream=op==0?new ContextWriter(new(null)):Reader();int marks=C.DispatchMarkCount;
     foreach(int version in new[]{-7,51,123,int.MaxValue})
     {
      var call=Call(value,kind,stream,op,version);var before=call;var plan=new B.Plan();
      Check(D.Classify(call,value,stream)==D.Route.LegacyOriginal&&D.Prepare(call,value,stream,ref plan)==0);
      Check(plan.Cookie==0&&plan.Replacement==IntPtr.Zero&&call.Version==version&&call.MethodInfo==before.MethodInfo&&call.State==before.State&&call.Stream==before.Stream);
      D.Finish(call,plan);D.Cleanup(call,plan,0);
     }
     Check(C.DispatchMarkCount==marks&&P.Get(value,kind) is null);
    });
   Test("train-dispatch-legacy-raw-byte-and-exception-control-flow",()=>
   {
    var value=new TrainCommand();var stream=new ContextWriter(new(null));var call=Call(value,P.Kind.Command,stream,0);var plan=new B.Plan();
    int originals=0;Check(D.Prepare(call,value,stream,ref plan)==0);originals++;stream.BaseStream.Bytes.Write(new byte[]{4,5,6});D.Finish(call,plan);D.Cleanup(call,plan,0);
    Check(originals==1&&stream.BaseStream.Bytes.ToArray().SequenceEqual(new byte[]{4,5,6}));
    Exception expected=new IOException("mock native failure"),caught;
    plan=new();Check(D.Prepare(call,value,stream,ref plan)==0);
    try{try{throw expected;}finally{D.Cleanup(call,plan,4);}}catch(Exception error){caught=error;}
    Check(ReferenceEquals(caught,expected));
   });
   Test("train-dispatch-invalid-call-before-any-retirement",()=>
   {
    var value=new TrainCommand{Type=ContextUnitType.MimicWarrior};P.Set(value,P.Kind.Command,Intent());var stream=Reader();long cursor=stream.BaseStream.Position;
    try
    {
     var correct=Call(value,P.Kind.Command,stream);
     foreach(int axis in Enumerable.Range(0,6))
     {var call=correct;switch(axis){case 0:call.Size=39;break;case 1:call.Site=2;break;case 2:call.Operation=2;break;case 3:call.State=new(99);break;case 4:call.Stream=new(98);break;case 5:call.State=IntPtr.Zero;break;}var p=new B.Plan();Reject(()=>D.Prepare(call,value,stream,ref p));Check(p.Cookie==0);}
     Check(P.Get(value,P.Kind.Command)==Intent()&&stream.BaseStream.Position==cursor);
    }finally{P.Forget(value,P.Kind.Command);}
   });
   Test("train-dispatch-owned-carrier-unscoped-and-retired-deny",()=>
   {
    object value;var stream=Reader();
    using(var trial=new C.Trial(Array.Empty<object>()))
    {value=trial.NewCarrier(P.Kind.Command).Value;Check(C.InspectForDispatch(value,P.Kind.Command,stream).HasFlag(C.DispatchOwnership.PrivateCarrier));Denied(value,P.Kind.Command,stream);}
    Check(C.InspectForDispatch(value,P.Kind.Command,stream).HasFlag(C.DispatchOwnership.Retired));Denied(value,P.Kind.Command,stream);
   });
   Test("train-dispatch-owned-stream-and-base-alias-deny",()=>
   {
    var value=new TrainCommand();using var trial=new C.Trial(Array.Empty<object>());var buffer=trial.NewBuffer();object stream=buffer.Stream(C.Direction.Read);
    Denied(value,P.Kind.Command,stream);var alias=new ContextReader((ContextStream)Native.Get(stream,"BaseStream")!);
    var flags=C.InspectForDispatch(value,P.Kind.Command,alias);Check(flags.HasFlag(C.DispatchOwnership.OwnedBaseStream));Denied(value,P.Kind.Command,alias);
    buffer.Dispose();Check(C.InspectForDispatch(value,P.Kind.Command,alias).HasFlag(C.DispatchOwnership.Retired));Denied(value,P.Kind.Command,alias);
   });
   Test("train-dispatch-protected-live-and-retired-wrapper-alias-deny",()=>
   {
    var value=new TrainCommand();var alias=new TrainCommand{Pointer=value.Pointer};var stream=Reader();
    using(var trial=new C.Trial(new[]{value})){Denied(alias,P.Kind.Command,stream);}
    Check(C.InspectForDispatch(alias,P.Kind.Command,stream).HasFlag(C.DispatchOwnership.ProtectedCarrier));Denied(alias,P.Kind.Command,stream);GC.KeepAlive(value);
   });
   Test("train-dispatch-tagged-retagged-and-forgotten-deny",()=>
   {
    var value=new TrainCommand{Type=ContextUnitType.MimicWarrior};var stream=Reader();P.Set(value,P.Kind.Command,Intent());
    value.Type=ContextUnitType.Warrior;Denied(value,P.Kind.Command,stream);Check(P.Get(value,P.Kind.Command)==Intent());
    P.Forget(value,P.Kind.Command);Denied(value,P.Kind.Command,stream);Check(C.InspectForDispatch(value,P.Kind.Command,stream).HasFlag(C.DispatchOwnership.TaggedCarrier));
   });
   Test("train-dispatch-unbound-custom-is-not-legacy-authority",()=>
   {foreach(var kind in new[]{P.Kind.Command,P.Kind.Action}){object value=Fresh(kind);SetBase(value);Denied(value,kind,Reader());((TrainCommand)value).Type=ContextUnitType.MimicRider;Denied(value,kind,Reader());}});
   foreach(var kind in new[]{P.Kind.Command,P.Kind.Action})
   {
    Test("train-dispatch-exact-private-read-"+kind,()=>
    {using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(kind);var b=trial.NewBuffer(Payload());using var parent=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1);PrivateRead(parent,v.Value,kind,b);Check(P.Get(v.Value,kind)==Intent());parent.Finish();});
    Test("train-dispatch-exact-private-write-"+kind,()=>
    {
     using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(kind);var b=trial.NewBuffer();using var parent=trial.Begin(b,Parent(C.Direction.Write),P.Wire.LegacyNative);
     object stream=b.Stream(C.Direction.Write);var call=Call(v.Value,kind,stream,0);var plan=new B.Plan();using var i=A.Open(parent,v.Value,kind,b,C.Direction.Write);
     Check(D.Prepare(call,v.Value,stream,ref plan)==1&&plan.Cookie!=0&&plan.Replacement!=call.Stream);
     ContextWriter.All[plan.Replacement.ToInt64()].BaseStream.Bytes.Write(new byte[]{2,4,8});D.Finish(call,plan);D.Cleanup(call,plan,0);i.RequireCompleted();Check(b.Snapshot().SequenceEqual(new byte[]{2,4,8}));parent.Finish();
    });
   }
   Test("train-dispatch-mismatched-private-call-no-fallback",()=>
   {
    using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Command);trial.Mutate(v,SetBase);P.Set(v.Value,v.Kind,Intent());var b=trial.NewBuffer(Payload());
    using var parent=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1);using var i=A.Open(parent,v.Value,v.Kind,b,C.Direction.Read);
    Denied(v.Value,v.Kind,b.Stream(C.Direction.Read),1,124);Denied(v.Value,v.Kind,Reader());Check(P.Get(v.Value,v.Kind)==Intent()&&C.ActiveReservations==0);
   });
   Test("train-dispatch-query-is-readonly-and-does-not-create-admission",()=>
   {
    using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Command);trial.Mutate(v,SetBase);P.Set(v.Value,v.Kind,Intent());var b=trial.NewBuffer(Payload());
    using var parent=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1);int count=C.DispatchMarkCount;long cursor=b.Native.Position;
    for(int j=0;j<50;j++){_=C.InspectForDispatch(v.Value,v.Kind,b.Stream(C.Direction.Read));Denied(v.Value,v.Kind,b.Stream(C.Direction.Read));}
    Check(C.DispatchMarkCount==count&&b.Native.Position==cursor&&P.Get(v.Value,v.Kind)==Intent()&&C.ActiveReservations==0);parent.Finish();
   });
   Test("train-dispatch-offthread-known-denied-before-weak-access",()=>
   {
    using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Command);var stream=Reader();
    int reads=NativeLedgerOwnership.TargetReads;
    Task.Run(()=>Denied(v.Value,v.Kind,stream)).GetAwaiter().GetResult();Check(NativeLedgerOwnership.TargetReads==reads);
    var ordinary=new TrainCommand();var unknown=Reader();_=D.Classify(Call(ordinary,P.Kind.Command,unknown),ordinary,unknown);
    Task.Run(()=>Check(D.Classify(Call(ordinary,P.Kind.Command,unknown),ordinary,unknown)==D.Route.LegacyOriginal)).GetAwaiter().GetResult();
   });
   Test("train-dispatch-factory-pending-to-exact-journal",()=>
   {
    using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(Payload());var stamp=Parent(C.Direction.Read);var value=new TrainAction();
    using(var parent=trial.Begin(b,stamp,P.Wire.DcpbV1))
    {
     using(var reader=parent.BeginFactoryReader(b.Stream(C.Direction.Read),stamp))
     {
      C.FactoryTicket ticket;using(var f=reader.BeginFactory(P.Kind.Action,5))ticket=f.Returned(value);
      Check(C.InspectForDispatch(value,P.Kind.Action,b.Stream(C.Direction.Read)).HasFlag(C.DispatchOwnership.FactoryPending));Denied(value,P.Kind.Action,b.Stream(C.Direction.Read));
      PrivateRead(parent,value,P.Kind.Action,b,ticket);Check(C.InspectForDispatch(value,P.Kind.Action,b.Stream(C.Direction.Read)).HasFlag(C.DispatchOwnership.ReservedCarrier));reader.Finish();
     }
     // Deliberately fail outer journal after accepted read.
    }
    Check(P.Get(value,P.Kind.Action) is null);Denied(value,P.Kind.Action,Reader());
   });
   Test("train-dispatch-unused-factory-ticket-retired-deny",()=>
   {
    using var trial=new C.Trial(Array.Empty<object>());var b=trial.NewBuffer(new byte[]{0});var stamp=Parent(C.Direction.Read);var value=new TrainCommand();
    using(var parent=trial.Begin(b,stamp,P.Wire.DcpbV1))using(var reader=parent.BeginFactoryReader(b.Stream(C.Direction.Read),stamp))using(var f=reader.BeginFactory(P.Kind.Command,5))_=f.Returned(value);
    Check(C.InspectForDispatch(value,P.Kind.Command,Reader()).HasFlag(C.DispatchOwnership.Retired));Denied(value,P.Kind.Command,Reader());
   });
   Test("train-dispatch-protected-other-trial-cannot-become-private",()=>
   {
    var source=new TrainCommand();using var protectedTrial=new C.Trial(new[]{source});using var trial=new C.Trial(Array.Empty<object>());
    var b=trial.NewBuffer(new byte[]{0});var stamp=Parent(C.Direction.Read);using var parent=trial.Begin(b,stamp,P.Wire.DcpbV1);using var reader=parent.BeginFactoryReader(b.Stream(C.Direction.Read),stamp);
    using(var f=reader.BeginFactory(P.Kind.Command,5))Reject(()=>f.Returned(source));Denied(source,P.Kind.Command,Reader());
   });
   Test("train-dispatch-weak-failure-rejects-with-no-query-mutation",()=>
   {
    using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Command);var stream=Reader();int count=C.DispatchMarkCount;
    NativeLedgerOwnership.ThrowTargetOnce=Native.Pointer(v.Value);
    try{_=D.Classify(Call(v.Value,v.Kind,stream),v.Value,stream);throw new Exception("Weak error was ignored");}
    catch(InvalidOperationException error){Check(error.Message=="Synthetic weak target failure.");}
    Check(C.DispatchMarkCount==count);Denied(v.Value,v.Kind,stream);
   });
   Test("train-dispatch-partial-registration-never-falls-back",()=>
   {
    var protectedValue=new TrainCommand();using var protectedTrial=new C.Trial(new[]{protectedValue});using var trial=new C.Trial(Array.Empty<object>());
    static Dictionary<long,NativeLedgerOwnership.WeakIdentity> Registry(string name)=>(Dictionary<long,NativeLedgerOwnership.WeakIdentity>)typeof(C).GetField(name,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.GetValue(null)!;
    var seen=Registry("SeenStreams");var old=seen.Keys.ToHashSet();NativeLedgerOwnership.ThrowTargetOnce=Native.Pointer(protectedValue);
    try{_=trial.NewBuffer();throw new Exception("Expected registration fault");}catch(InvalidOperationException e){Check(e.Message=="Synthetic weak target failure.");}
    var added=seen.Where(x=>!old.Contains(x.Key)).Select(x=>x.Value.Target()).Where(x=>x is not null).ToArray();
    var failedReader=(ContextReader)added.Single(x=>x is ContextReader)!;var ordinary=new TrainCommand();
    Check(C.InspectForDispatch(ordinary,P.Kind.Command,failedReader).HasFlag(C.DispatchOwnership.IncompleteRegistration));Denied(ordinary,P.Kind.Command,failedReader);
    Denied(ordinary,P.Kind.Command,new ContextReader(failedReader.BaseStream));
    var carriers=Registry("CarrierSeen");old=carriers.Keys.ToHashSet();NativeLedgerOwnership.ThrowTargetOnce=Native.Pointer(protectedValue);
    try{_=trial.NewCarrier(P.Kind.Command);throw new Exception("Expected carrier publication fault");}catch(InvalidOperationException e){Check(e.Message=="Synthetic weak target failure.");}
    object failedCarrier=carriers.Where(x=>!old.Contains(x.Key)).Single().Value.Target()!;
    Check(C.InspectForDispatch(failedCarrier,P.Kind.Command,Reader()).HasFlag(C.DispatchOwnership.IncompleteRegistration));Denied(failedCarrier,P.Kind.Command,Reader());
   });
   Test("train-dispatch-dead-weak-target-not-address-only-ownership",()=>
   {
    var source=new TrainCommand();using(var trial=new C.Trial(new[]{source})){}
    NativeLedgerOwnership.ExpiredKeys.Add(Native.Pointer(source));
    try
    {var reused=new TrainCommand{Pointer=source.Pointer};var stream=Reader();int count=C.DispatchMarkCount;Check(D.Classify(Call(reused,P.Kind.Command,stream),reused,stream)==D.Route.LegacyOriginal&&C.DispatchMarkCount==count);}
    finally{NativeLedgerOwnership.ExpiredKeys.Remove(Native.Pointer(source));}
   });
   Test("train-dispatch-unrelated-legacy-does-not-inherit-private-mode",()=>
   {
    using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Action);var b=trial.NewBuffer(Payload());using var parent=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1);
    using var i=A.Open(parent,v.Value,v.Kind,b,C.Direction.Read);var unrelated=new TrainCommand();var stream=Reader();var call=Call(unrelated,P.Kind.Command,stream,1,51);var plan=new B.Plan();
    Check(D.Prepare(call,unrelated,stream,ref plan)==0&&plan.Cookie==0);D.Finish(call,plan);D.Cleanup(call,plan,0);Check(A.CurrentDepth==1&&C.ActiveReservations==0);
   });
   Test("train-dispatch-faulted-parent-never-falls-back",()=>
   {
    using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Command);var b=trial.NewBuffer(new byte[]{255});using var parent=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1);
    using var i=A.Open(parent,v.Value,v.Kind,b,C.Direction.Read);parent.Root.Faulted=true;Denied(v.Value,v.Kind,b.Stream(C.Direction.Read));Check(C.ActiveReservations==0);
   });
   Test("train-dispatch-malformed-private-cleans-without-legacy-retry",()=>
   {
    using var trial=new C.Trial(Array.Empty<object>());var v=trial.NewCarrier(P.Kind.Command);var b=trial.NewBuffer(new byte[]{255});using var parent=trial.Begin(b,Parent(C.Direction.Read),P.Wire.DcpbV1);
    using var i=A.Open(parent,v.Value,v.Kind,b,C.Direction.Read);object stream=b.Stream(C.Direction.Read);var call=Call(v.Value,v.Kind,stream);var plan=new B.Plan();int originals=0;
    Check(D.Prepare(call,v.Value,stream,ref plan)==0&&plan.Cookie!=0);originals++;SetBase(v.Value);Reject(()=>D.Finish(call,plan));D.Cleanup(call,plan,3);
    Check(originals==1&&P.Get(v.Value,v.Kind) is null&&parent.Root.Faulted);Denied(v.Value,v.Kind,Reader());
   });
   File.WriteAllText(Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT")!,JsonSerializer.Serialize(new{passed=Results.Count-Failed,failed=Failed,results=Results},new JsonSerializerOptions{WriteIndented=true}));return Failed==0?0:1;
  }
 }
}
