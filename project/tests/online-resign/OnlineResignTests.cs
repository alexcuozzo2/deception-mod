using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Deception.PolyModAdapter;
using Source=Il2CppSystem.Threading.Tasks.TaskCompletionSource<PolytopiaBackendBase.ServerResponse<PolytopiaBackendBase.ResponseViewModel>>;

namespace Deception.Tests
{
    public static class OnlineResignTests
    {
        static int Cases,Assertions;static readonly List<string> Names=new();
        const string ContractMessage="Could not resign: this game's synchronization data does not match the loaded mod setup. No resignation was sent. Keep the game and send the latest log to Alex.";
        const string UnconfirmedMessage="Could not confirm the resignation. Reopen the game to check its status before trying again.";
        static void Check(bool good,string why){Assertions++;if(!good)throw new Exception(why);}
        static void Test(string name,Action run){Cases++;Names.Add(name);Fixture.Reset();try{run();Check(NativeOnlineSession.Active==0&&NativeOnlineSession.Started==NativeOnlineSession.Disposed,"Scope leaked");Check(PolyMod.Multiplayer.ModdedClient._sendLock.CurrentCount==1,"Send lock leaked");}catch(Exception e){throw new Exception(name,e);}}
        static (Source source,Task task) Begin(bool worker=false,System.Guid? epoch=null)
        {
            var source=new Source();source.Popup=new ResignPopupProbe();source.Popup.Completion=source.Popup.Observe(source.Task);var guid=new Il2CppSystem.Guid(epoch??Fixture.Epoch);
            Task Start(){Task task=Task.CompletedTask;Check(!NativeOnlineResign.Before(source,guid,ref task,true),"Original handler not replaced");return task;}
            return (source,worker?Task.Run(Start):Start());
        }
        static void Until(Func<bool> condition)
        {
            var limit=DateTime.UtcNow.AddSeconds(5);
            while(!condition()){TeslaDispatchComponent.Drain();if(DateTime.UtcNow>limit)throw new Exception("Bounded async fixture timed out");Thread.Sleep(1);}TeslaDispatchComponent.Drain();
        }
        static void Done((Source source,Task task) value,bool success)
        {Until(()=>value.task.IsCompleted);value.task.GetAwaiter().GetResult();Check(value.source.Completions==1,"Native completion was not exactly once");Check(value.source.Error is null&&value.source.Result?.Success==success,"Native caller must receive a handled response, never a faulted task");
         Check(value.source.Popup!.Completion.IsCompletedSuccessfully&&!value.source.Popup.Loader,"Native popup await faulted or left its loader open");
         Check(value.source.Popup.FailurePopups==(success?0:1)&&value.source.Popup.Successes==(success?1:0),"Native popup took the wrong response branch");Check(success||!string.IsNullOrWhiteSpace(value.source.Result?.ErrorMessage),"Handled failure has no backend popup message");}
        static void FinishUpload(bool value)=>Task.Run(()=>Fixture.UploadGate!.SetResult(value)).GetAwaiter().GetResult();
        public static int Run()
        {
            try
            {
                Native.MainThread=Environment.CurrentManagedThreadId;NativeOnlineDispatch.Initialize();var harmony=new HarmonyLib.Harmony();
                NativeOnlineResign.Install(harmony,typeof(Fixture).Assembly);
                Check(harmony.Original?.Name=="HandleModdedResign"&&harmony.Prefix?.method.Name=="Before"&&harmony.Prefix.priority==800,"Wrong patch surface");
                Test("RESIGN-01-cached-success-preserves-command-and-native-arguments",()=>{var r=Begin();Done(r,true);Check(Fixture.Fetches==0&&Fixture.Uploads==1&&Fixture.Sets==1&&Fixture.Receives==1,"Wrong cached flow");Check(Fixture.LastResign is {Issuer:1,Target:2,Reason:0,Flag:false},"Wrong native resign payload");Check(Fixture.Received?.Length==3&&ReferenceEquals(Fixture.Received[0],Fixture.LastResign),"Resign must precede followups");Check(Fixture.Events.SequenceEqual(new[]{"get","compute","upload","set","success"}),"Preview order changed");});
                Test("RESIGN-02-worker-entry-marshals-every-native-leaf",()=>{Done(Begin(true),true);Check(Fixture.Computes==1&&Fixture.Receives==1,"Worker route failed");});
                Test("RESIGN-03-fetch-and-upload-continuations-have-no-scope",()=>{Fixture.Shadow=null;Fixture.FetchGate=new(TaskCreationOptions.RunContinuationsAsynchronously);Fixture.UploadGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var r=Begin();Check(!r.task.IsCompleted&&NativeOnlineSession.Active==0,"Scope held during fetch");Task.Run(()=>Fixture.FetchGate.SetResult(new byte[]{1,123})).Wait();Until(()=>Fixture.Uploads==1);Check(NativeOnlineSession.Active==0&&NativeOnlineSession.Disposed==1,"Scope held during upload");FinishUpload(true);Done(r,true);Check(Fixture.Fetches==1&&Fixture.Receives==1,"Fetched flow failed");});
                Test("RESIGN-04-null-fetch-fails-without-simulation",()=>{Fixture.Shadow=null;Done(Begin(),false);Check(Fixture.Fetches==1&&Fixture.Decodes==0&&Fixture.Uploads==0&&Fixture.Drops==0,"Null shadow mutated state");});
                Test("RESIGN-05-unframed-shadow-rejected-before-native-decode",()=>{Fixture.Shadow=new byte[]{0,123};Done(Begin(),false);Check(Fixture.Decodes==0&&Fixture.Uploads==0,"Unframed shadow admitted");});
                Test("RESIGN-06-other-match-frame-rejected",()=>{Done(Begin(epoch:Fixture.Other),false);Check(Fixture.Decodes==0&&Fixture.Uploads==0,"Wrong epoch admitted");});
                Test("RESIGN-07-other-contract-frame-rejected",()=>{Fixture.Shadow=new byte[]{1,124};Done(Begin(),false);Check(Fixture.Decodes==0&&Fixture.Uploads==0,"Wrong contract admitted");});
                Test("RESIGN-08-native-decode-false-never-uploads",()=>{Fixture.DecodeFalse=true;Done(Begin(),false);Check(Fixture.Decodes==1&&Fixture.Computes==0&&Fixture.Uploads==0&&Fixture.Drops==0,"Failed decoder ignored");});
                Test("RESIGN-09-native-version-mismatch-never-uploads",()=>{Fixture.Version=124;Done(Begin(),false);Check(Fixture.Computes==0&&Fixture.Uploads==0,"Wrong version admitted");});
                Test("RESIGN-10-missing-account-player-never-uploads",()=>{Fixture.MissingPlayer=true;Done(Begin(),false);Check(Fixture.Computes==0&&Fixture.Uploads==0&&Fixture.Drops==0,"Missing own player admitted");});
                Test("RESIGN-11-null-compute-drops-shadow",()=>{Fixture.ComputeNull=true;Done(Begin(),false);Check(Fixture.Drops==1&&Fixture.Uploads==0&&Fixture.Receives==0,"Null update handling");});
                Test("RESIGN-12-compute-exception-cleans-scope",()=>{Fixture.ComputeThrows=true;Done(Begin(),false);Check(Fixture.Uploads==0&&Fixture.Receives==0,"Thrown compute leaked commit");});
                Test("RESIGN-13-upload-false-drops-shadow",()=>{Fixture.UploadGate=new();Fixture.UploadGate.SetResult(false);Done(Begin(),false);Check(Fixture.Drops==1&&Fixture.Sets==0&&Fixture.Receives==0,"Rejected upload applied");});
                Test("RESIGN-14-upload-exception-does-not-apply",()=>{Fixture.UploadGate=new();Fixture.UploadGate.SetException(new IOException("upload failed"));Done(Begin(),false);Check(Fixture.Sets==0&&Fixture.Receives==0,"Thrown upload applied");});
                Test("RESIGN-15-closed-client-preserves-server-success",()=>{GameManager.Client=null;Done(Begin(),true);Check(Fixture.Sets==1&&Fixture.Receives==0,"Closed client altered server success");});
                Test("RESIGN-16-different-client-never-receives",()=>{GameManager.Client!.CurrentGameId.Value=new(Fixture.Other);Done(Begin(),true);Check(Fixture.Sets==1&&Fixture.Receives==0,"Foreign client received resignation");});
                Test("RESIGN-17-no-current-game-never-receives",()=>{GameManager.Client!.CurrentGameId.HasValue=false;Done(Begin(),true);Check(Fixture.Receives==0,"No-game client received");});
                Test("RESIGN-18-invalid-followup-tail-rejected-before-upload",()=>{Fixture.FollowUps.Add(new byte[]{0});Done(Begin(),false);Check(Fixture.CommandDecodes==3&&Fixture.Uploads==0&&Fixture.Sets==0&&Fixture.Receives==0,"Bad tail partially committed");});
                Test("RESIGN-19-followup-version-rejected-before-upload",()=>{Fixture.FollowUps.Add(new byte[]{124});Done(Begin(),false);Check(Fixture.Uploads==0&&Fixture.Receives==0,"Bad command version committed");});
                Test("RESIGN-20-empty-followups-still-feed-resign",()=>{Fixture.FollowUps.Clear();Done(Begin(),true);Check(Fixture.Received?.Length==1&&Fixture.Received[0] is ResignCommand,"Resign lost on empty tail");});
                Test("RESIGN-21-account-change-before-upload-rejects",()=>{Fixture.ComputeChangesAccount=true;Done(Begin(),false);Check(Fixture.Uploads==0&&Fixture.Receives==0,"Wrong account upload");});
                Test("RESIGN-22-account-change-after-upload-skips-live-apply",()=>{Fixture.UploadGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var r=Begin();AccountManager.PlayerAccountId=new(Fixture.Other);FinishUpload(true);Done(r,true);Check(Fixture.Sets==1&&Fixture.Receives==0,"Switched account received old commands");});
                Test("RESIGN-23-logout-after-upload-keeps-server-success",()=>{Fixture.UploadGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var r=Begin();AccountManager.PlayerAccountId=new(System.Guid.Empty);FinishUpload(true);Done(r,true);Check(Fixture.Sets==1&&Fixture.Receives==0,"Logout caused postcommit retry ambiguity");});
                Test("RESIGN-24-switch-game-during-upload-skips-live-apply",()=>{Fixture.UploadGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var r=Begin();GameManager.Client!.CurrentGameId.Value=new(Fixture.Other);FinishUpload(true);Done(r,true);Check(Fixture.Receives==0,"New game received old resignation");});
                Test("RESIGN-25-postcommit-receive-failure-still-reports-server-success",()=>{Fixture.ReceiveThrows=true;Done(Begin(),true);Check(Fixture.Uploads==1&&Fixture.Sets==1&&Fixture.Receives==1,"Committed resignation status lost");});
                Test("RESIGN-26-shared-lock-serializes-concurrent-resignations",()=>{Fixture.UploadGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var a=Begin();var b=Begin(true);Check(Fixture.Computes==1&&PolyMod.Multiplayer.ModdedClient._sendLock.CurrentCount==0,"Concurrent command bypassed send lock");FinishUpload(true);Done(a,true);Done(b,true);Check(Fixture.Computes==2&&Fixture.Uploads==2&&Fixture.Receives==2,"Waiting command lost");});
                Test("RESIGN-27-fetch-exception-completes-and-releases-lock",()=>{Fixture.Shadow=null;Fixture.FetchGate=new();Fixture.FetchGate.SetException(new IOException("fetch failed"));Done(Begin(),false);Check(Fixture.Uploads==0,"Fetch error uploaded");});
                Test("RESIGN-28-completion-throw-is-not-retried",()=>{Fixture.UploadGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var r=Begin();r.source.ThrowCompletion=true;FinishUpload(true);Until(()=>r.task.IsCompleted);Check(r.task.IsFaulted&&r.source.Completions==1,"Failed native completion was retried");});
                Test("RESIGN-29-skipped-original-does-no-work",()=>{Task existing=Task.CompletedTask;Check(!NativeOnlineResign.Before(new Source(),new Il2CppSystem.Guid(Fixture.Epoch),ref existing,false)&&existing==Task.CompletedTask,"Skipped original result changed");Check(Fixture.Computes==0,"Skipped original performed resign");});
                Test("RESIGN-30-unavailable-preview-preserves-original",()=>{NativeOnlinePreview.Available=false;try{Task existing=Task.CompletedTask;Check(NativeOnlineResign.Before(new Source(),new Il2CppSystem.Guid(Fixture.Epoch),ref existing,true),"Unavailable preview replaced original");Check(Fixture.Computes==0,"Unavailable preview ran simulation");}finally{NativeOnlinePreview.Available=true;}});
                Test("RESIGN-31-cached-contract-rejection-preserves-shadow-and-clears-native-loader",()=>{
                    Fixture.Shadow=new byte[]{1,124,7};var original=Fixture.Shadow;var copy=original.ToArray();var r=Begin();Done(r,false);
                    Check(Fixture.Decodes==0&&Fixture.Computes==0&&Fixture.Uploads==0&&Fixture.Sets==0&&Fixture.Drops==0&&Fixture.Receives==0,"Contract rejection performed game work");
                    Check(ReferenceEquals(Fixture.Shadow,original)&&Fixture.Shadow.SequenceEqual(copy),"Contract rejection rewrote or deleted snapshot");
                    Check(NativeOnlineSession.Started==0&&Entry.Notes.Count(n=>n.Kind=="online-resign-failed")==1,"Rejection began authority or lost diagnostic");
                    Check(r.source.Result?.ErrorMessage==ContractMessage,"Compatibility failure lacks actionable nontechnical explanation");
                });
                Test("RESIGN-32-fetched-contract-rejection-clears-native-loader-without-mutation",()=>{
                    Fixture.Shadow=null;Fixture.FetchGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var r=Begin();var old=new byte[]{1,124,7};
                    Task.Run(()=>Fixture.FetchGate.SetResult(old)).Wait();Done(r,false);
                    Check(Fixture.Fetches==1&&Fixture.Decodes==0&&Fixture.Computes==0&&Fixture.Uploads==0&&Fixture.Sets==0&&Fixture.Drops==0&&Fixture.Receives==0,"Fetched mismatch reached simulation or mutation");
                    Check(old.SequenceEqual(new byte[]{1,124,7})&&Fixture.Shadow is null&&NativeOnlineSession.Started==0,"Fetched mismatch mutated supplied bytes or authority");
                    Check(r.source.Result?.ErrorMessage==ContractMessage,"Fetched compatibility failure explanation differs");
                });
                Test("RESIGN-33-fetch-cancellation-becomes-handled-failure",()=>{
                    Fixture.Shadow=null;Fixture.FetchGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var r=Begin();Task.Run(()=>Fixture.FetchGate.SetCanceled()).Wait();Done(r,false);
                    Check(Fixture.Decodes==0&&Fixture.Uploads==0&&Fixture.Sets==0&&Fixture.Receives==0,"Cancelled fetch mutated match");
                });
                Test("RESIGN-34-upload-cancellation-does-not-claim-commit",()=>{
                    Fixture.UploadGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var original=Fixture.Shadow;var r=Begin();Task.Run(()=>Fixture.UploadGate.SetCanceled()).Wait();Done(r,false);
                    Check(Fixture.Uploads==1&&Fixture.Sets==0&&Fixture.Receives==0&&ReferenceEquals(Fixture.Shadow,original),"Cancelled upload reported or applied commit");
                });
                Test("RESIGN-35-rejection-does-not-poison-next-accepted-request",()=>{
                    Fixture.Shadow=new byte[]{1,124};var denied=Begin();Done(denied,false);Fixture.Shadow=new byte[]{1,123};var accepted=Begin(true);Done(accepted,true);
                    Check(Fixture.Computes==1&&Fixture.Uploads==1&&Fixture.Sets==1&&Fixture.Receives==1,"Rejected request poisoned subsequent valid resignation");
                    Check(denied.source.Completions==1&&denied.source.Result?.Success==false&&denied.source.Popup!.FailurePopups==1,"Retry changed prior response");
                });
                Test("RESIGN-36-rejection-native-completion-throw-is-not-retried",()=>{
                    Fixture.Shadow=null;Fixture.FetchGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var r=Begin();r.source.ThrowCompletion=true;
                    Task.Run(()=>Fixture.FetchGate.SetResult(new byte[]{1,124})).Wait();Until(()=>r.task.IsCompleted);
                    Check(r.task.IsFaulted&&r.source.Completions==1&&r.source.Error is null,"Failed response completion was retried or replaced with native fault");
                    Check(Fixture.Decodes==0&&Fixture.Computes==0&&Fixture.Uploads==0&&Fixture.Sets==0&&Fixture.Receives==0,"Completion failure bypassed admission");
                });
                Test("RESIGN-37-compute-error-uses-safe-unconfirmed-response-text",()=>{
                    Fixture.ComputeThrows=true;var r=Begin();Done(r,false);
                    Check(r.source.Result?.ErrorMessage==UnconfirmedMessage&&!r.source.Result.ErrorMessage.Contains("compute failed"),"Raw internal error escaped into backend popup");
                    Check(Entry.Notes.Any(n=>n.Kind=="online-resign-failed"&&n.Text=="compute failed")&&Fixture.Uploads==0,"Safe popup lost technical diagnostic or uploaded despite failure");
                });
                Test("RESIGN-38-normal-upload-rejection-retains-preview-failure-message",()=>{
                    Fixture.UploadGate=new();Fixture.UploadGate.SetResult(false);var r=Begin();Done(r,false);
                    Check(r.source.Result?.ErrorMessage=="Modded game operation failed.","Normal backend rejection text was replaced");
                    Check(!Entry.Notes.Any(n=>n.Kind=="online-resign-failed")&&Fixture.Sets==0&&Fixture.Receives==0,"Normal rejected response was misclassified as thrown error or applied");
                });
                Test("RESIGN-39-upload-error-contract-prefix-keeps-commit-uncertainty",()=>{
                    Fixture.UploadGate=new(TaskCreationOptions.RunContinuationsAsynchronously);var original=Fixture.Shadow;var r=Begin();
                    Task.Run(()=>Fixture.UploadGate.SetException(new IOException("Online snapshot compatibility contract differs. upload reply unavailable"))).Wait();Done(r,false);
                    Check(r.source.Result?.ErrorMessage==UnconfirmedMessage&&!r.source.Result.ErrorMessage.Contains("No resignation was sent"),"Upload exception incorrectly claimed no server commit");
                    Check(Fixture.Uploads==1&&Fixture.Sets==0&&Fixture.Receives==0&&ReferenceEquals(original,Fixture.Shadow),"Unconfirmed upload fault applied commands or changed cached snapshot");
                });
                var result=new{passed=true,cases=Cases,assertions=Assertions,tests=Names,scope="actual resign adapter and dispatcher; strict native/thread/authority and preview boundary doubles; no Harmony detour, game or network resignation"};
                var json=JsonSerializer.Serialize(result);Console.WriteLine(json);string? output=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT");if(output is not null)File.WriteAllText(output,json);return 0;
            }
            catch(Exception e){Console.WriteLine(e);Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases,assertions=Assertions}));return 1;}
        }
    }
}
