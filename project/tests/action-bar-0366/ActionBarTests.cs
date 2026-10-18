using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.PolyModAdapter;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Deception.Tests
{
 public static class ActionBarTests
 {
  static int Cases,Assertions;
  static void Check(bool value,string label){Assertions++;if(!value)throw new Exception(label);}
  static void Near(float a,float b,string label)=>Check(Math.Abs(a-b)<.00001f,label+" "+a+" / "+b);
  static object? Call(string method,params object?[] args)=>typeof(NativeActionBarScroll).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
  static bool Zoom(bool native=false){object?[] args={native};Call("BlockZoom",args);return (bool)args[0]!;}
  static bool Scroll(PolytopiaScrollRect scroll,float x,float y)
  {var e=new PointerEventData{position=PolytopiaInput.mousePosition,scrollDelta=new(x,y)};bool native=(bool)Call("BeforeScroll",scroll,e)!;Check(e.used==!native,"consumed event marked used only when handled");return native;}
  static bool Tick(PolytopiaScrollRect scroll,float x,float y){Input.mouseScrollDelta=new(x,y);return(bool)Call("BeforeUpdate",scroll)!;}
  static void Layout(PolytopiaScrollRect scroll)=>Call("AfterLayout",scroll);
  static RectTransform? Track(PolytopiaScrollRect scroll)=>Enumerable.Range(0,scroll.viewport!.childCount).Select(scroll.viewport.GetChild).OfType<RectTransform>().SingleOrDefault(x=>!x.gameObject.Destroyed&&x.gameObject.name=="Deception action scroll track");
  static (InteractionBar Bar,PolytopiaScrollRect Scroll) Setup(bool horizontal=true,float content=800,float visible=300)
  {
   var bar=new InteractionBar();var scroll=new PolytopiaScrollRect{horizontal=horizontal,vertical=!horizontal};scroll.transform.SetParent(bar.transform,false);
   var viewport=new RectTransform{rect=new(visible,horizontal?100:visible)};viewport.SetParent(scroll.transform,false);
   var items=new RectTransform{rect=horizontal?new(content,100):new(visible,content)};items.SetParent(viewport,false);scroll.viewport=viewport;scroll.content=items;
   bar.bottomBarScrollRect=scroll;bar.bottomBarButtonContainer=items;
   NativeActionBarScroll.Observe(bar);PolytopiaInput.mousePosition=new(25,25);Input.mouseScrollDelta=new(0,-1);Layout(scroll);return(bar,scroll);
  }
  static void Case(string name,Action work)
  {
   Cases++;NativeActionBarScroll.Clear();NativeApparentUpgradeUi.Reset();NativeSpecialActionUi.Clear();GameObject.FailAddComponentForName=null;
   PopupManager.PopupShowing=false;PolytopiaInput.OutsideWindow=false;UIManager.Canvas=new();Time.frameCount=10;Input.mouseScrollDelta=new(0,0);
   try{work();}catch(Exception e){throw new Exception(name,e);}finally{NativeActionBarScroll.Clear();NativeApparentUpgradeUi.Reset();GameObject.FailAddComponentForName=null;}
  }
  public static int Run(){try{
   Case("native API hook signatures",()=>{var h=new HarmonyLib.Harmony();NativeActionBarScroll.Install(h);Check(h.Patched.Count==9,"nine narrow hooks installed");Check(h.Patched.Contains("UIManager::ShouldBlockScrollWheelFromZooming")&&h.Patched.Contains("PolytopiaScrollRect::OnScroll"),"existing native zoom and scroll boundaries");});
   Case("free owned actions hide whole cost widget, paid/native unchanged",()=>{var a=Setup();var free=a.Bar.CreateRoundBottomBarButton("Shed Disguise",true);free.Cost=0;var paid=a.Bar.CreateRoundBottomBarButton("Paid mod",true);paid.Cost=2;var native=a.Bar.CreateRoundBottomBarButton("Native zero",true);native.Cost=0;
    NativeApparentUpgradeUi.FixtureBar=a.Bar;NativeApparentUpgradeUi.FixtureButtons=new object[]{free,paid};var click=Native.ButtonCallback(()=>{});free.OnClicked=(UIButtonBase.ButtonAction)click;
    MimicActionMenu.Normalize(a.Bar);Check(free.Cost==-1&&!free.resourceWidget.gameObject.activeSelf,"free number+star parent hidden");Check(paid.Cost==2&&paid.resourceWidget.gameObject.activeSelf&&native.resourceWidget.gameObject.activeSelf,"paid and native zero visible");Check(free.text=="Shed Disguise"&&ReferenceEquals(free.OnClicked,click),"caption and click untouched");
    NativeApparentUpgradeUi.Reset();free.Cost=0;MimicActionMenu.Normalize(a.Bar);Check(free.resourceWidget.gameObject.activeSelf,"lost ownership cannot hide native zero cost");free.Cost=7;MimicActionMenu.Normalize(a.Bar);Check(free.resourceWidget.gameObject.activeSelf&&free.resourceWidget.Value==7,"paid pooled reuse restored by native setter");});
   Case("wheel down advances horizontal bar, consumes zoom",()=>{var a=Setup();Check(Zoom(),"overflow hover owns scroll zoom");Check(!Scroll(a.Scroll,0,-1),"wheel consumed");Near(a.Scroll.horizontalNormalizedPosition,.08f,"native normalized offset");Check(a.Scroll.Stops==1,"old momentum stopped");});
   Case("event then raw update handles gesture once",()=>{var a=Setup();Scroll(a.Scroll,0,-1);Check(!Tick(a.Scroll,0,-1),"raw native trackpad update skipped");Near(a.Scroll.horizontalNormalizedPosition,.08f,"no double movement");Check(a.Scroll.Stops==1,"one movement write");});
   Case("raw update then event handles gesture once",()=>{var a=Setup();Check(!Tick(a.Scroll,0,-1),"raw fallback owns blank viewport");Scroll(a.Scroll,0,-1);Near(a.Scroll.horizontalNormalizedPosition,.08f,"same frame event deduplicated");Time.frameCount++;Scroll(a.Scroll,0,-1);Near(a.Scroll.horizontalNormalizedPosition,.16f,"new frame continues");});
   Case("trackpad fractional delta maps to horizontal",()=>{var a=Setup();Scroll(a.Scroll,0,-.125f);Near(a.Scroll.horizontalNormalizedPosition,.01f,"fractional vertical gesture preserved");Time.frameCount++;Scroll(a.Scroll,-.25f,0);Near(a.Scroll.horizontalNormalizedPosition,.03f,"horizontal trackpad preserved");});
   Case("vertical native bar scrolls down from top",()=>{var a=Setup(false);Scroll(a.Scroll,0,-1);Near(a.Scroll.verticalNormalizedPosition,.92f,"vertical down");Check(!Track(a.Scroll)!.gameObject.Destroyed,"vertical indicator visible");});
   foreach(float edge in new[]{0f,1f})Case("wheel endpoint keeps map zoom blocked "+edge,()=>{var a=Setup();a.Scroll.horizontalNormalizedPosition=edge;Scroll(a.Scroll,0,edge==0?1:-1);Near(a.Scroll.horizontalNormalizedPosition,edge,"clamped edge");Check(Zoom(),"edge does not spill scroll into map zoom");});
   Case("outside bar preserves scrolling and map zoom",()=>{var a=Setup();PolytopiaInput.mousePosition=new(500,500);Check(!Zoom(),"outside native false remains false");Check(Zoom(true),"native true remains true");Check(Scroll(a.Scroll,0,-1)&&Tick(a.Scroll,0,-1),"outside events and update native");Near(a.Scroll.horizontalNormalizedPosition,0,"outside unchanged");});
   Case("nonoverflow bar uses entirely native input",()=>{var a=Setup(content:280);Check(!Zoom()&&Scroll(a.Scroll,0,-1)&&Tick(a.Scroll,0,-1),"fitting content does not capture");Check(Track(a.Scroll) is null,"no nonoverflow decoration allocation");});
   Case("hover without wheel preserves keyboard zoom",()=>{Setup();Input.mouseScrollDelta=new(0,0);Check(!Zoom()&&Zoom(true),"keyboard/pinch policy remains native even over overflow");});
   Case("popup preserves existing UI ownership",()=>{var a=Setup();PopupManager.PopupShowing=true;Check(!Zoom()&&Zoom(true),"no new zoom claim behind popup");Check(Scroll(a.Scroll,0,-1),"popup scroll not captured");});
   foreach(string reason in new[]{"bar hidden","inactive root","disabled scroll","replaced scroll","replaced content","replaced viewport","replaced buttons","reparented content","destroyed viewport"})Case("pool/lifetime guard "+reason,()=>{var a=Setup();var track=Track(a.Scroll)!;
    switch(reason){case"bar hidden":a.Bar.IsShowing=false;break;case"inactive root":a.Bar.gameObject.SetActive(false);break;case"disabled scroll":a.Scroll.enabled=false;break;case"replaced scroll":a.Bar.bottomBarScrollRect=new();break;case"replaced content":a.Scroll.content=new();break;case"replaced viewport":a.Scroll.viewport=new();break;case"replaced buttons":a.Bar.buttons=new();break;case"reparented content":a.Scroll.content!.SetParent(new GameObject().transform,false);break;case"destroyed viewport":UnityEngine.Object.Destroy(a.Scroll.viewport!.gameObject);break;}
    Check(!Zoom(),"invalid bar never blocks native zoom");Check(Tick(a.Scroll,0,-1),"invalid update left native");Check(track.gameObject.Destroyed,"old owned decoration retired");});
   Case("scroll indicator follows position and hides when content fits",()=>{var a=Setup();var track=Track(a.Scroll)!;var thumb=(RectTransform)track.GetChild(0);Check(track.gameObject.activeSelf&&thumb.gameObject.activeSelf,"visible track and thumb");Check(!track.gameObject.FindComponent<UnityEngine.UI.Image>()!.raycastTarget&&!thumb.gameObject.FindComponent<UnityEngine.UI.Image>()!.raycastTarget,"decoration cannot block buttons or drag");Near(thumb.anchorMax.x,.375f,"thumb visible ratio");
    a.Scroll.horizontalNormalizedPosition=1;Layout(a.Scroll);Near(thumb.anchorMin.x,.625f,"thumb at far end");a.Scroll.content!.rect=new(200,100);Layout(a.Scroll);Check(!track.gameObject.activeSelf,"no misleading track for fitting row");a.Scroll.content.rect=new(800,100);for(int i=0;i<200;i++)Layout(a.Scroll);Check(ReferenceEquals(track,Track(a.Scroll))&&track.childCount==1,"indicator objects reused across layout ticks");});
   Case("new bar retires previous indicator",()=>{var a=Setup();var old=Track(a.Scroll)!;var b=Setup();Check(old.gameObject.Destroyed,"old bar owned decorations destroyed");Check(Scroll(a.Scroll,0,-1),"old scroll returns native");Check(!Scroll(b.Scroll,0,-1),"new exact scroll owns input");});
   Case("clear hook removes and deactivates indicator",()=>{var a=Setup();var track=Track(a.Scroll)!;Call("ResetBar",a.Bar);Check(track.gameObject.Destroyed&&!track.gameObject.activeSelf,"immediate hidden then native destroy");Check(!Zoom()&&Scroll(a.Scroll,0,-1),"no retired input authority");});
   Case("screen camera canvas supplies its camera",()=>{var a=Setup();var camera=new Camera();UIManager.Canvas.renderMode=RenderMode.ScreenSpaceCamera;UIManager.Canvas.worldCamera=camera;Check(Zoom()&&ReferenceEquals(RectTransformUtility.LastCamera,camera),"camera hit test preserved");UIManager.Canvas.worldCamera=null;Check(!Zoom()&&Scroll(a.Scroll,0,-1),"missing canvas camera denied");});
   Case("nonfinite and zero input does not consume",()=>{var a=Setup();foreach(var y in new[]{float.NaN,float.PositiveInfinity,0f})Check(Scroll(a.Scroll,0,y),"invalid/no gesture stays native");Near(a.Scroll.horizontalNormalizedPosition,0,"invalid input unchanged");});
   Case("untracked native scroll does not enter bar path",()=>{Setup();var other=new PolytopiaScrollRect();Check(Scroll(other,0,-1)&&Tick(other,0,-1),"unrelated scroller native");});
   Case("partial indicator creation is cleaned up",()=>{GameObject.FailAddComponentForName="Deception action scroll position";var a=Setup();Check(Track(a.Scroll) is null,"created track destroyed after thumb failure");Check(!Zoom(),"failure relinquishes input");Check(Scroll(a.Scroll,0,-1),"native fallback retained");int children=a.Scroll.viewport!.childCount;
    for(int i=0;i<10;i++){NativeActionBarScroll.Observe(a.Bar);Layout(a.Scroll);}Check(a.Scroll.viewport.childCount==children,"failed same context cannot allocate every frame");
    GameObject.FailAddComponentForName=null;Call("ResetBar",a.Bar);NativeActionBarScroll.Observe(a.Bar);Layout(a.Scroll);Check(Track(a.Scroll) is not null,"native reset allows bounded fresh attempt");});
   Case("scroll cannot activate a selected action",()=>{var a=Setup();int calls=0;var button=a.Bar.CreateRoundBottomBarButton("Action",true);button.OnClicked=(UIButtonBase.ButtonAction)Native.ButtonCallback(()=>calls++);for(int i=0;i<10;i++){Time.frameCount++;Scroll(a.Scroll,0,-1);}Check(calls==0&&ReferenceEquals(a.Bar.buttons[0],button),"wheel changes only viewport, stable button identity");});
   Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,nativeGame="NOT RUN"}));return 0;
  }catch(Exception e){Console.WriteLine(e);Console.WriteLine(string.Join("\n",Entry.Notes));return 1;}}
 }
}
