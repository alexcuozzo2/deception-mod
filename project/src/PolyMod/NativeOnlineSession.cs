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
    // The preview executes the same commands in a live client and in detached
    // shadow copies. A scoped shadow never borrows the live client's authority.
    // Scopes cover synchronous leaf calls only; none survive an async await.
    internal static class NativeOnlineSession
    {
        internal static bool Enabled {get;private set;}
        static int MainThread;
        static Live? Current;
        static Live? Pending;
        [ThreadStatic] static Scope? Active;
        [ThreadStatic] static Execution? Executing;
        static readonly Dictionary<long,Owned> States=new();
        sealed record Owned(WeakIdentity State,Owner Owner);
        sealed record Execution(Owner Owner,Execution? Previous,int Thread);
        internal abstract class Owner
        {
            internal readonly Guid Epoch;
            internal readonly string Contract;
            readonly OnlineAuthorityScope Authority;
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal bool Closed,Faulted;
            protected Owner(Guid epoch){Require(epoch!=Guid.Empty,"Empty match identity.");Epoch=epoch;Contract=NativeOnlinePreview.CurrentContract();Authority=new(epoch,this);}
            internal abstract void Check(bool presentation=false);
            internal void Common(bool presentation)
            {Authority.Require(Epoch,this);Require(Enabled&&!Closed&&(!Faulted||presentation)&&Thread==MainThread,"Online authority expired, faulted, or crossed its owner thread.");}
        }
        sealed class Live:Owner
        {
            internal readonly WeakIdentity Client;
            internal bool Opening=true;
            internal Live(object client,Guid epoch):base(epoch){Client=new(client);}
            internal override void Check(bool presentation=false)
            {
                Common(presentation);Require(Active is null,"Detached simulation cannot borrow a live client.");Require(ReferenceEquals(Current,this)||ReferenceEquals(Pending,this),"Online client was replaced.");
                var target=Client.Target();Require(target is not null,"Online client expired.");
                var client=Native.As(target!,"ClientBase");Require(Remote(client),"Online client mode changed.");
                // Native OpenMultiplayerGame awaits RemoteClient.OpenSession
                // BEFORE publishing it through SetRemoteClient. Only that exact
                // witnessed opening client may decode before publication.
                if(presentation||!Opening)Require(Native.Same(client,ActiveClient()),"Exact online client is no longer active.");
                Require(GuidValue(Native.Get(client!,"gameId")!)==Epoch,"Online client match identity changed.");
            }
        }
        internal sealed class Scope:Owner,IDisposable
        {
            internal readonly Scope? Previous;
            internal readonly bool Creation;
            internal WeakIdentity? Source,GeneratedMap;
            internal bool Generated,Bound;
            internal Scope(Guid epoch,bool creation):base(epoch){Previous=Active;Creation=creation;}
            internal override void Check(bool presentation=false)
            {Common(presentation);Require(ReferenceEquals(Active,this),"Detached scope is no longer current.");}
            public void Dispose()
            {
                if(Closed)return;
                Require(Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(Active,this),"Detached scope cleanup order changed.");
                try
                {
                    foreach(var entry in States.Where(x=>ReferenceEquals(x.Value.Owner,this)).ToArray())
                    {if(entry.Value.State.Target() is object state)NativeGameStateEnvelope.Forget(state);}
                    // Retain the weak ownership tombstone: a summary's out
                    // GameState can escape its caller, but must never become an
                    // unframed writer or a new live/source identity.
                }
                finally{Closed=true;Active=Previous;}
            }
        }
        internal sealed class Transport
        {
            readonly Owner Owner;
            readonly WeakIdentity State;
            readonly bool Reading;
            internal Transport(Owner owner,object state,bool reading){Owner=owner;State=new(state);Reading=reading;}
            internal Guid Epoch=>Owner.Epoch;
            internal void Check(object state,bool reading)
            {Owner.Check();Require(Reading==reading&&State.Matches(state),"Online transport changed state or direction.");}
            internal void Frame(object state,bool reading,long revision,byte[] payload)
            {
                Check(state,reading);var ledger=NativeIdentityLedgerCodec.Decode(payload,revision);
                Require(ledger.Epoch==Owner.Epoch&&ledger.OnlineContract==Owner.Contract,"Online snapshot belongs to a different game or mod build.");
            }
            internal void Adopt(object state){Check(state,true);Register(Owner,state);}
            internal void Fail(){Owner.Faulted=true;}
        }
        static void Require(bool value,string why){if(!value)throw new InvalidDataException("Deception online: "+why);}
        static Guid GuidValue(object value)
        {
            var text=(string?)Native.Invoke(Native.M("Il2CppSystem.Guid","ToString"),value);
            Require(Guid.TryParseExact(text,"D",out var epoch)&&epoch!=Guid.Empty,"Missing native match Guid.");return epoch;
        }
        static object? ActiveClient()
        {
            if(Native.Static("GameManager","instance") is not object manager)return null;
            Require(Native.Get(manager,"fakeSession") is null,"A preview cannot be an online game.");return Native.Static("GameManager","Client");
        }
        internal static bool Remote(object client)=>Native.Get(client,"clientType")!.ToString()=="Remote"&&
            !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!;
        internal static bool HasScope=>Active is not null;
        internal static bool SessionFaulted=>Current?.Faulted==true&&Native.Same(ActiveClient(),Current.Client.Target())||
            Pending?.Faulted==true&&Native.Same(ActiveClient(),Pending.Client.Target());
        internal static bool HasContext=>Active is not null||Executing is not null||
            Current is not null&&Native.Same(ActiveClient(),Current.Client.Target())||Pending is not null&&Native.Same(ActiveClient(),Pending.Client.Target());
        internal static void LeaveForLocal()
        {
            if(!Enabled)return;Require(Active is null,"Local creation cannot interrupt detached online simulation.");
            if(Pending is Live pending){pending.Closed=true;Pending=null;}
            if(Current is Live current){current.Closed=true;Current=null;NativeRegroupSession.OnSessionEnded?.Invoke();}
        }
        static Owned? Find(object state)
        {
            long key=Native.Pointer(state);if(!States.TryGetValue(key,out var entry))return null;
            if(entry.State.Matches(state))return entry;States.Remove(key);return null;
        }
        internal static bool OwnedState(object state)=>Enabled&&Find(state) is not null;
        static void Register(Owner owner,object state)
        {
            owner.Check();Require(Native.Int(Native.Get(state,"Version"))==123,"Unsupported native state version.");
            var frame=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Online state lost its ledger.");
            var ledger=NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision);
            Require(ledger.Epoch==owner.Epoch&&ledger.OnlineContract==owner.Contract,"Online state contract mismatch.");
            NativeLedgerState.Validate(state,frame.Revision,frame.Bytes());
            var old=Find(state);Require(old is null||ReferenceEquals(old.Owner,owner),"Native state crossed online owners.");
            if(old is not null)return;
            foreach(long key in States.Where(x=>x.Value.State.Target() is null).Select(x=>x.Key).ToArray())States.Remove(key);
            Require(States.Count<4096,"Online live state bound exceeded.");
            States.Add(Native.Pointer(state),new(new WeakIdentity(state),owner));NativeBlackout.Bind(state);
        }
        internal static IDisposable BeginCreation(Guid epoch)=>Begin(epoch,true);
        static Scope Begin(Guid epoch,bool creation)
        {
            Require(Enabled&&Environment.CurrentManagedThreadId==MainThread,"Preview callback must execute on the game thread.");
            if(Active is Scope prior){prior.Check();Require(!creation&&prior.Epoch==epoch,"Nested online operation changed game or creation authority.");}
            int depth=0;for(var s=Active;s is not null;s=s.Previous)depth++;
            Require(depth<8,"Online scope nesting bound exceeded.");
            var scope=new Scope(epoch,creation);Active=scope;return scope;
        }
        internal static IDisposable BeginDetached(Guid epoch,byte[] framed,string purpose)
        {
            _=OnlineSnapshotDescriptor.Validate(NativeOnlinePreview.InnerFrame(framed),epoch,NativeOnlinePreview.CurrentContract());
            return Begin(epoch,false);
        }
        internal static IDisposable? BeginCount(byte[] framed)
        {
            byte[] inner=NativeOnlinePreview.InnerFrame(framed);
            var decoded=NativeStateFrame.Decode(inner,123);
            var ledger=NativeIdentityLedgerCodec.Decode(decoded.Extension,decoded.Revision);
            if(ledger.OnlineContract is null)return null;
            return BeginDetached(ledger.Epoch,framed,"summary/count");
        }
        internal static Exception? Finish(IDisposable? value,Exception? error)
        {
            if(value is Scope scope&&error is not null)scope.Faulted=true;
            try{value?.Dispose();}catch(Exception cleanup){return error??cleanup;}return error;
        }
        internal static NativeGameStateEnvelope.Snapshot? Snapshot(object state,bool presentation=false)
        {
            if(!Enabled)return null;
            try
            {
                var owned=Find(state);if(owned is null)return null;owned.Owner.Check(presentation);
                var frame=NativeGameStateEnvelope.Get(state);return frame is not null&&frame.Epoch==owned.Owner.Epoch?frame:null;
            }
            catch{return null;}
        }
        internal static bool Prepare(object state)
        {
            if(!Enabled||Find(state) is not Owned owned)return false;
            owned.Owner.Check();var frame=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Online state has no ledger.");
            var ledger=NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision);
            Require(ledger.Epoch==owned.Owner.Epoch&&ledger.OnlineContract==owned.Owner.Contract,"Online ledger identity changed.");
            var next=NativeLedgerState.Reconcile(state,frame);
            if(next.Changed)NativeGameStateEnvelope.Set(state,next.Revision,next.Payload);
            NativeBlackout.Bind(state);return true;
        }
        internal static Transport? BeforeTransport(object state,bool reading)
        {
            if(!Enabled)return null;
            var prior=Find(state);
            Owner? owner=Active;
            if(owner is null&&prior?.Owner is Live)owner=prior.Owner;
            if(owner is null)owner=Pending;
            if(owner is null&&Current is Live live&&Native.Same(ActiveClient(),live.Client.Target()))owner=live;
            if(owner is null){Require(prior is null,"Retired online state cannot escape its session.");return null;}
            owner.Check();
            if(reading)
            {Require(prior is null&&NativeGameStateEnvelope.Get(state) is null,"Online read receiver was already enrolled.");return new(owner,state,true);}
            if(owner is Scope source&&source.Creation&&!source.Bound)
            {
                Require(source.Generated&&source.Source?.Matches(state)==true&&source.GeneratedMap?.Matches(Native.Get(state,"Map")!)==true,
                    "Online creation writer lacks its completed native generator witness.");
                Require(prior is null&&NativeGameStateEnvelope.Get(state) is null,"Online source was already framed.");
                var ledger=NativeLedgerState.Bootstrap(state,owner.Epoch) with{OnlineContract=owner.Contract};
                try{NativeGameStateEnvelope.Set(state,0,NativeIdentityLedgerCodec.Encode(ledger,0));Register(owner,state);source.Bound=true;}
                catch{States.Remove(Native.Pointer(state));NativeGameStateEnvelope.Forget(state);throw;}
            }
            Require(Find(state) is Owned entry&&ReferenceEquals(entry.Owner,owner),"Online writer lacks exact state ownership.");
            Prepare(state);return new(owner,state,false);
        }
        internal static bool CanDispatch(object? client=null)
        {
            if(!Enabled)return false;
            try{var live=Current;if(live is null||live.Opening||(client is not null&&!live.Client.Matches(client)))return false;live.Check();return true;}catch{return false;}
        }
        internal static void Fault(string reason)
        {
            if(Active is Scope scope)scope.Faulted=true;
            else if(Executing is Execution execution)execution.Owner.Faulted=true;
            else if(Current is Live live&&Native.Same(ActiveClient(),live.Client.Target()))live.Faulted=true;
            else if(Pending is Live pending&&Native.Same(ActiveClient(),pending.Client.Target()))pending.Faulted=true;
            else return;
            // No hidden unit facts in the ordinary online log.
            Entry.Note("online-sync-fault","Synchronization stopped; reopen the match after checking the matching build and connection.");
        }
        internal static void Install(Harmony h)
        {
            if(!NativeRegroupSession.Enabled)return;
            MainThread=Environment.CurrentManagedThreadId;
            if(!NativeOnlinePreview.Install(h))return;
            Type p=typeof(NativeOnlineSession);
            string bytes=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte)).FullName!;
            Native.Hook(h,Native.M("ClientBase","CreateSession",bytes,"Il2CppSystem.Guid"),p,nameof(BeforeOpenBytes),nameof(Created),nameof(OpenFinished));
            Native.Hook(h,Native.M("ClientBase","OpenSession","Il2CppSystem.Guid","PolytopiaBackendBase.Game.GameType"),p,nameof(BeforeOpen),null,nameof(OpenFinished));
            Native.Hook(h,Native.M("ClientBase","UpdateGameStateImmediate","GameState","PolytopiaBackendBase.Game.StateUpdateReason"),p,nameof(BeforeAdopt),nameof(AfterClient));
            foreach(var m in new[]{Native.M("ClientBase","CreateOrResetActionManager","System.UInt16"),Native.M("ClientBase","PrepareSession"),Native.M("ClientBase","SaveSession","System.String","System.Boolean","System.Boolean"),Native.M("ClientBase","PrepareAndValidateSaveState")})
                Native.Hook(h,m,p,nameof(BeforeClient),nameof(AfterClient));
            Native.Hook(h,Native.M("ClientBase","SessionOpened"),p,null,nameof(Opened));
            Native.Hook(h,Native.M("ClientBase","EndSession"),p,null,nameof(Ended));
            Native.Hook(h,Native.M("GameManager","SetRemoteClient","RemoteClient"),p,null,nameof(Published));
            Native.Hook(h,Native.M("ActionManager","Update"),p,nameof(BeforeExecution),null,nameof(ExecutionFinished));
            Native.Hook(h,Native.M("ActionManager","ExecuteCommand","CommandBase","System.String&"),p,nameof(BeforeExecution),null,nameof(ExecutionFinished));
            Native.Hook(h,Native.M("MapGenerator","Generate","GameState","MapGeneratorSettings","Il2CppSystem.Action","System.Int32"),p,nameof(BeforeGeneration),nameof(AfterGeneration),nameof(GenerationFinished));
            Enabled=true;
        }
        static object? Open(object client,object epoch)
        {
            if(!Enabled||!Remote(client))return null;
            Require(Environment.CurrentManagedThreadId==MainThread&&Active is null,"Online open requires the game-thread client boundary.");
            _=ActiveClient(); // Also excludes native fake sessions.
            Guid id=GuidValue(epoch);
            if(Pending is Live old)
            {
                if(old.Client.Matches(client)&&old.Epoch==id&&!old.Faulted&&!old.Closed&&old.Opening)return old;
                old.Closed=true;
            }
            Pending=new Live(client,id);return Pending;
        }
        public static void BeforeOpen(object __instance,object __0,out object? __state)=>__state=Open(__instance,__0);
        public static void BeforeOpenBytes(object __instance,object __1,out object? __state)=>__state=Open(__instance,__1);
        public static Exception? OpenFinished(Exception? __exception,object? __state)
        {if(__exception is not null&&__state is Live live)live.Faulted=true;return __exception;}
        public static void Created(object? __state,object __result)
        {if(__state is Live live){if(Native.Int(__result)!=0){live.Faulted=true;return;}Roles(live,true);}}
        static void Roles(Live live,bool complete=false)
        {
            live.Check();object client=Native.As(live.Client.Target()!,"ClientBase");
            if(complete)Require(Native.Get(client,"initialGameState") is not null&&Native.Get(client,"currentGameState") is not null&&Native.Get(client,"ActionManager") is not null,"Online client completed without required states.");
            foreach(string role in new[]{"initialGameState","currentGameState","targetGameState","lastTurnGameState","GameState"})
                if(Native.Get(client,role) is object state){Require(Prepare(state),"Online client adopted an unframed state.");Register(live,state);}
            if(Native.Get(client,"ActionManager") is object manager&&Native.Get(manager,"GameState") is object managed)
            {Require(Prepare(managed),"Online action manager lacks its framed state.");Register(live,managed);}
        }
        public static bool BeforeAdopt(object __instance,object __0)
        {
            var live=ForClient(__instance);if(!Enabled||live is null)return true;
            try{Require(Prepare(__0),"Unframed online state adoption.");Register(live,__0);return true;}catch{live.Faulted=true;return false;}
        }
        public static bool BeforeClient(object __instance)
        {
            var live=ForClient(__instance);if(!Enabled||live is null)return true;
            try{Roles(live);return true;}catch{live.Faulted=true;return false;}
        }
        public static void AfterClient(object __instance,bool __runOriginal)
        {if(__runOriginal&&Enabled&&ForClient(__instance) is Live live){try{Roles(live);}catch{live.Faulted=true;}}}
        public static void Opened(object __instance)
        {if(Enabled&&ForClient(__instance) is Live live){try{Roles(live,true);}catch{live.Faulted=true;throw;}}}
        static Live? ForClient(object client)=>Pending?.Client.Matches(client)==true?Pending:Current?.Client.Matches(client)==true?Current:null;
        public static void Published(object __0)
        {
            if(!Enabled||Pending is not Live live||!live.Client.Matches(__0))return;
            try
            {
                Require(Native.Same(ActiveClient(),__0),"Online client publication did not select the pending client.");
                Roles(live,true);if(Current is Live old){old.Closed=true;NativeRegroupSession.OnSessionEnded?.Invoke();}
                Current=live;Pending=null;live.Opening=false;
                Entry.Note("online-sync-ready","Matching framed online session ready.");
            }
            catch{live.Faulted=true;throw;}
        }
        public static void Ended(object __instance)
        {if(Pending is Live pending&&pending.Client.Matches(__instance)){pending.Closed=true;Pending=null;}if(Current is Live live&&live.Client.Matches(__instance)){live.Closed=true;Current=null;NativeRegroupSession.OnSessionEnded?.Invoke();}}
        public static void BeforeGeneration(object __0,object? __2,int __3,out object? __state)
        {
            __state=null;if(!Enabled||Active is not Scope scope||!scope.Creation)return;
            scope.Check();Require(scope.Source is null&&__2 is null&&__3==-1&&Native.Int(Native.Get(__0,"Version"))==123,"Unexpected online generator source.");
            Require(Find(__0) is null&&NativeGameStateEnvelope.Get(__0) is null,"Online generator reused a framed state.");scope.Source=new(__0);__state=scope;
        }
        public static void AfterGeneration(object __0,object? __state,bool __runOriginal)
        {
            if(__state is not Scope scope)return;scope.Check();Require(__runOriginal&&scope.Source?.Matches(__0)==true,"Online generator did not complete.");
            scope.GeneratedMap=new(Native.Get(__0,"Map")!);scope.Generated=true;
        }
        public static Exception? GenerationFinished(Exception? __exception,object? __state)
        {if(__exception is not null&&__state is Scope scope)scope.Faulted=true;return __exception;}
        public static void BeforeExecution(object __instance,out object? __state)
        {
            __state=null;if(!Enabled)return;
            object? state=Native.Get(__instance,"GameState");var owned=state is null?null:Find(state);
            Owner? owner=owned?.Owner;
            if(owner is null)
            {
                if(Active is null)return;
                // The exact preview starts its freshly generated match before
                // the first serialization creates the identity ledger.
                var source=Active;
                Require(source.Creation&&!source.Bound&&source.Generated&&state is not null&&
                    source.Source?.Matches(state)==true&&source.GeneratedMap?.Matches(Native.Get(state!,"Map")!)==true,
                    "Detached execution cannot borrow an unowned manager state.");
                owner=source;
            }
            owner.Check();var execution=new Execution(owner,Executing,Environment.CurrentManagedThreadId);Executing=execution;__state=execution;
        }
        public static Exception? ExecutionFinished(Exception? __exception,object? __state)
        {
            if(__state is not Execution execution)return __exception;
            try
            {
                Require(ReferenceEquals(Executing,execution)&&execution.Thread==Environment.CurrentManagedThreadId,"Online execution cleanup order changed.");
                if(__exception is not null)execution.Owner.Faulted=true;
            }
            catch(Exception e){execution.Owner.Faulted=true;return __exception??e;}
            finally{Executing=execution.Previous;}
            return __exception;
        }
    }
}
