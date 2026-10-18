using System;
using System.Collections.Generic;
using System.IO;

// Fixture-only seed: production never loads embedded artwork. Existing fixture
// embeds provide reviewed bytes without making test runs depend on cwd/files.
namespace PolyMod
{
    public sealed class Mod
    {
        public sealed record File(string name,byte[] bytes);
        public List<File> files=new();
    }
    public static class Registry
    {
        public static Dictionary<string,object> sprites=new(StringComparer.Ordinal);
        public static Dictionary<string,Mod> mods=Seed();
        static Dictionary<string,Mod> Seed()
        {
            var mod=new Mod();var assembly=typeof(Registry).Assembly;
            foreach(var resource in assembly.GetManifestResourceNames())
            {
                string? path=resource.StartsWith("Deception.UnitArt.",StringComparison.Ordinal)?"assets/unit-art/"+resource.Substring("Deception.UnitArt.".Length):
                    resource.StartsWith("Deception.SpyEffects.",StringComparison.Ordinal)?"assets/spy-effects/"+resource.Substring("Deception.SpyEffects.".Length):
                    resource=="Deception.MimicPortrait.png"?"assets/mimic-portrait.png":null;
                if(path is null)continue;
                using var stream=assembly.GetManifestResourceStream(resource)!;
                using var buffer=new MemoryStream();stream.CopyTo(buffer);
                var bytes=buffer.ToArray();
                if(path.EndsWith(".png",StringComparison.Ordinal))
                {
                    var slash=path.LastIndexOf('/');
                    path=path.Substring(0,slash+1)+"dcp_"+path.Substring(slash+1,path.Length-slash-5)+"__.png";
                    // Match stock loader ownership without requiring Unity in
                    // the independent byte-registry fixture executable.
                    var visual=assembly.GetType("PolyMod.Managers.Visual");
                    if(visual is not null)
                    {
                        var sprite=visual.GetMethod("BuildSprite")!.Invoke(null,new object?[]{bytes,null,100f})!;
                        var texture=sprite.GetType().GetProperty("texture")!.GetValue(sprite)!;
                        texture.GetType().GetProperty("width")!.SetValue(texture,System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16,4)));
                        texture.GetType().GetProperty("height")!.SetValue(texture,System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20,4)));
                        sprites.Add(Path.GetFileNameWithoutExtension(path),sprite);
                    }
                }
                mod.files.Add(new(path,bytes));
            }
            return new(StringComparer.Ordinal){["alex_deception"]=mod};
        }
    }
}
