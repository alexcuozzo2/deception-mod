using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Deception.PolyModAdapter;

// Managed callback model only. No actual native wrappers, hooks or libraries.
namespace Deception.PolyModAdapter
{
    internal sealed class MockObject
    {
        internal readonly Dictionary<string,object?> Fields=new();
        internal bool FailReads;
        internal MockObject(params (string Key,object? Value)[] fields)
        {foreach(var field in fields)Fields.Add(field.Key,field.Value);}
    }
    internal static class Native
    {
        internal static object? Get(object value,string name)
        {
            var item=(MockObject)value;if(item.FailReads)throw new InvalidOperationException("Injected read failure");
            return item.Fields[name];
        }
        internal static int Int(object? value)=>Convert.ToInt32(value);
        internal static bool Same(object? a,object? b)
        {if(a is MockObject x&&x.FailReads||b is MockObject y&&y.FailReads)throw new InvalidOperationException("Injected pointer read failure");return a is not null&&ReferenceEquals(a,b);}
        internal static string EnumName(object value)=>(string)value;
        internal static string M(string owner,string name,params string[] args)=>owner+"."+name;
        internal static object? Invoke(string method,object? target,params object?[] args)
        {
            if(method=="GameState.TryGetPlayer")
            {
                var players=(Dictionary<byte,object>)Get(target!,"Players")!;
                bool found=players.TryGetValue((byte)args[0]!,out var player);args[1]=player;return found;
            }
            if(method=="MapDataExtensions.GetTile")return Get(args[0]!,"Tile");
            throw new MissingMethodException(method);
        }
    }
}

namespace Deception.PaidObservationMocks
{
    using Observer=NativePaidBirthObservation;
    internal sealed class Trial:Observer.IAuthority
    {
        internal readonly object Permit=new();
        internal readonly MockObject Coordinates=new(("X",3),("Y",4));
        internal readonly MockObject State,Action,Player,Tile,Data,Spawn;
        internal bool Revoked;
        internal int AuthorityCalls;
        readonly int Thread=Environment.CurrentManagedThreadId;
        internal Trial()
        {
            const string rule="dcp_mimic_form_warrior";
            Player=new(("Id",(byte)1),("Currency",8));Data=new(("type",rule));
            Tile=new(("unit",null));
            State=new(("Version",123),("CurrentPlayer",(byte)1),("CurrentTurn",(uint)4),("CurrentCommand",(ushort)8),
                ("CurrentUnitId",(uint)9),("Players",new Dictionary<byte,object>{{1,Player}}),("Map",new MockObject(("Tile",Tile))));
            Action=new(("PlayerId",(byte)1),("Coordinates",Coordinates),("Cost",1),("trainActionAborted",false),("Type",rule));
            Spawn=new(("id",(uint)10),("UnitData",Data),("type",rule),("owner",(byte)1),("coordinates",Coordinates),("home",Coordinates));
        }
        public void RequirePrivateAction(object permit,object action,object state,object player,object tile,object data)
        {
            AuthorityCalls++;
            if(Revoked||Environment.CurrentManagedThreadId!=Thread||!ReferenceEquals(permit,Permit)||!ReferenceEquals(action,Action)||
                !ReferenceEquals(state,State)||!ReferenceEquals(player,Player)||!ReferenceEquals(tile,Tile)||!ReferenceEquals(data,Data))
                throw new InvalidOperationException("Mock authority rejected exact private action.");
        }
        internal Observer.Scope Begin()=>Observer.Begin(this,Permit,Action,State,Player,Tile,Data,"dcp_mimic_form_warrior");
        internal object? Prefix(object? state=null)
        {Observer.BeforeTrainUnit(state??State,Player,Tile,Data,out var frame);return frame;}
        internal void Body()
        {State.Fields["CurrentUnitId"]=(uint)10;Player.Fields["Currency"]=7;Tile.Fields["unit"]=Spawn;}
        internal void Post(object? frame,bool ran=true,object? result=null,bool explicitNull=false)=>
            Observer.AfterTrainUnit(State,Player,Tile,Data,explicitNull?null:result??Spawn,ran,frame);
        internal Exception? Final(object? frame,Exception? exception=null)=>Observer.TrainUnitFinished(exception,frame);
        internal void Success()
        {var frame=Prefix();Body();Post(frame);Final(frame);}
    }

    public static class Harness
    {
        static int Assertions,Failed;
        static readonly List<object> Results=new();
        static void Check(bool value,string message)
        {Assertions++;if(!value)throw new InvalidOperationException(message);}
        static void Reject(Action action,string message)
        {bool threw=false;try{action();}catch(InvalidDataException){threw=true;}catch(InvalidOperationException){threw=true;}Check(threw,message);}
        static void Case(string name,Action body)
        {
            try{body();Results.Add(new{id=name,status="PASS"});}
            catch(Exception error){Failed++;Results.Add(new{id=name,status="FAIL",error=error.ToString()});}
        }
        static void RejectedAfter(string name,Action<Trial,Observer.Scope> mutation)
        {
            Case(name,()=>{var t=new Trial();using var s=t.Begin();mutation(t,s);Reject(()=>s.RequireCompleted(true),"Expected rejection");});
        }
        public static int Run()
        {
            Case("success-and-idempotent-cleanup",()=>
            {
                var t=new Trial();var s=t.Begin();t.Success();var w=s.RequireCompleted(true);
                Check(ReferenceEquals(w.Spawn,t.Spawn)&&w.Calls==1&&w.Returns==1&&w.Finalizers==1,"Exact helper witness");
                Check(w.CounterBefore==9&&w.SpawnedId==10&&w.CurrencyBefore==8&&w.CurrencyAfter==7,"Native fact witness");
                Reject(()=>s.RequireCompleted(true),"Repeated publication witness");s.Dispose();s.Dispose();
                Check(s.Authority is null&&s.Action is null&&s.State is null&&s.Spawn is null&&s.Open.Count==0,"Released wrapper roots");
                var next=new Trial();using var again=next.Begin();next.Success();again.RequireCompleted(true);
            });
            RejectedAfter("missing-helper",(t,s)=>{});
            RejectedAfter("native-helper-throws",(t,s)=>
            {var f=t.Prefix();var e=new Exception("original");Check(ReferenceEquals(t.Final(f,e),e),"Original exception identity preserved");Check(s.Calls==1&&s.Returns==0&&s.Finalizers==1,"Failed call counted");});
            RejectedAfter("skipped-helper",(t,s)=>{var f=t.Prefix();t.Body();t.Post(f,false);t.Final(f);});
            RejectedAfter("null-helper-result",(t,s)=>{var f=t.Prefix();t.Body();t.Post(f,explicitNull:true);t.Final(f);});
            RejectedAfter("extra-helper-including-other-state",(t,s)=>
            {t.Success();var f=t.Prefix(new Trial().State);t.Post(f);t.Final(f);Check(s.Calls==2,"All calls counted");});
            RejectedAfter("wrong-first-helper-state",(t,s)=>{var f=t.Prefix(new Trial().State);t.Body();t.Post(f);t.Final(f);});
            RejectedAfter("wrong-thread-helper",(t,s)=>
            {Exception? unexpected=null;var thread=new Thread(()=>{try{var f=t.Prefix();t.Post(f);t.Final(f);}catch(Exception e){unexpected=e;}});thread.Start();thread.Join();Check(unexpected is null&&s.Calls==1,"Foreign-thread callback contained and counted");});
            RejectedAfter("authority-revoked",(t,s)=>{t.Success();t.Revoked=true;});
            RejectedAfter("nested-action-observation",(t,s)=>
            {var nested=new Trial();Reject(()=>nested.Begin(),"Nested Begin rejects");t.Success();Check(s.Failure=="nested-observation","Parent failure latch survives successful helper");});
            RejectedAfter("missing-finalizer",(t,s)=>{var f=t.Prefix();t.Body();t.Post(f);Check(s.Open.Count==1,"Unclosed helper retained");});
            RejectedAfter("duplicate-postfix",(t,s)=>{var f=t.Prefix();t.Body();t.Post(f);t.Post(f);t.Final(f);});
            RejectedAfter("duplicate-finalizer",(t,s)=>{var f=t.Prefix();t.Body();t.Post(f);t.Final(f);t.Final(f);});
            RejectedAfter("changed-cost-zero-retry",(t,s)=>{t.Success();t.Action.Fields["Cost"]=0;});
            RejectedAfter("aborted-action",(t,s)=>{t.Success();t.Action.Fields["trainActionAborted"]=true;});
            RejectedAfter("changed-counter",(t,s)=>{t.Success();t.State.Fields["CurrentUnitId"]=(uint)11;});
            RejectedAfter("wrong-debit",(t,s)=>{t.Success();t.Player.Fields["Currency"]=4;});
            RejectedAfter("wrong-city-result",(t,s)=>{t.Success();t.Tile.Fields["unit"]=new Trial().Spawn;});
            RejectedAfter("wrong-newborn-profile",(t,s)=>{t.Success();t.Spawn.Fields["UnitData"]=new Trial().Data;});
            RejectedAfter("changed-command-context",(t,s)=>{t.Success();t.State.Fields["CurrentCommand"]=(ushort)9;});
            Case("original-did-not-run",()=>{var t=new Trial();using var s=t.Begin();t.Success();Reject(()=>s.RequireCompleted(false),"Outer original flag required");});
            Case("successful-completion-atomically-seals-evidence",()=>
            {
                var t=new Trial();using var s=t.Begin();var f=t.Prefix();t.Body();t.Post(f);t.Final(f);var w=s.RequireCompleted(true);
                var after=t.Prefix();Check(after is null,"No post-seal helper frame");t.Post(f);var e=new Exception("late");
                Check(ReferenceEquals(t.Final(f,e),e)&&s.Calls==1&&s.Returns==1&&s.Finalizers==1&&s.Failure is null,"Late callbacks cannot change sealed evidence");
                Check(ReferenceEquals(w.Spawn,t.Spawn),"Sealed witness retained");
            });
            RejectedAfter("contained-reflection-failure",(t,s)=>
            {t.Data.FailReads=true;var f=t.Prefix();t.Body();t.Post(f);t.Final(f);t.Data.FailReads=false;Check(s.Failure is not null,"Callback read failure latched");});
            Case("late-helper-cannot-taint-next-scope",()=>
            {
                var t=new Trial();var old=t.Begin();var f=t.Prefix();old.Dispose();
                var next=new Trial();using var current=next.Begin();t.Post(f);t.Final(f);next.Success();current.RequireCompleted(true);
                Check(current.Calls==1&&current.Failure is null&&old.Failure is not null,"Late old frame isolated");
            });
            Case("unscoped-callbacks-ignored",()=>
            {var t=new Trial();var f=t.Prefix();Check(f is null,"No unscoped frame");t.Post(f);var e=new Exception("unscoped");Check(ReferenceEquals(t.Final(f,e),e),"Unscoped exception preserved");});
            RejectedAfter("bounded-open-frames-count-all-calls",(t,s)=>
            {var frames=new object?[11];for(int i=0;i<frames.Length;i++)frames[i]=t.Prefix();Check(s.Calls==11&&s.Open.Count==8,"All calls counted, frame retention bounded");foreach(var f in frames)t.Final(f);Check(s.Open.Count==0,"Retained frames retired");});
            Case("ineligible-begin-leaves-no-scope",()=>
            {
                var t=new Trial();t.Tile.Fields["unit"]=t.Spawn;Reject(()=>t.Begin(),"Occupied begins reject");
                var next=new Trial();using var s=next.Begin();next.Success();s.RequireCompleted(true);
            });
            Case("observer-performs-no-fixture-writes",()=>
            {
                var t=new Trial();using var s=t.Begin();Check((uint)t.State.Fields["CurrentUnitId"]! ==9&&(int)t.Player.Fields["Currency"]! ==8&&t.Tile.Fields["unit"] is null,"Begin unchanged");
                var f=t.Prefix();t.Post(f);t.Final(f);Reject(()=>s.RequireCompleted(true),"Successful helper callbacks cannot invent native debit/spawn");
                Check((uint)t.State.Fields["CurrentUnitId"]! ==9&&(int)t.Player.Fields["Currency"]! ==8&&t.Tile.Fields["unit"] is null,"Failed observation unchanged");
            });
            var result=new{scope="Managed callback mocks only; no hooks, permission implementation, IL2CPP or native TrainUnit execution",tests=Results.Count,assertions=Assertions,failed=Failed,results=Results};
            File.WriteAllText(Environment.GetEnvironmentVariable("DCP_PAID_OBSERVATION_RESULT")??throw new InvalidOperationException("Missing result path"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            return Failed==0?0:1;
        }
    }
}
