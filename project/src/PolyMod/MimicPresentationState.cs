using System;
using System.Collections.Generic;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    internal sealed record MimicProjection(uint UnitId,byte Owner,string VariantId,string NativeId,string Label,
        int ActualHealth,int ActualMaxHealth,int ApparentHealth,int ApparentMaxHealth,
        object ActualData,object ApparentData,object State,LedgerAppearance? Appearance=null,
        bool ActualIsMimic=true,bool ActualMimicBoat=false,string ActualNativeId="dcp_mimic",string ActualLabel="Mimic",
        ulong LogicalId=0,LedgerUnit? Identity=null,string ActualBadgeNativeId="",bool ApparentMimicBoat=false)
    {
        internal bool ActualAmphibiousMimic=>Identity?.AmphibiousMimic==true||ActualNativeId==AquarionCatalog.MimicId;
        internal bool ApparentMimic=>RecruitmentCatalog.IsMenuId(NativeId)||RecruitmentCatalog.IsMenuId(Appearance?.Passenger?.Form??"");
        internal bool Exposed=>ActualIsMimic&&ApparentMimic;
    }

    // Read-only projection of the Local ledger when available, with the
    // original fresh-disguise fallback retained for diagnostic/legacy callers.
    // Inspection never mutates real health, ownership, flags or persistence.
    internal static class MimicPresentationState
    {
        sealed record Cached(string Variant,ApparentHealth Health);
        internal static Func<object,uint,LedgerAppearance?>? ReadAppearance;
        internal static Func<object,uint,LedgerUnit?>? ReadIdentity;
        static object? Game;
        static readonly Dictionary<uint,Cached> Appearances=new();
        public static void Clear(){Game=null;Appearances.Clear();}
        public static bool TryProject(object unitState,out MimicProjection projection)
        {
            projection=null!;
            if(!RecruitmentHooks.Registered||unitState is null)return false;
            string variant=Native.EnumName(Native.Get(unitState,"type")!);
            RecruitmentHooks.Forms.TryGetValue(variant,out var form);
            var game=Native.Static("GameManager","GameState");
            if(game is null)return false;
            if(!Native.Same(Game,game)){Clear();Game=game;}
            uint id=Convert.ToUInt32(Native.Get(unitState,"id"));
            object?[] lookup={id,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),game,lookup)!
                ||!Native.Same(lookup[1],unitState))return false;
            var identity=ReadIdentity?.Invoke(game,id);
            if(ReadIdentity is not null&&(identity is null||!identity.Alive||!identity.Independent||identity.NativeId!=id||identity.Appearance is null))
            {Appearances.Remove(id);return false;}
            bool mimic=identity?.Kind==LedgerActualKind.Mimic||(identity is null&&form is not null);
            if(identity is null&&form is null)return false;
            if(mimic&&form is null)throw new InvalidOperationException("Mimic presentation lacks its registered operative carrier.");
            if(!mimic&&form is not null)throw new InvalidOperationException("Genuine presentation aliases a Mimic carrier.");
            int stored=Native.Int(Native.Get(unitState,"health"));
            if(stored<=0){Appearances.Remove(id);return false;}
            if(mimic&&stored!=RecruitmentCatalog.NativeHealth)
                throw new InvalidOperationException("Mimic actual health changed; presentation must not repair it.");
            var actual=Native.Get(unitState,"UnitData")??RecruitmentHooks.Data(game,variant);
            var persisted=identity?.Appearance??ReadAppearance?.Invoke(game,id);
            string native=persisted?.Form??form!.NativeId;
            var apparent=RecruitmentHooks.Data(game,native);
            int maximum=Native.Int(Native.Get(apparent,"health"));
            if(persisted is null&&(maximum<=0||maximum%10!=0||maximum/10!=form!.ApparentHp))
                throw new InvalidOperationException("Native apparent health profile differs from registered form.");
            if(mimic&&Native.Int(Native.Get(actual,"health"))!=RecruitmentCatalog.NativeHealth)
                throw new InvalidOperationException("Mimic actual maximum health changed.");
            if(!Appearances.TryGetValue(id,out var cached)||cached.Variant!=variant)
                Appearances[id]=cached=new Cached(variant,Deception.Core.ApparentHealth.Fresh(maximum/10));
            if(mimic&&persisted is not null&&persisted.Form!=form!.NativeId)
                throw new InvalidOperationException("Saved Mimic appearance and native movement profile differ.");
            int actualMaximum=mimic?10:Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMaxHealth","UnitState","GameState"),null,unitState,game));
            if(actualMaximum<=0||actualMaximum<stored)throw new InvalidOperationException("Native actual health profile is invalid.");
            // Private truth artwork follows the occupied tile, not the copied
            // movement profile. A flying/swimming disguise may stand over water
            // without a real wrapper; showing a boat must not embark that unit.
            bool amphibious=identity?.AmphibiousMimic==true||variant==AquarionCatalog.MimicId;
            bool boat=mimic&&!amphibious&&WaterArtwork(game,unitState,identity?.MimicWaterForm==true||identity?.Transport is not null);
            // Native UnitStatusDisplay uses the real passenger's type icon for
            // a vessel. The vessel still supplies its actual body and stats.
            var badgeUnit=Native.Get(unitState,"passengerUnit")??unitState;
            string actualBadge=mimic?(amphibious?AquarionCatalog.MimicId:RecruitmentCatalog.MenuId):Native.EnumName(Native.Get(badgeUnit,"type")!);
            bool apparentMimic=RecruitmentCatalog.IsMenuId(native)||RecruitmentCatalog.IsMenuId(persisted?.Passenger?.Form??"");
            bool apparentAmphibious=native==AquarionCatalog.MimicId||persisted?.Passenger?.Form==AquarionCatalog.MimicId;
            bool apparentBoat=apparentMimic&&!apparentAmphibious&&WaterArtwork(game,unitState,persisted?.Passenger is not null);
            projection=new MimicProjection(id,Convert.ToByte(Native.Get(unitState,"owner")),variant,native,apparentMimic?(apparentAmphibious?"Mermimic":apparentBoat?"Mimic Boat":"Mimic"):RecruitmentCatalog.Label(native),
                (stored+9)/10,(actualMaximum+9)/10,persisted is null?cached.Health.Current:(persisted.Health+9)/10,
                persisted is null?cached.Health.Maximum:(persisted.Maximum+9)/10,actual,apparent,game,persisted,
                mimic,boat,mimic?(amphibious?AquarionCatalog.MimicId:RecruitmentCatalog.MenuId):variant,mimic?(amphibious?"Mermimic":boat?"Mimic Boat":"Mimic"):RecruitmentCatalog.Label(variant),identity?.LogicalId??0,identity,actualBadge,apparentBoat);
            return true;
        }
        static bool WaterArtwork(object game,object unit,bool transportFallback)
        {
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,
                Native.Get(game,"Map"),Native.Get(unit,"coordinates"));
            // Older diagnostic callers can lack a tile snapshot. In a live
            // map the current tile is authoritative even if the unit has cargo.
            return tile is null?transportFallback:NativeMimicTransport.WaterEndpoint(tile,transportFallback);
        }
    }
}
