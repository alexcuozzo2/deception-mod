using System;
using System.Collections.Generic;
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
    public static class NavalPresentationTests
    {
        static int Cases,Assertions;
        static void Check(bool v,string why){Assertions++;if(!v)throw new Exception(why);}
        static object? Call(Type t,string m,params object?[] a)=>t.GetMethod(m,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,a);
        static LedgerAppearance App(string form="rider",ushort hp=100,ushort max=100)=>new(form,new("Imperius","Default"),new("Imperius","Default"),new("Imperius","Default"),0x4455aa,hp,max,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
        static LedgerUnit Row(Unit u,LedgerActualKind kind,LedgerAppearance app,ulong logical=17,LedgerTransport? transport=null)=>new(logical,u.UnitState.id,true,kind,true,app,PaidInvestmentKind.Known,kind==LedgerActualKind.Mimic?3:2,transport);
        static (Unit Unit,SpriteRenderer Body) Actor(bool mimic=false,bool badge=false)
        {
            var actor=Call(typeof(MapAdapterTests),"Create",true,3u)!;
            var unit=(Unit)actor.GetType().GetProperty("Unit")!.GetValue(actor)!;
            var body=(SpriteRenderer)actor.GetType().GetProperty("Body")!.GetValue(actor)!;
            if(badge)Call(typeof(MapAdapterTests),"AttachBadge",actor);
            unit.spriteContainer.localScale=Vector3.one;
            if(!mimic){unit.UnitState.type=UnitType.warrior;unit.UnitState.UnitData=new(UnitType.warrior);unit.UnitState.health=70;}
            return(unit,body);
        }
        static void Case(Action body)
        {
            Call(typeof(UiAdapterTests),"Reset");Call(typeof(MapAdapterTests),"Reset");PrefabManager.Reset();NativeVisualFixture.Reset();
            MimicMapInspection.Configure(MimicPresentationUi.MapView);Cases++;
            try{body();}finally{MimicPresentationState.ReadIdentity=null;MimicPresentationUi.Clear();}
        }
        static GameObject? Overlay(Unit u)=>Enumerable.Range(0,u.spriteContainer.childCount).Select(i=>u.spriteContainer.GetChild(i).gameObject)
            .SingleOrDefault(g=>g.name=="Deception apparent native form"&&!g.Destroyed);
        static GameObject? Truth(Unit u)=>Enumerable.Range(0,u.spriteContainer.childCount).Select(i=>u.spriteContainer.GetChild(i).gameObject)
            .SingleOrDefault(g=>g.name=="Deception private Mimic"&&!g.Destroyed);
        static SpriteRenderer[] Art(Unit u)=>Truth(u)!.ComponentsInChildren<SpriteRenderer>().Where(x=>x.gameObject.name.StartsWith("Deception art part ")).ToArray();
        static GameObject[] Badges(Unit u)=>Enumerable.Range(0,u.statusDisplay!.typeContainer.childCount).Select(i=>u.statusDisplay.typeContainer.GetChild(i).gameObject)
            .Where(g=>g.name=="Deception private Mimic badge"&&!g.Destroyed).ToArray();
        static void Select(ClientInteraction c,Unit u){c.SelectUnit(u);MimicPresentationUi.Selected(c,u);MimicPresentationUi.BarReady(c.Bar);}
        static string Health(UnitPopup p)=>Native.Items(p.statsRows).Cast<UnitStatsRow>().Single(x=>x.statsNameLabel.text=="Health").statsValueLabel.text;
        static UnitPopup Popup(Unit u){PopupManager.ProductionCallbacks=true;return PopupManager.GetUnitPopup(new(){unit=u,unitData=u.UnitState.UnitData});}
        static void Reject(Action a){bool failed=false;try{a();}catch(InvalidOperationException){failed=true;}Check(failed,"Expected bounded visual rejection");}
        public static int Run()
        {
            try
            {
                Case(()=>{
                    var a=Actor();var row=Row(a.Unit,LedgerActualKind.Genuine,App());MimicPresentationState.ReadIdentity=(_,_)=>row;
                    Check(MimicPresentationState.TryProject(a.Unit.UnitState,out var p),"Genuine disguise not projected");
                    Check(!p.ActualIsMimic&&p.ActualLabel=="Warrior"&&p.NativeId=="rider"&&p.ActualNativeId=="warrior","Actual and apparent identity merged");
                    Check(p.ActualHealth==7&&p.ActualMaxHealth==10&&p.ApparentHealth==10,"Actual and apparent HP merged");
                    var c=new ClientInteraction();Select(c,a.Unit);Check(c.Bar.headerString=="Rider (Disguise)","Apparent label missing");
                    var images=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().ToArray();
                    Check(images.Any(x=>x.sprite?.Name=="Warrior body")&&images.Any(x=>x.sprite?.Name=="rider static body"),"Genuine pair must contain two native forms");
                    Check(!images.Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Icon())),"Genuine actual used Mimic artwork");
                    Check(UIUtils.Previews.Count>=2&&UIUtils.Previews.All(x=>x.gameObject.Destroyed),"Native preview temporary lifetime: "+UIUtils.Previews.Count+"/"+string.Join(",",UIUtils.Previews.Select(x=>x.gameObject.Destroyed)));
                    Check(MimicPresentationUi.ToggleInspection(c,a.Unit)&&c.Bar.headerString=="Warrior","Actual genuine toggle label");
                    var pop=Popup(a.Unit);Check(pop.Header=="Warrior"&&Health(pop)=="7/10","Actual genuine info must use native HP");
                    Check(a.Unit.UnitState.type==UnitType.warrior&&a.Unit.UnitState.health==70&&!a.Unit.UnitState.attacked,"Portrait changed real identity/action");
                });
                Case(()=>{
                    var a=Actor();var row=Row(a.Unit,LedgerActualKind.Genuine,App());MimicPresentationState.ReadIdentity=(_,_)=>row;
                    MimicMapInspection.Refresh(a.Unit);var child=Overlay(a.Unit);Check(child is not null&&a.Body.color.a==0,"Genuine apparent overlay/hiding absent");
                    Check(NativeVisualFixture.CloneCalls==1&&NativeVisualFixture.Clones.Single().gameObject.Destroyed,"Native map clone not destroyed");
                    Check(NativeVisualFixture.LastProfile!.Color==row.Appearance!.SourceColor,"Copied native color missing");
                    var borrowed=PrefabManager.Fixtures.Values.ToArray();for(int i=0;i<30;i++)MimicMapInspection.Refresh(a.Unit);
                    Check(NativeVisualFixture.CloneCalls==1&&ReferenceEquals(child,Overlay(a.Unit)),"Per-frame native clone/child allocation");
                    var c=new ClientInteraction();Select(c,a.Unit);Check(MimicPresentationUi.ToggleInspection(c,a.Unit),"Actual toggle denied");
                    Check(!child!.activeSelf&&a.Body.color.a==.4f,"Actual genuine body not restored");
                    Check(MimicPresentationUi.ToggleInspection(c,a.Unit),"Apparent retoggle denied");
                    Check(child.activeSelf&&NativeVisualFixture.CloneCalls==1,"Retoggle rebuilt snapshot");
                    row=row with{Independent=false,Appearance=null};MimicPresentationUi.Tick();
                    Check(child.Destroyed&&a.Body.color.a==.4f&&MimicPresentationUi.MapView(a.Unit)==null,"Reveal did not restore actual map body");
                    Check(c.Bar.headerString=="Warrior","Reveal retained disguised bar title");
                    Check(borrowed.All(x=>!x.gameObject.Destroyed),"Borrowed prefab destroyed on reveal");
                });
                Case(()=>{
                    var a=Actor();var owner=GameManager.LocalPlayer;GameManager.GameState.Players[owner.Id]=owner;
                    var row=Row(a.Unit,LedgerActualKind.Genuine,App());MimicPresentationState.ReadIdentity=(_,_)=>row;
                    GameManager.LocalPlayer=new(){Id=2};MimicMapInspection.Refresh(a.Unit);
                    Check(MimicPresentationUi.MapView(a.Unit)==false&&Overlay(a.Unit)?.activeSelf==true&&a.Body.color.a==0,"Opponent sees native truth instead of apparent form");
                    var c=new ClientInteraction();Select(c,a.Unit);Check(!MimicPresentationUi.ToggleInspection(c,a.Unit),"Opponent may inspect actual");
                    a.Unit.Hidden=true;MimicMapInspection.Refresh(a.Unit);Check(Overlay(a.Unit) is null,"Concealed unit overlay survived");
                    a.Unit.Hidden=false;GameManager.Client.IsReplay=true;MimicMapInspection.Refresh(a.Unit);Check(Overlay(a.Unit) is null,"Replay overlay is not authorized");
                    Check(a.Unit.UnitState.type==UnitType.warrior&&a.Unit.UnitState.health==70,"Public visual changed real body");
                });
                Case(()=>{
                    var a=Actor();var row=Row(a.Unit,LedgerActualKind.Genuine,App());MimicPresentationState.ReadIdentity=(_,_)=>row;
                    NativeVisualFixture.FailSkin=true;for(int i=0;i<20;i++)MimicMapInspection.Refresh(a.Unit);
                    Check(NativeVisualFixture.CloneCalls==1&&NativeVisualFixture.Clones[0].gameObject.Destroyed,"Failed native skinning retried or leaked");
                    Check(a.Body.color.a==0&&Overlay(a.Unit) is null,"Failed fake visual leaked real form");
                    row=row with{Appearance=App("knight")};NativeVisualFixture.FailSkin=false;MimicMapInspection.Refresh(a.Unit);
                    Check(NativeVisualFixture.CloneCalls==2&&Overlay(a.Unit) is not null,"Changed profile did not recover");
                    MimicMapInspection.Clear();Check(a.Body.color.a==.4f,"Cleanup failed to restore source");
                });
                Case(()=>{
                    var profile=App();var prefab=PrefabManager.GetPrefab(UnitType.rider,PolytopiaBackendBase.Common.TribeType.Imperius,PolytopiaBackendBase.Common.SkinType.Default);
                    var src=prefab.gameObject.FindComponent<SkinVisualsReference>()!;src.visualParts[0].tintable=true;
                    var layers=NativeVisualSnapshot.Capture("rider",profile);
                    Check(layers.Count==4&&layers.Count(x=>x.Outline)==1,"Snapshot lacks native parts/outline");
                    Check(layers.Any(x=>x.Tintable&&!x.Outline&&(Color)x.Color==ColorUtil.ColorFromInt(profile.SourceColor)),"Native tint was not captured");
                    Check(prefab.skinVisuals is null&&!prefab.gameObject.Destroyed,"Snapshot mutated borrowed prefab/cache");
                    Check(layers.All(x=>!(x.Sprite is UnityEngine.Object o)||!o.Destroyed),"Clone cleanup destroyed borrowed sprites");
                    NativeVisualFixture.Alias=true;Reject(()=>NativeVisualSnapshot.Capture("rider",profile));Check(!prefab.gameObject.Destroyed,"Aliased clone destroyed borrowed prefab");
                    NativeVisualFixture.Alias=false;int calls=NativeVisualFixture.CloneCalls;
                    Reject(()=>NativeVisualSnapshot.Capture("rider",profile with{TribeVisual=new("bad","Default")}));Check(NativeVisualFixture.CloneCalls==calls,"Bad profile allocated native clone");
                });
                Case(()=>{
                    var a=Actor();var realCargo=new UnitState{id=21,type=UnitType.warrior,UnitData=new(UnitType.warrior),health=70};
                    a.Unit.UnitState.passengerUnit=realCargo;a.Unit.UnitState.type=UnitType.scoutship;a.Unit.UnitState.UnitData=new(UnitType.scoutship);
                    var apparentCargo=App("polytaur",120,150);var app=App("scoutship",120,150) with{Passenger=apparentCargo};
                    var row=Row(a.Unit,LedgerActualKind.Genuine,app,transport:new(21));MimicPresentationState.ReadIdentity=(_,_)=>row;
                    var display=a.Unit.UnitState;MimicPresentationUi.HealthState(ref display);
                    Check(!ReferenceEquals(display,a.Unit.UnitState)&&!ReferenceEquals(display.passengerUnit,realCargo),"Status retained real cargo");
                    Check(display.passengerUnit!.type==UnitType.polytaur&&display.passengerUnit.health==120&&UnitDataExtensions.GetMaxHealth(display,GameManager.GameState)==150,"Apparent cargo maxHP not presented");
                    Check(realCargo.type==UnitType.warrior&&realCargo.health==70&&ReferenceEquals(a.Unit.UnitState.passengerUnit,realCargo),"Status changed actual cargo");
                    Check(MimicPresentationState.TryProject(a.Unit.UnitState,out var p)&&p.ActualMaxHealth==10&&p.ApparentMaxHealth==15,"Vessel independent health merged");
                });
                Case(()=>{
                    var a=Actor(true);var app=App("warrior");LedgerUnit row=Row(a.Unit,LedgerActualKind.Mimic,app);
                    MimicPresentationState.ReadIdentity=(_,id)=>id==row.NativeId?row:null;
                    var c=new ClientInteraction();Select(c,a.Unit);Check(MimicPresentationUi.ToggleInspection(c,a.Unit),"Initial actual choice denied");
                    var cargo=a.Unit.UnitState;var boat=new UnitState{id=45,type=UnitType.dcp_mimic_form_transportship,UnitData=new(UnitType.dcp_mimic_form_transportship),health=10,passengerUnit=cargo};
                    RecruitmentHooks.Forms[RecruitmentCatalog.Variant("transportship")]=new("transportship",RecruitmentCatalog.Variant("transportship"),"Boat",10,2,0,0,false);
                    GameManager.GameState.Units.Remove(cargo.id);GameManager.GameState.Units[boat.id]=boat;
                    a.Unit.UnitState=boat;MapRenderer.Current!.Units.Clear();MapRenderer.Current.Units[boat.id]=a.Unit;
                    row=row with{NativeId=boat.id,Transport=new(cargo.id),Appearance=App("transportship") with{Passenger=app}};
                    Select(c,a.Unit);Check(MimicPresentationUi.MapView(a.Unit)==true&&c.Bar.headerString=="Mimic Boat","Embark wrapper lost actual preference");
                    Check(MimicPresentationState.TryProject(boat,out var p)&&p.ActualMimicBoat&&p.ActualHealth==1,"Actual boat identity missing");
                    var sprites=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().Select(x=>x.sprite).ToArray();
                    Check(sprites.Contains(UnitArtAssets.Get("mimic_boat_fixed"))&&sprites.Contains(UnitArtAssets.Get("mimic_boat_team")),"Boat portrait layers missing");
                    MimicMapInspection.Refresh(a.Unit);var owned=a.Unit.spriteContainer.gameObject.ComponentsInChildren<SpriteRenderer>().ToArray();
                    Check(owned.Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("mimic_boat_fixed"))),"Actual map boat missing");
                    Check(!owned.Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("mimic_mask"))),"Boat uses land Mimic artwork");
                    Check(boat.type==UnitType.dcp_mimic_form_transportship&&ReferenceEquals(boat.passengerUnit,cargo)&&cargo.health==10,"Map boat changed native body/cargo");
                    GameManager.GameState.Units.Remove(boat.id);GameManager.GameState.Units[cargo.id]=cargo;a.Unit.UnitState=cargo;MapRenderer.Current.Units.Clear();MapRenderer.Current.Units[cargo.id]=a.Unit;
                    row=row with{NativeId=cargo.id,Transport=null,Appearance=app};Select(c,a.Unit);
                    Check(MimicPresentationUi.MapView(a.Unit)==true&&c.Bar.headerString=="Mimic","Disembark lost logical actual choice");
                });
                Case(()=>{
                    var a=Actor();var row=Row(a.Unit,LedgerActualKind.Genuine,App());MimicPresentationState.ReadIdentity=(_,_)=>row;
                    var c=new ClientInteraction();Select(c,a.Unit);var popup=Popup(a.Unit);popup.Show();
                    Check(popup.IsShowing()&&popup.Header=="Rider","Apparent popup setup failed");
                    row=row with{Independent=false,Appearance=null};MimicPresentationUi.Tick();
                    Check(!popup.IsShowing()&&!MimicPresentationUi.PopupCanShow(popup),"Revealed disguise retained stale apparent popup");
                    Check(c.Bar.headerString=="Warrior"&&a.Unit.UnitState.type==UnitType.warrior,"Reveal restored incorrect identity");
                });
                Case(()=>{
                    var a=Actor();var row=Row(a.Unit,LedgerActualKind.Genuine,App());MimicPresentationState.ReadIdentity=(_,id)=>id==row.NativeId?row:null;
                    MimicMapInspection.Refresh(a.Unit);var child=Overlay(a.Unit)!;
                    MimicMapInspection.UnitDestroying(a.Unit);Check(child.Destroyed&&a.Body.color.a==.4f,"Pooling did not release old overlay");
                    var original=a.Unit.UnitState;GameManager.GameState.Units.Remove(original.id);
                    a.Unit.UnitState=new(){id=55,type=UnitType.warrior,UnitData=new(UnitType.warrior),health=100};GameManager.GameState.Units[55]=a.Unit.UnitState;
                    MapRenderer.Current!.Units.Remove(original.id);MapRenderer.Current.Units[55]=a.Unit;MimicMapInspection.Refresh(a.Unit);
                    Check(Overlay(a.Unit) is null&&a.Body.color.a==.4f,"Pooled genuine unmasked unit inherited old disguise");
                    Check(NativeVisualFixture.CloneCalls==1,"Pooled unmasked unit allocated disguise visual");
                });
                Case(()=>{
                    var profile=App();var prefab=PrefabManager.GetPrefab(UnitType.rider,PolytopiaBackendBase.Common.TribeType.Imperius,PolytopiaBackendBase.Common.SkinType.Default);
                    var parts=prefab.gameObject.FindComponent<SkinVisualsReference>()!.visualParts;
                    var source=parts[0].renderer.spriteRenderer!;source.transform.localRotation=Quaternion.Euler(0,0,37);source.transform.localScale=new(2,-3,1);source.sortingOrder=8;
                    parts[1].renderer.spriteRenderer!.sortingOrder=1;parts[0].outlineRenderer.spriteRenderer!.gameObject.SetActive(false);
                    var snapshot=NativeVisualSnapshot.Capture("rider",profile);var actual=snapshot.Single(x=>ReferenceEquals(x.Sprite,source.sprite));
                    Check(Math.Abs(actual.Rotation-37)<.0001f&&Math.Abs(actual.ScaleX-2)<.0001f&&Math.Abs(actual.ScaleY+3)<.0001f,"Native rotated/reflected sprite geometry lost");
                    Check(snapshot.Any(x=>x.Outline)&&snapshot.Last().Order==8,"Inactive ready outline or native part order lost");
                    source.transform.localRotation=Quaternion.Euler(20,0,0);Reject(()=>NativeVisualSnapshot.Capture("rider",profile));
                    Check(NativeVisualFixture.Clones.All(x=>x.gameObject.Destroyed),"Rejected nonplanar clone leaked");
                });
                Case(()=>{
                    var a=Actor(true);const string form="firedragon";
                    RecruitmentHooks.Forms[RecruitmentCatalog.Variant(form)]=new(form,RecruitmentCatalog.Variant(form),"Dragon",20,3,1,2,false);
                    a.Unit.UnitState.type=UnitType.dcp_mimic_form_firedragon;a.Unit.UnitState.UnitData=new(UnitType.dcp_mimic_form_firedragon);
                    var row=Row(a.Unit,LedgerActualKind.Mimic,App(form,200,200));MimicPresentationState.ReadIdentity=(_,_)=>row;
                    var tile=new TileData{unit=a.Unit.UnitState,coordinates=a.Unit.UnitState.coordinates,IsWater=true};GameManager.GameState.Map.Add(tile);
                    var c=new ClientInteraction();Select(c,a.Unit);Check(MimicPresentationUi.ToggleInspection(c,a.Unit),"Flying Mimic truth toggle denied");
                    Check(MimicPresentationState.TryProject(a.Unit.UnitState,out var p)&&p.ActualMimicBoat&&p.ActualLabel=="Mimic Boat","Water flying disguise did not choose boat art");
                    Check(Art(a.Unit).Length==2&&Art(a.Unit).Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("mimic_boat_fixed"))),"Water flying Mimic has land parts");
                    Check(c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("mimic_boat_team"))),"Water flying portrait is not boat");
                    tile.IsWater=false;MimicPresentationUi.Tick();MimicMapInspection.Refresh(a.Unit);
                    Check(MimicPresentationState.TryProject(a.Unit.UnitState,out p)&&!p.ActualMimicBoat&&p.ActualLabel=="Mimic","Land flying disguise retained boat art");
                    Check(Art(a.Unit).Length==4&&Art(a.Unit).Any(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("mimic_mask"))),"Land transition did not replace boat composition");
                    Check(MimicPresentationUi.MapView(a.Unit)==true&&c.Bar.headerString=="Mimic","Tile visual change lost persistent actual choice");
                    Check(a.Unit.UnitState.type==UnitType.dcp_mimic_form_firedragon&&a.Unit.UnitState.health==10&&a.Unit.UnitState.passengerUnit is null&&row.Transport is null,"Water artwork created a real vessel/passenger or changed actual health");
                });
                Case(()=>{
                    var a=Actor(true);var row=Row(a.Unit,LedgerActualKind.Mimic,App("warrior"));MimicPresentationState.ReadIdentity=(_,_)=>row;
                    var tile=new TileData{unit=a.Unit.UnitState,coordinates=a.Unit.UnitState.coordinates,Wetland=true};GameManager.GameState.Map.Add(tile);
                    Check(MimicPresentationState.TryProject(a.Unit.UnitState,out var p)&&!p.ActualMimicBoat&&p.ActualLabel=="Mimic","Flooded tile changed incoming land appearance");
                    row=row with{MimicWaterForm=true};
                    Check(MimicPresentationState.TryProject(a.Unit.UnitState,out p)&&p.ActualMimicBoat&&p.ActualLabel=="Mimic Boat","Flooded tile lost incoming boat appearance without a native wrapper");
                    Check(row.Transport is null&&a.Unit.UnitState.passengerUnit is null&&a.Unit.UnitState.health==10,"Private flooded boat appearance changed native cargo or health");
                    tile.Improvement=Polytopia.Data.ImprovementData.Type.city;
                    Check(MimicPresentationState.TryProject(a.Unit.UnitState,out p)&&p.ActualMimicBoat,"Flooded city lost incoming boat mode");
                    row=row with{MimicWaterForm=false};
                    Check(MimicPresentationState.TryProject(a.Unit.UnitState,out p)&&!p.ActualMimicBoat,"Flooded city changed incoming land mode");
                    row=row with{MimicWaterForm=true};tile.Wetland=false;tile.IsWater=true;
                    Check(MimicPresentationState.TryProject(a.Unit.UnitState,out p)&&!p.ActualMimicBoat&&p.ActualLabel=="Mimic","Non-flood coastal city did not use native standing-ground mode");
                    tile.IsWater=false;row=row with{Transport=new(21)};
                    Check(MimicPresentationState.TryProject(a.Unit.UnitState,out p)&&!p.ActualMimicBoat,"Ledger wrapper overrode land artwork classification");
                });
                Case(()=>{
                    var a=Actor(true);var row=Row(a.Unit,LedgerActualKind.Mimic,App("warrior")) with{AmphibiousMimic=true};
                    MimicPresentationState.ReadIdentity=(_,_)=>row;
                    var tile=new TileData{unit=a.Unit.UnitState,coordinates=a.Unit.UnitState.coordinates};GameManager.GameState.Map.Add(tile);
                    foreach(var terrain in new[]{"land","water","flood"})
                    {
                        tile.IsWater=terrain=="water";tile.Wetland=terrain=="flood";
                        Check(MimicPresentationState.TryProject(a.Unit.UnitState,out var p)&&p.ActualAmphibiousMimic&&!p.ActualMimicBoat&&p.ActualLabel=="Mermimic","Mermimic gained boat art on "+terrain);
                    }
                    Check(a.Unit.UnitState.passengerUnit is null&&a.Unit.UnitState.health==10,"Amphibious art created cargo or changed health");
                });
                Case(()=>{
                    var a=Actor(true);var row=Row(a.Unit,LedgerActualKind.Mimic,App("warrior"));MimicPresentationState.ReadIdentity=(_,_)=>row;
                    GameManager.GameState.Map.Add(new(){unit=a.Unit.UnitState,coordinates=a.Unit.UnitState.coordinates,IsWater=true});
                    MimicMapInspection.Configure(_=>true);MimicMapInspection.Refresh(a.Unit);var child=Truth(a.Unit)!;var art=Art(a.Unit);
                    float expected=.313131313f/(UnitArtAssets.BoatVisibleWidth/100f)*.95f*1.2f;
                    Check(Math.Abs(child.transform.localScale.x-expected)<.00001f&&Math.Abs(child.transform.localScale.y-expected)<.00001f,"Boat map was not enlarged uniformly by twenty percent");
                    Check(art.Length==2&&art.All(x=>x.transform.localScale.x<0&&x.transform.localScale.y>0),"Boat fixed/team parts do not share horizontal reflection");
                    Check(art.Select(x=>x.transform.localScale).Distinct().Count()==1&&art.Select(x=>x.transform.localPosition).Distinct().Count()==1,"Reflected boat stripe is misaligned");
                    float frameLeft=-.199165656f,frameRight=.113965656f;
                    float silhouetteX=child.transform.localPosition.x-UnitArtAssets.BoatVisibleCenterX/100f*child.transform.localScale.x;
                    Check(Math.Abs(silhouetteX-((frameLeft+frameRight)/2+(frameRight-frameLeft)*.04f))<.00001f,"Boat visible center still follows transparent canvas padding");
                    Check(child.transform.localPosition.y+UnitArtAssets.BoatFootBaseline/100f*child.transform.localScale.y>-.02345455f,"Boat up correction is absent");
                    var outline=child.ComponentsInChildren<SpriteRenderer>().Where(x=>x.gameObject.name=="Deception actual outline").ToArray();
                    Check(outline.Length==2&&outline.All(x=>x.transform.localScale.x<0),"Boat glow did not mirror with silhouette");
                    var c=new ClientInteraction();Select(c,a.Unit);MimicPresentationUi.ToggleInspection(c,a.Unit);
                    var images=c.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<Image>().Where(x=>ReferenceEquals(x.sprite,UnitArtAssets.Get("mimic_boat_fixed"))||ReferenceEquals(x.sprite,UnitArtAssets.Get("mimic_boat_team"))).ToArray();
                    Check(images.Length==2&&images.All(x=>x.transform.localScale.x<0),"Bottom boat portrait reflection differs from map");
                    Check(a.Unit.UnitState.health==10&&a.Unit.UnitState.passengerUnit is null&&!a.Unit.UnitState.attacked,"Boat geometry mutated native unit");
                });
                Case(()=>{
                    var a=Actor(false,true);var cargo=new UnitState{id=21,type=UnitType.warrior,UnitData=new(UnitType.warrior),health=100};
                    var status=a.Unit.statusDisplay!;status.ProductionHealthCallbacks=true;
                    a.Unit.UnitState.type=UnitType.transportship;a.Unit.UnitState.UnitData=new(UnitType.transportship);a.Unit.UnitState.health=100;a.Unit.UnitState.passengerUnit=cargo;
                    var row=Row(a.Unit,LedgerActualKind.Genuine,App("firedragon",200,200),transport:new(21));MimicPresentationState.ReadIdentity=(_,_)=>row;
                    GameManager.GameState.Map.Add(new(){unit=a.Unit.UnitState,coordinates=a.Unit.UnitState.coordinates,IsWater=true});
                    var c=new ClientInteraction();Select(c,a.Unit);MimicMapInspection.Refresh(a.Unit);
                    Check(MimicPresentationState.TryProject(a.Unit.UnitState,out var p)&&!p.ActualIsMimic&&p.ActualNativeId=="transportship"&&p.ActualBadgeNativeId=="warrior","Vessel body and native passenger badge identities merged");
                    Check(p.ActualHealth==10&&p.ActualMaxHealth==10&&p.ApparentHealth==20&&p.ApparentMaxHealth==20,"Warrior raft copied dragon actual health");
                    Check(status.LastState!.health==200&&status.LastState.type==UnitType.firedragon,"Apparent map state did not use dragon HP/form");
                    Check(Overlay(a.Unit)?.activeSelf==true&&a.Body.color.a==0,"Genuine raft failed to paint apparent dragon");
                    Check(Badges(a.Unit).Length==1&&Badges(a.Unit)[0].ComponentsInChildren<SpriteRenderer>().Any(x=>x.sprite?.Name=="Native icon warrior"),"Genuine raft private badge did not follow real passenger icon");
                    Check(!SpriteHandle.Requested.Any(x=>x.Addresses?[0].sprite=="transportship"),"Missing vessel-type glyph requested instead of native cargo glyph");
                    int statusCalls=status.StateCalls;Check(MimicPresentationUi.ToggleInspection(c,a.Unit),"Genuine disguised raft actual toggle denied");
                    Check(Overlay(a.Unit)?.activeSelf==false&&a.Body.color.a==.4f&&Badges(a.Unit).Length==1,"Actual raft view lost body or paired badge");
                    Check(status.StateCalls==statusCalls+1&&status.LastState!.health==100&&UnitDataExtensions.GetMaxHealth(status.LastState,GameManager.GameState)==100,"Actual toggle did not immediately restore map 10/10");
                    Check(status.LastState!.type==UnitType.firedragon&&status.typeIcon.sprite?.Name=="Status icon firedragon","Actual map health refresh replaced apparent badge identity");
                    statusCalls=status.StateCalls;for(int i=0;i<30;i++)MimicMapInspection.Refresh(a.Unit);
                    Check(status.StateCalls==statusCalls,"Idle map frames repeatedly repopulated native status");
                    var popup=Popup(a.Unit);Check(Health(popup)=="10/10","Actual genuine raft stats use dragon HP");
                    Check(a.Unit.UnitState.health==100&&cargo.health==100&&a.Unit.UnitState.UnitData.health==100&&cargo.UnitData.health==100&&ReferenceEquals(a.Unit.UnitState.passengerUnit,cargo),"Private toggle changed live vessel/passenger health/data");
                    GameManager.GameState.Players[GameManager.LocalPlayer.Id]=GameManager.LocalPlayer;GameManager.LocalPlayer=new(){Id=2};MimicMapInspection.Refresh(a.Unit);
                    Check(status.LastState!.health==200&&MimicPresentationUi.MapView(a.Unit)==false&&Badges(a.Unit).Length==0,"Viewer handoff exposed actual HP/private badge");
                });
                Case(()=>{
                    var a=Actor(false,true);var cargo=new UnitState{id=21,type=UnitType.warrior,UnitData=new(UnitType.warrior),health=75,promotionLevel=1};
                    a.Unit.UnitState.type=UnitType.scoutship;a.Unit.UnitState.UnitData=new(UnitType.scoutship);a.Unit.UnitState.health=75;a.Unit.UnitState.passengerUnit=cargo;
                    var fakeCargo=App("firedragon",180,200) with{Veteran=true};var app=App("scoutship",180,200) with{Passenger=fakeCargo};
                    var row=Row(a.Unit,LedgerActualKind.Genuine,app,transport:new(21));MimicPresentationState.ReadIdentity=(_,_)=>row;
                    var status=a.Unit.statusDisplay!;status.ProductionHealthCallbacks=true;var realData=a.Unit.UnitState.UnitData;
                    var c=new ClientInteraction();Select(c,a.Unit);Check(MimicPresentationUi.ToggleInspection(c,a.Unit),"Veteran cargo actual toggle denied");
                    var display=status.LastState!;Check(display.health==75&&display.passengerUnit!.health==75&&UnitDataExtensions.GetMaxHealth(display,GameManager.GameState)==150,"Actual wounded veteran cargo maximum was not preserved");
                    Check(display.type==UnitType.scoutship&&display.passengerUnit!.type==UnitType.firedragon&&display.promotionLevel==0&&display.passengerUnit.promotionLevel==0,"Actual health clone changed badge or double-counted veteran maximum");
                    Check(!ReferenceEquals(display,a.Unit.UnitState)&&!ReferenceEquals(display.UnitData,realData)&&!ReferenceEquals(display.passengerUnit,cargo)&&!ReferenceEquals(display.passengerUnit!.UnitData,cargo.UnitData),"Actual map health clone aliases authoritative data");
                    Check(a.Unit.UnitState.health==75&&cargo.health==75&&cargo.promotionLevel==1&&cargo.UnitData.health==100&&fakeCargo.Health==180&&fakeCargo.Veteran,"Actual status projection mutated saved health/veterancy");
                    int calls=status.StateCalls;status.unit=new Unit();MimicPresentationUi.RefreshMapHealth(a.Unit);Check(status.StateCalls==calls,"Foreign pooled status received old unit HP");
                    status.unit=a.Unit;status.IsUsed=false;MimicPresentationUi.RefreshMapHealth(a.Unit);Check(status.StateCalls==calls,"Unused status received health repopulation");
                });
                Console.WriteLine(JsonSerializer.Serialize(new{suite="Naval and genuine disguise presentation",scope="Production adapters against synthetic native contracts; no Unity/game acceptance",cases=Cases,assertions=Assertions,passed=true}));return 0;
            }
            catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{cases=Cases,assertions=Assertions,passed=false,error=e.ToString()}));return 1;}
        }
    }
}
