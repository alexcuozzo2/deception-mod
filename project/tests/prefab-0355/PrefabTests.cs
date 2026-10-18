using System;
using System.Linq;
using System.Text.Json;
using Deception.PolyModAdapter;
using UnitType=Polytopia.Data.UnitData.Type;
using Tribe=PolytopiaBackendBase.Common.TribeType;
using Skin=PolytopiaBackendBase.Common.SkinType;
namespace Deception.Tests
{
 public static class PrefabTests
 {
  static int Cases,Assertions;
  static void Check(bool condition,string reason){Assertions++;if(!condition)throw new Exception(reason);}
  static void Case(Action action){Cases++;Reset();action();}
  static void Reset()
  {
   RecruitmentHooks.Registered=true;RecruitmentHooks.Forms.Clear();
   var values=EnumCache<UnitType>.Values;values.Clear();
   foreach(var value in Enum.GetValues<UnitType>())values.Add(value.ToString().ToLowerInvariant(),value);
   EnumCache<UnitType>.NameReads=EnumCache<UnitType>.ValueReads=0;
   PrefabManager.Units.Clear();PrefabManager.Christmas.Clear();PrefabManager.IsChristmas=false;PrefabManager.Patch=true;PrefabManager.OriginalCalls=0;
   foreach(var value in Enum.GetValues<UnitType>())PrefabManager.Units[(int)value]=new(value.ToString());
   PrefabManager.Units[0]=new("Default nature Bunny");
  }
  static UnitType Form(string native,int custom)
  {
   string id="dcp_mimic_form_"+native;var value=(UnitType)custom;
   EnumCache<UnitType>.Values.Add(id,value);RecruitmentHooks.Forms.Add(id,new(native,id));return value;
  }
  static string Fingerprint(UnitState s)=>$"{s.id}/{s.owner}/{s.health}/{s.investment}/{s.x}/{s.y}/{s.moved}/{s.attacked}/{(int)s.type}/{(int)s.data.type}/{s.effects.GetHashCode()}";
  public static int Run()
  {
   try
   {
    Case(()=>{
     var h=new HarmonyLib.Harmony();NativeMimicPrefabLookup.Install(h);
     Check(h.Hooks.Count==1,"Unexpected hook count");var hook=h.Hooks.Single();
     Check(hook.Target.ReturnType==typeof(Unit)&&hook.Target.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(UnitType),typeof(Tribe),typeof(Skin)}),"Hook does not bind exact unit prefab overload");
     Check(hook.Prefix!.Method.GetParameters().Single().ParameterType==typeof(UnitType).MakeByRefType(),"Prefix argument is not exact typed ref enum");
    });
    Case(()=>{
     var custom=Form("firedragon",1042);PrefabManager.LoaderAlias(custom,"firedragon");
     Check(PrefabManager.Units[1042].Name=="Default nature Bunny","Fixture did not reproduce ignored FireDragon TitleCase parse failure");
     PrefabManager.Patch=false;Check(PrefabManager.GetPrefab(custom,Tribe.Elyrion,Skin.Default).Name=="Default nature Bunny","Old native lookup did not cache wrong alias");
     Check(PrefabManager.Units[6042].Name=="Default nature Bunny","Old custom full key was not populated");
     PrefabManager.Patch=true;
     Check(PrefabManager.GetPrefab(custom,Tribe.Elyrion,Skin.Default).Name=="FireDragon","Existing wrong custom cache defeated corrected native lookup");
     Check(PrefabManager.Last==(UnitType.FireDragon,Tribe.Elyrion,Skin.Default),"Native visual lookup changed tribe or skin");
     Check(PrefabManager.Units[1042].Name=="Default nature Bunny"&&PrefabManager.Units[6042].Name=="Default nature Bunny","Adapter mutated shared custom cache entries");
     Check(PrefabManager.OriginalCalls==2,"Adapter skipped or recursively called native lookup");
    });
    Case(()=>{
     var custom=Form("amphibian",1001);PrefabManager.Units[1001]=new("Nature Scout");
     PrefabManager.LoaderAlias(custom,"amphibian");
     Check(PrefabManager.Hash(UnitType.Scout,Tribe.Nature,Skin.Default)==(int)custom,"Collision fixture numeric premise is wrong");
     PrefabManager.Patch=false;Check(PrefabManager.GetPrefab(custom,Tribe.Elyrion,Skin.Default).Name=="Nature Scout","Loader TryAdd collision was not reproduced");
     PrefabManager.Patch=true;Check(PrefabManager.GetPrefab(custom,Tribe.Elyrion,Skin.Default).Name=="Amphibian","Carrier namespace collision remains");
     Check(PrefabManager.GetPrefab(UnitType.Scout,Tribe.Nature,Skin.Default).Name=="Nature Scout","Fix overwrote genuine native prefab key");
    });
    Case(()=>{
     var custom=Form("archer",1002);PrefabManager.LoaderAlias(custom,"archer");
     PrefabManager.Units[8002]=new("Kickoo Warrior");
     PrefabManager.Patch=false;Check(PrefabManager.GetPrefab(custom,Tribe.Imperius,Skin.Default).Name=="Kickoo Warrior","Full-key neighboring tribe collision was not reproduced");
     PrefabManager.Patch=true;Check(PrefabManager.GetPrefab(custom,Tribe.Imperius,Skin.Default).Name=="Archer","Full-key neighboring tribe collision remains");
    });
    Case(()=>{
     var custom=Form("firedragon",1042);
     foreach(var tribe in Enum.GetValues<Tribe>())foreach(var skin in Enum.GetValues<Skin>())
     {
      var expected=new Unit($"FireDragon/{tribe}/{skin}");PrefabManager.Units[PrefabManager.Hash(UnitType.FireDragon,tribe,skin)]=expected;
      Check(ReferenceEquals(PrefabManager.GetPrefab(custom,tribe,skin),expected),"Genuine native tribe/skin-specific prefab was lost");
      Check(PrefabManager.Last==(UnitType.FireDragon,tribe,skin),"Tribe/skin argument changed");
     }
    });
    Case(()=>{
     var custom=Form("firedragon",1042);PrefabManager.IsChristmas=true;var expected=new Unit("Seasonal FireDragon");PrefabManager.Christmas[UnitType.FireDragon]=expected;
     Check(ReferenceEquals(PrefabManager.GetPrefab(custom,Tribe.Elyrion,Skin.DarkElf),expected),"Native seasonal lookup was bypassed");
    });
    Case(()=>{
     foreach(var type in Enum.GetValues<UnitType>())
     {
      var before=type;var value=type;NativeMimicPrefabLookup.BeforeLookup(ref value);
      Check(value==before,"Non-carrier native enum modified");
     }
     Check(EnumCache<UnitType>.ValueReads==0,"Ordinary native lookup resolved an unnecessary custom source");
    });
    Case(()=>{
     var custom=Form("firedragon",1042);RecruitmentHooks.Registered=false;var value=custom;
     NativeMimicPrefabLookup.BeforeLookup(ref value);
     Check(value==custom&&EnumCache<UnitType>.NameReads==0&&EnumCache<UnitType>.ValueReads==0,"Unregistered lookup used native registry");
    });
    Case(()=>{
     var value=(UnitType)7777;EnumCache<UnitType>.Values.Add("other_mod_unit",value);var before=value;
     NativeMimicPrefabLookup.BeforeLookup(ref value);Check(value==before,"Other mod type was remapped");
     value=(UnitType)7778;EnumCache<UnitType>.Values.Add("dcp_mimic_form_unregistered",value);before=value;
     NativeMimicPrefabLookup.BeforeLookup(ref value);Check(value==before,"Unregistered prefix lookalike was remapped");
     value=(UnitType)1027;EnumCache<UnitType>.Values.Add("dcp_mimic",value);before=value;
     NativeMimicPrefabLookup.BeforeLookup(ref value);Check(value==UnitType.Warrior,"Exposed Mimic did not resolve its native Warrior base");
    });
    Case(()=>{
     var custom=Form("mermaiddagger",1044);PrefabManager.LoaderAlias(custom,"mermaiddagger");
     Check(PrefabManager.Units[1044].Name=="Default nature Bunny","Mixed-case MermaidDagger regression absent");
     Check(PrefabManager.GetPrefab(custom,Tribe.Aquarion,Skin.Default).Name=="MermaidDagger","Mixed-case source still resolves None");
    });
    Case(()=>{
     Form("firedragon",1042);var state=new UnitState();var sourceData=state.data;string before=Fingerprint(state);
     var prefab=PrefabManager.GetPrefab(state.type,Tribe.Elyrion,Skin.Default);
     Check(prefab.Name=="FireDragon"&&Fingerprint(state)==before&&ReferenceEquals(state.data,sourceData),"Presentation lookup modified authoritative unit/data/action state");
    });
    Case(()=>{
     var custom=Form("firedragon",1042);EnumCache<UnitType>.Values.Remove("firedragon");var value=custom;bool rejected=false;
     try{NativeMimicPrefabLookup.BeforeLookup(ref value);}catch(InvalidOperationException){rejected=true;}
     Check(rejected&&value==custom,"Missing native source silently selected None or partially changed argument");
    });
    Case(()=>{
     // New carrier forms use the registered source mapping, not a duplicated
     // hand-maintained type switch; includes later Egg/Baby growth forms.
     foreach(var pair in new[]{("warrior",1026),("rider",1021),("polytaur",1045),("firedragon",1042),("dragonegg",1047),("babydragon",1046)})
     {
      var custom=Form(pair.Item1,pair.Item2);var expected=EnumCache<UnitType>.GetType(pair.Item1);
      Check(PrefabManager.GetPrefab(custom,Tribe.Elyrion,Skin.Default).Name==expected.ToString(),"Registered source form not followed: "+pair.Item1);
     }
    });
    Case(()=>{
     foreach(var pair in new[]{("cloak",1055,UnitType.Cloak),("mermaidcloak",1056,UnitType.MermaidCloak)})
     {
      var custom=Form(pair.Item1,pair.Item2);PrefabManager.LoaderAlias(custom,pair.Item1);
      if(pair.Item1=="mermaidcloak")Check(PrefabManager.Units[pair.Item2].Name=="Default nature Bunny","MermaidCloak mixed-case fallback fixture changed");
      foreach(var tribe in new[]{Tribe.Imperius,Tribe.Aquarion})
      {
       var expected=new Unit($"{pair.Item3}/{tribe}/Default");PrefabManager.Units[PrefabManager.Hash(pair.Item3,tribe,Skin.Default)]=expected;
       Check(ReferenceEquals(PrefabManager.GetPrefab(custom,tribe,Skin.Default),expected),"Cloak carrier did not use exact native source prefab");
       Check(PrefabManager.Last==(pair.Item3,tribe,Skin.Default),"Cloak visual lookup changed source, tribe or skin");
      }
     }
    });
    Case(()=>{
     // Exposed Mimic and genuine Spy both borrow Warrior's native body frame;
     // art adapters draw their owned layers without entering custom hash space.
     EnumCache<UnitType>.Values.Add("dcp_mimic",(UnitType)1027);EnumCache<UnitType>.Values.Add("dcp_spy",(UnitType)1065);
     var copied=Form("dcp_spy",1066);
     foreach(var tribe in new[]{Tribe.Imperius,Tribe.Aquarion,Tribe.Cymanti})foreach(var skin in new[]{Skin.Default,Skin.DarkElf,Skin.Magma}) {
      var expected=new Unit($"Warrior/{tribe}/{skin}");PrefabManager.Units[PrefabManager.Hash(UnitType.Warrior,tribe,skin)]=expected;
      foreach(var type in new[]{(UnitType)1027,(UnitType)1065,copied}) {
       var bad=new Unit("Colliding custom cache entry");int hash=PrefabManager.Hash(type,tribe,skin);PrefabManager.Units[hash]=bad;
       Check(ReferenceEquals(PrefabManager.GetPrefab(type,tribe,skin),expected),"Exposed Mimic/Spy source did not use Warrior frame");
       Check(PrefabManager.Last==(UnitType.Warrior,tribe,skin)&&ReferenceEquals(PrefabManager.Units[hash],bad),"Spy/Mimic frame changed tribe/skin or shared custom cache");
      }
     }
     Check(PrefabManager.OriginalCalls==27,"Spy/Mimic remapping skipped or reentered native lookup");
    });
    Case(()=>{
     EnumCache<UnitType>.Values.Add("dcp_mimic",(UnitType)1027);EnumCache<UnitType>.Values.Add("dcp_spy",(UnitType)1065);var copy=Form("dcp_spy",1066);
     RecruitmentHooks.Registered=false;
     foreach(var original in new[]{(UnitType)1027,(UnitType)1065,copy}) {
      var value=original;NativeMimicPrefabLookup.BeforeLookup(ref value);Check(value==original,"Unregistered special lookup was remapped");
     }
     Check(EnumCache<UnitType>.NameReads==0&&EnumCache<UnitType>.ValueReads==0,"Unregistered special lookup read incomplete enum cache");
    });
    Case(()=>{
     EnumCache<UnitType>.Values.Add("dcp_mimic",(UnitType)1027);EnumCache<UnitType>.Values.Add("dcp_spy",(UnitType)1065);var copy=Form("dcp_spy",1066);
     EnumCache<UnitType>.Values.Remove("warrior");
     foreach(var original in new[]{(UnitType)1027,(UnitType)1065,copy}) {
      var value=original;bool rejected=false;try{NativeMimicPrefabLookup.BeforeLookup(ref value);}catch(InvalidOperationException){rejected=true;}
      Check(rejected&&value==original,"Missing Warrior silently rewrote exposed Mimic or Spy to None");
     }
    });
    Case(()=>{
     EnumCache<UnitType>.Values.Add("dcp_spy",(UnitType)1065);Form("dcp_spy",1066);
     foreach(var type in new[]{(UnitType)1065,(UnitType)1066}) {
      var state=new UnitState{type=type,health=type==(UnitType)1065?100:10,investment=type==(UnitType)1065?8:3,data=new(){type=type}};
      string before=Fingerprint(state);var source=state.data;var result=PrefabManager.GetPrefab(state.type,Tribe.Cymanti,Skin.Magma);
      Check(result.Name=="Warrior"&&Fingerprint(state)==before&&ReferenceEquals(source,state.data),"Spy lookup rewrote actual type/HP/investment/data/action identity");
     }
    });
    Case(()=>{
     foreach(var pair in new[]{("raychi",1060,UnitType.Raychi),("jelly",1061,UnitType.Jelly),("shark",1062,UnitType.Shark),("aquapult",1063,UnitType.Aquapult),("island",1064,UnitType.Island)}) {
      var custom=Form(pair.Item1,pair.Item2);var expected=new Unit("Exact "+pair.Item1);PrefabManager.Units[PrefabManager.Hash(pair.Item3,Tribe.Aquarion,Skin.Default)]=expected;
      Check(ReferenceEquals(PrefabManager.GetPrefab(custom,Tribe.Aquarion,Skin.Default),expected)&&PrefabManager.Last==(pair.Item3,Tribe.Aquarion,Skin.Default),"Water disguise did not preserve exact native source prefab");
     }
    });
    Case(()=>{
     EnumCache<UnitType>.Values.Add("dcp_motherfogger",(UnitType)1084);var copy=Form("dcp_motherfogger",1085);
     foreach(var type in new[]{(UnitType)1084,copy})foreach(var tribe in new[]{Tribe.Imperius,Tribe.Aquarion,Tribe.Cymanti}) {
      var state=new UnitState{type=type,health=type==(UnitType)1084?100:10,data=new(){type=type}};string before=Fingerprint(state);
      var result=PrefabManager.GetPrefab(type,tribe,Skin.Default);
      Check(result.Name=="Warrior"&&PrefabManager.Last==(UnitType.Warrior,tribe,Skin.Default),"Fogger custom identity entered unsafe native prefab hash space");
      Check(Fingerprint(state)==before,"Fogger art lookup edited real state");
     }
    });
    Console.WriteLine(JsonSerializer.Serialize(new{status="PASS",cases=Cases,assertions=Assertions,scope="Actual presentation prefix and NativeApi on synthetic audited native prefab hash/fallback and loader-alias seams; no native rendered success claim"}));return 0;
   }
   catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{status="FAIL",cases=Cases,assertions=Assertions,error=e.ToString()}));return 1;}
  }
 }
}
