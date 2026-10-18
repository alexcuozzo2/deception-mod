using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // UI-A05: first select the Mimic normally, then hold on that selected unit.
    // A short/repeated click keeps the game's normal unit/tile-selection meaning.
    // This adapter owns only an input gesture, never a Unit or gameplay field.
    internal static class MimicHoldInput
    {
        internal const double HoldSeconds=0.55;
        internal const double MaximumMovementPixels=12;
        const double MaximumMovementSquared=MaximumMovementPixels*MaximumMovementPixels;
        const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
        sealed class Press
        {
            public object Interaction=null!,Unit=null!,Tile=null!;
            public InspectionContext Context;
            public NativeIntelligenceOverlay.KnowledgeGestureContext KnowledgeContext;
            public bool Knowledge;
            public double Started,X,Y;
            public bool Mouse,Fired;
            public int Finger,Index;
        }
        readonly record struct Input(bool Mouse,bool Down,int Finger,int Index,double X,double Y,double MaximumDistance,object Position);
        static Press? Current;
        // Native Mac Process/OnRelease run without OnPress. Observe one physical
        // contact independently of the accepted hold: cancellation must not arm
        // again while that same mouse button/touch remains down.
        static bool InputObserved,ContactDown;
        static long PreviouslySelected;
        static readonly HashSet<string> Noted=new(StringComparer.Ordinal);
        static readonly Dictionary<string,string> LastTrace=new(StringComparer.Ordinal);
        static int TraceCount;

        static double Now()=>Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;
        static void Note(string kind,string detail)
        {if(Noted.Count<12&&Noted.Add(kind))Entry.Note(kind,detail);}
        // Diagnostic reads only: failures never change input recognition. The cap
        // survives selection/game resets so a failing native hook cannot flood logs.
        static void Trace(string phase,Func<string> read)
        {
            if(TraceCount>=100)return;
            try
            {
                string detail=read();
                if(LastTrace.TryGetValue(phase,out var old)&&old==detail)return;
                LastTrace[phase]=detail;
                Entry.Note("mimic-hold-trace",(++TraceCount)+" "+phase+" "+detail);
            }
            catch{/* Diagnostics must not alter production input or exceptions. */}
        }
        static long Id(object? value)=>value is null?0:Native.Pointer(value);
        static string InputText(Input input)=>"source="+(input.Mouse?"mouse":"touch")+" down="+input.Down+
            " finger="+input.Finger+" index="+input.Index+" xy="+input.X.ToString("0.##",CultureInfo.InvariantCulture)+","+
            input.Y.ToString("0.##",CultureInfo.InvariantCulture)+" distance2="+input.MaximumDistance.ToString("0.##",CultureInfo.InvariantCulture);
        static string Snapshot(object? interaction,object? tile=null)
        {
            var touches=Touches();
            string input=touches.Length>1?"multiple-touches="+touches.Length:InputText(touches.Length==1?TouchInput(touches[0],0):MouseInput());
            if(touches.Length==1)input+=" phase="+Native.Get(touches[0],"phase");
            return "interaction="+Id(interaction)+" tile="+Id(tile)+" selected="+Id(interaction is null?null:Native.Get(interaction,"selectedUnit"))+
                " tileUnit="+Id(tile is null?null:Native.Get(tile,"Unit"))+" armed="+(Current is not null)+" fired="+(Current?.Fired??false)+
                " ui="+(interaction is null?"absent":Native.Get(interaction,"wasDownOverUI"))+" popup="+Native.Static("PopupManager","PopupShowing")+
                " outside="+Native.Static("PolytopiaInput","OutsideWindow")+" dragging="+Native.Static("CameraController","IsDragging")+
                " blocked="+Native.Static("CameraController","IsBlocked")+" "+input;
        }

        public static void Install(Harmony h)
        {
            foreach(var method in new[]{
                Native.M("ClientInteraction","OnPress","Tile"),
                Native.M("ClientInteraction","Process"),
                Native.M("ClientInteraction","OnRelease","Tile","System.Int32"),
                Native.M("ClientInteraction","get_selectedUnit"),
                Native.M("ClientInteraction","get_wasDownOverUI"),
                Native.M("Tile","get_Unit"),
                Native.M("PolytopiaInput","get_CurrentTouches"),
                Native.M("PolytopiaInput","get_mousePosition"),
                Native.M("PolytopiaTouch","get_fingerId"),
                Native.M("PolytopiaTouch","get_phase"),
                Native.M("PolytopiaTouch","get_position"),
                Native.M("PolytopiaTouch","get_maxSquareDistance")}){_=method;}
            foreach(var method in new[]{
                Native.M("ClientInteraction","IsTouchValidLongpress","System.Int32"),
                Native.M("PolytopiaInput","IsPositionOverUIObject","UnityEngine.Vector2","System.Int32"),
                Native.M("PolytopiaInput","GetMouseButton","System.Int32"),
                Native.M("PolytopiaInput","get_OutsideWindow"),
                Native.M("CameraController","get_IsDragging"),
                Native.M("CameraController","get_IsBlocked"),
                Native.M("PopupManager","get_PopupShowing")})Native.RequireReturn(method,typeof(bool));
            // These are reads from a boxed native Vector2; no patch signature
            // includes a Unity value type. Both representations are explicit.
            foreach(string axis in new[]{"x","y"})ValidateAxis(Native.T("UnityEngine.Vector2"),axis);
            Native.RequireReturn(Native.M("ClientInteraction","get_currentTile"),Native.T("Tile"));
            Native.Hook(h,Native.M("ClientInteraction","OnPress","Tile"),typeof(MimicHoldInput),nameof(Pressed),null,nameof(PressFinished));
            Native.Hook(h,Native.M("ClientInteraction","Process"),typeof(MimicHoldInput),null,nameof(Process));
            Native.Hook(h,Native.M("ClientInteraction","OnRelease","Tile","System.Int32"),typeof(MimicHoldInput),nameof(Released));
        }

        public static void Clear()
        {
            if(Current is Press press)Trace("clear",()=>"unit="+Id(press.Unit)+" fired="+press.Fired);
            Current=null;PreviouslySelected=0;
        }
        public static void Pressed(object __instance,object __0)=>PressedAt(__instance,__0,Now());
        public static void Process(object __instance)
        {
            Trace("process-hook",()=>"interaction="+Id(__instance)+" armed="+(Current is not null));
            ProcessAt(__instance,Now());
        }
        public static Exception? PressFinished(Exception? __exception,object? __instance=null)
        {
            Trace("press-finished",()=>"exception="+(__exception?.GetType().Name??"none")+" "+Snapshot(__instance));
            if(__exception is not null)Clear();return __exception;
        }

        // Time-parameter seams execute this same production algorithm in fixtures,
        // without sleeps or replacing the game's clock/input methods.
        internal static void PressedAt(object interaction,object tile,double now,bool fromProcess=false)
        {
            Trace(fromProcess?"process-arm-attempt":"press-prefix",()=>Snapshot(interaction,tile));
            Clear();
            ContactDown=true;InputObserved=true;
            try
            {
                if(NativeSpecialActionUi.Choosing){Trace("press-reject",()=>"special targeting");return;}
                if(tile is null){Trace("press-reject",()=>"null-tile");return;}
                if(!SafeEnvironment(interaction)){Trace("press-reject",()=>"environment "+Snapshot(interaction,tile));return;}
                var unit=Native.Get(tile,"Unit");
                if(unit is null){Trace("press-reject",()=>"no-unit "+Snapshot(interaction,tile));return;}
                if(!Native.Same(Native.Get(interaction,"selectedUnit"),unit))
                {Trace("press-reject",()=>"selected-mismatch "+Snapshot(interaction,tile));return;}
                InspectionContext context=default;
                bool mimic=MimicPresentationUi.CanInspect(interaction,unit)&&MimicPresentationUi.TryInspectionContext(interaction,unit,out context);
                NativeIntelligenceOverlay.KnowledgeGestureContext knowledge=default;
                if(!mimic&&!NativeIntelligenceOverlay.TryGestureContext(interaction,unit,out knowledge))
                {Trace("press-reject",()=>"can-inspect-false "+Snapshot(interaction,tile));return;}
                if(!TryInput(out var input)){Trace("press-reject",()=>"multiple-inputs "+Snapshot(interaction,tile));return;}
                if(!input.Down||input.MaximumDistance>MaximumMovementSquared)
                {Trace("press-reject",()=>"input "+InputText(input));return;}
                if(OverUi(input)){Trace("press-reject",()=>"pointer-over-ui "+InputText(input));return;}
                Current=new Press{Interaction=interaction,Unit=unit,Tile=tile,Context=context,Knowledge=!mimic,KnowledgeContext=knowledge,Started=now,
                    X=input.X,Y=input.Y,Mouse=input.Mouse,Finger=input.Finger,Index=input.Index};
                Trace("armed",()=>"unit="+Id(unit)+" "+InputText(input));
                if(input.Mouse)Note("mimic-hold-mouse-fallback",
                    "No native touch record: hold uses the native left-button/position reads with a 0.55-second monotonic threshold and 12-pixel movement guard. Native Mac input order remains a playtest gate.");
            }
            catch(Exception e){Clear();Note("mimic-hold-press-error",e.ToString());}
        }

        internal static void ProcessAt(object interaction,double now)
        {
            try
            {
                ObserveProcessStart(interaction,now);
                var press=Current;
                if(press is null||!Native.Same(press.Interaction,interaction))return;
                if(!Eligible(press)){Trace("process-cancel",()=>"eligibility "+Snapshot(interaction,press.Tile));Clear();return;}
                if(!TryInput(out var input)){Trace("process-cancel",()=>"multiple-inputs");Clear();return;}
                if(!Matches(press,input)||!input.Down||Moved(press,input)||now<press.Started)
                {Trace("process-cancel",()=>"input elapsed="+(now-press.Started).ToString("0.###",CultureInfo.InvariantCulture)+" "+InputText(input));Clear();return;}
                if(OverUi(input)){Trace("process-cancel",()=>"pointer-over-ui "+InputText(input));Clear();return;}
                press.Index=input.Index;
                if(press.Fired||now-press.Started<HoldSeconds)return;
                // Touch input must also satisfy the game's own long-press rules.
                // With no touch record, calling that index-based predicate would
                // guess an index; the explicit mouse path above needs no guess.
                if(!input.Mouse&&!(bool)Native.Invoke(
                    Native.M("ClientInteraction","IsTouchValidLongpress","System.Int32"),interaction,input.Index)!)
                {Trace("native-longpress-false",()=>"unit="+Id(press.Unit)+" "+InputText(input));return;}
                bool toggled=press.Knowledge?NativeIntelligenceOverlay.TryToggle(interaction,press.Unit,press.KnowledgeContext):
                    MimicPresentationUi.ToggleInspection(interaction,press.Unit);
                if(!toggled)
                {Trace("process-cancel",()=>"toggle-false unit="+Id(press.Unit));Clear();return;}
                if(ReferenceEquals(Current,press))press.Fired=true;
                Trace("fired",()=>"unit="+Id(press.Unit)+" retained="+ReferenceEquals(Current,press)+" elapsed="+(now-press.Started).ToString("0.###",CultureInfo.InvariantCulture));
            }
            catch(Exception e){Clear();Note("mimic-hold-process-error",e.ToString());}
        }

        static void ObserveProcessStart(object interaction,double now)
        {
            var selected=Native.Get(interaction,"selectedUnit");
            long selectedId=Id(selected),previous=PreviouslySelected;
            PreviouslySelected=selectedId;
            if(!TryInput(out var input))
            {
                // A multi-touch gesture cannot become a new hold merely because
                // all but one finger lift. Require an observed contact-free frame.
                ContactDown=true;InputObserved=true;return;
            }
            bool began=InputObserved&&!ContactDown&&input.Down;
            ContactDown=input.Down;InputObserved=true;
            if(!began||Current is not null)return;
            if(selected is null||selectedId==0||selectedId!=previous)
            {Trace("process-start-reject",()=>"selection-changed-or-not-previously-selected "+InputText(input));return;}
            var tile=Native.Get(interaction,"currentTile");
            if(tile is null)
            {Trace("process-start-reject",()=>"no-current-tile "+InputText(input));return;}
            // Read native currentTile at the input edge; never substitute the
            // selected unit's coordinates for the pointer's native tile context.
            PressedAt(interaction,tile,now,fromProcess:true);
        }

        public static bool Released(object __instance,object __0,int __1)
        {
            Trace("release-prefix",()=>"index="+__1+" "+Snapshot(__instance,__0));
            var press=Current;
            if(press is null||!Native.Same(press.Interaction,__instance))return true;
            Clear();
            try
            {
                if(!press.Fired||!Native.Same(press.Tile,__0)||!Eligible(press))
                {Trace("release-native",()=>"not-fired-or-context-changed unit="+Id(press.Unit)+" fired="+press.Fired);return true;}
                // A touch may disappear before this callback. Without its current
                // position we cannot rule out a UI release over a stale map tile,
                // so let native input handle that unverified release.
                var touches=Touches();
                if(touches.Length>1)return true;
                if(!press.Mouse)
                {
                    if(touches.Length==0)
                    {Trace("release-native",()=>"missing-touch-position unit="+Id(press.Unit));return true;}
                    if(__1!=press.Index)return true;
                    if(touches.Length==1)
                    {
                        if(string.Equals(Native.Get(touches[0],"phase")?.ToString(),"Canceled",StringComparison.Ordinal))return true;
                        var input=TouchInput(touches[0],0);
                        if(!Matches(press,input)||Moved(press,input)||OverUi(input))return true;
                    }
                }
                else
                {
                    // A new touch replaces the mouse-only gesture; do not consume it.
                    if(touches.Length!=0)return true;
                    var input=MouseInput();
                    if(input.Down||Moved(press,input)||OverUi(input))return true;
                }
                // Holding selects the view only. Keep this matching release from
                // becoming a native click; the information icon owns opening stats.
                Trace("release-consumed",()=>"unit="+Id(press.Unit)+" view-toggle-only");
                return false;
            }
            catch(Exception e){Note("mimic-hold-release-error",e.ToString());return true;}
        }

        static bool Eligible(Press press)=>SafeEnvironment(press.Interaction)&&
            Native.Same(Native.Get(press.Interaction,"currentTile"),press.Tile)&&
            Native.Same(Native.Get(press.Tile,"Unit"),press.Unit)&&
            // Session preferences may survive turns, but a contact belongs only
            // to its original client/viewer/turn/unit authorization context.
            (press.Knowledge?
                NativeIntelligenceOverlay.TryGestureContext(press.Interaction,press.Unit,out var knowledge)&&knowledge==press.KnowledgeContext:
                MimicPresentationUi.TryInspectionContext(press.Interaction,press.Unit,out var context)&&context==press.Context&&
                MimicPresentationUi.CanInspect(press.Interaction,press.Unit));
        static bool OverUi(Input input)=>
            // Native UpdateMouseInput skips currentTile over UI, and clears
            // wasDownOverUI for Stationary touches. Query its actual position
            // helper instead; -1 is the native default (exclude no UI layer).
            (bool)Native.Invoke(Native.M("PolytopiaInput","IsPositionOverUIObject","UnityEngine.Vector2","System.Int32"),null,input.Position,-1)!;
        static bool SafeEnvironment(object interaction)=>
            !(bool)Native.Get(interaction,"wasDownOverUI")!&&
            !(bool)Native.Static("PopupManager","PopupShowing")!&&
            !(bool)Native.Static("PolytopiaInput","OutsideWindow")!&&
            !(bool)Native.Static("CameraController","IsDragging")!&&
            !(bool)Native.Static("CameraController","IsBlocked")!;
        static bool Matches(Press press,Input input)=>press.Mouse==input.Mouse&&
            (press.Mouse||press.Finger==input.Finger);
        static bool Moved(Press press,Input input)
        {
            double dx=input.X-press.X,dy=input.Y-press.Y;
            return input.MaximumDistance>MaximumMovementSquared||dx*dx+dy*dy>MaximumMovementSquared;
        }
        static object[] Touches()
        {
            var list=Native.Static("PolytopiaInput","CurrentTouches");
            return list is null?Array.Empty<object>():Native.Items(list).ToArray();
        }
        static bool TryInput(out Input input)
        {
            var touches=Touches();
            if(touches.Length>1){input=default;return false;}
            input=touches.Length==1?TouchInput(touches[0],0):MouseInput();
            return true;
        }
        static Input TouchInput(object touch,int index)
        {
            string phase=Native.Get(touch,"phase")?.ToString()??"";
            bool down=phase=="Began"||phase=="Moved"||phase=="Stationary";
            var position=Native.Get(touch,"position")??throw new InvalidOperationException("Native touch has no position");
            double distance=Convert.ToDouble(Native.Get(touch,"maxSquareDistance"),CultureInfo.InvariantCulture);
            if(double.IsNaN(distance)||double.IsInfinity(distance)||distance<0)
                throw new InvalidOperationException("Native touch movement distance is invalid");
            return new Input(false,down,Native.Int(Native.Get(touch,"fingerId")),index,
                Axis(position,"x"),Axis(position,"y"),distance,position);
        }
        static Input MouseInput()
        {
            var position=Native.Static("PolytopiaInput","mousePosition")??throw new InvalidOperationException("Native mouse has no position");
            return new Input(true,(bool)Native.Invoke(Native.M("PolytopiaInput","GetMouseButton","System.Int32"),null,0)!,0,0,
                Axis(position,"x"),Axis(position,"y"),0,position);
        }
        static void ValidateAxis(Type type,string axis)
        {
            var field=type.GetField(axis,Members);
            if(field is not null&&field.FieldType==typeof(float))return;
            var property=type.GetProperty(axis,Members);
            if(property is not null&&property.PropertyType==typeof(float)&&property.GetIndexParameters().Length==0&&property.GetMethod is not null)return;
            throw new MissingMemberException(type.FullName,"Single "+axis);
        }
        static double Axis(object position,string axis)
        {
            Type type=position.GetType();ValidateAxis(type,axis);
            var field=type.GetField(axis,Members);
            object? value=field is not null?field.GetValue(position):type.GetProperty(axis,Members)!.GetValue(position);
            double result=Convert.ToDouble(value,CultureInfo.InvariantCulture);
            if(double.IsNaN(result)||double.IsInfinity(result))throw new InvalidOperationException("Native pointer position is not finite");
            return result;
        }
    }
}
