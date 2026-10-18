using System;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
    public static class GrowthAudioTests
    {
        static int Cases,Assertions;
        static void Check(bool ok,string why){Assertions++;if(!ok)throw new Exception(why);}
        static void Case(System.Action act){Cases++;act();}
        sealed record Rig(GameState State,MapRenderer Map,Tile Tile,UnitState Unit,LedgerUnit Row,LedgerAppearance Before,EndTurnReaction Reaction);
        static LedgerAppearance Appearance(string form)=>new(form,new("Elyrion","Default"),new("Elyrion","Default"),new("Elyrion","Default"),7,
            (ushort)(form=="dragonegg"?60:form=="babydragon"?110:160),(ushort)(form=="dragonegg"?100:form=="babydragon"?150:200),false,ApparentConcealment.Visible,form=="firedragon"?-1:3,Array.Empty<string>(),form=="firedragon"?-1:11);
        static Rig Make(string before="dragonegg",string after="babydragon",LedgerActualKind kind=LedgerActualKind.Mimic)
        {
            NativeDisguiseGrowthAudio.Clear();NativeGameStateEnvelope.Frames.Clear();AudioManager.Calls.Clear();AudioManager.Enabled=true;AudioManager.Throw=false;AudioManager.DuringEnabled=null;AudioManager.DuringPlay=null;AudioManager.instance=new();
            Entry.Notes.Clear();HotSeatOverlay.IsActive=false;GameManager.LocalIds=new(){1};GameManager.Client=new();GameManager.LocalPlayer=new();
            var unit=new UnitState();var state=new GameState();state.Units.Add(unit.id,unit);var tile=new Tile();tile.Data.unit=unit;state.Map.Tiles.Add((2,3),tile.Data);
            var map=new MapRenderer();map.Tiles.Add((2,3),tile);MapRenderer.Current=map;GameManager.GameState=state;NativeRegroupSession.Owned=state;NativeRegroupSession.Allowed=true;
            var row=new LedgerUnit(10,9,true,kind,true,Appearance(after),PaidInvestmentKind.Known,3);Put(state,row);
            return new(state,map,tile,unit,row,Appearance(before),new());
        }
        static void Put(GameState state,params LedgerUnit[] rows)=>NativeGameStateEnvelope.Put(state,new(Guid.Parse("a7b8c021-dcfe-458e-a4e4-777333003581"),100,1,rows,Array.Empty<LedgerReceipt>()));
        static void Record(Rig r)=>NativeDisguiseGrowthAudio.Record(r.Reaction.action,r.State,r.Before,r.Row);
        static void Play(Rig r)=>NativeDisguiseGrowthAudio.BeforeReaction(r.Reaction);
        public static int Run()
        {
            try
            {
                Case(()=>{var h=new HarmonyLib.Harmony();NativeDisguiseGrowthAudio.Install(h);Check(h.Hooks.SequenceEqual(new[]{"EndTurnReaction.Execute:BeforeReaction:","DisembarkReaction.Execute::BeforeReaction","UpgradeReaction.Execute:BeforeNativeUpgrade:","MapRenderer.OnDestroy:MapDestroying:"}),"Audio hook timing drifted");});
                foreach(var phase in new[]{("dragonegg","babydragon"),("babydragon","firedragon")})Case(()=>{
                    var r=Make(phase.Item1,phase.Item2);Record(r);Check(AudioManager.Calls.Count==0&&NativeDisguiseGrowthAudio.PendingCount==1,"Simulation played sound or failed to enqueue");
                    Play(r);Check(AudioManager.Calls.Count==1&&AudioManager.Calls[0]==new AudioManager.Played(SFXTypes.Promote,2,3,PolytopiaBackendBase.Common.SkinType.Default,1,1),"Wrong native local growth cue/parameters");
                    Check(r.Tile.Data.LastIgnore==false,"Growth sound bypassed invisible-unit filter");
                    for(int i=0;i<50;i++){Record(r);Play(r);}Check(AudioManager.Calls.Count==1&&NativeDisguiseGrowthAudio.PendingCount==0,"Repeated commit/reaction replayed sound");
                });
                Case(()=>{var r=Make();r.Unit.owner=2;r.Reaction.action.PlayerId=2;Record(r);Play(r);Check(AudioManager.Calls.Single().Cue==SFXTypes.SpawnEnemy,"Visible opposing growth did not use native SpawnEnemy");});
                Case(()=>{var r=Make();r.Unit.owner=2;GameManager.LocalIds.Add(2);Record(r);Play(r);Check(AudioManager.Calls.Single().Cue==SFXTypes.Promote,"Cue guessed owner==viewer instead of native local-owner predicate");});
                foreach(string denial in new[]{"mute","manager","hidden","unexplored","invisible","tile-inactive","tile-dead","map-inactive","map-dead","tile-data","missing-tile","missing-unit","dead-unit","owner","coordinate","foreign-unit","unowned","replay","spectator","recap","hotseat","foreign-state","epoch","logical","phase","health","stale-round","viewer-missing","client-missing"})Case(()=>{
                    var r=Make();Record(r);
                    switch(denial)
                    {
                        case "mute":AudioManager.Enabled=false;break;case "manager":AudioManager.instance=null;break;case "hidden":r.Tile.IsHidden=true;break;
                        case "unexplored":r.Tile.Data.Explored=false;break;case "invisible":r.Tile.Data.Invisible=true;break;case "tile-inactive":r.Tile.gameObject.activeInHierarchy=false;break;
                        case "tile-dead":r.Tile.Destroyed=true;break;case "map-inactive":r.Map.gameObject.activeInHierarchy=false;break;case "map-dead":r.Map.Destroyed=true;break;
                        case "tile-data":r.Tile.Data=new();break;case "missing-tile":r.Map.Tiles.Clear();break;case "missing-unit":r.State.Units.Clear();break;
                        case "dead-unit":r.Unit.health=0;break;case "owner":r.Unit.owner=2;break;case "coordinate":r.Unit.coordinates=new(4,4);break;
                        case "foreign-unit":r.Tile.Data.unit=new();break;case "unowned":NativeRegroupSession.Allowed=false;break;
                        case "replay":GameManager.Client!.IsReplay=true;break;case "spectator":GameManager.Client!.IsSpectating=true;break;case "recap":GameManager.Client!.IsRecap=true;break;
                        case "hotseat":HotSeatOverlay.IsActive=true;break;case "foreign-state":GameManager.GameState=new();break;
                        case "epoch":NativeGameStateEnvelope.Put(r.State,new(Guid.NewGuid(),100,1,new[]{r.Row},Array.Empty<LedgerReceipt>()));break;
                        case "logical":Put(r.State,r.Row with{LogicalId=11});break;case "phase":Put(r.State,r.Row with{Appearance=Appearance("firedragon")});break;
                        case "health":Put(r.State,r.Row with{Appearance=r.Row.Appearance! with{Health=100}});break;
                        case "stale-round":r.State.CurrentTurn=10;break;
                        case "viewer-missing":GameManager.LocalPlayer=null;break;case "client-missing":GameManager.Client=null;break;
                    }
                    Play(r);Check(AudioManager.Calls.Count==0&&NativeDisguiseGrowthAudio.PendingCount==0,"Denied presentation leaked or retained audio: "+denial);
                    AudioManager.Enabled=true;r.Tile.IsHidden=false;r.Tile.Data.Explored=true;r.Tile.Data.Invisible=false;Play(r);Check(AudioManager.Calls.Count==0,"Denied event replayed later: "+denial);
                });
                Case(()=>{var r=Make();Record(r);var other=new EndTurnReaction();NativeDisguiseGrowthAudio.BeforeReaction(other);Check(NativeDisguiseGrowthAudio.PendingCount==1&&AudioManager.Calls.Count==0,"Unrelated action consumed pending cue");Play(r);Check(AudioManager.Calls.Count==1,"Exact action could not consume cue");});
                Case(()=>{var r=Make();Record(r);NativeDisguiseGrowthAudio.Cancel(r.Reaction.action,r.State);Play(r);Check(AudioManager.Calls.Count==0,"Failed owner-end replayed cue");});
                foreach(bool clear in new[]{false,true})Case(()=>{var r=Make();Record(r);if(clear)NativeDisguiseGrowthAudio.Clear();else NativeDisguiseGrowthAudio.MapDestroying(r.Map);Play(r);Check(AudioManager.Calls.Count==0&&NativeDisguiseGrowthAudio.PendingCount==0,"Retired lifecycle retained cue");});
                Case(()=>{var r=Make();Record(r);AudioManager.Throw=true;Play(r);Play(r);Check(AudioManager.Calls.Count==1&&Entry.Notes.Any(n=>n.StartsWith("disguise-growth-audio-error")),"Sound failure threw or retried");});
                foreach(var phase in new[]{("dragonegg","dragonegg"),("babydragon","babydragon"),("dragonegg","firedragon"),("warrior","babydragon")})Case(()=>{var r=Make(phase.Item1,phase.Item2);Record(r);Play(r);Check(AudioManager.Calls.Count==0&&NativeDisguiseGrowthAudio.PendingCount==0,"Non-native phase emitted growth: "+phase);});
                Case(()=>{var r=Make();var landing=new DisembarkReaction();NativeDisguiseGrowthAudio.Record(landing.action,r.State,r.Before,r.Row);NativeDisguiseGrowthAudio.BeforeReaction(landing);NativeDisguiseGrowthAudio.BeforeReaction(landing);Check(AudioManager.Calls.Count==1,"Ordinary overdue landing cue missing/repeated");});
                Case(()=>{var r=Make();NativeDisguiseGrowthAudio.Record(new ActionBase(),r.State,r.Before,r.Row);Play(r);Check(AudioManager.Calls.Count==0&&NativeDisguiseGrowthAudio.PendingCount==0,"Exchange/arbitrary action queued growth sound");});
                Case(()=>{var r=Make();var ship=r.Row with{Appearance=r.Row.Appearance! with{Form="transportship",GrowthTurns=-1,GrowthDueTurn=-1,Passenger=r.Before},Transport=new(11)};Put(r.State,ship);NativeDisguiseGrowthAudio.Record(r.Reaction.action,r.State,r.Before,ship);Play(r);Check(AudioManager.Calls.Count==0,"Afloat passenger emitted growth sound");});
                Case(()=>{var r=Make(kind:LedgerActualKind.Genuine);r.Unit.type=Polytopia.Data.UnitData.Type.warrior;Record(r);Play(r);Check(AudioManager.Calls.Count==1,"Genuine independent appearance did not receive its own growth cue");});
                Case(()=>{var r=Make(kind:LedgerActualKind.Genuine);r.Unit.type=Polytopia.Data.UnitData.Type.babydragon;NativeDisguiseGrowthAudio.BeforeNativeUpgrade(new UpgradeReaction());Check(AudioManager.Calls.Count==0,"Observer replaced native sound");Record(r);Play(r);Check(AudioManager.Calls.Count==0,"Private and actual growth double-played same native cue");});
                Case(()=>{var r=Make();NativeDisguiseGrowthAudio.BeforeNativeUpgrade(new UpgradeReaction());Record(r);Play(r);Check(AudioManager.Calls.Count==1,"Unrelated/native-looking reaction suppressed Mimic cue");});
                Case(()=>{var r=Make(kind:LedgerActualKind.Genuine);r.Unit.type=Polytopia.Data.UnitData.Type.babydragon;var upgrade=new UpgradeReaction();upgrade.action.Coordinates=new(7,7);NativeDisguiseGrowthAudio.BeforeNativeUpgrade(upgrade);Record(r);Play(r);Check(AudioManager.Calls.Count==1,"Different coordinate native growth suppressed private cue");});
                Case(()=>{var r=Make("babydragon","firedragon",LedgerActualKind.Genuine);r.Unit.type=Polytopia.Data.UnitData.Type.transportship;var row=r.Row with{Transport=new(40)};Put(r.State,row);NativeDisguiseGrowthAudio.Record(r.Reaction.action,r.State,r.Before,row);Play(r);Check(AudioManager.Calls.Count==1,"Actual cargo suppressed a growing visible root disguise");});
                Case(()=>{var r=Make(kind:LedgerActualKind.Genuine);r.Unit.type=Polytopia.Data.UnitData.Type.dragonegg;var native=new UpgradeAction();r.State.ActionStack.Add(native);var landing=new DisembarkReaction();NativeDisguiseGrowthAudio.Record(landing.action,r.State,r.Before,r.Row);NativeDisguiseGrowthAudio.BeforeReaction(landing);Check(AudioManager.Calls.Count==0,"Landing played private cue before already-queued genuine upgrade");r.State.ActionStack.Clear();r.Unit.type=Polytopia.Data.UnitData.Type.babydragon;NativeDisguiseGrowthAudio.BeforeNativeUpgrade(new UpgradeReaction{action=native});AudioManager.PlaySFXAtTile(SFXTypes.Promote,r.Unit.coordinates,PolytopiaBackendBase.Common.SkinType.Default,1,1);Check(AudioManager.Calls.Count==1,"Native genuine upgrade cue was suppressed or doubled");});
                foreach(string mismatch in new[]{"owner","coordinate","target","invalid"})Case(()=>{var r=Make(kind:LedgerActualKind.Genuine);r.Unit.type=Polytopia.Data.UnitData.Type.dragonegg;var queued=new UpgradeAction();if(mismatch=="owner")queued.PlayerId=2;if(mismatch=="coordinate")queued.Coordinates=new(8,9);if(mismatch=="target")queued.Type=Polytopia.Data.UnitData.Type.firedragon;if(mismatch=="invalid")queued.Valid=false;r.State.ActionStack.Add(queued);Record(r);Play(r);Check(AudioManager.Calls.Count==1,"Unrelated/invalid pending upgrade swallowed sound: "+mismatch);});
                Case(()=>{var r=Make();Record(r);AudioManager.DuringEnabled=()=>GameManager.GameState=new();Play(r);Check(AudioManager.Calls.Count==0,"Native context change during mute check leaked stale sound");});
                Case(()=>{var r=Make();for(int i=0;i<80;i++)NativeDisguiseGrowthAudio.Record(new EndTurnAction(),r.State,r.Before,r.Row);Check(NativeDisguiseGrowthAudio.PendingCount==64,"Pending action batches unbounded");});
                Case(()=>{var r=Make();Record(r);r.State.CurrentTurn=9;Play(r);Check(AudioManager.Calls.Count==1,"Last owner's native round rollover lost growth cue");});
                Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,scope="synthetic-native-growth-reaction-audio"}));return 0;
            }
            catch(Exception e){Console.WriteLine(e);return 1;}
        }
    }
}
