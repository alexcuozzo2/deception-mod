using System;
using System.IO;

namespace Deception.Core
{
    // Pure apparent-state transition. The runtime supplies only an authenticated
    // owner's native turn event; this class neither advances turns nor edits a
    // native body, ownership, readiness, investment or identity.
    public static class NativeApparentGrowth
    {
        public const int PhaseTurns=3;
        public static bool IsGrowing(string form)=>form is "dragonegg" or "babydragon" or "bugegg" or "larva";
        static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        public static int Duration(string form)=>form=="bugegg"?1:PhaseTurns;
        public static string? Next(string form)=>form switch{"dragonegg"=>"babydragon","babydragon"=>"firedragon","bugegg"=>"larva","larva"=>"moth",_=>null};
        static int Maximum(string form)=>form switch{"dragonegg"=>100,"babydragon"=>150,"firedragon"=>200,"bugegg" or "larva" or "moth"=>100,_=>0};
        static void Body(LedgerAppearance appearance)
        {
            ArgumentNullException.ThrowIfNull(appearance);
            Require(appearance.Health>0&&appearance.Health<=appearance.Maximum,"Invalid apparent growth health.");
            int maximum=Maximum(appearance.Form);
            if(maximum!=0)Require(!appearance.Veteran&&appearance.Maximum==maximum,"Growing dragon stage has a nonnative maximum or promotion.");
            Require(appearance.Effects is not null,"Missing apparent growth effects.");
        }
        public static void Validate(LedgerAppearance appearance)
        {
            Body(appearance);
            if(IsGrowing(appearance.Form))
                Require(appearance.GrowthTurns>=0&&appearance.GrowthTurns<=Duration(appearance.Form)&&appearance.GrowthDueTurn is >=0 and <=uint.MaxValue,
                    "Growing disguise needs a bounded native phase and scheduled owner turn.");
            else Require(appearance.GrowthTurns==-1&&appearance.GrowthDueTurn==-1,"Nongrowing disguise retains a growth timer.");
        }
        public static LedgerAppearance CaptureTimer(LedgerAppearance appearance,uint createdRound,uint currentRound)
        {
            Body(appearance);
            if(!IsGrowing(appearance.Form))return appearance with{GrowthTurns=-1,GrowthDueTurn=-1,Effects=(string[])appearance.Effects.Clone()};
            Require(createdRound<=currentRound,"Native growing source was created in a future round.");
            long remaining=Math.Max(0,Duration(appearance.Form)-((long)currentRound-createdRound));
            long due=(long)currentRound+remaining;
            Require(due<=uint.MaxValue,"Native growth deadline overflows the round counter.");
            return appearance with{GrowthTurns=(int)remaining,GrowthDueTurn=due,Effects=(string[])appearance.Effects.Clone()};
        }
        public static LedgerAppearance Advance(LedgerAppearance appearance,uint currentRound)
        {
            Validate(appearance);
            if(!IsGrowing(appearance.Form))return appearance;
            if(currentRound<appearance.GrowthDueTurn)
            {
                long remaining=appearance.GrowthDueTurn-currentRound;
                Require(remaining<=Duration(appearance.Form),"Growth callback predates the captured phase.");
                return appearance.GrowthTurns==remaining?appearance:
                    appearance with{GrowthTurns=(int)remaining,Effects=(string[])appearance.Effects.Clone()};
            }
            // Native growth preserves the missing stored-health amount, capped
            // so a living source remains alive. Never copy actual Mimic health.
            string next=Next(appearance.Form)!;
            int maximum=Maximum(next),missing=appearance.Maximum-appearance.Health;
            ushort health=(ushort)(maximum-Math.Min(missing,maximum-1));
            long due=IsGrowing(next)?(long)currentRound+Duration(next):-1;
            Require(due<=uint.MaxValue,"Next apparent growth deadline overflows the round counter.");
            var result=appearance with{Form=next,Health=health,Maximum=(ushort)maximum,Veteran=false,
                GrowthTurns=IsGrowing(next)?Duration(next):-1,GrowthDueTurn=due,Effects=(string[])appearance.Effects.Clone()};
            Validate(result);return result;
        }
    }
}
