using System;

namespace Deception.PolyModAdapter
{
    // One runtime composite for Camouflage's supplied mask. The original PNG,
    // its Sprite and the separate flat Disguise action artwork stay untouched.
    // A single Sprite also lets native tree/button bounds account for the glow
    // without accidentally fitting a 64-pixel padded child as another icon.
    internal static class TechIconGlow
    {
        internal const int ReadSize=256,ContentEdge=192,Padding=32;
        internal const float GlowSigma=9f,GlowRadius=28f;
        internal const byte EdgeAlpha=180;
        sealed record Cached(object Source,object SourceTexture,object Sprite,object Texture);
        static Cached? Current;
        static object? FailedSource;
        static bool Building;
        static void Destroy(object? value)
        {if(Native.UnityAlive(value))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,value);}
        static float Number(object? value)
        {float n=Convert.ToSingle(value);if(!float.IsFinite(n)||n<=0)throw new InvalidOperationException("Invalid tech glow dimensions.");return n;}
        static object V(float x,float y)=>Activator.CreateInstance(Native.T("UnityEngine.Vector2"),x,y)!;
        internal static object Get(object source)
        {
            if(!Native.UnityAlive(source))throw new InvalidOperationException("Camouflage glow source is absent.");
            if(Current is Cached old)
            {
                if(Native.Same(old.Source,source)&&Native.UnityAlive(old.SourceTexture)&&Native.UnityAlive(old.Sprite)&&Native.UnityAlive(old.Texture))return old.Sprite;
                // Do not accumulate replacement textures after device loss.
                Destroy(old.Sprite);Destroy(old.Texture);Current=null;
            }
            // The decorative effect must not interrupt a native research
            // reaction. A failed source keeps its original mask, with no retry
            // or log on ordinary menu refreshes.
            if(Building||Native.Same(FailedSource,source))return source;
            object? output=null,sprite=null;
            Building=true;
            try
            {
                var texture=Native.Get(source,"texture");if(!Native.UnityAlive(texture))throw new InvalidOperationException("Camouflage source texture is absent.");
                var frame=Native.Get(source,"rect")!;
                float width=Number(Native.Get(frame,"width")),height=Number(Native.Get(frame,"height"));
                float tw=Number(Native.Get(texture!,"width")),th=Number(Native.Get(texture!,"height"));
                float x=Convert.ToSingle(Native.Get(frame,"x")),y=Convert.ToSingle(Native.Get(frame,"y"));
                if(!float.IsFinite(x+y)||x<0||y<0||x+width>tw||y+height>th)throw new InvalidOperationException("Camouflage source crop exceeds its texture.");
                var rgba=UnitArtOutline.ReadTextureCrop(texture!,x/tw,y/th,width/tw,height/th);
                bool linear=Equals(Native.Static("UnityEngine.QualitySettings","activeColorSpace"),Enum.Parse(Native.T("UnityEngine.ColorSpace"),"Linear"));
                int innerWidth=Math.Max(1,(int)MathF.Round(ContentEdge*width/Math.Max(width,height)));
                int innerHeight=Math.Max(1,(int)MathF.Round(ContentEdge*height/Math.Max(width,height)));
                var pixels=Compose(rgba,ReadSize,ReadSize,innerWidth,innerHeight,linear);
                int w=innerWidth+Padding*2,h=innerHeight+Padding*2;
                output=Activator.CreateInstance(Native.T("UnityEngine.Texture2D"),w,h,Enum.Parse(Native.T("UnityEngine.TextureFormat"),"RGBA32"),false)!;
                Native.Set(output,"filterMode",Enum.Parse(Native.T("UnityEngine.FilterMode"),"Bilinear"));
                Native.Set(output,"wrapMode",Enum.Parse(Native.T("UnityEngine.TextureWrapMode"),"Clamp"));
                var bytesType=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
                var bytes=Activator.CreateInstance(bytesType,new object[]{pixels})!;
                Native.Invoke(Native.M("UnityEngine.Texture2D","LoadRawTextureData",bytesType.FullName!),output,bytes);
                Native.Invoke(Native.M("UnityEngine.Texture2D","Apply","System.Boolean","System.Boolean"),output,false,true);
                // Preserve the existing icon's maximum native UI bound. The
                // diffuse glow shares that budget rather than enlarging buttons.
                float ppu=Math.Max(w,h)*Number(Native.Get(source,"pixelsPerUnit"))/Math.Max(width,height);
                sprite=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),null,
                    output,Activator.CreateInstance(Native.T("UnityEngine.Rect"),0f,0f,(float)w,(float)h),V(.5f,.5f),ppu,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"));
                if(!Native.UnityAlive(sprite))throw new InvalidOperationException("Camouflage glow Sprite creation failed.");
                Current=new(source,texture!,sprite!,output);FailedSource=null;return sprite!;
            }
            catch(Exception e)
            {
                FailedSource=source;Destroy(sprite);Destroy(output);
                Entry.Note("tech-mask-glow-unavailable",e.ToString());return source;
            }
            finally{Building=false;}
        }
        // Premultiplied bilinear sampling prevents transparent white pixels in
        // an artist's canvas from creating a white rectangle around black art.
        internal static byte[] Compose(byte[] source,int sourceWidth,int sourceHeight,int innerWidth,int innerHeight,bool linear=false)
        {
            if(sourceWidth<1||sourceHeight<1||sourceWidth>ReadSize||sourceHeight>ReadSize||source.Length!=sourceWidth*sourceHeight*4||
                innerWidth<1||innerHeight<1||innerWidth>ContentEdge||innerHeight>ContentEdge)
                throw new ArgumentException("Invalid bounded tech glow raster.");
            int width=innerWidth+2*Padding,height=innerHeight+2*Padding;
            var rgba=new byte[width*height*4];var alpha=new byte[width*height];
            for(int y=0;y<innerHeight;y++)for(int x=0;x<innerWidth;x++)
            {
                float sx=Math.Clamp((x+.5f)*sourceWidth/innerWidth-.5f,0,sourceWidth-1),sy=Math.Clamp((y+.5f)*sourceHeight/innerHeight-.5f,0,sourceHeight-1);
                int x0=(int)sx,y0=(int)sy,x1=Math.Min(x0+1,sourceWidth-1),y1=Math.Min(y0+1,sourceHeight-1);
                float dx=sx-x0,dy=sy-y0;
                float Sample(int channel)
                {
                    float Pixel(int xx,int yy)
                    {int i=(yy*sourceWidth+xx)*4;return channel==3?source[i+3]/255f:source[i+channel]/255f*(source[i+3]/255f);}
                    return (Pixel(x0,y0)*(1-dx)+Pixel(x1,y0)*dx)*(1-dy)+(Pixel(x0,y1)*(1-dx)+Pixel(x1,y1)*dx)*dy;
                }
                int at=(y+Padding)*width+x+Padding;float a=Sample(3);alpha[at]=Byte(a);rgba[at*4+3]=Byte(a);
                if(a<=0)continue;
                for(int channel=0;channel<3;channel++)
                {
                    float color=Sample(channel)/a;
                    if(linear)color=color<=.0031308f?color*12.92f:1.055f*MathF.Pow(color,1f/2.4f)-.055f;
                    rgba[at*4+channel]=Byte(color);
                }
            }
            var distance=UnitArtOutline.DistanceSquared(alpha,width,height);int absent=width*width+height*height+1;bool nonempty=false;
            double tail=Math.Exp(-.5*GlowRadius*GlowRadius/(GlowSigma*GlowSigma));
            for(int i=0;i<alpha.Length;i++)
            {
                nonempty|=alpha[i]>=UnitArtOutline.CoverageThreshold;
                if(alpha[i]>=UnitArtOutline.CoverageThreshold||distance[i]>=absent)continue;
                // A broad Gaussian falloff emanates from the actual boundary.
                // There is no constant-brightness band that reads as a stroke.
                // Subtract the far-tail value so support ends at a transparent
                // boundary inside the padded frame, with no clipped rectangle.
                double d=Math.Max(0,Math.Sqrt(distance[i])-.5);
                double falloff=d>=GlowRadius?0:Math.Clamp((Math.Exp(-.5*d*d/(GlowSigma*GlowSigma))-tail)/(1-tail),0,1);
                float glow=(float)(EdgeAlpha/255d*falloff),a=alpha[i]/255f,total=a+glow*(1-a);
                if(total<=0)continue;
                for(int channel=0;channel<3;channel++)rgba[i*4+channel]=Byte((rgba[i*4+channel]/255f*a+glow*(1-a))/total);
                rgba[i*4+3]=Byte(total);
            }
            if(!nonempty)throw new InvalidOperationException("Camouflage source has no solid mask alpha.");
            return rgba;
        }
        static byte Byte(float value)=>(byte)Math.Clamp((int)MathF.Round(value*255),0,255);
    }
}
