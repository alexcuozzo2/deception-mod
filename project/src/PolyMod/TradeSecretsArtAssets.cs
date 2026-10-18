using System;
using System.Collections.Generic;

namespace Deception.PolyModAdapter
{
    // Both PNGs retain the same crop and pivot. Tint only the transparent crown
    // layer; no per-player texture, pixel rewrite or shared Image color change.
    internal static class TradeSecretsArtAssets
    {
        static readonly Dictionary<string,object> Views=new(StringComparer.Ordinal);
        internal static object IncomeStar()=>MimicControls.NativeSprite("UI_STARICON");
        internal static object Sprite(bool team=false)
        {
            string name=team?"trade_secrets_team":"trade_secrets_fixed";
            if(Views.TryGetValue(name,out var old)&&Native.UnityAlive(old)&&Native.UnityAlive(Native.Get(old,"texture")))return old;
            if(Native.UnityAlive(old))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,old);
            Views.Remove(name);
            var source=UnitArtAssets.Get(name);var reference=MimicControls.NativeSprite("UNKNOWN_ICON");
            var frame=Native.Get(source,"rect")!;var nativeFrame=Native.Get(reference,"rect")!;
            float sourceSize=Math.Max(Convert.ToSingle(Native.Get(frame,"width")),Convert.ToSingle(Native.Get(frame,"height")));
            float nativeSize=Math.Max(Convert.ToSingle(Native.Get(nativeFrame,"width")),Convert.ToSingle(Native.Get(nativeFrame,"height")));
            float ppu=sourceSize*Convert.ToSingle(Native.Get(reference,"pixelsPerUnit"))/nativeSize;
            if(!float.IsFinite(ppu)||ppu<=0||!Native.UnityAlive(source)||!Native.UnityAlive(Native.Get(source,"texture")))
                throw new InvalidOperationException("Trade Secrets artwork frame is unavailable.");
            var result=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),
                null,Native.Get(source,"texture"),frame,Activator.CreateInstance(Native.T("UnityEngine.Vector2"),.5f,.5f),ppu,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"))!;
            if(!Native.UnityAlive(result))throw new InvalidOperationException("Trade Secrets artwork view is unavailable.");
            Views.Add(name,result);return result;
        }
        internal static int PlayerColor(object? state,object? player)
        {
            if(player is null)return 0xFFFFFF;
            int color=Native.Int(Native.Get(player,"color"));
            if(color!=-1)return color;
            if(state is null)return 0xFFFFFF;
            return Native.Int(Native.Invoke(Native.M(RecruitmentHooks.GLD,"GetTribeColor","PolytopiaBackendBase.Common.TribeType","PolytopiaBackendBase.Common.SkinType"),
                Native.Get(state,"GameLogicData"),Native.Get(player,"tribe"),Native.Get(player,"skinType")));
        }
        internal static object TeamColor(int color)=>Native.Invoke(Native.M("ColorUtil","ColorFromInt","System.Int32"),null,color)!;
        static object Vector(float x,float y)=>Activator.CreateInstance(Native.T("UnityEngine.Vector2"),x,y)!;
        // The returned child belongs to its native fixed-layer Image. It never
        // receives raycasts and inherits native button scale/opacity/layout.
        internal static object AddTeam(object image,int color)
        {
            if(!Native.UnityAlive(image))throw new InvalidOperationException("Trade Secrets fixed image is absent.");
            object? child=null;
            try
            {
                child=Native.Invoke(Native.M("UIUtils","GetImage"),null)!;
                Native.Set(child,"sprite",Sprite(true));Native.Set(child,"color",TeamColor(color));
                Native.Set(child,"preserveAspect",true);Native.Set(child,"raycastTarget",false);
                var t=Native.Get(child,"rectTransform")!;
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),t,Native.Get(image,"rectTransform"),false);
                Native.Set(t,"anchorMin",Vector(0,0));Native.Set(t,"anchorMax",Vector(1,1));
                Native.Set(t,"offsetMin",Vector(0,0));Native.Set(t,"offsetMax",Vector(0,0));
                Native.Set(t,"localScale",Activator.CreateInstance(Native.T("UnityEngine.Vector3"),1f,1f,1f));
                Native.Set(t,"localRotation",Native.Static("UnityEngine.Quaternion","identity"));
                var result=child;child=null;return result;
            }
            finally{if(Native.UnityAlive(child))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(child!,"gameObject"));}
        }
        internal static object Image(int color)
        {
            object? image=null;
            try
            {
                image=Native.Invoke(Native.M("UIUtils","GetImage"),null)!;
                Native.Set(image,"sprite",Sprite());Native.Set(image,"preserveAspect",true);Native.Set(image,"raycastTarget",false);
                Native.Invoke(Native.M("UnityEngine.UI.Image","SetNativeSize"),image);AddTeam(image,color);
                var result=image;image=null;return result;
            }
            finally{if(Native.UnityAlive(image))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(image!,"gameObject"));}
        }
        internal static object Button(object button,int color,bool ui2=false)
        {
            Native.Invoke(ui2?Native.M("UIRoundButton_UI2","SetSprite","UnityEngine.Sprite","System.Single"):
                Native.M("UIRoundButton","SetSprite","UnityEngine.Sprite","System.Boolean"),button,Sprite(),ui2?(object)1f:true);
            return AddTeam(Native.Get(button,"icon")!,color);
        }
    }
}
