using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Transitional runtime compatibility, NOT an edited/repackaged PolyMod DLL.
    // The exact beta registers a dormant CLR-reader postfix on an IL2CPP-reader
    // serializer. Filter only its attribute discovery before Client.Init runs.
    // Enabling allowGldMods later via reflection is unsupported; this does not
    // implement that optional protocol or relax our native serializer guards.
    internal static class NativeCleanBetaCompatibility
    {
        internal const string CleanSha256="a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016";
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
        static MethodInfo? Selected;
        static FieldInfo? Dormant;
        internal static bool Installed {get;private set;}
        internal static int Filtered {get;private set;}
        static void Require(bool ok,string message)
        {if(!ok)throw new InvalidDataException("Clean PolyMod beta compatibility: "+message);}
        internal static bool Install(Harmony harmony)
        {
            Require(!Installed&&Selected is null,"Already installed.");
            var assemblies=AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name=="PolyMod").ToArray();
            if(assemblies.Length!=1)return false;
            var assembly=assemblies[0];
            if(string.IsNullOrEmpty(assembly.Location)||!File.Exists(assembly.Location)||
                !string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))),CleanSha256,StringComparison.OrdinalIgnoreCase))return false;
            Type client=assembly.GetType("PolyMod.Multiplayer.Client",true)!;
            var selected=client.GetMethods(Flags|BindingFlags.DeclaredOnly).Single(m=>m.Name=="Deserialize_Postfix");
            var parameters=selected.GetParameters();
            Require(selected.IsPrivate&&selected.IsStatic&&!selected.IsGenericMethod&&selected.ReturnType==typeof(void)&&
                parameters.Length==2&&parameters[0].ParameterType==Native.T("GameState")&&parameters[0].Name=="__instance"&&
                parameters[1].ParameterType==typeof(BinaryReader)&&parameters[1].Name=="__0","Unexpected postfix signature.");
            var attributes=selected.GetCustomAttributesData().Where(a=>a.AttributeType.Namespace=="HarmonyLib").ToArray();
            Require(attributes.Length==2&&attributes.Count(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPatch")==1&&
                attributes.Count(a=>a.AttributeType.FullName=="HarmonyLib.HarmonyPostfix")==1,"Unexpected registration attributes.");
            var dormant=client.GetField("allowGldMods",Flags|BindingFlags.DeclaredOnly);
            Require(dormant is not null&&dormant.IsAssembly&&dormant.IsStatic&&dormant.FieldType==typeof(bool),"Unexpected GLD flag.");
            Require(!(bool)dormant!.GetValue(null)!,"Optional server GLD mode is enabled; incompatible serializer cannot be suppressed.");
            var serializer=Native.M("GameState","Deserialize","Il2CppSystem.IO.BinaryReader","System.Int32");
            var existing=Harmony.GetPatchInfo(serializer);
            Require(existing is null||!existing.Owners.Any(),"Serializer already patched; installation is too late or conflicts with another mod.");
            Type attributePatch=typeof(Harmony).Assembly.GetType("HarmonyLib.AttributePatch",true)!;
            var create=attributePatch.GetMethod("Create",Flags,null,new[]{typeof(MethodInfo),typeof(bool)},null);
            Type result=typeof(IEnumerable<>).MakeGenericType(attributePatch);
            Require(create is not null&&create.IsStatic&&!create.IsGenericMethod&&create.ReturnType==result,"Unexpected Harmony attribute discovery contract.");
            var discoveryPatches=Harmony.GetPatchInfo(create!);
            Require(discoveryPatches is null||!discoveryPatches.Owners.Any(),"Attribute discovery already patched.");
            var prefix=typeof(NativeCleanBetaCompatibility).GetMethod(nameof(BeforeCreate),Flags)!.MakeGenericMethod(attributePatch);
            Selected=selected;Dormant=dormant;
            try {harmony.Patch(create!,prefix:new HarmonyMethod(prefix));Installed=true;return true;}
            catch {Selected=null;Dormant=null;throw;}
        }
        static bool BeforeCreate<T>(MethodInfo __0,ref IEnumerable<T> __result)
        {
            if(Selected is null||!Selected.Equals(__0))return true;
            Require(Dormant is not null&&!(bool)Dormant.GetValue(null)!,"Optional server GLD mode was enabled before registration.");
            __result=Array.Empty<T>();Filtered++;Entry.Note("clean-beta-filter", "Skipped exact incompatible reader callback; count="+Filtered);return false;
        }
    }
}
