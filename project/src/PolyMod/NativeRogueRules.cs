using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native owner is the active command context. Permanent ownership remains
    // in the framed ledger and is used for restoration, support and accounting.
    internal static class NativeRogueRules
    {
        sealed record Cache(NativeLedgerOwnership.WeakIdentity State,NativeGameStateEnvelope.Snapshot Snapshot,Dictionary<uint,LedgerUnit> Rows);
        static readonly Dictionary<long,Cache> Caches=new();
        internal static LedgerUnit? Row(object state,object unit)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return null;
            long key=Native.Pointer(state);
            if(!Caches.TryGetValue(key,out var cache)||!cache.State.Matches(state)||!ReferenceEquals(cache.Snapshot,snapshot))
            {
                if(Caches.Count>=64)Caches.Clear();
                var rows=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Units.Where(x=>x.Alive).ToDictionary(x=>x.NativeId);
                cache=new(new(state),snapshot,rows);Caches[key]=cache;
            }
            return cache.Rows.GetValueOrDefault(Convert.ToUInt32(Native.Get(unit,"id")));
        }
        internal static byte RememberedOwner(object state,object unit)=>Row(state,unit)?.Rogue?.Owner??Convert.ToByte(Native.Get(unit,"owner"));
        internal static bool IsRogue(object state,object? unit)=>unit is not null&&Row(state,unit)?.Rogue is not null;
        internal static bool CanCommand(object state,object unit,byte actor)
        {
            if(Native.Int(Native.Get(state,"CurrentPlayer"))!=actor||Native.Int(Native.Get(unit,"owner"))!=actor)return false;
            var rogue=Row(state,unit)?.Rogue;
            return rogue is null||(rogue.Commander==actor&&rogue.AcquiredTurn==Convert.ToUInt32(Native.Get(state,"CurrentTurn")));
        }
        internal static object? At(object state,object coordinate)
        {var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),coordinate);return tile is null?null:Native.Get(tile,"unit");}
        internal static void SetOwner(object unit,byte owner)
        {Native.Set(unit,"owner",owner);if(Native.Get(unit,"passengerUnit") is object cargo)Native.Set(cargo,"owner",owner);}
        internal static void Clear(){Caches.Clear();AttackGeneration++;CurrentAttack=null;}
        internal static void Install(Harmony h)
        {
            var p=typeof(NativeRogueRules);
            Native.Hook(h,Native.M("UnitDataExtensions","CanCapture","UnitState","GameState","TileData","System.Boolean","System.Boolean"),p,nameof(Ban));
            Native.Hook(h,Native.M("UnitDataExtensions","CanExamineRuins","UnitState","GameState","TileData"),p,nameof(Ban));
            Native.Hook(h,Native.M("UnitDataExtensions","CanDisband","UnitState","GameState","PlayerState"),p,nameof(Ban));
            Native.Hook(h,Native.M("StartTurnAction","Execute","GameState"),p,null,nameof(AfterStart));
            Native.Hook(h,Native.M("EndTurnCommand","Execute","GameState"),p,nameof(BeforeEnd),null,nameof(EndFinished));
            Native.Hook(h,Native.M("ConvertAction","Execute","GameState"),p,nameof(BeforeConvert),nameof(AfterConvert),nameof(ConvertFinished));
            Native.Hook(h,Native.M("TileData","IsBeingCaptured","GameState"),p,null,nameof(Siege));
            Native.Hook(h,Native.M("TileDataExtensions","CalculateWork","TileData","GameState","PlayerState","System.Int32"),p,null,nameof(Work));
            Native.Hook(h,Native.M("UnitDataExtensions","GetAttackOptionsAtPosition","GameState","System.Byte","WorldCoordinates","System.Int32","System.Boolean","UnitState","System.Boolean"),p,null,nameof(AttackOptions));
            Native.Hook(h,Native.M("BattleHelpers","GetBattleResults","GameState","UnitState","UnitState","System.Boolean","System.Boolean"),p,nameof(Battle));
            Native.Hook(h,Native.M("UnitState","HasActivePeaceTreaty","GameState","PlayerState"),p,nameof(CombatRelation));
            Native.Hook(h,Native.M("AttackCommand","Execute","GameState"),p,nameof(BeforeAttack),null,nameof(AttackFinished));
            Native.Hook(h,Native.M("AttackAction","Execute","GameState"),p,nameof(BeforeAttack),null,nameof(AttackFinished));
            Native.Hook(h,Native.M("InfiltrateAction","Execute","GameState"),p,nameof(BeforeAttack),null,nameof(AttackFinished));
            Native.Hook(h,Native.M("PlayerDiplomacyExtensions","ModifyDiplomacyRelationsFromAttack","PlayerState","System.Byte","GameState"),p,nameof(Diplomacy));
            Native.Hook(h,Native.M("PlayerExtensions","ModifyAggressionsFromAttack","PlayerState","PlayerState","TileData","GameState"),p,nameof(Diplomacy));
        }
        public static bool Ban(object __0,object __1,ref bool __result)
        {if(!IsRogue(__1,__0))return true;__result=false;return false;}
        public static void Siege(object __instance,object __0,ref bool __result)
        {if(Native.Get(__instance,"unit") is object unit&&IsRogue(__0,unit)&&Native.Int(Native.Get(__instance,"owner"))!=0)__result=true;}
        public static void Work(object __0,object __1,ref int __result)
        {if(Native.Get(__0,"unit") is object unit&&IsRogue(__1,unit))__result=0;}
        internal sealed record OwnerBefore(object Unit,byte Owner);
        internal sealed record Ending(object State,OwnerBefore[] Owners,NativeFeignSupportEffects.Scope? Effects);
        public static void BeforeEnd(object __0,out Ending? __state)
        {
            __state=null;var snapshot=NativeGameStateEnvelope.Get(__0);if(snapshot is null)return;
            var graph=NativeLedgerState.Graph(__0);var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
            __state=new(__0,ledger.Units.Where(x=>x.Alive&&x.Rogue is not null&&graph.ContainsKey(x.NativeId)).Select(x=>new OwnerBefore(graph[x.NativeId].Unit,Convert.ToByte(Native.Get(graph[x.NativeId].Unit,"owner")))).ToArray(),NativeFeignSupportEffects.BeginEndTurn(__0));
            foreach(var row in ledger.Units.Where(x=>x.Alive&&x.Rogue is not null&&graph.ContainsKey(x.NativeId)))SetOwner(graph[row.NativeId].Unit,row.Rogue!.Owner);
        }
        public static Exception? EndFinished(Exception? __exception,Ending? __state)
        {
            if(__state is null)return __exception;
            foreach(var item in __state.Owners)SetOwner(item.Unit,item.Owner);
            return NativeFeignSupportEffects.EndTurnFinished(__state.Effects,__exception);
        }
        public static void AfterStart(object __instance,object __0)
        {
            var snapshot=NativeGameStateEnvelope.Get(__0);if(snapshot is null)return;
            byte player=Convert.ToByte(Native.Get(__instance,"PlayerId"));
            var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
            if(!ledger.Units.Any(x=>x.Alive&&x.Rogue?.Owner==player))return;
            // Native StartTurn has now computed/queued income using the sieged
            // board. Return before interactive rewards; do not run turn effects twice.
            var graph=NativeLedgerState.Graph(__0);var rows=ledger.Units.Select(row=>
            {
                if(!row.Alive||row.Rogue?.Owner!=player||!graph.TryGetValue(row.NativeId,out var body))return row;
                SetOwner(body.Unit,player);Native.Set(body.Unit,"moved",false);Native.Set(body.Unit,"attacked",false);
                if(Native.Get(body.Unit,"passengerUnit") is object cargo){Native.Set(cargo,"moved",false);Native.Set(cargo,"attacked",false);}
                return row with{Rogue=null};
            }).ToArray();
            NativeGameStateEnvelope.Set(__0,snapshot.Revision,NativeIdentityLedgerCodec.Encode(ledger with{Units=rows},snapshot.Revision));
            Native.Set(__0,"HasFlaggedNeedForUpdateRoutes",true);
        }
        internal sealed record Conversion(object Unit,LedgerUnit Row,bool Moved,bool Attacked)
        {internal bool Completed;}
        public static void BeforeConvert(object __instance,object __0,out Conversion? __state)
        {
            __state=null;var unit=At(__0,Native.Get(__instance,"Target")!);if(unit is null)return;
            var row=Row(__0,unit);if(row?.Rogue is not null)
            {
                __state=new(unit,row,(bool)Native.Get(unit,"moved")!,(bool)Native.Get(unit,"attacked")!);
                // Native conversion transfers score/home from the previous
                // owner. Its bookkeeping must debit the remembered owner.
                SetOwner(unit,row.Rogue.Owner);
            }
        }
        public static void AfterConvert(object __0,Conversion? __state)
        {
            if(__state is null)return;
            var snapshot=NativeGameStateEnvelope.Get(__0);if(snapshot is null)return;
            var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
            Native.Set(__state.Unit,"moved",__state.Moved);Native.Set(__state.Unit,"attacked",__state.Attacked);
            var rows=ledger.Units.Select(x=>x.LogicalId==__state.Row.LogicalId?x with{Rogue=null}:x).ToArray();
            NativeGameStateEnvelope.Set(__0,snapshot.Revision,NativeIdentityLedgerCodec.Encode(ledger with{Units=rows},snapshot.Revision));
            __state.Completed=true;
        }
        public static Exception? ConvertFinished(Exception? __exception,Conversion? __state)
        {
            if(__state is null||(__exception is null&&__state.Completed))return __exception;
            SetOwner(__state.Unit,__state.Row.Rogue!.Commander);
            NativeRegroupSession.Fault("Rogue permanent conversion did not finish.");return __exception;
        }
        public static void AttackOptions(object __0,byte __1,object __2,int __3,object? __5,ref object __result)
        {
            var state=__0;var unit=__5??At(state,__2);if(unit is null||NativeGameStateEnvelope.Get(state) is null)return;
            string form=Native.EnumName(Native.Get(unit,"type")!);
            if(RecruitmentCatalog.IsMimicId(form)||SpyCatalog.IsSpyId(form)||FoggerCatalog.IsFoggerId(form))return;
            bool converts=(bool)Native.Invoke(Native.M("UnitDataExtensions","HasAbility","UnitState","Polytopia.Data.UnitAbility+Type"),null,unit,Native.EnumValue("Polytopia.Data.UnitAbility+Type","convert"))!;
            bool sourceRogue=IsRogue(state,unit);byte actor=__1;
            bool infiltrate=(bool)Native.Invoke(Native.M("UnitDataExtensions","HasAbility","UnitState","Polytopia.Data.UnitAbility+Type"),null,unit,Enum.ToObject(Native.T("Polytopia.Data.UnitAbility+Type"),35))!;
            if(Native.Int(Native.Get(Native.Get(unit,"UnitData")!,"attack"))<=0&&!converts&&!infiltrate)return;
            var area=Native.Invoke(Native.M("MapDataExtensions","GetAreaSorted","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),null,Native.Get(state,"Map"),__2,__3,true,false)!;
            var extra=new List<object>();
            foreach(var tile in Native.Items(area))
            {
                if(!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,actor)!)continue;
                if(infiltrate)
                {
                    if(!sourceRogue||Native.Int(Native.Get(tile,"owner"))==0||
                        !(bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),tile,Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!||
                        (bool)Native.Invoke(Native.M("TileData","IsBeingCaptured","GameState"),tile,state)!)continue;
                    var city=Native.Get(tile,"improvement");
                    if(city is null||(bool)Native.Invoke(Native.M("ImprovementState","HasEffect","ImprovementEffect"),city,Enum.ToObject(Native.T("ImprovementEffect"),1))!)continue;
                    extra.Add(Native.Get(tile,"coordinates")!);continue;
                }
                var target=Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),tile,state,actor,false);
                if(target is null||Native.Same(target,unit))continue;
                if(sourceRogue||IsRogue(state,target))extra.Add(Native.Get(tile,"coordinates")!);
            }
            if(extra.Count==0)return;
            var all=Native.Items(__result).ToList();
            foreach(var pos in extra)if(!all.Any(p=>Native.Int(Native.Get(p,"X"))==Native.Int(Native.Get(pos,"X"))&&Native.Int(Native.Get(p,"Y"))==Native.Int(Native.Get(pos,"Y"))))all.Add(pos);
            __result=Native.NewList("WorldCoordinates",all);
        }
        public static void Battle(object __0,object __1,object __2,ref bool __3)
        {if(IsRogue(__0,__1)||IsRogue(__0,__2))__3=false;}
        // The native command checks target.HasActivePeaceTreaty before it
        // calls GetBattleResults. Same-owner bodies return true there as well.
        // Keep native ownership intact and exempt only this exact combat query.
        [ThreadStatic] static AttackScope? CurrentAttack;
        static int AttackGeneration;
        internal sealed class AttackScope
        {
            internal readonly object State,Map,Action;
            internal readonly AttackScope? Previous;
            internal readonly int Generation;
            internal readonly uint Turn;internal readonly byte CurrentPlayer;internal readonly Guid Epoch;
            internal NativeGameStateEnvelope.Snapshot? EpochSnapshot;internal Guid ObservedEpoch;
            internal object? Source,Target;internal ulong SourceId;internal byte Actor;
            internal bool Authorized,SourceRogue,TargetRogue;
            internal AttackScope(object state,object action)
            {State=state;Map=Native.Get(state,"Map")!;Action=action;Previous=CurrentAttack;Generation=AttackGeneration;
             Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));CurrentPlayer=Convert.ToByte(Native.Get(state,"CurrentPlayer"));
             var snapshot=NativeGameStateEnvelope.Get(state);Epoch=snapshot is null?Guid.Empty:NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Epoch;EpochSnapshot=snapshot;ObservedEpoch=Epoch;}
        }
        static bool IsAction(object action,string type){try{Native.As(action,type);return true;}catch{return false;}}
        static bool Root(object state,object unit)=>Native.Same(At(state,Native.Get(unit,"coordinates")!),unit);
        static bool Bound(AttackScope s)
        {
            if(!s.Authorized||s.Generation!=AttackGeneration||!NativeRegroupSession.Ready(s.State)||Native.EnumName(Native.Get(s.State,"CurrentState")!)!="started"||
                !Native.Same(Native.Get(s.State,"Map"),s.Map)||Convert.ToUInt32(Native.Get(s.State,"CurrentTurn"))!=s.Turn||
                Convert.ToByte(Native.Get(s.State,"CurrentPlayer"))!=s.CurrentPlayer||s.Source is null||!Root(s.State,s.Source)||
                Native.Int(Native.Get(s.Source,"owner"))!=s.Actor)return false;
            var snapshot=NativeGameStateEnvelope.Get(s.State);
            if(snapshot is null)return false;
            if(!ReferenceEquals(snapshot,s.EpochSnapshot)){s.ObservedEpoch=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Epoch;s.EpochSnapshot=snapshot;}
            return s.ObservedEpoch==s.Epoch&&
                Row(s.State,s.Source) is {Alive:true} row&&row.LogicalId==s.SourceId&&(row.Rogue is not null)==s.SourceRogue;
        }
        public static void BeforeAttack(object __instance,object __0,out AttackScope __state)
        {
            // Even an unrelated nested invocation fences an outer Rogue scope.
            __state=new(__0,__instance);CurrentAttack=__state;
            if(!NativeRegroupSession.Ready(__0)||Native.EnumName(Native.Get(__0,"CurrentState")!)!="started")return;
            var source=At(__0,Native.Get(__instance,"Origin")!);if(source is null||!Root(__0,source))return;
            var row=Row(__0,source);if(row is not {Alive:true})return;
            byte actor=Convert.ToByte(Native.Get(__instance,"PlayerId"));
            if(Native.Int(Native.Get(source,"owner"))!=actor)return;
            bool command=IsAction(__instance,"AttackCommand");
            if(command)
            {
                if(!CanCommand(__0,source,actor)||Native.Int(Native.Get(source,"health"))<=0||
                    Convert.ToUInt32(Native.Get(__instance,"UnitId"))!=Convert.ToUInt32(Native.Get(source,"id")))return;
                object?[] args={__0,null};
                if(!(bool)Native.Invoke(Native.M("AttackCommand","IsValid","GameState","System.String&"),__instance,args)!)return;
            }
            else if(!Native.Items(Native.Get(__0,"ActionStack")!).Any(x=>Native.Same(x,__instance)))return;
            var target=At(__0,Native.Get(__instance,"Target")!);
            __state.Source=source;__state.Target=target;__state.SourceId=row.LogicalId;__state.Actor=actor;
            __state.SourceRogue=row.Rogue is not null;__state.TargetRogue=IsRogue(__0,target);__state.Authorized=true;
        }
        public static bool CombatRelation(object __instance,object __0,object __1,ref bool __result)
        {
            var s=CurrentAttack;
            if(s is null||!Native.Same(s.State,__0)||!Bound(s)||Native.Int(Native.Get(__1,"Id"))!=s.Actor||
                Native.Same(__instance,s.Source)||!Root(__0,__instance))return true;
            // A Rogue source can hit any body selected by its native command,
            // including native splash neighbors. An ordinary source gains only
            // the exception for its individual Rogue target, never its allies.
            if(!s.SourceRogue&&(!Native.Same(__instance,s.Target)||!s.TargetRogue||!IsRogue(__0,__instance)))return true;
            __result=false;return false;
        }
        public static Exception? AttackFinished(Exception? __exception,AttackScope? __state)
        {if(__state is not null&&__state.Generation==AttackGeneration&&ReferenceEquals(CurrentAttack,__state))CurrentAttack=__state.Previous;return __exception;}
        public static bool Diplomacy()
        {var s=CurrentAttack;return s is null||!Bound(s)||!s.SourceRogue&&!s.TargetRogue;}
    }
}
