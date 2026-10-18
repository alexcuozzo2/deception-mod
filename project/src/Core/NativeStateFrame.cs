using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;

namespace Deception.Core
{
    // A transport frame, not a gameplay schema. The native adapter must also
    // validate its extension's meaning before publishing it to a GameState.
    public static class NativeStateFrame
    {
        public const uint Magic=0xD0435044;
        public const ushort Schema=1;
        public const int NativeVersion=123;
        public const int HeaderBytes=60;
        public const int MaxNativeBytes=16*1024*1024;
        public const int MaxExtensionBytes=1024*1024;
        const int SignedHeaderBytes=28;

        public sealed record Decoded(long Revision,byte[] Native,byte[] Extension);

        static void Require(bool condition,string reason)
        {if(!condition)throw new InvalidDataException(reason);}

        public static int LengthFromHeader(ReadOnlySpan<byte> header,int serializerVersion)
        {
            Require(header.Length>=HeaderBytes,"Truncated Deception state header.");
            Require(serializerVersion==NativeVersion,"Unsupported Deception native serializer version.");
            Require(BinaryPrimitives.ReadUInt32LittleEndian(header)==Magic,"Invalid Deception state marker.");
            Require(BinaryPrimitives.ReadUInt16LittleEndian(header[4..])==Schema,"Unknown Deception state frame schema.");
            Require(BinaryPrimitives.ReadUInt16LittleEndian(header[6..])==0,"Unknown Deception state frame flags.");
            Require(BinaryPrimitives.ReadInt32LittleEndian(header[8..])==serializerVersion,"Deception state serializer version mismatch.");
            Require(BinaryPrimitives.ReadInt64LittleEndian(header[12..])>=0,"Negative Deception state revision.");
            int nativeLength=BinaryPrimitives.ReadInt32LittleEndian(header[20..]);
            int extensionLength=BinaryPrimitives.ReadInt32LittleEndian(header[24..]);
            Require(nativeLength>=4&&nativeLength<=MaxNativeBytes,"Invalid Deception native section length.");
            Require(extensionLength>0&&extensionLength<=MaxExtensionBytes,"Invalid Deception extension section length.");
            return checked(HeaderBytes+nativeLength+extensionLength);
        }

        public static byte[] Encode(byte[] native,byte[] extension,long revision,int serializerVersion)
        {
            ArgumentNullException.ThrowIfNull(native);ArgumentNullException.ThrowIfNull(extension);
            Require(native.Length>=4&&native.Length<=MaxNativeBytes,"Invalid native state size.");
            Require(extension.Length>0&&extension.Length<=MaxExtensionBytes,"Empty extensions must retain the native format.");
            Require(serializerVersion==NativeVersion&&BinaryPrimitives.ReadInt32LittleEndian(native)==NativeVersion,
                "This frame supports native state version 123 only.");
            Require(revision>=0,"Negative Deception state revision.");
            var frame=new byte[checked(HeaderBytes+native.Length+extension.Length)];
            var span=frame.AsSpan();
            BinaryPrimitives.WriteUInt32LittleEndian(span,Magic);
            BinaryPrimitives.WriteUInt16LittleEndian(span[4..],Schema);
            BinaryPrimitives.WriteInt32LittleEndian(span[8..],serializerVersion);
            BinaryPrimitives.WriteInt64LittleEndian(span[12..],revision);
            BinaryPrimitives.WriteInt32LittleEndian(span[20..],native.Length);
            BinaryPrimitives.WriteInt32LittleEndian(span[24..],extension.Length);
            native.CopyTo(span[HeaderBytes..]);extension.CopyTo(span[(HeaderBytes+native.Length)..]);
            Digest(frame).CopyTo(span[SignedHeaderBytes..HeaderBytes]);
            return frame;
        }

        public static Decoded Decode(byte[] frame,int serializerVersion)
        {
            ArgumentNullException.ThrowIfNull(frame);
            int length=LengthFromHeader(frame,serializerVersion);
            Require(frame.Length==length,"Truncated or trailing Deception state frame bytes.");
            Require(CryptographicOperations.FixedTimeEquals(Digest(frame),frame.AsSpan(SignedHeaderBytes,32)),
                "Deception state checksum mismatch.");
            int nativeLength=BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(20));
            var native=frame.AsSpan(HeaderBytes,nativeLength).ToArray();
            Require(BinaryPrimitives.ReadInt32LittleEndian(native)==NativeVersion,"Unsupported embedded Deception native state version.");
            return new(BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(12)),native,
                frame.AsSpan(HeaderBytes+nativeLength).ToArray());
        }

        static byte[] Digest(byte[] frame)
        {
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(frame,0,SignedHeaderBytes);
            hash.AppendData(frame,HeaderBytes,frame.Length-HeaderBytes);
            return hash.GetHashAndReset();
        }
    }
}
