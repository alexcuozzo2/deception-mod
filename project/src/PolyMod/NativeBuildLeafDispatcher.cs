using System;
using System.IO;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using J=Deception.PolyModAdapter.NativeBuildRecordJournalDraft;
using A=Deception.PolyModAdapter.NativeBuildLeafProbeAdapter;
using B=Deception.PolyModAdapter.NativeStateBridge;
namespace Deception.PolyModAdapter
{
    internal static class NativeBuildLeafDispatcher
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Build leaf dispatcher: "+why);}
        internal static int Prepare(B.Call call,object value,object stream,ref B.Plan plan)
        {
            Require(call.Size==40&&call.Site is 1 or 2&&call.Operation is 0 or 1&&call.State!=IntPtr.Zero&&call.Stream!=IntPtr.Zero,
                "Invalid Build call header.");
            Require(Native.T(call.Site==1?"BuildCommand":"BuildAction").IsInstanceOfType(value)&&
                new IntPtr(Native.Pointer(value))==call.State&&new IntPtr(Native.Pointer(stream))==call.Stream,
                "Exact typed receiver/stream differs.");
            Require(plan.Cookie==0&&plan.Replacement==IntPtr.Zero,"Nonempty prepare plan.");
            if(A.MatchesInvocation(call))return A.Prepare(call,ref plan);
            if(NativeBuildFactoryTraversalDraft.MatchesPending(call,value,stream))return NativeBuildFactoryTraversalDraft.Prepare(call,value,stream,ref plan);
            J.RejectUnscopedPointer(call.State.ToInt64());
            C.RejectUnscopedStream(stream);
            Require(!Native.EnumName(Native.Get(value,"Type")!).StartsWith("dcp_",StringComparison.Ordinal),
                "Custom Build requires an exact private invocation.");
            // Untouched ordinary calls retain their actual version, stream,
            // MethodInfo and native exceptions; no binding read/write occurs.
            return 0;
        }
        internal static void Finish(B.Call call,B.Plan plan)
        {if(NativeBuildFactoryTraversalDraft.OwnsCallback(call)){NativeBuildFactoryTraversalDraft.Finish(call,plan);return;}if(plan.Cookie!=0){A.Finish(call,plan);return;}Require(plan.Replacement==IntPtr.Zero,"Legacy Build cannot replace its stream.");}
        internal static void Cleanup(B.Call call,B.Plan plan,int outcome)
        {Require(outcome is >=0 and <=5,"Unknown Build cleanup outcome.");if(NativeBuildFactoryTraversalDraft.OwnsCallback(call)){NativeBuildFactoryTraversalDraft.Cleanup(call,plan,outcome);return;}if(plan.Cookie!=0){A.Cleanup(call,plan,outcome);return;}Require(plan.Replacement==IntPtr.Zero,"Unprepared Build cannot own a stream.");}
    }
}
