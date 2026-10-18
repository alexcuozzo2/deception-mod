using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using UnitType=Polytopia.Data.UnitData.Type;
namespace Deception.Tests
{
 public static partial class Presentation0361Tests
 {
  static UITextButton[] DefinitionTags(UnitPopup popup)=>Native.Items(popup.abilityButtons).Cast<UITextButton>().Where(b=>b.gameObject.activeSelf).ToArray();
  static string[] DefinitionKeys(UnitPopup popup)=>DefinitionTags(popup).Select(b=>b.Key).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
  static UnitPopup DefinitionPopup(bool amphibious)
  {PopupManager.ProductionCallbacks=true;return PopupManager.GetUnitPopup(new(){unitData=new(amphibious?UnitType.dcp_mermimic:UnitType.dcp_mimic)});}
  static void MimicDefinitionTests()
  {
   foreach(bool amphibious in new[]{false,true})Case("A44 actual popup uses canonical species abilities independent of carrier="+amphibious,()=>
   {
    var ability=amphibious?Polytopia.Data.UnitAbility.Type.land:Polytopia.Data.UnitAbility.Type.fortify;
    var anchor=new Polytopia.Data.UnitData(amphibious?UnitType.dcp_mermimic:UnitType.dcp_mimic);anchor.unitAbilities.Add(ability);anchor.unitAbilities.Add(Polytopia.Data.UnitAbility.Type.dash);var anchorList=anchor.unitAbilities;
    string anchorId=amphibious?AquarionCatalog.MimicId:RecruitmentCatalog.MenuId;RecruitmentHooks.DataOverride=(_,id)=>id==anchorId?anchor:null;
    try
    {
     PopupManager.ProductionCallbacks=true;var definition=PopupManager.GetUnitPopup(new(){unitData=anchor});var expected=DefinitionKeys(definition);
     var a=Actor();var real=a.Unit.UnitState.UnitData;real.movement=4;real.unitAbilities.Add(Polytopia.Data.UnitAbility.Type.escape);var carrierList=real.unitAbilities;
     var row=Row(a.Unit,App()) with{AmphibiousMimic=amphibious};MimicPresentationState.ReadIdentity=(_,_)=>row;var interaction=Select(a.Unit);Toggle(interaction,a.Unit);
     var popup=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",a.Unit)!;
     Check(DefinitionKeys(popup).SequenceEqual(expected)&&expected.Contains(ability.ToString()),"complete native and custom ability button set differs between anchor and actual carrier popup");
     Check(!DefinitionKeys(popup).Contains("escape")&&DefinitionKeys(popup).Contains("dash"),"actual popup must retain Mimic Dash without borrowing operative escape");
     Check(!ReferenceEquals(popup.data,real)&&!ReferenceEquals(popup.data.unitAbilities,carrierList)&&!ReferenceEquals(popup.data.unitAbilities,anchorList),"actual popup must own a detached canonical ability list");
     Check(ReferenceEquals(real.unitAbilities,carrierList)&&Native.Items(carrierList).Cast<Polytopia.Data.UnitAbility.Type>().SequenceEqual(new[]{Polytopia.Data.UnitAbility.Type.escape})&&ReferenceEquals(anchor.unitAbilities,anchorList)&&Native.Items(anchorList).Cast<Polytopia.Data.UnitAbility.Type>().SequenceEqual(new[]{ability,Polytopia.Data.UnitAbility.Type.dash}),"canonical display changed native carrier or registry list");
     Check(popup.data.movement==4&&Native.Items(popup.statsRows).Cast<UnitStatsRow>().Single(r=>r.statsNameLabel.text=="Movement").statsValueLabel.text=="4"&&popup.PopulationCalls==1&&popup.statsRows.Count==6,"canonical abilities replaced operative movement stats or repopulated rows");
    }
    finally{RecruitmentHooks.DataOverride=null;}
   });
   foreach(bool amphibious in new[]{false,true})foreach(bool camouflage in new[]{false,true})Case("A44 technology and actual Mimic ability parity amphibious="+amphibious+" camouflage="+camouflage,()=>
   {
    GameManager.LocalPlayer.Propaganda=camouflage;var popup=DefinitionPopup(amphibious);var data=popup.data;var tags=DefinitionTags(popup);var keys=DefinitionKeys(popup);
    Check(tags.Length==(camouflage?3:2)&&keys.Contains(MimicAbilityTags.MimicryNameKey)&&keys.Contains(MimicAbilityTags.NameKey),"static definition lacks shared Mimicry/Regroup help");
    Check(keys.Contains(MimicAbilityTags.DisguiseNameKey)==camouflage&&!keys.Any(k=>k.Contains("dash",StringComparison.OrdinalIgnoreCase)),"custom help must gate Disguise without inventing a Dash label when the fixture definition has no Dash");
    Check(popup.PopulationCalls==1&&popup.statsRows.Count==6&&popup.unit is null&&ReferenceEquals(popup.data,data),"technology tags must retain the single native definition population");
    popup.Show();tags.Single(t=>t.Key==MimicAbilityTags.MimicryNameKey).OnClicked!.Click();var help=PopupManager.LastBasicPopup;
    Check(help is not null&&help.Header=="Localized["+MimicAbilityTags.MimicryNameKey+"]"&&help.Description=="Localized["+MimicAbilityTags.MimicryDescriptionKey+"]"&&help.DefaultOkCalls==1,"Mimicry must open the actual shared localized help, not an inert label");
    help!.Hide();popup.Hide();var a=Actor();var body=a.Unit.UnitState;var row=Row(a.Unit,App()) with{AmphibiousMimic=amphibious};MimicPresentationState.ReadIdentity=(_,_)=>row;
    var interaction=Select(a.Unit);Toggle(interaction,a.Unit);var map=(UnitPopup)Call(typeof(PresentationAdapterTests),"Popup",a.Unit)!;
    Check(DefinitionKeys(map).SequenceEqual(keys),"on-map actual Mimic and static species definition must expose the same ability help");
    Check(body.health==10&&!body.moved&&!body.attacked&&ReferenceEquals(MimicPresentationState.ReadIdentity!(GameManager.GameState,body.id),row),"definition/help cannot alter real action budget, HP or private frame");
   });
   foreach(bool amphibious in new[]{false,true})Case("A44 direct recruitment setter carries Mimic definition amphibious="+amphibious,()=>
   {
    PopupManager.ProductionCallbacks=true;var popup=new UnitPopup{Player=GameManager.LocalPlayer};var data=new Polytopia.Data.UnitData(amphibious?UnitType.dcp_mermimic:UnitType.dcp_mimic);popup.UnitData=data;
    Check(DefinitionKeys(popup).SequenceEqual(new[]{MimicAbilityTags.MimicryNameKey,MimicAbilityTags.NameKey}.OrderBy(x=>x,StringComparer.Ordinal)),"direct native setter bypasses factory but still needs the same public abilities");
    Check(popup.PopulationCalls==1&&popup.statsRows.Count==6&&ReferenceEquals(popup.data,data),"static setter must not repopulate native rows or replace its definition");
   });
   Case("A44 static Disguise help uses captured preview player research",()=>
   {
    PopupManager.ProductionCallbacks=true;GameManager.LocalPlayer.Propaganda=false;var owner=new PlayerState{Id=2,Propaganda=true};var popup=new UnitPopup{Player=owner};popup.UnitData=new(UnitType.dcp_mimic);popup.Show();
    var tag=DefinitionTags(popup).Single(t=>t.Key==MimicAbilityTags.DisguiseNameKey);owner.Propaganda=false;GameManager.LocalPlayer.Propaganda=true;PopupManager.LastBasicPopup=null;tag.OnClicked!.Click();
    Check(PopupManager.LastBasicPopup is null,"unrelated viewer research cannot authorize a revoked preview player's Disguise help");
    owner.Propaganda=true;tag.OnClicked!.Click();Check(PopupManager.LastBasicPopup?.Description=="Localized["+MimicAbilityTags.DisguiseDescriptionKey+"]","current preview-player research reauthorizes its description");
   });
   foreach(string changed in new[]{"definition","player","viewer","client"})Case("A44 stale static Mimic help refuses changed "+changed,()=>
   {
    var popup=DefinitionPopup(false);popup.Show();var click=DefinitionTags(popup).Single(t=>t.Key==MimicAbilityTags.MimicryNameKey).OnClicked!;
    if(changed=="definition")popup.data=new(UnitType.warrior);if(changed=="player")popup.player=new(){Id=2};if(changed=="viewer")GameManager.LocalPlayer=new(){Id=2};if(changed=="client")GameManager.Client=new();
    PopupManager.LastBasicPopup=null;click.Click();Check(PopupManager.LastBasicPopup is null,"stale definition callback borrowed a new popup/session context");
    Check(popup.PopulationCalls==1&&popup.statsRows.Count==6,"stale callback must not rebuild native statistics");
   });
   Case("A44 static tag refresh and pool reuse preserve native callbacks",()=>
   {
    var popup=DefinitionPopup(false);popup.Show();var original=DefinitionTags(popup);var stale=original.Select(t=>t.OnClicked!).ToArray();
    for(int i=0;i<8;i++)NativeSpyPresentation.PopupPopulated(popup);
    Check(DefinitionTags(popup).Length==2&&popup.abilityButtons.Count==2&&popup.PopulationCalls==1,"repeat definition refresh accumulates buttons or stats");
    PopupManager.LastBasicPopup=null;foreach(var click in stale)click.Click();Check(PopupManager.LastBasicPopup is null,"replaced callbacks remain usable");
    popup.Hide();popup.ResetPopup();popup.UnitData=new(UnitType.warrior);Check(DefinitionTags(popup).Length==0,"native Warrior reuse retains Mimic help");
    var native=popup.GetAbilityButton(Polytopia.Data.UnitAbility.Type.fortify);native.Key="Fortify";native.OnClicked!.Click();Check(popup.NativeAbilityClicks==1&&original.Contains(native),"retired custom tag damaged native pool callback");
   });
   foreach(UnitType kind in new[]{UnitType.warrior,UnitType.dcp_mimic_form_warrior,UnitType.dcp_spy})Case("A44 definition selection never reveals hidden Mimic for "+kind,()=>
   {
    PopupManager.ProductionCallbacks=true;var popup=PopupManager.GetUnitPopup(new(){unitData=new(kind)});var keys=DefinitionKeys(popup);
    Check(!keys.Contains(MimicAbilityTags.MimicryNameKey)&&!keys.Contains(MimicAbilityTags.NameKey),"non-menu/carrier definition received a private Mimic identity tag");
    Check(kind==UnitType.dcp_spy?keys.SequenceEqual(new[]{MimicAbilityTags.SubvertNameKey}.OrderBy(x=>x,StringComparer.Ordinal)):keys.Length==0,"ordinary/Spy displayed abilities changed");
   });
  }
 }
}
