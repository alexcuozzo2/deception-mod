using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Il2CppSystem
{
    public sealed class Action
    {
        readonly System.Action Value;
        public Action(System.Action value){Value=value;}
        public void Invoke()=>Value();
    }
}
namespace Il2CppSystem.Collections.Concurrent
{
    public sealed class ConcurrentQueue<T>
    {
        readonly System.Collections.Concurrent.ConcurrentQueue<T> Values=new();
        public void Enqueue(T value)=>Values.Enqueue(value);
        public bool TryDequeue(out T value)=>Values.TryDequeue(out value!);
        public int Count=>Values.Count;
    }
}
public static class TeslaDispatchComponent
{
    public static Il2CppSystem.Collections.Concurrent.ConcurrentQueue<Il2CppSystem.Action>? Queue=new();
    public static int Enqueues;
    public static Exception? ThrowBefore,ThrowAfter;
    public static readonly ConcurrentQueue<int> Threads=new();
    public static Il2CppSystem.Collections.Concurrent.ConcurrentQueue<Il2CppSystem.Action>? get__taskQueue()=>Queue;
    public static void Enqueue(Il2CppSystem.Action action)
    {
        Interlocked.Increment(ref Enqueues);Threads.Enqueue(Environment.CurrentManagedThreadId);
        if(ThrowBefore is Exception before)throw before;
        Queue!.Enqueue(action);
        if(ThrowAfter is Exception after)throw after;
    }
    // Matches the native Update's TryDequeue/Invoke loop, driven explicitly by
    // the fixture's recorded game thread rather than by Unity.
    public static void Drain(){while(Queue!.TryDequeue(out var action))action.Invoke();}
}
namespace Deception.PolyModAdapter
{
    internal static class Native
    {
        internal static int MainThread=Environment.CurrentManagedThreadId,Callbacks,Lookups;
        static void MainOnly(){if(Environment.CurrentManagedThreadId!=MainThread)throw new Exception("Fixture detected native setup/reflection off game thread.");}
        internal static Type T(string name)
        {MainOnly();return name switch{"TeslaDispatchComponent"=>typeof(TeslaDispatchComponent),"Il2CppSystem.Action"=>typeof(Il2CppSystem.Action),_=>throw new NotSupportedException(name)};}
        internal static MethodInfo M(string type,string name,params string[] args)
        {MainOnly();Lookups++;return T(type).GetMethods(BindingFlags.Public|BindingFlags.Static).Single(m=>m.Name==name&&m.GetParameters().Select(p=>p.ParameterType.FullName).SequenceEqual(args));}
        internal static object? Invoke(MethodInfo method,object? target,params object?[] arguments)
        {
            if(method.Name!="Enqueue")MainOnly();
            try{return method.Invoke(target,arguments);}
            catch(TargetInvocationException e)when(e.InnerException is not null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
        }
        internal static object Callback(System.Action callback){MainOnly();Callbacks++;return new Il2CppSystem.Action(callback);}
    }
}
