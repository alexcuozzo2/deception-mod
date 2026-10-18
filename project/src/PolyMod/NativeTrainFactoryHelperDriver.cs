using System;
using System.Buffers.Binary;
using System.IO;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using A=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;

namespace Deception.PolyModAdapter
{
    // Explicit PRIVATE helper traversal. Does not install observers or detours.
    internal static class NativeTrainFactoryHelperDriver
    {
        internal static C.Carrier Read(C.Trial trial,byte[] body,P.Kind kind,P.Wire wire,C.Scope? parent=null)
        {
            if(body is null||body.Length<2||body.Length>4098||BinaryPrimitives.ReadUInt16LittleEndian(body)!=5)
                throw new InvalidDataException("Expected bounded native Train body with its UInt16 discriminator.");
            if(kind is not (P.Kind.Command or P.Kind.Action))throw new InvalidDataException("Unsupported Train factory kind.");
            var input=trial.NewBuffer(body);var stamp=new C.Call(0,A.NewCookie(),123,C.Direction.Read);C.Carrier result;
            using(var scope=trial.Begin(input,stamp,wire,parent))
            {
                result=ReadInScope(scope,input,kind);scope.Finish();
            }
            // Without a parent, root cleanup precedes return. With an explicit
            // parent this is a provisional private candidate; its binding and
            // buffers remain in that root until the caller completes cleanup.
            return result;
        }
        // Caller must establish a discriminator-validated owned bounded input;
        // this internal seam allows explicit nested ROOT journals in the probe.
        internal static C.Carrier ReadInScope(C.Scope parent,C.Buffer input,P.Kind kind)
        {
            parent.Check();byte[] body=input.Snapshot();
            if(input.Native.Position!=0||body.Length<2||body.Length>4098||BinaryPrimitives.ReadUInt16LittleEndian(body)!=5)
                throw new InvalidDataException("Owned helper input must start at its bounded Train discriminator.");
            using var traversal=NativeTrainFactoryTraversal.Open(parent,input,kind);
            object value=Native.Invoke(Native.M("GameState",kind==P.Kind.Command?"DeserializeCommand":"DeserializeAction",
                "Il2CppSystem.IO.BinaryReader","System.Int32"),null,input.Stream(C.Direction.Read),123)
                ??throw new InvalidDataException("Native Train helper returned null.");
            if(input.Native.Position!=input.Native.Length)throw new InvalidDataException("Trailing native Train helper input.");
            traversal.Complete(value);return parent.Trial.RequireCarrier(value,kind);
        }
    }
}
