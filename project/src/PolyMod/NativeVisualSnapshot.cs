using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // A bounded value snapshot of a temporary, unregistered native visual.
    // Same clone/SetupStatic/SkinUnit pipeline as UIUnitRenderer.CreateUnit;
    // never bind a UnitState, register a map Unit, or retain the native clone.
    internal static class NativeVisualSnapshot
    {
        internal sealed record Layer(object Sprite,object? Material,object Color,float X,float Y,float Z,
            float ScaleX,float ScaleY,float ScaleZ,float Rotation,bool Outline,bool Tintable,int Order);
        const string UT="Polytopia.Data.UnitData+Type",Tribe="PolytopiaBackendBase.Common.TribeType",Skin="PolytopiaBackendBase.Common.SkinType";
        internal static IReadOnlyList<Layer> Capture(string form,LedgerAppearance appearance)
        {
            object Member(string type,string name)
            {
                var t=Native.T(type);if(!t.IsEnum||!Enum.IsDefined(t,name))throw new InvalidOperationException("Unknown visual profile "+type+"."+name);
                return Enum.Parse(t,name,false);
            }
            var tribe=Member(Tribe,appearance.TribeVisual.Tribe);var skin=Member(Skin,appearance.TribeVisual.Skin);
            var birth=Member(Tribe,appearance.BirthVisual.Tribe);var birthSkin=Member(Skin,appearance.BirthVisual.Skin);
            var prefab=Native.Invoke(Native.M("PrefabManager","GetPrefab",UT,Tribe,Skin),null,Native.EnumValue(UT,form),tribe,skin);
            if(!Native.UnityAlive(prefab))throw new InvalidOperationException("Disguise visual prefab is missing.");
            object? clone=null,ownedGameObject=null;
            try
            {
                clone=Native.Invoke(Native.M("UnityEngine.Object","Instantiate","UnityEngine.Object"),null,prefab);
                if(Native.Same(clone,prefab)){clone=null;throw new InvalidOperationException("Disguise visual clone aliases its borrowed prefab.");}
                if(!Native.UnityAlive(clone))throw new InvalidOperationException("Disguise visual clone is invalid.");
                ownedGameObject=Native.Get(Native.As(clone!,"UnityEngine.Component"),"gameObject");
                clone=Native.As(clone!,"Unit");
                var go=Native.Get(clone,"gameObject")!;var root=Native.Get(clone,"transform")!;
                var type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("SkinVisualsReference"));
                var component=Native.Invoke(Native.M("UnityEngine.GameObject","GetComponent","Il2CppSystem.Type"),go,type);
                if(!Native.UnityAlive(component))throw new InvalidOperationException("Disguise visual has no serialized skin component.");
                component=Native.As(component!,"SkinVisualsReference");
                var transient=Activator.CreateInstance(Native.T("SkinVisualsTransientData"))!;
                Native.Invoke(Native.M("SkinVisualsTransientData","SetupStatic",Tribe,Skin,"System.Int32",Tribe,Skin),transient,tribe,skin,appearance.SourceColor,birth,birthSkin);
                Native.Invoke(Native.M("SkinVisualsRenderer","SkinUnit","SkinVisualsReference","SkinVisualsTransientData","System.Boolean","System.Int32"),null,component,transient,true,-1);
                Native.Invoke(Native.M("Unit","UpdateHeadScale","System.Boolean"),clone,(bool)Native.Static("ChipMunkManager","active")!);
                var result=new List<Layer>();var seen=new HashSet<long>();
                (float X,float Y,float Z) Position(object transform,float x,float y,float z)
                {
                    var p=Activator.CreateInstance(Native.T("UnityEngine.Vector3"),x,y,z)!;
                    var world=Native.Invoke(Native.M("UnityEngine.Transform","TransformPoint","UnityEngine.Vector3"),transform,p);
                    var value=Native.Invoke(Native.M("UnityEngine.Transform","InverseTransformPoint","UnityEngine.Vector3"),root,world)!;
                    float Axis(int n){float f=Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Vector3","get_Item","System.Int32"),value,n));if(!float.IsFinite(f))throw new InvalidOperationException("Nonfinite native visual transform.");return f;}
                    return(Axis(0),Axis(1),Axis(2));
                }
                void Add(object? renderer,bool poly,bool outline,bool tintable)
                {
                    if(!Native.UnityAlive(renderer))return;
                    if(poly&&Native.UnityAlive(Native.Get(renderer!,"spriteRenderer"))){Add(Native.Get(renderer!,"spriteRenderer"),false,outline,tintable);return;}
                    if(!seen.Add(Native.Pointer(renderer!)))return;
                    if(result.Count>=128)throw new InvalidOperationException("Disguise visual part limit exceeded.");
                    var transform=Native.Get(renderer!,"transform")!;
                    if(!(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),transform,root)!)throw new InvalidOperationException("Disguise visual part escaped its clone.");
                    if(!outline&&!(bool)Native.Get(Native.Get(renderer!,"gameObject")!,"activeInHierarchy")!)return;
                    var sprite=Native.Get(renderer!,poly?"Sprite":"sprite");if(!Native.UnityAlive(sprite))return;
                    var origin=Position(transform,0,0,0);var ax=Position(transform,1,0,0);var ay=Position(transform,0,1,0);var az=Position(transform,0,0,1);
                    float xx=ax.X-origin.X,xy=ax.Y-origin.Y,yx=ay.X-origin.X,yy=ay.Y-origin.Y;
                    float sx=MathF.Sqrt(xx*xx+xy*xy),sy=MathF.Sqrt(yx*yx+yy*yy),sz=az.Z-origin.Z;
                    if(!float.IsFinite(sx)||!float.IsFinite(sy)||sx<=0||sy<=0||sz<=0||
                        Math.Abs(ax.Z-origin.Z)+Math.Abs(ay.Z-origin.Z)+Math.Abs(az.X-origin.X)+Math.Abs(az.Y-origin.Y)>.0001f||
                        Math.Abs(xx*yx+xy*yy)>sx*sy*.0001f)throw new InvalidOperationException("Unsupported native visual plane.");
                    if(xx*yy-xy*yx<0)sy=-sy;
                    float angle=MathF.Atan2(xy,xx)*180/MathF.PI;
                    var material=poly?null:Native.Get(renderer!,"sharedMaterial");
                    result.Add(new(sprite!,material,Native.Get(renderer!,poly?"Color":"color")!,origin.X,origin.Y,origin.Z,sx,sy,sz,angle,outline,tintable,Native.Int(Native.Get(renderer!,poly?"SortingOrder":"sortingOrder"))));
                }
                var parts=Native.Get(component,"visualParts")!;int count=Native.Int(Native.Get(parts,"Count"));
                if(count<=0||count>64)throw new InvalidOperationException("Disguise visual part array is invalid.");
                foreach(var part in Native.Items(parts))foreach(bool outline in new[]{false,true})
                {
                    var union=Native.Get(part,outline?"outlineRenderer":"renderer");if(union is null)continue;
                    bool tintable=(bool)Native.Get(part,"tintable")!;
                    Add(Native.Get(union,"spriteRenderer"),false,outline,tintable);Add(Native.Get(union,"polytopiaSpriteRenderer"),true,outline,tintable);
                }
                if(!result.Exists(p=>!p.Outline))throw new InvalidOperationException("Disguise visual has no main sprites.");
                return result.OrderBy(x=>x.Outline?0:1).ThenBy(x=>x.Order).ToArray();
            }
            finally
            {
                if(Native.UnityAlive(ownedGameObject))
                {
                    try{Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),ownedGameObject,false);}
                    finally{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,ownedGameObject);}
                }
                else if(Native.UnityAlive(clone))
                    Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,clone);
            }
        }
    }
}
