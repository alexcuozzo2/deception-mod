using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.PolyModAdapter;
namespace HarmonyLib
{
 public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo m){}}
 public sealed class Harmony{public void Patch(MethodInfo target,HarmonyMethod? pre,HarmonyMethod? post,object? transpiler,HarmonyMethod? final,object? il){}}
}
namespace Il2CppSystem
{
 public class Object {static long Next=10;public IntPtr Pointer {get;}=new(++Next);public T Cast<T>() where T:class=>(T)(object)this;}
 public sealed class Type:Object {public System.Type Managed;public Type(System.Type t){Managed=t;}}
 public sealed class Action:Object {readonly System.Action Fn;public int Calls;public Action(System.Action fn){Fn=fn;}public void Invoke(){Calls++;Fn();}}
}
namespace Il2CppInterop.Runtime{public static class Il2CppType{public static Il2CppSystem.Type From(System.Type t)=>new(t);}}
namespace UnityEngine
{
 public class Object:Il2CppSystem.Object{public bool Destroyed;public static implicit operator bool(Object? o)=>o is not null&&!o.Destroyed;}
 public record struct Vector3(float x,float y,float z);
 public class GameObject:Object{public Puff? Puff;public Transform transform {get;}=new();public bool activeInHierarchy {get;private set;}=true;public void SetActive(bool b){activeInHierarchy=b;}public Component? GetComponent(Il2CppSystem.Type t)=>t.Managed==typeof(Puff)?Puff:null;}
 public class Transform:Object{public Vector3 position {get;set;}}
 public class Component:Object{public GameObject gameObject {get;set;}=new();public Transform transform=>gameObject.transform;}
}
namespace Deception.Core
{
 public enum NativeExchangeKind{Regroup,Disguise,UpgradeScout,UpgradeRammer,UpgradeBomber,ShedDisguise,Subvert,Blackout,FeignHeal,FeignBoost,FeignSwarm,FeignRecover,Blackmail,LighthouseSecret=13}
 public static class NativeExchangeCatalog
 {public static bool IsUpgrade(NativeExchangeKind kind)=>kind is NativeExchangeKind.UpgradeScout or NativeExchangeKind.UpgradeRammer or NativeExchangeKind.UpgradeBomber;}
 public record NativeExchangeUnit(int X,int Y);
 public record NativeExchangeIntent(NativeExchangeKind Kind,bool Swap,NativeExchangeUnit Initiator,NativeExchangeUnit Partner);
}
namespace Deception.PolyModAdapter
{
 internal static class NativeLighthouseSecretPresentation
 {
  internal static int Calls;internal static object? Action,Callback;
  internal static void Complete(object action,object callback){Calls++;Action=action;Callback=callback;((Il2CppSystem.Action)callback).Invoke();}
 }
 internal static class NativeExchangeTransformationVisual
 {
  internal static bool Admitted=true,Committed=true,Valid=true;
  internal static bool EndpointVisible=true,Disappearing,DepartureValid=true;
  internal sealed class Disappearance{}
  internal static System.Action? PrepareDisappearance(object state,Deception.Core.NativeExchangeIntent intent)=>null;
  internal static Disappearance? TakeDisappearance(object state,Deception.Core.NativeExchangeIntent intent){if(!Disappearing)return null;Disappearing=false;return new();}
  internal static bool CanShowDisappearance(Disappearance? pending,int x,int y)=>pending!=null&&DepartureValid&&x==1&&y==1;
  internal static void PruneDisappearances(){}
  internal static void ClearDisappearances(){Disappearing=false;}
  internal sealed class Lease:System.IDisposable{public bool Done;public void Dispose(){if(Done)return;Done=true;MapRenderer.Events.Add("restore");}}
  internal static bool Context(object state,object map,object client,object player,byte viewer)=>Admitted;
  internal static bool CanShowEndpoint(object state,object map,byte viewer,object at)=>EndpointVisible;
  internal static bool Authorized(object state,Deception.Core.NativeExchangeIntent intent)=>Committed;
  internal static Lease? Begin(object state,object map,object client,object player,Deception.Core.NativeExchangeIntent intent)
  {if(!EndpointVisible)return null;var at=intent.Kind==Deception.Core.NativeExchangeKind.Regroup?intent.Initiator:intent.Partner;MapRenderer.Events.Add("transform:"+at.X);return new();}
  internal static bool Current(Lease value)=>Valid&&!value.Done;
 }
 internal static class NativeExchangeCommit{internal static Func<object,Deception.Core.NativeExchangeIntent,System.Action?>? PreparePresentation;}
 internal static class NativeFeignSupport{internal static bool IsKind(Deception.Core.NativeExchangeKind kind)=>(int)kind>=8&&(int)kind<=11;}
 internal static class Entry{internal static readonly List<string> Notes=new();public static void Note(string kind,string message){Notes.Add(kind+":"+message);}}
 internal static class NativeExchangeCommands
 {
  public static bool IsExchange(object a)=>((BuildAction)a).Custom;
  public static Deception.Core.NativeExchangeIntent? Get(object a)=>((BuildAction)a).Intent;
  public static void ValidateBase(object a,Deception.Core.NativeExchangeIntent intent){if(((BuildAction)a).Invalid)throw new InvalidOperationException("bad actor");}
 }
}
public class BuildAction:Il2CppSystem.Object{public bool Custom=true,Invalid;public Deception.Core.NativeExchangeIntent? Intent=new(Deception.Core.NativeExchangeKind.Regroup,false,new(1,1),new(2,1));}
public class BuildReaction:Il2CppSystem.Object{public BuildAction action {get;set;}=new();public void Execute(Il2CppSystem.Action a)=>a.Invoke();public bool ShouldFocusCamera()=>true;}
public class WorldCoordinates:Il2CppSystem.Object{public int X {get;}public int Y {get;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
public class PlayerState:Il2CppSystem.Object{public byte Id {get;set;}=1;}
public class ClientBase:Il2CppSystem.Object{}
public class MapData:Il2CppSystem.Object{public Dictionary<(int,int),TileData> Tiles=new();}
public class GameState:Il2CppSystem.Object{public MapData Map {get;set;}=new();}
public class TileData:Il2CppSystem.Object{public bool Explored=true;public bool GetExplored(byte p)=>Explored;}
public class Tile:UnityEngine.Component{public TileData Data {get;set;}=new();public bool IsHidden {get;set;}}
public static class MapDataExtensions{public static TileData? GetTile(MapData m,WorldCoordinates c)=>m.Tiles.GetValueOrDefault((c.X,c.Y));}
public static class GameManager{public static ClientBase Client {get;set;}=new();public static GameState GameState {get;set;}=new();public static PlayerState LocalPlayer {get;set;}=new();}
public class MapRenderer:UnityEngine.Component
{
 public static MapRenderer? Current {get;set;}
 public Dictionary<(int,int),Tile> Tiles=new();public int Refreshes;public bool FailRefresh;
 public void Refresh(bool b){Refreshes++;Events.Add("refresh");if(FailRefresh)throw new InvalidOperationException("refresh");}
 public Tile? GetTileInstance(WorldCoordinates c)=>Tiles.GetValueOrDefault((c.X,c.Y));
 public void LateUpdate(){}public void OnDestroy(){}
 public static readonly List<string> Events=new();
}
public class Puff:UnityEngine.Component
{
 public bool IsUsed {get;set;}public Il2CppSystem.Object? puffSequence {get;set;}public int Returns;
 public void StartAnimation(UnityEngine.Transform t,UnityEngine.Vector3 local)
 {
  transform.position=t.position;StartAnimation();
 }
 public void StartAnimation()
 {
  MimicRegroupAnimation.PuffRestarting(this);
  IsUsed=true;puffSequence=new();MapRenderer.Events.Add("puff:"+transform.position.x);
  if(ObjectPool.FailStart)throw new InvalidOperationException("start");
 }
 public void ReturnToPool(){Returns++;if(ObjectPool.FailReturn)throw new InvalidOperationException("return");IsUsed=false;gameObject.SetActive(false);}
}
public static class ObjectPool
{
 public static readonly List<Puff> Puffs=new();public static bool FailStart,FailReturn,NoPuff;public static int ReturnedObjects;
 public static UnityEngine.GameObject? GetPooledObject(string id)
 {if(id!="Puff")throw new Exception("wrong key");if(NoPuff)return null;var p=new Puff();p.gameObject.Puff=p;Puffs.Add(p);return p.gameObject;}
 public static void ReturnObject(UnityEngine.GameObject go){ReturnedObjects++;go.SetActive(false);}
}
public enum SFXTypes {Transform=28}
namespace PolytopiaBackendBase.Common {public enum SkinType {Default=0}}
public class AudioManager:UnityEngine.Component
{
 public static AudioManager? instance {get;set;}=new();
 public static bool Enabled=true,FailSettings,FailPlay;
 public static System.Action? DuringSettings;
 public static readonly List<(SFXTypes Cue,PolytopiaBackendBase.Common.SkinType Skin,float Volume,float Pitch,float Pan)> Requests=new();
 public static bool ShouldPlaySoundEffects()
 {DuringSettings?.Invoke();if(FailSettings)throw new InvalidOperationException("settings");return Enabled;}
 public static void PlaySFX(SFXTypes cue,PolytopiaBackendBase.Common.SkinType skin,float volume,float pitch,float pan)
 {Requests.Add((cue,skin,volume,pitch,pan));MapRenderer.Events.Add("sound:"+(int)cue);if(FailPlay)throw new InvalidOperationException("play");}
}
public static class AnimationTests
{
 static int Assertions,Cases;static List<string> Results=new();
 static void Check(bool b,string why="animation assertion"){Assertions++;if(!b)throw new Exception(why);}
 static double Start()
 {
  var list=(System.Collections.IEnumerable)typeof(MimicRegroupAnimation).GetField("Jobs",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
  return list.Cast<object>().Select(j=>(double)j.GetType().GetField("Start",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(j)!).First();
 }
 static MapRenderer Reset()
 {
  NativeLighthouseSecretPresentation.Calls=0;NativeLighthouseSecretPresentation.Action=null;NativeLighthouseSecretPresentation.Callback=null;
  ObjectPool.FailReturn=false;MimicRegroupAnimation.Clear();ObjectPool.Puffs.Clear();ObjectPool.FailStart=false;ObjectPool.ReturnedObjects=0;ObjectPool.NoPuff=false;
  MimicRegroupAnimation.ShedReaction=null;MimicRegroupAnimation.ShedTick=null;MimicRegroupAnimation.ShedClear=null;
  AudioManager.instance=new();AudioManager.Enabled=true;AudioManager.FailSettings=false;AudioManager.FailPlay=false;AudioManager.DuringSettings=null;AudioManager.Requests.Clear();
  NativeExchangeTransformationVisual.Admitted=NativeExchangeTransformationVisual.Committed=NativeExchangeTransformationVisual.Valid=true;GameManager.Client=new();GameManager.GameState=new();GameManager.LocalPlayer=new();var map=new MapRenderer();MapRenderer.Current=map;MapRenderer.Events.Clear();
  NativeExchangeTransformationVisual.EndpointVisible=NativeExchangeTransformationVisual.DepartureValid=true;
  foreach(int x in new[]{1,2}){var t=new Tile();t.transform.position=new(x,1,0);map.Tiles[(x,1)]=t;GameManager.GameState.Map.Tiles[(x,1)]=t.Data;}
  return map;
 }
 static void Test(string name,System.Action work){Reset();work();MimicRegroupAnimation.Clear();Cases++;Results.Add(name);}
 public static int Run()
 {
  try
  {
   MimicRegroupAnimation.Install(new());
   Test("ordinary pass and custom no-camera",()=>{var r=new BuildReaction();r.action.Custom=false;var cb=new Il2CppSystem.Action(()=>{});bool focus=true;
    Check(MimicRegroupAnimation.BeforeExecute(r,cb)&&cb.Calls==0);Check(MimicRegroupAnimation.BeforeFocus(r,ref focus)&&focus);
    r.action.Custom=true;Check(!MimicRegroupAnimation.BeforeFocus(r,ref focus)&&!focus);});
   string? keep=null;
   foreach(bool swap in new[]{false,true})Test("identical full cue "+swap,()=>{var map=MapRenderer.Current!;var r=new BuildReaction();r.action.Intent=r.action.Intent! with{Swap=swap};var cb=new Il2CppSystem.Action(()=>{});
    Check(!MimicRegroupAnimation.BeforeExecute(r,cb));double start=Start();Check(ObjectPool.Puffs.Count==1&&map.Refreshes==0&&cb.Calls==0);
    Check(MapRenderer.Events.SequenceEqual(new[]{"puff:1","sound:28","transform:1"}),"only recipient transforms before any backend swapped refresh");
    MimicRegroupAnimation.TickAt(start+.23);Check(ObjectPool.Puffs.Count==3&&cb.Calls==0);
    MimicRegroupAnimation.TickAt(start+.56);MimicRegroupAnimation.TickAt(start+2);MimicRegroupAnimation.Clear();
    Check(cb.Calls==1&&MimicRegroupAnimation.ActiveCount==0&&ObjectPool.Puffs.All(p=>p.Returns==1));
    Check(AudioManager.Requests.Single()==(SFXTypes.Transform,PolytopiaBackendBase.Common.SkinType.Default,1f,1f,0f),"one identical neutral native cue");
    string events=string.Join("|",MapRenderer.Events);if(keep is null)keep=events;else Check(keep==events);});
   Test("hidden endpoint localized and not newly admitted",()=>{var m=MapRenderer.Current!;m.Tiles[(2,1)].IsHidden=true;var cb=new Il2CppSystem.Action(()=>{});
    MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);double s=Start();Check(ObjectPool.Puffs.Count==1);m.Tiles[(2,1)].IsHidden=false;
    MimicRegroupAnimation.TickAt(s+.23);Check(ObjectPool.Puffs.Count==2&&ObjectPool.Puffs.All(p=>p.transform.position.x==1));MimicRegroupAnimation.TickAt(s+.6);Check(cb.Calls==1);});
   Test("neither visible completes without cue",()=>{var m=MapRenderer.Current!;m.Tiles[(1,1)].IsHidden=true;m.Tiles[(2,1)].Data.Explored=false;var cb=new Il2CppSystem.Action(()=>{});
    MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);Check(cb.Calls==1&&ObjectPool.Puffs.Count==0&&m.Refreshes==1&&MimicRegroupAnimation.ActiveCount==0);Check(AudioManager.Requests.Count==0,"fully hidden reaction must be silent");});
   Test("witnessed disappearance gets original puff and one sound",()=>{NativeExchangeTransformationVisual.EndpointVisible=false;NativeExchangeTransformationVisual.Disappearing=true;var cb=new Il2CppSystem.Action(()=>{});
    MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);Check(MapRenderer.Events.SequenceEqual(new[]{"puff:1","sound:28"}),"known departure tile only; copied invisible art never drawn");double s=Start();
    MimicRegroupAnimation.TickAt(s+.23);Check(ObjectPool.Puffs.Count==1&&AudioManager.Requests.Count==1,"no hidden destination or second hidden burst");MimicRegroupAnimation.TickAt(s+.6);Check(cb.Calls==1&&ObjectPool.Puffs.Single().Returns==1,"same completion and lease retirement");});
   Test("unwitnessed hidden recipient remains silent",()=>{NativeExchangeTransformationVisual.EndpointVisible=false;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);Check(cb.Calls==1&&ObjectPool.Puffs.Count==0&&AudioManager.Requests.Count==0,"posthidden status alone cannot authorize departure");});
   Test("departure permission revoked retires puff",()=>{NativeExchangeTransformationVisual.EndpointVisible=false;NativeExchangeTransformationVisual.Disappearing=true;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);double s=Start();NativeExchangeTransformationVisual.DepartureValid=false;MimicRegroupAnimation.TickAt(s+.1);MimicRegroupAnimation.TickAt(s+.6);Check(ObjectPool.Puffs.Count==1&&ObjectPool.Puffs[0].Returns==1&&cb.Calls==1,"revoked departure cannot persist or repeat");});
   Test("witnessed disappearance honors mute",()=>{NativeExchangeTransformationVisual.EndpointVisible=false;NativeExchangeTransformationVisual.Disappearing=true;AudioManager.Enabled=false;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);Check(ObjectPool.Puffs.Count==1&&AudioManager.Requests.Count==0,"native mute applies to disappearance");});
   Test("disappearance grant consumed only once",()=>{NativeExchangeTransformationVisual.EndpointVisible=false;NativeExchangeTransformationVisual.Disappearing=true;var a=new Il2CppSystem.Action(()=>{});var b=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),a);MimicRegroupAnimation.BeforeExecute(new BuildReaction(),b);Check(ObjectPool.Puffs.Count==1&&AudioManager.Requests.Count==1&&b.Calls==1,"duplicate hidden reaction has no new cue");});
   Test("visibility loss retires puff and suppresses second",()=>{var m=MapRenderer.Current!;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);double s=Start();m.Tiles[(1,1)].IsHidden=true;
    MimicRegroupAnimation.TickAt(s+.1);Check(ObjectPool.Puffs[0].Returns==1);MimicRegroupAnimation.TickAt(s+.24);
    Check(ObjectPool.Puffs.Count==2&&ObjectPool.Puffs.Last().transform.position.x==2);MimicRegroupAnimation.Clear();Check(cb.Calls==1);});
   Test("reused native puff untouched",()=>{var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);var reused=ObjectPool.Puffs[0];reused.puffSequence=new();
    MimicRegroupAnimation.Clear();Check(reused.Returns==0&&reused.IsUsed&&reused.gameObject.activeInHierarchy&&cb.Calls==1);});
   Test("same sequence reuse retired before restart",()=>{var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);var reused=ObjectPool.Puffs[0];var sequence=reused.puffSequence;
    MimicRegroupAnimation.PuffRestarting(reused);reused.puffSequence=sequence;reused.IsUsed=true;MimicRegroupAnimation.Clear();
    Check(reused.Returns==0&&reused.gameObject.activeInHierarchy&&cb.Calls==1);});
   Test("start failure returns checkout and callback once",()=>{ObjectPool.FailStart=true;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);
    Check(cb.Calls==1&&ObjectPool.Puffs.Single().Returns==1&&MapRenderer.Current!.Refreshes==1&&MimicRegroupAnimation.ActiveCount==0);});
   Test("start and return failure hides partial checkout",()=>{ObjectPool.FailStart=true;ObjectPool.FailReturn=true;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);
    Check(cb.Calls==1&&ObjectPool.Puffs.Single().Returns==1&&!ObjectPool.Puffs.Single().gameObject.activeInHierarchy&&MimicRegroupAnimation.ActiveCount==0);ObjectPool.FailReturn=false;});
   Test("refresh failure cleanup and callback once",()=>{MapRenderer.Current!.FailRefresh=true;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);MimicRegroupAnimation.TickAt(Start()+.23);
    Check(cb.Calls==1&&MapRenderer.Current.Refreshes==1&&ObjectPool.Puffs.All(p=>p.Returns==1)&&MimicRegroupAnimation.ActiveCount==0);});
   Test("return failure hides own cue and completes all",()=>{var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);ObjectPool.FailReturn=true;
    MimicRegroupAnimation.Clear();Check(cb.Calls==1&&ObjectPool.Puffs.All(p=>p.Returns==1&&!p.gameObject.activeInHierarchy));ObjectPool.FailReturn=false;});
   Test("callback throw and reentry each invocation once",()=>{var second=new Il2CppSystem.Action(()=>throw new Exception("caller"));var first=new Il2CppSystem.Action(()=>{MimicRegroupAnimation.BeforeExecute(new BuildReaction(),second);throw new Exception("first");});
    MimicRegroupAnimation.BeforeExecute(new BuildReaction(),first);MimicRegroupAnimation.Clear();MimicRegroupAnimation.Clear();Check(first.Calls==1&&second.Calls==1&&MimicRegroupAnimation.ActiveCount==0);});
   Test("map destruction blocks reentrant refresh/cue",()=>{var map=MapRenderer.Current!;var second=new Il2CppSystem.Action(()=>{});var first=new Il2CppSystem.Action(()=>MimicRegroupAnimation.BeforeExecute(new BuildReaction(),second));
    MimicRegroupAnimation.BeforeExecute(new BuildReaction(),first);int count=ObjectPool.Puffs.Count;MimicRegroupAnimation.MapDestroying(map);
    Check(first.Calls==1&&second.Calls==1&&map.Refreshes==0&&ObjectPool.Puffs.Count==count&&MimicRegroupAnimation.ActiveCount==0);});
   Test("viewer change cancels without refreshing new world",()=>{var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);double s=Start();GameManager.LocalPlayer=new();
    MimicRegroupAnimation.TickAt(s+.1);Check(cb.Calls==1&&MapRenderer.Current!.Refreshes==0&&ObjectPool.Puffs.All(p=>p.Returns==1));});
   Test("missing intent completes without invented endpoint",()=>{var r=new BuildReaction();r.action.Intent=null;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(r,cb);
    Check(cb.Calls==1&&ObjectPool.Puffs.Count==0&&MapRenderer.Current!.Refreshes==1);});
   foreach(bool invalid in new[]{false,true})Test("invalid or unrelated exchange is silent "+invalid,()=>{var r=new BuildReaction();r.action.Invalid=invalid;
    if(!invalid)r.action.Intent=r.action.Intent! with{Kind=(Deception.Core.NativeExchangeKind)255};var cb=new Il2CppSystem.Action(()=>{});
    MimicRegroupAnimation.BeforeExecute(r,cb);Check(AudioManager.Requests.Count==0&&ObjectPool.Puffs.Count==0&&cb.Calls==1);});
   Test("ordinary reaction and cancellation without reaction are silent",()=>{var r=new BuildReaction();r.action.Custom=false;var cb=new Il2CppSystem.Action(()=>{});
    Check(MimicRegroupAnimation.BeforeExecute(r,cb));MimicRegroupAnimation.Clear();Check(AudioManager.Requests.Count==0&&cb.Calls==0);});
   Test("muted native settings suppress cue without late replay",()=>{AudioManager.Enabled=false;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);double s=Start();
    AudioManager.Enabled=true;MimicRegroupAnimation.TickAt(s+.23);MimicRegroupAnimation.TickAt(s+.6);Check(AudioManager.Requests.Count==0&&ObjectPool.Puffs.Count==3&&cb.Calls==1);});
   foreach(bool missing in new[]{false,true})Test("unavailable native manager is silent "+missing,()=>{if(missing)AudioManager.instance=null;else AudioManager.instance!.Destroyed=true;
    var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);MimicRegroupAnimation.TickAt(Start()+.6);
    Check(AudioManager.Requests.Count==0&&cb.Calls==1);});
   foreach(bool play in new[]{false,true})Test("native audio failure does not retry or fail completion "+play,()=>{AudioManager.FailPlay=play;AudioManager.FailSettings=!play;
    var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);double s=Start();MimicRegroupAnimation.TickAt(s+.23);MimicRegroupAnimation.TickAt(s+.6);MimicRegroupAnimation.Clear();
    Check(AudioManager.Requests.Count==(play?1:0)&&cb.Calls==1&&ObjectPool.Puffs.Count==3&&ObjectPool.Puffs.All(p=>p.Returns==1));});
   Test("world change during audio settings check remains silent",()=>{AudioManager.DuringSettings=()=>GameManager.LocalPlayer=new();var cb=new Il2CppSystem.Action(()=>{});
    MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);MimicRegroupAnimation.TickAt(Start()+.6);Check(AudioManager.Requests.Count==0&&cb.Calls==1);});
   Test("unavailable puff pool does not play cue",()=>{ObjectPool.NoPuff=true;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);
    MimicRegroupAnimation.TickAt(Start()+.6);Check(AudioManager.Requests.Count==0&&cb.Calls==1);});
   Test("failed first burst remains silent",()=>{ObjectPool.FailStart=true;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);
    Check(AudioManager.Requests.Count==0&&cb.Calls==1);});
   Test("disguise keep and swap share regroup cue and completion",()=>{
    foreach(bool swap in new[]{false,true}){
     Reset();var r=new BuildReaction();r.action.Intent=r.action.Intent! with{Kind=Deception.Core.NativeExchangeKind.Disguise,Swap=swap};var cb=new Il2CppSystem.Action(()=>{});
     Check(!MimicRegroupAnimation.BeforeExecute(r,cb)&&cb.Calls==0&&ObjectPool.Puffs.Count==1&&MapRenderer.Current!.Refreshes==0);
     Check(AudioManager.Requests.Single()==(SFXTypes.Transform,PolytopiaBackendBase.Common.SkinType.Default,1f,1f,0f));
     double start=Start();MimicRegroupAnimation.TickAt(start+.23);MimicRegroupAnimation.TickAt(start+.6);MimicRegroupAnimation.Clear();
     Check(cb.Calls==1&&MimicRegroupAnimation.ActiveCount==0&&ObjectPool.Puffs.Count==3&&ObjectPool.Puffs.All(p=>p.Returns==1));
    }
   });
   Test("Disguise transforms partner before the symmetric shuffle",()=>{
    var r=new BuildReaction();r.action.Intent=r.action.Intent! with{Kind=Deception.Core.NativeExchangeKind.Disguise,Swap=true};var cb=new Il2CppSystem.Action(()=>{});
    MimicRegroupAnimation.BeforeExecute(r,cb);Check(MapRenderer.Events.SequenceEqual(new[]{"puff:2","sound:28","transform:2"}),"Disguise recipient is partner, not initiating Mimic");
    MimicRegroupAnimation.TickAt(Start()+.23);Check(MapRenderer.Events.TakeLast(4).SequenceEqual(new[]{"puff:1","puff:2","restore","refresh"}),"both original positions puff before final swapped refresh");
   });
   foreach(bool admitted in new[]{false,true})Test("unenrolled or uncommitted reactions do not animate "+admitted,()=>{
    NativeExchangeTransformationVisual.Admitted=admitted;NativeExchangeTransformationVisual.Committed=false;var cb=new Il2CppSystem.Action(()=>{});
    MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);Check(ObjectPool.Puffs.Count==0&&AudioManager.Requests.Count==0&&cb.Calls==1,"no private receipt authority, no public cue");
   });
   Test("hidden recipient only has the later visible endpoint cue",()=>{
    var map=MapRenderer.Current!;map.Tiles[(1,1)].IsHidden=true;var cb=new Il2CppSystem.Action(()=>{});MimicRegroupAnimation.BeforeExecute(new BuildReaction(),cb);
    Check(ObjectPool.Puffs.Count==0&&AudioManager.Requests.Count==0&&map.Refreshes==0,"hidden recipient has no initial puff/sound");MimicRegroupAnimation.TickAt(Start()+.23);
    Check(ObjectPool.Puffs.Count==1&&ObjectPool.Puffs.Single().transform.position.x==2&&AudioManager.Requests.Count==1&&map.Refreshes==1,"only visible donor puffs during shuffle");
   });
   Test("single-unit upgrades bypass the two-unit regroup cue",()=>{
    foreach(var kind in new[]{Deception.Core.NativeExchangeKind.UpgradeScout,Deception.Core.NativeExchangeKind.UpgradeRammer,Deception.Core.NativeExchangeKind.UpgradeBomber,Deception.Core.NativeExchangeKind.Subvert,Deception.Core.NativeExchangeKind.Blackout,Deception.Core.NativeExchangeKind.FeignHeal,Deception.Core.NativeExchangeKind.FeignBoost,Deception.Core.NativeExchangeKind.FeignSwarm,Deception.Core.NativeExchangeKind.FeignRecover}){
     var reaction=new BuildReaction();reaction.action.Intent=reaction.action.Intent! with{Kind=kind};var callback=new Il2CppSystem.Action(()=>{});
     Check(MimicRegroupAnimation.BeforeExecute(reaction,callback)&&callback.Calls==0&&ObjectPool.Puffs.Count==0&&AudioManager.Requests.Count==0&&MapRenderer.Current!.Refreshes==0&&MimicRegroupAnimation.ActiveCount==0,"Upgrade was consumed or emitted a Regroup reaction");
    }
   });
   Test("Shed emits one-tile bursts and one transform cue",()=>{
    var reaction=new BuildReaction();var origin=reaction.action.Intent!.Initiator;reaction.action.Intent=reaction.action.Intent with{Kind=Deception.Core.NativeExchangeKind.ShedDisguise,Partner=origin};var callback=new Il2CppSystem.Action(()=>{});
    Check(!MimicRegroupAnimation.BeforeExecute(reaction,callback)&&callback.Calls==0&&ObjectPool.Puffs.Count==1,"Shed must deduplicate canonical self endpoints");
    Check(AudioManager.Requests.Count==1&&MapRenderer.Current!.Refreshes==1,"one initial transform cue and native refresh");double start=Start();
    MimicRegroupAnimation.TickAt(start+.23);Check(ObjectPool.Puffs.Count==2&&ObjectPool.Puffs.All(p=>p.transform.position.x==1),"both bursts stay at Shed's only tile");
    MimicRegroupAnimation.TickAt(start+.6);MimicRegroupAnimation.Clear();MimicRegroupAnimation.TickAt(start+2);
    Check(callback.Calls==1&&AudioManager.Requests.Count==1&&ObjectPool.Puffs.All(p=>p.Returns==1),"Shed completes and returns its particles once");
   });
   foreach(bool invalid in new[]{false,true})Test("hidden or invalid Shed is silent "+invalid,()=>{
    var reaction=new BuildReaction();var origin=reaction.action.Intent!.Initiator;reaction.action.Intent=reaction.action.Intent with{Kind=Deception.Core.NativeExchangeKind.ShedDisguise,Partner=origin};reaction.action.Invalid=invalid;
    if(!invalid)MapRenderer.Current!.Tiles[(1,1)].IsHidden=true;var callback=new Il2CppSystem.Action(()=>{});
    Check(!MimicRegroupAnimation.BeforeExecute(reaction,callback)&&callback.Calls==1&&ObjectPool.Puffs.Count==0&&AudioManager.Requests.Count==0,"invisible/invalid Shed must not emit a spatial cue");MimicRegroupAnimation.Clear();Check(callback.Calls==1,"silent Shed callback once");
   });
   Test("Shed failure cleans up its single endpoint",()=>{
    var reaction=new BuildReaction();var origin=reaction.action.Intent!.Initiator;reaction.action.Intent=reaction.action.Intent with{Kind=Deception.Core.NativeExchangeKind.ShedDisguise,Partner=origin};ObjectPool.FailStart=true;var callback=new Il2CppSystem.Action(()=>{});
    Check(!MimicRegroupAnimation.BeforeExecute(reaction,callback)&&callback.Calls==1&&ObjectPool.Puffs.Count==1&&ObjectPool.Puffs.Single().Returns==1&&AudioManager.Requests.Count==0,"Shed particle failure returns checkout and completes");MimicRegroupAnimation.Clear();Check(callback.Calls==1,"failed Shed must not complete twice");
   });
   Test("Lighthouse secret completes once with no camera animation sound or refresh",()=>{
    var map=MapRenderer.Current!;var reaction=new BuildReaction();reaction.action.Intent=new(Deception.Core.NativeExchangeKind.LighthouseSecret,false,new(400,400),new(400,400));
    var callback=new Il2CppSystem.Action(()=>{});bool focus=true;
    Check(!MimicRegroupAnimation.BeforeFocus(reaction,ref focus)&&!focus,"Lighthouse reward must not focus the camera");
    Check(!MimicRegroupAnimation.BeforeExecute(reaction,callback),"Lighthouse reward must not run native Build reaction");
    Check(NativeLighthouseSecretPresentation.Calls==1&&ReferenceEquals(NativeLighthouseSecretPresentation.Action,reaction.action)&&ReferenceEquals(NativeLighthouseSecretPresentation.Callback,callback)&&callback.Calls==1,"exact presentation/callback delegated once");
    Check(ObjectPool.Puffs.Count==0&&AudioManager.Requests.Count==0&&map.Refreshes==0&&MimicRegroupAnimation.ActiveCount==0&&MapRenderer.Events.Count==0,"silent reward allocated an animation job or emitted effects");
    MimicRegroupAnimation.TickAt(1000000);MimicRegroupAnimation.Clear();
    Check(callback.Calls==1&&NativeLighthouseSecretPresentation.Calls==1&&MimicRegroupAnimation.ActiveCount==0&&map.Refreshes==0,"later animation cleanup repeated completion or refresh");
   });
   Test("Blackmail city reaction delegates exclusively to its feedback adapter",()=>{
    var map=MapRenderer.Current!;var reaction=new BuildReaction();reaction.action.Intent=new(Deception.Core.NativeExchangeKind.Blackmail,false,new(400,400),new(400,400));
    var callback=new Il2CppSystem.Action(()=>{});bool focus=true;
    Check(!MimicRegroupAnimation.BeforeFocus(reaction,ref focus)&&!focus,"city command cannot focus a nonexistent unit endpoint");
    Check(MimicRegroupAnimation.BeforeExecute(reaction,callback)&&callback.Calls==0,"Blackmail delegates without stealing callback");
    Check(map.Refreshes==0&&ObjectPool.Puffs.Count==0&&AudioManager.Requests.Count==0&&MimicRegroupAnimation.ActiveCount==0,"refresh only, no unit transform or fruit animation");
    MimicRegroupAnimation.TickAt(1000000);MimicRegroupAnimation.Clear();Check(callback.Calls==0,"Blackmail callback remains unowned");
   });
   Test("Blackmail callback ownership stays with its feedback adapter",()=>{
    var reaction=new BuildReaction();reaction.action.Intent=new(Deception.Core.NativeExchangeKind.Blackmail,false,new(0,0),new(0,0));
    var callback=new Il2CppSystem.Action(()=>throw new Exception("city callback"));
    Check(MimicRegroupAnimation.BeforeExecute(reaction,callback)&&callback.Calls==0,"Regroup adapter never calls the city callback");
    MimicRegroupAnimation.Clear();Check(callback.Calls==0&&MimicRegroupAnimation.ActiveCount==0&&ObjectPool.Puffs.Count==0,"no leaked city reaction job");
   });
   Test("committed automatic Shed reuses one-tile poof and native sound",()=>{
    Check(MimicRegroupAnimation.StartShedCue(GameManager.GameState,2,1,()=>true),"automatic cue not accepted");double start=Start();
    Check(ObjectPool.Puffs.Count==1&&ObjectPool.Puffs[0].transform.position.x==2&&AudioManager.Requests.Count==1&&MapRenderer.Current!.Refreshes==1,"first poof and sound absent");
    MimicRegroupAnimation.TickAt(start+.23);MimicRegroupAnimation.TickAt(start+.6);
    Check(ObjectPool.Puffs.Count==2&&ObjectPool.Puffs.All(p=>p.transform.position.x==2&&p.Returns==1)&&AudioManager.Requests.Count==1&&MimicRegroupAnimation.ActiveCount==0,"automatic poof lifecycle diverged");
   });
   Test("automatic Shed authorization prevents stale and superseded cues",()=>{
    Check(!MimicRegroupAnimation.StartShedCue(GameManager.GameState,1,1,()=>false)&&ObjectPool.Puffs.Count==0&&AudioManager.Requests.Count==0,"stale initial cue played");bool live=true;
    Check(MimicRegroupAnimation.StartShedCue(GameManager.GameState,1,1,()=>live),"valid cue denied");double start=Start();live=false;MimicRegroupAnimation.TickAt(start+.23);
    Check(ObjectPool.Puffs.Count==1&&ObjectPool.Puffs[0].Returns==1&&AudioManager.Requests.Count==1&&MimicRegroupAnimation.ActiveCount==0,"superseded cue continued");
   });
   Test("queued Shed owns manual cue while native callback completes once",()=>{
    int requests=0;MimicRegroupAnimation.ShedReaction=(state,intent)=>{requests++;return true;};var reaction=new BuildReaction();reaction.action.Intent=reaction.action.Intent! with{Kind=Deception.Core.NativeExchangeKind.ShedDisguise};var cb=new Il2CppSystem.Action(()=>{});
    Check(!MimicRegroupAnimation.BeforeExecute(reaction,cb)&&requests==1&&cb.Calls==1,"manual Shed callback not completed");MimicRegroupAnimation.Clear();
    Check(cb.Calls==1&&ObjectPool.Puffs.Count==0&&AudioManager.Requests.Count==0&&MimicRegroupAnimation.ActiveCount==0,"manual fallback duplicated queued cue");
   });
   Test("Shed pending queue follows current map frame and teardown",()=>{
    int ticks=0,clears=0;MimicRegroupAnimation.ShedTick=()=>ticks++;MimicRegroupAnimation.ShedClear=()=>clears++;
    MimicRegroupAnimation.AfterFrame(new MapRenderer());MimicRegroupAnimation.AfterFrame(MapRenderer.Current!);Check(ticks==1,"foreign map drove pending queue");
    MimicRegroupAnimation.MapDestroying(MapRenderer.Current!);Check(clears==1,"map teardown retained pending Shed");
   });
   Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{cases=Cases,assertions=Assertions,passed=true,results=Results}));return 0;
  }
  catch(Exception e){Console.WriteLine(e);return 1;}
 }
}
