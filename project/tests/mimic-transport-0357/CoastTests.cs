using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using UnitType=Polytopia.Data.UnitData.Type;
namespace Deception.Tests
{
    internal static class CoastTests
    {
        sealed record Rig(GameState State,UnitState A,UnitState B,TileData Land,TileData Water,NativeExchangeIntent Intent);
        static LedgerAppearance Art(string form,ushort health=100,LedgerAppearance? passenger=null)=>new(form,new("Elyrion","DarkElf"),new("Imperius","Default"),new("Elyrion","DarkElf"),57,health,health,false,ApparentConcealment.Visible,-1,Array.Empty<string>(),Passenger:passenger);
        static Rig Make(bool matrixB,NativeExchangeKind kind,bool swap,bool partnerMoved=false,bool partnerAttacked=false)
        {
            Fault.At="";NativeExchangeCommit.PreparePresentation=null;RecruitmentHooks.Reset();NativeRegroupSession.Enabled=NativeRegroupSession.Admitted=true;
            var state=new GameState{CurrentUnitId=9};NativeRegroupSession.Owned=state;
            UnitState Unit(uint id,string form,WorldCoordinates at,UnitState? cargo=null)=>new(){id=id,type=RecruitmentHooks.Definitions[form].type,UnitData=RecruitmentHooks.Definitions[form],health=RecruitmentCatalog.IsMimicId(form)?(ushort)10:form=="giant"?(ushort)400:(ushort)100,coordinates=at,passengerUnit=cargo};
            var land=new TileData{coordinates=new(2,3)};var water=new TileData{coordinates=new(3,3),IsWater=true};state.Map.Tiles.Add(land);state.Map.Tiles.Add(water);
            var mimic=Unit(3,RecruitmentCatalog.Variant("giant"),land.coordinates);var genuine=Unit(4,"warrior",land.coordinates);
            UnitState a,b;
            if(matrixB){a=Unit(6,RecruitmentCatalog.Variant("juggernaut"),water.coordinates,mimic);b=genuine;water.unit=a;land.unit=b;}
            else{a=mimic;b=Unit(5,"transportship",water.coordinates,genuine);land.unit=a;water.unit=b;}
            b.moved=partnerMoved;b.attacked=partnerAttacked;
            var apparent=matrixB?Art("juggernaut",400,Art("giant",400)):Art("giant",400);
            var rows=new[]{new LedgerUnit(4,a.id,true,LedgerActualKind.Mimic,true,apparent,PaidInvestmentKind.Known,3,matrixB?new(3):null),new LedgerUnit(5,b.id,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,2,matrixB?null:new(4))};
            var ledger=new NativeIdentityLedger(Guid.Parse("37273416-1c28-4381-9a53-9fb91b1f9d40"),11,1,rows,Array.Empty<LedgerReceipt>());NativeGameStateEnvelope.Set(state,4,NativeIdentityLedgerCodec.Encode(ledger,4));
            var source=kind==NativeExchangeKind.Disguise?apparent:NativeLedgerState.Capture(state,new(b,true),b.type.ToString(),false);
            var intent=new NativeExchangeIntent(ledger.Epoch,1,4,state.CurrentTurn,1,kind,swap,NativePartnerReference.Existing,11,new(4,a.id,a.coordinates.X,a.coordinates.Y),new(5,b.id,b.coordinates.X,b.coordinates.Y),NativeExchangeIntentCodec.AppearanceFingerprint(source));
            return new(state,a,b,land,water,intent);
        }
        internal static void Run(Action<Action> test,Action<bool,string> check)
        {
            foreach(bool matrixB in new[]{false,true})foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})foreach(bool swap in new[]{false,true})
                foreach(bool moved in new[]{false,true})foreach(bool attacked in new[]{false,true})foreach(bool initiatorMoved in new[]{false,true})test(()=>
                {
                    var r=Make(matrixB,kind,swap,moved,attacked);r.A.moved=initiatorMoved;string before=TransportStateDump.Dump(r.State);var snapshot=NativeGameStateEnvelope.Get(r.State)!;
                    check(NativeExchangePlanning.TryPlan(r.State,r.Intent,out var plan,out var why),"coast plan: "+why);
                    check(TransportStateDump.Dump(r.State)==before&&NativeGameStateEnvelope.Get(r.State)==snapshot,"coast planning mutated native state");
                    check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out var result,out why),"coast commit: "+why);
                    var current=NativeGameStateEnvelope.Get(r.State)!;var ledger=NativeIdentityLedgerCodec.Decode(current.Bytes(),current.Revision);var a=ledger.Units.Single(x=>x.LogicalId==4);var b=ledger.Units.Single(x=>x.LogicalId==5);
                    var actualA=r.State.Map.Tiles.Select(t=>t.unit!).Single(u=>u.id==a.NativeId);var actualB=r.State.Map.Tiles.Select(t=>t.unit!).Single(u=>u.id==b.NativeId);
                    string expectedA=kind==NativeExchangeKind.Regroup?(matrixB!=swap?"transportship":"warrior"):(matrixB!=swap?"juggernaut":"giant");
                    string? expectedB=kind==NativeExchangeKind.Regroup?null:(matrixB==swap?"juggernaut":"giant");
                    check(a.Appearance!.Form==expectedA&&b.Appearance?.Form==expectedB,"canonical coast apparent matrix incorrect");
                    check(a.PaidStars==3&&b.PaidStars==2&&ledger.Units.Length==2&&ledger.NextSequence==2&&ledger.Receipts.Length==1,"coast identity/payment/history changed");
                    check(actualA.health==10&&actualA.UnitData.health==10&&actualA.UnitData.attack==0&&actualA.moved&&actualA.attacked,"coast Mimic acquired HP/offense or unspent action");
                    check(actualB.moved==moved&&actualB.attacked==attacked,"coast partner allowance changed");
                    check(RecruitmentCatalog.IsMimicId(actualA.type.ToString())&&!RecruitmentCatalog.IsMimicId(actualB.type.ToString())&&actualB.health==100,"coast changed genuine/Mimic identity or fabricated Giant");
                    check(ledger.NextLogicalId==(ulong)r.State.CurrentUnitId+2&&r.State.Stars==21&&r.State.Score==300&&r.State.CitySupport==1,"coast counter/support/economy changed");
                    if(actualA.passengerUnit is UnitState cargo)check(cargo.id==3&&cargo.type.ToString()==RecruitmentCatalog.Variant(a.Appearance.Passenger!.Form)&&cargo.health==10&&cargo.xp==4,"coast apparent cargo failed to update original carrier");
                    string done=TransportStateDump.Dump(r.State);check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out var duplicate,out why)&&duplicate!.AlreadyCommitted&&TransportStateDump.Dump(r.State)==done,"duplicate coast action mutated state");
                });
            foreach(bool matrixB in new[]{false,true})foreach(string fault in new[]{"type","data","coordinates","tile","counter","exhaust","sight-stage","sight-pending","validation","ledger","sight-accept"})test(()=>
            {
                var r=Make(matrixB,NativeExchangeKind.Disguise,true,true,false);r.A.moved=true;string before=TransportStateDump.Dump(r.State);var saved=NativeGameStateEnvelope.Get(r.State)!;var stack=r.State.ActionStack;Fault.At=fault;
                check(!NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out var why),"Missing coast fault: "+fault);
                check(TransportStateDump.Dump(r.State)==before&&NativeGameStateEnvelope.Get(r.State)!.Bytes().SequenceEqual(saved.Bytes())&&ReferenceEquals(stack,r.State.ActionStack),"Coast rollback failed "+fault+": "+why);
            });
            foreach(string fault in new[]{"type","data","coordinates","tile","counter","exhaust","sight-stage","sight-pending","validation","ledger","sight-accept"})test(()=>
            {
                var r=Make(true,NativeExchangeKind.Regroup,false,true,true);string before=TransportStateDump.Dump(r.State);var saved=NativeGameStateEnvelope.Get(r.State)!;Fault.At=fault;
                check(!NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out var why),"Missing refit fault "+fault);
                check(TransportStateDump.Dump(r.State)==before&&NativeGameStateEnvelope.Get(r.State)!.Bytes().SequenceEqual(saved.Bytes()),"Afloat cargo/type rollback failed "+fault+": "+why);
            });
            test(()=>{var r=Make(true,NativeExchangeKind.Regroup,false);r.Water.Explored=false;check(!NativeExchangePlanning.TryPlan(r.State,r.Intent,out _,out _),"hidden coast participant admitted");});
            test(()=>{var r=Make(false,NativeExchangeKind.Regroup,true);r.State.Player.Researched=false;check(!NativeExchangePlanning.TryPlan(r.State,r.Intent,out _,out _),"unresearched coast exchange admitted");});
            PresentationBoundary(test,check);
        }
        static void PresentationBoundary(Action<Action> test,Action<bool,string> check)
        {
            test(()=>
            {
                var r=Make(false,NativeExchangeKind.Regroup,true);var before=NativeGameStateEnvelope.Get(r.State)!;
                int captures=0,completions=0;bool capturedBefore=false,completedAfter=false;
                try
                {
                    NativeExchangeCommit.PreparePresentation=(state,intent)=>
                    {
                        captures++;capturedBefore=ReferenceEquals(state,r.State)&&ReferenceEquals(intent,r.Intent)&&
                            ReferenceEquals(NativeGameStateEnvelope.Get(state),before)&&ReferenceEquals(r.Land.unit,r.A)&&ReferenceEquals(r.Water.unit,r.B);
                        return ()=>
                        {
                            completions++;var after=NativeGameStateEnvelope.Get(state)!;
                            var ledger=NativeIdentityLedgerCodec.Decode(after.Bytes(),after.Revision);
                            completedAfter=after.Revision==before.Revision+1&&ledger.Receipts.Length==1&&
                                ledger.NextSequence==2&&r.State.ActionStack.Count==2&&r.Water.unit!.moved&&r.Water.unit.attacked;
                        };
                    };
                    check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out var result,out var why)&&result is {AlreadyCommitted:false},"presentation seam commit failed: "+why);
                    check(captures==1&&capturedBefore,"presentation capture did not observe exact precommit state");
                    check(completions==1&&completedAfter,"presentation ran before complete ledger/native acceptance");
                    string done=TransportStateDump.Dump(r.State);
                    check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out var repeat,out why)&&repeat!.AlreadyCommitted,"presentation seam duplicate was not idempotent: "+why);
                    check(captures==1&&completions==1&&TransportStateDump.Dump(r.State)==done,"duplicate exchange repeated presentation capture/completion or state mutation");
                }
                finally{NativeExchangeCommit.PreparePresentation=null;}
            });
            foreach(string fault in new[]{"type","sight-pending","ledger","sight-accept"})test(()=>
            {
                var r=Make(false,NativeExchangeKind.Disguise,true);var before=NativeGameStateEnvelope.Get(r.State)!;
                string bodyBefore=TransportStateDump.Dump(r.State);int captures=0,completions=0;
                try
                {
                    NativeExchangeCommit.PreparePresentation=(_,__)=>{captures++;return ()=>completions++;};Fault.At=fault;
                    check(!NativeExchangeCommit.TryCommit(r.State,r.Intent,out _,out _),"presentation rollback fault was not injected: "+fault);
                    // Refit staging writes only detached temporary objects;
                    // its injected type failure precedes observer capture.
                    check(captures==(fault=="type"?0:1)&&completions==0,"failed exchange captured/completed at the wrong boundary: "+fault);
                    check(TransportStateDump.Dump(r.State)==bodyBefore&&NativeGameStateEnvelope.Get(r.State)!.Bytes().SequenceEqual(before.Bytes()),"presentation callback changed rollback: "+fault);
                }
                finally{NativeExchangeCommit.PreparePresentation=null;}
            });
            test(()=>
            {
                var r=Make(false,NativeExchangeKind.Regroup,true);int captures=0;
                try
                {
                    NativeExchangeCommit.PreparePresentation=(_,__)=>{captures++;return ()=>throw new Exception("must not run");};
                    check(!NativeExchangeCommit.TryCommit(r.State,r.Intent with{Revision=r.Intent.Revision+1},out _,out _),"stale exchange was accepted");
                    check(captures==0,"rejected plan captured presentation");
                }
                finally{NativeExchangeCommit.PreparePresentation=null;}
            });
            foreach(bool throwDuringCapture in new[]{true,false})test(()=>
            {
                var r=Make(false,NativeExchangeKind.Regroup,true);int captures=0,completions=0;
                try
                {
                    NativeExchangeCommit.PreparePresentation=(_,__)=>
                    {captures++;if(throwDuringCapture)throw new InvalidOperationException("cosmetic capture failure");return ()=>{completions++;throw new InvalidOperationException("cosmetic completion failure");};};
                    check(NativeExchangeCommit.TryCommit(r.State,r.Intent,out var result,out var why)&&result is {AlreadyCommitted:false},"cosmetic exception rolled back accepted command: "+why);
                    var after=NativeGameStateEnvelope.Get(r.State)!;var ledger=NativeIdentityLedgerCodec.Decode(after.Bytes(),after.Revision);
                    check(captures==1&&completions==(throwDuringCapture?0:1)&&after.Revision==5&&ledger.Receipts.Length==1,"cosmetic exception lost accepted receipt or ran extra callbacks");
                    check(r.Water.unit!.moved&&r.Water.unit.attacked&&r.Water.unit.health==10,"cosmetic exception changed native body/allowance");
                }
                finally{NativeExchangeCommit.PreparePresentation=null;}
            });
        }
    }
}
