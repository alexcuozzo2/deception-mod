using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using UnityEngine;
using UnitType=Polytopia.Data.UnitData.Type;

namespace Deception.Tests
{
    // Production map adapter, synthetic Unity transforms/native prefab surfaces.
    // No game renderer or native visual acceptance is claimed by these checks.
    public static class FrameTests
    {
        static int Assertions,Cases;
        static void Check(bool value,string why){Assertions++;if(!value)throw new Exception(why);}
        static void Near(float value,float expected,string why)=>Check(Math.Abs(value-expected)<.00001f,why+": "+value+" != "+expected);
        static void Point(Vector3 value,Vector3 expected,string why){Near(value.x,expected.x,why+" x");Near(value.y,expected.y,why+" y");Near(value.z,expected.z,why+" z");}
        static object? Call(Type type,string method,params object?[] args)=>type.GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
        static void Reset()
        {
            Call(typeof(UiAdapterTests),"Reset");Call(typeof(MapAdapterTests),"Reset");PrefabManager.Reset();
            foreach(string id in new[]{"dragonegg","babydragon","firedragon"})RecruitmentHooks.Forms[RecruitmentCatalog.Variant(id)]=new(id,RecruitmentCatalog.Variant(id),id,10,1,2,2,false);
        }
        static (Unit Unit,SpriteRenderer Body,PolytopiaSpriteRenderer Poly) Actor()
        {
            var actor=Call(typeof(MapAdapterTests),"Create",true,3u)!;
            T Read<T>(string name)=>(T)actor.GetType().GetProperty(name)!.GetValue(actor)!;
            var unit=Read<Unit>("Unit");unit.spriteContainer.localScale=Vector3.one;
            return(unit,Read<SpriteRenderer>("Body"),Read<PolytopiaSpriteRenderer>("Poly"));
        }
        static GameObject? Child(Unit unit)=>Enumerable.Range(0,unit.spriteContainer.childCount).Select(i=>unit.spriteContainer.GetChild(i).gameObject)
            .SingleOrDefault(g=>g.name=="Deception private Mimic"&&!g.Destroyed);
        static Vector3 Feet(GameObject child)=>child.transform.TransformPoint(new(0,UnitArtAssets.FootBaseline/100f,0));
        static void Case(Action body){Reset();Cases++;try{body();}finally{MimicMapInspection.Clear();MimicMapInspection.Configure(_=>null);}}
        public static int Run()
        {
            try
            {
                Case(()=>{
                    var a=Actor();MimicMapInspection.Configure(_=>true);
                    Vector3? referenceFeet=null,referenceScale=null;GameObject? first=null;
                    foreach(var form in new[]{("warrior",.4f,.5f), ("dragonegg",.06f,.08f),("babydragon",.3f,.4f),("firedragon",1.4f,1.8f)})
                    {
                        a.Unit.UnitState.type=Enum.Parse<UnitType>(RecruitmentCatalog.Variant(form.Item1));
                        a.Unit.spriteContainer.localPosition=form.Item1=="warrior"?new(0,0,0):new(.005f,.2f,0);
                        a.Body.bounds=new(new(13,29,0),new(form.Item2,form.Item3,0));a.Poly.Bounds=a.Body.bounds;
                        MimicMapInspection.Refresh(a.Unit);var child=Child(a.Unit);Check(child is not null,"Actual child missing for "+form.Item1);
                        referenceFeet??=Feet(child!);referenceScale??=child!.transform.localScale;first??=child;
                        Point(Feet(child!),referenceFeet.Value,"Disguise changed actual feet");Point(child!.transform.localScale,referenceScale.Value,"Disguise changed actual size");
                        Check(ReferenceEquals(child,first),"Form-only refresh allocated another child");
                        Check(a.Unit.UnitState.health==10&&!a.Unit.UnitState.moved&&!a.Unit.UnitState.attacked,"Presentation changed real state");
                    }
                    int calls=PrefabManager.Calls,previews=UIUtils.Previews.Count;
                    for(int i=0;i<100;i++)MimicMapInspection.Tick();
                    Check(PrefabManager.Calls==calls&&UIUtils.Previews.Count==previews,"Warm refresh fetched/instantiated native previews");
                    Check(ReferenceEquals(Child(a.Unit),first),"Warm refresh replaced actual child");
                    Check(PrefabManager.Fixtures.Values.All(p=>p.skinVisuals is null),"Reference read initialized or mutated prefab cache");
                    Near(referenceFeet!.Value.y,20-.02345455f,"Actual feet do not align with native Warrior floor");
                });
                Case(()=>{
                    var a=Actor();a.Unit.UnitState.type=UnitType.dcp_mimic_form_dragonegg;a.Unit.spriteContainer.localPosition=new(.005f,.2f,0);
                    MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);var child=Child(a.Unit)!;var feet=Feet(child);
                    a.Unit.transform.localPosition=new(12,23,0);a.Unit.spriteContainer.localPosition=new(.005f,.24f,0);MimicMapInspection.Tick();
                    Point(Feet(child),new(feet.x+2,feet.y+3.04f,feet.z),"Native move/bob was canceled");
                    a.Unit.transform.localScale=new(-1,1,1);MimicMapInspection.Tick();
                    Near(Feet(child).x,12-(feet.x-10),"Native root mirroring was canceled");
                    MapRenderer.ProductionCallbacks=true;a.Unit.SetAlpha(.25f);
                    var art=child.ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name.StartsWith("Deception art part ")).ToArray();
                    var layers=UnitArtAssets.Layers(a.Unit.Owner);
                    for(int i=0;i<art.Length;i++)Near(art[i].color.a,((Color)layers[i].Color).a*.25f,"Native alpha was not retained");
                    a.Unit.SetShowOutline(false);Check(child.ComponentsInChildren<SpriteRenderer>().All(r=>r.gameObject.name!="Deception actual outline"),"Outline state did not follow native");
                    a.Unit.SetShowOutline(true);Check(child.ComponentsInChildren<SpriteRenderer>().Count(r=>r.gameObject.name=="Deception actual outline")==4,"Native glow did not restore");
                });
                Case(()=>{
                    var a=Actor();MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);var child=Child(a.Unit)!;
                    var prefabs=PrefabManager.Fixtures.Values.ToArray();MimicMapInspection.RendererReturning(a.Poly);
                    Check(child.Destroyed&&child.transform.parent is null&&a.Body.color.a==.4f,"Pool return retained child or hid native sprite");
                    a.Unit.UnitState.id=44;GameManager.GameState.Units.Clear();GameManager.GameState.Units[44]=a.Unit.UnitState;
                    MapRenderer.Current!.Units.Clear();MapRenderer.Current.Register(a.Unit);MimicMapInspection.Refresh(a.Unit);
                    Check(Child(a.Unit) is {} current&&!ReferenceEquals(current,child),"Reused pooled unit did not get fresh owned art");
                    int calls=PrefabManager.Calls;MimicMapInspection.Clear();
                    Check(prefabs.All(p=>!p.gameObject.Destroyed),"Cleanup destroyed borrowed native prefab");
                    MimicMapInspection.Refresh(a.Unit);Check(PrefabManager.Calls>calls,"Session Clear retained prefab layout cache");
                    a.Unit.Visible=false;MimicMapInspection.Refresh(a.Unit);Check(Child(a.Unit) is null&&a.Body.color.a==.4f,"Hidden unit retained private art");
                });
                Case(()=>{
                    var a=Actor();var tribe=GameManager.LocalPlayer.tribe;var skin=GameManager.LocalPlayer.GetVisualSkin();
                    var prefab=PrefabManager.GetPrefab(UnitType.warrior,tribe,skin);var serialized=prefab.gameObject.FindComponent<SkinVisualsReference>()!;
                    var foreign=new SpriteRenderer{sprite=new Sprite("Foreign"){rect=new(99999,99999)}};
                    serialized.visualParts=new(Native.Items(serialized.visualParts).Cast<SkinVisualsReference.VisualPart>().Concat(new[]{new SkinVisualsReference.VisualPart{renderer=new(){spriteRenderer=foreign}}}).ToArray());
                    MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);var child=Child(a.Unit)!;
                    Near(child.transform.localScale.x,.313131313f/10*.95f,"Foreign/outline/overlay/uninitialized renderer bounds polluted reference");
                    Check(foreign.gameObject.Destroyed==false&&prefab.skinVisuals is null,"Reference measurement mutated borrowed parts");
                });
                Case(()=>{
                    var a=Actor();a.Unit.UnitState.type=UnitType.dcp_mimic_form_dragonegg;
                    var tribe=GameManager.LocalPlayer.tribe;var skin=GameManager.LocalPlayer.GetVisualSkin();
                    var prefab=PrefabManager.GetPrefab(UnitType.dragonegg,tribe,skin);prefab.spriteContainer.localScale=new(2,3,1);a.Unit.spriteContainer.localScale=new(2,3,1);a.Unit.spriteContainer.localPosition=new(.005f,.2f,0);
                    MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);var child=Child(a.Unit)!;
                    Near(child.transform.localScale.x*2,.313131313f/10*.95f,"Static carrier X scale not compensated");Near(child.transform.localScale.y*3,.313131313f/10*.95f,"Static carrier Y scale not compensated");
                    Near(Feet(child).y,20-.02345455f,"Scaled container changed feet");
                });
                Case(()=>{
                    var a=Actor();PrefabManager.Override=(_,_,_)=>null;MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);
                    Check(Child(a.Unit) is null&&a.Body.color.a==.4f,"Missing reference hid the native body");
                    var diagnostics=Entry.Notes.Where(n=>n.Kind=="mimic-map-project-error").ToArray();
                    Check(diagnostics.Length==1&&!diagnostics[0].Message.Contains("reference prefab is missing"),"Missing reference must retain an opaque operation diagnostic without private exception text");
                });
                Console.WriteLine(JsonSerializer.Serialize(new{cases=Cases,assertions=Assertions,passed=true}));return 0;
            }
            catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{cases=Cases,assertions=Assertions,passed=false,error=e.ToString()}));return 1;}
        }
    }
}
