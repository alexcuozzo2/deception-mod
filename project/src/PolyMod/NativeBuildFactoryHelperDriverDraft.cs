using System;
using System.Buffers.Binary;
using System.IO;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using J=Deception.PolyModAdapter.NativeBuildRecordJournalDraft;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
namespace Deception.PolyModAdapter
{
    // Caller-owned private input only. No native hooks, hidden streams or live history admission.
    internal static class NativeBuildFactoryHelperDriverDraft
    {
        internal static J.Carrier Read(J.Trial trial,byte[] body,J.Kind kind,P.Wire wire,C.Scope? parent=null)
        {
            Validate(body,kind);var input=trial.Streams.NewBuffer(body);J.Carrier result;
            using(var scope=trial.Streams.Begin(input,new(0,NativeTrainLeafProbeAdapter.NewCookie(),123,C.Direction.Read),wire,parent))
            {result=ReadInScope(trial,scope,input,kind);scope.Finish();}
            return result; // Optional parent retains all bindings/reservations until its outcome.
        }
        static void Validate(byte[] body,J.Kind kind)
        {
            if(body is null||body.Length<2||body.Length>4098||BinaryPrimitives.ReadUInt16LittleEndian(body)!=1||kind is not(J.Kind.Command or J.Kind.Action))
                throw new InvalidDataException("Expected a bounded native Build body with explicit UInt16 discriminator1.");
        }
        internal static J.Carrier ReadInScope(J.Trial trial,C.Scope parent,C.Buffer input,J.Kind kind)
        {
            parent.Check();Validate(input.Snapshot(),kind);if(input.Native.Position!=0)throw new InvalidDataException("Build helper input must start at zero.");
            using var traversal=NativeBuildFactoryTraversalDraft.Open(trial,parent,input,kind);
            object value=Native.Invoke(Native.M("GameState",kind==J.Kind.Command?"DeserializeCommand":"DeserializeAction","Il2CppSystem.IO.BinaryReader","System.Int32"),null,input.Stream(C.Direction.Read),123)
                ??throw new InvalidDataException("Native Build helper returned null.");
            if(input.Native.Position!=input.Native.Length)throw new InvalidDataException("Trailing native Build helper input.");
            return traversal.Complete(value);
        }
    }
}
