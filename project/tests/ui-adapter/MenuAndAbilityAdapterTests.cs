using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using Polytopia.Data;
using UnityEngine;

namespace Deception.Tests
{
    // Real presentation/control callbacks, synthetic native layout/pool objects.
    // This does not execute Unity or establish native menu/playtest success.
    internal static class MenuAndAbilityAdapterTests
    {
        static void Check(bool condition,string message="Menu/tag fixture assertion failed")
        {if(!condition)throw new InvalidOperationException(message);}
        static (Unit Unit,ClientInteraction Interaction) Setup(bool harvest=false)
        {
            var state=GameManager.GameState;var us=new UnitState{coordinates=new(){X=3,Y=2},home=new(){X=1,Y=1}};
            var unit=new Unit{UnitState=us};state.Units[us.id]=us;state.Players[1]=GameManager.LocalPlayer;
            var data=new TileData{coordinates=us.coordinates,unit=us,owner=1};
            if(harvest)data.resource=new(){type=ResourceData.Type.fruit};
            state.Map.Add(data);state.Map.Add(new(){coordinates=us.home,owner=1,Improvement=ImprovementData.Type.city,Population=4});
            var tile=new Tile{Coordinates=us.coordinates,Data=data,Unit=unit,Resource=harvest?new():null};
            MapRenderer.Current=new();MapRenderer.Current.Tiles[(3,2)]=tile;MapRenderer.Current.Units[us.id]=unit;
            MimicSmuggleActions.Clear();MimicSmuggleActions.Configure(SmuggleCatalog.Harvests.Select(x=>new SmuggleHarvest(x.Id,x.NativeHarvest,x.Resource)));
            var interaction=new ClientInteraction();interaction.Bar.tile=tile;interaction.SelectUnit(unit);
            MimicPresentationUi.Selected(interaction,unit);MimicPresentationUi.BarReady(interaction.Bar);
            return(unit,interaction);
        }
        static UIRoundButton[] Buttons(InteractionBar bar)=>Native.Items(bar.buttons).Cast<UIRoundButton>().ToArray();
        static UnitPopup Popup(Unit unit)
        {PopupManager.ProductionCallbacks=true;return PopupManager.GetUnitPopup(new(){unit=unit,unitData=unit.UnitState.UnitData});}
        static UITextButton[] Tags(UnitPopup popup)=>Native.Items(popup.abilityButtons).Cast<UITextButton>().Where(x=>x.gameObject.activeSelf).ToArray();
        internal static void Run(Action<string,Action> run)
        {
            void Test(string id,Action work)=>run(id,()=>
            {
                MimicRegroupUi.Choosing=false;MimicRegroupUi.Button=null;NativeApparentUpgradeUi.Reset();PopupManager.Current=null;PopupManager.LastBasicPopup=null;
                try{work();}finally{MimicRegroupUi.Choosing=false;MimicRegroupUi.Button=null;NativeApparentUpgradeUi.Reset();}
            });
            Test("UI-131-inspection-slot-survives-toggle-native-refresh-and-late-actions",()=>
            {
                var(unit,interaction)=Setup(true);var bar=interaction.Bar;
                var inspect=MimicInspectionControls.OwnedButton(bar);var smuggle=MimicSmuggleUi.OwnedButton(bar);
                Check(inspect is not null&&smuggle is not null);
                var ordinary=bar.CreateRoundBottomBarButton("Harvest fruit",true);
                var regroup=bar.CreateRoundBottomBarButton("Regroup",true);MimicRegroupUi.Button=regroup;
                int regroupClicks=0;regroup.OnClicked=new((_,_)=>regroupClicks++);
                for(int i=0;i<12;i++)
                {
                    MimicPresentationUi.BarReady(bar);
                    Check(ReferenceEquals(bar.buttons[0],inspect)&&ReferenceEquals(bar.buttons[1],ordinary)&&
                        ReferenceEquals(bar.buttons[2],smuggle)&&ReferenceEquals(bar.buttons[3],regroup));
                    ((UIRoundButton)inspect!).OnClicked!.Click();
                    Check(ReferenceEquals(MimicInspectionControls.OwnedButton(bar),inspect));
                    MimicPresentationUi.BeforeBarRefresh(bar);MimicPresentationUi.BarReady(bar);
                    Check(ReferenceEquals(bar.buttons[0],inspect));
                }
                Check(regroupClicks==0&&bar.ImprovementCalls==0&&!unit.UnitState.moved&&!unit.UnitState.attacked&&unit.UnitState.health==10);
            });
            Test("UI-132-menu-permutations-match-navigation-and-preserve-decoration-slots",()=>
            {
                var(_,interaction)=Setup(true);var bar=interaction.Bar;
                var inspect=(UIRoundButton)MimicInspectionControls.OwnedButton(bar)!;
                var smuggle=(UIRoundButton)MimicSmuggleUi.OwnedButton(bar)!;
                var ordinary=bar.CreateRoundBottomBarButton("Native action",true);
                var regroup=bar.CreateRoundBottomBarButton("Regroup",true);MimicRegroupUi.Button=regroup;
                var expected=new[]{inspect,ordinary,smuggle,regroup};
                var decoration=new Transform();decoration.SetParent(bar.bottomBarButtonContainer,false);
                var callbacks=expected.Select(x=>x.OnClicked).ToArray();
                foreach(var a in expected)foreach(var b in expected.Where(x=>x!=a))foreach(var c in expected.Where(x=>x!=a&&x!=b))
                {
                    var d=expected.Single(x=>x!=a&&x!=b&&x!=c);var shuffled=new[]{a,b,c,d};
                    for(int i=0;i<4;i++){bar.buttons[i]=shuffled[i];shuffled[i].transform.SetSiblingIndex(i);}
                    decoration.SetSiblingIndex(2);int decorationIndex=decoration.GetSiblingIndex();
                    MimicActionMenu.Normalize(bar);
                    Check(Buttons(bar).SequenceEqual(expected)&&decoration.GetSiblingIndex()==decorationIndex);
                    var visual=Enumerable.Range(0,bar.bottomBarButtonContainer.childCount).Select(i=>bar.bottomBarButtonContainer.GetChild(i)).Where(x=>x!=decoration).ToArray();
                    Check(visual.SequenceEqual(expected.Select(x=>x.transform)));
                    Check(expected.Select(x=>x.OnClicked).SequenceEqual(callbacks));
                    int nav=bar.NavigationCalls;MimicActionMenu.Normalize(bar);Check(bar.NavigationCalls==nav);
                }
            });
            Test("UI-133-regroup-target-mode-keeps-disabled-inspection-leading-slot",()=>
            {
                var(unit,interaction)=Setup(true);var bar=interaction.Bar;
                var inspect=(UIRoundButton)MimicInspectionControls.OwnedButton(bar)!;
                var regroup=bar.CreateRoundBottomBarButton("Cancel",true);MimicRegroupUi.Button=regroup;
                MimicRegroupUi.Choosing=true;MimicPresentationUi.BarReady(bar);
                Check(ReferenceEquals(bar.buttons[0],inspect)&&!inspect.ButtonEnabled&&ReferenceEquals(bar.buttons[1],regroup));
                inspect.OnClicked!.Click();Check(bar.headerString=="Warrior (Disguise)"&&!unit.UnitState.moved&&!unit.UnitState.attacked);
                MimicRegroupUi.Choosing=false;MimicPresentationUi.BarReady(bar);
                Check(ReferenceEquals(bar.buttons[0],inspect)&&inspect.ButtonEnabled&&MimicSmuggleUi.OwnedButton(bar) is not null);
                inspect.OnClicked!.Click();Check(bar.headerString=="Mimic");
            });
            Test("UI-134-menu-sort-ignores-unowned-same-label-and-preserves-parent",()=>
            {
                var(unit,interaction)=Setup();var bar=interaction.Bar;
                var inspect=(UIRoundButton)MimicInspectionControls.OwnedButton(bar)!;
                var impostor=bar.CreateRoundBottomBarButton("Inspect Mimic",true);
                var foreignParent=new Transform();impostor.transform.SetParent(foreignParent,false);
                bar.buttons[0]=impostor;bar.buttons[1]=inspect;
                MimicActionMenu.Normalize(bar);
                Check(ReferenceEquals(bar.buttons[0],inspect)&&ReferenceEquals(bar.buttons[1],impostor)&&ReferenceEquals(impostor.transform.parent,foreignParent));
                var replacement=new UIButtonBase.ButtonAction((_,_)=>{});inspect.OnClicked=replacement;
                var order=Buttons(bar);MimicActionMenu.Normalize(bar);Check(Buttons(bar).SequenceEqual(order)&&ReferenceEquals(inspect.OnClicked,replacement));
            });
            Test("UI-164-upgrade-order-keeps-inspection-fixed-and-native-lookalikes-native",()=>
            {
                var(_,interaction)=Setup(true);var bar=interaction.Bar;
                var inspect=(UIRoundButton)MimicInspectionControls.OwnedButton(bar)!;
                var smuggle=(UIRoundButton)MimicSmuggleUi.OwnedButton(bar)!;
                var ordinary=bar.CreateRoundBottomBarButton("Feign Scout",true);
                var regroup=bar.CreateRoundBottomBarButton("Regroup",true);MimicRegroupUi.Button=regroup;
                var upgrades=new[]{"Scout","Rammer","Bomber"}.Select(name=>bar.CreateRoundBottomBarButton("Feign "+name,true)).ToArray();
                NativeApparentUpgradeUi.FixtureBar=bar;NativeApparentUpgradeUi.FixtureButtons=upgrades.Cast<object>().ToArray();
                var expected=new[]{inspect,ordinary,smuggle,regroup}.Concat(upgrades).ToArray();
                int clicks=0;foreach(var button in upgrades.Append(ordinary).Append(regroup))button.OnClicked=new((_,_)=>clicks++);
                var callbacks=expected.Select(x=>x.OnClicked).ToArray();
                var decoration=new Transform();decoration.SetParent(bar.bottomBarButtonContainer,false);decoration.SetSiblingIndex(2);
                for(int shift=0;shift<expected.Length;shift++)
                {
                    var permutation=expected.Skip(shift).Concat(expected.Take(shift)).Reverse().ToArray();
                    for(int i=0;i<permutation.Length;i++){bar.buttons[i]=permutation[i];permutation[i].transform.SetSiblingIndex(i);}
                    decoration.SetSiblingIndex(2);MimicActionMenu.Normalize(bar);
                    Check(Buttons(bar).SequenceEqual(expected),"Upgrade button ranking moved inspection or trusted a native lookalike caption");
                    Check(decoration.GetSiblingIndex()==2&&Enumerable.Range(0,bar.bottomBarButtonContainer.childCount).Select(i=>bar.bottomBarButtonContainer.GetChild(i)).Where(x=>x!=decoration).SequenceEqual(expected.Select(x=>x.transform)),"Visual order differs from navigation or moved decoration");
                    int navigation=bar.NavigationCalls;MimicActionMenu.Normalize(bar);
                    Check(bar.NavigationCalls==navigation&&expected.Select(x=>x.OnClicked).SequenceEqual(callbacks)&&clicks==0,"Stable sorting rebuilt navigation or invoked/replaced a callback");
                }
            });
            Test("UI-135-regroup-tag-is-native-style-actual-only-and-one-population",()=>
            {
                var(unit,interaction)=Setup();var original=unit.UnitState.UnitData;
                Check(Tags(Popup(unit)).Length==0);Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                PopupManager.AfterPopulate=()=>
                {
                    var p=PopupManager.LastPopup!;
                    p.GetAbilityButton(UnitAbility.Type.dash).Key="Dash";
                    p.GetAbilityButton(UnitAbility.Type.fortify).Key="Fortify";
                };
                var popup=Popup(unit);var tags=Tags(popup);
                Check(tags.Select(x=>x.Key).SequenceEqual(new[]{"Dash","Fortify",MimicAbilityTags.NameKey,MimicAbilityTags.MimicryNameKey}));
                Check(tags.All(x=>ReferenceEquals(x.transform.parent,popup.abilityButtonContainer))&&popup.PopulationCalls==1&&popup.statsRows.Count==6);
                Check(ReferenceEquals(unit.UnitState.UnitData,original)&&unit.UnitState.health==10&&!unit.UnitState.moved&&!unit.UnitState.attacked);
            });
            Test("UI-136-regroup-tag-opens-concise-localized-native-help",()=>
            {
                var(unit,interaction)=Setup();Check(MimicPresentationUi.ToggleInspection(interaction,unit));var popup=Popup(unit);popup.Show();
                var tag=Tags(popup).Single(x=>x.Key==MimicAbilityTags.NameKey);tag.OnClicked!.Click();var help=PopupManager.LastBasicPopup;
                Check(help is not null&&help.Header=="Localized["+MimicAbilityTags.NameKey+"]"&&help.Description=="Localized["+MimicAbilityTags.DescriptionKey+"]"&&help.DefaultOkCalls==1&&help.ShowCalls==1);
                Check(popup.PopulationCalls==1&&!unit.UnitState.moved&&!unit.UnitState.attacked);
            });
            Test("UI-137-regroup-tag-restores-native-callback-before-pool-reuse",()=>
            {
                var(unit,interaction)=Setup();Check(MimicPresentationUi.ToggleInspection(interaction,unit));var popup=Popup(unit);popup.Show();
                var tag=Tags(popup).Single(x=>x.Key==MimicAbilityTags.NameKey);var stale=tag.OnClicked!;popup.Hide();Check(!tag.gameObject.activeSelf);
                var reused=popup.GetAbilityButton(UnitAbility.Type.dash);Check(ReferenceEquals(reused,tag)&&!ReferenceEquals(reused.OnClicked,stale));
                reused.Key="Dash";reused.OnClicked!.Click();Check(popup.NativeAbilityClicks==1);
                stale.Click();Check(PopupManager.LastBasicPopup is null&&popup.NativeAbilityClicks==1);
                popup.ResetPopup();Check(!reused.gameObject.activeSelf);
            });
            Test("UI-138-regroup-tag-stale-view-and-replaced-owner-are-inert",()=>
            {
                var(unit,interaction)=Setup();Check(MimicPresentationUi.ToggleInspection(interaction,unit));var popup=Popup(unit);popup.Show();
                var tag=Tags(popup).Single(x=>x.Key==MimicAbilityTags.NameKey);var stale=tag.OnClicked!;int replacements=0;
                var replacement=new UIButtonBase.ButtonAction((_,_)=>replacements++);tag.OnClicked=replacement;
                MimicAbilityTags.Remove(popup);Check(ReferenceEquals(tag.OnClicked,replacement)&&tag.gameObject.activeSelf);
                stale.Click();Check(PopupManager.LastBasicPopup is null);replacement.Click();Check(replacements==1);
                popup.Hide();Check(MimicPresentationUi.ToggleInspection(interaction,unit));stale.Click();Check(PopupManager.LastBasicPopup is null);
            });
            Test("UI-151-disguise-tag-requires-camouflage-and-actual-mimic-view",()=>
            {
                var(unit,interaction)=Setup();GameManager.LocalPlayer.Propaganda=true;
                Check(Tags(Popup(unit)).Length==0,"Apparent stats exposed private abilities");
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));var popup=Popup(unit);popup.Show();
                var tags=Tags(popup);Check(tags.Select(x=>x.Key).SequenceEqual(new[]{MimicAbilityTags.NameKey,MimicAbilityTags.MimicryNameKey,MimicAbilityTags.DisguiseNameKey}));
                Check(popup.PopulationCalls==1&&unit.UnitState.health==10&&!unit.UnitState.moved&&!unit.UnitState.attacked);
                var disguise=tags.Single(x=>x.Key==MimicAbilityTags.DisguiseNameKey);
                GameManager.LocalPlayer.Propaganda=false;disguise.OnClicked!.Click();Check(PopupManager.LastBasicPopup is null,"Revoked research authorized stale help callback");
                GameManager.LocalPlayer.Propaganda=true;disguise.OnClicked!.Click();
                var help=PopupManager.LastBasicPopup;Check(help is not null&&help.Header=="Localized["+MimicAbilityTags.DisguiseNameKey+"]"&&
                    help.Description=="Localized["+MimicAbilityTags.DisguiseDescriptionKey+"]"&&help.DefaultOkCalls==1);
            });
            Test("UI-152-both-exchange-tags-restore-native-callbacks-before-reuse",()=>
            {
                var(unit,interaction)=Setup();GameManager.LocalPlayer.Propaganda=true;
                Check(MimicPresentationUi.ToggleInspection(interaction,unit));var popup=Popup(unit);popup.Show();
                var tags=Tags(popup);var stale=tags.Select(x=>x.OnClicked!).ToArray();popup.Hide();Check(tags.All(x=>!x.gameObject.activeSelf));
                var first=popup.GetAbilityButton(UnitAbility.Type.dash);first.Key="Dash";
                var second=popup.GetAbilityButton(UnitAbility.Type.fortify);second.Key="Fortify";
                Check(tags.Contains(first)&&tags.Contains(second)&&!ReferenceEquals(first,second));
                first.OnClicked!.Click();second.OnClicked!.Click();Check(popup.NativeAbilityClicks==2);
                foreach(var callback in stale)callback.Click();Check(PopupManager.LastBasicPopup is null&&popup.NativeAbilityClicks==2);
            });
            Test("UI-165-spy-tags-use-native-single-population-and-no-action-grant",()=>
            {
                var h=new HarmonyLib.Harmony();NativeSpyPresentation.Install(h);Check(h.Patched.Contains("UnitPopup::set_UnitData"));
                foreach(var species in new[]{UnitData.Type.dcp_spy,UnitData.Type.dcp_spymaid})
                {
                var(unit,_)=Setup();unit.UnitState.type=species;unit.UnitState.UnitData=new(species);unit.UnitState.health=50;
                var popup=Popup(unit);Check(Tags(popup).Select(x=>x.Key).SequenceEqual(new[]{MimicAbilityTags.SubvertNameKey}));
                Check(popup.PopulationCalls==1&&popup.statsRows.Count==6&&!unit.UnitState.moved&&!unit.UnitState.attacked&&unit.UnitState.health==50);
                }
            });
            Test("UI-166-copied-spy-apparent-popup-has-public-spy-help",()=>
            {
                var(unit,_)=Setup();var a=new LedgerAppearance("dcp_spy",new("Imperius","Default"),new("Imperius","Default"),new("Imperius","Default"),1,100,100,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
                MimicPresentationState.ReadIdentity=(_,_)=>new LedgerUnit(1,unit.UnitState.id,true,LedgerActualKind.Genuine,true,a,PaidInvestmentKind.Known,2);
                unit.UnitState.type=UnitData.Type.warrior;unit.UnitState.UnitData=new(UnitData.Type.warrior);unit.UnitState.health=100;
                var popup=Popup(unit);Check(popup.UnitData.type==UnitData.Type.dcp_spy&&Tags(popup).Length==1,"Spy appearance lacks matching public ability help");
                Check(unit.UnitState.type==UnitData.Type.warrior&&unit.UnitState.UnitData.type==UnitData.Type.warrior&&!unit.UnitState.attacked,"public help must not create actual Spy powers");
            });
            Test("UI-167-spy-under-warrior-disguise-does-not-leak-tags",()=>
            {
                var(unit,_)=Setup();unit.UnitState.type=UnitData.Type.dcp_spy;unit.UnitState.UnitData=new(UnitData.Type.dcp_spy);unit.UnitState.health=50;
                var a=new LedgerAppearance("warrior",new("Imperius","Default"),new("Imperius","Default"),new("Imperius","Default"),1,100,100,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
                MimicPresentationState.ReadIdentity=(_,_)=>new LedgerUnit(1,unit.UnitState.id,true,LedgerActualKind.Genuine,true,a,PaidInvestmentKind.Known,8);
                var popup=Popup(unit);Check(popup.UnitData.type==UnitData.Type.warrior&&Tags(popup).Length==0,"ordinary-looking costume leaked true specialist tags");
            });
            Test("UI-168-spy-help-is-localized-without-relay",()=>
            {
                var(unit,_)=Setup();unit.UnitState.type=UnitData.Type.dcp_spy;unit.UnitState.UnitData=new(UnitData.Type.dcp_spy);var popup=Popup(unit);popup.Show();
                foreach(var tag in Tags(popup)){PopupManager.Current=popup;tag.OnClicked!.Click();var help=PopupManager.LastBasicPopup;Check(help is not null&&help.Header=="Localized["+tag.Key+"]"&&help.Description=="Localized["+tag.Key.Replace("names","descriptions")+"]"&&help.DefaultOkCalls==1);}
                Check(!unit.UnitState.moved&&!unit.UnitState.attacked&&popup.PopulationCalls==1,"help invoked a real command");
            });
            Test("UI-169-spy-tag-refresh-is-bounded-and-retires-old-callbacks",()=>
            {
                var(unit,_)=Setup();unit.UnitState.type=UnitData.Type.dcp_spy;unit.UnitState.UnitData=new(UnitData.Type.dcp_spy);var popup=Popup(unit);popup.Show();
                var stale=Tags(popup).Select(x=>x.OnClicked!).ToArray();for(int i=0;i<8;i++)NativeSpyPresentation.PopupPopulated(popup);
                Check(popup.abilityButtons.Count==1&&Tags(popup).Length==1&&popup.PopulationCalls==1,"repeated population postfix appended tags or stats");
                foreach(var callback in stale)callback.Click();Check(PopupManager.LastBasicPopup is null,"retired callback remained authorized");
            });
            Test("UI-170-spy-tags-return-original-native-pool-handlers",()=>
            {
                var(unit,_)=Setup();unit.UnitState.type=UnitData.Type.dcp_spy;unit.UnitState.UnitData=new(UnitData.Type.dcp_spy);var popup=Popup(unit);popup.Show();var before=Tags(popup);popup.Hide();
                Check(before.All(t=>!t.gameObject.activeSelf));var native=popup.GetAbilityButton(UnitAbility.Type.dash);native.Key="Dash";native.OnClicked!.Click();Check(popup.NativeAbilityClicks==1&&before.Contains(native));
            });
            Test("UI-171-spy-tags-fail-closed-for-rebound-source-and-foreign-callback",()=>
            {
                var(unit,_)=Setup();unit.UnitState.type=UnitData.Type.dcp_spy;unit.UnitState.UnitData=new(UnitData.Type.dcp_spy);var popup=Popup(unit);popup.Show();var tag=Tags(popup)[0];var old=tag.OnClicked!;
                popup.data=new(UnitData.Type.warrior);old.Click();Check(PopupManager.LastBasicPopup is null,"rebound data retained Spy help authority");
                int clicks=0;var replacement=new UIButtonBase.ButtonAction((_,_)=>clicks++);tag.OnClicked=replacement;NativeSpyPresentation.PopupPopulated(popup);Check(ReferenceEquals(tag.OnClicked,replacement)&&tag.gameObject.activeSelf,"cleanup overwrote a reused native widget");replacement.Click();Check(clicks==1);
            });
        }
    }
}
