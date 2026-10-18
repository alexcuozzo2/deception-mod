using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    internal static class RecruitmentWheelUi
    {
        const string UD="Polytopia.Data.UnitData", SKIN="PolytopiaBackendBase.Common.SkinType";
        static long TickNumber;
        [ThreadStatic] static int ChangingUi;
        sealed class Session
        {
            public object Bar=null!,State=null!,Player=null!,Coordinates=null!,Wheel=null!,Position=null!;
            public RecruitmentWheel Control=null!;
            public long Due,ShownAt,CancelAt;
            public List<object> CallbackRoots=new();
        }
        static Session? Active;
        static MethodInfo CreateButton=null!,Renderer=null!,SetRenderer=null!,ShowWheel=null!,HideWheel=null!,ClearButtons=null!;
        public static void Install(Harmony h)
        {
            CreateButton=Native.M("InteractionBar","CreateRoundBottomBarButton","System.String","System.Boolean");
            Renderer=Native.M("UIUtils","GetUIUnitRenderer",UD,"PlayerState",SKIN);
            SetRenderer=Native.M("UIRoundButton","SetUnitRenderer","UIUnitRenderer");
            ShowWheel=Native.M("QuickActions","Show","System.Boolean");HideWheel=Native.M("QuickActions","Hide","System.Boolean");
            ClearButtons=Native.M("InteractionBar","ClearButtons");
            foreach(var m in new[]{
                Native.M("QuickActions","AddButton","UIRoundButton"),Native.M("QuickActions","SortButtons"),
                Native.M("UIRoundButton","SetText","System.String"),
                Native.M("UIButtonBase","set_OnClicked","UIButtonBase+ButtonAction"),
                Native.M("UIButtonBase","set_OnClickedWhenDisabled","UIButtonBase+ButtonAction"),
                Native.M("PopupManager","GetBasicPopup"),Native.M("BasicPopup","SetHeader","System.String"),
                Native.M("BasicPopup","SetDescription","System.String"),Native.M("BasicPopup","SetMainButton","System.String","Il2CppSystem.Action"),
                Native.M("PopupBase","Show"),Native.M("PopupBase","Hide")}){_=m;}
            // Validate the exact generated event conversion without constructing a UI object.
            var callbackType=typeof(Action<,>).MakeGenericType(typeof(int),Native.T("UnityEngine.EventSystems.BaseEventData"));
            Native.M("UIButtonBase+ButtonAction","op_Implicit",callbackType.FullName!);
            Native.Hook(h,RecruitmentHooks.ClickedSpawn,typeof(RecruitmentWheelUi),nameof(InterceptSpawn));
            Native.Hook(h,Native.M("GameManager","Update"),typeof(RecruitmentWheelUi),null,nameof(Tick));
            Native.Hook(h,Native.M("InteractionBar","Show","System.Boolean","System.Boolean"),typeof(RecruitmentWheelUi),nameof(PreserveWheel));
            Native.Hook(h,Native.M("InteractionBar","Refresh"),typeof(RecruitmentWheelUi),nameof(PreserveWheel));
            Native.Hook(h,Native.M("InteractionBar","OnClose"),typeof(RecruitmentWheelUi),nameof(CloseRequested));
            Native.Hook(h,Native.M("InteractionBar","OnSelectionCleared"),typeof(RecruitmentWheelUi),nameof(SelectionCleared));
            Native.Hook(h,Native.M("InteractionBar","OnTileSelected","Tile"),typeof(RecruitmentWheelUi),nameof(TileSelected));
            foreach(var name in new[]{"Reset","DeInit","OnDestroy"})
                Native.Hook(h,Native.M("InteractionBar",name),typeof(RecruitmentWheelUi),nameof(SelectionCleared));
            Native.Hook(h,Native.M("InteractionBar","OnUnitSelected","Unit"),typeof(RecruitmentWheelUi),nameof(SelectionCleared));
            Native.Hook(h,Native.M("InteractionBar","GetPopulatedUnitPopup",UD),typeof(RecruitmentWheelUi),null,nameof(ConfirmationLabel));
            TechPresentation.Install(h);
        }
        static RecruitmentContext Context(object bar,object state,object player)
        {
            var c=Native.Get(bar,"coordinates")!;
            return new(Native.Pointer(state),Native.Pointer(player),Native.Int(Native.Get(player,"Id")),
                Convert.ToUInt32(Native.Get(state,"CurrentTurn")),Native.Int(Native.Get(c,"X")),Native.Int(Native.Get(c,"Y")));
        }
        static bool ValidContext(Session p)
        {
            var state=Native.Static("GameManager","GameState");var player=Native.Static("GameManager","LocalPlayer");
            if(state is null||player is null||!Native.Same(state,p.State)||!Native.Same(player,p.Player))return false;
            return p.Control.Matches(Context(p.Bar,state,player))&&
                Native.Int(Native.Get(state,"CurrentPlayer"))==p.Control.Context.PlayerId&&RecruitmentHooks.OwnsDeception(player);
        }
        static bool IsOurBar(object bar)=>Active is not null&&Native.Same(Active.Bar,bar);
        public static bool InterceptSpawn(object __instance,object __0,object __1)
        {
            if(!RecruitmentHooks.Registered)return true;
            string id=RecruitmentHooks.Id(__1);
            if(!RecruitmentCatalog.IsMenuId(id))return true;
            try
            {
                var state=Native.Static("GameManager","GameState");var player=Native.Static("GameManager","LocalPlayer");
                if(state is null||player is null||!RecruitmentHooks.OwnsDeception(player))return false;
                var command=Native.As(__0,"TrainCommand");
                if(Native.Int(Native.Get(command,"PlayerId"))!=Native.Int(Native.Get(player,"Id")))return false;
                var coords=Native.Get(command,"Coordinates")!;var context=Context(__instance,state,player);
                if(context.CityX!=Native.Int(Native.Get(coords,"X"))||context.CityY!=Native.Int(Native.Get(coords,"Y")))return false;
                if(Active is not null)return false; // Repeated clicks cannot create two wheels.
                var wheel=Native.Get(__instance,"quickActions")??throw new InvalidOperationException("Native city wheel is absent.");
                // Capture the displayed city's position NOW, before the native click
                // finishes hiding/clearing its wheel. Target is optional; a city wheel
                // can use a fixed world position without any follow-target Transform.
                var position=Native.Get(wheel,"Position")??throw new InvalidOperationException("Native city wheel has no world-position value.");
                Active=new Session{Bar=__instance,State=state,Player=player,Coordinates=coords,Wheel=wheel,Position=position,
                    Control=new RecruitmentWheel(context),Due=TickNumber+2};
                Entry.Note("mimic-wheel-queued","City "+context.CityX+","+context.CityY+"; no currency or command submitted.");
            }
            catch(Exception e){Entry.Note("mimic-wheel-open-error",e.ToString());Clear();}
            return false;
        }
        // Defer transitions out of the button's mouse-up dispatch. Otherwise the
        // old native OnUp/Hide can immediately close the newly opened wheel/popup.
        public static void Tick()
        {
            TickNumber++;var p=Active;if(p is null)return;
            try
            {
                if(!ValidContext(p)){Dismiss(p,"game/player/turn/city changed",false);return;}
                if(p.Control.Stage==RecruitmentStage.Opening&&TickNumber>=p.Due){Draw(p);return;}
                if(p.Control.Stage==RecruitmentStage.Choosing&&TickNumber>=p.Due){Confirm(p);return;}
                if(p.Control.Stage==RecruitmentStage.Returning&&TickNumber>=p.Due){Return(p);return;}
                if(p.Control.Stage==RecruitmentStage.Appearances)
                {
                    if(p.CancelAt>0&&TickNumber>=p.CancelAt){Dismiss(p,"native close / Escape",true);return;}
                    if(TickNumber>p.ShownAt+2&&!(bool)Native.Get(p.Wheel,"IsShowing")!)
                        Dismiss(p,"wheel dismissed",true);
                }
            }
            catch(Exception e){Entry.Note("mimic-wheel-error",e.ToString());Dismiss(p,"UI operation failed",true);}
        }
        static void Draw(Session p)
        {
            if(!ReferenceEquals(Active,p)||!ValidContext(p))return;
            var forms=RecruitmentEligibility.Choices(RecruitmentHooks.Forms.Values,RecruitmentHooks.CityAppearanceRoster(p.State,p.Player,p.Coordinates));
            if(!p.Control.Show(Context(p.Bar,p.State,p.Player),forms.Select(f=>f.VariantId)))
            {Dismiss(p,"no supported eligible forms",true);Message("No Mimic appearance","No currently unlocked native form has an adapter in this checkpoint.");return;}
            ChangingUi++;
            try
            {
                Native.Invoke(HideWheel,p.Wheel,true);Native.Invoke(ClearButtons,p.Bar);
                // Never round-trip a nullable Target through QuickActions.set_Target:
                // M1B failed in that setter before creating its first button. The
                // requested submenu belongs to a fixed city, not a moving unit.
                // Restore the captured world position, so camera/world-icon tracking
                // remains native, without depending on a follow-target Transform.
                Native.Set(p.Wheel,"Position",p.Position);
                Native.Set(p.Wheel,"KeepWorldPosition",true);
                Entry.Note("mimic-wheel-position","Restored captured city world position; Target setter not used.");
                foreach(var form in forms)
                {
                    var command=RecruitmentHooks.NewCommand(p.Player,p.Coordinates,form.VariantId);
                    bool valid=RecruitmentHooks.NativeValid(p.State,command,out var why);
                    var button=MakeButton(p,"Mimic\n"+form.Label,RecruitmentCatalog.Cost,()=>Choose(p,form.VariantId));
                    var renderer=Native.Invoke(Renderer,null,RecruitmentHooks.Data(p.State,form.NativeId),p.Player,Native.Get(p.Player,"skinType"))!;
                    Native.Invoke(SetRenderer,button,renderer);
                    Native.Set(button,"buttonActive",valid);Native.Set(button,"ButtonEnabled",valid);
                    Native.Set(button,"buttonExpensive",Native.Int(Native.Get(p.Player,"Currency"))<RecruitmentCatalog.Cost);
                    Entry.Note("mimic-wheel-option",form.NativeId+"; native-valid="+valid+"; reason="+why);
                }
                var back=MakeButton(p,"Back",-1f,()=>Back(p));
                MimicControls.ApplyBack(back);
                Native.Set(p.Bar,"headerString","Mimics");
                Native.Set(p.Bar,"descriptionString","Choose a disguise.");
                // The persistent two-line labels identify every option even when a
                // display mode does not render the information bar's heading.
                try
                {
                    var header=Native.Get(p.Bar,"header");if(header is not null)Native.Set(header,"text","Mimics");
                    var description=Native.Get(p.Bar,"description");if(description is not null)Native.Set(description,"text","Choose a disguise.");
                }
                catch(Exception e){Entry.Note("mimic-wheel-heading",e.Message+"; native button labels remain available.");}
                Native.Invoke(Native.M("QuickActions","SortButtons"),p.Wheel);
                Native.Invoke(Native.M("InteractionBar","SetupButtonNavigation"),p.Bar);
                Native.Invoke(ShowWheel,p.Wheel,false);
                // This wheel opens AFTER the initiating click's release, not on its down event.
                Native.Set(p.Wheel,"waitingForMouseUp",false);p.ShownAt=TickNumber;
                Entry.Note("mimic-wheel-open",forms.Length+" appearance buttons + Back. Native QuickActions wheel; no picker popup; no stars spent.");
            }
            finally{ChangingUi--;}
        }
        static object MakeButton(Session p,string text,float cost,Action action)
        {
            var button=Native.Invoke(CreateButton,p.Bar,text,false)!;
            Native.Invoke(Native.M("UIRoundButton","SetText","System.String"),button,text);
            var label=Native.Get(button,"Label");if(label is not null)Native.Set(label,"Text",text);
            Native.Set(button,"ShowLabel",true);Native.Set(button,"LabelShowOnHover",false);
            Native.Set(button,"ShowTextBackground",true);if(cost>=0)Native.Set(button,"Cost",cost);
            Native.Set(button,"buttonActive",true);Native.Set(button,"ButtonEnabled",true);
            // Replace click handlers only: keep native hover/navigation/animation handlers.
            var callback=Native.ButtonCallback(action);p.CallbackRoots.Add(callback);
            Native.Set(button,"OnClicked",callback);Native.Set(button,"OnClickedWhenDisabled",callback);
            var buttons=Native.Get(p.Wheel,"buttons")!;
            if(!Native.Items(buttons).Any(b=>Native.Same(b,button)))
                Native.Invoke(Native.M("QuickActions","AddButton","UIRoundButton"),p.Wheel,button);
            return button;
        }
        static void Choose(Session p,string variant)
        {
            if(!ReferenceEquals(Active,p)||!ValidContext(p))return;
            try
            {
                var eligible=RecruitmentEligibility.Choices(RecruitmentHooks.Forms.Values,RecruitmentHooks.CityAppearanceRoster(p.State,p.Player,p.Coordinates))
                    .Select(f=>f.VariantId).ToHashSet(StringComparer.Ordinal);
                if(!p.Control.Choose(Context(p.Bar,p.State,p.Player),variant,eligible))return;
                p.CancelAt=0;p.Due=TickNumber+2;
            }
            catch(Exception e){Entry.Note("mimic-wheel-selection-error",e.ToString());p.CancelAt=TickNumber+2;}
        }
        static void Back(Session p)
        {
            if(!ReferenceEquals(Active,p)||!ValidContext(p))return;
            if(p.Control.Back(Context(p.Bar,p.State,p.Player))){p.CancelAt=0;p.Due=TickNumber+2;}
        }
        static void Confirm(Session p)
        {
            if(!ReferenceEquals(Active,p)||!ValidContext(p))return;
            string id=p.Control.Selected??throw new InvalidOperationException("No selected appearance.");
            var command=RecruitmentHooks.NewCommand(p.Player,p.Coordinates,id);
            bool eligible=RecruitmentHooks.CheckEligibility(p.State,command,out var why);
            bool valid=eligible&&RecruitmentHooks.NativeValid(p.State,command,out why);
            if(!valid)
            {
                Entry.Note("mimic-confirm-rejected",id+": "+why);
                Dismiss(p,"native validation rejected selection",false);
                RestoreNative(p);
                Message("Cannot recruit Mimic",why);return;
            }
            Dismiss(p,"appearance selected",false);
            // No direct Execute, no star debit, no native ordinary-unit relabel.
            Native.Invoke(RecruitmentHooks.ClickedSpawn,p.Bar,command,RecruitmentHooks.Data(p.State,id));
            Entry.Note("mimic-confirm-open",id+"; eligible + native IsValid passed; normal recruitment handler invoked once.");
        }
        static void Return(Session p)
        {
            if(!ReferenceEquals(Active,p)||!ValidContext(p))return;
            Dismiss(p,"Back",false);
            RestoreNative(p);
            Entry.Note("mimic-wheel-back","Native city refreshed; wheel buttons="+Native.Get(Native.Get(p.Wheel,"buttons")!,"Count")+"; no command or stars spent.");
        }
        static void RestoreNative(Session p)
        {
            // The caller validated the city before dismissing its session. Native
            // Show only displays/layouts existing contents; ClearButtons removed
            // those contents, so Refresh must rebuild options and city headings
            // from the current native selection and roster first.
            Native.Invoke(Native.M("InteractionBar","Refresh"),p.Bar);
            Native.Invoke(Native.M("InteractionBar","Show","System.Boolean","System.Boolean"),p.Bar,false,true);
            // Some native display modes leave QuickActions hidden when Show(force) is used.
            if((bool)Native.Invoke(Native.M("QuickActions","HasButtons"),p.Wheel)!)Native.Invoke(ShowWheel,p.Wheel,false);
        }
        static void Dismiss(Session p,string why,bool closeNative)
        {
            if(!ReferenceEquals(Active,p))return;
            Active=null;p.Control.Dismiss();ChangingUi++;
            try
            {
                Native.Invoke(HideWheel,p.Wheel,true);Native.Invoke(ClearButtons,p.Bar);
                if(closeNative)Native.Invoke(Native.M("InteractionBar","OnClose"),p.Bar);
            }
            catch(Exception e){Entry.Note("mimic-wheel-cleanup",e.Message);}
            finally{p.CallbackRoots.Clear();ChangingUi--;}
            Entry.Note("mimic-wheel-closed",why+"; closing is not a purchase.");
        }
        public static void Clear(){if(Active is Session p)Dismiss(p,"registry/lifecycle reset",false);}
        public static bool PreserveWheel(object __instance)
        {
            if(ChangingUi>0||!IsOurBar(__instance))return true;
            try
            {
                if(!ValidContext(Active!)){Clear();return true;}
                // Keep ordinary automatic refresh from repopulating this wheel with real units.
                return false;
            }
            catch{Clear();return true;}
        }
        public static bool CloseRequested(object __instance)
        {
            if(ChangingUi>0||!IsOurBar(__instance))return true;
            var p=Active!;
            if(p.Control.Stage==RecruitmentStage.Appearances)p.CancelAt=TickNumber+2;
            // A click's normal close request must not consume the new UI during the
            // same dispatch. A selected form/Back cancels this pending dismissal.
            return false;
        }
        public static void SelectionCleared(object __instance)
        {if(ChangingUi==0&&IsOurBar(__instance))Clear();}
        public static void TileSelected(object __instance,object __0)
        {
            if(ChangingUi>0||!IsOurBar(__instance))return;
            var c=__0 is null?null:Native.Get(__0,"Coordinates");var p=Active!;
            if(c is null||Native.Int(Native.Get(c,"X"))!=p.Control.Context.CityX||Native.Int(Native.Get(c,"Y"))!=p.Control.Context.CityY)Clear();
        }
        static void Message(string title,string description)
        {
            var p=Native.Invoke(Native.M("PopupManager","GetBasicPopup"),null)!;
            Native.Invoke(Native.M("BasicPopup","SetHeader","System.String"),p,title);
            Native.Invoke(Native.M("BasicPopup","SetDescription","System.String"),p,description);
            Native.Invoke(Native.M("BasicPopup","SetMainButton","System.String","Il2CppSystem.Action"),p,"Close",Native.Callback(()=>Native.Invoke(Native.M("PopupBase","Hide"),p)));
            Native.Invoke(Native.M("PopupBase","Show"),p);
        }
        public static void ConfirmationLabel(object __0,object __result)
        {
            if(!RecruitmentHooks.Registered||__result is null||!RecruitmentHooks.Forms.TryGetValue(RecruitmentHooks.Id(__0),out var form))return;
            try
            {
                Native.Set(__result,"Header","Mimic — "+form.Label);
                Native.Set(__result,"Description","Recruit for "+RecruitmentCatalog.Cost+" star. Appears as "+form.Label+". Actual health: 1/1; no damaging attacks or retaliation. Select, then hold to inspect; or use Inspect Mimic in the bottom menu.");
            }
            catch(Exception e){Entry.Note("mimic-confirm-label-error",e.ToString());}
        }
    }
}
