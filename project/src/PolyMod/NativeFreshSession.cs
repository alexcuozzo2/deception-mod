using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Deception.Core;
using HarmonyLib;
using WeakIdentity=Deception.PolyModAdapter.NativeLedgerOwnership.WeakIdentity;

namespace Deception.PolyModAdapter
{
    // Proposed opt-in fresh-Local enrollment only. Never opens an old save,
    // publishes a client/state, runs a command, or repairs a failed creation.
    // The native creator owns all those operations. An armed failure remains
    // a process-lifetime barrier; there is deliberately no reset/Retire method.
    internal static class NativeFreshSession
    {
        const string Mode="DECEPTION_NATIVE_SESSION_PROBE",Arm="DECEPTION_NATIVE_SESSION_ARM_TOKEN",FaultMode="DECEPTION_NATIVE_SESSION_FAULT";
        const int Version=123;
        static bool Installed;
        internal static bool Enabled {get;private set;}
        static Guid ArmToken;
        static string Injection="",BuildVersion="";
        static Attempt? Current;
        [ThreadStatic] static CreationScope? Creating;

        sealed class Attempt
        {
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal readonly Guid Epoch=Guid.NewGuid();
            internal WeakIdentity? Manager,Settings,Client,Source,FirstReader;
            internal NativeLedgerOwnership.Context? Ownership;
            internal Guid GameId;
            internal bool Faulted,LocalEntered,Bound,SourceWritten,CopyRead,Adopted,ManagerReturned,SaveReturned,SaveValidated,InitialSaveDeferred,PrepareReturned,LevelEntered,OuterReturned;
            internal int Writes,Reads,Notes,SaveEntries;
            internal Boundary? ActiveSave;
        }
        sealed record CreationScope(Attempt Attempt);
        sealed record Boundary(Attempt Attempt,string Stage)
        {internal InitialSaveWitness? InitialSave;}
        sealed record InitialSaveWitness(long Current,long Initial,long Command,int Cursor,int Commands,int InitialCursor,int InitialCommands,
            int Actions,int InitialActions,bool Initialized,bool Ended,bool Queued,byte Actor,ushort CommandType);
        static void Require(bool value,string reason)
        {if(!value)throw new InvalidDataException("Fresh Local enrollment: "+reason);}
        static bool On(string name)=>Environment.GetEnvironmentVariable(name)=="1";
        static object Required(object? value,string reason)=>value??throw new InvalidDataException("Fresh Local enrollment: "+reason);
        static object Target(WeakIdentity? weak,string type,string reason)=>Native.As(Required(weak?.Target(),reason),type);

        internal static void Install(Harmony harmony)
        {
            if(!On(Mode))return;
            if(Installed)throw new InvalidOperationException("Fresh Local enrollment already installed.");
            foreach(string name in new[]{"DECEPTION_NATIVE_STATE_PROBE","DECEPTION_NATIVE_ENVELOPE_PROBE","DECEPTION_NATIVE_LEDGER_PROBE",
                "DECEPTION_NATIVE_COMMAND_PROBE","DECEPTION_NATIVE_EXCHANGE_PROBE","DECEPTION_NATIVE_RECRUITMENT_PROBE"})
                Require(!On(name),"Session mode cannot share a detached/recruitment diagnostic process.");
            Require(Guid.TryParseExact(Environment.GetEnvironmentVariable(Arm),"D",out ArmToken)&&ArmToken!=Guid.Empty,"Missing exact prelaunch arm token.");
            Injection=Environment.GetEnvironmentVariable(FaultMode)??"";
            Require(Injection is "" or "pre-context" or "pre-binding" or "first-read","Unknown requested fault stage.");
            BuildVersion=typeof(Entry).Assembly.GetName().Version?.ToString()??throw new InvalidOperationException("Missing candidate assembly version.");
            Validate();
            Type patch=typeof(NativeFreshSession);
            Native.Hook(harmony,Native.M("GameManager","CreateSinglePlayerGame"),patch,nameof(BeforeCreate),nameof(AfterCreate),nameof(CreateFinished));
            string players=Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(Native.T("PlayerState")).FullName!;
            Native.Hook(harmony,Native.M("GameStateUtils","CreateLocalGame","System.Int32","GameSettings","PlayerState",players),patch,
                nameof(BeforeLocal),nameof(AfterLocal),nameof(LocalFinished));
            Native.Hook(harmony,Native.M("ClientBase","UpdateGameStateImmediate","GameState","PolytopiaBackendBase.Game.StateUpdateReason"),patch,
                nameof(BeforeAdopt),nameof(AfterClient),nameof(ClientFinished));
            Native.Hook(harmony,Native.M("ClientBase","PrepareAndValidateSaveState"),patch,nameof(BeforeValidateSave),nameof(AfterValidateSave),nameof(ClientFinished));
            foreach(var method in new[]{Native.M("ClientBase","CreateOrResetActionManager","System.UInt16"),
                Native.M("ClientBase","SaveSession","System.String","System.Boolean","System.Boolean"),
                Native.M("ClientBase","PrepareSession"),Native.M("ClientBase","SessionOpened")})
                Native.Hook(harmony,method,patch,nameof(BeforeClient),nameof(AfterClient),nameof(ClientFinished));
            Native.Hook(harmony,Native.M("GameManager","LoadLevel"),patch,nameof(BeforeLevel),null,nameof(ClientFinished));
            Installed=true;Enabled=true;
            Note(null,"armed",new{armToken=ArmToken.ToString("D"),buildVersion=BuildVersion,faultStage=Injection,
                scope="fresh-local-empty-ledger",oneAttempt=true,paidMimicBirth=false});
        }
        static void Validate()
        {
            foreach(string name in new[]{"get_instance","get_Client","get_GameState","get_client","get_fakeSession","get_settings","get_isLoadingGame"})_=Native.M("GameManager",name);
            foreach(string name in new[]{"get_gameId","get_clientType","get_IsReplay","get_IsSpectating","get_GameState","get_ActionManager",
                "get_initialGameState","get_currentGameState","get_targetGameState","get_lastTurnGameState"})_=Native.M("ClientBase",name);
            _=Native.M("GameSettings","IsTutorial");_=Native.M("GameState","get_Settings");_=Native.M("GameState","get_Version");
            _=Native.M("ClientBase","get_hasInitializedSaveData");_=Native.M("ClientBase","get_isSessionEnded");
            _=Native.M("GameState","HasQueuedCommands");_=Native.M("StartMatchCommand","GetCommandType");
            _=Native.M("ClientActionManager","get_GameState");_=Native.M("Il2CppSystem.Guid","ToString");
            _=Enum.Parse(Native.T("ClientBase+ClientType"),"Local");
        }
        static Guid ReadGameId(object client)
        {
            var value=Required(Native.Get(client,"gameId"),"Missing assigned native Guid.");
            string? text=(string?)Native.Invoke(Native.M("Il2CppSystem.Guid","ToString"),value);
            Require(Guid.TryParseExact(text,"D",out var id)&&id!=Guid.Empty,"Native game Guid is empty or invalid.");return id;
        }
        static object Client(Attempt a)
        {
            Require(!a.Faulted,"The one armed attempt has a persistent fault.");
            Require(a.Thread==Environment.CurrentManagedThreadId,"Attempt crossed native owner thread.");
            var manager=Target(a.Manager,"GameManager","Native manager expired.");
            Require(Native.Same(Native.Static("GameManager","instance"),manager),"Active manager changed.");
            Require(Native.Get(manager,"fakeSession") is null,"Fake session cannot be enrolled.");
            var client=Target(a.Client,"ClientBase","No enrolled native client.");
            Require(Native.Same(Native.Get(manager,"client"),client)&&Native.Same(Native.Static("GameManager","Client"),client),"Active client changed.");
            Require(ReadGameId(client)==a.GameId&&Equals(Native.Get(client,"clientType"),Enum.Parse(Native.T("ClientBase+ClientType"),"Local"))&&
                !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!,"Client Guid/type/view changed.");
            return client;
        }
        static void Inject(Attempt a,string stage)
        {if(Injection==stage)throw new InvalidDataException("Requested fresh-session fault at "+stage+".");}
        static void Empty(Attempt a,long revision,byte[] payload)
        {
            var ledger=NativeIdentityLedgerCodec.Decode(payload,revision);
            Require(revision==0&&ledger.Epoch==a.Epoch&&ledger.NextLogicalId==1&&ledger.NextSequence==1&&ledger.Units.Length==0&&ledger.Receipts.Length==0,
                "This first session slice requires its unchanged empty real ledger.");
        }
        static void Bound(Attempt a,object state,bool reserve)
        {
            Require(Native.Int(Native.Get(state,"Version"))==Version,"State version differs from123.");
            var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Fresh Local enrollment: missing state binding.");
            byte[] bytes=snapshot.Bytes();Empty(a,snapshot.Revision,bytes);NativeLedgerState.Validate(state,snapshot.Revision,bytes);
            if(reserve)NativeLedgerOwnership.RecordAdopted(a.Ownership??throw new InvalidDataException("Missing required ownership context."),state);
        }
        static object Roles(Attempt a,object client,bool needManager)
        {
            Require(Native.Same(Client(a),client)&&a.Bound&&a.SourceWritten&&a.CopyRead,"Initial framed source/copy evidence is incomplete.");
            var initial=Required(Native.Get(client,"initialGameState"),"Missing native initial state.");
            var current=Required(Native.Get(client,"currentGameState"),"Missing native current state.");
            Bound(a,initial,true);Bound(a,current,true);
            // Native StartMatch history is appended only to the original after
            // initial cloning; compare ledger ownership, never native byte equality.
            foreach(string name in new[]{"targetGameState","lastTurnGameState","GameState"})
                if(Native.Get(client,name) is object state)Bound(a,state,true);
            var manager=Native.Get(client,"ActionManager");
            if(needManager)Require(manager is not null,"Missing native action manager.");
            if(manager is not null)
            {
                var state=Required(Native.Get(manager,"GameState"),"Action manager has no state.");Bound(a,state,true);
                if(needManager)Require(Native.Same(state,current),"Native manager/current-state identity differs.");
            }
            return current;
        }
        // Outer entry is void: returning false deliberately skips creation,
        // without manufacturing CreateSessionResult.Success (whose enum is0).
        public static bool BeforeCreate(object __instance,out object? __state)
        {
            __state=null;if(!Enabled)return true;
            try
            {
                Require(Current is null&&Creating is null,"Only one new Local attempt is permitted per process.");
                var a=new Attempt();Current=a;var scope=new CreationScope(a);Creating=scope;__state=scope;
                a.Manager=new WeakIdentity(__instance);
                Require(Native.Same(Native.Static("GameManager","instance"),__instance),"Creation did not enter the exact native singleton.");
                Require(Native.Get(__instance,"fakeSession") is null&&!(bool)Native.Get(__instance,"isLoadingGame")!,"Creation is already loading or uses fakeSession.");
                var settings=Required(Native.Get(__instance,"settings"),"Missing native creation settings.");a.Settings=new WeakIdentity(settings);
                Require(!(bool)Native.Invoke(Native.M("GameSettings","IsTutorial"),settings)!,"Tutorial creation is outside this explicit slice.");
                Note(a,"create-enter",new{armToken=ArmToken.ToString("D"),buildVersion=BuildVersion});return true;
            }
            catch(Exception e){Fail(Current,"create-prefix",e);return false;}
        }
        public static void AfterCreate(object? __state,bool __runOriginal)
        {
            if(__state is not CreationScope scope)return;var a=scope.Attempt;
            try
            {
                if(!__runOriginal||a.Faulted)return;
                Require(a.LocalEntered&&a.Bound&&a.SourceWritten&&a.CopyRead&&a.Adopted&&a.ManagerReturned&&a.SaveReturned&&(a.SaveValidated||a.InitialSaveDeferred)&&a.PrepareReturned&&a.LevelEntered,
                    "Native creator returned without the required enrollment/publication barriers.");
                a.OuterReturned=true;Note(a,"create-returned",new{nativeGameId=a.GameId.ToString("D"),sourceFramed=a.SourceWritten,initialCopyFramed=a.CopyRead,
                    levelEntryObserved=a.LevelEntered,saveMethodReturned=a.SaveReturned,nativeSuccessClaim=false});
            }
            catch(Exception e){Fail(a,"create-postfix",e);}
        }
        public static Exception? CreateFinished(Exception? __exception,object? __state)
        {
            if(__state is CreationScope scope)
            {
                if(__exception is not null)Fail(scope.Attempt,"create-native",__exception);
                if(ReferenceEquals(Creating,scope))Creating=null;
                // Keep Current, weak identities, bindings and Ownership after
                // failure AND success. Scope exit does not discard a live client.
            }
            return __exception;
        }
        public static bool BeforeLocal(int __0,object __1,object? __3,ref object? __result,out object? __state)
        {
            __state=null;if(!Enabled||Creating is null)return true;var a=Creating.Attempt;__state=new Boundary(a,"CreateLocalGame");
            try
            {
                Require(ReferenceEquals(Current,a)&&!a.Faulted&&a.Thread==Environment.CurrentManagedThreadId&&!a.LocalEntered,"Unexpected/reentrant native local factory.");
                a.LocalEntered=true;
                Require(__0==Version&&__3 is null&&a.Settings!.Matches(__1)&&!(bool)Native.Invoke(Native.M("GameSettings","IsTutorial"),__1)!,"Unsupported factory version/settings/opponents.");
                var manager=Target(a.Manager,"GameManager","Manager expired during creation.");
                Require(Native.Same(Native.Static("GameManager","instance"),manager)&&Native.Get(manager,"fakeSession") is null,"Creation manager changed.");
                var client=Native.As(Required(Native.Get(manager,"client"),"Native creator did not assign a client."),"ClientBase");
                a.Client=new WeakIdentity(client);a.GameId=ReadGameId(client);_=Client(a);
                // Preservation witness precedes every injected failure and does
                // not say that enrollment or a file write has succeeded.
                Note(a,"created",new{schema=1,armToken=ArmToken.ToString("D"),attempt=1,nativeGameId=a.GameId.ToString("D"),ledgerEpoch=a.Epoch.ToString("D"),
                    clientType="Local",serializerVersion=Version,scope="fresh-local-empty-ledger",buildVersion=BuildVersion,faultStage=Injection,
                    processId=Environment.ProcessId,timestampUtc=DateTimeOffset.UtcNow.ToString("O")});
                foreach(string name in new[]{"initialGameState","currentGameState","targetGameState","ActionManager"})
                    Require(Native.Get(client,name) is null,"Fresh native client already owns "+name+".");
                Inject(a,"pre-context");a.Ownership=NativeLedgerOwnership.BeginRequiredContext(client,a.GameId,a.Epoch);
                Note(a,"context-begun",new{nativeGameId=a.GameId.ToString("D"),generation=a.Ownership.Generation});return true;
            }
            catch(Exception e){__result=null;Fail(a,"local-prefix",e);return false;}
        }
        public static void AfterLocal(ref object? __result,object? __state,bool __runOriginal)
        {
            if(__state is not Boundary b)return;var a=b.Attempt;
            try
            {
                Require(__runOriginal&&!a.Faulted,"Native local construction was skipped or faulted.");_=Client(a);
                var state=Native.As(Required(__result,"Native local constructor returned null."),"GameState");
                Require(a.Source is null&&a.Ownership is not null&&Native.Int(Native.Get(state,"Version"))==Version&&
                    a.Settings!.Matches(Required(Native.Get(state,"Settings"),"Fresh state has no settings.")),"Unexpected source state/version/settings.");
                Require(NativeGameStateEnvelope.Get(state) is null,"Fresh source already has a private binding.");
                NativeLedgerOwnership.BeforeRead(state); // Read-only publication/adoption census, no stream or permit.
                foreach(var body in NativeLedgerState.Graph(state).Values)
                    Require(!RecruitmentCatalog.IsMimicId(Native.EnumName(Required(Native.Get(body.Unit,"type"),"Missing native unit type."))),"Fresh source contains an unrecorded Mimic.");
                Inject(a,"pre-binding");
                byte[] payload=NativeIdentityLedgerCodec.Encode(new NativeIdentityLedger(a.Epoch,1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>()),0);
                NativeLedgerState.Validate(state,0,payload);NativeGameStateEnvelope.Set(state,0,payload);
                a.Source=new WeakIdentity(state);NativeLedgerOwnership.RecordAdopted(a.Ownership!,state);a.Bound=true;
                Note(a,"source-bound",new{nativeGameId=a.GameId.ToString("D"),ledgerEpoch=a.Epoch.ToString("D"),revision=0,units=0,receipts=0,beforeFirstNativeWriter=true});
            }
            catch(Exception e){__result=null;Fail(a,"local-postfix",e);}
        }
        public static Exception? LocalFinished(Exception? __exception,object? __state)
        {if(__state is Boundary b&&__exception is not null)Fail(b.Attempt,"local-native",__exception);return __exception;}

        // These are native bridge policy callbacks, not Harmony guards. Throwing
        // here is intentional and is translated by the existing native bridge.
        internal static void BeforeTransport(object state,bool reading)
        {
            if(!Enabled||Current is not Attempt a)return;
            try
            {
                _=Client(a);Require(a.Ownership is not null&&a.Bound,"Transport preceded the source's real ledger enrollment.");
                if(reading)Require(a.SourceWritten,"State copy preceded its first completed framed source writer.");
                else
                {
                    Bound(a,state,false);
                    if(!a.SourceWritten)Require(a.Source!.Matches(state),"First native writer is not the exact newly enrolled source.");
                }
            }
            catch(Exception e){Fail(a,reading?"transport-read-guard":"transport-write-guard",e);throw;}
        }
        internal static void BeforeFrame(object receiver,long revision,byte[] payload)
        {
            if(!Enabled||Current is not Attempt a)return;
            try
            {
                _=Client(a);Require(a.Bound&&a.SourceWritten,"Frame read preceded source enrollment/write.");Empty(a,revision,payload);
                if(!a.CopyRead)
                {
                    Require(a.FirstReader is null,"Overlapping first cloned readers are unsupported.");
                    Require(!a.Source!.Matches(receiver),"First copy aliases the native source.");a.FirstReader=new WeakIdentity(receiver);
                    Inject(a,"first-read");
                }
            }
            catch(Exception e){Fail(a,"frame-before-native-read",e);throw;}
        }
        internal static void BeforePublish(object state,bool reading,long revision,byte[] payload)
        {
            if(!Enabled||Current is not Attempt a)return;
            try
            {
                _=Client(a);Require(a.Bound&&a.Ownership is not null,"Publication preceded source enrollment.");Empty(a,revision,payload);
                if(reading)
                {
                    Require(a.SourceWritten,"Read publication preceded first source write.");
                    NativeLedgerState.Validate(state,revision,payload);
                    if(!a.CopyRead)Require(a.FirstReader is not null&&a.FirstReader.Matches(state),"First cloned reader changed identity before publication.");
                }
                else
                {
                    Bound(a,state,false);
                    if(!a.SourceWritten)Require(a.Source is not null&&a.Source.Matches(state),"First writer changed source identity before publication.");
                }
            }
            catch(Exception e){Fail(a,reading?"read-before-publication":"write-before-publication",e);throw;}
        }
        internal static void FrameCompleted(object state,bool reading)
        {
            // Evidence only: the exact source/receiver/context and empty payload
            // were checked before the native operation. Envelope Finish validates
            // and publishes the resulting frame/binding before this notification.
            // Do not add correctness checks, native reads or throw-capable policy
            // here: the caller stream may already contain the complete frame.
            try
            {
                if(!Enabled||Current is not Attempt a||a.Faulted)return;
                if(reading)
                {
                    if(a.Reads<int.MaxValue)a.Reads++;
                    if(!a.CopyRead)
                    {
                        a.CopyRead=true;Note(a,"first-copy-framed",new{nativeGameId=a.GameId.ToString("D"),ledgerEpoch=a.Epoch.ToString("D")});
                    }
                }
                else
                {
                    if(a.Writes<int.MaxValue)a.Writes++;
                    if(!a.SourceWritten)
                    {
                        a.SourceWritten=true;Note(a,"first-source-framed",new{nativeGameId=a.GameId.ToString("D"),ledgerEpoch=a.Epoch.ToString("D")});
                    }
                }
            }
            catch{/* Accounting/logging never changes a completed native writer or reader outcome. */}
        }
        internal static void OnTransportFailure(string stage,string reason)
        {
            try{if(Enabled&&Current is Attempt a)Fail(a,"bridge-"+stage,new InvalidDataException(reason));}
            catch{/* Never escape a managed/native callback or mask its original outcome. */}
        }
        internal static bool BlockMimicRecruitment(object state)=>Enabled;

        // Read-only source selection for the separately armed execution probe.
        // Never enrolls a resumed game or reconstructs missing ownership.
        internal static bool TryGetExecutionSource(out object? state,out string reason)
        {
            state=null;reason="Waiting for the fresh disposable match.";
            if(!Enabled||!NativeExecutionTrigger.Requested||Current is not Attempt a)return false;
            reason=$"enrollment: fault={a.Faulted}, outer={a.OuterReturned}, level={a.LevelEntered}, adopted={a.Adopted}, prepared={a.PrepareReturned}, creating={Creating is not null}, saving={a.ActiveSave is not null}";
            if(a.Faulted||!a.OuterReturned||!a.LevelEntered||!a.Adopted||!a.PrepareReturned||Creating is not null||a.ActiveSave is not null)return false;
            try
            {
                var client=Client(a);var manager=Target(a.Manager,"GameManager","Creation manager expired.");
                reason="GameManager.isLoadingGame";if((bool)Native.Get(manager,"isLoadingGame")!)return false;
                reason="PopupManager.PopupShowing";if((bool)Native.Static("PopupManager","PopupShowing")!)return false;
                reason="ClientBase.IsReady=false";if(!(bool)Native.Get(client,"IsReady")!)return false;
                reason="ClientBase.isSessionEnded";if((bool)Native.Get(client,"isSessionEnded")!)return false;
                reason="ClientBase.IsWaitingForCommand";if((bool)Native.Get(client,"IsWaitingForCommand")!)return false;
                var current=Roles(a,client,true);var actions=Required(Native.Get(client,"ActionManager"),"Missing native manager.");
                reason="ClientActionManager.IsProcessing";if((bool)Native.Get(actions,"IsProcessing")!)return false;
                reason="ClientActionManager.IsSimulating";if((bool)Native.Get(actions,"IsSimulating")!)return false;
                reason="GameState.HasQueuedCommands";if((bool)Native.Invoke(Native.M("GameState","HasQueuedCommands"),current)!)return false;
                reason="GameManager.GameState differs from current";if(!Native.Same(Native.Static("GameManager","GameState"),current))return false;
                reason="GameState.CurrentState is not Started";if(!Equals(Native.Get(current,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started")))return false;
                foreach(string name in new[]{"ActionStack","pendingCommandTriggers"})
                {
                    reason="GameState."+name+" is nonempty";var queue=Native.Get(current,name);
                    // Native TryGetPendingCommandTrigger returns false for an
                    // absent lazy trigger list. ActionStack is always required.
                    if(name=="pendingCommandTriggers"&&queue is null)continue;
                    if(Native.Int(Native.Get(Required(queue,"Missing native queue."),"Count"))!=0)return false;
                }
                reason="GameManager.LocalPlayer is absent";var player=Native.Static("GameManager","LocalPlayer");if(player is null)return false;
                byte viewer=Convert.ToByte(Native.Get(player,"Id"));
                reason="GameState.CurrentPlayer differs from local viewer";if(Native.Int(Native.Get(current,"CurrentPlayer"))!=viewer)return false;
                reason="GameManager.IsPlayerViewing=false";if(!(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!)return false;
                NativeLedgerOwnership.BeforeWrite(current);state=current;reason="";return true;
            }
            catch(Exception e){reason="Readiness inspection failed after "+reason+": "+e.GetType().Name+": "+e.Message;return false;}
        }

        public static bool BeforeAdopt(object __instance,object __0,out object? __state)
        {
            __state=null;if(!Enabled||Current is not Attempt a)return true;__state=new Boundary(a,"UpdateGameStateImmediate");
            try
            {
                var client=Client(a);Require(Native.Same(client,__instance)&&a.SourceWritten&&a.CopyRead,"Adoption preceded complete native copy.");
                Bound(a,__0,true);var initial=Required(Native.Get(client,"initialGameState"),"First native reader did not publish its initial sibling.");Bound(a,initial,true);
                if(!a.Adopted)
                {
                    Require(a.Source!.Matches(__0)&&a.FirstReader!.Matches(initial)&&!Native.Same(initial,__0),"First source/initial adoption identities differ.");
                }
                return true;
            }
            catch(Exception e){Fail(a,"adoption-prefix",e);return false;}
        }
        public static bool BeforeClient(object __instance,MethodBase __originalMethod,out object? __state)
        {
            __state=null;if(!Enabled||Current is not Attempt a)return true;string stage=__originalMethod.Name;var b=new Boundary(a,stage);__state=b;
            try
            {
                bool needManager=stage!="CreateOrResetActionManager";
                _=Roles(a,__instance,needManager);
                if(stage=="SaveSession")
                {
                    Require(a.ActiveSave is null,"Nested native saves are outside this slice.");
                    Require(a.SaveEntries<int.MaxValue,"Native save observation count exhausted.");
                    a.SaveEntries++;a.ActiveSave=b;
                }
                if(stage=="SaveSession")a.SaveValidated=false;
                // SaveSession is guarded irrespective of native ignoreValidation;
                // never invoke it ourselves or reinterpret its void return.
                return true;
            }
            catch(Exception e){Fail(a,stage+"-prefix",e);return false;}
        }
        public static bool BeforeValidateSave(object __instance,ref bool __result,out object? __state)
        {
            __state=null;if(!Enabled||Current is not Attempt a)return true;var b=new Boundary(a,"PrepareAndValidateSaveState");__state=b;
            try
            {
                _=Roles(a,__instance,true);
                if(Creating?.Attempt==a&&a.ActiveSave is not null&&a.SaveEntries==1&&!a.SaveReturned&&!a.PrepareReturned&&!a.LevelEntered&&!a.OuterReturned)
                {
                    b.InitialSave=ObserveInitialSave(a,__instance);
                    Note(a,"initial-save-before-validation",b.InitialSave);
                }
                return true;
            }
            catch(Exception e){__result=false;Fail(a,"validate-save-prefix",e);return false;}
        }
        static InitialSaveWitness ObserveInitialSave(Attempt a,object client)
        {
            var current=Roles(a,client,true);var initial=Required(Native.Get(client,"initialGameState"),"Missing initial sibling.");
            Require(a.Source!.Matches(current)&&a.FirstReader!.Matches(initial)&&!Native.Same(current,initial),"Initial save roles changed.");
            var commands=Required(Native.Get(current,"CommandStack"),"Missing current command stack.");
            int count=Native.Int(Native.Get(commands,"Count"));Require(count==1,"Expected exactly the native initial StartMatch command.");
            var command=Native.As(Required(Native.Invoke(Native.M(commands.GetType(),"get_Item","System.Int32"),commands,0),"Missing initial command."),"StartMatchCommand");
            return new(Native.Pointer(current),Native.Pointer(initial),Native.Pointer(command),Native.Int(Native.Get(current,"CurrentCommand")),count,
                Native.Int(Native.Get(initial,"CurrentCommand")),Native.Int(Native.Get(Native.Get(initial,"CommandStack")!,"Count")),
                Native.Int(Native.Get(Native.Get(current,"ActionStack")!,"Count")),Native.Int(Native.Get(Native.Get(initial,"ActionStack")!,"Count")),
                (bool)Native.Get(client,"hasInitializedSaveData")!,(bool)Native.Get(client,"isSessionEnded")!,
                (bool)Native.Invoke(Native.M("GameState","HasQueuedCommands"),current)!,Convert.ToByte(Native.Get(command,"PlayerId")),
                Convert.ToUInt16(Native.Invoke(Native.M("StartMatchCommand","GetCommandType"),command)));
        }
        public static void AfterValidateSave(object __instance,object? __state,bool __runOriginal,bool __result)
        {
            if(__state is not Boundary b||!__runOriginal||b.Attempt.Faulted)return;var a=b.Attempt;
            try
            {
                // The native creator queues StartMatch before its early save.
                // HasQueuedCommands makes native validation return false without
                // processing or saving. Preserve that exact native deferral;
                // never force a save, flip the Boolean, or process its command.
                _=Roles(a,__instance,true);
                if(!__result)
                {
                    Require(b.InitialSave is not null&&Creating?.Attempt==a&&a.ActiveSave is not null&&a.SaveEntries==1&&
                        !a.SaveReturned&&!a.PrepareReturned&&!a.LevelEntered&&!a.OuterReturned,"Unclassified native save validation false.");
                    var after=ObserveInitialSave(a,__instance);Note(a,"initial-save-after-validation",after);
                    Require(after==b.InitialSave&&after.Initialized&&!after.Ended&&after.Queued&&after.Cursor==0&&after.Commands==1&&
                        after.InitialCursor==0&&after.InitialCommands==0&&after.Actions==0&&after.InitialActions==0&&after.Actor==1&&after.CommandType==20,
                        "Native initial save deferral does not match the audited queued StartMatch boundary.");
                    a.InitialSaveDeferred=true;
                    Note(a,"initial-save-deferred",new{nativeGameId=a.GameId.ToString("D"),nativeResult=false,nativeFileSuccessClaim=false});return;
                }
                a.SaveValidated=true;
                Note(a,"save-validation-true",new{nativeGameId=a.GameId.ToString("D"),nativeFileSuccessClaim=false});
            }
            catch(Exception e){Fail(a,"native-save-validation",e);}
        }
        public static void AfterClient(object __instance,object? __state,bool __runOriginal)
        {
            if(__state is not Boundary b)return;var a=b.Attempt;
            try
            {
                if(!__runOriginal||a.Faulted)return;_=Roles(a,__instance,true);
                switch(b.Stage)
                {
                    case "UpdateGameStateImmediate":a.Adopted=true;break;
                    case "CreateOrResetActionManager":a.ManagerReturned=true;break;
                    case "SaveSession":a.SaveReturned=true;break;
                    case "PrepareSession":a.PrepareReturned=true;break;
                }
                Note(a,"boundary-returned",new{nativeGameId=a.GameId.ToString("D"),boundary=b.Stage,nativeFileSuccessClaim=false});
            }
            catch(Exception e){Fail(a,b.Stage+"-postfix",e);}
        }
        public static bool BeforeLevel(object __instance,out object? __state)
        {
            __state=null;if(!Enabled||Current is not Attempt a)return true;__state=new Boundary(a,"LoadLevel");
            try
            {
                Require(a.Manager is not null&&a.Manager.Matches(__instance),"Level loading changed manager.");
                var client=Client(a);_=Roles(a,client,true);
                Require(a.Adopted&&a.ManagerReturned&&a.SaveReturned&&(a.SaveValidated||a.InitialSaveDeferred)&&a.PrepareReturned,"Native preparation did not complete its required guarded boundaries.");
                a.LevelEntered=true;Note(a,"load-level-enter",new{nativeGameId=a.GameId.ToString("D"),nativeReadinessClaim=false,framedWrites=a.Writes,framedReads=a.Reads});return true;
            }
            catch(Exception e){Fail(a,"load-level-prefix",e);return false;}
        }
        public static Exception? ClientFinished(Exception? __exception,object? __state)
        {
            if(__state is Boundary b)
            {
                if(__exception is not null)Fail(b.Attempt,b.Stage+"-native",__exception);
                if(ReferenceEquals(b.Attempt.ActiveSave,b))b.Attempt.ActiveSave=null;
            }
            return __exception;
        }
        static void Fail(Attempt? a,string stage,Exception error)
        {
            // Persistent fault assignment must precede every optional native
            // weak access or sink call. A pre-context failure is still sticky.
            try
            {
                if(a is null)return;bool first=!a.Faulted;a.Faulted=true;
                try{if(a.Ownership is not null)NativeLedgerOwnership.Fault(a.Ownership);}catch{}
                if(first)Note(a,"fault",new{armToken=ArmToken.ToString("D"),nativeGameId=a.GameId==Guid.Empty?null:a.GameId.ToString("D"),stage,
                    reason=error.GetType().Name+": "+error.Message,contextBegun=a.Ownership is not null,sourceBound=a.Bound,
                    sourceFramed=a.SourceWritten,copyFramed=a.CopyRead,loadEntered=a.LevelEntered,persistent=true});
            }
            catch{/* Fault reporting is never a new native exception. */}
        }
        static void Note(Attempt? a,string suffix,object detail)
        {
            try
            {
                if(a is not null&&a.Notes++>=128)return;
                Entry.Note("native-session-"+suffix,JsonSerializer.Serialize(detail));
            }
            catch{/* Evidence logging cannot alter native control flow. */}
        }
    }
}
