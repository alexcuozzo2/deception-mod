using System;
using System.IO;

namespace Deception.PolyModAdapter
{
    // Alex's original PNG is embedded unchanged. This private UI sprite is never
    // registered as a world unit or written into the game's global sprite atlas.
    internal static class MimicPortraitAsset
    {
        internal const string ResourceName="Deception.MimicPortrait.png";
        static object? Sprite,DecodedSprite;
        internal static object Get()
        {
            if(Native.UnityAlive(Sprite))return Sprite!;
            using var source=typeof(MimicPortraitAsset).Assembly.GetManifestResourceStream(ResourceName)
                ??throw new InvalidOperationException("Embedded Mimic portrait is missing.");
            using var buffer=new MemoryStream();source.CopyTo(buffer);
            byte[] bytes=buffer.ToArray();
            if(bytes.Length!=263111)throw new InvalidOperationException("Embedded Mimic portrait differs from the reviewed PNG.");
            var pivot=typeof(Nullable<>).MakeGenericType(Native.T("UnityEngine.Vector2"));
            var sprite=Native.Invoke(Native.M("PolyMod.Managers.Visual","BuildSprite","System.Byte[]",pivot.FullName!,"System.Single"),null,bytes,null,100f)
                ??throw new InvalidOperationException("Framework did not create the Mimic portrait.");
            // The existing framework helper discards LoadImage's Boolean result.
            // Its sprite covers the whole texture, so dimensions detect decode failure.
            var rect=Native.Get(sprite,"rect")!;
            if(Convert.ToSingle(Native.Get(rect,"width"))!=1310f||Convert.ToSingle(Native.Get(rect,"height"))!=1201f)
                throw new InvalidOperationException("Framework did not decode the expected Mimic PNG dimensions.");
            // Read-only alpha inspection identified the character within the
            // original canvas. Frame that region with two pixels of padding;
            // the original PNG and texture bytes remain unchanged.
            var frame=Activator.CreateInstance(Native.T("UnityEngine.Rect"),452f,213f,430f,690f)!;
            var center=Activator.CreateInstance(Native.T("UnityEngine.Vector2"),0.5f,0.5f)!;
            var framed=Native.Invoke(Native.M("UnityEngine.Sprite","Create","UnityEngine.Texture2D","UnityEngine.Rect","UnityEngine.Vector2","System.Single"),null,
                Native.Get(sprite,"texture"),frame,center,100f)??throw new InvalidOperationException("Mimic portrait frame was not created.");
            DecodedSprite=sprite;Sprite=framed;
            Entry.Note("mimic-portrait-asset","Loaded Alex's unchanged 1310x1201 PNG; private sprite frame 452,213,430,690.");
            return framed;
        }
    }
}
