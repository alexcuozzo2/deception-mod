using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // A40: actual Invisible must reach native SetVisible and Unit.Update.
    // Suppressing that effect made a genuine Cloak's disguise opaque/visible.
    // Native visibility, owner/peace access and pulse alpha now remain intact.
    // Kept as a lifecycle compatibility boundary for existing Entry callers.
    internal static class NativeDisguiseConcealmentVisual
    {
        internal static void Install(Harmony harmony){}
        internal static void Clear(){}
    }
}
