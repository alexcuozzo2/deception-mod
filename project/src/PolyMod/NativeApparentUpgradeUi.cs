using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    internal static class NativeApparentUpgradeUi
    {
        sealed class Control
        {
            internal object Bar=null!,Unit=null!,Button=null!,Buttons=null!,Callback=null!;
            internal NativeExchangeKind Kind;
        }
        sealed record Cache(object Bar,object Unit,object Body,object State,object Player,object Snapshot,long Command,uint Turn,byte TileOwner,int Research,NativeExchangeKind[] Available);
        static readonly Dictionary<(long,NativeExchangeKind),Control> Controls=new();
        static readonly Dictionary<long,Cache> Caches=new();
        static readonly HashSet<string> Errors=new(StringComparer.Ordinal);
        static string Caption(NativeExchangeKind kind)=>kind==NativeExchangeKind.ShedDisguise?"Shed Disguise":"Feign "+RecruitmentCatalog.Label(NativeApparentUpgrades.Form(kind));
        static bool Offer(object state,uint id,NativeExchangeKind kind,out NativeExchangeIntent? intent,out string reason)=>kind==NativeExchangeKind.ShedDisguise?
            NativeShedDisguise.TryOffer(state,id,out intent,out reason):NativeApparentUpgrades.TryOffer(state,id,kind,out intent,out reason);
        internal static void Install(Harmony h)
        {
            foreach(string name in new[]{"ClearButtons","Reset","DeInit","OnDestroy"})
                Native.Hook(h,Native.M("InteractionBar",name),typeof(NativeApparentUpgradeUi),nameof(NativeClear));
            Native.Hook(h,Native.M("BuildReaction","Execute","Il2CppSystem.Action"),typeof(NativeApparentUpgradeUi),nameof(BeforeReaction));
            _=Native.M("UpgradeReaction","Execute","Il2CppSystem.Action");
            _=Native.M(RecruitmentHooks.GLD,"GetUnlockedUpgradesForUnit","PlayerState","GameState","Polytopia.Data.UnitData");
            _=Native.M("Polytopia.Data.UnitData","get_upgradesFrom");_=Native.M("TileData","get_owner");
        }
        static bool Owned(Control c)=>Native.UnityAlive(c.Bar)&&Native.UnityAlive(c.Button)&&
            Native.Same(Native.Get(c.Bar,"buttons"),c.Buttons)&&Native.Items(c.Buttons).Any(x=>Native.Same(x,c.Button))&&
            (string?)Native.Get(c.Button,"text")==Caption(c.Kind)&&Native.Same(Native.Get(c.Button,"OnClicked"),c.Callback);
        internal static IEnumerable<object> OwnedButtons(object bar)=>Controls.Values.Where(c=>Native.Same(c.Bar,bar)&&Owned(c)).OrderBy(c=>(int)c.Kind).Select(c=>c.Button).ToArray();
        static int Research(object player)
        {
            int result=0,bit=1;
            foreach(string tech in new[]{TechTree.PropagandaId,"sailing","ramming","navigation"})
            {
                if((bool)Native.Invoke(Native.M("PlayerExtensions","HasTech","PlayerState",RecruitmentHooks.TT),null,player,Native.EnumValue(RecruitmentHooks.TT,tech))!)result|=bit;
                bit<<=1;
            }
            return result;
        }
        internal static void Sync(object bar,object? unit,bool allowed)
        {
            try
            {
                if(!allowed||unit is null||MimicRegroupUi.Choosing||!Native.UnityAlive(bar)||!Native.UnityAlive(unit)){Remove(bar);return;}
                object? state=Native.Static("GameManager","GameState"),player=Native.Static("GameManager","LocalPlayer"),body=Native.Get(unit,"UnitState");
                if(state is null||player is null||body is null||!NativeRegroupSession.Ready(state)){Remove(bar);return;}
                var snapshot=NativeGameStateEnvelope.Get(state);
                var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),Native.Get(body,"coordinates"));
                if(snapshot is null||tile is null||!Native.Same(Native.Get(tile,"unit"),body)){Remove(bar);return;}
                long key=Native.Pointer(bar),command=Convert.ToInt64(Native.Get(state,"CurrentCommand"));uint turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));
                byte owner=Convert.ToByte(Native.Get(tile,"owner"));int research=Research(player);
                if(Caches.TryGetValue(key,out var cached)&&Native.Same(cached.Bar,bar)&&Native.Same(cached.Unit,unit)&&Native.Same(cached.Body,body)&&
                    Native.Same(cached.State,state)&&Native.Same(cached.Player,player)&&ReferenceEquals(cached.Snapshot,snapshot)&&cached.Command==command&&cached.Turn==turn&&cached.TileOwner==owner&&cached.Research==research&&
                    Controls.Values.Where(c=>Native.Same(c.Bar,bar)).All(Owned))return;
                Remove(bar,false);
                uint id=Convert.ToUInt32(Native.Get(body,"id"));
                var available=NativeApparentUpgrades.Kinds.Append(NativeExchangeKind.ShedDisguise).Where(k=>Offer(state,id,k,out _,out _)).ToArray();
                if(Caches.Count>=8)Caches.Clear();Caches[key]=new(bar,unit,body,state,player,snapshot,command,turn,owner,research,available);
                foreach(var kind in available)
                {
                    object button=Native.Invoke(Native.M("InteractionBar","CreateRoundBottomBarButton","System.String","System.Boolean"),bar,Caption(kind),true)!;
                    var c=new Control{Bar=bar,Unit=unit,Button=button,Buttons=Native.Get(bar,"buttons")!,Kind=kind};
                    c.Callback=Native.ButtonCallback(()=>Clicked(c));Controls[(key,kind)]=c;
                    Native.Set(button,"OnClicked",c.Callback);Native.Set(button,"OnClickedWhenDisabled",null);
                    if(kind==NativeExchangeKind.ShedDisguise)
                        Native.Invoke(Native.M("UIRoundButton","SetSprite","UnityEngine.Sprite","System.Boolean"),button,UnitArtAssets.Icon(),true);
                    else
                    {
                    object data=RecruitmentHooks.Data(state,NativeApparentUpgrades.Form(kind));
                    object skin=Native.Invoke(Native.M("PlayerState","GetVisualSkin"),player)!;
                    object renderer=Native.Invoke(Native.M("UIUtils","GetUIUnitRenderer","Polytopia.Data.UnitData","PlayerState","PolytopiaBackendBase.Common.SkinType"),null,data,player,skin)!;
                    bool attached=false;
                    try{Native.Invoke(Native.M("UIRoundButton","SetUnitRenderer","UIUnitRenderer"),button,renderer);attached=true;}
                    finally
                    {
                        if(!attached&&Native.UnityAlive(renderer))
                            Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(renderer,"gameObject"));
                    }
                    }
                    Native.Set(button,"Cost",-1f);Native.Set(button,"buttonActive",true);Native.Set(button,"ButtonEnabled",true);
                    Native.Invoke(Native.M("UIButtonBase","SetActive","System.Boolean"),button,true);
                }
                if(available.Length>0){Native.Invoke(Native.M("InteractionBar","SetupButtonNavigation"),bar);MimicPresentationUi.ReflowBar(bar);}
            }
            catch(Exception e){Note(e);Remove(bar);}
        }
        static void Clicked(Control c)
        {
            object? command=null;bool handed=false;
            try
            {
                if(!Controls.TryGetValue((Native.Pointer(c.Bar),c.Kind),out var current)||!ReferenceEquals(c,current)||!Owned(c)||MimicRegroupUi.Choosing||
                    !MimicPresentationUi.CanSmuggleView(c.Unit)||!Native.Same(Native.Get(c.Bar,"unit"),c.Unit))return;
                var state=Native.Static("GameManager","GameState");var client=Native.Static("GameManager","Client");
                var body=Native.Get(c.Unit,"UnitState");if(state is null||client is null||body is null)return;
                if(!Offer(state,Convert.ToUInt32(Native.Get(body,"id")),c.Kind,out var intent,out _)){Remove(c.Bar);return;}
                command=NativeExchangeCommands.Create(intent!);handed=true;
                Native.Invoke(Native.M("ClientBase","SendCommand","CommandBase"),client,command);
            }
            catch(Exception e){Note(e);}
            finally{if(command is not null&&!handed)NativeExchangeCommands.Forget(command);}
        }
        static void Disable(Control c)
        {
            Native.Set(c.Button,"OnClicked",null);Native.Set(c.Button,"OnClickedWhenDisabled",null);c.Callback=null!;
            Native.Set(c.Button,"ButtonEnabled",false);Native.Set(c.Button,"buttonActive",false);
            Native.Invoke(Native.M("UIButtonBase","SetActive","System.Boolean"),c.Button,false);
        }
        internal static void Remove(object bar,bool reflow=true)
        {
            long key=Native.Pointer(bar);Caches.Remove(key);bool changed=false;
            foreach(var pair in Controls.Where(x=>x.Key.Item1==key).ToArray())
            {
                Controls.Remove(pair.Key);var c=pair.Value;
                try
                {
                    if(!Owned(c))continue;
                    Disable(c);Native.Invoke(Native.M(c.Buttons.GetType(),"Remove","UIRoundButton"),c.Buttons,c.Button);
                    Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(c.Button,"gameObject"));changed=true;
                }
                catch(Exception e){Note(e);}
            }
            if(changed&&reflow&&Native.UnityAlive(bar))MimicPresentationUi.ReflowBar(bar);
        }
        public static void NativeClear(object __instance)
        {
            long key=Native.Pointer(__instance);Caches.Remove(key);
            foreach(var pair in Controls.Where(x=>x.Key.Item1==key).ToArray())
            {Controls.Remove(pair.Key);try{if(Owned(pair.Value))Disable(pair.Value);}catch(Exception e){Note(e);}}
        }
        internal static void Clear(){foreach(var c in Controls.Values.ToArray())Remove(c.Bar,false);Caches.Clear();Errors.Clear();}
        public static bool BeforeReaction(object __instance,object __0)
        {
            var action=Native.Get(__instance,"action");if(action is null||!NativeExchangeCommands.IsExchange(action))return true;
            var intent=NativeExchangeCommands.Get(action);if(intent is null||!NativeApparentUpgrades.IsKind(intent.Kind))return true;
            bool handed=false,completed=false;
            void Complete(){if(completed)return;completed=true;Native.Invoke(Native.M("Il2CppSystem.Action","Invoke"),__0);}
            try
            {
                NativeExchangeCommands.ValidateBase(action,intent);
                object? state=Native.Static("GameManager","GameState"),player=Native.Static("GameManager","LocalPlayer"),map=Native.Static("MapRenderer","Current");
                if(state is null||player is null||!Native.UnityAlive(map)||!NativeRegroupSession.Ready(state))return false;
                var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null||!NativeExchangeIntentCodec.AlreadyCommitted(NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision),snapshot.Revision,intent))return false;
                var at=Activator.CreateInstance(Native.T("WorldCoordinates"),intent.Initiator.X,intent.Initiator.Y)!;
                var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
                byte viewer=Convert.ToByte(Native.Get(player,"Id"));
                if(tile is null||!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,viewer)!)return false;
                var visual=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,at);
                if(!Native.UnityAlive(visual)||!Native.Same(Native.Get(visual!,"Data"),tile)||(bool)Native.Get(visual!,"IsHidden")!)return false;
                var visible=Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),tile,state,viewer,false);
                if(visible is null||Convert.ToUInt32(Native.Get(visible,"id"))!=intent.Initiator.NativeId)return false;
                // A detached action is supplied ONLY to native visual reaction;
                // it is never executed, enqueued or recorded as a real upgrade.
                object fake=Activator.CreateInstance(Native.T("UpgradeAction"),intent.Actor,Native.EnumValue(RecruitmentHooks.UT,NativeApparentUpgrades.Form(intent.Kind)),at,0)!;
                object reaction=Activator.CreateInstance(Native.T("UpgradeReaction"),fake)!;
                var callback=Native.Callback(Complete);
                handed=true;Native.Invoke(Native.M("UpgradeReaction","Execute","Il2CppSystem.Action"),reaction,callback);
            }
            catch(Exception e){Note(e);Complete();}
            finally{if(!handed)Complete();}
            return false;
        }
        static void Note(Exception e){if(Errors.Count<24&&Errors.Add(e.ToString()))Entry.Note("apparent-upgrade-ui-error",e.ToString());}
    }
}
