using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // UI owns only buttons and disposable target renderers. Every committed
    // action is re-offered and validated against the authoritative ledger.
    internal static class NativeSpecialActionUi
    {
        internal static readonly NativeExchangeKind[] Kinds={NativeExchangeKind.Subvert,NativeExchangeKind.Blackout,
            NativeExchangeKind.FeignHeal,NativeExchangeKind.FeignBoost,NativeExchangeKind.FeignSwarm};
        sealed class Control
        {internal object Bar=null!,Unit=null!,Button=null!,Buttons=null!,Callback=null!;internal NativeExchangeKind Kind;}
        sealed record Context(object Bar,object Unit,object Body,object State,object Client,object Player,object Map,object Interaction,
            uint Id,byte Actor,uint Turn,ushort Command,long Revision,int X,int Y,object Snapshot);
        sealed record Availability(Context Context,string Stamp,NativeExchangeKind[] Kinds);
        sealed record Target(object Tile,object Data,object? Unit,object? Body,int X,int Y,uint Id,object Object);
        sealed class Session
        {internal Context Context=null!;internal Control Control=null!;internal bool Automatic;internal object? Root;internal long Due;internal readonly List<Target> Targets=new();}
        static readonly Dictionary<(long,NativeExchangeKind),Control> Controls=new();
        static readonly Dictionary<long,Availability> Available=new();
        static object? SelectedInteraction,SelectedUnit;
        sealed record FocusWatch(object Interaction,object Client,object Map);
        static FocusWatch? Watch;
        static Session? Active;static Availability? AutoAttempt;
        static long TickNumber;
        static bool Ending;
        internal static bool Choosing=>Active is {Automatic:false};
        static string Caption(NativeExchangeKind k)=>k switch{NativeExchangeKind.Subvert=>"Subvert",NativeExchangeKind.Blackout=>"Blackout",
            NativeExchangeKind.FeignHeal=>"Feign Heal",NativeExchangeKind.FeignBoost=>"Feign Boost",NativeExchangeKind.FeignSwarm=>"Feign Swarm",NativeExchangeKind.FeignRecover=>"Feign Recover",_=>throw new InvalidOperationException("Unsupported specialist action")};
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("ClientInteraction","SelectUnit","Unit"),typeof(NativeSpecialActionUi),null,nameof(Selected));
            // Native movement preserves focus through these paths, without
            // calling SelectUnit. Read the completed native selection only.
            Native.Hook(h,Native.M("ClientInteraction","TryUpdateSelection","Unit"),typeof(NativeSpecialActionUi),null,nameof(Refocused));
            Native.Hook(h,Native.M("ClientInteraction","UpdateAfterMapRefresh"),typeof(NativeSpecialActionUi),null,nameof(Refocused));
            foreach(string name in new[]{"ClearButtons","Reset","DeInit","OnDestroy"})Native.Hook(h,Native.M("InteractionBar",name),typeof(NativeSpecialActionUi),nameof(NativeClear));
            Native.Hook(h,Native.M("GameManager","Update"),typeof(NativeSpecialActionUi),null,nameof(Tick));
            foreach(string name in new[]{"SendCommand","SendCommandRemote"})Native.Hook(h,Native.M("ClientBase",name,"CommandBase"),typeof(NativeSpecialActionUi),nameof(GuardCommands));
            Native.Hook(h,Native.M("ClientInteraction","ValidateMove","Unit","Tile","Il2CppSystem.Action"),typeof(NativeSpecialActionUi),nameof(GuardCommands));
            Native.Hook(h,Native.M("ClientInteraction","OnPress","Tile"),typeof(NativeSpecialActionUi),nameof(GuardCommands));
            Native.Hook(h,Native.M("ClientInteraction","OnRelease","Tile","System.Int32"),typeof(NativeSpecialActionUi),nameof(Released));
            Native.Hook(h,Native.M("Unit","UpdateOverlays"),typeof(NativeSpecialActionUi),nameof(BeforeOverlays));
            foreach(string name in new[]{"ClearSelection","DeselectUnit","OnSelectionCleared"})Native.Hook(h,Native.M("ClientInteraction",name),typeof(NativeSpecialActionUi),nameof(SelectionClearing));
            foreach(string name in new[]{"OnTurnStarted","OnDisable"})Native.Hook(h,Native.M("ClientInteraction",name),typeof(NativeSpecialActionUi),nameof(Clear));
            foreach(string name in new[]{"ReturnToMenu","OnClientSessionOpened"})Native.Hook(h,Native.M("GameManager",name),typeof(NativeSpecialActionUi),nameof(Clear));
        }
        public static bool GuardCommands()=>!Choosing;
        public static bool BeforeOverlays(object __instance)=>Active is null||Active.Automatic||!Native.Same(Active.Context.Unit,__instance);
        public static void Selected(object __instance,object __0)
        {
            End(false);SelectedInteraction=__instance;SelectedUnit=__0;Observe(__instance);
            try{var bar=Native.Invoke(Native.M("ClientInteraction","GetInteractionBar"),__instance);if(bar is not null)Sync(bar,__0);}
            catch(Exception e){Entry.Note("special-select-error",e.ToString());Clear();}
        }
        public static void Refocused(object __instance,bool __runOriginal)
        {
            if(!__runOriginal)return;
            try
            {
                var unit=Native.Get(__instance,"selectedUnit");
                if(!Native.UnityAlive(unit))return;
                Observe(__instance);
                var bar=Native.Invoke(Native.M("ClientInteraction","GetInteractionBar"),__instance);
                if(!Native.UnityAlive(bar)||!Native.Same(Native.Get(bar!,"unit"),unit))return;
                // A map refresh can replace the native visual wrapper. Full
                // context/visibility/turn/command guards still gate Sync.
                if(!Native.Same(SelectedInteraction,__instance)||!Native.Same(SelectedUnit,unit)){End(false);AutoAttempt=null;}
                SelectedInteraction=__instance;SelectedUnit=unit;Sync(bar!,unit);
            }
            catch(Exception e){Entry.Note("special-refocus-error",e.ToString());End(false);}
        }
        static void Observe(object interaction)
        {
            var client=Native.Static("GameManager","Client");var map=Native.Static("MapRenderer","Current");
            if(client is not null&&Native.UnityAlive(map))Watch=new(interaction,client,map!);
        }
        public static void SelectionClearing(object? __instance=null)
        {
            // Native move/map-refresh callbacks can temporarily clear/rebind
            // the selection before the bar catches up. Retire all authority,
            // but keep this exact UI observer until the current client/map ends.
            // Native selection is authoritative. This observer never selects
            // a unit or restores focus after movement.
            var watch=Watch;ClearUi();Watch=watch;
        }
        static void ReadCompletedFocus()
        {
            var watch=Watch;if(watch is null)return;
            if(!Native.UnityAlive(watch.Interaction)||!Native.Same(Native.Static("GameManager","Client"),watch.Client)||
                !Native.Same(Native.Static("MapRenderer","Current"),watch.Map)){Watch=null;return;}
            if((bool)Native.Get(watch.Client,"IsWaitingForCommand")!||(bool)Native.Get(Native.Get(watch.Client,"ActionManager")!,"IsProcessing")!)return;
            var unit=Native.Get(watch.Interaction,"selectedUnit");
            var bar=Native.Invoke(Native.M("ClientInteraction","GetInteractionBar"),watch.Interaction);
            if(!Native.UnityAlive(unit)||!Native.UnityAlive(bar)||!Native.Same(Native.Get(bar!,"unit"),unit))return;
            if(!Native.Same(SelectedInteraction,watch.Interaction)||!Native.Same(SelectedUnit,unit))
            {End(false);AutoAttempt=null;SelectedInteraction=watch.Interaction;SelectedUnit=unit;}
        }
        static bool Owned(Control c)=>Native.UnityAlive(c.Bar)&&Native.UnityAlive(c.Button)&&Native.Same(Native.Get(c.Bar,"buttons"),c.Buttons)&&
            Native.Items(c.Buttons).Any(b=>Native.Same(b,c.Button))&&Native.Same(Native.Get(c.Button,"OnClicked"),c.Callback)&&
            ((string?)Native.Get(c.Button,"text")==Caption(c.Kind)||(string?)Native.Get(c.Button,"text")=="Cancel");
        internal static IEnumerable<object> OwnedButtons(object bar)=>Kinds.Select(k=>Controls.TryGetValue((Native.Pointer(bar),k),out var c)&&Owned(c)?c.Button:null).Where(x=>x is not null)!;
        static bool ReadContext(object bar,object unit,out Context c)
        {
            c=null!;var interaction=SelectedInteraction;
            if(!NativeRegroupSession.Enabled||!RecruitmentHooks.Registered||!Native.UnityAlive(bar)||!Native.UnityAlive(unit)||interaction is null||
                !Native.Same(SelectedUnit,unit)||!Native.Same(Native.Get(interaction,"selectedUnit"),unit)||!Native.Same(Native.Get(bar,"unit"),unit)||
                !Native.Same(Native.Invoke(Native.M("ClientInteraction","GetInteractionBar"),interaction),bar)||
                (bool)Native.Static("PopupManager","PopupShowing")!)return false;
            var state=Native.Static("GameManager","GameState");var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");
            var map=Native.Static("MapRenderer","Current");var body=Native.Get(unit,"UnitState");
            if(state is null||client is null||player is null||body is null||Native.Int(Native.Get(body,"health"))<=0||!Native.UnityAlive(map)||!NativeRegroupSession.Ready(state)||
                !Native.Same(Native.Get(client,"GameState"),state)||!(bool)Native.Get(client,"IsReady")!||
                (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!||(bool)Native.Get(client,"IsWaitingForCommand")!||
                (bool)Native.Get(Native.Get(client,"ActionManager")!,"IsProcessing")!||!(bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!||
                (bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!)return false;
            byte actor=Convert.ToByte(Native.Get(player,"Id"));uint id=Convert.ToUInt32(Native.Get(body,"id"));
            if(Native.Int(Native.Get(state,"CurrentPlayer"))!=actor||!(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,actor)!||
                !NativeRogueRules.CanCommand(state,body,actor)||!Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,id),unit))return false;
            object?[] lookup={id,null};if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,lookup)!||!Native.Same(lookup[1],body))return false;
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return false;var at=Native.Get(body,"coordinates")!;
            c=new(bar,unit,body,state,client,player,map!,interaction,id,actor,Convert.ToUInt32(Native.Get(state,"CurrentTurn")),
                Convert.ToUInt16(Native.Get(state,"CurrentCommand")),snapshot.Revision,Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")),snapshot);return true;
        }
        static bool Same(Context a,Context b)=>Native.Same(a.Bar,b.Bar)&&Native.Same(a.Unit,b.Unit)&&Native.Same(a.Body,b.Body)&&Native.Same(a.State,b.State)&&
            Native.Same(a.Client,b.Client)&&Native.Same(a.Player,b.Player)&&Native.Same(a.Map,b.Map)&&Native.Same(a.Interaction,b.Interaction)&&
            ReferenceEquals(a.Snapshot,b.Snapshot)&&a.Id==b.Id&&a.Actor==b.Actor&&a.Turn==b.Turn&&a.Command==b.Command&&a.Revision==b.Revision&&a.X==b.X&&a.Y==b.Y;
        static NativeExchangeKind[] Offers(Context c)
        {
            bool spy=SpyCatalog.IsSpyId(Native.EnumName(Native.Get(c.Body,"type")!));
            string stamp=Native.Get(c.Body,"type")+":"+Native.Get(c.Body,"owner")+":"+Native.Get(c.Body,"health")+":"+
                Native.Get(c.Body,"moved")+":"+Native.Get(c.Body,"attacked")+":"+string.Join(",",Native.Items(Native.Get(c.Body,"effects")!).Select(Native.Int));
            long key=Native.Pointer(c.Bar);
            if(Available.TryGetValue(key,out var old)&&Same(old.Context,c)&&old.Stamp==stamp)return old.Kinds;
            var kinds=new List<NativeExchangeKind>();
            if(spy&&NativeSpyNetwork.ActionReady(c.State,c.Body,c.Actor)&&NativeSpyNetwork.TryReachable(c.State,c.Actor,c.Id,out var targets,out _)&&targets.Length>0)
            {
                if(targets.Length<=8192&&targets.Any(data=>SubvertTarget(c,data,out _,out _,out _)))kinds.Add(NativeExchangeKind.Subvert);
            }
            if(NativeFoggerActions.Ready(c.State,c.Body,c.Actor)&&NativeSpyNetwork.TryFoggerReachable(c.State,c.Actor,c.Id,out var fogTargets,out _)&&fogTargets.Length>0)
                kinds.Add(NativeExchangeKind.Blackout);
            foreach(var kind in Kinds.Skip(2))if(NativeFeignSupport.TryOffer(c.State,c.Id,kind,out _,out _))kinds.Add(kind);
            if(Available.Count>=8&&!Available.ContainsKey(key))Available.Clear();Available[key]=new(c,stamp,kinds.ToArray());return kinds.ToArray();
        }
        internal static void Sync(object bar,object? unit)
        {
            if(Ending)return;
            try
            {
                if(Active is Session s&&Native.Same(s.Context.Bar,bar)){if(Valid(s))return;End(false);}
                if(unit is null||MimicRegroupUi.Choosing||Choosing||!ReadContext(bar,unit,out var c)){Remove(bar);return;}
                var kinds=Offers(c);bool changed=false;
                foreach(var kind in Kinds)
                {
                    var key=(Native.Pointer(bar),kind);
                    if(Controls.TryGetValue(key,out var old)&&(!Owned(old)||!Native.Same(old.Unit,unit))){Remove(bar,kind);changed=true;}
                    if(!kinds.Contains(kind)){changed|=Controls.ContainsKey(key);Remove(bar,kind);continue;}if(Controls.ContainsKey(key))continue;
                    object button=Native.Invoke(Native.M("InteractionBar","CreateRoundBottomBarButton","System.String","System.Boolean"),bar,Caption(kind),true)!;
                    var control=new Control{Bar=bar,Unit=unit,Button=button,Buttons=Native.Get(bar,"buttons")!,Kind=kind};
                    control.Callback=Native.ButtonCallback(()=>Click(control));Native.Set(button,"OnClicked",control.Callback);Native.Set(button,"OnClickedWhenDisabled",null);
                    Native.Set(button,"Cost",-1f);Native.Set(button,"buttonActive",true);Native.Set(button,"ButtonEnabled",true);Controls[key]=control;changed=true;
                    Native.Invoke(Native.M("UIRoundButton","SetSprite","UnityEngine.Sprite","System.Boolean"),button,kind==NativeExchangeKind.Subvert?MimicMapInspection.SubvertIcon():kind==NativeExchangeKind.Blackout?SmokeGrenadeAssets.Icon():UnitArtAssets.Icon(),true);
                    Native.Invoke(Native.M("UIButtonBase","SetActive","System.Boolean"),button,true);
                }
                if(changed)MimicPresentationUi.ReflowBar(bar);
                Automatic(c,kinds);
            }
            catch(Exception e){Entry.Note("special-menu-error",e.ToString());Remove(bar);}
        }
        static void Click(Control control)
        {
            try
            {
                if(Ending||!Controls.TryGetValue((Native.Pointer(control.Bar),control.Kind),out var owned)||!ReferenceEquals(owned,control)||!Owned(control))return;
                if(Active is Session active){if(!active.Automatic&&ReferenceEquals(active.Control,control)){End(true);return;}if(active.Automatic)End(false);}
                if(Choosing||MimicRegroupUi.Choosing||!ReadContext(control.Bar,control.Unit,out var c))return;
                if(control.Kind is not (NativeExchangeKind.Subvert or NativeExchangeKind.Blackout))
                {if(NativeFeignSupport.TryOffer(c.State,c.Id,control.Kind,out var offered,out _)&&offered is not null)Send(c,offered);return;}
                Begin(control,c,false);
            }
            catch(Exception e){Entry.Note("special-target-error",e.ToString());End(false);}
        }
        static void Automatic(Context c,NativeExchangeKind[] kinds)
        {
            if(Active is not null||!kinds.Contains(NativeExchangeKind.Subvert)||!Controls.TryGetValue((Native.Pointer(c.Bar),NativeExchangeKind.Subvert),out var control))return;
            var offer=Available[Native.Pointer(c.Bar)];
            if(AutoAttempt is Availability old&&Same(old.Context,c)&&old.Stamp==offer.Stamp)return;
            Begin(control,c,true);AutoAttempt=offer;
        }
        static bool SubvertTarget(Context c,object data,out object? body,out object? unit,out uint id)
        {
            body=Native.Get(data,"unit");unit=null;id=0;if(body is null)return false;
            id=Convert.ToUInt32(Native.Get(body,"id"));
            unit=Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),c.Map,id);
            if(!Native.UnityAlive(unit)||!Native.Same(Native.Get(unit!,"UnitState"),body)||
                !(bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!||(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!||
                !(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,c.Actor)!)return false;
            var at=Native.Get(data,"coordinates")!;
            var tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),c.Map,at);
            if(!Native.UnityAlive(tile)||(bool)Native.Get(tile!,"IsHidden")!||!Native.Same(Native.Get(tile!,"Data"),data)||
                !Native.Same(Native.Get(tile!,"unit"),unit))return false;
            return NativeSubvert.TryOffer(c.State,c.Id,id,out _,out _);
        }
        static void Begin(Control control,Context c,bool automatic)
        {
            object[] tiles;
            bool reachable=control.Kind==NativeExchangeKind.Blackout?
                NativeSpyNetwork.TryFoggerReachable(c.State,c.Actor,c.Id,out tiles,out _):
                NativeSpyNetwork.TryReachable(c.State,c.Actor,c.Id,out tiles,out _);
            if(!reachable||tiles.Length==0||tiles.Length>8192)return;
            var session=new Session{Context=c,Control=control,Automatic=automatic,Due=TickNumber+1};Active=session;
            if(!automatic){MimicHoldInput.Clear();Native.Set(control.Button,"text","Cancel");}
            session.Root=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Deception "+Caption(control.Kind)+" targets"})!;
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),session.Root,false);
            foreach(var data in tiles)
            {
                uint target=0;object? body=null,unit=null;
                if(control.Kind==NativeExchangeKind.Subvert)
                {
                    if(!SubvertTarget(c,data,out body,out unit,out target))continue;
                }
                session.Targets.Add(Marker(session,data,unit,body,target));
            }
            if(session.Targets.Count==0||!Valid(session)){End(!automatic);return;}
            if(!automatic){Native.Invoke(Native.M("Unit","ClearTileOverlays"),c.Unit);Native.Invoke(Native.M("Unit","ClearUnitOverlays"),c.Unit);}
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),session.Root,true);
        }
        static Target Marker(Session s,object data,object? unit,object? body,uint id)
        {
            var c=s.Context;var at=Native.Get(data,"coordinates")!;var tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),c.Map,at);
            if(!Native.UnityAlive(tile)||!Native.Same(Native.Get(tile!,"Data"),data)||
                !Native.Same(Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(c.State,"Map"),at),data)||
                (unit is not null&&(!Native.Same(Native.Get(tile!,"unit"),unit)||!Native.Same(Native.Get(unit,"UnitState"),body))))throw new InvalidOperationException("Target tile identity changed");
            var source=Native.Get(tile!,"overlayRenderer")!;var sprite=Native.Get(tile!,"attackOverlay")!;
            if(!Native.UnityAlive(source)||!Native.UnityAlive(sprite))throw new InvalidOperationException("Native target art unavailable");
            object go=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Deception "+Caption(s.Control.Kind)+" target"})!;
            try
            {
                var type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"))!;
                var renderer=Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),go,type)!,"UnityEngine.SpriteRenderer");
                var t=Native.Get(go,"transform")!;var st=Native.Get(source,"transform")!;
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),t,Native.Get(s.Root!,"transform"),false);
                Native.Set(t,"localPosition",Native.Get(st,"position"));Native.Set(t,"localRotation",Native.Get(st,"rotation"));Native.Set(t,"localScale",Native.Get(st,"lossyScale"));
                Native.Set(go,"layer",Native.Get(Native.Get(source,"gameObject")!,"layer"));Native.Set(renderer,"sprite",sprite);Native.Set(renderer,"sharedMaterial",Native.Get(source,"sharedMaterial"));
                Native.Set(renderer,"color",Activator.CreateInstance(Native.T("UnityEngine.Color"),1f,1f,1f,1f)!);
                Native.Set(renderer,"sortingLayerID",Native.Get(source,"sortingLayerID"));Native.Set(renderer,"sortingOrder",Native.Int(Native.Get(source,"sortingOrder"))+1);
                return new(tile!,data,unit,body,Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")),id,go);
            }
            catch{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,go);throw;}
        }
        static bool Valid(Session s)=>ReferenceEquals(Active,s)&&Owned(s.Control)&&!MimicRegroupUi.Choosing&&ReadContext(s.Context.Bar,s.Context.Unit,out var now)&&
            Same(s.Context,now)&&(s.Control.Kind==NativeExchangeKind.Blackout?NativeFoggerActions.Ready(now.State,now.Body,now.Actor):
                SpyCatalog.IsSpyId(Native.EnumName(Native.Get(now.Body,"type")!))&&NativeSpyNetwork.ActionReady(now.State,now.Body,now.Actor));
        static bool TargetValid(Session s,Target t)
        {
            if(!Native.UnityAlive(t.Tile)||!Native.UnityAlive(t.Object)||!Native.Same(Native.Get(t.Tile,"Data"),t.Data))return false;
            var at=Native.Get(t.Data,"coordinates")!;
            if(Native.Int(Native.Get(at,"X"))!=t.X||Native.Int(Native.Get(at,"Y"))!=t.Y||!Native.Same(Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),s.Context.Map,at),t.Tile)||
                !Native.Same(Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(s.Context.State,"Map"),at),t.Data))return false;
            return s.Control.Kind==NativeExchangeKind.Blackout||Native.UnityAlive(t.Unit)&&Native.Same(Native.Get(t.Tile,"unit"),t.Unit)&&Native.Same(Native.Get(t.Data,"unit"),t.Body)&&
                Native.Same(Native.Get(t.Unit!,"UnitState"),t.Body)&&Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),s.Context.Map,t.Id),t.Unit)&&!(bool)Native.Get(t.Tile,"IsHidden")!&&
                (bool)Native.Invoke(Native.M("Unit","GetVisible"),t.Unit)!&&!(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),t.Unit)!;
        }
        public static bool Released(object __instance,object? __0,int __1)
        {
            var s=Active;if(s is null)return true;
            try
            {
                if(!Valid(s)||!Native.Same(s.Context.Interaction,__instance)){End(false);return s.Automatic;}
                if(s.Automatic&&TickNumber<s.Due)return true;
                if(TickNumber<s.Due||(bool)Native.Get(__instance,"wasDownOverUI")!||(bool)Native.Static("PolytopiaInput","OutsideWindow")!||
                    (bool)Native.Static("CameraController","IsDragging")!||(bool)Native.Static("CameraController","IsBlocked")!)return s.Automatic;
                var touches=Native.Items(Native.Static("PolytopiaInput","CurrentTouches")!).ToArray();
                if(touches.Length!=1||__1!=0||Native.Get(touches[0],"phase")?.ToString()!="Ended"||!(bool)Native.Invoke(Native.M("ClientInteraction","IsTouchValidTap","System.Int32"),__instance,__1)!)return s.Automatic;
                if((bool)Native.Invoke(Native.M("PolytopiaInput","IsPositionOverUIObject","UnityEngine.Vector2","System.Int32"),null,Native.Get(touches[0],"position"),-1)!)return s.Automatic;
                var target=s.Targets.SingleOrDefault(t=>Native.Same(t.Tile,__0));if(target is null){if(s.Automatic)return true;End(true);return false;}if(!TargetValid(s,target)){End(false);return false;}
                NativeExchangeIntent? offer;
                bool ok=s.Control.Kind==NativeExchangeKind.Blackout?NativeBlackout.TryOffer(s.Context.State,s.Context.Id,target.X,target.Y,out offer,out _):
                    NativeSubvert.TryOffer(s.Context.State,s.Context.Id,target.Id,out offer,out _);
                if(ok&&offer is not null)Send(s.Context,offer);else End(false);return false;
            }
            catch(Exception e){Entry.Note("special-target-click-error",e.ToString());End(false);return false;}
        }
        static void Send(Context c,NativeExchangeIntent intent)
        {
            if(!ReadContext(c.Bar,c.Unit,out var now)||!Same(c,now))return;
            var command=NativeExchangeCommands.Create(intent);
            if(!NativeExchangeCommands.Valid(command,c.State,true,out _)){NativeExchangeCommands.Forget(command);End(false);return;}
            End(false);Native.Invoke(Native.M("ClientBase","SendCommand","CommandBase"),c.Client,command);
        }
        public static void Tick()
        {
            TickNumber++;
            try
            {
                ReadCompletedFocus();
                if(Active is Session s){if(!Valid(s)||s.Targets.Any(t=>!TargetValid(s,t))){if(s.Automatic)AutoAttempt=null;End(false);}else return;}
                if(SelectedInteraction is object interaction&&Native.UnityAlive(SelectedUnit))
                {var bar=Native.Invoke(Native.M("ClientInteraction","GetInteractionBar"),interaction);if(bar is not null)Sync(bar,SelectedUnit);}
            }catch(Exception e){Entry.Note("special-target-tick-error",e.ToString());End(false);}
        }
        static void End(bool refresh)
        {
            Available.Clear();var s=Active;Active=null;if(s is null||Ending)return;Ending=true;
            try
            {
                if(Native.UnityAlive(s.Root)){try{Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),s.Root,false);}finally{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,s.Root);}}
                if(Owned(s.Control)){Native.Set(s.Control.Button,"text",Caption(s.Control.Kind));Native.Set(s.Control.Button,"ButtonEnabled",true);}
                if(refresh&&ReadContext(s.Context.Bar,s.Context.Unit,out var now)&&Same(s.Context,now))Native.Invoke(Native.M("Unit","UpdateOverlays"),s.Context.Unit);
            }
            catch(Exception e){Entry.Note("special-target-clear-error",e.ToString());}finally{Ending=false;}
        }
        static void Remove(object bar,NativeExchangeKind kind)
        {
            if(!Controls.Remove((Native.Pointer(bar),kind),out var c)||!Owned(c))return;
            Native.Set(c.Button,"OnClicked",null);Native.Set(c.Button,"OnClickedWhenDisabled",null);Native.Set(c.Button,"ButtonEnabled",false);
            Native.Invoke(Native.M(c.Buttons.GetType(),"Remove","UIRoundButton"),c.Buttons,c.Button);Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(c.Button,"gameObject"));
        }
        internal static void Remove(object bar){Available.Remove(Native.Pointer(bar));foreach(var kind in Kinds)Remove(bar,kind);}
        public static void NativeClear(object __instance)
        {
            // Native bar refresh may happen after the completed-move target
            // scan, with exactly the same command/snapshot. Its old no-repeat
            // token must not suppress reconstruction of the retired markers.
            if(AutoAttempt is Availability attempted&&Native.Same(attempted.Context.Bar,__instance))AutoAttempt=null;
            if(Active is Session s&&Native.Same(s.Context.Bar,__instance))End(false);Available.Remove(Native.Pointer(__instance));
            foreach(var kind in Kinds)if(Controls.Remove((Native.Pointer(__instance),kind),out var c)&&Owned(c)){Native.Set(c.Button,"OnClicked",null);Native.Set(c.Button,"OnClickedWhenDisabled",null);}
        }
        public static void Clear()=>ClearUi();
        static void ClearUi(){Watch=null;End(false);AutoAttempt=null;SelectedInteraction=null;SelectedUnit=null;Available.Clear();foreach(var bar in Controls.Values.Select(c=>c.Bar).Distinct().ToArray())Remove(bar);}
    }
}
