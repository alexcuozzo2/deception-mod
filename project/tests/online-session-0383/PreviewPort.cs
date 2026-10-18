using System;
using System.Buffers.Binary;
using System.IO;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Offline stand-in for the separately hash-pinned managed preview. This
    // fixture tests lifecycle ownership, not Harmony or the network service.
    internal static class NativeOnlinePreview
    {
        internal const string Contract="3412a2f8dcb5312c07a6aaf92fa04053f487c955dad120005c7d07d7e10a327e";
        internal static bool Available=true;
        internal static bool Install(Harmony harmony)=>Available;
        internal static string CurrentContract()=>Available?Contract:throw new InvalidDataException("Offline pinned preview unavailable.");
        internal static byte[] InnerFrame(byte[] bytes)
        {
            byte[] inner;
            if(bytes.Length>=4&&BinaryPrimitives.ReadUInt32LittleEndian(bytes)==NativeStateFrame.Magic)inner=(byte[])bytes.Clone();
            else if(bytes.Length>=8&&BinaryPrimitives.ReadInt32LittleEndian(bytes)==123&&BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4))==NativeStateFrame.Magic)inner=bytes.AsSpan(4).ToArray();
            else throw new InvalidDataException("Missing version123 online state frame.");
            _=NativeStateFrame.Decode(inner,123);return inner;
        }
    }
}
