using System;
using System.IO;
using Deception.Core;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using A=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;

namespace Deception.PolyModAdapter
{
    // DRAFT primitive operations for a future explicitly gated native probe.
    // No hooks, source discovery, client fields, GameState, Execute or Save.
    // Do not invoke until the exact ABI3 raw bridge routes sites3/4 to Adapter.
    internal static class NativeTrainLeafProbeDriver
    {
        static string Owner(P.Kind kind)=>kind switch{P.Kind.Command=>"TrainCommand",P.Kind.Action=>"TrainAction",_=>throw new InvalidDataException("Invalid Train kind.")};
        static void Input(byte[] bytes)
        {if(bytes is null||bytes.Length>4096)throw new InvalidDataException("Private Train input exceeds the record bound.");}
        static C.Call Parent(C.Direction direction)=>new(0,A.NewCookie(),NativeStateFrame.NativeVersion,direction);
        static void NativeLeaf(C.Scope scope,object value,P.Kind kind,C.Buffer buffer,C.Direction direction,C.FactoryTicket? ticket=null)
        {
            using var invocation=A.Open(scope,value,kind,buffer,direction,ticket);
            string method=direction==C.Direction.Read?"Deserialize":"Serialize";
            string stream=direction==C.Direction.Read?"Il2CppSystem.IO.BinaryReader":"Il2CppSystem.IO.BinaryWriter";
            Native.Invoke(Native.M(Owner(kind),method,stream,"System.Int32"),value,buffer.Stream(direction),NativeStateFrame.NativeVersion);
            invocation.RequireCompleted(); // Missing/swallowed detour cannot become a false pass.
        }
        internal static byte[] Write(C.Trial trial,C.Carrier carrier,P.Wire wire)
        {
            var buffer=trial.NewBuffer();using var scope=trial.Begin(buffer,Parent(C.Direction.Write),wire);
            NativeLeaf(scope,carrier.Value,carrier.Kind,buffer,C.Direction.Write);
            byte[] bytes=buffer.Snapshot();scope.Finish();return bytes;
            // using cleanup runs before the caller receives bytes; failure prevents return.
        }
        internal static void Read(C.Trial trial,C.Carrier carrier,byte[] bytes,P.Wire wire)
        {
            Input(bytes);var buffer=trial.NewBuffer(bytes);using var scope=trial.Begin(buffer,Parent(C.Direction.Read),wire);
            NativeLeaf(scope,carrier.Value,carrier.Kind,buffer,C.Direction.Read);
            if(buffer.Native.Position!=buffer.Native.Length)throw new InvalidDataException("Trailing private Train bytes.");
            scope.Finish();
        }
        internal static C.Carrier ReadFromFactory(C.Trial trial,P.Kind kind,byte[] leafBytes,P.Wire wire)
        {
            Input(leafBytes);var buffer=trial.NewBuffer(leafBytes);var stamp=Parent(C.Direction.Read);
            using var scope=trial.Begin(buffer,stamp,wire);
            object reader=buffer.Stream(C.Direction.Read),value;C.FactoryTicket ticket;
            using(var window=scope.BeginFactoryReader(reader,stamp))
            {
                // This direct managed call observes a REAL native factory return.
                // It is not a hook and does not cover native DeserializeCommand,
                // GameState.Deserialize or LoadCommandStack's inline factories.
                using(var factory=window.BeginFactory(kind,5))
                {
                    string method=kind==P.Kind.Command?"GetCommand":"GetAction";
                    string enumType=kind==P.Kind.Command?"CommandType":"ActionType";
                    object type=Enum.ToObject(Native.T(enumType),(ushort)5);
                    value=Native.Invoke(Native.M("GameState",method,enumType),null,type)
                        ??throw new InvalidDataException("Native Train factory returned null.");
                    value=Native.As(value,Owner(kind));ticket=factory.Returned(value);
                }
                NativeLeaf(scope,value,kind,buffer,C.Direction.Read,ticket);
                if(buffer.Native.Position!=buffer.Native.Length)throw new InvalidDataException("Trailing factory Train bytes.");
                window.Finish();
            }
            scope.Finish();return trial.RequireCarrier(value,kind);
        }
    }
}
