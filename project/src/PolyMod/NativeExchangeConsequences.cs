using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Called only by the authorized NativeExchangeCommit transaction. This
    // helper neither grants authority nor executes an action; it stages the
    // final roots' native sight actions for ordinary child processing.
    internal static class NativeExchangeConsequences
    {
        const int MaxPrefix=16384,MaxArea=25;
        static void Require(bool ok,string message)
        {if(!ok)throw new InvalidDataException(message);}
        static NativeExchangePosition Position(object coordinates)=>new(
            Native.Int(Native.Get(coordinates,"X")),Native.Int(Native.Get(coordinates,"Y")));
        static object[] Actions(object list)
        {
            int count=Native.Int(Native.Get(list,"Count"));
            Require(count is >=0 and <=MaxPrefix+2*MaxArea,"Exchange action queue exceeds its detached bound.");
            var result=Native.Items(list).ToArray();
            Require(result.Length==count,"Exchange action queue contains a null entry.");return result;
        }
        static object Player(object state,byte owner)
        {
            object?[] args={owner,null};
            Require(owner!=byte.MaxValue&&(bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!
                &&args[1] is not null,"Exchange sight owner is absent.");
            Require(Convert.ToByte(Native.Get(args[1]!,"Id"))==owner,"Exchange sight owner identity changed.");return args[1]!;
        }
        static bool Explored(object tile,byte owner)=>(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,owner)!;

        internal static Pending Capture(object state,NativeExchangePlan plan)=>new(state,plan);

        // Capture before the first native body write. Stage after all final body
        // and allowance writes; Accept only after ledger validation/publication.
        // Root rollback MUST call Rollback before comparing whole native bytes.
        internal sealed class Pending:IDisposable
        {
            readonly object State,Map,Owner,OriginalStack,StagedStack;
            readonly object[] Prefix;
            readonly bool OriginalDirty;
            readonly NativeExchangePlan Plan;
            readonly NativeExchangeIntent Request;
            readonly List<Expected> ExpectedTail=new();
            readonly NativeBlackoutDiscovery.Collection Discovery=new();
            readonly NativeRogueSight.Collection RogueSight=new();
            readonly Dictionary<long,Visibility> VisibilityBefore=new();
            bool Attached,Staged,Accepted,Disposed,RouteMarked;
            internal int SourceACount {get;private set;}
            internal int SourceBCount {get;private set;}
            internal int AddedCount=>ExpectedTail.Count;
            internal void MergeDiscovery()=>Plan.MergeDiscovery(RogueSight.Merge(Discovery.Merge(Plan.CandidateLedger)));
            sealed record Expected(NativeExchangePosition Position);
            sealed record Visibility(object Tile,bool Explored);
            sealed record Source(object Unit,object Tile,object Data,int Range,List<Expected> Expected);

            internal Pending(object state,NativeExchangePlan plan)
            {
                Require(Native.Same(state,plan.State)&&!plan.AlreadyCommitted&&plan.A is not null&&plan.B is not null&&
                    plan.FinalA.HasValue&&plan.FinalB.HasValue,"Exchange sight requires an uncommitted exact-state plan.");
                Require(Native.Int(Native.Get(state,"Version"))==123,"Exchange sight supports only native serializer123.");
                State=state;Plan=plan;Request=plan.Intent;Map=Native.Get(state,"Map")!;
                Require(Map is not null,"Exchange sight map is absent.");Owner=Player(state,Request.Actor);
                OriginalStack=Native.Get(state,"ActionStack")??throw new InvalidDataException("Exchange action queue is absent.");
                Prefix=Actions(OriginalStack);Require(Prefix.Length<=MaxPrefix,"Exchange action prefix exceeds its detached bound.");
                OriginalDirty=(bool)Native.Get(state,"HasFlaggedNeedForUpdateRoutes")!;
                StagedStack=Native.NewList("ActionBase",Prefix);
                Require(!Native.Same(OriginalStack,StagedStack),"Staged exchange queue aliases its original.");
                AssertPrefix(OriginalStack,false);AssertPrefix(StagedStack,false);
            }

            void AssertContext()
            {
                Require(!Disposed&&!Accepted&&Native.Same(Native.Get(State,"Map"),Map),"Exchange sight scope or map changed.");
                Require(Convert.ToByte(Native.Get(State,"CurrentPlayer"))==Request.Actor&&
                    Convert.ToUInt32(Native.Get(State,"CurrentTurn"))==Request.Turn&&Native.Same(Player(State,Request.Actor),Owner),
                    "Exchange sight commander or turn changed.");
                Require((bool)Native.Get(State,"HasFlaggedNeedForUpdateRoutes")! == (OriginalDirty||RouteMarked),
                    "The exchange route-dirty flag changed outside its transaction.");
                AssertPrefix(OriginalStack,false);
            }
            void AssertPrefix(object list,bool allowTail)
            {
                var items=Actions(list);Require(allowTail?items.Length>=Prefix.Length:items.Length==Prefix.Length,
                    "Exchange changed the preexisting action count.");
                for(int i=0;i<Prefix.Length;i++)Require(Native.Same(items[i],Prefix[i]),"Exchange changed a preexisting action or its order.");
            }
            Source FinalSource(object unit,NativeExchangeUnit identity,NativeExchangePosition position)
            {
                Require(Convert.ToUInt32(Native.Get(unit,"id"))==identity.NativeId&&
                    Convert.ToByte(Native.Get(unit,"owner"))==Request.Actor&&Native.Int(Native.Get(unit,"health"))>0,
                    "Final exchange sight body identity or owner changed.");
                object coordinates=Native.Get(unit,"coordinates")!;
                Require(Position(coordinates)==position,"Exchange sight source has not reached its committed coordinate.");
                object tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Map,coordinates)
                    ??throw new InvalidDataException("Final exchange sight tile is missing.");
                Require(Position(Native.Get(tile,"coordinates")!)==position&&Native.Same(Native.Get(tile,"unit"),unit)&&Explored(tile,Request.Actor),
                    "Final exchange sight source does not occupy its exact accessible tile.");
                object data=Native.Get(unit,"UnitData")??throw new InvalidDataException("Final actual sight profile is missing.");
                Require(Equals(Native.Get(data,"type"),Native.Get(unit,"type")),"Final exchange body and actual sight profile disagree.");
                int range=(int)Native.Invoke(Native.M("UnitDataExtensions","GetSightRange","Polytopia.Data.UnitData"),null,data)!;
                // The accepted Warrior/Rider slice has no Scout. The engine
                // applies Mountain's radius2 override inside ExploreFromTile.
                Require(range>=1&&range<=2&&(NativeRegroupSession.Enabled||range==1),"Native sight profile is outside the supported range.");
                int radius=Native.Int(Native.Get(tile,"terrain"))==4?2:range;
                object area=Native.Invoke(Native.M("MapDataExtensions","GetAreaSorted","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),
                    null,Map,Native.Get(tile,"coordinates"),radius,true,true)!;
                int count=Native.Int(Native.Get(area,"Length"));Require(count is >0 and <=MaxArea,"Native sight footprint exceeds its audited bound.");
                var item=Native.M(area.GetType(),"get_Item","System.Int32");var expected=new List<Expected>();
                // Match the audited reverse iteration. Do not deduplicate A/B
                // overlap: native IsValid skips already-explored work later.
                for(int i=count-1;i>=0;i--)
                {
                    object target=Native.Invoke(item,area,i)??throw new InvalidDataException("Native sight footprint contains a null tile.");
                    object targetCoordinates=Native.Get(target,"coordinates")!;
                    Require(Native.Same(target,Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Map,targetCoordinates)),
                        "Native sight footprint contains a foreign tile.");
                    bool explored=Explored(target,Request.Actor);long pointer=Native.Pointer(target);
                    if(VisibilityBefore.TryGetValue(pointer,out var before))Require(before.Explored==explored,"Exploration changed during sight staging.");
                    else VisibilityBefore.Add(pointer,new(target,explored));
                    if(!explored)expected.Add(new(Position(targetCoordinates)));
                }
                return new(unit,tile,data,range,expected);
            }

            internal void StageFinalSources(Action<string>? faultAfter=null)
            {
                Require(!Attached&&!Staged,"Exchange sight was staged more than once.");AssertContext();
                Require(Native.Same(Native.Get(State,"ActionStack"),OriginalStack),"Exchange action queue changed before staging.");
                var a=FinalSource(Plan.CommittedA!,Request.Initiator with{NativeId=Plan.FinalNativeIdA},Plan.FinalA!.Value);
                var b=FinalSource(Plan.CommittedB!,Request.Partner with{NativeId=Plan.FinalNativeIdB},Plan.FinalB!.Value);
                // Mark before the setter: rollback remains applicable if a
                // native setter changes its field and then reports failure.
                Attached=true;Native.Set(State,"ActionStack",StagedStack);
                Append(a);SourceACount=a.Expected.Count;faultAfter?.Invoke("sight-a");
                Append(b);SourceBCount=b.Expected.Count;faultAfter?.Invoke("sight-b");
                if(NativeRegroupSession.Enabled)
                {RouteMarked=true;Native.Set(State,"HasFlaggedNeedForUpdateRoutes",true);}
                Staged=true;AssertPending();
            }
            void Append(Source source)
            {
                Require(Native.Same(Native.Get(State,"ActionStack"),StagedStack),"Exchange lost its staged action queue.");
                using(NativeRogueSight.ExchangeSource(State,Request.Actor,source.Tile,RogueSight,Plan.CandidateLedger.Units.SingleOrDefault(r=>r.Alive&&r.NativeId==Convert.ToUInt32(Native.Get(source.Unit,"id")))))
                using(NativeBlackoutDiscovery.ExchangeSource(State,Request.Actor,source.Tile,Discovery))
                    Native.Invoke(Native.M("ActionUtils","ExploreFromTile","GameState","PlayerState","TileData","System.Int32","System.Boolean"),
                        null,State,Owner,source.Tile,source.Range,true);
                ExpectedTail.AddRange(source.Expected);AssertPending();
            }
            internal void AssertPending()
            {
                Require(Attached,"Exchange sight queue is not attached.");AssertContext();
                Require(Native.Same(Native.Get(State,"ActionStack"),StagedStack),"Exchange lost its staged action queue.");
                AssertPrefix(StagedStack,true);var actions=Actions(StagedStack);
                Require(actions.Length==Prefix.Length+ExpectedTail.Count,"Native sight appended an unexpected action count.");
                var allocated=new HashSet<long>(Prefix.Select(Native.Pointer));
                for(int i=0;i<ExpectedTail.Count;i++)
                {
                    var action=actions[Prefix.Length+i];
                    Require(allocated.Add(Native.Pointer(action)),"Native sight reused an existing action object.");
                    // Native.As performs the IL2CPP runtime type check even
                    // when List<ActionBase> exposes only a base wrapper.
                    var explore=Native.As(action,"ExploreAction");
                    Require(Convert.ToByte(Native.Get(explore,"PlayerId"))==Request.Actor&&
                        Position(Native.Get(explore,"Coordinates")!)==ExpectedTail[i].Position,
                        "Native sight appended an unauthorized owner, coordinate, type, or order.");
                }
                foreach(var before in VisibilityBefore.Values)
                    Require(Explored(before.Tile,Request.Actor)==before.Explored,"Sight staging executed exploration before receipt publication.");
            }
            internal void Accept()
            {
                Require(Staged,"Incomplete exchange sight cannot be accepted.");AssertPending();
                var binding=NativeGameStateEnvelope.Get(State);
                Require(binding is not null&&binding.Revision==Plan.AfterRevision&&binding.Bytes().SequenceEqual(Plan.CandidatePayload),
                    "Exchange sight requires the exact committed receipt and ledger.");
                Accepted=true;
            }
            internal void Rollback()
            {
                Require(!Accepted,"Published exchange sight cannot be rolled back by an endpoint helper.");
                if(Disposed)return;
                if(Attached)
                {
                    var current=Native.Get(State,"ActionStack");
                    Require(Native.Same(current,StagedStack)||Native.Same(current,OriginalStack),
                        "A foreign action queue replaced the exchange stage; discard the private state.");
                    // No in-place Clear/Remove of the original action prefix.
                    Native.Set(State,"ActionStack",OriginalStack);
                    Native.Set(State,"HasFlaggedNeedForUpdateRoutes",OriginalDirty);RouteMarked=false;
                    Require(Native.Same(Native.Get(State,"ActionStack"),OriginalStack)&&
                        (bool)Native.Get(State,"HasFlaggedNeedForUpdateRoutes")! == OriginalDirty,"Exchange queue rollback failed.");
                    AssertPrefix(OriginalStack,false);Attached=false;
                }
                Disposed=true;
            }
            public void Dispose(){if(Accepted){Disposed=true;return;}Rollback();}
        }
    }
}
