using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using UnityEngine;
using UnitType=Polytopia.Data.UnitData.Type;
using Image=UnityEngine.UI.Image;
namespace Deception.Tests
{
 public static partial class Presentation0361Tests
 {
  static int Cases,Assertions;
  static void Check(bool value,string text){Assertions++;if(!value)throw new Exception(text);}
  static object? Call(Type type,string name,params object?[] args)=>type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
  static LedgerAppearance App(string form="warrior",ushort hp=40,ushort max=100)=>new(form,new("Imperius","Default"),new("Imperius","Default"),new("Imperius","Default"),0x4455aa,hp,max,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
  static LedgerUnit Row(Unit u,LedgerAppearance app,bool mimic=true)=>new(17,u.UnitState.id,true,mimic?LedgerActualKind.Mimic:LedgerActualKind.Genuine,true,app,PaidInvestmentKind.Known,3);
  static (Unit Unit,SpriteRenderer Body,UnitStatusDisplay Status) Actor(bool mimic=true)
  {
   var actor=Call(typeof(MapAdapterTests),"Create",true,3u)!;
   var unit=(Unit)actor.GetType().GetProperty("Unit")!.GetValue(actor)!;var body=(SpriteRenderer)actor.GetType().GetProperty("Body")!.GetValue(actor)!;
   var status=(UnitStatusDisplay)Call(typeof(MapAdapterTests),"AttachBadge",actor)!;status.ProductionHealthCallbacks=true;
   if(!mimic){unit.UnitState.type=UnitType.warrior;unit.UnitState.UnitData=new(UnitType.warrior);unit.UnitState.health=70;}
   return(unit,body,status);
  }
  static ClientInteraction Select(Unit u){var c=new ClientInteraction();c.SelectUnit(u);MimicPresentationUi.Selected(c,u);MimicPresentationUi.BarReady(c.Bar);return c;}
  static void Toggle(ClientInteraction c,Unit u)
  {var button=(UIRoundButton?)MimicInspectionControls.OwnedButton(c.Bar);Check(button?.OnClicked is not null,"owned inspection callback absent");button!.OnClicked!.Click();}
  static GameObject? Overlay(Unit u)=>Enumerable.Range(0,u.spriteContainer.childCount).Select(i=>u.spriteContainer.GetChild(i).gameObject).SingleOrDefault(g=>g.name=="Deception apparent native form"&&!g.Destroyed);
  static void Case(string label,Action body)
  {Call(typeof(UiAdapterTests),"Reset");Call(typeof(MapAdapterTests),"Reset");PrefabManager.Reset();NativeVisualFixture.Reset();NativeSpyPresentation.Ready=_=>true;MimicMapInspection.Configure(MimicPresentationUi.MapView);Cases++;try{body();}catch(Exception e){throw new Exception(label,e);}finally{MimicPresentationState.ReadIdentity=null;MimicPresentationUi.Clear();}}
  static GameObject? RogueRoot(Unit unit)
  {
   var views=(System.Collections.IDictionary)typeof(MimicMapInspection).GetField("RogueViews",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
   var view=views[Native.Pointer(unit)];return view?.GetType().GetField("Root",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(view) as GameObject;
  }
  static Unit SpyActor(bool maid=false,bool enemy=false)
  {
   var a=Actor(false);a.Unit.UnitState.type=maid?UnitType.dcp_spymaid:UnitType.dcp_spy;
   a.Unit.UnitState.UnitData=new(a.Unit.UnitState.type);a.Unit.UnitState.health=30;
   if(enemy){a.Unit.UnitState.owner=2;GameManager.GameState.Players[2]=new(){Id=2,color=0x22aa55};}
   MimicPresentationState.ReadIdentity=(_,_)=>null;return a.Unit;
  }
  static Image[] PopupImages(UnitPopup popup)=>popup.unitRenderer.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();
  // Actual Image rect geometry after the native fitted renderer transform.
  // Deliberately separate from UIUtils.GetImageVerticesBounds: the old bug kept
  // its root fit while replacing large design-space rects with tiny bar rects.
  static Bounds PopupDrawnBounds(UnitPopup popup)
  {
   float lx=float.PositiveInfinity,ly=float.PositiveInfinity,hx=float.NegativeInfinity,hy=float.NegativeInfinity;
   foreach(var image in PopupImages(popup))
   {
    var r=image.rectTransform.rect;
    foreach(var point in new[]{new Vector2(r.x,r.y),new Vector2(r.x+r.width,r.y+r.height)})
    {
     float x=point.x,y=point.y;
     for(Transform? t=image.rectTransform;t is not null&&!ReferenceEquals(t,popup.iconContainer);t=t.parent)
     {var at=t is RectTransform rt?rt.anchoredPosition:new Vector2(t.localPosition.x,t.localPosition.y);x=x*t.localScale.x+at.x;y=y*t.localScale.y+at.y;}
     lx=Math.Min(lx,x);hx=Math.Max(hx,x);ly=Math.Min(ly,y);hy=Math.Max(hy,y);
    }
   }
   return new(new((lx+hx)/2,(ly+hy)/2,0),new(hx-lx,hy-ly,0));
  }
  static void CheckPopupFit(UnitPopup popup,string phase)
  {
   var drawn=PopupDrawnBounds(popup);var box=popup.iconContainer.sizeDelta;float target=Math.Max(box.x,box.y);
   Check(Math.Abs(Math.Max(drawn.size.x,drawn.size.y)-target)<.02f,phase+" portrait must fill native assigned extent, not double-shrink to a few pixels: "+drawn.size+" target="+target);
   Check(Math.Abs(drawn.center.x)<.02f&&Math.Abs(drawn.center.y)<.02f,phase+" portrait must retain native content centering");
   Check(popup.unitRenderer.rectTransform.localScale.x>0&&float.IsFinite(popup.unitRenderer.rectTransform.localScale.x),phase+" renderer scale must be finite and positive");
  }
  public static int Run(){try{
   MimicDefinitionTests();
   LogPrivacyTests();
   BadgeGeometryTests();
   foreach(bool maid in new[]{false,true})foreach(bool enemy in new[]{false,true})foreach(string kind in new[]{"genuine","costume","Mimic costume"})Case("native popup geometry maid="+maid+" enemy="+enemy+" kind="+kind,()=>{
    UIUtils.CategoryCallbacks=true;string id=maid?SpyCatalog.AquarionId:SpyCatalog.Id;Unit unit;
    if(kind=="genuine")unit=SpyActor(maid,enemy);
    else
    {
     bool mimic=kind=="Mimic costume";unit=Actor(mimic).Unit;
     if(mimic){unit.UnitState.type=maid?UnitType.dcp_mimic_form_dcp_spymaid:UnitType.dcp_mimic_form_dcp_spy;unit.UnitState.UnitData=new(unit.UnitState.type);RecruitmentHooks.Forms[RecruitmentCatalog.Variant(id)]=new(id,RecruitmentCatalog.Variant(id),RecruitmentCatalog.Label(id),10,1,0,10,false);}
     if(enemy){unit.UnitState.owner=2;GameManager.GameState.Players[2]=new(){Id=2,color=0x2277aa};}
     var row=Row(unit,App(id,60,100),mimic);MimicPresentationState.ReadIdentity=(_,_)=>row;
    }
    var body=unit.UnitState;var health=body.health;var type=body.type;var art=UnitArtAssets.Layers(unit.Owner,true,maid).ToArray();
    if(!enemy)Select(unit);
    PopupManager.BeforePopulate=()=>PopupManager.LastPopup!.iconContainer.sizeDelta=enemy?new(160,90):new(100,100);
    var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",unit)!;CheckPopupFit(popup,"initial "+kind);
    Check(popup.PopulationCalls==1&&popup.statsRows.Count==6&&popup.OwnerAtPopulate==body.owner,"fitting preserves one native owner-seeded stats population");
    Check(PopupImages(popup).Length==3&&PopupImages(popup).Any(i=>ReferenceEquals(i.sprite!.texture,((Sprite)UnitArtAssets.Get(maid?"spymaid_fixed":"spy-fixed")).texture)),"correct Spy species texture in native fitted popup");
    Check(popup.statsRows[0].statsValueLabel.text==(kind=="genuine"?"3/5":"6/10")&&body.health==health&&body.type==type,"native fit never changes actual or displayed HP policy");
    Check(UnitArtAssets.Layers(unit.Owner,true,maid).Zip(art,(a,b)=>a.Width==b.Width&&a.Height==b.Height&&a.X==b.X&&a.Y==b.Y).All(x=>x),"popup must not alter shared body/head proportions");
    int creates=UIManager.Instance.SpriteDuplicator.FactoryCalls;for(int i=0;i<5;i++)MimicPresentationUi.Tick();CheckPopupFit(popup,"stable");Check(creates==UIManager.Instance.SpriteDuplicator.FactoryCalls,"ready fitted popup does not recreate geometry every frame");
   });
   foreach(bool maid in new[]{false,true})Case("actual masked Spy popup keeps native fit maid="+maid,()=>{
    UIUtils.CategoryCallbacks=true;var unit=SpyActor(maid);var row=Row(unit,App("warrior",40,100),false);MimicPresentationState.ReadIdentity=(_,_)=>row;var c=Select(unit);Toggle(c,unit);
    var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",unit)!;CheckPopupFit(popup,"private actual");Check(popup.Header==(maid?"Spymaid":"Spy")&&popup.statsRows[0].statsValueLabel.text=="3/5","actual selection keeps actual species/HP");
   });
   foreach(bool maid in new[]{false,true})Case("delayed Spy popup head refits once maid="+maid,()=>{
    UIUtils.CategoryCallbacks=true;SpriteHandle.Delay=true;var unit=SpyActor(maid);var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",unit)!;popup.Show();
    Check(PopupImages(popup).Length==1,"pending native head uses normalized glyph");CheckPopupFit(popup,"pending");
    foreach(var h in SpriteHandle.Requested.ToArray())h.Complete(GameManager.Atlas.Head);MimicPresentationUi.Tick();
    Check(PopupImages(popup).Length==3&&popup.PopulationCalls==1,"head completion repaints only art");CheckPopupFit(popup,"completed");
    int creates=UIManager.Instance.SpriteDuplicator.FactoryCalls;MimicPresentationUi.Tick();Check(creates==UIManager.Instance.SpriteDuplicator.FactoryCalls,"completed head fit is stable");
   });
   Case("native fit failure blocks popup and releases its scope",()=>{
    UIUtils.CategoryCallbacks=true;var unit=SpyActor();PopupManager.AfterPopulate=()=>UIUtils.FailFitRoot=PopupManager.LastPopup!.unitRenderer.rectTransform;
    try{var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",unit)!;Check(!MimicPresentationUi.PopupCanShow(popup),"failed native fitting must not show a half-composed popup");}
    finally{UIUtils.FailFitRoot=null;PopupManager.AfterPopulate=null;}
    var next=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",unit)!;CheckPopupFit(next,"recovery");Check(next.PopulationCalls==1,"recovery does not duplicate stats");
   });
   Case("pending Spy popup cannot refit a reparented renderer",()=>{
    UIUtils.CategoryCallbacks=true;SpriteHandle.Delay=true;var unit=SpyActor();var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",unit)!;popup.Show();
    var root=popup.unitRenderer.rectTransform;var other=new RectTransform{rect=new(500,300)};root.SetParent(other,false);root.localScale=new(.7f,.9f,1);root.anchoredPosition=new(31,17);
    var before=PopupImages(popup);int factories=UIManager.Instance.SpriteDuplicator.FactoryCalls,fits=UIUtils.PopupFitCalls;
    foreach(var h in SpriteHandle.Requested.ToArray())h.Complete(GameManager.Atlas.Head);MimicPresentationUi.Tick();
    Check(PopupImages(popup).SequenceEqual(before)&&factories==UIManager.Instance.SpriteDuplicator.FactoryCalls&&fits==UIUtils.PopupFitCalls,"reparented native renderer is no longer owned by pending popup");
    Check(root.localScale==new Vector3(.7f,.9f,1)&&root.anchoredPosition==new Vector2(31,17),"foreign native placement remains untouched");
   });
   Case("closed pending Spy popup never repaints a pooled renderer",()=>{
    UIUtils.CategoryCallbacks=true;SpriteHandle.Delay=true;var unit=SpyActor();var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",unit)!;popup.Show();popup.Hide();popup.ResetPopup();popup.UnitData=new(UnitType.warrior);
    var pooled=PopupImages(popup).ToArray();foreach(var h in SpriteHandle.Requested.ToArray())h.Complete(GameManager.Atlas.Head);MimicPresentationUi.Tick();
    Check(PopupImages(popup).SequenceEqual(pooled)&&PopupImages(popup).All(x=>x.sprite?.Name=="Warrior body"),"late Spy completion must not touch natively reused renderer");
   });

   foreach(bool maid in new[]{false,true})foreach(bool enemy in new[]{false,true})Case("public Spy popup native renderer branch maid="+maid+" enemy="+enemy,()=>{
    var unit=SpyActor(maid,enemy);UIUtils.CategoryCallbacks=true;var native=unit.UnitState;var data=native.UnitData;
    // Before projection this native branch bypasses the category callback and
    // copies the Warrior placeholder, despite correct Spy/Spymaid UnitData.
    var control=PopupManager.GetUnitPopup(new(){unit=unit,unitData=data});
    Check(PopupImages(control).Any(x=>x.sprite?.Name=="Warrior body"),"native live-unit control must reproduce placeholder");
    var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",unit)!;var images=PopupImages(popup);
    string asset=maid?"spymaid_fixed":"spy-fixed";
    Check(popup.PopulationCalls==1&&popup.statsRows.Count==6&&popup.OwnerAtPopulate==unit.UnitState.owner,"public popup keeps one owner-seeded native stat population");
    Check(images.Length==3&&ReferenceEquals(images[0].sprite!.texture,((Sprite)UnitArtAssets.Get(asset)).texture),"public popup must replace Warrior with species artwork");
    Check(popup.Header==(maid?"Spymaid":"Spy")&&popup.statsRows[0].statsValueLabel.text=="3/5","public popup label and actual health preserved");
    Check(popup.unit is null&&popup.unitRenderer.spriteDuplicationData.source.gameObject.Destroyed,"detached static renderer outlives temporary source");
    Check(MimicPresentationUi.PopupCanShow(popup)&&ReferenceEquals(unit.UnitState,native)&&ReferenceEquals(native.UnitData,data)&&native.health==30,"visible public popup grants no gameplay mutation");
    MimicPresentationUi.SelectionCleared();Check(MimicPresentationUi.PopupCanShow(popup),"public popup must not need private selection authority");
    unit.Hidden=true;MimicPresentationUi.Tick();Check(!MimicPresentationUi.PopupCanShow(popup),"hidden public source closes popup");
    popup.ResetPopup();popup.unit=null;popup.UnitData=new(UnitType.warrior);Check(PopupImages(popup).All(x=>x.sprite?.Name=="Warrior body"),"pooled popup reuse keeps native Warrior artwork");
   });
   foreach(bool maid in new[]{false,true})foreach(bool mimic in new[]{false,true})Case("enemy copied Spy single menu and popup maid="+maid+" mimic="+mimic,()=>{
    var a=Actor(mimic);string id=maid?SpyCatalog.AquarionId:SpyCatalog.Id;
    if(mimic){a.Unit.UnitState.type=maid?UnitType.dcp_mimic_form_dcp_spymaid:UnitType.dcp_mimic_form_dcp_spy;a.Unit.UnitState.UnitData=new(a.Unit.UnitState.type);RecruitmentHooks.Forms[RecruitmentCatalog.Variant(id)]=new(id,RecruitmentCatalog.Variant(id),RecruitmentCatalog.Label(id),10,1,0,10,false);}
    a.Unit.UnitState.owner=2;GameManager.GameState.Players[2]=new(){Id=2,color=0x22aa55};
    var app=App(id,60,100) with{SourceColor=0x883322};var row=Row(a.Unit,app,mimic);MimicPresentationState.ReadIdentity=(_,_)=>row;
    var body=a.Unit.UnitState;var c=Select(a.Unit);var images=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();
    string asset=maid?"spymaid_fixed":"spy-fixed";
    Check(images.Length==3&&ReferenceEquals(images[0].sprite,UnitArtAssets.Get(asset)),"enemy single portrait displays costume species only");
    Check(images[1].color==ColorUtil.ColorFromInt(app.SourceColor)&&c.Bar.headerString==RecruitmentCatalog.Label(id),"copied team color retained without private label");
    Check(MimicInspectionControls.OwnedButton(c.Bar) is null&&!MimicPresentationUi.ToggleInspection(c,a.Unit),"enemy gets no inspection or paired truth");
    int factories=UIManager.Instance.SpriteDuplicator.FactoryCalls;for(int i=0;i<6;i++){MimicPresentationUi.BarReady(c.Bar);NativeSpyPresentation.Tick();}
    Check(factories==UIManager.Instance.SpriteDuplicator.FactoryCalls,"value-identical copied appearance does not rebuild each tick");
    var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",a.Unit)!;Check(PopupImages(popup).Length==3&&ReferenceEquals(PopupImages(popup)[0].sprite!.texture,((Sprite)UnitArtAssets.Get(asset)).texture),"enemy popup matches Spy costume");
    Check(popup.statsRows[0].statsValueLabel.text=="6/10"&&popup.Header==RecruitmentCatalog.Label(id),"enemy popup keeps apparent HP and public title");
    Check(ReferenceEquals(a.Unit.UnitState,body)&&body.health==(mimic?10:70)&&ReferenceEquals(row.Appearance,app),"public art changes no real fields or ledger");
    MimicMapInspection.Refresh(a.Unit);Check(a.Status.typeContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Icon(true))),"opponent badge uses Spy species icon");
    app=app with{SourceColor=0x114499};row=row with{Appearance=app};NativeSpyPresentation.Tick();images=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();Check(images[1].color==ColorUtil.ColorFromInt(app.SourceColor),"committed copied cosmetics refresh existing public menu");
    a.Unit.Hidden=true;NativeSpyPresentation.Tick();Check(images.All(x=>x.gameObject.Destroyed||!x.gameObject.activeInHierarchy),"hidden public portrait is retired");
   });
   foreach(string change in new[]{"viewer","client","client state","client unready","map","body","mask","unready"})Case("public Spy popup revokes exact context "+change,()=>{
    var unit=SpyActor();var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",unit)!;Check(MimicPresentationUi.PopupCanShow(popup),"initial popup authorized");
    if(change=="viewer")GameManager.LocalPlayer=new(){Id=2};
    if(change=="client")GameManager.Client=new();
    if(change=="client state")GameManager.Client.GameState=new();
    if(change=="client unready")GameManager.Client.IsReady=false;
    if(change=="map")MapRenderer.Current=new();
    if(change=="body"){unit.UnitState=new(){id=3,type=UnitType.dcp_spy,UnitData=new(UnitType.dcp_spy),health=70};GameManager.GameState.Units[3]=unit.UnitState;}
    if(change=="mask")MimicPresentationState.ReadIdentity=(_,_)=>Row(unit,App(),false);
    if(change=="unready")NativeSpyPresentation.Ready=_=>false;
    Check(!MimicPresentationUi.PopupCanShow(popup),"stale or masked public source must fail closed");
   });
   Case("hidden public Spy cannot be recopied from stale map art",()=>{
    var unit=SpyActor(enemy:true);MapRenderer.ProductionCallbacks=true;MimicMapInspection.Refresh(unit);var c=Select(unit);
    unit.Hidden=true;NativeSpyPresentation.Tick();
    Check(!c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("spy-fixed"))),"visibility cleanup recopied stale public Spy pixels from source");
   });
   Case("new independent mask revokes stale public actual Spy bar",()=>{
    var unit=SpyActor(enemy:true);MapRenderer.ProductionCallbacks=true;MimicMapInspection.Refresh(unit);var c=Select(unit);var old=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();Check(old.Length==3,"public Spy starts painted");
    var row=Row(unit,App("warrior",40,100),false);MimicPresentationState.ReadIdentity=(_,_)=>row;NativeSpyPresentation.Tick();
    Check(!c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("spy-fixed")))&&c.Bar.headerString=="Warrior","new Warrior mask cannot leave actual Spy on enemy menu");
    Check(old.All(x=>x.gameObject.Destroyed)&&MimicInspectionControls.OwnedButton(c.Bar) is null,"mask transition retires owned art without private controls");
   });
   Case("public menu offset preserves exact fitting and restores pooled native art",()=>{
    var unit=SpyActor();var c=Select(unit);var images=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();var layers=MimicInspectionControls.ActualLayers(unit.Owner,spy:true);
    var rect=c.Bar.duplicatedSprites.rectTransform.rect;float fit=Math.Min(rect.width*.8f/UnitArtAssets.CanvasWidth,rect.height*.8f/UnitArtAssets.CanvasHeight);
    for(int i=0;i<3;i++){Check(Math.Abs(images[i].rectTransform.anchoredPosition.y-(layers[i].Y*fit-4))<.001f,"menu has exact -4 UI-unit composition shift");Check(Math.Abs(images[i].rectTransform.rect.width-layers[i].Width*fit)<.001f&&Math.Abs(images[i].rectTransform.rect.height-layers[i].Height*fit)<.001f,"head/body width-height and fit remain unchanged");}
    var native=c.Bar.spriteDuplicationData;NativeSpyPresentation.BeforeBar(c.Bar);Check(c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().Any(x=>x.sprite?.Name=="Warrior body")&&!c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("spy-fixed"))),"owned restore reproduces native source");
    MimicPresentationUi.BarReady(c.Bar);var old=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();
    c.Bar.unit=null;c.Bar.duplicatedSprites.Clear();var replacement=new Image{sprite=new Sprite("pooled other unit")};replacement.rectTransform.SetParent(c.Bar.duplicatedSprites.rectTransform,false);int restores=c.Bar.duplicatedSprites.RestoreCalls;
    NativeSpyPresentation.Clear();Check(!replacement.gameObject.Destroyed&&c.Bar.duplicatedSprites.RestoreCalls==restores,"cleanup must not overwrite a natively reused portrait");
   });
   Case("popup factory exception releases public scope",()=>{
    var unit=SpyActor();PopupManager.ProductionCallbacks=true;PopupManager.BeforePopulate=()=>{PopupManager.BeforePopulate=null;PopupManager.LastPopup!.FailPopulate=true;};
    bool failed=false;try{PopupManager.GetUnitPopup(new(){unit=unit});}catch(InvalidOperationException){failed=true;}Check(failed,"native setter error exercised");
    var ordinary=Actor(false);var popup=PopupManager.GetUnitPopup(new(){unit=ordinary.Unit});Check(PopupImages(popup).Any(x=>x.sprite?.Name=="Warrior body")&&!PopupImages(popup).Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("spy-fixed")))&&popup.data.type==UnitType.warrior,"failed public scope cannot affect later ordinary factory");
   });

   Case("committed apparent health and statuses refresh without toggling",()=>{
    var a=Actor(false);a.Unit.UnitState.effects.Add(UnitEffect.Boosted);var row=Row(a.Unit,App() with{Effects=new[]{"Swift"}},false);MimicPresentationState.ReadIdentity=(_,_)=>row;
    Select(a.Unit);MimicMapInspection.Refresh(a.Unit);Check(a.Unit.EffectParticles[UnitEffect.Swift]&&a.Status.healthLabel.text=="4","initial snapshot rendered");
    row=row with{Appearance=row.Appearance! with{Health=80,Effects=new[]{"Poisoned"}}};MimicMapInspection.Refresh(a.Unit);
    Check(a.Status.healthLabel.text=="8"&&!a.Unit.EffectParticles[UnitEffect.Swift]&&a.Unit.EffectParticles[UnitEffect.Poisoned],"same-focus ledger update refreshes health and native particles");
    row=row with{Independent=false,Appearance=null};MimicMapInspection.Refresh(a.Unit);
    Check(!a.Unit.EffectParticles[UnitEffect.Poisoned]&&a.Unit.EffectParticles[UnitEffect.Boosted],"mask removal restores actual native particles");
   });
   foreach(bool disguised in new[]{false,true})Case("visible Rogue tint and bolts disguise="+disguised,()=>{
    var a=Actor(false);NativeRegroupSession.Enabled=true;GameManager.GameState.Map.Add(new(){coordinates=a.Unit.UnitState.coordinates,unit=a.Unit.UnitState});
    var row=Row(a.Unit,App("cloak",50,50),false) with{Independent=disguised,Appearance=disguised?App("cloak",50,50):null,Rogue=new(2,1,GameManager.GameState.CurrentTurn)};
    MimicPresentationState.ReadIdentity=(_,_)=>row;var original=a.Body.color;var rogue=row.Rogue;row=row with{Rogue=null};MimicMapInspection.Refresh(a.Unit);
    var beforeBody=disguised?Overlay(a.Unit)!.ComponentsInChildren<SpriteRenderer>().First():a.Body;var prior=beforeBody.color;row=row with{Rogue=rogue};
    MimicMapInspection.Refresh(a.Unit);var root=RogueRoot(a.Unit);Check(root?.activeSelf==true&&root.ComponentsInChildren<SpriteRenderer>().Count()==2,"visible Rogue owns two red bolts");
    var bolts=root!.ComponentsInChildren<SpriteRenderer>().ToArray();var scale=bolts[0].transform.lossyScale;
    Check(Math.Abs(Math.Abs(scale.x)-.1365f)<.0001f&&Math.Abs(scale.y-.1505f)<.0001f,"Rogue bolts use modest constant world dimensions near the accepted warrior scale");
    var body=disguised?Overlay(a.Unit)!.ComponentsInChildren<SpriteRenderer>().First():a.Body;var tinted=body.color;
    Check(tinted.g<prior.g&&tinted.a==prior.a,"light red tint preserves native opacity");
    for(int i=0;i<20;i++)MimicMapInspection.Refresh(a.Unit);Check(body.color==tinted&&ReferenceEquals(RogueRoot(a.Unit),root),"repeated map refresh neither accumulates tint nor reallocates bolts");
    a.Unit.Hidden=true;MimicMapInspection.Refresh(a.Unit);Check(root!.Destroyed&&RogueRoot(a.Unit) is null,"native concealment removes public Rogue marker");
    a.Unit.Hidden=false;MimicMapInspection.Refresh(a.Unit);var root2=RogueRoot(a.Unit);row=row with{Rogue=null};MimicMapInspection.Refresh(a.Unit);
    Check(root2!.Destroyed&&RogueRoot(a.Unit) is null,"return or permanent conversion retires status immediately");
    if(!disguised)Check(a.Body.color==original,"return restores exact prior native color");
    NativeRegroupSession.Enabled=false;
   });
   Case("Rogue bolt size stays fixed under large native body scale",()=>{
    var a=Actor(false);NativeRegroupSession.Enabled=true;GameManager.GameState.Map.Add(new(){coordinates=a.Unit.UnitState.coordinates,unit=a.Unit.UnitState});
    var row=Row(a.Unit,App(),false) with{Independent=false,Appearance=null,Rogue=new(2,1,GameManager.GameState.CurrentTurn)};MimicPresentationState.ReadIdentity=(_,_)=>row;
    MimicMapInspection.Refresh(a.Unit);var first=RogueRoot(a.Unit)!.ComponentsInChildren<SpriteRenderer>().First();var size=first.transform.lossyScale;
    a.Unit.transform.localScale=new(4,4,1);MimicMapInspection.Refresh(a.Unit);var after=RogueRoot(a.Unit)!.ComponentsInChildren<SpriteRenderer>().First();Check(after.transform.lossyScale==size,"Large unit inherited bolt scale");NativeRegroupSession.Enabled=false;
   });
   foreach(float facing in new[]{-1f,1f})Case("A34 compact symmetric Rogue bolts facing="+facing,()=>{
    var a=Actor(false);NativeRegroupSession.Enabled=true;GameManager.GameState.Map.Add(new(){coordinates=a.Unit.UnitState.coordinates,unit=a.Unit.UnitState});
    var row=Row(a.Unit,App(),false) with{Independent=false,Appearance=null,Rogue=new(2,1,GameManager.GameState.CurrentTurn)};MimicPresentationState.ReadIdentity=(_,_)=>row;
    a.Unit.transform.position=new(0,0,0);a.Unit.spriteContainer.localScale=new(facing,1,1);
    a.Body.bounds=new(new(0,.1715f,0),new(.28f,.343f,0));a.Unit.SpriteRenderer.Bounds=a.Body.bounds;
    // Native circle_30 is 62px/200PPU at scale .7. Its own status
    // anchor is untouched; this representative raised badge overlaps the
    // old outer bolt horizontally while the corrected pair clears it.
    a.Status.transform.SetParent(a.Unit.transform,false);a.Status.transform.localPosition=new(0,0,0);
    a.Status.typeContainer.localPosition=new(.24f*facing,.35f,0);
    a.Status.typeBg.bounds=new(new(.24f*facing,.35f,0),new(.217f,.217f,0));
    var anchor=a.Status.typeContainer.localPosition;var nativeBody=a.Unit.UnitState;var color=a.Body.color;
    MimicMapInspection.Refresh(a.Unit);var root=RogueRoot(a.Unit)!;var bolts=root.ComponentsInChildren<SpriteRenderer>().OrderBy(x=>x.bounds.center.x).ToArray();
    var left=bolts[0].bounds;var right=bolts[1].bounds;var bubble=a.Status.typeBg.bounds;
    Check(left.size==right.size&&Math.Abs(left.size.x-.08736f)<.00001f&&Math.Abs(left.size.y-.09632f)<.00001f,"Both bolts retain identical compact world footprint");
    Check(Math.Abs(left.center.x+right.center.x)<.00001f&&Math.Abs(left.center.y-right.center.y)<.00001f,"Facing changes cannot make one bolt lower or farther out");
    Check(Math.Abs(left.center.x)<.28f*.38f&&left.center.y>.1715f+.343f*.57f,"Both bolts move inward and upward from the old placement");
    bool Overlap(Bounds x,Bounds y)=>x.min.x<y.max.x&&x.max.x>y.min.x&&x.min.y<y.max.y&&x.max.y>y.min.y;
    Check(!Overlap(left,bubble)&&!Overlap(right,bubble),"Raised side badge covers a Rogue bolt");
    for(int i=0;i<8;i++)MimicMapInspection.Refresh(a.Unit);
    Check(a.Status.typeContainer.localPosition==anchor&&ReferenceEquals(a.Unit.UnitState,nativeBody)&&bolts[0].bounds==left&&bolts[1].bounds==right,"Refresh drifts geometry or moves the native status anchor");
    a.Unit.Hidden=true;MimicMapInspection.Refresh(a.Unit);Check(root.Destroyed&&a.Body.color==color,"Hidden Rogue leaves marker or tint behind");NativeRegroupSession.Enabled=false;
   });
   foreach(float facing in new[]{-1f,1f})foreach(bool actual in new[]{false,true})Case("A34 Mimic badge alpha centered facing="+facing+" actual="+actual,()=>{
    var a=Actor();var row=Row(a.Unit,App());MimicPresentationState.ReadIdentity=(_,_)=>row;
    a.Status.typeContainer.localScale=new(facing*1.2f,.85f,1);var anchor=a.Status.typeContainer.localPosition;var hp=a.Status.healthContainer.localPosition;
    MimicMapInspection.Configure(_=>actual);MimicMapInspection.Refresh(a.Unit);
    var glyph=a.Status.typeContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Single(r=>r.gameObject.name=="Deception private badge mask");
    var background=a.Status.typeContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Single(r=>r.gameObject.name=="Deception private badge background");
    var sprite=glyph.sprite!;var rect=sprite.rect;
    // Measured original PNG alpha bounds in bottom-left texture coordinates:
    // x256..741, y273..790 (exclusive upper edges). Test visible pixels,
    // not the transparent rect's center that previously hid the bias.
    var visibleCenter=glyph.transform.TransformPoint(new((498.5f-rect.x-rect.width*.5f)/sprite.pixelsPerUnit,(531.5f-rect.y-rect.height*.5f)/sprite.pixelsPerUnit,0));
    Check(Math.Abs(visibleCenter.x-background.bounds.center.x)<.00001f&&Math.Abs(visibleCenter.y-background.bounds.center.y)<.00001f,"Visible mask is off-center inside its circle");
    Check(ReferenceEquals(sprite.texture,((Sprite)UnitArtAssets.Icon()).texture)&&rect.height==525&&((Sprite)UnitArtAssets.Icon()).rect.width==494,"Badge frame alters source texture, sizing, or action icon");
    var first=visibleCenter;for(int i=0;i<6;i++)MimicMapInspection.Refresh(a.Unit);Check(glyph.transform.TransformPoint(new(0,0,0))==first,"Repeated facing projection drifts centered mask");
    MimicMapInspection.Clear();Check(a.Status.typeContainer.localPosition==anchor&&a.Status.healthContainer.localPosition==hp,"Badge centering shifts native circle or HP anchor after cleanup");
   });
   Case("Spy visible head is normalized while body canvas stays aligned",()=>{
    var layers=UnitArtAssets.Layers(GameManager.LocalPlayer,true);var native=GameManager.Atlas.Head;float h=Math.Min(470f,500f*native.rect.height/native.rect.width),w=h*native.rect.width/native.rect.height;
    Check(Math.Abs(layers[2].Height-UnitArtAssets.SpyVisibleHeadHeight)<.001f&&Math.Abs(layers[2].Width/layers[2].Height-w/h)<.001f,"Spy visible head target or native aspect differs");
    Check(layers[0].Width==2089.5f&&layers[1].Width==layers[0].Width&&layers[0].X==layers[1].X&&layers[0].Y==layers[1].Y,"Spy body layer alignment changed");
   });
   Case("Rogue remembered owner truth is read-only",()=>{
    var a=Actor(false);a.Unit.UnitState.owner=2;GameManager.GameState.CurrentPlayer=2;GameManager.GameState.Players[2]=new(){Id=2};
    var row=Row(a.Unit,App("cloak",50,50),false) with{Rogue=new(1,2,GameManager.GameState.CurrentTurn)};MimicPresentationState.ReadIdentity=(_,_)=>row;
    var c=Select(a.Unit);Check(MimicInspectionControls.OwnedButton(c.Bar) is not null,"remembered owner can inspect visible Rogue");Toggle(c,a.Unit);
    Check(c.Bar.headerString=="Warrior"&&MimicPresentationUi.MapView(a.Unit)==true,"privileged truth follows remembered owner");
    Check(!MimicPresentationUi.CanSmuggleView(a.Unit)&&!MimicPresentationUi.CanContinueAction(a.Unit,null),"remembered owner has no action authority");
    row=row with{Rogue=null};MimicPresentationUi.BarReady(c.Bar);Check(MimicInspectionControls.OwnedButton(c.Bar) is null&&MimicPresentationUi.MapView(a.Unit)==false,"permanent conversion revokes truth");
   });
   Case("scoped map status never changes gameplay effects",()=>{
    var a=Actor(false);a.Unit.UnitState.effects.Add(UnitEffect.Boosted);var row=Row(a.Unit,App() with{Effects=new[]{"Poisoned","Swift"}},false);MimicPresentationState.ReadIdentity=(_,_)=>row;
    var c=Select(a.Unit);var context=new MapRenderContext{mapData=GameManager.GameState.Map,gld=GameManager.GameState.GameLogicData,unitRegistry=MapRenderer.Current!,localPlayerId=1};
    MimicPresentationUi.BeforeVisualEffects(a.Unit,context,out var scope);bool poison=false,boost=true,invisible=true,foreign=false;
    MimicPresentationUi.VisualEffectRead(a.Unit.UnitState,UnitEffect.Poisoned,ref poison);MimicPresentationUi.VisualEffectRead(a.Unit.UnitState,UnitEffect.Boosted,ref boost);
    MimicPresentationUi.VisualEffectRead(a.Unit.UnitState,UnitEffect.Invisible,ref invisible);MimicPresentationUi.VisualEffectRead(new UnitState(),UnitEffect.Poisoned,ref foreign);
    Check(scope is not null&&poison&&!boost&&invisible&&!foreign,"only exact apparent visual status changes; concealment and other units untouched");
    var error=new Exception("native render failure");Check(ReferenceEquals(MimicPresentationUi.VisualEffectsFinished(error,scope),error),"finalizer preserves render failure");poison=false;MimicPresentationUi.VisualEffectRead(a.Unit.UnitState,UnitEffect.Poisoned,ref poison);Check(!poison,"failed native update retires visual scope");
    MimicPresentationUi.RefreshMapEffects(a.Unit);Check(a.Unit.EffectParticles[UnitEffect.Poisoned]&&a.Unit.EffectParticles[UnitEffect.Swift]&&!a.Unit.EffectParticles[UnitEffect.Boosted],"native visible particles follow apparent statuses");
    Toggle(c,a.Unit);MimicPresentationUi.RefreshMapEffects(a.Unit);Check(a.Unit.EffectParticles[UnitEffect.Boosted]&&!a.Unit.EffectParticles[UnitEffect.Poisoned],"true view restores genuine particles");
    Check(Native.Items(a.Unit.UnitState.effects).Single().Equals(UnitEffect.Boosted),"no true status write");
   });
   Case("status effects are detached apparent snapshots",()=>{
    var a=Actor(false);var real=a.Unit.UnitState;real.effects.Add(UnitEffect.Boosted);
    var app=App() with{Effects=new[]{"Poisoned","Swift"}};var row=Row(a.Unit,app,false);MimicPresentationState.ReadIdentity=(_,_)=>row;
    var c=Select(a.Unit);a.Status.SetState(real);var shown=a.Status.LastState!;
    Check(Native.Items(shown.effects).Contains(UnitEffect.Poisoned)&&Native.Items(shown.effects).Contains(UnitEffect.Swift)&&!Native.Items(shown.effects).Contains(UnitEffect.Boosted),"apparent widget uses copied status only");
    shown.effects.Clear();Check(real.effects.Count==1&&Native.Items(real.effects).Contains(UnitEffect.Boosted)&&app.Effects.Length==2,"UI effect list never aliases true effects or snapshot");
    Toggle(c,a.Unit);a.Status.SetState(real);Check(Native.Items(a.Status.LastState!.effects).Contains(UnitEffect.Boosted)&&!Native.Items(a.Status.LastState.effects).Contains(UnitEffect.Poisoned),"actual widget retains native status");
   });
   foreach(bool boat in new[]{false,true})foreach(bool enemy in new[]{false,true})Case("copied exposed Mimic boat="+boat+" enemy="+enemy,()=>{
    UIUtils.CategoryCallbacks=true;var a=Actor(false);var body=a.Unit.UnitState;body.health=10;
    var app=App(RecruitmentCatalog.MenuId,10,10);
    if(boat){body.type=UnitType.scoutship;body.UnitData=new(body.type);body.passengerUnit=new(){id=9,type=UnitType.warrior,UnitData=new(UnitType.warrior),health=10};GameManager.GameState.Map.Tiles[(body.coordinates.X,body.coordinates.Y)]=new(){IsWater=true};app=App("transportship",10,10) with{Passenger=app};}
    var row=Row(a.Unit,app,false);if(boat)row=row with{Transport=new LedgerTransport(9)};
    if(enemy){body.owner=2;GameManager.GameState.Players[2]=new(){Id=2};}MimicPresentationState.ReadIdentity=(_,_)=>row;
    MimicMapInspection.Refresh(a.Unit);var art=a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name.StartsWith("Deception art part ")).ToArray();
    Check(art.Length==(boat?2:4)&&a.Body.color.a==0,"copied Mimic must draw custom apparent body instead of native Warrior alias");
    Check(ReferenceEquals(art[0].sprite,UnitArtAssets.Get(boat?"mimic_boat_fixed":"mimic-body-fixed")),"apparent body uses correct supplied pixels");
    Check(art[1].color.r==ColorUtil.ColorFromInt(app.SourceColor).r,"copied artwork retains donor team color");
    Check(enemy||ReferenceEquals(a.Status.typeIcon.sprite!.texture,((Sprite)UnitArtAssets.Icon()).texture),"owner paired apparent badge uses Mimic glyph");
    a.Status.SetState(body);Check(a.Status.healthLabel.text=="1"&&a.Status.healthLabel.color.g==1,"full copied1/1 must be healthy white");
    Check(MimicPresentationState.TryProject(body,out var projected)&&!projected.ActualIsMimic&&!projected.Exposed&&projected.ApparentMaxHealth==1&&projected.ActualMaxHealth==10,"apparent Mimic never changes genuine identity or real maximum");
    if(enemy){Check(!MimicPresentationUi.ToggleInspection(new ClientInteraction(),a.Unit),"opponent cannot inspect truth");Check(a.Status.typeIcon.color.a==0,"public copied Mimic has no leaked genuine type glyph");}
    else {
      var c=Select(a.Unit);Check(MimicInspectionControls.OwnedButton(c.Bar) is not null&&c.Bar.headerString==(boat?"Mimic Boat (Disguise)":"Mimic (Disguise)"),"genuine copied Mimic remains separately inspectable");
      var images=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();Check(images.Any(i=>ReferenceEquals(i.sprite,UnitArtAssets.Get(boat?"mimic_boat_fixed":"mimic-body-fixed")))&&images.Length>(boat?2:4),"dual menu retains distinct genuine and copied Mimic art");
      var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",a.Unit)!;
      Check(Native.Items(popup.statsRows).Cast<UnitStatsRow>().Any(r=>r.statsValueLabel.text=="1/1"),"apparent popup reports one of one health");
      Check(popup.unitRenderer.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<Image>().Any(i=>ReferenceEquals(i.sprite,UnitArtAssets.Get(boat?"mimic_boat_fixed":"mimic-body-fixed"))),"apparent popup uses Mimic artwork");
      PopupManager.Current=null;Toggle(c,a.Unit);Check(a.Body.color.a>0&&art.All(r=>r.gameObject.Destroyed||!r.gameObject.activeInHierarchy),"actual view restores genuine body only");
      a.Status.SetState(body);Check(a.Status.healthLabel.text=="1"&&a.Status.healthLabel.color.g==0,"genuine wounded1/10 must remain red");
      Check(row.Independent&&row.Kind==LedgerActualKind.Genuine&&body.health==10&&body.type==(boat?UnitType.scoutship:UnitType.warrior),"view never mutates real stats or independent mask");
      Toggle(c,a.Unit);a.Status.SetState(body);Check(a.Status.healthLabel.color.g==1,"retoggle restores healthy apparent Mimic");
    }
    a.Unit.Hidden=true;MimicMapInspection.Refresh(a.Unit);Check(a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().All(r=>!r.gameObject.name.StartsWith("Deception art part ")),"native invisibility removes custom apparent art");
   });
   foreach(string form in new[]{"cloak","mermaidcloak"})foreach(bool hidden in new[]{false,true})Case("genuine "+form+" hidden="+hidden,()=>{
    var a=Actor(false);var appearance=App(form,50,50) with{Concealment=hidden?ApparentConcealment.Hidden:ApparentConcealment.Visible,Effects=hidden?new[]{"Invisible"}:Array.Empty<string>()};var row=Row(a.Unit,appearance,false);MimicPresentationState.ReadIdentity=(_,_)=>row;
    var c=Select(a.Unit);MimicMapInspection.Refresh(a.Unit);var overlay=Overlay(a.Unit)!;Check(overlay?.activeSelf==true&&a.Body.color.a==0,"apparent Cloak replaces native body");
    var images=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();Check(images.Any(x=>x.sprite?.Name=="Warrior body")&&images.Any(x=>x.sprite?.Name==form+" static body"),"native truth and Cloak portraits must differ");
    Toggle(c,a.Unit);Check(MimicPresentationUi.MapView(a.Unit)==true&&c.Bar.headerString=="Warrior","callback changes to true view");Check(!overlay!.activeSelf&&a.Body.color.a>0&&a.Body.sprite!.Name=="Warrior body","real view must show native Warrior, no Cloak overlay");
    Toggle(c,a.Unit);Check(MimicPresentationUi.MapView(a.Unit)==false&&overlay.activeSelf&&a.Body.color.a==0,"retoggle restores only apparent Cloak");
    Check(a.Unit.UnitState.type==UnitType.warrior&&a.Unit.UnitState.health==70&&row.Appearance==appearance,"inspection never changes real body or concealment ledger");
    a.Unit.Hidden=true;MimicMapInspection.Refresh(a.Unit);Check(Overlay(a.Unit) is null&&!MimicPresentationUi.ToggleInspection(c,a.Unit),"native invisibility denies private truth and removes overlay");
   });
   foreach(string form in new[]{"warrior","transportship","scoutship","cloak_boat"})Case("healthy actual Mimic "+form,()=>{
    var a=Actor();var appearance=App(form);var real=a.Unit.UnitState;
    if(form!="warrior"){
      real.type=Enum.Parse<UnitType>(RecruitmentCatalog.Variant(form));real.UnitData=new(real.type);RecruitmentHooks.Forms[RecruitmentCatalog.Variant(form)]=new(form,RecruitmentCatalog.Variant(form),"Boat",10,2,0,0,false);
      real.passengerUnit=new(){id=9};appearance=appearance with{Passenger=App()};
    }
    var row=Row(a.Unit,appearance);MimicPresentationState.ReadIdentity=(_,_)=>row;var c=Select(a.Unit);
    a.Status.SetState(real);Check(a.Status.healthLabel.text=="4"&&a.Status.healthLabel.color.r==1&&a.Status.healthLabel.color.g==0,"apparent wounded display retains native red");
    Toggle(c,a.Unit);a.Status.SetState(real);Check(a.Status.healthLabel.text=="1"&&a.Status.healthLabel.color==new Color(1,1,1,1),"full actual1/1 must be healthy white");
    Check(real.health==10&&real.UnitData.health==10&&(real.passengerUnit is null||real.passengerUnit.health==10),"HP fix does not alter real data/cargo");
    Toggle(c,a.Unit);a.Status.SetState(real);Check(a.Status.healthLabel.text=="4"&&a.Status.healthLabel.color.g==0,"retoggle restores apparent wound color");
   });
   Case("genuine injured actual HP remains red",()=>{var a=Actor(false);a.Unit.UnitState.health=10;var row=Row(a.Unit,App("cloak",50,50),false);MimicPresentationState.ReadIdentity=(_,_)=>row;var c=Select(a.Unit);Toggle(c,a.Unit);a.Status.SetState(a.Unit.UnitState);Check(a.Status.healthLabel.text=="1"&&a.Status.healthLabel.color.g==0,"oneHP genuine body remains injured");});
   Case("healthy correction preserves native label alpha",()=>{var a=Actor();var row=Row(a.Unit,App());MimicPresentationState.ReadIdentity=(_,_)=>row;var c=Select(a.Unit);Toggle(c,a.Unit);a.Status.healthLabel.color=new(1,0,0,.37f);MimicPresentationUi.HealthColor(a.Status,a.Status.LastState!);Check(a.Status.healthLabel.color==new Color(1,1,1,.37f),"fade alpha preserved");});
   foreach(string condition in new[]{"apparent","opponent","replay","spectator","hidden","wrong-status","foreign-label","foreign-state","changed-health"})Case("color guard "+condition,()=>{
    var a=Actor();var row=Row(a.Unit,App());MimicPresentationState.ReadIdentity=(_,_)=>row;var c=Select(a.Unit);Toggle(c,a.Unit);var display=a.Status.LastState!;
    switch(condition){case "apparent":Toggle(c,a.Unit);break;case "opponent":GameManager.LocalPlayer=new(){Id=2};break;case "replay":GameManager.Client.IsReplay=true;break;case "spectator":GameManager.Client.IsSpectating=true;break;case "hidden":a.Unit.Hidden=true;break;case "wrong-status":a.Unit.statusDisplay=new();break;case "foreign-label":a.Status.healthLabel.transform.SetParent(new GameObject("foreign").transform,false);break;case "foreign-state":display=new(){id=99};break;case "changed-health":display.health=20;break;}
    a.Status.healthLabel.color=new(1,0,0,.42f);MimicPresentationUi.HealthColor(a.Status,display);Check(a.Status.healthLabel.color==new Color(1,0,0,.42f),"denied context does not paint private health");
   });
   Case("Spy category uses supplied layers and native tribe head",()=>{
    UIUtils.CategoryCallbacks=true;var player=GameManager.LocalPlayer;var renderer=UIUtils.GetUIUnitRenderer(new(UnitType.dcp_spy),player,player.GetVisualSkin());
    var images=renderer.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();
    Check(images.Length==3,"Spy category has exactly fixed/team/head layers");
    Check(ReferenceEquals(images[0].sprite!.texture,((Sprite)UnitArtAssets.Get("spy-fixed")).texture)&&ReferenceEquals(images[1].sprite!.texture,((Sprite)UnitArtAssets.Get("spy-team")).texture),"category references original Spy textures");
    Check(ReferenceEquals(images[2].overrideSprite,GameManager.Atlas.Head),"Spy head uses live native tribe atlas");
    var button=new UIRoundButton();MimicControls.RendererAssigned(button,renderer);Check(button.Text=="Spy","native recruitment label stays Spy");
    Check(!Entry.Notes.Any(x=>x.Kind.EndsWith("error")),"category has no recorded error");
   });
   foreach(bool enemy in new[]{false,true})Case("ordinary Spy public art enemy="+enemy,()=>{
    var a=Actor(false);a.Unit.UnitState.type=UnitType.dcp_spy;a.Unit.UnitState.UnitData=new(UnitType.dcp_spy);a.Unit.UnitState.health=50;
    if(enemy){a.Unit.UnitState.owner=2;GameManager.GameState.Players[2]=new(){Id=2,color=0xaa3311};}
    MimicPresentationState.ReadIdentity=(_,_)=>null;a.Status.typeIcon.sprite=null;var nativeColor=a.Body.color;var badgeColor=a.Status.typeIcon.color;
    MimicMapInspection.Refresh(a.Unit);var child=a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name.StartsWith("Deception art part ")).ToArray();
    Check(child.Length==3&&a.Body.color.a==0,"single public Spy uses three artwork layers and hides fallback Warrior");
    Check(ReferenceEquals(child[0].sprite,UnitArtAssets.Get("spy-fixed"))&&ReferenceEquals(child[1].sprite,UnitArtAssets.Get("spy-team")),"map draws supplied Spy pixels");
    Check(child[1].color.r==ColorUtil.ColorFromInt(a.Unit.Owner.color).r,"only team layer uses current owner color");
    var badges=a.Status.typeContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name=="Deception private badge mask").ToArray();
    Check(badges.Length==1&&ReferenceEquals(badges[0].sprite,UnitArtAssets.Icon(true))&&a.Status.typeIcon.color.a==0,"Spy has one supplied glyph at native anchor");
    Check(a.Status.typeContainer.localPosition==new Vector3(2.4f,3.5f,0),"single badge never moves native anchor");
    a.Unit.Hidden=true;MimicMapInspection.Refresh(a.Unit);Check(a.Body.color==nativeColor&&a.Status.typeIcon.color==badgeColor&&child.All(r=>r.gameObject.Destroyed),"visibility revocation restores native state and retires owned art");
    Check(a.Unit.UnitState.type==UnitType.dcp_spy&&a.Unit.UnitState.health==50,"public art never modifies real Spy");
   });
   Case("ordinary Spy bottom single art survives native copy and refresh",()=>{
    var a=Actor(false);a.Unit.UnitState.type=UnitType.dcp_spy;a.Unit.UnitState.UnitData=new(UnitType.dcp_spy);a.Unit.UnitState.health=50;MimicPresentationState.ReadIdentity=(_,_)=>null;
    MapRenderer.ProductionCallbacks=true;MimicMapInspection.Refresh(a.Unit);var c=Select(a.Unit);
    var images=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();
    Check(images.Length==3&&ReferenceEquals(images[0].sprite,UnitArtAssets.Get("spy-fixed"))&&c.Bar.headerString=="Spy","bottom contains only single Spy, not fallback Warrior");
    Check(MimicInspectionControls.OwnedButton(c.Bar) is null,"ordinary Spy has no private inspection toggle");
    int calls=UIManager.Instance.SpriteDuplicator.FactoryCalls;MimicPresentationUi.BarReady(c.Bar);Check(calls==UIManager.Instance.SpriteDuplicator.FactoryCalls,"unchanged public portrait does not rebuild");
    UIManager.Instance.SpriteDuplicator.RenderSprites(c.Bar.spriteDuplicationData);MimicPresentationUi.BarReady(c.Bar);
    Check(c.Bar.duplicatedSprites.rectTransform.childCount==3,"native forced-full-alpha copy is replaced by correct single artwork");
    a.Unit.Owner.color=0x993311;MimicPresentationUi.BarReady(c.Bar);images=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();
    Check(images[1].color.r==ColorUtil.ColorFromInt(0x993311).r,"team-color changes invalidate menu art");
   });
   Case("masked Spy retains separate true art and cloak mask",()=>{
    UIUtils.CategoryCallbacks=true;var a=Actor(false);a.Unit.UnitState.type=UnitType.dcp_spy;a.Unit.UnitState.UnitData=new(UnitType.dcp_spy);a.Unit.UnitState.health=50;
    var row=Row(a.Unit,App("cloak",50,50),false);MimicPresentationState.ReadIdentity=(_,_)=>row;var c=Select(a.Unit);MimicMapInspection.Refresh(a.Unit);Check(Overlay(a.Unit)?.activeSelf==true,"Spy's apparent Cloak is initially shown");
    Toggle(c,a.Unit);Check(c.Bar.headerString=="Spy"&&Overlay(a.Unit) is null,"true view removes apparent Cloak");
    var art=a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name.StartsWith("Deception art part ")).ToArray();
    Check(art.Length==3&&ReferenceEquals(art[0].sprite,UnitArtAssets.Get("spy-fixed")),"true map uses Spy body rather than fallback Warrior");
    var images=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();Check(images.Length==4&&images.Any(i=>ReferenceEquals(i.sprite!.texture,((Sprite)UnitArtAssets.Get("spy-fixed")).texture)),"dual menu combines native Cloak with custom true Spy");
    Toggle(c,a.Unit);Check(Overlay(a.Unit)?.activeSelf==true&&art.All(r=>r.gameObject.Destroyed),"retoggle retires Spy and restores native mask");
   });
   foreach(bool boat in new[]{false,true})foreach(bool enemy in new[]{false,true})Case("exposed Mimic public one-art boat="+boat+" enemy="+enemy,()=>{
    var a=Actor();var body=a.Unit.UnitState;
    string actual=boat?"transportship":RecruitmentCatalog.MenuId;string variant=boat?RecruitmentCatalog.Variant(actual):RecruitmentCatalog.MenuId;
    RecruitmentHooks.Forms[variant]=new(actual,variant,"Mimic",1,1,0,0,false);body.type=Enum.Parse<UnitType>(variant);body.UnitData=new(body.type);
    var app=App(RecruitmentCatalog.MenuId,10,10);
    if(boat){body.passengerUnit=new(){id=9,type=UnitType.dcp_mimic,UnitData=new(UnitType.dcp_mimic)};GameManager.GameState.Map.Tiles[(body.coordinates.X,body.coordinates.Y)]=new(){IsWater=true};app=App("transportship",10,10) with{Passenger=app};}
    var row=Row(a.Unit,app);if(boat)row=row with{Transport=new LedgerTransport(9)};
    if(enemy){body.owner=2;GameManager.GameState.Players[2]=new(){Id=2};}MimicPresentationState.ReadIdentity=(_,_)=>row;
    MimicMapInspection.Refresh(a.Unit);Check(a.Body.color.a==0&&a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Count(r=>r.gameObject.name.StartsWith("Deception art part "))==(boat?2:4),"exposed Mimic has one public actual body");
    Check(a.Status.typeContainer.localPosition==new Vector3(2.4f,3.5f,0)&&a.Status.typeIcon.color.a==0,"one Mimic badge retains original anchor");
    a.Status.SetState(body);Check(a.Status.healthLabel.text=="1"&&a.Status.healthLabel.color.g==1,"exposed1/1 is healthy in public display");
    var c=Select(a.Unit);Check(MimicInspectionControls.OwnedButton(c.Bar) is null&&!MimicPresentationUi.ToggleInspection(c,a.Unit),"exposed unit never offers or accepts appearance toggle");
    Check(c.Bar.duplicatedSprites.rectTransform.childCount==(boat?2:4)&&c.Bar.headerString==(boat?"Mimic Boat":"Mimic"),"single public menu has actual name/art");
   });
   Case("ordinary Spy pool cleanup and readiness alpha",()=>{
    var a=Actor(false);a.Unit.UnitState.type=UnitType.dcp_spy;a.Unit.UnitState.UnitData=new(UnitType.dcp_spy);a.Unit.UnitState.health=50;MimicPresentationState.ReadIdentity=(_,_)=>null;MapRenderer.ProductionCallbacks=true;
    var original=a.Body.color;MimicMapInspection.Refresh(a.Unit);a.Unit.SetAlpha(.2f);
    var art=a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name.StartsWith("Deception art part ")).ToArray();
    Check(art.Length==3&&art.All(x=>Math.Abs(x.color.a-.2f)<.0001f),"Spy retains native spent-move opacity");
    MimicMapInspection.BeforeUnit(a.Unit);a.Unit.UnitState.type=UnitType.warrior;a.Unit.UnitState.UnitData=new(UnitType.warrior);MimicMapInspection.UnitFinished(null,a.Unit);
    Check(art.All(x=>x.gameObject.Destroyed)&&a.Body.color.r==original.r&&Math.Abs(a.Body.color.a-.2f)<.0001f,"pool reuse retires Spy without repairing native opacity");
    Check(a.Status.typeIcon.color.a>0,"pool reuse restores native type badge");
   });
   Case("unprojectable masked Spy never falls back to public truth",()=>{
    var a=Actor(false);a.Unit.UnitState.type=UnitType.dcp_spy;a.Unit.UnitState.UnitData=new(UnitType.dcp_spy);a.Unit.UnitState.health=50;
    var row=Row(a.Unit,App("cloak",50,50),false);MimicPresentationState.ReadIdentity=(_,_)=>row;MimicMapInspection.Configure(_=>null);
    MimicMapInspection.Refresh(a.Unit);Check(!a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Any(r=>r.gameObject.name.StartsWith("Deception art part ")),"failed private projection cannot reveal Spy");
    Check(!NativeSpyPresentation.TryProject(a.Unit,out _),"public art helper refuses independent masks");
   });
   Case("masked Spy sheds to enrolled ordinary Spy artwork",()=>{
    var a=Actor(false);a.Unit.UnitState.type=UnitType.dcp_spy;a.Unit.UnitState.UnitData=new(UnitType.dcp_spy);a.Unit.UnitState.health=50;
    var row=Row(a.Unit,App("cloak",50,50),false);MimicPresentationState.ReadIdentity=(_,_)=>row;MimicMapInspection.Refresh(a.Unit);Check(Overlay(a.Unit)?.activeSelf==true,"masked Spy starts as Cloak");
    row=row with{Independent=false,Appearance=null};MimicMapInspection.Refresh(a.Unit);
    var art=a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name.StartsWith("Deception art part ")).ToArray();
    Check(art.Length==3&&ReferenceEquals(art[0].sprite,UnitArtAssets.Get("spy-fixed"))&&Overlay(a.Unit) is null,"shed restores public Spy without deleting enrollment");
    Check(NativeSpyPresentation.TryProject(a.Unit,out _),"nonindependent unmasked genuine row is ordinary Spy");
   });
   foreach(bool mimic in new[]{false,true})foreach(bool enemy in new[]{false,true})Case("copied Spy artwork mimic="+mimic+" enemy="+enemy,()=>{
    UIUtils.CategoryCallbacks=true;var a=Actor(mimic);
    if(mimic){a.Unit.UnitState.type=UnitType.dcp_mimic_form_dcp_spy;a.Unit.UnitState.UnitData=new(UnitType.dcp_mimic_form_dcp_spy);RecruitmentHooks.Forms[RecruitmentCatalog.Variant(SpyCatalog.Id)]=new(SpyCatalog.Id,RecruitmentCatalog.Variant(SpyCatalog.Id),"Spy",10,1,0,10,false);}
    var appearance=App(SpyCatalog.Id,100,100);var row=Row(a.Unit,appearance,mimic);MimicPresentationState.ReadIdentity=(_,_)=>row;
    if(enemy){a.Unit.UnitState.owner=2;GameManager.GameState.Players[2]=new(){Id=2,color=0x33aa11};}
    MimicMapInspection.Refresh(a.Unit);var art=a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name.StartsWith("Deception art part ")).ToArray();
    Check(art.Length==3&&ReferenceEquals(art[0].sprite,UnitArtAssets.Get("spy-fixed"))&&a.Body.color.a==0,"copied Spy always uses proper public coat");
    Check(art[1].color.r==ColorUtil.ColorFromInt(appearance.SourceColor).r,"Spy disguise keeps copied color independently from real owner");
    if(!enemy){Check(ReferenceEquals(a.Status.typeIcon.sprite!.texture,((Sprite)UnitArtAssets.Icon(true)).texture),"native apparent glyph replaced by normalized Spy icon");var c=Select(a.Unit);Toggle(c,a.Unit);Check(MimicPresentationUi.MapView(a.Unit)==true,"copied Spy toggles to real body");Toggle(c,a.Unit);Check(MimicPresentationUi.MapView(a.Unit)==false,"copied Spy retoggles normally");}
    else Check(a.Status.typeIcon.color.a==0,"opponent sees one custom Spy badge only");
    Check(row.Appearance==appearance,"visual copy does not rewrite appearance");
   });
   Case("unavailable ledger never presents public Spy truth",()=>{
    var a=Actor(false);a.Unit.UnitState.type=UnitType.dcp_spy;a.Unit.UnitState.UnitData=new(UnitType.dcp_spy);a.Unit.UnitState.health=50;MimicPresentationState.ReadIdentity=(_,_)=>null;NativeSpyPresentation.Ready=_=>false;
    Check(!NativeSpyPresentation.TryProject(a.Unit,out _),"null identity from unavailable binding is not authenticated untracked Spy");MimicMapInspection.Refresh(a.Unit);
    Check(!a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Any(r=>r.gameObject.name.StartsWith("Deception art part ")),"unavailable binding supplies no real Spy overlay");
   });
   foreach(string kind in new[]{"ordinary","independent","apparent"})Case("Spy passenger badge "+kind,()=>{
    var a=Actor(false);var body=a.Unit.UnitState;body.type=UnitType.scoutship;body.UnitData=new(UnitType.scoutship);body.health=(ushort)(kind=="apparent"?100:50);
    body.passengerUnit=new(){id=9,type=kind=="apparent"?UnitType.warrior:UnitType.dcp_spy,UnitData=new(kind=="apparent"?UnitType.warrior:UnitType.dcp_spy),health=(ushort)(kind=="apparent"?100:50)};
    var appearance=App("transportship",100,100) with{Passenger=App(kind=="apparent"?SpyCatalog.Id:"warrior",100,100)};
    var row=Row(a.Unit,appearance,false);if(kind=="ordinary")row=row with{Independent=false,Appearance=null};MimicPresentationState.ReadIdentity=(_,_)=>row;
    var before=a.Body.color;MimicMapInspection.Refresh(a.Unit);
    if(kind=="ordinary"){
      Check(a.Body.color==before&&!a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Any(r=>r.gameObject.name.StartsWith("Deception art part ")),"ordinary vessel body remains native");
      Check(a.Status.typeContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Any(r=>r.gameObject.name=="Deception private badge mask"&&ReferenceEquals(r.sprite,UnitArtAssets.Icon(true))),"real Spy passenger has supplied single badge");
      var c=Select(a.Unit);Check(!c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().Any(i=>ReferenceEquals(i.sprite,UnitArtAssets.Get("spy-fixed"))),"vessel menu remains native vessel, never Spy body");
    }else if(kind=="independent"){
      var c=Select(a.Unit);Toggle(c,a.Unit);Check(a.Status.typeContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Any(r=>r.gameObject.name=="Deception private badge mask"&&ReferenceEquals(r.sprite,UnitArtAssets.Icon(true))),"private actual vessel uses real Spy passenger glyph");
    }else Check(ReferenceEquals(a.Status.typeIcon.sprite!.texture,((Sprite)UnitArtAssets.Icon(true)).texture),"apparent vessel uses independent Spy passenger glyph");
    Check(body.type==UnitType.scoutship&&body.health==(kind=="apparent"?100:50)&&body.passengerUnit.health==(kind=="apparent"?100:50),"badge fix never modifies body/cargo stats");
   });
   foreach(string kind in new[]{"Spy","Warrior","Spy passenger","registration off"})Case("Spy action guard "+kind,()=>{
    var unit=new UnitState{type=kind=="Warrior"?UnitType.warrior:kind=="Spy passenger"?UnitType.scoutship:UnitType.dcp_spy};
    if(kind=="Spy passenger")unit.passengerUnit=new(){type=UnitType.dcp_spy};if(kind=="registration off")RecruitmentHooks.Registered=false;
    bool denied=kind=="Spy",result=true;
    Check(NativeSpyActions.NoAttack(unit,ref result)==!denied&&result==!denied,"native Attack guard admits only nonSpy body");result=true;
    Check(NativeSpyActions.NoAttackAny(unit,ref result)==!denied&&result==!denied,"CanAttackAnything guard matches real body");result=true;
    Check(NativeSpyActions.NoRetaliation(unit,ref result)==!denied&&result==!denied,"actual Spy cannot retaliate; real armed vessel can");result=true;
    Check(NativeSpyActions.NoPromotion(unit,ref result)==!denied&&result==!denied,"actual Spy cannot acquire veteran attack stats");
    Check(unit.type==(kind=="Warrior"?UnitType.warrior:kind=="Spy passenger"?UnitType.scoutship:UnitType.dcp_spy)&&unit.health==10,"guards never mutate real body or health");
   });
   Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,nativeGame="NOT RUN"}));return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
