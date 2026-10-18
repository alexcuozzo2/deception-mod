using System;
using System.Collections.Generic;
using System.IO;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Native transport ownership policy. Contexts are armed only by the exact
    // disposable diagnostic until lifecycle/adoption hooks are proven separately.
    // This policy never adopts a client state or creates a ledger binding.
    internal static class NativeLedgerOwnership
    {
        const string WeakType="Il2CppSystem.WeakReference";
        const int MaxAdopted=4096;
        static readonly object Gate=new();
        static readonly Dictionary<long,Adoption> Adopted=new();
        static Context? Current;
        static long NextGeneration,PolicySerial;
        [ThreadStatic] static DiagnosticRead? ReadPermit;

        // A managed wrapper of a native WeakReference roots the weak-reference
        // object, not its target. Never retain a Target result in this registry.
        internal sealed class WeakIdentity
        {
            internal readonly long Key;
            readonly object Weak;
            internal WeakIdentity(object target)
            {
                Key=Native.Pointer(target);Require(Key!=0,"Null native ownership target.");
                Weak=Native.T(WeakType).GetConstructor(new[]{Native.T("Il2CppSystem.Object"),typeof(bool)})!
                    .Invoke(new[]{target,(object)false});
                Require(Matches(target),"Native weak ownership target did not round-trip.");
            }
            internal object? Target()
            {
                // IsAlive is not a lifetime lock. Read Target once and keep it
                // local; never construct a wrapper from the remembered Key.
                object? target=Native.Get(Weak,"Target");
                if(target is not null)Require(Native.Pointer(target)==Key,"Native weak target changed identity.");
                return target;
            }
            internal bool Matches(object requested)
            {
                object? target=Target();
                try{return target is not null&&Native.Same(target,requested);}
                finally{GC.KeepAlive(target);GC.KeepAlive(requested);}
            }
        }

        // The token carries only managed values and a native weak client handle.
        // Generation is process-local, unlike the persisted ledger Epoch.
        internal sealed class Context
        {
            internal readonly Guid GameId,Epoch;
            internal readonly long Generation;
            internal readonly int Thread;
            internal readonly WeakIdentity Client;
            internal bool Retired,Faulted;
            internal Context(object client,Guid gameId,Guid epoch,long generation)
            {Client=new(client);GameId=gameId;Epoch=epoch;Generation=generation;Thread=Environment.CurrentManagedThreadId;}
        }
        sealed record Adoption(WeakIdentity State,Context Owner);

        static void Require(bool condition,string reason)
        {if(!condition)throw new InvalidDataException("Native ledger ownership: "+reason);}
        static object State(object value)
        {
            object state=Native.As(value,"GameState");
            Require(Native.Pointer(state)!=0,"Missing GameState identity.");return state;
        }
        static Guid GameId(object client)
        {
            object value=Native.Get(client,"gameId")??throw new InvalidDataException("Missing native client game Guid.");
            string? text=(string?)Native.Invoke(Native.M("Il2CppSystem.Guid","ToString"),value);
            Require(Guid.TryParseExact(text,"D",out var id)&&id!=Guid.Empty,"Invalid native client game Guid.");return id;
        }
        static void LocalClient(object client,Guid expected)
        {
            Require(expected!=Guid.Empty&&GameId(client)==expected,"Native client game Guid differs from the supplied context.");
            Require(Equals(Native.Get(client,"clientType"),Enum.Parse(Native.T("ClientBase+ClientType"),"Local"))&&
                !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!,
                "Only the exact Local, non-replay, non-spectating client is supported.");
            // Deliberately no IsReady/IsWaitingForCommand/idle requirement:
            // required serialization occurs during creation and native actions.
        }
        static object? ActiveClient()
        {
            // GameManager.Client/GameState native getters throw when the
            // singleton itself is absent. This generated STATIC FIELD getter
            // only reads the native instance field and does not create one.
            if(Native.Static("GameManager","instance") is null)return null;
            return Native.Static("GameManager","Client");
        }
        static object ContextClient(Context context)
        {
            Require(ReferenceEquals(Current,context)&&!context.Retired&&!context.Faulted,"Context is retired, superseded, or faulted.");
            Require(context.Thread==Environment.CurrentManagedThreadId,"Guarded ownership crossed its native owner thread.");
            object target=context.Client.Target()??throw new InvalidDataException("Guarded native client expired.");
            object client=Native.As(target,"ClientBase");LocalClient(client,context.GameId);
            object? active=ActiveClient();
            Require(active is null||Native.Same(active,client),"Another client is active during the required context.");return client;
        }
        static void Sweep()
        {
            var dead=new List<long>();
            foreach(var pair in Adopted)if(pair.Value.State.Target() is null)dead.Add(pair.Key);
            foreach(long key in dead)Adopted.Remove(key);
            // Any reflection/liveness exception fails the operation; it is not
            // evidence permitting eviction of an existing live adoption.
        }
        static Adoption? AdoptionOf(object state)
        {
            long key=Native.Pointer(state);
            if(!Adopted.TryGetValue(key,out var entry))return null;
            if(entry.State.Matches(state))return entry;
            // Matches can be false here only for a dead prior weak target or
            // an explicitly different requested identity, never wrapper churn.
            Adopted.Remove(key);return null;
        }
        static NativeGameStateEnvelope.Snapshot Binding(object state,Guid epoch)
        {
            var binding=NativeGameStateEnvelope.Get(state);
            Require(binding is not null,"Required state has no ledger binding.");
            var ledger=NativeIdentityLedgerCodec.Decode(binding!.Bytes(),binding.Revision);
            Require(ledger.Epoch==epoch,"Required state has a different ledger Epoch.");return binding;
        }

        // Integration must call this at an audited boundary BEFORE the first
        // required writer, with gameId already assigned to this native client.
        // This method does not authorize or implement create/open/adoption.
        internal static Context BeginRequiredContext(object suppliedClient,Guid gameId,Guid epoch)
        {
            NativeGameStateEnvelope.BeforePrivateContextChange();
            lock(Gate)
            {
                Require(Current is null,"Retire the exact old context before starting another generation.");
                Require(epoch!=Guid.Empty,"A known nonempty ledger Epoch is required by this first draft.");
                object client=Native.As(suppliedClient,"ClientBase");LocalClient(client,gameId);
                object? active=ActiveClient();
                Require(active is null||Native.Same(active,client),"Another native client is already active.");
                Require(NextGeneration<long.MaxValue&&PolicySerial<long.MaxValue,"Ownership generation exhausted.");
                var context=new Context(client,gameId,epoch,NextGeneration+1);
                NextGeneration++;PolicySerial++;Current=context;return context;
            }
        }
        internal static void Fault(Context context)
        {
            lock(Gate){Require(ReferenceEquals(Current,context)&&!context.Retired,"Cannot fault a different context.");context.Faulted=true;}
        }
        internal static void Retire(Context context)
        {
            NativeGameStateEnvelope.BeforePrivateContextChange();
            lock(Gate)
            {
                Require(ReferenceEquals(Current,context)&&!context.Retired,"Cannot retire a different context.");
                Require(PolicySerial<long.MaxValue,"Ownership generation exhausted.");
                context.Retired=true;Current=null;PolicySerial++;
                // Do not clear Adopted or envelope bindings: still-live old
                // snapshots remain protected after menu/reset/reload.
            }
        }
        internal static void RecordAdopted(Context context,object suppliedState)
        {
            NativeGameStateEnvelope.RejectPrivateAccess(suppliedState);
            lock(Gate)
            {
                _=ContextClient(context);object state=State(suppliedState);
                var binding=Binding(state,context.Epoch);
                NativeLedgerState.Validate(state,binding.Revision,binding.Bytes());
                var prior=AdoptionOf(state);
                Require(prior is null||ReferenceEquals(prior.Owner,context),"State was already adopted by another generation.");
                if(prior is not null)return;
                Sweep();Require(Adopted.Count<MaxAdopted,"Too many live adopted states; none may be silently evicted.");
                Adopted.Add(Native.Pointer(state),new(new WeakIdentity(state),context));
            }
        }

        static bool ClientContains(object client,object state)
        {
            // Read a fresh census on EVERY boundary. Native code writes these
            // fields directly; a generated setter hook cannot establish ownership.
            foreach(string field in new[]{"GameState","initialGameState","currentGameState","targetGameState","lastTurnGameState"})
                if(Native.Same(Native.Get(client,field),state))return true;
            var manager=Native.Get(client,"ActionManager");
            return manager is not null&&Native.Same(Native.Get(manager,"GameState"),state);
        }
        static bool Published(object state,object? contextClient)
        {
            object? active=null;
            if(Native.Static("GameManager","instance") is not null)
            {
                if(Native.Same(Native.Static("GameManager","GameState"),state))return true;
                active=Native.Static("GameManager","Client");
            }
            if(active is not null&&ClientContains(Native.As(active,"ClientBase"),state))return true;
            return contextClient is not null&&!Native.Same(active,contextClient)&&ClientContains(contextClient,state);
        }
        static object? FreshContextClient()=>Current is null?null:ContextClient(Current);

        // A private mutation claim needs an existing current context but can
        // never use a current or previously adopted state as its receiver.
        internal static Context PrivateContext(object suppliedState)
        {
            lock(Gate)
            {
                var context=Current??throw new InvalidDataException("Private trial requires an active native ownership context.");
                object state=State(suppliedState),client=ContextClient(context);
                Require(!Published(state,client)&&AdoptionOf(state) is null,"Private trial receiver is published or was adopted.");
                return context;
            }
        }
        internal static void RequirePrivateContext(Context expected,object state)
        {Require(ReferenceEquals(PrivateContext(state),expected),"Private trial ownership generation changed.");}

        // Call BEFORE operation-cookie allocation, stream wrapping/access,
        // EnterOriginal, and envelope Forget. Exceptions go to the existing
        // native bridge's contained Prepare callback and native rejection path.
        internal static void BeforeRead(object suppliedReceiver)
        {
            NativeGameStateEnvelope.RejectPrivateAccess(suppliedReceiver);
            lock(Gate)
            {
                object receiver=State(suppliedReceiver);object? contextClient=FreshContextClient();
                Require(!Published(receiver,contextClient),"Deserialize receiver is a published client/manager state.");
                Require(AdoptionOf(receiver) is null,"Deserialize receiver was already adopted while still live.");
                if(NativeGameStateEnvelope.Get(receiver) is null)return;
                var permit=ReadPermit;
                Require(permit is not null&&permit.Authorize(receiver),"Deserialize receiver already has a binding.");
                // Consumed even if frame parsing later fails. Native decode may
                // mutate this explicitly private receiver; discard it on failure.
            }
        }
        internal static void BeforeWrite(object suppliedState)
        {
            NativeGameStateEnvelope.RejectPrivateAccess(suppliedState);
            lock(Gate)
            {
                object state=State(suppliedState);var adopted=AdoptionOf(state);
                Require(adopted is null||!adopted.Owner.Faulted,"Writer belongs to a faulted adopted context, including after retirement.");
                if(Current is not null)
                {
                    _=ContextClient(Current);
                    Require(adopted is null||ReferenceEquals(adopted.Owner,Current),"Writer belongs to another adopted generation.");
                    _=Binding(state,Current.Epoch);
                    // All states require bindings while this explicit one-client
                    // context is armed, including a first not-yet-adopted copy.
                    // No global GameManager match is used to grant authority.
                }
                else if(adopted is not null)_=Binding(state,adopted.Owner.Epoch);
                // Existing envelope validation remains required afterward.
                // Outside any context, unadopted ordinary native writers pass.
            }
        }

        // Called before native decoding, and again before publishing a decoded
        // binding. Historical revisions remain independent; Epoch is the match
        // identity and must agree with the explicit required context.
        internal static void BeforeFrame(long revision,byte[] payload)
        {
            lock(Gate)
            {
                if(Current is null)return;
                _=ContextClient(Current);
                var ledger=NativeIdentityLedgerCodec.Decode(payload,revision);
                Require(ledger.Epoch==Current.Epoch,"Decoded frame belongs to a different ledger Epoch.");
            }
        }
        internal static void BeforeLegacyRead()
        {
            lock(Gate)
            {
                if(Current is null)return;
                _=ContextClient(Current);
                throw new InvalidDataException("Native ledger ownership: Required context cannot decode an unbound legacy state.");
            }
        }

        // No environment-wide reader bypass. Each scope names one seeded native
        // receiver, exact live disposable client/Guid, thread, and policy serial.
        // It can authorize ONE bound-receiver read and never publication/adoption.
        internal sealed class DiagnosticRead : IDisposable
        {
            readonly WeakIdentity Receiver,Client;
            readonly Guid ExpectedGame;
            readonly long Serial;
            readonly int Thread;
            readonly DiagnosticRead? Previous;
            bool Used,Disposed;
            internal DiagnosticRead(object receiver,object client,Guid game)
            {
                Receiver=new(receiver);Client=new(client);ExpectedGame=game;
                Serial=PolicySerial;Thread=Environment.CurrentManagedThreadId;Previous=ReadPermit;
            }
            internal bool Authorize(object receiver)
            {
                if(Used||Disposed||!ReferenceEquals(ReadPermit,this)||Thread!=Environment.CurrentManagedThreadId||Serial!=PolicySerial||!Receiver.Matches(receiver))return false;
                object? nativeClient=Client.Target();if(nativeClient is null)return false;
                object client=Native.As(nativeClient,"ClientBase");LocalClient(client,ExpectedGame);
                if(!Native.Same(ActiveClient(),client))return false;
                // Even an unconsumed permit cannot defeat a field/adoption
                // change that occurred between issuance and reader entry.
                if(Published(receiver,client)||AdoptionOf(receiver) is not null)return false;
                Used=true;return true;
            }
            public void Dispose()
            {
                lock(Gate)
                {
                    if(Disposed)return;
                    Require(Thread==Environment.CurrentManagedThreadId&&ReferenceEquals(ReadPermit,this),"Diagnostic scopes must unwind synchronously in order.");
                    ReadPermit=Previous;Disposed=true;
                }
            }
        }
        internal static DiagnosticRead PermitSeededPrivateRead(object suppliedClient,Guid disposableGameId,object suppliedReceiver)
        {
            lock(Gate)
            {
                Require(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_ENVELOPE_PROBE")=="1"||
                    Environment.GetEnvironmentVariable("DECEPTION_NATIVE_LEDGER_PROBE")=="1"||
                    Environment.GetEnvironmentVariable("DECEPTION_NATIVE_COMMAND_PROBE")=="1"||
                    Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXCHANGE_PROBE")=="1","Detached diagnostic is not explicitly enabled.");
                object client=Native.As(suppliedClient,"ClientBase"),receiver=State(suppliedReceiver);
                LocalClient(client,disposableGameId);
                Require(Native.Same(ActiveClient(),client),"Diagnostic client is not the exact active client.");
                object? requiredClient=FreshContextClient();
                Require(requiredClient is null||Native.Same(requiredClient,client),"Diagnostic client differs from the required context.");
                Require(!Published(receiver,client)&&AdoptionOf(receiver) is null,"Diagnostic receiver is published or was adopted.");
                Require(NativeGameStateEnvelope.Get(receiver) is not null,"Diagnostic receiver must already have an explicit private binding.");
                var permit=new DiagnosticRead(receiver,client,disposableGameId);ReadPermit=permit;return permit;
            }
        }
    }
}
