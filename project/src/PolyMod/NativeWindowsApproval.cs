#if DECEPTION_WINDOWS_PORT_CANDIDATE
using System;
using System.IO;
using System.Security.Cryptography;

namespace Deception.PolyModAdapter
{
    // Installation approval is bound to one exact archive, not a permanent
    // permission for arbitrary future candidates or game/loader versions.
    internal static class NativeWindowsApproval
    {
        internal static bool IsApproved(string approvalPath,string archivePath)
        {
            try
            {
                var approval=new FileInfo(approvalPath);var archive=new FileInfo(archivePath);
                if(!approval.Exists||!archive.Exists||approval.Length>128||archive.Length>32*1024*1024||
                    (approval.Attributes&FileAttributes.ReparsePoint)!=0||(archive.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                string expected=File.ReadAllText(approvalPath).Trim();
                if(expected.Length!=64)return false;
                foreach(char c in expected)if(!((c>='0'&&c<='9')||(c>='a'&&c<='f')))return false;
                using var input=File.OpenRead(archivePath);using var sha=SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(input)).ToLowerInvariant()==expected;
            }
            catch(IOException){return false;}
            catch(UnauthorizedAccessException){return false;}
        }
    }
}
#endif
