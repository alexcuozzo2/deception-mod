using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // UI ownership only. Inspection authorization and its selected-unit state
    // belong to MimicPresentationUi; no command or authoritative unit is edited.
    internal static class MimicInspectionControls
    {
        sealed class Control
        {
            public object Bar=null!,Unit=null!,Button=null!;
            public object? Callback;
            public string Label="";
            public bool Active,Enabled;
        }
        static readonly Dictionary<long,Control> Controls=new();
        static readonly HashSet<string> ReportedErrors=new(StringComparer.Ordinal);
        static readonly Dictionary<string,string> LastTrace=new(StringComparer.Ordinal);
        static int TraceCount;
        static void Trace(string phase,Func<string> read)
        {
            if(TraceCount>=100)return;
            try
            {
                string detail=read();
                if(LastTrace.TryGetValue(phase,out var old)&&old==detail)return;
                LastTrace[phase]=detail;
                Entry.Note("mimic-inspection-button-trace",(++TraceCount)+" "+phase+" "+detail);
            }
            catch{/* Diagnostic reads must not change control behavior. */}
        }
        static long Id(object? value)=>value is null?0:Native.Pointer(value);
        static string Layout(object component)
        {
            var transform=Native.Get(component,"transform");
            string result="";
            for(int depth=0;transform is not null&&depth<6;depth++,transform=Native.Get(transform,"parent"))
            {
                var go=Native.Get(transform,"gameObject")!;
                result+=" /"+Native.Get(go,"name")+"[self="+Native.Get(go,"activeSelf")+",hierarchy="+Native.Get(go,"activeInHierarchy")+
                    ",sibling="+Native.Invoke(Native.M("UnityEngine.Transform","GetSiblingIndex"),transform)+"]";
            }
            return result;
        }
        static string Geometry(object rect)
        {
            string Vector(object value)=>Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),value,0)+","+
                Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),value,1);
            return "position="+Vector(Native.Get(rect,"anchoredPosition")!)+" size="+Vector(Native.Get(rect,"sizeDelta")!);
        }
        static void TraceLayout(Control c)=>Trace("layout",()=>"showing="+Native.Get(c.Bar,"IsShowing")+
            " button="+Geometry(Native.Get(c.Button,"rectTransform")!)+Layout(c.Button)+
            " bottom="+Layout(Native.Get(c.Bar,"bottomBar")!)+" container="+Layout(Native.Get(c.Bar,"bottomBarButtonContainer")!));
        static string Snapshot(Control c)=>"bar="+Id(c.Bar)+" unit="+Id(c.Unit)+" barUnit="+Id(Native.Get(c.Bar,"unit"))+
            " button="+Id(c.Button)+" text="+Native.Get(c.Button,"text")+" cachedActive="+c.Active+
            " nativeActive="+Native.Get(c.Button,"buttonActive")+" enabled="+Native.Get(c.Button,"ButtonEnabled")+
            " callback="+Id(Native.Get(c.Button,"OnClicked"))+" expectedCallback="+Id(c.Callback)+
            " owned="+Owned(c)+" listCount="+Native.Get(Native.Get(c.Bar,"buttons")!,"Count");
        static void Note(string kind,Exception error)
        {
            string detail=error.ToString();
            if(ReportedErrors.Count<32&&ReportedErrors.Add(kind+"\n"+detail))Entry.Note(kind,detail);
        }
        public static void Install(Harmony h)
        {
            Native.RequireReturn(Native.M("InteractionBar","CreateRoundBottomBarButton","System.String","System.Boolean"),Native.T("UIRoundButton"));
            _=Native.M("InteractionBar","get_buttons");_=Native.M("InteractionBar","SetupButtonNavigation");
            _=Native.M("UIButtonBase","SetActive","System.Boolean");
            _=Native.M("UIRoundButton","get_text");
            var buttons=Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(Native.T("UIRoundButton"));
            Native.RequireReturn(Native.M(buttons,"Remove","UIRoundButton"),typeof(bool));
            Native.RequireReturn(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),typeof(void));
            foreach(string name in new[]{"ClearButtons","Reset","DeInit","OnDestroy"})
                Native.Hook(h,Native.M("InteractionBar",name),typeof(MimicInspectionControls),nameof(NativeClear));
            _=Native.M("UIManager","get_Instance");_=Native.M("UIManager","get_SpriteDuplicator");
            _=Native.M("UIDuplicatedSprites","Clear");_=Native.M("UIDuplicatedSprites","get_rectTransform");
            _=Native.M("UISpriteDuplicator+SpriteDuplicationData","get_scale");
            _=Native.M("UISpriteDuplicator+SpriteDuplicationData","get_offset");
            _=Native.M("UISpriteDuplicator","CreateImageFromSprite","UnityEngine.Sprite","UnityEngine.Transform","UnityEngine.Vector2","System.Single");
            _=Native.M("UISpriteDuplicator","RenderSprites","UISpriteDuplicator+SpriteDuplicationData");
            _=Native.M("UnityEngine.Vector2","get_zero");_=Native.M("UnityEngine.Sprite","get_rect");
            _=Native.M("UnityEngine.RectTransform","get_rect");_=Native.M("UnityEngine.Rect","get_width");_=Native.M("UnityEngine.Rect","get_height");
            _=Native.M("UnityEngine.Transform","get_childCount");_=Native.M("UnityEngine.Transform","GetChild","System.Int32");_=Native.M("UnityEngine.Transform","get_parent");
            _=Native.M("UnityEngine.RectTransform","SetSizeWithCurrentAnchors","UnityEngine.RectTransform+Axis","System.Single");
        }
        static bool Owned(Control c)
        {
            var buttons=Native.Get(c.Bar,"buttons");
            if(buttons is null||!Native.Items(buttons).Any(b=>Native.Same(b,c.Button)))return false;
            if(!string.Equals((string?)Native.Get(c.Button,"text"),c.Label,StringComparison.Ordinal))return false;
            var callback=Native.Get(c.Button,"OnClicked");
            return c.Callback is null?callback is null:Native.Same(callback,c.Callback);
        }
        internal static object? OwnedButton(object bar)=>Controls.TryGetValue(Native.Pointer(bar),out var c)&&Owned(c)?c.Button:null;
        public static void Show(object bar,object unit,bool actual,Action toggle,bool enabled=true,string actualLabel="Mimic")
        {
            try
            {
                long key=Native.Pointer(bar);
                string label=actual?"Show disguise":"Inspect "+actualLabel;
                Trace("show",()=>"bar="+key+" unit="+Id(unit)+" label="+label);
                if(Controls.TryGetValue(key,out var old)&&!Owned(old))
                {Trace("ownership-lost",()=>Snapshot(old));Controls.Remove(key);}
                if(Controls.TryGetValue(key,out var unchanged)&&unchanged.Active&&
                   Native.Same(unchanged.Unit,unit)&&unchanged.Label==label&&unchanged.Enabled==enabled)
                {Trace("fastpath",()=>Snapshot(unchanged));TraceLayout(unchanged);return;}
                bool created=false;
                if(!Controls.TryGetValue(key,out var c))
                {
                    // forceToBottom=true is the native parameter: this is an
                    // information-bar control, never a recruitment-wheel entry.
                    var button=Native.Invoke(Native.M("InteractionBar","CreateRoundBottomBarButton","System.String","System.Boolean"),bar,"Inspect Mimic",true)
                        ??throw new InvalidOperationException("Native inspection button was not created.");
                    c=new Control{Bar=bar,Unit=unit,Button=button};Controls.Add(key,c);
                    created=true;
                    var buttons=Native.Get(bar,"buttons")??throw new InvalidOperationException("Native bottom-button list is absent.");
                    if(!Native.Items(buttons).Any(b=>Native.Same(b,button)))
                        Native.Invoke(Native.M(buttons.GetType(),"Add","UIRoundButton"),buttons,button);
                    Trace("created",()=>Snapshot(c));
                }
                // Selection-context resets remove this control even when a new
                // interaction selects the same unit, so an unchanged callback
                // is reused only within the lead's still-authorized session.
                c.Unit=unit;c.Label=label;
                MimicControls.ApplyIcon(c.Button,"UI_INFO",c.Label);
                Native.Set(c.Button,"Cost",-1f);Native.Set(c.Button,"buttonActive",true);Native.Set(c.Button,"ButtonEnabled",enabled);
                c.Active=true;c.Enabled=enabled;
                var control=c;
                c.Callback=Native.ButtonCallback(()=>
                {
                    try
                    {
                        Trace("click",()=>Snapshot(control));
                        if(!Controls.TryGetValue(key,out var current)||!ReferenceEquals(current,control)||!control.Active||!control.Enabled||
                           !Native.Same(Native.Get(control.Bar,"unit"),control.Unit)||!Owned(control))
                        {Trace("click-rejected",()=>Snapshot(control));return;}
                        toggle(); // The lead revalidates owner/turn/fog/game identity.
                        Trace("click-dispatched",()=>Snapshot(control));
                    }
                    catch(Exception e){Note("mimic-inspection-button-click-error",e);}
                });
                Native.Set(c.Button,"OnClicked",c.Callback);
                Native.Set(c.Button,"OnClickedWhenDisabled",null);
                Native.Invoke(Native.M("UIButtonBase","SetActive","System.Boolean"),c.Button,true);
                Native.Invoke(Native.M("InteractionBar","SetupButtonNavigation"),bar);
                // Native Show(force=true) derives the bar's row/position from the
                // actual list count. A late append does not update that layout.
                if(created)MimicPresentationUi.ReflowBar(bar);
                Trace("shown",()=>Snapshot(c));
                TraceLayout(c);
            }
            catch(Exception e){Note("mimic-inspection-button-error",e);Remove(bar);}
        }
        public static void Remove(object bar,bool reflow=true)
        {
            long key=Native.Pointer(bar);
            if(!Controls.TryGetValue(key,out var c))return;
            try
            {
                if(!Owned(c)){Trace("remove-unowned",()=>Snapshot(c));Controls.Remove(key);return;}
                Trace("remove",()=>Snapshot(c));
                Disable(c);
                // Recheck after native visibility setters: never remove a button
                // whose native owner replaced its callback during those calls.
                if(!Owned(c)){Controls.Remove(key);return;}
                var buttons=Native.Get(bar,"buttons")!;
                if(!(bool)Native.Invoke(Native.M(buttons.GetType(),"Remove","UIRoundButton"),buttons,c.Button)!)
                    throw new InvalidOperationException("Owned inspection button could not be removed from native list.");
                Controls.Remove(key);
                var gameObject=Native.Get(c.Button,"gameObject")??throw new InvalidOperationException("Inspection button has no native GameObject.");
                Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,gameObject);
                Native.Invoke(Native.M("InteractionBar","SetupButtonNavigation"),bar);
                if(reflow)MimicPresentationUi.ReflowBar(bar);
                Trace("removed",()=>"bar="+key+" owned button detached/destroyed; native layout refreshed");
            }
            catch(Exception e){c.Active=false;Note("mimic-inspection-button-remove-error",e);}
        }
        static void Disable(Control c)
        {
            c.Active=false;
            Native.Set(c.Button,"OnClicked",null);Native.Set(c.Button,"OnClickedWhenDisabled",null);c.Callback=null;
            Native.Set(c.Button,"ButtonEnabled",false);Native.Set(c.Button,"buttonActive",false);
            Native.Invoke(Native.M("UIButtonBase","SetActive","System.Boolean"),c.Button,false);
        }
        public static void NativeClear(object __instance)
        {
            long key=Native.Pointer(__instance);
            if(!Controls.TryGetValue(key,out var c))return;
            try
            {
                Trace("native-clear",()=>Snapshot(c));
                if(Owned(c))Disable(c);
                // The native clear/reset owns list traversal and destruction.
                // Editing its list or reflowing here would interfere with it.
            }
            catch(Exception e){Note("mimic-inspection-button-native-clear-error",e);}
            finally{c.Active=false;Controls.Remove(key);}
        }
        public static void Clear()
        {
            foreach(var c in Controls.Values.ToArray())Remove(c.Bar);
            Controls.Clear();ReportedErrors.Clear();
        }
        static object Duplicator()
        {
            var manager=Native.Static("UIManager","Instance")??throw new InvalidOperationException("Native UI manager is absent.");
            return Native.Get(manager,"SpriteDuplicator")??throw new InvalidOperationException("Native sprite duplicator is absent.");
        }
        internal static IReadOnlyList<UnitArtAssets.Layer> ActualLayers(object player,bool boat=false,bool spy=false,bool aquarion=false,bool fogger=false)
        {
            var layers=(fogger?FoggerArtAssets.Layers(player):boat&&!aquarion?UnitArtAssets.BoatLayers(player):UnitArtAssets.Layers(player,spy,aquarion)).ToArray();
            if(layers.Length==0)throw new InvalidOperationException("Actual portrait has no layers.");
            foreach(var layer in layers)
            {
                if(!Native.UnityAlive(layer.Sprite)||layer.Color is null||!float.IsFinite(layer.X)||!float.IsFinite(layer.Y))
                    throw new InvalidOperationException("Actual portrait layer is invalid.");
                PositiveSize(layer.Width);PositiveSize(layer.Height);
            }
            return layers;
        }
        internal static object[] AppendActualLayers(object destination,IReadOnlyList<UnitArtAssets.Layer> layers,float fit,float x,float y)
        {
            PositiveSize(fit);
            var duplicator=Duplicator();var children=new List<object>();
            var imageType=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.UI.Image"));
            foreach(var layer in layers)
            {
                int count=Native.Int(Native.Get(destination,"childCount"));
                var position=Activator.CreateInstance(Native.T("UnityEngine.Vector2"),x+layer.X*fit,y+layer.Y*fit)!;
                Native.Invoke(Native.M("UISpriteDuplicator","CreateImageFromSprite","UnityEngine.Sprite","UnityEngine.Transform","UnityEngine.Vector2","System.Single"),
                    duplicator,layer.Sprite,destination,position,1f);
                if(Native.Int(Native.Get(destination,"childCount"))!=count+1)
                    throw new InvalidOperationException("Native actual portrait factory did not append exactly one layer.");
                var child=Native.Invoke(Native.M("UnityEngine.Transform","GetChild","System.Int32"),destination,count)
                    ??throw new InvalidOperationException("Native actual portrait layer is absent.");
                if(!Native.Same(Native.Get(child,"parent"),destination))
                    throw new InvalidOperationException("Native actual portrait layer changed parent.");
                var go=Native.Get(child,"gameObject")!;
                var component=Native.Invoke(Native.M("UnityEngine.GameObject","GetComponent","Il2CppSystem.Type"),go,imageType)
                    ??throw new InvalidOperationException("Native actual portrait layer has no Image.");
                var image=Native.As(component,"UnityEngine.UI.Image");
                if(!Native.UnityAlive(image)||!Native.Same(Native.Get(image,"gameObject"),go))
                    throw new InvalidOperationException("Native actual portrait Image ownership differs.");
                // Only this freshly created image receives player color. Shared
                // atlas sprites, source renderers and the native container do not.
                Native.Set(image,"color",layer.Color);Native.Set(image,"raycastTarget",false);
                Native.Set(child,"localScale",Activator.CreateInstance(Native.T("UnityEngine.Vector3"),layer.MirrorX?-1f:1f,1f,1f)!);
                var rect=Native.As(child,"UnityEngine.RectTransform");
                var setter=Native.M("UnityEngine.RectTransform","SetSizeWithCurrentAnchors","UnityEngine.RectTransform+Axis","System.Single");
                var axis=Native.T("UnityEngine.RectTransform+Axis");
                Native.Invoke(setter,rect,Enum.Parse(axis,"Horizontal"),PositiveSize(layer.Width*fit));
                Native.Invoke(setter,rect,Enum.Parse(axis,"Vertical"),PositiveSize(layer.Height*fit));
                children.Add(child);
            }
            return children.ToArray();
        }
        public static void ApplyActualPortrait(object container,object data,object player,float areaFraction=0.8f,bool boat=false,bool spy=false,bool aquarion=false,float yOffset=0,bool requireLiveSource=true,bool fogger=false)
        {
            if(!Native.UnityAlive(container)||!Native.Same(Native.Get(data,"container"),container)||
               (requireLiveSource&&!Native.UnityAlive(Native.Get(data,"source"))))
                throw new InvalidOperationException("Actual portrait source/container ownership is invalid.");
            var layers=ActualLayers(player,boat,spy,aquarion,fogger);
            var destination=Native.Get(container,"rectTransform")??throw new InvalidOperationException("Native portrait container is absent.");
            var bounds=Native.Get(destination,"rect")!;
            float width=PositiveSize(Native.Get(bounds,"width")),height=PositiveSize(Native.Get(bounds,"height"));
            float fit=PositiveSize(Math.Min(width*PositiveSize(areaFraction)/UnitArtAssets.CanvasWidth,
                height*areaFraction/UnitArtAssets.CanvasHeight));
            float x=0,y=yOffset;
            if(spy)(fit,x,y)=MimicDualPortrait.FitSingle(layers,width,height,areaFraction,yOffset);
            // Validate all input layers and geometry before the first clear.
            Native.Invoke(Native.M("UIDuplicatedSprites","Clear"),container);
            try
            {
                if(Native.Int(Native.Get(destination,"childCount"))!=0)
                    throw new InvalidOperationException("Native actual portrait clear retained children.");
                // Static popup renderers keep their copied images after native
                // CreateUnit destroys its temporary source. Their caller checks
                // exact renderer/data/container ownership instead of source life.
                if(!float.IsFinite(yOffset))throw new InvalidOperationException("Portrait offset is not finite.");
                var children=AppendActualLayers(destination,layers,fit,x,y);
                Trace("portrait-fit",()=>"destination="+width+"x"+height+" actual-layers="+children.Length+" fit="+fit);
            }
            catch
            {
                if(Native.UnityAlive(Native.Get(data,"source")))RestorePortrait(data);
                throw;
            }
        }
        static float PositiveSize(object? value)
        {
            float size=Convert.ToSingle(value,System.Globalization.CultureInfo.InvariantCulture);
            if(float.IsNaN(size)||float.IsInfinity(size)||size<=0)
                throw new InvalidOperationException("Native private portrait dimensions must be finite and positive.");
            return size;
        }
        public static void RestorePortrait(object data)
        {
            // Caller owns current context/source checks: do not globally retain
            // a pooled portrait and later overwrite a newer native selection.
            Native.Invoke(Native.M("UISpriteDuplicator","RenderSprites","UISpriteDuplicator+SpriteDuplicationData"),Duplicator(),data);
        }
    }
}
