using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deception.Core;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using J=Deception.PolyModAdapter.NativeBuildRecordJournalDraft;
using Port=Deception.PolyModAdapter.NativeBuildAttachmentPortDraft;
using A=Deception.PolyModAdapter.NativeBuildLeafProbeAdapter;
using H=Deception.PolyModAdapter.NativeBuildFactoryHelperDriverDraft;
using F=Deception.PolyModAdapter.NativeBuildFactoryTraversalDraft;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using T=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;
using TH=Deception.PolyModAdapter.NativeTrainFactoryHelperDriver;
using B=Deception.PolyModAdapter.NativeStateBridge;
using Report=Deception.PolyModAdapter.NativeStateProbe.Report;
namespace Deception.PolyModAdapter
{
    // Explicit diagnostic only. Never installs hooks, Executes a command,
    // admits a live reader, changes a state/client field or touches saves.
    internal static class NativeBuildFactoryProbe
    {
        internal static bool Requested=>Environment.GetEnvironmentVariable("DECEPTION_NATIVE_BUILD_FACTORY_PROBE")=="1";
        static bool Attempted,Running;
        const int Version=123;
        const string IT="Polytopia.Data.ImprovementData+Type",NativeError="Il2CppInterop.Runtime.Il2CppException";
        static readonly Guid Epoch=new("1ea5c524-6df8-4607-9e9d-82c89fb3c6e7");
        readonly record struct Counts(long Prepared,long Finished,long Cleaned,long Rejected);
        readonly record struct Image(string Rule,byte Actor,int X,int Y,bool Deduct);
        sealed record Record(J.Kind Kind,string Rule,byte[] Body,int NativeLength,byte[] Suffix,Image Fields);
        static Counts Count()=>new(B.Prepared,B.Finished,B.Cleaned,B.Rejected);
        static C.Call Parent(C.Direction direction)=>new(0,T.NewCookie(),Version,direction);
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Build factory probe: "+why);}
        static void Check(Report r,bool ok,string why){Require(ok,why);r.Checks.Add(why);}
        static void Measure(Report r,string name,object? value)=>r.Measurements[name]=Convert.ToString(value,CultureInfo.InvariantCulture)??"null";
        static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        static Exception? Failure(Action action){try{action();return null;}catch(Exception e){return e;}}
        static object Coord(int x,int y)=>Native.T("WorldCoordinates").GetConstructor(new[]{typeof(int),typeof(int)})!.Invoke(new object[]{x,y});
        static void Seed(object value,J.Kind kind,string rule,byte actor)
        {
            Native.Set(value,"Type",Native.EnumValue(IT,rule));Native.Set(value,"PlayerId",actor);Native.Set(value,"Coordinates",Coord(4,5));
            if(kind==J.Kind.Action)Native.Set(value,"DeductCost",true);
        }
        static Image Fields(object value,J.Kind kind)
        {object at=Native.Get(value,"Coordinates")!;return new(Native.EnumName(Native.Get(value,"Type")!),Convert.ToByte(Native.Get(value,"PlayerId")),Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")),kind==J.Kind.Action&&(bool)Native.Get(value,"DeductCost")!);}
        static NativeExchangeIntent Intent(NativeExchangeKind kind,byte actor,ulong sequence)=>new(Epoch,sequence,7,2,actor,kind,kind==NativeExchangeKind.Disguise,
            NativePartnerReference.Existing,3,new(1,0,4,5),new(2,uint.MaxValue,6,5),Enumerable.Range(0,32).Select(x=>(byte)(x+1)).ToArray());
        static byte[] SetSuffix(J.Trial trial,J.Carrier carrier,string rule,byte actor,ulong sequence)
        {
            if(rule=="harvestfruit")return Array.Empty<byte>();
            byte[] suffix;
            if(rule.StartsWith(SmuggleCatalog.Prefix,StringComparison.Ordinal))
            {
                uint unit=rule.EndsWith("fruit",StringComparison.Ordinal)?0:rule.EndsWith("fish",StringComparison.Ordinal)?uint.MaxValue:42;
                using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(MimicSmuggleActions.PayloadMagic);writer.Write(MimicSmuggleActions.PayloadVersion);writer.Write(unit);suffix=stream.ToArray();
                trial.Mutate(carrier,value=>MimicSmuggleActions.PublishPrivateJournal(value,unit));
            }
            else
            {
                var kind=rule==NativeExchangeCatalog.Id(NativeExchangeKind.Regroup)?NativeExchangeKind.Regroup:NativeExchangeKind.Disguise;
                var intent=Intent(kind,actor,sequence);suffix=NativeExchangeIntentCodec.Encode(intent);trial.Mutate(carrier,value=>NativeExchangeCommands.Set(value,intent));
            }
            Require(Port.Capture(carrier.Value)!.CopyBytes().SequenceEqual(suffix),"Synthetic source attachment differs.");return suffix;
        }
        static void Idle(Report r,string label)=>Check(r,C.CurrentDepth==0&&C.ActiveReservations==0&&!C.HasMutationPermit&&J.ActiveReservations==0&&A.CurrentDepth==0&&T.CurrentDepth==0&&F.CurrentDepth==0&&NativeTrainFactoryTraversal.CurrentDepth==0,label+"-no-active-private-authority");
        static void Balanced(Report r,Counts before,string label,int calls,int finished,int rejected)
        {
            var after=Count();Check(r,after.Prepared-before.Prepared==calls&&after.Cleaned-before.Cleaned==calls&&after.Finished-before.Finished==finished&&after.Rejected-before.Rejected==rejected,label+"-exact-callback-counts");Idle(r,label);
        }
        static byte[] WriteBody(C.Trial streams,J.Carrier carrier)
        {
            var output=streams.NewBuffer();using var scope=streams.Begin(output,Parent(C.Direction.Write),P.Wire.DcpbV1);
            using(var invocation=A.Open(scope,carrier,output,C.Direction.Write))
            {
                Native.Invoke(Native.M("GameState",carrier.Kind==J.Kind.Command?"SerializeCommand":"SerializeAction",carrier.Kind==J.Kind.Command?"CommandBase":"ActionBase","Il2CppSystem.IO.BinaryWriter","System.Int32"),null,carrier.Value,output.Stream(C.Direction.Write),Version);
                invocation.RequireCompleted();
            }
            byte[] bytes=output.Snapshot();Require(bytes.Length is >=2 and <=4098&&BinaryPrimitives.ReadUInt16LittleEndian(bytes)==1,"Native Build helper discriminator/bound differs.");scope.Finish();return bytes;
        }
        static J.Carrier ReadBody(J.Trial builds,byte[] body,J.Kind kind,Report report,string label)
        {
            var input=builds.Streams.NewBuffer(body);J.Carrier result;
            using(var scope=builds.Streams.Begin(input,Parent(C.Direction.Read),P.Wire.DcpbV1))
            {
                result=H.ReadInScope(builds,scope,input,kind);
                Check(report,input.Native.Position==body.Length&&input.Native.Position==input.Native.Length&&result.FromFactory&&result.ReadAttempted,label+"-exact-helper-body-consumption-and-positive-factory-origin");scope.Finish();
            }
            return result;
        }
        static void NativeReject(Report report,NativeTrainLeafProbe.SourceWitness witness,J.Trial builds,Record record,byte[] body,string label)
        {
            witness.Verify();var before=Count();int ownedBefore=builds.SnapshotOwnedCarriers().Length;
            var error=Failure(()=>H.Read(builds,body,record.Kind,P.Wire.DcpbV1));Measure(report,label+"-exception",error?.GetType().FullName??"none");
            Check(report,error?.GetType().FullName==NativeError,label+"-actual-native-helper-rejection");
            var added=builds.SnapshotOwnedCarriers().Skip(ownedBefore).ToArray();
            Check(report,added.Length==1&&added[0].FromFactory&&Port.IsUnbound(added[0].Value),label+"-failed-factory-result-retained-private-and-unbound");
            Balanced(report,before,label,1,1,1);witness.Verify();
        }
        static byte[] TrainBody(C.Trial streams,C.Carrier carrier,P.Wire wire)
        {
            var output=streams.NewBuffer();using var scope=streams.Begin(output,Parent(C.Direction.Write),wire);
            using(var invocation=T.Open(scope,carrier.Value,carrier.Kind,output,C.Direction.Write))
            {
                Native.Invoke(Native.M("GameState","SerializeAction","ActionBase","Il2CppSystem.IO.BinaryWriter","System.Int32"),null,carrier.Value,output.Stream(C.Direction.Write),Version);invocation.RequireCompleted();
            }
            byte[] bytes=output.Snapshot();Require(bytes.Length is >=2 and <=4098&&BinaryPrimitives.ReadUInt16LittleEndian(bytes)==5,"Native Train helper discriminator/bound differs.");scope.Finish();return bytes;
        }
        static void DirectTrainRead(C.Scope parent,C.Carrier carrier,C.Buffer input)
        {
            using var invocation=T.Open(parent,carrier.Value,carrier.Kind,input,C.Direction.Read);
            Native.Invoke(Native.M("TrainAction","Deserialize","Il2CppSystem.IO.BinaryReader","System.Int32"),carrier.Value,input.Stream(C.Direction.Read),Version);invocation.RequireCompleted();
        }
        internal static Report Run(object client,object source,Report buildReport,Report trainReport)
        {
            var report=new Report{Scope="Private Build native Serialize/DeserializeCommand/Action with positive GetCommand/GetAction tickets and the shared Train journal",
                Limit="No Execute, live source/history receiver, state/client publication, new frame schema, save/load or replay. Synthetic Smuggle IDs, exchange identities and paid Train intent imply no native eligibility/payment/commit. Failed native readers may alter private base fields; only attachment rollback is claimed. Reverse mixed rollback uses a constructor-owned direct TrainAction reader; Build records use actual native factory helpers. Observer-loss/fault injection remains synthetic; no hook is removed."};
            if(Attempted||Running){report.Failure="Repeated/reentrant Build factory trial.";return report;}
            Attempted=true;Running=true;NativeTrainLeafProbe.SourceWitness? witness=null;C.Trial? streams=null;J.Trial? builds=null;
            J.Carrier[] allBuild=Array.Empty<J.Carrier>();var allTrain=new List<C.Carrier>();var initial=Count();
            try
            {
                Require(Requested&&NativeBuildLeafProbe.Requested&&NativeTrainOuterProbe.Requested&&buildReport.Passed&&trainReport.Passed&&B.BuildLeavesInstalled&&NativeTrainFactoryObserverHooks.Installed&&NativeTrainFactoryObserverHooks.BuildRouteInstalled&&!NativeFreshSession.Enabled,"Mode, prior native proofs or installed shared factory/Build bridge prerequisite.");
                NativeRegistryOrder.RequireCompatible();Require(RecruitmentHooks.Registered,"Recruitment registration absent.");
                Check(report,NativeRegistryIds.Entries.Count==38&&NativeRegistryIds.Entries.All(x=>Native.Int(Native.EnumValue(x.EnumType,x.Id))==x.Index),"all-38-native-registry-ids-retained");
                witness=new(client,source);witness.Describe(report);
                foreach(object value in witness.Protected)if(Native.T("CommandBase").IsInstanceOfType(value))Require(Native.Int(Native.Invoke(Native.M("CommandBase","GetCommandType"),value))!=1,"Published Build history cannot receive a private diagnostic writer lease.");
                object[] protectedValues=NativeProtectedCensus.Normalize(witness.Protected);streams=new(protectedValues);builds=new(streams,protectedValues);
                byte actor=Convert.ToByte(Native.Get(source,"CurrentPlayer"));ulong sequence=1;
                var records=new List<Record>();string[] rules=new[]{"harvestfruit"}.Concat(SmuggleCatalog.Harvests.Select(x=>x.Id)).Concat(new[]{NativeExchangeCatalog.Id(NativeExchangeKind.Regroup),NativeExchangeCatalog.Id(NativeExchangeKind.Disguise)}).ToArray();
                foreach(var kind in new[]{J.Kind.Command,J.Kind.Action})foreach(string rule in rules)
                {
                    string label=kind+"-"+rule;witness.Verify();var carrier=kind==J.Kind.Command?builds.NewCommand():builds.NewAction();builds.Mutate(carrier,value=>Seed(value,kind,rule,actor));
                    var fields=Fields(carrier.Value,kind);byte[] suffix=SetSuffix(builds,carrier,rule,actor,sequence++);var before=Count();byte[] body=WriteBody(streams,carrier);
                    int length=body.Length-2-suffix.Length;Require(length>0,"Native Build prefix absent.");byte[] native=body.Skip(2).Take(length).ToArray();
                    Check(report,buildReport.Measurements.TryGetValue("raw-"+label+"-sha256",out var expected)&&Hash(native)==expected&&body.Skip(2+length).SequenceEqual(suffix),label+"-actual-native-discriminator-prefix-prehook-hash-and-existing-suffix");
                    Check(report,Fields(carrier.Value,kind)==fields&&(suffix.Length==0?Port.IsUnbound(carrier.Value):Port.Capture(carrier.Value)!.CopyBytes().SequenceEqual(suffix)),label+"-writer-fields-and-attachment-unchanged");
                    var copy=ReadBody(builds,body,kind,report,label);
                    Check(report,Fields(copy.Value,kind)==fields&&(suffix.Length==0?Port.IsUnbound(copy.Value):Port.Capture(copy.Value)!.CopyBytes().SequenceEqual(suffix)),label+"-native-factory-fields-and-canonical-binding");
                    records.Add(new(kind,rule,body,length,suffix,fields));Measure(report,label+"-body-sha256",Hash(body));Balanced(report,before,label,2,2,0);witness.Verify();
                }
                foreach(var kind in new[]{J.Kind.Command,J.Kind.Action})
                {
                    var smuggle=records.Single(x=>x.Kind==kind&&x.Rule==SmuggleCatalog.Harvests[0].Id);byte[] bad=(byte[])smuggle.Body.Clone();bad[2+smuggle.NativeLength]^=0x80;
                    NativeReject(report,witness,builds,smuggle,bad,kind+"-invalid-smuggle-magic");
                    NativeReject(report,witness,builds,smuggle,smuggle.Body.Take(smuggle.Body.Length-1).ToArray(),kind+"-truncated-smuggle-suffix");
                    var exchange=records.Single(x=>x.Kind==kind&&x.Rule==NativeExchangeCatalog.Id(NativeExchangeKind.Regroup));byte other=actor==1?(byte)2:(byte)1;
                    byte[] actorSuffix=NativeExchangeIntentCodec.Encode(Intent(NativeExchangeKind.Regroup,other,sequence++));
                    Check(report,NativeExchangeIntentCodec.Decode(actorSuffix).Actor==other,kind+"-actor-mismatch-is-codec-valid");
                    NativeReject(report,witness,builds,exchange,exchange.Body.Take(2+exchange.NativeLength).Concat(actorSuffix).ToArray(),kind+"-native-base-actor-mismatch");
                    byte[] kindSuffix=NativeExchangeIntentCodec.Encode(Intent(NativeExchangeKind.Disguise,actor,sequence++));
                    Check(report,NativeExchangeIntentCodec.Decode(kindSuffix).Kind==NativeExchangeKind.Disguise,kind+"-kind-mismatch-is-codec-valid");
                    NativeReject(report,witness,builds,exchange,exchange.Body.Take(2+exchange.NativeLength).Concat(kindSuffix).ToArray(),kind+"-native-base-kind-mismatch");
                }
                // A single synthetic paid TrainAction supplies the other family.
                var train=streams.NewCarrier(P.Kind.Action);allTrain.Add(train);streams.Mutate(train,value=>
                {Native.Set(value,"Type",Native.EnumValue("Polytopia.Data.UnitData+Type",NativePaidBirthIntentCodec.Warrior));Native.Set(value,"PlayerId",actor);Native.Set(value,"Coordinates",Coord(4,5));Native.Set(value,"Cost",3);Native.Set(value,"LookCoordinates",Coord(6,7));Native.Set(value,"trainActionAborted",false);});
                var count=Count();byte[] legacy=TrainBody(streams,train,P.Wire.LegacyNative);
                Check(report,trainReport.Measurements.TryGetValue("raw-Action-"+NativePaidBirthIntentCodec.Warrior+"-sha256",out var raw)&&Hash(legacy.Skip(2).ToArray())==raw,"mixed-train-native-prefix-prehook-hash");
                var paid=new NativePaidBirthIntent(Epoch,1,0,1,1,actor,NativePaidBirthIntentCodec.Warrior,4,5,20,3);P.Set(train.Value,train.Kind,paid);byte[] bound=TrainBody(streams,train,P.Wire.DcpbV1);
                Check(report,bound.SequenceEqual(legacy.Concat(new byte[]{1}).Concat(NativePaidBirthIntentCodec.Encode(paid))),"mixed-train-native-helper-canonical-bytes");Balanced(report,count,"mixed-train-writer",2,2,0);
                var good=records.Single(x=>x.Kind==J.Kind.Command&&x.Rule==SmuggleCatalog.Harvests[0].Id);
                var last=records.Single(x=>x.Kind==J.Kind.Action&&x.Rule==NativeExchangeCatalog.Id(NativeExchangeKind.Regroup));byte[] malformed=(byte[])last.Body.Clone();malformed[2+last.NativeLength]^=0x80;
                J.Carrier? earlierBuild=null;C.Carrier? earlierTrain=null;count=Count();var outer=streams.NewBuffer();
                using(var parent=streams.Begin(outer,Parent(C.Direction.Read),P.Wire.DcpbV1))
                {
                    earlierTrain=TH.Read(streams,bound,P.Kind.Action,P.Wire.DcpbV1,parent);allTrain.Add(earlierTrain);
                    earlierBuild=H.Read(builds,good.Body,good.Kind,P.Wire.DcpbV1,parent);
                    Check(report,P.Get(earlierTrain.Value,earlierTrain.Kind)==paid&&!Port.IsUnbound(earlierBuild.Value)&&C.ActiveReservations==1&&J.ActiveReservations==1,"later-build-has-native-helper-siblings-bound-under-parent");
                    var error=Failure(()=>H.Read(builds,malformed,last.Kind,P.Wire.DcpbV1,parent));Measure(report,"later-build-exception",error?.GetType().FullName??"none");
                    Check(report,error?.GetType().FullName==NativeError&&parent.Root.Faulted,"later-build-native-helper-faults-shared-parent");
                }
                Check(report,P.Get(earlierTrain!.Value,earlierTrain.Kind) is null&&Port.IsUnbound(earlierBuild!.Value),"later-build-failure-retires-earlier-train-and-build");Balanced(report,count,"later-build-shared-rollback",3,3,1);witness.Verify();
                var failedTrain=streams.NewCarrier(P.Kind.Action);allTrain.Add(failedTrain);byte[] badTrain=legacy.Skip(2).Concat(new byte[]{255}).ToArray();outer=streams.NewBuffer(badTrain);count=Count();
                using(var parent=streams.Begin(outer,Parent(C.Direction.Read),P.Wire.DcpbV1))
                {
                    earlierBuild=H.Read(builds,last.Body,last.Kind,P.Wire.DcpbV1,parent);Check(report,!Port.IsUnbound(earlierBuild.Value)&&J.ActiveReservations==1,"later-train-has-native-build-helper-binding");
                    var error=Failure(()=>DirectTrainRead(parent,failedTrain,outer));Measure(report,"later-train-exception",error?.GetType().FullName??"none");
                    Check(report,error?.GetType().FullName==NativeError&&parent.Root.Faulted,"later-train-native-leaf-faults-shared-parent");
                }
                Check(report,Port.IsUnbound(earlierBuild!.Value)&&P.Get(failedTrain.Value,failedTrain.Kind) is null,"later-train-rejection-retires-earlier-factory-build");Balanced(report,count,"later-train-shared-rollback",2,2,1);witness.Verify();
                report.Passed=true;
            }
            catch(Exception error){report.Passed=false;report.Failure=error.ToString();}
            finally
            {
                void Cleanup(string label,Action action){try{action();}catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+"\n"+label+": "+e;}}
                Cleanup("Snapshot owned factory records",()=>{if(builds is not null)allBuild=builds.SnapshotOwnedCarriers();});
                Cleanup("Build Trial retirement",()=>builds?.Dispose());Cleanup("Train Trial retirement",()=>streams?.Dispose());
                Cleanup("Exact attachment retirement",()=>
                {Check(report,allBuild.All(x=>Port.IsUnbound(x.Value))&&allTrain.All(x=>P.Get(x.Value,x.Kind) is null),"all-owned-build-including-failed-factory-and-tracked-train-bindings-retired");Measure(report,"private-build-records-retired",allBuild.Length);Measure(report,"private-build-factory-records-retired",allBuild.Count(x=>x.FromFactory));Measure(report,"private-train-records-retired",allTrain.Count);});
                Cleanup("Final authority",()=>Idle(report,"final"));
                Cleanup("Published source",()=>{if(witness is not null){witness.Verify();report.Checks.Add("final-full-source-census-and-bytes-unchanged");}});
                var final=Count();Measure(report,"prepared-delta",final.Prepared-initial.Prepared);Measure(report,"finished-delta",final.Finished-initial.Finished);Measure(report,"cleaned-delta",final.Cleaned-initial.Cleaned);Measure(report,"rejected-delta",final.Rejected-initial.Rejected);
                Cleanup("Final callbacks",()=>Check(report,final.Prepared-initial.Prepared==final.Cleaned-initial.Cleaned,"every-native-helper-leaf-cleaned-once"));
                GC.KeepAlive(allBuild);GC.KeepAlive(allTrain);Running=false;
            }
            return report;
        }
    }
}
