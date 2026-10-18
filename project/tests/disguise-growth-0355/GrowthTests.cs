using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using Type=Polytopia.Data.UnitData.Type;
namespace Deception.Tests
{
    public static class GrowthTests
    {
        static int Cases,Assertions;
        static void Check(bool condition,string message){Assertions++;if(!condition)throw new Exception(message);}
        static void Case(Action action){Cases++;action();}
        sealed record Rig(GameState Game,UnitState Unit,EndTurnAction Action);
        static LedgerAppearance Appearance(string form="dragonegg",long due=8,ushort health=60)
        {
            ushort maximum=(ushort)(form=="dragonegg"?100:form=="babydragon"?150:200);
            return new(form,new("Elyrion","Default"),new("Imperius","Default"),new("XinXi","Default"),7,health,maximum,false,
                ApparentConcealment.Visible,form=="firedragon"?-1:0,new[]{"Poison"},form=="firedragon"?-1:due);
        }
        static Rig Make(string form="dragonegg",long due=8,ushort health=60,LedgerActualKind kind=LedgerActualKind.Mimic)
        {
            NativeRegroupSession.Enabled=true;NativeRegroupSession.IsReady=true;NativeRegroupSession.PrepareCalls=0;NativeRegroupSession.DuringPrepare=null;NativeRegroupSession.Faults.Clear();
            NativeGameStateEnvelope.Frames.Clear();NativeGameStateEnvelope.Failure="";NativeGameStateEnvelope.Writes=0;
            NativeLedgerState.FailValidation=false;NativeLedgerState.Validations=0;Entry.Notes.Clear();RecruitmentHooks.Reset();
            NativeGrowthSight.Reset();NativeDisguiseGrowthAudio.Recorded.Clear();
            var game=new GameState();NativeRegroupSession.Owned=game;
            string carrier=RecruitmentCatalog.Variant(form);var data=RecruitmentHooks.Definitions[carrier];
            var unit=new UnitState{id=9,type=kind==LedgerActualKind.Mimic?data.type:Type.warrior,UnitData=kind==LedgerActualKind.Mimic?data:new(){type=Type.warrior,health=100}};
            game.Units.Add(unit.id,unit);unit.TypeWrites=unit.DataWrites=0;
            var ledger=new NativeIdentityLedger(Guid.Parse("413a80fa-5900-4bd0-aef8-11e6ec9d15e8"),42,1,
                new[]{new LedgerUnit(10,unit.id,true,kind,true,Appearance(form,due,health),PaidInvestmentKind.Known,3)},Array.Empty<LedgerReceipt>());
            NativeGameStateEnvelope.Set(game,4,NativeIdentityLedgerCodec.Encode(ledger,4));NativeGameStateEnvelope.Writes=0;
            return new(game,unit,new());
        }
        static LedgerAppearance Read(Rig r)=>Ledger(r).Units.Single().Appearance!;
        static NativeIdentityLedger Ledger(Rig r)
        {var s=NativeGameStateEnvelope.Get(r.Game)!;return NativeIdentityLedgerCodec.Decode(s.Bytes(),s.Revision);}
        static string Real(Rig r)=>$"{r.Unit.Pointer}|{r.Unit.id}|{r.Unit.owner}|{r.Unit.health}|{r.Unit.createdTurn}|{r.Unit.xp}|{r.Unit.promotionLevel}|{r.Unit.moved}|{r.Unit.attacked}|{r.Unit.home}|{r.Unit.support}|{r.Unit.coordinates}|{r.Unit.effects.GetHashCode()}|{r.Unit.PaidStars}|{r.Game.Stars}|{r.Game.Score}|{r.Game.VisionRevision}|{r.Game.CurrentUnitId}|{r.Game.CurrentPlayer}|{r.Game.CurrentTurn}";
        static void Before(Rig r)=>NativeDisguiseGrowth.BeforeEndTurn(r.Action,r.Game,out _);
        static bool Throws(Action call){try{call();return false;}catch{return true;}}
        static void Unchanged(Rig r,byte[] bytes,string real,Type type,object data,string label)
        {
            Check(NativeGameStateEnvelope.Get(r.Game)!.Bytes().SequenceEqual(bytes),label+": ledger was not restored byte-for-byte");
            Check(NativeDisguiseGrowthAudio.Recorded.Count==0,label+": failed growth retained audio");
            Check(Real(r)==real&&r.Unit.type==type&&ReferenceEquals(r.Unit.UnitData,data),label+": native fields were not restored exactly");
        }
        public static int Run()
        {
            try
            {
                Case(()=>{var h=new HarmonyLib.Harmony();NativeDisguiseGrowth.Install(h);Check(h.Hooks.SequenceEqual(new[]{("EndTurnAction.Execute",(string?)"BeforeEndTurn",(string?)"Finished")}),"Wrong growth hook/finalizer");});
                Case(()=>{
                    var r=Make();string real=Real(r);var prior=Ledger(r);Before(r);var a=Read(r);
                    Check(NativeDisguiseGrowthAudio.Recorded.Count==1&&ReferenceEquals(NativeDisguiseGrowthAudio.Recorded[0].Action,r.Action)&&NativeDisguiseGrowthAudio.Recorded[0].Before.Form=="dragonegg"&&NativeDisguiseGrowthAudio.Recorded[0].After.Appearance!.Form=="babydragon","Accepted growth did not stage exactly its phase cue");
                    Check(a.Form=="babydragon"&&a.Health==110&&a.Maximum==150&&a.GrowthTurns==3&&a.GrowthDueTurn==11,"Egg native schedule/wounds were not copied");
                    Check(r.Unit.type==Type.dcp_mimic_form_babydragon&&ReferenceEquals(r.Unit.UnitData,RecruitmentHooks.Definitions[RecruitmentCatalog.Variant("babydragon")]),"Wrong baby carrier");
                    Check(NativeGrowthSight.Stages==1&&NativeGrowthSight.Accepts==1&&NativeGrowthSight.Rollbacks==0&&r.Game.ActionStack.Last() is NativeGrowthSight.Explore{Owner:1,Unit:9,Form:Type.dcp_mimic_form_babydragon},"Growth did not publish sight for final carrier and ending owner");
                    Check(Real(r)==real,"Apparent growth changed true identity/health/readiness/support/owner/investment/score/vision");
                    Check(a.SourceColor==7&&a.TribeVisual.Tribe=="Elyrion"&&a.ClimateVisual.Tribe=="Imperius"&&a.BirthVisual.Tribe=="XinXi"&&a.Effects.SequenceEqual(new[]{"Poison"}),"Copied appearance cosmetics/effects were lost");
                    var after=Ledger(r);Check(after.NextSequence==prior.NextSequence&&after.NextLogicalId==prior.NextLogicalId&&after.Receipts.Length==0&&NativeGameStateEnvelope.Get(r.Game)!.Revision==4,"Growth invented command or identity history");
                    for(int i=0;i<50;i++)Before(r);
                    Check(NativeDisguiseGrowthAudio.Recorded.Count==1,"Duplicate end boundary queued duplicate growth audio");
                    Check(r.Unit.TypeWrites==1&&r.Unit.DataWrites==1&&NativeGameStateEnvelope.Writes==1,"Repeated same owner-end churned state or advanced again");
                    Check(NativeGrowthSight.Stages==1&&r.Game.ActionStack.Count==2,"Repeated same deadline restaged discovery");
                    r.Game.CurrentTurn=11;real=Real(r);Before(r);a=Read(r);
                    Check(a.Form=="firedragon"&&a.Health==160&&a.Maximum==200&&a.GrowthTurns==-1&&a.GrowthDueTurn==-1,"Baby did not become native wounded Fire at correct round");
                    Check(r.Unit.type==Type.dcp_mimic_form_firedragon&&Real(r)==real,"Final phase changed true body");
                    int writes=NativeGameStateEnvelope.Writes;r.Game.CurrentTurn=99;Before(r);Check(NativeGameStateEnvelope.Writes==writes&&Read(r).Form=="firedragon","Adult continued growing");
                });
                Case(()=>{
                    var r=Make(due:9);string real=Real(r);Before(r);
                    Check(Read(r).Form=="dragonegg"&&Read(r).GrowthTurns==1&&Read(r).GrowthDueTurn==9&&r.Unit.TypeWrites==0&&Real(r)==real,"Remaining-one copy grew one end-turn early");
                    Before(r);Check(NativeGameStateEnvelope.Writes==1,"Repeated countdown refresh wrote again");
                    r.Game.CurrentTurn=9;Before(r);Check(Read(r).Form=="babydragon"&&Read(r).GrowthDueTurn==12,"One remaining round did not mature at next owner-end");
                });
                Case(()=>{var r=Make(due:5);Before(r);Check(Read(r).Form=="babydragon"&&Read(r).GrowthDueTurn==11,"Overdue callback caught up through multiple phases");});
                Case(()=>{
                    var r=Make();r.Unit.owner=2;var bytes=NativeGameStateEnvelope.Get(r.Game)!.Bytes();string real=Real(r);Before(r);
                    Check(Read(r).Form=="dragonegg"&&NativeGameStateEnvelope.Writes==0&&Real(r)==real,"Other owner's end turn advanced growing unit");
                    r.Game.CurrentPlayer=2;r.Action.PlayerId=2;Before(r);Check(Read(r).Form=="babydragon","Actual ending owner could not grow its body");
                });
                foreach(var kind in new[]{LedgerActualKind.Mimic,LedgerActualKind.Genuine})foreach(byte ending in new byte[]{1,2})Case(()=>{
                    var r=Make(kind:kind);r.Unit.owner=2;var frame=NativeGameStateEnvelope.Get(r.Game)!;var ledger=Ledger(r);
                    ledger=ledger with{Units=new[]{ledger.Units[0] with{Rogue=new(1,2,7)}}};
                    NativeGameStateEnvelope.Set(r.Game,frame.Revision,NativeIdentityLedgerCodec.Encode(ledger,frame.Revision));NativeGameStateEnvelope.Writes=0;
                    r.Game.CurrentPlayer=ending;r.Action.PlayerId=ending;var stack=r.Game.ActionStack;string real=Real(r);Before(r);
                    Check(Read(r).Form==(ending==1?"babydragon":"dragonegg"),"Rogue growth must follow remembered owner, not commander");
                    Check(NativeGrowthSight.Stages==0&&ReferenceEquals(stack,r.Game.ActionStack)&&stack.Count==1,"Idle Rogue growth cannot grant discovery to either side");
                    Check(NativeRegroupSession.Faults.Count==0&&NativeRegroupSession.IsReady&&Real(r)==real,"Rogue growth does not fault on commander mismatch or alter true facts");
                    Check(Ledger(r).Units[0].Rogue is {Owner:1,Commander:2,AcquiredTurn:7},"Growth does not consume Rogue authority or acquisition history");
                });
                Case(()=>{var r=Make();r.Unit.health=0;Before(r);Check(Read(r).Form=="dragonegg"&&r.Unit.TypeWrites==0&&NativeGameStateEnvelope.Writes==0,"Pending native death grew");});
                foreach(string denial in new[]{"disabled","unready","foreign"})Case(()=>{
                    var r=Make();if(denial=="disabled")NativeRegroupSession.Enabled=false;if(denial=="unready")NativeRegroupSession.IsReady=false;if(denial=="foreign")NativeRegroupSession.Owned=new();
                    NativeDisguiseGrowth.BeforeEndTurn(r.Action,r.Game,out bool entered);
                    Check(!entered&&NativeRegroupSession.PrepareCalls==0&&r.Unit.TypeWrites==0&&NativeGameStateEnvelope.Writes==0&&NativeRegroupSession.Faults.Count==0,"Unadmitted state mutated: "+denial);
                });
                Case(()=>{var r=Make();r.Action.PlayerId=2;Check(Throws(()=>Before(r)),"Wrong actor accepted");Check(NativeRegroupSession.Faults.Count==1&&NativeRegroupSession.PrepareCalls==0&&r.Unit.TypeWrites==0,"Wrong actor reached growth write");});
                Case(()=>{var r=Make();NativeGameStateEnvelope.Frames.Clear();Check(Throws(()=>Before(r))&&r.Unit.TypeWrites==0,"Missing private state was manufactured");});
                Case(()=>{var r=Make();r.Game.Units.Clear();Check(Throws(()=>Before(r))&&NativeGameStateEnvelope.Writes==0,"Missing native body passed growth");});
                Case(()=>{
                    var r=Make(kind:LedgerActualKind.Genuine);string real=Real(r);var originalData=r.Unit.UnitData;Before(r);
                    Check(Read(r).Form=="babydragon"&&Read(r).Health==110&&r.Unit.type==Type.warrior&&ReferenceEquals(originalData,r.Unit.UnitData)&&Real(r)==real,"Maintained genuine disguise promoted the actual unit");
                });
                Case(()=>{
                    var r=Make(due:9);Before(r);var frame=NativeGameStateEnvelope.Get(r.Game)!;byte[] saved=frame.Bytes();
                    NativeGameStateEnvelope.Frames[r.Game]=new(frame.Revision,saved);r.Game.CurrentTurn=9;Before(r);
                    Check(Read(r).Form=="babydragon"&&Read(r).GrowthDueTurn==12,"Copied saved snapshot restarted deadline");
                    var newFrame=NativeGameStateEnvelope.Get(r.Game)!;NativeGameStateEnvelope.Frames[r.Game]=new(newFrame.Revision,newFrame.Bytes());r.Game.CurrentTurn=12;Before(r);
                    Check(Read(r).Form=="firedragon"&&Read(r).Health==160,"Second saved phase failed roundtrip");
                });
                foreach(string fault in new[]{"type-before","type-after","data-before","data-after","envelope-before","envelope-after","validation"})Case(()=>{
                    var r=Make();var bytes=NativeGameStateEnvelope.Get(r.Game)!.Bytes();string real=Real(r);var type=r.Unit.type;object data=r.Unit.UnitData;var queue=r.Game.ActionStack;
                    if(fault.StartsWith("envelope"))NativeGameStateEnvelope.Failure=fault[9..];else if(fault=="validation")NativeLedgerState.FailValidation=true;else r.Unit.Failure=fault;
                    Check(Throws(()=>Before(r)),"Injected fault did not fail: "+fault);
                    Unchanged(r,bytes,real,type,data,fault);Check(NativeRegroupSession.Faults.Count==1,"Fault did not retire session: "+fault);
                    Check(ReferenceEquals(queue,r.Game.ActionStack)&&queue.Count==1,"Failed transaction retained staged discovery: "+fault);
                });
                foreach(string fault in new[]{"stage","accept"})Case(()=>{
                    var r=Make();var bytes=NativeGameStateEnvelope.Get(r.Game)!.Bytes();string real=Real(r);var type=r.Unit.type;object data=r.Unit.UnitData;var queue=r.Game.ActionStack;
                    NativeGrowthSight.Failure=fault;Check(Throws(()=>Before(r)),"Injected sight failure did not fail: "+fault);
                    Unchanged(r,bytes,real,type,data,"sight "+fault);
                    Check(ReferenceEquals(queue,r.Game.ActionStack)&&queue.Count==1&&NativeGrowthSight.Rollbacks==1&&NativeGrowthSight.Accepts==0,"Sight failure retained queue or rolled back after body: "+fault);
                });
                foreach(string invalid in new[]{"missing","form","health","attack","range","defence","data-type"})Case(()=>{
                    var r=Make();string target=RecruitmentCatalog.Variant("babydragon");var data=RecruitmentHooks.Definitions[target];
                    if(invalid=="missing")RecruitmentHooks.Forms.Remove(target);
                    if(invalid=="form")RecruitmentHooks.Forms[target]=RecruitmentHooks.Forms[target] with{NativeId="warrior"};
                    if(invalid=="health")data.health=150;if(invalid=="attack")data.attack=30;if(invalid=="range")data.range=1;if(invalid=="defence")data.defence=30;if(invalid=="data-type")data.type=Type.babydragon;
                    var bytes=NativeGameStateEnvelope.Get(r.Game)!.Bytes();string real=Real(r);var type=r.Unit.type;object beforeData=r.Unit.UnitData;
                    Check(Throws(()=>Before(r)),"Invalid native target accepted: "+invalid);Unchanged(r,bytes,real,type,beforeData,invalid);
                });
                Case(()=>{
                    var r=Make();var before=NativeGameStateEnvelope.Get(r.Game)!.Bytes();var originalData=r.Unit.UnitData;string real=Real(r);var type=r.Unit.type;
                    var second=new UnitState{id=10,type=type,UnitData=originalData};r.Game.Units.Add(10,second);
                    var ledger=Ledger(r);var rows=ledger.Units.Append(ledger.Units[0] with{LogicalId=11,NativeId=10}).ToArray();NativeGameStateEnvelope.Set(r.Game,4,NativeIdentityLedgerCodec.Encode(ledger with{Units=rows},4));before=NativeGameStateEnvelope.Get(r.Game)!.Bytes();second.Failure="data-after";
                    Check(Throws(()=>Before(r)),"Second body fault was not triggered");Unchanged(r,before,real,type,originalData,"multi-unit rollback");Check(second.type==type&&ReferenceEquals(second.UnitData,originalData),"Second body partial write survived rollback");
                });
                Case(()=>{
                    var r=Make();NativeRegroupSession.DuringPrepare=()=>Before(r);Check(Throws(()=>Before(r)),"Reentrant turn admitted");
                    Check(r.Unit.TypeWrites==0&&NativeGameStateEnvelope.Writes==0,"Reentrant event wrote state");
                    NativeRegroupSession.DuringPrepare=null;NativeRegroupSession.IsReady=true;Before(r);Check(Read(r).Form=="babydragon","Reentry failure left thread latch permanently active");
                });
                Case(()=>{var r=Make();Before(r);var error=new InvalidOperationException("native end failed");var bytes=NativeGameStateEnvelope.Get(r.Game)!.Bytes();Check(ReferenceEquals(NativeDisguiseGrowth.Finished(error,true,r.Action,r.Game),error)&&NativeRegroupSession.Faults.Count==1,"Native failure was suppressed");Check(NativeDisguiseGrowthAudio.Recorded.Count==0,"Native failure retained queued audio");Check(NativeGameStateEnvelope.Get(r.Game)!.Bytes().SequenceEqual(bytes)&&Read(r).Form=="babydragon","Finalizer undid completed pre-end growth");});
                Case(()=>{var r=Make();var error=new Exception("unrelated");Check(ReferenceEquals(NativeDisguiseGrowth.Finished(error,false),error)&&NativeRegroupSession.Faults.Count==0,"Unadmitted finalizer faulted Local state");});
                foreach(string vessel in new[]{"transportship","rammership"})Case(()=>{
                    var r=Make();var cargo=r.Unit;var prior=Read(r);var beforeReal=Real(r);
                    var boat=new UnitState{id=11,type=Enum.Parse<Type>(RecruitmentCatalog.Variant(vessel)),passengerUnit=cargo};
                    boat.UnitData=new(){type=boat.type};r.Game.Units.Add(boat.id,boat);
                    var appearance=prior with{Form=vessel,Health=60,Maximum=100,GrowthTurns=-1,GrowthDueTurn=-1,Passenger=prior};
                    var ledger=Ledger(r);ledger=ledger with{Units=new[]{ledger.Units[0] with{NativeId=boat.id,Appearance=appearance,Transport=new(cargo.id)}}};
                    NativeGameStateEnvelope.Set(r.Game,4,NativeIdentityLedgerCodec.Encode(ledger,4));
                    Before(r);var a=Read(r);
                    Check(a.Form==vessel&&a.Passenger!.Form=="dragonegg"&&a.Passenger.GrowthDueTurn==8,"Cargo retains original due round without hatching while aboard");
                    Check(cargo.type==Type.dcp_mimic_form_dragonegg&&cargo.health==10&&boat.health==10,"Cargo remains actual Mimic and does not grant real HP");
                    Check(a.Maximum==100&&a.Health==60,"Hidden cargo does not change apparent vessel health while aboard");
                    Check(NativeGrowthSight.Stages==0&&Real(r)==beforeReal,"Cargo growth does not create a second sight source or reset true facts");
                });
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,idleBoundaries=50,scope="synthetic-native-growth-boundary"}));return 0;
            }
            catch(Exception error){Console.WriteLine(error);return 1;}
        }
    }
}
