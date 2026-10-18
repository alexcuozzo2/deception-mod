using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // These scopes belong only to native text producers. CalculateWork outside
    // them retains its original economy/AI result; settlement still pays once.
    internal static class NativeBlackmailIncomeUi
    {
        internal sealed class Scope
        {
            internal readonly Scope? Previous;
            internal readonly long Generation;
            internal readonly bool Info;
            internal readonly long Serial=System.Threading.Interlocked.Increment(ref NextScope);
            internal object? State,Client,Map,Data,Tile,Improvement,City,Container;
            internal NativeGameStateEnvelope.Snapshot? Snapshot;
            internal byte Viewer,Owner;internal uint Turn;internal int Count,Net;
            internal bool Closed,HadWork;
            internal Scope(Scope? previous,long generation,bool info){Previous=previous;Generation=generation;Info=info;}
        }
        internal sealed class HeaderScope
        {
            internal readonly HeaderScope? Previous;internal readonly long Generation;internal readonly long Serial=System.Threading.Interlocked.Increment(ref NextScope);
            internal object? State,Client,Map,DataMap,Bar;internal NativeGameStateEnvelope.Snapshot? Snapshot;
            internal byte Viewer;internal uint Turn;internal bool Closed;
            internal HeaderScope(HeaderScope? previous,long generation){Previous=previous;Generation=generation;}
        }
        sealed record IncomeStamp(object State,object Client,object Map,object DataMap,byte Viewer,NativeGameStateEnvelope.Snapshot Snapshot,int Net,int Command,uint Turn);
        static IncomeStamp? LastIncome;static bool Refreshing;
        static long Generation,NextScope;
        [ThreadStatic] static HeaderScope? Header;
        [ThreadStatic] static Scope? Current;
        static readonly HashSet<string> Errors=new();
        internal static void Clear(){Generation++;Current=null;Header=null;LastIncome=null;Errors.Clear();}
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("BuildingUtils","GetInfo","PolytopiaBackendBase.Common.SkinType","Polytopia.Data.ImprovementData","ImprovementState","PlayerState","TileData"),
                typeof(NativeBlackmailIncomeUi),nameof(BeforeInfo),null,nameof(Finished));
            Native.Hook(h,Native.M("CityStatusNameContainer","SetCity","City"),typeof(NativeBlackmailIncomeUi),nameof(BeforeCity),null,nameof(Finished));
            Native.Hook(h,Native.M("TileDataExtensions","CalculateWork","TileData","GameState"),typeof(NativeBlackmailIncomeUi),null,nameof(InfoWork));
            Native.Hook(h,Native.M("TileDataExtensions","CalculateWork","TileData","GameState","PlayerState","System.Int32"),typeof(NativeBlackmailIncomeUi),null,nameof(CityWork));
            Native.Hook(h,Native.M("CityStatusNameContainer","SetWork","System.Int32"),typeof(NativeBlackmailIncomeUi),null,nameof(WorkSet));
            Native.Hook(h,Native.M("ResourceBar","RefreshIncome","System.Byte"),typeof(NativeBlackmailIncomeUi),nameof(BeforeHeader),null,nameof(HeaderFinished));
            Native.Hook(h,Native.M("ResourceDataUtils","CalculateIncomeFor","GameState","System.Byte"),typeof(NativeBlackmailIncomeUi),null,nameof(HeaderIncome));
            Native.Hook(h,Native.M("GameManager","Update"),typeof(NativeBlackmailIncomeUi),null,nameof(Tick));
        }
        static void Error(Exception e){if(Errors.Count<8&&Errors.Add(e.Message))Entry.Note("blackmail-income-ui-error",e.ToString());}
        static bool Active(object? component)=>Native.UnityAlive(component)&&(bool)Native.Get(Native.Get(component!,"gameObject")!,"activeInHierarchy")!;
        static bool View(out object state,out object client,out object map,out byte viewer)
        {
            state=client=map=null!;viewer=0;if(!NativeRegroupSession.Enabled||!RecruitmentHooks.Registered)return false;
            var s=Native.Static("GameManager","GameState");var c=Native.Static("GameManager","Client");var m=Native.Static("MapRenderer","Current");var p=Native.Static("GameManager","LocalPlayer");
            if(s is null||c is null||p is null||!Active(m)||!NativeRegroupSession.Ready(s)||!Native.Same(s,Native.Get(c,"GameState"))||
                !(bool)Native.Get(c,"IsReady")!||(bool)Native.Get(c,"IsReplay")!||(bool)Native.Get(c,"IsSpectating")!||
                (bool)Native.Static("HotSeatOverlay","IsActive")!)return false;
            viewer=Convert.ToByte(Native.Get(p,"Id"));if(viewer is 0 or 255||!(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!)return false;
            state=s;client=c;map=m!;return true;
        }
        static bool Visible(object state,object map,object data,byte viewer,out object tile)
        {
            tile=null!;var at=Native.Get(data,"coordinates")!;
            if(!Native.Same(Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at),data)||
                !(bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),data,Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!||
                !(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,viewer)!)return false;
            var t=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,at);
            if(!Active(t)||(bool)Native.Get(t!,"IsHidden")!||!Native.Same(Native.Get(t!,"Data"),data))return false;
            tile=t!;return true;
        }
        static void Capture(Scope s,object? data,object? improvement,object? owner)
        {
            if(data is null||improvement is null||owner is null||!View(out var state,out var client,out var map,out byte viewer)||
                !Visible(state,map,data,viewer,out var tile)||!Native.Same(Native.Get(data,"improvement"),improvement))return;
            byte victim=Convert.ToByte(Native.Get(data,"owner"));
            if(victim is 0 or 255||Native.Int(Native.Get(owner,"Id"))!=victim)return;
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return;
            int count=NativeBlackmail.At(state,data).Sum(r=>r.Layers);if(count==0)return;
            s.State=state;s.Client=client;s.Map=map;s.Data=data;s.Tile=tile;s.Improvement=improvement;s.Snapshot=snapshot;
            s.Viewer=viewer;s.Owner=victim;s.Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));s.Count=count;
        }
        public static void BeforeInfo(object __1,object __2,object __3,object __4,out Scope __state)
        {
            // Invalid nested render contexts fence the enclosing scope too.
            __state=Current=new(Current,Generation,true);
            try
            {
                if(__1 is null||Native.EnumName(Native.Get(__1,"type")!)!="city")return;
                Capture(__state,__4,__2,__3);
            }catch(Exception e){__state.State=null;Error(e);}
        }
        public static void BeforeCity(object __instance,object __0,out Scope __state)
        {
            __state=Current=new(Current,Generation,false);
            try
            {
                if(!Active(__instance)||!Active(__0))return;
                var tile=Native.Get(__0,"Tile");var data=tile is null?null:Native.Get(tile,"Data");
                Capture(__state,data,Native.Get(__0,"State"),Native.Get(__0,"Owner"));
                if(__state.State is null||!Native.Same(tile,__state.Tile)||!Native.Same(Native.Get(tile!,"improvement"),__0)){__state.State=null;return;}
                __state.City=__0;__state.Container=__instance;
            }catch(Exception e){__state.State=null;Error(e);}
        }
        static bool Valid(Scope s)
        {
            if(s.Closed||s.Generation!=Generation||s.State is null||s.Data is null||!ReferenceEquals(Current,s)||
                !View(out var state,out var client,out var map,out byte viewer)||!Native.Same(s.State,state)||!Native.Same(s.Client,client)||!Native.Same(s.Map,map)||s.Viewer!=viewer||
                s.Turn!=Convert.ToUInt32(Native.Get(state,"CurrentTurn"))||!ReferenceEquals(s.Snapshot,NativeGameStateEnvelope.Get(state))||
                s.Owner!=Native.Int(Native.Get(s.Data,"owner"))||!Native.Same(s.Improvement,Native.Get(s.Data,"improvement"))||
                !Visible(state,map,s.Data,viewer,out var tile)||!Native.Same(tile,s.Tile)||NativeBlackmail.At(state,s.Data).Sum(r=>r.Layers)!=s.Count)return false;
            return s.Info||(Active(s.Container)&&Active(s.City)&&Native.Same(Native.Get(s.City!,"Tile"),tile)&&
                Native.Same(Native.Get(tile,"improvement"),s.City)&&Native.Same(Native.Get(s.Container!,"city"),s.City));
        }
        static void Project(object tile,object state,bool info,ref int income)
        {
            try
            {
                if(NativeBlackmail.ReadingGrossIncome||Current is not Scope s||(Header is HeaderScope header&&!header.Closed&&header.Serial>s.Serial)||s.Info!=info||!Native.Same(s.Data,tile)||!Native.Same(s.State,state)||!Valid(s))return;
                // Active layers are already capped to this city's current output.
                income=(int)Math.Max(0,(long)income-s.Count);s.Net=income;s.HadWork=true;
            }catch(Exception e){Error(e);}
        }
        public static void InfoWork(object __0,object __1,bool __runOriginal,ref int __result)
        {if(__runOriginal)Project(__0,__1,true,ref __result);}
        public static void CityWork(object __0,object __1,object __2,bool __runOriginal,ref int __result)
        {
            try{if(__runOriginal&&Current is Scope s&&__2 is not null&&Native.Int(Native.Get(__2,"Id"))==s.Owner)Project(__0,__1,false,ref __result);}
            catch(Exception e){Error(e);}
        }
        public static void WorkSet(object __instance,int __0,bool __runOriginal)
        {
            try
            {
                if(!__runOriginal||Current is not Scope s||s.Info||!s.HadWork||__0!=s.Net||!Native.Same(__instance,s.Container)||!Valid(s))return;
                // Native SetWork hides <=0. Retain the native signed text and
                // star icon so an affected city's zero net is visible.
                var work=Native.Get(__instance,"workContainer");if(Active(__instance)&&Native.UnityAlive(work))
                    Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),Native.Get(work!,"gameObject"),true);
            }catch(Exception e){Error(e);}
        }
        public static void Tick()
        {
            if(Refreshing)return;Refreshing=true;
            try
            {
                if(!View(out var state,out var client,out var map,out byte viewer)||(bool)Native.Get(client,"IsRecap")!)return;
                var actions=Native.Get(client,"ActionManager");
                if(actions is null||(bool)Native.Get(actions,"IsRecap")!||(bool)Native.Get(actions,"IsProcessing")!)return;
                var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return;var dataMap=Native.Get(state,"Map")!;
                var previous=LastIncome;bool same=previous is not null&&Native.Same(previous.State,state)&&Native.Same(previous.Client,client)&&Native.Same(previous.Map,map)&&Native.Same(previous.DataMap,dataMap)&&previous.Viewer==viewer;
                int command=Native.Int(Native.Get(state,"CurrentCommand"));uint turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));
                if(same&&ReferenceEquals(previous!.Snapshot,snapshot)&&previous.Command==command&&previous.Turn==turn)return;
                // Pruning/setup changes the immutable envelope. Only those
                // changes (or a new local viewer) need another relationship scan.
                var rows=NativeBlackmail.All(state);int net=rows.Where(r=>r.Blackmailer==viewer).Sum(r=>r.Layers)-rows.Where(r=>r.Victim==viewer).Sum(r=>r.Layers);
                long generation=Generation;
                if(!same||previous!.Net!=net)Native.Invoke(Native.M("ResourceEvents","IncomeChanged","System.Byte"),null,viewer);
                if(generation==Generation)LastIncome=new(state,client,map,dataMap,viewer,snapshot,net,command,turn);
            }catch(Exception e){Error(e);}
            finally{Refreshing=false;}
        }
        public static void BeforeHeader(object __instance,byte __0,out HeaderScope __state)
        {
            __state=Header=new(Header,Generation);
            try
            {
                if(!Active(__instance)||!View(out var state,out var client,out var map,out byte viewer)||viewer!=__0)return;
                var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return;
                __state.State=state;__state.Client=client;__state.Map=map;__state.DataMap=Native.Get(state,"Map");__state.Bar=__instance;__state.Viewer=viewer;
                __state.Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));__state.Snapshot=snapshot;
            }catch(Exception e){__state.State=null;Error(e);}
        }
        public static void HeaderIncome(object __0,byte __1,bool __runOriginal,ref int __result)
        {
            try
            {
                if(!__runOriginal||Header is not HeaderScope s||s.Closed||s.Generation!=Generation||s.State is null||(Current is Scope city&&!city.Closed&&city.Serial>s.Serial)||!Active(s.Bar)||
                    !Native.Same(__0,s.State)||__1!=s.Viewer||!View(out var state,out var client,out var map,out byte viewer)||
                    viewer!=s.Viewer||!Native.Same(state,s.State)||!Native.Same(client,s.Client)||!Native.Same(map,s.Map)||!Native.Same(s.DataMap,Native.Get(state,"Map"))||
                    s.Turn!=Convert.ToUInt32(Native.Get(state,"CurrentTurn"))||!ReferenceEquals(s.Snapshot,NativeGameStateEnvelope.Get(state)))return;
                var rows=NativeBlackmail.All(state);long net=(long)__result+rows.Where(r=>r.Blackmailer==viewer).Sum(r=>r.Layers)-rows.Where(r=>r.Victim==viewer).Sum(r=>r.Layers);
                __result=(int)Math.Clamp(net,int.MinValue,int.MaxValue);
            }catch(Exception e){Error(e);}
        }
        public static Exception? HeaderFinished(Exception? __exception,HeaderScope? __state)
        {
            if(__state is not HeaderScope s||s.Closed)return __exception;s.Closed=true;
            if(s.Generation==Generation&&ReferenceEquals(Header,s))
            {Header=s.Previous;while(Header is HeaderScope previous&&previous.Closed)Header=previous.Previous;}
            return __exception;
        }
        public static Exception? Finished(Exception? __exception,Scope? __state)
        {
            if(__state is not Scope s||s.Closed)return __exception;s.Closed=true;
            if(s.Generation==Generation&&ReferenceEquals(Current,s))
            {Current=s.Previous;while(Current is Scope previous&&previous.Closed)Current=previous.Previous;}
            return __exception;
        }
    }
}
