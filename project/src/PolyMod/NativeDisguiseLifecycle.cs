using System;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Authoritative native events only. A renderer never reveals or heals a
    // disguise, and these hooks never change real health, form or readiness.
    internal static class NativeDisguiseLifecycle
    {
        internal static Func<object,object,Action?>? PreparePresentation;
        static readonly string[] ActiveCommands={"AttackCommand","BoostCommand","ExplodeCommand","FreezeAreaCommand","HealOthersCommand","SwarmCommand"};
        internal static void Install(Harmony harmony)
        {
            foreach(string type in ActiveCommands)
                Native.Hook(harmony,Native.M(type,"Execute","GameState"),typeof(NativeDisguiseLifecycle),nameof(BeforeActive),null,nameof(Finished));
            Native.Hook(harmony,Native.M("ConvertAction","Execute","GameState"),typeof(NativeDisguiseLifecycle),nameof(BeforeConvert),null,nameof(Finished));
            Native.Hook(harmony,Native.M("ActionUtils","PerformAttack","GameState","System.Byte","WorldCoordinates","WorldCoordinates","System.Int32"),typeof(NativeDisguiseLifecycle),nameof(BeforeDamage),nameof(AfterDamage),nameof(DamageFinished));
            Native.Hook(harmony,Native.M("HealAction","Execute","GameState"),typeof(NativeDisguiseLifecycle),nameof(BeforeHeal),nameof(AfterHeal),nameof(HealFinished));
        }
        static object? At(object state,object coordinate)
        {
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),coordinate);
            return tile is null?null:Native.Get(tile,"unit");
        }
        static bool Bound(object state)=>NativeRegroupSession.Enabled&&NativeRegroupSession.Ready(state);
        static void Edit(object state,uint id,Func<LedgerUnit,LedgerUnit> change,object? revealUnit=null,bool preserveNativeConcealment=true)
        {
            var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Disguise event lost its ledger.");
            var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
            int i=Array.FindIndex(ledger.Units,r=>r.Alive&&r.NativeId==id&&r.Independent);
            if(i<0)return;
            var row=change(ledger.Units[i]);if(row==ledger.Units[i])return;
            var rows=(LedgerUnit[])ledger.Units.Clone();rows[i]=row;
            byte[] payload=NativeIdentityLedgerCodec.Encode(ledger with{Units=rows},snapshot.Revision);
            object? oldEffects=revealUnit is null?null:Native.Get(revealUnit,"effects");
            try
            {
                if(revealUnit is not null)Native.Set(revealUnit,"effects",NativeDisguiseConcealment.WithConcealment(oldEffects!,null,preserveNativeConcealment?revealUnit:null));
                NativeLedgerState.Validate(state,snapshot.Revision,payload);
                NativeGameStateEnvelope.Set(state,snapshot.Revision,payload);
            }
            catch{if(revealUnit is not null)Native.Set(revealUnit,"effects",oldEffects);throw;}
        }
        internal static void Reveal(object state,object? unit,bool preserveNativeConcealment=true)
        {
            if(unit is null)return;
            var before=NativeGameStateEnvelope.Get(state);Action? present=null;
            try{if(Native.Int(Native.Get(unit,"health"))>0)present=PreparePresentation?.Invoke(state,unit);}
            catch(Exception e){Entry.Note("shed-reveal-presentation-capture-error",e.Message);}
            Edit(state,Convert.ToUInt32(Native.Get(unit,"id")),r=>r.Kind==LedgerActualKind.Genuine?r with{Independent=false,Appearance=null}:r,unit,preserveNativeConcealment);
            if(!ReferenceEquals(before,NativeGameStateEnvelope.Get(state)))
                try{present?.Invoke();}catch(Exception e){Entry.Note("shed-reveal-presentation-complete-error",e.Message);}
        }
        public static void BeforeActive(object __instance,object __0,out bool __state,bool __runOriginal=true)
        {
            __state=__runOriginal&&Bound(__0);if(!__state)return;
            try
            {
                string type=__instance.GetType().Name;
                if(!ActiveCommands.Contains(type,StringComparer.Ordinal))throw new InvalidDataException("Unknown active disguise event.");
                object?[] valid={__0,null};
                if(!(bool)Native.Invoke(Native.M(type,"IsValid","GameState","System.String&"),__instance,valid)!)return;
                NativeRegroupSession.Prepare(__0);
                Reveal(__0,At(__0,Native.Get(__instance,type=="AttackCommand"?"Origin":"Coordinates")!));
            }
            catch(Exception e){NativeRegroupSession.Fault("Disguise activation: "+e.Message);throw;}
        }
        public static void BeforeConvert(object __instance,object __0,out bool __state)
        {
            __state=Bound(__0);if(!__state)return;
            try{NativeRegroupSession.Prepare(__0);Reveal(__0,At(__0,Native.Get(__instance,"Origin")!));}
            catch(Exception e){NativeRegroupSession.Fault("Disguise conversion: "+e.Message);throw;}
        }
        internal sealed record Damage(object Unit,int Health);
        public static void BeforeDamage(object __0,object __3,out Damage? __state)
        {
            __state=null;if(!Bound(__0))return;
            var unit=At(__0,__3);
            if(unit is not null)__state=new(unit,Native.Int(Native.Get(unit,"health")));
        }
        public static void AfterDamage(object __0,Damage? __state)
        {
            if(__state is null||Native.Int(Native.Get(__state.Unit,"health"))>=__state.Health)return;
            // Positive damage reveals the actual Cloak as detected (the native
            // RevealAction removes Invisible). Preserve that effect only for
            // a prevalidated active command so revealing its mask cannot make
            // the already-eligible Infiltrate command invalidate itself.
            try{Reveal(__0,__state.Unit,preserveNativeConcealment:false);}
            catch(Exception e){NativeRegroupSession.Fault("Disguise damage reveal: "+e.Message);throw;}
        }
        internal sealed record Healing(uint Id,int? ApparentAmount);
        public static void BeforeHeal(object __instance,object __0,out Healing? __state)
        {
            __state=null;if(!Bound(__0))return;
            var unit=At(__0,Native.Get(__instance,"Coordinates")!);
            if(unit is not null)
            {
                uint id=Convert.ToUInt32(Native.Get(unit,"id"));int? amount=null;
                var snapshot=NativeGameStateEnvelope.Get(__0);
                var row=snapshot is null?null:NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Units.SingleOrDefault(r=>r.Alive&&r.NativeId==id);
                if(row?.Appearance is LedgerAppearance a)
                {
                    int fixedAmount=Native.Int(Native.Get(__instance,"FixedHealAmount"));
                    amount=fixedAmount>0?fixedAmount:NativeFeignSupport.RecoveryAmount(__0,NativeFeignSupport.Tile(__0,unit),unit,a);
                }
                __state=new(id,amount);
            }
        }
        public static void AfterHeal(object __instance,object __0,Healing? __state)
        {
            if(__state is null)return;
            try
            {
                // Healing is evaluated independently in each view: native
                // actual-full or poison cleansing must not suppress ordinary
                // recovery of a wounded, unpoisoned apparent profile.
                int amount=__state.ApparentAmount??Native.Int(Native.Get(__instance,"HealAmount"));
                Edit(__0,__state.Id,r=>
                {
                    if(r.Appearance is not LedgerAppearance a)return r;
                    var next=NativeFeignSupport.Heal(a,amount);
                    return NativeFeignSupport.Has(a,"Poisoned")||next.Health!=a.Health?r with{Appearance=next}:r;
                });
            }
            catch(Exception e){NativeRegroupSession.Fault("Apparent healing: "+e.Message);throw;}
        }
        public static Exception? Finished(Exception? __exception,bool __state)
        {if(__state&&__exception is not null)NativeRegroupSession.Fault("Native disguise event failed: "+__exception.Message);return __exception;}
        public static Exception? DamageFinished(Exception? __exception,Damage? __state)=>Finished(__exception,__state is not null);
        public static Exception? HealFinished(Exception? __exception,Healing? __state)=>Finished(__exception,__state is not null);
    }
}
