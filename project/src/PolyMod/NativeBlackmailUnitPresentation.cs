using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Public presentation of a currently visible compromised unit. The tint
    // composes after Rogue art and restores before it; no UnitState is edited.
    internal static partial class MimicMapInspection
    {
        sealed class BlackmailView
        {internal View Frame=null!;internal object? Root,Label,Star,LabelSource;}
        static readonly Dictionary<long,BlackmailView> BlackmailViews=new();
        static readonly Dictionary<uint,int> BlackmailCounts=new();
        static object? BlackmailState,BlackmailMap;
        static NativeGameStateEnvelope.Snapshot? BlackmailSnapshot;
        static int BlackmailCommand;static uint BlackmailTurn;
        static void RestoreBlackmail(long key)
        {
            if(!BlackmailViews.TryGetValue(key,out var v))return;
            try{RestoreBlackmailParts(v.Frame);if(Native.UnityAlive(v.Root))Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),v.Root,false);}
            catch(Exception e){Note("blackmail-unit-restore-error",e);RemoveBlackmail(key);}
        }
        static void RestoreBlackmailParts(View view)
        {
            // Cloak pulse writes native alpha directly without Unit.SetAlpha.
            // Preserve that newer alpha while removing only our owned RGB.
            foreach(var part in view.Parts)
            {
                if(!PartOwned(view,part))continue;var current=ReadColor(part);
                if(current.R==part.Applied.R&&current.G==part.Applied.G&&current.B==part.Applied.B&&current.A!=part.Applied.A)
                {part.Original=part.Original.Alpha(current.A);part.Applied=part.Applied.Alpha(current.A);}
            }
            RestoreParts(view);
        }
        static void RemoveBlackmail(long key)
        {
            if(!BlackmailViews.Remove(key,out var v))return;
            try{RestoreBlackmailParts(v.Frame);}
            finally{if(Native.UnityAlive(v.Root)){Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),v.Root,false);Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,v.Root);}}
        }
        internal static void HideBlackmailMarkers(){foreach(var view in BlackmailViews.Values)if(Native.UnityAlive(view.Root))Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),view.Root,false);}
        static void ClearBlackmail()
        {
            foreach(long key in BlackmailViews.Keys.ToArray())RemoveBlackmail(key);
            BlackmailCounts.Clear();BlackmailState=BlackmailMap=null;BlackmailSnapshot=null;
        }
        static int BlackmailCount(object state,object map,uint id)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return -1;
            int command=Native.Int(Native.Get(state,"CurrentCommand"));uint turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));
            if(!Native.Same(state,BlackmailState)||!Native.Same(map,BlackmailMap)||!ReferenceEquals(snapshot,BlackmailSnapshot)||command!=BlackmailCommand||turn!=BlackmailTurn)
            {
                BlackmailCounts.Clear();
                // A temporarily unproductive city pauses payments, not the
                // compromise. Keep its public tint and show zero income.
                foreach(var row in NativeBlackmail.Contracts(state))
                {
                    var body=NativeBlackmail.ResolveUnit(state,row);if(body is null)continue;
                    BlackmailCounts[Convert.ToUInt32(Native.Get(body,"id"))]=0;
                }
                foreach(var row in NativeBlackmail.All(state))
                {
                    var body=NativeBlackmail.ResolveUnit(state,row);if(body is null)continue;
                    uint nativeId=Convert.ToUInt32(Native.Get(body,"id"));BlackmailCounts.TryGetValue(nativeId,out int count);
                    BlackmailCounts[nativeId]=checked(count+row.Layers);
                }
                BlackmailState=state;BlackmailMap=map;BlackmailSnapshot=snapshot;BlackmailCommand=command;BlackmailTurn=turn;
            }
            return BlackmailCounts.TryGetValue(id,out int layers)?layers:-1;
        }
        static void RefreshBlackmail(object unit)
        {
            long key=Native.Pointer(unit);
            try
            {
                var state=Native.Static("GameManager","GameState");var map=Native.Static("MapRenderer","Current");var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");
                var body=Native.UnityAlive(unit)?Native.Get(unit,"UnitState"):null;
                if(state is null||!Native.UnityAlive(map)||client is null||player is null||body is null||Native.Int(Native.Get(body,"health"))<=0||!NativeRegroupSession.Ready(state)||
                    !Native.Same(state,Native.Get(client,"GameState"))||!(bool)Native.Get(client,"IsReady")!||
                    (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!||(bool)Native.Static("HotSeatOverlay","IsActive")!||
                    !(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,Convert.ToByte(Native.Get(player,"Id")))!||
                    !(bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!||(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!)
                {RemoveBlackmail(key);return;}
                uint id=Convert.ToUInt32(Native.Get(body,"id"));int count=BlackmailCount(state,map!,id);
                if(count<0||!Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,id),unit)){RemoveBlackmail(key);return;}
                object?[] lookup={id,null};
                if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,lookup)!||!Native.Same(lookup[1],body)){RemoveBlackmail(key);return;}
                var at=Native.Get(body,"coordinates")!;var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
                var visualTile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,at);
                if(tile is null||!Native.UnityAlive(visualTile)||!Native.Same(Native.Get(visualTile!,"Data"),tile)||(bool)Native.Get(visualTile!,"IsHidden")!||
                    !Native.Same(Native.Get(tile,"unit"),body)||!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,Convert.ToByte(Native.Get(player,"Id")))!){RemoveBlackmail(key);return;}
                var root=Native.Get(unit,"transform")!;var container=Native.Get(unit,"spriteContainer")!;
                if(BlackmailViews.TryGetValue(key,out var old)&&(old.Frame.UnitId!=id||!Native.Same(old.Frame.Root,root)||!Native.Same(old.Frame.Container,container))){RemoveBlackmail(key);old=null;}
                if(old is null){if(BlackmailViews.Count>=8192)throw new InvalidOperationException("Blackmail unit view bound exceeded");BlackmailViews[key]=old=new(){Frame=new(){Unit=unit,UnitId=id,Container=container,Root=root}};}
                var parts=Collect(old.Frame).Where(p=>p.Main).ToList();
                if(Views.TryGetValue(key,out var custom)&&Native.UnityAlive(custom.Child)&&(bool)Native.Get(custom.Child!,"activeSelf")!)
                    foreach(var renderer in custom.ArtRenderers.Where(Native.UnityAlive))parts.Add(new(){Renderer=renderer,Transform=Native.Get(renderer,"transform")!,Sprite=Native.Get(renderer,"sprite")!,Main=true});
                foreach(var part in parts){part.Original=ReadColor(part);part.Applied=part.Original.A==0?part.Original:new(part.Original.R*.62f,part.Original.G*.62f,part.Original.B*.62f,part.Original.A);}
                foreach(var part in parts)WriteColor(part,part.Applied);old.Frame.Parts.AddRange(parts);
                var source=parts.FirstOrDefault(p=>!p.Poly&&p.Original.A>0)??parts.FirstOrDefault(p=>p.Original.A>0);
                if(source is null)return;
                var status=Native.Get(unit,"statusDisplay");
                var label=status is null?null:Native.Get(status,"healthLabel");
                if(!Native.UnityAlive(label)||!Native.Same(Native.Get(status!,"unit"),unit))return;
                if(!Native.UnityAlive(old.Root)||!Native.Same(old.LabelSource,label))
                {
                    if(Native.UnityAlive(old.Root))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,old.Root);
                    old.Root=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Deception Blackmail unit income"})!;
                    Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),old.Root,false);
                    old.Label=Native.As(Native.Invoke(Native.M("UnityEngine.Object","Instantiate","UnityEngine.Object"),null,label)!,"TMPro.TextMeshPro");
                    if(Native.Same(old.Label,label))throw new InvalidOperationException("Blackmail label clone aliases native health");
                    old.LabelSource=label;
                    Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),Native.Get(old.Label,"gameObject"),true);
                    Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),Native.Get(old.Label,"transform"),Native.Get(old.Root,"transform"),false);
                    var starObject=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Blackmail income star"})!;
                    Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),Native.Get(starObject,"transform"),Native.Get(old.Root,"transform"),false);
                    var type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"))!;
                    old.Star=Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),starObject,type)!,"UnityEngine.SpriteRenderer");
                }
                // Fixed board height, independent of unit/species/head scale.
                // Borrow native numeric font and native star artwork only.
                Native.Set(old.Label!,"text",count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Native.Set(old.Label!,"color",new Rgba(1,1,1,1).Box());
                var labelFrame=Native.Get(old.Label!,"transform")!;var nativeScale=PointOf(Native.Get(Native.Get(label!,"transform")!,"lossyScale")!);
                Native.Set(labelFrame,"localScale",new Point(Math.Abs(nativeScale.X),Math.Abs(nativeScale.Y),Math.Abs(nativeScale.Z)).Box());
                Native.Set(labelFrame,"localPosition",new Point(.022f,0,0).Box());Native.Set(labelFrame,"localRotation",Native.Static("UnityEngine.Quaternion","identity"));
                var star=BlackmailArtAssets.IncomeStar();Native.Set(old.Star!,"sprite",star);Native.Set(old.Star!,"color",new Rgba(1,1,1,1).Box());
                var starSize=PointOf(Native.Get(Native.Get(star,"bounds")!,"size")!);float starScale=.045f/Math.Max(starSize.X,starSize.Y);
                Native.Set(Native.Get(old.Star!,"transform")!,"localScale",new Point(starScale,starScale,starScale).Box());Native.Set(Native.Get(old.Star!,"transform")!,"localPosition",new Point(-.025f,0,0).Box());
                var tileCenter=PointOf(Native.Get(visualTile!,"VisualCenter")!);
                var frame=Native.Get(old.Root!,"transform")!;Native.Set(frame,"position",new Point(tileCenter.X,tileCenter.Y-.14f,tileCenter.Z).Box());Native.Set(frame,"localRotation",Native.Get(Native.Get(label!,"transform")!,"rotation"));
                int layer=Native.Int(Native.Get(source.Renderer,source.Poly?"SortingLayer":"sortingLayerID")),order=Native.Int(Native.Get(source.Renderer,source.Poly?"SortingOrder":"sortingOrder"))+25;
                foreach(var renderer in new[]{old.Star!,Native.Get(old.Label!,"renderer")!})
                {Native.Set(Native.Get(renderer,"gameObject")!,"layer",Native.Get(Native.Get(source.Renderer,"gameObject")!,"layer"));Native.Set(renderer,"sortingLayerID",layer);Native.Set(renderer,"sortingOrder",order);}
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),old.Root,true);
            }
            catch(Exception e){Note("blackmail-unit-view-error",e);RemoveBlackmail(key);}
        }
    }
}
