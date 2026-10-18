using System;
using System.Linq;
using Deception.PolyModAdapter;
using UnityEngine;
namespace Deception.Tests
{
    public static partial class KnowledgeTests
    {
        static UIRoundButton Button(World w)=>w.Interaction.Bar.buttons.Single();
        static void Open(World w){BranchArtAssets.Fail=false;NativeSurveillanceUi.Selected(w.Interaction);}
        static void ControlTests()
        {
            Test("SURVEILLANCE-001-selection-default-off-with-explicit-free-icon",()=>{
                var w=new World(false);int reads=w.Fogged.Data.ExploredReads;Open(w);
                Check(!Shown&&w.Fogged.Data.ExploredReads==reads,"Selection auto-enabled or censused the map");
                var b=Button(w);Check(b.text=="Surveillance"&&b.Cost==-1f&&ReferenceEquals(b.Icon,BranchArtAssets.Icon)&&b.ButtonEnabled,"Native explicit icon/caption/free control absent");
                Check(b.icon.preserveAspect&&b.NativeSize,"Tall tech artwork is stretched into the square action icon");
                string snapshot=w.DataSnapshot();b.OnClicked!.Click();Check(Shown&&Marks().Length==1,"First deliberate click did not enable");b.OnClicked!.Click();Check(!Shown&&snapshot==w.DataSnapshot(),"Second click failed or changed game state");
            });
            foreach(bool city in new[]{false,true})Test("SURVEILLANCE-002-waiting-"+(city?"city":"unit"),()=>{
                var w=new World(false);if(city){w.Interaction.selectedUnit=null;w.Interaction.selectedTile=w.Selected;w.Selected.Data.City=true;w.Select();}
                w.Client.IsWaitingForCommand=true;w.Client.ActionManager.IsProcessing=true;Open(w);Button(w).OnClicked!.Click();
                Check(Shown,"Authorized waiting viewer cannot inspect selected opponent");Check(w.Unit.UnitState.health==50&&!w.Unit.UnitState.Moved&&!w.Unit.UnitState.Attacked,"Read-only control changed unit budget");
            });
            Test("SURVEILLANCE-003-enabled-choice-persists-without-selection-auto-enable",()=>{
                var w=new World(false);Open(w);Button(w).OnClicked!.Click();NativeIntelligenceOverlay.Clear();w.Select();Check(Shown,"Explicit opt-in lost on reselection");
                w.Unit.UnitState.owner=3;w.Select();Check(!Shown,"Another opponent auto-enabled");w.Unit.UnitState.owner=2;w.Select();Check(Shown,"Original deliberate opt-in lost");
            });
            Test("SURVEILLANCE-004-button-idle-reuse-and-native-pool-cleanup",()=>{
                var w=new World(false);Open(w);var b=Button(w);int count=GameObject.All.Count;for(int i=0;i<40;i++)NativeSurveillanceUi.Tick();
                Check(ReferenceEquals(b,Button(w))&&GameObject.All.Count==count,"Idle frames allocate controls");
                NativeSurveillanceUi.NativeClear(w.Interaction.Bar);Check(b.OnClicked is null&&!b.gameObject.Destroyed,"Native pool callback retained or native button destroyed");
                w.Interaction.Bar.buttons.Clear();NativeSurveillanceUi.Tick();Check(w.Interaction.Bar.buttons.Count==1,"Native bar rebuild did not restore control");
            });
            Test("SURVEILLANCE-005-native-reused-button-preserved",()=>{
                var w=new World(false);Open(w);var b=Button(w);var old=b.OnClicked!;b.text="Native action";b.OnClicked=new((_,_)=>{});var replacement=b.OnClicked;
                NativeSurveillanceUi.Clear();old.Click();Check(b.text=="Native action"&&ReferenceEquals(replacement,b.OnClicked)&&!b.gameObject.Destroyed&&!Shown,"Retired callback changed pooled control or toggled");
            });
            foreach(string reason in new[]{"turn","owner","unit","hidden","invisible","research","client","popup","bar-list","bar-hidden"})Test("SURVEILLANCE-006-stale-click-"+reason,()=>{
                var w=new World(false);Open(w);var b=Button(w);var click=b.OnClicked!;
                switch(reason){case "turn":w.State.CurrentTurn++;break;case "owner":w.Unit.UnitState.owner=3;break;case "unit":w.Interaction.selectedUnit=new();break;case "hidden":w.Selected.IsHidden=true;break;case "invisible":w.Unit.Invisible=true;break;case "research":w.Viewer.Intelligence=false;break;case "client":GameManager.Client=new();break;case "popup":PopupManager.PopupShowing=true;break;case "bar-list":w.Interaction.Bar.buttons=new();break;case "bar-hidden":w.Interaction.Bar.gameObject.SetActive(false);break;}
                click.Click();Check(!Shown,"Stale or hidden control enabled overlay");
            });
            Test("SURVEILLANCE-007-partial-button-construction-rolls-back",()=>{
                var w=new World(false);BranchArtAssets.Fail=true;NativeSurveillanceUi.Selected(w.Interaction);Check(w.Interaction.Bar.buttons.Count==0,"Failed factory left appended button");
                Check(GameObject.All.Where(g=>g.Name=="native-button").All(g=>g.Destroyed),"Partial button root leaked");BranchArtAssets.Fail=false;NativeSurveillanceUi.Tick();Check(w.Interaction.Bar.buttons.Count==1,"Valid retry failed");
            });
            Test("SURVEILLANCE-008-ordinary-city-unit-and-portrait-authority",()=>{
                var w=new World(false);w.Unit.UnitState.owner=1;w.Select();Open(w);Check(w.Interaction.Bar.buttons.Count==0,"Own unit granted opponent inspection");
                w.Unit.UnitState.owner=2;w.Select();Open(w);NativeIntelligenceOverlay.BeforeDuplicate();NativeSurveillanceUi.Tick();Check(w.Interaction.Bar.buttons.Count==0,"Portrait capture retained inspection control");NativeIntelligenceOverlay.AfterDuplicate(null);
            });
            Test("SURVEILLANCE-009-native-hook-signatures",()=>{var h=new HarmonyLib.Harmony();NativeSurveillanceUi.Install(h);Check(h.Hooks.Count(x=>x.Target.DeclaringType==typeof(ClientInteraction)&&x.Target.Name.StartsWith("Select"))==2,"City/unit selection hook absent");Check(h.Hooks.Any(x=>x.Target.DeclaringType==typeof(InteractionBar)&&x.Target.Name=="ClearButtons"),"Pool lifecycle hook absent");});
        }
    }
}
