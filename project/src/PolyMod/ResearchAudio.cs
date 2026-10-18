using System;
using System.Collections.Generic;
using System.Reflection;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    internal static class ResearchAudio
    {
        public sealed class Scope
        {
            public Scope? Parent;
            public bool Relevant, Heard;
            public string Key="";
            public object? Skin, Player, State, Technology;
        }
        [ThreadStatic] static Scope? Current;
        static readonly HashSet<string> Completed=new();
        static MethodInfo Play=null!,HasTech=null!;
        static object Cue=null!;
        public static void Install(Harmony h)
        {
            var reaction=Native.M("ResearchReaction","Execute","Il2CppSystem.Action");Native.RequireReturn(reaction,typeof(void));
            Play=Native.M("AudioManager","PlaySFX","SFXTypes","PolytopiaBackendBase.Common.SkinType","System.Single","System.Single","System.Single");
            Native.RequireReturn(Play,typeof(void));
            HasTech=Native.M("PlayerExtensions","HasTech","PlayerState","Polytopia.Data.TechData+Type");
            Cue=Enum.Parse(Native.T("SFXTypes"),"Tech",false);
            if(Native.Int(Cue)!=47)throw new InvalidOperationException("Pinned SFXTypes.Tech differs");
            Native.Hook(h,reaction,typeof(ResearchAudio),nameof(Begin),nameof(End),nameof(FinalizeScope));
            Native.Hook(h,Play,typeof(ResearchAudio),nameof(ObserveNativeCue));
        }
        public static void Begin(object __instance,out Scope __state)
        {
            __state=new Scope{Parent=Current};Current=__state;
            try
            {
                var a=Native.Get(__instance,"action");var local=Native.Static("GameManager","LocalPlayer");var state=Native.Static("GameManager","GameState");
                if(a is null||local is null||state is null)return;
                var type=Native.Get(a,"Type")!;
                if(!TechTree.IsBranchId(Native.EnumName(type))||Native.Int(Native.Get(a,"PlayerId"))!=Native.Int(Native.Get(local,"Id")))return;
                __state.Relevant=true;__state.Player=local;__state.State=state;__state.Technology=type;
                __state.Key=Native.Pointer(state)+":"+Native.Int(Native.Get(state,"Seed"))+":"+Native.Int(Native.Get(local,"Id"))+":"+Native.Int(type);
                __state.Skin=Native.Invoke(Native.M("PlayerState","GetVisualSkin"),local);
            }
            catch(Exception e){Entry.Note("research-audio-guard",e.Message);}
        }
        public static void ObserveNativeCue(object __0)
        {
            if(Native.Int(__0)==47)for(var s=Current;s is not null;s=s.Parent)if(s.Relevant)s.Heard=true;
        }
        public static void End(Scope __state)
        {
            try
            {
                if(!__state.Relevant)return;
                bool sameGame=Native.Same(__state.State,Native.Static("GameManager","GameState"));
                bool local=Native.Same(__state.Player,Native.Static("GameManager","LocalPlayer"));
                bool owned=sameGame&&local&&(bool)Native.Invoke(HasTech,null,__state.Player,__state.Technology)!;
                bool completed=Completed.Contains(__state.Key);
                if(ResearchFeedbackPolicy.NeedsFallback(true,local,owned,true,__state.Heard,completed))
                {
                    var audio=Native.Static("AudioManager","instance");
                    if(audio is not null)
                    {
                        Native.Invoke(Play,audio,Cue,__state.Skin,1f,1f,0f);
                        Completed.Add(__state.Key);Entry.Note("research-cue","Native SFXTypes.Tech requested for committed local branch research.");
                    }
                }
                else if(__state.Heard){Completed.Add(__state.Key);Entry.Note("research-cue","Original native Tech cue observed; no extra sound requested.");}
            }
            catch(Exception e){Entry.Note("research-audio-error",e.ToString());}
            finally{Current=__state.Parent;}
        }
        public static Exception? FinalizeScope(Exception? __exception,Scope? __state)
        {if(__state is not null)Current=__state.Parent;return __exception;}
    }
}
