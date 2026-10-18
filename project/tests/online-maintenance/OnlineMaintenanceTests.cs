using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Deception.PolyModAdapter;

namespace Deception.Tests
{
    public static class OnlineMaintenanceTests
    {
        static int Cases,Assertions;static readonly List<string> Names=new();
        static void Check(bool value,string reason){Assertions++;if(!value)throw new Exception(reason);}
        static void Test(string name,Action run){Fixture.Reset();Cases++;Names.Add(name);try{run();Check(Fixture.Requests==0,"Observer created network request");}catch(Exception e){throw new Exception(name,e);}}
        static NativeOnlineMaintenance.Completion? Begin(bool original=true)
        {
            NativeOnlineMaintenance.BeforeParticipation(new PolytopiaBackendBase.Game.SetParticipationDoneBindingModel{GameId=new(Fixture.Game)},original,out var state);
            NativeOnlineMaintenance.AfterParticipation(Fixture.Task,state);return state;
        }
        static void Finish(bool success)
        {Fixture.Task.IsCompleted=true;Fixture.Task.Result=new(){Success=success,ErrorCode=PolytopiaBackendBase.ErrorCode.Denied};NativeOnlineMaintenance.Tick();}
        public static int Run()
        {
            try
            {
                var harmony=new HarmonyLib.Harmony();NativeOnlineMaintenance.Install(harmony,typeof(Fixture).Assembly);
                Check(harmony.Patches.Count==3&&harmony.Patches.Any(x=>x.Method.Name=="SetParticipationDone"&&x.Prefix?.method.Name=="BeforeParticipation"&&x.Postfix?.method.Name=="AfterParticipation"),"Wrong hooks");
                Test("MAINT-01-success-removes-ended-row-exactly-once",()=>{Check(Begin() is not null,"No capture");NativeOnlineMaintenance.Tick();Check(Fixture.Removals==0,"Removed before completion");Finish(true);NativeOnlineMaintenance.Tick();Check(Fixture.Removals==1&&Fixture.Events==1&&GameManager.Manager.Summary is null,"Wrong native cache completion");Check(Fixture.Task.Result.Success,"Response changed");});
                Test("MAINT-02-rejected-response-retains-row",()=>{Begin();Finish(false);Check(Fixture.Removals==0&&GameManager.Manager.Summary is not null,"Rejected row removed");Check(Entry.Notes.Any(x=>x.Contains("code=7"))&&!Entry.Notes.Any(x=>x.Contains("SECRET")),"Unsafe error diagnostic");});
                Test("MAINT-03-faulted-response-retains-row",()=>{Begin();Fixture.Task.IsFaulted=true;Fixture.Task.IsCompleted=true;NativeOnlineMaintenance.Tick();Check(Fixture.Removals==0,"Faulted row removed");});
                Test("MAINT-04-canceled-response-retains-row",()=>{Begin();Fixture.Task.IsCanceled=true;Fixture.Task.IsCompleted=true;NativeOnlineMaintenance.Tick();Check(Fixture.Removals==0,"Canceled row removed");});
                Test("MAINT-05-null-response-retains-row",()=>{Begin();Fixture.Task.IsCompleted=true;NativeOnlineMaintenance.Tick();Check(Fixture.Removals==0,"Null response removed row");});
                Test("MAINT-06-active-game-is-never-captured",()=>{GameManager.Manager.Summary!.State=3;Check(Begin() is null,"Active game captured");Finish(true);Check(Fixture.Removals==0,"Active game removed");});
                Test("MAINT-07-nonterminal-states-are-never-captured",()=>{foreach(int state in new[]{0,1,2,3,5,99}){GameManager.Manager.Summary!.State=state;Check(Begin() is null,"Unknown state captured");}Finish(true);Check(Fixture.Removals==0,"Nonterminal removed");});
                Test("MAINT-08-state-change-before-success-retains-row",()=>{Begin();GameManager.Manager.Summary!.State=3;Finish(true);Check(Fixture.Removals==0,"Changed state removed");});
                Test("MAINT-09-account-switch-retains-row",()=>{Begin();AccountManager.PlayerAccountId=new(Fixture.Other);Finish(true);Check(Fixture.Removals==0,"Foreign account removed row");});
                Test("MAINT-10-logout-retains-row",()=>{Begin();AccountManager.PlayerAccountId=new(Guid.Empty);Finish(true);Check(Fixture.Removals==0,"Logout removed row");});
                Test("MAINT-11-cache-switch-retains-row",()=>{Begin();GameManager.Manager=new(){Summary=new(){GameId=new(Fixture.Game)}};Finish(true);Check(Fixture.Removals==0,"New cache changed");});
                Test("MAINT-12-native-event-already-removed-no-double-event",()=>{Begin();GameManager.Manager.Summary=null;Finish(true);Check(Fixture.Removals==0&&Fixture.Events==0,"Duplicate removal event");});
                Test("MAINT-13-nonmodded-row-is-not-captured",()=>{PolyMod.Multiplayer.ModdedClient.Modded=false;Check(Begin() is null,"Nonmodded row captured");Finish(true);Check(Fixture.Removals==0,"Nonmodded ended row removed");});
                Test("MAINT-14-skipped-original-is-never-observed",()=>{Check(Begin(false) is null,"Skipped request captured");Finish(true);Check(Fixture.Removals==0,"Skipped request mutated");});
                Test("MAINT-15-unavailable-preview-preserves-native-route",()=>{NativeOnlinePreview.Available=false;Check(Begin() is null,"Unavailable captured");Finish(true);Check(Fixture.Removals==0,"Unavailable mutated");});
                Test("MAINT-16-worker-hooks-touch-no-native-state",()=>{Task.Run(()=>{Check(Begin() is null,"Worker captured");NativeOnlineMaintenance.Tick();NativeOnlineMaintenance.ObserveClock();}).GetAwaiter().GetResult();Check(Fixture.Removals==0,"Worker mutated");});
                Test("MAINT-17-observer-timeout-does-not-cancel-request",()=>{var state=Begin()!;state.Started=Stopwatch.GetTimestamp()-121L*Stopwatch.Frequency;NativeOnlineMaintenance.Tick();Finish(true);Check(Fixture.Removals==0&&!Fixture.Task.IsCanceled,"Timeout changed original task");});
                Test("MAINT-18-duplicate-success-is-idempotent",()=>{Begin();Begin();Finish(true);Check(Fixture.Removals==1&&Fixture.Events==1,"Duplicate event");});
                Test("MAINT-19-null-return-task-retains-row",()=>{NativeOnlineMaintenance.BeforeParticipation(new PolytopiaBackendBase.Game.SetParticipationDoneBindingModel{GameId=new(Fixture.Game)},true,out var state);NativeOnlineMaintenance.AfterParticipation(null,state);Finish(true);Check(Fixture.Removals==0,"Null task removed row");});
                Test("MAINT-20-cache-lookup-failure-preserves-request",()=>{GameManager.Manager.ThrowFind=true;Check(Begin() is null,"Thrown lookup captured");Check(!Entry.Notes.Any(x=>x.Contains("SECRET")),"Exception leaked");});
                Test("MAINT-21-removal-failure-does-not-alter-success",()=>{Begin();GameManager.Manager.ThrowRemove=true;Finish(true);Check(Fixture.Task.Result.Success&&Fixture.Removals==0,"Cache exception changed server success");});
                Test("MAINT-22-observer-capacity-is-bounded",()=>{for(int i=0;i<16;i++)Check(Begin() is not null,"Capacity too small");Check(Begin() is null,"Capacity not enforced");Finish(true);Check(Fixture.Removals==1,"Bounded completion lost");});
                Test("MAINT-23-no-account-is-not-captured",()=>{AccountManager.PlayerAccountId=new(Guid.Empty);Check(Begin() is null,"Anonymous capture");});
                Test("MAINT-24-different-game-not-captured",()=>{GameManager.Manager.Summary!.GameId=new(Fixture.Other);Check(Begin() is null,"Wrong game capture");});
                Test("MAINT-25-clock-absence-is-observed-without-invention",()=>{var summary=GameManager.Manager.Summary!;NativeOnlineMaintenance.ObserveClock();Check(Entry.Notes.Single().Contains("deadline=False; timeBank=False")&&Entry.Notes.Single().Contains("unsupported"),"Missing field facts lost");Check(!summary.Participators[0].DateCurrentTurnDeadline.HasValue&&!summary.Participators[0].TimeBank.HasValue,"Clock invented");Check(!Entry.Notes.Any(x=>x.Contains(Fixture.Game.ToString())||x.Contains(Fixture.Account.ToString())||x.Contains("SECRET")),"Identity leaked");NativeOnlineMaintenance.ObserveClock();Check(Entry.Notes.Count==1,"Clock flooded logs");});
                Test("MAINT-26-clock-authoritative-deadline-retained",()=>{GameManager.Manager.Summary!.Participators[0].DateCurrentTurnDeadline.HasValue=true;NativeOnlineMaintenance.ObserveClock();Check(Entry.Notes.Single().Contains("native deadline available"),"Authoritative deadline ignored");});
                Test("MAINT-27-clock-nonmodded-game-ignored",()=>{PolyMod.Multiplayer.ModdedClient.Modded=false;NativeOnlineMaintenance.ObserveClock();Check(Entry.Notes.Count==0,"Nonmodded clock observed");});
                Test("MAINT-28-clock-no-live-client-ignored",()=>{GameManager.Client=null;NativeOnlineMaintenance.ObserveClock();Check(Entry.Notes.Count==0,"No-client clock observed");});
                Test("MAINT-29-summary-replacement-retains-new-generation",()=>{Begin();GameManager.Manager.Summary=new(){GameId=new(Fixture.Game)};Finish(true);Check(Fixture.Removals==0,"Replacement summary removed");});
                Test("MAINT-30-binding-change-before-postfix-is-not-observed",()=>{var binding=new PolytopiaBackendBase.Game.SetParticipationDoneBindingModel{GameId=new(Fixture.Game)};NativeOnlineMaintenance.BeforeParticipation(binding,true,out var state);binding.GameId=new(Fixture.Other);NativeOnlineMaintenance.AfterParticipation(Fixture.Task,state);Finish(true);Check(Fixture.Removals==0,"Changed binding paired with old task");});
                Test("MAINT-31-binding-change-while-pending-retains-row",()=>{var state=Begin()!;((PolytopiaBackendBase.Game.SetParticipationDoneBindingModel)state.Binding).GameId=new(Fixture.Other);Finish(true);Check(Fixture.Removals==0,"Changed pending binding removed row");});
                var result=new{passed=true,cases=Cases,assertions=Assertions,tests=Names,scope="Actual maintenance source with strict boundary doubles; no Harmony detour, game, backend request or live deletion."};
                string json=JsonSerializer.Serialize(result);Console.WriteLine(json);var output=Environment.GetEnvironmentVariable("DECEPTION_TEST_OUTPUT");if(output is not null)File.WriteAllText(output,json);return 0;
            }
            catch(Exception e){Console.WriteLine(e);return 1;}
        }
    }
}
