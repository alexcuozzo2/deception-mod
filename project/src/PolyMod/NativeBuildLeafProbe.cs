using System;
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
using T=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using D=Deception.PolyModAdapter.NativeTrainLeafProbeDriver;
using B=Deception.PolyModAdapter.NativeStateBridge;
using Report=Deception.PolyModAdapter.NativeStateProbe.Report;

namespace Deception.PolyModAdapter
{
    // Private diagnostic draft. No Execute, native factories, state Deserialize,
    // ledger, client publication, history mutation, save or research writes.
    internal static class NativeBuildLeafProbe
    {
        internal static bool Requested=>Environment.GetEnvironmentVariable("DECEPTION_NATIVE_BUILD_LEAF_PROBE")=="1";
        const int Version=123;
        const ushort Sentinel=0xA65C;
        const string IT="Polytopia.Data.ImprovementData+Type",NativeException="Il2CppInterop.Runtime.Il2CppException";
        static readonly Guid Epoch=new("5b36fb78-9815-4570-95f4-e40401314bde");
        static bool Attempted,Running;
        readonly record struct Counts(long Prepared,long Finished,long Cleaned,long Rejected);
        readonly record struct Image(string Type,byte Actor,int X,int Y,bool Deduct);
        sealed record Baseline(J.Kind Kind,string Rule,byte[] Bytes,Image Fields);
        static Counts Count()=>new(B.Prepared,B.Finished,B.Cleaned,B.Rejected);
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Build leaf probe: "+why);}
        static void Check(Report r,bool ok,string why){Require(ok,why);r.Checks.Add(why);}
        static void Measure(Report r,string key,object value)=>r.Measurements[key]=Convert.ToString(value,CultureInfo.InvariantCulture)??"null";
        static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        static string Owner(J.Kind kind)=>kind==J.Kind.Command?"BuildCommand":"BuildAction";
        static C.Call Parent(C.Direction direction)=>new(0,T.NewCookie(),Version,direction);
        static Exception? Failure(Action action){try{action();return null;}catch(Exception error){return error;}}
        static object Coord(int x,int y)=>Native.T("WorldCoordinates").GetConstructor(new[]{typeof(int),typeof(int)})!.Invoke(new object[]{x,y});
        static void Seed(object value,J.Kind kind,string rule,byte actor)
        {
            Native.Set(value,"Type",Native.EnumValue(IT,rule));Native.Set(value,"PlayerId",actor);Native.Set(value,"Coordinates",Coord(4,5));
            if(kind==J.Kind.Action)Native.Set(value,"DeductCost",true);
        }
        static Image Fields(object value,J.Kind kind)
        {
            object at=Native.Get(value,"Coordinates")!;
            return new(Native.EnumName(Native.Get(value,"Type")!),Convert.ToByte(Native.Get(value,"PlayerId")),Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")),
                kind==J.Kind.Action&&(bool)Native.Get(value,"DeductCost")!);
        }
        static NativeExchangeIntent Intent(NativeExchangeKind kind,byte actor,ulong sequence,bool swap=false)=>new(Epoch,sequence,7,2,actor,kind,swap,
            NativePartnerReference.Existing,3,new(1,0,4,5),new(2,uint.MaxValue,6,5),Enumerable.Range(0,32).Select(x=>(byte)(x+1)).ToArray());
        static byte[] Smuggle(uint unitId)
        {using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);writer.Write(MimicSmuggleActions.PayloadMagic);writer.Write(MimicSmuggleActions.PayloadVersion);writer.Write(unitId);return stream.ToArray();}
        static byte[] Suffix(J.Trial trial,J.Carrier carrier,string rule,byte actor,ulong sequence)
        {
            if(rule=="harvestfruit")return Array.Empty<byte>();
            byte[] suffix;
            if(rule.StartsWith(SmuggleCatalog.Prefix,StringComparison.Ordinal))
            {
                uint id=rule.EndsWith("fruit",StringComparison.Ordinal)?0:rule.EndsWith("fish",StringComparison.Ordinal)?uint.MaxValue:42;
                trial.Mutate(carrier,value=>MimicSmuggleActions.PublishPrivateJournal(value,id));suffix=Smuggle(id);
            }
            else
            {
                var kind=rule==NativeExchangeCatalog.Id(NativeExchangeKind.Regroup)?NativeExchangeKind.Regroup:NativeExchangeKind.Disguise;
                var intent=Intent(kind,actor,sequence,kind==NativeExchangeKind.Disguise);trial.Mutate(carrier,value=>NativeExchangeCommands.Set(value,intent));suffix=NativeExchangeIntentCodec.Encode(intent);
            }
            Require(Port.Capture(carrier.Value)!.CopyBytes().SequenceEqual(suffix),"Synthetic input binding differs from its canonical suffix.");return suffix;
        }
        static void Idle(Report report,string label)
        {
            Check(report,C.CurrentDepth==0&&C.ActiveReservations==0&&!C.HasMutationPermit&&J.ActiveReservations==0&&A.CurrentDepth==0&&T.CurrentDepth==0,
                label+"-no-active-private-authority");
        }
        static void Balanced(Report report,Counts before,string label,int calls,int finished,int rejected)
        {
            var after=Count();Check(report,after.Prepared-before.Prepared==calls&&after.Cleaned-before.Cleaned==calls&&after.Finished-before.Finished==finished&&after.Rejected-before.Rejected==rejected,
                label+"-exact-callback-counts");Idle(report,label);
        }
        static void Leaf(C.Scope parent,J.Carrier carrier,C.Buffer buffer,C.Direction direction)
        {
            using var invocation=A.Open(parent,carrier,buffer,direction);
            Native.Invoke(Native.M(Owner(carrier.Kind),direction==C.Direction.Read?"Deserialize":"Serialize",
                direction==C.Direction.Read?"Il2CppSystem.IO.BinaryReader":"Il2CppSystem.IO.BinaryWriter","System.Int32"),carrier.Value,buffer.Stream(direction),Version);
            invocation.RequireCompleted();
        }
        static byte[] Write(C.Trial streams,J.Carrier carrier)
        {
            var buffer=streams.NewBuffer();using var parent=streams.Begin(buffer,Parent(C.Direction.Write),P.Wire.DcpbV1);
            Leaf(parent,carrier,buffer,C.Direction.Write);byte[] result=buffer.Snapshot();Require(result.Length<=4096,"Build output record bound.");parent.Finish();return result;
        }
        static void Read(C.Trial streams,J.Carrier carrier,byte[] bytes,bool sentinel=true)
        {
            Require(bytes.Length<=4096,"Build input record bound.");byte[] input=sentinel?bytes.Concat(BitConverter.GetBytes(Sentinel)).ToArray():bytes;
            var buffer=streams.NewBuffer(input);using var parent=streams.Begin(buffer,Parent(C.Direction.Read),P.Wire.DcpbV1);
            Leaf(parent,carrier,buffer,C.Direction.Read);Require(buffer.Native.Position==bytes.Length,"Native Build record consumed the wrong number of bytes.");
            if(sentinel)Require(Convert.ToUInt16(buffer.Native.Read("UInt16"))==Sentinel,"Build overwrote/consumed following sentinel.");
            Require(buffer.Native.Position==buffer.Native.Length,"Trailing private Build bytes.");parent.Finish();
        }
        static void TrainRead(C.Scope parent,C.Carrier carrier,C.Buffer buffer)
        {
            using var invocation=T.Open(parent,carrier.Value,carrier.Kind,buffer,C.Direction.Read);
            Native.Invoke(Native.M(carrier.Kind==P.Kind.Command?"TrainCommand":"TrainAction","Deserialize","Il2CppSystem.IO.BinaryReader","System.Int32"),carrier.Value,buffer.Stream(C.Direction.Read),Version);
            invocation.RequireCompleted();
        }
        static void NativeFailure(Report report,NativeTrainLeafProbe.SourceWitness witness,string label,Action action,int finishes,int rejected)
        {
            witness.Verify();var before=Count();var error=Failure(action);Measure(report,label+"-exception",error?.GetType().FullName??"none");
            Check(report,error?.GetType().FullName==NativeException,label+"-actual-native-exception");Balanced(report,before,label,1,finishes,rejected);witness.Verify();
        }
        static void NoBuildHistory(NativeTrainLeafProbe.SourceWitness witness)
        {
            // Observing source bytes after Build hooks must not ask the private
            // dispatcher to grant a live history writer lease. Fresh sources
            // without Build history are sufficient for this diagnostic.
            foreach(object value in witness.Protected)
                if(Native.T("CommandBase").IsInstanceOfType(value))
                    Require(Native.Int(Native.Invoke(Native.M("CommandBase","GetCommandType"),value))!=1,"Source contains Build history; private proof must stop before detours.");
        }
        internal static Report Run(object client,object source,Report trainReport)
        {
            var report=new Report{Scope="Private constructor-owned Build command/action leaves sharing the Train root journal",
                Limit="No Execute, Build factory admission, live source/history receiver, state decode/publication, outer mixed format, save/load or replay. Smuggle unit IDs, exchange identities and paid Train intents are synthetic private transport values, not native units, valid gameplay eligibility, payment or commits. Native readers may change private base fields before rejecting a suffix; only attachment rollback is claimed."};
            if(Attempted||Running){report.Failure="Repeated/reentrant private Build probe.";return report;}
            Attempted=true;Running=true;NativeTrainLeafProbe.SourceWitness? witness=null;C.Trial? streams=null;J.Trial? builds=null;
            var ownedBuild=new List<J.Carrier>();var ownedTrain=new List<C.Carrier>();var rawOwned=new List<object>();var beforeAll=Count();
            try
            {
                Require(Requested&&Environment.GetEnvironmentVariable("DECEPTION_NATIVE_TRAIN_LEAF_PROBE")=="1"&&trainReport.Passed&&B.Installed&&!B.BuildLeavesInstalled&&!NativeFreshSession.Enabled,"Mode/preceding Train proof/installation prerequisite.");
                NativeRegistryOrder.RequireCompatible();Require(RecruitmentHooks.Registered,"Recruitment registration absent.");
                Check(report,NativeRegistryIds.Entries.Count==38&&NativeRegistryIds.Entries.All(x=>Native.Int(Native.EnumValue(x.EnumType,x.Id))==x.Index),"all-38-native-registry-ids-unchanged");
                witness=new(client,source);NoBuildHistory(witness);witness.Describe(report);object[] protectedValues=NativeProtectedCensus.Normalize(witness.Protected);
                streams=new(protectedValues);builds=new(streams,protectedValues);byte actor=Convert.ToByte(Native.Get(source,"CurrentPlayer"));
                J.Carrier New(J.Kind kind,string? rule=null)
                {
                    var carrier=kind==J.Kind.Command?builds.NewCommand():builds.NewAction();ownedBuild.Add(carrier);
                    if(rule is not null)builds.Mutate(carrier,value=>Seed(value,kind,rule,actor));return carrier;
                }
                C.Carrier NewTrain(P.Kind kind)
                {var carrier=streams.NewCarrier(kind);ownedTrain.Add(carrier);return carrier;}
                object Raw(J.Kind kind)
                {
                    object value=Native.T(Owner(kind)).GetConstructor(Type.EmptyTypes)!.Invoke(Array.Empty<object>());
                    Require(Native.Pointer(value)!=0&&!witness.Protected.Any(x=>Native.Same(x,value))&&!rawOwned.Any(x=>Native.Same(x,value))&&
                        !ownedBuild.Any(x=>Native.Same(x.Value,value))&&!ownedTrain.Any(x=>Native.Same(x.Value,value)),"Raw constructor aliases an existing/protected native identity.");
                    rawOwned.Add(value);return value;
                }
                string[] rules=new[]{"harvestfruit"}.Concat(SmuggleCatalog.Harvests.Select(x=>x.Id)).Concat(new[]{NativeExchangeCatalog.Id(NativeExchangeKind.Regroup),NativeExchangeCatalog.Id(NativeExchangeKind.Disguise)}).ToArray();
                var baseline=new List<Baseline>();
                foreach(var kind in new[]{J.Kind.Command,J.Kind.Action})foreach(string rule in rules)
                {
                    Require(!B.BuildLeavesInstalled,"Raw baseline must precede Build detours.");object value=Raw(kind);Seed(value,kind,rule,actor);var image=Fields(value,kind);
                    byte[] bytes=NativeStateProbe.Serialize(value,Owner(kind),Version);Require(bytes.Length is >0 and <4096,"Raw Build baseline bound.");
                    Check(report,Fields(value,kind)==image&&Port.IsUnbound(value),"raw-"+kind+"-"+rule+"-unchanged-unbound");baseline.Add(new(kind,rule,bytes,image));Measure(report,"raw-"+kind+"-"+rule+"-sha256",Hash(bytes));
                }
                witness.Verify();var install=Count();B.InstallBuildLeaves();Check(report,B.BuildLeavesInstalled&&Count()==install,"build-only-install-without-leaf-call");
                var records=new Dictionary<(J.Kind,string),byte[]>();ulong sequence=1;
                foreach(var row in baseline)
                {
                    string label=row.Kind+"-"+row.Rule;witness.Verify();var value=New(row.Kind,row.Rule);byte[] suffix=Suffix(builds,value,row.Rule,actor,sequence++);var before=Count();
                    byte[] bytes=Write(streams,value);Check(report,bytes.SequenceEqual(row.Bytes.Concat(suffix))&&Fields(value.Value,row.Kind)==row.Fields,label+"-native-prefix-exact-existing-suffix");
                    var copy=New(row.Kind);Read(streams,copy,bytes);Check(report,Fields(copy.Value,row.Kind)==row.Fields&&(suffix.Length==0?Port.IsUnbound(copy.Value):Port.Capture(copy.Value)!.CopyBytes().SequenceEqual(suffix)),label+"-native-read-fields-binding-and-sentinel");
                    Balanced(report,before,label+"-roundtrip",2,2,0);records.Add((row.Kind,row.Rule),bytes);
                    if(suffix.Length!=0)
                    {
                        var unbound=New(row.Kind,row.Rule);NativeFailure(report,witness,label+"-missing-write-binding",()=>Write(streams,unbound),0,1);
                        Check(report,Port.IsUnbound(unbound.Value)&&Fields(unbound.Value,row.Kind)==row.Fields,label+"-rejected-write-preserves-fields");
                        var shortCopy=New(row.Kind);NativeFailure(report,witness,label+"-short-suffix",()=>Read(streams,shortCopy,bytes.Take(bytes.Length-1).ToArray(),false),1,1);
                        Check(report,Port.IsUnbound(shortCopy.Value),label+"-short-read-unbound");
                        byte[] corrupt=(byte[])bytes.Clone();corrupt[row.Bytes.Length]^=0x80;var badCopy=New(row.Kind);
                        NativeFailure(report,witness,label+"-bad-suffix",()=>Read(streams,badCopy,corrupt,false),1,1);Check(report,Port.IsUnbound(badCopy.Value),label+"-bad-read-unbound");
                        byte[] prior=Port.Capture(copy.Value)!.CopyBytes();var priorFields=Fields(copy.Value,row.Kind);
                        NativeFailure(report,witness,label+"-existing-read-receiver-denied",()=>Read(streams,copy,bytes,false),0,1);
                        Check(report,Port.Capture(copy.Value)!.CopyBytes().SequenceEqual(prior)&&Fields(copy.Value,row.Kind)==priorFields,label+"-denied-reread-preserves-existing-binding");
                    }
                    witness.Verify();
                }
                foreach(var kind in new[]{J.Kind.Command,J.Kind.Action})
                {
                    var row=baseline.Single(x=>x.Kind==kind&&x.Rule=="harvestfruit");var before=Count();object plain=Raw(kind);Seed(plain,kind,"harvestfruit",actor);
                    Check(report,NativeStateProbe.Serialize(plain,Owner(kind),Version).SequenceEqual(row.Bytes),kind+"-unrelated-legacy-write-parity");object receiver=Raw(kind);Seed(receiver,kind,"harvestfruit",actor);
                    using(var buffer=new NativeStateProbe.Buffer(row.Bytes.Concat(BitConverter.GetBytes(Sentinel)).ToArray()))
                    {
                        Native.Invoke(Native.M(Owner(kind),"Deserialize","Il2CppSystem.IO.BinaryReader","System.Int32"),receiver,buffer.R,Version);
                        Check(report,buffer.Position==row.Bytes.Length&&Convert.ToUInt16(buffer.Read("UInt16"))==Sentinel&&buffer.Position==buffer.Length&&Fields(receiver,kind)==row.Fields&&Port.IsUnbound(receiver),kind+"-unrelated-legacy-read-sentinel");
                    }
                    Balanced(report,before,kind+"-unrelated-legacy",2,2,0);witness.Verify();
                    var eof=New(kind);NativeFailure(report,witness,kind+"-native-base-eof",()=>Read(streams,eof,Array.Empty<byte>(),false),0,0);Check(report,Port.IsUnbound(eof.Value),kind+"-eof-receiver-unbound");
                    var known=New(kind,"harvestfruit");NativeFailure(report,witness,kind+"-unscoped-owned-denied",()=>NativeStateProbe.Serialize(known.Value,Owner(kind),Version),0,1);
                }
                // Prepare one native TrainAction record. Its native base must
                // match the earlier pre-Train-detour report, not this writer's
                // own output alone. No Train action or payment is executed.
                var trainSource=NewTrain(P.Kind.Action);streams.Mutate(trainSource,value=>
                {
                    Native.Set(value,"Type",Native.EnumValue("Polytopia.Data.UnitData+Type",NativePaidBirthIntentCodec.Warrior));Native.Set(value,"PlayerId",actor);Native.Set(value,"Coordinates",Coord(4,5));
                    Native.Set(value,"Cost",3);Native.Set(value,"LookCoordinates",Coord(6,7));Native.Set(value,"trainActionAborted",false);
                });
                var paid=new NativePaidBirthIntent(Epoch,1,0,1,1,actor,NativePaidBirthIntentCodec.Warrior,4,5,20,3);
                var trainCount=Count();byte[] legacy=D.Write(streams,trainSource,P.Wire.LegacyNative);
                Check(report,trainReport.Measurements.TryGetValue("raw-Action-"+NativePaidBirthIntentCodec.Warrior+"-sha256",out string? expected)&&Hash(legacy)==expected,"mixed-train-prefix-matches-prehook-baseline");
                P.Set(trainSource.Value,trainSource.Kind,paid);byte[] trainBytes=D.Write(streams,trainSource,P.Wire.DcpbV1);
                Check(report,trainBytes.SequenceEqual(legacy.Concat(new byte[]{1}).Concat(NativePaidBirthIntentCodec.Encode(paid))),"mixed-train-complete-private-intent-bytes");Balanced(report,trainCount,"mixed-train-source",2,2,0);
                byte[] smuggle=records[(J.Kind.Command,SmuggleCatalog.Harvests[0].Id)];byte[] exchange=records[(J.Kind.Action,NativeExchangeCatalog.Id(NativeExchangeKind.Regroup))];
                var goodSmuggle=New(J.Kind.Command);var goodTrain=NewTrain(P.Kind.Action);var goodExchange=New(J.Kind.Action);var mixedCount=Count();
                var mixed=streams.NewBuffer(smuggle.Concat(trainBytes).Concat(exchange).Concat(BitConverter.GetBytes(Sentinel)).ToArray());
                using(var parent=streams.Begin(mixed,Parent(C.Direction.Read),P.Wire.DcpbV1))
                {
                    Leaf(parent,goodSmuggle,mixed,C.Direction.Read);TrainRead(parent,goodTrain,mixed);Leaf(parent,goodExchange,mixed,C.Direction.Read);
                    Check(report,J.ActiveReservations==2&&C.ActiveReservations==1&&!Port.IsUnbound(goodSmuggle.Value)&&!Port.IsUnbound(goodExchange.Value)&&P.Get(goodTrain.Value,goodTrain.Kind)==paid,"mixed-siblings-bound-under-shared-root");
                    Check(report,Failure(()=>MimicSmuggleActions.ForgetOffer(goodSmuggle.Value)) is InvalidDataException&&Failure(()=>NativeExchangeCommands.Forget(goodExchange.Value)) is InvalidDataException&&Failure(()=>P.Forget(goodTrain.Value,goodTrain.Kind)) is InvalidDataException,"mixed-live-reservations-reject-managed-mutation");
                    Check(report,Convert.ToUInt16(mixed.Native.Read("UInt16"))==Sentinel&&mixed.Native.Position==mixed.Native.Length,"mixed-siblings-exact-final-sentinel");parent.Finish();
                }
                Check(report,!Port.IsUnbound(goodSmuggle.Value)&&!Port.IsUnbound(goodExchange.Value)&&P.Get(goodTrain.Value,goodTrain.Kind)==paid,"mixed-success-stays-private-trial-owned");Balanced(report,mixedCount,"mixed-success",3,3,0);witness.Verify();
                // A failing later Build suffix rolls back both earlier families.
                byte[] badExchange=(byte[])exchange.Clone();int prefix=baseline.Single(x=>x.Kind==J.Kind.Action&&x.Rule==NativeExchangeCatalog.Id(NativeExchangeKind.Regroup)).Bytes.Length;badExchange[prefix]^=0x80;
                var firstTrain=NewTrain(P.Kind.Action);var firstSmuggle=New(J.Kind.Command);var lastExchange=New(J.Kind.Action);var failCount=Count();
                var failing=streams.NewBuffer(trainBytes.Concat(smuggle).Concat(badExchange).ToArray());
                using(var parent=streams.Begin(failing,Parent(C.Direction.Read),P.Wire.DcpbV1))
                {
                    TrainRead(parent,firstTrain,failing);Leaf(parent,firstSmuggle,failing,C.Direction.Read);
                    Check(report,P.Get(firstTrain.Value,firstTrain.Kind)==paid&&!Port.IsUnbound(firstSmuggle.Value),"later-build-failure-has-earlier-bound-siblings");
                    var error=Failure(()=>Leaf(parent,lastExchange,failing,C.Direction.Read));Measure(report,"later-build-failure-exception",error?.GetType().FullName??"none");
                    Check(report,error?.GetType().FullName==NativeException&&parent.Root.Faulted,"later-build-sibling-native-rejection");
                }
                Check(report,P.Get(firstTrain.Value,firstTrain.Kind) is null&&Port.IsUnbound(firstSmuggle.Value)&&Port.IsUnbound(lastExchange.Value),"later-build-failure-rolls-back-all-new-bindings");Balanced(report,failCount,"later-build-failure",3,3,1);witness.Verify();
                // Symmetric proof: a failing Train suffix retires an earlier Build.
                byte[] badTrain=legacy.Concat(new byte[]{255}).ToArray();var firstExchange=New(J.Kind.Action);var lastTrain=NewTrain(P.Kind.Action);failCount=Count();
                var reverse=streams.NewBuffer(exchange.Concat(badTrain).ToArray());
                using(var parent=streams.Begin(reverse,Parent(C.Direction.Read),P.Wire.DcpbV1))
                {
                    Leaf(parent,firstExchange,reverse,C.Direction.Read);Check(report,!Port.IsUnbound(firstExchange.Value),"later-train-failure-has-earlier-build-binding");
                    var error=Failure(()=>TrainRead(parent,lastTrain,reverse));Measure(report,"later-train-failure-exception",error?.GetType().FullName??"none");
                    Check(report,error?.GetType().FullName==NativeException&&parent.Root.Faulted,"later-train-sibling-native-rejection");
                }
                Check(report,Port.IsUnbound(firstExchange.Value)&&P.Get(lastTrain.Value,lastTrain.Kind) is null,"later-train-failure-rolls-back-earlier-build");Balanced(report,failCount,"later-train-failure",2,2,1);witness.Verify();
                Idle(report,"all-cases");report.Passed=true;
            }
            catch(Exception error){report.Passed=false;report.Failure=error.ToString();}
            finally
            {
                void Cleanup(string label,Action action)
                {try{action();}catch(Exception error){report.Passed=false;report.Failure=(report.Failure??"")+"\n"+label+": "+error;}}
                Cleanup("Build retirement",()=>builds?.Dispose());
                Cleanup("Train retirement",()=>streams?.Dispose());
                Cleanup("Attachment retirement proof",()=>
                {
                    Check(report,ownedBuild.All(x=>Port.IsUnbound(x.Value))&&ownedTrain.All(x=>P.Get(x.Value,x.Kind) is null),"all-private-build-train-bindings-retired");
                    Measure(report,"private-build-records-retired",ownedBuild.Count);Measure(report,"private-train-records-retired",ownedTrain.Count);
                });
                Cleanup("Final authority",()=>Idle(report,"final"));
                Cleanup("Published source witness",()=>{if(witness is not null){witness.Verify();report.Checks.Add("final-full-published-source-witness-unchanged");}});
                var after=Count();Measure(report,"prepared-delta",after.Prepared-beforeAll.Prepared);Measure(report,"finished-delta",after.Finished-beforeAll.Finished);
                Measure(report,"cleaned-delta",after.Cleaned-beforeAll.Cleaned);Measure(report,"rejected-delta",after.Rejected-beforeAll.Rejected);
                Cleanup("Final callback balance",()=>Check(report,after.Prepared-beforeAll.Prepared==after.Cleaned-beforeAll.Cleaned,"every-native-build-train-call-cleaned-once"));
                GC.KeepAlive(rawOwned);GC.KeepAlive(ownedBuild);GC.KeepAlive(ownedTrain);Running=false;
            }
            return report;
        }
    }
}
