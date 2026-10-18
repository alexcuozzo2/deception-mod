using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using U=Polytopia.Data.UnitData.Type;
using A=Polytopia.Data.UnitAbility.Type;
namespace Deception.Tests {
 public static partial class CloakReadinessTests {
 static int Assertions;static readonly List<string> Cases=new();
 static void Check(bool yes,string why){Assertions++;if(!yes)throw new Exception(why);}static void Test(string n,Action body){try{body();Cases.Add(n);}catch(Exception e){throw new Exception(n,e);}}
 static LedgerAppearance Mask(string form="warrior")=>new(form,new("imperius","default"),new("imperius","default"),new("imperius","default"),7,100,100,false,ApparentConcealment.Visible,-1,new[]{"Poisoned"});
 static (GameState,UnitState) Make(U actual=U.cloak,string form="warrior",bool earned=false){
  NativeDisguiseConcealment.Clear();NativeDisguiseConcealmentVisual.Clear();NativeRegroupSession.Enabled=true;NativeLedgerState.Fail=false;NativeRegroupSession.Faults=0;
  var s=new GameState();s.Players[1]=new(){Id=1};s.Players[2]=new(){Id=2};var u=new UnitState{id=1,type=actual};u.effects.Add(UnitEffect.Frozen);if(earned)u.effects.Add(UnitEffect.Invisible);s.Map.Tiles[(0,0)]=new(){unit=u};
  Put(s,new(1,1,true,actual==U.dcp_mimic_cloak?LedgerActualKind.Mimic:LedgerActualKind.Genuine,true,Mask(form),PaidInvestmentKind.Known,8));GameManager.GameState=s;GameManager.Client=new(){Player=s.Players[2]};return(s,u);
 }
 static void Put(GameState s,LedgerUnit row){var l=new NativeIdentityLedger(Guid.Parse("a9c5de32-889f-43b6-84bb-7cd209265e37"),2,1,new[]{row},Array.Empty<LedgerReceipt>());NativeGameStateEnvelope.Set(s,0,NativeIdentityLedgerCodec.Encode(l,0));}
 static LedgerUnit Row(GameState s)=>NativeIdentityLedgerCodec.Decode(NativeGameStateEnvelope.Get(s)!.Bytes(),0).Units.Single();
 static bool StateHidden(GameState s,UnitState u,byte owner=2){bool b=u.IsInvisible(s,owner);NativeDisguiseConcealment.AfterInvisiblePlayer(u,s.Players[owner],ref b);NativeDisguiseConcealment.AfterInvisibleState(u,s,owner,ref b);return b;}
 static bool PlayerHidden(UnitState u,PlayerState p){bool b=u.IsInvisible(p);NativeDisguiseConcealment.AfterInvisiblePlayer(u,p,ref b);return b;}
 static bool HideAbility(UnitState u){bool result=UnitDataExtensions.HasAbility(u,A.Hide);NativeDisguiseConcealment.AfterAbility(u,A.Hide,ref result);return result;}
 static Unit Visual(GameState s,UnitState u){var v=new Unit{UnitState=u};MapRenderer.Current=new();MapRenderer.Current.Units.Add(u.id,v);return v;}
 static MapRenderContext Context(GameState s)=>new(){mapData=s.Map,gld=s.GameLogicData,unitRegistry=MapRenderer.Current!,localPlayerId=2};
 static bool Effect(UnitState u,UnitEffect effect)=>u.HasEffect(effect);
 public static int Run(){try{
 foreach(U actual in new[]{U.cloak,U.mermaidcloak,U.cloak_boat})Test("native-hide-readiness-hidden-mask-"+actual,()=>{
  var(s,u)=Make(actual);NativeDisguiseConcealment.BeforeMove(new MoveCommand{UnitId=1},s,out var movement);Check(HideAbility(u),"real hide ability retained during native move");NativeDisguiseConcealment.MoveFinished(null,movement);
  bool valid=true;string why="";NativeDisguiseConcealment.AfterHideValid(new HideCommand(),s,ref valid,ref why);Check(valid,"native valid Hide stays valid");
  var action=new HideAction();var old=u.effects;bool run=NativeDisguiseConcealment.BeforeHide(action,s,out var hide);Check(run&&hide!=null,"native Hide executes");action.Execute(s);NativeDisguiseConcealment.AfterEffect(hide,true);
  Check(u.effects.Contains(UnitEffect.Invisible)&&u.attacked,"native readiness and cost preserved");Check(!old.Contains(UnitEffect.Invisible),"old effect list alias untouched");
  Check(Row(s).Appearance!.Concealment==ApparentConcealment.Hidden&&Row(s).Appearance!.Effects.SequenceEqual(new[]{"Invisible","Poisoned"}),"native Hide makes every public mask hidden");
  Check(StateHidden(s,u)&&PlayerHidden(u,s.Players[2])&&!StateHidden(s,u,1),"opponent cannot see the hidden mask; owner retains native access");Check(u.type==actual&&u.health==70,"native identity and health unchanged");
 });
 foreach(U actual in new[]{U.cloak,U.mermaidcloak,U.cloak_boat})foreach(string transition in new[]{"mask","shed"})Test("earned-readiness-survives-"+actual+"-"+transition,()=>{
  var(s,u)=Make(actual,earned:true);var old=u.effects;var applied=(Il2CppSystem.Collections.Generic.List<UnitEffect>)NativeDisguiseConcealment.WithConcealment(old,transition=="mask"?Mask():null,u);
  Check(applied.Contains(UnitEffect.Invisible)&&applied.Contains(UnitEffect.Frozen),"earned native effect preserved through public change");Check(!ReferenceEquals(old,applied)&&old.Count==2,"separate owned list");
 });
 Test("visible-mask-does-not-invent-unearned-readiness",()=>{var(s,u)=Make();var copy=(Il2CppSystem.Collections.Generic.List<UnitEffect>)NativeDisguiseConcealment.WithConcealment(u.effects,Mask(),u);Check(!copy.Contains(UnitEffect.Invisible)&&copy.Contains(UnitEffect.Frozen),"no new actual Invisible without native Hide");});
 Test("other-real-unit-reveal-clears-borrowed-invisibility",()=>{var(s,u)=Make(U.warrior,"cloak",true);var copy=(Il2CppSystem.Collections.Generic.List<UnitEffect>)NativeDisguiseConcealment.WithConcealment(u.effects,null,u);Check(!copy.Contains(UnitEffect.Invisible)&&copy.Contains(UnitEffect.Frozen),"warrior does not retain borrowed concealment");});
 Test("fake-cloak-never-retains-real-cloak-readiness",()=>{var(s,u)=Make(U.dcp_mimic_cloak,"cloak",true);var copy=(Il2CppSystem.Collections.Generic.List<UnitEffect>)NativeDisguiseConcealment.WithConcealment(u.effects,Mask(),u);Check(!copy.Contains(UnitEffect.Invisible)&&!UnitDataExtensions.HasAbility(u,A.Infiltrate),"Mimic gets no real Infiltrate");});
 Test("native-reveal-consumes-readiness-without-changing-mask",()=>{var(s,u)=Make(earned:true);var reveal=new RevealAction();NativeDisguiseConcealment.BeforeReveal(reveal,s,out var e);reveal.Execute(s);NativeDisguiseConcealment.AfterEffect(e,true);Check(!u.effects.Contains(UnitEffect.Invisible)&&Row(s).Independent&&Row(s).Appearance!.Form=="warrior","normal detection consumes earned hide without identity reveal");});
 Test("active-command-reveal-preserves-verified-infiltration-readiness",()=>{var(s,u)=ReadyInfiltrator();var command=new AttackCommand();Check(command.IsValid(s,out _),"native Infiltrate is initially ready");NativeDisguiseLifecycle.BeforeActive(command,s,out bool scope);Check(scope&&!Row(s).Independent&&Row(s).Appearance==null,"real active action sheds disguise");Check(u.effects.Contains(UnitEffect.Invisible)&&command.IsValid(s,out _),"native command still sees earned readiness after reveal");Check(u.effects.Contains(UnitEffect.Poisoned)&&!u.attacked&&!u.moved,"pre-hook does not spend native budget or clear status");});
 Test("invalid-native-attack-does-not-reveal-or-mint-readiness",()=>{var(s,u)=Make();var command=new AttackCommand();NativeDisguiseLifecycle.BeforeActive(command,s,out _);Check(Row(s).Independent&&!u.effects.Contains(UnitEffect.Invisible),"unready invalid attack leaves state intact");});
 foreach(U actual in new[]{U.cloak,U.mermaidcloak,U.cloak_boat,U.warrior})Test("positive-damage-detects-revealed-real-body-"+actual,()=>{
  var(s,u)=Make(actual,earned:true);u.moved=true;u.attacked=true;var before=u.effects;NativeDisguiseLifecycle.BeforeDamage(s,u.coordinates,out var damage);u.health--;
  NativeDisguiseLifecycle.AfterDamage(s,damage);Check(!Row(s).Independent&&Row(s).Appearance==null,"positive damage removes genuine disguise");
  Check(!u.effects.Contains(UnitEffect.Invisible)&&!StateHidden(s,u),"actual Cloak and borrowed cloak concealment are detected after damage");
  Check(u.effects.Contains(UnitEffect.Frozen)&&u.moved&&u.attacked&&u.health==69&&u.type==actual,"damage reveal preserves other true status/allowance/body");
  Check(before.Contains(UnitEffect.Invisible)&&!ReferenceEquals(before,u.effects),"old effects remain untouched for transactional rollback");
 });
 foreach(string eventKind in new[]{"zero","healing","status"})Test("non-positive-damage-keeps-mask-and-readiness-"+eventKind,()=>{
  var(s,u)=Make(earned:true);var before=u.effects;NativeDisguiseLifecycle.BeforeDamage(s,u.coordinates,out var damage);if(eventKind=="healing")u.health++;if(eventKind=="status")u.attacked=true;
  NativeDisguiseLifecycle.AfterDamage(s,damage);Check(Row(s).Independent&&Row(s).Appearance?.Form=="warrior","no HP loss does not reveal identity");
  Check(ReferenceEquals(before,u.effects)&&u.effects.Contains(UnitEffect.Invisible)&&StateHidden(s,u),"every mask retains genuine native concealment");
 });
 Test("actual-hidden-effect-still-produces-hostile-warning",()=>{var(s,u)=Make(earned:true);Check(StateHidden(s,u),"hidden before warning");var detector=new UnitState{id=2,owner=2,coordinates=new(1,0)};s.Map.Tiles[(1,0)]=new(){unit=detector};NativeDisguiseConcealment.BeforeDetecting(detector,s,out var warning);Check(StateHidden(s,u)&&!StateHidden(s,u,1),"warning only for hostile viewer");NativeDisguiseConcealment.WarningFinished(null,warning);Check(StateHidden(s,u),"warning does not expose normal rendering");NativeDisguiseConcealment.BeforeWarningUi(out warning);Check(PlayerHidden(u,s.Players[2]),"native UI eye retained");NativeDisguiseConcealment.WarningFinished(null,warning);});
 Test("explicit-backend-state-never-borrows-displayed-mask",()=>{var(s,u)=Make(earned:true);var(other,v)=Make(earned:true);other.Map.Tiles[(0,0)]!.unit=u;other.Players[2]=s.Players[2];Put(other,Row(other) with{Appearance=Mask("cloak") with{Concealment=ApparentConcealment.Hidden,Effects=new[]{"Invisible"}}});GameManager.GameState=other;Check(StateHidden(s,u),"actual native hidden effect overrides old visible mask metadata");Check(StateHidden(other,u),"explicit hidden ledger remains hidden");Check(!ReferenceEquals(u,v),"fixture shares exact unit only intentionally");});
 Test("wrong-viewer-identity-retains-native-result",()=>{var(s,u)=Make(earned:true);Check(PlayerHidden(u,new PlayerState{Id=2}),"unbound viewer cannot borrow own state ledger");});
 Test("removed-native-root-retains-native-result",()=>{var(s,u)=Make(earned:true);s.Map.Tiles[(0,0)]!.unit=null;Check(StateHidden(s,u)&&PlayerHidden(u,s.Players[2]),"stale unit does not project ledger");});
 Test("ordinary-cloak-and-cloak-mask-stay-native-hidden",()=>{var(s,u)=Make(earned:true);Put(s,Row(s) with{Independent=false,Appearance=null});Check(StateHidden(s,u),"ordinary genuine cloak untouched");Put(s,Row(s) with{Independent=true,Appearance=Mask("cloak") with{Concealment=ApparentConcealment.Hidden,Effects=new[]{"Invisible"}}});Check(StateHidden(s,u),"hidden cloak disguise untouched");});
 A40Cases();
 SpyCases();
 Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases.Count,assertions=Assertions,tests=Cases,nativeGame="NOT RUN"}));return 0;
 }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
