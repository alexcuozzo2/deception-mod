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
        // Private payload contracts only. These tests do not contain a native
        // GameState, establish identity association, or execute an exchange.
        static void NativeIdentityLedgerTests()
        {
            NativeApparentGrowthTests();
            NativeLedgerTransportTests();
            NativeEspionageLedgerTests();
            NativeBlackmailLedgerTests();
            const long Revision=4;
            Guid Id(int value)=>new Guid(value,0,0,new byte[8]);
            byte[] Fingerprint(byte value)=>SHA256.HashData(new[]{value});
            LedgerAppearance Appearance()=>new("warrior",new("imperius","default"),new("imperius","default"),new("imperius","default"),0,73,100,true,
                ApparentConcealment.Visible,-1,Array.Empty<string>());
            LedgerUnit Unit(ulong logical=1,uint native=0)=>new(logical,native,true,LedgerActualKind.Mimic,
                true,Appearance(),PaidInvestmentKind.UnknownLegacy,0);
            NativeIdentityLedger Single()=>new(Id(1),2,1,new[]{Unit()},Array.Empty<LedgerReceipt>());
            NativeIdentityLedger Rich()=>new(Id(7),4,3,new[]{
                Unit() with {Appearance=Appearance() with {ClimateVisual=new("bardur","skin_b"),BirthVisual=new("oumaji","skin_c"),
                    SourceColor=int.MinValue,Effects=new[]{"frozen","poisoned"}}},
                Unit(2,55) with {Kind=LedgerActualKind.Genuine,InvestmentKind=PaidInvestmentKind.Known,
                    Appearance=Appearance() with {Health=100}},
                new LedgerUnit(3,55,false,LedgerActualKind.Mimic,false,null,PaidInvestmentKind.Known,3)
            },new[]{
                new LedgerReceipt(1,Id(11),Fingerprint(1),0,1,new ulong[]{1,2}),
                // Private revisions may include a non-command change between receipts.
                new LedgerReceipt(2,Id(12),Fingerprint(2),3,4,new ulong[]{2,3})
            });
            void RejectData(Action action,string context)
            {
                bool rejected=false;
                try{action();}
                catch(InvalidDataException){rejected=true;}
                Assert(rejected,"Expected InvalidDataException: "+context);
            }
            void RejectLedger(NativeIdentityLedger ledger,long revision=Revision,string context="ledger")=>
                RejectData(()=>NativeIdentityLedgerCodec.Encode(ledger,revision),context);
            void RejectWire(byte[] wire,long revision=Revision,string context="wire")
            {
                var untouched=wire.ToArray();var previous=Single();var published=previous;
                RejectData(()=>published=NativeIdentityLedgerCodec.Decode(wire,revision),context);
                Assert(ReferenceEquals(previous,published),"Invalid decode published a ledger: "+context);
                Assert(wire.SequenceEqual(untouched),"Invalid decode changed input: "+context);
            }
            byte[] Change(Action<byte[]> mutation)
            {var wire=NativeIdentityLedgerCodec.Encode(Single(),Revision);mutation(wire);return wire;}
            NativeIdentityLedger WithRow(LedgerUnit row)=>Single() with {Units=new[]{row}};
            NativeIdentityLedger WithReceipt(LedgerReceipt receipt)=>Rich() with {
                NextSequence=2,Receipts=new[]{receipt}};
            LedgerUnit Genuine(ulong id)=>new(id,checked((uint)id),true,LedgerActualKind.Genuine,
                false,null,PaidInvestmentKind.UnknownLegacy,0);

            Test("A31-001-amphibious-body-species-roundtrips-separately-from-mask",new[]{"DEC-171","A30"},()=>{
                var a=WithRow(Unit() with{AmphibiousMimic=true});
                byte[] old=NativeIdentityLedgerCodec.Encode(Single(),Revision),wire=NativeIdentityLedgerCodec.Encode(a,Revision);
                var row=NativeIdentityLedgerCodec.Decode(wire,Revision).Units.Single();
                Assert(row.AmphibiousMimic&&row.Appearance!.Form=="warrior"&&row.Kind==LedgerActualKind.Mimic);
                Assert(!wire.SequenceEqual(old)&&NativeIdentityLedgerCodec.Encode(NativeIdentityLedgerCodec.Decode(wire,Revision),Revision).SequenceEqual(wire));
                Assert(NativeExchangeIntentCodec.AppearanceFingerprint(row.Appearance).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(Appearance())),"Private body species changed public mask fingerprint");
            });
            Test("A31-002-only-mimic-identities-may-carry-amphibious-species",new[]{"DEC-171"},()=>{
                RejectLedger(WithRow(Genuine(1) with{AmphibiousMimic=true}));
                RejectLedger(WithRow(Unit() with{Kind=LedgerActualKind.Genuine,AmphibiousMimic=true}));
                var dead=Unit() with{AmphibiousMimic=true,Alive=false,Independent=false,Appearance=null};
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(WithRow(dead),Revision),Revision).Units[0].AmphibiousMimic,"Historical body species was erased");
            });
            Test("A31-005-water-mode-roundtrip-is-independent-of-cargo-and-mask",new[]{"DEC-171"},()=>{
                var row=Unit() with{MimicWaterForm=true};var wire=NativeIdentityLedgerCodec.Encode(WithRow(row),Revision);
                var decoded=NativeIdentityLedgerCodec.Decode(wire,Revision).Units[0];
                Assert(decoded.MimicWaterForm&&decoded.Transport is null&&!decoded.AmphibiousMimic);
                Assert(NativeIdentityLedgerCodec.Encode(NativeIdentityLedgerCodec.Decode(wire,Revision),Revision).SequenceEqual(wire));
                Assert(NativeExchangeIntentCodec.AppearanceFingerprint(decoded.Appearance!).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(Appearance())));
                RejectLedger(WithRow(row with{AmphibiousMimic=true}));RejectLedger(WithRow(Genuine(1) with{MimicWaterForm=true}));
                RejectData(()=>NativeIdentityLedgerCodec.EncodeLegacyFingerprint(WithRow(row),Revision),"Legacy encoder erased boat mode");
            });
            void LegacySpecies(ushort version)
            {
                // Independent old wire: one masked Mimic and no history. Do
                // not use the current encoder to manufacture legacy bytes.
                using var stream=new MemoryStream();using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
                {
                    void Text(string value){w.Write((byte)value.Length);w.Write(System.Text.Encoding.ASCII.GetBytes(value));}
                    w.Write(0x4c504344u);w.Write(version);w.Write((ushort)0);w.Write(Id(1).ToByteArray());w.Write(2ul);w.Write(1ul);w.Write((ushort)1);w.Write((ushort)0);
                    w.Write(1ul);w.Write(7u);w.Write(true);w.Write((byte)LedgerActualKind.Mimic);w.Write(true);w.Write((byte)PaidInvestmentKind.Known);w.Write(3);
                    Text("warrior");for(int n=0;n<3;n++){Text("imperius");Text("default");}w.Write(0);w.Write((ushort)73);w.Write((ushort)100);w.Write(false);w.Write((byte)0);w.Write(-1);w.Write((byte)0);
                    if(version>=2)w.Write(-1L);if(version>=3){w.Write(false);w.Write(false);}if(version>=4){w.Write(false);w.Write((byte)0);w.Write(0);w.Write(0);}if(version>=5)w.Write(0);
                }
                var migrated=NativeIdentityLedgerCodec.Decode(stream.ToArray(),Revision);
                Assert(!migrated.Units[0].AmphibiousMimic&&!migrated.Units[0].MimicWaterForm&&migrated.Units[0].NativeId==7&&migrated.Units[0].PaidStars==3);
                var current=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(migrated,Revision),Revision);
                Assert(!current.Units[0].AmphibiousMimic&&!current.Units[0].MimicWaterForm&&current.Units[0].Appearance!.Health==73);
            }
            Test("A31-003-legacy-species-default-false-schema-1",new[]{"DEC-171","DEC-179"},()=>LegacySpecies(1));
            Test("A31-003-legacy-species-default-false-schema-2",new[]{"DEC-171","DEC-179"},()=>LegacySpecies(2));
            Test("A31-003-legacy-species-default-false-schema-3",new[]{"DEC-171","DEC-179"},()=>LegacySpecies(3));
            Test("A31-003-legacy-species-default-false-schema-4",new[]{"DEC-171","DEC-179"},()=>LegacySpecies(4));
            Test("A31-003-legacy-species-default-false-schema-5",new[]{"DEC-171","DEC-179"},()=>LegacySpecies(5));
            Test("A31-003-legacy-species-default-false-schema-6",new[]{"DEC-171","DEC-179"},()=>LegacySpecies(6));
            Test("A31-004-new-body-species-cannot-be-lost-through-legacy-encoders",new[]{"DEC-171"},()=>{
                var l=WithRow(Unit() with{AmphibiousMimic=true});
                RejectData(()=>NativeIdentityLedgerCodec.EncodeLegacyFingerprint(l,Revision),"schema1 species loss");
                RejectData(()=>NativeIdentityLedgerCodec.EncodeTimedFingerprint(l,Revision),"schema2 species loss");
                RejectData(()=>NativeIdentityLedgerCodec.EncodeTransportFingerprint(l,Revision),"schema3 species loss");
                // Appearance digests intentionally contain a constant dummy
                // identity; new amphibious operations can still fingerprint.
                Assert(NativeExchangeIntentCodec.AppearanceFingerprint(l.Units[0].Appearance!).Length==32);
            });

            Test("M2-020-native-ledger-canonical-roundtrip-and-owned-decode",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                var source=Rich();var encoded=NativeIdentityLedgerCodec.Encode(source,Revision);
                var before=encoded.ToArray();
                Assert(BinaryPrimitives.ReadUInt32LittleEndian(encoded)==0x4C504344u&&
                    BinaryPrimitives.ReadUInt16LittleEndian(encoded.AsSpan(4))==9&&
                    BinaryPrimitives.ReadUInt16LittleEndian(encoded.AsSpan(6))==0,"Private schema wire prefix changed");
                var first=NativeIdentityLedgerCodec.Decode(encoded,Revision);
                var second=NativeIdentityLedgerCodec.Decode(encoded,Revision);
                Assert(first.Epoch==Id(7)&&first.NextLogicalId==4&&first.NextSequence==3);
                Assert(first.Units[0].NativeId==0&&first.Units[0].Appearance!.Health==73&&
                    first.Units[0].Appearance!.Maximum==100,"Native-scaled apparent wounds or ID0 changed");
                Assert(first.Units[0].Appearance!.TribeVisual==new LedgerVisualProfile("imperius","default")&&
                    first.Units[0].Appearance!.ClimateVisual==new LedgerVisualProfile("bardur","skin_b")&&
                    first.Units[0].Appearance!.BirthVisual==new LedgerVisualProfile("oumaji","skin_c")&&
                    first.Units[0].Appearance!.SourceColor==int.MinValue,"Distinct native visual profiles or signed color collapsed");
                Assert(NativeIdentityLedgerCodec.Encode(first,Revision).SequenceEqual(encoded)&&
                    NativeIdentityLedgerCodec.Encode(second,Revision).SequenceEqual(encoded),"Noncanonical roundtrip");
                Assert(!ReferenceEquals(first.Units,second.Units)&&!ReferenceEquals(first.Receipts,second.Receipts)&&
                    !ReferenceEquals(first.Units[0].Appearance!.Effects,second.Units[0].Appearance!.Effects)&&
                    !ReferenceEquals(first.Receipts[0].Fingerprint,second.Receipts[0].Fingerprint)&&
                    !ReferenceEquals(first.Receipts[0].Affected,second.Receipts[0].Affected),"Decoded arrays alias");
                first.Units[0].Appearance!.Effects[0]="modified";
                first.Receipts[0].Fingerprint[0]^=0x80;first.Receipts[0].Affected[0]=3;
                first.Units[1]=first.Units[1] with {PaidStars=99};
                source.Units[0].Appearance!.Effects[0]="caller_change";
                source.Receipts[0].Fingerprint[0]^=1;source.Receipts[0].Affected[0]=3;
                Assert(encoded.SequenceEqual(before)&&NativeIdentityLedgerCodec.Encode(second,Revision).SequenceEqual(before),
                    "Decoded result shares caller/other-result ownership");
                encoded[0]^=1;
                Assert(NativeIdentityLedgerCodec.Encode(second,Revision).SequenceEqual(before),"Decode retained its input buffer");
            });

            Test("M2-021-native-ledger-every-truncation-and-trailing-byte-rejected",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                byte[] good=NativeIdentityLedgerCodec.Encode(Rich(),Revision);
                for(int length=0;length<good.Length;length++)RejectWire(good[..length],context:"prefix "+length);
                RejectWire(good.Concat(new byte[]{0}).ToArray(),context:"trailing zero");
                RejectWire(good.Concat(good).ToArray(),context:"concatenated independent ledger");
                Assert(NativeIdentityLedgerCodec.Encode(NativeIdentityLedgerCodec.Decode(good,Revision),Revision).SequenceEqual(good),
                    "Malformed reads contaminated later valid decode");
            });

            Test("M2-022-native-ledger-strict-header-enums-and-booleans",new[]{"DEC-171","DEC-179"},()=> {
                RejectWire(Change(b=>b[0]^=1),context:"magic");
                foreach(ushort schema in new ushort[]{0,10,ushort.MaxValue})
                    RejectWire(Change(b=>BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(4),schema)),context:"schema");
                RejectWire(Change(b=>b[6]=1),context:"reserved flags");
                RejectWire(Change(b=>Array.Clear(b,8,16)),context:"empty epoch");
                // Schema9 preserves the schema1 prefix: header44; fixed unit20; form plus
                // THREE imperius/default pairs; color123, health127/max129,
                // veteran131/concealment132/growth133/effect count137; due138; passenger146/transport147;
                // amphibious150/water151; three empty list counts152..163; null online contract164.
                var singleWire=NativeIdentityLedgerCodec.Encode(Single(),Revision);
                Assert(singleWire.Length==165&&singleWire[164]==0,"Wire fixture layout changed");
                foreach(int offset in new[]{56,58,131,146,147,150,151,164})
                    foreach(byte value in new byte[]{2,255})RejectWire(Change(b=>b[offset]=value),context:"Boolean@"+offset);
                foreach(int offset in new[]{57,59,132})
                    foreach(byte value in new byte[]{2,255})RejectWire(Change(b=>b[offset]=value),context:"enum@"+offset);
                RejectLedger(WithRow(Unit() with {Kind=(LedgerActualKind)255}),context:"source actual enum");
                RejectLedger(WithRow(Unit() with {InvestmentKind=(PaidInvestmentKind)255}),context:"source investment enum");
                RejectLedger(WithRow(Unit() with {Appearance=Appearance() with {Concealment=(ApparentConcealment)255}}),context:"source concealment enum");
            });

            Test("M2-023-native-ledger-strict-profile-text-and-effects",new[]{"DEC-171","DEC-179"},()=> {
                foreach(int offset in new[]{64,72,81,89,98,106,115})
                    foreach(byte length in new byte[]{0,65,255})RejectWire(Change(b=>b[offset]=length),context:"text length");
                foreach(byte value in new byte[]{0,32,47,128,255})
                    RejectWire(Change(b=>b[65]=value),context:"profile character");
                foreach(string value in new[]{"",new string('a',65),"white space","slash/name","é","nul\0byte"})
                {
                    RejectLedger(WithRow(Unit() with {Appearance=Appearance() with {Form=value}}),context:"form identifier");
                    foreach(var appearance in new[]{
                        Appearance() with {TribeVisual=new(value,"default")},Appearance() with {TribeVisual=new("imperius",value)},
                        Appearance() with {ClimateVisual=new(value,"default")},Appearance() with {ClimateVisual=new("imperius",value)},
                        Appearance() with {BirthVisual=new(value,"default")},Appearance() with {BirthVisual=new("imperius",value)}})
                        RejectLedger(WithRow(Unit() with {Appearance=appearance}),context:"visual profile identifier");
                }
                foreach(var appearance in new[]{Appearance() with {TribeVisual=null!},Appearance() with {ClimateVisual=null!},Appearance() with {BirthVisual=null!}})
                    RejectLedger(WithRow(Unit() with {Appearance=appearance}),context:"null visual profile");
                string maximum="Aa09_.-"+new string('z',57);
                var valid=WithRow(Unit() with {Appearance=Appearance() with {Form=maximum,TribeVisual=new(maximum,maximum),
                    ClimateVisual=new(maximum,maximum),BirthVisual=new(maximum,maximum)}});
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(valid,Revision),Revision).Units[0].Appearance!.Form==maximum);
                foreach(string[] effects in new[]{new[]{"poisoned","frozen"},new[]{"frozen","frozen"},new[]{"bad/name"},new[]{""}})
                    RejectLedger(WithRow(Unit() with {Appearance=Appearance() with {Effects=effects}}),context:"noncanonical effects");
                RejectLedger(WithRow(Unit() with {Appearance=Appearance() with {Effects=null!}}),context:"null effects");
                RejectWire(Change(b=>b[137]=17),context:"wire effects count");
                RejectLedger(WithRow(Unit() with {Appearance=Appearance() with {
                    Effects=Enumerable.Range(0,17).Select(i=>"e"+i.ToString("D2")).ToArray()}}),context:"source effects count");
                var most=WithRow(Unit() with {Appearance=Appearance() with {
                    Effects=Enumerable.Range(0,16).Select(i=>"e"+i.ToString("D2")).ToArray()}});
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(most,Revision),Revision).Units[0].Appearance!.Effects.Length==16);
            });

            Test("M2-024-native-ledger-count-and-total-byte-bounds",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                RejectWire(Change(b=>BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(40),8193)),context:"wire unit count");
                RejectWire(Change(b=>BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(42),2049)),context:"wire receipt count");
                var maximum=Single() with {NextLogicalId=8193,Units=Enumerable.Range(1,8192).Select(i=>Genuine((ulong)i)).ToArray()};
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(maximum,Revision),Revision).Units.Length==8192);
                RejectLedger(maximum with {NextLogicalId=8194,Units=maximum.Units.Append(Genuine(8193)).ToArray()},context:"source unit count");
                LedgerReceipt Receipt(int i)=>new((ulong)i,Id(i),Fingerprint(1),i-1,i,new ulong[]{1});
                var history=Single() with {NextSequence=2049,Receipts=Enumerable.Range(1,2048).Select(Receipt).ToArray()};
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(history,2048),2048).Receipts.Length==2048);
                RejectLedger(history with {NextSequence=2050,Receipts=history.Receipts.Append(Receipt(2049)).ToArray()},2049,"source receipt count");
                RejectLedger(Single() with {Units=null!},context:"null units");
                RejectLedger(Single() with {Receipts=null!},context:"null receipts");
                RejectWire(new byte[NativeStateFrame.MaxExtensionBytes+1],context:"input byte bound");
                var profile=new LedgerVisualProfile(new string('b',64),new string('c',64));
                var largeAppearance=Appearance() with {Form=new string('a',64),TribeVisual=profile,ClimateVisual=profile,BirthVisual=profile,
                    Effects=Enumerable.Range(0,16).Select(i=>i.ToString("D2")+new string('e',62)).ToArray()};
                var oversized=Single() with {NextLogicalId=1025,Units=Enumerable.Range(1,1024)
                    .Select(i=>Unit((ulong)i,(uint)i) with {Appearance=largeAppearance}).ToArray()};
                RejectLedger(oversized,context:"encoded total byte bound");
            });

            Test("M2-025-native-ledger-logical-and-live-native-identities",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                var good=Rich();
                RejectLedger(Single() with {NextLogicalId=3,Units=new[]{Unit(2,2),Unit(1,1)}},context:"unit order");
                RejectLedger(Single() with {Units=new[]{Unit(1,1),Unit(1,2)}},context:"duplicate logical ID");
                RejectLedger(WithRow(Unit(0)),context:"zero logical ID");
                RejectLedger(WithRow(Unit(2)),context:"unallocated logical ID");
                RejectLedger(Single() with {NextLogicalId=3,Units=new[]{Unit(1,0),Unit(2,0)}},context:"duplicate live native ID");
                RejectLedger(Single() with {Units=new LedgerUnit[]{null!}},context:"null unit row");
                // A tombstone's historical native ID does not claim a living body.
                var restored=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(good,Revision),Revision);
                Assert(restored.Units[0].NativeId==0&&restored.Units[1].NativeId==restored.Units[2].NativeId&&
                    restored.Units[1].Alive&&!restored.Units[2].Alive,"Historical/native0 identity policy changed");
            });

            Test("M2-026-native-ledger-tombstone-and-equal-type-independence",new[]{"DEC-013","DEC-171","DEC-179"},()=> {
                var genuine=Unit() with {Kind=LedgerActualKind.Genuine,Appearance=Appearance() with {Health=100}};
                var decoded=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(WithRow(genuine),Revision),Revision).Units[0];
                Assert(decoded.Kind==LedgerActualKind.Genuine&&decoded.Independent&&decoded.Appearance!.Form=="warrior"&&
                    decoded.Appearance.Health==decoded.Appearance.Maximum,"Same-type/full-HP appearance was collapsed");
                RejectLedger(WithRow(Unit() with {Independent=false,Appearance=null}),context:"living bare Mimic");
                RejectLedger(WithRow(genuine with {Independent=false}),context:"payload without independent flag");
                RejectLedger(WithRow(genuine with {Appearance=null}),context:"independent flag without payload");
                RejectLedger(WithRow(Unit() with {Alive=false}),context:"tombstone appearance");
                var dead=WithRow(Unit() with {Alive=false,Independent=false,Appearance=null});
                var tombstone=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(dead,Revision),Revision).Units[0];
                Assert(!tombstone.Alive&&!tombstone.Independent&&tombstone.Appearance is null&&tombstone.LogicalId==1);
                var revealed=WithRow(genuine with {Independent=false,Appearance=null});
                Assert(!NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(revealed,Revision),Revision).Units[0].Independent,
                    "Genuine identity/history row must survive reveal without inventing appearance");
            });

            Test("M2-027-native-ledger-unknown-investment-is-not-known-zero",new[]{"DEC-171","DEC-179"},()=> {
                var unknown=Single();var zero=WithRow(Unit() with {InvestmentKind=PaidInvestmentKind.Known});
                var three=WithRow(Unit() with {InvestmentKind=PaidInvestmentKind.Known,PaidStars=3});
                byte[][] wire=new[]{unknown,zero,three}.Select(l=>NativeIdentityLedgerCodec.Encode(l,Revision)).ToArray();
                Assert(!wire[0].SequenceEqual(wire[1])&&!wire[1].SequenceEqual(wire[2]));
                var values=wire.Select(b=>NativeIdentityLedgerCodec.Decode(b,Revision).Units[0]).ToArray();
                Assert(values[0].InvestmentKind==PaidInvestmentKind.UnknownLegacy&&values[0].PaidStars==0&&
                    values[1].InvestmentKind==PaidInvestmentKind.Known&&values[1].PaidStars==0&&
                    values[2].InvestmentKind==PaidInvestmentKind.Known&&values[2].PaidStars==3);
                RejectLedger(WithRow(Unit() with {PaidStars=3}),context:"unknown positive investment");
                RejectLedger(WithRow(Unit() with {InvestmentKind=PaidInvestmentKind.Known,PaidStars=-1}),context:"negative known investment");
                RejectLedger(WithRow(Unit() with {PaidStars=-1}),context:"negative unknown investment");
                var largest=WithRow(Unit() with {InvestmentKind=PaidInvestmentKind.Known,PaidStars=int.MaxValue});
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(largest,Revision),Revision).Units[0].PaidStars==int.MaxValue);
            });

            Test("M2-028-native-ledger-apparent-health-and-growth-bounds",new[]{"DEC-022","DEC-171","DEC-179"},()=> {
                foreach(var appearance in new[]{Appearance() with {Health=0},Appearance() with {Maximum=0},
                    Appearance() with {Health=101},Appearance() with {GrowthTurns=-2},Appearance() with {GrowthTurns=65536},
                    Appearance() with {GrowthTurns=0},Appearance() with {GrowthDueTurn=0},
                    Appearance() with {GrowthTurns=1,GrowthDueTurn=-2},Appearance() with {GrowthTurns=1,GrowthDueTurn=(long)uint.MaxValue+1}})
                    RejectLedger(WithRow(Unit() with {Appearance=appearance}),context:"appearance numeric bound");
                foreach(int growth in new[]{-1,0,65535})
                {
                    var largest=WithRow(Unit() with {Appearance=Appearance() with {Health=ushort.MaxValue,Maximum=ushort.MaxValue,
                        GrowthTurns=growth,GrowthDueTurn=growth==-1?-1:uint.MaxValue,SourceColor=int.MaxValue,Concealment=ApparentConcealment.Hidden}});
                    var appearance=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(largest,Revision),Revision).Units[0].Appearance!;
                    Assert(appearance.Health==ushort.MaxValue&&appearance.GrowthTurns==growth&&appearance.SourceColor==int.MaxValue&&
                        appearance.Concealment==ApparentConcealment.Hidden);
                }
                RejectWire(Change(b=>BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(127),0)),context:"zero wire health");
                RejectWire(Change(b=>BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(133),int.MinValue)),context:"wire growth underflow");
                RejectWire(Change(b=>BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(138),0)),context:"wire deadline without growth");
            });

            Test("GROW-009-schema1-migrates-only-untimed-appearances",new[]{"DEC-089","DEC-090","DEC-171"},()=> {
                // Independently retained schema1 wire, not produced by the new encoder.
                byte[] legacy=Convert.FromHexString("4443504c0100000001000000000000000000000000000000020000000000000001000000000000000100000001000000000000000000000001010100000000000777617272696f7208696d7065726975730764656661756c7408696d7065726975730764656661756c7408696d7065726975730764656661756c7400000000490064000100ffffffff00");
                var migrated=NativeIdentityLedgerCodec.Decode(legacy,Revision);var a=migrated.Units.Single().Appearance!;
                Assert(a.GrowthTurns==-1&&a.GrowthDueTurn==-1&&a.Form=="warrior"&&a.Health==73&&migrated.NextSequence==1);
                Assert(NativeIdentityLedgerCodec.Encode(migrated,Revision).SequenceEqual(NativeIdentityLedgerCodec.Encode(Single(),Revision)));
                Assert(NativeIdentityLedgerCodec.EncodeLegacyFingerprint(migrated,Revision).SequenceEqual(legacy));
                foreach(int remaining in new[]{0,1,3,65535}){
                    var timed=legacy.ToArray();BinaryPrimitives.WriteInt32LittleEndian(timed.AsSpan(133),remaining);
                    RejectWire(timed,context:"schema1 cannot invent growth deadline");
                }
                for(int n=0;n<legacy.Length;n++)RejectWire(legacy[..n],context:"legacy truncated"+n);
                RejectWire(legacy.Concat(new byte[]{0}).ToArray(),context:"legacy trailing byte");
            });
            Test("GROW-010-schema2-timed-roundtrip-and-deadline-bounds",new[]{"DEC-089","DEC-090","DEC-171"},()=> {
                var a=Appearance() with{Form="dragonegg",GrowthTurns=1,GrowthDueTurn=40,Veteran=false};
                var timed=WithRow(Unit() with{Appearance=a});var bytes=NativeIdentityLedgerCodec.EncodeTimedFingerprint(timed,Revision);
                Assert(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4))==2);
                var reloaded=NativeIdentityLedgerCodec.Decode(bytes,Revision).Units.Single().Appearance!;
                Assert(reloaded==a with{Effects=reloaded.Effects}&&reloaded.GrowthDueTurn==40);
                Assert(NativeIdentityLedgerCodec.EncodeTimedFingerprint(NativeIdentityLedgerCodec.Decode(bytes,Revision),Revision).SequenceEqual(bytes));
                RejectData(()=>NativeIdentityLedgerCodec.EncodeLegacyFingerprint(timed,Revision),"timed legacy digest");
                foreach(long due in new[]{0L,1L,(long)uint.MaxValue}){
                    var b=NativeIdentityLedgerCodec.Encode(WithRow(Unit() with{Appearance=a with{GrowthDueTurn=due}}),Revision);
                    Assert(NativeIdentityLedgerCodec.Decode(b,Revision).Units[0].Appearance!.GrowthDueTurn==due);
                }
            });

            Test("M2-029-native-ledger-receipt-fingerprint-and-operation-identity",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                var receipt=Rich().Receipts[0];
                foreach(int count in new[]{0,31,33})RejectLedger(WithReceipt(receipt with {Fingerprint=new byte[count]}),context:"fingerprint length");
                RejectLedger(WithReceipt(receipt with {Fingerprint=null!}),context:"null fingerprint");
                RejectLedger(WithReceipt(receipt with {Operation=Guid.Empty}),context:"empty operation");
                var rich=Rich();RejectLedger(rich with {Receipts=new[]{rich.Receipts[0],rich.Receipts[1] with {Operation=rich.Receipts[0].Operation}}},
                    context:"duplicate operation");
                RejectLedger(Single() with {NextSequence=2,Receipts=new LedgerReceipt[]{null!}},context:"null receipt");
                var changed=WithReceipt(receipt with {Fingerprint=Fingerprint(99)});
                Assert(!NativeIdentityLedgerCodec.Encode(WithReceipt(receipt),Revision).SequenceEqual(
                    NativeIdentityLedgerCodec.Encode(changed,Revision)),"Opaque fingerprint value collapsed");
                // The codec validates representation, not whether this digest
                // matches a native command. That belongs to semantic validation.
            });

            Test("M2-030-native-ledger-receipt-chain-and-affected-identities",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                var rich=Rich();var first=rich.Receipts[0];var second=rich.Receipts[1];
                foreach(ulong sequence in new ulong[]{0,2,ulong.MaxValue})
                    RejectLedger(WithReceipt(first with {Sequence=sequence}),context:"receipt sequence");
                RejectLedger(rich with {NextSequence=4},context:"missing receipt tail");
                RejectLedger(rich with {NextSequence=2},context:"unallocated receipt");
                RejectLedger(rich with {Receipts=new[]{first,second with {BeforeRevision=0,AfterRevision=1}}},context:"overlapping revision chain");
                foreach(var receipt in new[]{first with {BeforeRevision=-1,AfterRevision=0},first with {AfterRevision=0},
                    first with {AfterRevision=2},first with {BeforeRevision=4,AfterRevision=5}})
                    RejectLedger(WithReceipt(receipt),context:"invalid revision step");
                foreach(ulong[] affected in new[]{Array.Empty<ulong>(),new ulong[]{2,1},new ulong[]{1,1},new ulong[]{0},new ulong[]{4}})
                    RejectLedger(WithReceipt(first with {Affected=affected}),context:"affected identities");
                RejectLedger(WithReceipt(first with {Affected=null!}),context:"null affected IDs");
                var many=Single() with {NextLogicalId=1025,NextSequence=2,
                    Units=Enumerable.Range(1,1024).Select(i=>Genuine((ulong)i)).ToArray(),
                    Receipts=new[]{first with {Affected=Enumerable.Range(1,1024).Select(i=>(ulong)i).ToArray()}}};
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(many,Revision),Revision).Receipts[0].Affected.Length==1024);
                RejectLedger(many with {NextLogicalId=1026,Units=many.Units.Append(Genuine(1025)).ToArray(),
                    Receipts=new[]{first with {Affected=Enumerable.Range(1,1025).Select(i=>(ulong)i).ToArray()}}},context:"affected count");
                // One row without appearance fixes the schema9 receipt start at69:
                // header44 + fixed unit20 + transport/Rogue/acquisitions/species5.
                var compact=Single() with {NextSequence=2,Units=new[]{Genuine(1)},Receipts=new[]{first with {Affected=new ulong[]{1}}}};
                var wire=NativeIdentityLedgerCodec.Encode(compact,Revision);
                Assert(BinaryPrimitives.ReadUInt16LittleEndian(wire.AsSpan(141))==1,
                    "Receipt affected-count corruption no longer targets its count field");
                foreach(ushort count in new ushort[]{0,1025,ushort.MaxValue})
                {
                    var invalid=wire.ToArray();BinaryPrimitives.WriteUInt16LittleEndian(invalid.AsSpan(141),count);
                    RejectWire(invalid,context:"wire affected count");
                }
            });

            Test("M2-031-native-ledger-id-revision-and-overflow-boundaries",new[]{"DEC-171","DEC-173","DEC-179"},()=> {
                RejectLedger(Single(),-1,"negative revision");
                RejectLedger(Single() with {Epoch=Guid.Empty},context:"empty epoch");
                RejectLedger(Single() with {NextLogicalId=0},context:"wrapped logical allocator");
                RejectLedger(Single() with {NextSequence=0},context:"wrapped receipt allocator");
                RejectLedger(Single() with {NextSequence=ulong.MaxValue},context:"missing history cannot masquerade as overflow");
                var highest=Single() with {NextLogicalId=ulong.MaxValue,Units=new[]{Unit(ulong.MaxValue-1,uint.MaxValue)}};
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(highest,long.MaxValue),long.MaxValue).Units[0].LogicalId==ulong.MaxValue-1);
                RejectLedger(highest with {Units=new[]{Unit(ulong.MaxValue,uint.MaxValue)}},context:"unallocated maximum logical ID");
                var last=Rich().Receipts[0] with {BeforeRevision=long.MaxValue-1,AfterRevision=long.MaxValue};
                var accepted=WithReceipt(last);
                Assert(NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(accepted,long.MaxValue),long.MaxValue).Receipts[0].AfterRevision==long.MaxValue);
                RejectLedger(WithReceipt(last with {BeforeRevision=long.MaxValue,AfterRevision=long.MinValue}),long.MaxValue,"revision overflow");
                var empty=new NativeIdentityLedger(Id(20),1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>());
                Assert(NativeIdentityLedgerCodec.Encode(empty,0).Length==57,"Empty epoch header with null online contract changed");
                NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(empty,0),0);
            });
        }
    }
}
