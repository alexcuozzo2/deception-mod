using System;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // A genuine body keeps its own legal move. Compare the accepted native
    // route with a detached appearance, then remove an incompatible mask only
    // after that exact MoveAction succeeds. No path query changes live facts.
    internal static class NativeDisguiseMovement
    {
        const int MaxPath=4096;
        [ThreadStatic] static Scope? Current;
        static long Generation;
        internal static Func<object,object,object[],bool>? MimicRoute;
        internal static Func<object,object,object,int>? MimicShore;
        internal static Func<object,object,object,bool>? MimicFloodMode;
        internal static Action<object,object,bool>? ShedMimic;
        internal static Func<object,object,Action?>? PreparePresentation;
        internal sealed class Scope
        {
            internal readonly Scope? Previous;
            internal readonly object State,Map,Unit,Action,Destination;
            internal readonly LedgerUnit Identity;
            internal readonly byte Owner;
            internal readonly uint Turn;
            internal readonly long Epoch;
            internal bool Executed,Closed;
            internal readonly Action? Present;
            internal readonly bool ExhaustTransport;
            internal Scope(object state,object unit,object action,object destination,LedgerUnit identity,bool exhaustTransport=false)
            {ExhaustTransport=exhaustTransport;Previous=Current;State=state;Map=Native.Get(state,"Map")!;Unit=unit;Action=action;Destination=destination;Identity=identity;
             Owner=Convert.ToByte(Native.Get(unit,"owner"));Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));Epoch=Generation;Current=this;
             try{Present=PreparePresentation?.Invoke(state,unit);}catch(Exception e){Entry.Note("shed-move-presentation-capture-error",e.Message);}}
        }
        internal static void Clear(){Generation++;Current=null;}
        internal static void Install(Harmony h)=>Native.Hook(h,Native.M("MoveAction","Execute","GameState"),typeof(NativeDisguiseMovement),nameof(BeforeMove),nameof(AfterMove),nameof(Finished));
        static bool Bound(object state)=>NativeRegroupSession.Enabled&&NativeRegroupSession.Ready(state);
        static object? Tile(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static bool At(object a,object b)=>Native.Int(Native.Get(a,"X"))==Native.Int(Native.Get(b,"X"))&&Native.Int(Native.Get(a,"Y"))==Native.Int(Native.Get(b,"Y"));
        static bool Root(object state,object unit)=>Tile(state,Native.Get(unit,"coordinates")!) is object t&&Native.Same(Native.Get(t,"unit"),unit);
        static object? Find(object state,uint id)
        {object?[] a={id,null};return (bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,a)!?a[1]:null;}
        static bool Live(Scope s)=>!s.Closed&&s.Epoch==Generation&&Bound(s.State)&&Native.Same(s.Map,Native.Get(s.State,"Map"))&&
            Native.Int(Native.Get(s.State,"CurrentPlayer"))==s.Owner&&Convert.ToUInt32(Native.Get(s.State,"CurrentTurn"))==s.Turn&&
            Native.Int(Native.Get(s.Unit,"owner"))==s.Owner&&Native.Same(Find(s.State,s.Identity.NativeId),s.Unit);
        internal static bool IsShedding(object state,object unit)=>Current is Scope s&&Native.Same(s.State,state)&&Native.Same(s.Unit,unit)&&Live(s);
        internal static (object State,object Unit)? SheddingMimicData(object data)
            =>Current is Scope s&&s.Identity.Kind==LedgerActualKind.Mimic&&Live(s)&&Native.Same(Native.Get(s.Unit,"UnitData"),data)?(s.State,s.Unit):null;
        static bool SameAppearance(LedgerAppearance? a,LedgerAppearance? b)=>a is null?b is null:b is not null&&
            a.Form==b.Form&&a.TribeVisual==b.TribeVisual&&a.ClimateVisual==b.ClimateVisual&&a.BirthVisual==b.BirthVisual&&
            a.SourceColor==b.SourceColor&&a.Health==b.Health&&a.Maximum==b.Maximum&&a.Veteran==b.Veteran&&a.Concealment==b.Concealment&&
            a.GrowthTurns==b.GrowthTurns&&a.GrowthDueTurn==b.GrowthDueTurn&&a.Effects.SequenceEqual(b.Effects)&&SameAppearance(a.Passenger,b.Passenger);
        internal static object Profile(object state,object source,LedgerAppearance a,int depth=0)
        {
            if(depth>1)throw new InvalidDataException("Apparent movement cargo is nested.");
            var profile=Native.DetachedClone(source,"UnitState");
            var data=RecruitmentHooks.Data(state,a.Form);
            Native.Set(profile,"UnitData",data);Native.Set(profile,"type",Native.Get(data,"type"));
            Native.Set(profile,"health",a.Health);Native.Set(profile,"promotionLevel",(ushort)(a.Veteran?1:0));
            // Actual Cloak concealment can be mirrored onto a Rider/Warrior
            // mask. It keeps the public unit hidden, but cannot lend that
            // hypothetical non-Cloak profile native Invisible movement powers.
            Native.Set(profile,"effects",Native.NewList("UnitEffect",a.Effects.Where(e=>e!="Invisible"||a.Form is "cloak" or "mermaidcloak" or "cloak_boat")
                .Select(e=>Enum.Parse(Native.T("UnitEffect"),e,false))));
            Native.Set(profile,"passengerUnit",a.Passenger is null?null:Profile(state,Native.Get(source,"passengerUnit")??source,a.Passenger,depth+1));
            return profile;
        }
        internal static bool Compatible(object state,object unit,LedgerAppearance appearance,object[] path)
        {
            // Native CanMove checks moved || effects.Contains(Frozen). The
            // real command has already spent moved before this queued action;
            // only the independently apparent status is relevant here.
            if(appearance.Effects.Contains("Frozen",StringComparer.Ordinal))return false;
            var profile=Profile(state,unit,appearance);
            using var spyMovement=NativeSpyMovementProfile.Begin(state,unit,profile);
            if(!NativeMimicWaterMovement.AllowsApparentRoute(state,profile,path))return false;
            // This wrapped native value is INVOKED through its generated
            // marshaller. It must never appear in a Harmony hook signature.
            var settings=Native.Invoke(Native.M("PathFinderSettings","CreateForUnit","UnitState","GameState"),null,profile,state)!;
            int movement=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMovement","UnitState","GameState"),null,profile,state));
            if(movement<=0)return false;
            long budget=checked((long)movement*10),cost=0;
            for(int i=path.Length-2;i>=0;i--)
            {
                var from=Tile(state,path[i+1]);var to=Tile(state,path[i]);
                if(from is null||to is null||cost>=budget||
                    !(bool)Native.Invoke(Native.M("PathFinder","IsTileAccessible","TileData","TileData","PathFinderSettings"),null,to,from,settings)!)return false;
                int edge=Native.Int(Native.Invoke(Native.M("TileData","GetMovementCost","MapData","TileData","PathFinderSettings"),to,Native.Get(state,"Map"),from,settings));
                if(edge<0)throw new InvalidDataException("Negative native apparent movement cost.");
                cost=checked(cost+edge);
            }
            var destination=Tile(state,path[0])!;
            if(!(bool)Native.Invoke(Native.M("PathFinder","IsAllowedToFinishOnTile","PathFinderSettings","TileData"),null,settings,destination)!)return false;
            // Flooded cities can request native boarding/landing for a
            // hypothetical profile, but a real Mimic retains its current mode
            // there. A suppressed coast child is not an incompatible disguise.
            if(MimicFloodMode?.Invoke(state,unit,destination)==true)return spyMovement is null||spyMovement.Valid;
            int apparent=Native.Int(Native.Invoke(Native.M("ActionUtils","WillUnitEmbark","UnitState","TileData","GameState"),null,profile,destination,state));
            int actual=Native.Int(Native.Invoke(Native.M("ActionUtils","WillUnitEmbark","UnitState","TileData","GameState"),null,unit,destination,state));
            // Only a real queued coast transition can convert a land mask to
            // its vessel/cargo. An actual flier merely flying onto a port does
            // not embark; a hypothetical apparent boat cannot preserve it.
            return (spyMovement is null||spyMovement.Valid)&&(apparent==0||apparent==actual);
        }
        [HarmonyPriority(Priority.High)]
        public static void BeforeMove(object __instance,object __0,out Scope? __state)
        {
            __state=null;if(!Bound(__0)||Native.Int(Native.Get(__instance,"Reason"))!=0)return;
            try
            {
                uint id=Convert.ToUInt32(Native.Get(__instance,"UnitId"));var unit=Find(__0,id);
                if(unit is null||Native.Int(Native.Get(unit,"health"))<=0||!Root(__0,unit))return;
                var row=NativeRegroupSession.ReadIdentity(__0,id);
                if(row is not {Alive:true,Independent:true,Appearance:not null}||row.NativeId!=id||
                    row.Kind==LedgerActualKind.Mimic&&MimicRoute is null)return;
                int owner=Native.Int(Native.Get(unit,"owner"));
                if(owner!=Native.Int(Native.Get(__0,"CurrentPlayer"))||owner!=Native.Int(Native.Get(__instance,"PlayerId")))return;
                var queue=Native.Get(__0,"ActionStack")!;int count=Native.Int(Native.Get(queue,"Count"));
                if(count is <1 or >16384||!Native.Items(queue).Any(x=>Native.Same(x,__instance))||
                    !(bool)Native.Invoke(Native.M("ActionBase","IsValid","GameState"),__instance,__0)!)return;
                var nativePath=Native.Get(__instance,"Path")!;count=Native.Int(Native.Get(nativePath,"Count"));
                if(count<2||count>MaxPath)return;
                var path=Native.Items(nativePath).ToArray();
                if(!At(path[^1],Native.Get(unit,"coordinates")!)||At(path[0],path[^1]))return;
                for(int i=1;i<path.Length;i++)
                {int dx=Math.Abs(Native.Int(Native.Get(path[i],"X"))-Native.Int(Native.Get(path[i-1],"X"))),dy=Math.Abs(Native.Int(Native.Get(path[i],"Y"))-Native.Int(Native.Get(path[i-1],"Y")));
                 if(Math.Max(dx,dy)!=1)return;}
                if(!Compatible(__0,unit,row.Appearance,path)&&
                    (row.Kind==LedgerActualKind.Genuine||MimicRoute?.Invoke(__0,unit,path)==true))
                    __state=new(__0,unit,__instance,path[0],row,row.Kind==LedgerActualKind.Mimic&&MimicShore?.Invoke(__0,unit,path[0])>0);
            }
            catch(Exception e){NativeRegroupSession.Fault("Disguise movement planning: "+e.Message);throw;}
        }
        public static void AfterMove(bool __runOriginal,Scope? __state){if(__state is not null)__state.Executed=__runOriginal;}
        public static Exception? Finished(Exception? __exception,Scope? __state)
        {
            if(__state is not Scope s||s.Closed)return __exception;
            try
            {
                if(__exception is null&&s.Executed&&Live(s)&&Root(s.State,s.Unit)&&Native.Int(Native.Get(s.Unit,"health"))>0&&At(s.Destination,Native.Get(s.Unit,"coordinates")!))
                {
                    var row=NativeRegroupSession.ReadIdentity(s.State,s.Identity.NativeId);
                    if(row is {Alive:true,Independent:true}&&row.Kind==s.Identity.Kind&&row.LogicalId==s.Identity.LogicalId&&SameAppearance(row.Appearance,s.Identity.Appearance))
                    {
                        if(row.Kind==LedgerActualKind.Genuine)NativeDisguiseLifecycle.Reveal(s.State,s.Unit);
                        else ShedMimic?.Invoke(s.State,s.Unit,s.ExhaustTransport);
                        try{s.Present?.Invoke();}catch(Exception e){Entry.Note("shed-move-presentation-complete-error",e.Message);}
                    }
                }
            }
            catch(Exception e){NativeRegroupSession.Fault("Disguise movement reveal: "+e.Message);return __exception??e;}
            finally{s.Closed=true;if(ReferenceEquals(Current,s))Current=s.Epoch==Generation?s.Previous:null;}
            return __exception;
        }
    }
}
