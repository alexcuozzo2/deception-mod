using System;
using System.Collections.Generic;

namespace Deception.PolyModAdapter
{
    // Both original PNGs stay byte-for-byte embedded. Their independent crops
    // are placed in the same measured source canvas; the pilot is already drawn.
    internal static class FoggerArtAssets
    {
        internal const float DisplayScale=1.32f;
        internal const float Width=1000f*DisplayScale,Height=674f/938f*Width;
        static object? TeamSource,TeamTexture,TeamView;
        internal static object Icon()=>UnitArtAssets.Get("fogger_fixed");
        internal static object Badge()=>UnitArtAssets.Get("fogger_badge");
        static void Destroy(object? o){if(Native.UnityAlive(o))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,o);}
        internal static byte[] AlphaMask(byte[] rgba,int sourceWidth,int sourceHeight,int width,int height)
        {
            if(sourceWidth<1||sourceHeight<1||width<1||height<1||sourceWidth>256||sourceHeight>256||width>256||height>256||rgba.Length!=sourceWidth*sourceHeight*4)
                throw new ArgumentException("Invalid bounded Fogger team mask.");
            var result=new byte[width*height*4];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int from=(Math.Min(sourceHeight-1,(int)((y+.5f)*sourceHeight/height))*sourceWidth+Math.Min(sourceWidth-1,(int)((x+.5f)*sourceWidth/width)))*4;
                int to=(y*width+x)*4;result[to]=result[to+1]=result[to+2]=255;result[to+3]=rgba[from+3];
            }
            return result;
        }
        internal static object Team()
        {
            var source=UnitArtAssets.Get("fogger_team");
            if(Native.Same(TeamSource,source)&&Native.UnityAlive(TeamTexture)&&Native.UnityAlive(TeamView))return TeamView!;
            Destroy(TeamView);Destroy(TeamTexture);TeamView=null;TeamTexture=null;TeamSource=null;
            object? texture=null,view=null;
            try
            {
                // Submitted team pixels are nearly black. Use their alpha as
                // the color mask so multiplicative Unity tint remains legible.
                // Only this small crop is read once, never the 1240-square body.
                var rgba=UnitArtOutline.ReadTextureCrop(Native.Get(source,"texture")!,598f/1240,664f/1240,91f/1240,97f/1240);
                var bytes=AlphaMask(rgba,256,256,91,97);
                texture=Activator.CreateInstance(Native.T("UnityEngine.Texture2D"),91,97,Enum.Parse(Native.T("UnityEngine.TextureFormat"),"RGBA32"),false)!;
                Native.Set(texture,"filterMode",Enum.Parse(Native.T("UnityEngine.FilterMode"),"Bilinear"));
                var array=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
                Native.Invoke(Native.M("UnityEngine.Texture2D","LoadRawTextureData",array.FullName!),texture,Activator.CreateInstance(array,new object[]{bytes}));
                Native.Invoke(Native.M("UnityEngine.Texture2D","Apply","System.Boolean","System.Boolean"),texture,false,false);
                view=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),null,
                    texture,Activator.CreateInstance(Native.T("UnityEngine.Rect"),0f,0f,91f,97f),Activator.CreateInstance(Native.T("UnityEngine.Vector2"),.5f,.5f),100f,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"))!;
                if(!Native.UnityAlive(view))throw new InvalidOperationException("Fogger team Sprite was not created.");
                TeamSource=source;TeamTexture=texture;TeamView=view;return view;
            }
            catch{Destroy(view);Destroy(texture);throw;}
        }
        internal static IReadOnlyList<UnitArtAssets.Layer> Layers(object player)
        {
            if(player is null)throw new InvalidOperationException("Fogger artwork requires an explicit cosmetic owner.");
            var white=Activator.CreateInstance(Native.T("UnityEngine.Color"),1f,1f,1f,1f)!;
            var team=BlackmailArtAssets.TeamColor(BlackmailArtAssets.PlayerColor(Native.Static("GameManager","GameState"),player));
            float y=UnitArtAssets.FootBaseline+Height/2;
            return new[]{new UnitArtAssets.Layer(Icon(),0,y,Width,Height,white,AssetName:"fogger_fixed"),
                new UnitArtAssets.Layer(Team(),43.5f/938f*Width,y+40.5f/938f*Width,91f/938f*Width,97f/938f*Width,team,AssetName:"fogger_team",Tintable:true)};
        }
    }
}
