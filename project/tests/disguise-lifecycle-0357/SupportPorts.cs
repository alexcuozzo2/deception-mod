// Narrow dependency port; production support semantics have their own suite.
using System;
using System.Linq;
using Deception.Core;
namespace Deception.PolyModAdapter{
 internal static class Entry{internal static void Note(string kind,string message){}}
 internal static class NativeFeignSupport{
  internal static bool Has(LedgerAppearance a,string effect)=>a.Effects.Contains(effect,StringComparer.Ordinal);
  internal static object Tile(object state,object unit)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),Activator.CreateInstance(Native.T("WorldCoordinates")))!;
  internal static int RecoveryAmount(object state,object tile,object unit,LedgerAppearance a)=>40;
  internal static LedgerAppearance Heal(LedgerAppearance a,int amount)=>Has(a,"Poisoned")?a with{Effects=a.Effects.Where(x=>x is not ("Poisoned" or "Charmed")).ToArray()}:a with{Health=(ushort)Math.Min(a.Maximum,a.Health+amount)};
 }
}
