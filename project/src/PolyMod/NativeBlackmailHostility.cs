using System;
using System.IO;
using System.Linq;

namespace Deception.PolyModAdapter
{
    // The native hostile-action helper owns diplomacy timestamps and queues
    // ordinary bilateral embassy removal. Setup publication owns this stage;
    // no native action is executed here and no treaty is broken implicitly.
    internal static class NativeBlackmailHostility
    {
        const int MaxActions=16384;
        static void Require(bool value,string why){if(!value)throw new InvalidDataException(why);}
        static object Player(object state,byte id)
        {
            object?[] args={id,null};Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!,"Blackmail hostility player is absent.");return args[1]!;
        }
        static object[] Items(object stack)
        {
            int count=Native.Int(Native.Get(stack,"Count"));Require(count>=0&&count<=MaxActions,"Blackmail hostility queue exceeds its bound.");
            var items=Native.Items(stack).ToArray();Require(items.Length==count,"Blackmail hostility queue contains a null action.");return items;
        }
        internal static bool IsPeace(object state,byte a,byte b)
        {
            // HasPeaceWith calls GetRelation, which inserts missing keys.
            // Offer/read paths must not modify native diplomacy.
            bool Direction(byte from,byte to)
            {
                var dictionary=Native.Get(Player(state,from),"relations")!;
                if(!(bool)Native.Invoke(Native.M(dictionary.GetType(),"ContainsKey","System.Byte"),dictionary,to)!)return false;
                var relation=Native.Invoke(Native.M(dictionary.GetType(),"get_Item","System.Byte"),dictionary,to)!;
                return Equals(Native.Get(relation,"State"),Enum.Parse(Native.T("DiplomacyRelationState"),"Peace"));
            }
            return Direction(a,b)||Direction(b,a);
        }
        sealed class Relation
        {
            internal readonly object Player,Dictionary;
            internal readonly byte Other;
            internal readonly object? Original;
            internal readonly int Last,Previous;
            internal Relation(object player,byte other)
            {
                Player=player;Other=other;Dictionary=Native.Get(player,"relations")!;Original=Read();
                if(Original is not null){Last=Native.Int(Native.Get(Original,"LastAttackTurn"));Previous=Native.Int(Native.Get(Original,"PreviousAttackTurn"));}
            }
            internal object? Read()=> (bool)Native.Invoke(Native.M(Dictionary.GetType(),"ContainsKey","System.Byte"),Dictionary,Other)!?
                Native.Invoke(Native.M(Dictionary.GetType(),"get_Item","System.Byte"),Dictionary,Other):null;
            internal void AssertOriginal()
            {
                Require(Native.Same(Dictionary,Native.Get(Player,"relations")),"Blackmail diplomacy dictionary changed.");var now=Read();
                Require(Original is null?now is null:Native.Same(Original,now)&&Last==Native.Int(Native.Get(now!,"LastAttackTurn"))&&Previous==Native.Int(Native.Get(now!,"PreviousAttackTurn")),"Blackmail diplomacy changed before publication.");
            }
            internal void Restore()
            {
                Require(Native.Same(Dictionary,Native.Get(Player,"relations")),"Foreign diplomacy dictionary replaced Blackmail stage.");var now=Read();
                if(Original is null){if(now is not null)Require((bool)Native.Invoke(Native.M(Dictionary.GetType(),"Remove","System.Byte"),Dictionary,Other)!,"Blackmail new diplomacy entry was not removed.");}
                else {Require(Native.Same(Original,now),"Foreign relation replaced Blackmail stage.");Native.Set(Original,"LastAttackTurn",Last);Native.Set(Original,"PreviousAttackTurn",Previous);}
                AssertOriginal();
            }
        }
        internal static Pending Prepare(object state,byte actor,byte victim)=>new(state,actor,victim);
        internal sealed class Pending
        {
            readonly object State,Map,Original,Staged;
            readonly object[] Prefix;
            readonly Relation Actor,Victim;
            readonly byte ActorId,VictimId,CurrentPlayer;
            readonly uint Turn;
            bool Attached,Accepted;
            internal Pending(object state,byte actor,byte victim)
            {
                Require(actor is not (0 or 255)&&victim is not (0 or 255)&&actor!=victim,"Blackmail hostility requires two players.");
                State=state;Map=Native.Get(state,"Map")!;ActorId=actor;VictimId=victim;CurrentPlayer=Convert.ToByte(Native.Get(state,"CurrentPlayer"));Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));
                Require(CurrentPlayer==actor,"Blackmail hostility actor is not current.");Actor=new(Player(state,actor),victim);Victim=new(Player(state,victim),actor);
                Require(!IsPeace(state,actor,victim),"Blackmail cannot be established during peace.");
                Original=Native.Get(state,"ActionStack")!;Prefix=Items(Original);Require(Prefix.Length<=MaxActions-2,"Blackmail hostility would overflow the native queue.");
                Staged=Native.NewList("ActionBase",Prefix);Require(!Native.Same(Original,Staged),"Blackmail hostility queue aliases native source.");Assert(false);
            }
            void Context()
            {
                Require(!Accepted&&NativeRegroupSession.Ready(State)&&Native.Same(Map,Native.Get(State,"Map"))&&CurrentPlayer==Convert.ToByte(Native.Get(State,"CurrentPlayer"))&&Turn==Convert.ToUInt32(Native.Get(State,"CurrentTurn")),"Blackmail hostility context changed.");
                Require(Native.Same(Actor.Player,Player(State,ActorId))&&Native.Same(Victim.Player,Player(State,VictimId)),"Blackmail hostility player identity changed.");
                Require(!IsPeace(State,ActorId,VictimId),"Blackmail peace agreement changed.");
                Require(Native.Same(Actor.Dictionary,Native.Get(Actor.Player,"relations"))&&Native.Same(Victim.Dictionary,Native.Get(Victim.Player,"relations")),"Blackmail hostility diplomacy dictionary changed.");
            }
            static void SamePrefix(object stack,object[] expected,bool tail)
            {
                var items=Items(stack);Require(items.Length==expected.Length+(tail?2:0),"Blackmail hostility queue length changed.");
                for(int i=0;i<expected.Length;i++)Require(Native.Same(items[i],expected[i]),"Blackmail hostility queue order changed.");
            }
            void Assert(bool attached)
            {
                Context();Require(Native.Same(Native.Get(State,"ActionStack"),attached?Staged:Original),"Blackmail hostility queue was replaced.");SamePrefix(Original,Prefix,false);SamePrefix(Staged,Prefix,attached);
                if(!attached){Actor.AssertOriginal();Victim.AssertOriginal();return;}
                var a=Actor.Read();var b=Victim.Read();Require(a is not null&&b is not null,"Native hostility did not create both relations.");
                Require((Actor.Original is null||Native.Same(a,Actor.Original))&&(Victim.Original is null||Native.Same(b,Victim.Original)),"Native hostility replaced an existing relation.");
                Require(Native.Int(Native.Get(a!,"LastAttackTurn"))==Turn&&Native.Int(Native.Get(b!,"PreviousAttackTurn"))==Turn,"Native hostility did not record the attack.");
                if(Actor.Original is not null)Require(Native.Int(Native.Get(a!,"PreviousAttackTurn"))==Actor.Last,"Native previous attack timestamp differs.");
                var items=Items(Staged);var expected=new[]{(ActorId,VictimId),(VictimId,ActorId)};
                for(int i=0;i<2;i++)
                {
                    var child=items[Prefix.Length+i];Require(Native.Get(Native.As(child,"DestroyEmbassyAction"),"OpponentId") is object other&&Convert.ToByte(other)==expected[i].Item2&&Convert.ToByte(Native.Get(child,"PlayerId"))==expected[i].Item1,"Native hostile action queued an unexpected embassy target.");
                }
            }
            internal void Attach()
            {
                Require(!Attached,"Blackmail hostility attached twice.");Assert(false);Attached=true;Native.Set(State,"ActionStack",Staged);
                Native.Invoke(Native.M("PlayerDiplomacyExtensions","ModifyDiplomacyRelationsFromAttack","PlayerState","System.Byte","GameState"),null,Actor.Player,VictimId,State);Assert(true);
            }
            internal void Accept(){Require(Attached,"Blackmail hostility was not attached.");Assert(true);Accepted=true;}
            internal void Rollback()
            {
                Require(!Accepted,"Committed Blackmail hostility cannot roll back.");if(!Attached)return;
                Require(Native.Same(Native.Get(State,"ActionStack"),Original)||Native.Same(Native.Get(State,"ActionStack"),Staged),"Foreign queue replaced Blackmail hostility stage.");
                Actor.Restore();Victim.Restore();Native.Set(State,"ActionStack",Original);Require(Native.Same(Native.Get(State,"ActionStack"),Original),"Blackmail hostility rollback failed.");SamePrefix(Original,Prefix,false);Attached=false;
            }
        }
    }
}
