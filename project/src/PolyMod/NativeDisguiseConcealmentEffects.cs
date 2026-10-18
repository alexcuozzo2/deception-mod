using System;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    internal static partial class NativeDisguiseConcealment
    {
        internal static bool ActualCloak(object unit)=>Native.EnumName(Native.Get(unit,"type")!) is "cloak" or "mermaidcloak" or "cloak_boat";
        internal static bool ActualSpy(object unit)=>Native.EnumName(Native.Get(unit,"type")!) is "dcp_spy" or "dcp_spymaid";
        internal static bool ActualConcealing(object unit)=>ActualCloak(unit);
        // Actual Spy abilities expose the caster. This pure helper is staged
        // by the owning action transaction, never by targeting or rendering.
        internal static object ActiveAbilityEffects(object effects,object unit)=>ActualSpy(unit)?
            Native.NewList("UnitEffect",Native.Items(effects).Where(e=>Native.Int(e)!=3)):effects;
        internal static LedgerAppearance ActiveAbilityAppearance(LedgerAppearance appearance,object unit)=>ActualSpy(unit)?
            appearance with{Concealment=ApparentConcealment.Visible,Effects=appearance.Effects.Where(e=>e!="Invisible").ToArray()}:appearance;
        static bool ActualHidden(object unit)=>ActualConcealing(unit)&&Native.Items(Native.Get(unit,"effects")!).Any(e=>Native.Int(e)==3);
        // Read-only effective public state, including old saves whose visible
        // mask predates actual native concealment. Callers bind the exact body;
        // neither inspecting nor copying this record mutates native readiness.
        internal static LedgerAppearance EffectiveAppearance(LedgerAppearance appearance,object actualUnit)
        {
            bool hidden=appearance.Concealment==ApparentConcealment.Hidden;
            if(hidden!=appearance.Effects.Contains("Invisible",StringComparer.Ordinal))throw new System.IO.InvalidDataException("Snapshot status disagrees with concealment");
            var result=appearance;
            if(!hidden&&ActualHidden(actualUnit))
                result=appearance with{Concealment=ApparentConcealment.Hidden,Effects=appearance.Effects.Concat(new[]{"Invisible"}).OrderBy(e=>e,StringComparer.Ordinal).ToArray()};
            if(appearance.Passenger is LedgerAppearance passenger&&Native.Get(actualUnit,"passengerUnit") is object actualPassenger)
            {
                var next=EffectiveAppearance(passenger,actualPassenger);
                if(!ReferenceEquals(next,passenger))result=result with{Passenger=next};
            }
            return result;
        }
        internal static object WithConcealment(object effects,LedgerAppearance? appearance,object? actualUnit=null)
        {
            bool hidden=appearance?.Concealment==ApparentConcealment.Hidden;
            if(appearance is not null){if(hidden!=appearance.Effects.Contains("Invisible",StringComparer.Ordinal))throw new System.IO.InvalidDataException("Snapshot status disagrees with concealment");}
            var original=Native.Items(effects).ToArray();
            // Native Hide earned by an actual Cloak survives a passive
            // mask change. Explicit Spy ability commits clear it separately;
            // genuine Cloak Infiltrate readiness retains its native meaning.
            if(actualUnit is not null&&ActualConcealing(actualUnit)&&original.Any(e=>Native.Int(e)==3))hidden=true;
            var values=original.Where(e=>Native.Int(e)!=3).ToList();
            if(hidden)values.Add(Enum.ToObject(Native.T("UnitEffect"),3));
            return Native.NewList("UnitEffect",values);
        }
    }
}
