using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Deception.PolyModAdapter
{
    // Descriptor validation is not native-code admission. Only the immutable
    // compiled registry below authorizes a profile; resources cannot register
    // another platform or substitute its game/seam/ABI audit.
    internal static class NativePlatformContract
    {
        internal enum BridgeKind { State,Execution,Command }
        internal sealed class Artifact
        {
            internal string Resource {get;}
            internal string DigestResource {get;}
            internal string SeamsResource {get;}
            internal string FileName {get;}
            internal Artifact(string resource,string digestResource,string seamsResource,string fileName)
            {
                Require(ResourceName(resource)&&ResourceName(digestResource)&&ResourceName(seamsResource),"Invalid artifact resource name.");
                Require(Segment(fileName,128),"Invalid native artifact basename.");
                Resource=resource;DigestResource=digestResource;SeamsResource=seamsResource;FileName=fileName;
            }
        }
        internal sealed class Profile
        {
            internal string Rid {get;}
            internal string OperatingSystem {get;}
            internal Architecture Architecture {get;}
            internal int PointerSize {get;}
            internal string LibraryExtension {get;}
            internal string GameRelativePath {get;}
            internal string GameSha256 {get;}
            readonly Artifact[] Artifacts;
            internal Profile(string rid,string os,Architecture architecture,int pointerSize,string gameRelativePath,string gameSha256,
                Artifact state,Artifact execution,Artifact command)
            {
                string extension=os switch {"osx"=>".dylib","win"=>".dll","linux"=>".so",_=>throw Invalid("Unknown native operating system.")};
                // All current bridge sources use the audited x86-64 ABI. A new
                // architecture requires an explicit ABI implementation first.
                Require(architecture==Architecture.X64&&pointerSize==8,"Unsupported native bridge architecture or pointer size.");
                Require(rid==os+"-x64","RID does not match its native platform.");
                Require(RelativePath(gameRelativePath)&&gameRelativePath.EndsWith(extension,StringComparison.Ordinal),"Invalid relative game image path or extension.");
                Require(gameSha256 is not null&&gameSha256.Length==64&&Array.TrueForAll(gameSha256.ToCharArray(),c=>c is >= '0' and <= '9' or >= 'a' and <= 'f'),"Invalid game image SHA-256.");
                Rid=rid;OperatingSystem=os;Architecture=architecture;PointerSize=pointerSize;LibraryExtension=extension;
                GameRelativePath=gameRelativePath;GameSha256=gameSha256!;
                Artifacts=new[]{state,execution,command};
                var resources=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var artifact in Artifacts)
                {
                    Require(artifact is not null,"Every native bridge requires an artifact descriptor.");
                    Require(artifact!.FileName.EndsWith(extension,StringComparison.Ordinal)&&artifact.Resource.EndsWith(extension,StringComparison.Ordinal),"Artifact extension differs from its platform.");
                    string stem=artifact.Resource.Substring(0,artifact.Resource.Length-extension.Length);
                    Require(artifact.DigestResource==stem+".sha256"&&artifact.SeamsResource==stem+".seams.json","Artifact integrity/seam resources must share its exact stem.");
                    Require(files.Add(artifact.FileName),"Native artifact basenames collide.");
                    Require(resources.Add(artifact.Resource)&&resources.Add(artifact.DigestResource)&&resources.Add(artifact.SeamsResource),"Native artifact resources collide.");
                }
            }
            internal Artifact GetArtifact(BridgeKind kind)=>kind switch
            {
                BridgeKind.State=>Artifacts[0],BridgeKind.Execution=>Artifacts[1],BridgeKind.Command=>Artifacts[2],
                _=>throw new NotSupportedException("Unknown Deception native bridge kind.")
            };
        }
        static InvalidDataException Invalid(string reason)=>new("Deception native platform descriptor: "+reason);
        static void Require(bool condition,string reason){if(!condition)throw Invalid(reason);}
        static bool Segment(string? value,int maximum)
        {
            if(string.IsNullOrEmpty(value)||value.Length>maximum||value[0]=='.'||value.EndsWith(".",StringComparison.Ordinal)||value.Contains("..",StringComparison.Ordinal))return false;
            foreach(char c in value)if(!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '.'))return false;
            // Reject aliases reserved by Windows even when validated on Mac.
            string basename=value.Split('.')[0].ToUpperInvariant();
            return basename is not ("CON" or "PRN" or "AUX" or "NUL")&&
                !(basename.Length==4&&(basename.StartsWith("COM",StringComparison.Ordinal)||basename.StartsWith("LPT",StringComparison.Ordinal))&&basename[3] is >= '0' and <= '9');
        }
        static bool ResourceName(string? value)=>value is not null&&value.StartsWith("Deception.",StringComparison.Ordinal)&&Segment(value,192);
        static bool RelativePath(string? value)
        {
            if(string.IsNullOrEmpty(value)||value.Length>512)return false;
            string[] pieces=value.Split('/');if(pieces.Length>16)return false;
            return Array.TrueForAll(pieces,p=>Segment(p,128));
        }
        // Checks a proposed compiled table without admitting it. Resource keys
        // must be unique across profiles in the single managed assembly.
        internal static void ValidateDescriptors(params Profile[] profiles)
        {
            Require(profiles is not null&&profiles.Length>0&&profiles.Length<=16,"Invalid registered profile count.");
            var rids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var resources=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var profile in profiles!)
            {
                Require(profile is not null,"Missing native platform descriptor.");
                Require(rids.Add(profile!.Rid),"Duplicate native platform selector.");
                foreach(BridgeKind kind in Enum.GetValues<BridgeKind>())
                {
                    var artifact=profile.GetArtifact(kind);
                    Require(resources.Add(artifact.Resource)&&resources.Add(artifact.DigestResource)&&resources.Add(artifact.SeamsResource),"Cross-platform resource collision.");
                }
            }
        }
        // Keep these Mac resource names compatible with the installed package.
        // Add other rows only after target game hash, method prologues, bridge
        // exports/layout/callback-unwinding and native execution are audited.
        static readonly Profile[] Profiles={new("osx-x64","osx",Architecture.X64,8,
            "Polytopia.app/Contents/Frameworks/GameAssembly.dylib","5391c27cf9e63ba3db10b168ba7435da723853b265695821f31942bb7ab95dc0",
            new("Deception.StateBridge.dylib","Deception.StateBridge.sha256","Deception.StateBridge.seams.json","libdeception-state-bridge.dylib"),
            new("Deception.ExecutionBridge.dylib","Deception.ExecutionBridge.sha256","Deception.ExecutionBridge.seams.json","libdeception-execution-bridge.dylib"),
            new("Deception.CommandBridge.dylib","Deception.CommandBridge.sha256","Deception.CommandBridge.seams.json","libdeception-command-bridge.dylib"))
#if DECEPTION_WINDOWS_PORT_CANDIDATE
            // Exact Windows image and separately audited helper payloads/prologues.
            // Selection requires verified loaded runtime dependencies.
            ,new("win-x64","win",Architecture.X64,8,"GameAssembly.dll",
                "bfc4f4de358a2fa129ab6621d2d2cb58aa56f8e4176fbc9ee13a316039d16926",
                new("Deception.Win.StateBridge.dll","Deception.Win.StateBridge.sha256","Deception.Win.StateBridge.seams.json","deception-state-bridge.dll"),
                new("Deception.Win.ExecutionBridge.dll","Deception.Win.ExecutionBridge.sha256","Deception.Win.ExecutionBridge.seams.json","deception-execution-bridge.dll"),
                new("Deception.Win.CommandBridge.dll","Deception.Win.CommandBridge.sha256","Deception.Win.CommandBridge.seams.json","deception-command-bridge.dll"))
#endif
        };
        static NativePlatformContract(){ValidateDescriptors(Profiles);}
        internal static Profile[] RegisteredProfiles()=>(Profile[])Profiles.Clone();
#if DECEPTION_WINDOWS_PORT_CANDIDATE
        internal static bool WindowsRuntimeVerified {get;private set;}
        // Admission is tied to runtime evidence, never an archive name, marker or
        // environment opt-in. Unknown detour builds fail before gameplay hooks.
        internal static void AdmitWindowsRuntime(string game,string loader,string dobby,string interop,string harmonySupport,string harmony)
        {
            WindowsRuntimeVerified=false;
            if(game!="bfc4f4de358a2fa129ab6621d2d2cb58aa56f8e4176fbc9ee13a316039d16926"||
                (loader!="a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016"&&
                 loader!="db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d")||
                dobby!="8015de7d867245a1095d13947a63763878e4cf5fd3d3089b63cc39200b055ded"||
                interop!="65ab051a681c2c1e52df7596ff738be401aae72cff882ff18241cf54a3ea38a4"||
                harmonySupport!="f7612ea2e5a4d473b771f34ee3b20cf32dc0c4ecea0185416474999a17fbcc07"||
                harmony!="6c898933b52149e8bcf6722305c7e4d94add47c9e53b364096f91721624c0877")
                throw new PlatformNotSupportedException("Unsupported Windows game/loader/hook runtime. Requires the audited game, unmodified PolyMod 1.2.18 or the supported multiplayer beta, and verified stock hook runtime. No opt-in can bypass this check.");
            WindowsRuntimeVerified=true;
        }
#endif
        internal static Profile Current()=>Select(
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX)?"osx":RuntimeInformation.IsOSPlatform(OSPlatform.Windows)?"win":
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)?"linux":"unknown",RuntimeInformation.ProcessArchitecture,IntPtr.Size);
        internal static Profile Select(string os,Architecture architecture,int pointerSize)
        {
#if DECEPTION_WINDOWS_PORT_CANDIDATE
            if(os=="win"&&!WindowsRuntimeVerified)
                throw new PlatformNotSupportedException("Windows runtime compatibility has not been verified. No native helper was admitted.");
#endif
            foreach(var profile in Profiles)
                if(profile.OperatingSystem==os&&profile.Architecture==architecture&&profile.PointerSize==pointerSize)return profile;
            throw new PlatformNotSupportedException("Deception has no verified native persistence profile for "+os+"/"+architecture+
                " (pointer size "+pointerSize+"). No native helper was loaded.");
        }
        internal static void RequireAdmitted(Profile profile)
        {
            foreach(var registered in Profiles)if(ReferenceEquals(profile,registered))return;
            throw new PlatformNotSupportedException("Unregistered Deception native platform contract.");
        }
    }
}
