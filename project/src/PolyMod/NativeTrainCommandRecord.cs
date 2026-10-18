using System;
using System.IO;
using Deception.Core;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using A=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;
using F=Deception.Core.NativeTrainCommandFrame;

namespace Deception.PolyModAdapter
{
    // Private, explicit DcpbV1 record only. No hooks, fallback, client/history,
    // automatic framing, raw outer helper interception or Execute calls.
    // Requires the lead's already-installed ABI3 Train leaf dispatcher.
    internal static class NativeTrainCommandRecord
    {
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Private Train command record: "+why);}
        static C.Call Parent(C.Direction direction)=>new(0,A.NewCookie(),F.Version,direction);
        static ushort Discriminator(object value)=>Convert.ToUInt16(Native.Invoke(Native.M("TrainCommand","GetCommandType"),value));
        static void CheckEpoch(object value,Guid epoch)
        {
            var intent=P.Get(value,P.Kind.Command);
            Require(intent is null||intent.Epoch==epoch,"Intent epoch differs from its explicit enclosing frame.");
        }
        static void Leaf(C.Scope scope,object value,C.Buffer buffer,C.Direction direction,C.FactoryTicket? ticket=null)
        {
            using var invocation=A.Open(scope,value,P.Kind.Command,buffer,direction,ticket);
            Native.Invoke(Native.M("TrainCommand",direction==C.Direction.Read?"Deserialize":"Serialize",
                direction==C.Direction.Read?"Il2CppSystem.IO.BinaryReader":"Il2CppSystem.IO.BinaryWriter","System.Int32"),
                value,buffer.Stream(direction),F.Version);
            invocation.RequireCompleted();
        }
        internal static byte[] Write(C.Trial trial,C.Carrier carrier,Guid epoch,C.Scope? parent=null)
        {
            ArgumentNullException.ThrowIfNull(trial);ArgumentNullException.ThrowIfNull(carrier);
            Require(epoch!=Guid.Empty&&carrier.Kind==P.Kind.Command&&ReferenceEquals(carrier.Trial,trial),"Wrong private carrier or epoch.");
            _=trial.RequireCarrier(carrier.Value,P.Kind.Command);
            Require(Discriminator(carrier.Value)==F.TrainDiscriminator,"Carrier's native discriminator is not Train.");
            CheckEpoch(carrier.Value,epoch);
            byte[] result;
            var buffer=trial.NewBuffer();
            using(var scope=trial.Begin(buffer,Parent(C.Direction.Write),P.Wire.DcpbV1,parent))
            {
                buffer.Native.Write("System.UInt16",F.TrainDiscriminator);
                Leaf(scope,carrier.Value,buffer,C.Direction.Write);
                CheckEpoch(carrier.Value,epoch);
                result=F.Encode(epoch,buffer.Snapshot());scope.Finish();
            }
            // Standalone: cleanup precedes return. Nested: the explicit parent
            // retains buffers/reservations and controls final publication.
            return result;
        }
        internal static C.Carrier Read(C.Trial trial,byte[] input,Guid expectedEpoch,C.Scope? parent=null)
        {
            ArgumentNullException.ThrowIfNull(trial);
            // All structural checks and expected epoch precede native allocation.
            var frame=F.Decode(input);Require(expectedEpoch!=Guid.Empty&&frame.Epoch==expectedEpoch,"Unexpected frame epoch.");
            var buffer=trial.NewBuffer(frame.CopyBody());var stamp=Parent(C.Direction.Read);
            C.Carrier accepted;
            using(var scope=trial.Begin(buffer,stamp,P.Wire.DcpbV1,parent))
            {
                ushort discriminator=Convert.ToUInt16(buffer.Native.Read("UInt16"));
                Require(discriminator==F.TrainDiscriminator,"Native discriminator changed.");
                object reader=buffer.Stream(C.Direction.Read),value;C.FactoryTicket ticket;
                using(var window=scope.BeginFactoryReader(reader,stamp))
                {
                    using(var factory=window.BeginFactory(P.Kind.Command,discriminator))
                    {
                        // Positive provenance from this exact synchronous native
                        // GetCommand return. This is not an inline factory hook.
                        object nativeType=Enum.ToObject(Native.T("CommandType"),discriminator);
                        value=Native.Invoke(Native.M("GameState","GetCommand","CommandType"),null,nativeType)
                            ??throw new InvalidDataException("Native Train factory returned null.");
                        value=Native.As(value,"TrainCommand");ticket=factory.Returned(value);
                    }
                    Leaf(scope,value,buffer,C.Direction.Read,ticket);
                    Require(Discriminator(value)==discriminator,"Decoded command discriminator differs.");
                    Require(buffer.Native.Position==buffer.Native.Length,"Trailing command record bytes.");
                    CheckEpoch(value,frame.Epoch);window.Finish();
                }
                accepted=trial.RequireCarrier(value,P.Kind.Command);scope.Finish();
            }
            // Standalone cleanup completes before return. With a supplied parent,
            // its root retains every attachment reservation and buffer; the
            // outer caller must not publish this carrier before closing that root.
            return accepted;
        }
    }
}
