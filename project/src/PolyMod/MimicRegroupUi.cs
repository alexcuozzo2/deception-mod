using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;


namespace Deception.PolyModAdapter
{
    internal static class MimicRegroupUi
    {
        sealed class Control
        {
            internal object Bar=null!,Unit=null!,Button=null!,Buttons=null!,Callback=null!;
            internal NativeExchangeKind Kind;
        }
        sealed class Session
        {
            internal object Bar=null!,Unit=null!,State=null!,Client=null!,Player=null!,Interaction=null!,Map=null!;
            internal uint Initiator,Turn;
            internal NativeExchangeKind Kind;
            internal Control Control=null!;
            internal long Due;
            internal NeighborhoodSnapshot Neighborhood=null!;
            internal long Revision;
            internal NativeGameStateEnvelope.Snapshot LedgerBinding=null!;
            internal ushort Command;
            internal Option[] Options=Array.Empty<Option>();
            internal Option? Partner;
            internal object? MarkerRoot;
            internal readonly List<Marker> Markers=new();
        }
        sealed record Option(uint Id,NativeExchangeIntent? Keep,NativeExchangeIntent? Swap);
        sealed record Marker(object Tile,object Unit,object Data,object Object,object Renderer,bool Swap);
        readonly record struct EligibilityStamp(uint Turn,byte Actor,ushort Command,long Revision,uint Id,byte Owner,
            int Type,int X,int Y,bool Moved,bool Attacked,ushort Health,bool Frozen,bool Researched);
        sealed record Eligibility(object Bar,object Unit,object UnitState,object State,object Client,object Player,
            object Map,NativeGameStateEnvelope.Snapshot LedgerBinding,EligibilityStamp Stamp,NeighborhoodSnapshot Neighborhood,bool Available);
        const int MaxEligibilityBars=8;
        static readonly Dictionary<(long Bar,NativeExchangeKind Kind),Control> Controls=new();
        // Cache only whether an idle bar has an option, never a command/intent.
        // Native roots remain held for these bounded entries, so a recycled
        // wrapper address cannot silently inherit a prior eligibility result.
        static readonly Dictionary<(long Bar,NativeExchangeKind Kind),Eligibility> EligibilityCache=new();
        static Session? Active;
        static long TickNumber;
        static bool Ending;
        internal static bool Choosing=>Active is not null;
        internal static void Install(Harmony h)
        {
            foreach(string name in new[]{"ClearButtons","Reset","DeInit","OnDestroy"})
                Native.Hook(h,Native.M("InteractionBar",name),typeof(MimicRegroupUi),nameof(NativeClear));
            Native.Hook(h,Native.M("GameManager","Update"),typeof(MimicRegroupUi),null,nameof(Tick));
            foreach(string name in new[]{"SendCommand","SendCommandRemote"})
                Native.Hook(h,Native.M("ClientBase",name,"CommandBase"),typeof(MimicRegroupUi),nameof(GuardCommands));
            Native.Hook(h,Native.M("ClientInteraction","ValidateMove","Unit","Tile","Il2CppSystem.Action"),typeof(MimicRegroupUi),nameof(GuardCommands));
            Native.Hook(h,Native.M("ClientInteraction","OnPress","Tile"),typeof(MimicRegroupUi),nameof(GuardCommands));
            Native.Hook(h,Native.M("ClientInteraction","OnRelease","Tile","System.Int32"),typeof(MimicRegroupUi),nameof(Released));
            Native.Hook(h,Native.M("Unit","UpdateOverlays"),typeof(MimicRegroupUi),nameof(BeforeOverlays));
            foreach(string name in new[]{"ClearSelection","DeselectUnit","OnSelectionCleared","OnTurnStarted","OnDisable"})
                Native.Hook(h,Native.M("ClientInteraction",name),typeof(MimicRegroupUi),nameof(Clear));
            foreach(string name in new[]{"ReturnToMenu","OnClientSessionOpened"})
                Native.Hook(h,Native.M("GameManager",name),typeof(MimicRegroupUi),nameof(Clear));
        }
        public static bool GuardCommands()=>!Choosing;
        static readonly NativeExchangeKind[] Kinds={NativeExchangeKind.Regroup,NativeExchangeKind.Disguise};
        static string Caption(NativeExchangeKind kind)=>kind==NativeExchangeKind.Regroup?"Regroup":"Disguise";
        static bool Researched(object player,NativeExchangeKind kind)=>RecruitmentHooks.Registered&&(bool)Native.Invoke(
            Native.M("PlayerExtensions","HasTech","PlayerState","Polytopia.Data.TechData+Type"),null,player,
            Native.EnumValue("Polytopia.Data.TechData+Type",kind==NativeExchangeKind.Regroup?TechTree.DeceptionId:TechTree.PropagandaId))!;
        static bool Owned(Control c)=>Native.UnityAlive(c.Bar)&&Native.UnityAlive(c.Button)&&
            Native.Same(Native.Get(c.Bar,"buttons"),c.Buttons)&&Native.Items(c.Buttons).Any(x=>Native.Same(x,c.Button))&&
            ((string?)Native.Get(c.Button,"text")==Caption(c.Kind)||(string?)Native.Get(c.Button,"text")=="Cancel")&&Native.Same(Native.Get(c.Button,"OnClicked"),c.Callback);
        internal static object? OwnedButton(object bar)=>OwnedButton(bar,NativeExchangeKind.Regroup);
        internal static object? OwnedButton(object bar,NativeExchangeKind kind)=>Controls.TryGetValue((Native.Pointer(bar),kind),out var c)&&Owned(c)?c.Button:null;
        public static bool BeforeOverlays(object __instance)=>Active is not Session s||!Native.Same(s.Unit,__instance);
        static object[] NeighborhoodTiles(object state,object unit)
        {
            var area=Native.Invoke(Native.M("MapDataExtensions","GetAreaSorted","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),
                null,Native.Get(state,"Map"),Native.Get(unit,"coordinates"),1,true,true)!;
            int count=Native.Int(Native.Get(area,"Length"));if(count>9)throw new InvalidOperationException("Regroup neighborhood exceeds nine tiles.");
            var item=Native.M(area.GetType(),"get_Item","System.Int32");
            return Enumerable.Range(0,count).Select(i=>Native.Invoke(item,area,i)!).ToArray();
        }
        readonly record struct NeighborStamp(long Tile,long Body,uint Id,int Type,byte Owner,ushort Health,
            bool Moved,bool Attacked,int X,int Y,bool Frozen);
        sealed record NeighborCell(object Coordinates,object? Tile,object? Body,NeighborStamp Stamp);
        sealed class NeighborhoodSnapshot
        {
            internal readonly object Map;
            internal readonly NeighborCell[] Cells;
            internal NeighborhoodSnapshot(object state,object unit)
            {
                Map=Native.Get(state,"Map")!;
                Cells=NeighborhoodTiles(state,unit).Select(t=>ReadCell(Map,Native.Get(t,"coordinates")!)).ToArray();
            }
            internal bool Matches(object state)
            {
                if(!Native.Same(Map,Native.Get(state,"Map")))return false;
                foreach(var cell in Cells)
                    if(ReadStamp(Map,cell.Coordinates,out _,out _)!=cell.Stamp)return false;
                return true;
            }
        }
        static object? FrozenEffect;
        static NeighborStamp ReadStamp(object map,object coordinates,out object? tile,out object? body)
        {
            tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,map,coordinates);
            body=tile is null?null:Native.Get(tile,"unit");
            if(tile is null)return default;
            if(body is null)return new(Native.Pointer(tile),0,0,0,0,0,false,false,0,0,false);
            var at=Native.Get(body,"coordinates")!;
            return new(Native.Pointer(tile),Native.Pointer(body),Convert.ToUInt32(Native.Get(body,"id")),Native.Int(Native.Get(body,"type")),
                Convert.ToByte(Native.Get(body,"owner")),Convert.ToUInt16(Native.Get(body,"health")),(bool)Native.Get(body,"moved")!,
                (bool)Native.Get(body,"attacked")!,Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")),
                (bool)Native.Invoke(Native.M("UnitState","HasEffect","UnitEffect"),body,FrozenEffect??=Enum.Parse(Native.T("UnitEffect"),"Frozen"))!);
        }
        static NeighborCell ReadCell(object map,object coordinates)
        {var stamp=ReadStamp(map,coordinates,out var tile,out var body);return new(coordinates,tile,body,stamp);}
        static IEnumerable<Option> Options(object state,uint id,NativeExchangeKind kind)
        {
            object?[] args={id,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,args)!||args[1] is null)yield break;
            foreach(var tile in NeighborhoodTiles(state,args[1]!))
            {
                var b=Native.Get(tile,"unit");if(b is null)continue;
                uint other=Convert.ToUInt32(Native.Get(b,"id"));if(other==id)continue;
                NativeExchangeCommands.TryOffer(state,id,other,kind,false,out var keep,out _);
                NativeExchangeCommands.TryOffer(state,id,other,kind,true,out var swap,out _);
                if(keep is not null||swap is not null)yield return new(other,keep,swap);
            }
        }
        static bool HasOptions(object bar,object unit,object us,object state,object client,object player,object map,NativeGameStateEnvelope.Snapshot snapshot,NativeExchangeKind kind)
        {
            object at=Native.Get(us,"coordinates")!;
            var stamp=new EligibilityStamp(Convert.ToUInt32(Native.Get(state,"CurrentTurn")),Convert.ToByte(Native.Get(state,"CurrentPlayer")),
                Convert.ToUInt16(Native.Get(state,"CurrentCommand")),snapshot.Revision,Convert.ToUInt32(Native.Get(us,"id")),Convert.ToByte(Native.Get(us,"owner")),
                Native.Int(Native.Get(us,"type")),Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")),
                (bool)Native.Get(us,"moved")!,(bool)Native.Get(us,"attacked")!,Convert.ToUInt16(Native.Get(us,"health")),
                (bool)Native.Invoke(Native.M("UnitState","HasEffect","UnitEffect"),us,Enum.Parse(Native.T("UnitEffect"),"Frozen"))!,Researched(player,kind));
            var key=(Native.Pointer(bar),kind);
            if(EligibilityCache.TryGetValue(key,out var old)&&old.Stamp==stamp&&ReferenceEquals(old.LedgerBinding,snapshot)&&old.Neighborhood.Matches(state)&&Native.Same(old.Bar,bar)&&Native.Same(old.Unit,unit)&&
                Native.Same(old.UnitState,us)&&Native.Same(old.State,state)&&Native.Same(old.Client,client)&&Native.Same(old.Player,player)&&Native.Same(old.Map,map))
                return old.Available;
            var neighborhood=new NeighborhoodSnapshot(state,us);
            // Research mutates the current native player's tech list in place.
            // It need not replace the GameState, increment our ledger revision,
            // or wait for a turn; invalidate this bounded UI cache on that fact.
            // Native births can replace the immutable ledger binding without a
            // command revision. Observe that binding as well: a negative offer
            // sampled before paid enrollment must not survive its completion.
            // Native planning still decides action budget and partner legality.
            bool available=stamp.Researched&&Options(state,stamp.Id,kind).Any();
            if(!EligibilityCache.ContainsKey(key)&&EligibilityCache.Count>=MaxEligibilityBars)EligibilityCache.Clear();
            EligibilityCache[key]=new(bar,unit,us,state,client,player,map,snapshot,stamp,neighborhood,available);
            return available;
        }
        internal static void Sync(object bar,object? unit,bool allowed)
        {
            foreach(var kind in Kinds)SyncKind(bar,unit,allowed,kind);
        }
        static void SyncKind(object bar,object? unit,bool allowed,NativeExchangeKind kind)
        {
            try
            {
                if(Ending)return;
                if(Active is Session choosing&&Native.Same(choosing.Bar,bar))
                {
                    if(Valid(choosing))return;
                    Clear();
                }
                if(NativeSpecialActionUi.Choosing||!NativeRegroupSession.Enabled||!allowed||unit is null||Choosing||!Native.UnityAlive(bar)||!Native.UnityAlive(unit)){Remove(bar,kind);return;}
                var state=Native.Static("GameManager","GameState");var us=Native.Get(unit,"UnitState");
                var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");
                if(state is null||us is null||client is null||player is null||!NativeRegroupSession.Ready(state)){Remove(bar,kind);return;}
                var snapshot=NativeGameStateEnvelope.Get(state);var map=Native.Get(state,"Map");
                if(snapshot is null||map is null){Remove(bar,kind);return;}
                var key=(Native.Pointer(bar),kind);
                if(Controls.TryGetValue(key,out var old)&&(!Owned(old)||!Native.Same(old.Unit,unit)))Remove(bar,kind);
                if(!HasOptions(bar,unit,us,state,client,player,map,snapshot,kind)){RemoveControl(bar,kind);return;}
                if(Controls.ContainsKey(key))return;
                object button=Native.Invoke(Native.M("InteractionBar","CreateRoundBottomBarButton","System.String","System.Boolean"),bar,Caption(kind),true)!;
                var c=new Control{Bar=bar,Unit=unit,Button=button,Buttons=Native.Get(bar,"buttons")!,Kind=kind};
                c.Callback=Native.ButtonCallback(()=>
                {
                    try
                    {
                        if(Ending||!Controls.TryGetValue(key,out var current)||!ReferenceEquals(current,c)||!Owned(c))return;
                        if(Active is Session choosing&&ReferenceEquals(choosing.Control,c)){Cancel();return;}
                        if(Choosing||NativeSpecialActionUi.Choosing||!MimicPresentationUi.CanSmuggleView(unit))return;
                        var game=Native.Static("GameManager","GameState");var activeClient=Native.Static("GameManager","Client");var activePlayer=Native.Static("GameManager","LocalPlayer");
                        if(game is null||activeClient is null||activePlayer is null||!Native.Same(Native.Get(bar,"unit"),unit))return;
                        uint id=Convert.ToUInt32(Native.Get(Native.Get(unit,"UnitState")!,"id"));
                        var options=Options(game,id,kind).ToArray();
                        var interaction=MimicPresentationUi.ActionInteraction(unit);var map=Native.Static("MapRenderer","Current");
                        var currentSnapshot=NativeGameStateEnvelope.Get(game);
                        if(options.Length==0||interaction is null||map is null||currentSnapshot is null){Remove(bar,kind);return;}
                        var session=new Session{Bar=bar,Unit=unit,State=game,Client=activeClient,Player=activePlayer,Interaction=interaction,Map=map,Initiator=id,Kind=kind,Control=c,
                            Turn=Convert.ToUInt32(Native.Get(game,"CurrentTurn")),Command=Convert.ToUInt16(Native.Get(game,"CurrentCommand")),
                            Revision=currentSnapshot.Revision,LedgerBinding=currentSnapshot,Neighborhood=new NeighborhoodSnapshot(game,Native.Get(unit,"UnitState")!),Options=options,Due=TickNumber+1};
                        Active=session;MimicHoldInput.Clear();Native.Set(c.Button,"text","Cancel");
                        foreach(var other in Controls.Values.Where(x=>Native.Same(x.Bar,bar)&&!ReferenceEquals(x,c)&&Owned(x)))
                            Native.Set(other.Button,"ButtonEnabled",false);
                        Native.Invoke(Native.M("Unit","ClearTileOverlays"),unit);
                        Native.Invoke(Native.M("Unit","ClearUnitOverlays"),unit);
                        RenderTargets(session);
                    }
                    catch(Exception e){Entry.Note("regroup-target-error",e.ToString());Clear();}
                });
                Native.Set(button,"OnClicked",c.Callback);Native.Set(button,"OnClickedWhenDisabled",null);
                Native.Set(button,"Cost",-1f);Native.Set(button,"buttonActive",true);Native.Set(button,"ButtonEnabled",true);
                Controls[key]=c;
                var glyph=kind==NativeExchangeKind.Regroup?UnitArtAssets.Icon():BranchArtAssets.DisguiseAction();
                if(glyph is not null)Native.Invoke(Native.M("UIRoundButton","SetSprite","UnityEngine.Sprite","System.Boolean"),button,glyph,true);
                if(kind==NativeExchangeKind.Disguise)NativeButtonArt.WhiteBackground(button);
                Native.Invoke(Native.M("UIButtonBase","SetActive","System.Boolean"),button,true);
                Native.Invoke(Native.M("InteractionBar","SetupButtonNavigation"),bar);MimicPresentationUi.ReflowBar(bar);
            }
            catch(Exception e){Entry.Note("regroup-ui-error",e.ToString());Remove(bar,kind);}
        }
        static bool Valid(Session s)
        {
            if(!ReferenceEquals(Active,s)||!Controls.TryGetValue((Native.Pointer(s.Bar),s.Kind),out var control)||
                !ReferenceEquals(control,s.Control)||!Owned(control)||!Native.UnityAlive(s.Unit)||!Native.UnityAlive(s.Bar)||!Native.UnityAlive(s.Map)||
                !Native.Same(Native.Static("GameManager","GameState"),s.State)||!Native.Same(Native.Static("GameManager","Client"),s.Client)||
                !Native.Same(Native.Static("GameManager","LocalPlayer"),s.Player)||!Native.Same(Native.Get(s.Bar,"unit"),s.Unit)||
                !Researched(s.Player,s.Kind)||
                !Native.Same(Native.Static("MapRenderer","Current"),s.Map)||!Native.Same(MimicPresentationUi.ActionInteraction(s.Unit),s.Interaction)||
                Native.Int(Native.Get(s.State,"CurrentPlayer"))!=Native.Int(Native.Get(s.Player,"Id"))||
                Convert.ToUInt32(Native.Get(s.State,"CurrentTurn"))!=s.Turn||Convert.ToUInt16(Native.Get(s.State,"CurrentCommand"))!=s.Command||
                !ReferenceEquals(NativeGameStateEnvelope.Get(s.State),s.LedgerBinding)||
                (bool)Native.Get(s.Client,"IsWaitingForCommand")!||(bool)Native.Get(Native.Get(s.Client,"ActionManager")!,"IsProcessing")!)return false;
            var unit=Native.Get(s.Unit,"UnitState");
            return unit is not null&&Convert.ToUInt32(Native.Get(unit,"id"))==s.Initiator&&
                Native.Int(Native.Get(unit,"owner"))==Native.Int(Native.Get(s.Player,"Id"))&&s.Neighborhood.Matches(s.State)&&
                MimicPresentationUi.CanContinueAction(s.Unit,null)&&NativeRegroupSession.Ready(s.State);
        }
        static void RetireMarkers(Session s)
        {
            if(Native.UnityAlive(s.MarkerRoot))
            {
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),s.MarkerRoot,false);
                Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,s.MarkerRoot);
            }
            s.MarkerRoot=null;s.Markers.Clear();
        }
        const int DestinationSize=96;
        static object? DestinationTexture,DestinationArt;
        static bool DestinationAttempted;
        internal static byte[] DestinationPixels()
        {
            // A geometric UI indicator, generated once. No atlas readback or
            // user-art conversion is involved. White alpha can be tinted green
            // even when the native attack sprite has red baked into its pixels.
            var pixels=new byte[DestinationSize*DestinationSize*4];
            for(int y=0;y<DestinationSize;y++)for(int x=0;x<DestinationSize;x++)
            {
                float dx=x+.5f-DestinationSize*.5f,dy=y+.5f-DestinationSize*.5f,r=MathF.Sqrt(dx*dx+dy*dy);
                float outer=MathF.Abs(r-32.5f)-2.2f,inner=MathF.Abs(r-14f)-1.8f,dot=r-2.8f;
                float vertical=MathF.Max(MathF.Abs(dx)-1.8f,MathF.Abs(MathF.Abs(dy)-34f)-9f);
                float horizontal=MathF.Max(MathF.Abs(dy)-1.8f,MathF.Abs(MathF.Abs(dx)-34f)-9f);
                float distance=MathF.Min(MathF.Min(outer,inner),MathF.Min(dot,MathF.Min(vertical,horizontal)));
                int at=(y*DestinationSize+x)*4;pixels[at]=pixels[at+1]=pixels[at+2]=255;
                pixels[at+3]=(byte)Math.Clamp((int)MathF.Round(Math.Clamp(.5f-distance,0f,1f)*255f),0,255);
            }
            return pixels;
        }
        static object DestinationSprite()
        {
            if(Native.UnityAlive(DestinationArt)&&Native.UnityAlive(DestinationTexture))return DestinationArt!;
            if(DestinationAttempted)throw new InvalidOperationException("Green Regroup target artwork is unavailable.");
            DestinationAttempted=true;object? texture=null,sprite=null;
            try
            {
                texture=Activator.CreateInstance(Native.T("UnityEngine.Texture2D"),DestinationSize,DestinationSize,
                    Enum.Parse(Native.T("UnityEngine.TextureFormat"),"RGBA32"),false)!;
                Native.Set(texture,"filterMode",Enum.Parse(Native.T("UnityEngine.FilterMode"),"Bilinear"));
                Native.Set(texture,"wrapMode",Enum.Parse(Native.T("UnityEngine.TextureWrapMode"),"Clamp"));
                var bytes=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
                var data=Activator.CreateInstance(bytes,new object[]{DestinationPixels()})!;
                Native.Invoke(Native.M("UnityEngine.Texture2D","LoadRawTextureData",bytes.FullName!),texture,data);
                Native.Invoke(Native.M("UnityEngine.Texture2D","Apply","System.Boolean","System.Boolean"),texture,false,true);
                sprite=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single","System.UInt32","UnityEngine.SpriteMeshType"),
                    null,texture,Activator.CreateInstance(Native.T("UnityEngine.Rect"),0f,0f,(float)DestinationSize,(float)DestinationSize),
                    Activator.CreateInstance(Native.T("UnityEngine.Vector2"),.5f,.5f),100f,(uint)0,Enum.Parse(Native.T("UnityEngine.SpriteMeshType"),"FullRect"))!;
                if(!Native.UnityAlive(sprite))throw new InvalidOperationException("Green Regroup Sprite creation failed.");
                DestinationTexture=texture;DestinationArt=sprite;return sprite;
            }
            catch
            {
                if(Native.UnityAlive(sprite))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,sprite);
                if(Native.UnityAlive(texture))Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,texture);
                throw;
            }
        }
        static float Axis(object vector,int index)=>Convert.ToSingle(Native.Invoke(Native.M("UnityEngine.Vector3","get_Item","System.Int32"),vector,index));
        static object FitDestination(object nativeSprite,object nativeTransform)
        {
            var bounds=Native.Get(nativeSprite,"bounds")!;var size=Native.Get(bounds,"size")!;var scale=Native.Get(nativeTransform,"lossyScale")!;
            float width=Axis(size,0),height=Axis(size,1);
            if(!float.IsFinite(width)||!float.IsFinite(height)||width<=0||height<=0)throw new InvalidOperationException("Native target bounds are invalid.");
            return Activator.CreateInstance(Native.T("UnityEngine.Vector3"),Axis(scale,0)*width/(DestinationSize/100f),Axis(scale,1)*height/(DestinationSize/100f),Axis(scale,2))!;
        }
        static Marker MarkerFor(Session s,uint id,bool swap)
        {
            object?[] args={id,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),s.State,args)!||args[1] is null)
                throw new InvalidOperationException("Regroup target unit disappeared.");
            object us=args[1]!,at=Native.Get(us,"coordinates")!;
            object tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),s.Map,at)!;
            object body=Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),s.Map,id)!;
            object data=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(s.State,"Map"),at)!;
            if(!Native.UnityAlive(tile)||!Native.UnityAlive(body)||!Native.Same(Native.Get(tile,"Data"),data)||!Native.Same(Native.Get(tile,"unit"),body)||
                !Native.Same(Native.Get(body,"UnitState"),us)||!Native.Same(Native.Get(data,"unit"),us)||(bool)Native.Get(tile,"IsHidden")!||
                !(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,Convert.ToByte(Native.Get(s.Player,"Id")))!||
                !(bool)Native.Invoke(Native.M("Unit","GetVisible"),body)!||(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),body)!)
                throw new InvalidOperationException("Regroup target no longer has an exact visible map body.");
            object source=Native.Get(tile,"overlayRenderer")!,sprite=Native.Get(tile,"attackOverlay")!;
            if(!Native.UnityAlive(source)||!Native.UnityAlive(sprite))throw new InvalidOperationException("Native Regroup bullseye artwork is unavailable.");
            object go=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{"Deception "+Caption(s.Kind)+" target"})!;
            try
            {
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,false);
                object type=Native.Invoke(Native.M("Il2CppInterop.Runtime.Il2CppType","From","System.Type"),null,Native.T("UnityEngine.SpriteRenderer"))!;
                object renderer=Native.As(Native.Invoke(Native.M("UnityEngine.GameObject","AddComponent","Il2CppSystem.Type"),go,type)!,"UnityEngine.SpriteRenderer");
                object sourceTransform=Native.Get(source,"transform")!,transform=Native.Get(go,"transform")!;
                Native.Invoke(Native.M("UnityEngine.Transform","SetParent","UnityEngine.Transform","System.Boolean"),transform,Native.Get(s.MarkerRoot!,"transform"),false);
                Native.Set(transform,"localPosition",Native.Get(sourceTransform,"position"));Native.Set(transform,"localRotation",Native.Get(sourceTransform,"rotation"));
                Native.Set(transform,"localScale",Native.Get(sourceTransform,"lossyScale"));Native.Set(go,"layer",Native.Get(Native.Get(source,"gameObject")!,"layer"));
                bool destination=s.Partner is not null;
                if(destination)
                {
                    // Match the native marker's visible bounds and center even
                    // if its packed sprite has an offset pivot or non-square crop.
                    var center=Native.Get(Native.Get(sprite,"bounds")!,"center")!;
                    Native.Set(transform,"localPosition",Native.Invoke(Native.M("UnityEngine.Transform","TransformPoint","UnityEngine.Vector3"),sourceTransform,center));
                    Native.Set(transform,"localScale",FitDestination(sprite,sourceTransform));
                }
                Native.Set(renderer,"sprite",destination?DestinationSprite():sprite);Native.Set(renderer,"sharedMaterial",Native.Get(source,"sharedMaterial"));
                Native.Set(renderer,"color",Activator.CreateInstance(Native.T("UnityEngine.Color"),destination?new object[]{.15f,1f,.35f,1f}:new object[]{1f,1f,1f,1f}));
                Native.Set(renderer,"sortingLayerID",Native.Get(source,"sortingLayerID"));Native.Set(renderer,"sortingOrder",Native.Int(Native.Get(source,"sortingOrder"))+1);
                Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),go,true);
                return new(tile,body,data,go,renderer,swap);
            }
            catch{Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,go);throw;}
        }
        static void RenderTargets(Session s)
        {
            RetireMarkers(s);
            s.MarkerRoot=Activator.CreateInstance(Native.T("UnityEngine.GameObject"),new object[]{Caption(s.Kind)+(s.Partner is null?": choose ally":": choose Mimic destination")})!;
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),s.MarkerRoot,false);
            if(s.Partner is Option chosen)
            {
                if(chosen.Keep is not null)s.Markers.Add(MarkerFor(s,s.Initiator,false));
                if(chosen.Swap is not null)s.Markers.Add(MarkerFor(s,chosen.Id,true));
            }
            else foreach(var option in s.Options)s.Markers.Add(MarkerFor(s,option.Id,false));
            if(!Valid(s))throw new InvalidOperationException("Regroup targeting context changed before display.");
            Native.Invoke(Native.M("UnityEngine.GameObject","SetActive","System.Boolean"),s.MarkerRoot,true);
        }
        static bool MarkerValid(Session s,Marker marker)
        {
            if(!Native.UnityAlive(marker.Tile)||!Native.UnityAlive(marker.Unit)||!Native.UnityAlive(marker.Object)||
                !Native.Same(Native.Get(marker.Tile,"Data"),marker.Data)||!Native.Same(Native.Get(marker.Tile,"unit"),marker.Unit)||
                (bool)Native.Get(marker.Tile,"IsHidden")!||!(bool)Native.Invoke(Native.M("Unit","GetVisible"),marker.Unit)!||
                (bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),marker.Unit)!)return false;
            var us=Native.Get(marker.Unit,"UnitState")!;var at=Native.Get(us,"coordinates")!;
            return Native.Same(Native.Get(marker.Data,"unit"),us)&&
                Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),s.Map,Convert.ToUInt32(Native.Get(us,"id"))),marker.Unit)&&
                Native.Same(Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),s.Map,at),marker.Tile)&&
                (bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),marker.Data,Convert.ToByte(Native.Get(s.Player,"Id")))!;
        }
        static bool IntentValid(Session s,NativeExchangeIntent? intent)=>intent is not null&&NativeExchangePlanning.TryPlan(s.State,intent,out _,out _);
        public static bool Released(object __instance,object? __0,int __1)
        {
            var s=Active;if(s is null)return true;
            try
            {
                if(!Valid(s)||!Native.Same(s.Interaction,__instance)){Clear();return false;}
                if(TickNumber<s.Due)return false; // Never use one release for both target stages.
                if((bool)Native.Get(__instance,"wasDownOverUI")!||(bool)Native.Static("PolytopiaInput","OutsideWindow")!||
                    (bool)Native.Static("CameraController","IsDragging")!||(bool)Native.Static("CameraController","IsBlocked")!)return false;
                var touches=Native.Items(Native.Static("PolytopiaInput","CurrentTouches")!).ToArray();
                // The native desktop mouse is represented by an Ended touch at
                // index zero. IsTouchValidTap does not bounds-check negatives;
                // never pass an invented mouse index or a vanished contact.
                if(touches.Length!=1||__1!=0||Native.Get(touches[0],"phase")?.ToString()!="Ended"||
                    !(bool)Native.Invoke(Native.M("ClientInteraction","IsTouchValidTap","System.Int32"),__instance,__1)!)return false;
                object position=Native.Get(touches[0],"position")!;
                if((bool)Native.Invoke(Native.M("PolytopiaInput","IsPositionOverUIObject","UnityEngine.Vector2","System.Int32"),null,position,-1)!)return false;
                var marker=s.Markers.SingleOrDefault(m=>Native.Same(m.Tile,__0));
                if(marker is null){Cancel();return false;}
                if(!MarkerValid(s,marker)){Clear();return false;}
                if(s.Partner is null)
                {
                    uint id=Convert.ToUInt32(Native.Get(Native.Get(marker.Unit,"UnitState")!,"id"));
                    var chosen=s.Options.Single(x=>x.Id==id);
                    if((chosen.Keep is not null&&!IntentValid(s,chosen.Keep))||(chosen.Swap is not null&&!IntentValid(s,chosen.Swap))){Clear();return false;}
                    s.Partner=chosen;s.Due=TickNumber+1;RenderTargets(s);return false;
                }
                var intent=marker.Swap?s.Partner.Swap:s.Partner.Keep;
                if(intent is null){Clear();return false;}
                object command=NativeExchangeCommands.Create(intent);
                if(!NativeExchangeCommands.Valid(command,s.State,true,out _)){NativeExchangeCommands.Forget(command);Clear();return false;}
                Clear();Native.Invoke(Native.M("ClientBase","SendCommand","CommandBase"),s.Client,command);return false;
            }
            catch(Exception e){Entry.Note("regroup-target-click-error",e.ToString());Clear();return false;}
        }
        public static void Tick()
        {
            TickNumber++;var s=Active;if(s is null)return;
            try{if(!Valid(s)||s.Markers.Any(m=>!MarkerValid(s,m)))Clear();}
            catch(Exception e){Entry.Note("regroup-ui-tick-error",e.ToString());Clear();}
        }
        internal static void Remove(object bar)
        {
            foreach(var kind in Kinds)Remove(bar,kind);
        }
        static void Remove(object bar,NativeExchangeKind kind)
        {
            EligibilityCache.Remove((Native.Pointer(bar),kind));RemoveControl(bar,kind);
        }
        static void RemoveControl(object bar,NativeExchangeKind kind)
        {
            if(!Controls.Remove((Native.Pointer(bar),kind),out var c)||!Owned(c))return;
            Native.Set(c.Button,"OnClicked",null);Native.Set(c.Button,"OnClickedWhenDisabled",null);Native.Set(c.Button,"ButtonEnabled",false);
            Native.Invoke(Native.M(c.Buttons.GetType(),"Remove","UIRoundButton"),c.Buttons,c.Button);
            Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Native.Get(c.Button,"gameObject"));
        }
        public static void NativeClear(object __instance)
        {
            if(Active is Session s&&Native.Same(s.Bar,__instance))Clear();
            foreach(var kind in Kinds)
            {
                EligibilityCache.Remove((Native.Pointer(__instance),kind));
                if(!Controls.Remove((Native.Pointer(__instance),kind),out var c)||!Owned(c))continue;
                Native.Set(c.Button,"OnClicked",null);Native.Set(c.Button,"OnClickedWhenDisabled",null);
            }
        }
        static void Cancel()=>EndTargets(true);
        public static void Clear()=>EndTargets(false);
        static void EndTargets(bool restoreOverlays)
        {
            EligibilityCache.Clear();var s=Active;Active=null;if(s is null||Ending)return;
            Ending=true;
            try
            {
                RetireMarkers(s);
                foreach(var c in Controls.Values.Where(x=>Native.Same(x.Bar,s.Bar)&&Owned(x)))
                {Native.Set(c.Button,"text",Caption(c.Kind));Native.Set(c.Button,"ButtonEnabled",true);}
                // Explicit cancellation may restore move hints. Teardown hooks
                // must not invoke a native UI rebuild inside ClearButtons,
                // DeselectUnit, OnDisable, ReturnToMenu or session retirement.
                if(restoreOverlays&&Native.UnityAlive(s.Unit)&&Native.Same(Native.Static("GameManager","GameState"),s.State)&&
                    Native.Same(Native.Static("GameManager","Client"),s.Client)&&Native.Same(MimicPresentationUi.ActionInteraction(s.Unit),s.Interaction)&&
                    MimicPresentationUi.CanContinueAction(s.Unit,null))Native.Invoke(Native.M("Unit","UpdateOverlays"),s.Unit);
            }
            catch(Exception e){Entry.Note("regroup-target-clear-error",e.ToString());}
            finally{Ending=false;}
        }
    }
}
