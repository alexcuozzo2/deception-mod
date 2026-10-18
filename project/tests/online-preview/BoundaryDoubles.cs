using System;
using System.Collections.Generic;
using System.Reflection;

namespace HarmonyLib
{
    public static class Priority {public const int First=800;}
    public sealed class HarmonyMethod {public MethodInfo method;public int priority;public HarmonyMethod(MethodInfo method){this.method=method;}}
    public sealed class Harmony
    {
        public readonly List<(MethodInfo original,HarmonyMethod prefix,HarmonyMethod finalizer)> Hooks=new();
        public void Patch(MethodInfo original,HarmonyMethod prefix,HarmonyMethod finalizer)=>Hooks.Add((original,prefix,finalizer));
    }
}
namespace Deception.PolyModAdapter
{
    // Runtime filter is exercised separately by clean-beta fixtures.
    internal static class NativeCleanBetaCompatibility
    {
        internal const string CleanSha256="a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016";
        internal static bool Installed;
    }
    // Resignation's real adapter and dispatcher have a separate behavioral fixture.
    internal static class NativeOnlineMaintenance
    {
        internal static void Install(HarmonyLib.Harmony harmony,System.Reflection.Assembly exactPreview){}
    }
    internal static class NativeOnlineResign
    {
        internal static void Install(HarmonyLib.Harmony harmony,Assembly exactPreview){}
    }
    // Dispatch's real queue and threads are exercised by its separate source
    // fixture. This double reenters the adapter to verify scope routing.
    internal static class NativeOnlineDispatch
    {
        internal static bool IsMainThread=true;
        internal static int Invoked;
        internal static Func<MethodInfo,object[],object?>? Handler;
        internal static void Initialize(){}
        internal static object? Invoke(MethodInfo method,object[] args)
        {
            Invoked++;bool before=IsMainThread;IsMainThread=true;
            try{return Handler!(method,args);}finally{IsMainThread=before;}
        }
        internal static void Reset(){IsMainThread=true;Invoked=0;}
    }
    internal static class Native
    {
        internal static Type T(string name)=>name=="Il2CppSystem.Guid"?typeof(Guid):throw new NotSupportedException(name);
        internal static MethodInfo M(string owner,string name,params string[] args)=>T(owner).GetMethod(name,Type.EmptyTypes)!;
        internal static object? Invoke(MethodInfo method,object? target,params object?[] args)=>method.Invoke(target,args);
        internal static object? Get(object obj,string property)=>obj.GetType().GetProperty(property)!.GetValue(obj);
        internal static object Static(string owner,string property)=>owner=="VersionManager"?(object)(property=="GameVersion"?123:28):throw new NotSupportedException(owner);
        internal static int Int(object? value)=>Convert.ToInt32(value);
        internal static void RequireReturn(MethodInfo method,Type expected)=>throw new NotSupportedException("Fixture does not install native hooks.");
        internal static void Hook(HarmonyLib.Harmony harmony,MethodInfo method,Type patch,string? pre=null,string? post=null,string? final=null)=>throw new NotSupportedException("Fixture does not install hooks.");
    }
    internal static class NativeStateProbe {internal static byte[] ManagedBytes(object value)=>(byte[])value;}
    internal static class NativeOnlineSession
    {
        internal static Guid Epoch;
        internal static byte[]? Bytes;
        internal static string Purpose="";
        internal static int Started,Disposed;
        internal static Exception? Error;
        sealed class Scope:IDisposable {public void Dispose(){Disposed++;}}
        internal static void Reset(){Epoch=Guid.Empty;Bytes=null;Purpose="";Started=Disposed=0;Error=null;}
        internal static IDisposable BeginCreation(Guid epoch){Epoch=epoch;Purpose="create";Started++;return new Scope();}
        internal static IDisposable BeginDetached(Guid epoch,byte[] bytes,string purpose){Epoch=epoch;Bytes=bytes;Purpose=purpose;Started++;return new Scope();}
        internal static IDisposable BeginCount(byte[] bytes){Bytes=bytes;Purpose="count";Started++;return new Scope();}
        internal static Exception? Finish(IDisposable? scope,Exception? error){Error=error;scope?.Dispose();return error;}
    }
}
