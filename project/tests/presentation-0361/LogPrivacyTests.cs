using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using UnityEngine;
using UnitType=Polytopia.Data.UnitData.Type;
namespace Deception.Tests
{
 public static partial class Presentation0361Tests
 {
  static (string[] Log,string Header,string Health) ObserverLog(UnitType actual,uint id,bool mimic)
  {
   MimicPresentationUi.Clear();Call(typeof(MapAdapterTests),"Reset");PrefabManager.Reset();NativeVisualFixture.Reset();NativeSpyPresentation.Ready=_=>true;MimicMapInspection.Configure(MimicPresentationUi.MapView);
   var a=Actor(mimic);var body=a.Unit.UnitState;GameManager.GameState.Units.Remove(body.id);MapRenderer.Current!.Units.Remove(body.id);body.id=id;GameManager.GameState.Units[id]=body;MapRenderer.Current.Register(a.Unit);
   body.owner=2;GameManager.GameState.Players[2]=new(){Id=2};body.type=actual;body.UnitData=new(actual);body.health=(ushort)(mimic?10:60);
   var row=Row(a.Unit,App("warrior",80,100),mimic);MimicPresentationState.ReadIdentity=(_,_)=>row;
   Entry.Notes.Clear();MimicMapInspection.Refresh(a.Unit);a.Status.SetState(body);
   var interaction=new ClientInteraction();interaction.SelectUnit(a.Unit);MimicPresentationUi.Selected(interaction,a.Unit);MimicPresentationUi.BarReady(interaction.Bar);
   var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",a.Unit)!;
   Check(MimicPresentationUi.MapView(a.Unit)!=true&&!MimicPresentationUi.ToggleInspection(interaction,a.Unit),"ordinary opponent cannot acquire truth view");
   Check(body.type==actual&&body.health==(mimic?10:60)&&body.id==id&&!body.moved&&!body.attacked,"diagnostic removal cannot alter actual identity or allowance");
   if(!mimic)Check(Overlay(a.Unit)?.activeSelf==true,"genuine hidden body still has the public disguise overlay");
   return(Entry.Notes.Select(n=>n.Kind+"\n"+n.Message).ToArray(),popup.Header,popup.statsRows[0].statsValueLabel.text);
  }
  static void LogPrivacyTests()
  {
   Case("A43 equal enemy appearances have equal diagnostics despite true type and ID",()=>{
    var one=ObserverLog(UnitType.rider,317,false);var two=ObserverLog(UnitType.warrior,901,false);
    Check(one.Header==two.Header&&one.Health==two.Health&&one.Health=="8/10","public native popup still projects matching form and false HP");
    Check(one.Log.SequenceEqual(two.Log),"public logs track hidden actual type or persistent native identity: "+string.Join(" | ",one.Log.Concat(two.Log)));
   });
   Case("A43 equal apparent ordinary and Mimic bodies do not log authenticity",()=>{
    var genuine=ObserverLog(UnitType.warrior,71,false);var mimic=ObserverLog(UnitType.dcp_mimic_form_warrior,820,true);
    Check(genuine.Header==mimic.Header&&genuine.Health==mimic.Health,"ordinary and Mimic public popup remain equivalent");
    Check(genuine.Log.SequenceEqual(mimic.Log),"diagnostic event presence discloses hidden Mimic versus genuine identity");
   });
   Case("A43 presentation boundary failures retain opaque bounded event diagnostics",()=>{
    var a=Actor(false);string secret="hidden-body-"+Guid.NewGuid().ToString("N");
    MimicMapInspection.Configure(_=>throw new InvalidOperationException(secret));Entry.Notes.Clear();
    for(int i=0;i<4;i++)MimicMapInspection.Refresh(a.Unit);
    var map=Entry.Notes.Where(n=>n.Kind=="mimic-map-project-error").ToArray();Check(map.Length==1,"map error operation remains available and bounded");Check(!map[0].Message.Contains(secret),"map exception payload must not disclose private context");
    MimicPresentationState.ReadIdentity=(_,_)=>throw new InvalidOperationException(secret);var c=new ClientInteraction();c.SelectUnit(a.Unit);
    for(int i=0;i<4;i++)MimicPresentationUi.Selected(c,a.Unit);
    var ui=Entry.Notes.Where(n=>n.Kind=="mimic-inspection-select-error").ToArray();Check(ui.Length==1,"inspection error operation remains available and bounded");Check(!ui[0].Message.Contains(secret),"inspection exception payload must not disclose private context");
    Check(a.Unit.UnitState.type==UnitType.warrior&&a.Unit.UnitState.health==70&&!a.Unit.UnitState.moved&&!a.Unit.UnitState.attacked,"failed diagnostics must preserve original gameplay state");
   });
  }
 }
}
