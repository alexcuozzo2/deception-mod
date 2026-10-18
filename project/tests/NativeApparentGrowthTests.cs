using System;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.Tests
{
    public static partial class KernelTests
    {
        static void NativeApparentGrowthTests()
        {
            LedgerAppearance Egg()=>new("dragonegg",new("Elyrion","Default"),new("Bardur","Default"),new("Elyrion","Default"),
                0x123456,60,100,false,ApparentConcealment.Visible,-1,new[]{"Frozen","Poisoned"});
            void Invalid(Action action)
            {bool denied=false;try{action();}catch(InvalidDataException){denied=true;}Assert(denied,"Invalid growth was accepted");}
            LedgerAppearance Reload(LedgerAppearance a)
            {
                var ledger=new NativeIdentityLedger(new Guid("a3cbff7b-a6e5-4ba2-a63b-a838f1989ac1"),2,1,
                    new[]{new LedgerUnit(1,42,true,LedgerActualKind.Mimic,true,a,PaidInvestmentKind.Known,3)},Array.Empty<LedgerReceipt>());
                return NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(ledger,0),0).Units[0].Appearance!;
            }
            Test("GROW-001-captures-donor-phase-without-newborn-reset",new[]{"DEC-089"},()=> {
                foreach(var (round,remaining,due) in new[]{(10u,3,13L),(11u,2,13L),(12u,1,13L),(13u,0,13L),(40u,0,40L)})
                {
                    var source=Egg();var a=NativeApparentGrowth.CaptureTimer(source,10,round);
                    Assert(a.Form=="dragonegg"&&a.GrowthTurns==remaining&&a.GrowthDueTurn==due&&a.Health==60);
                    Assert(source.GrowthTurns==-1&&source.GrowthDueTurn==-1&&!ReferenceEquals(a.Effects,source.Effects));
                    NativeApparentGrowth.Validate(a);
                }
                var baby=Egg() with{Form="babydragon",Health=110,Maximum=150};
                var copied=NativeApparentGrowth.CaptureTimer(baby,20,22);
                Assert(copied.GrowthTurns==1&&copied.GrowthDueTurn==23&&copied.Form=="babydragon");
            });
            Test("GROW-002-owner-boundary-is-idempotent-and-never-catches-up-stages",new[]{"DEC-089","DEC-090"},()=> {
                var a=NativeApparentGrowth.CaptureTimer(Egg(),10,10);
                Assert(ReferenceEquals(NativeApparentGrowth.Advance(a,10),a));
                a=NativeApparentGrowth.Advance(a,12);Assert(a.Form=="dragonegg"&&a.GrowthTurns==1&&a.GrowthDueTurn==13);
                var baby=NativeApparentGrowth.Advance(a,13);Assert(baby.Form=="babydragon"&&baby.GrowthTurns==3&&baby.GrowthDueTurn==16);
                for(int i=0;i<20;i++)Assert(ReferenceEquals(NativeApparentGrowth.Advance(baby,13),baby));
                var late=NativeApparentGrowth.Advance(a,100);Assert(late.Form=="babydragon"&&late.GrowthDueTurn==103&&late.GrowthTurns==3);
                Assert(ReferenceEquals(NativeApparentGrowth.Advance(late,100),late));
                var fire=NativeApparentGrowth.Advance(baby,16);Assert(fire.Form=="firedragon"&&fire.GrowthTurns==-1&&fire.GrowthDueTurn==-1);
                Assert(ReferenceEquals(NativeApparentGrowth.Advance(fire,uint.MaxValue),fire));
            });
            Test("GROW-003-native-stored-health-deficit-survives-both-stages",new[]{"DEC-089"},()=> {
                // Independent expected rows include lowest positive stored HP,
                // wounds and full health; no proportional scaling or real HP.
                foreach(var (eggHp,babyHp,fireHp) in new[]{(1,51,101),(60,110,160),(99,149,199),(100,150,200)})
                {
                    var source=Egg() with{Health=(ushort)eggHp};var a=NativeApparentGrowth.CaptureTimer(source,0,3);
                    var b=NativeApparentGrowth.Advance(a,3);var f=NativeApparentGrowth.Advance(b,6);
                    Assert(b.Health==babyHp&&b.Maximum==150&&f.Health==fireHp&&f.Maximum==200);
                    Assert(source.Health==eggHp&&source.Maximum==100&&!b.Veteran&&!f.Veteran);
                }
                var baby=NativeApparentGrowth.CaptureTimer(Egg() with{Form="babydragon",Health=1,Maximum=150},4,7);
                Assert(NativeApparentGrowth.Advance(baby,7).Health==51);
            });
            Test("GROW-004-snapshot-and-copied-effects-do-not-link-to-donor",new[]{"DEC-089","DEC-090"},()=> {
                var donor=NativeApparentGrowth.CaptureTimer(Egg(),10,12);var copy=Reload(donor);
                var originalHash=NativeExchangeIntentCodec.AppearanceFingerprint(copy);
                var donorNext=NativeApparentGrowth.Advance(donor,13);donorNext.Effects[0]="Invisible";
                Assert(copy.Form=="dragonegg"&&copy.GrowthTurns==1&&copy.GrowthDueTurn==13&&copy.Effects[0]=="Frozen");
                Assert(originalHash.SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(copy)));
                var copyNext=NativeApparentGrowth.Advance(copy,13);
                Assert(copyNext.TribeVisual==copy.TribeVisual&&copyNext.ClimateVisual==copy.ClimateVisual&&copyNext.BirthVisual==copy.BirthVisual&&
                    copyNext.SourceColor==copy.SourceColor&&copyNext.Concealment==copy.Concealment&&copyNext.Effects.SequenceEqual(copy.Effects));
                Assert(!ReferenceEquals(copyNext.Effects,copy.Effects)&&copy.Form=="dragonegg");
            });
            Test("GROW-005-reload-before-and-after-boundary-preserves-phase",new[]{"DEC-090","DEC-171","DEC-173"},()=> {
                var a=NativeApparentGrowth.CaptureTimer(Egg(),7,9);var saved=Reload(a);
                var direct=NativeApparentGrowth.Advance(a,10);var restored=NativeApparentGrowth.Advance(saved,10);
                Assert(NativeExchangeIntentCodec.AppearanceFingerprint(direct).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(restored)));
                var after=Reload(restored);var duplicate=NativeApparentGrowth.Advance(after,10);
                Assert(duplicate.Form=="babydragon"&&duplicate.GrowthDueTurn==13&&duplicate.GrowthTurns==3);
                var fire=NativeApparentGrowth.Advance(Reload(duplicate),13);Assert(fire.Form=="firedragon"&&fire.Health==160);
            });
            Test("GROW-006-replaced-costume-has-no-stray-timer",new[]{"DEC-089","DEC-090"},()=> {
                var growing=NativeApparentGrowth.CaptureTimer(Egg(),10,12);
                var warrior=growing with{Form="warrior",GrowthTurns=-1,GrowthDueTurn=-1};
                Assert(ReferenceEquals(NativeApparentGrowth.Advance(warrior,13),warrior));
                Invalid(()=>NativeApparentGrowth.Validate(warrior with{GrowthTurns=1,GrowthDueTurn=13}));
                var newer=NativeApparentGrowth.CaptureTimer(Egg(),12,12);
                Assert(newer.GrowthDueTurn==15&&NativeApparentGrowth.Advance(newer,13).Form=="dragonegg");
            });
            Test("GROW-007-invalid-phase-health-promotion-and-clock-rejected",new[]{"DEC-089","DEC-179"},()=> {
                var valid=NativeApparentGrowth.CaptureTimer(Egg(),10,12);
                foreach(var a in new[]{valid with{Health=0},valid with{Health=101},valid with{Maximum=101},valid with{Veteran=true},
                    valid with{GrowthTurns=-1},valid with{GrowthTurns=4},valid with{GrowthDueTurn=-1},valid with{GrowthDueTurn=(long)uint.MaxValue+1},
                    valid with{Effects=null!}})Invalid(()=>NativeApparentGrowth.Validate(a));
                Invalid(()=>NativeApparentGrowth.CaptureTimer(Egg(),13,12));
                Invalid(()=>NativeApparentGrowth.Advance(valid,9));
                Assert(valid.Health==60&&valid.GrowthDueTurn==13&&valid.GrowthTurns==1);
            });
            Test("GROW-008-round-overflow-rejects-before-mutating-snapshot",new[]{"DEC-090","DEC-179"},()=> {
                Invalid(()=>NativeApparentGrowth.CaptureTimer(Egg(),uint.MaxValue-1,uint.MaxValue-1));
                var egg=NativeApparentGrowth.CaptureTimer(Egg(),uint.MaxValue-3,uint.MaxValue);
                Invalid(()=>NativeApparentGrowth.Advance(egg,uint.MaxValue));
                Assert(egg.Form=="dragonegg"&&egg.GrowthDueTurn==uint.MaxValue&&egg.GrowthTurns==0);
                var baby=NativeApparentGrowth.CaptureTimer(Egg() with{Form="babydragon",Health=110,Maximum=150},uint.MaxValue-3,uint.MaxValue);
                var fire=NativeApparentGrowth.Advance(baby,uint.MaxValue);Assert(fire.Form=="firedragon"&&fire.GrowthDueTurn==-1&&fire.Health==160);
            });
            Test("GROW-009-insect-egg-hatches-on-one-turn-timer",new[]{"DEC-089"},()=> {
                var egg=NativeApparentGrowth.CaptureTimer(Egg() with{Form="bugegg"},8,8);
                Assert(egg.GrowthTurns==1&&egg.GrowthDueTurn==9);
                var larva=NativeApparentGrowth.Advance(Reload(egg),9);
                Assert(larva.Form=="larva"&&larva.Health==60&&larva.Maximum==100&&larva.GrowthDueTurn==12&&larva.GrowthTurns==3);
                Invalid(()=>NativeApparentGrowth.Validate(egg with{GrowthTurns=2}));
            });
            Test("GROW-010-larva-matures-without-healing-or-real-body-change",new[]{"DEC-089","DEC-090"},()=> {
                var larva=NativeApparentGrowth.CaptureTimer(Egg() with{Form="larva",Health=1},8,10);
                Assert(larva.GrowthTurns==1&&larva.GrowthDueTurn==11);
                var moth=NativeApparentGrowth.Advance(Reload(larva),11);
                Assert(moth.Form=="moth"&&moth.Health==1&&moth.Maximum==100&&moth.GrowthTurns==-1&&moth.GrowthDueTurn==-1);
                Assert(moth.Effects.SequenceEqual(larva.Effects)&&!ReferenceEquals(moth.Effects,larva.Effects));
                Assert(larva.Form=="larva"&&larva.GrowthDueTurn==11);
            });
            Test("GROW-011-overdue-insect-copy-advances-one-stage-only",new[]{"DEC-090"},()=> {
                var egg=NativeApparentGrowth.CaptureTimer(Egg() with{Form="bugegg"},8,20);
                Assert(egg.GrowthTurns==0&&egg.GrowthDueTurn==20);
                var larva=NativeApparentGrowth.Advance(egg,20);
                Assert(larva.Form=="larva"&&larva.GrowthDueTurn==23);
                Assert(ReferenceEquals(NativeApparentGrowth.Advance(larva,20),larva));
                Assert(NativeApparentGrowth.Advance(larva,23).Form=="moth");
            });
        }
    }
}
