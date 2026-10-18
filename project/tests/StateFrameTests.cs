using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        // Transport-only tests. These opaque native bytes are not a complete
        // native GameState, and opaque extension bytes are not a mechanics schema.
        static void StateFrameTests()
        {
            const int Version=123,HeaderSize=60,SignedHeaderSize=28;
            byte[] NativeBytes(int length=16)
            {
                var bytes=new byte[length];
                if(length>=4)BinaryPrimitives.WriteInt32LittleEndian(bytes,Version);
                for(int i=4;i<length;i++)bytes[i]=(byte)(i*17);
                return bytes;
            }
            byte[] Extension()=>new byte[]{0,255,0x80,0x44,0,0x7f};
            byte[] Good()=>NativeStateFrame.Encode(NativeBytes(),Extension(),9,Version);
            void RejectData(Action action,string? reason=null)
            {
                bool rejected=false;
                try{action();}
                catch(InvalidDataException e)
                {rejected=true;if(reason is not null)Assert(e.Message.Contains(reason,StringComparison.Ordinal),e.Message);}
                Assert(rejected,"Expected InvalidDataException for an invalid transport frame");
            }
            void RejectDecode(byte[] frame,int version=Version,string? reason=null)
            {
                var before=frame.ToArray();
                var original=new NativeStateFrame.Decoded(71,new byte[]{4},new byte[]{5});
                var published=original;
                RejectData(()=>published=NativeStateFrame.Decode(frame,version),reason);
                Assert(ReferenceEquals(published,original),"Rejected decode returned data for publication");
                Assert(frame.SequenceEqual(before),"Rejected decode mutated its input buffer");
            }
            // Independently sign adversarial wire records so semantic rejection
            // cannot pass merely because their old checksum stopped matching.
            void SignWire(byte[] frame)
            {
                var authenticated=frame.Take(SignedHeaderSize).Concat(frame.Skip(HeaderSize)).ToArray();
                SHA256.HashData(authenticated).CopyTo(frame,SignedHeaderSize);
            }
            byte[] Changed(Action<byte[]> change,bool sign=false)
            {
                var frame=Good();change(frame);if(sign)SignWire(frame);return frame;
            }

            Test("M2-010-state-frame-preserves-opaque-sections-and-revisions",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                Assert(NativeStateFrame.Magic==0xD0435044u&&unchecked((int)NativeStateFrame.Magic)<0,
                    "Private frame marker must remain the reserved negative value");
                Assert(NativeStateFrame.NativeVersion==Version&&NativeStateFrame.HeaderBytes==HeaderSize);
                var native=NativeBytes();var extension=Extension();
                var nativeBefore=native.ToArray();var extensionBefore=extension.ToArray();
                foreach(long revision in new[]{0L,1L,long.MaxValue})
                {
                    var frame=NativeStateFrame.Encode(native,extension,revision,Version);
                    Assert(frame.Length==HeaderSize+native.Length+extension.Length);
                    Assert(BinaryPrimitives.ReadUInt32LittleEndian(frame)==0xD0435044u);
                    Assert(BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(4))==1&&
                        BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(6))==0);
                    Assert(BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(8))==Version&&
                        BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(12))==revision);
                    Assert(BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(20))==native.Length&&
                        BinaryPrimitives.ReadInt32LittleEndian(frame.AsSpan(24))==extension.Length);
                    Assert(frame.AsSpan(HeaderSize,native.Length).SequenceEqual(native)&&
                        frame.AsSpan(HeaderSize+native.Length).SequenceEqual(extension),"Opaque sections changed wire ordering/content");
                    var first=NativeStateFrame.Decode(frame,Version);var second=NativeStateFrame.Decode(frame,Version);
                    Assert(first.Revision==revision&&first.Native.SequenceEqual(native)&&first.Extension.SequenceEqual(extension));
                    Assert(!ReferenceEquals(first.Native,second.Native)&&!ReferenceEquals(first.Extension,second.Extension));
                    var unchanged=frame.ToArray();first.Native[5]^=0xff;first.Extension[0]^=0xff;
                    Assert(frame.SequenceEqual(unchanged)&&second.Native.SequenceEqual(native)&&second.Extension.SequenceEqual(extension),
                        "Decoded sections alias another decode or the frame");
                    Assert(NativeStateFrame.Encode(native,extension,revision,Version).SequenceEqual(frame),"Encoding is not deterministic");
                }
                var oldFrame=NativeStateFrame.Encode(native,extension,4,Version);
                var newFrame=NativeStateFrame.Encode(native,extension,5,Version);
                Assert(!oldFrame.SequenceEqual(newFrame)&&NativeStateFrame.Decode(oldFrame,Version).Revision==4&&
                    NativeStateFrame.Decode(newFrame,Version).Revision==5,"Identical native bytes collapsed distinct revisions");
                native[5]^=0xff;extension[0]^=0xff;
                Assert(NativeStateFrame.Decode(oldFrame,Version).Native.SequenceEqual(nativeBefore)&&
                    NativeStateFrame.Decode(oldFrame,Version).Extension.SequenceEqual(extensionBefore),"Encoding retained caller-owned arrays");
            });

            Test("M2-011-state-frame-checksum-authenticates-header-and-sections",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                var valid=Good();
                var independentlySigned=valid.ToArray();SignWire(independentlySigned);
                Assert(independentlySigned.SequenceEqual(valid),"Checksum does not cover the specified wire header plus both sections");
                foreach(int offset in new[]{12,HeaderSize+5,valid.Length-1,SignedHeaderSize,SignedHeaderSize+31})
                {
                    var corrupt=valid.ToArray();corrupt[offset]^=1;
                    RejectDecode(corrupt,reason:"checksum");
                }
                // Repartition the exact same total payload into two valid-size
                // sections: structural length checks alone cannot catch this.
                var boundaryChange=Changed(frame=> {
                    BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(20),17);
                    BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(24),5);
                });
                Assert(NativeStateFrame.LengthFromHeader(boundaryChange,Version)==valid.Length);
                RejectDecode(boundaryChange,reason:"checksum");
                var signedRevision=Changed(frame=>BinaryPrimitives.WriteInt64LittleEndian(frame.AsSpan(12),10),true);
                Assert(NativeStateFrame.Decode(signedRevision,Version).Revision==10,
                    "A correctly signed, otherwise identical revision was not transported");
            });

            Test("M2-012-state-frame-invalid-input-never-publishes",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                var valid=Good();
                for(int length=0;length<valid.Length;length++)RejectDecode(valid[..length]);
                RejectDecode(valid.Concat(new byte[]{0x5a}).ToArray(),reason:"trailing");
                foreach(int version in new[]{-1,0,122,124,int.MaxValue})RejectDecode(valid,version);
                RejectDecode(Changed(frame=>BinaryPrimitives.WriteUInt32LittleEndian(frame,0x50435044),true),reason:"marker");
                foreach(ushort schema in new ushort[]{0,2,ushort.MaxValue})
                    RejectDecode(Changed(frame=>BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4),schema),true),reason:"schema");
                RejectDecode(Changed(frame=>BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6),1),true),reason:"flags");
                RejectDecode(Changed(frame=>BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(8),122),true),reason:"version");
                RejectDecode(Changed(frame=>BinaryPrimitives.WriteInt64LittleEndian(frame.AsSpan(12),-1),true),reason:"revision");
                // Valid framing and checksum do not authorize a different native
                // payload version. This must fail before returning Decoded data.
                foreach(int embedded in new[]{-1,0,122,124,int.MaxValue})
                    RejectDecode(Changed(frame=>BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(HeaderSize),embedded),true),reason:"embedded");
                Assert(NativeStateFrame.Decode(valid,Version).Revision==9,"A rejected decode contaminated a later valid decode");
            });

            Test("M2-013-empty-extension-remains-native-only",new[]{"DEC-171","DEC-179"},()=> {
                var native=NativeBytes();var untouched=native.ToArray();
                RejectData(()=>NativeStateFrame.Encode(native,Array.Empty<byte>(),0,Version),"Empty extensions");
                Assert(native.SequenceEqual(untouched),"Empty-extension rejection altered ordinary native bytes");
                var emptyFrame=Good()[..(HeaderSize+native.Length)];
                BinaryPrimitives.WriteInt32LittleEndian(emptyFrame.AsSpan(24),0);SignWire(emptyFrame);
                RejectDecode(emptyFrame,reason:"extension");
                RejectData(()=>NativeStateFrame.LengthFromHeader(emptyFrame,Version),"extension");
                // All-zero content is valid opaque data; only a zero-length
                // extension belongs to the adapter's native pass-through path.
                var zero=NativeStateFrame.Decode(NativeStateFrame.Encode(native,new byte[]{0},0,Version),Version);
                Assert(zero.Extension.SequenceEqual(new byte[]{0})&&zero.Native.SequenceEqual(native));
            });

            Test("M2-014-state-frame-section-length-bounds",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                var header=Good()[..HeaderSize];
                byte[] WithLengths(int nativeLength,int extensionLength)
                {
                    var changed=header.ToArray();
                    BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(20),nativeLength);
                    BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(24),extensionLength);
                    return changed;
                }
                Assert(NativeStateFrame.LengthFromHeader(WithLengths(4,1),Version)==HeaderSize+5);
                Assert(NativeStateFrame.LengthFromHeader(WithLengths(NativeStateFrame.MaxNativeBytes,NativeStateFrame.MaxExtensionBytes),Version)==
                    HeaderSize+NativeStateFrame.MaxNativeBytes+NativeStateFrame.MaxExtensionBytes);
                foreach(int length in new[]{int.MinValue,-1,0,1,3,NativeStateFrame.MaxNativeBytes+1,int.MaxValue})
                {
                    var invalid=WithLengths(length,1);
                    RejectData(()=>NativeStateFrame.LengthFromHeader(invalid,Version),"native");RejectDecode(invalid);
                }
                foreach(int length in new[]{int.MinValue,-1,0,NativeStateFrame.MaxExtensionBytes+1,int.MaxValue})
                {
                    var invalid=WithLengths(4,length);
                    RejectData(()=>NativeStateFrame.LengthFromHeader(invalid,Version),"extension");RejectDecode(invalid);
                }
                foreach(int length in new[]{0,1,3})RejectData(()=>NativeStateFrame.Encode(new byte[length],Extension(),0,Version),"native");
                RejectData(()=>NativeStateFrame.Encode(NativeBytes(),Extension(),-1,Version),"revision");
                foreach(int version in new[]{122,124})RejectData(()=>NativeStateFrame.Encode(NativeBytes(),Extension(),0,version),"version");
                var wrongNative=NativeBytes();BinaryPrimitives.WriteInt32LittleEndian(wrongNative,124);
                RejectData(()=>NativeStateFrame.Encode(wrongNative,Extension(),0,Version),"version");
                RejectData(()=>NativeStateFrame.Encode(new byte[NativeStateFrame.MaxNativeBytes+1],Extension(),0,Version),"native");
                RejectData(()=>NativeStateFrame.Encode(NativeBytes(),new byte[NativeStateFrame.MaxExtensionBytes+1],0,Version));
                var largestNative=new byte[NativeStateFrame.MaxNativeBytes];BinaryPrimitives.WriteInt32LittleEndian(largestNative,Version);
                var largestExtension=new byte[NativeStateFrame.MaxExtensionBytes];largestNative[^1]=0xa1;largestExtension[^1]=0xb2;
                var largest=NativeStateFrame.Encode(largestNative,largestExtension,long.MaxValue,Version);
                var decoded=NativeStateFrame.Decode(largest,Version);
                Assert(decoded.Native.SequenceEqual(largestNative)&&decoded.Extension.SequenceEqual(largestExtension)&&decoded.Revision==long.MaxValue);
                // Header inspection intentionally reports one record's length;
                // whole-record Decode independently rejects trailing bytes.
                var minimum=NativeStateFrame.Encode(NativeBytes(4),new byte[]{1},0,Version);
                Assert(NativeStateFrame.Decode(minimum,Version).Native.Length==4);
                Assert(NativeStateFrame.LengthFromHeader(minimum.Concat(new byte[]{7}).ToArray(),Version)==minimum.Length);
            });
        }
    }
}
