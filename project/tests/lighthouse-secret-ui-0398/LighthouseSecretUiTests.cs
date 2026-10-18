using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Deception.PolyModAdapter;
using UI=Deception.PolyModAdapter.NativeLighthouseSecretUi;
namespace Deception.Tests
{
 public static class LighthouseSecretUiTests
 {
  static int Checks;static readonly List<string> Cases=new();
  static void Check(bool yes,string why){Checks++;if(!yes)throw new Exception(why);}
  static void Test(string name,Action body){body();Cases.Add(name);}
  sealed record Scene(GameState State,ClientBase Client,PlayerState Player,MapRenderer Renderer,ClientInteraction Input,Tile Tile,TileData Data);
  static Scene Make()
  {
   FixtureReset.Reset();var s=new GameState();var p=new PlayerState();s.PlayerStates.Add(p);
   var d=new TileData();var t=new Tile{Data=d};s.Map.Tiles.Add(d.coordinates,d);var map=new MapRenderer();map.Tiles.Add(d.coordinates,t);
   var client=new ClientBase{GameState=s};GameManager.GameState=s;GameManager.Client=client;GameManager.LocalPlayer=p;MapRenderer.Current=map;
   NativeRegroupSession.States.Add(s);Touch();return new(s,client,p,map,new(),t,d);
  }
  static void Touch(UnityEngine.TouchPhase phase=UnityEngine.TouchPhase.Ended,int count=1)
  {PolytopiaInput.CurrentTouches=new();for(int i=0;i<count;i++)PolytopiaInput.CurrentTouches.Add(new(){phase=phase});}
  static void Select(Scene s,Tile? tile=null,bool run=true,Exception? failure=null)
  {
   UI.BeforeSelect(s.Input,tile??s.Tile,out var scope,run);if(run)s.Input.SelectTile(tile??s.Tile);UI.AfterSelect(run,scope);
   Check(ReferenceEquals(UI.SelectFinished(failure,scope),failure),"selection exception was changed");
  }
  static void Release(Scene s,Tile? tile=null,bool run=true,bool select=true,bool runSelect=true,Exception? failure=null,Exception? selectFailure=null,Action? during=null,int index=0)
  {
   UI.BeforeRelease(s.Input,tile??s.Tile,index,out var scope,run);
   if(run){s.Input.OnRelease(tile??s.Tile,index);if(select)Select(s,tile,runSelect,selectFailure);during?.Invoke();}
   UI.AfterRelease(run,scope);Check(ReferenceEquals(UI.ReleaseFinished(failure,scope),failure),"release exception was changed");
  }
  static void Click(Scene s,bool process=true,bool release=true,bool select=true,bool runSelect=true,bool newFrame=true,Exception? processFailure=null,Exception? releaseFailure=null,Exception? selectFailure=null,Action? during=null,int index=0,Tile? tile=null)
  {
   if(newFrame)UnityEngine.Time.frameCount++;
   UI.BeforeProcess(s.Input,out var scope,process);
   if(process){s.Input.Process();Release(s,tile,release,select,runSelect,releaseFailure,selectFailure,during,index);}
   UI.AfterProcess(process,scope);Check(ReferenceEquals(UI.ProcessFinished(processFailure,scope),processFailure),"process exception was changed");
  }
  static void Many(Scene s,int count){for(int i=0;i<count;i++)Click(s);}
  public static int Run()
  {
   try
   {
    Checks=0;Cases.Clear();
    Test("exactly_ten_completed_clicks_submit_one_validated_native_command",()=>
    {
     var s=Make();Many(s,9);Check(NativeLighthouseSecret.Calls.Count==0&&s.Client.Sent.Count==0,"early reward");Click(s);
     Check(NativeLighthouseSecret.Calls.Count==1&&s.Client.Sent.Count==1,"tenth click must dispatch once");
     var call=NativeLighthouseSecret.Calls.Single();var validated=NativeExchangeCommands.Validated.Single();
     Check(ReferenceEquals(call.State,s.State)&&call.Actor==1&&ReferenceEquals(call.Tile,s.Data),"offer lost exact state/player/tile");
     Check(ReferenceEquals(validated.State,s.State)&&validated.Dispatch&&ReferenceEquals(validated.Command,s.Client.Sent[0]),"native validity/dispatch mismatch");
     Check(s.Input.ProcessCalls==10&&s.Input.ReleaseCalls==10&&s.Input.SelectCalls==10,"native ordinary click suppressed");
     Check(s.Player.Currency==10,"UI wrote gameplay treasury");
    });
    Test("repeat_requires_ten_more_genuine_clicks",()=>{var s=Make();Many(s,19);Check(s.Client.Sent.Count==1,"early second reward");Click(s);Check(s.Client.Sent.Count==2&&s.Input.SelectCalls==20,"twentieth click did not preserve native click and dispatch once");});
    Test("ordinary_programmatic_selection_and_release_never_count",()=>{var s=Make();for(int i=0;i<20;i++){Select(s);Release(s);}Check(s.Client.Sent.Count==0&&NativeLighthouseSecret.Calls.Count==0,"programmatic event advanced counter");Many(s,9);Select(s);Check(s.Client.Sent.Count==0,"programmatic focus became tenth click");Click(s);Check(s.Client.Sent.Count==1,"programmatic focus damaged real sequence");});
    Test("different_visible_lighthouse_starts_its_own_sequence",()=>{var s=Make();Many(s,9);var d=new TileData{coordinates=new(8,9)};var t=new Tile{Data=d};s.State.Map.Tiles.Add(d.coordinates,d);s.Renderer.Tiles.Add(d.coordinates,t);Click(s,tile:t);Check(s.Client.Sent.Count==0,"mixed lighthouse count");for(int i=0;i<9;i++)Click(s,tile:t);Check(s.Client.Sent.Count==1&&ReferenceEquals(NativeLighthouseSecret.Calls[0].Tile,d),"second lighthouse sequence");});
    Test("ordinary_non_lighthouse_click_resets_sequence",()=>{var s=Make();Many(s,9);s.Data.IsLighthouse=false;Click(s);s.Data.IsLighthouse=true;Many(s,9);Check(s.Client.Sent.Count==0,"non-lighthouse did not reset");Click(s);Check(s.Client.Sent.Count==1,"sequence did not recover");});
    foreach(string disabled in new[]{"unregistered","disabled","unadopted","dispatch","client-not-ready","replay","spectating","recap","waiting","processing","renderer-dead","renderer-hidden","renderer-unready","ended","offturn","wrong-viewer","stale-player","dead-player","outside-window","drag","camera-blocked","popup","regroup","special-target","handoff","was-over-ui","pointer-over-ui","invalid-tap","tap-error","canceled","multiple-touches","wrong-index","tile-dead","tile-hidden-object","tile-fog","unexplored","not-lighthouse","no-native-tasks","no-native-lighthouse","stale-map-tile","stale-render-tile","missing-client","missing-player","missing-map"})
     Test("ineligible_tenth_click_is_inert_"+disabled,()=>
     {
      var s=Make();Many(s,9);int index=0;
      switch(disabled)
      {
       case"unregistered":RecruitmentHooks.Registered=false;break;case"disabled":NativeRegroupSession.Enabled=false;break;case"unadopted":NativeRegroupSession.States.Clear();break;case"dispatch":NativeRegroupSession.DispatchAllowed=false;break;
       case"client-not-ready":s.Client.IsReady=false;break;case"replay":s.Client.IsReplay=true;break;case"spectating":s.Client.IsSpectating=true;break;case"recap":s.Client.IsRecap=true;break;case"waiting":s.Client.IsWaitingForCommand=true;break;case"processing":s.Client.ActionManager.IsProcessing=true;break;
       case"renderer-dead":s.Renderer.Dead=true;break;case"renderer-hidden":s.Renderer.gameObject.activeInHierarchy=false;break;case"renderer-unready":s.Renderer.IsRendered=false;break;case"ended":s.State.CurrentState=GameState.State.Ended;break;case"offturn":s.State.CurrentPlayer=2;break;case"wrong-viewer":GameManager.Viewing=false;break;case"stale-player":GameManager.LocalPlayer=new();break;case"dead-player":s.Player.Alive=false;break;
       case"outside-window":PolytopiaInput.OutsideWindow=true;break;case"drag":CameraController.IsDragging=true;break;case"camera-blocked":CameraController.IsBlocked=true;break;case"popup":PopupManager.PopupShowing=true;break;case"regroup":MimicRegroupUi.Choosing=true;break;case"special-target":NativeSpecialActionUi.Choosing=true;break;case"handoff":HotSeatOverlay.IsActive=true;break;case"was-over-ui":s.Input.wasDownOverUI=true;break;case"pointer-over-ui":PolytopiaInput.OverUi=true;break;
       case"invalid-tap":s.Input.ValidTap=false;break;case"tap-error":s.Input.ThrowOnTap=true;break;case"canceled":Touch(UnityEngine.TouchPhase.Canceled);break;case"multiple-touches":Touch(count:2);break;case"wrong-index":index=1;break;
       case"tile-dead":s.Tile.Dead=true;break;case"tile-hidden-object":s.Tile.gameObject.activeInHierarchy=false;break;case"tile-fog":s.Tile.IsHidden=true;break;case"unexplored":s.Data.Explored.Clear();break;case"not-lighthouse":s.Data.IsLighthouse=false;break;case"no-native-tasks":ExploreLightHouseTask.UseTasks=false;break;case"no-native-lighthouse":ExploreLightHouseTask.AllowTile=false;break;
       case"stale-map-tile":s.State.Map.Tiles[s.Data.coordinates]=new();break;case"stale-render-tile":s.Renderer.Tiles[s.Data.coordinates]=new();break;case"missing-client":GameManager.Client=null;break;case"missing-player":GameManager.LocalPlayer=null;break;case"missing-map":MapRenderer.Current=null;break;
      }
      Click(s,index:index);Check(s.Client.Sent.Count==0&&NativeLighthouseSecret.Calls.Count==0,"invalid physical input dispatched "+disabled);
     });
    foreach(string boundary in new[]{"process-suppressed","release-suppressed","selection-suppressed","no-selection","process-exception","release-exception","selection-exception"})Test("original_completion_required_"+boundary,()=>
    {var s=Make();Many(s,9);var failure=new Exception("native failure");Click(s,process:boundary!="process-suppressed",release:boundary!="release-suppressed",select:boundary!="no-selection",runSelect:boundary!="selection-suppressed",processFailure:boundary=="process-exception"?failure:null,releaseFailure:boundary=="release-exception"?failure:null,selectFailure:boundary=="selection-exception"?failure:null);Check(s.Client.Sent.Count==0,"suppressed/faulted input counted");Many(s,9);Check(s.Client.Sent.Count==0,"failed gesture did not reset");Click(s);Check(s.Client.Sent.Count==1,"normal recognition did not recover");});
    Test("same_frame_callback_is_consumed_only_once",()=>{var s=Make();Many(s,9);Click(s,newFrame:false);Check(s.Client.Sent.Count==0,"duplicate callback counted twice");Click(s);Check(s.Client.Sent.Count==1,"fresh physical frame ignored");});
    Test("long_press_movement_and_stationary_callbacks_cannot_count",()=>{var s=Make();Many(s,9);foreach(var phase in new[]{UnityEngine.TouchPhase.Began,UnityEngine.TouchPhase.Moved,UnityEngine.TouchPhase.Stationary}){Touch(phase);Click(s);}Touch();Many(s,9);Check(s.Client.Sent.Count==0,"non-tap callback counted");Click(s);Check(s.Client.Sent.Count==1,"tap recovery");});
    Test("ui_release_with_no_native_selection_resets",()=>{var s=Make();Many(s,9);UnityEngine.Time.frameCount++;PolytopiaInput.OverUi=true;UI.BeforeProcess(s.Input,out var pump);s.Input.Process();UI.AfterProcess(true,pump);UI.ProcessFinished(null,pump);PolytopiaInput.OverUi=false;Many(s,9);Check(s.Client.Sent.Count==0,"UI click retained prior sequence");Click(s);Check(s.Client.Sent.Count==1,"UI reset recovery");});
    foreach(string change in new[]{"turn","command","map","state","client","renderer","player"})Test("context_replacement_resets_"+change,()=>
    {
     var s=Make();Many(s,9);
     switch(change)
     {
      case"turn":s.State.CurrentTurn++;break;case"command":s.State.CurrentCommand++;break;
      case"map":var map=new MapData();map.Tiles[s.Data.coordinates]=s.Data;s.State.Map=map;break;
      case"state":var state=new GameState{Map=s.State.Map};state.PlayerStates.Add(s.Player);NativeRegroupSession.States.Add(state);s.Client.GameState=state;GameManager.GameState=state;break;
      case"client":GameManager.Client=new(){GameState=s.State};break;
      case"renderer":var renderer=new MapRenderer();renderer.Tiles.Add(s.Data.coordinates,s.Tile);MapRenderer.Current=renderer;break;
      case"player":var player=new PlayerState();s.State.PlayerStates.Clear();s.State.PlayerStates.Add(player);GameManager.LocalPlayer=player;break;
     }
     Click(s);Check(NativeLighthouseSecret.Calls.Count==0,"context replacement borrowed nine clicks");Many(s,9);Check(NativeLighthouseSecret.Calls.Count==1,"new context did not count independently");
    });
    Test("native_selection_changed_state_before_completion_aborts",()=>{var s=Make();Many(s,9);Click(s,during:()=>s.State.CurrentCommand++);Check(s.Client.Sent.Count==0,"stale native selection submitted command");});
    Test("clear_during_native_callback_invalidates_all_pending_scopes",()=>{var s=Make();Many(s,9);Click(s,during:UI.Clear);Check(s.Client.Sent.Count==0,"cleared callback counted");Many(s,9);Check(s.Client.Sent.Count==0,"cleared generation leaked");Click(s);Check(s.Client.Sent.Count==1,"fresh scope after clear");});
    Test("nested_process_and_duplicate_release_fail_closed",()=>{var s=Make();Many(s,9);Click(s,during:()=>Click(s));Check(s.Client.Sent.Count==0,"nested pump borrowed authority");Many(s,9);Click(s,during:()=>Release(s));Check(s.Client.Sent.Count==0,"nested release borrowed authority");});
    Test("foreign_tile_selection_cannot_confirm_release",()=>{var s=Make();Many(s,9);Click(s,select:false,during:()=>Select(s,new Tile()));Check(s.Client.Sent.Count==0,"mismatched selected tile accepted");});
    Test("post_click_camera_animation_does_not_erase_valid_counter",()=>{var s=Make();Many(s,9);CameraController.IsBlocked=true;UI.Tick();CameraController.IsBlocked=false;Click(s);Check(s.Client.Sent.Count==1,"native camera animation reset genuine clicks");});
    Test("popup_and_session_clear_reset_between_clicks",()=>{var s=Make();Many(s,9);PopupManager.PopupShowing=true;UI.Tick();PopupManager.PopupShowing=false;Many(s,9);Check(s.Client.Sent.Count==0,"popup retained clicks");UI.Clear();Many(s,9);Check(s.Client.Sent.Count==0,"session clear retained clicks");Click(s);Check(s.Client.Sent.Count==1,"fresh sequence after clear");});
    foreach(string reject in new[]{"offer-false","null-offer","offer-throw","create-throw","valid-false","valid-throw","send-throw"})Test("dispatch_failure_does_not_retain_tenth_click_"+reject,()=>
    {
     var s=Make();switch(reject){case"offer-false":NativeLighthouseSecret.OfferAllowed=false;break;case"null-offer":NativeLighthouseSecret.NullOffer=true;break;case"offer-throw":NativeLighthouseSecret.ThrowOnOffer=true;break;case"create-throw":NativeExchangeCommands.ThrowOnCreate=true;break;case"valid-false":NativeExchangeCommands.AllowValid=false;break;case"valid-throw":NativeExchangeCommands.ThrowOnValid=true;break;case"send-throw":s.Client.ThrowOnSend=true;break;}
     Many(s,10);Check(s.Client.Sent.Count==0&&s.Player.Currency==10,"failure credited locally");
     if(reject is "valid-false" or "valid-throw")Check(NativeExchangeCommands.Forgotten.Count==1,"rejected command attachment leaked");
     NativeLighthouseSecret.OfferAllowed=true;NativeLighthouseSecret.NullOffer=false;NativeLighthouseSecret.ThrowOnOffer=false;NativeExchangeCommands.ThrowOnCreate=false;NativeExchangeCommands.AllowValid=true;NativeExchangeCommands.ThrowOnValid=false;s.Client.ThrowOnSend=false;
     Many(s,9);Check(s.Client.Sent.Count==0,"failed tenth click retried on next contact");Click(s);Check(s.Client.Sent.Count==1,"fresh full gesture cannot retry");
    });
    Test("installation_keeps_all_native_methods_and_adds_update_cleanup",()=>{Make();var h=new HarmonyLib.Harmony();UI.Install(h);Check(h.Hooks.OrderBy(x=>x).SequenceEqual(new[]{"ClientInteraction.OnRelease","ClientInteraction.Process","ClientInteraction.SelectTile","GameManager.Update"}),"exact hook set");Check(h.Patches.All(p=>p.Prefix is null||p.Prefix.Method.ReturnType==typeof(void)),"gesture suppresses original native method");});
    Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases.Count,assertions=Checks,tests=Cases,scope="Real input adapter and reflection binder with synthetic native objects; no native gameplay or network"}));return 0;
   }
   catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases.Count,assertions=Checks,tests=Cases,error=e.ToString()}));return 1;}
  }
 }
}
