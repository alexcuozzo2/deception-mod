using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Codec/attachment slice. Native execution remains unavailable; a complete
    // payload cannot itself grant authority or bypass the gameplay transaction.
    internal static partial class NativeExchangeCommands
    {
        // Native history owns the record lifetime. Retain only its native weak
        // identity so discarded read/copy records can be collected; live native
        // records keep their exact journal token and remain serializable.
        sealed class Binding
        {
            internal readonly NativeLedgerOwnership.WeakIdentity Identity;
            internal readonly byte[] Bytes;
            public Binding(object value,byte[] bytes){Identity=new(value);Bytes=bytes;}
        }
        static readonly Dictionary<long,Binding> Payloads=new();
        static uint Publications;
        static Binding? Find(object value)
        {
            long key=Native.Pointer(value);if(!Payloads.TryGetValue(key,out var binding))return null;
            if(binding.Identity.Matches(value))return binding;
            Payloads.Remove(key);return null;
        }
        internal static void PruneDead()
        {
            foreach(long key in Payloads.Where(p=>p.Value.Identity.Target() is null).Select(p=>p.Key).ToArray())Payloads.Remove(key);
        }
        static void BeforePublication()
        {if((++Publications&127)==0||Payloads.Count>=8192)PruneDead();}
        internal static bool IsExchange(object value)
        {
            foreach(string type in new[]{"BuildCommand","BuildAction"})
                try{if(Native.EnumName(Native.Get(Native.As(value,type),"Type")!).StartsWith(NativeExchangeCatalog.Prefix,StringComparison.Ordinal))return true;}catch{}
            return false;
        }
        internal static void ValidateBase(object value,NativeExchangeIntent intent)
        {
            NativeExchangeIntentCodec.Validate(intent);
            if(Native.EnumName(Native.Get(value,"Type")!)!=NativeExchangeCatalog.Id(intent.Kind)||Convert.ToByte(Native.Get(value,"PlayerId"))!=intent.Actor)
                throw new InvalidDataException("Exchange native carrier/player differs from its complete intent.");
            object at=Native.Get(value,"Coordinates")!;
            if(Native.Int(Native.Get(at,"X"))!=intent.Initiator.X||Native.Int(Native.Get(at,"Y"))!=intent.Initiator.Y)
                throw new InvalidDataException("Exchange native source coordinates differ from its complete intent.");
        }
        internal static void Set(object value,NativeExchangeIntent intent)
        {
            NativeBuildRecordJournalDraft.BeforeMutation(value);ValidateBase(value,intent);
            long key=Native.Pointer(value);_=Find(value);BeforePublication();
            if(!Payloads.ContainsKey(key)&&Payloads.Count>=8192)throw new InvalidDataException("Exchange attachment bound exceeded.");
            Payloads[key]=new(value,NativeExchangeIntentCodec.Encode(intent));
        }
        internal static NativeExchangeIntent? Get(object value)
        {
            var binding=Find(value);if(binding is null)return null;
            return NativeExchangeIntentCodec.Decode((byte[])binding.Bytes.Clone());
        }
        internal static void Forget(object value){NativeBuildRecordJournalDraft.BeforeMutation(value);Payloads.Remove(Native.Pointer(value));}
        internal static (object Token,byte[] Bytes)? CapturePrivateJournal(object value)
        {
            var binding=Find(value);if(binding is null)return null;
            return(binding,(byte[])binding.Bytes.Clone());
        }
        internal static object PublishPrivateJournal(object value,NativeExchangeIntent intent)
        {
            NativeBuildRecordJournalDraft.BeforeMutation(value);ValidateBase(value,intent);
            long key=Native.Pointer(value);_=Find(value);BeforePublication();
            if(Payloads.ContainsKey(key)||Payloads.Count>=8192)throw new InvalidDataException("Exchange private publication requires absent binding/capacity.");
            var binding=new Binding(value,NativeExchangeIntentCodec.Encode(intent));Payloads.Add(key,binding);return binding;
        }
        internal static void ForgetPrivateJournal(object value,object token)
        {
            NativeBuildRecordJournalDraft.BeforeMutation(value);long key=Native.Pointer(value);
            var binding=Find(value);if(binding is null)return;
            if(!ReferenceEquals(binding,token))throw new InvalidDataException("Exchange exact rollback token changed.");
            Payloads.Remove(key);
        }
        internal static byte[] Bytes(object value)
        {
            var intent=Get(value)??throw new InvalidDataException("Exchange has no complete intent.");ValidateBase(value,intent);return NativeExchangeIntentCodec.Encode(intent);
        }
        internal static void Install(Harmony harmony)
        {
            Native.Hook(harmony,Native.M("BuildCommand","IsValid","GameState","System.String&"),typeof(NativeExchangeCommands),nameof(GuardValidity));
            if(!NativeExecutionTrigger.Requested)
            Native.Hook(harmony,Native.M("BuildCommand","Execute","GameState"),typeof(NativeExchangeCommands),nameof(GuardExecution));
            foreach(string name in new[]{"Execute","ExecuteDefault","ExecuteV105"})
                if(name!="Execute"||!NativeExecutionTrigger.Requested)
                Native.Hook(harmony,Native.M("BuildAction",name,"GameState"),typeof(NativeExchangeCommands),nameof(GuardExecution));
        }
        public static bool GuardValidity(object __instance,ref string __1,ref bool __result)
        {if(!IsExchange(__instance))return true;__1="Exchange is not available in this build.";__result=false;return false;}
        public static bool GuardExecution(object __instance)
        {if(!IsExchange(__instance))return true;Entry.Note("exchange-unavailable","Rejected an inert exchange carrier before native Build execution.");return false;}
    }
}
