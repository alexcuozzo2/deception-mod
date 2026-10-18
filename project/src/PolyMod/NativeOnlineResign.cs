using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // The admitted preview's resignation coroutine has two native crossings
    // outside its Compute/FeedFollowUps leaves. Keep its network protocol and
    // shared send lock, but dispatch native work in synchronous, bounded pieces.
    internal static class NativeOnlineResign
    {
        const BindingFlags Static=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static SemaphoreSlim? SendLock;
        static MethodInfo GetShadow=null!,SetShadow=null!,DropShadow=null!,Fetch=null!,Compute=null!,Upload=null!,Success=null!,Failure=null!,ResponseMessage=null!;
        static MethodInfo GuidText=null!,Account=null!,Decode=null!,Player=null!,CurrentPlayer=null!,PlayerId=null!,Client=null!,CurrentGame=null!,HasValue=null!,Value=null!,DecodeCommand=null!,Receive=null!,SetResult=null!;
        static ConstructorInfo Resign=null!;
        static FieldInfo UpdateField=null!,FollowUpsField=null!,StateField=null!;
        static bool Installed;
        static readonly Dictionary<string,MethodInfo> Leaves=new();
        sealed class Request
        {
            internal object NativeGame=null!,NativeAccount=null!;
            internal string Id="",AccountId="";
            internal Guid Epoch;
            internal byte[]? Shadow;
            internal bool DropOnFailure;
        }
        sealed class Prepared
        {
            internal object Command=null!,Update=null!;
            internal List<object> Commands=new();
            internal byte[] State=Array.Empty<byte>();
        }
        sealed class Completion
        {
            internal readonly object Source;
            internal bool Attempted;
            internal bool UploadStarted;
            internal Completion(object source){Source=source;}
        }
        static void Require(bool valid,string reason)
        {if(!valid)throw new InvalidDataException("Deception online resign: "+reason);}
        static MethodInfo Exact(Type owner,string name,Type result,bool isStatic,params Type[] args)
        {
            var found=owner.GetMethods(isStatic?Static:Instance).Where(m=>m.Name==name&&!m.IsGenericMethodDefinition&&
                m.ReturnType==result&&m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(args)).ToArray();
            Require(found.Length==1,"Signature differs: "+owner.FullName+"."+name);return found[0];
        }
        static Type Required(Assembly assembly,string name)=>assembly.GetType(name,false)??throw new TypeLoadException(name);
        // Caller has already admitted the exact preview hash. Resolve everything
        // before patching; this adapter never patches an async state-machine body.
        internal static void Install(Harmony harmony,Assembly exactPreview)
        {
            Require(!Installed,"Adapter already installed.");
            Type owner=Required(exactPreview,"PolyMod.Multiplayer.ModdedClient"),update=Required(exactPreview,"PolyMod.Multiplayer.ViewModels.ModdedGameStateViewModel");
            Type guid=Native.T("Il2CppSystem.Guid"),command=Native.T("CommandBase"),state=Native.T("GameState"),player=Native.T("PlayerState");
            Type bytes=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
            Type response=Native.T("PolytopiaBackendBase.ServerResponse`1").MakeGenericType(Native.T("PolytopiaBackendBase.ResponseViewModel"));
            Type source=Native.T("Il2CppSystem.Threading.Tasks.TaskCompletionSource`1").MakeGenericType(response);
            Type nullable=Native.T("Il2CppSystem.Nullable`1").MakeGenericType(guid),list=Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(command);
            var target=Exact(owner,"HandleModdedResign",typeof(Task),true,source,guid);
            var gate=owner.GetField("_sendLock",Static);
            Require(gate is not null&&gate.FieldType==typeof(SemaphoreSlim),"Shared send lock differs.");
            SendLock=(SemaphoreSlim?)gate!.GetValue(null);Require(SendLock is not null,"Missing shared send lock.");
            GetShadow=Exact(owner,"GetShadowState",typeof(byte[]),true,typeof(string));
            SetShadow=Exact(owner,"SetShadowState",typeof(void),true,typeof(string),typeof(byte[]));
            DropShadow=Exact(owner,"DropShadowState",typeof(void),true,typeof(string));
            Fetch=Exact(owner,"FetchShadowState",typeof(Task<byte[]>),true,guid);
            var tuple=typeof(ValueTuple<,,>).MakeGenericType(update,typeof(List<byte[]>),typeof(byte[]));
            Compute=Exact(owner,"ComputeModdedUpdate",tuple,true,typeof(byte[]),typeof(string),command,typeof(string),typeof(int));
            UpdateField=tuple.GetField("Item1")!;FollowUpsField=tuple.GetField("Item2")!;StateField=tuple.GetField("Item3")!;
            Upload=Exact(owner,"UploadModdedUpdate",typeof(Task<bool>),true,update);
            Success=Exact(owner,"SuccessResponse",response,true);Failure=Exact(owner,"FailedResponse",response,true);
            ResponseMessage=Exact(response,"set_ErrorMessage",typeof(void),false,typeof(string));
            GuidText=Exact(guid,"ToString",typeof(string),false);
            Account=Exact(Native.T("AccountManager"),"get_PlayerAccountId",guid,true);
            Player=Exact(state,"TryGetPlayer",typeof(bool),false,guid,player.MakeByRefType());
            CurrentPlayer=Exact(state,"get_CurrentPlayer",typeof(byte),false);PlayerId=Exact(player,"get_Id",typeof(byte),false);
            Client=Exact(Native.T("GameManager"),"get_Client",Native.T("ClientBase"),true);
            CurrentGame=Exact(Native.T("ClientBase"),"get_CurrentGameId",nullable,false);
            HasValue=Exact(nullable,"get_HasValue",typeof(bool),false);Value=Exact(nullable,"get_Value",guid,false);
            DecodeCommand=Exact(command,"FromByteArray",typeof(bool),true,bytes,command.MakeByRefType(),typeof(int).MakeByRefType());
            Receive=Exact(Native.T("ClientBase"),"ReceiveCommand",Native.T("Il2CppSystem.Threading.Tasks.Task`1").MakeGenericType(typeof(bool)),false,list);
            SetResult=Exact(source,"SetResult",typeof(void),false,response);
            Resign=Native.T("ResignCommand").GetConstructor(new[]{typeof(byte),typeof(byte),typeof(byte),typeof(bool)})??throw new MissingMethodException("ResignCommand(Byte,Byte,Byte,Boolean)");
            var decoders=Native.T("SerializationHelpers").GetMethods(Static).Where(m=>m.Name=="FromByteArray"&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==1)
                .Select(m=>m.MakeGenericMethod(state)).Where(m=>m.ReturnType==typeof(bool)&&m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{bytes,state.MakeByRefType(),typeof(int).MakeByRefType()})).ToArray();
            Require(decoders.Length==1,"State decoder differs.");Decode=decoders[0];
            foreach(var name in new[]{nameof(Capture),nameof(StartFetch),nameof(PrepareResign),nameof(StartUpload),nameof(ApplyResign),nameof(Drop),nameof(Complete),nameof(ReportCommittedFailure)})
                Leaves.Add(name,typeof(NativeOnlineResign).GetMethod(name,Static)!);
            harmony.Patch(target,prefix:new HarmonyMethod(typeof(NativeOnlineResign).GetMethod(nameof(Before),Static)!){priority=Priority.First});
            Installed=true;
        }
        public static bool Before(object __0,object __1,ref Task __result,bool __runOriginal)
        {
            if(!__runOriginal)return false;
            if(!Installed||!NativeOnlinePreview.Available)return true;
            __result=Run(new Completion(__0),__1);return false;
        }
        static object? OnMain(string name,params object?[] args)=>NativeOnlineDispatch.Invoke(Leaves[name],args);
        static void Main()=>Require(NativeOnlineDispatch.IsMainThread,"Native work escaped the game thread.");
        static async Task Run(Completion completion,object nativeGame)
        {
            bool acquired=false,committed=false;
            try
            {
                await SendLock!.WaitAsync().ConfigureAwait(false);acquired=true;
                var request=(Request)OnMain(nameof(Capture),nativeGame)!;
                var shadow=request.Shadow;
                if(shadow is null)shadow=await ((Task<byte[]>)OnMain(nameof(StartFetch),request)!).ConfigureAwait(false);
                if(shadow is null){OnMain(nameof(Complete),completion,false,null);return;}
                var prepared=(Prepared?)OnMain(nameof(PrepareResign),request,shadow);
                if(prepared is null){if(request.DropOnFailure)OnMain(nameof(Drop),request);OnMain(nameof(Complete),completion,false,null);return;}
                completion.UploadStarted=true;
                bool uploaded=await ((Task<bool>)OnMain(nameof(StartUpload),request,prepared)!).ConfigureAwait(false);
                if(!uploaded){OnMain(nameof(Drop),request);OnMain(nameof(Complete),completion,false,null);return;}
                committed=true;
                OnMain(nameof(ApplyResign),request,prepared);
                OnMain(nameof(Complete),completion,true,null);
            }
            catch(Exception error)
            {
                // A native completion is attempted once. Repeating SetResult
                // after a thrown completion can corrupt its state.
                if(!completion.Attempted)
                {
                    // An accepted upload cannot be undone by a local replay or
                    // menu/account change. Report success so callers do not
                    // retry a resignation already committed by the server.
                    if(committed){OnMain(nameof(ReportCommittedFailure),error.Message);OnMain(nameof(Complete),completion,true,null);}
                    else OnMain(nameof(Complete),completion,false,error.Message);
                }
                else throw;
            }
            finally{if(acquired)SendLock!.Release();}
        }
        static string Id(object guid)
        {
            string text=(string)Native.Invoke(GuidText,guid)!;
            Require(Guid.TryParseExact(text,"D",out var parsed)&&parsed!=Guid.Empty,"Invalid game/account identity.");return parsed.ToString("D");
        }
        static Request Capture(object nativeGame)
        {
            Main();object account=Native.Invoke(Account,null)!;string id=Id(nativeGame);
            return new Request{NativeGame=nativeGame,NativeAccount=account,Id=id,Epoch=Guid.ParseExact(id,"D"),AccountId=Id(account),
                Shadow=Native.Invoke(GetShadow,null,id) is byte[] bytes?(byte[])bytes.Clone():null};
        }
        static void SameAccount(Request request)=>Require(Id(Native.Invoke(Account,null)!)==request.AccountId,"Account changed during resignation.");
        // Opaque async handles leave these synchronous starters, never an
        // authority scope. Their existing preview network continuations remain.
        static object StartFetch(Request request){Main();SameAccount(request);return Native.Invoke(Fetch,null,request.NativeGame)!;}
        static object StartUpload(Request request,Prepared prepared){Main();SameAccount(request);return Native.Invoke(Upload,null,prepared.Update)!;}
        static Prepared? PrepareResign(Request request,byte[] shadow)
        {
            Main();SameAccount(request);shadow=(byte[])shadow.Clone();
            using(NativeOnlineSession.BeginDetached(request.Epoch,shadow,"preview-resign"))
            {
                object?[] decoded={NativeStateProbe.NativeBytes(shadow),null,0};
                if(!(bool)Native.Invoke(Decode,null,decoded)!||decoded[1] is null)return null;
                Require((int)decoded[2]! ==123,"Wrong state version.");
                object?[] found={request.NativeAccount,null};
                if(!(bool)Native.Invoke(Player,decoded[1],found)!||found[1] is null)return null;
                request.DropOnFailure=true;
                // The native ctor's third argument is a reason byte, not bool.
                object command=Resign.Invoke(new[]{Native.Invoke(CurrentPlayer,decoded[1]),Native.Invoke(PlayerId,found[1]),(object)(byte)0,false});
                object tuple=Native.Invoke(Compute,null,shadow,request.Id,command,request.AccountId,-1)!;
                object? update=UpdateField.GetValue(tuple);if(update is null)return null;
                var followUps=FollowUpsField.GetValue(tuple) as List<byte[]>;
                var state=StateField.GetValue(tuple) as byte[];
                Require(followUps is not null&&state is not null,"Incomplete computed update.");
                var commands=new List<object>{command};
                foreach(var bytes in followUps!)
                {
                    object?[] follow={NativeStateProbe.NativeBytes(bytes),null,0};
                    Require((bool)Native.Invoke(DecodeCommand,null,follow)!&&follow[1] is not null&&(int)follow[2]! ==123,"Invalid resign follow-up command.");
                    commands.Add(follow[1]!);
                }
                // Validate the entire command list before any server commit.
                // Only opaque native commands, never GameState or PlayerState,
                // survive this detached simulation scope.
                return new Prepared{Command=command,Update=update,Commands=commands,State=(byte[])state!.Clone()};
            }
        }
        static void Drop(Request request){Main();Native.Invoke(DropShadow,null,request.Id);}
        static void ApplyResign(Request request,Prepared prepared)
        {
            Main();Native.Invoke(SetShadow,null,request.Id,prepared.State);
            // Server commit still succeeds when no matching game is open. Never
            // feed an unrelated/newly opened client or a newly signed-in account.
            if(!Matches(Native.Invoke(Account,null)!,request.AccountId))return;
            object? client=Native.Invoke(Client,null);if(client is null)return;
            object? current=Native.Invoke(CurrentGame,client);
            if(current is null||!(bool)Native.Invoke(HasValue,current)!||!Matches(Native.Invoke(Value,current)!,request.Id))return;
            // The entire list was validated before upload. As in the
            // audited preview, ReceiveCommand owns its asynchronous completion.
            Native.Invoke(Receive,client,Native.NewList("CommandBase",prepared.Commands));
        }
        static bool Matches(object guid,string id)=>string.Equals((string?)Native.Invoke(GuidText,guid),id,StringComparison.OrdinalIgnoreCase);
        static void ReportCommittedFailure(string error){Main();Entry.Note("online-resign-live-refresh","Server accepted resignation; reopen the match to refresh its local view. "+error);}
        static void Complete(Completion completion,bool success,string? error)
        {
            Main();Require(!completion.Attempted,"Completion was repeated.");completion.Attempted=true;
            if(error is not null)Entry.Note("online-resign-failed",error);
            // The native menu awaits this task in an async-void handler. A
            // fault escapes that handler and causes the game's fatal-error
            // shutdown. Its normal failed-response branch hides the loader
            // and shows the backend error popup. Keep rejection as a result;
            // no failed preparation may report success or upload a command.
            object response=Native.Invoke(success?Success:Failure,null)!;
            if(!success&&error is not null)
            {
                string message=!completion.UploadStarted&&error.StartsWith("Online snapshot compatibility contract differs",StringComparison.Ordinal)
                    ?"Could not resign: this game's synchronization data does not match the loaded mod setup. No resignation was sent. Keep the game and send the latest log to Alex."
                    :"Could not confirm the resignation. Reopen the game to check its status before trying again.";
                Native.Invoke(ResponseMessage,response,message);
            }
            Native.Invoke(SetResult,completion.Source,response);
        }
    }
}
