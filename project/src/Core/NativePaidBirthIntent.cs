using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Deception.Core
{
    // Canonical purchase choice, not authority to train,
    // evidence of payment, a pending occupied retry, or a native action itself.
    public sealed record NativePaidBirthIntent(Guid Epoch,ulong Sequence,long Revision,ulong NextLogicalId,
        uint Turn,byte Actor,string NativeRuleId,int CityX,int CityY,uint ExpectedNativeCounter,int ExpectedPrice);

    public static class NativePaidBirthIntentCodec
    {
        public const uint Magic=0x42504344; // DCPB
        public const ushort Schema=1;
        public const int HeaderSize=16,FixedSize=78,Price=RecruitmentCatalog.Cost;
        // Old committed records remain decodable; only the current price may
        // stage a fresh purchase. Saved investment is never repriced.
        const int HistoricalPrice=3;
        public const string Warrior="dcp_mimic_form_warrior",Rider="dcp_mimic_form_rider";
        static void Require(bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
        public static string Form(string rule)=>rule switch
        {Warrior=>"warrior",Rider=>"rider",_=>throw new InvalidDataException("Unsupported paid-birth native rule ID.")};
        public static void Validate(NativePaidBirthIntent value)
        {
            ArgumentNullException.ThrowIfNull(value);
            Require(value.Epoch!=Guid.Empty&&value.Sequence>0&&value.Sequence<ulong.MaxValue&&
                value.Revision>=0&&value.Revision<long.MaxValue&&value.NextLogicalId>0&&value.NextLogicalId<ulong.MaxValue,
                "Invalid paid-birth lineage or exhausted private allocator.");
            _=Form(value.NativeRuleId);
            Require(value.ExpectedNativeCounter<uint.MaxValue&&(value.ExpectedPrice==Price||value.ExpectedPrice==HistoricalPrice),
                "Paid birth needs an available native allocation and a recognized exact price.");
            // Coordinates preserve the native signed Int32 representation.
            // The exact city and map bounds are checked by the supplied-state
            // adapter; the codec must not invent a different coordinate space.
        }
        public static byte[] Encode(NativePaidBirthIntent value)
        {
            Validate(value);byte[] rule=Encoding.ASCII.GetBytes(value.NativeRuleId);
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream,Encoding.UTF8,true);
            writer.Write(Magic);writer.Write(Schema);writer.Write((ushort)0);writer.Write(NativeStateFrame.NativeVersion);
            writer.Write(FixedSize+rule.Length-HeaderSize);writer.Write(value.Epoch.ToByteArray());writer.Write(value.Sequence);
            writer.Write(value.Revision);writer.Write(value.NextLogicalId);writer.Write(value.Turn);writer.Write(value.Actor);
            writer.Write(value.ExpectedPrice);writer.Write(value.ExpectedNativeCounter);writer.Write(value.CityX);writer.Write(value.CityY);
            writer.Write((byte)rule.Length);writer.Write(rule);writer.Flush();
            Require(stream.Length==FixedSize+rule.Length,"Paid-birth codec layout mismatch.");return stream.ToArray();
        }
        public static NativePaidBirthIntent Decode(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            Require(bytes.Length>=FixedSize&&bytes.Length<=FixedSize+NativeIdentityLedgerCodec.MaxText,
                "Truncated or oversized paid-birth intent.");
            using var stream=new MemoryStream(bytes,false);using var reader=new BinaryReader(stream,Encoding.UTF8,true);
            Require(reader.ReadUInt32()==Magic&&reader.ReadUInt16()==Schema&&reader.ReadUInt16()==0&&
                reader.ReadInt32()==NativeStateFrame.NativeVersion&&reader.ReadInt32()==bytes.Length-HeaderSize,
                "Unknown paid-birth header, native version or length.");
            Guid epoch=new(reader.ReadBytes(16));ulong sequence=reader.ReadUInt64();long revision=reader.ReadInt64();ulong next=reader.ReadUInt64();
            uint turn=reader.ReadUInt32();byte actor=reader.ReadByte();int price=reader.ReadInt32();uint counter=reader.ReadUInt32();
            int x=reader.ReadInt32(),y=reader.ReadInt32(),length=reader.ReadByte();
            Require(length>0&&length<=NativeIdentityLedgerCodec.MaxText&&stream.Position+length==stream.Length,
                "Noncanonical paid-birth rule length or trailing bytes.");
            byte[] raw=reader.ReadBytes(length);Require(raw.Length==length&&raw.All(c=>c<128),"Invalid paid-birth rule encoding.");
            var result=new NativePaidBirthIntent(epoch,sequence,revision,next,turn,actor,Encoding.ASCII.GetString(raw),x,y,counter,price);
            Validate(result);Require(stream.Position==stream.Length,"Paid-birth intent was not exactly consumed.");return result;
        }
        public static Guid Operation(ulong sequence)
        {
            Require(sequence>0&&sequence<ulong.MaxValue,"Invalid paid-birth operation sequence.");
            byte[] bytes=Encoding.ASCII.GetBytes("DCPBOP01").Concat(new byte[8]).ToArray();
            BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8),sequence);return new Guid(bytes);
        }
        public static byte[] Fingerprint(NativePaidBirthIntent value)
            =>SHA256.HashData(Encoding.ASCII.GetBytes("DCPB.INTENT.1\0").Concat(Encode(value)).ToArray());
    }

    // Values observed by the native adapter before original execution. These
    // constructors are not a trust boundary: only an exact native mutation token
    // and authoritative native checks can supply evidence for a live commit.
    public sealed record NativePaidBirthBefore(int NativeVersion,uint Turn,byte Actor,string NativeRuleId,
        int CityX,int CityY,byte CityOwner,bool IsCity,bool Empty,bool NativeEligible,
        uint NativeCounter,int Currency,int NativeCost,ushort ApparentMaximum,uint[] NativeIds,LedgerAppearance Appearance);
    public sealed record NativePaidBirthBody(uint Id,byte Owner,string NativeRuleId,string CachedRuleId,
        int X,int Y,int HomeX,int HomeY,ushort Health,int Maximum,int Promotion,int Attack,int Defence,int Range,
        bool Root,bool SingleBody,bool Moved,bool Attacked);
    public sealed record NativePaidBirthAfter(bool OriginalCompleted,bool Aborted,int MatchingHelperResults,bool ExactHelperBody,
        uint Turn,byte Actor,int NativeCost,uint NativeCounter,int Currency,NativePaidBirthBody Body,
        uint[] NativeIds,bool ExistingGraphPreserved,bool QueuePrefixPreserved);

    public sealed class NativePaidBirthPreparation
    {
        readonly byte[] IntentBytes,BeforeBytes,StagedBytes;
        readonly uint[] OldNativeIds;
        internal int CurrencyBefore {get;}
        internal uint[] NativeIdsCopy()=>(uint[])OldNativeIds.Clone();
        internal byte[] StagedCopy()=>(byte[])StagedBytes.Clone();
        public bool AlreadyCommitted {get;}
        public long BeforeRevision {get;}
        public long AfterRevision {get;}
        public NativePaidBirthIntent Intent=>NativePaidBirthIntentCodec.Decode(IntentBytes);
        public byte[] BeforePayload=>(byte[])BeforeBytes.Clone();
        public uint ExpectedNativeId=>checked(Intent.ExpectedNativeCounter+1);
        internal NativePaidBirthPreparation(NativePaidBirthIntent intent,bool duplicate,long revision,byte[] before,
            byte[] staged,int currency,uint[] ids)
        {
            IntentBytes=NativePaidBirthIntentCodec.Encode(intent);AlreadyCommitted=duplicate;BeforeRevision=revision;
            AfterRevision=duplicate?revision:checked(revision+1);BeforeBytes=(byte[])before.Clone();StagedBytes=(byte[])staged.Clone();
            CurrencyBefore=currency;OldNativeIds=(uint[])ids.Clone();
        }
    }

    public sealed class NativePaidBirthResult
    {
        readonly byte[] Payload;
        public bool AlreadyCommitted {get;}
        public long BeforeRevision {get;}
        public long AfterRevision {get;}
        public ulong LogicalId {get;}
        public uint NativeId {get;}
        public byte[] Bytes()=>(byte[])Payload.Clone();
        public NativeIdentityLedger Ledger=>NativeIdentityLedgerCodec.Decode(Payload,AfterRevision);
        internal NativePaidBirthResult(bool duplicate,long before,long after,ulong logical,uint nativeId,byte[] payload)
        {AlreadyCommitted=duplicate;BeforeRevision=before;AfterRevision=after;LogicalId=logical;NativeId=nativeId;Payload=(byte[])payload.Clone();}
    }

    // Pure planning only: no native references, callbacks, setters, money/ID
    // writes or binding publication. Staging a proposed encoded row below does
    // not attach it to any state; callers receive no staged paid payload until
    // Complete has checked the post-native witness. No occupied/retry phase is
    // represented and no phantom identity is inserted into the supplied ledger.
    public static class NativePaidBirthLedger
    {
        static void Require(bool ok,string reason){if(!ok)throw new InvalidDataException(reason);}
        static uint[] Ids(uint[] values)
        {
            Require(values is not null&&values.Length<=16384&&values.Distinct().Count()==values.Length,"Invalid native body identity census.");
            return values.OrderBy(x=>x).ToArray();
        }
        static byte[] Payload(NativeIdentityLedger ledger,long revision)=>NativeIdentityLedgerCodec.Encode(ledger,revision);
        public static bool AlreadyCommitted(NativeIdentityLedger ledger,long revision,NativePaidBirthIntent intent)
        {
            NativeIdentityLedgerCodec.Validate(ledger,revision);NativePaidBirthIntentCodec.Validate(intent);
            Require(ledger.Epoch==intent.Epoch,"Paid-birth Epoch differs from the supplied ledger.");
            var receipt=ledger.Receipts.SingleOrDefault(r=>r.Sequence==intent.Sequence);
            if(receipt is not null)
            {
                Require(receipt.Operation==NativePaidBirthIntentCodec.Operation(intent.Sequence)&&
                    receipt.Fingerprint.SequenceEqual(NativePaidBirthIntentCodec.Fingerprint(intent))&&
                    receipt.BeforeRevision==intent.Revision&&receipt.AfterRevision==checked(intent.Revision+1)&&
                    receipt.City is null&&receipt.Affected.SequenceEqual(new[]{intent.NextLogicalId}),"Paid-birth receipt conflicts with this intent.");
                var identity=ledger.Units.SingleOrDefault(u=>u.LogicalId==intent.NextLogicalId);
                // Later movement, appearance changes and legitimate death do
                // not undo a receipt. The identity/paid facts remain attached to
                // that logical row, including a tombstone. Never train again.
                Require(identity is not null&&identity.NativeId==checked(intent.ExpectedNativeCounter+1)&&
                    identity.Kind==LedgerActualKind.Mimic&&identity.InvestmentKind==PaidInvestmentKind.Known&&identity.PaidStars==intent.ExpectedPrice,
                    "Committed paid-birth identity or investment evidence conflicts.");
                return true;
            }
            Require(intent.Sequence==ledger.NextSequence&&intent.Revision==revision&&intent.NextLogicalId==ledger.NextLogicalId,
                "Paid-birth intent is stale or skips global history.");
            return false;
        }
        public static NativePaidBirthPreparation Prepare(NativeIdentityLedger ledger,long revision,NativePaidBirthIntent intent,NativePaidBirthBefore? observed)
        {
            bool duplicate=AlreadyCommitted(ledger,revision,intent);
            byte[] before=Payload(ledger,revision);var owned=NativeIdentityLedgerCodec.Decode(before,revision);
            if(duplicate)return new(intent,true,revision,before,before,0,Array.Empty<uint>());
            Require(observed is not null,"Fresh paid birth requires pre-native evidence.");var b=observed!;
            Require(b.NativeVersion==NativeStateFrame.NativeVersion&&b.Turn==intent.Turn&&b.Actor==intent.Actor&&
                b.NativeRuleId==intent.NativeRuleId&&b.CityX==intent.CityX&&b.CityY==intent.CityY&&b.CityOwner==intent.Actor,
                "Paid-birth native source, actor, turn, selected form or city differs.");
            Require(b.IsCity&&b.Empty&&b.NativeEligible,"Fresh paid birth needs an eligible empty owned city; occupied retries require persisted payment semantics.");
            Require(intent.ExpectedPrice==NativePaidBirthIntentCodec.Price&&b.NativeCounter==intent.ExpectedNativeCounter&&
                b.NativeCost==intent.ExpectedPrice&&b.Currency>=intent.ExpectedPrice,
                "Paid-birth native counter, cost or funds differ.");
            uint[] nativeIds=Ids(b.NativeIds);uint born=checked(intent.ExpectedNativeCounter+1);
            Require(!nativeIds.Contains(born)&&!owned.Units.Any(u=>u.NativeId==born),"Paid-birth allocation would reuse an existing native/logical identity.");
            Require(owned.Units.Where(u=>u.Alive).All(u=>nativeIds.Contains(u.NativeId)),"A living private identity is missing from the pre-native census.");
            Require(owned.Units.Length<NativeIdentityLedgerCodec.MaxUnits&&owned.Receipts.Length<NativeIdentityLedgerCodec.MaxReceipts,
                "Paid-birth ledger capacity requires explicit migration.");
            var a=b.Appearance;Require(a is not null&&a.Form==NativePaidBirthIntentCodec.Form(intent.NativeRuleId)&&
                a.Health==a.Maximum&&a.Maximum>0&&a.Maximum==b.ApparentMaximum&&!a.Veteran&&a.Effects is not null&&a.Effects.Length==0&&
                a.Concealment==ApparentConcealment.Visible&&a.GrowthTurns==-1,"Unsupported initial paid-birth appearance.");
            // The native adapter resolves all three effective cosmetic pairs;
            // the pure layer neither reads global GameSkin nor invents colors.
            var row=new LedgerUnit(intent.NextLogicalId,born,true,LedgerActualKind.Mimic,true,a,PaidInvestmentKind.Known,intent.ExpectedPrice);
            var receipt=new LedgerReceipt(intent.Sequence,NativePaidBirthIntentCodec.Operation(intent.Sequence),NativePaidBirthIntentCodec.Fingerprint(intent),
                revision,checked(revision+1),new[]{intent.NextLogicalId});
            var proposed=new NativeIdentityLedger(owned.Epoch,checked(owned.NextLogicalId+1),checked(owned.NextSequence+1),
                owned.Units.Append(row).ToArray(),owned.Receipts.Append(receipt).ToArray(),owned.Blackouts,owned.Discoveries,owned.Blackmails,owned.OnlineContract);
            byte[] staged=Payload(proposed,checked(revision+1)); // Also proves exact final byte bound before native mutation.
            return new(intent,false,revision,before,staged,b.Currency,nativeIds);
        }
        public static NativePaidBirthResult Complete(NativePaidBirthPreparation prepared,NativeIdentityLedger current,long revision,NativePaidBirthAfter? observed)
        {
            ArgumentNullException.ThrowIfNull(prepared);var intent=prepared.Intent;
            if(AlreadyCommitted(current,revision,intent))
                return new(true,revision,revision,intent.NextLogicalId,prepared.ExpectedNativeId,Payload(current,revision));
            Require(!prepared.AlreadyCommitted&&revision==prepared.BeforeRevision&&Payload(current,revision).SequenceEqual(prepared.BeforePayload),
                "Paid-birth ledger changed during native execution.");
            Require(observed is not null,"Paid birth requires completed native evidence.");var a=observed!;
            Require(a.OriginalCompleted&&!a.Aborted&&a.MatchingHelperResults==1&&a.ExactHelperBody,
                "Native paid birth did not complete with exactly one matching helper body.");
            Require(a.Turn==intent.Turn&&a.Actor==intent.Actor&&a.NativeCost==intent.ExpectedPrice&&a.NativeCounter==prepared.ExpectedNativeId&&
                a.Currency==checked(prepared.CurrencyBefore-intent.ExpectedPrice),"Native paid-birth context, allocation or exact debit differs.");
            var body=a.Body;Require(body is not null&&body.Id==prepared.ExpectedNativeId&&body.Owner==intent.Actor&&
                body.NativeRuleId==intent.NativeRuleId&&body.CachedRuleId==intent.NativeRuleId&&body.X==intent.CityX&&body.Y==intent.CityY&&
                body.HomeX==intent.CityX&&body.HomeY==intent.CityY,"Native newborn identity, type, tile or home differs.");
            Require(body!.Root&&body.SingleBody&&body.Health==10&&body.Maximum==10&&body.Promotion==0&&
                body.Attack==0&&body.Defence==0&&body.Range==0&&body.Moved&&body.Attacked,"Native newborn Mimic invariants or exhaustion differ.");
            Require(a.ExistingGraphPreserved&&a.QueuePrefixPreserved&&
                Ids(a.NativeIds).SequenceEqual(prepared.NativeIdsCopy().Append(prepared.ExpectedNativeId).OrderBy(x=>x)),
                "Native birth changed an old identity/queue prefix or created extra bodies.");
            return new(false,revision,prepared.AfterRevision,intent.NextLogicalId,prepared.ExpectedNativeId,prepared.StagedCopy());
        }
    }
}

// Adapter integration obligations, intentionally outside this draft:
// * Check an existing matching receipt before native occupancy/cost/funds. A
//   duplicate must skip original and add no children; Complete's no-op is pure.
// * Tag exact TrainCommand->one TrainAction through paired reliable native
//   transport; match actor/type/coordinates/cost against this canonical intent.
// * Hold exact state/owner/mutation authority and in-flight write/copy barrier.
//   Revalidate existing graph object identities, all untouched native fields and
//   queue prefix bytes; boolean witnesses above do not prove those observations.
// * Call Prepare before native execution; keep it private to that operation.
//   Staged bytes are not accessible to the native writer/binding. A pure prepared
//   object cannot prevent duplicate callback publication; use one guarded Set/CAS.
// * Observe original completion and the exact single TrainUnit return. Supply
//   body values from that exact body, not an independently looked-up replacement.
//   Keep actual effects/poison separate from the precomputed clean appearance.
// * Before publishing Complete.Bytes, validate the full candidate ledger against
//   the actual supplied post-native graph under the same token. Install once and
//   verify binding; then release the barrier and let native children run normally.
// * Failure after original begins is quarantine/native abort, not native rollback.
//   No row/refund is created for Cost0, an occupied debit/no body or an unscoped helper.
//   A future occupied retry needs a separate persisted pending-payment schema.
// * Sequence spans birth and exchange receipts. Never recompute paid investment
//   from current catalog price; old UnknownLegacy0 rows are preserved verbatim.
