// Synthetic native prefab lookup; actual native metadata values and hash/fallback order.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnitType=Polytopia.Data.UnitData.Type;
using Tribe=PolytopiaBackendBase.Common.TribeType;
using Skin=PolytopiaBackendBase.Common.SkinType;
namespace HarmonyLib
{
 public sealed class HarmonyMethod { public MethodInfo Method;public HarmonyMethod(MethodInfo method){Method=method;} }
 public sealed class Harmony
 {
  public List<(MethodInfo Target,HarmonyMethod? Prefix)> Hooks=new();
  public void Patch(MethodInfo target,HarmonyMethod? pre,HarmonyMethod? post,object? t,HarmonyMethod? final,object? il)=>Hooks.Add((target,pre));
 }
}
namespace Polytopia.Data
{
 public sealed class UnitData { public Type type; public enum Type {None=0,Scout=1,Warrior=2,Rider=3,Knight=4,Defender=5,Ship=6,Battleship=7,Catapult=8,Archer=9,MindBender=10,Swordsman=11,Giant=12,Bunny=13,Boat=14,Polytaur=15,Navalon=16,DragonEgg=17,BabyDragon=18,FireDragon=19,Amphibian=20,Tridention=21,Mooni=22,BattleSled=23,IceFortress=24,IceArcher=25,Crab=26,Gaami=27,Hexapod=28,Doomux=29,Phychi=30,Kiton=31,Exida=32,Centipede=33,Segment=34,Raychi=35,Shaman=36,Dagger=37,Cloak=38,Cloak_Boat=39,Pirate=40,Bombership=41,Scoutship=42,Transportship=43,Rammership=44,Juggernaut=45,MermaidWarrior=46,MermaidArcher=47,MermaidSwordsman=48,MermaidDefender=49,MermaidCloak=50,MermaidDagger=51,Jelly=52,Shark=53,Siren=54,Aquapult=55,Boomchi=56,Island=57,Ciru=58,Mantis=59,BugEgg=60,Moth=61,Larva=62} }
}
namespace PolytopiaBackendBase.Common
{
 public enum TribeType {None=0,Nature=1,Aimo=2,Aquarion=3,Bardur=4,Elyrion=5,Hoodrick=6,Imperius=7,Kickoo=8,Luxidoor=9,Oumaji=10,Quetzali=11,Vengir=12,Xinxi=13,Yadakk=14,Zebasi=15,Polaris=16,Cymanti=17}
 public enum SkinType {None=-1,Default=0,Ranger=1,Ninja=2,Baerion=3,Scholar=5,Mercenary=7,Sfinx=8,Skeleton=9,Arty=10,Pirate=11,Aibo=12,Urkaz=13,Ikarus=14,DarkElf=15,Swamp=17,Magma=18,Cute=19,Test=2000}
}
public static class EnumCache<T>where T:struct,Enum
{
 public static readonly Dictionary<string,T> Values=Enum.GetValues<T>().ToDictionary(v=>v.ToString().ToLowerInvariant(),v=>v);
 public static int NameReads,ValueReads;
 public static T GetType(string id){ValueReads++;return Values.GetValueOrDefault(id);}
 public static string GetName(T value){NameReads++;return Values.FirstOrDefault(p=>EqualityComparer<T>.Default.Equals(p.Value,value)).Key??"";}
}
namespace Deception.Core
{
 public sealed record RecruitmentForm(string NativeId,string VariantId);
 public static class SpyCatalog { public const string Id="dcp_spy",AquarionId="dcp_spymaid"; }
 public static class FoggerCatalog { public const string Id="dcp_motherfogger"; }
 public static class AquarionCatalog { public const string MimicId="dcp_mermimic"; }
 public static class RecruitmentCatalog
 {public const string MenuId="dcp_mimic";public static bool IsMimicId(string id)=>id=="dcp_mimic"||id==AquarionCatalog.MimicId||id.StartsWith("dcp_mimic_form_",StringComparison.Ordinal);}
}
namespace Deception.PolyModAdapter
{
 internal static class RecruitmentHooks
 {
  internal const string UT="Polytopia.Data.UnitData+Type";
  public static bool Registered;
  public static readonly Dictionary<string,Deception.Core.RecruitmentForm> Forms=new(StringComparer.Ordinal);
 }
}
public sealed record Unit(string Name);
public sealed class UnitState
{
 public uint id=23;public byte owner=5;public int health=10,investment=3,x=4,y=7;
 public bool moved=true,attacked=true;public UnitType type=(UnitType)1042;
 public Polytopia.Data.UnitData data=new(){type=(UnitType)1042};public object effects=new();
}
public static class PrefabManager
{
 public static readonly Dictionary<int,Unit> Units=new();
 public static readonly Dictionary<UnitType,Unit> Christmas=new();
 public static bool IsChristmas,Patch=true;
 public static int OriginalCalls;public static (UnitType Type,Tribe Tribe,Skin Skin) Last;
 public static int Hash(UnitType type,Tribe tribe,Skin skin)=>(int)type+(int)tribe*1000+(int)skin*1000000;
 public static Unit GetPrefab(UnitType type,Tribe tribe,Skin skin)
 {
  if(Patch)Deception.PolyModAdapter.NativeMimicPrefabLookup.BeforeLookup(ref type);
  OriginalCalls++;Last=(type,tribe,skin);
  // Audited GetPrefab0x29724d0: seasonal override first, full key next;
  // fallback skin-only, tribe-only, bare type; cache found fallback under full key.
  if(IsChristmas&&Christmas.TryGetValue(type,out var christmas))return christmas;
  int full=Hash(type,tribe,skin);
  if(Units.TryGetValue(full,out var direct))return direct;
  if((int)tribe!=0||(int)skin!=0)
   foreach(int key in new[]{Hash(type,Tribe.None,skin),Hash(type,tribe,Skin.Default),(int)type})
    if(key!=0&&Units.TryGetValue(key,out var found)){Units[full]=found;return found;}
  return Units[0];
 }
 public static void LoaderAlias(UnitType custom,string nativeId)
 {
  // Installed PolyMod Loader.ProcessPrefabs: ignored TryParse result leaves
  // None on miss, then units.TryAdd(custom, units[native default key]).
  Enum.TryParse<UnitType>(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(nativeId),out var parsed);
  Units.TryAdd((int)custom,Units[(int)parsed]);
 }
}
