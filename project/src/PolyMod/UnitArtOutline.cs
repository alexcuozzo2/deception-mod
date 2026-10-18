using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Globalization;

namespace Deception.PolyModAdapter
{
    // Owned, bounded alpha-outline assets. Does not alter source textures,
    // Sprite meshes, shared materials, native units, or their renderers.
    internal static class UnitArtOutline
    {
        const int ReadSize=256,MaxEdge=256,MaxVariants=128,MaxVertices=512,MaxIndices=1536;
        internal const int Padding=64,BandRadius=4,TailRadius=16;
        internal const byte CoverageThreshold=128,EdgeAlpha=180;
        sealed class Trace
        {
            public string Stage="source",Label="",Name="?",Texture="?",Rect="?",Bounds="?",Packed="?";
            public long Pointer;
            public int Vertices=-1,Uv=-1,Indices=-1;
            public readonly Stopwatch Timer=Stopwatch.StartNew();
            public string Summary()=>"source="+Pointer+" label="+Label+" name="+Name+" texture="+Texture+" rect="+Rect+
                " packed="+Packed+" bounds="+Bounds+" vertices="+Vertices+" uv="+Uv+" indices="+Indices+
                " stage="+Stage+" elapsedMs="+Timer.Elapsed.TotalMilliseconds.ToString("F1",CultureInfo.InvariantCulture);
        }
        static string Brief(object? value)
        {
            string text=Convert.ToString(value,CultureInfo.InvariantCulture)??"?";
            text=text.Replace('\n',' ').Replace('\r',' ');return text.Length>160?text.Substring(0,160):text;
        }
        static string ReadDiagnostic(Func<object?> value)
        {try{return Brief(value());}catch(Exception e){return "unavailable("+e.GetType().Name+")";}}
        internal readonly record struct V2(float X,float Y);
        internal sealed record Result(object Sprite,float WidthScale,float HeightScale);
        sealed record Cached(object Source,object Texture,Result Outline);
        static readonly Dictionary<long,Cached> Cache=new();
        static readonly HashSet<long> Failed=new();
        static bool Building;

        // Returned Sprite is borrowed from this process cache. The caller owns
        // only its renderer and must not destroy the Sprite or its texture.
        internal static Result? Get(object source,string? label=null)
        {
            if(!Native.UnityAlive(source))return null;
            long key=Native.Pointer(source);
            if(Cache.TryGetValue(key,out var old))
            {
                if(Native.Same(old.Source,source)&&Native.UnityAlive(old.Outline.Sprite))return old.Outline;
                // An unexpected dead cached resource cannot be reused; do not
                // destroy a possibly reassigned/borrowed resource here.
                return null;
            }
            if(Building||Failed.Contains(key)||Cache.Count+Failed.Count>=MaxVariants)return null;
            Building=true;var trace=new Trace{Pointer=key,Label=Brief(label)};
            try
            {
                var made=Build(source,trace);Cache.Add(key,made);
                trace.Stage="complete";Entry.Note("unit-art-outline-ready",trace.Summary()+" coverage="+CoverageThreshold+" band="+BandRadius+" tail="+TailRadius+" edgeAlpha="+EdgeAlpha+" padding="+Padding);
                return made.Outline;
            }
            catch(Exception e)
            {
                Failed.Add(key);Entry.Note("unit-art-outline-failed",trace.Summary()+"\n"+e);return null;
            }
            finally{Building=false;}
        }
        static V2 Point(object value)
        {
            string owner=value.GetType().FullName!;
            float Axis(int index)=>Number(Native.Invoke(Native.M(owner,"get_Item","System.Int32"),value,index));
            return new V2(Axis(0),Axis(1));
        }
        static float Number(object? v)
        {float f=Convert.ToSingle(v);if(!float.IsFinite(f))throw new InvalidOperationException("Nonfinite outline geometry.");return f;}
        static object V(float x,float y)=>Activator.CreateInstance(Native.T("UnityEngine.Vector2"),x,y)!;
        static object Rect(float x,float y,float w,float h)=>Activator.CreateInstance(Native.T("UnityEngine.Rect"),x,y,w,h)!;
        static object ByteArray(byte[] bytes)=>Activator.CreateInstance(
            Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte)),new object[]{bytes})!;
        static string BytesType=>Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte)).FullName!;
        static object NewTexture(int width,int height)=>Activator.CreateInstance(Native.T("UnityEngine.Texture2D"),width,height,
            Enum.Parse(Native.T("UnityEngine.TextureFormat"),"RGBA32"),false)!;
        static void Destroy(object? o)
        {if(Native.UnityAlive(o))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,o);}

        static Cached Build(object source,Trace trace)
        {
            trace.Name=ReadDiagnostic(()=>Native.Get(source,"name"));
            trace.Packed=ReadDiagnostic(()=>Native.Get(source,"packed"));
            trace.Rect=ReadDiagnostic(()=>Native.Get(source,"rect"));
            var texture=Native.Get(source,"texture");
            if(!Native.UnityAlive(texture))throw new InvalidOperationException("Outline source texture is missing.");
            trace.Texture=ReadDiagnostic(()=>Native.Get(texture!,"width"))+"x"+ReadDiagnostic(()=>Native.Get(texture!,"height"));
            // A separate alpha texture requires another sampling path. Fail
            // explicitly instead of creating a solid atlas rectangle.
            if(Native.UnityAlive(Native.Get(source,"associatedAlphaSplitTexture")))
                throw new InvalidOperationException("Split-alpha sprite outline is not supported.");
            trace.Stage="mesh-read";
            var vertexArray=Native.Get(source,"vertices")!;var uvArray=Native.Get(source,"uv")!;var indexArray=Native.Get(source,"triangles")!;
            trace.Vertices=Native.Int(Native.Get(vertexArray,"Count"));trace.Uv=Native.Int(Native.Get(uvArray,"Count"));trace.Indices=Native.Int(Native.Get(indexArray,"Count"));
            var bounds=Native.Get(source,"bounds")!;
            var min=Point(Native.Get(bounds,"min")!);var max=Point(Native.Get(bounds,"max")!);
            trace.Bounds=min+".."+max;
            trace.Stage="mesh-validation";
            // Bound managed allocations/enumeration before copying mesh arrays.
            // FullRect owned PNG frames should be four vertices/six indices;
            // packed native heads retain their real mesh and the same caps.
            ValidateCounts(trace.Vertices,trace.Uv,trace.Indices);
            var vertices=Native.Items(vertexArray).Select(Point).ToArray();
            var uv=Native.Items(uvArray).Select(Point).ToArray();
            var triangles=Native.Items(indexArray).Select(Convert.ToInt32).ToArray();
            Validate(vertices,uv,triangles,min,max);
            float u0=uv.Min(v=>v.X),v0=uv.Min(v=>v.Y),u1=uv.Max(v=>v.X),v1=uv.Max(v=>v.Y);
            if(u0<0||v0<0||u1>1||v1>1||u1<=u0||v1<=v0)throw new InvalidOperationException("Outline UV crop is invalid.");
            trace.Stage="gpu-crop-readback";
            byte[] cropped=ReadTextureCrop(texture!,u0,v0,u1-u0,v1-v0);
            float width=max.X-min.X,height=max.Y-min.Y;
            float pixelsPerUnit=(MaxEdge-2f*Padding)/Math.Max(width,height);
            int w=Math.Min(MaxEdge,(int)Math.Ceiling(width*pixelsPerUnit)+2*Padding);
            int h=Math.Min(MaxEdge,(int)Math.Ceiling(height*pixelsPerUnit)+2*Padding);
            // Symmetric padding; normalize each axis to its pixel interior. The
            // output sprite aspect and factors preserve source bounds exactly.
            trace.Stage="raster";
            var alpha=Rasterize(cropped,ReadSize,ReadSize,vertices,uv,triangles,min,max,u0,v0,u1,v1,w,h,Padding);
            trace.Stage="edge-distance";
            var outline=EdgeHalo(alpha,w,h);
            var rgba=new byte[checked(w*h*4)];bool nonempty=false;
            for(int i=0;i<outline.Length;i++)
            {rgba[i*4]=rgba[i*4+1]=rgba[i*4+2]=255;rgba[i*4+3]=outline[i];nonempty|=outline[i]!=0;}
            if(!nonempty)throw new InvalidOperationException("Outline source sampled no alpha.");
            object? output=null,sprite=null;bool committed=false;
            try
            {
                trace.Stage="output-texture";
                output=NewTexture(w,h);
                Native.Set(output,"filterMode",Enum.Parse(Native.T("UnityEngine.FilterMode"),"Bilinear"));
                Native.Set(output,"wrapMode",Enum.Parse(Native.T("UnityEngine.TextureWrapMode"),"Clamp"));
                Native.Invoke(Native.M("UnityEngine.Texture2D","LoadRawTextureData",BytesType),output,ByteArray(rgba));
                Native.Invoke(Native.M("UnityEngine.Texture2D","Apply","System.Boolean","System.Boolean"),output,false,true);
                trace.Stage="output-sprite";
                sprite=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),
                    null,output,Rect(0,0,w,h),V(.5f,.5f),100f,(uint)0,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"))!;
                if(!Native.UnityAlive(sprite))throw new InvalidOperationException("Outline Sprite creation failed.");
                var result=new Result(sprite,(float)w/(w-2*Padding),(float)h/(h-2*Padding));
                committed=true;return new Cached(source,output,result);
            }
            finally{if(!committed){try{Destroy(sprite);}finally{Destroy(output);}}}
        }
        // Shared bounded GPU crop; RGB is linear only when Unity is running in
        // linear color space. Unit outlines consume alpha alone.
        internal static byte[] ReadTextureCrop(object source,float u,float v,float width,float height)
        {
            object? target=null,readable=null;var previous=Native.Static("UnityEngine.RenderTexture","active");
            try
            {
                target=Native.Invoke(Native.M("UnityEngine.RenderTexture","GetTemporary","System.Int32","System.Int32","System.Int32","UnityEngine.RenderTextureFormat","UnityEngine.RenderTextureReadWrite"),
                    null,ReadSize,ReadSize,0,Enum.Parse(Native.T("UnityEngine.RenderTextureFormat"),"ARGB32"),Enum.Parse(Native.T("UnityEngine.RenderTextureReadWrite"),"Linear"))!;
                Native.Invoke(Native.M("UnityEngine.Graphics","Blit","UnityEngine.Texture","UnityEngine.RenderTexture","UnityEngine.Vector2","UnityEngine.Vector2"),null,source,target,V(width,height),V(u,v));
                Native.Invoke(Native.M("UnityEngine.RenderTexture","set_active","UnityEngine.RenderTexture"),null,target);
                readable=NewTexture(ReadSize,ReadSize);
                Native.Invoke(Native.M("UnityEngine.Texture2D","ReadPixels","UnityEngine.Rect","System.Int32","System.Int32","System.Boolean"),readable,Rect(0,0,ReadSize,ReadSize),0,0,false);
                var data=Native.Invoke(Native.M("UnityEngine.Texture2D","GetRawTextureData"),readable)!;
                if(data is not ICollection<byte> bytes||bytes.Count!=ReadSize*ReadSize*4)
                    throw new InvalidOperationException("Outline RGBA32 readback size/type differs.");
                var managed=new byte[bytes.Count];bytes.CopyTo(managed,0);return managed;
            }
            finally
            {
                // Always restore the pre-existing target, including when Blit
                // itself throws; release only our acquired temporary target.
                try{Native.Invoke(Native.M("UnityEngine.RenderTexture","set_active","UnityEngine.RenderTexture"),null,previous);}
                finally
                {
                    try{Destroy(readable);}
                    finally{if(Native.UnityAlive(target))Native.Invoke(Native.M("UnityEngine.RenderTexture","ReleaseTemporary","UnityEngine.RenderTexture"),null,target);}
                }
            }
        }
        internal static void ValidateCounts(int positions,int uv,int indices)
        {
            if(positions<3||positions>MaxVertices)throw new InvalidOperationException("Outline vertex count "+positions+" is outside3.."+MaxVertices+".");
            if(uv!=positions)throw new InvalidOperationException("Outline UV count "+uv+" differs from vertex count "+positions+".");
            if(indices<3||indices>MaxIndices||indices%3!=0)throw new InvalidOperationException("Outline index count "+indices+" must be a multiple of3 within3.."+MaxIndices+".");
        }
        internal static void Validate(V2[] positions,V2[] uv,int[] triangles,V2 min,V2 max)
        {
            ValidateCounts(positions.Length,uv.Length,triangles.Length);
            if(!(max.X>min.X)||!(max.Y>min.Y)||!float.IsFinite(min.X+min.Y+max.X+max.Y)||!float.IsFinite(max.X-min.X)||!float.IsFinite(max.Y-min.Y))
                throw new InvalidOperationException("Outline bounds are nonfinite or empty: "+min+".."+max+".");
            if(positions.Concat(uv).Any(v=>!float.IsFinite(v.X)||!float.IsFinite(v.Y)))
                throw new InvalidOperationException("Outline mesh positions or UV contain nonfinite values.");
            if(triangles.Any(t=>t<0||t>=positions.Length))
                throw new InvalidOperationException("Outline mesh contains an out-of-range vertex index.");
        }
        // Packed/rotated/tight sprites use their actual UV mapping. Cropping a
        // textureRect and assuming it is an upright full rectangle is unsafe.
        internal static byte[] Rasterize(byte[] rgba,int sourceWidth,int sourceHeight,V2[] positions,V2[] uv,int[] triangles,
            V2 min,V2 max,float u0,float v0,float u1,float v1,int width,int height,int padding)
        {
            Validate(positions,uv,triangles,min,max);
            if(sourceWidth<1||sourceHeight<1||sourceWidth>256||sourceHeight>256||rgba.Length!=sourceWidth*sourceHeight*4||
               width<1||height<1||width>256||height>256||padding<0||2*padding>=width||2*padding>=height||u1<=u0||v1<=v0||!float.IsFinite(u0+v0+u1+v1))
                throw new ArgumentException("Invalid outline raster dimensions.");
            float innerW=width-2*padding,innerH=height-2*padding;
            V2 Pixel(V2 p)=>new(padding+(p.X-min.X)/(max.X-min.X)*innerW,padding+(p.Y-min.Y)/(max.Y-min.Y)*innerH);
            var pts=positions.Select(Pixel).ToArray();var output=new byte[width*height];
            for(int i=0;i<triangles.Length;i+=3)
            {
                int ia=triangles[i],ib=triangles[i+1],ic=triangles[i+2];var a=pts[ia];var b=pts[ib];var c=pts[ic];
                float d=(b.Y-c.Y)*(a.X-c.X)+(c.X-b.X)*(a.Y-c.Y);if(Math.Abs(d)<1e-8f)continue;
                int left=Math.Clamp((int)Math.Floor(Math.Min(a.X,Math.Min(b.X,c.X))),0,width-1),right=Math.Clamp((int)Math.Ceiling(Math.Max(a.X,Math.Max(b.X,c.X))),0,width-1);
                int bottom=Math.Clamp((int)Math.Floor(Math.Min(a.Y,Math.Min(b.Y,c.Y))),0,height-1),top=Math.Clamp((int)Math.Ceiling(Math.Max(a.Y,Math.Max(b.Y,c.Y))),0,height-1);
                for(int y=bottom;y<=top;y++)for(int x=left;x<=right;x++)
                {
                    float px=x+.5f,py=y+.5f;
                    float aa=((b.Y-c.Y)*(px-c.X)+(c.X-b.X)*(py-c.Y))/d;
                    float bb=((c.Y-a.Y)*(px-c.X)+(a.X-c.X)*(py-c.Y))/d;float cc=1-aa-bb;
                    if(aa< -1e-5f||bb< -1e-5f||cc< -1e-5f)continue;
                    float u=(aa*uv[ia].X+bb*uv[ib].X+cc*uv[ic].X-u0)/(u1-u0);
                    float v=(aa*uv[ia].Y+bb*uv[ib].Y+cc*uv[ic].Y-v0)/(v1-v0);
                    byte alpha=Sample(rgba,sourceWidth,sourceHeight,u,v);int at=y*width+x;if(alpha>output[at])output[at]=alpha;
                }
            }
            return output;
        }
        static byte Sample(byte[] rgba,int w,int h,float u,float v)
        {
            float x=Math.Clamp(u*w-.5f,0,w-1),y=Math.Clamp(v*h-.5f,0,h-1);int x0=(int)x,y0=(int)y,x1=Math.Min(w-1,x0+1),y1=Math.Min(h-1,y0+1);
            float dx=x-x0,dy=y-y0;
            float a=rgba[(y0*w+x0)*4+3]*(1-dx)+rgba[(y0*w+x1)*4+3]*dx;
            float b=rgba[(y1*w+x0)*4+3]*(1-dx)+rgba[(y1*w+x1)*4+3]*dx;
            return (byte)Math.Clamp((int)MathF.Round(a*(1-dy)+b*dy),0,255);
        }
        // Exact squared Euclidean distance between raster-pixel centers.
        // Finite sentinel exceeds every possible in-frame distance. Two
        // separable lower-envelope passes give bounded linear work and retain
        // thin-feature intensity instead of averaging it with empty space.
        internal static int[] DistanceSquared(byte[] alpha,int w,int h)
        {
            if(w<1||h<1||w>256||h>256||alpha.Length!=w*h)
                throw new ArgumentException("Invalid edge-distance dimensions.");
            int absent=w*w+h*h+1;var horizontal=new int[alpha.Length];var result=new int[alpha.Length];
            int length=Math.Max(w,h);var f=new int[length];var output=new int[length];
            var sites=new int[length];var breaks=new double[length+1];
            void Transform(int n)
            {
                int k=0;sites[0]=0;breaks[0]=double.NegativeInfinity;breaks[1]=double.PositiveInfinity;
                for(int q=1;q<n;q++)
                {
                    double Intersect()=>((double)f[q]+q*q-f[sites[k]]-sites[k]*sites[k])/(2*(q-sites[k]));
                    double cross=Intersect();
                    while(cross<=breaks[k]){k--;cross=Intersect();}
                    k++;sites[k]=q;breaks[k]=cross;breaks[k+1]=double.PositiveInfinity;
                }
                k=0;
                for(int q=0;q<n;q++)
                {
                    while(breaks[k+1]<q)k++;
                    int d=q-sites[k];output[q]=Math.Min(absent,d*d+f[sites[k]]);
                }
            }
            for(int y=0;y<h;y++)
            {
                for(int x=0;x<w;x++)f[x]=alpha[y*w+x]>=CoverageThreshold?0:absent;
                Transform(w);for(int x=0;x<w;x++)horizontal[y*w+x]=output[x];
            }
            for(int x=0;x<w;x++)
            {
                for(int y=0;y<h;y++)f[y]=horizontal[y*w+x];
                Transform(h);for(int y=0;y<h;y++)result[y*w+x]=output[y];
            }
            return result;
        }
        internal static byte[] EdgeHalo(byte[] alpha,int w,int h)
        {
            var squared=DistanceSquared(alpha,w,h);var result=new byte[alpha.Length];
            // Border padding must enclose the complete external profile. This
            // is an output texture; native SpriteRenderer alpha is unchanged.
            if(BandRadius<1||TailRadius<1||BandRadius+TailRadius+1>=Padding)
                throw new InvalidOperationException("Edge profile exceeds its padding.");
            int absent=w*w+h*h+1;
            for(int i=0;i<result.Length;i++)
            {
                // No light beneath the same covered part, including the mask's
                // submitted204-alpha face. Other per-part rings can overlap.
                if(alpha[i]>=CoverageThreshold||squared[i]>=absent)continue;
                double distance=Math.Max(0,Math.Sqrt(squared[i])-.5);
                double t=Math.Clamp((distance-BandRadius)/TailRadius,0,1);
                double falloff=1-t*t*(3-2*t);
                result[i]=(byte)Math.Clamp((int)Math.Round(EdgeAlpha*falloff),0,255);
            }
            return result;
        }
    }
}
