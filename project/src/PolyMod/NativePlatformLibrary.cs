using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Deception.PolyModAdapter
{
    // Shared loader boundary. No game lookup by guessed offsets, implicit
    // architecture fallback, package download or process-lifetime unload.
    internal static class NativePlatformLibrary
    {
        const int MaxArtifactBytes=1024*1024-1;
        [StructLayout(LayoutKind.Sequential)] struct ImageInfo {internal IntPtr Name,Base,Symbol,Address;}
        [DllImport("/usr/lib/libSystem.B.dylib",EntryPoint="dladdr",CallingConvention=CallingConvention.Cdecl)]
        static extern int MacAddress(IntPtr address,out ImageInfo info);
        [DllImport("libdl.so.2",EntryPoint="dladdr",CallingConvention=CallingConvention.Cdecl)]
        static extern int LinuxAddress(IntPtr address,out ImageInfo info);
        [DllImport("libc.so.6",EntryPoint="dladdr",CallingConvention=CallingConvention.Cdecl)]
        static extern int LinuxLibcAddress(IntPtr address,out ImageInfo info);
        [DllImport("kernel32.dll",EntryPoint="GetModuleHandleExW",ExactSpelling=true,SetLastError=true)]
        static extern int WindowsAddress(uint flags,IntPtr address,out IntPtr module);
        static void Require(bool condition,string reason)
        {if(!condition)throw new InvalidDataException("Deception native platform: "+reason);}
        internal static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        static bool HexDigest(string value)=>value.Length==64&&Array.TrueForAll(value.ToCharArray(),c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
        static void Regular(string path)
        {Require(File.Exists(path)&&(File.GetAttributes(path)&(FileAttributes.ReparsePoint|FileAttributes.Directory))==0,"Expected regular file: "+path);}
        static void DirectorySafe(string path,bool create)
        {
            // Inspect before CreateDirectory too: a dangling symlink must not
            // be treated as an absent directory and followed while creating it.
            if(Directory.Exists(path)||File.Exists(path)||new FileInfo(path).LinkTarget is not null)
                Require((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0,"Linked directory rejected: "+path);
            if(create)Directory.CreateDirectory(path);
            Require(Directory.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)==0,"Expected ordinary directory: "+path);
        }
        static string Root(string gameRoot)
        {
            Require(Path.IsPathFullyQualified(gameRoot),"Game root must be an absolute loader path.");
            string root=Path.GetFullPath(gameRoot);DirectorySafe(root,false);return root;
        }
        internal static string ValidateGame(NativePlatformContract.Profile profile,string gameRoot)
        {
            NativePlatformContract.RequireAdmitted(profile);
            string root=Root(gameRoot),path=root;var pieces=profile.GameRelativePath.Split('/');
            for(int i=0;i<pieces.Length;i++)
            {path=Path.Combine(path,pieces[i]);if(i<pieces.Length-1)DirectorySafe(path,false);}
            Regular(path);
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            using var algorithm=SHA256.Create();
            string digest=Convert.ToHexString(algorithm.ComputeHash(input)).ToLowerInvariant();
            Require(digest==profile.GameSha256,"Game binary differs from the audited "+profile.Rid+" contract.");return path;
        }
        static byte[] ReadBounded(Stream input,int maximum)
        {
            using(input)using(var output=new MemoryStream())
            {
                var buffer=new byte[8192];int count;
                while((count=input.Read(buffer,0,Math.Min(buffer.Length,maximum+1-(int)output.Length)))>0)
                {output.Write(buffer,0,count);Require(output.Length<=maximum,"Embedded artifact exceeds its bound.");}
                Require(output.Length>0,"Embedded artifact is empty.");return output.ToArray();
            }
        }
        internal static string ExtractOwn(NativePlatformContract.Profile profile,NativePlatformContract.BridgeKind kind,
            string gameRoot,Func<string,Stream?> resource)
        {
            NativePlatformContract.RequireAdmitted(profile);
            var artifact=profile.GetArtifact(kind);
            // Validate all embedded bytes before making the first directory.
            var bytes=ReadBounded(resource(artifact.Resource)??throw new InvalidDataException("Missing native resource: "+artifact.Resource),MaxArtifactBytes);
            var digestBytes=ReadBounded(resource(artifact.DigestResource)??throw new InvalidDataException("Missing native digest: "+artifact.DigestResource),128);
            string expected=Encoding.UTF8.GetString(digestBytes).Trim();
            Require(HexDigest(expected)&&Hash(bytes)==expected,"Embedded native artifact integrity mismatch.");
            string root=Root(gameRoot),parent=Path.Combine(root,"DeceptionNative");DirectorySafe(parent,true);
            string platform=Path.Combine(parent,profile.Rid);DirectorySafe(platform,true);
            string folder=Path.Combine(platform,expected);DirectorySafe(folder,true);
            string path=Path.Combine(folder,artifact.FileName);
            if(!File.Exists(path))
            {
                Require(new FileInfo(path).LinkTarget is null,"Linked artifact rejected: "+path);
                try{using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);output.Write(bytes);output.Flush(true);}
                catch(IOException) when(File.Exists(path)){} // A concurrent exact publisher is verified below.
            }
            DirectorySafe(parent,false);DirectorySafe(platform,false);DirectorySafe(folder,false);Regular(path);
            using var existing=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            Require(existing.Length==bytes.Length&&Hash(ReadBounded(existing,MaxArtifactBytes))==expected,"Extracted native artifact changed.");
            return path;
        }
        internal static IntPtr LoadOwn(NativePlatformContract.Profile profile,NativePlatformContract.BridgeKind kind,string gameRoot,Assembly assembly)
        {
            NativePlatformContract.RequireAdmitted(profile);
            Require(ReferenceEquals(profile,NativePlatformContract.Current()),"Platform changed before library loading.");
            return NativeLibrary.Load(ExtractOwn(profile,kind,gameRoot,assembly.GetManifestResourceStream));
        }
        internal static void RequireSameImage(NativePlatformContract.Profile profile,IntPtr target,IntPtr gameExport)
        {
            NativePlatformContract.RequireAdmitted(profile);
            Require(ReferenceEquals(profile,NativePlatformContract.Current()),"Platform changed before image validation.");
            RequireSameImage(target,gameExport,address=>ImageBase(profile.OperatingSystem,address));
        }
        internal static void RequireSameImage(IntPtr target,IntPtr gameExport,Func<IntPtr,IntPtr> resolver)
        {
            Require(target!=IntPtr.Zero&&gameExport!=IntPtr.Zero,"Null native method or reference export.");
            IntPtr first=resolver(target),second=resolver(gameExport);
            Require(first!=IntPtr.Zero&&first==second,"Resolved method does not belong to the loaded native game image.");
        }
        // OS APIs identify the containing module, never infer a game offset.
        // Windows/Linux implementations are preparation only: no profile above
        // admits them until game, helper, unwinding and seam evidence exists.
        internal static IntPtr ImageBase(string os,IntPtr address)
        {
            if(address==IntPtr.Zero)return IntPtr.Zero;
            if(os=="osx"&&RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return MacAddress(address,out var mac)!=0?mac.Base:IntPtr.Zero;
            if(os=="win"&&RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return WindowsAddress(0x00000004|0x00000002,address,out var windows)!=0?windows:IntPtr.Zero;
            if(os=="linux"&&RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                try{return LinuxAddress(address,out var linux)!=0?linux.Base:IntPtr.Zero;}
                catch(DllNotFoundException){return LinuxLibcAddress(address,out var linux)!=0?linux.Base:IntPtr.Zero;}
                catch(EntryPointNotFoundException){return LinuxLibcAddress(address,out var linux)!=0?linux.Base:IntPtr.Zero;}
            }
            throw new PlatformNotSupportedException("No native image resolver for the current process: "+os);
        }
    }
}
