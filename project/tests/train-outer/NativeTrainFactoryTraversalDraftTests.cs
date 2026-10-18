// CLR models invoke the actual observer callbacks and candidate dispatcher.
// No Harmony patch or game/native function is invoked by this suite.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using A=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;
using T=Deception.PolyModAdapter.NativeTrainFactoryTraversal;
using H=Deception.PolyModAdapter.NativeTrainFactoryHelperDriver;
public enum ActionType:ushort { Train=5 }
internal static class FactoryTraversalMock
{
    internal static string Mode="";internal static int OriginalReads;internal static object? LastFrame;
    internal static TrainCommand? Replacement;internal static ContextReader? CurrentReader;
    internal static Exception? Thrown;
    internal static readonly List<TrainCommand> Returned=new();
    internal static void Reset(){Mode="";OriginalReads=0;LastFrame=null;Replacement=null;CurrentReader=null;Thrown=null;Returned.Clear();RecordMock.Reset();}
    internal static TrainCommand Factory(P.Kind kind,ushort type)
    {
        object? state=null;Exception? error=null;TrainCommand? result=null;
        if(Mode is not ("missing-all" or "missing-prefix"))
        {
            if(kind==P.Kind.Command)T.BeforeCommand((CommandType)(Mode=="wrong-discriminator"?4:type),out state);
            else T.BeforeAction((ActionType)(Mode=="wrong-discriminator"?4:type),out state);
            LastFrame=state;
        }
        if(Mode=="duplicate-prefix")T.BeforeCommand((CommandType)type,out _);
        try
        {
            if(Mode=="factory-exception")throw Thrown=new IOException("Synthetic native factory exception");
            result=Replacement??(kind==P.Kind.Command?new TrainCommand():new TrainAction());Returned.Add(result);
            if(Mode is not ("missing-all" or "missing-postfix"))
            {
                if(Mode=="off-thread-postfix")System.Threading.Tasks.Task.Run(()=>T.AfterFactory(result,true,state)).GetAwaiter().GetResult();
                else T.AfterFactory(Mode=="null-return"?null:result,Mode!="original-skipped",state);
                if(Mode=="duplicate-postfix")T.AfterFactory(result,true,state);
            }
        }
        catch(Exception e){error=e;}
        finally
        {
            if(Mode is not ("missing-all" or "missing-finalizer"))
            {
                var same=T.FactoryFinished(error,state);if(!ReferenceEquals(same,error))throw new Exception("Observer replaced native exception");
                if(Mode=="duplicate-finalizer")T.FactoryFinished(error,state);
            }
        }
        if(error is not null)ExceptionDispatchInfo.Capture(error).Throw();
        if(Mode=="cursor-changed")CurrentReader!.BaseStream.Position++;
        return result!;
    }
    internal static object Read(P.Kind kind,Il2CppSystem.IO.BinaryReader reader,int version)
    {
        CurrentReader=reader;using var raw=new System.IO.BinaryReader(reader.BaseStream.Bytes,System.Text.Encoding.UTF8,true);
        ushort type=raw.ReadUInt16();var value=Factory(kind,type);
        var actual=Mode=="reader-alias"?new Il2CppSystem.IO.BinaryReader(reader.BaseStream):reader;
        value.Deserialize(actual,Mode=="wrong-version"?124:version);
        return Mode=="helper-changed-result"?new TrainCommand():value;
    }
}
namespace Deception.Tests
{
    public static class NativeTrainFactoryTraversalDraftTests
    {
        static readonly Guid Epoch=new("a144a273-cd26-49da-821f-1c28106a35f9");
        static readonly List<object> Results=new();static int Failed,Assertions;
        static void Check(bool ok,string why="Factory traversal invariant"){Assertions++;if(!ok)throw new Exception(why);}
        static Exception Error(Action action)
        {try{action();}catch(Exception e){Assertions++;return e;}throw new Exception("Expected rejection");}
        static void Clean()=>Check(C.CurrentDepth==0&&C.ActiveReservations==0&&!C.HasMutationPermit&&A.CurrentDepth==0&&T.CurrentDepth==0,"Leaked read authority");
        static void Test(string id,Action action)
        {
            FactoryTraversalMock.Reset();
            try{action();Clean();Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}
            catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e);}
            finally{FactoryTraversalMock.Reset();}
        }
        static NativePaidBirthIntent Intent()=>new(Epoch,1,0,1,1,1,NativePaidBirthIntentCodec.Warrior,4,5,20,3);
        static byte[] Body(P.Wire wire=P.Wire.DcpbV1,bool bound=true)
        {
            using var s=new MemoryStream();using(var w=new System.IO.BinaryWriter(s,System.Text.Encoding.UTF8,true))w.Write((ushort)5);
            RecordMock.WriteModel(new TrainCommand{Type=ContextUnitType.MimicWarrior},s);
            byte[] suffix=P.EncodeExtension(true,wire,123,bound?NativePaidBirthIntentCodec.Encode(Intent()):null);s.Write(suffix);return s.ToArray();
        }
        static void Unbound()=>Check(FactoryTraversalMock.Returned.All(x=>P.Get(x,x is TrainAction?P.Kind.Action:P.Kind.Command) is null),"Rejected helper retained private attachment");
        public static int Run()
        {
            foreach(var kind in new[]{P.Kind.Command,P.Kind.Action})foreach(bool bound in new[]{false,true})
                Test("factory-helper-positive-"+kind+"-"+bound,()=>
                {
                    using var trial=new C.Trial(Array.Empty<object>());var result=H.Read(trial,Body(bound:bound),kind,P.Wire.DcpbV1);
                    Check(result.FromFactory&&P.Get(result.Value,kind)==(bound?Intent():null)&&FactoryTraversalMock.OriginalReads==1);
                });
            foreach(var kind in new[]{P.Kind.Command,P.Kind.Action})Test("factory-helper-explicit-legacy-"+kind,()=>
            {using var trial=new C.Trial(Array.Empty<object>());var result=H.Read(trial,Body(P.Wire.LegacyNative,false),kind,P.Wire.LegacyNative);Check(P.Get(result.Value,kind) is null&&FactoryTraversalMock.OriginalReads==1);});
            foreach(string mode in new[]{"missing-all","missing-prefix","missing-postfix","missing-finalizer","duplicate-prefix","duplicate-postfix","duplicate-finalizer","off-thread-postfix","original-skipped","null-return","wrong-discriminator","cursor-changed","reader-alias","wrong-version"})
                Test("factory-helper-"+mode+"-before-native-read",()=>
                {
                    using var trial=new C.Trial(Array.Empty<object>());FactoryTraversalMock.Mode=mode;
                    Error(()=>H.Read(trial,Body(),P.Kind.Command,P.Wire.DcpbV1));Check(FactoryTraversalMock.OriginalReads==0);Unbound();
                });
            Test("factory-helper-original-error-identity-preserved",()=>
            {using var trial=new C.Trial(Array.Empty<object>());FactoryTraversalMock.Mode="factory-exception";var e=Error(()=>H.Read(trial,Body(),P.Kind.Command,P.Wire.DcpbV1));Check(ReferenceEquals(e,FactoryTraversalMock.Thrown)&&FactoryTraversalMock.OriginalReads==0);});
            Test("factory-helper-protected-return-preserves-binding",()=>
            {
                var value=new TrainCommand{Type=ContextUnitType.MimicWarrior};P.Set(value,P.Kind.Command,Intent());
                try{using var trial=new C.Trial(new object[]{value});FactoryTraversalMock.Replacement=value;Error(()=>H.Read(trial,Body(),P.Kind.Command,P.Wire.DcpbV1));Check(FactoryTraversalMock.OriginalReads==0&&P.Get(value,P.Kind.Command)==Intent());}
                finally{P.Forget(value,P.Kind.Command);}
            });
            Test("factory-helper-result-identity-and-eof-roll-back",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());FactoryTraversalMock.Mode="helper-changed-result";
                Error(()=>H.Read(trial,Body(),P.Kind.Command,P.Wire.DcpbV1));Unbound();FactoryTraversalMock.Reset();
                Error(()=>H.Read(trial,Body().Concat(new byte[]{3}).ToArray(),P.Kind.Command,P.Wire.DcpbV1));Unbound();
            });
            Test("factory-helper-native-leaf-failure-cleans-opened-invocation",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());RecordMock.ThrowOriginal=true;
                Error(()=>H.Read(trial,Body(),P.Kind.Command,P.Wire.DcpbV1));Unbound();RecordMock.ThrowOriginal=false;
                var bad=Body();bad[^1]^=1;Error(()=>H.Read(trial,bad,P.Kind.Action,P.Wire.DcpbV1));Unbound();
            });
            Test("factory-helper-parent-failure-retires-earlier-nested-results",()=>
            {
                using var trial=new C.Trial(Array.Empty<object>());var parentInput=trial.NewBuffer(Body());C.Carrier? accepted=null;
                using(var root=trial.Begin(parentInput,new(0,A.NewCookie(),123,C.Direction.Read),P.Wire.DcpbV1))
                {
                    accepted=H.Read(trial,Body(),P.Kind.Command,P.Wire.DcpbV1,root);
                    Check(P.Get(accepted.Value,P.Kind.Command)==Intent());
                    Error(()=>H.Read(trial,Body().Concat(new byte[]{42}).ToArray(),P.Kind.Action,P.Wire.DcpbV1,root));
                    // Deliberately unfinished root: all earlier child bindings retire.
                }
                Check(P.Get(accepted!.Value,P.Kind.Command) is null);Unbound();
            });
            Test("factory-helper-buffer-disposal-withholds-accepted-result",()=>
            {using var trial=new C.Trial(Array.Empty<object>());RecordMock.FailReadDispose=true;C.Carrier? value=null;Error(()=>value=H.Read(trial,Body(),P.Kind.Command,P.Wire.DcpbV1));Check(value is null);Unbound();});
            Test("factory-helper-unsupported-body-before-native-factory",()=>
            {using var trial=new C.Trial(Array.Empty<object>());foreach(byte[] bytes in new[]{Array.Empty<byte>(),new byte[]{4,0},new byte[4099]})Error(()=>H.Read(trial,bytes,P.Kind.Command,P.Wire.DcpbV1));Check(FactoryTraversalMock.Returned.Count==0);});
            Test("factory-observers-unscoped-and-stale-are-no-ops",()=>
            {
                T.BeforeCommand(CommandType.Train,out var none);Check(none is null);T.AfterFactory(new TrainCommand(),true,none);
                var error=new IOException("original");Check(ReferenceEquals(T.FactoryFinished(error,none),error));
                using var trial=new C.Trial(Array.Empty<object>());var value=H.Read(trial,Body(),P.Kind.Command,P.Wire.DcpbV1);object? old=FactoryTraversalMock.LastFrame;
                T.AfterFactory(new TrainCommand(),true,old);Check(ReferenceEquals(T.FactoryFinished(error,old),error));Check(P.Get(value.Value,P.Kind.Command)==Intent());
            });
            string path=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT")??throw new Exception("Missing output");
            File.WriteAllText(path,JsonSerializer.Serialize(new{passed=Results.Count-Failed,failed=Failed,assertions=Assertions,results=Results},new JsonSerializerOptions{WriteIndented=true}));return Failed==0?0:1;
        }
    }
}
