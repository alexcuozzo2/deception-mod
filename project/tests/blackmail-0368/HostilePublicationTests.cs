using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static partial class BlackmailTests
 {
  static void HostilePublicationTests()
  {
   Test("BLACKMAIL-H01-setup-native-hostility-and-bilateral-embassy-removal",()=>{
    var s=Make();var ar=PlayerDiplomacyExtensions.Get(P(s,1),2);var br=PlayerDiplomacyExtensions.Get(P(s,2),1);var unrelated=PlayerDiplomacyExtensions.Get(P(s,1),3);
    var request=Offer(s);Check(Commit(s,request,out _,out _),"commit");Check(P(s,1).Currency==18&&ar.LastAttackTurn==4&&br.PreviousAttackTurn==4,"cost and native hostility published");
    Check(ar.EmbassyLevel==2&&br.EmbassyLevel==2&&s.ActionStack.OfType<DestroyEmbassyAction>().Count()==2,"embassy work queued, not executed in transaction");DrainEmbassies(s);
    Check(ar.EmbassyLevel==0&&br.EmbassyLevel==0&&unrelated.EmbassyLevel==2,"only pair embassies removed");Check(Commit(s,request,out _,out _)&&s.ActionStack.Count==0,"retry does not repeat hostile action");
    ar.EmbassyLevel=2;s.CurrentTurn=5;Marker(s);Check(ar.EmbassyLevel==2&&P(s,1).Currency==19,"later embassy coexists with ongoing own-turn Blackmail income");
   });
   foreach(bool existing in new[]{false,true})foreach(string failure in new[]{"hostile-actor","hostile-victim","hostile-first-child","hostile-second-child","contact-attach","ledger","currency1"})Test("BLACKMAIL-H02-atomic-hostility-"+existing+"-"+failure,()=>{
    var s=Make();if(existing){PlayerDiplomacyExtensions.Get(P(s,1),2).LastAttackTurn=1;PlayerDiplomacyExtensions.Get(P(s,2),1).PreviousAttackTurn=2;}
    var request=Offer(s);var before=Snap(s);var queue=s.ActionStack;Fault.At=failure;Check(!Commit(s,request,out _,out _),"failed publication reported");
    Check(ReferenceEquals(queue,s.ActionStack)&&queue.Count==0&&P(s,1).Currency==20&&Snap(s).Bytes().SequenceEqual(before.Bytes()),"exact treasury/ledger/queue rollback");
    Check(P(s,1).relations.ContainsKey(2)==existing&&P(s,2).relations.ContainsKey(1)==existing,"membership restored");
    if(existing)Check(P(s,1).relations[2].LastAttackTurn==1&&P(s,2).relations[1].PreviousAttackTurn==2&&P(s,1).relations[2].EmbassyLevel==2,"timestamps and embassy restored");
   });
   Test("BLACKMAIL-H03-peace-offer-and-live-revalidation",()=>{
    var s=Make();var request=Offer(s);PlayerDiplomacyExtensions.Get(P(s,2),1).State=DiplomacyRelationState.Peace;
    Check(!NativeBlackmail.TryOffer(s,1,Target(s),out _,out _)&&!Commit(s,request,out _,out _)&&P(s,1).Currency==20,"peace blocks new and stale offers");
   });
   Test("BLACKMAIL-H04-accepted-peace-clears-both-directions-only",()=>{
    var s=Make();City(s,2).owner=1;Rows(s,new(1,2,2,3,4),new(3,2,3,2,4),new(2,1,4,4,4));
    var action=new PeaceRequestResponseAction{PlayerId=2,SenderId=1,Accepted=true};
    Cleanup(s,()=>{PlayerDiplomacyExtensions.Get(P(s,1),2).State=DiplomacyRelationState.Peace;PlayerDiplomacyExtensions.Get(P(s,2),1).State=DiplomacyRelationState.Peace;},action);
    Check(Read(s).Blackmails!.Single().Blackmailer==3,"accepted treaty removes both directions, preserves third tribe");
    PlayerDiplomacyExtensions.Get(P(s,1),2).State=DiplomacyRelationState.War;PlayerDiplomacyExtensions.Get(P(s,2),1).State=DiplomacyRelationState.War;s.CurrentTurn=5;Marker(s);
    Check(P(s,1).Currency==20&&Read(s).Blackmails!.Length==1,"ending peace does not revive removed Blackmail");
   });
   Test("BLACKMAIL-H05-rejected-peace-and-embassy-do-not-clear",()=>{
    var s=Make();Buy(s);var before=Snap(s);Cleanup(s,()=>{},new PeaceRequestResponseAction{PlayerId=2,SenderId=1,Accepted=false});
    Check(ReferenceEquals(before,Snap(s))&&Read(s).Blackmails!.Length==1,"declined request retains rows");PlayerDiplomacyExtensions.Get(P(s,1),2).EmbassyLevel=2;
    Check(NativeBlackmail.All(s).Length==1,"embassy alone keeps Blackmail");
   });
  }
 }
}
