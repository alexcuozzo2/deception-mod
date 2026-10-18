using System;
using System.Text.Json;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // OPT-IN DETACHED DIAGNOSTIC: disabled during normal launches.
    // The explicit process opt-in and exact known disposable match are both
    // required. This never opens a save, clears gameplay/presentation state,
    // submits a command, creates a client, or publishes a detached GameState.
    internal static class NativeStateProbeTrigger
    {
        const string OptIn="DECEPTION_NATIVE_STATE_PROBE";
        const string DisposableGameId="5e0a1602-1fca-4a35-b7d9-1efb983dd06f";
        static bool Hooked,Attempted,EnvelopeMode,LedgerMode,CommandMode,ExchangeMode;
        static string EventPrefix=>ExchangeMode?"native-exchange-probe":CommandMode?"native-command-probe":LedgerMode?"native-ledger-probe":EnvelopeMode?"native-envelope-probe":"native-state-probe";
        static Action<string,string>? Record;
        static ReadyStamp? Previous;

        readonly record struct ReadyStamp(long Client,long State,int Version,uint Turn,ushort Command,byte Viewer);

        // Existing corruption probes deliberately overwrite only their freshly
        // seeded private receivers. Each read needs an exact one-use permit;
        // the ownership policy still rejects published or adopted receivers.
        internal static IDisposable PermitSeededPrivateRead(object receiver)
        {
            if(!Hooked||!Attempted)throw new InvalidOperationException("No active disposable native diagnostic.");
            object client=Native.Static("GameManager","Client")??throw new InvalidOperationException("Missing disposable diagnostic client.");
            return NativeLedgerOwnership.PermitSeededPrivateRead(client,new Guid(DisposableGameId),receiver);
        }

        // Pass the existing diagnostic logger (for example Entry.Note). No
        // report destination or filesystem operation is selected by this draft.
        public static bool Install(Harmony harmony,Action<string,string> record)
        {
            bool detached=Environment.GetEnvironmentVariable(OptIn)=="1";
            bool envelope=Environment.GetEnvironmentVariable("DECEPTION_NATIVE_ENVELOPE_PROBE")=="1";
            bool ledger=Environment.GetEnvironmentVariable("DECEPTION_NATIVE_LEDGER_PROBE")=="1";
            bool command=Environment.GetEnvironmentVariable("DECEPTION_NATIVE_COMMAND_PROBE")=="1";
            bool exchange=Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXCHANGE_PROBE")=="1";
            if(!detached&&!envelope&&!ledger&&!command&&!exchange)return false;
            if((detached?1:0)+(envelope?1:0)+(ledger?1:0)+(command?1:0)+(exchange?1:0)!=1)throw new InvalidOperationException("Select one native diagnostic mode per process.");
            EnvelopeMode=envelope;LedgerMode=ledger;CommandMode=command;ExchangeMode=exchange;
            if(Hooked)throw new InvalidOperationException("Native state probe trigger is already installed.");
            Record=record??throw new ArgumentNullException(nameof(record));
            Validate();
            Native.Hook(harmony,Native.M("GameManager","Update"),typeof(NativeStateProbeTrigger),null,nameof(Tick));
            Hooked=true;
            Note(EventPrefix+"-armed","Explicit opt-in; waiting for the exact local disposable match to become idle. No probe has run.");
            return true;
        }

        static void Validate()
        {
            _=Native.M("GameManager","get_Client");_=Native.M("GameManager","get_GameState");
            _=Native.M("GameManager","get_LocalPlayer");_=Native.M("GameManager","get_isLoadingGame");
            _=Native.M("GameManager","IsPlayerViewing","System.Byte");
            foreach(string name in new[]{"get_clientType","get_IsReady","get_isSessionEnded","get_IsReplay","get_IsSpectating",
                "get_IsWaitingForCommand","get_ActionManager","get_GameState","get_gameId"})_=Native.M("ClientBase",name);
            _=Native.M("ClientActionManager","get_IsProcessing");_=Native.M("PopupManager","get_PopupShowing");
            _=Native.M("Il2CppSystem.Guid","ToString");_=Native.M("PlayerState","get_Id");
            foreach(string name in new[]{"get_Version","get_CurrentState","get_CurrentTurn","get_CurrentCommand",
                "get_CurrentPlayer","get_ActionStack","get_pendingCommandTriggers"})_=Native.M("GameState",name);
            _=Enum.Parse(Native.T("ClientBase+ClientType"),"Local");_=Enum.Parse(Native.T("GameState+State"),"Started");
        }

        static bool Ready(object manager,out object? state,out ReadyStamp stamp)
        {
            state=null;stamp=default;
            if((bool)Native.Get(manager,"isLoadingGame")!||(bool)Native.Static("PopupManager","PopupShowing")!)return false;
            var client=Native.Static("GameManager","Client");
            if(client is null)return false;
            if(!Equals(Native.Get(client,"clientType"),Enum.Parse(Native.T("ClientBase+ClientType"),"Local"))||
                !(bool)Native.Get(client,"IsReady")!||(bool)Native.Get(client,"isSessionEnded")!||
                (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!||
                (bool)Native.Get(client,"IsWaitingForCommand")!)return false;
            state=Native.Get(client,"GameState");
            if(state is null||!Equals(Native.Get(state,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started"))||
                !Native.Same(Native.Static("GameManager","GameState"),state))return false;
            // GameManager.LocalPlayer may depend on an initialized client/state.
            // Menu-time absence is expected waiting, not a consumed attempt.
            var player=Native.Static("GameManager","LocalPlayer");
            if(player is null)return false;
            var actionManager=Native.Get(client,"ActionManager");
            if(actionManager is null||(bool)Native.Get(actionManager,"IsProcessing")!)return false;
            byte viewer=checked((byte)Native.Int(Native.Get(player,"Id")));
            if(Native.Int(Native.Get(state,"CurrentPlayer"))!=viewer||
                !(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!)return false;
            foreach(string name in new[]{"ActionStack","pendingCommandTriggers"})
            {
                var queue=Native.Get(state,name);
                if(queue is null||Native.Int(Native.Get(queue,"Count"))!=0)return false;
            }
            // CommandStack is native history, so it need not be empty. The
            // detached probe separately rejects custom Smuggle history before
            // serialization; no mechanic registry is cleared to make it pass.
            var gameId=Native.Get(client,"gameId");
            string? id=gameId is null?null:(string?)Native.Invoke(Native.M("Il2CppSystem.Guid","ToString"),gameId);
            if(!string.Equals(id,DisposableGameId,StringComparison.OrdinalIgnoreCase))return false;
            int version=Native.Int(Native.Get(state,"Version"));
            if(version<0)return false;
            stamp=new(Native.Pointer(client),Native.Pointer(state),version,
                Convert.ToUInt32(Native.Get(state,"CurrentTurn")),Convert.ToUInt16(Native.Get(state,"CurrentCommand")),viewer);
            return true;
        }

        // Postfix of the native GameManager.Update. No worker thread, task,
        // timer, await, or UI callback can start this diagnostic.
        public static void Tick(object __instance)
        {
            if(!Hooked||Attempted)return;
            try
            {
                if(!Ready(__instance,out var state,out var stamp)){Previous=null;return;}
                if(Previous!=stamp){Previous=stamp;return;}
                // Consume the one attempt BEFORE native serialization or the
                // logger can reenter. Never rearm on menu/reload or failure.
                Attempted=true;Previous=null;
                Note(EventPrefix+"-start","Exact disposable local match; two consecutive ready frames; source version "+stamp.Version+".");
                NativeStateProbe.Report report;
                if(CommandMode||ExchangeMode)
                {
                    var transport=NativeEnvelopeProbe.Run(state!,stamp.Version);
                    Note("native-command-transport-regression",JsonSerializer.Serialize(transport));
                    if(!transport.Passed)throw new InvalidOperationException("Native transport regression failed before the command trial.");
                    report=NativeCommandProbe.Run(state!,stamp.Version);
                    if(ExchangeMode)
                    {
                        Note("native-exchange-command-regression",JsonSerializer.Serialize(report));
                        if(!report.Passed)throw new InvalidOperationException("Native command transport failed before the exchange trial.");
                        report=NativeExchangeProbe.Run(state!,stamp.Version);
                    }
                }
                else if(LedgerMode)
                {
                    var transport=NativeEnvelopeProbe.Run(state!,stamp.Version);
                    Note("native-ledger-transport-regression",JsonSerializer.Serialize(transport));
                    if(!transport.Passed)throw new InvalidOperationException("Native transport regression failed before the ledger trial.");
                    report=NativeLedgerProbe.Run(state!,stamp.Version);
                }
                else report=EnvelopeMode?NativeEnvelopeProbe.Run(state!,stamp.Version):NativeStateProbe.RunDetachedStateProbe(state!,stamp.Version);
                Note(EventPrefix+"-result",JsonSerializer.Serialize(report));
                if(ExchangeMode&&report.Passed)
                {
                    object client=Native.Static("GameManager","Client")??throw new InvalidOperationException("Disposable client disappeared before ownership trial.");
                    var ownership=NativeOwnershipProbe.Run(client,new Guid(DisposableGameId),state!,stamp.Version);
                    Note("native-ownership-probe-result",JsonSerializer.Serialize(ownership));
                }
            }
            catch(Exception e)
            {
                Attempted=true;Previous=null;
                Note(EventPrefix+"-error",e.GetType().FullName+": "+e.Message+"; no automatic retry.");
            }
        }
        static void Note(string kind,string message)
        {try{Record?.Invoke(kind,message);}catch{/* A diagnostic sink must not throw into native Update. */}}
    }
}
