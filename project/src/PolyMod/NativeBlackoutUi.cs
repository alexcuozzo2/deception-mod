using System;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Only asks the game's renderer to refresh ordinary fog. No custom fog
    // materials, public Blackout markers or relay traces are created.
    internal static class NativeBlackoutUi
    {
        static NativeGameStateEnvelope.Snapshot? Last;
        static NativeLedgerOwnership.WeakIdentity? State,Map;
        static NativeLedgerOwnership.WeakIdentity? RenderedMap,RenderedData,RenderedClient;
        static long Generation;
        [ThreadStatic] static RenderScope? Rendering;
        internal sealed class RenderScope
        {
            internal readonly object Map,Data;
            internal readonly RenderScope? Previous;
            internal readonly long Generation;
            internal bool Completed,Invalidated;
            internal RenderScope(object map,object data){Map=map;Data=data;Previous=Rendering;Generation=NativeBlackoutUi.Generation;}
        }
        static LedgerBlackout[] Previous=Array.Empty<LedgerBlackout>();
        static byte Viewer;
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("GameManager","Update"),typeof(NativeBlackoutUi),null,nameof(Tick));
            // SelectTile is the native input dispatcher, including the normal
            // fog puff, coordinate-specific cloud SFX and selection cleanup.
            // Keep that path; guard the actual selection sinks underneath it.
            Native.Hook(h,Native.M("ClientInteraction","SelectTileInternal","Tile"),typeof(NativeBlackoutUi),nameof(BeforeTile));
            Native.Hook(h,Native.M("ClientInteraction","SelectUnit","Unit"),typeof(NativeBlackoutUi),nameof(BeforeUnit));
            Native.Hook(h,Native.M("ClientInteraction","UpdateAfterMapRefresh"),typeof(NativeBlackoutUi),null,nameof(AfterRefresh));
            _=Native.M("MapRenderer","get_IsRendered");
            Native.Hook(h,Native.M("MapRenderer","RenderMap","MapData"),typeof(NativeBlackoutUi),nameof(BeforeRender),nameof(AfterRender),nameof(RenderFinished));
            foreach(string method in new[]{"Clear","OnDestroy"})
                Native.Hook(h,Native.M("MapRenderer",method),typeof(NativeBlackoutUi),nameof(RendererClearing));
        }
        static void ClearSnapshot(){Last=null;State=Map=null;Previous=Array.Empty<LedgerBlackout>();Viewer=0;}
        internal static void Clear(){Generation++;Rendering=null;RenderedMap=RenderedData=RenderedClient=null;ClearSnapshot();}
        static bool Active(object? map)=>Native.UnityAlive(map)&&(bool)Native.Get(Native.Get(map!,"gameObject")!,"activeInHierarchy")!;
        public static void BeforeRender(object __instance,object __0,bool __runOriginal,out RenderScope? __state)
        {
            __state=null;if(!__runOriginal)return;
            if(RenderedMap?.Matches(__instance)==true){RenderedMap=RenderedData=RenderedClient=null;ClearSnapshot();}
            Rendering=__state=new(__instance,__0);
        }
        public static void AfterRender(RenderScope? __state,bool __runOriginal)
        {if(__state is not null&&__runOriginal)__state.Completed=true;}
        public static Exception? RenderFinished(Exception? __exception,RenderScope? __state)
        {
            if(__state is null)return __exception;
            bool current=ReferenceEquals(Rendering,__state)&&__state.Generation==Generation;
            if(ReferenceEquals(Rendering,__state))Rendering=__state.Generation==Generation?__state.Previous:null;
            if(!current||__exception is not null||!__state.Completed||__state.Invalidated)return __exception;
            for(var parent=Rendering;parent is not null;parent=parent.Previous)
                if(Native.Same(parent.Map,__state.Map))return __exception;
            try
            {
                // IsRendered alone only proves a nonempty allocated tile array;
                // native RenderMap fills it synchronously. Admit only its exact
                // successful completion, never an Update during construction.
                object? state=Native.Static("GameManager","GameState"),client=Native.Static("GameManager","Client");
                if(state is null||client is null||!Active(__state.Map)||!Native.Same(__state.Map,Native.Static("MapRenderer","Current"))||
                    !Native.Same(__state.Data,Native.Get(state,"Map"))||!Native.Same(state,Native.Get(client,"GameState"))||
                    !(bool)Native.Get(__state.Map,"IsRendered")!)return __exception;
                RenderedMap=new(__state.Map);RenderedData=new(__state.Data);RenderedClient=new(client);
                if(Context(out var observed,out byte viewer)&&Native.Same(observed,state))Observe(state,__state.Map,viewer,false);
            }
            catch(Exception e){RenderedMap=RenderedData=RenderedClient=null;ClearSnapshot();Entry.Note("blackout-render-refresh-error",e.Message);}
            return __exception;
        }
        public static void RendererClearing(object __instance)
        {
            // A nested clear invalidates a pending completion too. Clearing a
            // different preview renderer must not retire the active map.
            if(RenderedMap?.Matches(__instance)==true){Clear();return;}
            for(var scope=Rendering;scope is not null;scope=scope.Previous)
                if(Native.Same(scope.Map,__instance))scope.Invalidated=true;
        }
        static bool Context(out object state,out byte viewer)
        {
            state=null!;viewer=0;if(Native.Static("GameManager","instance") is null)return false;
            object? current=Native.Static("GameManager","GameState"),player=Native.Static("GameManager","LocalPlayer");
            if(current is null||player is null||NativeGameStateEnvelope.Get(current) is null)return false;
            state=current;viewer=Convert.ToByte(Native.Get(player,"Id"));return true;
        }
        static bool Hidden(object state,object coordinates,byte actor)=>NativeBlackout.Covered(state,actor,NativeSpyNetwork.At(coordinates));
        public static bool BeforeTile(object? __0)=>__0 is null||!Context(out var state,out byte viewer)||!Hidden(state,Native.Get(Native.Get(__0,"Data")!,"coordinates")!,viewer);
        public static bool BeforeUnit(object? __0)=>__0 is null||!Context(out var state,out byte viewer)||!Hidden(state,Native.Get(Native.Get(__0,"UnitState")!,"coordinates")!,viewer);
        public static void AfterRefresh(object __instance)
        {
            if(!Context(out var state,out byte viewer))return;
            object? unit=Native.Get(__instance,"selectedUnit"),tile=Native.Get(__instance,"selectedTile");
            if(unit is not null&&Hidden(state,Native.Get(Native.Get(unit,"UnitState")!,"coordinates")!,viewer)||
                tile is not null&&Hidden(state,Native.Get(Native.Get(tile,"Data")!,"coordinates")!,viewer))
                Native.Invoke(Native.M("ClientInteraction","ClearSelection"),__instance);
        }
        public static void Tick()
        {
            try
            {
                if(!Context(out var state,out byte viewer)){Clear();return;}
                object? map=Native.Static("MapRenderer","Current"),client=Native.Static("GameManager","Client");
                if(Rendering is not null||!Active(map)||client is null||RenderedMap?.Matches(map!)!=true||
                    RenderedData?.Matches(Native.Get(state,"Map")!)!=true||
                    RenderedClient?.Matches(client)!=true||!Native.Same(state,Native.Get(client,"GameState"))||
                    !(bool)Native.Get(client,"IsReady")!||!(bool)Native.Get(map!,"IsRendered")!)return;
                Observe(state,map!,viewer,true);
                // Native refresh triggers UpdateAfterMapRefresh, which retires
                // covered stale selections before they can expose controls.
            }
            catch(Exception e){Clear();Entry.Note("blackout-render-refresh-error",e.Message);}
        }
        static void Observe(object state,object map,byte viewer,bool refresh)
        {
            var snapshot=NativeGameStateEnvelope.Get(state)!;
            if(ReferenceEquals(Last,snapshot)&&State?.Matches(state)==true&&Map?.Matches(map)==true&&viewer==Viewer)return;
            var marks=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Blackouts??Array.Empty<LedgerBlackout>();
            // A replay/seek may replace GameState while keeping the exact map
            // and current client. Keep its prior fog baseline for real deltas.
            bool prior=Map?.Matches(map)==true;
            bool changed=prior&&(viewer!=Viewer||!marks.SequenceEqual(Previous));
            Last=snapshot;State=new(state);Map=new(map);Viewer=viewer;Previous=marks;
            // The initial native render already applied the current fog. Never
            // manufacture a pending refresh solely because a map was reopened.
            if(refresh&&changed)Native.Set(map,"shouldRefresh",true);
        }
    }
}
