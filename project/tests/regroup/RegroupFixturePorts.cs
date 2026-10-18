using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using Polytopia.Data;

namespace Deception.PolyModAdapter
{
    internal static class NativeExecutionTrigger {internal static bool Requested=>false;}
    internal static class NativeRegroupSession
    {
        internal static bool Enabled=true,Faulted,RequirePrepared;
        internal static int PrepareCalls;
        internal static object? PreparedState;
        internal static Exception? PrepareFailure;
        internal static readonly List<string> BoundaryEvents=new();
        internal static readonly List<string> Faults=new();
        internal static void Fault(string reason){BoundaryEvents.Add("fault");Faulted=true;Faults.Add(reason);}
        internal static void Prepare(object state)
        {
            BoundaryEvents.Add("prepare");PrepareCalls++;
            if(PrepareFailure is not null)throw PrepareFailure;
            if(!Native.Same(state,GameManager.GameState))throw new InvalidOperationException("Synthetic wrong supplied state");
            PreparedState=state;
        }
        internal static bool Ready(object state)
        {
            BoundaryEvents.Add("ready");
            return Enabled&&!Faulted&&Native.Same(state,GameManager.GameState)&&
                (!RequirePrepared||Native.Same(state,PreparedState));
        }
    }
    internal static class NativeLedgerState
    {
        internal sealed record Body(object Unit,bool Root);
        internal static LedgerAppearance Appearance()=>new("warrior",new("imperius","default"),new("imperius","default"),new("imperius","default"),0x123456,100,100,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
        internal static int CaptureCalls;
        internal static bool FailCapture;
        // Original-owner Rogue capture is covered by the real ledger fixture.
        internal static LedgerAppearance CapturePublic(object state,Body body,string form,LedgerUnit? row)=>Capture(state,body,form,false);
        internal static LedgerAppearance Capture(object state,Body body,string form,bool mimic)
        {CaptureCalls++;if(FailCapture)throw new InvalidOperationException("Synthetic unsupported recipient capture");return Appearance() with{Form=form,Health=40};}
    }
    internal static class NativeGameStateEnvelope
    {
        internal static long Revision;
        static object? CachedState;static Snapshot? Cached;
        internal static void Rebind(){Cached=null;CachedState=null;}
        internal sealed record Snapshot(long Revision,byte[] Data){internal byte[] Bytes()=>Data.ToArray();}
        internal static Snapshot? Get(object state)
        {
            if(!Native.Same(state,GameManager.GameState))return null;
            if(Native.Same(CachedState,state)&&Cached?.Revision==Revision)return Cached;
            var ledger=new NativeIdentityLedger(new Guid("00112233-4455-6677-8899-aabbccddeeff"),3,1,new[]{
                new LedgerUnit(1,3,true,LedgerActualKind.Mimic,true,NativeLedgerState.Appearance(),PaidInvestmentKind.UnknownLegacy,0),
                new LedgerUnit(2,4,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0)},Array.Empty<LedgerReceipt>());
            CachedState=state;return Cached=new(Revision,NativeIdentityLedgerCodec.Encode(ledger,Revision));
        }
    }
    internal sealed record NativeExchangePlan(NativeExchangeIntent Intent);
    internal static class NativeExchangePlanning
    {
        internal static int Calls;
        internal static bool Available=true,AllowDisguise;
        internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason)
        {
            Calls++;
            NativeRegroupSession.BoundaryEvents.Add("plan");
            plan=null;reason="Synthetic stale intent";
            if(!Available||intent.Kind==NativeExchangeKind.Disguise&&!AllowDisguise)return false;
            if(intent.Revision!=NativeGameStateEnvelope.Revision)return false;
            var game=(GameState)state;
            foreach(var r in new[]{intent.Initiator,intent.Partner})
                if(!game.Units.TryGetValue(r.NativeId,out var u)||u.coordinates.X!=r.X||u.coordinates.Y!=r.Y)return false;
            plan=new(intent);reason="";return true;
        }
    }
    internal static class NativeExchangeCommit
    {
        internal sealed record Result(bool AlreadyCommitted,NativeExchangePlan Plan);
        internal static int Calls;internal static Action? During;
        internal static bool TryCommit(object state,NativeExchangeIntent intent,out Result? result,out string reason)
        {NativeRegroupSession.BoundaryEvents.Add("commit");Calls++;During?.Invoke();result=new(false,new(intent));reason="";return true;}
    }
}
