namespace Deception.PolyModAdapter
{
    // This UI fixture has no private Build/Train transaction or native bridge.
    // Its store calls see the ordinary, unreserved case. Actual reservation,
    // mutation and rollback logic is compiled in the separate build-journal suite.
    internal static class NativeBuildRecordJournalDraft
    {
        internal static void BeforeMutation(object value)
        {if(value is null)throw new System.ArgumentNullException(nameof(value));}
    }
}

namespace Deception.PolyModAdapter
{
    // Separate Regroup suites exercise the real coordinator. This existing
    // portrait/Smuggle suite has no Regroup session or native state bridge.
    internal static class MimicRegroupUi
    {
        internal static bool Choosing {get;set;}
        internal static object? Button;
        internal static object? OwnedButton(object bar)=>Button;
        internal static object? OwnedButton(object bar,Deception.Core.NativeExchangeKind kind)=>kind==Deception.Core.NativeExchangeKind.Regroup?Button:null;
        internal static void Sync(object bar,object? unit,bool allowed){}
        internal static void Remove(object bar){}
    }
}
