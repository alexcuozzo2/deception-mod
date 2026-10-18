using System;
using System.IO;
using System.Security.Cryptography;
using Deception.PolyModAdapter;
namespace Deception.WindowsPortTests
{
    public static class WindowsApprovalTests
    {
        public static int Run()
        {
            string dir=Path.Combine(Path.GetTempPath(),"deception-approval-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string marker=Path.Combine(dir,"approval"),archive=Path.Combine(dir,"candidate");
            void Check(bool value){if(!value)throw new Exception("Windows archive approval regression.");}
            try
            {
                File.WriteAllText(archive,"test archive");
                Check(!NativeWindowsApproval.IsApproved(marker,archive));
                File.WriteAllText(marker,new string('0',64));
                Check(!NativeWindowsApproval.IsApproved(marker,archive));
                string digest=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))).ToLowerInvariant();
                File.WriteAllText(marker,digest+"\n");
                Check(NativeWindowsApproval.IsApproved(marker,archive));
                File.AppendAllText(archive," changed");
                Check(!NativeWindowsApproval.IsApproved(marker,archive));
                File.WriteAllText(marker,new string('a',129));
                Check(!NativeWindowsApproval.IsApproved(marker,archive));
                File.WriteAllText(marker,new string('x',64));
                Check(!NativeWindowsApproval.IsApproved(marker,archive));
                File.Delete(archive);
                Check(!NativeWindowsApproval.IsApproved(marker,archive));
                Console.WriteLine("Windows archive approval: 7 checks passed.");return 0;
            }
            finally{File.Delete(marker);if(File.Exists(archive))File.Delete(archive);Directory.Delete(dir);}
        }
    }
}
