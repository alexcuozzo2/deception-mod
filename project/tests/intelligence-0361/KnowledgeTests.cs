using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.PolyModAdapter;
using UnityEngine;
namespace Deception.Tests
{
    public static partial class KnowledgeTests
    {
        static int Assertions;static readonly List<string> Results=new();
        static void Check(bool yes,string why){Assertions++;if(!yes)throw new Exception(why);}
        static void Test(string id,Action action){try{if(Environment.GetEnvironmentVariable("DCP_CASE_PROGRESS") is string path)System.IO.File.AppendAllText(path,id+"\n");action();Results.Add(id);}catch(Exception e){throw new Exception(id,e);}}
        static GameObject? Root=>(GameObject?)typeof(NativeIntelligenceOverlay).GetField("Root",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null);
        static SpriteRenderer[] Marks()=>Root?.transform.Children.SelectMany(t=>t.gameObject.Components).OfType<SpriteRenderer>().Where(s=>!s.Destroyed&&s.gameObject!.Active).ToArray()??Array.Empty<SpriteRenderer>();
        static bool Shown=>Root is {Destroyed:false,Active:true};
        sealed class World
        {
            internal readonly GameState State=new();internal readonly ClientBase Client=new();internal readonly MapRenderer Map=new();
            internal readonly PlayerState Viewer=new(){Id=1},Enemy=new(){Id=2},Other=new(){Id=3,color=0xa04123};
            internal readonly Unit Unit=new();internal readonly ClientInteraction Interaction=new();
            internal Tile Selected=null!,Fogged=null!,Seen=null!,Unexplored=null!;
            internal World(bool enable=true)
            {
                NativeSurveillanceUi.Clear();NativeIntelligenceOverlay.Reset();MimicHoldInput.Clear();MimicRegroupUi.Choosing=false;MimicPresentationUi.Toggles=0;
                Entry.Notes.Clear();UnityEngine.Object.DestroyedObjects.Clear();GameObject.All.Clear();GameObject.FailComponent=GameObject.FailParent=false;
                PolytopiaInput.MouseHeld=PolytopiaInput.OverUi=PolytopiaInput.OutsideWindow=false;PolytopiaInput.CurrentTouches.Clear();PolytopiaInput.mousePosition=new(10,10);
                CameraController.IsDragging=CameraController.IsBlocked=false;PopupManager.PopupShowing=false;PopupManager.Opens=0;
                RecruitmentHooks.Registered=true;GameManager.Viewing=true;
                Client.GameState=State;State.Players[1]=Viewer;State.Players[2]=Enemy;State.Players[3]=Other;State.Units[Unit.UnitState.id]=Unit.UnitState;
                Map.Units[Unit.UnitState.id]=Unit;GameManager.Client=Client;GameManager.GameState=State;GameManager.LocalPlayer=Viewer;MapRenderer.Current=Map;
                Selected=Add(2,3,1,2,3);Selected.unit=Unit;Selected.Data.unit=Unit.UnitState;
                Fogged=Add(4,3,1);Seen=Add(5,3,1,2);Unexplored=Add(6,3,2);
                Interaction.selectedUnit=Unit;Interaction.currentTile=Selected;
                NativeIntelligenceOverlay.Selected(Interaction);
                if(enable)NativeIntelligenceOverlay.TryToggleControl(Interaction,ControlToken());
            }
            internal Tile Add(int x,int y,params byte[] explored)
            {
                var t=new Tile{Data=new(){coordinates=new(x,y),Explorers=explored.ToHashSet()}};
                t.fogOfWarRenderer.gameObject.layer=7;t.fogOfWarRenderer.transform.localPosition=new(x,y,4);
                t.fogOfWarRenderer.transform.localRotation=new(0,0,0.3f,0.9f);t.fogOfWarRenderer.transform.localScale=new(0.3f,0.4f,1);
                State.Map.Tiles.Add(t.Data);State.Map.Index[(x,y)]=t.Data;Map.Tiles[(x,y)]=t;return t;
            }
            internal string DataSnapshot()=>JsonSerializer.Serialize(new{Unit.UnitState.owner,Unit.UnitState.health,Unit.UnitState.Moved,Unit.UnitState.Attacked,State.CurrentTurn,
                tiles=State.Map.Tiles.Select(t=>new{pointer=t.Pointer.ToInt64(),t.coordinates,t.owner,unit=t.unit?.Pointer.ToInt64(),explored=t.Explorers.OrderBy(v=>v)}),
                fog=Map.Tiles.Values.Select(t=>new{t.IsHidden,sprite=t.fogOfWarRenderer.Sprite?.Pointer.ToInt64(),dead=t.fogOfWarRenderer.Sprite?.Destroyed,t.fogOfWarRenderer.SortingLayer,t.fogOfWarRenderer.SortingOrder})});
            internal NativeIntelligenceOverlay.ControlContext ControlToken(){if(!NativeIntelligenceOverlay.TryControlContext(Interaction,out var c))throw new Exception("No control context");return c;}
            internal NativeIntelligenceOverlay.KnowledgeGestureContext Token()
            {Check(NativeIntelligenceOverlay.TryGestureContext(Interaction,Unit,out var context),"Valid selected opponent did not authorize gesture");return context;}
            internal bool Toggle()=>NativeIntelligenceOverlay.TryToggle(Interaction,Unit,Token());
            internal void Select(){NativeIntelligenceOverlay.Selected(Interaction);}
            internal void Press(double time=10){PolytopiaInput.MouseHeld=true;MimicHoldInput.PressedAt(Interaction,Selected,time);}
            internal void Hold(double time=10.56)=>MimicHoldInput.ProcessAt(Interaction,time);
            internal bool Release(){PolytopiaInput.MouseHeld=false;return MimicHoldInput.Released(Interaction,Selected,0);}
        }
        static void SafeBorrowed(World w)
        {
            foreach(var tile in w.Map.Tiles.Values)
            {
                var fog=tile.fogOfWarRenderer;
                Check(!fog.Destroyed&&!fog.gameObject.Destroyed&&!fog.transform.Destroyed&&!fog.Sprite!.Destroyed&&!fog.Sprite.Texture.Destroyed,"Cleanup destroyed native fog or borrowed artwork");
            }
            Check(UnityEngine.Object.DestroyedObjects.All(o=>o is GameObject||o is Transform||o is SpriteRenderer),"Cleanup destroyed a shared sprite, texture or material");
        }
        public static int Run()
        {
            try
            {
                Test("KNOW-001-current-own-visible-opponent-fog-only",()=>{
                    var w=new World();var marks=Marks();Check(Shown&&marks.Length==1,"Unexpected fog projection");
                    Check(ReferenceEquals(marks[0].sprite,w.Fogged.fogOfWarRenderer.Sprite),"Overlay does not borrow the actual fog silhouette");
                    Check(marks[0].color==new Color(.75f+.25f*0x12/255f,.75f+.25f*0x56/255f,.75f+.25f*0xa0/255f,.55f),"Opponent tint or readable translucent alpha changed");
                    var t=marks[0].gameObject!.transform;var source=w.Fogged.fogOfWarRenderer.transform;
                    Check(t.localPosition==source.position&&t.localRotation==source.rotation&&t.localScale==source.lossyScale,"Fog position/rotation/scale does not match native visual");
                    Check(marks[0].sortingLayerID==9&&marks[0].sortingOrder==17&&marks[0].gameObject!.layer==7,"Fog render layer/order does not match native visual");
                });
                Test("KNOW-002-local-renderer-hidden-tile-does-not-abort-visible-marks",()=>{
                    var w=new World();var extra=w.Add(7,3,1);extra.IsHidden=true;NativeIntelligenceOverlay.Tick();
                    Check(Shown&&Marks().Length==1&&ReferenceEquals(Marks()[0].sprite,w.Fogged.fogOfWarRenderer.Sprite),"Hidden local tile leaked or removed safe marks");
                });
                Test("KNOW-003-subject-exploration-removes-mark",()=>{var w=new World();w.Fogged.Data.Explorers.Add(2);NativeIntelligenceOverlay.Tick();Check(Marks().Length==0,"Opponent-visible tile retained fog mark");});
                Test("KNOW-004-local-exploration-revocation-hides-mark",()=>{var w=new World();w.Fogged.Data.Explorers.Clear();NativeIntelligenceOverlay.Tick();Check(Marks().Length==0,"Own hidden area disclosed");});
                Test("KNOW-005-custom-color-and-native-tribe-fallback",()=>{var w=new World();w.Enemy.color=-1;NativeIntelligenceOverlay.Tick();Check(Marks()[0].color==new Color(.75f+.25f*0x7f/255f,.75f+.25f*0xa5/255f,.75f+.25f*0x33/255f,.55f),"Native tribe fallback color not used");});
                Test("KNOW-006-refresh-reuses-owned-markers-and-preserves-game-data",()=>{var w=new World();string before=w.DataSnapshot();int count=GameObject.All.Count;var root=Root;for(int i=0;i<50;i++)NativeIntelligenceOverlay.Tick();Check(GameObject.All.Count==count&&ReferenceEquals(Root,root),"Idle refresh creates unbounded render objects");Check(w.DataSnapshot()==before,"Knowledge changed native game/fog state");});
                Test("KNOW-007-toggle-off-on-is-visual-only",()=>{var w=new World();string before=w.DataSnapshot();Check(w.Toggle()&&!Shown&&Root is null,"Toggle did not hide");Check(w.Toggle()&&Shown&&Marks().Length==1,"Retoggle did not restore");Check(w.DataSnapshot()==before,"Toggle mutated game state");SafeBorrowed(w);});
                Test("KNOW-008-off-preference-persists-across-selection-and-turns",()=>{var w=new World();w.Toggle();NativeIntelligenceOverlay.Clear();w.State.CurrentTurn++;w.Select();Check(!Shown,"Selection/turn reset suppression");Check(w.Toggle()&&Shown,"Suppressed opponent cannot be retoggled");});
                Test("KNOW-009-preference-is-per-opponent",()=>{var w=new World();w.Toggle();w.Unit.UnitState.owner=3;w.Select();Check(!Shown,"New opponent was implicitly enabled");w.Unit.UnitState.owner=2;w.Select();Check(!Shown,"Returning to opponent lost suppression");});
                Test("KNOW-010-preference-is-per-local-viewer",()=>{var w=new World();w.Toggle();var viewer=new PlayerState{Id=3};GameManager.LocalPlayer=viewer;w.Fogged.Data.Explorers.Add(3);w.Select();Check(!Shown,"Hotseat viewer inherited another viewer opt-in");GameManager.LocalPlayer=w.Viewer;w.Select();Check(!Shown,"Original viewer preference was overwritten");});
                Test("KNOW-011-new-client-resets-suppression",()=>{var w=new World();w.Toggle();GameManager.Client=new(){GameState=w.State};w.Select();Check(!Shown,"A different native session inherited opt-in");});
                Test("KNOW-012-new-renderer-resets-suppression",()=>{var w=new World();w.Toggle();MapRenderer.Current=new(){Tiles=w.Map.Tiles,Units=w.Map.Units};w.Select();Check(!Shown,"A new map load inherited opt-in");});
                Test("KNOW-013-reset-cleans-owned-root-only",()=>{var w=new World();var old=Root!;NativeIntelligenceOverlay.Reset();Check(old.Destroyed&&Root is null&&!Shown,"Reset retained owned render tree");SafeBorrowed(w);w.Select();Check(!Shown&&w.Toggle()&&Shown,"New session was enabled without deliberate toggle or could not enable");});
                Test("KNOW-014-current-map-destruction-clears-preferences",()=>{var w=new World();w.Toggle();NativeIntelligenceOverlay.MapDestroying(w.Map);w.Select();Check(!Shown,"Current map destruction retained opt-in");SafeBorrowed(w);});
                Test("KNOW-015-unrelated-preview-map-destruction-is-ignored",()=>{var w=new World();var root=Root;NativeIntelligenceOverlay.MapDestroying(new MapRenderer());Check(ReferenceEquals(root,Root)&&Shown,"Preview destruction retired active gameplay overlay");});
                Test("KNOW-015b-temporarily-current-preview-map-preserves-preference",()=>{var w=new World();w.Toggle();var preview=new MapRenderer();MapRenderer.Current=preview;NativeIntelligenceOverlay.MapDestroying(preview);MapRenderer.Current=w.Map;w.Select();Check(!Shown,"Temporary Current preview reset gameplay preference");});
                Test("KNOW-016-portrait-duplication-excludes-overlay",()=>{var w=new World();NativeIntelligenceOverlay.BeforeDuplicate();Check(!Shown,"Overlay remains visible in portrait capture");NativeIntelligenceOverlay.Tick();Check(!Shown,"Update republished overlay during capture");var e=new Exception("portrait failure");Check(ReferenceEquals(NativeIntelligenceOverlay.AfterDuplicate(e),e),"Finalizer changed native exception");NativeIntelligenceOverlay.Tick();Check(Shown,"Balanced duplication failed to restore");SafeBorrowed(w);});
                Test("KNOW-017-pooled-tile-replacement-rebinds-owned-marker",()=>{var w=new World();var before=Marks()[0];var tile=new Tile{Data=w.Fogged.Data};w.Map.Tiles[(4,3)]=tile;NativeIntelligenceOverlay.Tick();Check(Shown&&ReferenceEquals(before,Marks()[0])&&ReferenceEquals(Marks()[0].sprite,tile.fogOfWarRenderer.Sprite),"Pooled tile did not rebind visual");});
                Test("KNOW-018-external-owned-root-retirement-recovers",()=>{var w=new World();var old=Root!;UnityEngine.Object.Destroy(old);NativeIntelligenceOverlay.Tick();Check(Shown&&!ReferenceEquals(old,Root)&&Marks().Length==1,"Destroyed own root kept stale child references");SafeBorrowed(w);});
                foreach(string failure in new[]{"component","parent","fog"})Test("KNOW-019-partial-allocation-"+failure,()=>{
                    var w=new World();NativeIntelligenceOverlay.Clear();if(failure=="component")GameObject.FailComponent=true;if(failure=="parent")GameObject.FailParent=true;if(failure=="fog")w.Fogged.fogOfWarRenderer.Sprite=null;
                    w.Select();Check(!Shown&&Root is null,"Partial render failure left a visible tree");Check(GameObject.All.Where(g=>g.Name.StartsWith("Intelligence ")||g.Name=="Deception Intelligence overlay").All(g=>g.Destroyed),"Partially allocated child leaked");
                    GameObject.FailComponent=GameObject.FailParent=false;
                });
                Test("KNOW-020-disabled-overlay-skips-full-tile-census",()=>{var w=new World();w.Toggle();int reads=w.Fogged.Data.ExploredReads;for(int i=0;i<50;i++)NativeIntelligenceOverlay.Tick();Check(w.Fogged.Data.ExploredReads==reads&&!Shown,"Suppressed overlay still scans map tiles");});
                Test("KNOW-021-map-census-bound-rejects-before-exploration",()=>{var w=new World();int reads=w.Fogged.Data.ExploredReads;while(w.State.Map.Tiles.Count<=4096)w.State.Map.Tiles.Add(new());NativeIntelligenceOverlay.Tick();Check(!Shown&&w.Fogged.Data.ExploredReads==reads,"Oversize census enumerated or published");});
                Test("KNOW-022-obsolete-coordinate-marker-is-retired",()=>{var w=new World();var old=Marks()[0].gameObject!;w.State.Map.Tiles.Remove(w.Fogged.Data);w.State.Map.Index.Remove((4,3));w.Map.Tiles.Remove((4,3));NativeIntelligenceOverlay.Tick();Check(old.Destroyed&&Marks().Length==0,"Removed map coordinate retained owned marker");});
                foreach(string change in new[]{"turn","state","unit","owner","invisible","research","popup","waiting","processing","choosing"})Test("KNOW-023-stale-gesture-"+change,()=>{
                    var w=new World();var token=w.Token();switch(change){case "turn":w.State.CurrentTurn++;break;case "state":GameManager.GameState=new();break;case "unit":w.Interaction.selectedUnit=new(){UnitState=w.Unit.UnitState};break;case "owner":w.Unit.UnitState.owner=3;break;case "invisible":w.Unit.Invisible=true;break;case "research":w.Viewer.Intelligence=false;break;case "popup":PopupManager.PopupShowing=true;break;case "waiting":w.Client.IsWaitingForCommand=true;break;case "processing":w.Client.ActionManager.IsProcessing=true;break;case "choosing":MimicRegroupUi.Choosing=true;break;}
                    Check(!NativeIntelligenceOverlay.TryToggle(w.Interaction,w.Unit,token),"Stale/unauthorized gesture toggled Knowledge");
                });
                Test("KNOW-024-mouse-hold-toggles-once-and-consumes-only-its-release",()=>{var w=new World();w.Press();w.Hold(10.54);Check(Shown,"Short hold toggled");w.Hold();Check(!Shown,"Long hold did not suppress");w.Hold(12);Check(!Shown,"Held contact toggled repeatedly");Check(!w.Release()&&MimicHoldInput.Released(w.Interaction,w.Selected,0),"Matching release not consumed exactly once");Check(w.Toggle()&&Shown&&PopupManager.Opens==0&&MimicPresentationUi.Toggles==0,"Enemy hold opened stats or entered Mimic view");});
                Test("KNOW-025-short-click-keeps-native-meaning",()=>{var w=new World();w.Press();w.Hold(10.2);Check(w.Release()&&Shown,"Short native click was intercepted");});
                foreach(string reason in new[]{"drag","pointer","ui","popup","multitouch","turn","enemy-hidden"})Test("KNOW-026-contact-cancels-"+reason,()=>{
                    var w=new World();w.Press();switch(reason){case "drag":CameraController.IsDragging=true;break;case "pointer":PolytopiaInput.mousePosition=new(40,10);break;case "ui":PolytopiaInput.OverUi=true;break;case "popup":PopupManager.PopupShowing=true;break;case "multitouch":PolytopiaInput.CurrentTouches.Add(new());PolytopiaInput.CurrentTouches.Add(new());break;case "turn":w.State.CurrentTurn++;break;case "enemy-hidden":w.Unit.Invisible=true;break;}
                    w.Hold();Check(w.Release(),"Canceled contact consumed native release");Check(MimicPresentationUi.Toggles==0,"Canceled enemy contact entered Mimic path");
                });
                Test("KNOW-027-mac-process-fallback-preserves-already-selected-rule",()=>{
                    var w=new World();MimicHoldInput.ProcessAt(w.Interaction,20);PolytopiaInput.MouseHeld=true;MimicHoldInput.ProcessAt(w.Interaction,21);MimicHoldInput.ProcessAt(w.Interaction,21.56);Check(!Shown,"Mac no-OnPress fallback did not toggle");Check(!w.Release(),"Mac fallback did not consume release");
                });
                Test("KNOW-028-touch-native-longpress-guard-and-release",()=>{
                    var w=new World();var touch=new PolytopiaTouch{fingerId=7,phase=TouchPhase.Stationary,position=new(10,10)};PolytopiaInput.CurrentTouches.Add(touch);w.Interaction.LongPress=false;
                    MimicHoldInput.PressedAt(w.Interaction,w.Selected,30);MimicHoldInput.ProcessAt(w.Interaction,31);Check(Shown,"Invalid native touch hold toggled");w.Interaction.LongPress=true;MimicHoldInput.ProcessAt(w.Interaction,31.1);Check(!Shown,"Valid touch hold did not toggle");touch.phase=TouchPhase.Ended;Check(!MimicHoldInput.Released(w.Interaction,w.Selected,0),"Valid touch release not consumed");
                });
                Test("KNOW-029-map-teardown-and-native-selection-hooks-registered",()=>{
                    var w=new World();var h=new HarmonyLib.Harmony();NativeIntelligenceOverlay.Install(h);MimicHoldInput.Install(h);
                    Check(h.Hooks.Any(x=>x.Target.DeclaringType==typeof(MapRenderer)&&x.Target.Name=="OnDestroy"&&x.Pre!.Method.Name=="MapDestroying"),"Map cleanup hook absent");
                    Check(h.Hooks.Any(x=>x.Target.DeclaringType==typeof(ClientBase)&&x.Target.Name=="EndSession"&&x.Pre!.Method.Name=="Reset"),"Session cleanup hook absent");
                    Check(h.Hooks.Count(x=>x.Target.DeclaringType==typeof(ClientInteraction)&&x.Target.Name=="OnRelease")==1,"Knowledge installed a competing release interceptor");
                });
                ControlTests();
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Results.Count,assertions=Assertions,results=Results,nativeGame="NOT RUN",scope="Production Knowledge overlay and shared hold adapter; fake native render/input boundaries"}));return 0;
            }
            catch(Exception e){string message="";for(Exception? x=e;x is not null;x=x.InnerException)message+=x.GetType().Name+": "+x.Message+"\n";if(Environment.GetEnvironmentVariable("DCP_CASE_PROGRESS") is string path)System.IO.File.AppendAllText(path,message);Console.WriteLine(message);return 1;}
            finally{NativeSurveillanceUi.Clear();NativeIntelligenceOverlay.Reset();MimicHoldInput.Clear();}
        }
    }
}
