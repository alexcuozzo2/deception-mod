using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Own-mod typed native execution boundary. Enabled only by the explicit
    // detached execution diagnostic; normal launches retain existing hooks.
    // The router owns state authority; this boundary grants no gameplay action.
    internal static class NativeExecutionBridge
    {
        internal enum Decision:int { Pass=0,Handled=1,Reject=2 }
        internal enum Outcome:int { Complete=0,PrepareRejected=1,FinishRejected=2,OriginalException=3,CallbackException=4,InvalidPrepare=5 }
        [StructLayout(LayoutKind.Sequential)] internal struct Call
        {internal uint Size,Site;internal IntPtr Instance,State,MethodInfo;}
        [StructLayout(LayoutKind.Sequential)] internal struct Plan {internal ulong Cookie;}
        [StructLayout(LayoutKind.Sequential)] struct Config
        {internal uint Size,Version;internal IntPtr Original,Prepare,Finish,Cleanup,Factory,Raise,Image,Namespace,Name;}
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void Original(IntPtr instance,IntPtr state,IntPtr methodInfo);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int PrepareCallback(IntPtr call,IntPtr plan);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int FinishCallback(IntPtr call,IntPtr plan,int originalRan);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int CleanupCallback(IntPtr call,IntPtr plan,int outcome);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint Number();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ConfigureSite(uint site,ref Config config);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr GetImage();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr GetClass(IntPtr image,IntPtr space,IntPtr name);

        // Managed-only interfaces, never marshalled. Prepare must assign its
        // operation before acquiring resources/mutating anything requiring its
        // cleanup. A throw after assignment is contained and cleanup still runs.
        // The router owns all gameplay authority, transaction/rollback policy,
        // owner-thread checks and wrapper lifetime; this bridge grants none.
        internal interface IOperation
        {
            void Finish(bool originalRan);
            void Cleanup(Outcome outcome);
        }
        internal interface IRouter
        {
            Decision Prepare(Call call,out IOperation? operation);
            // Optional reporting/session faulting must itself tolerate repeated
            // notifications (prepare rejection then native cleanup outcome).
            void Failed(Call? call,string stage,string reason);
        }
        sealed class Operation
        {
            internal readonly Call Call;
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal IOperation? Handler;
            internal Decision Decision=Decision.Reject;
            internal bool FinishEntered;
            internal Operation(Call call){Call=call;}
        }
        sealed class SiteBinding
        {
            internal readonly uint Site;
            internal readonly string Owner,ExportName;
            internal object? Detour;
            internal Original? Export,TrampolineDelegate;
            internal IntPtr ExportPointer;
            internal SiteBinding(uint site,string owner,string name){Site=site;Owner=owner;ExportName=name;}
        }
        static readonly PrepareCallback PrepareRoot=Prepare;
        static readonly FinishCallback FinishRoot=Finish;
        static readonly CleanupCallback CleanupRoot=Cleanup;
        static readonly List<SiteBinding> Bindings=new();
        static readonly Dictionary<ulong,Operation> Operations=new();
        static readonly object OperationsGate=new();
        static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static volatile IRouter? Router;
        static IntPtr Library,GameLibrary;
        static bool SetupAttempted;
        internal static bool Installed {get;private set;}
        internal static bool RollbackFaulted {get;private set;}
        internal static long Prepared,Finished,Cleaned,Rejected;
        static ulong NextCookie;
        const int MaxOperations=64;
        static NativePlatformContract.Profile Profile=null!;
        internal static int ActiveOperations {get{lock(OperationsGate)return Operations.Count;}}
        internal static ulong AllocatedOperations {get{lock(OperationsGate)return NextCookie;}}
        static void Require(bool value,string reason){if(!value)throw new InvalidOperationException("Native execution bridge: "+reason);}
        static T Export<T>(IntPtr library,string name)where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,name));
        static void Layout()
        {
            Require(Marshal.SizeOf<Call>()==32&&Marshal.SizeOf<Plan>()==8&&Marshal.SizeOf<Config>()==80,"POD sizes differ from native ABI1.");
            foreach(var pair in new[]{(nameof(Call.Size),0),(nameof(Call.Site),4),(nameof(Call.Instance),8),(nameof(Call.State),16),(nameof(Call.MethodInfo),24)})
                Require(Marshal.OffsetOf<Call>(pair.Item1).ToInt32()==pair.Item2,"Call field offset differs.");
            Require(Marshal.OffsetOf<Plan>(nameof(Plan.Cookie)).ToInt32()==0,"Plan cookie offset differs.");
            foreach(var pair in new[]{(nameof(Config.Size),0),(nameof(Config.Version),4),(nameof(Config.Original),8),(nameof(Config.Prepare),16),
                (nameof(Config.Finish),24),(nameof(Config.Cleanup),32),(nameof(Config.Factory),40),(nameof(Config.Raise),48),
                (nameof(Config.Image),56),(nameof(Config.Namespace),64),(nameof(Config.Name),72)})
                Require(Marshal.OffsetOf<Config>(pair.Item1).ToInt32()==pair.Item2,"Config field offset differs.");
        }
        static MethodInfo Method(SiteBinding site)
        {
            var method=Native.M(site.Owner,"Execute","GameState");
            Require(!method.IsStatic&&method.ReturnType==typeof(void)&&method.DeclaringType==Native.T(site.Owner),"Execute wrapper signature/owner differs: "+site.Owner);return method;
        }
        static void ValidateOriginal(string name,IntPtr pointer)
        {
            NativePlatformLibrary.RequireSameImage(Profile,pointer,NativeLibrary.GetExport(GameLibrary,"il2cpp_get_corlib"));
            using var stream=typeof(NativeExecutionBridge).Assembly.GetManifestResourceStream(Profile.GetArtifact(NativePlatformContract.BridgeKind.Execution).SeamsResource)??throw new InvalidDataException("Missing execution seam contract.");
            using var document=System.Text.Json.JsonDocument.Parse(stream);byte[] expected=Convert.FromHexString(document.RootElement.GetProperty(name).GetString()!);
            Require(expected.Length==32,"Expected exact32-byte Execute fingerprint.");var actual=new byte[32];Marshal.Copy(pointer,actual,0,32);
            Require(actual.SequenceEqual(expected),"Execute prologue differs or is already detoured: "+name);
        }
        static void CreateDetour(SiteBinding site)
        {
            var method=Method(site);var patches=Harmony.GetPatchInfo(method);
            Require(patches is null||!patches.Owners.Any(),"Existing Execute Harmony patch must be migrated first: "+site.Owner);
            var resolver=Native.T("Il2CppInterop.Runtime.Injection.InjectorHelpers").GetMethod("GetIl2CppMethodPointer",Flags,null,new[]{typeof(MethodBase)},null)
                ??throw new MissingMethodException("Exact framework native method-pointer resolver unavailable.");
            Require(resolver.ReturnType==typeof(IntPtr),"Native resolver return differs.");IntPtr pointer=(IntPtr)Native.Invoke(resolver,null,method)!;
            ValidateOriginal(site.Owner+".Execute",pointer);
            var runtime=Native.Static("Il2CppInterop.Runtime.Startup.Il2CppInteropRuntime","Instance")!;var provider=Native.Get(runtime,"DetourProvider")!;
            var contract=Native.T("Il2CppInterop.Runtime.Injection.IDetourProvider");
            var create=contract.GetMethods().Single(m=>m.Name=="Create"&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==1&&m.GetParameters().Length==2);
            // Store the handle BEFORE preparation can throw. Startup rollback
            // retains it if provider disposal fails; never lose an installed or
            // prepared native handle behind a failed return assignment.
            site.Detour=Native.Invoke(create.MakeGenericMethod(typeof(Original)),provider,pointer,site.Export)!;
            var detour=Native.T("Il2CppInterop.Runtime.Injection.IDetour");
            var generate=detour.GetMethods().Single(m=>m.Name=="GenerateTrampoline"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==0);
            site.TrampolineDelegate=(Original)Native.Invoke(generate.MakeGenericMethod(typeof(Original)),site.Detour)!;
            Require((IntPtr)detour.GetProperty("Target")!.GetValue(site.Detour)! ==pointer&&
                (IntPtr)detour.GetProperty("Detour")!.GetValue(site.Detour)! ==site.ExportPointer,"Provider changed target/export identity.");
            Require(Trampoline(site)!=IntPtr.Zero,"Provider did not prepare the raw native original trampoline.");
        }
        static IntPtr Trampoline(SiteBinding site)=>(IntPtr)Native.T("Il2CppInterop.Runtime.Injection.IDetour").GetProperty("OriginalTrampoline")!.GetValue(site.Detour)!;

        // Explicit startup integration; the caller must first select the matching
        // hook route. There is no automatic gameplay authority.
        internal static void Install(IRouter router)
        {
            Require(!SetupAttempted&&!Installed&&Library==IntPtr.Zero,"Setup cannot be repeated in this process.");SetupAttempted=true;
            Require(router is not null,"An explicit reviewed router is required.");
            Profile=NativePlatformContract.Current();
            Layout();Router=router;
            IntPtr space=IntPtr.Zero,name=IntPtr.Zero;
            try
            {
                string game=Path.GetFullPath((string)Native.Static("BepInEx.Paths","GameRootPath")!);
                string binary=NativePlatformLibrary.ValidateGame(Profile,game);
                Library=NativePlatformLibrary.LoadOwn(Profile,NativePlatformContract.BridgeKind.Execution,game,typeof(NativeExecutionBridge).Assembly);GameLibrary=NativeLibrary.Load(binary);
                Require(Export<Number>(Library,"dcp_exec_bridge_abi_version")()==1&&Export<Number>(Library,"dcp_exec_bridge_call_size")()==32&&
                    Export<Number>(Library,"dcp_exec_bridge_plan_size")()==8&&Export<Number>(Library,"dcp_exec_bridge_config_size")()==80&&
                    Export<Number>(Library,"dcp_exec_bridge_site_count")()==4,"Native execution ABI differs.");
                space=Marshal.StringToCoTaskMemUTF8("System");name=Marshal.StringToCoTaskMemUTF8("InvalidOperationException");
                IntPtr image=Export<GetImage>(GameLibrary,"il2cpp_get_corlib")();
                Require(image!=IntPtr.Zero&&Export<GetClass>(GameLibrary,"il2cpp_class_from_name")(image,space,name)!=IntPtr.Zero,"Native rejection exception class is absent.");
                Bindings.AddRange(new[]{new SiteBinding(0,"BuildCommand","build_command"),new SiteBinding(1,"BuildAction","build_action"),
                    new SiteBinding(2,"TrainCommand","train_command"),new SiteBinding(3,"TrainAction","train_action")});
                foreach(var site in Bindings)
                {
                    site.ExportPointer=NativeLibrary.GetExport(Library,"dcp_exec_bridge_"+site.ExportName);
                    site.Export=Marshal.GetDelegateForFunctionPointer<Original>(site.ExportPointer);
                    Require(Marshal.GetFunctionPointerForDelegate(site.Export)==site.ExportPointer,"Native delegate roundtrip introduced a CLR boundary.");
                    CreateDetour(site);
                    var config=new Config{Size=80,Version=1,Original=Trampoline(site),Prepare=Marshal.GetFunctionPointerForDelegate(PrepareRoot),
                        Finish=Marshal.GetFunctionPointerForDelegate(FinishRoot),Cleanup=Marshal.GetFunctionPointerForDelegate(CleanupRoot),
                        Factory=NativeLibrary.GetExport(GameLibrary,"il2cpp_exception_from_name_msg"),Raise=NativeLibrary.GetExport(GameLibrary,"il2cpp_raise_exception"),
                        Image=image,Namespace=space,Name=name};
                    Require(Export<ConfigureSite>(Library,"dcp_exec_bridge_configure_site")(site.Site,ref config)==0,"Native immutable site configuration rejected "+site.Owner);
                }
                // All raw trampolines/configurations exist before the first Apply.
                // Run at the same startup owner-thread boundary as state bridge.
                foreach(var site in Bindings)
                {
                    var patches=Harmony.GetPatchInfo(Method(site));Require(patches is null||!patches.Owners.Any(),"Execute was patched during startup preparation.");
                    var target=(IntPtr)Native.T("Il2CppInterop.Runtime.Injection.IDetour").GetProperty("Target")!.GetValue(site.Detour)!;
                    ValidateOriginal(site.Owner+".Execute",target);
                }
                foreach(var site in Bindings)Native.Invoke(Native.T("Il2CppInterop.Runtime.Injection.IDetour").GetMethod("Apply")!,site.Detour);
                Installed=true;
            }
            catch{RollbackSetup();throw;}
            finally{if(space!=IntPtr.Zero)Marshal.FreeCoTaskMem(space);if(name!=IntPtr.Zero)Marshal.FreeCoTaskMem(name);}
        }
        internal static void RollbackSetup()
        {
            // This is intentionally not a runtime shutdown/unload method.
            Require(Interlocked.Read(ref Prepared)==0&&ActiveOperations==0,"Cannot retire Execute detours after a call was admitted.");
            Exception? failure=null;
            foreach(var site in Bindings.AsEnumerable().Reverse())
            {
                try{if(site.Detour is IDisposable disposable)disposable.Dispose();site.Detour=null;}
                catch(Exception e){failure??=e;}
            }
            RollbackFaulted=failure is not null;
            if(failure is not null)throw new InvalidOperationException("Execution startup rollback failed; native handles and roots retained.",failure);
            Installed=false;
            // Preserve native library handles, immutable configuration, router,
            // exports and original delegates. A new installation needs a restart.
        }
        static void Validate(Call call)
        {
            Require(call.Size==32&&call.Site<4&&call.Instance!=IntPtr.Zero&&call.State!=IntPtr.Zero,"Invalid native execution call.");
            Require(Router is not null,"Execution router is unavailable.");
            // A native direct caller may pass null MethodInfo. Preserve it exactly;
            // do not invent a generated wrapper MethodInfo for the raw original.
        }
        static bool Same(Call a,Call b)=>a.Size==b.Size&&a.Site==b.Site&&a.Instance==b.Instance&&a.State==b.State&&a.MethodInfo==b.MethodInfo;
        static Operation Find(Call call,Plan plan)
        {
            Require(plan.Cookie!=0&&Operations.TryGetValue(plan.Cookie,out var slot)&&Same(slot.Call,call),"Execution cookie/call identity differs.");
            var operation=Operations[plan.Cookie];Require(operation.Thread==Environment.CurrentManagedThreadId,"Execution callback crossed its owner thread.");return operation;
        }
        static int Prepare(IntPtr callPointer,IntPtr planPointer)
        {
            var call=new Call();var plan=new Plan();int result=(int)Decision.Reject;
            try
            {
                Interlocked.Increment(ref Prepared);Require(callPointer!=IntPtr.Zero&&planPointer!=IntPtr.Zero,"Null native call/plan pointer.");
                call=Marshal.PtrToStructure<Call>(callPointer);Validate(call);
                var slot=new Operation(call);
                lock(OperationsGate)
                {
                    Require(Operations.Count<MaxOperations&&NextCookie<ulong.MaxValue,"Execution operation bound exhausted.");
                    plan.Cookie=++NextCookie;Operations.Add(plan.Cookie,slot);
                }
                slot.Decision=Router!.Prepare(call,out slot.Handler);
                Require(slot.Decision is Decision.Pass or Decision.Handled or Decision.Reject,"Router returned an unknown execution decision.");result=(int)slot.Decision;
            }
            catch(Exception e){Reject(call,"prepare",e);}
            try{Marshal.StructureToPtr(plan,planPointer,false);return result;}
            catch(Exception e)
            {
                Reject(call,"prepare-plan",e);
                // If native cookie publication failed, retire the locally known
                // handler now. A later native cleanup cannot execute it twice.
                try{CleanupOperation(call,plan,Outcome.PrepareRejected);}catch(Exception cleanup){Reject(call,"prepare-plan-cleanup",cleanup);}
                return (int)Decision.Reject;
            }
        }
        static int Finish(IntPtr callPointer,IntPtr planPointer,int originalRan)
        {
            Call? parsed=null;
            try
            {
                Interlocked.Increment(ref Finished);Require(callPointer!=IntPtr.Zero&&planPointer!=IntPtr.Zero,"Null native finish input.");
                Call call=Marshal.PtrToStructure<Call>(callPointer);parsed=call;Validate(call);var plan=Marshal.PtrToStructure<Plan>(planPointer);Operation slot;
                lock(OperationsGate)
                {
                    slot=Find(call,plan);Require(!slot.FinishEntered&&originalRan==(slot.Decision==Decision.Pass?1:0)&&(slot.Decision is Decision.Pass or Decision.Handled),"Execute finish ordering/decision differs.");
                    slot.FinishEntered=true;
                }
                slot.Handler?.Finish(originalRan==1);return 0;
            }
            catch(Exception e){Reject(parsed,"finish",e);return 1;}
        }
        static void CleanupOperation(Call call,Plan plan,Outcome outcome)
        {
            if(plan.Cookie==0)return;Operation slot;
            lock(OperationsGate){slot=Find(call,plan);Operations.Remove(plan.Cookie);}
            // Retire the cookie BEFORE user cleanup. Reentrant calls receive new
            // cookies; a cleanup throw cannot trigger a second handler cleanup.
            slot.Handler?.Cleanup(outcome);
        }
        static int Cleanup(IntPtr callPointer,IntPtr planPointer,int outcome)
        {
            Call? parsed=null;
            try
            {
                Interlocked.Increment(ref Cleaned);Require(callPointer!=IntPtr.Zero&&planPointer!=IntPtr.Zero,"Null native cleanup input.");
                Call call=Marshal.PtrToStructure<Call>(callPointer);parsed=call;Validate(call);var plan=Marshal.PtrToStructure<Plan>(planPointer);
                Require(outcome>=0&&outcome<=5,"Unknown native cleanup outcome.");
                if(outcome!=0)Notify(call,"native-cleanup-outcome",outcome.ToString());
                CleanupOperation(call,plan,(Outcome)outcome);return 0;
            }
            catch(Exception e){Reject(parsed,"cleanup",e);return 1;}
        }
        static void Reject(Call? call,string stage,Exception error)
        {try{Interlocked.Increment(ref Rejected);Notify(call,stage,error.GetType().Name+": "+error.Message);}catch{}}
        static void Notify(Call? call,string stage,string reason)
        {try{Router?.Failed(call,stage,reason);}catch{/* No CLR exception may cross a native callback. */}}
    }
}
