using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // A saved ExploreAction alone does not say what discovered its tile. The
    // private permit records an approved *new* source, while native action
    // execution owns ordinary exploration, meeting players and route effects.
    internal static class NativeBlackoutDiscovery
    {
        [ThreadStatic] static Source? Current;
        [ThreadStatic] static Explore? Exploring;
        static long Generation;
        static void Require(bool yes,string why){if(!yes)throw new InvalidDataException(why);}
        internal sealed class Collection
        {
            internal readonly HashSet<LedgerDiscovery> Permits=new();
            internal NativeIdentityLedger Merge(NativeIdentityLedger ledger)
            {
                var covered=(ledger.Blackouts??Array.Empty<LedgerBlackout>()).Select(m=>(m.Player,m.X,m.Y)).ToHashSet();
                return ledger with{Discoveries=(ledger.Discoveries??Array.Empty<LedgerDiscovery>()).Concat(Permits)
                    .Where(p=>covered.Contains((p.Player,p.X,p.Y))).Distinct().OrderBy(p=>p.Player).ThenBy(p=>p.X).ThenBy(p=>p.Y).ToArray()};
            }
        }
        internal sealed class Source:IDisposable
        {
            internal readonly object State;internal readonly byte Actor;internal readonly Source? Previous;
            internal readonly Collection Pending;internal readonly bool Publish;internal bool Explicit,Allowed;
            internal object? Map;internal readonly object? Action;
            internal object? City,Improvement,QueueOwner;
            internal readonly long Epoch;internal uint Turn;
            internal readonly bool Explorer,Border;
            internal object[] Queue=Array.Empty<object>();
            internal NativeSpyNetwork.Position? Center;
            internal HashSet<NativeSpyNetwork.Position>? Footprint;
            internal bool Retired,Executed;
            internal Source(object state,byte actor,Collection pending,bool publish,bool explicitReveal,object? action=null,bool explorer=false,bool border=false)
            {
                State=state;Actor=actor;Pending=pending;Publish=publish;Explicit=explicitReveal;Action=action;Explorer=explorer;Border=border;
                Epoch=Generation;Previous=Current;Current=this;
            }
            internal void Admit()
            {
                // Inert tokens never read an unadopted state's native fields.
                Map=Native.Get(State,"Map")!;Turn=Convert.ToUInt32(Native.Get(State,"CurrentTurn"));
                QueueOwner=Border?Native.Get(State,"ActionStack"):null;Queue=QueueOwner is not null?Native.Items(QueueOwner).ToArray():Array.Empty<object>();
                Center=Border?NativeSpyNetwork.At(Native.Get(Action!,"Coordinates")!):null;
                City=Border?Tile(State,Native.Get(Action!,"Coordinates")!):null;Improvement=City is null?null:Native.Get(City,"improvement");
                Allowed=true;
            }
            internal bool Active=>Allowed&&!Retired&&Epoch==Generation&&NativeRegroupSession.Ready(State)&&Native.Same(Map,Native.Get(State,"Map"))&&
                Turn==Convert.ToUInt32(Native.Get(State,"CurrentTurn"))&&(!(Explorer||Border)||CurrentPlayer(State,Actor))&&
                (Action is null||(Queued(State,Action)&&Convert.ToByte(Native.Get(Action,"PlayerId"))==Actor));
            public void Dispose(){if(Retired)return;Retired=true;if(ReferenceEquals(Current,this))Current=Epoch==Generation?Previous:null;}
        }
        internal sealed class Explore
        {
            internal readonly object State,ActionOrTile;internal readonly byte Player;internal readonly NativeSpyNetwork.Position At;
            internal readonly Explore? Previous;internal readonly bool Queued;internal readonly object? OldExplorers;internal readonly object?[] Queue;
            internal readonly NativeGameStateEnvelope.Snapshot? Before;
            internal NativeBlackoutLighthouse.Discovery? Lighthouse;
            internal Explore(object state,object actionOrTile,byte player,NativeSpyNetwork.Position at,bool queued,object? oldExplorers,object?[] queue)
            {State=state;ActionOrTile=actionOrTile;Player=player;At=at;Queued=queued;OldExplorers=oldExplorers;Queue=queue;Before=NativeGameStateEnvelope.Get(state);Previous=Exploring;Exploring=this;}
        }
        internal sealed record Footprint(Source Owner,HashSet<NativeSpyNetwork.Position>? Previous);
        internal static void Install(Harmony h)
        {
            var patch=typeof(NativeBlackoutDiscovery);
            foreach(string name in new[]{"MoveAction","ScoutMoveAction","TrainAction","UpgradeAction","ConvertAction","RuleAreaAction","ExpandCityAction","EstablishEmbassyAction","RevealCapitalAction"})
                Native.Hook(h,Native.M(name,"Execute","GameState"),patch,nameof(BeforeSource),nameof(AfterSource),nameof(SourceFinished));
            Native.Hook(h,Native.M("ActionUtils","ExploreFromTile","GameState","PlayerState","TileData","System.Int32","System.Boolean"),patch,nameof(BeforeFootprint),null,nameof(FootprintFinished));
            Native.Hook(h,Native.M("ActionUtils","ExploreTile","GameState","PlayerState","TileData","System.Boolean"),patch,nameof(BeforeTile),nameof(AfterTile),nameof(TileFinished));
            Native.Hook(h,Native.M("ExploreAction","IsValid","GameState"),patch,null,nameof(AfterValid));
            Native.Hook(h,Native.M("ExploreAction","Execute","GameState"),patch,nameof(BeforeExecute),nameof(AfterExecute),nameof(ExecuteFinished));
        }
        internal static void Clear(){Generation++;Current=null;Exploring=null;}
        static bool Queued(object state,object action)=>Native.Items(Native.Get(state,"ActionStack")!).Any(a=>Native.Same(a,action));
        static object? Tile(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static bool SourceAccessible(object state,object? tile,byte actor)=>tile is not null&&NativeSpyNetwork.Accessible(state,tile,actor);
        static bool CurrentPlayer(object state,byte actor)
        {
            if(actor is 0 or byte.MaxValue||Native.Int(Native.Get(state,"CurrentPlayer"))!=actor)return false;
            object?[] args={actor,null};return (bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&args[1] is not null;
        }
        static bool OwnedCity(object? tile,byte actor)=>tile is not null&&Convert.ToByte(Native.Get(tile,"owner"))==actor&&
            (bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),tile,Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!;
        static bool HasPermit(object state,byte actor,NativeSpyNetwork.Position at)
        {
            var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return false;
            return (NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Discoveries??Array.Empty<LedgerDiscovery>()).Any(p=>p.Player==actor&&p.X==at.X&&p.Y==at.Y);
        }
        public static void BeforeSource(object __instance,object __0,MethodBase __originalMethod,out Source? __state,bool __runOriginal=true)
        {
            byte actor=Convert.ToByte(Native.Get(__instance,"PlayerId"));string kind=__originalMethod.DeclaringType!.Name;bool allowed=false,explicitReveal=false;
            // An inert nested token masks outer authority even when this native
            // action is suppressed, detached, or belongs to another context.
            __state=new(__0,actor,new(),true,false,__instance,kind=="ScoutMoveAction",kind=="ExpandCityAction");
            if(!__runOriginal||!NativeRegroupSession.Ready(__0)||!Queued(__0,__instance))return;
            if(kind=="ScoutMoveAction")
            {
                // City/ruin explorers reserve an animation id but create no
                // UnitState. Their queued native action owns the selected To
                // tile; its occupant is unrelated to the explorer's sight.
                allowed=CurrentPlayer(__0,actor)&&Tile(__0,Native.Get(__instance,"From")!) is not null;
            }
            else if(kind=="ExpandCityAction")
                allowed=CurrentPlayer(__0,actor)&&OwnedCity(Tile(__0,Native.Get(__instance,"Coordinates")!),actor);
            else if(kind=="MoveAction")
            {
                object? unit=NativeSpyNetwork.Unit(__0,Convert.ToUInt32(Native.Get(__instance,"UnitId")));
                allowed=unit is not null&&NativeRogueRules.CanCommand(__0,unit,actor)&&SourceAccessible(__0,Tile(__0,Native.Get(unit,"coordinates")!),actor);
            }
            else if(kind=="UpgradeAction")
            {
                // Native Upgrade replaces the root, clones its identity, then
                // explores using the new real UnitData's sight range. The
                // old root must be commandable on an accessible source tile;
                // a passive/covered/foreign upgrade cannot refresh Blackout.
                var tile=Tile(__0,Native.Get(__instance,"Coordinates")!);
                var unit=tile is null?null:Native.Get(tile,"unit");
                allowed=unit is not null&&NativeRogueRules.CanCommand(__0,unit,actor)&&SourceAccessible(__0,tile,actor);
            }
            else if(kind=="ConvertAction")allowed=SourceAccessible(__0,Tile(__0,Native.Get(__instance,"Origin")!),actor);
            else if(kind=="EstablishEmbassyAction"||kind=="RevealCapitalAction")allowed=explicitReveal=true;
            else
            {
                var tile=Tile(__0,Native.Get(__instance,"Coordinates")!);
                allowed=SourceAccessible(__0,tile,actor)||(kind=="RuleAreaAction"&&CurrentPlayer(__0,actor)&&OwnedCity(tile,actor));
                explicitReveal=kind=="RuleAreaAction";
            }
            __state.Explicit=explicitReveal;if(allowed)__state.Admit();
        }
        public static void AfterSource(bool __runOriginal,Source? __state){if(__state is not null)__state.Executed=__runOriginal;}
        public static Exception? SourceFinished(Exception? __exception,Source? __state)
        {
            if(__state is null)return __exception;
            try
            {
                if(__exception is null&&__state.Publish&&__state.Executed&&__state.Active&&ReferenceEquals(Current,__state))
                {if(__state.Border)CollectBorderChildren(__state);Publish(__state.State,__state.Pending);}
            }
            catch(Exception e){NativeRegroupSession.Fault("Blackout discovery publication failed: "+e.Message);return __exception??e;}
            finally{__state.Dispose();}return __exception;
        }
        static void CollectBorderChildren(Source source)
        {
            // ExpandCityAction directly appends ExploreAction children, bypassing
            // ExploreTile. Admit only its new native queue suffix and real final
            // city radius after successful execution; never existing requests.
            if(!CurrentPlayer(source.State,source.Actor)||source.Action is null||source.Center!=NativeSpyNetwork.At(Native.Get(source.Action,"Coordinates")!))return;
            var city=Tile(source.State,Native.Get(source.Action,"Coordinates")!);
            if(!OwnedCity(city,source.Actor)||!Native.Same(city,source.City)||!Native.Same(Native.Get(city!,"improvement"),source.Improvement))return;
            if(!Native.Same(source.QueueOwner,Native.Get(source.State,"ActionStack")))return;
            object[] now=Native.Items(Native.Get(source.State,"ActionStack")!).ToArray();
            if(now.Length<source.Queue.Length||source.Queue.Where((a,i)=>!Native.Same(a,now[i])).Any())return;
            var old=source.Queue.Select(Native.Pointer).ToHashSet();
            object area=Native.Invoke(Native.M("MapDataExtensions","GetArea","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),null,
                source.Map,Native.Get(city!,"coordinates"),Native.Int(Native.Get(Native.Get(city!,"improvement")!,"borderSize")),true,true)!;
            var cells=Native.Items(area).ToArray();Require(cells.Length<=NativeSpyNetwork.MaxTiles,"Native city discovery area exceeds map bound.");
            foreach(var tile in cells)
            {
                var at=NativeSpyNetwork.At(Native.Get(tile,"coordinates")!);
                if(NativeBlackout.Covered(source.State,source.Actor,at)&&Native.Same(tile,Tile(source.State,Native.Get(tile,"coordinates")!))&&
                    now.Skip(source.Queue.Length).Any(a=>!old.Contains(Native.Pointer(a))&&IsExplore(a,source.Actor,at)))
                    source.Pending.Permits.Add(new(source.Actor,at.X,at.Y));
            }
        }
        // Caller stages real final-position sight, merges Collection into its
        // candidate ledger and publishes atomically with the exchange itself.
        internal static IDisposable ExchangeSource(object state,byte actor,object sourceTile,Collection collection)
        {
            Require(SourceAccessible(state,sourceTile,actor),"Exchange discovery source is inaccessible.");
            var source=new Source(state,actor,collection,false,false);
            try{source.Admit();return source;}catch{source.Dispose();throw;}
        }
        public static void BeforeFootprint(object __0,object __1,object __2,int __3,out Footprint? __state)
        {
            __state=null;
            if(Current is not Source scope||!scope.Active||!Native.Same(scope.State,__0)||scope.Actor!=Convert.ToByte(Native.Get(__1,"Id")))return;
            __state=new(scope,scope.Footprint);
            scope.Footprint=new();
            object? tile=Tile(__0,Native.Get(__2,"coordinates")!);if(!Native.Same(tile,__2)||__3 is <1 or >2)return;
            object? unit=Native.Get(__2,"unit");
            // Sight is computed from the real native body, never its costume.
            // Border/embassy explicit discovery is scoped separately.
            if(scope.Explorer)
            {
                if(!CurrentPlayer(__0,scope.Actor)||scope.Action is null||!Native.Same(Tile(__0,Native.Get(scope.Action,"To")!),__2)||
                    (__3==2&&Native.Int(Native.Get(__2,"terrain"))!=4))return;
            }
            else if(unit is not null)
            {
                if(!NativeRogueRules.CanCommand(__0,unit,scope.Actor))return;
                int actual=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetSightRange","Polytopia.Data.UnitData"),null,Native.Get(unit,"UnitData")));
                if(__3!=actual)return;
            }
            object area=Native.Invoke(Native.M("MapDataExtensions","GetAreaSorted","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),null,
                Native.Get(__0,"Map"),Native.Get(__2,"coordinates"),Native.Int(Native.Get(__2,"terrain"))==4?2:__3,true,true)!;
            int count=Native.Int(Native.Get(area,"Length"));Require(count is >0 and <=25,"Native discovery footprint exceeds range two.");
            var item=Native.M(area.GetType(),"get_Item","System.Int32");
            for(int i=0;i<count;i++)scope.Footprint.Add(NativeSpyNetwork.At(Native.Get(Native.Invoke(item,area,i)!,"coordinates")!));
        }
        public static Exception? FootprintFinished(Exception? __exception,Footprint? __state)
        {if(__state is not null&&ReferenceEquals(Current,__state.Owner)&&__state.Owner.Active)__state.Owner.Footprint=__state.Previous;return __exception;}
        public static bool BeforeTile(object __0,object __1,object __2,bool __3,out Explore? __state)
        {
            __state=null;byte actor=Convert.ToByte(Native.Get(__1,"Id"));var at=NativeSpyNetwork.At(Native.Get(__2,"coordinates")!);
            if(!NativeBlackout.Covered(__0,actor,at))return true;
            if(Current is not Source source||!source.Active||!Native.Same(source.State,__0)||source.Actor!=actor||
                (source.Footprint?.Contains(at)!=true&&!(source.Footprint is null&&source.Explicit)))return false;
            Require(Native.Same(Tile(__0,Native.Get(__2,"coordinates")!),__2),"Discovery target is not the exact source-state tile.");
            __state=new(__0,__2,actor,at,__3,Native.Get(__2,"explorers"),Native.Items(Native.Get(__0,"ActionStack")!).Cast<object?>().ToArray());
            if(!__3){__state.Lighthouse=NativeBlackoutLighthouse.PrepareDiscovery(__0,__2,actor);__state.Lighthouse?.Attach();Native.Set(__2,"explorers",NativeBlackout.ExplorerList(__state.OldExplorers is null?Array.Empty<object>():Native.Items(__state.OldExplorers)));}
            return true;
        }
        public static void AfterTile(Explore? __state)
        {
            if(__state is null)return;var e=__state;
            if(e.Queued)
            {
                var old=e.Queue.Select(x=>Native.Pointer(x!)).ToHashSet();
                bool found=Native.Items(Native.Get(e.State,"ActionStack")!).Any(a=>!old.Contains(Native.Pointer(a))&&IsExplore(a,e.Player,e.At));
                Require(found,"Native discovery did not queue its exact ExploreAction.");
                Current!.Pending.Permits.Add(new(e.Player,e.At.X,e.At.Y));
            }
            else if((bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),e.ActionOrTile,e.Player)!){e.Lighthouse?.Immediate(e.ActionOrTile);Consume(e.State,e.Player,e.At);}
        }
        static bool IsExplore(object action,byte actor,NativeSpyNetwork.Position at)
        {try{var e=Native.As(action,"ExploreAction");return Convert.ToByte(Native.Get(e,"PlayerId"))==actor&&NativeSpyNetwork.At(Native.Get(e,"Coordinates")!)==at;}catch{return false;}}
        public static Exception? TileFinished(Exception? __exception,Explore? __state)
        {
            if(__state is null)return __exception;
            try{if(__exception is not null&&!__state.Queued)Restore(__state,__state.ActionOrTile);}
            catch(Exception rollback){NativeRegroupSession.Fault("Blackout immediate discovery rollback failed: "+rollback.Message);return __exception is null?rollback:new AggregateException(__exception,rollback);}
            finally{if(ReferenceEquals(Exploring,__state))Exploring=__state.Previous;}
            return __exception;
        }
        internal static bool MayWrite(object state,object tile,byte actor)=>Exploring is Explore e&&Native.Same(state,e.State)&&actor==e.Player&&
            NativeSpyNetwork.At(Native.Get(tile,"coordinates")!)==e.At&&Native.Same(tile,Tile(state,Native.Get(tile,"coordinates")!));
        public static void AfterValid(object __instance,object __0,ref bool __result)
        {
            if(!__result)return;byte actor=Convert.ToByte(Native.Get(__instance,"PlayerId"));var at=NativeSpyNetwork.At(Native.Get(__instance,"Coordinates")!);
            if(NativeBlackout.Covered(__0,actor,at))__result=Queued(__0,__instance)&&HasPermit(__0,actor,at);
        }
        public static bool BeforeExecute(object __instance,object __0,out Explore? __state)
        {
            __state=null;byte actor=Convert.ToByte(Native.Get(__instance,"PlayerId"));var at=NativeSpyNetwork.At(Native.Get(__instance,"Coordinates")!);
            if(!NativeBlackout.Covered(__0,actor,at))return true;
            if(!NativeRegroupSession.Ready(__0)||!Queued(__0,__instance)||!HasPermit(__0,actor,at))return false;
            var tile=Tile(__0,Native.Get(__instance,"Coordinates")!);if(tile is null)return false;
            __state=new(__0,__instance,actor,at,true,Native.Get(tile,"explorers"),Array.Empty<object?>());
            __state.Lighthouse=NativeBlackoutLighthouse.PrepareDiscovery(__0,tile,actor);__state.Lighthouse?.Attach();
            Native.Set(tile,"explorers",NativeBlackout.ExplorerList(__state.OldExplorers is null?Array.Empty<object>():Native.Items(__state.OldExplorers)));return true;
        }
        public static void AfterExecute(Explore? __state)
        {
            if(__state is null)return;var e=__state;object tile=Tile(e.State,Native.Get(e.ActionOrTile,"Coordinates")!)!;
            Require((bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,e.Player)!,"Native permitted discovery did not reveal its tile.");
            Consume(e.State,e.Player,e.At);
        }
        public static Exception? ExecuteFinished(Exception? __exception,Explore? __state)
        {
            if(__state is null)return __exception;
            try
            {
                if(__exception is not null)
                {var tile=Tile(__state.State,Native.Get(__state.ActionOrTile,"Coordinates")!);if(tile is not null)Restore(__state,tile);NativeRegroupSession.Fault("Native Blackout discovery failed: "+__exception.Message);}
            }
            catch(Exception rollback){NativeRegroupSession.Fault("Blackout queued discovery rollback failed: "+rollback.Message);return __exception is null?rollback:new AggregateException(__exception,rollback);}
            finally{if(ReferenceEquals(Exploring,__state))Exploring=__state.Previous;}
            return __exception;
        }
        static void Restore(Explore value,object tile)
        {
            value.Lighthouse?.Rollback();Native.Set(tile,"explorers",value.OldExplorers);
            if(value.Before is not null)NativeGameStateEnvelope.Set(value.State,value.Before.Revision,value.Before.Bytes());
            NativeBlackout.Bind(value.State);
        }
        static void Publish(object state,Collection pending)
        {
            if(pending.Permits.Count==0)return;
            var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Discovery ledger is absent.");
            var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);var candidate=pending.Merge(ledger);
            if((ledger.Discoveries??Array.Empty<LedgerDiscovery>()).SequenceEqual(candidate.Discoveries!))return;
            long revision=checked(snapshot.Revision+1);NativeGameStateEnvelope.Set(state,revision,NativeIdentityLedgerCodec.Encode(candidate,revision));NativeBlackout.Bind(state);
        }
        static void Consume(object state,byte actor,NativeSpyNetwork.Position at)
        {
            var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Discovery ledger is absent.");
            var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
            var candidate=ledger with{Blackouts=(ledger.Blackouts??Array.Empty<LedgerBlackout>()).Where(m=>m.Player!=actor||m.X!=at.X||m.Y!=at.Y).ToArray(),
                Discoveries=(ledger.Discoveries??Array.Empty<LedgerDiscovery>()).Where(m=>m.Player!=actor||m.X!=at.X||m.Y!=at.Y).ToArray()};
            long revision=checked(snapshot.Revision+1);NativeGameStateEnvelope.Set(state,revision,NativeIdentityLedgerCodec.Encode(candidate,revision));NativeBlackout.Bind(state);
        }
    }
}
