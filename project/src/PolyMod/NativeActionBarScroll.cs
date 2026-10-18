using System;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Reuse the bar's native ScrollRect and layout. Only wheel routing and a
    // passive scroll-position indicator belong to this adapter.
    internal static class NativeActionBarScroll
    {
        sealed class View
        {
            internal object Bar=null!,Scroll=null!,Content=null!,Viewport=null!,Buttons=null!;
            internal object? Track,Thumb;
            internal int WheelFrame=-1;
            internal bool IndicatorFailed;
            internal bool Horizontal;
            internal float Extent,Visible,Position=float.NaN,Ratio=float.NaN;
        }
        static View? Current,Failed;
        static bool Reported;
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("InteractionBar","Show","System.Boolean","System.Boolean"),typeof(NativeActionBarScroll),post:nameof(Shown));
            foreach(string name in new[]{"ClearButtons","Reset","DeInit","OnDestroy"})
                Native.Hook(h,Native.M("InteractionBar",name),typeof(NativeActionBarScroll),nameof(ResetBar));
            Native.Hook(h,Native.M("PolytopiaScrollRect","OnScroll","UnityEngine.EventSystems.PointerEventData"),typeof(NativeActionBarScroll),nameof(BeforeScroll));
            Native.Hook(h,Native.M("PolytopiaScrollRect","Update"),typeof(NativeActionBarScroll),nameof(BeforeUpdate));
            Native.Hook(h,Native.M("UnityEngine.UI.ScrollRect","LateUpdate"),typeof(NativeActionBarScroll),post:nameof(AfterLayout));
            Native.Hook(h,Native.M("UIManager","ShouldBlockScrollWheelFromZooming"),typeof(NativeActionBarScroll),post:nameof(BlockZoom));
        }
        static void Shown(object __instance)=>Observe(__instance);
        internal static void Observe(object bar)
        {
            try
            {
                if(!Native.UnityAlive(bar)||!(bool)Native.Get(bar,"IsShowing")!){if(Current is not null&&Native.Same(Current.Bar,bar))Clear();return;}
                object? scroll=Native.Get(bar,"bottomBarScrollRect");
                if(!Native.UnityAlive(scroll))return;
                object? content=Native.Get(scroll!,"content"),viewport=Native.Get(scroll!,"viewRect"),buttons=Native.Get(bar,"buttons");
                if(!Native.UnityAlive(content)||!Native.UnityAlive(viewport)||buttons is null||Native.Same(content,viewport))return;
                if(Failed is not null&&Native.Same(Failed.Bar,bar)&&Native.Same(Failed.Scroll,scroll)&&Native.Same(Failed.Buttons,buttons))return;
                if(Current is not null&&Native.Same(Current.Bar,bar)&&Native.Same(Current.Scroll,scroll)&&Native.Same(Current.Content,content)&&
                    Native.Same(Current.Viewport,viewport)&&Native.Same(Current.Buttons,buttons))return;
                Clear();
                Current=new View{Bar=bar,Scroll=scroll!,Content=content!,Viewport=viewport!,Buttons=buttons};
            }
            catch(Exception e){Fail(e);}
        }
        static bool Valid(View v)=>Native.UnityAlive(v.Bar)&&Native.UnityAlive(v.Scroll)&&Native.UnityAlive(v.Content)&&Native.UnityAlive(v.Viewport)&&
            (bool)Native.Get(v.Bar,"IsShowing")!&&(bool)Native.Get(Native.Get(v.Bar,"gameObject")!,"activeInHierarchy")!&&
            (bool)Native.Get(v.Scroll,"isActiveAndEnabled")!&&Native.Same(Native.Get(v.Bar,"bottomBarScrollRect"),v.Scroll)&&
            Native.Same(Native.Get(v.Scroll,"content"),v.Content)&&Native.Same(Native.Get(v.Scroll,"viewRect"),v.Viewport)&&
            Native.Same(Native.Get(v.Bar,"buttons"),v.Buttons)&&
            (bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),v.Content,Native.Get(v.Scroll,"transform"))!&&
            (bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),v.Viewport,Native.Get(v.Scroll,"transform"))!;
        static float Number(object? x)=>Convert.ToSingle(x,System.Globalization.CultureInfo.InvariantCulture);
        static bool Measure(View v)
        {
            object c=Native.Get(v.Content,"rect")!,r=Native.Get(v.Viewport,"rect")!;
            float cw=Number(Native.Get(c,"width")),ch=Number(Native.Get(c,"height")),vw=Number(Native.Get(r,"width")),vh=Number(Native.Get(r,"height"));
            if(!float.IsFinite(cw)||!float.IsFinite(ch)||!float.IsFinite(vw)||!float.IsFinite(vh)||vw<=0||vh<=0)return false;
            bool x=(bool)Native.Get(v.Scroll,"horizontal")!&&cw>vw+1,y=(bool)Native.Get(v.Scroll,"vertical")!&&ch>vh+1;
            if(!x&&!y)return false;
            bool horizontal=x&&(!y||cw/vw>=ch/vh);
            if(v.Horizontal!=horizontal){v.Position=float.NaN;v.Ratio=float.NaN;}
            v.Horizontal=horizontal;v.Visible=horizontal?vw:vh;v.Extent=(horizontal?cw:ch)-v.Visible;
            return true;
        }
        static bool Available(View v)=>Valid(v)&&!(bool)Native.Static("PopupManager","PopupShowing")!&&Measure(v);
        static bool Hovered(View v,object point)
        {
            if((bool)Native.Static("PolytopiaInput","OutsideWindow")!)return false;
            object? canvas=Native.Static("UIManager","Canvas");if(!Native.UnityAlive(canvas))return false;
            object? camera=Native.Int(Native.Get(canvas!,"renderMode"))==0?null:Native.Get(canvas!,"worldCamera");
            if(Native.Int(Native.Get(canvas!,"renderMode"))!=0&&!Native.UnityAlive(camera))return false;
            return (bool)Native.Invoke(Native.M("UnityEngine.RectTransformUtility","RectangleContainsScreenPoint","UnityEngine.RectTransform","UnityEngine.Vector2","UnityEngine.Camera"),null,v.Viewport,point,camera)!;
        }
        static bool ReadWheel(object delta,out float x,out float y)
        {
            x=Number(Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),delta,0));
            y=Number(Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),delta,1));
            return float.IsFinite(x)&&float.IsFinite(y)&&(x!=0||y!=0);
        }
        static bool Wheel(View v,object delta,object point)
        {
            if(!ReadWheel(delta,out var x,out var y)||!Available(v)||!Hovered(v,point))return false;
            int frame=Native.Int(Native.Static("UnityEngine.Time","frameCount"));
            if(v.WheelFrame==frame)return true; // EventSystem and raw update share one gesture.
            float amount=v.Horizontal?(Math.Abs(x)>Math.Abs(y)?-x:-y):(Math.Abs(y)>=Math.Abs(x)?y:-x);
            float sensitivity=Number(Native.Get(v.Scroll,"scrollSensitivity"));
            if(!float.IsFinite(sensitivity)||sensitivity<=0)sensitivity=24;
            sensitivity=Math.Max(24,Math.Min(120,sensitivity));
            string property=v.Horizontal?"horizontalNormalizedPosition":"verticalNormalizedPosition";
            float position=Number(Native.Get(v.Scroll,property));if(!float.IsFinite(position))return false;
            Native.Invoke(Native.M("UnityEngine.UI.ScrollRect","StopMovement"),v.Scroll);
            Native.Set(v.Scroll,property,Math.Clamp(position+amount*sensitivity/v.Extent,0,1));
            v.WheelFrame=frame;
            UpdateIndicator(v,true);
            return true;
        }
        static bool BeforeScroll(object __instance,object __0)
        {
            var v=Current;if(v is null||!Native.Same(v.Scroll,__instance))return true;
            try
            {
                if(!Wheel(v,Native.Get(__0,"scrollDelta")!,Native.Get(__0,"position")!))return true;
                Native.Invoke(Native.M("UnityEngine.EventSystems.AbstractEventData","Use"),__0);return false;
            }
            catch(Exception e){Fail(e);return true;}
        }
        static bool BeforeUpdate(object __instance)
        {
            var v=Current;if(v is null||!Native.Same(v.Scroll,__instance))return true;
            try
            {
                if(!Valid(v)){Clear();return true;}
                // Native PolytopiaScrollRect.Update also reads this delta and
                // synthesizes drags; do not process the same wheel twice.
                return !Wheel(v,Native.Static("UnityEngine.Input","mouseScrollDelta")!,Native.Static("PolytopiaInput","mousePosition")!);
            }
            catch(Exception e){Fail(e);return true;}
        }
        static void BlockZoom(ref bool __result)
        {
            var v=Current;if(__result||v is null)return;
            // Native uses this predicate for +/- too. Add only wheel ownership;
            // keyboard/gamepad zoom and pinch retain their native policy.
            try {if(ReadWheel(Native.Static("UnityEngine.Input","mouseScrollDelta")!,out _,out _)&&Available(v)&&Hovered(v,Native.Static("PolytopiaInput","mousePosition")!))__result=true;}
            catch(Exception e){Fail(e);}
        }
        static void AfterLayout(object __instance)
        {
            var v=Current;if(v is null||!Native.Same(v.Scroll,__instance))return;
            try{if(!Valid(v)){Clear();return;}UpdateIndicator(v,Measure(v));}
            catch(Exception e){Fail(e);}
        }
        static object Vector(float x,float y)=>Activator.CreateInstance(Native.T("UnityEngine.Vector2"),x,y)!;
        static object RectangleImage(string name,object parent,float red,float green,float blue,float alpha)
        {
            object go=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),name)!;
            try
            {
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,false);
                object rt=Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),go,
                    Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.RectTransform")))!,"UnityEngine.RectTransform");
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),rt,parent,false);
                object img=Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),go,
                    Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.UI.Image")))!,"UnityEngine.UI.Image");
                Native.Set(img,"raycastTarget",false);
                Native.Set(img,"color",Activator.CreateInstance(Native.T("UnityEngine.Color"),red,green,blue,alpha));
                Native.Set(go,"layer",Native.Get(Native.Get(parent,"gameObject")!,"layer"));
                return rt;
            }
            catch{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,go);throw;}
        }
        static void Rect(object rect,float minX,float minY,float maxX,float maxY,float left,float bottom,float right,float top)
        {
            Native.Set(rect,"anchorMin",Vector(minX,minY));Native.Set(rect,"anchorMax",Vector(maxX,maxY));
            Native.Set(rect,"offsetMin",Vector(left,bottom));Native.Set(rect,"offsetMax",Vector(right,top));
        }
        static void Active(object rect,bool active)
        {
            object go=Native.Get(rect,"gameObject")!;
            if((bool)Native.Get(go,"activeSelf")! !=active)Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,active);
        }
        static void UpdateIndicator(View v,bool overflow)
        {
            if(v.IndicatorFailed)return;
            if(!overflow){if(Native.UnityAlive(v.Track))Active(v.Track!,false);return;}
            if(v.Track is null)
            {
                try
                {
                    v.Track=RectangleImage("Deception action scroll track",v.Viewport,0,0,0,.35f);
                    v.Thumb=RectangleImage("Deception action scroll position",v.Track,1,1,1,.8f);
                }
                catch{v.IndicatorFailed=true;if(Native.UnityAlive(v.Track))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(v.Track!,"gameObject"));throw;}
            }
            if(!Native.UnityAlive(v.Track)||!Native.UnityAlive(v.Thumb)||!Native.Same(Native.Get(v.Track!,"parent"),v.Viewport)||!Native.Same(Native.Get(v.Thumb!,"parent"),v.Track))
            {v.IndicatorFailed=true;return;}
            float position=Number(Native.Get(v.Scroll,v.Horizontal?"horizontalNormalizedPosition":"verticalNormalizedPosition"));
            if(!float.IsFinite(position))return;
            position=Math.Clamp(position,0,1);
            float ratio=Math.Clamp(v.Visible/(v.Visible+v.Extent),.08f,1);
            if(position!=v.Position||ratio!=v.Ratio)
            {
                if(v.Horizontal){Rect(v.Track!,0,0,1,0,8,3,-8,7);Rect(v.Thumb!,position*(1-ratio),0,position*(1-ratio)+ratio,1,0,0,0,0);}
                else {Rect(v.Track!,1,0,1,1,-7,8,-3,-8);Rect(v.Thumb!,0,position*(1-ratio),1,position*(1-ratio)+ratio,0,0,0,0);}
                v.Position=position;v.Ratio=ratio;
            }
            Active(v.Track!,true);Active(v.Thumb!,true);
        }
        static void ResetBar(object __instance){if((Current is not null&&Native.Same(Current.Bar,__instance))||(Failed is not null&&Native.Same(Failed.Bar,__instance)))Clear();}
        internal static void Clear()
        {
            var v=Current;Current=null;Failed=null;if(v is null)return;
            if(Native.UnityAlive(v.Track)&&Native.Same(Native.Get(v.Track!,"parent"),v.Viewport))
            {
                try{Active(v.Track!,false);}finally{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(v.Track!,"gameObject"));}
            }
        }
        static void Fail(Exception e)
        {
            var failed=Current;try{Clear();}catch{ }
            Failed=failed; // One failed context is latched until native reset/reuse; no allocation loop.
            if(!Reported){Reported=true;Entry.Note("action-bar-scroll-error",e.ToString());}
        }
    }
}
