// Synthetic native API surface for executing the REAL managed UI adapter in isolation.
// This is not Unity, Harmony detouring, IL2CPP marshalling or an in-game UI test.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
using Polytopia.Data;

namespace HarmonyLib
{
    public sealed class HarmonyMethod { public MethodInfo Method; public HarmonyMethod(MethodInfo m){Method=m;} }
    public sealed class Harmony
    {
        public readonly List<string> Patched=new();
        public void Patch(MethodInfo target,HarmonyMethod? pre,HarmonyMethod? post,object? transpiler,HarmonyMethod? final,object? il)
        { Patched.Add(target.DeclaringType!.FullName+"::"+target.Name); }
    }
}
namespace Il2CppSystem
{
    public sealed class Type:Object
    {public System.Type ManagedType {get;}public Type(System.Type type){ManagedType=type;}}
    public class Object
    {
        static long Next=100; public static bool AliasClone;
        public IntPtr Pointer { get; private set; }=new IntPtr(++Next);
        public T Cast<T>() where T:class => (T)(object)this;
        public new Object MemberwiseClone()
        {
            if(AliasClone)return this;
            var result=(Object)base.MemberwiseClone();result.Pointer=new IntPtr(++Next);return result;
        }
    }
    public sealed class Action:Object
    {
        public System.Action Callback { get; }
        public Action(System.Action a){Callback=a;}
        public void Invoke()=>Callback();
        public static implicit operator Action(System.Action a)=>new(a);
    }
}
namespace Il2CppInterop.Runtime
{public static class Il2CppType {public static Il2CppSystem.Type From(System.Type type)=>new(type);}}
namespace Il2CppSystem.IO
{
    // Real byte streams behind the synthetic wrapper surface. Command payload
    // tests must exercise ordering, truncation and trailing-record boundaries.
    public class Stream:Il2CppSystem.Object
    {
        internal readonly System.IO.MemoryStream Data;
        public Stream(){Data=new();}public Stream(byte[] bytes){Data=new(bytes);}
        public long Position {get=>Data.Position;set=>Data.Position=value;}
        public long Length=>Data.Length;public bool CanSeek=>Data.CanSeek;
    }
    public sealed class MemoryStream:Stream
    {public MemoryStream(){}public MemoryStream(byte[] bytes):base(bytes){}public byte[] ToArray()=>Data.ToArray();}
    public sealed class BinaryWriter:Il2CppSystem.Object
    {
        readonly System.IO.BinaryWriter Writer;public Stream BaseStream {get;}
        public BinaryWriter(Stream stream){BaseStream=stream;Writer=new(stream.Data,System.Text.Encoding.UTF8,true);}
        public void Write(byte value)=>Writer.Write(value);public void Write(uint value)=>Writer.Write(value);
        public void Write(int value)=>Writer.Write(value);public void Flush()=>Writer.Flush();
    }
    public sealed class BinaryReader:Il2CppSystem.Object
    {
        readonly System.IO.BinaryReader Reader;public Stream BaseStream {get;}
        public BinaryReader(Stream stream){BaseStream=stream;Reader=new(stream.Data,System.Text.Encoding.UTF8,true);}
        public byte ReadByte()=>Reader.ReadByte();public uint ReadUInt32()=>Reader.ReadUInt32();public int ReadInt32()=>Reader.ReadInt32();
    }
}
namespace Il2CppSystem.Collections.Generic
{
    public sealed class List<T>:Il2CppSystem.Object
    {
        readonly System.Collections.Generic.List<T> Data=new();
        public int Count=>Data.Count;
        public T this[int i] {get=>Data[i];set=>Data[i]=value;}
        public void Add(T item)=>Data.Add(item);
        public void Clear()=>Data.Clear();
        public bool Remove(T item)=>Data.Remove(item);
        public void RemoveAt(int index)=>Data.RemoveAt(index);
    }
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    public sealed class Il2CppStructArray<T>:Il2CppSystem.Object,ICollection<T> where T:struct
    {
        public T[] Data;public Il2CppStructArray(T[] values){Data=values;}
        public int Count=>Data.Length;public T this[int index] {get=>Data[index];set=>Data[index]=value;}
        public bool IsReadOnly=>false;public void CopyTo(T[] array,int index)=>Data.CopyTo(array,index);
        public IEnumerator<T> GetEnumerator()=>((IEnumerable<T>)Data).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>Data.GetEnumerator();
        public bool Contains(T item)=>Data.Contains(item);public void Add(T item)=>throw new NotSupportedException();
        public bool Remove(T item)=>throw new NotSupportedException();public void Clear()=>Array.Clear(Data);
    }
    public sealed class Il2CppReferenceArray<T>:Il2CppSystem.Object
    {
        readonly T[] Data;public int Length=>Data.Length;public int Count=>Data.Length;
        public Il2CppReferenceArray(long length){Data=new T[length];}
        public Il2CppReferenceArray(T[] items){Data=items;}
        public T this[int index] {get=>Data[index];set=>Data[index]=value;}
    }
}
namespace UnityEngine
{
    public class Camera:Component { }
    public enum RenderMode {ScreenSpaceOverlay,ScreenSpaceCamera,WorldSpace}
    public class Canvas:Component {public RenderMode renderMode {get;set;}public Camera? worldCamera {get;set;}}
    public static class Input {public static Vector2 mouseScrollDelta {get;set;}}
    public static class Time {public static int frameCount {get;set;}}
    public static class RectTransformUtility
    {
        public static Camera? LastCamera;
        public static bool RectangleContainsScreenPoint(RectTransform rect,Vector2 point,Camera? camera)
        {LastCamera=camera;var p=rect.InverseTransformPoint(new(point.x,point.y,0));return p.x>=rect.rect.x&&p.x<=rect.rect.x+rect.rect.width&&p.y>=rect.rect.y&&p.y<=rect.rect.y+rect.rect.height;}
    }
    public readonly record struct Vector2(float x,float y)
    {public static Vector2 zero=>new(0,0);public float this[int axis]=>axis switch{0=>x,1=>y,_=>throw new ArgumentOutOfRangeException(nameof(axis))};}
    public readonly record struct Vector3(float x,float y,float z)
    {public static Vector3 one=>new(1,1,1);public float this[int axis]=>axis switch{0=>x,1=>y,2=>z,_=>throw new ArgumentOutOfRangeException(nameof(axis))};}
    public readonly record struct Color(float r,float g,float b,float a)
    {public float this[int axis]=>axis switch{0=>r,1=>g,2=>b,3=>a,_=>throw new ArgumentOutOfRangeException(nameof(axis))};}
    public readonly record struct Quaternion(float x,float y,float z,float w) {public static Quaternion identity=>new(0,0,0,1);public static Quaternion Euler(float x,float y,float z)
        {float a=x*MathF.PI/360,b=y*MathF.PI/360,c=z*MathF.PI/360;return new(MathF.Sin(a)*MathF.Cos(b)*MathF.Cos(c)-MathF.Cos(a)*MathF.Sin(b)*MathF.Sin(c),MathF.Cos(a)*MathF.Sin(b)*MathF.Cos(c)+MathF.Sin(a)*MathF.Cos(b)*MathF.Sin(c),MathF.Cos(a)*MathF.Cos(b)*MathF.Sin(c)-MathF.Sin(a)*MathF.Sin(b)*MathF.Cos(c),MathF.Cos(a)*MathF.Cos(b)*MathF.Cos(c)+MathF.Sin(a)*MathF.Sin(b)*MathF.Sin(c));}}
    public readonly record struct Bounds(Vector3 center,Vector3 size)
    {
        public Vector3 min=>new(center.x-size.x/2,center.y-size.y/2,center.z-size.z/2);
        public Vector3 max=>new(center.x+size.x/2,center.y+size.y/2,center.z+size.z/2);
    }
    public readonly record struct Rect(float width,float height)
    {public float x {get;init;}=0;public float y {get;init;}=0;public Rect(float x,float y,float width,float height):this(width,height){this.x=x;this.y=y;}}
    public enum TouchPhase { Began,Moved,Stationary,Ended,Canceled }
    public class Object:Il2CppSystem.Object
    {
        public string name {get;set;}="";
        public static Object Instantiate(Object source)=>NativeVisualFixture.Instantiate(source);
        public static implicit operator bool(Object? value)=>value is not null&&!value.Destroyed&&(!(value is Component c)||!c.gameObject.Destroyed);
        public int DestroyCalls {get;private set;}public bool Destroyed=>DestroyCalls>0;
        public static void Destroy(Object value)
        {
            value.DestroyCalls++;
            if(value is GameObject go)
            {
                go.SetActive(false);
                // Native GameObject destruction owns its descendant objects,
                // but never the borrowed sprites/textures assigned to them.
                for(int i=0;i<go.transform.childCount;i++)Destroy(go.transform.GetChild(i).gameObject);
            }
        }
    }
    public enum TextureFormat {RGBA32}
    public enum FilterMode {Trilinear,Bilinear}
    public enum TextureWrapMode {Clamp}
    public class Texture:Object
    {public int width {get;set;}=1;public int height {get;set;}=1;public FilterMode filterMode {get;set;}public TextureWrapMode wrapMode {get;set;}}
    public sealed class Texture2D:Texture
    {
        public string SourceName="Mimic portrait";public bool MipChain,AppliedMips,MadeNonReadable;
        public static List<Texture2D> Allocations=new();
        public Texture2D(){}public Texture2D(int w,int h,TextureFormat format,bool mip){width=w;height=h;MipChain=mip;Allocations.Add(this);}
        public byte[] Raw=Array.Empty<byte>();public static List<Texture2D> Readbacks=new();
        public void ReadPixels(Rect area,int x,int y,bool mips)
        {
            Readbacks.Add(this);if(Graphics.Failure=="read")throw new InvalidOperationException("Injected readback failure");
            Raw=Enumerable.Repeat((byte)255,width*height*4).ToArray();
        }
        public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> GetRawTextureData()=>new(Raw);
        public void LoadRawTextureData(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> data)
        {if(Graphics.Failure=="load")throw new InvalidOperationException("Injected texture upload failure");Raw=data.Data.ToArray();}
        public void Apply(bool updateMipmaps,bool makeNoLongerReadable){AppliedMips=updateMipmaps;MadeNonReadable=makeNoLongerReadable;}
    }
    public enum RenderTextureFormat {ARGB32}public enum RenderTextureReadWrite {Linear}
    public class RenderTexture:Texture
    {
        public static RenderTexture? active {get;set;}public static List<RenderTexture> Allocated=new();public int Releases;
        public static RenderTexture GetTemporary(int w,int h,int d,RenderTextureFormat format,RenderTextureReadWrite rw)
        {var r=new RenderTexture{width=w,height=h};Allocated.Add(r);return r;}
        public static void ReleaseTemporary(RenderTexture value){value.Releases++;}
    }
    public static class Graphics
    {
        public static string Failure="";public static int Blits;
        public static void Blit(Texture source,RenderTexture target,Vector2 scale,Vector2 offset)
        {Blits++;RenderTexture.active=target;if(Failure=="blit")throw new InvalidOperationException("Injected blit failure");}
    }
    public static class ImageConversion
    {
        public static int Loads;public static bool Fail,WrongSize;public static Texture2D? LastTexture;
        public static bool LoadImage(Texture2D tex,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<byte> data,bool markNonReadable)
        {
            Loads++;LastTexture=tex;var b=data.Data;
            if(Fail||b.Length<24||b[0]!=137||b[1]!=80||b[2]!=78||b[3]!=71)return false;
            tex.width=WrongSize?1:System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16,4));
            tex.height=System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20,4));
            tex.SourceName="User PNG "+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b)).Substring(0,8);
            tex.MadeNonReadable=markNonReadable;return true;
        }
    }
    public enum SpriteMeshType { FullRect=0,Tight=1 }
    public sealed class Sprite:Object
    {
        public string Name {get;}public Rect rect {get;set;}=new(20,20);public Texture2D texture {get;set;}=new();public Sprite(string name){Name=name;}
        public Vector3 BoundsCenter;public float PixelsPerUnit=100;public Bounds? FixtureBounds;public Vector2? FixturePivot;public float pixelsPerUnit=>PixelsPerUnit;public Vector2 pivot=>FixturePivot??new(rect.width/2,rect.height/2);public Bounds bounds=>FixtureBounds??new(BoundsCenter,new(rect.width/PixelsPerUnit,rect.height/PixelsPerUnit,0));
        public Texture2D? associatedAlphaSplitTexture {get;set;}
        public Vector2[]? FixtureVertices;public ushort[]? FixtureTriangles;
        public int VertexReads,TriangleReads;
        public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector2> vertices {get{VertexReads++;return new(FixtureVertices??new[]{new Vector2(bounds.min.x,bounds.min.y),new Vector2(bounds.max.x,bounds.min.y),new Vector2(bounds.max.x,bounds.max.y),new Vector2(bounds.min.x,bounds.max.y)});}}
        public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector2> uv=>new(new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)});
        public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<ushort> triangles {get{TriangleReads++;return new(FixtureTriangles??new ushort[]{0,1,2,0,2,3});}}
        public SpriteMeshType MeshType=SpriteMeshType.Tight;
        public static Rect LastFrame;public static Vector2 LastPivot;
        public static readonly List<SpriteMeshType> CreateModes=new();
        public static Sprite Create(Texture2D texture,Rect rect,Vector2 pivot,float pixelsPerUnit)
            =>Create(texture,rect,pivot,pixelsPerUnit,0u,SpriteMeshType.Tight);
        public static Sprite Create(Texture2D texture,Rect rect,Vector2 pivot,float pixelsPerUnit,uint extrude,SpriteMeshType meshType)
        {LastFrame=rect;LastPivot=pivot;CreateModes.Add(meshType);return new(texture.SourceName){texture=texture,rect=rect,PixelsPerUnit=pixelsPerUnit,MeshType=meshType};}
    }
    public class GameObject:Object
    {
        Transform? Transform;readonly System.Collections.Generic.List<Component> Components=new();
        public int layer {get;set;}
        public GameObject():this(""){}public GameObject(string name){this.name=name;}
        public Transform transform=>Transform??=new Transform(this);
        internal void Bind(Transform value){Transform=value;}
        public bool activeSelf {get;private set;}=true;
        public bool activeInHierarchy=>activeSelf&&!Destroyed&&(transform.parent?.gameObject.activeInHierarchy??true);
        public void SetActive(bool value){activeSelf=value;}
        public static string? FailAddComponentForName;
        public Component AddComponent(Il2CppSystem.Type type)
        {if(name==FailAddComponentForName)throw new InvalidOperationException("Injected component creation failure");var result=(Component)Activator.CreateInstance(type.ManagedType)!;result.gameObject=this;if(result is Transform transform)Bind(transform);return result;}
        public Component? GetComponent(Il2CppSystem.Type type)=>Components.SingleOrDefault(c=>type.ManagedType.IsInstanceOfType(c));
        internal void Attach(Component value){if(!Components.Contains(value))Components.Add(value);}
        internal void Detach(Component value){Components.Remove(value);}
        public T? FindComponent<T>() where T:Component=>Components.OfType<T>().SingleOrDefault();
        public System.Collections.Generic.IEnumerable<T> ComponentsInChildren<T>() where T:Component
        {
            if(!activeInHierarchy)yield break;
            foreach(var component in Components.OfType<T>())if(component)yield return component;
            for(int i=0;i<transform.childCount;i++)foreach(var component in transform.GetChild(i).gameObject.ComponentsInChildren<T>())yield return component;
        }
    }
    public class Component:Object
    {
        GameObject? Owner;
        public GameObject gameObject {get=>Owner!;set{Owner?.Detach(this);Owner=value;Owner.Attach(this);}}
        public Transform transform=>gameObject.transform;
        public Component():this(true){}protected Component(bool create){if(create)gameObject=new();}
    }
    public class Transform:Component
    {
        readonly System.Collections.Generic.List<Transform> Children=new();
        public Transform(){gameObject.Bind(this);}internal Transform(GameObject owner):base(false){gameObject=owner;}
        public Vector3 localPosition {get;set;}public Quaternion localRotation {get;set;}=Quaternion.identity;
        public Vector3 position {get=>parent is null?localPosition:parent.TransformPoint(localPosition);set=>localPosition=parent is null?value:parent.InverseTransformPoint(value);}
        public Transform? parent {get;private set;}
        public Vector3? FixtureWorldScale;public Quaternion? FixtureWorldRotation;
        public Vector3 localScale {get;set;}=Vector3.one;public Vector3 lossyScale=>FixtureWorldScale??localScale;public Quaternion rotation=>FixtureWorldRotation??localRotation;
        public int childCount=>Children.Count;public Transform GetChild(int index)=>Children[index];
        public void Append(Transform child){child.SetParent(this,false);}
        public void SetParent(Transform? value,bool worldPositionStays)
        {var world=position;parent?.Children.Remove(this);parent=value;value?.Children.Add(this);if(worldPositionStays)position=world;}
        public int GetSiblingIndex()=>parent?.Children.IndexOf(this)??0;
        public void SetSiblingIndex(int index){if(parent is not null){parent.Children.Remove(this);parent.Children.Insert(Math.Clamp(index,0,parent.Children.Count),this);}}
        public void SetAsFirstSibling(){if(parent is not null){parent.Children.Remove(this);parent.Children.Insert(0,this);}}
        public void SetAsLastSibling(){if(parent is not null){parent.Children.Remove(this);parent.Children.Add(this);}}
        public bool IsChildOf(Transform ancestor)=>ReferenceEquals(this,ancestor)||(parent?.IsChildOf(ancestor)??false);
        static Vector3 Rotate(Quaternion q,Vector3 v)
        {float tx=2*(q.y*v.z-q.z*v.y),ty=2*(q.z*v.x-q.x*v.z),tz=2*(q.x*v.y-q.y*v.x);return new(v.x+q.w*tx+q.y*tz-q.z*ty,v.y+q.w*ty+q.z*tx-q.x*tz,v.z+q.w*tz+q.x*ty-q.y*tx);}
        public Vector3 TransformPoint(Vector3 value)
        {var r=Rotate(localRotation,new(value.x*localScale.x,value.y*localScale.y,value.z*localScale.z));var point=new Vector3(r.x+localPosition.x,r.y+localPosition.y,r.z+localPosition.z);return parent is null?point:parent.TransformPoint(point);}
        public Vector3 InverseTransformPoint(Vector3 value)
        {var p=parent is null?value:parent.InverseTransformPoint(value);var r=Rotate(new(-localRotation.x,-localRotation.y,-localRotation.z,localRotation.w),new(p.x-localPosition.x,p.y-localPosition.y,p.z-localPosition.z));return new(r.x/localScale.x,r.y/localScale.y,r.z/localScale.z);}
        public void ClearChildren(){foreach(var child in Children){child.parent=null;Object.Destroy(child.gameObject);}Children.Clear();}
    }
    public class Shader:Object {}
    public class Material:Object { public Shader shader {get;set;}=new(){name="Sprites/Default"}; public float OverlayStrength; }
    public class MaterialPropertyBlock:Il2CppSystem.Object
    {
        public Dictionary<string,object> Values=new();
        public void SetColor(string name,Color value)=>Values[name]=value;
        public void SetFloat(string name,float value)=>Values[name]=value;
        public Color GetColor(string name)=>Values.TryGetValue(name,out var v)?(Color)v:default;
        public float GetFloat(string name)=>Values.TryGetValue(name,out var v)?(float)v:0;
    }
    public class Renderer:Component
    {
        public Material? sharedMaterial {get;set;}=new();public MaterialPropertyBlock Properties=new();
        public void GetPropertyBlock(MaterialPropertyBlock block)=>block.Values=new(Properties.Values);
        public void SetPropertyBlock(MaterialPropertyBlock block)=>Properties.Values=new(block.Values);
public bool enabled {get;set;}=true;public virtual Bounds bounds {get;set;}=new(new(0,0,0),new(1,1,0));public int sortingLayerID {get;set;}public int sortingOrder {get;set;}}
    public sealed class MeshRenderer:Renderer {public string Geometry="native body";public float VertexAlpha=1;public bool Visible=>enabled&&gameObject.activeInHierarchy&&VertexAlpha>0;}
    public sealed class SpriteRenderer:Renderer
    {
        Bounds? LocalBounds;
        static Bounds Between(Vector3 low,Vector3 high)=>new(new((low.x+high.x)/2,(low.y+high.y)/2,(low.z+high.z)/2),new(Math.Abs(high.x-low.x),Math.Abs(high.y-low.y),Math.Abs(high.z-low.z)));
        // Native renderer bounds follow their transform. Store an explicit test
        // bound in local space so badge-offset drift cannot hide behind a fixed
        // world-space fixture record.
        public override Bounds bounds
        {
            get{var value=LocalBounds??sprite?.bounds??base.bounds;return Between(transform.TransformPoint(value.min),transform.TransformPoint(value.max));}
            set{LocalBounds=Between(transform.InverseTransformPoint(value.min),transform.InverseTransformPoint(value.max));}
        }
        Color Color=new(1,1,1,1);public bool RejectColorWrites;public int ColorWrites;
        public Color color {get=>Color;set{ColorWrites++;if(!RejectColorWrites)Color=value;}}
        public Sprite? sprite {get;set;}
    }
    public class RectTransform:Transform
    {
        public enum Axis {Horizontal,Vertical}
        public object? Owner;public Rect rect {get;set;}=new(100,80);
        public Vector2 sizeDelta {get=>new(rect.width,rect.height);set=>rect=new(rect.x,rect.y,value.x,value.y);}
        public Vector2 anchoredPosition {get;set;}
        public Vector2 anchorMin {get;set;}=new(.5f,.5f);public Vector2 anchorMax {get;set;}=new(.5f,.5f);
        public Vector2 offsetMin {get;set;}public Vector2 offsetMax {get;set;}
        public int SizeWrites;public bool CenteredSizeWrites;
        public void SetSizeWithCurrentAnchors(Axis axis,float size){SizeWrites++;float w=axis==Axis.Horizontal?size:rect.width,h=axis==Axis.Vertical?size:rect.height;rect=CenteredSizeWrites?new(-w/2,-h/2,w,h):new(w,h);}
    }
}
namespace UnityEngine.EventSystems
{
 public class AbstractEventData:Il2CppSystem.Object {public bool used {get;private set;}public void Use(){used=true;}}
 public class BaseEventData:AbstractEventData { }
 public class PointerEventData:BaseEventData {public UnityEngine.Vector2 position {get;set;}public UnityEngine.Vector2 scrollDelta {get;set;}}
}
namespace PolyMod.Managers
{
    public static class Visual
    {
        public static int Builds;public static byte[]? LastBytes;public static float LastPixelsPerUnit;
        public static UnityEngine.Sprite BuildSprite(byte[] data,UnityEngine.Vector2? pivot,float pixelsPerUnit)
        {
            Builds++;LastBytes=data;LastPixelsPerUnit=pixelsPerUnit;
            if(data.Length<24||data[0]!=137||data[1]!=80||data[2]!=78||data[3]!=71)throw new InvalidOperationException("Fixture PNG header invalid");
            int Read(int at)=>System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(at,4));
            string name=data.Length==263111?"Mimic portrait":"User PNG "+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).Substring(0,8);
            // Dimensions come from real resource IHDR bytes. Pixel decoding and
            // native texture creation remain outside this synthetic fixture.
            return new(name){rect=new(Read(16),Read(20)),texture=new(){SourceName=name}};
        }
    }
}
namespace UnityEngine.UI
{
    public class ScrollRect:UnityEngine.Component
    {
        public UnityEngine.RectTransform? content {get;set;}
        public UnityEngine.RectTransform? viewport {get;set;}
        public UnityEngine.RectTransform viewRect=>viewport??(UnityEngine.RectTransform)transform;
        public bool enabled {get;set;}=true;public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;
        public bool horizontal {get;set;}=true;public bool vertical {get;set;}
        public float scrollSensitivity {get;set;}=40;
        public float horizontalNormalizedPosition {get;set;}
        public float verticalNormalizedPosition {get;set;}=1;
        public int Stops;public void StopMovement(){Stops++;}
        public void LateUpdate(){}
    }
    public class Graphic:UnityEngine.Component
    {public UnityEngine.Color color {get;set;}=new(1,1,1,1); public UnityEngine.Material material {get;set;}=new();}
    public class Image:Graphic
    {
        public UnityEngine.Sprite? sprite {get;set;}
        UnityEngine.Sprite? Override;
        public static bool FailOverrideSprite;
        public UnityEngine.Sprite? overrideSprite {get=>activeSprite;set{Override=value;if(FailOverrideSprite)throw new InvalidOperationException("Injected head override failure");}}
        public UnityEngine.Sprite? activeSprite=>Override?Override:sprite;
        public UnityEngine.Texture2D? mainTexture=>activeSprite?.texture;
        bool PreserveAspect;public bool preserveAspect {get=>PreserveAspect;set{if(BranchTechFixture.Failure=="image-preserve")throw new InvalidOperationException("Synthetic image-preserve failure");PreserveAspect=value;}}
        public bool raycastTarget {get;set;}=true;
        public int NativeSizeCalls;
        public void SetNativeSize()
        {
            NativeSizeCalls++;
            if(BranchTechFixture.Failure=="image-native-size")throw new InvalidOperationException("Synthetic native-size failure");
            if(activeSprite is null)throw new InvalidOperationException("SetNativeSize called before sprite assignment");
            rectTransform.rect=new(activeSprite.rect.width*100f/activeSprite.pixelsPerUnit,activeSprite.rect.height*100f/activeSprite.pixelsPerUnit);
        }
        public UnityEngine.RectTransform rectTransform=>(UnityEngine.RectTransform)transform;
        public Image(){gameObject=new UnityEngine.RectTransform().gameObject;}
    }
}
namespace TMPro
{
    public class TMP_Text:UnityEngine.Component
    {
        public UnityEngine.Color color {get;set;}=new(1,1,1,1);
        public string text {get;set;}="";
        public UnityEngine.Vector2 GetPreferredValues()=>new(text.Length*9f,18f);
    }
    public sealed class TextMeshProUGUI:TMP_Text { }
    public sealed class TextMeshPro:TMP_Text { }
}
namespace PolytopiaBackendBase.Common { public enum SkinType { Default,TestSkin }public enum TribeType { Imperius,Bardur,Elyrion,XinXi,Aquarion,Cymanti } }
namespace Polytopia.Data
{
    public static class UnitAbility { public enum Type { dash,fortify } }
    public sealed class ImprovementData:Il2CppSystem.Object
    {
        public enum Type { dcp_exchange_regroup,dcp_exchange_disguise,city,harvestfruit,fishing,hunting,farm,enchantanimal,dcp_smuggle_fruit,dcp_smuggle_fish,dcp_smuggle_animal,dcp_smuggle_unknown }
        public Type type {get;set;}public uint Population {get;set;}=1;public int cost {get;set;}=2;
        public ImprovementData(){}public ImprovementData(Type value){type=value;}
    }
    public sealed class ResourceData:Il2CppSystem.Object
    {public enum Type { fruit,fish,game,starfish,ore }public Type type {get;set;}}
    public sealed class GameLogicData:Il2CppSystem.Object
    {
        public int GetTribeColor(PolytopiaBackendBase.Common.TribeType tribe,PolytopiaBackendBase.Common.SkinType skin)=>0x3366CC;
        public Dictionary<ImprovementData.Type,ImprovementData> Improvements=Enum.GetValues<ImprovementData.Type>().ToDictionary(x=>x,x=>new ImprovementData(x));
        public Dictionary<ResourceData.Type,ImprovementData> Harvests=new() {
            [ResourceData.Type.fruit]=new(ImprovementData.Type.harvestfruit),
            [ResourceData.Type.fish]=new(ImprovementData.Type.fishing),
            [ResourceData.Type.game]=new(ImprovementData.Type.hunting)
        };
        public ImprovementData? GetActionableImprovementForResource(ResourceData.Type resource,PlayerState player)=>
            Harvests.TryGetValue(resource,out var value)&&player.HarvestUnlocks.Contains(value.type)?value:null;
        // Native GetImprovementsFromTech skips null slots, while this legacy
        // predicate returns false at the first null slot in researched techs.
        public bool IsUnlocked(ImprovementData.Type improvement,PlayerState player)
        {foreach(var slot in player.HarvestUnlocks){if(slot is null)return false;if(slot==improvement)return true;}return false;}
        public ImprovementData GetImprovementData(ImprovementData.Type type)=>Improvements[type];
    }
    public sealed class UnitData:Il2CppSystem.Object
    {
        public enum Type { warrior,mermaidwarrior,rider,knight,polytaur,dcp_mimic,dcp_mimic_form_warrior,dcp_mimic_form_rider,dcp_mimic_form_polytaur,
            dragonegg,babydragon,firedragon,dcp_mimic_form_dragonegg,dcp_mimic_form_babydragon,dcp_mimic_form_firedragon,transportship,scoutship,rhammership,bombership,juggernaut,pirate,cloak_boat,
            dcp_mimic_form_transportship,dcp_mimic_form_scoutship,dcp_mimic_form_rhammership,dcp_mimic_form_bombership,dcp_mimic_form_juggernaut,dcp_mimic_form_pirate,dcp_mimic_form_cloak_boat,cloak,mermaidcloak,dcp_mimic_form_cloak,dcp_mimic_form_mermaidcloak,dcp_spy,dcp_mimic_form_dcp_spy,dcp_mermimic,dcp_spymaid,dcp_mimic_form_dcp_spymaid,dcp_mimic_form_dcp_mermimic }
        public Type type {get;set;}
        public int health {get;set;}
        public int attack {get;set;} public int defence {get;set;} public int range {get;set;}
        public int cost {get;set;} public int movement {get;set;}
        public string displayName=>type.ToString();
        public UnitData(Type t){type=t;bool mimic=t.ToString().StartsWith("dcp_mimic")||t==Type.dcp_mermimic;bool spy=t is Type.dcp_spy or Type.dcp_spymaid;health=mimic?10:spy?50:t==Type.polytaur?150:100;cost=mimic?Deception.Core.RecruitmentCatalog.Cost:2;attack=mimic?0:2;defence=mimic?0:spy?10:2;range=mimic?0:1;movement=1;}
    }
    public sealed class TechData:Il2CppSystem.Object
    {
        public enum Type { basic,dcp_deception,riding,dcp_propaganda,dcp_camouflage,dcp_intelligence,dcp_espionage }
        public Type type {get;set;}
        public int idx {get;set;}
        public int cost {get;set;}
        public Il2CppSystem.Collections.Generic.List<UnitData> unitUnlocks {get;set;}=new();
        public object techUnlocks {get;set;}=new object();
    }
}
public static class EnumCache<T> where T:struct,Enum
{
    public static string GetName(T value)=>value.ToString();
    public static T GetType(string value)
    {
        // The native content cache is a registry namespace, not a CLR enum
        // parser. In 0.3.53 the recorded "Elyrion" cosmetic member failed here.
        if(typeof(T)==typeof(PolytopiaBackendBase.Common.TribeType)||typeof(T)==typeof(PolytopiaBackendBase.Common.SkinType))
        {
            if(value!=value.ToLowerInvariant())throw new InvalidOperationException("Missing enum value for string "+value);
            return Enum.Parse<T>(value,true);
        }
        return Enum.Parse<T>(value);
    }
}
public sealed class WorldCoordinates:Il2CppSystem.Object
{
    public WorldCoordinates(){}public WorldCoordinates(int x,int y){X=x;Y=y;}
    public int X {get;set;} public int Y {get;set;}
}
public sealed class PlayerState:Il2CppSystem.Object
{
    public byte Id {get;set;}=1; public int Currency {get;set;}=15;
    public PolytopiaBackendBase.Common.SkinType skinType {get;set;}
    public PolytopiaBackendBase.Common.TribeType tribe {get;set;}
    public PolytopiaBackendBase.Common.TribeType tribeMix {get;set;}
    public int color {get;set;}=0xBBDD33;
    public PolytopiaBackendBase.Common.SkinType GetVisualSkin()=>skinType;
    public bool Deception=true,Propaganda,Espionage,Intelligence;
    // Ordered improvement slots from researched technologies, flattened only
    // for this fixture. Null entries remain visible to the legacy predicate.
    public List<ImprovementData.Type?> HarvestUnlocks=new(){ImprovementData.Type.harvestfruit,ImprovementData.Type.fishing,ImprovementData.Type.hunting};
    public HashSet<byte> PeaceWith=new();
}
public sealed class GameState:Il2CppSystem.Object
{
    public enum State{Started,Ended}public State CurrentState{get;set;}=State.Started;
    public ushort CurrentCommand {get;set;}
    public uint CurrentTurn {get;set;}=7; public byte CurrentPlayer {get;set;}=1;
    public Dictionary<uint,UnitState> Units=new();
    public bool TryGetUnit(uint id,out UnitState? unit)=>Units.TryGetValue(id,out unit);
    public Dictionary<byte,PlayerState> Players=new();
    public Il2CppSystem.Collections.Generic.List<PlayerState> PlayerStates {get{var result=new Il2CppSystem.Collections.Generic.List<PlayerState>();foreach(var p in Players.Values)result.Add(p);return result;}}
    public bool TryGetPlayer(byte id,out PlayerState? player)=>Players.TryGetValue(id,out player);
    public MapData Map {get;set;}=new();public GameLogicData GameLogicData {get;set;}=new();
    public Il2CppSystem.Collections.Generic.List<ActionBase> ActionStack {get;set;}=new();
    public bool BasicCommandValid=true;public int NativeBuildExecutions,NativeScaffolds;
}
public enum UnitEffect { Frozen=0,Poisoned=1,Boosted=2,Invisible=3,Bubble=4,Petrified=5,Swift=6,DoubleReady=7,Charmed=8 }
public sealed class UnitState:Il2CppSystem.Object
{
    public uint id {get;set;}=3;public byte owner {get;set;}=1;public ushort health {get;set;}=10;
    public UnitData.Type type {get;set;}=UnitData.Type.dcp_mimic_form_warrior;
    public UnitData UnitData {get;set;}=new(UnitData.Type.dcp_mimic_form_warrior);
    public bool moved {get;set;} public bool attacked {get;set;} public bool frozen {get;set;}
    public int promotionLevel {get;set;}public UnitState? passengerUnit {get;set;}
    public bool Dash;public bool CanAttack()=>(!moved||Dash)&&!attacked&&!frozen;
    public WorldCoordinates coordinates {get;set;}=new();public WorldCoordinates home {get;set;}=new();
    public Il2CppSystem.Collections.Generic.List<UnitEffect> effects {get;set;}=new();
    public bool Leader,Follower;public int ExhaustCalls;public bool ThrowExhaust;
    public bool HasEffect(UnitEffect value)=>value==UnitEffect.Frozen?frozen||Enumerable.Range(0,effects.Count).Any(i=>effects[i]==value):Enumerable.Range(0,effects.Count).Any(i=>effects[i]==value);
    public bool HasLeader()=>Leader;public bool HasFollower()=>Follower;
    public void MakeExhauseted(GameState state)
    {
        ExhaustCalls++;moved=attacked=true;
        effects.Remove(UnitEffect.Boosted);effects.Remove(UnitEffect.DoubleReady);
        if(ThrowExhaust)throw new InvalidOperationException("Synthetic native exhaustion failure");
    }
}
public sealed class TileData:Il2CppSystem.Object
{
    public bool IsWater {get;set;}public bool Wetland {get;set;}public bool IsWetland()=>Wetland;
    public WorldCoordinates coordinates {get;set;}=new();public UnitState? unit {get;set;}public ResourceData? resource {get;set;}
    public byte owner {get;set;}=byte.MaxValue;public ImprovementData.Type? Improvement;
    public bool Explored=true;public bool GetExplored(byte player)=>Explored;
    public int Population,ResourceRemovals,PopulationActions,DeferredPopulationActions;public byte? LastPopulationPlayer;
    public bool HasImprovement(ImprovementData.Type type)=>Improvement==type;
}
public sealed class MapData:Il2CppSystem.Object
{public Dictionary<(int,int),TileData> Tiles=new();public void Add(TileData tile)=>Tiles[(tile.coordinates.X,tile.coordinates.Y)]=tile;}
public static class MapDataExtensions
{public static int AreaReads;public static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<TileData> GetAreaSorted(MapData map,WorldCoordinates at,int radius,bool includeCenter,bool includeDiagonals){AreaReads++;return new(map.Tiles.Values.Where(t=>Math.Abs(t.coordinates.X-at.X)<=radius&&Math.Abs(t.coordinates.Y-at.Y)<=radius).ToArray());}public static TileData? GetTile(MapData map,WorldCoordinates coordinates)=>map.Tiles.TryGetValue((coordinates.X,coordinates.Y),out var tile)?tile:null;}
public static class ResourceDataUtils {public static uint GetPopulationReward(ImprovementData data)=>data.Population;}
public static class PlayerDiplomacyExtensions {public static bool HasPeaceWith(PlayerState player,byte other)=>player.PeaceWith.Contains(other);}
public static class UnitDataExtensions
{
    public static bool HasAbility(UnitState u,Polytopia.Data.UnitAbility.Type a)=>a==Polytopia.Data.UnitAbility.Type.dash&&u.Dash;
    public static int GetMaxHealth(UnitState unit,GameState state)=>unit.passengerUnit is null||unit.type.ToString() is "transportship" or "bombership" or "juggernaut" or "cloak_boat"?unit.UnitData.health+unit.promotionLevel*50:GetMaxHealth(unit.passengerUnit,state);
    public static bool CanCapture(UnitState unit,GameState game,TileData tile,bool checkTurn,bool checkSkills)=>false;
    public static bool CanExamineRuins(UnitState unit,GameState game,TileData tile)=>false;
    public static bool CanDisband(UnitState unit,GameState game,PlayerState player)=>false;
}
public sealed class GameManager:Il2CppSystem.Object
{
    public static GameManager instance {get;set;}=new();
    public object? fakeSession {get;set;} public bool isLevelLoaded {get;set;}=true;
    public void OnLevelLoaded(){}public void OnGameReady(){}
    public static GameState GameState {get;set;}=new(); public static PlayerState LocalPlayer {get;set;}=new();
    public static ClientBase Client {get;set;}=new();
    public static SpriteAtlasManager Atlas=new();public static SpriteAtlasManager GetSpriteAtlasManager()=>Atlas;
    public static bool IsPlayerViewing(byte player)=>LocalPlayer.Id==player;
    public void SetReplayClient(ReplayClient client){Client=client;}public static void ReturnToMenu(){}
    public void OnClientSessionOpened(){}
    public void Update() { }
}
public class CommandBase:Il2CppSystem.Object
{public int GetCommandType()=>17;
    public bool PassesBasicValidation(GameState state,out string reason)
    {reason=state.BasicCommandValid?"":"Native pending trigger";return state.BasicCommandValid;}
}
public sealed class BuildCommand:CommandBase
{
    public byte PlayerId {get;set;}public ImprovementData.Type Type {get;set;}public WorldCoordinates Coordinates {get;set;}=new();
    public BuildCommand(){}public BuildCommand(byte player,ImprovementData.Type type,WorldCoordinates coordinates){PlayerId=player;Type=type;Coordinates=coordinates;}
    public bool IsValid(GameState state,out string reason)=>PassesBasicValidation(state,out reason);
    // This is the independently modeled native command scaffold. The test
    // harness applies production prefixes/postfixes explicitly around it.
    public void Execute(GameState state)
    {state.NativeScaffolds++;state.ActionStack.Add(new CommandScaffoldAction());state.ActionStack.Add(new BuildAction(PlayerId,Type,Coordinates,true));}
    public void Serialize(Il2CppSystem.IO.BinaryWriter writer,int version)
    {writer.Write(PlayerId);writer.Write((int)Type);writer.Write(Coordinates.X);writer.Write(Coordinates.Y);}
    public void Deserialize(Il2CppSystem.IO.BinaryReader reader,int version)
    {PlayerId=reader.ReadByte();Type=(ImprovementData.Type)reader.ReadInt32();Coordinates=new(){X=reader.ReadInt32(),Y=reader.ReadInt32()};}
}
public class ActionBase:Il2CppSystem.Object
{
    public Il2CppSystem.Collections.Generic.List<ActionBase> subActions {get;set;}=new();
    public int CommitCalls;public int ThrowAfterAppend;
    public void CommitSubActionsToStack(Il2CppSystem.Collections.Generic.List<ActionBase> stack)
    {
        CommitCalls++;int appended=0;
        for(int i=subActions.Count-1;i>=0;i--)
        {stack.Add(subActions[i]);if(++appended==ThrowAfterAppend)throw new InvalidOperationException("Synthetic native subaction dispatch failure");}
    }
    public virtual void Execute(GameState state){}
}
public sealed class CommandScaffoldAction:ActionBase{}
public sealed class EndCommandAction:ActionBase {public byte PlayerId {get;set;}public int commandType {get;set;}=17;}
public sealed class RunDeferredAction:ActionBase {public byte PlayerId {get;set;}}
public sealed class BuildAction:ActionBase
{
    public byte PlayerId {get;set;}public ImprovementData.Type Type {get;set;}public WorldCoordinates Coordinates {get;set;}=new();public bool DeductCost {get;set;}
    public BuildAction(){}public BuildAction(byte player,ImprovementData.Type type,WorldCoordinates coordinates,bool deductCost){PlayerId=player;Type=type;Coordinates=coordinates;DeductCost=deductCost;}
    public override void Execute(GameState state)=>ExecuteDefault(state);
    public void ExecuteDefault(GameState state){state.NativeBuildExecutions++;}public void ExecuteV105(GameState state){state.NativeBuildExecutions++;}
    public void Serialize(Il2CppSystem.IO.BinaryWriter writer,int version)
    {writer.Write(PlayerId);writer.Write((int)Type);writer.Write(Coordinates.X);writer.Write(Coordinates.Y);writer.Write((byte)(DeductCost?1:0));}
    public void Deserialize(Il2CppSystem.IO.BinaryReader reader,int version)
    {PlayerId=reader.ReadByte();Type=(ImprovementData.Type)reader.ReadInt32();Coordinates=new(){X=reader.ReadInt32(),Y=reader.ReadInt32()};DeductCost=reader.ReadByte()!=0;}
}
public sealed class DestroyResourceAction:ActionBase
{
    public byte PlayerId {get;set;}public WorldCoordinates Coordinates {get;set;}
    public DestroyResourceAction(byte player,WorldCoordinates coordinates){PlayerId=player;Coordinates=coordinates;}
    public override void Execute(GameState state)
    {var tile=MapDataExtensions.GetTile(state.Map,Coordinates)!;if(tile.resource is null)throw new InvalidOperationException("Duplicate resource removal");tile.resource=null;tile.ResourceRemovals++;}
}
public sealed class IncreasePopulationAction:ActionBase
{
    public byte PlayerId {get;set;}public WorldCoordinates Source {get;set;}public WorldCoordinates Target {get;set;}public int Delay {get;set;}
    public IncreasePopulationAction(byte player,WorldCoordinates source,WorldCoordinates target,int delay){PlayerId=player;Source=source;Target=target;Delay=delay;}
    public override void Execute(GameState state)
    {
        var tile=MapDataExtensions.GetTile(state.Map,Target)!;tile.Population++;tile.PopulationActions++;tile.LastPopulationPlayer=PlayerId;
        if(tile.owner!=state.CurrentPlayer)tile.DeferredPopulationActions++;
    }
}
public sealed class IncreasePopulationReaction:Il2CppSystem.Object
{public IncreasePopulationAction action {get;set;}=new(1,new(),new(),60);public void Execute(Il2CppSystem.Action callback)=>callback.Invoke();}
public sealed class TrainCommand:CommandBase
{
    public byte PlayerId {get;set;} public UnitData.Type Type {get;set;}
    public WorldCoordinates Coordinates {get;set;}=new();
}
public sealed class Resource:Il2CppSystem.Object {public UnityEngine.Sprite? Sprite {get;set;}=new("Native fruit");}
public sealed class Tile:UnityEngine.Component
{
    public object? Improvement {get;set;}
    public PolytopiaSpriteRenderer fogOfWarRenderer {get;set;}=new();
    public Unit? unit {get=>Unit;set=>Unit=value;}public UnityEngine.Sprite attackOverlay {get;set;}=new("Native red bullseye");public UnityEngine.SpriteRenderer overlayRenderer {get;set;}=new();
    public WorldCoordinates Coordinates {get;set;}=new();public Unit? Unit {get;set;}public bool IsHidden {get;set;}
    public TileData? Data {get;set;}public Resource? Resource {get;set;}
}
public class WorldObject:UnityEngine.Component
{public PolytopiaSpriteRenderer? SpriteRenderer {get;set;}}
public sealed class Unit:WorldObject
{
    public UnitStatusDisplay? statusDisplay {get;set;}
    public UnitState UnitState {get;set;}=new();public PlayerState Owner=>GameManager.GameState.Players.TryGetValue(UnitState.owner,out var owner)?owner:GameManager.LocalPlayer.Id==UnitState.owner?GameManager.LocalPlayer:new(){Id=UnitState.owner};
    public UnityEngine.Transform spriteContainer {get;set;}=new();public SkinVisualsReference skinVisuals {get;set;}=new();
    public Unit(){spriteContainer.SetParent(transform,false);}
    public void UpdateHeadScale(bool value){NativeVisualFixture.HeadScaleCalls++;}
    public System.Action? DuringOverlayRefresh;public int OverlayRefreshes,OverlayClears;public void UpdateOverlays(){OverlayRefreshes++;DuringOverlayRefresh?.Invoke();}public void ClearTileOverlays(){OverlayClears++;}public void ClearUnitOverlays(){}
    public bool Bubble;public readonly Dictionary<UnitEffect,bool> EffectParticles=new();public void ShowBubble(bool value){Bubble=value;}public void UpdateUnitEffectParticles(UnitEffect effect,bool value){EffectParticles[effect]=value;}
    public bool Visible=true,Hidden=false,Interactable=true;
    public static bool PulseHookInstalled;
    public float NativePulseAlpha=.35f;
    public void Update()
    {
        // Pinned Unit.Update calls SkinVisualsRenderer.SetAlpha directly; it
        // never dispatches the Unit.SetAlpha method intercepted elsewhere.
        object? scope=null;if(PulseHookInstalled){object?[] args={this,null};typeof(Deception.PolyModAdapter.MimicMapInspection).GetMethod("BeforePulse")!.Invoke(null,args);scope=args[1];}
        Exception? error=null;
        try{if(UnitState.HasEffect(UnitEffect.Invisible))for(int i=0;i<skinVisuals.visualParts.Length;i++)
            if(skinVisuals.visualParts[i].renderer.spriteRenderer is UnityEngine.SpriteRenderer sprite&&sprite) sprite.color=sprite.color with{a=NativePulseAlpha};}
        catch(Exception e){error=e;throw;}
        finally{if(PulseHookInstalled)typeof(Deception.PolyModAdapter.MimicMapInspection).GetMethod("PulseFinished")!.Invoke(null,new object?[]{error,this,scope});}
    }
    public bool GetVisible()=>Visible&&gameObject.activeInHierarchy;public bool IsInvisibleForLocalPlayer()=>Hidden;
    public bool IsInteractableByPlayer(byte viewer)=>Interactable&&UnitState.owner==viewer;
    // Native hooks are modeled explicitly, not supplied by synthetic Harmony.
    // SetAlpha assigns (never multiplies) only main plain SpriteRenderer alpha.
    void NativeOperation(System.Action action)
    {
        if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.BeforeUnit(this);
        Exception? error=null;
        try{action();}catch(Exception e){error=e;throw;}
        finally{if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.UnitFinished(error,this);}
    }
    public void SetAlpha(float alpha)=>NativeOperation(()=> {
        for(int i=0;i<skinVisuals.visualParts.Length;i++)
        {var r=skinVisuals.visualParts[i].renderer.spriteRenderer;if(r is not null)r.color=r.color with{a=alpha};}
    });
    public System.Action<Unit>? UpdateVisuals;
    public void UpdateObject()=>NativeOperation(()=>UpdateObject(new(),new()));
    public void UpdateObject(MapRenderContext context,SkinVisualsTransientData transient)=>NativeOperation(()=>UpdateVisuals?.Invoke(this));
    public void UpdateStatusDisplay(MapRenderContext context)=>NativeOperation(()=> {if(statusDisplay is not null){if(!ReferenceEquals(statusDisplay.unit,this))statusDisplay.SetUnit(this);statusDisplay.SetState(UnitState);statusDisplay.RefreshColor();statusDisplay.Show();}});
    public void SetVisible(bool visible)=>NativeOperation(()=>SetVisible(visible,new()));
    public void SetVisible(bool visible,MapRenderContext context)=>NativeOperation(()=> {Visible=visible;gameObject.SetActive(visible);if(visible)SetAlpha(1);});
    public void SetShowOutline(bool value)=>NativeOperation(()=> {
        for(int i=0;i<skinVisuals.visualParts.Length;i++)
        {var outline=skinVisuals.visualParts[i].outlineRenderer;outline.spriteRenderer?.gameObject.SetActive(value);outline.polytopiaSpriteRenderer?.gameObject.SetActive(value);}
    });
    int NativeDepth;public int Depth {get=>NativeDepth;set=>NativeOperation(()=>NativeDepth=value);}
    public void Destroy(){if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.UnitDestroying(this);UnityEngine.Object.Destroy(gameObject);}
    public void OnDestroy(){if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.UnitDestroying(this);}
}
public sealed class MapRenderContext:Il2CppSystem.Object {public MapData mapData {get;set;}=new();public GameLogicData gld {get;set;}=new();public MapRenderer? unitRegistry {get;set;}public byte localPlayerId {get;set;}}
public sealed class SkinVisualsTransientData:Il2CppSystem.Object
{
    public PolytopiaBackendBase.Common.TribeType Tribe,Birth;public PolytopiaBackendBase.Common.SkinType Skin,BirthSkin;public int Color;
    public void SetupStatic(PolytopiaBackendBase.Common.TribeType tribe,PolytopiaBackendBase.Common.SkinType skin,int color,
        PolytopiaBackendBase.Common.TribeType birth,PolytopiaBackendBase.Common.SkinType birthSkin)
    {Tribe=tribe;Skin=skin;Color=color;Birth=birth;BirthSkin=birthSkin;}
}
public static class ChipMunkManager{public static bool active {get;set;}}
public static class SkinVisualsRenderer
{
    public static void SkinUnit(SkinVisualsReference reference,SkinVisualsTransientData data,bool immediate,int level)
    {
        NativeVisualFixture.SkinCalls++;NativeVisualFixture.LastProfile=data;
        if(NativeVisualFixture.FailSkin)throw new InvalidOperationException("Injected skin failure");
        for(int i=0;i<reference.visualParts.Length;i++)
            if(reference.visualParts[i].tintable&&reference.visualParts[i].renderer.spriteRenderer is {} renderer)renderer.color=ColorUtil.ColorFromInt(data.Color);
    }
}
public static class NativeVisualFixture
{
    public static int CloneCalls,SkinCalls,HeadScaleCalls;public static bool Alias,FailSkin;
    public static SkinVisualsTransientData? LastProfile;public static readonly List<Unit> Clones=new();
    public static UnityEngine.Object Instantiate(UnityEngine.Object source)
    {
        CloneCalls++;if(Alias)return source;
        var original=(Unit)source;var result=new Unit();Clones.Add(result);
        result.spriteContainer.localPosition=original.spriteContainer.localPosition;result.spriteContainer.localScale=original.spriteContainer.localScale;
        var src=original.gameObject.FindComponent<SkinVisualsReference>()!;
        var target=(SkinVisualsReference)result.gameObject.AddComponent(new Il2CppSystem.Type(typeof(SkinVisualsReference)));
        UnityEngine.SpriteRenderer? Copy(UnityEngine.SpriteRenderer? r)
        {
            if(r is null)return null;
            var copy=new UnityEngine.SpriteRenderer{sprite=r.sprite,color=r.color,sharedMaterial=r.sharedMaterial,sortingOrder=r.sortingOrder};
            copy.transform.SetParent(result.spriteContainer,false);copy.transform.localPosition=r.transform.localPosition;
            copy.transform.localScale=r.transform.localScale;copy.transform.localRotation=r.transform.localRotation;copy.gameObject.SetActive(r.gameObject.activeSelf);return copy;
        }
        var parts=new List<SkinVisualsReference.VisualPart>();
        for(int i=0;i<src.visualParts.Length;i++){var part=src.visualParts[i];parts.Add(new(){tintable=part.tintable,
            renderer=new(){spriteRenderer=Copy(part.renderer.spriteRenderer)},outlineRenderer=new(){spriteRenderer=Copy(part.outlineRenderer.spriteRenderer)}});}
        target.visualParts=new(parts.ToArray());result.skinVisuals=null!;return result;
    }
    public static void Reset(){CloneCalls=SkinCalls=HeadScaleCalls=0;Alias=FailSkin=false;Clones.Clear();LastProfile=null;ChipMunkManager.active=false;}
}
public sealed class SkinVisualsReference:UnityEngine.Component
{
    public sealed class RendererUnion:Il2CppSystem.Object
    {public UnityEngine.SpriteRenderer? spriteRenderer {get;set;}public PolytopiaSpriteRenderer? polytopiaSpriteRenderer {get;set;}}
    public sealed class VisualPart:Il2CppSystem.Object
    {
        public bool tintable {get;set;}public RendererUnion renderer {get;set;}=new();public RendererUnion outlineRenderer {get;set;}=new();}
    public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<VisualPart> visualParts {get;set;}=new(0);
}
public sealed class PolytopiaSpriteRenderer:UnityEngine.Component
{
    UnityEngine.Color Stored=new(1,1,1,1);
    public UnityEngine.SpriteRenderer? spriteRenderer {get;set;}public UnityEngine.MeshRenderer? meshRenderer {get;set;}public UnityEngine.Sprite? Sprite {get;set;}
    public UnityEngine.Bounds Bounds {get;set;}=new(new(0,0,0),new(1,1,0));
    public int SortingLayer {get;set;}public int SortingOrder {get;set;}
    public UnityEngine.Material SharedMaterial {get;set;}=new();public UnityEngine.Color overlayColor {get;set;}=new(1,1,1,1);public float overlayStrength {get;set;}
    public bool isColorLocked {get;set;}public bool IsUsed {get;set;}=true;public bool isBatched=true;
    public bool Dirty;public int ColorWrites,LockedWrites,PoolReturns;
    public UnityEngine.Color Color
    {
        get=>Stored;
        set
        {
            ColorWrites++;
            float d=(Stored.r-value.r)*(Stored.r-value.r)+(Stored.g-value.g)*(Stored.g-value.g)+(Stored.b-value.b)*(Stored.b-value.b)+(Stored.a-value.a)*(Stored.a-value.a);
            if(d<0.000000001f||isColorLocked)return;
            if(spriteRenderer is not null)spriteRenderer.color=value;
            Stored=value;SetDirty(true);
        }
    }
    public void SetUnchangeableColor(UnityEngine.Color value)
    {LockedWrites++;isColorLocked=false;Stored=new(0,0,0,0);Color=value;isColorLocked=true;}
    public void SetDirty(bool value){Dirty=value;}
    public void ReturnToPool()
    {
        if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.RendererReturning(this);
        PoolReturns++;isBatched=false;gameObject.SetActive(false);transform.SetParent(MapRenderer.Pool,false);IsUsed=false;
        // Native return leaves Color and isColorLocked untouched for reuse.
    }
}
public sealed class MapRenderer:UnityEngine.Component
{
    public static MapRenderer? Current {get;set;}public static bool ProductionCallbacks;
    public static MapRenderer? instance {get=>Current;set=>Current=value;}
    public Dictionary<(int,int),Tile> Tiles=new();public Tile? GetTileInstance(WorldCoordinates coordinates)=>Tiles.TryGetValue((coordinates.X,coordinates.Y),out var tile)?tile:null;
    public static UnityEngine.Transform Pool=new();
    public Il2CppSystem.Collections.Generic.List<Unit> RenderedUnits {get;set;}=new();
    public readonly Dictionary<uint,Unit> Units=new();
    public Unit? GetUnitInstance(uint id)=>Units.TryGetValue(id,out var unit)?unit:null;
    public void Register(Unit unit){Units[unit.UnitState.id]=unit;RenderedUnits.Add(unit);}
}
public sealed class ClientActionManager:Il2CppSystem.Object {public bool IsProcessing {get;set;}}
public class ClientBase:Il2CppSystem.Object
{
    public enum ClientType { Local=0,PassAndPlay=1,Remote=2 }
    public ClientType clientType {get;set;}=ClientType.Local;
    public bool IsReady {get;set;}=true;public GameState GameState {get;set;}=GameManager.GameState;
    public ClientActionManager ActionManager {get;set;}=new();public bool IsWaitingForCommand {get;set;}
    public bool IsReplay {get;set;}public bool IsSpectating {get;set;}
    public int Sent;public CommandBase? SentCommand;public void SendCommand(CommandBase command){Sent++;SentCommand=command;}public void SendCommandRemote(CommandBase command){}
    public void SetNewLocalPlayerTurnForPassAndPlay(byte player){}
}
public sealed class ReplayClient:ClientBase {public ReplayClient(){IsReplay=true;}}
// Installed ClientInteraction derives UnityEngine.MonoBehaviour; keep the
// regroup-specific methods below while modeling native Unity lifetime.
public sealed class ClientInteraction:UnityEngine.Component
{
    public sealed class __c__DisplayClass36_0:Il2CppSystem.Object {public ClientInteraction __4__this{get;set;}=null!;public Tile tile{get;set;}=null!;public void _SelectTile_b__0(){}}
    public int Selections;
    public Tile? selectedTile {get;set;}public void SelectTile(Tile tile){selectedTile=tile;selectedUnit=null;Bar.tile=tile;}
    public Unit? selectedUnit {get;set;}public bool wasDownOverUI {get;set;}public bool ValidTap=true;
    public InteractionBar Bar=new();public InteractionBar GetInteractionBar()=>Bar;
    public int TapChecks;public bool IsTouchValidTap(int touch){TapChecks++;if(touch<0)throw new ArgumentOutOfRangeException(nameof(touch));return ValidTap;}
    public bool ValidLongPress=true;public int LastLongpressIndex=-1;
    public bool IsTouchValidLongpress(int touch){LastLongpressIndex=touch;return ValidLongPress;}
    public Tile? currentTile {get;set;} public void OnPress(Tile tile){}public void Process(){}
    public void OnRelease(Tile tile,int touch){}public void SelectUnit(Unit unit){Selections++;selectedUnit=unit;selectedTile=null;Bar.unit=unit;}
    public bool TryDeselectUnit(Unit unit)=>true;
    public void ClearSelection(){}public void DeselectUnit(){}public void OnSelectionCleared(){}
    public void OnTurnStarted(){}public void OnDisable(){}
    public void ValidateMove(Unit unit,Tile tile,Il2CppSystem.Action callback){}
}
public static class CameraController
{public static bool IsDragging {get;set;}public static bool IsBlocked {get;set;}}
public static class PolytopiaInput
{
    public static Func<UnityEngine.Vector2,bool>? UIHitTest;
    public static UnityEngine.Vector2 LastUIHitPosition;public static int LastUIExcludeLayer;
    public static bool IsPositionOverUIObject(UnityEngine.Vector2 position,int excludeLayer)
    {LastUIHitPosition=position;LastUIExcludeLayer=excludeLayer;return UIHitTest?.Invoke(position)??false;}
    public static bool OutsideWindow {get;set;}public static bool MouseHeld;
    public static UnityEngine.Vector2 mousePosition {get;set;}
    public static Il2CppSystem.Collections.Generic.List<PolytopiaTouch> CurrentTouches {get;set;}=new();
    public static bool GetMouseButton(int button)=>MouseHeld;
}
public sealed class PolytopiaTouch:Il2CppSystem.Object
{
    public int fingerId {get;set;}public UnityEngine.TouchPhase phase {get;set;}=UnityEngine.TouchPhase.Stationary;
    public UnityEngine.Vector2 position {get;set;}public float maxSquareDistance {get;set;}
}
public static class SpriteRef
{public static int UI_BACK_WHITE=>1;public static int UI_INFO=>2;public static int UNKNOWN_ICON=>3;public static int UI_BACK=>4;public static int UI_UNKNOWN=>5;public static int HEAD_ROBOT=>6;}
public sealed class SpriteAtlasManager:Il2CppSystem.Object
{
    public UnityEngine.Rect UnknownBounds=new(20,20);
    public sealed class SpriteLookupResult:Il2CppSystem.Object {public UnityEngine.Sprite? sprite {get;set;}}
    public readonly System.Collections.Generic.List<(string Name,PolytopiaBackendBase.Common.TribeType Tribe,PolytopiaBackendBase.Common.SkinType Skin,int Level)> Lookups=new();
    public UnityEngine.Sprite? Head=new("Native tribe head"){rect=new(40,32)};
    public SpriteLookupResult DoSpriteLookup(string name,PolytopiaBackendBase.Common.TribeType tribe,PolytopiaBackendBase.Common.SkinType skin,int level)
    {Lookups.Add((name,tribe,skin,level));return new(){sprite=Head};}
    public UnityEngine.Sprite GetSprite(int id)=>new(id switch {1=>"UI_BACK_WHITE",2=>"UI_INFO",3=>"UNKNOWN_ICON",4=>"UI_BACK",5=>"UI_UNKNOWN",_=>"HEAD_ROBOT"}){rect=id==3?UnknownBounds:new(20,20)};
}
public sealed class SpriteAddress:Il2CppSystem.Object
{
    public string atlas {get;set;}="";public string sprite {get;set;}="";
    public PolytopiaBackendBase.Common.TribeType Tribe;
    public PolytopiaBackendBase.Common.SkinType Skin;
}
public static class SpriteData
{
    public static SpriteAddress GetUnitIconAddress(UnitData.Type type)=>new(){atlas="unit",sprite=type.ToString()};
    public static string HeadSpriteBaseName=>"head";
    public static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<SpriteAddress> GetHeadSpriteAddresses(GameState game,PlayerState player)
        =>new(new[]{new SpriteAddress{atlas="skin",sprite=player.GetVisualSkin().ToString(),Tribe=player.tribe,Skin=player.GetVisualSkin()},
            new SpriteAddress{atlas="tribe",sprite=player.tribe.ToString(),Tribe=player.tribe,Skin=player.GetVisualSkin()}});
}
public sealed class SpriteHandle:Il2CppSystem.Object
{
    public static bool Delay;
    public static readonly List<SpriteHandle> Requested=new();
    public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<SpriteAddress>? Addresses;
    public bool isCompleted {get;set;}
    public UnityEngine.Sprite? sprite {get;set;}
    public void Request(SpriteAddress address){Addresses=new(new[]{address});Requested.Add(this);if(!Delay)Complete(new UnityEngine.Sprite("Native icon "+address.sprite));}
    public void Request(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<SpriteAddress> addresses)
    {Addresses=addresses;Requested.Add(this);if(!Delay)Complete(GameManager.Atlas.Head);}
    public void Complete(UnityEngine.Sprite? value){sprite=value;isCompleted=true;}
    public static void Reset(){Delay=false;Requested.Clear();}
}
public static class PlayerExtensions
{public static PolytopiaBackendBase.Common.TribeType GetVisualTribeType(PlayerState player)=>player.tribe;
 public static bool HasTech(PlayerState player,TechData.Type tech)=>tech==TechData.Type.dcp_intelligence?player.Intelligence:tech==TechData.Type.dcp_espionage?player.Espionage:tech==TechData.Type.dcp_propaganda?player.Propaganda:tech==TechData.Type.dcp_deception&&player.Deception;}
public static class ColorUtil
{public static UnityEngine.Color ColorFromInt(int color)=>new(((color>>16)&255)/255f,((color>>8)&255)/255f,(color&255)/255f,1);}
public sealed class UIManager:Il2CppSystem.Object
{public static UnityEngine.Canvas Canvas {get;set;}=new();public static bool ShouldBlockScrollWheelFromZooming()=>false;public static UIManager Instance {get;set;}=new();public UISpriteDuplicator SpriteDuplicator {get;set;}=new();}
public sealed class UIDuplicatedSprites:UnityEngine.Component
{
    public UnityEngine.RectTransform rectTransform {get;set;}=new();public int ClearCalls,RestoreCalls;public UnityEngine.Sprite? LastSprite;
    public readonly System.Collections.Generic.List<UISpriteDuplicator.CopiedImage> CopiedImages=new();
    public readonly System.Collections.Generic.List<UnityEngine.Transform> RenderedSources=new();
    public UIDuplicatedSprites(){rectTransform.Owner=this;}
    public void Clear(){ClearCalls++;LastSprite=null;CopiedImages.Clear();rectTransform.ClearChildren();}
}
public sealed class UISpriteDuplicator:Il2CppSystem.Object
{
    public sealed record CopiedImage(UnityEngine.Component Source,UnityEngine.Sprite Sprite,UnityEngine.Color Color,UnityEngine.RectTransform Rect);
    public sealed class SpriteDuplicationData:Il2CppSystem.Object
    {public UnityEngine.Transform source {get;set;}=new();public UIDuplicatedSprites container {get;set;}=new();public UnityEngine.Vector2 offset {get;set;}public float scale {get;set;}=1;public string excludeSortingLayer {get;set;}="";public bool forceFullAlpha {get;set;}}
    public bool ThrowRestore,ExtraFactoryChild;public int FailAtFactoryCall=-1,FactoryCalls;public readonly System.Collections.Generic.List<UnityEngine.UI.Image> FactoryImages=new();public UnityEngine.RectTransform? LastCreated;public UnityEngine.Vector2 LastOffset;public float LastScale;
    public Action<SpriteDuplicationData>? RenderOverride;
    public void RenderSprites(SpriteDuplicationData data)
    {
        object? scope=null;Exception? error=null;
        if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.BeforeDuplicationData(data,out scope);
        try{RenderSprites(data.source,data.container,data.scale,data.excludeSortingLayer,data.offset,data.forceFullAlpha);}
        catch(Exception e){error=e;throw;}
        finally{if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.DuplicationFinished(error,scope);}
    }
    public void RenderSprites(UnityEngine.Transform source,UIDuplicatedSprites container,float scale,string excludeSortingLayer,UnityEngine.Vector2 offset,bool forceFullAlpha)
    {
        object? scope=null;Exception? error=null;
        if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.BeforeDuplicationSource(source,out scope);
        try
        {
            if(ThrowRestore)throw new InvalidOperationException("Synthetic portrait restore failure");
            container.RestoreCalls++;container.RenderedSources.Add(source);container.Clear();
            var data=new SpriteDuplicationData{source=source,container=container,scale=scale,excludeSortingLayer=excludeSortingLayer,offset=offset,forceFullAlpha=forceFullAlpha};
            if(RenderOverride is not null)RenderOverride(data);else CopyNativeChildren(data);
        }
        catch(Exception e){error=e;throw;}
        finally{if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.DuplicationFinished(error,scope);}
    }
    public static UnityEngine.Transform NativePreviewSource()
    {
        var root=new UnityEngine.Transform();
        var body=new UnityEngine.SpriteRenderer{sprite=new UnityEngine.Sprite("Warrior body"){rect=new(40,60)}};
        body.transform.SetParent(root,false);return root;
    }
    internal void CopyNativeChildren(SpriteDuplicationData data)
    {
        // The audited fallback enumerates active descendant SpriteRenderers,
        // then PSRs without an underlying SpriteRenderer (no double body copy).
        foreach(var renderer in data.source.gameObject.ComponentsInChildren<UnityEngine.SpriteRenderer>())
            if(renderer.sprite is not null)Copy(renderer,renderer.sprite,renderer.color,data);
        foreach(var renderer in data.source.gameObject.ComponentsInChildren<PolytopiaSpriteRenderer>())
            if(!renderer.spriteRenderer&&renderer.Sprite is not null)Copy(renderer,renderer.Sprite,renderer.Color,data);
    }
    void Copy(UnityEngine.Component source,UnityEngine.Sprite sprite,UnityEngine.Color color,SpriteDuplicationData data)
    {
        // Representative copy geometry, not a full Unity CreateImage simulation:
        // root-relative rect children let the audited GetActualRect union observe
        // every copied layer. Tests compare this to its own clean native baseline.
        var position=data.source.InverseTransformPoint(source.transform.position);
        float width=sprite.rect.width*data.scale,height=sprite.rect.height*data.scale;
        var rect=new UnityEngine.RectTransform{rect=new(-width/2,-height/2,width,height),anchoredPosition=new(position.x*data.scale+data.offset.x,position.y*data.scale+data.offset.y)};
        data.container.rectTransform.Append(rect);
        var copiedColor=data.forceFullAlpha?color with{a=1}:color;
        var image=new UnityEngine.UI.Image{gameObject=rect.gameObject,sprite=sprite,color=copiedColor};
        data.container.CopiedImages.Add(new(source,sprite,copiedColor,rect));
    }
    public void CreateImageFromSprite(UnityEngine.Sprite sprite,UnityEngine.Transform parent,UnityEngine.Vector2 offset,float scale)
    {
        LastOffset=offset;LastScale=scale;LastCreated=new(){CenteredSizeWrites=true,rect=new(-sprite.rect.width*scale/2,-sprite.rect.height*scale/2,sprite.rect.width*scale,sprite.rect.height*scale),anchoredPosition=offset,localScale=new(0.25f,0.25f,0.25f)};parent.Append(LastCreated);
        FactoryImages.Add(new UnityEngine.UI.Image{gameObject=LastCreated.gameObject,sprite=sprite});
        if(++FactoryCalls==FailAtFactoryCall)throw new InvalidOperationException("Injected partial image factory failure");
        if(ExtraFactoryChild)parent.Append(new UnityEngine.RectTransform());
        if(parent is UnityEngine.RectTransform r&&r.Owner is UIDuplicatedSprites container)container.LastSprite=sprite;
    }
}
public sealed class TestLabel { public string Text {get;set;}=""; }
public sealed class TestText { public string text {get;set;}=""; }
public class UIButtonBase:UnityEngine.Component
{
    public UnityEngine.RectTransform rectTransform=>(UnityEngine.RectTransform)transform;
    public UIButtonBase(){gameObject=new UnityEngine.RectTransform().gameObject;}
    public sealed class ButtonAction:Il2CppSystem.Object
    {
        readonly Action<int,UnityEngine.EventSystems.BaseEventData> Fn;
        public ButtonAction(Action<int,UnityEngine.EventSystems.BaseEventData> fn){Fn=fn;}
        public static implicit operator ButtonAction(Action<int,UnityEngine.EventSystems.BaseEventData> a)=>new(a);
        public void Click()=>Fn(0,new());
    }
    public ButtonAction? OnClicked {get;set;}
    public ButtonAction? OnClickedWhenDisabled {get;set;}
    public bool ButtonEnabled {get;set;}=true;
    public bool Active {get;private set;}=true;public void SetActive(bool active){Active=active;gameObject.SetActive(active);}
    public void ClearCallbacks(){OnClicked=null;OnClickedWhenDisabled=null;}
}
public sealed class UITextButton:UIButtonBase
{
    public string Key {get;set;}="";
    public PolytopiaBackendBase.Common.SkinType Skin {get;set;}
    public PolytopiaBackendBase.Common.TribeType Tribe {get;set;}
}
public sealed class UIUnitRenderer:UnityEngine.Component
{
    public UnitData.Type unitType {get;set;}
    public UIDuplicatedSprites duplicatedSpritesContainer {get;set;}=new();
    public UISpriteDuplicator.SpriteDuplicationData spriteDuplicationData {get;set;}
    public UnityEngine.RectTransform rectTransform=>(UnityEngine.RectTransform)transform;
    public UIUnitRenderer(){gameObject=new UnityEngine.RectTransform().gameObject;duplicatedSpritesContainer.rectTransform.SetParent(rectTransform,false);spriteDuplicationData=new(){container=duplicatedSpritesContainer,source=UISpriteDuplicator.NativePreviewSource()};}
}
public static class UIConstants { public static float UI_PIXELS_PER_UNIT {get;set;}=100; }
public static class UIUtils
{
    public static UnityEngine.RectTransform? FailBoundsRoot;
    public static int FailBoundsImageCount;
    public static UnityEngine.UI.Image GetImage()
    {var image=new UnityEngine.UI.Image();BranchTechFixture.Images.Add(image);return image;}
    public static UnityEngine.Sprite GetImprovementSprite(ImprovementData.Type type,PolytopiaBackendBase.Common.TribeType tribe,PolytopiaBackendBase.Common.SkinType skin,SpriteAtlasManager atlas)
    {if(PopupManager.SmuggleCallbacks)Deception.PolyModAdapter.MimicSmuggleUi.SpriteAlias(ref type);return new(type.ToString());}
    public static UnityEngine.Sprite GetImprovementSprite(SkinVisualsTransientData visuals,ImprovementData.Type type,SpriteAtlasManager atlas)
    {if(PopupManager.SmuggleCallbacks)Deception.PolyModAdapter.MimicSmuggleUi.TransientSpriteAlias(ref type);return new(type.ToString());}
    public static bool CategoryCallbacks;
    public static Action<UIUnitRenderer,UnitData,PlayerState,PolytopiaBackendBase.Common.SkinType>? PreviewOverride;
    public static readonly List<UIUnitRenderer> Previews=new();
    // Installed native UnitPopup.set_UnitData branches to this overload
    // for payload.unit, bypassing the UnitData category-art hook (0x2a9bac0).
    public static UIUnitRenderer GetUIUnitRenderer(Unit unit,PlayerState player)
    {
        var renderer=new UIUnitRenderer{unitType=unit.UnitState.type};Previews.Add(renderer);
        renderer.spriteDuplicationData.source=unit.spriteContainer;
        UIManager.Instance.SpriteDuplicator.RenderSprites(renderer.spriteDuplicationData);
        return renderer;
    }
    public static UIUnitRenderer GetUIUnitRenderer(UnitData data,PlayerState player,PolytopiaBackendBase.Common.SkinType skin)
    {
        var renderer=new UIUnitRenderer{unitType=data.type};Previews.Add(renderer);
        if(PreviewOverride is not null)PreviewOverride(renderer,data,player,skin);
        else
        {
            UIManager.Instance.SpriteDuplicator.RenderSprites(renderer.spriteDuplicationData);
            // A static genuine prefab is selected by the requested form rather
            // than the map object's retained original prefab or action flags.
            foreach(var image in renderer.duplicatedSpritesContainer.rectTransform.gameObject.ComponentsInChildren<UnityEngine.UI.Image>())
            {image.sprite=new UnityEngine.Sprite(data.type==UnitData.Type.rider?"rider static body":data.type==UnitData.Type.polytaur?"polytaur static body":data.type is UnitData.Type.cloak or UnitData.Type.mermaidcloak?data.type+" static body":"Warrior body"){rect=new(40,60)};image.color=new(1,1,1,1);}
        }
        if(CategoryCallbacks)Deception.PolyModAdapter.MimicControls.CategoryRenderer(data,player,renderer);
        // Mirrors native CreateUnit(true): copied Images survive their source.
        UnityEngine.Object.Destroy(renderer.spriteDuplicationData.source.gameObject);
        return renderer;
    }
    // Exact native arithmetic for centered full-rect fixture meshes: sprite
    // vertices * UI PPU plus root-relative child origin. Image rect size and
    // child localScale DO NOT scale the vertices in the real native helper.
    // This fixture still does not decode alpha-tight PNG or atlas meshes.
    public static UnityEngine.RectTransform? FailFitRoot;
    public static int PopupFitCalls;
    // Installed UIUtils0x2b079b0: parent.sizeDelta / image-vertex extent,
    // independent of the old root scale; then center the fitted content.
    public static void FitImageContentInParent(UnityEngine.RectTransform imageRect)
    {
        PopupFitCalls++;
        if(ReferenceEquals(imageRect,FailFitRoot))throw new InvalidOperationException("Injected popup fit failure");
        var parent=imageRect.parent as UnityEngine.RectTransform??imageRect;
        var bounds=GetImageVerticesBounds(imageRect);float extent=Math.Max(bounds.size.x,bounds.size.y);
        float fit=extent>0?Math.Max(parent.sizeDelta.x,parent.sizeDelta.y)/extent:1;
        imageRect.localScale=new(fit,fit,1);imageRect.anchoredPosition=new(-bounds.center.x*fit,-bounds.center.y*fit);
    }
    public static UnityEngine.Bounds GetImageVerticesBounds(UnityEngine.RectTransform root)
    {
        float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
        void Visit(UnityEngine.Transform node,float px,float py,float sx,float sy,bool isRoot)
        {
            var rect=node as UnityEngine.RectTransform;
            float x=px+(isRoot?0:(rect?.anchoredPosition.x??node.localPosition.x)*sx);
            float y=py+(isRoot?0:(rect?.anchoredPosition.y??node.localPosition.y)*sy);
            var image=node.gameObject.FindComponent<UnityEngine.UI.Image>();
            if(image?.sprite is UnityEngine.Sprite sprite&&rect is not null)
            {
                float w=sprite.rect.width/sprite.PixelsPerUnit*UIConstants.UI_PIXELS_PER_UNIT;
                float h=sprite.rect.height/sprite.PixelsPerUnit*UIConstants.UI_PIXELS_PER_UNIT;
                minX=Math.Min(minX,x-w/2);maxX=Math.Max(maxX,x+w/2);
                minY=Math.Min(minY,y-h/2);maxY=Math.Max(maxY,y+h/2);
            }
            sx*=isRoot?1:node.localScale.x;sy*=isRoot?1:node.localScale.y;
            for(int i=0;i<node.childCount;i++)Visit(node.GetChild(i),x,y,sx,sy,false);
        }
        if(ReferenceEquals(root,FailBoundsRoot)&&root.gameObject.ComponentsInChildren<UnityEngine.UI.Image>().Count()==FailBoundsImageCount)
            throw new InvalidOperationException("Injected final category fit failure");
        Visit(root,0,0,1,1,true);
        if(!float.IsFinite(minX))return new(new(0,0,0),new(0,0,0));
        return new(new((minX+maxX)/2,(minY+maxY)/2,0),new(maxX-minX,maxY-minY,0));
    }
    // Native GetActualRect unions direct RectTransform children, including the
    // origin (or root rect when requested). It does not inspect image opacity.
    public static UnityEngine.Rect GetActualRect(UnityEngine.RectTransform root,bool includeRootSize)
    {
        var initial=includeRootSize?root.rect:new UnityEngine.Rect(0,0);
        float left=initial.x,bottom=initial.y,right=left+initial.width,top=bottom+initial.height;
        for(int i=0;i<root.childCount;i++)
        {
            if(root.GetChild(i) is not UnityEngine.RectTransform child)continue;
            float x=child.rect.x+child.anchoredPosition.x,y=child.rect.y+child.anchoredPosition.y;
            left=Math.Min(left,x);bottom=Math.Min(bottom,y);right=Math.Max(right,x+child.rect.width);top=Math.Max(top,y+child.rect.height);
        }
        return new(left,bottom,right-left,top-bottom);
    }
}
public sealed class ResourceWidget:UnityEngine.Component {public float Value {get;set;}}
public sealed class PolytopiaScrollRect:UnityEngine.UI.ScrollRect
{public void OnScroll(UnityEngine.EventSystems.PointerEventData e){}public void Update(){}}
public sealed class UIRoundButton:UIButtonBase
{
    public string Text="";public string text {get=>Text;set=>Text=value;} public TestLabel Label {get;set;}=new();
    public bool ShowLabel {get;set;} public bool LabelShowOnHover {get;set;}
    public bool ShowTextBackground {get;set;}
    float NativeCost=-1;public ResourceWidget resourceWidget {get;set;}=new();
    public float Cost {get=>NativeCost;set{NativeCost=value;resourceWidget.gameObject.SetActive(value>=0);resourceWidget.Value=value;}}
    public bool buttonActive {get;set;} public bool buttonExpensive {get;set;}
    public UIUnitRenderer? Icon;
    public UnityEngine.UI.Image icon {get;set;}=new();
    public UnityEngine.UI.Image BG {get;set;}=new(){sprite=new UnityEngine.Sprite("Native circle")};
    public UIRoundButton(){resourceWidget.transform.SetParent(transform,false);Cost=-1;BG.transform.SetParent(transform,false);icon.transform.SetParent(transform,false);}
    public UIRoundButton SetText(string text){Text=text;return this;}
    public void SetUnitRenderer(UIUnitRenderer renderer){Icon=renderer;}
    public UnityEngine.Sprite? AssignedSprite;public int SpriteCalls;public UIRoundButton SetSprite(string name,bool nativeSize){SetSprite(new UnityEngine.Sprite(name),nativeSize);return this;}public void SetSprite(UnityEngine.Sprite sprite,bool value){AssignedSprite=sprite;SpriteCalls++;}
    // Keep ordinary fixture object inspection available after destruction, but
    // model the native Component.get_gameObject dereference used during cleanup.
    // A stale managed wrapper cannot make a destroyed native button usable.
    internal UnityEngine.GameObject NativeGameObject()
    {if(!this)throw new NullReferenceException("Native button gameObject was destroyed");return gameObject;}
}
public sealed class QuickActions:Il2CppSystem.Object
{
    UnityEngine.Transform? Follow;
    public int TargetSetCalls,ShowCalls,ClearCalls,ClearTweensCalls;
    public UnityEngine.RectTransform buttonContainer {get;set;}=new();
    public UnityEngine.Transform? Target
    {
        get=>Follow;
        set { TargetSetCalls++;if(value is null)throw new NullReferenceException("QuickActions.set_Target fixture rejects a null Transform");Follow=value; }
    }
    public UnityEngine.Vector3 Position {get;set;}
    public bool KeepWorldPosition {get;set;}=true;
    public bool IsShowing {get;set;}
    public bool waitingForMouseUp {get;set;}
    public Il2CppSystem.Collections.Generic.List<UIRoundButton> buttons {get;set;}=new();
    public void Show(bool instant){ShowCalls++;IsShowing=true;}
    public void Hide(bool instant){IsShowing=false;Follow=null;}
    public void AddButton(UIRoundButton b)=>buttons.Add(b);
    public void SortButtons() { }
    public bool HasButtons()=>buttons.Count>0;
    public void Clear()
    {
        ClearCalls++;ClearTweensCalls++;
        // Native Clear dereferences every entry without an alive guard. It does
        // not silently discard a destroyed button left by another list owner.
        for(int i=0;i<buttons.Count;i++)UnityEngine.Object.Destroy(buttons[i].NativeGameObject());
        buttons.Clear();
        // Native Clear preserves Target, Position and IsShowing. Hide owns its
        // own visibility/follow behavior; clearing the list does not replace it.
    }
}
public class PopupBase:UnityEngine.Component
{
    public sealed class PopupButtonData:Il2CppSystem.Object {public enum States {None,Disabled} public int id;public string text="";public States state;public bool closesPopup;public Il2CppSystem.Action? callback;}
    public Il2CppSystem.Action? HideCallback {get;set;}
    public bool Showing=true;public int ShowCalls;
    public void Show(){if(PopupManager.ProductionCallbacks&&!Deception.PolyModAdapter.MimicPresentationUi.PopupCanShow(this))return;Showing=true;ShowCalls++;if(PopupManager.RegroupCallbacks){PopupManager.Current=this;PopupManager.PopupShowing=true;}if(PopupManager.ProductionCallbacks)PopupManager.PopupShowing=true;}
    public void Hide(){if(PopupManager.ProductionCallbacks)Deception.PolyModAdapter.MimicPresentationUi.PopupHiding(this);Showing=false;PopupManager.PopupShowing=false;if(PopupManager.RegroupCallbacks){if(ReferenceEquals(PopupManager.Current,this))PopupManager.Current=null;HideCallback?.Invoke();}}public bool IsShowing()=>Showing;
}
public sealed class UISquareButton_UI2:Il2CppSystem.Object { }
public sealed class UIBuildingRenderer:UnityEngine.Component
{
    public ImprovementData? LastData;public UnityEngine.Sprite? LastSprite;
    public void SetBuildingData(ImprovementData data,PolytopiaBackendBase.Common.SkinType skin,PolytopiaBackendBase.Common.TribeType tribe)
    {LastData=data;LastSprite=UIUtils.GetImprovementSprite(new SkinVisualsTransientData(),data.type,GameManager.Atlas);}
}
public class BasicPopupLegacy:PopupBase
{
    public int DefaultOkCalls;public Il2CppSystem.Action? DefaultOkCallback;
    public void SetDefaultOkbutton(Il2CppSystem.Action? callback){DefaultOkCalls++;DefaultOkCallback=callback;}
}
public sealed class BasicPopup:PopupBase
{public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<PopupBase.PopupButtonData>? Buttons;public void SetButtonData(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<PopupBase.PopupButtonData> buttons,bool navigation){Buttons=buttons;}
    public string Header="",Description="";
    public int TopRightCost;public UIBuildingRenderer? Building;
    public BasicPopup SetHeader(string text){Header=text;return this;}
    public BasicPopup SetDescription(string text){Description=text;return this;}
    public UISquareButton_UI2 SetMainButton(string text,Il2CppSystem.Action action)=>new();
    public int DefaultOkCalls;public void SetDefaultOkbutton(Il2CppSystem.Action? callback){DefaultOkCalls++;}
}
public static class PopupManager
{
    public static bool RegroupCallbacks;public static BasicPopup? LastBasic;
    public static object instance {get;}=new object();
    public static PopupBase? Current;public static PopupBase? GetCurrentPopup()=>Current;
    public static bool SmuggleCallbacks;public static BasicPopup? LastIconPopup;
    public static BasicPopup GetIconPopup()
    {var popup=new BasicPopup();LastIconPopup=popup;if(SmuggleCallbacks)Deception.PolyModAdapter.MimicSmuggleUi.PopupCreated(popup);return popup;}
    public sealed class UnitPopupData:Il2CppSystem.Object
    {
        public Unit? unit {get;set;}public UnitData? unitData {get;set;}
        public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<PopupBase.PopupButtonData>? buttonData {get;set;}
    }
    public static bool PopupShowing {get;set;}
    public static bool ProductionCallbacks;public static int FactoryCalls;public static UnitPopup? LastPopup;public static UnitPopupData? LastPayload;
    public static System.Action? BeforePopulate,AfterPopulate;
    public static BasicPopup? LastBasicPopup;
    public static BasicPopup GetBasicPopup(){LastBasic=new();LastBasicPopup=LastBasic;return LastBasic;}
    public static UnitPopup GetUnitPopup(UnitPopupData payload)
    {
        // This models the factory/setter boundary, including the native setter's
        // modeled ability to append rows. It does not execute Harmony or Unity.
        FactoryCalls++;object? scope=null;Exception? failure=null;
        try
        {
            if(ProductionCallbacks)Deception.PolyModAdapter.MimicPresentationUi.PopupData(ref payload,out scope);
            var popup=new UnitPopup{Player=GameManager.LocalPlayer,unit=payload.unit,Showing=!ProductionCallbacks};
            LastPayload=payload;LastPopup=popup;BeforePopulate?.Invoke();
            popup.UnitData=payload.unitData??payload.unit?.UnitState.UnitData??new(UnitData.Type.warrior);
            AfterPopulate?.Invoke();
            if(ProductionCallbacks)Deception.PolyModAdapter.MimicPresentationUi.PopupReady(popup,scope);
            return popup;
        }
        catch(Exception e){failure=e;throw;}
        finally{if(ProductionCallbacks)Deception.PolyModAdapter.MimicPresentationUi.PopupFinished(failure,scope);}
    }
}
namespace I2.Loc {public static class ScriptLocalization {public static string world_unit_health=>"Health";}}
public sealed class UnitStatsRow:Il2CppSystem.Object
{public TMPro.TextMeshProUGUI statsNameLabel {get;set;}=new();public TMPro.TextMeshProUGUI statsValueLabel {get;set;}=new();}
public sealed class UnitPopup:BasicPopupLegacy
{
    public TMPro.TextMeshProUGUI header {get;set;}=new();
    public UITextButton abilityButtonMovement {get;set;}=new();
    public string Header {get=>header.text;set=>header.text=value;}public string Description {get;set;}="";
    public UnitData data {get;set;}=new(UnitData.Type.warrior);
    public PlayerState player {get;set;}=new();
    public Unit? unit {get;set;}
    public UnityEngine.RectTransform iconContainer {get;set;}=new(){rect=new(100,100)};
    public int PopulationCalls,PlayerSets;public byte OwnerAtPopulate;public bool FailPopulate;
    public UnityEngine.RectTransform abilityButtonContainer {get;set;}=new();
    public Il2CppSystem.Collections.Generic.List<UITextButton> abilityButtons {get;set;}=new();
    public int NativeAbilityClicks;
    public PolytopiaBackendBase.Common.SkinType getUnitSkin()=>player.skinType;
    public PolytopiaBackendBase.Common.TribeType getUnitTribe()=>player.tribe;
    public UITextButton GetAbilityButton(UnitAbility.Type ability)
    {
        var button=Enumerable.Range(0,abilityButtons.Count).Select(i=>abilityButtons[i]).FirstOrDefault(x=>!x.gameObject.activeSelf);
        if(button is null){button=new();button.transform.SetParent(abilityButtonContainer,false);button.OnClicked=new((_,_)=>NativeAbilityClicks++);abilityButtons.Add(button);}
        button.gameObject.SetActive(true);return button;
    }
    public UnitData UnitData
    {
        get=>data;
        set
        {
            var projected=value;
            if(PopupManager.ProductionCallbacks)Deception.PolyModAdapter.MimicPresentationUi.PopupPopulate(this,ref projected);
            if(FailPopulate)throw new InvalidOperationException("Synthetic native setter failure");
            data=projected;PopulationCalls++;OwnerAtPopulate=player.Id;
            // Native selects a live-unit overload or the static category
            // overload, then retains the copied renderer after source disposal.
            unitRenderer=unit is null?UIUtils.GetUIUnitRenderer(data,player,getUnitSkin()):UIUtils.GetUIUnitRenderer(unit,player);
            // Observed native popup duplicated-sprite prefab is100x100 (0376 logs).
            unitRenderer.duplicatedSpritesContainer.rectTransform.rect=new(100,100);
            unitRenderer.rectTransform.SetParent(iconContainer,false);
            UIUtils.FitImageContentInParent(unitRenderer.rectTransform);
            // Native rows are pooled visual objects. A second set does not replace
            // the first group here, reproducing the user's duplicated-stat symptom.
            AddStatsRow("Health",(data.health/10).ToString());AddStatsRow("Attack",data.attack.ToString());
            AddStatsRow("Defense",data.defence.ToString());AddStatsRow("Movement",data.movement.ToString());
            AddStatsRow("Range",data.range.ToString());AddStatsRow("Cost",data.cost.ToString());
            if(PopupManager.ProductionCallbacks)Deception.PolyModAdapter.NativeSpyPresentation.PopupPopulated(this);
        }
    }
    public PlayerState Player {get=>player;set{player=value;PlayerSets++;}}
    public UIUnitRenderer unitRenderer {get;set;}=new();
    public Il2CppSystem.Collections.Generic.List<UnitStatsRow> statsRows {get;set;}=new();
    public void AddStatsRow(string label,string value)=>statsRows.Add(new(){statsNameLabel=new(){text=label},statsValueLabel=new(){text=value}});
    public void ResetPopup(){if(PopupManager.ProductionCallbacks)Deception.PolyModAdapter.MimicPresentationUi.ResetPopup(this);statsRows.Clear();foreach(var b in Enumerable.Range(0,abilityButtons.Count).Select(i=>abilityButtons[i]))b.gameObject.SetActive(false);}
}
public sealed class UnitStatusDisplay:UnityEngine.Component
{
    public bool IsUsed {get;set;}public Unit? unit {get;set;}
    public UnityEngine.Transform healthContainer {get;set;}=new();
    public UnityEngine.Transform typeContainer {get;set;}=new();
    public UnityEngine.SpriteRenderer healthBg {get;set;}=new();
    public TMPro.TextMeshPro healthLabel {get;set;}=new();
    public UnityEngine.SpriteRenderer typeBg {get;set;}=new();
    public UnityEngine.SpriteRenderer typeOutline {get;set;}=new();
    public UnityEngine.SpriteRenderer typeIcon {get;set;}=new();
    public UnityEngine.SpriteRenderer typeIconShadow {get;set;}=new();
    public UnityEngine.SpriteRenderer peaceIcon {get;set;}=new();
    public UnityEngine.GameObject invisibleUnitIndicator {get;set;}=new();
    public UnitState? LastState;public bool ThrowState;public int PoolReturns,StateCalls;public bool ProductionHealthCallbacks;public UnityEngine.Color NativePlayerColor=new(0.2f,0.4f,0.8f,1);
    public UnitStatusDisplay()
    {
        healthContainer.SetParent(transform,false);typeContainer.SetParent(transform,false);
        healthBg.transform.SetParent(healthContainer,false);healthLabel.transform.SetParent(healthContainer,false);
        foreach(var renderer in new[]{typeBg,typeOutline,typeIcon,typeIconShadow})renderer.transform.SetParent(typeContainer,false);
        peaceIcon.transform.SetParent(transform,false);invisibleUnitIndicator.transform.SetParent(transform,false);
    }
    void NativeStatusOperation(Action action)
    {
        object? scope=null;Exception? error=null;
        if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.StatusChanging(this,out scope);
        try{action();}catch(Exception e){error=e;throw;}
        finally{if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.StatusFinished(error,scope);}
    }
    public void SetState(UnitState state)=>NativeStatusOperation(()=> {StateCalls++;if(ThrowState)throw new InvalidOperationException("Synthetic native status failure");
        if(ProductionHealthCallbacks){Deception.PolyModAdapter.MimicPresentationUi.HealthState(ref state);SetTypeSprite(new UnityEngine.Sprite("Status icon "+(state.passengerUnit??state).type));}
        LastState=state;healthLabel.text=((state.health+9)/10).ToString();healthLabel.color=state.health<50?new(1,0,0,1):new(1,1,1,1);
        if(ProductionHealthCallbacks)Deception.PolyModAdapter.MimicPresentationUi.HealthColor(this,state);});
    public void SetUnit(Unit value){if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.StatusReturning(this);unit=value;}
    public void Show(){IsUsed=true;gameObject.SetActive(true);}
    public void ReturnToPool()
    {
        // Native pooling keeps unit and sprite fields for reuse. A private badge
        // must be removed before this object can receive a new owner.
        if(MapRenderer.ProductionCallbacks)Deception.PolyModAdapter.MimicMapInspection.StatusReturning(this);
        PoolReturns++;IsUsed=false;gameObject.SetActive(false);invisibleUnitIndicator.SetActive(false);transform.SetParent(MapRenderer.Pool,false);
    }
    public void SetTypeSprite(UnityEngine.Sprite sprite){typeIcon.sprite=sprite;typeIconShadow.sprite=sprite;}
    public void RefreshColor()=>NativeStatusOperation(()=>typeBg.color=NativePlayerColor);
}
public sealed class UIRoundButton_UI2:UIBasicComponent
{
    public UnityEngine.UI.Image icon {get;set;}=new();
    public string Text {get;set;}="";public UnityEngine.RectTransform IconContainer {get;set;}=new();
    public UnityEngine.UI.Image BG {get;set;}=new(){sprite=new UnityEngine.Sprite("Native circle")};
    public UIRoundButton_UI2(){icon.transform.SetParent(transform,false);BG.transform.SetParent(transform,false);IconContainer.SetParent(transform,false);}
    public UnityEngine.Sprite? Sprite;public float SpriteScale;public string? InfoHeader,InfoDescription;
    public void SetSprite(UnityEngine.Sprite sprite,float scale)
    {if(BranchTechFixture.Failure=="button-sprite")throw new InvalidOperationException("Synthetic button-sprite failure");Sprite=sprite;icon.sprite=sprite;SpriteScale=scale;}
}
public sealed class InteractionBar:UnityEngine.Component
{
    public void RefreshTileOptions(){}
    public Unit? unit {get;set;}
    public Tile? tile {get;set;}
    public Il2CppSystem.Collections.Generic.List<UIRoundButton> buttons {get;set;}=new();
    public UnityEngine.RectTransform bottomBarButtonContainer {get;set;}=new();
    public PolytopiaScrollRect? bottomBarScrollRect {get;set;}
    // The observed desktop native policy is false. Other native display modes
    // can route even forceToBottom=false into the bottom container.
    public bool NativeBottomButtons;
    public UIDuplicatedSprites duplicatedSprites {get;set;}=new();
    public UISpriteDuplicator.SpriteDuplicationData spriteDuplicationData {get;set;}
    public InteractionBar(){spriteDuplicationData=new(){container=duplicatedSprites,source=UISpriteDuplicator.NativePreviewSource()};}
    public WorldCoordinates coordinates {get;set;}=new(){X=5,Y=5};
    public QuickActions quickActions {get;set;}=new(){Position=new(12,0,9),IsShowing=true};
    public string headerString {get;set;}="City"; public string descriptionString {get;set;}="";
    public TestText header {get;set;}=new(); public TestText description {get;set;}=new();
    public int ConfirmationCalls,Returns,CloseCalls; public string? LastConfirmed;
    public int ImprovementCalls;public BuildCommand? LastImprovement;
    public int RefreshCalls;public bool CityShowOpensWheel=true;
    public UnitData.Type[] NativeCityRoster=new[]{UnitData.Type.warrior};
    public string NativeCityHeader="Native city",NativeCityDescription="Native city details";
    public bool NativePortraitLayout,showInfoIcon=true;
    public UnityEngine.UI.Image infoIcon {get;set;}=new();
    public UnityEngine.Rect NativePortraitRect,NativeInfoRect;
    public bool IsShowing {get;set;}=true;public bool BottomBarActive,LastShowInstant,LastShowForce;public int ShowCalls,LayoutPasses,LayoutButtonCount;
    public void Show(bool instant,bool force)
    {
        ShowCalls++;LastShowInstant=instant;LastShowForce=force;
        if(unit is not null)
        {
            if(IsShowing&&!force)return;
            if(PopupManager.ProductionCallbacks)Deception.PolyModAdapter.MimicPresentationUi.BeforeBarRefresh(this);
            LayoutPasses++;LayoutButtonCount=buttons.Count;BottomBarActive=buttons.Count>0;IsShowing=true;duplicatedSprites.LastSprite=null;
            if(NativePortraitLayout)
            {
                UIManager.Instance.SpriteDuplicator.RenderSprites(spriteDuplicationData);
                NativePortraitRect=UIUtils.GetActualRect(duplicatedSprites.rectTransform,true);
                NativeInfoRect=UIUtils.GetActualRect(duplicatedSprites.rectTransform,false);
                var icon=infoIcon.rectTransform.rect;
                infoIcon.rectTransform.anchoredPosition=new(NativeInfoRect.x+NativeInfoRect.width/2-icon.width/2,NativeInfoRect.y+NativeInfoRect.height/2+icon.height/2);
                infoIcon.gameObject.SetActive(showInfoIcon);
            }
            if(PopupManager.ProductionCallbacks)Deception.PolyModAdapter.MimicPresentationUi.BarReady(this);
            return;
        }
        // Native Show presents existing contents. It does not reconstruct city
        // options or overwrite the title left by a custom submenu.
        Returns++;IsShowing=true;header.text=headerString;description.text=descriptionString;
        if(CityShowOpensWheel&&quickActions.HasButtons())quickActions.Show(instant);
    }
    public void Refresh()
    {
        if(!Deception.PolyModAdapter.RecruitmentWheelUi.PreserveWheel(this))return;
        RefreshCalls++;ClearButtons();
        if(unit is not null)return;
        headerString=NativeCityHeader;descriptionString=NativeCityDescription;
        foreach(var type in NativeCityRoster)
        {
            var data=new UnitData(type);var button=CreateRoundBottomBarButton("Ordinary "+RecruitmentCatalog.Label(type.ToString()),false);
            button.Cost=data.cost;button.OnClicked=new((_,_)=>ClickedSpawnUnit(new TrainCommand{Type=type,Coordinates=coordinates},data));
        }
        SetupButtonNavigation();Show(false,false);
    }
    public void RefreshUnitOptions(){}public void RefreshPCBarInternal(){}
    public void OnClose(){CloseCalls++;}
    public void OnSelectionCleared() { }
    public void OnTileSelected(Tile tile) { }
    public void OnUnitSelected(Unit unit) { }
    public void Reset() { }
    public void DeInit() { }
    public void OnDestroy() { }
    public void ClearButtons()
    {
        if(PopupManager.ProductionCallbacks)Deception.PolyModAdapter.MimicInspectionControls.NativeClear(this);
        if(PopupManager.SmuggleCallbacks)Deception.PolyModAdapter.MimicSmuggleUi.NativeClear(this);
        for(int i=0;i<buttons.Count;i++)UnityEngine.Object.Destroy(buttons[i].NativeGameObject());
        buttons.Clear();quickActions.Clear();
    }
    public int NavigationCalls;public void SetupButtonNavigation(){NavigationCalls++;}
    public void SortButtons(){}
    public bool LastForceToBottom;public UIRoundButton CreateRoundBottomBarButton(string text,bool forceToBottom)
    {
        LastForceToBottom=forceToBottom;var button=new UIRoundButton{Text=text};
        if(forceToBottom||NativeBottomButtons)
        {button.transform.SetParent(bottomBarButtonContainer,false);buttons.Add(button);}
        else
        {button.transform.SetParent(quickActions.buttonContainer,false);quickActions.AddButton(button);}
        return button;
    }
    public UnitPopup GetPopulatedUnitPopup(UnitData data)=>new();
    public void ClickedSpawnUnit(CommandBase command,UnitData data){ConfirmationCalls++;LastConfirmed=data.type.ToString();}
    public void ClickedImprovement(BuildCommand command)
    {
        ImprovementCalls++;LastImprovement=command;Deception.PolyModAdapter.MimicSmuggleUi.PopupScope? scope=null;Exception? failure=null;
        if(PopupManager.SmuggleCallbacks)Deception.PolyModAdapter.MimicSmuggleUi.BeforeClicked(command,out scope);
        try
        {
            // Audited native path: factory first, then ordinary setters/cost,
            // building renderer and Show. It never uses GetIconPopupWithData.
            var data=GameManager.GameState.GameLogicData.GetImprovementData(command.Type);var popup=PopupManager.GetIconPopup();
            popup.SetHeader(data.type.ToString());popup.SetDescription("Native harvest description");popup.TopRightCost=data.cost;
            popup.Building=new();popup.Building.SetBuildingData(data,default,default);popup.Show();PopupManager.PopupShowing=true;
        }
        catch(Exception e){failure=e;throw;}
        finally{if(scope is not null)Deception.PolyModAdapter.MimicSmuggleUi.AfterClicked(failure,scope);}
    }
}
public class UIBasicComponent:UnityEngine.Component
{
    public UnityEngine.RectTransform rectTransform=>(UnityEngine.RectTransform)transform;
    public UIBasicComponent(){gameObject=new UnityEngine.RectTransform().gameObject;rectTransform.rect=new(100,120);}
    public float GetWidth()=>rectTransform.rect.width;public float GetHeight()=>rectTransform.rect.height;
    public UIBasicComponent SetSize(float w,float h){rectTransform.rect=new(w,h);return this;}
    public void SetPosition(float x,float y){rectTransform.anchoredPosition=new(x,y);}
}
public static class BranchTechFixture
{
    public static string? Failure;
    public static readonly System.Collections.Generic.List<UnityEngine.UI.Image> Images=new();
    public static readonly System.Collections.Generic.List<UIRoundButton_UI2> Buttons=new();
    public static readonly System.Collections.Generic.List<string> LocalizedKeys=new();
    public static void Reset(){Failure=null;Images.Clear();Buttons.Clear();LocalizedKeys.Clear();}
}
public static class UILibrary
{
    public static UIBasicComponent NewEmptyComponent(UnityEngine.RectTransform? parent){var result=new UIBasicComponent();result.transform.SetParent(parent,false);return result;}
    public static UIRoundButton_UI2 NewRoundButton(UnityEngine.RectTransform? parent)
    {var button=new UIRoundButton_UI2();button.rectTransform.SetParent(parent,false);BranchTechFixture.Buttons.Add(button);return button;}
}
public static class Localization
{
    // Deliberately unique fixture values prove that the adapter passes native
    // localized title/description through; these are not a localization audit.
    public static string Get(string key,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>? args)
    {
        BranchTechFixture.LocalizedKeys.Add(key);
        if(key=="ui.labels.dcp_disguise")return "(Disguise)";
        if(BranchTechFixture.Failure=="localization")throw new InvalidOperationException("Synthetic localization failure");
        return "Localized["+key+"]";
    }
}
public static class TechPopupContent
{
    public static UIRoundButton_UI2 AddInfoPopup(UIRoundButton_UI2 button,string header,string description)
    {
        if(BranchTechFixture.Failure=="info-popup")throw new InvalidOperationException("Synthetic info-popup failure");
        button.InfoHeader=header;button.InfoDescription=description;return button;
    }
    public static void SetUnitData(UIRoundButton_UI2 button,UnitData data){}
    public static TechData? LastRendered;
    public static UIBasicComponent CreateTechDataContent(TechData data){LastRendered=data;return new();}
}
public sealed class TechPopup:Il2CppSystem.Object { public void SetTechData(TechData data) { } }
public sealed class TechItem:Il2CppSystem.Object
{
    public static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<UnityEngine.RectTransform> GetUnlockItems(TechData techData,PlayerState playerState,bool onlyPickFirstItem)=>new(Array.Empty<UnityEngine.RectTransform>());
}
namespace Deception.PolyModAdapter
{
    internal static class Entry
    {
        public static readonly List<(string Kind,string Message)> Notes=new();
        internal static void Note(string kind,string message)=>Notes.Add((kind,message));
    }
    internal static class RecruitmentHooks
    {
        internal const string TT="Polytopia.Data.TechData+Type",GLD="Polytopia.Data.GameLogicData";
        public static bool Registered=true,Valid=true,Eligible=true;
        public static readonly Dictionary<string,RecruitmentForm> Forms=new(StringComparer.Ordinal);
        public static HashSet<string> Roster=new(StringComparer.Ordinal){"warrior"};
        public static MethodInfo ClickedSpawn=typeof(InteractionBar).GetMethod("ClickedSpawnUnit")!;
        public static string Id(object data)=>((UnitData)data).type.ToString();
        public static bool OwnsDeception(object player)=>Registered&&((PlayerState)player).Deception;
        public static HashSet<string> AppearanceRoster(object state,object player)=>Roster;
        public static HashSet<string> CityAppearanceRoster(object state,object player,object coordinates)=>Roster;
        public static object Data(object state,string id)=>new UnitData(Enum.Parse<UnitData.Type>(id));
        public static object NewCommand(object player,object coords,string id)=>new TrainCommand{PlayerId=((PlayerState)player).Id,Type=Enum.Parse<UnitData.Type>(id),Coordinates=(WorldCoordinates)coords};
        public static bool NativeValid(object state,object command,out string reason)
        {bool v=Valid&&GameManager.LocalPlayer.Currency>=RecruitmentCatalog.Cost;reason=v?"":"Native recruitment refused in fixture";return v;}
        public static bool CheckEligibility(object state,object command,out string reason)
        {
            string id=((TrainCommand)command).Type.ToString();
            bool v=Eligible&&OwnsDeception(GameManager.LocalPlayer)&&Forms.TryGetValue(id,out var form)&&Roster.Contains(form.NativeId);
            reason=v?"":"Native roster refused in fixture";return v;
        }
    }
}
