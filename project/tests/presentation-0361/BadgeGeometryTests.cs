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
  // Unlike the old badge ports, these renderer bounds come from the actual
  // sprite plus transform. A fixed assigned bounds box hides glyph-size bugs.
  static UnitStatusDisplay GeometryStatus(Unit unit,bool mirrored=false)
  {
   var status=new UnitStatusDisplay();unit.statusDisplay=status;status.SetUnit(unit);status.transform.SetParent(unit.spriteContainer,false);status.Show();
   unit.spriteContainer.localScale=new(2,1.5f,1);status.typeContainer.localPosition=new(2.4f,3.5f,0);
   status.typeContainer.localScale=new(mirrored?-1.3f:1.3f,.8f,1);
   foreach(var r in new[]{status.typeBg,status.typeOutline,status.typeIcon,status.typeIconShadow})
   {r.sharedMaterial=new Material{shader=new Shader{name="Polytopia/Sprite"}};r.sortingLayerID=9;r.sprite=new Sprite("native placeholder"){rect=new(160,80),BoundsCenter=new(.15f,-.08f,0)};}
   status.typeBg.sprite=new Sprite("native circle"){rect=new(100,100),BoundsCenter=new(.03f,-.02f,0)};
   status.typeBg.transform.localScale=new(.7f,.7f,1);status.typeBg.color=new(.2f,.4f,.8f,1);
   status.typeIcon.transform.localScale=new(-.72f,.74f,1);status.typeIcon.transform.localPosition=new(.12f,-.09f,0);status.typeIcon.color=new(.8f,.6f,.4f,1);
   status.typeIconShadow.transform.localScale=new(.74f,.74f,1);status.typeIconShadow.transform.localPosition=new(.15f,-.13f,0);status.typeIconShadow.color=new(0,0,0,191f/255f);
   return status;
  }
  static SpriteRenderer OwnedGlyph(UnitStatusDisplay status,string role)=>status.typeContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Single(r=>!r.gameObject.Destroyed&&r.gameObject.name=="Deception private badge "+role);
  static Bounds BadgeDrawn(SpriteRenderer renderer,UnitStatusDisplay status)
  {
   var b=renderer.bounds;var a=status.typeContainer.InverseTransformPoint(b.min);var z=status.typeContainer.InverseTransformPoint(b.max);
   return new(new((a.x+z.x)/2,(a.y+z.y)/2,0),new(Math.Abs(z.x-a.x),Math.Abs(z.y-a.y),0));
  }
  static bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
  static void SameBadge(Bounds expected,SpriteRenderer renderer,UnitStatusDisplay status,string label)
  {
   var b=BadgeDrawn(renderer,status);Check(Near(expected.size.x,b.size.x)&&Near(expected.size.y,b.size.y),label+" actual drawn extent differs: "+expected+" versus "+b);
   Check(Near(expected.center.x,b.center.x)&&Near(expected.center.y,b.center.y),label+" actual drawn center differs: "+expected+" versus "+b);
  }
  static void CopiedBadge(Unit unit,string form,bool cargo=false)
  {
   bool mimic=form!=RecruitmentCatalog.MenuId&&!cargo;
   unit.UnitState.type=cargo?UnitType.scoutship:mimic?Enum.Parse<UnitType>(RecruitmentCatalog.Variant(form)):UnitType.warrior;unit.UnitState.UnitData=new(unit.UnitState.type);
   if(mimic){unit.UnitState.health=10;RecruitmentHooks.Forms[RecruitmentCatalog.Variant(form)]=new(form,RecruitmentCatalog.Variant(form),RecruitmentCatalog.Label(form),10,1,0,10,false);}
   if(cargo)unit.UnitState.passengerUnit=new(){id=9,type=UnitType.warrior,UnitData=new(UnitType.warrior),health=100};
   var app=App(form,100,100);if(cargo)app=App("transportship",100,100) with{Passenger=app};
   var row=Row(unit,app,mimic);MimicPresentationState.ReadIdentity=(_,_)=>row;
  }
  static void BadgeGeometryTests()
  {
   foreach(bool maid in new[]{false,true})foreach(bool enemy in new[]{false,true})foreach(bool cargo in new[]{false,true})Case("A41 genuine/copied Spy badge geometry maid="+maid+" enemy="+enemy+" cargo="+cargo,()=>{
    var unit=SpyActor(maid,enemy);var status=GeometryStatus(unit,maid);string id=maid?SpyCatalog.AquarionId:SpyCatalog.Id;
    if(cargo){unit.UnitState.type=UnitType.scoutship;unit.UnitState.UnitData=new(UnitType.scoutship);unit.UnitState.passengerUnit=new(){id=9,type=maid?UnitType.dcp_spymaid:UnitType.dcp_spy,UnitData=new(maid?UnitType.dcp_spymaid:UnitType.dcp_spy),health=50};}
    MimicMapInspection.Refresh(unit);var foreground=BadgeDrawn(OwnedGlyph(status,"mask"),status);var shadow=BadgeDrawn(OwnedGlyph(status,"shadow"),status);
    var glyph=OwnedGlyph(status,"mask").sprite;MimicMapInspection.Clear();CopiedBadge(unit,id,cargo);MimicMapInspection.Refresh(unit);
    var shown=enemy?OwnedGlyph(status,"mask"):status.typeIcon;var shownShadow=enemy?OwnedGlyph(status,"shadow"):status.typeIconShadow;
    SameBadge(foreground,shown,status,"copied foreground");SameBadge(shadow,shownShadow,status,"copied shadow");Check(ReferenceEquals(shown.sprite,glyph),"same borrowed glyph, no placeholder-dependent sprite view");
    Check(shown.color.r==1&&shown.color.g==1&&shown.color.b==1,"native packed foreground remains white");
    if(!enemy){Check(status.typeIcon.sharedMaterial!.shader.name=="Polytopia/Sprite"&&status.typeIcon.color.a==1&&status.typeIconShadow.color.a==191f/255f,"native material and packed opacity retained");var c=Select(unit);Toggle(c,unit);SameBadge(foreground,status.typeIcon,status,"actual-view rear foreground");SameBadge(shadow,status.typeIconShadow,status,"actual-view rear shadow");Toggle(c,unit);}
    for(int i=0;i<3;i++)MimicMapInspection.Refresh(unit);
    SameBadge(foreground,enemy?OwnedGlyph(status,"mask"):status.typeIcon,status,"repeated foreground");SameBadge(shadow,enemy?OwnedGlyph(status,"shadow"):status.typeIconShadow,status,"repeated shadow");
    Check(status.typeBg.color.r==.2f&&status.typeBg.color.g==.4f&&status.typeBg.color.b==.8f,"badge background team tint preserved");
   });
   foreach(bool enemy in new[]{false,true})Case("A41 copied exposed Mimic badge geometry enemy="+enemy,()=>{
    var unit=Actor().Unit;var status=GeometryStatus(unit,true);unit.UnitState.type=UnitType.dcp_mimic;unit.UnitState.UnitData=new(UnitType.dcp_mimic);
    RecruitmentHooks.Forms[RecruitmentCatalog.MenuId]=new(RecruitmentCatalog.MenuId,RecruitmentCatalog.MenuId,"Mimic",1,1,0,0,false);
    if(enemy){unit.UnitState.owner=2;GameManager.GameState.Players[2]=new(){Id=2};}
    var row=Row(unit,App(RecruitmentCatalog.MenuId,10,10));MimicPresentationState.ReadIdentity=(_,_)=>row;
    MimicMapInspection.Refresh(unit);var foreground=BadgeDrawn(OwnedGlyph(status,"mask"),status);var shadow=BadgeDrawn(OwnedGlyph(status,"shadow"),status);
    MimicMapInspection.Clear();CopiedBadge(unit,RecruitmentCatalog.MenuId);status.typeIcon.sprite=null;status.typeIconShadow.sprite=null;MimicMapInspection.Refresh(unit);
    SameBadge(foreground,enemy?OwnedGlyph(status,"mask"):status.typeIcon,status,"copied Mimic with missing placeholder");SameBadge(shadow,enemy?OwnedGlyph(status,"shadow"):status.typeIconShadow,status,"copied Mimic shadow");
   });
   foreach(bool refresh in new[]{false,true})Case("A41 async native glyph replacement restores independent geometry refresh="+refresh,()=>{
    var unit=Actor().Unit;var status=GeometryStatus(unit,true);CopiedBadge(unit,SpyCatalog.Id);
    var pos=status.typeIcon.transform.localPosition;var scale=status.typeIcon.transform.localScale;var color=status.typeIcon.color;
    var shadowPos=status.typeIconShadow.transform.localPosition;var shadowScale=status.typeIconShadow.transform.localScale;
    MimicMapInspection.Refresh(unit);var replacement=new Sprite("late native icon"){rect=new(30,65),BoundsCenter=new(-.04f,.1f,0)};status.SetTypeSprite(replacement);
    if(refresh)MimicMapInspection.Refresh(unit);MimicMapInspection.Clear();
    Check(ReferenceEquals(status.typeIcon.sprite,replacement)&&ReferenceEquals(status.typeIconShadow.sprite,replacement),"late SpriteHandle sprite must survive cleanup");
    Check(status.typeIcon.transform.localPosition==pos&&status.typeIcon.transform.localScale==scale&&status.typeIcon.color==color,"owned foreground geometry/RGB restored despite async sprite change");
    Check(status.typeIconShadow.transform.localPosition==shadowPos&&status.typeIconShadow.transform.localScale==shadowScale,"owned shadow geometry restored despite async sprite change");
   });
   Case("A41 native geometry change wins over stale custom cleanup",()=>{
    var unit=Actor().Unit;var status=GeometryStatus(unit);CopiedBadge(unit,SpyCatalog.Id);MimicMapInspection.Refresh(unit);
    var replacement=new Sprite("native reused icon");status.SetTypeSprite(replacement);status.typeIcon.transform.localScale=new(.2f,.3f,1);status.typeIcon.transform.localPosition=new(.5f,.6f,0);status.typeIcon.color=new(.1f,.2f,.3f,.4f);
    MimicMapInspection.Clear();Check(status.typeIcon.transform.localScale==new Vector3(.2f,.3f,1)&&status.typeIcon.transform.localPosition==new Vector3(.5f,.6f,0)&&status.typeIcon.color==new Color(.1f,.2f,.3f,.4f)&&ReferenceEquals(status.typeIcon.sprite,replacement),"native replacement values must not be overwritten");
   });
   Case("A41 ordinary apparent native glyph keeps native geometry",()=>{
    var unit=Actor().Unit;var status=GeometryStatus(unit);var sprite=status.typeIcon.sprite;var scale=status.typeIcon.transform.localScale;var pos=status.typeIcon.transform.localPosition;var color=status.typeIcon.color;
    var row=Row(unit,App("warrior",100,100));MimicPresentationState.ReadIdentity=(_,_)=>row;MimicMapInspection.Refresh(unit);
    Check(ReferenceEquals(status.typeIcon.sprite,sprite)&&status.typeIcon.transform.localScale==scale&&status.typeIcon.transform.localPosition==pos&&status.typeIcon.color==color,"ordinary native glyph cannot acquire custom sizing/tint");
   });
  }
 }
}
