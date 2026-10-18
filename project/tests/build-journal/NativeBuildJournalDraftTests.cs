// CLR-only. Real candidate journal/context/codec/store methods; fake native objects.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Deception.Core;
using Deception.PolyModAdapter;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using J=Deception.PolyModAdapter.NativeBuildRecordJournalDraft;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using Port=Deception.PolyModAdapter.NativeBuildAttachmentPortDraft;
public enum BuildType { Ordinary=0,Smuggle=2000,Regroup=2001,Disguise=2002,Unknown=2003 }
public static class BuildAllocation { public static long ForcePointer; }
public class BuildCommand:ContextObject
{
    public BuildType Type{get;set;}
    public byte PlayerId{get;set;}=1;
    ContextCoordinates At=new(){X=4,Y=5};public bool FailCoordinates;
    public ContextCoordinates Coordinates{get{if(FailCoordinates)throw new InvalidOperationException("Synthetic snapshot field failure");return At;}set{At=value;}}
    public BuildCommand(){if(BuildAllocation.ForcePointer!=0){Pointer=new(BuildAllocation.ForcePointer);BuildAllocation.ForcePointer=0;}}
}
public sealed class BuildAction:BuildCommand {public bool DeductCost{get;set;}=true;}
namespace Deception.PolyModAdapter
{
    internal static class Entry {internal static void Note(string kind,string value){}}
    internal static class NativeExecutionTrigger {internal static bool Requested=>false;}
}
namespace Deception.Tests
{
    public static class NativeBuildJournalDraftTests
    {
        static readonly List<object> Results=new();static int Failed,Assertions;static ulong Next=10000;
        static readonly Guid Epoch=new("5b36fb78-9815-4570-95f4-e40401314bde");
        static void Check(bool b,string why="Build journal invariant failed"){Assertions++;if(!b)throw new Exception(why);}
        static void Reject(Action a){Assertions++;try{a();}catch(InvalidDataException){return;}catch(AggregateException){return;}throw new Exception("Expected rejection");}
        static void Test(string id,Action a)
        {
            try{a();Check(C.CurrentDepth==0&&C.ActiveReservations==0&&J.ActiveReservations==0&&!C.HasMutationPermit,"Leaked journal authority");Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}
            catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+" "+e);}
        }
        static C.Call Call(uint site,C.Direction dir=C.Direction.Read)=>new(site,++Next,123,dir);
        static NativeExchangeIntent Intent()=>new(Epoch,1,0,2,1,NativeExchangeKind.Regroup,false,NativePartnerReference.Existing,3,new(1,11,4,5),new(2,12,6,5),new byte[32]);
        static NativePaidBirthIntent Paid()=>new(Epoch,1,0,1,1,1,NativePaidBirthIntentCodec.Warrior,4,5,20,3);
        static byte[] Smuggle(uint unit=42){using var m=new MemoryStream();using var w=new BinaryWriter(m);w.Write(MimicSmuggleActions.PayloadMagic);w.Write((byte)1);w.Write(unit);return m.ToArray();}
        static byte[] Exchange()=>NativeExchangeIntentCodec.Encode(Intent());
        static byte[] Train()=>P.EncodeExtension(true,P.Wire.DcpbV1,123,NativePaidBirthIntentCodec.Encode(Paid()));
        static void Read(J.Trial trial,C.Scope root,J.Carrier carrier,C.Buffer buffer,BuildType type,C.Call? stamp=null)
        {
            var call=stamp??Call((uint)carrier.Kind);using var lease=trial.Read(root,carrier,buffer,call);
            lease.RequireOriginal(carrier.Value,buffer.Stream(C.Direction.Read),call);
            ((BuildCommand)carrier.Value).Type=type;lease.FinishRead();
        }
        static void TrainRead(C.Trial trial,C.Scope root,C.Carrier carrier,C.Buffer buffer)
        {
            var call=Call(3);using var leaf=root.BeginLeaf(carrier.Value,buffer.Stream(C.Direction.Read),call);
            leaf.RequireOriginal(carrier.Value,buffer.Stream(C.Direction.Read),call);((TrainCommand)carrier.Value).Type=ContextUnitType.MimicWarrior;leaf.FinishRead();
        }
        static C.Scope Root(C.Trial t,C.Buffer b,C.Direction d=C.Direction.Read)=>t.Begin(b,Call(0,d),P.Wire.DcpbV1);
        static void Mixed(bool fail,bool disposeFailure=false)
        {
            using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());
            var s=b.NewCommand();var e=b.NewAction();var paid=t.NewCarrier(P.Kind.Command);
            var bytes=Smuggle().Concat(Train()).Concat(Exchange()).ToArray();var buffer=t.NewBuffer(bytes);
            var root=Root(t,buffer);
            Read(b,root,s,buffer,BuildType.Smuggle);TrainRead(t,root,paid,buffer);Read(b,root,e,buffer,BuildType.Regroup);
            Check(buffer.Native.Position==bytes.Length&&C.ActiveReservations==1&&J.ActiveReservations==2);
            Reject(()=>MimicSmuggleActions.ForgetOffer(s.Value));Reject(()=>NativeExchangeCommands.Set(e.Value,Intent()));Reject(()=>P.Forget(paid.Value,paid.Kind));
            root.Finish();if(disposeFailure)buffer.Native.FailDispose=true;
            if(disposeFailure)Reject(()=>root.Close(0));else root.Close(fail?1:0);
            Check(Bound(s.Value)==(!fail&&!disposeFailure));Check(Bound(e.Value)==(!fail&&!disposeFailure));
            Check((P.Get(paid.Value,paid.Kind) is not null)==(!fail&&!disposeFailure));
        }
        static bool Bound(object value)=>!Port.IsUnbound(value);
        public static int Run()
        {
            Test("mixed-success-commits-only-at-shared-root",()=>Mixed(false));
            Test("outer-failure-rolls-back-smuggle-exchange-train",()=>Mixed(true));
            Test("buffer-cleanup-failure-rolls-back-all-three",()=>Mixed(false,true));
            Test("later-malformed-sibling-rolls-back-earlier-build-and-train",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());
                var s=b.NewCommand();var e=b.NewAction();var paid=t.NewCarrier(P.Kind.Command);var buffer=t.NewBuffer(Smuggle().Concat(Train()).Concat(new byte[9]).ToArray());
                var root=Root(t,buffer);Read(b,root,s,buffer,BuildType.Smuggle);TrainRead(t,root,paid,buffer);
                Reject(()=>Read(b,root,e,buffer,BuildType.Smuggle));root.Close(1);Check(!Bound(s.Value)&&!Bound(e.Value)&&P.Get(paid.Value,paid.Kind) is null);
            });
            Test("nested-completed-siblings-retain-reservations-until-parent",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();
                var outerBuffer=t.NewBuffer(Array.Empty<byte>());var root=Root(t,outerBuffer);var innerBuffer=t.NewBuffer(Smuggle());
                using(var child=t.Begin(innerBuffer,Call(0),P.Wire.LegacyNative,root)){Read(b,child,c,innerBuffer,BuildType.Smuggle);child.Finish();}
                Check(Bound(c.Value)&&J.ActiveReservations==1);Reject(()=>MimicSmuggleActions.ForgetOffer(c.Value));root.Close(1);Check(!Bound(c.Value));
            });
            Test("protected-constructor-alias-never-enters-cleanup",()=>
            {
                var source=new BuildCommand{Type=BuildType.Regroup};NativeExchangeCommands.Set(source,Intent());
                try{using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,new[]{source});BuildAllocation.ForcePointer=Native.Pointer(source);Reject(()=>b.NewCommand());Check(NativeExchangeCommands.Bytes(source).SequenceEqual(Exchange()));Reject(()=>J.RejectUnscopedPointer(Native.Pointer(source)));}
                finally{NativeExchangeCommands.Forget(source);}
            });
            Test("foreign-carrier-and-forged-token-have-no-read-authority",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());using var other=new J.Trial(t,Array.Empty<object>());
                var c=other.NewCommand();var buffer=t.NewBuffer(Smuggle());using var root=Root(t,buffer);
                Reject(()=>b.Read(root,c,buffer,Call(1)));var forged=new J.Carrier(b,new BuildCommand(),J.Kind.Command);Reject(()=>b.Read(root,forged,buffer,Call(1)));Check(buffer.Native.Position==0);
            });
            Test("read-admission-precedes-binding-retirement-and-stream-consumption",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();((BuildCommand)c.Value).Type=BuildType.Regroup;NativeExchangeCommands.Set(c.Value,Intent());
                var buffer=t.NewBuffer(Exchange());using var root=Root(t,buffer);Reject(()=>b.Read(root,c,buffer,Call(1)));Check(buffer.Native.Position==0&&NativeExchangeCommands.Bytes(c.Value).SequenceEqual(Exchange())&&J.ActiveReservations==0);
            });
            Test("wrong-alias-site-version-direction-rejected-before-native",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var buffer=t.NewBuffer(Smuggle());var unregistered=t.NewBuffer(Smuggle());using var root=Root(t,buffer);
                Reject(()=>b.Read(root,c,unregistered,Call(1)));Reject(()=>b.Read(root,c,buffer,new(2,++Next,123,C.Direction.Read)));Reject(()=>b.Read(root,c,buffer,new(1,++Next,122,C.Direction.Read)));Reject(()=>b.Read(root,c,buffer,Call(1,C.Direction.Write)));Check(J.ActiveReservations==0&&!c.ReadAttempted&&buffer.Native.Position==0);
            });
            Test("write-lease-is-separate-and-retains-original-binding-on-failure",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewAction();((BuildCommand)c.Value).Type=BuildType.Regroup;NativeExchangeCommands.Set(c.Value,Intent());
                var buffer=t.NewBuffer();var root=Root(t,buffer,C.Direction.Write);var call=Call(2,C.Direction.Write);
                using(var lease=b.Write(root,c,buffer,call)){lease.RequireOriginal(c.Value,buffer.Stream(C.Direction.Write),call);buffer.Native.Bytes(new byte[]{7,8});lease.FinishWrite();}
                Check(buffer.Snapshot().SequenceEqual(new byte[]{7,8}.Concat(Exchange())));Reject(()=>NativeExchangeCommands.Forget(c.Value));Check(!c.ReadAttempted);root.Close(1);Check(NativeExchangeCommands.Bytes(c.Value).SequenceEqual(Exchange()));
            });
            Test("writer-bound-is-per-record-after-large-private-prefix",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var buffer=t.NewBuffer();buffer.Native.Bytes(new byte[5000]);var root=Root(t,buffer,C.Direction.Write);var call=Call(1,C.Direction.Write);
                using(var lease=b.Write(root,c,buffer,call)){lease.RequireOriginal(c.Value,buffer.Stream(C.Direction.Write),call);buffer.Native.Bytes(new byte[4096]);lease.FinishWrite();}Check(buffer.Native.Length==9096);root.Finish();root.Close(0);
            });
            Test("oversized-native-reader-stops-before-private-publication",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var buffer=t.NewBuffer(new byte[4100]);var root=Root(t,buffer);var call=Call(1);
                using(var lease=b.Read(root,c,buffer,call)){lease.RequireOriginal(c.Value,buffer.Stream(C.Direction.Read),call);((ContextReader)buffer.Stream(C.Direction.Read)).BaseStream.Position=4097;((BuildCommand)c.Value).Type=BuildType.Smuggle;Reject(lease.FinishRead);Check(!Bound(c.Value)&&buffer.Native.Position==4097);}root.Close(1);
            });
            Test("snapshot-field-failure-before-publication-keeps-binding-absent",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var buffer=t.NewBuffer(Smuggle());var root=Root(t,buffer);var call=Call(1);
                using(var lease=b.Read(root,c,buffer,call))
                {
                    lease.RequireOriginal(c.Value,buffer.Stream(C.Direction.Read),call);var native=(BuildCommand)c.Value;native.Type=BuildType.Smuggle;native.FailCoordinates=true;
                    Exception? failure=null;try{lease.FinishRead();}catch(Exception e){failure=e;}finally{native.FailCoordinates=false;}
                    Check(failure is TargetInvocationException&&failure.InnerException?.Message=="Synthetic snapshot field failure"&&!Bound(c.Value));
                }
                root.Close(1);Check(!Bound(c.Value));
            });
            Test("short-suffix-never-publishes-a-partial-binding",()=>
            {
                foreach(var item in new[]{(BuildType.Smuggle,Smuggle()),(BuildType.Regroup,Exchange())})
                for(int length=0;length<item.Item2.Length;length++)
                {using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var buffer=t.NewBuffer(item.Item2.Take(length).ToArray());var root=Root(t,buffer);Reject(()=>Read(b,root,c,buffer,item.Item1));root.Close(1);Check(!Bound(c.Value)&&J.ActiveReservations==0);}
            });
            Test("write-base-change-is-detected-before-root-commit",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewAction();var buffer=t.NewBuffer();var root=Root(t,buffer,C.Direction.Write);var call=Call(2,C.Direction.Write);
                using(var lease=b.Write(root,c,buffer,call)){lease.RequireOriginal(c.Value,buffer.Stream(C.Direction.Write),call);lease.FinishWrite();}
                ((BuildAction)c.Value).DeductCost=false;root.Finish();Reject(()=>root.Close(0));Check(root.Root.Faulted&&J.ActiveReservations==0);
            });
            Test("off-thread-conflict-rejects-before-weak-target",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var buffer=t.NewBuffer(Smuggle());var root=Root(t,buffer);Read(b,root,c,buffer,BuildType.Smuggle);
                int before=NativeLedgerOwnership.TargetReads;Task.Run(()=>Reject(()=>MimicSmuggleActions.ForgetOffer(c.Value))).GetAwaiter().GetResult();Check(NativeLedgerOwnership.TargetReads==before&&Bound(c.Value));root.Close(1);
            });
            Test("active-build-and-train-leaves-cannot-interleave",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var train=t.NewCarrier(P.Kind.Command);var buffer=t.NewBuffer(Smuggle());var root=Root(t,buffer);
                using(var lease=b.Read(root,c,buffer,Call(1)))Reject(()=>root.BeginLeaf(train.Value,buffer.Stream(C.Direction.Read),Call(3)));
                root.Close(1);Check(!Bound(c.Value)&&P.Get(train.Value,train.Kind) is null);
                var next=t.NewBuffer(Train());var root2=Root(t,next);var other=b.NewCommand();
                using(var leaf=root2.BeginLeaf(train.Value,next.Stream(C.Direction.Read),Call(3)))Reject(()=>b.Read(root2,other,next,Call(1)));
                root2.Close(1);Check(J.ActiveReservations==0);
            });
            Test("ordinary-build-consumes-no-private-suffix",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var buffer=t.NewBuffer(new byte[]{0xAB,0xCD});var root=Root(t,buffer);Read(b,root,c,buffer,BuildType.Ordinary);Check(buffer.Native.Position==0&&!Bound(c.Value));root.Finish();root.Close(0);
            });
            Test("unknown-private-tag-cannot-downgrade-to-ordinary",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var buffer=t.NewBuffer(new byte[9]);var root=Root(t,buffer);Reject(()=>Read(b,root,c,buffer,BuildType.Unknown));Check(buffer.Native.Position==0);root.Close(1);Check(!Bound(c.Value));
            });
            Test("duplicate-build-cookie-does-not-release-earlier-reservation",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var d=b.NewCommand();var buffer=t.NewBuffer(Smuggle().Concat(Smuggle()).ToArray());var root=Root(t,buffer);var call=Call(1);Read(b,root,c,buffer,BuildType.Smuggle,call);Reject(()=>b.Read(root,d,buffer,call));Check(J.ActiveReservations==1&&Bound(c.Value)&&root.Root.Faulted);Reject(root.Finish);root.Close(1);Check(!Bound(c.Value));
            });
            Test("retired-carrier-never-falls-back-and-cannot-be-reread",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var buffer=t.NewBuffer(Smuggle());var root=Root(t,buffer);Read(b,root,c,buffer,BuildType.Smuggle);root.Close(1);
                var next=t.NewBuffer(Smuggle());using(var again=Root(t,next))Reject(()=>b.Read(again,c,next,Call(1)));b.Dispose();Reject(()=>J.RejectUnscopedPointer(Native.Pointer(c.Value)));J.RejectUnscopedPointer(Native.Pointer(new BuildCommand()));
            });
            Test("exact-token-rollback-does-not-delete-unexpected-replacement",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var c=b.NewCommand();var d=b.NewAction();var paid=t.NewCarrier(P.Kind.Command);var buffer=t.NewBuffer(Exchange().Concat(Smuggle()).Concat(Train()).ToArray());var root=Root(t,buffer);
                Read(b,root,c,buffer,BuildType.Regroup);Read(b,root,d,buffer,BuildType.Smuggle);TrainRead(t,root,paid,buffer);
                // Fault injection ONLY: replace private store entry bypassing its
                // correctly rejecting public mutation gate. This is not an API.
                var table=(System.Collections.IDictionary)typeof(NativeExchangeCommands).GetField("Payloads",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
                object old=table[Native.Pointer(c.Value)]!;var type=old.GetType();object replacement=Activator.CreateInstance(type,c.Value,Exchange())!;table[Native.Pointer(c.Value)]=replacement;
                Reject(()=>root.Close(1));Check(ReferenceEquals(table[Native.Pointer(c.Value)],replacement)&&!Bound(d.Value)&&P.Get(paid.Value,paid.Kind) is null&&J.ActiveReservations==0&&C.ActiveReservations==0);
            });
            string? output=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT");if(output is not null)File.WriteAllText(output,JsonSerializer.Serialize(new{scope="SYNTHETIC CLR ONLY; actual candidate journal/context/port and extracted exact existing attachment methods, no native original or hooks",failed=Failed,assertions=Assertions,results=Results},new JsonSerializerOptions{WriteIndented=true}));return Failed==0?0:1;
        }
    }
}
