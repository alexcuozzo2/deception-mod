using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Polytopia.Data;
using UnityEngine;

namespace Deception.PolyModAdapter
{
    // Native unit rewards retain their native renderer and info-popup callback.
    // Behavior rewards are additional owned UI, never authoritative unlock data.
    internal static class NativeBranchTechUi
    {
        [ThreadStatic] static int NativeContent;
        sealed record LabelLayout(object Item,object Localizer,object Text,bool Wrap,bool TextWrap,bool AutoSize,float Font,float Min,float Max);
        static readonly List<LabelLayout> Labels=new();
        public static void Install(Harmony h)
        {
            NativeButtonArt.Install();
            Native.RequireReturn(Native.M("UIUtils","GetImage"),Native.T("UnityEngine.UI.Image"));
            Native.RequireReturn(Native.M("UnityEngine.UI.Image","SetNativeSize"),typeof(void));
            Native.RequireReturn(Native.M("UILibrary","NewRoundButton","UnityEngine.RectTransform"),Native.T("UIRoundButton_UI2"));
            Native.RequireReturn(Native.M("TechPopupContent","AddInfoPopup","UIRoundButton_UI2","System.String","System.String"),Native.T("UIRoundButton_UI2"));
            Native.RequireReturn(Native.M("UIRoundButton_UI2","SetSprite","UnityEngine.Sprite","System.Single"),typeof(void));
            Native.Hook(h,Native.M("TechItem","GetUnlockItems","Polytopia.Data.TechData","PlayerState","System.Boolean"),typeof(NativeBranchTechUi),null,nameof(UnlockItems));
            Native.Hook(h,Native.M("TechPopupContent","CreateTechDataContent","Polytopia.Data.TechData"),typeof(NativeBranchTechUi),nameof(PopupContent));
            Native.Hook(h,Native.M("TechItem","SetData","Polytopia.Data.TechData"),typeof(NativeBranchTechUi),nameof(BeforeTreeLabel),nameof(TreeLabel));
            Native.Hook(h,Native.M("TechView","OnDisable"),typeof(NativeBranchTechUi),nameof(ClearLabels));
        }
        static void RestoreLabel(LabelLayout row)
        {
            if(!Native.UnityAlive(row.Localizer)||!Native.UnityAlive(row.Text))return;
            Native.Set(row.Localizer,"ShouldWrapNonAsian",row.Wrap);
            Native.Set(row.Text,"enableWordWrapping",row.TextWrap);
            Native.Set(row.Text,"fontSizeMin",row.Min);Native.Set(row.Text,"fontSizeMax",row.Max);
            Native.Set(row.Text,"enableAutoSizing",row.AutoSize);Native.Set(row.Text,"fontSize",row.Font);
        }
        public static void ClearLabels()
        {
            foreach(var row in Labels)try{RestoreLabel(row);}catch(Exception e){Entry.Note("branch-tech-label-error",e.ToString());}
            Labels.Clear();
        }
        public static void BeforeTreeLabel(object __instance)
        {
            for(int i=Labels.Count-1;i>=0;i--)
            {
                var row=Labels[i];bool same=Native.Same(row.Item,__instance);
                if(!same&&Native.UnityAlive(row.Item)&&Native.UnityAlive(row.Localizer)&&Native.UnityAlive(row.Text))continue;
                Labels.RemoveAt(i);if(same)try{RestoreLabel(row);}catch(Exception e){Entry.Note("branch-tech-label-error",e.ToString());}
            }
        }
        public static void TreeLabel(object __instance,TechData __0)
        {
            if(!RecruitmentHooks.Registered||__0 is null||Native.EnumName(Native.Get(__0,"type")!)!=TechTree.CounterintelligenceId)return;
            LabelLayout? row=null;
            try
            {
                var localizer=Native.Get(__instance,"label");if(!Native.UnityAlive(localizer))return;
                var text=Native.Get(localizer!,"TextComponent");if(!Native.UnityAlive(text))return;
                float font=Convert.ToSingle(Native.Get(text!,"fontSize"));if(!float.IsFinite(font)||font<=0)return;
                row=new(__instance,localizer!,text!,Convert.ToBoolean(Native.Get(localizer!,"ShouldWrapNonAsian")),
                    Convert.ToBoolean(Native.Get(text!,"enableWordWrapping")),Convert.ToBoolean(Native.Get(text!,"enableAutoSizing")),font,
                    Convert.ToSingle(Native.Get(text!,"fontSizeMin")),Convert.ToSingle(Native.Get(text!,"fontSizeMax")));
                // Shipped node:120x120; its centered label is90x30 at font14.
                // Keep the same rectangle/position and let the two words wrap,
                // with at most15% native font reduction to fit those two lines.
                // The full localized name remains unchanged in every popup.
                Native.Set(localizer!,"ShouldWrapNonAsian",true);Native.Set(text!,"enableWordWrapping",true);
                Native.Set(text!,"fontSizeMin",font*.85f);Native.Set(text!,"fontSizeMax",font);Native.Set(text!,"enableAutoSizing",true);
                Labels.Add(row);row=null;
            }
            catch(Exception e){if(row is not null)try{RestoreLabel(row);}catch{}Entry.Note("branch-tech-label-error",e.ToString());}
        }
        static bool Behavior(TechData? data,out string id)
        {
            id=data is null?"":Native.EnumName(Native.Get(data,"type")!);
            return RecruitmentHooks.Registered&&id!=TechTree.DeceptionId&&TechTree.IsBranchId(id);
        }
        internal static string[] AbilityKeys(string id)=>id switch
        {
            TechTree.PropagandaId=>new[]{"dcp_disinformation","dcp_disguise"},
            TechTree.EspionageId=>new[]{"dcp_blackmail"},
            TechTree.IntelligenceId=>new[]{"dcp_knowledge","dcp_tech_insight"},
            TechTree.CounterintelligenceId=>new[]{"dcp_trade_secrets"},
            _=>Array.Empty<string>()
        };
        static bool HasUnit(string id)=>id==TechTree.EspionageId||id==TechTree.CounterintelligenceId;
        static string Localized(string key)=>(string)(Native.Invoke(Native.M("Localization","Get","System.String",typeof(Il2CppReferenceArray<Il2CppSystem.Object>).FullName!),null,key,null)
            ??throw new InvalidOperationException("Missing localized branch text: "+key));
        static string Name(string key)=>Localized(key=="dcp_disguise"?"ui.labels.dcp_disguise_unlock":"ability.names."+key);
        static int ViewerColor(object? supplied=null)=>BlackmailArtAssets.PlayerColor(Native.Static("GameManager","GameState"),supplied??Native.Static("GameManager","LocalPlayer"));
        static void Retire(object? component)
        {if(Native.UnityAlive(component))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(component!,"gameObject"));}
        static object Sprite(string key)=>key switch
        {
            "dcp_disinformation"=>BranchArtAssets.Get(TechTree.PropagandaId,true),
            "dcp_disguise"=>BranchArtAssets.DisguiseTech(),
            "dcp_knowledge"=>BranchArtAssets.Get(TechTree.IntelligenceId,true),
            "dcp_tech_insight"=>BranchArtAssets.Reconnaissance(),
            _=>throw new InvalidOperationException("Unknown branch ability icon: "+key)
        };
        static object Image(string key,int color)
        {
            if(key=="dcp_blackmail")return BlackmailArtAssets.Image(color);
            if(key=="dcp_trade_secrets")return TradeSecretsArtAssets.Image(color);
            object? image=null;
            try
            {
                image=Native.Invoke(Native.M("UIUtils","GetImage"),null)!;
                Native.Set(image,"sprite",Sprite(key));Native.Set(image,"preserveAspect",true);Native.Set(image,"raycastTarget",false);
                Native.Invoke(Native.M("UnityEngine.UI.Image","SetNativeSize"),image);
                var result=image;image=null;return result;
            }
            finally{Retire(image);}
        }
        public static void UnlockItems(TechData __0,ref Il2CppReferenceArray<RectTransform> __result,PlayerState? __1=null,bool __2=false)
        {
            if(!Behavior(__0,out var id)||__result is null)return;
            // __2 requests one research-animation item. Keep a native unit when
            // present; otherwise the first behavior reward supplies that item.
            if(__2&&__result.Length>0)return;
            var owned=new List<object>();
            try
            {
                var items=Native.Items(__result).Cast<RectTransform>().ToList();
                foreach(string key in AbilityKeys(id).Take(__2?1:int.MaxValue))
                {
                    var image=Image(key,ViewerColor(__1));owned.Add(image);items.Add((RectTransform)Native.Get(image,"rectTransform")!);
                }
                __result=new Il2CppReferenceArray<RectTransform>(items.ToArray());owned.Clear();
            }
            catch(Exception e){foreach(var item in owned)Retire(item);Entry.Note("branch-tech-icon-error",id+": "+e);throw;}
        }
        static object Pair(object first,object second)
        {
            object? container=null;
            try
            {
                container=Native.Invoke(Native.M("UILibrary","NewEmptyComponent","UnityEngine.RectTransform"),null,new object?[]{null})!;
                float a=Convert.ToSingle(Native.Invoke(Native.M("UIBasicComponent","GetWidth"),first)),b=Convert.ToSingle(Native.Invoke(Native.M("UIBasicComponent","GetWidth"),second));
                float h=Math.Max(Convert.ToSingle(Native.Invoke(Native.M("UIBasicComponent","GetHeight"),first)),Convert.ToSingle(Native.Invoke(Native.M("UIBasicComponent","GetHeight"),second)));
                if(!float.IsFinite(a)||!float.IsFinite(b)||!float.IsFinite(h)||a<=0||b<=0||h<=0)throw new InvalidOperationException("Native ability layout is unavailable.");
                const float gap=24;
                Native.Invoke(Native.M("UIBasicComponent","SetSize","System.Single","System.Single"),container,a+b+gap,h);
                foreach(var child in new[]{first,second})Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),Native.Get(child,"transform"),Native.Get(container,"rectTransform"),false);
                Native.Invoke(Native.M("UIBasicComponent","SetPosition","System.Single","System.Single"),first,-(b+gap)/2,0f);
                Native.Invoke(Native.M("UIBasicComponent","SetPosition","System.Single","System.Single"),second,(a+gap)/2,0f);
                var result=container;container=null;return result;
            }
            finally{Retire(container);}
        }
        static object Button(string key,int color)
        {
            object? button=null;
            try
            {
                button=Native.Invoke(Native.M("UILibrary","NewRoundButton","UnityEngine.RectTransform"),null,new object?[]{null})!;
                if(key=="dcp_blackmail")BlackmailArtAssets.Button(button,color,true);
                else if(key=="dcp_trade_secrets")TradeSecretsArtAssets.Button(button,color,true);
                else Native.Invoke(Native.M("UIRoundButton_UI2","SetSprite","UnityEngine.Sprite","System.Single"),button,Sprite(key),1f);
                string title=Name(key),description=Localized("ability.descriptions."+key);
                Native.Set(button,"Text",title);
                Native.Invoke(Native.M("TechPopupContent","AddInfoPopup","UIRoundButton_UI2","System.String","System.String"),null,button,title,description);
                var result=button;button=null;return result;
            }
            finally{Retire(button);}
        }
        public static bool PopupContent(TechData __0,ref UIBasicComponent __result)
        {
            if(NativeContent>0||!Behavior(__0,out var id))return true;
            object? content=null,next=null;
            try
            {
                if(HasUnit(id))
                {
                    // Reenter only the native content factory. The bounded
                    // thread scope bypasses this prefix, retaining native unit
                    // click behavior and TechPresentation's Aquarion correction.
                    try{NativeContent++;content=Native.Invoke(Native.M("TechPopupContent","CreateTechDataContent","Polytopia.Data.TechData"),null,__0);}
                    finally{NativeContent--;}
                    if(content is null)throw new InvalidOperationException("Native unit unlock content is absent.");
                }
                foreach(string key in AbilityKeys(id))
                {
                    next=Button(key,ViewerColor());
                    if(content is null){content=next;next=null;}
                    else{content=Pair(content,next);next=null;}
                }
                if(content is null)return true;
                __result=(UIBasicComponent)Native.As(content,"UIBasicComponent");content=null;return false;
            }
            catch(Exception e){Retire(content);Retire(next);Entry.Note("branch-tech-popup-error",id+": "+e);throw;}
        }
    }
}
