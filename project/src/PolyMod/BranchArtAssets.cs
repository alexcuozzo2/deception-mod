using System;
using System.Collections.Generic;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Immutable asset names and shared Sprite views. PNG pixels/textures
    // belong to UnitArtAssets; this provider owns only the normalized wrappers.
    internal static class BranchArtAssets
    {
        static readonly Dictionary<string,object> Views=new(StringComparer.Ordinal);
        internal static string? AssetName(string tech,bool unlock)=>tech switch
        {
            TechTree.PropagandaId=>"propaganda_mask",
            TechTree.CounterintelligenceId=>"fogger_fixed",
            TechTree.IntelligenceId=>"intelligence_icon",
            _=>null
        };
        internal static object Get(string tech,bool unlock)
        {
            string? name=AssetName(tech,unlock);
            if(name is null)return MimicControls.NativeSprite("UNKNOWN_ICON");
            var view=GetNamed(name);
            return view;
        }
        internal static object DisguiseTech()=>TechIconGlow.Get(GetNamed("disguise_mask"));
        internal static object Reconnaissance()=>ReconnaissanceArt.Get(GetNamed("intelligence_icon"));
        internal static object DisguiseAction()=>GetNamed("disguise_icon");
        static object GetNamed(string name)
        {
            if(Views.TryGetValue(name,out var cached))
            {
                // A borrowed source Sprite may be retired independently of its
                // texture. Its immutable, hash-pinned pixels and frame still
                // belong to this live view, including already-published widgets.
                if(Native.UnityAlive(cached)&&Native.UnityAlive(Native.Get(cached,"texture")))return cached;
                Views.Remove(name);
                if(Native.UnityAlive(cached))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,cached);
            }
            var source=UnitArtAssets.Get(name);var texture=Native.Get(source,"texture");
            if(!Native.UnityAlive(source)||!Native.UnityAlive(texture))throw new InvalidOperationException("Branch icon texture is absent: "+name);
            var frame=Native.Get(source,"rect")!;
            var reference=MimicControls.NativeSprite("UNKNOWN_ICON");var referenceFrame=Native.Get(reference,"rect")!;
            float width=Positive(Native.Get(frame,"width")),height=Positive(Native.Get(frame,"height"));
            float referenceSize=Math.Max(Positive(Native.Get(referenceFrame,"width")),Positive(Native.Get(referenceFrame,"height")));
            float ppu=Positive(Math.Max(width,height)*Positive(Native.Get(reference,"pixelsPerUnit"))/referenceSize);
            var pivot=Activator.CreateInstance(Native.T("UnityEngine.Vector2"),0.5f,0.5f)!;
            object? view=null;
            try
            {
                // Native Image.SetNativeSize and TechItem's sprite-vertex bounds
                // both honor PPU. Merely shrinking the RectTransform leaves the
                // latter huge for user-supplied multi-thousand-pixel PNGs.
                view=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),
                    null,texture,frame,pivot,ppu,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"));
                if(!Native.UnityAlive(view))throw new InvalidOperationException("Branch icon view creation failed: "+name);
                Views.Add(name,view!);return view!;
            }
            catch
            {
                if(Native.UnityAlive(view))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,view);
                throw;
            }
        }
        static float Positive(object? value)
        {
            float number=Convert.ToSingle(value,System.Globalization.CultureInfo.InvariantCulture);
            if(!float.IsFinite(number)||number<=0)throw new InvalidOperationException("Invalid native branch icon geometry.");
            return number;
        }
    }
}

namespace Deception.PolyModAdapter
{
    // Bounded, shared derivative of the Surveillance source. Its RGB inverse
    // receives the same diffuse white silhouette glow as the Disguise reward.
    // This cache owns its output separately: neither live icon can retire the
    // other one's texture. Source pixels and the Surveillance sprite are borrowed.
    internal static class ReconnaissanceArt
    {
        internal const int Edge=256;
        sealed record Cached(object Source,object SourceTexture,object Sprite,object Texture);
        static Cached? Current;static object? FailedSource;static bool Building;
        static void Destroy(object? o){if(Native.UnityAlive(o))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,o);}
        static float Positive(object? o){float n=Convert.ToSingle(o);if(!float.IsFinite(n)||n<=0)throw new InvalidOperationException("Invalid Reconnaissance icon geometry.");return n;}
        internal static object Get(object source)
        {
            if(!Native.UnityAlive(source))throw new InvalidOperationException("Reconnaissance icon source is absent.");
            if(Current is Cached old)
            {
                if(Native.Same(old.Source,source)&&Native.UnityAlive(old.SourceTexture)&&Native.UnityAlive(old.Sprite)&&Native.UnityAlive(old.Texture))return old.Sprite;
                Destroy(old.Sprite);Destroy(old.Texture);Current=null;
            }
            if(Building||Native.Same(FailedSource,source))return source;
            object? texture=null,sprite=null;Building=true;
            try
            {
                var input=Native.Get(source,"texture");if(!Native.UnityAlive(input))throw new InvalidOperationException("Reconnaissance source texture is absent.");
                var frame=Native.Get(source,"rect")!;float w=Positive(Native.Get(frame,"width")),h=Positive(Native.Get(frame,"height")),
                    tw=Positive(Native.Get(input!,"width")),th=Positive(Native.Get(input!,"height"));
                float x=Convert.ToSingle(Native.Get(frame,"x")),y=Convert.ToSingle(Native.Get(frame,"y"));
                if(!float.IsFinite(x+y)||x<0||y<0||x+w>tw||y+h>th)throw new InvalidOperationException("Reconnaissance source crop exceeds its texture.");
                var rgba=UnitArtOutline.ReadTextureCrop(input!,x/tw,y/th,w/tw,h/th);
                bool linear=Equals(Native.Static("UnityEngine.QualitySettings","activeColorSpace"),Enum.Parse(Native.T("UnityEngine.ColorSpace"),"Linear"));
                int innerWidth=Math.Max(1,(int)MathF.Round(TechIconGlow.ContentEdge*w/Math.Max(w,h))),
                    innerHeight=Math.Max(1,(int)MathF.Round(TechIconGlow.ContentEdge*h/Math.Max(w,h)));
                var pixels=Compose(rgba,Edge,Edge,innerWidth,innerHeight,linear);
                int width=innerWidth+2*TechIconGlow.Padding,height=innerHeight+2*TechIconGlow.Padding;
                texture=Activator.CreateInstance(Native.T("UnityEngine.Texture2D"),width,height,Enum.Parse(Native.T("UnityEngine.TextureFormat"),"RGBA32"),false)!;
                Native.Set(texture,"filterMode",Enum.Parse(Native.T("UnityEngine.FilterMode"),"Bilinear"));Native.Set(texture,"wrapMode",Enum.Parse(Native.T("UnityEngine.TextureWrapMode"),"Clamp"));
                var array=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
                Native.Invoke(Native.M("UnityEngine.Texture2D","LoadRawTextureData",array.FullName!),texture,Activator.CreateInstance(array,new object[]{pixels}));
                Native.Invoke(Native.M("UnityEngine.Texture2D","Apply","System.Boolean","System.Boolean"),texture,false,true);
                float ppu=Math.Max(width,height)*Positive(Native.Get(source,"pixelsPerUnit"))/Math.Max(w,h);
                sprite=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),null,
                    texture,Activator.CreateInstance(Native.T("UnityEngine.Rect"),0f,0f,(float)width,(float)height),Activator.CreateInstance(Native.T("UnityEngine.Vector2"),.5f,.5f),ppu,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"));
                if(!Native.UnityAlive(sprite))throw new InvalidOperationException("Reconnaissance Sprite was not created.");
                Current=new(source,input!,sprite!,texture);FailedSource=null;return sprite!;
            }
            catch(Exception e){Destroy(sprite);Destroy(texture);FailedSource=source;Entry.Note("reconnaissance-icon-unavailable",e.ToString());return source;}
            finally{Building=false;}
        }
        internal static byte[] Compose(byte[] rgba,int sourceWidth,int sourceHeight,int innerWidth,int innerHeight,bool linear=false)
        {
            // Invert performs the one required linear-to-sRGB conversion.
            // Compose adds transparent padding and an emanating silhouette glow;
            // a second color conversion would incorrectly wash out the artwork.
            var inverse=Invert(rgba,sourceWidth,sourceHeight,innerWidth,innerHeight,linear);
            return TechIconGlow.Compose(inverse,innerWidth,innerHeight,innerWidth,innerHeight);
        }
        internal static byte[] Invert(byte[] rgba,int sourceWidth,int sourceHeight,int width,int height,bool linear=false)
        {
            if(sourceWidth<1||sourceHeight<1||width<1||height<1||sourceWidth>Edge||sourceHeight>Edge||width>Edge||height>Edge||rgba.Length!=sourceWidth*sourceHeight*4)
                throw new ArgumentException("Invalid bounded Reconnaissance raster.");
            var output=new byte[width*height*4];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                float sx=Math.Clamp((x+.5f)*sourceWidth/width-.5f,0,sourceWidth-1),sy=Math.Clamp((y+.5f)*sourceHeight/height-.5f,0,sourceHeight-1);
                int x0=(int)sx,y0=(int)sy,x1=Math.Min(x0+1,sourceWidth-1),y1=Math.Min(y0+1,sourceHeight-1);float dx=sx-x0,dy=sy-y0;
                float Sample(int channel)
                {
                    float Pixel(int xx,int yy){int i=(yy*sourceWidth+xx)*4;return channel==3?rgba[i+3]/255f:rgba[i+channel]/255f*(rgba[i+3]/255f);}
                    return (Pixel(x0,y0)*(1-dx)+Pixel(x1,y0)*dx)*(1-dy)+(Pixel(x0,y1)*(1-dx)+Pixel(x1,y1)*dx)*dy;
                }
                float a=Sample(3);int to=(y*width+x)*4;output[to+3]=Byte(a);if(a<=0)continue;
                for(int c=0;c<3;c++)
                {
                    float value=Sample(c)/a;
                    // GPU readback can be linear; inversion is defined in the
                    // original visible/sRGB color space before output encoding.
                    if(linear)value=value<=.0031308f?value*12.92f:1.055f*MathF.Pow(value,1/2.4f)-.055f;
                    output[to+c]=Byte(1-value);
                }
            }
            return output;
        }
        static byte Byte(float n)=>(byte)Math.Clamp((int)MathF.Round(n*255),0,255);
    }
}
