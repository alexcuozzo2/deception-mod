using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    internal sealed record NativeExchangeCommitResult(bool AlreadyCommitted,long BeforeRevision,long AfterRevision,NativeExchangePlan Plan);

    // Atomic native transaction for an authorized Local Build action or an
    // explicit detached diagnostic. Final-source sight is queued with the
    // state change; ordinary native child processing remains authoritative.
    internal static class NativeExchangeCommit
    {
        internal static Func<object,NativeExchangeIntent,Action?>? PreparePresentation;
        sealed class Trial:IDisposable
        {
            internal readonly object State,Source;
            internal readonly string? FailAfter;
            internal Trial(object state,object source,string? failAfter){State=state;Source=source;FailAfter=failAfter;}
            public void Dispose(){if(!ReferenceEquals(Current,this))throw new InvalidOperationException("Detached exchange scope imbalance.");Current=null;}
        }
        [ThreadStatic] static Trial? Current;
        [ThreadStatic] static bool Committing;
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        internal static IDisposable DetachedTrial(object state,object protectedSource,string? failAfter=null)
        {
            Require(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXCHANGE_PROBE")=="1"&&Current is null,
                "Native exchange execution requires an explicit, nonnested detached trial.");
            if(failAfter is not null&&!new[]{"forms","positions","allowance","sight-a","sight-b","ledger"}.Contains(failAfter))
                throw new InvalidDataException("Unknown exchange fault-injection boundary.");
            Independent(state,protectedSource);
            var trial=new Trial(state,protectedSource,failAfter);Current=trial;return trial;
        }
        static void Independent(object state,object source)
        {
            Require(!Native.Same(state,source)&&!Native.Same(Native.Get(state,"Map"),Native.Get(source,"Map")),"Detached exchange aliases the protected source.");
            foreach(string name in new[]{"Settings","PlayerStates","ActionStack","CommandStack","pendingCommandTriggers"})
                Require(!Native.Same(Native.Get(state,name),Native.Get(source,name)),"Detached exchange shares a mutable native state container.");
            var protectedBodies=NativeLedgerState.Graph(source).Values.Select(x=>Native.Pointer(x.Unit)).ToHashSet();
            foreach(var body in NativeLedgerState.Graph(state).Values)Require(!protectedBodies.Contains(Native.Pointer(body.Unit)),"Detached exchange shares a native body.");
            // Recheck actual publication on every invocation; a trial cannot
            // turn an adopted state into an authorized mutation target.
            var client=Native.Static("GameManager","Client");
            Require(!Native.Same(Native.Static("GameManager","GameState"),state),"A published GameManager state cannot enter a detached exchange.");
            if(client is not null)
            {
                foreach(string name in new[]{"GameState","initialGameState","currentGameState","targetGameState","lastTurnGameState"})
                    Require(!Native.Same(Native.Get(client,name),state),"A published client state cannot enter a detached exchange.");
                var manager=Native.Get(client,"ActionManager");
                if(manager is not null)Require(!Native.Same(Native.Get(manager,"GameState"),state),"An action-manager state cannot enter a detached exchange.");
            }
        }
        sealed class UnitBefore
        {
            internal readonly object Unit,Coordinates,Type,Data,Effects;
            internal readonly bool Moved,Attacked;
            internal UnitBefore(object unit)
            {Unit=unit;Coordinates=Native.Get(unit,"coordinates")!;Type=Native.Get(unit,"type")!;Data=Native.Get(unit,"UnitData")!;
                Effects=Native.Get(unit,"effects")!;Moved=(bool)Native.Get(unit,"moved")!;Attacked=(bool)Native.Get(unit,"attacked")!;}
            internal void Restore()
            {
                Native.Set(Unit,"coordinates",Coordinates);Native.Set(Unit,"type",Type);Native.Set(Unit,"UnitData",Data);
                Native.Set(Unit,"moved",Moved);Native.Set(Unit,"attacked",Attacked);Native.Set(Unit,"effects",Effects);
            }
        }
        static void Fault(string stage){if(Current?.FailAfter==stage)throw new InvalidOperationException("Injected detached exchange failure after "+stage+".");}
        internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason)
        {
            result=null;reason="";NativeExchangePlan? plan=null;UnitBefore? a=null,b=null;
            NativeExchangeConsequences.Pending? sight=null;
            NativeMimicTransport.Pending? coastA=null,coastB=null;
            uint counterBefore=0;Action? present=null;
            byte[]? nativeBefore=null;object? tileUnitA=null,tileUnitB=null;bool changed=false,entered=false;
            try
            {
                var trial=Current;
                bool live=NativeExchangeCommands.LiveExecution(state);
                Require(!Committing&&(live||(trial is not null&&Native.Same(trial.State,state))),"No authorized exchange action or detached execution scope.");
                if(!live)Independent(state,trial!.Source);
                if(intent.Kind==NativeExchangeKind.LighthouseSecret)
                {Committing=true;entered=true;return NativeLighthouseSecret.TryCommit(state,intent,out result,out reason);}
                if(intent.Kind==NativeExchangeKind.Blackmail)
                {Committing=true;entered=true;return NativeBlackmail.TryCommit(state,intent,out result,out reason);}
                if(intent.Kind==NativeExchangeKind.Subvert)
                {Committing=true;entered=true;return NativeSubvert.TryCommit(state,intent,out result,out reason);}
                if(intent.Kind==NativeExchangeKind.Blackout)
                {Committing=true;entered=true;return NativeBlackout.TryCommit(state,intent,out result,out reason);}
                if(NativeExchangeCatalog.IsSupport(intent.Kind))
                {Committing=true;entered=true;return NativeFeignSupport.TryCommit(state,intent,out result,out reason);}
                if(intent.Kind==NativeExchangeKind.ShedDisguise)
                {Committing=true;entered=true;return NativeShedDisguise.TryCommit(state,intent,out result,out reason);}
                if(NativeExchangeCatalog.IsUpgrade(intent.Kind))
                {Committing=true;entered=true;return NativeApparentUpgrades.TryCommit(state,intent,out result,out reason);}
                Require(NativeExchangePlanning.TryPlan(state,intent,out plan,out var why),why);
                if(plan!.AlreadyCommitted){result=new(true,plan.BeforeRevision,plan.AfterRevision,plan);return true;}
                a=new(plan.A!);b=new(plan.B!);tileUnitA=Native.Get(plan.TileA!,"unit");tileUnitB=Native.Get(plan.TileB!,"unit");
                counterBefore=Convert.ToUInt32(Native.Get(state,"CurrentUnitId"));
                object typeA=Native.EnumValue(RecruitmentHooks.UT,plan.FinalMimicCarrierA!),dataA=RecruitmentHooks.Data(state,plan.FinalMimicCarrierA!);
                object typeB=plan.FinalMimicCarrierB is null?b.Type:Native.EnumValue(RecruitmentHooks.UT,plan.FinalMimicCarrierB);
                object dataB=plan.FinalMimicCarrierB is null?b.Data:RecruitmentHooks.Data(state,plan.FinalMimicCarrierB);
                if(NativeRegroupSession.Enabled)
                {
                    object destinationA=intent.Swap?plan.TileB!:plan.TileA!,destinationB=intent.Swap?plan.TileA!:plan.TileB!;
                    if(NativeMimicTransport.IsVessel(a.Unit)||NativeMimicTransport.IsVesselData(RecruitmentHooks.Data(state,plan.FinalTypeA!)))
                        coastA=NativeMimicTransport.StageConversion(state,a.Unit,destinationA,plan.FinalTypeA!,plan.AllocatedWrapperA,false,plan.CandidateLedger.Units.Single(x=>x.LogicalId==intent.Initiator.LogicalId).Appearance?.Passenger is LedgerAppearance ap?RecruitmentCatalog.Variant(ap.Form):null);
                    if(NativeMimicTransport.IsVessel(b.Unit)!=NativeMimicTransport.IsVesselData(RecruitmentHooks.Data(state,plan.FinalTypeB!))||
                        (plan.FinalMimicCarrierB is not null&&NativeMimicTransport.IsVessel(b.Unit)))
                        coastB=NativeMimicTransport.StageConversion(state,b.Unit,destinationB,plan.FinalTypeB!,plan.AllocatedWrapperB,false,plan.CandidateLedger.Units.Single(x=>x.LogicalId==intent.Partner.LogicalId).Appearance?.Passenger is LedgerAppearance bp?RecruitmentCatalog.Variant(bp.Form):null);
                }
                // Native exhaustion removes DoubleReady/Boosted in place. Give
                // it an owned list so the exact old list and any aliased reader
                // remain unchanged if this transaction must roll back.
                object stagedEffects=Native.NewList("UnitEffect",Native.Items(a.Effects));
                sight=NativeExchangeConsequences.Capture(state,plan);
                nativeBefore=NativeGameStateEnvelope.NativeBytes(state,123);
                Require(NativeExchangePlanning.TryPlan(state,intent,out var fresh,out why)&&fresh is not null&&!fresh.AlreadyCommitted&&
                    Native.Same(fresh.A,a.Unit)&&Native.Same(fresh.B,b.Unit)&&fresh.OriginalPayload.SequenceEqual(plan.OriginalPayload)&&
                    fresh.CandidatePayload.SequenceEqual(plan.CandidatePayload),"Exchange changed during staging: "+why);
                try{present=PreparePresentation?.Invoke(state,intent);}catch(Exception e){try{Entry.Note("exchange-presentation-capture-error",e.Message);}catch{}}
                Committing=true;entered=true;changed=true;
                coastA?.Apply();coastB?.Apply();
                object finalUnitA=coastA?.FinalUnit??a.Unit,finalUnitB=coastB?.FinalUnit??b.Unit;
                plan.CommittedA=finalUnitA;plan.CommittedB=finalUnitB;
                if(coastA is null){Native.Set(finalUnitA,"type",typeA);Native.Set(finalUnitA,"UnitData",dataA);}
                if(coastB is null&&plan.FinalMimicCarrierB is not null){Native.Set(finalUnitB,"type",typeB);Native.Set(finalUnitB,"UnitData",dataB);}
                Fault("forms");
                Native.Set(plan.TileA!,"unit",intent.Swap?finalUnitB:finalUnitA);Native.Set(plan.TileB!,"unit",intent.Swap?finalUnitA:finalUnitB);
                Native.Set(finalUnitA,"coordinates",intent.Swap?b.Coordinates:a.Coordinates);Native.Set(finalUnitB,"coordinates",intent.Swap?a.Coordinates:b.Coordinates);
                if(NativeRegroupSession.Enabled)Native.Set(state,"CurrentUnitId",plan.FinalCounter);
                Fault("positions");
                Native.Set(finalUnitA,"effects",stagedEffects);
                Native.Invoke(Native.M("UnitState","MakeExhauseted","GameState"),finalUnitA,state);
                Require((bool)Native.Get(finalUnitA,"moved")!&&(bool)Native.Get(finalUnitA,"attacked")!,"Native exchange exhaustion did not spend the initiator.");
                Require((bool)Native.Get(finalUnitB,"moved")! == b.Moved&&(bool)Native.Get(finalUnitB,"attacked")! == b.Attacked&&
                    (coastB is null?Native.Same(Native.Get(finalUnitB,"effects"),b.Effects):Native.Items(Native.Get(finalUnitB,"effects")!).SequenceEqual(Native.Items(b.Effects))),"Native exchange changed the partner's allowance or effects.");
                Fault("allowance");
                // Invisibility is a functional selected-form mechanic, not a
                // fake status used for storage. Keep every other real effect.
                var appearanceA=plan.CandidateLedger.Units.Single(r=>r.LogicalId==intent.Initiator.LogicalId).Appearance;
                var appearanceB=plan.CandidateLedger.Units.Single(r=>r.LogicalId==intent.Partner.LogicalId).Appearance;
                if(appearanceA is not null)Native.Set(finalUnitA,"effects",NativeDisguiseConcealment.WithConcealment(Native.Get(finalUnitA,"effects")!,appearanceA,finalUnitA));
                if(appearanceB is not null)Native.Set(finalUnitB,"effects",NativeDisguiseConcealment.WithConcealment(Native.Get(finalUnitB,"effects")!,appearanceB,finalUnitB));
                sight.StageFinalSources(Fault);
                sight.MergeDiscovery();
                NativeLedgerState.Validate(state,plan.AfterRevision,plan.CandidatePayload);
                sight.AssertPending();
                NativeGameStateEnvelope.Set(state,plan.AfterRevision,plan.CandidatePayload);
                Fault("ledger");
                // Native exploration actions remain pending. No movement,
                // arrival, rendering or child action is executed here.
                var completed=new NativeExchangeCommitResult(false,plan.BeforeRevision,plan.AfterRevision,plan);
                coastA?.PrepareAcceptance();coastB?.PrepareAcceptance();sight.Accept();coastA?.Accept();coastB?.Accept();result=completed;
                try{present?.Invoke();}catch(Exception e){try{Entry.Note("exchange-presentation-complete-error",e.Message);}catch{}}return true;
            }
            catch(Exception error)
            {
                if(changed&&plan is not null&&a is not null&&b is not null)
                {
                    try
                    {
                        sight?.Rollback();
                        coastA?.Rollback();coastB?.Rollback();
                        a.Restore();b.Restore();Native.Set(plan.TileA!,"unit",tileUnitA);Native.Set(plan.TileB!,"unit",tileUnitB);
                        Native.Set(state,"CurrentUnitId",counterBefore);plan.CommittedA=plan.A;plan.CommittedB=plan.B;
                        NativeGameStateEnvelope.Set(state,plan.BeforeRevision,plan.OriginalPayload);
                        var restored=NativeGameStateEnvelope.Get(state);
                        Require(nativeBefore is not null&&NativeGameStateEnvelope.NativeBytes(state,123).SequenceEqual(nativeBefore)&&
                            restored is not null&&restored.Revision==plan.BeforeRevision&&restored.Bytes().SequenceEqual(plan.OriginalPayload),
                            "Detached exchange rollback did not restore exact native bytes and ledger.");
                    }
                    catch(Exception rollback){throw new InvalidOperationException("Exchange rollback failed; stop this game and preserve its prior save.",new AggregateException(error,rollback));}
                }
                reason=error.GetType().Name+": "+error.Message;return false;
            }
            finally{try{sight?.Dispose();coastA?.Dispose();coastB?.Dispose();}finally{if(entered)Committing=false;}}
        }
    }
}
