using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using B=Deception.PolyModAdapter.NativeExecutionBridge;

namespace Deception.ExecutionCallbackTests
{
    // Isolated managed POD/callback tests, never an installer or game entry point.
    public static unsafe class Harness
    {
        const BindingFlags Hidden=BindingFlags.Static|BindingFlags.NonPublic;
        static readonly List<object> Results=new();
        static int Assertions,Failures;
        static string Current="";
        static readonly IntPtr PreparePointer=RootPointer("PrepareRoot"),FinishPointer=RootPointer("FinishRoot"),CleanupPointer=RootPointer("CleanupRoot");
        static readonly delegate* unmanaged[Cdecl]<IntPtr,IntPtr,int> Prepare=(delegate* unmanaged[Cdecl]<IntPtr,IntPtr,int>)PreparePointer;
        static readonly delegate* unmanaged[Cdecl]<IntPtr,IntPtr,int,int> Finish=(delegate* unmanaged[Cdecl]<IntPtr,IntPtr,int,int>)FinishPointer;
        static readonly delegate* unmanaged[Cdecl]<IntPtr,IntPtr,int,int> Cleanup=(delegate* unmanaged[Cdecl]<IntPtr,IntPtr,int,int>)CleanupPointer;
        static IntPtr RootPointer(string name)=>Marshal.GetFunctionPointerForDelegate((Delegate)typeof(B).GetField(name,Hidden)!.GetValue(null)!);
        static object? Field(string name)=>typeof(B).GetField(name,Hidden)!.GetValue(null);
        static void SetField(string name,object? value)=>typeof(B).GetField(name,Hidden)!.SetValue(null,value);
        static void Check(bool value,string reason){Interlocked.Increment(ref Assertions);if(!value)throw new InvalidOperationException(Current+": "+reason);}
        static void Equal<T>(T actual,T expected,string reason)=>Check(EqualityComparer<T>.Default.Equals(actual,expected),reason+" (actual="+actual+", expected="+expected+")");
        static void Test(string id,Action body)
        {
            Current=id;int before=Assertions;
            try{Equal(B.ActiveOperations,0,"test begins balanced");body();Equal(B.ActiveOperations,0,"test ends balanced");Results.Add(new{id,status="PASS",assertions=Assertions-before});Console.WriteLine("PASS "+id);}
            catch(Exception e){Failures++;Results.Add(new{id,status="FAIL",assertions=Assertions-before,error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e);}
        }
        sealed class Buffers:IDisposable
        {
            readonly IntPtr callStorage,planStorage;
            internal readonly IntPtr CallPointer,PlanPointer;
            internal Buffers(uint site=0,IntPtr? method=null)
            {
                callStorage=Marshal.AllocHGlobal(64);planStorage=Marshal.AllocHGlobal(40);
                for(int i=0;i<64;i++)Marshal.WriteByte(callStorage,i,0x5a);
                for(int i=0;i<40;i++)Marshal.WriteByte(planStorage,i,0x5a);
                CallPointer=callStorage+16;PlanPointer=planStorage+16;
                Call=new B.Call{Size=32,Site=site,Instance=(IntPtr)0x12340,State=(IntPtr)0x56780,MethodInfo=method??(IntPtr)0x9abc0};
                Plan=new B.Plan();
            }
            internal B.Call Call{get=>Marshal.PtrToStructure<B.Call>(CallPointer);set=>Marshal.StructureToPtr(value,CallPointer,false);}
            internal B.Plan Plan{get=>Marshal.PtrToStructure<B.Plan>(PlanPointer);set=>Marshal.StructureToPtr(value,PlanPointer,false);}
            internal void Guards()
            {
                Check(Enumerable.Range(0,16).All(i=>Marshal.ReadByte(callStorage,i)==0x5a&&Marshal.ReadByte(callStorage,48+i)==0x5a),"Call boundary canaries intact");
                Check(Enumerable.Range(0,16).All(i=>Marshal.ReadByte(planStorage,i)==0x5a&&Marshal.ReadByte(planStorage,24+i)==0x5a),"Plan boundary canaries intact");
            }
            internal int P(){int result=Prepare(CallPointer,PlanPointer);Guards();return result;}
            internal int F(int original){int result=Finish(CallPointer,PlanPointer,original);Guards();return result;}
            internal int C(B.Outcome outcome=B.Outcome.Complete){int result=Cleanup(CallPointer,PlanPointer,(int)outcome);Guards();return result;}
            public void Dispose()
            {
                // Emergency cleanup uses the actual owner-thread callback, never clears the registry.
                if(((IDictionary)Field("Operations")!).Contains(Plan.Cookie))Cleanup(CallPointer,PlanPointer,(int)B.Outcome.CallbackException);
                Marshal.FreeHGlobal(planStorage);Marshal.FreeHGlobal(callStorage);
            }
        }
        delegate B.Decision PrepareBody(B.Call call,out B.IOperation? operation);
        sealed class Router:B.IRouter
        {
            internal readonly List<B.Call> Calls=new();
            internal readonly List<(string stage,string reason)> Notifications=new();
            internal PrepareBody? Body;
            internal bool NotificationThrows;
            public B.Decision Prepare(B.Call call,out B.IOperation? operation)
            {lock(Calls)Calls.Add(call);operation=null;return Body is null?B.Decision.Pass:Body(call,out operation);}
            public void Failed(B.Call? call,string stage,string reason)
            {lock(Notifications)Notifications.Add((stage,reason));if(NotificationThrows)throw new ApplicationException("notification failure");}
        }
        sealed class Op:B.IOperation
        {
            internal int Finishes,Cleans;
            internal bool? Original;
            internal B.Outcome? Outcome;
            internal Action? DuringFinish,DuringCleanup;
            internal bool FinishThrows,CleanupThrows;
            public void Finish(bool original){Finishes++;Original=original;DuringFinish?.Invoke();if(FinishThrows)throw new ApplicationException("finish failure");}
            public void Cleanup(B.Outcome outcome){Cleans++;Outcome=outcome;DuringCleanup?.Invoke();if(CleanupThrows)throw new ApplicationException("cleanup failure");}
        }
        static Router Use(Op? operation=null,B.Decision decision=B.Decision.Pass)
        {
            var router=new Router{Body=(B.Call call,out B.IOperation? found)=>{found=operation;return decision;}};
            SetField("Router",router);return router;
        }
        static bool Same(B.Call a,B.Call b)=>a.Size==b.Size&&a.Site==b.Site&&a.Instance==b.Instance&&a.State==b.State&&a.MethodInfo==b.MethodInfo;
        static void Simple(uint site,B.Decision decision)
        {
            var op=new Op();var router=Use(op,decision);using var b=new Buffers(site,site==3?IntPtr.Zero:null);var original=b.Call;
            ulong allocated=B.AllocatedOperations;
            Equal(b.P(),(int)decision,"decision returned through Cdecl thunk");Equal(B.AllocatedOperations,allocated+1,"one cookie allocated");
            Equal(b.Plan.Cookie,allocated+1,"native Plan carries owned cookie");Equal(B.ActiveOperations,1,"one outstanding operation");
            Check(Same(router.Calls.Single(),original),"exact Call and optional null MethodInfo preserved");
            Equal(b.F(decision==B.Decision.Pass?1:0),0,"finish succeeds");Equal(op.Original,decision==B.Decision.Pass,"handler receives exact original flag");
            Equal(b.C(),0,"cleanup succeeds");Equal(op.Finishes,1,"finish once");Equal(op.Cleans,1,"cleanup once");Equal(op.Outcome,B.Outcome.Complete,"completion outcome");
            Check(Same(b.Call,original),"callback leaves Call bytes unchanged");
            Equal(b.C(),1,"stale cookie rejected");Equal(op.Cleans,1,"stale cleanup cannot repeat handler");
        }
        static void Rejections()
        {
            Test("callback-null-and-invalid-pre-cookie",()=>
            {
                var router=Use();using var b=new Buffers();ulong allocated=B.AllocatedOperations;
                Equal(Prepare(IntPtr.Zero,b.PlanPointer),2,"null Call rejects");Equal(b.Plan.Cookie,0UL,"pre-cookie rejection publishes zero");
                Equal(Prepare(b.CallPointer,IntPtr.Zero),2,"null Plan is contained");Equal(Finish(IntPtr.Zero,b.PlanPointer,1),1,"null finish rejects");
                Equal(Cleanup(b.CallPointer,IntPtr.Zero,0),1,"null cleanup rejects");
                foreach(int variant in new[]{0,1,2,3})
                {var call=b.Call;if(variant==0)call.Size=31;else if(variant==1)call.Site=4;else if(variant==2)call.Instance=IntPtr.Zero;else call.State=IntPtr.Zero;b.Call=call;Equal(b.P(),2,"malformed Call rejects");b.Call=new B.Call{Size=32,Site=0,Instance=(IntPtr)1,State=(IntPtr)2};}
                Equal(B.AllocatedOperations,allocated,"invalid POD never allocated a cookie");Equal(router.Calls.Count,0,"invalid Call never reaches router");
                Equal(b.C(),0,"valid zero-cookie cleanup is empty");
                SetField("Router",null);Equal(b.P(),2,"missing router rejects before cookie");SetField("Router",router);
                Equal(B.AllocatedOperations,allocated,"missing router never allocated a cookie");
            });
            foreach(var decision in new[]{B.Decision.Reject,(B.Decision)9})Test("callback-prepare-decision-"+(int)decision,()=>
            {
                var op=new Op();Use(op,decision);using var b=new Buffers();Equal(b.P(),2,"rejection reported");Check(b.Plan.Cookie>0,"published handler remains tracked for native cleanup");
                Equal(b.F(0),1,"rejected decision may not finish");Equal(b.C(B.Outcome.PrepareRejected),0,"rejection cleanup succeeds");Equal(op.Cleans,1,"handler cleaned once");Equal(op.Finishes,0,"no handler finish");
            });
            foreach(bool assigned in new[]{false,true})Test("callback-prepare-throw-"+(assigned?"after":"before")+"-handler",()=>
            {
                var op=new Op();var router=Use();router.NotificationThrows=true;
                router.Body=(B.Call call,out B.IOperation? found)=>{found=assigned?op:null;throw new ApplicationException("prepare failure");};
                using var b=new Buffers();Equal(b.P(),2,"managed prepare throw contained");Check(b.Plan.Cookie>0,"allocated cookie survives throw");
                Equal(b.C(B.Outcome.PrepareRejected),0,"cleanup after throw succeeds");Equal(op.Cleans,assigned?1:0,"only published handler cleaned");
                Check(router.Notifications.Any(n=>n.stage=="prepare")&&router.Notifications.Any(n=>n.stage=="native-cleanup-outcome"),"rejection and native outcome notifications recorded despite reporter throws");
            });
            Test("callback-finish-throw-and-repeat",()=>
            {
                var op=new Op{FinishThrows=true};var router=Use(op);router.NotificationThrows=true;using var b=new Buffers();Equal(b.P(),0,"prepare succeeds");
                Equal(b.F(1),1,"finish throw contained");Equal(b.F(1),1,"second finish rejects");Equal(op.Finishes,1,"throwing finish never repeated");
                Equal(b.C(B.Outcome.FinishRejected),0,"cleanup after finish failure");Equal(op.Cleans,1,"cleanup exactly once");Equal(op.Outcome,B.Outcome.FinishRejected,"failure outcome delivered");
            });
            Test("callback-cleanup-throw-retirement",()=>
            {
                var op=new Op{CleanupThrows=true};Use(op);using var b=new Buffers();Equal(b.P(),0,"prepare succeeds");Equal(b.F(1),0,"finish succeeds");
                Equal(b.C(),1,"cleanup throw contained");Equal(B.ActiveOperations,0,"cookie retired before throwing cleanup");Equal(b.C(),1,"repeat cleanup rejected");Equal(op.Cleans,1,"throwing cleanup once");
            });
            foreach(B.Outcome outcome in Enum.GetValues<B.Outcome>())Test("callback-native-outcome-"+outcome,()=>
            {
                var op=new Op();var router=Use(op);using var b=new Buffers();Equal(b.P(),0,"prepare succeeds");
                // Models C++ calling Cleanup after an original-native exception; no native throw is performed here.
                Equal(b.C(outcome),0,"known native outcome accepted");Equal(op.Cleans,1,"cleanup once");Equal(op.Outcome,outcome,"outcome preserved");
                Equal(router.Notifications.Count,outcome==B.Outcome.Complete?0:1,"non-success native outcome notified");
            });
        }
        static void IdentityAndOrdering()
        {
            Test("callback-identity-and-cookie-mismatch",()=>
            {
                var op=new Op();Use(op);using var b=new Buffers();Equal(b.P(),0,"prepare succeeds");var original=b.Call;var plan=b.Plan;
                foreach(int variant in Enumerable.Range(0,7))
                {
                    var call=original;var wrong=plan;
                    switch(variant){case 0:call.Size=0;break;case 1:call.Site=1;break;case 2:call.Instance+=(int)8;break;case 3:call.State+=(int)8;break;case 4:call.MethodInfo+=(int)8;break;case 5:wrong.Cookie=0;break;case 6:wrong.Cookie+=1000;break;}
                    b.Call=call;b.Plan=wrong;Equal(b.F(1),1,"mismatched finish rejects");
                    if(variant!=5)Equal(b.C(),1,"mismatched cleanup rejects");
                    Equal(B.ActiveOperations,1,"mismatch cannot retire real owner");b.Call=original;b.Plan=plan;
                }
                Equal(op.Finishes,0,"mismatch never calls finish");Equal(op.Cleans,0,"mismatch never calls cleanup");
                Equal(b.F(1),0,"exact owner can still finish");Equal(b.C(),0,"exact owner can still retire");
            });
            foreach(var decision in new[]{B.Decision.Pass,B.Decision.Handled})Test("callback-original-ran-"+decision,()=>
            {
                var op=new Op();Use(op,decision);using var b=new Buffers();Equal(b.P(),(int)decision,"prepare decision");
                foreach(int wrong in new[]{-1,2,decision==B.Decision.Pass?0:1})Equal(b.F(wrong),1,"invalid originalRan rejected");
                Equal(op.Finishes,0,"invalid flag never calls handler");Equal(b.F(decision==B.Decision.Pass?1:0),0,"correct originalRan succeeds");
                Equal(b.F(decision==B.Decision.Pass?1:0),1,"finish exactly once");Equal(b.C(),0,"cleanup succeeds");
            });
            Test("callback-invalid-cleanup-outcome",()=>
            {
                var op=new Op();Use(op);using var b=new Buffers();Equal(b.P(),0,"prepare succeeds");
                foreach(int invalid in new[]{-1,6,int.MaxValue})Equal(Cleanup(b.CallPointer,b.PlanPointer,invalid),1,"unknown outcome rejects");
                Equal(op.Cleans,0,"invalid outcome cannot run cleanup");Equal(B.ActiveOperations,1,"invalid outcome retains owner");Equal(b.C(),0,"valid cleanup retires");
            });
            Test("callback-thread-ownership",()=>
            {
                var op=new Op();Use(op);using var b=new Buffers();Equal(b.P(),0,"prepare on owner");int finish=-1,cleanup=-1;Exception? error=null;
                var worker=new Thread(()=>{try{finish=b.F(1);cleanup=b.C();}catch(Exception e){error=e;}});worker.Start();Check(worker.Join(5000),"worker finished");
                Check(error is null,"worker callback exceptions contained");Equal(finish,1,"foreign thread finish rejects");Equal(cleanup,1,"foreign thread cleanup rejects");
                Equal(op.Finishes,0,"foreign thread did not finish handler");Equal(op.Cleans,0,"foreign thread did not clean handler");Equal(B.ActiveOperations,1,"owner retained after foreign-thread callback");
                Equal(b.F(1),0,"owner finish still succeeds");Equal(b.C(),0,"owner cleanup still succeeds");
            });
        }
        static void Nested()
        {
            foreach(uint nestedSite in new uint[]{0,2})Test("callback-nested-prepare-site-"+nestedSite,()=>
            {
                var outer=new Op();var inner=new Op();int depth=0;ulong nestedCookie=0;var router=Use();
                router.Body=(B.Call call,out B.IOperation? found)=>
                {
                    found=depth==0?outer:inner;if(depth++==0)
                    {Equal(B.ActiveOperations,1,"outer cookie owned before router entry");using var b=new Buffers(nestedSite);Equal(b.P(),0,"nested prepare succeeds");nestedCookie=b.Plan.Cookie;Equal(B.ActiveOperations,2,"nested operation independent");Equal(b.F(1),0,"nested finish succeeds");Equal(b.C(),0,"nested cleanup succeeds");Equal(B.ActiveOperations,1,"outer survives nested cleanup");}
                    depth--;return B.Decision.Pass;
                };
                using var parent=new Buffers();Equal(parent.P(),0,"outer prepare succeeds");Check(nestedCookie>parent.Plan.Cookie,"nested cookie differs and advances");
                Equal(parent.F(1),0,"outer finish succeeds");Equal(parent.C(),0,"outer cleanup succeeds");Equal(outer.Cleans,1,"outer cleaned once");Equal(inner.Cleans,1,"inner cleaned once");
            });
            foreach(bool inCleanup in new[]{false,true})Test("callback-nested-"+(inCleanup?"cleanup":"finish"),()=>
            {
                var outer=new Op();var inner=new Op();bool outerEntered=false;var router=Use();router.Body=(B.Call call,out B.IOperation? found)=>{found=outerEntered?inner:outer;outerEntered=true;return B.Decision.Pass;};
                using var parent=new Buffers();Equal(parent.P(),0,"outer prepare succeeds");ulong outerCookie=parent.Plan.Cookie;
                Action nested=()=>
                {
                    Equal(B.ActiveOperations,inCleanup?0:1,"cleanup retires before calling handler");
                    if(inCleanup)Equal(parent.C(),1,"reentrant cleanup of same cookie rejects");else Equal(parent.F(1),1,"reentrant finish of same cookie rejects");
                    using var b=new Buffers(3);Equal(b.P(),0,"nested callback prepare succeeds");Check(b.Plan.Cookie>outerCookie,"nested callback has fresh cookie");Equal(b.F(1),0,"nested callback finish succeeds");Equal(b.C(),0,"nested callback cleanup succeeds");
                };
                if(inCleanup)outer.DuringCleanup=nested;else outer.DuringFinish=nested;
                Equal(parent.F(1),0,"outer finish succeeds");Equal(parent.C(),0,"outer cleanup succeeds");Equal(outer.Finishes,1,"outer finish once");Equal(outer.Cleans,1,"outer cleanup once");Equal(inner.Cleans,1,"nested cleanup once");
            });
            Test("callback-parallel-independent-owners",()=>
            {
                var router=Use();var operations=new List<Op>();router.Body=(B.Call call,out B.IOperation? found)=>{var op=new Op();lock(operations)operations.Add(op);found=op;return B.Decision.Handled;};
                using var barrier=new Barrier(4);var errors=new List<Exception>();var threads=Enumerable.Range(0,4).Select(index=>new Thread(()=>
                {
                    try{using var b=new Buffers((uint)index);Equal(b.P(),1,"independent thread prepare");Check(barrier.SignalAndWait(5000),"parallel cookies overlap");Equal(b.F(0),0,"same thread finish");Equal(b.C(),0,"same thread cleanup");}
                    catch(Exception e){lock(errors)errors.Add(e);}
                })).ToArray();
                foreach(var thread in threads)thread.Start();foreach(var thread in threads)Check(thread.Join(10000),"parallel thread completed");
                Check(errors.Count==0,"parallel independent operations succeeded: "+string.Join(";",errors.Select(e=>e.Message)));Equal(operations.Count,4,"four independent handlers");Check(operations.All(o=>o.Finishes==1&&o.Cleans==1),"each independent handler exactly once");
            });
        }
        static void Bounds()
        {
            Test("callback-operation-capacity",()=>
            {
                var operations=new List<Op>();var router=Use();router.Body=(B.Call call,out B.IOperation? found)=>{var op=new Op();operations.Add(op);found=op;return B.Decision.Pass;};
                var leases=new List<Buffers>();try
                {
                    for(int i=0;i<64;i++){var b=new Buffers((uint)(i%4));leases.Add(b);Equal(b.P(),0,"within-capacity prepare");}
                    Equal(B.ActiveOperations,64,"maximum owned operations present");ulong before=B.AllocatedOperations;using(var overflow=new Buffers()){Equal(overflow.P(),2,"65th prepare rejects");Equal(overflow.Plan.Cookie,0UL,"overflow publishes no cookie");Equal(overflow.C(B.Outcome.PrepareRejected),0,"zero-cookie overflow cleanup");}
                    Equal(B.AllocatedOperations,before,"overflow did not increment allocator");Equal(operations.Count,64,"overflow did not call router");
                    foreach(var b in leases)Equal(b.C(B.Outcome.OriginalException),0,"bounded operation cleanup");Check(operations.All(o=>o.Cleans==1&&o.Finishes==0),"all outstanding handlers cleaned once");
                }
                finally{foreach(var b in leases)b.Dispose();}
            });
            Test("callback-startup-rollback-refuses-admission",()=>
            {try{B.RollbackSetup();throw new Exception("rollback unexpectedly returned");}catch(InvalidOperationException e){Check(e.Message.Contains("after a call was admitted"),"rollback rejects callback admission");}Check(!B.Installed,"test never installed");});
        }
        static void CookieExhaustion()
        {
            Test("callback-cookie-exhaustion-test-only-counter",()=>
            {
                // Explicit test-only setup of a private monotonic counter; never a runtime API.
                SetField("NextCookie",ulong.MaxValue-1);var op=new Op();var router=Use(op);using var last=new Buffers();Equal(last.P(),0,"last nonzero cookie admitted");Equal(last.Plan.Cookie,ulong.MaxValue,"no signed narrowing");
                using var overflow=new Buffers();Equal(overflow.P(),2,"cookie wrap rejects");Equal(overflow.Plan.Cookie,0UL,"wrapped cookie is not published");Equal(router.Calls.Count,1,"exhausted allocator skips router");
                Equal(last.C(),0,"last cookie still retires");Equal(overflow.C(B.Outcome.PrepareRejected),0,"zero overflow cleanup");Equal(op.Cleans,1,"last handler cleanup once");
            });
        }
        static void Publication()
        {
#if PUBLICATION_FAULT
            Test("injected-plan-publication-before-write",()=>PublicationCase(false));
            Test("injected-plan-publication-after-write",()=>PublicationCase(true));
#endif
        }
#if PUBLICATION_FAULT
        static void PublicationCase(bool afterWrite)
        {
            var op=new Op();var router=Use(op);router.NotificationThrows=true;using var b=new Buffers();PublicationFault.AfterWrite=afterWrite;PublicationFault.Armed=true;
            Equal(b.P(),2,"injected managed publication error rejects");Check(!PublicationFault.Armed,"one-shot injection consumed");Equal(op.Cleans,1,"local handler retired after publication failure");Equal(op.Outcome,B.Outcome.PrepareRejected,"local cleanup rejection outcome");Equal(B.ActiveOperations,0,"no unreachable operation remains");
            Equal(b.C(B.Outcome.PrepareRejected),afterWrite?1:0,"later native cleanup cannot retire again");Equal(op.Cleans,1,"handler cleaned exactly once across publication failure");
            Check(router.Notifications.Any(n=>n.stage=="prepare-plan"),"publication failure notification contained");
        }
#endif
        public static int Run()
        {
            try
            {
                Test("callback-layout-and-entry-thunks",()=>
                {
                    typeof(B).GetMethod("Layout",Hidden)!.Invoke(null,null);Equal(Marshal.SizeOf<B.Call>(),32,"Call size");Equal(Marshal.SizeOf<B.Plan>(),8,"Plan size");
                    Check(PreparePointer!=IntPtr.Zero&&FinishPointer!=IntPtr.Zero&&CleanupPointer!=IntPtr.Zero,"rooted Cdecl thunk pointers available");
                    Equal(RuntimeInformation.ProcessArchitecture,Architecture.X64,"bundled Intel test process");Check(!B.Installed,"installer never invoked");
                });
                foreach(uint site in new uint[]{0,1,2,3})foreach(var decision in new[]{B.Decision.Pass,B.Decision.Handled})Test("callback-site-"+site+"-"+decision,()=>Simple(site,decision));
                Rejections();IdentityAndOrdering();Nested();Bounds();Publication();CookieExhaustion();
                Test("callback-final-isolation",()=>
                {
                    Equal(Deception.PolyModAdapter.Native.Calls,0,"installer/game access stubs never used");Equal((IntPtr)Field("Library")!,IntPtr.Zero,"execution dylib not loaded");Equal((IntPtr)Field("GameLibrary")!,IntPtr.Zero,"game library not loaded");
                    Equal(((ICollection)Field("Bindings")!).Count,0,"no native detour bindings created");Check(!(bool)Field("SetupAttempted")!,"Install never attempted");Check(!B.Installed,"no detours installed");Equal(B.ActiveOperations,0,"all operations retired");
                });
            }
            catch(Exception e){Failures++;Results.Add(new{id="harness-fatal",status="FAIL",error=e.ToString()});Console.WriteLine(e);}
            var result=new{schema=1,scope="MANAGED CALLBACK MOCKS; NO GAME OR DETOUR EXECUTION",instrumentedPublicationFailure=
#if PUBLICATION_FAULT
                true,
#else
                false,
#endif
                architecture=RuntimeInformation.ProcessArchitecture.ToString(),runtime=RuntimeInformation.FrameworkDescription,tests=Results.Count,assertions=Assertions,failed=Failures,activeOperations=B.ActiveOperations,
                counters=new{prepared=B.Prepared,finished=B.Finished,cleaned=B.Cleaned,rejected=B.Rejected,allocated=B.AllocatedOperations},results=Results};
            File.WriteAllText(Environment.GetEnvironmentVariable("DCP_EXEC_CALLBACK_RESULT")??throw new InvalidOperationException("Missing result path"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("Managed callback result: "+Results.Count+" tests, "+Assertions+" assertions, "+Failures+" failures");return Failures==0?0:1;
        }
    }
    internal static class PublicationFault
    {
        internal static bool Armed=false,AfterWrite=false;
        internal static void Write(B.Plan plan,IntPtr pointer)
        {
            if(!Armed){Marshal.StructureToPtr(plan,pointer,false);return;}
            Armed=false;if(AfterWrite)Marshal.StructureToPtr(plan,pointer,false);
            throw new InvalidOperationException("Explicit test-only plan publication fault");
        }
    }
}
