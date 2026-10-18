using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using Polytopia.Data;
using PolytopiaBackendBase.Common;
namespace Deception.Tests
{
 public static class IntelTests
 {
  static int Checks;static readonly List<string> Cases=new();
  static void Check(bool pass,string why){Checks++;if(!pass)throw new Exception(why);}
  static void Reject(Action call,string why){try{call();}catch{Checks++;return;}throw new Exception("Accepted "+why);}
  static void Test(string name,Action body){body();Cases.Add(name);}
  static (GameState State,PlayerState Viewer,PlayerState Enemy) State(bool income=true)
  {
   NativeTradeSecrets.Clear();NativeTechnologyIntelligence.Clear();Entry.Errors.Clear();NativeRegroupSession.Enabled=true;RecruitmentHooks.Registered=true;HotSeatOverlay.IsActive=false;GameManager.Viewing=true;
   var g=new GameLogicData();foreach(var t in Enum.GetValues<TechData.Type>())g.Tech[t]=new(){type=t,cost=t==TechData.Type.basic?0:1};
   void Children(TechData.Type parent,params TechData.Type[] ids){g.Tech[parent].techUnlocks=new(ids.Select(id=>g.Tech[id]));}
   Children(TechData.Type.basic,TechData.Type.fishing,TechData.Type.dcp_deception);
   Children(TechData.Type.fishing,TechData.Type.sailing,TechData.Type.ramming);Children(TechData.Type.sailing,TechData.Type.navigation);Children(TechData.Type.ramming,TechData.Type.aquatism);
   Children(TechData.Type.aquafishing,TechData.Type.sailing,TechData.Type.ramming);Children(TechData.Type.aquaculture,TechData.Type.aquatism);Children(TechData.Type.marinelife,TechData.Type.navigation);
   Children(TechData.Type.frostwork,TechData.Type.sailing,TechData.Type.aquaculture);Children(TechData.Type.icefishing,TechData.Type.aquatism);Children(TechData.Type.sledding,TechData.Type.navigation);
   Children(TechData.Type.dcp_deception,TechData.Type.dcp_intelligence,TechData.Type.dcp_propaganda);Children(TechData.Type.dcp_intelligence,TechData.Type.dcp_camouflage);Children(TechData.Type.dcp_propaganda,TechData.Type.dcp_espionage);
   foreach(var t in new[]{TechData.Type.ramming,TechData.Type.sailing,TechData.Type.aquaculture,TechData.Type.marinelife,TechData.Type.sledding,TechData.Type.icefishing,TechData.Type.dcp_intelligence,TechData.Type.dcp_propaganda})g.Tech[t].cost=2;
   foreach(var t in new[]{TechData.Type.navigation,TechData.Type.aquatism,TechData.Type.polarism,TechData.Type.polarwarfare,TechData.Type.dcp_camouflage,TechData.Type.dcp_espionage})g.Tech[t].cost=3;
   foreach(var t in Enum.GetValues<TribeType>())g.Tribes.Add(new(){type=t});
   var aqua=g.Tribes[1];aqua.Overrides[TechData.Type.fishing]=g.Tech[TechData.Type.aquafishing];aqua.Overrides[TechData.Type.ramming]=g.Tech[TechData.Type.aquaculture];aqua.Overrides[TechData.Type.sailing]=g.Tech[TechData.Type.marinelife];
   var polar=g.Tribes[2];polar.Overrides[TechData.Type.fishing]=g.Tech[TechData.Type.frostwork];polar.Overrides[TechData.Type.sailing]=g.Tech[TechData.Type.icefishing];polar.Overrides[TechData.Type.aquaculture]=g.Tech[TechData.Type.sledding];polar.Overrides[TechData.Type.aquatism]=g.Tech[TechData.Type.polarism];polar.Overrides[TechData.Type.navigation]=g.Tech[TechData.Type.polarwarfare];
   var p=new PlayerState(){Id=1,tribe=TribeType.imperius};p.Research.UnionWith(new[]{TechData.Type.basic,TechData.Type.dcp_deception,TechData.Type.dcp_intelligence});if(income)p.Research.Add(TechData.Type.dcp_camouflage);p.Known.Add(2);
   var e=new PlayerState(){Id=2,tribe=TribeType.aquarion};e.Research.UnionWith(new[]{TechData.Type.basic,TechData.Type.aquafishing,TechData.Type.aquaculture,TechData.Type.aquatism});
   var s=new GameState(){GameLogicData=g};s.PlayerStates.Add(p);s.PlayerStates.Add(e);NativeRegroupSession.States.Add(s);
   GameManager.GameState=s;GameManager.Client=new(){GameState=s};GameManager.LocalPlayer=p;MapRenderer.Current=new();return(s,p,e);
  }
  static void Start(GameState state,byte actor=1,bool run=true,Action? after=null)
  {
   var action=new StartTurnAction(){PlayerId=actor};NativeTradeSecrets.BeforeTurn(action,state,out var scope);Exception? failure=null;
   try{if(run)action.Execute(state);after?.Invoke();NativeTradeSecrets.AfterTurn(action,state,run,scope);}catch(Exception e){failure=e;throw;}finally{NativeTradeSecrets.FinishedTurn(failure,scope);}
  }
  static TechView Tree(GameState state,params TechData.Type[] tech){var tree=new TechView();foreach(var id in tech)tree.items.Add(new(){TechData=state.GameLogicData.Tech[id]});return tree;}
  public static int Run()
  {try{
   Test("once_per_met_living_opponent_technology_and_native_currency",()=>
   {var(s,p,e)=State();var extra=new PlayerState(){Id=3,tribe=TribeType.aquarion};extra.Research.UnionWith(e.Research);p.Known.Add(3);s.PlayerStates.Add(extra);Check(NativeTradeSecrets.Total(s,1)==12,"each met opponent must pay 1+2+3 for its three shared technologies");int forecast=5;NativeTradeSecrets.Income(s,1,true,ref forecast);Check(forecast==17,"tier-weighted stacked forecast");Start(s);Check(s.ActionStack.Count==2,"one extra native currency action");Check(p.Currency==10,"no immediate payment");for(int i=0;i<s.ActionStack.Count;i++)((IncreaseCurrencyAction)s.ActionStack[i]).Execute(s);Check(p.Currency==27,"base income plus twelve tier-weighted stacked stars credited once");Check(Entry.Errors.Count==0,"no native errors");});
   Test("research_and_lifecycle_recalculate",()=>
   {var(s,p,e)=State();p.Research.Add(TechData.Type.fishing);Check(NativeTradeSecrets.Total(s,1)==5,"viewer research removes tier1 and retains tier2+3");e.Alive=false;Check(NativeTradeSecrets.Total(s,1)==0,"dead enemy");e.Alive=true;p.Known.Clear();Check(NativeTradeSecrets.Total(s,1)==0,"unmet enemy");p.Known.Add(2);p.Alive=false;Check(NativeTradeSecrets.Total(s,1)==0,"dead viewer");p.Alive=true;p.Research.Remove(TechData.Type.dcp_camouflage);Check(NativeTradeSecrets.Total(s,1)==0,"requires tier3");Check(NativeTradeSecrets.Capture(s,1)!.Projection.Cells.Any(c=>c.Researchers.Length>0),"tier2 retains reconnaissance");});
   Test("polaris_changed_tree_normalizes_override_chains",()=>
   {var(s,p,e)=State();e.tribe=TribeType.polaris;e.Research.Clear();e.Research.UnionWith(new[]{TechData.Type.frostwork,TechData.Type.sledding,TechData.Type.polarwarfare});var r=NativeTradeSecrets.Capture(s,1)!;Check(r.Projection.Income==6,"polar tier total 1+2+3");Check(r.Projection.Cells.Single(c=>c.TechId=="ramming").Researchers.SequenceEqual(new byte[]{2}),"sledding maps across Aquarion intermediary");Check(r.Projection.Cells.Single(c=>c.TechId=="navigation").Income==3,"polar warfare maps tier3 navigation");});
   Test("counterintelligence_from_special_viewer",()=>
   {var(s,p,e)=State();p.tribe=TribeType.polaris;e.tribe=TribeType.imperius;e.Research.Clear();e.Research.UnionWith(new[]{TechData.Type.fishing,TechData.Type.ramming,TechData.Type.navigation});var r=NativeTradeSecrets.Capture(s,1)!;Check(r.Projection.Income==6,"special viewer total 1+2+3");Check(r.Projection.Cells.Single(c=>c.TechId=="sledding").Income==2,"special viewer tier2 cell");p.Research.Add(TechData.Type.sledding);Check(NativeTradeSecrets.Total(s,1)==4,"special research cancels equivalent tier2 contribution");});
   Test("foreign_and_suppressed_turns_do_not_credit",()=>
   {var(s,p,e)=State();Start(s,2);Check(s.ActionStack.Count==1,"not current actor");s.ActionStack.Clear();Start(s,1,false);Check(s.ActionStack.Count==0,"suppressed original");s.CurrentState=GameState.State.Ended;Start(s);Check(s.ActionStack.Count==1,"ended game no extra");});
   Test("detached_state_does_not_read_global_game",()=>
   {var(s,p,e)=State();var(global,gp,ge)=State(false);Check(NativeTradeSecrets.Total(s,1)==6,"detached state tier-weighted research");Start(s);Check(s.ActionStack.Count==2&&global.ActionStack.Count==0,"detached queue ownership");Check(gp.Currency==10,"global no side effects");});
   Test("queue_and_context_failure_are_bounded",()=>
   {var(s,p,e)=State();Reject(()=>Start(s,1,true,()=>s.Map=new()),"map replacement");Check(s.ActionStack.Count==1,"no extra on context change");(s,p,e)=State();Reject(()=>Start(s,1,true,()=>s.ActionStack=new()),"queue replacement");Check(s.ActionStack.Count==0,"no replacement queue mutation");});
   Test("finalizer_removes_only_own_child",()=>
   {var(s,p,e)=State();var a=new StartTurnAction(){PlayerId=1};NativeTradeSecrets.BeforeTurn(a,s,out var scope);a.Execute(s);NativeTradeSecrets.AfterTurn(a,s,true,scope);var foreign=new IncreaseCurrencyAction(2,new(),99);s.ActionStack.Add(foreign);var ex=new Exception("later postfix");Check(ReferenceEquals(NativeTradeSecrets.FinishedTurn(ex,scope),ex),"original exception");Check(s.ActionStack.Count==2&&ReferenceEquals(s.ActionStack[1],foreign),"own child removal only");});
   Test("preview_resource_normalization_preserves_income",()=>
   {var(s,p,e)=State();var create=new ActionBase(){Kind=ActionType.CreateResource};var tail=new ActionBase();s.ActionStack.Add(create);s.ActionStack.Add(tail);Start(s,1,true,()=>s.ActionStack.Remove(create));Check(s.ActionStack.Count==3&&ReferenceEquals(s.ActionStack[0],tail),"documented preview removal accepted");var extra=(IncreaseCurrencyAction)s.ActionStack[2];Check(extra.Amount==6&&extra.Delay==10,"four argument amount/delay constructor");});
   Test("unexpected_original_action_removal_fails_without_payment",()=>
   {var(s,p,e)=State();var marker=new ActionBase();s.ActionStack.Add(marker);Reject(()=>Start(s,1,true,()=>s.ActionStack.Remove(marker)),"foreign action deletion");Check(s.ActionStack.Count==1,"only native action remains");});
   Test("duplicate_postfix_and_suppressed_forecast_are_inert",()=>
   {var(s,p,e)=State();var a=new StartTurnAction(){PlayerId=1};NativeTradeSecrets.BeforeTurn(a,s,out var scope);a.Execute(s);NativeTradeSecrets.AfterTurn(a,s,true,scope);NativeTradeSecrets.AfterTurn(a,s,true,scope);Check(s.ActionStack.Count==2,"same invocation cannot add twice");int total=50;NativeTradeSecrets.Income(s,1,false,ref total);Check(total==50,"suppressed native forecast untouched");Check(new IncreaseCurrencyAction(1,new(),99).Amount==1,"native three argument overload means delay");});
   Test("registry_cached_and_rebuilt_for_new_logic",()=>
   {var(s,p,e)=State();NativeTradeSecrets.Total(s,1);NativeTradeSecrets.Total(s,1);Check(s.GameLogicData.Scans==1,"no repeated registry scan");NativeTradeSecrets.Clear();NativeTradeSecrets.Total(s,1);Check(s.GameLogicData.Scans==2,"clear rebuild");});
   Test("native_hooks_use_exact_managed_signatures",()=>
   {var h=new HarmonyLib.Harmony();NativeTradeSecrets.Install(h);NativeTechnologyIntelligence.Install(h);Check(h.Hooks.Count==7,"all hook signatures");});
   Test("tech_ui_marks_badges_and_income_without_native_mutation",()=>
   {var(s,p,e)=State();var tree=Tree(s,TechData.Type.fishing,TechData.Type.ramming,TechData.Type.aquatism);var old=tree.items[0].bg.sprite;NativeTechnologyIntelligence.TreeChanged(tree,true);Check(Entry.Errors.Count==0,string.Join(";",Entry.Errors));Check(UILibrary.Roots.Count(r=>!r.Dead)==3,"three decorated cells");Check(UILibrary.Resources.Where(r=>!r.Dead).Select(r=>r.Amount).OrderBy(x=>x).SequenceEqual(new float[]{1,2,3}),"native numeric stars distinguish technology tiers");Check(ReferenceEquals(tree.items[0].bg.sprite,old),"native icon untouched");Check(p.Currency==10,"UI no payment");Check(UIUtils.Images.Where(i=>!i.Dead).All(i=>!i.raycastTarget),"ornaments ignore raycasts");int roots=UILibrary.Roots.Count,scans=s.GameLogicData.Scans;NativeTechnologyIntelligence.Tick();Check(UILibrary.Roots.Count==roots&&s.GameLogicData.Scans==scans,"stable UI no rebuild");});
   Test("async_head_restores_alpha_and_pooled_cleanup",()=>
   {var(s,p,e)=State();SpriteHandle.Delay=true;var tree=Tree(s,TechData.Type.fishing);NativeTechnologyIntelligence.TreeChanged(tree,true);var pending=UIUtils.Images.Last();Check(pending.color.a==0,"pending head not white square");SpriteHandle.All.Last().Complete();NativeTechnologyIntelligence.Tick();Check(pending.color.a==1&&pending.sprite is not null,"ready head visible");NativeTechnologyIntelligence.ItemChanging(tree.items[0]);Check(pending.Dead,"pooled cell destroys own ornaments");SpriteHandle.Delay=false;});
   Test("hotseat_replay_hidden_tree_and_unmet_are_private",()=>
   {var(s,p,e)=State();var tree=Tree(s,TechData.Type.fishing);NativeTechnologyIntelligence.TreeChanged(tree,true);HotSeatOverlay.IsActive=true;NativeTechnologyIntelligence.Tick();Check(UILibrary.Roots.All(r=>r.Dead),"handoff hides prior player");HotSeatOverlay.IsActive=false;GameManager.Client!.IsReplay=true;NativeTechnologyIntelligence.TreeChanged(tree,true);Check(UILibrary.Roots.All(r=>r.Dead),"replay no intel");GameManager.Client.IsReplay=false;tree.lastPlayer=2;NativeTechnologyIntelligence.TreeChanged(tree,true);Check(UILibrary.Roots.All(r=>r.Dead),"foreign tree no intel");tree.lastPlayer=1;p.Known.Clear();s.CurrentCommand++;NativeTechnologyIntelligence.TreeChanged(tree,true);Check(UILibrary.Roots.All(r=>r.Dead),"unmet no markers");});
   Test("research_command_refreshes_income_and_clears_cell_payment",()=>
   {var(s,p,e)=State();var tree=Tree(s,TechData.Type.fishing);NativeTechnologyIntelligence.TreeChanged(tree,true);int notices=ResourceEvents.Notices.Count;p.Research.Add(TechData.Type.fishing);s.CurrentCommand++;NativeTechnologyIntelligence.Tick();Check(ResourceEvents.Notices.Count==notices+1,"income refresh event");Check(!UILibrary.Resources.Any(r=>!r.Dead),"owned technology loses star projection");Check(UILibrary.Roots.Any(r=>!r.Dead),"enemy badge remains");NativeTechnologyIntelligence.TreeDisabled(tree);Check(UILibrary.Roots.All(r=>r.Dead),"disable cleanup");});
   Test("stacking_ignores_unmet_dead_and_self_research",()=>
   {
    var(s,p,e)=State();var met=new PlayerState(){Id=3,tribe=TribeType.aquarion};var unmet=new PlayerState(){Id=4,tribe=TribeType.aquarion};var dead=new PlayerState(){Id=5,tribe=TribeType.aquarion,Alive=false};
    foreach(var other in new[]{met,unmet,dead}){other.Research.UnionWith(e.Research);s.PlayerStates.Add(other);}p.Known.UnionWith(new byte[]{1,3,5});
    var projection=NativeTradeSecrets.Capture(s,1)!.Projection;
    Check(projection.Income==12&&projection.Cells.Where(c=>c.Income>0).Select(c=>c.Income).OrderBy(x=>x).SequenceEqual(new[]{2,4,6}),"unmet/dead/self records inflated two valid tier-weighted opponents");
    Check(projection.Cells.Single(c=>c.TechId=="fishing").Researchers.SequenceEqual(new byte[]{2,3}),"private or dead opponent leaked into markers");
    met.Alive=false;Check(NativeTradeSecrets.Total(s,1)==6,"one dead contributor must remove only its 1+2+3 shares");
    p.Known.Remove(2);Check(NativeTradeSecrets.Total(s,1)==0,"no met living contributor means no income");
   });
   Test("stacked_markers_forecast_and_research_removal_agree",()=>
   {
    var(s,p,e)=State();var other=new PlayerState(){Id=3,tribe=TribeType.aquarion};other.Research.UnionWith(e.Research);s.PlayerStates.Add(other);p.Known.Add(3);
    var tree=Tree(s,TechData.Type.fishing,TechData.Type.ramming,TechData.Type.aquatism);NativeTechnologyIntelligence.TreeChanged(tree,true);
    Check(UILibrary.Resources.Where(r=>!r.Dead).Select(r=>r.Amount).OrderBy(x=>x).SequenceEqual(new float[]{2,4,6}),"one displayed tier-weighted star count must include both researchers");
    int forecast=5;NativeTradeSecrets.Income(s,1,true,ref forecast);Check(forecast==17&&UILibrary.Resources.Where(r=>!r.Dead).Sum(r=>r.Amount)==forecast-5,"UI labels and forecast disagree");
    p.Research.Add(TechData.Type.fishing);s.CurrentCommand++;NativeTechnologyIntelligence.Tick();
    Check(NativeTradeSecrets.Total(s,1)==10&&UILibrary.Resources.Where(r=>!r.Dead).Select(r=>r.Amount).SequenceEqual(new float[]{4,6}),"own research must remove both shares for the researched slot");
    Check(NativeTradeSecrets.Capture(s,1)!.Projection.Cells.Single(c=>c.TechId=="fishing").Researchers.SequenceEqual(new byte[]{2,3}),"researching must preserve informative opponent badges");
    p.Research.UnionWith(new[]{TechData.Type.ramming,TechData.Type.aquatism});s.CurrentCommand++;NativeTechnologyIntelligence.Tick();
    Check(NativeTradeSecrets.Total(s,1)==0&&!UILibrary.Resources.Any(r=>!r.Dead),"researching every shared slot must remove all stacked income");
    Check(UILibrary.Roots.Count(r=>!r.Dead)==3,"opponents' research badges should remain after own research");
    forecast=5;NativeTradeSecrets.Income(s,1,true,ref forecast);Start(s);Check(forecast==5&&s.ActionStack.Count==1,"stale stacked income survived in forecast or native turn payment");
    Check(Entry.Errors.Count==0,string.Join(";",Entry.Errors));
   });
   Test("native_research_aliases_and_duplicate_rows_pay_once_per_opponent",()=>
   {
    var(s,p,e)=State();e.Research.Add(TechData.Type.fishing);e.Research.Add(TechData.Type.ramming);
    s.GameLogicData.Tech[TechData.Type.aquafishing].techUnlocks.Add(new(){type=TechData.Type.aquafishing,cost=1});
    var polar=new PlayerState(){Id=3,tribe=TribeType.polaris};polar.Research.UnionWith(new[]{TechData.Type.frostwork,TechData.Type.fishing,TechData.Type.sledding,TechData.Type.ramming,TechData.Type.polarism,TechData.Type.aquatism});s.PlayerStates.Add(polar);p.Known.Add(3);
    var result=NativeTradeSecrets.Capture(s,1)!.Projection;
    Check(result.Income==12,"aliases or repeated effective-tree records multiplied a single opponent's tier-weighted share");
    Check(result.Cells.Single(c=>c.TechId=="fishing").Income==2&&result.Cells.Single(c=>c.TechId=="ramming").Income==4&&result.Cells.Single(c=>c.TechId=="aquatism").Income==6,"tribe replacements did not map into tier1/2/3 two-opponent slots");
    Check(result.Cells.Where(c=>c.Income>0).All(c=>c.Researchers.SequenceEqual(new byte[]{2,3})),"a contributor appears twice inside its normalized cell");
   });
   Test("pure_duplicate_slot_and_opponent_records_never_multiply_shares",()=>
   {
    var slots=TechnologyIntelligence.Slots(new[]{"fishing","aquafishing","frostwork"},new[]{("fishing","aquafishing"),("aquafishing","frostwork")},new[]{"fishing"});
    var tech=new[]{new ResearchTechnology("fishing","fishing",1)};var known=new[]{"fishing","aquafishing","frostwork","aquafishing"}.Select(id=>slots[id]).ToArray();
    var opponents=new[]{new ResearchOpponent(2,true,true,known),new ResearchOpponent(3,true,true,known)};
    var result=TechnologyIntelligence.Project(1,tech,Array.Empty<string>(),opponents,true);
    Check(result.Income==2&&result.Cells[0].Researchers.SequenceEqual(new byte[]{2,3}),"duplicate known aliases must count once inside each opponent");
    Check(TechnologyIntelligence.Project(1,tech,new[]{"fishing","fishing"},opponents,true).Income==0,"duplicate viewer research must still stop every share");
    Reject(()=>TechnologyIntelligence.Project(1,tech,Array.Empty<string>(),new[]{opponents[0],opponents[0]},true),"duplicate player identity counted as another opponent");
   });
   Test("mixed_tiers_and_researchers_weight_each_missing_slot",()=>
   {
    var tech=new[]{new ResearchTechnology("a","a",1),new ResearchTechnology("b","b",2),new ResearchTechnology("c","c",3)};
    var opponents=new[]{new ResearchOpponent(2,true,true,new[]{"a","b","c"}),new ResearchOpponent(3,true,true,new[]{"a","c"})};
    var result=TechnologyIntelligence.Project(1,tech,Array.Empty<string>(),opponents,true);
    Check(result.Income==10&&result.Cells.Select(c=>c.Income).SequenceEqual(new[]{2,2,6}),"each missing slot must combine its own tier and actual contributor count");
    Check(result.Cells.Single(c=>c.TechId=="b").Researchers.SequenceEqual(new byte[]{2}),"tier2 must not inherit another slot's researchers");
    var researched=TechnologyIntelligence.Project(1,tech,new[]{"c"},opponents,true);
    Check(researched.Income==4&&researched.Cells.Select(c=>c.Income).SequenceEqual(new[]{2,2,0}),"tier3 research must remove both three-star shares");
    Check(researched.Cells.Single(c=>c.TechId=="c").Researchers.SequenceEqual(new byte[]{2,3}),"researching retains both opponent markers");
    var disabled=TechnologyIntelligence.Project(1,tech,Array.Empty<string>(),opponents,false);
    Check(disabled.Income==0&&disabled.Cells.All(c=>c.Income==0)&&disabled.Cells.Select(c=>c.Researchers.Length).SequenceEqual(new[]{2,1,2}),"income unlock must gate every tier without hiding research markers");
   });
   Test("pure_validation_and_equivalence",()=>
   {Reject(()=>TechnologyIntelligence.Slots(new[]{"a","b"},new[]{("a","b")},new[]{"a","b"}),"ambiguous merged base slots");Reject(()=>TechnologyIntelligence.Project(1,new[]{new ResearchTechnology("a","a",4)},Array.Empty<string>(),Array.Empty<ResearchOpponent>(),true),"invalid tier");var p=TechnologyIntelligence.Project(1,new[]{new ResearchTechnology("a","a",2)},Array.Empty<string>(),new[]{new ResearchOpponent(2,false,true,new[]{"a"}),new ResearchOpponent(3,true,false,new[]{"a"})},true);Check(p.Income==0&&p.Cells[0].Researchers.Length==0,"pure privacy guard");});
   Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases.Count,assertions=Checks,tests=Cases,scope="Real research/economy/UI adapters with synthetic native objects; no native game execution"}));return 0;
  }catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases.Count,assertions=Checks,error=e.ToString()}));return 1;}}
 }
}
