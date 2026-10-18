// UI-A06. Local session presentation only; never serialized into World or used
// to authorize a command. Health in this file is DISPLAY HP.
using System;
using System.Collections.Generic;

namespace Deception.Core
{
    public readonly record struct InspectionContext(long World,int Viewer,uint Turn,uint UnitId);

    public sealed class MimicInspectionSession
    {
        readonly record struct PreferenceKey(long World,int Viewer,uint UnitId);
        readonly HashSet<PreferenceKey> Actual=new();
        InspectionContext? Selected;
        static PreferenceKey Key(InspectionContext context)=>new(context.World,context.Viewer,context.UnitId);
        public void Reset(){ClearSelection();Actual.Clear();}
        public void ClearSelection(){Selected=null;}
        public void Select(InspectionContext context,bool authorized,bool choosingTarget=false)
        {
            if(!authorized){ClearSelection();return;}
            if(choosingTarget)return;
            Selected=context;
        }
        public bool Toggle(InspectionContext context,bool authorized,bool choosingTarget=false)
        {
            if(!authorized){ClearSelection();return false;}
            if(choosingTarget)return false;
            if(Selected is not InspectionContext previous||previous!=context)
            {ClearSelection();return false;}
            var key=Key(context);
            if(!Actual.Add(key))Actual.Remove(key);
            return true;
        }
        public bool ActualFor(InspectionContext context,bool authorized)
        {
            return authorized&&Selected is InspectionContext previous&&previous==context&&StoredActualFor(context,true);
        }
        // The map can read the owner's remembered view for an unselected unit.
        // Authorization belongs to the current read; a failed read is not a reset.
        public bool StoredActualFor(InspectionContext context,bool authorized)=>authorized&&Actual.Contains(Key(context));
    }

    public sealed record ApparentHealth(int Current,int Maximum)
    {
        public bool IsFull=>Current==Maximum;
        public ApparentHealth Validate()
        {
            if(Maximum<1||Current<0||Current>Maximum)
                throw new RuleViolation("Invalid apparent health snapshot");
            return this;
        }
        public static ApparentHealth Fresh(int nativeDisplayMaximum)=>
            new ApparentHealth(nativeDisplayMaximum,nativeDisplayMaximum).Validate();
        public int ToNativeStored()=>checked(Validate().Current*10);
    }
}
