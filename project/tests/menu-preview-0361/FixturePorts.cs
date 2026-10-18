using System;
using System.Collections.Generic;
using System.Reflection;
using Deception.Core;
public class Identity {static long Next;public IntPtr Pointer{get;}=new(++Next);public T Cast<T>() where T:class=>(T)(object)this;}
namespace HarmonyLib
{
    public sealed class HarmonyMethod{public MethodInfo Method;public HarmonyMethod(MethodInfo m){Method=m;}}
    public sealed class Harmony
    {
        public MethodInfo? Target;public HarmonyMethod? Prefix,Postfix,Finalizer;public int Calls;
        public void Patch(MethodInfo m,HarmonyMethod? p,HarmonyMethod? q,object? t,HarmonyMethod? f,object? il){Calls++;Target=m;Prefix=p;Postfix=q;Finalizer=f;}
    }
}
namespace Il2CppSystem {public sealed class Guid {public System.Guid Value;public Guid(System.Guid value){Value=value;}public override string ToString()=>Value.ToString("D");}}
public sealed class SelectTribePopup {public void SetBackground(){throw new Exception("No native popup body should execute in this fixture");}}
public sealed class GameState:Identity {public int Version{get;set;}=123;}
public sealed class ClientBase:Identity
{
    public enum ClientType {Local,Network,PassAndPlay}
    public ClientType clientType{get;set;}=ClientType.Local;public bool IsReplay{get;set;}public bool IsSpectating{get;set;}
    public Il2CppSystem.Guid gameId{get;set;}=new(System.Guid.NewGuid());
}
public sealed class GameManager
{
    public static GameManager? instance{get;set;}=new();public object? fakeSession{get;set;}
    public static ClientBase? Client{get;set;}
}
namespace Deception.PolyModAdapter
{
    internal static class Entry {internal static void Note(string k,string m){ }}
    internal static class NativeLedgerOwnership
    {
        internal sealed class WeakIdentity
        {
            readonly WeakReference<object> Value;internal WeakIdentity(object o){Value=new(o);}
            internal object? Target()=>Value.TryGetTarget(out var o)?o:null;
            internal bool Matches(object o)=>ReferenceEquals(Target(),o);
        }
    }
    // Native graph reconciliation is outside this transport-scope fixture.
    // The real ledger codec and immutable Snapshot are still used below.
    internal static class NativeLedgerState
    {
        internal static int Validations,Reconciliations,Bootstraps;
        internal static void Validate(object state,long revision,byte[] payload){Validations++;NativeIdentityLedgerCodec.Decode(payload,revision);}
        internal static NativeIdentityLedger Bootstrap(object state,Guid epoch){Bootstraps++;return new(epoch,1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>());}
        internal static (bool Changed,long Revision,byte[] Payload) Reconcile(object state,NativeGameStateEnvelope.Snapshot snapshot)
        {Reconciliations++;return(false,snapshot.Revision,snapshot.Bytes());}
    }
    internal static partial class NativeGameStateEnvelope
    {
        internal static readonly Dictionary<object,Snapshot> Frames=new();internal static int Reads,Writes;
        internal static Snapshot? Get(object state){Reads++;return Frames.GetValueOrDefault(state);}
        internal static void Set(object state,long revision,byte[] payload){Writes++;Frames[state]=new(revision,payload);}
        internal static void Forget(object state)=>Frames.Remove(state);
    }
    internal static partial class NativeRegroupSession
    {
        // Only the unrelated paid-observer partial is replaced. Production
        // BeforeTransport, BeforeFrame, BeforeLegacyRead and Completed execute.
        static object? Buying;
        static void InstallPaid(HarmonyLib.Harmony h)=>throw new NotSupportedException("Paid installation is outside this fixture");
        internal static Session FixtureStart(ClientBase client,Guid epoch,bool opening=false)
        {
            Enabled=true;States.Clear();Buying=null;NativeGameStateEnvelope.Frames.Clear();
            NativeGameStateEnvelope.Reads=NativeGameStateEnvelope.Writes=0;
            NativeLedgerState.Validations=NativeLedgerState.Reconciliations=NativeLedgerState.Bootstraps=0;
            GameManager.instance=new();GameManager.Client=client;
            Current=new Session(client,epoch,++NextGeneration,opening,false);return Current;
        }
        internal static void FixtureEnroll(GameState state,Guid epoch)
        {NativeGameStateEnvelope.Set(state,0,NativeIdentityLedgerCodec.Encode(new(epoch,1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>()),0));Register(Current!,state);}
        internal static void FixtureBuying(bool active)=>Buying=active?new object():null;
    }
}
