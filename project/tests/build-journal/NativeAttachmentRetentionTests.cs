// Actual attachment stores and native journal port; synthetic native lifetime.
// ExpiredKeys explicitly models native collection, separate from wrapper GC.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using Port=Deception.PolyModAdapter.NativeBuildAttachmentPortDraft;
namespace Deception.Tests
{
    public static class NativeAttachmentRetentionTests
    {
        static readonly List<object> Results=new();static int Failed,Assertions;
        static NativeExchangeIntent Intent()=>new(new Guid("5b36fb78-9815-4570-95f4-e40401314bde"),1,0,2,1,NativeExchangeKind.Regroup,false,NativePartnerReference.Existing,3,new(1,11,4,5),new(2,12,6,5),new byte[32]);
        static void Check(bool value,string why="Attachment lifetime invariant failed") {Assertions++;if(!value)throw new Exception(why);}
        static void Reject(Action action){Assertions++;try{action();}catch(InvalidDataException){return;}throw new Exception("Expected attachment rejection");}
        static void Test(string id,Action action)
        {
            try{action();Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}
            catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e);}
        }
        static IDictionary Table(bool exchange)=>(IDictionary)(exchange?typeof(NativeExchangeCommands):typeof(MimicSmuggleActions)).GetField("Payloads",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
        static BuildCommand Record(bool exchange,bool action=false)=>action?new BuildAction{Type=exchange?BuildType.Regroup:BuildType.Smuggle}:new BuildCommand{Type=exchange?BuildType.Regroup:BuildType.Smuggle};
        static object Publish(object value,bool exchange)=>exchange?NativeExchangeCommands.PublishPrivateJournal(value,Intent()):MimicSmuggleActions.PublishPrivateJournal(value,42);
        static void Prune(bool exchange){if(exchange)NativeExchangeCommands.PruneDead();else MimicSmuggleActions.PruneDead();}
        static void Forget(object value,bool exchange){if(exchange)NativeExchangeCommands.Forget(value);else MimicSmuggleActions.ForgetOffer(value);}
        static void ForgetExact(object value,object token,bool exchange){if(exchange)NativeExchangeCommands.ForgetPrivateJournal(value,token);else MimicSmuggleActions.ForgetPrivateJournal(value,token);}
        static void Collect(){for(int n=0;n<3;n++){GC.Collect();GC.WaitForPendingFinalizers();}}
        [MethodImpl(MethodImplOptions.NoInlining)]
        static (WeakReference<object> Weak,object Token,long Key) Unrooted(bool exchange)
        {var value=Record(exchange);return(new(value),Publish(value,exchange),Native.Pointer(value));}
        [MethodImpl(MethodImplOptions.NoInlining)]
        static bool IsAlive(WeakReference<object> weak)=>weak.TryGetTarget(out _);
        public static int Run()
        {
            foreach(bool exchange in new[]{true,false})
            {
                string name=exchange?"exchange":"smuggle";
                Test(name+"-journal-token-does-not-root-native-record",()=>
                {
                    var sample=Unrooted(exchange);Collect();Check(!IsAlive(sample.Weak),"Retained token roots discarded record");
                    Prune(exchange);Check(!Table(exchange).Contains(sample.Key));GC.KeepAlive(sample.Token);
                });
                Test(name+"-live-history-survives-sweep-and-wrapper-alias",()=>
                {
                    var record=Record(exchange,true);object token=Publish(record,exchange);var before=Port.Capture(record)!;
                    var alias=Record(exchange,true);alias.Pointer=record.Pointer;Collect();Prune(exchange);
                    var after=Port.Capture(alias)!;Check(ReferenceEquals(after.Token,token));Check(before.CopyBytes().SequenceEqual(after.CopyBytes()));
                    Forget(record,exchange);GC.KeepAlive(record);GC.KeepAlive(alias);
                });
                Test(name+"-dead-pointer-reuse-never-inherits-payload",()=>
                {
                    var old=Record(exchange);object prior=Publish(old,exchange);long key=Native.Pointer(old);
                    NativeLedgerOwnership.ExpiredKeys.Add(key);var replacement=Record(exchange);replacement.Pointer=new(key);
                    Check(Port.IsUnbound(replacement));NativeLedgerOwnership.ExpiredKeys.Remove(key);
                    object current=Publish(replacement,exchange);Check(!ReferenceEquals(prior,current));
                    Reject(()=>ForgetExact(replacement,prior,exchange));Check(ReferenceEquals(Port.Capture(replacement)!.Token,current));
                    ForgetExact(replacement,current,exchange);Check(Port.IsUnbound(replacement));GC.KeepAlive(old);
                });
                Test(name+"-live-records-still-save-and-copy-with-private-bytes",()=>
                {
                    var source=Record(exchange,true);Publish(source,exchange);byte[] bytes=Port.Capture(source)!.CopyBytes();
                    Collect();Prune(exchange);var copy=Record(exchange,true);int offset=0;
                    var decoded=Port.ReadPublish(copy,n=>{var result=bytes.Skip(offset).Take(n).ToArray();offset+=n;return result;});
                    Check(offset==bytes.Length&&decoded is not null);Check(Port.Capture(copy)!.CopyBytes().SequenceEqual(bytes));
                    Check(Port.Capture(source)!.CopyBytes().SequenceEqual(bytes));Forget(source,exchange);Forget(copy,exchange);
                });
                Test(name+"-periodic-sweep-scales-with-live-records",()=>
                {
                    var live=Enumerable.Range(0,8).Select(_=>Record(exchange)).ToArray();foreach(var value in live)Publish(value,exchange);
                    var expired=new List<long>();int peak=0;
                    // More cumulative copies than the hard registry capacity.
                    // Keep dead CLR wrappers alive so only native expiration,
                    // rather than accidental managed GC, drives reclamation.
                    var wrappers=new List<BuildCommand>();
                    for(int batch=0;batch<40;batch++)
                    {
                        for(int i=0;i<256;i++)
                        {
                            var value=Record(exchange);Publish(value,exchange);wrappers.Add(value);
                            long key=Native.Pointer(value);NativeLedgerOwnership.ExpiredKeys.Add(key);expired.Add(key);
                            peak=Math.Max(peak,Table(exchange).Count);
                        }
                        foreach(var value in live)Check(Port.Capture(value) is not null);
                    }
                    Prune(exchange);Check(Table(exchange).Count==live.Length,"Dead history grew registry");Check(peak<=live.Length+128,"Sweep was not amortized/bounded");
                    foreach(var value in live)Forget(value,exchange);foreach(long key in expired)NativeLedgerOwnership.ExpiredKeys.Remove(key);GC.KeepAlive(wrappers);
                });
                Test(name+"-weak-read-failure-does-not-authorize-eviction",()=>
                {
                    var value=Record(exchange);object token=Publish(value,exchange);NativeLedgerOwnership.ThrowTargetOnce=Native.Pointer(value);
                    bool rejected=false;try{Prune(exchange);}catch(InvalidOperationException){rejected=true;}
                    Check(rejected&&ReferenceEquals(Port.Capture(value)!.Token,token));Forget(value,exchange);
                });
            }
            string? output=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT");
            if(output is not null)File.WriteAllText(output,JsonSerializer.Serialize(new{scope="Actual attachment stores/port; synthetic native weak identity and GC, no game execution",failed=Failed,assertions=Assertions,results=Results},new JsonSerializerOptions{WriteIndented=true}));
            return Failed==0?0:1;
        }
    }
}
