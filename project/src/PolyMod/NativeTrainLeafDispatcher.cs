using System;
using System.IO;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using A=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using B=Deception.PolyModAdapter.NativeStateBridge;

namespace Deception.PolyModAdapter
{
    // PRIVATE LEAF DIAGNOSTIC DRAFT. No installation, library loading, raw
    // original invocation, format inference or live/history admission.
    // Rooted bridge callbacks must contain all exceptions from these methods.
    internal static class NativeTrainLeafDispatcher
    {
        internal enum Route { LegacyOriginal,ExactPrivate,ObservedFactory }
        static void Require(bool ok,string why)
        {if(!ok)throw new InvalidDataException("Train leaf dispatcher: "+why);}
        internal static Route Classify(B.Call call,object value,object stream)
        {
            Require(call.Size==40&&call.Site is 3 or 4&&call.Operation is 0 or 1&&
                call.State!=IntPtr.Zero&&call.Stream!=IntPtr.Zero,
                "Invalid typed Train call header.");
            P.Kind kind=call.Site==3?P.Kind.Command:P.Kind.Action;
            Require(Native.T(kind==P.Kind.Command?"TrainCommand":"TrainAction").IsInstanceOfType(value),
                "Bridge must supply its exact typed receiver wrapper.");
            Require(new IntPtr(Native.Pointer(value))==call.State&&new IntPtr(Native.Pointer(stream))==call.Stream,
                "Callback wrappers differ from the current native call.");
            var ownership=C.InspectForDispatch(value,kind,stream);
            Require((ownership&(C.DispatchOwnership.ProtectedCarrier|C.DispatchOwnership.Retired|C.DispatchOwnership.OffOwnerThread|C.DispatchOwnership.IncompleteRegistration))==0,
                "Protected, retired, off-thread or incompletely registered identity cannot enter a native leaf.");
            if(A.MatchesInvocation(call))return Route.ExactPrivate;
            if(NativeTrainFactoryTraversal.MatchesPending(call,value,stream))return Route.ObservedFactory;
            Require(ownership==C.DispatchOwnership.Unrelated,
                "Known carrier or stream has no exact admitted private invocation.");
            // This first diagnostic does not support live legacy custom Train
            // history. In particular, absent payload != safe legacy downgrade.
            string rule=Native.EnumName(Native.Get(value,"Type")!);
            Require(!string.IsNullOrEmpty(rule)&&!rule.StartsWith("dcp_",StringComparison.Ordinal),
                "Custom Train requires an explicit private wire scope.");
            // No version rewrite/123 restriction on unrelated legacy calls.
            // No byte read, seek, binding Get/Forget or stream registration.
            return Route.LegacyOriginal;
        }
        internal static int Prepare(B.Call call,object value,object stream,ref B.Plan plan)
        {
            Require(plan.Cookie==0&&plan.Replacement==IntPtr.Zero,"Prepare plan is not empty.");
            return Classify(call,value,stream) switch
            {
                Route.ExactPrivate=>A.Prepare(call,ref plan),
                Route.ObservedFactory=>NativeTrainFactoryTraversal.Prepare(call,value,stream,ref plan),
                _=>0
            };
        }
        internal static void Finish(B.Call call,B.Plan plan)
        {
            if(NativeTrainFactoryTraversal.OwnsCallback(call)){NativeTrainFactoryTraversal.Finish(call,plan);return;}
            if(plan.Cookie!=0){A.Finish(call,plan);return;}
            Require(plan.Replacement==IntPtr.Zero,"Legacy pass cannot replace its stream.");
            // Empty-plan PASS owns no resources and invokes no managed native
            // getters after the original. The C++ bridge preserves its exact
            // this/stream/signed-version/MethodInfo and original exception.
        }
        internal static void Cleanup(B.Call call,B.Plan plan,int outcome)
        {
            Require(outcome is >=0 and <=5,"Unknown native bridge cleanup outcome.");
            if(NativeTrainFactoryTraversal.OwnsCallback(call)){NativeTrainFactoryTraversal.Cleanup(call,plan,outcome);return;}
            if(plan.Cookie!=0){A.Cleanup(call,plan,outcome);return;}
            Require(plan.Replacement==IntPtr.Zero,"Unprepared/legacy cleanup cannot own a replacement.");
        }
    }
}
