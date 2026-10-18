using System;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Retired command identity retained for already committed saved receipts.
    // No input hook, new offer, reward calculation or treasury mutation remains.
    internal static class NativeLighthouseSecret
    {
        const string Removed="The lighthouse star reward has been removed.";
        internal static bool TryOffer(object state,byte actor,object tile,out NativeExchangeIntent? intent,out string reason)
        {intent=null;reason=Removed;return false;}

        internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason)
        {
            plan=null;reason=Removed;
            try
            {
                if(intent.Kind!=NativeExchangeKind.LighthouseSecret||!NativeRegroupSession.Enabled||
                    !NativeRegroupSession.Ready(state)||!RecruitmentHooks.Registered||
                    !Equals(Native.Get(state,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started")))return false;
                var request=NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(intent));
                var snapshot=NativeGameStateEnvelope.Get(state);
                if(snapshot is null)return false;
                byte[] before=snapshot.Bytes();var ledger=NativeIdentityLedgerCodec.Decode(before,snapshot.Revision);
                if(!NativeExchangeIntentCodec.AlreadyCommitted(ledger,snapshot.Revision,request))return false;
                plan=new(state,request,true,snapshot.Revision,snapshot.Revision,before,before);
                reason="";return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }

        internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason)
        {
            result=null;reason=Removed;
            try
            {
                if(!NativeExchangeCommands.LiveExecution(state))return false;
                if(!TryPlan(state,intent,out var plan,out reason))return false;
                result=new(true,plan!.BeforeRevision,plan.AfterRevision,plan);return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
    }
}
