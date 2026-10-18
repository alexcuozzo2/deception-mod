using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;

namespace Deception.Tests
{
    // Actual preview adapter source with narrow boundary doubles. This does not
    // claim that Harmony, native serialization, or a remote server was executed.
    public static class OnlinePreviewTests
    {
        static int Cases,Assertions;
        static readonly List<string> Names=new();
        static readonly Guid Epoch=new("9ee29371-43b5-46d0-a924-c766a0a3a650");
        static void Check(bool good,string message){Assertions++;if(!good)throw new Exception(message);}
        static void Test(string name,Action run){Cases++;Names.Add(name);try{run();}catch(Exception e){throw new Exception(name,e);}}
        static void Reject(Action run){try{run();}catch(InvalidDataException){Assertions++;return;}throw new Exception("Expected rejection");}
        static FieldInfo Field(string name)=>typeof(NativeOnlinePreview).GetField(name,BindingFlags.Static|BindingFlags.NonPublic)!;
        static void Enable(bool enabled)=>Field("<Available>k__BackingField").SetValue(null,enabled);
        static byte[] Frame()=>NativeStateFrame.Encode(new byte[]{123,0,0,0,1,2,3},new byte[]{9,8,7},4,123);
        static byte[] Outer(byte[] frame,int version=123)
        {var bytes=new byte[frame.Length+4];BinaryPrimitives.WriteInt32LittleEndian(bytes,version);frame.CopyTo(bytes,4);return bytes;}
        static object? Invoke(MethodInfo method,object?[] args)
        {try{return method.Invoke(null,args);}catch(TargetInvocationException e)when(e.InnerException is not null){ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}}
        static object? RunPatched(MethodInfo method,object[] args)
        {
            bool isVoid=method.ReturnType==typeof(void);
            var prefix=typeof(NativeOnlinePreview).GetMethod(isVoid?nameof(NativeOnlinePreview.BeforeVoid):nameof(NativeOnlinePreview.BeforeManaged))!;
            if(!isVoid)prefix=prefix.MakeGenericMethod(method.ReturnType);
            object?[] inputs=isVoid?new object?[]{method,args,true,null}:new object?[]{method,args,method.ReturnType.IsValueType?Activator.CreateInstance(method.ReturnType):null,true,null};
            object? result=null;Exception? error=null;
            try{bool original=(bool)Invoke(prefix,inputs)!;result=original?Invoke(method,args):(isVoid?null:inputs[2]);}
            catch(Exception e){error=e;}
            error=NativeOnlinePreview.Finished(error,(IDisposable?)inputs[^1]);
            if(error is not null)ExceptionDispatchInfo.Capture(error).Throw();return result;
        }
        static MethodInfo Leaf(string name,int arguments)=>typeof(FakeLeaves).GetMethods().Single(m=>m.Name==name&&m.GetParameters().Length==arguments);
        static void ResetLeaf(){NativeOnlineSession.Reset();NativeOnlineDispatch.Reset();FakeLeaves.Bodies=0;FakeLeaves.ScopeAtBody=0;FakeLeaves.Error=null;}
        public static int Run()
        {
            try
            {
                Test("PREVIEW-23-clean-beta-requires-runtime-filter",()=>{
                    Check(!NativeOnlinePreview.AdmittedLoaderHash(NativeCleanBetaCompatibility.CleanSha256),"Clean beta denied without filter");
                    NativeCleanBetaCompatibility.Installed=true;
                    Check(NativeOnlinePreview.AdmittedLoaderHash(NativeCleanBetaCompatibility.CleanSha256),"Clean beta with filter");
                    Check(!NativeOnlinePreview.AdmittedLoaderHash(new string('0',64)),"Unknown denied with filter");
                    Check(!NativeOnlinePreview.AdmittedLoaderHash("db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d"),"Public denied with beta filter");
                    NativeCleanBetaCompatibility.Installed=false;
                    Check(!NativeOnlinePreview.AdmittedLoaderHash("db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d"),"Public stays local without filter");
                    Check(NativeOnlinePreview.AdmittedLoaderHash(NativeOnlinePreview.PreviewSha256),"Historical adapted beta");
                });
                Test("PREVIEW-01-unknown-loader-keeps-adapter-unavailable",()=>{Check(!NativeOnlinePreview.Install(new HarmonyLib.Harmony()),"No PolyMod assembly loaded");Check(!NativeOnlinePreview.Available,"Unknown loader admitted");Reject(()=>NativeOnlinePreview.CurrentContract());});
                Test("PREVIEW-02-raw-and-generic-outer-frame-exact-owned-boundary",()=>{var raw=Frame();var a=NativeOnlinePreview.InnerFrame(raw);var b=NativeOnlinePreview.InnerFrame(Outer(raw));Check(a.SequenceEqual(raw)&&b.SequenceEqual(raw),"Exact sections");a[0]=0;b[0]=0;Check(raw[0]!=0,"Copied bytes own their storage");});
                Test("PREVIEW-03-no-marker-scanning-or-version-guessing",()=>{var raw=Frame();Reject(()=>NativeOnlinePreview.InnerFrame(Outer(raw,124)));Reject(()=>NativeOnlinePreview.InnerFrame(Outer(Outer(raw))));Reject(()=>NativeOnlinePreview.InnerFrame(new byte[]{123,0,0,0,1,2,3,4}));});
                Test("PREVIEW-04-corrupt-truncated-trailing-frame-rejected",()=>{var raw=Frame();var corrupt=(byte[])raw.Clone();corrupt[^1]^=1;Reject(()=>NativeOnlinePreview.InnerFrame(corrupt));Reject(()=>NativeOnlinePreview.InnerFrame(Outer(raw[..^1])));Reject(()=>NativeOnlinePreview.InnerFrame(Outer(raw.Concat(new byte[]{0}).ToArray())));});
                Test("PREVIEW-05-marker-identification-does-not-admit-by-itself",()=>{Check(!NativeOnlinePreview.HasFrame(Array.Empty<byte>())&&!NativeOnlinePreview.HasFrame(new byte[]{123,0,0,0}),"Ordinary input ignored");Check(NativeOnlinePreview.HasFrame(Frame())&&NativeOnlinePreview.HasFrame(Outer(Frame(),124)),"Marker retained even for invalid version so it can reject");});
                Field("Checksum").SetValue(null,typeof(FakeCompatibility).GetField(nameof(FakeCompatibility.checksum)));
                Field("IsModded").SetValue(null,typeof(FakePreview).GetMethod(nameof(FakePreview.IsModdedGame)));
                Field("Shadow").SetValue(null,typeof(FakePreview).GetMethod(nameof(FakePreview.GetShadowState)));
                Enable(true);
                Test("PREVIEW-06-contract-canonical-exact-checksum-profile-protocol",()=>{FakeCompatibility.checksum=new string('a',64);string actual=NativeOnlinePreview.CurrentContract();string source="DeceptionOnline\nprotocol=1\npreview="+NativeOnlinePreview.PreviewSha256+"\nprofile="+NativeOnlinePreview.ProfileId+"\nmods="+new string('A',64)+"\n";Check(actual==Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant(),"Canonical contract");FakeCompatibility.checksum=new string('A',64);Check(NativeOnlinePreview.CurrentContract()==actual,"Hex case stable");FakeCompatibility.checksum=new string('b',64);Check(NativeOnlinePreview.CurrentContract()!=actual,"Changed mod set changes contract");});
                Test("PREVIEW-07-missing-and-malformed-checksum-rejected",()=>{foreach(var bad in new[]{"",new string('a',63),new string('g',64),new string('a',65)}){FakeCompatibility.checksum=bad;Reject(()=>NativeOnlinePreview.CurrentContract());}FakeCompatibility.checksum=new string('a',64);});
                Test("PREVIEW-08-creation-validates-native-versions-and-lobby-identity",()=>{NativeOnlineSession.Reset();NativeOnlinePreview.BeforeCreation(new FakeLobby{Id=Epoch},123,28,out var s);Check(NativeOnlineSession.Epoch==Epoch&&NativeOnlineSession.Purpose=="create"&&s is not null,"Creation callback");Check(NativeOnlinePreview.Finished(null,s) is null&&NativeOnlineSession.Disposed==1,"Creation cleanup");Reject(()=>NativeOnlinePreview.BeforeCreation(new FakeLobby{Id=Epoch},124,28,out _));Reject(()=>NativeOnlinePreview.BeforeCreation(new FakeLobby{Id=Epoch},123,29,out _));Reject(()=>NativeOnlinePreview.BeforeCreation(new FakeLobby{Id=Guid.Empty},123,28,out _));Check(NativeOnlineSession.Started==1,"Invalid creation never begins");});
                Test("PREVIEW-09-compute-exact-epoch-bytes-and-purpose",()=>{NativeOnlineSession.Reset();var bytes=Outer(Frame());NativeOnlinePreview.BeforeCompute(bytes,Epoch.ToString("D"),out var s);Check(NativeOnlineSession.Epoch==Epoch&&ReferenceEquals(NativeOnlineSession.Bytes,bytes)&&NativeOnlineSession.Purpose=="preview-update","Compute callback");NativeOnlinePreview.Finished(null,s);Reject(()=>NativeOnlinePreview.BeforeCompute(bytes,"bad-guid",out _));Check(NativeOnlineSession.Started==1,"Bad epoch does not begin");});
                Test("PREVIEW-10-counter-and-summary-ignore-ordinary-input",()=>{NativeOnlineSession.Reset();NativeOnlinePreview.BeforeCount(new byte[]{123,0,0,0,1},out var s);Check(s is null,"Ordinary count");NativeOnlinePreview.BeforeSummary(new byte[]{123,0,0,0,1},out s);Check(s is null&&NativeOnlineSession.Started==0,"Ordinary summary");NativeOnlinePreview.BeforeCount(Outer(Frame()),out s);Check(s is not null&&NativeOnlineSession.Purpose=="count","Framed count");NativeOnlinePreview.Finished(null,s);NativeOnlinePreview.BeforeSummary(Outer(Frame()),out s);Check(s is not null&&NativeOnlineSession.Started==2,"Framed summary");NativeOnlinePreview.Finished(null,s);});
                Test("PREVIEW-11-receive-only-existing-preview-route-and-shadow",()=>{NativeOnlineSession.Reset();FakePreview.Modded=false;FakePreview.Shadow=Frame();var data=new FakeCommand{GameId=Epoch};NativeOnlinePreview.BeforeReceive(data,out var s);Check(s is null&&NativeOnlineSession.Started==0,"Ordinary route");FakePreview.Modded=true;FakePreview.Shadow=null;NativeOnlinePreview.BeforeReceive(data,out s);Check(s is null&&NativeOnlineSession.Started==0,"No cached shadow");FakePreview.Shadow=Outer(Frame());NativeOnlinePreview.BeforeReceive(data,out s);Check(s is not null&&NativeOnlineSession.Epoch==Epoch&&NativeOnlineSession.Purpose=="preview-receive"&&ReferenceEquals(NativeOnlineSession.Bytes,FakePreview.Shadow),"Exact receive callback");NativeOnlinePreview.Finished(null,s);});
                Test("PREVIEW-12-finalizer-preserves-original-and-delegates-failure",()=>{NativeOnlineSession.Reset();NativeOnlinePreview.BeforeCount(Frame(),out var s);var error=new IOException("native decode failed");Check(ReferenceEquals(NativeOnlinePreview.Finished(error,s),error),"Original error retained");Check(ReferenceEquals(NativeOnlineSession.Error,error)&&NativeOnlineSession.Disposed==1,"Fault and disposal delegated");Check(ReferenceEquals(NativeOnlinePreview.Finished(error,null),error),"Failure before scope retained");});
                Test("PREVIEW-13-unavailable-adapter-does-not-begin-scopes",()=>{Enable(false);NativeOnlineSession.Reset();NativeOnlinePreview.BeforeCreation(new FakeLobby{Id=Guid.Empty},0,0,out var a);NativeOnlinePreview.BeforeCompute(Array.Empty<byte>(),"invalid",out var b);NativeOnlinePreview.BeforeCount(Frame(),out var c);NativeOnlinePreview.BeforeSummary(Frame(),out var d);NativeOnlinePreview.BeforeReceive(new FakeCommand{GameId=Guid.Empty},out var e);Check(a is null&&b is null&&c is null&&d is null&&e is null&&NativeOnlineSession.Started==0,"Disabled guards");});
                Enable(true);NativeOnlineDispatch.Handler=RunPatched;
                var harmony=new HarmonyLib.Harmony();
                var create=Leaf(nameof(FakeLeaves.CreateMultiplayerGame),3);var update=Leaf(nameof(FakeLeaves.ComputeModdedUpdate),5);
                var count=Leaf(nameof(FakeLeaves.CountCommands),1);var receive=Leaf(nameof(FakeLeaves.OnCommand),1);
                var summary=Leaf(nameof(FakeLeaves.FromGameStateByteArray),3);var summaryBytes=Leaf(nameof(FakeLeaves.FromGameStateByteArray),1);
                var followUps=Leaf(nameof(FakeLeaves.FeedFollowUpsToLiveClient),2);
                foreach(var (method,route) in new[]{(create,NativeOnlinePreview.ScopeRoute.Creation),(update,NativeOnlinePreview.ScopeRoute.Compute),(count,NativeOnlinePreview.ScopeRoute.Count),(receive,NativeOnlinePreview.ScopeRoute.Receive),(summary,NativeOnlinePreview.ScopeRoute.Summary),(summaryBytes,NativeOnlinePreview.ScopeRoute.Summary),(followUps,NativeOnlinePreview.ScopeRoute.LiveFollowUps)})NativeOnlinePreview.Patch(harmony,method,route);
                Test("PREVIEW-14-patches-bind-exact-typed-result-and-first-priority",()=>{Check(harmony.Hooks.Count==7,"Seven synchronous leaves");foreach(var hook in harmony.Hooks){Check(hook.prefix.priority==HarmonyLib.Priority.First,"Dispatch prefix first");Check(hook.finalizer.method.Name==nameof(NativeOnlinePreview.Finished),"Shared cleanup");if(hook.original.ReturnType!=typeof(void))Check(hook.prefix.method.GetGenericArguments().Single()==hook.original.ReturnType,"Typed value/reference return");else Check(hook.prefix.method.Name==nameof(NativeOnlinePreview.BeforeVoid),"Void prefix");}});
                Test("PREVIEW-15-worker-compute-reenters-scope-once-and-returns-tuple",()=>{ResetLeaf();NativeOnlineDispatch.IsMainThread=false;var bytes=Outer(Frame());object[] args={bytes,Epoch.ToString("D"),new object(),"hash",0};var result=(ValueTuple<int,List<byte[]>,byte[]>)RunPatched(update,args)!;Check(result.Item1==7&&ReferenceEquals(result.Item3,bytes),"ValueTuple survives dispatch");Check(NativeOnlineDispatch.Invoked==1&&FakeLeaves.Bodies==1&&FakeLeaves.ScopeAtBody==1,"One main reentry and one scope");Check(NativeOnlineSession.Started==1&&NativeOnlineSession.Disposed==1&&!NativeOnlineDispatch.IsMainThread,"No worker scope; thread context restored");});
                Test("PREVIEW-16-worker-summary-keeps-ref-out-array-and-reference-return",()=>{ResetLeaf();NativeOnlineDispatch.IsMainThread=false;object[] args={Outer(Frame()),null!,null!};Check((bool)RunPatched(summary,args)!,"Bool result");Check((string)args[1]=="summary"&&(int)args[2]==123,"Ref/out changes survive marshaling");Check(NativeOnlineSession.Started==1&&NativeOnlineSession.Disposed==1,"Summary scope once");var bytes=Frame();Check(ReferenceEquals(RunPatched(summaryBytes,new object[]{bytes}),bytes),"Reference result survives");Check(NativeOnlineDispatch.Invoked==2&&NativeOnlineSession.Started==2&&NativeOnlineSession.Disposed==2,"Each summary has one main scope");});
                Test("PREVIEW-17-worker-receive-and-live-followups-have-distinct-scopes",()=>{ResetLeaf();NativeOnlineDispatch.IsMainThread=false;FakePreview.Modded=true;FakePreview.Shadow=Frame();RunPatched(receive,new object[]{new FakeCommand{GameId=Epoch}});Check(NativeOnlineSession.Purpose=="preview-receive"&&NativeOnlineSession.Started==1&&FakeLeaves.ScopeAtBody==1,"Receive shadow scope on main");RunPatched(followUps,new object[]{Epoch.ToString("D"),new List<byte[]>()});Check(FakeLeaves.Bodies==2&&FakeLeaves.ScopeAtBody==0&&NativeOnlineSession.Started==1&&NativeOnlineSession.Disposed==1,"Live followups marshal without detached scope");Check(NativeOnlineDispatch.Invoked==2,"Both worker leaves dispatched");});
                Test("PREVIEW-18-skipped-original-has-no-dispatch-scope-or-result-write",()=>{ResetLeaf();NativeOnlineDispatch.IsMainThread=false;int result=19;Check(!NativeOnlinePreview.BeforeManaged(count,new object[]{Frame()},ref result,false,out var scope)&&scope is null&&result==19,"Skipped typed prefix");Check(!NativeOnlinePreview.BeforeVoid(receive,new object[]{new FakeCommand{GameId=Epoch}},false,out scope)&&scope is null,"Skipped void prefix");Check(NativeOnlineDispatch.Invoked==0&&NativeOnlineSession.Started==0,"No hidden work for suppressed original");});
                Test("PREVIEW-19-main-creation-and-worker-counter-clean-up-once",()=>{ResetLeaf();var result=(ValueTuple<byte[],string>)RunPatched(create,new object[]{new FakeLobby{Id=Epoch},123,28})!;Check(result.Item2==Epoch.ToString("D")&&NativeOnlineDispatch.Invoked==0,"Main creation stays synchronous");Check(NativeOnlineSession.Started==1&&NativeOnlineSession.Disposed==1&&FakeLeaves.ScopeAtBody==1,"Creation scope around body");NativeOnlineDispatch.IsMainThread=false;Check((int)RunPatched(count,new object[]{Frame()})! ==3,"Worker counter return");Check(NativeOnlineDispatch.Invoked==1&&NativeOnlineSession.Started==2&&NativeOnlineSession.Disposed==2,"Counter scope around main body");});
                Test("PREVIEW-20-dispatched-body-exception-preserved-with-single-disposal",()=>{ResetLeaf();NativeOnlineDispatch.IsMainThread=false;var expected=new IOException("shadow failure");FakeLeaves.Error=expected;try{RunPatched(count,new object[]{Frame()});throw new Exception("Expected original failure");}catch(IOException error){Check(ReferenceEquals(error,expected),"Original exception preserved");}Check(NativeOnlineSession.Started==1&&NativeOnlineSession.Disposed==1&&ReferenceEquals(NativeOnlineSession.Error,expected),"Only main scope fails and disposes");Check(!NativeOnlineDispatch.IsMainThread,"Worker context restored on failure");});
                Test("PREVIEW-21-unregistered-method-cannot-be-dispatched",()=>{ResetLeaf();NativeOnlineDispatch.IsMainThread=false;int result=0;Reject(()=>NativeOnlinePreview.BeforeManaged(typeof(FakeLeaves).GetMethod(nameof(FakeLeaves.Unregistered))!,Array.Empty<object>(),ref result,true,out _));Check(NativeOnlineDispatch.Invoked==0&&NativeOnlineSession.Started==0,"No arbitrary reflection work");});
                Test("PREVIEW-22-disabled-combined-prefixes-preserve-native-behavior",()=>{ResetLeaf();Enable(false);NativeOnlineDispatch.IsMainThread=false;int result=19;Check(NativeOnlinePreview.BeforeManaged(count,new object[]{Frame()},ref result,true,out var scope)&&scope is null&&result==19,"Disabled typed passthrough");Check(NativeOnlinePreview.BeforeVoid(receive,new object[]{new FakeCommand{GameId=Epoch}},true,out scope)&&scope is null,"Disabled void passthrough");Check(NativeOnlineDispatch.Invoked==0&&NativeOnlineSession.Started==0,"Unavailable adapter never dispatches");});
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,tests=Names,scope="actual managed adapter with boundary doubles; no Harmony/native/server execution"}));return 0;
            }
            catch(Exception e){Console.WriteLine(e);Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases,assertions=Assertions}));return 1;}
            finally{Enable(false);}
        }
    }
    public static class FakeCompatibility {public static string checksum="";}
    public static class FakePreview {public static bool Modded;public static byte[]? Shadow;public static bool IsModdedGame(string id)=>Modded;public static byte[]? GetShadowState(string id)=>Shadow;}
    public sealed class FakeLobby {public Guid Id{get;set;}}
    public sealed class FakeCommand {public Guid GameId{get;set;}}
    public static class FakeLeaves
    {
        public static int Bodies,ScopeAtBody;public static Exception? Error;
        static void Body(){if(!NativeOnlineDispatch.IsMainThread)throw new Exception("Native body ran on worker");Bodies++;ScopeAtBody=NativeOnlineSession.Started-NativeOnlineSession.Disposed;if(Error is not null)throw Error;}
        public static (byte[],string) CreateMultiplayerGame(FakeLobby lobby,int version,int logic){Body();return (Array.Empty<byte>(),lobby.Id.ToString("D"));}
        public static (int,List<byte[]>,byte[]) ComputeModdedUpdate(byte[] bytes,string id,object command,string hash,int count){Body();return (7,new List<byte[]>(),bytes);}
        public static int CountCommands(byte[] bytes){Body();return 3;}
        public static void OnCommand(FakeCommand data){Body();}
        public static bool FromGameStateByteArray(byte[] bytes,out string summary,out object state){Body();summary="summary";state=123;return true;}
        public static byte[] FromGameStateByteArray(byte[] bytes){Body();return bytes;}
        public static void FeedFollowUpsToLiveClient(string id,List<byte[]> commands){Body();}
        public static int Unregistered()=>0;
    }
}
