// Synthetic native boundary; the real adapters, reflection binder and pure
// model are compiled unchanged. These fixtures do not execute the game.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace HarmonyLib {public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo m){}}public sealed class Harmony{public List<string> Hooks=new();public void Patch(MethodInfo m,HarmonyMethod? p,HarmonyMethod? q,object? t,HarmonyMethod? f,object? x=null)=>Hooks.Add(m.DeclaringType!.Name+"."+m.Name);}}
namespace Il2CppSystem {public class Object{static long Next;public IntPtr Pointer{get;}=new(++Next);}}
public sealed class NList<T>:Il2CppSystem.Object
{readonly List<T> Values=new();public int Count=>Values.Count;public T this[int i]=>Values[i];public void Add(T value){Values.Add(value);}public bool Remove(T value)=>Values.Remove(value);public void Clear()=>Values.Clear();public NList(){}public NList(IEnumerable<T> values){Values.AddRange(values);}}
public static class EnumCache<T>where T:struct,Enum{public static string GetName(T v)=>v.ToString();public static T GetType(string s)=>Enum.Parse<T>(s);}
namespace PolytopiaBackendBase.Common {public enum TribeType{imperius,aquarion,polaris,cymanti}public enum SkinType{Default}}
namespace Polytopia.Data
{
 public sealed class TechData:Il2CppSystem.Object
 {public enum Type{basic,fishing,ramming,aquatism,sailing,navigation,aquafishing,aquaculture,marinelife,frostwork,icefishing,sledding,polarism,polarwarfare,dcp_intelligence,dcp_camouflage,dcp_deception,dcp_propaganda,dcp_espionage}public Type type{get;set;}public int cost{get;set;}public NList<TechData> techUnlocks{get;set;}=new();}
 public sealed class TribeData:Il2CppSystem.Object
 {public PolytopiaBackendBase.Common.TribeType type{get;set;}public int category{get;set;}=1;public Dictionary<TechData.Type,TechData> Overrides=new();}
 public sealed class GameLogicData:Il2CppSystem.Object
 {
  public Dictionary<TechData.Type,TechData> Tech=new();public NList<TribeData> Tribes=new();public int Scans;
  public TechData GetTechData(TechData.Type id)=>Tech[id];public NList<TribeData> GetAllTribes(){Scans++;return Tribes;}
  public TechData GetOverride(TechData tech,TribeData tribe)=>tribe.Overrides.GetValueOrDefault(tech.type)??tech;
  public NList<TechData> GetAllTechForTribe(TribeData tribe)
  {var result=new List<TechData>();var q=new Queue<TechData>();q.Enqueue(Tech[TechData.Type.basic]);while(q.Count>0){var d=GetOverride(q.Dequeue(),tribe);if(result.Contains(d))continue;result.Add(d);for(int i=0;i<d.techUnlocks.Count;i++)q.Enqueue(d.techUnlocks[i]);}return new(result);}
 }
}
public struct WorldCoordinates{public int X,Y;public WorldCoordinates(int x,int y){X=x;Y=y;}}
public sealed class PlayerState:Il2CppSystem.Object
{public byte Id{get;set;}public bool Alive=true;public int Currency{get;set;}=10;public PolytopiaBackendBase.Common.TribeType tribe{get;set;}public HashSet<Polytopia.Data.TechData.Type> Research=new();public HashSet<byte> Known=new();public int color{get;set;}=0xff0000;}
public static class PlayerExtensions
{public static bool IsAlive(PlayerState p,GameState s)=>p.Alive;public static bool HasTech(PlayerState p,Polytopia.Data.TechData.Type id)=>p.Research.Contains(id);public static bool KnowsPlayer(PlayerState p,byte id)=>p.Known.Contains(id);public static WorldCoordinates GetCurrentCapitalCoordinates(PlayerState p,GameState s)=>new(2,3);}
public sealed class MapData:Il2CppSystem.Object{}
public sealed class GameState:Il2CppSystem.Object
{public enum State{Started,Ended}public State CurrentState{get;set;}=State.Started;public Polytopia.Data.GameLogicData GameLogicData{get;set;}=new();public NList<PlayerState> PlayerStates{get;set;}=new();public MapData Map{get;set;}=new();public int CurrentCommand{get;set;}=4;public uint CurrentTurn{get;set;}=3;public byte CurrentPlayer{get;set;}=1;public NList<ActionBase> ActionStack{get;set;}=new();public bool TryGetPlayer(byte id,out PlayerState? p){p=null;for(int i=0;i<PlayerStates.Count;i++)if(PlayerStates[i].Id==id)p=PlayerStates[i];return p is not null;}}
public enum ActionType{Other,IncreaseCurrency,CreateResource=50}
public class ActionBase:Il2CppSystem.Object{public byte PlayerId{get;set;}public ActionType Kind{get;set;}public virtual ActionType GetActionType()=>Kind;}
public sealed class StartTurnAction:ActionBase{public void Execute(GameState state){state.ActionStack.Add(new IncreaseCurrencyAction(PlayerId,new(2,3),5,10));}}
public sealed class IncreaseCurrencyAction:ActionBase{public int Amount{get;set;}public int Delay{get;set;}public WorldCoordinates Source{get;set;}public IncreaseCurrencyAction(byte id,WorldCoordinates at,int delay):this(id,at,1,delay){}public IncreaseCurrencyAction(byte id,WorldCoordinates at,int amount,int delay){PlayerId=id;Source=at;Amount=amount;Delay=delay;}public void Execute(GameState state){state.TryGetPlayer(PlayerId,out var p);p!.Currency+=Amount;}}
public static class ResourceDataUtils{public static int CalculateIncomeFor(GameState state,byte player)=>5;}
namespace Deception.PolyModAdapter
{
 internal static class Entry{internal static List<string> Errors=new();internal static void Note(string what,string why)=>Errors.Add(what+":"+why);}
 internal static class RecruitmentHooks{internal const string TT="Polytopia.Data.TechData+Type",GLD="Polytopia.Data.GameLogicData";internal static bool Registered=true;}
 internal static class NativeRegroupSession{internal static bool Enabled=true;internal static HashSet<object> States=new();internal static bool Ready(object state)=>States.Contains(state);}
 internal static class BlackmailArtAssets{internal static int PlayerColor(object state,object player)=>((PlayerState)player).color;internal static object TeamColor(int packed)=>new UnityEngine.Color((packed>>16)/255f,((packed>>8)&255)/255f,(packed&255)/255f,1);}
}
namespace UnityEngine
{
 public class Object:Il2CppSystem.Object{public bool Dead;public static implicit operator bool(Object? o)=>o is not null&&!o.Dead;public static void Destroy(Object o){o.Dead=true;if(o is GameObject g){foreach(var c in g.Components)c.Dead=true;foreach(var child in g.transform.Children.ToArray())Destroy(child.gameObject);g.activeInHierarchy=false;}}}
 public class GameObject:Object{public bool activeInHierarchy{get;set;}=true;public RectTransform transform{get;}public List<Object> Components=new();public GameObject(){transform=new(this);Components.Add(transform);}public void SetActive(bool active)=>activeInHierarchy=active;}
 public class Component:Object{public GameObject gameObject{get;}=new();public RectTransform transform=>gameObject.transform;public RectTransform rectTransform=>transform;public Component(){gameObject.Components.Add(this);}}
 public class Transform:Object{public GameObject gameObject{get;}public Transform? parent{get;set;}public List<Transform> Children=new();public Vector3 localScale{get;set;}=new(1,1,1);public Quaternion localRotation{get;set;}=Quaternion.identity;public Transform(GameObject g){gameObject=g;}public void SetParent(Transform p,bool world){parent?.Children.Remove(this);parent=p;p.Children.Add(this);}}
 public sealed class RectTransform:Transform{public RectTransform(GameObject g):base(g){}public Vector2 anchorMin{get;set;}=new(0,0);public Vector2 anchorMax{get;set;}=new(1,1);public Vector2 pivot{get;set;}=new(.5f,.5f);public Vector2 sizeDelta{get;set;}=new(100,100);public Vector2 anchoredPosition{get;set;}=new(0,0);public Rect rect=>new(sizeDelta.x,sizeDelta.y);}
 public sealed class Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
 public sealed class Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}}
 public sealed class Quaternion{public static Quaternion identity=>new();}
 public sealed class Rect{public float width{get;}public float height{get;}public Rect(float w,float h){width=w;height=h;}}
 public sealed class Color{public float r,g,b,a;public static Color white=>new(1,1,1,1);public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}}
 public sealed class Sprite:Object{}
}
namespace UnityEngine.UI {public sealed class Image:UnityEngine.Component{public UnityEngine.Sprite? sprite{get;set;}public UnityEngine.Color color{get;set;}=UnityEngine.Color.white;public bool raycastTarget{get;set;}=true;public bool preserveAspect{get;set;}}}
namespace TMPro {public sealed class TextMeshProUGUI:UnityEngine.Component{public bool raycastTarget{get;set;}=true;}}
public sealed class Client:Il2CppSystem.Object{public GameState? GameState{get;set;}public bool IsReady{get;set;}=true;public bool IsReplay{get;set;}public bool IsSpectating{get;set;}public bool IsRecap{get;set;}}
public sealed class GameManager{public static GameState? GameState{get;set;}public static Client? Client{get;set;}public static PlayerState? LocalPlayer{get;set;}public static bool Viewing=true;public static bool IsPlayerViewing(byte p)=>Viewing&&p==LocalPlayer?.Id;public void Update(){}}
public sealed class MapRenderer:UnityEngine.Component{public static MapRenderer? Current{get;set;}}
public static class HotSeatOverlay{public static bool IsActive{get;set;}}
public static class ResourceEvents{public static List<byte> Notices=new();public static void IncomeChanged(byte p)=>Notices.Add(p);}
public sealed class TechView:UnityEngine.Component{public byte lastPlayer{get;set;}=1;public NList<TechItem> items{get;set;}=new();public void Show(bool animated){}public void RefreshTechItems(){}public void OnDisable(){}}
public sealed class TechItem:UnityEngine.Component{public Polytopia.Data.TechData? TechData{get;set;}public UnityEngine.UI.Image bg{get;set;}=new(){sprite=new()};public void SetData(Polytopia.Data.TechData data)=>TechData=data;}
public static class UIUtils{public static List<UnityEngine.UI.Image> Images=new();public static UnityEngine.UI.Image GetImage(){var i=new UnityEngine.UI.Image();Images.Add(i);return i;}}
public class UIBasicComponent:UnityEngine.Component{}
public sealed class ResourceWidget:UIBasicComponent{public float Amount{get;set;}public bool WarnIfLow{get;set;}=true;public bool AllowUpdateColors{get;set;}=true;public UnityEngine.UI.Image icon{get;set;}=new();public UnityEngine.UI.Image drop{get;set;}=new();public TMPro.TextMeshProUGUI label{get;set;}=new();}
public static class UILibrary{public static List<UIBasicComponent> Roots=new();public static List<ResourceWidget> Resources=new();public static UIBasicComponent NewEmptyComponent(UnityEngine.RectTransform parent){var c=new UIBasicComponent();c.rectTransform.SetParent(parent,false);Roots.Add(c);return c;}public static ResourceWidget NewResourceWidget(UnityEngine.RectTransform parent,int amount){var c=new ResourceWidget(){Amount=amount};c.rectTransform.SetParent(parent,false);Resources.Add(c);return c;}}
public sealed class SpriteAddress:Il2CppSystem.Object{}
public static class SpriteData{public static SpriteAddress[] GetHeadSpriteAddresses(GameState s,PlayerState p)=>new[]{new SpriteAddress()};}
public sealed class SpriteHandle:Il2CppSystem.Object{public static bool Delay;public static List<SpriteHandle> All=new();public bool isCompleted{get;set;}public UnityEngine.Sprite? sprite{get;set;}public SpriteHandle(){All.Add(this);}public void Request(SpriteAddress[] addresses){if(!Delay)Complete();}public void Complete(){isCompleted=true;sprite=new();}}
