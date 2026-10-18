using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static partial class LighthouseSecretTests
 {
  static readonly Guid Epoch=new("fc50ef7c-dd37-4b59-a105-3da3a7e76445");
  static int Assertions;static readonly List<string> Cases=new();
  static void Check(bool yes,string why){Assertions++;if(!yes)throw new Exception(why);}
  static void Test(string name,Action body)
  {
   Fault.Reset();NativeRegroupSession.Enabled=true;NativeRegroupSession.Owned.Clear();NativeRegroupSession.Faults.Clear();NativeGameStateEnvelope.Values.Clear();NativeExchangeCommands.Executing=null;RecruitmentHooks.Registered=true;
   GameManager.GameState=null;GameManager.Client=null;ResourceEvents.Refreshed.Clear();ResourceEvents.Wallets.Clear();Entry.Notes.Clear();
   try{body();Check(NativeExchangeCommands.Executing is null,"fixture execution authority leaked");Cases.Add(name);}catch(Exception e){throw new Exception(name,e);}
  }
  static GameState Make()
  {
   var s=new GameState();s.Players.AddRange(new[]{new PlayerState{Id=1},new PlayerState{Id=2}});
   s.Map.Tiles.AddRange(new[]{new TileData{coordinates=new(0,0)},new TileData{coordinates=new(11,7)},new TileData{coordinates=new(5,5),Lighthouse=false}});
   var ledger=new NativeIdentityLedger(Epoch,2,1,new[]{new LedgerUnit(1,101,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,3)},Array.Empty<LedgerReceipt>());
   NativeGameStateEnvelope.Set(s,0,NativeIdentityLedgerCodec.Encode(ledger,0));NativeRegroupSession.Owned.Add(s);return s;
  }
  static PlayerState Player(GameState s,byte id=1)=>s.Players.Single(p=>p.Id==id);
  static TileData Tile(GameState s,int index=0)=>s.Map.Tiles[index];
  static NativeGameStateEnvelope.Snapshot Snapshot(GameState s)=>NativeGameStateEnvelope.Get(s)!;
  static NativeIdentityLedger Ledger(GameState s)=>NativeIdentityLedgerCodec.Decode(Snapshot(s).Bytes(),Snapshot(s).Revision);
  static void Write(GameState s,NativeIdentityLedger ledger)=>NativeGameStateEnvelope.Set(s,Snapshot(s).Revision,NativeIdentityLedgerCodec.Encode(ledger,Snapshot(s).Revision));
  // Reproduce an old recorded command without asking the removed feature to
  // create one. The retained decoder and receipt matcher must accept its bytes.
  static NativeExchangeIntent HistoricalIntent(GameState s,byte actor=1,int tile=0)
  {
   var snapshot=Snapshot(s);var ledger=Ledger(s);var at=Tile(s,tile).coordinates;byte[] before=snapshot.Bytes();
   using var stream=new MemoryStream();using var writer=new BinaryWriter(stream);
   writer.Write("DCP.LIGHTHOUSE.SECRET.1");writer.Write(before.Length);writer.Write(before);writer.Write(actor);writer.Write(at.X);writer.Write(at.Y);writer.Write(Player(s,actor).Currency);writer.Write(1000);writer.Flush();
   var target=new NativeExchangeUnit(0,0,at.X,at.Y);
   return new(ledger.Epoch,ledger.NextSequence,snapshot.Revision,s.CurrentTurn,actor,NativeExchangeKind.LighthouseSecret,false,NativePartnerReference.Existing,ledger.NextLogicalId,target,target,SHA256.HashData(stream.ToArray()));
  }
  static NativeExchangeIntent SeedHistorical(GameState s,byte actor=1,int tile=0)
  {
   var intent=HistoricalIntent(s,actor,tile);var ledger=Ledger(s);long after=Snapshot(s).Revision+1;
   var receipt=new LedgerReceipt(intent.Sequence,NativeExchangeIntentCodec.Operation(intent.Sequence),NativeExchangeIntentCodec.Fingerprint(intent),intent.Revision,after,Array.Empty<ulong>(),new LedgerCityReceipt(actor,intent.Initiator.X,intent.Initiator.Y));
   // This is a saved state from the former release, not a current reward call.
   Player(s,actor).Currency+=1000;
   NativeGameStateEnvelope.Set(s,after,NativeIdentityLedgerCodec.Encode(ledger with{NextSequence=ledger.NextSequence+1,Receipts=ledger.Receipts.Append(receipt).ToArray()},after));return intent;
  }
  static bool Commit(GameState s,NativeExchangeIntent intent,out NativeExchangeCommitResult? result,out string reason)
  {NativeExchangeCommands.Executing=s;try{return NativeLighthouseSecret.TryCommit(s,intent,out result,out reason);}finally{NativeExchangeCommands.Executing=null;}}
  static byte[] StateBytes(GameState s)=>JsonSerializer.SerializeToUtf8Bytes(new{turn=s.CurrentTurn,actor=s.CurrentPlayer,players=s.Players.Select(p=>new{p.Id,p.Currency,p.Alive}),ledger=Snapshot(s).Bytes(),revision=Snapshot(s).Revision,s.UnitMoved,s.UnitAttacked,s.CityExhausted,s.Actions,s.Sounds,s.Effects});
  static void Unchanged(GameState s,byte[] before,string why)=>Check(StateBytes(s).SequenceEqual(before),why);
  static GameState Reload(GameState original)
  {
   var copy=Make();copy.CurrentTurn=original.CurrentTurn;copy.CurrentPlayer=original.CurrentPlayer;
   foreach(var p in copy.Players){p.Currency=Player(original,p.Id).Currency;p.Alive=Player(original,p.Id).Alive;}
   for(int i=0;i<copy.Map.Tiles.Count;i++){Tile(copy,i).Lighthouse=Tile(original,i).Lighthouse;Tile(copy,i).Visible=new(Tile(original,i).Visible);}
   NativeGameStateEnvelope.Set(copy,Snapshot(original).Revision,Snapshot(original).Bytes());return copy;
  }
  public static int Run()
  {try{
   Test("LIGHTHOUSE-RETIRED-001-no-offer-even-after-repeated-requests",()=>{
    var s=Make();var before=StateBytes(s);int reads=0;Fault.Observe=_=>reads++;
    for(int n=0;n<20;n++)Check(!NativeLighthouseSecret.TryOffer(s,1,Tile(s),out var intent,out _)&&intent is null,"removed input must never create an intent");
    Check(!NativeLighthouseSecret.TryOffer(null!,0,null!,out var absent,out _)&&absent is null,"retired offer must be inert without native context");
    Fault.Reset();Check(reads==0,"retired offer touched native treasury or state");Unchanged(s,before,"repeated lighthouse requests changed game state");
   });
   foreach(byte actor in new byte[]{1,2})foreach(int tile in new[]{0,1})foreach(int currency in new[]{0,20,int.MaxValue})
   Test("LIGHTHOUSE-RETIRED-002-fresh-command-denied-"+actor+"-"+tile+"-"+currency,()=>{
    var s=Make();s.CurrentPlayer=actor;Player(s,actor).Currency=currency;var intent=HistoricalIntent(s,actor,tile);var before=StateBytes(s);var snapshot=Snapshot(s);int nativeReads=0;Fault.Observe=_=>nativeReads++;
    Check(!NativeLighthouseSecret.TryPlan(s,intent,out var plan,out _)&&plan is null,"fresh historical-format command received a new plan");
    Check(!Commit(s,intent,out var result,out _)&&result is null,"fresh historical-format command awarded stars");
    Fault.Reset();Check(nativeReads==0,"fresh rejection reached reward-dependent native reads or writes");
    Check(ReferenceEquals(snapshot,Snapshot(s)),"fresh rejection republished ledger");Unchanged(s,before,"fresh rejection changed authoritative state");
   });
   Test("LIGHTHOUSE-RETIRED-003-historical-wire-and-plan-read-only",()=>{
    var s=Make();var request=SeedHistorical(s);var before=StateBytes(s);var snapshot=Snapshot(s);byte[] wire=NativeExchangeIntentCodec.Encode(request);var decoded=NativeExchangeIntentCodec.Decode(wire);
    Check(wire.Length==136&&(byte)decoded.Kind==13&&wire.SequenceEqual(NativeExchangeIntentCodec.Encode(decoded)),"retired command format changed");
    Check(NativeLighthouseSecret.TryPlan(s,decoded,out var plan,out var why),why);
    Check(plan!.AlreadyCommitted&&plan.BeforeRevision==1&&plan.AfterRevision==1&&plan.StarCost==0&&plan.A is null&&plan.B is null&&plan.TileA is null,"historical receipt did not produce a pure duplicate plan");
    Check(plan.OriginalPayload.SequenceEqual(plan.CandidatePayload)&&ReferenceEquals(snapshot,Snapshot(s)),"duplicate planning published or invented a new ledger");Unchanged(s,before,"historical planning changed state");
   });
   Test("LIGHTHOUSE-RETIRED-004-duplicate-replay-never-awards",()=>{
    var s=Make();var request=SeedHistorical(s);var before=StateBytes(s);var snapshot=Snapshot(s);int nativeReads=0;Fault.Observe=_=>nativeReads++;
    for(int n=0;n<3;n++)Check(Commit(s,request,out var duplicate,out var why)&&duplicate!.AlreadyCommitted,why);
    Fault.Reset();Check(nativeReads==0,"duplicate commit touched treasury or publication boundary");
    Check(ReferenceEquals(snapshot,Snapshot(s)),"duplicate unnecessarily published a ledger");Unchanged(s,before,"duplicate awarded again");
   });
   Test("LIGHTHOUSE-RETIRED-005-reloaded-receipt-ignores-later-world-changes",()=>{
    var original=Make();var request=SeedHistorical(original);var s=Reload(original);s.CurrentTurn++;s.CurrentPlayer=2;Tile(s).Visible.Clear();Tile(s).Lighthouse=false;Player(s).Alive=false;Player(s).Currency=int.MaxValue;
    var before=StateBytes(s);Fault.At="get-currency";
    Check(Commit(s,NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(request)),out var replay,out var why)&&replay!.AlreadyCommitted,why);
    Check(Fault.At=="get-currency","historical replay consulted current treasury");Fault.Reset();Unchanged(s,before,"load/replay changed historical economy");
   });
   Test("LIGHTHOUSE-RETIRED-006-execution-authority-required-even-for-duplicate",()=>{
    var s=Make();var request=SeedHistorical(s);var before=StateBytes(s);
    Check(!NativeLighthouseSecret.TryCommit(s,request,out _,out _),"unscoped duplicate accepted");
    NativeExchangeCommands.Executing=Make();try{Check(!NativeLighthouseSecret.TryCommit(s,request,out _,out _),"foreign execution state accepted");}finally{NativeExchangeCommands.Executing=null;}
    Unchanged(s,before,"unauthorized duplicate changed state");
   });
   foreach(string field in new[]{"actor","coordinate","turn","digest","epoch","kind"})Test("LIGHTHOUSE-RETIRED-007-conflicting-receipt-"+field,()=>{
    var s=Make();var request=SeedHistorical(s);request=field switch{"actor"=>request with{Actor=2},"coordinate"=>request with{Initiator=new(0,0,11,7),Partner=new(0,0,11,7)},"turn"=>request with{Turn=99},"digest"=>request with{SourceAppearanceHash=new byte[32]},"epoch"=>request with{Epoch=Guid.NewGuid()},_=>request with{Kind=NativeExchangeKind.Blackmail}};
    var before=StateBytes(s);Check(!Commit(s,request,out _,out _),"conflicting intent borrowed old receipt");Unchanged(s,before,"conflict altered authoritative state");
   });
   foreach(string unavailable in new[]{"disabled","unowned","unregistered","ended","missing-ledger"})Test("LIGHTHOUSE-RETIRED-008-context-denial-"+unavailable,()=>{
    var s=Make();var request=SeedHistorical(s);
    switch(unavailable){case"disabled":NativeRegroupSession.Enabled=false;break;case"unowned":NativeRegroupSession.Owned.Clear();break;case"unregistered":RecruitmentHooks.Registered=false;break;case"ended":s.CurrentState=GameState.State.Ended;break;case"missing-ledger":NativeGameStateEnvelope.Values.Remove(s);break;}
    var snapshot=NativeGameStateEnvelope.Get(s);int balance=Player(s).Currency;
    Check(!Commit(s,request,out _,out _),"unavailable session accepted historical replay");
    Check(ReferenceEquals(snapshot,NativeGameStateEnvelope.Get(s))&&Player(s).Currency==balance,"unavailable session was mutated");
   });
   Test("LIGHTHOUSE-RETIRED-009-historical-balance-is-not-reversed",()=>{
    var host=Make();var first=SeedHistorical(host);var second=SeedHistorical(host,1,1);var peer=Reload(host);var before=StateBytes(peer);
    foreach(var old in new[]{first,second})Check(Commit(peer,NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(old)),out var duplicate,out var why)&&duplicate!.AlreadyCommitted,why);
    Check(Player(peer).Currency==2020&&Ledger(peer).Receipts.Length==2,"retirement repriced previously recorded income");Unchanged(peer,before,"replaying older receipts changed copied state");
    var fresh=HistoricalIntent(peer);Check(!Commit(peer,fresh,out _,out _),"later fresh sequence revived removed feature");Unchanged(peer,before,"denied later sequence changed state");
   });
   Test("LIGHTHOUSE-RETIRED-010-plan-retains-owned-byte-copies",()=>{
    var s=Make();var request=SeedHistorical(s);Check(NativeLighthouseSecret.TryPlan(s,request,out var plan,out var why),why);var before=StateBytes(s);
    byte[] candidate=plan!.CandidatePayload;candidate[^1]^=1;byte[] old=plan.OriginalPayload;old[0]^=1;request.SourceAppearanceHash[0]^=1;
    Check(plan.CandidatePayload.SequenceEqual(plan.OriginalPayload)&&plan.CandidateLedger.Receipts.Length==1,"caller mutated duplicate plan storage");
    Check(Commit(s,plan.Intent,out var duplicate,out why)&&duplicate!.AlreadyCommitted,why);Unchanged(s,before,"owned duplicate plan altered state");
   });
   foreach(string field in new[]{"unit-id","native-id","swap","allocate","different-endpoint","negative-x","too-large-y","actor-zero","actor-max","hash-length"})
   Test("LIGHTHOUSE-RETIRED-011-malformed-retired-command-"+field,()=>{
    var s=Make();var request=HistoricalIntent(s);request=field switch{
     "unit-id"=>request with{Initiator=new(1,0,0,0),Partner=new(1,0,0,0)},"native-id"=>request with{Initiator=new(0,101,0,0),Partner=new(0,101,0,0)},
     "swap"=>request with{Swap=true},"allocate"=>request with{PartnerReference=NativePartnerReference.Allocate},"different-endpoint"=>request with{Partner=new(0,0,11,7)},
     "negative-x"=>request with{Initiator=new(0,0,-1,0),Partner=new(0,0,-1,0)},"too-large-y"=>request with{Initiator=new(0,0,0,4096),Partner=new(0,0,0,4096)},
     "actor-zero"=>request with{Actor=0},"actor-max"=>request with{Actor=255},_=>request with{SourceAppearanceHash=new byte[31]}};
    var before=StateBytes(s);Check(!Commit(s,request,out _,out _),"malformed retired intent admitted");Unchanged(s,before,"malformed intent altered state");
   });
   PresentationTests();
   Console.WriteLine(JsonSerializer.Serialize(new{passed=true,cases=Cases.Count,assertions=Assertions,tests=Cases,scope="Retired lighthouse command rejection, historical receipt compatibility and silent completion through actual adapters with synthetic native ports; no game or network execution"}));return 0;
  }catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{passed=false,cases=Cases.Count,assertions=Assertions,error=e.ToString()}));return 1;}}
 }
}
