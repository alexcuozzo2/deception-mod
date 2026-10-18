using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Deception.PolyModAdapter;
using Type=Polytopia.Data.UnitData.Type;
namespace Deception.Tests
{
 public static class SpyReadinessTests
 {
  static int Assertions;static readonly List<string> Cases=new();
  static void Check(bool ok,string why){Assertions++;if(!ok)throw new Exception(why);}
  static void Test(string id,Action body){try{body();Cases.Add(id);}catch(Exception e){throw new Exception(id,e);}}
  sealed class World
  {
   internal readonly GameState State=new();internal readonly Unit Visual=new();internal readonly MapRenderer Map=new();internal readonly PlayerState Player=new();
   internal readonly ClientBase Client=new();internal readonly MapRenderContext Context=new();internal readonly TileData Tile=new();
   internal UnitState Body=>Visual.UnitState;internal SkinVisualsReference Skin=>Visual.skinVisuals;
   internal World()
   {
    NativeSpyReadinessPresentation.Clear();NativeRegroupSession.Available=true;NativeRegroupSession.Reads=0;NativeBlackout.Blocked=false;RecruitmentHooks.Registered=true;
    GameManager.Viewing=true;Unit.OverlayThrows=false;UnitState.FakeFrozenRead=false;Entry.Notes.Clear();
    GameManager.GameState=State;GameManager.Client=Client;GameManager.LocalPlayer=Player;MapRenderer.Current=Map;Client.GameState=State;
    State.Units[Body.id]=Body;Tile.unit=Body;State.Map.Tiles[Body.coordinates]=Tile;Map.Units[Body.id]=Visual;
    Context.mapData=State.Map;Context.gld=State.GameLogicData;Context.unitRegistry=Map;
   }
   // Exercise the corrective sinks with native spent arguments by default;
   // the explicit passthrough case uses the normal native readiness calculation.
   internal void Render(bool native=false){Visual.NativeReady=native?null:false;Visual.UpdateObject(Context,new());}
   internal void Lit()=>Check(Skin.Outline&&Skin.OverlayStrength==0&&Skin.TeamStrength==0,"Remaining specialist action did not retain native ready colors/outline");
   internal void Dark()=>Check(!Skin.Outline&&Skin.OverlayStrength==0.5f&&Skin.TeamStrength==0.5f,"Rejected/spent Spy got native readiness glow");
   internal string Snapshot()=>JsonSerializer.Serialize(new{Body.id,Body.owner,Body.type,Body.health,Body.moved,Body.attacked,Body.coordinates,effects=Body.effects.ToArray(),State.CurrentPlayer,State.CurrentTurn,Tile.Accessible});
  }
  public static int Run()
  {
   try
   {
#if DECEPTION_WINDOWS_PORT_CANDIDATE
    Test("WINDOWS-ready-sink-bypasses-failing-tuple-wrapper",()=>{
     var w=new World();NativeWindowsHooks.Enabled=true;Unit.OverlayThrows=true;
     NativeSpyReadinessPresentation.BeforeUpdate(w.Visual,w.Context,out long scope);
     try {
      float overlay=4.79392E19f,team=0.5f;
      NativeSpyReadinessPresentation.BeforeColor(w.Skin,ref overlay,ref team);
      Check(overlay==0&&team==0,"Windows clear readiness must not read failing native tuple");
      w.Body.effects.Add(UnitEffect.Poisoned);overlay=4.79392E19f;
      NativeSpyReadinessPresentation.BeforeColor(w.Skin,ref overlay,ref team);
      Check(overlay==0.3f&&team==0,"Windows poison strength retained");
     } finally {NativeSpyReadinessPresentation.Finished(null,scope);NativeWindowsHooks.Enabled=false;Unit.OverlayThrows=false;}
    });
#endif
    Test("WINDOWS-ready-overlay-native-priority-all-effect-combinations",()=>{
     for(int mask=0;mask<128;mask++){
      var effects=new Il2CppSystem.Collections.Generic.List<UnitEffect>();
      for(int bit=0;bit<7;bit++)if((mask&(1<<bit))!=0)effects.Add((UnitEffect)bit);
      float expected=(mask&32)!=0?0.6f:(mask&1)!=0?0.5f:(mask&2)!=0?0.3f:(mask&64)!=0?0.4f:(mask&4)!=0?0.5f:0;
      Check(NativeSpyReadinessPresentation.WindowsReadyStrength(effects)==expected,"Native effect priority/strength mismatch "+mask);
     }
    });
    Test("SPY-GLOW-001-unmoved-specialist-ready-retains-native-render",()=>{var w=new World();string data=w.Snapshot();w.Render();w.Lit();Check(w.Snapshot()==data,"Rendering changed physical identity, HP, movement, attack or fog");});
    Test("SPY-GLOW-002-spent-specialist-action-dims",()=>{var w=new World();w.Render();w.Lit();w.Body.attacked=true;w.Render();w.Dark();});
    Test("SPY-GLOW-003-attack-denial-remains-during-and-after-render",()=>{
     var w=new World();w.Visual.DuringRender=()=>{Check(!w.Body.CanAttack()&&!UnitDataExtensions.CanAttackAnything(w.Body,1,w.State),"Render scope granted attack query");};w.Render();
     Check(!w.Body.CanAttack()&&!UnitDataExtensions.CanAttackAnything(w.Body,1,w.State),"Render cleanup changed attack ban");
     bool retaliate=true;NativeSpyActions.NoRetaliation(w.Body,ref retaliate);Check(!retaliate,"Retaliation ban changed");
    });
    Test("SPY-GLOW-004-native-status-color-preserved",()=>{var w=new World();w.Body.effects.Add(UnitEffect.Poisoned);w.Render();Check(w.Skin.Outline&&w.Skin.OverlayStrength==0.3f&&w.Skin.TeamStrength==0&&w.Skin.Color.r==0&&w.Skin.Color.g==1,"Ready correction erased native poison color");});
    Test("SPY-GLOW-005-fake-effect-read-cannot-change-real-readiness",()=>{var w=new World();UnitState.FakeFrozenRead=true;w.Render();w.Lit();w.Body.effects.Add(UnitEffect.Frozen);w.Render();w.Dark();});
    Test("SPY-GLOW-006-unmoved-native-glow-unchanged",()=>{var w=new World();w.Body.moved=false;w.Render(true);w.Lit();});
    Test("SPY-GLOW-014-Spymaid-native-ready-and-attack-denial",()=>{var w=new World();w.Body.type=Type.dcp_spymaid;w.Render();w.Lit();Check(!w.Body.CanAttack()&&!UnitDataExtensions.CanAttackAnything(w.Body,1,w.State),"Aquarion Spy acquired ordinary offense");w.Body.attacked=true;w.Render();w.Dark();});
    foreach(string reason in new[]{"moved","spent","frozen","opponent","idle-rogue","off-turn","finished","hidden","invisible","replay","spectator","client-not-ready","unbound","blackout","unexplored","leader","follower","dead-body","dead-visual","dead-map","dead-skin","viewer-not-viewing","context-map","context-data","context-registry","context-viewer","client-state","unit-table","tile-root","tile-coordinate","visual-table","registration"})
    Test("SPY-GLOW-guard-"+reason,()=>{
     var w=new World();switch(reason)
     {
      case "moved":w.Body.moved=true;break;case "spent":w.Body.attacked=true;break;case "frozen":w.Body.effects.Add(UnitEffect.Frozen);break;case "opponent":w.Body.owner=2;break;case "idle-rogue":w.Body.CanCommand=false;break;
      case "off-turn":w.State.CurrentPlayer=2;break;case "finished":w.State.CurrentState=GameState.State.Finished;break;case "hidden":w.Visual.Visible=false;break;case "invisible":w.Visual.Invisible=true;break;
      case "replay":w.Client.IsReplay=true;break;case "spectator":w.Client.IsSpectating=true;break;case "client-not-ready":w.Client.IsReady=false;break;case "unbound":NativeRegroupSession.Available=false;break;
      case "blackout":NativeBlackout.Blocked=true;break;case "unexplored":w.Tile.Accessible=false;break;case "leader":w.Body.Leader=true;break;case "follower":w.Body.Follower=true;break;
      case "dead-body":w.Body.health=0;break;case "dead-visual":w.Visual.Destroyed=true;break;case "dead-map":w.Map.Destroyed=true;break;case "dead-skin":w.Skin.Destroyed=true;break;
      case "viewer-not-viewing":GameManager.Viewing=false;break;case "context-map":w.Context.mapData=new();break;case "context-data":w.Context.gld=new();break;case "context-registry":w.Context.unitRegistry=new();break;
      case "context-viewer":w.Context.localPlayerId=2;break;case "client-state":w.Client.GameState=new();break;case "unit-table":w.State.Units[1]=new();break;case "tile-root":w.Tile.unit=new();break;
      case "tile-coordinate":w.Tile.coordinates=new(8,8);break;case "visual-table":w.Map.Units[1]=new();break;case "registration":RecruitmentHooks.Registered=false;break;
     }
     string before=w.Snapshot();if(reason=="registration")w.Body.attacked=true;w.Render();w.Dark();if(reason!="registration")Check(w.Snapshot()==before,"Denied renderer mutated native state");
    });
    Test("SPY-GLOW-007-preview-outline-suppression-remains",()=>{var w=new World();w.Context.hideOutlines=true;w.Render();Check(!w.Skin.Outline,"Native hideOutlines was overridden");});
    foreach(Type type in new[]{Type.warrior,Type.rider,Type.cloak,Type.dcp_mimic_spy,Type.raft})Test("SPY-GLOW-native-passthrough-"+type,()=>{
     var w=new World();w.Body.type=type;w.Body.attacked=true;w.Body.passengerUnit=new(){type=Type.dcp_spy};w.Render();w.Dark();Check(NativeRegroupSession.Reads==0,"Ordinary/native vessel/Spy-looking Mimic opened a readiness session scope");
    });
    foreach(string mutation in new[]{"body","skin","visual","client","state","map","player","moved","spent","frozen","blackout","owner","type","context"})Test("SPY-GLOW-inflight-"+mutation,()=>{
     var w=new World();NativeSpyReadinessPresentation.BeforeUpdate(w.Visual,w.Context,out var frame);var skin=w.Skin;
     switch(mutation){case "body":w.Visual.UnitState=new();break;case "skin":w.Visual.skinVisuals=new();break;case "visual":w.Map.Units[1]=new();break;case "client":GameManager.Client=new(){GameState=w.State};break;
      case "state":GameManager.GameState=new();break;case "map":MapRenderer.Current=new();break;case "player":GameManager.LocalPlayer=new();break;case "moved":w.Body.moved=true;break;case "spent":w.Body.attacked=true;break;case "frozen":w.Body.effects.Add(UnitEffect.Frozen);break;
      case "blackout":NativeBlackout.Blocked=true;break;case "owner":w.Body.owner=2;break;case "type":w.Body.type=Type.warrior;break;case "context":w.Context.hideOutlines=true;break;}
     bool show=false;NativeSpyReadinessPresentation.BeforeOutline(skin,ref show);Check(!show,"Stale/rebound render scope forced outline");NativeSpyReadinessPresentation.Finished(null,frame);
    });
    Test("SPY-GLOW-008-sinks-outside-scope-and-foreign-skin-unchanged",()=>{var w=new World();float a=0.5f,b=0.5f;bool glow=false;NativeSpyReadinessPresentation.BeforeColor(w.Skin,ref a,ref b);NativeSpyReadinessPresentation.BeforeOutline(w.Skin,ref glow);Check(a==0.5f&&b==0.5f&&!glow,"Unscoped sink was modified");NativeSpyReadinessPresentation.BeforeUpdate(w.Visual,w.Context,out var frame);NativeSpyReadinessPresentation.BeforeOutline(new SkinVisualsReference(),ref glow);Check(!glow,"Foreign pooled skin borrowed Spy scope");NativeSpyReadinessPresentation.Finished(null,frame);});
    Test("SPY-GLOW-009-nested-ordinary-render-cannot-borrow-outer-skin",()=>{var w=new World();NativeSpyReadinessPresentation.BeforeUpdate(w.Visual,w.Context,out var outer);var ordinary=new Unit{UnitState=new(){type=Type.warrior},skinVisuals=w.Skin};NativeSpyReadinessPresentation.BeforeUpdate(ordinary,w.Context,out var inner);bool glow=false;NativeSpyReadinessPresentation.BeforeOutline(w.Skin,ref glow);Check(!glow,"Nested unrelated render borrowed outer scope");NativeSpyReadinessPresentation.Finished(null,inner);NativeSpyReadinessPresentation.BeforeOutline(w.Skin,ref glow);Check(glow,"Nested cleanup did not restore valid outer scope");NativeSpyReadinessPresentation.Finished(null,outer);});
    Test("SPY-GLOW-010-exception-unwinds-scope",()=>{var w=new World();w.Visual.Throw=true;try{w.Render();throw new Exception("missing fixture exception");}catch(Exception e)when(e.Message=="native-render-failure"){}bool glow=false;NativeSpyReadinessPresentation.BeforeOutline(w.Skin,ref glow);Check(!glow,"Native exception retained render scope");w.Visual.Throw=false;w.Render();w.Lit();});
    Test("SPY-GLOW-011-clear-blocks-old-finalizer-resurrection",()=>{var w=new World();NativeSpyReadinessPresentation.BeforeUpdate(w.Visual,w.Context,out var a);NativeSpyReadinessPresentation.BeforeUpdate(w.Visual,w.Context,out var b);NativeSpyReadinessPresentation.Clear();var error=new Exception("native");Check(ReferenceEquals(NativeSpyReadinessPresentation.Finished(error,b),error),"Finalizer changed native exception");NativeSpyReadinessPresentation.Finished(null,a);bool glow=false;NativeSpyReadinessPresentation.BeforeOutline(w.Skin,ref glow);Check(!glow,"Clear resurrected scope");w.Render();w.Lit();});
    Test("SPY-GLOW-012-native-color-failure-keeps-existing-render-args",()=>{var w=new World();NativeSpyReadinessPresentation.BeforeUpdate(w.Visual,w.Context,out var f);Unit.OverlayThrows=true;float a=0.5f,b=0.5f;NativeSpyReadinessPresentation.BeforeColor(w.Skin,ref a,ref b);Check(a==0.5f&&b==0.5f&&Entry.Notes.Count==1,"Visual failure partially changed args or was unbounded");NativeSpyReadinessPresentation.BeforeColor(w.Skin,ref a,ref b);Check(Entry.Notes.Count==1,"Repeated failure logs each frame");NativeSpyReadinessPresentation.Finished(null,f);});
    foreach(Type type in new[]{Type.warrior,Type.rider,Type.cloak,Type.dcp_mimic_spy,Type.raft})Test("ROGUE-GLOW-ready-"+type,()=>{
     var w=new World();w.Body.type=type;w.Body.Rogue=true;w.Body.moved=true;w.Render();w.Lit();w.Body.attacked=true;w.Render();w.Dark();
    });
    foreach(string reason in new[]{"frozen","idle","offturn","blackout","foreign-root","leader"})Test("ROGUE-GLOW-denied-"+reason,()=>{
     var w=new World();w.Body.type=Type.warrior;w.Body.Rogue=true;
     switch(reason){case "frozen":w.Body.effects.Add(UnitEffect.Frozen);break;case "idle":w.Body.CanCommand=false;break;case "offturn":w.State.CurrentPlayer=2;break;case "blackout":NativeBlackout.Blocked=true;break;case "foreign-root":w.Tile.unit=new();break;case "leader":w.Body.Leader=true;break;}
     NativeSpyReadinessPresentation.BeforeUpdate(w.Visual,w.Context,out var f);bool glow=false;NativeSpyReadinessPresentation.BeforeOutline(w.Skin,ref glow);Check(!glow,"Ineligible Rogue was promoted to ready");NativeSpyReadinessPresentation.Finished(null,f);
    });
    foreach(Type type in new[]{Type.dcp_spy,Type.dcp_spymaid})Test("SPY-GLOW-no-Dash-native-after-move-"+type,()=>{var w=new World();w.Body.type=type;w.Body.moved=true;string before=w.Snapshot();w.Render(true);w.Dark();Check(before==w.Snapshot(),"Readiness restored an exhausted Spy allowance");});
    foreach(string stage in new[]{"ready","moved","first-shot","second-shot"})Test("FOGGER-GLOW-action-stage-"+stage,()=>{
     var w=new World();w.Body.type=Type.dcp_motherfogger;w.Body.moved=stage!="ready";w.Body.attacked=stage=="second-shot";if(stage is "ready" or "moved")w.Body.effects.Add(UnitEffect.DoubleReady);
     string before=w.Snapshot();w.Render();if(stage=="second-shot")w.Dark();else w.Lit();Check(before==w.Snapshot(),"Fogger presentation mutated native action allowance");
     bool promotion=true,retaliation=true;NativeSpyActions.NoPromotion(w.Body,ref promotion);NativeSpyActions.NoRetaliation(w.Body,ref retaliation);
     Check(!w.Body.CanAttack()&&!UnitDataExtensions.CanAttackAnything(w.Body,1,w.State)&&!promotion&&!retaliation,"Blackout range granted real offense or promotion");
    });
    foreach(string invalid in new[]{"frozen","foreign","offturn","unexplored","blackout","leader","stale-root","dead","spent"})Test("FOGGER-GLOW-guard-"+invalid,()=>{
     var w=new World();w.Body.type=Type.dcp_motherfogger;w.Body.moved=true;switch(invalid){case"frozen":w.Body.effects.Add(UnitEffect.Frozen);break;case"foreign":w.Body.owner=2;break;case"offturn":w.State.CurrentPlayer=2;break;case"unexplored":w.Tile.Accessible=false;break;case"blackout":NativeBlackout.Blocked=true;break;case"leader":w.Body.Leader=true;break;case"stale-root":w.Tile.unit=new();break;case"dead":w.Body.health=0;break;case"spent":w.Body.attacked=true;break;}w.Render();w.Dark();
    });
    Test("FOGGER-GLOW-native-ready-color-preserves-poison",()=>{var w=new World();w.Body.type=Type.dcp_motherfogger;w.Body.moved=true;w.Body.effects.Add(UnitEffect.Poisoned);w.Render();Check(w.Skin.Outline&&w.Skin.OverlayStrength==0.3f&&w.Skin.TeamStrength==0,"Fogger glow erased poison tint");});
    Test("SPY-GLOW-013-only-safe-render-hooks-registered",()=>{var h=new HarmonyLib.Harmony();NativeSpyReadinessPresentation.Install(h);Check(h.Hooks.Count==3&&h.Hooks.All(x=>x.Method.ReturnType==typeof(void)),"Unexpected/unsafe hook boundary");Check(h.Hooks.All(x=>x.Method.DeclaringType==typeof(Unit)||x.Method.DeclaringType==typeof(SkinVisualsRenderer)),"Readiness fix hooked backend action queries");Check(h.Hooks.Single(x=>x.Method.Name=="UpdateObject").Finalizer is not null,"Render scope lacks exception cleanup");});
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases.Count,assertions=Assertions,tests=Cases,nativeGame="NOT RUN",scope="Production render adapter, Spy attack restrictions and Spy network Node; synthetic native render sinks"}));return 0;
   }catch(Exception e){Console.WriteLine(e);return 1;}finally{NativeSpyReadinessPresentation.Clear();}
  }
 }
}
