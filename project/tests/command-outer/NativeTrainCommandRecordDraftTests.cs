// CLR model of native objects and original calls. Actual draft/context/adapter/
// dispatcher run below; no IL2CPP code, detour, factory hook or save is executed.
using System;
using System.Buffers.Binary;
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
using D=Deception.PolyModAdapter.NativeTrainLeafDispatcher;
using F=Deception.Core.NativeTrainCommandFrame;
using R=Deception.PolyModAdapter.NativeTrainCommandRecord;

public enum CommandType:ushort { Train=5 }
public static class GameState
{
    public static TrainCommand? GetCommand(CommandType type)
    {
        if(type!=CommandType.Train)throw new InvalidDataException("Wrong model discriminator.");
        RecordMock.FactoryCalls++;
        if(RecordMock.ThrowFactory)throw new InvalidOperationException("model factory failure");
        var value=RecordMock.FactoryOverride??new TrainCommand();
        if(RecordMock.NullFactory)return null;
        RecordMock.FactoryResults.Add(value);return value;
    }
}
namespace Il2CppSystem.IO
{
    public sealed class BinaryReader:ContextReader {public BinaryReader(ContextStream stream):base(stream){}}
    public sealed class BinaryWriter:ContextWriter {public BinaryWriter(ContextStream stream):base(stream){}}
}
namespace Deception.PolyModAdapter
{
    internal static class NativeStateBridge
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Call
        {internal uint Size;internal int Operation,Version;internal uint Site;internal IntPtr State,Stream,MethodInfo;}
        [StructLayout(LayoutKind.Sequential)] internal struct Plan {internal ulong Cookie;internal IntPtr Replacement;}
    }
}
internal static class RecordMock
{
    internal static int FactoryCalls,LeafCalls;internal static bool NullFactory,ThrowFactory,ThrowOriginal,SkipHook,FailReadDispose,FailWriteDispose;
    internal static TrainCommand? FactoryOverride;
    internal static readonly List<TrainCommand> FactoryResults=new();
    internal static readonly Dictionary<long,ContextWriter> Writers=new();
    internal static void Reset()
    {FactoryCalls=LeafCalls=0;NullFactory=ThrowFactory=ThrowOriginal=SkipHook=FailReadDispose=FailWriteDispose=false;FactoryOverride=null;FactoryResults.Clear();}
    internal static void WriteModel(TrainCommand v,Stream stream)
    {using var w=new System.IO.BinaryWriter(stream,System.Text.Encoding.UTF8,true);w.Write((ushort)v.Type);w.Write(v.PlayerId);w.Write(v.Coordinates.X);w.Write(v.Coordinates.Y);}
    internal static void ReadModel(TrainCommand v,Stream stream)
    {using var r=new System.IO.BinaryReader(stream,System.Text.Encoding.UTF8,true);v.Type=(ContextUnitType)r.ReadUInt16();v.PlayerId=r.ReadByte();v.Coordinates=new(){X=r.ReadInt32(),Y=r.ReadInt32()};}
    internal static void Leaf(TrainCommand value,ContextObject stream,int version,bool reading)
    {
        LeafCalls++;
        if(SkipHook)return;
        var call=new B.Call{Size=40,Site=3,Operation=reading?1:0,Version=version,State=value.Pointer,Stream=stream.Pointer,MethodInfo=new(718)};
        var plan=new B.Plan();int outcome=1;
        try
        {
            int result=D.Prepare(call,value,stream,ref plan);if(result is not (0 or 1))throw new InvalidDataException("model bridge rejected");
            outcome=4;if(ThrowOriginal)throw new IOException("model original failure");
            if(reading)ReadModel(value,((ContextReader)stream).BaseStream.Bytes);
            else WriteModel(value,(plan.Replacement!=IntPtr.Zero?Writers[plan.Replacement.ToInt64()]:((ContextWriter)stream)).BaseStream.Bytes);
            outcome=3;D.Finish(call,plan);outcome=0;
        }
        finally{D.Cleanup(call,plan,outcome);}
    }
}
namespace Deception.Tests
{
    public static class NativeTrainCommandRecordDraftTests
    {
        static readonly Guid Epoch=new("a144a273-cd26-49da-821f-1c28106a35f9");
        static readonly List<object> Results=new();static int Failed,Assertions;
        static void Check(bool ok,string why="Outer command invariant failed") {Assertions++;if(!ok)throw new Exception(why);}
        static void Reject(Action action,Type? exact=null)
        {try{action();}catch(Exception e)when(exact is null?e is InvalidDataException or AggregateException:e.GetType()==exact){Assertions++;return;}throw new Exception("Expected rejection");}
        static void Test(string id,Action action)
        {
            RecordMock.Reset();
            try{action();Check(C.CurrentDepth==0&&C.ActiveReservations==0&&!C.HasMutationPermit&&A.CurrentDepth==0,"Leaked authority");Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}
            catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e);}
            finally{RecordMock.Reset();}
        }
        static NativePaidBirthIntent Intent(string rule=NativePaidBirthIntentCodec.Warrior)=>new(Epoch,1,0,1,1,1,rule,4,5,20,3);
        static C.Carrier Carrier(C.Trial trial,ContextUnitType type=ContextUnitType.MimicWarrior,bool bound=true)
        {
            var value=trial.NewCarrier(P.Kind.Command);trial.Mutate(value,x=>((TrainCommand)x).Type=type);
            if(bound)P.Set(value.Value,value.Kind,Intent(type==ContextUnitType.MimicRider?NativePaidBirthIntentCodec.Rider:NativePaidBirthIntentCodec.Warrior));return value;
        }
        static byte[] Wire()
        {using var trial=new C.Trial(Array.Empty<object>());return R.Write(trial,Carrier(trial),Epoch);}
        static void NoSurvivingFactoryBinding()=>Check(RecordMock.FactoryResults.All(v=>P.Get(v,P.Kind.Command) is null),"Rejected record retained an attachment");
        static byte[] BodyChange(byte[] frame,Func<byte[],byte[]> change)=>F.Encode(Epoch,change(F.Decode(frame).CopyBody()));
        public static int Run()
        {
            Test("outer-negative-marker-and-fixed-layout",()=>
            {
                byte[] bytes=F.Encode(Epoch,new byte[]{5,0,9});
                Check(unchecked((int)F.Magic)<0&&F.HeaderBytes==72&&F.Capability==1&&F.CommandKind==1);
                Check(bytes.Length==75&&Convert.ToHexString(bytes.AsSpan(0,24))=="435443D0010048007B000000010001000100000003000000");
                Check(bytes.AsSpan(24,16).SequenceEqual(Epoch.ToByteArray())&&F.Decode(bytes).Epoch==Epoch);
                for(int version=51;version<=123;version++){byte[] old=(byte[])bytes.Clone();BinaryPrimitives.WriteInt32LittleEndian(old,version);Reject(()=>F.Decode(old));}
            });
            Test("outer-owned-decode-and-exact-roundtrip",()=>
            {
                byte[] body={5,0,9};byte[] bytes=F.Encode(Epoch,body);var decoded=F.Decode(bytes);body[2]=1;bytes[^1]=2;
                var first=decoded.CopyBody();first[2]=4;Check(decoded.CopyBody().SequenceEqual(new byte[]{5,0,9}));
            });
            Test("outer-every-truncation-and-trailing-byte",()=>
            {byte[] wire=Wire();for(int i=0;i<wire.Length;i++)Reject(()=>F.Decode(wire.Take(i).ToArray()));Reject(()=>F.Decode(wire.Concat(new byte[]{0}).ToArray()));});
            Test("outer-every-bit-location-authenticated",()=>
            {byte[] wire=Wire();for(int i=0;i<wire.Length;i++){var bad=(byte[])wire.Clone();bad[i]^=0x40;Reject(()=>F.Decode(bad));}});
            Test("outer-length-overflow-schema-capability-count-and-epoch",()=>
            {
                byte[] wire=Wire();foreach(int length in new[]{int.MinValue,-1,0,1,4099,int.MaxValue}){var bad=(byte[])wire.Clone();BinaryPrimitives.WriteInt32LittleEndian(bad.AsSpan(20),length);Reject(()=>F.Decode(bad));}
                Reject(()=>F.Encode(Guid.Empty,new byte[]{5,0}));Reject(()=>F.Encode(Epoch,new byte[]{5}));Reject(()=>F.Encode(Epoch,new byte[4099]));Reject(()=>F.Encode(Epoch,new byte[]{4,0}));
                var large=new byte[4098];large[0]=5;Check(F.Decode(F.Encode(Epoch,large)).CopyBody().Length==4098);
                foreach(int offset in new[]{4,6,8,12,14,16,24}){var bad=(byte[])wire.Clone();bad[offset]=0;Reject(()=>F.Decode(bad));}
            });
            foreach(var type in new[]{ContextUnitType.Warrior,ContextUnitType.MimicWarrior,ContextUnitType.MimicRider})
            foreach(bool bound in (type==ContextUnitType.Warrior?new[]{false}:new[]{false,true}))
            Test("outer-private-roundtrip-"+type+"-"+bound,()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var original=Carrier(trial,type,bound);byte[] frame=R.Write(trial,original,Epoch);var read=R.Read(trial,frame,Epoch);
                Check(!Native.Same(original.Value,read.Value)&&read.FromFactory&&RecordMock.FactoryCalls==1);
                Check(((TrainCommand)read.Value).Type==type&&((TrainCommand)read.Value).Coordinates.X==4);
                Check(P.Get(read.Value,P.Kind.Command)==(bound?Intent(type==ContextUnitType.MimicRider?NativePaidBirthIntentCodec.Rider:NativePaidBirthIntentCodec.Warrior):null));
                Check(R.Write(trial,read,Epoch).SequenceEqual(frame)&&P.Get(original.Value,P.Kind.Command)==P.Get(read.Value,P.Kind.Command));
                byte[] body=F.Decode(frame).CopyBody();Check(body[0]==5&&body[1]==0);
                Check(type==ContextUnitType.Warrior?body.Length==13:body[13]==(bound?1:0));
            });
            Test("outer-wrong-epoch-before-native-work",()=>
            {
                byte[] wire=Wire();RecordMock.Reset();using var trial=new C.Trial(Array.Empty<object>());
                Reject(()=>R.Read(trial,wire,Guid.NewGuid()));Check(RecordMock.FactoryCalls==0&&RecordMock.LeafCalls==0);
                var carrier=Carrier(trial);Reject(()=>R.Write(trial,carrier,Guid.NewGuid()));Check(RecordMock.LeafCalls==0&&P.Get(carrier.Value,P.Kind.Command)==Intent());
            });
            Test("outer-checksummed-tail-rolls-back-decoded-binding",()=>
            {byte[] bad=BodyChange(Wire(),b=>b.Concat(new byte[]{123}).ToArray());RecordMock.Reset();using var trial=new C.Trial(Array.Empty<object>());C.Carrier? accepted=null;Reject(()=>accepted=R.Read(trial,bad,Epoch));Check(accepted is null&&RecordMock.FactoryCalls==1);NoSurvivingFactoryBinding();});
            Test("outer-checksummed-wrong-intent-epoch-rolls-back",()=>
            {byte[] bad=F.Encode(Guid.NewGuid(),F.Decode(Wire()).CopyBody());RecordMock.Reset();using var trial=new C.Trial(Array.Empty<object>());Reject(()=>R.Read(trial,bad,F.Decode(bad).Epoch));NoSurvivingFactoryBinding();});
            Test("outer-checksummed-unknown-tag-and-short-payload",()=>
            {
                byte[] wire=Wire();foreach(var bad in new[]{BodyChange(wire,b=>{b[13]=255;return b;}),BodyChange(wire,b=>b[..^1])})
                {RecordMock.Reset();using var trial=new C.Trial(Array.Empty<object>());Reject(()=>R.Read(trial,bad,Epoch));NoSurvivingFactoryBinding();}
            });
            Test("outer-bare-legacy-and-magic-inside-leaf-never-infer-mode",()=>
            {
                byte[] frame=Wire(),body=F.Decode(frame).CopyBody();RecordMock.Reset();using var trial=new C.Trial(Array.Empty<object>());
                Reject(()=>R.Read(trial,body,Epoch));var legacy=new byte[body.Length+4];BinaryPrimitives.WriteInt32LittleEndian(legacy,123);body.CopyTo(legacy,4);
                Reject(()=>R.Read(trial,legacy,Epoch));Check(RecordMock.FactoryCalls==0&&RecordMock.LeafCalls==0);
                var value=Carrier(trial,ContextUnitType.Warrior,false);trial.Mutate(value,x=>((TrainCommand)x).Coordinates.X=unchecked((int)F.Magic));
                var result=R.Read(trial,R.Write(trial,value,Epoch),Epoch);Check(((TrainCommand)result.Value).Coordinates.X==unchecked((int)F.Magic)&&P.Get(result.Value,P.Kind.Command) is null);
            });
            Test("outer-checksummed-native-field-intent-conflict-rejected",()=>
            {
                byte[] bad=BodyChange(Wire(),b=>{b[5]^=1;return b;});RecordMock.Reset();using var trial=new C.Trial(Array.Empty<object>());
                Reject(()=>R.Read(trial,bad,Epoch));NoSurvivingFactoryBinding();
            });
            Test("outer-private-protected-and-reused-factory-rejected",()=>
            {
                byte[] wire=Wire();var protectedValue=new TrainCommand{Type=ContextUnitType.MimicWarrior};P.Set(protectedValue,P.Kind.Command,Intent());
                try{using var trial=new C.Trial(new object[]{protectedValue});RecordMock.FactoryOverride=protectedValue;Reject(()=>R.Read(trial,wire,Epoch));Check(P.Get(protectedValue,P.Kind.Command)==Intent());}
                finally{P.Forget(protectedValue,P.Kind.Command);}
                RecordMock.Reset();using var fresh=new C.Trial(Array.Empty<object>());var read=R.Read(fresh,wire,Epoch);RecordMock.FactoryOverride=(TrainCommand)read.Value;
                Reject(()=>R.Read(fresh,wire,Epoch));Check(P.Get(read.Value,P.Kind.Command)==Intent());
            });
            Test("outer-null-and-throwing-factory-return-no-result",()=>
            {
                byte[] wire=Wire();using var trial=new C.Trial(Array.Empty<object>());RecordMock.NullFactory=true;Reject(()=>R.Read(trial,wire,Epoch));
                RecordMock.NullFactory=false;RecordMock.ThrowFactory=true;Reject(()=>R.Read(trial,wire,Epoch),typeof(InvalidOperationException));NoSurvivingFactoryBinding();
            });
            Test("outer-original-failure-and-missing-hook-cannot-return",()=>
            {
                byte[] wire=Wire();using var trial=new C.Trial(Array.Empty<object>());RecordMock.ThrowOriginal=true;Reject(()=>R.Read(trial,wire,Epoch),typeof(IOException));NoSurvivingFactoryBinding();
                RecordMock.ThrowOriginal=false;RecordMock.SkipHook=true;Reject(()=>R.Read(trial,wire,Epoch));NoSurvivingFactoryBinding();
            });
            Test("outer-buffer-disposal-failure-rolls-back-and-withholds-result",()=>
            {
                byte[] wire=Wire();RecordMock.Reset();using var trial=new C.Trial(Array.Empty<object>());RecordMock.FailReadDispose=true;C.Carrier? accepted=null;
                Reject(()=>accepted=R.Read(trial,wire,Epoch));Check(accepted is null);NoSurvivingFactoryBinding();RecordMock.FailReadDispose=false;
                var carrier=Carrier(trial);RecordMock.FailWriteDispose=true;byte[]? output=null;Reject(()=>output=R.Write(trial,carrier,Epoch));Check(output is null&&P.Get(carrier.Value,P.Kind.Command)==Intent());
            });
            Test("outer-cross-trial-action-and-off-thread-denied",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());using var other=new C.Trial(Array.Empty<object>());var carrier=Carrier(trial);
                Reject(()=>R.Write(other,carrier,Epoch));Reject(()=>R.Write(trial,trial.NewCarrier(P.Kind.Action),Epoch));
                System.Threading.Tasks.Task.Run(()=>Reject(()=>R.Write(trial,carrier,Epoch))).GetAwaiter().GetResult();Check(RecordMock.LeafCalls==0);
            });
            string path=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT")??throw new Exception("Missing output");
            File.WriteAllText(path,JsonSerializer.Serialize(new{passed=Results.Count-Failed,failed=Failed,assertions=Assertions,results=Results},new JsonSerializerOptions{WriteIndented=true}));return Failed==0?0:1;
        }
    }
}
