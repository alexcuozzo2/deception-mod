using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
    public static partial class PlatformTests
    {
        static int Cases,Assertions;static readonly List<string> Names=new();
        static readonly NativePlatformContract.Profile Profile=NativePlatformContract.Select("osx",Architecture.X64,8);
        static readonly byte[] Bytes=Encoding.UTF8.GetBytes("own-mod fixture artifact; never executed");
        static string Root=>Path.Combine(Environment.GetEnvironmentVariable("DECEPTION_PLATFORM_TEST_ROOT")!,Cases.ToString());
        static void Check(bool ok,string why){Assertions++;if(!ok)throw new Exception(why);}
        static void Reject(Action action,string why){try{action();}catch(Exception e)when(e is InvalidDataException or NotSupportedException or IOException or UnauthorizedAccessException){Check(true,why);return;}throw new Exception("Expected rejection: "+why);}
        static void Test(string name,Action action){Cases++;Names.Add(name);Directory.CreateDirectory(Root);try{action();}catch(Exception e){throw new Exception(name,e);}}
        static Stream? Resource(string name)=>new MemoryStream(name.EndsWith(".sha256",StringComparison.Ordinal)?Encoding.UTF8.GetBytes(NativePlatformLibrary.Hash(Bytes)+"\n"):Bytes);
        static string Extract()=>NativePlatformLibrary.ExtractOwn(Profile,NativePlatformContract.BridgeKind.State,Root,Resource);
        static string Folder=>Path.Combine(Root,"DeceptionNative",Profile.Rid,NativePlatformLibrary.Hash(Bytes));
        static string Artifact=>Path.Combine(Folder,Profile.GetArtifact(NativePlatformContract.BridgeKind.State).FileName);
        static void GameTree(){Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Root,Profile.GameRelativePath))!);File.WriteAllText(Path.Combine(Root,Profile.GameRelativePath),"wrong game");}
        public static int Run(){try{
            Test("PORT-current-mac-exact-profile",()=>Check(ReferenceEquals(Profile,NativePlatformContract.Current()),"only actual x64 Mac process matches"));
            foreach(string os in new[]{"win","linux","unknown","OSX","","../osx"})Test("PORT-reject-os-"+os,()=>Reject(()=>NativePlatformContract.Select(os,Architecture.X64,8),"unverified OS"));
            foreach(var arch in new[]{Architecture.X86,Architecture.Arm,Architecture.Arm64,(Architecture)999})Test("PORT-reject-architecture-"+arch,()=>Reject(()=>NativePlatformContract.Select("osx",arch,8),"unverified architecture"));
            foreach(int size in new[]{0,4,16})Test("PORT-reject-pointer-size-"+size,()=>Reject(()=>NativePlatformContract.Select("osx",Architecture.X64,size),"unverified pointer width"));
            Test("PORT-unregistered-profile-before-resource-or-load",()=>{
                int reads=0;Reject(()=>NativePlatformLibrary.ExtractOwn(Copy(Profile),NativePlatformContract.BridgeKind.State,Root,n=>{reads++;return Resource(n);}),"forged profile");
                Check(reads==0&&!Directory.Exists(Path.Combine(Root,"DeceptionNative")),"no artifact IO before admission");
                Reject(()=>NativePlatformLibrary.LoadOwn(Copy(Profile),NativePlatformContract.BridgeKind.State,Root,typeof(PlatformTests).Assembly),"no library load for unregistered contract");
            });
            Test("PORT-all-three-embedded-kinds-distinct",()=>{
                var kinds=Enum.GetValues<NativePlatformContract.BridgeKind>().Select(Profile.GetArtifact).ToArray();
                Check(kinds.Select(x=>x.Resource).Distinct().Count()==3&&kinds.Select(x=>x.FileName).Distinct().Count()==3,"independent embedded helpers");
                Check(kinds.All(x=>x.SeamsResource.EndsWith(".seams.json")&&x.DigestResource.EndsWith(".sha256")),"every helper has pinned seam/digest resources");
                Reject(()=>Profile.GetArtifact((NativePlatformContract.BridgeKind)99),"unknown artifact");
            });
            DescriptorCases();
            Test("PORT-extract-once-and-preserve-exact-file",()=>{
                string p=Extract();DateTime stamp=File.GetLastWriteTimeUtc(p);string next=Extract();
                Check(p==next&&p.Contains(Path.Combine("DeceptionNative","osx-x64"))&&File.ReadAllBytes(p).SequenceEqual(Bytes),"RID and content-addressed exact artifact");
                Check(File.GetLastWriteTimeUtc(p)==stamp,"repeat must not rewrite existing verified library");
            });
            foreach(string bad in new[]{"empty","oversized","exact bound","missing","digest missing","digest huge","digest wrong","digest malformed"})Test("PORT-embedded-reject-"+bad,()=>{
                Stream? Read(string name){bool digest=name.EndsWith(".sha256");if((bad=="missing"&&!digest)||(bad=="digest missing"&&digest))return null;
                    if(!digest&&bad=="empty")return new MemoryStream();if(!digest&&bad=="oversized")return new MemoryStream(new byte[1024*1024+1]);if(!digest&&bad=="exact bound")return new MemoryStream(new byte[1024*1024]);
                    if(digest&&bad=="digest huge")return new MemoryStream(new byte[129]);if(digest&&bad=="digest wrong")return new MemoryStream(Encoding.UTF8.GetBytes(new string('0',64)));
                    if(digest&&bad=="digest malformed")return new MemoryStream(Encoding.UTF8.GetBytes(new string('X',64)));return Resource(name);}
                Reject(()=>NativePlatformLibrary.ExtractOwn(Profile,NativePlatformContract.BridgeKind.State,Root,Read),"invalid embedded payload");
                Check(!Directory.Exists(Path.Combine(Root,"DeceptionNative")),"failure creates no native cache directory");
            });
            Test("PORT-existing-artifact-tamper-not-overwritten",()=>{string p=Extract();File.WriteAllText(p,"tampered");Reject(()=>Extract(),"tampered cache");Check(File.ReadAllText(p)=="tampered","no overwrite to conceal mismatch");});
            foreach(string level in new[]{"root","platform","digest","artifact","dangling artifact"})Test("PORT-linked-cache-reject-"+level,()=>{
                string other=Path.Combine(Root,"other");Directory.CreateDirectory(other);
                if(level=="root")Directory.CreateSymbolicLink(Path.Combine(Root,"DeceptionNative"),other);
                else if(level=="platform"){Directory.CreateDirectory(Path.Combine(Root,"DeceptionNative"));Directory.CreateSymbolicLink(Path.Combine(Root,"DeceptionNative",Profile.Rid),other);}
                else if(level=="digest"){Directory.CreateDirectory(Path.GetDirectoryName(Folder)!);Directory.CreateSymbolicLink(Folder,other);}
                else{Directory.CreateDirectory(Folder);string target=Path.Combine(other,"payload");if(level=="artifact")File.WriteAllBytes(target,Bytes);File.CreateSymbolicLink(Artifact,target);}
                Reject(()=>Extract(),"linked own library cache");Check(!Directory.EnumerateFiles(other).Any(x=>Path.GetFileName(x).StartsWith("libdeception")),"link target unchanged");
            });
            Test("PORT-relative-game-root-rejected",()=>Reject(()=>NativePlatformLibrary.ExtractOwn(Profile,NativePlatformContract.BridgeKind.State,"relative",Resource),"relative root"));
            Test("PORT-game-binary-exact-existing-hash",()=>Check(File.Exists(NativePlatformLibrary.ValidateGame(Profile,Environment.GetEnvironmentVariable("DECEPTION_GAME_ROOT")!)),"existing pinned game verified read-only"));
            Test("PORT-wrong-game-hash-rejected-before-cache",()=>{GameTree();Reject(()=>NativePlatformLibrary.ValidateGame(Profile,Root),"different native game");Check(!Directory.Exists(Path.Combine(Root,"DeceptionNative")),"no own library extraction on game rejection");});
            Test("PORT-linked-game-image-rejected",()=>{GameTree();string p=Path.Combine(Root,Profile.GameRelativePath);File.Delete(p);File.CreateSymbolicLink(p,Path.Combine(Environment.GetEnvironmentVariable("DECEPTION_GAME_ROOT")!,Profile.GameRelativePath));Reject(()=>NativePlatformLibrary.ValidateGame(Profile,Root),"linked game file");});
            Test("PORT-linked-game-component-rejected",()=>{string p=Path.Combine(Root,"Polytopia.app");Directory.CreateSymbolicLink(p,Path.Combine(Environment.GetEnvironmentVariable("DECEPTION_GAME_ROOT")!,"Polytopia.app"));Reject(()=>NativePlatformLibrary.ValidateGame(Profile,Root),"linked game parent");});
            Test("PORT-image-identity-exact-base-required",()=>{
                NativePlatformLibrary.RequireSameImage(new(1),new(2),_=>new(100));Check(true,"same containing module");
                Reject(()=>NativePlatformLibrary.RequireSameImage(new(1),new(2),p=>p),"different module");
                Reject(()=>NativePlatformLibrary.RequireSameImage(new(1),new(2),_=>IntPtr.Zero),"unresolved module");int calls=0;
                Reject(()=>NativePlatformLibrary.RequireSameImage(IntPtr.Zero,new(2),p=>{calls++;return p;}),"null target");Check(calls==0,"null address never passed to OS resolver");
            });
            Test("PORT-cross-os-resolvers-do-not-call-foreign-APIs",()=>{Reject(()=>NativePlatformLibrary.ImageBase("win",new(1)),"Windows resolver on Mac");Reject(()=>NativePlatformLibrary.ImageBase("linux",new(1)),"Linux resolver on Mac");});
            Test("PORT-real-own-library-load-and-image-resolver",()=>{
                IntPtr own=NativePlatformLibrary.LoadOwn(Profile,NativePlatformContract.BridgeKind.State,Root,typeof(PlatformTests).Assembly);
                IntPtr a=NativeLibrary.GetExport(own,"dcp_state_bridge_abi_version"),b=NativeLibrary.GetExport(own,"dcp_state_bridge_site_count");
                NativePlatformLibrary.RequireSameImage(Profile,a,b);Check(NativePlatformLibrary.ImageBase("osx",a)!=IntPtr.Zero,"real Mac dladdr on own native library");
                IntPtr system=NativeLibrary.Load("/usr/lib/libSystem.B.dylib");Reject(()=>NativePlatformLibrary.RequireSameImage(Profile,a,NativeLibrary.GetExport(system,"malloc")),"real foreign native image rejected");
                // Keep native handles loaded, matching production callback lifetime.
            });
            Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases,assertions=Assertions,tests=Names,nativeGame="NOT RUN",nativeOs="Mac x64 own helper only"}));return 0;
        }catch(Exception e){Console.WriteLine(e);Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases,assertions=Assertions}));return 1;}}
    }
}
