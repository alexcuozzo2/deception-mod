using System;
using System.IO;
using System.Collections.Generic;
using HarmonyLib;
using System.Linq;

namespace Deception.PolyModAdapter
{
    // Existing native exploration and population fields are the persistence.
    // A covered corner can pay once again only after a legitimate discovery.
    internal static class NativeBlackoutLighthouse
    {
        const int MaxQueue=16384;
        sealed record Trace(NativeLedgerOwnership.WeakIdentity Action,NativeLedgerOwnership.WeakIdentity State,NativeLedgerOwnership.WeakIdentity Capital,byte Actor,int X,int Y);
        static readonly Dictionary<long,Trace> PendingTraces=new();
        internal static void Install(Harmony h)=>Native.Hook(h,Native.M("DecreasePopulationAction","Execute","GameState"),typeof(NativeBlackoutLighthouse),nameof(BeforeDecrease),nameof(AfterDecrease),nameof(DecreaseFinished));
        internal static void Clear()=>PendingTraces.Clear();
        internal sealed record Debit(object State,object Tile,object Improvement,TraceDetail Detail,short Population,short Xp,uint Score);
        internal sealed record TraceDetail(byte Actor,int X,int Y);
        static void Note(string kind,string message){try{Entry.Note(kind,message);}catch{}}
        static void Track(object state,object action,object capital,byte actor,NativeSpyNetwork.Position source)
        {
            foreach(long key in PendingTraces.Where(p=>p.Value.Action.Target() is null||p.Value.State.Target() is null).Select(p=>p.Key).ToArray())PendingTraces.Remove(key);
            if(PendingTraces.Count>=MaxQueue){Note("blackout-lighthouse-trace-limit","Native population debit remains queued; diagnostic capacity reached.");return;}
            PendingTraces[Native.Pointer(action)]=new(new(action),new(state),new(capital),actor,source.X,source.Y);
        }
        public static void BeforeDecrease(object __instance,object __0,out Debit? __state)
        {
            __state=null;
            try
            {
                long key=Native.Pointer(__instance);if(!PendingTraces.TryGetValue(key,out var trace)||!trace.Action.Matches(__instance)||!trace.State.Matches(__0))return;
                PendingTraces.Remove(key);object? tile=Tile(__0,Native.Get(__instance,"Target")!);
                if(tile is null||!trace.Capital.Matches(tile))return;object improvement=Native.Get(tile,"improvement")!;
                __state=new(__0,tile,improvement,new(trace.Actor,trace.X,trace.Y),Convert.ToInt16(Native.Get(improvement,"population")),Convert.ToInt16(Native.Get(improvement,"xp")),Convert.ToUInt32(Native.Get(Player(__0,trace.Actor),"score")));
            }
            catch(Exception e){Note("blackout-lighthouse-trace-error",e.Message);}
        }
        public static void AfterDecrease(Debit? __state)
        {
            if(__state is not Debit d)return;
            try
            {
                if(!Native.Same(Native.Get(d.Tile,"improvement"),d.Improvement))return;
                short population=Convert.ToInt16(Native.Get(d.Improvement,"population")),xp=Convert.ToInt16(Native.Get(d.Improvement,"xp"));
                Note("blackout-lighthouse-debit",$"player={d.Detail.Actor}; lighthouse={d.Detail.X},{d.Detail.Y}; capital={NativeSpyNetwork.At(Native.Get(d.Tile,"coordinates")!)}; population={d.Population}->{population}; xp={d.Xp}->{xp}; score={d.Score}->{Native.Get(Player(d.State,d.Detail.Actor),"score")}; expected={population==d.Population-1&&xp==d.Xp-1}");
            }
            catch(Exception e){Note("blackout-lighthouse-trace-error",e.Message);}
        }
        public static Exception? DecreaseFinished(Exception? __exception,Debit? __state)
        {if(__exception is not null&&__state is not null)Note("blackout-lighthouse-debit-error",__exception.Message);return __exception;}
        static void Require(bool yes,string why){if(!yes)throw new InvalidDataException(why);}
        static object Player(object state,byte id)
        {object?[] args={id,null};Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!,"Lighthouse player is absent.");return args[1]!;}
        static object? Tile(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static bool Lighthouse(object state,object tile)=>(bool)Native.Invoke(Native.M("ExploreLightHouseTask","ShouldUseLighthouseTasks","GameState"),null,state)!&&
            (bool)Native.Invoke(Native.M("ExploreLightHouseTask","EvaluateTile","TileData","GameState"),null,tile,state)!;
        static object[] Queue(object state)
        {object list=Native.Get(state,"ActionStack")!;int n=Native.Int(Native.Get(list,"Count"));Require(n>=0&&n<=MaxQueue,"Lighthouse queue exceeds bound.");return Native.Items(list).ToArray();}
        static bool Target(object action,string type,string field,byte actor,NativeSpyNetwork.Position at)
        {object value;try{value=Native.As(action,type);}catch(InvalidCastException){return false;}return Convert.ToByte(Native.Get(value,"PlayerId"))==actor&&NativeSpyNetwork.At(Native.Get(value,field)!)==at;}
        static void Drained(object state,byte actor,NativeSpyNetwork.Position at)
        {
            Require(!Queue(state).Any(a=>Target(a,"IncreasePopulationAction","Source",actor,at)||Target(a,"ImprovementLevelUpAction","Coordinates",actor,at)||Target(a,"IncreaseScoreAction","Source",actor,at)),
                "A lighthouse discovery reward is still pending.");
        }
        static object? Capital(object state,object player)
        {
            object at=Native.Invoke(Native.M("PlayerExtensions","GetCurrentCapitalCoordinates","PlayerState","GameState"),null,player,state)!;
            object? tile=Tile(state,at);if(tile is null)return null;
            Require(Convert.ToByte(Native.Get(tile,"owner"))==Convert.ToByte(Native.Get(player,"Id"))&&
                (bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),tile,Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!,"Lighthouse capital is not an owned city.");
            return tile;
        }
        sealed class Loss
        {
            internal readonly object Player;internal readonly object? Capital,Improvement;
            internal readonly uint Score;internal readonly short Population,Xp;internal readonly int ScoreLoss;
            internal Loss(object state,object player,int explorationScore)
            {
                Player=player;Score=Convert.ToUInt32(Native.Get(player,"score"));Capital=NativeBlackoutLighthouse.Capital(state,player);ScoreLoss=explorationScore;
                if(Capital is not null)
                {
                    Improvement=Native.Get(Capital,"improvement")!;Population=Convert.ToInt16(Native.Get(Improvement,"population"));Xp=Convert.ToInt16(Native.Get(Improvement,"xp"));
                    Require(Population>short.MinValue&&Xp>short.MinValue,"Lighthouse population underflow.");
                    // Native scoring uses the actual version and registry. The
                    // comparison mutates only fresh detached native objects.
                    object copy=Native.DetachedClone(Improvement,"ImprovementState"),tile=Native.DetachedClone(Capital,"TileData");Native.Set(tile,"improvement",copy);
                    int before=Native.Int(Native.Invoke(Native.M("ScoreExtensions","CalculateImprovementScore","GameState","TileData"),null,state,Capital));
                    Native.Invoke(Native.M("ImprovementState","AddPopulation","System.Int16"),copy,(short)-1);
                    int after=Native.Int(Native.Invoke(Native.M("ScoreExtensions","CalculateImprovementScore","GameState","TileData"),null,state,tile));
                    Require(before>=after,"Lighthouse population score delta is negative.");ScoreLoss=checked(explorationScore+before-after);
                }
                Require(Score>=ScoreLoss,"Lighthouse score cannot cover its forgotten discovery.");
            }
            internal void Assert()
            {Require(Score==Convert.ToUInt32(Native.Get(Player,"score")),"Lighthouse player score changed.");if(Improvement is not null)Require(Native.Same(Improvement,Native.Get(Capital!,"improvement"))&&Population==Convert.ToInt16(Native.Get(Improvement,"population"))&&Xp==Convert.ToInt16(Native.Get(Improvement,"xp")),"Lighthouse capital changed.");}
            internal object? DebitAction()=>Capital is null?null:Activator.CreateInstance(Native.T("DecreasePopulationAction"),Convert.ToByte(Native.Get(Player,"Id")),Native.Get(Capital,"coordinates"),0)!;
            internal void ForgetScore(int explorationScore)
            {
                Native.Invoke(Native.M("ActionUtils","RemoveScore","PlayerState","System.Int32"),null,Player,explorationScore);
                Require(Convert.ToUInt32(Native.Get(Player,"score"))==Score-explorationScore,"Native lighthouse exploration score withdrawal differs.");
            }
            internal void Restore()
            {if(Improvement is not null){Require(Native.Same(Improvement,Native.Get(Capital!,"improvement")),"Lighthouse rollback capital was replaced.");Native.Set(Improvement,"population",Population);Native.Set(Improvement,"xp",Xp);}Native.Set(Player,"score",Score);}
        }
        internal sealed class Forget
        {
            readonly object State,Tile,Improvement,OriginalQueue;readonly object[] Prefix;readonly ushort Level;readonly Loss[] Losses;readonly int ExplorationScore;object? StagedQueue;object?[] Actions=Array.Empty<object?>();bool Changed;
            internal Forget(object state,object tile,byte[] victims)
            {
                State=state;Tile=tile;OriginalQueue=Native.Get(state,"ActionStack")!;Prefix=Queue(state);Improvement=Native.Get(tile,"improvement")??throw new InvalidDataException("Lighthouse improvement is absent.");Level=Convert.ToUInt16(Native.Get(Improvement,"level"));
                Require(Level>=victims.Length,"Lighthouse discovery level is inconsistent.");ExplorationScore=Native.Int(Native.Static("ScoreSheet","exploreValue"));Require(ExplorationScore>=0,"Invalid native discovery score.");
                var at=NativeSpyNetwork.At(Native.Get(tile,"coordinates")!);foreach(byte p in victims)Drained(state,p,at);
                Losses=victims.Select(p=>new Loss(state,Player(state,p),ExplorationScore)).ToArray();
            }
            internal void Apply()
            {
                Require(!Changed&&Native.Same(Improvement,Native.Get(Tile,"improvement"))&&Convert.ToUInt16(Native.Get(Improvement,"level"))==Level,"Lighthouse changed before forgetting.");foreach(var loss in Losses)loss.Assert();
                Require(Native.Same(Native.Get(State,"ActionStack"),OriginalQueue)&&Queue(State).Select(Native.Pointer).SequenceEqual(Prefix.Select(Native.Pointer)),"Lighthouse queue changed before forgetting.");
                Actions=Losses.Select(loss=>loss.DebitAction()).ToArray();Require(Prefix.Length+Actions.Count(a=>a is not null)<=MaxQueue,"Lighthouse debit queue is full.");
                StagedQueue=Native.NewList("ActionBase",Prefix.Concat(Actions.Where(a=>a is not null).Cast<object>()));
                // The native dispatcher executes these children before older
                // actions, and also creates their normal population reactions.
                // Inline Execute skipped that display/animation lifecycle.
                Changed=true;foreach(var loss in Losses)loss.ForgetScore(ExplorationScore);Native.Set(Improvement,"level",checked((ushort)(Level-Losses.Length)));Native.Set(State,"ActionStack",StagedQueue);
            }
            internal void Accepted()
            {
                try
                {
                    var source=NativeSpyNetwork.At(Native.Get(Tile,"coordinates")!);
                    for(int i=0;i<Losses.Length;i++)
                    {
                        var loss=Losses[i];byte actor=Convert.ToByte(Native.Get(loss.Player,"Id"));
                        if(Actions[i] is object action&&loss.Capital is object capital)Track(State,action,capital,actor,source);
                        Note("blackout-lighthouse-forgotten",$"player={actor}; lighthouse={source.X},{source.Y}; populationBefore={loss.Population}; xpBefore={loss.Xp}; debitQueued={Actions[i] is not null}");
                    }
                }
                catch(Exception e){Note("blackout-lighthouse-trace-error",e.Message);}
            }
            internal void Rollback()
            {
                if(!Changed)return;
                Require(Native.Same(Native.Get(State,"ActionStack"),OriginalQueue)||Native.Same(Native.Get(State,"ActionStack"),StagedQueue),"Foreign queue replaced Lighthouse debit stage.");
                foreach(var loss in Losses)loss.Restore();Require(Native.Same(Improvement,Native.Get(Tile,"improvement")),"Lighthouse rollback improvement was replaced.");Native.Set(Improvement,"level",Level);Native.Set(State,"ActionStack",OriginalQueue);Changed=false;
            }
        }
        internal static void RecordCommit(object state,object tile,byte caster,byte[] priorExplorers,Forget? debit)
        {
            try{var at=NativeSpyNetwork.At(Native.Get(tile,"coordinates")!);Note("blackout-lighthouse-target",$"caster={caster}; target={at.X},{at.Y}; nativeCorner={Lighthouse(state,tile)}; priorExplorers={string.Join(",",priorExplorers)}; debitPrepared={debit is not null}");debit?.Accepted();}
            catch(Exception e){Note("blackout-lighthouse-trace-error",e.Message);}
        }
        internal static Forget? PrepareForget(object state,object tile,byte caster)
        {
            if(!Lighthouse(state,tile))return null;
            var at=NativeSpyNetwork.At(Native.Get(tile,"coordinates")!);
            var victims=Native.Items(Native.Get(state,"PlayerStates")!).Select(p=>Convert.ToByte(Native.Get(p,"Id"))).Where(p=>p!=caster&&p is not (0 or 255)&&!NativeBlackout.Covered(state,p,at)&&
                (bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,p)!).Distinct().ToArray();
            return victims.Length==0?null:new(state,tile,victims);
        }
        internal sealed class Discovery
        {
            readonly object State,Player,OriginalQueue,StagedQueue,OriginalTasks,StagedTasks;
            readonly (object Task,bool Started,bool Completed)[] Tasks;
            readonly uint Score;readonly bool Routes;bool Attached;
            internal Discovery(object state,byte actor)
            {
                State=state;Player=NativeBlackoutLighthouse.Player(state,actor);
                // Normal native LIFO dispatch drains the debit before any
                // older exploration child. Refuse out-of-order direct calls;
                // they must not award a rediscovery before its loss executes.
                Require(!Queue(state).Any(a=>PendingDebit(a,actor)),"A native population debit must finish before Lighthouse rediscovery.");Score=Convert.ToUInt32(Native.Get(Player,"score"));Routes=(bool)Native.Get(state,"HasFlaggedNeedForUpdateRoutes")!;
                OriginalQueue=Native.Get(state,"ActionStack")!;var queue=Queue(state);Require(queue.Length<MaxQueue-16,"Lighthouse discovery queue is full.");StagedQueue=Native.NewList("ActionBase",queue);
                OriginalTasks=Native.Get(Player,"tasks")!;var tasks=Native.Items(OriginalTasks).ToArray();Require(tasks.Length<=128,"Lighthouse task list exceeds bound.");
                Tasks=tasks.Select(t=>(t,(bool)Native.Get(t,"IsStarted")!,(bool)Native.Get(t,"IsCompleted")!)).ToArray();StagedTasks=Native.NewList("TaskBase",tasks);
            }
            internal void Attach(){Attached=true;Native.Set(State,"ActionStack",StagedQueue);Native.Set(Player,"tasks",StagedTasks);}
            internal void Immediate(object tile)=>Native.Invoke(Native.M("ActionUtils","OnRevealLighthouseTile","GameState","PlayerState","WorldCoordinates"),null,State,Player,Native.Get(tile,"coordinates"));
            internal void Rollback()
            {
                if(!Attached)return;Require((Native.Same(Native.Get(State,"ActionStack"),StagedQueue)||Native.Same(Native.Get(State,"ActionStack"),OriginalQueue))&&
                    (Native.Same(Native.Get(Player,"tasks"),StagedTasks)||Native.Same(Native.Get(Player,"tasks"),OriginalTasks)),"Lighthouse discovery staging was replaced.");
                foreach(var t in Tasks){Native.Set(t.Task,"IsStarted",t.Started);Native.Set(t.Task,"IsCompleted",t.Completed);}Native.Set(Player,"tasks",OriginalTasks);Native.Set(Player,"score",Score);Native.Set(State,"ActionStack",OriginalQueue);Native.Set(State,"HasFlaggedNeedForUpdateRoutes",Routes);Attached=false;
            }
        }
        static bool PendingDebit(object action,byte actor)
        {object debit;try{debit=Native.As(action,"DecreasePopulationAction");}catch(InvalidCastException){return false;}return Convert.ToByte(Native.Get(debit,"PlayerId"))==actor;}
        internal static Discovery? PrepareDiscovery(object state,object tile,byte actor)=>Lighthouse(state,tile)?new(state,actor):null;
    }
}
