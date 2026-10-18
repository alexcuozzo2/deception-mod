// Production Smuggle command/action hooks against synthetic native objects.
// Byte framing is real; native command effects, Harmony and game success are not.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
using Polytopia.Data;
using BinaryReader=Il2CppSystem.IO.BinaryReader;
using BinaryWriter=Il2CppSystem.IO.BinaryWriter;
using MemoryStream=Il2CppSystem.IO.MemoryStream;

namespace Deception.Tests
{
    public static class SmuggleAdapterTests
    {
        sealed record Fixture(GameState State,PlayerState Player,UnitState Unit,TileData Source,TileData Home);
        static void Check(bool value,string message="Smuggle fixture assertion failed")
        {if(!value)throw new Exception(message);}
        static void Throws(Action action)
        {try{action();}catch{return;}throw new Exception("Expected failure");}
        static T[] Items<T>(Il2CppSystem.Collections.Generic.List<T> list)=>Enumerable.Range(0,list.Count).Select(i=>list[i]).ToArray();
        static Fixture Setup(uint id=3)
        {
            MimicPresentationUi.Clear();MimicPresentationState.Clear();MimicSmuggleUi.Clear();
            PopupManager.PopupShowing=false;PopupManager.ProductionCallbacks=false;PopupManager.SmuggleCallbacks=false;PopupManager.LastIconPopup=null;
            MimicSmuggleActions.Clear();
            MimicSmuggleActions.Configure(SmuggleCatalog.Harvests.Select(x=>new SmuggleHarvest(x.Id,x.NativeHarvest,x.Resource)));
            RecruitmentHooks.Registered=true;RecruitmentHooks.Forms.Clear();
            RecruitmentHooks.Forms.Add("dcp_mimic_form_warrior",new("warrior","dcp_mimic_form_warrior","Warrior",10,1,2,2,false));
            var state=new GameState();var player=new PlayerState{Id=1,Currency=7};state.Players[1]=player;state.Players[2]=new(){Id=2,Currency=19};
            var home=new TileData{coordinates=new(){X=1,Y=1},owner=1,Improvement=ImprovementData.Type.city,Population=4};
            var unit=new UnitState{id=id,coordinates=new(){X=3,Y=2},home=home.coordinates};
            var source=new TileData{coordinates=unit.coordinates,unit=unit,owner=1,resource=new(){type=ResourceData.Type.fruit}};
            state.Map.Add(home);state.Map.Add(source);state.Units[id]=unit;
            GameManager.GameState=state;GameManager.LocalPlayer=player;GameManager.Client=new();
            MapRenderer.Current=new();MapRenderer.Current.Tiles[(3,2)]=new(){Coordinates=source.coordinates};MapRenderer.Current.Tiles[(1,1)]=new(){Coordinates=home.coordinates};
            return new(state,player,unit,source,home);
        }
        static (ClientInteraction Interaction,Unit Unit) SelectUi(Fixture f)
        {
            var unit=new Unit{UnitState=f.Unit};var interaction=new ClientInteraction();
            interaction.Bar.tile=new(){Coordinates=f.Source.coordinates,Data=f.Source,Resource=new(),Unit=unit};
            MapRenderer.Current!.Tiles[(f.Source.coordinates.X,f.Source.coordinates.Y)]=interaction.Bar.tile;
            interaction.SelectUnit(unit);MimicPresentationUi.Selected(interaction,unit);MimicPresentationUi.BarReady(interaction.Bar);
            return (interaction,unit);
        }
        static UIRoundButton? SmuggleButton(InteractionBar bar)=>Items(bar.buttons).SingleOrDefault(x=>x.Text=="Smuggle");
        static BuildCommand Create(Fixture f)
        {
            int noteStart=Entry.Notes.Count;
            bool created=MimicSmuggleActions.TryCreate(f.State,f.Player,f.Source,out var command,out var reason);
            Check(created,reason+string.Concat(Entry.Notes.Skip(noteStart).Select(n=>"\n"+n.Kind+": "+n.Message)));
            return (BuildCommand)command!;
        }
        static bool Valid(Fixture f,BuildCommand command)
        {
            string reason="unchanged";bool result=true;
            Check(!MimicSmuggleActions.GuardValidity(command,f.State,ref reason,ref result),"Custom validity must replace only its own native predicate");
            return result;
        }
        static BuildAction Queue(Fixture f,BuildCommand command)
        {
            Check(MimicSmuggleActions.GuardCommand(command,f.State,out var scope),"Valid command was rejected");
            Exception? error=null;
            try{command.Execute(f.State);MimicSmuggleActions.CommandReturned(command,f.State,scope);}
            catch(Exception e){error=e;throw;}
            finally{Check(ReferenceEquals(MimicSmuggleActions.CommandFinished(error,scope),error));}
            return Items(f.State.ActionStack).OfType<BuildAction>().Last();
        }
        static byte[] Encode(BuildCommand command)
        {var stream=new MemoryStream();var writer=new BinaryWriter(stream);command.Serialize(writer,1);MimicSmuggleActions.WritePayload(command,writer,1);writer.Flush();return stream.ToArray();}
        static BuildCommand DecodeCommand(byte[] data,bool sentinel=false)
        {
            var stream=new MemoryStream(data);var reader=new BinaryReader(stream);var command=new BuildCommand();
            command.Deserialize(reader,1);MimicSmuggleActions.ReadPayload(command,reader,1);
            if(sentinel)Check(reader.ReadUInt32()==0xA1B2C3D4,"Payload consumed a following record");
            Check(stream.Position==stream.Length,"Command framing left unexpected bytes");return command;
        }
        static byte[] Encode(BuildAction action)
        {var stream=new MemoryStream();var writer=new BinaryWriter(stream);action.Serialize(writer,1);MimicSmuggleActions.WritePayload(action,writer,1);writer.Flush();return stream.ToArray();}
        static BuildAction DecodeAction(byte[] data)
        {
            var stream=new MemoryStream(data);var reader=new BinaryReader(stream);var action=new BuildAction();
            action.Deserialize(reader,1);MimicSmuggleActions.ReadPayload(action,reader,1);Check(stream.Position==stream.Length);return action;
        }
        static void Drain(Fixture f)
        {
            int limit=100;
            while(f.State.ActionStack.Count>0)
            {
                Check(--limit>0,"Action stack did not finish");int index=f.State.ActionStack.Count-1;var action=f.State.ActionStack[index];
                if(action is BuildAction build){if(MimicSmuggleActions.ExecuteAction(build,f.State))build.Execute(f.State);}
                else if(action is DestroyResourceAction resource)ExecuteResource(f,resource);
                else action.Execute(f.State);
                f.State.ActionStack.RemoveAt(index);
            }
        }
        static void ExecuteResource(Fixture f,DestroyResourceAction action)
        {
            MimicSmuggleActions.ResourceScope? scope=null;Exception? error=null;
            try
            {MimicSmuggleActions.ResourceRemoving(action,f.State,out scope);action.Execute(f.State);MimicSmuggleActions.ResourceRemoved(action,f.State,scope);}
            catch(Exception e){error=e;}
            error=MimicSmuggleActions.ResourceFinished(error,scope);if(error is not null)throw error;
        }
        static TileData Sanctuary(Fixture f,int x,int y,byte owner,int level=1,bool hidden=false)
        {
            if(!f.State.Players.ContainsKey(owner))f.State.Players[owner]=new(){Id=owner};
            var tile=new TileData{coordinates=new(x,y),owner=owner,Improvement=ImprovementData.Type.sanctuary,SanctuaryLevel=level};
            f.State.Map.Add(tile);MapRenderer.Current!.Tiles[(x,y)]=new(){Coordinates=tile.coordinates,Data=tile,IsHidden=hidden};return tile;
        }
        static DestroyResourceAction StageResource(Fixture f)
        {
            var action=Queue(f,Create(f));Check(!MimicSmuggleActions.ExecuteAction(action,f.State));f.State.ActionStack.Remove(action);
            return Items(f.State.ActionStack).OfType<DestroyResourceAction>().Single();
        }
        static void NativeLevelReaction(ImprovementLevelDownAction action,ref int callbacks)
        {
            int count=callbacks;new ImprovementLevelDownReaction{action=action}.Execute(new Il2CppSystem.Action(()=>count++));callbacks=count;
        }
        static string Snapshot(Fixture f)=>string.Join("|",f.Player.Currency,f.State.Players[2].Currency,f.Unit.id,f.Unit.owner,
            f.Unit.type,f.Unit.health,f.Unit.moved,f.Unit.attacked,f.Unit.coordinates.X,f.Unit.coordinates.Y,
            f.Unit.home.X,f.Unit.home.Y,f.Source.resource?.type.ToString()??"none",f.Source.ResourceRemovals,
            f.Home.Population,f.Home.PopulationActions,f.Home.DeferredPopulationActions,f.State.ActionStack.Count,
            string.Join(",",Items(f.Unit.effects)),
            string.Join(";",f.State.Map.Tiles.Values.Where(t=>t.Improvement==ImprovementData.Type.sanctuary).OrderBy(t=>t.coordinates.X).ThenBy(t=>t.coordinates.Y)
                .Select(t=>$"{t.coordinates.X},{t.coordinates.Y}:{t.SanctuaryLevel}:{t.SanctuaryLevelActions}:{t.SanctuaryUpdates}:{t.LastSanctuaryActor}")));

        public static void Run(Action<string,Action> test)
        {
            test("UI-attachment-smuggle-consumed-history-remains-encodable",()=> {
                var f=Setup();var command=Create(f);var action=Queue(f,command);byte[] before=Encode(action);Drain(f);
                // Native history may still hold this consumed action while a
                // subsequent offer is opened in the same mutable game state.
                f.Source.resource=new(){type=ResourceData.Type.fruit};f.Unit.moved=false;f.Unit.attacked=false;
                var later=Create(f);MimicSmuggleActions.PruneDead();
                Check(Encode(action).SequenceEqual(before),"Consumed history lost private action bytes");
                Check(Encode(command).Length>0);MimicSmuggleActions.ForgetOffer(later);
            });
            test("UI-080-smuggle-hooks-leave-ordinary-builds-and-bytes-unchanged",()=> {
                var f=Setup();var harmony=new HarmonyLib.Harmony();MimicSmuggleActions.Install(harmony);
                foreach(string method in new[]{"BuildCommand::IsValid","BuildCommand::Execute","BuildAction::Execute","BuildAction::ExecuteDefault",
                    "BuildCommand::Serialize","BuildCommand::Deserialize","BuildAction::Serialize","BuildAction::Deserialize","IncreasePopulationReaction::Execute",
                    "DestroyResourceAction::Execute","ActionUtils::CheckSurroundingArea","ActionUtils::UpdateImprovementLevel"})
                    Check(harmony.Patched.Contains(method),"Missing hook "+method);
                var command=new BuildCommand(1,ImprovementData.Type.harvestfruit,f.Source.coordinates);string reason="native";bool valid=false;
                Check(!MimicSmuggleActions.IsSmuggle(command)&&MimicSmuggleActions.Describe(command)=="");
                Check(MimicSmuggleActions.GuardValidity(command,f.State,ref reason,ref valid)&&!valid&&reason=="native");
                Check(MimicSmuggleActions.GuardCommand(command,f.State,out var scope)&&scope is null);
                command.Execute(f.State);MimicSmuggleActions.CommandReturned(command,f.State,scope);
                var action=Items(f.State.ActionStack).OfType<BuildAction>().Single();Check(MimicSmuggleActions.ExecuteAction(action,f.State));action.Execute(f.State);
                Check(f.State.NativeBuildExecutions==1&&f.Player.Currency==7&&!f.Unit.moved);
                var stream=new MemoryStream();var writer=new BinaryWriter(stream);command.Serialize(writer,1);writer.Flush();byte[] ordinary=stream.ToArray();
                MimicSmuggleActions.WritePayload(command,writer,1);writer.Flush();Check(stream.ToArray().SequenceEqual(ordinary));
                stream.Position=0;var reader=new BinaryReader(stream);var restored=new BuildCommand();restored.Deserialize(reader,1);long end=stream.Position;
                MimicSmuggleActions.ReadPayload(restored,reader,1);Check(stream.Position==end&&end==stream.Length);
            });
            test("UI-081-smuggle-source-id-roundtrips-command-action-and-frame",()=> {
                foreach(uint id in new[]{0u,3u,uint.MaxValue})
                {
                    var f=Setup(id);var original=Create(f);byte[] bytes=Encode(original);
                    // The synthetic native prefix is thirteen bytes; the suffix
                    // is exactly magic + schema byte + full UInt32 source ID.
                    Check(bytes.Length==22&&BitConverter.ToUInt32(bytes,13)==0x534D5547&&bytes[17]==1&&BitConverter.ToUInt32(bytes,18)==id);
                    MimicSmuggleActions.Clear();var decoded=DecodeCommand(bytes.Concat(BitConverter.GetBytes(0xA1B2C3D4u)).ToArray(),true);Check(Valid(f,decoded));
                    var action=Queue(f,decoded);var encodedAction=Encode(action);MimicSmuggleActions.Clear();
                    var restored=DecodeAction(encodedAction);Check(restored.DeductCost&&restored.Type==ImprovementData.Type.dcp_smuggle_fruit);
                    f.State.ActionStack.Clear();f.State.ActionStack.Add(restored);Drain(f);
                    Check(f.Source.ResourceRemovals==1&&f.Player.Currency==5&&f.Home.Population==5&&f.Unit.id==id);
                }
            });
            test("UI-082-smuggle-malformed-payloads-retire-stale-source",()=> {
                var f=Setup();var command=Create(f);var bytes=Encode(command);
                var invalid=new List<byte[]>{bytes[..13],bytes[..17],bytes[..18],bytes[..21]};
                var magic=(byte[])bytes.Clone();magic[13]^=1;invalid.Add(magic);
                var version=(byte[])bytes.Clone();version[17]=2;invalid.Add(version);
                foreach(var damaged in invalid)
                {
                    // Reuse a wrapper which previously held a valid source. A
                    // failed read must not leave the previous command executable.
                    command=DecodeCommand(bytes);var stream=new MemoryStream(damaged);var reader=new BinaryReader(stream);command.Deserialize(reader,1);
                    Throws(()=>MimicSmuggleActions.ReadPayload(command,reader,1));Check(!Valid(f,command),"Bad read retained stale source authority");
                    string before=Snapshot(f);Check(!MimicSmuggleActions.GuardCommand(command,f.State,out _));Check(Snapshot(f)==before);
                }
                var opaque=new BuildCommand(1,ImprovementData.Type.dcp_smuggle_unknown,f.Source.coordinates);
                Check(MimicSmuggleActions.IsSmuggle(opaque)&&!Valid(f,opaque));Throws(()=>Encode(opaque));
                var missing=new BuildCommand(1,ImprovementData.Type.dcp_smuggle_fruit,f.Source.coordinates);Check(!Valid(f,missing));
            });
            test("UI-083-smuggle-never-substitutes-a-new-unit-at-source",()=> {
                var f=Setup();var command=DecodeCommand(Encode(Create(f)));var action=Queue(f,command);byte[] encodedAction=Encode(action);
                var replacement=new UnitState{id=99,coordinates=f.Source.coordinates,home=f.Home.coordinates};f.State.Units[99]=replacement;f.Source.unit=replacement;
                f.Unit.coordinates=new(){X=9,Y=9};string before=Snapshot(f);
                Check(!Valid(f,command));Check(!MimicSmuggleActions.GuardCommand(command,f.State,out _));
                Check(!MimicSmuggleActions.ExecuteAction(DecodeAction(encodedAction),f.State));
                Check(Snapshot(f)==before&&!replacement.moved&&!replacement.attacked,"Stale command acted on replacement");
                // An id match is insufficient if the authoritative registry now
                // resolves a different native object from the on-tile instance.
                f=Setup();command=Create(f);f.State.Units[f.Unit.id]=new(){id=f.Unit.id,coordinates=f.Unit.coordinates,home=f.Unit.home};
                Check(!Valid(f,command));
            });
            test("UI-084-smuggle-revalidates-eligibility-without-effects",()=> {
                Action<Fixture>[] invalidate={
                    f=>f.Unit.attacked=true,f=>f.Unit.frozen=true,f=>f.Unit.Leader=true,f=>f.Unit.Follower=true,
                    f=>f.State.CurrentPlayer=2,f=>f.Unit.owner=2,f=>f.Unit.health=0,f=>f.Unit.type=UnitData.Type.warrior,
                    f=>f.Player.Currency=1,f=>f.Player.HarvestUnlocks.Clear(),f=>f.Source.resource=null,f=>f.Source.Explored=false,
                    f=>f.Source.resource!.type=ResourceData.Type.starfish,
                    f=>f.State.GameLogicData.Harvests[ResourceData.Type.fruit]=new(ImprovementData.Type.enchantanimal),
                    f=>f.State.GameLogicData.Harvests[ResourceData.Type.fruit].Population=0,
                    f=>f.Home.Improvement=null,f=>f.Home.owner=77,
                    f=>{f.Source.owner=2;f.Player.PeaceWith.Add(2);},f=>f.State.BasicCommandValid=false
                };
                foreach(var change in invalidate)
                {
                    var f=Setup();var command=Create(f);change(f);string before=Snapshot(f);
                    Check(!Valid(f,command));Check(!MimicSmuggleActions.TryCreate(f.State,f.Player,f.Source,out _,out _));
                    Check(Snapshot(f)==before,"Eligibility changed gameplay");
                }
                // Native resource/technology mapping is tribe-specific, and a
                // live enemy-owned home is still a legitimate population target.
                foreach(var resource in new[]{ResourceData.Type.fruit,ResourceData.Type.fish,ResourceData.Type.game})
                {
                    var f=Setup();f.Source.resource!.type=resource;f.Source.owner=byte.MaxValue;f.Home.owner=2;
                    Check(Valid(f,Create(f)));f.Source.owner=2;Check(Valid(f,Create(f)));
                }
                foreach(bool inaccessible in new[]{false,true})
                {
                    var f=Setup();var action=Queue(f,Create(f));
                    if(inaccessible)f.Source.Explored=false;else action.DeductCost=false;
                    string before=Snapshot(f);Check(!MimicSmuggleActions.ExecuteAction(action,f.State));
                    Check(Snapshot(f)==before&&f.Unit.ExhaustCalls==0,"Action boundary skipped access or paid-action validation");
                }
            });
            test("UI-092-smuggle-native-resource-mapper-survives-null-unlock-slot",()=> {
                foreach(bool removeResearch in new[]{false,true})
                {
                    var f=Setup();f.Source.resource!.type=ResourceData.Type.game;f.Player.HarvestUnlocks.Insert(0,null);
                    var data=f.State.GameLogicData;
                    Check(data.GetActionableImprovementForResource(ResourceData.Type.game,f.Player)?.type==ImprovementData.Type.hunting,
                        "Native resource mapper must skip the earlier null slot and retain researched Hunting");
                    Check(!data.IsUnlocked(ImprovementData.Type.hunting,f.Player),"Fixture must reproduce the legacy early-null false result");
                    string initial=Snapshot(f);var command=Create(f);Check(Valid(f,command)&&Snapshot(f)==initial);
                    var action=Queue(f,command);
                    if(removeResearch)
                    {
                        f.Player.HarvestUnlocks.Remove(ImprovementData.Type.hunting);string before=Snapshot(f);
                        Check(data.GetActionableImprovementForResource(ResourceData.Type.game,f.Player) is null);
                        Check(!Valid(f,command)&&!MimicSmuggleActions.TryCreate(f.State,f.Player,f.Source,out _,out _));
                        Check(!MimicSmuggleActions.ExecuteAction(action,f.State)&&Snapshot(f)==before&&f.Unit.ExhaustCalls==0,
                            "Removing actual Hunting research must reject offer, command and action without effects");
                    }
                    else
                    {
                        Drain(f);
                        Check(f.Player.Currency==5&&f.Source.ResourceRemovals==1&&f.Home.Population==5&&f.Unit.ExhaustCalls==1,
                            "A researched native Hunting mapping must execute despite legacy IsUnlocked returning false");
                    }
                }
            });
            test("UI-085-smuggle-cancel-and-native-command-scaffold-cost-nothing",()=> {
                var f=Setup();string original=Snapshot(f);var cancelled=Create(f);Check(Valid(f,cancelled)&&Snapshot(f)==original);
                MimicSmuggleActions.Clear();Check(!Valid(f,cancelled)&&Snapshot(f)==original);
                var command=Create(f);var oldBuild=new BuildAction(1,ImprovementData.Type.dcp_smuggle_fruit,f.Source.coordinates,true);
                f.State.ActionStack.Add(oldBuild);var queued=Queue(f,command);
                Check(f.State.NativeScaffolds==1&&f.State.ActionStack.Count==3&&!ReferenceEquals(oldBuild,queued));
                Check(f.Player.Currency==7&&!f.Unit.moved&&f.Source.resource is not null&&f.Home.Population==4);
                Throws(()=>Encode(oldBuild));Check(Encode(queued).Length==23,"Only the newly appended matching action should bind");
                var token=new InvalidOperationException("native command failure");Check(ReferenceEquals(MimicSmuggleActions.CommandFinished(token,null),token));
                foreach(bool bindingFailure in new[]{false,true})
                {
                    f=Setup();command=Create(f);var preserved=new CommandScaffoldAction();f.State.ActionStack.Add(preserved);
                    Check(MimicSmuggleActions.GuardCommand(command,f.State,out var scope));command.Execute(f.State);
                    Exception failure=token;
                    if(bindingFailure)
                    {
                        f.State.ActionStack.Add(new BuildAction(1,command.Type,command.Coordinates,true));
                        try{MimicSmuggleActions.CommandReturned(command,f.State,scope);throw new Exception("Expected duplicate native action rejection");}
                        catch(InvalidOperationException e){failure=e;}
                    }
                    Check(ReferenceEquals(MimicSmuggleActions.CommandFinished(failure,scope),failure));
                    Check(f.State.ActionStack.Count==1&&ReferenceEquals(f.State.ActionStack[0],preserved)&&f.Player.Currency==7&&!f.Unit.moved,
                        "Failed command did not preserve the preexisting native stack");
                }
            });
            test("UI-086-smuggle-native-effects-once-and-home-owner-attribution",()=> {
                foreach(byte owner in new byte[]{1,2})foreach(bool moved in new[]{false,true})
                {
                    var f=Setup();f.Unit.moved=moved;f.Home.owner=owner;f.State.GameLogicData.Harvests[ResourceData.Type.fruit].Population=2;
                    f.Unit.effects.Add(UnitEffect.Boosted);f.Unit.effects.Add(UnitEffect.Poisoned);
                    var action=Queue(f,Create(f));var retry=DecodeAction(Encode(action));Drain(f);
                    Check(f.Player.Currency==5&&f.State.Players[2].Currency==19&&f.Source.ResourceRemovals==1&&f.Source.resource is null);
                    Check(f.Home.Population==6&&f.Home.PopulationActions==2&&f.Home.LastPopulationPlayer==owner);
                    Check(f.Home.DeferredPopulationActions==(owner==2?2:0),"Population recipient was confused with payer");
                    Check(f.Unit.moved&&f.Unit.attacked&&f.Unit.ExhaustCalls==1&&Items(f.Unit.effects).SequenceEqual(new[]{UnitEffect.Poisoned}));
                    Check(f.Unit.health==10&&f.Unit.owner==1&&f.Unit.type==UnitData.Type.dcp_mimic_form_warrior&&f.State.NativeBuildExecutions==0);
                    string before=Snapshot(f);Check(!MimicSmuggleActions.ExecuteAction(retry,f.State));Check(Snapshot(f)==before,"Repeated action duplicated payment/effects");
                }
            });
            test("UI-087-smuggle-native-commit-failure-rolls-back-before-children",()=> {
                foreach(bool exhaustionFailure in new[]{false,true})foreach(bool moved in new[]{false,true})
                {
                    var f=Setup();f.Unit.moved=moved;f.Source.resource!.type=ResourceData.Type.game;f.Source.owner=2;Sanctuary(f,2,2,2);
                    f.Unit.effects.Add(UnitEffect.Boosted);f.Unit.effects.Add(UnitEffect.Poisoned);var action=Queue(f,Create(f));
                    var originalSubActions=action.subActions;var originalStack=Items(f.State.ActionStack);string before=Snapshot(f);
                    if(exhaustionFailure)f.Unit.ThrowExhaust=true;else action.ThrowAfterAppend=1;
                    Throws(()=>MimicSmuggleActions.ExecuteAction(action,f.State));
                    Check(Snapshot(f)==before&&Items(f.State.ActionStack).SequenceEqual(originalStack));
                    Check(ReferenceEquals(action.subActions,originalSubActions)&&f.Source.ResourceRemovals==0&&f.Home.PopulationActions==0);
                    action.ThrowAfterAppend=0;f.Unit.ThrowExhaust=false;Drain(f);Check(f.Player.Currency==5&&f.Source.ResourceRemovals==1&&f.Home.PopulationActions==1);
                }
            });
            test("UI-088-smuggle-population-visibility-survives-flat-child-reload",()=> {
                var f=Setup();var action=Queue(f,Create(f));Check(!MimicSmuggleActions.ExecuteAction(action,f.State));
                var population=Items(f.State.ActionStack).OfType<IncreasePopulationAction>().Single();
                foreach(bool reload in new[]{false,true})
                {
                    if(reload){MimicSmuggleActions.Clear();population=new(population.PlayerId,population.Source,population.Target,population.Delay);}
                    var reaction=new IncreasePopulationReaction{action=population};int callbacks=0;var done=new Il2CppSystem.Action(()=>callbacks++);
                    Check(MimicSmuggleActions.PopulationReaction(reaction,done)&&callbacks==0,"Visible native trajectory changed");
                    MapRenderer.Current!.Tiles[(3,2)].IsHidden=true;
                    Check(!MimicSmuggleActions.PopulationReaction(reaction,done)&&callbacks==1,"Hidden source trajectory leaked or callback stalled");
                    MapRenderer.Current.Tiles[(3,2)].IsHidden=false;MapRenderer.Current.Tiles[(1,1)].IsHidden=true;
                    Check(!MimicSmuggleActions.PopulationReaction(reaction,done)&&callbacks==2,"Hidden home trajectory leaked");
                    MapRenderer.Current.Tiles[(1,1)].IsHidden=false;
                }
                MimicSmuggleActions.Clear();MapRenderer.Current!.Tiles[(3,2)].IsHidden=true;
                int ordinaryCallbacks=0;var ordinaryDone=new Il2CppSystem.Action(()=>ordinaryCallbacks++);
                foreach(var ordinary in new[]{new IncreasePopulationAction(2,f.Source.coordinates,f.Home.coordinates,60),new IncreasePopulationAction(1,f.Source.coordinates,new(){X=9,Y=9},60)})
                    Check(MimicSmuggleActions.PopulationReaction(new IncreasePopulationReaction{action=ordinary},ordinaryDone)&&ordinaryCallbacks==0,"Unrelated population reaction changed");
                f.Unit.type=UnitData.Type.warrior;
                Check(MimicSmuggleActions.PopulationReaction(new IncreasePopulationReaction{action=new(1,f.Source.coordinates,f.Home.coordinates,60)},ordinaryDone));
            });
            test("UI-SANCTUARY-001-smuggle-refreshes-enemy-before-view-switch",()=> {
                var f=Setup();f.Source.resource!.type=ResourceData.Type.game;f.Source.owner=2;var sanctuary=Sanctuary(f,2,2,2);
                var player=GameManager.LocalPlayer;var turn=f.State.CurrentTurn;Queue(f,Create(f));Drain(f);
                Check(sanctuary.SanctuaryLevel==0&&sanctuary.SanctuaryLevelActions==1&&sanctuary.LastSanctuaryActor==2,
                    "Enemy Sanctuary level must fall under its actual owner in this completed command");
                Check(f.State.SanctuaryChecks.Count==1&&!f.State.SanctuaryChecks[0].ResourcePresent,
                    "Only the post-removal native adjacency pass may queue the downgrade");
                Check(ReferenceEquals(player,GameManager.LocalPlayer)&&f.State.CurrentTurn==turn&&f.State.CurrentPlayer==1,
                    "The correction must not require switching viewer or turn");
                Check(f.Player.Currency==5&&f.Home.Population==5&&f.Source.ResourceRemovals==1&&f.Unit.ExhaustCalls==1,
                    "Sanctuary update must preserve Smuggle's cost, home reward and actual action budget");
            });
            test("UI-SANCTUARY-002-neighbors-use-separate-owners-and-distant-control",()=> {
                var f=Setup();f.Source.resource!.type=ResourceData.Type.game;f.Source.owner=2;
                var one=Sanctuary(f,2,2,2);var two=Sanctuary(f,4,2,2);var foreign=Sanctuary(f,3,3,3);var far=Sanctuary(f,7,2,2);
                f.State.Map.Add(new TileData{coordinates=new(3,4),owner=3,resource=new(){type=ResourceData.Type.game}});
                Queue(f,Create(f));Drain(f);
                Check(one.SanctuaryLevel==0&&two.SanctuaryLevel==0&&one.SanctuaryLevelActions==1&&two.SanctuaryLevelActions==1,
                    "One removed animal may lower both neighboring Sanctuaries exactly once");
                Check(one.LastSanctuaryActor==2&&two.LastSanctuaryActor==2,"Enemy native level actions must use the Sanctuary owner rather than the smuggler");
                Check(foreign.SanctuaryLevel==1&&foreign.SanctuaryLevelActions==0&&foreign.LastSanctuaryActor==3,
                    "A foreign-owner Sanctuary counts only its own remaining animal, not the stolen opponent resource");
                Check(far.SanctuaryLevel==1&&far.SanctuaryUpdates==0&&far.SanctuaryLevelActions==0,"Distant improvement remains untouched");
            });
            test("UI-SANCTUARY-003-already-correct-level-and-duplicate-source-inert",()=> {
                var f=Setup();f.Source.resource!.type=ResourceData.Type.game;f.Source.owner=2;var sanctuary=Sanctuary(f,2,2,2,0);
                var resource=StageResource(f);Drain(f);
                Check(sanctuary.SanctuaryLevel==0&&sanctuary.SanctuaryLevelActions==0&&sanctuary.SanctuaryUpdates==1,"An already correct native level cannot receive another downgrade");
                int checks=f.State.SanctuaryChecks.Count;Throws(()=>ExecuteResource(f,resource));
                Check(f.State.SanctuaryChecks.Count==checks&&sanctuary.SanctuaryLevelActions==0&&f.Player.Currency==5&&f.Home.Population==5,
                    "Repeated source action cannot repeat refresh, population or setup debit");
            });
            test("UI-SANCTUARY-004-ordinary-resource-removal-keeps-native-order",()=> {
                var f=Setup();f.Source.resource!.type=ResourceData.Type.game;f.Source.owner=2;var sanctuary=Sanctuary(f,2,2,2);
                var ordinary=new DestroyResourceAction(1,f.Source.coordinates);f.State.ActionStack.Add(ordinary);ExecuteResource(f,ordinary);
                Check(f.Source.resource is null&&f.State.SanctuaryChecks.Count==1&&f.State.SanctuaryChecks[0].ResourcePresent,
                    "Unmarked ordinary resource removal must retain its native pre-removal check");
                Check(sanctuary.LastSanctuaryActor==1&&sanctuary.SanctuaryLevelActions==0&&sanctuary.SanctuaryLevel==1,
                    "Scoped Smuggle correction cannot rewrite an ordinary action owner or add another pass");
            });
            test("UI-SANCTUARY-005-flat-child-reload-retains-correction",()=> {
                var f=Setup();f.Source.resource!.type=ResourceData.Type.game;f.Source.owner=2;var sanctuary=Sanctuary(f,2,2,2);StageResource(f);
                // Reconstruct only native fields after private weak tags are
                // lost, matching native flattened pending-child persistence.
                var pending=Items(f.State.ActionStack).Select<ActionBase,ActionBase>(a=>a switch{
                    DestroyResourceAction d=>new DestroyResourceAction(d.PlayerId,new(d.Coordinates.X,d.Coordinates.Y)),
                    IncreasePopulationAction p=>new IncreasePopulationAction(p.PlayerId,new(p.Source.X,p.Source.Y),new(p.Target.X,p.Target.Y),p.Delay),_=>a}).ToArray();
                MimicSmuggleActions.Clear();f.State.ActionStack.Clear();foreach(var a in pending)f.State.ActionStack.Add(a);
                Drain(f);Check(sanctuary.SanctuaryLevel==0&&sanctuary.SanctuaryLevelActions==1&&sanctuary.LastSanctuaryActor==2,
                    "Reloaded native child fields must still bind this exhausted Mimic's pending Smuggle");
                Check(f.Player.Currency==5&&f.Home.Population==5&&f.Source.ResourceRemovals==1,"Resume cannot charge, remove or reward twice");
            });
            test("UI-SANCTUARY-006-native-exception-restores-private-scope",()=> {
                foreach(bool postFailure in new[]{false,true})
                {
                    var f=Setup();f.Source.resource!.type=ResourceData.Type.game;f.Source.owner=2;var sanctuary=Sanctuary(f,2,2,2);var resource=StageResource(f);
                    resource.ThrowBeforeRemoval=!postFailure;f.State.ThrowSanctuaryCheck=postFailure;
                    Throws(()=>ExecuteResource(f,resource));f.State.ThrowSanctuaryCheck=false;
                    int checks=f.State.SanctuaryChecks.Count;
                    ActionUtils.CheckSurroundingArea(f.State,1,f.Source);
                    Check(f.State.SanctuaryChecks.Count==checks+1&&sanctuary.LastSanctuaryActor==1,
                        "Finalizer must restore both deferral and owner override after native/body or post-refresh failure");
                    Check(f.Source.ResourceRemovals==(postFailure?1:0),"A failed pre-removal body must not clear the resource");
                }
            });
            test("UI-SANCTUARY-007-native-level-reaction-keeps-hidden-guards",()=> {
                foreach(string visibility in new[]{"visible","hidden","missing"})
                {
                    var f=Setup();f.Source.resource!.type=ResourceData.Type.game;f.Source.owner=2;var sanctuary=Sanctuary(f,2,2,2,1,visibility=="hidden");
                    var visual=MapRenderer.Current!.Tiles[(2,2)];if(visibility=="missing")MapRenderer.Current.Tiles.Remove((2,2));
                    var resource=StageResource(f);ExecuteResource(f,resource);f.State.ActionStack.Remove(resource);
                    var change=Items(f.State.ActionStack).OfType<ImprovementLevelDownAction>().Single();change.Execute(f.State);f.State.ActionStack.Remove(change);
                    int callbacks=0;NativeLevelReaction(change,ref callbacks);
                    Check(callbacks==1&&f.State.SanctuaryIncomeEvents.SequenceEqual(new byte[]{2})&&sanctuary.SanctuaryLevel==0,
                        "Native reaction completes once and publishes actual-owner income even when its tile is hidden");
                    Check(visual.SanctuaryPuffs==(visibility=="visible"?1:0)&&visual.SanctuaryRenders==(visibility=="visible"?1:0),
                        "Native visible reaction renders; hidden or absent tile never reveals Sanctuary art");
                }
            });
            test("UI-SANCTUARY-008-native-harvest-matching-flat-pattern-is-safe",()=> {
                var f=Setup();f.Source.resource!.type=ResourceData.Type.game;f.Source.owner=2;var sanctuary=Sanctuary(f,2,2,2);
                f.Unit.moved=f.Unit.attacked=true;
                // Native flattened children cannot prove which parent created
                // them. This ordinary harvest deliberately matches the same
                // exhausted-Mimic/source/home/recipient pattern without a tag.
                f.State.ActionStack.Add(new IncreasePopulationAction(1,f.Source.coordinates,f.Home.coordinates,60));
                f.State.ActionStack.Add(new DestroyResourceAction(1,f.Source.coordinates));Drain(f);
                Check(sanctuary.SanctuaryLevel==0&&sanctuary.SanctuaryLevelActions==1&&sanctuary.LastSanctuaryActor==2,
                    "The ambiguous ordinary harvest gets the same correct post-removal adjacency result");
                Check(f.Source.ResourceRemovals==1&&f.Home.Population==5&&f.Home.PopulationActions==1&&f.Player.Currency==7&&f.Unit.ExhaustCalls==0,
                    "Fallback must not add resource removal, population, debit or exhaustion beyond the existing native children");
            });
            test("UI-089-smuggle-button-uses-native-resource-confirmation-in-both-views",()=> {
                foreach(bool actual in new[]{false,true})foreach(bool moved in new[]{false,true})
                {
                    var f=Setup();f.Unit.moved=moved;var patches=new HarmonyLib.Harmony();MimicSmuggleUi.Install(patches);
                    Check(patches.Patched.Contains("InteractionBar::ClickedImprovement")&&patches.Patched.Contains("PopupManager::GetIconPopup"));
                    var (interaction,unit)=SelectUi(f);var bar=interaction.Bar;
                    var sourceSprite=bar.tile!.Resource!.Sprite;bar.tile=null; // Authoritative unit coordinates own the command.
                    if(actual)Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                    var button=SmuggleButton(bar);Check(button is not null,"Eligible Smuggle absent in "+(actual?"actual":"apparent")+" view");
                    Check(button!.Cost==2&&button.ButtonEnabled&&button.Active&&button.buttonActive&&
                        ReferenceEquals(button.AssignedSprite,sourceSprite),"Smuggle replaced native resource/cost presentation");
                    string before=Snapshot(f);PopupManager.SmuggleCallbacks=true;var click=button.OnClicked!;click.Click();click.Click();
                    Check(bar.ImprovementCalls==1&&bar.LastImprovement is not null&&MimicSmuggleActions.IsSmuggle(bar.LastImprovement));
                    var popup=PopupManager.LastIconPopup!;
                    Check(popup.Header=="Smuggle"&&popup.TopRightCost==2&&popup.Description.StartsWith("Real action.")&&popup.Description.Contains("home city"));
                    Check(popup.Building?.LastSprite?.Name=="harvestfruit"&&popup.Building.LastData?.type==ImprovementData.Type.dcp_smuggle_fruit,
                        "Native building renderer did not alias only its transient sprite");
                    Check(Snapshot(f)==before&&f.State.NativeScaffolds==0,"UI click spent or executed before native confirmation");
                    PopupManager.PopupShowing=false;
                    for(int i=0;i<4;i++)
                    {
                        Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                        Check(ReferenceEquals(SmuggleButton(bar),button)&&Snapshot(f)==before,
                            "Inspection mode changed the legal action set or authoritative state");
                    }
                }
            });
            test("UI-130-smuggle-both-views-retain-access-and-stale-click-guards",()=> {
                foreach(bool actual in new[]{false,true})
                foreach(string reason in new[]{"owner","turn","fog","hidden","wait","processing","popup","spectating","replay","selected","world","command","resource","spent"})
                {
                    var f=Setup();var (interaction,unit)=SelectUi(f);var bar=interaction.Bar;
                    if(actual)Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                    var button=SmuggleButton(bar);Check(button is not null,"Missing initial action control");var click=button!.OnClicked!;
                    if(reason=="owner")GameManager.LocalPlayer=new(){Id=2};
                    if(reason=="turn")f.State.CurrentPlayer=2;
                    if(reason=="fog")unit.Visible=false;
                    if(reason=="hidden")unit.Hidden=true;
                    if(reason=="wait")GameManager.Client.IsWaitingForCommand=true;
                    if(reason=="processing")GameManager.Client.ActionManager.IsProcessing=true;
                    if(reason=="popup")PopupManager.PopupShowing=true;
                    if(reason=="spectating")GameManager.Client.IsSpectating=true;
                    if(reason=="replay")GameManager.Client.IsReplay=true;
                    if(reason=="selected")interaction.selectedUnit=new();
                    if(reason=="world")GameManager.GameState=new();
                    if(reason=="command")MimicPresentationUi.CommandDispatch();
                    if(reason=="resource")f.Source.resource=null;
                    if(reason=="spent")f.Unit.attacked=true;
                    string before=Snapshot(f);click.Click();
                    Check(bar.ImprovementCalls==0&&Snapshot(f)==before,"Stale click dispatched during "+reason);
                    MimicPresentationUi.BarReady(bar);Check(SmuggleButton(bar) is null,"Control survived revoked "+reason+" context");
                    click.Click();Check(bar.ImprovementCalls==0&&Snapshot(f)==before,"Retired callback dispatched during "+reason);
                }
            });
            test("UI-093-smuggle-popup-cycle-keeps-native-widget-ownership",()=> {
                var f=Setup();var (interaction,unit)=SelectUi(f);Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                var bar=interaction.Bar;var first=SmuggleButton(bar)!;
                Check(bar.LastForceToBottom&&!Items(bar.quickActions.buttons).Contains(first),
                    "Smuggle must use the native bottom unit-action owner, not duplicate a quick-action widget");
                var nativeQuick=new UIRoundButton{Text="Native quick action"};bar.quickActions.AddButton(nativeQuick);
                var firstClick=first.OnClicked!;string before=Snapshot(f);PopupManager.SmuggleCallbacks=true;firstClick.Click();
                Check(PopupManager.PopupShowing&&bar.ImprovementCalls==1&&Snapshot(f)==before);
                MimicSmuggleUi.Sync(bar,unit,false);
                Check(first.gameObject.Destroyed&&!Items(bar.quickActions.buttons).Contains(first));
                Check(!nativeQuick.gameObject.Destroyed&&Items(bar.quickActions.buttons).Contains(nativeQuick),
                    "Removing Smuggle must preserve another native quick action");
                firstClick.Click();Check(bar.ImprovementCalls==1,"Retired popup source remained clickable");
                PopupManager.PopupShowing=false;MimicSmuggleUi.Sync(bar,unit,true);
                var second=SmuggleButton(bar)!;Check(!ReferenceEquals(first,second)&&bar.LastForceToBottom&&
                    !Items(bar.quickActions.buttons).Contains(second)&&Snapshot(f)==before);
                second.OnClicked!.Click();Check(bar.ImprovementCalls==2&&PopupManager.PopupShowing);
                MimicSmuggleUi.Sync(bar,unit,false);bar.ClearButtons();
                Check(!Items(bar.quickActions.buttons).Any()&&!Items(bar.buttons).Any(),
                    "Native clear must encounter no dangling Smuggle reference after a cancel/reopen cycle");
                Check(Snapshot(f)==before,"Popup lifecycle changed authoritative game fields");
            });
            test("UI-090-smuggle-stale-button-and-native-replacement-remain-safe",()=> {
                var f=Setup();var (interaction,unit)=SelectUi(f);Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                var bar=interaction.Bar;var button=SmuggleButton(bar)!;var stale=button.OnClicked!;
                f.Source.resource=null;string before=Snapshot(f);stale.Click();Check(bar.ImprovementCalls==0&&Snapshot(f)==before);
                MimicSmuggleUi.Sync(bar,unit,true);Check(SmuggleButton(bar) is null&&button.gameObject.Destroyed&&!button.ButtonEnabled);
                stale.Click();Check(bar.ImprovementCalls==0,"Retired offer dispatched from stale callback");
                f=Setup();(interaction,unit)=SelectUi(f);Check(MimicPresentationUi.ToggleInspection(interaction,unit));
                bar=interaction.Bar;button=SmuggleButton(bar)!;stale=button.OnClicked!;int nativeClicks=0;
                var native=new UIButtonBase.ButtonAction((_,_)=>nativeClicks++);button.Text="Native farm";button.OnClicked=native;
                MimicSmuggleUi.Remove(bar);Check(!button.gameObject.Destroyed&&ReferenceEquals(button.OnClicked,native)&&Items(bar.buttons).Contains(button));
                stale.Click();button.OnClicked!.Click();Check(bar.ImprovementCalls==0&&nativeClicks==1,"Private cleanup took native callback ownership");
                MimicPresentationUi.SelectionCleared();Check(!MimicPresentationUi.CanSmuggleView(unit));
            });
            test("UI-091-smuggle-popup-scope-and-sprite-alias-preserve-ordinary-ui",()=> {
                var f=Setup();var custom=Create(f);var ordinary=new BuildCommand(1,ImprovementData.Type.harvestfruit,f.Source.coordinates);
                MimicSmuggleUi.BeforeClicked(custom,out var outer);
                var data=new BasicPopup{Header="Native",Description="Ordinary",TopRightCost=9};MimicSmuggleUi.PopupCreated(data);
                MimicSmuggleUi.BeforeClicked(ordinary,out var inner);
                var native=new BasicPopup{Header="Native",Description="Ordinary",TopRightCost=9};MimicSmuggleUi.PopupCreated(native);
                Check(MimicSmuggleUi.AfterClicked(null,inner) is null&&native.Header=="Native"&&native.TopRightCost==9);
                Check(data.Header=="Native","Override ran before native popup setters completed");
                Check(MimicSmuggleUi.AfterClicked(null,outer) is null&&data.Header=="Smuggle"&&data.Description.Contains("home city")&&data.TopRightCost==9);
                MimicSmuggleUi.BeforeClicked(custom,out var failed);var untouched=new BasicPopup{Header="Native",Description="Ordinary"};MimicSmuggleUi.PopupCreated(untouched);
                var failure=new InvalidOperationException("Native popup factory failure");Check(ReferenceEquals(MimicSmuggleUi.AfterClicked(failure,failed),failure)&&untouched.Header=="Native");
                native=new(){Header="Native",Description="Ordinary",TopRightCost=9};MimicSmuggleUi.PopupCreated(native);Check(native.Header=="Native"&&native.TopRightCost==9);
                foreach(var (carrier,expected) in new[]{(ImprovementData.Type.dcp_smuggle_fruit,ImprovementData.Type.harvestfruit),
                    (ImprovementData.Type.dcp_smuggle_fish,ImprovementData.Type.fishing),(ImprovementData.Type.dcp_smuggle_animal,ImprovementData.Type.hunting),
                    (ImprovementData.Type.farm,ImprovementData.Type.farm)})
                {var image=carrier;MimicSmuggleUi.SpriteAlias(ref image);Check(image==expected);image=carrier;MimicSmuggleUi.TransientSpriteAlias(ref image);Check(image==expected);}
                Check(custom.Type==ImprovementData.Type.dcp_smuggle_fruit,"Sprite alias changed authoritative command carrier");
            });
        }
    }
}
