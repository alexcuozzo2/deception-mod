// Actual production asset/presentation adapters with synthetic Unity/native ports.
// No assertion here establishes on-screen gameplay acceptance.
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
 public static class FoggerArtTests
 {
  static int Cases,Assertions;
  static object? Call(Type t,string n,params object?[] args)=>t.GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,args);
  static void Check(bool value,string why){Assertions++;if(!value)throw new Exception(why);}
  static void Case(string name,Action action)
  {Cases++;NativeBranchTechUi.ClearLabels();Call(typeof(UiAdapterTests),"Reset");Call(typeof(MapAdapterTests),"Reset");PrefabManager.Reset();NativeVisualFixture.Reset();NativeSpyPresentation.Ready=_=>true;MimicMapInspection.Configure(MimicPresentationUi.MapView);try{action();}catch(Exception e){throw new Exception(name,e);}finally{MimicPresentationState.ReadIdentity=null;MimicPresentationUi.Clear();NativeBranchTechUi.ClearLabels();}}
  static Unit Actor(UnitType kind=UnitType.dcp_motherfogger)
  {var actor=Call(typeof(MapAdapterTests),"Create",true,3u)!;var u=(Unit)actor.GetType().GetProperty("Unit")!.GetValue(actor)!;u.UnitState.type=kind;u.UnitState.UnitData=new(kind);u.UnitState.health=(ushort)(kind==UnitType.dcp_mimic_form_dcp_motherfogger?10:100);return u;}
  static LedgerAppearance Appearance()=>new(FoggerCatalog.Id,new("Imperius","Default"),new("Imperius","Default"),new("Imperius","Default"),0x4488cc,100,100,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
  static SpriteRenderer[] Art(Unit u)=>u.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().Where(r=>r.gameObject.name.StartsWith("Deception art part ")).ToArray();
  public static int Run()
  {
   try
   {
    Case("original cropped artwork has two aligned layers and no borrowed tribe head",()=>{
     var fixedSprite=(Sprite)FoggerArtAssets.Icon();Check(fixedSprite.rect==new Rect(131,335,938,674),"fixed source crop");
     var player=GameManager.LocalPlayer;player.color=0xFF0000;var first=FoggerArtAssets.Layers(player);var blue=new PlayerState{Id=2,color=0x0000FF,tribe=PolytopiaBackendBase.Common.TribeType.Aquarion};var second=FoggerArtAssets.Layers(blue);
     Check(first.Count==2&&first.All(x=>!x.IsHead)&&SpriteHandle.Requested.Count==0,"complete fixed pilot must never request tribe head");
     Check(first.Select(x=>(x.X,x.Y,x.Width,x.Height)).SequenceEqual(second.Select(x=>(x.X,x.Y,x.Width,x.Height))),"tribe altered Fogger geometry");
     Check(ReferenceEquals(first[0].Sprite,second[0].Sprite)&&ReferenceEquals(first[1].Sprite,second[1].Sprite),"immutable art cache diverged by player");
     Check(((Color)first[1].Color).r==1&&((Color)second[1].Color).b==1,"team color not player specific");
     Check(Math.Abs(first[0].Y-first[0].Height/2-UnitArtAssets.FootBaseline)<.001,"feet baseline shifted");
    });
    Case("095 supplied white badge is independent cropped art with a stable cache",()=>{
     var badge=(Sprite)FoggerArtAssets.Badge();var aircraft=(Sprite)FoggerArtAssets.Icon();
     Check(badge.rect==new Rect(107,323,823,346)&&badge.texture.width==1024&&badge.texture.height==1024,"badge crop or original source dimensions changed");
     Check(badge.texture.SourceName.Contains("BB0EA720")&&!ReferenceEquals(badge.texture,aircraft.texture),"badge did not use supplied original PNG");
     Check(badge.MeshType==SpriteMeshType.FullRect&&badge.pixelsPerUnit==100,"badge does not use established native icon decoding");
     int loads=ImageConversion.Loads,reads=Texture2D.Readbacks.Count,creates=Sprite.CreateModes.Count;
     Check(Enumerable.Range(0,100).All(_=>ReferenceEquals(badge,FoggerArtAssets.Badge())),"badge cache changes between units");
     Check(loads==ImageConversion.Loads&&reads==Texture2D.Readbacks.Count&&creates==Sprite.CreateModes.Count,"idle badge refresh decodes or reads back textures");
    });
    Case("alpha mask preserves transparency without retaining near-black source RGB",()=>{
     byte[] rgba={2,3,4,0,8,9,10,96,12,13,14,255,15,16,17,1};var before=(byte[])rgba.Clone();var mask=FoggerArtAssets.AlphaMask(rgba,2,2,2,2);
     Check(mask.Where((b,i)=>i%4==3).SequenceEqual(new byte[]{0,96,255,1}),"source alpha changed");Check(mask.Where((b,i)=>i%4!=3).All(x=>x==255)&&rgba.SequenceEqual(before),"mask damaged source or remained dark");
     bool denied=false;try{FoggerArtAssets.AlphaMask(rgba,2,2,1000,1);}catch(ArgumentException){denied=true;}Check(denied,"mask allocation unbounded");
    });
    Case("idle refresh reuses bounded team mask without GPU readback",()=>{
     var team=(Sprite)FoggerArtAssets.Team();int reads=Texture2D.Readbacks.Count,loads=ImageConversion.Loads,creates=Sprite.CreateModes.Count;
     for(int i=0;i<200;i++)Check(ReferenceEquals(team,FoggerArtAssets.Team()),"mask identity changed");
     Check(reads==Texture2D.Readbacks.Count&&loads==ImageConversion.Loads&&creates==Sprite.CreateModes.Count,"idle allocation/readback");Check(team.rect.width==91&&team.rect.height==97&&team.texture.width==91&&team.texture.height==97,"mask dimensions not bounded");
    });
    foreach(bool aquatic in new[]{false,true})Case("native category and popup fit complete Fogger aquatic="+aquatic,()=>{
     UIUtils.CategoryCallbacks=true;SpriteHandle.Delay=true;if(aquatic)GameManager.LocalPlayer.tribe=PolytopiaBackendBase.Common.TribeType.Aquarion;
     var data=new Polytopia.Data.UnitData(UnitType.dcp_motherfogger);var renderer=UIUtils.GetUIUnitRenderer(data,GameManager.LocalPlayer,GameManager.LocalPlayer.GetVisualSkin());Call(typeof(TechArtAdapterTests),"NativeFit",renderer);
     var button=new UIRoundButton_UI2();renderer.transform.SetParent(button.IconContainer,false);MimicControls.TechCategory(button,data);
     var images=renderer.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();
     Check(images.Length==2&&button.Text=="Fogger"&&images.All(x=>x.gameObject.activeInHierarchy),"native reward does not show complete named aircraft");
     Check(ReferenceEquals(images[0].mainTexture,((Sprite)FoggerArtAssets.Icon()).texture)&&ReferenceEquals(images[1].mainTexture,((Sprite)FoggerArtAssets.Team()).texture),"reward layers wrong");
     Check(MimicControls.PendingPreviewCount==0&&SpriteHandle.Requested.Count==0,"fixed pilot waits for nonexistent head");Call(typeof(TechArtAdapterTests),"SameNativeExtent",renderer);
    });
    Case("actual map and menu use two Fogger layers without changing game fields",()=>{
     var u=Actor();var original=u.UnitState.UnitData;MimicMapInspection.Refresh(u);Check(Art(u).Length==2,"actual Fogger map missing");
     var c=new ClientInteraction();c.SelectUnit(u);MimicPresentationUi.Selected(c,u);MimicPresentationUi.BarReady(c.Bar);
     var images=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();Check(images.Any(x=>ReferenceEquals(x.sprite,FoggerArtAssets.Icon())),"bar aircraft missing");
     PopupManager.ProductionCallbacks=true;var popup=PopupManager.GetUnitPopup(new(){unit=u,unitData=u.UnitState.UnitData});Check(popup.data.type==UnitType.dcp_motherfogger,"popup native type changed");
     Check(popup.abilityButtons.Count==1&&Native.Items(popup.abilityButtons).Cast<UITextButton>().Any(x=>x.Key==MimicAbilityTags.BlackoutNameKey),"Blackout help absent (native Double Attack tag remains native)");
     Check(u.UnitState.type==UnitType.dcp_motherfogger&&ReferenceEquals(u.UnitState.UnitData,original)&&u.UnitState.health==100&&!u.UnitState.moved&&!u.UnitState.attacked,"presentation wrote live game data");
     Check(!Entry.Notes.Any(x=>x.Kind.EndsWith("-error")),"presentation error: "+string.Join(";",Entry.Notes.Select(x=>x.Message)));
    });
    foreach(bool mimic in new[]{false,true})Case("copied Fogger map and portrait use same public art mimic="+mimic,()=>{
     var u=Actor(mimic?UnitType.dcp_mimic_form_dcp_motherfogger:UnitType.warrior);
     if(mimic)RecruitmentHooks.Forms[RecruitmentCatalog.Variant(FoggerCatalog.Id)]=new(FoggerCatalog.Id,RecruitmentCatalog.Variant(FoggerCatalog.Id),"Fogger",10,1,0,1,false);
     var row=new LedgerUnit(1,u.UnitState.id,true,mimic?LedgerActualKind.Mimic:LedgerActualKind.Genuine,true,Appearance(),PaidInvestmentKind.Known,3);MimicPresentationState.ReadIdentity=(_,_)=>row;
     MimicMapInspection.Refresh(u);Check(Art(u).Length==2&&ReferenceEquals(Art(u)[0].sprite,FoggerArtAssets.Icon()),"copied map art mismatch");
     Check(Math.Abs(Art(u)[0].sprite!.bounds.size.x*Art(u)[0].transform.localScale.x-13.2f)<.001,"copied aircraft scale differs from genuine aircraft");
     var c=new ClientInteraction();c.SelectUnit(u);MimicPresentationUi.Selected(c,u);MimicPresentationUi.BarReady(c.Bar);
     Check(c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().Any(x=>ReferenceEquals(x.sprite,FoggerArtAssets.Icon())),"copied dual portrait missing");
     Check(MimicPresentationUi.ToggleInspection(c,u),"dual inspect refused");MimicMapInspection.Refresh(u);Check(Art(u).Length==(mimic?4:0),"actual view retained false Fogger");
     Check(u.UnitState.health==(mimic?10:100),"disguise art edited real HP");
    });
    foreach(bool enemy in new[]{false,true})foreach(bool cargo in new[]{false,true})Case("actual/copied Fogger badge parity enemy="+enemy+" cargo="+cargo,()=>{
     var u=Actor();if(enemy){u.UnitState.owner=2;GameManager.GameState.Players[2]=new(){Id=2};}
     var status=(UnitStatusDisplay)Call(typeof(Presentation0361Tests),"GeometryStatus",u,true)!;
     if(cargo){u.UnitState.type=UnitType.scoutship;u.UnitState.UnitData=new(UnitType.scoutship);u.UnitState.passengerUnit=new(){id=9,type=UnitType.dcp_motherfogger,UnitData=new(UnitType.dcp_motherfogger),health=100};}
     MimicMapInspection.Refresh(u);var original=(SpriteRenderer)Call(typeof(Presentation0361Tests),"OwnedGlyph",status,"mask")!;var before=(Bounds)Call(typeof(Presentation0361Tests),"BadgeDrawn",original,status)!;var icon=original.sprite;
     MimicMapInspection.Clear();Call(typeof(Presentation0361Tests),"CopiedBadge",u,FoggerCatalog.Id,cargo);MimicMapInspection.Refresh(u);
     var shown=enemy?(SpriteRenderer)Call(typeof(Presentation0361Tests),"OwnedGlyph",status,"mask")!:status.typeIcon;var after=(Bounds)Call(typeof(Presentation0361Tests),"BadgeDrawn",shown,status)!;
     Check(ReferenceEquals(icon,FoggerArtAssets.Badge())&&ReferenceEquals(icon,shown.sprite),"opponent can distinguish copied Fogger badge");
     Check(Math.Abs(before.size.x-after.size.x)<.0001&&Math.Abs(before.size.y-after.size.y)<.0001&&Math.Abs(before.center.x-after.center.x)<.0001&&Math.Abs(before.center.y-after.center.y)<.0001,"copy changed badge geometry");
     Check(shown.color.r==1&&shown.color.g==1&&shown.color.b==1,"copied badge lost fixed colors");
     var circle=(Bounds)Call(typeof(Presentation0361Tests),"BadgeDrawn",status.typeBg,status)!;
     Check(Math.Abs(after.size.x/circle.size.x-.68f)<.0001&&Math.Abs(after.size.x/after.size.y-823f/346)<.0001,"aircraft scale enlarged or distorted native badge glyph");
     Check(Math.Abs(after.center.x-circle.center.x)<.0001&&Math.Abs(after.center.y-circle.center.y)<.0001,"supplied badge not centered inside native circle");
    });
    Case("Trade Secrets preserves original beige icon separately from current Blackmail",()=>{
     var trade=(Sprite)TradeSecretsArtAssets.Sprite();var black=(Sprite)BlackmailArtAssets.Sprite();Check(!ReferenceEquals(trade.texture,black.texture),"new Trade Secrets changed current Blackmail");
     Check(trade.texture.SourceName.Contains("8694AA7E")&&((Sprite)TradeSecretsArtAssets.Sprite(true)).texture.SourceName.Contains("4B45ABC8"),"historical original bytes not embedded");
    });
    Case("095 aircraft grows ten percent beyond091 around unchanged feet",()=>{
     var layers=FoggerArtAssets.Layers(GameManager.LocalPlayer);Check(Math.Abs(layers[0].Width-1320)<.001&&Math.Abs(layers[0].Height-674f/938f*1320)<.001,"aircraft not uniformly10% larger than091");
     Check(Math.Abs(layers[1].X-43.5f/938f*1320)<.001&&Math.Abs(layers[1].Width-91f/938f*1320)<.001&&Math.Abs(layers[1].Y-layers[0].Y-40.5f/938f*1320)<.001,"team panel did not grow and align with aircraft");
     var u=Actor();MimicMapInspection.Refresh(u);var body=Art(u)[0];Check(Math.Abs(body.sprite!.bounds.size.x*body.transform.localScale.x-13.2f)<.001,"map normalized away requested size");
     var c=new ClientInteraction();c.SelectUnit(u);MimicPresentationUi.Selected(c,u);MimicPresentationUi.BarReady(c.Bar);var image=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().Single(x=>ReferenceEquals(x.sprite,FoggerArtAssets.Icon()));
     var box=c.Bar.duplicatedSprites.rectTransform.rect;float previous=Math.Min(box.width*.8f/1000,box.height*.8f/1000)*1200;
     Check(Math.Abs(image.rectTransform.rect.width/previous-1.1f)<.001,"menu normalized away aircraft10% increase");
    });
    foreach(bool definition in new[]{false,true})Case("095 popup another ten percent larger remains centered and stable definition="+definition,()=>{
     PopupManager.ProductionCallbacks=true;UIUtils.CategoryCallbacks=true;var u=definition?null:Actor();
     var popup=PopupManager.GetUnitPopup(new(){unit=u,unitData=new(UnitType.dcp_motherfogger)});var r=popup.unitRenderer;
     for(int n=0;n<3;n++)
     {
      var b=UIUtils.GetImageVerticesBounds(r.rectTransform);var scale=r.rectTransform.localScale;var pos=r.rectTransform.anchoredPosition;float expected=Math.Max(popup.iconContainer.sizeDelta.x,popup.iconContainer.sizeDelta.y)*1.2f*1.1f;
      Check(Math.Abs(Math.Max(b.size.x,b.size.y)*scale.x-expected)<.01,"popup aircraft not10% above091 fit");
      Check(Math.Abs(pos.x+b.center.x*scale.x)<.01&&Math.Abs(pos.y+b.center.y*scale.y)<.01,"larger popup moved away from center");
      MimicControls.FitSpyPopup(popup.data,popup.player,r,popup.iconContainer);
     }
    });
    Case("091 Reconnaissance inverts RGB only and preserves alpha without touching input",()=>{
     byte[] pixels={0,0,0,255,255,255,255,128,255,255,0,64,23,61,99,0};var original=(byte[])pixels.Clone();var result=ReconnaissanceArt.Invert(pixels,2,2,2,2);
     Check(result.Take(4).SequenceEqual(new byte[]{255,255,255,255})&&result.Skip(4).Take(4).SequenceEqual(new byte[]{0,0,0,128})&&result.Skip(8).Take(4).SequenceEqual(new byte[]{0,0,255,64}),"inversion changed visible colors or source alpha incorrectly");
     Check(result[15]==0&&pixels.SequenceEqual(original),"inversion filled transparent canvas or wrote input");
     var linear=ReconnaissanceArt.Invert(new byte[]{64,64,64,192},1,1,1,1,true);Check(linear[0]>=116&&linear[0]<=119&&linear[3]==192,"linear readback must invert visible sRGB rather than linear channel");
     bool denied=false;try{ReconnaissanceArt.Invert(new byte[4],1,1,5000,1);}catch(ArgumentException){denied=true;}Check(denied,"unbounded inversion allocation");
    });
    Case("091 Reconnaissance has separate cached texture and same native icon footprint",()=>{
     var original=(Sprite)BranchArtAssets.Get(TechTree.IntelligenceId,true);var texture=original.texture;var inverse=(Sprite)BranchArtAssets.Reconnaissance();
     Check(!ReferenceEquals(original,inverse)&&!ReferenceEquals(texture,inverse.texture)&&ReferenceEquals(texture,((Sprite)BranchArtAssets.Get(TechTree.IntelligenceId,false)).texture),"Surveillance source was replaced by inverse");
     Check(Math.Abs(Math.Max(original.bounds.size.x,original.bounds.size.y)-Math.Max(inverse.bounds.size.x,inverse.bounds.size.y))<.0001,"glow must share existing native icon footprint");
     Check(Math.Abs(original.rect.width/original.rect.height-(inverse.rect.width-2*TechIconGlow.Padding)/(inverse.rect.height-2*TechIconGlow.Padding))<.003,"padded inverse distorted interior artwork aspect");
     Check(inverse.texture.width<=256&&inverse.texture.height<=256&&inverse.texture.MadeNonReadable,"derivative texture is not bounded and immutable");
     int reads=Texture2D.Readbacks.Count,loads=ImageConversion.Loads,creates=Sprite.CreateModes.Count;for(int n=0;n<100;n++)Check(ReferenceEquals(inverse,BranchArtAssets.Reconnaissance()),"inverse cache identity changed");
     Check(reads==Texture2D.Readbacks.Count&&loads==ImageConversion.Loads&&creates==Sprite.CreateModes.Count&&original&&texture,"idle redraw allocated or damaged source");
     UIBasicComponent popup=null!;NativeBranchTechUi.PopupContent(new(){type=Polytopia.Data.TechData.Type.dcp_intelligence},ref popup);var buttons=popup.gameObject.ComponentsInChildren<UIRoundButton_UI2>().ToArray();
     Check(ReferenceEquals(buttons.Single(x=>x.Text.Contains("dcp_tech_insight")).Sprite,inverse)&&ReferenceEquals(buttons.Single(x=>x.Text.Contains("dcp_knowledge")).Sprite,original),"Intelligence rewards don't distinguish inverse Reconnaissance and original Surveillance");
    });
    Case("092 Reconnaissance emits white glow around inverted silhouette without a backplate",()=>{
     const int iw=17,ih=21;var pixels=new byte[iw*ih*4];for(int y=3;y<18;y++)for(int x=4;x<13;x++){int i=(y*iw+x)*4;pixels[i]=20;pixels[i+1]=80;pixels[i+2]=160;pixels[i+3]=255;}
     var before=(byte[])pixels.Clone();var rgba=ReconnaissanceArt.Compose(pixels,iw,ih,iw,ih);int w=iw+2*TechIconGlow.Padding,h=ih+2*TechIconGlow.Padding;
     int At(int x,int y)=>(y*w+x)*4;int center=At(TechIconGlow.Padding+8,TechIconGlow.Padding+10),near=At(TechIconGlow.Padding+3,TechIconGlow.Padding+10),far=At(TechIconGlow.Padding-8,TechIconGlow.Padding+10);
     Check(rgba.Length==w*h*4&&pixels.SequenceEqual(before),"composite changed source pixels or frame");
     Check(rgba.Skip(center).Take(4).SequenceEqual(new byte[]{235,175,95,255}),"glow changed solid inverted artwork");
     Check(rgba.Skip(near).Take(3).All(x=>x==255)&&rgba.Skip(far).Take(3).All(x=>x==255),"glow is not white");
     Check(rgba[near+3]>rgba[far+3]&&rgba[far+3]>0&&rgba[near+3]<255,"glow is a hard border rather than an emanating fade");
     Check(Enumerable.Range(0,w).All(x=>rgba[At(x,0)+3]==0&&rgba[At(x,h-1)+3]==0)&&Enumerable.Range(0,h).All(y=>rgba[At(0,y)+3]==0&&rgba[At(w-1,y)+3]==0),"glow clipped or filled a rectangular background");
     var linear=ReconnaissanceArt.Compose(new byte[]{64,64,64,255},1,1,1,1,true);int mid=(TechIconGlow.Padding*(1+2*TechIconGlow.Padding)+TechIconGlow.Padding)*4;
     Check(linear[mid]>=116&&linear[mid]<=119&&linear[mid+3]==255,"glow double-converted linear readback");
    });
    Case("092 Disguise and Reconnaissance hold independent simultaneously-live glow caches",()=>{
     var disguise=(Sprite)BranchArtAssets.DisguiseTech();var inverse=(Sprite)BranchArtAssets.Reconnaissance();var surveillance=(Sprite)BranchArtAssets.Get(TechTree.IntelligenceId,true);
     Check(!ReferenceEquals(disguise.texture,inverse.texture)&&!ReferenceEquals(surveillance.texture,inverse.texture),"independent icons share a mutable derivative");
     int reads=Texture2D.Readbacks.Count,creates=Sprite.CreateModes.Count;for(int n=0;n<40;n++)Check(ReferenceEquals(disguise,BranchArtAssets.DisguiseTech())&&ReferenceEquals(inverse,BranchArtAssets.Reconnaissance())&&disguise&&disguise.texture&&inverse&&inverse.texture,"one visible reward retired another reward's texture");
     Check(reads==Texture2D.Readbacks.Count&&creates==Sprite.CreateModes.Count&&surveillance&&surveillance.texture,"alternating visible rewards allocate or retire original Surveillance");
    });
    Case("092 Counter Intelligence wraps within unchanged native label rectangle",()=>{
     var item=new TechItem();var text=item.label.TextComponent;text.gameObject=new RectTransform().gameObject;text.text="Counter Intelligence";text.transform.localPosition=new(0,-34,0);((RectTransform)text.transform).sizeDelta=new(90,30);
     var data=new Polytopia.Data.TechData{type=Polytopia.Data.TechData.Type.dcp_camouflage};NativeBranchTechUi.BeforeTreeLabel(item);NativeBranchTechUi.TreeLabel(item,data);
     Check(item.label.ShouldWrapNonAsian&&text.enableWordWrapping&&text.enableAutoSizing,"narrow tech cell still disallows native word wrap");
     Check(Math.Abs(text.fontSizeMin-11.9f)<.001&&text.fontSizeMax==14&&text.fontSize==14,"font fitting exceeded85-100% native range");
     Check(text.text=="Counter Intelligence"&&((RectTransform)text.transform).sizeDelta==new Vector2(90,30)&&text.transform.localPosition==new Vector3(0,-34,0),"fit altered localization or native label placement");
     Check(data.type==Polytopia.Data.TechData.Type.dcp_camouflage,"display changed persisted technology ID");
    });
    Case("092 native font settings restore before pooled label receives unrelated tech",()=>{
     var item=new TechItem();var text=item.label.TextComponent;NativeBranchTechUi.BeforeTreeLabel(item);NativeBranchTechUi.TreeLabel(item,new(){type=Polytopia.Data.TechData.Type.dcp_camouflage});text.fontSize=11.9f;
     NativeBranchTechUi.BeforeTreeLabel(item);NativeBranchTechUi.TreeLabel(item,new(){type=Polytopia.Data.TechData.Type.dcp_intelligence});
     Check(!item.label.ShouldWrapNonAsian&&!text.enableWordWrapping&&!text.enableAutoSizing&&text.fontSize==14&&text.fontSizeMin==18&&text.fontSizeMax==72,"pooled ordinary label retained Counter Intelligence fitting");
     for(int n=0;n<20;n++){NativeBranchTechUi.BeforeTreeLabel(item);NativeBranchTechUi.TreeLabel(item,new(){type=Polytopia.Data.TechData.Type.dcp_camouflage});text.fontSize=12;}
     Check(text.fontSizeMax==14&&Math.Abs(text.fontSizeMin-11.9f)<.001,"repeated tree refresh gradually shrank font");
     NativeBranchTechUi.ClearLabels();Check(text.fontSize==14&&!text.enableAutoSizing&&!item.label.ShouldWrapNonAsian,"tree-close cleanup did not restore original font policy");
    });
    Case("092 label wrapping leaves ordinary techs untouched and tolerates destroyed views",()=>{
     var ordinary=new TechItem();var text=ordinary.label.TextComponent;NativeBranchTechUi.BeforeTreeLabel(ordinary);NativeBranchTechUi.TreeLabel(ordinary,new(){type=Polytopia.Data.TechData.Type.dcp_propaganda});
     Check(!ordinary.label.ShouldWrapNonAsian&&!text.enableWordWrapping&&!text.enableAutoSizing&&text.fontSize==14,"ordinary tech layout changed");
     var stale=new TechItem();NativeBranchTechUi.TreeLabel(stale,new(){type=Polytopia.Data.TechData.Type.dcp_camouflage});UnityEngine.Object.Destroy(stale);UnityEngine.Object.Destroy(stale.label);UnityEngine.Object.Destroy(stale.label.TextComponent);
     NativeBranchTechUi.BeforeTreeLabel(ordinary);NativeBranchTechUi.ClearLabels();Check(!Entry.Notes.Any(x=>x.Kind=="branch-tech-label-error"),"destroyed pooled view caused label cleanup exception");
     RecruitmentHooks.Registered=false;NativeBranchTechUi.TreeLabel(ordinary,new(){type=Polytopia.Data.TechData.Type.dcp_camouflage});Check(!ordinary.label.ShouldWrapNonAsian&&!text.enableAutoSizing,"unregistered branch mutated native widget");
    });
    Case("091 inverse cache repairs loss retires only owned outputs and bounds failures",()=>{
     var source=new Sprite("isolated inverse"){rect=new(32,48),texture=new Texture2D(32,48,TextureFormat.RGBA32,false)};var original=source.texture;var first=(Sprite)ReconnaissanceArt.Get(source);var oldTexture=first.texture;
     UnityEngine.Object.Destroy(oldTexture);var repaired=(Sprite)ReconnaissanceArt.Get(source);Check(first.DestroyCalls==1&&oldTexture.DestroyCalls==1&&repaired&&repaired.texture&&source&&original,"device loss destroyed borrowed art or reused stale result");
     var next=new Sprite("failed inverse"){rect=new(32,48),texture=new Texture2D(32,48,TextureFormat.RGBA32,false)};Graphics.Failure="read";object fallback;try{fallback=ReconnaissanceArt.Get(next);}finally{Graphics.Failure=null;}
     Check(ReferenceEquals(fallback,next)&&repaired.DestroyCalls==1&&repaired.texture.DestroyCalls==1&&source&&original&&next&&next.texture,"failure leaked owned output or destroyed input");
     int reads=Texture2D.Readbacks.Count,notes=Entry.Notes.Count(x=>x.Kind=="reconnaissance-icon-unavailable");for(int n=0;n<20;n++)Check(ReferenceEquals(ReconnaissanceArt.Get(next),next),"failure fallback changed");
     Check(reads==Texture2D.Readbacks.Count&&notes==Entry.Notes.Count(x=>x.Kind=="reconnaissance-icon-unavailable"),"failed source repeatedly reads GPU or logs");
    });
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions}));return 0;
   }
   catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases,assertions=Assertions,error=e.ToString()}));return 1;}
  }
 }
}
