using System;
using System.Collections.Generic;
using System.Linq;

namespace Deception.PolyModAdapter
{
    // Information tags only. No ability is inserted into real UnitData,
    // and native stats population still runs exactly once.
    internal static class MimicAbilityTags
    {
        const string Ability="Polytopia.Data.UnitAbility+Type";
        // PolyMod decodes archive underscores to the game's dotted term names.
        internal const string MimicryNameKey="ability.names.dcp_mimicry";
        internal const string MimicryDescriptionKey="ability.descriptions.dcp_mimicry";
        internal const string NameKey="ability.names.dcp_regroup";
        internal const string DescriptionKey="ability.descriptions.dcp_regroup";
        internal const string DisguiseNameKey="ability.names.dcp_disguise";
        internal const string DisguiseDescriptionKey="ability.descriptions.dcp_disguise";
        internal const string SubvertNameKey="ability.names.dcp_subvert";
        internal const string SubvertDescriptionKey="ability.descriptions.dcp_subvert";
        internal const string BlackoutNameKey="ability.names.dcp_blackout";
        internal const string BlackoutDescriptionKey="ability.descriptions.dcp_blackout";
        sealed class Tag
        {
            internal object Popup=null!,Button=null!,Buttons=null!,Callback=null!;
            internal object? OriginalCallback;
            internal Func<bool> Authorized=null!;
            internal string Name="",Description="";
        }
        static readonly Dictionary<(long Popup,string Name),Tag> Tags=new();
        internal static void Install()
        {
            Native.RequireReturn(Native.M("UnitPopup","GetAbilityButton",Ability),Native.T("UITextButton"));
            Native.RequireReturn(Native.M("PlayerExtensions","HasTech","PlayerState","Polytopia.Data.TechData+Type"),typeof(bool));
            _=Native.M("UnitPopup","get_abilityButtons");_=Native.M("UnitPopup","get_abilityButtonContainer");
            _=Native.M("UnitPopup","getUnitSkin");_=Native.M("UnitPopup","getUnitTribe");
            _=Native.M("UITextButton","set_Key","System.String");_=Native.M("UITextButton","get_Key");
            _=Native.M("UITextButton","set_Skin","PolytopiaBackendBase.Common.SkinType");
            _=Native.M("UITextButton","set_Tribe","PolytopiaBackendBase.Common.TribeType");
            _=Native.M("BasicPopup","SetDefaultOkbutton","Il2CppSystem.Action");
            _=Native.M("Localization","Get","System.String",Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1").MakeGenericType(Native.T("Il2CppSystem.Object")).FullName!);
        }
        static string Localized(string key)=>(string)Native.Invoke(Native.M("Localization","Get","System.String",
            Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1").MakeGenericType(Native.T("Il2CppSystem.Object")).FullName!),null,key,null)!;
        static bool Owned(Tag tag)=>Native.UnityAlive(tag.Popup)&&Native.UnityAlive(tag.Button)&&
            Native.Same(Native.Get(tag.Popup,"abilityButtons"),tag.Buttons)&&Native.Items(tag.Buttons).Any(x=>Native.Same(x,tag.Button))&&
            Native.Same(Native.Get(tag.Button,"OnClicked"),tag.Callback)&&(string?)Native.Get(tag.Button,"Key")==tag.Name;
        internal static void Show(object popup,Func<bool> authorized)=>Show(popup,authorized,Native.Static("GameManager","LocalPlayer"));
        internal static void Show(object popup,Func<bool> authorized,object? definitionPlayer)
        {
            Remove(popup);
            if(!authorized())return;
            Add(popup,NameKey,DescriptionKey,authorized);
            Add(popup,MimicryNameKey,MimicryDescriptionKey,authorized);
            if(OwnsCamouflage(definitionPlayer))Add(popup,DisguiseNameKey,DisguiseDescriptionKey,()=>authorized()&&OwnsCamouflage(definitionPlayer));
        }
        internal static void ShowSpy(object popup,Func<bool> authorized)
        {
            Remove(popup);
            if(!authorized())return;
            Add(popup,SubvertNameKey,SubvertDescriptionKey,authorized);

        }
        internal static void ShowFogger(object popup,Func<bool> authorized)
        {
            Remove(popup);
            if(!authorized())return;
            Add(popup,BlackoutNameKey,BlackoutDescriptionKey,authorized);
            // Native Double Attack remains the single two-shot tag; Blackout
            // help explains that these shots produce fog instead of damage.
        }
        static bool OwnsCamouflage(object? player)
        {
            return player is not null&&RecruitmentHooks.Registered&&(bool)Native.Invoke(
                Native.M("PlayerExtensions","HasTech","PlayerState","Polytopia.Data.TechData+Type"),null,player,
                Native.EnumValue("Polytopia.Data.TechData+Type",Deception.Core.TechTree.PropagandaId))!;
        }
        static void Add(object popup,string name,string description,Func<bool> authorized)
        {
            if(!authorized())return;
            // Dash is a regular (non-movement-category) ability. The native
            // factory uses it only to select the pooled text-button prefab;
            // it neither adds Dash nor changes the popup's source data.
            object button=Native.Invoke(Native.M("UnitPopup","GetAbilityButton",Ability),popup,Native.EnumValue(Ability,"dash"))!;
            object buttons=Native.Get(popup,"abilityButtons")!;
            if(!Native.UnityAlive(button)||!Native.Items(buttons).Any(x=>Native.Same(x,button)))
                throw new InvalidOperationException("Native ability tag was not owned by its popup.");
            var tag=new Tag{Popup=popup,Button=button,Buttons=buttons,OriginalCallback=Native.Get(button,"OnClicked"),Authorized=authorized,Name=name,Description=description};
            tag.Callback=Native.ButtonCallback(()=>
            {
                try
                {
                    if(!Tags.TryGetValue((Native.Pointer(popup),name),out var current)||!ReferenceEquals(current,tag)||!Owned(tag)||!tag.Authorized())return;
                    var manager=Native.Static("PopupManager","instance");
                    if(manager is null||!Native.Same(Native.Invoke(Native.M("PopupManager","GetCurrentPopup"),manager),popup))return;
                    object help=Native.Invoke(Native.M("PopupManager","GetBasicPopup"),null)!;
                    Native.Invoke(Native.M("BasicPopup","SetHeader","System.String"),help,Localized(name));
                    Native.Invoke(Native.M("BasicPopup","SetDescription","System.String"),help,Localized(description));
                    Native.Invoke(Native.M("BasicPopup","SetDefaultOkbutton","Il2CppSystem.Action"),help,new object?[]{null});
                    Native.Invoke(Native.M("PopupBase","Show"),help);
                }
                catch(Exception e){Entry.Note("mimic-regroup-tag-click-error",e.ToString());}
            });
            Native.Set(button,"Skin",Native.Invoke(Native.M("UnitPopup","getUnitSkin"),popup));
            Native.Set(button,"Tribe",Native.Invoke(Native.M("UnitPopup","getUnitTribe"),popup));
            Native.Set(button,"Key",name);Tags[(Native.Pointer(popup),name)]=tag;
            Native.Set(button,"OnClicked",tag.Callback);Native.Set(button,"ButtonEnabled",true);
            MimicControls.SetVisible(Native.Get(popup,"abilityButtonContainer")!,true);
        }
        internal static void Remove(object popup)
        {
            foreach(var key in Tags.Keys.Where(x=>x.Popup==Native.Pointer(popup)).ToArray())Remove(key);
        }
        static void Remove((long Popup,string Name) key)
        {
            if(!Tags.Remove(key,out var tag)||!Owned(tag))return;
            // GetAbilityButton reuses inactive widgets without rebuilding their
            // callback. Restore its native callback before returning to that pool.
            Native.Set(tag.Button,"OnClicked",tag.OriginalCallback);
            MimicControls.SetVisible(tag.Button,false);
        }
        internal static void Clear()
        {
            foreach(var key in Tags.Keys.ToArray())Remove(key);
            Tags.Clear();
        }
    }
}
