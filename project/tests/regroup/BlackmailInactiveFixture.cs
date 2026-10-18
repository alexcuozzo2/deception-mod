using System;
using Deception.Core;
namespace Deception.PolyModAdapter
{
    // Regroup callback tests contain no Blackmail operations or liens.
    // The actual unit projection is exercised in blackmail-presentation-0368;
    // the actual economy/identity adapter in blackmail-0368.
    internal static class NativeBlackmail
    {
        internal static LedgerBlackmail[] Contracts(object state)=>Array.Empty<LedgerBlackmail>();
        internal static LedgerBlackmail[] All(object state)=>Array.Empty<LedgerBlackmail>();
        internal static object? ResolveUnit(object state,LedgerBlackmail mark)=>null;
    }
}
