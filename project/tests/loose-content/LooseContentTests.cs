using System;
using System.IO;
using Deception.PolyModAdapter;

namespace Deception.Tests
{
    public static class LooseContentTests
    {
        public static void Run(Action<string,Action> test)
        {
            test("loose-content-own-mod-exact-path-and-immutable-registry",()=>{
                var saved=PolyMod.Registry.mods;
                try
                {
                    var own=new PolyMod.Mod();own.files.Add(new("assets/probe.png",new byte[]{1,2}));
                    var other=new PolyMod.Mod();other.files.Add(new("assets/other.png",new byte[]{3}));
                    PolyMod.Registry.mods=new(){["alex_deception"]=own,["other"]=other};
                    var bytes=ModContentAssets.Read("assets/probe.png");bytes[0]=9;
                    if(own.files[0].bytes[0]!=1)throw new Exception("Registry buffer mutated.");
                    own.files.Add(new("patch.json",new byte[]{123,125}));
                    if(ModContentAssets.Read("patch.json").Length!=2)throw new Exception("Patch registry read failed.");
                    Reject(()=>ModContentAssets.Read("assets/other.png"));
                    Reject(()=>ModContentAssets.Read("assets/PROBE.png"));
                    Reject(()=>ModContentAssets.Read("assets/unit-art/probe.png"));
                    own.files.Add(new("assets\\folder\\windows.png",new byte[]{5}));
                    if(ModContentAssets.Read("assets/folder/windows.png")[0]!=5)throw new Exception("Windows extracted-folder asset was not found.");
                    own.files.Add(new("assets/folder/windows.png",new byte[]{6}));
                    Reject(()=>ModContentAssets.Read("assets/folder/windows.png"));
                    own.files.Add(new("assets/probe.png",new byte[]{4}));
                    Reject(()=>ModContentAssets.Read("assets/probe.png"));
                    own.files.Clear();own.files.Add(new("assets/probe.png",Array.Empty<byte>()));
                    Reject(()=>ModContentAssets.Read("assets/probe.png"));
                    PolyMod.Registry.mods.Remove("alex_deception");
                    Reject(()=>ModContentAssets.Read("assets/probe.png"));
                    foreach(var path in new[]{"assets/../probe.png","assets//probe.png","assets\\probe.png","probe.png"})
                    {
                        bool rejected=false;try{ModContentAssets.Read(path);}catch(ArgumentException){rejected=true;}
                        if(!rejected)throw new Exception("Unsafe asset path accepted.");
                    }
                }
                finally{PolyMod.Registry.mods=saved;}
            });
        }
        static void Reject(Action action)
        {
            try{action();}catch(InvalidDataException){return;}
            throw new Exception("Invalid registry asset accepted.");
        }
    }
}
