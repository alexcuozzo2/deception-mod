namespace Deception.PolyModAdapter
{
 internal static class NativeMimicTransport
 {
  internal static bool WaterEndpoint(object value,bool wasBoat)
  {var tile=(TileData)value;return tile.Wetland?wasBoat:tile.HasImprovement(Polytopia.Data.ImprovementData.Type.city)?false:tile.IsWater;}
 }
}
