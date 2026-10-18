namespace Deception.PolyModAdapter
{
    // This retained fixture has no Rogue or special-action state.
    internal static class NativeRogueRules {internal static byte RememberedOwner(object state,object unit)=>System.Convert.ToByte(Native.Get(unit,"owner"));}
    internal static class NativeSpecialActionUi {internal static bool Choosing=>false;}
}
