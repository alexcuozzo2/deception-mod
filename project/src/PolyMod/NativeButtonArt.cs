using System;

namespace Deception.PolyModAdapter
{
    // Only called while constructing our own native buttons. Their normal
    // background owns this child, so layout and destruction stay native.
    internal static class NativeButtonArt
    {
        internal static void Install()
        {
            foreach(string type in new[]{"UIRoundButton","UIRoundButton_UI2"})
                Native.RequireReturn(Native.M(type,"get_BG"),Native.T("UnityEngine.UI.Image"));
            foreach(string property in new[]{"anchorMin","anchorMax","offsetMin","offsetMax"})
                Native.RequireReturn(Native.M("UnityEngine.RectTransform","set_"+property,"UnityEngine.Vector2"),typeof(void));
        }
        internal static void WhiteBackground(object button)
        {
            if(!Native.UnityAlive(button))throw new InvalidOperationException("Owned button is absent.");
            object background=Native.Get(button,"BG")??throw new InvalidOperationException("Native button background is absent.");
            var parent=Native.Get(background,"rectTransform")!;
            if(!Native.UnityAlive(background)||!Native.UnityAlive(parent)||
                !(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),parent,Native.Get(button,"transform"))!)
                throw new InvalidOperationException("Native button background is not owned.");
            var sprite=Native.Get(background,"overrideSprite");
            if(!Native.UnityAlive(sprite))throw new InvalidOperationException("Native button background artwork is absent.");
            object? plate=null;
            try
            {
                plate=Native.Invoke(Native.M("UIUtils","GetImage"),null)??throw new InvalidOperationException("Native image factory returned null.");
                var go=Native.Get(plate,"gameObject")!;var transform=Native.Get(plate,"rectTransform")!;
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,false);
                Native.Set(plate,"sprite",sprite);Native.Set(plate,"raycastTarget",false);Native.Set(plate,"preserveAspect",false);
                Native.Set(plate,"color",Activator.CreateInstance(Native.T("UnityEngine.Color"),1f,1f,1f,1f));
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),transform,parent,false);
                object zero=Activator.CreateInstance(Native.T("UnityEngine.Vector2"),0f,0f)!;
                Native.Set(transform,"anchorMin",zero);Native.Set(transform,"anchorMax",Activator.CreateInstance(Native.T("UnityEngine.Vector2"),1f,1f));
                Native.Set(transform,"offsetMin",zero);Native.Set(transform,"offsetMax",zero);
                Native.Set(transform,"localScale",Activator.CreateInstance(Native.T("UnityEngine.Vector3"),1f,1f,1f));
                Native.Set(transform,"localRotation",Native.Static("UnityEngine.Quaternion","identity"));
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,true);
                plate=null;
            }
            finally
            {
                if(Native.UnityAlive(plate))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(plate!,"gameObject"));
            }
        }
    }
}
