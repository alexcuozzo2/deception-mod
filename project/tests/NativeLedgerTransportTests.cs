using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        // Wire/value tests: native cargo graph and transport commands have separate fixtures.
        static void NativeLedgerTransportTests()
        {
            var epoch=Guid.Parse("c88e55ab-1f59-4615-b3fb-7f4ec12514dc");
            LedgerAppearance Land(string form="warrior")=>new(form,new("Imperius","Default"),new("Bardur","Default"),
                new("Oumaji","Default"),12345,73,100,false,ApparentConcealment.Visible,-1,new[]{"Frozen"});
            LedgerUnit Vessel()=>new(1,40,true,LedgerActualKind.Mimic,true,Land("scoutship") with{Passenger=Land("giant") with{Health=250,Maximum=400}},
                PaidInvestmentKind.Known,3,new(12));
            NativeIdentityLedger Ledger(params LedgerUnit[] rows)=>new(epoch,10,1,rows,Array.Empty<LedgerReceipt>());
            void Invalid(Action f){bool bad=false;try{f();}catch(InvalidDataException){bad=true;}Assert(bad,"Invalid transport payload was accepted");}
            Test("NAV-001-schema3-independent-vessel-passenger-and-actual-cargo",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                var original=Ledger(Vessel());var wire=NativeIdentityLedgerCodec.Encode(original,0);
                Assert(BinaryPrimitives.ReadUInt16LittleEndian(wire.AsSpan(4))==9);
                var a=NativeIdentityLedgerCodec.Decode(wire,0);var b=NativeIdentityLedgerCodec.Decode(wire,0);
                var row=a.Units.Single();var appearance=row.Appearance!;
                Assert(row.NativeId==40&&row.LogicalId==1&&row.Transport!.PassengerNativeId==12&&row.PaidStars==3);
                Assert(appearance.Form=="scoutship"&&appearance.Health==73&&appearance.Passenger!.Form=="giant"&&appearance.Passenger.Health==250);
                Assert(appearance.Passenger!.TribeVisual==new LedgerVisualProfile("Imperius","Default")&&appearance.Passenger.ClimateVisual.Tribe=="Bardur");
                Assert(!ReferenceEquals(appearance.Effects,b.Units[0].Appearance!.Effects)&&
                    !ReferenceEquals(appearance.Passenger.Effects,b.Units[0].Appearance!.Passenger!.Effects));
                appearance.Passenger.Effects[0]="Poisoned";original.Units[0].Appearance!.Effects[0]="Boosted";
                Assert(NativeIdentityLedgerCodec.Encode(b,0).SequenceEqual(wire),"Passenger decode aliases caller/another decode");
            });
            Test("NAV-002-cargo-binding-rejects-self-root-alias-and-duplicate",new[]{"DEC-171","DEC-179"},()=> {
                var a=Vessel();var plain=new LedgerUnit(2,12,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0);
                foreach(var ledger in new[]{Ledger(a with{Transport=new(40)}),Ledger(a,plain),
                    Ledger(plain with{LogicalId=1},a with{LogicalId=2}),Ledger(a,a with{LogicalId=2,NativeId=41}),
                    Ledger(a with{Alive=false,Independent=false,Appearance=null})})
                    Invalid(()=>NativeIdentityLedgerCodec.Encode(ledger,0));
                var dead=plain with{Alive=false};
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(Ledger(a,dead),0),0).Units.Length==2,
                    "Historical tombstone cannot claim live cargo but may retain its old native number");
            });
            Test("NAV-003-apparent-cargo-is-one-level-and-land-only",new[]{"DEC-171","DEC-179"},()=> {
                var a=Vessel();var boat=a.Appearance!;
                foreach(var appearance in new[]{boat with{Form="warrior"},boat with{Passenger=Land("scoutship")},
                    boat with{Passenger=Land() with{Passenger=Land()}}})
                    Invalid(()=>NativeIdentityLedgerCodec.Encode(Ledger(a with{Appearance=appearance}),0));
                var egg=Land("dragonegg") with{GrowthTurns=1,GrowthDueTurn=9,Health=60};
                var decoded=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(Ledger(a with{Appearance=boat with{Passenger=egg}}),0),0);
                Assert(decoded.Units[0].Appearance!.Passenger!.GrowthDueTurn==9&&decoded.Units[0].Appearance!.Passenger!.Health==60);
                Invalid(()=>NativeIdentityLedgerCodec.EncodeLegacyFingerprint(Ledger(a),0));
                Invalid(()=>NativeIdentityLedgerCodec.EncodeTimedFingerprint(Ledger(a),0));
            });
            Test("NAV-004-legacy-schema1-and-schema2-receipts-survive-upgrade",new[]{"DEC-171","DEC-173"},()=> {
                foreach(bool timed in new[]{false,true}){
                    var appearance=Land(timed?"dragonegg":"warrior") with{Effects=Array.Empty<string>(),GrowthTurns=timed?1:-1,GrowthDueTurn=timed?9:-1};
                    var baseLedger=Ledger(Vessel() with{NativeId=12,Transport=null,Appearance=appearance});
                    byte[] fingerprint=NativeExchangeIntentCodec.AppearanceFingerprint(appearance);
                    var ledger=baseLedger with{NextSequence=2,Receipts=new[]{new LedgerReceipt(1,epoch,fingerprint,0,1,new ulong[]{1})}};
                    byte[] legacy=timed?NativeIdentityLedgerCodec.EncodeTimedFingerprint(ledger,1):NativeIdentityLedgerCodec.EncodeLegacyFingerprint(ledger,1);
                    Assert(BinaryPrimitives.ReadUInt16LittleEndian(legacy.AsSpan(4))==(timed?2:1));
                    var decoded=NativeIdentityLedgerCodec.Decode(legacy,1);var modern=NativeIdentityLedgerCodec.Encode(decoded,1);
                    Assert(BinaryPrimitives.ReadUInt16LittleEndian(modern.AsSpan(4))==9&&!modern.SequenceEqual(legacy));
                    var restored=NativeIdentityLedgerCodec.Decode(modern,1);
                    Assert(restored.Units[0].Transport is null&&restored.Units[0].Appearance!.Passenger is null);
                    Assert(restored.Receipts[0].Fingerprint.SequenceEqual(fingerprint)&&
                        NativeExchangeIntentCodec.AppearanceFingerprint(restored.Units[0].Appearance!).SequenceEqual(fingerprint));
                    Assert(restored.Units[0].Appearance!.GrowthDueTurn==(timed?9:-1));
                }
            });
            Test("NAV-005-transport-wire-truncation-and-boolean-fail-closed",new[]{"DEC-171","DEC-179"},()=> {
                var ledger=Ledger(Vessel());byte[] wire=NativeIdentityLedgerCodec.Encode(ledger,0);var untouched=wire.ToArray();
                for(int n=0;n<wire.Length;n++)Invalid(()=>NativeIdentityLedgerCodec.Decode(wire[..n],0));
                Invalid(()=>NativeIdentityLedgerCodec.Decode(wire.Concat(new byte[]{0}).ToArray(),0));
                // Cargo presence is followed by its uint ID, Rogue/acquisition
                // and species fields, three empty lists, then schema9's null contract.
                int cargo=wire.Length-22;
                Assert(wire[cargo]==1&&BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(cargo+1))==12&&wire[^1]==0,
                    "Transport Boolean corruption no longer targets the cargo field");
                foreach(byte invalid in new byte[]{2,255}){var bytes=wire.ToArray();bytes[cargo]=invalid;Invalid(()=>NativeIdentityLedgerCodec.Decode(bytes,0));}
                Assert(wire.SequenceEqual(untouched)&&NativeIdentityLedgerCodec.Encode(NativeIdentityLedgerCodec.Decode(wire,0),0).SequenceEqual(wire));
            });
            Test("NAV-006-fingerprint-covers-apparent-passenger-not-live-donor",new[]{"DEC-171","DEC-173"},()=> {
                var source=Vessel().Appearance!;byte[] expected=NativeExchangeIntentCodec.AppearanceFingerprint(source);
                foreach(var changed in new[]{source with{Passenger=source.Passenger! with{Health=249}},
                    source with{Passenger=source.Passenger! with{Form="warrior",Health=100,Maximum=100}},
                    source with{Passenger=source.Passenger! with{SourceColor=54321}},source with{Passenger=null}})
                    Assert(!NativeExchangeIntentCodec.AppearanceFingerprint(changed).SequenceEqual(expected));
                var clone=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(Ledger(Vessel()),0),0).Units[0].Appearance!;
                Assert(NativeExchangeIntentCodec.AppearanceFingerprint(clone).SequenceEqual(expected));
                source.Passenger!.Effects[0]="Poisoned";
                Assert(NativeExchangeIntentCodec.AppearanceFingerprint(clone).SequenceEqual(expected),"Saved passenger follows a donor array");
            });
            Test("NAV-007-amphibious-species-survives-wrapper-shed-and-receipt",new[]{"DEC-171","DEC-173"},()=>{
                var source=Vessel() with{AmphibiousMimic=true};
                var appearance=source.Appearance!;byte[] digest=NativeExchangeIntentCodec.AppearanceFingerprint(appearance);
                var receipt=new LedgerReceipt(1,epoch,digest,0,1,new ulong[]{source.LogicalId});
                var boarded=Ledger(source) with{NextSequence=2,Receipts=new[]{receipt}};
                var row=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(boarded,1),1).Units.Single();
                Assert(row.AmphibiousMimic&&row.Transport?.PassengerNativeId==12&&row.NativeId==40);
                var landed=boarded with{Units=new[]{row with{NativeId=12,Transport=null,Appearance=Land(AquarionCatalog.MimicId) with{Health=10,Maximum=10}}}};
                var saved=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(landed,1),1);
                Assert(saved.Units[0].AmphibiousMimic&&saved.Units[0].LogicalId==source.LogicalId&&saved.Units[0].PaidStars==3);
                Assert(saved.Units[0].Transport==null&&saved.Receipts[0].Fingerprint.SequenceEqual(digest),"Species transition invalidated old receipt");
            });
        }
    }
}
