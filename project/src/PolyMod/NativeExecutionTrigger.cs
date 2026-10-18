using System;
using System.Collections.Generic;
using System.Text.Json;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // One detached execution trial in an explicitly armed, successfully created
    // fresh Local match. No prior save is accepted; normal launches are unchanged.
    internal static class NativeExecutionTrigger
    {
        internal static bool PaidRequested=>Environment.GetEnvironmentVariable("DECEPTION_NATIVE_PAID_BIRTH_PROBE")=="1";
        internal static bool Requested=>Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXECUTION_PROBE")=="1"||PaidRequested;
        static bool Installed,Attempted,TickSeen;
        static Stamp? Previous;
        static readonly HashSet<string> WaitReasons=new(StringComparer.Ordinal);
        readonly record struct Stamp(long State,uint Turn,ushort Cursor);
        internal static void ValidateMode()
        {
            if(!Requested)return;
            if(PaidRequested&&Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXECUTION_PROBE")=="1")
                throw new InvalidOperationException("Paid-birth and ordinary execution trials require separate native runs.");
            if(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_SESSION_PROBE")!="1"||
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_SESSION_FAULT")))
                throw new InvalidOperationException("Execution probe requires successful fresh-session mode without injected creation faults.");
            foreach(string mode in new[]{"STATE","ENVELOPE","LEDGER","COMMAND","EXCHANGE","RECRUITMENT"})
                if(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_"+mode+"_PROBE")=="1")
                    throw new InvalidOperationException("Execution probe cannot share an older diagnostic mode.");
        }
        internal static void Install(Harmony harmony)
        {
            if(!Requested)return;ValidateMode();
            if(Installed||!NativeFreshSession.Enabled)throw new InvalidOperationException("Execution probe startup ownership differs.");
            NativeExecutionBridge.Install(NativeExecutionProbe.Router);
            if(PaidRequested)
                Native.Hook(harmony,Native.M("ActionUtils","TrainUnit","GameState","PlayerState","TileData","Polytopia.Data.UnitData"),
                    typeof(NativePaidBirthObservation),nameof(NativePaidBirthObservation.BeforeTrainUnit),
                    nameof(NativePaidBirthObservation.AfterTrainUnit),nameof(NativePaidBirthObservation.TrainUnitFinished));
            Native.Hook(harmony,Native.M("GameManager","Update"),typeof(NativeExecutionTrigger),null,nameof(Tick));
            Installed=true;Entry.Note("native-execution-probe-armed",PaidRequested?
                "Private paid-birth trial armed; waiting for actual research, funds and an empty city in the newly enrolled disposable.":
                "Four typed native sites installed. Waiting for two idle frames in the exact newly enrolled disposable; no trial has run.");
        }
        public static void Tick()
        {
            if(!Installed||Attempted)return;
            try
            {
                if(!TickSeen){TickSeen=true;Entry.Note("native-execution-probe-first-tick","Native GameManager.Update reached the installed diagnostic postfix.");}
                if(!NativeFreshSession.TryGetExecutionSource(out var source,out var reason))
                {
                    Previous=null;
                    if(WaitReasons.Count<16&&WaitReasons.Add(reason))Entry.Note("native-execution-probe-wait",reason);
                    return;
                }
                if(PaidRequested&&!NativeExecutionProbe.PaidReady(source!,out reason))
                {
                    Previous=null;
                    if(WaitReasons.Count<16&&WaitReasons.Add(reason))Entry.Note("native-paid-birth-probe-wait",reason);
                    return;
                }
                var stamp=new Stamp(Native.Pointer(source!),Convert.ToUInt32(Native.Get(source!,"CurrentTurn")),Convert.ToUInt16(Native.Get(source!,"CurrentCommand")));
                if(Previous!=stamp){Previous=stamp;return;}
                Attempted=true;Previous=null;
                Entry.Note("native-execution-probe-start","Native source idle; private state copies only; no command submitted to the live client.");
                var result=PaidRequested?NativeExecutionProbe.RunPaid(source!,123):NativeExecutionProbe.Run(source!,123);
                Entry.Note(PaidRequested?"native-paid-birth-probe-result":"native-execution-probe-result",JsonSerializer.Serialize(result));
            }
            catch(Exception e)
            {Attempted=true;Previous=null;Entry.Note("native-execution-probe-error",e.ToString());}
        }
    }
}
