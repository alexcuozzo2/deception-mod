using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Deception.Core
{
    public enum NativeExchangeKind:byte { Regroup=0,Disguise=1,UpgradeScout=2,UpgradeRammer=3,UpgradeBomber=4,ShedDisguise=5,
        Subvert=6,Blackout=7,FeignHeal=8,FeignBoost=9,FeignSwarm=10,FeignRecover=11,Blackmail=12,LighthouseSecret=13 }
    public enum NativePartnerReference:byte { Existing=0,Allocate=1 }
    public sealed record NativeExchangeUnit(ulong LogicalId,uint NativeId,int X,int Y);
    public sealed record NativeExchangeIntent(Guid Epoch,ulong Sequence,long Revision,uint Turn,byte Actor,
        NativeExchangeKind Kind,bool Swap,NativePartnerReference PartnerReference,ulong NextLogicalId,
        NativeExchangeUnit Initiator,NativeExchangeUnit Partner,byte[] SourceAppearanceHash);

    // Complete choice, not a half-executed interaction. This codec owns no
    // native rules or UI state. Its digest detects stale intent; it is not an
    // authorization token. The adapter must revalidate the supplied GameState.
    public static class NativeExchangeIntentCodec
    {
        public const uint Magic=0x58504344; // DCPX
        public const ushort Schema=1;
        public const int Size=136,HeaderSize=16;
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        public static void Validate(NativeExchangeIntent value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Require(value.Epoch!=Guid.Empty&&value.Sequence>0&&value.Sequence<ulong.MaxValue&&value.Revision>=0&&value.Revision<long.MaxValue&&
                value.NextLogicalId>0&&value.NextLogicalId<ulong.MaxValue,"Invalid exchange lineage or exhausted allocator.");
            Require(Enum.IsDefined(typeof(NativeExchangeKind),value.Kind)&&Enum.IsDefined(typeof(NativePartnerReference),value.PartnerReference),"Unknown exchange kind or partner reference.");
            var a=value.Initiator;var b=value.Partner;
            if(value.Kind==NativeExchangeKind.LighthouseSecret)
            {
                Require(value.Actor is >0 and <255&&a is not null&&a==b&&a.LogicalId==0&&a.NativeId==0&&
                    a.X is >=0 and <4096&&a.Y is >=0 and <4096&&!value.Swap&&value.PartnerReference==NativePartnerReference.Existing,
                    "Invalid lighthouse location reference.");
                Require(value.SourceAppearanceHash is not null&&value.SourceAppearanceHash.Length==32,"Lighthouse reward needs an exact state digest.");return;
            }
            if(value.Kind==NativeExchangeKind.Blackmail)
            {
                Require(value.Actor is >0 and <255&&a is not null&&a==b&&
                    a.X is >=0 and <4096&&a.Y is >=0 and <4096&&!value.Swap&&
                    (a.LogicalId==0?a.NativeId==0&&value.PartnerReference==NativePartnerReference.Existing:
                        value.PartnerReference==NativePartnerReference.Existing?a.LogicalId<value.NextLogicalId:a.LogicalId==value.NextLogicalId),
                    "Invalid Blackmail target reference.");
                Require(value.SourceAppearanceHash is not null&&value.SourceAppearanceHash.Length==32,"Blackmail needs one exact state digest.");return;
            }
            bool upgrade=NativeExchangeCatalog.IsSingle(value.Kind);
            Require(a is not null&&b is not null&&a.LogicalId>0&&a.LogicalId<value.NextLogicalId&&b.LogicalId>0&&
                (value.Kind==NativeExchangeKind.Blackout?a.LogicalId==b.LogicalId&&a.NativeId==b.NativeId&&!value.Swap&&value.PartnerReference==NativePartnerReference.Existing:
                upgrade?a==b&&!value.Swap&&value.PartnerReference==NativePartnerReference.Existing:
                a.LogicalId!=b.LogicalId&&a.NativeId!=b.NativeId&&(a.X!=b.X||a.Y!=b.Y)),"Invalid ordered exchange identities.");
            Require(value.PartnerReference==NativePartnerReference.Existing?b.LogicalId<value.NextLogicalId:b.LogicalId==value.NextLogicalId,
                "Exchange partner allocation differs from its declared allocator.");
            Require(value.SourceAppearanceHash is not null&&value.SourceAppearanceHash.Length==32,"Exchange needs one exact appearance digest.");
        }
        public static byte[] Encode(NativeExchangeIntent value)
        {
            Validate(value);
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream,Encoding.UTF8,true);
            writer.Write(Magic);writer.Write(Schema);writer.Write((ushort)0);writer.Write(NativeStateFrame.NativeVersion);writer.Write(Size-HeaderSize);
            writer.Write(value.Epoch.ToByteArray());writer.Write(value.Sequence);writer.Write(value.Revision);writer.Write(value.Turn);
            writer.Write(value.Actor);writer.Write((byte)value.Kind);writer.Write(value.Swap);writer.Write((byte)value.PartnerReference);writer.Write(value.NextLogicalId);
            foreach(var unit in new[]{value.Initiator,value.Partner}){writer.Write(unit.LogicalId);writer.Write(unit.NativeId);writer.Write(unit.X);writer.Write(unit.Y);}
            writer.Write(value.SourceAppearanceHash);writer.Flush();Require(stream.Length==Size,"Exchange codec layout mismatch.");return stream.ToArray();
        }
        public static NativeExchangeIntent Decode(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);Require(bytes.Length==Size,"Exchange intent has a truncated or trailing payload.");
            using var stream=new MemoryStream(bytes,false);using var reader=new BinaryReader(stream,Encoding.UTF8,true);
            Require(reader.ReadUInt32()==Magic&&reader.ReadUInt16()==Schema&&reader.ReadUInt16()==0&&
                reader.ReadInt32()==NativeStateFrame.NativeVersion&&reader.ReadInt32()==Size-HeaderSize,"Unknown exchange header, native version or length.");
            Guid epoch=new Guid(reader.ReadBytes(16));ulong sequence=reader.ReadUInt64();long revision=reader.ReadInt64();uint turn=reader.ReadUInt32();byte actor=reader.ReadByte();
            var kind=(NativeExchangeKind)reader.ReadByte();byte swap=reader.ReadByte();Require(swap<=1,"Noncanonical exchange choice Boolean.");
            var partnerReference=(NativePartnerReference)reader.ReadByte();ulong next=reader.ReadUInt64();
            NativeExchangeUnit Unit()=>new(reader.ReadUInt64(),reader.ReadUInt32(),reader.ReadInt32(),reader.ReadInt32());
            var result=new NativeExchangeIntent(epoch,sequence,revision,turn,actor,kind,swap==1,partnerReference,next,Unit(),Unit(),reader.ReadBytes(32));
            Validate(result);Require(stream.Position==Size,"Exchange payload was not exactly consumed.");return result;
        }
        public static byte[] Fingerprint(NativeExchangeIntent value)=>DomainHash("DCPX.INTENT.1",Encode(value));
        static byte[] DomainHash(string domain,byte[] bytes)
        {var prefix=Encoding.ASCII.GetBytes(domain+"\0");return SHA256.HashData(prefix.Concat(bytes).ToArray());}
        public static Guid Operation(ulong sequence)
        {
            Require(sequence>0&&sequence<ulong.MaxValue,"Invalid operation sequence.");
            // Injective within the separately checked epoch. No hash collision,
            // runtime randomness, native command counter or pointer identity.
            byte[] bytes=Encoding.ASCII.GetBytes("DCPXOP01").Concat(new byte[8]).ToArray();BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8),sequence);return new Guid(bytes);
        }
        public static byte[] AppearanceFingerprint(LedgerAppearance appearance)
        {
            // Reuse the schema's exact validated field encoding. All surrounding
            // fields are constants; none come from actual unit health or flags.
            var ledger=new NativeIdentityLedger(new Guid("7324c8ad-99ec-4795-9ddb-1038a2308341"),2,1,
                new[]{new LedgerUnit(1,0,true,LedgerActualKind.Mimic,true,appearance,PaidInvestmentKind.UnknownLegacy,0)},Array.Empty<LedgerReceipt>());
            return appearance.Passenger is not null?
                DomainHash("DCPX.APPEARANCE.3",NativeIdentityLedgerCodec.EncodeTransportFingerprint(ledger,0)):
                appearance.GrowthDueTurn==-1?
                DomainHash("DCPX.APPEARANCE.1",NativeIdentityLedgerCodec.EncodeLegacyFingerprint(ledger,0)):
                DomainHash("DCPX.APPEARANCE.2",NativeIdentityLedgerCodec.EncodeTimedFingerprint(ledger,0));
        }
        public static bool AlreadyCommitted(NativeIdentityLedger ledger,long revision,NativeExchangeIntent value)
        {
            NativeIdentityLedgerCodec.Validate(ledger,revision);Validate(value);Require(ledger.Epoch==value.Epoch,"Exchange epoch mismatch.");
            var receipt=ledger.Receipts.SingleOrDefault(x=>x.Sequence==value.Sequence);
            if(receipt is not null)
            {
                Require(receipt.Operation==Operation(value.Sequence)&&receipt.Fingerprint.SequenceEqual(Fingerprint(value))&&
                    receipt.BeforeRevision==value.Revision&&
                    ((value.Kind==NativeExchangeKind.Blackmail&&value.Initiator.LogicalId==0)||value.Kind==NativeExchangeKind.LighthouseSecret?receipt.City==new LedgerCityReceipt(value.Actor,value.Initiator.X,value.Initiator.Y)&&receipt.Affected.Length==0:
                    receipt.City is null&&receipt.Affected.SequenceEqual(new[]{value.Initiator.LogicalId,value.Partner.LogicalId}.Distinct().OrderBy(x=>x))),"Exchange receipt conflicts with this intent.");
                return true; // Before fresh readiness/position/revision checks.
            }
            Require(value.Sequence==ledger.NextSequence&&value.Revision==revision&&value.NextLogicalId==ledger.NextLogicalId,"Exchange intent is stale or skips history.");
            Require(ledger.Receipts.Length<NativeIdentityLedgerCodec.MaxReceipts,"Exchange history capacity requires explicit migration.");return false;
        }
    }
}
