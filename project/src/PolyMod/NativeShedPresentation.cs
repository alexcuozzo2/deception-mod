using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Successful backend shedding has no native BuildReaction when it is part
    // of movement. Capture its original visible event, then wait briefly for
    // the exact destination renderer; never reveal a later-discovered event.
    internal static class NativeShedPresentation
    {
        internal static Func<double> Clock=()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        sealed record Capture(object State,object Map,object Client,object Player,byte Viewer,uint Turn,
            Guid Epoch,long Revision,NativeGameStateEnvelope.Snapshot Before,ulong Logical,long Generation);
        readonly record struct Key(long State,Guid Epoch,long Revision,ulong Logical,byte Viewer);
        sealed record Pending(Capture Context,Key Key,NativeGameStateEnvelope.Snapshot Committed,uint NativeId,int X,int Y,double Deadline);
        static readonly List<Pending> Waiting=new();
        static readonly HashSet<Key> Seen=new();
        static readonly Queue<Key> History=new();
        static readonly HashSet<Key> Unobserved=new();
        static readonly Queue<Key> UnobservedHistory=new();
        static long Generation;
        internal static int ActiveCount=>Waiting.Count;
        internal static void Install()
        {
            NativeShedDisguise.PreparePresentation=Prepare;
            NativeDisguiseMovement.PreparePresentation=Prepare;
            NativeDisguiseLifecycle.PreparePresentation=Prepare;
            MimicRegroupAnimation.ShedReaction=Reaction;
            MimicRegroupAnimation.ShedTick=Tick;
            MimicRegroupAnimation.ShedClear=Clear;
        }
        internal static void Clear(){Generation++;Waiting.Clear();Seen.Clear();History.Clear();Unobserved.Clear();UnobservedHistory.Clear();}
        static void Note(Exception e){try{Entry.Note("shed-presentation-error",e.Message);}catch{}}
        static object Coordinates(int x,int y)=>Activator.CreateInstance(Native.T("WorldCoordinates"),x,y)!;
        static bool Active(object? value)=>Native.UnityAlive(value)&&(bool)Native.Get(Native.Get(value!,"gameObject")!,"activeInHierarchy")!;
        static bool Context(Capture c)=>c.Generation==Generation&&NativeExchangeTransformationVisual.Context(c.State,c.Map,c.Client,c.Player,c.Viewer)&&
            Convert.ToUInt32(Native.Get(c.State,"CurrentTurn"))==c.Turn&&NativeGameStateEnvelope.Get(c.State)?.Epoch==c.Epoch;
        static LedgerUnit? Row(object state,ulong logical)
        {var frame=NativeGameStateEnvelope.Get(state);return frame is null?null:NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision).Units.SingleOrDefault(r=>r.Alive&&r.LogicalId==logical);}
        static bool Exposed(LedgerUnit row)=>row.Kind==LedgerActualKind.Genuine?!row.Independent&&row.Appearance is null:
            row.Appearance is LedgerAppearance a&&(RecruitmentCatalog.IsMenuId(a.Form)||a.Passenger is LedgerAppearance p&&RecruitmentCatalog.IsMenuId(p.Form));
        static object? PublicTile(Capture c,object body,int x,int y,bool rendered)
        {
            if(!Context(c)||Native.Int(Native.Get(body,"health"))<=0)return null;var at=Coordinates(x,y);
            var data=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(c.State,"Map"),at);
            if(data is null||!Native.Same(Native.Get(data,"unit"),body)||!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,c.Viewer)!||
                !Native.Same(Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),data,c.State,c.Viewer,false),body))return null;
            var tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),c.Map,at);
            if(!Active(tile)||!Native.Same(Native.Get(tile!,"Data"),data)||(bool)Native.Get(tile!,"IsHidden")!)return null;
            if(!rendered)return tile;
            var unit=Native.Get(tile!,"unit");uint id=Convert.ToUInt32(Native.Get(body,"id"));
            return Active(unit)&&Native.Same(Native.Get(unit!,"UnitState"),body)&&
                Native.Same(unit,Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),c.Map,id))&&
                (bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!&&!(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!?tile:null;
        }
        static object? Body(object state,uint id)
        {object?[] found={id,null};return (bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,found)!?found[1]:null;}
        internal static Action? Prepare(object state,object body)
        {
            try
            {
                var frame=NativeGameStateEnvelope.Get(state);if(frame is null||Native.Int(Native.Get(body,"health"))<=0)return null;
                var row=NativeRegroupSession.ReadIdentity(state,Convert.ToUInt32(Native.Get(body,"id")));
                if(row is not {Alive:true,Independent:true,Appearance:not null}||Exposed(row))return null;
                var player=Native.Static("GameManager","LocalPlayer");if(player is null)return null;
                var c=new Capture(state,Native.Static("MapRenderer","Current")!,Native.Static("GameManager","Client")!,player,
                    Convert.ToByte(Native.Get(player,"Id")),Convert.ToUInt32(Native.Get(state,"CurrentTurn")),frame.Epoch,frame.Revision,frame,row.LogicalId,Generation);
                var at=Native.Get(body,"coordinates")!;
                if(!Context(c))return null;
                bool observed=PublicTile(c,body,Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")),true) is not null;
                bool completed=false;
                return ()=>{if(completed)return;completed=true;try{Complete(c,observed);}catch(Exception e){Note(e);}};
            }
            catch(Exception e){Note(e);return null;}
        }
        static void Complete(Capture c,bool observed)
        {
            if(!Context(c))return;var frame=NativeGameStateEnvelope.Get(c.State);
            // Native movement reveal publishes at the existing revision; a
            // private Shed command increments it. Both replace immutable data.
            if(frame is null||ReferenceEquals(frame,c.Before)||frame.Revision<c.Revision)return;
            if(!observed)
            {
                // A same-view native reaction must not manufacture a visible
                // event after the originally hidden body sheds its mask. A
                // separate observed pre-move capture can still authorize it.
                var row=Row(c.State,c.Logical);if(row is null||!Exposed(row))return;
                var key=new Key(Native.Pointer(c.State),c.Epoch,frame.Revision,c.Logical,c.Viewer);
                if(Unobserved.Add(key))UnobservedHistory.Enqueue(key);
                while(UnobservedHistory.Count>128)Unobserved.Remove(UnobservedHistory.Dequeue());
                return;
            }
            Queue(c,frame.Revision);
        }
        static void Queue(Capture c,long revision,bool nativeReaction=false)
        {
            var frame=NativeGameStateEnvelope.Get(c.State);if(frame?.Revision!=revision)return;
            var row=Row(c.State,c.Logical);if(row is null||!Exposed(row)||Body(c.State,row.NativeId) is not object body)return;
            var at=Native.Get(body,"coordinates")!;int x=Native.Int(Native.Get(at,"X")),y=Native.Int(Native.Get(at,"Y"));
            var key=new Key(Native.Pointer(c.State),c.Epoch,revision,c.Logical,c.Viewer);
            if(Seen.Contains(key)||nativeReaction&&Unobserved.Contains(key)||Waiting.Count>=8||PublicTile(c,body,x,y,false) is null)return;
            Seen.Add(key);History.Enqueue(key);while(History.Count>128)Seen.Remove(History.Dequeue());
            Waiting.Add(new(c,key,frame,row.NativeId,x,y,Clock()+1.5));
        }
        // Return true for every Shed reaction: this queue owns its cue and the
        // existing animation prefix still completes that native callback once.
        internal static bool Reaction(object state,NativeExchangeIntent intent)
        {
            if(intent.Kind!=NativeExchangeKind.ShedDisguise)return false;
            try
            {
                if(!NativeExchangeTransformationVisual.Authorized(state,intent))return true;
                var player=Native.Static("GameManager","LocalPlayer");var frame=NativeGameStateEnvelope.Get(state);if(player is null||frame is null)return true;
                var c=new Capture(state,Native.Static("MapRenderer","Current")!,Native.Static("GameManager","Client")!,player,
                    Convert.ToByte(Native.Get(player,"Id")),intent.Turn,intent.Epoch,intent.Revision,frame,intent.Initiator.LogicalId,Generation);
                if(!Context(c)||!NativeExchangeTransformationVisual.CanShowEndpoint(state,c.Map,c.Viewer,Coordinates(intent.Initiator.X,intent.Initiator.Y)))return true;
                Queue(c,checked(intent.Revision+1),true);
            }
            catch(Exception e){Note(e);}return true;
        }
        static bool Valid(Pending p,bool rendered)
        {
            if(!Context(p.Context)||!ReferenceEquals(NativeGameStateEnvelope.Get(p.Context.State),p.Committed))return false;var row=Row(p.Context.State,p.Context.Logical);
            return row is not null&&row.NativeId==p.NativeId&&Exposed(row)&&Body(p.Context.State,p.NativeId) is object body&&PublicTile(p.Context,body,p.X,p.Y,rendered) is not null;
        }
        internal static void Tick()
        {
            foreach(var p in Waiting.ToArray())try
            {
                if(!double.IsFinite(Clock())||Clock()>p.Deadline||!Valid(p,false)){Waiting.Remove(p);continue;}
                if(!Valid(p,true))continue;
                Waiting.Remove(p);MimicRegroupAnimation.StartShedCue(p.Context.State,p.X,p.Y,()=>Valid(p,true));
            }
            catch(Exception e){Waiting.Remove(p);Note(e);}
        }
    }
}
