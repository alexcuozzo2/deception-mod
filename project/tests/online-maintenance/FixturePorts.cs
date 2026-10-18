using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace HarmonyLib
{
    public sealed class HarmonyMethod{public MethodInfo method;public HarmonyMethod(MethodInfo m){method=m;}}
    public sealed class Harmony
    {
        public readonly List<(MethodInfo Method,HarmonyMethod? Prefix,HarmonyMethod? Postfix)> Patches=new();
        public void Patch(MethodInfo method,HarmonyMethod? prefix=null,HarmonyMethod? postfix=null)=>Patches.Add((method,prefix,postfix));
    }
}
namespace Il2CppSystem
{
    public readonly struct Guid{readonly System.Guid Value;public Guid(System.Guid value){Value=value;}public override string ToString(){Deception.PolyModAdapter.Native.MainOnly();return Value.ToString("D");}}
    public sealed class Nullable<T>{public bool HasValue{get;set;}public T Value{get;set;}=default!;}
}
namespace Il2CppSystem.Threading.Tasks
{
    public sealed class Task<T>
    {
        public bool IsCompleted{get;set;}public bool IsFaulted{get;set;}public bool IsCanceled{get;set;}
        public T Result{get;set;}=default!;
    }
}
namespace PolytopiaBackendBase
{
    public enum ErrorCode{None=0,Denied=7}
    public sealed class ResponseViewModel{}
    public sealed class ServerResponse<T>{public bool Success{get;set;}public ErrorCode ErrorCode{get;set;}public string ErrorMessage{get;set;}="SECRET BODY";}
    public class BackendAdapter
    {
        public Il2CppSystem.Threading.Tasks.Task<ServerResponse<ResponseViewModel>> SetParticipationDone(Game.SetParticipationDoneBindingModel model)
        {Fixture.Requests++;return Fixture.Task;}
    }
}
namespace PolytopiaBackendBase.Game
{
    public sealed class SetParticipationDoneBindingModel{public Il2CppSystem.Guid GameId{get;set;}}
    public sealed class ParticipatorViewModel
    {
        public Il2CppSystem.Guid UserId{get;set;}
        public Il2CppSystem.Nullable<int> DateCurrentTurnDeadline{get;set;}=new();
        public Il2CppSystem.Nullable<int> DateLastStartTurn{get;set;}=new();
        public Il2CppSystem.Nullable<int> TimeBank{get;set;}=new();
    }
    public sealed class GameSummaryViewModel
    {
        public int State{get;set;}=4;public Il2CppSystem.Guid GameId{get;set;}
        public List<ParticipatorViewModel> Participators{get;set;}=new();
        public Il2CppSystem.Nullable<int> DateLastEndTurn{get;set;}=new();
    }
}
public static class AccountManager{public static Il2CppSystem.Guid PlayerAccountId{get;set;}}
public sealed class RemoteGameDataManager
{
    public PolytopiaBackendBase.Game.GameSummaryViewModel? Summary;
    public bool ThrowFind,ThrowRemove;
    public bool TryGetGameSummaryViewModel(Il2CppSystem.Guid id,out PolytopiaBackendBase.Game.GameSummaryViewModel? summary)
    {
        if(ThrowFind)throw new Exception("SECRET find error");summary=Summary;
        return summary is not null&&summary.GameId.ToString()==id.ToString();
    }
    public PolytopiaBackendBase.Game.GameSummaryViewModel? RemoveGameSummary(Il2CppSystem.Guid id)
    {
        if(ThrowRemove)throw new Exception("SECRET removal error");
        var removed=Summary;Summary=null;Fixture.Removals++;Fixture.Events++;return removed;
    }
}
public sealed class ClientBase{public Il2CppSystem.Nullable<Il2CppSystem.Guid> CurrentGameId{get;set;}=new();}
public sealed class GameSettings
{
    public bool UseDynamicTimers{get;set;}=true;public bool UseTimeBanks{get;set;}=true;public float BaseTimeSeconds{get;set;}=8;
}
public sealed class GameState{public GameSettings Settings{get;set;}=new();}
public sealed class GameManager
{
    public static ClientBase? Client{get;set;}public static GameState? GameState{get;set;}
    public static RemoteGameDataManager Manager=new();public static RemoteGameDataManager GetRemoteGameDataManager()=>Manager;
    public void Update(){}
}
public sealed class TurnTimerContainer{public void UpdateTimer(){}}
namespace PolyMod.Multiplayer{public static class ModdedClient{public static bool Modded=true;public static bool IsModdedGame(string id)=>Modded;}}
public static class Fixture
{
    public static readonly System.Guid Game=new("1dc6265b-4413-4fb3-af9d-93d04857f9ac"),Account=new("8df80bfa-bb13-44bc-9358-0eb13bdf2c5a"),Other=new("1f48e62b-d92e-4c08-bfd9-6850078d1bd3");
    public static int Requests,Removals,Events;
    public static Il2CppSystem.Threading.Tasks.Task<PolytopiaBackendBase.ServerResponse<PolytopiaBackendBase.ResponseViewModel>> Task=new();
    public static void Reset()
    {
        Requests=Removals=Events=0;Task=new();Deception.PolyModAdapter.Entry.Notes.Clear();
        Deception.PolyModAdapter.NativeOnlinePreview.Available=true;PolyMod.Multiplayer.ModdedClient.Modded=true;
        AccountManager.PlayerAccountId=new(Account);
        GameManager.Manager=new(){Summary=new(){GameId=new(Game),Participators=new(){new(){UserId=new(Account)}}}};
        GameManager.Client=new(){CurrentGameId=new(){HasValue=true,Value=new(Game)}};GameManager.GameState=new();
        var t=typeof(Deception.PolyModAdapter.NativeOnlineMaintenance);
        ((System.Collections.IList)t.GetField("Pending",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!).Clear();
        t.GetField("NextClockCheck",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,0L);
        t.GetField("LastClock",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,"");
        t.GetField("ClockNotes",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,0);
    }
}
namespace Deception.PolyModAdapter
{
    internal static class Entry{internal static readonly List<string> Notes=new();internal static void Note(string kind,string text){Native.MainOnly();Notes.Add(kind+": "+text);}}
    internal static class NativeOnlinePreview{internal static bool Available=true;}
    internal static class NativeOnlineDispatch{internal static bool IsMainThread=>Environment.CurrentManagedThreadId==Native.MainThread;}
    internal static class Native
    {
        internal static readonly int MainThread=Environment.CurrentManagedThreadId;
        internal static void MainOnly(){if(Environment.CurrentManagedThreadId!=MainThread)throw new Exception("Native API touched worker thread");}
        internal static Type T(string name){MainOnly();return typeof(Fixture).Assembly.GetType(name)??throw new TypeLoadException(name);}
        internal static MethodInfo M(string owner,string name,params string[] args)=>M(T(owner),name,args);
        internal static MethodInfo M(Type owner,string name,params string[] args){MainOnly();return owner.GetMethods(BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Single(m=>m.Name==name&&m.GetParameters().Select(p=>p.ParameterType.FullName).SequenceEqual(args));}
        internal static object? Invoke(MethodInfo method,object? target,params object?[] args)
        {MainOnly();try{return method.Invoke(target,args);}catch(TargetInvocationException e)when(e.InnerException is not null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
        internal static object? Get(object target,string name){MainOnly();return target.GetType().GetProperty(name)!.GetValue(target);}
        internal static object? Static(string type,string name){MainOnly();return T(type).GetProperty(name)!.GetValue(null);}
        internal static bool Same(object? a,object? b){MainOnly();return a is not null&&ReferenceEquals(a,b);}
        internal static int Int(object? value)=>Convert.ToInt32(value);
        internal static IEnumerable<object> Items(object value){MainOnly();return ((System.Collections.IEnumerable)value).Cast<object>();}
    }
}
