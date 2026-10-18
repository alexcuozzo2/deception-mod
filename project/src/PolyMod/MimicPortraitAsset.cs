using System;
using System.Security.Cryptography;

namespace Deception.PolyModAdapter
{
    // Alex's original PNG is loaded unchanged from our mod file registry. This private UI sprite is never
    // registered as a world unit or written into the game's global sprite atlas.
    internal static class MimicPortraitAsset
    {
        internal const string AssetPath="assets/dcp_mimic-portrait__.png";
        static object? Sprite;
        internal static object Get()
        {
            if(Native.UnityAlive(Sprite))
            {
                if(Native.UnityAlive(Native.Get(Sprite!,"texture")))return Sprite!;
                Native.Invoke(Native.M("UnityEngine.Object","Destroy","UnityEngine.Object"),null,Sprite);
                Sprite=null;
            }
            byte[] bytes=ModContentAssets.Read(AssetPath);
            if(bytes.Length!=263111||!Convert.ToHexString(SHA256.HashData(bytes)).Equals("a34bc881dc3fedfe007701f37f8d7717be89b5a7553236e719401208e4907bd0",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Mimic portrait differs from the reviewed PNG.");
            // Preserve the original framing while borrowing the loader texture.
            var framed=UnitArtAssets.DecodeLoadedFrame(AssetPath,1310,1201,452f,213f,430f,690f);
            Sprite=framed;
            Entry.Note("mimic-portrait-asset","Loaded Alex's unchanged 1310x1201 PNG; private sprite frame 452,213,430,690.");
            return framed;
        }
    }
}
