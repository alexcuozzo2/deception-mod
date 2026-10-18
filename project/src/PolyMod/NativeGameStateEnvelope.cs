using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using System.Runtime.InteropServices;
using Buffer=Deception.PolyModAdapter.NativeStateProbe.Buffer;

namespace Deception.PolyModAdapter
{
    // Symmetric native transport. The first integration is opt-in and accepts
    // only detached diagnostic payloads. No gameplay feature consumes this data.
    internal static partial class NativeGameStateEnvelope
    {
        const string Reader="Il2CppSystem.IO.BinaryReader",Writer="Il2CppSystem.IO.BinaryWriter";
        const string Weak="Il2CppSystem.WeakReference";
        const int MaxStates=1024;
        internal sealed record Binding(object WeakState,Snapshot Data);
        static readonly Dictionary<long,Binding> States=new();
        static Func<byte[],bool>? ValidatePayload;
        static Action<object,long,byte[]>? ValidateState;
        static bool Installed;
        internal static int FramedWrites,FramedReads,LegacyReads;

        sealed class OriginalScope
        {
            internal OriginalScope? Previous;
            internal long State,Stream;
            internal int Version;
            internal bool Reading,Entered;
        }
        [ThreadStatic] static OriginalScope? CurrentOriginal;

        internal static void Install(Func<byte[],bool> validator,Action<object,long,byte[]>? stateValidator=null)
        {
            if(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_ENVELOPE_PROBE")!="1"&&Environment.GetEnvironmentVariable("DECEPTION_NATIVE_LEDGER_PROBE")!="1"&&Environment.GetEnvironmentVariable("DECEPTION_NATIVE_COMMAND_PROBE")!="1"&&Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXCHANGE_PROBE")!="1"&&!NativeFreshSession.Enabled&&!NativeRegroupSession.Enabled)return;
            if(Installed)throw new InvalidOperationException("Native state envelope already installed.");
            ValidatePayload=validator??throw new ArgumentNullException(nameof(validator));ValidateState=stateValidator;
            _=Native.T(Weak).GetConstructor(new[]{Native.T("Il2CppSystem.Object"),typeof(bool)})
                ??throw new MissingMethodException(Weak,".ctor(Object,Boolean)");
            _=Native.M(Weak,"get_Target");
            _=Native.M(Weak,"get_IsAlive");
            NativeStateBridge.Install(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_COMMAND_PROBE")=="1"||Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXCHANGE_PROBE")=="1"||NativeRegroupSession.Enabled);
            Installed=true;
        }

        static void Validate(long revision,byte[] payload)
        {
            if(!Installed||revision<0||payload is null||payload.Length==0||payload.Length>NativeStateFrame.MaxExtensionBytes||
                ValidatePayload is null||!ValidatePayload((byte[])payload.Clone()))
                throw new InvalidDataException("Invalid or unsupported Deception state payload.");
        }
        internal static void Set(object state,long revision,byte[] payload)
        {
            RejectPrivateAccess(state);
            if(!Native.T("GameState").IsInstanceOfType(state)||Native.Int(Native.Get(state,"Version"))!=NativeStateFrame.NativeVersion)
                throw new InvalidDataException("Deception state payload requires an exact native version 123 GameState.");
            Validate(revision,payload);ValidateState?.Invoke(state,revision,(byte[])payload.Clone());
            var snapshot=new Snapshot(revision,payload);
            object weak=Native.T(Weak).GetConstructor(new[]{Native.T("Il2CppSystem.Object"),typeof(bool)})!.Invoke(new[]{state,(object)false});
            lock(States)
            {
                RejectPrivateAccess(state);
                foreach(var expired in States.Where(x=>!(bool)Native.Get(x.Value.WeakState,"IsAlive")!).Select(x=>x.Key).ToArray())States.Remove(expired);
                long key=Native.Pointer(state);
                if(!States.ContainsKey(key)&&States.Count>=MaxStates)throw new InvalidDataException("Too many live Deception state payloads.");
                RejectPrivateAccess(state);
                States[key]=new(weak,snapshot);
            }
        }
        internal static Snapshot? Get(object state)
        {
            RejectPrivateAccess(state);
            lock(States)
            {
                RejectPrivateAccess(state);
                long key=Native.Pointer(state);
                if(!States.TryGetValue(key,out var binding))return null;
                // A native weak target survives managed wrapper recreation but
                // cannot silently transfer authority to a reused native pointer.
                var target=Native.Get(binding.WeakState,"Target");
                RejectPrivateAccess(state);
                if(target is null||!Native.Same(target,state)){States.Remove(key);return null;}
                // Snapshot owns its bytes, exposes copies and never changes.
                // Reuse that immutable binding: selection and rendering read it
                // several times per frame, without copying the whole ledger.
                return binding.Data;
            }
        }
        internal static void Forget(object state){RejectPrivateAccess(state);lock(States){RejectPrivateAccess(state);States.Remove(Native.Pointer(state));}}

        static bool EnterOriginal(bool reading,object state,object stream,int version)
        {
            var scope=CurrentOriginal;
            if(scope is null||scope.Entered||scope.Reading!=reading||scope.Version!=version||scope.State!=Native.Pointer(state)||scope.Stream!=Native.Pointer(stream))return false;
            scope.Entered=true;return true;
        }
        static void Original(bool reading,object state,object stream,int version)
        {
            var scope=new OriginalScope{Previous=CurrentOriginal,Reading=reading,Version=version,State=Native.Pointer(state),Stream=Native.Pointer(stream)};
            CurrentOriginal=scope;
            try
            {
                Native.Invoke(Native.M("GameState",reading?"Deserialize":"Serialize",reading?Reader:Writer,"System.Int32"),state,stream,version);
                if(!scope.Entered)throw new InvalidOperationException("Native original-call scope did not enter its matching hook.");
            }
            finally{CurrentOriginal=scope.Previous;}
        }
        internal static byte[] NativeBytes(object state,int version)
        {RejectPrivateAccess(state);using var b=new Buffer();Original(false,state,b.W,version);return b.Snapshot();}

        sealed class Operation
        {
            internal NativeStateBridge.Call Call;
            internal object State,Stream;
            internal Buffer? Private;
            internal Snapshot? Payload;
            internal bool Reading,Bypass,Framed,Published;
            internal NativeRegroupSession.Transport? Production;
            internal Operation(NativeStateBridge.Call call,object state,object stream)
            {Call=call;State=state;Stream=stream;Reading=call.Operation==1;}
        }
        static readonly Dictionary<ulong,Operation> Operations=new();
        static ulong NextOperation;
        internal static int ActiveOperations {get{lock(Operations)return Operations.Count;}}
        internal static ulong AllocatedOperations {get{lock(Operations)return NextOperation;}}
        static object Wrap(string type,IntPtr pointer)=>Native.T(type).GetConstructor(new[]{typeof(IntPtr)})!.Invoke(new object[]{pointer});
        internal static int Prepare(NativeStateBridge.Call call,ref NativeStateBridge.Plan plan)
        {
            if(call.Size!=40||call.Site!=0||call.Operation is <0 or >1||call.State==IntPtr.Zero||call.Stream==IntPtr.Zero)
                throw new InvalidDataException("Invalid native state bridge call.");
            // This check precedes the operation cookie, stream access and any
            // binding retirement. Managed loader exceptions are swallowed by
            // its caller; native reads/writes must reject a failed registry.
            NativeRegistryOrder.RequireCompatible();
            object state=Wrap("GameState",call.State);
            RejectPrivateAccess(state);
            NativeFreshSession.BeforeTransport(state,call.Operation==1);
            var production=NativeRegroupSession.BeforeTransport(state,call.Operation==1);
            // Reject protected receivers/writers before any stream access,
            // cookie allocation, raw-original bypass or binding retirement.
            // Cleanup of a rejected policy call receives cookie zero.
            if(call.Operation==1)NativeLedgerOwnership.BeforeRead(state);
            else NativeLedgerOwnership.BeforeWrite(state);
            var op=new Operation(call,state,Wrap(call.Operation==1?Reader:Writer,call.Stream)){Production=production};
            lock(Operations)
            {
                if(Operations.Count>=32||NextOperation==ulong.MaxValue)throw new InvalidDataException("Native state bridge operation bound exceeded.");
                plan.Cookie=++NextOperation;Operations.Add(plan.Cookie,op);
            }
            if(EnterOriginal(op.Reading,op.State,op.Stream,call.Version)){op.Bypass=true;return 0;}
            if(!op.Reading)
            {
                op.Payload=Get(op.State);if(op.Payload is null)return 0;
                Validate(op.Payload.Revision,op.Payload.Bytes());ValidateState?.Invoke(op.State,op.Payload.Revision,op.Payload.Bytes());
                if(call.Version!=NativeStateFrame.NativeVersion)throw new InvalidDataException("Framed native writer version mismatch.");
                op.Framed=true;op.Private=new Buffer();plan.Replacement=new IntPtr(Native.Pointer(op.Private.W));return 1;
            }
            Forget(op.State);
            var stream=Native.Get(op.Stream,"BaseStream")!;
            if(!(bool)Native.Get(stream,"CanSeek")!)throw new InvalidDataException("Deception native state reading requires a seekable stream.");
            long start=Convert.ToInt64(Native.Get(stream,"Position"));
            if(Convert.ToInt64(Native.Get(stream,"Length"))-start<4)throw new InvalidDataException("Truncated native state prefix.");
            uint marker=Convert.ToUInt32(Native.Invoke(Native.M(Reader,"ReadUInt32"),op.Stream));
            Native.Set(stream,"Position",start);
            if(marker!=NativeStateFrame.Magic){NativeLedgerOwnership.BeforeLegacyRead();NativeRegroupSession.BeforeLegacyRead(op.State,op.Production);LegacyReads++;return 0;}
            byte[] header=ReadExact(op.Stream,NativeStateFrame.HeaderBytes);
            int length=NativeStateFrame.LengthFromHeader(header,call.Version);
            byte[] tail=ReadExact(op.Stream,length-header.Length);
            var frame=new byte[length];header.CopyTo(frame,0);tail.CopyTo(frame,header.Length);
            var decoded=NativeStateFrame.Decode(frame,call.Version);Validate(decoded.Revision,decoded.Extension);
            NativeLedgerOwnership.BeforeFrame(decoded.Revision,decoded.Extension);
            NativeFreshSession.BeforeFrame(op.State,decoded.Revision,decoded.Extension);
            NativeRegroupSession.BeforeFrame(op.State,true,op.Production,decoded.Revision,decoded.Extension);
            op.Payload=new Snapshot(decoded.Revision,decoded.Extension);op.Framed=true;
            op.Private=new Buffer(decoded.Native);plan.Replacement=new IntPtr(Native.Pointer(op.Private.R));return 1;
        }
        static Operation Find(NativeStateBridge.Call call,NativeStateBridge.Plan plan)
        {
            lock(Operations)
            {
                if(!Operations.TryGetValue(plan.Cookie,out var op)||op.Call.State!=call.State||op.Call.Stream!=call.Stream||
                    op.Call.Operation!=call.Operation||op.Call.Version!=call.Version||op.Call.MethodInfo!=call.MethodInfo)
                    throw new InvalidDataException("Native state bridge operation identity mismatch.");
                return op;
            }
        }
        internal static void Finish(NativeStateBridge.Call call,NativeStateBridge.Plan plan)
        {
            var op=Find(call,plan);if(op.Bypass)return;
            if(!op.Framed){NativeRegroupSession.Completed(op.State,op.Reading,false,op.Production);return;}
            if(op.Private is null||op.Payload is null)throw new InvalidDataException("Missing prepared native state operation.");
            if(op.Reading)
            {
                if(op.Private.Position!=op.Private.Length)throw new InvalidDataException("Native state did not consume its exact Deception frame section.");
                NativeLedgerOwnership.BeforeFrame(op.Payload.Revision,op.Payload.Bytes());
                NativeFreshSession.BeforePublish(op.State,true,op.Payload.Revision,op.Payload.Bytes());
                NativeRegroupSession.BeforeFrame(op.State,true,op.Production,op.Payload.Revision,op.Payload.Bytes());
                Set(op.State,op.Payload.Revision,op.Payload.Bytes());op.Published=true;FramedReads++;
                NativeFreshSession.FrameCompleted(op.State,true);
                NativeRegroupSession.Completed(op.State,true,true,op.Production);
            }
            else
            {
                ValidateState?.Invoke(op.State,op.Payload.Revision,op.Payload.Bytes());
                byte[] frame=NativeStateFrame.Encode(op.Private.Snapshot(),op.Payload.Bytes(),op.Payload.Revision,call.Version);
                var bytes=NativeStateProbe.NativeBytes(frame);
                NativeFreshSession.BeforePublish(op.State,false,op.Payload.Revision,op.Payload.Bytes());
                NativeRegroupSession.BeforeFrame(op.State,false,op.Production,op.Payload.Revision,op.Payload.Bytes());
                Native.Invoke(Native.M(Writer,"Write",bytes.GetType().FullName!),op.Stream,bytes);FramedWrites++;
                NativeFreshSession.FrameCompleted(op.State,false);
                NativeRegroupSession.Completed(op.State,false,true,op.Production);
            }
        }
        internal static void Cleanup(NativeStateBridge.Call call,NativeStateBridge.Plan plan,int outcome)
        {
            if(plan.Cookie==0)return;
            var op=Find(call,plan);lock(Operations)Operations.Remove(plan.Cookie);
            try{op.Private?.Dispose();}
            catch{if(op.Reading&&!op.Bypass)Forget(op.State);throw;}
            finally{if(outcome!=0){NativeRegroupSession.Failed(op.Production,"cleanup/"+outcome);if(op.Reading&&!op.Bypass)Forget(op.State);}}
        }

        static byte[] ReadExact(object reader,int count)
        {
            var stream=Native.Get(reader,"BaseStream")!;
            long remaining=Convert.ToInt64(Native.Get(stream,"Length"))-Convert.ToInt64(Native.Get(stream,"Position"));
            if(count<0||count>NativeStateFrame.HeaderBytes+NativeStateFrame.MaxNativeBytes+NativeStateFrame.MaxExtensionBytes||count>remaining)
                throw new InvalidDataException("Truncated or oversized Deception state frame.");
            byte[] bytes=NativeStateProbe.ManagedBytes(Native.Invoke(Native.M(Reader,"ReadBytes","System.Int32"),reader,count)!);
            if(bytes.Length!=count)throw new InvalidDataException("Short native Deception frame read.");
            return bytes;
        }
    }
}
