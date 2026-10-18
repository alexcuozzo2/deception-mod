using System;
using Deception.Core;
namespace Deception.PolyModAdapter
{
 // The real special action adapter is tested separately. Regroup/Disguise
 // fixture offers remain isolated from Spy network and support transactions.
 internal static class NativeRogueRules {internal static Deception.Core.LedgerUnit? Row(object state,object unit)=>null;internal static bool IsRogue(object state,object? unit)=>false;internal static bool CanCommand(object state,object unit,byte actor)=>Convert.ToByte(Native.Get(unit,"owner"))==actor;}
 internal static class NativeSpyNetwork
 {internal static bool ActionReady(object state,object unit,byte actor)=>false;internal static bool TryFoggerReachable(object state,byte actor,uint id,out object[] targets,out string reason){targets=Array.Empty<object>();reason="Inactive Fogger fixture port";return false;}internal static bool TryReachable(object state,byte actor,uint id,out object[] targets,out string reason,bool ready=true){targets=Array.Empty<object>();reason="Inactive fixture port";return false;}}
 internal static class NativeFoggerActions
 {internal static bool IsFogger(object? unit)=>false;internal static bool Ready(object state,object unit,byte actor)=>false;}
 internal static class NativeSubvert
 {internal static bool TryOffer(object state,uint source,uint target,out NativeExchangeIntent? intent,out string reason){intent=null;reason="Inactive fixture port";return false;}}
 internal static class NativeBlackout
 {internal static bool TryOffer(object state,uint source,int x,int y,out NativeExchangeIntent? intent,out string reason){intent=null;reason="Inactive fixture port";return false;}}
 internal static class NativeFeignSupport
 {internal static bool TryOffer(object state,uint source,NativeExchangeKind kind,out NativeExchangeIntent? intent,out string reason){intent=null;reason="Inactive fixture port";return false;}}
}
