using System;
using System.Runtime.InteropServices;
using Deception.PolyModAdapter;
namespace Deception.WindowsPortTests {
    public static class CandidateGateTests {
        static void Require(bool value,string reason) { if(!value)throw new Exception(reason); }
        static void Reject(Action action) {
            try { action(); } catch(PlatformNotSupportedException) { return; }
            throw new Exception("Unsupported target was admitted");
        }
        public static int Run() {
            string? prior=Environment.GetEnvironmentVariable("DECEPTION_WINDOWS_PORT_TEST");
            try {
                Environment.SetEnvironmentVariable("DECEPTION_WINDOWS_PORT_TEST",null);
                Reject(()=>NativePlatformContract.Select("win",Architecture.X64,8));
                var mac=NativePlatformContract.Select("osx",Architecture.X64,8);
                Require(mac.GameRelativePath.EndsWith("GameAssembly.dylib"),"Mac changed");
                Environment.SetEnvironmentVariable("DECEPTION_WINDOWS_PORT_TEST","1");
                var win=NativePlatformContract.Select("win",Architecture.X64,8);
                Require(win.GameRelativePath=="GameAssembly.dll","Windows image path");
                Require(win.GameSha256=="bfc4f4de358a2fa129ab6621d2d2cb58aa56f8e4176fbc9ee13a316039d16926","Windows image pin");
                foreach(var kind in Enum.GetValues<NativePlatformContract.BridgeKind>())
                    Require(win.GetArtifact(kind).Resource!=mac.GetArtifact(kind).Resource,"Cross-platform resource collision");
                Reject(()=>NativePlatformContract.Select("win",Architecture.Arm64,8));
                Reject(()=>NativePlatformContract.Select("win",Architecture.X64,4));
                Reject(()=>NativePlatformContract.Select("linux",Architecture.X64,8));
                Environment.SetEnvironmentVariable("DECEPTION_WINDOWS_PORT_TEST",null);
                Reject(()=>NativePlatformContract.Select("win",Architecture.X64,8));
                NativePlatformContract.WindowsInstallationApproved=true;
                Require(NativePlatformContract.Select("win",Architecture.X64,8).GameSha256==win.GameSha256,"Approved install changed compatibility pin");
                Reject(()=>NativePlatformContract.Select("win",Architecture.Arm64,8));
                NativePlatformContract.WindowsInstallationApproved=false;
                Reject(()=>NativePlatformContract.Select("win",Architecture.X64,8));
                Console.WriteLine("PASS Windows candidate admission and cross-platform resource gates");return 0;
            }catch(Exception error){Console.Error.WriteLine(error);return 1;}
            finally{NativePlatformContract.WindowsInstallationApproved=false;Environment.SetEnvironmentVariable("DECEPTION_WINDOWS_PORT_TEST",prior);}
        }
    }
}
