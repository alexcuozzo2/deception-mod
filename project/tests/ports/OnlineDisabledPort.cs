using System;
namespace Deception.PolyModAdapter
{
    // Local lifecycle regression fixtures deliberately have no online loader.
    // The separate online-session fixture compiles the actual online adapter.
    internal static class NativeOnlineSession
    {
        internal static bool Enabled=>false;
        internal static bool HasScope=>false;
        internal static bool HasContext=>false;
        internal static bool SessionFaulted=>false;
        internal static bool Remote(object client)=>false;
        internal static bool CanDispatch(object? client=null)=>false;
        internal static bool Prepare(object state)=>false;
        internal static bool OwnedState(object state)=>false;
        internal static void LeaveForLocal(){}
        internal static NativeGameStateEnvelope.Snapshot? Snapshot(object state,bool presentation=false)=>null;
        internal static Transport? BeforeTransport(object state,bool reading)=>null;
        internal static void Fault(string reason)=>throw new InvalidOperationException("Inactive online path reached a Local-only fixture.");
        internal sealed class Transport
        {
            internal Guid Epoch=>throw new NotSupportedException();
            internal void Check(object state,bool reading)=>throw new NotSupportedException();
            internal void Frame(object state,bool reading,long revision,byte[] payload)=>throw new NotSupportedException();
            internal void Adopt(object state)=>throw new NotSupportedException();
            internal void Fail()=>throw new NotSupportedException();
        }
    }
}
