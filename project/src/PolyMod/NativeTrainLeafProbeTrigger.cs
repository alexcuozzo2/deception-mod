using System;
using System.Collections.Generic;
using System.Text.Json;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // DRAFT: one main-thread private leaf trial following successful fresh
    // behavior creation. Does not load a save, create a client or submit input.
    internal static class NativeTrainLeafProbeTrigger
    {
        internal static bool Requested=>Environment.GetEnvironmentVariable("DECEPTION_NATIVE_TRAIN_LEAF_PROBE")=="1";
        static bool Installed,Attempted;
        static ReadyStamp? Previous;
        static readonly HashSet<string> WaitReasons=new(StringComparer.Ordinal);
        readonly record struct ReadyStamp(long Client,long State,uint Turn,ushort Command,byte Viewer);
        internal static void ValidateMode()
        {
            if(NativeBuildFactoryProbe.Requested&&(!NativeBuildLeafProbe.Requested||!NativeTrainOuterProbe.Requested))
                throw new InvalidOperationException("Build factory trial requires the preceding Build leaf trial and shared Train factory observers.");
            if(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_BUILD_LEAF_PROBE")=="1"&&!Requested)
                throw new InvalidOperationException("Private Build trial requires the preceding fresh Train leaf trial.");
            if(NativeCommandOuterProbe.Requested&&!NativeTrainOuterProbe.Requested)
                throw new InvalidOperationException("Typed command trial requires the preceding native Train outer trial.");
            if(NativeTrainOuterProbe.Requested&&!Requested)
                throw new InvalidOperationException("Outer Train trial requires the preceding fresh Train leaf trial.");
            if(!Requested)return;
            if(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_BEHAVIOR_PROBE")!="1"||
                !Guid.TryParseExact(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_BEHAVIOR_ARM_TOKEN"),"D",out var token)||token==Guid.Empty)
                throw new InvalidOperationException("Train leaf trial requires fresh behavior creation and its exact nonempty arm token.");
            foreach(string mode in new[]{"SESSION","STATE","ENVELOPE","LEDGER","COMMAND","EXCHANGE","RECRUITMENT","PAID_BIRTH","EXECUTION"})
                if(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_"+mode+"_PROBE")=="1")
                    throw new InvalidOperationException("Train leaf trial cannot share "+mode+" mode.");
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_SESSION_FAULT")))
                throw new InvalidOperationException("Train leaf trial cannot share session fault injection.");
        }
        internal static void Install(Harmony harmony)
        {
            if(!Requested)return;ValidateMode();
            if(Installed||NativeStateBridge.Installed||NativeFreshSession.Enabled)
                throw new InvalidOperationException("Train leaf trigger must start before any native bridge/session installation.");
            Native.Hook(harmony,Native.M("GameManager","Update"),typeof(NativeTrainLeafProbeTrigger),null,nameof(Tick));
            Installed=true;Note("armed","Waiting for successful fresh creation and two idle native frames; serializers are not installed yet.");
        }
        static void Note(string suffix,string message)
        {try{Entry.Note("native-train-leaf-probe-"+suffix,message);}catch{/* Evidence cannot escape native Update. */}}
        static bool Wait(out string reason,string why){reason=why;return false;}
        internal static bool Ready(out object? client,out object? state,out string reason)
        {
            client=null;state=null;reason="";
            object? manager=Native.Static("GameManager","instance");
            if(manager is null)return Wait(out reason,"no-manager");
            if((bool)Native.Get(manager,"isLoadingGame")!||(bool)Native.Static("PopupManager","PopupShowing")!)return Wait(out reason,"loading-or-popup");
            if(!NativeBehaviorSession.HasCompletedCreation)return Wait(out reason,"fresh-creation-not-completed");
            client=NativeBehaviorSession.RequireCreatedClient();
            if(!Equals(Native.Get(client,"clientType"),Enum.Parse(Native.T("ClientBase+ClientType"),"Local"))||
                !(bool)Native.Get(client,"IsReady")!||(bool)Native.Get(client,"isSessionEnded")!||
                (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!||
                (bool)Native.Get(client,"IsWaitingForCommand")!)return Wait(out reason,"client-not-ready-local");
            state=Native.Get(client,"GameState");
            if(state is null||!Equals(Native.Get(state,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started"))||
                !Native.Same(state,Native.Static("GameManager","GameState")))return Wait(out reason,"state-not-current-started");
            if(Native.Int(Native.Get(state,"Version"))!=123)return Wait(out reason,"version-not-123");
            object? player=Native.Static("GameManager","LocalPlayer");
            if(player is null)return Wait(out reason,"local-player-absent");
            byte viewer=Convert.ToByte(Native.Get(player,"Id"));
            if(Native.Int(Native.Get(state,"CurrentPlayer"))!=viewer||
                !(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!)return Wait(out reason,"not-viewing-current-player");
            object? actionManager=Native.Get(client,"ActionManager");
            if(actionManager is null||(bool)Native.Get(actionManager,"IsProcessing")!||
                !Native.Same(Native.Get(actionManager,"GameState"),state))return Wait(out reason,"action-manager-not-idle-current");
            object? actions=Native.Get(state,"ActionStack"),history=Native.Get(state,"CommandStack"),pending=Native.Get(state,"pendingCommandTriggers");
            if(actions is null||history is null)return Wait(out reason,"required-native-queue-absent");
            if(Native.Int(Native.Get(actions,"Count"))!=0)return Wait(out reason,"pending-actions");
            if(pending is not null&&Native.Int(Native.Get(pending,"Count"))!=0)return Wait(out reason,"pending-command-trigger");
            if((bool)Native.Invoke(Native.M("GameState","HasQueuedCommands"),state)!)return Wait(out reason,"queued-commands");
            return true;
        }
        public static void Tick()
        {
            if(!Installed||Attempted)return;
            try
            {
                if(!Ready(out var client,out var state,out string reason))
                {Previous=null;if(WaitReasons.Count<16&&WaitReasons.Add(reason))Note("wait",reason);return;}
                var stamp=new ReadyStamp(Native.Pointer(client!),Native.Pointer(state!),Convert.ToUInt32(Native.Get(state!,"CurrentTurn")),
                    Convert.ToUInt16(Native.Get(state!,"CurrentCommand")),Convert.ToByte(Native.Get(state!,"CurrentPlayer")));
                if(Previous!=stamp){Previous=stamp;return;}
                Attempted=true;Previous=null;Note("start","Exact fresh behavior client idle; raw native baselines precede lazy Train-only installation.");
                var leaves=NativeTrainLeafProbe.Run(client!,state!,123);Note("result",JsonSerializer.Serialize(leaves));
                bool priorPassed=leaves.Passed;
                if(NativeTrainOuterProbe.Requested&&priorPassed)
                {
                    var outer=NativeTrainOuterProbe.Run(client!,state!,leaves);
                    Entry.Note("native-train-outer-probe-result",JsonSerializer.Serialize(outer));priorPassed=outer.Passed;
                    if(NativeCommandOuterProbe.Requested&&priorPassed)
                    {
                        var command=NativeCommandOuterProbe.Run(client!,state!,leaves);
                        Entry.Note("native-command-outer-probe-result",JsonSerializer.Serialize(command));priorPassed=command.Passed;
                    }
                }
                if(NativeBuildLeafProbe.Requested&&priorPassed)
                {
                    var build=NativeBuildLeafProbe.Run(client!,state!,leaves);
                    Entry.Note("native-build-leaf-probe-result",JsonSerializer.Serialize(build));
                    if(NativeBuildFactoryProbe.Requested&&build.Passed)
                        Entry.Note("native-build-factory-probe-result",JsonSerializer.Serialize(NativeBuildFactoryProbe.Run(client!,state!,build,leaves)));
                }
            }
            catch(Exception e){Attempted=true;Previous=null;Note("error",e.ToString());}
        }
    }
}
