using System;
using System.IO;
using System.Linq;
using System.Buffers.Binary;
using Deception.Core;
namespace Deception.Tests
{
    public static partial class KernelTests
    {
        static void NativeEspionageLedgerTests()
        {
            var unit=new LedgerUnit(1,3,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,8,null,new(2,1,9),new[]{new LedgerAcquisition(1,9),new LedgerAcquisition(3,8)});
            var original=new NativeIdentityLedger(Guid.Parse("0b127acf-1cc1-4e32-9afc-ceaa4a4b23dc"),2,1,new[]{unit},Array.Empty<LedgerReceipt>(),new[]{new LedgerBlackout(2,3,4)},new[]{new LedgerDiscovery(2,3,4)});
            void Bad(NativeIdentityLedger l){bool bad=false;try{NativeIdentityLedgerCodec.Encode(l,0);}catch(InvalidDataException){bad=true;}Assert(bad);}
            void T(string id,Action f)=>Test(id,new[]{"DEC-171","DEC-173","DEC-179"},f);
            T("ESP-001-schema4-rogue-history-and-fog-roundtrip",()=>{
                var wire=NativeIdentityLedgerCodec.Encode(original,0);var a=NativeIdentityLedgerCodec.Decode(wire,0);var b=NativeIdentityLedgerCodec.Decode(wire,0);
                Assert(BinaryPrimitives.ReadUInt16LittleEndian(wire.AsSpan(4))==9&&a.Units[0].Rogue==unit.Rogue&&a.Units[0].Acquisitions!.SequenceEqual(unit.Acquisitions!));
                a.Units[0].Acquisitions![0]=new(1,99);a.Blackouts![0]=new(2,1,1);a.Discoveries![0]=new(2,1,1);
                Assert(NativeIdentityLedgerCodec.Encode(b,0).SequenceEqual(wire),"Decoded state aliases another snapshot");
            });
            T("ESP-002-schema3-migration-preserves-legacy-digest",()=>{
                var legacy=original with{Units=new[]{unit with{Rogue=null,Acquisitions=null}},Blackouts=null,Discoveries=null};
                var wire=NativeIdentityLedgerCodec.EncodeTransportFingerprint(legacy,0);var modern=NativeIdentityLedgerCodec.Decode(wire,0);
                Assert(modern.Units[0].Rogue is null&&(modern.Units[0].Acquisitions?.Length??0)==0&&(modern.Blackouts?.Length??0)==0);
                Assert(NativeIdentityLedgerCodec.EncodeTransportFingerprint(modern,0).SequenceEqual(wire));
            });
            T("ESP-003-rogue-owner-commander-validation",()=>{
                foreach(var r in new[]{new LedgerRogue(0,1,9),new LedgerRogue(2,0,9),new LedgerRogue(2,2,9)})Bad(original with{Units=new[]{unit with{Rogue=r}}});
                Bad(original with{Units=new[]{unit with{Alive=false}}});
            });
            T("ESP-004-acquisition-is-canonical-per-player",()=>{
                foreach(var history in new[]{new[]{new LedgerAcquisition(0,9)},new[]{new LedgerAcquisition(1,9),new LedgerAcquisition(1,8)},new[]{new LedgerAcquisition(3,8),new LedgerAcquisition(1,9)}})
                    Bad(original with{Units=new[]{unit with{Acquisitions=history}}});
            });
            T("ESP-005-blackout-mask-rejects-duplicates-and-offmap",()=>{
                foreach(var mask in new[]{new[]{new LedgerBlackout(0,1,1)},new[]{new LedgerBlackout(2,-1,1)},new[]{new LedgerBlackout(2,4096,1)},new[]{new LedgerBlackout(2,1,1),new LedgerBlackout(2,1,1)}})Bad(original with{Blackouts=mask});
            });
            T("ESP-006-discovery-provenance-is-canonical",()=>{
                foreach(var permits in new[]{new[]{new LedgerDiscovery(0,1,1)},new[]{new LedgerDiscovery(2,1,-1)},new[]{new LedgerDiscovery(2,2,1),new LedgerDiscovery(2,1,1)}})Bad(original with{Discoveries=permits});
            });
            T("ESP-007-physical-history-survives-costume-and-wrapper",()=>{
                var row=unit with{NativeId=40,Transport=new(3)};
                var l=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(original with{Units=new[]{row}},0),0);
                Assert(l.Units[0].Rogue==unit.Rogue&&l.Units[0].Acquisitions!.SequenceEqual(unit.Acquisitions!)&&l.Units[0].PaidStars==8);
            });
            T("ESP-008-cleared-rogue-retains-acquisition-history",()=>{
                var row=unit with{Rogue=null};var l=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(original with{Units=new[]{row}},0),0);
                Assert(l.Units[0].Rogue is null&&l.Units[0].Acquisitions!.SequenceEqual(unit.Acquisitions!));
            });
            T("ESP-009-retained-rogue-sight-roundtrips-without-aliases",()=>{
                var row=unit with{Rogue=unit.Rogue! with{Sight=new[]{new LedgerSight(1,2),new LedgerSight(2,1)},PendingSight=new[]{new LedgerSight(2,2)}}};
                var wire=NativeIdentityLedgerCodec.Encode(original with{Units=new[]{row}},0);
                var a=NativeIdentityLedgerCodec.Decode(wire,0);var b=NativeIdentityLedgerCodec.Decode(wire,0);
                Assert(a.Units[0].Rogue!.Sight!.SequenceEqual(row.Rogue!.Sight!)&&a.Units[0].Rogue!.PendingSight!.SequenceEqual(row.Rogue!.PendingSight!));a.Units[0].Rogue!.Sight![0]=new(9,9);
                Assert(NativeIdentityLedgerCodec.Encode(b,0).SequenceEqual(wire));
            });
            T("ESP-010-retained-sight-rejects-duplicates-order-and-range",()=>{
                foreach(var sight in new[]{new[]{new LedgerSight(-1,1)},new[]{new LedgerSight(4096,1)},new[]{new LedgerSight(1,1),new LedgerSight(1,1)},new[]{new LedgerSight(2,1),new LedgerSight(1,1)}})
                    { Bad(original with{Units=new[]{unit with{Rogue=unit.Rogue! with{Sight=sight}}}});Bad(original with{Units=new[]{unit with{Rogue=unit.Rogue! with{PendingSight=sight}}}}); }
            });
        }
    }
}
