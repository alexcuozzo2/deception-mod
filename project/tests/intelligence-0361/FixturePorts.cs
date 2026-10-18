using System;
using System.Collections.Generic;
using System.Reflection;
using Deception.Core;
public class Identity
{
    static long Next=100;public IntPtr Pointer{get;}=new(++Next);
    public T Cast<T>() where T:class=>(T)(object)this;
}
namespace HarmonyLib
{
    public sealed class HarmonyMethod {public MethodInfo Method;public HarmonyMethod(MethodInfo method){Method=method;}}
    public sealed class Harmony {public readonly List<(MethodInfo Target,HarmonyMethod? Pre,HarmonyMethod? Post,HarmonyMethod? Final)> Hooks=new();public void Patch(MethodInfo t,HarmonyMethod? p,HarmonyMethod? q,object? transpiler,HarmonyMethod? f,object? il)=>Hooks.Add((t,p,q,f));}
}
namespace UnityEngine
{
    public class Object:Identity
    {
        public bool Destroyed;public static readonly List<Object> DestroyedObjects=new();
        public static implicit operator bool(Object? o)=>o is not null&&!o.Destroyed;
        public static void Destroy(Object o)
        {
            if(o.Destroyed)return;DestroyedObjects.Add(o);o.Destroyed=true;
            if(o is GameObject go){foreach(var child in go.transform.Children.ToArray())Destroy(child.gameObject);foreach(var c in go.Components)Destroy(c);Destroy(go.transform);}
        }
    }
    public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
    public readonly record struct Vector3(float x,float y,float z);
    public readonly record struct Quaternion(float x,float y,float z,float w);
    public readonly record struct Color(float r,float g,float b,float a);
    public sealed class Sprite:Object {public readonly Object Texture=new();}
    public sealed class Material:Object { }
    public class Renderer:Object
    {
        public int sortingLayerID{get;set;}public int sortingOrder{get;set;}
        public Material sharedMaterial{get;set;}=new();
    }
    public sealed class SpriteRenderer:Renderer {public Sprite? sprite{get;set;}public Color color{get;set;}public GameObject? gameObject{get;set;}}
    public sealed class Transform:Object
    {
        public GameObject gameObject{get;}public Transform(GameObject go){gameObject=go;}
        public readonly List<Transform> Children=new();public Transform? Parent;
        public Vector3 localPosition{get;set;}public Quaternion localRotation{get;set;}public Vector3 localScale{get;set;}=new(1,1,1);
        public Vector3 position=>localPosition;public Quaternion rotation=>localRotation;public Vector3 lossyScale=>localScale;
        public void SetParent(Transform p,bool worldPositionStays){if(GameObject.FailParent)throw new Exception("parent-failure");Parent?.Children.Remove(this);Parent=p;p.Children.Add(this);}
    }
    public sealed class GameObject:Object
    {
        public static readonly List<GameObject> All=new();public static bool FailComponent,FailParent;
        public bool activeInHierarchy=>!Destroyed&&Active&&(ParentActive());bool ParentActive()=>transform.Parent is null||transform.Parent.gameObject.activeInHierarchy;
        public string Name;public bool Active;public int layer{get;set;}public Transform transform{get;}
        public readonly List<Object> Components=new();public GameObject(string name){Name=name;transform=new(this);All.Add(this);}
        public void SetActive(bool active){if(Destroyed)throw new Exception("Destroyed object used");Active=active;}
        public Object AddComponent(Il2CppSystem.Type type){if(FailComponent)throw new Exception("component-failure");var result=(SpriteRenderer)Activator.CreateInstance(type.Managed)!;result.gameObject=this;Components.Add(result);return result;}
    }
    public enum TouchPhase{Began,Moved,Stationary,Ended,Canceled}
}
namespace Il2CppSystem {public sealed class Type {public System.Type Managed;public Type(System.Type t){Managed=t;}}}
namespace Il2CppInterop.Runtime {public static class Il2CppType {public static Il2CppSystem.Type From(System.Type t)=>new(t);}}
public sealed class PolytopiaSpriteRenderer:UnityEngine.Object
{
    public UnityEngine.Sprite? Sprite{get;set;}=new();public int SortingLayer{get;set;}=9;public int SortingOrder{get;set;}=17;
    public UnityEngine.SpriteRenderer? spriteRenderer{get;set;}public UnityEngine.GameObject gameObject{get;}=new("native-fog");
    public UnityEngine.Transform transform=>gameObject.transform;
}
namespace Polytopia.Data {public static class TechData {public enum Type{dcp_intelligence}}public static class ImprovementData {public enum Type{city}}}
namespace PolytopiaBackendBase.Common {public enum TribeType{Imperius,Bardur}public enum SkinType{Default}}
public static class EnumCache<T> where T:struct,Enum {public static T GetType(string name)=>Enum.Parse<T>(name);public static string GetName(T value)=>value.ToString();}
public readonly record struct WorldCoordinates(int X,int Y);
public sealed class PlayerState:Identity
{
    public byte Id{get;set;}public bool Intelligence=true;public int color{get;set;}=0x1256a0;
    public PolytopiaBackendBase.Common.TribeType tribe{get;set;}
    public PolytopiaBackendBase.Common.SkinType skinType{get;set;}
}
public static class PlayerExtensions {public static bool HasTech(PlayerState p,Polytopia.Data.TechData.Type t)=>p.Intelligence;}
public sealed class UnitState:Identity {public uint id{get;set;}=9;public byte owner{get;set;}=2;public WorldCoordinates coordinates{get;set;}=new(2,3);public int health{get;set;}=50;public bool Moved,Attacked;}
public sealed class Unit:UnityEngine.Object {public UnitState UnitState{get;set;}=new();public bool Visible=true,Invisible;public bool GetVisible()=>Visible;public bool IsInvisibleForLocalPlayer()=>Invisible;}
public sealed class TileData:Identity
{
    public WorldCoordinates coordinates{get;set;}public UnitState? unit{get;set;}public byte owner{get;set;}=2;public bool City;
    public int ExploredReads;public HashSet<byte> Explorers=new(){1};public bool GetExplored(byte player){ExploredReads++;return Explorers.Contains(player);}
    public bool HasImprovement(Polytopia.Data.ImprovementData.Type t)=>City;
}
public sealed class Tile:UnityEngine.Object
{
    public TileData Data{get;set;}=new();public Unit? unit{get;set;}public Unit? Unit=>unit;
    public bool IsHidden{get;set;}public PolytopiaSpriteRenderer fogOfWarRenderer{get;set;}=new();
}
public sealed class MapData:Identity {public List<TileData> Tiles{get;}=new();public Dictionary<(int,int),TileData> Index=new();}
public static class MapDataExtensions {public static TileData? GetTile(MapData m,WorldCoordinates c)=>m.Index.GetValueOrDefault((c.X,c.Y));}
public sealed class MapRenderer:UnityEngine.Object
{
    public static MapRenderer? Current{get;set;}public Dictionary<(int,int),Tile> Tiles=new();public Dictionary<uint,Unit> Units=new();
    public Tile? GetTileInstance(WorldCoordinates c)=>Tiles.GetValueOrDefault((c.X,c.Y));public Unit? GetUnitInstance(uint id)=>Units.GetValueOrDefault(id);
    public void OnDestroy(){ }
}
public sealed class GameLogicData {public int Color=0x7fa533;public int GetTribeColor(PolytopiaBackendBase.Common.TribeType t,PolytopiaBackendBase.Common.SkinType s)=>Color;}
public sealed class GameState:Identity
{
    public MapData Map{get;set;}=new();public GameLogicData GameLogicData{get;}=new();public uint CurrentTurn{get;set;}=5;
    public Dictionary<uint,UnitState> Units=new();public Dictionary<byte,PlayerState> Players=new();
    public bool TryGetUnit(uint id,out UnitState? u)=>Units.TryGetValue(id,out u);public bool TryGetPlayer(byte id,out PlayerState? p)=>Players.TryGetValue(id,out p);
}
public sealed class ActionManager {public bool IsProcessing{get;set;}}
public sealed class ClientBase:Identity
{
    public bool IsReplay{get;set;}public bool IsSpectating{get;set;}public bool IsReady{get;set;}=true;
    public GameState GameState{get;set;}=new();public ActionManager ActionManager{get;}=new();public bool IsWaitingForCommand{get;set;}
    public void SetNewLocalPlayerTurnForPassAndPlay(byte id){ }public void EndSession(){ }
}
public sealed class ReplayClient{ }
public static class GameManager
{
    public static ClientBase Client{get;set;}=new();public static GameState GameState{get;set;}=new();public static PlayerState LocalPlayer{get;set;}=new(){Id=1};
    public static bool Viewing=true;public static bool IsPlayerViewing(byte id)=>Viewing&&LocalPlayer.Id==id;
    public static void Update(){ }public static void ReturnToMenu(){ }public static void OnClientSessionOpened(){ }public static void SetReplayClient(ReplayClient r){ }
}
public static class PopupManager {public static bool PopupShowing{get;set;}public static int Opens;}
public sealed class ClientInteraction:Identity
{
    public readonly InteractionBar Bar=new();public InteractionBar GetInteractionBar(){Bar.unit=selectedUnit;Bar.tile=selectedTile;return Bar;}
    public Unit? selectedUnit{get;set;}public Tile? selectedTile{get;set;}public Tile? currentTile{get;set;}public bool wasDownOverUI{get;set;}public bool LongPress=true;
    public bool IsTouchValidLongpress(int index)=>LongPress;
    public void SelectUnit(Unit u){ }public void SelectTile(Tile t){ }public void ClearSelection(){ }public void DeselectUnit(){ }public void OnSelectionCleared(){ }public void OnTurnStarted(){ }public void OnDisable(){ }
    public void OnPress(Tile t){ }public void Process(){ }public void OnRelease(Tile t,int index){ }
}
public sealed class UIDuplicatedSprites{ }
public sealed class UISpriteDuplicator
{
    public sealed class SpriteDuplicationData { }
    public void RenderSprites(SpriteDuplicationData data){ }
    public void RenderSprites(UnityEngine.Transform t,UIDuplicatedSprites sprites,float scale,string name,UnityEngine.Vector2 offset,bool b){ }
}
public sealed class PolytopiaTouch
{
    public int fingerId{get;set;}public UnityEngine.TouchPhase phase{get;set;}public UnityEngine.Vector2 position{get;set;}public float maxSquareDistance{get;set;}
}
public static class PolytopiaInput
{
    public static List<PolytopiaTouch> CurrentTouches{get;}=new();public static UnityEngine.Vector2 mousePosition{get;set;}
    public static bool OutsideWindow{get;set;}public static bool MouseHeld,OverUi;
    public static bool GetMouseButton(int index)=>MouseHeld;public static bool IsPositionOverUIObject(UnityEngine.Vector2 p,int ignore)=>OverUi;
}
public static class CameraController{public static bool IsDragging{get;set;}public static bool IsBlocked{get;set;}}
namespace Deception.PolyModAdapter
{
    internal static class Entry {internal static readonly List<(string Kind,string Message)> Notes=new();internal static void Note(string k,string m)=>Notes.Add((k,m));}
    internal static class RecruitmentHooks {internal static bool Registered=true;internal const string TT="Polytopia.Data.TechData+Type",GLD="GameLogicData";}
    internal static class NativeLedgerOwnership
    {
        internal sealed class WeakIdentity {readonly WeakReference<object> Value;internal WeakIdentity(object o){Value=new(o);}internal object? Target()=>Value.TryGetTarget(out var o)?o:null;}
    }
    internal static class MimicRegroupUi {internal static bool Choosing;}
    internal static class MimicPresentationUi
    {
        internal static int Toggles;internal static void ReflowBar(object bar){}
        internal static bool CanInspect(object interaction,object unit)=>false;
        internal static bool TryInspectionContext(object i,object u,out InspectionContext context){context=default;return false;}
        internal static bool ToggleInspection(object i,object u){Toggles++;throw new Exception("Enemy Knowledge entered own-Mimic inspection path");}
    }
}
