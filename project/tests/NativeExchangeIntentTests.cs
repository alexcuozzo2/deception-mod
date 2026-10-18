using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        // Serialized intent/receipt and synthetic registry contracts only.
        // Native readiness, authority, movement, mutation and replay execution
        // remain adapter tests; a valid digest grants no command permission.
        static void NativeExchangeIntentTests()
        {
            var epoch=new Guid("00112233-4455-6677-8899-aabbccddeeff");
            LedgerAppearance Appearance()=>new("warrior",new("imperius","default"),new("bardur","skin_b"),
                new("oumaji","skin_c"),0x123456,73,100,true,ApparentConcealment.Visible,-1,new[]{"frozen","poisoned"});
            NativeExchangeIntent Intent()=>new(epoch,1,7,0xa0b0c0d0u,0xab,NativeExchangeKind.Regroup,false,
                NativePartnerReference.Existing,3,new(1,0,-2,3),new(2,uint.MaxValue,4,-5),
                Enumerable.Range(0,32).Select(x=>(byte)x).ToArray());
            NativeIdentityLedger Ledger()=>new(epoch,3,1,new[]{
                new LedgerUnit(1,0,true,LedgerActualKind.Mimic,true,Appearance(),PaidInvestmentKind.UnknownLegacy,0),
                new LedgerUnit(2,uint.MaxValue,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,3)
            },Array.Empty<LedgerReceipt>());
            LedgerReceipt Receipt(NativeExchangeIntent intent)=>new(intent.Sequence,NativeExchangeIntentCodec.Operation(intent.Sequence),
                NativeExchangeIntentCodec.Fingerprint(intent),intent.Revision,checked(intent.Revision+1),
                new[]{intent.Initiator.LogicalId,intent.Partner.LogicalId}.Distinct().OrderBy(x=>x).ToArray());
            NativeIdentityLedger Committed()=>Ledger() with {NextSequence=2,Receipts=new[]{Receipt(Intent())}};
            void RejectData(Action action,string context)
            {
                bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}
                Assert(rejected,"Expected InvalidDataException: "+context);
            }
            void RejectIntent(NativeExchangeIntent value,string context)=>RejectData(()=>NativeExchangeIntentCodec.Encode(value),context);
            void RejectWire(byte[] wire,string context)
            {
                var before=wire.ToArray();var previous=Intent();var published=previous;
                RejectData(()=>published=NativeExchangeIntentCodec.Decode(wire),context);
                Assert(ReferenceEquals(previous,published),"Invalid decode published a replacement: "+context);
                Assert(before.SequenceEqual(wire),"Invalid decode changed its input: "+context);
            }
            byte[] Change(Action<byte[]> mutation)
            {var bytes=NativeExchangeIntentCodec.Encode(Intent());mutation(bytes);return bytes;}
            JsonObject Registry()=>new() {
                ["improvementData"]=new JsonObject {
                    ["harvestfruit"]=new JsonObject {
                        ["idx"]=17,["hidden"]=false,["cost"]=29,["prefab"]="fruit-art",
                        ["improvementAbilities"]=new JsonArray("exhaust","spawn"),
                        ["creates"]=new JsonArray("fixture-unit"),["growthRewards"]=new JsonArray("population"),
                        ["growthRate"]=4,["terrainRequirements"]=new JsonArray(new JsonObject{["resource"]="fruit"}),
                        ["shouldSuggestUnlock"]=true,["unrelatedNativeField"]=new JsonObject{["preserve"]="exact"}
                    },["farm"]=new JsonObject{["cost"]=5,["prefab"]="farm-art"}
                },
                ["techData"]=new JsonObject{["organization"]=new JsonObject{["improvementUnlocks"]=new JsonArray("harvestfruit")}},
                ["tribeData"]=new JsonObject{["imperius"]=new JsonObject{["startingTech"]=new JsonArray("organization")}},
                ["unitData"]=new JsonObject{["warrior"]=new JsonObject{["health"]=100}}
            };

            Test("M2-040-exchange-intent-golden-wire-and-owned-roundtrip",new[]{"DEC-031","DEC-032","DEC-033","DEC-034","DEC-173"},()=> {
                // Published schema1 vector, independently fixed for wire compatibility.
                byte[] golden=Convert.FromHexString(
                    "44435058010000007b00000078000000"+
                    "33221100554477668899aabbccddeeff"+
                    "01000000000000000700000000000000d0c0b0a0ab000000"+
                    "0300000000000000010000000000000000000000feffffff03000000"+
                    "0200000000000000ffffffff04000000fbffffff"+
                    "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");
                Assert(golden.Length==136&&NativeExchangeIntentCodec.Encode(Intent()).SequenceEqual(golden),"Canonical exchange wire changed");
                Assert(Convert.ToHexString(NativeExchangeIntentCodec.Fingerprint(Intent())).ToLowerInvariant()==
                    "6c1bec438a3ea5075989aac95446151eb387beee9931f7a149563b8f0b1afda9","Canonical domain-separated fingerprint changed");
                foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})foreach(bool swap in new[]{false,true})
                {
                    var source=Intent() with {Kind=kind,Swap=swap};var encoded=NativeExchangeIntentCodec.Encode(source);var before=encoded.ToArray();
                    var a=NativeExchangeIntentCodec.Decode(encoded);var b=NativeExchangeIntentCodec.Decode(encoded);
                    Assert(a.Kind==kind&&a.Swap==swap&&a.Epoch==epoch&&a.Actor==0xab&&a.Turn==0xa0b0c0d0u);
                    Assert(a.Initiator==source.Initiator&&a.Partner==source.Partner&&a.Sequence==1&&a.Revision==7&&a.NextLogicalId==3);
                    Assert(!ReferenceEquals(a.SourceAppearanceHash,b.SourceAppearanceHash)&&!ReferenceEquals(a.SourceAppearanceHash,source.SourceAppearanceHash));
                    Assert(NativeExchangeIntentCodec.Encode(a).SequenceEqual(before),"Roundtrip changed identity or choice");
                    a.SourceAppearanceHash[0]^=1;source.SourceAppearanceHash[1]^=2;encoded[0]^=4;
                    Assert(NativeExchangeIntentCodec.Encode(b).SequenceEqual(before),"Decoded intent aliases caller bytes or another result");
                }
            });

            Test("NAV-UPGRADE-001-canonical-single-unit-intent-and-rejections",new[]{"DEC-173","DEC-179"},()=> {
                var kinds=new[]{NativeExchangeKind.UpgradeScout,NativeExchangeKind.UpgradeRammer,NativeExchangeKind.UpgradeBomber};
                string[] targets={"scoutship","rammership","bombership"};
                foreach(var row in kinds.Select((kind,n)=>(kind,n))) {
                    var source=Intent();source=source with {Kind=row.kind,Partner=source.Initiator};
                    var bytes=NativeExchangeIntentCodec.Encode(source);var decoded=NativeExchangeIntentCodec.Decode(bytes);
                    Assert(bytes.Length==136&&BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4))==1&&bytes[53]==row.n+2,"Upgrade altered legacy wire layout or enum values");
                    Assert(decoded.Partner==decoded.Initiator&&!decoded.Swap&&decoded.PartnerReference==NativePartnerReference.Existing);
                    Assert(decoded.Kind==row.kind&&NativeExchangeCatalog.UpgradeTarget(decoded.Kind)==targets[row.n]);
                    Assert(NativeExchangeIntentCodec.Encode(decoded).SequenceEqual(bytes),"Single-unit intent failed canonical roundtrip");
                    foreach(var wrong in new[]{source with {Swap=true},source with {PartnerReference=NativePartnerReference.Allocate},
                        source with {Partner=Intent().Partner},source with {Partner=source.Initiator with {X=source.Initiator.X+1}},
                        source with {Kind=NativeExchangeKind.Regroup},source with {Kind=NativeExchangeKind.Disguise},source with {Kind=(NativeExchangeKind)6}})
                        RejectIntent(wrong,"noncanonical single-unit upgrade");
                    foreach(var change in new Action<byte[]>[]{b=>b[54]=1,b=>b[55]=1,b=>b[53]=0,b=>b[53]=1,b=>b[53]=6,b=>b[96]^=1}) {
                        var invalid=bytes.ToArray();change(invalid);RejectWire(invalid,"changed single-unit choice/identity");
                    }
                }
                Assert(!NativeExchangeCatalog.IsUpgrade(NativeExchangeKind.Regroup)&&!NativeExchangeCatalog.IsUpgrade(NativeExchangeKind.Disguise));
                Reject(()=>NativeExchangeCatalog.UpgradeTarget(NativeExchangeKind.Regroup));
            });
            Test("NAV-UPGRADE-002-single-unit-receipt-retry-and-digest-binding",new[]{"DEC-173","DEC-179","DEC-180"},()=> {
                foreach(var kind in new[]{NativeExchangeKind.UpgradeScout,NativeExchangeKind.UpgradeRammer,NativeExchangeKind.UpgradeBomber}) {
                    var source=Intent();source=source with {Kind=kind,Partner=source.Initiator};
                    var ledger=Ledger();var before=NativeIdentityLedgerCodec.Encode(ledger,7);
                    Assert(!NativeExchangeIntentCodec.AlreadyCommitted(ledger,7,source),"Fresh upgrade was considered committed");
                    var receipt=Receipt(source);Assert(receipt.Affected.SequenceEqual(new ulong[]{1}),"Single-unit receipt duplicated its participant");
                    var committed=ledger with {NextSequence=2,Receipts=new[]{receipt}};
                    var saved=NativeIdentityLedgerCodec.Encode(committed,8);var loaded=NativeIdentityLedgerCodec.Decode(saved,8);
                    Assert(NativeExchangeIntentCodec.AlreadyCommitted(loaded,8,NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(source))),"Saved upgrade retry lost operation identity");
                    byte[] digest=source.SourceAppearanceHash.ToArray();digest[0]^=1;
                    RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(loaded,8,source with {SourceAppearanceHash=digest}),"changed apparent source digest reused a committed upgrade");
                    var other=kind==NativeExchangeKind.UpgradeScout?NativeExchangeKind.UpgradeRammer:NativeExchangeKind.UpgradeScout;
                    RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(loaded,8,source with {Kind=other}),"another upgrade target reused the same receipt");
                    var wrongAffected=committed with {Receipts=new[]{receipt with {Affected=new ulong[]{1,2}}}};
                    NativeIdentityLedgerCodec.Validate(wrongAffected,8);
                    RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(wrongAffected,8,source),"upgrade receipt claims another participant");
                    Assert(NativeIdentityLedgerCodec.Encode(ledger,7).SequenceEqual(before)&&NativeIdentityLedgerCodec.Encode(loaded,8).SequenceEqual(saved),"Intent checks changed ledger state");
                }
            });
            Test("M2-041-exchange-intent-every-truncation-and-trailing-byte-rejected",new[]{"DEC-173","DEC-179"},()=> {
                var good=NativeExchangeIntentCodec.Encode(Intent());
                for(int length=0;length<good.Length;length++)RejectWire(good[..length],"truncated at "+length);
                RejectWire(good.Append((byte)0).ToArray(),"trailing byte");
                RejectWire(good.Concat(good).ToArray(),"two concatenated intents");
                Assert(NativeExchangeIntentCodec.Encode(NativeExchangeIntentCodec.Decode(good)).SequenceEqual(good),"Malformed input contaminated the next decode");
            });
            Test("SHED-001-canonical-single-unit-wire-preserves-old-kinds",new[]{"DEC-173","DEC-179"},()=> {
                var source=Intent();source=source with {Kind=NativeExchangeKind.ShedDisguise,Partner=source.Initiator};
                var bytes=NativeExchangeIntentCodec.Encode(source);var decoded=NativeExchangeIntentCodec.Decode(bytes);
                Assert(bytes.Length==136&&BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4))==1&&bytes[53]==5);
                Assert(decoded.Kind==NativeExchangeKind.ShedDisguise&&decoded.Partner==decoded.Initiator&&!decoded.Swap&&decoded.PartnerReference==NativePartnerReference.Existing);
                Assert(NativeExchangeIntentCodec.Encode(decoded).SequenceEqual(bytes)&&NativeExchangeCatalog.IsSingle(decoded.Kind)&&!NativeExchangeCatalog.IsUpgrade(decoded.Kind));
                Assert(NativeExchangeCatalog.Id(decoded.Kind)=="dcp_exchange_shed_disguise");Reject(()=>NativeExchangeCatalog.UpgradeTarget(decoded.Kind));
                foreach(var wrong in new[]{source with {Swap=true},source with {PartnerReference=NativePartnerReference.Allocate},source with {Partner=Intent().Partner},source with {Partner=source.Initiator with {Y=4}},source with {Kind=(NativeExchangeKind)6}})
                    RejectIntent(wrong,"noncanonical Shed intent");
                foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise,NativeExchangeKind.UpgradeScout,NativeExchangeKind.UpgradeRammer,NativeExchangeKind.UpgradeBomber}) {
                    var prior=Intent() with {Kind=kind};if(NativeExchangeCatalog.IsUpgrade(kind))prior=prior with {Partner=prior.Initiator};
                    var old=NativeExchangeIntentCodec.Encode(prior);Assert(old[53]==(byte)kind&&NativeExchangeIntentCodec.Encode(NativeExchangeIntentCodec.Decode(old)).SequenceEqual(old));
                }
            });
            Test("SHED-002-single-unit-receipt-binds-source-and-operation",new[]{"DEC-173","DEC-179","DEC-180"},()=> {
                var source=Intent();source=source with {Kind=NativeExchangeKind.ShedDisguise,Partner=source.Initiator};
                var ledger=Ledger();var before=NativeIdentityLedgerCodec.Encode(ledger,7);
                Assert(!NativeExchangeIntentCodec.AlreadyCommitted(ledger,7,source));var receipt=Receipt(source);
                Assert(receipt.Affected.SequenceEqual(new ulong[]{1}));
                var saved=NativeIdentityLedgerCodec.Encode(ledger with {NextSequence=2,Receipts=new[]{receipt}},8);var loaded=NativeIdentityLedgerCodec.Decode(saved,8);
                Assert(NativeExchangeIntentCodec.AlreadyCommitted(loaded,8,NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(source))));
                byte[] digest=source.SourceAppearanceHash.ToArray();digest[0]^=1;
                RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(loaded,8,source with {SourceAppearanceHash=digest}),"Shed retry changed source disguise");
                RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(loaded,8,source with {Kind=NativeExchangeKind.UpgradeScout}),"Upgrade cannot reuse Shed operation");
                var forged=loaded with {Receipts=new[]{receipt with {Affected=new ulong[]{1,2}}}};
                RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(forged,8,source),"Shed receipt cannot claim a partner");
                Assert(NativeIdentityLedgerCodec.Encode(ledger,7).SequenceEqual(before)&&NativeIdentityLedgerCodec.Encode(loaded,8).SequenceEqual(saved));
            });

            Test("M2-042-exchange-intent-header-enums-and-choice-fail-closed",new[]{"DEC-173","DEC-179"},()=> {
                RejectWire(Change(b=>b[0]^=1),"magic");
                foreach(ushort schema in new ushort[]{0,2,ushort.MaxValue})RejectWire(Change(b=>BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(4),schema)),"schema");
                foreach(ushort flags in new ushort[]{1,256,ushort.MaxValue})RejectWire(Change(b=>BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(6),flags)),"flags");
                foreach(int version in new[]{-1,0,51,122,124,int.MaxValue})RejectWire(Change(b=>BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(8),version)),"native version");
                foreach(int length in new[]{int.MinValue,-1,0,119,121,136,int.MaxValue})RejectWire(Change(b=>BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(12),length)),"declared body length");
                // Fixed header16 + epoch16 + sequence8 + revision8 + turn4.
                foreach(int offset in new[]{53,54,55})foreach(byte value in new byte[]{2,255})
                    RejectWire(Change(b=>b[offset]=value),"enum/Boolean at "+offset);
                RejectIntent(Intent() with {Kind=(NativeExchangeKind)255},"unknown source kind");
                RejectIntent(Intent() with {PartnerReference=(NativePartnerReference)255},"unknown source reference kind");
            });

            Test("M2-043-exchange-intent-fingerprint-covers-all-declared-inputs",new[]{"DEC-173","DEC-179"},()=> {
                var source=Intent() with {NextLogicalId=5};var expected=NativeExchangeIntentCodec.Fingerprint(source);
                var variants=new[]{
                    source with {Epoch=new Guid("11112233-4455-6677-8899-aabbccddeeff")},source with {Sequence=2},
                    source with {Revision=8},source with {Turn=123},source with {Actor=0},
                    source with {Kind=NativeExchangeKind.Disguise},source with {Swap=true},source with {NextLogicalId=6},
                    source with {Initiator=source.Initiator with {LogicalId=3}},source with {Initiator=source.Initiator with {NativeId=9}},
                    source with {Initiator=source.Initiator with {X=-3}},source with {Initiator=source.Initiator with {Y=2}},
                    source with {Partner=source.Partner with {LogicalId=4}},source with {Partner=source.Partner with {NativeId=8}},
                    source with {Partner=source.Partner with {X=5}},source with {Partner=source.Partner with {Y=-6}},
                    source with {PartnerReference=NativePartnerReference.Allocate,Partner=source.Partner with {LogicalId=5}},
                    source with {Initiator=source.Partner,Partner=source.Initiator}
                };
                var hashes=variants.Select(NativeExchangeIntentCodec.Fingerprint).ToArray();
                Assert(hashes.All(x=>x.Length==32&&!x.SequenceEqual(expected)),"A declared command field was omitted from fingerprint");
                Assert(hashes.Select(x=>Convert.ToHexString(x)).Distinct().Count()==hashes.Length,"Distinct canonical variants collapsed");
                for(int index=0;index<32;index++)
                {
                    var hash=source.SourceAppearanceHash.ToArray();hash[index]^=0x80;
                    Assert(!NativeExchangeIntentCodec.Fingerprint(source with {SourceAppearanceHash=hash}).SequenceEqual(expected),"Source hash byte omitted: "+index);
                }
                Assert(NativeExchangeIntentCodec.Fingerprint(NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(source))).SequenceEqual(expected));
            });

            Test("M2-044-exchange-intent-source-digest-is-exactly-thirty-two-bytes",new[]{"DEC-173","DEC-179"},()=> {
                foreach(int length in new[]{0,1,31,33,64,4096})RejectIntent(Intent() with {SourceAppearanceHash=new byte[length]},"digest length "+length);
                RejectIntent(Intent() with {SourceAppearanceHash=null!},"null digest");
                foreach(byte value in new byte[]{0,255})
                {
                    var expected=Enumerable.Repeat(value,32).ToArray();var decoded=NativeExchangeIntentCodec.Decode(
                        NativeExchangeIntentCodec.Encode(Intent() with {SourceAppearanceHash=expected}));
                    Assert(decoded.SourceAppearanceHash.SequenceEqual(expected),"Opaque digest content was normalized");
                }
            });

            Test("M2-045-exchange-intent-existing-and-proposed-identities-are-distinct",new[]{"DEC-043","DEC-173","DEC-179"},()=> {
                var existing=Intent();var allocate=existing with {PartnerReference=NativePartnerReference.Allocate,Partner=existing.Partner with {LogicalId=3}};
                Assert(NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(allocate)).PartnerReference==NativePartnerReference.Allocate);
                RejectIntent(existing with {Partner=existing.Partner with {LogicalId=3}},"existing unallocated partner");
                RejectIntent(allocate with {Partner=existing.Partner},"allocation of existing logical ID");
                RejectIntent(allocate with {Partner=allocate.Partner with {LogicalId=4}},"allocation skips next ID");
                foreach(var invalid in new[]{
                    existing with {Initiator=existing.Initiator with {LogicalId=0}},existing with {Partner=existing.Partner with {LogicalId=0}},
                    existing with {Initiator=existing.Initiator with {LogicalId=3}},existing with {Partner=existing.Partner with {LogicalId=1}},
                    existing with {Partner=existing.Partner with {NativeId=0}},existing with {Partner=existing.Partner with {X=-2,Y=3}},
                    existing with {Initiator=null!},existing with {Partner=null!}})RejectIntent(invalid,"invalid participant binding");
                var ledger=Ledger() with {Units=Ledger().Units.Take(1).ToArray()};var before=NativeIdentityLedgerCodec.Encode(ledger,7);
                Assert(!NativeExchangeIntentCodec.AlreadyCommitted(ledger,7,allocate),"Fresh declared allocation rejected");
                Assert(NativeIdentityLedgerCodec.Encode(ledger,7).SequenceEqual(before),"Validation allocated identity or consumed sequence");
            });

            Test("M2-046-exchange-intent-native-identities-and-signed-coordinate-boundaries",new[]{"DEC-173","DEC-179"},()=> {
                var value=Intent() with {Turn=uint.MaxValue,Actor=byte.MaxValue,
                    Initiator=new(1,uint.MaxValue,int.MinValue,int.MaxValue),Partner=new(2,0,int.MaxValue,int.MinValue)};
                var result=NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(value));
                Assert(result.Initiator==value.Initiator&&result.Partner==value.Partner&&result.Turn==uint.MaxValue&&result.Actor==byte.MaxValue,
                    "Native ID0/max or signed coordinates were narrowed");
                var zero=NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(value with {Turn=0,Actor=0}));
                Assert(zero.Turn==0&&zero.Actor==0,"Zero native turn/player was arbitrarily reserved");
                // Map bounds/adjacency remain the authoritative adapter's job;
                // the private transport must retain coordinates without wrapping.
            });

            Test("M2-047-exchange-intent-lineage-and-counter-overflow-rejected",new[]{"DEC-173","DEC-179"},()=> {
                var value=Intent();
                foreach(var invalid in new[]{value with {Epoch=Guid.Empty},value with {Sequence=0},value with {Sequence=ulong.MaxValue},
                    value with {Revision=-1},value with {Revision=long.MaxValue},value with {NextLogicalId=0},value with {NextLogicalId=ulong.MaxValue}})
                    RejectIntent(invalid,"lineage/counter bound");
                var maximum=value with {Sequence=ulong.MaxValue-1,Revision=long.MaxValue-1,NextLogicalId=ulong.MaxValue-1,
                    Initiator=value.Initiator with {LogicalId=ulong.MaxValue-3},Partner=value.Partner with {LogicalId=ulong.MaxValue-2}};
                var decoded=NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(maximum));
                Assert(decoded.Sequence==ulong.MaxValue-1&&decoded.Revision==long.MaxValue-1&&decoded.NextLogicalId==ulong.MaxValue-1);
                RejectWire(Change(b=>Array.Clear(b,16,16)),"empty wire epoch");
                foreach(ulong sequence in new[]{0UL,ulong.MaxValue})RejectWire(Change(b=>BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(32),sequence)),"wire sequence");
                foreach(long revision in new[]{-1L,long.MaxValue})RejectWire(Change(b=>BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(40),revision)),"wire revision");
                foreach(ulong next in new[]{0UL,ulong.MaxValue})RejectWire(Change(b=>BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(56),next)),"wire allocator");
            });

            Test("M2-048-exchange-operation-is-deterministic-and-injective-within-epoch",new[]{"DEC-173","DEC-179"},()=> {
                var sequences=Enumerable.Range(1,4096).Select(x=>(ulong)x).Concat(new[]{(ulong)uint.MaxValue,(ulong)uint.MaxValue+1,1UL<<63,ulong.MaxValue-1}).ToArray();
                var operations=sequences.Select(NativeExchangeIntentCodec.Operation).ToArray();
                Assert(operations.All(x=>x!=Guid.Empty)&&operations.Distinct().Count()==sequences.Length,"Operation sequence narrowed or collided");
                for(int i=0;i<sequences.Length;i++)Assert(NativeExchangeIntentCodec.Operation(sequences[i])==operations[i],"Operation depends on runtime state");
                RejectData(()=>NativeExchangeIntentCodec.Operation(0),"zero operation sequence");
                RejectData(()=>NativeExchangeIntentCodec.Operation(ulong.MaxValue),"exhausted operation sequence");
                var committed=Committed();RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(committed,8,Intent() with {
                    Epoch=new Guid("11112233-4455-6677-8899-aabbccddeeff")}),"same sequence under another epoch");
            });

            Test("GROW-011-preserves-legacy-command-digest-and-binds-growth-deadline",new[]{"DEC-090","DEC-173"},()=> {
                var a=Appearance();
                Assert(Convert.ToHexString(NativeExchangeIntentCodec.AppearanceFingerprint(a)).ToLowerInvariant()==
                    "344dfb3e067539a1e8735ef59d85cd78d7b088c46984c9aa4e9e5e1c1ace200f","Existing saved command appearance digest changed");
                var first=a with{GrowthTurns=1,GrowthDueTurn=20};var second=first with{GrowthDueTurn=21};
                Assert(!NativeExchangeIntentCodec.AppearanceFingerprint(first).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(second)),
                    "Different absolute growth deadline accepted by same command digest");
                var ledger=Ledger() with{Units=Ledger().Units.Select(r=>r.LogicalId==1?r with{Appearance=first}:r).ToArray()};
                var reloaded=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(ledger,7),7).Units[0].Appearance!;
                Assert(NativeExchangeIntentCodec.AppearanceFingerprint(first).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(reloaded)));
            });

            Test("M2-049-exchange-appearance-digest-covers-independent-visual-snapshot",new[]{"DEC-013","DEC-022","DEC-173"},()=> {
                var source=Appearance();var expected=NativeExchangeIntentCodec.AppearanceFingerprint(source);
                var variants=new[]{source with {Form="rider"},source with {TribeVisual=new("oumaji","default")},
                    source with {TribeVisual=new("imperius","skin_a")},source with {ClimateVisual=new("imperius","skin_b")},
                    source with {ClimateVisual=new("bardur","default")},source with {BirthVisual=new("bardur","skin_c")},
                    source with {BirthVisual=new("oumaji","default")},source with {SourceColor=int.MinValue},
                    source with {Health=72},source with {Maximum=101},source with {Veteran=false},
                    source with {Concealment=ApparentConcealment.Hidden},source with {GrowthTurns=0,GrowthDueTurn=4},source with {Effects=new[]{"frozen"}}};
                var hashes=variants.Select(NativeExchangeIntentCodec.AppearanceFingerprint).ToArray();
                Assert(expected.Length==32&&hashes.All(x=>!x.SequenceEqual(expected))&&hashes.Select(x=>Convert.ToHexString(x)).Distinct().Count()==hashes.Length,
                    "An apparent profile, color, wound or status field was omitted");
                var equal=Appearance();Assert(!ReferenceEquals(source.Effects,equal.Effects)&&NativeExchangeIntentCodec.AppearanceFingerprint(equal).SequenceEqual(expected));
                var ledger=Ledger();var decoded=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(ledger,7),7);
                Assert(NativeExchangeIntentCodec.AppearanceFingerprint(decoded.Units[0].Appearance!).SequenceEqual(expected),"Decoded independent appearance changed digest");
                source.Effects[0]="changed";Assert(NativeExchangeIntentCodec.AppearanceFingerprint(equal).SequenceEqual(expected),"Digest/result owns mutable source state");
                RejectData(()=>NativeExchangeIntentCodec.AppearanceFingerprint(equal with {Effects=new[]{"poisoned","frozen"}}),"noncanonical effect order");
                RejectData(()=>NativeExchangeIntentCodec.AppearanceFingerprint(equal with {Effects=new[]{"frozen","frozen"}}),"duplicate effect");
                RejectData(()=>NativeExchangeIntentCodec.AppearanceFingerprint(equal with {Health=0}),"invalid apparent health");
                RejectData(()=>NativeExchangeIntentCodec.AppearanceFingerprint(equal with {BirthVisual=new("bad/name","default")}),"invalid profile name");
            });

            Test("M2-050-exchange-fresh-intent-requires-exact-lineage-and-capacity",new[]{"DEC-043","DEC-173","DEC-179"},()=> {
                var ledger=Ledger();var before=NativeIdentityLedgerCodec.Encode(ledger,7);
                Assert(!NativeExchangeIntentCodec.AlreadyCommitted(ledger,7,Intent()),"Fresh intent mistaken for committed");
                foreach(var invalid in new[]{Intent() with {Epoch=new Guid("11112233-4455-6677-8899-aabbccddeeff")},Intent() with {Sequence=2},
                    Intent() with {Revision=6},Intent() with {Revision=8},Intent() with {NextLogicalId=4}})
                    RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(ledger,7,invalid),"fresh lineage mismatch");
                var full=ledger with {NextSequence=2049,Receipts=Enumerable.Range(1,2048).Select(i=> {
                    var value=Intent() with {Sequence=(ulong)i,Revision=i-1};return Receipt(value);
                }).ToArray()};
                var fullBefore=NativeIdentityLedgerCodec.Encode(full,2048);
                RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(full,2048,Intent() with {Sequence=2049,Revision=2048}),"receipt capacity");
                Assert(NativeExchangeIntentCodec.AlreadyCommitted(full,2048,Intent() with {Revision=0}),"A full history rejected an existing valid receipt");
                Assert(NativeIdentityLedgerCodec.Encode(full,2048).SequenceEqual(fullBefore)&&NativeIdentityLedgerCodec.Encode(ledger,7).SequenceEqual(before),
                    "Lineage/capacity checks changed the source ledger");
            });

            Test("M2-051-exchange-existing-receipt-precedes-fresh-state-guards",new[]{"DEC-173","DEC-179","DEC-180"},()=> {
                var original=Intent();var ledger=Committed();
                // Later turns may retire the initiator, change appearances and
                // allocate more units. An exact committed retry still no-ops.
                ledger=ledger with {NextLogicalId=5,Units=new[]{
                    ledger.Units[0] with {Alive=false,Independent=false,Appearance=null},
                    ledger.Units[1] with {Independent=true,Appearance=Appearance() with {Health=12}},
                    new LedgerUnit(4,14,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0)
                }};
                byte[] before=NativeIdentityLedgerCodec.Encode(ledger,100);
                Assert(NativeExchangeIntentCodec.AlreadyCommitted(ledger,100,original),"Exact retry was subjected to fresh revision/allocator/body checks");
                var loaded=NativeIdentityLedgerCodec.Decode(before,100);
                Assert(NativeExchangeIntentCodec.AlreadyCommitted(loaded,100,NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(original))),
                    "Committed identity depended on process-local object roots");
                Assert(NativeIdentityLedgerCodec.Encode(ledger,100).SequenceEqual(before),"Retry rewrote history or current appearance");
                // The native adapter must branch on true before checking its
                // current readiness/positions. This codec has no native flags.
            });

            Test("M2-052-exchange-conflicting-receipt-cannot-authorize-a-retry",new[]{"DEC-173","DEC-179"},()=> {
                var committed=Committed();var receipt=committed.Receipts[0];
                foreach(var invalid in new[]{Intent() with {Swap=true},Intent() with {Kind=NativeExchangeKind.Disguise},
                    Intent() with {Actor=0},Intent() with {Turn=1},Intent() with {Partner=Intent().Partner with {NativeId=8}}})
                    RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(committed,8,invalid),"same receipt key, changed intent");
                var wrongOperation=committed with {Receipts=new[]{receipt with {Operation=NativeExchangeIntentCodec.Operation(2)}}};
                RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(wrongOperation,8,Intent()),"operation/sequence mismatch");
                var changedHash=receipt.Fingerprint.ToArray();changedHash[0]^=1;
                RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(committed with {Receipts=new[]{receipt with {Fingerprint=changedHash}}},8,Intent()),"receipt fingerprint mismatch");
                // These receipts satisfy the generic ledger codec, but disagree
                // with this specific command's committed revision/participants.
                var wrongRevision=committed with {Receipts=new[]{receipt with {BeforeRevision=6,AfterRevision=7}}};
                NativeIdentityLedgerCodec.Validate(wrongRevision,8);
                RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(wrongRevision,8,Intent()),"matching digest with wrong commit revision");
                foreach(var affected in new[]{new ulong[]{1},new ulong[]{2}})
                {
                    var incomplete=committed with {Receipts=new[]{receipt with {Affected=affected}}};
                    NativeIdentityLedgerCodec.Validate(incomplete,8);
                    RejectData(()=>NativeExchangeIntentCodec.AlreadyCommitted(incomplete,8,Intent()),"matching digest omits a participant");
                }
            });

            Test("M2-053-exchange-catalog-adds-only-owned-carriers-and-preserves-native-data",new[]{"DEC-031","DEC-033","DEC-197"},()=> {
                var original=Registry();string input=original.ToJsonString();var after=JsonNode.Parse(NativeExchangeCatalog.Add(input))!.AsObject();
                Assert(original.ToJsonString()==input,"Catalog registration mutated caller data");
                foreach(var entry in original)
                    if(entry.Key!="improvementData")Assert(TechTree.Canonical(entry.Value)==TechTree.Canonical(after[entry.Key]),entry.Key+" changed");
                foreach(var entry in original["improvementData"]!.AsObject())
                    Assert(TechTree.Canonical(entry.Value)==TechTree.Canonical(after["improvementData"]![entry.Key]),"Existing native improvement changed: "+entry.Key);
                var added=after["improvementData"]!.AsObject().Select(x=>x.Key).Except(original["improvementData"]!.AsObject().Select(x=>x.Key)).OrderBy(x=>x,StringComparer.Ordinal);
                Assert(added.SequenceEqual(new[]{"dcp_exchange_blackmail","dcp_exchange_blackout","dcp_exchange_disguise","dcp_exchange_feign_boost","dcp_exchange_feign_heal","dcp_exchange_feign_recover","dcp_exchange_feign_swarm","dcp_exchange_lighthouse_secret","dcp_exchange_regroup","dcp_exchange_shed_disguise","dcp_exchange_subvert","dcp_exchange_upgrade_bomber","dcp_exchange_upgrade_rammer","dcp_exchange_upgrade_scout"}),"Unexpected carrier/unlock registered");
                Assert(NativeExchangeCatalog.Id(NativeExchangeKind.Regroup)=="dcp_exchange_regroup"&&NativeExchangeCatalog.Id(NativeExchangeKind.Disguise)=="dcp_exchange_disguise");
                Reject(()=>NativeExchangeCatalog.Id((NativeExchangeKind)255));
            });

            Test("M2-054-exchange-catalog-carriers-are-free-hidden-and-inert",new[]{"DEC-031","DEC-033","DEC-172"},()=> {
                var after=JsonNode.Parse(NativeExchangeCatalog.Add(Registry().ToJsonString()))!;
                foreach(string id in Enum.GetValues<NativeExchangeKind>().Select(NativeExchangeCatalog.Id))
                {
                    var carrier=after["improvementData"]![id]!;
                    Assert(carrier["idx"]!.GetValue<int>()==-1&&carrier["hidden"]!.GetValue<bool>()&&carrier["cost"]!.GetValue<int>()==0,
                        "Carrier consumes stars or enters an ordinary visible registry slot");
                    foreach(string field in new[]{"improvementAbilities","creates","growthRewards","terrainRequirements"})
                        Assert(carrier[field]!.AsArray().Count==0,"Carrier retains native effect/eligibility: "+field);
                    Assert(carrier["growthRate"]!.GetValue<int>()==0&&!carrier["shouldSuggestUnlock"]!.GetValue<bool>(),"Carrier grows or changes unlock suggestions");
                    Assert(carrier["prefab"]!.GetValue<string>()=="fruit-art"&&carrier["unrelatedNativeField"]!["preserve"]!.GetValue<string>()=="exact");
                }
                after["improvementData"]![NativeExchangeCatalog.Regroup]!["unrelatedNativeField"]!["preserve"]="changed";
                Assert(after["improvementData"]![NativeExchangeCatalog.Disguise]!["unrelatedNativeField"]!["preserve"]!.GetValue<string>()=="exact"&&
                    after["improvementData"]!["harvestfruit"]!["unrelatedNativeField"]!["preserve"]!.GetValue<string>()=="exact","Carrier copies alias each other or native metadata");
            });

            Test("M2-055-exchange-catalog-reentry-and-conflicts-fail-closed",new[]{"DEC-171","DEC-179","DEC-197"},()=> {
                var registered=JsonNode.Parse(NativeExchangeCatalog.Add(Registry().ToJsonString()))!;
                registered["improvementData"]![NativeExchangeCatalog.Regroup]!["idx"]=310;
                registered["improvementData"]![NativeExchangeCatalog.Disguise]!["idx"]=311;
                NativeExchangeCatalog.ValidateReentry(registered.ToJsonString());
                foreach(string id in Enum.GetValues<NativeExchangeKind>().Select(NativeExchangeCatalog.Id))
                foreach(string field in new[]{"cost","hidden","improvementAbilities","creates","growthRewards","growthRate","terrainRequirements","shouldSuggestUnlock","unrelatedNativeField"})
                {
                    var bad=JsonNode.Parse(registered.ToJsonString())!;var carrier=bad["improvementData"]![id]!;
                    if(field=="hidden")carrier[field]=false;
                    else if(field=="shouldSuggestUnlock")carrier[field]=true;
                    else if(field is "improvementAbilities" or "creates" or "growthRewards" or "terrainRequirements")carrier[field]=new JsonArray("unsafe");
                    else if(field=="unrelatedNativeField")carrier[field]=new JsonObject{["preserve"]="changed"};
                    else carrier[field]=99;
                    Reject(()=>NativeExchangeCatalog.ValidateReentry(bad.ToJsonString()));
                }
                Reject(()=>NativeExchangeCatalog.Add(registered.ToJsonString()));
                foreach(string id in Enum.GetValues<NativeExchangeKind>().Select(NativeExchangeCatalog.Id).Append("dcp_exchange_unknown"))
                {
                    var collision=Registry();collision["improvementData"]![id]=new JsonObject();string before=collision.ToJsonString();
                    Reject(()=>NativeExchangeCatalog.Add(before));Assert(collision.ToJsonString()==before);
                }
                var extra=JsonNode.Parse(registered.ToJsonString())!;extra["improvementData"]!["dcp_exchange_unknown"]=new JsonObject();
                Reject(()=>NativeExchangeCatalog.ValidateReentry(extra.ToJsonString()));
                var missing=JsonNode.Parse(registered.ToJsonString())!;missing["improvementData"]!.AsObject().Remove(NativeExchangeCatalog.Disguise);
                Reject(()=>NativeExchangeCatalog.ValidateReentry(missing.ToJsonString()));
                var drift=JsonNode.Parse(registered.ToJsonString())!;drift["improvementData"]!["harvestfruit"]!["prefab"]="new-art";
                Reject(()=>NativeExchangeCatalog.ValidateReentry(drift.ToJsonString()));
            });
        }
    }
}
