using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;

namespace Deception.Core
{
    // Draft format for one explicitly requested PRIVATE Train command record.
    // This is not an auto-detecting native reader or a GameState schema change.
    internal static class NativeTrainCommandFrame
    {
        internal const uint Magic=0xD0435443;
        internal const ushort Schema=1,HeaderBytes=72,Capability=1,CommandKind=1,TrainDiscriminator=5;
        internal const int Version=123,MaxBodyBytes=4098,MaxFrameBytes=HeaderBytes+MaxBodyBytes;
        internal sealed class Decoded
        {
            readonly byte[] Body;
            internal Guid Epoch {get;}
            internal Decoded(Guid epoch,byte[] body){Epoch=epoch;Body=(byte[])body.Clone();}
            internal byte[] CopyBody()=>(byte[])Body.Clone();
        }
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Private Train command frame: "+why);}
        static void BodyValid(ReadOnlySpan<byte> body)
        {Require(body.Length>=2&&body.Length<=MaxBodyBytes,"Body length is outside its bound.");Require(BinaryPrimitives.ReadUInt16LittleEndian(body)==TrainDiscriminator,"Only a native Train command discriminator is supported.");}
        static byte[] Digest(ReadOnlySpan<byte> header,ReadOnlySpan<byte> body)
        {
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(header);hash.AppendData(body);return hash.GetHashAndReset();
        }
        internal static byte[] Encode(Guid epoch,byte[] body)
        {
            Require(epoch!=Guid.Empty&&body is not null,"Missing epoch or body.");
            BodyValid(body!);byte[] owned=(byte[])body!.Clone();BodyValid(owned);
            var bytes=new byte[checked(HeaderBytes+owned.Length)];var h=bytes.AsSpan();
            BinaryPrimitives.WriteUInt32LittleEndian(h,Magic);
            BinaryPrimitives.WriteUInt16LittleEndian(h[4..],Schema);
            BinaryPrimitives.WriteUInt16LittleEndian(h[6..],HeaderBytes);
            BinaryPrimitives.WriteInt32LittleEndian(h[8..],Version);
            BinaryPrimitives.WriteUInt16LittleEndian(h[12..],Capability);
            BinaryPrimitives.WriteUInt16LittleEndian(h[14..],CommandKind);
            BinaryPrimitives.WriteInt32LittleEndian(h[16..],1);
            BinaryPrimitives.WriteInt32LittleEndian(h[20..],owned.Length);
            // Same deterministic Guid.ToByteArray representation as the private
            // intent codec. No native Guid layout is read or guessed.
            epoch.ToByteArray().CopyTo(bytes,24);owned.CopyTo(bytes,HeaderBytes);
            Digest(h[..40],owned).CopyTo(bytes,40);return bytes;
        }
        internal static Decoded Decode(byte[] input)
        {
            Require(input is not null&&input.Length>=HeaderBytes&&input.Length<=MaxFrameBytes,"Truncated or oversized frame.");
            byte[] owned=(byte[])input!.Clone();var h=owned.AsSpan();
            Require(BinaryPrimitives.ReadUInt32LittleEndian(h)==Magic&&BinaryPrimitives.ReadUInt16LittleEndian(h[4..])==Schema&&
                BinaryPrimitives.ReadUInt16LittleEndian(h[6..])==HeaderBytes,"Unknown explicit frame header.");
            Require(BinaryPrimitives.ReadInt32LittleEndian(h[8..])==Version&&BinaryPrimitives.ReadUInt16LittleEndian(h[12..])==Capability&&
                BinaryPrimitives.ReadUInt16LittleEndian(h[14..])==CommandKind&&BinaryPrimitives.ReadInt32LittleEndian(h[16..])==1,"Unsupported version, capability, kind or count.");
            int length=BinaryPrimitives.ReadInt32LittleEndian(h[20..]);
            Require(length>=2&&length<=MaxBodyBytes&&length==owned.Length-HeaderBytes,"Section length or trailing bytes differ.");
            var epoch=new Guid(h.Slice(24,16));Require(epoch!=Guid.Empty,"Empty epoch.");
            var body=h.Slice(HeaderBytes,length);BodyValid(body);
            Require(CryptographicOperations.FixedTimeEquals(h.Slice(40,32),Digest(h[..40],body)),"Integrity mismatch.");
            return new(epoch,body.ToArray());
        }
    }
}
