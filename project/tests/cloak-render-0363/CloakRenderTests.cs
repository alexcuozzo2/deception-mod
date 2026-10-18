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
 public static class CloakRenderTests
 {
  static int Cases,Assertions;
  static void Check(bool ok,string message){Assertions++;if(!ok)throw new Exception(message);}
  static void Near(float a,float b,string message)=>Check(Math.Abs(a-b)<.00001f,message+$" ({a}, {b})");
  static object? Call(Type type,string name,params object?[] args)=>type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
  static void Case(string label,Action work)
  {
   Call(typeof(UiAdapterTests),"Reset");Call(typeof(MapAdapterTests),"Reset");Unit.PulseHookInstalled=false;PrefabManager.Reset();NativeVisualFixture.Reset();NativeSpyPresentation.Ready=_=>true;MimicMapInspection.Configure(MimicPresentationUi.MapView);
   var h=new HarmonyLib.Harmony();MimicMapInspection.Install(h);Unit.PulseHookInstalled=h.Patched.Contains("Unit::Update");
   Cases++;try{work();}catch(Exception e){throw new Exception(label,e);}finally{MimicPresentationState.ReadIdentity=null;MimicPresentationUi.Clear();Unit.PulseHookInstalled=false;MapRenderer.ProductionCallbacks=false;}
  }
  sealed record Actor(Unit Unit,PolytopiaSpriteRenderer Poly,SpriteRenderer Body,ClientInteraction Interaction);
  static Actor Create()
  {
   var a=Call(typeof(MapAdapterTests),"Create",true,3u)!;var type=a.GetType();var unit=(Unit)type.GetProperty("Unit")!.GetValue(a)!;var poly=(PolytopiaSpriteRenderer)type.GetProperty("Poly")!.GetValue(a)!;var body=(SpriteRenderer)type.GetProperty("Body")!.GetValue(a)!;
   unit.UnitState.type=UnitType.warrior;unit.UnitState.UnitData=new(UnitType.warrior);unit.UnitState.health=70;unit.UnitState.effects.Add(UnitEffect.Invisible);
   var mask=new LedgerAppearance("cloak",new("Imperius","Default"),new("Imperius","Default"),new("Imperius","Default"),0x4455aa,50,50,false,ApparentConcealment.Hidden,-1,new[]{"Invisible"});
   var row=new LedgerUnit(17,unit.UnitState.id,true,LedgerActualKind.Genuine,true,mask,PaidInvestmentKind.Known,2);MimicPresentationState.ReadIdentity=(_,_)=>row;
   var interaction=new ClientInteraction();interaction.SelectUnit(unit);MimicPresentationUi.Selected(interaction,unit);MimicPresentationUi.BarReady(interaction.Bar);MimicMapInspection.Refresh(unit);
   return new(unit,poly,body,interaction);
  }
  static GameObject? Overlay(Unit u)=>Enumerable.Range(0,u.spriteContainer.childCount).Select(i=>u.spriteContainer.GetChild(i).gameObject).SingleOrDefault(g=>g.name=="Deception apparent native form"&&!g.Destroyed);
  static void Genuine(Actor a)=>Check(a.Unit.UnitState.type==UnitType.warrior&&a.Unit.UnitState.health==70&&a.Unit.UnitState.owner==1&&a.Unit.UnitState.effects.Count==1&&a.Unit.UnitState.HasEffect(UnitEffect.Invisible)&&!a.Unit.UnitState.moved&&!a.Unit.UnitState.attacked,"rendering changed authoritative Warrior state");
  public static int Run(){try{
   Case("direct Invisible pulse hides true body",()=>{var a=Create();Check(Unit.PulseHookInstalled,"audited native Unit.Update hook installed");Check(Overlay(a.Unit)?.activeSelf==true,"copied Cloak active");a.Unit.Update();Near(a.Body.color.a,0,"native frame pulse must not reshow Warrior");Check(Overlay(a.Unit)?.activeSelf==true,"Cloak remains visible");Genuine(a);});
   Case("successive native frames do not rebuild projection",()=>{var a=Create();var child=Overlay(a.Unit);int resolves=0;MimicMapInspection.Configure(_=>{resolves++;throw new Exception("per-frame projection forbidden");});int clones=NativeVisualFixture.CloneCalls;for(int i=0;i<40;i++){a.Unit.NativePulseAlpha=.2f+i*.01f;a.Unit.Update();Near(a.Body.color.a,0,"original hidden every frame");Check(ReferenceEquals(child,Overlay(a.Unit)),"no child allocation");}Check(resolves==0&&NativeVisualFixture.CloneCalls==clones,"no projection or native clone allocation");Genuine(a);});
   Case("toggle restores latest native pulse",()=>{var a=Create();for(int i=0;i<12;i++){a.Unit.NativePulseAlpha=.2f+i*.03f;a.Unit.Update();Check(MimicPresentationUi.ToggleInspection(a.Interaction,a.Unit),"toggle actual");Check(Overlay(a.Unit)?.activeSelf!=true,"actual has no copied body");Near(a.Body.color.a,a.Unit.NativePulseAlpha,"latest native alpha restored");a.Unit.NativePulseAlpha+=.01f;a.Unit.Update();Near(a.Body.color.a,a.Unit.NativePulseAlpha,"actual native pulse unchanged");Check(MimicPresentationUi.ToggleInspection(a.Interaction,a.Unit),"toggle apparent");Near(a.Body.color.a,0,"apparent hides true body");}Genuine(a);});
   Case("pool return restores latest pulse and retires child",()=>{var a=Create();a.Unit.Update();var child=Overlay(a.Unit)!;MapRenderer.ProductionCallbacks=true;a.Poly.ReturnToPool();Check(child.Destroyed,"owned Cloak retired on pool return");Near(a.Body.color.a,a.Unit.NativePulseAlpha,"latest native color restored before pool detach");object? scope;MimicMapInspection.BeforePulse(a.Unit,out scope);Check(scope is null,"returned renderer has no tracked pulse scope");});
   Case("original shader mesh uses hidden PSR color",()=>{var a=Create();MimicMapInspection.Clear();a.Poly.spriteRenderer=null;a.Unit.skinVisuals.visualParts[0].renderer.spriteRenderer=null;UnityEngine.Object.Destroy(a.Body);var mesh=new MeshRenderer{Geometry="Native Warrior geometry",gameObject=a.Poly.gameObject};a.Poly.meshRenderer=mesh;MimicMapInspection.Refresh(a.Unit);mesh.VertexAlpha=a.Poly.Color.a;Check(!mesh.Visible,"mesh built from native PSR color is hidden");a.Unit.Update();mesh.VertexAlpha=a.Poly.Color.a;Check(!mesh.Visible&&mesh.enabled,"mesh stays hidden without overriding renderer enabled");Check(Overlay(a.Unit)?.activeSelf==true,"copied Cloak remains one rendered shape");});
   Case("ordinary native unit bypass",()=>{var ordinary=new Unit();ordinary.UnitState.effects.Add(UnitEffect.Invisible);var sr=new SpriteRenderer{sprite=new("body")};ordinary.skinVisuals.visualParts=new(new[]{new SkinVisualsReference.VisualPart{renderer=new(){spriteRenderer=sr}}});MimicMapInspection.Configure(_=>throw new Exception("ordinary unit inspected"));ordinary.Update();Near(sr.color.a,ordinary.NativePulseAlpha,"ordinary invisible native unit unchanged");object? scope;MimicMapInspection.BeforePulse(ordinary,out scope);Check(scope is null,"ordinary frame allocates no tracking scope");});
   Case("without Invisible no pulse or art changes",()=>{var a=Create();a.Unit.UnitState.effects.Clear();var child=Overlay(a.Unit);MimicMapInspection.BeforePulse(a.Unit,out var scope);Check(scope is null,"non-Invisible tracked view skips per-part frame work");a.Unit.Update();Near(a.Body.color.a,0,"true body remains hidden");Check(ReferenceEquals(child,Overlay(a.Unit)),"visible disguise unchanged");});
   Case("visibility loss clears copied visual",()=>{var a=Create();var child=Overlay(a.Unit)!;a.Unit.Visible=false;a.Unit.Update();Check(child.Destroyed,"no custom visible body after native visibility loss");});
   Case("hidden from local player clears copied visual",()=>{var a=Create();var child=Overlay(a.Unit)!;a.Unit.Hidden=true;a.Unit.Update();Check(child.Destroyed,"no custom hidden body disclosure");});
   Case("viewer switch clears old private frame",()=>{var a=Create();var child=Overlay(a.Unit)!;GameManager.LocalPlayer.Id=2;a.Unit.Update();Check(child.Destroyed,"old-viewer apparent body retired");});
   Case("state replacement clears old frame",()=>{var a=Create();var child=Overlay(a.Unit)!;GameManager.GameState=new();a.Unit.Update();Check(child.Destroyed,"old-state body retired");});
   Case("native wrapper reuse cannot recolor new owner",()=>{var a=Create();var child=Overlay(a.Unit)!;a.Unit.UnitState.id=77;a.Body.color=new(.1f,.2f,.3f,.8f);a.Unit.Update();Check(child.Destroyed,"pooled unit old child retired");Near(a.Body.color.a,a.Unit.NativePulseAlpha,"new identity retains native pulse");Check(a.Body.color.r==.1f,"new identity RGB preserved");});
   Case("foreign renderer reparent cannot be suppressed",()=>{var a=Create();var child=Overlay(a.Unit)!;a.Body.transform.SetParent(new GameObject("foreign").transform,false);a.Unit.Update();Check(child.Destroyed,"reparented source invalidates custom child");Near(a.Body.color.a,a.Unit.NativePulseAlpha,"foreign renderer retained native alpha");});
   Case("duplicate scope does not intercept native preview capture",()=>{var a=Create();MimicMapInspection.BeforeUnit(a.Unit);object? scope;MimicMapInspection.BeforePulse(a.Unit,out scope);Check(scope is null,"native refresh owner suppresses nested pulse adapter");MimicMapInspection.UnitFinished(null,a.Unit);Near(a.Body.color.a,0,"outer refresh restores apparent hiding");});
   Case("effect removed after prefix still hides any native write",()=>{var a=Create();MimicMapInspection.BeforePulse(a.Unit,out var scope);Check(scope is not null,"hidden frame captured");a.Unit.UnitState.effects.Clear();a.Body.color=a.Body.color with{a=.7f};MimicMapInspection.PulseFinished(null,a.Unit,scope);Near(a.Body.color.a,0,"captured native frame remains suppressed after effect removal");});
   Case("native error preserved and tracked frame retired",()=>{var a=Create();var child=Overlay(a.Unit)!;MimicMapInspection.BeforePulse(a.Unit,out var scope);var failure=new InvalidOperationException("native frame");Check(ReferenceEquals(failure,MimicMapInspection.PulseFinished(failure,a.Unit,scope)),"native exception preserved");Check(child.Destroyed,"failed native frame retires owned visual");});
   Case("removed scope cannot touch replacement view",()=>{var a=Create();MimicMapInspection.BeforePulse(a.Unit,out var old);MimicMapInspection.Clear();MimicMapInspection.Refresh(a.Unit);var child=Overlay(a.Unit);var color=a.Body.color;MimicMapInspection.PulseFinished(null,a.Unit,old);Check(ReferenceEquals(child,Overlay(a.Unit))&&a.Body.color==color,"late old finalizer cannot affect replacement");});
   Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,nativeGame="NOT RUN"}));return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
