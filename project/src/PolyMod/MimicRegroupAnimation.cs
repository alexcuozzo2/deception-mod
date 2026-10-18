using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Presentation only: the recipient copies its public form first; both
    // original endpoints then puff before the final fog-aware map refresh.
    // Keep/Swap share timing and effects, with no unit/path/camera/data mutation.
    internal static class MimicRegroupAnimation
    {
        const double SecondBurst=.22, FinishAt=.55;
        sealed record End(int X,int Y);
        sealed class PuffLease
        {
            internal object Puff=null!,Sequence=null!,GameObject=null!;
            internal End At=null!;
        }
        sealed class Job
        {
            internal object Map=null!,Client=null!,State=null!,Player=null!,Complete=null!;
            internal byte Viewer;
            internal double Start;
            internal bool Done,Refreshed,Second,SoundAttempted;
            internal NativeExchangeIntent Intent=null!;
            internal Func<bool>? Authorization;
            internal End? Recipient;
            internal NativeExchangeTransformationVisual.Lease? Transformation;
            internal NativeExchangeTransformationVisual.Disappearance? Disappearance;
            internal List<End> Endpoints=new();
            internal List<PuffLease> Puffs=new();
        }
        internal static Func<object,NativeExchangeIntent,bool>? ShedReaction;
        internal static Action? ShedTick,ShedClear;
        static readonly List<Job> Jobs=new();
        static bool Clearing;
        static object? ClosingMap;
        static MethodInfo PlaySound=null!,SoundEnabled=null!;
        static object ShuffleCue=null!,DefaultSkin=null!;
        static double Now=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        internal static int ActiveCount=>Jobs.Count;
        internal static void Install(Harmony harmony)
        {
            NativeExchangeCommit.PreparePresentation=NativeExchangeTransformationVisual.PrepareDisappearance;
            PlaySound=Native.M("AudioManager","PlaySFX","SFXTypes","PolytopiaBackendBase.Common.SkinType","System.Single","System.Single","System.Single");
            SoundEnabled=Native.M("AudioManager","ShouldPlaySoundEffects");
            Native.RequireReturn(PlaySound,typeof(void));Native.RequireReturn(SoundEnabled,typeof(bool));
            _=Native.M("AudioManager","get_instance");
            ShuffleCue=Enum.Parse(Native.T("SFXTypes"),"Transform",false);
            DefaultSkin=Enum.Parse(Native.T("PolytopiaBackendBase.Common.SkinType"),"Default",false);
            if(Native.Int(ShuffleCue)!=28||Native.Int(DefaultSkin)!=0)
                throw new InvalidOperationException("Pinned native Regroup cue or default skin differs.");
            foreach(var m in new[]{Native.M("BuildReaction","get_action"),Native.M("BuildReaction","Execute","Il2CppSystem.Action"),
                Native.M("BuildReaction","ShouldFocusCamera"),Native.M("MapRenderer","LateUpdate"),Native.M("MapRenderer","OnDestroy"),
                Native.M("MapRenderer","Refresh","System.Boolean"),Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),
                Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),Native.M("TileData","GetExplored","System.Byte"),
                Native.M("Tile","get_IsHidden"),Native.M("Tile","get_Data"),Native.M("ObjectPool","GetPooledObject","System.String"),
                Native.M("Puff","StartAnimation","UnityEngine.Transform","UnityEngine.Vector3"),Native.M("Puff","StartAnimation"),Native.M("Puff","get_puffSequence"),
                Native.M("Puff","get_IsUsed"),Native.M("Puff","ReturnToPool"),Native.M("Il2CppSystem.Action","Invoke")}){_=m;}
            Native.Hook(harmony,Native.M("BuildReaction","Execute","Il2CppSystem.Action"),typeof(MimicRegroupAnimation),nameof(BeforeExecute));
            Native.Hook(harmony,Native.M("BuildReaction","ShouldFocusCamera"),typeof(MimicRegroupAnimation),nameof(BeforeFocus));
            Native.Hook(harmony,Native.M("MapRenderer","LateUpdate"),typeof(MimicRegroupAnimation),null,nameof(AfterFrame));
            Native.Hook(harmony,Native.M("MapRenderer","OnDestroy"),typeof(MimicRegroupAnimation),nameof(MapDestroying));
            Native.Hook(harmony,Native.M("Puff","StartAnimation"),typeof(MimicRegroupAnimation),nameof(PuffRestarting));
        }
        static object? ActionOf(object reaction)=>Native.Get(reaction,"action");
        public static bool BeforeFocus(object __instance,ref bool __result)
        {
#if DECEPTION_WINDOWS_PORT_CANDIDATE
            // MSVC shares this function across 15 reaction classes. Harmony
            // supplies a BuildReaction wrapper even for a different receiver.
            if(NativeWindowsHooks.Enabled&&!NativeWindowsHooks.IsReceiver(__instance,"BuildReaction"))return true;
#endif
            var action=ActionOf(__instance);if(action is null||!NativeExchangeCommands.IsExchange(action))return true;
            __result=false;return false;
        }
        public static bool BeforeExecute(object __instance,object __0)
        {
            var action=ActionOf(__instance);if(action is null||!NativeExchangeCommands.IsExchange(action))return true;
            // A61: reward has no presentation, camera focus, sound or delay.
            if(NativeExchangeCommands.Get(action)?.Kind==NativeExchangeKind.LighthouseSecret)
            {NativeLighthouseSecretPresentation.Complete(action,__0);return false;}
            if(NativeExchangeCommands.Get(action) is NativeExchangeIntent intent0&&
                (NativeExchangeCatalog.IsUpgrade(intent0.Kind)||NativeFeignSupport.IsKind(intent0.Kind)||intent0.Kind is NativeExchangeKind.Subvert or NativeExchangeKind.Blackout or NativeExchangeKind.Blackmail))return true;
            // Latch completion before any allocation/native rendering operation.
            var job=new Job{Complete=__0,Start=Now};
            try
            {
                job.Map=Native.Static("MapRenderer","Current")!;job.Client=Native.Static("GameManager","Client")!;
                job.State=Native.Static("GameManager","GameState")!;job.Player=Native.Static("GameManager","LocalPlayer")!;
                if(Clearing||Jobs.Count>=8||job.Client is null||job.State is null||job.Player is null||!Native.UnityAlive(job.Map))
                {EndJob(job,true);return false;}
                job.Viewer=Convert.ToByte(Native.Get(job.Player,"Id"));
                var intent=NativeExchangeCommands.Get(action);
                if(intent is null||(intent.Kind is not NativeExchangeKind.Regroup and not NativeExchangeKind.Disguise and not NativeExchangeKind.ShedDisguise))
                {EndJob(job,true);return false;}
                NativeExchangeCommands.ValidateBase(action,intent);
                job.Intent=intent;
                if(!Current(job)||!NativeExchangeTransformationVisual.Authorized(job.State,intent))
                {EndJob(job,true);return false;}
                if(intent.Kind==NativeExchangeKind.ShedDisguise&&ShedReaction?.Invoke(job.State,intent)==true)
                {EndJob(job,true);return false;}
                if(intent.Kind is NativeExchangeKind.Regroup or NativeExchangeKind.Disguise)
                    job.Disappearance=NativeExchangeTransformationVisual.TakeDisappearance(job.State,intent);
                // Original coordinate set is identical for Keep and Swap. Never
                // inspect or trace a true unit's post-action movement/appearance.
                foreach(var at in new[]{new End(intent.Initiator.X,intent.Initiator.Y),new End(intent.Partner.X,intent.Partner.Y)}.Distinct())
                    if(Visible(job,at) is not null)job.Endpoints.Add(at);
                if(job.Endpoints.Count==0){EndJob(job,true);return false;}
                Jobs.Add(job);
                if(intent.Kind is NativeExchangeKind.Regroup or NativeExchangeKind.Disguise)
                {
                    var recipient=intent.Kind==NativeExchangeKind.Regroup?intent.Initiator:intent.Partner;
                    job.Recipient=new End(recipient.X,recipient.Y);
                    Burst(job,true);Sound(job);
                    job.Transformation=NativeExchangeTransformationVisual.Begin(job.State,job.Map,job.Client,job.Player,intent);
                }
                else {Burst(job);Sound(job);Refresh(job);}
            }
            catch(Exception e){Note("regroup-animation-error",e);EndJob(job,true);}
            return false;
        }
        static bool Current(Job j)=>j.Client is not null&&j.State is not null&&j.Player is not null&&Native.UnityAlive(j.Map)&&!Native.Same(j.Map,ClosingMap)&&
            Native.Same(j.Map,Native.Static("MapRenderer","Current"))&&Native.Same(j.Client,Native.Static("GameManager","Client"))&&
            Native.Same(j.State,Native.Static("GameManager","GameState"))&&Native.Same(j.Player,Native.Static("GameManager","LocalPlayer"))&&
            Convert.ToByte(Native.Get(j.Player,"Id"))==j.Viewer&&
            NativeExchangeTransformationVisual.Context(j.State,j.Map,j.Client,j.Player,j.Viewer)&&(j.Authorization?.Invoke()??true);
        internal static bool StartShedCue(object state,int x,int y,Func<bool> authorized)
        {
            var job=new Job{State=state,Map=Native.Static("MapRenderer","Current")!,Client=Native.Static("GameManager","Client")!,
                Player=Native.Static("GameManager","LocalPlayer")!,Start=Now,Authorization=authorized};
            try
            {
                if(Clearing||Jobs.Count>=8||job.Player is null)return false;
                job.Viewer=Convert.ToByte(Native.Get(job.Player,"Id"));var at=new End(x,y);
                if(!Current(job)||Visible(job,at) is null)return false;
                job.Endpoints.Add(at);Jobs.Add(job);Burst(job);Sound(job);Refresh(job);return true;
            }
            catch(Exception e){Note("shed-animation-error",e);EndJob(job,false);return false;}
        }
        static object? Visible(Job j,End at)
        {
            if(!Current(j))return null;
            var coordinates=Activator.CreateInstance(Native.T("WorldCoordinates"),at.X,at.Y)!;
            var data=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(j.State,"Map"),coordinates);
            if(data is null||!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,j.Viewer)!)return null;
            var tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),j.Map,coordinates);
            if(!Native.UnityAlive(tile)||!Native.Same(Native.Get(tile!,"Data"),data)||(bool)Native.Get(tile!,"IsHidden")!)return null;
            if(!NativeExchangeTransformationVisual.CanShowEndpoint(j.State,j.Map,j.Viewer,coordinates)&&
                !NativeExchangeTransformationVisual.CanShowDisappearance(j.Disappearance,at.X,at.Y))return null;
            var go=Native.Get(tile!,"gameObject");
            return Native.UnityAlive(go)&&(bool)Native.Get(go!,"activeInHierarchy")!?tile:null;
        }
        static void Refresh(Job j)
        {
            if(j.Refreshed||!Current(j))return;
            // Mark first: a native refresh failure must not repeat the operation
            // during cleanup or invoke the command-completion callback twice.
            j.Refreshed=true;
            Native.Invoke(Native.M("MapRenderer","Refresh","System.Boolean"),j.Map,true);
        }
        static void Burst(Job j,bool recipientOnly=false)
        {
            foreach(var at in j.Endpoints)
            {
                if(recipientOnly&&at!=j.Recipient)continue;
                // One departure puff at the witnessed original endpoint is
                // enough; never add a second burst at a hidden destination.
                if(!recipientOnly&&NativeExchangeTransformationVisual.CanShowDisappearance(j.Disappearance,at.X,at.Y))continue;
                var tile=Visible(j,at);if(tile is null)continue;
                object? go=null,puff=null;bool started=false;
                try
                {
                    // Exact string used by native Tile.SpawnPuff; no invented
                    // prefab ID, Instantiate, shared sprite or material changes.
                    go=Native.Invoke(Native.M("ObjectPool","GetPooledObject","System.String"),null,"Puff");
                    if(!Native.UnityAlive(go))continue;
                    var type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("Puff"));
                    var component=Native.Invoke(Native.M("UnityEngine.GameObject","GetComponent","Il2CppSystem.Type"),go,type);
                    if(!Native.UnityAlive(component))throw new InvalidOperationException("Native Puff prefab has no Puff component.");
                    puff=Native.As(component!,"Puff");
                    var zero=Activator.CreateInstance(Native.T("UnityEngine.Vector3"),0f,0f,0f)!;
                    Native.Invoke(Native.M("Puff","StartAnimation","UnityEngine.Transform","UnityEngine.Vector3"),puff,Native.Get(tile,"transform"),zero);
                    var sequence=Native.Get(puff,"puffSequence")??throw new InvalidOperationException("Native Puff animation sequence was not created.");
                    j.Puffs.Add(new(){Puff=puff,Sequence=sequence,GameObject=go!,At=at});started=true;
                }
                finally
                {
                    // This is still the synchronous checkout/start extent; no
                    // lease escaped if initialization threw. Return only that GO.
                    if(!started&&Native.UnityAlive(go))
                    {
                        if(Native.UnityAlive(puff))ReturnCheckout(go!,puff!);
                        else Native.Invoke(Native.M("ObjectPool","ReturnObject","UnityEngine.GameObject"),null,go);
                    }
                }
            }
        }
        static void Sound(Job j)
        {
            if(j.SoundAttempted)return;
            if(!j.Puffs.Any(p=>Owns(p)&&Visible(j,p.At) is not null))return;
            j.SoundAttempted=true;
            try
            {
                // Exactly one centered native transformation cue accompanies the
                // first successfully started, visible puff burst. Do not derive
                // audio skin/pan from either participant or a hidden endpoint.
                if(j.Done||!Current(j)||!Native.UnityAlive(Native.Static("AudioManager","instance"))||
                    !(bool)Native.Invoke(SoundEnabled,null)!)return;
                if(!j.Puffs.Any(p=>Owns(p)&&Visible(j,p.At) is not null))return;
                Native.Invoke(PlaySound,null,ShuffleCue,DefaultSkin,1f,1f,0f);
            }
            catch(Exception e){Note("regroup-animation-audio-error",e);}
            // Audio failures never fail gameplay, retry playback, or suppress the
            // remaining already-committed animation/completion callback.
        }
        static void ReturnCheckout(object go,object puff)
        {
            object? sequence=Native.Get(puff,"puffSequence");
            try{Native.Invoke(Native.M("Puff","ReturnToPool"),puff);}
            catch
            {
                try
                {
                    object? current=Native.Get(puff,"puffSequence");
                    if(Native.UnityAlive(puff)&&Native.UnityAlive(go)&&Native.Same(Native.Get(puff,"gameObject"),go)&&
                        ((sequence is null&&current is null)||Native.Same(sequence,current)))
                        Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,false);
                }
                catch{}
                throw;
            }
        }
        public static void PuffRestarting(object __instance)
        {
            // Native Transform overload reaches this method. Retire our old
            // authority before every restart, even if DOTween recycles the same
            // Sequence identity. This observer never suppresses native work.
            try
            {
                foreach(var job in Jobs)
                    for(int i=job.Puffs.Count-1;i>=0;i--)
                        if(Native.Same(job.Puffs[i].Puff,__instance))job.Puffs.RemoveAt(i);
            }
            catch{foreach(var job in Jobs)job.Puffs.Clear();}
        }
        static bool Owns(PuffLease p)=>Native.UnityAlive(p.Puff)&&Native.UnityAlive(p.GameObject)&&
            Native.Same(Native.Get(p.Puff,"gameObject"),p.GameObject)&&(bool)Native.Get(p.Puff,"IsUsed")!&&
            Native.Same(Native.Get(p.Puff,"puffSequence"),p.Sequence);
        static void Retire(Job j,PuffLease p)
        {
            j.Puffs.Remove(p); // A throwing native pool return is not retried.
            if(!Owns(p))return;
            try{Native.Invoke(Native.M("Puff","ReturnToPool"),p.Puff);}
            catch
            {
                // A failed pool return must not leave a visible private cue.
                // Never hide a Puff that has already been recycled/reused.
                try{if(Owns(p))Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),p.GameObject,false);}catch{}
                throw;
            }
        }
        public static void AfterFrame(object __instance)
        {
            if(Native.Same(__instance,Native.Static("MapRenderer","Current"))){NativeExchangeTransformationVisual.PruneDisappearances();ShedTick?.Invoke();TickAt(Now);}
        }
        internal static void TickAt(double now)
        {
            foreach(var job in Jobs.ToArray())
            {
                if(job.Done)continue;
                try
                {
                    if(!double.IsFinite(now)||now<job.Start||!Current(job)){EndJob(job,true);continue;}
                    foreach(var puff in job.Puffs.ToArray())
                        if(!Owns(puff)||Visible(job,puff.At) is null)Retire(job,puff);
                    if(job.Transformation is not null&&!NativeExchangeTransformationVisual.Current(job.Transformation))
                    {job.Transformation.Dispose();job.Transformation=null;}
                    double elapsed=now-job.Start;
                    if(!job.Second&&elapsed>=SecondBurst)
                    {
                        job.Second=true;Burst(job);Sound(job);
                        job.Transformation?.Dispose();job.Transformation=null;
                        Refresh(job);
                    }
                    if(elapsed>=FinishAt)EndJob(job,false);
                }
                catch(Exception e){Note("regroup-animation-frame-error",e);EndJob(job,true);}
            }
        }
        static void EndJob(Job j,bool refresh)
        {
            if(j.Done)return;j.Done=true;Jobs.Remove(j);
            try
            {
                try{j.Transformation?.Dispose();}catch(Exception e){Note("regroup-transformation-cleanup-error",e);}
                j.Transformation=null;
                if(refresh)try{Refresh(j);}catch(Exception e){Note("regroup-animation-refresh-error",e);}
                foreach(var puff in j.Puffs.ToArray())try{Retire(j,puff);}catch(Exception e){Note("regroup-animation-cleanup-error",e);}
            }
            finally
            {
                // Marked Done and removed before invoking the caller: callback
                // throws or synchronous reaction reentry cannot cause a retry.
                try{if(j.Complete is not null)Native.Invoke(Native.M("Il2CppSystem.Action","Invoke"),j.Complete);}
                catch(Exception e){Note("regroup-animation-completion-error",e);}
                j.Complete=null!;j.Puffs.Clear();
            }
        }
        public static void MapDestroying(object __instance)
        {
            NativeExchangeTransformationVisual.ClearDisappearances();
            ShedClear?.Invoke();
            ClosingMap=__instance; // Includes custom reaction reentry from completion.
            foreach(var job in Jobs.Where(j=>Native.Same(j.Map,__instance)).ToArray())EndJob(job,false);
        }
        internal static void Clear()
        {
            if(Clearing)return;Clearing=true;
            try{NativeExchangeTransformationVisual.ClearDisappearances();foreach(var job in Jobs.ToArray())EndJob(job,true);}
            finally{Clearing=false;}
        }
        static void Note(string kind,Exception e){try{Entry.Note(kind,e.Message);}catch{}}
    }
}
