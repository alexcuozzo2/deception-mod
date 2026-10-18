using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Deception.PolyModAdapter;

// These are signature-shape doubles only. No Harmony detour or native game
// method is executed. Production Native.Hook must reject before this sink.
namespace HarmonyLib
{
    public sealed class HarmonyMethod
    {
        public static int Constructed;
        public MethodInfo Method;
        public HarmonyMethod(MethodInfo method){Method=method;Constructed++;}
    }
    public sealed class Harmony
    {
        public int Calls;
        public MethodInfo? Original;
        public HarmonyMethod? Prefix,Postfix,Finalizer;
        public object? Transpiler,IlManipulator;
        public void Patch(MethodInfo target,HarmonyMethod? pre,HarmonyMethod? post,object? trans,HarmonyMethod? final,object? il)
        {Calls++;Original=target;Prefix=pre;Postfix=post;Transpiler=trans;Finalizer=final;IlManipulator=il;}
    }
}
namespace Il2CppSystem
{
    public class Object { }
    public class ValueType:Object { }
}
namespace HookAbiFixture
{
    // Installed PathFinderSettings is a managed class wrapper for a native
    // 56-byte value, unlike the real 8-byte CLR WorldCoordinates struct.
    public class PathFinderSettings:Il2CppSystem.ValueType
    {
        public static int Calls;
        public int InstanceMethod(){Calls++;return 1;}
        public static int StaticMethod()=>1;
        public static void StaticWithWrapped(PathFinderSettings settings){ }
    }
    public sealed class DerivedSettings:PathFinderSettings { }
    public sealed class NativeReference:Il2CppSystem.Object { }
    [StructLayout(LayoutKind.Sequential)] public struct WorldCoordinates {public int x,y;}
    [StructLayout(LayoutKind.Sequential)] public struct Vector2 {public float x,y;}
    public enum UnitType {Warrior,Cloak}
    public sealed class ValueType { } // Matching short name alone is not a wrapper.
    public sealed class WrappedField {public PathFinderSettings? Field;}
    public static class Targets
    {
        static PathFinderSettings Settings=new();
        public static int Bodies;
        public static bool IsAllowedToFinishOnTile(PathFinderSettings settings,NativeReference tile){Bodies++;return true;}
        public static void WrappedRef(ref PathFinderSettings settings){Bodies++;}
        public static void WrappedOut(out PathFinderSettings settings){settings=new();Bodies++;}
        public static PathFinderSettings WrappedReturn(){Bodies++;return Settings;}
        public static ref PathFinderSettings WrappedRefReturn()=>ref Settings;
        public static void DerivedWrapped(DerivedSettings settings){Bodies++;}
        public static bool SafeNativeReferences(NativeReference unit,NativeReference tile)=>true;
        public static WorldCoordinates SafeCoordinates(WorldCoordinates position)=>position;
        public static Vector2 SafeVector(Vector2 value)=>value;
        public static void SafeCoordinateRef(ref WorldCoordinates position){ }
        public static bool SafeEnum(UnitType type)=>true;
        public static void SafeScalar(int n,byte player,float duration,bool enabled,string text){ }
        public static void SafeScalarRef(ref int n,out string reason){reason="";}
        public static Guid SafeGuid(Guid id)=>id;
        public static PathFinderSettings[] SafeArray(PathFinderSettings[] array)=>array;
        public static List<PathFinderSettings> SafeList(List<PathFinderSettings> list)=>list;
        public static ValueType SafeNamesake(ValueType value)=>value;
        public static WrappedField SafeReferenceWithWrappedField(WrappedField value)=>value;
        public static Il2CppSystem.Object SafeNativeObject(Il2CppSystem.Object value)=>value;
        public static void SafeNoArguments(){ }
    }
    public static class Patches
    {
        public static int Calls;
        public static void Pre(){Calls++;}
        public static void Post(){Calls++;}
        public static void Final(){Calls++;}
    }
}
namespace Deception.Tests
{
    using HookAbiFixture;
    using HarmonyLib;
    public static class HookAbiTests
    {
        static int Assertions;
        static readonly List<string> Completed=new();
        static void Check(bool ok,string why){Assertions++;if(!ok)throw new Exception(why);}
        static MethodInfo Method(Type type,string name)=>type.GetMethod(name,BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance)!;
        static MethodInfo Method(string name)=>Method(typeof(Targets),name);
        static void Test(string id,Action action){try{action();Completed.Add(id);}catch(Exception e){throw new Exception(id,e);}}
        static void Rejected(MethodInfo target,bool missingPatch=false)
        {
            var h=new Harmony();int constructed=HarmonyMethod.Constructed;
            Exception? error=null;
            try{Native.Hook(h,target,typeof(Patches),missingPatch?"MissingPatch":nameof(Patches.Pre),nameof(Patches.Post),nameof(Patches.Final));}
            catch(Exception e){error=e;}
            Check(error is NotSupportedException,"Wrapped native value must fail explicitly before registration");
            Check(error!.Message.Contains(target.Name,StringComparison.Ordinal),"Rejection must identify the offending target");
            Check(h.Calls==0&&h.Original is null,"Unsafe native signature reached Harmony.Patch");
            Check(HarmonyMethod.Constructed==constructed,"Unsafe native signature constructed patch wrappers before validation");
        }
        static void Allowed(MethodInfo target)
        {
            var h=new Harmony();Native.Hook(h,target,typeof(Patches),nameof(Patches.Pre),nameof(Patches.Post),nameof(Patches.Final));
            Check(h.Calls==1&&h.Original==target,"Safe hook registration was blocked or changed");
            Check(h.Prefix!.Method==Method(typeof(Patches),nameof(Patches.Pre))&&h.Postfix!.Method==Method(typeof(Patches),nameof(Patches.Post))&&h.Finalizer!.Method==Method(typeof(Patches),nameof(Patches.Final)),"Patch method identity changed");
            Check(h.Transpiler is null&&h.IlManipulator is null,"Hook introduced an unrelated transform");
        }
        public static int Run()
        {
            try
            {
                Test("HOOK-001-native-56-byte-wrapper-parameter-rejected",()=>Rejected(Method(nameof(Targets.IsAllowedToFinishOnTile))));
                Test("HOOK-002-wrapper-ref-parameter-rejected",()=>Rejected(Method(nameof(Targets.WrappedRef))));
                Test("HOOK-003-wrapper-out-parameter-rejected",()=>Rejected(Method(nameof(Targets.WrappedOut))));
                Test("HOOK-004-wrapper-return-rejected",()=>Rejected(Method(nameof(Targets.WrappedReturn))));
                Test("HOOK-005-wrapper-ref-return-rejected",()=>Rejected(Method(nameof(Targets.WrappedRefReturn))));
                Test("HOOK-006-derived-wrapper-parameter-rejected",()=>Rejected(Method(nameof(Targets.DerivedWrapped))));
                Test("HOOK-007-wrapper-instance-receiver-rejected",()=>Rejected(Method(typeof(PathFinderSettings),nameof(PathFinderSettings.InstanceMethod))));
                Test("HOOK-008-derived-wrapper-instance-receiver-rejected",()=>Rejected(Method(typeof(DerivedSettings),nameof(PathFinderSettings.InstanceMethod))));
                Test("HOOK-009-wrapper-static-parameter-rejected",()=>Rejected(Method(typeof(PathFinderSettings),nameof(PathFinderSettings.StaticWithWrapped))));
                Test("HOOK-010-rejection-precedes-patch-method-resolution",()=>Rejected(Method(nameof(Targets.IsAllowedToFinishOnTile)),true));
                Test("HOOK-011-safe-wrapper-static-method-accepted",()=>Allowed(Method(typeof(PathFinderSettings),nameof(PathFinderSettings.StaticMethod))));
                foreach(string name in new[]{nameof(Targets.SafeNativeReferences),nameof(Targets.SafeCoordinates),nameof(Targets.SafeVector),nameof(Targets.SafeCoordinateRef),nameof(Targets.SafeEnum),nameof(Targets.SafeScalar),nameof(Targets.SafeScalarRef),nameof(Targets.SafeGuid),nameof(Targets.SafeArray),nameof(Targets.SafeList),nameof(Targets.SafeNamesake),nameof(Targets.SafeReferenceWithWrappedField),nameof(Targets.SafeNativeObject),nameof(Targets.SafeNoArguments)})
                    Test("HOOK-safe-"+name,()=>Allowed(Method(name)));
                Test("HOOK-026-coordinate-shape-is-real-eight-byte-clr-value",()=>{
                    Check(typeof(WorldCoordinates).IsValueType&&Marshal.SizeOf<WorldCoordinates>()==8,"Coordinate double must match pinned ABI classification");
                    Check(!typeof(PathFinderSettings).IsValueType&&typeof(PathFinderSettings).BaseType!.FullName=="Il2CppSystem.ValueType","Unsafe double must match pinned wrapper classification");
                });
                Test("HOOK-027-reflection-invoke-wrapper-results-remains-available",()=>{
                    int before=Targets.Bodies;object? result=Native.Invoke(Method(nameof(Targets.WrappedReturn)),null);
                    Check(result is PathFinderSettings&&Targets.Bodies==before+1,"Ordinary generated-wrapper invocation was incorrectly prohibited");
                });
                Test("HOOK-028-valid-hook-without-optional-patches-unchanged",()=>{
                    var h=new Harmony();Native.Hook(h,Method(nameof(Targets.SafeNoArguments)),typeof(Patches));
                    Check(h.Calls==1&&h.Prefix is null&&h.Postfix is null&&h.Finalizer is null,"Null patch roles changed");
                });
                Test("HOOK-029-missing-safe-patch-does-not-register",()=>{
                    var h=new Harmony();Exception? error=null;try{Native.Hook(h,Method(nameof(Targets.SafeNoArguments)),typeof(Patches),"MissingPatch");}catch(Exception e){error=e;}
                    Check(error is MissingMethodException&&h.Calls==0,"Safe hook's missing patch behavior changed");
                });
                Test("HOOK-030-repeated-rejection-preserves-earlier-safe-registration",()=>{
                    var h=new Harmony();var safe=Method(nameof(Targets.SafeCoordinates));Native.Hook(h,safe,typeof(Patches),nameof(Patches.Pre));
                    var prefix=h.Prefix;int constructed=HarmonyMethod.Constructed;
                    for(int i=0;i<20;i++){try{Native.Hook(h,Method(nameof(Targets.IsAllowedToFinishOnTile)),typeof(Patches),nameof(Patches.Pre));throw new Exception("Unsafe retry was accepted");}catch(NotSupportedException){ }}
                    Check(h.Calls==1&&h.Original==safe&&ReferenceEquals(h.Prefix,prefix),"Rejected hook changed previously accepted registration");
                    Check(HarmonyMethod.Constructed==constructed,"Rejected hook retries created patch wrappers");
                });
                Test("HOOK-031-registration-never-runs-native-or-patch-bodies",()=>Check(PathFinderSettings.Calls==0&&Targets.Bodies==1&&Patches.Calls==0,"Registration ran a target or callback body"));
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Completed.Count,assertions=Assertions,results=Completed,nativeGame="NOT RUN",scope="Production Native.Hook; synthetic method signatures and recording Harmony boundary only"}));return 0;
            }
            catch(Exception e){Console.WriteLine(e);return 1;}
        }
    }
}
