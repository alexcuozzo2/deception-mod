using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Deception.PolyModAdapter
{
    // OPT-IN DETACHED DIAGNOSTIC ONLY. No hooks, callbacks, filesystem, Client, GameManager,
    // save, action execution or automatic trigger. The caller must explicitly
    // supply a quiescent NEW DISPOSABLE match state on the native main thread.
    // Reports contain counts/checksums; they never return a detached state.
    internal static class NativeStateProbe
    {
        const string MS="Il2CppSystem.IO.MemoryStream", Stream="Il2CppSystem.IO.Stream";
        const string BR="Il2CppSystem.IO.BinaryReader", BW="Il2CppSystem.IO.BinaryWriter";
        const string EncodingType="Il2CppSystem.Text.Encoding";
        const int MaxNativeBytes=16*1024*1024,MaxOuterBytes=3*(MaxNativeBytes+128)+64;
        const int MaxTiles=4096,MaxEntries=16384,MaxUnitDepth=8;
        const uint Magic=0x50435044; // Private trial only. NOT a legacy discriminator.
        const ushort FrameSchema=1, Sentinel=0xA65C;
        const int ExtensionBytes=12;
        [ThreadStatic] static bool Running;

        internal sealed class Report
        {
            public bool Passed { get; set; }
            public string Scope { get; set; }="";
            public List<string> Checks { get; }=new();
            public Dictionary<string,string> Measurements { get; }=new();
            public string? Failure { get; set; }
            public string Limit { get; set; }="Detached buffers only; no live save, client copy, replay, transport or persistence success is established.";
        }
        sealed class Observation
        {
            internal string Digest="";
            internal HashSet<long> MutablePointers=new();
            internal int Bodies,Passengers;
        }
        internal sealed class Buffer:IDisposable
        {
            internal object Value;
            object? Writer,Reader;
            internal Buffer(byte[]? input=null)
            {
                if(input is not null)Require(input.Length<=MaxOuterBytes,"Input buffer is too large.");
                Value=input is null?New(MS):New(MS,new[]{ByteArrayType,typeof(bool)},NativeBytes(input),false);
            }
            internal object W=>Writer??=New(BW,new[]{Native.T(Stream),Native.T(EncodingType),typeof(bool)},
                Value,Native.Static(EncodingType,"UTF8")!,true);
            internal object R=>Reader??=New(BR,new[]{Native.T(Stream),Native.T(EncodingType),typeof(bool)},
                Value,Native.Static(EncodingType,"UTF8")!,true);
            internal long Position=>Convert.ToInt64(Native.Get(Value,"Position"));
            internal long Length=>Convert.ToInt64(Native.Get(Value,"Length"));
            internal void Seek(long position)=>Native.Set(Value,"Position",position);
            internal void Write(string type,object value)=>Native.Invoke(Native.M(BW,"Write",type),W,value);
            internal void Bytes(byte[] value)=>Native.Invoke(Native.M(BW,"Write",ByteArrayType.FullName!),W,NativeBytes(value));
            internal object Read(string suffix)=>Native.Invoke(Native.M(BR,"Read"+suffix),R)!;
            internal byte[] ReadExact(int length)
            {
                Require(length>=0&&length<=MaxNativeBytes,"Invalid read length.");
                Require(length<=Length-Position,"Truncated private buffer.");
                byte[] result=ManagedBytes(Native.Invoke(Native.M(BR,"ReadBytes","System.Int32"),R,length)!);
                Require(result.Length==length,"Native ReadBytes returned a short read.");return result;
            }
            internal byte[] Snapshot()
            {
                if(Writer is not null)Native.Invoke(Native.M(BW,"Flush"),Writer);
                Require(Length<=MaxOuterBytes,"Native output exceeded the probe bound.");
                return ManagedBytes(Native.Invoke(Native.M(MS,"ToArray"),Value)!);
            }
            internal void CloseWriter()
            {if(Writer is not null){Native.Invoke(Native.M(BW,"Dispose"),Writer);Writer=null;}}
            internal void CloseReader()
            {if(Reader is not null){Native.Invoke(Native.M(BR,"Dispose"),Reader);Reader=null;}}
            public void Dispose()
            {
                try{CloseReader();}finally{try{CloseWriter();}finally{Native.Invoke(Native.M(Stream,"Dispose"),Value);}}
            }
        }
        static Type ByteArrayType=>Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
        internal static object NativeBytes(byte[] bytes)=>New(ByteArrayType,new[]{typeof(byte[])},bytes);
        internal static byte[] ManagedBytes(object bytes)
        {
            var arrayBase=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppArrayBase`1").MakeGenericType(typeof(byte));
            return (byte[])Native.Invoke(Native.M(arrayBase,"op_Implicit",arrayBase.FullName!),null,bytes)!;
        }
        static object New(string type)=>New(Native.T(type),Type.EmptyTypes);
        static object New(string type,Type[] args,params object?[] values)=>New(Native.T(type),args,values);
        static object New(Type type,Type[] args,params object?[] values)
        {
            var ctor=type.GetConstructor(args)??throw new MissingMethodException(type.FullName,".ctor("+string.Join(",",args.Select(t=>t.FullName))+")");
            try{return ctor.Invoke(values);}catch(TargetInvocationException e)when(e.InnerException is not null)
            {System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
        }
        static void Require(bool condition,string reason){if(!condition)throw new InvalidDataException(reason);}
        static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        static void Check(Report r,bool condition,string name){Require(condition,name);r.Checks.Add(name);}

        // This method is safe to call before a match; it uses only its new streams.
        public static Report RunStreamProbe()=>Run("native-memory-stream",r=>StreamTrial(r));

        // The native record serializer version is explicit. The audited current-
        // state caller passes its Version (currently 123), never envelope version
        // 2. Preserve the received argument for nested sibling records.
        public static Report RunDetachedStateProbe(object disposableState,int serializerVersion)=>Run("detached-native-game-state",r=>
        {
            Require(serializerVersion>=0,"A verified nonnegative native serializer version is required.");
            Require(Native.T("GameState").IsInstanceOfType(disposableState),"Expected a supplied native GameState.");
            StreamTrial(r);Preflight(disposableState);
            Observation before=Observe(disposableState);
            byte[] bytes=Serialize(disposableState,"GameState",serializerVersion);
            Require(bytes.Length>0&&bytes.Length<=MaxNativeBytes,"Native state byte length is outside the probe bound.");
            var clones=Enumerable.Range(0,3).Select(_=>DeserializeState(bytes,serializerVersion)).ToArray();
            for(int i=0;i<clones.Length;i++)
            {
                Observation after=Observe(clones[i]);
                Check(r,before.Digest==after.Digest,$"clone-{i+1}-unit-graph-and-action-fields");
                Check(r,!before.MutablePointers.Overlaps(after.MutablePointers),$"clone-{i+1}-mutable-objects-detached");
                for(int j=0;j<i;j++)Check(r,!Observe(clones[j]).MutablePointers.Overlaps(after.MutablePointers),$"clones-{j+1}-{i+1}-independent");
                Check(r,Serialize(clones[i],"GameState",serializerVersion).SequenceEqual(bytes),$"clone-{i+1}-native-bytes-equal");
            }
            LegacySequence(r,clones,2,bytes,serializerVersion);
            LegacySequence(r,clones,3,bytes,serializerVersion);
            LegacyContainer(r,"LocalGameData",clones,bytes,serializerVersion);
            LegacyContainer(r,"HotseatGameData",clones,bytes,serializerVersion);
            FrameSequence(r,bytes,serializerVersion,2);
            FrameSequence(r,bytes,serializerVersion,3);
            MalformedFrames(r,bytes,serializerVersion);
            Check(r,Observe(disposableState).Digest==before.Digest,"source-explicit-fields-unchanged");
            Check(r,Serialize(disposableState,"GameState",serializerVersion).SequenceEqual(bytes),"source-native-bytes-unchanged");
            r.Measurements["nativeBytes"]=bytes.Length.ToString(CultureInfo.InvariantCulture);
            r.Measurements["nativeSha256"]=Hash(bytes);
            r.Measurements["unitGraphSha256"]=before.Digest;
            r.Measurements["bodies"]=before.Bodies.ToString(CultureInfo.InvariantCulture);
            r.Measurements["passengers"]=before.Passengers.ToString(CultureInfo.InvariantCulture);
            r.Measurements["serializerVersion"]=serializerVersion.ToString(CultureInfo.InvariantCulture);
        });
        static Report Run(string scope,Action<Report> test)
        {
            var report=new Report{Scope=scope};
            if(Running){report.Failure="Probe reentry rejected.";return report;}
            Running=true;
            try{test(report);report.Passed=true;}
            catch(Exception e){report.Failure=e.GetType().FullName+": "+e.Message;}
            finally{Running=false;}
            return report;
        }
        static void StreamTrial(Report r)
        {
            byte[] pattern={0x00,0x7f,0x80,0xff,0x20,0x55,0xaa,0x01};
            using var b=new Buffer();
            Check(r,(bool)Native.Get(b.Value,"CanRead")!&&(bool)Native.Get(b.Value,"CanWrite")!&&(bool)Native.Get(b.Value,"CanSeek")!,"native-stream-capabilities");
            b.Bytes(pattern);b.Write("System.UInt16",Sentinel);
            var expected=pattern.Concat(new[]{(byte)(Sentinel&0xff),(byte)(Sentinel>>8)}).ToArray();
            Check(r,b.Position==expected.Length&&b.Snapshot().SequenceEqual(expected),"native-byte-array-write-and-little-endian-sentinel");
            Check(r,Native.Same(Native.Get(b.W,"BaseStream"),b.Value),"writer-base-stream-identity");
            b.CloseWriter();
            Check(r,(bool)Native.Get(b.Value,"CanRead")!&&(bool)Native.Get(b.Value,"CanSeek")!,"writer-dispose-leaves-stream-open");
            b.Seek(0);Check(r,Native.Same(Native.Get(b.R,"BaseStream"),b.Value),"reader-base-stream-identity");
            Check(r,b.ReadExact(pattern.Length).SequenceEqual(pattern)&&Convert.ToUInt16(b.Read("UInt16"))==Sentinel,"native-reader-roundtrip");
            Check(r,b.Position==b.Length&&ManagedBytes(Native.Invoke(Native.M(BR,"ReadBytes","System.Int32"),b.R,1)!).Length==0,"native-readbytes-eof");
            b.CloseReader();Check(r,(bool)Native.Get(b.Value,"CanSeek")!,"reader-dispose-leaves-stream-open");
            b.Seek(0);b.Read("UInt32");b.Seek(0);
            Check(r,b.Position==0&&b.ReadExact(pattern.Length).SequenceEqual(pattern),"probe-peek-and-exact-cursor-restore");
        }
        static void Preflight(object state)
        {
            var map=Native.Get(state,"Map")??throw new InvalidDataException("State has no native map.");
            int tiles=Convert.ToInt32(Native.Get(Native.Get(map,"Tiles")!,"Length"));
            Require(tiles>0&&tiles<=MaxTiles,"Probe requires a bounded disposable map.");
            foreach(string p in new[]{"CommandStack","ActionStack","pendingCommandTriggers","PlayerStates"})
            {
                var list=Native.Get(state,p)!;Require(Native.Int(Native.Get(list,"Count"))<=MaxEntries,"Native list exceeds probe bound.");
                if(p is "CommandStack" or "ActionStack")foreach(object entry in Native.Items(list))
                    Require(!MimicSmuggleActions.IsSmuggle(entry),"Custom Smuggle history requires a separate scoped attachment probe; use a clean disposable state.");
            }
        }
        internal static byte[] Serialize(object value,string type,int version)
        {using var b=new Buffer();Native.Invoke(Native.M(type,"Serialize",BW,"System.Int32"),value,b.W,version);return b.Snapshot();}
        static object ReadState(Buffer b,int version)
        {
            // Always fresh: native Deserialize may reuse a receiver's map,
            // player lists and caches. The audited ctor samples OS/crypto seed
            // entropy, not Unity/game-state RNG; this read overwrites its seeds.
            object state=New("GameState");Native.Invoke(Native.M("GameState","Deserialize",BR,"System.Int32"),state,b.R,version);return state;
        }
        internal static object DeserializeState(byte[] bytes,int version)
        {using var b=new Buffer(bytes);object state=ReadState(b,version);Require(b.Position==b.Length,"Native state did not consume exactly its section.");return state;}
        static void LegacySequence(Report r,object[] states,int count,byte[] bytes,int version)
        {
            using var b=new Buffer();for(int i=0;i<count;i++)Native.Invoke(Native.M("GameState","Serialize",BW,"System.Int32"),states[i],b.W,version);
            b.Write("System.UInt16",Sentinel);var serialized=b.Snapshot();b.CloseWriter();b.Seek(0);
            for(int i=0;i<count;i++)
            {
                var state=ReadState(b,version);
                Check(r,b.Position==(long)(i+1)*bytes.Length&&Serialize(state,"GameState",version).SequenceEqual(bytes),$"legacy-{count}-state-boundary-{i+1}");
            }
            Check(r,Convert.ToUInt16(b.Read("UInt16"))==Sentinel&&b.Position==serialized.Length,$"legacy-{count}-trailing-sentinel");
        }
        static void LegacyContainer(Report r,string type,object[] states,byte[] expected,int version)
        {
            object data=New(type);Native.Set(data,"initialGameState",states[0]);Native.Set(data,"currentGameState",states[1]);
            if(type=="HotseatGameData")
            {
                Native.Set(data,"lastTurnGameState",states[2]);
                var array=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(ushort));
                int players=Native.Int(Native.Get(Native.Get(states[0],"PlayerStates")!,"Count"));
                Require(players>0&&players<=16,"Probe requires a bounded native player roster.");
                var seen=Enumerable.Range(0,players).Select(i=>(ushort)(7+i)).ToArray();
                Native.Set(data,"lastSeenCommands",New(array,new[]{typeof(ushort[])},seen));
            }
            else Native.Set(data,"lastSeenCommand",(ushort)19);
            using var b=new Buffer();Native.Invoke(Native.M(type,"Serialize",BW,"System.Int32"),data,b.W,version);
            long nativeEnd=b.Position;b.Write("System.UInt16",Sentinel);b.Snapshot();b.CloseWriter();b.Seek(0);
            object restored=New(type);Native.Invoke(Native.M(type,"Deserialize",BR,"System.Int32"),restored,b.R,version);
            Check(r,b.Position==nativeEnd&&Convert.ToUInt16(b.Read("UInt16"))==Sentinel&&b.Position==b.Length,type+"-native-boundary-and-outer-sentinel");
            foreach(string p in type=="HotseatGameData"?new[]{"initialGameState","currentGameState","lastTurnGameState"}:new[]{"initialGameState","currentGameState"})
                Check(r,Serialize(Native.Get(restored,p)!,"GameState",version).SequenceEqual(expected),type+"-"+p);
            Check(r,Serialize(data,type,version).SequenceEqual(Serialize(restored,type,version)),type+"-full-native-roundtrip");
        }
        static Observation Observe(object state)
        {
            var o=new Observation();var text=new StringBuilder();
            // CommandTrigger is a native value struct; its managed box has no
            // native object pointer and does not represent a shared mutable node.
            void Own(object? value){if(value is not null&&!value.GetType().IsValueType)o.MutablePointers.Add(Native.Pointer(value));}
            void Scalar(object value,string p)=>text.Append(p).Append('=').Append(Convert.ToString(Native.Get(value,p),CultureInfo.InvariantCulture)).Append(';');
            void Coordinates(object value,string p){text.Append(p).Append('=');var c=Native.Get(value,p)!;Scalar(c,"X");Scalar(c,"Y");}
            var visited=new HashSet<long>();
            void Unit(object unit,int depth)
            {
                Require(depth<=MaxUnitDepth&&visited.Add(Native.Pointer(unit)),"Invalid or cyclic passenger graph.");
                Own(unit);if(depth==0)o.Bodies++;else o.Passengers++;
                text.Append("unit{");
                foreach(string p in new[]{"id","owner","type","leader","follower","health","promotionLevel","xp","moved","attacked","createdTurn"})Scalar(unit,p);
                Coordinates(unit,"coordinates");Coordinates(unit,"home");
                var effects=Native.Get(unit,"effects");Own(effects);
                if(effects is not null)
                {
                    Require(Native.Int(Native.Get(effects,"Count"))<=MaxEntries,"Unit effects exceed the probe bound.");
                    foreach(var effect in Native.Items(effects))text.Append(Convert.ToInt32(effect)).Append(',');
                }
                var passenger=Native.Get(unit,"passengerUnit");if(passenger is not null)Unit(passenger,depth+1);
                text.Append('}');
            }
            Own(state);Own(Native.Get(state,"Settings"));
            foreach(string p in new[]{"Version","CurrentTurn","CurrentPlayerIndex","CurrentUnitId","CurrentCommand","CurrentState"})Scalar(state,p);
            foreach(string p in new[]{"CommandStack","ActionStack","pendingCommandTriggers","PlayerStates"})
            {
                var list=Native.Get(state,p)!;Own(list);text.Append(p).Append(':').Append(Native.Int(Native.Get(list,"Count"))).Append(';');
                foreach(object entry in Native.Items(list))Own(entry);
            }
            var map=Native.Get(state,"Map")!;Own(map);Scalar(map,"Width");Scalar(map,"Height");
            var tiles=Native.Get(map,"Tiles")!;Own(tiles);int count=Native.Int(Native.Get(tiles,"Length"));
            var item=Native.M(tiles.GetType(),"get_Item","System.Int32");
            for(int i=0;i<count;i++)
            {
                var tile=Native.Invoke(item,tiles,i)!;Own(tile);var unit=Native.Get(tile,"unit");
                if(unit is not null){text.Append(i).Append(':');Unit(unit,0);}
            }
            o.Digest=Hash(Encoding.UTF8.GetBytes(text.ToString()));return o;
        }
        // Private explicit framing trial. No autodetection or production hook:
        // a marker alone is NOT proven disjoint from every legacy state prefix.
        static byte[] Extension(uint token,ulong revision)
        {using var b=new MemoryStream();using var w=new BinaryWriter(b);w.Write(token);w.Write(revision);return b.ToArray();}
        static byte[] Frame(byte[] native,int version,uint token,ulong revision)
        {
            byte[] extension=Extension(token,revision);using var b=new Buffer();
            b.Write("System.UInt32",Magic);b.Write("System.UInt16",FrameSchema);b.Write("System.Int32",version);
            b.Write("System.Int32",native.Length);b.Write("System.Int32",extension.Length);
            b.Bytes(SHA256.HashData(native));b.Bytes(SHA256.HashData(extension));b.Bytes(native);b.Bytes(extension);return b.Snapshot();
        }
        static (object State,uint Token,ulong Revision) ReadFrame(Buffer b,int version)
        {
            Require(b.Length-b.Position>=18,"Private frame header is truncated.");
            Require(Convert.ToUInt32(b.Read("UInt32"))==Magic,"Private frame marker mismatch.");
            Require(Convert.ToUInt16(b.Read("UInt16"))==FrameSchema,"Unsupported private frame schema.");
            Require(Convert.ToInt32(b.Read("Int32"))==version,"Private frame native version mismatch.");
            int nativeLength=Convert.ToInt32(b.Read("Int32")),extensionLength=Convert.ToInt32(b.Read("Int32"));
            Require(nativeLength>0&&nativeLength<=MaxNativeBytes&&extensionLength==ExtensionBytes,"Private frame length rejected.");
            Require(checked((long)nativeLength+extensionLength+64)<=b.Length-b.Position,"Private frame is truncated.");
            byte[] nativeHash=b.ReadExact(32),extensionHash=b.ReadExact(32),native=b.ReadExact(nativeLength),extension=b.ReadExact(extensionLength);
            Require(SHA256.HashData(native).SequenceEqual(nativeHash)&&SHA256.HashData(extension).SequenceEqual(extensionHash),"Private frame integrity rejected.");
            using var ext=new MemoryStream(extension,false);using var reader=new BinaryReader(ext);
            uint token=reader.ReadUInt32();ulong revision=reader.ReadUInt64();Require(token>0&&revision>0&&ext.Position==ext.Length,"Private extension rejected.");
            // Publish only as this return value after every section is checked.
            return (DeserializeState(native,version),token,revision);
        }
        static void FrameSequence(Report r,byte[] native,int version,int count)
        {
            using var b=new Buffer();var lengths=new List<long>();
            for(int i=0;i<count;i++){b.Bytes(Frame(native,version,(uint)i+1,(ulong)i+101));lengths.Add(b.Position);}
            b.Write("System.UInt16",Sentinel);b.Snapshot();b.CloseWriter();b.Seek(0);
            for(int i=0;i<count;i++)
            {
                var result=ReadFrame(b,version);
                Check(r,result.Token==(uint)i+1&&result.Revision==(ulong)i+101&&b.Position==lengths[i]&&Serialize(result.State,"GameState",version).SequenceEqual(native),$"private-frame-{count}-distinct-revision-{i+1}");
            }
            Check(r,Convert.ToUInt16(b.Read("UInt16"))==Sentinel&&b.Position==b.Length,$"private-frame-{count}-trailing-sentinel");
        }
        static void MalformedFrames(Report r,byte[] native,int version)
        {
            byte[] good=Frame(native,version,1,101);
            var cases=new Dictionary<string,byte[]>();
            cases["truncated-header"]=good.Take(9).ToArray();cases["truncated-tail"]=good.Take(good.Length-1).ToArray();
            byte[] schema=(byte[])good.Clone();schema[4]=0xff;schema[5]=0xff;cases["unknown-schema"]=schema;
            byte[] huge=(byte[])good.Clone();Array.Copy(BitConverter.GetBytes(int.MaxValue),0,huge,10,4);cases["oversized-native-length"]=huge;
            byte[] negative=(byte[])good.Clone();Array.Copy(BitConverter.GetBytes(-1),0,negative,14,4);cases["negative-extension-length"]=negative;
            byte[] corrupt=(byte[])good.Clone();corrupt[^1]^=0x20;cases["corrupt-extension"]=corrupt;
            foreach(var pair in cases)
            {
                bool rejected=false;using var b=new Buffer(pair.Value);
                try{_=ReadFrame(b,version);}catch(InvalidDataException){rejected=true;}
                Check(r,rejected,"private-frame-rejects-"+pair.Key);
            }
        }
    }
}
