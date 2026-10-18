using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // UI-A07: per-unit view memory is session-local. Input/selection authorization
    // remains transient; only the native information action opens a popup.
    internal static class MimicPresentationUi
    {
        const string UD="Polytopia.Data.UnitData";
        static readonly MimicInspectionSession Inspection=new();
        sealed class FactoryScope
        {
            public FactoryScope? Previous;
            public MimicProjection? Projection;
            public object? Owner,Data,Unit;
            public InspectionContext Context;
            public bool Actual,Populated,Failed;
            public PublicPopup? Public;
            public int Populations;
        }
        sealed record PublicPopup(object Client,object Map,object Viewer,object Body);
        sealed record PopupView(object Popup,object Unit,MimicProjection Projection,InspectionContext Context,bool Actual,bool ArtReady,PublicPopup? Public=null);
        sealed record Portrait(object Container,object Data,object Source,object? PopupParent=null);
        sealed record BarView(object Bar,object Unit,Portrait? Portrait,bool Actual,bool ArtReady,MimicProjection Projection,int OwnerColor,bool Failed=false);
        [ThreadStatic] static FactoryScope? CurrentFactory;
        static object? SelectedInteraction,SelectedUnit;
        static BarView? PrivateBar;
        static long SessionClient;
        static readonly Dictionary<(bool Logical,ulong Id),uint> PreferenceKeys=new();
        static readonly Dictionary<long,PopupView> PopupViews=new();
        static readonly Dictionary<long,Portrait> PopupPortraits=new();
        static readonly HashSet<long> BlockedPopups=new();
        static readonly HashSet<string> ReportedErrors=new(StringComparer.Ordinal);
        static readonly HashSet<long> ReflowingBars=new();
        // Details can include secret-bearing native exception messages. Keep
        // the operation name, without types, IDs, pointers or per-unit traces.
        static void Note(string kind,string detail)
        {if(ReportedErrors.Count<32&&ReportedErrors.Add(kind))Entry.Note(kind,"Presentation diagnostic; private context omitted.");}
        public static void Install(Harmony h)
        {
            var array=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1")
                .MakeGenericType(Native.T("PopupBase+PopupButtonData"));
            _=Native.M("PopupManager+UnitPopupData","set_buttonData",array.FullName!);
            _=Native.T("PopupManager+UnitPopupData").GetConstructor(Type.EmptyTypes)??throw new MissingMethodException("UnitPopupData constructor");
            _=array.GetConstructor(new[]{typeof(long)})??throw new MissingMethodException("Native popup-button array constructor");
            _=Native.M("UnitPopup","set_player","PlayerState");
            _=Native.M("UnitPopup","get_statsRows");
            _=Native.M("BasicPopupLegacy","SetDefaultOkbutton","Il2CppSystem.Action");
            MimicActionMenu.Install();MimicAbilityTags.Install();
            MimicControls.ValidateVisibilityApi();
            _=Native.M("UnitStatusDisplay","get_healthLabel");
            _=Native.M("TMPro.TMP_Text","get_color");
            _=Native.M("TMPro.TMP_Text","set_color","UnityEngine.Color");
            Native.Hook(h,Native.M("UnitStatusDisplay","SetState","UnitState"),typeof(MimicPresentationUi),nameof(HealthState),nameof(HealthColor));
            Native.Hook(h,Native.M("Unit","UpdateObject","MapRenderContext","SkinVisualsTransientData"),typeof(MimicPresentationUi),nameof(BeforeVisualEffects),null,nameof(VisualEffectsFinished));
            Native.Hook(h,Native.M("UnitState","HasEffect","UnitEffect"),typeof(MimicPresentationUi),null,nameof(VisualEffectRead));
            Native.Hook(h,Native.M("ClientInteraction","SelectUnit","Unit"),typeof(MimicPresentationUi),null,nameof(Selected));
            foreach(string name in new[]{"ClearSelection","DeselectUnit","OnSelectionCleared","OnTurnStarted","OnDisable"})
                Native.Hook(h,Native.M("ClientInteraction",name),typeof(MimicPresentationUi),null,nameof(SelectionCleared));
            Native.Hook(h,Native.M("ClientInteraction","ValidateMove","Unit","Tile","Il2CppSystem.Action"),typeof(MimicPresentationUi),nameof(CommandDispatch));
            Native.Hook(h,Native.M("ClientBase","SendCommand","CommandBase"),typeof(MimicPresentationUi),nameof(CommandDispatch));
            Native.Hook(h,Native.M("ClientBase","SendCommandRemote","CommandBase"),typeof(MimicPresentationUi),nameof(CommandDispatch));
            Native.Hook(h,Native.M("PopupManager","GetUnitPopup","PopupManager+UnitPopupData"),typeof(MimicPresentationUi),nameof(PopupData),nameof(PopupReady),nameof(PopupFinished));
            Native.Hook(h,Native.M("UnitPopup","set_UnitData",UD),typeof(MimicPresentationUi),nameof(PopupPopulate));
            Native.Hook(h,Native.M("PopupBase","Show"),typeof(MimicPresentationUi),nameof(PopupCanShow));
            Native.Hook(h,Native.M("PopupBase","Hide"),typeof(MimicPresentationUi),nameof(PopupHiding));
            Native.Hook(h,Native.M("UnitPopup","ResetPopup"),typeof(MimicPresentationUi),nameof(ResetPopup));
            foreach(string name in new[]{"Refresh","RefreshUnitOptions","RefreshPCBarInternal"})
                Native.Hook(h,Native.M("InteractionBar",name),typeof(MimicPresentationUi),nameof(BeforeBarRefresh),nameof(BarReady));
            Native.Hook(h,Native.M("InteractionBar","OnUnitSelected","Unit"),typeof(MimicPresentationUi),nameof(BeforeBarRefresh),nameof(BarReady));
            Native.Hook(h,Native.M("GameManager","Update"),typeof(MimicPresentationUi),null,nameof(Tick));
            Native.Hook(h,Native.M("ClientBase","SetNewLocalPlayerTurnForPassAndPlay","System.Byte"),typeof(MimicPresentationUi),nameof(SelectionCleared));
            Native.Hook(h,Native.M("GameManager","SetReplayClient","ReplayClient"),typeof(MimicPresentationUi),nameof(Clear));
            Native.Hook(h,Native.M("GameManager","ReturnToMenu"),typeof(MimicPresentationUi),nameof(Clear));
            Native.Hook(h,Native.M("GameManager","OnClientSessionOpened"),typeof(MimicPresentationUi),nameof(Clear));
        }
        public static void Clear()
        {VisualScope=null;VisualGeneration++;NativeSpyPresentation.Clear();foreach(var view in new List<PopupView>(PopupViews.Values))if(view.Public is not null)ClosePrivatePopup(view);ResetInspection();Inspection.Reset();SessionClient=0;PreferenceKeys.Clear();MimicMapInspection.Clear();MimicAbilityTags.Clear();CurrentFactory=null;PopupViews.Clear();PopupPortraits.Clear();ReportedErrors.Clear();}
        static void ResetInspection(bool clearInput=true)
        {
            Inspection.ClearSelection();if(clearInput)MimicHoldInput.Clear();
            // Native bar reflow can reenter our presentation callback. Revoke
            // selection first so cleanup cannot recreate an authorized overlay.
            SelectedInteraction=null;SelectedUnit=null;
            RestorePrivateBar();MimicInspectionControls.Clear();MimicSmuggleUi.Clear();ClosePrivatePopups();
        }
        static string Hp(int current,int maximum)=>current.ToString(CultureInfo.InvariantCulture)+"/"+maximum.ToString(CultureInfo.InvariantCulture);
        internal static string DisguiseLabel(string label)
        {
            var array=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1").MakeGenericType(Native.T("Il2CppSystem.Object"));
            var suffix=(string)Native.Invoke(Native.M("Localization","Get","System.String",array.FullName!),null,"ui.labels.dcp_disguise",null)!;
            return label+" "+suffix;
        }
        static string ApparentLabel(MimicProjection p)=>HasTruthAccess(p)&&!p.Exposed?DisguiseLabel(p.Label):p.Label;
        internal static bool IsHealthLabel(string? label,string localizedHealth)
        {
            // Native rows append spaced dot leaders to the localized name.
            // Match only that decoration, so similarly named stats cannot match.
            if(label is null||string.IsNullOrEmpty(localizedHealth)||!label.StartsWith(localizedHealth,StringComparison.Ordinal))return false;
            for(int i=localizedHealth.Length;i<label.Length;i++)
                if(label[i]!='.'&&!char.IsWhiteSpace(label[i]))return false;
            return true;
        }
        static bool ProjectUnit(object? unit,out MimicProjection projection)
        {projection=null!;return unit is not null&&MimicPresentationState.TryProject(Native.Get(unit,"UnitState")!,out projection);}
        static bool Context(MimicProjection p,out InspectionContext context,bool requireTurn=true)
        {
            var player=Native.Static("GameManager","LocalPlayer");
            int viewer=player is null?-1:Native.Int(Native.Get(player,"Id"));
            var game=Native.Static("GameManager","GameState");var client=Native.Static("GameManager","Client");
            long session=client is null?0:Native.Pointer(client);
            if(SessionClient!=0&&SessionClient!=session)Clear();
            SessionClient=session;
            var key=(p.LogicalId!=0,p.LogicalId!=0?p.LogicalId:p.UnitId);
            if(!PreferenceKeys.TryGetValue(key,out uint preference))
            {
                if(PreferenceKeys.Count>=8192)throw new InvalidOperationException("Private view preference bound exceeded.");
                preference=checked((uint)PreferenceKeys.Count+1);PreferenceKeys.Add(key,preference);
            }
            context=new InspectionContext(session,viewer,Convert.ToUInt32(Native.Get(p.State,"CurrentTurn")),preference);
            bool remembered=p.Identity?.Rogue?.Owner==viewer&&viewer!=p.Owner;
            return Native.Same(game,p.State)&&(viewer==p.Owner||remembered)&&client is not null&&
                (remembered||!requireTurn||Native.Int(Native.Get(p.State,"CurrentPlayer"))==viewer)&&
                !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!&&
                (bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,checked((byte)viewer))!;
        }
        static bool Visible(object unit)=>(bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!&&
                !(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!;
        static bool Idle()
        {
            var client=Native.Static("GameManager","Client");
            var manager=client is null?null:Native.Get(client,"ActionManager");
            return client is not null&&manager is not null&&!(bool)Native.Get(client,"IsWaitingForCommand")!&&
                !(bool)Native.Get(manager,"IsProcessing")!;
        }
        static bool Authorized(object interaction,object unit,out MimicProjection p,out InspectionContext context)
        {
            context=default;p=null!;
            return Native.Same(Native.Get(interaction,"selectedUnit"),unit)&&ProjectUnit(unit,out p)&&Context(p,out context)&&Visible(unit);
        }
        internal static bool TryInspectionContext(object interaction,object unit,out InspectionContext context)=>
            Authorized(interaction,unit,out _,out context);
        internal static bool CanInspect(object interaction,object unit)
        {
            try{return !MimicRegroupUi.Choosing&&!NativeSpecialActionUi.Choosing&&Authorized(interaction,unit,out _,out _)&&Idle()&&!(bool)Native.Static("PopupManager","PopupShowing")!;}
            catch(Exception e){Note("mimic-inspection-access-error",e.Message);return false;}
        }
        static bool CanContinueInspection(object unit)=>SelectedInteraction is not null&&Native.Same(SelectedUnit,unit)&&
            Authorized(SelectedInteraction,unit,out _,out _)&&Idle()&&!(bool)Native.Static("PopupManager","PopupShowing")!;
        internal static bool CanContinueAction(object unit,object? popup)
        {
            if(SelectedInteraction is null||!Native.Same(SelectedUnit,unit)||!Authorized(SelectedInteraction,unit,out var p,out _)||!CommandAuthority(unit,p)||!Idle())return false;
            if(!(bool)Native.Static("PopupManager","PopupShowing")!)return popup is null;
            var manager=Native.Static("PopupManager","instance");
            return popup is not null&&manager is not null&&Native.Same(Native.Invoke(Native.M("PopupManager","GetCurrentPopup"),manager),popup);
        }
        internal static object? ActionInteraction(object unit)=>SelectedInteraction is not null&&Native.Same(SelectedUnit,unit)&&
            Native.Same(Native.Get(SelectedInteraction,"selectedUnit"),unit)?SelectedInteraction:null;
        static bool Actual(MimicProjection p,out InspectionContext context)
        {
            bool authorized=Context(p,out context)&&SelectedInteraction is not null&&SelectedUnit is not null&&
                Authorized(SelectedInteraction,SelectedUnit,out var live,out var selectedContext)&&selectedContext==context&&
                live.UnitId==p.UnitId&&live.Owner==p.Owner&&live.VariantId==p.VariantId;
            // An unrelated popup/bar query must not reset the selected Mimic session.
            return p.Exposed?PublicMapContext(p):authorized&&Inspection.ActualFor(context,true);
        }
        public static void Selected(object __instance,object __0)
        {
            try
            {
                if(!Authorized(__instance,__0,out _,out var context)){ResetInspection();return;}
                if(SelectedUnit is not null&&(!Native.Same(SelectedUnit,__0)||!Native.Same(SelectedInteraction,__instance)))ResetInspection();
                SelectedInteraction=__instance;SelectedUnit=__0;Inspection.Select(context,true);
                RefreshSelectedBar();
            }
            catch(Exception e){Note("mimic-inspection-select-error",e.ToString());ResetInspection();}
        }
        public static void SelectionCleared(MethodBase? __originalMethod=null)
        {ResetInspection();}
        public static void CommandDispatch()
        {ResetInspection();}
        internal static bool ToggleInspection(object interaction,object unit)
        {
            try
            {
                if(!CanInspect(interaction,unit)||!Authorized(interaction,unit,out var p,out var context)||p.Exposed)return false;
                if(!Native.Same(SelectedInteraction,interaction)||!Native.Same(SelectedUnit,unit))
                {ResetInspection(clearInput:false);SelectedInteraction=interaction;SelectedUnit=unit;Inspection.Select(context,true);}
                Inspection.Select(context,true);
                if(!Inspection.Toggle(context,true))return false;
                ClosePrivatePopups();RefreshSelectedBar();MimicMapInspection.Tick();
                Entry.Note("mimic-inspection-toggle","Private inspection view changed; no command submitted.");
                return true;
            }
            catch(Exception e){Note("mimic-inspection-toggle-error",e.ToString());ResetInspection();return false;}
        }
        // Map preferences are independent of selection. The renderer additionally
        // verifies this is the current live map instance before applying a view.
        internal static bool? MapView(object unit)
        {
            try
            {
                // A remembered map appearance is read-only. The current viewer
                // can keep seeing its own unit during an opponent turn; all
                // input, popup and selection paths retain the turn gate.
                if(!ProjectUnit(unit,out var p)||!Visible(unit))return null;
                if(p.Exposed&&PublicMapContext(p))return true;
                if(Context(p,out var context,requireTurn:false))return Inspection.StoredActualFor(context,true);
                // A genuine unit's real prefab remains native. Its independent
                // apparent overlay must also be visible to ordinary opponents;
                // this grants no access to actual art or private paired badges.
                return (!p.ActualIsMimic||NativeFoggerPresentation.CustomBadge(p))&&PublicMapContext(p)?false:null;
            }
            catch(Exception e){Note("mimic-map-access-error",e.Message);return null;}
        }
        internal static bool PublicMapContext(MimicProjection p)
        {
            var client=Native.Static("GameManager","Client");var viewer=Native.Static("GameManager","LocalPlayer");
            return p.Identity is {Alive:true,Independent:true}&&Native.Same(Native.Static("GameManager","GameState"),p.State)&&
                client is not null&&viewer is not null&&!(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!&&
                (bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,Convert.ToByte(Native.Get(viewer,"Id")))!;
        }
        static object ApparentPlayer(MimicProjection p,object owner)=>NativeSpyPresentation.AppearancePlayer(owner,p.Appearance!);
        static bool ArtReady(MimicProjection p,object owner)=>(!p.ActualIsMimic||p.ActualMimicBoat||UnitArtAssets.Prepare(owner))&&
            (!p.ApparentMimic||p.ApparentMimicBoat||UnitArtAssets.Prepare(ApparentPlayer(p,owner)));
        static bool PopupCustom(MimicProjection p,bool actual)=>actual?p.ActualIsMimic||NativeFoggerPresentation.CustomActual(p):p.ApparentMimic||NativeFoggerPresentation.CustomForm(p.NativeId);
        static bool PopupFittedCustom(MimicProjection p,bool actual)=>actual?NativeFoggerPresentation.CustomActual(p):NativeFoggerPresentation.CustomForm(p.NativeId);
        static bool PopupAquarion(MimicProjection p,bool actual)=>actual?p.ActualAmphibiousMimic||p.ActualNativeId==SpyCatalog.AquarionId:
            p.NativeId==AquarionCatalog.MimicId||p.Appearance?.Passenger?.Form==AquarionCatalog.MimicId||p.NativeId==SpyCatalog.AquarionId;
        static object PopupArtOwner(MimicProjection p,object unit,object owner,bool actual)=>actual?
            NativeRogueVisuals.ArtOwner(p.State,Native.Get(unit,"UnitState")!,owner):ApparentPlayer(p,owner);
        static bool PopupArtReady(MimicProjection p,object unit,object owner,bool actual)=>!PopupCustom(p,actual)||
            (actual?NativeFoggerPresentation.IsFogger(p):NativeFoggerPresentation.IsForm(p.NativeId))||
            (actual?p.ActualMimicBoat:p.ApparentMimicBoat)||UnitArtAssets.Prepare(PopupArtOwner(p,unit,owner,actual));
        static void RefreshSelectedBar()
        {
            if(SelectedInteraction is null)return;
            var bar=Native.Invoke(Native.M("ClientInteraction","GetInteractionBar"),SelectedInteraction);
            if(bar is not null)BarReady(bar);
        }

        [ThreadStatic] static VisualEffects? VisualScope;
        static long VisualGeneration;
        internal sealed record VisualEffects(object Visual,object Unit,MimicProjection Projection,object Map,object Client,object Player,VisualEffects? Previous,long Generation);
        public static void BeforeVisualEffects(object __instance,object __0,out VisualEffects? __state)
        {
            __state=null;
            try
            {
                if(!ProjectUnit(__instance,out var p)||p.Appearance is null||MapView(__instance)!=false||!NativeSpyPresentation.Visible(__instance,p))return;
                var map=Native.Static("MapRenderer","Current")!;var player=Native.Static("GameManager","LocalPlayer")!;
                if(!Native.Same(Native.Get(__0,"mapData"),Native.Get(p.State,"Map"))||!Native.Same(Native.Get(__0,"gld"),Native.Get(p.State,"GameLogicData"))||
                    !Native.Same(Native.Get(__0,"unitRegistry"),map)||Native.Int(Native.Get(__0,"localPlayerId"))!=Native.Int(Native.Get(player,"Id")))return;
                __state=new(__instance,Native.Get(__instance,"UnitState")!,p,map,Native.Static("GameManager","Client")!,player,VisualScope,VisualGeneration);VisualScope=__state;
            }
            catch(Exception e){Note("apparent-effects-scope-error",e.Message);}
        }
        public static void VisualEffectRead(object __instance,object __0,ref bool __result)
        {
            if(VisualScope is not VisualEffects scope||!Native.Same(scope.Unit,__instance)||Native.Int(__0)==3)return;
            try
            {
                if(!Native.Same(Native.Static("GameManager","GameState"),scope.Projection.State)||!Native.Same(Native.Static("GameManager","Client"),scope.Client)||
                    !Native.Same(Native.Static("GameManager","LocalPlayer"),scope.Player)||!Native.Same(Native.Static("MapRenderer","Current"),scope.Map)||
                    !NativeSpyPresentation.Visible(scope.Visual,scope.Projection)||!Native.Same(Native.Get(scope.Visual,"UnitState"),scope.Unit)||
                    MapView(scope.Visual)!=false||!MimicPresentationState.TryProject(scope.Unit,out var live)||!SameAppearance(live.Appearance,scope.Projection.Appearance))return;
                int value=Native.Int(__0);if(value is <0 or >8)return;
                __result=scope.Projection.Appearance!.Effects.Any(e=>Native.Int(Enum.Parse(Native.T("UnitEffect"),e,false))==value);
            }
            catch(Exception e){Note("apparent-effects-read-error",e.Message);}
        }
        public static Exception? VisualEffectsFinished(Exception? __exception,VisualEffects? __state)
        {if(__state is not null)VisualScope=__state.Generation==VisualGeneration?__state.Previous:null;return __exception;}
        internal static void RefreshMapEffects(object unit,bool forceActual=false)
        {
            if(!Native.UnityAlive(unit))return;
            var state=Native.Static("GameManager","GameState");var map=Native.Static("MapRenderer","Current");var body=Native.Get(unit,"UnitState");
            if(state is null||!Native.UnityAlive(map)||body is null)return;
            uint id=Convert.ToUInt32(Native.Get(body,"id"));object?[] lookup={id,null};
            if(!Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,id),unit)||
                !(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,lookup)!||!Native.Same(lookup[1],body))return;
            bool visible=(bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!&&!(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!;
            bool projected=ProjectUnit(unit,out var p);bool actual=forceActual||!projected||MapView(unit)==true;
            var effects=actual?Native.Items(Native.Get(body,"effects")!).Select(Native.Int).ToArray():
                (p.Appearance?.Effects??Array.Empty<string>()).Select(e=>Native.Int(Enum.Parse(Native.T("UnitEffect"),e,false))).ToArray();
            Native.Invoke(Native.M("Unit","ShowBubble","System.Boolean"),unit,visible&&effects.Contains(4));
            foreach(int effect in new[]{6,1,2,5,0})Native.Invoke(Native.M("Unit","UpdateUnitEffectParticles","UnitEffect","System.Boolean"),unit,
                Enum.ToObject(Native.T("UnitEffect"),effect),visible&&effects.Contains(effect)&&(effect!=0||!effects.Contains(5)));
        }

        public static void HealthState(ref UnitState __0)
        {
            try
            {
                if(!MimicPresentationState.TryProject(__0,out var p))return;
                var display=Native.DetachedClone(__0,"UnitState");
                // This argument belongs exclusively to the native status widget.
                // Supplying its normal native form/max lets native healthy/injured
                // styling run. RefreshColor's exact implementation remains a Mac gate.
                Native.Set(display,"UnitData",p.ApparentData);
                Native.Set(display,"type",Native.Get(p.ApparentData,"type"));
                Native.Set(display,"health",p.Appearance?.Health??checked((ushort)(p.ApparentHealth*10)));
                if(p.Appearance is not null)Native.Set(display,"promotionLevel",(ushort)(p.Appearance.Veteran?1:0));
                // Native ship health may come from its cargo. Supply a detached
                // apparent cargo snapshot; never expose or modify the real one.
                Native.Set(display,"passengerUnit",null);
                bool actualEffects=p.Exposed||(Context(p,out var effectContext,requireTurn:false)&&Inspection.StoredActualFor(effectContext,true));
                if(!actualEffects&&p.Appearance is LedgerAppearance statusAppearance)
                    Native.Set(display,"effects",Native.NewList("UnitEffect",statusAppearance.Effects.Select(x=>Enum.Parse(Native.T("UnitEffect"),x,false))));
                if(p.Appearance?.Passenger is LedgerAppearance cargo)
                {
                    if(cargo.Passenger is not null)throw new InvalidOperationException("Nested apparent cargo is unsupported.");
                    var passenger=Native.DetachedClone(__0,"UnitState");
                    var cargoData=RecruitmentHooks.Data(p.State,cargo.Form);
                    Native.Set(passenger,"UnitData",cargoData);Native.Set(passenger,"type",Native.Get(cargoData,"type"));
                    Native.Set(passenger,"health",cargo.Health);Native.Set(passenger,"promotionLevel",(ushort)(cargo.Veteran?1:0));
                    Native.Set(passenger,"passengerUnit",null);
                    if(!actualEffects)Native.Set(passenger,"effects",Native.NewList("UnitEffect",cargo.Effects.Select(x=>Enum.Parse(Native.T("UnitEffect"),x,false))));
                    else if(Native.Get(__0,"passengerUnit") is object realCargo)Native.Set(passenger,"effects",Native.NewList("UnitEffect",Native.Items(Native.Get(realCargo,"effects")!)));
                    Native.Set(display,"passengerUnit",passenger);
                }
                // StatusDisplay also chooses its badge from this value. Retain
                // the apparent type/cargo glyph while the owner's actual view
                // uses actual HP/max; both data objects are detached UI copies.
                if(p.Exposed||(Context(p,out var healthContext,requireTurn:false)&&Inspection.StoredActualFor(healthContext,true)))
                {
                    var shown=Native.DetachedClone(p.ApparentData,UD);
                    Native.Set(shown,"health",checked((ushort)(p.ActualMaxHealth*10)));
                    Native.Set(display,"UnitData",shown);Native.Set(display,"promotionLevel",(ushort)0);
                    Native.Set(display,"health",Native.Get(__0,"health"));
                    if(Native.Get(display,"passengerUnit") is object statusCargo)
                    {
                        var cargoData=Native.DetachedClone(Native.Get(statusCargo,"UnitData")!,UD);
                        Native.Set(cargoData,"health",checked((ushort)(p.ActualMaxHealth*10)));
                        Native.Set(statusCargo,"UnitData",cargoData);Native.Set(statusCargo,"promotionLevel",(ushort)0);
                        Native.Set(statusCargo,"health",Native.Get(__0,"health"));
                    }
                }
                __0=(UnitState)display;
            }
            catch(Exception e){Note("mimic-health-view-error",e.ToString());}
        }

        public static void HealthColor(object __instance,UnitState __0)
        {
            try
            {
                if(!Native.UnityAlive(__instance))return;
                var unit=Native.Get(__instance,"unit");
                if(!Native.UnityAlive(unit)||!Native.Same(Native.Get(unit!,"statusDisplay"),__instance)||!Visible(unit!))return;
                if(!ProjectUnit(unit,out var p))return;
                bool actual=p.Exposed||(Context(p,out var context,requireTurn:false)&&Inspection.StoredActualFor(context,true));
                bool fullMimic=actual?p.ActualIsMimic&&p.ActualHealth==1&&p.ActualMaxHealth==1:
                    p.ApparentMimic&&p.ApparentHealth==1&&p.ApparentMaxHealth==1;
                if(!fullMimic||!NativeSpyPresentation.Visible(unit!,p))return;
                // SetState colors every value below five HP red, even when it
                // represents a fully healthy one-HP unit. Correct this exact
                // private status copy only; apparent damage keeps native styling.
                if(Convert.ToUInt32(Native.Get(__0,"id"))!=p.UnitId||Convert.ToByte(Native.Get(__0,"owner"))!=p.Owner||
                    Native.Int(Native.Get(__0,"health"))!=RecruitmentCatalog.NativeHealth)return;
                var label=Native.Get(__instance,"healthLabel");
                if(!Native.UnityAlive(label)||!(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),
                    Native.Get(label!,"transform"),Native.Get(__instance,"transform"))!)return;
                var color=Native.Get(label!,"color")!;
                float alpha=Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Color","get_Item","System.Int32"),color,3));
                if(!float.IsFinite(alpha))return;
                Native.Set(label!,"color",Activator.CreateInstance(Native.T("UnityEngine.Color"),1f,1f,1f,alpha)!);
            }
            catch(Exception e){Note("mimic-health-color-error",e.ToString());}
        }

        // Called only on a map-view transition, including revocation. Native
        // status rendering and our prefix recompute health for the current
        // viewer without reselecting the unit or changing its authority.
        internal static void RefreshMapHealth(object unit)
        {
            if(!Native.UnityAlive(unit))return;
            var status=Native.Get(unit,"statusDisplay");
            if(!Native.UnityAlive(status)||!Native.Same(Native.Get(status!,"unit"),unit)||
                !(bool)Native.Get(status!,"IsUsed")!)return;
            Native.Invoke(Native.M("UnitStatusDisplay","SetState","UnitState"),status,Native.Get(unit,"UnitState"));
        }

        public static void PopupData(ref PopupManager.UnitPopupData __0,out object? __state)
        {
            var scope=new FactoryScope{Previous=CurrentFactory};CurrentFactory=scope;__state=scope;
            try
            {
                if(__0 is null)return;
                var unit=Native.Get(__0,"unit");
                if(unit is not null)
                {
                    var nativeState=Native.Get(unit,"UnitState");
                    scope.Failed=nativeState is not null&&(RecruitmentHooks.Forms.ContainsKey(Native.EnumName(Native.Get(nativeState,"type")!))||NativeFoggerPresentation.CustomForm(Native.EnumName(Native.Get(nativeState,"type")!)));
                }
                bool projected=ProjectUnit(unit,out var p);
                if(!projected)
                {
                    if(unit is null||!NativeSpyPresentation.TryProject(unit,out p)||!NativeFoggerPresentation.CustomActual(p))return;
                    if(!NativeSpyPresentation.Visible(unit,p)){scope.Failed=true;return;}
                    // An ordinary visible Spy is public information. It still
                    // uses the single detached population, without inspection
                    // authority, true/apparent controls, or a private badge.
                    scope.Public=new(Native.Static("GameManager","Client")!,Native.Static("MapRenderer","Current")!,
                        Native.Static("GameManager","LocalPlayer")!,Native.Get(unit,"UnitState")!);
                }
                bool actual=Actual(p,out var context)||scope.Public is not null;
                var payload=Native.DetachedClone(__0,"PopupManager+UnitPopupData");
                var data=Native.DetachedClone(actual?p.ActualData:p.ApparentData,UD);
                if(actual&&p.ActualIsMimic)
                {
                    // Actual information describes the Mimic's own abilities,
                    // while its operative carrier still supplies movement stats.
                    // Never edit the carrier or the registered anchor's list.
                    var anchor=RecruitmentHooks.Data(p.State,p.ActualAmphibiousMimic?AquarionCatalog.MimicId:RecruitmentCatalog.MenuId);
                    Native.Set(data,"unitAbilities",Native.NewList("Polytopia.Data.UnitAbility+Type",Native.Items(Native.Get(anchor,"unitAbilities")!)));
                }
                scope.Projection=p;scope.Owner=Native.Get(unit!,"Owner");scope.Data=data;scope.Unit=unit;
                scope.Context=context;scope.Actual=actual;
                Native.Set(payload,"unit",null);Native.Set(payload,"unitData",data);__0=(PopupManager.UnitPopupData)payload;scope.Failed=false;
            }
            catch(Exception e){scope.Failed=true;Note("mimic-popup-projection-error",e.ToString());}
        }
        // Technology and recruitment information have no live unit to project.
        // Read only the definition already chosen by the native popup; carrier
        // types and ordinary/Spy disguises cannot expose a hidden Mimic here.
        internal static bool ShowDefinitionAbilities(object popup,object? data)
        {
            if(data is null||CurrentFactory?.Projection is not null||Native.Get(popup,"unit") is not null||
                !RecruitmentCatalog.IsMenuId(Native.EnumName(Native.Get(data,"type")!)))return false;
            var player=Native.Get(popup,"player");
            var client=Native.Static("GameManager","Client");var viewer=Native.Static("GameManager","LocalPlayer");
            MimicAbilityTags.Show(popup,()=>Native.UnityAlive(popup)&&
                Native.Same(Native.Get(popup,"data"),data)&&Native.Get(popup,"unit") is null&&
                SameDefinitionContext(Native.Get(popup,"player"),player)&&
                SameDefinitionContext(Native.Static("GameManager","Client"),client)&&SameDefinitionContext(Native.Static("GameManager","LocalPlayer"),viewer)&&
                RecruitmentCatalog.IsMenuId(Native.EnumName(Native.Get(data,"type")!)),player);
            return true;
        }
        static bool SameDefinitionContext(object? current,object? expected)=>ReferenceEquals(current,expected)||Native.Same(current,expected);
        public static void PopupPopulate(object __instance,ref Polytopia.Data.UnitData __0)
        {
            var scope=CurrentFactory;
            if(scope?.Projection is not MimicProjection p||scope.Data is null||!Native.Same(scope.Data,__0))return;
            try
            {
                // Seed the UI's raw owner field BEFORE its sole behaviorful
                // UnitData population. Calling Player/UnitData again afterward
                // can rebuild/append native stats and is deliberately avoided.
                if(scope.Public is null&&scope.Actual&&(!Actual(p,out var now)||now!=scope.Context))
                {scope.Actual=false;scope.Data=Native.DetachedClone(p.ApparentData,UD);__0=(Polytopia.Data.UnitData)scope.Data;}
                if(scope.Owner is null)throw new InvalidOperationException("Projected native unit has no owner.");
                Native.Set(__instance,"player",scope.Owner);scope.Populated=true;scope.Populations++;
            }
            catch(Exception e){scope.Populated=false;Note("mimic-popup-populate-error",e.ToString());}
        }
        public static void PopupReady(object __result,object? __state)
        {
            if(__result is null||__state is not FactoryScope scope)return;
            long pointer=Native.Pointer(__result);
            if(scope.Failed){BlockPopup(__result);return;}
            if(scope.Projection is not MimicProjection p||scope.Unit is null)return;
            BlockedPopups.Remove(pointer);
            try
            {
                if(!scope.Populated||scope.Populations!=1)
                    throw new InvalidOperationException("Expected one owner-seeded native UnitData population, got "+scope.Populations);
                var view=new PopupView(__result,scope.Unit,p,scope.Context,scope.Actual,PopupArtReady(p,scope.Unit,scope.Owner!,scope.Actual),scope.Public);PopupViews[pointer]=view;
                if(scope.Actual&&!PopupViewAuthorized(view))
                    throw new InvalidOperationException("Inspection authorization changed during popup population.");
                SetPopupHeader(__result,scope.Actual?p.ActualLabel:p.Label);
                string healthName=(string)Native.Static("I2.Loc.ScriptLocalization","world_unit_health")!;
                int matches=0;
                foreach(var row in Native.Items(Native.Get(__result,"statsRows")!))
                {
                    var label=Native.Get(row,"statsNameLabel");
                    if(label is null||!IsHealthLabel((string?)Native.Get(label,"text"),healthName))continue;
                    var value=Native.Get(row,"statsValueLabel");
                    if(value is not null)Native.Set(value,"text",scope.Actual?Hp(p.ActualHealth,p.ActualMaxHealth):Hp(p.ApparentHealth,p.ApparentMaxHealth));
                    matches++;
                }
                if(matches!=1)Note("mimic-popup-health-row-count","Expected one native health row; observed "+matches+". No extra row added.");
                if(scope.Actual&&p.ActualIsMimic)
                    MimicAbilityTags.Show(__result,()=>PopupViews.TryGetValue(pointer,out var current)&&current.Actual&&
                        Native.Same(current.Unit,scope.Unit)&&current.Context==scope.Context&&PopupViewAuthorized(current));
                if(PopupCustom(p,scope.Actual))
                {
                    var renderer=Native.Get(__result,"unitRenderer");
                    var portrait=renderer is null?null:ReadPortrait(Native.Get(renderer,"duplicatedSpritesContainer"),Native.Get(renderer,"spriteDuplicationData"));
                    // The popup owns its fitted renderer transform. Spy layers
                    // must remain in the native category coordinate space.
                    if(portrait is not null)
                    {
                        portrait=portrait with{PopupParent=Native.Get(Native.Get(renderer!,"rectTransform")!,"parent")};
                        PaintPopupPortrait(renderer!,portrait,p,scope.Unit,scope.Owner!,scope.Actual);
                        PopupPortraits[pointer]=portrait;
                    }
                }
            }
            catch(Exception e)
            {BlockPopup(__result);Note("mimic-popup-view-error",e.ToString());}
        }
        static void PaintPopupPortrait(object renderer,Portrait portrait,MimicProjection p,object unit,object owner,bool actual)
        {
            var artOwner=PopupArtOwner(p,unit,owner,actual);
            if(PopupFittedCustom(p,actual))
                MimicControls.FitSpyPopup(actual?p.ActualData:p.ApparentData,artOwner,renderer,portrait.PopupParent!);
            else MimicInspectionControls.ApplyActualPortrait(portrait.Container,portrait.Data,artOwner,0.4f,
                actual?p.ActualMimicBoat:p.ApparentMimicBoat,spy:false,aquarion:PopupAquarion(p,actual),requireLiveSource:false);
        }
        static void SetPopupHeader(object popup,string title)
        {
            Native.Set(popup,"Header",title);
            // Native UnitData population positions this tag once using its
            // original (longer) name. Repeat only that native calculation after
            // our final title, retaining its standard 12-unit gap and y anchor.
            var tag=Native.Get(popup,"abilityButtonMovement");var header=Native.Get(popup,"header");
            if(!Native.UnityAlive(tag)||!Native.UnityAlive(header)||!(bool)Native.Get(Native.Get(tag!,"gameObject")!,"activeSelf")!)return;
            var preferred=Native.Invoke(Native.M("TMPro.TMP_Text","GetPreferredValues"),header)!;
            float x=Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),preferred,0))+12f;
            var rect=Native.Get(tag!,"rectTransform")!;var position=Native.Get(rect,"anchoredPosition")!;
            float y=Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Vector2","get_Item","System.Int32"),position,1));
            if(!float.IsFinite(x)||!float.IsFinite(y))throw new InvalidOperationException("Native popup title geometry is not finite.");
            Native.Set(rect,"anchoredPosition",Activator.CreateInstance(Native.T("UnityEngine.Vector2"),x,y)!);
        }
        static void RefreshPopupArt(PopupView view)
        {
            if(view.ArtReady||!PopupCustom(view.Projection,view.Actual)||!PopupProjectionCurrent(view)||
                (view.Actual?!PopupViewAuthorized(view):!NativeSpyPresentation.Visible(view.Unit,view.Projection)))return;
            var owner=Native.Get(view.Unit,"Owner");if(owner is null)return;
            owner=PopupArtOwner(view.Projection,view.Unit,owner,view.Actual);
            if(!UnitArtAssets.Prepare(owner))return;
            long key=Native.Pointer(view.Popup);
            if(!PopupPortraits.TryGetValue(key,out var portrait))return;
            var renderer=Native.Get(view.Popup,"unitRenderer");
            if(!Native.UnityAlive(renderer)||!Native.Same(Native.Get(renderer!,"duplicatedSpritesContainer"),portrait.Container)||
               !Native.Same(Native.Get(renderer!,"spriteDuplicationData"),portrait.Data)||
               !Native.Same(Native.Get(portrait.Data,"source"),portrait.Source)||
               !Native.UnityAlive(portrait.PopupParent)||!Native.Same(Native.Get(Native.Get(renderer!,"rectTransform")!,"parent"),portrait.PopupParent))return;
            if(PopupFittedCustom(view.Projection,view.Actual))
                MimicControls.FitSpyPopup(view.Actual?view.Projection.ActualData:view.Projection.ApparentData,owner,renderer!,portrait.PopupParent!);
            else MimicInspectionControls.ApplyActualPortrait(portrait.Container,portrait.Data,owner,0.4f,
                view.Actual?view.Projection.ActualMimicBoat:view.Projection.ApparentMimicBoat,spy:false,aquarion:PopupAquarion(view.Projection,view.Actual),requireLiveSource:false);
            PopupViews[key]=view with{ArtReady=true};
        }
        public static Exception? PopupFinished(Exception? __exception,object? __state)
        {CurrentFactory=(__state as FactoryScope)?.Previous;return __exception;}
        static bool PublicPopupCurrent(PopupView view)=>view.Public is PublicPopup context&&
            Native.Same(context.Client,Native.Static("GameManager","Client"))&&Native.Same(context.Map,Native.Static("MapRenderer","Current"))&&
            Native.Same(context.Viewer,Native.Static("GameManager","LocalPlayer"))&&Native.Same(context.Body,Native.Get(view.Unit,"UnitState"))&&
            NativeSpyPresentation.Visible(view.Unit,view.Projection);
        static bool PopupProjectionCurrent(PopupView view)=>(view.Public is not null?NativeSpyPresentation.TryProject(view.Unit,out var live):ProjectUnit(view.Unit,out live))&&
            Native.Same(live.State,view.Projection.State)&&(view.Public is null||PublicPopupCurrent(view))&&
            live.UnitId==view.Projection.UnitId&&live.LogicalId==view.Projection.LogicalId&&live.Owner==view.Projection.Owner&&
            live.ActualIsMimic==view.Projection.ActualIsMimic&&live.NativeId==view.Projection.NativeId&&
            SameAppearance(live.Appearance,view.Projection.Appearance);
        static bool PopupViewAuthorized(PopupView view)=>view.Public is not null?PopupProjectionCurrent(view):PopupProjectionCurrent(view)&&ProjectUnit(view.Unit,out var live)&&
            live.UnitId==view.Projection.UnitId&&live.Owner==view.Projection.Owner&&live.VariantId==view.Projection.VariantId&&
            Native.Same(live.State,view.Projection.State)&&Actual(live,out var now)&&now==view.Context&&
            (!live.Exposed||NativeSpyPresentation.Visible(view.Unit,live));
        public static bool PopupCanShow(object __instance)
        {
            try
            {
                long key=Native.Pointer(__instance);
                if(BlockedPopups.Contains(key))return false;
                if(PopupViews.TryGetValue(key,out var view)&&(!PopupProjectionCurrent(view)||view.Actual&&!PopupViewAuthorized(view)))
                {BlockedPopups.Add(key);return false;}
                return true;
            }
            catch(Exception e){Note("mimic-popup-show-error",e.Message);return false;}
        }
        static void BlockPopup(object popup)
        {
            long key=Native.Pointer(popup);BlockedPopups.Add(key);
            try{Native.Invoke(Native.M("PopupBase","Hide"),popup);}
            catch(Exception e){Note("mimic-popup-hide-error",e.Message);}
            finally{BlockedPopups.Add(key);}
        }
        public static void ResetPopup(object __instance)
        {
            MimicAbilityTags.Remove(__instance);
            long key=Native.Pointer(__instance);
            // Real native reset starts a fresh population; always release these
            // guards even if restoring the previous portrait fails.
            PopupViews.Remove(key);BlockedPopups.Remove(key);
            try{if(PopupPortraits.Remove(key,out var portrait))RestorePortrait(portrait);}
            catch(Exception e){Note("mimic-popup-reset-error",e.Message);}
        }
        static void ClosePrivatePopup(PopupView view)
        {
            long key=Native.Pointer(view.Popup);BlockedPopups.Add(key);
            try
            {
                if(PopupPortraits.Remove(key,out var portrait))RestorePortrait(portrait);
            }
            catch(Exception e){Note("mimic-private-portrait-close-error",e.Message);}
            finally
            {
                // Hiding must still run if restoring art throws: a Show guard
                // alone cannot close private information already on screen.
                try{if(Native.UnityAlive(view.Popup))Native.Invoke(Native.M("PopupBase","Hide"),view.Popup);}
                catch(Exception e){Note("mimic-private-popup-close-error",e.Message);}
                finally
                {
                    // Hide does not prove that native rows were cleared. Keep a
                    // late Show blocked until real native ResetPopup starts reuse.
                    PopupViews.Remove(key);BlockedPopups.Add(key);
                }
            }
        }
        static void ClosePrivatePopups()
        {
            foreach(var view in new List<PopupView>(PopupViews.Values))
                if(view.Actual&&view.Public is null)ClosePrivatePopup(view);
        }
        public static void PopupHiding(object __instance)
        {
            MimicAbilityTags.Remove(__instance);
            long key=Native.Pointer(__instance);
            if(PopupViews.Remove(key,out var view)&&view.Actual)BlockedPopups.Add(key);
            // Retire ownership before native Hide can destroy its source renderer.
            // A later context reset must not render from the closed popup's source.
            try{if(PopupPortraits.Remove(key,out var portrait))RestorePortrait(portrait);}
            catch(Exception e){Note("mimic-popup-hide-restore-error",e.Message);}
        }
        static Portrait? ReadPortrait(object? container,object? data)
        {
            if(container is null||data is null)return null;
            var source=Native.Get(data,"source");return source is null?null:new Portrait(container,data,source);
        }
        static void RestorePortrait(Portrait portrait)
        {
            if(Native.UnityAlive(portrait.Source)&&Native.UnityAlive(portrait.Container)&&
               Native.Same(Native.Get(portrait.Data,"source"),portrait.Source)&&
               Native.Same(Native.Get(portrait.Data,"container"),portrait.Container))MimicInspectionControls.RestorePortrait(portrait.Data);
        }
        static void RestorePrivateBar(bool reflow=true)
        {
            var view=PrivateBar;PrivateBar=null;if(view is null)return;
            try
            {
                if(!Native.Same(Native.Get(view.Bar,"unit"),view.Unit))return;
                if(view.Portrait is not null&&Native.Same(Native.Get(view.Bar,"spriteDuplicationData"),view.Portrait.Data)&&
                   Native.Same(Native.Get(view.Bar,"duplicatedSprites"),view.Portrait.Container))RestorePortrait(view.Portrait);
                // Portrait refreshes and inspection toggles do not own action
                // controls. Keep their objects/slots until selection or native
                // button-list lifecycle actually changes.
                bool projected=ProjectUnit(view.Unit,out var p);SetBar(view.Bar,projected?ApparentLabel(p):
                    RecruitmentCatalog.Label(Native.EnumName(Native.Get(Native.Get(view.Unit,"UnitState")!,"type")!)));
            }
            catch(Exception e){Note("mimic-private-bar-reset-error",e.ToString());}
        }
        public static void BeforeBarRefresh(object __instance)
        {
            NativeSpyPresentation.BeforeBar(__instance);
            if(ReflowingBars.Contains(Native.Pointer(__instance)))return;
            // Some native refreshes update options without rebuilding portraits.
            // Restore while the old source is still owned, then let native reuse it.
            if(PrivateBar is not null&&Native.Same(PrivateBar.Bar,__instance))RestorePrivateBar(reflow:false);
        }
        static bool PrivateBarAuthorized(object unit)=>SelectedInteraction is not null&&
            Native.Same(SelectedUnit,unit)&&Authorized(SelectedInteraction,unit,out _,out _);
        static bool SameAppearance(LedgerAppearance? a,LedgerAppearance? b)
        {
            if(a is null||b is null)return a is null&&b is null;
            if(a.Form!=b.Form||a.TribeVisual!=b.TribeVisual||a.ClimateVisual!=b.ClimateVisual||a.BirthVisual!=b.BirthVisual||
               a.SourceColor!=b.SourceColor||a.Veteran!=b.Veteran||a.Concealment!=b.Concealment||a.GrowthTurns!=b.GrowthTurns||a.GrowthDueTurn!=b.GrowthDueTurn||
               a.Effects.Length!=b.Effects.Length||!SameAppearance(a.Passenger,b.Passenger))return false;
            for(int i=0;i<a.Effects.Length;i++)if(a.Effects[i]!=b.Effects[i])return false;
            return true;
        }
        static bool SameBarAppearance(BarView view,MimicProjection p,int ownerColor,object? container,object? data)
        {
            var old=view.Projection;var portrait=view.Portrait;
            return old.UnitId==p.UnitId&&old.NativeId==p.NativeId&&old.VariantId==p.VariantId&&old.Owner==p.Owner&&
                old.LogicalId==p.LogicalId&&old.ActualIsMimic==p.ActualIsMimic&&old.ActualMimicBoat==p.ActualMimicBoat&&old.ActualNativeId==p.ActualNativeId&&old.ApparentMimicBoat==p.ApparentMimicBoat&&
                Native.Same(old.State,p.State)&&view.OwnerColor==ownerColor&&SameAppearance(old.Appearance,p.Appearance)&&
                portrait is not null&&Native.Same(portrait.Container,container)&&Native.Same(portrait.Data,data)&&
                Native.Same(portrait.Source,data is null?null:Native.Get(data,"source"));
        }
        static void ApplyBarView(object bar,object unit,MimicProjection p)
        {
            if(p.Exposed)
            {
                if(PrivateBar is not null)RestorePrivateBar(reflow:false);
                NativeSpyPresentation.ApplyBar(bar,unit,p);SetBar(bar,p.ActualLabel);return;
            }
            bool authorized=PrivateBarAuthorized(unit),actual=Actual(p,out _);
            if(authorized)
            {
                NativeSpyPresentation.RemoveBar(bar);
                var owner=Native.Get(unit,"Owner")??throw new InvalidOperationException("Mimic bar owner is missing.");
                bool ready=ArtReady(p,owner);
                int ownerColor=Convert.ToInt32(Native.Get(owner,"color"));
                var container=Native.Get(bar,"duplicatedSprites");var data=Native.Get(bar,"spriteDuplicationData");
                if(PrivateBar is null||!Native.Same(PrivateBar.Bar,bar)||!Native.Same(PrivateBar.Unit,unit)||PrivateBar.Actual!=actual||PrivateBar.ArtReady!=ready||
                   !SameBarAppearance(PrivateBar,p,ownerColor,container,data))
                {
                    RestorePrivateBar(reflow:false);
                    var portrait=ReadPortrait(Native.Get(bar,"duplicatedSprites"),Native.Get(bar,"spriteDuplicationData"));
                    // Latch the attempt before native preview callbacks. A bad
                    // asset must not create/destroy a fresh preview every frame.
                    // Selection, native refresh or a changed appearance retries.
                    var attempt=new BarView(bar,unit,portrait,actual,ready,p,ownerColor);PrivateBar=attempt;
                    try
                    {
                        if(portrait is not null)MimicDualPortrait.Apply(portrait.Container,portrait.Data,actual,owner,unit,p);
                    }
                    catch(Exception e)
                    {
                        if(ReferenceEquals(PrivateBar,attempt))PrivateBar=attempt with{Failed=true};
                        // Portrait failure must not tear down the action buttons
                        // and thereby provoke recursive layout/preview retries.
                        Note("mimic-private-bar-portrait-error",e.ToString());
                    }
                }
            }
            else
            {
                if(PrivateBar is not null&&Native.Same(PrivateBar.Bar,bar))RestorePrivateBar(reflow:false);
                if(NativeFoggerPresentation.CustomForm(p.NativeId))NativeSpyPresentation.ApplyBar(bar,unit,p,apparent:true);
                else NativeSpyPresentation.RemoveBar(bar);
            }
            SetBar(bar,actual?p.ActualLabel:ApparentLabel(p));
        }
        public static void BarReady(object __instance)
        {
            if(ReflowingBars.Contains(Native.Pointer(__instance)))return;
            try
            {
                var unit=Native.Get(__instance,"unit");
                if(!ProjectUnit(unit,out var p))
                {
                    NativeSpecialActionUi.Sync(__instance,unit);
                    MimicInspectionControls.Remove(__instance);MimicSmuggleUi.Remove(__instance);MimicRegroupUi.Remove(__instance);NativeApparentUpgradeUi.Remove(__instance);
                    if(unit is not null&&NativeSpyPresentation.TryProject(unit,out var spy)&&NativeFoggerPresentation.CustomActual(spy)){NativeSpyPresentation.ApplyBar(__instance,unit,spy);SetBar(__instance,spy.ActualLabel);}
                    return;
                }
                ApplyBarView(__instance,unit!,p);
                bool actual=Actual(p,out _);var interaction=SelectedInteraction;
                if(!p.Exposed&&interaction is not null&&unit is not null&&CanContinueInspection(unit))
                    MimicInspectionControls.Show(__instance,unit,actual,()=>ToggleInspection(interaction,unit),!MimicRegroupUi.Choosing&&!NativeSpecialActionUi.Choosing,p.ActualLabel);
                else MimicInspectionControls.Remove(__instance);
                bool actions=unit is not null&&CanSmuggleView(unit);
                MimicSmuggleUi.Sync(__instance,unit,actions);
                MimicRegroupUi.Sync(__instance,unit,actions);
                NativeApparentUpgradeUi.Sync(__instance,unit,actions);
                NativeSpecialActionUi.Sync(__instance,unit);
                MimicActionMenu.Normalize(__instance);
            }
            catch(Exception e){Note("mimic-bar-view-error",e.ToString());}
        }
        // Inspection chooses portraits and information, never the action set.
        // Keep the exact selected owner/turn/visibility/idle/popup checks.
        internal static bool CanSmuggleView(object unit)=>SelectedInteraction is not null&&
            Native.Same(SelectedUnit,unit)&&CanInspect(SelectedInteraction,unit)&&ProjectUnit(unit,out var p)&&CommandAuthority(unit,p);
        static bool CommandAuthority(object unit,MimicProjection p)
        {
            var viewer=Native.Static("GameManager","LocalPlayer");
            return viewer is not null&&Native.Int(Native.Get(viewer,"Id"))==p.Owner&&Native.Int(Native.Get(p.State,"CurrentPlayer"))==p.Owner&&
                NativeRogueRules.CanCommand(p.State,Native.Get(unit,"UnitState")!,p.Owner);
        }
        internal static bool HasTruthAccess(MimicProjection p)=>Context(p,out _,requireTurn:false);
        internal static void ReflowBar(object bar)
        {
            long key=Native.Pointer(bar);
            if(ReflowingBars.Contains(key)||!(bool)Native.Get(bar,"IsShowing")!)return;
            ReflowingBars.Add(key);
            try
            {
                MimicActionMenu.Normalize(bar);
                // Native Show owns both parent visibility and PC vertical layout.
                // force is required after late button insertion into a visible bar.
                // It redraws native portraits, so project the current view afterward.
                Native.Invoke(Native.M("InteractionBar","Show","System.Boolean","System.Boolean"),bar,true,true);
                if(PrivateBar is not null&&Native.Same(PrivateBar.Bar,bar)&&!PrivateBar.Failed)PrivateBar=null;
                var unit=Native.Get(bar,"unit");
                if(!ProjectUnit(unit,out var p))return;
                ApplyBarView(bar,unit!,p);
                MimicActionMenu.Normalize(bar);
            }
            finally{ReflowingBars.Remove(key);}
        }
        static void SetBar(object bar,string header)
        {
            Native.Set(bar,"headerString",header);Native.Set(bar,"descriptionString","");
            var h=Native.Get(bar,"header");if(h is not null)Native.Set(h,"text",header);
            var d=Native.Get(bar,"description");if(d is not null)Native.Set(d,"text","");
        }
        public static void Tick()
        {
            try
            {
                // Warm the local player's native head address before the tech
                // tree opens; completion never writes to a retained UI object.
                var player=Native.Static("GameManager","LocalPlayer");
                try{if(player is not null&&Native.Static("GameManager","GameState") is not null)UnitArtAssets.Prepare(player);}
                catch(Exception e){Note("unit-head-prewarm-error",e.ToString());}
                MimicControls.Tick();NativeSpyPresentation.Tick();
                if(SelectedInteraction is not null&&SelectedUnit is not null)
                {
                    if(!Authorized(SelectedInteraction,SelectedUnit,out _,out var context))ResetInspection();
                    else {Inspection.Select(context,true);RefreshSelectedBar();}
                }
                MimicMapInspection.Tick();
                foreach(var view in new List<PopupView>(PopupViews.Values))
                {
                    if(!PopupProjectionCurrent(view)||view.Actual&&!PopupViewAuthorized(view))ClosePrivatePopup(view);
                    else RefreshPopupArt(view);
                }
            }
            catch(Exception e){Note("mimic-inspection-context-error",e.ToString());ResetInspection();}
        }
    }
}
