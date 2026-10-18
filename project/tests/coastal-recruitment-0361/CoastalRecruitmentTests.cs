using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using Polytopia.Data;
namespace Deception.Tests {
 public static class CoastalRecruitmentTests {
  static int Assertions;static readonly List<string> Cases=new();static string Input="";
  static void Check(bool yes,string why){Assertions++;if(!yes)throw new Exception(why);}
  static void Test(string name,Action body){try{body();Cases.Add(name);}catch(Exception e){throw new Exception(name,e);}}
  static readonly string[] WaterForms={"raychi","jelly","shark","aquapult","island"};
  static GameState Make(string tribe,string[]? unlocked=null,bool propaganda=false){
   CommandValidation.Fail=false;CommandValidation.Calls=0;NativeFreshSession.Block=false;NativeTrainLeafProbeTrigger.Requested=false;
   var s=new GameState();s.Player.tribe=Enum.Parse<PolytopiaBackendBase.Common.TribeType>(tribe);s.Player.Propaganda=propaganda;
   s.Player.Unlocked=(unlocked??new[]{"warrior"}).ToHashSet(StringComparer.Ordinal);
   var mutation=RecruitmentCatalog.Add(TechTree.AddDeception(Input).Json);
   RecruitmentHooks.Register(mutation.Forms,NativeRecruitmentRosters.AllResearchRosters(Input));
   using var json=JsonDocument.Parse(mutation.Json);
   foreach(var pair in json.RootElement.GetProperty("unitData").EnumerateObject()){
    if(!Enum.TryParse<UnitData.Type>(pair.Name,out var t))continue;
    s.GameLogicData.Data.Add(t,new UnitData{type=t,movementTerrain=pair.Value.TryGetProperty("movementTerrain",out var terrain)?terrain.EnumerateArray().Select(v=>v.GetString()!).ToArray():Array.Empty<string>()});
   }
   typeof(RecruitmentHooks).GetField("GetUnit",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,typeof(GameLogicData).GetMethod("GetUnitData"));
   typeof(RecruitmentHooks).GetField("GetUnits",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,typeof(GameLogicData).GetMethod("GetUnlockedUnits"));
   typeof(RecruitmentHooks).GetField("HasTech",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,typeof(PlayerExtensions).GetMethod("HasTech"));
   RecruitmentHooks.TrainValid=typeof(TrainCommand).GetMethod("IsValid")!;GameManager.GameState=s;
   s.Neighbors[(1,1)]=new[]{"field","forest"};s.Neighbors[(2,2)]=new[]{"water"};s.Neighbors[(3,3)]=new[]{"wetland"};s.Neighbors[(4,4)]=new[]{"ocean"};
   return s;
  }
  static TrainCommand Cmd(GameState s,string form,int city=2)=>new(s.Player.Id,Enum.Parse<UnitData.Type>(RecruitmentCatalog.Variant(form)),new(city,city));
  static bool Allowed(GameState s,string form,int city=2)=>RecruitmentHooks.CheckEligibility(s,Cmd(s,form,city),out _);
  public static int Run(){try{
   Input=File.ReadAllText(Environment.GetEnvironmentVariable("DECEPTION_CAPTURED_TREE_FIXTURE")!);
   foreach(string form in WaterForms)foreach(int city in new[]{1,2,3,4})Test("terrain-"+form+"-"+city,()=>{
    var s=Make(form is "raychi" or "island"?"cymanti":"aquarion",new[]{form});
    Check(Allowed(s,form,city)==(city!=1),"native adjacent terrain decides water-only form");
    Check(RecruitmentHooks.CityAppearanceRoster(s,s.Player,new WorldCoordinates(city,city)).Contains(form)==(city!=1),"wheel uses same exact native predicate");
    Check(ReferenceEquals(CommandValidation.LastData,s.GameLogicData.GetUnitData(Enum.Parse<UnitData.Type>(form))),"native original form, not counterfeit carrier, passed");
    Check(CommandValidation.LastCity.X==city&&CommandValidation.LastCity.Y==city,"exact city coordinates");
    Check(s.Player.Currency==3&&s.Player.Unlocked.SetEquals(new[]{form}),"eligibility is read-only");
   });
   Test("coastal-all-research-tribe-rosters",()=>{
    var a=Make("aquarion",new[]{"mermaidwarrior"},true);var ar=RecruitmentHooks.CityAppearanceRoster(a,a.Player,new WorldCoordinates(2,2));
    Check(new[]{"jelly","shark","aquapult"}.All(ar.Contains)&&!ar.Contains("raychi")&&!ar.Contains("island"),"Aquarion replacements only");
    var c=Make("cymanti",new[]{"warrior"},true);var cr=RecruitmentHooks.CityAppearanceRoster(c,c.Player,new WorldCoordinates(2,2));
    Check(cr.Contains("raychi")&&cr.Contains("island")&&!cr.Contains("jelly")&&!cr.Contains("shark"),"Cymanti replacements only");
    var ordinary=Make("imperius",new[]{"warrior"},true);Check(!WaterForms.Any(RecruitmentHooks.AppearanceRoster(ordinary,ordinary.Player).Contains),"Propaganda does not grant foreign tribe roster");
   });
   Test("propaganda-bypasses-research-not-city-terrain",()=>{
    var s=Make("aquarion",new[]{"mermaidwarrior"});Check(!Allowed(s,"shark"),"locked native tech excluded");s.Player.Propaganda=true;
    Check(Allowed(s,"shark"),"Propaganda admits locked native tribe form");Check(!Allowed(s,"shark",1),"Propaganda cannot create water unit inland");
    Check(!RecruitmentHooks.CityAppearanceRoster(s,s.Player,new WorldCoordinates(1,1)).Overlaps(WaterForms),"inland wheel hides water-only forms");
   });
   Test("land-and-amphibious-city-behavior-preserved",()=>{
    var s=Make("aquarion",new[]{"mermaidwarrior","amphibian"});foreach(int city in new[]{1,2,3,4}){Check(Allowed(s,"mermaidwarrior",city),"land-capable Mermaid allowed");Check(Allowed(s,"amphibian",city),"amphibious unit not water-only");}
   });
   Test("stale-city-change-rejected-before-native-command-and-action",()=>{
    var s=Make("cymanti",new[]{"raychi"});var command=Cmd(s,"raychi");Check(RecruitmentHooks.CheckEligibility(s,command,out _),"offered on coast");s.Neighbors[(2,2)]=new[]{"field"};
    string why="";bool result=true;Check(!RecruitmentHooks.GuardTrainValidity(command,s,ref why,ref result)&&!result&&why.Contains("terrain"),"stale validation rejected");
    Check(!RecruitmentHooks.GuardTrainCommand(command,s),"command guard rejects stale city via native path fixture bridge");
    var action=new TrainAction(command.PlayerId,command.Type,command.Coordinates);Check(!RecruitmentHooks.GuardTrainAction(action,s,out bool ran)&&!ran,"action rejects stale coordinate without spawn");
   });
   foreach(string gate in new[]{"funds","occupancy","support","ownership"})Test("native-"+gate+"-still-authoritative",()=>{
    var s=Make("cymanti",new[]{"island"});if(gate=="funds")s.Player.Currency=0;if(gate=="occupancy")s.Occupied=true;if(gate=="support")s.Support=false;if(gate=="ownership")s.CityOwned=false;
    var command=Cmd(s,"island");string reason="";bool result=false;Check(RecruitmentHooks.GuardTrainValidity(command,s,ref reason,ref result),"custom gate permits native validation to run");
    Check(!RecruitmentHooks.NativeValid(s,command,out _)&&s.ValidationCalls==1,"native gate retained");Check(!RecruitmentHooks.GuardTrainCommand(command,s),"native invalid command rejected");
   });
   Test("native-action-price-is-one-star",()=>{
    var s=Make("aquarion",new[]{"shark"});var cmd=Cmd(s,"shark");var action=new TrainAction(cmd.PlayerId,cmd.Type,cmd.Coordinates);
    s.Player.Currency=1;Check(RecruitmentHooks.NativeValid(s,cmd,out _),"exactly one star satisfies native funds");Check(RecruitmentHooks.GuardTrainAction(action,s,out bool native)&&native,"one-star native action allowed");action.Cost=8;Check(!RecruitmentHooks.GuardTrainAction(action,s,out native)&&!native,"genuine cost cannot leak into counterfeit action");
   });
   Test("actual-command-city-overrides-unrelated-selected-state",()=>{
    var s=Make("cymanti",new[]{"raychi"});GameManager.GameState=Make("cymanti",new[]{"raychi"});
    Check(Allowed(s,"raychi",2)&&!Allowed(s,"raychi",1),"explicit state+coordinates used, not global display state");
   });
   Test("counterfeit-terrain-does-not-authorize-original-inland",()=>{
    var s=Make("cymanti",new[]{"raychi"});s.GameLogicData.GetUnitData(UnitData.Type.dcp_mimic_form_raychi).movementTerrain=Array.Empty<string>();
    Check(!Allowed(s,"raychi",1),"source gate rejects corrupted unrestricted carrier");
   });
   Test("native-terrain-fault-fails-closed",()=>{
    var s=Make("cymanti",new[]{"raychi"});CommandValidation.Fail=true;string why="";bool result=true;
    Check(!RecruitmentHooks.GuardTrainValidity(Cmd(s,"raychi"),s,ref why,ref result)&&!result,"native failure cannot permit custom train");
    Check(!RecruitmentHooks.GuardTrainAction(new TrainAction(1,UnitData.Type.dcp_mimic_form_raychi,new(2,2)),s,out bool run)&&!run,"replayed action failure closed");
   });
   Test("super-grown-and-carried-forms-not-city-recruitment",()=>{
    var s=Make("aquarion",new[]{"firedragon","transportship","giant"},true);
    foreach(var form in new[]{"firedragon","transportship","giant"})Check(!Allowed(s,form),"not native city-produced: "+form);
   });
   Test("deception-and-private-diagnostic-gates-retained",()=>{
    var s=Make("cymanti",new[]{"raychi"});s.Player.Deception=false;Check(!Allowed(s,"raychi"),"requires Deception");s.Player.Deception=true;
    NativeFreshSession.Block=true;Check(!Allowed(s,"raychi"),"fresh-session authorization gate");NativeFreshSession.Block=false;NativeTrainLeafProbeTrigger.Requested=true;Check(!Allowed(s,"raychi"),"serializer diagnostic blocked");
   });
   Test("ordinary-native-unit-validity-hook-bypasses-custom-city-gate",()=>{
    var s=Make("imperius");CommandValidation.Fail=true;string why="native";bool result=true;
    Check(RecruitmentHooks.GuardTrainValidity(new TrainCommand(1,UnitData.Type.warrior,new(1,1)),s,ref why,ref result)&&result&&why=="native","ordinary validity untouched");
    Check(RecruitmentHooks.GuardTrainCommand(new TrainCommand(1,UnitData.Type.warrior,new(1,1)),s),"ordinary command untouched");
   });
   foreach(string form in RecruitmentCatalog.Supported)Test("unbound-session-"+form,()=>{
    var s=Make("cymanti",new[]{form},true);s.SessionReady=false;
    var command=Cmd(s,form);string why="";bool result=true;
    Check(!RecruitmentHooks.GuardTrainValidity(command,s,ref why,ref result)&&!result&&why.Contains("session"),"unbound state rejected before native validity");
    Check(!RecruitmentHooks.GuardTrainCommand(command,s)&&s.ValidationCalls==1,"no custom command queued");
    var action=new TrainAction(command.PlayerId,command.Type,command.Coordinates);
    Check(!RecruitmentHooks.GuardTrainAction(action,s,out bool ran)&&!ran&&action.trainActionAborted,"replayed rejected action cannot request spawn reaction");
    Check(s.Player.Currency==3&&!s.SessionReady,"no spend or enrollment through eligibility");
   });
   Test("spy-unbound-session-and-native-bypass",()=>{
    var s=Make("imperius");s.SessionReady=false;var spy=new TrainCommand(1,UnitData.Type.dcp_spy,new(1,1));string why="";bool result=true;
    Check(!RecruitmentHooks.GuardTrainValidity(spy,s,ref why,ref result)&&!result&&why.Contains("session"),"genuine Spy also requires persistent match");
    Check(!RecruitmentHooks.GuardTrainCommand(spy,s),"unbound Spy cannot queue");
    var action=new TrainAction(1,UnitData.Type.dcp_spy,new(1,1));Check(!RecruitmentHooks.GuardTrainAction(action,s,out _)&&action.trainActionAborted,"unbound Spy action aborts safely");
    var warrior=new TrainCommand(1,UnitData.Type.warrior,new(1,1));Check(RecruitmentHooks.GuardTrainCommand(warrior,s),"ordinary recruitment unaffected by unavailable mod state");
    s.SessionReady=true;Check(RecruitmentHooks.GuardTrainValidity(spy,s,ref why,ref result)&&RecruitmentHooks.GuardTrainCommand(spy,s),"enrolled Spy keeps native research and purchase gates");
   });
   Console.WriteLine(JsonSerializer.Serialize(new{status="PASS",cases=Cases.Count,assertions=Assertions,tests=Cases,scope="Production recruitment hooks and city roster with synthetic native ports; no native game acceptance"}));return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
