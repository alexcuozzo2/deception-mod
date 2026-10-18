using System;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Invisible is a real native mechanical effect, not a persistence marker.
    // The ledger mirrors it. Backend lookups always use the supplied GameState;
    // only the native warning UI's own scope reads its displayed GameState.
    internal static partial class NativeDisguiseConcealment
    {
        const int Invisible=3,Hide=5;
        [ThreadStatic] static Movement? Moving;
        [ThreadStatic] static Warning? Warnings;
        [ThreadStatic] static long Generation;
        internal sealed record Movement(object State,object Unit,LedgerUnit Row,Movement? Previous,long Generation);
        internal sealed record Warning(object State,object Viewer,Warning? Previous,bool Ui,long Generation,object? Client=null);
        internal sealed record EffectChange(object State,object Unit,ulong LogicalId,uint NativeId,bool Hiding,bool PreserveMimicAction=false);
        static void Require(bool value,string message){if(!value)throw new InvalidDataException("Disguise concealment: "+message);}
        internal static bool Cloak(string form)=>form is "cloak" or "mermaidcloak" or "cloak_boat";
        internal static bool Concealing(string form)=>Cloak(form);
        internal static void Clear(){Moving=null;Warnings=null;Generation++;}
        internal static void Install(Harmony harmony)
        {
            Type patch=typeof(NativeDisguiseConcealment);
            Native.Hook(harmony,Native.M("MoveCommand","Execute","GameState"),patch,nameof(BeforeMove),null,nameof(MoveFinished));
            Native.Hook(harmony,Native.M("UnitDataExtensions","HasAbility","UnitState","Polytopia.Data.UnitAbility+Type"),patch,null,nameof(AfterAbility));
            Native.Hook(harmony,Native.M("HideAction","Execute","GameState"),patch,nameof(BeforeHide),nameof(AfterEffect),nameof(EffectFinished));
            Native.Hook(harmony,Native.M("RevealAction","Execute","GameState"),patch,nameof(BeforeReveal),nameof(AfterEffect),nameof(EffectFinished));
            Native.Hook(harmony,Native.M("HideCommand","IsValid","GameState","System.String&"),patch,null,nameof(AfterHideValid));
            Native.Hook(harmony,Native.M("UnitState","IsDetectingHiddenUnits","GameState"),patch,nameof(BeforeDetecting),null,nameof(WarningFinished));
            Native.Hook(harmony,Native.M("UnitManager","UpdateInvisibleUnitIndicatorIcons"),patch,nameof(BeforeWarningUi),null,nameof(WarningFinished));
            Native.Hook(harmony,Native.M("UnitState","IsInvisible","GameState","System.Byte"),patch,null,nameof(AfterInvisibleState));
            Native.Hook(harmony,Native.M("UnitState","IsInvisible","PlayerState"),patch,null,nameof(AfterInvisiblePlayer));
        }
        static bool Bound(object state)=>NativeRegroupSession.Enabled&&NativeRegroupSession.Ready(state);
        static object? At(object state,object coordinate)
        {
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),coordinate);
            return tile is null?null:Native.Get(tile,"unit");
        }
        static bool Root(object state,object unit)=>Native.Same(At(state,Native.Get(unit,"coordinates")!),unit);
        static LedgerUnit? Independent(object state,object unit)
        {
            if(!Root(state,unit))return null;
            var row=NativeRegroupSession.ReadIdentity(state,Convert.ToUInt32(Native.Get(unit,"id")));
            return row is {Alive:true,Independent:true,Appearance:not null}?row:null;
        }
        static object? Player(object state,byte id)
        {
            object?[] args={id,null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!?args[1]:null;
        }
        public static void BeforeMove(object __instance,object __0,out Movement? __state)
        {
            __state=null;if(!Bound(__0))return;
            object?[] args={Convert.ToUInt32(Native.Get(__instance,"UnitId")),null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),__0,args)!||args[1] is not object unit)return;
            var row=Independent(__0,unit);if(row is null)return;
            __state=new(__0,unit,row,Moving,Generation);Moving=__state;
        }
        public static void AfterAbility(object __0,object __1,ref bool __result)
        {
            if(Native.Int(__1)!=Hide||Moving is not Movement scope||!Native.Same(__0,scope.Unit))return;
            if(!Root(scope.State,__0))return;
            // A successful move may expose an incompatible disguise before
            // MoveCommand finishes queuing its follow-up Hide action.
            var row=Independent(scope.State,__0);
            if(row is null||row.LogicalId!=scope.Row.LogicalId)return;
            // Do not grant Infiltrate, movement, Scout, or other copied powers to
            // a genuine unit. Native MoveCommand owns Hide's action cost/order.
            __result=Concealing(row.Appearance!.Form)||(row.Kind==LedgerActualKind.Genuine&&ActualConcealing(__0)&&__result);
        }
        public static Exception? MoveFinished(Exception? __exception,Movement? __state)
        {if(__state is not null)Moving=__state.Generation==Generation?__state.Previous:null;return __exception;}
        public static void AfterHideValid(object __instance,object __0,ref bool __result,ref string __1)
        {
            if(!__result||!Bound(__0))return;
            var unit=At(__0,Native.Get(__instance,"Coordinates")!);if(unit is null)return;
            if(ActualSpy(unit)&&!Concealing(Independent(__0,unit)?.Appearance?.Form??"")){__result=false;__1="Spies cannot hide.";return;}
            var row=Independent(__0,unit);if(row is null||Concealing(row.Appearance!.Form)||row.Kind==LedgerActualKind.Genuine&&ActualConcealing(unit))return;
            __result=false;__1="This appearance cannot hide.";
        }
        static EffectChange? BeginEffect(object action,object state,bool hiding)
        {
            if(!Bound(state))return null;
            object? unit=At(state,Native.Get(action,"Coordinates")!);if(unit is null)return null;
            var row=Independent(state,unit);if(row is null)return null;
            NativeRegroupSession.Prepare(state);
            // Native AddEffect/RemoveEffect mutate lists in place. Keep aliases
            // outside this event intact; the native event owns its own list.
            Native.Set(unit,"effects",Native.NewList("UnitEffect",Native.Items(Native.Get(unit,"effects")!)));
            return new(state,unit,row.LogicalId,row.NativeId,hiding,hiding&&row.Kind==LedgerActualKind.Mimic&&
                !(bool)Native.Get(unit,"attacked")!&&PendingMove(action,state,unit));
        }
        static bool PendingMove(object hide,object state,object unit)
        {
            // Native MoveCommand appends MoveAction, then HideAction; the
            // stack executes Hide first. Match the immediately pending move,
            // including a serialized/reloaded queue, never an unrelated move.
            var stack=Native.Get(state,"ActionStack")!;
            int count=Native.Int(Native.Get(stack,"Count"));
            if(count is <1 or >16384||Native.Int(Native.Get(hide,"PlayerId"))!=Native.Int(Native.Get(unit,"owner"))||
                Native.Int(Native.Get(state,"CurrentPlayer"))!=Native.Int(Native.Get(unit,"owner")))return false;
            var pending=Native.Items(stack).Reverse().FirstOrDefault(a=>!Native.Same(a,hide));
            if(pending is null||Native.Int(Native.Invoke(Native.M("ActionBase","GetActionType"),pending))!=6)return false;
            var move=Native.As(pending,"MoveAction");
            if(Native.Int(Native.Get(move,"Reason"))!=0||Convert.ToUInt32(Native.Get(move,"UnitId"))!=Convert.ToUInt32(Native.Get(unit,"id"))||
                Native.Int(Native.Get(move,"PlayerId"))!=Native.Int(Native.Get(unit,"owner")))return false;
            var path=Native.Items(Native.Get(move,"Path")!).ToArray();
            if(path.Length<2)return false;
            var origin=Native.Get(unit,"coordinates")!;
            return Native.Int(Native.Get(path[^1],"X"))==Native.Int(Native.Get(origin,"X"))&&
                Native.Int(Native.Get(path[^1],"Y"))==Native.Int(Native.Get(origin,"Y"));
        }
        public static bool BeforeHide(object __instance,object __0,out EffectChange? __state)
        {
            __state=null;
            if(Bound(__0))
            {
                var unit=At(__0,Native.Get(__instance,"Coordinates")!);
                var row=unit is null?null:Independent(__0,unit);
                if(unit is not null&&ActualSpy(unit)&&!Concealing(row?.Appearance?.Form??""))return false;
                // A real Cloak still earns its native Hide/Infiltrate readiness.
                // Any appearance follows that actual native concealment. A
                // copied form cannot grant another body genuine Infiltrate.
                if(row is not null&&!Concealing(row.Appearance!.Form)&&!(row.Kind==LedgerActualKind.Genuine&&ActualConcealing(unit!)))return false;
            }
            __state=BeginEffect(__instance,__0,true);return true;
        }
        public static void BeforeReveal(object __instance,object __0,out EffectChange? __state)=>__state=BeginEffect(__instance,__0,false);
        public static void AfterEffect(EffectChange? __state,bool __runOriginal)
        {
            if(__state is not EffectChange scope||!__runOriginal)return;
            Require(Root(scope.State,scope.Unit)&&Convert.ToUInt32(Native.Get(scope.Unit,"id"))==scope.NativeId,"native hide/reveal changed its root");
            bool hidden=Native.Items(Native.Get(scope.Unit,"effects")!).Any(e=>Native.Int(e)==Invisible);
            Require(hidden==scope.Hiding,"native hide/reveal did not apply its audited effect");
            var snapshot=NativeGameStateEnvelope.Get(scope.State)??throw new InvalidDataException("Concealment event lost ledger");
            var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
            int index=Array.FindIndex(ledger.Units,r=>r.Alive&&r.Independent&&r.NativeId==scope.NativeId&&r.LogicalId==scope.LogicalId);
            Require(index>=0,"native hide/reveal lost independent identity");
            var row=ledger.Units[index];var a=row.Appearance!;
            // The real native hide/detection result governs every appearance,
            // including a genuine Cloak wearing a non-Cloak mask.
            var effects=a.Effects.Where(e=>e!="Invisible").Concat(hidden?new[]{"Invisible"}:Array.Empty<string>()).OrderBy(e=>e,StringComparer.Ordinal).ToArray();
            if(a.Concealment==(hidden?ApparentConcealment.Hidden:ApparentConcealment.Visible)&&a.Effects.SequenceEqual(effects))
            {if(scope.PreserveMimicAction)Native.Set(scope.Unit,"attacked",false);return;}
            var rows=(LedgerUnit[])ledger.Units.Clone();rows[index]=row with{Appearance=a with{Concealment=hidden?ApparentConcealment.Hidden:ApparentConcealment.Visible,Effects=effects}};
            byte[] payload=NativeIdentityLedgerCodec.Encode(ledger with{Units=rows},snapshot.Revision);
            NativeLedgerState.Validate(scope.State,snapshot.Revision,payload);
            NativeGameStateEnvelope.Set(scope.State,snapshot.Revision,payload);
            if(scope.PreserveMimicAction)Native.Set(scope.Unit,"attacked",false);
        }
        public static Exception? EffectFinished(Exception? __exception,EffectChange? __state)
        {if(__exception is not null&&__state is not null)NativeRegroupSession.Fault("Native concealment event failed: "+__exception.Message);return __exception;}
        public static void BeforeDetecting(object __instance,object __0,out Warning? __state)
        {
            __state=null;if(!Bound(__0)||!Root(__0,__instance))return;
            var viewer=Player(__0,Convert.ToByte(Native.Get(__instance,"owner")));if(viewer is null)return;
            __state=new(__0,viewer,Warnings,false,Generation);Warnings=__state;
        }
        public static void BeforeWarningUi(out Warning? __state)
        {
            __state=null;
            var state=Native.Static("GameManager","GameState");var client=Native.Static("GameManager","Client");
            if(state is null||client is null||!Bound(state))return;
            var viewer=Native.Invoke(Native.M("ClientBase","GetCurrentLocalPlayer"),client);if(viewer is null)return;
            __state=new(state,viewer,Warnings,true,Generation,client);Warnings=__state;
        }
        static bool ExtraWarning(Warning scope,object unit,object viewer)
        {
            if(!Native.Same(viewer,scope.Viewer)||!Root(scope.State,unit))return false;
            int owner=Native.Int(Native.Get(unit,"owner"));
            if(owner==Native.Int(Native.Get(viewer,"Id"))||(bool)Native.Invoke(Native.M("PlayerDiplomacyExtensions","HasPeaceWith","PlayerState","System.Byte"),null,viewer,(byte)owner)!)return false;
            var row=Independent(scope.State,unit);
            // Ordinary visible Cloaks retain ordinary native warning behavior.
            // A detected real Cloak still contributes its native-looking eye
            // warning when wearing a non-Cloak mask (actual OR apparent).
            return row is {Kind:LedgerActualKind.Genuine}&&!Cloak(row.Appearance!.Form)&&Cloak(Native.EnumName(Native.Get(unit,"type")!));
        }
        public static void AfterInvisibleState(object __instance,object __0,byte __1,ref bool __result)
        {
            if(__result||Warnings is not Warning scope||scope.Ui||!Native.Same(scope.State,__0)||__1!=Convert.ToByte(Native.Get(scope.Viewer,"Id")))return;
            __result=ExtraWarning(scope,__instance,scope.Viewer);
        }
        public static void AfterInvisiblePlayer(object __instance,object __0,ref bool __result)
        {
            if(__result||Warnings is not Warning scope||!scope.Ui)return;
            // Resolve nothing from a later/replaced UI world inside the scope.
            if(!Native.Same(Native.Static("GameManager","GameState"),scope.State)||!Native.Same(Native.Static("GameManager","Client"),scope.Client))return;
            __result=ExtraWarning(scope,__instance,__0);
        }
        public static Exception? WarningFinished(Exception? __exception,Warning? __state)
        {if(__state is not null)Warnings=__state.Generation==Generation?__state.Previous:null;return __exception;}
    }
}
