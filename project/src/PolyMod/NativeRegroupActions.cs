using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Complete intents travel through the native Build command/action/history
    // pipeline. UI offers never change a unit, the ledger, or the action budget.
    internal static partial class NativeExchangeCommands
    {
        internal static void InstallLive(Harmony harmony)
        {
            Native.Hook(harmony,Native.M("BuildCommand","IsValid","GameState","System.String&"),typeof(NativeExchangeCommands),nameof(LiveValidity));
            Native.Hook(harmony,Native.M("BuildCommand","Execute","GameState"),typeof(NativeExchangeCommands),nameof(BeforeCommand),nameof(AfterCommand),nameof(CommandFinished));
            foreach(string method in new[]{"Execute","ExecuteDefault","ExecuteV105"})
                Native.Hook(harmony,Native.M("BuildAction",method,"GameState"),typeof(NativeExchangeCommands),nameof(ExecuteAction));
        }
        internal sealed class CommandScope
        {
            internal object State=null!,Command=null!,OriginalStack=null!;
            internal object[] Prefix=Array.Empty<object>();
            internal NativeExchangeIntent Intent=null!;
        }
        [ThreadStatic] static object? ExecutingState,ExecutingAction;
        internal static bool LiveExecution(object state)=>NativeRegroupSession.Enabled&&
            ExecutingAction is not null&&Native.Same(ExecutingState,state)&&NativeRegroupSession.Ready(state);
        static object? Unit(object state,uint id)
        {
            object?[] args={id,null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,args)!?args[1]:null;
        }
        static NativeExchangeUnit Reference(object unit,ulong logical)
        {
            object at=Native.Get(unit,"coordinates")!;
            return new(logical,Convert.ToUInt32(Native.Get(unit,"id")),Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")));
        }
        internal static bool TryOffer(object state,uint initiator,uint partner,bool swap,out NativeExchangeIntent? intent,out string reason)
            =>TryOffer(state,initiator,partner,NativeExchangeKind.Regroup,swap,out intent,out reason);
        internal static bool TryOffer(object state,uint initiator,uint partner,NativeExchangeKind kind,bool swap,out NativeExchangeIntent? intent,out string reason)
        {
            intent=null;reason="Exchange is unavailable.";
            try
            {
                if(kind is not (NativeExchangeKind.Regroup or NativeExchangeKind.Disguise))return false;
                if(!NativeRegroupSession.Ready(state))return false;
                var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return false;
                var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
                var a=Unit(state,initiator);var b=Unit(state,partner);if(a is null||b is null)return false;
                var rowA=ledger.Units.SingleOrDefault(r=>r.Alive&&r.NativeId==initiator);
                var rowB=ledger.Units.SingleOrDefault(r=>r.Alive&&r.NativeId==partner);
                if(rowA is null||rowA.Kind!=LedgerActualKind.Mimic||!rowA.Independent||rowA.Appearance is null)return false;
                string type=Native.EnumName(Native.Get(b,"type")!);
                string form=RecruitmentHooks.Forms.TryGetValue(type,out var carrier)?carrier.NativeId:type;
                var source=kind==NativeExchangeKind.Disguise?rowA.Appearance:
                    NativeLedgerState.CapturePublic(state,new NativeLedgerState.Body(b,true),form,rowB);
                var request=new NativeExchangeIntent(ledger.Epoch,ledger.NextSequence,snapshot.Revision,
                    Convert.ToUInt32(Native.Get(state,"CurrentTurn")),Convert.ToByte(Native.Get(a,"owner")),kind,swap,
                    rowB is null?NativePartnerReference.Allocate:NativePartnerReference.Existing,ledger.NextLogicalId,
                    Reference(a,rowA.LogicalId),Reference(b,rowB?.LogicalId??ledger.NextLogicalId),NativeExchangeIntentCodec.AppearanceFingerprint(source));
                if(!NativeExchangePlanning.TryPlan(state,request,out _,out reason))return false;
                intent=request;return true;
            }
            catch(Exception e){reason=e.Message;return false;}
        }
        internal static object Create(NativeExchangeIntent intent)
        {
            object at=Activator.CreateInstance(Native.T("WorldCoordinates"),new object[]{intent.Initiator.X,intent.Initiator.Y})!;
            var command=Activator.CreateInstance(Native.T("BuildCommand"),new[]{(object)intent.Actor,
                Native.EnumValue("Polytopia.Data.ImprovementData+Type",NativeExchangeCatalog.Id(intent.Kind)),at})!;
            Set(command,intent);return command;
        }
        internal static bool Valid(object command,object state,bool basic,out string reason)
        {
            reason="Exchange is unavailable.";
            if(!NativeRegroupSession.Ready(state))return false;
            var intent=Get(command);if(intent is null){reason="The complete exchange choice is missing.";return false;}
            ValidateBase(command,intent);
            if(basic)
            {
                object?[] args={state,null};
                if(!(bool)Native.Invoke(Native.M("CommandBase","PassesBasicValidation","GameState","System.String&"),command,args)!)
                {reason=(string?)args[1]??"Another command is pending.";return false;}
            }
            return NativeExchangePlanning.TryPlan(state,intent,out _,out reason);
        }
        public static bool LiveValidity(object __instance,object __0,ref string __1,ref bool __result)
        {
            if(!IsExchange(__instance))return true;
            __result=false;
            try{__result=Valid(__instance,__0,true,out __1);}
            catch(Exception e){__1=e.Message;Entry.Note("regroup-validity-error",e.ToString());}
            return false;
        }
        public static bool BeforeCommand(object __instance,object __0,out CommandScope? __state)
        {
            __state=null;if(!IsExchange(__instance))return true;
            try
            {
            NativeRegroupSession.Prepare(__0);
            if(!Valid(__instance,__0,true,out var why)){NativeRegroupSession.Fault("Regroup command rejected at execution: "+why);Entry.Note("regroup-command-rejected",why);return false;}
            var stack=Native.Get(__0,"ActionStack")!;
            __state=new(){State=__0,Command=__instance,OriginalStack=stack,Prefix=Native.Items(stack).ToArray(),Intent=Get(__instance)!};
            return true;
            }
            catch(Exception e){NativeRegroupSession.Fault("Regroup command preflight: "+e.Message);throw;}
        }
        public static void AfterCommand(object __instance,object __0,CommandScope? __state)
        {
            if(__state is null)return;
            try
            {
            if(!Native.Same(__state.State,__0)||!Native.Same(__state.Command,__instance))throw new InvalidDataException("Regroup command scope changed.");
            var stack=Native.Get(__0,"ActionStack")!;
            if(!Native.Same(stack,__state.OriginalStack))throw new InvalidDataException("Regroup command replaced the action queue.");
            var entries=Native.Items(stack).ToArray();
            if(entries.Length!=__state.Prefix.Length+3||__state.Prefix.Where((x,i)=>!Native.Same(x,entries[i])).Any())
                throw new InvalidDataException("Regroup changed the native queue prefix or scaffold length.");
            object end=Native.As(entries[^3],"EndCommandAction"),deferred=Native.As(entries[^2],"RunDeferredAction"),action=Native.As(entries[^1],"BuildAction");
            if(new[]{end,deferred,action}.Any(x=>Native.Int(Native.Get(x,"PlayerId"))!=__state.Intent.Actor)||
                !Equals(Native.Get(end,"commandType"),Native.Invoke(Native.M("CommandBase","GetCommandType"),__instance))||
                !(bool)Native.Get(action,"DeductCost")!)throw new InvalidDataException("Regroup native command scaffold differs.");
            ValidateBase(action,__state.Intent);Set(action,__state.Intent);
            Entry.Note("regroup-command-queued",__state.Intent.Kind+(__state.Intent.Swap?" with Swap":" with Keep"));
            }
            catch(Exception e){NativeRegroupSession.Fault("Regroup queue binding: "+e.Message);throw;}
        }
        public static Exception? CommandFinished(Exception? __exception,CommandScope? __state)
        {
            if(__exception is not null&&__state is not null)
            {
                NativeRegroupSession.Fault("Regroup command execution: "+__exception.Message);
                var old=__state.Prefix.Select(Native.Pointer).ToHashSet();
                foreach(var value in Native.Items(Native.Get(__state.State,"ActionStack")!).Where(x=>!old.Contains(Native.Pointer(x))).ToArray())
                    if(IsExchange(value))Forget(value);
                var original=__state.OriginalStack;
                var remove=Native.M(original.GetType(),"RemoveAt","System.Int32");
                while(Native.Int(Native.Get(original,"Count"))>__state.Prefix.Length)
                    Native.Invoke(remove,original,Native.Int(Native.Get(original,"Count"))-1);
                var restored=Native.Items(original).ToArray();
                if(restored.Length!=__state.Prefix.Length||restored.Where((x,i)=>!Native.Same(x,__state.Prefix[i])).Any())
                    throw new InvalidDataException("Regroup native queue rollback could not restore its exact prefix.");
                Native.Set(__state.State,"ActionStack",original);
            }
            return __exception;
        }
        public static bool ExecuteAction(object __instance,object __0)
        {
            if(!IsExchange(__instance))return true;
            if(ExecutingAction is not null){NativeRegroupSession.Fault("Reentrant Regroup execution.");throw new InvalidOperationException("Reentrant Regroup execution.");}
            ExecutingState=__0;ExecutingAction=__instance;
            try
            {
                NativeRegroupSession.Prepare(__0);
                if(!Valid(__instance,__0,false,out var why)){NativeRegroupSession.Fault("Regroup action rejected: "+why);Entry.Note("regroup-action-rejected",why);return false;}
                if(!NativeExchangeCommit.TryCommit(__0,Get(__instance)!,out var result,out why))
                    throw new InvalidOperationException("Regroup transaction rejected: "+why);
                Entry.Note("regroup-action-committed",result!.AlreadyCommitted?"Existing receipt; no changes.":
                    result.Plan.Intent.Kind==NativeExchangeKind.LighthouseSecret?"Retired lighthouse receipt acknowledged; no state changed.":
                    result.Plan.Intent.Kind==NativeExchangeKind.Blackmail?"Unit Blackmail strengthened; quoted star cost paid; no unit action spent.":
                    result.Plan.Intent.Kind==NativeExchangeKind.ShedDisguise?"Disguise shed; real health, investment and action flags preserved; zero stars.":
                    NativeExchangeCatalog.IsUpgrade(result.Plan.Intent.Kind)?"Apparent vessel upgraded; real health, cargo, investment and action flags preserved; zero stars.":
                    "Disguise copied; "+(result.Plan.Intent.Swap?"positions swapped":"positions kept")+"; only initiating Mimic exhausted; zero stars.");
            }
            catch(Exception e){NativeRegroupSession.Fault("Regroup transaction: "+e.Message);throw;}
            finally{ExecutingAction=null;ExecutingState=null;}
            return false;
        }
    }
}
