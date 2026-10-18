using System;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Read-only initial appearance for the exact two native
    // carriers admitted by DCPB. No UnitState.Create/clone, ID allocation,
    // native field writes, GameManager lookup or ledger attachment.
    // GetVisualSkin reads the native process GameSkin override; capture its
    // effective value now, then verify the same profile on the actual newborn.
    internal static class NativePaidBirthAppearance
    {
        const string Tribe="PolytopiaBackendBase.Common.TribeType";
        const string Skin="PolytopiaBackendBase.Common.SkinType";
        static void Require(bool value,string reason)
        {if(!value)throw new InvalidDataException("Paid-birth appearance: "+reason);}
        static string Id(object value)=>Enum.GetName(value.GetType(),value)
            ??throw new InvalidDataException("Unknown native cosmetic enum value.");

        internal static LedgerAppearance Prepare(object state,object player,string rule)
        {
            string form=NativePaidBirthIntentCodec.Form(rule);
            Require(Native.Int(Native.Get(state,"Version"))==123&&RecruitmentHooks.Registered&&
                RecruitmentHooks.Forms.TryGetValue(rule,out var registered)&&registered.NativeId==form,
                "Unsupported state or registered carrier.");
            object?[] owner={Convert.ToByte(Native.Get(player,"Id")),null};
            Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,owner)!
                &&Native.Same(owner[1],player),"Player is not the supplied state's exact owner.");
            object data=RecruitmentHooks.Data(state,rule);
            Require(Native.EnumName(Native.Get(data,"type")!)==rule&&Native.Int(Native.Get(data,"type"))!=15,
                "Native carrier reached the special birth-climate constructor branch.");
            object nativeForm=RecruitmentHooks.Data(state,form);
            int maximum=Native.Int(Native.Get(nativeForm,"health"));
            Require(maximum is >0 and <=ushort.MaxValue,"Unsupported native apparent health range.");

            object visual=Native.Invoke(Native.M("PlayerExtensions","GetVisualTribeType","PlayerState"),null,player)!;
            object skin=Native.Invoke(Native.M("PlayerState","GetVisualSkin"),player)!;
            object tribe=Native.Get(player,"tribe")!,climate=Native.Get(player,"_climate")!;
            if(Native.Int(climate)==0)climate=tribe;
            if(Native.Int(Native.Get(player,"tribeMix"))!=0)
            {
                object tribeData=Native.Invoke(Native.M(RecruitmentHooks.GLD,"GetTribeData",Tribe),
                    Native.Get(state,"GameLogicData"),tribe)!;
                climate=Native.Get(tribeData,"type")!;
            }
            // Pinned UnitState.Create(state,...): data.type != 15 passes zero
            // TribeType and SkinType to the value constructor. Preserve None;
            // do not replace it with the recruiting player's profile.
            string birthTribe=Id(Enum.ToObject(Native.T(Tribe),0));
            string birthSkin=Id(Enum.ToObject(Native.T(Skin),0));
            return new(form,new(Id(visual),Id(skin)),new(Id(climate),Id(skin)),new(birthTribe,birthSkin),
                Native.Int(Native.Get(player,"color")),(ushort)maximum,(ushort)maximum,false,
                ApparentConcealment.Visible,-1,Array.Empty<string>());
        }

        // Invoke only after the enclosing exact TrainUnit witness exists; the
        // caller still checks payment, native HP/ID/home, queue and ownership.
        internal static void Verify(object state,NativeLedgerState.Body body,string rule,LedgerAppearance expected)
        {
            Require(Native.EnumName(Native.Get(body.Unit,"type")!)==rule&&
                Native.Int(Native.Get(body.Unit,"birthClimate"))==0&&
                Native.Int(Native.Get(body.Unit,"birthClimateSkinType"))==0,"Newborn birth profile differs.");
            var actual=NativeLedgerState.Capture(state,body,NativePaidBirthIntentCodec.Form(rule),true);
            Require(NativeExchangeIntentCodec.AppearanceFingerprint(actual).SequenceEqual(
                NativeExchangeIntentCodec.AppearanceFingerprint(expected)),"Effective newborn appearance changed during native execution.");
        }
    }
}
