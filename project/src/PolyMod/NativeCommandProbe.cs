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
    // Opt-in transport evidence only. All edited carriers, histories, action
    // stacks and receivers are new private objects. Never execute a command,
    // publish a state, allocate gameplay identities, or change the source.
    internal static class NativeCommandProbe
    {
        const string Reader="Il2CppSystem.IO.BinaryReader",Writer="Il2CppSystem.IO.BinaryWriter";
        const string Improvement="Polytopia.Data.ImprovementData+Type";
        const ushort Sentinel=0xC75A;
        [ThreadStatic] static bool Running;
        sealed record Case(string Name,uint Site,object Value,byte[] Base,byte[] Payload,NativeExchangeIntent? Intent);
        sealed class Trial
        {
            internal object Source=null!;
            internal byte[] SourceBytes=null!;
            internal int Version;
            internal readonly List<object> States=new();
            internal readonly Dictionary<long,object> Values=new();
            internal readonly HashSet<long> SourceValues=new();
            internal object Own(object value)
            {
                long pointer=Native.Pointer(value);Require(!SourceValues.Contains(pointer),"Private carrier aliases source history.");
                Values.TryAdd(pointer,value);return value;
            }
            internal void Collect(object state)
            {
                foreach(string name in new[]{"CommandStack","ActionStack"})
                {
                    var list=Native.Get(state,name);if(list is null)continue;
                    Require(Native.Int(Native.Get(list,"Count"))<=16384,"Private native history exceeds probe bound.");
                    foreach(var value in Native.Items(list))Own(value);
                }
            }
            internal object State(byte[] bytes)
            {
                object value=New("GameState");Require(!Native.Same(value,Source),"Private state aliases source.");States.Add(value);
                using var buffer=new Buffer(bytes);
                try
                {
                    Native.Invoke(Native.M("GameState","Deserialize",Reader,"System.Int32"),value,buffer.R,Version);
                    Require(buffer.Position==buffer.Length,"Private state did not consume its exact record.");return value;
                }
                finally{Collect(value);}
            }
        }
        static Type BytesType=>Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
        static object New(string type)=>Native.T(type).GetConstructor(Type.EmptyTypes)?.Invoke(Array.Empty<object>())??throw new MissingMethodException(type,".ctor()");
        static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
        static void Check(Report report,bool condition,string name){Require(condition,name);report.Checks.Add(name);}
        static void Measure(Report report,string name,object? value)=>report.Measurements[name]=Convert.ToString(value,CultureInfo.InvariantCulture)??"null";
        static byte[] Serialized(object value,uint site,int version)=>NativeStateProbe.Serialize(value,NativeCommandEnvelope.Owner(site),version);
        static object Concrete(object value,uint site)=>Native.As(value,NativeCommandEnvelope.Owner(site));
        static byte[] Outer(byte[] record,int version,ushort? commandType=null)
        {
            byte[] bytes=new byte[record.Length+(commandType.HasValue?6:4)];BinaryPrimitives.WriteInt32LittleEndian(bytes,version);
            if(commandType.HasValue)BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4),commandType.Value);
            record.CopyTo(bytes,commandType.HasValue?6:4);return bytes;
        }
        static MethodInfo GenericFor(string name,string owner)
        {
            Require(owner=="GameState","Concrete Build generics are not a supported native transport path.");
            Type type=Native.T(owner);
            Type[] args=name=="ToByteArray"?new[]{type,typeof(int)}:new[]{BytesType,type.MakeByRefType(),typeof(int).MakeByRefType()};
            Type result=name=="ToByteArray"?BytesType:typeof(bool);
            var found=Native.T("SerializationHelpers").GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.DeclaredOnly)
                .Where(m=>m.Name==name&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==1)
                .Select(m=>m.MakeGenericMethod(type)).Where(m=>m.ReturnType==result&&m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(args)).ToArray();
            Require(found.Length==1,"Missing exact native GameState generic helper: "+name);return found[0];
        }
        static MethodInfo CommandRead()=>Native.M("CommandBase","FromByteArray",BytesType.FullName!,"CommandBase&","System.Int32&");
        static ushort CommandType(object value)=>Convert.ToUInt16(Native.Invoke(Native.M("BuildCommand","GetCommandType"),Concrete(value,1)));
        static ushort ActionType(object value)=>Convert.ToUInt16(Native.Invoke(Native.M("BuildAction","GetActionType"),Concrete(value,2)));
        static byte[] ActionWire(byte[] record,object value)
        {
            byte[] bytes=new byte[record.Length+2];BinaryPrimitives.WriteUInt16LittleEndian(bytes,ActionType(value));record.CopyTo(bytes,2);return bytes;
        }
        static void SerializeAction(object value,Buffer buffer,int version)=>Native.Invoke(
            Native.M("GameState","SerializeAction","ActionBase",Writer,"System.Int32"),null,value,buffer.W,version);
        static object? DeserializeAction(Buffer buffer,int version)=>Native.Invoke(
            Native.M("GameState","DeserializeAction",Reader,"System.Int32"),null,buffer.R,version);
        static object Carrier(Trial trial,uint site,string id,byte actor,int x,int y)
        {
            object at=Native.T("WorldCoordinates").GetConstructor(new[]{typeof(int),typeof(int)})!.Invoke(new object[]{x,y});
            var types=new List<Type>{typeof(byte),Native.T(Improvement),Native.T("WorldCoordinates")};
            var values=new List<object>{actor,Native.EnumValue(Improvement,id),at};
            if(site==2){types.Add(typeof(bool));values.Add(true);}
            object value=Native.T(NativeCommandEnvelope.Owner(site)).GetConstructor(types.ToArray())!.Invoke(values.ToArray());
            return trial.Own(value);
        }
        static byte[] SmugglePayload(uint id)
        {
            byte[] bytes=new byte[9];BinaryPrimitives.WriteUInt32LittleEndian(bytes,MimicSmuggleActions.PayloadMagic);
            bytes[4]=MimicSmuggleActions.PayloadVersion;BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(5),id);return bytes;
        }
        static void BindSmuggle(object value,byte[] payload,int version)
        {using var buffer=new Buffer(payload);MimicSmuggleActions.ReadPayload(value,buffer.R,version);Require(buffer.Position==buffer.Length,"Smuggle payload binding did not consume nine bytes.");}
        static byte[] Payload(object value,int version)
        {
            if(NativeExchangeCommands.IsExchange(value))return NativeExchangeCommands.Bytes(value);
            using var buffer=new Buffer();MimicSmuggleActions.WritePayload(value,buffer.W,version);return buffer.Snapshot();
        }
        static bool Unbound(object value,int version)
        {
            if(NativeExchangeCommands.Get(value) is not null)return false;
            if(!MimicSmuggleActions.IsSmuggle(value))return true;
            try{_=Payload(value,version);return false;}catch(InvalidDataException){return true;}
        }
        static Case[] Cases(Trial trial,uint site,byte actor,uint turn)
        {
            var result=new List<Case>();
            void Add(string name,object value,byte[] payload,NativeExchangeIntent? intent=null)=>result.Add(new(name,site,value,NativeCommandEnvelope.NativeBytes(value,site,trial.Version),payload,intent));
            Add("ordinary",Carrier(trial,site,"hunting",actor,1,2),Array.Empty<byte>());
            object smuggle=Carrier(trial,site,SmuggleCatalog.Harvests.First().Id,actor,2,3);
            byte[] smugglePayload=SmugglePayload(1001);BindSmuggle(smuggle,smugglePayload,trial.Version);Add("smuggle",smuggle,smugglePayload);
            foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})foreach(bool swap in new[]{false,true})
            {
                bool allocate=kind==NativeExchangeKind.Disguise;
                var intent=new NativeExchangeIntent(new Guid("d04e6612-ef38-483e-935f-e5b27a776e1f"),swap?22UL:21UL,7,turn,actor,kind,swap,
                    allocate?NativePartnerReference.Allocate:NativePartnerReference.Existing,allocate?2UL:3UL,
                    new(1,1001,3,4),new(2,1002,4,4),Enumerable.Range(0,32).Select(i=>(byte)i).ToArray());
                object value=Carrier(trial,site,NativeExchangeCatalog.Id(kind),actor,3,4);NativeExchangeCommands.Set(value,intent);
                Add(kind.ToString().ToLowerInvariant()+"-"+(swap?"swap":"keep"),value,NativeExchangeIntentCodec.Encode(intent),intent);
            }
            return result.ToArray();
        }
        static void Equivalent(Report report,Trial trial,Case expected,object actual,string label)
        {
            actual=Concrete(trial.Own(actual),expected.Site);
            Check(report,!Native.Same(expected.Value,actual)&&NativeCommandEnvelope.NativeBytes(actual,expected.Site,trial.Version).SequenceEqual(expected.Base)&&
                Payload(actual,trial.Version).SequenceEqual(expected.Payload)&&Serialized(actual,expected.Site,trial.Version).SequenceEqual(expected.Base.Concat(expected.Payload)),label);
            if(expected.Intent is not null)Check(report,NativeExchangeCommands.Get(actual) is NativeExchangeIntent got&&
                NativeExchangeIntentCodec.Encode(got).SequenceEqual(NativeExchangeIntentCodec.Encode(expected.Intent)),label+"-complete-intent");
        }
        static void Valid(Report report,Trial trial,Case item)
        {
            string label=NativeCommandEnvelope.Owner(item.Site)+"-"+item.Name;
            byte[] expected=item.Base.Concat(item.Payload).ToArray();byte[] wire=Serialized(item.Value,item.Site,trial.Version);
            Check(report,wire.SequenceEqual(expected),label+"-native-base-and-exact-suffix");
            if(item.Intent is not null)
            {
                int numeric=Native.Int(Native.Get(item.Value,"Type"));
                Require(numeric>=0&&numeric<=ushort.MaxValue&&item.Base.Length>=3,"Inert improvement ID is outside native UInt16 wire range.");
                // Audited Build records start with base PlayerId Byte, then
                // improvement Type UInt16. Record the observed bytes as well
                // as checking them; no insertion-order ID is assumed here.
                ushort serialized=BinaryPrimitives.ReadUInt16LittleEndian(item.Base.AsSpan(1,2));
                Check(report,serialized==numeric,label+"-observed-improvement-id-fits-native-wire");
                Measure(report,label+"-improvement-enum-int32",numeric);
                Measure(report,label+"-improvement-wire-uint16",serialized);
                Measure(report,label+"-improvement-wire-bytes-hex",Convert.ToHexString(item.Base.AsSpan(1,2)).ToLowerInvariant());
            }
            using(var buffer=new Buffer())
            {
                buffer.Bytes(wire);buffer.Write("System.UInt16",Sentinel);buffer.CloseWriter();buffer.Seek(0);
                object receiver=trial.Own(New(NativeCommandEnvelope.Owner(item.Site)));
                Native.Invoke(Native.M(NativeCommandEnvelope.Owner(item.Site),"Deserialize",Reader,"System.Int32"),receiver,buffer.R,trial.Version);
                Check(report,buffer.Position==wire.Length&&Convert.ToUInt16(buffer.Read("UInt16"))==Sentinel&&buffer.Position==buffer.Length,label+"-direct-exact-boundary");
                Equivalent(report,trial,item,receiver,label+"-direct-roundtrip");
            }
            if(item.Site==2)
            {
                using var buffer=new Buffer();SerializeAction(item.Value,buffer,trial.Version);
                byte[] framed=buffer.Snapshot();Check(report,framed.SequenceEqual(ActionWire(expected,item.Value)),label+"-native-action-factory-exact-wire");
                buffer.Write("System.UInt16",Sentinel);buffer.CloseWriter();buffer.Seek(0);
                object? result=null;
                try{result=DeserializeAction(buffer,trial.Version);}
                finally{if(result is not null)trial.Own(result);}
                Require(result is not null,label+" native action factory returned null.");
                Check(report,buffer.Position==framed.Length&&Convert.ToUInt16(buffer.Read("UInt16"))==Sentinel&&buffer.Position==buffer.Length,label+"-native-action-factory-exact-boundary");
                Equivalent(report,trial,item,result!,label+"-native-action-factory-roundtrip");return;
            }
            byte[] command=NativeStateProbe.ManagedBytes(Native.Invoke(Native.M("CommandBase","ToByteArray","CommandBase","System.Int32"),null,item.Value,trial.Version)!);
            Check(report,command.SequenceEqual(Outer(expected,trial.Version,CommandType(item.Value))),label+"-factory-helper-exact-wire");
            object?[] args={NativeStateProbe.NativeBytes(command),null,0};bool ok;
            try{ok=(bool)Native.Invoke(CommandRead(),null,args)!;}
            finally{if(args[1] is not null)trial.Own(args[1]!);}
            Require(ok&&args[1] is not null&&Native.Int(args[2])==trial.Version,label+" command factory read failed.");
            Equivalent(report,trial,item,args[1]!,label+"-factory-helper-roundtrip");
        }
        static void Histories(Report report,Trial trial,byte[] sourceBytes,Case[] commands,Case[] actions)
        {
            object state=trial.State(sourceBytes);
            Native.Set(state,"CommandStack",Native.NewList("CommandBase",commands.Select(c=>c.Value)));
            Native.Set(state,"ActionStack",Native.NewList("ActionBase",actions.Select(c=>c.Value)));
            byte[] history=NativeStateProbe.ManagedBytes(Native.Invoke(Native.M("GameState","GetCommandStack"),state)!);
            using(var expected=new Buffer())
            {
                expected.Write("System.Int32",trial.Version);expected.Write("System.Int32",commands.Length);
                foreach(var item in commands){expected.Write("System.UInt16",CommandType(item.Value));expected.Bytes(item.Base.Concat(item.Payload).ToArray());}
                Check(report,history.SequenceEqual(expected.Snapshot()),"mixed-command-history-exact-version-count-type-and-suffixes");
            }
            object receiver=trial.State(sourceBytes);
            try{Native.Invoke(Native.M("GameState","LoadCommandStack",BytesType.FullName!),receiver,NativeStateProbe.NativeBytes(history));}
            finally{trial.Collect(receiver);}
            var restored=Native.Items(Native.Get(receiver,"CommandStack")!).ToArray();
            Check(report,restored.Length==commands.Length,"mixed-command-history-count");
            for(int i=0;i<commands.Length;i++)Equivalent(report,trial,commands[i],restored[i],"mixed-command-history-slot-"+i);
            Check(report,NativeStateProbe.ManagedBytes(Native.Invoke(Native.M("GameState","GetCommandStack"),receiver)!).SequenceEqual(history),"mixed-command-history-reencode-identical");
            byte[] full=NativeGameStateEnvelope.NativeBytes(state,trial.Version);object copy=trial.State(full);
            foreach(var pair in new[]{(Name:"CommandStack",Cases:commands),(Name:"ActionStack",Cases:actions)})
            {
                var entries=Native.Items(Native.Get(copy,pair.Name)!).ToArray();Check(report,entries.Length==pair.Cases.Length,"full-state-"+pair.Name+"-count");
                for(int i=0;i<entries.Length;i++)Equivalent(report,trial,pair.Cases[i],entries[i],"full-state-"+pair.Name+"-slot-"+i);
            }
            Check(report,NativeGameStateEnvelope.NativeBytes(copy,trial.Version).SequenceEqual(full),"full-state-mixed-pending-actions-byte-roundtrip");
            byte[] generic=NativeStateProbe.ManagedBytes(Native.Invoke(GenericFor("ToByteArray","GameState"),null,state,trial.Version)!);
            Check(report,generic.SequenceEqual(Outer(full,trial.Version)),"full-state-generic-positive-exact-wire");
            object?[] args={NativeStateProbe.NativeBytes(generic),null,0};bool ok;
            try{ok=(bool)Native.Invoke(GenericFor("FromByteArray","GameState"),null,args)!;}
            finally
            {
                if(args[1] is not null)
                {
                    Require(!Native.Same(args[1],trial.Source),"Generic private state aliases source.");
                    trial.States.Add(args[1]!);trial.Collect(args[1]!);
                }
            }
            Check(report,ok&&args[1] is not null&&Native.Int(args[2])==trial.Version&&
                NativeGameStateEnvelope.NativeBytes(args[1]!,trial.Version).SequenceEqual(full),"full-state-generic-positive-preserves-mixed-pending-actions");
            ContainerRejections(report,trial,sourceBytes,commands,actions);
        }
        static byte[] CorruptUniqueSchema(byte[] container,Case item)
        {
            byte[] record=item.Base.Concat(item.Payload).ToArray();int offset=-1;
            for(int i=0;i<=container.Length-record.Length;i++)
            {
                if(!container.AsSpan(i,record.Length).SequenceEqual(record))continue;
                Require(offset<0,"Private malformed-container record is ambiguous.");offset=i;
            }
            Require(offset>=0,"Private malformed-container record is absent.");
            byte[] result=(byte[])container.Clone();result[offset+item.Base.Length+4]=0xff;return result;
        }
        static void ContainerRejections(Report report,Trial trial,byte[] sourceBytes,Case[] commands,Case[] actions)
        {
            // The malformed entry is the first custom record. Native helpers
            // may retain an unpublished local list on failure; do not create
            // earlier successful private attachments hidden in that list.
            object historyState=trial.State(sourceBytes);
            Native.Set(historyState,"CommandStack",Native.NewList("CommandBase",new[]{commands[2].Value,commands[0].Value}));
            byte[] history=NativeStateProbe.ManagedBytes(Native.Invoke(Native.M("GameState","GetCommandStack"),historyState)!);
            byte[] badHistory=CorruptUniqueSchema(history,commands[2]);object historyReceiver=trial.State(sourceBytes);bool historyFailed=false;
            Native.Set(historyReceiver,"CommandStack",Native.NewList("CommandBase",new[]{commands[0].Value}));
            try{Native.Invoke(Native.M("GameState","LoadCommandStack",BytesType.FullName!),historyReceiver,NativeStateProbe.NativeBytes(badHistory));}
            catch{historyFailed=true;}
            finally{trial.Collect(historyReceiver);}
            Check(report,historyFailed,"malformed-native-command-history-rejected-by-LoadCommandStack");
            Check(report,NoExchangePayload(historyReceiver,"CommandStack"),"malformed-history-exposes-no-exchange-payload");

            object pending=trial.State(sourceBytes);
            Native.Set(pending,"CommandStack",Native.NewList("CommandBase",new[]{commands[0].Value}));
            Native.Set(pending,"ActionStack",Native.NewList("ActionBase",new[]{actions[2].Value,actions[0].Value}));
            byte[] full=NativeGameStateEnvelope.NativeBytes(pending,trial.Version);byte[] badFull=CorruptUniqueSchema(full,actions[2]);
            object receiver=New("GameState");Require(!Native.Same(receiver,trial.Source),"Private malformed receiver aliases source.");trial.States.Add(receiver);
            bool directFailed=false;
            using(var buffer=new Buffer(badFull))
            {
                try{Native.Invoke(Native.M("GameState","Deserialize",Reader,"System.Int32"),receiver,buffer.R,trial.Version);}
                catch{directFailed=true;}
                finally{trial.Collect(receiver);}
            }
            Check(report,directFailed&&NativeGameStateEnvelope.Get(receiver) is null,"malformed-pending-action-rejected-by-native-GameState-Deserialize");
            Check(report,NoExchangePayload(receiver,"ActionStack"),"malformed-pending-action-exposes-no-exchange-payload");
            object?[] args={NativeStateProbe.NativeBytes(Outer(badFull,trial.Version)),null,0};bool? ok=null;
            try{ok=(bool)Native.Invoke(GenericFor("FromByteArray","GameState"),null,args)!;}
            catch{/* Failure must still meet the native false/null publication contract below. */}
            finally
            {
                if(args[1] is not null)
                {
                    Require(!Native.Same(args[1],trial.Source),"Malformed generic state aliases source.");
                    trial.States.Add(args[1]!);trial.Collect(args[1]!);
                }
            }
            Check(report,ok==false&&args[1] is null,"malformed-pending-action-native-generic-state-read-returns-false-null");
        }
        static bool NoExchangePayload(object state,string property)
        {
            var list=Native.Get(state,property);
            return list is null||Native.Items(list).All(value=>NativeExchangeCommands.Get(value) is null);
        }
        static void Rejections(Report report,Trial trial,Case item)
        {
            string label=NativeCommandEnvelope.Owner(item.Site)+"-"+item.Name;
            byte[] good=item.Base.Concat(item.Payload).ToArray();
            var cases=new Dictionary<string,byte[]>{["native-eof"]=item.Base.Take(1).ToArray(),["truncated-suffix"]=good.Take(good.Length-1).ToArray()};
            byte[] schema=(byte[])good.Clone();schema[item.Base.Length+4]=0xff;cases.Add("unknown-schema",schema);
            if(item.Intent is not null)
            {
                var different=item.Intent with{Actor=unchecked((byte)(item.Intent.Actor+1))};
                cases.Add("carrier-actor-mismatch",item.Base.Concat(NativeExchangeIntentCodec.Encode(different)).ToArray());
                var kind=item.Intent.Kind==NativeExchangeKind.Regroup?NativeExchangeKind.Disguise:NativeExchangeKind.Regroup;
                cases.Add("carrier-kind-mismatch",item.Base.Concat(NativeExchangeIntentCodec.Encode(item.Intent with{Kind=kind})).ToArray());
            }
            foreach(var failure in cases)
            {
                object receiver=trial.Own(New(NativeCommandEnvelope.Owner(item.Site)));
                using(var seed=new Buffer(good))Native.Invoke(Native.M(NativeCommandEnvelope.Owner(item.Site),"Deserialize",Reader,"System.Int32"),receiver,seed.R,trial.Version);
                Require(Payload(receiver,trial.Version).SequenceEqual(item.Payload),"Failed to seed old private payload.");
                bool failed=false;string error="none";
                using(var buffer=new Buffer(failure.Value))
                {
                    try{Native.Invoke(Native.M(NativeCommandEnvelope.Owner(item.Site),"Deserialize",Reader,"System.Int32"),receiver,buffer.R,trial.Version);}
                    catch(Exception e){failed=true;error=e.GetType().FullName??e.GetType().Name;}
                    Check(report,failed&&Unbound(receiver,trial.Version),label+"-"+failure.Key+"-direct-rejects-and-retires-old-payload");
                    Measure(report,label+"-"+failure.Key+"-direct-error",error);
                }
                RejectedFactory(report,trial,item,failure.Value,label+"-"+failure.Key);
            }
        }
        static void RejectedFactory(Report report,Trial trial,Case item,byte[] malformed,string label)
        {
            if(item.Site==2)
            {
                using var buffer=new Buffer(ActionWire(malformed,item.Value));object? result=null;bool failed=false;string failure="none";
                try{result=DeserializeAction(buffer,trial.Version);}
                catch(Exception e){failed=true;failure=e.GetType().FullName??e.GetType().Name;}
                finally{if(result is not null)trial.Own(result);}
                Measure(report,label+"-action-factory-error",failure);
                Check(report,failed&&result is null,label+"-action-factory-throws-without-result");return;
            }
            byte[] bytes=Outer(malformed,trial.Version,CommandType(item.Value));
            object?[] args={NativeStateProbe.NativeBytes(bytes),null,0};bool? ok=null;bool threw=false,nativeException=false;string error="none";
            try{ok=(bool)Native.Invoke(CommandRead(),null,args)!;}
            catch(Exception e){threw=true;error=e.GetType().FullName??e.GetType().Name;nativeException=error=="Il2CppInterop.Runtime.Il2CppException";}
            finally{if(args[1] is not null)trial.Own(args[1]!);}
            // Native CommandBase.FromByteArray publishes its output only after
            // DeserializeCommand returns (0x67b73f/0x67b744). Its cleanup paths
            // rethrow; candidate0310 observed that IL2CPP exception. This is
            // distinct from the generic GameState helper's false/null contract.
            Measure(report,label+"-factory-error",error);
            Measure(report,label+"-factory-threw",threw);
            Measure(report,label+"-factory-native-exception",nativeException);
            Measure(report,label+"-factory-boolean-return",ok.HasValue?ok.Value.ToString():"not-returned");
            Measure(report,label+"-factory-output-is-null",args[1] is null);
            Measure(report,label+"-factory-out-version-observed",args[2]);
            Check(report,nativeException&&ok is null&&args[1] is null,label+"-factory-propagates-native-exception-without-output");
        }
        static void Replacement(Report report,Trial trial,Case plain,Case custom)
        {
            object receiver=trial.Own(New(NativeCommandEnvelope.Owner(custom.Site)));
            using(var buffer=new Buffer(custom.Base.Concat(custom.Payload).ToArray()))
                Native.Invoke(Native.M(NativeCommandEnvelope.Owner(custom.Site),"Deserialize",Reader,"System.Int32"),receiver,buffer.R,trial.Version);
            using(var buffer=new Buffer(plain.Base))
                Native.Invoke(Native.M(NativeCommandEnvelope.Owner(custom.Site),"Deserialize",Reader,"System.Int32"),receiver,buffer.R,trial.Version);
            Check(report,NativeExchangeCommands.Get(receiver) is null&&!MimicSmuggleActions.HasPayload(receiver)&&Serialized(receiver,custom.Site,trial.Version).SequenceEqual(plain.Base),
                NativeCommandEnvelope.Owner(custom.Site)+"-ordinary-replacement-retires-"+custom.Name+"-payload");
        }
        static void MissingWriter(Report report,Trial trial,Case item)
        {
            object value=trial.Own(New(NativeCommandEnvelope.Owner(item.Site)));
            using(var buffer=new Buffer(item.Base.Concat(item.Payload).ToArray()))
                Native.Invoke(Native.M(NativeCommandEnvelope.Owner(item.Site),"Deserialize",Reader,"System.Int32"),value,buffer.R,trial.Version);
            NativeExchangeCommands.Forget(value);MimicSmuggleActions.ForgetOffer(value);
            string label=NativeCommandEnvelope.Owner(item.Site)+"-"+item.Name+"-missing-payload";
            using(var buffer=new Buffer())
            {
                buffer.Bytes(new byte[]{0x01,0x55,0xD1});buffer.Write("System.UInt16",Sentinel);byte[] before=buffer.Snapshot();long position=buffer.Position;
                bool failed=false;try{Native.Invoke(Native.M(NativeCommandEnvelope.Owner(item.Site),"Serialize",Writer,"System.Int32"),value,buffer.W,trial.Version);}catch{failed=true;}
                Check(report,failed&&buffer.Position==position&&buffer.Snapshot().SequenceEqual(before),label+"-direct-fails-with-caller-buffer-unchanged");
            }
            NoFactoryBytes(report,trial,value,item.Site,label);
        }
        static void NoFactoryBytes(Report report,Trial trial,object value,uint site,string label)
        {
            if(site==2)
            {
                using(var buffer=new Buffer())
                {
                    bool failed=false;try{SerializeAction(value,buffer,trial.Version);}catch{failed=true;}
                    // The native outer helper writes its UInt16 action type
                    // before dispatch. Only the direct concrete writer has an
                    // unchanged-buffer promise; this helper must fail instead
                    // of returning a completed action record.
                    Check(report,failed,label+"-native-SerializeAction-does-not-complete");
                    Measure(report,label+"-incomplete-action-record-bytes",buffer.Position);
                }
                object state=trial.State(trial.SourceBytes);
                Native.Set(state,"CommandStack",Native.NewList("CommandBase",Array.Empty<object>()));
                Native.Set(state,"ActionStack",Native.NewList("ActionBase",new[]{value}));
                object? bytes=null;bool failedGeneric=false;
                try{bytes=Native.Invoke(GenericFor("ToByteArray","GameState"),null,state,trial.Version);}catch{failedGeneric=true;}
                Measure(report,label+"-native-state-generic-threw",failedGeneric);
                Check(report,bytes is null,label+"-native-state-generic-produces-no-byte-array");return;
            }
            object? output=null;bool threw=false;
            try{output=Native.Invoke(Native.M("CommandBase","ToByteArray","CommandBase","System.Int32"),null,value,trial.Version);}catch{threw=true;}
            Measure(report,label+"-command-factory-threw",threw);
            Check(report,output is null,label+"-command-factory-produces-no-byte-array");
        }
        static void RetaggedWriter(Report report,Trial trial,Case item)
        {
            object value=trial.Own(New(NativeCommandEnvelope.Owner(item.Site)));
            using(var buffer=new Buffer(item.Base.Concat(item.Payload).ToArray()))
                Native.Invoke(Native.M(NativeCommandEnvelope.Owner(item.Site),"Deserialize",Reader,"System.Int32"),value,buffer.R,trial.Version);
            // Mutate only this private carrier. Its existing complete binding
            // must prevent silently downgrading it into a plain native record.
            Native.Set(value,"Type",Native.EnumValue(Improvement,"harvestfruit"));
            string label=NativeCommandEnvelope.Owner(item.Site)+"-"+item.Name+"-bound-retagged-to-ordinary";
            using(var buffer=new Buffer())
            {
                buffer.Bytes(new byte[]{0x23,0x90,0xF1});buffer.Write("System.UInt16",Sentinel);byte[] before=buffer.Snapshot();long position=buffer.Position;
                bool failed=false;try{Native.Invoke(Native.M(NativeCommandEnvelope.Owner(item.Site),"Serialize",Writer,"System.Int32"),value,buffer.W,trial.Version);}catch{failed=true;}
                Check(report,failed&&buffer.Position==position&&buffer.Snapshot().SequenceEqual(before),label+"-fails-with-caller-buffer-unchanged");
            }
            NoFactoryBytes(report,trial,value,item.Site,label);
        }

        internal static Report Run(object source,int version)
        {
            var report=new Report{Scope="detached-native-command-action-transport",Limit=
                "Detached transport only. No command or action is executed, no state is published, and no live save, transaction, gameplay, replay or multiplayer success is established. " +
                "Candidate039 observed native MethodAccessException from SerializationHelpers.ToByteArray<BuildCommand> at IBinarySerializable.Serialize. " +
                "That concrete BuildCommand writer is unavailable on this runtime; its generic reader and the concrete BuildAction generic paths are unverified and excluded. Native command/action factories and the established generic GameState path are tested instead."};
            if(Running){report.Failure="Command probe reentry rejected.";return report;}
            Running=true;var trial=new Trial{Source=source,Version=version};byte[]? sourceBytes=null;
            byte[]? sourceExtension=null;long? sourceRevision=null;
            long prepared=NativeStateBridge.Prepared,cleaned=NativeStateBridge.Cleaned,rejected=NativeStateBridge.Rejected;
            try
            {
                Require((Environment.GetEnvironmentVariable("DECEPTION_NATIVE_COMMAND_PROBE")=="1"||Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXCHANGE_PROBE")=="1")&&
                    NativeStateBridge.Installed,"Command probe needs its explicit installed native bridge opt-in.");
                Require(version==NativeStateFrame.NativeVersion&&Native.Int(Native.Get(source,"Version"))==version,"Command probe requires audited state/serializer123.");
                Require(NativeCommandEnvelope.ActiveOperations==0&&NativeGameStateEnvelope.ActiveOperations==0,"Native bridge is already inside an operation.");
                _=NativeLedgerState.Graph(source); // Bounded read-only graph validation; no ledger bootstrap.
                foreach(string name in new[]{"CommandStack","ActionStack"})
                {
                    var values=Native.Get(source,name)!;Require(Native.Int(Native.Get(values,"Count"))<=16384,"Source history exceeds probe bound.");
                    foreach(var value in Native.Items(values))trial.SourceValues.Add(Native.Pointer(value));
                }
                var snapshot=NativeGameStateEnvelope.Get(source);sourceExtension=snapshot?.Bytes();sourceRevision=snapshot?.Revision;
                sourceBytes=NativeGameStateEnvelope.NativeBytes(source,version);trial.SourceBytes=sourceBytes;
                byte actor=Convert.ToByte(Native.Get(source,"CurrentPlayer"));uint turn=Convert.ToUInt32(Native.Get(source,"CurrentTurn"));
                Case[] commands=Cases(trial,1,actor,turn),actions=Cases(trial,2,actor,turn);
                foreach(var item in commands.Concat(actions))Valid(report,trial,item);
                Histories(report,trial,sourceBytes,commands,actions);
                foreach(var set in new[]{commands,actions})
                {
                    foreach(var item in set.Where(c=>c.Name is "smuggle" or "regroup-keep"))
                    {Rejections(report,trial,item);MissingWriter(report,trial,item);RetaggedWriter(report,trial,item);}
                    Replacement(report,trial,set[0],set[1]);Replacement(report,trial,set[0],set[2]);
                }
                report.Passed=true;
            }
            catch(Exception e){report.Failure=e.GetType().FullName+": "+e.Message;}
            finally
            {
                if(sourceBytes is not null)
                {
                    try
                    {
                        var snapshot=NativeGameStateEnvelope.Get(source);
                        Check(report,NativeGameStateEnvelope.NativeBytes(source,version).SequenceEqual(sourceBytes)&&
                            (sourceExtension is null?snapshot is null:snapshot is not null&&snapshot.Revision==sourceRevision&&snapshot.Bytes().SequenceEqual(sourceExtension)),
                            "supplied-source-native-bytes-and-private-binding-unchanged");
                        Measure(report,"source-native-bytes",sourceBytes.Length);Measure(report,"source-native-sha256",Convert.ToHexString(SHA256.HashData(sourceBytes)).ToLowerInvariant());
                    }
                    catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Source verification: "+e.Message;}
                }
                foreach(var state in trial.States)
                {
                    try{trial.Collect(state);NativeGameStateEnvelope.Forget(state);}
                    catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Private state cleanup: "+e.Message;}
                }
                foreach(var value in trial.Values.Values)
                {
                    try{NativeExchangeCommands.Forget(value);MimicSmuggleActions.ForgetOffer(value);}
                    catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Private carrier cleanup: "+e.Message;}
                }
                Measure(report,"private-states-retired",trial.States.Count);Measure(report,"private-carriers-retired",trial.Values.Count);
                Measure(report,"bridge-prepared",NativeStateBridge.Prepared-prepared);Measure(report,"bridge-cleaned",NativeStateBridge.Cleaned-cleaned);Measure(report,"bridge-rejected",NativeStateBridge.Rejected-rejected);
                try{Check(report,NativeStateBridge.Prepared-prepared==NativeStateBridge.Cleaned-cleaned&&NativeCommandEnvelope.ActiveOperations==0&&NativeGameStateEnvelope.ActiveOperations==0,"all-command-and-state-bridge-operations-cleaned");}
                catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Bridge cleanup: "+e.Message;}
                Running=false;
            }
            return report;
        }
    }
}
