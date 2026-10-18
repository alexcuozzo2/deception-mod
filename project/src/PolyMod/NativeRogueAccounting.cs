using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Native owner supplies tactical command context, while the framed row
    // reserves ownership for counts, defeat cleanup and the victim's score.
    internal static class NativeRogueAccounting
    {
        internal static void Install(Harmony h)
        {
            var p=typeof(NativeRogueAccounting);
            Native.Hook(h,Native.M("PlayerExtensions","CountUnits","PlayerState","GameState"),p,nameof(Count));
            string listRef=Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(Native.T("UnitState")).MakeByRefType().FullName!;
            Native.Hook(h,Native.M("PlayerExtensions","CountUnits","PlayerState","GameState",listRef),p,nameof(CountList));
            Native.Hook(h,Native.M("ActionUtils","KillUnit","GameState","TileData"),p,nameof(BeforeDeath),null,nameof(OwnersFinished));
            Native.Hook(h,Native.M("ActionUtils","PerformAttack","GameState","System.Byte","WorldCoordinates","WorldCoordinates","System.Int32"),p,nameof(BeforeDamage),nameof(AfterDamage));
            Native.Hook(h,Native.M("WipePlayerAction","Execute","GameState"),p,nameof(BeforeWipe),null,nameof(OwnersFinished));
            Native.Hook(h,Native.M("UnitDataExtensions","GetDefenceBonus","UnitState","GameState"),p,nameof(BeforeDefence),null,nameof(DefenceFinished));
            Native.Hook(h,Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),p,nameof(CityFortification));
        }
        static bool HasRogues(object state)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);
            return snapshot is not null&&NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Units.Any(x=>x.Alive&&x.Rogue is not null);
        }
        static object[] ReservedUnits(object state,byte owner)=>Native.Items(Native.Get(Native.Get(state,"Map")!,"Tiles")!)
            .Select(t=>Native.Get(t,"unit")).Where(u=>u is not null&&NativeRogueRules.RememberedOwner(state,u)==owner).Cast<object>().ToArray();
        public static bool Count(object __0,object __1,ref int __result)
        {
            if(!HasRogues(__1))return true;
            __result=ReservedUnits(__1,Convert.ToByte(Native.Get(__0,"Id"))).Length;return false;
        }
        public static bool CountList(object __0,object __1,ref object __2,ref int __result)
        {
            if(!HasRogues(__1))return true;
            var units=ReservedUnits(__1,Convert.ToByte(Native.Get(__0,"Id")));
            __2=Native.NewList("UnitState",units);__result=units.Length;return false;
        }
        // PerformAttack increments the victim's casualty counter before its
        // later KillUnitAction. Redirect that one completed native statistic;
        // the killer's kills/follow-ups and queued score actions stay native.
        internal sealed record Casualty(object State,object Map,object Unit,object CommandPlayer,object OwnerPlayer,Guid Epoch,byte Owner,uint Before,uint OwnerBefore);
        public static void BeforeDamage(object __0,object __3,int __4,out Casualty? __state)
        {
            __state=null;if(__4<=0||!NativeRegroupSession.Ready(__0))return;
            var unit=NativeRogueRules.At(__0,__3);if(unit is null||Native.Int(Native.Get(unit,"health"))<=0)return;
            var row=NativeRogueRules.Row(__0,unit);if(row?.Rogue is not LedgerRogue rogue)return;
            byte current=Convert.ToByte(Native.Get(unit,"owner"));if(current==rogue.Owner)return;
            object?[] command={current,null},owner={rogue.Owner,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),__0,command)!||
                !(bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),__0,owner)!)return;
            var snapshot=NativeGameStateEnvelope.Get(__0);if(snapshot is null)return;
            __state=new(__0,Native.Get(__0,"Map")!,unit,command[1]!,owner[1]!,NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Epoch,
                current,Convert.ToUInt32(Native.Get(command[1]!,"casualities")),Convert.ToUInt32(Native.Get(owner[1]!,"casualities")));
        }
        public static void AfterDamage(Casualty? __state,bool __runOriginal)
        {
            if(!__runOriginal||__state is not Casualty c||!NativeRegroupSession.Ready(c.State)||
                !Native.Same(Native.Get(c.State,"Map"),c.Map)||!Native.Same(NativeRogueRules.At(c.State,Native.Get(c.Unit,"coordinates")!),c.Unit)||
                Native.Int(Native.Get(c.Unit,"health"))!=0||Native.Int(Native.Get(c.Unit,"owner"))!=c.Owner)return;
            var snapshot=NativeGameStateEnvelope.Get(c.State);
            if(snapshot is null||NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Epoch!=c.Epoch)return;
            uint after=unchecked(c.Before+1);
            if(Convert.ToUInt32(Native.Get(c.CommandPlayer,"casualities"))!=after||Convert.ToUInt32(Native.Get(c.OwnerPlayer,"casualities"))!=c.OwnerBefore)return;
            try
            {Native.Set(c.CommandPlayer,"casualities",c.Before);Native.Set(c.OwnerPlayer,"casualities",unchecked(c.OwnerBefore+1));}
            catch
            {Native.Set(c.CommandPlayer,"casualities",after);Native.Set(c.OwnerPlayer,"casualities",c.OwnerBefore);throw;}
        }
        internal sealed record OwnerBefore(object Unit,byte Owner);
        internal sealed record Owners(OwnerBefore[] Before);
        static Owners? Substitute(object state,IEnumerable<object> bodies)
        {
            var original=new List<OwnerBefore>();
            try
            {
                foreach(var body in bodies)
                {
                    if(!NativeRogueRules.IsRogue(state,body))continue;
                    byte owner=NativeRogueRules.RememberedOwner(state,body);
                    // Preserve both exact prior fields, including a nested
                    // native cleanup or EndTurn scope already using that owner.
                    original.Add(new(body,Convert.ToByte(Native.Get(body,"owner"))));
                    if(Native.Get(body,"passengerUnit") is object cargo)original.Add(new(cargo,Convert.ToByte(Native.Get(cargo,"owner"))));
                    NativeRogueRules.SetOwner(body,owner);
                }
                return original.Count==0?null:new(original.ToArray());
            }
            catch {Restore(original);throw;}
        }
        static void Restore(IEnumerable<OwnerBefore> before)
        {foreach(var old in before.Reverse())Native.Set(old.Unit,"owner",old.Owner);}
        public static void BeforeDeath(object __0,object __1,out Owners? __state)
        {
            __state=null;
            if(Native.Get(__1,"unit") is object body)__state=Substitute(__0,new[]{body});
        }
        public static void BeforeWipe(object __0,out Owners? __state)
        {
            __state=null;if(!HasRogues(__0))return;
            // The native confirmed-defeat action itself chooses/removes the
            // player's units. It must include reserved Rogues and exclude
            // foreign bodies merely borrowed by that defeated player.
            __state=Substitute(__0,Native.Items(Native.Get(Native.Get(__0,"Map")!,"Tiles")!).Select(t=>Native.Get(t,"unit")).Where(u=>u is not null).Cast<object>());
        }
        public static Exception? OwnersFinished(Exception? __exception,Owners? __state)
        {if(__state is not null)Restore(__state.Before);return __exception;}

        [ThreadStatic] static Defence? CurrentDefence;
        internal sealed record Defence(object Tile,Defence? Parent);
        public static void BeforeDefence(object __0,object __1,out Defence? __state)
        {
            __state=null;if(!NativeRogueRules.IsRogue(__1,__0))return;
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(__1,"Map"),Native.Get(__0,"coordinates"));
            if(tile is null)return;
            __state=new(tile,CurrentDefence);CurrentDefence=__state;
        }
        public static bool CityFortification(object __instance,object __0,ref bool __result)
        {
            if(CurrentDefence is null||!Native.Same(CurrentDefence.Tile,__instance)||
                !Equals(__0,Native.EnumValue("Polytopia.Data.ImprovementData+Type","city")))return true;
            // The pinned native defence function computes terrain first and
            // then tests HasImprovement(city) for the ownership-only override.
            // Leave its terrain, status and version calculations intact.
            __result=false;return false;
        }
        public static Exception? DefenceFinished(Exception? __exception,Defence? __state)
        {if(__state is not null)CurrentDefence=__state.Parent;return __exception;}
    }
}
