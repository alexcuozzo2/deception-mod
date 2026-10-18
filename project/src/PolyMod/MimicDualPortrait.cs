using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Bottom-left copies only. Equal-height identities exchange two fixed slots;
    // the focused portrait is full-color at the lower-right and drawn last.
    // No source renderer, borrowed Sprite or shared art is changed.
    internal static class MimicDualPortrait
    {
        const float HeightFraction=0.68f, CombinedWidthFraction=0.88f, GapFraction=0.06f;
        const float UnfocusedAlpha=0.55f, VerticalStepFraction=0.08f;
        readonly record struct Point(float X,float Y);
        readonly record struct Box(float Left,float Bottom,float Right,float Top)
        {
            public float Width=>Positive(Right-Left);
            public float Height=>Positive(Top-Bottom);
            public float X=>(Left+Right)/2f;
            public float Y=>(Bottom+Top)/2f;
            public Box Include(Box b)=>new(Math.Min(Left,b.Left),Math.Min(Bottom,b.Bottom),Math.Max(Right,b.Right),Math.Max(Top,b.Top));
        }
        sealed record Part(object Rect,object Image,object Sprite,Point Position,float XScale,float YScale,float ZScale,object Color);

        // Selected-unit panels fit the visible composed silhouette, not the
        // nominal 1000-unit canvas (Spy bodies extend beyond that canvas).
        internal static (float Fit,float X,float Y) FitSingle(IReadOnlyList<UnitArtAssets.Layer> layers,float width,float height,float fraction,float yOffset)
        {
            var bounds=ActualBounds(layers);
            float fit=Positive(Math.Min(Positive(width)*Positive(fraction)/bounds.Width,
                Positive(height-2*Math.Abs(yOffset))*fraction/bounds.Height));
            return (fit,-bounds.X*fit,yOffset-bounds.Y*fit);
        }

        public static void Validate()
        {
            foreach(var method in new[]{Native.M("UnityEngine.GameObject","GetComponent","Il2CppSystem.Type"),
                Native.M("UnityEngine.UI.Image","get_sprite"),Native.M("UnityEngine.UI.Graphic","get_color"),
                Native.M("UnityEngine.UI.Graphic","set_color","UnityEngine.Color"),
                Native.M("UnityEngine.UI.Graphic","get_material"),Native.M("UnityEngine.Material","get_shader"),Native.M("UnityEngine.Object","get_name"),
                Native.M("UIUtils","GetUIUnitRenderer","Polytopia.Data.UnitData","PlayerState","PolytopiaBackendBase.Common.SkinType"),
                Native.M("UIUnitRenderer","get_duplicatedSpritesContainer"),Native.M("UIUnitRenderer","get_unitType"),
                Native.M("PlayerState","set_tribe","PolytopiaBackendBase.Common.TribeType"),Native.M("PlayerState","set_tribeMix","PolytopiaBackendBase.Common.TribeType"),
                Native.M("PlayerState","set_color","System.Int32"),
                Native.M("UnityEngine.Transform","get_localScale"),Native.M("UnityEngine.Transform","set_localScale","UnityEngine.Vector3"),
                Native.M("UnityEngine.Transform","SetAsFirstSibling"),Native.M("UnityEngine.Transform","SetAsLastSibling"),
                Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),
                Native.M("UnityEngine.RectTransform","get_anchoredPosition"),Native.M("UnityEngine.RectTransform","set_anchoredPosition","UnityEngine.Vector2"),
                Native.M("UnityEngine.Rect","get_x"),Native.M("UnityEngine.Rect","get_y"),Native.M("UnityEngine.Sprite","get_vertices"),
                Native.M("UnityEngine.Sprite","get_pivot"),Native.M("UnityEngine.Sprite","get_pixelsPerUnit"),Native.M("UnityEngine.Color","get_Item","System.Int32"),
                Native.M("UnityEngine.Vector2","get_Item","System.Int32"),Native.M("UnityEngine.Vector3","get_Item","System.Int32")}){_=method;}
            _=Native.T("UnityEngine.Vector2").GetConstructor(new[]{typeof(float),typeof(float)})??throw new MissingMethodException("Vector2(Single,Single)");
            _=Native.T("UnityEngine.Vector3").GetConstructor(new[]{typeof(float),typeof(float),typeof(float)})??throw new MissingMethodException("Vector3(Single,Single,Single)");
        }

        sealed record Preview(object Renderer,object Root,List<Part> Parts,Box Bounds);
        static Preview ReadPreview(object data,object player,object skin,string nativeId,object destination,float opacity)
        {
            object? renderer=null;
            try
            {
                renderer=Native.Invoke(Native.M("UIUtils","GetUIUnitRenderer","Polytopia.Data.UnitData","PlayerState","PolytopiaBackendBase.Common.SkinType"),null,data,player,skin);
                if(!Native.UnityAlive(renderer)||Native.EnumName(Native.Get(renderer!,"unitType")!)!=nativeId)
                    throw new InvalidOperationException("Native portrait preview has the wrong unit type.");
                var container=Native.Get(renderer!,"duplicatedSpritesContainer");
                if(!Native.UnityAlive(container)||Native.Same(container,destination))throw new InvalidOperationException("Native preview does not own its image container.");
                var root=Native.Get(container!,"rectTransform")!;
                if(!(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),root,Native.Get(renderer!,"transform"))!)
                    throw new InvalidOperationException("Native preview container belongs to another renderer.");
                int count=Native.Int(Native.Get(root,"childCount"));
                if(count<=0||count>128)throw new InvalidOperationException("Native portrait copied part count is invalid.");
                var parts=new List<Part>();Box? bounds=null;
                var imageType=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.UI.Image"))!;
                for(int i=0;i<count;i++)
                {
                    var child=Native.Invoke(Native.M("UnityEngine.Transform","GetChild","System.Int32"),root,i)!;
                    if(!Native.Same(Native.Get(child,"parent"),root))throw new InvalidOperationException("Native copied UI part changed parent.");
                    var rect=Native.As(child,"UnityEngine.RectTransform");
                    var image=Native.Invoke(Native.M("UnityEngine.GameObject","GetComponent","Il2CppSystem.Type"),Native.Get(child,"gameObject"),imageType);
                    if(!Native.UnityAlive(image))throw new InvalidOperationException("Native copied part is not an Image.");
                    image=Native.As(image!,"UnityEngine.UI.Image");var sprite=Native.Get(image,"sprite");
                    if(!Native.UnityAlive(sprite))throw new InvalidOperationException("Native preview image has no sprite.");
                    var position=PointOf(Native.Get(rect,"anchoredPosition")!);var scale=Native.Get(child,"localScale")!;
                    float sx=Axis3(scale,0),sy=Axis3(scale,1),sz=Axis3(scale,2);
                    var r=Native.Get(rect,"rect")!;
                    var b=Project(MeshBounds(sprite!),Finite(Native.Get(r,"x")),Finite(Native.Get(r,"y")),
                        Positive(Native.Get(r,"width")),Positive(Native.Get(r,"height")),position.X,position.Y,sx,sy);
                    bounds=bounds is Box old?old.Include(b):b;
                    parts.Add(new(rect,image,sprite!,position,sx,sy,sz,WithAlpha(Native.Get(image,"color")!,FocusAlpha(image,opacity))));
                }
                return new(renderer!,root,parts,bounds??throw new InvalidOperationException("Native portrait has no visible bounds."));
            }
            catch{DisposePreview(renderer);throw;}
        }
        static void DisposePreview(object? preview)
        {
            if(!Native.UnityAlive(preview))return;
            var go=Native.Get(preview!,"gameObject")!;
            try{Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,false);}
            finally{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,go);}
        }
        public static void Apply(object container,object data,bool actual,object player,object unit,MimicProjection projection)
        {
            var source=Native.Get(data,"source");RequireSource(container,data,source,unit,player);
            var destination=Native.Get(container,"rectTransform")??throw new InvalidOperationException("Native portrait container is absent.");
            var area=Native.Get(destination,"rect")!;
            float width=Positive(Native.Get(area,"width")),height=Positive(Native.Get(area,"height"));
            float originX=Finite(Native.Get(area,"x"))+width/2,originY=Finite(Native.Get(area,"y"))+height/2;
            var artPlayer=NativeRogueVisuals.ArtOwner(projection.State,Native.Get(unit,"UnitState")!,player);
            var layers=projection.ActualIsMimic||NativeFoggerPresentation.CustomActual(projection)?MimicInspectionControls.ActualLayers(artPlayer,projection.ActualMimicBoat,NativeSpyPresentation.IsSpy(projection),projection.ActualAmphibiousMimic||projection.ActualNativeId==Deception.Core.SpyCatalog.AquarionId,fogger:NativeFoggerPresentation.IsFogger(projection)):null;
            if(Native.EnumName(Native.Get(projection.ApparentData,"type")!)!=projection.NativeId)
                throw new InvalidOperationException("Disguise preview does not match its genuine native form.");
            var previewPlayer=player;var skin=Native.Invoke(Native.M("PlayerState","GetVisualSkin"),player)!;
            if(projection.Appearance is LedgerAppearance appearance)
            {
                const string tribe="PolytopiaBackendBase.Common.TribeType";
                var previewTribe=CosmeticMember(tribe,appearance.TribeVisual.Tribe);
                skin=CosmeticMember("PolytopiaBackendBase.Common.SkinType",appearance.TribeVisual.Skin);
                previewPlayer=Native.DetachedClone(player,"PlayerState");
                if(Native.Same(previewPlayer,player))throw new InvalidOperationException("Preview player aliases live owner.");
                Native.Set(previewPlayer,"tribe",previewTribe);Native.Set(previewPlayer,"tribeMix",Enum.ToObject(Native.T(tribe),0));
                Native.Set(previewPlayer,"color",appearance.SourceColor);Native.Set(previewPlayer,"skinType",skin);
            }
            var disguiseLayers=projection.ApparentMimic||NativeFoggerPresentation.CustomForm(projection.NativeId)?MimicInspectionControls.ActualLayers(previewPlayer,projection.ApparentMimicBoat,Deception.Core.SpyCatalog.IsSpyId(projection.NativeId),projection.NativeId==Deception.Core.AquarionCatalog.MimicId||projection.Appearance?.Passenger?.Form==Deception.Core.AquarionCatalog.MimicId||projection.NativeId==Deception.Core.SpyCatalog.AquarionId,fogger:NativeFoggerPresentation.IsForm(projection.NativeId)):null;
            Preview? disguise=null,truth=null;bool published=false;
            try
            {
                if(disguiseLayers is null)disguise=ReadPreview(projection.ApparentData,previewPlayer,skin,projection.NativeId,container,actual?UnfocusedAlpha:1f);
                if(layers is null)
                    truth=ReadPreview(projection.ActualData,artPlayer,Native.Invoke(Native.M("PlayerState","GetVisualSkin"),artPlayer)!,
                        projection.ActualNativeId,container,actual?1f:UnfocusedAlpha);
                var actualBounds=truth?.Bounds??ActualBounds(layers!);var disguiseBounds=disguise?.Bounds??ActualBounds(disguiseLayers!);
                float commonHeight=Positive(Math.Min(height*HeightFraction,
                    width*CombinedWidthFraction/(actualBounds.Width/actualBounds.Height+disguiseBounds.Width/disguiseBounds.Height)));
                // Native pair normalization would otherwise cancel the aircraft size adjustment.
                float actualFit=Positive(commonHeight/actualBounds.Height)*(NativeFoggerPresentation.IsFogger(projection)?FoggerArtAssets.DisplayScale:1),
                    disguiseFit=Positive(commonHeight/disguiseBounds.Height)*(NativeFoggerPresentation.IsForm(projection.NativeId)?FoggerArtAssets.DisplayScale:1);
                float actualWidth=actualBounds.Width*actualFit,disguiseWidth=disguiseBounds.Width*disguiseFit,gap=width*GapFraction;
                float direction=actual?-1f:1f;
                float actualX=originX-direction*(disguiseWidth+gap)/2,disguiseX=originX+direction*(actualWidth+gap)/2;
                float step=height*VerticalStepFraction,actualY=originY+direction*step/2,disguiseY=originY-direction*step/2;
                RequireSource(container,data,source,unit,player);
                foreach(var preview in new[]{disguise,truth}.Where(x=>x is not null))foreach(var part in preview!.Parts)
                    if(!Native.UnityAlive(part.Image)||!Native.Same(Native.Get(part.Rect,"parent"),preview.Root)||!Native.Same(Native.Get(part.Image,"sprite"),part.Sprite))
                        throw new InvalidOperationException("Native preview changed before publication.");
                Native.Invoke(Native.M("UIDuplicatedSprites","Clear"),container);published=true;
                void Transfer(Preview preview,float fit,float x,float y)
                {
                    foreach(var part in preview.Parts)
                    {
                        Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),part.Rect,destination,false);
                        Native.Set(part.Rect,"anchoredPosition",Vector2((part.Position.X-preview.Bounds.X)*fit+x,(part.Position.Y-preview.Bounds.Y)*fit+y));
                        Native.Set(part.Rect,"localScale",Activator.CreateInstance(Native.T("UnityEngine.Vector3"),part.XScale*fit,part.YScale*fit,part.ZScale*fit)!);
                        Native.Set(part.Image,"color",part.Color);Native.Set(part.Image,"raycastTarget",false);
                    }
                }
                if(disguise is not null)Transfer(disguise,disguiseFit,disguiseX,disguiseY);
                else MimicInspectionControls.AppendActualLayers(destination,disguiseLayers!.Select(l=>l with{Color=WithAlpha(l.Color,actual?UnfocusedAlpha:1f)}).ToArray(),
                    disguiseFit,disguiseX-disguiseBounds.X*disguiseFit,disguiseY-disguiseBounds.Y*disguiseFit);
                object[] actualChildren;
                if(truth is not null){Transfer(truth,actualFit,actualX,actualY);actualChildren=truth.Parts.Select(x=>x.Rect).ToArray();}
                else
                {
                    var focused=layers!.Select(l=>l with{Color=WithAlpha(l.Color,actual?1f:UnfocusedAlpha)}).ToArray();
                    actualChildren=MimicInspectionControls.AppendActualLayers(destination,focused,actualFit,actualX-actualBounds.X*actualFit,actualY-actualBounds.Y*actualFit);
                }
                if(!actual)for(int i=actualChildren.Length-1;i>=0;i--)Native.Invoke(Native.M("UnityEngine.Transform","SetAsFirstSibling"),actualChildren[i]);
                RequireSource(container,data,source,unit,player);
            }
            catch{if(published)MimicInspectionControls.RestorePortrait(data);throw;}
            finally{try{DisposePreview(disguise?.Renderer);}finally{DisposePreview(truth?.Renderer);}}
        }

        static object CosmeticMember(string typeName,string member)
        {
            var type=Native.T(typeName);
            // Exact recorded names only: numbers, content IDs and unknown
            // cosmetics must fail before allocating a native preview.
            if(!type.IsEnum||string.IsNullOrEmpty(member)||!Enum.IsDefined(type,member))
                throw new InvalidOperationException("Unknown native cosmetic member "+typeName+"."+member);
            return Enum.Parse(type,member,false);
        }

        static float FocusAlpha(object image,float opacity)
        {
            // The native sprite shader packs saturation in the high alpha bit.
            // UI focus should use full color and an independent visible opacity.
            // Linear UI shaders (including actual PNG layers) use plain alpha.
            var material=Native.Get(image,"material");
            var shader=Native.UnityAlive(material)?Native.Get(material!,"shader"):null;
            if(Native.UnityAlive(shader)&&(string?)Native.Get(shader!,"name")=="Polytopia/Sprite")
                return (128f+(float)Math.Round(127f*opacity))/255f;
            return opacity;
        }

        static void RequireSource(object container,object data,object? source,object unit,object player)
        {
            if(!Native.UnityAlive(container)||!Native.Same(Native.Get(data,"container"),container)||!Native.UnityAlive(source)||
                !Native.Same(Native.Get(data,"source"),source)||!Native.UnityAlive(unit)||!Native.Same(Native.Get(unit,"Owner"),player)||
                !(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),source,Native.Get(unit,"transform"))!)
                throw new InvalidOperationException("Dual portrait source/unit/container ownership is invalid.");
        }
        static Box ActualBounds(IReadOnlyList<UnitArtAssets.Layer> layers)
        {
            // Alpha>=16 bounds measured once from the exact hash-validated PNGs.
            // Avoid synchronous texture readback or a 15M-pixel scan on a toggle.
            Box Known(int index,string name,float width,float height,float left,float bottom,float right,float top)
            {
                var sprite=layers[index].Sprite;var rect=Native.Get(sprite,"rect")!;
                if(!Native.Same(sprite,UnitArtAssets.Get(name))||Positive(Native.Get(rect,"width"))!=width||Positive(Native.Get(rect,"height"))!=height)
                    throw new InvalidOperationException("Actual portrait artwork does not match its measured frame.");
                return new(left/width,bottom/height,right/width,top/height);
            }
            Box[] visible;
            if(layers.Count==1)visible=layers[0].AssetName=="spy_icon"?new[]{Known(0,"spy_icon",867,649,0,0,867,649)}:new[]{Known(0,"mimic_icon",494,525,5,5,488,520)};
            else if(layers.Count==2&&layers[0].AssetName=="fogger_fixed")visible=new[]{new Box(0,0,1,1),new Box(0,0,1,1)};
            else if(layers.Count==2)visible=new[]{Known(0,"mimic_boat_fixed",1024,1024,146,313,815,762),
                Known(1,"mimic_boat_team",1024,1024,584,446,707,619)};
            else if(layers.Count==3&&layers[0].AssetName=="mermimic_body_team")visible=new[]{Known(0,"mermimic_body_team",3870,3870,245,967,2792,2514),MeshBounds(layers[1].Sprite),Known(2,"mimic_mask",590,790,8,10,584,785)};
            else if(layers.Count==3&&layers[0].AssetName=="spymaid_fixed")visible=new[]{Known(0,"spymaid_fixed",1024,1024,309,312,678,659),Known(1,"spymaid_team",1024,1024,114,242,679,713),MeshBounds(layers[2].Sprite)};
            else if(layers.Count==3&&layers[0].AssetName=="spy-fixed")visible=new[]{Known(0,"spy-fixed",1024,1024,293,227,662,673),Known(1,"spy-team",1024,1024,388,430,625,727),MeshBounds(layers[2].Sprite)};
            else if(layers.Count==4)visible=new[]{Known(0,"mimic-body-fixed",1024,1024,204,142,812,556),
                Known(1,"mimic-body-team",3870,3870,633,1160,3101,3004),MeshBounds(layers[2].Sprite),
                Known(3,"mimic_mask",590,790,8,10,584,785)};
            else throw new InvalidOperationException("Unsupported actual portrait composition.");
            Box? result=null;
            for(int i=0;i<layers.Count;i++)
            {
                var l=layers[i];var b=Project(visible[i],-l.Width/2,-l.Height/2,l.Width,l.Height,l.X,l.Y,l.MirrorX?-1:1,1);
                result=result is Box old?old.Include(b):b;
            }
            return result!.Value;
        }
        static Box MeshBounds(object sprite)
        {
            // Images map the original sprite rectangle, not the potentially tight
            // Sprite.bounds. Convert mesh vertices back to source pixel space.
            var rect=Native.Get(sprite,"rect")!;var pivot=PointOf(Native.Get(sprite,"pivot")!);
            float w=Positive(Native.Get(rect,"width")),h=Positive(Native.Get(rect,"height")),ppu=Positive(Native.Get(sprite,"pixelsPerUnit"));
            var vertices=Native.Get(sprite,"vertices")!;int count=Native.Int(Native.Get(vertices,"Count"));
            if(count<3||count>512)throw new InvalidOperationException("Native portrait sprite mesh count is invalid.");
            float left=1,bottom=1,right=0,top=0;int seen=0;
            foreach(var vertex in Native.Items(vertices))
            {
                var v=PointOf(vertex);float nx=Finite((v.X*ppu+pivot.X)/w),ny=Finite((v.Y*ppu+pivot.Y)/h);
                if(nx < -0.001f||nx > 1.001f||ny < -0.001f||ny > 1.001f)
                    throw new InvalidOperationException("Native portrait sprite mesh exceeds its source rectangle.");
                left=Math.Min(left,nx);bottom=Math.Min(bottom,ny);right=Math.Max(right,nx);top=Math.Max(top,ny);seen++;
            }
            if(seen!=count)throw new InvalidOperationException("Native portrait sprite mesh changed during read.");
            var result=new Box(left,bottom,right,top);_=result.Width;_=result.Height;return result;
        }
        static Box Project(Box normalized,float x,float y,float width,float height,float px,float py,float sx,float sy)
        {
            float x0=Finite(px+(x+normalized.Left*width)*sx),x1=Finite(px+(x+normalized.Right*width)*sx);
            float y0=Finite(py+(y+normalized.Bottom*height)*sy),y1=Finite(py+(y+normalized.Top*height)*sy);
            return new(Math.Min(x0,x1),Math.Min(y0,y1),Math.Max(x0,x1),Math.Max(y0,y1));
        }
        static object WithAlpha(object color,float alpha)
        {
            float Channel(int n)=>Finite(Native.Invoke(Native.M("UnityEngine.Color","get_Item","System.Int32"),color,n));
            return Activator.CreateInstance(Native.T("UnityEngine.Color"),Channel(0),Channel(1),Channel(2),alpha)!;
        }
        static object Vector2(float x,float y)=>Activator.CreateInstance(Native.T("UnityEngine.Vector2"),x,y)!;
        static Point PointOf(object vector)=>new(Finite(Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),vector,0)),
            Finite(Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),vector,1)));
        static float Axis3(object vector,int axis)=>Finite(Native.Invoke(Native.M("UnityEngine.Vector3","get_Item","System.Int32"),vector,axis));
        static float Finite(object? value)
        {float n=Convert.ToSingle(value,System.Globalization.CultureInfo.InvariantCulture);if(!float.IsFinite(n))throw new InvalidOperationException("Native dual portrait geometry is not finite.");return n;}
        static float Positive(object? value)
        {float n=Finite(value);if(n<=0)throw new InvalidOperationException("Native dual portrait dimensions must be positive.");return n;}
    }
}
