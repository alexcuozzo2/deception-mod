using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using FixtureState=Deception.PolyModAdapter.State;
using Tile=Deception.PolyModAdapter.Tile;

namespace Deception.Tests
{
    public static partial class OnlineProtocolTests
    {
        sealed class Peer
        {
            internal FixtureState State;
            internal readonly object Token=new();
            internal int Count;
            internal Peer(FixtureState state,int count=0){State=state;Count=count;}
            internal string Receive(int wireIndex,byte[] bytes)
            {
                // Model only the preview's existing wire-index boundary, not a
                // new production scheduler or the ushort execution cursor.
                if(wireIndex<0)throw new InvalidDataException("Negative wire command index.");
                if(wireIndex<Count)return "Duplicate";
                if(wireIndex>Count)return "Gap";
                var request=NativeExchangeIntentCodec.Decode(bytes);
                using var authority=new OnlineAuthorityScope(Epoch,Token);authority.Require(request.Epoch,Token);
                if(!NativeExchangePlanning.TryPlan(State,request,out var plan,out var reason))throw new InvalidDataException(reason);
                if(!plan!.AlreadyCommitted)PublishFixturePlan(State,plan);
                Count++;return plan.AlreadyCommitted?"ReceiptDuplicate":"Applied";
            }
        }
        static FixtureState Setup()
        {
            RecruitmentHooks.Initialize();NativeRegroupSession.Production=true;Native.SpecificTerrain=false;
            var a=new Unit{id=3,type=RecruitmentCatalog.Variant("warrior"),health=10,coordinates=new(){X=3,Y=2},UnitData=RecruitmentHooks.Data(null!,RecruitmentCatalog.Variant("warrior"))};
            var b=new Unit{id=4,type="rider",health=70,coordinates=new(){X=4,Y=2},UnitData=RecruitmentHooks.Data(null!,"rider"),moved=true,attacked=true};
            var state=new FixtureState{CurrentUnitId=4,Map=new(){Tiles=new[]{new Tile{unit=a,coordinates=a.coordinates},new Tile{unit=b,coordinates=b.coordinates}}}};
            var ledger=NativeLedgerState.Bootstrap(state,Epoch) with{OnlineContract=Contract};state.Ledger=new(0,NativeIdentityLedgerCodec.Encode(ledger,0));return state;
        }
        static Unit Body(FixtureState state,uint id)=>state.Map.Tiles.Select(t=>t.unit).Single(u=>u?.id==id)!;
        static NativeExchangeIntent Offer(FixtureState state,NativeExchangeKind kind,bool swap)
        {
            var frame=state.Ledger!;var ledger=NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision);var a=Body(state,3);var b=Body(state,4);
            var ar=ledger.Units.Single(x=>x.NativeId==3);var br=ledger.Units.Single(x=>x.NativeId==4);
            var source=kind==NativeExchangeKind.Disguise?ar.Appearance!:NativeLedgerState.CapturePublic(state,new(b,true),b.type,br);
            NativeExchangeUnit Ref(Unit u,LedgerUnit r)=>new(r.LogicalId,u.id,u.coordinates.X,u.coordinates.Y);
            return new(Epoch,ledger.NextSequence,frame.Revision,state.CurrentTurn,state.CurrentPlayer,kind,swap,NativePartnerReference.Existing,ledger.NextLogicalId,
                Ref(a,ar),Ref(b,br),NativeExchangeIntentCodec.AppearanceFingerprint(source));
        }
        static void PublishFixturePlan(FixtureState state,NativeExchangePlan plan)
        {
            // The real read-only planner chooses the complete outcome. These
            // native-shaped scalar writes stand in for native commit and must
            // not be presented as native action/transaction execution coverage.
            var a=(Unit)plan.A!;var b=(Unit)plan.B!;var at=(Tile)plan.TileA!;var bt=(Tile)plan.TileB!;
            a.type=plan.FinalTypeA!;a.UnitData=RecruitmentHooks.Data(state,a.type);b.type=plan.FinalTypeB!;b.UnitData=RecruitmentHooks.Data(state,b.type);
            a.coordinates=new(){X=plan.FinalA!.Value.X,Y=plan.FinalA.Value.Y};b.coordinates=new(){X=plan.FinalB!.Value.X,Y=plan.FinalB.Value.Y};
            at.unit=plan.Intent.Swap?b:a;bt.unit=plan.Intent.Swap?a:b;a.moved=a.attacked=true;state.CurrentUnitId=plan.FinalCounter;
            NativeLedgerState.Validate(state,plan.AfterRevision,plan.CandidatePayload);state.Ledger=new(plan.AfterRevision,plan.CandidatePayload);
        }
        static byte[] NativeProjection(FixtureState state)
        {
            var data=JsonSerializer.SerializeToUtf8Bytes(new{state.CurrentTurn,state.CurrentPlayer,state.CurrentUnitId,units=state.Map.Tiles.Select(t=>t.unit!).OrderBy(u=>u.id).Select(u=>new{
                u.id,u.type,u.owner,u.health,u.moved,u.attacked,u.coordinates.X,u.coordinates.Y,effects=u.effects.ToArray()}).ToArray()});
            return new byte[]{123,0,0,0}.Concat(data).ToArray();
        }
        static byte[] Snapshot(Peer peer)=>NativeStateFrame.Encode(NativeProjection(peer.State),peer.State.Ledger!.Bytes(),peer.State.Ledger.Revision,123);
        static void Equal(Peer a,Peer b,string reason)
        {Check(a.Count==b.Count&&Snapshot(a).SequenceEqual(Snapshot(b)),reason);}
        static void NextTurn(Peer peer)
        {peer.State.CurrentTurn++;var a=Body(peer.State,3);a.moved=a.attacked=false;}
        static Peer Rejoin(Peer source,byte[] frame)
        {
            var d=OnlineSnapshotDescriptor.Validate(frame,Epoch,Contract);var original=source.State;
            var state=new FixtureState{CurrentTurn=original.CurrentTurn,CurrentPlayer=original.CurrentPlayer,CurrentUnitId=original.CurrentUnitId};
            state.Map.Tiles=original.Map.Tiles.Select(t=>{
                var u=t.unit!;var body=new Unit{id=u.id,type=u.type,owner=u.owner,health=u.health,moved=u.moved,attacked=u.attacked,coordinates=new(){X=u.coordinates.X,Y=u.coordinates.Y},UnitData=RecruitmentHooks.Data(state,u.type),effects=u.effects.ToList()};
                return new Tile{coordinates=new(){X=t.coordinates.X,Y=t.coordinates.Y},unit=body};
            }).ToArray();
            Check(NativeProjection(state).SequenceEqual(d.CopyNativeSection()),"fixture's separately reconstructed native section matches frame");
            state.Ledger=new(d.Revision,d.CopyExtension());NativeLedgerState.Validate(state,d.Revision,d.CopyExtension());return new(state,source.Count);
        }
        static void PeerCases()
        {
            Test("ONLINE-016-two-peers-real-planner-multiround-identical-frames",()=>{
                var a=new Peer(Setup());var b=new Peer(Setup());Equal(a,b,"independent initial peers");
                var rounds=new[]{(NativeExchangeKind.Regroup,false),(NativeExchangeKind.Disguise,true),(NativeExchangeKind.Regroup,true),(NativeExchangeKind.Disguise,false)};
                foreach(var (kind,swap) in rounds){var request=Offer(a.State,kind,swap);var wire=NativeExchangeIntentCodec.Encode(request);int index=a.Count;
                    Check(a.Receive(index,wire)=="Applied"&&b.Receive(index,wire)=="Applied","same actual planner intent on both peers");Equal(a,b,"post-plan bytes equal");
                    var before=Snapshot(b);Check(b.Receive(index,wire)=="Duplicate"&&Snapshot(b).SequenceEqual(before),"transport duplicate performs no writes");
                    var ledger=NativeIdentityLedgerCodec.Decode(b.State.Ledger!.Bytes(),b.State.Ledger.Revision);Check(NativeExchangeIntentCodec.AlreadyCommitted(ledger,b.State.Ledger.Revision,request),"real stored receipt acknowledges identical intent");
                    // Different writer cadence must not create feature revisions.
                    var reconciled=NativeLedgerState.Reconcile(b.State,b.State.Ledger);b.State.Ledger=new(reconciled.Revision,reconciled.Payload);Equal(a,b,"extra peer reconciliation is deterministic");
                    NextTurn(a);NextTurn(b);
                }
                Check(a.Count==4&&a.State.Ledger!.Revision==4,"four native indices and four feature receipts");
                Check(Body(a.State,4).moved&&Body(a.State,4).attacked&&Body(a.State,4).health==70,"spent injured partner unchanged");
            });
            Test("ONLINE-017-wire-gap-rejected-then-ordered-retry-applies",()=>{
                var a=new Peer(Setup());var b=new Peer(Setup());var wire=NativeExchangeIntentCodec.Encode(Offer(a.State,NativeExchangeKind.Regroup,false));var before=Snapshot(b);
                Check(b.Receive(1,wire)=="Gap"&&b.Count==0&&Snapshot(b).SequenceEqual(before),"gap cannot execute before missing command");
                Check(a.Receive(0,wire)=="Applied"&&b.Receive(0,wire)=="Applied","next native index accepted");Equal(a,b,"ordered retry equal");
                Reject(()=>b.Receive(-1,wire),"negative wire index");
            });
            Test("ONLINE-018-rejoin-framed-ledger-continues-sequence-and-retries",()=>{
                var host=new Peer(Setup());var first=NativeExchangeIntentCodec.Encode(Offer(host.State,NativeExchangeKind.Regroup,true));host.Receive(0,first);
                var peer=Rejoin(host,Snapshot(host));Equal(host,peer,"rejoin framed ledger and native projection");
                Check(!ReferenceEquals(Body(host.State,3),Body(peer.State,3)),"no peer shares live native-shaped unit");
                Check(peer.Receive(0,first)=="Duplicate","pre-reconnect native index stays old");
                NextTurn(host);NextTurn(peer);var next=NativeExchangeIntentCodec.Encode(Offer(host.State,NativeExchangeKind.Disguise,false));
                Check(host.Receive(1,next)=="Applied"&&peer.Receive(1,next)=="Applied","next feature sequence retained across rejoin");Equal(host,peer,"continued peer bytes");
            });
            Test("ONLINE-019-receipt-idempotence-after-new-turn-and-body-movement",()=>{
                var p=new Peer(Setup());var request=Offer(p.State,NativeExchangeKind.Regroup,true);var wire=NativeExchangeIntentCodec.Encode(request);p.Receive(0,wire);NextTurn(p);
                var before=Snapshot(p);Check(p.Receive(1,wire)=="ReceiptDuplicate","native next index carrying prior feature is a receipt no-op");
                Check(Snapshot(p).SequenceEqual(before)&&p.Count==2,"receipt does not refresh actions or republish a feature");
                var ledger=NativeIdentityLedgerCodec.Decode(p.State.Ledger!.Bytes(),p.State.Ledger.Revision);
                Reject(()=>NativeExchangeIntentCodec.AlreadyCommitted(ledger,p.State.Ledger.Revision,request with{Swap=!request.Swap}),"conflicting same sequence cannot reuse receipt");
            });
            Test("ONLINE-020-next-index-malformed-and-foreign-intent-no-publication",()=>{
                var p=new Peer(Setup());var request=Offer(p.State,NativeExchangeKind.Regroup,false);var wire=NativeExchangeIntentCodec.Encode(request);var before=Snapshot(p);
                Reject(()=>p.Receive(0,wire.Take(wire.Length-1).ToArray()),"malformed next command");
                Reject(()=>p.Receive(0,NativeExchangeIntentCodec.Encode(request with{Epoch=Foreign})),"wrong match command");
                Reject(()=>p.Receive(0,NativeExchangeIntentCodec.Encode(request with{Sequence=2})),"skipped feature sequence");
                Check(p.Count==0&&Snapshot(p).SequenceEqual(before),"all rejected commands leave native projection and ledger exact");
            });
            Test("ONLINE-021-native-authority-and-stale-digest-revalidated",()=>{
                var p=new Peer(Setup());var request=Offer(p.State,NativeExchangeKind.Regroup,false);var before=Snapshot(p);
                foreach(var bad in new[]{request with{Actor=2},request with{Turn=request.Turn+1},request with{Initiator=request.Initiator with{X=99}},request with{SourceAppearanceHash=new byte[32]}})
                    Reject(()=>p.Receive(0,NativeExchangeIntentCodec.Encode(bad)),"native planner rejects stale/foreign request");
                Check(p.Count==0&&Snapshot(p).SequenceEqual(before),"rejections have no native projection side effects");
                Body(p.State,3).attacked=true;Reject(()=>p.Receive(0,NativeExchangeIntentCodec.Encode(request)),"spent initiating body rejected");Check(p.Count==0,"failed native plan does not advance index");
            });
            Test("ONLINE-022-native-wire-index-is-not-ushort-execution-cursor",()=>{
                var p=new Peer(Setup(),70000);var wire=NativeExchangeIntentCodec.Encode(Offer(p.State,NativeExchangeKind.Regroup,false));
                Check(p.Receive(69999,wire)=="Duplicate"&&p.Receive(70001,wire)=="Gap"&&p.Receive(70000,wire)=="Applied"&&p.Count==70001,"wire ordering retains int CommandStack.Count width");
            });
            Test("ONLINE-023-specialist-kinds-preserve-complete-wire-choice",()=>{
                var p=new Peer(Setup());var basis=Offer(p.State,NativeExchangeKind.Regroup,false);
                foreach(var kind in new[]{NativeExchangeKind.Subvert,NativeExchangeKind.Blackout,NativeExchangeKind.Blackmail}){
                    var request=basis with{Kind=kind,SourceAppearanceHash=System.Security.Cryptography.SHA256.HashData(p.State.Ledger!.Bytes())};
                    if(kind==NativeExchangeKind.Blackout)request=request with{Partner=request.Initiator with{X=7,Y=8}};
                    if(kind==NativeExchangeKind.Blackmail)request=request with{Initiator=basis.Partner,Partner=basis.Partner};
                    var bytes=NativeExchangeIntentCodec.Encode(request);var received=NativeExchangeIntentCodec.Decode(bytes);
                    Check(bytes.SequenceEqual(NativeExchangeIntentCodec.Encode(received))&&received.Kind==kind&&received.Actor==basis.Actor&&received.Partner==request.Partner,"specialist codec identity and target preserved");
                    Check(NativeExchangeIntentCodec.Fingerprint(request).SequenceEqual(NativeExchangeIntentCodec.Fingerprint(received)),"specialist intent fingerprint stable");
                }
                // Historical city-target records remain decodable for exact
                // replay receipts; current offers use the positive unit above.
                var city=new NativeExchangeUnit(0,0,7,8);
                var legacy=basis with{Kind=NativeExchangeKind.Blackmail,Initiator=city,Partner=city,
                    SourceAppearanceHash=System.Security.Cryptography.SHA256.HashData(p.State.Ledger!.Bytes())};
                var legacyWire=NativeExchangeIntentCodec.Encode(legacy);
                var legacyRead=NativeExchangeIntentCodec.Decode(legacyWire);
                Check(legacyRead.Initiator==city&&legacyRead.Partner==city&&legacyRead.Kind==NativeExchangeKind.Blackmail&&
                    legacyWire.SequenceEqual(NativeExchangeIntentCodec.Encode(legacyRead)),"legacy city choice still roundtrips exactly");
                Check(NativeExchangeIntentCodec.Fingerprint(legacy).SequenceEqual(NativeExchangeIntentCodec.Fingerprint(legacyRead)),"legacy city intent fingerprint remains stable");
            });
        }
    }
}
