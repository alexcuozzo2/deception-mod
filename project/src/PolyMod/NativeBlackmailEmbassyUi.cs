using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native CityStatusNameContainer.SetCity obtains the foreign capital's
    // embassy income from CalculateWork(tile,state,viewer,level), then uses
    // SetWork's existing star label. Add only this viewer's Blackmail income
    // in that exact display scope; the real economy remains untouched.
    internal static class NativeBlackmailEmbassyUi
    {
        internal sealed class Scope
        {
            internal readonly Scope? Previous;internal readonly long Generation;
            internal object? State,Client,Map,Tile,Data,City,Container;
            internal NativeGameStateEnvelope.Snapshot? Snapshot;
            internal byte Viewer,Victim;internal uint Turn;internal bool Closed;
            internal Scope(Scope? previous,long generation){Previous=previous;Generation=generation;}
        }
        [ThreadStatic] static Scope? Current;
        static long Generation;
        static readonly HashSet<string> Errors=new();
        internal static void Clear(){Generation++;Current=null;Errors.Clear();}
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("CityStatusNameContainer","SetCity","City"),typeof(NativeBlackmailEmbassyUi),nameof(BeforeCity),null,nameof(Finished));
            Native.Hook(h,Native.M("TileDataExtensions","CalculateWork","TileData","GameState","PlayerState","System.Int32"),typeof(NativeBlackmailEmbassyUi),null,nameof(Income));
        }
        static void Error(Exception e){if(Errors.Count<8&&Errors.Add(e.Message))Entry.Note("blackmail-embassy-ui-error",e.ToString());}
        static bool Active(object? o)=>Native.UnityAlive(o)&&(bool)Native.Get(Native.Get(o!,"gameObject")!,"activeInHierarchy")!;
        static bool View(out object state,out object client,out object map,out byte viewer)
        {
            state=client=map=null!;viewer=0;if(!NativeRegroupSession.Enabled||!RecruitmentHooks.Registered)return false;
            var s=Native.Static("GameManager","GameState");var c=Native.Static("GameManager","Client");var m=Native.Static("MapRenderer","Current");var p=Native.Static("GameManager","LocalPlayer");
            if(s is null||c is null||p is null||!Active(m)||!NativeRegroupSession.Ready(s)||!Native.Same(s,Native.Get(c,"GameState"))||
                !(bool)Native.Get(c,"IsReady")!||(bool)Native.Get(c,"IsReplay")!||(bool)Native.Get(c,"IsSpectating")!||(bool)Native.Static("HotSeatOverlay","IsActive")!)return false;
            viewer=Convert.ToByte(Native.Get(p,"Id"));if(viewer is 0 or 255||!(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!)return false;
            state=s;client=c;map=m!;return true;
        }
        static bool Visible(object state,object map,object tile,object data,byte viewer)
        {
            var at=Native.Get(data,"coordinates")!;
            return Active(tile)&&!(bool)Native.Get(tile,"IsHidden")!&&Native.Same(Native.Get(tile,"Data"),data)&&
                Native.Same(Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at),data)&&
                Native.Same(Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,at),tile)&&
                (bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),data,Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!&&
                (bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,viewer)!;
        }
        public static void BeforeCity(object __instance,object __0,out Scope __state)
        {
            // Invalid nested producers fence, rather than borrow, outer scope.
            __state=Current=new(Current,Generation);
            try
            {
                if(!Active(__instance)||!Active(__0)||!View(out var state,out var client,out var map,out byte viewer))return;
                var tile=Native.Get(__0,"Tile");var data=tile is null?null:Native.Get(tile,"Data");
                if(tile is null||data is null||!Native.Same(Native.Get(tile,"Improvement"),__0)||!Visible(state,map,tile,data,viewer))return;
                byte victim=Convert.ToByte(Native.Get(data,"owner"));var owner=Native.Get(__0,"Owner");
                if(victim is 0 or 255||victim==viewer||owner is null||Native.Int(Native.Get(owner,"Id"))!=victim)return;
                var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null||!NativeBlackmail.At(state,data).Any(r=>r.Blackmailer==viewer))return;
                var s=__state;s.State=state;s.Client=client;s.Map=map;s.Viewer=viewer;s.Victim=victim;s.Tile=tile;s.Data=data;s.City=__0;s.Container=__instance;s.Snapshot=snapshot;s.Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));
            }catch(Exception e){__state.State=null;Error(e);}
        }
        static bool Valid(Scope s)
        {
            if(s.Closed||s.Generation!=Generation||!ReferenceEquals(Current,s)||s.State is null||s.Data is null||s.Tile is null||!View(out var state,out var client,out var map,out byte viewer)||
                !Native.Same(s.State,state)||!Native.Same(s.Client,client)||!Native.Same(s.Map,map)||s.Viewer!=viewer||s.Turn!=Convert.ToUInt32(Native.Get(state,"CurrentTurn"))||
                !ReferenceEquals(s.Snapshot,NativeGameStateEnvelope.Get(state))||s.Victim!=Native.Int(Native.Get(s.Data,"owner"))||!Visible(state,map,s.Tile,s.Data,viewer)||
                !Active(s.City)||!Active(s.Container)||!Native.Same(Native.Get(s.Tile,"Improvement"),s.City)||!Native.Same(Native.Get(s.City!,"Tile"),s.Tile)||!Native.Same(Native.Get(s.Container!,"city"),s.City))return false;
            var owner=Native.Get(s.City!,"Owner");return owner is not null&&Native.Int(Native.Get(owner,"Id"))==s.Victim;
        }
        public static void Income(object __0,object __1,object __2,bool __runOriginal,ref int __result)
        {
            try
            {
                if(!__runOriginal||NativeBlackmail.ReadingGrossIncome||Current is not Scope s||!Native.Same(s.State,__1)||!Native.Same(s.Data,__0)||__2 is null||Native.Int(Native.Get(__2,"Id"))!=s.Viewer||!Valid(s))return;
                int count=NativeBlackmail.At(__1,__0).Where(r=>r.Blackmailer==s.Viewer&&r.Victim==s.Victim).Sum(r=>r.Layers);
                __result=(int)Math.Min(int.MaxValue,(long)__result+count);
            }catch(Exception e){Error(e);}
        }
        public static Exception? Finished(Exception? __exception,Scope? __state)
        {
            if(__state is not Scope s||s.Closed)return __exception;s.Closed=true;
            if(s.Generation==Generation&&ReferenceEquals(Current,s)){Current=s.Previous;while(Current is Scope prior&&prior.Closed)Current=prior.Previous;}
            return __exception;
        }
    }
}
