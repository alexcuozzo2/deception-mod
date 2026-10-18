using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Own-mod native boundary: callbacks return POD status; only the native
    // bridge calls the original native trampoline and raises native exceptions.
    internal static class NativeStateBridge
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Call
        {internal uint Size;internal int Operation,Version;internal uint Site;internal IntPtr State,Stream,MethodInfo;}
        [StructLayout(LayoutKind.Sequential)] internal struct Plan
        {internal ulong Cookie;internal IntPtr Replacement;}
        [StructLayout(LayoutKind.Sequential)] struct Config
        {
            internal uint Size,Version;
            internal IntPtr Write,Read,Prepare,Finish,Cleanup,Factory,Raise,Image,Namespace,Name;
        }
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Original(IntPtr state,IntPtr stream,int version,IntPtr method);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int PrepareCallback(IntPtr call,IntPtr plan);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int FinishCallback(IntPtr call,IntPtr plan);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int CleanupCallback(IntPtr call,IntPtr plan,int outcome);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint Number();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ConfigureSite(uint site,ref Config config);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr GetImage();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr GetClass(IntPtr image,IntPtr space,IntPtr name);
        static readonly PrepareCallback PrepareRoot=Prepare;
        static readonly FinishCallback FinishRoot=Finish;
        static readonly CleanupCallback CleanupRoot=Cleanup;
        sealed class SiteBinding
        {
            internal uint Site;internal string Owner="",Prefix="";
            internal object? WriteDetour,ReadDetour;
            internal Original? WriteExport,ReadExport;
        }
        static readonly System.Collections.Generic.List<SiteBinding> Bindings=new();
        static readonly System.Collections.Generic.List<Delegate> OriginalRoots=new();
        static IntPtr Library,GameLibrary;
        internal static bool Installed { get; private set; }
        internal static bool RollbackFaulted { get; private set; }
        internal static bool BuildLeavesInstalled { get; private set; }
        static bool TrainMode,BuildAttempted,PrivateBuildMode,ApplyAttempted;
        internal static long Prepared,Finished,Cleaned,Rejected;
        static NativePlatformContract.Profile Profile=null!;
        static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static void Require(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);}
        static T Export<T>(IntPtr library,string name) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,name));
        static object CreateDetour(MethodInfo method,Original export)
        {
            var patches=Harmony.GetPatchInfo(method);
            Require(patches is null||!patches.Owners.Any(),"Existing serializer patch conflicts with the native state bridge: "+method.Name);
            var resolver=Native.T("Il2CppInterop.Runtime.Injection.InjectorHelpers").GetMethod("GetIl2CppMethodPointer",Flags,null,new[]{typeof(MethodBase)},null)
                ??throw new MissingMethodException("Exact native method-pointer resolver unavailable.");
            Require(resolver.ReturnType==typeof(IntPtr),"Native method-pointer resolver return mismatch.");
            IntPtr pointer=(IntPtr)Native.Invoke(resolver,null,method)!;Require(pointer!=IntPtr.Zero,"Missing native serializer pointer.");
            ValidateOriginal(method.DeclaringType!.Name+"."+method.Name,pointer);
            object runtime=Native.Static("Il2CppInterop.Runtime.Startup.Il2CppInteropRuntime","Instance")!;
            object provider=Native.Get(runtime,"DetourProvider")!;
            Type contract=Native.T("Il2CppInterop.Runtime.Injection.IDetourProvider");
            var create=contract.GetMethods().Single(x=>x.Name=="Create"&&x.IsGenericMethodDefinition&&x.GetGenericArguments().Length==1&&x.GetParameters().Length==2);
            object detour=Native.Invoke(create.MakeGenericMethod(typeof(Original)),provider,pointer,export)!;
            try
            {
                Type detourType=Native.T("Il2CppInterop.Runtime.Injection.IDetour");
                var generate=detourType.GetMethods().Single(x=>x.Name=="GenerateTrampoline"&&x.IsGenericMethodDefinition&&x.GetParameters().Length==0);
                OriginalRoots.Add((Delegate)Native.Invoke(generate.MakeGenericMethod(typeof(Original)),detour)!);
                Require((IntPtr)detourType.GetProperty("Target")!.GetValue(detour)! ==pointer&&
                    (IntPtr)detourType.GetProperty("Detour")!.GetValue(detour)! ==Marshal.GetFunctionPointerForDelegate(export),
                    "Native provider changed the verified target or native export pointer.");
                Require(Trampoline(detour)!=IntPtr.Zero,"Native provider did not prepare its original trampoline.");return detour;
            }
            catch{((IDisposable)detour).Dispose();throw;}
        }
        static IntPtr Trampoline(object detour)=>(IntPtr)Native.T("Il2CppInterop.Runtime.Injection.IDetour").GetProperty("OriginalTrampoline")!.GetValue(detour)!;
        static void Apply(object detour)=>Native.Invoke(Native.T("Il2CppInterop.Runtime.Injection.IDetour").GetMethod("Apply")!,detour);
        static void ValidateOriginal(string method,IntPtr pointer)
        {
            NativePlatformLibrary.RequireSameImage(Profile,pointer,NativeLibrary.GetExport(GameLibrary,"il2cpp_get_corlib"));
            // Exact audited prologues are supplied by the build's pinned native
            // seam contract; addresses are resolved by generated metadata only.
            using var stream=typeof(NativeStateBridge).Assembly.GetManifestResourceStream(Profile.GetArtifact(NativePlatformContract.BridgeKind.State).SeamsResource)
                ??throw new InvalidDataException("Missing pinned native seam contract.");
            using var document=System.Text.Json.JsonDocument.Parse(stream);
            string hex=document.RootElement.GetProperty(method).GetString()!;
            byte[] expected=Convert.FromHexString(hex),actual=new byte[expected.Length];
            Require(expected.Length>=16&&expected.Length<=32,"Invalid native seam signature size.");Marshal.Copy(pointer,actual,0,actual.Length);
            Require(actual.SequenceEqual(expected),"Native serializer prologue differs or was already detoured: "+method);
        }
        internal static void Install(bool commands=false)=>InstallMode(commands,false);
        internal static void InstallTrainLeaves()
        {
            Require(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_TRAIN_LEAF_PROBE")=="1"&&
                Environment.GetEnvironmentVariable("DECEPTION_NATIVE_BEHAVIOR_PROBE")=="1"&&!NativeFreshSession.Enabled,
                "Private Train leaves require a fresh standalone behavior diagnostic.");
            foreach(string suffix in new[]{"SESSION","STATE","ENVELOPE","LEDGER","COMMAND","EXCHANGE","RECRUITMENT","PAID_BIRTH","EXECUTION"})
                Require(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_"+suffix+"_PROBE")!="1","Train leaf mode cannot share another diagnostic.");
            _=NativeBehaviorSession.RequireCreatedClient();
            InstallMode(false,true);
        }
        static void InstallMode(bool commands,bool trainOnly)
        {
            Require(!Installed&&Library==IntPtr.Zero,"Native bridge setup cannot be repeated.");
            Profile=NativePlatformContract.Current();
            Require(Marshal.SizeOf<Call>()==40&&Marshal.SizeOf<Plan>()==16&&Marshal.SizeOf<Config>()==88,"Managed native-bridge ABI layout mismatch.");
            Require(Marshal.OffsetOf<Call>(nameof(Call.State)).ToInt32()==16&&Marshal.OffsetOf<Call>(nameof(Call.MethodInfo)).ToInt32()==32&&Marshal.OffsetOf<Config>(nameof(Config.Name)).ToInt32()==80,"Managed native-bridge ABI offsets differ.");
            string game=Path.GetFullPath((string)Native.Static("BepInEx.Paths","GameRootPath")!);
            string binary=NativePlatformLibrary.ValidateGame(Profile,game);
            Library=NativePlatformLibrary.LoadOwn(Profile,NativePlatformContract.BridgeKind.State,game,typeof(NativeStateBridge).Assembly);GameLibrary=NativeLibrary.Load(binary);
            Require(Export<Number>(Library,"dcp_state_bridge_abi_version")()==3&&Export<Number>(Library,"dcp_state_bridge_site_count")()==5&&Export<Number>(Library,"dcp_state_bridge_call_size")()==40&&
                Export<Number>(Library,"dcp_state_bridge_plan_size")()==16&&Export<Number>(Library,"dcp_state_bridge_config_size")()==88,"Native bridge ABI version or size differs.");
            IntPtr space=Marshal.StringToCoTaskMemUTF8("System"),name=Marshal.StringToCoTaskMemUTF8("InvalidOperationException");
            try
            {
                IntPtr image=Export<GetImage>(GameLibrary,"il2cpp_get_corlib")();
                Require(image!=IntPtr.Zero&&Export<GetClass>(GameLibrary,"il2cpp_class_from_name")(image,space,name)!=IntPtr.Zero,"Native rejection exception class unavailable in corlib.");
                if(trainOnly)
                {
                    Bindings.Add(new SiteBinding{Site=3,Owner="TrainCommand",Prefix="train_command_"});
                    Bindings.Add(new SiteBinding{Site=4,Owner="TrainAction",Prefix="train_action_"});
                }
                else
                {
                    Bindings.Add(new SiteBinding{Site=0,Owner="GameState",Prefix=""});
                    if(commands){Bindings.Add(new SiteBinding{Site=1,Owner="BuildCommand",Prefix="command_"});Bindings.Add(new SiteBinding{Site=2,Owner="BuildAction",Prefix="action_"});}
                }
                PrepareSites(Bindings.ToArray(),image,space,name);
                ValidateAndApply(Bindings.ToArray());
                TrainMode=trainOnly;
                Installed=true;
                Entry.Note("native-state-bridge-installed","Exact native export/delegate pointers matched; raw original trampolines prepared; ABI v3 x86_64 verified; pairs="+Bindings.Count+"; train-only="+trainOnly+". Opt-in only.");
            }
            catch{RollbackSetup();throw;}
            finally{Marshal.FreeCoTaskMem(space);Marshal.FreeCoTaskMem(name);}
        }
        static void PrepareSites(SiteBinding[] sites,IntPtr image,IntPtr space,IntPtr name)
        {
            foreach(var site in sites)
            {
                IntPtr writePointer=NativeLibrary.GetExport(Library,"dcp_state_bridge_"+site.Prefix+"serialize"),
                    readPointer=NativeLibrary.GetExport(Library,"dcp_state_bridge_"+site.Prefix+"deserialize");
                site.WriteExport=Marshal.GetDelegateForFunctionPointer<Original>(writePointer);site.ReadExport=Marshal.GetDelegateForFunctionPointer<Original>(readPointer);
                Require(Marshal.GetFunctionPointerForDelegate(site.WriteExport)==writePointer&&Marshal.GetFunctionPointerForDelegate(site.ReadExport)==readPointer,
                    "Native-export delegate roundtrip inserted a managed callback boundary: "+site.Owner);
                site.WriteDetour=CreateDetour(Native.M(site.Owner,"Serialize","Il2CppSystem.IO.BinaryWriter","System.Int32"),site.WriteExport);
                site.ReadDetour=CreateDetour(Native.M(site.Owner,"Deserialize","Il2CppSystem.IO.BinaryReader","System.Int32"),site.ReadExport);
                var config=new Config{Size=88,Version=3,Write=Trampoline(site.WriteDetour),Read=Trampoline(site.ReadDetour),
                    Prepare=Marshal.GetFunctionPointerForDelegate(PrepareRoot),Finish=Marshal.GetFunctionPointerForDelegate(FinishRoot),Cleanup=Marshal.GetFunctionPointerForDelegate(CleanupRoot),
                    Factory=NativeLibrary.GetExport(GameLibrary,"il2cpp_exception_from_name_msg"),Raise=NativeLibrary.GetExport(GameLibrary,"il2cpp_raise_exception"),Image=image,Namespace=space,Name=name};
                Require(Export<ConfigureSite>(Library,"dcp_state_bridge_configure_site")(site.Site,ref config)==0,"Native bridge rejected immutable configuration: "+site.Owner);
            }
        }
        static void ValidateAndApply(SiteBinding[] sites)
        {
            // Prepare and validate every pair before applying any of them.
            foreach(var site in sites)
            {
                ValidateOriginal(site.Owner+".Serialize",(IntPtr)Native.T("Il2CppInterop.Runtime.Injection.IDetour").GetProperty("Target")!.GetValue(site.WriteDetour)!);
                ValidateOriginal(site.Owner+".Deserialize",(IntPtr)Native.T("Il2CppInterop.Runtime.Injection.IDetour").GetProperty("Target")!.GetValue(site.ReadDetour)!);
            }
            foreach(var site in sites){ApplyAttempted=true;Apply(site.WriteDetour!);Apply(site.ReadDetour!);}
        }
        internal static void InstallBuildLeaves()
        {
            NativeTrainLeafProbeTrigger.ValidateMode();
            Require(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_BUILD_LEAF_PROBE")=="1"&&TrainMode&&Installed&&
                !BuildAttempted&&!BuildLeavesInstalled&&!RollbackFaulted&&!NativeFreshSession.Enabled,
                "Private Build requires the existing fresh Train-only boundary and a first explicit request.");
            _=NativeBehaviorSession.RequireCreatedClient();BuildAttempted=true;
            Require(Bindings.Count==2&&Bindings.All(x=>x.Site is 3 or 4)&&Library!=IntPtr.Zero&&GameLibrary!=IntPtr.Zero,
                "Build cannot replace a configured state/command boundary.");
            var sites=new[]{new SiteBinding{Site=1,Owner="BuildCommand",Prefix="command_"},new SiteBinding{Site=2,Owner="BuildAction",Prefix="action_"}};
            Bindings.AddRange(sites);PrivateBuildMode=true;
            IntPtr space=Marshal.StringToCoTaskMemUTF8("System"),name=Marshal.StringToCoTaskMemUTF8("InvalidOperationException");
            try
            {
                IntPtr image=Export<GetImage>(GameLibrary,"il2cpp_get_corlib")();
                Require(image!=IntPtr.Zero&&Export<GetClass>(GameLibrary,"il2cpp_class_from_name")(image,space,name)!=IntPtr.Zero,
                    "Native Build rejection exception unavailable.");
                PrepareSites(sites,image,space,name);ValidateAndApply(sites);BuildLeavesInstalled=true;
                Entry.Note("native-build-leaf-bridge-installed","Constructor-owned private Build sites1/2 joined existing Train3/4; no GameState hook or live admission.");
            }
            catch
            {
                // Train sites are already reachable. Retain all configured
                // callbacks/libraries/trampolines to process exit on any failure.
                RollbackFaulted=true;throw;
            }
            finally{Marshal.FreeCoTaskMem(space);Marshal.FreeCoTaskMem(name);}
        }
        internal static void RollbackSetup()
        {
            if(ApplyAttempted)
            {
                RollbackFaulted=true;
                throw new InvalidOperationException("A native detour Apply was attempted; retain every handle and callback to process exit.");
            }
            Exception? failure=null;
            foreach(var site in Bindings.AsEnumerable().Reverse())
            {
                try{if(site.ReadDetour is IDisposable read)read.Dispose();site.ReadDetour=null;}catch(Exception e){failure??=e;}
                try{if(site.WriteDetour is IDisposable write)write.Dispose();site.WriteDetour=null;}catch(Exception e){failure??=e;}
            }
            RollbackFaulted=failure is not null;
            if(failure is not null)throw new InvalidOperationException("Own native detour startup rollback failed; handles retained.",failure);
            Installed=false;
            // Startup rollback only, before gameplay/diagnostics can run. Keep
            // the library/callback roots loaded; this is not a runtime unload API.
        }
        static int Prepare(IntPtr call,IntPtr plan)
        {
            var value=new Plan();var parsed=new Call();int result;
            try{Prepared++;parsed=Marshal.PtrToStructure<Call>(call);result=PrepareOperation(parsed,ref value);}
            catch(Exception e){Rejection("prepare",e);result=2;}
            try{Marshal.StructureToPtr(value,plan,false);return result;}
            catch(Exception e)
            {
                Rejection("prepare-plan",e);
                // The native plan may still contain cookie zero. Release the
                // local operation before returning rejection in that case.
                try{CleanupOperation(parsed,value,1);}catch(Exception cleanup){Rejection("prepare-plan-cleanup",cleanup);}
                return 2;
            }
        }
        static int Finish(IntPtr call,IntPtr plan)
        {try{Finished++;FinishOperation(Marshal.PtrToStructure<Call>(call),Marshal.PtrToStructure<Plan>(plan));return 0;}catch(Exception e){Rejection("finish",e);return 1;}}
        static int Cleanup(IntPtr call,IntPtr plan,int outcome)
        {
            // An original native exception can bypass every managed Prepare/
            // Finish catch. The live creation attempt must still retain fault
            // even when a native generic caller translates that exception to false.
            if(outcome!=0)NotifySessionFailure("native-cleanup-outcome",outcome.ToString());
            try{Cleaned++;CleanupOperation(Marshal.PtrToStructure<Call>(call),Marshal.PtrToStructure<Plan>(plan),outcome);return 0;}
            catch(Exception e){Rejection("cleanup",e);return 1;}
        }
        static object Wrap(string type,IntPtr pointer)=>Native.T(type).GetConstructor(new[]{typeof(IntPtr)})!.Invoke(new object[]{pointer});
        static int PrepareOperation(Call call,ref Plan plan)
        {
            if(call.Site==0)return NativeGameStateEnvelope.Prepare(call,ref plan);
            if(call.Site is 1 or 2)
            {
                if(!PrivateBuildMode)return NativeCommandEnvelope.Prepare(call,ref plan);
                Require(call.Size==40&&call.Operation is 0 or 1&&call.State!=IntPtr.Zero&&call.Stream!=IntPtr.Zero,"Invalid Build bridge call.");
                return NativeBuildLeafDispatcher.Prepare(call,Wrap(call.Site==1?"BuildCommand":"BuildAction",call.State),
                    Wrap(call.Operation==1?"Il2CppSystem.IO.BinaryReader":"Il2CppSystem.IO.BinaryWriter",call.Stream),ref plan);
            }
            Require(call.Site is 3 or 4&&call.Size==40&&call.Operation is 0 or 1&&call.State!=IntPtr.Zero&&call.Stream!=IntPtr.Zero,"Invalid Train bridge call.");
            return NativeTrainLeafDispatcher.Prepare(call,Wrap(call.Site==3?"TrainCommand":"TrainAction",call.State),
                Wrap(call.Operation==1?"Il2CppSystem.IO.BinaryReader":"Il2CppSystem.IO.BinaryWriter",call.Stream),ref plan);
        }
        static void FinishOperation(Call call,Plan plan)
        {
            if(call.Site==0)NativeGameStateEnvelope.Finish(call,plan);
            else if(call.Site is 1 or 2){if(PrivateBuildMode)NativeBuildLeafDispatcher.Finish(call,plan);else NativeCommandEnvelope.Finish(call,plan);}
            else if(call.Site is 3 or 4)NativeTrainLeafDispatcher.Finish(call,plan);
            else throw new InvalidOperationException("Unknown native bridge site.");
        }
        static void CleanupOperation(Call call,Plan plan,int outcome)
        {
            if(call.Site==0)NativeGameStateEnvelope.Cleanup(call,plan,outcome);
            else if(call.Site is 1 or 2){if(PrivateBuildMode)NativeBuildLeafDispatcher.Cleanup(call,plan,outcome);else NativeCommandEnvelope.Cleanup(call,plan,outcome);}
            else if(call.Site is 3 or 4)NativeTrainLeafDispatcher.Cleanup(call,plan,outcome);
            else throw new InvalidOperationException("Unknown native bridge site.");
        }
        static void Rejection(string stage,Exception error)
        {
            Rejected++;NotifySessionFailure(stage,error.GetType().Name+": "+error.Message);
            try{Entry.Note("native-envelope-rejection",stage+": "+error.GetType().Name+": "+error.Message);}catch{}
        }
        static void NotifySessionFailure(string stage,string reason)
        {
            // No managed exception may cross a native callback ABI, including
            // failures in the optional session observer itself.
            try{NativeFreshSession.OnTransportFailure(stage,reason);}catch{}
            try{if(NativeRegroupSession.Enabled)NativeRegroupSession.TransportFault("Native transport "+stage+": "+reason);}catch{}
        }
    }
}
