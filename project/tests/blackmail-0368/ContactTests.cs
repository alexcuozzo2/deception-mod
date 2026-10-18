using System;
using System.Linq;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests
{
 public static partial class BlackmailTests
 {
  static void ContactTests()
  {
   Test("CONTACT-001-native-meeting-queued-on-victim-turn-only",()=>{
    var s=Make();P(s,2).Known.Clear();Buy(s);Check(s.ActionStack.Count==0&&P(s,2).Known.Count==0,"setup defers native first contact");
    Marker(s);Check(s.ActionStack.Count==0,"no contact on blackmailer turn");s.CurrentPlayer=2;Marker(s);
    var meet=(MeetAction)s.ActionStack.Single();Check(meet.PlayerId==2&&meet.OtherPlayerId==1&&meet.Coordinates.X==2&&meet.Coordinates.Y==3,"genuine action actor/other/city");
    Check(P(s,2).Currency==19&&P(s,2).Known.Count==0,"staging did not execute meeting or award stars");
    s.ActionStack.Remove(meet);meet.Execute(s);var reward=(IncreaseCurrencyAction)s.ActionStack.Single();Check(reward.PlayerId==2,"native meeting owns its reward action");
    reward.Execute(s);s.ActionStack.Remove(reward);Marker(s);Check(s.ActionStack.Count==0&&P(s,2).Currency==26,"meeting rewarded once; duplicate marker adds no reward or payment");
   });
   Test("CONTACT-002-multiple-cities-one-contact-per-tribe",()=>{
    var s=Make();P(s,2).Known.Clear();Rows(s,new LedgerBlackmail(1,2,2,3,4),new(3,2,2,3,4),new(1,2,3,2,4));s.CurrentPlayer=2;Marker(s);
    Check(s.ActionStack.OfType<MeetAction>().Select(a=>a.OtherPlayerId).SequenceEqual(new byte[]{1,3}),"one contact per unique blackmailer in canonical city order");
    Marker(s);Check(s.ActionStack.Count==2,"already queued native contacts suppress duplicates");
   });
   Test("CONTACT-003-existing-native-meeting-prefix-preserved",()=>{
    var s=Make();P(s,2).Known.Clear();Rows(s,new LedgerBlackmail(1,2,2,3,4),new(3,2,3,2,4));s.CurrentPlayer=2;
    var native=new MeetAction(2,1,new(7,8));var other=new CaptureCityAction();s.ActionStack.Add(native);s.ActionStack.Add(other);var old=s.ActionStack;Marker(s);
    Check(!ReferenceEquals(old,s.ActionStack)&&ReferenceEquals(s.ActionStack[0],native)&&ReferenceEquals(s.ActionStack[1],other),"preserved prefix identity/order");
    Check(s.ActionStack.OfType<MeetAction>().Count()==2&&((MeetAction)s.ActionStack[^1]).OtherPlayerId==3,"only absent relationship appended");
   });
   Test("CONTACT-004-known-tribes-never-get-extra-reward",()=>{
    var s=Make();Buy(s);s.CurrentPlayer=2;var old=s.ActionStack;Marker(s);Check(ReferenceEquals(old,s.ActionStack)&&s.ActionStack.Count==0,"already met no queue replacement");
   });
   Test("CONTACT-005-meet-queued-for-another-player-does-not-suppress-victim",()=>{
    var s=Make();P(s,2).Known.Clear();Rows(s,new LedgerBlackmail(1,2,2,3,4));s.CurrentPlayer=2;s.ActionStack.Add(new MeetAction(3,1,new(2,3)));Marker(s);
    Check(s.ActionStack.OfType<MeetAction>().Select(a=>a.PlayerId).SequenceEqual(new byte[]{3,2}),"same other tribe distinct meeting actor");
   });
   foreach(string failure in new[]{"contact-create","contact-valid","contact-stage","contact-attach","ledger","currency2"})Test("CONTACT-006-atomic-failure-"+failure,()=>{
    var s=Make();P(s,2).Known.Clear();Rows(s,new LedgerBlackmail(1,2,2,3,4));s.CurrentPlayer=2;var scope=Start(s);var old=s.ActionStack;var before=Snap(s);Fault.At=failure;
    Check(NativeBlackmail.Finished(null,scope) is not null,"failure surfaced");
    Check(ReferenceEquals(s.ActionStack,old)&&s.ActionStack.Count==1&&ReferenceEquals(s.ActionStack[0],scope!.Action),"exact original queue restored");
    Check(s.Players.All(p=>p.Currency==20)&&Snap(s).Bytes().SequenceEqual(before.Bytes())&&P(s,2).Known.Count==0,"ledger, treasury and contact state unchanged");
   });
   Test("CONTACT-007-loaded-already-paid-row-still-establishes-first-contact",()=>{
    var s=Make();P(s,2).Known.Clear();Rows(s,new LedgerBlackmail(1,2,2,3,4,4));s.CurrentPlayer=2;Marker(s);
    Check(s.ActionStack.Single() is MeetAction&&s.Players.All(p=>p.Currency==20),"contact independent of already settled installment");
   });
   Test("CONTACT-008-captured-or-defeated-relationship-does-not-meet",()=>{
    var s=Make();P(s,2).Known.Clear();Rows(s,new LedgerBlackmail(1,2,2,3,4),new(3,2,3,2,4));City(s).owner=4;P(s,3).Alive=false;s.CurrentPlayer=2;Marker(s);
    Check(s.ActionStack.Count==0&&Read(s).Blackmails!.Length==0,"prune before contact planning");
   });
   Test("CONTACT-009-recreated-player-wrappers-share-native-contact-state",()=>{
    var s=Make();s.Rewrap=true;P(s,2).Known.Clear();Rows(s,new LedgerBlackmail(1,2,2,3,4));s.CurrentPlayer=2;Marker(s);var action=(MeetAction)s.ActionStack.Single();s.ActionStack.Remove(action);action.Execute(s);s.ActionStack.Clear();Marker(s);
    Check(s.ActionStack.Count==0&&P(s,2).Known.Contains(1),"native contact identity survives wrapper recreation");
   });
   Test("CONTACT-010-prepared-stage-rejects-changed-turn",()=>{
    var s=Make();P(s,2).Known.Clear();s.CurrentPlayer=2;var original=s.ActionStack;var pending=NativeBlackmailContacts.Prepare(s,2,new[]{new LedgerBlackmail(1,2,2,3,4)})!;s.CurrentTurn++;
    bool rejected=false;try{pending.Attach();}catch{rejected=true;}pending.Rollback();Check(rejected&&ReferenceEquals(s.ActionStack,original),"stale turn cannot publish contacts");
   });
   Test("CONTACT-011-prefix-mutation-is-not-overwritten",()=>{
    var s=Make();P(s,2).Known.Clear();s.CurrentPlayer=2;var pending=NativeBlackmailContacts.Prepare(s,2,new[]{new LedgerBlackmail(1,2,2,3,4)})!;var foreign=new CaptureCityAction();s.ActionStack.Add(foreign);
    bool rejected=false;try{pending.Attach();}catch{rejected=true;}pending.Rollback();Check(rejected&&ReferenceEquals(s.ActionStack.Single(),foreign),"foreign native queue update not lost");
   });
   Test("CONTACT-012-no-first-contact-for-future-row",()=>{
    var s=Make();P(s,2).Known.Clear();s.CurrentPlayer=2;Check(NativeBlackmailContacts.Prepare(s,2,new[]{new LedgerBlackmail(1,2,2,3,5)}) is null,"no premature event");
   });
  }
 }
}
