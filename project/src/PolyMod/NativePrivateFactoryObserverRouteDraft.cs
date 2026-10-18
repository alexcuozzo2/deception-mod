using System;
namespace Deception.PolyModAdapter
{
    // Proposed single Harmony owner route only. No installation or activation.
    // Each component contains its own failures and never suppresses originals.
    internal static class NativePrivateFactoryObserverRouteDraft
    {
        sealed class State{internal object? Train,Build;}
        public static void BeforeCommand(object __0,out object? __state)
        {
            var state=new State();__state=state;
            NativeTrainFactoryTraversal.BeforeCommand(__0,out state.Train);
            NativeBuildFactoryTraversalDraft.BeforeCommand(__0,out state.Build);
        }
        public static void BeforeAction(object __0,out object? __state)
        {
            var state=new State();__state=state;
            NativeTrainFactoryTraversal.BeforeAction(__0,out state.Train);
            NativeBuildFactoryTraversalDraft.BeforeAction(__0,out state.Build);
        }
        public static void AfterFactory(object? __result,bool __runOriginal,object? __state)
        {
            if(__state is not State state)return;
            NativeTrainFactoryTraversal.AfterFactory(__result,__runOriginal,state.Train);
            NativeBuildFactoryTraversalDraft.AfterFactory(__result,__runOriginal,state.Build);
        }
        public static Exception? FactoryFinished(Exception? __exception,object? __state)
        {
            if(__state is State state)
            {
                NativeTrainFactoryTraversal.FactoryFinished(__exception,state.Train);
                NativeBuildFactoryTraversalDraft.FactoryFinished(__exception,state.Build);
            }
            return __exception;
        }
    }
}
