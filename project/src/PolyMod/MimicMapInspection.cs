using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Local renderer ownership only. The lead supplies the session preference;
    // this adapter never changes a UnitState, world type, atlas or native command.
    internal static partial class MimicMapInspection
    {
        const float RearBadgeAlpha=0.5f;
        const string NativeUnitType="Polytopia.Data.UnitData+Type";
        sealed class Part
        {
            public object Renderer=null!,Transform=null!,Sprite=null!;
            public bool Poly,Main,PulseChanged;
            public Rgba Original,Applied;
        }
        sealed class View
        {
            public object Unit=null!,Container=null!,Root=null!;
            public uint UnitId;
            public object? Child,ChildRenderer,ChildTransform;
            public readonly List<object> ArtRenderers=new();
            public readonly List<object> OutlineRenderers=new();
            public object? NativeProperties,ArtProperties;
            public object? Badge, BadgeTransform, BadgeParent, Status;
            public readonly Dictionary<string,object> BadgeRenderers=new(StringComparer.Ordinal);
            public bool? AppliedMode;
            public bool PublicArt,NativeHidden;
            public object? PulseState,PulseMap,PulseClient,PulseViewer;
            public byte PulseOwner,PulseViewerId;
            public bool ArtIsActual;
            public object? AppearancePlayer;
            public string AppearancePlayerKey="";
            public readonly List<BadgeSprite> NativeBadgeSprites=new();
            public string EffectKey="",HealthKey="";
            public string VisualKey="",FailedVisualKey="";
            public IReadOnlyList<NativeVisualSnapshot.Layer>? NativeVisual;
            public readonly List<Part> Parts=new();
            public readonly List<BadgeColor> NativeBadgeColors=new();
            public BadgePosition? NativeBadgePosition;
        }
        sealed record BadgeSprite(string Role,object Renderer,object Transform,object? Original,object Applied)
        {
            internal object? Parent,OriginalRotation,AppliedRotation;
            internal Point? OriginalPosition,OriginalScale,AppliedPosition,AppliedScale;
            internal Rgba? OriginalColor,AppliedColor;
        }
        readonly record struct GlyphPlacement(Point Position,Point Scale);
        sealed class BadgeColor
        {
            public string Role="";
            public object Renderer=null!,Transform=null!;
            public Rgba Original,Applied;
        }
        sealed class BadgePosition
        {
            public object Transform=null!,Parent=null!;
            public Point Original,Applied;
        }
        sealed class DuplicationScope
        {public readonly List<(long Key,View View)> Views=new();}
        readonly record struct Rgba(float R,float G,float B,float A)
        {
            public Rgba Alpha(float alpha)=>new(R,G,B,alpha);
            public object Box()=>Activator.CreateInstance(Native.T("UnityEngine.Color"),R,G,B,A)!;
        }
        readonly record struct Point(float X,float Y,float Z)
        {
            public object Box()=>Activator.CreateInstance(Native.T("UnityEngine.Vector3"),X,Y,Z)!;
        }
        sealed record PrefabLayout(object Prefab,Point ContainerOrigin,Point ContainerScale,Point Low,Point High);
        static readonly Dictionary<(string Form,int Tribe,int Skin),PrefabLayout> PrefabLayouts=new();
        static readonly Dictionary<long,View> Views=new();
        static readonly Dictionary<long,int> Updating=new();
        static readonly Dictionary<long,int> Duplicating=new();
        static readonly HashSet<string> Noted=new(StringComparer.Ordinal);
        static Func<object,bool?>? Resolve;
        static bool Applying;
        static object? InvisiblePulseEffect;
        internal static void Configure(Func<object,bool?> resolve)=>Resolve=resolve;
        // Native exceptions may contain real form names or object identifiers.
        // Keep only the bounded operation name in ordinary presentation logs.
        static void Note(string kind,Exception error)
        {if(Noted.Count<20&&Noted.Add(kind))Entry.Note(kind,"Presentation operation failed; private context omitted.");}

        public static void Install(Harmony h)
        {
            InvisiblePulseEffect=Enum.Parse(Native.T("UnitEffect"),"Invisible");
            foreach(var method in new[]{
                Native.M("MapRenderer","get_Current"),Native.M("MapRenderer","get_RenderedUnits"),
                Native.M("MapRenderer","GetUnitInstance","System.UInt32"),
                Native.M("Unit","get_spriteContainer"),Native.M("Unit","get_skinVisuals"),
                Native.M("WorldObject","get_SpriteRenderer"),Native.M("SkinVisualsReference","get_visualParts"),
                Native.M("SkinVisualsReference+VisualPart","get_renderer"),Native.M("SkinVisualsReference+VisualPart","get_outlineRenderer"),
                Native.M("SkinVisualsReference+RendererUnion","get_spriteRenderer"),Native.M("SkinVisualsReference+RendererUnion","get_polytopiaSpriteRenderer"),
                Native.M("PolytopiaSpriteRenderer","get_Color"),Native.M("PolytopiaSpriteRenderer","set_Color","UnityEngine.Color"),
                Native.M("PolytopiaSpriteRenderer","get_Sprite"),Native.M("PolytopiaSpriteRenderer","get_Bounds"),
                Native.M("PolytopiaSpriteRenderer","get_SortingLayer"),Native.M("PolytopiaSpriteRenderer","get_SortingOrder"),
                Native.M("PolytopiaSpriteRenderer","get_spriteRenderer"),Native.M("PolytopiaSpriteRenderer","get_isColorLocked"),
                Native.M("PolytopiaSpriteRenderer","SetUnchangeableColor","UnityEngine.Color"),Native.M("PolytopiaSpriteRenderer","SetDirty","System.Boolean"),
                Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),
                Native.M("UnityEngine.SpriteRenderer","get_color"),Native.M("UnityEngine.SpriteRenderer","set_color","UnityEngine.Color"),
                Native.M("UnityEngine.SpriteRenderer","get_sprite"),Native.M("UnityEngine.SpriteRenderer","set_sprite","UnityEngine.Sprite"),
                Native.M("UnityEngine.Renderer","get_bounds"),Native.M("UnityEngine.Renderer","get_sortingLayerID"),Native.M("UnityEngine.Renderer","set_sortingLayerID","System.Int32"),
                Native.M("UnityEngine.Renderer","get_sortingOrder"),Native.M("UnityEngine.Renderer","set_sortingOrder","System.Int32"),
                Native.M("UnityEngine.GameObject","get_layer"),Native.M("UnityEngine.GameObject","set_layer","System.Int32"),
                Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),
                Native.M("UnityEngine.Transform","InverseTransformPoint","UnityEngine.Vector3"),
                Native.M("UnityEngine.Transform","set_localPosition","UnityEngine.Vector3"),Native.M("UnityEngine.Transform","set_localRotation","UnityEngine.Quaternion"),
                Native.M("UnityEngine.Quaternion","get_identity"),Native.M("UnityEngine.Sprite","get_bounds"),
                Native.M("UnityEngine.Bounds","get_min"),Native.M("UnityEngine.Bounds","get_max"),Native.M("UnityEngine.Bounds","get_size"),
                Native.M("UnityEngine.Vector3","get_Item","System.Int32"),Native.M("UnityEngine.Color","get_Item","System.Int32")}){_=method;}
            _=Native.M("UISpriteDuplicator+SpriteDuplicationData","get_source");
            foreach(var method in new[]{Native.M("Unit","get_statusDisplay"),Native.M("UnitStatusDisplay","get_unit"),Native.M("UnitStatusDisplay","get_IsUsed"),
                Native.M("UnitStatusDisplay","get_typeContainer"),Native.M("UnitStatusDisplay","get_typeBg"),Native.M("UnitStatusDisplay","get_typeOutline"),
                Native.M("UnitStatusDisplay","get_typeIcon"),Native.M("UnitStatusDisplay","get_typeIconShadow"),Native.M("UnityEngine.Bounds","get_center"),
                Native.M("UnityEngine.Renderer","get_enabled"),Native.M("UnityEngine.Transform","get_localPosition"),
                Native.M("UnityEngine.Transform","TransformPoint","UnityEngine.Vector3")}){_=method;}
            _=Native.M("UnityEngine.Material","get_shader");
            _=Native.M("UnityEngine.Object","get_name");
            _=Native.M("PrefabManager","GetPrefab",NativeUnitType,"PolytopiaBackendBase.Common.TribeType","PolytopiaBackendBase.Common.SkinType");
            _=Native.T("UnityEngine.GameObject").GetConstructor(new[]{typeof(string)})??throw new MissingMethodException("GameObject(String)");
            foreach(var method in new[]{
                Native.M("Unit","UpdateObject"),Native.M("Unit","UpdateObject","MapRenderContext","SkinVisualsTransientData"),
                Native.M("Unit","SetVisible","System.Boolean"),Native.M("Unit","SetVisible","System.Boolean","MapRenderContext"),
                Native.M("Unit","SetAlpha","System.Single"),Native.M("Unit","SetShowOutline","System.Boolean"),
                Native.M("Unit","set_Depth","System.Int32"),Native.M("Unit","UpdateStatusDisplay","MapRenderContext")})
                Native.Hook(h,method,typeof(MimicMapInspection),nameof(BeforeUnit),null,nameof(UnitFinished));
            Native.Hook(h,Native.M("Unit","Update"),typeof(MimicMapInspection),nameof(BeforePulse),null,nameof(PulseFinished));
            foreach(string method in new[]{"Destroy","OnDestroy"})
                Native.Hook(h,Native.M("Unit",method),typeof(MimicMapInspection),nameof(UnitDestroying));
            Native.Hook(h,Native.M("PolytopiaSpriteRenderer","ReturnToPool"),typeof(MimicMapInspection),nameof(RendererReturning));
            Native.Hook(h,Native.M("UnitStatusDisplay","ReturnToPool"),typeof(MimicMapInspection),nameof(StatusReturning));
            Native.Hook(h,Native.M("UnitStatusDisplay","SetUnit","Unit"),typeof(MimicMapInspection),nameof(StatusReturning));
            Native.Hook(h,Native.M("UnitStatusDisplay","SetState","UnitState"),typeof(MimicMapInspection),nameof(StatusChanging),null,nameof(StatusFinished));
            Native.Hook(h,Native.M("UnitStatusDisplay","RefreshColor"),typeof(MimicMapInspection),nameof(StatusChanging),null,nameof(StatusFinished));
            Native.Hook(h,Native.M("UISpriteDuplicator","RenderSprites","UISpriteDuplicator+SpriteDuplicationData"),
                typeof(MimicMapInspection),nameof(BeforeDuplicationData),null,nameof(DuplicationFinished));
            Native.Hook(h,Native.M("UISpriteDuplicator","RenderSprites","UnityEngine.Transform","UIDuplicatedSprites","System.Single","System.String","UnityEngine.Vector2","System.Boolean"),
                typeof(MimicMapInspection),nameof(BeforeDuplicationSource),null,nameof(DuplicationFinished));
        }

        public static void BeforeDuplicationData(object __0,out object? __state)
        {
            __state=null;
            try{if(__0 is not null)__state=BeginDuplication(Native.Get(__0,"source"));}
            catch(Exception e){Note("mimic-map-duplicate-before-error",e);}
        }
        public static void BeforeDuplicationSource(object __0,out object? __state)
        {
            __state=null;
            try{__state=BeginDuplication(__0);}
            catch(Exception e){Note("mimic-map-duplicate-before-error",e);}
        }
        static object? BeginDuplication(object? source)
        {
            if(Applying||!Native.UnityAlive(source))return null;
            var scope=new DuplicationScope();
            foreach(var pair in Views.ToArray())
            {
                var view=pair.Value;
                if(view.PublicArt)continue; // Public Spy art must be copied into its ordinary native menu.
                try
                {
                    // Native UI copies the source hierarchy. Match only a tracked
                    // map source, never an unrelated popup/preview or map ancestor.
                    string match=Native.Same(source,view.Root)?"unit-root":Native.Same(source,view.Container)?"sprite-container":
                        Native.Same(source,view.BadgeParent)?"badge-container":
                        (Native.UnityAlive(view.Status)&&Native.Same(source,Native.Get(view.Status!,"transform")))?"status-root":
                        view.Parts.Any(p=>Native.Same(source,p.Transform))?"body-transform":"";
                    if(match.Length==0)continue;
                    Duplicating.TryGetValue(pair.Key,out int depth);Duplicating[pair.Key]=depth+1;
                    scope.Views.Add((pair.Key,view));
                    if(depth!=0)continue;
                    RestoreBlackmail(pair.Key);RestoreRogue(pair.Key);
                    RestoreParts(view);RestoreBadgePosition(view);RestoreBadgeColors(view);RestoreBadgeSprites(view);HideChild(view);HideBadge(view);
                    if(Native.UnityAlive(view.ChildTransform)&&Native.Same(Native.Get(view.ChildTransform!,"parent"),view.Container))
                        Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),view.ChildTransform,null,false);
                    if(Native.UnityAlive(view.BadgeTransform)&&Native.Same(Native.Get(view.BadgeTransform!,"parent"),view.BadgeParent))
                        Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),view.BadgeTransform,null,false);

                }
                catch(Exception e){Note("mimic-map-duplicate-detach-error",e);Remove(pair.Key);Duplicating.Remove(pair.Key);}
            }
            return scope.Views.Count==0?null:scope;
        }
        public static Exception? DuplicationFinished(Exception? __exception,object? __state)
        {
            if(__state is not DuplicationScope scope)return __exception;
            foreach(var entry in scope.Views)
            {
                if(!Duplicating.TryGetValue(entry.Key,out int depth))continue;
                if(depth>1){Duplicating[entry.Key]=depth-1;continue;}
                Duplicating.Remove(entry.Key);
                if(!Views.TryGetValue(entry.Key,out var current)||!ReferenceEquals(current,entry.View))continue;
                try
                {
                    if(!MimicPresentationState.TryProject(Native.Get(current.Unit,"UnitState")!,out var p)||
                        Resolve?.Invoke(current.Unit) is not bool mode||!Eligible(current.Unit,p,mode)||
                        !Native.Same(Native.Get(current.Unit,"transform"),current.Root)||
                        !Native.Same(Native.Get(current.Unit,"spriteContainer"),current.Container)||
                        Convert.ToUInt32(Native.Get(Native.Get(current.Unit,"UnitState")!,"id"))!=current.UnitId)
                    {Remove(entry.Key);continue;}
                    if(Native.UnityAlive(current.ChildTransform))
                    {
                        // A new parent indicates reassignment; do not move it.
                        if(Native.UnityAlive(Native.Get(current.ChildTransform!,"parent"))){Remove(entry.Key);continue;}
                        Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),current.ChildTransform,current.Container,false);
                    }
                    if(Native.UnityAlive(current.BadgeTransform))
                    {
                        if(!StatusOwned(current)||Native.UnityAlive(Native.Get(current.BadgeTransform!,"parent")))DestroyBadge(current);
                        else Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),current.BadgeTransform,current.BadgeParent,false);
                    }
                    if(!Updating.ContainsKey(entry.Key))Refresh(current.Unit);
                }
                catch(Exception e){Note("mimic-map-duplicate-resume-error",e);Remove(entry.Key);}
            }
            // Restore presentation even after a failed copy, while preserving
            // the native exception for its original caller.
            return __exception;
        }

        public static void BeforeUnit(object __instance)
        {
            if(Applying)return;
            long key=Native.Pointer(__instance);RestoreBlackmail(key);RestoreRogue(key);Updating.TryGetValue(key,out int depth);Updating[key]=depth+1;
            if(depth!=0||!Views.TryGetValue(key,out var view))return;
            try{RestoreParts(view);RestoreBadgePosition(view);RestoreBadgeColors(view);RestoreBadgeSprites(view);HideChild(view);HideBadge(view);}
            catch(Exception e){Note("mimic-map-before-error",e);Remove(key);}
        }
        public static Exception? UnitFinished(Exception? __exception,object __instance)
        {
            if(Applying)return __exception;
            long key=Native.Pointer(__instance);
            if(Updating.TryGetValue(key,out int depth)&&depth>1){Updating[key]=depth-1;return __exception;}
            Updating.Remove(key);
            if(__exception is not null){Remove(key);return __exception;}
            Refresh(__instance);return __exception;
        }
        // Native Unit.Update writes the Invisible pulse directly through
        // SkinVisualsRenderer.SetAlpha, bypassing Unit.SetAlpha/SetVisible.
        // Reuse the exact captured parts; no projection, collection, prefab or
        // ledger work belongs in this per-frame boundary. Ordinary units exit
        // at the dictionary lookup, and native actual views keep their pulse.
        public static void BeforePulse(object __instance,out object? __state)
        {
            __state=null;if(Applying)return;
            long key=Native.Pointer(__instance);
            if(Views.TryGetValue(key,out var view)&&view.NativeHidden&&!Updating.ContainsKey(key)&&!Duplicating.ContainsKey(key))
            {
                try
                {
                    if(InvisiblePulseEffect is not null&&(bool)Native.Invoke(Native.M("UnitState","HasEffect","UnitEffect"),Native.Get(__instance,"UnitState"),InvisiblePulseEffect)!)__state=view;
                }
                catch(Exception e){Note("mimic-map-before-pulse-error",e);}
            }
        }
        public static Exception? PulseFinished(Exception? __exception,object __instance,object? __state)
        {
            if(__state is not View view||Applying)return __exception;
            long key=Native.Pointer(__instance);
            if(!Views.TryGetValue(key,out var current)||!ReferenceEquals(current,view))return __exception;
            try
            {
                if(__exception is not null||!view.NativeHidden||!Native.UnityAlive(view.Unit)||
                    !Native.Same(view.PulseState,Native.Static("GameManager","GameState"))||
                    !Native.Same(view.PulseMap,Native.Static("MapRenderer","Current"))||
                    !Native.Same(view.PulseClient,Native.Static("GameManager","Client"))||
                    !Native.Same(view.PulseViewer,Native.Static("GameManager","LocalPlayer"))||
                    Convert.ToByte(Native.Get(view.PulseViewer!,"Id"))!=view.PulseViewerId||
                    Convert.ToByte(Native.Get(Native.Get(view.Unit,"UnitState")!,"owner"))!=view.PulseOwner||
                    (bool)Native.Get(view.PulseClient!,"IsReplay")!||(bool)Native.Get(view.PulseClient!,"IsSpectating")!||
                    !(bool)Native.Invoke(Native.M("Unit","GetVisible"),view.Unit)!||
                    (bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),view.Unit)!||
                    !Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),view.PulseMap,view.UnitId),view.Unit))
                {Remove(key);return __exception;}
                // Capture all newly written native colors before any PSR write
                // can also alter its underlying Unity renderer. RestoreParts
                // later restores this latest pulse, never an old pre-pulse alpha.
                foreach(var part in view.Parts)
                {
                    if(!PartOwned(view,part)){Remove(key);return __exception;}
                    var color=ReadColor(part);part.PulseChanged=color!=part.Applied;
                    if(part.PulseChanged){part.Original=color;part.Applied=color.Alpha(0);}
                }
                foreach(var part in view.Parts)if(part.PulseChanged)WriteColor(part,part.Applied);
            }
            catch(Exception e){Note("mimic-map-pulse-error",e);Remove(key);}
            return __exception;
        }
        public static void UnitDestroying(object __instance)
        {long key=Native.Pointer(__instance);RemoveBlackmail(key);RemoveRogue(key);Remove(key);Updating.Remove(key);Duplicating.Remove(key);}
        public static void RendererReturning(object __instance)
        {
            if(Applying)return;
            foreach(var pair in BlackmailViews.ToArray())if(pair.Value.Frame.Parts.Any(p=>Native.Same(p.Renderer,__instance)))RemoveBlackmail(pair.Key);
            foreach(var pair in RogueViews.ToArray())if(pair.Value.Frame.Parts.Any(p=>Native.Same(p.Renderer,__instance)))RemoveRogue(pair.Key);
            // Native pooling does not reset color. Restore before the exact old
            // renderer can be detached or assigned to another unit.
            foreach(var pair in Views.ToArray())
                if(pair.Value.Parts.Any(p=>Native.Same(p.Renderer,__instance)))Remove(pair.Key);
        }
        public static void StatusReturning(object __instance)
        {
            if(Applying)return;
            foreach(var pair in BlackmailViews.ToArray())if(Native.Same(Native.Get(pair.Value.Frame.Unit,"statusDisplay"),__instance))RemoveBlackmail(pair.Key);
            foreach(var pair in Views.ToArray())if(Native.Same(pair.Value.Status,__instance))Remove(pair.Key);
        }
        public static void StatusChanging(object __instance,out object? __state)
        {
            __state=null;if(Applying)return;
            var view=Views.Values.FirstOrDefault(v=>Native.Same(v.Status,__instance));
            if(view is null)return;
            __state=view.Unit;BeforeUnit(view.Unit);
        }
        public static Exception? StatusFinished(Exception? __exception,object? __state)=>
            __state is null?__exception:UnitFinished(__exception,__state);
        public static void Tick()
        {
            if(Applying)return;
            try
            {
                var map=Native.Static("MapRenderer","Current");
                if(Resolve is null||!Native.UnityAlive(map)){Clear();return;}
                var units=Native.Get(map!,"RenderedUnits");
                if(units is null){Clear();return;}
                var seen=new HashSet<long>();
                foreach(var unit in Native.Items(units).ToArray())
                {long key=Native.Pointer(unit);seen.Add(key);if(!Updating.ContainsKey(key)&&!Duplicating.ContainsKey(key))Refresh(unit);}
                foreach(long key in Views.Keys.Where(k=>!seen.Contains(k)).ToArray())Remove(key);
                foreach(long key in BlackmailViews.Keys.Where(k=>!seen.Contains(k)).ToArray())RemoveBlackmail(key);
                foreach(long key in RogueViews.Keys.Where(k=>!seen.Contains(k)).ToArray())RemoveRogue(key);
            }
            catch(Exception e){Note("mimic-map-tick-error",e);Clear();}
        }
        public static void Refresh(object unit)
        {
            if(Applying||Resolve is null)return;
            long key=Native.Pointer(unit);
            if(Duplicating.ContainsKey(key))return;
            RestoreBlackmail(key);RestoreRogue(key);
            try
            {
                bool? actual=Resolve(unit);
                MimicProjection projection=null!;
                bool projected=actual is not null&&MimicPresentationState.TryProject(Native.Get(unit,"UnitState")!,out projection);
                bool publicArt=!projected&&NativeSpyPresentation.TryProject(unit,out projection);
                if(projected&&projection.Exposed)publicArt=true;
                if(publicArt)actual=true;
                if((!projected&&!publicArt)||actual is null||!Eligible(unit,projection,actual.Value,publicArt)){Remove(key);return;}
                uint unitId=Convert.ToUInt32(Native.Get(Native.Get(unit,"UnitState")!,"id"));
                var container=Native.Get(unit,"spriteContainer");
                if(!Native.UnityAlive(container)){Remove(key);return;}
                if(Views.TryGetValue(key,out var old)&&(old.UnitId!=unitId||!Native.Same(old.Container,container)||old.PublicArt!=publicArt))Remove(key);
                if(!Views.TryGetValue(key,out var view))
                    Views[key]=view=new View{Unit=unit,UnitId=unitId,Container=container!,Root=Native.Get(unit,"transform")!,PublicArt=publicArt};
                Applying=true;
                try
                {
                    RestoreParts(view);RestoreBadgePosition(view);RestoreBadgeColors(view);RestoreBadgeSprites(view);
                    // A mode change can happen without another native unit
                    // update. Refresh only this exact live status widget so its
                    // HP follows the selected view immediately, never per frame.
                    string healthKey=actual+":"+projection.ActualHealth+":"+projection.ActualMaxHealth+":"+projection.ApparentHealth+":"+projection.ApparentMaxHealth;
                    if((!publicArt||projection.Exposed)&&view.HealthKey!=healthKey){MimicPresentationUi.RefreshMapHealth(view.Unit);view.HealthKey=healthKey;}
                    string effectKey=actual+":"+string.Join(",",actual.Value?Native.Items(Native.Get(Native.Get(unit,"UnitState")!,"effects")!).Select(x=>Native.Int(x).ToString()):projection.Appearance?.Effects??Array.Empty<string>());
                    if(projected&&view.EffectKey!=effectKey){MimicPresentationUi.RefreshMapEffects(view.Unit);view.EffectKey=effectKey;}
                    var parts=Collect(view);
                    if(parts.Count==0){DestroyChild(view);DestroyBadge(view);return;}
                    // Capture every value before PSR writes can also affect its
                    // underlying Unity renderer. Apparent applies no tint.
                    bool spy=NativeFoggerPresentation.CustomActual(projection);
                    bool apparentCustom=!actual.Value&&(NativeFoggerPresentation.CustomForm(projection.NativeId)||projection.ApparentMimic);
                    bool overlay=spy||apparentCustom||(projection.ActualIsMimic?actual.Value:!actual.Value);
                    foreach(var part in parts){part.Original=ReadColor(part);part.Applied=overlay?part.Original.Alpha(0):part.Original;}
                    if(apparentCustom)
                    {
                        string visualKey=VisualProfileKey(projection);
                        if(view.FailedVisualKey!=visualKey)
                        {
                            try{ShowActual(view,parts,true,projection,appearanceArtwork:true);}
                            catch(Exception e){view.FailedVisualKey=visualKey;DestroyChild(view);Note("spy-disguise-visual-error",e);}
                        }
                    }
                    else if(projection.ActualIsMimic||(spy&&actual.Value))ShowActual(view,parts,actual.Value,projection);
                    else ShowGenuineDisguise(view,parts,!actual.Value,projection);
                    if(publicArt||IsOwner(projection))ShowBadge(view,actual.Value,projection,publicArt);
                    else if(!actual.Value&&projection.ApparentMimic)ShowBadge(view,true,projection with{ActualIsMimic=true},true);
                    else if(!actual.Value&&NativeFoggerPresentation.CustomBadge(projection))ShowBadge(view,true,projection with{ActualIsMimic=false,ActualNativeId=NativeFoggerPresentation.ApparentBadgeForm(projection),ActualBadgeNativeId=NativeFoggerPresentation.ApparentBadgeForm(projection)},true);
                    else DestroyBadge(view);
                    view.Parts.AddRange(parts);
                    view.NativeHidden=overlay;view.PulseState=projection.State;
                    view.PulseMap=Native.Static("MapRenderer","Current");view.PulseClient=Native.Static("GameManager","Client");
                    view.PulseViewer=Native.Static("GameManager","LocalPlayer");view.PulseViewerId=Convert.ToByte(Native.Get(view.PulseViewer!,"Id"));
                    view.PulseOwner=Convert.ToByte(Native.Get(Native.Get(unit,"UnitState")!,"owner"));
                    if(overlay)
                    {
                        foreach(var part in parts)WriteColor(part,part.Applied);
                        foreach(var part in parts)if(ReadColor(part)!=part.Applied)throw new InvalidOperationException("Native map renderer rejected private body hiding.");
                    }
                    view.AppliedMode=actual;
                }
                finally{Applying=false;}
            }
            catch(Exception e){Applying=false;Note("mimic-map-project-error",e);Remove(key);}
            finally{RefreshRogue(unit);RefreshBlackmail(unit);}
        }
        static bool IsOwner(MimicProjection p)=>MimicPresentationUi.HasTruthAccess(p);
        static bool Eligible(object unit,MimicProjection p,bool actual,bool publicArt=false)
        {
            if(!Native.UnityAlive(unit))return false;
            var map=Native.Static("MapRenderer","Current");
            if(!Native.UnityAlive(map)||!Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,p.UnitId),unit))return false;
            var player=Native.Static("GameManager","LocalPlayer");var client=Native.Static("GameManager","Client");
            bool privateAllowed=IsOwner(p);
            bool publicAllowed=(publicArt&&NativeSpyPresentation.Visible(unit,p))||(!actual&&(!p.ActualIsMimic||NativeFoggerPresentation.CustomBadge(p))&&MimicPresentationUi.PublicMapContext(p));
            return player is not null&&client is not null&&(privateAllowed||publicAllowed)&&
                Native.Same(Native.Static("GameManager","GameState"),p.State)&&
                !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!&&
                (bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!&&
                !(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!;
        }
        static List<Part> Collect(View view,object? skinOverride=null)
        {
            var parts=new Dictionary<long,Part>();
            void Add(object? renderer,bool poly,bool main)
            {
                if(!Native.UnityAlive(renderer))return;
                var transform=Native.Get(renderer!,"transform")!;
                if(!(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),transform,view.Root)!)return;
                var sprite=Native.Get(renderer!,poly?"Sprite":"sprite");if(!Native.UnityAlive(sprite))return;
                long key=Native.Pointer(renderer!);
                if(parts.TryGetValue(key,out var existing)){existing.Main|=main;return;}
                parts.Add(key,new Part{Renderer=renderer!,Transform=transform,Sprite=sprite!,Poly=poly,Main=main});
                // SetAlpha writes this underlying Unity renderer directly, and
                // PSR.Color can therefore hold a different alpha. Preserve both.
                if(poly)Add(Native.Get(renderer!,"spriteRenderer"),false,main);
            }
            void Union(object? union,bool main)
            {
                if(union is null)return;
                Add(Native.Get(union,"polytopiaSpriteRenderer"),true,main);
                Add(Native.Get(union,"spriteRenderer"),false,main);
            }
            var skin=skinOverride??Native.Get(view.Unit,"skinVisuals");var visualParts=skin is null?null:Native.Get(skin,"visualParts");
            if(visualParts is not null)
                foreach(var part in Native.Items(visualParts))
                {Union(Native.Get(part,"renderer"),true);Union(Native.Get(part,"outlineRenderer"),false);}
            // Older/compact visuals can use the inherited primary renderer.
            Add(Native.Get(view.Unit,"SpriteRenderer"),true,parts.Count==0);
            return parts.Values.OrderBy(p=>p.Poly?0:1).ToList();
        }
        static bool PartOwned(View view,Part part)=>Native.UnityAlive(part.Renderer)&&Native.UnityAlive(part.Transform)&&
            Native.UnityAlive(view.Unit)&&Convert.ToUInt32(Native.Get(Native.Get(view.Unit,"UnitState")!,"id"))==view.UnitId&&
            Native.Same(Native.Get(view.Unit,"transform"),view.Root)&&Native.Same(Native.Get(view.Unit,"spriteContainer"),view.Container)&&
            Native.Same(Native.Get(part.Renderer,part.Poly?"Sprite":"sprite"),part.Sprite)&&
            (bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),part.Transform,view.Root)!;
        static void RestoreParts(View view)
        {
            if(view.Parts.Count==0)return;
            if(!view.Parts.Any(p=>p.Original!=p.Applied)){view.Parts.Clear();return;}
            var writes=new List<(Part Part,Rgba Color)>();
            // Decide all writes first. Restoring PSR.Color also writes its Unity
            // renderer, so preserve any newer native underlying alpha afterward.
            foreach(var part in view.Parts)
            {
                if(!PartOwned(view,part))continue;
                var current=ReadColor(part);
                if(current==part.Applied&&part.Original!=part.Applied)writes.Add((part,part.Original));
                else if(!part.Poly)writes.Add((part,current));
            }
            view.Parts.Clear();
            foreach(var entry in writes)WriteColor(entry.Part,entry.Color);
        }
        static Rgba ReadColor(Part part)
        {
            var color=Native.Get(part.Renderer,part.Poly?"Color":"color")!;
            float Axis(int n)=>Finite(Native.Invoke(Native.M("UnityEngine.Color","get_Item","System.Int32"),color,n));
            return new Rgba(Axis(0),Axis(1),Axis(2),Axis(3));
        }
        static void WriteColor(Part part,Rgba color)
        {
            if(part.Poly)
            {
                bool locked=(bool)Native.Get(part.Renderer,"isColorLocked")!;
                if(locked)Native.Invoke(Native.M("PolytopiaSpriteRenderer","SetUnchangeableColor","UnityEngine.Color"),part.Renderer,color.Box());
                else Native.Set(part.Renderer,"Color",color.Box());
                Native.Invoke(Native.M("PolytopiaSpriteRenderer","SetDirty","System.Boolean"),part.Renderer,true);
            }
            else Native.Set(part.Renderer,"color",color.Box());
        }
        static string VisualProfileKey(MimicProjection p)=>p.NativeId+":"+p.Appearance?.TribeVisual+":"+p.Appearance?.BirthVisual+
            ":"+p.Appearance?.SourceColor+":"+p.ApparentMimicBoat+":"+p.Appearance?.Passenger?.TribeVisual+":"+p.Appearance?.Passenger?.SourceColor+":"+Native.Static("ChipMunkManager","active");
        static void ShowGenuineDisguise(View view,List<Part> parts,bool apparent,MimicProjection p)
        {
            string key=VisualProfileKey(p);
            if(apparent&&view.FailedVisualKey==key){HideChild(view);return;}
            try{DrawGenuineDisguise(view,parts,apparent,p);}
            catch(Exception e)
            {
                // Retain native hiding for a failed apparent view. A rendering
                // failure must not silently disclose a genuine unit's true form.
                // Retry only after its immutable copied visual profile changes.
                view.FailedVisualKey=key;DestroyChild(view);Note("genuine-disguise-visual-error",e);
            }
        }
        static void DrawGenuineDisguise(View view,List<Part> parts,bool apparent,MimicProjection p)
        {
            if(!apparent){HideChild(view);return;}
            if(view.ArtIsActual)DestroyChild(view);
            var appearance=p.Appearance??throw new InvalidOperationException("Genuine disguise appearance is missing.");
            string key=VisualProfileKey(p);
            if(view.VisualKey!=key)
            {
                DestroyChild(view);view.NativeVisual=null;
                if(view.FailedVisualKey==key)return;
                view.FailedVisualKey=key; // One allocation attempt per immutable visual profile.
                try
                {
                    view.NativeVisual=NativeVisualSnapshot.Capture(p.NativeId,appearance);
                    view.VisualKey=key;view.FailedVisualKey="";
                }
                catch(Exception e){Note("genuine-disguise-visual-error",e);return;}
            }
            var snapshot=view.NativeVisual!;
            var main=parts.Where(x=>x.Main&&!x.Poly&&(bool)Native.Get(Native.Get(x.Renderer,"gameObject")!,"activeInHierarchy")!).FirstOrDefault()
                ??parts.FirstOrDefault(x=>x.Main)??throw new InvalidOperationException("Genuine unit has no native visual part.");
            var outline=parts.FirstOrDefault(x=>!x.Main&&!x.Poly&&x.Original.A>0&&
                (bool)Native.Get(x.Renderer,"enabled")!&&(bool)Native.Get(Native.Get(x.Renderer,"gameObject")!,"activeInHierarchy")!);
            if(!Native.UnityAlive(view.Child)||view.ArtRenderers.Count!=snapshot.Count||view.ArtRenderers.Any(r=>!Native.UnityAlive(r)))
            {
                DestroyChild(view);
                view.Child=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),"Deception apparent native form")!;
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),view.Child,false);
                view.ChildTransform=Native.Get(view.Child,"transform")!;
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),view.ChildTransform,view.Container,false);
                var rendererType=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"));
                foreach(var unused in snapshot)
                {
                    var go=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),"Deception apparent native part")!;
                    Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),Native.Get(go,"transform"),view.ChildTransform,false);
                    view.ArtRenderers.Add(Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),go,rendererType)!,"UnityEngine.SpriteRenderer"));
                }
            }
            if(!Native.Same(Native.Get(view.ChildTransform!,"parent"),view.Container))throw new InvalidOperationException("Apparent child changed owner.");
            var owner=Native.Get(view.Unit,"Owner")!;
            var tribe=Native.Invoke(Native.M("PlayerExtensions","GetVisualTribeType","PlayerState"),null,owner)!;
            var skin=Native.Invoke(Native.M("PlayerState","GetVisualSkin"),owner)!;
            var carrier=ReadPrefabLayout(ArtCarrier(p.ActualNativeId),tribe,skin);
            Native.Set(view.ChildTransform!,"localPosition",new Point(-carrier.ContainerOrigin.X/carrier.ContainerScale.X,
                -carrier.ContainerOrigin.Y/carrier.ContainerScale.Y,-carrier.ContainerOrigin.Z/carrier.ContainerScale.Z).Box());
            Native.Set(view.ChildTransform!,"localScale",new Point(1/carrier.ContainerScale.X,1/carrier.ContainerScale.Y,1/carrier.ContainerScale.Z).Box());
            Native.Set(view.ChildTransform!,"localRotation",Native.Static("UnityEngine.Quaternion","identity"));
            int layer=Native.Int(Native.Get(main.Renderer,main.Poly?"SortingLayer":"sortingLayerID"));
            int order=parts.Max(x=>Native.Int(Native.Get(x.Renderer,x.Poly?"SortingOrder":"sortingOrder")))+1;
            for(int i=0;i<snapshot.Count;i++)
            {
                var value=snapshot[i];var renderer=view.ArtRenderers[i];var go=Native.Get(renderer,"gameObject")!;
                var transform=Native.Get(renderer,"transform")!;
                if(!Native.Same(Native.Get(transform,"parent"),view.ChildTransform))throw new InvalidOperationException("Apparent native part changed parent.");
                Native.Set(go,"layer",Native.Get(Native.Get(main.Renderer,"gameObject")!,"layer"));
                Native.Set(renderer,"sprite",value.Sprite);
                ApplyNativeAppearance(view,parts,renderer,value.Tintable);
                if(Native.UnityAlive(value.Material))Native.Set(renderer,"sharedMaterial",value.Material);
                float Channel(int n)=>Finite(Native.Invoke(Native.M("UnityEngine.Color","get_Item","System.Int32"),value.Color,n));
                Native.Set(renderer,"color",value.Outline&&outline is not null?outline.Original.Box():
                    new Rgba(Channel(0),Channel(1),Channel(2),main.Original.A).Box());
                Native.Set(renderer,"sortingLayerID",layer);Native.Set(renderer,"sortingOrder",checked(order+(value.Outline?-snapshot.Count:0)+i));
                Native.Set(transform,"localPosition",new Point(value.X,value.Y,value.Z).Box());
                Native.Set(transform,"localScale",new Point(value.ScaleX,value.ScaleY,value.ScaleZ).Box());
                Native.Set(transform,"localRotation",Native.Invoke(Native.M("UnityEngine.Quaternion","Euler","System.Single","System.Single","System.Single"),null,0f,0f,value.Rotation));
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,!value.Outline||outline is not null);
            }
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),view.Child!,true);
        }
        static void ShowActual(View view,List<Part> parts,bool actual,MimicProjection projection,bool appearanceArtwork=false)
        {
            if(!actual){DestroyChild(view);return;}
            if(Native.UnityAlive(view.Child)&&!view.ArtIsActual)DestroyChild(view);
            view.ArtIsActual=true;
            var candidates=parts.Where(p=>p.Main&&(bool)Native.Get(Native.Get(p.Renderer,"gameObject")!,"activeInHierarchy")!).ToArray();
            if(candidates.Length==0)throw new InvalidOperationException("Visible native map body has no owned renderer.");
            float minX=float.PositiveInfinity,minY=float.PositiveInfinity,minZ=float.PositiveInfinity;
            float maxX=float.NegativeInfinity,maxY=float.NegativeInfinity,maxZ=float.NegativeInfinity;
            Part? representative=null;float largest=-1;
            foreach(var part in candidates)
            {
                // Where both exist, the Unity renderer contains native SetAlpha's
                // latest value. Do not derive opacity from PSR's stale backing.
                if(part.Poly&&Native.UnityAlive(Native.Get(part.Renderer,"spriteRenderer")))
                {
                    var underlyingBounds=Native.Get(Native.Get(part.Renderer,"spriteRenderer")!,"bounds")!;
                    var size=PointOf(Native.Get(underlyingBounds,"size")!);
                    if(size.X>0&&size.Y>0)continue;
                }
                var bounds=Native.Get(part.Renderer,part.Poly?"Bounds":"bounds")!;
                var low=PointOf(Native.Get(bounds,"min")!);var high=PointOf(Native.Get(bounds,"max")!);
                float area=(high.X-low.X)*(high.Y-low.Y);if(area<=0)continue;
                minX=Math.Min(minX,low.X);minY=Math.Min(minY,low.Y);minZ=Math.Min(minZ,low.Z);
                maxX=Math.Max(maxX,high.X);maxY=Math.Max(maxY,high.Y);maxZ=Math.Max(maxZ,high.Z);
                if(area>largest){representative=part;largest=area;}
            }
            if(representative is null||largest<=0)throw new InvalidOperationException("Native map body bounds are empty.");
            var owner=Native.Get(view.Unit,"Owner")??throw new InvalidOperationException("Actual Mimic owner is missing.");
            owner=NativeRogueVisuals.ArtOwner(projection.State,Native.Get(view.Unit,"UnitState")!,owner);
            var tribe=Native.Invoke(Native.M("PlayerExtensions","GetVisualTribeType","PlayerState"),null,owner)!;
            var skin=Native.Invoke(Native.M("PlayerState","GetVisualSkin"),owner)!;
            string id=Native.EnumName(Native.Get(Native.Get(view.Unit,"UnitState")!,"type")!);
            bool spy=appearanceArtwork?Deception.Core.SpyCatalog.IsSpyId(projection.NativeId):NativeSpyPresentation.IsSpy(projection);
            bool fogger=appearanceArtwork?NativeFoggerPresentation.IsForm(projection.NativeId):NativeFoggerPresentation.IsFogger(projection);
            bool aquarion=appearanceArtwork?(projection.NativeId==Deception.Core.AquarionCatalog.MimicId||projection.NativeId==Deception.Core.SpyCatalog.AquarionId||projection.Appearance?.Passenger?.Form==Deception.Core.AquarionCatalog.MimicId):
                projection.ActualAmphibiousMimic||projection.ActualNativeId==Deception.Core.SpyCatalog.AquarionId;
            bool boat=!aquarion&&(appearanceArtwork?projection.ApparentMimicBoat:projection.ActualMimicBoat);
            RecruitmentHooks.Forms.TryGetValue(id,out var form);
            if(form is null&&projection.ActualIsMimic&&!spy)throw new InvalidOperationException("Actual Mimic carrier is not registered.");
            var reference=ReadPrefabLayout(boat?"transportship":"warrior",tribe,skin);
            string carrierId=form?.NativeId??projection.ActualNativeId;
            carrierId=ArtCarrier(carrierId);
            var carrier=ReadPrefabLayout(carrierId,tribe,skin);
            if(appearanceArtwork)
            {
                string playerKey=VisualProfileKey(projection)+":"+Native.Pointer(owner);
                if(view.AppearancePlayerKey!=playerKey||view.AppearancePlayer is null)
                {view.AppearancePlayer=NativeSpyPresentation.AppearancePlayer(owner,projection.Appearance!);view.AppearancePlayerKey=playerKey;}
                owner=view.AppearancePlayer;
            }
            
            var layers=fogger?FoggerArtAssets.Layers(owner):boat?UnitArtAssets.BoatLayers(owner):UnitArtAssets.Layers(owner,spy,aquarion);
            var dimensions=new Point((boat?UnitArtAssets.BoatVisibleWidth:UnitArtAssets.CanvasWidth)/100f,
                (boat?UnitArtAssets.BoatVisibleHeight:UnitArtAssets.CanvasHeight)/100f,0);
            float width=reference.High.X-reference.Low.X,height=reference.High.Y-reference.Low.Y;
            float scale=Math.Min(width/dimensions.X,height/dimensions.Y)*0.95f*(boat?UnitArtAssets.BoatMapScale:1f);
            if(!float.IsFinite(scale)||scale<=0)throw new InvalidOperationException("Private map sprite fit is invalid.");
            if(!Native.UnityAlive(view.Child)||view.ArtRenderers.Count!=layers.Count||view.ArtRenderers.Any(r=>!Native.UnityAlive(r)))
            {
                DestroyChild(view);
                var child=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),fogger?"Deception Motherfogger":spy?"Deception Spy":"Deception private Mimic")!;
                view.ArtIsActual=true;
                view.Child=child;Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),child,false);
                view.ChildTransform=Native.Get(child,"transform")!;
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),view.ChildTransform,view.Container,false);
                var type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"));
                for(int i=0;i<layers.Count;i++)
                {
                    var piece=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),"Deception art part "+i)!;
                    var transform=Native.Get(piece,"transform")!;
                    Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),transform,view.ChildTransform,false);
                    var renderer=Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),piece,type)!,"UnityEngine.SpriteRenderer");
                    view.ArtRenderers.Add(renderer);
                }
                view.ChildRenderer=view.ArtRenderers[0];
            }
            if(!Native.Same(Native.Get(view.ChildTransform!,"parent"),view.Container))throw new InvalidOperationException("Private map child changed parent.");
            Native.Set(view.Child!,"layer",Native.Get(Native.Get(representative.Renderer,"gameObject")!,"layer"));
            int layer=Native.Int(Native.Get(representative.Renderer,representative.Poly?"SortingLayer":"sortingLayerID"));
            var orders=parts.Where(p=>Native.Int(Native.Get(p.Renderer,p.Poly?"SortingLayer":"sortingLayerID"))==layer)
                .Select(p=>Native.Int(Native.Get(p.Renderer,p.Poly?"SortingOrder":"sortingOrder"))).ToArray();
            int order=checked(orders.Max()+1);
            float alpha=representative.Original.A;
            if(representative.Poly)
            {
                var underlying=Native.Get(representative.Renderer,"spriteRenderer");
                var nativePart=parts.FirstOrDefault(p=>!p.Poly&&Native.Same(p.Renderer,underlying));
                if(nativePart is not null)alpha=nativePart.Original.A;
            }
            for(int i=0;i<layers.Count;i++)
            {
                var art=layers[i];var renderer=view.ArtRenderers[i];var transform=Native.Get(renderer,"transform")!;
                if(!Native.Same(Native.Get(transform,"parent"),view.ChildTransform))throw new InvalidOperationException("Private art layer changed parent.");
                Native.Set(Native.Get(renderer,"gameObject")!,"layer",Native.Get(view.Child!,"layer"));
                Native.Set(renderer,"sprite",art.Sprite);
                ApplyNativeAppearance(view,parts,renderer,art.Tintable);
                Native.Set(renderer,"sortingLayerID",layer);Native.Set(renderer,"sortingOrder",checked(order+i));
                float Channel(int n)=>Finite(Native.Invoke(Native.M("UnityEngine.Color","get_Item","System.Int32"),art.Color,n));
                Native.Set(renderer,"color",new Rgba(Channel(0),Channel(1),Channel(2),alpha*Channel(3)).Box());
                var frame=UnitArtAssets.Frame(art);
                if(frame.Width<=0||frame.Height<=0)throw new InvalidOperationException("Private art layer bounds are empty.");
                float sx=art.Width/100f/frame.Width*(art.MirrorX?-1f:1f),sy=art.Height/100f/frame.Height;
                Native.Set(transform,"localPosition",new Point(art.X/100f-frame.X*sx,art.Y/100f-frame.Y*sy,0).Box());
                Native.Set(transform,"localRotation",Native.Static("UnityEngine.Quaternion","identity"));
                Native.Set(transform,"localScale",new Point(sx,sy,1).Box());
            }
            ShowOutlines(view,parts,layers,layer,order);
            Native.Set(view.ChildTransform!,"localRotation",Native.Static("UnityEngine.Quaternion","identity"));
            // The actual body always uses the native Warrior frame. Egg/Dragon
            // containers have a fixed +0.2 Y offset; cancel that PREFAB offset,
            // never the live transform, so native movement/bob/flip still apply.
            // Both boat canvases share a mirrored transform. Center their
            // measured visible silhouette rather than its padded PNG canvas;
            // keep a small up/right correction in the native vessel frame.
            var origin=new Point((reference.Low.X+reference.High.X)/2+
                (boat?UnitArtAssets.BoatVisibleCenterX/100f*scale+width*0.04f:0),
                reference.Low.Y-(boat?UnitArtAssets.BoatFootBaseline:UnitArtAssets.FootBaseline)/100f*scale+
                (boat?height*0.06f:0),
                (reference.Low.Z+reference.High.Z)/2);
            Native.Set(view.ChildTransform!,"localPosition",new Point(
                (origin.X-carrier.ContainerOrigin.X)/carrier.ContainerScale.X,
                (origin.Y-carrier.ContainerOrigin.Y)/carrier.ContainerScale.Y,
                (origin.Z-carrier.ContainerOrigin.Z)/carrier.ContainerScale.Z).Box());
            Native.Set(view.ChildTransform!,"localScale",new Point(scale/carrier.ContainerScale.X,scale/carrier.ContainerScale.Y,scale/carrier.ContainerScale.Z).Box());
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),view.Child!,true);
        }
        static string ArtCarrier(string id)=>id==Deception.Core.AquarionCatalog.MimicId||id==Deception.Core.SpyCatalog.AquarionId?"mermaidwarrior":
            NativeFoggerPresentation.CustomForm(id)||Deception.Core.RecruitmentCatalog.IsMenuId(id)?"warrior":id;
        static PrefabLayout ReadPrefabLayout(string form,object tribe,object skin)
        {
            var key=(form,Native.Int(tribe),Native.Int(skin));
            if(PrefabLayouts.TryGetValue(key,out var cached)&&Native.UnityAlive(cached.Prefab))return cached;
            PrefabLayouts.Remove(key); // A destroyed asset may reuse its profile slot.
            if(PrefabLayouts.Count>=1024)throw new InvalidOperationException("Native map reference profile limit exceeded.");
            var prefab=Native.Invoke(Native.M("PrefabManager","GetPrefab",NativeUnitType,
                "PolytopiaBackendBase.Common.TribeType","PolytopiaBackendBase.Common.SkinType"),null,
                Native.EnumValue(NativeUnitType,form),tribe,skin)!;
            if(!Native.UnityAlive(prefab))throw new InvalidOperationException("Native map reference prefab is missing: "+form);
            var root=Native.Get(prefab,"transform")!;var container=Native.Get(prefab,"spriteContainer")!;
            if(!Native.UnityAlive(root)||!Native.UnityAlive(container)||
                !(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),container,root)!)
                throw new InvalidOperationException("Native map reference container is not owned.");
            Point InRoot(object transform,Point p)=>PointOf(Native.Invoke(Native.M("UnityEngine.Transform","InverseTransformPoint","UnityEngine.Vector3"),root,
                Native.Invoke(Native.M("UnityEngine.Transform","TransformPoint","UnityEngine.Vector3"),transform,p.Box()))!);
            var origin=InRoot(container,new Point(0,0,0));
            var x=InRoot(container,new Point(1,0,0));var y=InRoot(container,new Point(0,1,0));var z=InRoot(container,new Point(0,0,1));
            var axes=new Point(x.X-origin.X,y.Y-origin.Y,z.Z-origin.Z);
            if(axes.X<=0||axes.Y<=0||axes.Z<=0||Math.Abs(x.Y-origin.Y)+Math.Abs(x.Z-origin.Z)+
                Math.Abs(y.X-origin.X)+Math.Abs(y.Z-origin.Z)+Math.Abs(z.X-origin.X)+Math.Abs(z.Y-origin.Y)>0.0001f)
                throw new InvalidOperationException("Native map reference container axes are unsupported.");
            var low=new Point(0,0,0);var high=low;
            if(form is "warrior" or "transportship")
            {
                // Unit.CreateUnit initializes its cached skinVisuals property.
                // A borrowed prefab has not run that path: read the serialized
                // component directly without assigning to the native asset.
                var skinType=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("SkinVisualsReference"));
                var skinComponent=Native.Invoke(Native.M("UnityEngine.GameObject","GetComponent","Il2CppSystem.Type"),Native.Get(prefab,"gameObject"),skinType);
                if(!Native.UnityAlive(skinComponent))throw new InvalidOperationException("Native Warrior skin component is missing.");
                skinComponent=Native.As(skinComponent!,"SkinVisualsReference");
                float minX=float.PositiveInfinity,minY=float.PositiveInfinity,minZ=float.PositiveInfinity;
                float maxX=float.NegativeInfinity,maxY=float.NegativeInfinity,maxZ=float.NegativeInfinity;
                // Prefab Renderer.bounds can be stale/uninitialized. Sprite.bounds
                // plus serialized part transforms are available without cloning.
                foreach(var part in Collect(new View{Unit=prefab,Root=root,Container=container},skinComponent).Where(p=>p.Main))
                {
                    var bounds=Native.Get(part.Sprite,"bounds")!;var a=PointOf(Native.Get(bounds,"min")!);var b=PointOf(Native.Get(bounds,"max")!);
                    if(b.X<=a.X||b.Y<=a.Y)continue;
                    for(int i=0;i<8;i++)
                    {
                        var p=InRoot(part.Transform,new Point((i&1)==0?a.X:b.X,(i&2)==0?a.Y:b.Y,(i&4)==0?a.Z:b.Z));
                        minX=Math.Min(minX,p.X);minY=Math.Min(minY,p.Y);minZ=Math.Min(minZ,p.Z);
                        maxX=Math.Max(maxX,p.X);maxY=Math.Max(maxY,p.Y);maxZ=Math.Max(maxZ,p.Z);
                    }
                }
                low=new Point(minX,minY,minZ);high=new Point(maxX,maxY,maxZ);
                if(!float.IsFinite(maxX-minX)||!float.IsFinite(maxY-minY)||maxX<=minX||maxY<=minY)
                    throw new InvalidOperationException("Native Warrior reference sprites are empty.");
            }
            var result=new PrefabLayout(prefab,origin,axes,low,high);PrefabLayouts[key]=result;return result;
        }
        static void ShowOutlines(View view,List<Part> parts,IReadOnlyList<UnitArtAssets.Layer> layers,int layer,int order)
        {
            bool Enabled(Part p)
            {
                if(!Native.UnityAlive(p.Renderer)||p.Original.A<=0||!(bool)Native.Get(Native.Get(p.Renderer,"gameObject")!,"activeInHierarchy")!)return false;
                var plain=p.Poly?Native.Get(p.Renderer,"spriteRenderer"):p.Renderer;
                return !Native.UnityAlive(plain)||((bool)Native.Get(plain!,"enabled")!&&
                    (bool)Native.Get(Native.Get(plain!,"gameObject")!,"activeInHierarchy")!);
            }
            // Visibility is native's decision (readiness, ownership, effects,
            // suppression). Use its color, but our own actual silhouette.
            var source=parts.Where(p=>!p.Main&&Enabled(p)).OrderBy(p=>p.Poly?1:0).FirstOrDefault();
            if(source is null)
            {
                foreach(var r in view.OutlineRenderers)if(Native.UnityAlive(r))
                    Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),Native.Get(r,"gameObject"),false);
                return;
            }
            while(view.OutlineRenderers.Count<layers.Count)
            {
                var go=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),"Deception actual outline")!;
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,false);
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),Native.Get(go,"transform"),view.ChildTransform,false);
                var type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"));
                view.OutlineRenderers.Add(Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),go,type)!,"UnityEngine.SpriteRenderer"));
            }
            for(int i=0;i<layers.Count;i++)
            {
                var art=layers[i];var renderer=view.OutlineRenderers[i];var go=Native.Get(renderer,"gameObject")!;
                var transform=Native.Get(renderer,"transform")!;
                if(!Native.Same(Native.Get(transform,"parent"),view.ChildTransform))throw new InvalidOperationException("Private outline changed parent.");
                var outline=UnitArtOutline.Get(art.Sprite,"Mimic layer "+i);
                if(outline is null){Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,false);continue;}
                Native.Set(go,"layer",Native.Get(view.Child!,"layer"));
                Native.Set(renderer,"sprite",outline.Sprite);Native.Set(renderer,"color",source.Original.Box());
                Native.Set(renderer,"sortingLayerID",layer);Native.Set(renderer,"sortingOrder",checked(order-layers.Count+i));
                var material=Native.Get(source.Renderer,source.Poly?"SharedMaterial":"sharedMaterial");
                if(Native.UnityAlive(material))Native.Set(renderer,"sharedMaterial",material);
                var size=PointOf(Native.Get(Native.Get(outline.Sprite,"bounds")!,"size")!);
                Native.Set(transform,"localPosition",new Point(art.X/100f,art.Y/100f,0).Box());
                Native.Set(transform,"localRotation",Native.Static("UnityEngine.Quaternion","identity"));
                Native.Set(transform,"localScale",new Point(art.Width*outline.WidthScale/100f/size.X*(art.MirrorX?-1f:1f),art.Height*outline.HeightScale/100f/size.Y,1).Box());
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,true);
            }
        }
        static void ApplyNativeAppearance(View view,List<Part> parts,object target,bool tintable)
        {
            // Native ColorizeUnit writes shader overlays, not color RGB, for
            // spent/frozen/etc. Copy only those two properties, so no native
            // texture or atlas binding can replace our own sprite.
            var visualParts=Native.Get(Native.Get(view.Unit,"skinVisuals")!,"visualParts");
            object? source=null,poly=null;
            foreach(var part in Native.Items(visualParts!))
            {
                if((bool)Native.Get(part,"tintable")! != tintable)continue;
                var union=Native.Get(part,"renderer")!;
                var psr=Native.Get(union,"polytopiaSpriteRenderer");
                var plain=Native.Get(union,"spriteRenderer");
                if(!Native.UnityAlive(plain)&&Native.UnityAlive(psr))plain=Native.Get(psr!,"spriteRenderer");
                bool Captured(object? renderer)=>Native.UnityAlive(renderer)&&parts.Any(p=>p.Main&&Native.Same(p.Renderer,renderer));
                // Batched Polytopia renderers retain their authoritative overlay
                // in the wrapper; the dormant Unity renderer can have stale data.
                if(Captured(psr)&&(bool)Native.Get(psr!,"isBatched")!){poly=psr;break;}
                if(Captured(plain)){source=plain;break;}
                if(Captured(psr))poly=psr; // Preserve wrapper-only native renderers.
            }
            view.NativeProperties??=Activator.CreateInstance(Native.T("UnityEngine.MaterialPropertyBlock"))!;
            view.ArtProperties??=Activator.CreateInstance(Native.T("UnityEngine.MaterialPropertyBlock"))!;
            object color=new Rgba(1,1,1,1).Box();float strength=0;object? material=null;
            if(source is not null)
            {
                material=Native.Get(source,"sharedMaterial");
                Native.Invoke(Native.M("UnityEngine.Renderer","GetPropertyBlock","UnityEngine.MaterialPropertyBlock"),source,view.NativeProperties);
                color=Native.Invoke(Native.M("UnityEngine.MaterialPropertyBlock","GetColor","System.String"),view.NativeProperties,"_OverlayColor")!;
                strength=Finite(Native.Invoke(Native.M("UnityEngine.MaterialPropertyBlock","GetFloat","System.String"),view.NativeProperties,"_OverlayStrength"));
            }
            else if(poly is not null)
            {
                material=Native.Get(poly,"SharedMaterial");color=Native.Get(poly,"overlayColor")!;
                strength=Finite(Native.Get(poly,"overlayStrength"));
            }
            // Assign borrowed material to this owned renderer only; never clone
            // or mutate native shared shader/material data.
            if(Native.UnityAlive(material))Native.Set(target,"sharedMaterial",material);
            Native.Invoke(Native.M("UnityEngine.MaterialPropertyBlock","SetColor","System.String","UnityEngine.Color"),view.ArtProperties,"_OverlayColor",color);
            Native.Invoke(Native.M("UnityEngine.MaterialPropertyBlock","SetFloat","System.String","System.Single"),view.ArtProperties,"_OverlayStrength",strength);
            Native.Invoke(Native.M("UnityEngine.Renderer","SetPropertyBlock","UnityEngine.MaterialPropertyBlock"),target,view.ArtProperties);
        }
        static bool StatusOwned(View view)=>Native.UnityAlive(view.Status)&&Native.UnityAlive(view.BadgeParent)&&
            Native.Same(Native.Get(view.Unit,"statusDisplay"),view.Status)&&Native.Same(Native.Get(view.Status!,"unit"),view.Unit)&&
            (bool)Native.Get(view.Status!,"IsUsed")!&&Native.Same(Native.Get(view.Status!,"typeContainer"),view.BadgeParent);
        static bool BadgeColorOwned(View view,BadgeColor color)=>Native.UnityAlive(view.Unit)&&Native.UnityAlive(view.Status)&&
            Native.UnityAlive(view.BadgeParent)&&Native.UnityAlive(color.Renderer)&&Native.UnityAlive(color.Transform)&&
            Convert.ToUInt32(Native.Get(Native.Get(view.Unit,"UnitState")!,"id"))==view.UnitId&&
            Native.Same(Native.Get(view.Unit,"transform"),view.Root)&&Native.Same(Native.Get(view.Unit,"spriteContainer"),view.Container)&&
            Native.Same(Native.Get(view.Status!,"unit"),view.Unit)&&Native.Same(Native.Get(view.Status!,"typeContainer"),view.BadgeParent)&&
            Native.Same(Native.Get(view.Status!,color.Role),color.Renderer)&&
            (bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),Native.Get(view.Status!,"transform"),view.Root)!&&
            (bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),color.Transform,view.BadgeParent)!;
        static void RestoreBadgeSprites(View view)
        {
            foreach(var part in view.NativeBadgeSprites)
            {
                var check=new BadgeColor{Role=part.Role,Renderer=part.Renderer,Transform=part.Transform};
                if(!BadgeColorOwned(view,check))continue;
                // Restore only exact values still owned by this replacement.
                // Native recoloring, reparenting or reuse wins over old art.
                if(part.Parent is not null&&Native.Same(Native.Get(part.Transform,"parent"),part.Parent))
                {
                    if(part.AppliedPosition is Point position&&PointOf(Native.Get(part.Transform,"localPosition")!)==position)
                        Native.Set(part.Transform,"localPosition",part.OriginalPosition!.Value.Box());
                    if(part.AppliedScale is Point scale&&PointOf(Native.Get(part.Transform,"localScale")!)==scale)
                        Native.Set(part.Transform,"localScale",part.OriginalScale!.Value.Box());
                    if(part.AppliedRotation is not null&&Equals(Native.Get(part.Transform,"localRotation"),part.AppliedRotation))
                        Native.Set(part.Transform,"localRotation",part.OriginalRotation);
                }
                if(part.AppliedColor is Rgba color&&ReadColor(new Part{Renderer=part.Renderer})==color)
                    Native.Set(part.Renderer,"color",part.OriginalColor!.Value.Box());
                if(Native.Same(Native.Get(part.Renderer,"sprite"),part.Applied))Native.Set(part.Renderer,"sprite",part.Original);
            }
            view.NativeBadgeSprites.Clear();
        }
        static void ApplyCustomGlyph(View view,bool spy,bool fogger=false)
        {
            var source=fogger?FoggerArtAssets.Badge():spy?UnitArtAssets.Icon(true):UnitArtAssets.MimicBadgeIcon();
            foreach(string role in new[]{"typeIcon","typeIconShadow"})
            {
                var renderer=Native.Get(view.Status!,role);if(!Native.UnityAlive(renderer))continue;
                var check=new BadgeColor{Role=role,Renderer=renderer!,Transform=Native.Get(renderer!,"transform")!};if(!BadgeColorOwned(view,check))continue;
                var original=Native.Get(renderer!,"sprite");
                var parent=Native.Get(check.Transform,"parent");if(!Native.UnityAlive(parent))continue;
                var frame=LocalBounds(parent!,Native.Get(Native.Get(view.Status!,"typeBg")!,"bounds")!);
                var placement=PlaceBadgeGlyph(frame,source,role=="typeIconShadow");
                var color=ReadColor(new Part{Renderer=renderer!});
                // Use the same supplied sprite, background fit and shadow
                // displacement as a genuine public custom badge. Placeholder
                // Warrior glyph size, pivot and transform do not define it.
                // Preserve the native shader's packed opacity/saturation byte.
                var appliedColor=role=="typeIcon"?new Rgba(1,1,1,color.A):color;
                var part=new BadgeSprite(role,renderer!,check.Transform,original,source){Parent=parent,
                    OriginalPosition=PointOf(Native.Get(check.Transform,"localPosition")!),OriginalScale=PointOf(Native.Get(check.Transform,"localScale")!),
                    OriginalRotation=Native.Get(check.Transform,"localRotation"),OriginalColor=color,
                    AppliedPosition=placement.Position,AppliedScale=placement.Scale,AppliedRotation=Native.Static("UnityEngine.Quaternion","identity"),AppliedColor=appliedColor};
                view.NativeBadgeSprites.Add(part);
                Native.Set(renderer!,"sprite",source);Native.Set(renderer!,"color",appliedColor.Box());
                Native.Set(check.Transform,"localPosition",placement.Position.Box());Native.Set(check.Transform,"localScale",placement.Scale.Box());
                Native.Set(check.Transform,"localRotation",part.AppliedRotation);
            }
        }
        static GlyphPlacement PlaceBadgeGlyph((Point Low,Point High) frame,object sprite,bool shadow)
        {
            float width=frame.High.X-frame.Low.X,height=frame.High.Y-frame.Low.Y;
            var bounds=Native.Get(sprite,"bounds")!;var size=PointOf(Native.Get(bounds,"size")!);var center=PointOf(Native.Get(bounds,"center")!);
            if(width<=0||height<=0||size.X<=0||size.Y<=0)throw new InvalidOperationException("Native badge glyph frame is empty.");
            float scale=Math.Min(width/size.X,height/size.Y)*0.68f,offset=shadow?0.025f:0;
            return new(new((frame.Low.X+frame.High.X)/2-center.X*scale+offset*width,
                (frame.Low.Y+frame.High.Y)/2-center.Y*scale-offset*height,(frame.Low.Z+frame.High.Z)/2),new(scale,scale,1));
        }
        static void RestoreBadgeColors(View view)
        {
            var writes=new List<(object Renderer,Rgba Color)>();
            foreach(var color in view.NativeBadgeColors)
            {
                // SpriteHandle replaces a role's sprite asynchronously. RGBA
                // ownership follows the exact native role/renderer hierarchy,
                // not that replaceable sprite and not a reused status object.
                if(BadgeColorOwned(view,color)&&ReadColor(new Part{Renderer=color.Renderer})==color.Applied&&color.Original!=color.Applied)
                    writes.Add((color.Renderer,color.Original));
            }
            view.NativeBadgeColors.Clear();
            foreach(var write in writes)Native.Set(write.Renderer,"color",write.Color.Box());
        }
        static void DimNativeBadge(View view,float factor=RearBadgeAlpha)
        {
            foreach(string role in new[]{"typeOutline","typeBg","typeIconShadow","typeIcon"})
            {
                var renderer=Native.Get(view.Status!,role);if(!Native.UnityAlive(renderer))continue;
                var color=new BadgeColor{Role=role,Renderer=renderer!,Transform=Native.Get(renderer!,"transform")!};
                if(!BadgeColorOwned(view,color))continue;
                color.Original=ReadColor(new Part{Renderer=renderer!});
                color.Applied=color.Original.Alpha(FadedBadgeAlpha(color.Original.A,EncodedBadgeAlpha(renderer!),factor));
                view.NativeBadgeColors.Add(color);
            }
            foreach(var color in view.NativeBadgeColors)Native.Set(color.Renderer,"color",color.Applied.Box());
            foreach(var color in view.NativeBadgeColors)
                if(ReadColor(new Part{Renderer=color.Renderer})!=color.Applied)throw new InvalidOperationException("Native type badge rejected rear alpha.");
        }
        static bool EncodedBadgeAlpha(object renderer)
        {
            var material=Native.Get(renderer,"sharedMaterial");
            if(!Native.UnityAlive(material))return false;
            var shader=Native.Get(material!,"shader");
            return Native.UnityAlive(shader)&&string.Equals(Native.Get(shader!,"name") as string,"Polytopia/Sprite",StringComparison.Ordinal);
        }
        static int BadgeAlphaByte(float alpha)=>(int)MathF.Round(Math.Clamp(alpha,0f,1f)*255f);
        internal static float BadgeOpacity(float alpha,bool encoded)=>encoded?(BadgeAlphaByte(alpha)&127)/127f:alpha;
        internal static float FadedBadgeAlpha(float alpha,bool encoded,float factor)
        {
            if(!encoded)return alpha*factor;
            // Polytopia/Sprite packs saturation in the high alpha bit and
            // opacity in the low seven bits. Multiplying 1 by .5 produces
            // byte128: fully TRANSPARENT in that native shader, not half opaque.
            int packed=BadgeAlphaByte(alpha);
            return ((packed&128)+(int)MathF.Round((packed&127)*factor))/255f;
        }
        static bool BadgePositionOwned(View view,BadgePosition position)=>Native.UnityAlive(view.Unit)&&Native.UnityAlive(view.Status)&&
            Native.UnityAlive(position.Transform)&&Native.UnityAlive(position.Parent)&&Native.Same(position.Transform,view.BadgeParent)&&
            Convert.ToUInt32(Native.Get(Native.Get(view.Unit,"UnitState")!,"id"))==view.UnitId&&
            Native.Same(Native.Get(view.Unit,"transform"),view.Root)&&Native.Same(Native.Get(view.Unit,"spriteContainer"),view.Container)&&
            Native.Same(Native.Get(view.Status!,"unit"),view.Unit)&&Native.Same(Native.Get(view.Status!,"typeContainer"),position.Transform)&&
            Native.Same(Native.Get(position.Transform,"parent"),position.Parent)&&
            (bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),Native.Get(view.Status!,"transform"),view.Root)!&&
            (bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),position.Transform,Native.Get(view.Status!,"transform"))!;
        static void RestoreBadgePosition(View view)
        {
            var position=view.NativeBadgePosition;view.NativeBadgePosition=null;
            if(position is not null&&BadgePositionOwned(view,position)&&PointOf(Native.Get(position.Transform,"localPosition")!)==position.Applied)
                Native.Set(position.Transform,"localPosition",position.Original.Box());
        }
        static void MoveNativeBadgeToRear(View view,Point rear)
        {
            var parent=Native.Get(view.BadgeParent!,"parent");
            if(!Native.UnityAlive(parent))throw new InvalidOperationException("Native type badge has no stable parent.");
            var position=new BadgePosition{Transform=view.BadgeParent!,Parent=parent!,Original=PointOf(Native.Get(view.BadgeParent!,"localPosition")!)};
            if(!BadgePositionOwned(view,position))throw new InvalidOperationException("Native type badge position ownership changed.");
            // The offset is measured in typeContainer coordinates. Convert it
            // through world space before assigning its parent's local position;
            // this preserves non-unit scale/rotation and every native role offset.
            var world=Native.Invoke(Native.M("UnityEngine.Transform","TransformPoint","UnityEngine.Vector3"),position.Transform,rear.Box());
            position.Applied=PointOf(Native.Invoke(Native.M("UnityEngine.Transform","InverseTransformPoint","UnityEngine.Vector3"),parent,world)!);
            view.NativeBadgePosition=position;
            Native.Set(position.Transform,"localPosition",position.Applied.Box());
            if(PointOf(Native.Get(position.Transform,"localPosition")!)!=position.Applied)throw new InvalidOperationException("Native type badge rejected rear position.");
        }
        static void ShowBadge(View view,bool actual,MimicProjection projection,bool single=false)
        {
            var status=Native.Get(view.Unit,"statusDisplay");
            var parent=Native.UnityAlive(status)?Native.Get(status!,"typeContainer"):null;
            if(!Native.UnityAlive(status)||!Native.UnityAlive(parent)||!Native.Same(Native.Get(status!,"unit"),view.Unit)||
                !(bool)Native.Get(status!,"IsUsed")!||!(bool)Native.Get(Native.Get(parent!,"gameObject")!,"activeInHierarchy")!)
            {DestroyBadge(view);return;}
            var bg=Native.Get(status!,"typeBg");var outline=Native.Get(status!,"typeOutline");
            var icon=Native.Get(status!,"typeIcon");var shadow=Native.Get(status!,"typeIconShadow");
            if(!Native.UnityAlive(bg)||!Native.UnityAlive(icon)||!Native.UnityAlive(Native.Get(bg!,"sprite"))||(!single&&!NativeFoggerPresentation.CustomBadge(projection)&&!projection.ApparentMimic&&!Native.UnityAlive(Native.Get(icon!,"sprite"))))
            {DestroyBadge(view);return;}
            if(!Native.Same(view.Status,status)||!Native.Same(view.BadgeParent,parent))DestroyBadge(view);
            view.Status=status;view.BadgeParent=parent;
            if(!single&&(NativeFoggerPresentation.CustomBadge(projection)||projection.ApparentMimic))ApplyCustomGlyph(view,!projection.ApparentMimic,NativeFoggerPresentation.ApparentBadge(projection));
            if(!Native.UnityAlive(view.Badge))
            {
                view.Badge=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),"Deception private Mimic badge")!;
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),view.Badge,false);
                view.BadgeTransform=Native.Get(view.Badge,"transform")!;
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),view.BadgeTransform,parent,false);
            }
            if(!Native.Same(Native.Get(view.BadgeTransform!,"parent"),parent))throw new InvalidOperationException("Private badge changed parent.");
            var bounds=LocalBounds(parent!,Native.Get(bg!,"bounds")!);
            float width=bounds.High.X-bounds.Low.X,height=bounds.High.Y-bounds.Low.Y;
            if(width<=0||height<=0){DestroyBadge(view);return;}
            var nativeRenderers=new[]{outline,bg,shadow,icon}.Where(Native.UnityAlive).Cast<object>().ToArray();
            int layer=Native.Int(Native.Get(icon!,"sortingLayerID"));
            var orders=nativeRenderers.Select(r=>Native.Int(Native.Get(r,"sortingOrder"))).ToArray();
            int first=checked(actual?orders.Max()+1:orders.Min()-4);
            object Renderer(string role)
            {
                if(view.BadgeRenderers.TryGetValue(role,out var existing)&&Native.UnityAlive(existing))return existing;
                var go=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),"Deception private badge "+role)!;
                var transform=Native.Get(go,"transform")!;
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),transform,view.BadgeTransform,false);
                var type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"));
                return view.BadgeRenderers[role]=Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),go,type)!,"UnityEngine.SpriteRenderer");
            }
            void Configure(string role,object source,object sprite,Rgba color,int order,bool mask)
            {
                // Native status rules can hide only the outline or another
                // individual role while keeping the type container visible.
                if(!(bool)Native.Get(Native.Get(source,"gameObject")!,"activeInHierarchy")!||!(bool)Native.Get(source,"enabled")!)
                {HideBadgeRole(view,role);return;}
                var renderer=Renderer(role);var go=Native.Get(renderer,"gameObject")!;
                Native.Set(go,"layer",Native.Get(Native.Get(source,"gameObject")!,"layer"));
                Native.Set(renderer,"sprite",sprite);Native.Set(renderer,"color",(actual?color:color.Alpha(color.A*RearBadgeAlpha)).Box());
                Native.Set(renderer,"sortingLayerID",layer);Native.Set(renderer,"sortingOrder",order);
                var frame=mask?bounds:LocalBounds(parent!,Native.Get(source,"bounds")!);
                float frameWidth=frame.High.X-frame.Low.X,frameHeight=frame.High.Y-frame.Low.Y;
                var spriteBounds=Native.Get(sprite,"bounds")!;var size=PointOf(Native.Get(spriteBounds,"size")!);
                var center=PointOf(Native.Get(spriteBounds,"center")!);
                if(frameWidth<=0||frameHeight<=0||size.X<=0||size.Y<=0)throw new InvalidOperationException("Native badge frame is empty.");
                float xScale=frameWidth/size.X,yScale=frameHeight/size.Y;
                var transform=Native.Get(renderer,"transform")!;
                var placement=mask?PlaceBadgeGlyph(frame,sprite,role=="shadow"):new GlyphPlacement(
                    new((frame.Low.X+frame.High.X)/2-center.X*xScale,(frame.Low.Y+frame.High.Y)/2-center.Y*yScale,(frame.Low.Z+frame.High.Z)/2),new(xScale,yScale,1));
                Native.Set(transform,"localPosition",placement.Position.Box());
                Native.Set(transform,"localRotation",Native.Static("UnityEngine.Quaternion","identity"));
                Native.Set(transform,"localScale",placement.Scale.Box());
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,true);
            }
            Rgba Color(object renderer)
            {
                var color=ReadColor(new Part{Renderer=renderer});
                // Owned badge renderers use Unity's ordinary Sprite default
                // material. Copy visual opacity, not the native packed byte.
                return color.Alpha(BadgeOpacity(color.A,EncodedBadgeAlpha(renderer)));
            }
            if(Native.UnityAlive(outline)&&Native.UnityAlive(Native.Get(outline!,"sprite")))Configure("outline",outline!,Native.Get(outline!,"sprite")!,Color(outline!),first,false);
            else HideBadgeRole(view,"outline");
            Configure("background",bg!,Native.Get(bg!,"sprite")!,Color(bg!),first+1,false);
            var mask=projection.ActualIsMimic?MaskBadgeSprite():(NativeFoggerPresentation.IsFogger(projection)||NativeFoggerPresentation.IsForm(projection.ActualBadgeNativeId))?FoggerArtAssets.Badge():(NativeSpyPresentation.IsSpy(projection)||Deception.Core.SpyCatalog.IsSpyId(projection.ActualBadgeNativeId))?UnitArtAssets.Icon(true):UnitArtAssets.NativeIcon(
                string.IsNullOrEmpty(projection.ActualBadgeNativeId)?projection.ActualNativeId:projection.ActualBadgeNativeId);
            if(!Native.UnityAlive(mask)){DestroyBadge(view);return;}
            if(Native.UnityAlive(shadow))Configure("shadow",shadow!,mask,Color(shadow!),first+2,true);
            else HideBadgeRole(view,"shadow");
            Configure("mask",icon!,mask,new Rgba(1,1,1,Color(icon!).A),first+3,true);
            // Leave the rear glyph's center outside the opaque front disk.
            // The tighter old overlap hid small native glyphs (e.g. Warrior)
            // while still exposing their colored rim. These remain two fixed
            // slots; focus swaps identities and never changes their separation.
            var rear=new Point(-width*0.65f,height*0.40f,0);
            // Front is always the original native anchor. The mask's opposite
            // offset cancels the native container's rear movement in actual mode.
            Native.Set(view.BadgeTransform!,"localPosition",(single?new Point(0,0,0):actual?new Point(-rear.X,-rear.Y,-rear.Z):rear).Box());
            Native.Set(view.BadgeTransform!,"localRotation",Native.Static("UnityEngine.Quaternion","identity"));
            Native.Set(view.BadgeTransform!,"localScale",new Point(1,1,1).Box());
            if(actual&&!single)MoveNativeBadgeToRear(view,rear);
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),view.Badge!,true);
            if(actual)DimNativeBadge(view,single?0:RearBadgeAlpha);

        }
        static (Point Low,Point High) LocalBounds(object parent,object bounds)
        {
            var low=PointOf(Native.Invoke(Native.M("UnityEngine.Transform","InverseTransformPoint","UnityEngine.Vector3"),parent,Native.Get(bounds,"min"))!);
            var high=PointOf(Native.Invoke(Native.M("UnityEngine.Transform","InverseTransformPoint","UnityEngine.Vector3"),parent,Native.Get(bounds,"max"))!);
            return (new Point(Math.Min(low.X,high.X),Math.Min(low.Y,high.Y),Math.Min(low.Z,high.Z)),
                new Point(Math.Max(low.X,high.X),Math.Max(low.Y,high.Y),Math.Max(low.Z,high.Z)));
        }
        static object MaskBadgeSprite()=>UnitArtAssets.MimicBadgeIcon();
        static void HideBadgeRole(View view,string role)
        {if(view.BadgeRenderers.TryGetValue(role,out var renderer)&&Native.UnityAlive(renderer))Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),Native.Get(renderer,"gameObject"),false);}
        static Point PointOf(object vector)
        {
            float Axis(int n)=>Finite(Native.Invoke(Native.M("UnityEngine.Vector3","get_Item","System.Int32"),vector,n));
            return new Point(Axis(0),Axis(1),Axis(2));
        }
        static float Finite(object? value)
        {float number=Convert.ToSingle(value,System.Globalization.CultureInfo.InvariantCulture);if(!float.IsFinite(number))throw new InvalidOperationException("Native map geometry/color is not finite.");return number;}
        static void HideChild(View view)
        {if(Native.UnityAlive(view.Child))Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),view.Child,false);}
        static void HideBadge(View view)
        {if(Native.UnityAlive(view.Badge))Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),view.Badge,false);}
        static void DestroyBadge(View view)
        {
            RestoreBadgePosition(view);RestoreBadgeColors(view);RestoreBadgeSprites(view);
            var badge=view.Badge;var transform=view.BadgeTransform;var parent=view.BadgeParent;
            view.Badge=null;view.BadgeTransform=null;view.BadgeParent=null;view.Status=null;view.BadgeRenderers.Clear();
            if(!Native.UnityAlive(badge))return;
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),badge,false);
            if(Native.UnityAlive(transform)&&Native.Same(Native.Get(transform!,"parent"),parent))
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),transform,null,false);
            Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,badge);
        }
        static void DestroyChild(View view)
        {
            var child=view.Child;var transform=view.ChildTransform;view.Child=null;view.ChildRenderer=null;view.ChildTransform=null;view.ArtIsActual=false;view.ArtRenderers.Clear();view.OutlineRenderers.Clear();
            if(!Native.UnityAlive(child))return;
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),child,false);
            // Unity destruction is deferred. Exclude our retired renderer from
            // any same-frame native hierarchy copy before scheduling removal.
            if(Native.UnityAlive(transform)&&Native.Same(Native.Get(transform!,"parent"),view.Container))
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),transform,null,false);
            Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,child);
        }
        static void Remove(long key)
        {
            if(!Views.Remove(key,out var view))return;
            try{RestoreParts(view);}catch(Exception e){Note("mimic-map-restore-error",e);}
            finally
            {
                try{DestroyChild(view);}catch(Exception e){Note("mimic-map-child-close-error",e);}
                try{DestroyBadge(view);}catch(Exception e){Note("mimic-map-badge-close-error",e);}
                try{MimicPresentationUi.RefreshMapEffects(view.Unit,true);}catch(Exception e){Note("mimic-map-effects-close-error",e);}
                if(view.AppliedMode==true&&!view.PublicArt)
                {
                    bool wasApplying=Applying;Applying=true;
                    try{MimicPresentationUi.RefreshMapHealth(view.Unit);}
                    catch(Exception e){Note("mimic-map-health-close-error",e);}
                    finally{Applying=wasApplying;}
                }
            }
        }
        public static void Clear()
        {
            ClearBlackmail();
            foreach(long key in RogueViews.Keys.ToArray())RemoveRogue(key);
            foreach(long key in Views.Keys.ToArray())Remove(key);Updating.Clear();Duplicating.Clear();PrefabLayouts.Clear();Noted.Clear();
        }
    }
}
