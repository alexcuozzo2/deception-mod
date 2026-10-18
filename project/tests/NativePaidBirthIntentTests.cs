using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        static void NativePaidBirthIntentTests()=>PaidBirthCases.Run();
        // Pure recorded-witness contracts; no native payment/spawn acceptance.
        static class PaidBirthCases
        {
        static readonly Guid Epoch=new("9cc40cee-1308-4143-a9c1-8536f7fbf571");
        static void Check(bool value,string message="Assertion failed"){if(!value)throw new Exception(message);}
        static void Reject(Action action,string contains="")
        {try{action();}catch(InvalidDataException error){Check(error.Message.Contains(contains,StringComparison.Ordinal),error.Message);return;}throw new Exception("Expected rejection: "+contains);}
        static LedgerAppearance Appearance(string form="warrior",ushort health=100)=>new(form,
            new("Imperius","None"),new("Bardur","None"),new("None","None"),unchecked((int)0x80AB1234),health,100,
            false,ApparentConcealment.Visible,-1,Array.Empty<string>());
        static NativeIdentityLedger Ledger()=>new(Epoch,3,1,new[]{
            new LedgerUnit(1,1,true,LedgerActualKind.Mimic,true,Appearance(health:61),PaidInvestmentKind.UnknownLegacy,0),
            new LedgerUnit(2,2,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,5)},Array.Empty<LedgerReceipt>());
        static NativePaidBirthIntent Intent(string rule=NativePaidBirthIntentCodec.Warrior)=>new(Epoch,1,0,3,12,1,rule,8,8,7,1);
        static NativePaidBirthBefore Before(NativePaidBirthIntent intent)=>new(123,intent.Turn,intent.Actor,intent.NativeRuleId,
            intent.CityX,intent.CityY,intent.Actor,true,true,true,intent.ExpectedNativeCounter,12,1,100,new uint[]{1,2,7},
            Appearance(NativePaidBirthIntentCodec.Form(intent.NativeRuleId)));
        static NativePaidBirthAfter After(NativePaidBirthIntent intent)=>new(true,false,1,true,intent.Turn,intent.Actor,1,
            intent.ExpectedNativeCounter+1,11,new(intent.ExpectedNativeCounter+1,intent.Actor,intent.NativeRuleId,intent.NativeRuleId,
                intent.CityX,intent.CityY,intent.CityX,intent.CityY,10,10,0,0,0,0,true,true,true,true),
            new uint[]{1,2,7,intent.ExpectedNativeCounter+1},true,true);
        static byte[] Bytes(NativeIdentityLedger ledger,long revision=0)=>NativeIdentityLedgerCodec.Encode(ledger,revision);
        static NativePaidBirthResult Purchase(string rule=NativePaidBirthIntentCodec.Warrior)
        {var intent=Intent(rule);var ledger=Ledger();var plan=NativePaidBirthLedger.Prepare(ledger,0,intent,Before(intent));return NativePaidBirthLedger.Complete(plan,ledger,0,After(intent));}

        internal static void Run()
        {
            KernelTests.Test("M2-070-birth-codec-both-forms-and-boundary-values",new[]{"DEC-003","DEC-173"},()=>
            {
                foreach(var rule in new[]{NativePaidBirthIntentCodec.Warrior,NativePaidBirthIntentCodec.Rider})
                {
                    var input=Intent(rule) with {Actor=byte.MaxValue,Turn=uint.MaxValue,CityX=int.MinValue,CityY=int.MaxValue,
                        ExpectedNativeCounter=uint.MaxValue-1,Sequence=ulong.MaxValue-1,NextLogicalId=ulong.MaxValue-1,Revision=long.MaxValue-1};
                    var bytes=NativePaidBirthIntentCodec.Encode(input);Check(NativePaidBirthIntentCodec.Decode(bytes)==input);
                    Check(NativePaidBirthIntentCodec.Encode(NativePaidBirthIntentCodec.Decode(bytes)).SequenceEqual(bytes));
                    Check(BinaryPrimitives.ReadUInt32LittleEndian(bytes)==0x42504344&&BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8))==123);
                }
                Check(NativePaidBirthIntentCodec.Decode(NativePaidBirthIntentCodec.Encode(Intent() with{Actor=0,ExpectedNativeCounter=0})).Actor==0);
            });
            KernelTests.Test("M2-071-birth-codec-rejects-malformed-header-version-length-trailing",new[]{"DEC-003","DEC-173"},()=>
            {
                byte[] bytes=NativePaidBirthIntentCodec.Encode(Intent());
                foreach(int offset in new[]{0,4,6,8,12,77}){var changed=(byte[])bytes.Clone();changed[offset]^=0x40;Reject(()=>NativePaidBirthIntentCodec.Decode(changed));}
                Reject(()=>NativePaidBirthIntentCodec.Decode(bytes[..^1]));Reject(()=>NativePaidBirthIntentCodec.Decode(bytes.Concat(new byte[]{0}).ToArray()));
                var nonascii=(byte[])bytes.Clone();nonascii[^1]=255;Reject(()=>NativePaidBirthIntentCodec.Decode(nonascii));
                foreach(var bad in new[]{Intent() with{Epoch=Guid.Empty},Intent() with{Sequence=0},Intent() with{Sequence=ulong.MaxValue},
                    Intent() with{Revision=-1},Intent() with{Revision=long.MaxValue},Intent() with{NextLogicalId=0},Intent() with{NextLogicalId=ulong.MaxValue},
                    Intent() with{ExpectedNativeCounter=uint.MaxValue},Intent() with{ExpectedPrice=0},Intent() with{ExpectedPrice=4},
                    Intent() with{NativeRuleId="dcp_mimic"},Intent() with{NativeRuleId="dcp_mimic_form_knight"}})
                    Reject(()=>NativePaidBirthIntentCodec.Encode(bad));
            });
            KernelTests.Test("M2-072-birth-domains-and-fingerprint-cover-complete-choice",new[]{"DEC-003","DEC-173"},()=>
            {
                var intent=Intent();byte[] original=NativePaidBirthIntentCodec.Fingerprint(intent);
                Check(NativePaidBirthIntentCodec.Operation(1)!=NativeExchangeIntentCodec.Operation(1));
                Check(NativePaidBirthIntentCodec.Operation(1)!=NativePaidBirthIntentCodec.Operation(2));
                Check(NativePaidBirthIntentCodec.Operation(ulong.MaxValue-1).ToByteArray()[0]==(byte)'D');
                foreach(var other in new[]{intent with{Epoch=Guid.NewGuid()},intent with{Sequence=2},intent with{Revision=1},
                    intent with{NextLogicalId=4},intent with{Turn=13},intent with{Actor=2},intent with{CityX=9},intent with{CityY=9},
                    intent with{ExpectedNativeCounter=8},intent with{NativeRuleId=NativePaidBirthIntentCodec.Rider}})
                    Check(!NativePaidBirthIntentCodec.Fingerprint(other).SequenceEqual(original));
            });
            KernelTests.Test("M2-073-birth-two-native-form-value-plans-known1-exact-increments",new[]{"DEC-003","DEC-173"},()=>
            {
                foreach(string rule in new[]{NativePaidBirthIntentCodec.Warrior,NativePaidBirthIntentCodec.Rider})
                {
                    var before=Ledger() with{Blackmails=new[]{new LedgerBlackmail(1,2,3,4,8,9)}};byte[] unchanged=Bytes(before);var intent=Intent(rule);
                    var plan=NativePaidBirthLedger.Prepare(before,0,intent,Before(intent));Check(Bytes(before).SequenceEqual(unchanged));
                    var result=NativePaidBirthLedger.Complete(plan,before,0,After(intent));var next=result.Ledger;
                    Check(!result.AlreadyCommitted&&result.BeforeRevision==0&&result.AfterRevision==1&&next.NextLogicalId==4&&next.NextSequence==2);
                    var born=next.Units[2];Check(born.LogicalId==3&&born.NativeId==8&&born.Kind==LedgerActualKind.Mimic&&born.Independent&&
                        born.InvestmentKind==PaidInvestmentKind.Known&&born.PaidStars==1&&born.Appearance!.Form==NativePaidBirthIntentCodec.Form(rule));
                    Check(next.Receipts[0].Affected.SequenceEqual(new ulong[]{3})&&next.Receipts[0].Operation==NativePaidBirthIntentCodec.Operation(1));
                    Check(next.Units[0].InvestmentKind==PaidInvestmentKind.UnknownLegacy&&next.Units[0].PaidStars==0&&next.Units[0].Appearance!.Health==61);
                    Check(next.Units[1].InvestmentKind==PaidInvestmentKind.Known&&next.Units[1].PaidStars==5);
                    Check(next.Blackmails!.SequenceEqual(before.Blackmails!),"Recruitment dropped city Blackmail history");
                    Check(Bytes(before).SequenceEqual(unchanged));
                }
            });
            KernelTests.Test("M2-074-birth-empty-ledger-allocates-only-newborn",new[]{"DEC-003","DEC-173"},()=>
            {
                var empty=new NativeIdentityLedger(Epoch,1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>());var intent=Intent() with{NextLogicalId=1};
                var before=Before(intent);var plan=NativePaidBirthLedger.Prepare(empty,0,intent,before);
                var result=NativePaidBirthLedger.Complete(plan,empty,0,After(intent));Check(result.Ledger.Units.Length==1&&result.LogicalId==1);
                Check(empty.Units.Length==0&&empty.Receipts.Length==0&&empty.NextLogicalId==1);
            });
            KernelTests.Test("A61-001-birth-exact-one-star-and-no-funds-boundary",new[]{"DEC-003","DEC-173"},()=>
            {
                var intent=Intent();var ledger=Ledger();var before=Before(intent) with{Currency=1};
                var plan=NativePaidBirthLedger.Prepare(ledger,0,intent,before);
                var result=NativePaidBirthLedger.Complete(plan,ledger,0,After(intent) with{Currency=0});
                Check(result.Ledger.Units.Single(u=>u.LogicalId==3).PaidStars==1);
                Reject(()=>NativePaidBirthLedger.Prepare(ledger,0,intent,before with{Currency=0}));
                Reject(()=>NativePaidBirthLedger.Prepare(ledger,0,intent,before with{NativeCost=3}));
                Reject(()=>NativePaidBirthLedger.Complete(plan,ledger,0,After(intent) with{Currency=1}));
                Reject(()=>NativePaidBirthLedger.Complete(plan,ledger,0,After(intent) with{Currency=-2,NativeCost=3}));
            });
            KernelTests.Test("A61-002-birth-historical-three-star-receipt-remains-idempotent",new[]{"DEC-003","DEC-173"},()=>
            {
                var historical=Intent() with{ExpectedPrice=3};
                Check(NativePaidBirthIntentCodec.Decode(NativePaidBirthIntentCodec.Encode(historical))==historical);
                var current=Purchase().Ledger;
                var old=current with{
                    Units=current.Units.Select(u=>u.LogicalId==3?u with{PaidStars=3}:u).ToArray(),
                    Receipts=current.Receipts.Select(r=>r with{Fingerprint=NativePaidBirthIntentCodec.Fingerprint(historical)}).ToArray()};
                var encoded=Bytes(old,1);var loaded=NativeIdentityLedgerCodec.Decode(encoded,1);
                var replay=NativePaidBirthLedger.Prepare(loaded,1,historical,null);
                Check(replay.AlreadyCommitted&&NativePaidBirthLedger.Complete(replay,loaded,1,null).Bytes().SequenceEqual(encoded));
                Reject(()=>NativePaidBirthLedger.AlreadyCommitted(loaded,1,Intent()),"conflicts");
                Reject(()=>NativePaidBirthLedger.AlreadyCommitted(loaded with{Units=loaded.Units.Select(u=>u.LogicalId==3?u with{PaidStars=1}:u).ToArray()},1,historical),"investment");
                Reject(()=>NativePaidBirthLedger.Prepare(Ledger(),0,historical,Before(historical) with{NativeCost=3}));
            });
            KernelTests.Test("M2-075-birth-duplicate-before-fresh-guards-and-after-retirement",new[]{"DEC-003","DEC-173"},()=>
            {
                var result=Purchase();var ledger=result.Ledger;var intent=Intent();byte[] committed=result.Bytes();
                var duplicate=NativePaidBirthLedger.Prepare(ledger,1,intent,null);Check(duplicate.AlreadyCommitted);
                var noOp=NativePaidBirthLedger.Complete(duplicate,ledger,1,null);Check(noOp.AlreadyCommitted&&noOp.Bytes().SequenceEqual(committed));
                var later=ledger with {NextSequence=3,Receipts=ledger.Receipts.Append(new LedgerReceipt(2,NativeExchangeIntentCodec.Operation(2),new byte[32],1,2,new ulong[]{1,3})).ToArray()};
                later=later with {Units=later.Units.Select(u=>u.LogicalId==3?u with{Alive=false,Independent=false,Appearance=null}:u).ToArray()};
                Check(NativePaidBirthLedger.AlreadyCommitted(later,2,intent));
                Check(NativePaidBirthLedger.Complete(duplicate,later,2,null).Bytes().SequenceEqual(Bytes(later,2)));
            });
            KernelTests.Test("M2-076-birth-receipt-conflicts-never-train-again",new[]{"DEC-003","DEC-173"},()=>
            {
                var ledger=Purchase().Ledger;var intent=Intent();
                Reject(()=>NativePaidBirthLedger.AlreadyCommitted(ledger,1,intent with{CityX=9}),"conflicts");
                var receipt=ledger.Receipts[0];
                foreach(var replacement in new[]{receipt with{Operation=NativeExchangeIntentCodec.Operation(1)},
                    receipt with{Affected=new ulong[]{1}},receipt with{Fingerprint=new byte[32]}})
                    Reject(()=>NativePaidBirthLedger.AlreadyCommitted(ledger with{Receipts=new[]{replacement}},1,intent),"conflicts");
                var unknown=ledger with{Units=ledger.Units.Select(u=>u.LogicalId==3?u with{InvestmentKind=PaidInvestmentKind.UnknownLegacy,PaidStars=0}:u).ToArray()};
                Reject(()=>NativePaidBirthLedger.AlreadyCommitted(unknown,1,intent),"investment");
            });
            KernelTests.Test("M2-077-birth-rejects-occupied-cost0-ineligible-and-stale-before-staging",new[]{"DEC-003","DEC-173"},()=>
            {
                var ledger=Ledger();var intent=Intent();var b=Before(intent);byte[] unchanged=Bytes(ledger);
                foreach(var bad in new[]{b with{Empty=false},b with{IsCity=false},b with{NativeEligible=false},b with{NativeCost=0},
                    b with{Currency=0},b with{NativeVersion=124},b with{Turn=13},b with{Actor=2},b with{CityOwner=2},b with{CityX=9},
                    b with{CityY=9},b with{NativeRuleId=NativePaidBirthIntentCodec.Rider},b with{NativeCounter=8},
                    b with{NativeIds=new uint[]{1,2,7,8}},b with{NativeIds=new uint[]{1,1,2}},b with{NativeIds=new uint[]{1,7}},
                    b with{Appearance=Appearance(health:99)},b with{Appearance=Appearance("rider")},b with{ApparentMaximum=150}})
                    Reject(()=>NativePaidBirthLedger.Prepare(ledger,0,intent,bad));
                foreach(var bad in new[]{intent with{Epoch=Guid.NewGuid()},intent with{Sequence=2},intent with{Revision=1},intent with{NextLogicalId=4}})
                    Reject(()=>NativePaidBirthLedger.Prepare(ledger,0,bad,b));
                Check(Bytes(ledger).SequenceEqual(unchanged));
            });
            KernelTests.Test("M2-078-birth-rejects-missing-payment-helper-body-and-queue-proof",new[]{"DEC-003","DEC-173"},()=>
            {
                var ledger=Ledger();var intent=Intent();var plan=NativePaidBirthLedger.Prepare(ledger,0,intent,Before(intent));var a=After(intent);
                foreach(var bad in new[]{a with{OriginalCompleted=false},a with{Aborted=true},a with{MatchingHelperResults=0},a with{MatchingHelperResults=2},
                    a with{ExactHelperBody=false},a with{Currency=12},a with{Currency=10},a with{NativeCost=0},a with{NativeCounter=9},
                    a with{Turn=13},a with{Actor=2},a with{ExistingGraphPreserved=false},a with{QueuePrefixPreserved=false},
                    a with{NativeIds=new uint[]{1,2,7,8,9}},a with{NativeIds=new uint[]{1,2,8}}})
                    Reject(()=>NativePaidBirthLedger.Complete(plan,ledger,0,bad));
                foreach(var body in new[]{a.Body with{Id=9},a.Body with{Owner=2},a.Body with{NativeRuleId=NativePaidBirthIntentCodec.Rider},
                    a.Body with{CachedRuleId="warrior"},a.Body with{X=9},a.Body with{Y=9},a.Body with{HomeX=9},a.Body with{HomeY=9},
                    a.Body with{Health=9},a.Body with{Maximum=20},a.Body with{Promotion=1},a.Body with{Attack=1},a.Body with{Defence=1},
                    a.Body with{Range=1},a.Body with{Root=false},a.Body with{SingleBody=false},a.Body with{Moved=false},a.Body with{Attacked=false}})
                    Reject(()=>NativePaidBirthLedger.Complete(plan,ledger,0,a with{Body=body}));
                Check(ledger.Units.Length==2&&ledger.Receipts.Length==0&&ledger.NextLogicalId==3);
            });
            KernelTests.Test("M2-079-birth-owned-plan-result-and-cosmetics-copies",new[]{"DEC-003","DEC-173"},()=>
            {
                var ledger=Ledger();var intent=Intent();var b=Before(intent);var plan=NativePaidBirthLedger.Prepare(ledger,0,intent,b);
                b.NativeIds[0]=999;byte[] exposed=plan.BeforePayload;exposed[0]^=255;
                var result=NativePaidBirthLedger.Complete(plan,ledger,0,After(intent));byte[] correct=result.Bytes();
                var copy=result.Ledger;copy.Receipts[0].Fingerprint[0]^=255;copy.Units[2]=copy.Units[2] with{PaidStars=0};
                exposed=result.Bytes();exposed[0]^=255;Check(result.Bytes().SequenceEqual(correct));
                var visual=result.Ledger.Units[2].Appearance!;Check(visual.TribeVisual.Tribe=="Imperius"&&visual.ClimateVisual.Tribe=="Bardur"&&
                    visual.BirthVisual.Tribe=="None"&&visual.SourceColor==unchecked((int)0x80AB1234));
                var changed=ledger with{Units=ledger.Units.Select(u=>u.LogicalId==2?u with{PaidStars=6}:u).ToArray()};
                Reject(()=>NativePaidBirthLedger.Complete(plan,changed,0,After(intent)),"changed during");
            });
            KernelTests.Test("M2-080-birth-capacity-and-native-id-reuse-preflight",new[]{"DEC-003","DEC-173"},()=>
            {
                var intent=Intent();var ledger=Ledger();var b=Before(intent);
                var retired=new LedgerUnit(3,8,false,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0);
                var reused=ledger with{NextLogicalId=4,Units=ledger.Units.Append(retired).ToArray()};
                Reject(()=>NativePaidBirthLedger.Prepare(reused,0,intent with{NextLogicalId=4},b),"reuse");
                var rows=Enumerable.Range(1,NativeIdentityLedgerCodec.MaxUnits).Select(i=>new LedgerUnit((ulong)i,(uint)i,false,
                    LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0)).ToArray();
                var full=new NativeIdentityLedger(Epoch,(ulong)rows.Length+1,1,rows,Array.Empty<LedgerReceipt>());
                var fullIntent=intent with{NextLogicalId=full.NextLogicalId,ExpectedNativeCounter=10000};
                Reject(()=>NativePaidBirthLedger.Prepare(full,0,fullIntent,Before(fullIntent)),"capacity");
                var receipts=Enumerable.Range(1,NativeIdentityLedgerCodec.MaxReceipts).Select(i=>new LedgerReceipt((ulong)i,
                    NativeExchangeIntentCodec.Operation((ulong)i),new byte[32],i-1,i,new ulong[]{1})).ToArray();
                full=ledger with{NextSequence=(ulong)receipts.Length+1,Receipts=receipts};
                fullIntent=intent with{Sequence=full.NextSequence,Revision=receipts.Length};
                Reject(()=>NativePaidBirthLedger.Prepare(full,receipts.Length,fullIntent,Before(fullIntent)),"capacity");
            });
        }
        }
    }
}
