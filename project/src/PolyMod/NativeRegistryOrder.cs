using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // The installed loader has one automatic counter shared by every enum.
    // Its unsafe-index option remains OFF. Let its unchanged mapping routine
    // process the historical entries once, then append the ordered technology and field-carrier batches.
    // Never edit its counter, mapping dictionaries, configuration or handlers.
    internal static class NativeRegistryOrder
    {
        static MethodInfo? CreateMappings;
        static object? Candidate;
        static bool Ready,Faulted,Mapped,BlockedNoted;
        [ThreadStatic] static bool TailCall;
        internal static bool Enabled=>true;
        sealed class Batch
        {
            internal readonly object Registry,Tail;
            internal readonly string Section;
            internal readonly PropertyInfo Indexer;
            internal readonly Dictionary<string,object> Original=new(StringComparer.Ordinal);
            internal Batch(object registry,string section,PropertyInfo indexer,object tail)
            {Registry=registry;Section=section;Indexer=indexer;Tail=tail;}
        }
        sealed class Work
        {
            internal readonly object Root;
            internal readonly List<Batch> Batches=new();
            internal Work(object root){Root=root;}
        }
        static PropertyInfo Index(object value)=>value.GetType().GetProperties(BindingFlags.Public|BindingFlags.Instance)
            .Single(p=>p.Name=="Item"&&p.GetIndexParameters().Length==1&&p.GetIndexParameters()[0].ParameterType==typeof(string));
        static object Parse(Type type,string json)=>Native.Invoke(Native.M(type,"Parse","System.String"),null,json)!;
        static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        static void Fault(Exception error)
        {Ready=false;Faulted=true;Entry.Note("registry-id-fault",error.GetType().Name+": "+error.Message);}
        internal static void RegistrationFailed(Exception error)=>Fault(error);
        internal static void Install(Harmony harmony,Type loader)
        {
            if(!Enabled)return;
            foreach(string name in new[]{"CreateSinglePlayerGame","ResumeSingleplayerGame"})
                Native.Hook(harmony,Native.M("GameManager",name),typeof(NativeRegistryOrder),nameof(AllowLocalGame));
            CreateMappings=Native.M(loader,"CreateMappings","Newtonsoft.Json.Linq.JObject");
            Native.RequireReturn(CreateMappings,typeof(void));
            Native.Hook(harmony,CreateMappings,typeof(NativeRegistryOrder),nameof(Before),nameof(After),nameof(Final));
        }
        internal static void Arm(object json)
        {
            if(!Enabled)return;
            try
            {
                Require(!Faulted,"A previous registry failure prevents further Deception registration.");
                if(Mapped)ValidateMappings(json);
                else RequireUnallocated(json);
                Candidate=json;Ready=false;
            }
            catch(Exception error){Fault(error);throw;}
        }
        static void RequireUnallocated(object json)
        {
            // Pin is private structural validation. Feeding its explicit IDs
            // to the loader would skip registration with unsafe indices OFF.
            string text=NativeJson.Read(json);_=NativeRegistryIds.Pin(text);
            var root=JsonNode.Parse(text)!;
            foreach(var entry in NativeRegistryIds.Entries.Where(x=>root[x.Section]?[x.Id] is not null))
                Require(root[entry.Section]![entry.Id]!["idx"]!.GetValue<int>()==-1,
                    "First registration contains a previously allocated identity: "+entry.Id);
        }
        public static bool Before(object __0,ref object? __state)
        {
            __state=null;if(!Enabled||TailCall||!Native.Same(__0,Candidate))return true;
            Work? work=null;
            try
            {
                Require(!Faulted,"Deception registry allocation is faulted.");
                // A second native parse may reuse the completed JSON. Never
                // rerun mapping handlers (unit prefab registration uses Add).
                if(Mapped){ValidateMappings(__0);return false;}
                RequireUnallocated(__0);string text=NativeJson.Read(__0);
                var root=JsonNode.Parse(text)!.AsObject();var top=Index(__0);
                var tailEntries=NativeRegistryIds.AllocationTail(text);
                // A newer release can append another run of an older section.
                // Group adjacent runs only: global GroupBy would move new units
                // before older improvement IDs and corrupt saved identities.
                var groups=new List<List<NativeRegistryId>>();
                foreach(var entry in tailEntries)
                {
                    if(groups.Count==0||groups[^1][0].Section!=entry.Section)groups.Add(new());
                    groups[^1].Add(entry);
                }
                work=new(__0);__state=work;
                foreach(var group in groups)
                {
                    var extras=new JsonObject();
                    object registry=Native.As(top.GetValue(__0,new object[]{group[0].Section})!,"Newtonsoft.Json.Linq.JObject");
                    var index=Index(registry);
                    foreach(var entry in group)extras[entry.Id]=JsonNode.Parse(root[entry.Section]![entry.Id]!.ToJsonString());
                    object tail=Parse(__0.GetType(),new JsonObject{[group[0].Section]=extras}.ToJsonString());
                    var batch=new Batch(registry,group[0].Section,index,tail);work.Batches.Add(batch);
                    foreach(var entry in group)
                    {
                        var original=index.GetValue(registry,new object[]{entry.Id});Require(original is not null,"Missing tail entry.");
                        batch.Original.Add(entry.Id,original!);
                    }
                }
                // Stage all newer IDs before the loader sees its historical
                // batch. Its shared counter and built-in handlers stay intact.
                foreach(var batch in work.Batches)foreach(string id in batch.Original.Keys)
                    Require((bool)Native.Invoke(Native.M(batch.Registry.GetType(),"Remove","System.String"),batch.Registry,id)!,"Could not stage a tail entry.");
                return true;
            }
            catch(Exception error)
            {
                if(work is not null)Restore(work);Fault(error);throw;
            }
        }
        public static void After(object? __state)
        {
            if(__state is not Work work)return;
            try
            {
                // The original batch and its handlers ran exactly once. Map
                // appended techs first, then field-only unit carriers, keeping
                // prior IDs stable and invoking new prefab handlers once.
                foreach(var batch in work.Batches)
                {
                    bool previous=TailCall;TailCall=true;
                    try{Native.Invoke(CreateMappings!,null,batch.Tail);}finally{TailCall=previous;}
                    object tailRegistry=Native.As(Index(batch.Tail).GetValue(batch.Tail,new object[]{batch.Section})!,"Newtonsoft.Json.Linq.JObject");
                    var tailIndex=Index(tailRegistry);
                    foreach(string id in batch.Original.Keys)
                    {
                        object entry=tailIndex.GetValue(tailRegistry,new object[]{id})!;
                        batch.Indexer.SetValue(batch.Registry,Native.Invoke(Native.M(entry.GetType(),"DeepClone"),entry),new object[]{id});
                    }
                }
                ValidateMappings(work.Root);Mapped=true;
            }
            catch(Exception error){Restore(work);Fault(error);throw;}
        }
        static void Restore(Work work)
        {
            try{foreach(var batch in work.Batches)foreach(var pair in batch.Original)batch.Indexer.SetValue(batch.Registry,pair.Value,new object[]{pair.Key});}
            catch(Exception error){Fault(error);}
        }
        public static Exception? Final(Exception? __exception,object? __state)
        {
            if(__exception is not null&&__state is Work work){Restore(work);Fault(__exception);}
            return __exception;
        }
        internal static void Complete(object json)
        {
            if(!Enabled||!Native.Same(json,Candidate))return;
            try
            {
                Require(!Faulted&&Mapped,"Registry mapping failed before native GLD parsing.");
                var values=ValidateMappings(json);
                Ready=true;Entry.Note("registry-ids-verified",JsonSerializer.Serialize(values));
            }
            catch(Exception error){Fault(error);}
        }
        static List<object> ValidateMappings(object json)
        {
            string text=NativeJson.Read(json);NativeRegistryIds.Validate(text);var root=JsonNode.Parse(text)!;
            var values=new List<object>();
            foreach(var entry in NativeRegistryIds.Entries.Where(x=>root[x.Section]?[x.Id] is not null))
            {
                int actual=Native.Int(Native.EnumValue(entry.EnumType,entry.Id));
                Require(actual==entry.Index,"Native saved-ID mapping changed: "+entry.Id);
                values.Add(new{id=entry.Id,type=entry.EnumType,index=actual});
            }
            return values;
        }
        public static bool AllowLocalGame()
        {
            if(Ready&&!Faulted)return true;
            if(!BlockedNoted){BlockedNoted=true;Entry.Note("registry-local-game-blocked","Saved unit IDs were not verified. Quit without loading or creating a local match.");}
            return false;
        }
        internal static void RequireCompatible()
        {
            if(Enabled)Require(Ready&&!Faulted,"Native state transport is blocked until the saved Deception registry IDs are verified.");
        }
    }
}
