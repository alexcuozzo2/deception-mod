// Runs production hold recognition with synthetic native input and deterministic
// monotonic timestamps. This cannot verify native Mac callback ordering.
using System;
using Deception.PolyModAdapter;

namespace Deception.Tests
{
    public static class HoldInputAdapterTests
    {
        static void Check(bool value,string message="Hold adapter assertion failed")
        {if(!value)throw new Exception(message);}
        static (ClientInteraction Interaction,Unit Unit,Tile Tile) Actor()
        {
            var state=new UnitState();GameManager.GameState.Units[state.id]=state;
            var unit=new Unit{UnitState=state};var tile=new Tile{Unit=unit};var interaction=new ClientInteraction{currentTile=tile};
            interaction.SelectUnit(unit);MimicPresentationUi.Selected(interaction,unit);MimicPresentationUi.BarReady(interaction.Bar);
            return(interaction,unit,tile);
        }
        static void Reset()
        {
            MimicHoldInput.Clear();MimicPresentationUi.Clear();MimicPresentationState.Clear();
            GameManager.GameState=new();GameManager.LocalPlayer=new();GameManager.Client=new();
            PopupManager.PopupShowing=false;PopupManager.FactoryCalls=0;PopupManager.ProductionCallbacks=true;
            PolytopiaInput.CurrentTouches=new();PolytopiaInput.MouseHeld=true;PolytopiaInput.mousePosition=new(20,30);
            PolytopiaInput.UIHitTest=null;
            PolytopiaInput.OutsideWindow=false;CameraController.IsDragging=false;CameraController.IsBlocked=false;
        }
        public static void Run(Action<string,Action> test)
        {
            test("UI-049-short-click-and-unselected-hold-retain-native-release",()=> {
                Reset();var a=Actor();MimicHoldInput.PressedAt(a.Interaction,a.Tile,5);
                MimicHoldInput.ProcessAt(a.Interaction,5.3);PolytopiaInput.MouseHeld=false;
                Check(MimicHoldInput.Released(a.Interaction,a.Tile,0));
                Check(a.Interaction.Bar.headerString=="Warrior (Disguise)"&&PopupManager.FactoryCalls==0);
                a.Interaction.selectedUnit=null;PolytopiaInput.MouseHeld=true;
                MimicHoldInput.PressedAt(a.Interaction,a.Tile,6);MimicHoldInput.ProcessAt(a.Interaction,7);
                PolytopiaInput.MouseHeld=false;Check(MimicHoldInput.Released(a.Interaction,a.Tile,0));
                Check(PopupManager.FactoryCalls==0&&a.Unit.UnitState.health==10);
            });
            test("UI-050-mouse-hold-toggles-once-and-consumes-only-matching-release",()=> {
                foreach(bool afterCommand in new[]{false,true})
                {
                    Reset();var a=Actor();if(afterCommand)MimicPresentationUi.CommandDispatch();
                    // A prior command cancels any in-flight gesture; native
                    // movement can leave this same physical unit selected.
                    MimicHoldInput.PressedAt(a.Interaction,a.Tile,10);
                    MimicHoldInput.ProcessAt(a.Interaction,10.54);Check(a.Interaction.Bar.headerString=="Warrior (Disguise)");
                    MimicHoldInput.ProcessAt(a.Interaction,10.56);Check(a.Interaction.Bar.headerString.Contains("Mimic"));
                    MimicHoldInput.ProcessAt(a.Interaction,11);MimicHoldInput.ProcessAt(a.Interaction,12);
                    Check(a.Interaction.Bar.headerString.Contains("Mimic")&&PopupManager.FactoryCalls==0);
                    PolytopiaInput.MouseHeld=false;Check(!MimicHoldInput.Released(a.Interaction,a.Tile,0));
                    Check(MimicHoldInput.Released(a.Interaction,a.Tile,0));
                    MimicPresentationUi.Tick();MimicPresentationUi.Tick();
                    Check(PopupManager.FactoryCalls==0&&!PopupManager.PopupShowing&&a.Interaction.Bar.headerString=="Mimic",
                        "Completed hold opened stats without the information icon");
                    // A separate hold switches back, with no synchronous or
                    // deferred popup in either direction.
                    PolytopiaInput.MouseHeld=true;MimicHoldInput.PressedAt(a.Interaction,a.Tile,20);
                    MimicHoldInput.ProcessAt(a.Interaction,20.56);Check(a.Interaction.Bar.headerString=="Warrior (Disguise)");
                    PolytopiaInput.MouseHeld=false;Check(!MimicHoldInput.Released(a.Interaction,a.Tile,0));
                    MimicPresentationUi.Tick();MimicPresentationUi.Tick();
                    Check(PopupManager.FactoryCalls==0&&!PopupManager.PopupShowing&&a.Interaction.Bar.headerString=="Warrior (Disguise)");
                    Check(a.Unit.UnitState.health==10&&!a.Unit.UnitState.moved&&!a.Unit.UnitState.attacked&&GameManager.LocalPlayer.Currency==15);
                }
            });
            test("UI-051-hold-cancellation-is-latched-for-entire-press",()=> {
                foreach(string change in new[]{"movement","drag","blocked","outside","multi","selected","viewer","processing","popup","ui"})
                {
                    Reset();var a=Actor();MimicHoldInput.PressedAt(a.Interaction,a.Tile,10);
                    if(change=="movement")PolytopiaInput.mousePosition=new(33,30);
                    if(change=="drag")CameraController.IsDragging=true;
                    if(change=="blocked")CameraController.IsBlocked=true;
                    if(change=="outside")PolytopiaInput.OutsideWindow=true;
                    if(change=="multi"){PolytopiaInput.CurrentTouches.Add(new(){fingerId=1});PolytopiaInput.CurrentTouches.Add(new(){fingerId=2});}
                    if(change=="selected")a.Interaction.selectedUnit=new();
                    if(change=="viewer")GameManager.LocalPlayer=new(){Id=2};
                    if(change=="processing")GameManager.Client.ActionManager.IsProcessing=true;
                    if(change=="popup")PopupManager.PopupShowing=true;
                    if(change=="ui")a.Interaction.wasDownOverUI=true;
                    MimicHoldInput.ProcessAt(a.Interaction,10.2);
                    PolytopiaInput.mousePosition=new(20,30);PolytopiaInput.CurrentTouches.Clear();PolytopiaInput.OutsideWindow=false;
                    CameraController.IsDragging=false;CameraController.IsBlocked=false;GameManager.LocalPlayer=new();
                    GameManager.Client.ActionManager.IsProcessing=false;PopupManager.PopupShowing=false;a.Interaction.wasDownOverUI=false;
                    a.Interaction.selectedUnit=a.Unit;
                    MimicHoldInput.ProcessAt(a.Interaction,11);PolytopiaInput.MouseHeld=false;
                    Check(MimicHoldInput.Released(a.Interaction,a.Tile,0),"Canceled hold consumed release: "+change);
                    Check(a.Interaction.Bar.headerString=="Warrior (Disguise)"&&PopupManager.FactoryCalls==0,"Canceled hold toggled: "+change);
                }
            });
            test("UI-075-stale-hold-context-cannot-toggle-after-turn",()=> {
                foreach(bool initiallyActual in new[]{false,true})
                {
                    Reset();var a=Actor();
                    if(initiallyActual)Check(MimicPresentationUi.ToggleInspection(a.Interaction,a.Unit));
                    MimicHoldInput.PressedAt(a.Interaction,a.Tile,10);
                    // The same owner/unit remains authorized, and the UI learns
                    // the new turn before native OnTurnStarted cleanup arrives.
                    GameManager.GameState.CurrentTurn++;MimicPresentationUi.Tick();
                    MimicHoldInput.ProcessAt(a.Interaction,10.2);
                    MimicHoldInput.ProcessAt(a.Interaction,11);MimicHoldInput.ProcessAt(a.Interaction,12);
                    PolytopiaInput.MouseHeld=false;Check(MimicHoldInput.Released(a.Interaction,a.Tile,0));
                    Check(MimicPresentationUi.MapView(a.Unit)==initiallyActual&&
                        a.Interaction.Bar.headerString==(initiallyActual?"Mimic":"Warrior (Disguise)"),
                        "Old contact changed the remembered view in the new turn");
                    // Cancellation remains latched until release. A new contact
                    // in the new context can still toggle the retained choice.
                    MimicHoldInput.ProcessAt(a.Interaction,13);
                    PolytopiaInput.MouseHeld=true;MimicHoldInput.ProcessAt(a.Interaction,14);
                    MimicHoldInput.ProcessAt(a.Interaction,14.6);
                    Check(MimicPresentationUi.MapView(a.Unit)==!initiallyActual);
                    PolytopiaInput.MouseHeld=false;Check(!MimicHoldInput.Released(a.Interaction,a.Tile,0));
                    Check(PopupManager.FactoryCalls==0&&!PopupManager.PopupShowing&&
                        a.Unit.UnitState.health==10&&!a.Unit.UnitState.moved&&!a.Unit.UnitState.attacked&&
                        GameManager.LocalPlayer.Currency==15);
                }
            });
            test("UI-052-touch-hold-uses-native-predicate-and-touch-index",()=> {
                Reset();var a=Actor();PolytopiaInput.MouseHeld=false;
                var touch=new PolytopiaTouch{fingerId=77,position=new(30,40)};PolytopiaInput.CurrentTouches.Add(touch);
                a.Interaction.ValidLongPress=false;MimicHoldInput.PressedAt(a.Interaction,a.Tile,1);
                MimicHoldInput.ProcessAt(a.Interaction,2);Check(a.Interaction.Bar.headerString=="Warrior (Disguise)");
                a.Interaction.ValidLongPress=true;MimicHoldInput.ProcessAt(a.Interaction,2.1);
                Check(a.Interaction.Bar.headerString.Contains("Mimic")&&a.Interaction.LastLongpressIndex==0);
                touch.phase=UnityEngine.TouchPhase.Ended;Check(!MimicHoldInput.Released(a.Interaction,a.Tile,0));
                MimicPresentationUi.Tick();MimicPresentationUi.Tick();
                Check(PopupManager.FactoryCalls==0&&!PopupManager.PopupShowing&&a.Interaction.Bar.headerString=="Mimic");
            });
            test("UI-053-touch-history-and-identity-reject-returned-pointer",()=> {
                foreach(string reason in new[]{"history","finger","canceled","clock","press-error"})
                {
                    Reset();var a=Actor();PolytopiaInput.MouseHeld=false;
                    var touch=new PolytopiaTouch{fingerId=77,position=new(30,40)};PolytopiaInput.CurrentTouches.Add(touch);
                    MimicHoldInput.PressedAt(a.Interaction,a.Tile,10);
                    if(reason=="history")touch.maxSquareDistance=145;
                    if(reason=="finger")touch.fingerId=78;
                    if(reason=="canceled")touch.phase=UnityEngine.TouchPhase.Canceled;
                    if(reason=="press-error")Check(MimicHoldInput.PressFinished(new Exception("native press failure"))!=null);
                    MimicHoldInput.ProcessAt(a.Interaction,reason=="clock"?9:10.6);
                    touch.maxSquareDistance=0;touch.fingerId=77;touch.phase=UnityEngine.TouchPhase.Stationary;
                    MimicHoldInput.ProcessAt(a.Interaction,11);touch.phase=UnityEngine.TouchPhase.Ended;
                    Check(MimicHoldInput.Released(a.Interaction,a.Tile,0));Check(a.Interaction.Bar.headerString=="Warrior (Disguise)"&&PopupManager.FactoryCalls==0);
                }
            });
            test("UI-060-native-process-hold-without-onpress",()=> {
                foreach(bool spent in new[]{false,true})
                {
                    Reset();var a=Actor();a.Interaction.currentTile=a.Tile;
                    a.Unit.UnitState.moved=spent;a.Unit.UnitState.attacked=spent;
                    // Real diagnostic run: Process and OnRelease occur, OnPress
                    // never occurs. Establish the previously selected idle unit.
                    PolytopiaInput.MouseHeld=false;MimicHoldInput.ProcessAt(a.Interaction,1);
                    var touch=new PolytopiaTouch{fingerId=0,phase=UnityEngine.TouchPhase.Began,position=new(30,40)};
                    PolytopiaInput.CurrentTouches.Add(touch);MimicHoldInput.ProcessAt(a.Interaction,2);
                    touch.phase=UnityEngine.TouchPhase.Stationary;
                    MimicHoldInput.ProcessAt(a.Interaction,2.54);Check(a.Interaction.Bar.headerString=="Warrior (Disguise)");
                    MimicHoldInput.ProcessAt(a.Interaction,2.56);Check(a.Interaction.Bar.headerString=="Mimic");
                    MimicHoldInput.ProcessAt(a.Interaction,3);Check(a.Interaction.Bar.headerString=="Mimic"&&PopupManager.FactoryCalls==0);
                    touch.phase=UnityEngine.TouchPhase.Ended;Check(!MimicHoldInput.Released(a.Interaction,a.Tile,0));
                    MimicPresentationUi.Tick();MimicPresentationUi.Tick();
                    Check(PopupManager.FactoryCalls==0&&!PopupManager.PopupShowing&&a.Interaction.Bar.headerString=="Mimic");
                    MimicHoldInput.ProcessAt(a.Interaction,3.2);touch.phase=UnityEngine.TouchPhase.Stationary;
                    MimicHoldInput.ProcessAt(a.Interaction,4);MimicHoldInput.ProcessAt(a.Interaction,4.56);
                    Check(a.Interaction.Bar.headerString=="Warrior (Disguise)");
                    touch.phase=UnityEngine.TouchPhase.Ended;Check(!MimicHoldInput.Released(a.Interaction,a.Tile,0));
                    MimicPresentationUi.Tick();MimicPresentationUi.Tick();
                    Check(PopupManager.FactoryCalls==0&&!PopupManager.PopupShowing&&a.Interaction.Bar.headerString=="Warrior (Disguise)");
                    Check(a.Unit.UnitState.health==10&&a.Unit.UnitState.moved==spent&&a.Unit.UnitState.attacked==spent&&GameManager.LocalPlayer.Currency==15);
                }
            });
            test("UI-061-process-hold-preserves-short-and-unselected-clicks",()=> {
                foreach(string reason in new[]{"short","elsewhere","unselected","selected-during-down"})
                {
                    Reset();var a=Actor();a.Interaction.currentTile=a.Tile;
                    if(reason=="unselected"||reason=="selected-during-down")a.Interaction.selectedUnit=null;
                    PolytopiaInput.MouseHeld=false;MimicHoldInput.ProcessAt(a.Interaction,1);
                    if(reason=="selected-during-down")a.Interaction.selectedUnit=a.Unit;
                    if(reason=="elsewhere")a.Interaction.currentTile=new Tile();
                    var touch=new PolytopiaTouch{fingerId=0,phase=UnityEngine.TouchPhase.Began,position=new(30,40)};
                    PolytopiaInput.CurrentTouches.Add(touch);MimicHoldInput.ProcessAt(a.Interaction,2);
                    // Selection/hit changes later in this contact cannot turn an
                    // ordinary native click into an inspection hold.
                    touch.phase=UnityEngine.TouchPhase.Stationary;a.Interaction.currentTile=a.Tile;a.Interaction.selectedUnit=a.Unit;
                    MimicHoldInput.ProcessAt(a.Interaction,reason=="short"?2.3:3);
                    touch.phase=UnityEngine.TouchPhase.Ended;Check(MimicHoldInput.Released(a.Interaction,a.Tile,0),reason);
                    Check(a.Interaction.Bar.headerString=="Warrior (Disguise)"&&PopupManager.FactoryCalls==0,reason);
                }
            });
            test("UI-062-process-hold-cancellation-cannot-rearm-held-contact",()=> {
                foreach(string reason in new[]{"movement","command","selection","viewer","processing","ui","multi"})
                {
                    Reset();var a=Actor();a.Interaction.currentTile=a.Tile;
                    PolytopiaInput.MouseHeld=false;MimicHoldInput.ProcessAt(a.Interaction,1);
                    var touch=new PolytopiaTouch{fingerId=0,phase=UnityEngine.TouchPhase.Began,position=new(30,40)};
                    PolytopiaInput.CurrentTouches.Add(touch);MimicHoldInput.ProcessAt(a.Interaction,2);touch.phase=UnityEngine.TouchPhase.Stationary;
                    if(reason=="movement")touch.position=new(43,40);
                    if(reason=="command")MimicPresentationUi.CommandDispatch();
                    if(reason=="selection")a.Interaction.selectedUnit=null;
                    if(reason=="viewer")GameManager.LocalPlayer=new(){Id=2};
                    if(reason=="processing")GameManager.Client.ActionManager.IsProcessing=true;
                    if(reason=="ui")a.Interaction.wasDownOverUI=true;
                    if(reason=="multi")PolytopiaInput.CurrentTouches.Add(new(){fingerId=1});
                    MimicHoldInput.ProcessAt(a.Interaction,2.2);
                    touch.position=new(30,40);a.Interaction.selectedUnit=a.Unit;GameManager.LocalPlayer=new();
                    GameManager.Client.ActionManager.IsProcessing=false;a.Interaction.wasDownOverUI=false;
                    if(reason=="multi"){PolytopiaInput.CurrentTouches.Clear();PolytopiaInput.CurrentTouches.Add(touch);}
                    MimicHoldInput.ProcessAt(a.Interaction,3);MimicHoldInput.ProcessAt(a.Interaction,4);
                    touch.phase=UnityEngine.TouchPhase.Ended;Check(MimicHoldInput.Released(a.Interaction,a.Tile,0),reason);
                    Check(a.Interaction.Bar.headerString=="Warrior (Disguise)"&&PopupManager.FactoryCalls==0,reason);
                    // A genuinely new mouse contact after idle can inspect again.
                    PolytopiaInput.CurrentTouches.Clear();MimicHoldInput.ProcessAt(a.Interaction,5);
                    PolytopiaInput.MouseHeld=true;MimicHoldInput.ProcessAt(a.Interaction,6);
                    MimicHoldInput.ProcessAt(a.Interaction,6.6);Check(a.Interaction.Bar.headerString=="Mimic",reason+" next contact");
                    PolytopiaInput.MouseHeld=false;Check(!MimicHoldInput.Released(a.Interaction,a.Tile,0),reason+" next release");
                }
            });
            test("UI-064-ui-hold-and-stale-map-hit-never-double-toggle",()=> {
                foreach(bool mouse in new[]{false,true})
                {
                    Reset();var a=Actor();PolytopiaInput.MouseHeld=false;
                    MimicHoldInput.ProcessAt(a.Interaction,1);
                    var position=new UnityEngine.Vector2(494,213);
                    PolytopiaInput.UIHitTest=p=>p==position;
                    // The live native path skips hit-tile refresh over UI and
                    // leaves wasDownOverUI false on Stationary fake touches.
                    var touch=new PolytopiaTouch{fingerId=0,phase=UnityEngine.TouchPhase.Stationary,position=position};
                    if(mouse){PolytopiaInput.mousePosition=position;PolytopiaInput.MouseHeld=true;}
                    else PolytopiaInput.CurrentTouches.Add(touch);
                    a.Interaction.wasDownOverUI=false;
                    MimicHoldInput.ProcessAt(a.Interaction,2);MimicHoldInput.ProcessAt(a.Interaction,2.8);
                    Check(a.Interaction.Bar.headerString=="Warrior (Disguise)"&&PopupManager.FactoryCalls==0,"UI contact became map hold");
                    Check(PolytopiaInput.LastUIHitPosition==position&&PolytopiaInput.LastUIExcludeLayer==-1);
                    touch.phase=UnityEngine.TouchPhase.Ended;PolytopiaInput.MouseHeld=false;
                    Check(MimicHoldInput.Released(a.Interaction,a.Tile,0));
                    // Only the native UI release callback toggles. The old bug
                    // fired the hold first, then this callback toggled back.
                    a.Interaction.Bar.buttons[0].OnClicked!.Click();
                    Check(a.Interaction.Bar.headerString=="Mimic"&&PopupManager.FactoryCalls==0);
                    Check(a.Unit.UnitState.health==10&&!a.Unit.UnitState.moved&&!a.Unit.UnitState.attacked&&GameManager.LocalPlayer.Currency==15);
                }
                foreach(string change in new[]{"tile","ui"})
                {
                    Reset();var a=Actor();PolytopiaInput.MouseHeld=false;MimicHoldInput.ProcessAt(a.Interaction,1);
                    var touch=new PolytopiaTouch{fingerId=0,phase=UnityEngine.TouchPhase.Stationary,position=new(30,40)};
                    PolytopiaInput.CurrentTouches.Add(touch);MimicHoldInput.ProcessAt(a.Interaction,2);
                    // An initially stale map hit resolves elsewhere next frame,
                    // or native UI becomes visible under a stationary pointer.
                    if(change=="tile")a.Interaction.currentTile=new Tile();
                    else PolytopiaInput.UIHitTest=_=>true;
                    MimicHoldInput.ProcessAt(a.Interaction,2.1);
                    a.Interaction.currentTile=a.Tile;PolytopiaInput.UIHitTest=null;
                    MimicHoldInput.ProcessAt(a.Interaction,2.8);
                    touch.phase=UnityEngine.TouchPhase.Ended;Check(MimicHoldInput.Released(a.Interaction,a.Tile,0));
                    Check(a.Interaction.Bar.headerString=="Warrior (Disguise)"&&PopupManager.FactoryCalls==0,change+" contact rearmed");
                }
                Reset();var missing=Actor();PolytopiaInput.MouseHeld=false;MimicHoldInput.ProcessAt(missing.Interaction,1);
                PolytopiaInput.CurrentTouches.Add(new(){fingerId=0,phase=UnityEngine.TouchPhase.Stationary,position=new(30,40)});
                MimicHoldInput.ProcessAt(missing.Interaction,2);MimicHoldInput.ProcessAt(missing.Interaction,2.8);
                Check(missing.Interaction.Bar.headerString=="Mimic");
                // A vanished release record gives no verified pointer position.
                // Keep the completed local view toggle, but do not consume native
                // release or invent a map-popup request from the stale tile.
                PolytopiaInput.CurrentTouches.Clear();PolytopiaInput.UIHitTest=_=>true;
                Check(MimicHoldInput.Released(missing.Interaction,missing.Tile,0));
                MimicPresentationUi.Tick();MimicPresentationUi.Tick();
                Check(PopupManager.FactoryCalls==0&&missing.Interaction.Bar.headerString=="Mimic");
                Check(missing.Unit.UnitState.health==10&&!missing.Unit.UnitState.moved&&!missing.Unit.UnitState.attacked&&GameManager.LocalPlayer.Currency==15);
                PolytopiaInput.UIHitTest=null;
            });
        }
    }
}
