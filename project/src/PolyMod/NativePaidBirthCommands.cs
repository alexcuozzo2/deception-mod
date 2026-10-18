using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // DRAFT: attachment/leaf-record mechanics only. No hooks, stream discovery,
    // client/state mutation, native execution, payment, or ledger publication.
    // A caller must prove the enclosing wire format before selecting DcpbV1.
    // It is NOT inferred from a Mimic Type, an existing ledger, or an env flag.
    internal static class NativePaidBirthCommands
    {
        internal enum Kind { Command, Action }
        internal enum Wire { LegacyNative, DcpbV1 }
        const int MaxBindings=8192;
        static readonly object Gate=new();
        static readonly Dictionary<(Kind,long),Binding> Bindings=new();
        sealed record Binding(NativeLedgerOwnership.WeakIdentity Identity,byte[] Bytes);
        static void Require(bool ok,string reason)
        {if(!ok)throw new InvalidDataException("Paid-birth record: "+reason);}
        static string Owner(Kind kind)=>kind switch
        {Kind.Command=>"TrainCommand",Kind.Action=>"TrainAction",_=>throw new InvalidDataException("Unknown Train record kind.")};
        static object Typed(object value,Kind kind)=>Native.As(value,Owner(kind));
        static string Rule(object value)=>Native.EnumName(Native.Get(value,"Type")!);
        static bool Candidate(object value)=>Rule(value) is NativePaidBirthIntentCodec.Warrior or NativePaidBirthIntentCodec.Rider;
        static void Mode(Wire wire,int version)
        {
            Require(wire is Wire.LegacyNative or Wire.DcpbV1,"Unknown enclosing Train wire format.");
            if(wire==Wire.DcpbV1)Require(version==NativeStateFrame.NativeVersion,"DCPB requires native serializer123.");
        }
        static (Kind,long) Key(object value,Kind kind)=>(kind,Native.Pointer(Typed(value,kind)));
        static Binding? Find(object value,Kind kind)
        {
            var key=Key(value,kind);
            if(!Bindings.TryGetValue(key,out var binding))return null;
            if(binding.Identity.Matches(value))return binding;
            Bindings.Remove(key);return null; // Dead native target, never pointer reconstitution.
        }
        static void Prune()
        {
            foreach(var key in Bindings.Where(p=>p.Value.Identity.Target() is null).Select(p=>p.Key).ToArray())Bindings.Remove(key);
        }
        internal static NativePaidBirthIntent? Get(object value,Kind kind)
        {
            lock(Gate){var binding=Find(value,kind);return binding is null?null:NativePaidBirthIntentCodec.Decode((byte[])binding.Bytes.Clone());}
        }
        internal static void Forget(object value,Kind kind)
        {NativeTrainStreamContext.BeforeCarrierMutation(value,kind);lock(Gate){if(Find(value,kind) is not null)Bindings.Remove(Key(value,kind));}}
        static void ForgetExact(object value,Kind kind,Binding? expected)
        {if(expected is not null){NativeTrainStreamContext.BeforeCarrierMutation(value,kind);lock(Gate){if(ReferenceEquals(Find(value,kind),expected))Bindings.Remove(Key(value,kind));}}}
        internal static void ValidateBase(object value,Kind kind,NativePaidBirthIntent intent)
        {
            value=Typed(value,kind);NativePaidBirthIntentCodec.Validate(intent);
            Require(Rule(value)==intent.NativeRuleId&&Convert.ToByte(Native.Get(value,"PlayerId"))==intent.Actor,
                "Native rule/player differs from the canonical intent.");
            object at=Native.Get(value,"Coordinates")!;
            Require(Native.Int(Native.Get(at,"X"))==intent.CityX&&Native.Int(Native.Get(at,"Y"))==intent.CityY,
                "Native training coordinates differ from the canonical intent.");
            if(kind==Kind.Action)
                // A saved carrier retains its recorded price. Fresh purchase
                // admission separately requires the current catalog price.
                Require(Native.Int(Native.Get(value,"Cost"))==intent.ExpectedPrice&&
                    !(bool)Native.Get(value,"trainActionAborted")!,"Occupied/retry/aborted or wrong-price action is unsupported.");
            // LookCoordinates is real native output and remains unchanged. It is
            // neither payload storage nor proof of payment/helper completion.
        }
        static Binding Attach(object value,Kind kind,NativePaidBirthIntent intent,bool requireAbsent=false)
        {
            NativeTrainStreamContext.BeforeCarrierMutation(value,kind);
            value=Typed(value,kind);ValidateBase(value,kind,intent);
            byte[] bytes=NativePaidBirthIntentCodec.Encode(intent);
            lock(Gate)
            {
                var old=Find(value,kind);
                if(old is not null)
                {
                    Require(!requireAbsent,"Another binding appeared inside the exclusive Train read.");
                    Require(old.Bytes.SequenceEqual(bytes),"Conflicting intent already belongs to this native record.");return old;
                }
                Prune();Require(Bindings.Count<MaxBindings,"Native attachment capacity reached.");
                // Retain the weak tagged identity even after Forget; a retired paid
                // record cannot silently become an unrelated legacy receiver.
                NativeTrainStreamContext.RememberTaggedCarrier(value,kind);
                var binding=new Binding(new(value),bytes);Bindings.Add(Key(value,kind),binding);return binding;
            }
        }
        internal static void Set(object value,Kind kind,NativePaidBirthIntent intent)=>Attach(value,kind,intent);
        internal static void TransferToAction(object command,object action)
        {
            var intent=Get(command,Kind.Command)??throw new InvalidDataException("Paid TrainCommand has no complete intent.");
            ValidateBase(command,Kind.Command,intent);ValidateBase(action,Kind.Action,intent);Set(action,Kind.Action,intent);
            // Caller must first prove this is the one newly appended action from
            // the exact command/state scope. This method cannot establish that.
        }

        // In the explicitly declared v1 format every supported custom Train has
        // one tag: 0=historical/unbound, 1=complete canonical DCPB payload. Genuine
        // native Train records and all legacy-format records have no tag. Tag0
        // preserves history; it NEVER authorizes a paid birth or Known3 evidence.
        internal static byte[] EncodeExtension(bool candidate,Wire wire,int version,byte[]? payload)
        {
            Mode(wire,version);
            if(payload is not null)_=NativePaidBirthIntentCodec.Decode(payload);
            Require(payload is null||candidate,"A bound paid record was retagged as an ordinary native type.");
            if(wire==Wire.LegacyNative)
            {Require(payload is null,"Cannot erase a paid intent into legacy Train bytes.");return Array.Empty<byte>();}
            if(!candidate)return Array.Empty<byte>();
            return payload is null?new byte[]{0}:new byte[]{1}.Concat(payload).ToArray();
        }
        internal static NativePaidBirthIntent? DecodeExtension(bool candidate,Wire wire,int version,Func<int,byte[]> readExactly)
        {
            Mode(wire,version);ArgumentNullException.ThrowIfNull(readExactly);
            if(wire==Wire.LegacyNative||!candidate)return null;
            byte[] Read(int count)
            {byte[] bytes=readExactly(count);Require(bytes is not null&&bytes.Length==count,"Truncated Train extension.");return bytes!;}
            byte tag=Read(1)[0];if(tag==0)return null;Require(tag==1,"Unknown Train intent tag.");
            byte[] header=Read(NativePaidBirthIntentCodec.HeaderSize);
            Require(BinaryPrimitives.ReadUInt32LittleEndian(header)==NativePaidBirthIntentCodec.Magic&&
                BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4))==NativePaidBirthIntentCodec.Schema&&
                BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6))==0&&
                BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(8))==NativeStateFrame.NativeVersion,
                "Unknown DCPB header or native version.");
            int tail=BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(12));
            Require(tail>=NativePaidBirthIntentCodec.FixedSize-NativePaidBirthIntentCodec.HeaderSize&&
                tail<=NativePaidBirthIntentCodec.FixedSize+NativeIdentityLedgerCodec.MaxText-NativePaidBirthIntentCodec.HeaderSize,
                "DCPB length is outside the bounded canonical record.");
            return NativePaidBirthIntentCodec.Decode(header.Concat(Read(tail)).ToArray());
        }

        // Bridge Prepare obtains this plan BEFORE calling original into a private
        // staging writer. Finish revalidates it BEFORE appending/publishing bytes.
        internal sealed class WriteRecord
        {
            readonly object Value;readonly Kind RecordKind;readonly Wire Format;readonly int Version;
            readonly byte[] Extension;readonly BaseImage Before;
            internal WriteRecord(object value,Kind kind,Wire wire,int version)
            {
                Value=Typed(value,kind);RecordKind=kind;Format=wire;Version=version;
                var intent=Get(Value,kind);if(intent is not null)ValidateBase(Value,kind,intent);
                Extension=EncodeExtension(Candidate(Value),wire,version,intent is null?null:NativePaidBirthIntentCodec.Encode(intent));
                Before=Image(Value,kind);
            }
            internal byte[] Finish()
            {
                Require(Before==Image(Value,RecordKind),"Native Train fields changed during serialization.");
                var intent=Get(Value,RecordKind);if(intent is not null)ValidateBase(Value,RecordKind,intent);
                byte[] current=EncodeExtension(Candidate(Value),Format,Version,intent is null?null:NativePaidBirthIntentCodec.Encode(intent));
                Require(Extension.SequenceEqual(current),"Train binding changed during serialization.");return (byte[])Extension.Clone();
            }
        }
        sealed record BaseImage(string Rule,byte Actor,int X,int Y,int Cost,int LookX,int LookY,bool Aborted);
        static BaseImage Image(object value,Kind kind)
        {
            object at=Native.Get(value,"Coordinates")!;object? look=kind==Kind.Action?Native.Get(value,"LookCoordinates"):null;
            return new(Rule(value),Convert.ToByte(Native.Get(value,"PlayerId")),Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")),
                kind==Kind.Action?Native.Int(Native.Get(value,"Cost")):0,look is null?0:Native.Int(Native.Get(look,"X")),
                look is null?0:Native.Int(Native.Get(look,"Y")),kind==Kind.Action&&(bool)Native.Get(value,"trainActionAborted")!);
        }

        // Bridge Prepare creates this BEFORE original reads even the native base.
        // The outer state/history decoder journals successful leaf operations;
        // failed outer decode must call RollbackPublished on every journal entry.
        internal sealed class ReadRecord:IDisposable
        {
            readonly object Value;readonly Kind RecordKind;readonly Wire Format;readonly int Version;
            readonly Action<object,Kind> RequireExclusiveRead;
            Binding? Published;bool Started,Finished,Accepted,Disposed;
            internal ReadRecord(object value,Kind kind,Wire wire,int version,Action<object,Kind> requireExclusiveRead)
            {
                Value=Typed(value,kind);RecordKind=kind;Format=wire;Version=version;
                // Mandatory outer authority: exact private/unpublished receiver
                // and an exclusive carrier token through outer cleanup. The
                // actual ownership policy is deliberately not implemented here.
                RequireExclusiveRead=requireExclusiveRead??throw new ArgumentNullException(nameof(requireExclusiveRead));
                RequireExclusiveRead(Value,kind); // BEFORE any binding retirement.
                Forget(Value,kind);Mode(wire,version); // Also retire old binding on a bad-version read.
            }
            internal void Finish(Func<int,byte[]> readExactly)
            {
                Require(!Started&&!Disposed,"Train read operation reused.");Started=true;
                RequireExclusiveRead(Value,RecordKind);
                var intent=DecodeExtension(Candidate(Value),Format,Version,readExactly);
                if(intent is not null)Published=Attach(Value,RecordKind,intent,requireAbsent:true);
                Finished=true;
            }
            internal void Accept()
            {Require(Finished&&!Disposed,"Train read was not completed.");Accepted=true;}
            internal void RollbackPublished()=>ForgetExact(Value,RecordKind,Published);
            public void Dispose()
            {if(Disposed)return;Disposed=true;if(!Accepted)RollbackPublished();}
        }
    }
}
