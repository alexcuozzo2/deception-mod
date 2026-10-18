using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using PolytopiaBackendBase.Common;
using UnityEngine;

namespace Deception.Tests
{
    // Real art providers, resource bytes, prewarm hooks and cache code. Unity
    // decoding and atlas completion are synthetic; these are not timings.
    public static class TechPreloadTests
    {
        static int Assertions;static readonly List<string> Results=new();static double Time;
        static void Check(bool value,string why){Assertions++;if(!value)throw new Exception(why);}
        static void Reset()
        {
            typeof(UiAdapterTests).GetMethod("Reset",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);
            NativeTechArtPrewarm.Clear();Time=0;NativeTechArtPrewarm.Clock=()=>Time;
            GameManager.instance=new();GameManager.Client.GameState=GameManager.GameState;
            GameManager.Client.clientType=ClientBase.ClientType.PassAndPlay;NativeRegroupSession.Enabled=true;
            GameManager.LocalPlayer.Id=1;GameManager.LocalPlayer.tribe=TribeType.Imperius;
            GameManager.GameState.Players[1]=GameManager.LocalPlayer;
            GameManager.GameState.Players[2]=new(){Id=2,tribe=TribeType.Aquarion,skinType=SkinType.TestSkin,color=0x3366CC};
        }
        static void Case(string id,Action action)
        {try{Reset();action();Results.Add(id);}catch(Exception e){throw new Exception(id,e);}finally{NativeTechArtPrewarm.Clear();}}
        static void Warm()=>NativeTechArtPrewarm.BeforeReady(GameManager.instance);
        static bool Pending=>typeof(NativeTechArtPrewarm).GetField("Pending",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null) is not null;
        static void Profiles()
        {foreach(var player in GameManager.GameState.Players.Values){_=UnitArtAssets.Layers(player,false,NativeAquarionUnits.IsAquarion(player));_=UnitArtAssets.Layers(player,true,NativeAquarionUnits.IsAquarion(player));}}
        static void TechArt()
        {foreach(string tech in new[]{TechTree.PropagandaId,TechTree.CamouflageId,TechTree.IntelligenceId})_=BranchArtAssets.Get(tech,false);_=BlackmailArtAssets.Sprite();_=BlackmailArtAssets.Sprite(true);_=TradeSecretsArtAssets.Sprite();_=TradeSecretsArtAssets.Sprite(true);_=FoggerArtAssets.Icon();_=FoggerArtAssets.Team();_=FoggerArtAssets.Badge();_=BranchArtAssets.DisguiseTech();_=BranchArtAssets.Reconnaissance();Profiles();}
        static void Skip(Action alter)
        {alter();int loads=ImageConversion.Loads,requests=SpriteHandle.Requested.Count;Warm();Check(ImageConversion.Loads==loads&&SpriteHandle.Requested.Count==requests,"Invalid context performed asset work");Check(!Pending,"Invalid context retained polling job");}
        public static int Run()
        {
            try
            {
                Case("PRELOAD-001-cold-both-player-first-open-has-no-decode",()=>{
                    int loads=ImageConversion.Loads,frames=Sprite.CreateModes.Count;Warm();int warm=ImageConversion.Loads,blits=Graphics.Blits,requests=SpriteHandle.Requested.Count;
                    Check(warm==loads&&requests==2&&Sprite.CreateModes.Count>frames&&
                        new[]{"mermimic_body_team","mimic-body-team"}.All(name=>ReferenceEquals(((Sprite)UnitArtAssets.Get(name)).texture,((Sprite)PolyMod.Registry.sprites["dcp_"+name+"__"]).texture)),
                        "Both profile families must prewarm frames from loader textures without duplicate PNG decoding");
                    Check(Entry.Notes.Any(n=>n.Kind=="unit-art-loaded"&&n.Message.StartsWith("mermimic_body_team "))&&Entry.Notes.Any(n=>n.Kind=="unit-art-loaded"&&n.Message.StartsWith("mimic-body-team ")),"Both species body assets were not prewarmed");
                    TechArt();GameManager.LocalPlayer=GameManager.GameState.Players[2];TechArt();
                    Check(ImageConversion.Loads==warm&&Graphics.Blits==blits&&SpriteHandle.Requested.Count==requests,"First/second player tech open repeated cold work");
                    Check(!Pending&&!Entry.Notes.Any(n=>n.Kind=="tech-art-prewarm-unavailable"),"Synchronous prewarm did not finish");
                });
                Case("PRELOAD-002-repeated-ready-and-idle-do-not-repeat-work",()=>{
                    Warm();int loads=ImageConversion.Loads,requests=SpriteHandle.Requested.Count,blits=Graphics.Blits;
                    for(int i=0;i<35;i++){Warm();NativeTechArtPrewarm.AfterUpdate(GameManager.instance);}
                    Check(ImageConversion.Loads==loads&&requests==SpriteHandle.Requested.Count&&blits==Graphics.Blits,"Repeated loading/update duplicated expensive work");
                    Check(Entry.Notes.Count(n=>n.Kind=="tech-art-prewarmed")==1,"Ready callback work was not deduplicated");
                });
                Case("PRELOAD-003-identical-profiles-share-request-colors-stay-local",()=>{
                    var second=GameManager.GameState.Players[2];second.tribe=TribeType.Imperius;second.skinType=SkinType.Default;second.color=0x00FF00;GameManager.LocalPlayer.color=0xFF0000;
                    Warm();Check(SpriteHandle.Requested.Count==1,"Identical native head profiles requested twice");
                    var first=UnitArtAssets.Layers(GameManager.LocalPlayer);var other=UnitArtAssets.Layers(second);
                    Check(((Color)first[1].Color).r==1&&((Color)other[1].Color).g==1,"Shared art retained another player's tint");
                });
                Case("PRELOAD-004-enrolled-sibling-does-not-clear-head-cache",()=>{
                    Warm();var prior=GameManager.GameState;int requests=SpriteHandle.Requested.Count;var sibling=new GameState();
                    foreach(var p in prior.Players)sibling.Players[p.Key]=new(){Id=p.Value.Id,tribe=p.Value.tribe,skinType=p.Value.skinType,color=p.Value.color};
                    GameManager.GameState=sibling;GameManager.Client.GameState=sibling;Profiles();Warm();
                    Check(SpriteHandle.Requested.Count==requests,"Sibling state discarded warmed atlas profiles");
                });
                Case("PRELOAD-005-new-atlas-invalidates-borrowed-heads",()=>{
                    Warm();var old=GameManager.Atlas.Head!;int requests=SpriteHandle.Requested.Count;GameManager.Atlas=new(){Head=new("Replacement atlas head")};Profiles();
                    Check(SpriteHandle.Requested.Count==requests+2,"New atlas reused stale handles");
                    Check(ReferenceEquals(UnitArtAssets.Layers(GameManager.LocalPlayer)[2].Sprite,GameManager.Atlas.Head)&&old.DestroyCalls==0,"Atlas invalidation destroyed borrowed art or retained wrong head");
                });
                Case("PRELOAD-006-level-reload-invalidates-same-atlas-generation",()=>{
                    Warm();int requests=SpriteHandle.Requested.Count;NativeTechArtPrewarm.BeforeLevel(GameManager.instance);Warm();
                    Check(SpriteHandle.Requested.Count==requests+2,"In-place atlas reload reused earlier generation");
                });
                Case("PRELOAD-007-dead-head-sprite-reloads-once",()=>{
                    Warm();var old=GameManager.Atlas.Head!;UnityEngine.Object.Destroy(old);GameManager.Atlas.Head=new("New head");int count=SpriteHandle.Requested.Count;
                    Check(UnitArtAssets.Prepare(GameManager.LocalPlayer)&&SpriteHandle.Requested.Count==count+1,"Dead sprite did not request replacement");
                    Check(UnitArtAssets.Prepare(GameManager.LocalPlayer)&&SpriteHandle.Requested.Count==count+1,"Replacement head requested twice");
                });
                Case("PRELOAD-008-dead-head-texture-reloads-once",()=>{
                    Warm();UnityEngine.Object.Destroy(GameManager.Atlas.Head!.texture);GameManager.Atlas.Head=new("New texture head");int count=SpriteHandle.Requested.Count;
                    Check(UnitArtAssets.Prepare(GameManager.LocalPlayer)&&SpriteHandle.Requested.Count==count+1,"Live wrapper with dead texture survived cache");
                });
                Case("PRELOAD-009-failed-head-completion-does-not-loop",()=>{
                    SpriteHandle.Delay=true;Warm();foreach(var handle in SpriteHandle.Requested)handle.Complete(null);int count=SpriteHandle.Requested.Count;
                    for(int i=0;i<20;i++)NativeTechArtPrewarm.AfterUpdate(GameManager.instance);
                    Check(SpriteHandle.Requested.Count==count,"Missing head retried each frame");
                    Check(Entry.Notes.Count(n=>n.Kind=="unit-head-load-failed")==2,"Missing native profiles logged repeatedly");
                });
                Case("PRELOAD-010-async-both-heads-finish-without-ui",()=>{
                    SpriteHandle.Delay=true;Warm();Check(Pending&&SpriteHandle.Requested.Count==2,"Both asynchronous profiles were not started");int loads=ImageConversion.Loads;
                    foreach(var handle in SpriteHandle.Requested)handle.Complete(new("Async head"));NativeTechArtPrewarm.AfterUpdate(GameManager.instance);
                    Check(!Pending&&ImageConversion.Loads==loads,"Completion repeated body decoding or failed to finish");
                    Check(Entry.Notes.Count(n=>n.Kind=="tech-art-heads-prewarmed")==1,"Missing asynchronous completion evidence");
                });
                Case("PRELOAD-011-async-enrolled-sibling-same-roster-finishes",()=>{
                    SpriteHandle.Delay=true;Warm();var sibling=new GameState();foreach(var p in GameManager.GameState.Players)sibling.Players[p.Key]=new(){Id=p.Value.Id,tribe=p.Value.tribe,skinType=p.Value.skinType};
                    GameManager.GameState=sibling;GameManager.Client.GameState=sibling;foreach(var handle in SpriteHandle.Requested)handle.Complete(new("Sibling head"));NativeTechArtPrewarm.AfterUpdate(GameManager.instance);
                    Check(!Pending&&SpriteHandle.Requested.Count==2&&Entry.Notes.Any(n=>n.Kind=="tech-art-heads-prewarmed"),"Safe sibling lost pending head completion");
                });
                Case("PRELOAD-012-async-changed-sibling-roster-cancels",()=>{
                    SpriteHandle.Delay=true;Warm();var sibling=new GameState();sibling.Players[1]=GameManager.LocalPlayer;GameManager.GameState=sibling;GameManager.Client.GameState=sibling;NativeTechArtPrewarm.AfterUpdate(GameManager.instance);
                    Check(!Pending&&!Entry.Notes.Any(n=>n.Kind=="tech-art-heads-prewarmed"),"Changed roster inherited another job");
                });
                Case("PRELOAD-013-async-new-client-cancels",()=>{
                    SpriteHandle.Delay=true;Warm();GameManager.Client=new();NativeTechArtPrewarm.AfterUpdate(GameManager.instance);Check(!Pending,"New client inherited loading job");
                });
                Case("PRELOAD-014-async-deadline-stops-polling",()=>{
                    SpriteHandle.Delay=true;Warm();Time=11;NativeTechArtPrewarm.AfterUpdate(GameManager.instance);Check(!Pending,"Deadline did not retire pending work");
                });
                Case("PRELOAD-015-async-poll-count-is-bounded",()=>{
                    SpriteHandle.Delay=true;Warm();for(int i=0;i<301;i++)NativeTechArtPrewarm.AfterUpdate(GameManager.instance);Check(!Pending&&SpriteHandle.Requested.Count==2,"Polling exceeded count or retried native requests");
                });
                Case("PRELOAD-016-session-clear-retires-pending-only",()=>{
                    SpriteHandle.Delay=true;Warm();var sprite=(Sprite)UnitArtAssets.Icon();NativeTechArtPrewarm.Clear();NativeTechArtPrewarm.AfterUpdate(GameManager.instance);
                    Check(!Pending&&sprite.DestroyCalls==0&&sprite.texture.DestroyCalls==0,"Clear retained job or destroyed shared published art");
                });
                Case("PRELOAD-017-before-atlases-is-noop",()=>Skip(()=>GameManager.instance.isLevelLoaded=false));
                Case("PRELOAD-018-fake-session-is-noop",()=>Skip(()=>GameManager.instance.fakeSession=new object()));
                Case("PRELOAD-019-unready-client-is-noop",()=>Skip(()=>GameManager.Client.IsReady=false));
                Case("PRELOAD-020-replay-is-noop",()=>Skip(()=>GameManager.Client.IsReplay=true));
                Case("PRELOAD-021-spectator-is-noop",()=>Skip(()=>GameManager.Client.IsSpectating=true));
                Case("PRELOAD-022-remote-is-noop",()=>Skip(()=>GameManager.Client.clientType=ClientBase.ClientType.Remote));
                Case("PRELOAD-023-unenrolled-is-noop-and-noted-once",()=>{
                    Skip(()=>NativeRegroupSession.Enabled=false);Warm();Check(Entry.Notes.Count(n=>n.Kind=="tech-art-prewarm-skipped")==1,"Enrolment timing gap silently skipped or spammed log");
                });
                Case("PRELOAD-024-client-state-mismatch-is-noop",()=>Skip(()=>GameManager.Client.GameState=new()));
                Case("PRELOAD-025-disabled-registry-is-noop",()=>Skip(()=>RecruitmentHooks.Registered=false));
                Case("PRELOAD-026-stale-manager-is-noop",()=>{
                    int requests=SpriteHandle.Requested.Count;NativeTechArtPrewarm.BeforeReady(new GameManager());Check(requests==SpriteHandle.Requested.Count,"Stale manager loaded art");
                });
                Case("PRELOAD-027-oversized-roster-fails-cosmetically",()=>{
                    for(byte i=3;i<34;i++)GameManager.GameState.Players[i]=new(){Id=i};Warm();Check(SpriteHandle.Requested.Count==0&&!Pending,"Oversized roster loaded unbounded profiles");
                    Check(Entry.Notes.Count(n=>n.Kind=="tech-art-prewarm-unavailable")==1,"Cosmetic failure did not produce bounded diagnostic");
                });
                Case("PRELOAD-028-neutral-entry-does-not-count-as-player",()=>{
                    GameManager.GameState.Players[0]=new(){Id=0};Warm();Check(SpriteHandle.Requested.Count==2&&Entry.Notes.Any(n=>n.Kind=="tech-art-prewarmed"&&n.Message.Contains("players=2")),"Neutral entry became a third player");
                });
                Case("PRELOAD-029-source-and-gameplay-unchanged",()=>{
                    var state=GameManager.GameState;state.Units[1]=new(){id=1,owner=1,health=10,moved=true,attacked=true};var unit=state.Units[1];var player=GameManager.LocalPlayer;int money=player.Currency;var map=state.Map;var queue=state.ActionStack;
                    Warm();Check(ReferenceEquals(state,GameManager.GameState)&&ReferenceEquals(map,state.Map)&&ReferenceEquals(queue,state.ActionStack)&&state.ActionStack.Count==0,"Prewarm replaced gameplay roots");
                    Check(unit.health==10&&unit.owner==1&&unit.moved&&unit.attacked&&player.Currency==money&&GameManager.Client.Sent==0,"Prewarm mutated gameplay or sent command");
                });
                Case("PRELOAD-030-all-fixture-tribes-use-native-address-profiles",()=>{
                    GameManager.GameState.Players.Clear();byte id=1;foreach(var tribe in Enum.GetValues<TribeType>())GameManager.GameState.Players[id]=new(){Id=id++,tribe=tribe};Warm();
                    Check(SpriteHandle.Requested.Count==Enum.GetValues<TribeType>().Length,"A tribe failed to preload its native head profile");
                    Check(!Pending&&!Entry.Notes.Any(n=>n.Kind=="tech-art-prewarm-unavailable"),"Valid tribe failed preload");
                });
                var result=new{passed=true,cases=Results.Count,assertions=Assertions,results=Results};Console.WriteLine(JsonSerializer.Serialize(result));return 0;
            }
            catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Results.Count,assertions=Assertions,error=e.ToString()}));return 1;}
        }
    }
}
