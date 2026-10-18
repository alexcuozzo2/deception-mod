using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Adapter for the single audited managed preview. These hooks surround only
    // synchronous work: no thread-local admission survives an await or hub call.
    // The session owns frame validation, state ownership and failure handling.
    internal static class NativeOnlinePreview
    {
        internal const string PreviewSha256="61588813d4b62d133db06fad64a2c440869aaffaf7460ec8bc8e9d82b1be63d7";
#if DECEPTION_WINDOWS_PORT_CANDIDATE
        // Wire identity is shared by both peers in the identical candidate
        // archive. Platform-specific binary admission stays in the native table.
        // This intentionally does not match the existing Mac release.
        internal const string ProfileId="polytopia-2.17.3.16375-state123-deception-win-mac-candidate1";
#else
        internal const string ProfileId="polytopia-2.17.3.16375-state123-osx-x64-5391c27cf9e63ba3db10b168ba7435da723853b265695821f31942bb7ab95dc0";
#endif
        const BindingFlags Flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
        static FieldInfo? Checksum;
        static MethodInfo? IsModded,Shadow;
        internal enum ScopeRoute {Creation,Compute,Count,Receive,Summary,LiveFollowUps}
        static readonly Dictionary<MethodBase,ScopeRoute> Routes=new();
        internal static bool Available {get;private set;}

        static void Require(bool value,string reason)
        {if(!value)throw new InvalidDataException("Deception online preview: "+reason);}
        static MethodInfo Exact(Type owner,string name,Type result,params Type[] arguments)
        {
            var methods=owner.GetMethods(Flags|BindingFlags.DeclaredOnly).Where(m=>m.Name==name&&!m.IsGenericMethodDefinition&&
                m.ReturnType==result&&m.GetParameters().Select(p=>p.ParameterType).SequenceEqual(arguments)).ToArray();
            Require(methods.Length==1,"Managed preview signature differs: "+owner.FullName+"."+name);return methods[0];
        }
        static Type RequiredType(Assembly assembly,string name)=>assembly.GetType(name,false)??throw new TypeLoadException(name);
        internal static bool AdmittedLoaderHash(string hash)=>
            string.Equals(hash,PreviewSha256,StringComparison.OrdinalIgnoreCase)||
            (NativeCleanBetaCompatibility.Installed&&string.Equals(hash,NativeCleanBetaCompatibility.CleanSha256,StringComparison.OrdinalIgnoreCase));
        internal static bool Install(Harmony harmony)
        {
            Require(!Available,"Adapter already installed.");
            var candidates=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="PolyMod").ToArray();
            if(candidates.Length!=1)return false;
            var assembly=candidates[0];
            // A different preview remains available to ordinary local gameplay;
            // it cannot enter this adapter's online scopes.
            if(string.IsNullOrEmpty(assembly.Location)||!File.Exists(assembly.Location)||
                !AdmittedLoaderHash(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)))))return false;
            Type multiplayer=RequiredType(assembly,"PolyMod.Multiplayer.ModMultiplayer"),client=RequiredType(assembly,"PolyMod.Multiplayer.ModdedClient");
            Checksum=RequiredType(assembly,"PolyMod.Managers.Compatibility").GetField("checksum",Flags);
            Require(Checksum is not null&&Checksum.FieldType==typeof(string)&&Checksum.IsStatic,"Missing managed compatibility checksum.");
            IsModded=Exact(client,"IsModdedGame",typeof(bool),typeof(string));
            Shadow=Exact(client,"GetShadowState",typeof(byte[]),typeof(string));
            var create=Exact(multiplayer,"CreateMultiplayerGame",typeof(ValueTuple<byte[],string>),Native.T("PolytopiaBackendBase.Game.LobbyGameViewModel"),typeof(int),typeof(int));
            var updateResult=typeof(ValueTuple<,,>).MakeGenericType(RequiredType(assembly,"PolyMod.Multiplayer.ViewModels.ModdedGameStateViewModel"),typeof(List<byte[]>),typeof(byte[]));
            var update=Exact(client,"ComputeModdedUpdate",updateResult,typeof(byte[]),typeof(string),Native.T("CommandBase"),typeof(string),typeof(int));
            var count=Exact(client,"CountCommands",typeof(int),typeof(byte[]));
            var receive=Exact(client,"PolytopiaBackendAdapter_OnCommand",typeof(void),Native.T("PolytopiaBackendBase.Game.CommandArrayViewModel"));
            var followUps=Exact(client,"FeedFollowUpsToLiveClient",typeof(void),typeof(string),typeof(List<byte[]>));
            string bytes=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte)).FullName!;
            var summary=Native.M("GameStateSummary","FromGameStateByteArray",bytes,"GameStateSummary&","GameState&");
            var summaryBytes=Native.M("GameStateSummary","FromGameStateByteArray",bytes);
            Native.RequireReturn(summary,typeof(bool));Native.RequireReturn(summaryBytes,Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte)));
            // Resolve every wrapper before installing any hook.
            _=Native.M("PolytopiaBackendBase.Game.LobbyGameViewModel","get_Id");
            _=Native.M("PolytopiaBackendBase.Game.CommandArrayViewModel","get_GameId");
            _=Native.M("VersionManager","get_GameVersion");_=Native.M("VersionManager","get_GameLogicDataVersion");
            _=Native.M("Il2CppSystem.Guid","ToString");
            NativeOnlineDispatch.Initialize();
            Patch(harmony,create,ScopeRoute.Creation);Patch(harmony,update,ScopeRoute.Compute);
            Patch(harmony,count,ScopeRoute.Count);Patch(harmony,receive,ScopeRoute.Receive);
            Patch(harmony,summary,ScopeRoute.Summary);Patch(harmony,summaryBytes,ScopeRoute.Summary);
            Patch(harmony,followUps,ScopeRoute.LiveFollowUps);
            NativeOnlineResign.Install(harmony,assembly);
            NativeOnlineMaintenance.Install(harmony,assembly);
            Available=true;return true;
        }
        internal static void Patch(Harmony harmony,MethodInfo method,ScopeRoute route)
        {
            Routes.Add(method,route);
            MethodInfo prefix=typeof(NativeOnlinePreview).GetMethod(method.ReturnType==typeof(void)?nameof(BeforeVoid):nameof(BeforeManaged),Flags)!;
            if(prefix.IsGenericMethodDefinition)prefix=prefix.MakeGenericMethod(method.ReturnType);
            harmony.Patch(method,prefix:new HarmonyMethod(prefix){priority=Priority.First},
                finalizer:new HarmonyMethod(typeof(NativeOnlinePreview).GetMethod(nameof(Finished),Flags)!));
        }
        static void BeginScope(MethodBase method,object[] args,out IDisposable? scope)
        {
            scope=null;
            switch(Routes[method])
            {
                case ScopeRoute.Creation:BeforeCreation(args[0],(int)args[1],(int)args[2],out scope);break;
                case ScopeRoute.Compute:BeforeCompute((byte[])args[0],(string)args[1],out scope);break;
                case ScopeRoute.Count:BeforeCount((byte[])args[0],out scope);break;
                case ScopeRoute.Receive:BeforeReceive(args[0],out scope);break;
                case ScopeRoute.Summary:BeforeSummary(args[0],out scope);break;
                case ScopeRoute.LiveFollowUps:break;
                default:throw new InvalidDataException("Unknown online preview scope.");
            }
        }
        // Managed async continuations and the preview's native-hub prefix can
        // run on worker threads. Marshal the entire synchronous leaf, including
        // its scope, to the game thread. Reentry there follows this same prefix.
        // One combined bool prefix ensures skipped originals never open scopes.
        public static bool BeforeManaged<T>(MethodBase __originalMethod,object[] __args,ref T __result,bool __runOriginal,out IDisposable? __state)
        {
            __state=null;if(!__runOriginal)return false;if(!Available)return true;
            Require(__originalMethod is MethodInfo&&Routes.ContainsKey(__originalMethod),"Unregistered managed leaf.");
            if(!NativeOnlineDispatch.IsMainThread)
            {__result=(T)NativeOnlineDispatch.Invoke((MethodInfo)__originalMethod,__args)!;return false;}
            BeginScope(__originalMethod,__args,out __state);return true;
        }
        public static bool BeforeVoid(MethodBase __originalMethod,object[] __args,bool __runOriginal,out IDisposable? __state)
        {
            __state=null;if(!__runOriginal)return false;if(!Available)return true;
            Require(__originalMethod is MethodInfo&&Routes.ContainsKey(__originalMethod),"Unregistered managed leaf.");
            if(!NativeOnlineDispatch.IsMainThread)
            {NativeOnlineDispatch.Invoke((MethodInfo)__originalMethod,__args);return false;}
            BeginScope(__originalMethod,__args,out __state);return true;
        }
        internal static string CurrentContract()
        {
            Require(Available&&Checksum is not null,"The exact managed preview is unavailable.");
            string? checksum=(string?)Checksum!.GetValue(null);
            Require(checksum is not null&&checksum.Length==64&&checksum.All(Uri.IsHexDigit),"PolyMod has not completed its mod compatibility checksum.");
            string canonical="DeceptionOnline\nprotocol=1\npreview="+(NativeCleanBetaCompatibility.Installed?NativeCleanBetaCompatibility.CleanSha256:PreviewSha256)+"\nprofile="+ProfileId+"\nmods="+checksum!.ToUpperInvariant()+"\n";
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }
        internal static bool HasFrame(byte[] bytes)=>bytes is not null&&
            (bytes.Length>=4&&BinaryPrimitives.ReadUInt32LittleEndian(bytes)==NativeStateFrame.Magic||
             bytes.Length>=8&&BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4))==NativeStateFrame.Magic);
        internal static byte[] InnerFrame(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            // SerializationHelpers prepends its own version before calling
            // GameState.Serialize; the raw bridge frame starts after that int.
            // NativeEnvelopeProbe.GenericCopies independently verified this.
            byte[] inner;
            if(bytes.Length>=4&&BinaryPrimitives.ReadUInt32LittleEndian(bytes)==NativeStateFrame.Magic)inner=(byte[])bytes.Clone();
            else
            {
                Require(bytes.Length>=8&&BinaryPrimitives.ReadInt32LittleEndian(bytes)==123&&
                    BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4))==NativeStateFrame.Magic,"Missing version123 online state frame.");
                inner=bytes.AsSpan(4).ToArray();
            }
            _=NativeStateFrame.Decode(inner,123);return inner;
        }
        static Guid MatchId(string? text)
        {Require(Guid.TryParseExact(text,"D",out var id)&&id!=Guid.Empty,"Invalid match Guid.");return id;}
        static Guid NativeGuid(object value)=>MatchId((string?)Native.Invoke(Native.M("Il2CppSystem.Guid","ToString"),value));
        public static void BeforeCreation(object __0,int __1,int __2,out IDisposable? __state)
        {
            __state=null;if(!Available)return;
            Require(__1==123&&__1==Native.Int(Native.Static("VersionManager","GameVersion"))&&
                __2==Native.Int(Native.Static("VersionManager","GameLogicDataVersion")),"Unsupported game or logic version.");
            _=CurrentContract();__state=NativeOnlineSession.BeginCreation(NativeGuid(Native.Get(__0,"Id")!));
        }
        public static void BeforeCompute(byte[] __0,string __1,out IDisposable? __state)
        {__state=null;if(Available)__state=NativeOnlineSession.BeginDetached(MatchId(__1),__0,"preview-update");}
        public static void BeforeCount(byte[] __0,out IDisposable? __state)
        {__state=null;if(Available&&HasFrame(__0))__state=NativeOnlineSession.BeginCount(__0);}
        public static void BeforeReceive(object __0,out IDisposable? __state)
        {
            __state=null;if(!Available)return;
            Guid epoch=NativeGuid(Native.Get(__0,"GameId")!);string id=epoch.ToString("D");
            // Match the preview's own route; this is not compatibility admission.
            // BeginDetached must validate the embedded frame contract before read.
            if(!(bool)Native.Invoke(IsModded!,null,id)!)return;
            if(Native.Invoke(Shadow!,null,id) is byte[] bytes)__state=NativeOnlineSession.BeginDetached(epoch,bytes,"preview-receive");
        }
        public static void BeforeSummary(object __0,out IDisposable? __state)
        {
            __state=null;if(!Available)return;byte[] bytes=NativeStateProbe.ManagedBytes(__0);
            if(HasFrame(bytes))__state=NativeOnlineSession.BeginCount(bytes);
        }
        public static Exception? Finished(Exception? __exception,IDisposable? __state)=>NativeOnlineSession.Finish(__state,__exception);
    }
}
