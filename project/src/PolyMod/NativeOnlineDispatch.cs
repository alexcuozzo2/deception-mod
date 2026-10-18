using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Deception.PolyModAdapter
{
    // The existing Tesla component drains its ConcurrentQueue on Unity Update.
    // Create the native delegate on Entry.Load's game thread, then let workers
    // call only its audited thread-safe Enqueue. No scope crosses an await.
    internal static class NativeOnlineDispatch
    {
        internal const int Capacity=16,DefaultQueuedTimeoutMilliseconds=10000;
        static readonly object Gate=new();
        static readonly List<Work> Queue=new();
        static int MainThread;
        static MethodInfo? Enqueue;
        static object? NativePulse;
        static Action? ManagedPulse;
        static bool Scheduled;
        enum Stage {Queued,Running,Completed,Cancelled}
        sealed class Work
        {
            internal readonly MethodInfo Method;
            internal readonly object?[] Arguments;
            internal readonly ManualResetEventSlim Done=new(false);
            internal Stage Stage;
            internal object? Result;
            internal ExceptionDispatchInfo? Error;
            internal Work(MethodInfo method,object?[] arguments){Method=method;Arguments=arguments;}
        }
        internal static bool IsMainThread=>Volatile.Read(ref MainThread)!=0&&Environment.CurrentManagedThreadId==Volatile.Read(ref MainThread);
        internal static void Initialize()
        {
            lock(Gate)
            {
                if(MainThread!=0)
                {
                    if(!IsMainThread)throw new InvalidOperationException("Online dispatcher initialization crossed the game thread.");
                    return;
                }
                var enqueue=Native.M("TeslaDispatchComponent","Enqueue","Il2CppSystem.Action");
                var queue=Native.M("TeslaDispatchComponent","get__taskQueue");
                if(!enqueue.IsStatic||enqueue.ReturnType!=typeof(void)||!queue.IsStatic||
                    !queue.ReturnType.IsGenericType||queue.ReturnType.GetGenericTypeDefinition().FullName!="Il2CppSystem.Collections.Concurrent.ConcurrentQueue`1"||
                    queue.ReturnType.GetGenericArguments()[0]!=Native.T("Il2CppSystem.Action")||Native.Invoke(queue,null) is null)
                    throw new InvalidDataException("Native online dispatcher differs or is unavailable.");
                Action pulse=Drain;
                var native=Native.Callback(pulse)??throw new InvalidDataException("Native online dispatcher callback was not created.");
                Enqueue=enqueue;ManagedPulse=pulse;NativePulse=native;
                Volatile.Write(ref MainThread,Environment.CurrentManagedThreadId);
            }
        }
        static void Validate(MethodInfo method,object?[] arguments,int timeout)
        {
            ArgumentNullException.ThrowIfNull(method);ArgumentNullException.ThrowIfNull(arguments);
            if(!method.IsStatic||method.ContainsGenericParameters||method.ReturnType.IsByRef||method.ReturnType.IsPointer||
                typeof(Task).IsAssignableFrom(method.ReturnType)||method.ReturnType==typeof(ValueTask)||
                method.ReturnType.IsGenericType&&method.ReturnType.GetGenericTypeDefinition()==typeof(ValueTask<>)||
                method.IsDefined(typeof(AsyncStateMachineAttribute),false)||method.GetParameters().Length!=arguments.Length)
                throw new InvalidDataException("Online dispatch requires an exact synchronous static leaf and its arguments.");
            if(timeout<1||timeout>60000)throw new ArgumentOutOfRangeException(nameof(timeout));
        }
        static object? Call(MethodInfo method,object?[] arguments)
        {
            try{return method.Invoke(null,arguments);}
            catch(TargetInvocationException e)when(e.InnerException is not null)
            {ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
        }
        internal static object? Invoke(MethodInfo method,object?[] arguments,int queuedTimeoutMilliseconds=DefaultQueuedTimeoutMilliseconds)
        {
            Validate(method,arguments,queuedTimeoutMilliseconds);
            MethodInfo? enqueue=null;object? pulse=null;Work? work=null;bool schedule=false,main;
            lock(Gate)
            {
                if(MainThread==0||Enqueue is null||NativePulse is null)throw new InvalidOperationException("Online dispatcher is not initialized.");
                main=IsMainThread;
                if(!main)
                {
                    if(Queue.Count>=Capacity)throw new InvalidOperationException("Online dispatcher queue is full.");
                    work=new(method,arguments);Queue.Add(work);schedule=!Scheduled;Scheduled=true;enqueue=Enqueue;pulse=NativePulse;
                }
            }
            if(main)return Call(method,arguments);
            try
            {
                if(schedule)
                {
                    try{Native.Invoke(enqueue!,null,pulse);}
                    catch(Exception e){FailQueued(e);}
                }
                if(!work!.Done.Wait(queuedTimeoutMilliseconds))
                {
                    lock(Gate)
                    {
                        if(work.Stage==Stage.Queued)
                        {
                            Queue.Remove(work);work.Stage=Stage.Cancelled;
                            work.Error=ExceptionDispatchInfo.Capture(new TimeoutException("Online work expired before the game thread started it."));
                            work.Done.Set();
                        }
                    }
                    // A running native operation must finish before its caller
                    // returns, even when it takes longer than the queue timeout.
                    work.Done.Wait();
                }
                work.Error?.Throw();return work.Result;
            }
            catch(Exception error)
            {
                lock(Gate)
                {
                    if(work!.Stage==Stage.Queued)
                    {
                        Queue.Remove(work);work.Stage=Stage.Cancelled;
                        work.Error=ExceptionDispatchInfo.Capture(error);work.Done.Set();
                    }
                }
                // Thread interruption must not abandon queued/running work or
                // dispose an event that the game-thread callback still owns.
                while(!work!.Done.IsSet)
                {try{work.Done.Wait();}catch(ThreadInterruptedException){}}
                throw;
            }
            finally{work!.Done.Dispose();}
        }
        static void FailQueued(Exception error)
        {
            lock(Gate)
            {
                foreach(var work in Queue)
                {work.Error=ExceptionDispatchInfo.Capture(error);work.Stage=Stage.Completed;work.Done.Set();}
                Queue.Clear();Scheduled=false;
            }
        }
        static void Drain()
        {
            // Never throw through the native Action callback, including when a
            // broken dispatcher attempts to drain from the wrong thread.
            if(!IsMainThread){FailQueued(new InvalidOperationException("Online dispatcher ran outside the game thread."));return;}
            while(true)
            {
                Work work;
                lock(Gate)
                {
                    if(Queue.Count==0){Scheduled=false;return;}
                    work=Queue[0];Queue.RemoveAt(0);work.Stage=Stage.Running;
                }
                try{work.Result=Call(work.Method,work.Arguments);}
                catch(Exception e){work.Error=ExceptionDispatchInfo.Capture(e);}
                finally{lock(Gate){work.Stage=Stage.Completed;work.Done.Set();}}
            }
        }
    }
}
