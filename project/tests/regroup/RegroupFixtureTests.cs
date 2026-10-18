using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using Polytopia.Data;

namespace Deception.Tests
{
    public static class RegroupFixtureTests
    {
        static readonly List<object> Results=new();static int Failed;
        static (ClientInteraction Interaction,Unit Unit)? Last;
        static void Check(bool value,string message){if(!value)throw new Exception(message);}
        static void Throws(Action action){try{action();}catch{return;}throw new Exception("Expected rejection");}
        static void BeginBoundary()
        {
            NativeRegroupSession.BoundaryEvents.Clear();NativeRegroupSession.PrepareCalls=0;
            NativeRegroupSession.RequirePrepared=true;NativeRegroupSession.PreparedState=null;
            NativeRegroupSession.PrepareFailure=null;
        }
        static bool ScopeClear()=>new[]{"ExecutingState","ExecutingAction"}.All(name=>
            typeof(NativeExchangeCommands).GetField(name,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null) is null);
        static bool HasRegroup(InteractionBar bar)=>Native.Items(bar.buttons).Cast<UIRoundButton>().Any(b=>b.Text=="Regroup"&&b.ButtonEnabled);
        static int CachedBars=>((System.Collections.IDictionary)typeof(MimicRegroupUi)
            .GetField("EligibilityCache",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!).Count;
        static void Test(string id,Action test)
        {
            try{test();Results.Add(new{id,status="PASS"});Console.WriteLine("PASS "+id);}
            catch(Exception e){Failed++;Results.Add(new{id,status="FAIL",error=e.ToString()});Console.WriteLine("FAIL "+id+": "+e.Message);}
            finally{MimicRegroupUi.Clear();NativeExchangeCommit.During=null;}
        }
        static (ClientInteraction Interaction,Unit Unit) Setup(bool propaganda=false)
        {
            MimicRegroupUi.Clear();if(Last is not null)MimicRegroupUi.NativeClear(Last.Value.Interaction.Bar);
            MimicPresentationUi.Clear();MimicPresentationState.Clear();MimicPresentationState.ReadAppearance=null;MimicPresentationState.ReadIdentity=null;Entry.Notes.Clear();
            NativeRegroupSession.Enabled=true;NativeRegroupSession.Faulted=false;NativeRegroupSession.Faults.Clear();NativeGameStateEnvelope.Revision=0;NativeExchangeCommit.Calls=0;
            NativeRegroupSession.RequirePrepared=false;NativeRegroupSession.PreparedState=null;NativeRegroupSession.PrepareFailure=null;
            NativeRegroupSession.PrepareCalls=0;NativeRegroupSession.BoundaryEvents.Clear();
            NativeExchangePlanning.Calls=0;NativeExchangePlanning.Available=true;NativeExchangePlanning.AllowDisguise=propaganda;NativeLedgerState.CaptureCalls=0;NativeLedgerState.FailCapture=false;
            MapRenderer.Current=new();PolytopiaInput.CurrentTouches=new();PolytopiaInput.CurrentTouches.Add(new(){phase=UnityEngine.TouchPhase.Ended});PolytopiaInput.UIHitTest=null;PolytopiaInput.OutsideWindow=false;CameraController.IsDragging=false;CameraController.IsBlocked=false;
            GameManager.GameState=new();GameManager.LocalPlayer=new(){Id=1,Propaganda=propaganda};GameManager.Client=new();
            GameManager.GameState.Players[1]=GameManager.LocalPlayer;
            PopupManager.PopupShowing=false;PopupManager.ProductionCallbacks=false;PopupManager.RegroupCallbacks=true;PopupManager.Current=null;PopupManager.LastBasic=null;
            RecruitmentHooks.Registered=true;RecruitmentHooks.Forms.Clear();
            RecruitmentHooks.Forms.Add("dcp_mimic_form_warrior",new("warrior","dcp_mimic_form_warrior","Warrior",10,1,2,2,false));
            var a=new UnitState{id=3,coordinates=new(3,2)};
            var b=new UnitState{id=4,type=UnitData.Type.warrior,UnitData=new(UnitData.Type.warrior),coordinates=new(4,2)};
            foreach(var u in new[]{a,b}){GameManager.GameState.Units[u.id]=u;GameManager.GameState.Map.Add(new(){coordinates=u.coordinates,unit=u,owner=1});}
            var unit=new Unit{UnitState=a};
            foreach(var rendered in new[]{unit,new Unit{UnitState=b}})
            {
                MapRenderer.Current.Register(rendered);
                var tile=new Tile{Coordinates=rendered.UnitState.coordinates,Data=GameManager.GameState.Map.Tiles[(rendered.UnitState.coordinates.X,rendered.UnitState.coordinates.Y)],Unit=rendered};
                tile.overlayRenderer.transform.SetParent(tile.transform,false);tile.overlayRenderer.transform.localPosition=new(1,2,3);
                MapRenderer.Current.Tiles[(tile.Coordinates.X,tile.Coordinates.Y)]=tile;
            }
            var body=new UnityEngine.SpriteRenderer{sprite=new UnityEngine.Sprite("Native body"){rect=new(40,60)}};
            body.transform.SetParent(unit.spriteContainer,false);
            unit.skinVisuals.visualParts=new(new[]{new SkinVisualsReference.VisualPart{renderer=new(){spriteRenderer=body}}});
            UIManager.Instance=new();
            var interaction=new ClientInteraction();interaction.Bar.spriteDuplicationData.source=unit.spriteContainer;interaction.SelectUnit(unit);
            MimicPresentationUi.Selected(interaction,unit);MimicPresentationUi.BarReady(interaction.Bar);
            Last=(interaction,unit);return Last.Value;
        }
        static object[] MarkerObjects()
        {
            var active=typeof(MimicRegroupUi).GetField("Active",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!.GetValue(null)!;
            return ((System.Collections.IEnumerable)active.GetType().GetField("Markers",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(active)!).Cast<object>().ToArray();
        }
        static Tile[] Targets()=>MarkerObjects().Select(x=>(Tile)x.GetType().GetProperty("Tile")!.GetValue(x)!).ToArray();
        static UnityEngine.GameObject[] TargetArt()=>MarkerObjects().Select(x=>(UnityEngine.GameObject)x.GetType().GetProperty("Object")!.GetValue(x)!).ToArray();
        static void Click(Tile? tile,int touch=0)
        {Check(!MimicRegroupUi.Released(Last!.Value.Interaction,tile,touch),"Target release leaked to native action dispatch");}
        static Tile[] First(bool actual=false,NativeExchangeKind kind=NativeExchangeKind.Regroup)
        {
            var (interaction,unit)=Setup(kind==NativeExchangeKind.Disguise);
            if(actual)Check(MimicPresentationUi.ToggleInspection(interaction,unit),"Actual inspection could not be selected");
            var button=Native.Items(interaction.Bar.buttons).Cast<UIRoundButton>().SingleOrDefault(x=>x.Text==kind.ToString());
            Check(button is not null,"Regroup control was not created: "+string.Join(";",Entry.Notes.Select(x=>x.Message)));
            Check(interaction.Bar.LastForceToBottom&&!Native.Items(interaction.Bar.quickActions.buttons).Any(x=>Native.Same(x,button)),"Wrong native button owner");
            button!.OnClicked!.Click();Check(MimicRegroupUi.Choosing&&!MimicRegroupUi.GuardCommands(),"Choice did not acquire command guard: "+string.Join(";",Entry.Notes.Select(x=>x.Message)));
            MimicRegroupUi.Tick();
            Check(MimicRegroupUi.Choosing&&PopupManager.LastBasic is null&&!PopupManager.PopupShowing,"Unexpected action popup or canceled targets: "+string.Join(";",Entry.Notes.Select(x=>x.Message)));
            return Targets();
        }
        static Tile[] Second(Tile[] first)
        {
            Click(first[0]);MimicRegroupUi.Tick();
            var second=Targets();Check(second.Length==2,"Keep and Swap target destinations absent");return second;
        }
        static (BuildCommand Command,NativeExchangeIntent Intent) Command()
        {
            Setup();Check(NativeExchangeCommands.TryOffer(GameManager.GameState,3,4,false,out var intent,out var reason),reason);
            return ((BuildCommand)NativeExchangeCommands.Create(intent!),intent!);
        }
        static BuildAction Scaffold(BuildCommand command)
        {
            var stack=GameManager.GameState.ActionStack;
            stack.Add(new EndCommandAction{PlayerId=command.PlayerId,commandType=command.GetCommandType()});
            stack.Add(new RunDeferredAction{PlayerId=command.PlayerId});
            var action=new BuildAction(command.PlayerId,command.Type,command.Coordinates,true);stack.Add(action);return action;
        }
        public static int Run()
        {
            Test("regroup-native-hook-coverage",()=>{
                var h=new HarmonyLib.Harmony();NativeExchangeCommands.Install(h);MimicRegroupUi.Install(h);
                foreach(string name in new[]{"BuildCommand::IsValid","BuildCommand::Execute","BuildAction::Execute","BuildAction::ExecuteDefault","BuildAction::ExecuteV105",
                    "InteractionBar::ClearButtons","InteractionBar::Reset","InteractionBar::DeInit","InteractionBar::OnDestroy",
                    "ClientBase::SendCommand","ClientBase::SendCommandRemote","ClientInteraction::ValidateMove","ClientInteraction::OnPress","ClientInteraction::OnRelease","Unit::UpdateOverlays"})
                    Check(h.Patched.Contains(name),"Missing installed boundary "+name);
            });
            Test("regroup-both-inspection-views-cancel-without-gameplay",()=>{
                foreach(bool actual in new[]{false,true})
                {
                    var targets=First(actual);var art=TargetArt();var source=Last!.Value.Unit.UnitState;int currency=GameManager.LocalPlayer.Currency;
                    Check(!MimicPresentationUi.ToggleInspection(Last.Value.Interaction,Last.Value.Unit),"Inspection stole target/choice input");
                    Click(null);
                    Check(!MimicRegroupUi.Choosing&&MimicRegroupUi.GuardCommands()&&art.All(x=>!x),"Cancel retained private choice authority/art");
                    Check(GameManager.Client.Sent==0&&GameManager.LocalPlayer.Currency==currency&&!source.moved&&!source.attacked,"Cancel changed gameplay");
                    Check(NativeRegroupSession.PrepareCalls==0,"Read-only offers or cancellation prepared the session");
                }
            });
            Test("regroup-destination-art-failure-cleans-only-owned-resources",()=>{
                var first=First();var source=first[0].attackOverlay;int allocations=UnityEngine.Texture2D.Allocations.Count;
                UnityEngine.Graphics.Failure="load";
                try{Click(first[0]);}finally{UnityEngine.Graphics.Failure="";}
                Check(!MimicRegroupUi.Choosing&&GameManager.Client.Sent==0,"Failed target texture left action authority");
                Check(UnityEngine.Texture2D.Allocations.Count==allocations+1&&UnityEngine.Texture2D.Allocations.Last().Destroyed,"Failed target upload leaked its owned texture");
                Check(source&&source.texture&&!source.Destroyed,"Failed target creation altered native sprite or atlas");
                // A fresh process could retry. Reset only this synthetic static
                // attempted flag before the subsequent successful-path test.
                typeof(MimicRegroupUi).GetField("DestinationAttempted",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static)!.SetValue(null,false);
            });
            Test("regroup-destination-targets-are-green-bounded-and-aligned",()=>{
                var first=First();var borrowed=first[0].attackOverlay;
                var red=TargetArt()[0].FindComponent<UnityEngine.SpriteRenderer>()!;
                Check(ReferenceEquals(red.sprite,borrowed)&&red.color==new UnityEngine.Color(1,1,1,1),"First stage changed native red artwork");
                var second=Second(first);var green=TargetArt().Select(x=>x.FindComponent<UnityEngine.SpriteRenderer>()!).ToArray();
                Check(green.All(x=>!ReferenceEquals(x.sprite,borrowed)&&x.color.g>.9f&&x.color.r<.3f&&x.color.b<.5f),"Second stage did not use distinct green artwork");
                Check(ReferenceEquals(green[0].sprite,green[1].sprite)&&green[0].sprite!.texture.width==96&&green[0].sprite!.texture.height==96,"Destination cache is not one bounded texture");
                var pixels=MimicRegroupUi.DestinationPixels();Check(pixels.Length==96*96*4&&pixels[3]==0&&pixels[((48*96)+48)*4+3]>0,"Destination target alpha/canvas is invalid");
                Check(Enumerable.Range(0,96*96).Count(i=>pixels[i*4+3]>0)>900&&Enumerable.Range(0,96*96).Count(i=>pixels[i*4+3]>0)<5000,"Bullseye is empty or a solid rectangle");
                for(int i=0;i<green.Length;i++)
                {
                    var source=second[i].overlayRenderer;var bounds=second[i].attackOverlay.bounds;
                    Check(green[i].transform.position==source.transform.TransformPoint(bounds.center),"Destination shifted from native target center");
                    Check(Math.Abs(green[i].bounds.size.x-bounds.size.x*source.transform.lossyScale.x)<.0001f&&Math.Abs(green[i].bounds.size.y-bounds.size.y*source.transform.lossyScale.y)<.0001f,"Destination changed native target visible dimensions");
                }
                int allocations=UnityEngine.Texture2D.Allocations.Count;var cached=green[0].sprite;Click(null);
                Second(First());Check(UnityEngine.Texture2D.Allocations.Count==allocations&&ReferenceEquals(TargetArt()[0].FindComponent<UnityEngine.SpriteRenderer>()!.sprite,cached),"Repeated targeting allocated new destination textures");
            });
            Test("regroup-idle-checks-do-not-allocate-native-neighborhood-arrays",()=>{
                var (interaction,unit)=Setup();int count=MapDataExtensions.AreaReads;
                for(int i=0;i<60;i++)MimicRegroupUi.Sync(interaction.Bar,unit,true);
                Check(MapDataExtensions.AreaReads==count,"Idle eligibility repeatedly allocated native sorted areas");
                First();count=MapDataExtensions.AreaReads;for(int i=0;i<60;i++)MimicRegroupUi.Tick();
                Check(MimicRegroupUi.Choosing&&MapDataExtensions.AreaReads==count,"Target frame checks repeatedly allocated native sorted areas");
            });
            Test("regroup-native-map-positive-choice",()=>{
                var first=First();Check(first.Length==1&&first[0].Unit!.UnitState.id==4,"First target was not adjacent ally");
                var second=Second(first);Check(second[0].Unit!.UnitState.id==3&&second[1].Unit!.UnitState.id==4,"Destinations do not identify initiating Mimic positions");
                var art=TargetArt();Click(second[0]);Check(art.All(x=>!x),"Sent command retained target artwork");
                Check(GameManager.Client.Sent==1&&!MimicRegroupUi.Choosing&&MimicRegroupUi.GuardCommands(),"Choice failed to dispatch once/release guard");
                Check(MimicRegroupUi.Released(Last!.Value.Interaction,second[0],0)&&GameManager.Client.Sent==1,"Completed target session consumed unrelated input");
                Check(NativeExchangeCommands.Get(GameManager.Client.SentCommand!) is not null,"Complete intent missing on sent command");
                Check(NativeRegroupSession.PrepareCalls==0,"Read-only choice validation prepared the session before execution");
            });
            Test("regroup-idle-positive-and-negative-eligibility-cache",()=>{
                var (interaction,unit)=Setup();var bar=interaction.Bar;
                Check(HasRegroup(bar)&&NativeExchangePlanning.Calls>0,"Positive eligibility baseline absent");
                int planned=NativeExchangePlanning.Calls;
                for(int i=0;i<60;i++)MimicPresentationUi.BarReady(bar);
                Check(NativeExchangePlanning.Calls==planned&&HasRegroup(bar),"Idle positive bar repeated planning");
                NativeExchangePlanning.Available=false;NativeGameStateEnvelope.Revision++;
                MimicRegroupUi.Sync(bar,unit,true);Check(!HasRegroup(bar)&&NativeExchangePlanning.Calls>planned,"Negative result was not freshly evaluated");
                planned=NativeExchangePlanning.Calls;
                for(int i=0;i<60;i++)MimicPresentationUi.BarReady(bar);
                Check(NativeExchangePlanning.Calls==planned&&!HasRegroup(bar),"Idle negative bar repeated planning");
                NativeExchangePlanning.Available=true;GameManager.GameState.CurrentCommand++;
                MimicRegroupUi.Sync(bar,unit,true);Check(HasRegroup(bar)&&NativeExchangePlanning.Calls>planned,"Committed command did not invalidate negative eligibility");
                Check(NativeRegroupSession.PrepareCalls==0,"Eligibility cache enrolled or prepared a native state");
            });
            Test("regroup-same-revision-ledger-binding-refreshes-negative-and-positive-hints",()=>{
                foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})
                {
                    var (interaction,unit)=Setup(true);var bar=interaction.Bar;var state=GameManager.GameState;
                    NativeExchangePlanning.Available=false;NativeGameStateEnvelope.Rebind();MimicRegroupUi.Sync(bar,unit,true);
                    Check(MimicRegroupUi.OwnedButton(bar,kind) is null,"Negative enrollment-window result was not cached");
                    int calls=NativeExchangePlanning.Calls;var binding=NativeGameStateEnvelope.Get(state);uint turn=state.CurrentTurn;ushort command=state.CurrentCommand;
                    NativeExchangePlanning.Available=true;NativeGameStateEnvelope.Rebind();MimicRegroupUi.Sync(bar,unit,true);
                    Check(!ReferenceEquals(binding,NativeGameStateEnvelope.Get(state))&&NativeGameStateEnvelope.Revision==0&&
                        MimicRegroupUi.OwnedButton(bar,kind) is not null&&NativeExchangePlanning.Calls>calls,"Same-revision enrollment completion retained negative hint");
                    calls=NativeExchangePlanning.Calls;for(int n=0;n<30;n++)MimicRegroupUi.Sync(bar,unit,true);
                    Check(NativeExchangePlanning.Calls==calls,"Stable immutable binding did not retain bounded idle cache");
                    NativeExchangePlanning.Available=false;NativeGameStateEnvelope.Rebind();MimicRegroupUi.Sync(bar,unit,true);
                    Check(MimicRegroupUi.OwnedButton(bar,kind) is null&&NativeExchangePlanning.Calls>calls,"Same-revision rollback retained positive hint");
                    Check(state.CurrentTurn==turn&&state.CurrentCommand==command&&!unit.UnitState.moved&&!unit.UnitState.attacked&&NativeRegroupSession.PrepareCalls==0,"Hint refresh changed authoritative state/actions");
                }
            });
            Test("regroup-same-revision-ledger-binding-revokes-both-target-stages",()=>{
                foreach(bool second in new[]{false,true})foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})
                {
                    var targets=First(kind:kind);if(second)Second(targets);var art=TargetArt();
                    NativeGameStateEnvelope.Rebind();MimicRegroupUi.Tick();
                    Check(!MimicRegroupUi.Choosing&&art.All(x=>!x)&&GameManager.Client.Sent==0&&NativeRegroupSession.PrepareCalls==0,"Replacement ledger retained an actionable stale target");
                }
            });
            Test("regroup-newly-born-spent-neighbor-invalidates-empty-cell-cache",()=>{
                var (interaction,unit)=Setup(true);var game=GameManager.GameState;var tile=game.Map.Tiles[(4,2)];tile.unit=null;game.Units.Remove(4);
                MimicRegroupUi.Sync(interaction.Bar,unit,true);Check(!HasRegroup(interaction.Bar),"Empty neighborhood offered a partner");
                var born=new UnitState{id=5,type=UnitData.Type.warrior,UnitData=new(UnitData.Type.warrior),coordinates=new(4,2),moved=true,attacked=true};
                tile.unit=born;game.Units[5]=born;MimicRegroupUi.Sync(interaction.Bar,unit,true);
                Check(HasRegroup(interaction.Bar)&&MimicRegroupUi.OwnedButton(interaction.Bar,NativeExchangeKind.Disguise) is not null,"Newly trained spent ally remained unavailable");
                Check(born.moved&&born.attacked&&!unit.UnitState.moved&&!unit.UnitState.attacked&&NativeRegroupSession.PrepareCalls==0,"Showing new partner refreshed or spent actions");
            });
            Test("regroup-eligibility-cache-invalidates-authority-and-unit-changes",()=>{
                foreach(string changed in new[]{"command","revision","turn","actor","client","viewer","state","map","unit-state","type","owner","moved","attacked","health","frozen","coordinates"})
                {
                    var (interaction,unit)=Setup();var bar=interaction.Bar;var state=GameManager.GameState;int planned=NativeExchangePlanning.Calls;
                    if(changed=="command")state.CurrentCommand++;if(changed=="revision")NativeGameStateEnvelope.Revision++;
                    if(changed=="turn")state.CurrentTurn++;if(changed=="actor")state.CurrentPlayer=2;
                    if(changed=="client")GameManager.Client=new();if(changed=="viewer")GameManager.LocalPlayer=new(){Id=1};
                    if(changed=="state")GameManager.GameState=new(){Units=state.Units,Map=state.Map,Players=state.Players,CurrentTurn=state.CurrentTurn};
                    if(changed=="map")state.Map=new(){Tiles=state.Map.Tiles};
                    if(changed=="unit-state"){var replacement=new UnitState{id=3,coordinates=new(3,2)};unit.UnitState=replacement;state.Units[3]=replacement;state.Map.Tiles[(3,2)].unit=replacement;}
                    if(changed=="type")unit.UnitState.type=UnitData.Type.warrior;if(changed=="owner")unit.UnitState.owner=2;
                    if(changed=="moved")unit.UnitState.moved=true;if(changed=="attacked")unit.UnitState.attacked=true;
                    if(changed=="health")unit.UnitState.health=9;if(changed=="frozen")unit.UnitState.frozen=true;
                    if(changed=="coordinates")unit.UnitState.coordinates=new(2,2);
                    MimicRegroupUi.Sync(bar,unit,true);
                    Check(NativeExchangePlanning.Calls>planned||!HasRegroup(bar),"Stale positive cache survived "+changed);
                }
                {
                    var (interaction,unit)=Setup();int planned=NativeExchangePlanning.Calls;
                    MimicRegroupUi.Sync(interaction.Bar,unit,false);Check(!HasRegroup(interaction.Bar)&&CachedBars==0,"Revoked access retained control/cache");
                    MimicRegroupUi.Sync(interaction.Bar,unit,true);Check(HasRegroup(interaction.Bar)&&NativeExchangePlanning.Calls>planned,"Restored access reused revoked eligibility");
                    planned=NativeExchangePlanning.Calls;NativeRegroupSession.Fault("Synthetic lifecycle fault");MimicRegroupUi.Sync(interaction.Bar,unit,true);
                    Check(!HasRegroup(interaction.Bar)&&CachedBars==0&&NativeExchangePlanning.Calls==planned,"Faulted session retained positive control or planned again");
                }
            });
            Test("regroup-target-idle-does-not-plan-and-clicks-revalidate",()=>{
                var first=First();int planned=NativeExchangePlanning.Calls;
                for(int i=0;i<60;i++)MimicRegroupUi.Tick();
                Check(MimicRegroupUi.Choosing&&NativeExchangePlanning.Calls==planned,"First target stage repeated planning");
                var second=Second(first);planned=NativeExchangePlanning.Calls;
                for(int i=0;i<60;i++)MimicRegroupUi.Tick();
                Check(MimicRegroupUi.Choosing&&NativeExchangePlanning.Calls==planned,"Destination stage repeated planning");
                NativeExchangePlanning.Available=false;Click(second[0]);
                Check(GameManager.Client.Sent==0&&!MimicRegroupUi.Choosing&&NativeExchangePlanning.Calls>planned,"Confirmation trusted stale cached eligibility");
                var (interaction,unit)=Setup();var button=Native.Items(interaction.Bar.buttons).Cast<UIRoundButton>().Single(b=>b.Text=="Regroup");
                planned=NativeExchangePlanning.Calls;NativeExchangePlanning.Available=false;button.OnClicked!.Click();
                Check(!MimicRegroupUi.Choosing&&GameManager.Client.Sent==0&&!HasRegroup(interaction.Bar)&&NativeExchangePlanning.Calls>planned,"Initial click trusted cached eligibility");
            });
            Test("regroup-eligibility-cache-is-bounded-and-cleared",()=>{
                var (interaction,unit)=Setup();
                var bars=new List<InteractionBar>();
                for(int i=0;i<12;i++){var bar=new InteractionBar{unit=unit};bars.Add(bar);MimicRegroupUi.Sync(bar,unit,true);Check(CachedBars<=8,"Eligibility roots grew without bound");}
                foreach(var bar in bars)MimicRegroupUi.Remove(bar);
                NativeExchangePlanning.Available=false;NativeGameStateEnvelope.Revision++;MimicRegroupUi.Sync(interaction.Bar,unit,true);
                Check(CachedBars>0,"Negative eligibility was not cached");
                MimicRegroupUi.NativeClear(interaction.Bar);Check(CachedBars==0,"Native bar cleanup retained negative cache");
                MimicRegroupUi.Sync(interaction.Bar,unit,true);MimicRegroupUi.Remove(interaction.Bar);Check(CachedBars==0,"Explicit removal retained cache");
                MimicRegroupUi.Sync(interaction.Bar,unit,true);MimicRegroupUi.Clear();Check(CachedBars==0,"Session cleanup retained cache");
            });
            Test("regroup-target-release-cannot-choose-both-stages",()=>{
                var first=First();var art=TargetArt();Click(first[0]);
                Check(art.All(x=>!x),"Old target artwork was not destroyed");
                Click(first[0]);Check(MimicRegroupUi.Choosing&&GameManager.Client.Sent==0,"One release selected both stages");
                MimicRegroupUi.Tick();Click(Targets()[1]);Check(GameManager.Client.Sent==1&&NativeExchangeCommands.Get(GameManager.Client.SentCommand!)!.Swap,"Swap destination did not dispatch swap");
            });
            Test("regroup-current-tile-reuse-revokes-target",()=>{
                var second=Second(First());var native=second[0].overlayRenderer;second[0].Data=new();Click(second[0]);
                Check(GameManager.Client.Sent==0&&!MimicRegroupUi.Choosing,"Reused tile dispatched a stale target");
                Check(native&&!native.gameObject.Destroyed,"Cleanup destroyed native overlay source");
            });
            Test("regroup-cancel-control-and-native-cleanup-retire-targets",()=>{
                foreach(bool nativeClear in new[]{false,true})
                {
                    First();var art=TargetArt();var bar=Last!.Value.Interaction.Bar;
                    var cancel=Native.Items(bar.buttons).Cast<UIRoundButton>().Single(x=>x.Text=="Cancel");
                    Check(Native.Same(MimicRegroupUi.OwnedButton(bar),cancel),"Cancel lost owned Regroup slot");
                    MimicPresentationUi.BarReady(bar);Check(MimicRegroupUi.Choosing&&Native.Same(MimicRegroupUi.OwnedButton(bar),cancel),"Bar refresh retired Cancel");
                    if(nativeClear)MimicRegroupUi.NativeClear(bar);else cancel.OnClicked!.Click();
                    Check(!MimicRegroupUi.Choosing&&art.All(x=>!x)&&GameManager.Client.Sent==0,"Cleanup retained target authority or spent action");
                    Check(nativeClear?Last.Value.Unit.OverlayRefreshes==0:Last.Value.Unit.OverlayRefreshes>0,"Teardown rebuilt native overlays or Cancel failed to restore them");
                }
            });
            Test("regroup-cancel-refresh-cannot-reenter-target-creation",()=>{
                First();var (interaction,unit)=Last!.Value;var cancel=(UIRoundButton)MimicRegroupUi.OwnedButton(interaction.Bar)!;var art=TargetArt();
                unit.DuringOverlayRefresh=()=>{MimicRegroupUi.Sync(interaction.Bar,unit,true);cancel.OnClicked!.Click();};
                try{cancel.OnClicked!.Click();}finally{unit.DuringOverlayRefresh=null;}
                Check(!MimicRegroupUi.Choosing&&art.All(x=>!x)&&GameManager.Client.Sent==0&&unit.OverlayRefreshes==1,"Cancel refresh reentered targeting or recursively rebuilt overlays");
            });
            Test("regroup-neighbor-movement-and-readiness-invalidates-hints",()=>{
                foreach(string change in new[]{"moved","attacked","health","frozen","type","owner","coordinates","occupant"})
                {
                    var (interaction,unit)=Setup();var b=GameManager.GameState.Units[4];int planned=NativeExchangePlanning.Calls;
                    if(change=="moved")b.moved=true;if(change=="attacked")b.attacked=true;if(change=="health")b.health=50;
                    if(change=="frozen")b.frozen=true;if(change=="type")b.type=UnitData.Type.dcp_mimic_form_warrior;if(change=="owner")b.owner=2;
                    if(change=="coordinates")b.coordinates=new(4,3);if(change=="occupant")GameManager.GameState.Map.Tiles[(4,2)].unit=null;
                    MimicRegroupUi.Sync(interaction.Bar,unit,true);
                    Check(NativeExchangePlanning.Calls>planned||!HasRegroup(interaction.Bar),"Neighbor change reused old hint: "+change);
                    Check(!unit.UnitState.moved&&!unit.UnitState.attacked,"Hint refresh moved initiator");
                }
            });
            Test("regroup-drag-ui-and-invalid-touch-do-not-commit",()=>{
                foreach(string input in new[]{"drag","blocked","outside","ui-start","ui-end","invalid-tap","multi-touch","negative-index","missing-touch","canceled-touch"})
                {
                    var second=Second(First());var interaction=Last!.Value.Interaction;
                    if(input=="drag")CameraController.IsDragging=true;if(input=="blocked")CameraController.IsBlocked=true;
                    if(input=="outside")PolytopiaInput.OutsideWindow=true;if(input=="ui-start")interaction.wasDownOverUI=true;
                    if(input=="ui-end")PolytopiaInput.UIHitTest=_=>true;if(input=="invalid-tap")interaction.ValidTap=false;
                    if(input=="multi-touch"){PolytopiaInput.CurrentTouches.Add(new());PolytopiaInput.CurrentTouches.Add(new());}
                    if(input=="missing-touch")PolytopiaInput.CurrentTouches.Clear();
                    if(input=="canceled-touch")PolytopiaInput.CurrentTouches[0].phase=UnityEngine.TouchPhase.Canceled;
                    int checks=interaction.TapChecks;Click(second[0],input=="negative-index"?-1:0);Check(GameManager.Client.Sent==0,"Input guard failed: "+input);
                    if(input=="negative-index")Check(interaction.TapChecks==checks,"Negative index reached unsafe native tap helper");MimicRegroupUi.Clear();
                }
            });
            Test("regroup-confirmed-intent-does-not-silently-rebase",()=>{
                var second=Second(First());NativeGameStateEnvelope.Revision++;
                Click(second[0]);Check(GameManager.Client.Sent==0,"Stale displayed intent was rebuilt and sent");
            });
            Test("regroup-session-retains-owner-selection-visibility-guards",()=>{
                foreach(string reason in new[]{"owner","turn","fog","hidden","replay","spectating","selected","waiting","processing","foreign-popup"})
                {
                    var second=Second(First());var unit=Last!.Value.Unit;
                    if(reason=="owner")unit.UnitState.owner=2;if(reason=="turn")GameManager.GameState.CurrentTurn++;
                    if(reason=="fog")unit.Visible=false;if(reason=="hidden")unit.Hidden=true;
                    if(reason=="replay")GameManager.Client.IsReplay=true;if(reason=="spectating")GameManager.Client.IsSpectating=true;
                    if(reason=="selected")Last.Value.Interaction.selectedUnit=new();
                    if(reason=="waiting")GameManager.Client.IsWaitingForCommand=true;
                    if(reason=="processing")GameManager.Client.ActionManager.IsProcessing=true;
                    if(reason=="foreign-popup"){PopupManager.Current=new BasicPopup();PopupManager.PopupShowing=true;}
                    Click(second[0]);Check(GameManager.Client.Sent==0,"Choice dispatched after "+reason);
                }
            });
            Test("regroup-complete-intent-transfers-to-exact-appended-action",()=>{
                var (command,intent)=Command();var state=GameManager.GameState;var prefix=new CommandScaffoldAction();state.ActionStack.Add(prefix);
                BeginBoundary();
                Check(NativeExchangeCommands.BeforeCommand(command,state,out var scope),"Command rejected");var action=Scaffold(command);
                Check(NativeRegroupSession.PrepareCalls==1&&Native.Same(NativeRegroupSession.PreparedState,state)&&
                    NativeRegroupSession.BoundaryEvents.SequenceEqual(new[]{"prepare","ready","plan"}),"Command did not prepare exact supplied state before validation");
                NativeExchangeCommands.AfterCommand(command,state,scope);
                Check(NativeExchangeIntentCodec.Encode(NativeExchangeCommands.Get(action)!).SequenceEqual(NativeExchangeIntentCodec.Encode(intent)),"Canonical intent changed");
                Check(ReferenceEquals(state.ActionStack[0],prefix),"Prefix changed");
            });
            Test("regroup-invalid-native-scaffolds-rejected-with-exact-queue-rollback",()=>{
                foreach(string bad in new[]{"queue-replaced","extra","wrong-order","wrong-player","wrong-command-type","unpaid","wrong-source"})
                {
                    var (command,intent)=Command();var state=GameManager.GameState;var prefix=new CommandScaffoldAction();state.ActionStack.Add(prefix);var original=state.ActionStack;
                    Check(NativeExchangeCommands.BeforeCommand(command,state,out var scope),"Command rejected");var action=Scaffold(command);
                    if(bad=="queue-replaced"){state.ActionStack=new();foreach(var x in Native.Items(original))state.ActionStack.Add((ActionBase)x);}
                    if(bad=="extra")state.ActionStack.Add(new CommandScaffoldAction());
                    if(bad=="wrong-order"){var x=state.ActionStack[1];state.ActionStack[1]=state.ActionStack[2];state.ActionStack[2]=x;}
                    if(bad=="wrong-player")action.PlayerId=2;if(bad=="wrong-command-type")((EndCommandAction)state.ActionStack[1]).commandType=999;
                    if(bad=="unpaid")action.DeductCost=false;if(bad=="wrong-source")action.Coordinates=new(8,8);
                    Throws(()=>NativeExchangeCommands.AfterCommand(command,state,scope));Check(NativeExchangeCommands.Get(action) is null,"Invalid scaffold received intent");
                    Check(NativeRegroupSession.Faulted&&!NativeRegroupSession.Ready(state),"Queue binding error did not close the session: "+bad);
                    var error=new Exception("native failure");Check(ReferenceEquals(NativeExchangeCommands.CommandFinished(error,scope),error),"Original failure changed");
                    Check(ReferenceEquals(state.ActionStack,original)&&state.ActionStack.Count==1&&ReferenceEquals(state.ActionStack[0],prefix),"Exact queue rollback failed: "+bad);
                }
            });
            Test("regroup-prefix-tamper-does-not-claim-successful-rollback",()=>{
                var (command,intent)=Command();var state=GameManager.GameState;state.ActionStack.Add(new CommandScaffoldAction());
                NativeExchangeCommands.BeforeCommand(command,state,out var scope);Scaffold(command);state.ActionStack[0]=new CommandScaffoldAction();
                Throws(()=>NativeExchangeCommands.AfterCommand(command,state,scope));Throws(()=>NativeExchangeCommands.CommandFinished(new Exception(),scope));
            });
            Test("regroup-action-wrapper-retires-execution-scope-on-error",()=>{
                var (command,intent)=Command();var action=new BuildAction(command.PlayerId,command.Type,command.Coordinates,true);NativeExchangeCommands.Set(action,intent);
                NativeExchangeCommit.During=()=>{Check(NativeExchangeCommands.LiveExecution(GameManager.GameState),"Commit was outside execution scope");throw new Exception("Injected commit failure");};
                Throws(()=>NativeExchangeCommands.ExecuteAction(action,GameManager.GameState));Check(!NativeExchangeCommands.LiveExecution(GameManager.GameState),"Failed action retained execution authority");
                Check(ScopeClear(),"Failed action retained its hidden execution scope behind the fault barrier");
                Check(NativeRegroupSession.Faulted&&!NativeRegroupSession.Ready(GameManager.GameState),"Failed commit did not close the session");
                NativeExchangeCommit.During=null;
                BeginBoundary();
                Check(NativeExchangeCommands.ExecuteAction(new BuildAction(1,ImprovementData.Type.farm,new(),true),GameManager.GameState),"Ordinary native action was intercepted");
                Check(NativeExchangeCommands.BeforeCommand(new BuildCommand(1,ImprovementData.Type.farm,new()),GameManager.GameState,out var ordinaryScope)&&ordinaryScope is null,
                    "Ordinary native command was intercepted");
                Check(NativeRegroupSession.PrepareCalls==0,"Ordinary native execution prepared the private Regroup session");
                (command,intent)=Command();action=new BuildAction(command.PlayerId,command.Type,command.Coordinates,true);NativeExchangeCommands.Set(action,intent);
                BeginBoundary();
                Check(!NativeExchangeCommands.ExecuteAction(action,GameManager.GameState),"Custom action fell through to native construction");
                Check(NativeRegroupSession.PrepareCalls==1&&Native.Same(NativeRegroupSession.PreparedState,GameManager.GameState)&&ScopeClear()&&
                    NativeRegroupSession.BoundaryEvents.SequenceEqual(new[]{"prepare","ready","plan","commit"}),"Action prepare/validate/commit ordering or scope cleanup differs");
            });
            Test("regroup-invalid-command-execution-boundary-faults-before-scope",()=>{
                foreach(string failure in new[]{"stale","base","prepare"})
                {
                    var (command,intent)=Command();var state=GameManager.GameState;var stack=state.ActionStack;
                    BeginBoundary();
                    if(failure=="base")command.Coordinates=new(9,9);
                    else if(failure=="stale")NativeGameStateEnvelope.Revision++;
                    else NativeRegroupSession.PrepareFailure=new InvalidOperationException("Injected command preparation failure");
                    NativeExchangeCommands.CommandScope? scope=null;
                    if(failure!="stale")Throws(()=>NativeExchangeCommands.BeforeCommand(command,state,out scope));
                    else Check(!NativeExchangeCommands.BeforeCommand(command,state,out scope),"Stale command ran original");
                    Check(NativeRegroupSession.PrepareCalls==1&&NativeRegroupSession.BoundaryEvents[0]=="prepare","Command validated before preparation");
                    if(failure=="prepare")Check(NativeRegroupSession.BoundaryEvents.SequenceEqual(new[]{"prepare","fault"})&&
                        NativeRegroupSession.Faults.Single().Contains("Injected command preparation failure"),"Preparation failure reached validation or the wrong fault path");
                    Check(scope is null&&NativeRegroupSession.Faulted&&!NativeRegroupSession.Ready(state),"Invalid command boundary did not fault before scope: "+failure);
                    Check(ReferenceEquals(stack,state.ActionStack)&&stack.Count==0&&NativeExchangeCommit.Calls==0,"Rejected command changed queue or called commit");
                }
            });
            Test("regroup-invalid-action-execution-boundary-faults-before-scope",()=>{
                foreach(string failure in new[]{"stale","base","prepare"})
                {
                    var (command,intent)=Command();var state=GameManager.GameState;
                    var action=new BuildAction(command.PlayerId,command.Type,command.Coordinates,true);NativeExchangeCommands.Set(action,intent);
                    BeginBoundary();
                    if(failure=="base")action.Coordinates=new(9,9);
                    else if(failure=="stale")NativeGameStateEnvelope.Revision++;
                    else NativeRegroupSession.PrepareFailure=new InvalidOperationException("Injected action preparation failure");
                    if(failure!="stale")Throws(()=>NativeExchangeCommands.ExecuteAction(action,state));
                    else Check(!NativeExchangeCommands.ExecuteAction(action,state),"Stale action ran original");
                    Check(NativeRegroupSession.PrepareCalls==1&&NativeRegroupSession.BoundaryEvents[0]=="prepare","Action validated before preparation");
                    if(failure=="prepare")Check(NativeRegroupSession.BoundaryEvents.SequenceEqual(new[]{"prepare","fault"})&&
                        NativeRegroupSession.Faults.Single().Contains("Injected action preparation failure"),"Preparation failure reached validation or the wrong fault path");
                    Check(NativeRegroupSession.Faulted&&!NativeRegroupSession.Ready(state)&&!NativeExchangeCommands.LiveExecution(state)&&ScopeClear(),"Invalid action boundary did not fault and retire its scope: "+failure);
                    Check(state.ActionStack.Count==0&&NativeExchangeCommit.Calls==0,"Rejected action changed queue or called commit");
                }
            });
            Test("regroup-native-command-error-faults-before-queue-rollback",()=>{
                var (command,intent)=Command();var state=GameManager.GameState;var stack=state.ActionStack;
                NativeExchangeCommands.BeforeCommand(command,state,out var scope);Scaffold(command);
                var error=new Exception("Synthetic native original error");
                Check(ReferenceEquals(NativeExchangeCommands.CommandFinished(error,scope),error),"Native error replaced");
                Check(NativeRegroupSession.Faulted&&!NativeRegroupSession.Ready(state)&&ReferenceEquals(stack,state.ActionStack)&&stack.Count==0,"Native error did not fault and restore queue");
            });
            Test("regroup-reentrant-action-faults-without-retained-authority",()=>{
                var (command,intent)=Command();var state=GameManager.GameState;
                var action=new BuildAction(command.PlayerId,command.Type,command.Coordinates,true);NativeExchangeCommands.Set(action,intent);
                BeginBoundary();
                NativeExchangeCommit.During=()=>NativeExchangeCommands.ExecuteAction(action,state);
                Throws(()=>NativeExchangeCommands.ExecuteAction(action,state));
                Check(NativeRegroupSession.Faulted&&!NativeExchangeCommands.LiveExecution(state)&&ScopeClear()&&NativeExchangeCommit.Calls==1&&NativeRegroupSession.PrepareCalls==1,
                    "Reentry retained authority, repeated preparation, or recursively committed");
            });
            Test("disguise-button-research-gated-kind-cache-and-stable-order",()=>{
                var (interaction,unit)=Setup();var bar=interaction.Bar;
                Check(MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise) is null,"Disguise shown before planner research permission");
                NativeExchangePlanning.AllowDisguise=true;GameManager.LocalPlayer.Propaganda=true;GameManager.GameState.CurrentCommand++;
                MimicPresentationUi.BarReady(bar);
                var disguise=(UIRoundButton)MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise)!;
                var regroup=(UIRoundButton)MimicRegroupUi.OwnedButton(bar)!;
                Check(disguise is not null&&regroup is not null&&disguise!=regroup,"Two action kinds do not own separate buttons");
                var order=Native.Items(bar.buttons).ToArray();int inspect=Array.FindIndex(order,b=>Native.Same(b,MimicInspectionControls.OwnedButton(bar)));
                Check(inspect==0&&Array.FindIndex(order,b=>Native.Same(b,regroup))<Array.FindIndex(order,b=>Native.Same(b,disguise)),"Exchange buttons are not in stable action order");
                var circle=disguise.BG.sprite!;var plate=disguise.BG.transform.GetChild(0).gameObject.FindComponent<UnityEngine.UI.Image>()!;
                Check(plate.color==new UnityEngine.Color(1,1,1,1)&&plate.sprite==circle&&!plate.raycastTarget&&
                    ReferenceEquals(disguise.AssignedSprite,BranchArtAssets.DisguiseAction()),"Disguise lacks its flat black mask and owned white native-circle background");
                int calls=NativeExchangePlanning.Calls;
                for(int n=0;n<30;n++)MimicPresentationUi.BarReady(bar);
                Check(NativeExchangePlanning.Calls==calls&&Native.Same(disguise,MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise)),"Idle refresh recreated or replanned Disguise");
                Check(MimicPresentationUi.ToggleInspection(interaction,unit),"Could not toggle with both actions available");
                Check(Native.Same(disguise,MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise))&&Native.Same(regroup,MimicRegroupUi.OwnedButton(bar)),"Inspection replaced or relabeled action identities");
                NativeExchangePlanning.AllowDisguise=false;GameManager.LocalPlayer.Propaganda=false;GameManager.GameState.CurrentCommand++;MimicPresentationUi.BarReady(bar);
                Check(MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise) is null&&HasRegroup(bar)&&!plate&&circle,
                    "Research revocation removed Regroup, retained Disguise, or damaged shared art");
            });
            Test("disguise-research-refreshes-current-bar-without-turn-command-or-ledger-change",()=>{
                var (interaction,unit)=Setup();var bar=interaction.Bar;var state=GameManager.GameState;
                uint turn=state.CurrentTurn;ushort command=state.CurrentCommand;long revision=NativeGameStateEnvelope.Revision;
                Check(MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise) is null,"Disguise shown before research");
                int calls=NativeExchangePlanning.Calls;
                for(int n=0;n<10;n++)MimicPresentationUi.BarReady(bar);
                Check(NativeExchangePlanning.Calls==calls,"Unresearched idle bar repeatedly replanned");
                NativeExchangePlanning.AllowDisguise=true;GameManager.LocalPlayer.Propaganda=true;
                MimicPresentationUi.Tick();
                var disguise=MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise);
                Check(disguise is not null,"Existing selected Mimic did not gain Disguise on the first UI tick after research");
                Check(state.CurrentTurn==turn&&state.CurrentCommand==command&&NativeGameStateEnvelope.Revision==revision&&
                    !unit.UnitState.moved&&!unit.UnitState.attacked&&unit.UnitState.health==10&&NativeRegroupSession.PrepareCalls==0,
                    "Research refresh changed authoritative state or spent/refreshed a unit action");
                calls=NativeExchangePlanning.Calls;
                for(int n=0;n<30;n++)MimicPresentationUi.Tick();
                Check(NativeExchangePlanning.Calls==calls&&Native.Same(disguise,MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise)),
                    "Idle researched bar replanned or replaced its action control");
                GameManager.LocalPlayer.Propaganda=false;NativeExchangePlanning.AllowDisguise=false;MimicPresentationUi.Tick();
                Check(MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise) is null&&HasRegroup(bar),
                    "Same-command research loss left a stale Disguise control or removed Regroup");
            });
            Test("disguise-research-refresh-preserves-exhaustion-and-revokes-stale-targets",()=>{
                var (interaction,unit)=Setup();unit.UnitState.moved=true;unit.UnitState.attacked=true;
                NativeExchangePlanning.Available=false;MimicPresentationUi.BarReady(interaction.Bar);
                NativeExchangePlanning.AllowDisguise=true;GameManager.LocalPlayer.Propaganda=true;MimicPresentationUi.Tick();
                Check(MimicRegroupUi.OwnedButton(interaction.Bar,NativeExchangeKind.Disguise) is null&&unit.UnitState.moved&&unit.UnitState.attacked,
                    "Propaganda research refreshed an exhausted Mimic's action");
                First(kind:NativeExchangeKind.Disguise);var markers=TargetArt();var bar=Last!.Value.Interaction.Bar;
                ushort command=GameManager.GameState.CurrentCommand;long revision=NativeGameStateEnvelope.Revision;
                GameManager.LocalPlayer.Propaganda=false;NativeExchangePlanning.AllowDisguise=false;MimicRegroupUi.Tick();
                Check(!MimicRegroupUi.Choosing&&markers.All(m=>!m)&&GameManager.Client.Sent==0&&
                    GameManager.GameState.CurrentCommand==command&&NativeGameStateEnvelope.Revision==revision,
                    "Same-command research loss retained stale target selection");
            });
            Test("disguise-both-views-two-targets-cancel-and-kind-correct-dispatch",()=>{
                foreach(bool actual in new[]{false,true})foreach(bool swap in new[]{false,true})
                {
                    var first=First(actual,NativeExchangeKind.Disguise);var bar=Last!.Value.Interaction.Bar;
                    var cancel=(UIRoundButton)MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise)!;
                    var regroup=(UIRoundButton)MimicRegroupUi.OwnedButton(bar)!;
                    Check(cancel.Text=="Cancel"&&cancel.ButtonEnabled&&regroup.Text=="Regroup"&&!regroup.ButtonEnabled,"Wrong slot owns cancellation");
                    regroup.OnClicked!.Click();Check(MimicRegroupUi.Choosing&&cancel.Text=="Cancel"&&Targets().Length==1,"Other action stole the current target session");
                    MimicPresentationUi.BarReady(bar);Check(Native.Same(cancel,MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise)),"Refresh replaced Disguise Cancel slot");
                    var second=Second(first);var markers=TargetArt();Click(second[swap?1:0]);
                    var intent=NativeExchangeCommands.Get(GameManager.Client.SentCommand!)!;
                    Check(GameManager.Client.Sent==1&&intent.Kind==NativeExchangeKind.Disguise&&intent.Swap==swap&&markers.All(m=>!m),"Target choice dispatched wrong kind/placement or leaked targets");
                    Check(intent.SourceAppearanceHash.SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(NativeLedgerState.Appearance())),"Disguise copied recipient instead of initiating apparent form");
                    Check(!MimicRegroupUi.Choosing&&regroup.ButtonEnabled&&cancel.Text=="Disguise"&&NativeRegroupSession.PrepareCalls==0,"Confirmation retained UI authority or mutated session");
                }
                First(kind:NativeExchangeKind.Disguise);var old=TargetArt();Click(null);
                Check(!MimicRegroupUi.Choosing&&GameManager.Client.Sent==0&&old.All(m=>!m),"Disguise cancellation spent or retained an action");
            });
            Test("disguise-offer-uses-initiator-snapshot-without-recipient-capture",()=>{
                Setup(true);NativeLedgerState.FailCapture=true;NativeLedgerState.CaptureCalls=0;
                Check(NativeExchangeCommands.TryOffer(GameManager.GameState,3,4,NativeExchangeKind.Disguise,false,out var intent,out var reason),reason);
                Check(NativeLedgerState.CaptureCalls==0&&intent!.Kind==NativeExchangeKind.Disguise,"Disguise unnecessarily captured recipient appearance");
                Check(!NativeExchangeCommands.TryOffer(GameManager.GameState,3,4,false,out _,out _)&&NativeLedgerState.CaptureCalls==1,"Regroup wrapper no longer copies its target");
                Check(!NativeExchangeCommands.TryOffer(GameManager.GameState,3,4,(NativeExchangeKind)255,false,out _,out _),"Unknown UI action kind was accepted");
            });
            Test("disguise-target-context-and-control-reassignment-rejects-stale-click",()=>{
                foreach(string change in new[]{"callback","bar","command","revision","partner","hidden"})
                {
                    var first=First(kind:NativeExchangeKind.Disguise);var art=TargetArt();var bar=Last!.Value.Interaction.Bar;
                    if(change=="callback")((UIRoundButton)MimicRegroupUi.OwnedButton(bar,NativeExchangeKind.Disguise)!).OnClicked=(UIButtonBase.ButtonAction)Native.ButtonCallback(()=>{});
                    if(change=="bar")bar.unit=new Unit();
                    if(change=="command")GameManager.GameState.CurrentCommand++;
                    if(change=="revision")NativeGameStateEnvelope.Revision++;
                    if(change=="partner")GameManager.GameState.Units[4].attacked=true;
                    if(change=="hidden")first[0].IsHidden=true;
                    Click(first[0]);
                    Check(!MimicRegroupUi.Choosing&&GameManager.Client.Sent==0&&art.All(m=>!m),"Stale Disguise click accepted: "+change);
                }
                First(kind:NativeExchangeKind.Disguise);var selectedBar=Last!.Value.Interaction.Bar;
                var saved=((UIRoundButton)MimicRegroupUi.OwnedButton(selectedBar,NativeExchangeKind.Disguise)!).OnClicked!;
                MimicRegroupUi.NativeClear(selectedBar);saved.Click();
                Check(!MimicRegroupUi.Choosing&&GameManager.Client.Sent==0&&CachedBars==0,"Native clear retained Disguise callback or cache");
            });
            Test("disguise-complete-command-retains-native-scaffold-and-owner-boundary",()=>{
                Setup(true);Check(NativeExchangeCommands.TryOffer(GameManager.GameState,3,4,NativeExchangeKind.Disguise,true,out var intent,out var reason),reason);
                var command=(BuildCommand)NativeExchangeCommands.Create(intent!);var state=GameManager.GameState;
                BeginBoundary();Check(NativeExchangeCommands.BeforeCommand(command,state,out var scope)&&scope is not null,"Disguise command denied after prepared session");
                var action=Scaffold(command);NativeExchangeCommands.AfterCommand(command,state,scope);
                Check(NativeExchangeCommands.Get(action)!.Kind==NativeExchangeKind.Disguise,"Native scaffold lost Disguise intent");
                NativeExchangeCommands.ExecuteAction(action,state);
                Check(NativeExchangeCommit.Calls==1&&ScopeClear()&&!NativeRegroupSession.Faulted,"Disguise did not use one guarded native transaction");
            });
            File.WriteAllText(Environment.GetEnvironmentVariable("DECEPTION_REGROUP_FIXTURE_OUTPUT")!,JsonSerializer.Serialize(new{total=Results.Count,passed=Results.Count-Failed,failed=Failed,tests=Results},new JsonSerializerOptions{WriteIndented=true}));
            return Failed==0?0:1;
        }
    }
}
