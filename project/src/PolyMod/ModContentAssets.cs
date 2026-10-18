using System;
using System.Collections;
using System.IO;
using System.Reflection;

namespace Deception.PolyModAdapter
{
    // Both supported PolyMod loaders retain archive entry names and bytes in
    // Mod.files. Verify bytes using our mod and exact entry path, without disk
    // assumptions or embedded fallback; then borrow its namespaced loaded texture.
    internal static class ModContentAssets
    {
        internal static object LoadedTexture(string path,int width,int height)
        {
            if(!path.EndsWith("__.png",StringComparison.Ordinal))throw new ArgumentException("Expected a named Deception PNG.",nameof(path));
            var key=Path.GetFileNameWithoutExtension(path);
            if(!key.StartsWith("dcp_",StringComparison.Ordinal))throw new ArgumentException("Expected the Deception sprite prefix.",nameof(path));
            var field=Native.T("PolyMod.Registry").GetField("sprites",BindingFlags.Public|BindingFlags.Static)
                ??throw new MissingFieldException("PolyMod.Registry","sprites");
            if(field.GetValue(null) is not IDictionary sprites||sprites[key] is not object sprite||!Native.UnityAlive(sprite))
                throw new InvalidDataException("PolyMod did not load the Deception sprite: "+path);
            var texture=Native.Get(sprite,"texture");
            if(!Native.UnityAlive(texture)||Native.Int(Native.Get(texture!,"width"))!=width||Native.Int(Native.Get(texture!,"height"))!=height)
                throw new InvalidDataException("Loaded Deception texture dimensions differ: "+path);
            return texture!;
        }
        internal static byte[] Read(string path)
        {
            if(string.IsNullOrEmpty(path)||(path!="patch.json"&&!path.StartsWith("assets/",StringComparison.Ordinal))||
                path.Contains("\\")||Array.Exists(path.Split('/'),p=>p.Length==0||p=="."||p==".."))
                throw new ArgumentException("Expected a canonical Deception asset path.",nameof(path));
            var registry=Native.T("PolyMod.Registry").GetField("mods",BindingFlags.Public|BindingFlags.Static)
                ??throw new MissingFieldException("PolyMod.Registry","mods");
            if(registry.GetValue(null) is not IDictionary mods||mods["alex_deception"] is not object mod)
                throw new InvalidDataException("Deception is absent from the PolyMod mod registry.");
            var field=mod.GetType().GetField("files",BindingFlags.Public|BindingFlags.Instance)
                ??throw new MissingFieldException(mod.GetType().FullName,"files");
            if(field.GetValue(mod) is not IEnumerable files)
                throw new InvalidDataException("Deception mod file registry is unavailable.");
            byte[]? found=null;
            foreach(var file in files)
            {
                // Extracted Windows mods use Path.GetRelativePath separators;
                // archive mods retain '/'. Normalize only the loader's name,
                // keeping caller paths canonical and slash aliases ambiguous.
                if(file is null||Native.Get(file,"name") is not string name||!string.Equals(name.Replace('\\','/'),path,StringComparison.Ordinal))continue;
                if(found is not null)throw new InvalidDataException("Duplicate Deception asset: "+path);
                found=Native.Get(file,"bytes") as byte[]
                    ??throw new InvalidDataException("Deception asset has no bytes: "+path);
            }
            if(found is null||found.Length==0)throw new InvalidDataException("Missing or empty Deception asset: "+path);
            // Decoders receive their own buffer; the loader's registry stays intact.
            return (byte[])found.Clone();
        }
    }
}
