using System;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // ClickedAbility formats its native confirmation header with GetShortInfo.
    // The Disband branch obtains the live UnitData name and localizes it before
    // appending it to the native action label. Replace only that name term;
    // never alter the command, its refund, or the unit to populate a popup.
    internal static class NativeDisbandPresentation
    {
        [ThreadStatic] static Scope? Current;
        static int Generation;
        internal sealed class Scope
        {
            internal readonly Scope? Previous;
            internal readonly int Generation;
            internal object? State,Map,Client,Viewer,Unit,Tile;
            internal LedgerUnit? Identity;
            internal byte Actor;
            internal uint Turn,UnitId;
            internal int Skin,Tribe;
            internal string OriginalTerm="",ActualTerm="",NativeType="";
            internal bool Authorized,Used;
            internal Scope(){Previous=Current;Generation=NativeDisbandPresentation.Generation;}
        }
        internal static void Install(Harmony h)
        {
            var p=typeof(NativeDisbandPresentation);
            Native.Hook(h,Native.M("CommandExtensions","GetShortInfo","CommandBase","PolytopiaBackendBase.Common.SkinType"),p,nameof(Begin),null,nameof(Finished));
            var array=Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1").MakeGenericType(Native.T("Il2CppSystem.Object"));
            Native.Hook(h,Native.M("Localization","GetSkinned","PolytopiaBackendBase.Common.SkinType","PolytopiaBackendBase.Common.TribeType","System.String",array.FullName!),p,nameof(NameTerm));
        }
        internal static void Clear(){Generation++;Current=null;}
        static object? At(object state,object coordinate)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),coordinate);
        static bool SameIdentity(Scope s)
        {
            var row=NativeRegroupSession.ReadIdentity(s.State!,s.UnitId);
            return s.Identity is null?row is null:row is {Alive:true}&&row.NativeId==s.UnitId&&
                row.LogicalId==s.Identity.LogicalId&&row.Kind==s.Identity.Kind&&row.AmphibiousMimic==s.Identity.AmphibiousMimic&&row.Rogue is null;
        }
        static bool Bound(Scope s)
        {
            if(!s.Authorized||s.Generation!=Generation||!Native.Same(s.State,Native.Static("GameManager","GameState"))||
                !Native.Same(s.Client,Native.Static("GameManager","Client"))||!Native.Same(s.Viewer,Native.Static("GameManager","LocalPlayer"))||
                !Native.Same(s.Map,Native.Get(s.State!,"Map"))||Convert.ToUInt32(Native.Get(s.State!,"CurrentTurn"))!=s.Turn||
                Native.Int(Native.Get(s.Viewer!,"Id"))!=s.Actor||Native.Int(Native.Get(s.Unit!,"owner"))!=s.Actor||Convert.ToUInt32(Native.Get(s.Unit!,"id"))!=s.UnitId||
                Native.EnumName(Native.Get(s.Unit!,"type")!)!=s.NativeType||
                (bool)Native.Get(s.Client!,"IsReplay")!||(bool)Native.Get(s.Client!,"IsSpectating")!||
                !(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,s.Actor)!)return false;
            return Native.Same(s.Tile,At(s.State!,Native.Get(s.Unit!,"coordinates")!))&&Native.Same(s.Unit,Native.Get(s.Tile!,"unit"))&&
                SameIdentity(s)&&NativeOrdinaryCommands.CanDisband(s.State!,s.Unit!,s.Actor);
        }
        public static void Begin(object __0,out Scope __state)
        {
            // Unrelated nested descriptions fence the outer description too.
            __state=new();Current=__state;
            try
            {
                object command;try{command=Native.As(__0,"DisbandCommand");}catch{return;}
                var s=__state;
                s.State=Native.Static("GameManager","GameState");s.Client=Native.Static("GameManager","Client");s.Viewer=Native.Static("GameManager","LocalPlayer");
                if(s.State is null||s.Client is null||s.Viewer is null)return;
                s.Actor=Convert.ToByte(Native.Get(command,"PlayerId"));
                s.Map=Native.Get(s.State,"Map");s.Tile=At(s.State,Native.Get(command,"Coordinates")!);s.Unit=s.Tile is null?null:Native.Get(s.Tile,"unit");
                if(s.Unit is null||s.Map is null)return;
                s.Turn=Convert.ToUInt32(Native.Get(s.State,"CurrentTurn"));s.UnitId=Convert.ToUInt32(Native.Get(s.Unit,"id"));s.NativeType=Native.EnumName(Native.Get(s.Unit,"type")!);
                s.Identity=NativeRegroupSession.ReadIdentity(s.State,s.UnitId);
                if(s.Identity is not {Alive:true}||s.Identity.NativeId!=s.UnitId||s.Identity.Rogue is not null)return;
                s.Authorized=true;if(!Bound(s)){s.Authorized=false;return;}
                var data=Native.Get(s.Unit,"UnitData")!;
                s.OriginalTerm=(string)Native.Get(data,"displayName")!;
                string real=s.NativeType;
                if(s.Identity.Kind==LedgerActualKind.Mimic)
                    real=s.Identity.AmphibiousMimic?AquarionCatalog.MimicId:RecruitmentCatalog.MenuId;
                else if(RecruitmentCatalog.IsMimicId(real)){s.Authorized=false;return;}
                s.ActualTerm="unit.names."+real;
                s.Skin=Native.Int(Native.Invoke(Native.M("PlayerState","GetVisualSkin"),s.Viewer));
                s.Tribe=Native.Int(Native.Get(s.Viewer,"tribe"));
            }
            catch(Exception e){__state.Authorized=false;Entry.Note("disband-name",e.Message);}
        }
        public static void NameTerm(object __0,object __1,ref string __2)
        {
            var s=Current;
            if(s is null||!s.Authorized||s.Used||__2!=s.OriginalTerm||Native.Int(__0)!=s.Skin||Native.Int(__1)!=s.Tribe)return;
            try
            {
                if(!Bound(s))return;
                s.Used=true;__2=s.ActualTerm;
            }
            catch(Exception e){s.Authorized=false;Entry.Note("disband-name",e.Message);}
        }
        public static Exception? Finished(Exception? __exception,Scope? __state)
        {
            if(__state is not null&&ReferenceEquals(Current,__state))Current=__state.Generation==Generation?__state.Previous:null;
            return __exception;
        }
    }
}
