using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Deception.PolyModAdapter;
using P=Deception.PolyModAdapter.NativePlatformContract.Profile;
using A=Deception.PolyModAdapter.NativePlatformContract.Artifact;
using K=Deception.PolyModAdapter.NativePlatformContract.BridgeKind;

namespace Deception.Tests
{
    public static partial class PlatformTests
    {
        // These synthetic descriptors test validation only. They are never
        // registered, extracted or loaded, and contain no target-game claim.
        static A Art(string os,string kind,string? prefix=null,string? file=null)
        {
            string extension=os=="osx"?".dylib":os=="win"?".dll":".so";
            prefix??="Deception."+os+"."+kind+"Bridge";
            return new(prefix+extension,prefix+".sha256",prefix+".seams.json",file??(os=="win"?"":"lib")+"deception-"+kind.ToLowerInvariant()+"-bridge"+extension);
        }
        static P Draft(string os)=>new(os+"-x64",os,Architecture.X64,8,"FixtureGame"+(os=="osx"?".dylib":os=="win"?".dll":".so"),new string('1',64),Art(os,"State"),Art(os,"Execution"),Art(os,"Command"));
        static P Copy(P source,string? rid=null,string? os=null,Architecture? arch=null,int? pointer=null,string? game=null,string? hash=null,A? state=null,A? execution=null,A? command=null)
            =>new(rid??source.Rid,os??source.OperatingSystem,arch??source.Architecture,pointer??source.PointerSize,game??source.GameRelativePath,hash??source.GameSha256,
                state??source.GetArtifact(K.State),execution??source.GetArtifact(K.Execution),command??source.GetArtifact(K.Command));
        static void DescriptorCases()
        {
            Test("PORT-D01-mac-contract-retained",()=>{
                Check(Profile.Rid=="osx-x64"&&Profile.Architecture==Architecture.X64&&Profile.PointerSize==8&&Profile.LibraryExtension==".dylib","exact known Mac ABI");
                Check(Profile.GameRelativePath=="Polytopia.app/Contents/Frameworks/GameAssembly.dylib"&&Profile.GameSha256=="5391c27cf9e63ba3db10b168ba7435da723853b265695821f31942bb7ab95dc0","exact observed game identity");
                foreach(var kind in Enum.GetValues<K>())
                {
                    var a=Profile.GetArtifact(kind);string name=kind==K.State?"State":kind==K.Execution?"Execution":"Command";
                    Check(a.Resource=="Deception."+name+"Bridge.dylib"&&a.DigestResource=="Deception."+name+"Bridge.sha256"&&a.SeamsResource=="Deception."+name+"Bridge.seams.json","unchanged embedded names for "+kind);
                }
            });
            foreach(string os in new[]{"win","linux"})Test("PORT-D02-describe-without-admitting-"+os,()=>{
                var profile=Draft(os);NativePlatformContract.ValidateDescriptors(Profile,profile);
                Check(profile.Rid==os+"-x64"&&profile.GetArtifact(K.State).FileName==(os=="win"?"deception-state-bridge.dll":"libdeception-state-bridge.so"),"correct platform descriptor and CMake basename");
                int reads=0;Reject(()=>NativePlatformLibrary.ExtractOwn(profile,K.State,Root,n=>{reads++;return Resource(n);}),"unregistered descriptor before resource IO");
                Reject(()=>NativePlatformLibrary.LoadOwn(profile,K.State,Root,typeof(PlatformTests).Assembly),"unregistered descriptor before native loading");
                Reject(()=>NativePlatformLibrary.ValidateGame(profile,Root),"unregistered descriptor before game access");
                Reject(()=>NativePlatformLibrary.RequireSameImage(profile,new(1),new(2)),"unregistered descriptor before image resolver");
                Reject(()=>NativePlatformContract.Select(os,Architecture.X64,8),"valid descriptor does not register itself");
                Check(reads==0&&!Directory.Exists(Path.Combine(Root,"DeceptionNative")),"no unverified native artifact writes");
            });
            Test("PORT-D03-multiple-platform-resource-descriptors",()=>{
                NativePlatformContract.ValidateDescriptors(Profile,Draft("win"),Draft("linux"));
                Check(NativePlatformContract.RegisteredProfiles().Length==1,"describing multiple packaged resources does not admit targets");
            });
            Test("PORT-D04-defensive-registered-array",()=>{
                var profiles=NativePlatformContract.RegisteredProfiles();profiles[0]=Draft("win");
                Check(ReferenceEquals(NativePlatformContract.Current(),Profile)&&ReferenceEquals(NativePlatformContract.RegisteredProfiles()[0],Profile),"caller cannot alter admission table");
                Reject(()=>NativePlatformContract.RequireAdmitted(profiles[0]),"array replacement is unregistered");
            });
            foreach(string rid in new[]{"win-x64","osx-arm64","../osx-x64","osx-x64/extra","OSX-X64",""})Test("PORT-D05-rid-"+rid,()=>Reject(()=>Copy(Profile,rid:rid),"RID must match exact OS and architecture"));
            foreach(string os in new[]{"freebsd","OSX","","osx/../win"})Test("PORT-D06-os-"+os,()=>Reject(()=>Copy(Profile,os:os),"unknown OS rejected"));
            Test("PORT-D07-unimplemented-architecture",()=>Reject(()=>Copy(Profile,arch:Architecture.Arm64),"descriptor cannot imply new bridge ABI"));
            Test("PORT-D08-pointer-width",()=>Reject(()=>Copy(Profile,pointer:4),"x64 requires audited pointer width"));
            foreach(string path in new[]{"/GameAssembly.dylib","../GameAssembly.dylib","sub/../GameAssembly.dylib","sub//GameAssembly.dylib","C:/GameAssembly.dylib","sub\\GameAssembly.dylib","GameAssembly.dylib/","./GameAssembly.dylib","NUL/GameAssembly.dylib","sub./GameAssembly.dylib","GameAssembly.dll",new string('a',129)+".dylib"})
                Test("PORT-D09-game-path-"+path,()=>Reject(()=>Copy(Profile,game:path),"portable bounded relative image path"));
            foreach(string hash in new[]{"",new string('a',63),new string('a',65),new string('A',64),new string('z',64)})Test("PORT-D10-sha-"+hash.Length+"-"+(hash.Length>0?hash[0]:'_'),()=>Reject(()=>Copy(Profile,hash:hash),"exact lowercase SHA-256"));
            foreach(string file in new[]{"../libbad.dylib","sub/libbad.dylib","sub\\libbad.dylib","C:libbad.dylib","libbad.dylib ","libbad.dylib.","NUL.dylib","com1.dylib","a\0.dylib","libbad.dll"})Test("PORT-D11-artifact-name-"+file,()=>Reject(()=>Copy(Profile,state:Art("osx","State",file:file)),"safe platform-specific native basename"));
            foreach(string resource in new[]{"../StateBridge.dylib","Other.StateBridge.dylib","Deception.a/b.dylib","Deception.a\\b.dylib","Deception..a.dylib","Deception.a.dylib ","Deception."+new string('a',192)+".dylib"})
                Test("PORT-D12-resource-name-"+resource,()=>Reject(()=>new A(resource,"Deception.State.sha256","Deception.State.seams.json","libstate.dylib"),"bounded exact own-assembly key"));
            Test("PORT-D13-binary-extension",()=>Reject(()=>Copy(Profile,state:new A("Deception.State.dll","Deception.State.sha256","Deception.State.seams.json","libstate.dylib")),"resource binary matches platform extension"));
            Test("PORT-D14-digest-stem",()=>Reject(()=>Copy(Profile,state:new A("Deception.State.dylib","Deception.Other.sha256","Deception.State.seams.json","libstate.dylib")),"digest cannot bind another artifact"));
            Test("PORT-D15-seam-stem",()=>Reject(()=>Copy(Profile,state:new A("Deception.State.dylib","Deception.State.sha256","Deception.Other.seams.json","libstate.dylib")),"seams cannot bind another artifact"));
            Test("PORT-D16-resource-collision",()=>{
                var a=Profile.GetArtifact(K.State);
                Reject(()=>Copy(Profile,execution:new A(a.Resource,a.DigestResource,a.SeamsResource,"libdistinct-execution.dylib")),"bridge resources cannot alias even with distinct files");
            });
            Test("PORT-D17-basename-case-collision",()=>Reject(()=>Copy(Profile,execution:Art("osx","Execution",file:Profile.GetArtifact(K.State).FileName.ToUpperInvariant().Replace(".DYLIB",".dylib"))),"case-insensitive filesystem collision"));
            Test("PORT-D18-required-bridge",()=>Reject(()=>new P(Profile.Rid,Profile.OperatingSystem,Profile.Architecture,Profile.PointerSize,Profile.GameRelativePath,Profile.GameSha256,Profile.GetArtifact(K.State),null!,Profile.GetArtifact(K.Command)),"missing execution artifact"));
            Test("PORT-D19-duplicate-selector",()=>Reject(()=>NativePlatformContract.ValidateDescriptors(Profile,Copy(Profile)),"same platform cannot shadow a registered selector"));
            Test("PORT-D20-cross-platform-resource-collision",()=>{
                var mac=Copy(Profile,state:Art("osx","State","Deception.Shared"));var win=Copy(Draft("win"),state:Art("win","State","Deception.Shared"));
                Reject(()=>NativePlatformContract.ValidateDescriptors(mac,win),"different extensions must not share digest/seam resource names");
            });
            Test("PORT-D21-bounded-profile-table",()=>{
                Reject(()=>NativePlatformContract.ValidateDescriptors(),"empty table");Reject(()=>NativePlatformContract.ValidateDescriptors(null!),"null table");
                Reject(()=>NativePlatformContract.ValidateDescriptors(new P[17]),"oversized table");Reject(()=>NativePlatformContract.ValidateDescriptors(new P[]{null!}),"null row");
            });
            Test("PORT-D22-null-descriptor-fields",()=>{
                Reject(()=>new A(null!,"Deception.State.sha256","Deception.State.seams.json","libstate.dylib"),"null resource");
                Reject(()=>new A("Deception.State.dylib","Deception.State.sha256","Deception.State.seams.json",null!),"null basename");
                Reject(()=>new P(Profile.Rid,"osx",Architecture.X64,8,null!,Profile.GameSha256,Profile.GetArtifact(K.State),Profile.GetArtifact(K.Execution),Profile.GetArtifact(K.Command)),"null game path");
            });
        }
    }
}
