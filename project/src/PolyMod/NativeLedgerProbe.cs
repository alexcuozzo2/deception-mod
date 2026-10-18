using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Deception.Core;
using Buffer=Deception.PolyModAdapter.NativeStateProbe.Buffer;
using Report=Deception.PolyModAdapter.NativeStateProbe.Report;

namespace Deception.PolyModAdapter
{
    // Explicit detached diagnostic only. The caller first runs the existing
    // NativeEnvelopeProbe regression on a quiescent disposable state. No hooks,
    // active-state publication, native unit edits, gameplay commands or save IO.
    internal static class NativeLedgerProbe
    {
        const string Reader="Il2CppSystem.IO.BinaryReader";
        const ushort Sentinel=0xC38D;
        static readonly Guid Epoch=new("3fe61c2d-e0ad-4db3-9ba7-01842b7dca38");
        static readonly Guid Operation=new("b911ce38-b386-4342-b7fd-1cc2d75e2fb9");
        const BindingFlags Methods=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.DeclaredOnly;
        [ThreadStatic] static bool Running;

        static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        static void Check(Report report,bool value,string message){Require(value,message);report.Checks.Add(message);}
        static void Measure(Report report,string key,object value)=>report.Measurements[key]=Convert.ToString(value,CultureInfo.InvariantCulture)??"null";
        static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        static object New(string type)=>Native.T(type).GetConstructor(Type.EmptyTypes)?.Invoke(Array.Empty<object>())
            ??throw new MissingMethodException(type,".ctor()");
        static byte[] Serialize(object state,int version)=>NativeStateProbe.Serialize(state,"GameState",version);
        static (long Prepared,long Cleaned) Counts()=>(NativeStateBridge.Prepared,NativeStateBridge.Cleaned);
        static void Balance(Report report,string label,(long Prepared,long Cleaned) before)
        {
            var after=Counts();long prepared=after.Prepared-before.Prepared,cleaned=after.Cleaned-before.Cleaned;
            Measure(report,label+"-prepared",prepared);Measure(report,label+"-cleaned",cleaned);
            Measure(report,label+"-active",NativeGameStateEnvelope.ActiveOperations);
            Check(report,prepared>0&&prepared==cleaned&&NativeGameStateEnvelope.ActiveOperations==0,label+"-bridge-balanced");
        }
        static object Own(object state,object source,List<object> owned)
        {
            Require(Native.T("GameState").IsInstanceOfType(state)&&!Native.Same(state,source),"Private ledger result aliases supplied source or has wrong type.");
            if(!owned.Any(x=>Native.Same(x,state)))owned.Add(state);return state;
        }
        static object Detached(byte[] bytes,int version,object source,List<object> owned)
        {
            object state=Own(New("GameState"),source,owned);
            using var buffer=new Buffer(bytes);
            Native.Invoke(Native.M("GameState","Deserialize",Reader,"System.Int32"),state,buffer.R,version);
            Require(buffer.Position==buffer.Length,"Detached ledger state did not consume its exact section.");return state;
        }
        static byte[] Payload(object state,long revision)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);
            Require(snapshot is not null&&snapshot.Revision==revision,"Missing or wrong private ledger revision.");return snapshot!.Bytes();
        }
        static bool SameAppearance(LedgerAppearance? a,LedgerAppearance b)=>a is not null&&a.Form==b.Form&&
            a.TribeVisual==b.TribeVisual&&a.ClimateVisual==b.ClimateVisual&&a.BirthVisual==b.BirthVisual&&
            a.SourceColor==b.SourceColor&&a.Health==b.Health&&a.Maximum==b.Maximum&&a.Veteran==b.Veteran&&
            a.Concealment==b.Concealment&&a.GrowthTurns==b.GrowthTurns&&a.GrowthDueTurn==b.GrowthDueTurn&&a.Effects.SequenceEqual(b.Effects);
        static void Equivalent(Report report,object state,byte[] native,byte[] payload,long revision,int version,string label)
        {
            Check(report,NativeGameStateEnvelope.NativeBytes(state,version).SequenceEqual(native),label+"-native-health-owner-home-action-bytes-unchanged");
            Check(report,Payload(state,revision).SequenceEqual(payload),label+"-exact-private-snapshot");
            NativeLedgerState.Validate(state,revision,payload);
            var frame=NativeStateFrame.Decode(Serialize(state,version),version);
            Check(report,frame.Revision==revision&&frame.Native.SequenceEqual(native)&&frame.Extension.SequenceEqual(payload),label+"-true-native-framed-writer");
        }
        static uint UnusedNativeId(Dictionary<uint,NativeLedgerState.Body> graph,params uint[] excluded)
        {
            var unavailable=new HashSet<uint>(graph.Keys.Concat(excluded));
            // A bounded census cannot occupy this many descending UInt32 IDs.
            for(uint offset=0;offset<=unavailable.Count;offset++)
            {uint candidate=uint.MaxValue-offset;if(!unavailable.Contains(candidate))return candidate;}
            throw new InvalidDataException("Cannot choose a detached diagnostic absent ID.");
        }
        static (uint Id,LedgerAppearance Appearance) GenuineWarrior(object state,Dictionary<uint,NativeLedgerState.Body> graph)
        {
            foreach(var pair in graph.OrderBy(x=>x.Key))
            {
                if(Native.EnumName(Native.Get(pair.Value.Unit,"type")!)!="warrior")continue;
                try{return(pair.Key,NativeLedgerState.Capture(state,pair.Value,"warrior",false));}
                catch(InvalidDataException){/* A native effect/compound form is outside this first trial. */}
            }
            throw new InvalidDataException("The detached ledger trial needs one supported genuine Warrior.");
        }

        internal static Report Run(object source,int version)
        {
            var report=new Report{Scope="detached-native-private-identity-ledger",Limit=
                "Private ledger transport/semantic diagnostic only. No Regroup/Disguise command, actual death/payment, " +
                "native mutation, client adoption, saved-game migration, replay or multiplayer secrecy is established. " +
                "Receipt and tombstone are synthetic private records; genuine data remains native."};
            if(Running){report.Failure="Native ledger probe reentry rejected.";return report;}
            Running=true;var owned=new List<object>();byte[]? native=null;var before=Counts();
            try
            {
                Require(NativeStateBridge.Installed&&NativeGameStateEnvelope.ActiveOperations==0,"Native ledger probe requires an installed idle bridge.");
                Require(version==123&&Native.T("GameState").IsInstanceOfType(source)&&Native.Int(Native.Get(source,"Version"))==version,
                    "Native ledger trial requires supplied serializer/native version123.");
                Require(NativeGameStateEnvelope.Get(source) is null,"Supplied source already has private state; probe will not alter it.");
                native=NativeGameStateEnvelope.NativeBytes(source,version);
                Check(report,Serialize(source,version).SequenceEqual(native),"source-native-only-format-before-ledger-trial");
                object baseline=Detached(native,version,source,owned);
                var initial=NativeLedgerState.Bootstrap(baseline,Epoch);
                Require(initial.Units.Length>0,"The disposable ledger trial needs an existing registered Mimic.");
                Check(report,initial.Epoch==Epoch&&initial.NextSequence==1&&initial.Receipts.Length==0&&
                    initial.Units.All(x=>x.Kind==LedgerActualKind.Mimic&&x.Alive&&x.Independent&&
                        x.InvestmentKind==PaidInvestmentKind.UnknownLegacy&&x.PaidStars==0),"legacy-Mimic-bootstrap-keeps-payment-unknown");
                var graph=NativeLedgerState.Graph(baseline);var genuine=GenuineWarrior(baseline,graph);
                Require(!initial.Units.Any(x=>x.NativeId==genuine.Id),"Genuine Warrior was incorrectly bootstrapped as a Mimic.");
                byte[] initialPayload=NativeIdentityLedgerCodec.Encode(initial,0);
                NativeGameStateEnvelope.Set(baseline,0,initialPayload);
                Equivalent(report,baseline,native,initialPayload,0,version,"bootstrap");

                ulong genuineLogical=initial.NextLogicalId,tombstoneLogical=checked(genuineLogical+1);
                uint retiredNative=UnusedNativeId(graph),missingNative=UnusedNativeId(graph,retiredNative);
                var rows=initial.Units.Select(x=>x with {Appearance=x.Appearance! with {
                    Health=checked((ushort)Math.Max(1,x.Appearance!.Maximum-10))}}).ToList();
                Require(rows.All(x=>x.Appearance!.Health<x.Appearance.Maximum),"Native apparent maximum is too small for wound trial.");
                rows.Add(new(genuineLogical,genuine.Id,true,LedgerActualKind.Genuine,true,genuine.Appearance,PaidInvestmentKind.UnknownLegacy,0));
                rows.Add(new(tombstoneLogical,retiredNative,false,LedgerActualKind.Mimic,false,null,PaidInvestmentKind.UnknownLegacy,0));
                var receipt=new LedgerReceipt(1,Operation,SHA256.HashData(Encoding.ASCII.GetBytes("detached-ledger-receipt-v1")),0,1,
                    new[]{rows[0].LogicalId,genuineLogical,tombstoneLogical}.OrderBy(x=>x).ToArray());
                var changed=initial with {NextLogicalId=checked(tombstoneLogical+1),NextSequence=2,Units=rows.ToArray(),Receipts=new[]{receipt}};
                byte[] changedPayload=NativeIdentityLedgerCodec.Encode(changed,1);
                object wounded=Detached(native,version,source,owned);NativeGameStateEnvelope.Set(wounded,1,changedPayload);
                Equivalent(report,wounded,native,changedPayload,1,version,"wounded-independent-appearance");
                var recovered=NativeIdentityLedgerCodec.Decode(Payload(wounded,1),1);
                Check(report,recovered.Units.Where(x=>x.Alive&&x.Kind==LedgerActualKind.Mimic).All(x=>x.Appearance!.Health<x.Appearance.Maximum)&&
                    recovered.Units.Single(x=>x.LogicalId==genuineLogical).Independent&&
                    SameAppearance(recovered.Units.Single(x=>x.LogicalId==genuineLogical).Appearance,genuine.Appearance)&&
                    recovered.Units.Single(x=>x.LogicalId==tombstoneLogical).Appearance is null&&
                    recovered.Receipts[0].Affected.Contains(tombstoneLogical),"wounds-equal-type-genuine-overlay-and-synthetic-history-retained");
                object baselineCopy=Detached(Serialize(baseline,version),version,source,owned);
                object changedCopy=Detached(Serialize(wounded,version),version,source,owned);
                Equivalent(report,baselineCopy,native,initialPayload,0,version,"framed-bootstrap-copy");
                Equivalent(report,changedCopy,native,changedPayload,1,version,"framed-wounded-copy");
                Check(report,!Native.Same(baselineCopy,baseline)&&!Native.Same(changedCopy,wounded)&&!Native.Same(baselineCopy,changedCopy),
                    "framed-copies-have-distinct-native-identities");
                // No mutation of a native UnitState: only defensive payload bytes.
                var defensive=Payload(changedCopy,1);defensive[^1]^=1;
                Check(report,Payload(changedCopy,1).SequenceEqual(changedPayload)&&Payload(baselineCopy,0).SequenceEqual(initialPayload),
                    "framed-ledger-snapshots-own-their-bytes");
                Containers(report,"LocalGameData",new[]{baseline,wounded},new[]{initialPayload,changedPayload},new long[]{0,1},native,version,source,owned);
                Containers(report,"HotseatGameData",new[]{baseline,wounded,changedCopy},new[]{initialPayload,changedPayload,changedPayload},new long[]{0,1,1},native,version,source,owned);
                GenericCopy(report,wounded,changedPayload,1,native,version,source,owned);
                Corruptions(report,baseline,changed,genuineLogical,tombstoneLogical,missingNative,native,version,source,owned);
                Measure(report,"bootstrapped-Mimics",initial.Units.Length);Measure(report,"native-bodies",graph.Count);
                Measure(report,"bootstrap-payload-bytes",initialPayload.Length);Measure(report,"wounded-payload-bytes",changedPayload.Length);
                Measure(report,"wounded-payload-sha256",Hash(changedPayload));
                report.Passed=true;
            }
            catch(Exception e){report.Failure=e.GetType().FullName+": "+e.Message;}
            finally
            {
                if(native is not null)
                {
                    try
                    {
                        Check(report,NativeGameStateEnvelope.Get(source) is null&&NativeGameStateEnvelope.NativeBytes(source,version).SequenceEqual(native),
                            "supplied-source-native-bytes-and-unbound-state-unchanged");
                        Measure(report,"source-native-bytes",native.Length);Measure(report,"source-native-sha256",Hash(native));
                    }
                    catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Source verification: "+e.Message;}
                }
                int retired=0;
                foreach(object state in owned)
                {
                    try
                    {
                        NativeGameStateEnvelope.Forget(state);
                        Require(NativeGameStateEnvelope.Get(state) is null,"Detached private binding survived retirement.");retired++;
                    }
                    catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Private cleanup: "+e.Message;}
                }
                Measure(report,"private-states-tracked",owned.Count);Measure(report,"private-states-retired",retired);
                try{Balance(report,"complete-ledger-probe",before);}
                catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Bridge cleanup: "+e.Message;}
                Running=false;
            }
            return report;
        }

        static string[] Fields(string type)=>type=="LocalGameData"?new[]{"initialGameState","currentGameState"}:
            new[]{"initialGameState","currentGameState","lastTurnGameState"};
        static void Containers(Report report,string type,object[] states,byte[][] payloads,long[] revisions,byte[] native,int version,object source,List<object> owned)
        {
            var before=Counts();string[] fields=Fields(type);object container=New(type);
            for(int i=0;i<fields.Length;i++)Native.Set(container,fields[i],states[i]);
            if(type=="LocalGameData")Native.Set(container,"lastSeenCommand",(ushort)37);
            else
            {
                int count=Native.Int(Native.Get(Native.Get(states[0],"PlayerStates")!,"Count"));Require(count is >0 and <=16,"Hotseat roster outside bound.");
                Type array=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(ushort));
                Native.Set(container,"lastSeenCommands",array.GetConstructor(new[]{typeof(ushort[])})!.Invoke(new object[]{Enumerable.Range(0,count).Select(i=>(ushort)(41+i)).ToArray()}));
            }
            byte[] wire=NativeStateProbe.Serialize(container,type,version);
            using var buffer=new Buffer();buffer.Bytes(wire);buffer.Write("System.UInt16",Sentinel);buffer.CloseWriter();buffer.Seek(0);
            object result=New(type);
            try{Native.Invoke(Native.M(type,"Deserialize",Reader,"System.Int32"),result,buffer.R,version);}
            finally{foreach(string field in fields){var child=Native.Get(result,field);if(child is not null)Own(child,source,owned);}}
            object[] children=fields.Select(field=>Own(Native.Get(result,field)??throw new InvalidDataException("Missing decoded ledger sibling."),source,owned)).ToArray();
            Check(report,buffer.Position==wire.Length&&Convert.ToUInt16(buffer.Read("UInt16"))==Sentinel&&buffer.Position==buffer.Length,type+"-ledger-sibling-boundaries-and-sentinel");
            Check(report,children.Select(Native.Pointer).Distinct().Count()==children.Length,type+"-ledger-siblings-not-aliased");
            for(int i=0;i<children.Length;i++)Equivalent(report,children[i],native,payloads[i],revisions[i],version,type+"-"+fields[i]);
            Check(report,NativeStateProbe.Serialize(result,type,version).SequenceEqual(wire),type+"-ledger-container-and-seen-command-roundtrip");
            Balance(report,type,before);
        }
        static MethodInfo Generic(string name)
        {
            Type state=Native.T("GameState"),bytes=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
            Type[] args=name=="ToByteArray"?new[]{state,typeof(int)}:new[]{bytes,state.MakeByRefType(),typeof(int).MakeByRefType()};
            Type result=name=="ToByteArray"?bytes:typeof(bool);
            var candidates=Native.T("SerializationHelpers").GetMethods(Methods).Where(m=>m.Name==name&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==1)
                .Select(m=>m.MakeGenericMethod(state)).Where(m=>m.ReturnType==result&&m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(args)).ToArray();
            Require(candidates.Length==1,"Exact native generic serializer unavailable: "+name);return candidates[0];
        }
        static void GenericCopy(Report report,object state,byte[] payload,long revision,byte[] native,int version,object source,List<object> owned)
        {
            var before=Counts();byte[] wire=NativeStateProbe.ManagedBytes(Native.Invoke(Generic("ToByteArray"),null,state,version)!);
            Check(report,wire.Length>=8&&BinaryPrimitives.ReadInt32LittleEndian(wire)==version&&wire.AsSpan(4).SequenceEqual(Serialize(state,version)),
                "generic-ledger-copy-keeps-outer-version-and-frame");
            object?[] args={NativeStateProbe.NativeBytes(wire),null,0};bool ok;
            try{ok=(bool)Native.Invoke(Generic("FromByteArray"),null,args)!;}
            finally{if(args[1] is not null)Own(args[1]!,source,owned);}
            Require(ok&&args[1] is not null&&Native.Int(args[2])==version,"Native generic ledger copy failed.");
            Equivalent(report,args[1]!,native,payload,revision,version,"generic-ledger-copy");Balance(report,"generic-ledger-copy",before);
        }
        static void Corruptions(Report report,object validationState,NativeIdentityLedger good,ulong genuineLogical,ulong tombstoneLogical,uint missingNative,
            byte[] native,int version,object source,List<object> owned)
        {
            const long revision=1;var first=good.Units.First(x=>x.Alive&&x.Kind==LedgerActualKind.Mimic);
            NativeIdentityLedger Replace(LedgerUnit row)=>good with {Units=good.Units.Select(x=>x.LogicalId==row.LogicalId?row:x).ToArray()};
            var a=first.Appearance!;var genuine=good.Units.Single(x=>x.LogicalId==genuineLogical);var tombstone=good.Units.Single(x=>x.LogicalId==tombstoneLogical);
            var invalid=new Dictionary<string,NativeIdentityLedger>{
                ["missing-native-ID"]=Replace(first with {NativeId=missingNative}),
                ["wrong-actual-kind"]=Replace(first with {Kind=LedgerActualKind.Genuine}),
                ["missing-Mimic-row"]=good with {Units=good.Units.Where(x=>x.LogicalId!=first.LogicalId).ToArray(),NextSequence=1,Receipts=Array.Empty<LedgerReceipt>()},
                ["carrier-appearance-mismatch"]=Replace(first with {Appearance=a with {Form=a.Form=="warrior"?"rider":"warrior"}}),
                ["maximum-health-profile"]=Replace(first with {Appearance=a with {Maximum=checked((ushort)(a.Maximum+1))}}),
                ["unknown-form"]=Replace(first with {Appearance=a with {Form="dcp_probe_missing_form"}}),
                ["unknown-tribe-visual-skin"]=Replace(first with {Appearance=a with {TribeVisual=a.TribeVisual with {Skin="dcp_probe_missing_skin"}}}),
                ["unknown-climate-visual-skin"]=Replace(first with {Appearance=a with {ClimateVisual=a.ClimateVisual with {Skin="dcp_probe_missing_skin"}}}),
                ["unknown-birth-visual-skin"]=Replace(first with {Appearance=a with {BirthVisual=a.BirthVisual with {Skin="dcp_probe_missing_skin"}}}),
                ["tombstone-has-current-native-body"]=Replace(tombstone with {NativeId=genuine.NativeId})
            };
            byte[] validPayload=NativeIdentityLedgerCodec.Encode(good,revision);MethodInfo generic=Generic("FromByteArray");
            foreach(var item in invalid)
            {
                string label="semantic-"+item.Key;byte[] payload=NativeIdentityLedgerCodec.Encode(item.Value,revision);
                // Ensure each case reaches semantic validation: canonical codec,
                // exact native section and a freshly authenticated outer frame.
                Require(NativeIdentityLedgerCodec.Encode(NativeIdentityLedgerCodec.Decode(payload,revision),revision).SequenceEqual(payload),"Semantic case has malformed syntax.");
                bool semanticRejected=false;
                try{NativeLedgerState.Validate(validationState,revision,payload);}catch(InvalidDataException){semanticRejected=true;}
                Check(report,semanticRejected,label+"-is-codec-valid-but-semantically-invalid");
                byte[] frame=NativeStateFrame.Encode(native,payload,revision,version);var decoded=NativeStateFrame.Decode(frame,version);
                Require(decoded.Native.SequenceEqual(native)&&decoded.Extension.SequenceEqual(payload),"Semantic test frame integrity changed.");
                object receiver=Detached(native,version,source,owned);NativeGameStateEnvelope.Set(receiver,revision,validPayload);
                bool failed=false;string error="none";var before=Counts();
                using(var buffer=new Buffer(frame))
                {
                    try
                    {
                        using var permit=NativeStateProbeTrigger.PermitSeededPrivateRead(receiver);
                        Native.Invoke(Native.M("GameState","Deserialize",Reader,"System.Int32"),receiver,buffer.R,version);
                    }
                    catch(Exception e){failed=true;error=e.GetType().FullName+": "+e.Message;}
                    Measure(report,label+"-direct-exception",error);Measure(report,label+"-reader-position",buffer.Position);
                    Check(report,failed&&buffer.Position==frame.Length&&NativeGameStateEnvelope.Get(receiver) is null&&
                        NativeGameStateEnvelope.NativeBytes(receiver,version).SequenceEqual(native),label+"-native-direct-failure-retired-binding-native-bytes-preserved");
                }
                Balance(report,label+"-direct",before);
                byte[] outer=new byte[frame.Length+4];BinaryPrimitives.WriteInt32LittleEndian(outer,version);frame.CopyTo(outer,4);
                object seed=Detached(native,version,source,owned);object?[] args={NativeStateProbe.NativeBytes(outer),seed,0};
                bool? accepted=null;string genericError="none";before=Counts();
                try{accepted=(bool)Native.Invoke(generic,null,args)!;}
                catch(Exception e){genericError=e.GetType().FullName+": "+e.Message;}
                finally{if(args[1] is not null)Own(args[1]!,source,owned);}
                Measure(report,label+"-generic-accepted",accepted?.ToString()??"exception");Measure(report,label+"-generic-exception",genericError);
                Measure(report,label+"-generic-result-nonnull",args[1] is not null);
                Check(report,accepted==false&&args[1] is null,label+"-native-generic-false-and-null");Balance(report,label+"-generic",before);
            }
            Measure(report,"semantic-corruption-cases",invalid.Count);
        }
    }
}
