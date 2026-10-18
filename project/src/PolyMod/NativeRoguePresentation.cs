using System;
using System.Collections.Generic;
using System.Linq;

namespace Deception.PolyModAdapter
{
    // Shares the map adapter's exact part ownership/restoration machinery.
    // Tint is truthful public state; neither it nor the bolts survive fog,
    // native pool reassignment, conversion, return, or a replacement map.
    internal static partial class MimicMapInspection
    {
        sealed class RogueView
        {internal View Frame=null!;internal object? Root;internal readonly List<object> Bolts=new();}
        static readonly Dictionary<long,RogueView> RogueViews=new();
        static object? RogueSprite,RogueTexture;
        static void RestoreRogue(long key)
        {
            if(!RogueViews.TryGetValue(key,out var v))return;
            try{RestoreParts(v.Frame);if(Native.UnityAlive(v.Root))Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),v.Root,false);}
            catch(Exception e){Note("rogue-restore-error",e);RemoveRogue(key);}
        }
        static void RemoveRogue(long key)
        {
            if(!RogueViews.Remove(key,out var v))return;
            try{RestoreParts(v.Frame);}
            finally{if(Native.UnityAlive(v.Root)){try{Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),v.Root,false);}finally{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,v.Root);}}}
        }
        internal static object SubvertIcon()=>RogueBolt();
        static object RogueBolt()
        {
            if(Native.UnityAlive(RogueSprite)&&Native.UnityAlive(RogueTexture))return RogueSprite!;
            const int size=64;var points=new[]{(38f,4f),(15f,34f),(29f,34f),(21f,60f),(50f,25f),(36f,25f)};
            var pixels=new byte[size*size*4];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                bool inside=false;for(int i=0,j=points.Length-1;i<points.Length;j=i++)
                    if((points[i].Item2>y+.5f)!=(points[j].Item2>y+.5f)&&x+.5f<(points[j].Item1-points[i].Item1)*(y+.5f-points[i].Item2)/(points[j].Item2-points[i].Item2)+points[i].Item1)inside=!inside;
                int at=(y*size+x)*4;pixels[at]=255;pixels[at+1]=pixels[at+2]=31;pixels[at+3]=(byte)(inside?255:0);
            }
            object? texture=null,sprite=null;
            try
            {
                texture=Activator.CreateInstance(Native.T("UnityEngine.Texture2D"),size,size,Enum.Parse(Native.T("UnityEngine.TextureFormat"),"RGBA32"),false)!;
                Native.Set(texture,"filterMode",Enum.Parse(Native.T("UnityEngine.FilterMode"),"Bilinear"));Native.Set(texture,"wrapMode",Enum.Parse(Native.T("UnityEngine.TextureWrapMode"),"Clamp"));
                var bytes=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
                Native.Invoke(Native.M("UnityEngine.Texture2D","LoadRawTextureData",bytes.FullName!),texture,Activator.CreateInstance(bytes,new object[]{pixels}));
                Native.Invoke(Native.M("UnityEngine.Texture2D","Apply","System.Boolean","System.Boolean"),texture,false,true);
                sprite=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),null,
                    texture,Activator.CreateInstance(Native.T("UnityEngine.Rect"),0f,0f,(float)size,(float)size),Activator.CreateInstance(Native.T("UnityEngine.Vector2"),.5f,.5f),100f,0u,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"))!;
                RogueTexture=texture;RogueSprite=sprite;return sprite;
            }
            catch{if(Native.UnityAlive(sprite))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,sprite);if(Native.UnityAlive(texture))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,texture);throw;}
        }
        static void RefreshRogue(object unit)
        {
            long key=Native.Pointer(unit);
            try
            {
                var state=Native.Static("GameManager","GameState");var map=Native.Static("MapRenderer","Current");var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");
                var body=Native.UnityAlive(unit)?Native.Get(unit,"UnitState"):null;
                if(state is null||map is null||client is null||player is null||body is null||!NativeRegroupSession.Ready(state)||
                    !NativeRogueRules.IsRogue(state,body)||!(bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!||(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!||
                    (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!||
                    !(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,Convert.ToByte(Native.Get(player,"Id")))!||
                    !Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,Convert.ToUInt32(Native.Get(body,"id"))),unit))
                {RemoveRogue(key);return;}
                object?[] lookup={Convert.ToUInt32(Native.Get(body,"id")),null};
                if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,lookup)!||!Native.Same(lookup[1],body)){RemoveRogue(key);return;}
                var at=Native.Get(body,"coordinates")!;var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
                if(tile is null||!Native.Same(Native.Get(tile,"unit"),body)||!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,Convert.ToByte(Native.Get(player,"Id")))!){RemoveRogue(key);return;}
                uint id=Convert.ToUInt32(Native.Get(body,"id"));var root=Native.Get(unit,"transform")!;var container=Native.Get(unit,"spriteContainer")!;
                if(RogueViews.TryGetValue(key,out var old)&&(old.Frame.UnitId!=id||!Native.Same(old.Frame.Root,root)||!Native.Same(old.Frame.Container,container))){RemoveRogue(key);old=null;}
                if(old is null){if(RogueViews.Count>=8192)throw new InvalidOperationException("Rogue view bound exceeded");RogueViews[key]=old=new(){Frame=new(){Unit=unit,UnitId=id,Container=container,Root=root}};}
                var parts=Collect(old.Frame).Where(p=>p.Main).ToList();
                if(Views.TryGetValue(key,out var custom)&&Native.UnityAlive(custom.Child)&&(bool)Native.Get(custom.Child!,"activeSelf")!)
                    foreach(var renderer in custom.ArtRenderers.Where(Native.UnityAlive))
                        parts.Add(new(){Renderer=renderer,Transform=Native.Get(renderer,"transform")!,Sprite=Native.Get(renderer,"sprite")!,Main=true});
                foreach(var part in parts){part.Original=ReadColor(part);part.Applied=part.Original.A==0?part.Original:new(part.Original.R+(1-part.Original.R)*.16f,part.Original.G*.83f,part.Original.B*.83f,part.Original.A);}
                foreach(var part in parts)WriteColor(part,part.Applied);old.Frame.Parts.AddRange(parts);
                var source=parts.FirstOrDefault(p=>!p.Poly&&p.Original.A!=0)??parts.FirstOrDefault(p=>p.Original.A!=0);
                if(source is null){if(Native.UnityAlive(old.Root))Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),old.Root,false);return;}
                float left=float.PositiveInfinity,right=float.NegativeInfinity,bottom=float.PositiveInfinity,top=float.NegativeInfinity,z=0;
                foreach(var p in parts.Where(p=>p.Original.A!=0))
                {
                    var bounds=Native.Get(p.Renderer,p.Poly?"Bounds":"bounds")!;var c=PointOf(Native.Get(bounds,"center")!);var dimensions=PointOf(Native.Get(bounds,"size")!);
                    left=Math.Min(left,c.X-dimensions.X*.5f);right=Math.Max(right,c.X+dimensions.X*.5f);bottom=Math.Min(bottom,c.Y-dimensions.Y*.5f);top=Math.Max(top,c.Y+dimensions.Y*.5f);z=c.Z;
                }
                var center=new Point((left+right)*.5f,(bottom+top)*.5f,z);var size=new Point(right-left,top-bottom,0);
                // Restore the accepted 0.3.69 neighborhood of the sprite.
                // Native board units are much smaller than one Unity world
                // unit: the old fixed .7 scale spanned neighboring tiles.
                // The preserved native warrior frame is ~.343 world units
                // high. Use one .35 reference for all units, with the old
                // .35 multiplier raised slightly for width and height.
                const float boltWidth=.35f*.39f,boltHeight=.35f*.43f;
                // Keep both bolts at identical world scale. Bring the pair
                // toward the head and above the status disk, independently
                // of the native body's facing or inherited transform scale.
                int layer=Native.Int(Native.Get(source.Renderer,source.Poly?"SortingLayer":"sortingLayerID"));int order=Native.Int(Native.Get(source.Renderer,source.Poly?"SortingOrder":"sortingOrder"));
                if(!Native.UnityAlive(old.Root))
                {
                    old.Root=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Deception Rogue status"})!;
                    Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),old.Root,false);
                    for(int i=0;i<2;i++)
                    {
                        var go=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Deception Rogue bolt"})!;
                        Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),Native.Get(go,"transform"),Native.Get(old.Root,"transform"),false);
                        var type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"))!;
                        old.Bolts.Add(Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),go,type)!,"UnityEngine.SpriteRenderer"));
                    }
                }
                for(int i=0;i<old.Bolts.Count;i++)
                {
                    var r=old.Bolts[i];var t=Native.Get(r,"transform")!;
                    Native.Set(Native.Get(r,"gameObject")!,"layer",Native.Get(Native.Get(source.Renderer,"gameObject")!,"layer"));
                    Native.Set(r,"sprite",RogueBolt());Native.Set(r,"color",new Rgba(1f,1f,1f,1f).Box());
                    Native.Set(r,"sharedMaterial",Native.Get(source.Renderer,source.Poly?"SharedMaterial":"sharedMaterial"));Native.Set(r,"sortingLayerID",layer);Native.Set(r,"sortingOrder",checked(order+20));
                    Native.Set(t,"localPosition",new Point(center.X+(i==0?-1f:1f)*size.X*.27f,center.Y+size.Y*.64f,center.Z).Box());
                    Native.Set(t,"localScale",new Point(boltWidth*(i==0?1:-1),boltHeight,1).Box());
                }
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),old.Root,true);
            }
            catch(Exception e){Note("rogue-view-error",e);RemoveRogue(key);}
        }
    }
}
