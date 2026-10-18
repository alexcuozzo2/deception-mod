using System;
using System.Collections.Generic;
using System.IO;
using Deception.Core;
using Buffer=Deception.PolyModAdapter.NativeStateProbe.Buffer;

namespace Deception.PolyModAdapter
{
    // Uses the raw native command/action serializer pair, never a Harmony read
    // hook. The native base record is unchanged. Tagged records append their
    // complete private payload; plain records append nothing.
    internal static class NativeCommandEnvelope
    {
        const string Reader="Il2CppSystem.IO.BinaryReader",Writer="Il2CppSystem.IO.BinaryWriter";
        sealed class Operation
        {
            internal NativeStateBridge.Call Call;
            internal object Value=null!,Stream=null!;
            internal Buffer? Private;
            internal bool Bypass,Reading;
        }
        sealed class OriginalScope
        {
            internal OriginalScope? Previous;internal uint Site;internal long Value,Stream;internal int Version;internal bool Entered;
        }
        [ThreadStatic] static OriginalScope? CurrentOriginal;
        static readonly Dictionary<ulong,Operation> Operations=new();static ulong Next;
        internal static int ActiveOperations {get{lock(Operations)return Operations.Count;}}
        internal static string Owner(uint site)=>site switch {1=>"BuildCommand",2=>"BuildAction",_=>throw new InvalidDataException("Unknown command bridge site.")};
        static object Wrap(string type,IntPtr pointer)=>Native.T(type).GetConstructor(new[]{typeof(IntPtr)})!.Invoke(new object[]{pointer});
        static bool Exchange(object value)=>NativeExchangeCommands.Get(value) is not null||NativeExchangeCommands.IsExchange(value);
        static void Forget(object value){NativeExchangeCommands.Forget(value);MimicSmuggleActions.ForgetOffer(value);}
        internal static byte[] NativeBytes(object value,uint site,int version)
        {
            using var buffer=new Buffer();var scope=new OriginalScope{Previous=CurrentOriginal,Site=site,Value=Native.Pointer(value),Stream=Native.Pointer(buffer.W),Version=version};
            CurrentOriginal=scope;
            try
            {
                Native.Invoke(Native.M(Owner(site),"Serialize",Writer,"System.Int32"),value,buffer.W,version);
                if(!scope.Entered)throw new InvalidDataException("Original command serializer scope was not entered.");return buffer.Snapshot();
            }
            finally{CurrentOriginal=scope.Previous;}
        }
        internal static int Prepare(NativeStateBridge.Call call,ref NativeStateBridge.Plan plan)
        {
            if(call.Size!=40||call.Site is <1 or >2||call.Operation is <0 or >1||call.State==IntPtr.Zero||call.Stream==IntPtr.Zero)
                throw new InvalidDataException("Invalid native command bridge call.");
            NativeRegistryOrder.RequireCompatible();
            if(NativeRegroupSession.SessionFaulted)throw new InvalidDataException("Regroup session is faulted; Build record persistence stopped.");
            var op=new Operation{Call=call,Value=Wrap(Owner(call.Site),call.State),Stream=Wrap(call.Operation==1?Reader:Writer,call.Stream),Reading=call.Operation==1};
            lock(Operations)
            {
                if(Operations.Count>=32||Next==ulong.MaxValue)throw new InvalidDataException("Native command operation bound exceeded.");
                plan.Cookie=++Next;Operations.Add(plan.Cookie,op);
            }
            var scope=CurrentOriginal;
            if(!op.Reading&&scope is not null&&!scope.Entered&&scope.Site==call.Site&&scope.Version==call.Version&&scope.Value==call.State.ToInt64()&&scope.Stream==call.Stream.ToInt64())
            {scope.Entered=true;op.Bypass=true;return 0;}
            if(op.Reading){Forget(op.Value);return 0;}
            // Stage even the native base bytes. Missing custom payload or failed
            // suffix validation must not leave a successful-looking base record.
            if(Exchange(op.Value))
            {if(call.Version!=NativeStateFrame.NativeVersion)throw new InvalidDataException("Exchange serializer version mismatch.");_=NativeExchangeCommands.Bytes(op.Value);}
            if(MimicSmuggleActions.HasPayload(op.Value)&&!MimicSmuggleActions.IsSmuggle(op.Value))throw new InvalidDataException("Bound Smuggle changed to an ordinary carrier.");
            op.Private=new Buffer();plan.Replacement=new IntPtr(Native.Pointer(op.Private.W));return 1;
        }
        static Operation Find(NativeStateBridge.Call call,NativeStateBridge.Plan plan)
        {
            lock(Operations)
            {
                if(!Operations.TryGetValue(plan.Cookie,out var op)||op.Call.Size!=call.Size||op.Call.Site!=call.Site||op.Call.Operation!=call.Operation||
                    op.Call.Version!=call.Version||op.Call.State!=call.State||op.Call.Stream!=call.Stream||op.Call.MethodInfo!=call.MethodInfo)
                    throw new InvalidDataException("Native command operation identity mismatch.");return op;
            }
        }
        internal static void Finish(NativeStateBridge.Call call,NativeStateBridge.Plan plan)
        {
            var op=Find(call,plan);if(op.Bypass)return;
            if(op.Reading)
            {
                if(Exchange(op.Value))
                {
                    if(call.Version!=NativeStateFrame.NativeVersion)throw new InvalidDataException("Exchange reader version mismatch.");
                    var bytes=NativeStateProbe.ManagedBytes(Native.Invoke(Native.M(Reader,"ReadBytes","System.Int32"),op.Stream,NativeExchangeIntentCodec.Size)!);
                    NativeExchangeCommands.Set(op.Value,NativeExchangeIntentCodec.Decode(bytes));
                }
                else MimicSmuggleActions.ReadPayload(op.Value,op.Stream,call.Version);
            }
            else
            {
                if(op.Private is null)throw new InvalidDataException("Missing private command writer.");
                if(Exchange(op.Value))op.Private.Bytes(NativeExchangeCommands.Bytes(op.Value));
                else
                {
                    if(MimicSmuggleActions.HasPayload(op.Value)&&!MimicSmuggleActions.IsSmuggle(op.Value))throw new InvalidDataException("Bound Smuggle changed to an ordinary carrier.");
                    MimicSmuggleActions.WritePayload(op.Value,op.Private.W,call.Version);
                }
                byte[] bytes=op.Private.Snapshot();if(bytes.Length>4096)throw new InvalidDataException("Native Build record exceeded its audited bound.");
                var native=NativeStateProbe.NativeBytes(bytes);Native.Invoke(Native.M(Writer,"Write",native.GetType().FullName!),op.Stream,native);
            }
        }
        internal static void Cleanup(NativeStateBridge.Call call,NativeStateBridge.Plan plan,int outcome)
        {
            if(outcome!=0&&NativeRegroupSession.Enabled)NativeRegroupSession.Fault("Native Build record transport failed.");
            if(plan.Cookie==0)return;var op=Find(call,plan);lock(Operations)Operations.Remove(plan.Cookie);
            try{op.Private?.Dispose();}catch{if(op.Reading&&!op.Bypass)Forget(op.Value);throw;}
            finally{if(outcome!=0&&op.Reading&&!op.Bypass)Forget(op.Value);}
        }
    }
}
