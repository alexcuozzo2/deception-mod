using System;
using System.Linq;
using Deception.Core;
namespace Deception.PolyModAdapter {
 // UI fixture only: real range/budget/authentication tested by fogger-actions-0390.
 internal static class NativeFoggerActions {
  internal static bool IsFogger(object? unit)=>unit is UnitState u&&FoggerCatalog.IsFoggerId(u.type.ToString());
  internal static bool Ready(object state,object unit,byte actor)=>IsFogger(unit)&&((UnitState)unit).owner==actor&&!((UnitState)unit).attacked&&((UnitState)unit).health>0;
 }
 internal static partial class NativeSpyNetwork {
  internal static bool TryFoggerReachable(object state,byte actor,uint id,out object[] targets,out string reason){Queries++;targets=Tiles;reason="";return true;}
 }
}
