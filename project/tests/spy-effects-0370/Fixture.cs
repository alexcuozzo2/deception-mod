using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.PolyModAdapter;
using Deception.Core;
namespace HarmonyLib {public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo m){}}public sealed class Harmony{public void Patch(MethodInfo t,HarmonyMethod? a,HarmonyMethod? b,object? c,HarmonyMethod? d,object? e){}}}
namespace Il2CppSystem
{
 public class Object{static long next;public IntPtr Pointer{get;}=new(++next);}
 public class Type:Object{public System.Type Managed;public Type(System.Type t){Managed=t;}}
 public class Action:Object{readonly System.Action F;public int Calls;public Action(System.Action f){F=f;}public void Invoke(){Calls++;F();}}
}
namespace Il2CppInterop.Runtime{public static class Il2CppType{public static Il2CppSystem.Type From(System.Type t)=>new(t);}}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays {public sealed class Il2CppStructArray<T>:Il2CppSystem.Object{public T[] Values;public Il2CppStructArray(T[] values){Values=values;}}}
namespace UnityEngine
{
 public class Object:Il2CppSystem.Object{public bool Dead;public static implicit operator bool(Object? o)=>o is not null&&!o.Dead;public static void Destroy(Object o){o.Dead=true;}}
 public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public float this[int i]=>i==0?x:i==1?y:z;}
 public record struct Vector2(float x,float y);public record struct Color(float r,float g,float b,float a);public record struct Rect(float x,float y,float width,float height);public enum SpriteMeshType{FullRect}
 public class Transform:Object{public Vector3 position{get;set;}public Vector3 localScale{get;set;}=new(1,1,1);public object rotation{get;set;}=new object();public Vector3 TransformVector(Vector3 v)=>v;}
 public class GameObject:Object{public static readonly List<GameObject> All=new();public GameObject(string name=""){Name=name;All.Add(this);}public string Name;public Transform transform{get;}=new();public bool activeInHierarchy{get;set;}=true;public int layer{get;set;}public void SetActive(bool a){if(!a&&Name=="Deception smoke grenade"&&SpyEffectTests.FailDeactivate)throw new Exception("deactivate");activeInHierarchy=a;}public Component AddComponent(Il2CppSystem.Type t){if(SpyEffectTests.FailAllocation)throw new Exception("allocation");return new SpriteRenderer{gameObject=this};}}
 public class Component:Object{GameObject? go;public GameObject gameObject{get=>go??=new();set=>go=value;}public Transform transform=>gameObject.transform;}
 public class Texture2D:Object{}
 public class Sprite:Object{public Texture2D texture{get;set;}=new();public Rect rect{get;set;}=new(0,0,100,100);public float pixelsPerUnit{get;set;}=100;public static Sprite Create(Texture2D t,Rect r,Vector2 p,float ppu,uint e,SpriteMeshType m)=>new(){texture=t,rect=r,pixelsPerUnit=ppu};}
 public class SpriteRenderer:Component{public Sprite? sprite{get;set;}public Color color{get;set;}public int sortingLayerID{get;set;}public int sortingOrder{get;set;}}
 public class AudioClip:Object
 {
  public int Frames,Channels,Rate;public float[]? Data;public string? Name;
  public static bool FailSet,FailCreate,ThrowSet,NullConstruct,ZeroNative,WrongShape,KillOnSet;public static readonly List<AudioClip> Created=new();
  public IntPtr m_CachedPtr{get;set;}public int samples=>Frames;public int channels=>Channels;public int frequency=>Rate;
  public static AudioClip Create(string n,int f,int c,int r,bool s)=>throw new MissingMethodException("ReadOnlySpan.GetPinnableReference");
  public bool SetData(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float> a,int offset)=>throw new MissingMethodException("ReadOnlySpan.GetPinnableReference");
  public static AudioClip? Construct_Internal(){if(NullConstruct)return null;var c=new AudioClip();c.m_CachedPtr=ZeroNative?IntPtr.Zero:new IntPtr(c.Pointer.ToInt64()+100000);Created.Add(c);return c;}
  static AudioClip Resolve(IntPtr p)=>Created.Last(c=>c.m_CachedPtr==p&&p!=IntPtr.Zero&&c.Pointer!=p);
  public static void CreateUserSound_Injected(IntPtr p,ref Bindings.ManagedSpanWrapper name,int frames,int channels,int rate,bool stream)
  {var c=Resolve(p);c.Name=System.Runtime.InteropServices.Marshal.PtrToStringUni(name.begin,name.length);if(FailCreate)throw new Exception("create");if(stream)throw new Exception("unexpected streaming");c.Frames=frames;c.Channels=channels;c.Rate=rate;}
  public static bool SetData_Injected(IntPtr p,ref Bindings.ManagedSpanWrapper data,int offset)
  {var c=Resolve(p);if(offset!=0||data.length!=c.Frames*c.Channels)throw new Exception("wrong PCM shape");if(ThrowSet)throw new Exception("copy");c.Data=new float[data.length];System.Runtime.InteropServices.Marshal.Copy(data.begin,c.Data,0,data.length);if(WrongShape)c.Frames++;if(KillOnSet)c.Dead=true;return !FailSet;}
 }

 public class AudioSource:Component{public int Calls;public AudioClip? Last;public bool Fail;public float volume{get;set;}=.37f;public void PlayOneShot(AudioClip c,float v){Calls++;Last=c;if(Fail)throw new Exception("sound");}}
}
namespace PolytopiaBackendBase.Common{public enum SkinType{Default}}
public enum SFXTypes{Embassy=67,TreatyReject=69}
namespace UnityEngine.Bindings
{
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
 public struct ManagedSpanWrapper {[System.Runtime.InteropServices.FieldOffset(0)]public IntPtr begin;[System.Runtime.InteropServices.FieldOffset(8)]public int length;}
}
namespace UnityEngine.Networking
{
 public static class UnityWebRequestMultimedia {public static object GetAudioClip(string uri,int type)=>throw new MissingMethodException("ReadOnlySpan.GetPinnableReference");}
}
public class AudioManager:UnityEngine.Component
{
 public enum AudioSourceTypes{SFX=6}public static AudioManager? instance{get;set;}=new();public static UnityEngine.AudioSource Source=new();public static bool Enabled=true;public static int Embassy;public static SFXTypes LastCue;
 public static bool ShouldPlaySoundEffects()=>Enabled;public static UnityEngine.AudioSource GetAudioSource(AudioSourceTypes t)=>Source;
 public static void PlaySFX(SFXTypes t,PolytopiaBackendBase.Common.SkinType s,float v,float p,float b){Embassy++;LastCue=t;}
}
public class WorldCoordinates:Il2CppSystem.Object{public int X{get;}public int Y{get;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
public class TileData:Il2CppSystem.Object{public bool Explored=true;public UnitState? unit{get;set;}public bool GetExplored(byte p)=>Explored;}
public class UnitState:Il2CppSystem.Object{public uint id{get;set;}=5;}
public class Unit:UnityEngine.Component{public UnitState UnitState{get;set;}=new();public UnityEngine.Transform spriteContainer{get;set;}=new();public bool Visible=true,Invisible;public bool GetVisible()=>Visible;public bool IsInvisibleForLocalPlayer()=>Invisible;}
public class Fog:Il2CppSystem.Object{public int SortingOrder{get;set;}=500;}
public class Tile:UnityEngine.Component{public TileData Data{get;set;}=new();public Unit? unit{get;set;}public bool IsHidden{get;set;}public UnityEngine.Vector3 VisualCenter{get;set;}public UnityEngine.SpriteRenderer overlayRenderer{get;set;}=new();public Fog fogOfWarRenderer{get;set;}=new();public int Puffs;public void SpawnPuff(){Puffs++;if(SpyEffectTests.FailPuff)throw new Exception("puff");}}
public class MapData:Il2CppSystem.Object{public Dictionary<(int,int),TileData> Tiles=new();}
public class GameState:Il2CppSystem.Object{public MapData Map{get;set;}=new();}
public static class MapDataExtensions{public static TileData? GetTile(MapData m,WorldCoordinates p)=>m.Tiles.GetValueOrDefault((p.X,p.Y));}
public class PlayerState:Il2CppSystem.Object{public byte Id{get;set;}=1;}
public class ClientBase:Il2CppSystem.Object{public GameState GameState{get;set;}=null!;public bool IsReplay{get;set;}public bool IsSpectating{get;set;}}
public static class HotSeatOverlay{public static bool IsActive{get;set;}}
public static class GameManager{public static GameState GameState{get;set;}=new();public static ClientBase Client{get;set;}=new();public static PlayerState LocalPlayer{get;set;}=new();public static bool Viewing=true;public static bool IsPlayerViewing(byte id)=>Viewing;}
public class MapRenderer:UnityEngine.Component{public static MapRenderer? Current{get;set;}public static float TILE_WIDTH{get;set;}=2;public Dictionary<(int,int),Tile> Tiles=new();public int Refreshes;public bool FailRefresh;public System.Action? OnRefresh;public Tile? GetTileInstance(WorldCoordinates p)=>Tiles.GetValueOrDefault((p.X,p.Y));public void Refresh(bool b){Refreshes++;OnRefresh?.Invoke();if(FailRefresh)throw new Exception("refresh");}public void LateUpdate(){}public void OnDestroy(){}}
namespace Deception.Core
{
 public enum NativeExchangeKind{Blackout,Blackmail,Regroup}
 public record NativeExchangeUnit(uint NativeId,int X,int Y);
 public record NativeExchangeIntent(Guid Epoch,ulong Sequence,byte Actor,NativeExchangeKind Kind,NativeExchangeUnit Initiator,NativeExchangeUnit Partner);
 public static class NativeIdentityLedgerCodec{public static object Decode(byte[] b,long r)=>new();}
 public static class NativeExchangeIntentCodec{public static bool Committed=true;public static bool AlreadyCommitted(object l,long r,NativeExchangeIntent i)=>Committed;public static byte[] Fingerprint(NativeExchangeIntent i)=>System.Text.Encoding.UTF8.GetBytes(i.ToString());}
}
namespace Deception.PolyModAdapter
{
 internal static class NativeLedgerOwnership
 {
  internal sealed class WeakIdentity
  {
   readonly WeakReference<object> Weak;internal WeakIdentity(object value){Weak=new(value);}
   internal object? Target()=>Weak.TryGetTarget(out var value)?value:null;
   internal bool Matches(object value)=>Target() is object target&&Native.Same(target,value);
  }
 }
 public class NativeExchangePlan{public bool AlreadyCommitted;public object State=null!;public NativeExchangeIntent Intent=null!;}
 public static class NativeRegroupSession{public static bool Enabled=true;public static bool Ready(object s)=>Enabled;}
 public static class NativeGameStateEnvelope{public class Snapshot{public long Revision=>1;public byte[] Bytes()=>new byte[]{1};}public static Snapshot? Value=new();public static Snapshot? Get(object s)=>Value;}
 public static class NativeExchangeCommands{public static bool IsExchange(object a)=>((BuildAction)a).Custom;public static NativeExchangeIntent? Get(object a)=>((BuildAction)a).Intent;public static void ValidateBase(object a,NativeExchangeIntent i){if(((BuildAction)a).Invalid)throw new Exception("base");}}
 public static class Entry{public static List<string> Notes=new();public static void Note(string k,string m)=>Notes.Add(k+":"+m);}
 public static class UnitArtAssets{public static object DecodeLoadedFrame(string path,int w,int h,float x,float y,float rw,float rh)=>new UnityEngine.Sprite{rect=new(x,y,rw,rh)};}
 public static class MimicControls{public static object NativeSprite(string n)=>new UnityEngine.Sprite();}
}
public class BuildAction:Il2CppSystem.Object{public bool Custom=true,Invalid;public NativeExchangeIntent? Intent;}
public class BuildReaction:Il2CppSystem.Object{public BuildAction action{get;set;}=new();public void Execute(Il2CppSystem.Action c)=>c.Invoke();}
public static class SpyEffectTests
{
 public static bool FailAllocation,FailPuff,FailDeactivate;static int Cases,Assertions;static readonly List<string> Names=new();static double Time;
 static void Check(bool yes,string why){Assertions++;if(!yes)throw new Exception(why);}
 static void Reset()
 {
  NativeSpyEffectPresentation.Clear();Time=0;NativeSpyEffectPresentation.Clock=()=>Time;FailAllocation=FailPuff=FailDeactivate=false;NativeRegroupSession.Enabled=true;NativeExchangeIntentCodec.Committed=true;NativeGameStateEnvelope.Value=new();
  GameManager.GameState=new();GameManager.LocalPlayer=new();GameManager.Client=new(){GameState=GameManager.GameState};GameManager.Viewing=true;HotSeatOverlay.IsActive=false;
  var map=MapRenderer.Current=new();var a=new Tile{VisualCenter=new(0,0,0),unit=new()};a.Data.unit=a.unit.UnitState;var b=new Tile{VisualCenter=new(4,0,0)};map.Tiles[(0,0)]=a;map.Tiles[(2,0)]=b;GameManager.GameState.Map.Tiles[(0,0)]=a.Data;GameManager.GameState.Map.Tiles[(2,0)]=b.Data;
  AudioManager.instance=new();AudioManager.Enabled=true;AudioManager.Source=new();AudioManager.Embassy=0;UnityEngine.AudioClip.FailSet=UnityEngine.AudioClip.FailCreate=UnityEngine.AudioClip.ThrowSet=UnityEngine.AudioClip.NullConstruct=UnityEngine.AudioClip.ZeroNative=UnityEngine.AudioClip.WrongShape=UnityEngine.AudioClip.KillOnSet=false;UnityEngine.AudioClip.Created.Clear();Entry.Notes.Clear();UnityEngine.GameObject.All.Clear();
  ResetAudio();SmokeGrenadeAssets.PrepareSound();SmokeGrenadeAssets.PollSound();
 }
 static void ResetAudio()
 {
  var type=typeof(SmokeGrenadeAssets);type.GetField("Clip",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,null);type.GetField("AudioFailed",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,false);
 }
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]struct SequentialSpan{public IntPtr begin;public int length;}
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]struct WrongSpan{public IntPtr begin;public long length;}
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]struct WrongOrder{public int length;public IntPtr begin;}
 static void BadLayout(Type t){bool rejected=false;try{SmokeGrenadeAssets.RequireSpanLayout(t);}catch(NotSupportedException){rejected=true;}Check(rejected,"unexpected span layout accepted");}
 static BuildReaction Make(NativeExchangeKind kind=NativeExchangeKind.Blackout,byte[]? known=null)
 {
  var i=new NativeExchangeIntent(Guid.Parse("ec4a08c1-9429-408b-83aa-550eb1fda031"),1,1,kind,new(5,0,0),new(5,2,0));
  if(kind==NativeExchangeKind.Blackout)NativeSpyEffectPresentation.Record(new(){State=GameManager.GameState,Intent=i},known??new byte[]{1,2});
  return new(){action=new(){Intent=i}};
 }
 static Il2CppSystem.Action Start(BuildReaction r){var c=new Il2CppSystem.Action(()=>{});Check(!NativeSpyEffectPresentation.BeforeReaction(r,c),"custom effect must suppress native Build reaction");return c;}
 static Tile Target=>MapRenderer.Current!.Tiles[(2,0)];static void Tick(double t){Time=t;NativeSpyEffectPresentation.Tick();}
 static void Test(string name,System.Action f){Reset();f();Cases++;Names.Add(name);}
 static void RejectWave(byte[] b){bool threw=false;try{SmokeGrenadeAssets.DecodeWave(b);}catch(System.IO.InvalidDataException){threw=true;}Check(threw,"malformed WAV accepted");}
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static WeakReference<object> DiscardedObservation()
 {
  var state=new GameState();var intent=new NativeExchangeIntent(Guid.NewGuid(),1,1,NativeExchangeKind.Blackout,new(5,0,0),new(5,2,0));
  NativeSpyEffectPresentation.Record(new(){State=state,Intent=intent},new byte[]{1,2});return new(state);
 }
 public static int Run()
 {
  try
  {
   NativeSpyEffectPresentation.Install(new());
   Test("pending observer receipt does not retain discarded game state",()=>{
    var weak=DiscardedObservation();for(int i=0;i<3;i++){GC.Collect();GC.WaitForPendingFinalizers();}
    Check(!weak.TryGetTarget(out _),"pending observer roots dead simulation state");
    var r=Make();var waiting=(System.Collections.IDictionary)typeof(NativeSpyEffectPresentation).GetField("Waiting",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
    Check(waiting.Count==1,"dead observer receipts not pruned");Start(r);Tick(1);Check(AudioManager.Source.Calls==1,"live delayed observation lost");
   });
   Test("grenade launch is silent and impact owns one sound",()=>{var c=Start(Make());Check(NativeSpyEffectPresentation.ActiveCount==1&&c.Calls==0,"flight did not retain callback");Check(AudioManager.Source.Calls==0&&Target.Puffs==0,"launch played impact");Tick(.449);Check(AudioManager.Source.Calls==0,"sound before landing");Tick(.45);Check(AudioManager.Source.Calls==1&&Target.Puffs==1,"landing lacks cue");Tick(.46);Check(AudioManager.Source.Calls==1&&Target.Puffs==1,"impact repeated");Tick(.72);Check(c.Calls==1&&NativeSpyEffectPresentation.ActiveCount==0,"flight did not complete");Check(AudioManager.Source.Last!.Frames==24000&&AudioManager.Source.Last.Channels==2&&AudioManager.Source.Last.Rate==48000,"PCM clip shape differs");});
   Test("injected clip copy bypasses both broken span facades and remains cached",()=>{var first=UnityEngine.AudioClip.Created.Single();SmokeGrenadeAssets.PrepareSound();SmokeGrenadeAssets.PollSound();Start(Make());Tick(.45);Check(UnityEngine.AudioClip.Created.Count==1&&ReferenceEquals(AudioManager.Source.Last,first),"audio was recreated or absent");Check(first.Name=="Deception smoke grenade"&&first.Data!.Length==48000&&first.Data.Any(v=>v!=0)&&first.Data.All(v=>v>=-1&&v<1),"native PCM copy lost exact samples");});
   Test("audited span layout rejects wrong field type order and reference wrappers",()=>{SmokeGrenadeAssets.RequireSpanLayout(typeof(UnityEngine.Bindings.ManagedSpanWrapper));BadLayout(typeof(SequentialSpan));BadLayout(typeof(WrongSpan));BadLayout(typeof(WrongOrder));BadLayout(typeof(Il2CppSystem.Object));});
   Test("pinned buffers survive GC and are cleared after deterministic disposal",()=>{
    var chars=new SmokeGrenadeAssets.PinnedBuffer("smoke".ToCharArray());var floats=new SmokeGrenadeAssets.PinnedBuffer(new float[]{.5f,-.25f});
    GC.Collect();GC.WaitForPendingFinalizers();var name=(UnityEngine.Bindings.ManagedSpanWrapper)chars.Span;var samples=(UnityEngine.Bindings.ManagedSpanWrapper)floats.Span;
    Check(System.Runtime.InteropServices.Marshal.PtrToStringUni(name.begin,name.length)=="smoke","pinned text moved");var copy=new float[2];System.Runtime.InteropServices.Marshal.Copy(samples.begin,copy,0,2);Check(copy[0]==.5f&&copy[1]==-.25f,"pinned samples moved");
    chars.Dispose();floats.Dispose();chars.Dispose();Check(((UnityEngine.Bindings.ManagedSpanWrapper)chars.Span).begin==IntPtr.Zero&&((UnityEngine.Bindings.ManagedSpanWrapper)floats.Span).length==0,"disposed span retains dangling buffer");
   });
   Test("unsupported and empty audio pin buffers fail before native code",()=>{
    foreach(var a in new Array[]{Array.Empty<float>(),new int[]{1}}){bool rejected=false;try{using var b=new SmokeGrenadeAssets.PinnedBuffer(a);}catch(ArgumentException){rejected=true;}Check(rejected,"invalid buffer accepted");}
   });
   foreach(var mode in new[]{"create","copy","rejected","blank","missing pointer","shape","destroyed"})Test("partial audio failure cleans candidate and never retries: "+mode,()=>{
    ResetAudio();UnityEngine.AudioClip.Created.Clear();UnityEngine.AudioClip.FailCreate=mode=="create";UnityEngine.AudioClip.ThrowSet=mode=="copy";UnityEngine.AudioClip.FailSet=mode=="rejected";UnityEngine.AudioClip.NullConstruct=mode=="blank";UnityEngine.AudioClip.ZeroNative=mode=="missing pointer";UnityEngine.AudioClip.WrongShape=mode=="shape";UnityEngine.AudioClip.KillOnSet=mode=="destroyed";
    SmokeGrenadeAssets.PrepareSound();SmokeGrenadeAssets.PollSound();var count=UnityEngine.AudioClip.Created.Count;SmokeGrenadeAssets.PrepareSound();
    Check(UnityEngine.AudioClip.Created.Count==count&&UnityEngine.AudioClip.Created.All(c=>c.Dead),"failed candidate leaked or retried");var c=Start(Make());Tick(1);Check(c.Calls==1&&AudioManager.Source.Calls==0&&Target.Puffs==1,"audio failure stalled reaction or supplied blank clip");
   });
   Test("native SFX bus volume is preserved",()=>{Start(Make());Tick(.45);Check(AudioManager.Source.volume==.37f,"native volume mutated");});
   Test("mute suppresses impact audio only",()=>{AudioManager.Enabled=false;var c=Start(Make());Tick(1);Check(AudioManager.Source.Calls==0&&Target.Puffs==1&&c.Calls==1,"mute suppressed visual or callback");});
   Test("victim sees cue on formerly visible now fogged tile",()=>{GameManager.LocalPlayer.Id=2;var r=Make();Target.Data.Explored=false;Target.IsHidden=true;var c=Start(r);Tick(1);Check(AudioManager.Source.Calls==1&&Target.Puffs==1&&c.Calls==1,"pre-fog permission lost");});
   Test("unaware opponent receives no hidden target cue",()=>{GameManager.LocalPlayer.Id=3;var c=Start(Make());Tick(1);Check(AudioManager.Source.Calls==0&&Target.Puffs==0&&c.Calls==1,"unknown target disclosed");});
   Test("hidden Spy gives no projectile but known impact remains",()=>{GameManager.LocalPlayer.Id=2;MapRenderer.Current!.Tiles[(0,0)].unit!.Invisible=true;var c=Start(Make());Check(!UnityEngine.GameObject.All.Any(g=>g.Name=="Deception smoke grenade"&&!g.Dead),"hidden source traced");Tick(1);Check(AudioManager.Source.Calls==1&&c.Calls==1,"known impact lost");});
   Test("different source unit cannot throw projectile",()=>{MapRenderer.Current!.Tiles[(0,0)].unit!.UnitState.id=99;Start(Make());Check(!UnityEngine.GameObject.All.Any(g=>g.Name=="Deception smoke grenade"&&!g.Dead),"wrong unit animated");});
   Test("duplicate reaction cannot replay effect or callback",()=>{var r=Make();var first=Start(r);var second=Start(r);Tick(1);Check(first.Calls==1&&second.Calls==1&&AudioManager.Source.Calls==1,"duplicate playback");});
   Test("same effect can be presented to actor then affected victim",()=>{var r=Make();Start(r);Tick(1);GameManager.LocalPlayer=new(){Id=2};Start(r);Tick(2);Check(AudioManager.Source.Calls==2,"per-observer effect missing");});
   Test("uncommitted command has no audiovisual effect",()=>{var r=Make();NativeExchangeIntentCodec.Committed=false;var c=Start(r);Tick(1);Check(AudioManager.Source.Calls==0&&Target.Puffs==0&&c.Calls==1,"uncommitted cue");});
   Test("missing commit observation is silent",()=>{var r=Make();NativeSpyEffectPresentation.Clear();var c=Start(r);Tick(1);Check(AudioManager.Source.Calls==0&&Target.Puffs==0&&c.Calls==1,"missing permission guessed");});
   Test("changed payload cannot borrow recorded permission",()=>{var r=Make();r.action.Intent=r.action.Intent! with{Partner=new(5,99,99)};var c=Start(r);Check(c.Calls==1&&NativeSpyEffectPresentation.ActiveCount==0,"changed target admitted");});
   Test("viewer change cancels in-flight effect",()=>{var c=Start(Make());GameManager.LocalPlayer=new(){Id=2};Tick(1);Check(c.Calls==1&&AudioManager.Source.Calls==0&&Target.Puffs==0,"viewer change leaked impact");});
   Test("map replacement cancels in-flight effect",()=>{var c=Start(Make());MapRenderer.Current=new();Tick(1);Check(c.Calls==1&&AudioManager.Source.Calls==0,"old map effect");});
   Test("target pool rebind cancels in-flight effect",()=>{var c=Start(Make());Target.Data=new();Tick(1);Check(c.Calls==1&&AudioManager.Source.Calls==0,"pooled target borrowed");});
   Test("session clear completes once without impact",()=>{var c=Start(Make());NativeSpyEffectPresentation.Clear();NativeSpyEffectPresentation.Clear();Tick(1);Check(c.Calls==1&&AudioManager.Source.Calls==0&&NativeSpyEffectPresentation.ActiveCount==0,"clear leaked callback");});
   Test("map destruction retires projectile",()=>{var c=Start(Make());NativeSpyEffectPresentation.Destroying(MapRenderer.Current!);Tick(1);Check(c.Calls==1&&AudioManager.Source.Calls==0,"destroyed map cue");Check(UnityEngine.GameObject.All.Where(g=>g.Name=="Deception smoke grenade").All(g=>g.Dead),"projectile leaked");});
   Test("partial projectile allocation failure never stalls commands",()=>{FailAllocation=true;var c=Start(Make());Tick(1);Check(c.Calls==1&&NativeSpyEffectPresentation.ActiveCount==0,"allocation left job");Check(UnityEngine.GameObject.All.Where(g=>g.Name=="Deception smoke grenade").All(g=>g.Dead),"allocation leaked object");});
   Test("completion refresh reentry clears once and preserves native refresh",()=>{
    FailAllocation=true;MapRenderer.Current!.OnRefresh=NativeSpyEffectPresentation.Clear;var c=Start(Make());
    Check(c.Calls==1&&MapRenderer.Current.Refreshes==1&&NativeSpyEffectPresentation.ActiveCount==0,"Refresh reentry completed callback twice or suppressed native refresh");
    Check(UnityEngine.GameObject.All.Where(g=>g.Name=="Deception smoke grenade").All(g=>g.Dead),"Reentrant completion leaked projectile");
   });
   Test("deactivation failure still destroys projectile and completes",()=>{
    var c=Start(Make());FailDeactivate=true;Tick(1);
    Check(c.Calls==1&&NativeSpyEffectPresentation.ActiveCount==0,"Deactivation exception stalled completion");
    Check(UnityEngine.GameObject.All.Where(g=>g.Name=="Deception smoke grenade").All(g=>g.Dead),"Failed SetActive prevented owned-object destruction");
   });
   Test("completion callback reentry cannot repeat callback",()=>{
    var c=new Il2CppSystem.Action(NativeSpyEffectPresentation.Clear);NativeSpyEffectPresentation.BeforeReaction(Make(),c);Tick(1);NativeSpyEffectPresentation.Clear();
    Check(c.Calls==1&&NativeSpyEffectPresentation.ActiveCount==0,"Callback reentry repeated completion");
   });
   Test("particle failure still allows audio and completion",()=>{FailPuff=true;var c=Start(Make());Tick(1);Check(c.Calls==1&&AudioManager.Source.Calls==1,"puff failure stalled command");});
   Test("audio failure never retries or stalls",()=>{AudioManager.Source.Fail=true;var c=Start(Make());Tick(.45);Tick(1);Check(c.Calls==1&&AudioManager.Source.Calls==1,"sound failure repeated");});
   Test("native refresh failure completes only once",()=>{MapRenderer.Current!.FailRefresh=true;var c=Start(Make());Tick(1);Tick(2);Check(c.Calls==1&&MapRenderer.Current.Refreshes==1,"refresh failure repeated");});
   Test("hotseat curtain suppresses effect",()=>{HotSeatOverlay.IsActive=true;var c=Start(Make());Check(c.Calls==1&&NativeSpyEffectPresentation.ActiveCount==0,"curtain leaked effect");});
   Test("spectator and detached replay are excluded",()=>{GameManager.Client.IsReplay=true;var c=Start(Make());Check(c.Calls==1&&NativeSpyEffectPresentation.ActiveCount==0,"replay admitted");GameManager.Client.IsReplay=false;GameManager.Client.IsSpectating=true;c=Start(Make());Check(c.Calls==1&&NativeSpyEffectPresentation.ActiveCount==0,"spectator admitted");});
   Test("ordinary reaction remains native",()=>{var r=Make(NativeExchangeKind.Regroup);var c=new Il2CppSystem.Action(()=>{});Check(NativeSpyEffectPresentation.BeforeReaction(r,c)&&c.Calls==0,"ordinary callback intercepted");});
   Test("Blackmail plays peace-breaking cue with no unit projectile",()=>{var r=Make(NativeExchangeKind.Blackmail);var c=Start(r);Check(c.Calls==1&&AudioManager.Embassy==1&&AudioManager.LastCue==SFXTypes.TreatyReject&&AudioManager.Source.Calls==0&&NativeSpyEffectPresentation.ActiveCount==0,"Blackmail cue wrong");Check(MapRenderer.Current!.Refreshes==1,"Blackmail completion lost authorized map refresh");Start(r);Check(AudioManager.Embassy==1,"Blackmail cue repeated");});
   Test("hidden Blackmail city is silent",()=>{MapRenderer.Current!.Tiles[(0,0)].Data.Explored=false;var c=Start(Make(NativeExchangeKind.Blackmail));Check(c.Calls==1&&AudioManager.Embassy==0,"hidden city sound");});
   Test("projectile arc has exact endpoints and raised midpoint",()=>{var a=new NativeSpyEffectPresentation.Point(0,0,0);var b=new NativeSpyEffectPresentation.Point(4,0,0);var up=new NativeSpyEffectPresentation.Point(0,1,0);Check(NativeSpyEffectPresentation.Position(a,b,up,2,0)==a&&NativeSpyEffectPresentation.Position(a,b,up,2,1)==b,"arc endpoint drift");var mid=NativeSpyEffectPresentation.Position(a,b,up,2,.5);Check(mid.X==2&&mid.Y>0&&mid.Z==0,"arc plane wrong");});
   Test("smoke icon uses cropped original frame at native size",()=>{var icon=(UnityEngine.Sprite)SmokeGrenadeAssets.Icon();Check(icon.rect.width==2829&&icon.rect.height==2670&&Math.Abs(icon.pixelsPerUnit-2829)<.01,"menu image scale");});
   Test("malformed wave headers and formats fail closed",()=>{RejectWave(new byte[44]);using var f=typeof(SmokeGrenadeAssets).Assembly.GetManifestResourceStream("Deception.SpyEffects.ninja_smoke.wav")!;using var m=new System.IO.MemoryStream();f.CopyTo(m);var b=m.ToArray();Check(SmokeGrenadeAssets.DecodeWave(b).Samples.Length==48000,"wave samples");var bad=(byte[])b.Clone();bad[4]=0;RejectWave(bad);bad=(byte[])b.Clone();bad[0]=0;RejectWave(bad);RejectWave(b.Take(b.Length-1).ToArray());});
   Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{cases=Cases,assertions=Assertions,passed=true,results=Names}));return 0;
  }
  catch(Exception e){Console.WriteLine(e);Console.WriteLine(string.Join("\n",Entry.Notes));return 1;}
 }
}
