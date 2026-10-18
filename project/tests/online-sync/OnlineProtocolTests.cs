using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Deception.Core;

namespace Deception.Tests
{
    // Real Core codecs and production planner over the retained native-shaped
    // land fixture. No backend, native GameState serializer or game is executed.
    public static partial class OnlineProtocolTests
    {
        static int Cases,Assertions;
        static readonly List<string> Names=new();
        static readonly Guid Epoch=new("9ee29371-43b5-46d0-a924-c766a0a3a650");
        static readonly Guid Foreign=new("e8a66a39-247d-4cc8-95fa-c7c0dc18f861");
        static readonly string Contract=new('a',64);
        static void Check(bool ok,string message){Assertions++;if(!ok)throw new Exception(message);}
        static void Reject(Action action,string message)
        {try{action();}catch(Exception e)when(e is InvalidDataException or ArgumentException){Check(true,message);return;}throw new Exception("Expected rejection: "+message);}
        static void Test(string name,Action run){Names.Add(name);Cases++;try{run();}catch(Exception e){throw new Exception(name,e);}}
        static NativeIdentityLedger Empty(string? contract=null)=>new(Epoch,1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>(),OnlineContract:contract);
        static byte[] NativeBytes()=>new byte[]{123,0,0,0,8,6,4,2};
        static byte[] Frame(NativeIdentityLedger? ledger=null,long revision=0)=>NativeStateFrame.Encode(NativeBytes(),NativeIdentityLedgerCodec.Encode(ledger??Empty(Contract),revision),revision,123);
        static byte[] ChangedFrame(int offset)
        {var bytes=Frame();bytes[offset]^=1;return bytes;}
        static void DescriptorCases()
        {
            Test("ONLINE-001-current-frame-owned-sections-and-contract",()=>{
                var frame=Frame();var d=OnlineSnapshotDescriptor.Validate(frame,Epoch,Contract);
                Check(d.Epoch==Epoch&&d.NativeVersion==123&&d.FrameSchema==1&&d.LedgerSchema==NativeIdentityLedgerCodec.Schema&&d.OnlineContract==Contract&&d.Revision==0,"descriptor fields");
                Check(d.NativeLength==8&&d.ExtensionLength==NativeIdentityLedgerCodec.Encode(Empty(Contract),0).Length,"section lengths");
                var native=d.CopyNativeSection();var extension=d.CopyExtension();native[4]^=1;extension[0]^=1;frame[^1]^=1;
                Check(d.CopyNativeSection().SequenceEqual(NativeBytes())&&d.CopyExtension().SequenceEqual(NativeIdentityLedgerCodec.Encode(Empty(Contract),0)),"descriptor must own bytes and return copies");
            });
            Test("ONLINE-002-wrong-or-empty-epoch-rejected",()=>{
                Reject(()=>OnlineSnapshotDescriptor.Validate(Frame(),Foreign,Contract),"foreign frame");
                Reject(()=>OnlineSnapshotDescriptor.Validate(Frame(),Guid.Empty,Contract),"unassigned match");
            });
            Test("ONLINE-003-contract-mismatch-and-local-frame-rejected-online",()=>{
                Reject(()=>OnlineSnapshotDescriptor.Validate(Frame(),Epoch,new string('b',64)),"different same-mod contract");
                Reject(()=>OnlineSnapshotDescriptor.Validate(Frame(Empty()),Epoch,Contract),"local ledger cannot be admitted online");
                Check(OnlineSnapshotDescriptor.Validate(Frame(Empty()),Epoch).OnlineContract is null,"local structural inspection remains explicit");
            });
            Test("ONLINE-004-noncanonical-contract-rejected",()=>{
                foreach(var bad in new[]{"",new string('A',64),new string('a',63),new string('a',65),new string('g',64)})
                    Reject(()=>OnlineSnapshotDescriptor.Validate(Frame(),Epoch,bad),"malformed contract");
            });
            Test("ONLINE-005-every-truncation-and-trailing-byte-rejected",()=>{
                var frame=Frame();for(int n=0;n<frame.Length;n++)Reject(()=>OnlineSnapshotDescriptor.Validate(frame.Take(n).ToArray(),Epoch,Contract),"truncated frame "+n);
                Reject(()=>OnlineSnapshotDescriptor.Validate(frame.Concat(new byte[]{0}).ToArray(),Epoch,Contract),"trailing frame byte");
            });
            Test("ONLINE-006-header-payload-and-checksum-corruption-rejected",()=>{
                foreach(int p in new[]{0,4,6,8,12,20,24,28,60,Frame().Length-1})Reject(()=>OnlineSnapshotDescriptor.Validate(ChangedFrame(p),Epoch,Contract),"corruption "+p);
            });
            Test("ONLINE-007-signed-invalid-ledger-and-native-version-rejected",()=>{
                var invalid=NativeIdentityLedgerCodec.Encode(Empty(Contract),0);invalid[4]=255;invalid[5]=255;
                Reject(()=>OnlineSnapshotDescriptor.Validate(NativeStateFrame.Encode(NativeBytes(),invalid,0,123),Epoch,Contract),"valid checksum cannot authorize unknown ledger schema");
                invalid=NativeIdentityLedgerCodec.Encode(Empty(Contract),0);invalid[6]=1;
                Reject(()=>OnlineSnapshotDescriptor.Validate(NativeStateFrame.Encode(NativeBytes(),invalid,0,123),Epoch,Contract),"valid checksum cannot authorize ledger flags");
                Reject(()=>NativeStateFrame.Encode(new byte[]{122,0,0,0},invalid,0,123),"wrong embedded native version");
            });
            Test("ONLINE-008-inner-frame-boundary-is-explicit",()=>{
                var inner=Frame();var outer=new byte[inner.Length+4];BinaryPrimitives.WriteInt32LittleEndian(outer,123);inner.CopyTo(outer,4);
                Reject(()=>OnlineSnapshotDescriptor.Validate(outer,Epoch,Contract),"descriptor must not guess or strip outer serializer prefix");
                Check(OnlineSnapshotDescriptor.Validate(outer.AsSpan(4).ToArray(),Epoch,Contract).Epoch==Epoch,"adapter-selected inner section accepted");
            });
            Test("ONLINE-009-legacy-local-frame-not-online-admission",()=>{
                var legacy=NativeIdentityLedgerCodec.EncodeLegacyFingerprint(Empty(),0);
                var frame=NativeStateFrame.Encode(NativeBytes(),legacy,0,123);
                Check(OnlineSnapshotDescriptor.Validate(frame,Epoch).LedgerSchema==1,"legacy schema remains explicit");
                Reject(()=>OnlineSnapshotDescriptor.Validate(frame,Epoch,Contract),"legacy missing contract is not online");
            });
            Test("ONLINE-010-native-body-validation-remains-adapter-owned",()=>{
                // The inner native marker is structurally valid but no genuine
                // GameState was deserialized: do not claim this grants gameplay.
                Check(OnlineSnapshotDescriptor.Validate(Frame(),Epoch,Contract).NativeLength==8,"descriptor is deliberately structural");
            });
        }
        static void ScopeCases()
        {
            Test("ONLINE-011-scope-exact-match-state-and-thread",()=>{
                var token=new object();using var scope=new OnlineAuthorityScope(Epoch,token);
                scope.Require(Epoch,token);Check(!scope.IsClosed&&scope.Epoch==Epoch&&scope.OwnerThread==Environment.CurrentManagedThreadId,"exact scope");
                Reject(()=>scope.Require(Foreign,token),"foreign epoch");Reject(()=>scope.Require(Epoch,new object()),"different state token");
                Reject(()=>scope.Require(Epoch,null!),"missing token");scope.Require(Epoch,token);Check(!scope.IsClosed,"failed query does not revoke valid owner's scope");
            });
            Test("ONLINE-012-closed-scope-cannot-revive",()=>{
                var token=new object();var scope=new OnlineAuthorityScope(Epoch,token);scope.Dispose();scope.Dispose();
                Check(scope.IsClosed,"closed");Reject(()=>scope.Require(Epoch,token),"disposed authority");
                using var next=new OnlineAuthorityScope(Epoch,token);next.Require(Epoch,token);Reject(()=>scope.Require(Epoch,token),"new generation cannot revive old scope");
            });
            Test("ONLINE-013-wrong-thread-require-and-close-rejected",()=>{
                var token=new object();using var scope=new OnlineAuthorityScope(Epoch,token);
                var result=Task.Run(()=>{
                    int rejected=0;try{scope.Require(Epoch,token);}catch(InvalidDataException){rejected++;}
                    try{scope.Dispose();}catch(InvalidDataException){rejected++;}return(rejected,Environment.CurrentManagedThreadId);
                }).GetAwaiter().GetResult();
                Check(result.Item1==2&&result.Item2!=scope.OwnerThread&&!scope.IsClosed,"foreign thread cannot use or revoke authority");scope.Require(Epoch,token);
            });
            Test("ONLINE-014-exception-cleanup-and-independent-scopes",()=>{
                var token=new object();var outer=new OnlineAuthorityScope(Epoch,token);OnlineAuthorityScope? inner=null;
                try{using(inner=new OnlineAuthorityScope(Epoch,new object()))throw new IOException("fixture interruption");}catch(IOException){}
                Check(inner!.IsClosed,"using closes on failure");outer.Require(Epoch,token);Check(!outer.IsClosed,"closing inner cannot close outer");outer.Dispose();
            });
            Test("ONLINE-015-invalid-scope-construction",()=>{
                Reject(()=>new OnlineAuthorityScope(Guid.Empty,new object()),"empty epoch");Reject(()=>new OnlineAuthorityScope(Epoch,null!),"null state");
            });
        }
        public static int Run()
        {
            try{DescriptorCases();ScopeCases();PeerCases();Console.WriteLine(JsonSerializer.Serialize(new{status="PASS",cases=Cases,assertions=Assertions,tests=Names,nativeGame="NOT RUN",backend="NOT RUN"}));return 0;}
            catch(Exception e){Console.WriteLine(e);Console.WriteLine(JsonSerializer.Serialize(new{status="FAIL",cases=Cases,assertions=Assertions,tests=Names,error=e.ToString()}));return 1;}
        }
    }
}
