using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using U=Polytopia.Data.UnitData.Type;
namespace Deception.Tests
{
 public static class DisbandPresentationTests
 {
  static int Assertions;static readonly List<string> Cases=new(),Failures=new();
  static void Check(bool b,string text){Assertions++;if(!b)throw new Exception(text);}
  static void Test(string name,Action action){Cases.Add(name);try{action();}catch(Exception e){Failures.Add(name+": "+e.Message);}}
  sealed class World
  {
   internal GameState State=new();internal UnitState Unit=new();internal PlayerState Player=new();internal ClientBase Client=new();
   internal World(U form=U.dcp_mimic_form_cloak,bool aquatic=false,bool mimic=true)
   {
    NativeDisbandPresentation.Clear();GameManager.GameState=State;GameManager.LocalPlayer=Player;GameManager.Client=Client;GameManager.Viewing=true;
    NativeRegroupSession.Enabled=true;Localization.Locale="en";CommandExtensions.Throw=false;CommandExtensions.BeforeName=null;Entry.Notes.Clear();
    Unit.type=form;State.Map.Tile.unit=Unit;
    State.Identity=new(31,Unit.id,true,mimic?LedgerActualKind.Mimic:LedgerActualKind.Genuine,true,
     new("cloak",new("imperius","default"),new("imperius","default"),new("imperius","default"),1,100,100,false,ApparentConcealment.Hidden,-1,Array.Empty<string>()),PaidInvestmentKind.Known,9,AmphibiousMimic:aquatic);
   }
   internal string Name()=>CommandExtensions.GetShortInfo(new DisbandCommand(),default);
   internal string Raw()=>Unit.UnitData.displayName;
   internal string Term(string? term=null)=>Localization.GetSkinned(Player.Skin,Player.tribe,term??Raw(),null);
   internal string Fingerprint()=>JsonSerializer.Serialize(new{Unit.id,Unit.owner,Unit.health,Unit.moved,Unit.attacked,Unit.investment,Unit.type,Unit.UnitData.displayName,Unit.coordinates.X,Unit.coordinates.Y,State.Commands,State.Actions,State.Stars,State.Identity});
  }
  public static int Run()
  {
   var species=new[]{(U.dcp_mimic_form_cloak,false,true,"Mimic"),(U.dcp_mimic_form_transportship,false,true,"Mimic"),(U.dcp_mimic_form_dcp_spy,false,true,"Mimic"),(U.dcp_mimic,false,true,"Mimic"),(U.dcp_mermimic,true,true,"Mermimic"),(U.dcp_mimic_form_cloak,true,true,"Mermimic"),(U.dcp_spy,false,false,"Spy"),(U.dcp_spymaid,false,false,"Spymaid"),(U.warrior,false,false,"Warrior"),(U.cloak,false,false,"Cloak"),(U.transportship,false,false,"Raft")};
   foreach(var (form,aquatic,mimic,label) in species)foreach(string locale in new[]{"en","translated"})
    Test("actual disband "+form+" aquatic="+aquatic+" locale="+locale,()=>{
     var w=new World(form,aquatic,mimic);Localization.Locale=locale;string before=w.Fingerprint();
     string expected=locale=="en"?"Disband "+label:"localized:Disband localized:"+label;
     Check(w.Name()==expected,"wrong localized underlying name "+w.Name());
     Check(w.Fingerprint()==before,"naming changed real body, investment, ledger or command counters");
     Check(w.Term()==(mimic&&!new[]{U.dcp_mimic,U.dcp_mermimic}.Contains(form)?w.Raw():(locale=="en"?label:"localized:"+label)),"scope escaped into unrelated localization");
     Check(Entry.Notes.Count==0,"unexpected production exception");
    });
   Test("broken native carrier key is never shown in confirmation",()=>{var w=new World();w.Unit.UnitData.OverrideName="unit.names.disguised cloak";Check(w.Name()=="Disband Mimic","carrier key leak remains");Check(w.Raw()=="unit.names.disguised cloak","shared UnitData modified");});
   foreach(var form in new[]{U.dcp_spy,U.dcp_spymaid,U.warrior})Test("actual name replaces apparent key "+form,()=>{var w=new World(form,mimic:false);w.Unit.UnitData.OverrideName="unit.names.disguised cloak";Check(w.Name()=="Disband "+RecruitmentCatalog.Label(form.ToString()),"apparent name used for actual disband");});
   foreach(var form in new[]{U.dcp_spy,U.dcp_spymaid,U.warrior,U.transportship})Test("unmasked genuine keeps native naming "+form,()=>{var w=new World(form,mimic:false);w.State.Identity=null;Check(w.Name()=="Disband "+RecruitmentCatalog.Label(form.ToString()),"ordinary native label changed");});
   var guards=new Dictionary<string,Action<World>>{
    ["foreign viewer"]=w=>w.Player.Id=2,["foreign owner"]=w=>w.Unit.owner=2,["foreign turn"]=w=>w.State.CurrentPlayer=2,
    ["replay"]=w=>w.Client.IsReplay=true,["spectator"]=w=>w.Client.IsSpectating=true,["handoff"]=w=>GameManager.Viewing=false,
    ["spent move"]=w=>w.Unit.moved=true,["spent action"]=w=>w.Unit.attacked=true,["dead"]=w=>w.Unit.health=0,
    ["no Free Spirit"]=w=>w.State.FreeSpirit=false,["no session"]=w=>NativeRegroupSession.Enabled=false,["ended game"]=w=>w.State.Started=false,
    ["hidden tile"]=w=>w.State.Map.Tile.Explored=false,["missing identity"]=w=>w.State.Identity=null,["retired identity"]=w=>w.State.Identity=w.State.Identity! with{Alive=false},
    ["wrong native identity"]=w=>w.State.Identity=w.State.Identity! with{NativeId=19},["Rogue"]=w=>w.State.Identity=w.State.Identity! with{Rogue=new(2,1,4)},
    ["genuine cannot alias carrier"]=w=>w.State.Identity=w.State.Identity! with{Kind=LedgerActualKind.Genuine}
   };
   foreach(var (name,change) in guards)Test("no private name for "+name,()=>{var w=new World();change(w);string before=w.Fingerprint();Check(w.Name()=="Disband "+w.Raw(),"unauthorized name substitution");Check(w.Fingerprint()==before,"guard mutated state");});
   Test("command actor must be actual owner",()=>{var w=new World();Check(CommandExtensions.GetShortInfo(new DisbandCommand{PlayerId=2},default)=="Disband "+w.Raw(),"foreign command obtained private name");});
   Test("base wrapper casts to native Disband before coordinate lookup",()=>{var w=new World();Check(CommandExtensions.GetShortInfo(new CommandProxy(),default)=="Disband Mimic","base CommandBase wrapper lacks Coordinates");Check(Entry.Notes.Count==0,"base wrapper raised missing property");});
   var invalidations=new Dictionary<string,Action<World>>{
    ["turn"]=w=>w.State.CurrentTurn++,["client"]=w=>GameManager.Client=new(),["viewer"]=w=>GameManager.LocalPlayer=new(),
    ["state"]=w=>GameManager.GameState=new(),["map"]=w=>w.State.Map=new(),["unit replaced"]=w=>w.State.Map.Tile.unit=new(),
    ["identity reused"]=w=>w.State.Identity=w.State.Identity! with{LogicalId=32},["species"]=w=>w.State.Identity=w.State.Identity! with{AmphibiousMimic=true},
    ["native id changed"]=w=>w.Unit.id=19,["native form changed"]=w=>w.Unit.type=U.dcp_spy,
    ["Rogue acquired"]=w=>w.State.Identity=w.State.Identity! with{Rogue=new(2,1,4)},["reset"]=w=>NativeDisbandPresentation.Clear()
   };
   foreach(var (name,change) in invalidations)Test("scope invalidated by "+name,()=>{var w=new World();NativeDisbandPresentation.Begin(new DisbandCommand(),out var scope);string raw=w.Raw();change(w);Check(w.Term(raw)==raw,"stale private term survived "+name);NativeDisbandPresentation.Finished(null,scope);Check(w.Term(raw)==raw,"scope returned after finalizer");});
   Test("nested unrelated descriptions fence and restore",()=>{var w=new World();NativeDisbandPresentation.Begin(new DisbandCommand(),out var outer);NativeDisbandPresentation.Begin(new CommandBase(),out var inner);Check(w.Term()==w.Raw(),"nested unrelated action used private name");var fault=new Exception("same fault");Check(ReferenceEquals(NativeDisbandPresentation.Finished(fault,inner),fault),"native exception changed");Check(w.Term()=="Mimic","outer scope not restored");NativeDisbandPresentation.Finished(null,outer);Check(w.Term()==w.Raw(),"outer scope leaked");});
   Test("native exception clears scope",()=>{var w=new World();CommandExtensions.Throw=true;try{w.Name();Check(false,"native exception swallowed");}catch(InvalidOperationException){}Check(w.Term()==w.Raw(),"exception leaked scope");});
   Test("only exact one name lookup is replaced",()=>{var w=new World();NativeDisbandPresentation.Begin(new DisbandCommand(),out var s);Check(w.Term("action.disband")=="Disband ","action label changed");string raw=w.Raw();Check(Localization.GetSkinned(PolytopiaBackendBase.Common.SkinType.Special,w.Player.tribe,raw,null)==raw,"unrelated skin intercepted");Check(Localization.GetSkinned(w.Player.Skin,PolytopiaBackendBase.Common.TribeType.Aquarion,raw,null)==raw,"unrelated tribe intercepted");Check(w.Term()=="Mimic","name lookup missed");Check(w.Term()==raw,"scope used more than once");NativeDisbandPresentation.Finished(null,s);});
   Test("reset cannot resurrect outer scope",()=>{var w=new World();NativeDisbandPresentation.Begin(new DisbandCommand(),out var a);NativeDisbandPresentation.Begin(new CommandBase(),out var b);NativeDisbandPresentation.Clear();NativeDisbandPresentation.Finished(null,b);NativeDisbandPresentation.Finished(null,a);Check(w.Term()==w.Raw(),"reset revived scope");});
   Test("skin-specific native localization is retained",()=>{var w=new World();w.Player.Skin=PolytopiaBackendBase.Common.SkinType.Special;w.Player.tribe=PolytopiaBackendBase.Common.TribeType.Cymanti;Check(w.Name()=="Disband Mimic","visual skin path omitted");});
   Test("exact hook signatures resolve",()=>{var h=new HarmonyLib.Harmony();NativeDisbandPresentation.Install(h);Check(h.Targets.Count==2,"unexpected hook surface");Check(h.Targets[0].Name=="GetShortInfo"&&h.Targets[1].Name=="GetSkinned","wrong native producers");});
   Console.WriteLine(JsonSerializer.Serialize(new{passed=Failures.Count==0,cases=Cases.Count,assertions=Assertions,failed=Failures.Count,tests=Cases,failures=Failures,scope="Actual NativeDisbandPresentation and NativeApi source; native-shaped formatter and authorization fixture ports, no game execution"}));
   return 0;
  }
 }
}
