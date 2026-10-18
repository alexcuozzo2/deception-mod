using System;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Public native Spy artwork is independent of private disguise inspection.
    // This art projection never creates a ledger row or grants a local truth view.
    internal static class NativeSpyPresentation
    {
        internal static void Install(Harmony h)
        {
            _=Native.M("UnitPopup","get_data");
            Native.Hook(h,Native.M("UnitPopup","set_UnitData","Polytopia.Data.UnitData"),typeof(NativeSpyPresentation),null,nameof(PopupPopulated));
        }
        public static void PopupPopulated(object __instance)
        {
            try
            {
                var data=Native.Get(__instance,"data");
                if(data is not null&&NativeFoggerPresentation.IsForm(Native.EnumName(Native.Get(data,"type")!)))return;
                MimicAbilityTags.Remove(__instance);
                if(MimicPresentationUi.ShowDefinitionAbilities(__instance,data))return;
                // The native popup has already populated the chosen actual or
                // apparent form exactly once. Read that public/display data;
                // inspecting a Spy costume must never reveal its real body.
                if(data is null||!SpyCatalog.IsSpyId(Native.EnumName(Native.Get(data,"type")!)))return;
                MimicAbilityTags.ShowSpy(__instance,()=>Native.UnityAlive(__instance)&&
                    Native.Same(Native.Get(__instance,"data"),data)&&
                    SpyCatalog.IsSpyId(Native.EnumName(Native.Get(data,"type")!)));
            }
            catch(Exception e){Entry.Note("spy-ability-tag-error",e.ToString());}
        }
        sealed record Bar(object Widget,object Unit,object Container,object Data,object Source,object Player,uint Id,int Color,int Tribe,int Skin,bool Boat,bool Spy,bool Aquarion,bool Fogger,bool Ready,object[] Children,object State,object NativeOwner,object Client,object Map,object Viewer,bool Apparent,LedgerAppearance? Appearance);
        static Bar? PaintedBar;
        internal static Func<object,bool>? Ready;
        internal static void Clear()=>RemoveBar(restore:false);
        internal static void BeforeBar(object bar)=>RemoveBar(bar);
        internal static void RemoveBar(object? bar=null,bool restore=true)
        {
            var old=PaintedBar;if(old is null||(bar is not null&&!Native.Same(old.Widget,bar)))return;PaintedBar=null;
            try
            {
                if(Owned(old))
                {
                    if(restore&&Native.UnityAlive(old.Source))MimicInspectionControls.RestorePortrait(old.Data);
                    else Native.Invoke(Native.M("UIDuplicatedSprites","Clear"),old.Container);
                }
            }
            catch(Exception e){Entry.Note("public-unit-portrait-restore-error",e.ToString());}
        }
        static bool Owned(Bar old)=>Native.UnityAlive(old.Widget)&&Native.Same(Native.Get(old.Widget,"unit"),old.Unit)&&
            Native.UnityAlive(old.Container)&&Native.Same(Native.Get(old.Widget,"duplicatedSprites"),old.Container)&&
            Native.Same(Native.Get(old.Widget,"spriteDuplicationData"),old.Data)&&Native.Same(Native.Get(old.Data,"source"),old.Source)&&
            Native.Same(Native.Get(old.Data,"container"),old.Container)&&SameChildren(old,Children(old.Container));
        static bool SameChildren(Bar old,object[] children)=>old.Children.Length==children.Length&&old.Children.Zip(children,Native.Same).All(x=>x);
        internal static void Tick()
        {
            var old=PaintedBar;if(old is null)return;
            try
            {
                if(!Owned(old)){PaintedBar=null;return;}
                if(!Native.Same(old.Client,Native.Static("GameManager","Client"))||!Native.Same(old.Map,Native.Static("MapRenderer","Current"))||
                    !Native.Same(old.Viewer,Native.Static("GameManager","LocalPlayer"))||!Native.Same(old.State,Native.Static("GameManager","GameState")))
                {RemoveBar(restore:false);return;}
                bool independent=MimicPresentationState.TryProject(Native.Get(old.Unit,"UnitState")!,out var p);
                if(!old.Apparent&&independent&&!p.Exposed)
                {
                    // A new mask revokes public actual artwork immediately.
                    // The normal bar path decides whether this viewer gets a
                    // private pair or the public apparent form.
                    // Native duplication must read the newly displayed mask,
                    // not an old public Spy overlay still attached to the map.
                    bool visible=Visible(old.Unit,p);if(visible)MimicMapInspection.Refresh(old.Unit);
                    RemoveBar(restore:visible);MimicPresentationUi.BarReady(old.Widget);return;
                }
                bool projected=independent||!old.Apparent&&TryProject(old.Unit,out p);
                if(!projected||!Visible(old.Unit,p)||old.Apparent&&!NativeFoggerPresentation.CustomForm(p.NativeId)||!old.Apparent&&!p.Exposed&&!NativeFoggerPresentation.CustomActual(p))
                {RemoveBar(restore:false);return;}
                // Head completion and changed copied cosmetics are retried on
                // this already-owned public portrait, without selecting a unit.
                ApplyBar(old.Widget,old.Unit,p,old.Apparent);
            }
            catch(Exception e){RemoveBar(restore:false);Entry.Note("public-unit-portrait-refresh-error",e.ToString());}
        }
        internal static bool Visible(object unit,MimicProjection p)
        {
            var map=Native.Static("MapRenderer","Current");var client=Native.Static("GameManager","Client");var viewer=Native.Static("GameManager","LocalPlayer");
            return Native.UnityAlive(unit)&&Native.UnityAlive(map)&&client is not null&&viewer is not null&&
                Native.Same(Native.Static("GameManager","GameState"),p.State)&&Native.Same(Native.Get(client,"GameState"),p.State)&&
                (bool)Native.Get(client,"IsReady")!&&
                Native.Same(Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,p.UnitId),unit)&&
                !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!&&
                (bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,Convert.ToByte(Native.Get(viewer,"Id")))!&&
                (bool)Native.Invoke(Native.M("Unit","GetVisible"),unit)!&&!(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),unit)!;
        }
        static object[] Children(object container)
        {
            var rect=Native.Get(container,"rectTransform")!;int count=Native.Int(Native.Get(rect,"childCount"));
            return Enumerable.Range(0,count).Select(i=>Native.Invoke(Native.M("UnityEngine.Transform","GetChild","System.Int32"),rect,i)!).ToArray();
        }
        internal static void ApplyBar(object bar,object unit,MimicProjection p,bool apparent=false)
        {
            if(!Visible(unit,p)||!Native.Same(Native.Get(bar,"unit"),unit)){RemoveBar(bar,restore:false);return;}
            if(apparent&&(p.Appearance is null||!NativeFoggerPresentation.CustomForm(p.NativeId)))return;
            var container=Native.Get(bar,"duplicatedSprites");var data=Native.Get(bar,"spriteDuplicationData");
            var source=data is null?null:Native.Get(data,"source");var player=Native.Get(unit,"Owner");
            if(!Native.UnityAlive(container)||data is null||!Native.Same(Native.Get(data,"container"),container)||!Native.UnityAlive(source)||player is null||
                !(bool)Native.Invoke(Native.M("UnityEngine.Transform","IsChildOf","UnityEngine.Transform"),source,Native.Get(unit,"transform"))!)return;
            var nativeOwner=player;var old=PaintedBar;
            if(apparent)
            {
                var appearance=p.Appearance!;
                // Reuse the detached cosmetic player for value-identical ledger
                // reads. The actual owner is retained only for pool identity.
                player=old is not null&&old.Apparent&&Native.Same(old.NativeOwner,nativeOwner)&&old.Appearance is LedgerAppearance before&&
                    before.TribeVisual==appearance.TribeVisual&&before.SourceColor==appearance.SourceColor?old.Player:AppearancePlayer(player,appearance);
            }
            else player=NativeRogueVisuals.ArtOwner(p.State,Native.Get(unit,"UnitState")!,player);
            int color=Native.Int(Native.Get(player,"color")),tribe=Native.Int(Native.Invoke(Native.M("PlayerExtensions","GetVisualTribeType","PlayerState"),null,player)),
                skin=Native.Int(Native.Invoke(Native.M("PlayerState","GetVisualSkin"),player));
            bool fogger=apparent?NativeFoggerPresentation.IsForm(p.NativeId):NativeFoggerPresentation.IsFogger(p);
            bool ready=fogger||UnitArtAssets.Prepare(player),spy=apparent?SpyCatalog.IsSpyId(p.NativeId):IsSpy(p),aquarion=apparent?p.NativeId==SpyCatalog.AquarionId:p.ActualAmphibiousMimic||p.ActualNativeId==SpyCatalog.AquarionId,boat=!apparent&&p.ActualMimicBoat;
            var children=Children(container!);
            if(old is not null&&Native.Same(old.State,p.State)&&old.Apparent==apparent&&Native.Same(old.NativeOwner,nativeOwner)&&Native.Same(old.Widget,bar)&&Native.Same(old.Unit,unit)&&Native.Same(old.Container,container)&&
                Native.Same(old.Data,data)&&Native.Same(old.Source,source)&&Native.Same(old.Player,player)&&old.Id==p.UnitId&&old.Color==color&&old.Tribe==tribe&&old.Skin==skin&&
                old.Boat==boat&&old.Spy==spy&&old.Aquarion==aquarion&&old.Fogger==fogger&&old.Ready==ready&&SameChildren(old,children))return;
            try{MimicInspectionControls.ApplyActualPortrait(container!,data,player,0.8f,boat,spy,aquarion,yOffset:spy?-4f:0,fogger:fogger);}
            catch(Exception e){Entry.Note("public-unit-portrait-error",e.ToString());}
            // Native refresh, selection/context changes or completed head art
            // retry; a failed allocation does not repeat every selected-frame.
            PaintedBar=new(bar,unit,container!,data,source!,player,p.UnitId,color,tribe,skin,boat,spy,aquarion,fogger,ready,Children(container!),p.State,nativeOwner,Native.Static("GameManager","Client")!,Native.Static("MapRenderer","Current")!,Native.Static("GameManager","LocalPlayer")!,apparent,p.Appearance);
        }
        internal static object AppearancePlayer(object owner,LedgerAppearance appearance)
        {
            const string tribe="PolytopiaBackendBase.Common.TribeType",skin="PolytopiaBackendBase.Common.SkinType";
            object Member(string type,string value)=>Enum.IsDefined(Native.T(type),value)?Enum.Parse(Native.T(type),value,false):
                throw new InvalidOperationException("Unknown copied unit cosmetic "+value);
            var player=Native.DetachedClone(owner,"PlayerState");
            if(Native.Same(player,owner))throw new InvalidOperationException("Copied unit player aliases its live owner.");
            Native.Set(player,"tribe",Member(tribe,appearance.TribeVisual.Tribe));Native.Set(player,"tribeMix",Enum.ToObject(Native.T(tribe),0));
            Native.Set(player,"skinType",Member(skin,appearance.TribeVisual.Skin));Native.Set(player,"color",appearance.SourceColor);return player;
        }
        internal static bool ApparentSpyBadge(MimicProjection p)=>SpyCatalog.IsSpyId(p.NativeId)||SpyCatalog.IsSpyId(p.Appearance?.Passenger?.Form??"");
        internal static bool IsSpy(MimicProjection p)=>!p.ActualIsMimic&&SpyCatalog.IsSpyId(p.ActualNativeId);
        internal static bool TryProject(object unit,out MimicProjection projection)
        {
            projection=null!;
            if(!RecruitmentHooks.Registered||!Native.UnityAlive(unit))return false;
            var body=Native.Get(unit,"UnitState");
            if(body is null)return false;
            string native=Native.EnumName(Native.Get(body,"type")!);var passenger=Native.Get(body,"passengerUnit");
            bool spy=NativeFoggerPresentation.CustomForm(native);
            if(!spy&&(passenger is null||!NativeFoggerPresentation.CustomForm(Native.EnumName(Native.Get(passenger,"type")!))))return false;
            if(!spy&&!Array.Exists(RecruitmentCatalog.NavalCopyOnly,x=>x==native))return false;
            var game=Native.Static("GameManager","GameState");if(game is null)return false;
            uint id=Convert.ToUInt32(Native.Get(body,"id"));object?[] args={id,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),game,args)!||!Native.Same(args[1],body))return false;
            if(MimicPresentationState.ReadIdentity is not null&&(Ready is null||!Ready(game)))return false;
            var row=MimicPresentationState.ReadIdentity?.Invoke(game,id);
            // Failure to project an independently disguised Spy must not expose
            // its real artwork as a fallback. Only an unmasked native root fits.
            if(row is not null&&(!row.Alive||row.Independent||row.NativeId!=id||row.Appearance is not null||row.Kind==LedgerActualKind.Mimic))return false;
            int health=Native.Int(Native.Get(body,"health"));if(health<=0)return false;
            var data=Native.Get(body,"UnitData")!;
            int maximum=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMaxHealth","UnitState","GameState"),null,body,game));
            if(maximum<=0||health>maximum)return false;
            byte owner=Convert.ToByte(Native.Get(body,"owner"));
            var player=Native.Get(unit,"Owner");if(player is null||Native.Int(Native.Get(player,"Id"))!=owner)return false;
            projection=new(id,owner,native,native,RecruitmentCatalog.Label(native),(health+9)/10,(maximum+9)/10,(health+9)/10,(maximum+9)/10,
                data,data,game,ActualIsMimic:false,ActualNativeId:native,ActualLabel:RecruitmentCatalog.Label(native),ActualBadgeNativeId:spy?native:Native.EnumName(Native.Get(passenger!,"type")!));
            return true;
        }
    }
}
