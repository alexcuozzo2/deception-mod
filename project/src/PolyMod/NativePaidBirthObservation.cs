using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Read-only evidence for one private, empty-city paid
    // TrainAction. No native allocation/writes, ledger attachment or permission
    // is supplied here. The root router must hold its mutation/save barrier
    // until this scope is disposed, including after native failure.
    internal static class NativePaidBirthObservation
    {
        // Supplied by the exact private execution router, not a native permission API.
        // Must reject unless permit still exclusively owns this exact detached
        // state/action under the current site-3 raw invocation on its owner
        // thread. Also reject published/ever-adopted/aliased states, nested
        // execution, revoked tokens, and changed player/tile/data identities.
        // This method must inspect authority only; it must not acquire it.
        internal interface IAuthority
        {
            void RequirePrivateAction(object permit,object action,object state,
                object player,object tile,object data);
        }

        internal sealed record Witness(object Spawn,long Calls,long Returns,long Finalizers,
            int CurrencyBefore,int CurrencyAfter,uint CounterBefore,uint SpawnedId);
        internal readonly record struct Point(int X,int Y);
        static Point Position(object p)=>new(Native.Int(Native.Get(p,"X")),Native.Int(Native.Get(p,"Y")));
        static readonly object Gate=new();
        static Scope? Active;
        const int MaximumOpenHelpers=8;
        static void Require(bool value,string reason)
        {if(!value)throw new InvalidDataException("Private paid-birth observation: "+reason);}

        // A single process-wide observation is deliberate: an unexpected helper
        // on another thread/state during the native call must invalidate the
        // trial, not silently disappear behind a ThreadStatic-only observer.
        internal static Scope Begin(IAuthority authority,object permit,object action,
            object state,object player,object tile,object data,string expectedRule)
        {
            ArgumentNullException.ThrowIfNull(authority);ArgumentNullException.ThrowIfNull(permit);
            lock(Gate)
            {
                if(Active is not null){Active.Fail("nested-observation");throw new InvalidOperationException("Paid-birth observation already active.");}
                var scope=new Scope(authority,permit,action,state,player,tile,data,expectedRule);
                Active=scope;return scope;
            }
        }

        internal sealed class Scope:IDisposable
        {
            internal IAuthority? Authority;
            internal object? Permit,Action,State,Player,Tile,Data,Spawn;
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal readonly string Rule;
            internal readonly Point City;
            internal readonly uint Turn,CounterBefore;
            internal readonly ushort Command;
            internal readonly byte Owner;
            internal readonly int CurrencyBefore;
            internal readonly HashSet<Helper> Open=new();
            internal long Calls,Returns,Finalizers;
            internal string? Failure;
            internal bool Disposed,Completed;

            internal Scope(IAuthority authority,object permit,object action,object state,
                object player,object tile,object data,string rule)
            {
                Authority=authority;Permit=permit;Action=action;State=state;Player=player;Tile=tile;Data=data;Rule=rule;
                Require(rule is "dcp_mimic_form_warrior" or "dcp_mimic_form_rider","Only the two reviewed paid carriers are admitted.");
                authority.RequirePrivateAction(permit,action,state,player,tile,data);
                Owner=Convert.ToByte(Native.Get(action,"PlayerId"));City=Position(Native.Get(action,"Coordinates")!);
                Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));Command=Convert.ToUInt16(Native.Get(state,"CurrentCommand"));
                CounterBefore=Convert.ToUInt32(Native.Get(state,"CurrentUnitId"));CurrencyBefore=Native.Int(Native.Get(player,"Currency"));
                CheckArguments();
                Require(Native.Get(tile,"unit") is null,"The native city is occupied.");
                Require(CurrencyBefore>=RecruitmentCatalog.Cost&&CounterBefore<uint.MaxValue,"Insufficient funds or exhausted native ID range.");
            }

            internal void Fail(string reason)=>Interlocked.CompareExchange(ref Failure,reason,null);
            internal void CheckAuthority()
            {
                Require(!Disposed&&!Completed&&Environment.CurrentManagedThreadId==Thread,"Scope lifetime or thread differs.");
                Require(ReferenceEquals(Active,this),"Scope is not the active observation.");
                Authority!.RequirePrivateAction(Permit!,Action!,State!,Player!,Tile!,Data!);
            }
            internal void CheckArguments()
            {
                Require(Native.Int(Native.Get(State!,"Version"))==123&&Native.Int(Native.Get(State!,"CurrentPlayer"))==Owner&&
                    Convert.ToUInt32(Native.Get(State!,"CurrentTurn"))==Turn&&Convert.ToUInt16(Native.Get(State!,"CurrentCommand"))==Command,
                    "Supplied action context changed.");
                Require(Native.Int(Native.Get(Action!,"PlayerId"))==Owner&&Native.Int(Native.Get(Player!,"Id"))==Owner&&
                    Native.Int(Native.Get(Action!,"Cost"))==RecruitmentCatalog.Cost&&!(bool)Native.Get(Action!,"trainActionAborted")!&&
                    Native.EnumName(Native.Get(Action!,"Type")!)==Rule&&Native.EnumName(Native.Get(Data!,"type")!)==Rule&&
                    Position(Native.Get(Action!,"Coordinates")!)==City,"Action/player/data fields differ.");
                object?[] playerArgs={Owner,null};
                Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),State,playerArgs)!
                    &&Native.Same(playerArgs[1],Player),"Player is not the exact supplied-state player.");
                Require(Native.Same(Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,
                    Native.Get(State!,"Map"),Native.Get(Action!,"Coordinates")),Tile),"Tile is not the exact supplied-state tile.");
            }
            internal void Match(object state,object player,object tile,object data)
            {
                CheckAuthority();
                Require(Native.Same(state,State)&&Native.Same(player,Player)&&Native.Same(tile,Tile)&&Native.Same(data,Data),
                    "Native helper arguments differ from the authorized call.");
            }

            // Call from typed execution IOperation.Finish BEFORE any ledger Set.
            // This is the throwing correctness boundary. Helper callbacks only
            // latch failures, so their framework return cannot imply success.
            // Success atomically seals the raw-original observation window.
            // Subsequent validation/publication must not call TrainUnit; the
            // outer authority (not this sealed witness) owns that later window.
            internal Witness RequireCompleted(bool originalRan)
            {
                lock(Gate)
                {
                    CheckAuthority();CheckArguments();
                    Require(originalRan,"TrainAction original did not run.");
                    Require(Failure is null,"Helper evidence failed: "+Failure);
                    Require(Calls==1&&Returns==1&&Finalizers==1&&Open.Count==0&&Spawn is not null,
                        "Exactly one completed native helper was not observed.");
                    uint id=Convert.ToUInt32(Native.Get(Spawn!,"id"));int after=Native.Int(Native.Get(Player!,"Currency"));
                    Require(id==CounterBefore+1&&Convert.ToUInt32(Native.Get(State!,"CurrentUnitId"))==id&&
                        (long)CurrencyBefore-after==RecruitmentCatalog.Cost,"Native counter or exact purchase debit differs.");
                    Require(Native.Same(Native.Get(Tile!,"unit"),Spawn)&&Native.Same(Native.Get(Spawn!,"UnitData"),Data)&&
                        Native.EnumName(Native.Get(Spawn!,"type")!)==Rule&&Native.Int(Native.Get(Spawn!,"owner"))==Owner&&
                        Position(Native.Get(Spawn!,"coordinates")!)==City&&Position(Native.Get(Spawn!,"home")!)==City,
                        "Returned newborn is not the exact new native city body.");
                    var witness=new Witness(Spawn!,Calls,Returns,Finalizers,CurrencyBefore,after,CounterBefore,id);
                    Completed=true;Active=null;return witness;
                }
            }

            // Always call from the bridge operation's exactly-once Cleanup.
            // No native rollback is attempted. Failed private states must be
            // quarantined/discarded by the authority, never published or saved.
            public void Dispose()
            {
                lock(Gate)
                {
                    if(Disposed)return;
                    if(Environment.CurrentManagedThreadId!=Thread)Fail("cleanup-thread-differs");
                    if(Open.Count!=0)Fail("cleanup-with-open-helper");
                    if(ReferenceEquals(Active,this))Active=null;else if(!Completed)Fail("cleanup-scope-differs");
                    Disposed=true;Open.Clear();Authority=null;Permit=null;Action=null;State=null;Player=null;Tile=null;Data=null;Spawn=null;
                }
            }
        }

        internal sealed class Helper
        {
            internal readonly Scope Scope;
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal bool Returned,Finished;
            internal Helper(Scope scope){Scope=scope;}
        }

        // Exact target (already pinned): static ActionUtils.TrainUnit(
        // GameState,PlayerState,TileData,UnitData) -> UnitState. Register these
        // three methods together; there is deliberately no Install method.
        public static void BeforeTrainUnit(object __0,object __1,object __2,object __3,out object? __state)
        {
            __state=null;Scope? scope=null;
            try
            {
                lock(Gate)
                {
                    scope=Active;if(scope is null)return;
                    Interlocked.Increment(ref scope.Calls);
                    if(scope.Calls!=1)scope.Fail("extra-native-helper-call");
                    if(scope.Open.Count>=MaximumOpenHelpers){scope.Fail("native-helper-depth-limit");return;}
                    var frame=new Helper(scope);scope.Open.Add(frame);__state=frame;
                    scope.Match(__0,__1,__2,__3);
                }
            }
            catch{scope?.Fail("helper-prefix-failed");}
        }

        public static void AfterTrainUnit(object __0,object __1,object __2,object __3,
            object? __result,bool __runOriginal,object? __state)
        {
            if(__state is not Helper frame)return;
            Scope scope=frame.Scope;
            try
            {
                lock(Gate)
                {
                    if(scope.Completed||scope.Disposed)return;
                    Require(!frame.Returned&&!frame.Finished&&scope.Open.Contains(frame)&&frame.Thread==Environment.CurrentManagedThreadId,
                        "Helper return ordering/thread differs.");
                    frame.Returned=true;Interlocked.Increment(ref scope.Returns);
                    scope.Match(__0,__1,__2,__3);
                    Require(__runOriginal&&__result is not null,"Helper was skipped or returned no body.");
                    if(scope.Spawn is null)scope.Spawn=__result;else scope.Fail("multiple-native-results");
                }
            }
            catch{scope.Fail("helper-postfix-failed");}
        }

        public static Exception? TrainUnitFinished(Exception? __exception,object? __state)
        {
            if(__state is not Helper frame)return __exception;
            Scope scope=frame.Scope;
            try
            {
                lock(Gate)
                {
                    if(scope.Completed||scope.Disposed)return __exception;
                    Require(!frame.Finished&&scope.Open.Remove(frame)&&frame.Thread==Environment.CurrentManagedThreadId,
                        "Helper finalizer ordering/thread differs.");
                    frame.Finished=true;Interlocked.Increment(ref scope.Finalizers);
                    if(__exception is not null)scope.Fail("native-helper-exception");
                    if(!frame.Returned)scope.Fail("native-helper-no-postfix");
                    scope.CheckAuthority();
                }
            }
            catch{scope.Fail("helper-finalizer-failed");}
            return __exception; // Preserve the exact incoming native/managed error.
        }
    }
}
