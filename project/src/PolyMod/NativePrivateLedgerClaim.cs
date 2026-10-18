using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    internal static partial class NativeGameStateEnvelope
    {
        // Deliberately narrower than a live transaction: claimed receivers are
        // never serializable/adoptable, even after successful publication. The
        // only readers/writer hold this exact private claim. No Train-v1 save
        // format or occupied pending payment is implied by an in-memory commit.
        const int MaxPrivateClaims=48;
        static readonly Dictionary<long,PrivateClaim> PrivateClaims=new();

        internal sealed class PrivateClaim
        {
            readonly long Key;
            readonly int Thread;
            readonly NativeLedgerOwnership.Context Context;
            internal readonly object WeakState;
            Binding Current;
            bool Published,Faulted;
            internal bool Retired;

            internal sealed class Staged
            {
                internal readonly PrivateClaim Owner;
                internal readonly Binding Before,After;
                internal Staged(PrivateClaim owner,Binding before,Binding after)
                {Owner=owner;Before=before;After=after;}
            }
            internal PrivateClaim(object state,Binding binding,NativeLedgerOwnership.Context context)
            {Key=Native.Pointer(state);Thread=Environment.CurrentManagedThreadId;WeakState=binding.WeakState;Current=binding;Context=context;}

            object Target()
            {
                object? state=Native.Get(WeakState,"Target");
                if(state is null||Native.Pointer(state)!=Key)throw new InvalidDataException("Private ledger receiver expired or changed identity.");
                return state;
            }
            void RequireOwner(object state)
            {
                lock(States)
                {
                    if(Thread!=Environment.CurrentManagedThreadId||Retired||Faulted||
                        !PrivateClaims.TryGetValue(Key,out var owner)||!ReferenceEquals(owner,this)||!Native.Same(Target(),state)||
                        !States.TryGetValue(Key,out var binding)||!ReferenceEquals(binding,Current))
                        throw new InvalidDataException("Private ledger claim is not the exact active receiver/binding/thread.");
                }
                NativeLedgerOwnership.RequirePrivateContext(Context,state);
            }
            internal Snapshot Read(object state)
            {RequireOwner(state);RequireEpoch(Current.Data.Revision,Current.Data.Bytes());return new(Current.Data.Revision,Current.Data.Bytes());}
            void RequireEpoch(long revision,byte[] payload)
            {
                if(NativeIdentityLedgerCodec.Decode(payload,revision).Epoch!=Context.Epoch)
                    throw new InvalidDataException("Private ledger payload belongs to a different ownership epoch.");
            }

            // Prepare storage before the native original. State validation is
            // deferred until the body really exists; these bytes stay private.
            internal Staged Stage(object state,long revision,byte[] payload)
            {
                RequireOwner(state);
                ArgumentNullException.ThrowIfNull(payload);
                byte[] owned=(byte[])payload.Clone();
                Validate(revision,owned);RequireEpoch(revision,owned);
                RequireOwner(state);
                if(Published||revision!=checked(Current.Data.Revision+1))throw new InvalidDataException("Private ledger publication must advance exactly once.");
                return new(this,Current,new(WeakState,new(revision,owned)));
            }
            internal void Publish(object state,Staged prepared,long revision,byte[] payload)
            {
                RequireOwner(state);
                ArgumentNullException.ThrowIfNull(payload);
                byte[] submitted=(byte[])payload.Clone(),owned=prepared.After.Data.Bytes();
                if(Published||!ReferenceEquals(prepared.Owner,this)||!ReferenceEquals(prepared.Before,Current)||
                    prepared.After.Data.Revision!=revision||!owned.SequenceEqual(submitted))
                    throw new InvalidDataException("Private ledger publication differs from its staged result.");
                Validate(revision,owned);RequireEpoch(revision,owned);ValidateState?.Invoke(state,revision,(byte[])owned.Clone());
                RequireOwner(state);
                lock(States)
                {
                    if(Published||Faulted||Retired||Context.Faulted||Context.Retired||!ReferenceEquals(States[Key],prepared.Before))
                        throw new InvalidDataException("Private ledger base or ownership changed before publication.");
                    // Existing dictionary slot and immutable binding were
                    // reserved before native mutation. No public Set is used.
                    States[Key]=prepared.After;Current=prepared.After;Published=true;
                }
            }
            internal void Quarantine()
            {
                // Latch even if a surrounding lifecycle/context already failed.
                // Quarantine cannot turn a failed action into a successful one.
                lock(States)Faulted=true;
            }
            internal void Retire(object state)
            {
                lock(States)
                {
                    if(Thread!=Environment.CurrentManagedThreadId||!PrivateClaims.TryGetValue(Key,out var owner)||
                        !ReferenceEquals(owner,this)||!Native.Same(Target(),state))
                        throw new InvalidDataException("Cannot retire a different private ledger receiver.");
                    Faulted=true;Retired=true;
                    if(States.TryGetValue(Key,out var binding))
                    {
                        if(!ReferenceEquals(binding,Current))throw new InvalidDataException("Private ledger retirement found a replaced binding.");
                        States.Remove(Key);
                    }
                }
                // Keep a weak tombstone: leaked receivers stay rejected after
                // retirement. Never rebuild a native object from the stored key.
            }
            internal bool RetiredWithoutBinding
            {get{lock(States)return Retired&&!States.ContainsKey(Key);}}
        }

        internal static PrivateClaim ClaimPrivate(object state)
        {
            RejectPrivateAccess(state);
            var context=NativeLedgerOwnership.PrivateContext(state);
            var before=Get(state)??throw new InvalidDataException("Private claim requires an existing validated ledger binding.");
            if(NativeIdentityLedgerCodec.Decode(before.Bytes(),before.Revision).Epoch!=context.Epoch)
                throw new InvalidDataException("Private claim binding belongs to a different ownership epoch.");
            ValidateState?.Invoke(state,before.Revision,before.Bytes());
            NativeLedgerOwnership.RequirePrivateContext(context,state);
            lock(Operations)
                if(Operations.Values.Any(op=>Native.Same(op.State,state)))throw new InvalidDataException("Cannot claim an active transport receiver.");
            lock(States)
            {
                foreach(long dead in PrivateClaims.Where(x=>Native.Get(x.Value.WeakState,"Target") is null).Select(x=>x.Key).ToArray())PrivateClaims.Remove(dead);
                long key=Native.Pointer(state);
                if(PrivateClaims.Count>=MaxPrivateClaims||PrivateClaims.ContainsKey(key)||!States.TryGetValue(key,out var binding)||
                    !Native.Same(Native.Get(binding.WeakState,"Target"),state)||binding.Data.Revision!=before.Revision||
                    !binding.Data.Bytes().SequenceEqual(before.Bytes()))throw new InvalidDataException("Private claim admission changed identity, binding or capacity.");
                var claim=new PrivateClaim(state,binding,context);PrivateClaims.Add(key,claim);return claim;
            }
        }
        internal static void RejectPrivateAccess(object state)
        {
            lock(States)
            {
                long key=Native.Pointer(state);
                if(!PrivateClaims.TryGetValue(key,out var claim))return;
                object? target=Native.Get(claim.WeakState,"Target");
                if(target is null){PrivateClaims.Remove(key);return;}
                if(Native.Pointer(target)!=key||!Native.Same(target,state))throw new InvalidDataException("Private ledger native identity conflict.");
                throw new InvalidDataException("Private paid-trial state cannot be read, replaced, serialized or adopted outside its exact claim.");
            }
        }
        internal static void BeforePrivateContextChange()
        {
            lock(States)
                if(PrivateClaims.Values.Any(x=>!x.Retired&&Native.Get(x.WeakState,"Target") is not null))
                    throw new InvalidDataException("Cannot change ownership generation during a private paid trial.");
        }
    }
}
