using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    internal readonly record struct NativeExchangePosition(int X,int Y);

    // An owned value plan plus exact supplied-state references. Merely holding
    // this object grants no execution authority: the commit adapter must still
    // protect its native preimages and publish both endpoints atomically.
    internal sealed class NativeExchangePlan
    {
        readonly byte[] IntentBytes,BeforeBytes;
        byte[] AfterBytes;
        internal object State {get;}
        internal object? A {get;}
        internal object? B {get;}
        internal object? TileA {get;}
        internal object? TileB {get;}
        internal bool AlreadyCommitted {get;}
        internal int StarCost {get;}
        internal long BeforeRevision {get;}
        internal long AfterRevision {get;}
        internal NativeExchangePosition? FinalA {get;}
        internal NativeExchangePosition? FinalB {get;}
        internal string? FinalMimicCarrierA {get;}
        internal string? FinalMimicCarrierB {get;}
        internal string? FinalTypeA {get;}
        internal string? FinalTypeB {get;}
        internal uint FinalNativeIdA {get;}
        internal uint FinalNativeIdB {get;}
        internal uint AllocatedWrapperA {get;}
        internal uint AllocatedWrapperB {get;}
        internal uint FinalCounter {get;}
        internal object? CommittedA {get;set;}
        internal object? CommittedB {get;set;}
        internal NativeExchangeIntent Intent=>NativeExchangeIntentCodec.Decode(IntentBytes);
        internal byte[] OriginalPayload=>(byte[])BeforeBytes.Clone();
        internal byte[] CandidatePayload=>(byte[])AfterBytes.Clone();
        internal NativeIdentityLedger OriginalLedger=>NativeIdentityLedgerCodec.Decode(BeforeBytes,BeforeRevision);
        internal NativeIdentityLedger CandidateLedger=>NativeIdentityLedgerCodec.Decode(AfterBytes,AfterRevision);
        internal void MergeDiscovery(NativeIdentityLedger ledger)
        {
            // Staged native sight may add discovery provenance and replace a
            // participating active Rogue's retained footprint. Neither change
            // can alter identity, ownership, receipt or action-allowance state.
            var original=CandidateLedger;var intent=Intent;
            var participants=new HashSet<ulong>{intent.Initiator.LogicalId,intent.Partner.LogicalId};
            var originals=original.Units.ToDictionary(row=>row.LogicalId);
            LedgerUnit WithoutStagedSight(LedgerUnit row)
            {
                if(intent.Kind is not (NativeExchangeKind.Regroup or NativeExchangeKind.Disguise or NativeExchangeKind.Subvert)||
                    !participants.Contains(row.LogicalId)||!originals.TryGetValue(row.LogicalId,out var before)||!before.Alive||
                    before.Rogue is not LedgerRogue previous||row.Rogue is not LedgerRogue current||
                    previous.Commander!=intent.Actor||previous.AcquiredTurn!=intent.Turn)return row;
                return row with{Rogue=current with{Sight=previous.Sight,PendingSight=previous.PendingSight}};
            }
            var stripped=ledger with{Discoveries=original.Discoveries,Units=ledger.Units.Select(WithoutStagedSight).ToArray()};
            if(!NativeIdentityLedgerCodec.Encode(stripped,AfterRevision).SequenceEqual(AfterBytes))
                throw new InvalidDataException("Sight merge changed non-discovery exchange state.");
            AfterBytes=NativeIdentityLedgerCodec.Encode(ledger,AfterRevision);
        }

        internal NativeExchangePlan(object state,NativeExchangeIntent intent,bool committed,long beforeRevision,long afterRevision,
            byte[] before,byte[] after,object? a=null,object? b=null,object? tileA=null,object? tileB=null,
            NativeExchangePosition? finalA=null,NativeExchangePosition? finalB=null,string? carrierA=null,string? carrierB=null,
            string? typeA=null,string? typeB=null,uint? idA=null,uint? idB=null,uint wrapperA=0,uint wrapperB=0,uint counter=0,int starCost=0)
        {
            StarCost=starCost;State=state;IntentBytes=NativeExchangeIntentCodec.Encode(intent);AlreadyCommitted=committed;
            BeforeRevision=beforeRevision;AfterRevision=afterRevision;BeforeBytes=(byte[])before.Clone();AfterBytes=(byte[])after.Clone();
            A=a;B=b;TileA=tileA;TileB=tileB;FinalA=finalA;FinalB=finalB;FinalMimicCarrierA=carrierA;FinalMimicCarrierB=carrierB;
            CommittedA=a;CommittedB=b;FinalTypeA=typeA;FinalTypeB=typeB;FinalNativeIdA=idA??intent.Initiator.NativeId;FinalNativeIdB=idB??intent.Partner.NativeId;
            AllocatedWrapperA=wrapperA;AllocatedWrapperB=wrapperB;FinalCounter=counter;
        }
    }

}
