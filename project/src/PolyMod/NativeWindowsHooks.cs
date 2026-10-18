#if DECEPTION_WINDOWS_PORT_CANDIDATE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Il2CppInterop.Runtime;

namespace Deception.PolyModAdapter
{
    // MSVC folds identical native bodies. Independent Harmony patchers on one
    // address can form a trampoline cycle and reinterpret unrelated receivers.
    internal static class NativeWindowsHooks
    {
        internal static bool Enabled=>RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        static readonly Dictionary<IntPtr,MethodInfo> Owners=new();
        static readonly HashSet<(MethodInfo,Type,string?,string?,string?)> Installed=new();
        static bool Ready;
        static long ImageBase;
        static IntPtr Pointer(MethodInfo method)=>(IntPtr)Native.T("Il2CppInterop.Runtime.Injection.InjectorHelpers")
            .GetMethod("GetIl2CppMethodPointer",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(MethodBase)},null)!
            .Invoke(null,new object[]{method})!;
        static string Digest(string path)
        {
            using var stream=File.OpenRead(path);using var hash=SHA256.Create();
            return Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant();
        }
        static string LoadedAssemblyHash(string name)
        {
            var matches=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name==name).ToArray();
            if(matches.Length!=1||string.IsNullOrEmpty(matches[0].Location))
                throw new PlatformNotSupportedException("Missing or ambiguous Windows runtime assembly: "+name);
            return Digest(matches[0].Location);
        }
        static void VerifyRuntime()
        {
            // Inspect the loaded detour module, not an unrelated DLL beside the game.
            // BepInEx has already initialized its detour provider before loading mods.
            using var process=Process.GetCurrentProcess();
            var modules=process.Modules.Cast<ProcessModule>().Where(m=>
                string.Equals(m.ModuleName,"dobby.dll",StringComparison.OrdinalIgnoreCase)).ToArray();
            if(modules.Length!=1)throw new PlatformNotSupportedException("Missing or ambiguous loaded Windows Dobby runtime.");
            NativePlatformContract.AdmitWindowsRuntime(
                Digest(Path.Combine(BepInEx.Paths.GameRootPath,"GameAssembly.dll")),LoadedAssemblyHash("PolyMod"),
                Digest(modules[0].FileName),LoadedAssemblyHash("Il2CppInterop.Runtime"),
                LoadedAssemblyHash("Il2CppInterop.HarmonySupport"),LoadedAssemblyHash("0Harmony"));
            Entry.Note("windows-runtime-compatibility","Verified game, unmodified loader and loaded hook dependencies; archive name and test opt-in are not required.");
        }
        internal static void Initialize()
        {
            if(!Enabled)return;
            VerifyRuntime();
            var profile=NativePlatformContract.Select("win",RuntimeInformation.ProcessArchitecture,IntPtr.Size);
            string path=Path.Combine(BepInEx.Paths.GameRootPath,profile.GameRelativePath);
            using var input=File.OpenRead(path);using var sha=SHA256.Create();
            if(Convert.ToHexString(sha.ComputeHash(input)).ToLowerInvariant()!=profile.GameSha256)
                throw new NotSupportedException("Windows hook folding requires the pinned game image.");
            using(var process=Process.GetCurrentProcess())
                foreach(ProcessModule module in process.Modules)
                    if(string.Equals(module.ModuleName,"GameAssembly.dll",StringComparison.OrdinalIgnoreCase))ImageBase=module.BaseAddress.ToInt64();
            if(ImageBase==0)throw new NotSupportedException("Windows game image is not loaded.");
            var empty=Pointer(Native.M("RevealCapitalAction","Execute","GameState"));
            if(empty!=Pointer(Native.M("Unit","set_Depth","System.Int32"))||
                Marshal.ReadByte(empty)!=0xc2||Marshal.ReadInt16(empty,1)!=0)
                throw new NotSupportedException("Windows empty-method folding changed.");
            if(Pointer(Native.M("InteractionBar","DeInit"))!=Pointer(Native.M("InteractionBar","OnDestroy"))||
                Pointer(Native.M("DisbandCommand","IsValid","GameState","System.String&"))!=
                Pointer(Native.M("ExplodeCommand","IsValid","GameState","System.String&"))||
                Pointer(Native.M("DisbandCommand","IsValid","GameState","System.String&"))!=
                Pointer(Native.M("EndTurnCommand","IsValid","GameState","System.String&")))
                throw new NotSupportedException("Windows shared-method contract changed.");
            Ready=true;
            VerifyReceiverDispatch();
            VerifyFocusReceivers();
            Entry.Note("windows-shared-hooks","Pinned empty methods excluded; UI cleanup and command validation share one hook owner each.");
        }
        static void VerifyReceiverDispatch()
        {
            // Allocate isolated command objects, never enqueue or execute them.
            // Exercise the actual IL2CPP receiver conversion used by Harmony.
            foreach(string name in new[]{"DisbandCommand","ExplodeCommand","EndTurnCommand"})
            {
                var type=Native.T(name);
                var pointer=IL2CPP.il2cpp_object_new(Il2CppClassPointerStore.GetNativeClassPointer(type));
                object canonical=Activator.CreateInstance(Native.T("DisbandCommand"),new object[]{pointer})!;
                object actual=ValidationReceiver(canonical);
                if(actual.GetType()!=type||Native.Pointer(actual)!=pointer.ToInt64())
                    throw new InvalidOperationException("Shared validator receiver identity changed.");
                string reason="unchanged";bool result=name!="DisbandCommand";
                NativeOrdinaryCommands.DisbandValid(canonical,null!,ref reason,ref result);
                if(reason!="unchanged"||result!=(name!="DisbandCommand"))
                    throw new InvalidOperationException("Disband validation leaked into another command.");
                if(name=="EndTurnCommand" &&
                    (!NativeBlackoutAccess.BeforeValid(canonical,null!,ref reason,ref result,
                        Native.M("DisbandCommand","IsValid","GameState","System.String&"))||reason!="unchanged"||!result))
                    throw new InvalidOperationException("EndTurn must retain native validation without a tile lookup.");
                GC.KeepAlive(canonical);GC.KeepAlive(actual);
            }
            Entry.Note("windows-shared-validator-check","Real IL2CPP Disband/Explode/EndTurn receivers retain identity; unrelated commands bypass Disband-only rules. No commands executed.");
        }
        static void VerifyFocusReceivers()
        {
            var owner=Pointer(Native.M("BuildReaction","ShouldFocusCamera"));
            foreach(string name in new[]{"AttackReaction","BoostOthersReaction","BoostReaction","BreakIceReaction",
                "BuildReaction","ClearTileEffectReaction","ConvertReaction","DecomposeReaction","DestroyImprovementReaction",
                "DestroyResourceReaction","DisbandUnitReaction","EatReaction","ExamineRuinsReaction","ExplodeUnitReaction","FreezeTileReaction"})
            {
                var type=Native.T(name);
                if(Pointer(Native.M(name,"ShouldFocusCamera"))!=owner)
                    throw new NotSupportedException("Windows focus-method folding changed: "+name);
                var pointer=IL2CPP.il2cpp_object_new(Il2CppClassPointerStore.GetNativeClassPointer(type));
                var wrapper=Activator.CreateInstance(Native.T("BuildReaction"),new object[]{pointer})!;
                if(IsReceiver(wrapper,"BuildReaction")!=(name=="BuildReaction"))
                    throw new InvalidOperationException("Focus receiver identity changed.");
                bool result=true;
                if(name!="BuildReaction"&&(!MimicRegroupAnimation.BeforeFocus(wrapper,ref result)||!result))
                    throw new InvalidOperationException("Build focus rule leaked into "+name);
                GC.KeepAlive(wrapper);
            }
            Entry.Note("windows-shared-focus-check","All 15 real reaction receiver classes checked; non-Build camera behavior remains native. No reactions executed.");
        }
        internal static bool Prepare(ref MethodInfo target,Type patch,string? pre,string? post,string? final)
        {
            if(!Enabled)return true;
            if(!Ready)throw new InvalidOperationException("Windows hook contract was not checked.");
            string owner=target.DeclaringType!.FullName!;
            // These pinned native bodies return immediately. They cannot run
            // nested discovery or change render depth; hooking their common RET
            // would intercept unrelated game methods with incompatible ABIs.
            if((owner=="RevealCapitalAction"&&target.Name=="Execute")||(owner=="Unit"&&target.Name=="set_Depth"))return false;
            bool folded=(owner=="InteractionBar"&&(target.Name=="DeInit"||target.Name=="OnDestroy"))||
                ((owner=="DisbandCommand"||owner=="ExplodeCommand")&&target.Name=="IsValid");
            if(owner=="InteractionBar"&&target.Name=="OnDestroy")target=Native.M("InteractionBar","DeInit");
            if(owner=="ExplodeCommand"&&target.Name=="IsValid")target=Native.M("DisbandCommand","IsValid","GameState","System.String&");
            // Managed beta hooks have no IL2CPP method pointer.
            if(target.DeclaringType!.Assembly.GetName().Name!="PolyMod")
            {
                var pointer=Pointer(target);
                NativeWindowsPrologueAudit.Require(pointer.ToInt64()-ImageBase,owner+"."+target.Name);
                if(Owners.TryGetValue(pointer,out var previous)&&previous!=target)
                    throw new NotSupportedException("Unreviewed shared Windows hook: "+previous+" / "+target);
                Owners[pointer]=target;
            }
            return !folded||Installed.Add((target,patch,pre,post,final));
        }
        internal static bool IsReceiver(object receiver,string type)=>
            IL2CPP.il2cpp_object_get_class((IntPtr)Native.Pointer(receiver))==Il2CppClassPointerStore.GetNativeClassPointer(Native.T(type));
        internal static object ValidationReceiver(object receiver)
        {
            var pointer=(IntPtr)Native.Pointer(receiver);
            string name=Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(IL2CPP.il2cpp_object_get_class(pointer)))!;
            if(name!="DisbandCommand"&&name!="ExplodeCommand"&&name!="EndTurnCommand")
                throw new NotSupportedException("Unexpected receiver for shared Windows validator: "+name);
            // Harmony's canonical wrapper is DisbandCommand even when the
            // native receiver is ExplodeCommand. Restore its real wrapper type.
            return Activator.CreateInstance(Native.T(name),new object[]{pointer})!;
        }
    }
}
#endif
