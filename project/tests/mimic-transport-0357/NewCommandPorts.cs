// Unrelated command dispatch ports deliberately reject; dedicated support/Spy fixtures test those routes.
using System;
using Deception.Core;
namespace Deception.PolyModAdapter {
 internal static class NativeSubvert {
 internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason){plan=null;reason="Unrelated command is outside this fixture";throw new NotSupportedException(reason);}
 internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason){result=null;reason="Unrelated command is outside this fixture";throw new NotSupportedException(reason);}
 }
 internal static class NativeBlackmail {
 internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason){plan=null;reason="Unrelated command is outside this fixture";throw new NotSupportedException(reason);}
 internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason){result=null;reason="Unrelated command is outside this fixture";throw new NotSupportedException(reason);}
 } internal static class NativeLighthouseSecret {
 internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason){plan=null;reason="Unrelated command is outside this fixture";throw new NotSupportedException(reason);}
 internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason){result=null;reason="Unrelated command is outside this fixture";throw new NotSupportedException(reason);}
 }
 internal static class NativeBlackout {
 internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason){plan=null;reason="Unrelated command is outside this fixture";throw new NotSupportedException(reason);}
 internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason){result=null;reason="Unrelated command is outside this fixture";throw new NotSupportedException(reason);}
 }
 internal static class NativeFeignSupport {
 internal static bool TryPlan(object state,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason){plan=null;reason="Unrelated command is outside this fixture";throw new NotSupportedException(reason);}
 internal static bool TryCommit(object state,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason){result=null;reason="Unrelated command is outside this fixture";throw new NotSupportedException(reason);}
 }
}
