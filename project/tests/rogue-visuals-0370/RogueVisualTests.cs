using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static class RogueVisualTests
 {
  static int Assertions;static readonly List<string> Tests=new();
  static void Check(bool ok,string why){Assertions++;if(!ok)throw new Exception(why);}
  static void Test(string id,Action run){try{run();Tests.Add(id);}catch(Exception e){throw new Exception(id,e);}}
  sealed class World
  {
   internal readonly GameState State=new();internal readonly PlayerState Commander=new(){Id=1,color=0x123456,tribe="imperius"},Owner=new(){Id=2,color=0xabcdef,tribe="elyrion"};
   internal readonly Unit Unit=new();internal readonly TileData Data=new();internal readonly Tile Tile=new();internal readonly UnitStatusDisplay Badge=new();internal readonly SkinVisualsTransientData Skin=new();
   internal World()
   {
    NativeRogueVisuals.Clear();RecruitmentHooks.Registered=true;NativeRegroupSession.Available=true;NativeRogueRules.Rows.Clear();Entry.Notes.Clear();
    GameManager.GameState=State;GameManager.LocalPlayer=Commander;GameManager.Client=new(){GameState=State};MapRenderer.Current=new();
    State.Players[1]=Commander;State.Players[2]=Owner;State.Units[Unit.UnitState.id]=Unit.UnitState;MapRenderer.Current.Units[Unit.UnitState.id]=Unit;
    Data.unit=Unit.UnitState;Data.coordinates=Unit.UnitState.coordinates;State.Map.Tiles[Data.coordinates]=Data;Tile.Data=Data;Badge.unit=Unit;Unit.statusDisplay=Badge;MapRenderer.Current.Tiles[Data.coordinates]=Tile;
    NativeRogueRules.Rows[Unit.UnitState.id]=new(1,Unit.UnitState.id,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0,Rogue:new(2,1,3));
    NativeGameStateEnvelope.Publish(State);
   }
   internal void Render(){Skin.SetupForUnit(State.GameLogicData,Unit.UnitState,Commander);Badge.RefreshColor();}
   internal void Kept()=>Check(ReferenceEquals(Skin.Applied,Owner)&&ReferenceEquals(Badge.Applied,Owner)&&Unit.UnitState.owner==1&&ReferenceEquals(Unit.Owner,Commander),"Cosmetics failed or leaked temporary query into real owner");
  }
  public static int Run()
  {
   try
   {
    Test("ROGUE-VIS-001-skin-and-badge-remember-owner",()=>{var w=new World();w.Render();w.Kept();Check(w.Skin.Applied!.tribe=="elyrion"&&w.Badge.Applied!.color==0xabcdef,"Remembered skin and color differ");});
    Test("ROGUE-VIS-002-native-movement-rebuild-keeps-owner",()=>{var w=new World();w.Render();w.Unit.UnitState.moved=true;w.Unit.UnitState.coordinates=new(8,2);w.Render();w.Kept();});
    Test("ROGUE-VIS-003-idle-rogue-still-public-owner-skin",()=>{var w=new World();w.State.CurrentTurn++;w.Unit.UnitState.CanCommand=false;w.Render();w.Kept();});
    Test("ROGUE-VIS-004-return-restores-ordinary-native-owner",()=>{var w=new World();w.Render();NativeRogueRules.Rows.Clear();w.Render();Check(ReferenceEquals(w.Skin.Applied,w.Commander)&&ReferenceEquals(w.Badge.Applied,w.Commander),"Returned unit retained cosmetic projection");});
    foreach(string reason in new[]{"unregistered","unbound","foreign-state","missing-unit","foreign-unit","dead","missing-owner"})Test("ROGUE-VIS-skin-guard-"+reason,()=>{
     var w=new World();switch(reason){case"unregistered":RecruitmentHooks.Registered=false;break;case"unbound":NativeRegroupSession.Available=false;break;case"foreign-state":GameManager.GameState=new();break;case"missing-unit":w.State.Units.Clear();break;case"foreign-unit":w.State.Units[1]=new();break;case"dead":w.Unit.UnitState.health=0;break;case"missing-owner":w.State.Players.Remove(2);break;}
     object owner=w.Commander;NativeRogueVisuals.BeforeSkin(w.State.GameLogicData,w.Unit.UnitState,ref owner);Check(ReferenceEquals(owner,w.Commander),"Invalid visual input projected owner");
    });
    Test("ROGUE-VIS-005-detached-mask-clone-does-not-borrow-owner",()=>{var w=new World();object p=w.Commander;NativeRogueVisuals.BeforeSkin(w.State.GameLogicData,new UnitState{id=1},ref p);Check(ReferenceEquals(p,w.Commander),"Detached visual clone was rebound to Rogue source");});
    Test("ROGUE-VIS-006-foreign-gld-cannot-change-skin",()=>{var w=new World();object p=w.Commander;NativeRogueVisuals.BeforeSkin(new Polytopia.Data.GameLogicData(),w.Unit.UnitState,ref p);Check(ReferenceEquals(p,w.Commander),"Preview GLD acquired match cosmetics");});
    Test("ROGUE-VIS-007-badge-scope-nesting",()=>{var w=new World();NativeRogueVisuals.BeforeStatusUnit(w.Unit,out var outer);Check(ReferenceEquals(w.Unit.Owner,w.Owner),"Outer badge scope missing");NativeRogueVisuals.BeforeStatusUnit(new Unit(),out var inner);Check(ReferenceEquals(w.Unit.Owner,w.Commander),"Nested foreign badge borrowed outer scope");NativeRogueVisuals.Finished(null,inner);Check(ReferenceEquals(w.Unit.Owner,w.Owner),"Nested cleanup lost outer");NativeRogueVisuals.Finished(null,outer);Check(ReferenceEquals(w.Unit.Owner,w.Commander),"Badge query persisted outside scope");});
    Test("ROGUE-VIS-008-badge-native-exception-unwinds",()=>{var w=new World();w.Badge.Throw=true;try{w.Badge.RefreshColor();}catch(Exception e)when(e.Message=="native-badge"){}Check(ReferenceEquals(w.Unit.Owner,w.Commander),"Thrown native badge kept public projection active");});
    Test("ROGUE-VIS-009-clear-prevents-finalizer-resurrection",()=>{var w=new World();NativeRogueVisuals.BeforeStatusUnit(w.Unit,out var a);NativeRogueVisuals.BeforeStatusUnit(w.Unit,out var b);NativeRogueVisuals.Clear();NativeRogueVisuals.Finished(null,b);NativeRogueVisuals.Finished(null,a);Check(ReferenceEquals(w.Unit.Owner,w.Commander),"Clear revived old projection");});
    Test("ROGUE-VIS-010-pooled-visual-is-not-recolored",()=>{var w=new World();NativeRogueVisuals.BeforeStatusUnit(w.Unit,out var f);MapRenderer.Current!.Units[1]=new();Check(ReferenceEquals(w.Unit.Owner,w.Commander),"Pooled former unit recolored");NativeRogueVisuals.Finished(null,f);});
    Test("ROGUE-VIS-011-fire-suppressed-without-changing-city-or-owner",()=>{var w=new World();w.Tile.Fire=true;w.Tile.SpawnFire(0,"normal");Check(!w.Tile.Fire&&w.Tile.Spawns==0&&w.Tile.Stops==1&&w.Data.City&&w.Unit.UnitState.owner==1,"Rogue retained capture flames or mutated native city");});
    foreach(string reason in new[]{"ordinary","empty","non-city","foreign-map","unbound","dead"})Test("ROGUE-VIS-fire-passthrough-"+reason,()=>{
     var w=new World();switch(reason){case"ordinary":NativeRogueRules.Rows.Clear();break;case"empty":w.Data.unit=null;break;case"non-city":w.Data.City=false;break;case"foreign-map":w.State.Map=new();break;case"unbound":NativeRegroupSession.Available=false;break;case"dead":w.Unit.UnitState.health=0;break;}
     w.Tile.SpawnFire(0,"native");Check(w.Tile.Fire&&w.Tile.Spawns==1&&w.Tile.Stops==0,"Ordinary/native fire was suppressed");
    });
    Test("ROGUE-VIS-013-inline-native-status-refresh-keeps-original-badge",()=>{var w=new World();w.Unit.UpdateObject();Check(w.Badge.Color.r==w.Owner.color&&ReferenceEquals(w.Badge.ReadyOwner,w.Commander),"Inline native color lost remembered owner or changed native readiness ownership");Check(ReferenceEquals(w.Unit.Owner,w.Commander),"Color scope leaked into true ownership");});
    Test("ROGUE-VIS-014-inline-color-only-scope-retains-unrelated-player",()=>{var w=new World();NativeRogueVisuals.BeforeStatusUpdate(w.Unit,out var f);object p=new PlayerState{Id=3};var exact=p;NativeRogueVisuals.BadgeColor(ref p,w.State);Check(ReferenceEquals(p,exact)&&ReferenceEquals(w.Unit.Owner,w.Commander),"Unrelated color or gameplay owner was replaced");NativeRogueVisuals.Finished(null,f);});
    Test("ROGUE-VIS-015-native-rainbow-city-fire-suppressed",()=>{var w=new World();w.Tile.Rainbow=true;w.Tile.SpawnFire(0,"green siege");w.Tile.SpawnRainbowFire(0);Check(!w.Tile.Fire&&!w.Tile.Rainbow&&w.Tile.Spawns==0&&w.Tile.RainbowSpawns==0,"Second native city flame path bypassed Rogue suppression");});
    Test("ROGUE-VIS-016-frame-publish-refreshes-acquired-rogue-immediately",()=>{var w=new World();w.Tile.Fire=w.Tile.Rainbow=true;w.Unit.skinVisuals.Outline=false;w.Unit.skinVisuals.TeamStrength=.5f;NativeRogueVisuals.Tick();Check(w.Unit.Updates==1&&w.Unit.skinVisuals.Outline&&w.Unit.skinVisuals.TeamStrength==0&&w.Badge.Color.r==w.Owner.color,"Completed acquisition did not refresh readiness and inline badge");Check(!w.Tile.Fire&&!w.Tile.Rainbow&&ReferenceEquals(w.Unit.LastSkin!.Applied,w.Owner)&&w.Unit.UnitState.owner==1,"Late old-native effects or changed true owner");});
    Test("ROGUE-VIS-017-no-repaint-or-full-ledger-work-on-idle-ticks",()=>{var w=new World();NativeRogueVisuals.Tick();for(int i=0;i<30;i++)NativeRogueVisuals.Tick();Check(w.Unit.Updates==1,"Idle frame repeatedly repainted unit");w.State.CurrentCommand++;w.Unit.UnitState.moved=true;NativeRogueVisuals.Tick();Check(w.Unit.Updates==2&&!w.Unit.skinVisuals.Outline&&w.Unit.skinVisuals.TeamStrength==.5f&&w.Unit.UnitState.moved&&!w.Unit.UnitState.attacked,"Moved no-Dash Spy stayed lit or its native flags changed");});
    Test("ROGUE-VIS-022-native-dash-rogue-keeps-remaining-action-glow",()=>{var w=new World();w.Unit.UnitState.type=Polytopia.Data.UnitData.Type.rider;w.Unit.UnitState.moved=true;NativeRogueVisuals.Tick();Check(w.Unit.Updates==1&&w.Unit.skinVisuals.Outline&&w.Unit.skinVisuals.TeamStrength==0&&w.Unit.UnitState.moved&&!w.Unit.UnitState.attacked,"Native Dash Rogue lost its remaining action glow or changed flags");w.State.CurrentCommand++;w.Unit.UnitState.attacked=true;NativeRogueVisuals.Tick();Check(w.Unit.Updates==2&&!w.Unit.skinVisuals.Outline&&w.Unit.skinVisuals.TeamStrength==.5f&&w.Unit.UnitState.attacked,"Spent native Dash Rogue stayed lit");});
    Test("ROGUE-VIS-018-defer-until-native-action-processing-finishes",()=>{var w=new World();GameManager.Client!.ActionManager.IsProcessing=true;NativeRogueVisuals.Tick();Check(w.Unit.Updates==0,"Visual reentered active command processing");GameManager.Client.ActionManager.IsProcessing=false;NativeRogueVisuals.Tick();Check(w.Unit.Updates==1,"Deferred publication refresh was lost");});
    Test("ROGUE-VIS-019-repeated-publication-and-return",()=>{var w=new World();NativeRogueVisuals.Tick();NativeRogueRules.Rows.Clear();w.Unit.UnitState.owner=2;NativeGameStateEnvelope.Publish(w.State);NativeRogueVisuals.Tick();Check(w.Unit.Updates==2&&ReferenceEquals(w.Unit.LastSkin!.Applied,w.Owner)&&!w.Unit.skinVisuals.Outline,"Rogue return retained commander readiness or failed native repaint");});
    foreach(string reason in new[]{"hidden","invisible","unexplored","foreign-body","foreign-tile","client-state","processing","waiting","replay","spectator","not-viewing","not-ready","unbound"})Test("ROGUE-VIS-tick-guard-"+reason,()=>{
     var w=new World();switch(reason){case"hidden":w.Unit.Visible=false;break;case"invisible":w.Unit.Invisible=true;break;case"unexplored":w.Data.Accessible=false;break;case"foreign-body":w.State.Units[1]=new();break;case"foreign-tile":w.Data.unit=new();break;case"client-state":GameManager.Client!.GameState=new();break;case"processing":GameManager.Client!.ActionManager.IsProcessing=true;break;case"waiting":GameManager.Client!.IsWaitingForCommand=true;break;case"replay":GameManager.Client!.IsReplay=true;break;case"spectator":GameManager.Client!.IsSpectating=true;break;case"not-viewing":GameManager.Viewing=false;break;case"not-ready":GameManager.Client!.IsReady=false;break;case"unbound":NativeRegroupSession.Available=false;break;}
     NativeRogueVisuals.Tick();Check(w.Unit.Updates==0,"Unauthorized frame repainted Rogue");GameManager.Viewing=true;
    });
    Test("ROGUE-VIS-020-native-repaint-exception-caught-and-bounded",()=>{var w=new World();w.Unit.Throw=true;NativeRogueVisuals.Tick();NativeRogueVisuals.Tick();Check(w.Unit.Updates==1&&Entry.Notes.Count==1&&ReferenceEquals(w.Unit.Owner,w.Commander),"Failed native repaint leaked scope or repeated each frame");});
    Test("ROGUE-VIS-021-clear-during-repaint-stops-old-work",()=>{var w=new World();w.Unit.Updated=()=>NativeRogueVisuals.Clear();NativeRogueVisuals.Tick();Check(ReferenceEquals(w.Unit.Owner,w.Commander)&&w.Tile.Stops==0&&w.Tile.RainbowStops==0,"Clear during native repaint resurrected cosmetic scope or touched old city");});
    Test("ROGUE-VIS-012-safe-only-cosmetic-hooks",()=>{var h=new HarmonyLib.Harmony();NativeRogueVisuals.Install(h);Check(h.Hooks.Count==9,"Unexpected hook count");Check(h.Hooks.All(x=>x.Method.ReturnType==typeof(void)||x.Method.ReturnType==typeof(PlayerState)||x.Method.ReturnType==typeof(UnityEngine.Color)),"Unsafe native value-return hook");Check(h.Hooks.All(x=>x.Method.DeclaringType!=typeof(UnitState)&&x.Method.DeclaringType!=typeof(GameState)),"Cosmetic fix hooked authoritative state");});
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Tests.Count,assertions=Assertions,tests=Tests,nativeGame="NOT RUN"}));return 0;
   }catch(Exception e){Console.WriteLine(e);return 1;}finally{NativeRogueVisuals.Clear();}
  }
 }
}
