using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using Polytopia.Data;
using UnityEngine;
namespace Deception.Tests
{
    public static class BlackmailPresentationTests
    {
        static int Assertions;static readonly List<string> Results=new();
        static void Check(bool value,string why){Assertions++;if(!value)throw new Exception(why);}
        static object? Private(string name)=>typeof(NativeBlackmailPresentation).GetField(name,BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null);
        static GameObject? Root=>(GameObject?)Private("MapRoot");
        static SpriteRenderer[] Renderers()=>Root?.ComponentsInChildren<SpriteRenderer>().ToArray()??Array.Empty<SpriteRenderer>();
        static void Case(string name,Action action)
        {try{NativeBlackmailPresentation.Clear();MimicMapInspection.Clear();MimicMapInspection.Configure(_=>null);typeof(UiAdapterTests).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);NativeRegroupSession.Enabled=true;NativeBlackmail.Reset();HotSeatOverlay.IsActive=false;NativeExchangeCommands.ValidResult=true;NativeExchangeCommands.Forgotten=0;NativeExchangeCommands.Last=null;NativeSpecialActionUi.Clear();action();Check(!Entry.Notes.Any(n=>n.Kind=="blackmail-presentation-error"||n.Kind.StartsWith("blackmail-unit-")),"Unexpected presentation exception: "+string.Join(";",Entry.Notes.Where(n=>n.Kind=="blackmail-presentation-error").Select(n=>n.Message)));Results.Add(name);}catch(Exception e){throw new Exception(name,e);}finally{NativeBlackmailPresentation.Clear();MimicMapInspection.Clear();}}
        sealed class Scene
        {
            internal GameState State=GameManager.GameState;internal ClientBase Client=GameManager.Client;internal PlayerState Viewer=GameManager.LocalPlayer;
            internal City City=new();internal MapRenderer Map=new();internal Tile Tile;internal TileData Data;internal Unit Unit=new();internal SpriteRenderer Body=new(){sprite=new("Ordinary visible body"),color=new(.8f,.7f,.6f,.5f)};internal ClientInteraction Interaction=new();internal InteractionBar Bar=>Interaction.Bar;
            internal Scene()
            {
                Viewer.Id=1;Viewer.Espionage=true;Viewer.color=0xFF0000;State.Players[1]=Viewer;State.Players[2]=new(){Id=2,color=0x0000FF};State.Players[3]=new(){Id=3,color=0x00FF00};
                MapRenderer.Current=Map;Data=new(){coordinates=new(3,5),owner=2,Improvement=ImprovementData.Type.city};State.Map.Add(Data);
                Tile=new(){Data=Data,Coordinates=Data.coordinates};Tile.overlayRenderer.bounds=new(new(3,4,0),new(2,1,0));Tile.overlayRenderer.transform.position=new(3,4,0);Map.Tiles[(3,5)]=Tile;Tile.fogOfWarRenderer.spriteRenderer=new(){sprite=new("Native fog"){rect=new(200,100)}};Tile.fogOfWarRenderer.spriteRenderer.transform.position=new(3,4,0);
                Tile.Improvement=City;City.Tile=Tile;BindName(City);City.cityRenderer.Coordinates=Data.coordinates;City.cityRenderer.city.transform.position=new(3,4,0);
                for(int i=0;i<2;i++){var plot=new CityPlot();plot.transform.SetParent(City.cityRenderer.city.transform,false);plot.transform.localPosition=new(i==0?-.3f:.3f,0,0);plot.AddNativeHouse("Native house");City.cityRenderer.plots.Add(plot);}
                Unit.UnitState=new(){id=90,owner=2,coordinates=Data.coordinates,home=Data.coordinates,type=UnitData.Type.warrior};Data.unit=Unit.UnitState;State.Units[90]=Unit.UnitState;Map.Register(Unit);Body.transform.SetParent(Unit.spriteContainer,false);Unit.skinVisuals.visualParts=new(new[]{new SkinVisualsReference.VisualPart{renderer=new(){spriteRenderer=Body}}});Unit.statusDisplay=new(){unit=Unit,IsUsed=true};Unit.statusDisplay.healthLabel.text="10";Interaction.SelectUnit(Unit);
            }
            internal CityStatusNameContainer Name=>City.cityOverlay.nameContainer;
            internal void BindName(City city)
            {
                city.cityOverlay.city=city;var name=city.cityOverlay.nameContainer;name.city=city;
                name.transform.position=new(3,3,0);name.bg.transform.SetParent(name.transform,false);name.label.transform.SetParent(name.transform,false);
                name.label.renderer.transform.SetParent(name.label.transform,false);
                name.bg.bounds=new(new(3,3,0),new(2,.3f,0));name.label.renderer.bounds=new(new(3,3.1f,0),new(1.4f,.2f,0));
                name.bg.sortingLayerID=7;name.bg.sortingOrder=30;
            }
            internal CityStatusProgressSegment Population(float center=2.68f,float height=.1f)
            {
                var display=City.cityOverlay;var bar=display.progressBar;bar.transform.SetParent(display.transform,false);bar.gameObject.SetActive(true);
                var segment=new CityStatusProgressSegment();segment.transform.SetParent(bar.transform,false);bar.segments.Add(segment);
                segment.background.transform.SetParent(segment.transform,false);segment.background.bounds=new(new(3,center,0),new(1.4f,height,0));
                segment.dot.transform.SetParent(segment.transform,false);segment.dot.gameObject.SetActive(false);return segment;
            }
            internal void Select()=>NativeBlackmailPresentation.Selected(Interaction);
            internal void UnitMarks(int layers=2){NativeBlackmail.Set(new LedgerBlackmail(1,2,3,5,State.CurrentTurn,UnitId:90,Layers:layers,LastAppliedTurn:State.CurrentTurn));MimicMapInspection.Refresh(Unit);}
            internal GameObject? UnitMark(){var dict=(System.Collections.IDictionary)typeof(MimicMapInspection).GetField("BlackmailViews",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;var value=dict.Values.Cast<object>().SingleOrDefault();return value is null?null:(GameObject?)value.GetType().GetField("Root",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value);}
            internal UIRoundButton Button=>Bar.buttons[0];
            internal void Records(params byte[] actors)=>NativeBlackmail.Set(actors.Select(x=>new LedgerBlackmail(x,2,3,5,State.CurrentTurn)).ToArray());
        }
        sealed class NativeFieldVector
        {public float x,y,z;internal NativeFieldVector(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
        sealed class NativeFieldBounds
        {public object center{get;}=new NativeFieldVector(4,6,0);public object size{get;}=new NativeFieldVector(2,4,0);}
        public static int Run()
        {
            try
            {
                Case("BLACKMAIL-UI-001-original-layer-crop-aligned",()=>{
                    var a=(Sprite)UnitArtAssets.Get("blackmail_fixed");var b=(Sprite)UnitArtAssets.Get("blackmail_team");
                    Check(a.texture!.width==512&&b.texture!.height==512,"Original canvases changed");Check(a.rect.x==44&&a.rect.y==21&&a.rect.width==421&&a.rect.height==457&&a.rect.Equals(b.rect),"Layer crops no longer align");
                    var fixedView=(Sprite)BlackmailArtAssets.Sprite();var teamView=(Sprite)BlackmailArtAssets.Sprite(true);Check(fixedView.pixelsPerUnit==teamView.pixelsPerUnit&&fixedView.rect.Equals(teamView.rect),"Normalized layer frames diverge");
                });
                Case("BLACKMAIL-UI-002-tech-unlocks-include-both-viewer-colored",()=>{
                    var s=new Scene();var nativeSpy=new RectTransform();var items=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<RectTransform>(new[]{nativeSpy});NativeBranchTechUi.UnlockItems(new(){type=TechData.Type.dcp_espionage},ref items,s.Viewer,false);
                    Check(items.Length==2&&ReferenceEquals(items[0],nativeSpy),"Espionage must retain native unit reward before Blackmail");var images=items[1].gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();Check(images.Length==2&&images[1].color.r==1&&images[1].color.g==0,"Tech crown was not viewer red");Check(images.All(x=>!x.raycastTarget&&x.preserveAspect),"Artwork intercepts native clicks or loses aspect");
                    Check(ReferenceEquals(images[0].sprite,BlackmailArtAssets.Sprite())&&ReferenceEquals(images[1].sprite,BlackmailArtAssets.Sprite(true)),"Wrong original layers");
                });
                Case("BLACKMAIL-UI-003-research-first-only-preserved",()=>{
                    var s=new Scene();var nativeSpy=new RectTransform();var items=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<RectTransform>(new[]{nativeSpy});NativeBranchTechUi.UnlockItems(new(){type=TechData.Type.dcp_espionage},ref items,s.Viewer,true);Check(items.Length==1,"Native first-only research reaction changed");
                });
                Case("BLACKMAIL-UI-004-popup-two-owned-widgets-and-descriptions",()=>{
                    var s=new Scene();UIBasicComponent result=null!;var tech=new TechData{type=TechData.Type.dcp_espionage};tech.unitUnlocks.Add(new(UnitData.Type.dcp_spy));NativeBranchTechUi.PopupContent(tech,ref result);var buttons=result.gameObject.ComponentsInChildren<UIRoundButton_UI2>().ToArray();
                    Check(buttons.Length==2&&buttons[0].NativeUnitData?.type==UnitData.Type.dcp_spy&&buttons[1].Text=="Localized[ability.names.dcp_blackmail]","Unlock names or order changed");Check(buttons[1].InfoDescription=="Localized[ability.descriptions.dcp_blackmail]","Blackmail explanation missing");
                    Check(buttons[0].rectTransform.anchoredPosition.x<0&&buttons[1].rectTransform.anchoredPosition.x>0&&result.GetWidth()==224,"Native widgets overlap");
                    UnityEngine.Object.Destroy(result.gameObject);Check(!buttons[0]&&!buttons[1],"Container did not own both ability widgets");
                });
                Case("BLACKMAIL-UI-005-default-tribe-color-resolved",()=>{var s=new Scene();s.Viewer.color=-1;Check(BlackmailArtAssets.PlayerColor(s.State,s.Viewer)==0x3366CC,"Default color sentinel was drawn white");});
                Case("BLACKMAIL-UI-006-unit-action-shows-two-stars-and-actor-color",()=>{var s=new Scene();s.Select();Check(s.Bar.buttons.Count==1&&s.Button.text=="Blackmail"&&s.Button.Cost==2&&s.Button.resourceWidget.gameObject.activeSelf,"Paid unit action cost absent");Check(s.Button.icon.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().Last().color.r==1,"Action crown not actor colored");});
                Case("BLACKMAIL-UI-047-city-only-selection-offers-no-unit-blackmail",()=>{var s=new Scene();s.Interaction.SelectTile(s.Tile);s.Select();Check(s.Bar.buttons.Count==0,"City still exposes the removed city Blackmail action");});
                Case("BLACKMAIL-UI-048-invisible-unit-selection-is-not-targetable",()=>{var s=new Scene();s.Unit.Hidden=true;s.Select();Check(s.Bar.buttons.Count==0,"Invisible target leaked a paid action");});
                Case("BLACKMAIL-UI-049-layered-city-notes-group-by-actor",()=>{var s=new Scene();NativeBlackmail.Set(new LedgerBlackmail(1,2,3,5,s.State.CurrentTurn){Layers=2,UnitId=90},new LedgerBlackmail(1,2,3,5,s.State.CurrentTurn){Layers=3,UnitId=91});NativeBlackmailPresentation.Tick();Check(Renderers().Length==2,"One actor produced duplicate city note objects");});
                Case("BLACKMAIL-UI-050-unit-tint-count-and-native-health-unchanged",()=>{var s=new Scene();s.UnitMarks(3);var root=s.UnitMark();Check(root is not null&&root.activeInHierarchy,"Visible income label absent: "+string.Join(";",Entry.Notes.Select(n=>n.Kind+":"+n.Message)));Check(root!.ComponentsInChildren<TMPro.TextMeshPro>().Single().text=="3","Stacked layer count missing");Check(root.ComponentsInChildren<SpriteRenderer>().Any(r=>r.sprite?.Name=="UI_STARICON"),"Native star artwork absent");Check(Math.Abs(s.Body.color.r-.8f*.62f)<.0001f&&s.Body.color.a==.5f,"Public tint must darken RGB without modifying invisibility alpha");Check(s.Unit.statusDisplay!.healthLabel.text=="10"&&s.Unit.UnitState.health==10&&s.Unit.UnitState.owner==2,"Income presentation changed native health or ownership");});
                Case("BLACKMAIL-UI-051-unit-idle-tint-does-not-accumulate",()=>{var s=new Scene();s.UnitMarks(2);var color=s.Body.color;var root=s.UnitMark();int reads=NativeBlackmail.AllQueries,contracts=NativeBlackmail.ContractQueries;for(int i=0;i<30;i++)MimicMapInspection.Refresh(s.Unit);Check(s.Body.color==color&&ReferenceEquals(root,s.UnitMark()),"Idle refresh accumulated darkening or allocated markers");Check(NativeBlackmail.AllQueries==reads&&NativeBlackmail.ContractQueries==contracts,"Per-unit refresh rescanned all contracts");});
                foreach(string guard in new[]{"fog","invisible","pooled-unit","dead-unit","viewer","hotseat","state","removed"})Case("BLACKMAIL-UI-052-unit-marker-cleanup-"+guard,()=>{var s=new Scene();var original=s.Body.color;s.UnitMarks();var root=s.UnitMark();switch(guard){case"fog":s.Data.Explored=false;break;case"invisible":s.Unit.Hidden=true;break;case"pooled-unit":s.Map.Units[90]=new();break;case"dead-unit":s.State.Units.Remove(90);break;case"viewer":GameManager.Client.IsSpectating=true;break;case"hotseat":HotSeatOverlay.IsActive=true;break;case"state":GameManager.GameState=new();break;case"removed":NativeBlackmail.Set();break;}MimicMapInspection.Refresh(s.Unit);Check(s.UnitMark() is null&&!root,"Hidden/stale unit remains trackable by label");Check(s.Body.color==original,"Cleanup retained compromised tint");});
                Case("BLACKMAIL-UI-053-unit-native-update-restores-before-writing",()=>{var s=new Scene();s.UnitMarks();MimicMapInspection.BeforeUnit(s.Unit);Check(s.Body.color==new Color(.8f,.7f,.6f,.5f),"Native writer received our tinted RGB");s.Body.color=new(.2f,.4f,.6f,.9f);MimicMapInspection.UnitFinished(null,s.Unit);Check(Math.Abs(s.Body.color.r-.2f*.62f)<.0001f&&s.Body.color.a==.9f,"Tint overwrote newer native color/alpha");MimicMapInspection.Clear();Check(s.Body.color==new Color(.2f,.4f,.6f,.9f),"Clear did not restore newest native color");});
                Case("BLACKMAIL-UI-054-zero-alpha-body-keeps-native-hidden-rgb",()=>{var s=new Scene();s.Body.color=new(.8f,.7f,.6f,0);s.UnitMarks();Check(s.Body.color==new Color(.8f,.7f,.6f,0),"A hidden native body must retain exact color for the private art restoration guard");});
                Case("BLACKMAIL-UI-055-income-hidden-from-native-preview-camera",()=>{var s=new Scene();s.UnitMarks();var root=s.UnitMark();NativeBlackmailPresentation.BeforeDuplicate(out var scope);Check(root is not null&&!root.activeInHierarchy,"World income label leaked into duplicate camera");NativeBlackmailPresentation.AfterDuplicate(null,scope);MimicMapInspection.Refresh(s.Unit);Check(root!.activeInHierarchy,"Label did not resume on live map");});
                Case("BLACKMAIL-UI-056-invisible-pulse-keeps-tint-and-new-alpha",()=>{var s=new Scene();s.UnitMarks();var color=s.Body.color;s.Body.color=color with{a=.25f};MimicMapInspection.Refresh(s.Unit);Check(s.Body.color==color with{a=.25f},"Native alpha pulse accumulated compromised darkening");MimicMapInspection.Clear();Check(s.Body.color==new Color(.8f,.7f,.6f,.25f),"Retirement discarded latest native invisibility alpha");});
                Case("BLACKMAIL-UI-057-unit-facing-does-not-flip-income",()=>{var s=new Scene();s.Unit.spriteContainer.localScale=new(-2,2,1);s.Unit.spriteContainer.localRotation=Quaternion.Euler(0,0,180);s.UnitMarks();var root=s.UnitMark();Check(root!.transform.localRotation==s.Unit.statusDisplay!.healthLabel.transform.rotation,"Income label inherited disguise/art facing instead of native text orientation");Check(root.transform.position.x==s.Tile.VisualCenter.x,"Facing displaced the under-unit income horizontally");});
                Case("BLACKMAIL-UI-058-zero-city-output-retains-compromise",()=>{
                    var s=new Scene();s.UnitMarks(3);NativeBlackmailPresentation.Tick();var root=s.UnitMark();var color=s.Body.color;var note=Renderers().First();
                    NativeBlackmail.IncomePaused=true;s.State.CurrentCommand++;MimicMapInspection.Refresh(s.Unit);NativeBlackmailPresentation.Tick();
                    Check(ReferenceEquals(root,s.UnitMark())&&root!.activeInHierarchy&&s.Body.color==color,"Zero output removed the public compromise or accumulated tint");
                    Check(root!.ComponentsInChildren<TMPro.TextMeshPro>().Single().text=="0","Temporarily paused contract must show zero payable income");
                    Check(Renderers().Length==2&&ReferenceEquals(note,Renderers().First()),"Zero output removed or duplicated the supporting-city note");
                    NativeBlackmail.IncomePaused=false;s.State.CurrentCommand++;MimicMapInspection.Refresh(s.Unit);NativeBlackmailPresentation.Tick();
                    Check(ReferenceEquals(root,s.UnitMark())&&root.ComponentsInChildren<TMPro.TextMeshPro>().Single().text=="3"&&s.Body.color==color,"Restored city output did not resume the same three-layer unit indicator");
                    Check(Renderers().Length==2&&ReferenceEquals(note,Renderers().First()),"Restored output duplicated supporting-city notes");
                });
                Case("BLACKMAIL-UI-059-zero-income-contract-obeys-fog-and-cleanup",()=>{
                    var s=new Scene();var original=s.Body.color;NativeBlackmail.IncomePaused=true;s.UnitMarks(2);var root=s.UnitMark();
                    Check(root is not null&&root.activeInHierarchy&&root.ComponentsInChildren<TMPro.TextMeshPro>().Single().text=="0","Initially paused compromise was not represented");
                    s.Data.Explored=false;MimicMapInspection.Refresh(s.Unit);Check(s.UnitMark() is null&&!root&&s.Body.color==original,"Paused contract tracked a hidden unit or retained tint");
                    s.Data.Explored=true;MimicMapInspection.Refresh(s.Unit);Check(s.UnitMark() is not null&&s.UnitMark()!.ComponentsInChildren<TMPro.TextMeshPro>().Single().text=="0","Revealed paused unit did not restore its zero-income marker");
                    NativeBlackmail.Set();MimicMapInspection.Refresh(s.Unit);Check(s.UnitMark() is null&&s.Body.color==original,"Removed paused contract retained its marker or tint");
                });
                Case("BLACKMAIL-UI-060-repeat-purchases-always-show-two-stars",()=>{
                    var s=new Scene();s.Select();Check(s.Button.Cost==2,"Initial quote must be two stars");
                    for(int layers=1;layers<=5;layers++)
                    {
                        NativeBlackmail.Set(new LedgerBlackmail(1,2,3,5,s.State.CurrentTurn,UnitId:90,Layers:layers,LastAppliedTurn:s.State.CurrentTurn));
                        NativeBlackmailPresentation.Tick();Check(s.Bar.buttons.Count==0,"Same-turn repeat was offered");
                        s.State.CurrentTurn++;NativeBlackmailPresentation.Tick();Check(s.Bar.buttons.Count==1&&s.Button.Cost==2,"Next turn must still display two stars");
                    }
                });
                Case("BLACKMAIL-UI-061-quote-flat-across-actors-and-units",()=>{
                    var s=new Scene();NativeBlackmail.Set(new LedgerBlackmail(3,2,3,5,0,UnitId:90,Layers:4),new LedgerBlackmail(1,2,3,5,0,UnitId:91,Layers:5));s.Select();
                    Check(s.Button.Cost==2,"Another actor or target increased the initial price");
                    NativeBlackmail.Set(new LedgerBlackmail(1,2,3,5,0,UnitId:90,Layers:2),new LedgerBlackmail(3,2,3,5,0,UnitId:90,Layers:4));NativeBlackmailPresentation.Tick();
                    Check(s.Button.Cost==2,"Own contract layers must not increase the quote");
                    var second=new Unit{UnitState=new(){id=91,owner=2,coordinates=s.Data.coordinates,home=s.Data.coordinates,type=UnitData.Type.warrior}};
                    s.State.Units[91]=second.UnitState;s.Data.unit=second.UnitState;s.Map.Register(second);s.Interaction.SelectUnit(second);s.Select();
                    Check(s.Bar.buttons.Count==1&&s.Button.Cost==2,"Selecting a different uncontracted unit retained the old price");
                });
                Case("BLACKMAIL-UI-062-existing-button-keeps-flat-price-in-place",()=>{
                    var s=new Scene();s.Select();var button=s.Button;
                    NativeBlackmail.Set(new LedgerBlackmail(1,2,3,5,0,UnitId:90,Layers:2));NativeBlackmailPresentation.Tick();
                    Check(ReferenceEquals(button,s.Button)&&button.Cost==2&&button.resourceWidget.gameObject.activeSelf,"Ledger update changed the flat cost or replaced the retained widget");
                    int queries=NativeBlackmail.Queries;for(int i=0;i<20;i++)NativeBlackmailPresentation.Tick();
                    Check(NativeBlackmail.Queries==queries&&ReferenceEquals(button,s.Button)&&button.Cost==2,"Idle dynamic quote repeated work or changed price");
                });
                Case("BLACKMAIL-UI-063-paused-contract-levels-do-not-increase-price",()=>{
                    var s=new Scene();NativeBlackmail.Set(new LedgerBlackmail(1,2,3,5,0,UnitId:90,Layers:2));NativeBlackmail.IncomePaused=true;s.Select();
                    Check(s.Button.Cost==2,"Saved or paused contract levels increased the two-star quote");
                    NativeBlackmail.IncomePaused=false;s.State.CurrentCommand++;NativeBlackmailPresentation.Tick();Check(s.Button.Cost==2,"Resumed income changed the flat price");
                });
                Case("BLACKMAIL-UI-064-flat-price-rechecks-affordability",()=>{
                    var s=new Scene();s.Viewer.Currency=2;s.Select();Check(s.Button.Cost==2,"Two stars should fund the first level");
                    NativeBlackmail.Set(new LedgerBlackmail(1,2,3,5,0,UnitId:90,Layers:4));NativeBlackmailPresentation.Tick();Check(s.Bar.buttons.Count==1&&s.Button.Cost==2,"Exactly two stars must fund a repeated layer");
                    s.Viewer.Currency=1;NativeBlackmailPresentation.Tick();Check(s.Bar.buttons.Count==0,"One star funded a repeated layer");
                    s.Viewer.Currency=2;NativeBlackmailPresentation.Tick();Check(s.Bar.buttons.Count==1&&s.Button.Cost==2,"Exact two-star treasury did not restore the offer");
                    s.Viewer.Currency=1;s.Button.OnClicked!.Click();Check(s.Client.Sent==0&&s.Bar.buttons.Count==0&&s.Viewer.Currency==1,"Stale unaffordable click submitted, debited, or retained active control");
                });
                Case("BLACKMAIL-UI-065-fresh-layer-quote-submits-at-same-displayed-price",()=>{
                    var s=new Scene();s.Select();var button=s.Button;NativeBlackmail.Set(new LedgerBlackmail(1,2,3,5,0,UnitId:90,Layers:1));
                    int queries=NativeBlackmail.Queries;button.OnClicked!.Click();
                    Check(s.Client.Sent==1&&NativeBlackmail.Queries==queries+1&&button.Cost==2&&NativeExchangeCommands.Last!.Kind==NativeExchangeKind.Blackmail,"Fresh valid layer change must submit at the same displayed two-star price");
                    Check(s.Viewer.Currency==15,"Presentation debited currency before authoritative execution");
                });
                Case("BLACKMAIL-UI-007-idle-frame-does-not-replan-or-duplicate",()=>{var s=new Scene();s.Select();int queries=NativeBlackmail.Queries;var button=s.Button;for(int i=0;i<40;i++)NativeBlackmailPresentation.Tick();Check(NativeBlackmail.Queries==queries&&s.Bar.buttons.Count==1&&ReferenceEquals(button,s.Button),"Idle UI replanned or duplicated controls");Check(NativeBlackmail.ContractQueries==1&&NativeBlackmail.AllQueries==0&&Root is null,"Empty ledger repeatedly scanned or allocated marker root");});
                Case("BLACKMAIL-UI-008-negative-offer-is-cached",()=>{var s=new Scene();NativeBlackmail.Allowed=false;s.Select();int queries=NativeBlackmail.Queries;for(int i=0;i<25;i++)NativeBlackmailPresentation.Tick();Check(NativeBlackmail.Queries==queries&&s.Bar.buttons.Count==0,"Unavailable action repeatedly replanned");});
                Case("BLACKMAIL-UI-009-fresh-click-reoffers-and-submits",()=>{var s=new Scene();s.Select();uint old=NativeBlackmail.Offered;int queries=NativeBlackmail.Queries;s.Button.OnClicked!.Click();Check(s.Client.Sent==1&&NativeExchangeCommands.Last!.Kind==NativeExchangeKind.Blackmail&&NativeBlackmail.Queries==queries+1&&NativeBlackmail.Offered>old,"Click reused stale intent or failed command path");Check(s.Viewer.Currency==15,"UI spent treasury directly");});
                Case("BLACKMAIL-UI-010-invalid-command-forgotten",()=>{var s=new Scene();s.Select();NativeExchangeCommands.ValidResult=false;s.Button.OnClicked!.Click();Check(s.Client.Sent==0&&NativeExchangeCommands.Forgotten==1,"Rejected command leaked");});
                foreach(string guard in new[]{"currency","owner","turn","selection","hidden","unexplored","client","processing","popup","replay","spectator","hotseat","disabled"})Case("BLACKMAIL-UI-011-click-guard-"+guard,()=>{
                    var s=new Scene();s.Select();var click=s.Button.OnClicked!;
                    switch(guard){case "currency":s.Viewer.Currency=1;break;case "owner":s.Unit.UnitState.owner=1;break;case "turn":s.State.CurrentTurn++;break;case "selection":s.Interaction.selectedUnit=new();break;case "hidden":s.Tile.IsHidden=true;break;case "unexplored":s.Data.Explored=false;break;case "client":s.Client.GameState=new();break;case "processing":s.Client.ActionManager.IsProcessing=true;break;case "popup":PopupManager.PopupShowing=true;break;case "replay":s.Client.IsReplay=true;break;case "spectator":s.Client.IsSpectating=true;break;case "hotseat":HotSeatOverlay.IsActive=true;break;case "disabled":NativeRegroupSession.Enabled=false;break;}
                    click.Click();Check(s.Client.Sent==0,"Stale/unauthorized unit action sent");
                });
                Case("BLACKMAIL-UI-012-affordability-change-refreshes-existing-offer",()=>{var s=new Scene();s.Viewer.Currency=1;s.Select();Check(s.Bar.buttons.Count==0,"Unaffordable offer shown active");s.Viewer.Currency=2;NativeBlackmailPresentation.Tick();Check(s.Bar.buttons.Count==1&&s.Button.Cost==2,"Newly affordable unit action absent");});
                Case("BLACKMAIL-UI-013-native-pool-clear-retires-only-owned-crown",()=>{var s=new Scene();s.Select();var button=s.Button;var crown=button.icon.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().Last();NativeBlackmailPresentation.NativeClear(s.Bar);Check(button&&button.OnClicked is null&&!crown,"Pool cleanup destroyed native button or kept crown");});
                Case("BLACKMAIL-UI-014-reused-native-button-not-modified",()=>{var s=new Scene();s.Select();var button=s.Button;var crown=button.icon.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().Last();button.OnClicked=new UIButtonBase.ButtonAction((_,_)=>{});button.text="Native action";NativeBlackmailPresentation.NativeClear(s.Bar);Check(button.OnClicked is not null&&button.text=="Native action"&&button&&!crown,"Pooled native occupant changed or owned crown leaked");});
                Case("BLACKMAIL-UI-015-two-visible-actor-colored-map-icons",()=>{var s=new Scene();s.Records(1,3);NativeBlackmailPresentation.Tick();var all=Renderers();Check(all.Length==4,"Multiple blackmailers were collapsed");var colors=all.Where(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite(true))).Select(x=>x.color).ToArray();Check(colors.Length==2&&colors.Any(x=>x.r==1&&x.g==0)&&colors.Any(x=>x.g==1&&x.r==0),"Markers used city owner/viewer instead of each blackmailer");Check(all.Select(x=>x.transform.position.x).Distinct().Count()==2,"Markers overlap horizontally");Check(s.Data.owner==2&&s.Viewer.Currency==15,"Map rendering changed city ownership/economy");});
                foreach(string guard in new[]{"hidden","unexplored","owner","tile-pool","state","viewer","client","replay","spectator","hotseat","inactive-tile","inactive-map"})Case("BLACKMAIL-UI-016-map-guard-"+guard,()=>{
                    var s=new Scene();s.Records(1,3);NativeBlackmailPresentation.Tick();Check(Renderers().Length==4,"Initial markers missing");
                    switch(guard){case "hidden":s.Tile.IsHidden=true;break;case "unexplored":s.Data.Explored=false;break;case "owner":s.Data.owner=3;break;case "inactive-tile":s.Tile.gameObject.SetActive(false);break;case "inactive-map":s.Map.gameObject.SetActive(false);break;case "tile-pool":s.Tile.Data=new();break;case "state":GameManager.GameState=new();break;case "viewer":GameManager.LocalPlayer=new(){Id=4};s.Data.Explored=false;break;case "client":s.Client.GameState=new();break;case "replay":s.Client.IsReplay=true;break;case "spectator":s.Client.IsSpectating=true;break;case "hotseat":HotSeatOverlay.IsActive=true;break;}
                    NativeBlackmailPresentation.Tick();Check(Renderers().Length==0,"Hidden/stale city remained marked");
                });
                Case("BLACKMAIL-UI-017-marks-available-to-city-owner-with-vision",()=>{var s=new Scene();s.Records(1,3);GameManager.LocalPlayer=s.State.Players[2];NativeBlackmailPresentation.Tick();Check(Renderers().Length==4,"Victim cannot see public blackmailer icons");});
                Case("BLACKMAIL-UI-018-duplicate-camera-hides-marks-and-restores-later",()=>{var s=new Scene();s.Records(1);NativeBlackmailPresentation.Tick();NativeBlackmailPresentation.BeforeDuplicate(out var scope);Check(Renderers().Length==0,"Marker leaked into native sprite camera");var failure=new Exception("native");Check(ReferenceEquals(NativeBlackmailPresentation.AfterDuplicate(failure,scope),failure),"Camera finalizer changed failure");NativeBlackmailPresentation.Tick();Check(Renderers().Length==2,"Marker not restored after preview");});
                Case("BLACKMAIL-UI-019-clear-inside-duplicate-does-not-resurrect-scope",()=>{var s=new Scene();s.Records(1);NativeBlackmailPresentation.BeforeDuplicate(out var a);NativeBlackmailPresentation.BeforeDuplicate(out var b);NativeBlackmailPresentation.Clear();NativeBlackmailPresentation.AfterDuplicate(null,b);NativeBlackmailPresentation.AfterDuplicate(null,a);NativeBlackmailPresentation.Tick();Check(Renderers().Length==2,"Retired camera scope blocked next live frame");});
                Case("BLACKMAIL-UI-020-source-view-retirement-preserves-original-texture",()=>{var source=(Sprite)UnitArtAssets.Get("blackmail_fixed");var view=(Sprite)BlackmailArtAssets.Sprite();UnityEngine.Object.Destroy(view);var replacement=(Sprite)BlackmailArtAssets.Sprite();Check(replacement&&source&&source.texture&&!ReferenceEquals(view,replacement),"Owned view repair destroyed original PNG texture");});
                Case("BLACKMAIL-UI-021-same-turn-research-invalidates-negative-cache",()=>{var s=new Scene();s.Viewer.Espionage=false;s.Select();Check(s.Bar.buttons.Count==0,"Locked action was active");int q=NativeBlackmail.Queries;s.Viewer.Espionage=true;NativeBlackmailPresentation.Tick();Check(s.Bar.buttons.Count==1&&NativeBlackmail.Queries==q+1,"Immediate Espionage purchase did not refresh unit action");});
                Case("BLACKMAIL-UI-022-color-change-updates-owned-crown",()=>{var s=new Scene();s.Select();var b=s.Button;s.Viewer.color=0x0000FF;NativeBlackmailPresentation.Tick();var color=b.icon.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().Last().color;Check(ReferenceEquals(s.Button,b)&&color.b==1&&color.r==0,"Existing crown retained old actor color");});
                Case("BLACKMAIL-UI-023-rotated-scaled-native-name-frame",()=>{
                    var s=new Scene();s.Records(1,3);var frame=s.Name.transform;frame.localRotation=Quaternion.Euler(90,0,0);frame.localScale=new(2,3,1);
                    NativeBlackmailPresentation.Tick();var docs=Renderers().Where(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite())).ToArray();
                    Check(docs.Length==2,"Rotated city label lost documents");Check(docs.All(x=>Math.Abs(frame.InverseTransformPoint(x.transform.position).z)<.0001f),"Document left native name plane");
                    Check(docs.All(x=>Math.Abs(x.transform.lossyScale.y/x.transform.lossyScale.x-1f)<.0001f),"Native name scaling stretched document aspect");
                });
                Case("BLACKMAIL-UI-024-construction-failure-removes-appended-native-entry",()=>{
                    var s=new Scene();BranchTechFixture.Failure="image-preserve";s.Select();Check(s.Bar.buttons.Count==0,"Failed artwork left dead native button entry");
                    Check(Entry.Notes.Any(n=>n.Kind=="blackmail-presentation-error"),"Injected failure did not reach production cleanup");Entry.Notes.Clear();BranchTechFixture.Failure=null;NativeBlackmailPresentation.Tick();Check(s.Bar.buttons.Count==1,"Later valid control could not recover");
                });
                Case("BLACKMAIL-UI-025-single-document-centered-below-name",()=>{
                    var s=new Scene();s.Records(1);NativeBlackmailPresentation.Tick();var doc=Renderers().Single(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite()));
                    Check(Math.Abs(doc.transform.position.x-s.Name.bg.bounds.center.x)<.0001f,"Single document is not centered beneath native name");
                    Check(Math.Abs(s.Name.bg.bounds.min.y-doc.bounds.max.y-.004f)<.0001f,"Document not immediately below name underline");
                    Check(Math.Abs(doc.bounds.size.y-.12f)<.0001f&&doc.sortingLayerID==7,"Document did not retain calibrated board size/native sorting");
                });
                Case("BLACKMAIL-UI-026-native-embassies-and-plots-unchanged",()=>{
                    var s=new Scene();s.City.cityRenderer.PlayerEmbassies.Add(3);foreach(var p in Native.Items(s.City.cityRenderer.plots).Cast<CityPlot>())p.AddNativeHouse("Native embassy",.8f);
                    s.Records(1,3);NativeBlackmailPresentation.Tick();Check(Renderers().Length==4,"Documents missing with native embassy");
                    Check(s.City.cityRenderer.PlayerEmbassies.Count==1&&s.City.cityRenderer.RngCalls==0&&Native.Items(s.City.cityRenderer.plots).Cast<CityPlot>().All(p=>p.floors==2&&p.houses.Count==2),"Documents consumed city plots/embassy/RNG");
                });
                Case("BLACKMAIL-UI-027-house-growth-does-not-displace-name-row",()=>{
                    var s=new Scene();s.Records(1,3);NativeBlackmailPresentation.Tick();var before=Renderers().Select(x=>x.transform.position).ToArray();
                    foreach(var p in Native.Items(s.City.cityRenderer.plots).Cast<CityPlot>())p.AddNativeHouse("New embassy",1f);
                    NativeBlackmailPresentation.Tick();Check(Renderers().Select(x=>x.transform.position).SequenceEqual(before),"City roof growth displaced label documents");
                    Check(Renderers().All(x=>x.color.a==1),"Documents or crowns faded");
                });
                Case("BLACKMAIL-UI-028-name-pool-replacement-rebinds-anchor",()=>{
                    var s=new Scene();s.Records(1);NativeBlackmailPresentation.Tick();var old=s.City.cityOverlay;
                    NativeBlackmailPresentation.NameReturning(old);Check(Renderers().Length==0,"Returned label left documents visible");
                    s.City.cityOverlay=new();s.BindName(s.City);s.Name.transform.position=new(8,7,0);NativeBlackmailPresentation.Tick();
                    Check(Renderers().Length==2&&Math.Abs(Renderers()[0].transform.position.x-8)<.0001f,"Document retained pooled old label anchor");
                    Check(old&&old.nameContainer.bg,"Cleanup destroyed borrowed label objects");
                });
                foreach(string guard in new[]{"absent-city","stale-coordinate","foreign-bg-parent","inactive-city","foreign-label-city","inactive-name","disabled-underline","transparent-underline","disabled-text"})Case("BLACKMAIL-UI-029-city-binding-"+guard,()=>{
                    var s=new Scene();s.Records(1);switch(guard){case "absent-city":s.Tile.Improvement=null;break;case "stale-coordinate":s.City.cityRenderer.Coordinates=new(9,9);break;case "foreign-bg-parent":s.Name.bg.transform.SetParent(null,false);break;case "inactive-city":s.City.cityRenderer.gameObject.SetActive(false);break;case "foreign-label-city":s.Name.city=new();break;case "inactive-name":s.Name.gameObject.SetActive(false);break;case "disabled-underline":s.Name.bg.enabled=false;break;case "transparent-underline":s.Name.bg.color=new(1,1,1,0);break;case "disabled-text":s.Name.label.renderer.enabled=false;break;}
                    NativeBlackmailPresentation.Tick();Check(Renderers().Length==0,"Stale/hidden native name retained documents");
                });
                Case("BLACKMAIL-UI-030-multiple-documents-centered-overlap-and-order",()=>{
                    var s=new Scene();s.Records(3,1);NativeBlackmailPresentation.Tick();var docs=Renderers().Where(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite())).ToArray();
                    Check(docs.Length==2&&docs[0].bounds.max.x>docs[1].bounds.min.x,"Requested overlapping row missing");
                    Check(Math.Abs((docs[0].bounds.min.x+docs[1].bounds.max.x)/2-s.Name.bg.bounds.center.x)<.0001f&&docs[0].transform.position.y==docs[1].transform.position.y,"Document row not centered/level");
                    Check(docs[0].sortingOrder<docs[1].sortingOrder,"Later crown/document stack interleaves earlier layer");var positions=docs.Select(x=>x.transform.position).ToArray();NativeBlackmailPresentation.Tick();
                    Check(Renderers().Where(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite())).Select(x=>x.transform.position).SequenceEqual(positions),"Actor order shuffled on idle frame");
                });
                Case("BLACKMAIL-UI-031-idle-layout-cache-and-native-name-resize",()=>{
                    var s=new Scene();s.Records(1,3);NativeBlackmailPresentation.Tick();var layouts=(System.Collections.IDictionary)Private("Layouts")!;var cached=layouts.Values.Cast<object>().Single();
                    for(int i=0;i<40;i++)NativeBlackmailPresentation.Tick();Check(ReferenceEquals(cached,layouts.Values.Cast<object>().Single()),"Idle frame rebuilt label layout");
                    s.Name.bg.bounds=new(new(4,3,0),new(3,.3f,0));NativeBlackmailPresentation.Tick();Check(!ReferenceEquals(cached,layouts.Values.Cast<object>().Single()),"Native name resize retained stale row");
                });
                Case("BLACKMAIL-UI-032-native-dirty-refresh-hides-until-ready",()=>{
                    var s=new Scene();s.Records(1,3);NativeBlackmailPresentation.Tick();s.City.cityRenderer.dataChanged=true;NativeBlackmailPresentation.BeforeCityRefresh(s.City.cityRenderer);
                    Check(Renderers().Length==0,"Half-refreshed city retained documents");NativeBlackmailPresentation.Tick();Check(Renderers().Length==0,"Tick showed documents during native rebuild");
                    s.City.cityRenderer.dataChanged=false;NativeBlackmailPresentation.Tick();Check(Renderers().Length==4,"Completed city refresh lost documents");
                });
                Case("BLACKMAIL-UI-033-generated-vector-fields-drive-city-geometry",()=>{
                    var bounds=new NativeFieldBounds();bool rejected=false;try{Native.Get(bounds.center,"x");}catch(MissingMemberException){rejected=true;}
                    Check(rejected,"Fixture accidentally modeled vector coordinates as properties");
                    var frame=new GameObject("Field-backed native bounds frame").transform;
                    var result=typeof(NativeBlackmailPresentation).GetMethod("InCity",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{frame,bounds})!;
                    Check(Convert.ToSingle(Native.Get(result,"Left"))==3&&Convert.ToSingle(Native.Get(result,"Right"))==5&&Convert.ToSingle(Native.Get(result,"Bottom"))==4&&Convert.ToSingle(Native.Get(result,"Top"))==8,"Actual native vector fields cannot position city documents");
                });
                Case("BLACKMAIL-UI-034-city-name-glyphs-do-not-resize-documents",()=>{
                    var s=new Scene();s.Records(1,3);NativeBlackmailPresentation.Tick();var before=Renderers().Where(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite())).Select(x=>x.bounds.size).ToArray();
                    foreach(float glyphHeight in new[]{.09f,.36f,.15f})
                    {
                        s.Name.label.renderer.bounds=new(new(3,3.1f,0),new(1.4f,glyphHeight,0));NativeBlackmailPresentation.Tick();
                        Check(Renderers().Where(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite())).Select(x=>x.bounds.size).SequenceEqual(before),"Name glyph height changed document size");
                    }
                });
                Case("BLACKMAIL-UI-035-scaled-city-name-keeps-uniform-board-size-and-gap",()=>{
                    var s=new Scene();s.Records(1);s.Name.transform.localScale=new(.7f,1.6f,1);NativeBlackmailPresentation.Tick();
                    var doc=Renderers().Single(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite()));var crown=Renderers().Single(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite(true)));
                    Check(Math.Abs(doc.bounds.size.y-.12f)<.0001f&&Math.Abs(doc.bounds.size.x/doc.bounds.size.y-421f/457f)<.0001f,"Name frame scale changed document size/aspect");
                    Check(Math.Abs(s.Name.bg.bounds.min.y-doc.bounds.max.y-.004f)<.0001f,"Name frame scale changed underline gap");
                    Check(doc.bounds==crown.bounds,"Fixed and team layers lost alignment");
                });
                Case("BLACKMAIL-UI-036-name-scale-change-reflows-centered-overlapping-row",()=>{
                    var s=new Scene();s.Records(1,3);NativeBlackmailPresentation.Tick();var layouts=(System.Collections.IDictionary)Private("Layouts")!;var cached=layouts.Values.Cast<object>().Single();
                    s.Name.transform.localScale=new(2,.5f,1);NativeBlackmailPresentation.Tick();var docs=Renderers().Where(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite())).ToArray();
                    Check(!ReferenceEquals(cached,layouts.Values.Cast<object>().Single()),"Name transform resize reused stale placement");
                    Check(docs.All(x=>Math.Abs(x.bounds.size.y-.12f)<.0001f)&&Math.Abs((docs[1].bounds.center.x-docs[0].bounds.center.x)/docs[0].bounds.size.x-.55f)<.0001f,"Name scale changed document size/overlap");
                    Check(Math.Abs((docs[0].bounds.min.x+docs[1].bounds.max.x)/2-s.Name.bg.bounds.center.x)<.0001f&&docs.All(x=>Math.Abs(s.Name.bg.bounds.min.y-x.bounds.max.y-.004f)<.0001f),"Resized name row lost its center/underline anchor");
                });
                Case("BLACKMAIL-UI-037-owner-population-bar-remains-clear",()=>{
                    var s=new Scene();s.Records(1,3);GameManager.LocalPlayer=s.State.Players[2];var segment=s.Population();var native=segment.background.bounds;
                    NativeBlackmailPresentation.Tick();var docs=Renderers().Where(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite())).ToArray();
                    Check(docs.Length==2&&docs.All(x=>Math.Abs(native.min.y-x.bounds.max.y-.004f)<.0001f),"Owned city documents overlap the visible population bar");
                    Check(docs.All(x=>Math.Abs(x.bounds.size.y-.12f)<.0001f)&&Math.Abs((docs[0].bounds.min.x+docs[1].bounds.max.x)/2-s.Name.bg.bounds.center.x)<.0001f,"Population layout changed note size or row center");
                    Check(segment.background.bounds==native&&segment.background.enabled&&s.City.cityOverlay.progressBar.gameObject.activeSelf,"Document placement mutated native population UI");
                });
                Case("BLACKMAIL-UI-038-hidden-population-bar-does-not-displace-foreign-note",()=>{
                    var s=new Scene();s.Records(1);s.Population();s.City.cityOverlay.progressBar.gameObject.SetActive(false);NativeBlackmailPresentation.Tick();
                    var doc=Renderers().Single(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite()));Check(Math.Abs(s.Name.bg.bounds.min.y-doc.bounds.max.y-.004f)<.0001f,"Hidden population bar added a foreign-city gap");
                });
                Case("BLACKMAIL-UI-039-pass-and-play-owner-switch-reflows-row",()=>{
                    var s=new Scene();s.Records(1,3);NativeBlackmailPresentation.Tick();var foreign=Renderers().Select(x=>x.transform.position).ToArray();
                    HotSeatOverlay.IsActive=true;NativeBlackmailPresentation.Tick();Check(Renderers().Length==0,"Handoff screen leaked city notes");
                    HotSeatOverlay.IsActive=false;GameManager.LocalPlayer=s.State.Players[2];s.State.CurrentPlayer=2;s.State.CurrentTurn++;var segment=s.Population();NativeBlackmailPresentation.Tick();
                    Check(Renderers().Length==4&&Renderers().All(x=>x.bounds.max.y<segment.background.bounds.min.y),"Owner handoff retained foreign layout inside population bar");
                    GameManager.LocalPlayer=s.Viewer;s.State.CurrentPlayer=1;s.State.CurrentTurn++;s.City.cityOverlay.progressBar.gameObject.SetActive(false);NativeBlackmailPresentation.Tick();
                    Check(Renderers().Select(x=>x.transform.position).SequenceEqual(foreign),"Returning to foreign view retained owner population spacing");
                });
                Case("BLACKMAIL-UI-040-population-resize-updates-cached-placement",()=>{
                    var s=new Scene();s.Records(1);var segment=s.Population();NativeBlackmailPresentation.Tick();var doc=Renderers().Single(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite()));var old=doc.transform.position;
                    segment.background.bounds=new(new(3,2.55f,0),new(2.8f,.2f,0));NativeBlackmailPresentation.Tick();
                    Check(ReferenceEquals(doc,Renderers().Single(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite())))&&doc.transform.position.y<old.y,"Selection/population resize left stale note placement or recreated note");
                    Check(Math.Abs(segment.background.bounds.min.y-doc.bounds.max.y-.004f)<.0001f&&Math.Abs(doc.bounds.size.y-.12f)<.0001f,"Population resize changed note size or clearance");
                });
                Case("BLACKMAIL-UI-041-only-visible-population-parts-reserve-space",()=>{
                    var s=new Scene();s.Records(1);var segment=s.Population();segment.dot.gameObject.SetActive(true);segment.dot.bounds=new(new(3,2.57f,0),new(.1f,.08f,0));NativeBlackmailPresentation.Tick();
                    var doc=Renderers().Single(x=>ReferenceEquals(x.sprite,BlackmailArtAssets.Sprite()));Check(Math.Abs(segment.dot.bounds.min.y-doc.bounds.max.y-.004f)<.0001f,"Visible dot overlaps note");
                    segment.dot.enabled=false;NativeBlackmailPresentation.Tick();Check(Math.Abs(segment.background.bounds.min.y-doc.bounds.max.y-.004f)<.0001f,"Disabled dot still reserves space");
                    segment.dot.enabled=true;segment.dot.color=new(1,1,1,0);NativeBlackmailPresentation.Tick();Check(Math.Abs(segment.background.bounds.min.y-doc.bounds.max.y-.004f)<.0001f,"Transparent dot still reserves space");
                });
                foreach(string guard in new[]{"bar","segment","part"})Case("BLACKMAIL-UI-042-population-pool-ownership-"+guard,()=>{
                    var s=new Scene();s.Records(1);var segment=s.Population();NativeBlackmailPresentation.Tick();Check(Renderers().Length==2,"Initial note absent");
                    (guard=="bar"?s.City.cityOverlay.progressBar.transform:guard=="segment"?segment.transform:segment.background.transform).SetParent(null,false);
                    NativeBlackmailPresentation.Tick();Check(Renderers().Length==0,"Stale/reparented native population object left note visible");
                });
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Results.Count,assertions=Assertions,results=Results,nativeGame="NOT RUN",scope="Production city presentation/tech/art + real Core; synthetic native UI and Blackmail command authority"}));return 0;
            }
            catch(Exception e){Console.WriteLine(e);return 1;}
        }
    }
}
