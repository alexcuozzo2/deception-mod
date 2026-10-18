using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
namespace HarmonyLib {public sealed class Harmony {public readonly List<string> Patched=new();}}
namespace Newtonsoft.Json.Linq
{
    public sealed class JObject
    {
        internal readonly JsonNode Node;
        internal static int FailRemoveAt,RemoveCount;
        public JObject(JsonNode value){Node=value;}
        public object? this[string key]
        {
            get=>Node[key] is JsonNode value?new JObject(value):null;
            set=>Node[key]=value is JObject wrapper?wrapper.Node:null;
        }
        public bool Remove(string key){bool removed=Node.AsObject().Remove(key);if(++RemoveCount==FailRemoveAt)throw new Exception("Synthetic remove failure after deletion");return removed;}
        public static JObject Parse(string json)=>new(JsonNode.Parse(json)!);
        public JObject DeepClone()=>Parse(Node.ToJsonString());
        public override string ToString()=>Node.ToJsonString();
    }
}
namespace Deception.PolyModAdapter
{
    using Newtonsoft.Json.Linq;
    internal static class NativeJson {internal static string Read(object value)=>value.ToString()!;}
    internal static class Entry {internal static readonly List<string> Notes=new();internal static void Note(string kind,string message)=>Notes.Add(kind+": "+message);}
    internal static class Native
    {
        internal static Type T(string name)=>name=="GameManager"?typeof(FixtureGameManager):name=="Newtonsoft.Json.Linq.JObject"?typeof(JObject):Type.GetType(name)??throw new Exception(name);
        internal static object As(object value,string type)=>T(type).IsInstanceOfType(value)?value:throw new Exception("Wrong explicit fixture type");
        internal static bool Same(object? a,object? b)=>a is not null&&b is not null&&(ReferenceEquals(a,b)||(a is JObject ja&&b is JObject jb&&ReferenceEquals(ja.Node,jb.Node)));
        internal static int Int(object? value)=>Convert.ToInt32(value);
        internal static object EnumValue(string type,string name)=>FixtureLoader.Mapping[(type,name)];
        internal static MethodInfo M(string type,string name,params string[] parameters)=>M(T(type),name,parameters);
        internal static MethodInfo M(Type type,string name,params string[] parameters)=>type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance).Single(m=>m.Name==name&&m.GetParameters().Select(p=>p.ParameterType.FullName).SequenceEqual(parameters));
        internal static object? Invoke(MethodInfo method,object? target,params object?[] args)
        {
            if(method.DeclaringType==typeof(FixtureLoader)&&method.Name==nameof(FixtureLoader.CreateMappings)){FixtureLoader.Run((JObject)args[0]!);return null;}
            return method.Invoke(target,args);
        }
        internal static void RequireReturn(MethodInfo m,Type type){if(m.ReturnType!=type)throw new Exception("Return mismatch");}
        internal static void Hook(HarmonyLib.Harmony h,MethodInfo method,Type patch,string? prefix=null,string? postfix=null,string? finalizer=null)=>h.Patched.Add(method.Name);
    }
    internal static class FixtureGameManager {public static void CreateSinglePlayerGame(){} public static void ResumeSingleplayerGame(){}}
    internal static class FixtureLoader
    {
        internal static int Counter=1000,Invocations;
        internal static readonly Dictionary<(string Type,string Name),int> Mapping=new();
        internal static readonly Dictionary<string,int> HandlerCount=new();
        internal static readonly Dictionary<int,string> PrefabNames=new();
        internal static readonly Dictionary<string,int> FirstHandlerCount=new();
        internal static readonly List<string[]> Batches=new();
        internal static string? FailAfter;
        internal static string TypeFor(string section)=>section switch{"techData"=>"Polytopia.Data.TechData+Type","unitData"=>"Polytopia.Data.UnitData+Type","improvementData"=>"Polytopia.Data.ImprovementData+Type",_=>throw new Exception(section)};
        public static void CreateMappings(JObject json)
        {
            // Pinned PolyMod IL: JSON traversal order; idx:-1 allocations in
            // pass one; handlers(false) on every entry in pass two. Unit prefab
            // registration is a Dictionary.Add in the false pass, even when
            // explicit indices caused pass one to skip a native entry.
            Invocations++;var allocated=new List<string>();
            var entries=json.Node.AsObject().Where(p=>new[]{"techData","unitData","improvementData"}.Contains(p.Key))
                .SelectMany(section=>section.Value!.AsObject().Select(pair=>(Section:section.Key,Name:pair.Key,Data:pair.Value!))).ToArray();
            foreach(var entry in entries)
            {
                int index=entry.Data["idx"]!.GetValue<int>();if(index!=-1)continue;
                index=Counter++;entry.Data["idx"]=index;allocated.Add(entry.Name);
                // The real cache tolerates its own repeated identical AddMapping.
                for(int repeat=0;repeat<2;repeat++)
                    if(Mapping.TryGetValue((TypeFor(entry.Section),entry.Name),out int prior)){if(prior!=index)throw new Exception("Conflicting repeat mapping");}
                    else Mapping.Add((TypeFor(entry.Section),entry.Name),index);
                if(entry.Section=="unitData")FirstHandlerCount[entry.Name]=FirstHandlerCount.GetValueOrDefault(entry.Name)+1;
            }
            foreach(var entry in entries)
            {
                if(entry.Section=="unitData")
                {
                    HandlerCount[entry.Name]=HandlerCount.GetValueOrDefault(entry.Name)+1;
                    if(entry.Data["prefab"] is not null)PrefabNames.Add(entry.Data["idx"]!.GetValue<int>(),entry.Data["prefab"]!.GetValue<string>());
                }
                if(FailAfter==entry.Name)throw new Exception("Synthetic native mapping failure after "+entry.Name);
            }
            Batches.Add(allocated.ToArray());
        }
        internal static void Run(JObject json)
        {
            object? state=null;try{if(NativeRegistryOrder.Before(json,ref state))CreateMappings(json);NativeRegistryOrder.After(state);NativeRegistryOrder.Final(null,state);}
            catch(Exception e){NativeRegistryOrder.Final(e,state);throw;}
        }
        internal static void Reset(){Counter=1000;Invocations=0;Mapping.Clear();HandlerCount.Clear();FirstHandlerCount.Clear();PrefabNames.Clear();Batches.Clear();FailAfter=null;JObject.FailRemoveAt=JObject.RemoveCount=0;}
    }
}
