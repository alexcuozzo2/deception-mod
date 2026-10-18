using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using B=Deception.PolyModAdapter.NativeCommandOuterAbi;
namespace Deception.PolyModAdapter
{
    // Diagnostic-only typed native boundary. Applied detours are retained to
    // process exit, including partial Apply failure; an idle frame is not a
    // process-wide quiescence guarantee for freeing a native trampoline.
    internal static class NativeCommandOuterBridge
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint Number();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Configure(ref B.Config config);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr GetImage();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr GetClass(IntPtr image,IntPtr space,IntPtr name);
        static IntPtr Library,GameLibrary;
        static object? WriteDetour,ReadDetour;
        static B.WriteOriginal? WriteExport,WriteTrampoline;
        static B.ReadOriginal? ReadExport,ReadTrampoline;
        static bool Attempted,ApplyAttempted;
        internal static bool Installed {get;private set;}
        internal static bool Faulted {get;private set;}
        static NativePlatformContract.Profile Profile=null!;
        static readonly BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static void Require(bool value,string why){if(!value)throw new InvalidOperationException("Typed command bridge: "+why);}
        static T Export<T>(IntPtr library,string name) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library,name));
        static void ValidateOriginal(MethodInfo method,IntPtr pointer)
        {
            NativePlatformLibrary.RequireSameImage(Profile,pointer,NativeLibrary.GetExport(GameLibrary,"il2cpp_get_corlib"));
            using var stream=typeof(NativeCommandOuterBridge).Assembly.GetManifestResourceStream(Profile.GetArtifact(NativePlatformContract.BridgeKind.Command).SeamsResource)??throw new InvalidDataException("Missing command seam contract.");
            using var doc=System.Text.Json.JsonDocument.Parse(stream);
            byte[] expected=Convert.FromHexString(doc.RootElement.GetProperty(method.DeclaringType!.Name+"."+method.Name).GetString()!);
            Require(expected.Length is >=16 and <=32,"Invalid command prologue size.");byte[] actual=new byte[expected.Length];Marshal.Copy(pointer,actual,0,actual.Length);
            Require(actual.SequenceEqual(expected),"Target differs or already has a detour: "+method.Name);
        }
        static IntPtr Target(object detour)=>(IntPtr)Native.T("Il2CppInterop.Runtime.Injection.IDetour").GetProperty("Target")!.GetValue(detour)!;
        static IntPtr Trampoline(object detour)=>(IntPtr)Native.T("Il2CppInterop.Runtime.Injection.IDetour").GetProperty("OriginalTrampoline")!.GetValue(detour)!;
        static object Create<T>(MethodInfo method,T export,out T original) where T:Delegate
        {
            var patches=Harmony.GetPatchInfo(method);Require(patches is null||!patches.Owners.Any(),"Conflicting command patch.");
            var resolver=Native.T("Il2CppInterop.Runtime.Injection.InjectorHelpers").GetMethod("GetIl2CppMethodPointer",Flags,null,new[]{typeof(MethodBase)},null)
                ??throw new MissingMethodException("Exact method-pointer resolver unavailable.");
            Require(resolver.ReturnType==typeof(IntPtr),"Resolver return differs.");IntPtr pointer=(IntPtr)Native.Invoke(resolver,null,method)!;ValidateOriginal(method,pointer);
            object runtime=Native.Static("Il2CppInterop.Runtime.Startup.Il2CppInteropRuntime","Instance")!;
            object provider=Native.Get(runtime,"DetourProvider")!;
            var create=Native.T("Il2CppInterop.Runtime.Injection.IDetourProvider").GetMethods().Single(x=>x.Name=="Create"&&x.IsGenericMethodDefinition&&x.GetGenericArguments().Length==1&&x.GetParameters().Length==2);
            object detour=Native.Invoke(create.MakeGenericMethod(typeof(T)),provider,pointer,export)!;
            try
            {
                var contract=Native.T("Il2CppInterop.Runtime.Injection.IDetour");
                var generate=contract.GetMethods().Single(x=>x.Name=="GenerateTrampoline"&&x.IsGenericMethodDefinition&&x.GetParameters().Length==0);
                original=(T)Native.Invoke(generate.MakeGenericMethod(typeof(T)),detour)!;
                Require(Target(detour)==pointer&&(IntPtr)contract.GetProperty("Detour")!.GetValue(detour)! ==Marshal.GetFunctionPointerForDelegate(export)&&Trampoline(detour)!=IntPtr.Zero,"Provider changed target/export/trampoline.");
                return detour;
            }
            catch{((IDisposable)detour).Dispose();throw;}
        }
        internal static void Install()
        {
            NativeTrainLeafProbeTrigger.ValidateMode();
            Require(NativeCommandOuterProbe.Requested&&NativeTrainOuterProbe.Requested&&NativeStateBridge.Installed&&NativeTrainFactoryObserverHooks.Installed&&!NativeFreshSession.Enabled,"Missing explicit preceding private trials.");
            _=NativeBehaviorSession.RequireCreatedClient();
            Require(!Attempted&&!Installed&&!Faulted&&Library==IntPtr.Zero,"Setup cannot repeat.");Attempted=true;
            Profile=NativePlatformContract.Current();
            Require(Marshal.SizeOf<B.Call>()==48&&Marshal.SizeOf<B.Value>()==16&&Marshal.SizeOf<B.Plan>()==24&&Marshal.SizeOf<B.Config>()==88&&
                Marshal.OffsetOf<B.Call>(nameof(B.Call.MethodInfo)).ToInt32()==40&&Marshal.OffsetOf<B.Plan>(nameof(B.Plan.Handled)).ToInt32()==8&&Marshal.OffsetOf<B.Config>(nameof(B.Config.Name)).ToInt32()==80,"Managed typed ABI differs.");
            string game=Path.GetFullPath((string)Native.Static("BepInEx.Paths","GameRootPath")!);
            string binary=NativePlatformLibrary.ValidateGame(Profile,game);
            Library=NativePlatformLibrary.LoadOwn(Profile,NativePlatformContract.BridgeKind.Command,game,typeof(NativeCommandOuterBridge).Assembly);GameLibrary=NativeLibrary.Load(binary);
            Require(Export<Number>(Library,"dcp_cmd_bridge_abi_version")()==1&&Export<Number>(Library,"dcp_cmd_bridge_call_size")()==48&&Export<Number>(Library,"dcp_cmd_bridge_value_size")()==16&&Export<Number>(Library,"dcp_cmd_bridge_plan_size")()==24&&Export<Number>(Library,"dcp_cmd_bridge_config_size")()==88,"Native ABI differs.");
            IntPtr space=Marshal.StringToCoTaskMemUTF8("System"),name=Marshal.StringToCoTaskMemUTF8("InvalidOperationException");
            try
            {
                IntPtr image=Export<GetImage>(GameLibrary,"il2cpp_get_corlib")();
                Require(image!=IntPtr.Zero&&Export<GetClass>(GameLibrary,"il2cpp_class_from_name")(image,space,name)!=IntPtr.Zero,"Native exception class unavailable.");
                IntPtr write=NativeLibrary.GetExport(Library,"dcp_cmd_bridge_to_bytes"),read=NativeLibrary.GetExport(Library,"dcp_cmd_bridge_from_bytes");
                WriteExport=Marshal.GetDelegateForFunctionPointer<B.WriteOriginal>(write);ReadExport=Marshal.GetDelegateForFunctionPointer<B.ReadOriginal>(read);
                Require(Marshal.GetFunctionPointerForDelegate(WriteExport)==write&&Marshal.GetFunctionPointerForDelegate(ReadExport)==read,"Export roundtrip inserted managed boundary.");
                Type bytes=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
                var to=Native.M("CommandBase","ToByteArray","CommandBase","System.Int32");
                var from=Native.M("CommandBase","FromByteArray",bytes.FullName!,"CommandBase&","System.Int32&");
                Require(to.IsStatic&&from.IsStatic&&to.ReturnType==bytes&&from.ReturnType==typeof(bool)&&to.DeclaringType==Native.T("CommandBase")&&from.DeclaringType==Native.T("CommandBase"),"Command method shape differs.");
                WriteDetour=Create(to,WriteExport,out WriteTrampoline);ReadDetour=Create(from,ReadExport,out ReadTrampoline);
                var config=new B.Config{Size=88,Version=1,Write=Trampoline(WriteDetour),Read=Trampoline(ReadDetour),
                    Prepare=Marshal.GetFunctionPointerForDelegate(B.PrepareRoot),Finish=Marshal.GetFunctionPointerForDelegate(B.FinishRoot),Cleanup=Marshal.GetFunctionPointerForDelegate(B.CleanupRoot),
                    Factory=NativeLibrary.GetExport(GameLibrary,"il2cpp_exception_from_name_msg"),Raise=NativeLibrary.GetExport(GameLibrary,"il2cpp_raise_exception"),Image=image,Namespace=space,Name=name};
                Require(Export<Configure>(Library,"dcp_cmd_bridge_configure")(ref config)==0,"Native immutable configuration rejected.");
                ValidateOriginal(to,Target(WriteDetour));ValidateOriginal(from,Target(ReadDetour));
                ApplyAttempted=true;
                var apply=Native.T("Il2CppInterop.Runtime.Injection.IDetour").GetMethod("Apply")!;
                Native.Invoke(apply,WriteDetour);Native.Invoke(apply,ReadDetour);Installed=true;
                Entry.Note("native-command-outer-installed","Typed CommandBase ABI1; both native exports and trampolines verified. Explicit private invocation only; handles retained to process exit.");
            }
            catch
            {
                Faulted=true;
                // Before Apply no native caller can enter these trampolines.
                // Afterwards retain even a partially applied pair and all roots.
                if(!ApplyAttempted){try{(ReadDetour as IDisposable)?.Dispose();}finally{(WriteDetour as IDisposable)?.Dispose();}}
                throw;
            }
            finally{Marshal.FreeCoTaskMem(space);Marshal.FreeCoTaskMem(name);}
        }
    }
}
