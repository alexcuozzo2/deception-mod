using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
namespace HarmonyLib { public sealed class HarmonyMethod { public HarmonyMethod(MethodInfo m){} } public sealed class Harmony {public void Patch(MethodInfo m,HarmonyMethod? p,HarmonyMethod? q,object? t,HarmonyMethod? f,object? il){}} }
namespace Il2CppSystem.Collections.Generic
{public sealed class List<T>{readonly System.Collections.Generic.List<T> Values=new();public int Count=>Values.Count;public T this[int index]=>Values[index];public void Add(T value)=>Values.Add(value);}}
public enum UnitEffect { Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8 }
public static class EnumCache<T>{public static string GetName(T value)=>value!.ToString()!;}
public sealed class UnitState {public uint id{get;set;}=1;public int health{get;set;}=40;public string type{get;set;}="warrior";public bool moved{get;set;}=true;public bool attacked{get;set;}=false;public Il2CppSystem.Collections.Generic.List<UnitEffect> effects{get;set;}=new();}
public sealed class TileData {public UnitState? unit{get;set;}}
public sealed class WorldCoordinates {}
public sealed class MapData {public TileData Tile=new();}
public sealed class GameState {public MapData Map{get;set;}=new();}
public static class MapDataExtensions {public static TileData GetTile(MapData map,WorldCoordinates at)=>map.Tile;}
public static class ActionUtils {public static ushort GetHealAmount(GameState state,TileData tile)=>40;public static void PerformAttack(GameState state,byte player,WorldCoordinates origin,WorldCoordinates target,int damage){}}
public class Active {public WorldCoordinates Coordinates{get;set;}=new();public WorldCoordinates Origin{get;set;}=new();public bool Valid=true;public bool IsValid(GameState state,out string error){error="";return Valid;}public void Execute(GameState state){}}
public sealed class AttackCommand:Active {} public sealed class BoostCommand:Active {} public sealed class ExplodeCommand:Active {} public sealed class FreezeAreaCommand:Active {} public sealed class HealOthersCommand:Active {} public sealed class SwarmCommand:Active {}
public sealed class ConvertAction:Active {}
public sealed class AttackAction {public WorldCoordinates Target{get;set;}=new();public void Execute(GameState state){}}
public sealed class HealAction {public WorldCoordinates Coordinates{get;set;}=new();public ushort FixedHealAmount{get;set;}public ushort HealAmount{get;set;}public void Execute(GameState state){}}
namespace Deception.PolyModAdapter {
 internal static class NativeRegroupSession {public static bool Enabled=true,IsReady=true;public static int Faults;public static bool Ready(object state)=>IsReady;public static void Prepare(object state){}public static void Fault(string message){Faults++;IsReady=false;}}
 internal static class NativeGameStateEnvelope {
  internal sealed class Snapshot {readonly byte[] Value;internal long Revision{get;}internal Snapshot(long revision,byte[] value){Revision=revision;Value=(byte[])value.Clone();}internal byte[] Bytes()=>(byte[])Value.Clone();}
  internal static Snapshot? Current;internal static Snapshot? Get(object state)=>Current;internal static void Set(object state,long revision,byte[] value)=>Current=new(revision,value);
 }
 internal static class NativeLedgerState {internal static bool Fail;internal static void Validate(object state,long revision,byte[] payload){if(Fail)throw new InvalidDataException("injected");NativeIdentityLedgerCodec.Validate(NativeIdentityLedgerCodec.Decode(payload,revision),revision);}}
}
namespace Deception.Tests {
 public static class LifecycleTests {
  static int Cases,Assertions;static void Check(bool ok,string why){Assertions++;if(!ok)throw new Exception(why);}
  static GameState Start(bool mimic=false){NativeDisguiseLifecycle.PreparePresentation=null;NativeRegroupSession.IsReady=true;NativeRegroupSession.Faults=0;NativeLedgerState.Fail=false;var s=new GameState();s.Map.Tile.unit=new();var a=new LedgerAppearance("giant",new("imperius","default"),new("imperius","default"),new("imperius","default"),5,380,400,false,ApparentConcealment.Visible,-1,Array.Empty<string>());var ledger=new NativeIdentityLedger(Guid.Parse("9b3d10e4-c517-4ad8-93a8-7efb938e4e2d"),3,1,new[]{new LedgerUnit(1,1,true,mimic?LedgerActualKind.Mimic:LedgerActualKind.Genuine,true,a,PaidInvestmentKind.Known,3)},Array.Empty<LedgerReceipt>());NativeGameStateEnvelope.Set(s,0,NativeIdentityLedgerCodec.Encode(ledger,0));return s;}
  static LedgerUnit Row(GameState s)=>NativeIdentityLedgerCodec.Decode(NativeGameStateEnvelope.Get(s)!.Bytes(),0).Units[0];
  static void Test(string name,Action action){try{action();Cases++;}catch(Exception e){throw new Exception(name,e);}}
  public static int Run(){try{
   foreach(var command in new Active[]{new AttackCommand(),new BoostCommand(),new ExplodeCommand(),new FreezeAreaCommand(),new HealOthersCommand(),new SwarmCommand()})Test(command.GetType().Name,()=>{var s=Start();var u=s.Map.Tile.unit!;NativeDisguiseLifecycle.BeforeActive(command,s,out bool entered);Check(entered&&!Row(s).Independent,"real command reveals");Check(u.health==40&&u.type=="warrior"&&u.moved&&!u.attacked,"reveal leaves true body and actions");Check(Row(s).PaidStars==3,"investment retained");});
   Test("invalid command preserves",()=>{var s=Start();var c=new AttackCommand{Valid=false};NativeDisguiseLifecycle.BeforeActive(c,s,out _);Check(Row(s).Independent,"invalid attempt cannot reveal");});
   Test("Mimic never gets a genuine reveal",()=>{var s=Start(true);NativeDisguiseLifecycle.BeforeActive(new AttackCommand(),s,out _);Check(Row(s).Independent,"Mimic identity survives");});
   foreach(int damage in new[]{0,1,40})Test("damage"+damage,()=>{var s=Start();NativeDisguiseLifecycle.BeforeDamage(s,new WorldCoordinates(),out var before);s.Map.Tile.unit!.health-=damage;NativeDisguiseLifecycle.AfterDamage(s,before);Check(Row(s).Independent==(damage==0),"positive actual damage only");Check(s.Map.Tile.unit.health==40-damage,"no counterfeit health write");});
   Test("heal has independent clamp",()=>{var s=Start();NativeDisguiseLifecycle.BeforeHeal(new HealAction(),s,out var before);s.Map.Tile.unit!.health=80;NativeDisguiseLifecycle.AfterHeal(new HealAction{HealAmount=40},s,before);Check(Row(s).Appearance!.Health==400&&Row(s).Independent,"apparent healing clamps separately");Check(s.Map.Tile.unit.health==80,"true healing is native");});
   Test("fixed healing independent of native wound cap",()=>{var s=Start();NativeDisguiseLifecycle.BeforeHeal(new HealAction{FixedHealAmount=5},s,out var before);NativeDisguiseLifecycle.AfterHeal(new HealAction{HealAmount=5},s,before);Check(Row(s).Appearance!.Health==385,"fixed native amount");});
   Test("actual cleansing and apparent healing are independent",()=>{var s=Start();NativeDisguiseLifecycle.BeforeHeal(new HealAction{FixedHealAmount=40},s,out var before);NativeDisguiseLifecycle.AfterHeal(new HealAction{HealAmount=0},s,before);Check(Row(s).Appearance!.Health==400,"apparent ordinary heal is independent of actual cleansing");});
   Test("apparent poison clears without normalizing HP",()=>{var s=Start();var snap=NativeGameStateEnvelope.Get(s)!;var ledger=NativeIdentityLedgerCodec.Decode(snap.Bytes(),0);ledger=ledger with{Units=new[]{ledger.Units[0] with{Appearance=ledger.Units[0].Appearance! with{Effects=new[]{"Charmed","Poisoned"}}}}};NativeGameStateEnvelope.Set(s,0,NativeIdentityLedgerCodec.Encode(ledger,0));NativeDisguiseLifecycle.BeforeHeal(new HealAction(),s,out var before);NativeDisguiseLifecycle.AfterHeal(new HealAction{HealAmount=40},s,before);Check(Row(s).Appearance!.Health==380&&Row(s).Appearance!.Effects.Length==0,"apparent cleansing remains separate");});
   Test("unowned context inert",()=>{var s=Start();NativeRegroupSession.IsReady=false;NativeDisguiseLifecycle.BeforeActive(new AttackCommand(),s,out var entered);Check(!entered&&Row(s).Independent,"no unowned state writes");});
   Test("failed validation publishes nothing",()=>{var s=Start();var bytes=NativeGameStateEnvelope.Get(s)!.Bytes();NativeLedgerState.Fail=true;try{NativeDisguiseLifecycle.BeforeActive(new AttackCommand(),s,out _);throw new Exception("expected failure");}catch(InvalidDataException){}Check(NativeGameStateEnvelope.Get(s)!.Bytes().SequenceEqual(bytes)&&NativeRegroupSession.Faults==1,"atomic private publication and fault");});
   foreach(bool fail in new[]{false,true})Test("real concealment list reveal and rollback "+fail,()=>{
    var s=Start();var u=s.Map.Tile.unit!;u.effects.Add(UnitEffect.Frozen);u.effects.Add(UnitEffect.Invisible);u.effects.Add(UnitEffect.Boosted);var beforeEffects=u.effects;
    var snapshot=NativeGameStateEnvelope.Get(s)!;var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),0);var row=ledger.Units[0];
    ledger=ledger with{Units=new[]{row with{Appearance=row.Appearance! with{Form="cloak",Concealment=ApparentConcealment.Hidden,Effects=new[]{"Invisible"}}}}};
    NativeGameStateEnvelope.Set(s,0,NativeIdentityLedgerCodec.Encode(ledger,0));var before=NativeGameStateEnvelope.Get(s)!.Bytes();NativeLedgerState.Fail=fail;
    if(fail){try{NativeDisguiseLifecycle.BeforeActive(new AttackCommand(),s,out _);throw new Exception("Expected reveal failure");}catch(InvalidDataException){}
     Check(ReferenceEquals(u.effects,beforeEffects)&&NativeGameStateEnvelope.Get(s)!.Bytes().SequenceEqual(before),"Reveal failure failed to restore exact native/private preimages");
    }else{NativeDisguiseLifecycle.BeforeActive(new AttackCommand(),s,out _);
     Check(!ReferenceEquals(u.effects,beforeEffects)&&Native.Items(u.effects).Cast<UnitEffect>().SequenceEqual(new[]{UnitEffect.Frozen,UnitEffect.Boosted})&&!Row(s).Independent,"Reveal did not remove only the disguise invisibility effect");
    }
    Check(Native.Items(beforeEffects).Cast<UnitEffect>().SequenceEqual(new[]{UnitEffect.Frozen,UnitEffect.Invisible,UnitEffect.Boosted}),"Reveal mutated an aliased preimage list");
    Check(u.health==40&&u.type=="warrior"&&u.moved&&!u.attacked,"Concealment reveal changed actual stats or action budget");
   });
   foreach(string eventKind in new[]{"active","zero-damage","positive-damage","damage-rollback"})Test("real Cloak reveal policy "+eventKind,()=>{
    var s=Start();var u=s.Map.Tile.unit!;u.type="cloak";u.effects.Add(UnitEffect.Invisible);u.effects.Add(UnitEffect.Frozen);var effects=u.effects;var bytes=NativeGameStateEnvelope.Get(s)!.Bytes();
    if(eventKind=="active")NativeDisguiseLifecycle.BeforeActive(new AttackCommand(),s,out _);
    else {NativeDisguiseLifecycle.BeforeDamage(s,new WorldCoordinates(),out var damage);if(eventKind!="zero-damage")u.health--;NativeLedgerState.Fail=eventKind=="damage-rollback";
      if(NativeLedgerState.Fail){try{NativeDisguiseLifecycle.AfterDamage(s,damage);throw new Exception("Expected damage reveal failure");}catch(InvalidDataException){}}
      else NativeDisguiseLifecycle.AfterDamage(s,damage);}
    bool preserved=eventKind!="positive-damage";Check(Native.Items(u.effects).Contains(UnitEffect.Invisible)==preserved,"only damage commitment forces genuine Cloak detected");
    Check(Row(s).Independent==(eventKind is "zero-damage" or "damage-rollback"),"mask follows committed reveal only");
    Check(Native.Items(u.effects).Contains(UnitEffect.Frozen)&&u.moved&&!u.attacked&&u.type=="cloak","reveal cannot reset native status or allowance");
    if(eventKind=="damage-rollback")Check(ReferenceEquals(effects,u.effects)&&NativeGameStateEnvelope.Get(s)!.Bytes().SequenceEqual(bytes),"failed positive-damage reveal restores exact effect and ledger preimages");
   });
   foreach(string cause in new[]{"active","damage","invalid","zero-damage","lethal","failed","repeat","mimic"})Test("successful-reveal-cue-boundary-"+cause,()=>{
    var s=Start(cause=="mimic");var u=s.Map.Tile.unit!;int captured=0,played=0;bool before=false,after=false;
    NativeDisguiseLifecycle.PreparePresentation=(state,unit)=>{captured++;before=Row(s).Independent;return ()=>{played++;after=!Row(s).Independent&&NativeGameStateEnvelope.Get(s)!.Revision==0;};};
    if(cause is "damage" or "zero-damage" or "lethal"){NativeDisguiseLifecycle.BeforeDamage(s,new WorldCoordinates(),out var d);u.health-=cause=="zero-damage"?0:cause=="lethal"?40:1;NativeDisguiseLifecycle.AfterDamage(s,d);}
    else{NativeLedgerState.Fail=cause=="failed";try{NativeDisguiseLifecycle.BeforeActive(new AttackCommand{Valid=cause!="invalid"},s,out _);}catch(InvalidDataException)when(cause=="failed"){}if(cause=="repeat")NativeDisguiseLifecycle.Reveal(s,u);}
    bool committed=cause is "active" or "damage" or "repeat";Check(played==(committed?1:0),"wrong successful reveal callback count");
    if(committed)Check(after&&NativeGameStateEnvelope.Get(s)!.Revision==0,"cue did not follow same-revision publication");
    if(cause is "invalid" or "zero-damage" or "lethal")Check(captured==0,"rejected/no-change/dead body captured audiovisual event");
    if(cause is "active" or "damage" or "failed")Check(before,"capture did not precede private reveal");
   });
   foreach(bool capture in new[]{false,true})Test("cosmetic-reveal-error-isolated-"+capture,()=>{
    var s=Start();NativeDisguiseLifecycle.PreparePresentation=(state,unit)=>capture?throw new Exception("capture"):()=>throw new Exception("playback");
    NativeDisguiseLifecycle.BeforeActive(new AttackCommand(),s,out _);Check(!Row(s).Independent&&NativeRegroupSession.Faults==0,"cosmetic failure faulted accepted real command");
   });
   Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,nativeGame="NOT RUN"}));return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}}
 }
}
