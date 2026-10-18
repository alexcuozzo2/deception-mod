using System;
using System.Collections.Generic;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    internal static partial class NativeGameStateEnvelope
    {
        // Immutable binding data. A replacement/rollback gets a new Snapshot,
        // even if its revision happens to match a prior binding. No native
        // object or authorization result is retained by this read cache.
        internal sealed class Snapshot
        {
            internal long Revision { get; }
            readonly byte[] Payload;
            readonly Lazy<ReadView> Presentation;
            sealed record ReadView(Guid Epoch,Dictionary<uint,LedgerUnit> Identities);
            internal Snapshot(long revision,byte[] payload)
            {
                Revision=revision;Payload=(byte[])payload.Clone();
                Presentation=new Lazy<ReadView>(()=>
                {
                    var ledger=NativeIdentityLedgerCodec.Decode(Payload,Revision);
                    var identities=new Dictionary<uint,LedgerUnit>();
                    foreach(var row in ledger.Units)
                        if(row.Alive&&row.Independent)identities.Add(row.NativeId,row);
                    // Do not retain receipt/history and ordinary unit rows.
                    return new ReadView(ledger.Epoch,identities);
                });
            }
            internal byte[] Bytes()=>(byte[])Payload.Clone();
            internal Guid Epoch=>Presentation.Value.Epoch;
            static LedgerAppearance Copy(LedgerAppearance a)=>a with {Effects=(string[])a.Effects.Clone(),Passenger=a.Passenger is LedgerAppearance cargo?Copy(cargo):null};
            internal LedgerUnit? ReadIdentity(uint nativeId)
            {
                if(!Presentation.Value.Identities.TryGetValue(nativeId,out var row))return null;
                return row with {Appearance=row.Appearance is LedgerAppearance a?Copy(a):null};
            }
            internal LedgerAppearance? ReadAppearance(uint nativeId)=>
                Presentation.Value.Identities.TryGetValue(nativeId,out var row)&&row.Appearance is LedgerAppearance a?Copy(a):null;
        }
    }
}
