using System;
using System.IO;
using System.Linq;
using System.Buffers.Binary;
using System.Security.Cryptography;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        static void NativeBlackmailLedgerTests()
        {
            Guid epoch=Guid.Parse("0119bf46-de8c-40cb-888c-b2bec5b31487");
            NativeIdentityLedger Empty()=>new(epoch,1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>());
            LedgerBlackmail Mark(byte p=1,byte owner=2,int x=3,int y=4)=>new(p,owner,x,y,8);
            NativeExchangeIntent Intent()=>new(epoch,1,0,8,1,NativeExchangeKind.Blackmail,false,NativePartnerReference.Existing,1,
                new(0,0,3,4),new(0,0,3,4),SHA256.HashData(new byte[]{1,2,3}));
            LedgerReceipt Receipt(NativeExchangeIntent i)=>new(i.Sequence,NativeExchangeIntentCodec.Operation(i.Sequence),NativeExchangeIntentCodec.Fingerprint(i),i.Revision,i.Revision+1,
                Array.Empty<ulong>(),new(i.Actor,i.Initiator.X,i.Initiator.Y));
            void Bad(Action a){bool rejected=false;try{a();}catch(InvalidDataException){rejected=true;}Assert(rejected,"Invalid Blackmail payload accepted");}
            void T(string id,Action a)=>Test(id,new[]{"A28","A29"},a);

            T("BM-001-schema6-multiple-blackmailers-roundtrip",()=>{
                var source=Empty() with{Blackmails=new[]{Mark(),Mark(3),Mark(1,3,4,4) with{LastPaidTurn=9,LastCollectedTurn=10}}};
                var wire=NativeIdentityLedgerCodec.Encode(source,0);Assert(BinaryPrimitives.ReadUInt16LittleEndian(wire.AsSpan(4))==9);
                var a=NativeIdentityLedgerCodec.Decode(wire,0);var b=NativeIdentityLedgerCodec.Decode(wire,0);
                Assert(a.Blackmails!.SequenceEqual(source.Blackmails)&&a.Blackmails[2].LastPaidTurn==9&&a.Blackmails[2].LastCollectedTurn==10);
                a.Blackmails[0]=a.Blackmails[0] with{Victim=4};
                Assert(b.Blackmails![0].Victim==2&&NativeIdentityLedgerCodec.Encode(b,0).SequenceEqual(wire));
            });
            T("BM-002-record-identity-order-and-time-bounds",()=>{
                foreach(var mark in new[]{Mark(0),Mark(255),Mark(1,0),Mark(1,255),Mark(2,2),Mark(1,2,-1),Mark(1,2,4096),
                    Mark(1,2,3,-1),Mark(1,2,3,4096),Mark() with{LastPaidTurn=-2},Mark() with{LastPaidTurn=7},Mark() with{LastPaidTurn=(long)uint.MaxValue+1}})
                    Bad(()=>NativeIdentityLedgerCodec.Encode(Empty() with{Blackmails=new[]{mark}},0));
                foreach(var marks in new[]{new[]{Mark(),Mark()},new[]{Mark(3),Mark()},new[]{Mark(),Mark(3,4)},new[]{Mark(1,2,4),Mark()},new LedgerBlackmail[]{null!}})
                    Bad(()=>NativeIdentityLedgerCodec.Encode(Empty() with{Blackmails=marks},0));
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(Empty() with{Blackmails=new[]{Mark() with{CreatedTurn=uint.MaxValue,LastPaidTurn=uint.MaxValue}}},0),0).Blackmails!.Length==1);
            });
            T("BM-003-city-intent-is-zero-identity-only",()=>{
                var source=Intent();var wire=NativeExchangeIntentCodec.Encode(source);Assert(wire.Length==136);
                var copy=NativeExchangeIntentCodec.Decode(wire);Assert(copy.Kind==NativeExchangeKind.Blackmail&&copy.Initiator==copy.Partner&&copy.Initiator.LogicalId==0);
                foreach(var bad in new[]{source with{Actor=0},source with{Actor=255},source with{Swap=true},source with{PartnerReference=NativePartnerReference.Allocate},
                    source with{Initiator=source.Initiator with{LogicalId=1}},source with{Initiator=source.Initiator with{NativeId=1}},
                    source with{Partner=source.Partner with{X=5}},source with{Initiator=new(0,0,-1,2),Partner=new(0,0,-1,2)},source with{SourceAppearanceHash=new byte[31]}})
                    Bad(()=>NativeExchangeIntentCodec.Encode(bad));
                foreach(var kind in Enum.GetValues<NativeExchangeKind>().Where(k=>k!=NativeExchangeKind.Blackmail&&k!=NativeExchangeKind.LighthouseSecret))
                    Bad(()=>NativeExchangeIntentCodec.Encode(source with{Kind=kind}));
            });
            T("BM-004-city-receipt-does-not-invent-unit-history",()=>{
                var intent=Intent();var receipt=Receipt(intent);var ledger=Empty() with{NextSequence=2,Receipts=new[]{receipt},Blackmails=new[]{Mark()}};
                var loaded=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(ledger,1),1);
                Assert(loaded.Units.Length==0&&loaded.NextLogicalId==1&&loaded.Receipts[0].City==new LedgerCityReceipt(1,3,4));
                Assert(NativeExchangeIntentCodec.AlreadyCommitted(loaded,1,intent));
                foreach(var changed in new[]{intent with{Actor=3},intent with{Turn=9},intent with{Initiator=new(0,0,4,4),Partner=new(0,0,4,4)}})
                    Bad(()=>NativeExchangeIntentCodec.AlreadyCommitted(loaded,1,changed));
                foreach(var invalid in new[]{receipt with{City=null},receipt with{City=new(0,3,4)},receipt with{City=new(1,-1,4)},receipt with{Affected=new ulong[]{1}}})
                    Bad(()=>NativeIdentityLedgerCodec.Encode(ledger with{Receipts=new[]{invalid}},1));
            });
            T("BM-005-unit-receipts-cannot-reuse-city-domain",()=>{
                var ledger=Empty() with{NextLogicalId=2,NextSequence=2,Units=new[]{new LedgerUnit(1,1,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,2)},
                    Receipts=new[]{Receipt(Intent()) with{Affected=new ulong[]{1}}}};
                Bad(()=>NativeIdentityLedgerCodec.Encode(ledger,1));
                var valid=ledger with{Receipts=new[]{ledger.Receipts[0] with{City=null}}};
                NativeIdentityLedgerCodec.Validate(valid,1);
                Bad(()=>NativeExchangeIntentCodec.AlreadyCommitted(valid,1,Intent()));
            });
            T("BM-006-city-wire-truncation-corruption-and-trailing",()=>{
                var ledger=Empty() with{NextSequence=2,Receipts=new[]{Receipt(Intent())},Blackmails=new[]{Mark()}};
                var wire=NativeIdentityLedgerCodec.Encode(ledger,1);
                for(int n=0;n<wire.Length;n++)Bad(()=>NativeIdentityLedgerCodec.Decode(wire[..n],1));
                Bad(()=>NativeIdentityLedgerCodec.Decode(wire.Concat(new byte[]{0}).ToArray(),1));
                // One 50-byte schema9 mark and the null-contract Boolean follow the count.
                int blackmailCount=wire.Length-55;
                Assert(BinaryPrimitives.ReadInt32LittleEndian(wire.AsSpan(blackmailCount))==1&&wire[^1]==0,
                    "Blackmail count corruption no longer targets its count field");
                var count=wire.ToArray();BinaryPrimitives.WriteInt32LittleEndian(count.AsSpan(blackmailCount),int.MaxValue);Bad(()=>NativeIdentityLedgerCodec.Decode(count,1));
                var flag=wire.ToArray();flag[118]=2;Bad(()=>NativeIdentityLedgerCodec.Decode(flag,1));
                Assert(NativeIdentityLedgerCodec.Encode(NativeIdentityLedgerCodec.Decode(wire,1),1).SequenceEqual(wire));
            });
            T("BM-007-schema4-save-migrates-without-blackmail",()=>{
                // Independent schema4 wire fixture: no units/receipts; one
                // Blackout and one discovery. No current encoder used.
                using var s=new MemoryStream();using(var w=new BinaryWriter(s,System.Text.Encoding.UTF8,true))
                {w.Write(0x4c504344u);w.Write((ushort)4);w.Write((ushort)0);w.Write(epoch.ToByteArray());w.Write(1ul);w.Write(1ul);w.Write((ushort)0);w.Write((ushort)0);
                 w.Write(1);w.Write((byte)2);w.Write(3);w.Write(4);w.Write(1);w.Write((byte)1);w.Write(5);w.Write(6);}
                var old=NativeIdentityLedgerCodec.Decode(s.ToArray(),0);
                Assert(old.Blackmails is null&&old.Blackouts![0]==new LedgerBlackout(2,3,4)&&old.Discoveries![0]==new LedgerDiscovery(1,5,6));
                var migrated=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(old,0),0);
                Assert(migrated.Blackmails!.Length==0&&migrated.Blackouts!.SequenceEqual(old.Blackouts)&&migrated.Discoveries!.SequenceEqual(old.Discoveries));
            });
            T("BM-008-city-data-cannot-enter-legacy-fingerprints",()=>{
                foreach(var ledger in new[]{Empty() with{Blackmails=new[]{Mark()}},Empty() with{NextSequence=2,Receipts=new[]{Receipt(Intent())}}})
                {Bad(()=>NativeIdentityLedgerCodec.EncodeLegacyFingerprint(ledger,1));Bad(()=>NativeIdentityLedgerCodec.EncodeTimedFingerprint(ledger,1));Bad(()=>NativeIdentityLedgerCodec.EncodeTransportFingerprint(ledger,1));}
            });
            T("BM-009-setup-sequence-revision-and-digest-idempotence",()=>{
                var intent=Intent();Assert(!NativeExchangeIntentCodec.AlreadyCommitted(Empty(),0,intent));
                Bad(()=>NativeExchangeIntentCodec.AlreadyCommitted(Empty(),0,intent with{Sequence=2}));
                Bad(()=>NativeExchangeIntentCodec.AlreadyCommitted(Empty(),0,intent with{Revision=1}));
                var ledger=Empty() with{NextSequence=2,Receipts=new[]{Receipt(intent)},Blackmails=new[]{Mark()}};
                var advanced=ledger with{Blackmails=new[]{Mark() with{LastPaidTurn=9,LastCollectedTurn=10}}};
                Assert(NativeExchangeIntentCodec.AlreadyCommitted(advanced,1,intent),"Payout must not invalidate setup receipt");
                Bad(()=>NativeExchangeIntentCodec.AlreadyCommitted(advanced,1,intent with{SourceAppearanceHash=new byte[32]}));
            });
            T("BM-010-unrelated-ledger-updates-preserve-blackmail",()=>{
                var source=Empty() with{Blackmails=new[]{Mark(),Mark(3)}};
                var updated=source with{Blackouts=new[]{new LedgerBlackout(2,1,1)},Discoveries=new[]{new LedgerDiscovery(3,1,1)}};
                var loaded=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(updated,0),0);
                Assert(loaded.Blackmails!.SequenceEqual(source.Blackmails!));
                Assert(NativeExchangeCatalog.Id(NativeExchangeKind.Blackmail)=="dcp_exchange_blackmail"&&!NativeExchangeCatalog.IsSingle(NativeExchangeKind.Blackmail));
            });
            T("BM-011-schema5-migrates-both-completed-payment-sides",()=>{
                foreach(long paid in new long[]{-1,9})
                {
                    using var s=new MemoryStream();using(var w=new BinaryWriter(s,System.Text.Encoding.UTF8,true))
                    {w.Write(0x4c504344u);w.Write((ushort)5);w.Write((ushort)0);w.Write(epoch.ToByteArray());w.Write(1ul);w.Write(1ul);w.Write((ushort)0);w.Write((ushort)0);
                     w.Write(0);w.Write(0);w.Write(1);w.Write((byte)1);w.Write((byte)2);w.Write(3);w.Write(4);w.Write(8u);w.Write(paid);}
                    var old=NativeIdentityLedgerCodec.Decode(s.ToArray(),0);Assert(old.Blackmails!.Single()==new LedgerBlackmail(1,2,3,4,8,paid,paid));
                    var migrated=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(old,0),0);Assert(migrated.Blackmails!.SequenceEqual(old.Blackmails));
                }
            });
            T("BM-012-independent-collection-time-bounds-and-roundtrip",()=>{
                foreach(long invalid in new long[]{-2,7,(long)uint.MaxValue+1})Bad(()=>NativeIdentityLedgerCodec.Encode(Empty() with{Blackmails=new[]{Mark() with{LastCollectedTurn=invalid}}},0));
                var a=Mark() with{LastPaidTurn=8,LastCollectedTurn=12};var wire=NativeIdentityLedgerCodec.Encode(Empty() with{Blackmails=new[]{a}},0);
                Assert(NativeIdentityLedgerCodec.Decode(wire,0).Blackmails!.Single()==a);
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(Empty() with{Blackmails=new[]{a with{LastPaidTurn=-1}}},0),0).Blackmails!.Single().LastCollectedTurn==12);
            });

            NativeIdentityLedger UnitLedger()=>Empty() with{NextLogicalId=3,Units=new[]{
                new LedgerUnit(1,7,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,2),
                new LedgerUnit(2,8,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,3)}};
            LedgerBlackmail UnitMark(byte actor=1,ulong unit=1,int layers=1,long applied=8)=>
                new(actor,2,3,4,8,UnitId:unit,Layers:layers,LastAppliedTurn:applied);
            NativeExchangeIntent UnitIntent(bool allocate=false)=>Intent() with{
                NextLogicalId=allocate?1ul:3ul,PartnerReference=allocate?NativePartnerReference.Allocate:NativePartnerReference.Existing,
                Initiator=new(1,7,5,6),Partner=new(1,7,5,6)};
            T("BM-013-schema9-unit-layers-roundtrip",()=>{
                var rows=new[]{UnitMark(1,1,4,11),UnitMark(1,2,2,10),UnitMark(3,1,3,9)};
                var source=UnitLedger() with{Blackmails=rows};var wire=NativeIdentityLedgerCodec.Encode(source,0);
                var copy=NativeIdentityLedgerCodec.Decode(wire,0);
                Assert(BinaryPrimitives.ReadUInt16LittleEndian(wire.AsSpan(4))==9&&copy.Blackmails!.SequenceEqual(rows));
                Assert(copy.Blackmails.Sum(r=>r.Layers)==9&&copy.Blackmails.Count(r=>r.UnitId==1)==2,
                    "Distinct actors and multiple targets sharing a city must preserve every layer");
                copy.Blackmails[0]=copy.Blackmails[0] with{Layers=1};
                Assert(NativeIdentityLedgerCodec.Decode(wire,0).Blackmails![0].Layers==4,"Decoded rows alias another read");
            });
            T("BM-014-unit-layer-identities-order-and-bounds",()=>{
                foreach(var row in new[]{UnitMark(unit:3),UnitMark(layers:0),UnitMark(layers:65536),UnitMark(applied:7),
                    UnitMark(applied:-1),UnitMark(applied:(long)uint.MaxValue+1)})
                    Bad(()=>NativeIdentityLedgerCodec.Encode(UnitLedger() with{Blackmails=new[]{row}},0));
                foreach(var rows in new[]{new[]{UnitMark(),UnitMark()},new[]{UnitMark(unit:2),UnitMark()},
                    new[]{UnitMark(),UnitMark() with{X=4}},new[]{UnitMark(),UnitMark(3) with{Victim=4}},
                    new[]{UnitMark(),UnitMark(3) with{Y=5}}})
                    Bad(()=>NativeIdentityLedgerCodec.Encode(UnitLedger() with{Blackmails=rows},0));
                var maximum=UnitMark(layers:65535,applied:uint.MaxValue);
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(UnitLedger() with{Blackmails=new[]{maximum}},0),0).Blackmails!.Single()==maximum);
            });
            T("BM-015-schema8-city-record-migrates-as-legacy-only",()=>{
                // Independent last-released schema fixture, not emitted by the
                // current encoder. Existing city liens have no honest unit binding.
                using var stream=new MemoryStream();using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {w.Write(0x4c504344u);w.Write((ushort)8);w.Write((ushort)0);w.Write(epoch.ToByteArray());w.Write(1ul);w.Write(1ul);
                 w.Write((ushort)0);w.Write((ushort)0);w.Write(0);w.Write(0);w.Write(1);w.Write((byte)1);w.Write((byte)2);
                 w.Write(3);w.Write(4);w.Write(8u);w.Write(9L);w.Write(10L);w.Write(false);}
                var old=NativeIdentityLedgerCodec.Decode(stream.ToArray(),0);var row=old.Blackmails!.Single();
                Assert(row==new LedgerBlackmail(1,2,3,4,8,9,10)&&row.UnitId==0,
                    "Legacy city records must not attach to an invented target");
                var migrated=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(old,0),0);
                Assert(migrated.Blackmails!.Single()==row&&migrated.Units.Length==0&&migrated.NextLogicalId==1);
            });
            T("BM-016-unit-intent-existing-and-allocated-wire",()=>{
                foreach(bool allocate in new[]{false,true})
                {
                    var source=UnitIntent(allocate);var wire=NativeExchangeIntentCodec.Encode(source);
                    var copy=NativeExchangeIntentCodec.Decode(wire);
                    Assert(wire.Length==136&&NativeExchangeIntentCodec.Encode(copy).SequenceEqual(wire));
                    Assert(copy.Initiator.LogicalId==1&&copy.Initiator==copy.Partner&&
                        copy.PartnerReference==(allocate?NativePartnerReference.Allocate:NativePartnerReference.Existing));
                    foreach(var invalid in new[]{source with{Swap=true},source with{Actor=0},source with{Actor=255},
                        source with{Partner=source.Partner with{NativeId=9}},source with{Partner=source.Partner with{X=2}},
                        source with{Initiator=new(1,7,-1,6),Partner=new(1,7,-1,6)},
                        source with{Initiator=new(1,7,5,4096),Partner=new(1,7,5,4096)},
                        source with{PartnerReference=allocate?NativePartnerReference.Existing:NativePartnerReference.Allocate}})
                        Bad(()=>NativeExchangeIntentCodec.Encode(invalid));
                }
                var skipped=UnitIntent(true) with{Initiator=new(2,8,5,6),Partner=new(2,8,5,6)};
                Bad(()=>NativeExchangeIntentCodec.Encode(skipped));
            });
            T("BM-017-unit-receipt-retry-survives-payment-and-transport",()=>{
                var intent=UnitIntent();var receipt=new LedgerReceipt(1,NativeExchangeIntentCodec.Operation(1),
                    NativeExchangeIntentCodec.Fingerprint(intent),0,1,new ulong[]{1});
                var ledger=UnitLedger() with{NextSequence=2,Receipts=new[]{receipt},Blackmails=new[]{UnitMark(layers:2,applied:9)}};
                var moved=ledger with{Units=ledger.Units.Select(r=>r.LogicalId==1?r with{NativeId=99,Transport=new LedgerTransport(7)}:r).ToArray(),
                    Blackmails=new[]{UnitMark(layers:2,applied:9) with{LastPaidTurn=10,LastCollectedTurn=11}}};
                var loaded=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(moved,1),1);
                Assert(NativeExchangeIntentCodec.AlreadyCommitted(loaded,1,intent),"Exact setup replay depends on logical receipt, not current root or position");
                Bad(()=>NativeExchangeIntentCodec.AlreadyCommitted(loaded,1,intent with{Actor=3}));
                Bad(()=>NativeExchangeIntentCodec.AlreadyCommitted(loaded,1,intent with{Turn=9}));
                Bad(()=>NativeExchangeIntentCodec.AlreadyCommitted(loaded with{Receipts=new[]{receipt with{Affected=new ulong[]{2}}}},1,intent));
                Bad(()=>NativeExchangeIntentCodec.AlreadyCommitted(loaded with{Receipts=new[]{receipt with{Affected=Array.Empty<ulong>(),City=new(1,5,6)}}},1,intent));
            });
            T("BM-018-unit-liens-survive-record-retirement-until-cleanup",()=>{
                var ledger=UnitLedger() with{Blackmails=new[]{UnitMark()}};
                var dead=ledger with{Units=ledger.Units.Select(r=>r.LogicalId==1?r with{Alive=false}:r).ToArray()};
                var copy=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(dead,0),0);
                Assert(!copy.Units[0].Alive&&copy.Blackmails!.Single().UnitId==1,
                    "Death reconciliation must encode before the native economy prunes the retired lien");
            });
        }
    }
}
