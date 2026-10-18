using System;
using System.Linq;
using System.IO;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Observe actual native debits. Cost-free costume changes never enter
    // these native action boundaries and cannot increase a refund.
    internal static class NativeInvestment
    {
        internal sealed record Purchase(object State,object Tile,object Player,int Currency,int Cost,LedgerUnit? Row,bool Upgrade);
        internal sealed record Refund(object State,object Unit,object Action,NativeGameStateEnvelope.Snapshot? Snapshot,int PaidStars,Refund? Previous,int Generation);
        [ThreadStatic] static Refund? Current;
        static int Generation;
        internal static void Clear(){Generation++;Current=null;}
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("TrainAction","Execute","GameState"),typeof(NativeInvestment),nameof(BeforeTrain),nameof(AfterPurchase));
            Native.Hook(h,Native.M("UpgradeAction","Execute","GameState"),typeof(NativeInvestment),nameof(BeforeUpgrade),nameof(AfterPurchase));
            Native.Hook(h,Native.M("DisbandUnitAction","Execute","GameState"),typeof(NativeInvestment),nameof(BeforeDisband),null,nameof(DisbandFinished));
            Native.Hook(h,Native.M("UnitState","GetFullCost","GameState"),typeof(NativeInvestment),nameof(DisbandCost));
        }
        public static void BeforeTrain(object __instance,object __0,out Purchase? __state)=>Begin(__instance,__0,false,out __state);
        public static void BeforeUpgrade(object __instance,object __0,out Purchase? __state)=>Begin(__instance,__0,true,out __state);
        static void Begin(object action,object state,bool upgrade,out Purchase? scope)
        {
            scope=null;if(!NativeRegroupSession.Ready(state))return;
            if(!upgrade&&RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(action,"Type")!)))return;
            int cost=Native.Int(Native.Get(action,"Cost"));if(cost<0)return;
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),Native.Get(action,"Coordinates"));if(tile is null)return;
            var unit=Native.Get(tile,"unit");if(upgrade!=(unit is not null))return;
            object?[] args={Convert.ToByte(Native.Get(action,"PlayerId")),null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!||args[1] is null)return;
            scope=new(state,tile,args[1]!,Native.Int(Native.Get(args[1]!,"Currency")),cost,unit is null?null:NativeRogueRules.Row(state,unit),upgrade);
        }
        public static void AfterPurchase(Purchase? __state,bool __runOriginal)
        {
            if(__state is not Purchase p||!__runOriginal||Native.Get(p.Tile,"unit") is not object unit||p.Currency-Native.Int(Native.Get(p.Player,"Currency"))!=p.Cost)return;
            uint id=Convert.ToUInt32(Native.Get(unit,"id"));
            if(p.Upgrade&&(p.Row is null||p.Row.NativeId!=id))throw new InvalidDataException("Native upgrade changed physical identity unexpectedly.");
            NativeRegroupSession.Prepare(p.State);
            var snapshot=NativeGameStateEnvelope.Get(p.State)!;var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
            var rows=ledger.Units.Select(r=>r.Alive&&r.NativeId==id&&(!p.Upgrade||r.InvestmentKind==PaidInvestmentKind.Known)?
                r with{InvestmentKind=PaidInvestmentKind.Known,PaidStars=p.Upgrade?checked(r.PaidStars+p.Cost):p.Cost}:r).ToArray();
            NativeGameStateEnvelope.Set(p.State,snapshot.Revision,NativeIdentityLedgerCodec.Encode(ledger with{Units=rows},snapshot.Revision));
        }
        public static void BeforeDisband(object __instance,object __0,out Refund? __state)
        {
            // A nested unrelated removal fences the outer body's refund scope.
            __state=new(__0,__instance,__instance,null,0,Current,Generation);Current=__state;
            if(!NativeRegroupSession.Ready(__0)||!(bool)Native.Get(__instance,"GetPaid")!||
                !Native.Items(Native.Get(__0,"ActionStack")!).Any(x=>Native.Same(x,__instance)))return;
            var at=Native.Get(__instance,"Coordinates")!;
            var unit=NativeRogueRules.At(__0,at);if(unit is null)return;var row=NativeRogueRules.Row(__0,unit);
            if(row is not {Alive:true,InvestmentKind:PaidInvestmentKind.Known,Rogue:null}||
                Native.Int(Native.Get(unit,"owner"))!=Native.Int(Native.Get(__instance,"PlayerId"))||
                NativeGameStateEnvelope.Get(__0) is not { } snapshot)return;
            __state=__state with{Unit=unit,Snapshot=snapshot,PaidStars=row.PaidStars};Current=__state;
        }
        public static bool DisbandCost(object __instance,object __0,ref int __result)
        {
            var r=Current;
            if(r is null||r.Snapshot is null||r.Generation!=Generation||!Native.Same(r.State,__0)||!Native.Same(r.Unit,__instance)||
                !NativeRegroupSession.Ready(__0)||!ReferenceEquals(NativeGameStateEnvelope.Get(__0),r.Snapshot)||
                !Native.Same(NativeRogueRules.At(__0,Native.Get(r.Action,"Coordinates")!),__instance))return true;
            // Native DisbandUnitAction computes floor(cost/2), then queues one
            // ordinary currency child per star (not one aggregate action).
            // Supplying genuine cost here preserves native reaction/order and
            // prevents a free apparent vessel upgrade inflating its refund.
            __result=r.PaidStars;return false;
        }
        public static Exception? DisbandFinished(Exception? __exception,Refund? __state)
        {
            if(__state is not null&&__state.Generation==Generation&&ReferenceEquals(Current,__state))Current=__state.Previous;
            return __exception;
        }
    }
}
