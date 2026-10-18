using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Deception.PolyModAdapter;

namespace Deception.Tests
{
    public static class OnlineDispatchTests
    {
        static int Cases,Assertions,Calls,Main;
        static readonly List<string> Names=new();
        static readonly List<int> Order=new();
        static readonly Exception Failure=new IOException("exact native leaf failure");
        static Future? Running;
        static bool ReturnedWhileRunning;
        sealed class Future
        {
            internal object? Result;
            internal Exception? Error;
            internal readonly ManualResetEventSlim Done=new(false);
            readonly Thread Worker;
            internal Future(Func<object?> run)
            {
                Worker=new Thread(()=>{try{Result=run();}catch(Exception e){Error=e;}finally{Done.Set();}}){IsBackground=true};Worker.Start();
            }
            internal void Join(){if(!Done.Wait(5000))throw new Exception("Fixture worker did not complete.");Worker.Join();}
            internal void Interrupt()=>Worker.Interrupt();
        }
        static void Check(bool value,string why){Assertions++;if(!value)throw new Exception(why);}
        static T Reject<T>(Action run)where T:Exception
        {try{run();}catch(T e){Assertions++;return e;}throw new Exception("Expected "+typeof(T).Name);}
        static void Test(string name,Action body){Cases++;Names.Add(name);try{body();}catch(Exception e){throw new Exception(name,e);}}
        static MethodInfo M(string name)=>typeof(OnlineDispatchTests).GetMethod(name,BindingFlags.Static|BindingFlags.Public)!;
        static int Queued()
        {
            var t=typeof(NativeOnlineDispatch);var gate=t.GetField("Gate",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            lock(gate)return ((ICollection)t.GetField("Queue",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!).Count;
        }
        static void Until(Func<bool> condition){if(!SpinWait.SpinUntil(condition,3000))throw new Exception("Fixture queue condition not reached.");}
        static void ReadyToDrain(int count)=>Until(()=>Queued()==count&&TeslaDispatchComponent.Queue!.Count>0);
        static Future Send(string method,object?[] args,int timeout=2000)=>new(()=>NativeOnlineDispatch.Invoke(M(method),args,timeout));
        public static int Leaf(int value){CheckThread();Calls++;Order.Add(value);return value+100;}
        static void CheckThread(){if(Environment.CurrentManagedThreadId!=Main)throw new Exception("Leaf ran on a worker.");}
        public static int RefOut(ref int value,out string result){CheckThread();value+=3;result="main-"+value;Calls++;return value;}
        public static void Throw(){CheckThread();throw Failure;}
        public static int Slow(){CheckThread();Thread.Sleep(150);ReturnedWhileRunning=Running!.Done.IsSet;return 9;}
        public static int Nested(){CheckThread();return (int)NativeOnlineDispatch.Invoke(M(nameof(Leaf)),new object?[]{7})!;}
        public static Task AsyncLeaf(){Calls++;return Task.CompletedTask;}
        public static ValueTask ValueTaskLeaf(){Calls++;return default;}
        public static async void AsyncVoid(){Calls++;await Task.Yield();}
        public static T Generic<T>(T value)=>value;
        public sealed class Instance {public int Leaf()=>1;}
        static void CasesRun()
        {
            Test("DISPATCH-001-uninitialized-never-runs-leaf",()=>{Reject<InvalidOperationException>(()=>NativeOnlineDispatch.Invoke(M(nameof(Leaf)),new object?[]{1}));Check(Calls==0,"Uninitialized leaf executed");});
            Test("DISPATCH-002-missing-native-queue-does-not-initialize",()=>{TeslaDispatchComponent.Queue=null;Reject<InvalidDataException>(NativeOnlineDispatch.Initialize);Check(!NativeOnlineDispatch.IsMainThread&&Native.Callbacks==0,"Failed setup leaked authority/callback");TeslaDispatchComponent.Queue=new();});
            Test("DISPATCH-003-main-thread-initialize-creates-one-rooted-pulse",()=>{NativeOnlineDispatch.Initialize();int lookups=Native.Lookups;NativeOnlineDispatch.Initialize();Check(NativeOnlineDispatch.IsMainThread&&Native.Callbacks==1&&Native.Lookups==lookups,"Initialization not idempotent or callback recreated");});
            Test("DISPATCH-004-worker-cannot-reinitialize-dispatcher",()=>{var worker=new Future(()=>{NativeOnlineDispatch.Initialize();return null;});worker.Join();Check(worker.Error is InvalidOperationException&&Native.Callbacks==1,"Worker changed initialized dispatcher");});
            Test("DISPATCH-005-main-leaf-direct-and-ref-out-values",()=>{int before=TeslaDispatchComponent.Enqueues;var args=new object?[]{5,null};Check((int)NativeOnlineDispatch.Invoke(M(nameof(RefOut)),args)! ==8&&(int)args[0]! ==8&&(string)args[1]! =="main-8","Direct ref/out results");Check(TeslaDispatchComponent.Enqueues==before,"Main thread unnecessarily queued");});
            Test("DISPATCH-006-worker-waits-for-main-drain",()=>{int before=Calls;var worker=Send(nameof(Leaf),new object?[]{3});ReadyToDrain(1);Check(!worker.Done.IsSet&&Calls==before,"Worker executed before native Update");TeslaDispatchComponent.Drain();worker.Join();Check(worker.Error is null&&(int)worker.Result! ==103&&Calls==before+1,"Worker result did not complete on main");});
            Test("DISPATCH-007-worker-preserves-exact-ref-out-array",()=>{var args=new object?[]{11,null};var worker=Send(nameof(RefOut),args);ReadyToDrain(1);TeslaDispatchComponent.Drain();worker.Join();Check(worker.Error is null&&(int)worker.Result! ==14&&(int)args[0]! ==14&&(string)args[1]! =="main-14","Queued ref/out results lost");});
            Test("DISPATCH-008-exceptions-propagate-original-not-reflection-wrapper",()=>{Check(ReferenceEquals(Reject<IOException>(()=>NativeOnlineDispatch.Invoke(M(nameof(Throw)),Array.Empty<object?>())),Failure),"Direct exception wrapped");var worker=Send(nameof(Throw),Array.Empty<object?>());ReadyToDrain(1);TeslaDispatchComponent.Drain();worker.Join();Check(ReferenceEquals(worker.Error,Failure),"Queued exception wrapped/lost");});
            Test("DISPATCH-009-expired-queued-work-never-executes-later",()=>{int before=Calls;var worker=Send(nameof(Leaf),new object?[]{4},40);worker.Join();Check(worker.Error is TimeoutException&&Queued()==0&&Calls==before,"Queued expiry did not cancel");TeslaDispatchComponent.Drain();Check(Calls==before,"Cancelled work mutated after timeout");});
            Test("DISPATCH-010-started-work-waits-past-queue-timeout",()=>{ReturnedWhileRunning=true;Running=Send(nameof(Slow),Array.Empty<object?>(),40);ReadyToDrain(1);TeslaDispatchComponent.Drain();Running.Join();Check(Running.Error is null&&(int)Running.Result! ==9&&!ReturnedWhileRunning,"Caller returned before started native work completed");Running=null;});
            Test("DISPATCH-011-capacity-bounded-and-pulses-coalesced",()=>{int before=TeslaDispatchComponent.Enqueues;Order.Clear();var workers=new List<Future>();for(int i=0;i<NativeOnlineDispatch.Capacity;i++){int value=i;workers.Add(Send(nameof(Leaf),new object?[]{value}));Until(()=>Queued()==value+1);}var overflow=Send(nameof(Leaf),new object?[]{999});overflow.Join();Check(overflow.Error is InvalidOperationException&&Queued()==16,"Capacity not enforced");Check(TeslaDispatchComponent.Enqueues==before+1,"Managed queue created redundant native pulses");ReadyToDrain(16);TeslaDispatchComponent.Drain();foreach(var worker in workers){worker.Join();Check(worker.Error is null,"Bounded request failed");}Check(Order.SequenceEqual(Enumerable.Range(0,16)),"FIFO order changed");});
            Test("DISPATCH-012-wrong-thread-native-drain-fails-without-callback-throw",()=>{int before=Calls;var worker=Send(nameof(Leaf),new object?[]{6});ReadyToDrain(1);var wrong=new Future(()=>{TeslaDispatchComponent.Drain();return null;});wrong.Join();worker.Join();Check(wrong.Error is null&&worker.Error is InvalidOperationException&&Calls==before&&Queued()==0,"Wrong-thread pulse escaped or executed leaf");});
            Test("DISPATCH-013-enqueue-failure-propagates-and-releases-work",()=>{TeslaDispatchComponent.ThrowBefore=Failure;var worker=Send(nameof(Leaf),new object?[]{8});worker.Join();TeslaDispatchComponent.ThrowBefore=null;Check(ReferenceEquals(worker.Error,Failure)&&Queued()==0,"Enqueue error lost/queued work leaked");var next=Send(nameof(Leaf),new object?[]{9});ReadyToDrain(1);TeslaDispatchComponent.Drain();next.Join();Check(next.Error is null&&(int)next.Result! ==109,"Queue did not recover after enqueue failure");});
            Test("DISPATCH-014-post-enqueue-failure-cancels-before-late-pulse",()=>{int before=Calls;TeslaDispatchComponent.ThrowAfter=Failure;var worker=Send(nameof(Leaf),new object?[]{10});worker.Join();TeslaDispatchComponent.ThrowAfter=null;TeslaDispatchComponent.Drain();Check(ReferenceEquals(worker.Error,Failure)&&Calls==before&&Queued()==0,"Late pulse ran failed queued work");});
            Test("DISPATCH-015-cancelled-pulse-can-service-new-valid-request",()=>{int before=TeslaDispatchComponent.Enqueues;var cancelled=Send(nameof(Leaf),new object?[]{11},40);cancelled.Join();var valid=Send(nameof(Leaf),new object?[]{12});ReadyToDrain(1);TeslaDispatchComponent.Drain();valid.Join();Check(cancelled.Error is TimeoutException&&valid.Error is null&&(int)valid.Result! ==112&&TeslaDispatchComponent.Enqueues==before+1,"Cancelled coalesced pulse lost new request");});
            Test("DISPATCH-016-main-thread-nesting-does-not-requeue",()=>{int before=TeslaDispatchComponent.Enqueues;var worker=Send(nameof(Nested),Array.Empty<object?>());ReadyToDrain(1);TeslaDispatchComponent.Drain();worker.Join();Check(worker.Error is null&&(int)worker.Result! ==107&&TeslaDispatchComponent.Enqueues==before+1,"Nested synchronous leaf queued/deadlocked");});
            Test("DISPATCH-017-async-instance-generic-and-arity-rejected-before-run",()=>{int before=Calls;foreach(var name in new[]{nameof(AsyncLeaf),nameof(ValueTaskLeaf),nameof(AsyncVoid),nameof(Generic)})Reject<InvalidDataException>(()=>NativeOnlineDispatch.Invoke(M(name),name==nameof(Generic)?new object?[]{1}:Array.Empty<object?>()));Reject<InvalidDataException>(()=>NativeOnlineDispatch.Invoke(typeof(Instance).GetMethod(nameof(Instance.Leaf))!,Array.Empty<object?>()));Reject<InvalidDataException>(()=>NativeOnlineDispatch.Invoke(M(nameof(Leaf)),Array.Empty<object?>()));Check(Calls==before,"Invalid leaf started");});
            Test("DISPATCH-018-timeout-input-and-null-arguments-rejected",()=>{Reject<ArgumentOutOfRangeException>(()=>NativeOnlineDispatch.Invoke(M(nameof(Leaf)),new object?[]{1},0));Reject<ArgumentOutOfRangeException>(()=>NativeOnlineDispatch.Invoke(M(nameof(Leaf)),new object?[]{1},60001));Reject<ArgumentNullException>(()=>NativeOnlineDispatch.Invoke(null!,Array.Empty<object?>()));Reject<ArgumentNullException>(()=>NativeOnlineDispatch.Invoke(M(nameof(Leaf)),null!));});
            Test("DISPATCH-019-failing-leaf-does-not-block-next-request",()=>{var bad=Send(nameof(Throw),Array.Empty<object?>());ReadyToDrain(1);var good=Send(nameof(Leaf),new object?[]{15});Until(()=>Queued()==2);TeslaDispatchComponent.Drain();bad.Join();good.Join();Check(ReferenceEquals(bad.Error,Failure)&&good.Error is null&&(int)good.Result! ==115,"Leaf failure escaped callback or stalled later work");});
            Test("DISPATCH-020-only-audited-enqueue-runs-native-on-workers",()=>{Check(Native.Callbacks==1&&TeslaDispatchComponent.Threads.Count>0&&TeslaDispatchComponent.Threads.All(id=>id!=Main),"Delegate setup or dispatch thread contract changed");Check(Queued()==0&&TeslaDispatchComponent.Queue!.Count==0,"Final pending work leaked");});
            Test("DISPATCH-021-interrupted-wait-cancels-before-late-native-drain",()=>{int before=Calls;var worker=Send(nameof(Leaf),new object?[]{16});ReadyToDrain(1);worker.Interrupt();worker.Join();TeslaDispatchComponent.Drain();Check(worker.Error is ThreadInterruptedException&&Calls==before&&Queued()==0,"Interrupted caller abandoned late work/event");});
        }
        public static int Run()
        {
            Main=Environment.CurrentManagedThreadId;Native.MainThread=Main;
            try{CasesRun();Console.WriteLine(JsonSerializer.Serialize(new{status="PASS",cases=Cases,assertions=Assertions,tests=Names,nativeGame="NOT RUN",scope="actual dispatcher with real managed worker threads and native-shaped Tesla queue"}));return 0;}
            catch(Exception e){Console.WriteLine(e);Console.WriteLine(JsonSerializer.Serialize(new{status="FAIL",cases=Cases,assertions=Assertions,tests=Names,error=e.ToString()}));return 1;}
        }
    }
}
