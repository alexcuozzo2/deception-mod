// Execute actual production recruitment, presentation and ordinary-action adapters.
// The native objects are synthetic. Passing is NOT evidence of Unity UI success.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using Polytopia.Data;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace Deception.Tests
{
    public static class UiAdapterTests
    {
        static readonly List<object> Results=new(); static int Failed;
        static void Check(bool value,string message="Assertion failed"){if(!value)throw new Exception(message);}
        static InteractionBar Reset()
        {
            RecruitmentWheelUi.Clear();MimicPresentationState.Clear();MimicPresentationUi.Clear();MimicControls.Clear();MimicInspectionControls.Clear();MimicHoldInput.Clear();OrdinaryActionEligibility.Clear();Entry.Notes.Clear();
            GameManager.GameState=new();GameManager.LocalPlayer=new();GameManager.Client=new();PopupManager.PopupShowing=false;Il2CppSystem.Object.AliasClone=false;
            PopupManager.ProductionCallbacks=false;PopupManager.FactoryCalls=0;PopupManager.LastPopup=null;PopupManager.LastPayload=null;PopupManager.BeforePopulate=null;PopupManager.AfterPopulate=null;
            PolytopiaInput.CurrentTouches=new();PolytopiaInput.MouseHeld=false;PolytopiaInput.mousePosition=new(0,0);PolytopiaInput.OutsideWindow=false;
            PolytopiaInput.UIHitTest=null;
            CameraController.IsDragging=false;CameraController.IsBlocked=false;UIManager.Instance=new();
            MimicPresentationState.ReadAppearance=null;MimicPresentationState.ReadIdentity=null;UIUtils.PreviewOverride=null;UIUtils.Previews.Clear();BranchTechFixture.Reset();GameManager.Atlas=new();SpriteHandle.Reset();ImageConversion.Fail=false;ImageConversion.WrongSize=false;
            RecruitmentHooks.Registered=true;RecruitmentHooks.Valid=true;RecruitmentHooks.Eligible=true;
            RecruitmentHooks.Roster=new(StringComparer.Ordinal){"warrior"};RecruitmentHooks.Forms.Clear();
            foreach(string id in new[]{"warrior","rider"})RecruitmentHooks.Forms.Add(RecruitmentCatalog.Variant(id),new(id,RecruitmentCatalog.Variant(id),RecruitmentCatalog.Label(id),10,id=="rider"?2:1,2,2,false));
            return new();
        }
        static void T(string id,Action test)
        {
            try{Reset();test();Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}
            catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e);}
            finally{RecruitmentWheelUi.Clear();Il2CppSystem.Object.AliasClone=false;}
        }
        static void Queue(InteractionBar bar)
        {
            var command=RecruitmentHooks.NewCommand(GameManager.LocalPlayer,bar.coordinates,RecruitmentCatalog.MenuId);
            Check(!RecruitmentWheelUi.InterceptSpawn(bar,command,RecruitmentHooks.Data(GameManager.GameState,RecruitmentCatalog.MenuId)));
        }
        static void Tick(int count=2){for(int i=0;i<count;i++)RecruitmentWheelUi.Tick();}
        static UIRoundButton Button(InteractionBar b,string text)=>Native.Items(b.quickActions.buttons).Cast<UIRoundButton>().Single(x=>x.Text==text);
        static TechData Tech()
        {
            var t=new TechData{type=TechData.Type.dcp_deception,idx=1000,cost=1};
            // Deliberately NOT category-first: projection must select the category by ID.
            foreach(var id in new[]{UnitData.Type.dcp_mimic_form_rider,UnitData.Type.dcp_mimic,UnitData.Type.dcp_mimic_form_warrior})t.unitUnlocks.Add(new(id));
            return t;
        }
        static void BranchTechTests()
        {
            BranchArtAdapterTests.Run(T);
            var kinds=new[]{TechData.Type.dcp_propaganda,TechData.Type.dcp_camouflage,TechData.Type.dcp_intelligence,TechData.Type.dcp_espionage};
            T("UI-094-behavior-research-has-fresh-owned-native-icon",()=>{
                var h=new HarmonyLib.Harmony();NativeBranchTechUi.Install(h);
                Check(h.Patched.Contains("TechItem::GetUnlockItems")&&h.Patched.Contains("TechPopupContent::CreateTechDataContent"));
                foreach(var kind in kinds)
                {
                    var data=new TechData{type=kind,cost=2,idx=1035};var units=data.unitUnlocks;var children=data.techUnlocks;
                    var first=new Il2CppReferenceArray<RectTransform>(0);var second=new Il2CppReferenceArray<RectTransform>(0);
                    NativeBranchTechUi.UnlockItems(data,ref first);NativeBranchTechUi.UnlockItems(data,ref second);
                    Check(first.Length==NativeBranchTechUi.AbilityKeys(kind.ToString()).Length&&second.Length==first.Length&&!Native.Same(first[0],second[0]));
                    foreach(var rect in new[]{first[0],second[0]})
                    {var image=rect.gameObject.FindComponent<UnityEngine.UI.Image>()!;Check(image&&image.sprite&&image.preserveAspect&&!image.raycastTarget&&rect.parent is null);Check(image.NativeSizeCalls==1&&Math.Abs(rect.rect.width-image.sprite!.rect.width*100f/image.sprite.pixelsPerUnit)<.001f&&Math.Abs(rect.rect.height-image.sprite.rect.height*100f/image.sprite.pixelsPerUnit)<.001f);}
                    UnityEngine.Object.Destroy(first[0].gameObject);Check(!first[0]&&second[0]);
                    Check(ReferenceEquals(data.unitUnlocks,units)&&units.Count==0&&ReferenceEquals(data.techUnlocks,children)&&data.cost==2&&data.idx==1035&&data.type==kind);
                }
                foreach(var kind in new[]{TechData.Type.dcp_espionage,TechData.Type.dcp_camouflage})
                {
                    var data=new TechData{type=kind};data.unitUnlocks.Add(new(kind==TechData.Type.dcp_espionage?UnitData.Type.dcp_spy:UnitData.Type.dcp_motherfogger));
                    var nativeRect=new RectTransform();var result=new Il2CppReferenceArray<RectTransform>(new[]{nativeRect});NativeBranchTechUi.UnlockItems(data,ref result);
                    Check(result.Length==2&&ReferenceEquals(result[0],nativeRect)&&data.unitUnlocks.Count==1,"Unit reward must remain before additional behavior");
                    var firstOnly=new Il2CppReferenceArray<RectTransform>(new[]{nativeRect});var same=firstOnly;NativeBranchTechUi.UnlockItems(data,ref firstOnly,null,true);Check(ReferenceEquals(firstOnly,same)&&firstOnly.Length==1,"Research animation must preserve native first unit");
                }
            });
            T("UI-095-behavior-icon-preserves-native-and-unrelated-results",()=>{
                var existing=new RectTransform();var nonempty=new Il2CppReferenceArray<RectTransform>(new[]{existing});
                NativeBranchTechUi.UnlockItems(new(){type=TechData.Type.dcp_propaganda},ref nonempty);
                Check(nonempty.Length==3&&ReferenceEquals(nonempty[0],existing));
                foreach(var data in new TechData?[]{null,new(){type=TechData.Type.riding},Tech()})
                {var empty=new Il2CppReferenceArray<RectTransform>(0);var same=empty;NativeBranchTechUi.UnlockItems(data!,ref empty);Check(ReferenceEquals(empty,same));UIBasicComponent popup=new();var originalPopup=popup;Check(NativeBranchTechUi.PopupContent(data!,ref popup)&&ReferenceEquals(popup,originalPopup));}
                RecruitmentHooks.Registered=false;int images=BranchTechFixture.Images.Count,buttons=BranchTechFixture.Buttons.Count,keys=BranchTechFixture.LocalizedKeys.Count;
                var disabled=new Il2CppReferenceArray<RectTransform>(0);NativeBranchTechUi.UnlockItems(new(){type=TechData.Type.dcp_intelligence},ref disabled);
                UIBasicComponent untouched=new();Check(NativeBranchTechUi.PopupContent(new(){type=TechData.Type.dcp_intelligence},ref untouched));
                Check(disabled.Length==0&&BranchTechFixture.Images.Count==images&&BranchTechFixture.Buttons.Count==buttons&&BranchTechFixture.LocalizedKeys.Count==keys);
            });
            T("UI-096-three-behavior-popups-leave-spy-unlock-native",()=>{
                foreach(var kind in kinds)
                {
                    var data=new TechData{type=kind};bool nativeUnit=kind is TechData.Type.dcp_espionage or TechData.Type.dcp_camouflage;
                    if(nativeUnit)data.unitUnlocks.Add(new(kind==TechData.Type.dcp_espionage?UnitData.Type.dcp_spymaid:UnitData.Type.dcp_motherfogger));
                    var units=data.unitUnlocks;var children=data.techUnlocks;var originals=Native.Items(units).Cast<UnitData>().ToArray();
                    UIBasicComponent result=null!;Check(!NativeBranchTechUi.PopupContent(data,ref result));Check(result.transform.parent is null&&result);
                    var buttons=result.gameObject.ComponentsInChildren<UIRoundButton_UI2>().ToArray();var keys=NativeBranchTechUi.AbilityKeys(kind.ToString());
                    Check(buttons.Length==2,"Each branch popup should have its two rewards");
                    if(nativeUnit)Check(ReferenceEquals(buttons[0].NativeUnitData,units[0]),"Native unit popup callback/data was replaced");
                    for(int i=0;i<keys.Length;i++)
                    {
                        var button=buttons[i+(nativeUnit?1:0)];var key=keys[i];string title=key=="dcp_disguise"?"ui.labels.dcp_disguise_unlock":"ability.names."+key;
                        Check(button.Text=="Localized["+title+"]"&&button.InfoHeader==button.Text&&button.InfoDescription=="Localized[ability.descriptions."+key+"]");
                        Check(button.Sprite&&button.SpriteScale==1f,"Reward icon missing");
                    }
                    Check(ReferenceEquals(data.unitUnlocks,units)&&Native.Items(units).Cast<UnitData>().SequenceEqual(originals)&&ReferenceEquals(data.techUnlocks,children));
                    UnityEngine.Object.Destroy(result.gameObject);Check(buttons.All(x=>!x),"Popup children leaked");
                }
            });
            T("UI-097-behavior-icon-failure-retires-unpublished-image",()=>{
                foreach(string failure in new[]{"image-native-size","image-preserve"})
                {
                    BranchTechFixture.Failure=failure;var result=new Il2CppReferenceArray<RectTransform>(0);var original=result;
                    int count=BranchTechFixture.Images.Count;
                    bool failed=false;try{NativeBranchTechUi.UnlockItems(new(){type=TechData.Type.dcp_propaganda},ref result);}catch{failed=true;}
                    Check(failed&&ReferenceEquals(result,original)&&result.Length==0&&BranchTechFixture.Images.Count==count+1);
                    Check(BranchTechFixture.Images.Last().gameObject.DestroyCalls==1&&Entry.Notes.Any(x=>x.Kind=="branch-tech-icon-error"));
                }
            });
            T("UI-098-behavior-popup-failure-retires-only-unpublished-widget",()=>{
                foreach(string failure in new[]{"button-sprite","localization","info-popup"})
                {
                    BranchTechFixture.Failure=failure;UIBasicComponent result=new();var original=result;
                    bool failed=false;try{NativeBranchTechUi.PopupContent(new(){type=TechData.Type.dcp_propaganda},ref result);}catch{failed=true;}
                    Check(failed&&ReferenceEquals(result,original)&&original&&original.gameObject.DestroyCalls==0);
                    Check(BranchTechFixture.Buttons.Last().gameObject.DestroyCalls==1);
                }
                Check(Entry.Notes.Count(x=>x.Kind=="branch-tech-popup-error")==3);
            });
        }
        public static int Run()
        {
            var patches=new HarmonyLib.Harmony();RecruitmentWheelUi.Install(patches);
            T("UI-001-original-target-failure-reproduced",()=>{
                var b=new InteractionBar();object? target=Native.Get(b.quickActions,"Target");b.quickActions.Hide(true);b.ClearButtons();
                bool rejected=false;try{Native.Set(b.quickActions,"Target",target);}catch(System.Reflection.TargetInvocationException e){rejected=e.InnerException is NullReferenceException;}
                Check(rejected,"Original null-target sequence did not reproduce its modeled failure.");
            });
            T("UI-002-actual-draw-opens-with-null-target",()=>{var b=new InteractionBar();Queue(b);Tick();Check(b.quickActions.IsShowing&&b.quickActions.buttons.Count==2&&b.quickActions.TargetSetCalls==0);Check(!Entry.Notes.Any(n=>n.Kind=="mimic-wheel-error"));});
            T("UI-003-position-captured-before-native-clear",()=>{var b=new InteractionBar();var position=b.quickActions.Position;Queue(b);b.quickActions.Hide(true);b.ClearButtons();Tick();Check(b.quickActions.Position==position&&b.quickActions.KeepWorldPosition);});
            T("UI-004-nonnull-follow-target-not-required",()=>{var b=new InteractionBar();b.quickActions.Target=new();b.quickActions.KeepWorldPosition=false;Queue(b);Tick();Check(b.quickActions.IsShowing&&b.quickActions.TargetSetCalls==1&&b.quickActions.KeepWorldPosition);});
            T("UI-005-open-is-deferred-and-does-not-spend",()=>{var b=new InteractionBar();Queue(b);Tick(1);Check(b.quickActions.ShowCalls==0);Tick(1);Check(b.quickActions.ShowCalls==1&&GameManager.LocalPlayer.Currency==15&&b.ConfirmationCalls==0);});
            T("UI-006-repeated-open-does-not-duplicate-wheel",()=>{var b=new InteractionBar();Queue(b);Queue(b);Tick(4);Check(b.quickActions.ShowCalls==1&&b.quickActions.buttons.Count==2);});
            T("UI-007-unlocked-only-plus-back-and-labels",()=>{var b=new InteractionBar();Queue(b);Tick();var w=Button(b,"Mimic\nWarrior");Check(w.Cost==1&&w.ShowLabel&&!w.LabelShowOnHover&&w.Icon is not null);Check(Button(b,"Back").Cost<0);});
            T("UI-008-riding-adds-only-rider",()=>{var b=new InteractionBar();RecruitmentHooks.Roster.Add("rider");Queue(b);Tick();Check(b.quickActions.buttons.Count==3&&Button(b,"Mimic\nRider").Icon is not null);});
            T("UI-009-back-restores-normal-wheel-without-purchase",()=>{
                foreach(bool nativeShowsWheel in new[]{false,true})
                {
                    var b=new InteractionBar{CityShowOpensWheel=nativeShowsWheel};var coordinates=b.coordinates;
                    // Reproduce the prior Show-only return against the native
                    // display/population split: it leaves no options and a stale title.
                    b.headerString="Mimic appearances";b.descriptionString="Choose a disguise";b.quickActions.Hide(true);b.ClearButtons();b.Show(false,true);
                    Check(b.quickActions.buttons.Count==0&&b.header.text=="Mimic appearances"&&!b.quickActions.IsShowing);
                    Queue(b);Tick();var back=Button(b,"Back");var staleBack=back.OnClicked!;
                    // Restoration must ask native for the current city contents,
                    // including roster changes since the submenu was opened.
                    b.NativeCityRoster=new[]{UnitData.Type.warrior,UnitData.Type.rider};b.NativeCityHeader="Updated city";
                    staleBack.Click();Tick(1);Check(b.RefreshCalls==0);b.quickActions.Hide(true);Tick(1);
                    Check(b.RefreshCalls==1&&b.quickActions.IsShowing&&b.quickActions.buttons.Count==2);
                    Check(b.headerString=="Updated city"&&b.header.text=="Updated city"&&b.descriptionString==b.NativeCityDescription&&b.description.text==b.NativeCityDescription);
                    Check(Button(b,"Ordinary Warrior").Cost==2&&Button(b,"Ordinary Rider").Cost==new UnitData(UnitData.Type.rider).cost);
                    Check(ReferenceEquals(coordinates,b.coordinates)&&b.CloseCalls==0&&b.ConfirmationCalls==0&&GameManager.LocalPlayer.Currency==15);
                    staleBack.Click();Tick();Check(b.RefreshCalls==1&&b.quickActions.buttons.Count==2);
                    Button(b,"Ordinary Warrior").OnClicked!.Click();Check(b.ConfirmationCalls==1&&b.LastConfirmed=="warrior");
                }
            });
            T("UI-010-native-close-cancels-without-purchase",()=>{var b=new InteractionBar();Queue(b);Tick();Check(!RecruitmentWheelUi.CloseRequested(b));Tick();Check(!b.quickActions.IsShowing&&b.ConfirmationCalls==0&&GameManager.LocalPlayer.Currency==15);});
            T("UI-011-city-change-cancels-stale-wheel",()=>{var b=new InteractionBar();Queue(b);b.coordinates=new(){X=6,Y=5};Tick();Check(b.quickActions.ShowCalls==0&&b.ConfirmationCalls==0);});
            T("UI-012-turn-change-cancels-stale-wheel",()=>{var b=new InteractionBar();Queue(b);GameManager.GameState.CurrentTurn++;Tick();Check(b.quickActions.ShowCalls==0&&b.ConfirmationCalls==0);});
            T("UI-013-select-defers-one-native-confirmation",()=>{var b=new InteractionBar();GameManager.LocalPlayer.Currency=1;Queue(b);Tick();var cb=Button(b,"Mimic\nWarrior").OnClicked!;cb.Click();cb.Click();Check(b.ConfirmationCalls==0);Tick();Check(b.ConfirmationCalls==1&&b.LastConfirmed==RecruitmentCatalog.Variant("warrior")&&GameManager.LocalPlayer.Currency==1);});
            T("UI-014-selection-survives-old-click-close",()=>{var b=new InteractionBar();Queue(b);Tick();RecruitmentWheelUi.CloseRequested(b);Button(b,"Mimic\nWarrior").OnClicked!.Click();Tick();Check(b.ConfirmationCalls==1);});
            T("UI-015-affordability-rechecked-before-confirm",()=>{var b=new InteractionBar();Queue(b);Tick();Button(b,"Mimic\nWarrior").OnClicked!.Click();GameManager.LocalPlayer.Currency=0;Tick();Check(b.ConfirmationCalls==0&&GameManager.LocalPlayer.Currency==0);Check(b.RefreshCalls==1&&b.quickActions.IsShowing&&Button(b,"Ordinary Warrior").Cost==2&&b.header.text==b.NativeCityHeader);});
            T("UI-016-roster-rechecked-before-confirm",()=>{var b=new InteractionBar();Queue(b);Tick();Button(b,"Mimic\nWarrior").OnClicked!.Click();RecruitmentHooks.Roster.Clear();Tick();Check(b.ConfirmationCalls==0);Check(b.RefreshCalls==1&&b.quickActions.IsShowing&&b.header.text==b.NativeCityHeader);});
            T("UI-017-modern-popup-hook-installed",()=>{Check(patches.Patched.Contains("TechPopupContent::CreateTechDataContent"));Check(patches.Patched.Contains("TechPopup::SetTechData"));Check(patches.Patched.Contains("TechItem::GetUnlockItems"));});
            T("UI-018-modern-popup-exactly-one-category",()=>{var t=Tech();TechPresentation.PopupContent(ref t);TechPopupContent.CreateTechDataContent(t);Check(t.unitUnlocks.Count==1&&t.unitUnlocks[0].type==UnitData.Type.dcp_mimic);});
            T("UI-019-live-native-unlocks-and-cost-unchanged",()=>{var live=Tech();var list=live.unitUnlocks;var branches=live.techUnlocks;var display=live;TechPresentation.PopupContent(ref display);Check(!Native.Same(live,display)&&!Native.Same(list,display.unitUnlocks));Check(live.unitUnlocks==list&&list.Count==3&&display.idx==live.idx&&display.cost==live.cost&&display.type==live.type&&ReferenceEquals(display.techUnlocks,branches));});
            T("UI-020-legacy-popup-also-compacted",()=>{var t=Tech();var live=t;TechPresentation.LegacyPopup(ref t);Check(t.unitUnlocks.Count==1&&live.unitUnlocks.Count==3);});
            T("UI-021-node-icon-compacted-not-full-registry",()=>{var t=Tech();bool first=false;TechPresentation.UnlockIcons(ref t,ref first);Check(first&&t.unitUnlocks.Count==1);});
            T("UI-022-other-tech-is-unmodified",()=>{var t=Tech();t.type=TechData.Type.riding;var original=t;bool first=false;TechPresentation.PopupContent(ref t);TechPresentation.LegacyPopup(ref t);TechPresentation.UnlockIcons(ref t,ref first);Check(ReferenceEquals(t,original)&&t.unitUnlocks.Count==3&&!first);});
            T("UI-023-clone-alias-rejected-without-mutation",()=>{var t=Tech();var original=t;Il2CppSystem.Object.AliasClone=true;TechPresentation.PopupContent(ref t);Check(ReferenceEquals(t,original)&&t.unitUnlocks.Count==3&&Entry.Notes.Any(n=>n.Kind=="mimic-tech-preview-error"));});
            T("UI-024-preview-does-not-alter-own-roster",()=>{var b=new InteractionBar();var t=Tech();TechPresentation.PopupContent(ref t);Queue(b);Tick();Check(b.quickActions.buttons.Count==2&&RecruitmentHooks.Roster.SetEquals(new[]{"warrior"}));});
            PresentationAdapterTests.Run(T);UnitArtAdapterTests.Run(T);OutlineTests.Run(T);TechArtAdapterTests.Run(T);HoldInputAdapterTests.Run(T);MapAdapterTests.Run(T);DualPortraitAdapterTests.Run(T);SmuggleAdapterTests.Run(T);
            BranchTechTests();
            string path=Environment.GetEnvironmentVariable("DECEPTION_ADAPTER_TEST_OUTPUT")??"ui-adapter-results.json";
            File.WriteAllText(path,JsonSerializer.Serialize(new{suite="M1E production UI/action adapters against synthetic native objects",scope="Managed adapter execution; NO Unity or native game execution",is64Bit=Environment.Is64BitProcess,runtime=Environment.Version.ToString(),total=Results.Count,passed=Results.Count-Failed,failed=Failed,tests=Results},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine("UI ADAPTER FIXTURE: "+(Results.Count-Failed)+"/"+Results.Count+" passed. Synthetic native surface, NOT in-game acceptance.");
            return Failed==0?0:1;
        }
    }
}
