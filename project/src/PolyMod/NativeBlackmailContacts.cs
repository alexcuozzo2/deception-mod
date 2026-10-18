using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Prepare ordinary native first-contact actions. Their dispatcher owns
    // contact persistence, meeting rewards, capital discovery and reactions.
    // Nothing here executes an action or invents a reward amount.
    internal static class NativeBlackmailContacts
    {
        const int MaxActions=16384;
        static void Require(bool yes,string why){if(!yes)throw new InvalidDataException(why);}
        static object[] Items(object stack)
        {
            int n=Native.Int(Native.Get(stack,"Count"));Require(n>=0&&n<=MaxActions,"Blackmail contact queue exceeds its bound.");
            var items=Native.Items(stack).ToArray();Require(items.Length==n,"Blackmail contact queue contains a null action.");return items;
        }
        internal static Pending? Prepare(object state,byte victim,LedgerBlackmail[] rows)
        {
            var targets=rows.Where(r=>r.Victim==victim&&r.CreatedTurn<=Convert.ToUInt32(Native.Get(state,"CurrentTurn")))
                .OrderBy(r=>r.X).ThenBy(r=>r.Y).ThenBy(r=>r.Blackmailer).GroupBy(r=>r.Blackmailer).Select(g=>g.First()).ToArray();
            if(targets.Length==0)return null;
            object stack=Native.Get(state,"ActionStack")!;var prefix=Items(stack);var tail=new List<object>();
            var queued=new HashSet<byte>();object meet=Enum.Parse(Native.T("ActionType"),"Meet");
            foreach(var item in prefix)
                if(Equals(Native.Invoke(Native.M("ActionBase","GetActionType"),item),meet))
                {
                    var action=Native.As(item,"MeetAction");
                    if(Convert.ToByte(Native.Get(action,"PlayerId"))==victim)queued.Add(Convert.ToByte(Native.Get(action,"OtherPlayerId")));
                }
            foreach(var row in targets)
            {
                if(queued.Contains(row.Blackmailer))continue;
                object at=Activator.CreateInstance(Native.T("WorldCoordinates"),new object[]{row.X,row.Y})!;
                object action=Activator.CreateInstance(Native.T("MeetAction"),new object[]{victim,row.Blackmailer,at})!;
                if((bool)Native.Invoke(Native.M("MeetAction","IsValid","GameState"),action,state)!)tail.Add(action);
            }
            return tail.Count==0?null:new Pending(state,stack,prefix,tail.ToArray());
        }
        internal sealed class Pending
        {
            readonly object State,Map,Original,Staged;
            readonly object[] Prefix,Tail;
            readonly byte Player;
            readonly uint Turn;
            bool Attached,Accepted;
            internal Pending(object state,object original,object[] prefix,object[] tail)
            {
                State=state;Map=Native.Get(state,"Map")!;Player=Convert.ToByte(Native.Get(state,"CurrentPlayer"));Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));
                Original=original;Prefix=prefix;Tail=tail;
                Require(prefix.Length+tail.Length<=MaxActions,"Blackmail contacts would overflow the native queue.");
                Staged=Native.NewList("ActionBase",prefix.Concat(tail));
                Require(!Native.Same(Staged,Original),"Blackmail contact stage aliases its source queue.");Assert(false);
            }
            void SameItems(object stack,object[] expected)
            {
                var items=Items(stack);Require(items.Length==expected.Length,"Blackmail contact queue length changed.");
                for(int i=0;i<items.Length;i++)Require(Native.Same(items[i],expected[i]),"Blackmail contact queue identity/order changed.");
            }
            void Assert(bool attached)
            {
                Require(!Accepted&&NativeRegroupSession.Ready(State)&&Native.Same(Map,Native.Get(State,"Map"))&&
                    Player==Convert.ToByte(Native.Get(State,"CurrentPlayer"))&&Turn==Convert.ToUInt32(Native.Get(State,"CurrentTurn")),"Blackmail contact turn changed.");
                Require(Native.Same(Native.Get(State,"ActionStack"),attached?Staged:Original),"Blackmail contact queue was replaced.");
                SameItems(Original,Prefix);SameItems(Staged,Prefix.Concat(Tail).ToArray());
            }
            internal void Attach()
            {
                Require(!Attached,"Blackmail contacts attached twice.");Assert(false);
                // Mark before setter: even a setter that mutates then throws
                // must restore the exact original queue during rollback.
                Attached=true;Native.Set(State,"ActionStack",Staged);Assert(true);
            }
            internal void Accept(){Assert(Attached);Require(Attached,"Unattached Blackmail contacts cannot commit.");Accepted=true;}
            internal void Rollback()
            {
                Require(!Accepted,"Committed Blackmail contacts cannot roll back.");if(!Attached)return;
                var now=Native.Get(State,"ActionStack");Require(Native.Same(now,Original)||Native.Same(now,Staged),"Foreign queue replaced Blackmail contact stage.");
                Native.Set(State,"ActionStack",Original);Require(Native.Same(Native.Get(State,"ActionStack"),Original),"Blackmail contact rollback failed.");
                SameItems(Original,Prefix);Attached=false;
            }
        }
    }
}
