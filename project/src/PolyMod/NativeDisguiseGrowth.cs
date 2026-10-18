using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native growth UpgradeActions finish before EndTurnAction (the action
    // stack is LIFO). Advance private appearances at that same owner boundary.
    // Never invoke UpgradeAction: it would create a genuine dragon, change
    // health, score and native birth/support facts.
    internal static class NativeDisguiseGrowth
    {
        [ThreadStatic] static bool Applying;
        static int Notes;
        sealed record Change(object Unit,object BeforeType,object BeforeData,object AfterType,object AfterData);
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        internal static void Install(Harmony harmony)
        {
            Native.Hook(harmony,Native.M("EndTurnAction","Execute","GameState"),typeof(NativeDisguiseGrowth),
                nameof(BeforeEndTurn),null,nameof(Finished));
        }
        public static void BeforeEndTurn(object __instance,object __0,out bool __state)
        {
            __state=false;
            if(!NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(__0))return;
            __state=true;bool entered=false;
            try
            {
                Require(!Applying,"Reentrant disguise growth boundary.");
                Require(Native.Int(Native.Get(__instance,"PlayerId"))==Native.Int(Native.Get(__0,"CurrentPlayer")),"Disguise growth actor is not the ending owner.");
                Applying=true;entered=true;
                NativeRegroupSession.Prepare(__0);
                Advance(__instance,__0,Convert.ToByte(Native.Get(__instance,"PlayerId")),Convert.ToUInt32(Native.Get(__0,"CurrentTurn")));
            }
            catch(Exception e){NativeRegroupSession.Fault("Disguise growth: "+e.Message);throw;}
            finally{if(entered)Applying=false;}
        }
        static void Advance(object action,object state,byte owner,uint round)
        {
            var before=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Growth state has no private ledger.");
            var ledger=NativeIdentityLedgerCodec.Decode(before.Bytes(),before.Revision);
            if(!ledger.Units.Any(r=>r.Alive&&r.Appearance is LedgerAppearance a&&NativeApparentGrowth.IsGrowing(a.Form)))return;
            var graph=NativeLedgerState.Graph(state);var rows=(LedgerUnit[])ledger.Units.Clone();var changes=new List<Change>();var audible=new List<(LedgerAppearance Before,LedgerUnit After)>();bool changed=false;
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i];
                if(!row.Alive||row.Appearance is not LedgerAppearance appearance||!NativeApparentGrowth.IsGrowing(appearance.Form))continue;
                Require(graph.TryGetValue(row.NativeId,out var body),"Growing appearance has no native body.");
                var unit=body!.Unit;
                if((row.Rogue?.Owner??Convert.ToByte(Native.Get(unit,"owner")))!=owner||Native.Int(Native.Get(unit,"health"))==0)continue;
                // Native owner-end growth visits map roots only. Apparent
                // cargo retains its absolute deadline until ordinary landing.
                var next=NativeApparentGrowth.Advance(appearance,round);
                if(next==appearance)continue;
                if(next.Form!=appearance.Form&&row.Kind==LedgerActualKind.Mimic)
                {
                    string carrier=RecruitmentCatalog.Variant(next.Form);
                    Require(RecruitmentHooks.Forms.TryGetValue(carrier,out var form)&&form.NativeId==next.Form,"Growth target has no registered carrier.");
                    object type=Native.EnumValue(RecruitmentHooks.UT,carrier),data=RecruitmentHooks.Data(state,carrier);
                    Require(Equals(Native.Get(data,"type"),type)&&Native.Int(Native.Get(data,"health"))==10&&Native.Int(Native.Get(data,"attack"))==0&&Native.Int(Native.Get(data,"defence"))==0&&Native.Int(Native.Get(data,"range"))==0,"Growth target violates actual Mimic invariants.");
                    changes.Add(new(unit,Native.Get(unit,"type")!,Native.Get(unit,"UnitData")!,type,data));
                }
                rows[i]=row with{Appearance=next};changed=true;
                if(next.Form!=appearance.Form)audible.Add((appearance,rows[i]));
            }
            if(!changed)return;
            // Native action history already orders this transition. As with
            // births/deaths reconciliation, do not invent a command receipt or
            // advance the private command sequence for transport cadence.
            byte[] payload=NativeIdentityLedgerCodec.Encode(ledger with{Units=rows},before.Revision);
            NativeGrowthSight.Pending? sight=null;
            try
            {
                foreach(var c in changes){Native.Set(c.Unit,"type",c.AfterType);Native.Set(c.Unit,"UnitData",c.AfterData);}
                // A Rogue grows on its remembered owner's schedule while its
                // native owner remains the temporary commander. That idle
                // owner boundary cannot grant either player a new sight pulse.
                // Acquisition/move/exchange discovery uses its own provenance.
                var roots=changes.Where(c=>graph[Convert.ToUInt32(Native.Get(c.Unit,"id"))].Root&&
                    Convert.ToByte(Native.Get(c.Unit,"owner"))==owner).Select(c=>c.Unit).ToArray();
                if(roots.Length>0)sight=NativeGrowthSight.Stage(state,owner,roots);
                NativeLedgerState.Validate(state,before.Revision,payload);
                NativeGameStateEnvelope.Set(state,before.Revision,payload);
                sight?.Accept();
            }
            catch
            {
                sight?.Rollback();
                foreach(var c in changes){Native.Set(c.Unit,"type",c.BeforeType);Native.Set(c.Unit,"UnitData",c.BeforeData);}
                NativeGameStateEnvelope.Set(state,before.Revision,before.Bytes());
                throw;
            }
            finally{sight?.Dispose();}
            foreach(var cue in audible)NativeDisguiseGrowthAudio.Record(action,state,cue.Before,cue.After);
            if(changes.Count>0&&Notes++<24)Entry.Note("disguise-grown","owner="+owner+" round="+round+" phases="+changes.Count+"; private appearance only; actual identity/health/actions retained");
        }
        public static Exception? Finished(Exception? __exception,bool __state,object? __instance=null,object? __0=null)
        {
            if(__state&&__exception is not null)
            {
                if(__instance is not null&&__0 is not null)NativeDisguiseGrowthAudio.Cancel(__instance,__0);
                NativeRegroupSession.Fault("Owner end-turn failed during disguise growth: "+__exception.Message);
            }
            return __exception;
        }
    }
}
