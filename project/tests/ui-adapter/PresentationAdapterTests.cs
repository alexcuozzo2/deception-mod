// Executes production callbacks against synthetic wrappers. This does not run
// Harmony detours, IL2CPP marshalling, native game predicates, Unity or gameplay.
using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using Polytopia.Data;

namespace Deception.Tests
{
    public static class PresentationAdapterTests
    {
        sealed class ZeroPointerUnit
        {public IntPtr Pointer=>IntPtr.Zero;public UnitData.Type type=>UnitData.Type.dcp_mimic_form_warrior;}
        static void Check(bool value,string message="Adapter assertion failed") {if(!value)throw new Exception(message);}
        static void Reject(Action operation)
        {bool failed=false;try{operation();}catch(InvalidOperationException){failed=true;}Check(failed,"Expected invalid native state to be rejected");}
        static UnitState Register(UnitState state){GameManager.GameState.Units[state.id]=state;return state;}
        static Unit Actor()=>new(){UnitState=Register(new UnitState())};
        static void Select(ClientInteraction interaction,Unit unit)
        {interaction.SelectUnit(unit);MimicPresentationUi.Selected(interaction,unit);MimicPresentationUi.BarReady(interaction.Bar);}
        static UnitPopup Popup(Unit unit)
        {
            PopupManager.ProductionCallbacks=true;
            return PopupManager.GetUnitPopup(new(){unit=unit,unitData=unit.UnitState.UnitData});
        }
        static string Stat(UnitPopup popup,string name)
        {
            string? value=null;int count=0;
            foreach(var row in Native.Items(popup.statsRows))
            {var r=(UnitStatsRow)row;if(r.statsNameLabel.text==name){value=r.statsValueLabel.text;count++;}}
            Check(count==1,"Expected exactly one "+name+" row, found "+count);return value!;
        }
        static void ResetContext()
        {
            MimicPresentationUi.Clear();MimicPresentationState.Clear();MimicHoldInput.Clear();
            GameManager.GameState=new();GameManager.LocalPlayer=new();GameManager.Client=new();
            PopupManager.PopupShowing=false;PopupManager.BeforePopulate=null;PopupManager.AfterPopulate=null;
        }
        public static void Run(Action<string,Action> test)
        {
            MenuAndAbilityAdapterTests.Run(test);
            test("UI-176-disguise-label-is-private-through-viewer-change-and-reset",()=> {
                var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                Check(interaction.Bar.headerString=="Warrior (Disguise)");
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                GameManager.LocalPlayer=new(){Id=2};MimicPresentationUi.SelectionCleared();
                Check(interaction.Bar.headerString=="Warrior","Cleanup leaked private suffix to new viewer");
                Select(interaction,unit);Check(interaction.Bar.headerString=="Warrior");
                MimicPresentationUi.BeforeBarRefresh(interaction.Bar);MimicPresentationUi.BarReady(interaction.Bar);
                Check(interaction.Bar.headerString=="Warrior","Enemy refresh leaked suffix");
                GameManager.LocalPlayer=new();Select(interaction,unit);
                if(interaction.Bar.headerString=="Mimic")Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                Check(interaction.Bar.headerString=="Warrior (Disguise)","Owner lost private distinction");
                GameManager.Client.IsReplay=true;MimicPresentationUi.BarReady(interaction.Bar);
                Check(interaction.Bar.headerString=="Warrior","Detached replay leaked suffix");
            });
            test("UI-141-bar-regroup-form-and-owner-color-invalidate-without-frame-rebuild",()=> {
                var unit=Actor();var interaction=new ClientInteraction();
                LedgerAppearance appearance=new("warrior",new("Imperius","Default"),new("Imperius","Default"),new("Imperius","Default"),
                    GameManager.LocalPlayer.color,100,100,false,ApparentConcealment.Visible,-1,new[]{"test-effect"});
                MimicPresentationState.ReadAppearance=(_,_)=>appearance with{Effects=appearance.Effects.ToArray()};
                Select(interaction,unit);int count=UIUtils.Previews.Count;
                for(int i=0;i<20;i++){MimicPresentationUi.BarReady(interaction.Bar);MimicPresentationUi.Tick();}
                Check(UIUtils.Previews.Count==count,"Value-identical appearance recreated native preview each frame");
                unit.UnitState.type=UnitData.Type.dcp_mimic_form_rider;unit.UnitState.UnitData=new(UnitData.Type.dcp_mimic_form_rider);
                appearance=appearance with{Form="rider"};MimicPresentationUi.BarReady(interaction.Bar);
                Check(UIUtils.Previews.Count==++count&&UIUtils.Previews.Last().unitType==UnitData.Type.rider&&interaction.Bar.headerString=="Rider (Disguise)");
                var images=interaction.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();
                Check(images.Any(i=>i.sprite!.Name=="rider static body")&&!images.Any(i=>i.sprite!.Name=="Warrior body"),"Old map prefab survived Regroup bottom portrait refresh");
                Check(UIUtils.Previews.Last().gameObject.Destroyed&&images.All(i=>!i.gameObject.Destroyed));
                GameManager.LocalPlayer.color=0x334455;MimicPresentationUi.BarReady(interaction.Bar);Check(UIUtils.Previews.Count==++count,"Owner color did not refresh actual portrait");
                appearance=appearance with{SourceColor=0x778899};MimicPresentationUi.BarReady(interaction.Bar);Check(UIUtils.Previews.Count==++count,"Copied color did not refresh disguise portrait");
                for(int i=0;i<10;i++)MimicPresentationUi.BarReady(interaction.Bar);
                Check(UIUtils.Previews.Count==count&&unit.UnitState.health==10&&!unit.UnitState.moved&&!unit.UnitState.attacked);
                MimicPresentationState.ReadAppearance=null;
            });
            test("UI-143-elyrion-regroup-polytaur-preview-keeps-exact-form",()=> {
                GameManager.LocalPlayer.tribe=PolytopiaBackendBase.Common.TribeType.Elyrion;
                var unit=Actor();var interaction=new ClientInteraction();
                LedgerAppearance appearance=new("warrior",new("Elyrion","Default"),new("Elyrion","Default"),new("Elyrion","Default"),
                    GameManager.LocalPlayer.color,100,100,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
                MimicPresentationState.ReadAppearance=(_,_)=>appearance;Select(interaction,unit);
                Check(UIUtils.Previews.Count>0&&interaction.Bar.duplicatedSprites.rectTransform.childCount==5,
                    "Recorded Elyrion enum name failed before dual portrait publication");
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                Check(interaction.Bar.headerString=="Mimic"&&interaction.Bar.duplicatedSprites.rectTransform.childCount==5);
                RecruitmentHooks.Forms["dcp_mimic_form_polytaur"]=new("polytaur","dcp_mimic_form_polytaur","Polytaur",15,1,30,10,false);
                try
                {
                    unit.UnitState.type=UnitData.Type.dcp_mimic_form_polytaur;unit.UnitState.UnitData=new(UnitData.Type.dcp_mimic_form_polytaur);
                    appearance=appearance with{Form="polytaur",Health=150,Maximum=150};MimicPresentationUi.BarReady(interaction.Bar);
                    Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                    var images=interaction.Bar.duplicatedSprites.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().ToArray();
                    Check(UIUtils.Previews.Last().unitType==UnitData.Type.polytaur&&interaction.Bar.headerString=="Polytaur (Disguise)");
                    Check(images.Length==5&&images.Any(i=>i.sprite!.Name=="polytaur static body")&&!images.Any(i=>i.sprite!.Name=="Warrior body"),
                        "Polytaur health changed but menu still copied the old Warrior prefab");
                    var popup=Popup(unit);Check(Stat(popup,"Health")=="15/15"&&popup.PopulationCalls==1);popup.Hide();
                    Check(unit.UnitState.health==10&&GameManager.LocalPlayer.tribe==PolytopiaBackendBase.Common.TribeType.Elyrion);
                    Check(!Entry.Notes.Any(n=>n.Kind=="mimic-private-bar-portrait-error"));
                }
                finally{RecruitmentHooks.Forms.Remove("dcp_mimic_form_polytaur");MimicPresentationState.ReadAppearance=null;}
            });
            test("UI-142-failed-preview-is-latched-until-context-changes",()=> {
                var unit=Actor();var interaction=new ClientInteraction();
                UIUtils.PreviewOverride=(renderer,data,player,skin)=>renderer.unitType=UnitData.Type.knight;
                Select(interaction,unit);int count=UIUtils.Previews.Count;
                Check(count==1&&UIUtils.Previews.All(r=>r.gameObject.Destroyed),"Failed native preview leaked or initial callback retried: count="+count+" destroyed="+string.Join(",",UIUtils.Previews.Select(r=>r.gameObject.Destroyed))+" notes="+string.Join(";",Entry.Notes.Select(n=>n.Kind+":"+n.Message)));
                for(int i=0;i<20;i++){MimicPresentationUi.BarReady(interaction.Bar);MimicPresentationUi.Tick();}
                Check(UIUtils.Previews.Count==count,"Identical failed preview retried each frame");
                UIUtils.PreviewOverride=null;unit.UnitState.type=UnitData.Type.dcp_mimic_form_rider;unit.UnitState.UnitData=new(UnitData.Type.dcp_mimic_form_rider);
                MimicPresentationUi.BarReady(interaction.Bar);
                Check(UIUtils.Previews.Count==count+1&&interaction.Bar.headerString=="Rider (Disguise)","New form did not retry preview");
            });
            test("UI-104-delayed-head-keeps-bar-title-and-upgrades-open-popup",()=> {
                SpriteHandle.Delay=true;var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                Check(interaction.Bar.headerString=="Mimic"&&interaction.Bar.duplicatedSprites.rectTransform.childCount==2);
                var popup=Popup(unit);popup.Show();var portrait=popup.unitRenderer.duplicatedSpritesContainer;
                Check(popup.Header=="Mimic"&&portrait.rectTransform.childCount==1&&ReferenceEquals(portrait.LastSprite,UnitArtAssets.Icon()));
                MimicPresentationUi.Tick();Check(popup.IsShowing()&&popup.Header=="Mimic");
                SpriteHandle.Requested[0].Complete(GameManager.Atlas.Head);MimicPresentationUi.Tick();
                Check(popup.IsShowing()&&portrait.rectTransform.childCount==4&&ReferenceEquals(portrait.LastSprite,UnitArtAssets.Get("mimic_mask")));
                Check(interaction.Bar.headerString=="Mimic"&&interaction.Bar.duplicatedSprites.rectTransform.childCount==5);
                Check(popup.PopulationCalls==1&&Stat(popup,"Health")=="1/1"&&SpriteHandle.Requested.Count==1);
                popup.Hide();Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                Check(interaction.Bar.headerString=="Warrior (Disguise)"&&unit.UnitState.health==10&&!unit.UnitState.moved&&!unit.UnitState.attacked);
            });
            test("UI-105-popup-tag-reflows-final-title-using-native-gap",()=> {
                var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                PopupManager.AfterPopulate=()=>PopupManager.LastPopup!.abilityButtonMovement.rectTransform.anchoredPosition=new(280,-7);
                var apparent=Popup(unit);
                Check(apparent.abilityButtonMovement.rectTransform.anchoredPosition==new UnityEngine.Vector2(apparent.header.GetPreferredValues().x+12,-7));
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));var actual=Popup(unit);
                Check(actual.Header=="Mimic"&&actual.abilityButtonMovement.rectTransform.anchoredPosition==new UnityEngine.Vector2(actual.header.GetPreferredValues().x+12,-7));
                Check(actual.abilityButtonMovement.rectTransform.anchoredPosition.x<apparent.abilityButtonMovement.rectTransform.anchoredPosition.x);
                PopupManager.AfterPopulate=()=>{var p=PopupManager.LastPopup!;p.abilityButtonMovement.gameObject.SetActive(false);p.abilityButtonMovement.rectTransform.anchoredPosition=new(280,-7);};
                var hidden=Popup(unit);Check(hidden.abilityButtonMovement.rectTransform.anchoredPosition==new UnityEngine.Vector2(280,-7));
                Check(hidden.PopulationCalls==1&&actual.PopulationCalls==1&&apparent.PopulationCalls==1);
            });
            test("UI-065-closed-popup-retires-native-portrait-source",()=> {
                var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                var popup=Popup(unit);popup.Show();var portrait=popup.unitRenderer.duplicatedSpritesContainer;
                popup.Hide();int restores=portrait.RestoreCalls;Check(restores==1);
                UnityEngine.Object.Destroy(popup.unitRenderer.spriteDuplicationData.source);
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                MimicPresentationUi.Tick();Check(portrait.RestoreCalls==restores);
                popup.Show();Check(!popup.IsShowing());
                Check(!Entry.Notes.Exists(note=>note.Kind=="mimic-private-portrait-close-error"));
            });
            test("UI-066-embedded-mimic-art-is-private-and-cached",()=> {
                var first=MimicPortraitAsset.Get();int builds=PolyMod.Managers.Visual.Builds;
                Check(ReferenceEquals(first,MimicPortraitAsset.Get())&&PolyMod.Managers.Visual.Builds==builds);
                var bytes=ModContentAssets.Read(MimicPortraitAsset.AssetPath);
                Check(bytes.Length==263111&&((UnityEngine.Sprite)first).pixelsPerUnit==100f);
                Check(bytes[0]==137&&bytes[1]==80);
                Check(ReferenceEquals(((UnityEngine.Sprite)first).texture,((UnityEngine.Sprite)PolyMod.Registry.sprites["dcp_mimic-portrait__"]).texture));
                Check(((UnityEngine.Sprite)first).rect==new UnityEngine.Rect(452,213,430,690));
                Check(UnityEngine.Sprite.LastFrame==new UnityEngine.Rect(452,213,430,690)&&UnityEngine.Sprite.LastPivot==new UnityEngine.Vector2(0.5f,0.5f));
                Check(MimicControls.NativeSprite("UNKNOWN_ICON") is UnityEngine.Sprite native&&native.Name=="UNKNOWN_ICON");
            });
            test("UI-054-native-health-dot-leaders",()=> {
                var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                foreach(bool actual in new[]{false,true})
                {
                    if(actual)Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                    PopupManager.AfterPopulate=()=>{
                        foreach(var item in Native.Items(PopupManager.LastPopup!.statsRows))
                            ((UnitStatsRow)item).statsNameLabel.text+=" . . . . . . ";
                    };
                    var popup=Popup(unit);Check(popup.PopulationCalls==1&&popup.statsRows.Count==6);
                    Check(Stat(popup,"Health . . . . . . ")==(actual?"1/1":"10/10"));
                }
                Check(MimicPresentationUi.IsHealthLabel("Santé . . . ","Santé"));
                Check(!MimicPresentationUi.IsHealthLabel("Health bonus . . . ","Health"));
                Check(!MimicPresentationUi.IsHealthLabel("Attack . . . ","Health"));
            });
            test("UI-025-projection-reads-separate-health-records",()=> {
                var unit=Register(new UnitState());var real=unit.UnitData;unit.moved=true;
                Check(MimicPresentationState.TryProject(unit,out var p));
                Check(p.ActualHealth==1&&p.ActualMaxHealth==1&&p.ApparentHealth==10&&p.ApparentMaxHealth==10);
                Check(p.NativeId=="warrior"&&p.VariantId=="dcp_mimic_form_warrior"&&p.UnitId==3&&p.Owner==1);
                Check(unit.health==10&&ReferenceEquals(unit.UnitData,real)&&real.health==10&&real.attack==0&&unit.moved);
                Check(p.ApparentData is UnitData apparent&&apparent.type==UnitData.Type.warrior&&apparent.health==100);
            });
            test("UI-026-projection-refuses-bad-actual-health",()=> {
                var unit=Register(new UnitState{health=20});Reject(()=>MimicPresentationState.TryProject(unit,out _));Check(unit.health==20);
                unit.health=10;unit.UnitData.health=100;Reject(()=>MimicPresentationState.TryProject(unit,out _));Check(unit.UnitData.health==100);
                unit.UnitData.health=10;unit.health=0;Check(!MimicPresentationState.TryProject(unit,out _));
                unit.health=10;RecruitmentHooks.Registered=false;Check(!MimicPresentationState.TryProject(unit,out _));
            });
            test("UI-027-projection-new-world-reused-id",()=> {
                var a=Register(new UnitState());Check(MimicPresentationState.TryProject(a,out var first));
                GameManager.GameState=new();var b=Register(new UnitState{owner=2,type=UnitData.Type.dcp_mimic_form_rider,UnitData=new(UnitData.Type.dcp_mimic_form_rider)});
                Check(MimicPresentationState.TryProject(b,out var next));
                Check(next.UnitId==first.UnitId&&next.Owner==2&&next.NativeId=="rider"&&!Native.Same(first.State,next.State));
                Check(a.health==10&&b.health==10&&a.type==UnitData.Type.dcp_mimic_form_warrior);
                Check(!MimicPresentationState.TryProject(a,out _));
                GameManager.GameState.Units.Remove(b.id);Check(!MimicPresentationState.TryProject(b,out _));
            });
            test("UI-028-detached-clone-never-aliases-native-source",()=> {
                var unit=new UnitState();var clone=(UnitState)Native.DetachedClone(unit,"UnitState");
                Check(!Native.Same(unit,clone));clone.health=100;Check(unit.health==10);
                Il2CppSystem.Object.AliasClone=true;Reject(()=>Native.DetachedClone(unit,"UnitState"));
                Check(unit.health==10&&unit.UnitData.health==10);
            });
            test("UI-029-ordinary-readiness-only-within-own-predicate",()=> {
                var unit=new UnitState();var other=new UnitState{id=4};Check(!OrdinaryActionEligibility.AllowsNativeReadiness(unit));
                OrdinaryActionEligibility.EnterCapture(unit,out var scope);
                try {Check(OrdinaryActionEligibility.AllowsNativeReadiness(unit));Check(!OrdinaryActionEligibility.AllowsNativeReadiness(other));}
                finally {Check(OrdinaryActionEligibility.ExitPredicate(null,scope)==null);}
                Check(!OrdinaryActionEligibility.AllowsNativeReadiness(unit));Check(unit.health==10&&!unit.moved&&!unit.attacked);
            });
            test("UI-030-native-false-and-exception-remain-authoritative",()=> {
                var unit=new UnitState{moved=true,attacked=true,frozen=true};
                OrdinaryActionEligibility.EnterDisband(unit,out var scope);bool native=false;
                OrdinaryActionEligibility.ObserveResult(native,scope);Check(!native&&scope.NativeResult==false);
                Check(OrdinaryActionEligibility.AllowsNativeReadiness(unit));Check(!unit.CanAttack());
                var failure=new InvalidOperationException("synthetic native predicate failure");
                Check(ReferenceEquals(OrdinaryActionEligibility.ExitPredicate(failure,scope),failure));
                Check(!OrdinaryActionEligibility.AllowsNativeReadiness(unit)&&unit.moved&&unit.attacked&&unit.frozen&&unit.health==10);
            });
            test("UI-031-nested-predicates-mask-and-unwind",()=> {
                var unit=new UnitState();var ordinary=new UnitState{type=UnitData.Type.warrior,UnitData=new(UnitData.Type.warrior)};
                OrdinaryActionEligibility.EnterCapture(unit,out var outer);
                OrdinaryActionEligibility.EnterRuins(ordinary,out var inner);
                Check(!OrdinaryActionEligibility.AllowsNativeReadiness(unit)&&!OrdinaryActionEligibility.AllowsNativeReadiness(ordinary));
                OrdinaryActionEligibility.ExitPredicate(new Exception("inner"),inner);Check(OrdinaryActionEligibility.AllowsNativeReadiness(unit));
                var second=new UnitState{id=4};OrdinaryActionEligibility.EnterRuins(second,out var nested);
                Check(!OrdinaryActionEligibility.AllowsNativeReadiness(unit)&&OrdinaryActionEligibility.AllowsNativeReadiness(second));
                OrdinaryActionEligibility.ExitPredicate(null,nested);Check(OrdinaryActionEligibility.AllowsNativeReadiness(unit));
                OrdinaryActionEligibility.ExitPredicate(null,outer);Check(!OrdinaryActionEligibility.AllowsNativeReadiness(unit));
            });
            test("UI-032-predicate-targets-are-narrow-and-registered",()=> {
                var h=new HarmonyLib.Harmony();OrdinaryActionEligibility.Install(h);
                Check(h.Patched.Count==3&&h.Patched.Contains("UnitDataExtensions::CanCapture")&&h.Patched.Contains("UnitDataExtensions::CanExamineRuins")&&h.Patched.Contains("UnitDataExtensions::CanDisband"));
                var unit=new UnitState();RecruitmentHooks.Registered=false;OrdinaryActionEligibility.EnterDisband(unit,out var scope);
                Check(!OrdinaryActionEligibility.AllowsNativeReadiness(unit));OrdinaryActionEligibility.ExitPredicate(null,scope);
                RecruitmentHooks.Registered=true;var zero=new ZeroPointerUnit();OrdinaryActionEligibility.EnterCapture(zero,out var missing);
                Check(!OrdinaryActionEligibility.AllowsNativeReadiness(zero));OrdinaryActionEligibility.ExitPredicate(null,missing);
            });
            test("UI-033-health-widget-gets-detached-native-profile",()=> {
                var source=Register(new UnitState{moved=true});var data=source.UnitData;var display=source;
                MimicPresentationUi.HealthState(ref display);
                Check(!Native.Same(source,display)&&display.health==100&&display.UnitData.health==100&&display.type==UnitData.Type.warrior);
                Check(source.health==10&&source.UnitData==data&&data.health==10&&data.attack==0&&source.moved);
                Il2CppSystem.Object.AliasClone=true;display=source;MimicPresentationUi.HealthState(ref display);
                Check(ReferenceEquals(display,source)&&source.health==10&&data.health==10);
            });
            test("UI-034-ordinary-selection-never-toggles-inspection",()=> {
                var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                Select(interaction,unit);Select(interaction,unit);
                Check(Popup(unit).Header=="Warrior"&&interaction.Bar.descriptionString=="");
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                Select(interaction,unit);Check(Popup(unit).Header=="Mimic");
                var other=new Unit{UnitState=Register(new UnitState{id=4,owner=2})};Check(Popup(other).Header=="Warrior");
                Check(Popup(unit).Header=="Mimic","Unrelated popup erased the selected Mimic's private mode");
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                Check(Popup(unit).Header=="Warrior"&&unit.UnitState.health==10);
            });
            test("UI-035-toggle-rechecks-selection-authority-and-busy-state",()=> {
                foreach(string reason in new[]{"other-selected","owner","fog","hidden","wait","processing","spectating","replay"})
                {
                    ResetContext();var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                    if(reason=="other-selected")interaction.selectedUnit=new();
                    if(reason=="owner")GameManager.LocalPlayer=new(){Id=2};
                    if(reason=="fog")unit.Visible=false;
                    if(reason=="hidden")unit.Hidden=true;
                    if(reason=="wait")GameManager.Client.IsWaitingForCommand=true;
                    if(reason=="processing")GameManager.Client.ActionManager.IsProcessing=true;
                    if(reason=="spectating")GameManager.Client.IsSpectating=true;
                    if(reason=="replay")GameManager.Client.IsReplay=true;
                    Check(!MimicPresentationUi.ToggleInspection(interaction,unit),"Toggle allowed during "+reason);
                    Check(Popup(unit).Header=="Warrior");
                    Check(unit.UnitState.health==10&&!unit.UnitState.moved&&!unit.UnitState.attacked&&GameManager.LocalPlayer.Currency==15);
                }
            });
            test("UI-036-actual-info-has-one-real-stats-set-and-placeholder",()=> {
                var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                var actual=Popup(unit);
                Check(actual.Header=="Mimic"&&Stat(actual,"Health")=="1/1"&&Stat(actual,"Attack")=="0"&&Stat(actual,"Defense")=="0");
                Check(actual.PopulationCalls==1&&actual.statsRows.Count==6);
                Check(ReferenceEquals(actual.unitRenderer.duplicatedSpritesContainer.LastSprite,UnitArtAssets.Get("mimic_mask")));
                Check(unit.UnitState.health==10&&unit.UnitState.type==UnitData.Type.dcp_mimic_form_warrior);
            });
            test("UI-037-selection-context-reset-and-command-cancellation",()=> {
                foreach(string change in new[]{"clear","turn","viewer","world","command","session"})
                {
                    ResetContext();var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                    Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                    if(change=="clear")MimicPresentationUi.SelectionCleared();
                    if(change=="turn")GameManager.GameState.CurrentTurn++;
                    if(change=="viewer")GameManager.LocalPlayer=new(){Id=2};
                    if(change=="world")GameManager.GameState=new();
                    if(change=="command")MimicPresentationUi.CommandDispatch();
                    if(change=="session")GameManager.Client=new();
                    MimicPresentationUi.Tick();
                    if(change=="viewer")
                    {
                        Check(Popup(unit).Header=="Warrior"&&MimicPresentationUi.MapView(unit)==null);
                        GameManager.LocalPlayer=new();
                    }
                    if(change!="world")
                    {
                        Select(interaction,unit);
                        Check(Popup(unit).Header==(change=="session"?"Warrior":"Mimic"),"Wrong remembered mode after "+change);
                    }
                    Check(unit.UnitState.health==10&&unit.UnitState.UnitData.type==UnitData.Type.dcp_mimic_form_warrior);
                }
            });
            test("UI-118-map-turn-view-keeps-input-and-viewer-guards",()=> {
                var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                GameManager.GameState.CurrentPlayer=2;MimicPresentationUi.Tick();
                Check(MimicPresentationUi.MapView(unit)==true&&!MimicPresentationUi.CanInspect(interaction,unit));
                Check(!MimicPresentationUi.ToggleInspection(interaction,unit));
                GameManager.Client.IsReplay=true;Check(MimicPresentationUi.MapView(unit)==null);GameManager.Client.IsReplay=false;
                GameManager.Client.IsSpectating=true;Check(MimicPresentationUi.MapView(unit)==null);GameManager.Client.IsSpectating=false;
                GameManager.LocalPlayer=new(){Id=2};Check(MimicPresentationUi.MapView(unit)==null);
                GameManager.LocalPlayer=new();GameManager.GameState.CurrentPlayer=1;Select(interaction,unit);
                Check(MimicPresentationUi.MapView(unit)==true&&MimicPresentationUi.CanInspect(interaction,unit));
            });
            test("UI-067-per-unit-map-preference-survives-unrelated-selection",()=> {
                var a=Actor();var b=new Unit{UnitState=Register(new UnitState{id=4})};
                var interaction=new ClientInteraction();Select(interaction,a);
                Check(MimicPresentationUi.ToggleInspection(interaction,a));
                Select(interaction,b);Check(interaction.Bar.headerString=="Warrior (Disguise)");
                Check(MimicPresentationUi.MapView(a)==true&&MimicPresentationUi.MapView(b)==false);
                Check(MimicPresentationUi.ToggleInspection(interaction,b));
                MimicPresentationUi.CommandDispatch();
                Check(MimicPresentationUi.MapView(a)==true&&MimicPresentationUi.MapView(b)==true);
                Select(interaction,a);Check(interaction.Bar.headerString=="Mimic");
                Check(MimicPresentationUi.ToggleInspection(interaction,a));
                Check(MimicPresentationUi.MapView(a)==false&&MimicPresentationUi.MapView(b)==true);
                a.Visible=false;Check(MimicPresentationUi.MapView(a)==null);
                a.Visible=true;MimicPresentationUi.Clear();Select(interaction,b);
                Check(interaction.Bar.headerString=="Warrior (Disguise)"&&MimicPresentationUi.MapView(b)==false);
            });
            test("UI-074-action-state-replacement-retains-logical-unit-view",()=> {
                var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));var oldPopup=Popup(unit);oldPopup.Show();
                var client=GameManager.Client;GameManager.GameState=new();
                var replacement=Actor();replacement.UnitState.moved=true;
                MimicPresentationUi.Tick();
                Check(ReferenceEquals(client,GameManager.Client)&&!oldPopup.IsShowing());
                Check(MimicPresentationUi.MapView(unit)==null&&MimicPresentationUi.MapView(replacement)==true);
                Select(interaction,replacement);Check(interaction.Bar.headerString=="Mimic"&&Popup(replacement).Header=="Mimic");
                Check(replacement.UnitState.moved&&replacement.UnitState.health==10);
            });
            test("UI-038-popup-populates-once-with-source-owner",()=> {
                var unit=Actor();unit.UnitState.owner=2;
                var original=new PopupManager.UnitPopupData{unit=unit,unitData=unit.UnitState.UnitData};
                PopupManager.ProductionCallbacks=true;var popup=PopupManager.GetUnitPopup(original);
                var payload=PopupManager.LastPayload!;
                Check(!Native.Same(original,payload)&&payload.unit==null&&original.unit==unit&&original.unitData==unit.UnitState.UnitData);
                Check(payload.unitData!.type==UnitData.Type.warrior&&popup.UnitData.type==UnitData.Type.warrior);
                Check(popup.PopulationCalls==1&&popup.PlayerSets==1&&popup.OwnerAtPopulate==2&&popup.player.Id==2);
                Check(popup.statsRows.Count==6&&Stat(popup,"Health")=="10/10"&&Stat(popup,"Attack")=="2"&&Stat(popup,"Defense")=="2");
                Check(unit.UnitState.health==10&&unit.UnitState.UnitData.health==10);
            });
            test("UI-039-back-arrow-and-category-pool-restoration",()=> {
                var button=new UIRoundButton();MimicControls.ApplyCategory(button);Check(button.icon.gameObject.activeSelf&&ReferenceEquals(button.AssignedSprite,UnitArtAssets.Icon()));
                MimicControls.ApplyBack(button);Check(button.Text=="Back"&&button.ShowLabel&&button.icon.gameObject.activeSelf&&button.AssignedSprite?.Name=="UI_BACK_WHITE");
                var category=new UIUnitRenderer{unitType=UnitData.Type.dcp_mimic};MimicControls.RendererAssigned(button,category);
                Check(button.Text=="Mimic"&&category.gameObject.activeSelf);
                category.unitType=UnitData.Type.warrior;MimicControls.RendererAssigned(button,category);
                Check(category.gameObject.activeSelf&&button.icon.gameObject.activeSelf);
                var tech=new UIRoundButton_UI2();MimicControls.TechCategory(tech,new UnitData(UnitData.Type.dcp_mimic));
                Check(tech.Text=="Mimic"&&tech.IconContainer.gameObject.activeSelf&&tech.Sprite is null);
                MimicControls.TechCategory(tech,new UnitData(UnitData.Type.warrior));Check(tech.IconContainer.gameObject.activeSelf);
            });
            test("UI-040-hooks-preserve-native-clicks-and-real-state",()=> {
                var h=new HarmonyLib.Harmony();MimicPresentationUi.Install(h);MimicControls.Install(h);MimicInspectionControls.Install(h);MimicHoldInput.Install(h);
                Check(h.Patched.Contains("UnitStatusDisplay::SetState")&&h.Patched.Contains("PopupManager::GetUnitPopup")&&h.Patched.Contains("UnitPopup::set_UnitData"));
                Check(h.Patched.Contains("ClientInteraction::OnPress")&&h.Patched.Contains("ClientInteraction::Process"));
                Check(!h.Patched.Contains("ClientInteraction::TryDeselectUnit"));
                Check(!h.Patched.Contains("UnitState::get_health")&&!h.Patched.Contains("UnitState::set_health")&&!h.Patched.Contains("UnitDataExtensions::GetMaxHealth"));
                Check(!h.Patched.Contains("Unit::get_UnitState"));
                Check(h.Patched.Contains("GameManager::OnClientSessionOpened")&&h.Patched.Contains("GameManager::ReturnToMenu")&&h.Patched.Contains("GameManager::SetReplayClient"));
            });
            test("UI-041-open-private-popup-closes-on-access-loss",()=> {
                foreach(string change in new[]{"fog","hidden","command","spectator","turn","world","replacement","toggle","restore-failure"})
                {
                    ResetContext();var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                    Check(MimicPresentationUi.ToggleInspection(interaction,unit));var popup=Popup(unit);popup.Show();
                    Check(popup.Header=="Mimic"&&popup.IsShowing());
                    if(change=="restore-failure"){UIManager.Instance.SpriteDuplicator.ThrowRestore=true;MimicPresentationUi.SelectionCleared();}
                    if(change=="fog")unit.Visible=false;
                    if(change=="hidden")unit.Hidden=true;
                    if(change=="command")MimicPresentationUi.CommandDispatch();
                    if(change=="spectator")GameManager.Client.IsSpectating=true;
                    if(change=="turn")GameManager.GameState.CurrentTurn++;
                    if(change=="world")GameManager.GameState=new();
                    if(change=="replacement")Register(new UnitState());
                    if(change=="toggle"){popup.Hide();Check(MimicPresentationUi.ToggleInspection(interaction,unit));}
                    MimicPresentationUi.Tick();Check(!popup.IsShowing(),"Private popup remained open after "+change);
                    int shows=popup.ShowCalls;popup.Show();Check(!popup.IsShowing()&&popup.ShowCalls==shows,"Late Show revived stale private popup after "+change);
                    UIManager.Instance.SpriteDuplicator.ThrowRestore=false;
                    MimicPresentationUi.Clear();popup.Show();Check(!popup.IsShowing(),"Global cleanup removed stale Show guard");
                    popup.ResetPopup();popup.UnitData=new UnitData(UnitData.Type.knight);popup.Show();
                    Check(popup.IsShowing(),"Native popup recycling failed to release the stale guard");
                    Check(unit.UnitState.health==10&&unit.UnitState.UnitData.health==10);
                }
            });
            test("UI-042-explicit-open-follows-current-inspection-mode",()=> {
                PopupManager.ProductionCallbacks=true;var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                MimicPresentationUi.Tick();MimicPresentationUi.Tick();Check(PopupManager.FactoryCalls==0);
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                MimicPresentationUi.Tick();MimicPresentationUi.Tick();Check(PopupManager.FactoryCalls==0&&!PopupManager.PopupShowing);
                // This factory call represents the separate native information action.
                var popup=Popup(unit);popup.Show();
                Check(PopupManager.FactoryCalls==1&&popup.ShowCalls==1&&popup.Header=="Mimic");
                Check(popup.PopulationCalls==1&&Stat(popup,"Health")=="1/1");popup.Hide();
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                MimicPresentationUi.Tick();MimicPresentationUi.Tick();Check(PopupManager.FactoryCalls==1&&!PopupManager.PopupShowing);
                popup=Popup(unit);popup.Show();Check(popup.Header=="Warrior"&&Stat(popup,"Health")=="10/10");
                Check(unit.UnitState.health==10&&!unit.UnitState.moved&&!unit.UnitState.attacked&&GameManager.LocalPlayer.Currency==15);
            });
            test("UI-043-popup-race-never-shows-stale-private-data",()=> {
                foreach(string timing in new[]{"before","after"})
                {
                    ResetContext();var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                    Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                    if(timing=="before")PopupManager.BeforePopulate=()=>GameManager.LocalPlayer=new(){Id=2};
                    else PopupManager.AfterPopulate=()=>GameManager.LocalPlayer=new(){Id=2};
                    var popup=Popup(unit);popup.Show();
                    if(timing=="before")Check(popup.Header=="Warrior"&&popup.UnitData.type==UnitData.Type.warrior&&Stat(popup,"Health")=="10/10");
                    else Check(!popup.IsShowing()&&popup.ShowCalls==0);
                    Check(popup.PopulationCalls==1);popup.Hide();
                    PopupManager.BeforePopulate=null;PopupManager.AfterPopulate=null;Check(Popup(unit).Header=="Warrior");
                }
            });
            test("UI-044-bottom-left-bar-has-no-health-and-preserves-native-reuse",()=> {
                var unit=Actor();var interaction=new ClientInteraction();Select(interaction,unit);
                Check(interaction.Bar.headerString=="Warrior (Disguise)"&&interaction.Bar.descriptionString==""&&interaction.Bar.description.text=="");
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                Check(interaction.Bar.headerString.Contains("Mimic")&&interaction.Bar.descriptionString==""&&interaction.Bar.description.text=="");
                var portrait=interaction.Bar.duplicatedSprites;Check(ReferenceEquals(portrait.LastSprite,UnitArtAssets.Get("mimic_mask")));
                int beforeRefreshRestores=portrait.RestoreCalls;
                MimicPresentationUi.BeforeBarRefresh(interaction.Bar);
                Check(portrait.LastSprite==null&&portrait.RestoreCalls==beforeRefreshRestores+1,"Option-only refresh left the prior private portrait behind");
                // Native option refresh deliberately makes no portrait change.
                GameManager.LocalPlayer=new(){Id=2};MimicPresentationUi.BarReady(interaction.Bar);
                Check(portrait.LastSprite==null&&interaction.Bar.headerString=="Warrior","Enemy view leaked disguise label");MimicPresentationUi.Tick();
                GameManager.LocalPlayer=new();Select(interaction,unit);Check(interaction.Bar.headerString=="Mimic");
                interaction.Bar.unit=new();interaction.Bar.headerString="Native replacement";interaction.Bar.descriptionString="Native description";
                MimicPresentationUi.SelectionCleared();
                Check(interaction.Bar.headerString=="Native replacement"&&interaction.Bar.descriptionString=="Native description");
                foreach(string changed in new[]{"source","container","bar-container"})
                {
                    ResetContext();unit=Actor();interaction=new ClientInteraction();Select(interaction,unit);
                    Check(MimicPresentationUi.ToggleInspection(interaction,unit));portrait=interaction.Bar.duplicatedSprites;
                    int restores=portrait.RestoreCalls;
                    if(changed=="source")interaction.Bar.spriteDuplicationData.source=new();
                    if(changed=="container")interaction.Bar.spriteDuplicationData.container=new();
                    if(changed=="bar-container")interaction.Bar.duplicatedSprites=new();
                    MimicPresentationUi.SelectionCleared();Check(portrait.RestoreCalls==restores,"Stale portrait restored after "+changed+" changed");
                }
            });
            test("UI-045-popup-factory-exception-unwinds-projection-scope",()=> {
                var unit=Actor();PopupManager.ProductionCallbacks=true;PopupManager.BeforePopulate=()=>throw new InvalidOperationException("factory failure");
                bool failed=false;try{Popup(unit);}catch(InvalidOperationException){failed=true;}Check(failed);
                PopupManager.BeforePopulate=null;
                var native=new UnitData(UnitData.Type.knight);var ordinary=new UnitPopup{Player=new(){Id=9}};ordinary.UnitData=native;
                Check(ReferenceEquals(ordinary.UnitData,native)&&ordinary.OwnerAtPopulate==9&&ordinary.PopulationCalls==1);
                UnitPopup? nested=null;
                PopupManager.BeforePopulate=()=>{PopupManager.BeforePopulate=null;nested=PopupManager.GetUnitPopup(new(){unitData=new UnitData(UnitData.Type.knight)});};
                var projected=Popup(unit);Check(projected.PopulationCalls==1&&projected.statsRows.Count==6&&Stat(projected,"Health")=="10/10");
                Check(nested is not null&&nested.PopulationCalls==1&&nested.UnitData.type==UnitData.Type.knight);
                Il2CppSystem.Object.AliasClone=true;var failedClone=Popup(unit);failedClone.Show();
                Check(!failedClone.IsShowing()&&failedClone.ShowCalls==0,"Failed Mimic projection exposed the native true-health popup");
                Il2CppSystem.Object.AliasClone=false;
            });
            test("UI-046-inspection-control-owned-slot-layout-and-stale-click",()=> {
                var bar=new InteractionBar();var unit=Actor();bar.unit=unit;int clicks=0;
                Check(bar.IsShowing&&!bar.BottomBarActive&&bar.LayoutPasses==0);
                // Appending an active button alone does not update the native
                // parent's layout; Show must be forced when already showing.
                var probe=bar.CreateRoundBottomBarButton("Native probe",true);bar.Show(true,false);
                Check(!bar.BottomBarActive&&bar.LayoutPasses==0);bar.buttons.Remove(probe);
                MimicInspectionControls.Show(bar,unit,false,()=>clicks++);
                Check(bar.buttons.Count==1&&bar.quickActions.buttons.Count==0&&bar.LastForceToBottom);
                Check(bar.BottomBarActive&&bar.LayoutButtonCount==1&&bar.LayoutPasses==1&&bar.LastShowInstant&&bar.LastShowForce);
                var button=bar.buttons[0];Check(button.Text=="Inspect Mimic"&&button.Active&&button.AssignedSprite?.Name=="UI_INFO");
                button.OnClicked!.Click();Check(clicks==1);
                var firstCallback=button.OnClicked;int navigation=bar.NavigationCalls,sprites=button.SpriteCalls,layouts=bar.LayoutPasses;
                MimicInspectionControls.Show(bar,unit,false,()=>clicks++);MimicInspectionControls.Show(bar,unit,false,()=>clicks++);
                Check(ReferenceEquals(button.OnClicked,firstCallback)&&bar.NavigationCalls==navigation&&button.SpriteCalls==sprites&&bar.LayoutPasses==layouts);
                MimicInspectionControls.Show(bar,unit,true,()=>clicks++);
                Check(bar.buttons.Count==1&&ReferenceEquals(bar.buttons[0],button)&&button.Text=="Show disguise"&&!ReferenceEquals(button.OnClicked,firstCallback)&&bar.LayoutPasses==layouts);
                bar.unit=new();button.OnClicked!.Click();Check(clicks==1);
                MimicInspectionControls.Remove(bar);Check(!button.Active&&button.OnClicked==null&&!button.ButtonEnabled);
                Check(bar.buttons.Count==0&&button.gameObject.DestroyCalls==1&&!bar.BottomBarActive&&bar.LayoutButtonCount==0&&bar.LayoutPasses==layouts+1);
                navigation=bar.NavigationCalls;MimicInspectionControls.Remove(bar);Check(bar.NavigationCalls==navigation);
                bar.unit=unit;MimicInspectionControls.Show(bar,unit,false,()=>clicks++);Check(bar.buttons.Count==1&&!ReferenceEquals(bar.buttons[0],button));
                var replacement=bar.buttons[0];var stale=replacement.OnClicked!;MimicPresentationUi.SelectionCleared();stale.Click();firstCallback!.Click();
                Check(clicks==1&&!replacement.Active&&replacement.OnClicked==null&&replacement.gameObject.DestroyCalls==1&&bar.buttons.Count==0,
                    "Apparent-mode stale inspection button survived context reset");
            });
            test("UI-047-control-cleanup-preserves-native-replacement-callback",()=> {
                var bar=new InteractionBar();var unit=Actor();bar.unit=unit;int nativeClicks=0;
                MimicInspectionControls.Show(bar,unit,false,()=>{});var button=bar.buttons[0];
                int layouts=bar.LayoutPasses;
                button.SetText("Native action");button.OnClicked=new((_,_)=>nativeClicks++);
                MimicInspectionControls.Remove(bar);Check(button.Active&&button.OnClicked!=null&&button.gameObject.DestroyCalls==0&&bar.LayoutPasses==layouts);
                button.OnClicked!.Click();Check(nativeClicks==1);
                PopupManager.ProductionCallbacks=true;bar.ClearButtons();Check(bar.buttons.Count==0&&button.gameObject.DestroyCalls==1);
                MimicInspectionControls.Show(bar,unit,false,()=>{});Check(bar.buttons.Count==1&&bar.buttons[0].Text=="Inspect Mimic");
                button=bar.buttons[0];var stale=button.OnClicked!;layouts=bar.LayoutPasses;
                // The prefix only detaches callbacks. Native Clear still owns
                // traversal, list clearing and exactly one destruction.
                MimicInspectionControls.NativeClear(bar);
                Check(bar.buttons.Count==1&&!button.Active&&button.OnClicked==null&&button.gameObject.DestroyCalls==0&&bar.LayoutPasses==layouts);
                stale.Click();bar.ClearButtons();Check(bar.buttons.Count==0&&button.gameObject.DestroyCalls==1&&bar.LayoutPasses==layouts);
                MimicInspectionControls.Show(bar,unit,false,()=>{});layouts=bar.LayoutPasses;
                MimicInspectionControls.Remove(bar,reflow:false);Check(bar.buttons.Count==0&&bar.LayoutPasses==layouts);
            });
            test("UI-048-private-portrait-restores-native-source",()=> {
                var renderer=new UIUnitRenderer{unitType=UnitData.Type.warrior};var container=renderer.duplicatedSpritesContainer;
                MimicInspectionControls.ApplyActualPortrait(container,renderer.spriteDuplicationData,GameManager.LocalPlayer);
                Check(container.ClearCalls==1&&ReferenceEquals(container.LastSprite,UnitArtAssets.Get("mimic_mask"))&&renderer.unitType==UnitData.Type.warrior);
                MimicInspectionControls.RestorePortrait(renderer.spriteDuplicationData);
                Check(container.RestoreCalls==1&&container.LastSprite==null&&renderer.unitType==UnitData.Type.warrior);
            });
            test("UI-063-private-portrait-fits-only-new-child",()=> {
                var renderer=new UIUnitRenderer{unitType=UnitData.Type.warrior};var container=renderer.duplicatedSpritesContainer;
                var destination=container.rectTransform;destination.rect=new(200,100);
                var oldChild=new UnityEngine.RectTransform{rect=new(13,17)};destination.Append(oldChild);
                renderer.spriteDuplicationData.offset=new(99,-45);renderer.spriteDuplicationData.scale=0.002f;
                var source=renderer.spriteDuplicationData.source;var factory=UIManager.Instance.SpriteDuplicator;
                var layers=UnitArtAssets.Layers(GameManager.LocalPlayer);
                MimicInspectionControls.ApplyActualPortrait(container,renderer.spriteDuplicationData,GameManager.LocalPlayer);
                Check(destination.childCount==layers.Count);
                for(int i=0;i<layers.Count;i++)
                {
                    var child=(UnityEngine.RectTransform)destination.GetChild(i);var image=child.gameObject.FindComponent<UnityEngine.UI.Image>()!;
                    Check(child.SizeWrites==2&&Math.Abs(child.rect.width-layers[i].Width*0.08f)<0.001f&&Math.Abs(child.rect.height-layers[i].Height*0.08f)<0.001f);
                    Check(child.localScale==UnityEngine.Vector3.one&&ReferenceEquals(child.parent,destination)&&ReferenceEquals(image.sprite,layers[i].Sprite)&&image.color==(UnityEngine.Color)layers[i].Color);
                    Check(child.anchoredPosition==new UnityEngine.Vector2(layers[i].X*0.08f,layers[i].Y*0.08f));
                }
                Check(destination.rect==new UnityEngine.Rect(200,100)&&destination.SizeWrites==0&&oldChild.rect==new UnityEngine.Rect(13,17)&&oldChild.SizeWrites==0&&oldChild.gameObject.DestroyCalls==1);
                Check(factory.LastScale==1f&&renderer.spriteDuplicationData.scale==0.002f&&renderer.spriteDuplicationData.offset==new UnityEngine.Vector2(99,-45)&&ReferenceEquals(source,renderer.spriteDuplicationData.source)&&renderer.unitType==UnitData.Type.warrior);
                foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity})
                {
                    int clears=container.ClearCalls;var visible=container.LastSprite;
                    destination.rect=new(invalid,100);Reject(()=>MimicInspectionControls.ApplyActualPortrait(container,renderer.spriteDuplicationData,GameManager.LocalPlayer));
                    Check(container.ClearCalls==clears&&ReferenceEquals(visible,container.LastSprite));
                }
                destination.rect=new(100,100);factory.ExtraFactoryChild=true;
                try{Reject(()=>MimicInspectionControls.ApplyActualPortrait(container,renderer.spriteDuplicationData,GameManager.LocalPlayer));}
                finally{factory.ExtraFactoryChild=false;}
                Check(destination.childCount==1&&container.RestoreCalls==1&&container.LastSprite==null&&destination.SizeWrites==0,
                    "Partial popup art was not replaced by a fresh native source copy");
            });
        }
    }
}
