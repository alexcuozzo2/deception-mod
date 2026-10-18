using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native GameInfoPopup calls SetParticipationDone directly and only hides
    // its popup on success. Unlike RemoteGameDataManager.DeleteGameAsync it
    // does not remove the cached row. Complete that native cache operation only
    // after the existing server request has succeeded for a finished game.
    // No request, response, participant state or compatibility result is changed.
    internal static class NativeOnlineMaintenance
    {
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        const int Capacity=16;
        static MethodInfo IsModded=null!,Manager=null!,Find=null!,Remove=null!,Account=null!,GuidText=null!;
        static MethodInfo Completed=null!,Faulted=null!,Canceled=null!,Result=null!,Success=null!,ErrorCode=null!;
        static readonly List<Completion> Pending=new();
        static bool Installed;
        static long NextClockCheck;
        static string LastClock="";
        static int ClockNotes;
        internal sealed class Completion
        {
            internal object Manager=null!,Game=null!,Summary=null!,Binding=null!,Task=null!;
            internal string Account="",GameId="";
            internal long Started=Stopwatch.GetTimestamp();
        }
        static MethodInfo Exact(Type type,string name,Type result,params Type[] args)
        {
            var found=type.GetMethods(Flags).Where(m=>m.Name==name&&!m.IsGenericMethodDefinition&&
                m.ReturnType==result&&m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(args)).ToArray();
            if(found.Length!=1)throw new InvalidDataException("Online maintenance signature differs: "+type.FullName+"."+name);
            return found[0];
        }
        // The caller admits the exact preview DLL before calling this method.
        internal static void Install(Harmony harmony,Assembly exactPreview)
        {
            if(Installed)throw new InvalidOperationException("Online maintenance already installed.");
            Type guid=Native.T("Il2CppSystem.Guid"),summary=Native.T("PolytopiaBackendBase.Game.GameSummaryViewModel");
            Type manager=Native.T("RemoteGameDataManager"),response=Native.T("PolytopiaBackendBase.ServerResponse`1").MakeGenericType(Native.T("PolytopiaBackendBase.ResponseViewModel"));
            Type task=Native.T("Il2CppSystem.Threading.Tasks.Task`1").MakeGenericType(response);
            Type client=exactPreview.GetType("PolyMod.Multiplayer.ModdedClient",true)!;
            IsModded=Exact(client,"IsModdedGame",typeof(bool),typeof(string));
            Manager=Exact(Native.T("GameManager"),"GetRemoteGameDataManager",manager);
            Account=Exact(Native.T("AccountManager"),"get_PlayerAccountId",guid);
            GuidText=Exact(guid,"ToString",typeof(string));
            Find=Exact(manager,"TryGetGameSummaryViewModel",typeof(bool),guid,summary.MakeByRefType());
            Remove=Exact(manager,"RemoveGameSummary",summary,guid);
            Completed=Exact(task,"get_IsCompleted",typeof(bool));Faulted=Exact(task,"get_IsFaulted",typeof(bool));
            Canceled=Exact(task,"get_IsCanceled",typeof(bool));Result=Exact(task,"get_Result",response);
            Success=Exact(response,"get_Success",typeof(bool));ErrorCode=Exact(response,"get_ErrorCode",Native.T("PolytopiaBackendBase.ErrorCode"));
            var participation=Exact(Native.T("PolytopiaBackendBase.BackendAdapter"),"SetParticipationDone",task,Native.T("PolytopiaBackendBase.Game.SetParticipationDoneBindingModel"));
            var timer=Exact(Native.T("TurnTimerContainer"),"UpdateTimer",typeof(void));
            var tick=Exact(Native.T("GameManager"),"Update",typeof(void));
            // Resolve observational getters before adding any patch.
            foreach(var name in new[]{"State","GameId","Participators"})_=Native.M(summary,"get_"+name);
            foreach(var name in new[]{"UserId","DateCurrentTurnDeadline","DateLastStartTurn","TimeBank"})_=Native.M("PolytopiaBackendBase.Game.ParticipatorViewModel","get_"+name);
            _=Native.M("PolytopiaBackendBase.Game.SetParticipationDoneBindingModel","get_GameId");
            _=Native.M("PolytopiaBackendBase.Game.GameSummaryViewModel","get_DateLastEndTurn");
            foreach(var name in new[]{"UseDynamicTimers","UseTimeBanks"})_=Native.M("GameSettings","get_"+name);
            harmony.Patch(participation,prefix:new HarmonyMethod(typeof(NativeOnlineMaintenance).GetMethod(nameof(BeforeParticipation),Flags)!),
                postfix:new HarmonyMethod(typeof(NativeOnlineMaintenance).GetMethod(nameof(AfterParticipation),Flags)!));
            harmony.Patch(timer,postfix:new HarmonyMethod(typeof(NativeOnlineMaintenance).GetMethod(nameof(ObserveClock),Flags)!));
            harmony.Patch(tick,postfix:new HarmonyMethod(typeof(NativeOnlineMaintenance).GetMethod(nameof(Tick),Flags)!));
            Installed=true;
        }
        static bool Ready=>Installed&&NativeOnlinePreview.Available&&NativeOnlineDispatch.IsMainThread;
        static string Id(object guid)=>(string)Native.Invoke(GuidText,guid)!;
        static bool ValidAccount(string text)=>Guid.TryParseExact(text,"D",out var id)&&id!=Guid.Empty;
        static object? Summary(object manager,object game)
        {
            object?[] args={game,null};
            return (bool)Native.Invoke(Find,manager,args)!?args[1]:null;
        }
        static bool Finished(object summary)=>Native.Int(Native.Get(summary,"State"))==4;
        static void Note(string value)=>Entry.Note("online-maintenance",value);
        public static void BeforeParticipation(object __0,bool __runOriginal,out Completion? __state)
        {
            __state=null;if(!Ready||!__runOriginal)return;
            try
            {
                if(Pending.Count>=Capacity){Note("Finished-game response observer is full; native request retained.");return;}
                object game=Native.Get(__0,"GameId")!;string id=Id(game);
                // IsModdedGame also recognizes persisted signatures from older
                // builds; it does not require today's compatibility checksum.
                if(!(bool)Native.Invoke(IsModded,null,id)!)return;
                object manager=Native.Invoke(Manager,null)!;
                object? summary=Summary(manager,game);
                // The native menu also uses this endpoint for resigned players
                // in active games. Leave those rows entirely to native behavior.
                if(summary is null||!Finished(summary))return;
                string account=Id(Native.Invoke(Account,null)!);if(!ValidAccount(account))return;
                __state=new Completion{Manager=manager,Game=game,GameId=id,Summary=summary,Binding=__0,Account=account};
                Note("Finished-game participation request observed; awaiting its server response.");
            }
            catch{Note("Finished-game request could not be observed; native request retained.");}
        }
        public static void AfterParticipation(object? __result,Completion? __state)
        {
            if(!Ready||__state is null||__result is null)return;
            if(Pending.Count>=Capacity)return;
            try{if(Id(Native.Get(__state.Binding,"GameId")!)!=__state.GameId)return;}
            catch{return;}
            __state.Task=__result;Pending.Add(__state);
        }
        public static void Tick()
        {
            if(!Ready)return;
            for(int i=Pending.Count-1;i>=0;i--)
            {
                var pending=Pending[i];bool done=false;
                try
                {
                    if(Id(Native.Invoke(Account,null)!)!=pending.Account||!Native.Same(Native.Invoke(Manager,null),pending.Manager)||
                        Id(Native.Get(pending.Binding,"GameId")!)!=pending.GameId)
                    {done=true;Note("Finished-game response belongs to an old account or cache; no row changed.");continue;}
                    if(!(bool)Native.Invoke(Completed,pending.Task)!)
                    {
                        if((Stopwatch.GetTimestamp()-pending.Started)/(double)Stopwatch.Frequency>=120)
                        {done=true;Note("Finished-game response still pending after 120 seconds; native request retained.");}
                        continue;
                    }
                    done=true;
                    if((bool)Native.Invoke(Faulted,pending.Task)!||(bool)Native.Invoke(Canceled,pending.Task)!)
                    {Note("Finished-game request faulted or was canceled; cached row retained.");continue;}
                    object? response=Native.Invoke(Result,pending.Task);
                    if(response is null||!(bool)Native.Invoke(Success,response)!)
                    {
                        // Numeric error only: backend error strings can contain
                        // account information, URLs, request bodies or tokens.
                        Note("Finished-game request rejected; cached row retained; code="+(response is null?"none":Native.Int(Native.Invoke(ErrorCode,response)).ToString())+".");continue;
                    }
                    object? summary=Summary(pending.Manager,pending.Game);
                    if(summary is null){Note("Finished-game request succeeded; native cache already removed the row.");continue;}
                    if(!Native.Same(summary,pending.Summary)||!Finished(summary))
                    {Note("Finished-game summary changed before response; no row changed.");continue;}
                    Native.Invoke(Remove,pending.Manager,pending.Game);
                    // RemoveGameSummary itself updates the list, dispatches
                    // GameSummariesUpdated and refreshes actionable-game count.
                    Note("Finished-game request succeeded; completed native cached-row removal. Server listings remain authoritative.");
                }
                catch{done=true;Note("Finished-game response observation failed; native response retained.");}
                finally{if(done)Pending.RemoveAt(i);}
            }
        }
        static bool HasValue(object? nullable)=>nullable is not null&&(bool)Native.Get(nullable,"HasValue")!;
        public static void ObserveClock()
        {
            if(!Ready||ClockNotes>=16||Stopwatch.GetTimestamp()<NextClockCheck)return;
            NextClockCheck=Stopwatch.GetTimestamp()+Stopwatch.Frequency*2;
            try
            {
                object? client=Native.Static("GameManager","Client");if(client is null)return;
                object? nullable=Native.Get(client,"CurrentGameId");if(!HasValue(nullable))return;
                object game=Native.Get(nullable!,"Value")!;string id=Id(game);
                if(!(bool)Native.Invoke(IsModded,null,id)!)return;
                object? manager=Native.Invoke(Manager,null);if(manager is null)return;
                object? summary=Summary(manager,game);if(summary is null)return;
                string account=Id(Native.Invoke(Account,null)!);if(!ValidAccount(account))return;
                object? local=Native.Items(Native.Get(summary,"Participators")!).FirstOrDefault(p=>Id(Native.Get(p,"UserId")!)==account);
                if(local is null)return;
                bool deadline=HasValue(Native.Get(local,"DateCurrentTurnDeadline")),bank=HasValue(Native.Get(local,"TimeBank"));
                bool start=HasValue(Native.Get(local,"DateLastStartTurn")),end=HasValue(Native.Get(summary,"DateLastEndTurn"));
                object? state=Native.Static("GameManager","GameState"),settings=state is null?null:Native.Get(state,"Settings");
                string facts="deadline="+deadline+"; timeBank="+bank+"; turnStart="+start+"; lastEndTurn="+end+
                    "; dynamic="+(settings is null?"unknown":Native.Get(settings,"UseDynamicTimers")!.ToString())+
                    "; useBanks="+(settings is null?"unknown":Native.Get(settings,"UseTimeBanks")!.ToString());
                // Match/account identities only deduplicate in memory. Logs
                // contain booleans, no timestamps, identifiers or user data.
                string key=id+account+facts;if(key==LastClock)return;LastClock=key;ClockNotes++;
                Entry.Note("online-clock-fields",facts+(deadline?"; native deadline available.":"; preview SkipTurn enforcement is unsupported; no replacement clock invented."));
            }
            catch{if(ClockNotes++==0)Entry.Note("online-clock-fields","Clock metadata unavailable; native display retained.");}
        }
    }
}
