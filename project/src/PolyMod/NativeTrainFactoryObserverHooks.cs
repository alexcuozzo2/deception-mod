using System;
using System.Linq;
using HarmonyLib;
namespace Deception.PolyModAdapter
{
    // Explicit private probe only; never installed during an ordinary launch.
    internal static class NativeTrainFactoryObserverHooks
    {
        static Harmony? Hooks;static bool Attempted;
        internal static bool Installed {get;private set;}
        internal static bool BuildRouteInstalled {get;private set;}
        internal static void Install()
        {
            if(Attempted||Installed)throw new InvalidOperationException("Factory observer installation cannot repeat.");
            if(!NativeTrainOuterProbe.Requested||!NativeTrainLeafProbeTrigger.Requested||!NativeStateBridge.Installed||NativeFreshSession.Enabled)
                throw new InvalidOperationException("Factory observers require the fresh explicit outer diagnostic after its Train bridge.");
            NativeTrainLeafProbeTrigger.ValidateMode();_=NativeBehaviorSession.RequireCreatedClient();Attempted=true;
            var methods=new[]{Native.M("GameState","GetCommand","CommandType"),Native.M("GameState","GetAction","ActionType")};
            for(int i=0;i<methods.Length;i++)
            {
                Native.RequireReturn(methods[i],Native.T(i==0?"CommandBase":"ActionBase"));
                if(!methods[i].IsStatic||methods[i].DeclaringType!=Native.T("GameState")||Harmony.GetPatchInfo(methods[i])?.Owners.Any()==true)
                    throw new InvalidOperationException("Factory signature or existing patch ownership differs.");
            }
            Hooks=new Harmony("alex_deception.private_train_factory_probe");
            bool buildRoute=NativeBuildFactoryProbe.Requested;
            Type route=buildRoute?typeof(NativePrivateFactoryObserverRouteDraft):typeof(NativeTrainFactoryTraversal);
            try
            {
                for(int i=0;i<methods.Length;i++)Native.Hook(Hooks,methods[i],route,
                    i==0?nameof(NativeTrainFactoryTraversal.BeforeCommand):nameof(NativeTrainFactoryTraversal.BeforeAction),
                    nameof(NativeTrainFactoryTraversal.AfterFactory),nameof(NativeTrainFactoryTraversal.FactoryFinished));
                Installed=true;BuildRouteInstalled=buildRoute;
            }
            catch{Hooks.UnpatchSelf();throw;}
        }
    }
}
