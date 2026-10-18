using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Unit controls submit only a fresh persistent command. City marks are owned,
    // public sprites on already visible cities; no fog or city state is edited.
    internal static class NativeBlackmailPresentation
    {
        sealed record Context(object State,object Client,object Player,object Map,byte Viewer);
        sealed record Selection(Context View,object Interaction,object Bar,object Tile,object Data,object Unit,object Body,uint Turn,int Command);
        sealed record Control(Selection Selection,object Button,object Buttons,object Callback,object Crown)
        {internal int QuotedCost;}
        sealed record Mark(object Tile,object Data,object Root,object Fixed,object Team);
        static readonly Dictionary<long,Control> Controls=new();
        sealed record Offer(Selection Selection,NativeGameStateEnvelope.Snapshot Snapshot,int Currency,int Owner,int Color,bool Research,bool Allowed,int Cost);
        static readonly Dictionary<long,Offer> Offers=new();
        static readonly Dictionary<(int X,int Y,byte Actor),Mark> Marks=new();
        static readonly Dictionary<long,Layout> Layouts=new();
        static object? Interaction,MapRoot,BoundMap,BoundState;
        static NativeGameStateEnvelope.Snapshot? ReadSnapshot;
        static uint ReadTurn;static int ReadCommand;
        static LedgerBlackmail[] Records=Array.Empty<LedgerBlackmail>();
        static readonly HashSet<string> Errors=new();
        static int Duplicating;static long Generation;
        public sealed record DuplicateScope(long Epoch,int Depth);
        internal static void Install(Harmony h)
        {
            foreach(var method in new[]{Native.M("ClientInteraction","SelectUnit","Unit"),Native.M("ClientInteraction","SelectTile","Tile")})
                Native.Hook(h,method,typeof(NativeBlackmailPresentation),null,nameof(Selected));
            foreach(string name in new[]{"RefreshTileOptions","RefreshUnitOptions","RefreshPCBarInternal"})
                Native.Hook(h,Native.M("InteractionBar",name),typeof(NativeBlackmailPresentation),null,nameof(Refreshed));
            foreach(string name in new[]{"ClearButtons","Reset","DeInit","OnDestroy"})Native.Hook(h,Native.M("InteractionBar",name),typeof(NativeBlackmailPresentation),nameof(NativeClear));
            Native.Hook(h,Native.M("GameManager","Update"),typeof(NativeBlackmailPresentation),null,nameof(Tick));
            foreach(string name in new[]{"ClearSelection","DeselectUnit","OnSelectionCleared","OnTurnStarted","OnDisable"})Native.Hook(h,Native.M("ClientInteraction",name),typeof(NativeBlackmailPresentation),nameof(ClearSelection));
            foreach(string name in new[]{"ReturnToMenu","OnClientSessionOpened"})Native.Hook(h,Native.M("GameManager",name),typeof(NativeBlackmailPresentation),nameof(Clear));
            Native.Hook(h,Native.M("MapRenderer","OnDestroy"),typeof(NativeBlackmailPresentation),nameof(MapDestroying));
            Native.Hook(h,Native.M("CityRenderer","RefreshCity","MapRenderContext"),typeof(NativeBlackmailPresentation),nameof(BeforeCityRefresh));
            Native.Hook(h,Native.M("CityRenderer","Clear"),typeof(NativeBlackmailPresentation),nameof(CityChanging));
            Native.Hook(h,Native.M("CityStatusDisplay","ReturnToPool"),typeof(NativeBlackmailPresentation),nameof(NameReturning));
            foreach(var method in new[]{Native.M("UISpriteDuplicator","RenderSprites","UISpriteDuplicator+SpriteDuplicationData"),
                Native.M("UISpriteDuplicator","RenderSprites","UnityEngine.Transform","UIDuplicatedSprites","System.Single","System.String","UnityEngine.Vector2","System.Boolean")})
                Native.Hook(h,method,typeof(NativeBlackmailPresentation),nameof(BeforeDuplicate),null,nameof(AfterDuplicate));
        }
        static void Error(Exception e){if(Errors.Count<16&&Errors.Add(e.Message))Entry.Note("blackmail-presentation-error",e.ToString());}
        static void Active(object go,bool active)=>Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,active);
        static void Destroy(object? go){if(Native.UnityAlive(go))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,go);}
        static object V3(float x,float y,float z)=>Activator.CreateInstance(Native.T("UnityEngine.Vector3"),x,y,z)!;
        static object At(int x,int y)=>Activator.CreateInstance(Native.T("WorldCoordinates"),x,y)!;
        static object? TileData(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static object? Player(object state,byte id){object?[] a={id,null};return (bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,a)!?a[1]:null;}
        internal static int Color(object state,object player)=>BlackmailArtAssets.PlayerColor(state,player);
        static bool View(out Context c)
        {
            c=null!;if(Duplicating!=0||!NativeRegroupSession.Enabled||!RecruitmentHooks.Registered)return false;
            var state=Native.Static("GameManager","GameState");var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");var map=Native.Static("MapRenderer","Current");
            if(state is null||client is null||player is null||!Native.UnityAlive(map)||!(bool)Native.Get(Native.Get(map!,"gameObject")!,"activeInHierarchy")!||!NativeRegroupSession.Ready(state)||
                !Native.Same(state,Native.Get(client,"GameState"))||!(bool)Native.Get(client,"IsReady")!||(bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!||
                (bool)Native.Static("HotSeatOverlay","IsActive")!)return false;
            byte viewer=Convert.ToByte(Native.Get(player,"Id"));
            if(!(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!)return false;
            c=new(state,client,player,map!,viewer);return true;
        }
        static bool Visible(Context c,object tile,object data)
        {
            if(!Native.UnityAlive(tile)||!(bool)Native.Get(Native.Get(tile,"gameObject")!,"activeInHierarchy")!||(bool)Native.Get(tile,"IsHidden")!||!Native.Same(Native.Get(tile,"Data"),data))return false;
            var at=Native.Get(data,"coordinates")!;
            return Native.Same(TileData(c.State,at),data)&&Native.Same(Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),c.Map,at),tile)&&
                (bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,c.Viewer)!&&
                (bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),data,Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!;
        }
        static bool ReadSelection(object bar,out Selection s)
        {
            s=null!;var interaction=Interaction;
            if(interaction is null||!Native.UnityAlive(bar)||!View(out var c)||Native.Int(Native.Get(c.State,"CurrentPlayer"))!=c.Viewer||
                (bool)Native.Static("PopupManager","PopupShowing")!||MimicRegroupUi.Choosing||NativeSpecialActionUi.Choosing||
                (bool)Native.Get(c.Client,"IsWaitingForCommand")!||(bool)Native.Get(Native.Get(c.Client,"ActionManager")!,"IsProcessing")!||
                !Native.Same(Native.Invoke(Native.M("ClientInteraction","GetInteractionBar"),interaction),bar))return false;
            var unit=Native.Get(interaction,"selectedUnit");
            if(!Native.UnityAlive(unit)||!Native.Same(unit,Native.Get(bar,"unit"))||
                !(bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!||(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!)return false;
            var body=Native.Get(unit!,"UnitState");if(body is null)return false;
            uint id=Convert.ToUInt32(Native.Get(body,"id"));object?[] lookup={id,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),c.State,lookup)!||!Native.Same(lookup[1],body)||
                !Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),c.Map,id),unit))return false;
            var at=Native.Get(body,"coordinates")!;var tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),c.Map,at);
            var data=TileData(c.State,at);
            if(data is null||!Native.UnityAlive(tile)||!(bool)Native.Get(Native.Get(tile!,"gameObject")!,"activeInHierarchy")!||
                (bool)Native.Get(tile!,"IsHidden")!||!Native.Same(Native.Get(tile!,"Data"),data)||!Native.Same(Native.Get(data,"unit"),body)||
                !(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,c.Viewer)!)return false;
            s=new(c,interaction,bar,tile!,data,unit!,body,Convert.ToUInt32(Native.Get(c.State,"CurrentTurn")),Native.Int(Native.Get(c.State,"CurrentCommand")));return true;
        }
        static bool Same(Selection a,Selection b)=>Native.Same(a.View.State,b.View.State)&&Native.Same(a.View.Client,b.View.Client)&&Native.Same(a.View.Map,b.View.Map)&&
            Native.Same(a.Interaction,b.Interaction)&&Native.Same(a.Bar,b.Bar)&&Native.Same(a.Tile,b.Tile)&&Native.Same(a.Data,b.Data)&&Native.Same(a.Unit,b.Unit)&&Native.Same(a.Body,b.Body)&&a.View.Viewer==b.View.Viewer&&a.Turn==b.Turn&&a.Command==b.Command;
        static bool Owned(Control c)=>Native.UnityAlive(c.Selection.Bar)&&Native.UnityAlive(c.Button)&&Native.Same(Native.Get(c.Selection.Bar,"buttons"),c.Buttons)&&
            Native.Items(c.Buttons).Any(x=>Native.Same(x,c.Button))&&Native.Same(Native.Get(c.Button,"OnClicked"),c.Callback)&&(string?)Native.Get(c.Button,"text")=="Blackmail";
        public static void Selected(object __instance){ClearSelection();Interaction=__instance;var bar=Native.Invoke(Native.M("ClientInteraction","GetInteractionBar"),__instance);if(bar is not null)Sync(bar);}
        public static void Refreshed(object __instance)=>Sync(__instance);
        static void Sync(object bar)
        {
            try
            {
                long key=Native.Pointer(bar);
                if(!ReadSelection(bar,out var s)){Remove(bar);Offers.Remove(key);return;}
                var snapshot=NativeGameStateEnvelope.Get(s.View.State);if(snapshot is null){Remove(bar);Offers.Remove(key);return;}
                int currency=Native.Int(Native.Get(s.View.Player,"Currency")),owner=Native.Int(Native.Get(s.Body,"owner")),color=Color(s.View.State,s.View.Player);
                bool research=(bool)Native.Invoke(Native.M("PlayerExtensions","HasTech","PlayerState",RecruitmentHooks.TT),null,s.View.Player,Native.EnumValue(RecruitmentHooks.TT,TechTree.EspionageId))!;
                if(!Offers.TryGetValue(key,out var offered)||!Same(offered.Selection,s)||!ReferenceEquals(offered.Snapshot,snapshot)||offered.Currency!=currency||offered.Owner!=owner||offered.Color!=color||offered.Research!=research)
                {
                    bool allowed=NativeBlackmail.TryOffer(s.View.State,s.View.Viewer,s.Body,out _,out int cost,out _);
                    if(Offers.Count>=8&&!Offers.ContainsKey(key))Offers.Clear();Offers[key]=offered=new(s,snapshot,currency,owner,color,research,allowed,cost);
                }
                if(!offered.Allowed){Remove(bar);return;}
                if(Controls.TryGetValue(key,out var existing))
                {if(Owned(existing)&&Same(existing.Selection,s))
                    {
                        if(existing.QuotedCost!=offered.Cost){Native.Set(existing.Button,"Cost",(float)offered.Cost);existing.QuotedCost=offered.Cost;}
                        Native.Set(existing.Crown,"color",BlackmailArtAssets.TeamColor(color));return;
                    }Remove(bar);}
                object button=Native.Invoke(Native.M("InteractionBar","CreateRoundBottomBarButton","System.String","System.Boolean"),bar,"Blackmail",true)!;
                object buttons=Native.Get(bar,"buttons")!;object? crown=null;
                try
                {
                    crown=BlackmailArtAssets.Button(button,Color(s.View.State,s.View.Player));
                    Control? control=null;var callback=Native.ButtonCallback(()=>{if(control is not null)Click(control);});
                    control=new(s,button,buttons,callback,crown){QuotedCost=offered.Cost};Native.Set(button,"OnClicked",callback);Native.Set(button,"OnClickedWhenDisabled",null);
                    Native.Set(button,"Cost",(float)offered.Cost);Native.Set(button,"buttonActive",true);Native.Set(button,"ButtonEnabled",true);
                    Native.Invoke(Native.M("UIButtonBase","SetActive","System.Boolean"),button,true);Controls[key]=control;
                    MimicPresentationUi.ReflowBar(bar);
                }
                catch
                {
                    // Native construction appends before artwork/callback setup.
                    // Roll back that exact entry even before Control publication.
                    if(Native.UnityAlive(bar)&&Native.Same(buttons,Native.Get(bar,"buttons"))&&Native.Items(buttons).Any(x=>Native.Same(x,button)))
                        Native.Invoke(Native.M(buttons.GetType(),"Remove","UIRoundButton"),buttons,button);
                    Destroy(crown is null?null:Native.Get(crown,"gameObject"));Destroy(Native.Get(button,"gameObject"));throw;
                }
            }
            catch(Exception e){Error(e);Remove(bar);}
        }
        static void Click(Control control)
        {
            try
            {
                if(!Controls.TryGetValue(Native.Pointer(control.Selection.Bar),out var same)||!ReferenceEquals(same,control)||!Owned(control)||
                    !ReadSelection(control.Selection.Bar,out var s)||!Same(control.Selection,s))return;
                bool allowed=NativeBlackmail.TryOffer(s.View.State,s.View.Viewer,s.Body,out var intent,out int cost,out _);
                // A ledger change may arrive before the next UI tick. Never
                // submit a purchase at a price the player has not yet seen.
                if(!allowed||intent is null||cost!=control.QuotedCost)
                {Offers.Remove(Native.Pointer(s.Bar));Sync(s.Bar);return;}
                var command=NativeExchangeCommands.Create(intent);
                if(!NativeExchangeCommands.Valid(command,s.View.State,true,out _)){NativeExchangeCommands.Forget(command);return;}
                Native.Invoke(Native.M("ClientBase","SendCommand","CommandBase"),s.View.Client,command);
            }
            catch(Exception e){Error(e);}
        }
        static void Remove(object bar)
        {
            if(!Controls.Remove(Native.Pointer(bar),out var c))return;
            if(Native.UnityAlive(c.Crown))Destroy(Native.Get(c.Crown,"gameObject"));
            if(!Owned(c))return;
            Native.Set(c.Button,"OnClicked",null);Native.Set(c.Button,"OnClickedWhenDisabled",null);Native.Set(c.Button,"ButtonEnabled",false);
            Native.Invoke(Native.M(c.Buttons.GetType(),"Remove","UIRoundButton"),c.Buttons,c.Button);Destroy(Native.Get(c.Button,"gameObject"));MimicPresentationUi.ReflowBar(bar);
        }
        public static void NativeClear(object __instance)
        {
            Offers.Remove(Native.Pointer(__instance));
            if(!Controls.Remove(Native.Pointer(__instance),out var c))return;
            if(Native.UnityAlive(c.Crown))Destroy(Native.Get(c.Crown,"gameObject"));
            if(!Owned(c))return;
            Native.Set(c.Button,"OnClicked",null);Native.Set(c.Button,"OnClickedWhenDisabled",null);
        }
        public static void ClearSelection(){Interaction=null;Offers.Clear();foreach(var c in Controls.Values.ToArray())Remove(c.Selection.Bar);}
        static void Hide(){if(Native.UnityAlive(MapRoot))Active(MapRoot!,false);}
        static void Retire(){Hide();Destroy(MapRoot);MapRoot=BoundMap=BoundState=null;Marks.Clear();Layouts.Clear();ReadSnapshot=null;Records=Array.Empty<LedgerBlackmail>();}
        public static void Clear(){Generation++;ClearSelection();Retire();Duplicating=0;}
        public static void MapDestroying(object __instance){if(Native.Same(__instance,BoundMap))Clear();}
        public static void BeforeCityRefresh(object __instance){if((bool)Native.Get(__instance,"dataChanged")!)CityChanging(__instance);}
        public static void CityChanging(object __instance){Layouts.Remove(Native.Pointer(__instance));Hide();}
        public static void BeforeDuplicate(out DuplicateScope __state){__state=new(Generation,Duplicating);Duplicating++;Hide();MimicMapInspection.HideBlackmailMarkers();}
        public static Exception? AfterDuplicate(Exception? __exception,DuplicateScope __state){if(__state.Epoch==Generation)Duplicating=__state.Depth;return __exception;}
        static object Renderer(object root)
        {
            var type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"))!;
            return Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),root,type)!,"UnityEngine.SpriteRenderer");
        }
        static Mark CreateMark(object tile,object data)
        {
            object root=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Deception Blackmail city marker"})!;
            try
            {
                Active(root,false);Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),Native.Get(root,"transform"),Native.Get(MapRoot!,"transform"),false);
                var fixedRenderer=Renderer(root);var teamObject=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Blackmail team crown"})!;
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),Native.Get(teamObject,"transform"),Native.Get(root,"transform"),false);
                var teamRenderer=Renderer(teamObject);Native.Set(fixedRenderer,"sprite",BlackmailArtAssets.Sprite());Native.Set(teamRenderer,"sprite",BlackmailArtAssets.Sprite(true));
                return new(tile,data,root,fixedRenderer,teamRenderer);
            }
            catch{Destroy(root);throw;}
        }
        readonly record struct Box(float Left,float Bottom,float Right,float Top);
        sealed record Layout(object Frame,object Display,object Name,object Source,object TextRenderer,Box Background,Box Text,float Bottom,float ScaleX,float ScaleY)
        {internal readonly Dictionary<int,object[]> Places=new();}
        // One board-space size for every city. Rendered glyph bounds vary with
        // the name's letters, and the name frame may be scaled independently.
        // Native NameLabel uses a .08 frame; keep documents at 1.5 of that
        // reference and a small gap beneath the underline, not below the roof.
        const float DocumentHeight=.12f,UnderlineGap=.004f;
        // Generated Unity vectors are fields, while native object members are
        // properties. Keep the actual wrapper ABI in this small geometry read.
        static float F(object value,string name)=>Convert.ToSingle(value.GetType().GetField(name,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance)?.GetValue(value)??Native.Get(value,name));
        static object Point(object transform,object point,bool inverse=false)=>Native.Invoke(Native.M("UnityEngine.Transform",inverse?"InverseTransformPoint":"TransformPoint","UnityEngine.Vector3"),transform,point)!;
        static Box InCity(object frame,object bounds)
        {
            var c=Native.Get(bounds,"center")!;var s=Native.Get(bounds,"size")!;
            float left=float.PositiveInfinity,bottom=left,right=float.NegativeInfinity,top=right;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {var p=Point(frame,V3(F(c,"x")+x*F(s,"x")/2,F(c,"y")+y*F(s,"y")/2,F(c,"z")+z*F(s,"z")/2),true);left=Math.Min(left,F(p,"x"));right=Math.Max(right,F(p,"x"));bottom=Math.Min(bottom,F(p,"y"));top=Math.Max(top,F(p,"y"));}
            return new(left,bottom,right,top);
        }
        static bool Shown(object? component)=>Native.UnityAlive(component)&&(bool)Native.Get(Native.Get(component!,"gameObject")!,"activeInHierarchy")!;
        static bool CityStatusBottom(object display,object frame,Box background,Box text,out float bottom)
        {
            bottom=Math.Min(background.Bottom,text.Bottom);
            var bar=Native.Get(display,"progressBar");if(!Shown(bar))return true;
            var barFrame=Native.Get(bar!,"transform")!;
            if(!(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),barFrame,Native.Get(display,"transform"))!)return false;
            // The owner's population bar is a sibling below the name. Reserve
            // its actual visible edge, including dots, without moving native UI
            // or retaining a pooled segment. Foreign/hidden bars add no gap.
            foreach(var segment in Native.Items(Native.Get(bar!,"segments")))
            {
                if(!Shown(segment))continue;
                if(!(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),Native.Get(segment,"transform"),barFrame)!)return false;
                for(int i=0;i<2;i++)
                {
                    var part=Native.Get(segment,i==0?"background":"dot");
                    if(!Shown(part)||!(bool)Native.Get(part!,"enabled")!||F(Native.Get(part!,"color")!,"a")<=0)continue;
                    if(!(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),Native.Get(part!,"transform"),Native.Get(segment,"transform"))!)return false;
                    var bounds=InCity(frame,Native.Get(part!,"bounds")!);
                    if(!float.IsFinite(bounds.Bottom)||!float.IsFinite(bounds.Top)||bounds.Top<bounds.Bottom)return false;
                    bottom=Math.Min(bottom,bounds.Bottom);
                }
            }
            return true;
        }
        static bool CityName(object tile,object data,out Layout layout)
        {
            layout=null!;var building=Native.Get(tile,"Improvement");if(!Native.UnityAlive(building))return false;
            var city=Native.As(building!,"City");var renderer=Native.Get(city,"cityRenderer");
            if(!Shown(renderer)||(bool)Native.Get(renderer!,"dataChanged")!||!Native.Same(Native.Get(city,"Tile"),tile))return false;
            var a=Native.Get(renderer!,"Coordinates")!;var b=Native.Get(data,"coordinates")!;
            if(Native.Int(Native.Get(a,"X"))!=Native.Int(Native.Get(b,"X"))||Native.Int(Native.Get(a,"Y"))!=Native.Int(Native.Get(b,"Y")))return false;
            var display=Native.Get(city,"cityOverlay");if(!Shown(display)||!Native.Same(Native.Get(display!,"city"),city))return false;
            var name=Native.Get(display!,"nameContainer");if(!Shown(name)||!Native.Same(Native.Get(name!,"city"),city))return false;
            var source=Native.Get(name!,"bg");var label=Native.Get(name!,"label");
            if(!Shown(source)||!Shown(label)||!(bool)Native.Get(source!,"enabled")!||F(Native.Get(source!,"color")!,"a")<=0)return false;
            var textRenderer=Native.Get(label!,"renderer");if(!Shown(textRenderer)||!(bool)Native.Get(textRenderer!,"enabled")!)return false;
            var frame=Native.Get(name!,"transform")!;
            if(!(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),Native.Get(source!,"transform"),frame)!||
                !(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),Native.Get(label!,"transform"),frame)!)return false;
            var bg=InCity(frame,Native.Get(source!,"bounds")!);var text=InCity(frame,Native.Get(textRenderer!,"bounds")!);
            if(!float.IsFinite(text.Top-text.Bottom)||text.Top<=text.Bottom||!float.IsFinite(bg.Right-bg.Left)||bg.Right<=bg.Left)return false;
            if(!CityStatusBottom(display!,frame,bg,text,out float bottom))return false;
            var lossy=Native.Get(frame,"lossyScale")!;float sx=Math.Abs(F(lossy,"x")),sy=Math.Abs(F(lossy,"y"));
            if(!float.IsFinite(sx)||!float.IsFinite(sy)||sx<=0||sy<=0)return false;
            long key=Native.Pointer(renderer);
            if(Layouts.TryGetValue(key,out var cached)&&Native.Same(cached.Frame,frame)&&Native.Same(cached.Display,display)&&Native.Same(cached.Name,name)&&
                Native.Same(cached.Source,source)&&Native.Same(cached.TextRenderer,textRenderer)&&cached.Background==bg&&cached.Text==text&&cached.Bottom==bottom&&cached.ScaleX==sx&&cached.ScaleY==sy){layout=cached;return true;}
            if(Layouts.Count>=4096&&!Layouts.ContainsKey(key))Layouts.Clear();
            Layouts[key]=layout=new(frame,display!,name!,source!,textRenderer!,bg,text,bottom,sx,sy);return true;
        }
        public static void NameReturning(object __instance)
        {
            // Pooled names can be reassigned before the next GameManager tick.
            // Hide owned documents immediately; never alter the native name.
            foreach(var key in Layouts.Where(p=>Native.Same(p.Value.Display,__instance)).Select(p=>p.Key).ToArray())Layouts.Remove(key);
            Hide();
        }
        static object[] Placement(Layout layout,int count)
        {
            var artSize=Native.Get(Native.Get(BlackmailArtAssets.Sprite(),"bounds")!,"size")!;
            float height=DocumentHeight/layout.ScaleY,width=DocumentHeight*F(artSize,"x")/F(artSize,"y")/layout.ScaleX,step=width*.55f;
            float center=(layout.Background.Left+layout.Background.Right)/2;
            float y=layout.Bottom-UnderlineGap/layout.ScaleY-height/2;
            return Enumerable.Range(0,count).Select(i=>V3(center+(i-(count-1)/2f)*step,y,0)).ToArray();
        }
        public static void Tick()
        {
            try
            {
                if(Interaction is object selected){var bar=Native.Invoke(Native.M("ClientInteraction","GetInteractionBar"),selected);if(bar is not null)Sync(bar);}
                Hide();if(!View(out var c)){Retire();return;}
                if(!Native.Same(BoundMap,c.Map)||!Native.Same(BoundState,c.State))Retire();
                var snapshot=NativeGameStateEnvelope.Get(c.State);if(snapshot is null){Retire();return;}
                uint turn=Convert.ToUInt32(Native.Get(c.State,"CurrentTurn"));int command=Native.Int(Native.Get(c.State,"CurrentCommand"));
                if(!ReferenceEquals(snapshot,ReadSnapshot)||ReadTurn!=turn||ReadCommand!=command){Records=NativeBlackmail.Contracts(c.State);ReadSnapshot=snapshot;ReadTurn=turn;ReadCommand=command;}
                if(Records.Length==0){Destroy(MapRoot);MapRoot=null;Marks.Clear();Layouts.Clear();BoundMap=c.Map;BoundState=c.State;return;}
                if(Records.Length>4096*16)throw new InvalidOperationException("Blackmail marker bound exceeded.");
                if(!Native.UnityAlive(MapRoot)){MapRoot=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Deception Blackmail markers"})!;BoundMap=c.Map;BoundState=c.State;Active(MapRoot,false);}
                foreach(var mark in Marks.Values)if(Native.UnityAlive(mark.Root))Active(mark.Root,false);
                var used=new HashSet<(int,int,byte)>();
                foreach(var group in Records.GroupBy(r=>(r.X,r.Y)))
                {
                    var at=At(group.Key.X,group.Key.Y);var data=TileData(c.State,at);var tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),c.Map,at);
                    if(data is null||!Native.UnityAlive(tile)||!Visible(c,tile!,data))continue;
                    var valid=group.Where(r=>Native.Int(Native.Get(data,"owner"))==r.Victim&&Player(c.State,r.Blackmailer) is not null).GroupBy(r=>r.Blackmailer).Select(g=>g.First()).OrderBy(r=>r.Blackmailer).ToArray();
                    if(valid.Length==0||!CityName(tile!,data,out var layout))continue;
                    if(!layout.Places.TryGetValue(valid.Length,out var places))layout.Places[valid.Length]=places=Placement(layout,valid.Length);
                    for(int i=0;i<valid.Length;i++)
                    {
                        var r=valid[i];var key=(r.X,r.Y,r.Blackmailer);used.Add(key);
                        if(!Marks.TryGetValue(key,out var mark)||!Native.UnityAlive(mark.Root)||!Native.Same(mark.Tile,tile)||!Native.Same(mark.Data,data))
                        {if(mark is not null)Destroy(mark.Root);Marks[key]=mark=CreateMark(tile!,data);}
                        var st=layout.Frame;var source=layout.Source;
                        var t=Native.Get(mark.Root,"transform")!;
                        float scale=DocumentHeight/F(Native.Get(Native.Get(BlackmailArtAssets.Sprite(),"bounds")!,"size")!,"y");
                        Native.Set(t,"position",Point(st,places[i]));Native.Set(t,"localRotation",Native.Get(st,"rotation"));
                        Native.Set(t,"localScale",V3(scale,scale,scale));
                        foreach(var part in new[]{mark.Fixed,mark.Team})
                        {
                            Native.Set(Native.Get(part,"gameObject")!,"layer",Native.Get(Native.Get(source,"gameObject")!,"layer"));
                            Native.Set(part,"sortingLayerID",Native.Get(source,"sortingLayerID"));
                            Native.Set(part,"sortingOrder",Native.Int(Native.Get(source,"sortingOrder"))+2+i*2+(Native.Same(part,mark.Team)?1:0));
                        }
                        Native.Set(mark.Fixed,"color",Activator.CreateInstance(Native.T("UnityEngine.Color"),1f,1f,1f,1f));
                        Native.Set(mark.Team,"color",BlackmailArtAssets.TeamColor(Color(c.State,Player(c.State,r.Blackmailer)!)));Active(mark.Root,true);
                    }
                }
                foreach(var key in Marks.Keys.Where(k=>!used.Contains(k)).ToArray()){Destroy(Marks[key].Root);Marks.Remove(key);}
                if(View(out var again)&&Native.Same(c.State,again.State)&&Native.Same(c.Client,again.Client)&&Native.Same(c.Map,again.Map)&&c.Viewer==again.Viewer)Active(MapRoot!,true);else Retire();
            }
            catch(Exception e){Error(e);Retire();}
        }
    }
}
