using System;
using B=Deception.PolyModAdapter.NativeStateBridge;
namespace Deception.PolyModAdapter
{
    // Direct constructor-leaf suite has no factory window. The real factory
    // route and its failure paths run in the separate build-factory suite.
    internal static class NativeBuildFactoryTraversalDraft
    {
        internal static bool MatchesPending(B.Call call,object value,object stream)=>false;
        internal static bool OwnsCallback(B.Call call)=>false;
        internal static int Prepare(B.Call call,object value,object stream,ref B.Plan plan)=>throw new InvalidOperationException("Factory route unexpectedly active in constructor suite.");
        internal static void Finish(B.Call call,B.Plan plan)=>throw new InvalidOperationException("Factory route unexpectedly active in constructor suite.");
        internal static void Cleanup(B.Call call,B.Plan plan,int outcome)=>throw new InvalidOperationException("Factory route unexpectedly active in constructor suite.");
    }
}
