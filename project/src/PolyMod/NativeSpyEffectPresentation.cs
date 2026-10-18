using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Effects follow an accepted command. They never apply or delay its rules.
    // Before-fog observer permissions allow the victim to see the impact on a
    // formerly visible tile, without exposing an unknown source or relay path.
    internal static class NativeSpyEffectPresentation
    {
        sealed record Pending(NativeLedgerOwnership.WeakIdentity State,NativeExchangeIntent Intent,byte[] Observers);
        sealed class Job
        {
            internal object State=null!,Map=null!,Client=null!,Player=null!,Callback=null!,Target=null!,TargetData=null!;
            internal object? Projectile,Renderer;internal NativeExchangeIntent Intent=null!;
            internal byte Viewer;internal double Start;internal bool Done,Hit,Refreshed,Finishing;
            internal Point From,To,Up;internal float Size;
        }
        internal readonly record struct Point(float X,float Y,float Z)
        {
            internal object Box()=>Activator.CreateInstance(Native.T("UnityEngine.Vector3"),X,Y,Z)!;
            internal static Point Read(object v)=>new(Scalar(v,0),Scalar(v,1),Scalar(v,2));
            static float Scalar(object v,int axis)=>Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Vector3","get_Item","System.Int32"),v,axis));
        }
        static readonly Dictionary<(long,Guid,ulong),Pending> Waiting=new();
        static readonly HashSet<(long,Guid,ulong,byte)> Seen=new();
        static readonly List<Job> Jobs=new();static readonly HashSet<string> Errors=new();
        static bool Clearing;static object? ClosingMap;
        internal static Func<double> Clock=()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        internal const double ImpactAt=.45,CompleteAt=.72;
        internal static int ActiveCount=>Jobs.Count;
        internal static void Install(Harmony h)
        {
            SmokeGrenadeAssets.PrepareSound();
            Native.Hook(h,Native.M("BuildReaction","Execute","Il2CppSystem.Action"),typeof(NativeSpyEffectPresentation),nameof(BeforeReaction));
            Native.Hook(h,Native.M("MapRenderer","LateUpdate"),typeof(NativeSpyEffectPresentation),null,nameof(Tick));
            Native.Hook(h,Native.M("MapRenderer","OnDestroy"),typeof(NativeSpyEffectPresentation),nameof(Destroying));
        }
        static void Error(Exception e){if(Errors.Count<16&&Errors.Add(e.Message))Entry.Note("spy-effect-error",e.ToString());}
        internal static void Record(NativeExchangePlan plan,byte[] observers)
        {
            if(plan.AlreadyCommitted||plan.Intent.Kind!=NativeExchangeKind.Blackout)return;
            // Caller captures these IDs before clearing exploration. At most
            // 254 real participants; the initiating player always knows its aim.
            var permitted=observers.Append(plan.Intent.Actor).Where(p=>p is >0 and <255).Distinct().OrderBy(p=>p).ToArray();
            // Observer receipts may outlive an animation (hotseat playback),
            // but must not keep discarded simulation game states alive.
            foreach(var key in Waiting.Where(p=>p.Value.State.Target() is null).Select(p=>p.Key).ToArray())Waiting.Remove(key);
            if(Waiting.Count>=128)Waiting.Remove(Waiting.Keys.First());
            Waiting[(Native.Pointer(plan.State),plan.Intent.Epoch,plan.Intent.Sequence)]=new(new(plan.State),plan.Intent,permitted);
        }
        static bool Active(object? v)=>Native.UnityAlive(v)&&(bool)Native.Get(Native.Get(v!,"gameObject")!,"activeInHierarchy")!;
        static bool Current(Job j)=>!j.Done&&!Clearing&&NativeRegroupSession.Ready(j.State)&&Active(j.Map)&&!Native.Same(j.Map,ClosingMap)&&
            Native.Same(j.State,Native.Static("GameManager","GameState"))&&Native.Same(j.Map,Native.Static("MapRenderer","Current"))&&
            Native.Same(j.Client,Native.Static("GameManager","Client"))&&Native.Same(j.Player,Native.Static("GameManager","LocalPlayer"))&&
            Native.Same(j.State,Native.Get(j.Client,"GameState"))&&j.Viewer==Convert.ToByte(Native.Get(j.Player,"Id"))&&
            !(bool)Native.Get(j.Client,"IsReplay")!&&!(bool)Native.Get(j.Client,"IsSpectating")!&&!(bool)Native.Static("HotSeatOverlay","IsActive")!&&
            (bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,j.Viewer)!;
        static object Coordinates(int x,int y)=>Activator.CreateInstance(Native.T("WorldCoordinates"),x,y)!;
        static object? Tile(Job j,int x,int y,bool explored)
        {
            if(!Current(j))return null;var at=Coordinates(x,y);
            var data=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(j.State,"Map"),at);
            var tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),j.Map,at);
            if(data is null||!Active(tile)||!Native.Same(Native.Get(tile!,"Data"),data))return null;
            if(explored&&(!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,j.Viewer)!||(bool)Native.Get(tile!,"IsHidden")!))return null;
            return tile;
        }
        static bool Committed(Job j)
        {
            var snapshot=NativeGameStateEnvelope.Get(j.State);
            return snapshot is not null&&NativeExchangeIntentCodec.AlreadyCommitted(NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision),snapshot.Revision,j.Intent);
        }
        static bool TargetCurrent(Job j)=>Current(j)&&Native.Same(j.Target,Tile(j,j.Intent.Partner.X,j.Intent.Partner.Y,false))&&Native.Same(j.TargetData,Native.Get(j.Target,"Data"));
        public static bool BeforeReaction(object __instance,object __0)
        {
            var action=Native.Get(__instance,"action");if(action is null||!NativeExchangeCommands.IsExchange(action))return true;
            var intent=NativeExchangeCommands.Get(action);if(intent is null||intent.Kind is not (NativeExchangeKind.Blackout or NativeExchangeKind.Blackmail))return true;
            var j=new Job{Intent=intent,Callback=__0,Start=Clock()};bool handed=false;
            try
            {
                NativeExchangeCommands.ValidateBase(action,intent);
                j.State=Native.Static("GameManager","GameState")!;j.Map=Native.Static("MapRenderer","Current")!;j.Client=Native.Static("GameManager","Client")!;j.Player=Native.Static("GameManager","LocalPlayer")!;
                if(j.State is null||j.Client is null||j.Player is null||!Active(j.Map))return false;
                j.Viewer=Convert.ToByte(Native.Get(j.Player,"Id"));if(!Current(j)||!Committed(j))return false;
                var key=(Native.Pointer(j.State),intent.Epoch,intent.Sequence,j.Viewer);
                if(Seen.Count>=8192||!Seen.Add(key))return false;
                if(intent.Kind==NativeExchangeKind.Blackmail)
                {
                    if(Tile(j,intent.Initiator.X,intent.Initiator.Y,true) is not null)BlackmailSound();
                    Refresh(j);return false;
                }
                if(!Waiting.TryGetValue((Native.Pointer(j.State),intent.Epoch,intent.Sequence),out var pending)||!pending.State.Matches(j.State)||
                    !pending.Observers.Contains(j.Viewer)||!NativeExchangeIntentCodec.Fingerprint(pending.Intent).SequenceEqual(NativeExchangeIntentCodec.Fingerprint(intent)))return false;
                j.Target=Tile(j,intent.Partner.X,intent.Partner.Y,false)!;if(j.Target is null||Jobs.Count>=8)return false;
                j.TargetData=Native.Get(j.Target,"Data")!;j.To=Point.Read(Native.Get(j.Target,"VisualCenter")!);
                j.Size=Convert.ToSingle(Native.Static("MapRenderer","TILE_WIDTH"));if(!float.IsFinite(j.Size)||j.Size<=0)throw new InvalidOperationException("Invalid native tile width.");
                Jobs.Add(j);handed=true;
                var source=Tile(j,intent.Initiator.X,intent.Initiator.Y,true);
                var unit=source is null?null:Native.Get(source,"unit");
                if(Active(unit)&&(bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!&&!(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!)
                {
                    var body=Native.Get(unit!,"UnitState");
                    if(body is not null&&Convert.ToUInt32(Native.Get(body,"id"))==intent.Initiator.NativeId)Launch(j,source!,unit!);
                }
                // Start flight timing after a first-use texture decode so the
                // initial throw gets the same visible duration as later throws.
                j.Start=Clock();
                // A hidden source gives no revealing line. The known target
                // still receives the same timed impact/poof/audio cue.
            }
            catch(Exception e){Error(e);End(j,true);}
            finally{if(!handed)End(j,true);}
            return false;
        }
        static void Launch(Job j,object source,object unit)
        {
            j.From=Point.Read(Native.Get(source,"VisualCenter")!);
            var frame=Native.Get(unit,"spriteContainer")!;
            var vector=Point.Read(Native.Invoke(Native.M("UnityEngine.Transform","TransformVector","UnityEngine.Vector3"),frame,new Point(0,1,0).Box())!);
            float length=MathF.Sqrt(vector.X*vector.X+vector.Y*vector.Y+vector.Z*vector.Z);
            if(!float.IsFinite(length)||length<=0)throw new InvalidOperationException("Invalid projectile plane.");
            j.Up=new(vector.X/length,vector.Y/length,vector.Z/length);
            j.From=new(j.From.X+j.Up.X*j.Size*.2f,j.From.Y+j.Up.Y*j.Size*.2f,j.From.Z+j.Up.Z*j.Size*.2f);
            j.Projectile=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Deception smoke grenade"})!;
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),j.Projectile,false);
            var type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"))!;
            j.Renderer=Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),j.Projectile,type)!,"UnityEngine.SpriteRenderer");
            var sprite=SmokeGrenadeAssets.Projectile();Native.Set(j.Renderer,"sprite",sprite);Native.Set(j.Renderer,"color",Activator.CreateInstance(Native.T("UnityEngine.Color"),1f,1f,1f,1f));
            var overlay=Native.Get(j.Target,"overlayRenderer")!;var fog=Native.Get(j.Target,"fogOfWarRenderer")!;
            Native.Set(j.Renderer,"sortingLayerID",Native.Get(overlay,"sortingLayerID"));Native.Set(j.Renderer,"sortingOrder",Math.Max(Native.Int(Native.Get(overlay,"sortingOrder")),Native.Int(Native.Get(fog,"SortingOrder")))+30);
            Native.Set(j.Projectile,"layer",Native.Get(Native.Get(unit,"gameObject")!,"layer"));
            var transform=Native.Get(j.Projectile,"transform")!;Native.Set(transform,"rotation",Native.Get(frame,"rotation"));
            float width=Convert.ToSingle(Native.Get(Native.Get(sprite,"rect")!,"width"))/Convert.ToSingle(Native.Get(sprite,"pixelsPerUnit"));
            float scale=j.Size*.16f/width;if(!float.IsFinite(scale)||scale<=0)throw new InvalidOperationException("Invalid projectile scale.");
            Native.Set(transform,"localScale",new Point(scale,scale,scale).Box());Native.Set(transform,"position",j.From.Box());
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),j.Projectile,true);
        }
        internal static Point Position(Point from,Point to,Point up,float size,double progress)
        {
            float t=(float)Math.Clamp(progress,0,1),arc=4*t*(1-t)*size*.35f;
            return new(from.X+(to.X-from.X)*t+up.X*arc,from.Y+(to.Y-from.Y)*t+up.Y*arc,from.Z+(to.Z-from.Z)*t+up.Z*arc);
        }
        internal static void BlackmailSound()
        {
            if(Native.UnityAlive(Native.Static("AudioManager","instance"))&&(bool)Native.Invoke(Native.M("AudioManager","ShouldPlaySoundEffects"),null)!)
                Native.Invoke(Native.M("AudioManager","PlaySFX","SFXTypes","PolytopiaBackendBase.Common.SkinType","System.Single","System.Single","System.Single"),null,
                    Enum.Parse(Native.T("SFXTypes"),"TreatyReject"),Enum.Parse(Native.T("PolytopiaBackendBase.Common.SkinType"),"Default"),1f,1f,0f);
        }
        static void HideProjectile(Job j)
        {
            var projectile=j.Projectile;
            try
            {
                if(Native.UnityAlive(projectile))
                    try{Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),projectile,false);}
                    finally{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,projectile);}
            }
            finally{j.Projectile=null;j.Renderer=null;}
        }
        static void Refresh(Job j)
        {
            if(j.Refreshed||!Current(j))return;j.Refreshed=true;Native.Invoke(Native.M("MapRenderer","Refresh","System.Boolean"),j.Map,true);
        }
        public static void Tick()
        {
            SmokeGrenadeAssets.PollSound();
            foreach(var j in Jobs.ToArray())try
            {
                if(!TargetCurrent(j)){End(j,false);continue;}
                double elapsed=Clock()-j.Start;
                if(!j.Hit&&elapsed<ImpactAt&&Native.UnityAlive(j.Projectile))Native.Set(Native.Get(j.Projectile!,"transform")!,"position",Position(j.From,j.To,j.Up,j.Size,elapsed/ImpactAt).Box());
                if(!j.Hit&&elapsed>=ImpactAt)
                {
                    j.Hit=true;HideProjectile(j);
                    try{Native.Invoke(Native.M("Tile","SpawnPuff"),j.Target);}catch(Exception e){Error(e);}
                    try{if(TargetCurrent(j))SmokeGrenadeAssets.ImpactSound();}catch(Exception e){Error(e);}
                    Refresh(j);
                }
                if(elapsed>=CompleteAt)End(j,true);
            }
            catch(Exception e){Error(e);End(j,true);}
        }
        static void End(Job j,bool refresh)
        {
            if(j.Done||j.Finishing)return;
            j.Finishing=true;
            try{if(refresh)Refresh(j);}catch(Exception e){Error(e);}
            j.Done=true;Jobs.Remove(j);
            try{HideProjectile(j);}catch(Exception e){Error(e);}
            try{Native.Invoke(Native.M("Il2CppSystem.Action","Invoke"),j.Callback);}catch(Exception e){Error(e);}
        }
        public static void Destroying(object __instance)
        {
            ClosingMap=__instance;foreach(var j in Jobs.Where(j=>Native.Same(j.Map,__instance)).ToArray())End(j,false);
        }
        internal static void Clear()
        {
            Clearing=true;try{foreach(var j in Jobs.ToArray())End(j,false);Waiting.Clear();Seen.Clear();Errors.Clear();}finally{Clearing=false;ClosingMap=null;}
        }
    }
}
