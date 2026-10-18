using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native category widgets retain their factory ownership, sizing and clicks.
    internal static class MimicControls
    {
        static readonly Dictionary<long,object> HiddenIcons=new();
        static readonly Dictionary<long,object> HiddenRenderers=new();
        static readonly Dictionary<long,object> HiddenTechIcons=new();
        static readonly HashSet<string> ReportedErrors=new(StringComparer.Ordinal);
        static readonly HashSet<string> ReportedPreviews=new(StringComparer.Ordinal);
        static void Note(string kind,Exception error)
        {
            string detail=error.ToString();
            if(ReportedErrors.Count<32&&ReportedErrors.Add(kind+"\n"+detail))Entry.Note(kind,detail);
        }
        public static void ValidateVisibilityApi()
        {
            // Unity reference DLLs are not included in the private handoff. Bind
            // these inherited members by their complete runtime signatures before
            // installing any hook; there is no value-type/native ABI detour here.
            Native.RequireReturn(Native.M("UnityEngine.Component","get_gameObject"),Native.T("UnityEngine.GameObject"));
            Native.RequireReturn(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),typeof(void));
        }
        public static void SetVisible(object component,bool visible)
        {
            var gameObject=Native.Get(component,"gameObject");
            if(gameObject is not null)Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),gameObject,visible);
        }
        public static void Install(Harmony h)
        {
            ValidateVisibilityApi();
            Native.RequireReturn(Native.M("UIUtils","GetImageVerticesBounds","UnityEngine.RectTransform"),Native.T("UnityEngine.Bounds"));
            Native.RequireReturn(Native.M("UIUtils","FitImageContentInParent","UnityEngine.RectTransform"),typeof(void));
            Native.RequireReturn(Native.M("UIConstants","get_UI_PIXELS_PER_UNIT"),typeof(float));
            Native.RequireReturn(Native.M("UnityEngine.UI.Image","get_overrideSprite"),Native.T("UnityEngine.Sprite"));
            Native.RequireReturn(Native.M("UnityEngine.UI.Image","set_overrideSprite","UnityEngine.Sprite"),typeof(void));
            Native.RequireReturn(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),Native.T("UnityEngine.Sprite"));
            Native.Hook(h,Native.M("UIUtils","GetUIUnitRenderer","Polytopia.Data.UnitData","PlayerState","PolytopiaBackendBase.Common.SkinType"),typeof(MimicControls),nameof(PreviewSpecies),nameof(CategoryRenderer));
            _=Native.M("UIRoundButton","get_icon");_=Native.M("UIRoundButton","SetText","System.String");
            _=Native.M("UIUnitRenderer","get_unitType");_=Native.M("UIRoundButton_UI2","set_Text","System.String");
            Native.Hook(h,Native.M("UIRoundButton","SetUnitRenderer","UIUnitRenderer"),typeof(MimicControls),null,nameof(RendererAssigned));
            Native.RequireReturn(Native.M("GameManager","GetSpriteAtlasManager"),Native.T("SpriteAtlasManager"));
            Native.RequireReturn(Native.M("SpriteAtlasManager","GetSprite","System.Int32"),Native.T("UnityEngine.Sprite"));
            foreach(string name in new[]{"UI_BACK_WHITE","UI_INFO","UNKNOWN_ICON"})
                Native.RequireReturn(Native.M("SpriteRef","get_"+name),typeof(int));
            Native.Hook(h,Native.M("UIRoundButton","SetSprite","UnityEngine.Sprite","System.Boolean"),typeof(MimicControls),null,nameof(SpriteAssigned));
            Native.Hook(h,Native.M("UIRoundButton","SetSprite","System.String","System.Boolean"),typeof(MimicControls),null,nameof(SpriteAssigned));
            Native.Hook(h,Native.M("TechPopupContent","SetUnitData","UIRoundButton_UI2","Polytopia.Data.UnitData"),typeof(MimicControls),nameof(TechSpecies),nameof(TechCategory));
        }
        public static void Clear()
        {
            TechPresentation.Clear();
            foreach(var icon in System.Linq.Enumerable.Concat(System.Linq.Enumerable.Concat(HiddenIcons.Values,HiddenRenderers.Values),HiddenTechIcons.Values))
                try{SetVisible(icon,true);}catch{/* A pooled UI object may already have been destroyed. */}
            HiddenIcons.Clear();HiddenRenderers.Clear();HiddenTechIcons.Clear();ReportedErrors.Clear();ReportedPreviews.Clear();PendingPreviews.Clear();
        }
        static void TextOnly(object button,string text)
        {
            Native.Invoke(Native.M("UIRoundButton","SetText","System.String"),button,text);
            var label=Native.Get(button,"Label");if(label is not null)Native.Set(label,"Text",text);
            Native.Set(button,"ShowLabel",true);Native.Set(button,"LabelShowOnHover",false);Native.Set(button,"ShowTextBackground",true);
            var icon=Native.Get(button,"icon");
            if(icon is not null){SetVisible(icon,false);HiddenIcons[Native.Pointer(button)]=icon;}
        }
        public static object NativeSprite(string property)
        {
            // These are native SpriteRef members inspected in this game's exact
            // wrapper, not string asset names whose fallback could be mistaken
            // for a successfully loaded icon.
            var atlas=Native.Invoke(Native.M("GameManager","GetSpriteAtlasManager"),null)
                ??throw new InvalidOperationException("Native sprite atlas is unavailable.");
            int id=Native.Int(Native.Static("SpriteRef",property));
            return Native.Invoke(Native.M("SpriteAtlasManager","GetSprite","System.Int32"),atlas,id)
                ??throw new InvalidOperationException("Native sprite is unavailable: "+property);
        }
        public static void ApplyIcon(object button,string property,string text)
        {
            Native.Invoke(Native.M("UIRoundButton","SetText","System.String"),button,text);
            var label=Native.Get(button,"Label");if(label is not null)Native.Set(label,"Text",text);
            Native.Set(button,"ShowLabel",true);Native.Set(button,"LabelShowOnHover",false);Native.Set(button,"ShowTextBackground",true);
            Native.Invoke(Native.M("UIRoundButton","SetSprite","UnityEngine.Sprite","System.Boolean"),button,NativeSprite(property),true);
            SpriteAssigned(button);
            var icon=Native.Get(button,"icon");if(icon is not null)SetVisible(icon,true);
        }
        public static void ApplyBack(object button)=>ApplyIcon(button,"UI_BACK_WHITE","Back");
        public static void ApplyCategory(object button)
        {
            Native.Invoke(Native.M("UIRoundButton","SetText","System.String"),button,"Mimic");
            var label=Native.Get(button,"Label");if(label is not null)Native.Set(label,"Text","Mimic");
            Native.Set(button,"ShowLabel",true);Native.Set(button,"LabelShowOnHover",false);Native.Set(button,"ShowTextBackground",true);
            Native.Invoke(Native.M("UIRoundButton","SetSprite","UnityEngine.Sprite","System.Boolean"),button,UnitArtAssets.Icon(),false);
            SpriteAssigned(button);
            var icon=Native.Get(button,"icon");if(icon is not null)SetVisible(icon,true);
        }
        public static void SpriteAssigned(object __instance)
        {
            try
            {
                // A category button can be reused for an ordinary sprite action
                // without ever receiving another UnitRenderer.
                if(HiddenIcons.Remove(Native.Pointer(__instance),out var icon))SetVisible(icon,true);
            }
            catch(Exception e){Note("mimic-control-sprite-error",e);}
        }
        public static void RendererAssigned(object __instance,object __0)
        {
            try
            {
                if(__0 is null)return;
                string id=Native.EnumName(Native.Get(__0,"unitType")!);
                bool category=RecruitmentCatalog.IsMenuId(id)||NativeFoggerPresentation.CustomForm(id);
                if(category)
                {
                    string title=RecruitmentCatalog.Label(id);
                    Native.Invoke(Native.M("UIRoundButton","SetText","System.String"),__instance,title);
                    var label=Native.Get(__instance,"Label");if(label is not null)Native.Set(label,"Text",title);
                    Native.Set(__instance,"ShowLabel",true);Native.Set(__instance,"LabelShowOnHover",false);
                    SetVisible(__0,true);
                }
                else
                {
                    // Native buttons/renderers may be pooled and used for a normal
                    // unit next. Undo only this adapter's hidden icon state.
                    if(HiddenIcons.Remove(Native.Pointer(__instance),out var icon))SetVisible(icon,true);
                    if(HiddenRenderers.Remove(Native.Pointer(__0),out var renderer))SetVisible(renderer,true);
                }
            }
            catch(Exception e){Note("mimic-category-control-error",e);}
        }
        sealed class Preview
        {
            public object Renderer=null!,Player=null!,Game=null!,Container=null!,Data=null!,Destination=null!;
            public object[] Children=Array.Empty<object>();
            public int Owner,Tribe,Skin,Color,Age;
            public string Type=RecruitmentCatalog.MenuId;
            public bool Spy=>SpyCatalog.IsSpyId(Type);
            public bool Fogger=>NativeFoggerPresentation.IsForm(Type);
            public bool Aquarion=>Type==AquarionCatalog.MimicId||Type==SpyCatalog.AquarionId;
        }
        static readonly Dictionary<long,Preview> PendingPreviews=new();
        const int MaxPendingPreviews=64,MaxPendingTicks=600;
        internal static int PendingPreviewCount=>PendingPreviews.Count;
        static int Tribe(object player)=>Native.Int(Native.Invoke(Native.M("PlayerExtensions","GetVisualTribeType","PlayerState"),null,player));
        static int Skin(object player)=>Native.Int(Native.Invoke(Native.M("PlayerState","GetVisualSkin"),player));
        static object[] Children(object parent)
        {
            int count=Native.Int(Native.Get(parent,"childCount"));var result=new object[count];
            for(int i=0;i<count;i++)result[i]=Native.Invoke(Native.M("UnityEngine.Transform","GetChild","System.Int32"),parent,i)!;
            return result;
        }
        static bool ChildrenMatch(Preview p)
        {
            var children=Children(p.Destination);
            return children.Length==p.Children.Length&&children.Zip(p.Children,(a,b)=>Native.Same(a,b)&&Native.UnityAlive(a)&&Native.Same(Native.Get(a,"parent"),p.Destination)).All(x=>x);
        }
        static bool Current(Preview p)=>Native.UnityAlive(p.Renderer)&&Native.UnityAlive(p.Container)&&
            Native.EnumName(Native.Get(p.Renderer,"unitType")!)==p.Type&&
            Native.Same(Native.Get(p.Renderer,"duplicatedSpritesContainer"),p.Container)&&
            Native.Same(Native.Get(p.Renderer,"spriteDuplicationData"),p.Data)&&
            Native.Same(Native.Get(p.Data,"container"),p.Container)&&
            Native.Same(Native.Get(p.Container,"rectTransform"),p.Destination)&&
            Native.Same(Native.Static("GameManager","GameState"),p.Game)&&
            Native.Int(Native.Get(p.Player,"Id"))==p.Owner&&Tribe(p.Player)==p.Tribe&&Skin(p.Player)==p.Skin&&
            Native.Int(Native.Get(p.Player,"color"))==p.Color&&ChildrenMatch(p);
        static float Axis(object vector,int axis)=>Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Vector3","get_Item","System.Int32"),vector,axis));
        static (float X,float Y,float Extent) Bounds(object rect)
        {
            // Same content-vertex measurement the native tech and round-button
            // fitters use, including native sprite vertices and child transforms.
            var bounds=Native.Invoke(Native.M("UIUtils","GetImageVerticesBounds","UnityEngine.RectTransform"),null,rect)!;
            var center=Native.Get(bounds,"center")!;var size=Native.Get(bounds,"size")!;
            float x=Axis(center,0),y=Axis(center,1),w=Axis(size,0),h=Axis(size,1);
            if(!float.IsFinite(x)||!float.IsFinite(y)||!float.IsFinite(w)||!float.IsFinite(h)||w<=0||h<=0)
                throw new InvalidOperationException("Category unit content bounds are invalid.");
            return (x,y,Math.Max(w,h));
        }
        static void RetirePreviewChild(object child,object expectedParent)
        {
            if(!Native.UnityAlive(child)||!Native.Same(Native.Get(child,"parent"),expectedParent))return;
            var go=Native.Get(child,"gameObject")!;
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,false);
            Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),child,null,false);
            Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,go);
        }
        sealed record UiSprite(object Source,float Width,float Height,float Ppu,object Sprite);
        static readonly List<UiSprite> UiSprites=new();
        static IReadOnlyList<UnitArtAssets.Layer> CategoryLayers(object player,bool spy=false,bool aquarion=false,bool fogger=false)
        {
            var layers=MimicInspectionControls.ActualLayers(player,spy:spy,aquarion:aquarion,fogger:fogger);
            if(layers.Count!=1&&layers.Count!=(fogger?2:spy||aquarion?3:4))throw new InvalidOperationException("Unsupported category artwork layer layout.");
            float ppu=Convert.ToSingle(Native.Static("UIConstants","UI_PIXELS_PER_UNIT"));
            if(!float.IsFinite(ppu)||ppu<=0)throw new InvalidOperationException("Native UI pixels-per-unit is invalid.");
            return layers.Select(layer=>
            {
                // Native atlas heads remain borrowed; only our PNG-backed views
                // receive normalized PPU. No pixel, shared Sprite or atlas edit.
                if(layer.AssetName is null)return layer;
                var old=UiSprites.FirstOrDefault(v=>Native.Same(v.Source,layer.Sprite)&&v.Width==layer.Width&&v.Height==layer.Height&&v.Ppu==ppu&&Native.UnityAlive(v.Sprite));
                if(old is not null)return layer with{Sprite=old.Sprite};
                var rect=Native.Get(layer.Sprite,"rect")!;
                float width=Convert.ToSingle(Native.Get(rect,"width")),height=Convert.ToSingle(Native.Get(rect,"height"));
                if(Math.Abs(width/height-layer.Width/layer.Height)>0.001f)throw new InvalidOperationException("Category PNG aspect ratio differs from its design layer.");
                var pivot=Activator.CreateInstance(Native.T("UnityEngine.Vector2"),0.5f,0.5f)!;
                var timer=Stopwatch.StartNew();
                // Provider PNGs use a full rectangle. These cached PPU views
                // must use the same cheap quad, not rescan the large alpha image
                // to regenerate a Tight contour for every display scale.
                var sprite=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),null,
                    Native.Get(layer.Sprite,"texture"),rect,pivot,ppu*width/layer.Width,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"))
                    ??throw new InvalidOperationException("Category PNG view could not be created.");
                Entry.Note("unit-art-category-view","pixels="+width+"x"+height+" design="+layer.Width+"x"+layer.Height+
                    " mesh=FullRect createMs="+timer.Elapsed.TotalMilliseconds.ToString("F1",System.Globalization.CultureInfo.InvariantCulture));
                // Process-lifetime cache mirrors the provider's Sprite lifetime;
                // native widget destruction owns Images, never shared textures.
                UiSprites.Add(new(layer.Sprite,layer.Width,layer.Height,ppu,sprite));
                return layer with{Sprite=sprite};
            }).ToArray();
        }
        sealed record HeadSize(float Width,float Height,float Ppu,object Texture,object Sprite);
        static readonly List<HeadSize> HeadSizingSprites=new();
        static void SetHeadSizingSprite(object[] children,IReadOnlyList<UnitArtAssets.Layer> layers)
        {
            if(layers.Count==1||!layers.Any(l=>l.IsHead))return; // Complete Fogger art has no borrowed atlas head.
            if((layers.Count!=4&&layers.Count!=3)||children.Length!=layers.Count)throw new InvalidOperationException("Category head layout differs.");
            int headIndex=Enumerable.Range(0,layers.Count).Single(i=>layers[i].IsHead);
            var head=layers[headIndex];
            // These views supply layout geometry only. Unity Image renders the
            // original atlas head through overrideSprite, preserving its exact
            // packed UVs and texture. Native UIUtils reads the regular sprite.
            float ppu=Convert.ToSingle(Native.Static("UIConstants","UI_PIXELS_PER_UNIT"));
            var icon=UnitArtAssets.Icon();var texture=Native.Get(icon,"texture")!;
            var cached=HeadSizingSprites.FirstOrDefault(v=>v.Width==head.Width&&v.Height==head.Height&&v.Ppu==ppu&&Native.Same(v.Texture,texture)&&Native.UnityAlive(v.Sprite));
            object sizing;
            if(cached is not null)sizing=cached.Sprite;
            else
            {
                var available=Native.Get(icon,"rect")!;
                float x=Convert.ToSingle(Native.Get(available,"x")),y=Convert.ToSingle(Native.Get(available,"y"));
                float w=Convert.ToSingle(Native.Get(available,"width")),h=Convert.ToSingle(Native.Get(available,"height"));
                float cropWidth=Math.Min(w,h*head.Width/head.Height),cropHeight=cropWidth*head.Height/head.Width;
                float viewPpu=ppu*cropWidth/head.Width;
                if(!float.IsFinite(viewPpu)||viewPpu<=0||!float.IsFinite(cropHeight)||cropHeight<=0)
                    throw new InvalidOperationException("Invalid category head sizing geometry.");
                var rect=Activator.CreateInstance(Native.T("UnityEngine.Rect"),x,y,cropWidth,cropHeight)!;
                var pivot=Activator.CreateInstance(Native.T("UnityEngine.Vector2"),0.5f,0.5f)!;
                sizing=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),null,
                    texture,rect,pivot,viewPpu,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"))
                    ??throw new InvalidOperationException("Category head sizing view could not be created.");
                HeadSizingSprites.Add(new(head.Width,head.Height,ppu,texture,sizing));
            }
            var imageType=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.UI.Image"));
            var go=Native.Get(children[headIndex],"gameObject")!;
            var raw=Native.Invoke(Native.M("UnityEngine.GameObject","GetComponent","Il2CppSystem.Type"),go,imageType)
                ??throw new InvalidOperationException("Category head Image is absent.");
            var image=Native.As(raw,"UnityEngine.UI.Image");
            if(!Native.UnityAlive(image)||!Native.Same(Native.Get(image,"gameObject"),go)||!Native.Same(Native.Get(image,"sprite"),head.Sprite))
                throw new InvalidOperationException("Category head Image ownership differs.");
            // Set the visible override first, so no sizing pixels can be shown.
            Native.Set(image,"overrideSprite",head.Sprite);Native.Set(image,"sprite",sizing);
            if(!Native.Same(Native.Get(image,"overrideSprite"),head.Sprite)||!Native.Same(Native.Get(image,"sprite"),sizing))
                throw new InvalidOperationException("Category head render/layout binding differs.");
        }
        static void PaintPreview(Preview p,bool preserveFit=false)
        {
            if(!Current(p))throw new InvalidOperationException("Category unit preview ownership changed.");
            var layers=CategoryLayers(p.Player,p.Spy,p.Aquarion,p.Fogger);
            object root=Native.Get(p.Renderer,"rectTransform")!;
            var before=preserveFit?Bounds(root):default;
            object? oldScale=preserveFit?Native.Get(root,"localScale"):null;
            object? oldPosition=preserveFit?Native.Get(root,"anchoredPosition"):null;
            object? stage=null;object? stageRect=null;bool stageOwned=false;object[] added=Array.Empty<object>();bool published=false,fitTouched=false;var originals=p.Children;
            try
            {
                // An unattached native factory object owns all partial output.
                // The original unit images stay intact until staging succeeds.
                stage=Native.Invoke(Native.M("UIUtils","GetImage"),null)
                    ??throw new InvalidOperationException("Category unit staging factory returned null.");
                stageRect=Native.Get(stage,"rectTransform")!;
                if(Native.Get(stageRect,"parent") is not null||Children(stageRect).Length!=0||Native.Same(stageRect,p.Destination))
                    throw new InvalidOperationException("Category unit staging object is not empty and detached.");
                stageOwned=true;Native.Set(stage,"sprite",null);Native.Set(stage,"raycastTarget",false);
                added=MimicInspectionControls.AppendActualLayers(stageRect,layers,1,0,0);
                SetHeadSizingSprite(added,layers);
                // Native GetImageVerticesBounds reads Sprite vertices in UI
                // units; it ignores Image sizes and child scale. Normalized PNG
                // views and the head sizing view measure every rendered part.
                _=Bounds(stageRect);
                if(!Current(p))throw new InvalidOperationException("Category unit preview changed during staging.");
                // Keep originals alive and recoverable until final native
                // bounds and the completed fit have both been validated.
                foreach(var child in originals)Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),child,stageRect,false);
                foreach(var child in added)Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),child,p.Destination,false);
                if(preserveFit)
                {
                    var after=Bounds(root);float ratio=before.Extent/after.Extent;
                    float sx=Axis(oldScale!,0),sy=Axis(oldScale!,1),sz=Axis(oldScale!,2);
                    float px=Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),oldPosition,0));
                    float py=Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),oldPosition,1));
                    float nx=px+before.X*sx-after.X*sx*ratio,ny=py+before.Y*sy-after.Y*sy*ratio;
                    if(!float.IsFinite(ratio)||ratio<=0||!float.IsFinite(sx*ratio)||!float.IsFinite(sy*ratio)||!float.IsFinite(sz)||!float.IsFinite(nx)||!float.IsFinite(ny))
                        throw new InvalidOperationException("Category completion fit is invalid.");
                    fitTouched=true;
                    Native.Set(root,"localScale",Activator.CreateInstance(Native.T("UnityEngine.Vector3"),sx*ratio,sy*ratio,sz)!);
                    Native.Set(root,"anchoredPosition",Activator.CreateInstance(Native.T("UnityEngine.Vector2"),nx,ny)!);
                }
                p.Children=added;added=Array.Empty<object>();published=true;
                foreach(var child in originals)RetirePreviewChild(child,stageRect);
            }
            finally
            {
                if(!published)
                {
                    foreach(var child in added)RetirePreviewChild(child,p.Destination);
                    foreach(var child in originals)
                        if(stageRect is not null&&Native.UnityAlive(child)&&Native.Same(Native.Get(child,"parent"),stageRect))
                            Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),child,p.Destination,false);
                    if(fitTouched&&Native.UnityAlive(root))
                    {Native.Set(root,"localScale",oldScale!);Native.Set(root,"anchoredPosition",oldPosition!);}
                }
                if(stageOwned&&stage is not null&&Native.UnityAlive(stage))
                    Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(stage,"gameObject"));
            }
        }
        public static void PreviewSpecies(ref Polytopia.Data.UnitData __0,object __1)
        {if(__0 is not null)__0=(Polytopia.Data.UnitData)TechPresentation.FinalUnit(__0,__1);}
        public static void TechSpecies(ref Polytopia.Data.UnitData __1)
        {if(__1 is not null)__1=(Polytopia.Data.UnitData)TechPresentation.FinalUnit(__1,Native.Static("GameManager","LocalPlayer"));}
        static void PreviewReady(Preview p,bool ready)
        {
            // The earlier tech log records only the chosen UnitData. Record the
            // completed factory output as well, once per species/cosmetic state,
            // so a native report can distinguish selection from rendered art.
            string key=p.Type+":"+p.Owner+":"+p.Tribe+":"+p.Skin+":"+ready;
            if(ReportedPreviews.Count>=32||ReportedPreviews.Contains(key)||!Current(p))return;
            ReportedPreviews.Add(key);
            string body=!ready?"pending glyph":p.Fogger?"fogger_fixed + fogger_team":p.Spy?(p.Aquarion?"spymaid_fixed + spymaid_team":"spy-fixed + spy-team"):
                p.Aquarion?"mermimic_body_team":"mimic-body-fixed + mimic-body-team";
            Entry.Note("unit-category-rendered","species="+p.Type+" owner="+p.Owner+" visualTribe="+p.Tribe+" skin="+p.Skin+
                " layers="+p.Children.Length+" body="+body+" renderer="+Native.Pointer(p.Renderer)+"; native screen acceptance pending.");
        }
        // Popup children are in the same design coordinate space as category
        // previews. UnitPopup has already fitted that space once; never replace
        // it with bar-sized Images while retaining the native renderer scale.
        internal static void FitSpyPopup(object unitData,object player,object renderer,object expectedParent)
        {
            if(!RecruitmentHooks.Registered||!NativeFoggerPresentation.CustomForm(RecruitmentHooks.Id(unitData)))
                throw new InvalidOperationException("Popup Spy species is not registered.");
            var p=new Preview{Type=RecruitmentHooks.Id(unitData),Renderer=renderer,Player=player,Game=Native.Static("GameManager","GameState")!,
                Container=Native.Get(renderer,"duplicatedSpritesContainer")!,Data=Native.Get(renderer,"spriteDuplicationData")!,
                Owner=Native.Int(Native.Get(player,"Id")),Tribe=Tribe(player),Skin=Skin(player),Color=Native.Int(Native.Get(player,"color"))};
            p.Destination=Native.Get(p.Container,"rectTransform")!;p.Children=Children(p.Destination);
            var root=Native.Get(renderer,"rectTransform")!;
            bool ParentCurrent()=>Native.UnityAlive(expectedParent)&&Native.Same(Native.Get(root,"parent"),expectedParent);
            if(p.Children.Length==0||p.Game is null||!Current(p)||!ParentCurrent())throw new InvalidOperationException("Popup Spy renderer ownership changed.");
            // The guarded popup lifecycle, not the generic category queue, owns
            // delayed head completion and the copied cosmetic player here.
            PendingPreviews.Remove(Native.Pointer(renderer));
            PaintPreview(p);
            if(!ParentCurrent())throw new InvalidOperationException("Popup Spy parent changed while composing.");
            Native.Invoke(Native.M("UIUtils","FitImageContentInParent","UnityEngine.RectTransform"),null,root);
            if(!Current(p)||!ParentCurrent())throw new InvalidOperationException("Popup Spy renderer changed while fitting.");
            if(p.Fogger)
            {
                // FitImageContentInParent resets native scale on every call.
                // Enlarge once around its fitted center, never the shifted pivot.
                var bounds=Bounds(root);var scale=Native.Get(root,"localScale")!;var position=Native.Get(root,"anchoredPosition")!;
                float sx=Axis(scale,0),sy=Axis(scale,1),sz=Axis(scale,2),extra=FoggerArtAssets.DisplayScale-1;
                float x=Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),position,0)),
                    y=Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),position,1));
                Native.Set(root,"localScale",Activator.CreateInstance(Native.T("UnityEngine.Vector3"),sx*FoggerArtAssets.DisplayScale,sy*FoggerArtAssets.DisplayScale,sz)!);
                Native.Set(root,"anchoredPosition",Activator.CreateInstance(Native.T("UnityEngine.Vector2"),x-bounds.X*sx*extra,y-bounds.Y*sy*extra)!);
            }
        }
        public static void CategoryRenderer(object __0,object __1,object __result)
        {
            if(!RecruitmentHooks.Registered||__0 is null||__1 is null||__result is null||(!RecruitmentCatalog.IsMenuId(RecruitmentHooks.Id(__0))&&!NativeFoggerPresentation.CustomForm(RecruitmentHooks.Id(__0))))return;
            try
            {
                var p=new Preview{Type=RecruitmentHooks.Id(__0),Renderer=__result,Player=__1,Game=Native.Static("GameManager","GameState")!,
                    Container=Native.Get(__result,"duplicatedSpritesContainer")!,Data=Native.Get(__result,"spriteDuplicationData")!,
                    Owner=Native.Int(Native.Get(__1,"Id")),Tribe=Tribe(__1),Skin=Skin(__1),Color=Native.Int(Native.Get(__1,"color"))};
                p.Destination=Native.Get(p.Container,"rectTransform")!;p.Children=Children(p.Destination);
                if(p.Children.Length==0||p.Game is null)throw new InvalidOperationException("Category unit native source is absent.");
                _=Bounds(p.Destination);
                bool ready=p.Fogger||UnitArtAssets.Prepare(p.Player);PaintPreview(p);PreviewReady(p,ready);
                PendingPreviews.Remove(Native.Pointer(p.Renderer));
                if(!ready&&PendingPreviews.Count<MaxPendingPreviews)PendingPreviews.Add(Native.Pointer(p.Renderer),p);
            }
            catch(Exception e){Note("mimic-category-art-error",e);}
        }
        public static void Tick()
        {
            foreach(var pair in PendingPreviews.ToArray())
            {
                var p=pair.Value;
                try
                {
                    if(++p.Age>MaxPendingTicks||!Current(p)){PendingPreviews.Remove(pair.Key);continue;}
                    if(!p.Fogger&&!UnitArtAssets.Prepare(p.Player))continue;
                    PaintPreview(p,true);PreviewReady(p,true);PendingPreviews.Remove(pair.Key);
                }
                catch(Exception e){PendingPreviews.Remove(pair.Key);Note("mimic-category-art-error",e);}
            }
        }

        public static void TechCategory(object __0,object __1)
        {
            try
            {
                if(__0 is null||__1 is null)return;
                if(RecruitmentCatalog.IsMenuId(RecruitmentHooks.Id(__1))||NativeFoggerPresentation.CustomForm(RecruitmentHooks.Id(__1)))
                {
                    Native.Set(__0,"Text",RecruitmentCatalog.Label(RecruitmentHooks.Id(__1)));
                    // Native SetUnitData already parents the category renderer via
                    // SetSpriteAsTransform and binds its unit-info click closure.
                    // Replacing the hidden Image sprite would leave that native
                    // transform visible and can reset its fitted size.
                }
                else if(HiddenTechIcons.Remove(Native.Pointer(__0),out var oldIcon))SetVisible(oldIcon,true);
            }
            catch(Exception e){Note("mimic-tech-control-error",e);}
        }
    }
}
