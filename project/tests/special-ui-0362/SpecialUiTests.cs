using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using UnityEngine;
using UnitType=Polytopia.Data.UnitData.Type;
namespace Deception.Tests
{
 public static class SpecialUiTests
 {
  static int Cases,Assertions;static void Check(bool ok,string why){Assertions++;if(!ok)throw new Exception(why);}
  static void Invoke(string method)=>typeof(UiAdapterTests).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null);
  static (Unit Unit,ClientInteraction Interaction,Tile Target) Setup(bool fogger=false)
  {
   NativeSpecialActionUi.Clear();Invoke("Reset");NativeRegroupSession.Enabled=true;NativeGameStateEnvelope.Revision=0;NativeGameStateEnvelope.FocusUnits=Array.Empty<LedgerUnit>();NativeGameStateEnvelope.Epoch=new("00112233-4455-6677-8899-aabbccddeeff");NativeSpyNetwork.Queries=NativeBlackout.Queries=NativeSubvert.Queries=NativeFeignSupport.Queries=0;
   NativeBlackout.Allowed=true;NativeFeignSupport.Available.Clear();NativeSubvert.Targets=new(){4};NativeExchangeCommands.ValidResult=true;NativeExchangeCommands.Last=null;NativeExchangeCommands.Forgotten=0;
   GameManager.LocalPlayer.Espionage=true;MapRenderer.Current=new();PolytopiaInput.CurrentTouches=new();PolytopiaInput.CurrentTouches.Add(new(){phase=TouchPhase.Ended});MimicRegroupUi.Choosing=false;
   var source=new Unit{UnitState=new(){id=3,owner=1,type=fogger?UnitType.dcp_motherfogger:UnitType.dcp_spy,UnitData=new(fogger?UnitType.dcp_motherfogger:UnitType.dcp_spy),health=50,coordinates=new(){X=3,Y=2}}};
   var target=new Unit{UnitState=new(){id=4,owner=2,type=UnitType.warrior,UnitData=new(UnitType.warrior),health=100,coordinates=new(){X=4,Y=2}}};
   foreach(var u in new[]{source,target})
   {
    GameManager.GameState.Units[u.UnitState.id]=u.UnitState;MapRenderer.Current.Register(u);
    var d=new TileData{coordinates=u.UnitState.coordinates,unit=u.UnitState};GameManager.GameState.Map.Add(d);
    var t=new Tile{Coordinates=d.coordinates,Data=d,Unit=u};t.overlayRenderer.transform.localPosition=new(1,2,3);MapRenderer.Current.Tiles[(d.coordinates.X,d.coordinates.Y)]=t;
   }
   var empty=new TileData{coordinates=new(){X=5,Y=2},Explored=false};GameManager.GameState.Map.Add(empty);MapRenderer.Current.Tiles[(5,2)]=new(){Data=empty,Coordinates=empty.coordinates,IsHidden=true};
   NativeSpyNetwork.Tiles=GameManager.GameState.Map.Tiles.Values.Cast<object>().ToArray();
   var c=new ClientInteraction();c.SelectUnit(source);NativeSpecialActionUi.Selected(c,source);
   return(source,c,MapRenderer.Current.Tiles[(4,2)]);
  }
  static UIRoundButton Button(ClientInteraction c,string text)=>Native.Items(c.Bar.buttons).Cast<UIRoundButton>().Single(b=>b.text==text);
  static void Click(ClientInteraction c,string text)=>Button(c,text).OnClicked!.Click();
  static void Case(string name,Action body){Cases++;try{body();}catch(Exception e){throw new Exception(name,e);}finally{NativeSpecialActionUi.Clear();NativeRegroupSession.Enabled=false;}}
  static void StartMove((Unit Unit,ClientInteraction Interaction,Tile Target) a)
  {
   var dest=new TileData{coordinates=new(3,3)};GameManager.GameState.Map.Add(dest);
   MapRenderer.Current!.Tiles[(3,3)]=new(){Data=dest,Coordinates=dest.coordinates};
   GameManager.Client.ActionManager.IsProcessing=true;
   NativeSpecialActionUi.SelectionClearing(a.Interaction);
   a.Interaction.selectedUnit=null;a.Interaction.Bar.unit=null;
  }
  static void Arrive((Unit Unit,ClientInteraction Interaction,Tile Target) a)
  {
   var old=MapRenderer.Current!.Tiles[(3,2)];old.Data!.unit=null;old.Unit=null;
   a.Unit.UnitState.coordinates=new(3,3);a.Unit.UnitState.moved=true;
   var dest=MapRenderer.Current.Tiles[(3,3)];dest.Data!.unit=a.Unit.UnitState;dest.Unit=a.Unit;
   GameManager.GameState.CurrentCommand++;NativeGameStateEnvelope.Revision++;
   GameManager.Client.ActionManager.IsProcessing=false;
  }
  public static int Run(){try{
   Case("stable owned slots and bounded idle plans",()=>{var a=Setup();Check(NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Count()==1,"Spy offers only Subvert");int network=NativeSpyNetwork.Queries,support=NativeFeignSupport.Queries,subvert=NativeSubvert.Queries;
    for(int i=0;i<20;i++)NativeSpecialActionUi.Sync(a.Interaction.Bar,a.Unit);Check(NativeSpyNetwork.Queries==network&&NativeFeignSupport.Queries==support&&NativeSubvert.Queries==subvert&&NativeBlackout.Queries==0,"idle never replans automatic targets");
    var first=NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).ToArray();Click(a.Interaction,"Subvert");Check(NativeSpecialActionUi.Choosing&&!NativeSpecialActionUi.GuardCommands(),"targets reserve ordinary command input");
    Check(NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Zip(first,ReferenceEquals).All(x=>x),"cancel caption retains slots");Click(a.Interaction,"Cancel");Check(!NativeSpecialActionUi.Choosing&&GameManager.Client.Sent==0,"cancel has no command");});
   Case("Subvert fresh offer dispatched once",()=>{var a=Setup();Click(a.Interaction,"Subvert");Check(!NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==0,"opening release cannot commit");NativeSpecialActionUi.Tick();
    Check(!NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==1&&NativeExchangeCommands.Last!.Kind==NativeExchangeKind.Subvert,"second deliberate tap sends current offer");
    Check(NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==1,"repeat release is ordinary input");});
   Case("acquired Spy needs no Espionage research",()=>{var a=Setup();GameManager.LocalPlayer.Espionage=false;NativeSpecialActionUi.Sync(a.Interaction.Bar,a.Unit);Click(a.Interaction,"Subvert");NativeSpecialActionUi.Tick();NativeSpecialActionUi.Released(a.Interaction,a.Target,0);Check(GameManager.Client.Sent==1,"native genuine acquired Spy retains specialist action");});
   Case("Blackout unseen empty tiles do not consult targets",()=>{var a=Setup(true);Click(a.Interaction,"Blackout");Check(NativeSpecialActionUi.Choosing&&NativeBlackout.Queries==0,"showing geographical targets never probes opponent cells");NativeSpecialActionUi.Tick();
    var target=MapRenderer.Current!.Tiles[(5,2)];NativeSpecialActionUi.Released(a.Interaction,target,0);Check(GameManager.Client.Sent==1&&NativeBlackout.Queries==1,"one final offer for unobserved empty target");});
   foreach(var kind in new[]{NativeExchangeKind.FeignHeal,NativeExchangeKind.FeignBoost,NativeExchangeKind.FeignSwarm})Case(kind+" immediate guarded action",()=>{var a=Setup();NativeFeignSupport.Available.Add(kind);NativeGameStateEnvelope.Revision++;NativeSpecialActionUi.Sync(a.Interaction.Bar,a.Unit);
    string label=kind.ToString().Replace("Feign","Feign ");Click(a.Interaction,label);Check(GameManager.Client.Sent==1&&NativeExchangeCommands.Last!.Kind==kind&&!NativeSpecialActionUi.Choosing,"support sends only offered whole-area intent");});
   foreach(string change in new[]{"owner","turn","command","revision","selection","state","client","hidden","replay","spectator","busy","attacked","pooled tile","target hidden","client-state","not-ready"})Case("target revoked "+change,()=>{var a=Setup();Click(a.Interaction,"Subvert");NativeSpecialActionUi.Tick();
    switch(change){case"client-state":GameManager.Client.GameState=new();break;case"not-ready":GameManager.Client.IsReady=false;break;case"owner":a.Unit.UnitState.owner=2;break;case"turn":GameManager.GameState.CurrentTurn++;break;case"command":GameManager.GameState.CurrentCommand++;break;case"revision":NativeGameStateEnvelope.Revision++;break;case"selection":a.Interaction.selectedUnit=null;break;case"state":GameManager.GameState=new();break;case"client":GameManager.Client=new();break;case"hidden":a.Unit.Hidden=true;break;case"replay":GameManager.Client.IsReplay=true;break;case"spectator":GameManager.Client.IsSpectating=true;break;case"busy":GameManager.Client.ActionManager.IsProcessing=true;break;case"attacked":a.Unit.UnitState.attacked=true;break;case"research":GameManager.LocalPlayer.Espionage=false;break;case"pooled tile":a.Target.Data=new();break;case"target hidden":a.Target.Unit!.Hidden=true;break;}
    NativeSpecialActionUi.Released(a.Interaction,a.Target,0);Check(GameManager.Client.Sent==0&&!NativeSpecialActionUi.Choosing,"revoked action cannot send");});
   foreach(string input in new[]{"UI","drag","camera","outside","missing touch","extra touch","negative index","canceled","invalid tap","UI hit"})Case("input guard "+input,()=>{var a=Setup();Click(a.Interaction,"Subvert");NativeSpecialActionUi.Tick();int index=0;
    switch(input){case"UI":a.Interaction.wasDownOverUI=true;break;case"drag":CameraController.IsDragging=true;break;case"camera":CameraController.IsBlocked=true;break;case"outside":PolytopiaInput.OutsideWindow=true;break;case"missing touch":PolytopiaInput.CurrentTouches.Clear();break;case"extra touch":PolytopiaInput.CurrentTouches.Add(new());break;case"negative index":index=-1;break;case"canceled":PolytopiaInput.CurrentTouches[0].phase=TouchPhase.Canceled;break;case"invalid tap":a.Interaction.ValidTap=false;break;case"UI hit":PolytopiaInput.UIHitTest=_=>true;break;}
    NativeSpecialActionUi.Released(a.Interaction,a.Target,index);Check(GameManager.Client.Sent==0,"unsafe tap never dispatches");});
   Case("authoritative refusal and invalid command",()=>{var a=Setup(true);Click(a.Interaction,"Blackout");NativeSpecialActionUi.Tick();NativeBlackout.Allowed=false;NativeSpecialActionUi.Released(a.Interaction,a.Target,0);Check(GameManager.Client.Sent==0&&!NativeSpecialActionUi.Choosing,"fresh rejection prevents send");
    a=Setup();NativeBlackout.Allowed=true;Click(a.Interaction,"Subvert");NativeSpecialActionUi.Tick();NativeExchangeCommands.ValidResult=false;NativeSpecialActionUi.Released(a.Interaction,a.Target,0);Check(GameManager.Client.Sent==0&&NativeExchangeCommands.Forgotten==1,"invalid built command forgotten");});
   Case("pool callback and mutual exclusion",()=>{var a=Setup();var button=Button(a.Interaction,"Subvert");var callback=button.OnClicked;NativeSpecialActionUi.NativeClear(a.Interaction.Bar);callback!.Click();Check(!NativeSpecialActionUi.Choosing&&GameManager.Client.Sent==0,"retired callback has no authority");
    MimicRegroupUi.Choosing=true;NativeSpecialActionUi.Sync(a.Interaction.Bar,a.Unit);Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(),"other target mode excludes controls");});
   Case("automatic Subvert targets preserve native movement",()=>{var a=Setup();Check(!NativeSpecialActionUi.Choosing&&NativeSpecialActionUi.GuardCommands()&&NativeSpecialActionUi.BeforeOverlays(a.Unit),"Automatic targets must not block movement or native overlays");NativeSpecialActionUi.Tick();Check(NativeSpecialActionUi.Released(a.Interaction,MapRenderer.Current!.Tiles[(5,2)],0)&&GameManager.Client.Sent==0,"Non-target tap keeps ordinary movement meaning");Check(!NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==1,"Automatic target sends a fresh Subvert without pressing action button");});
   Case("automatic targets refresh after move and spend",()=>{var a=Setup();a.Unit.UnitState.moved=true;GameManager.GameState.CurrentCommand++;NativeSpecialActionUi.Tick();NativeSpecialActionUi.Tick();Check(NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==0&&!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(),"Moved Spy must lose Subvert and Blackout immediately");a.Unit.UnitState.attacked=true;GameManager.GameState.CurrentCommand++;NativeSpecialActionUi.Tick();Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(),"Spent action removes all specialist targets and actions");});
   Case("removed Feign Recover has no action slot even with stale provider",()=>{var a=Setup();NativeFeignSupport.Available.Add(NativeExchangeKind.FeignRecover);NativeGameStateEnvelope.Revision++;NativeSpecialActionUi.Sync(a.Interaction.Bar,a.Unit);Check(!Native.Items(a.Interaction.Bar.buttons).Cast<UIRoundButton>().Any(b=>b.text=="Feign Recover")&&!NativeSpecialActionUi.Kinds.Contains(NativeExchangeKind.FeignRecover),"Removed support kind retained UI action");});
   Case("native map selection refresh preserves spent Spy focus without actions",()=>{var a=Setup();GameManager.Client.ActionManager.IsProcessing=true;NativeSpecialActionUi.Clear();a.Unit.UnitState.moved=true;GameManager.GameState.CurrentCommand++;a.Interaction.selectedUnit=a.Unit;a.Interaction.Bar.unit=a.Unit;NativeSpecialActionUi.Refocused(a.Interaction,true);Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(),"Movement processing admitted action input");GameManager.Client.ActionManager.IsProcessing=false;NativeSpecialActionUi.Tick();NativeSpecialActionUi.Tick();Check(ReferenceEquals(a.Interaction.selectedUnit,a.Unit)&&NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==0&&!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(),"Selection must persist while moved Spy actions stay unavailable");});
   foreach(string reason in new[]{"skipped-native","cleared-native","bar-mismatch","spent","foreign","hidden"})Case("native focus refresh guard "+reason,()=>{var a=Setup();NativeSpecialActionUi.Clear();a.Unit.UnitState.moved=true;switch(reason){case"cleared-native":a.Interaction.selectedUnit=null;break;case"bar-mismatch":a.Interaction.Bar.unit=new();break;case"spent":a.Unit.UnitState.attacked=true;break;case"foreign":a.Unit.UnitState.owner=2;break;case"hidden":a.Unit.Hidden=true;break;}NativeSpecialActionUi.Refocused(a.Interaction,reason!="skipped-native");NativeSpecialActionUi.Tick();NativeSpecialActionUi.Tick();Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any()&&NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==0,"Invalid native focus manufactured a specialist action");});
   Case("native bar rebuild restarts automatic targets within the same completed command",()=>{
    var a=Setup();GameManager.GameState.CurrentCommand++;NativeSpecialActionUi.Tick();
    NativeSpecialActionUi.NativeClear(a.Interaction.Bar);a.Interaction.Bar.buttons.Clear();NativeSpecialActionUi.Tick();NativeSpecialActionUi.Tick();
    Check(!NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==1,"Retired automatic scan token suppressed same-command native bar rebuild");
   });
   Case("completed move survives temporary native deselection and delayed bar rebind",()=>{
    var a=Setup();GameManager.Client.ActionManager.IsProcessing=true;NativeSpecialActionUi.SelectionClearing();a.Interaction.selectedUnit=null;NativeSpecialActionUi.Tick();
    var replacement=new Unit{UnitState=a.Unit.UnitState};replacement.UnitState.moved=true;replacement.UnitState.coordinates=new(3,3);GameManager.GameState.CurrentCommand++;MapRenderer.Current!.Register(replacement);
    var oldTile=MapRenderer.Current.Tiles[(3,2)];oldTile.Data!.unit=null;oldTile.Unit=null;
    var destination=new TileData{coordinates=replacement.UnitState.coordinates,unit=replacement.UnitState};GameManager.GameState.Map.Add(destination);
    MapRenderer.Current.Tiles[(3,3)]=new(){Coordinates=destination.coordinates,Data=destination,Unit=replacement};
    NativeSpyNetwork.Tiles=GameManager.GameState.Map.Tiles.Values.Cast<object>().ToArray();
    a.Interaction.selectedUnit=replacement;NativeSpecialActionUi.Refocused(a.Interaction,true);NativeSpecialActionUi.SelectionClearing();
    a.Interaction.Bar.unit=replacement;NativeSpecialActionUi.Tick();Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(),"In-flight move exposed specialist input");
    GameManager.Client.ActionManager.IsProcessing=false;NativeSpecialActionUi.Tick();NativeSpecialActionUi.Tick();
    Check(ReferenceEquals(a.Interaction.selectedUnit,replacement)&&NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==0&&!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(),"Rebound moved Spy must remain selected without action controls");
   });
   foreach(string reason in new[]{"deselected","waiting","client","map","session-clear","spent","foreign","hidden"})Case("deferred native focus guard "+reason,()=>{
    var a=Setup();NativeSpecialActionUi.SelectionClearing();a.Unit.UnitState.moved=true;
    switch(reason){case"deselected":a.Interaction.selectedUnit=null;break;case"waiting":GameManager.Client.IsWaitingForCommand=true;break;case"client":GameManager.Client=new();break;case"map":MapRenderer.Current=new();break;case"session-clear":NativeSpecialActionUi.Clear();break;case"spent":a.Unit.UnitState.attacked=true;break;case"foreign":a.Unit.UnitState.owner=2;break;case"hidden":a.Unit.Hidden=true;break;}
    NativeSpecialActionUi.Tick();NativeSpecialActionUi.Tick();Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any()&&NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==0,"Deferred observer manufactured action authority");
   });
   Case("Spymaid retains automatic specialist targets",()=>{var a=Setup();a.Unit.UnitState.type=UnitType.dcp_spymaid;NativeGameStateEnvelope.Revision++;NativeSpecialActionUi.Refocused(a.Interaction,true);NativeSpecialActionUi.Tick();NativeSpecialActionUi.Tick();Check(!NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==1,"Aquarion genuine Spy omitted from native focus refresh");});
   Case("Fogger requires explicit Blackout target mode",()=>{var a=Setup(true);NativeSpecialActionUi.Tick();NativeSpecialActionUi.Released(a.Interaction,MapRenderer.Current!.Tiles[(5,2)],0);Check(NativeBlackout.Queries==0&&GameManager.Client.Sent==0,"Automatic mode never offers Blackout on blank cells");Click(a.Interaction,"Blackout");NativeSpecialActionUi.Tick();NativeSpecialActionUi.Released(a.Interaction,MapRenderer.Current.Tiles[(5,2)],0);Check(GameManager.Client.Sent==1&&NativeExchangeCommands.Last!.Kind==NativeExchangeKind.Blackout,"Explicit Blackout replaces passive targets");});
   Case("automatic targets obey stale authoritative target offer",()=>{var a=Setup();NativeSpecialActionUi.Tick();NativeSubvert.Targets.Clear();NativeSpecialActionUi.Released(a.Interaction,a.Target,0);Check(GameManager.Client.Sent==0,"Stale passive target does not authorize Subvert");});
   foreach(string guard in new[]{"UI","drag","invalid tap","no touch"})Case("automatic ordinary release "+guard,()=>{var a=Setup();NativeSpecialActionUi.Tick();switch(guard){case"UI":a.Interaction.wasDownOverUI=true;break;case"drag":CameraController.IsDragging=true;break;case"invalid tap":a.Interaction.ValidTap=false;break;case"no touch":PolytopiaInput.CurrentTouches.Clear();break;}Check(NativeSpecialActionUi.Released(a.Interaction,MapRenderer.Current!.Tiles[(5,2)],0)&&GameManager.Client.Sent==0,"Passive mode consumed native release cleanup");});
   Case("same-revision snapshot replacement refreshes targets",()=>{var a=Setup();NativeSubvert.Targets.Clear();NativeGameStateEnvelope.Invalidate();NativeSpecialActionUi.Tick();int empty=NativeSubvert.Queries;NativeSubvert.Targets.Add(4);NativeGameStateEnvelope.Invalidate();NativeSpecialActionUi.Tick();NativeSpecialActionUi.Tick();Check(NativeSubvert.Queries>empty&&!NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==1,"Same revision ledger replacement retained stale automatic candidates");});
   Case("dead Spy retires automatic targets",()=>{var a=Setup();a.Unit.UnitState.health=0;NativeSpecialActionUi.Tick();Check(NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(),"Dead source retained targeting");});
   Case("empty automatic candidate scan stays bounded",()=>{var a=Setup();NativeSubvert.Targets.Clear();GameManager.GameState.CurrentCommand++;NativeSpecialActionUi.Tick();int count=NativeSubvert.Queries;for(int i=0;i<30;i++)NativeSpecialActionUi.Tick();Check(count==NativeSubvert.Queries,"No-target Spy rescanned network every frame");});
   Case("Subvert uses map bolt art and Blackout smoke art",()=>{var a=Setup();Check(ReferenceEquals(Button(a.Interaction,"Subvert").AssignedSprite,MimicMapInspection.SubvertIcon()),"Subvert action lost shared red lightning");});
   foreach(string missing in new[]{"no eligible enemy","no body","outside range","invisible","unexplored","hidden tile","stale wrapper"})Case("Subvert button excludes "+missing,()=>{var a=Setup();
    switch(missing){case"no eligible enemy":NativeSubvert.Targets.Clear();break;case"no body":a.Target.Data.unit=null;break;case"outside range":NativeSpyNetwork.Tiles=NativeSpyNetwork.Tiles.Where(t=>!ReferenceEquals(t,a.Target.Data)).ToArray();break;case"invisible":a.Target.Unit!.Hidden=true;break;case"unexplored":a.Target.Data.Explored=false;break;case"hidden tile":a.Target.IsHidden=true;break;case"stale wrapper":a.Target.Unit!.UnitState=new();break;}
    GameManager.GameState.CurrentCommand++;NativeSpecialActionUi.Tick();Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(b=>((UIRoundButton)b).text=="Subvert"),"Subvert offered without a visible eligible target");
    Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(),"Spy retained removed Blackout after losing Subvert target");});
   Case("newly reachable enemy requires next unmoved allowance",()=>{var a=Setup();NativeSubvert.Targets.Clear();GameManager.GameState.CurrentCommand++;NativeSpecialActionUi.Tick();Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(b=>((UIRoundButton)b).text=="Subvert"),"empty slot retained");
    a.Unit.UnitState.moved=true;NativeSubvert.Targets.Add(4);GameManager.GameState.CurrentCommand++;NativeSpecialActionUi.Tick();NativeSpecialActionUi.Tick();Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any()&&NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==0,"Moving into range restored a spent Spy action");a.Unit.UnitState.moved=false;GameManager.GameState.CurrentTurn++;NativeSpecialActionUi.Tick();NativeSpecialActionUi.Tick();Check(!NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==1,"Next unmoved allowance did not restore eligible targets");});
   foreach(string kind in new[]{"Spy","Spymaid","Warrior","Dash","Mimic"})foreach(bool spent in new[]{false,true})
   Case("native movement leaves cleared selection alone "+kind+" "+spent,()=>{
    var a=Setup();a.Unit.UnitState.type=kind switch{"Spymaid"=>UnitType.dcp_spymaid,"Mimic"=>UnitType.dcp_mimic_form_warrior,"Warrior" or "Dash"=>UnitType.warrior,_=>UnitType.dcp_spy};
    a.Unit.UnitState.Dash=kind=="Dash";int selections=a.Interaction.Selections;StartMove(a);
    NativeSpecialActionUi.Tick();Check(a.Interaction.selectedUnit is null&&a.Interaction.Selections==selections,"In-flight native deselection was overridden");
    Arrive(a);a.Unit.UnitState.attacked=spent;NativeSpecialActionUi.Refocused(a.Interaction,true);
    for(int i=0;i<8;i++)NativeSpecialActionUi.Tick();
    Check(a.Interaction.selectedUnit is null&&a.Interaction.selectedTile is null&&a.Interaction.Selections==selections,"Completed move secretly selected a unit");
    Check(a.Unit.UnitState.moved&&a.Unit.UnitState.attacked==spent&&!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any()&&GameManager.Client.Sent==0,"Native movement altered action budget or synthesized controls");
   });
   foreach(string kind in new[]{"Spy","Spymaid","Warrior","Dash","Mimic"})foreach(string focus in new[]{"retained","rebound","other unit","other tile"})
   Case("native focus choice remains authoritative "+kind+" "+focus,()=>{
    var a=Setup();a.Unit.UnitState.type=kind switch{"Spymaid"=>UnitType.dcp_spymaid,"Mimic"=>UnitType.dcp_mimic_form_warrior,"Warrior" or "Dash"=>UnitType.warrior,_=>UnitType.dcp_spy};
    a.Unit.UnitState.Dash=kind=="Dash";StartMove(a);Arrive(a);
    Unit? nativeUnit=a.Unit;Tile? nativeTile=null;
    switch(focus){
     case"rebound":nativeUnit=new Unit{UnitState=a.Unit.UnitState};MapRenderer.Current!.Register(nativeUnit);MapRenderer.Current.Tiles[(3,3)].Unit=nativeUnit;break;
     case"other unit":nativeUnit=a.Target.Unit;break;
     case"other tile":nativeUnit=null;nativeTile=a.Target;break;
    }
    // Native code/user input, not the adapter, supplies the completed selection.
    a.Interaction.selectedUnit=nativeUnit;a.Interaction.selectedTile=nativeTile;a.Interaction.Bar.unit=nativeUnit;
    int selections=a.Interaction.Selections;NativeSpecialActionUi.Refocused(a.Interaction,true);
    for(int i=0;i<8;i++)NativeSpecialActionUi.Tick();
    Check(ReferenceEquals(a.Interaction.selectedUnit,nativeUnit)&&ReferenceEquals(a.Interaction.selectedTile,nativeTile)&&a.Interaction.Selections==selections,"Adapter reselected or displaced native focus");
    Check(a.Unit.UnitState.moved&&!a.Unit.UnitState.attacked&&GameManager.Client.Sent==0,"Focus observation changed action state or sent a command");
    Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(),"Focus observation manufactured moved-Spy or foreign-unit specialist actions");
   });
   Case("deliberate native selection still enables ready Spy targeting",()=>{
    var a=Setup();StartMove(a);Arrive(a);a.Unit.UnitState.moved=false;GameManager.GameState.CurrentTurn++;
    a.Interaction.SelectUnit(a.Unit);int selections=a.Interaction.Selections;NativeSpecialActionUi.Selected(a.Interaction,a.Unit);
    NativeSpecialActionUi.Tick();Check(a.Interaction.Selections==selections&&!NativeSpecialActionUi.Released(a.Interaction,a.Target,0)&&GameManager.Client.Sent==1,"Manual ready selection failed to expose eligible Subvert target");
   });
   Case("post-move synthetic selection lifecycle is removed",()=>{
    var type=typeof(NativeSpecialActionUi);var methods=type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
    Check(!methods.Any(m=>new[]{"BeforeNativeMove","AfterNativeMove","NativeMoveFinished","RestoreMoveFocus","ManualFocusInput","OtherCommand"}.Contains(m.Name))&&
     type.GetNestedType("MoveFocus",BindingFlags.Public|BindingFlags.NonPublic) is null,"Native movement still has a custom capture/reselection lifecycle");
   });
   Case("Fogger retains one stable Blackout slot through move and first shot",()=>{
    var a=Setup(true);Check(NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Count()==1&&Button(a.Interaction,"Blackout") is not null,"Fogger Blackout slot missing");
    Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(b=>((UIRoundButton)b).text=="Subvert"),"Fogger gained Spy Subvert");
    foreach(string stage in new[]{"moved","first-shot"}){a.Unit.UnitState.moved=true;GameManager.GameState.CurrentCommand++;NativeGameStateEnvelope.Revision++;NativeSpecialActionUi.Tick();NativeSpecialActionUi.Tick();Check(NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Count()==1&&Button(a.Interaction,"Blackout") is not null,"Native remaining Fogger allowance lost action button "+stage);}
    a.Unit.UnitState.attacked=true;GameManager.GameState.CurrentCommand++;NativeSpecialActionUi.Tick();Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(),"Spent second shot retained Blackout");
   });
   Case("Fogger Blackout targeting revalidates remaining action",()=>{var a=Setup(true);Click(a.Interaction,"Blackout");NativeSpecialActionUi.Tick();a.Unit.UnitState.attacked=true;NativeSpecialActionUi.Released(a.Interaction,a.Target,0);Check(GameManager.Client.Sent==0&&!NativeSpecialActionUi.Choosing,"Exhausted Fogger sent stale target");});
   Case("Spy and Spymaid expose no Blackout button",()=>{foreach(var type in new[]{UnitType.dcp_spy,UnitType.dcp_spymaid}){var a=Setup();a.Unit.UnitState.type=type;GameManager.GameState.CurrentCommand++;NativeSpecialActionUi.Tick();Check(!NativeSpecialActionUi.OwnedButtons(a.Interaction.Bar).Any(b=>((UIRoundButton)b).text=="Blackout"),"Removed Spy Blackout remained in menu");}});
   Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,nativeGame="NOT RUN"}));return 0;
  }catch(Exception e){Console.WriteLine(e);Console.WriteLine(string.Join("\n",Entry.Notes));return 1;}}
 }
}
