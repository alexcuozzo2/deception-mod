using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Brief, detached art at the recipient's ORIGINAL endpoint. The backend
    // exchange is already committed; refreshing its swapped positions before
    // this phase would make the donor appear to be the unit that transformed.
    internal static class NativeExchangeTransformationVisual
    {
        // A postcommit hidden body is not evidence that an observer saw its
        // disappearance. Only a successful, witnessed exchange can issue this
        // short-lived permission for the recipient's ORIGINAL tile.
        internal static Func<double> Clock=()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        sealed record Observation(object State,object Map,object Client,object Player,byte Viewer,uint Turn,long Generation,
            NativeGameStateEnvelope.Snapshot Before,NativeExchangeIntent Intent,NativeExchangeUnit Recipient,
            object Tile,object Data,byte[] Fingerprint,double Deadline);
        internal sealed class Disappearance
        {
            internal object State=null!,Map=null!,Client=null!,Player=null!,Tile=null!,Data=null!,Body=null!;
            internal NativeGameStateEnvelope.Snapshot Frame=null!;
            internal NativeExchangeIntent Intent=null!;internal NativeExchangeUnit Recipient=null!;internal LedgerUnit Row=null!;
            internal byte Viewer;internal uint Turn;internal long Generation;internal byte[] Fingerprint=null!;internal double Deadline;
        }
        static long DisappearanceGeneration;
        static readonly List<Disappearance> Disappearances=new();
        static readonly HashSet<(long State,Guid Epoch,ulong Sequence,byte Viewer)> Witnessed=new();
        static readonly Queue<(long State,Guid Epoch,ulong Sequence,byte Viewer)> WitnessHistory=new();
        internal static int PendingDisappearances=>Disappearances.Count;
        internal static void ClearDisappearances(){DisappearanceGeneration++;Disappearances.Clear();Witnessed.Clear();WitnessHistory.Clear();}
        static bool InTime(double deadline){double now=Clock();return double.IsFinite(now)&&now<=deadline&&now>=deadline-3;}
        internal static void PruneDisappearances()
        {Disappearances.RemoveAll(p=>{try{return !DisappearanceCurrent(p);}catch(Exception e){DisappearanceNote(e);return true;}});}
        internal static Action? PrepareDisappearance(object state,NativeExchangeIntent intent)
        {
            try
            {
                PruneDisappearances();
                if(intent.Kind is not NativeExchangeKind.Regroup and not NativeExchangeKind.Disguise||Disappearances.Count>=8)return null;
                var frame=NativeGameStateEnvelope.Get(state);var map=Native.Static("MapRenderer","Current");
                var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");
                if(frame is null||map is null||client is null||player is null||frame.Revision!=intent.Revision||frame.Epoch!=intent.Epoch)return null;
                byte viewer=Convert.ToByte(Native.Get(player,"Id"));
                if(!Context(state,map,client,player,viewer)||Convert.ToUInt32(Native.Get(state,"CurrentTurn"))!=intent.Turn)return null;
                var recipient=intent.Kind==NativeExchangeKind.Regroup?intent.Initiator:intent.Partner;
                // The presentation read cache intentionally omits ordinary
                // genuine units. Disguise may target one before its first mask.
                var row=frame.ReadIdentity(recipient.NativeId)??NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision).Units.SingleOrDefault(r=>r.Alive&&r.NativeId==recipient.NativeId);
                if(row is not {Alive:true}||row.LogicalId!=recipient.LogicalId)return null;
                var coordinate=Coordinates(recipient.X,recipient.Y);var tile=Visible(state,map,viewer,coordinate);
                if(tile is null||!CanShowEndpoint(state,map,viewer,coordinate))return null;
                var data=Native.Get(tile,"Data")!;var body=Native.Get(data,"unit");var rendered=Native.Get(tile,"unit");
                if(body is null||!Active(rendered)||Native.Int(Native.Get(body,"health"))<=0||
                    Convert.ToUInt32(Native.Get(body,"id"))!=recipient.NativeId||Native.Int(Native.Get(body,"owner"))!=intent.Actor||
                    !Native.Same(Native.Get(rendered!,"UnitState"),body)||!Native.Same(rendered,Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,recipient.NativeId)))return null;
                double now=Clock();if(!double.IsFinite(now))return null;
                var seen=new Observation(state,map,client,player,viewer,intent.Turn,DisappearanceGeneration,frame,intent,recipient,
                    tile,data,NativeExchangeIntentCodec.Fingerprint(intent),now+3);
                bool completed=false;
                return ()=>{if(completed)return;completed=true;try{CompleteDisappearance(seen);}catch(Exception e){DisappearanceNote(e);}};
            }
            catch(Exception e){DisappearanceNote(e);return null;}
        }
        static void CompleteDisappearance(Observation seen)
        {
            if(seen.Generation!=DisappearanceGeneration||!InTime(seen.Deadline)||
                !Context(seen.State,seen.Map,seen.Client,seen.Player,seen.Viewer)||
                Convert.ToUInt32(Native.Get(seen.State,"CurrentTurn"))!=seen.Turn||
                !seen.Fingerprint.SequenceEqual(NativeExchangeIntentCodec.Fingerprint(seen.Intent)))return;
            var frame=NativeGameStateEnvelope.Get(seen.State);
            if(frame is null||ReferenceEquals(frame,seen.Before)||frame.Revision!=seen.Intent.Revision+1||frame.Epoch!=seen.Intent.Epoch)return;
            var row=Committed(seen.State,seen.Intent,seen.Recipient);
            if(row?.Appearance?.Concealment!=ApparentConcealment.Hidden)return;
            object?[] found={row.NativeId,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),seen.State,found)!||found[1] is null)return;
            var pending=new Disappearance{State=seen.State,Map=seen.Map,Client=seen.Client,Player=seen.Player,Viewer=seen.Viewer,
                Turn=seen.Turn,Generation=seen.Generation,Frame=frame,Intent=seen.Intent,Recipient=seen.Recipient,Row=row,
                Tile=seen.Tile,Data=seen.Data,Body=found[1]!,Fingerprint=seen.Fingerprint,Deadline=seen.Deadline};
            var key=(Native.Pointer(seen.State),seen.Intent.Epoch,seen.Intent.Sequence,seen.Viewer);
            if(Disappearances.Count>=8||!DisappearanceCurrent(pending)||!Witnessed.Add(key))return;
            WitnessHistory.Enqueue(key);while(WitnessHistory.Count>128)Witnessed.Remove(WitnessHistory.Dequeue());
            Disappearances.Add(pending);
        }
        internal static Disappearance? TakeDisappearance(object state,NativeExchangeIntent intent)
        {
            PruneDisappearances();var fingerprint=NativeExchangeIntentCodec.Fingerprint(intent);
            var pending=Disappearances.FirstOrDefault(p=>Native.Same(p.State,state)&&p.Fingerprint.SequenceEqual(fingerprint));
            if(pending is not null)Disappearances.Remove(pending);
            return pending;
        }
        static bool DisappearanceCurrent(Disappearance p)
        {
            if(p.Generation!=DisappearanceGeneration||!InTime(p.Deadline)||!Context(p.State,p.Map,p.Client,p.Player,p.Viewer)||
                Convert.ToUInt32(Native.Get(p.State,"CurrentTurn"))!=p.Turn||!ReferenceEquals(NativeGameStateEnvelope.Get(p.State),p.Frame))return false;
            var row=p.Frame.ReadIdentity(p.Row.NativeId);
            if(row is not {Alive:true,Independent:true,Appearance:not null}||row.LogicalId!=p.Recipient.LogicalId||
                row.Appearance.Concealment!=ApparentConcealment.Hidden||!SameAppearance(row.Appearance,p.Row.Appearance!))return false;
            object?[] found={row.NativeId,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),p.State,found)!||!Native.Same(found[1],p.Body)||
                Native.Int(Native.Get(p.Body,"health"))<=0||Native.Int(Native.Get(p.Body,"owner"))!=p.Intent.Actor)return false;
            var expected=p.Intent.Swap?(p.Intent.Kind==NativeExchangeKind.Regroup?p.Intent.Partner:p.Intent.Initiator):p.Recipient;
            var coordinate=Native.Get(p.Body,"coordinates")!;if(!At(coordinate,expected))return false;
            var data=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(p.State,"Map"),coordinate);
            if(data is null||!Native.Same(Native.Get(data,"unit"),p.Body)||
                Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),data,p.State,p.Viewer,false) is not null)return false;
            var original=Visible(p.State,p.Map,p.Viewer,Coordinates(p.Recipient.X,p.Recipient.Y));
            return Native.Same(original,p.Tile)&&Native.Same(Native.Get(p.Tile,"Data"),p.Data);
        }
        internal static bool CanShowDisappearance(Disappearance? pending,int x,int y)=>pending is not null&&
            pending.Recipient.X==x&&pending.Recipient.Y==y&&DisappearanceCurrent(pending);
        static void DisappearanceNote(Exception e){try{Entry.Note("regroup-disappearance-error",e.Message);}catch{}}

        internal sealed class Lease : IDisposable
        {
            internal object State=null!,Map=null!,Client=null!,Player=null!,Unit=null!,Body=null!,Container=null!,ContainerGo=null!,Root=null!;
            internal NativeExchangeIntent Intent=null!;internal NativeExchangeUnit Recipient=null!;
            internal LedgerUnit Row=null!;internal byte Viewer;internal bool WasActive,Hidden,Done;
            public void Dispose()
            {
                if(Done)return;Done=true;
                // A pooled/rebound native container belongs to its new owner.
                // Destroy our child, but never restore that new occupant's art.
                try
                {
                    if(Hidden&&Owns(this))Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),ContainerGo,WasActive);
                }
                finally
                {
                    if(Native.UnityAlive(Root))
                    {
                        try{Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),Root,false);}
                        finally{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Root);}
                    }
                    Root=null!;
                }
            }
        }
        internal static bool Context(object state,object map,object client,object player,byte viewer)=>
            NativeRegroupSession.Ready(state)&&Native.Same(state,Native.Static("GameManager","GameState"))&&
            Native.Same(client,Native.Static("GameManager","Client"))&&Native.Same(player,Native.Static("GameManager","LocalPlayer"))&&
            Native.Same(Native.Get(client,"GameState"),state)&&
            Convert.ToByte(Native.Get(player,"Id"))==viewer&&Active(map)&&Native.Same(map,Native.Static("MapRenderer","Current"))&&
            !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!&&
            !(bool)Native.Static("HotSeatOverlay","IsActive")!&&
            (bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!;
        static object Coordinates(int x,int y)=>Activator.CreateInstance(Native.T("WorldCoordinates"),x,y)!;
        static bool Active(object? value)=>Native.UnityAlive(value)&&(bool)Native.Get(Native.Get(value!,"gameObject")!,"activeInHierarchy")!;
        static bool At(object coordinates,NativeExchangeUnit at)=>Native.Int(Native.Get(coordinates,"X"))==at.X&&Native.Int(Native.Get(coordinates,"Y"))==at.Y;
        static object? Visible(object state,object map,byte viewer,object coordinates)
        {
            var data=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),coordinates);
            if(data is null||!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,viewer)!)return null;
            var tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,coordinates);
            return Active(tile)&&Native.Same(Native.Get(tile!,"Data"),data)&&!(bool)Native.Get(tile!,"IsHidden")!?tile:null;
        }
        internal static bool CanShowEndpoint(object state,object map,byte viewer,object coordinates)
        {
            var tile=Visible(state,map,viewer,coordinates);if(tile is null)return false;
            var data=Native.Get(tile,"Data")!;var publicUnit=Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),data,state,viewer,false);
            var rendered=Native.Get(tile,"unit");
            // A known tile alone does not disclose an invisible occupant. The
            // backend and still-pre-shuffle rendered endpoint must both permit
            // the observer to see a unit before an endpoint puff can be emitted.
            return publicUnit is not null&&Native.Same(publicUnit,Native.Get(data,"unit"))&&Active(rendered)&&
                (bool)Native.Invoke(Native.M("Unit","GetVisible"),rendered)!&&
                !(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),rendered)!;
        }
        static bool Owns(Lease l)=>Native.UnityAlive(l.Map)&&Native.UnityAlive(l.Unit)&&Native.UnityAlive(l.Container)&&Native.UnityAlive(l.ContainerGo)&&
            Native.Same(Native.Get(l.Unit,"UnitState"),l.Body)&&Convert.ToUInt32(Native.Get(l.Body,"id"))==l.Recipient.NativeId&&
            Native.Same(Native.Get(l.Unit,"spriteContainer"),l.Container)&&
            Native.Same(Native.Get(l.Container,"gameObject"),l.ContainerGo)&&
            (bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),l.Container,Native.Get(l.Unit,"transform"))!&&
            Native.Same(l.Unit,Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),l.Map,l.Recipient.NativeId));
        static LedgerUnit? Committed(object state,NativeExchangeIntent intent,NativeExchangeUnit recipient)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return null;
            var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
            if(!NativeExchangeIntentCodec.AlreadyCommitted(ledger,snapshot.Revision,intent))return null;
            return ledger.Units.SingleOrDefault(r=>r.Alive&&r.LogicalId==recipient.LogicalId&&r.Independent&&r.Appearance is not null);
        }
        internal static bool Authorized(object state,NativeExchangeIntent intent)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return false;
            return NativeExchangeIntentCodec.AlreadyCommitted(NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision),snapshot.Revision,intent);
        }
        internal static bool Current(Lease l)
        {
            if(l.Done||!Context(l.State,l.Map,l.Client,l.Player,l.Viewer)||!Owns(l)||!Active(l.Unit)||
                MimicPresentationUi.MapView(l.Unit)==true||!(bool)Native.Invoke(Native.M("Unit","GetVisible"),l.Unit)!||
                (bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),l.Unit)!)return false;
            var snapshot=NativeGameStateEnvelope.Get(l.State);
            if(snapshot is null||snapshot.Epoch!=l.Intent.Epoch)return false;
            var row=snapshot.ReadIdentity(l.Row.NativeId);
            if(row is null||row.LogicalId!=l.Row.LogicalId||row.Kind!=l.Row.Kind||row.Appearance is null||
                !SameAppearance(row.Appearance,l.Row.Appearance!))return false;
            var source=Visible(l.State,l.Map,l.Viewer,Coordinates(l.Recipient.X,l.Recipient.Y));
            if(source is null||!Native.Same(Native.Get(source,"unit"),l.Unit))return false;
            object?[] lookup={row.NativeId,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),l.State,lookup)!||lookup[1] is null)return false;
            var current=lookup[1]!;var at=Native.Get(current,"coordinates")!;
            var expected=l.Intent.Swap?(l.Intent.Kind==NativeExchangeKind.Regroup?l.Intent.Partner:l.Intent.Initiator):l.Recipient;
            if(Native.Int(Native.Get(current,"health"))<=0||Convert.ToByte(Native.Get(current,"owner"))!=l.Intent.Actor||!At(at,expected))return false;
            var destination=Visible(l.State,l.Map,l.Viewer,at);if(destination is null)return false;
            var data=Native.Get(destination,"Data")!;
            return Native.Same(Native.Get(data,"unit"),current)&&Native.Same(current,
                Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),data,l.State,l.Viewer,false));
        }
        static bool SameAppearance(LedgerAppearance a,LedgerAppearance b)=>
            (a with{Effects=Array.Empty<string>(),Passenger=null})==(b with{Effects=Array.Empty<string>(),Passenger=null})&&a.Effects.SequenceEqual(b.Effects)&&
            (a.Passenger is null?b.Passenger is null:b.Passenger is not null&&SameAppearance(a.Passenger,b.Passenger));
        internal static Lease? Begin(object state,object map,object client,object player,NativeExchangeIntent intent)
        {
            if(intent.Kind is not NativeExchangeKind.Regroup and not NativeExchangeKind.Disguise)return null;
            byte viewer=Convert.ToByte(Native.Get(player,"Id"));if(!Context(state,map,client,player,viewer))return null;
            var recipient=intent.Kind==NativeExchangeKind.Regroup?intent.Initiator:intent.Partner;
            var row=Committed(state,intent,recipient);if(row is null)return null;
            var unit=Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,recipient.NativeId);
            if(!Active(unit))return null;
            var body=Native.Get(unit!,"UnitState");var container=Native.Get(unit!,"spriteContainer");
            if(body is null||Convert.ToUInt32(Native.Get(body,"id"))!=recipient.NativeId||!Native.UnityAlive(container))return null;
            var l=new Lease{State=state,Map=map,Client=client,Player=player,Viewer=viewer,Intent=intent,Recipient=recipient,
                Row=row,Unit=unit!,Body=body,Container=container!,ContainerGo=Native.Get(container!,"gameObject")!};
            try
            {
                if(!Current(l))return null;
                var layers=Layers(l);if(layers.Count==0)return null;
                Draw(l,layers);
                if(!Current(l)){l.Dispose();return null;}
                l.WasActive=(bool)Native.Get(l.ContainerGo,"activeSelf")!;
                l.Hidden=true;Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),l.ContainerGo,false);
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),l.Root,true);
                return l;
            }
            catch{l.Dispose();throw;}
        }
        static IReadOnlyList<NativeVisualSnapshot.Layer> Layers(Lease l)
        {
            var a=l.Row.Appearance!;
            bool custom=RecruitmentCatalog.IsMenuId(a.Form)||(SpyCatalog.IsSpyId(a.Form)||a.Form==FoggerCatalog.Id)||RecruitmentCatalog.IsMenuId(a.Passenger?.Form??"");
            if(!custom)return NativeVisualSnapshot.Capture(a.Form,a);
            bool aquarion=a.Form==AquarionCatalog.MimicId||a.Form==SpyCatalog.AquarionId||a.Passenger?.Form==AquarionCatalog.MimicId;
            bool boat=!aquarion&&RecruitmentCatalog.IsMenuId(a.Passenger?.Form??""),spy=SpyCatalog.IsSpyId(a.Form);
            var reference=NativeVisualSnapshot.Capture(boat?"transportship":"warrior",a);
            float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
            foreach(var part in reference.Where(p=>!p.Outline))
            {
                var bounds=Native.Get(part.Sprite,"bounds")!;var low=Native.Get(bounds,"min")!;var high=Native.Get(bounds,"max")!;
                foreach(float vx in new[]{Axis(low,0),Axis(high,0)})foreach(float vy in new[]{Axis(low,1),Axis(high,1)})
                {
                    float x=vx*part.ScaleX,y=vy*part.ScaleY,r=part.Rotation*MathF.PI/180;
                    float px=part.X+x*MathF.Cos(r)-y*MathF.Sin(r),py=part.Y+x*MathF.Sin(r)+y*MathF.Cos(r);
                    minX=Math.Min(minX,px);minY=Math.Min(minY,py);maxX=Math.Max(maxX,px);maxY=Math.Max(maxY,py);
                }
            }
            var player=NativeSpyPresentation.AppearancePlayer(Native.Get(l.Unit,"Owner")!,a);
            var art=a.Form==FoggerCatalog.Id?FoggerArtAssets.Layers(player):boat?UnitArtAssets.BoatLayers(player):UnitArtAssets.Layers(player,spy,aquarion);
            float width=maxX-minX,height=maxY-minY;
            float scale=Math.Min(width/((boat?UnitArtAssets.BoatVisibleWidth:UnitArtAssets.CanvasWidth)/100),
                height/((boat?UnitArtAssets.BoatVisibleHeight:UnitArtAssets.CanvasHeight)/100))*.95f*(boat?UnitArtAssets.BoatMapScale:1);
            if(!float.IsFinite(scale)||scale<=0)throw new InvalidOperationException("Transformation art frame is invalid.");
            float ox=(minX+maxX)/2+(boat?UnitArtAssets.BoatVisibleCenterX/100*scale+width*.04f:0),
                oy=minY-(boat?UnitArtAssets.BoatFootBaseline:UnitArtAssets.FootBaseline)/100*scale+(boat?height*.06f:0);
            return art.Select((p,i)=>
            {
                var frame=UnitArtAssets.Frame(p);
                float sx=p.Width/100/frame.Width*(p.MirrorX?-1:1)*scale,sy=p.Height/100/frame.Height*scale;
                return new NativeVisualSnapshot.Layer(p.Sprite,null,p.Color,ox+p.X/100*scale-frame.X*sx,
                    oy+p.Y/100*scale-frame.Y*sy,0,sx,sy,scale,0,false,p.Tintable,i);
            }).ToArray();
        }
        static float Axis(object v,int n)=>Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Vector3","get_Item","System.Int32"),v,n));
        static object V(float x,float y,float z)=>Activator.CreateInstance(Native.T("UnityEngine.Vector3"),x,y,z)!;
        static void Draw(Lease l,IReadOnlyList<NativeVisualSnapshot.Layer> layers)
        {
            var poly=Native.Get(l.Unit,"SpriteRenderer");
            var main=Native.UnityAlive(poly)?Native.Get(poly!,"spriteRenderer"):null;
            if(!Native.UnityAlive(main))
            {
                var skin=Native.Get(l.Unit,"skinVisuals");
                if(Native.UnityAlive(skin))foreach(var part in Native.Items(Native.Get(skin!,"visualParts")!))
                {
                    var union=Native.Get(part,"renderer");if(union is null)continue;
                    var renderer=Native.Get(union,"spriteRenderer");
                    if(!Native.UnityAlive(renderer))
                    {
                        var psr=Native.Get(union,"polytopiaSpriteRenderer");
                        renderer=Native.UnityAlive(psr)?Native.Get(psr!,"spriteRenderer"):null;
                    }
                    if(Native.UnityAlive(renderer)){main=renderer;break;}
                }
            }
            if(!Native.UnityAlive(main))throw new InvalidOperationException("Transformation has no native main renderer.");
            int layer=Native.Int(Native.Get(main!,"sortingLayerID")),order=Native.Int(Native.Get(main!,"sortingOrder"))+1;
            l.Root=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),"Deception copied form")!;
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),l.Root,false);
            var root=Native.Get(l.Root,"transform")!;
            Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),root,Native.Get(l.Unit,"transform"),false);
            Native.Set(root,"localPosition",V(0,0,0));Native.Set(root,"localRotation",Native.Static("UnityEngine.Quaternion","identity"));
            Native.Set(root,"localScale",V(1,1,1));
            var rendererType=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"));
            foreach(var part in layers.Where(p=>!p.Outline))
            {
                var go=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),"Deception copied part")!;
                var transform=Native.Get(go,"transform")!;
                try{Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),transform,root,false);}
                catch{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,go);throw;}
                var renderer=Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),go,rendererType)!,"UnityEngine.SpriteRenderer");
                Native.Set(go,"layer",Native.Get(Native.Get(main!,"gameObject")!,"layer"));
                Native.Set(renderer,"sprite",part.Sprite);Native.Set(renderer,"color",part.Color);
                if(Native.UnityAlive(part.Material))Native.Set(renderer,"sharedMaterial",part.Material);
                Native.Set(renderer,"sortingLayerID",layer);Native.Set(renderer,"sortingOrder",checked(order++));
                Native.Set(transform,"localPosition",V(part.X,part.Y,part.Z));Native.Set(transform,"localScale",V(part.ScaleX,part.ScaleY,part.ScaleZ));
                Native.Set(transform,"localRotation",Native.Invoke(Native.M("UnityEngine.Quaternion","Euler","System.Single","System.Single","System.Single"),null,0f,0f,part.Rotation));
            }
        }
    }
}
