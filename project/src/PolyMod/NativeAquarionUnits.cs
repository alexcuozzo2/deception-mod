using Deception.Core;

namespace Deception.PolyModAdapter
{
    internal static class NativeAquarionUnits
    {
        // Native recruitment tribe, never cosmetic climate or current skin.
        internal static bool IsAquarion(object player)=>Native.EnumName(Native.Get(player,"tribe")!)=="aquarion";
        internal static string MimicMenu(object player)=>IsAquarion(player)?AquarionCatalog.MimicId:RecruitmentCatalog.MenuId;
    }
}
