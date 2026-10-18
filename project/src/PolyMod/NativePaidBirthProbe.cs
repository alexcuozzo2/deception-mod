using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using B=Deception.PolyModAdapter.NativeExecutionBridge;
using Report=Deception.PolyModAdapter.NativeStateProbe.Report;
using Claim=Deception.PolyModAdapter.NativeGameStateEnvelope.PrivateClaim;

namespace Deception.PolyModAdapter
{
    // DRAFT: exact private native purchases only. The canonical intent lives in
    // this invocation; no Train-v1 transport, resumed action, or live purchase
    // is claimed. Parent partial supplies isolation, bridge and retirement.
    internal static partial class NativeExecutionProbe
    {
        enum PaidFault { None,Observer,BeforePublish,WrongCounter }
        [ThreadStatic] static PaidInvocation? PaidCurrent;
        [ThreadStatic] static PaidValidation? PaidValidating;
        static bool PaidAttempted;
        static readonly string[] PaidRules={NativePaidBirthIntentCodec.Warrior,NativePaidBirthIntentCodec.Rider};

        internal static bool PaidReady(object state,out string reason)
        {
            reason="Paid-birth source is not ready.";
            try
            {
                if(Native.Int(Native.Get(state,"Version"))!=Version){reason="Native version differs.";return false;}
                object player=Player(state,Actor(state));
                if(!RecruitmentHooks.OwnsDeception(player)){reason="Research Deception using the native tree.";return false;}
                var roster=RecruitmentHooks.NativeRoster(state,player);
                if(!roster.Contains("warrior")||!roster.Contains("rider")){reason="Both native Warrior and Rider must be unlocked.";return false;}
                if(Native.Int(Native.Get(player,"Currency"))<RecruitmentCatalog.Cost){reason="Earn at least one native star.";return false;}
                if(PaidCity(state,Actor(state)) is null){reason="Move the native unit out of an explored owned city before the paid trial.";return false;}
                reason="";return true;
            }
            catch(Exception e){reason="Paid readiness inspection failed: "+e.GetType().Name+": "+e.Message;return false;}
        }
        static object? PaidCity(object state,byte actor)=>Tiles(state).FirstOrDefault(tile=>
            Native.Int(Native.Get(tile,"owner"))==actor&&City(tile)&&Explored(tile,actor)&&Native.Get(tile,"unit") is null&&Items(tile,"effects").Length==0);

        sealed class PaidValidation:IDisposable
        {
            internal readonly PaidInvocation Call;
            internal readonly object Carrier;
            internal PaidValidation(PaidInvocation call,object carrier)
            {Require(PaidValidating is null&&ReferenceEquals(PaidCurrent,call),"Nested paid native validity query.");Call=call;Carrier=carrier;PaidValidating=this;}
            public void Dispose(){Require(ReferenceEquals(PaidValidating,this),"Paid validity scope replaced.");PaidValidating=null;}
        }
        internal static bool AllowPaidEligibility(object state,object carrier)
        {
            var permit=PaidValidating;
            if(permit is null||!ReferenceEquals(PaidCurrent,permit.Call)||!Native.Same(state,permit.Call.Purchase.State)||!Native.Same(carrier,permit.Carrier))return false;
            permit.Call.RequireCurrent();return true;
        }
        internal static object? PaidUnlockState(string rule,object player)
        {
            var permit=PaidValidating;
            if(permit is null)return null;
            Require(ReferenceEquals(PaidCurrent,permit.Call)&&rule==permit.Call.Purchase.Intent.NativeRuleId&&Native.Same(player,permit.Call.Purchase.Player),
                "Custom unlock query differs from the active private validity scope.");
            permit.Call.RequireCurrent();return permit.Call.Purchase.State;
        }

        // Read-only witnesses intentionally avoid all GameState serializers once
        // the claim exists. Whole postbirth state/transport is NOT proven here.
        sealed class PaidWitness
        {
            readonly object State,CityTile,PayingPlayer,Stack;
            readonly (object Unit,byte[] Bytes)[] Bodies;
            readonly (object Tile,byte[] Bytes)[] OtherTiles;
            readonly (object Player,byte[] Bytes)[] OtherPlayers;
            readonly (object Carrier,string Type,byte[] Bytes)[] Commands,Prefix;
            readonly object[] PlayerOrder,TileOrder;
            readonly (string Name,object? Value)[] StateScalars,CityScalars,PlayerScalars;
            readonly object? ImprovementObject,ResourceObject;
            readonly byte[] SettingsBytes,ImprovementBytes,ResourceBytes;
            readonly object Settings,Map,CommandList,PlayerList;
            readonly int Currency;
            readonly uint Counter;
            readonly long[] RootPointers;
            static readonly string[] StateFields={"Version","Seed","VillageNameSeed","CurrentTurn","CurrentPlayerIndex","CurrentCommand","CurrentState","HasFlaggedNeedForUpdateRoutes"};
            static readonly string[] CityFields={"terrain","climate","_skin","altitude","owner","capitalOf","hasRoad","hasRoute","hadRoute"};
            static readonly string[] PlayerFields={"Id","tribe","tribeMix","_climate","skinType","color","score","endScore","cities","kills","casualities","wipeOuts","blockTrainUnits"};
            static (string,object?)[] Scalars(object value,string[] names)=>names.Select(name=>(name,Native.Get(value,name))).ToArray();
            static void EqualScalars(object value,(string Name,object? Value)[] expected)
            {foreach(var pair in expected)Require(Equals(pair.Value,Native.Get(value,pair.Name)),"Unapproved native scalar changed: "+pair.Name);}
            static byte[] Record(object value,string type)=>NativeStateProbe.Serialize(value,type,Version);
            static (object,string,byte[]) RecordCarrier(object value)
            {
                // Actual native factories support ordinary research/move/turn
                // history too. No guessed enum-name-to-class mapping, concrete
                // IL2CPP generic instantiation or enclosing history write.
                string type=Native.T("CommandBase").IsInstanceOfType(value)?"CommandBase":"ActionBase";
                return(value,type,CarrierBytes(value,type));
            }
            static byte[] CarrierBytes(object value,string type)
            {
                if(type=="CommandBase")return NativeStateProbe.ManagedBytes(Native.Invoke(Native.M("CommandBase","ToByteArray","CommandBase","System.Int32"),null,value,Version)!);
                using var buffer=new NativeStateProbe.Buffer();
                Native.Invoke(Native.M("GameState","SerializeAction","ActionBase","Il2CppSystem.IO.BinaryWriter","System.Int32"),null,value,buffer.W,Version);
                return buffer.Snapshot();
            }
            internal PaidWitness(PaidPurchase p)
            {
                State=p.State;CityTile=p.City;PayingPlayer=p.Player;Stack=Native.Get(State,"ActionStack")!;
                Settings=Native.Get(State,"Settings")!;Map=Native.Get(State,"Map")!;CommandList=Native.Get(State,"CommandStack")!;PlayerList=Native.Get(State,"PlayerStates")!;
                RootPointers=Roots(State).Select(Native.Pointer).ToArray();
                Bodies=NativeLedgerState.Graph(State).OrderBy(x=>x.Key).Select(x=>(x.Value.Unit,Record(x.Value.Unit,"UnitState"))).ToArray();
                TileOrder=Tiles(State);OtherTiles=TileOrder.Where(t=>!Native.Same(t,CityTile)).Select(t=>(t,Record(t,"TileData"))).ToArray();
                PlayerOrder=Items(State,"PlayerStates",16);OtherPlayers=PlayerOrder.Where(x=>!Native.Same(x,PayingPlayer)).Select(x=>(x,Record(x,"PlayerState"))).ToArray();
                Commands=Items(State,"CommandStack",16384).Select(RecordCarrier).ToArray();Prefix=Items(State,"ActionStack").Select(RecordCarrier).ToArray();
                StateScalars=Scalars(State,StateFields);CityScalars=Scalars(CityTile,CityFields);PlayerScalars=Scalars(PayingPlayer,PlayerFields);
                SettingsBytes=Record(Settings,"GameSettings");ImprovementObject=Native.Get(CityTile,"improvement");ResourceObject=Native.Get(CityTile,"resource");
                ImprovementBytes=ImprovementObject is null?Array.Empty<byte>():Record(ImprovementObject,"ImprovementState");
                ResourceBytes=ResourceObject is null?Array.Empty<byte>():Record(ResourceObject,"ResourceState");
                Currency=Native.Int(Native.Get(PayingPlayer,"Currency"));Counter=Convert.ToUInt32(Native.Get(State,"CurrentUnitId"));
            }
            internal object[] PrefixObjects=>Prefix.Select(x=>x.Carrier).ToArray();
            internal object StackObject=>Stack;
            internal void Verify(PaidPurchase p,bool birth,bool noQueueChange=false)
            {
                Require(Native.Same(Settings,Native.Get(State,"Settings"))&&Native.Same(Map,Native.Get(State,"Map"))&&Native.Same(CommandList,Native.Get(State,"CommandStack"))&&Native.Same(PlayerList,Native.Get(State,"PlayerStates")),"Native state roots changed.");
                Require(Tiles(State).Select(Native.Pointer).SequenceEqual(TileOrder.Select(Native.Pointer))&&Items(State,"PlayerStates",16).Select(Native.Pointer).SequenceEqual(PlayerOrder.Select(Native.Pointer)),"Native tile/player order changed.");
                EqualScalars(State,StateScalars);EqualScalars(CityTile,CityScalars);EqualScalars(PayingPlayer,PlayerScalars);
                Require(Record(Settings,"GameSettings").SequenceEqual(SettingsBytes),"Native settings changed.");
                Require(Same(ImprovementObject,Native.Get(CityTile,"improvement"))&&Same(ResourceObject,Native.Get(CityTile,"resource")),"City improvement/resource identity changed.");
                if(ImprovementObject is not null)Require(Record(ImprovementObject,"ImprovementState").SequenceEqual(ImprovementBytes),"City improvement changed before children ran.");
                if(ResourceObject is not null)Require(Record(ResourceObject,"ResourceState").SequenceEqual(ResourceBytes),"City resource changed.");
                foreach(var row in OtherTiles)Require(Record(row.Tile,"TileData").SequenceEqual(row.Bytes),"An unrelated tile changed.");
                foreach(var row in OtherPlayers)Require(Record(row.Player,"PlayerState").SequenceEqual(row.Bytes),"Another player's native record changed.");
                var graph=NativeLedgerState.Graph(State);
                foreach(var row in Bodies)Require(graph.Values.Any(x=>Native.Same(x.Unit,row.Unit))&&Record(row.Unit,"UnitState").SequenceEqual(row.Bytes),"Existing body identity or native bytes changed.");
                Require(graph.Count==Bodies.Length+(birth?1:0),"Unexpected native body count.");
                Require(Native.Int(Native.Get(PayingPlayer,"Currency"))==Currency-(birth?RecruitmentCatalog.Cost:0)&&Convert.ToUInt32(Native.Get(State,"CurrentUnitId"))==Counter+(birth?1u:0u),"Native currency/counter changed outside the exact birth.");
                object[] commands=Items(State,"CommandStack",16384);Require(commands.Length==Commands.Length,"Native command history grew during direct execution.");
                for(int i=0;i<Commands.Length;i++)Require(Native.Same(commands[i],Commands[i].Carrier)&&CarrierBytes(commands[i],Commands[i].Type).SequenceEqual(Commands[i].Bytes),"Native command history changed.");
                object[] tail=Tail(State,Stack,PrefixObjects);if(noQueueChange)Require(tail.Length==0,"Unexpected queue append.");
                foreach(var row in Prefix)Require(CarrierBytes(row.Carrier,row.Type).SequenceEqual(row.Bytes),"Queued prefix record changed.");
                // Original roots must survive; new body/effects/action objects
                // are allowed. This is not a complete scalar-state serializer.
                var now=Roots(State).Select(Native.Pointer).ToHashSet();Require(RootPointers.All(now.Contains),"An original mutable root disappeared.");
            }
        }

        sealed class PaidPurchase:NativePaidBirthObservation.IAuthority
        {
            internal readonly Trial Trial;
            internal readonly Report Report;
            internal readonly object State,Player,City,Data,Command;
            internal readonly Claim Claim;
            internal readonly NativePaidBirthIntent Intent;
            internal readonly byte[] IntentBytes;
            internal readonly PaidFault Fault;
            internal object? Action;
            internal bool Faulted,Committed;
            internal PaidPurchase(Trial trial,Report report,string rule,PaidFault fault)
            {
                Trial=trial;Report=report;Fault=fault;State=trial.Clone();byte actor=Actor(State);Player=PlayerOf(State,actor);
                City=PaidCity(State,actor)??throw new InvalidDataException("Copied native city is no longer empty/eligible.");Data=RecruitmentHooks.Data(State,rule);
                Require(Native.Int(Native.Get(Data,"cost"))==RecruitmentCatalog.Cost&&Native.Int(Native.Get(Data,"health"))==10,"Registered Mimic cost/health differs.");
                Require(Items(State,"ActionStack").Length==0&&Items(State,"pendingCommandTriggers").Length==0,"Private purchase requires an idle native queue.");
                Command=Carrier(trial,2,actor,Native.Get(City,"coordinates")!,rule);
                Claim=trial.Claim(State);var snapshot=Claim.Read(State);var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);var at=At(Native.Get(City,"coordinates")!);
                uint counter=Convert.ToUInt32(Native.Get(State,"CurrentUnitId"));Require(counter<uint.MaxValue-1,"Native counter bound.");
                Intent=new(ledger.Epoch,ledger.NextSequence,snapshot.Revision,ledger.NextLogicalId,Convert.ToUInt32(Native.Get(State,"CurrentTurn")),actor,rule,at.X,at.Y,counter+(fault==PaidFault.WrongCounter?1u:0u),RecruitmentCatalog.Cost);
                IntentBytes=NativePaidBirthIntentCodec.Encode(Intent);
            }
            static object PlayerOf(object state,byte actor)=>NativeExecutionProbe.Player(state,actor);
            internal void RequireOwned()
            {
                Require(!Faulted&&ReferenceEquals(Active,Trial)&&Trial.Thread==Environment.CurrentManagedThreadId,"Paid purchase authority expired or crossed thread.");
                _=Trial.Registration(State);_=Claim.Read(State);
                Require(NativePaidBirthIntentCodec.Encode(Intent).SequenceEqual(IntentBytes),"Canonical intent changed.");
            }
            internal void Canonical(object carrier,uint site)
            {
                RequireOwned();Require(site is 2 or 3&&Native.Same(carrier,site==2?Command:Action),"Paid action is not the exact command/appended action.");
                var at=At(Native.Get(carrier,"Coordinates")!);
                Require(Native.EnumName(Native.Get(carrier,"Type")!)==Intent.NativeRuleId&&Native.Int(Native.Get(carrier,"PlayerId"))==Intent.Actor&&at.X==Intent.CityX&&at.Y==Intent.CityY,"Native carrier differs from canonical intent.");
                if(site==3)Require(Native.Int(Native.Get(carrier,"Cost"))==RecruitmentCatalog.Cost&&!(bool)Native.Get(carrier,"trainActionAborted")!,"Unsupported occupied/zero-cost/aborted training action.");
            }
            internal bool Duplicate()
            {var snapshot=Claim.Read(State);return NativePaidBirthLedger.AlreadyCommitted(NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision),snapshot.Revision,Intent);}
            internal void Fresh(PaidInvocation call)
            {
                Require(Convert.ToUInt32(Native.Get(State,"CurrentTurn"))==Intent.Turn&&Actor(State)==Intent.Actor&&Convert.ToUInt32(Native.Get(State,"CurrentUnitId"))==Intent.ExpectedNativeCounter,"Fresh paid context/counter changed.");
                Require(Native.Get(City,"unit") is null&&Native.Int(Native.Get(City,"owner"))==Intent.Actor&&CityTest(City)&&Explored(City,Intent.Actor)&&Items(City,"effects").Length==0,"Fresh paid city is occupied or unsupported.");
                using var validity=new PaidValidation(call,Command);
                Require(RecruitmentHooks.NativeValid(State,Command,out string reason),"Native training rejected: "+reason);
            }
            static bool CityTest(object tile)=>NativeExecutionProbe.City(tile);
            public void RequirePrivateAction(object permit,object action,object state,object player,object tile,object data)
            {
                Require(ReferenceEquals(permit,this)&&PaidCurrent is PaidInvocation call&&ReferenceEquals(call.Purchase,this)&&call.Site==3&&call.Prepared==1&&call.Cleaned==0,
                    "Native helper is outside its exact paid invocation.");
                PaidCurrent!.RequireCurrent();Canonical(action,3);
                Require(Native.Same(state,State)&&Native.Same(player,Player)&&Native.Same(tile,City)&&Native.Same(data,Data),"Native helper authority arguments changed.");
            }
            internal void Quarantine(){Faulted=true;Claim.Quarantine();}
        }

        sealed class PaidInvocation:IDisposable
        {
            internal readonly PaidPurchase Purchase;
            internal readonly object Carrier;
            internal readonly uint Site;
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal int Prepared,Finished,Cleaned;
            internal bool? OriginalRan;
            internal B.Outcome? Outcome;
            internal B.Decision Decision=B.Decision.Reject;
            internal PaidInvocation(PaidPurchase p,uint site,object? carrier=null)
            {Require(PaidCurrent is null&&Current is null,"Reentrant paid execution.");Purchase=p;Site=site;Carrier=carrier??(site==2?p.Command:p.Action??throw new InvalidDataException("Missing appended action."));PaidCurrent=this;}
            internal void RequireCurrent()
            {Require(ReferenceEquals(PaidCurrent,this)&&Thread==Environment.CurrentManagedThreadId&&Cleaned==0,"Paid invocation identity/thread differs.");Purchase.RequireOwned();}
            public void Dispose(){Require(ReferenceEquals(PaidCurrent,this),"Paid invocation scope replaced.");PaidCurrent=null;}
        }

        sealed class PaidOperation:B.IOperation
        {
            readonly PaidInvocation Call;
            readonly PaidPurchase P;
            PaidWitness? Before;
            NativePaidBirthPreparation? PreparedLedger;
            Claim.Staged? Stage;
            NativePaidBirthObservation.Scope? Observation;
            LedgerAppearance? Appearance;
            object[] Exploration=Array.Empty<object>();
            internal PaidOperation(PaidInvocation call){Call=call;P=call.Purchase;}
            internal B.Decision Prepare()
            {
                Call.RequireCurrent();P.Canonical(Call.Carrier,Call.Site);
                if(P.Duplicate()){Before=new(P);return Call.Decision=B.Decision.Handled;}
                P.Fresh(Call);Before=new(P);
                if(Call.Site==3)
                {
                    var snapshot=P.Claim.Read(P.State);var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
                    Appearance=NativePaidBirthAppearance.Prepare(P.State,P.Player,P.Intent.NativeRuleId);
                    var evidence=new NativePaidBirthBefore(Version,Convert.ToUInt32(Native.Get(P.State,"CurrentTurn")),Actor(P.State),P.Intent.NativeRuleId,P.Intent.CityX,P.Intent.CityY,
                        Convert.ToByte(Native.Get(P.City,"owner")),City(P.City),Native.Get(P.City,"unit") is null,true,Convert.ToUInt32(Native.Get(P.State,"CurrentUnitId")),
                        Native.Int(Native.Get(P.Player,"Currency")),Native.Int(Native.Get(P.Data,"cost")),Appearance.Maximum,NativeLedgerState.Graph(P.State).Keys.ToArray(),Appearance);
                    PreparedLedger=NativePaidBirthLedger.Prepare(ledger,snapshot.Revision,P.Intent,evidence);
                    Stage=P.Claim.Stage(P.State,PreparedLedger.AfterRevision,PreparedLedger.StagedCopy());
                    Exploration=UnexploredFootprint(P.State,P.City,P.Data,P.Intent.Actor);
                    Observation=NativePaidBirthObservation.Begin(P,P,Call.Carrier,P.State,P.Player,P.City,P.Data,P.Intent.NativeRuleId);
                }
                return Call.Decision=B.Decision.Pass;
            }
            public void Finish(bool originalRan)
            {
                Call.RequireCurrent();Require(Call.Prepared==1&&Call.Finished==0&&Call.Cleaned==0,"Paid Finish order differs.");Call.Finished++;Call.OriginalRan=originalRan;
                Require(originalRan==(Call.Decision==B.Decision.Pass),"Paid original-ran differs.");
                if(Call.Decision==B.Decision.Handled){Before!.Verify(P,false,true);Require(P.Duplicate(),"Duplicate receipt disappeared.");return;}
                if(Call.Site==2)
                {
                    Before!.Verify(P,false);object[] commandTail=Tail(P.State,Before.StackObject,Before.PrefixObjects);Require(commandTail.Length==3,"Native command did not append exactly three children.");
                    object end=Native.As(commandTail[0],"EndCommandAction"),deferred=Native.As(commandTail[1],"RunDeferredAction"),action=Native.As(commandTail[2],"TrainAction");
                    Require(new[]{end,deferred,action}.All(x=>Native.Int(Native.Get(x,"PlayerId"))==P.Intent.Actor)&&Equals(Native.Get(end,"commandType"),Native.Invoke(Native.M("CommandBase","GetCommandType"),P.Command))&&
                        Native.EnumName(Native.Get(action,"Type")!)==P.Intent.NativeRuleId&&At(Native.Get(action,"Coordinates")!)==At(Native.Get(P.City,"coordinates")!)&&Native.Int(Native.Get(action,"Cost"))==RecruitmentCatalog.Cost,"Command-to-action native fields differ.");
                    Require(P.Action is null,"Native action was already assigned.");P.Action=P.Trial.Own(action);P.Trial.Collect(P.State);
                    Check(P.Report,true,P.Intent.NativeRuleId+"-native-command-exact-appended-action-no-debit");return;
                }
                Require(Observation is not null&&PreparedLedger is not null&&Stage is not null&&Appearance is not null,"Missing pre-original paid preparation.");
                if(P.Fault==PaidFault.Observer)Observation!.Fail("intentional-private-observer-fault");
                var helper=Observation!.RequireCompleted(originalRan);object spawned=helper.Spawn;
                Before!.Verify(P,true);var graph=NativeLedgerState.Graph(P.State);uint id=Convert.ToUInt32(Native.Get(spawned,"id"));Require(graph.TryGetValue(id,out var body)&&body.Root&&Native.Same(body.Unit,spawned),"Observed newborn is not the graph root.");
                NativePaidBirthAppearance.Verify(P.State,body!,P.Intent.NativeRuleId,Appearance!);
                object[] tail=Tail(P.State,Before.StackObject,Before.PrefixObjects);Require(tail.Length==Exploration.Length+1,"Unexpected paid action children.");
                for(int i=0;i<Exploration.Length;i++)
                {object child=Native.As(tail[i],"ExploreAction");Require(Native.Int(Native.Get(child,"PlayerId"))==P.Intent.Actor&&At(Native.Get(child,"Coordinates")!)==At(Native.Get(Exploration[i],"coordinates")!),"Native sight action/order differs.");}
                object score=Native.As(tail[^1],"IncreaseScoreAction");int scoreValue=Native.Int(Native.Invoke(Native.M("ScoreSheet","GetUnitScore","UnitState","GameState"),null,spawned,P.State));
                Require(scoreValue==RecruitmentCatalog.Cost*5&&Native.Int(Native.Get(score,"Amount"))==scoreValue&&Native.Int(Native.Get(score,"PlayerId"))==P.Intent.Actor&&Native.Int(Native.Get(score,"Delay"))==0&&At(Native.Get(score,"Source")!)==At(Native.Get(P.City,"coordinates")!),"Native paid Mimic score child differs.");
                object[] local=Items(Call.Carrier,"subActions");Require(local.Length==1&&Native.Same(local[0],score),"Native TrainAction local subaction list differs.");
                var at=At(Native.Get(spawned,"coordinates")!);var home=At(Native.Get(spawned,"home")!);
                bool single=Native.Get(spawned,"passengerUnit") is null&&!(bool)Native.Invoke(Native.M("UnitState","HasLeader"),spawned)!&&!(bool)Native.Invoke(Native.M("UnitState","HasFollower"),spawned)!&&Items(spawned,"effects").Length==0;
                var newborn=new NativePaidBirthBody(id,Convert.ToByte(Native.Get(spawned,"owner")),Native.EnumName(Native.Get(spawned,"type")!),Native.EnumName(Native.Get(Native.Get(spawned,"UnitData")!,"type")!),at.X,at.Y,home.X,home.Y,
                    Convert.ToUInt16(Native.Get(spawned,"health")),Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMaxHealth","UnitState","GameState"),null,spawned,P.State)),Native.Int(Native.Get(spawned,"promotionLevel")),
                    Native.Int(Native.Get(P.Data,"attack")),Native.Int(Native.Get(P.Data,"defence")),Native.Int(Native.Get(P.Data,"range")),body!.Root,single,(bool)Native.Get(spawned,"moved")!,(bool)Native.Get(spawned,"attacked")!);
                var after=new NativePaidBirthAfter(true,(bool)Native.Get(Call.Carrier,"trainActionAborted")!,checked((int)helper.Returns),true,Convert.ToUInt32(Native.Get(P.State,"CurrentTurn")),Actor(P.State),Native.Int(Native.Get(Call.Carrier,"Cost")),
                    Convert.ToUInt32(Native.Get(P.State,"CurrentUnitId")),Native.Int(Native.Get(P.Player,"Currency")),newborn,graph.Keys.ToArray(),true,true);
                var current=P.Claim.Read(P.State);var result=NativePaidBirthLedger.Complete(PreparedLedger!,NativeIdentityLedgerCodec.Decode(current.Bytes(),current.Revision),current.Revision,after);
                NativeLedgerState.Validate(P.State,result.AfterRevision,result.Bytes());
                if(P.Fault==PaidFault.BeforePublish)throw new InvalidDataException("Intentional paid prepublication failure.");
                P.Claim.Publish(P.State,Stage!,result.AfterRevision,result.Bytes());var installed=P.Claim.Read(P.State);
                Require(installed.Revision==1&&installed.Bytes().SequenceEqual(result.Bytes())&&P.Duplicate(),"Exact paid receipt publication differs.");P.Committed=true;P.Trial.Collect(P.State);
                Check(P.Report,true,P.Intent.NativeRuleId+"-native-paid1-body-home-exhaustion-profile-sight-score-known1-receipt");
            }
            public void Cleanup(B.Outcome outcome)
            {
                Require(ReferenceEquals(PaidCurrent,Call)&&Call.Cleaned==0&&Call.Thread==Environment.CurrentManagedThreadId,"Paid cleanup identity/order differs.");
                Call.Cleaned++;Call.Outcome=outcome;
                try{Observation?.Dispose();}finally{if(outcome!=B.Outcome.Complete)P.Quarantine();}
            }
        }
        internal static bool TryPreparePaid(B.Call call,object carrier,out B.Decision decision,out B.IOperation? operation)
        {
            decision=B.Decision.Reject;operation=null;var scope=PaidCurrent;if(scope is null)return false;
            try
            {
                scope.RequireCurrent();Require(scope.Prepared==0&&call.Site==scope.Site&&call.State.ToInt64()==Native.Pointer(scope.Purchase.State)&&call.Instance.ToInt64()==Native.Pointer(scope.Carrier)&&Native.Same(carrier,scope.Carrier),"Paid native call differs from its exact invocation.");
            }
            catch{scope.Purchase.Quarantine();throw;}
            // A rejected nested/different native call must never borrow the
            // enclosing invocation's handler, counters or observer cleanup.
            var handler=new PaidOperation(scope);operation=handler;scope.Prepared=1;
            decision=handler.Prepare();return true;
        }
        static void PaidCall(PaidPurchase p,uint site,string label,bool error=false)
        {
            p.Trial.VerifyPublished();var before=Count();int notifications;lock(p.Trial.Notifications)notifications=p.Trial.Notifications.Count;
            using var invocation=new PaidInvocation(p,site);Exception? exception=Execute(p.State,invocation.Carrier,site);
            Failure(p.Report,label,exception,error);bool finished=p.Fault!=PaidFault.WrongCounter;
            VerifyCounts(p.Report,before,label,finished);
            B.Outcome expected=!error?B.Outcome.Complete:p.Fault==PaidFault.WrongCounter?B.Outcome.PrepareRejected:B.Outcome.FinishRejected;
            Check(p.Report,invocation.Prepared==1&&invocation.Finished==(finished?1:0)&&invocation.Cleaned==1&&invocation.Outcome==expected,label+"-exact-phases-and-cleanup");
            Check(p.Report,finished?invocation.OriginalRan==(invocation.Decision==B.Decision.Pass):invocation.OriginalRan is null,label+"-raw-original-evidence");
            if(error)
            {
                string stage=p.Fault==PaidFault.WrongCounter?"prepare":"finish";
                string reason=p.Fault switch
                {
                    PaidFault.WrongCounter=>"InvalidDataException: Fresh paid context/counter changed.",
                    PaidFault.Observer=>"InvalidDataException: Private paid-birth observation: Helper evidence failed: intentional-private-observer-fault",
                    PaidFault.BeforePublish=>"InvalidDataException: Intentional paid prepublication failure.",
                    _=>throw new InvalidDataException("Unspecified expected paid failure.")
                };
                lock(p.Trial.Notifications)
                    Check(p.Report,p.Trial.Notifications.Skip(notifications).Count(x=>x.Site==site&&x.Stage==stage&&x.Reason==reason)==1,label+"-exact-intended-rejection-reason");
            }
            p.Trial.VerifyPublished();
        }
        internal static Report RunPaid(object suppliedSource,int serializerVersion)
        {
            var report=new Report{Scope="detached-native-paid-mimic-birth",Limit="Private in-memory Warrior/Rider purchases only, using real source research/funds and an already empty city. No synthetic research or relocation. Native commands/actions run directly; no child is drained. Canonical intent is invocation-owned, not persisted Train-v1 transport. Exact old bodies/queue/other tiles/other players/settings and selected scalar fields are compared; paying-player/city/full postbirth GameState serialization is NOT proven. No postbirth GameState save/copy/load, live purchase, occupied retry, death or client publication is attempted. Expected post-original failures quarantine the mutated private state; no rollback/refund is claimed."};
            Trial? trial=null;
            try
            {
                Require(serializerVersion==Version&&B.Installed&&B.ActiveOperations==0&&Current is null&&PaidCurrent is null&&PaidValidating is null,"Paid bridge/scope is not ready.");
                Require(NativeFreshSession.TryGetExecutionSource(out var source,out string why)&&Native.Same(source,suppliedSource),"Fresh paid source unavailable: "+why);
                Require(PaidReady(suppliedSource,out why),why);
                lock(Gate){Require(!PaidAttempted&&!Attempted&&Active is null,"Paid trial is once per process and exclusive with ordinary probe.");PaidAttempted=true;}
                trial=new Trial(suppliedSource,report);Active=trial;
                foreach(string rule in PaidRules)
                {
                    var p=new PaidPurchase(trial,report,rule,PaidFault.None);PaidCall(p,2,rule+"-command");PaidCall(p,3,rule+"-action");
                    Require(p.Committed,"Paid action did not commit.");PaidCall(p,2,rule+"-duplicate-command");PaidCall(p,3,rule+"-duplicate-action");
                    Check(report,true,rule+"-duplicates-before-occupied-funds-native-checks-no-debit-helper-or-queue");
                }
                foreach(var fault in new[]{PaidFault.WrongCounter,PaidFault.Observer,PaidFault.BeforePublish})
                {
                    var p=new PaidPurchase(trial,report,NativePaidBirthIntentCodec.Warrior,fault);
                    var before=new PaidWitness(p);
                    PaidCall(p,2,"paid-"+fault+"-command",fault==PaidFault.WrongCounter);
                    if(fault!=PaidFault.WrongCounter){before=new PaidWitness(p);PaidCall(p,3,"paid-"+fault+"-action",true);before.Verify(p,true);}
                    else before.Verify(p,false,true);
                    Check(report,p.Faulted&&!p.Committed,"paid-"+fault+"-quarantined-without-publication");
                }
                trial.VerifyPublished();Check(report,!trial.NotificationOverflow&&B.ActiveOperations==0,"paid-final-published-source-and-operation-isolation");
                Measure(report,"source-frame-sha256",Hash(trial.SourceBytes));Measure(report,"native-error-notifications",trial.Notifications.Count);
                Measure(report,"postbirth-state-transport","NOT RUN; every claimed receiver remains forbidden to public serializers even after receipt.");
                Measure(report,"occupied-cost0-death-child-processing","NOT RUN; outside this private empty-city increment.");report.Passed=true;
            }
            catch(Exception e){Fail(report,"paid-birth-probe",e);}
            finally
            {
                if(trial is not null){try{trial.VerifyPublished();}catch(Exception e){Fail(report,"paid-published-source-final-witness",e);}trial.Cleanup(report);}
                if(B.ActiveOperations!=0||PaidCurrent is not null||PaidValidating is not null)Fail(report,"paid-scope-cookie-balance",new InvalidDataException("Paid synchronous scope survived."));
                Active=null;
            }
            return report;
        }
    }
}
