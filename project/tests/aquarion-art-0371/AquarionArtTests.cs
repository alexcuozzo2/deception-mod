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
 public static class AquarionArtTests
 {
  static int Cases,Assertions;
  static object? Call(Type type,string name,params object?[] args)=>type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
  static void Check(bool condition,string reason){Assertions++;if(!condition)throw new Exception(reason);}
  static void Near(float a,float b,string reason,float tolerance=.003f)=>Check(Math.Abs(a-b)<tolerance,reason+": "+a+" vs "+b);
  static string Error(Exception e)=>e.Message+(e.InnerException is null?"":" -> "+Error(e.InnerException));
  static void Case(string name,Action action){Call(typeof(UiAdapterTests),"Reset");Call(typeof(MapAdapterTests),"Reset");PrefabManager.Reset();NativeVisualFixture.Reset();NativeSpyPresentation.Ready=_=>true;MimicMapInspection.Configure(MimicPresentationUi.MapView);Cases++;try{action();}catch(Exception e){throw new Exception(name,e);}finally{MimicPresentationState.ReadIdentity=null;MimicPresentationUi.Clear();}}
  static Unit Actor(bool mimic=true)
  {var a=Call(typeof(MapAdapterTests),"Create",true,3u)!;var u=(Unit)a.GetType().GetProperty("Unit")!.GetValue(a)!;if(!mimic){u.UnitState.type=UnitType.warrior;u.UnitState.UnitData=new(UnitType.warrior);u.UnitState.health=100;}return u;}
  static LedgerAppearance App(string form)=>new(form,new("Imperius","Default"),new("Imperius","Default"),new("Imperius","Default"),0x4488cc,form==AquarionCatalog.MimicId? (ushort)10:(ushort)100,form==AquarionCatalog.MimicId?(ushort)10:(ushort)100,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
  static SpriteRenderer[] Art(Unit u)=>u.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name.StartsWith("Deception art part ")).ToArray();
  static void RunCases()
  {
   foreach(bool spy in new[]{false,true})foreach(bool explicitPlayer in new[]{false,true})Case("Aquarion technology preview spy="+spy+" explicit="+explicitPlayer,()=>{
    TechPresentation.Install(new HarmonyLib.Harmony());
    var aqua=new PlayerState{Id=2,tribe=PolytopiaBackendBase.Common.TribeType.Aquarion};
    var t=new Polytopia.Data.TechData{type=spy?Polytopia.Data.TechData.Type.dcp_espionage:Polytopia.Data.TechData.Type.dcp_deception};
    t.unitUnlocks.Add(new(spy?UnitType.dcp_spy:UnitType.dcp_mimic));if(!spy)t.unitUnlocks.Add(new(UnitType.dcp_mimic_form_warrior));var original=t;var list=t.unitUnlocks;
    if(explicitPlayer){bool first=false;TechPresentation.UnlockIconsForPlayer(ref t,aqua,ref first);Check(first,"native first-item selector");}
    else{GameManager.LocalPlayer=aqua;TechPresentation.PopupContent(ref t);}
    Check(!ReferenceEquals(t,original)&&ReferenceEquals(original.unitUnlocks,list)&&list[0].type==(spy?UnitType.dcp_spy:UnitType.dcp_mimic),"technology display never changes real unlocks");
    Check(t.unitUnlocks.Count==1&&t.unitUnlocks[0].type==(spy?UnitType.dcp_spymaid:UnitType.dcp_mermimic),"exact replacement preview");
    var again=t;TechPresentation.PopupContent(ref again);Check(again.unitUnlocks[0].type==t.unitUnlocks[0].type,"nested preview remains a valid category");
   });
   foreach(bool popup in new[]{false,true})foreach(bool delayed in new[]{false,true})Case("final Spymaid technology widget popup="+popup+" delayed="+delayed,()=>{
    TechPresentation.Install(new HarmonyLib.Harmony());UIUtils.CategoryCallbacks=true;SpriteHandle.Delay=delayed;
    GameManager.LocalPlayer.tribe=PolytopiaBackendBase.Common.TribeType.Aquarion;GameManager.Atlas.Head=NativeHeads0371.Get("aquarion");
    var live=new Polytopia.Data.TechData{type=Polytopia.Data.TechData.Type.dcp_espionage};live.unitUnlocks.Add(new(UnitType.dcp_spy));var display=live;
    if(popup)TechPresentation.PopupContent(ref display);else{bool first=false;TechPresentation.UnlockIconsForPlayer(ref display,GameManager.LocalPlayer,ref first);Check(first,"tree retains native first reward");}
    var data=display.unitUnlocks[0];var renderer=UIUtils.GetUIUnitRenderer(data,GameManager.LocalPlayer,GameManager.LocalPlayer.GetVisualSkin());
    Call(typeof(TechArtAdapterTests),"NativeFit",renderer);
    var button=new UIRoundButton_UI2();renderer.transform.SetParent(button.IconContainer,false);MimicControls.TechCategory(button,data);
    if(delayed){Check(MimicControls.PendingPreviewCount==1,"delayed aquatic head retains guarded preview");foreach(var handle in SpriteHandle.Requested)handle.Complete(GameManager.Atlas.Head);MimicControls.Tick();}
    var images=renderer.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();
    Check(renderer.unitType==UnitType.dcp_spymaid&&data.type==UnitType.dcp_spymaid&&button.Text=="Spymaid","final native renderer and reward label must agree");
    Check(images.Length==3&&ReferenceEquals(images[0].mainTexture,((Sprite)UnitArtAssets.Get("spymaid_fixed")).texture)&&ReferenceEquals(images[1].mainTexture,((Sprite)UnitArtAssets.Get("spymaid_team")).texture),"final active Images use original Spymaid body textures");
    Check(images.All(i=>i.gameObject.activeInHierarchy&&i.color.a>0)&&ReferenceEquals(images[2].activeSprite,GameManager.Atlas.Head),"native holder displays complete body and actual aquatic head");
    Call(typeof(TechArtAdapterTests),"SameNativeExtent",renderer);Check(renderer.rectTransform.localScale.x>0,"visible native fit");
    Check(live.unitUnlocks.Count==1&&live.unitUnlocks[0].type==UnitType.dcp_spy&&!ReferenceEquals(display,live),"authoritative unlock data stays unchanged");
    Check(MimicControls.PendingPreviewCount==0&&Entry.Notes.Any(n=>n.Kind=="unit-category-rendered"&&n.Message.Contains("species=dcp_spymaid")&&n.Message.Contains("spymaid_fixed + spymaid_team")),"completed art diagnostic reports actual factory output");
    Check(!Entry.Notes.Any(n=>n.Kind.EndsWith("-error")),"no silent fallback in full preview pipeline");
   });
   Case("same tech definition follows explicit player across hotseat preview factories",()=>{
    TechPresentation.Install(new HarmonyLib.Harmony());UIUtils.CategoryCallbacks=true;
    var aqua=new PlayerState{Id=2,tribe=PolytopiaBackendBase.Common.TribeType.Aquarion};var land=GameManager.LocalPlayer;
    var live=new Polytopia.Data.TechData{type=Polytopia.Data.TechData.Type.dcp_espionage};live.unitUnlocks.Add(new(UnitType.dcp_spy));
    foreach(var player in new[]{aqua,land,aqua}){var display=live;bool first=false;TechPresentation.UnlockIconsForPlayer(ref display,player,ref first);var unit=display.unitUnlocks[0];var r=UIUtils.GetUIUnitRenderer(unit,player,player.GetVisualSkin());var images=r.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();bool aquatic=ReferenceEquals(player,aqua);Check(r.unitType==(aquatic?UnitType.dcp_spymaid:UnitType.dcp_spy),"explicit player species");Check(ReferenceEquals(images[1].mainTexture,((Sprite)UnitArtAssets.Get(aquatic?"spymaid_team":"spy-team")).texture),"recreated widget never inherits prior player body");}
    Check(live.unitUnlocks[0].type==UnitType.dcp_spy,"shared native tech definition unchanged after switching viewers");
   });
   Case("native Spy owned by Aquarion remains genuine Spy outside technology projection",()=>{
    GameManager.LocalPlayer.tribe=PolytopiaBackendBase.Common.TribeType.Aquarion;UIUtils.CategoryCallbacks=true;
    var renderer=UIUtils.GetUIUnitRenderer(new(UnitType.dcp_spy),GameManager.LocalPlayer,GameManager.LocalPlayer.GetVisualSkin());var images=renderer.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();
    Check(renderer.unitType==UnitType.dcp_spy&&ReferenceEquals(images[1].mainTexture,((Sprite)UnitArtAssets.Get("spy-team")).texture),"species is not inferred from an acquired unit owner");
   });
   foreach(var replacement in new[]{UnitType.warrior,UnitType.dcp_spy})Case("pending Spymaid renderer reuse "+replacement,()=>{
    SpriteHandle.Delay=true;GameManager.LocalPlayer.tribe=PolytopiaBackendBase.Common.TribeType.Aquarion;UIUtils.CategoryCallbacks=true;
    var renderer=UIUtils.GetUIUnitRenderer(new(UnitType.dcp_spymaid),GameManager.LocalPlayer,GameManager.LocalPlayer.GetVisualSkin());var old=renderer.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();renderer.unitType=replacement;
    int factories=UIManager.Instance.SpriteDuplicator.FactoryCalls;foreach(var handle in SpriteHandle.Requested)handle.Complete(GameManager.Atlas.Head);MimicControls.Tick();
    Check(renderer.unitType==replacement&&MimicControls.PendingPreviewCount==0&&UIManager.Instance.SpriteDuplicator.FactoryCalls==factories,"late Spymaid head does not paint reassigned native renderer");Check(old.All(i=>i&&!i.gameObject.Destroyed),"cleanup does not destroy reused native children");
   });
   foreach(bool popup in new[]{false,true})Case("scoped final tech argument repairs generic Spy fallback popup="+popup,()=>{
    TechPresentation.Install(new HarmonyLib.Harmony());UIUtils.CategoryCallbacks=true;GameManager.LocalPlayer.tribe=PolytopiaBackendBase.Common.TribeType.Aquarion;
    var tech=new Polytopia.Data.TechData{type=Polytopia.Data.TechData.Type.dcp_espionage};tech.unitUnlocks.Add(new(UnitType.dcp_spy));var live=tech;
    TechPresentation.PreviewScope scope;if(popup)TechPresentation.BeginPopup(ref tech,out scope);else{bool first=false;TechPresentation.BeginTree(ref tech,GameManager.LocalPlayer,ref first,out scope);Check(!first,"Spymaid tree retains room for Blackmail reward");}
    try{
     var downstream=new Polytopia.Data.UnitData(UnitType.dcp_spy);var original=downstream;
     MimicControls.PreviewSpecies(ref downstream,GameManager.LocalPlayer);
     var renderer=UIUtils.GetUIUnitRenderer(downstream,GameManager.LocalPlayer,GameManager.LocalPlayer.GetVisualSkin());var images=renderer.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();
     Check(downstream.type==UnitType.dcp_spymaid&&renderer.unitType==UnitType.dcp_spymaid&&images.Length==3&&ReferenceEquals(images[1].mainTexture,((Sprite)UnitArtAssets.Get("spymaid_team")).texture),"final factory argument and active body agree");
     var rewardData=original;MimicControls.TechSpecies(ref rewardData);var button=new UIRoundButton_UI2();MimicControls.TechCategory(button,rewardData);
     Check(rewardData.type==UnitType.dcp_spymaid&&button.Text=="Spymaid","native popup receives Spymaid data before binding its unit-info callback");
     Check(original.type==UnitType.dcp_spy&&live.unitUnlocks[0].type==UnitType.dcp_spy,"final argument repair cannot edit native prototype or source unlocks");
    }finally{TechPresentation.EndPreview(null,scope);}
    var outside=new Polytopia.Data.UnitData(UnitType.dcp_spy);MimicControls.PreviewSpecies(ref outside,GameManager.LocalPlayer);Check(outside.type==UnitType.dcp_spy,"scope cannot affect genuine Spy preview afterward");
   });
   foreach(string change in new[]{"player","state","registry","tribe","registration","other-tech-unit"})Case("final tech argument context guard "+change,()=>{
    TechPresentation.Install(new HarmonyLib.Harmony());GameManager.LocalPlayer.tribe=PolytopiaBackendBase.Common.TribeType.Aquarion;
    var tech=new Polytopia.Data.TechData{type=Polytopia.Data.TechData.Type.dcp_espionage};tech.unitUnlocks.Add(new(UnitType.dcp_spy));TechPresentation.BeginPopup(ref tech,out var scope);
    try{var player=GameManager.LocalPlayer;var data=new Polytopia.Data.UnitData(change=="other-tech-unit"?UnitType.dcp_mimic:UnitType.dcp_spy);var original=data;
     if(change=="player")player=new(){Id=player.Id,tribe=player.tribe};if(change=="state")GameManager.GameState=new();if(change=="registry")GameManager.GameState.GameLogicData=new();if(change=="tribe")player.tribe=PolytopiaBackendBase.Common.TribeType.Imperius;if(change=="registration")RecruitmentHooks.Registered=false;
     MimicControls.PreviewSpecies(ref data,player);Check(ReferenceEquals(data,original),"foreign/stale/unregistered request remains native");
    }finally{TechPresentation.EndPreview(null,scope);}
   });
   Case("nested unrelated tech blocks outer Spymaid scope and exception restores it",()=>{
    TechPresentation.Install(new HarmonyLib.Harmony());GameManager.LocalPlayer.tribe=PolytopiaBackendBase.Common.TribeType.Aquarion;
    var outer=new Polytopia.Data.TechData{type=Polytopia.Data.TechData.Type.dcp_espionage};outer.unitUnlocks.Add(new(UnitType.dcp_spy));TechPresentation.BeginPopup(ref outer,out var a);
    try{var unrelated=new Polytopia.Data.TechData{type=Polytopia.Data.TechData.Type.riding};TechPresentation.BeginPopup(ref unrelated,out var b);var failure=new Exception("native popup failure");
     try{var native=new Polytopia.Data.UnitData(UnitType.dcp_spy);MimicControls.PreviewSpecies(ref native,GameManager.LocalPlayer);Check(native.type==UnitType.dcp_spy,"unrelated native nested render cannot inherit outer context");}
     finally{Check(ReferenceEquals(TechPresentation.EndPreview(failure,b),failure),"native exception preserved");}
     var again=new Polytopia.Data.UnitData(UnitType.dcp_spy);MimicControls.PreviewSpecies(ref again,GameManager.LocalPlayer);Check(again.type==UnitType.dcp_spymaid,"valid enclosing preview restored");
    }finally{TechPresentation.EndPreview(null,a);}
   });
   Case("lifecycle clear invalidates nested technology preview scopes",()=>{
    TechPresentation.Install(new HarmonyLib.Harmony());GameManager.LocalPlayer.tribe=PolytopiaBackendBase.Common.TribeType.Aquarion;
    var tech=new Polytopia.Data.TechData{type=Polytopia.Data.TechData.Type.dcp_espionage};tech.unitUnlocks.Add(new(UnitType.dcp_spy));TechPresentation.BeginPopup(ref tech,out var a);TechPresentation.BeginPopup(ref tech,out var b);
    MimicControls.Clear();TechPresentation.EndPreview(null,b);TechPresentation.EndPreview(null,a);var data=new Polytopia.Data.UnitData(UnitType.dcp_spy);MimicControls.PreviewSpecies(ref data,GameManager.LocalPlayer);Check(data.type==UnitType.dcp_spy,"old finalizers cannot restore retired preview authority");
   });
   foreach(string tribe in new[]{"imperius","cymanti","aquarion"})foreach(bool aquatic in new[]{false,true})Case("native visible Spy head "+tribe+" aquatic="+aquatic,()=>{
    var head=NativeHeads0371.Get(tribe);GameManager.Atlas.Head=head;GameManager.GameState=new();
    var layers=UnitArtAssets.Layers(GameManager.LocalPlayer,true,aquatic);var h=layers.Single(x=>x.IsHead);var f=UnitArtAssets.Frame(h);
    float visible=(head.FixtureVertices!.Max(v=>v.y)-head.FixtureVertices!.Min(v=>v.y))*h.Height/f.Height;
    Near(visible,341f*2.21f*.9f*(tribe=="cymanti"?.88f:1f),"visible head target");Near(h.Width/h.Height,head.rect.width/head.rect.height,"native aspect retained");
    Check(ReferenceEquals(layers.Last().Sprite,head)&&h.RectMapped,"head must render last and preserve full-rect image frame");
    Check(layers[0].AssetName==(aquatic?"spymaid_fixed":"spy-fixed")&&layers[1].AssetName==(aquatic?"spymaid_team":"spy-team")&&layers[1].Tintable&&!layers[0].Tintable,"body roles");
    Near(layers[0].X,layers[1].X,"body x alignment");Near(layers[0].Y,layers[1].Y,"body y alignment");
    float neck=layers[0].Y+(512-(aquatic?396:382))/1024f*layers[0].Height;
    var method=typeof(UnitArtAssets).GetMethod("HeadContactY",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(object),typeof(float),typeof(float),typeof(float),typeof(float),typeof(bool)},null)!;
    float contact=(float)method.Invoke(null,new object[]{head,h.Width,h.Height,0f,neck,true})!;
    Near(contact-h.Y,5f*2.21f*.9f,"five native pixels down from mesh contact");
    Check(h.Y-h.Height/2<neck,"head overlaps collar");int reads=head.VertexReads;
    for(int i=0;i<30;i++)_=UnitArtAssets.Layers(GameManager.LocalPlayer,true,aquatic);
    Check(head.VertexReads==reads&&head.DestroyCalls==0,"cached native geometry stays borrowed");
   });
   Case("Mermimic one team body and accepted head mask",()=>{
    var normal=UnitArtAssets.Layers(GameManager.LocalPlayer);var aqua=UnitArtAssets.Layers(GameManager.LocalPlayer,aquarion:true);
    Check(aqua.Count==3&&aqua[0].AssetName=="mermimic_body_team"&&aqua[0].Tintable&&aqua[1].IsHead&&aqua[2].AssetName=="mimic_mask","Mermimic uses team-only body");
    Near(aqua[1].Y,normal[2].Y,"accepted head anchor");Near(aqua[1].Width,normal[2].Width,"accepted head size");Near(aqua[2].Width,normal[3].Width,"accepted mask size");
    Near(aqua[0].Y+(0.5f-2916f/3870f)*aqua[0].Height,UnitArtAssets.FootBaseline,"tail baseline");
    var safe=MimicInspectionControls.ActualLayers(GameManager.LocalPlayer,boat:true,aquarion:true);Check(safe[0].AssetName=="mermimic_body_team","amphibious identity cannot select boat art");
   });
   foreach(string id in new[]{AquarionCatalog.MimicId,SpyCatalog.AquarionId})Case("native category "+id,()=>{
    UIUtils.CategoryCallbacks=true;var type=Enum.Parse<UnitType>(id);var data=new Polytopia.Data.UnitData(type);
    var r=UIUtils.GetUIUnitRenderer(data,GameManager.LocalPlayer,PolytopiaBackendBase.Common.SkinType.Default);
    var imgs=r.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();
    Check(imgs.Length==3,"native category receives complete aquatic composition");
    Check(imgs.Any(x=>ReferenceEquals(x.sprite?.texture,((Sprite)UnitArtAssets.Get(id==AquarionCatalog.MimicId?"mermimic_body_team":"spymaid_team")).texture)),"category original team body");
    var button=new UIRoundButton();MimicControls.RendererAssigned(button,r);Check(button.Text==RecruitmentCatalog.Label(id),"category species name");
   });
   foreach(bool water in new[]{false,true})Case("permanent Mermimic actual view water="+water,()=>{
    var u=Actor();var row=new LedgerUnit(17,u.UnitState.id,true,LedgerActualKind.Mimic,true,App("warrior"),PaidInvestmentKind.Known,3,AmphibiousMimic:true);
    MimicPresentationState.ReadIdentity=(_,_)=>row;GameManager.GameState.Map.Add(new(){coordinates=u.UnitState.coordinates,unit=u.UnitState,IsWater=water});
    Check(MimicPresentationState.TryProject(u.UnitState,out var p)&&p.ActualAmphibiousMimic&&!p.ActualMimicBoat&&p.ActualLabel=="Mermimic","persistent species projection");
    var c=new ClientInteraction();c.SelectUnit(u);MimicPresentationUi.Selected(c,u);MimicPresentationUi.BarReady(c.Bar);Check(MimicPresentationUi.ToggleInspection(c,u),"inspection");MimicMapInspection.Refresh(u);
    var art=Art(u);Check(art.Length==3&&ReferenceEquals(art[0].sprite,UnitArtAssets.Get("mermimic_body_team")),"map amphibious body");
    Check(art[0].color.r==ColorUtil.ColorFromInt(u.Owner.color).r,"first team layer tinted correctly");
    var original=u.UnitState.type;u.Hidden=true;MimicMapInspection.Refresh(u);Check(Art(u).Length==0&&u.UnitState.type==original,"hidden cleanup without gameplay type change");
   });
   Case("copied Mermimic appearance does not replace genuine truth",()=>{
    var u=Actor(false);var row=new LedgerUnit(17,u.UnitState.id,true,LedgerActualKind.Genuine,true,App(AquarionCatalog.MimicId),PaidInvestmentKind.Known,3);MimicPresentationState.ReadIdentity=(_,_)=>row;
    MimicMapInspection.Refresh(u);Check(Art(u).Length==3&&ReferenceEquals(Art(u)[0].sprite,UnitArtAssets.Get("mermimic_body_team")),"apparent Mermimic map art");
    var c=new ClientInteraction();c.SelectUnit(u);MimicPresentationUi.Selected(c,u);MimicPresentationUi.BarReady(c.Bar);Check(MimicPresentationUi.ToggleInspection(c,u),"genuine remains inspectable");MimicMapInspection.Refresh(u);
    Check(Art(u).Length==0&&u.UnitState.type==UnitType.warrior&&u.UnitState.health==100,"native Warrior truth retained");
   });
   foreach(bool actualSpy in new[]{false,true})Case("dual Spymaid native colors and current artwork actual="+actualSpy,()=>{
    var u=Actor(false);if(actualSpy){u.UnitState.type=UnitType.dcp_spymaid;u.UnitState.UnitData=new(UnitType.dcp_spymaid);u.UnitState.health=50;}
    var app=App(actualSpy?"warrior":SpyCatalog.AquarionId);var row=new LedgerUnit(17,u.UnitState.id,true,LedgerActualKind.Genuine,true,app,PaidInvestmentKind.Known,3);MimicPresentationState.ReadIdentity=(_,_)=>row;
    var c=new ClientInteraction();c.SelectUnit(u);MimicPresentationUi.Selected(c,u);MimicPresentationUi.BarReady(c.Bar);
    var imgs=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();
    Check(imgs.Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("spymaid_fixed"))),"dual portrait contains current Spymaid fixed body");
    Check(!Entry.Notes.Any(n=>n.Kind=="mimic-private-bar-portrait-error"),"dual construction errors");Check(MimicPresentationUi.ToggleInspection(c,u),"dual toggle accepted");
    imgs=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();Check(imgs.Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("spymaid_team"))),"retoggled dual contains aligned team body");
    Check(u.UnitState.type==(actualSpy?UnitType.dcp_spymaid:UnitType.warrior)&&u.UnitState.health==(actualSpy?50:100),"dual never alters real unit");
   });
   Case("Rogue Spymaid dual keeps remembered body color",()=>{
    var u=Actor(false);u.UnitState.type=UnitType.dcp_spymaid;u.UnitState.UnitData=new(UnitType.dcp_spymaid);u.UnitState.health=50;u.UnitState.owner=2;
    GameManager.LocalPlayer.color=0x224488;GameManager.GameState.Players[1]=GameManager.LocalPlayer;GameManager.GameState.Players[2]=new(){Id=2,color=0x882244};GameManager.GameState.CurrentPlayer=2;NativeRegroupSession.Enabled=true;
    var row=new LedgerUnit(17,u.UnitState.id,true,LedgerActualKind.Genuine,true,App("warrior"),PaidInvestmentKind.Known,3,Rogue:new(1,2,GameManager.GameState.CurrentTurn));MimicPresentationState.ReadIdentity=(_,_)=>row;
    var c=new ClientInteraction();c.SelectUnit(u);MimicPresentationUi.Selected(c,u);MimicPresentationUi.BarReady(c.Bar);
    var team=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().Single(x=>ReferenceEquals(x.sprite!.texture,((Sprite)UnitArtAssets.Get("spymaid_team")).texture));
    Near(team.color.b,ColorUtil.ColorFromInt(GameManager.LocalPlayer.color).b,"remembered owner's actual body color");Check(u.UnitState.owner==2,"portrait cannot change commander");NativeRegroupSession.Enabled=false;
   });
   foreach(string species in new[]{AquarionCatalog.MimicId,SpyCatalog.AquarionId})Case("aquatic prefab container canceled "+species,()=>{
    var u=Actor(false);u.UnitState.type=Enum.Parse<UnitType>(species);u.UnitState.UnitData=new(u.UnitState.type);u.UnitState.health=species==AquarionCatalog.MimicId?(ushort)10:(ushort)50;
    if(species==AquarionCatalog.MimicId){RecruitmentHooks.Forms[species]=new(species,species,"Mermimic",1,1,0,0,true);var row=new LedgerUnit(17,u.UnitState.id,true,LedgerActualKind.Mimic,true,App(species),PaidInvestmentKind.Known,3,AmphibiousMimic:true);MimicPresentationState.ReadIdentity=(_,_)=>row;}
    else MimicPresentationState.ReadIdentity=(_,_)=>null;
    u.spriteContainer.localPosition=new(0,0,0);u.spriteContainer.localScale=new(1,1,1);MimicMapInspection.Refresh(u);var before=Art(u)[0].transform.position;var scale=Art(u)[0].transform.lossyScale;
    MimicMapInspection.Clear();PrefabManager.Reset();
    var prefab=PrefabManager.GetPrefab(UnitType.mermaidwarrior,u.Owner.tribe,u.Owner.skinType);prefab.spriteContainer.localPosition=new(.25f,.4f,0);prefab.spriteContainer.localScale=new(2,3,1);
    u.spriteContainer.localPosition=prefab.spriteContainer.localPosition;u.spriteContainer.localScale=prefab.spriteContainer.localScale;MimicMapInspection.Refresh(u);var after=Art(u)[0].transform.position;var afterScale=Art(u)[0].transform.lossyScale;
    Near(before.x,after.x,"native mermaid container offset x canceled");Near(before.y,after.y,"native mermaid container offset y canceled");Near(scale.x,afterScale.x,"native mermaid scale x canceled");Near(scale.y,afterScale.y,"native mermaid scale y canceled");
    Check(prefab.spriteContainer.localPosition==new Vector3(.25f,.4f,0)&&prefab.spriteContainer.localScale==new Vector3(2,3,1),"borrowed prefab unchanged");
   });
   foreach(bool mimic in new[]{false,true})foreach(bool delayed in new[]{false,true})Case("Rogue popup imagery owner mimic="+mimic+" delayed="+delayed,()=>{
    var u=Actor(mimic);if(!mimic){u.UnitState.type=UnitType.dcp_spymaid;u.UnitState.UnitData=new(UnitType.dcp_spymaid);u.UnitState.health=50;}u.UnitState.owner=2;
    GameManager.LocalPlayer.color=0x224488;GameManager.GameState.Players[1]=GameManager.LocalPlayer;GameManager.GameState.Players[2]=new(){Id=2,color=0x882244};GameManager.GameState.CurrentPlayer=2;NativeRegroupSession.Enabled=true;
    var row=new LedgerUnit(17,u.UnitState.id,true,mimic?LedgerActualKind.Mimic:LedgerActualKind.Genuine,true,App("warrior"),PaidInvestmentKind.Known,3,Rogue:new(1,2,GameManager.GameState.CurrentTurn),AmphibiousMimic:mimic);MimicPresentationState.ReadIdentity=(_,_)=>row;
    SpriteHandle.Delay=delayed;var c=new ClientInteraction();c.SelectUnit(u);MimicPresentationUi.Selected(c,u);MimicPresentationUi.BarReady(c.Bar);Check(MimicPresentationUi.ToggleInspection(c,u),"remembered owner inspection");
    PopupManager.ProductionCallbacks=true;var popup=PopupManager.GetUnitPopup(new(){unit=u});popup.Show();
    if(delayed){foreach(var handle in SpriteHandle.Requested)handle.Complete(GameManager.Atlas.Head);MimicPresentationUi.Tick();}
    var team=popup.unitRenderer.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().Single(x=>ReferenceEquals(x.sprite!.texture,((Sprite)UnitArtAssets.Get(mimic?"mermimic_body_team":"spymaid_team")).texture));
    Near(team.color.b,ColorUtil.ColorFromInt(GameManager.LocalPlayer.color).b,"remembered-owner popup art");
    Check(popup.OwnerAtPopulate==2&&popup.player.Id==2&&popup.PopulationCalls==1&&popup.statsRows.Count==6,"native owner and single stats population preserved");Check(u.UnitState.owner==2,"art keeps real commander");NativeRegroupSession.Enabled=false;
   });
   Case("apparent Spymaid popup keeps copied color independently of Rogue",()=>{
    var u=Actor();u.UnitState.type=UnitType.dcp_mimic_form_dcp_spymaid;u.UnitState.UnitData=new(u.UnitState.type);RecruitmentHooks.Forms[u.UnitState.type.ToString()]=new(SpyCatalog.AquarionId,u.UnitState.type.ToString(),"Spymaid",10,1,0,10,true);u.UnitState.owner=2;
    GameManager.GameState.Players[1]=GameManager.LocalPlayer;GameManager.GameState.Players[2]=new(){Id=2,color=0x882244};GameManager.GameState.CurrentPlayer=2;NativeRegroupSession.Enabled=true;
    var row=new LedgerUnit(17,u.UnitState.id,true,LedgerActualKind.Mimic,true,App(SpyCatalog.AquarionId),PaidInvestmentKind.Known,3,Rogue:new(1,2,GameManager.GameState.CurrentTurn));MimicPresentationState.ReadIdentity=(_,_)=>row;
    var c=new ClientInteraction();c.SelectUnit(u);MimicPresentationUi.Selected(c,u);MimicPresentationUi.BarReady(c.Bar);PopupManager.ProductionCallbacks=true;
    var popup=PopupManager.GetUnitPopup(new(){unit=u});var team=popup.unitRenderer.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().Single(x=>ReferenceEquals(x.sprite!.texture,((Sprite)UnitArtAssets.Get("spymaid_team")).texture));
    Near(team.color.b,ColorUtil.ColorFromInt(row.Appearance!.SourceColor).b,"public disguise keeps donor color");Check(popup.OwnerAtPopulate==2&&popup.PopulationCalls==1&&popup.UnitData.type==UnitType.dcp_spymaid,"public copied stats only");Check(u.UnitState.health==10&&u.UnitState.owner==2,"no true-unit write");NativeRegroupSession.Enabled=false;
   });
   Case("Spymaid acquired by another tribe keeps species",()=>{
    var u=Actor(false);u.UnitState.type=UnitType.dcp_spymaid;u.UnitState.UnitData=new(UnitType.dcp_spymaid);u.UnitState.health=50;MimicPresentationState.ReadIdentity=(_,_)=>null;
    Check(NativeSpyPresentation.TryProject(u,out var p)&&NativeSpyPresentation.IsSpy(p)&&p.ActualLabel=="Spymaid","native Spymaid public identity");MimicMapInspection.Refresh(u);
    var art=Art(u);Check(art.Length==3&&ReferenceEquals(art[0].sprite,UnitArtAssets.Get("spymaid_fixed")),"body does not follow Imperius owner's species");
    Check(art[2].sortingOrder>art[0].sortingOrder&&art[2].sortingOrder>art[1].sortingOrder,"head sorted in front of coat and team layer");
   });
  }
  public static int Run(){try{RunCases();Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions}));}catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases,assertions=Assertions,error=Error(e)}));}return 0;}
  public static int FullUi(){try{_=UiAdapterTests.Run();}catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,error=Error(e)}));}return 0;}
  public static int Presentation(){try{_=Presentation0361Tests.Run();}catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,error=Error(e)}));}return 0;}
 }
}
