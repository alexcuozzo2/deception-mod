using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deception.Core;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using A=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;
using D=Deception.PolyModAdapter.NativeTrainLeafProbeDriver;
using H=Deception.PolyModAdapter.NativeTrainFactoryHelperDriver;
using F=Deception.Core.NativeTrainCommandFrame;
using R=Deception.PolyModAdapter.NativeTrainCommandRecord;
using B=Deception.PolyModAdapter.NativeStateBridge;
using Report=Deception.PolyModAdapter.NativeStateProbe.Report;
namespace Deception.PolyModAdapter
{
    // Separate opt-in private record/helper trial. The preceding immutable
    // leaf report supplies raw pre-detour baseline hashes for comparison.
    internal static class NativeTrainOuterProbe
    {
        internal static bool Requested=>Environment.GetEnvironmentVariable("DECEPTION_NATIVE_TRAIN_OUTER_PROBE")=="1";
        static bool Attempted;
        static readonly Guid Epoch=new("b9ac825d-17a6-42e1-a3e9-a8d4751f1a4f");
        const string NativeFailure="Il2CppInterop.Runtime.Il2CppException";
        static void Require(bool value,string why){if(!value)throw new InvalidDataException("Train outer probe: "+why);}
        static void Check(Report report,bool value,string why){Require(value,why);report.Checks.Add(why);}
        static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        static byte[] Body(byte[] leaf)=>BitConverter.GetBytes((ushort)5).Concat(leaf).ToArray();
        static object Coordinate(int x,int y)=>Native.T("WorldCoordinates").GetConstructor(new[]{typeof(int),typeof(int)})!.Invoke(new object[]{x,y});
        static void Seed(object value,P.Kind kind,string rule,byte actor)
        {
            Native.Set(value,"Type",Native.EnumValue("Polytopia.Data.UnitData+Type",rule));Native.Set(value,"PlayerId",actor);
            Native.Set(value,"Coordinates",Coordinate(4,5));
            if(kind==P.Kind.Action){Native.Set(value,"Cost",rule=="warrior"?2:3);Native.Set(value,"LookCoordinates",Coordinate(6,7));Native.Set(value,"trainActionAborted",false);}
        }
        static Exception? Failure(Action action){try{action();return null;}catch(Exception e){return e;}}
        static void Idle(Report report,string label)=>Check(report,C.CurrentDepth==0&&C.ActiveReservations==0&&!C.HasMutationPermit&&A.CurrentDepth==0&&NativeTrainFactoryTraversal.CurrentDepth==0,label+"-no-active-private-authority");
        internal static Report Run(object client,object source,Report leaves)
        {
            var report=new Report{Scope="Explicit private Train command frames and actual native DeserializeCommand/DeserializeAction factory traversal",
                Limit="No CommandBase.ToByteArray/FromByteArray interception, live history/state/container persistence, command Execute, paid birth, Regroup/Disguise or save/replay/multiplayer acceptance."};
            NativeTrainLeafProbe.SourceWitness? witness=null;C.Trial? trial=null;
            long prepared=B.Prepared,finished=B.Finished,cleaned=B.Cleaned,rejected=B.Rejected;
            try
            {
                Require(Requested&&!Attempted&&leaves.Passed&&B.Installed&&!NativeFreshSession.Enabled,"Missing preceding successful leaf trial or invalid mode.");Attempted=true;
                witness=new(client,source);witness.Describe(report);trial=new(NativeProtectedCensus.Normalize(witness.Protected));
                NativeTrainFactoryObserverHooks.Install();Check(report,NativeTrainFactoryObserverHooks.Installed,"exact-native-factory-observers-installed");
                byte actor=Convert.ToByte(Native.Get(source,"CurrentPlayer"));ulong sequence=1;
                foreach(var kind in new[]{P.Kind.Command,P.Kind.Action})foreach(string rule in new[]{"warrior",NativePaidBirthIntentCodec.Warrior,NativePaidBirthIntentCodec.Rider})
                {
                    string label=kind+"-"+rule;var original=trial.NewCarrier(kind);trial.Mutate(original,value=>Seed(value,kind,rule,actor));
                    byte[] legacy=D.Write(trial,original,P.Wire.LegacyNative);
                    Check(report,leaves.Measurements.TryGetValue("raw-"+label+"-sha256",out var raw)&&Hash(legacy)==raw,label+"-matches-prehook-raw-native-baseline");
                    var legacyCopy=H.Read(trial,Body(legacy),kind,P.Wire.LegacyNative);
                    Check(report,P.Get(legacyCopy.Value,kind) is null&&D.Write(trial,legacyCopy,P.Wire.LegacyNative).SequenceEqual(legacy),label+"-native-helper-legacy-roundtrip");
                    byte[] unbound=D.Write(trial,original,P.Wire.DcpbV1);var unboundCopy=H.Read(trial,Body(unbound),kind,P.Wire.DcpbV1);
                    Check(report,P.Get(unboundCopy.Value,kind) is null&&D.Write(trial,unboundCopy,P.Wire.DcpbV1).SequenceEqual(unbound),label+"-native-helper-v1-unbound-roundtrip");
                    if(kind==P.Kind.Command)
                    {
                        byte[] frame=R.Write(trial,original,Epoch);var decoded=F.Decode(frame);var record=R.Read(trial,frame,Epoch);
                        Check(report,decoded.Epoch==Epoch&&decoded.CopyBody().SequenceEqual(Body(unbound))&&P.Get(record.Value,kind) is null&&
                            D.Write(trial,record,P.Wire.DcpbV1).SequenceEqual(unbound),label+"-explicit-unbound-command-frame-roundtrip");
                    }
                    if(rule!="warrior")
                    {
                        var intent=new NativePaidBirthIntent(Epoch,sequence++,0,1,1,actor,rule,4,5,20,3);P.Set(original.Value,kind,intent);
                        byte[] bound=D.Write(trial,original,P.Wire.DcpbV1);var boundCopy=H.Read(trial,Body(bound),kind,P.Wire.DcpbV1);
                        Check(report,P.Get(boundCopy.Value,kind)==intent&&D.Write(trial,boundCopy,P.Wire.DcpbV1).SequenceEqual(bound),label+"-native-helper-bound-canonical-intent");
                        byte[] bad=Body(legacy.Concat(new byte[]{255}).ToArray());long before=B.Prepared;
                        var error=Failure(()=>H.Read(trial,bad,kind,P.Wire.DcpbV1));
                        Check(report,error?.GetType().FullName==NativeFailure&&B.Prepared==before+1,label+"-actual-native-helper-rejects-unknown-tag");
                        error=Failure(()=>H.Read(trial,Body(bound.Take(bound.Length-1).ToArray()),kind,P.Wire.DcpbV1));
                        Check(report,error?.GetType().FullName==NativeFailure,label+"-actual-native-helper-rejects-truncated-suffix");
                        C.Carrier? earlier=null;var rootBuffer=trial.NewBuffer(Array.Empty<byte>());
                        using(var parent=trial.Begin(rootBuffer,new(0,A.NewCookie(),123,C.Direction.Read),P.Wire.DcpbV1))
                        {
                            earlier=H.Read(trial,Body(bound),kind,P.Wire.DcpbV1,parent);
                            Check(report,P.Get(earlier.Value,kind)==intent&&C.ActiveReservations>0,label+"-earlier-native-helper-retains-parent-reservation");
                            error=Failure(()=>H.Read(trial,bad,kind,P.Wire.DcpbV1,parent));
                            Check(report,error?.GetType().FullName==NativeFailure&&parent.Root.Faulted,label+"-later-native-helper-faults-parent");
                        }
                        Check(report,earlier is not null&&P.Get(earlier.Value,kind) is null,label+"-outer-failure-retires-earlier-native-helper-intent");
                        if(kind==P.Kind.Command)
                        {
                            byte[] frame=R.Write(trial,original,Epoch);var record=R.Read(trial,frame,Epoch);
                            Check(report,P.Get(record.Value,kind)==intent&&F.Decode(frame).CopyBody().SequenceEqual(Body(bound)),label+"-explicit-bound-command-frame-roundtrip");
                            byte[] corrupt=(byte[])frame.Clone();corrupt[^1]^=1;before=B.Prepared;
                            error=Failure(()=>R.Read(trial,corrupt,Epoch));
                            Check(report,error is InvalidDataException&&B.Prepared==before,label+"-corrupt-frame-rejected-before-native-reader");
                            byte[] tail=F.Encode(Epoch,Body(bound).Concat(new byte[]{0x5A}).ToArray());
                            error=Failure(()=>R.Read(trial,tail,Epoch));
                            Check(report,error is InvalidDataException,label+"-checksummed-trailing-data-rejected");
                            Guid other=new("d59be1d3-39ae-4a9a-8daa-1a11a274d015");byte[] wrong=F.Encode(other,Body(bound));
                            error=Failure(()=>R.Read(trial,wrong,other));
                            Check(report,error is InvalidDataException,label+"-checksummed-intent-epoch-conflict-rejected");
                        }
                    }
                    Idle(report,label);witness.Verify();
                }
                Check(report,B.Prepared-prepared==B.Cleaned-cleaned,"every-outer-trial-native-leaf-cleaned-once");witness.Verify();report.Passed=true;
            }
            catch(Exception e){report.Passed=false;report.Failure=e.ToString();}
            finally
            {
                try{trial?.Dispose();Idle(report,"final");witness?.Verify();report.Checks.Add("final-full-published-source-witness-unchanged");}
                catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+"\nFinal cleanup/witness: "+e;}
                report.Measurements["bridge-prepared-delta"]=(B.Prepared-prepared).ToString();report.Measurements["bridge-finished-delta"]=(B.Finished-finished).ToString();
                report.Measurements["bridge-cleaned-delta"]=(B.Cleaned-cleaned).ToString();report.Measurements["bridge-rejected-delta"]=(B.Rejected-rejected).ToString();
            }
            return report;
        }
    }
}
