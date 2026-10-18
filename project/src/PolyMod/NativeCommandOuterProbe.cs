using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Deception.Core;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using A=Deception.PolyModAdapter.NativeCommandOuterAdapter;
using B=Deception.PolyModAdapter.NativeCommandOuterAbi;
using F=Deception.Core.NativeTrainCommandFrame;
using R=Deception.PolyModAdapter.NativeTrainCommandRecord;
using Report=Deception.PolyModAdapter.NativeStateProbe.Report;
namespace Deception.PolyModAdapter
{
    internal static class NativeCommandOuterProbe
    {
        internal static bool Requested=>Environment.GetEnvironmentVariable("DECEPTION_NATIVE_COMMAND_OUTER_PROBE")=="1";
        static bool Attempted;
        static readonly Guid Epoch=new("2248edb3-3815-4ccb-b344-e4e7e5f5de90");
        static readonly string[] Rules={"warrior",NativePaidBirthIntentCodec.Warrior,NativePaidBirthIntentCodec.Rider};
        static Type BytesType=>Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
        static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        static void Require(bool value,string why){if(!value)throw new InvalidDataException("Typed command probe: "+why);}
        static void Check(Report r,bool value,string why){Require(value,why);r.Checks.Add(why);}
        static void Seed(object value,string rule,byte actor)
        {
            Native.Set(value,"Type",Native.EnumValue("Polytopia.Data.UnitData+Type",rule));Native.Set(value,"PlayerId",actor);
            Native.Set(value,"Coordinates",Native.T("WorldCoordinates").GetConstructor(new[]{typeof(int),typeof(int)})!.Invoke(new object[]{4,5}));
        }
        static object Write(object input,int version)=>Native.Invoke(Native.M("CommandBase","ToByteArray","CommandBase","System.Int32"),null,Native.As(input,"CommandBase"),version)!;
        static object Read(object input,out int version)
        {
            // The generated wrapper converts its version byref to a native
            // pointer. Use this frame's local storage, not reflection's boxed
            // copyback cell, across the allocating reverse-P/Invoke callbacks.
            // Do not infer or repair the returned version from the frame.
            var bytes=(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>)input;
            bool success=global::CommandBase.FromByteArray(bytes,out var command,out int nativeVersion);
            GC.KeepAlive(bytes);
            Require(success&&command is not null,"Native FromByteArray did not return a command.");version=nativeVersion;return command!;
        }
        static Exception? Failure(Action action){try{action();return null;}catch(Exception e){return e;}}
        static void NativeFailure(Report r,Action action,string label)
        {
            var error=Failure(action);Check(r,error?.GetType().FullName=="Il2CppInterop.Runtime.Il2CppException",label+"-actual-native-exception");
            r.Measurements[label+"-exception"]=error!.GetType().FullName!;
        }
        static void Idle(Report r,string label)=>Check(r,A.ActiveCount==0&&C.CurrentDepth==0&&C.ActiveReservations==0&&!C.HasMutationPermit&&NativeTrainLeafProbeAdapter.CurrentDepth==0&&NativeTrainFactoryTraversal.CurrentDepth==0,label+"-private-authority-empty");
        static byte[] FramedWrite(C.Trial trial,C.Carrier input)
        {
            using var invocation=A.Write(trial,input,Epoch);object result=Write(invocation.Input,F.Version);
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            invocation.RequireReturned(result);return NativeStateProbe.ManagedBytes(result);
        }
        static object FramedRead(Report report,C.Trial trial,byte[] bytes,string label)
        {
            using var invocation=A.Read(trial,bytes,Epoch);object result=Read(invocation.Input,out int version);
            report.Measurements[label+"-before-gc"]=invocation.ReturnEvidence(result,version);
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
            report.Measurements[label+"-after-gc"]=invocation.ReturnEvidence(result,version);
            return invocation.RequireReturned(result,version);
        }
        internal static Report Run(object client,object source,Report leaves)
        {
            var report=new Report{Scope="Actual typed CommandBase byte-array helpers with caller-owned private records and result roots",
                Limit="No Execute, live history/state/container transport, paid birth, Regroup/Disguise, save/load or replay claim. Generated wrapper exceptions do not prove seeded native output-slot clearing; that has only separate C++ harness evidence."};
            NativeTrainLeafProbe.SourceWitness? witness=null;C.Trial? trial=null;
            var baseline=new List<(string Rule,object Value,byte[] Bytes)>();
            long prepared=B.Prepared,finished=B.Finished,cleaned=B.Cleaned,rejected=B.Rejected;
            try
            {
                Require(Requested&&!Attempted&&!NativeCommandOuterBridge.Installed&&!NativeFreshSession.Enabled,"Invalid/repeated mode.");Attempted=true;
                witness=new(client,source);witness.Describe(report);byte actor=Convert.ToByte(Native.Get(source,"CurrentPlayer"));
                // Raw generated wrappers before either typed outer detour. These
                // new untagged objects are never admitted as private carriers.
                foreach(string rule in new[]{"warrior"})
                {
                    object command=Native.T("TrainCommand").GetConstructor(Type.EmptyTypes)!.Invoke(Array.Empty<object>());Seed(command,rule,actor);
                    byte[] bytes=NativeStateProbe.ManagedBytes(Write(command,123));baseline.Add((rule,command,bytes));
                    object copy=Read(NativeStateProbe.NativeBytes(bytes),out int version);
                    Check(report,version==123&&NativeStateProbe.ManagedBytes(Write(copy,version)).SequenceEqual(bytes),rule+"-raw-native-outer-roundtrip");
                    report.Measurements[rule+"-raw-outer-sha256"]=Hash(bytes);GC.KeepAlive(copy);
                }
                NativeCommandOuterBridge.Install();Check(report,NativeCommandOuterBridge.Installed&&!NativeCommandOuterBridge.Faulted,"typed-native-outer-installed");
                trial=new(NativeProtectedCensus.Normalize(witness.Protected));
                foreach(var item in baseline)
                {
                    Check(report,NativeStateProbe.ManagedBytes(Write(item.Value,123)).SequenceEqual(item.Bytes),item.Rule+"-unrelated-legacy-write-parity");
                    object copy=Read(NativeStateProbe.NativeBytes(item.Bytes),out int version);
                    Check(report,version==123&&NativeStateProbe.ManagedBytes(Write(copy,version)).SequenceEqual(item.Bytes),item.Rule+"-unrelated-legacy-read-parity");GC.KeepAlive(copy);
                }
                ulong sequence=1;
                foreach(string rule in Rules)
                {
                    var carrier=trial.NewCarrier(P.Kind.Command);trial.Mutate(carrier,value=>Seed(value,rule,actor));
                    NativeFailure(report,()=>Write(carrier.Value,123),rule+"-never-armed-private-legacy-denied");Idle(report,rule+"-unscoped-denied");
                    byte[] legacy=NativeTrainLeafProbeDriver.Write(trial,carrier,P.Wire.LegacyNative);
                    Check(report,leaves.Measurements.TryGetValue("raw-Command-"+rule+"-sha256",out var raw)&&Hash(legacy)==raw,rule+"-private-prefix-matches-prehook-native-baseline");
                    byte[] frame=FramedWrite(trial,carrier);object decoded=FramedRead(report,trial,frame,rule+"-unbound");
                    Check(report,F.Decode(frame).CopyBody().Take(legacy.Length+2).SequenceEqual(BitConverter.GetBytes((ushort)5).Concat(legacy)),rule+"-framed-prefix-is-exact-native-record");
                    Check(report,P.Get(decoded,P.Kind.Command) is null&&R.Write(trial,trial.RequireCarrier(decoded,P.Kind.Command),Epoch).SequenceEqual(frame),rule+"-unbound-native-framed-roundtrip-after-gc");
                    if(rule!="warrior")
                    {
                        var intent=new NativePaidBirthIntent(Epoch,sequence++,0,1,1,actor,rule,4,5,20,3);P.Set(carrier.Value,P.Kind.Command,intent);
                        frame=FramedWrite(trial,carrier);decoded=FramedRead(report,trial,frame,rule+"-bound");
                        Check(report,P.Get(decoded,P.Kind.Command)==intent&&R.Write(trial,trial.RequireCarrier(decoded,P.Kind.Command),Epoch).SequenceEqual(frame),rule+"-bound-native-framed-roundtrip-after-gc");
                        var body=F.Decode(frame).CopyBody();byte[] corrupt=(byte[])frame.Clone();corrupt[^1]^=1;
                        foreach(var bad in new[]{("checksum",corrupt),("truncated-frame",frame.Take(frame.Length-1).ToArray()),
                            ("trailing-body",F.Encode(Epoch,body.Concat(new byte[]{0x5A}).ToArray())),
                            ("wrong-epoch",F.Encode(Guid.NewGuid(),body)),
                            ("unknown-tag",F.Encode(Epoch,body.Take(legacy.Length+2).Concat(new byte[]{255}).ToArray()))})
                        {
                            NativeFailure(report,()=>{using var invocation=A.Read(trial,bad.Item2,Epoch);_=Read(invocation.Input,out _);},rule+"-"+bad.Item1);
                            Idle(report,rule+"-"+bad.Item1);witness.Verify();
                        }
                        NativeFailure(report,()=>{using var invocation=A.Write(trial,carrier,Epoch);_=Write(invocation.Input,122);},rule+"-armed-version-downgrade");
                        Check(report,P.Get(carrier.Value,P.Kind.Command)==intent,rule+"-rejection-keeps-original-intent");
                    }
                    Idle(report,rule+"-complete");witness.Verify();
                }
                Check(report,B.Prepared-prepared==B.Cleaned-cleaned,"all-typed-outer-native-calls-cleaned-once");report.Passed=true;
            }
            catch(Exception e){report.Passed=false;report.Failure=e.ToString();}
            finally
            {
                try{trial?.Dispose();Idle(report,"final");witness?.Verify();report.Checks.Add("final-published-source-witness-unchanged");}
                catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+"\nCleanup/witness: "+e;}
                GC.KeepAlive(baseline);
                report.Measurements["typed-prepared-delta"]=(B.Prepared-prepared).ToString();report.Measurements["typed-finished-delta"]=(B.Finished-finished).ToString();
                report.Measurements["typed-cleaned-delta"]=(B.Cleaned-cleaned).ToString();report.Measurements["typed-rejected-delta"]=(B.Rejected-rejected).ToString();
            }
            return report;
        }
    }
}
