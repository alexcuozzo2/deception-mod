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
                Reject(()=>NativePlatformContract.Select("win",Architecture.X64,8));
                string[] pins={
                    "bfc4f4de358a2fa129ab6621d2d2cb58aa56f8e4176fbc9ee13a316039d16926",
                    "a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016",
                    "8015de7d867245a1095d13947a63763878e4cf5fd3d3089b63cc39200b055ded",
                    "65ab051a681c2c1e52df7596ff738be401aae72cff882ff18241cf54a3ea38a4",
                    "f7612ea2e5a4d473b771f34ee3b20cf32dc0c4ecea0185416474999a17fbcc07",
                    "6c898933b52149e8bcf6722305c7e4d94add47c9e53b364096f91721624c0877"};
                void Admit(string[] values)=>NativePlatformContract.AdmitWindowsRuntime(values[0],values[1],values[2],values[3],values[4],values[5]);
                Admit(pins);
                for(int i=0;i<pins.Length;i++) {
                    var bad=(string[])pins.Clone();bad[i]=new string('0',64);
                    Reject(()=>Admit(bad));
                    Reject(()=>NativePlatformContract.Select("win",Architecture.X64,8));
                    Admit(pins);
                }
                var adapted=(string[])pins.Clone();adapted[1]="61588813d4b62d133db06fad64a2c440869aaffaf7460ec8bc8e9d82b1be63d7";
                Reject(()=>Admit(adapted));
                var publicPins=(string[])pins.Clone();publicPins[1]="db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d";
                Admit(publicPins);
                Require(NativePlatformContract.WindowsRuntimeVerified,"Public local profile rejected");
                Require(NativePlatformContract.Select("win",Architecture.X64,8).GameRelativePath=="GameAssembly.dll","Public Windows profile");
                for(int i=0;i<publicPins.Length;i++) {
                    var bad=(string[])publicPins.Clone();bad[i]=new string('0',64);
                    Reject(()=>Admit(bad));
                    Reject(()=>NativePlatformContract.Select("win",Architecture.X64,8));
                    Admit(publicPins);
                }
                Environment.SetEnvironmentVariable("DECEPTION_WINDOWS_PORT_TEST",null);
                Admit(pins);
                var win=NativePlatformContract.Select("win",Architecture.X64,8);
                Require(win.GameRelativePath=="GameAssembly.dll","Windows image path");
                Require(win.GameSha256=="bfc4f4de358a2fa129ab6621d2d2cb58aa56f8e4176fbc9ee13a316039d16926","Windows image pin");
                foreach(var kind in Enum.GetValues<NativePlatformContract.BridgeKind>())
                    Require(win.GetArtifact(kind).Resource!=mac.GetArtifact(kind).Resource,"Cross-platform resource collision");
                Reject(()=>NativePlatformContract.Select("win",Architecture.Arm64,8));
                Reject(()=>NativePlatformContract.Select("win",Architecture.X64,4));
                Reject(()=>NativePlatformContract.Select("linux",Architecture.X64,8));
                Require(NativePlatformContract.Select("win",Architecture.X64,8).GameSha256==win.GameSha256,"Normal launch changed game pin");
                var invalid=(string[])pins.Clone();invalid[2]="";
                Reject(()=>Admit(invalid));
                Reject(()=>NativePlatformContract.Select("win",Architecture.X64,8));
                Require(NativePlatformContract.Select("osx",Architecture.X64,8)==mac,"Windows rejection changed Mac admission");
                Console.WriteLine("PASS Windows candidate admission and cross-platform resource gates");return 0;
            }catch(Exception error){Console.Error.WriteLine(error);return 1;}
            finally{Environment.SetEnvironmentVariable("DECEPTION_WINDOWS_PORT_TEST",prior);}
        }
    }
}
