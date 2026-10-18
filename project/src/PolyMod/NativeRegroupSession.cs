using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deception.Core;
using HarmonyLib;
using WeakIdentity=Deception.PolyModAdapter.NativeLedgerOwnership.WeakIdentity;

namespace Deception.PolyModAdapter
{
    // Normal on-device Local/PassAndPlay lifecycle. No UI/rendering call may
    // enroll a state. Roots are the real native Local constructor, the first
    // completed Hotseat generator source before its first write, and Open/Create reads.
    // All subsequent siblings must arrive through the framed serializer.
    // Does not publish client fields, save files, create native units, or execute
    // commands. NativeFreshSession and its diagnostic ownership remain separate.
    internal static partial class NativeRegroupSession
    {
        const int MaxOwned=4096;
        internal static bool Enabled {get;private set;}
        internal static Action? OnSessionEnded;
        static Session? Current;
        static long NextGeneration;
        static readonly Dictionary<long,Owned> States=new();
        internal sealed class Session
        {
            internal readonly WeakIdentity Client;
            internal readonly Guid Epoch;
            internal readonly long Generation;
            internal readonly object ClientKind;
            internal readonly bool Hotseat;
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal bool Opening,Creating,CreationActive,Faulted,Retired;
            internal WeakIdentity? Settings,Source;
            internal Generation? Generating,Generated;
            internal int Notes;
            internal Session(object client,Guid epoch,long generation,bool opening,bool creating)
            {Client=new(client);Epoch=epoch;Generation=generation;ClientKind=Native.Get(client,"clientType")!;Hotseat=IsClient(client,"PassAndPlay");Opening=opening;Creating=creating;CreationActive=creating;}
        }
        internal sealed class Generation
        {
            internal readonly Session Owner;
            internal readonly WeakIdentity State;
            internal WeakIdentity? Map;
            internal bool Completed,Disposed;
            internal Generation(Session owner,object state){Owner=owner;State=new(state);}
        }
        sealed record Owned(WeakIdentity State,Session Owner);
        internal sealed class Transport
        {
            readonly Session? Owner;
            readonly SummaryRead? Summary;
            NativeOnlineSession.Transport? Online;
            readonly WeakIdentity State;
            readonly bool Reading;
            internal Transport(Session owner,object state,bool reading){Owner=owner;State=new(state);Reading=reading;}
            internal Transport(SummaryRead summary,object state,bool reading){Summary=summary;State=new(state);Reading=reading;}
            internal Transport(NativeOnlineSession.Transport online,object state,bool reading,Session? localCandidate=null){Online=online;Owner=localCandidate;State=new(state);Reading=reading;}
            internal bool IsSummary=>Summary is not null;
            internal bool IsOnline=>Online is not null;
            internal void Check(object state,bool reading)
            {if(Online is not null){Online.Check(state,reading);return;}if(Summary is not null){Require(Reading==reading&&State.Matches(state),"Summary changed state/direction.");Summary.Check(state);return;}Require(ReferenceEquals(Current,Owner)&&Reading==reading&&State.Matches(state),"Transport changed session/state/direction.");RequireSession(Owner!);}
            internal Guid Epoch=>Online?.Epoch??Summary?.Epoch??Owner!.Epoch;
            internal bool LegacyAllowed=>Online is null&&(Summary is not null||Owner!.Opening||Owner.Creating);
            internal void Adopt(object state){Check(state,Reading);if(Online is not null){Online.Adopt(state);return;}if(Summary is not null){Summary.Complete(state);return;}Register(Owner!,state);}
            internal void Frame(object state,bool reading,long revision,byte[] payload)
            {
                // While a remote open awaits its download, the published Local
                // client may still clone its own state. Resolve that one
                // ambiguity from the validated frame before native decoding.
                if(Online is not null&&Owner is not null&&reading)
                {
                    var ledger=NativeIdentityLedgerCodec.Decode(payload,revision);
                    if(ledger.OnlineContract is null&&ledger.Epoch==Owner.Epoch)
                    {Check(state,true);_=RequireSession(Owner);Online=null;}
                }
                Online?.Frame(state,reading,revision,payload);
            }
            internal void Fail()=>Online?.Fail();
        }
        static void Require(bool condition,string reason)
        {if(!condition)throw new InvalidDataException("Regroup Local lifecycle: "+reason);}
        static Guid GuidValue(object value)
        {
            string? text=(string?)Native.Invoke(Native.M("Il2CppSystem.Guid","ToString"),value);
            Require(Guid.TryParseExact(text,"D",out var id)&&id!=Guid.Empty,"Missing assigned native match Guid.");return id;
        }
        static bool IsClient(object client,string kind)=>Equals(Native.Get(client,"clientType"),Enum.Parse(Native.T("ClientBase+ClientType"),kind));
        static bool Local(object client)=>(IsClient(client,"Local")||IsClient(client,"PassAndPlay"))&&
            !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!;
        static object? ActiveClient()
        {
            if(Native.Static("GameManager","instance") is not object manager)return null;
            Require(Native.Get(manager,"fakeSession") is null,"A fake session is not a Local match.");
            return Native.Static("GameManager","Client");
        }
        static object RequireSession(Session session)
        {
            Require(Enabled&&ReferenceEquals(Current,session)&&!session.Retired&&!session.Faulted,"Session is absent, retired, or faulted.");
            Require(session.Thread==Environment.CurrentManagedThreadId,"Lifecycle crossed its native owner thread.");
            object client=session.Client.Target()??throw new InvalidDataException("Regroup Local client expired.");
            client=Native.As(client,"ClientBase");
            Require(Local(client)&&Equals(Native.Get(client,"clientType"),session.ClientKind)&&Native.Same(ActiveClient(),client),"Exact on-device client is no longer active.");
            // The constructor assigns gameId before CreateLocalGame; OpenSession
            // assigns it before reading native state. The prefix itself uses its
            // explicit Guid argument, and does not infer identity from old fields.
            Require(GuidValue(Native.Get(client,"gameId")!)==session.Epoch,"Native match Guid changed.");return client;
        }
        static Owned? Find(object state)
        {
            long key=Native.Pointer(state);
            if(!States.TryGetValue(key,out var entry))return null;
            if(entry.State.Matches(state))return entry;
            States.Remove(key);return null;
        }
        static void Register(Session session,object state)
        {
            _=RequireSession(session);
            Require(Native.Int(Native.Get(state,"Version"))==123,"Only native state123 is supported.");
            var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Adopted state has no framed ledger.");
            Require(NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Epoch==session.Epoch,"Adopted ledger belongs to another match.");
            NativeLedgerState.Validate(state,snapshot.Revision,snapshot.Bytes());
            var prior=Find(state);
            Require(prior is null||ReferenceEquals(prior.Owner,session),"A live state cannot cross lifecycle generations.");
            if(prior is not null)return;
            foreach(long key in States.Where(x=>x.Value.State.Target() is null).Select(x=>x.Key).ToArray())States.Remove(key);
            Require(States.Count<MaxOwned,"Too many live owned states; live historical states cannot be evicted.");
            States.Add(Native.Pointer(state),new(new WeakIdentity(state),session));
            NativeBlackout.Bind(state);
        }
        static Session Begin(object client,Guid epoch,bool opening,bool creating,object? settings)
        {
            Require(Enabled&&Local(client)&&Native.Same(ActiveClient(),client),"Enrollment must enter the exact active Local client.");
            Require(epoch!=Guid.Empty,"Empty match Guid.");
            NativeOnlineSession.LeaveForLocal();
            Require(Buying is null,"Session replacement during unfinished paid action.");
            if(Current is Session prior)
            {
                Require(prior.Thread==Environment.CurrentManagedThreadId,"Session switch crossed the owner thread.");
                if(prior.Client.Matches(client))
                {
                    Require(!prior.Retired&&!prior.Faulted&&prior.Epoch==epoch,"Reused client is faulted or belongs to another match.");
                    Require(!prior.Opening&&!prior.Creating,"Nested session creation/opening.");
                    // A new generation of this SAME client may still hold old
                    // role states. EndSession must retire it before reuse.
                    throw new InvalidDataException("Regroup lifecycle: active client was opened twice without EndSession.");
                }
                Require(prior.Generating is null,"Session replacement during unfinished map generation.");
                Retire(prior);
            }
            Require(NextGeneration<long.MaxValue,"Lifecycle generation exhausted.");
            var session=new Session(client,epoch,++NextGeneration,opening,creating);
            if(settings is not null)session.Settings=new(settings);
            Current=session;Note(session,"begin",epoch.ToString("D"));return session;
        }
        internal static void Install(Harmony harmony)
        {
            if(Enabled)throw new InvalidOperationException("Regroup lifecycle already installed.");
            // Diagnostics retain their exact private contexts. Production is not
            // obtained by enabling, relaxing, or sharing those contexts.
            foreach(string env in new[]{"DECEPTION_NATIVE_SESSION_PROBE","DECEPTION_NATIVE_STATE_PROBE","DECEPTION_NATIVE_ENVELOPE_PROBE",
                "DECEPTION_NATIVE_LEDGER_PROBE","DECEPTION_NATIVE_COMMAND_PROBE","DECEPTION_NATIVE_EXCHANGE_PROBE","DECEPTION_NATIVE_RECRUITMENT_PROBE"})
                if(Environment.GetEnvironmentVariable(env)=="1")return;
            Type patch=typeof(NativeRegroupSession);
            string bytes=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte)).FullName!;
            Native.Hook(harmony,Native.M("ClientBase","CreateSession","GameSettings","Il2CppSystem.Guid"),patch,
                nameof(BeforeCreate),nameof(AfterCreate),nameof(CreationFinished));
            Native.Hook(harmony,Native.M("ClientBase","CreateSession",bytes,"Il2CppSystem.Guid"),patch,
                nameof(BeforeCreate),nameof(AfterCreate),nameof(CreationFinished));
            Native.Hook(harmony,Native.M("ClientBase","OpenSession","Il2CppSystem.Guid","PolytopiaBackendBase.Game.GameType"),patch,
                nameof(BeforeOpen),null,nameof(OpenFinished));
            string players=Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(Native.T("PlayerState")).FullName!;
            Native.Hook(harmony,Native.M("GameStateUtils","CreateLocalGame","System.Int32","GameSettings","PlayerState",players),patch,
                null,nameof(AfterLocal));
            Native.Hook(harmony,Native.M("MapGenerator","Generate","GameState","MapGeneratorSettings","Il2CppSystem.Action","System.Int32"),patch,
                nameof(BeforeGeneration),nameof(AfterGeneration),nameof(GenerationFinished));
            Native.Hook(harmony,Native.M("ClientBase","UpdateGameStateImmediate","GameState","PolytopiaBackendBase.Game.StateUpdateReason"),patch,
                nameof(BeforeAdopt),nameof(AfterClient));
            foreach(var method in new[]{Native.M("ClientBase","CreateOrResetActionManager","System.UInt16"),Native.M("ClientBase","PrepareSession"),
                Native.M("ClientBase","SaveSession","System.String","System.Boolean","System.Boolean"),Native.M("ClientBase","PrepareAndValidateSaveState")})
                Native.Hook(harmony,method,patch,nameof(BeforeClient),nameof(AfterClient));
            Native.Hook(harmony,Native.M("ClientBase","SessionOpened"),patch,null,nameof(Opened));
            Native.Hook(harmony,Native.M("ClientBase","EndSession"),patch,null,nameof(Ended));
            foreach(string name in new[]{"SendCommand","SendCommandRemote"})
                Native.Hook(harmony,Native.M("ClientBase",name,"CommandBase"),patch,nameof(BeforeDispatch));
            InstallSummaryReads(harmony);
            InstallPaid(harmony);
            Enabled=true;
        }
        public static void BeforeCreate(object __instance,object __0,object __1,MethodBase __originalMethod,out object? __state)
        {
            __state=null;if(!Enabled||!Local(__instance))return;
            bool settings=__originalMethod.GetParameters()[0].ParameterType==Native.T("GameSettings");
            __state=Begin(__instance,GuidValue(__1),!settings,settings,settings?__0:null);
        }
        public static void AfterCreate(object? __state,object __result)
        {
            if(__state is not Session session)return;
            // Never manufacture Success. The ordinary native result is retained.
            if(Native.Int(__result)!=0){Fault(session,"Native CreateSession did not succeed.");return;}
            try{Roles(session,true);session.Creating=false;session.Opening=false;Note(session,"created",session.Epoch.ToString("D"));}
            catch(Exception e){Fault(session,e.Message);throw;}
        }
        public static Exception? CreationFinished(Exception? __exception,object? __state)
        {if(__state is Session session){session.CreationActive=false;session.Generated=null;if(__exception is not null)Fault(session,__exception.Message);}return __exception;}
        public static void BeforeOpen(object __instance,object __0,out object? __state)
        {__state=null;if(Enabled&&Local(__instance))__state=Begin(__instance,GuidValue(__0),true,false,null);}
        public static Exception? OpenFinished(Exception? __exception,object? __state)
        {if(__exception is not null&&__state is Session session)Fault(session,__exception.Message);return __exception;}
        public static void AfterLocal(int __0,object __1,object? __result,bool __runOriginal)
        {
            if(!Enabled||Current is not Session session||!session.Creating)return;
            try
            {
                _=RequireSession(session);Require(__runOriginal&&__result is not null&&__0==123&&session.Settings?.Matches(__1)==true,"Unexpected native Local constructor result/settings/version.");
                object state=Native.As(__result!,"GameState");
                Require(!session.Hotseat&&session.CreationActive,"Local constructor is outside its exact creation scope.");
                BindSource(session,state);
            }
            catch(Exception e){Fault(session,e.Message);throw;}
        }
        static void Roles(Session session,bool requireCore=false)
        {
            object client=RequireSession(session);
            if(requireCore)Require(Native.Get(client,"initialGameState") is not null&&Native.Get(client,"currentGameState") is not null&&Native.Get(client,"ActionManager") is not null,"Native session completed without initial/current/action-manager states.");
            // Several roles normally alias the same native state. Reconcile
            // each exact object once at this synchronous boundary, while still
            // validating every distinct state on every later boundary.
            var visited=new List<object>(6);
            void Visit(object state)
            {
                if(visited.Any(prior=>Native.Same(prior,state)))return;
                Prepare(state);Register(session,state);visited.Add(state);
            }
            foreach(string name in new[]{"initialGameState","currentGameState","targetGameState","lastTurnGameState","GameState"})
                if(Native.Get(client,name) is object state)Visit(state);
            if(Native.Get(client,"ActionManager") is object manager&&Native.Get(manager,"GameState") is object managed)
                Visit(managed);
        }
        static void BindSource(Session session,object state)
        {
            _=RequireSession(session);
            Require(session.Source is null&&session.Creating&&session.CreationActive&&session.Settings is not null&&
                (!session.Hotseat||session.Settings.Matches(Native.Get(state,"Settings")!)),
                "Fresh source is outside the exact settings creation scope.");
            Require(Native.Int(Native.Get(state,"Version"))==123&&NativeGameStateEnvelope.Get(state) is null&&Find(state) is null,"Fresh source already has an owner or unsupported version.");
            var ledger=NativeLedgerState.Bootstrap(state,session.Epoch);
            byte[] payload=NativeIdentityLedgerCodec.Encode(ledger,0);
            // Validate before publishing. A failing registration cannot leave an
            // unenrolled payload that a later native writer mistakes for a root.
            NativeLedgerState.Validate(state,0,payload);
            try{NativeGameStateEnvelope.Set(state,0,payload);Register(session,state);session.Source=new(state);session.Generated=null;}
            catch{States.Remove(Native.Pointer(state));NativeGameStateEnvelope.Forget(state);throw;}
            Note(session,"source-bound","Before first native writer; client="+session.ClientKind+"; epoch="+session.Epoch.ToString("D"));
        }
        public static void BeforeGeneration(object __0,object? __2,int __3,out object? __state)
        {
            __state=null;
            if(!Enabled||Current is not Session session||!session.Hotseat||!session.Creating||!session.CreationActive)return;
            // Unrelated generators and packaged previews cannot nominate the
            // source of this CreateSession, including nested native work.
            if(NativeMenuPreviewScope.Unbound(__0,Find(__0) is not null))return;
            if(session.Settings?.Matches(Native.Get(__0,"Settings")!)!=true)return;
            try
            {
                _=RequireSession(session);
                Require(__2 is null&&__3==-1,"Hotseat generator arguments differ from the native creation path.");
                Require(session.Source is null&&session.Generating is null&&session.Generated is null,
                    "Hotseat creation already has a generator/source witness.");
                Require(Native.Int(Native.Get(__0,"Version"))==123&&Find(__0) is null&&NativeGameStateEnvelope.Get(__0) is null,
                    "Hotseat generator source is already owned/framed or has an unsupported version.");
                var generation=new Generation(session,__0);session.Generating=generation;__state=generation;
            }
            catch(Exception e){Fault(session,"Map generation entry: "+e.Message);throw;}
        }
        public static void AfterGeneration(object __0,object? __state,bool __runOriginal)
        {
            if(__state is not Generation generation)return;
            var session=generation.Owner;
            try
            {
                _=RequireSession(session);
                Require(__runOriginal&&!generation.Disposed&&ReferenceEquals(session.Generating,generation)&&generation.State.Matches(__0)&&
                    session.CreationActive&&session.Creating&&session.Settings?.Matches(Native.Get(__0,"Settings")!)==true,
                    "Native generator completion changed its source or creation scope.");
                generation.Map=new(Native.Get(__0,"Map")??throw new InvalidDataException("Generated source lacks its native map."));
                generation.Completed=true;
            }
            catch(Exception e){Fault(session,"Map generation completion: "+e.Message);throw;}
        }
        public static Exception? GenerationFinished(Exception? __exception,object? __state)
        {
            if(__state is not Generation generation)return __exception;
            var session=generation.Owner;
            bool exact=ReferenceEquals(Current,session)&&ReferenceEquals(session.Generating,generation)&&!generation.Disposed;
            if(__exception is null&&generation.Completed&&exact&&session.CreationActive&&!session.Faulted&&!session.Retired)
                session.Generated=generation;
            else{session.Generated=null;Fault(session,"Native map generation failed or its exact creation scope ended.");}
            if(ReferenceEquals(session.Generating,generation))session.Generating=null;
            generation.Disposed=true;return __exception;
        }
        public static bool BeforeAdopt(object __instance,object __0,object __1)
        {
            if(!Enabled||Current is not Session session||!session.Client.Matches(__instance))return true;
            try{Prepare(__0);Register(session,__0);return true;}
            catch(Exception e){Fault(session,"State adoption: "+e.Message);return false;}
        }
        public static bool BeforeClient(object __instance)
        {
            if(!Enabled||Current is not Session session||!session.Client.Matches(__instance))return true;
            try{Roles(session);return true;}catch(Exception e){Fault(session,"Client boundary: "+e.Message);return false;}
        }
        public static void AfterClient(object __instance,bool __runOriginal)
        {
            if(!__runOriginal||!Enabled||Current is not Session session||!session.Client.Matches(__instance))return;
            try{Roles(session);}catch(Exception e){Fault(session,"Client adoption: "+e.Message);}
        }
        public static void Opened(object __instance)
        {
            if(!Enabled||Current is not Session session||!session.Client.Matches(__instance))return;
            try{Roles(session,true);session.Opening=false;session.Creating=false;Note(session,"opened",session.Epoch.ToString("D"));}
            catch(Exception e){Fault(session,"Session opened: "+e.Message);throw;}
        }
        public static void Ended(object __instance)
        {
            if(!Enabled||Current is not Session session||!session.Client.Matches(__instance))return;
            Retire(session);
        }
        static void Retire(Session session)
        {
            Require(session.Thread==Environment.CurrentManagedThreadId,"Session retirement crossed the owner thread.");
            session.Retired=true;session.CreationActive=false;session.Generated=null;Current=null;Note(session,"retired",session.Epoch.ToString("D"));
            try{OnSessionEnded?.Invoke();}catch(Exception e){Note(session,"cleanup-failed",e.Message);}
            // Keep weak old-generation records until native states die.
        }
        internal static bool SessionFaulted=>Enabled&&((Current?.Faulted==true&&Native.Same(ActiveClient(),Current.Client.Target()))||NativeOnlineSession.SessionFaulted);
        internal static bool CanDispatch()
        {
            if(!Enabled)return true;
            if(NativeOnlineSession.CanDispatch())return true;
            try{if(Current is not Session session||session.Opening||session.Creating)return false;_=RequireSession(session);return true;}catch{return false;}
        }
        internal static bool CanDispatch(object client)
        {
            if(!Enabled)return true;
            if(IsClient(client,"Remote"))return NativeOnlineSession.Enabled&&NativeOnlineSession.CanDispatch(client);
            if(Current is not Session session||!session.Client.Matches(client))return !Local(client);
            return CanDispatch();
        }
        public static bool BeforeDispatch(object __instance)
        {
            if(CanDispatch(__instance))return true;
            if(Current is Session session)Note(session,"command-blocked","Match persistence is stopped; close this test after a lifecycle/transaction fault.");
            return false;
        }
        static NativeGameStateEnvelope.Snapshot? ReadySnapshot(object state,bool presentation=false)
        {
            if(!Enabled)return null;
            if(NativeOnlineSession.Snapshot(state,presentation) is NativeGameStateEnvelope.Snapshot online)return online;
            if(NativeOnlineSession.HasScope)return null;
            try
            {
                var session=Current;if(session is null)return null;_=RequireSession(session);
                var owned=Find(state);if(owned is null||!ReferenceEquals(owned.Owner,session))return null;
                var snapshot=NativeGameStateEnvelope.Get(state);
                return snapshot is not null&&snapshot.Epoch==session.Epoch?snapshot:null;
            }
            catch{return null;}
        }
        internal static bool Ready(object state)=>ReadySnapshot(state) is not null;
        internal static LedgerUnit? ReadIdentity(object state,uint nativeId)=>ReadySnapshot(state,true)?.ReadIdentity(nativeId);
        internal static LedgerAppearance? ReadAppearance(object state,uint nativeId)=>ReadySnapshot(state,true)?.ReadAppearance(nativeId);
        // Genuine action/command and before-write boundary; never call from a
        // selection, renderer, unit portrait, or read-only eligibility predicate.
        internal static void Prepare(object state)
        {
            if(NativeOnlineSession.Prepare(state))return;
            Require(!NativeOnlineSession.HasScope,"Detached online simulation cannot borrow a Local state.");
            Require(Enabled&&Current is Session,"No production Local session.");var session=Current!;_=RequireSession(session);
            var owned=Find(state);Require(owned is not null&&ReferenceEquals(owned.Owner,session),"Unenrolled state cannot be bootstrapped at an action/write boundary.");
            var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Required state lost its ledger.");
            Require(NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Epoch==session.Epoch,"Writer ledger epoch changed.");
            var reconciled=NativeLedgerState.Reconcile(state,snapshot);
            if(reconciled.Changed)NativeGameStateEnvelope.Set(state,reconciled.Revision,reconciled.Payload);
            NativeBlackout.Bind(state);
        }
        internal static Transport? BeforeTransport(object state,bool reading)
        {
            if(!Enabled)return null;
            Require(Buying is null,"State transport attempted inside an unfinished paid action.");
            var prior=Find(state);
            Require(prior is null||!NativeOnlineSession.HasScope,"Detached online simulation cannot serialize a Local state.");
            bool enrolled=prior is not null||NativeOnlineSession.OwnedState(state);
            if(SummaryScope is SummaryRead summary)return SummaryTransport(summary,state,reading,enrolled);
            RejectSummaryEscape(state);
            if(NativeMenuPreviewScope.Unbound(state,enrolled))return null;
            if(prior is null&&NativeOnlineSession.BeforeTransport(state,reading) is NativeOnlineSession.Transport online)
            {
                Session? localCandidate=null;
                if(reading&&!NativeOnlineSession.HasScope&&Current is Session local&&!local.Faulted&&!local.Retired&&Native.Same(ActiveClient(),local.Client.Target()))
                {_=RequireSession(local);localCandidate=local;}
                return new Transport(online,state,reading,localCandidate);
            }
            var session=Current;
            if(session is null)
            {
                Require(prior is null&&NativeGameStateEnvelope.Get(state) is null,"A retired/framed state cannot escape its Local session.");return null;
            }
            // Unrelated game clients never acquire this match's authority.
            if(!Native.Same(ActiveClient(),session.Client.Target()))
            {Require(prior is null&&NativeGameStateEnvelope.Get(state) is null,"Framed state is being used under a different client.");return null;}
            _=RequireSession(session);
            if(reading)
            {
                Require(prior is null&&NativeGameStateEnvelope.Get(state) is null,"Deserialize receiver was already enrolled.");
                return new Transport(session,state,true);
            }
            // Hotseat serializes the source and creates framed initial/current
            // siblings before GameCreated adoption. Only the completed native
            // generator witness may authorize this first write; sharing settings
            // or merely being the first unbound writer never grants ownership.
            if(session.Hotseat&&session.Creating&&session.CreationActive&&session.Source is null&&
                session.Generated is Generation generated&&generated.Disposed&&generated.Completed&&generated.State.Matches(state))
            {
                Require(generated.Map?.Matches(Native.Get(state,"Map")!)==true,"Generated Hotseat source map changed before its first writer.");
                BindSource(session,state);session.Generated=null;
            }
            Prepare(state);return new Transport(session,state,false);
        }
        internal static void BeforeFrame(object state,bool reading,Transport? token,long revision,byte[] payload)
        {
            if(!Enabled)return;Require(token is not null,"Framed data requires an exact Local lifecycle boundary.");token!.Check(state,reading);
            token.Frame(state,reading,revision,payload);
            var ledger=NativeIdentityLedgerCodec.Decode(payload,revision);
            Require(ledger.Epoch==token.Epoch,"Frame belongs to a different match Guid.");
            Require(token.IsOnline||token.IsSummary||ledger.OnlineContract is null,"An online frame cannot enter a Local gameplay session.");
        }
        internal static void BeforeLegacyRead(object state,Transport? token)
        {
            if(!Enabled)return;
            // Outside a Local enrollment there is no implicit migration. Normal
            // unrelated native state reads still retain their unframed behavior.
            if(token is null)return;
            token.Check(state,true);Require(token.LegacyAllowed,"An already-open framed match cannot decode an unbound legacy sibling.");
        }
        internal static void Completed(object state,bool reading,bool framed,Transport? token)
        {
            if(!Enabled||token is null)return;token.Check(state,reading);
            if(reading&&!framed&&!token.IsSummary)
            {
                Require(token.LegacyAllowed,"Legacy enrollment window ended during native decoding.");
                var ledger=NativeLedgerState.Bootstrap(state,token.Epoch);
                NativeGameStateEnvelope.Set(state,0,NativeIdentityLedgerCodec.Encode(ledger,0));
            }
            if(reading)token.Adopt(state);
        }
        internal static void Failed(Transport? token,string stage)
        {if(token?.IsOnline==true){token.Fail();return;}if(Enabled&&token is not null&&!token.IsSummary&&Current is Session session)Fault(session,"Native state transport failed at "+stage);}
        internal static void Fault(string reason){if(NativeOnlineSession.HasContext){NativeOnlineSession.Fault(reason);return;}if(Current is Session session)Fault(session,reason);}
        static void Fault(Session session,string reason){session.Faulted=true;session.Generated=null;Note(session,"fault",reason);}
        static void Note(Session session,string stage,string detail)
        {try{if(session.Notes++<32)Entry.Note("native-regroup-session-"+stage,detail);}catch{/* logging cannot change native control flow */}}
    }
}
