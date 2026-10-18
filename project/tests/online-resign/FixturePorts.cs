using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace HarmonyLib
{
    public static class Priority{public const int First=800;}
    public sealed class HarmonyMethod{public MethodInfo method;public int priority;public HarmonyMethod(MethodInfo m){method=m;}}
    public sealed class Harmony{public MethodInfo? Original;public HarmonyMethod? Prefix;public void Patch(MethodInfo original,HarmonyMethod prefix){Original=original;Prefix=prefix;}}
}
namespace Il2CppSystem
{
    public sealed class Action{readonly System.Action Value;public Action(System.Action value){Value=value;}public void Invoke()=>Value();}
    public readonly struct Guid{readonly System.Guid Value;public Guid(System.Guid value){Value=value;}public override string ToString(){Deception.PolyModAdapter.Native.MainOnly();return Value.ToString("D");}}
    public sealed class Nullable<T>{public bool HasValue{get;set;}public T Value{get;set;}=default!;}
    public sealed class Exception:System.Exception{public Exception(string text):base(text){Deception.PolyModAdapter.Native.MainOnly();}}
}
namespace Il2CppSystem.Threading.Tasks
{
    public sealed class Task<T>
    {
        readonly System.Threading.Tasks.Task<T> Value;
        public Task():this(System.Threading.Tasks.Task.FromResult(default(T)!)){}
        internal Task(System.Threading.Tasks.Task<T> value){Value=value;}
        public System.Runtime.CompilerServices.TaskAwaiter<T> GetAwaiter()=>Value.GetAwaiter();
    }
    public sealed class TaskCompletionSource<T>
    {
        readonly System.Threading.Tasks.TaskCompletionSource<T> Completion=new();
        public Task<T> Task=>new(Completion.Task);
        public ResignPopupProbe? Popup;
        public T? Result;public Il2CppSystem.Exception? Error;public int Completions;public bool ThrowCompletion;
        public void SetResult(T result){Deception.PolyModAdapter.Native.MainOnly();Completions++;if(ThrowCompletion)throw new System.Exception("completion failed");Result=result;Completion.SetResult(result);}
        public void SetException(Il2CppSystem.Exception error){Deception.PolyModAdapter.Native.MainOnly();Completions++;Error=error;Completion.SetException(error);}
    }
}
namespace Il2CppSystem.Collections.Generic{public sealed class List<T>:System.Collections.Generic.List<T>{}}
namespace Il2CppSystem.Collections.Concurrent
{
    public sealed class ConcurrentQueue<T>{readonly System.Collections.Concurrent.ConcurrentQueue<T> Values=new();public void Enqueue(T value)=>Values.Enqueue(value);public bool TryDequeue(out T value)=>Values.TryDequeue(out value!);}
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays{public sealed class Il2CppStructArray<T>{public T[] Value;public Il2CppStructArray(T[] v){Value=v;}}}
namespace PolytopiaBackendBase{public sealed class ResponseViewModel{} public sealed class ServerResponse<T>{public bool Success;public string ErrorMessage{get;set;}="";}}
// Mirrors the audited native async popup order: await response, hide loader,
// then show backend error for Success=false. A fault skips both UI statements.
public sealed class ResignPopupProbe
{
    public bool Loader=true;public int FailurePopups,Successes;
    public Task Completion=Task.CompletedTask;
    public async Task Observe(Il2CppSystem.Threading.Tasks.Task<PolytopiaBackendBase.ServerResponse<PolytopiaBackendBase.ResponseViewModel>> result)
    {
        var response=await result;
        Fixture.CheckMain();Loader=false;
        if(response.Success)Successes++;else FailurePopups++;
    }
}
public static class TeslaDispatchComponent
{
    public static Il2CppSystem.Collections.Concurrent.ConcurrentQueue<Il2CppSystem.Action> Queue=new();
    public static Il2CppSystem.Collections.Concurrent.ConcurrentQueue<Il2CppSystem.Action> get__taskQueue()=>Queue;
    public static void Enqueue(Il2CppSystem.Action action)=>Queue.Enqueue(action);
    public static void Drain(){while(Queue.TryDequeue(out var action))action.Invoke();}
}
public static class AccountManager{public static Il2CppSystem.Guid PlayerAccountId{get;set;}}
public sealed class PlayerState{public byte Id{get;set;}=2;}
public sealed class GameState
{
    public byte CurrentPlayer{get;set;}=1;
    public bool TryGetPlayer(Il2CppSystem.Guid id,out PlayerState? player){Fixture.CheckScope();player=Fixture.MissingPlayer?null:new PlayerState();return player is not null;}
}
public class CommandBase
{
    public static bool FromByteArray(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> bytes,out CommandBase? command,out int version)
    {Fixture.CheckMain();Fixture.CommandDecodes++;command=bytes.Value[0]==0?null:new CommandBase();version=bytes.Value[0]==124?124:123;return command is not null;}
}
public sealed class ResignCommand:CommandBase
{
    public byte Issuer,Target,Reason;public bool Flag;
    public ResignCommand(byte issuer,byte target,byte reason,bool flag){Fixture.CheckScope();Issuer=issuer;Target=target;Reason=reason;Flag=flag;Fixture.LastResign=this;}
}
public static class SerializationHelpers
{
    public static bool FromByteArray<T>(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> bytes,out T? value,out int version)
    {Fixture.CheckScope();Fixture.Decodes++;value=Fixture.DecodeFalse?default:(T)(object)new GameState();version=Fixture.Version;return !Fixture.DecodeFalse;}
}
public sealed class ClientBase
{
    public Il2CppSystem.Nullable<Il2CppSystem.Guid> CurrentGameId{get;set;}=new(){HasValue=true};
    public Il2CppSystem.Threading.Tasks.Task<bool> ReceiveCommand(Il2CppSystem.Collections.Generic.List<CommandBase> commands)
    {Fixture.CheckMain();if(Deception.PolyModAdapter.NativeOnlineSession.Active!=0)throw new System.Exception("Detached scope escaped into live client");Fixture.Receives++;Fixture.Received=commands.ToArray();if(Fixture.ReceiveThrows)throw new System.Exception("receive failed");return new();}
}
public static class GameManager{public static ClientBase? Client{get;set;}}
public static class Fixture
{
    public static readonly System.Guid Epoch=new("899f553a-5e9c-479e-96ee-8a3d716d6027"),AccountId=new("378f8382-8b64-4369-bd28-1c5611c9a45f"),Other=new("c35c9313-3106-4a41-b6f5-386fcd944bfb");
    public static byte[]? Shadow;public static bool MissingPlayer,DecodeFalse,ComputeNull,ComputeThrows,ReceiveThrows,ComputeChangesAccount;
    public static int Version=123,Decodes,CommandDecodes,Computes,Fetches,Uploads,Drops,Sets,Receives;public static ResignCommand? LastResign;public static CommandBase[]? Received;
    public static System.Threading.Tasks.TaskCompletionSource<byte[]>? FetchGate;
    public static System.Threading.Tasks.TaskCompletionSource<bool>? UploadGate;
    public static List<byte[]> FollowUps=new();public static readonly List<string> Events=new();
    public static void CheckMain()=>Deception.PolyModAdapter.Native.MainOnly();
    public static void CheckScope(){CheckMain();if(Deception.PolyModAdapter.NativeOnlineSession.Active!=1)throw new System.Exception("Missing exact detached authority");}
    public static void Reset()
    {
        Shadow=new byte[]{1,123};MissingPlayer=DecodeFalse=ComputeNull=ComputeThrows=ReceiveThrows=ComputeChangesAccount=false;Version=123;
        Decodes=CommandDecodes=Computes=Fetches=Uploads=Drops=Sets=Receives=0;LastResign=null;Received=null;FetchGate=null;UploadGate=null;FollowUps=new(){new byte[]{1},new byte[]{2}};Events.Clear();
        AccountManager.PlayerAccountId=new(AccountId);GameManager.Client=new(){CurrentGameId=new(){HasValue=true,Value=new(Epoch)}};
        Deception.PolyModAdapter.NativeOnlineSession.Reset();Deception.PolyModAdapter.Entry.Notes.Clear();
    }
}
namespace PolyMod.Multiplayer.ViewModels{public sealed class ModdedGameStateViewModel{}}
namespace PolyMod.Multiplayer
{
    public static class ModdedClient
    {
        public static readonly SemaphoreSlim _sendLock=new(1,1);
        static Task HandleModdedResign(Il2CppSystem.Threading.Tasks.TaskCompletionSource<PolytopiaBackendBase.ServerResponse<PolytopiaBackendBase.ResponseViewModel>> tcs,Il2CppSystem.Guid guid)=>throw new System.Exception("Original coroutine must be replaced");
        static byte[]? GetShadowState(string id){Fixture.CheckMain();Fixture.Events.Add("get");return Fixture.Shadow;}
        static void SetShadowState(string id,byte[] bytes){Fixture.CheckMain();Fixture.Sets++;Fixture.Events.Add("set");Fixture.Shadow=bytes;}
        static void DropShadowState(string id){Fixture.CheckMain();Fixture.Drops++;Fixture.Events.Add("drop");Fixture.Shadow=null;}
        static Task<byte[]> FetchShadowState(Il2CppSystem.Guid id){Fixture.CheckMain();Fixture.Fetches++;Fixture.Events.Add("fetch");if(Deception.PolyModAdapter.NativeOnlineSession.Active!=0)throw new System.Exception("Fetch holds scope");return Fixture.FetchGate?.Task??Task.FromResult<byte[]>(null!);}
        static (ViewModels.ModdedGameStateViewModel?,List<byte[]>,byte[]) ComputeModdedUpdate(byte[] bytes,string id,CommandBase command,string account,int clock)
        {Fixture.CheckScope();Fixture.Computes++;Fixture.Events.Add("compute");if(clock!=-1||id!=Fixture.Epoch.ToString("D")||account!=Fixture.AccountId.ToString("D"))throw new System.Exception("Compute arguments differ");if(Fixture.ComputeThrows)throw new System.Exception("compute failed");if(Fixture.ComputeChangesAccount)AccountManager.PlayerAccountId=new(Fixture.Other);return (Fixture.ComputeNull?null:new(),Fixture.FollowUps,new byte[]{1,123,9});}
        static Task<bool> UploadModdedUpdate(ViewModels.ModdedGameStateViewModel update){Fixture.CheckMain();Fixture.Uploads++;Fixture.Events.Add("upload");if(Deception.PolyModAdapter.NativeOnlineSession.Active!=0)throw new System.Exception("Upload holds scope");return Fixture.UploadGate?.Task??Task.FromResult(true);}
        static PolytopiaBackendBase.ServerResponse<PolytopiaBackendBase.ResponseViewModel> SuccessResponse(){Fixture.CheckMain();Fixture.Events.Add("success");return new(){Success=true};}
        static PolytopiaBackendBase.ServerResponse<PolytopiaBackendBase.ResponseViewModel> FailedResponse(){Fixture.CheckMain();Fixture.Events.Add("failed");return new(){Success=false,ErrorMessage="Modded game operation failed."};}
    }
}
namespace Deception.PolyModAdapter
{
    internal static class NativeOnlinePreview{internal static bool Available=true;}
    internal static class Entry
    {
        internal static readonly List<(string Kind,string Text)> Notes=new();
        internal static void Note(string kind,string text){Native.MainOnly();Notes.Add((kind,text));}
    }
    internal static class NativeOnlineSession
    {
        internal static int Active,Started,Disposed;
        internal static void Reset(){if(Active!=0)throw new System.Exception("Leaked authority");Started=Disposed=0;}
        sealed class Scope:IDisposable{public void Dispose(){Native.MainOnly();Active--;Disposed++;}}
        internal static IDisposable BeginDetached(System.Guid epoch,byte[] bytes,string purpose)
        {Native.MainOnly();if(epoch!=Fixture.Epoch)throw new InvalidDataException("Online snapshot belongs to a different native match.");if(bytes.Length<2||bytes[0]!=1)throw new InvalidDataException("Missing version123 online state frame.");if(bytes[1]!=123)throw new InvalidDataException("Online snapshot compatibility contract differs.");Active++;Started++;return new Scope();}
    }
    internal static class NativeStateProbe{internal static object NativeBytes(byte[] bytes){Native.MainOnly();return new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte>((byte[])bytes.Clone());}}
    internal static class Native
    {
        internal static int MainThread=Environment.CurrentManagedThreadId;
        internal static void MainOnly(){if(Environment.CurrentManagedThreadId!=MainThread)throw new System.Exception("Native API touched worker thread");}
        internal static Type T(string name){MainOnly();return typeof(Fixture).Assembly.GetType(name)??throw new TypeLoadException(name);}
        internal static MethodInfo M(string type,string name,params string[] args){MainOnly();return T(type).GetMethods(BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).Single(m=>m.Name==name&&m.GetParameters().Select(p=>p.ParameterType.FullName).SequenceEqual(args));}
        internal static object? Invoke(MethodInfo method,object? target,params object?[] args)
        {if(method.Name!="Enqueue")MainOnly();try{return method.Invoke(target,args);}catch(TargetInvocationException e)when(e.InnerException is not null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
        internal static object Callback(System.Action callback){MainOnly();return new Il2CppSystem.Action(callback);}
        internal static object NewList(string name,IEnumerable<object> items){MainOnly();var list=new Il2CppSystem.Collections.Generic.List<CommandBase>();foreach(var item in items)list.Add((CommandBase)item);return list;}
    }
}
