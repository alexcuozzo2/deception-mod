using System;
using System.Collections.Generic;
using System.Text.Json;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Read-only, opt-in evidence for one native empty-city recruitment path.
    // This never attaches a ledger, allocates a native ID, or changes an action.
    internal static class NativeRecruitmentProbe
    {
        const string DisposableGameId="5e0a1602-1fca-4a35-b7d9-1efb983dd06f";
        const int MaximumObservations=16,MaximumMessages=40,MaximumDepth=8;
        static bool Configured,Enabled;
        static int Observations,Messages;
        static Action<string,string>? Record;
        [ThreadStatic] static Scope? Current;

        sealed class Scope
        {
            internal Scope? Parent;
            internal int Depth,Number,Cost,BeforeCurrency,HelperCalls;
            internal uint BeforeUnitId,Turn;
            internal ushort Command;
            internal byte Owner;
            internal bool Observed,Empty,Postfix,OriginalRan,HelperMatched;
            internal string Type="",Error="";
            internal object Action=null!,State=null!,Client=null!,Manager=null!,Player=null!,Tile=null!,Data=null!;
            internal object? Spawn;
            internal Point Coordinates;
            internal HashSet<uint> BeforeIds=new();
        }
        readonly record struct Point(int X,int Y);
        static Point Position(object coordinates)=>new(Native.Int(Native.Get(coordinates,"X")),Native.Int(Native.Get(coordinates,"Y")));

        public static bool Configure(Action<string,string> record)
        {
            if(Configured)throw new InvalidOperationException("Recruitment probe is already configured.");
            Configured=true;
            if(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_RECRUITMENT_PROBE")!="1")return false;
            Record=record??throw new ArgumentNullException(nameof(record));
            Enabled=true;
            Note("native-recruitment-probe-armed","Read-only; exact disposable local match only; at most 16 observed Mimic TrainActions. No ledger receipt is installed.");
            return true;
        }

        // Unlike the detached serializer trigger, training requires an active
        // action manager. Reject simulation and a different/publication state,
        // but do not require an empty action stack or an idle action manager.
        // IsWaitingForCommand is true during ordinary local action processing;
        // it is an idle-probe gate, not a valid gate for this active observer.
        static bool Context(object state,byte owner,out object? client,out object? manager,out object? player)
        {
            client=null;manager=null;player=null;
            var gameManager=Native.Static("GameManager","instance"); // Raw static field getter; never the constructing Instance getter.
            if(gameManager is null||(bool)Native.Get(gameManager,"isLoadingGame")!)return false;
            client=Native.Static("GameManager","Client");
            if(client is null||!Equals(Native.Get(client,"clientType"),Enum.Parse(Native.T("ClientBase+ClientType"),"Local"))||
                !(bool)Native.Get(client,"IsReady")!||(bool)Native.Get(client,"isSessionEnded")!||
                (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!)return false;
            var gameId=Native.Get(client,"gameId");
            string? id=gameId is null?null:(string?)Native.Invoke(Native.M("Il2CppSystem.Guid","ToString"),gameId);
            if(!string.Equals(id,DisposableGameId,StringComparison.OrdinalIgnoreCase))return false;
            if(Native.Int(Native.Get(state,"Version"))!=123||
                !Equals(Native.Get(state,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started"))||
                !Native.Same(Native.Get(client,"GameState"),state)||!Native.Same(Native.Static("GameManager","GameState"),state))return false;
            manager=Native.Get(client,"ActionManager");
            if(manager is null||(bool)Native.Get(manager,"IsSimulating")!||!Native.Same(Native.Get(manager,"GameState"),state))return false;
            player=Native.Static("GameManager","LocalPlayer");
            if(player is null||Native.Int(Native.Get(player,"Id"))!=owner||Native.Int(Native.Get(state,"CurrentPlayer"))!=owner||
                !(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,owner)!)return false;
            object?[] args={owner,null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&Native.Same(args[1],player);
        }

        public static void BeforeTrain(object __instance,object __0,out object? __state)
        {
            __state=null;
            if(!Enabled)return;
            try
            {
                // Every nested TrainAction gets a frame, even when it is an
                // ordinary/non-observed action. Its helper cannot be credited
                // to the enclosing custom action.
                var s=new Scope{Parent=Current,Depth=(Current?.Depth??0)+1,Action=__instance,State=__0};
                Current=s;__state=s;
                if(s.Depth>MaximumDepth||Observations>=MaximumObservations||!RecruitmentHooks.Registered)return;
                s.Type=Native.EnumName(Native.Get(__instance,"Type")!);
                if(!RecruitmentHooks.Forms.ContainsKey(s.Type))return;
                s.Owner=Convert.ToByte(Native.Get(__instance,"PlayerId"));
                if(!Context(__0,s.Owner,out var client,out var manager,out var player))return;
                s.Observed=true;s.Number=++Observations;s.Client=client!;s.Manager=manager!;s.Player=player!;
                s.Cost=Native.Int(Native.Get(__instance,"Cost"));
                s.BeforeCurrency=Native.Int(Native.Get(s.Player,"Currency"));
                s.BeforeUnitId=Convert.ToUInt32(Native.Get(__0,"CurrentUnitId"));
                s.Turn=Convert.ToUInt32(Native.Get(__0,"CurrentTurn"));
                s.Command=Convert.ToUInt16(Native.Get(__0,"CurrentCommand"));
                var coordinates=Native.Get(__instance,"Coordinates")!;s.Coordinates=Position(coordinates);
                s.Tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(__0,"Map"),coordinates)!;
                if(s.Tile is null){s.Error="missing-native-tile";return;}
                s.Empty=Native.Get(s.Tile,"unit") is null;
                s.Data=RecruitmentHooks.Data(__0,s.Type);
                s.BeforeIds=new HashSet<uint>(NativeLedgerState.Graph(__0).Keys);
            }
            catch(Exception e){Fault(Current,"capture",e);}
        }

        // Postfix of ActionUtils.TrainUnit. It is shared by several native
        // mechanics, so only the exact enclosing observed purchase can match.
        public static void Spawned(object __0,object __1,object __2,object __3,object? __result)
        {
            if(!Enabled||Current is not Scope s||!s.Observed)return;
            try
            {
                s.HelperCalls++;
                if(s.HelperCalls!=1){s.Error="multiple-native-spawn-helpers";return;}
                s.HelperMatched=Native.Same(__0,s.State)&&Native.Same(__1,s.Player)&&Native.Same(__2,s.Tile)&&Native.Same(__3,s.Data);
                s.Spawn=__result;
                if(!s.HelperMatched)s.Error="native-spawn-arguments-differ";
            }
            catch(Exception e){Fault(s,"spawn-observation",e);}
        }

        public static void AfterTrain(object __instance,object __0,bool __runOriginal,object? __state)
        {
            if(__state is not Scope s||!s.Observed)return;
            try
            {
                s.Postfix=true;s.OriginalRan=__runOriginal;
                if(!ReferenceEquals(Current,s)||!Native.Same(__instance,s.Action)||!Native.Same(__0,s.State))s.Error="action-scope-mismatch";
            }
            catch(Exception e){Fault(s,"action-return",e);}
        }

        public static Exception? TrainFinished(Exception? __exception,object? __state)
        {
            if(__state is not Scope s)return __exception;
            try
            {
                if(s.Observed)
                {
                    if(!ReferenceEquals(Current,s))s.Error="unbalanced-action-scope";
                    Report(s,__exception);
                }
            }
            catch(Exception e){Fault(s,"completion",e);}
            finally
            {
                // No native wrappers survive the completed scope. Never change
                // the exception returned by the original native action.
                Current=s.Parent;s.Parent=null;s.Spawn=null;
            }
            return __exception;
        }

        static void Report(Scope s,Exception? error)
        {
            string status="unsupported",reason=s.Error;
            int afterCurrency=Native.Int(Native.Get(s.Player,"Currency"));
            uint afterId=Convert.ToUInt32(Native.Get(s.State,"CurrentUnitId"));
            bool aborted=(bool)Native.Get(s.Action,"trainActionAborted")!;
            uint? spawnedId=s.Spawn is null?null:Convert.ToUInt32(Native.Get(s.Spawn,"id"));
            Point? home=s.Spawn is null?null:Position(Native.Get(s.Spawn,"home")!);
            bool context=Context(s.State,s.Owner,out var client,out var manager,out var player)&&
                Native.Same(client,s.Client)&&Native.Same(manager,s.Manager)&&Native.Same(player,s.Player)&&
                Convert.ToUInt32(Native.Get(s.State,"CurrentTurn"))==s.Turn&&Convert.ToUInt16(Native.Get(s.State,"CurrentCommand"))==s.Command;
            if(error is not null){status="native-exception";reason=error.GetType().FullName??"native exception";}
            else if(!s.Postfix||!s.OriginalRan){status="not-executed";reason="original-did-not-complete";}
            else if(!s.Empty||aborted){status="deferred-or-occupied";reason="ordinary-empty-tile-path-not-observed";}
            else if(reason.Length!=0){status="observer-rejected";}
            else if(!context)reason="local-state-context-changed";
            else if(s.Cost!=RecruitmentCatalog.Cost||s.BeforeCurrency<RecruitmentCatalog.Cost||(long)s.BeforeCurrency-afterCurrency!=RecruitmentCatalog.Cost)reason="exact-purchase-debit-not-observed";
            else if(s.HelperCalls!=1||!s.HelperMatched||s.Spawn is null)reason="exact-single-spawn-return-not-observed";
            else if(s.BeforeUnitId==uint.MaxValue||spawnedId!=s.BeforeUnitId+1||afterId!=spawnedId||s.BeforeIds.Contains(spawnedId.Value))reason="new-native-id-sequence-differs";
            else
            {
                var unit=s.Spawn;
                var graph=NativeLedgerState.Graph(s.State);
                bool graphMatches=graph.Count==s.BeforeIds.Count+1&&graph.TryGetValue(spawnedId!.Value,out var body)&&body.Root&&Native.Same(body.Unit,unit);
                if(graphMatches)foreach(uint id in s.BeforeIds)if(!graph.ContainsKey(id)){graphMatches=false;break;}
                if(!graphMatches||!Native.Same(Native.Get(s.Tile,"unit"),unit))reason="spawn-not-exact-new-map-root";
                else if(Native.EnumName(Native.Get(unit,"type")!)!=s.Type||!Native.Same(Native.Get(unit,"UnitData"),s.Data)||
                    Native.Int(Native.Get(unit,"owner"))!=s.Owner||Position(Native.Get(unit,"coordinates")!)!=s.Coordinates||home!=s.Coordinates)
                    reason="spawn-identity-profile-or-native-home-differs";
                else if(Native.Int(Native.Get(unit,"health"))!=10||!(bool)Native.Get(unit,"moved")!||!(bool)Native.Get(unit,"attacked")!||
                    Native.Get(unit,"passengerUnit") is not null||(bool)Native.Invoke(Native.M("UnitState","HasLeader"),unit)!||
                    (bool)Native.Invoke(Native.M("UnitState","HasFollower"),unit)!)reason="unsupported-health-readiness-or-multibody";
                else{status="observed-paid1-empty-tile";reason="Exact native spawn and completed payment observed; no ledger receipt attached.";}
            }
            Note("native-recruitment-probe-result",JsonSerializer.Serialize(new{
                observation=s.Number,status,reason,type=s.Type,owner=s.Owner,turn=s.Turn,command=s.Command,coordinates=s.Coordinates,
                cost=s.Cost,currencyBefore=s.BeforeCurrency,currencyAfter=afterCurrency,wasEmpty=s.Empty,aborted,
                originalRan=s.OriginalRan,postfixReached=s.Postfix,helperCalls=s.HelperCalls,helperArgumentsMatched=s.HelperMatched,
                beforeUnitId=s.BeforeUnitId,afterUnitId=afterId,spawnedId,home,ledgerAttached=false
            }));
        }

        static void Fault(Scope? s,string phase,Exception error)
        {
            try
            {
                if(s is not null)s.Error="observer-error-"+phase;
                Note("native-recruitment-probe-error",phase+": "+error.GetType().FullName+": "+error.Message+"; no receipt attached.");
            }
            catch{/* Diagnostic failures must not cross a native callback. */}
        }
        static void Note(string kind,string message)
        {try{if(Messages++<MaximumMessages)Record?.Invoke(kind,message);}catch{/* The diagnostic sink cannot affect training. */}}
    }
}
