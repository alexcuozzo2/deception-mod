using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Deception.Core;
using Buffer=Deception.PolyModAdapter.NativeStateProbe.Buffer;
using Report=Deception.PolyModAdapter.NativeStateProbe.Report;

namespace Deception.PolyModAdapter
{
    // Opt-in transport diagnostic. Every binding belongs to a fresh detached
    // state; the supplied disposable source is read only. No gameplay schema,
    // client adoption, disk access, action simulation, or global cache mutation.
    internal static class NativeEnvelopeProbe
    {
        const string Reader="Il2CppSystem.IO.BinaryReader",Writer="Il2CppSystem.IO.BinaryWriter";
        const uint PayloadMagic=0x45425044; // DPBE, followed by one diagnostic token.
        const ushort Sentinel=0xB75D;
        const BindingFlags Methods=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.DeclaredOnly;
        [ThreadStatic] static bool Running;

        internal static bool ValidatePayload(byte[] bytes)=>bytes is not null&&bytes.Length==8&&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes)==PayloadMagic&&
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) is >=1 and <=3;
        static byte[] Payload(uint token)
        {
            var bytes=new byte[8];BinaryPrimitives.WriteUInt32LittleEndian(bytes,PayloadMagic);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4),token);return bytes;
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
        static void Check(Report report,bool condition,string name){Require(condition,name);report.Checks.Add(name);}
        static void Measure(Report report,string name,object value)=>report.Measurements[name]=Convert.ToString(value,CultureInfo.InvariantCulture)??"null";
        static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        static (long Prepared,long Finished,long Cleaned,long Rejected) BridgeCounts()=>
            (NativeStateBridge.Prepared,NativeStateBridge.Finished,NativeStateBridge.Cleaned,NativeStateBridge.Rejected);
        static void CheckBridgeBalance(Report report,string label,(long Prepared,long Finished,long Cleaned,long Rejected) before)
        {
            var after=BridgeCounts();
            Measure(report,label+"-bridge-prepared",after.Prepared-before.Prepared);
            Measure(report,label+"-bridge-finished",after.Finished-before.Finished);
            Measure(report,label+"-bridge-cleaned",after.Cleaned-before.Cleaned);
            Measure(report,label+"-bridge-rejected",after.Rejected-before.Rejected);
            Measure(report,label+"-active-operations",NativeGameStateEnvelope.ActiveOperations);
            Check(report,after.Prepared-before.Prepared==after.Cleaned-before.Cleaned&&NativeGameStateEnvelope.ActiveOperations==0,
                label+"-every-prepared-operation-cleaned");
        }
        static object New(string name)=>Native.T(name).GetConstructor(Type.EmptyTypes)?.Invoke(Array.Empty<object>())
            ??throw new MissingMethodException(name,".ctor()");
        static byte[] Serialize(object state,int version)=>NativeStateProbe.Serialize(state,"GameState",version);

        // Resolve only the two audited generic wrappers, then verify their
        // closed signatures. Native.M deliberately excludes generic definitions.
        static MethodInfo Generic(string name)
        {
            Type state=Native.T("GameState");
            Type bytes=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
            Type[] parameters=name=="ToByteArray"?new[]{state,typeof(int)}:
                new[]{bytes,state.MakeByRefType(),typeof(int).MakeByRefType()};
            Type result=name=="ToByteArray"?bytes:typeof(bool);
            var candidates=Native.T("SerializationHelpers").GetMethods(Methods)
                .Where(m=>m.Name==name&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==1)
                .Select(m=>m.MakeGenericMethod(state))
                .Where(m=>m.ReturnType==result&&m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(parameters)).ToArray();
            Require(candidates.Length==1,"Exact native generic serializer signature unavailable: "+name);
            return candidates[0];
        }

        internal static Report Run(object source,int version)
        {
            var report=new Report{Scope="detached-native-envelope-detours",Limit=
                "Detached diagnostic payloads only. No live saves, client adoption, gameplay persistence, seek/replay, or multiplayer result is established. " +
                "KeyFrameCache is static/global and has no independent native constructor; its live storage was not touched."};
            if(Running){report.Failure="Envelope probe reentry rejected.";return report;}
            Running=true;
            var owned=new List<object>();byte[]? sourceBytes=null;
            int writes=NativeGameStateEnvelope.FramedWrites,reads=NativeGameStateEnvelope.FramedReads,legacy=NativeGameStateEnvelope.LegacyReads;
            var bridgeBefore=BridgeCounts();
            try
            {
                Require(NativeStateBridge.Installed&&NativeGameStateEnvelope.ActiveOperations==0,"Detached envelope probe requires an installed idle native bridge.");
                Require(version==NativeStateFrame.NativeVersion,"Envelope probe requires audited serializer version 123.");
                Require(Native.T("GameState").IsInstanceOfType(source),"Expected supplied disposable native GameState.");
                Require(NativeGameStateEnvelope.Get(source) is null,"Source already has an extension; diagnostic will not alter it.");
                // The original detached preflight rejects Smuggle command/action
                // history and proves the bounded native graph before any binding.
                var preflight=NativeStateProbe.RunDetachedStateProbe(source,version);
                Require(preflight.Passed,"Detached native preflight failed: "+preflight.Failure);
                report.Checks.Add("original-detached-preflight-passed");
                foreach(var item in preflight.Measurements)report.Measurements["preflight-"+item.Key]=item.Value;
                sourceBytes=NativeGameStateEnvelope.NativeBytes(source,version);
                Check(report,Serialize(source,version).SequenceEqual(sourceBytes),"unbound-source-remains-exact-native-format");
                var states=Enumerable.Range(0,3).Select(_=>Detached(sourceBytes,version,source,owned)).ToArray();
                long[] revisions={11,22,33};
                for(int i=0;i<states.Length;i++)
                {
                    byte[] payload=Payload((uint)i+1);NativeGameStateEnvelope.Set(states[i],revisions[i],payload);
                    payload[0]^=0xff;
                    CheckSnapshot(report,states[i],revisions[i],(uint)i+1,"state-"+(i+1));
                    Check(report,NativeGameStateEnvelope.NativeBytes(states[i],version).SequenceEqual(sourceBytes),"state-"+(i+1)+"-identical-native-section");
                    byte[] framed=Serialize(states[i],version);var decoded=NativeStateFrame.Decode(framed,version);
                    Check(report,decoded.Native.SequenceEqual(sourceBytes)&&decoded.Revision==revisions[i]&&decoded.Extension.SequenceEqual(Payload((uint)i+1)),
                        "state-"+(i+1)+"-native-detour-emits-exact-frame");
                    object restored=Detached(framed,version,source,owned);
                    CheckSnapshot(report,restored,revisions[i],(uint)i+1,"restored-"+(i+1));
                    Check(report,!Native.Same(restored,states[i])&&NativeGameStateEnvelope.NativeBytes(restored,version).SequenceEqual(sourceBytes)&&
                        Serialize(restored,version).SequenceEqual(framed),"state-"+(i+1)+"-native-detour-roundtrip-independent");
                }
                Check(report,states.Select(Native.Pointer).Distinct().Count()==3,"three-native-identities-with-distinct-revisions");
                NativeGameStateEnvelope.Set(states[0],44,Payload(3));
                CheckSnapshot(report,states[1],22,2,"sibling-after-other-state-update");
                NativeGameStateEnvelope.Set(states[0],11,Payload(1));
                object plain=Detached(sourceBytes,version,source,owned);
                Containers(report,"LocalGameData",new[]{states[0],plain},source,sourceBytes,version,owned,"framed-first");
                Containers(report,"LocalGameData",new[]{plain,states[1]},source,sourceBytes,version,owned,"legacy-first");
                Containers(report,"HotseatGameData",new[]{states[0],plain,states[2]},source,sourceBytes,version,owned,"mixed-three");
                GenericCopies(report,states,plain,source,sourceBytes,version,owned);
                LegacyReplacement(report,source,sourceBytes,version,owned);
                Check(report,NativeGameStateEnvelope.FramedWrites>writes&&NativeGameStateEnvelope.FramedReads>reads&&
                    NativeGameStateEnvelope.LegacyReads>legacy,"actual-native-detours-observed-in-both-directions");
                // Exercise native callers, including container unwinding. A
                // managed-only callback test cannot establish this boundary.
                bool rejected=Malformed(report,source,sourceBytes,version,owned);
                rejected&=MalformedContainers(report,states,source,sourceBytes,version,owned);
                rejected&=WrongWriterVersion(report,states[0],version);
                Require(rejected,"UNSAFE DETOUR FAILURE: a malformed reader or unsupported writer was not rejected at its native caller boundary; see rejection measurements. " +
                    "A retired extension alone does not prevent publication of a partial native GameState.");
                report.Passed=true;
            }
            catch(Exception e){report.Failure=e.GetType().FullName+": "+e.Message;}
            finally
            {
                if(sourceBytes is not null)
                {
                    try
                    {
                        Check(report,NativeGameStateEnvelope.Get(source) is null&&NativeGameStateEnvelope.NativeBytes(source,version).SequenceEqual(sourceBytes),
                            "supplied-source-native-bytes-and-unbound-state-unchanged");
                        Measure(report,"source-native-bytes",sourceBytes.Length);Measure(report,"source-native-sha256",Hash(sourceBytes));
                    }
                    catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Source verification: "+e.Message;}
                }
                foreach(object state in owned)
                {
                    try{NativeGameStateEnvelope.Forget(state);}
                    catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Private binding cleanup: "+e.Message;}
                }
                Measure(report,"framed-writes",NativeGameStateEnvelope.FramedWrites-writes);
                Measure(report,"framed-reads",NativeGameStateEnvelope.FramedReads-reads);
                Measure(report,"legacy-reads",NativeGameStateEnvelope.LegacyReads-legacy);
                Measure(report,"private-states-retired",owned.Count);
                try{CheckBridgeBalance(report,"complete-probe",bridgeBefore);}
                catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Bridge cleanup: "+e.Message;}
                Running=false;
            }
            return report;
        }
        static object Own(object state,object source,List<object> owned)
        {
            Require(Native.T("GameState").IsInstanceOfType(state)&&!Native.Same(state,source),"Detached result aliases the supplied state or has the wrong type.");
            if(!owned.Any(x=>Native.Same(x,state)))owned.Add(state);return state;
        }
        static object Detached(byte[] bytes,int version,object source,List<object> owned)
        {
            // Track before calling native code: an exceptional read may already
            // have published a binding on this private receiver.
            object state=Own(New("GameState"),source,owned);
            using var buffer=new Buffer(bytes);
            Native.Invoke(Native.M("GameState","Deserialize",Reader,"System.Int32"),state,buffer.R,version);
            Require(buffer.Position==buffer.Length,"Native detached read did not consume its exact record.");return state;
        }
        static void CheckSnapshot(Report report,object state,long revision,uint token,string label)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);
            Check(report,snapshot is not null&&snapshot.Revision==revision&&snapshot.Bytes().SequenceEqual(Payload(token)),label+"-independent-payload-and-revision");
            byte[] copy=snapshot!.Bytes();copy[0]^=0xff;
            Check(report,NativeGameStateEnvelope.Get(state)!.Bytes().SequenceEqual(Payload(token)),label+"-snapshot-bytes-defensive-copy");
        }
        static void Equivalent(Report report,object expected,object actual,byte[] native,int version,string label)
        {
            var left=NativeGameStateEnvelope.Get(expected);var right=NativeGameStateEnvelope.Get(actual);
            Check(report,!Native.Same(expected,actual)&&NativeGameStateEnvelope.NativeBytes(actual,version).SequenceEqual(native)&&
                (left is null?right is null:right is not null&&left.Revision==right.Revision&&left.Bytes().SequenceEqual(right.Bytes())),label);
        }
        static string[] ContainerFields(string type)=>type=="HotseatGameData"?
            new[]{"initialGameState","currentGameState","lastTurnGameState"}:new[]{"initialGameState","currentGameState"};
        static object Container(string type,object[] states)
        {
            string[] fields=ContainerFields(type);
            object container=New(type);
            for(int i=0;i<fields.Length;i++)Native.Set(container,fields[i],states[i]);
            if(type=="HotseatGameData")
            {
                int players=Native.Int(Native.Get(Native.Get(states[0],"PlayerStates")!,"Count"));
                Require(players>0&&players<=16,"Detached Hotseat roster outside bound.");
                ushort[] seen=Enumerable.Range(0,players).Select(i=>(ushort)(41+i)).ToArray();
                Type array=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(ushort));
                Native.Set(container,"lastSeenCommands",array.GetConstructor(new[]{typeof(ushort[])})!.Invoke(new object[]{seen}));
            }
            else Native.Set(container,"lastSeenCommand",(ushort)37);
            return container;
        }
        static void Containers(Report report,string type,object[] states,object source,byte[] native,int version,List<object> owned,string pattern)
        {
            string label=type+"-"+pattern;string[] fields=ContainerFields(type);
            object container=Container(type,states);
            using var buffer=new Buffer();
            Native.Invoke(Native.M(type,"Serialize",Writer,"System.Int32"),container,buffer.W,version);
            long end=buffer.Position;buffer.Write("System.UInt16",Sentinel);byte[] wire=buffer.Snapshot();buffer.CloseWriter();buffer.Seek(0);
            object restored=New(type);
            try{Native.Invoke(Native.M(type,"Deserialize",Reader,"System.Int32"),restored,buffer.R,version);}
            finally
            {
                // A later sibling can fail after earlier siblings acquired
                // bindings. Retire all fields that the native parser exposed.
                foreach(string field in fields)
                {var child=Native.Get(restored,field);if(child is not null)Own(child,source,owned);}
            }
            // Retain all successfully exposed detached children for cleanup even
            // if a subsequent boundary/equivalence assertion fails.
            object[] children=fields.Select(p=>Own(Native.Get(restored,p)??throw new InvalidDataException("Null container state: "+p),source,owned)).ToArray();
            Check(report,buffer.Position==end&&Convert.ToUInt16(buffer.Read("UInt16"))==Sentinel&&buffer.Position==wire.Length,label+"-exact-native-sibling-and-sentinel-boundary");
            for(int i=0;i<fields.Length;i++)Equivalent(report,states[i],children[i],native,version,label+"-"+fields[i]);
            Check(report,children.Select(Native.Pointer).Distinct().Count()==children.Length,label+"-siblings-not-aliased");
            Check(report,NativeStateProbe.Serialize(container,type,version).SequenceEqual(NativeStateProbe.Serialize(restored,type,version)),label+"-full-roundtrip-preserves-seen-command-suffix");
        }
        static void GenericCopies(Report report,object[] states,object plain,object source,byte[] native,int version,List<object> owned)
        {
            MethodInfo write=Generic("ToByteArray"),read=Generic("FromByteArray");
            foreach(object state in states.Concat(new[]{plain}))
            {
                byte[] bytes=NativeStateProbe.ManagedBytes(Native.Invoke(write,null,state,version)!);
                Require(bytes.Length>=8&&BinaryPrimitives.ReadInt32LittleEndian(bytes)==version,"Native generic helper omitted expected outer serializer version.");
                Check(report,bytes.AsSpan(4).SequenceEqual(Serialize(state,version)),"generic-copy-exact-outer-version-and-state-frame");
                object?[] args={NativeStateProbe.NativeBytes(bytes),null,0};
                bool ok;
                try{ok=(bool)Native.Invoke(read,null,args)!;}
                finally{if(args[1] is not null)Own(args[1]!,source,owned);}
                Require(ok&&args[1] is not null&&Native.Int(args[2])==version,"Native generic helper failed a valid detached copy.");
                object copy=Own(args[1]!,source,owned);
                Equivalent(report,state,copy,native,version,"generic-native-helper-preserves-independent-revision");
            }
        }
        static void LegacyReplacement(Report report,object source,byte[] native,int version,List<object> owned)
        {
            object receiver=Detached(native,version,source,owned);
            NativeGameStateEnvelope.Set(receiver,77,Payload(2));
            using var buffer=new Buffer(native);
            using(NativeStateProbeTrigger.PermitSeededPrivateRead(receiver))
                Native.Invoke(Native.M("GameState","Deserialize",Reader,"System.Int32"),receiver,buffer.R,version);
            Check(report,buffer.Position==buffer.Length&&NativeGameStateEnvelope.Get(receiver) is null&&Serialize(receiver,version).SequenceEqual(native),
                "native-legacy-read-retires-previous-private-binding");
        }
        static bool Malformed(Report report,object source,byte[] native,int version,List<object> owned)
        {
            byte[] good=NativeStateFrame.Encode(native,Payload(1),91,version);
            byte[] checksum=(byte[])good.Clone();checksum[^1]^=0x40;
            byte[] revision=(byte[])good.Clone();revision[12]^=1;
            byte[] schema=(byte[])good.Clone();schema[4]=0xff;
            byte[] badPayload=Payload(1);badPayload[4]=0;
            var cases=new Dictionary<string,byte[]>{["checksum"]=checksum,["revision-checksum"]=revision,
                ["unknown-schema"]=schema,["truncated-header"]=good.Take(8).ToArray(),["truncated-tail"]=good.Take(good.Length-1).ToArray(),
                ["invalid-diagnostic-payload"]=NativeStateFrame.Encode(native,badPayload,91,version)};
            foreach(var item in NativeReaderFailures(native,version))cases.Add(item.Key,item.Value);
            bool safe=true;MethodInfo read=Generic("FromByteArray");
            foreach(var item in cases)
            {
                string label="corrupt-"+item.Key;
                object receiver=Detached(native,version,source,owned);
                NativeGameStateEnvelope.Set(receiver,88,Payload(3));
                bool callerFailed=false;string exception="none";
                using(var buffer=new Buffer(item.Value))
                {
                    var before=BridgeCounts();
                    try
                    {
                        using var permit=NativeStateProbeTrigger.PermitSeededPrivateRead(receiver);
                        Native.Invoke(Native.M("GameState","Deserialize",Reader,"System.Int32"),receiver,buffer.R,version);
                    }
                    catch(Exception e){callerFailed=true;exception=e.GetType().FullName+": "+e.Message;}
                    CheckBridgeBalance(report,label+"-direct",before);
                    bool retired=NativeGameStateEnvelope.Get(receiver) is null;
                    // An original native EOF may leave scalar fields changed.
                    // Do not serialize that partial receiver again or assert an
                    // atomic native decode; it remains owned and unadopted.
                    bool mayChange=item.Key=="native-eof";
                    bool unchanged=mayChange||NativeGameStateEnvelope.NativeBytes(receiver,version).SequenceEqual(native);
                    Measure(report,label+"-direct-caller-failed",callerFailed);Measure(report,label+"-direct-exception",exception);
                    Measure(report,label+"-prior-binding-retired",retired);
                    Measure(report,label+"-receiver-native-unchanged",mayChange?"not-compared-partial-private-receiver":unchanged);
                    Measure(report,label+"-reader-position",buffer.Position);
                    safe&=callerFailed&&retired&&unchanged;
                }
                // The native Boolean helper has its own exception handler and
                // publication boundary. Exercise it separately; a logged prefix
                // failure may otherwise return true and publish a partial state.
                byte[] outer=new byte[item.Value.Length+4];BinaryPrimitives.WriteInt32LittleEndian(outer,version);item.Value.CopyTo(outer,4);
                object seed=Detached(native,version,source,owned);
                object?[] args={NativeStateProbe.NativeBytes(outer),seed,0};
                bool? accepted=null;string outerException="none";
                var genericBefore=BridgeCounts();
                try{accepted=(bool)Native.Invoke(read,null,args)!;}
                catch(Exception e){outerException=e.GetType().FullName+": "+e.Message;}
                bool published=args[1] is not null;
                if(published)Own(args[1]!,source,owned);
                CheckBridgeBalance(report,label+"-generic",genericBefore);
                Measure(report,label+"-generic-accepted",accepted?.ToString()??"exception");
                Measure(report,label+"-generic-result-nonnull",published);Measure(report,label+"-generic-exception",outerException);
                // Its audited contract returns false with a null output. A
                // managed wrapper exception with a stale output is not proof.
                safe&=accepted==false&&!published;
            }
            if(safe)report.Checks.Add("all-malformed-frames-fail-at-native-caller-and-generic-publication-boundaries");
            else report.Limit+=" MALFORMED INPUT SAFETY FAILED: native caller rejection or partial-state publication protection failed; do not enable this transport for gameplay or saves.";
            return safe;
        }
        static Dictionary<string,byte[]> NativeReaderFailures(byte[] native,int version)
        {
            // Both frames pass framing, checksum and diagnostic-payload checks.
            // The first reaches a genuine native reader EOF; the second reaches
            // Finish after native decoding leaves one byte in its private section.
            var onlyVersion=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(onlyVersion,version);
            var tail=new byte[native.Length+1];native.CopyTo(tail,0);tail[^1]=0xA9;
            return new Dictionary<string,byte[]>{
                ["native-eof"]=NativeStateFrame.Encode(onlyVersion,Payload(1),92,version),
                ["native-section-tail"]=NativeStateFrame.Encode(tail,Payload(1),93,version)};
        }
        static int UniqueOffset(byte[] container,byte[] section)
        {
            int found=-1;
            for(int i=0;i<=container.Length-section.Length;i++)
            {
                if(!container.AsSpan(i,section.Length).SequenceEqual(section))continue;
                Require(found<0,"Native container section appeared more than once.");found=i;
            }
            Require(found>=0,"Native container did not contain its exact serialized child frame.");return found;
        }
        static bool MalformedContainers(Report report,object[] states,object source,byte[] native,int version,List<object> owned)
        {
            bool safe=true;
            foreach(string type in new[]{"LocalGameData","HotseatGameData"})
            {
                string[] fields=ContainerFields(type);
                object container=Container(type,states);
                byte[] valid=NativeStateProbe.Serialize(container,type,version);
                var originals=fields.Select((_,i)=>Serialize(states[i],version)).ToArray();
                int[] offsets=originals.Select(bytes=>UniqueOffset(valid,bytes)).ToArray();
                Require(offsets.SequenceEqual(offsets.OrderBy(x=>x)),"Native container sibling order differs from the audited contract.");
                for(int slot=0;slot<fields.Length;slot++)
                {
                    byte[] checksum=(byte[])originals[slot].Clone();checksum[^1]^=0x40;
                    var failures=NativeReaderFailures(native,version);failures.Add("checksum",checksum);
                    foreach(var failure in failures)
                    {
                        string label="corrupt-"+type+"-"+fields[slot]+"-"+failure.Key;
                        int start=offsets[slot],oldEnd=start+originals[slot].Length,failedEnd=start+failure.Value.Length;
                        byte[] wire=valid.Take(start).Concat(failure.Value).Concat(valid.Skip(oldEnd)).ToArray();
                        using var buffer=new Buffer();buffer.Bytes(wire);buffer.Write("System.UInt16",Sentinel);
                        byte[] originalBuffer=buffer.Snapshot();buffer.CloseWriter();buffer.Seek(0);
                        object receiver=New(type);bool failed=false;string exception="none";
                        var before=BridgeCounts();var children=new List<object>();
                        try{Native.Invoke(Native.M(type,"Deserialize",Reader,"System.Int32"),receiver,buffer.R,version);}
                        catch(Exception e){failed=true;exception=e.GetType().FullName+": "+e.Message;}
                        finally
                        {
                            // Native containers may expose earlier siblings or
                            // the failed private receiver before throwing. This
                            // is cleanup, not a claim of atomic container decode.
                            foreach(string field in fields)
                            {
                                var child=Native.Get(receiver,field);
                                if(child is not null)children.Add(Own(child,source,owned));
                            }
                        }
                        CheckBridgeBalance(report,label,before);
                        bool stopped=buffer.Position>=start&&buffer.Position<=failedEnd;
                        bool inputUnchanged=buffer.Snapshot().SequenceEqual(originalBuffer);
                        object? failedChild=Native.Get(receiver,fields[slot]);
                        bool failedBindingAbsent=failedChild is null||NativeGameStateEnvelope.Get(failedChild) is null;
                        int bound=children.Count(x=>NativeGameStateEnvelope.Get(x) is not null);
                        foreach(object child in children)NativeGameStateEnvelope.Forget(child);
                        bool retired=children.All(x=>NativeGameStateEnvelope.Get(x) is null);
                        Measure(report,label+"-caller-failed",failed);Measure(report,label+"-exception",exception);
                        Measure(report,label+"-reader-position",buffer.Position);Measure(report,label+"-failed-section-end",failedEnd);
                        Measure(report,label+"-stopped-within-failed-section",stopped);Measure(report,label+"-input-and-sentinel-unchanged",inputUnchanged);
                        Measure(report,label+"-exposed-private-children",children.Count);Measure(report,label+"-earlier-private-bindings",bound);
                        Measure(report,label+"-failed-child-has-no-binding",failedBindingAbsent);Measure(report,label+"-all-exposed-bindings-retired",retired);
                        bool passed=failed&&stopped&&inputUnchanged&&failedBindingAbsent&&retired;
                        if(passed)report.Checks.Add(label+"-native-caller-failure-and-private-cleanup");safe&=passed;
                    }
                }
            }
            if(!safe)report.Limit+=" A detached native container rejection failed; no container/client publication safety is established.";
            return safe;
        }
        static bool WrongWriterVersion(Report report,object state,int version)
        {
            int wrong=checked(version+1);Require(wrong==124,"Unexpected wrong-version probe value.");
            var snapshot=NativeGameStateEnvelope.Get(state);Require(snapshot is not null,"Writer rejection requires a bound detached state.");
            bool failed=false;string exception="none";
            using var buffer=new Buffer();buffer.Bytes(new byte[]{0xD1,0x02,0x80});buffer.Write("System.UInt16",Sentinel);
            byte[] previous=buffer.Snapshot();long position=buffer.Position;var before=BridgeCounts();
            try{Native.Invoke(Native.M("GameState","Serialize",Writer,"System.Int32"),state,buffer.W,wrong);}
            catch(Exception e){failed=true;exception=e.GetType().FullName+": "+e.Message;}
            CheckBridgeBalance(report,"writer-version124-direct",before);
            bool unchanged=buffer.Position==position&&buffer.Snapshot().SequenceEqual(previous);
            Measure(report,"writer-version124-direct-caller-failed",failed);Measure(report,"writer-version124-direct-exception",exception);
            Measure(report,"writer-version124-existing-prefix-and-sentinel-unchanged",unchanged);
            object? result=null;bool genericFailed=false;string genericException="none";var genericBefore=BridgeCounts();
            try{result=Native.Invoke(Generic("ToByteArray"),null,state,wrong);}
            catch(Exception e){genericFailed=true;genericException=e.GetType().FullName+": "+e.Message;}
            CheckBridgeBalance(report,"writer-version124-generic",genericBefore);
            Measure(report,"writer-version124-generic-caller-failed",genericFailed);
            Measure(report,"writer-version124-generic-exception",genericException);
            Measure(report,"writer-version124-generic-returned-byte-array",result is not null);
            var after=NativeGameStateEnvelope.Get(state);
            bool bindingUnchanged=after is not null&&after.Revision==snapshot!.Revision&&after.Bytes().SequenceEqual(snapshot.Bytes());
            Measure(report,"writer-version124-payload-unchanged",bindingUnchanged);
            bool safe=failed&&unchanged&&result is null&&bindingUnchanged;
            if(safe)report.Checks.Add("bound-writer124-rejected-before-caller-stream-write-or-generic-byte-result");
            else report.Limit+=" A bound version124 writer rejection failed; no framed writer safety is established.";
            return safe;
        }
    }
}
