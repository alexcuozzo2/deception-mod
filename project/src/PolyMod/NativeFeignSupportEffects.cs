using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Status history follows native events, not polling of true effects. A
    // copied healthy mask therefore stays independent of pre-existing poison.
    internal static class NativeFeignSupportEffects
    {
        internal sealed class Scope
        {
            internal readonly object State;internal readonly bool EndTurn;
            internal readonly List<(object Unit,string Effect,bool Add)> Changes=new();
            internal Scope(object state,bool endTurn){State=state;EndTurn=endTurn;}
        }
        [ThreadStatic] static List<Scope>? Scopes;
        static readonly Dictionary<long,Scope> PendingTurns=new();
        static readonly string[] Effects={"Frozen","Poisoned","Boosted","Petrified","Swift","DoubleReady","Charmed"};
        internal static void Clear(){Scopes=null;PendingTurns.Clear();}
        internal static void Install(Harmony harmony)
        {
            foreach(string action in new[]{"BoostAction","FreezeUnitAction","PoisonUnitAction"})
                Native.Hook(harmony,Native.M(action,"Execute","GameState"),typeof(NativeFeignSupportEffects),nameof(BeforeScope),null,nameof(FinishScope));
            Native.Hook(harmony,Native.M("ActionUtils","PerformAttack","GameState","System.Byte","WorldCoordinates","WorldCoordinates","System.Int32"),typeof(NativeFeignSupportEffects),nameof(BeforeScope),null,nameof(FinishScope));
            Native.Hook(harmony,Native.M("UnitState","MakeExhauseted","GameState"),typeof(NativeFeignSupportEffects),nameof(BeforeScope),nameof(AfterExhausted),nameof(FinishScope));
            Native.Hook(harmony,Native.M("UnitState","AddEffect","UnitEffect"),typeof(NativeFeignSupportEffects),null,nameof(Added),null);
            Native.Hook(harmony,Native.M("UnitState","RemoveEffect","UnitEffect"),typeof(NativeFeignSupportEffects),null,nameof(Removed),null);
            Native.Hook(harmony,Native.M("TileDataExtensions","GetHealOptions","TileData","System.Byte","GameState","System.Boolean"),typeof(NativeFeignSupportEffects),null,nameof(HealOptions),null);
            Native.Hook(harmony,Native.M("TileDataExtensions","GetBoostOptions","TileData","System.Byte","GameState"),typeof(NativeFeignSupportEffects),null,nameof(BoostOptions),null);
            Native.Hook(harmony,Native.M("UnitDataExtensions","CanRecover","UnitState","GameState"),typeof(NativeFeignSupportEffects),null,nameof(CanRecover),null);
        }
        public static void BeforeScope(object __0,MethodBase __originalMethod,out Scope? __state)
        {
            __state=null;if(!NativeRegroupSession.Ready(__0))return;
            __state=new(__0,__originalMethod.DeclaringType?.Name=="EndTurnCommand");(Scopes??=new()).Add(__state);
        }
        internal static Scope? BeginEndTurn(object state)
        {
            if(!NativeRegroupSession.Ready(state))return null;
            var scope=new Scope(state,true);(Scopes??=new()).Add(scope);return scope;
        }
        // Called directly from the Rogue boundary after owners are restored;
        // correctness does not depend on Harmony finalizer registration order.
        internal static Exception? EndTurnFinished(Scope? scope,Exception? error)
        {
            if(scope is null)return error;
            error=FinishScope(error,scope);
            try{if(error is null)Flush(scope.State);else Cancel(scope.State);}
            catch(Exception e){NativeRegroupSession.Fault("Apparent turn status: "+e.Message);return e;}
            return error;
        }
        static void Queue(object unit,string effect,bool add)
        {
            if(Scopes is not {Count:>0}||!Effects.Contains(effect,StringComparer.Ordinal))return;
            Scopes[^1].Changes.Add((unit,effect,add));
        }
        public static void Added(object __instance,object __0)=>Queue(__instance,Enum.GetName(__0.GetType(),__0)??"",true);
        public static void Removed(object __instance,object __0)=>Queue(__instance,Enum.GetName(__0.GetType(),__0)??"",false);
        public static void AfterExhausted(object __instance){Queue(__instance,"Boosted",false);Queue(__instance,"DoubleReady",false);}
        public static Exception? FinishScope(Exception? __exception,Scope? __state)
        {
            if(__state is null)return __exception;
            if(Scopes is not {Count:>0}||!ReferenceEquals(Scopes[^1],__state))
            {Scopes=null;NativeRegroupSession.Fault("Support status scope lost its nesting.");return __exception;}
            Scopes.RemoveAt(Scopes.Count-1);
            if(__exception is not null)return __exception;
            if(Scopes.Count>0&&Native.Same(Scopes[^1].State,__state.State))Scopes[^1].Changes.AddRange(__state.Changes);
            else if(__state.EndTurn)PendingTurns[Native.Pointer(__state.State)]=__state;
            else try{Apply(__state);}catch(Exception e){NativeRegroupSession.Fault("Apparent status event: "+e.Message);return e;}
            return __exception;
        }
        // Root calls after restoring temporary Rogue owner substitutions.
        internal static void Flush(object state)
        {
            if(!PendingTurns.Remove(Native.Pointer(state),out var pending))return;
            if(!Native.Same(state,pending.State))return;Apply(pending);
        }
        internal static void Cancel(object state)=>PendingTurns.Remove(Native.Pointer(state));
        static void Apply(Scope scope)
        {
            if(scope.Changes.Count==0||!NativeRegroupSession.Ready(scope.State))return;
            var snapshot=NativeGameStateEnvelope.Get(scope.State)??throw new InvalidDataException("Status event lost its ledger.");
            var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);var rows=ledger.Units.ToArray();var graph=NativeLedgerState.Graph(scope.State);bool changed=false;
            foreach(var (unit,effect,add) in scope.Changes)
            {
                uint id=Convert.ToUInt32(Native.Get(unit,"id"));
                if(!graph.TryGetValue(id,out var body)||!body.Root||!Native.Same(body.Unit,unit))continue;
                int index=Array.FindIndex(rows,r=>r.Alive&&r.NativeId==id&&r.Independent);
                if(index<0||rows[index].Appearance is not LedgerAppearance a||NativeFeignSupport.Has(a,effect)==add)continue;
                rows[index]=rows[index] with{Appearance=NativeFeignSupport.Effect(a,effect,add)};changed=true;
            }
            if(!changed)return;
            byte[] bytes=NativeIdentityLedgerCodec.Encode(ledger with{Units=rows},snapshot.Revision);
            NativeLedgerState.Validate(scope.State,snapshot.Revision,bytes);NativeGameStateEnvelope.Set(scope.State,snapshot.Revision,bytes);
        }
        static bool Context(object state,out NativeIdentityLedger? ledger)
        {
            ledger=null;if(!NativeRegroupSession.Ready(state))return false;
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return false;
            ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);return true;
        }
        static void Options(object tile,byte actor,object state,bool healing,bool center,ref object result)
        {
            if(!Context(state,out var ledger))return;
            var original=Native.Items(result).ToArray();var chosen=new List<object>();
            bool Eligible(object t,out object? u,out LedgerUnit? row)
            {
                u=Native.Get(t,"unit");uint id=u is null?0:Convert.ToUInt32(Native.Get(u,"id"));row=u is null?null:ledger!.Units.SingleOrDefault(r=>r.Alive&&r.NativeId==id);
                return u is not null&&NativeFeignSupport.Recipient(state,u,row,actor);
            }
            foreach(var candidate in original)if(Eligible(candidate,out _,out _))chosen.Add(candidate);
            // A true-full but apparently wounded ally is still a legal target
            // of the real visible heal, with independent clamping in each view.
            if(healing)
            {
                var area=Native.Invoke(Native.M("MapDataExtensions","GetArea","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),null,
                    Native.Get(state,"Map"),Native.Get(tile,"coordinates"),1,true,center)!;
                foreach(var candidate in Native.Items(area))
                    if(Eligible(candidate,out var unit,out var row)&&row!.Appearance is LedgerAppearance a&&
                        (a.Health<a.Maximum||NativeFeignSupport.Has(a,"Poisoned"))&&!chosen.Any(x=>Native.Same(x,candidate)))chosen.Add(candidate);
            }
            if(chosen.Count!=original.Length||chosen.Where((x,i)=>!Native.Same(x,original[i])).Any())result=Native.NewList("TileData",chosen);
        }
        public static void HealOptions(object __0,byte __1,object __2,bool __3,ref object __result)=>Options(__0,__1,__2,true,__3,ref __result);
        public static void BoostOptions(object __0,byte __1,object __2,ref object __result)=>Options(__0,__1,__2,false,false,ref __result);
        public static void CanRecover(object __0,object __1,ref bool __result)
        {
            if(!Context(__1,out var ledger))return;
            var row=ledger!.Units.SingleOrDefault(r=>r.Alive&&r.NativeId==Convert.ToUInt32(Native.Get(__0,"id")));
            if(row?.Rogue is not null){__result=false;return;}
            if(__result||row?.Appearance is not LedgerAppearance a||!(a.Health<a.Maximum||NativeFeignSupport.Has(a,"Poisoned")))return;
            __result=NativeFeignSupport.Recipient(__1,__0,row,Convert.ToByte(Native.Get(__0,"owner")))&&
                !(bool)Native.Invoke(Native.M("UnitState","HasLeader"),__0)!&&
                NativeFeignSupport.Ready(__0);
        }
    }
}
