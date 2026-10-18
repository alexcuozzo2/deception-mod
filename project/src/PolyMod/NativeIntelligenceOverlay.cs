using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Private, read-only tile-access inspection. Own plain SpriteRenderers borrow
    // native fog sprites; no native fog bit, material, tile or unit is changed.
    internal static class NativeIntelligenceOverlay
    {
        internal readonly record struct KnowledgeGestureContext(long Client,long State,long Map,long Interaction,long Unit,long NativeUnit,long Tile,uint Turn,byte Viewer,byte Owner);
        internal const float TintAlpha=0.55f;
        sealed record Marker(object Tile,object Object,object Renderer);
        static readonly Dictionary<(int X,int Y),Marker> Markers=new();
        static readonly HashSet<string> Errors=new();
        static readonly HashSet<string> WaitReasons=new();
        static NativeLedgerOwnership.WeakIdentity? Selection,Client;
        static NativeLedgerOwnership.WeakIdentity? PreferenceClient,PreferenceMap;
        static readonly HashSet<(byte Viewer,byte Owner)> EnabledPairs=new();
        static object? Root;
        static bool Rendering;
        static long MapKey,StateKey;
        static int Duplicating,LastSubject=-1,LastCount=-1;

        internal static void Install(Harmony h)
        {
            foreach(var method in new[]{Native.M("ClientInteraction","get_selectedTile"),Native.M("Tile","get_Data"),
                Native.M("Tile","get_IsHidden"),Native.M("Tile","get_fogOfWarRenderer"),Native.M("Tile","get_unit"),
                Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),Native.M("MapRenderer","GetUnitInstance","System.UInt32"),
                Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),
                Native.M("PolytopiaSpriteRenderer","get_spriteRenderer"),Native.M("UnityEngine.SpriteRenderer","get_sprite"),
                Native.M("UnityEngine.Renderer","get_sortingLayerID"),Native.M("UnityEngine.Renderer","get_sortingOrder"),
                Native.M("UnityEngine.Transform","get_rotation"),Native.M("UnityEngine.Transform","get_lossyScale")}){_=method;}
            foreach(var method in new[]{Native.M("ClientInteraction","SelectUnit","Unit"),Native.M("ClientInteraction","SelectTile","Tile")})
                Native.Hook(h,method,typeof(NativeIntelligenceOverlay),nameof(Hide),nameof(Selected));
            foreach(string name in new[]{"ClearSelection","DeselectUnit","OnSelectionCleared","OnTurnStarted","OnDisable"})
                Native.Hook(h,Native.M("ClientInteraction",name),typeof(NativeIntelligenceOverlay),nameof(Clear));
            Native.Hook(h,Native.M("GameManager","Update"),typeof(NativeIntelligenceOverlay),null,nameof(Tick));
            foreach(string name in new[]{"ReturnToMenu","OnClientSessionOpened"})
                Native.Hook(h,Native.M("GameManager",name),typeof(NativeIntelligenceOverlay),nameof(Reset));
            Native.Hook(h,Native.M("GameManager","SetReplayClient","ReplayClient"),typeof(NativeIntelligenceOverlay),nameof(Reset));
            Native.Hook(h,Native.M("MapRenderer","OnDestroy"),typeof(NativeIntelligenceOverlay),nameof(MapDestroying));
            Native.Hook(h,Native.M("ClientBase","EndSession"),typeof(NativeIntelligenceOverlay),nameof(Reset));
            Native.Hook(h,Native.M("ClientBase","SetNewLocalPlayerTurnForPassAndPlay","System.Byte"),typeof(NativeIntelligenceOverlay),nameof(Clear));
            foreach(var method in new[]{Native.M("UISpriteDuplicator","RenderSprites","UISpriteDuplicator+SpriteDuplicationData"),
                Native.M("UISpriteDuplicator","RenderSprites","UnityEngine.Transform","UIDuplicatedSprites","System.Single","System.String","UnityEngine.Vector2","System.Boolean")})
                Native.Hook(h,method,typeof(NativeIntelligenceOverlay),nameof(BeforeDuplicate),null,nameof(AfterDuplicate));
        }
        static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
        static bool Alive(object? value)=>value is not null&&Native.UnityAlive(value);
        static void Active(object go,bool value)=>Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,value);
        static void Destroy(object go)=>Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,go);
        static void Error(Exception e){if(Errors.Count<16&&Errors.Add(e.Message))Entry.Note("intelligence-overlay-error",e.ToString());}
        public static void Hide(){try{if(Alive(Root))Active(Root!,false);}catch(Exception e){Error(e);}}
        public static void Clear(){Selection=null;Client=null;Hide();Retire();LastSubject=LastCount=-1;}
        public static void Reset(){Clear();EnabledPairs.Clear();PreferenceClient=PreferenceMap=null;Duplicating=0;}
        public static void MapDestroying(object __instance)
        {
            try
            {
                // Sprite-camera preview maps can temporarily become Current.
                // Only retire the map to which this controller actually bound.
                if(Native.Same(__instance,PreferenceMap?.Target())||Native.Pointer(__instance)==MapKey)Reset();
            }
            catch(Exception e){Reset();Error(e);}
        }
        static void PreferenceScope(object client,object map)
        {
            if(!Native.Same(client,PreferenceClient?.Target())||!Native.Same(map,PreferenceMap?.Target()))
            {EnabledPairs.Clear();PreferenceClient=new(client);PreferenceMap=new(map);}
        }
        // UI inspection is available while waiting as well as on the viewer's
        // own turn. It never grants a command or changes authoritative sight.
        internal readonly record struct ControlContext(long Client,long State,long Map,long Interaction,long Unit,long Tile,uint Turn,byte Viewer,byte Owner);
        internal static bool TryControlContext(object interaction,out ControlContext context)
        {
            context=default;
            try
            {
                if(MimicRegroupUi.Choosing||NativeSpecialActionUi.Choosing||!Native.Same(interaction,Selection?.Target())||
                    !Context(out var state,out var map,out _,out byte viewer,out byte owner))return false;
                object? unit=Native.Get(interaction,"selectedUnit");
                object tile=unit is null?Native.Get(interaction,"selectedTile")!:
                    Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,Native.Get(Native.Get(unit,"UnitState")!,"coordinates"))!;
                PreferenceScope(Client!.Target()!,map);
                context=new(Native.Pointer(Client.Target()!),Native.Pointer(state),Native.Pointer(map),Native.Pointer(interaction),unit is null?0:Native.Pointer(unit),Native.Pointer(tile),
                    Convert.ToUInt32(Native.Get(state,"CurrentTurn")),viewer,owner);
                return true;
            }
            catch(Exception e){Error(e);return false;}
        }
        internal static bool IsEnabled(ControlContext context)=>EnabledPairs.Contains((context.Viewer,context.Owner));
        internal static bool TryToggleControl(object interaction,ControlContext expected)
        {
            if(!TryControlContext(interaction,out var current)||current!=expected)return false;
            var key=(current.Viewer,current.Owner);if(!EnabledPairs.Remove(key))EnabledPairs.Add(key);
            Tick();return true;
        }
        internal static bool TryGestureContext(object interaction,object unit,out KnowledgeGestureContext context)
        {
            context=default;
            try
            {
                if(MimicRegroupUi.Choosing||!Native.Same(interaction,Selection?.Target())||
                    !Native.Same(unit,Native.Get(interaction,"selectedUnit"))||
                    !Context(out var state,out var map,out _,out byte viewer,out byte owner))return false;
                object client=Client!.Target()!,native=Native.Get(unit,"UnitState")!;
                object? manager=Native.Get(client,"ActionManager");
                if(manager is null||(bool)Native.Get(client,"IsWaitingForCommand")!||(bool)Native.Get(manager,"IsProcessing")!)return false;
                object tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,Native.Get(native,"coordinates"))!;
                context=new(Native.Pointer(client),Native.Pointer(state),Native.Pointer(map),Native.Pointer(interaction),Native.Pointer(unit),Native.Pointer(native),Native.Pointer(tile),
                    Convert.ToUInt32(Native.Get(state,"CurrentTurn")),viewer,owner);
                return true;
            }
            catch(Exception e){Error(e);return false;}
        }
        internal static bool TryToggle(object interaction,object unit,KnowledgeGestureContext expected)
        {
            if(!TryGestureContext(interaction,unit,out var live)||live!=expected)return false;
            PreferenceScope(Client!.Target()!,Native.Static("MapRenderer","Current")!);
            var key=(live.Viewer,live.Owner);
            if(!EnabledPairs.Remove(key))EnabledPairs.Add(key);
            Tick();return true;
        }
        static void Retire()
        {
            Hide();
            try
            {
                if(Alive(Root))Destroy(Root!);
                Root=null;Markers.Clear();MapKey=StateKey=0;
            }
            catch(Exception e){Error(e);} // Retain ownership for the next cleanup attempt.
        }
        public static void Selected(object __instance)
        {
            try
            {
                Hide();object? client=Native.Static("GameManager","Client");
                if(client is null){Clear();return;}
                Selection=new(__instance);Client=new(client);Tick();
            }
            catch(Exception e){Clear();Error(e);}
        }
        public static void BeforeDuplicate(){Duplicating++;Hide();}
        public static Exception? AfterDuplicate(Exception? __exception)
        {Duplicating=Math.Max(0,Duplicating-1);return __exception;}

        // Fixed labels, at most16 once per process. A quiet failed guard is
        // distinguishable from a renderer error without logging every frame.
        static bool Wait(string reason)
        {
            if(WaitReasons.Count<16&&WaitReasons.Add(reason))Entry.Note("intelligence-overlay-wait",reason);
            return false;
        }
        static bool Context(out object state,out object map,out object subject,out byte viewer,out byte owner)
        {
            state=map=subject=null!;viewer=owner=0;
            if(Duplicating!=0||Selection?.Target() is not object selectedTarget||Client?.Target() is not object clientTarget)return false;
            object interaction=Native.As(selectedTarget,"ClientInteraction"),client=Native.As(clientTarget,"ClientBase");
            if(!Native.Same(client,Native.Static("GameManager","Client"))||
                (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!||!(bool)Native.Get(client,"IsReady")!)return Wait("client-not-current-ready-local-view");
            if((bool)Native.Static("PopupManager","PopupShowing")!)return Wait("popup-showing");
            // Replay and spectator access require their own observer adapters.
            object? current=Native.Static("GameManager","GameState"),local=Native.Static("GameManager","LocalPlayer"),renderer=Native.Static("MapRenderer","Current");
            if(current is null||local is null||!Alive(renderer)||!RecruitmentHooks.Registered||
                !Native.Same(current,Native.Get(client,"GameState")))return Wait("state-map-or-registry-not-current");
            viewer=Convert.ToByte(Native.Get(local,"Id"));
            if(!(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!||
                !(bool)Native.Invoke(Native.M("PlayerExtensions","HasTech","PlayerState",RecruitmentHooks.TT),null,local,Native.EnumValue(RecruitmentHooks.TT,TechTree.IntelligenceId))!)return Wait("viewer-or-intelligence-research");
            object? unit=Native.Get(interaction,"selectedUnit"),nativeUnit=null,tile,data;
            if(Alive(unit))
            {
                // Native SelectUnit clears selectedTile before assigning the
                // unit. Resolve its CURRENT authoritative tile independently.
                if(!(bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!||(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!)return Wait("selected-unit-not-visible");
                nativeUnit=Native.Get(unit!,"UnitState");if(nativeUnit is null)return Wait("selected-unit-state-missing");
                uint id=Convert.ToUInt32(Native.Get(nativeUnit,"id"));object?[] unitArgs={id,null};
                if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),current,unitArgs)!||
                    !Native.Same(nativeUnit,unitArgs[1])||!Native.Same(unit,Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),renderer,id)))return Wait("selected-unit-not-authoritative");
                object coordinates=Native.Get(nativeUnit,"coordinates")!;
                data=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(current,"Map"),coordinates);
                tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),renderer,coordinates);
                if(data is null||!Alive(tile)||!Native.Same(data,Native.Get(tile!,"Data"))||
                    !Native.Same(nativeUnit,Native.Get(data,"unit"))||!Native.Same(unit,Native.Get(tile!,"unit")))return Wait("selected-unit-tile-or-occupant-changed");
                owner=NativeRogueRules.RememberedOwner(current,nativeUnit);
            }
            else
            {
                // City selection retains the original native selectedTile path.
                // A destroyed-but-retained unit does not grant a city fallback.
                if(unit is not null)return Wait("selected-unit-retired");
                tile=Native.Get(interaction,"selectedTile");if(!Alive(tile))return Wait("selected-city-tile-missing");
                data=Native.Get(tile!,"Data");if(data is null)return Wait("selected-city-data-missing");
                object coordinates=Native.Get(data,"coordinates")!;
                object? authoritative=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(current,"Map"),coordinates);
                if(!Native.Same(data,authoritative)||!Native.Same(tile,Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),renderer,coordinates))||
                    !(bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),data,
                        Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!)return Wait("selected-city-not-authoritative");
                owner=Convert.ToByte(Native.Get(data,"owner"));
            }
            if((bool)Native.Get(tile!,"IsHidden")!||!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,viewer)!)return Wait("selected-tile-outside-viewer-access");
            if(IntelligenceVision.Subject(viewer,owner) is null)return Wait("selection-is-not-an-opponent");
            object?[] args={owner,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),current,args)!||args[1] is null)return Wait("selected-owner-missing");
            state=current;map=renderer!;subject=args[1]!;return true;
        }

        static (object Sprite,int Layer,int Order) FogSource(object fog)
        {
            Require(Alive(fog),"Native fog sprite renderer is unavailable.");
            object? sprite=Native.Get(fog,"Sprite");
            if(Alive(sprite))return(sprite!,Native.Int(Native.Get(fog,"SortingLayer")),Native.Int(Native.Get(fog,"SortingOrder")));
            // A revealed tile's inactive fog may never have run native Init.
            // Its cached Sprite/sorting fields are then unpopulated; the
            // original prefab SpriteRenderer still owns the visual values.
            // Read those values without initializing or activating native fog.
            object? raw=Native.Get(fog,"spriteRenderer");
            Require(Alive(raw),"Native fog has no cached sprite or live source renderer.");
            sprite=Native.Get(raw!,"sprite");Require(Alive(sprite),"Native fog source sprite is unavailable.");
            return(sprite!,Native.Int(Native.Get(raw!,"sortingLayerID")),Native.Int(Native.Get(raw!,"sortingOrder")));
        }

        public static void Tick()
        {
            // Revalidate before every rendered frame. A stale selection never
            // keeps an overlay through fog, client, owner or research changes.
            if(Rendering){Hide();return;}
            Rendering=true;Hide();
            try
            {
                if(!Context(out var state,out var map,out var subject,out byte viewer,out byte owner)){Retire();return;}
                PreferenceScope(Client!.Target()!,map);
                if(!EnabledPairs.Contains((viewer,owner))){Retire();return;}
                object nativeTiles=Native.Get(Native.Get(state,"Map")!,"Tiles")!;
                Require(Native.Int(Native.Get(nativeTiles,"Count"))<=4096,"Intelligence map exceeds its supported bound.");
                var tiles=Native.Items(nativeTiles).ToArray();
                var byPosition=tiles.ToDictionary(t=>(Native.Int(Native.Get(Native.Get(t,"coordinates")!,"X")),Native.Int(Native.Get(Native.Get(t,"coordinates")!,"Y"))));
                // Native GetExplored is effective access (the game does not
                // re-fog explored terrain). Check the renderer's current local
                // fog too, so one transient hidden tile cannot publish a mark.
                var visible=new Dictionary<(int X,int Y),object>();
                var marks=IntelligenceVision.Fog(true,viewer,owner,true,byPosition.Select(p=>{
                    bool ours=(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),p.Value,viewer)!;
                    if(ours)
                    {
                        object? tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,Native.Get(p.Value,"coordinates"));
                        ours=Alive(tile)&&Native.Same(Native.Get(tile!,"Data"),p.Value)&&!(bool)Native.Get(tile!,"IsHidden")!;
                        if(ours)visible.Add(p.Key,tile!);
                    }
                    return new VisionTile(p.Key.Item1,p.Key.Item2,ours,
                        ours&&(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),p.Value,owner)!);
                }));
                if(MapKey!=Native.Pointer(map)||StateKey!=Native.Pointer(state)){Retire();Require(Root is null,"Old Intelligence root could not retire.");}
                if(!Alive(Root))
                {
                    Retire();Require(Root is null,"Old Intelligence root could not retire.");
                    Root=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Deception Intelligence overlay"})!;
                    Active(Root,false);MapKey=Native.Pointer(map);StateKey=Native.Pointer(state);
                }
                int packed=Native.Int(Native.Get(subject,"color"));
                if(packed==-1)packed=Native.Int(Native.Invoke(Native.M(RecruitmentHooks.GLD,"GetTribeColor","PolytopiaBackendBase.Common.TribeType","PolytopiaBackendBase.Common.SkinType"),
                    Native.Get(state,"GameLogicData"),Native.Get(subject,"tribe"),Native.Get(subject,"skinType")));
                var color=Activator.CreateInstance(Native.T("UnityEngine.Color"),new object[]{0.75f+0.25f*((packed>>16)&255)/255f,0.75f+0.25f*((packed>>8)&255)/255f,0.75f+0.25f*(packed&255)/255f,TintAlpha})!;
                foreach(var obsolete in Markers.Keys.Where(k=>!byPosition.ContainsKey(k)).ToArray())
                {Destroy(Markers[obsolete].Object);Markers.Remove(obsolete);}
                foreach(var marker in Markers.Values)Active(marker.Object,false);
                foreach(var mark in marks)
                {
                    var key=(mark.X,mark.Y);object data=byPosition[key];
                    object tile=visible[key];
                    Require(Alive(tile)&&Native.Same(Native.Get(tile,"Data"),data)&&!(bool)Native.Get(tile,"IsHidden")!,"Visible Intelligence tile lost its current native renderer.");
                    object fog=Native.Get(tile,"fogOfWarRenderer")!;var visual=FogSource(fog);
                    if(!Markers.TryGetValue(key,out var marker))
                    {
                        object go=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Intelligence "+mark.X+","+mark.Y})!;
                        try
                        {
                            Active(go,false);object type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"))!;
                            object sr=Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),go,type)!,"UnityEngine.SpriteRenderer");
                            Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),Native.Get(go,"transform"),Native.Get(Root!,"transform"),false);
                            marker=new(tile,go,sr);Markers.Add(key,marker);
                        }
                        catch{Destroy(go);throw;}
                    }
                    if(!Native.Same(marker.Tile,tile))
                    {
                        // Tile pooling may replace a native tile without a
                        // new MapRenderer. Reuse only our renderer, then bind
                        // its borrowed visual to the current authoritative tile.
                        marker=marker with{Tile=tile};Markers[key]=marker;
                    }
                    object source=Native.Get(fog,"transform")!,target=Native.Get(marker.Object,"transform")!;
                    Native.Set(target,"localPosition",Native.Get(source,"position"));Native.Set(target,"localRotation",Native.Get(source,"rotation"));Native.Set(target,"localScale",Native.Get(source,"lossyScale"));
                    Native.Set(marker.Object,"layer",Native.Get(Native.Get(fog,"gameObject")!,"layer"));
                    Native.Set(marker.Renderer,"sprite",visual.Sprite);Native.Set(marker.Renderer,"color",color);
                    Native.Set(marker.Renderer,"sortingLayerID",visual.Layer);Native.Set(marker.Renderer,"sortingOrder",visual.Order);
                    Active(marker.Object,true);
                }
                // All marks were prepared while hidden. Recheck the selection
                // once more before publishing this purely visual frame.
                Require(Context(out var current,out var currentMap,out _,out byte v,out byte o)&&Native.Same(state,current)&&Native.Same(map,currentMap)&&v==viewer&&o==owner&&EnabledPairs.Contains((v,o)),
                    "Intelligence selection changed while preparing the frame.");
                Active(Root!,true);
                if(LastSubject!=owner||LastCount!=marks.Length){LastSubject=owner;LastCount=marks.Length;Entry.Note("intelligence-overlay","Selected faction="+owner+"; own-visible enemy-fog tiles="+marks.Length+"; no exploration changed.");}
            }
            catch(Exception e){Retire();Error(e);}
            finally{Rendering=false;}
        }
    }
}
