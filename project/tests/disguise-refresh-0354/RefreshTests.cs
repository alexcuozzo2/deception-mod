using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Deception.PolyModAdapter;
using Type=Polytopia.Data.UnitData.Type;
namespace Deception.Tests
{
    public static class RefreshTests
    {
        static int Assertions,Cases;
        static void Check(bool condition,string reason){Assertions++;if(!condition)throw new Exception(reason);}
        sealed record Rig(GameState Game,MapRenderer Map,MapRenderContext Context,Tile Tile,Unit View,UnitState State);
        static Rig Make()
        {
            NativeDisguiseRefresh.Clear();Entry.Notes.Clear();NativeRegroupSession.IsReady=true;RecruitmentHooks.Registered=true;
            var game=new GameState();var map=new MapRenderer();GameManager.GameState=game;MapRenderer.Current=map;
            GameManager.Client=new();GameManager.LocalPlayer=new();MapRenderer.Callbacks=true;
            var state=new UnitState{id=9,type=Type.dcp_mimic_form_warrior,UnitData=new(){type=Type.dcp_mimic_form_warrior}};
            var tile=new Tile{Data=new(){unit=state}};var view=new Unit{Tile=tile,UnitState=state,UnitData=state.UnitData};tile.Unit=view;
            game.Units.Add(state.id,state);game.Map.Tiles.Add((0,0),tile.Data);map.Tiles.Add((0,0),tile);map.Units.Add(state.id,view);
            state.type=Type.dcp_mimic_form_polytaur;state.UnitData=new(){type=Type.dcp_mimic_form_polytaur};state.moved=true;state.attacked=true;
            return new(game,map,new(){mapData=game.Map,gld=game.GameLogicData,unitRegistry=map},tile,view,state);
        }
        static string StateFingerprint(Rig r)=>$"{r.State.Pointer}/{r.State.id}/{r.State.owner}/{r.State.type}/{r.State.UnitData.Pointer}/{r.State.coordinates.Pointer}/{r.State.coordinates.X}/{r.State.coordinates.Y}/{r.State.health}/{r.State.Investment}/{r.State.moved}/{r.State.attacked}/{r.State.Effects.GetHashCode()}/{GameManager.LocalPlayer!.Tribe}/{GameManager.LocalPlayer.Skin}/{GameManager.LocalPlayer.Color}";
        static void Case(Action test){Cases++;test();}
        public static int Run()
        {
            try
            {
                Case(()=>{var h=new HarmonyLib.Harmony();NativeDisguiseRefresh.Install(h);Check(h.Hooks.SequenceEqual(new[]{"Tile.RenderUnit","MapRenderer.OnDestroy"}),"Unexpected native hooks");});
                Case(()=>{
                    var r=Make();string before=StateFingerprint(r);r.Map.Selected=r.View;
                    MapRenderer.Callbacks=false;r.Tile.RenderUnit(r.Context);
                    Check(r.Tile.Unit==r.View&&r.View.UnitData.type==Type.dcp_mimic_form_warrior,"Did not reproduce native aliasing defect");
                    MapRenderer.Callbacks=true;r.Tile.RenderUnit(r.Context);
                    Check(r.View.DestroyCalls==1&&r.View.PoolReturns==1&&r.View.Destroyed,"Native visual was not retired exactly once");
                    Check(r.Tile.Unit!=r.View&&r.Tile.Unit!.UnitData.type==Type.dcp_mimic_form_polytaur,"Current native carrier was not regenerated");
                    Check(ReferenceEquals(r.Tile.Unit!.UnitState,r.State)&&ReferenceEquals(r.Game.Units[9],r.State)&&ReferenceEquals(r.Tile.Data.unit,r.State),"Body identity changed");
                    Check(r.Map.Selected is null,"Adapter restored selection during command");
                    Check(before==StateFingerprint(r),"Rendering changed authoritative state or owner appearance");
                    var created=r.Tile.Unit;for(int i=0;i<50;i++)r.Tile.RenderUnit(r.Context);
                    Check(r.Tile.Creates==1&&r.View.DestroyCalls==1&&created==r.Tile.Unit&&created!.DestroyCalls==0,"Normal refresh recreated current geometry repeatedly");
                });
                Case(()=>{
                    var r=Make();var partnerState=new UnitState{id=10,type=Type.polytaur,UnitData=new(){type=Type.polytaur},coordinates=new(0,0)};
                    var destination=new Tile{Coordinates=new(1,0),Data=new(){unit=r.State}};
                    var partner=new Unit{Tile=destination,UnitState=partnerState,UnitData=partnerState.UnitData};destination.Unit=partner;
                    r.Tile.Data.unit=partnerState;r.State.coordinates=new(1,0);r.Game.Units.Add(10,partnerState);
                    r.Game.Map.Tiles.Add((1,0),destination.Data);r.Map.Tiles.Add((1,0),destination);r.Map.Units.Add(10,partner);
                    string before=StateFingerprint(r);NativeDisguiseRefresh.BeforeTile(destination,r.Context);
                    Check(r.View.DestroyCalls==1&&partner.DestroyCalls==0,"Swap retired the destination partner instead of stale initiating visual");
                    Check(r.Tile.Unit is null&&destination.Unit==partner&&ReferenceEquals(r.Tile.Data.unit,partnerState),"Native retirement cleared wrong tile or real occupancy");
                    Check(before==StateFingerprint(r),"Swap refresh modified action/state");
                });
                foreach(bool reversed in new[]{false,true})Case(()=>{
                    var r=Make();var partnerState=new UnitState{id=10,type=Type.polytaur,UnitData=new(){type=Type.polytaur},coordinates=new(0,0)};
                    var destination=new Tile{Coordinates=new(1,0),Data=new(){unit=r.State}};
                    var partner=new Unit{Tile=destination,UnitState=partnerState,UnitData=partnerState.UnitData};destination.Unit=partner;
                    r.Tile.Data.unit=partnerState;r.State.coordinates=new(1,0);r.Game.Units.Add(10,partnerState);
                    r.Game.Map.Tiles.Add((1,0),destination.Data);r.Map.Tiles.Add((1,0),destination);r.Map.Units.Add(10,partner);
                    string before=StateFingerprint(r);
                    var order=reversed?new[]{destination,r.Tile}:new[]{r.Tile,destination};r.Map.Refresh(r.Context,order);
                    Check(destination.Unit is not null&&destination.Unit!=r.View&&destination.Unit.UnitData.type==Type.dcp_mimic_form_polytaur,"Native full Swap refresh retained stale initiating geometry");
                    Check(r.Tile.Unit==partner&&partner.Tile==r.Tile&&partner.DestroyCalls==0,"Full Swap refresh lost or replaced genuine partner");
                    Check(r.View.DestroyCalls==1&&r.View.PoolReturns==1&&r.Map.Units.Count==2,"Full Swap refresh duplicated/dropped a registered visual");
                    Check(before==StateFingerprint(r)&&ReferenceEquals(r.Tile.Data.unit,partnerState),"Full Swap refresh altered actual occupancy or action budget");
                    r.Map.Refresh(r.Context,order);Check(r.View.DestroyCalls==1&&destination.Creates==1&&partner.DestroyCalls==0,"Second Swap refresh churned current native visuals");
                });
                var denied=new Dictionary<string,Action<Rig>>{
                    ["unregistered"]=r=>RecruitmentHooks.Registered=false,
                    ["wrong scene map"]=r=>MapRenderer.Current=new(),["wrong scene state"]=r=>GameManager.GameState=new(),
                    ["context map"]=r=>r.Context.mapData=new(),["context registry"]=r=>r.Context.unitRegistry=new(),["context gld"]=r=>r.Context.gld=new(),
                    ["viewer"]=r=>r.Context.localPlayerId=2,["replay"]=r=>GameManager.Client!.IsReplay=true,["spectating"]=r=>GameManager.Client!.IsSpectating=true,
                    ["unready session"]=r=>NativeRegroupSession.IsReady=false,["hidden tile"]=r=>r.Tile.Hidden=true,["hidden unit"]=r=>r.View.Visible=false,
                    ["invisible unit"]=r=>r.View.Invisible=true,["inactive unit"]=r=>r.View.gameObject.activeInHierarchy=false,["inactive tile"]=r=>r.Tile.gameObject.activeInHierarchy=false,
                    ["foreign tile"]=r=>r.Map.Tiles[(0,0)]=new(),["foreign unit"]=r=>r.Map.Units[9]=new(),
                    ["foreign live state"]=r=>r.Game.Units[9]=new(){id=9},["foreign tile state"]=r=>r.Tile.Data.unit=new(){id=9,type=Type.dcp_mimic_form_polytaur},
                    ["foreign map data"]=r=>r.Game.Map.Tiles[(0,0)]=new(),["wrong coordinate"]=r=>r.State.coordinates=new(3,3),
                    ["wrong cached native form"]=r=>r.View.UnitData=new(){type=Type.warrior},["wrong live native data"]=r=>r.State.UnitData=new(){type=Type.warrior},
                    ["foreign previous backlink"]=r=>r.Tile.Unit=new(),["foreign previous tile"]=r=>r.View.Tile=new(),
                    ["ordinary unit"]=r=>r.State.type=Type.polytaur,["no client"]=r=>GameManager.Client=null,["no player"]=r=>GameManager.LocalPlayer=null,
                };
                foreach(var denial in denied)Case(()=>{var r=Make();denial.Value(r);NativeDisguiseRefresh.BeforeTile(r.Tile,r.Context);Check(r.View.DestroyCalls==0,"Unsafe retirement allowed: "+denial.Key);});
                Case(()=>{
                    var r=Make();r.View.Failure="before";string before=StateFingerprint(r);
                    for(int i=0;i<5;i++)r.Tile.RenderUnit(r.Context);
                    Check(r.View.DestroyCalls==1&&r.Tile.OriginalRenders==5,"Native error retried or suppressed ordinary rendering");
                    Check(r.Tile.Unit==r.View&&before==StateFingerprint(r),"Failed pre-retirement changed state");
                    Check(Entry.Notes.Count(n=>n.Kind.EndsWith("error"))==1,"Failure logs are unbounded");
                    r.Map.OnDestroy();r.View.Failure="";r.Tile.RenderUnit(r.Context);
                    Check(r.View.DestroyCalls==2&&r.Tile.Creates==1,"Scene cleanup retained stale failure latch");
                });
                Case(()=>{
                    var r=Make();r.View.Failure="after";string before=StateFingerprint(r);r.Tile.RenderUnit(r.Context);
                    Check(r.Tile.Creates==1&&r.Tile.Unit!=r.View&&r.View.DestroyCalls==1,"Post-retirement error prevented native continuation");
                    Check(before==StateFingerprint(r),"Post-retirement error changed body");
                });
                Case(()=>{var r=Make();r.View.Failure="retains";for(int i=0;i<4;i++)r.Tile.RenderUnit(r.Context);Check(r.View.DestroyCalls==1&&r.Tile.Creates==0,"Incomplete native cleanup repeated destructively");});
                Case(()=>{var r=Make();r.View.Reenter=()=>NativeDisguiseRefresh.BeforeTile(r.Tile,r.Context);r.Tile.RenderUnit(r.Context);Check(r.View.DestroyCalls==1&&r.Tile.Creates==1,"Native callback reentered retirement");});
                Case(()=>{
                    var r=Make();r.View.Failure="before";NativeDisguiseRefresh.BeforeTile(r.Tile,r.Context);
                    r.Map.Units.Remove(9);r.Game.Units.Remove(9);r.State.id=10;r.Map.Units.Add(10,r.View);r.Game.Units.Add(10,r.State);
                    r.View.Failure="";r.Tile.RenderUnit(r.Context);
                    Check(r.View.DestroyCalls==2&&r.Tile.Creates==1,"Failure latch blacklisted a reused visual with a different identity");
                });
                NativeDisguiseRefresh.Clear();
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,deniedContexts=denied.Count,idleRefreshes=50}));return 0;
            }
            catch(Exception e){Console.WriteLine(e);return 1;}
        }
    }
}
