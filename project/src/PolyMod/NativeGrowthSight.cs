using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Deception.PolyModAdapter
{
    // Called inside the authenticated growth transaction after carrier writes.
    // Queue ordinary native exploration; never execute children or write fog.
    internal static class NativeGrowthSight
    {
        const int MaxPrefix=16384,MaxSources=4096,MaxArea=25;
        readonly record struct Position(int X,int Y);
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        static Position At(object coordinates)=>new(Native.Int(Native.Get(coordinates,"X")),Native.Int(Native.Get(coordinates,"Y")));
        static bool Explored(object tile,byte owner)=>(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,owner)!;
        static object Player(object state,byte owner)
        {
            object?[] args={owner,null};
            Require(owner!=byte.MaxValue&&(bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!
                &&args[1] is not null&&Convert.ToByte(Native.Get(args[1]!,"Id"))==owner,"Growing sight owner is absent or mismatched.");
            return args[1]!;
        }
        static object[] Actions(object list,int bound)
        {
            int count=Native.Int(Native.Get(list,"Count"));Require(count>=0&&count<=bound,"Growing sight action queue exceeds its bound.");
            var items=Native.Items(list).ToArray();Require(items.Length==count,"Growing sight queue contains null actions.");return items;
        }
        internal static Pending Stage(object state,byte owner,IReadOnlyList<object> changedUnits)
        {
            Require(changedUnits is not null&&changedUnits.Count<=MaxSources,"Growing sight source count exceeds native map bound.");
            var pending=new Pending(state,owner,changedUnits.Count);
            try{pending.StageSources(changedUnits);return pending;}
            catch(Exception error)
            {
                try{pending.Rollback();}
                catch(Exception rollback){throw new InvalidOperationException("Growing sight rollback failed; retire this state.",new AggregateException(error,rollback));}
                throw;
            }
        }
        internal sealed class Pending:IDisposable
        {
            readonly object State,Map,Owner,OriginalStack,StagedStack;
            readonly object[] Prefix;
            readonly byte OwnerId;
            readonly uint Round;
            readonly bool OriginalDirty;
            readonly int Limit;
            readonly List<Position> Tail=new();
            readonly Dictionary<long,(object Tile,bool Explored)> Fog=new();
            bool Attached,Prepared,Accepted,Disposed,RouteMarked;
            sealed record Source(object Tile,int Range,Position[] Targets);
            internal int AddedCount=>Tail.Count;
            internal Pending(object state,byte owner,int count)
            {
                Require(NativeRegroupSession.Enabled&&NativeRegroupSession.Ready(state)&&Native.Int(Native.Get(state,"Version"))==123,
                    "Growing sight requires an owned native123 state.");
                State=state;OwnerId=owner;Round=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));
                Require(Convert.ToByte(Native.Get(state,"CurrentPlayer"))==owner,"Growing sight actor is not ending its turn.");
                Map=Native.Get(state,"Map")??throw new InvalidDataException("Growing sight has no map.");Owner=Player(state,owner);
                OriginalStack=Native.Get(state,"ActionStack")??throw new InvalidDataException("Growing sight has no action queue.");
                Prefix=Actions(OriginalStack,MaxPrefix);OriginalDirty=(bool)Native.Get(state,"HasFlaggedNeedForUpdateRoutes")!;
                Limit=MaxPrefix+count*MaxArea;
                StagedStack=Native.NewList("ActionBase",Prefix);
                Require(!Native.Same(OriginalStack,StagedStack),"Growing sight staged queue aliases the original.");
                AssertPrefix(StagedStack,false);
            }
            void Context()
            {
                Require(!Disposed&&!Accepted&&NativeRegroupSession.Ready(State)&&Native.Same(Native.Get(State,"Map"),Map)&&
                    Convert.ToByte(Native.Get(State,"CurrentPlayer"))==OwnerId&&Convert.ToUInt32(Native.Get(State,"CurrentTurn"))==Round&&
                    Native.Same(Player(State,OwnerId),Owner),"Growing sight transaction context changed.");
                Require((bool)Native.Get(State,"HasFlaggedNeedForUpdateRoutes")! == (OriginalDirty||RouteMarked),"Growing sight route flag changed outside staging.");
                AssertPrefix(OriginalStack,false);
            }
            void AssertPrefix(object list,bool tail)
            {
                var items=Actions(list,Limit);Require(tail?items.Length>=Prefix.Length:items.Length==Prefix.Length,"Growing sight changed the original action count.");
                for(int i=0;i<Prefix.Length;i++)Require(Native.Same(items[i],Prefix[i]),"Growing sight changed original action identity/order.");
            }
            Source? Capture(object unit,Dictionary<uint,NativeLedgerState.Body> graph)
            {
                uint id=Convert.ToUInt32(Native.Get(unit,"id"));
                Require(graph.TryGetValue(id,out var body)&&body.Root&&Native.Same(body.Unit,unit)&&
                    Convert.ToByte(Native.Get(unit,"owner"))==OwnerId&&Native.Int(Native.Get(unit,"health"))>0,"Growing sight source is not the same live owned graph root.");
                object coordinates=Native.Get(unit,"coordinates")!;
                object tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Map,coordinates)
                    ??throw new InvalidDataException("Growing sight source tile is absent.");
                Require(At(Native.Get(tile,"coordinates")!)==At(coordinates)&&Native.Same(Native.Get(tile,"unit"),unit),
                    "Growing sight source does not occupy its exact tile.");
                // Passive growth is not a new discovery source for Blackout.
                // The body may still grow while covered; skip only its sight.
                var position=At(coordinates);
                bool covered=NativeBlackout.Covered(State,OwnerId,new(position.X,position.Y));
                Require(covered||Explored(tile,OwnerId),"Growing sight source does not occupy an explored tile.");
                object data=Native.Get(unit,"UnitData")??throw new InvalidDataException("Growing sight data is absent.");
                Require(Equals(Native.Get(data,"type"),Native.Get(unit,"type")),"Growing sight carrier/data mismatch.");
                int range=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetSightRange","Polytopia.Data.UnitData"),null,data));
                Require(range is >=1 and <=2,"Growing sight range is outside audited bounds.");
                if(covered)return null;
                int radius=Native.Int(Native.Get(tile,"terrain"))==4?2:range;
                object area=Native.Invoke(Native.M("MapDataExtensions","GetAreaSorted","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),
                    null,Map,Native.Get(tile,"coordinates"),radius,true,true)!;
                int count=Native.Int(Native.Get(area,"Length"));Require(count is >0 and <=MaxArea,"Growing sight footprint exceeds audited bounds.");
                var targets=new List<Position>();var item=Native.M(area.GetType(),"get_Item","System.Int32");
                for(int i=count-1;i>=0;i--)
                {
                    object target=Native.Invoke(item,area,i)??throw new InvalidDataException("Growing sight footprint contains null.");
                    object at=Native.Get(target,"coordinates")!;
                    Require(Native.Same(target,Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Map,at)),"Growing sight footprint contains a foreign tile.");
                    bool explored=Explored(target,OwnerId);long pointer=Native.Pointer(target);
                    if(Fog.TryGetValue(pointer,out var before))Require(before.Explored==explored,"Growing sight fog changed during capture.");
                    else Fog.Add(pointer,(target,explored));
                    var targetAt=At(at);
                    // NativeBlackoutDiscovery rejects these children during
                    // ExploreTile. Match that queue contract rather than
                    // counting actions that native processing cannot append.
                    if(!explored&&!NativeBlackout.Covered(State,OwnerId,new(targetAt.X,targetAt.Y)))targets.Add(targetAt);
                }
                return new(tile,range,targets.ToArray());
            }
            internal void StageSources(IReadOnlyList<object> units)
            {
                Require(!Prepared&&!Attached,"Growing sight staged twice.");Context();
                Require(Native.Same(Native.Get(State,"ActionStack"),OriginalStack),"Growing sight original queue changed before staging.");
                var graph=NativeLedgerState.Graph(State);var seen=new HashSet<long>();var sources=new List<Source>();
                foreach(var unit in units){Require(unit is not null&&seen.Add(Native.Pointer(unit)),"Duplicate or null growing sight source.");var source=Capture(unit,graph);if(source is not null)sources.Add(source);}
                if(sources.Count==0){Prepared=true;return;}
                // Mark before setters so their partial-write failures are covered.
                Attached=true;Native.Set(State,"ActionStack",StagedStack);
                foreach(var source in sources)
                {
                    Require(Native.Same(Native.Get(State,"ActionStack"),StagedStack),"Growing sight lost its owned queue during append.");
                    Native.Invoke(Native.M("ActionUtils","ExploreFromTile","GameState","PlayerState","TileData","System.Int32","System.Boolean"),
                        null,State,Owner,source.Tile,source.Range,true);
                    // Native queues overlap independently; IsValid filters them
                    // on execution. Require distinct action objects, not tiles.
                    Tail.AddRange(source.Targets);
                    Require(Native.Int(Native.Get(StagedStack,"Count"))<=Limit,"Growing sight appended beyond its queue bound.");
                }
                RouteMarked=true;Native.Set(State,"HasFlaggedNeedForUpdateRoutes",true);Prepared=true;AssertPending();
            }
            internal void AssertPending()
            {
                Context();Require(Native.Same(Native.Get(State,"ActionStack"),Attached?StagedStack:OriginalStack),"Growing sight lost its owned queue.");
                if(Attached)
                {
                    AssertPrefix(StagedStack,true);var actions=Actions(StagedStack,Limit);
                    Require(actions.Length==Prefix.Length+Tail.Count,"Growing sight appended an unexpected action count.");
                    var pointers=new HashSet<long>(Prefix.Select(Native.Pointer));
                    for(int i=0;i<Tail.Count;i++)
                    {
                        var action=actions[Prefix.Length+i];Require(pointers.Add(Native.Pointer(action)),"Growing sight reused an existing action object.");
                        var explore=Native.As(action,"ExploreAction");
                        Require(Convert.ToByte(Native.Get(explore,"PlayerId"))==OwnerId&&At(Native.Get(explore,"Coordinates")!)==Tail[i],
                            "Growing sight appended a wrong owner, coordinate, action type or order.");
                    }
                }
                foreach(var before in Fog.Values)Require(Explored(before.Tile,OwnerId)==before.Explored,"Growing sight executed fog changes before publication.");
            }
            internal void Accept(){Require(Prepared,"Unprepared growing sight cannot commit.");AssertPending();Accepted=true;}
            internal void Rollback()
            {
                Require(!Accepted,"Accepted growing sight cannot roll back.");if(Disposed)return;
                if(Attached)
                {
                    var current=Native.Get(State,"ActionStack");Require(Native.Same(current,StagedStack)||Native.Same(current,OriginalStack),"Foreign queue replaced growing sight stage.");
                    Native.Set(State,"ActionStack",OriginalStack);Native.Set(State,"HasFlaggedNeedForUpdateRoutes",OriginalDirty);RouteMarked=false;
                    Require(Native.Same(Native.Get(State,"ActionStack"),OriginalStack)&&(bool)Native.Get(State,"HasFlaggedNeedForUpdateRoutes")! == OriginalDirty,"Growing sight queue rollback failed.");
                    AssertPrefix(OriginalStack,false);Attached=false;
                }
                Disposed=true;
            }
            public void Dispose(){if(Accepted){Disposed=true;return;}Rollback();}
        }
    }
}
