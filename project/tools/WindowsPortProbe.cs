using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using BepInEx;
using BepInEx.Unity.IL2CPP;

// Diagnostic plugin only. Resolves wrapper pointers and reads code bytes;
// never invokes a gameplay method or installs detours. An optional hash-checked
// helper tests native exception/callback boundaries without gameplay actions.
[BepInPlugin("alex.deception.windows-port-probe", "Deception Windows Port Probe", "0.1.0")]
public sealed class WindowsPortProbe : BasePlugin
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Callback(int stage);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int ExceptionProbe(IntPtr image,IntPtr factory,IntPtr raise,Callback callback);
    public override void Load()
    {
        string output = Path.Combine(BepInEx.Paths.BepInExRootPath,"deception-windows-probe-"+Guid.NewGuid().ToString("N")+".json");
        var rows = new List<object>();
        var shared = new List<object>();
        var allMethods = new List<object>();
        string? failure = null;
        int? exceptionResult = null;
        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.ProcessArchitecture != Architecture.X64)
                throw new PlatformNotSupportedException("Probe requires a Windows x64 process.");
            using var requestStream = GetType().Assembly.GetManifestResourceStream("Deception.WindowsProbe.Request.json")!;
            using var request = JsonDocument.Parse(requestStream);
            string imagePath = Path.Combine(BepInEx.Paths.GameRootPath,"GameAssembly.dll");
            using var image = File.OpenRead(imagePath);
            using var sha = SHA256.Create();
            string hash = Convert.ToHexString(sha.ComputeHash(image)).ToLowerInvariant();
            if (hash != request.RootElement.GetProperty("game_sha256").GetString())
                throw new InvalidDataException("Game image changed after probe compilation.");
            using var process = Process.GetCurrentProcess();
            var module = process.Modules.Cast<ProcessModule>().Single(m=>
                string.Equals(Path.GetFullPath(m.FileName),Path.GetFullPath(imagePath),StringComparison.OrdinalIgnoreCase));
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var resolverType = assemblies.Select(a=>a.GetType("Il2CppInterop.Runtime.Injection.InjectorHelpers",false)).Single(t=>t!=null)!;
            var resolver = resolverType.GetMethod("GetIl2CppMethodPointer",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,
                null,new[]{typeof(MethodBase)},null) ?? throw new MissingMethodException("Native pointer resolver unavailable.");
            var all = new Dictionary<long,List<string>>();
            foreach(var assembly in assemblies.Where(a=>a.GetName().Name is "GameLogicAssembly" or "PolytopiaAssembly"))
            foreach(var type in assembly.GetTypes().Where(t=>!t.ContainsGenericParameters))
            foreach(var method in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly).Where(m=>!m.ContainsGenericParameters))
            {
                IntPtr pointer;
                try { pointer=(IntPtr)resolver.Invoke(null,new object[]{method})!; }
                catch { continue; } // Non-native helpers have no method pointer.
                long offset=pointer.ToInt64()-module.BaseAddress.ToInt64();
                if(offset<0||offset>module.ModuleMemorySize-32)continue;
                allMethods.Add(new {method=type.FullName+"."+method.Name,
                    signature=method.ToString(),is_static=method.IsStatic,rva=offset});
                if(!all.TryGetValue(offset,out var aliases))all[offset]=aliases=new List<string>();
                aliases.Add(type.FullName+"::"+method+" static="+method.IsStatic);
            }
            foreach(var pair in all.Where(p=>p.Value.Count>1).OrderBy(p=>p.Key))
            {
                byte[] code=new byte[32];Marshal.Copy(module.BaseAddress+(int)pair.Key,code,0,code.Length);
                shared.Add(new {rva=pair.Key,prologue=Convert.ToHexString(code).ToLowerInvariant(),methods=pair.Value});
            }
            foreach (var entry in request.RootElement.GetProperty("methods").EnumerateArray())
            {
                string key = entry.GetString()!;
                int split = key.LastIndexOf('.');
                string owner = key.Substring(0,split), name = key.Substring(split+1);
                var type = assemblies.Select(a=>a.GetType(owner,false)).Single(t=>t!=null)!;
                var methods = type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly)
                    .Where(m=>m.Name==name).ToArray();
                if (methods.Length == 0) throw new MissingMethodException(key);
                foreach (var method in methods)
                {
                    IntPtr pointer = (IntPtr)resolver.Invoke(null,new object[]{method})!;
                    long offset = pointer.ToInt64()-module.BaseAddress.ToInt64();
                    if (offset<0 || offset>module.ModuleMemorySize-32) throw new InvalidDataException("Pointer outside game image: "+key);
                    byte[] bytes = new byte[32]; Marshal.Copy(pointer,bytes,0,bytes.Length);
                    rows.Add(new { method=key, signature=method.ToString(), is_static=method.IsStatic,
                        rva=offset, prologue=Convert.ToHexString(bytes).ToLowerInvariant() });
                }
            }
            if (request.RootElement.TryGetProperty("exception_probe_sha256",out var expectedProbe))
            {
                using var embedded = GetType().Assembly.GetManifestResourceStream("Deception.WindowsExceptionProbe.dll")!;
                using var buffer = new MemoryStream(); embedded.CopyTo(buffer);
                byte[] payload = buffer.ToArray();
                string digest = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
                if (digest != expectedProbe.GetString()) throw new InvalidDataException("Diagnostic helper hash mismatch.");
                string nativePath = Path.Combine(BepInEx.Paths.CachePath,"deception-windows-exception-probe-"+digest+".dll");
                Directory.CreateDirectory(BepInEx.Paths.CachePath);
                if (!File.Exists(nativePath)) { using var file = new FileStream(nativePath,FileMode.CreateNew); file.Write(payload); }
                if ((File.GetAttributes(nativePath)&FileAttributes.ReparsePoint)!=0 ||
                    Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(nativePath))).ToLowerInvariant()!=digest)
                    throw new InvalidDataException("Cached diagnostic helper differs.");
                IntPtr native = NativeLibrary.Load(nativePath);
                var test = Marshal.GetDelegateForFunctionPointer<ExceptionProbe>(NativeLibrary.GetExport(native,"dcp_windows_exception_probe"));
                int last = 0;
                Callback callback = stage => { if(stage != last+1)return -1; last=stage; return stage+100; };
                exceptionResult = test(NativeLibrary.GetExport(module.BaseAddress,"il2cpp_get_corlib"),
                    NativeLibrary.GetExport(module.BaseAddress,"il2cpp_exception_from_name_msg"),
                    NativeLibrary.GetExport(module.BaseAddress,"il2cpp_raise_exception"),callback);
                GC.KeepAlive(callback);
                if (exceptionResult != 0 || last!=3) throw new InvalidOperationException("Native exception probe failed: "+exceptionResult);
                Log.LogInfo("Windows native IL2CPP exception probe passed; three managed callbacks and two native catches.");
                // Retain the diagnostic module for this process lifetime.
            }
            Log.LogInfo("Windows method inventory complete; no gameplay hooks installed.");
        }
        catch(Exception error) { failure=error.ToString(); Log.LogError(error); }
        File.WriteAllText(output,JsonSerializer.Serialize(new {
            schema=1, all_methods=allMethods, status=failure==null?"POINTER_INVENTORY_COMPLETE":"FAILED", error=failure,
            os_architecture=RuntimeInformation.OSArchitecture.ToString(),
            process_architecture=RuntimeInformation.ProcessArchitecture.ToString(),
            runtime_admitted=false, gameplay_verified=false, methods=rows
            , shared_bodies=shared
            , native_exception_probe=exceptionResult
        },new JsonSerializerOptions { WriteIndented=true }));
        Log.LogInfo("Windows port probe report: "+output);
    }
}
