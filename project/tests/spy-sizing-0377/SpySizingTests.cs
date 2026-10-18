using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.PolyModAdapter;
using UnityEngine;
namespace Deception.Tests
{
 public static class SpySizingTests
 {
  static int Cases,Assertions;static readonly List<string> Names=new();
  static void Check(bool yes,string why){Assertions++;if(!yes)throw new Exception(why);}
  static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.015f,why+": "+a+" vs "+b);
  static void Test(string name,Action action){Cases++;Names.Add(name);typeof(UiAdapterTests).GetMethod("Reset",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null);UnitArtAssets.ClearNativeHeads();try{action();}catch(Exception e){throw new Exception(name,e);}finally{UnitArtAssets.ClearNativeHeads();}}
  static float Factor(string name)=>name switch{"head_darkelf"=>1.20f,"head_cute"=>.84f,"head_cymanti"=>.88f,_=>1f};
  static UnitArtAssets.Layer[] Layers(Sprite sprite,bool spy=true,bool maid=false)
  {UnitArtAssets.ClearNativeHeads();GameManager.Atlas.Head=sprite;return UnitArtAssets.Layers(GameManager.LocalPlayer,spy,maid).ToArray();}
  static float Visible(UnitArtAssets.Layer layer)
  {var s=(Sprite)layer.Sprite;return (s.FixtureVertices!.Max(v=>v.y)-s.FixtureVertices!.Min(v=>v.y))*s.PixelsPerUnit/s.rect.height*layer.Height;}
  static void Target(UnitArtAssets.Layer h,float factor)=>Near(Visible(h),UnitArtAssets.SpyVisibleHeadHeight*factor,"shared visible head calibration");
  public static int Run(){try{
   foreach(string name in NativeHeads0377.Names)Test("SPYSIZE-profile-"+name,()=>{
    var sprite=NativeHeads0377.Get(name);var texture=sprite.texture;var rect=sprite.rect;var vertices=sprite.FixtureVertices;
    var layers=Layers(sprite);var h=layers.Single(x=>x.IsHead);Target(h,Factor(name));
    Near(h.Width/h.Height,rect.width/rect.height,"head aspect remains native");
    Check(layers.Length==3&&h.RectMapped&&ReferenceEquals(h.Sprite,sprite),"shared original head rendered last");
    Near(layers[0].Width,1393f*1.5f,"fixed body width untouched");Near(layers[1].Height,1393f*1.5f,"team body height untouched");
    Near(layers[0].X,(512f-478)/1024*2089.5f,"body horizontal anchor untouched");Near(layers[0].Y,-483f+(797f/1024-.5f)*2089.5f,"feet anchor untouched");
    Check(sprite.rect==rect&&ReferenceEquals(sprite.texture,texture)&&ReferenceEquals(sprite.FixtureVertices,vertices)&&sprite.DestroyCalls==0,"borrowed sprite pixels/geometry untouched");
   });
   foreach(string name in new[]{"head_darkelf","head_cute","head_cymanti"})foreach(bool maid in new[]{false,true})Test("SPYSIZE-collar-and-consumers-"+name+"-"+maid,()=>{
    var sprite=NativeHeads0377.Get(name);var layers=Layers(sprite,maid:maid);var h=layers.Single(x=>x.IsHead);Target(h,Factor(name));
    float neck=layers[0].Y+(512-(maid?396:382))/1024f*layers[0].Height;
    var method=typeof(UnitArtAssets).GetMethod("HeadContactY",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(object),typeof(float),typeof(float),typeof(float),typeof(float),typeof(bool)},null)!;
    float contact=(float)method.Invoke(null,new object[]{sprite,h.Width,h.Height,0f,neck,true})!;
    Near(contact-h.Y,UnitArtAssets.SpyHeadDrop,"reanchor after calibration keeps accepted collar drop");
    var frame=UnitArtAssets.Frame(h);Near(frame.Width,sprite.rect.width/sprite.PixelsPerUnit,"map uses original full-rectangle frame");
    var ui=MimicInspectionControls.ActualLayers(GameManager.LocalPlayer,spy:true,aquarion:maid).Single(x=>x.IsHead);
    Near(ui.Width,h.Width,"UI and map share calibrated width");Near(ui.Height,h.Height,"UI and map share calibrated height");Near(ui.Y,h.Y,"UI and map share calibrated collar anchor");
   });
   foreach(string guard in new[]{"unknown","case","rect width","rect height","mesh width","mesh height"})Test("SPYSIZE-exact-profile-guard-"+guard,()=>{
    var s=NativeHeads0377.Get("head_darkelf");
    switch(guard){case "unknown":s.name="head_future";break;case "case":s.name="HEAD_DARKELF";break;case "rect width":s.rect=new(s.rect.width+2,s.rect.height);break;case "rect height":s.rect=new(s.rect.width,s.rect.height+2);break;case "mesh width":s.FixtureVertices=s.FixtureVertices!.Select(v=>new Vector2(v.x*.98f,v.y)).ToArray();break;case "mesh height":s.FixtureVertices=s.FixtureVertices!.Select(v=>new Vector2(v.x,v.y*.98f)).ToArray();break;}
    Target(Layers(s).Single(x=>x.IsHead),1f);
   });
   Test("SPYSIZE-native-fallback-uses-loaded-asset",()=>{
    GameManager.LocalPlayer.tribe=PolytopiaBackendBase.Common.TribeType.Elyrion;GameManager.LocalPlayer.skinType=PolytopiaBackendBase.Common.SkinType.TestSkin;
    Target(Layers(NativeHeads0377.Get("head_imperius")).Single(x=>x.IsHead),1f);
    GameManager.LocalPlayer.tribe=PolytopiaBackendBase.Common.TribeType.Bardur;
    Target(Layers(NativeHeads0377.Get("head_darkelf")).Single(x=>x.IsHead),1.2f);
   });
   Test("SPYSIZE-cache-measures-once-and-preserves-borrowed-head",()=>{
    var s=NativeHeads0377.Get("head_darkelf");var first=Layers(s).Single(x=>x.IsHead);int reads=s.VertexReads,triangles=s.TriangleReads;
    for(int i=0;i<30;i++){var next=UnitArtAssets.Layers(GameManager.LocalPlayer,true).Single(x=>x.IsHead);Near(next.Width,first.Width,"cached width");Near(next.Y,first.Y,"cached contact");}
    Check(s.VertexReads==reads&&s.TriangleReads==triangles&&s.DestroyCalls==0,"idle must not repeatedly measure or destroy native head");
   });
   Test("SPYSIZE-cache-dead-head-reloads-new-profile",()=>{
    var old=NativeHeads0377.Get("head_darkelf");_=Layers(old);UnityEngine.Object.Destroy(old);var next=NativeHeads0377.Get("head_cute");GameManager.Atlas.Head=next;
    var h=UnitArtAssets.Layers(GameManager.LocalPlayer,true).Single(x=>x.IsHead);Target(h,.84f);Check(ReferenceEquals(h.Sprite,next)&&old.DestroyCalls==1&&next.DestroyCalls==0,"dead native cache cannot reuse calibration or destroy replacement");
   });
   Test("SPYSIZE-cache-atlas-change-reloads-native-head",()=>{
    var old=NativeHeads0377.Get("head_cute");_=Layers(old);var next=NativeHeads0377.Get("head_darkelf");GameManager.Atlas=new(){Head=next};
    var h=UnitArtAssets.Layers(GameManager.LocalPlayer,true).Single(x=>x.IsHead);Target(h,1.2f);Check(ReferenceEquals(h.Sprite,next)&&old.DestroyCalls==0,"atlas replacement preserves borrowed old assets");
   });
   foreach(string name in new[]{"head_darkelf","head_cute","head_cymanti"})Test("SPYSIZE-mimic-geometry-unchanged-"+name,()=>{
    var named=Layers(NativeHeads0377.Get(name),spy:false);var unknown=NativeHeads0377.Get(name);unknown.name="head_unknown";var unchanged=Layers(unknown,spy:false);
    Check(named.Length==unchanged.Length,"Mimic composition unchanged");for(int i=0;i<named.Length;i++){Near(named[i].Width,unchanged[i].Width,"Mimic width");Near(named[i].Height,unchanged[i].Height,"Mimic height");Near(named[i].X,unchanged[i].X,"Mimic x");Near(named[i].Y,unchanged[i].Y,"Mimic y");}
   });
   Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,tests=Names,nativeGame="NOT RUN"}));return 0;
  }catch(Exception e){Console.WriteLine(e);Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases,assertions=Assertions}));return 1;}}
 }
}
