using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;
namespace Deception.PolyModAdapter
{
    // Cosmetic consumers only. Native unit ownership remains the commander;
    // the remembered player supplies public skin and badge colors.
    internal static class NativeRogueVisuals
    {
        internal sealed record Frame(Frame? Previous,int Depth,int Generation,object? Unit,bool ColorOnly=false);
        [ThreadStatic] static Frame? Current;[ThreadStatic] static int Depth;
        static int Generation;static bool Noted;
        static object? LastState,LastMap,LastClient,LastPlayer,LastSnapshot;
        static uint LastTurn;static int LastCommand;static uint[] LastIds=Array.Empty<uint>();
        static void Note(Exception e){if(!Noted){Noted=true;Entry.Note("rogue-cosmetics",e.Message);}}
        internal static void Clear(){Generation++;Current=null;Depth=0;Noted=false;LastState=LastMap=LastClient=LastPlayer=LastSnapshot=null;LastIds=Array.Empty<uint>();}
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("SkinVisualsTransientData","SetupForUnit","Polytopia.Data.GameLogicData","UnitState","PlayerState"),typeof(NativeRogueVisuals),nameof(BeforeSkin));
            Native.Hook(h,Native.M("UnitStatusDisplay","SetUnit","Unit"),typeof(NativeRogueVisuals),nameof(BeforeStatusUnit),null,nameof(Finished));
            Native.Hook(h,Native.M("UnitStatusDisplay","RefreshColor"),typeof(NativeRogueVisuals),nameof(BeforeStatusColor),null,nameof(Finished));
            Native.Hook(h,Native.M("Unit","UpdateStatusDisplay","MapRenderContext"),typeof(NativeRogueVisuals),nameof(BeforeStatusUpdate),null,nameof(Finished));
            Native.Hook(h,Native.M("ClientPlayerExtensions","GetPlayerColor","PlayerState","GameState"),typeof(NativeRogueVisuals),nameof(BadgeColor));
            Native.Hook(h,Native.M("Unit","get_Owner"),typeof(NativeRogueVisuals),null,nameof(Owner));
            Native.Hook(h,Native.M("GameManager","Update"),typeof(NativeRogueVisuals),null,nameof(Tick));
        }
        static bool State(object state)=>RecruitmentHooks.Registered&&NativeRegroupSession.Ready(state)&&Native.Same(state,Native.Static("GameManager","GameState"));
        static bool Exact(object state,object body)
        {
            if(!State(state)||Native.Int(Native.Get(body,"health"))<=0)return false;
            object?[] a={Convert.ToUInt32(Native.Get(body,"id")),null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,a)!&&Native.Same(a[1],body);
        }
        internal static object ArtOwner(object state,object body,object owner)
        {
            if(!Exact(state,body)||Native.Int(Native.Get(owner,"Id"))!=Native.Int(Native.Get(body,"owner")))return owner;
            var row=NativeRogueRules.Row(state,body);if(row?.Rogue is not LedgerRogue rogue)return owner;
            object?[] p={rogue.Owner,null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,p)!&&p[1] is object player?player:owner;
        }
        public static void BeforeSkin(object __0,object __1,ref object __2)
        {
            try
            {
                var state=Native.Static("GameManager","GameState");
                if(state is not null&&Native.Same(__0,Native.Get(state,"GameLogicData")))__2=ArtOwner(state,__1,__2);
            }catch(Exception e){Note(e);}
        }
        public static void BeforeStatusUnit(object __0,out Frame __state)=>__state=Current=new(Current,++Depth,Generation,__0);
        public static void BeforeStatusColor(object __instance,out Frame __state)
        {__state=Current=new(Current,++Depth,Generation,null);try{__state=Current=__state with{Unit=Native.Get(__instance,"unit")};}catch(Exception e){Note(e);}}
        public static void BeforeStatusUpdate(object __instance,out Frame __state)=>__state=Current=new(Current,++Depth,Generation,__instance,true);
        public static void BadgeColor(ref object __0,object __1)
        {
            try
            {
                var f=Current;var map=Native.Static("MapRenderer","Current");
                if(f is null||f.Generation!=Generation||f.Depth!=Depth||!Native.UnityAlive(f.Unit)||!Native.UnityAlive(map))return;
                var body=Native.Get(f.Unit!,"UnitState");
                if(body is null||!Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,Convert.ToUInt32(Native.Get(body,"id"))),f.Unit))return;
                __0=ArtOwner(__1,body,__0);
            }catch(Exception e){Note(e);}
        }
        public static void Owner(object __instance,ref object __result)
        {
            try
            {
                var f=Current;var state=Native.Static("GameManager","GameState");var map=Native.Static("MapRenderer","Current");
                if(f is null||f.ColorOnly||f.Generation!=Generation||f.Depth!=Depth||state is null||!Native.UnityAlive(__instance)||!Native.Same(f.Unit,__instance)||!Native.UnityAlive(map))return;
                var body=Native.Get(__instance,"UnitState");if(body is null||!Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,Convert.ToUInt32(Native.Get(body,"id"))),__instance))return;
                __result=ArtOwner(state,body,__result);
            }catch(Exception e){Note(e);}
        }
        public static Exception? Finished(Exception? __exception,Frame? __state)
        {
            if(__state is Frame f&&f.Generation==Generation&&ReferenceEquals(Current,f)){Current=f.Previous;Depth=f.Depth-1;}
            return __exception;
        }
        // Native reactions can render before the new ledger/owner/action frame
        // is fully published. Rebuild only affected Rogue visuals once native
        // processing settles, using the completed immutable frame. No game
        // state, action flags, economic siege or true poison is changed here.
        public static void Tick()
        {
            try
            {
                var state=Native.Static("GameManager","GameState");var map=Native.Static("MapRenderer","Current");
                var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");
                if(state is null||!State(state)||!Native.UnityAlive(map)||client is null||player is null||
                    !Native.Same(Native.Get(client,"GameState"),state)||!(bool)Native.Get(client,"IsReady")!||
                    (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!)return;
                if((bool)Native.Get(client,"IsWaitingForCommand")!||(bool)Native.Get(Native.Get(client,"ActionManager")!,"IsProcessing")!)return;
                byte viewer=Convert.ToByte(Native.Get(player,"Id"));
                if(!(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!)return;
                var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return;
                uint turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));int command=Native.Int(Native.Get(state,"CurrentCommand"));
                bool sameWorld=Native.Same(LastState,state)&&Native.Same(LastMap,map)&&Native.Same(LastClient,client);
                if(sameWorld&&Native.Same(LastPlayer,player)&&ReferenceEquals(LastSnapshot,snapshot)&&LastTurn==turn&&LastCommand==command)return;
                var ids=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Units.Where(r=>r.Alive&&r.Rogue is not null).Select(r=>r.NativeId).ToArray();
                var refresh=sameWorld?ids.Concat(LastIds).Distinct().ToArray():ids;
                // Commit the observation before calling native display code;
                // nested callbacks cannot recurse or repeatedly repaint it.
                LastState=state;LastMap=map;LastClient=client;LastPlayer=player;LastSnapshot=snapshot;LastTurn=turn;LastCommand=command;LastIds=ids;
                int generation=Generation;
                foreach(uint id in refresh)
                {
                    if(generation!=Generation||!Native.Same(state,Native.Static("GameManager","GameState"))||!Native.Same(map,Native.Static("MapRenderer","Current")))break;
                    object?[] lookup={id,null};if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,lookup)!||lookup[1] is not object body)continue;
                    var visual=Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,id);
                    if(!Native.UnityAlive(visual)||!Native.Same(Native.Get(visual!,"UnitState"),body)||!(bool)Native.Invoke(Native.M("Unit","GetVisible"),visual)!||(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),visual)!)continue;
                    var data=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),Native.Get(body,"coordinates"));
                    if(data is null||!Native.Same(Native.Get(data,"unit"),body)||!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,viewer)!)continue;
                    Native.Invoke(Native.M("Unit","UpdateObject"),visual);
                    if(generation!=Generation||!Native.Same(state,Native.Static("GameManager","GameState"))||!Native.Same(map,Native.Static("MapRenderer","Current"))||
                        !Native.Same(client,Native.Static("GameManager","Client"))||!Native.Same(player,Native.Static("GameManager","LocalPlayer")))break;
                    var tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,Native.Get(body,"coordinates"));
                    if(Native.UnityAlive(tile)&&Native.Same(Native.Get(tile!,"Data"),data))Native.Invoke(Native.M("Tile","RenderImprovement"),tile);
                }
            }catch(Exception e){Note(e);}
        }
    }
}
