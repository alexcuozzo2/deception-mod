// CLR-only observer/callback model. No IL2CPP invocation, hooks or game execution.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Deception.Core;
using Deception.PolyModAdapter;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using J=Deception.PolyModAdapter.NativeBuildRecordJournalDraft;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using Port=Deception.PolyModAdapter.NativeBuildAttachmentPortDraft;
using A=Deception.PolyModAdapter.NativeBuildLeafProbeAdapter;
using T=Deception.PolyModAdapter.NativeBuildFactoryTraversalDraft;
using D=Deception.PolyModAdapter.NativeBuildLeafDispatcher;
using R=Deception.PolyModAdapter.NativePrivateFactoryObserverRouteDraft;
using B=Deception.PolyModAdapter.NativeStateBridge;
using TT=Deception.PolyModAdapter.NativeTrainFactoryTraversal;
using TD=Deception.PolyModAdapter.NativeTrainLeafDispatcher;
public sealed class OpaqueBuildFactoryResult:ContextObject
{
    readonly BuildCommand Target;
    public OpaqueBuildFactoryResult(BuildCommand target){Target=target;Pointer=target.Pointer;}
    public TCast Cast<TCast>() where TCast:class => (TCast)(object)Target;
}
namespace Deception.Tests
{
    public static class NativeBuildFactoryDraftTests
    {
        static readonly List<object> Results=new();static int Failed,Assertions,OriginalReads;static ulong Next=50000;
        static readonly Guid Epoch=new("5b36fb78-9815-4570-95f4-e40401314bde");
        static void Check(bool ok,string why="Build factory invariant"){Assertions++;if(!ok)throw new Exception(why);}
        static Exception Error(Action f){try{f();}catch(Exception e){Assertions++;return e;}throw new Exception("Expected rejection.");}
        static void Test(string id,Action f)
        {
            OriginalReads=0;
            try{f();Check(C.CurrentDepth==0&&C.ActiveReservations==0&&J.ActiveReservations==0&&A.CurrentDepth==0&&T.CurrentDepth==0&&TT.CurrentDepth==0&&!C.HasMutationPermit,"Leaked private authority");Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}
            catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+" "+e);}
        }
        static C.Call Parent()=>new(0,++Next,123,C.Direction.Read);
        static NativePaidBirthIntent Paid()=>new(Epoch,1,0,1,1,1,NativePaidBirthIntentCodec.Warrior,4,5,20,3);
        static byte[] Train()=>P.EncodeExtension(true,P.Wire.DcpbV1,123,NativePaidBirthIntentCodec.Encode(Paid()));
        static NativeExchangeIntent Intent(byte actor=1)=>new(Epoch,1,0,2,actor,NativeExchangeKind.Regroup,false,NativePartnerReference.Existing,3,new(1,11,4,5),new(2,12,6,5),new byte[32]);
        static byte[] Smuggle(){using var m=new MemoryStream();using var w=new BinaryWriter(m);w.Write(MimicSmuggleActions.PayloadMagic);w.Write((byte)1);w.Write((uint)42);return m.ToArray();}
        static BuildCommand Fresh(J.Kind kind)=>kind==J.Kind.Command?new BuildCommand():new BuildAction();
        static B.Call Call(object v,object stream,uint site,int version=123)=>new(){Size=40,Site=site,Operation=1,Version=version,State=new(Native.Pointer(v)),Stream=new(Native.Pointer(stream)),MethodInfo=new(71)};
        static object? Before(J.Kind kind,ushort discriminator=1){object? frame;if(kind==J.Kind.Command)R.BeforeCommand(discriminator,out frame);else R.BeforeAction(discriminator,out frame);return frame;}
        static J.Carrier Simulate(J.Trial trial,C.Scope root,C.Buffer buffer,J.Kind kind,BuildType type=BuildType.Smuggle,string mode="",BuildCommand? supplied=null)
        {
            using var traversal=T.Open(trial,root,buffer,kind);object? frame=mode=="missing-prefix"?null:Before(kind,mode=="wrong-discriminator"?(ushort)5:(ushort)1);
            if(mode=="duplicate-prefix")Before(kind);
            var value=supplied??Fresh(kind);object result=mode=="opaque-return"?new OpaqueBuildFactoryResult(value):value;
            if(mode!="missing-postfix")
            {
                if(mode=="offthread-postfix")Task.Run(()=>R.AfterFactory(result,true,frame)).GetAwaiter().GetResult();
                else R.AfterFactory(mode=="null-return"?null:result,mode!="original-skipped",frame);
                if(mode=="duplicate-postfix")R.AfterFactory(result,true,frame);
            }
            if(mode!="missing-finalizer"){Check(R.FactoryFinished(null,frame) is null);if(mode=="duplicate-finalizer")R.FactoryFinished(null,frame);}
            if(mode=="cursor-changed")((ContextReader)buffer.Stream(C.Direction.Read)).BaseStream.Position++;
            object stream=mode=="reader-alias"?new ContextReader(((ContextReader)buffer.Stream(C.Direction.Read)).BaseStream):buffer.Stream(C.Direction.Read);
            var call=Call(value,stream,(uint)kind,mode=="wrong-version"?122:123);var plan=new B.Plan();int outcome=1;
            try
            {
                D.Prepare(call,value,stream,ref plan);outcome=4;
                OriginalReads++;value.Type=type;outcome=3;D.Finish(call,plan);outcome=0;
            }
            finally{D.Cleanup(call,plan,outcome);}
            return traversal.Complete(mode=="helper-replaced"?Fresh(kind):value);
        }
        static void TrainRead(C.Scope root,C.Buffer buffer,C.Carrier value)
        {
            var call=new C.Call(value.Kind==P.Kind.Command?3u:4u,++Next,123,C.Direction.Read);
            using var leaf=root.BeginLeaf(value.Value,buffer.Stream(C.Direction.Read),call);
            leaf.RequireOriginal(value.Value,buffer.Stream(C.Direction.Read),call);((TrainCommand)value.Value).Type=ContextUnitType.MimicWarrior;leaf.FinishRead();
        }
        public static int Run()
        {
            foreach(var kind in new[]{J.Kind.Command,J.Kind.Action})foreach(var type in new[]{BuildType.Ordinary,BuildType.Smuggle,BuildType.Regroup})
            Test("positive-native-return-model-"+kind+"-"+type,()=>
            {
                byte[] bytes=type==BuildType.Ordinary?Array.Empty<byte>():type==BuildType.Smuggle?Smuggle():NativeExchangeIntentCodec.Encode(Intent());
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(bytes);J.Carrier carrier;
                using(var root=t.Begin(input,Parent(),P.Wire.DcpbV1))
                {carrier=Simulate(b,root,input,kind,type);Check(carrier.FromFactory&&carrier.ReadAttempted&&J.ActiveReservations==1&&input.Native.Position==bytes.Length);Error(()=>MimicSmuggleActions.ForgetOffer(carrier.Value));root.Finish();}
                Check(OriginalReads==1&&(Port.IsUnbound(carrier.Value)==(type==BuildType.Ordinary)));
            });
            Test("opaque-native-base-wrapper-casts-with-same-pointer",()=>
            {using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);var c=Simulate(b,root,input,J.Kind.Command,mode:"opaque-return");Check(!Port.IsUnbound(c.Value));root.Finish();});
            foreach(string mode in new[]{"missing-prefix","missing-postfix","missing-finalizer","wrong-discriminator","duplicate-prefix","duplicate-postfix","duplicate-finalizer","offthread-postfix","null-return","original-skipped","cursor-changed","reader-alias","wrong-version"})
            Test("reject-before-original-"+mode,()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());var value=new BuildCommand();
                using(var root=t.Begin(input,Parent(),P.Wire.DcpbV1)){Error(()=>Simulate(b,root,input,J.Kind.Command,mode:mode,supplied:value));Check(OriginalReads==0&&Port.IsUnbound(value));}
                Check(Port.IsUnbound(value));
            });
            Test("wrong-concrete-factory-family-rejected",()=>
            {using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);Error(()=>Simulate(b,root,input,J.Kind.Command,supplied:new BuildAction()));Check(OriginalReads==0&&J.ActiveReservations==0);});
            Test("helper-replaced-result-rolls-back-binding",()=>
            {using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());var value=new BuildCommand();using(var root=t.Begin(input,Parent(),P.Wire.DcpbV1)){Error(()=>Simulate(b,root,input,J.Kind.Command,mode:"helper-replaced",supplied:value));Check(OriginalReads==1);}Check(Port.IsUnbound(value));});
            Test("codec-valid-exchange-base-mismatch-rejects-publication",()=>
            {using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(NativeExchangeIntentCodec.Encode(Intent(2)));var value=new BuildCommand();using(var root=t.Begin(input,Parent(),P.Wire.DcpbV1)){Error(()=>Simulate(b,root,input,J.Kind.Command,BuildType.Regroup,supplied:value));Check(OriginalReads==1&&Port.IsUnbound(value));}Check(Port.IsUnbound(value));});
            foreach(bool returned in new[]{false,true})Test("never-reaches-leaf-cleanup-"+returned,()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());var value=new BuildCommand();
                using(var root=t.Begin(input,Parent(),P.Wire.DcpbV1))
                {using(var traversal=T.Open(b,root,input,J.Kind.Command)){var frame=Before(J.Kind.Command);if(returned)R.AfterFactory(value,true,frame);R.FactoryFinished(null,frame);Check(J.ActiveReservations==(returned?1:0));}Check(root.Root.Faulted);}
                Check(Port.IsUnbound(value)&&J.ActiveReservations==0);
            });
            Test("factory-exception-identity-preserved",()=>
            {using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);using var traversal=T.Open(b,root,input,J.Kind.Command);var frame=Before(J.Kind.Command);var ex=new IOException("native-model");Check(ReferenceEquals(R.FactoryFinished(ex,frame),ex)&&root.Root.Faulted);});
            Test("pending-ticket-denies-mutation-unscoped-and-thread-before-weak",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());var value=new BuildCommand();
                using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);using var reader=b.BeginFactoryReader(root,input,J.Kind.Command,new(1,++Next,123,C.Direction.Read));reader.Before(J.Kind.Command,1);reader.ObserveReturn(value,true);reader.After(null);
                Error(()=>MimicSmuggleActions.ForgetOffer(value));Error(()=>J.RejectUnscopedPointer(Native.Pointer(value)));
                int reads=NativeLedgerOwnership.TargetReads;Task.Run(()=>Error(()=>reader.Ticket!.Claim(value,input.Stream(C.Direction.Read),reader.Call))).GetAwaiter().GetResult();Check(reads==NativeLedgerOwnership.TargetReads&&input.Native.Position==0);
            });
            Test("ticket-claim-once-and-native-reader-once",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());var value=new BuildCommand();using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);
                using var reader=b.BeginFactoryReader(root,input,J.Kind.Command,new(1,++Next,123,C.Direction.Read));reader.Before(J.Kind.Command,1);reader.ObserveReturn(value,true);reader.After(null);
                var lease=reader.Ticket!.Claim(value,input.Stream(C.Direction.Read),reader.Call);lease.RequireOriginal(value,input.Stream(C.Direction.Read),reader.Call);
                Error(()=>reader.Ticket.Claim(value,input.Stream(C.Direction.Read),reader.Call));Check(input.Native.Position==0&&Port.IsUnbound(value));
            });
            Test("forged-ticket-cannot-reuse-positive-return-authority",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());var value=new BuildCommand();using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);
                using var reader=b.BeginFactoryReader(root,input,J.Kind.Command,new(1,++Next,123,C.Direction.Read));reader.Before(J.Kind.Command,1);reader.ObserveReturn(value,true);reader.After(null);
                var exact=reader.Ticket!;var forged=new J.FactoryTicket(reader,exact.Carrier,exact.Position){Lease=exact.Lease};
                Error(()=>forged.Claim(value,input.Stream(C.Direction.Read),reader.Call));Check(!exact.Consumed&&!forged.Consumed&&input.Native.Position==0&&Port.IsUnbound(value));
            });
            Test("factory-origin-cannot-reenter-constructor-read",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Array.Empty<byte>());J.Carrier c;
                using(var root=t.Begin(input,Parent(),P.Wire.LegacyNative)){c=Simulate(b,root,input,J.Kind.Command,BuildType.Ordinary);root.Finish();}
                var second=t.NewBuffer(Smuggle());using var later=t.Begin(second,Parent(),P.Wire.DcpbV1);Error(()=>b.Read(later,c,second,new(1,++Next,123,C.Direction.Read)));Check(second.Native.Position==0&&Port.IsUnbound(c.Value));
            });
            Test("protected-wrapper-alias-keeps-original-attachment",()=>
            {
                var source=new BuildCommand{Type=BuildType.Regroup};NativeExchangeCommands.Set(source,Intent());var alias=new BuildCommand{Pointer=source.Pointer};byte[] prior=NativeExchangeCommands.Bytes(source);
                try{using(var t=new C.Trial(new[]{source})){using var b=new J.Trial(t,new[]{source});var input=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);Error(()=>Simulate(b,root,input,J.Kind.Command,supplied:alias));Check(J.ActiveReservations==0&&OriginalReads==0&&NativeExchangeCommands.Bytes(source).SequenceEqual(prior));}Check(NativeExchangeCommands.Bytes(source).SequenceEqual(prior));}
                finally{NativeExchangeCommands.Forget(source);}
            });
            Test("bound-unrelated-return-never-cleanup-owned",()=>
            {
                var value=new BuildCommand{Type=BuildType.Regroup};NativeExchangeCommands.Set(value,Intent());
                try{using(var t=new C.Trial(Array.Empty<object>())){using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);Error(()=>Simulate(b,root,input,J.Kind.Command,supplied:value));}Check(NativeExchangeCommands.Bytes(value).SequenceEqual(NativeExchangeIntentCodec.Encode(Intent())));}
                finally{NativeExchangeCommands.Forget(value);}
            });
            Test("known-live-and-retired-return-never-readmitted",()=>
            {
                BuildCommand old;
                using(var t=new C.Trial(Array.Empty<object>())){using var b=new J.Trial(t,Array.Empty<object>());old=(BuildCommand)b.NewCommand().Value;var input=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);Error(()=>Simulate(b,root,input,J.Kind.Command,supplied:old));}
                using(var t=new C.Trial(Array.Empty<object>())){using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);Error(()=>Simulate(b,root,input,J.Kind.Command,supplied:old));Error(()=>J.RejectUnscopedPointer(Native.Pointer(old)));}
            });
            Test("train-window-and-build-window-are-mutually-exclusive",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);
                using(var build=b.BeginFactoryReader(root,input,J.Kind.Command,new(1,++Next,123,C.Direction.Read)))
                {Error(()=>root.BeginFactoryReader(input.Stream(C.Direction.Read),root.Call));var child=t.NewBuffer();Error(()=>t.Begin(child,Parent(),P.Wire.DcpbV1,root));Error(root.Finish);}
            });
            Test("shared-router-leaves-train-five-admission-intact",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());var input=t.NewBuffer(Train());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);
                using(var traversal=TT.Open(root,input,P.Kind.Command))
                {
                    R.BeforeCommand((ushort)5,out var frame);var value=new TrainCommand();R.AfterFactory(value,true,frame);Check(R.FactoryFinished(null,frame) is null);
                    var call=Call(value,input.Stream(C.Direction.Read),3);var plan=new B.Plan();TD.Prepare(call,value,input.Stream(C.Direction.Read),ref plan);value.Type=ContextUnitType.MimicWarrior;TD.Finish(call,plan);TD.Cleanup(call,plan,0);traversal.Complete(value);Check(P.Get(value,P.Kind.Command)==Paid());
                }
                root.Finish();
            });
            Test("later-build-failure-rolls-back-earlier-native-family-bindings",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var paid=t.NewCarrier(P.Kind.Command);var input=t.NewBuffer(Train().Concat(Smuggle()).Concat(new byte[9]).ToArray());var first=new BuildCommand();var last=new BuildAction();
                using(var root=t.Begin(input,Parent(),P.Wire.DcpbV1))
                {TrainRead(root,input,paid);Simulate(b,root,input,J.Kind.Command,supplied:first);Check(P.Get(paid.Value,paid.Kind)==Paid()&&!Port.IsUnbound(first));Error(()=>Simulate(b,root,input,J.Kind.Action,supplied:last));}
                Check(P.Get(paid.Value,paid.Kind) is null&&Port.IsUnbound(first)&&Port.IsUnbound(last));
            });
            Test("later-train-failure-rolls-back-earlier-factory-build",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var paid=t.NewCarrier(P.Kind.Command);var input=t.NewBuffer(Smuggle().Concat(new byte[]{255}).ToArray());var first=new BuildCommand();
                using(var root=t.Begin(input,Parent(),P.Wire.DcpbV1)){Simulate(b,root,input,J.Kind.Command,supplied:first);Check(!Port.IsUnbound(first));Error(()=>TrainRead(root,input,paid));}
                Check(Port.IsUnbound(first)&&P.Get(paid.Value,paid.Kind) is null);
            });
            Test("nested-success-retained-until-parent-failure",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var outer=t.NewBuffer();var value=new BuildAction();using(var root=t.Begin(outer,Parent(),P.Wire.DcpbV1))
                {var childBuffer=t.NewBuffer(Smuggle());using(var child=t.Begin(childBuffer,Parent(),P.Wire.DcpbV1,root)){Simulate(b,child,childBuffer,J.Kind.Action,supplied:value);child.Finish();}Check(!Port.IsUnbound(value)&&J.ActiveReservations==1);root.Root.Faulted=true;}
                Check(Port.IsUnbound(value));
            });
            foreach(string mode in new[]{"expired","changed-identity","failed-cast"})Test("factory-cursor-opaque-stream-"+mode,()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);
                using var reader=b.BeginFactoryReader(root,input,J.Kind.Command,new(1,++Next,123,C.Direction.Read));reader.Before(J.Kind.Command,1);long key=Native.Pointer(((ContextReader)input.Stream(C.Direction.Read)).BaseStream);
                if(mode=="expired")NativeLedgerOwnership.ExpiredKeys.Add(key);ContextWeakStream.ChangeIdentity=mode=="changed-identity";ContextWeakStream.ThrowCast=mode=="failed-cast";
                try{Error(()=>reader.ObserveReturn(new BuildCommand(),true));Check(J.ActiveReservations==0);}finally{NativeLedgerOwnership.ExpiredKeys.Remove(key);ContextWeakStream.ChangeIdentity=false;ContextWeakStream.ThrowCast=false;}
            });
            Test("foreign-buffer-cannot-open-build-window",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());var foreign=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);
                Error(()=>b.BeginFactoryReader(root,foreign,J.Kind.Command,new(1,++Next,123,C.Direction.Read)));Check(root.BuildFactoryWindow is null&&foreign.Native.Position==0);root.Finish();
            });
            Test("pending-root-fault-blocks-claim-before-weak-read",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);
                using var reader=b.BeginFactoryReader(root,input,J.Kind.Command,new(1,++Next,123,C.Direction.Read));reader.Before(J.Kind.Command,1);var value=new BuildCommand();reader.ObserveReturn(value,true);reader.After(null);root.Root.Faulted=true;
                int reads=NativeLedgerOwnership.TargetReads;Error(()=>reader.Ticket!.Claim(value,input.Stream(C.Direction.Read),reader.Call));Check(reads==NativeLedgerOwnership.TargetReads&&input.Native.Position==0);
            });
            foreach(string mismatch in new[]{"kind","version","cookie","receiver","alias"})Test("exact-ticket-claim-"+mismatch,()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());using var root=t.Begin(input,Parent(),P.Wire.DcpbV1);
                using var reader=b.BeginFactoryReader(root,input,J.Kind.Command,new(1,++Next,123,C.Direction.Read));reader.Before(J.Kind.Command,1);var value=new BuildCommand();reader.ObserveReturn(value,true);reader.After(null);
                var exact=reader.Call;var call=mismatch=="kind"?exact with{Site=2}:mismatch=="version"?exact with{Version=122}:mismatch=="cookie"?exact with{Cookie=exact.Cookie+1}:exact;
                object stream=mismatch=="alias"?new ContextReader(((ContextReader)input.Stream(C.Direction.Read)).BaseStream):input.Stream(C.Direction.Read);
                Error(()=>reader.Ticket!.Claim(mismatch=="receiver"?new BuildCommand():value,stream,call));Check(input.Native.Position==0&&!reader.Ticket!.Consumed&&Port.IsUnbound(value));
            });
            Test("duplicate-enlist-cookie-unreserves-only-new-carrier",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle().Concat(Smuggle()).ToArray());var first=b.NewCommand();var call=new C.Call(1,++Next,123,C.Direction.Read);
                using(var root=t.Begin(input,Parent(),P.Wire.DcpbV1))
                {
                    using(var lease=b.Read(root,first,input,call)){lease.RequireOriginal(first.Value,input.Stream(C.Direction.Read),call);((BuildCommand)first.Value).Type=BuildType.Smuggle;lease.FinishRead();}
                    using(var reader=b.BeginFactoryReader(root,input,J.Kind.Command,call)){reader.Before(J.Kind.Command,1);var later=new BuildCommand();Error(()=>reader.ObserveReturn(later,true));Check(J.ActiveReservations==1&&!Port.IsUnbound(first.Value)&&Port.IsUnbound(later));Error(()=>J.RejectUnscopedPointer(Native.Pointer(later)));}
                }
                Check(Port.IsUnbound(first.Value));
            });
            Test("completed-factory-binding-rolls-back-on-buffer-dispose-failure",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());var value=new BuildAction();var root=t.Begin(input,Parent(),P.Wire.DcpbV1);
                Simulate(b,root,input,J.Kind.Action,supplied:value);root.Finish();input.Native.FailDispose=true;Error(()=>root.Close(0));Check(Port.IsUnbound(value)&&J.ActiveReservations==0);
            });
            Test("stale-ticket-cannot-touch-new-binding",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());using var b=new J.Trial(t,Array.Empty<object>());var input=t.NewBuffer(Smuggle());var value=new BuildCommand();J.FactoryTicket ticket;
                using(var root=t.Begin(input,Parent(),P.Wire.DcpbV1))
                {using var reader=b.BeginFactoryReader(root,input,J.Kind.Command,new(1,++Next,123,C.Direction.Read));reader.Before(J.Kind.Command,1);reader.ObserveReturn(value,true);reader.After(null);ticket=reader.Ticket!;}
                value.Type=BuildType.Regroup;NativeExchangeCommands.Set(value,Intent());byte[] bytes=NativeExchangeCommands.Bytes(value);
                var next=t.NewBuffer(Smuggle());using var later=t.Begin(next,Parent(),P.Wire.DcpbV1);Error(()=>ticket.Claim(value,next.Stream(C.Direction.Read),ticket.Reader.Call));Check(NativeExchangeCommands.Bytes(value).SequenceEqual(bytes));later.Finish();
            });
            Test("owned-carrier-snapshot-is-defensive-and-includes-failed-return",()=>
            {
                using var t=new C.Trial(Array.Empty<object>());var b=new J.Trial(t,Array.Empty<object>());var value=new BuildCommand();var input=t.NewBuffer(Smuggle());
                using(var root=t.Begin(input,Parent(),P.Wire.DcpbV1)){using var traversal=T.Open(b,root,input,J.Kind.Command);var frame=Before(J.Kind.Command);R.AfterFactory(value,true,frame);R.FactoryFinished(null,frame);}
                var snapshot=b.SnapshotOwnedCarriers();Check(snapshot.Length==1&&Native.Same(snapshot[0].Value,value));snapshot[0]=null!;Check(b.SnapshotOwnedCarriers()[0] is not null);b.Dispose();Error(()=>b.SnapshotOwnedCarriers());Check(Port.IsUnbound(value));
            });
            string path=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT")!;File.WriteAllText(path,JsonSerializer.Serialize(new{passed=Results.Count-Failed,failed=Failed,assertions=Assertions,results=Results}));return Failed==0?0:1;
        }
    }
}
