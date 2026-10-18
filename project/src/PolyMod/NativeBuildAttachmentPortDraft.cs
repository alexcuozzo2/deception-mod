using System;
using System.IO;
using System.Linq;
using System.Text;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Small adapter to EXISTING Smuggle/exchange stores, supplied by the separate
    // candidate patch. Wire bytes are unchanged: Smuggle magic/version/unitID9,
    // exchange NativeExchangeIntentCodec.Size136, ordinary Build has no suffix.
    internal static class NativeBuildAttachmentPortDraft
    {
        internal sealed class Snapshot
        {
            internal object Token;internal readonly bool Exchange;
            readonly byte[] Bytes,Base;
            internal Snapshot(object token,bool exchange,byte[] bytes,object value)
            {Token=token;Exchange=exchange;Bytes=(byte[])bytes.Clone();Base=BaseStamp(value);}
            internal byte[] CopyBytes()=>(byte[])Bytes.Clone();
            internal bool Same(Snapshot other)=>ReferenceEquals(Token,other.Token)&&Exchange==other.Exchange&&Bytes.SequenceEqual(other.Bytes)&&Base.SequenceEqual(other.Base);
        }
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Build attachment draft: "+why);}
        static (bool Exchange,bool Smuggle) Tag(object value)
        {
            bool exchange=NativeExchangeCommands.IsExchange(value),smuggle=MimicSmuggleActions.IsSmuggle(value);
            Require(!(exchange&&smuggle),"Ambiguous Build tag.");
            string name=Native.EnumName(Native.Get(value,"Type")!);
            Require(exchange||smuggle||!name.StartsWith("dcp_",StringComparison.Ordinal),"Unknown private Build tag.");
            return(exchange,smuggle);
        }
        internal static byte[] BaseStamp(object value)
        {
            using var bytes=new MemoryStream();using var writer=new BinaryWriter(bytes,Encoding.UTF8,true);
            writer.Write(Convert.ToByte(Native.Get(value,"PlayerId")));writer.Write(Convert.ToInt32(Native.Get(value,"Type")));
            object at=Native.Get(value,"Coordinates")!;writer.Write(Native.Int(Native.Get(at,"X")));writer.Write(Native.Int(Native.Get(at,"Y")));
            bool action=Native.T("BuildAction").IsInstanceOfType(value);writer.Write(action);if(action)writer.Write((bool)Native.Get(value,"DeductCost")!);
            writer.Flush();return bytes.ToArray();
        }
        internal static Snapshot? Capture(object value)
        {
            var tag=Tag(value);var exchange=NativeExchangeCommands.CapturePrivateJournal(value);var smuggle=MimicSmuggleActions.CapturePrivateJournal(value);
            Require(exchange is null||tag.Exchange,"Exchange binding changed to another tag.");
            Require(smuggle is null||tag.Smuggle,"Smuggle binding changed to another tag.");
            Require(exchange is null||smuggle is null,"Multiple private Build attachments.");
            if(tag.Exchange){Require(exchange is not null,"Exchange is missing its complete intent.");NativeExchangeCommands.ValidateBase(value,NativeExchangeIntentCodec.Decode(exchange!.Value.Bytes));return new(exchange.Value.Token,true,exchange.Value.Bytes,value);}
            if(tag.Smuggle){Require(smuggle is not null,"Smuggle is missing its unit ID.");return new(smuggle!.Value.Token,false,smuggle.Value.Bytes,value);}
            return null;
        }
        // Absence must be testable before publishing a newly decoded tag, so it
        // checks stores without interpreting the carrier's now-custom tag.
        internal static bool IsUnbound(object value)=>NativeExchangeCommands.CapturePrivateJournal(value) is null&&MimicSmuggleActions.CapturePrivateJournal(value) is null;
        internal static Snapshot? ReadPublish(object value,Func<int,byte[]> readExact)
        {
            Require(IsUnbound(value),"Unexpected preexisting read binding.");var tag=Tag(value);
            if(tag.Exchange)
            {
                byte[] bytes=readExact(NativeExchangeIntentCodec.Size);var intent=NativeExchangeIntentCodec.Decode(bytes);NativeExchangeCommands.ValidateBase(value,intent);
                // Allocate/capture every fallible field BEFORE store publication.
                var snapshot=new Snapshot(new object(),true,bytes,value);snapshot.Token=NativeExchangeCommands.PublishPrivateJournal(value,intent);return snapshot;
            }
            if(tag.Smuggle)
            {
                byte[] bytes=readExact(9);Require(bytes.Length==9,"Short Smuggle suffix.");
                using var reader=new BinaryReader(new MemoryStream(bytes,false));
                Require(reader.ReadUInt32()==MimicSmuggleActions.PayloadMagic&&reader.ReadByte()==MimicSmuggleActions.PayloadVersion,"Unknown Smuggle suffix.");
                uint unitId=reader.ReadUInt32();var snapshot=new Snapshot(new object(),false,bytes,value);
                snapshot.Token=MimicSmuggleActions.PublishPrivateJournal(value,unitId);return snapshot;
            }
            return null;
        }
        internal static void RequireSame(object value,Snapshot? expected)
        {
            var actual=Capture(value);Require(expected is null?actual is null:actual is not null&&expected.Same(actual),"Binding/base identity or bytes changed.");
        }
        internal static void ForgetExact(object value,Snapshot snapshot)
        {
            if(snapshot.Exchange)NativeExchangeCommands.ForgetPrivateJournal(value,snapshot.Token);
            else MimicSmuggleActions.ForgetPrivateJournal(value,snapshot.Token);
        }
        internal static void ForgetOwned(object value)
        {NativeExchangeCommands.Forget(value);MimicSmuggleActions.ForgetOffer(value);}
    }
}
