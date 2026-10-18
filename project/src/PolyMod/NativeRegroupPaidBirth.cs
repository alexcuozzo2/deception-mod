using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    internal static partial class NativeRegroupSession
    {
        // Normal TrainAction observation. Existing RecruitmentHooks retains
        // eligibility and native original ownership. This witness records only
        // a completed purchase at the exact registered price; no custom Train payload,
        // manufactured expense, native respawn, or health/effects persistence.
        static PaidTrain? Buying;
        internal sealed class PaidTrain
        {
            internal readonly object State,Action,Player,Tile,Data;
            internal readonly string Rule;
            internal readonly byte Owner;
            internal readonly int X,Y,Currency,Thread=Environment.CurrentManagedThreadId;
            internal readonly uint Counter,Turn;
            internal readonly ushort Command;
            internal readonly NativeGameStateEnvelope.Snapshot Before;
            internal readonly Dictionary<uint,NativeLedgerState.Body> Bodies;
            internal bool Completed,Disposed;
            internal PaidTrain(object state,object action)
            {
                State=state;Action=action;Rule=Native.EnumName(Native.Get(action,"Type")!);
                Require(RecruitmentHooks.Forms.ContainsKey(Rule),"Unknown paid carrier.");
                Prepare(state);Before=NativeGameStateEnvelope.Get(state)!;
                Bodies=NativeLedgerState.Graph(state);Counter=Convert.ToUInt32(Native.Get(state,"CurrentUnitId"));
                Owner=Convert.ToByte(Native.Get(action,"PlayerId"));Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));
                Command=Convert.ToUInt16(Native.Get(state,"CurrentCommand"));
                object coordinates=Native.Get(action,"Coordinates")!;X=Native.Int(Native.Get(coordinates,"X"));Y=Native.Int(Native.Get(coordinates,"Y"));
                object?[] args={Owner,null};
                Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&args[1] is not null,"Paid player missing.");
                Player=args[1]!;Currency=Native.Int(Native.Get(Player,"Currency"));
                Tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),coordinates)??throw new InvalidDataException("Paid city tile missing.");
                Data=RecruitmentHooks.Data(state,Rule);
                Require(Native.Get(Tile,"unit") is null&&Currency>=RecruitmentCatalog.Cost&&Counter<uint.MaxValue&&Native.Int(Native.Get(action,"Cost"))==RecruitmentCatalog.Cost&&
                    !(bool)Native.Get(action,"trainActionAborted")!&&Native.Int(Native.Get(state,"CurrentPlayer"))==Owner,"Paid native preconditions differ.");
            }
            internal void Check()
            {
                Require(ReferenceEquals(Buying,this)&&!Disposed&&!Completed&&Thread==Environment.CurrentManagedThreadId&&Ready(State),"Paid action authority changed.");
                Require(Native.Int(Native.Get(Action,"PlayerId"))==Owner&&Native.Int(Native.Get(Player,"Id"))==Owner&&Native.Int(Native.Get(Action,"Cost"))==RecruitmentCatalog.Cost&&
                    Native.EnumName(Native.Get(Action,"Type")!)==Rule&&Convert.ToUInt32(Native.Get(State,"CurrentTurn"))==Turn&&
                    Convert.ToUInt16(Native.Get(State,"CurrentCommand"))==Command&&Native.Int(Native.Get(State,"CurrentPlayer"))==Owner,"Paid action identity/context changed.");
                object coordinates=Native.Get(Action,"Coordinates")!;
                Require(Position(coordinates,X,Y)&&Native.Same(Tile,Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(State,"Map"),coordinates)),"Paid action tile changed.");
                object?[] args={Owner,null};Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),State,args)!&&Native.Same(args[1],Player),"Paid player identity changed.");
                var now=NativeGameStateEnvelope.Get(State);Require(now is not null&&now.Revision==Before.Revision&&now.Bytes().SequenceEqual(Before.Bytes()),"Ledger changed inside native purchase.");
            }
        }
        static bool Position(object coordinates,int x,int y)=>Native.Int(Native.Get(coordinates,"X"))==x&&Native.Int(Native.Get(coordinates,"Y"))==y;
        static void InstallPaid(Harmony harmony)
        {
            Require(!NativeExecutionTrigger.Requested,"Production paid observation cannot share diagnostic Execute detours.");
            Native.Hook(harmony,Native.M("TrainAction","Execute","GameState"),typeof(NativeRegroupSession),
                nameof(BeforePaidTrain),nameof(AfterPaidTrain),nameof(PaidTrainFinished));
        }
        public static bool BeforePaidTrain(object __instance,object __0,out object? __state)
        {
            __state=null;if(!Enabled)return true;
            string rule=Native.EnumName(Native.Get(__instance,"Type")!);
            if(!RecruitmentCatalog.IsMimicId(rule))return true;
            try
            {
                Require(Buying is null,"Nested native Mimic purchase.");
                // Existing native validity owns rejected/aborted purchases. Only
                // an eligible action at the registered price opens this witness.
                if(!RecruitmentHooks.Forms.ContainsKey(rule)||Native.Int(Native.Get(__instance,"Cost"))!=RecruitmentCatalog.Cost||(bool)Native.Get(__instance,"trainActionAborted")!)return true;
                if(!Ready(__0))throw new InvalidDataException("Mimic purchase lacks its production Local ledger.");
                var paid=new PaidTrain(__0,__instance);Buying=paid;__state=paid;return true;
            }
            catch(Exception e){Native.Set(__instance,"trainActionAborted",true);Fault("Paid TrainAction preflight: "+e.Message);if(Current is Session session)Note(session,"paid-rejected",e.Message);return false;}
        }
        public static void AfterPaidTrain(object __instance,object __0,object? __state,bool __runOriginal)
        {
            if(__state is not PaidTrain paid)return;
            try
            {
                paid.Check();Require(Native.Same(__instance,paid.Action)&&Native.Same(__0,paid.State),"Paid return arguments changed.");
                if(!__runOriginal||(bool)Native.Get(paid.Action,"trainActionAborted")!)
                {
                    Require(Native.Int(Native.Get(paid.Player,"Currency"))==paid.Currency&&Convert.ToUInt32(Native.Get(paid.State,"CurrentUnitId"))==paid.Counter&&
                        Native.Get(paid.Tile,"unit") is null,"Rejected/aborted native purchase changed payment or spawned a unit.");
                    paid.Completed=true;return;
                }
                object spawn=Native.Get(paid.Tile,"unit")??throw new InvalidDataException("Paid action returned without its native city body.");
                uint id=Convert.ToUInt32(Native.Get(spawn,"id"));var graph=NativeLedgerState.Graph(paid.State);
                Require(id==paid.Counter+1&&Convert.ToUInt32(Native.Get(paid.State,"CurrentUnitId"))==id&&graph.Count==paid.Bodies.Count+1&&
                    (long)paid.Currency-Native.Int(Native.Get(paid.Player,"Currency"))==RecruitmentCatalog.Cost,"Paid native counter, graph delta, or debit differs.");
                foreach(var pair in paid.Bodies)Require(graph.TryGetValue(pair.Key,out var existing)&&Native.Same(existing.Unit,pair.Value.Unit),"Existing native identity changed during purchase.");
                Require(graph.TryGetValue(id,out var newborn)&&newborn.Root&&Native.Same(newborn.Unit,spawn)&&
                    Native.Same(Native.Get(spawn,"UnitData"),paid.Data)&&Native.EnumName(Native.Get(spawn,"type")!)==paid.Rule&&
                    Native.Int(Native.Get(spawn,"owner"))==paid.Owner&&Position(Native.Get(spawn,"coordinates")!,paid.X,paid.Y)&&
                    Position(Native.Get(spawn,"home")!,paid.X,paid.Y)&&Native.Int(Native.Get(spawn,"health"))==10&&
                    (bool)Native.Get(spawn,"moved")!&&(bool)Native.Get(spawn,"attacked")!,"Paid newborn identity, home, actual HP, or exhaustion differs.");
                // Pure candidate first. Publication is a single validated ledger
                // value swap after debit/body evidence, before any writer opens.
                var candidate=NativeLedgerState.Reconcile(paid.State,paid.Before);
                var ledger=NativeIdentityLedgerCodec.Decode(candidate.Payload,candidate.Revision);
                var rows=ledger.Units.Select(row=>row.Alive&&row.NativeId==id?
                    row with{InvestmentKind=PaidInvestmentKind.Known,PaidStars=RecruitmentCatalog.Cost,AmphibiousMimic=NativeAquarionUnits.IsAquarion(paid.Player)}:row).ToArray();
                Require(rows.Count(row=>row.Alive&&row.NativeId==id&&row.Kind==LedgerActualKind.Mimic&&row.InvestmentKind==PaidInvestmentKind.Known&&row.PaidStars==RecruitmentCatalog.Cost)==1,"Paid ledger candidate lacks exact newborn.");
                byte[] bytes=NativeIdentityLedgerCodec.Encode(ledger with{Units=rows},candidate.Revision);
                NativeLedgerState.Validate(paid.State,candidate.Revision,bytes);
                NativeGameStateEnvelope.Set(paid.State,candidate.Revision,bytes);paid.Completed=true;
                if(Current is Session session)Note(session,"paid-birth","nativeId="+id+"; verifiedDebit="+RecruitmentCatalog.Cost+"; revision="+candidate.Revision);
            }
            catch(Exception e){Fault("Paid TrainAction finish: "+e.Message);throw;}
        }
        public static Exception? PaidTrainFinished(Exception? __exception,object? __state)
        {
            if(__state is not PaidTrain paid)return __exception;
            if(__exception is not null||!paid.Completed)Fault("Native paid TrainAction failed or did not complete its evidence boundary.");
            paid.Disposed=true;if(ReferenceEquals(Buying,paid))Buying=null;else Fault("Paid action scope changed before cleanup.");
            return __exception;
        }
    }
}
