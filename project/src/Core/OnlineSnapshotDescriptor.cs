using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;

namespace Deception.Core
{
    // Structural admission of the INNER GameState frame only. The native
    // adapter owns any SerializationHelpers prefix, client/lobby authority,
    // and validation of decoded native bodies against the ledger.
    public sealed class OnlineSnapshotDescriptor
    {
        readonly byte[] NativeSection,Extension;
        public Guid Epoch { get; }
        public int NativeVersion=>NativeStateFrame.NativeVersion;
        public ushort FrameSchema=>NativeStateFrame.Schema;
        public ushort LedgerSchema { get; }
        public string? OnlineContract { get; }
        public long Revision { get; }
        public int NativeLength=>NativeSection.Length;
        public int ExtensionLength=>Extension.Length;
        OnlineSnapshotDescriptor(NativeStateFrame.Decoded frame,NativeIdentityLedger ledger,ushort schema)
        {Epoch=ledger.Epoch;OnlineContract=ledger.OnlineContract;LedgerSchema=schema;Revision=frame.Revision;NativeSection=frame.Native;Extension=frame.Extension;}
        public byte[] CopyNativeSection()=>(byte[])NativeSection.Clone();
        public byte[] CopyExtension()=>(byte[])Extension.Clone();

        public static OnlineSnapshotDescriptor Validate(byte[] innerFramedState,Guid expectedEpoch,string? expectedContract=null)
        {
            ArgumentNullException.ThrowIfNull(innerFramedState);
            if(expectedEpoch==Guid.Empty)throw new InvalidDataException("Online snapshot requires an assigned native match Guid.");
            if(innerFramedState.Length<NativeStateFrame.HeaderBytes||innerFramedState.Length>
                NativeStateFrame.HeaderBytes+NativeStateFrame.MaxNativeBytes+NativeStateFrame.MaxExtensionBytes)
                throw new InvalidDataException("Online snapshot frame length is outside its bounds.");
            // Decode copies both sections and enforces exact length, version,
            // checksum and bounds. Neither checksum nor Guid authenticates a peer.
            var frame=NativeStateFrame.Decode((byte[])innerFramedState.Clone(),NativeStateFrame.NativeVersion);
            var ledger=NativeIdentityLedgerCodec.Decode(frame.Extension,frame.Revision);
            if(ledger.Epoch!=expectedEpoch)throw new InvalidDataException("Online snapshot belongs to a different native match.");
            // Online adapters always supply their exact agreed contract. Null
            // remains useful for structurally inspecting local/legacy frames;
            // it does not make those frames eligible for an online session.
            if(expectedContract is not null&&(expectedContract.Length!=64||
                !expectedContract.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f')||
                !string.Equals(ledger.OnlineContract,expectedContract,StringComparison.Ordinal)))
                throw new InvalidDataException("Online snapshot compatibility contract differs. Local="+ContractHint(expectedContract)+"; saved="+ContractHint(ledger.OnlineContract)+".");
            return new(frame,ledger,BinaryPrimitives.ReadUInt16LittleEndian(frame.Extension.AsSpan(4)));
        }
        // Diagnostic fingerprints identify mismatched builds without recording
        // game/account identifiers or any of the underlying unit state.
        static string ContractHint(string? contract)=>contract is not null&&contract.Length==64&&
            contract.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f')?contract.Substring(0,12):"missing-or-invalid";
    }
}
