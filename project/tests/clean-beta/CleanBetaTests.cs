using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deception.PolyModAdapter;
class Fixture
{
    internal static bool allowGldMods;
    static void Target(){}
    static void Other(){}
    static int Main()
    {
        var type=typeof(NativeCleanBetaCompatibility);const BindingFlags f=BindingFlags.NonPublic|BindingFlags.Static;
        var selected=type.GetField("Selected",f)!;var dormant=type.GetField("Dormant",f)!;
        var before=type.GetMethod("BeforeCreate",f)!.MakeGenericMethod(typeof(string));
        var target=typeof(Fixture).GetMethod("Target",f)!;var other=typeof(Fixture).GetMethod("Other",f)!;
        object?[] args={target,new[]{"preserve"}};
        Check((bool)before.Invoke(null,args)!&&((IEnumerable<string>)args[1]!).Single()=="preserve","uninstalled passthrough");
        selected.SetValue(null,target);dormant.SetValue(null,typeof(Fixture).GetField("allowGldMods",f));
        args=new object?[]{other,new[]{"preserve"}};
        Check((bool)before.Invoke(null,args)!&&((IEnumerable<string>)args[1]!).Single()=="preserve","other method preserved");
        args=new object?[]{target,null};
        Check(!(bool)before.Invoke(null,args)!&&args[1] is IEnumerable<string> e&&!e.Any(),"target returns typed empty enumerable");
        allowGldMods=true;
        try{before.Invoke(null,new object?[]{target,null});throw new Exception("accepted enabled GLD");}
        catch(TargetInvocationException ex){Check(ex.InnerException is InvalidDataException,"enabled GLD rejects");}
        allowGldMods=false;dormant.SetValue(null,null);
        try{before.Invoke(null,new object?[]{target,null});throw new Exception("accepted missing flag");}
        catch(TargetInvocationException ex){Check(ex.InnerException is InvalidDataException,"missing flag rejects");}
        selected.SetValue(null,null);Check(!NativeCleanBetaCompatibility.Install(new HarmonyLib.Harmony()),"no loader remains untouched");
        Console.WriteLine("6 clean-beta filter cases passed (boundary doubles; native Harmony not executed)");return 0;
    }
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
}
namespace Deception.PolyModAdapter {static class Entry {internal static void Note(string kind,string text){}} static class Native {internal static Type T(string n)=>typeof(object);internal static MethodInfo M(string n,string m,params string[] p)=>throw new Exception("unexpected native lookup");}}
namespace HarmonyLib
{
    class PatchInfo {public IEnumerable<string> Owners=>Array.Empty<string>();}
    class HarmonyMethod {public HarmonyMethod(MethodInfo m){}}
    class Harmony {public static PatchInfo? GetPatchInfo(MethodInfo m)=>null;public void Patch(MethodInfo m,HarmonyMethod prefix)=>throw new Exception("unexpected patch");}
}
