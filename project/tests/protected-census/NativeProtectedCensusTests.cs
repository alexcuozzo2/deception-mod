using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.PolyModAdapter;
namespace HarmonyLib
{
    public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo method){}}
    public sealed class Harmony{public void Patch(MethodInfo method,HarmonyMethod? a,HarmonyMethod? b,object? c,HarmonyMethod? d,object? e)=>throw new Exception("No hooks in census fixture");}
}
namespace Il2CppSystem
{
    public class Object{public IntPtr Pointer{get;}public Object(long pointer){Pointer=new(pointer);}}
}
public sealed class ArrayLikeWrapper
{
    public IntPtr Pointer{get;}public int Calls;public int Behavior;public Action? DuringCast;
    public ArrayLikeWrapper(long pointer,int behavior=0){Pointer=new(pointer);Behavior=behavior;}
    public T Cast<T>() where T:class
    {
        Calls++;DuringCast?.Invoke();if(Behavior==1)throw new InvalidCastException("Synthetic failed native cast");
        if(Behavior==2)return null!;return (T)(object)new Il2CppSystem.Object(Pointer.ToInt64()+(Behavior==3?1:0));
    }
}
namespace Deception.Tests
{
    public static class NativeProtectedCensusDraftTests
    {
        static readonly List<object> Results=new();static int Failed;
        static void Check(bool ok){if(!ok)throw new Exception("Census fixture assertion failed");}
        static void Reject(Action action){try{action();}catch(InvalidDataException){return;}catch(InvalidCastException){return;}throw new Exception("Expected reject");}
        static void Test(string id,Action action){try{action();Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e);}}
        public static int Run()
        {
            Test("census-native-object-retains-managed-wrapper",()=>{var value=new Il2CppSystem.Object(10);var result=NativeProtectedCensus.Normalize(new object[]{value});Check(result.Length==1&&ReferenceEquals(result[0],value));});
            Test("census-array-wrapper-same-pointer-cast",()=>{var value=new ArrayLikeWrapper(20);Check(!typeof(Il2CppSystem.Object).IsInstanceOfType(value));var result=NativeProtectedCensus.Normalize(new object[]{value});Check(value.Calls==1&&result[0] is Il2CppSystem.Object&&Native.Same(result[0],value));});
            Test("census-mixed-order-aliases-and-input-unchanged",()=>{var a=new ArrayLikeWrapper(30);var b=new Il2CppSystem.Object(40);var alias=new ArrayLikeWrapper(30);object[] input={a,b,alias};var before=(object[])input.Clone();var result=NativeProtectedCensus.Normalize(input);Check(result.Length==3&&result.Select(Native.Pointer).SequenceEqual(new long[]{30,40,30})&&input.SequenceEqual(before)&&a.Calls==1&&alias.Calls==1);});
            Test("census-wrong-pointer-cast-rejected",()=>{var a=new ArrayLikeWrapper(50,3);Reject(()=>NativeProtectedCensus.Normalize(new object[]{a}));Check(a.Pointer.ToInt64()==50&&a.Calls==1);});
            Test("census-null-cast-rejected",()=>Reject(()=>NativeProtectedCensus.Normalize(new object[]{new ArrayLikeWrapper(60,2)})));
            Test("census-native-cast-exception-preserved",()=>Reject(()=>NativeProtectedCensus.Normalize(new object[]{new ArrayLikeWrapper(70,1)})));
            Test("census-zero-pointer-rejected-before-cast",()=>{var a=new ArrayLikeWrapper(0);Reject(()=>NativeProtectedCensus.Normalize(new object[]{a}));Check(a.Calls==0);});
            Test("census-mid-conversion-input-change-rejected",()=>{var a=new ArrayLikeWrapper(80);var list=new List<object>{a};a.DuringCast=()=>list[0]=new Il2CppSystem.Object(90);Reject(()=>NativeProtectedCensus.Normalize(list));});
            Test("census-null-item-and-bound-rejected",()=>{Reject(()=>NativeProtectedCensus.Normalize(new object[]{null!}));Reject(()=>NativeProtectedCensus.Normalize(new object[8192]));});
            File.WriteAllText(Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT")!,JsonSerializer.Serialize(new{scope="CLR cast-wrapper model using actual NativeApi.As; no native weak/cast execution",passed=Results.Count-Failed,failed=Failed,results=Results},new JsonSerializerOptions{WriteIndented=true}));return Failed==0?0:1;
        }
    }
}
