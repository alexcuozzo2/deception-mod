using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    internal static class NativeTradeSecrets
    {
        internal sealed record Research(object State,object Player,TechnologyIntelProjection Projection,Dictionary<byte,object> Opponents);
        sealed record Node(object Data,string Id,string Slot,int Tier);
        sealed record Registry(object Gld,Dictionary<string,Node[]> Trees,IReadOnlyDictionary<string,string> Slots);
        static Registry? Catalog;
        static long Generation;
        static readonly HashSet<string> Errors=new();
        internal sealed class Turn
        {
            internal long Generation;internal object? State,Map,Player,Stack,Action;
            internal object[] Before=Array.Empty<object>();internal byte Actor;internal uint Number;internal int Command,Income;
            internal object? Added,AfterStack;
        }
        internal static void Clear(){Catalog=null;Generation++;Errors.Clear();}
        internal static void Install(Harmony h)
        {
            Native.Hook(h,Native.M("ResourceDataUtils","CalculateIncomeFor","GameState","System.Byte"),typeof(NativeTradeSecrets),null,nameof(Income));
            Native.Hook(h,Native.M("StartTurnAction","Execute","GameState"),typeof(NativeTradeSecrets),nameof(BeforeTurn),nameof(AfterTurn),nameof(FinishedTurn));
        }
        static void Error(Exception e){if(Errors.Count<8&&Errors.Add(e.Message))Entry.Note("trade-secrets-error",e.ToString());}
        internal static bool Ready(object state)=>NativeRegroupSession.Enabled&&RecruitmentHooks.Registered&&NativeRegroupSession.Ready(state)&&Native.Get(state,"CurrentState")!.ToString()=="Started";
        internal static object? Player(object state,byte id)
        {
            if(id is 0 or 255)return null;object?[] args={id,null};
            return (bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&args[1] is object p&&Native.Int(Native.Get(p,"Id"))==id?p:null;
        }
        internal static bool Has(object player,string id)=>(bool)Native.Invoke(Native.M("PlayerExtensions","HasTech","PlayerState",RecruitmentHooks.TT),null,player,Native.EnumValue(RecruitmentHooks.TT,id))!;
        static object[] Bounded(object list,int maximum,string label)
        {
            int count=Native.Int(Native.Get(list,"Count"));if(count<0||count>maximum)throw new InvalidOperationException("Unexpected "+label+" count");
            var values=Native.Items(list).ToArray();if(values.Length!=count)throw new InvalidOperationException("Null entry in "+label);return values;
        }
        static Registry RegistryFor(object state)
        {
            var gld=Native.Get(state,"GameLogicData")??throw new InvalidOperationException("Missing research registry");
            if(Catalog is Registry cached&&Native.Same(cached.Gld,gld))return cached;
            const string G="Polytopia.Data.GameLogicData",T="Polytopia.Data.TechData",R="Polytopia.Data.TribeData";
            var tribes=Bounded(Native.Invoke(Native.M(G,"GetAllTribes"),gld)!,64,"tribes")
                .Where(t=>Native.Int(Native.Get(t,"category"))!=0).ToArray();
            var trees=new Dictionary<string,object[]>(StringComparer.Ordinal);var all=new Dictionary<string,object>(StringComparer.Ordinal);
            foreach(var tribe in tribes)
            {
                string id=Native.EnumName(Native.Get(tribe,"type")!);
                var nodes=Bounded(Native.Invoke(Native.M(G,"GetAllTechForTribe",R),gld,tribe)!,256,"technologies");
                trees.Add(id,nodes);foreach(var node in nodes)all[Native.EnumName(Native.Get(node,"type")!)]=node;
            }
            // Prefer the ordinary Basic graph's names for equivalence classes.
            // Walking each effective tribe tree above also discovers replacement
            // nodes which change the shape of that graph, notably Polaris.
            var basic=Native.Invoke(Native.M(G,"GetTechData",RecruitmentHooks.TT),gld,Native.EnumValue(RecruitmentHooks.TT,"basic"))!;
            var pending=new Queue<object>();pending.Enqueue(basic);var original=new HashSet<string>(StringComparer.Ordinal);
            while(pending.Count>0)
            {
                var node=pending.Dequeue();string id=Native.EnumName(Native.Get(node,"type")!);if(!original.Add(id))continue;
                if(original.Count>256)throw new InvalidOperationException("Research graph exceeds supported bound");
                all[id]=node;foreach(var child in Bounded(Native.Get(node,"techUnlocks")!,256,"technology children"))pending.Enqueue(child);
            }
            var edges=new List<(string Source,string Target)>();
            // GetOverride is the game's actual public override resolution. No
            // private research, unlock caches, or player data is changed here.
            foreach(var tribe in tribes)
                foreach(var pair in all.ToArray())
                {
                    var data=Native.Invoke(Native.M(G,"GetOverride",T,R),gld,pair.Value,tribe)!;
                    string target=Native.EnumName(Native.Get(data,"type")!);all[target]=data;edges.Add((pair.Key,target));
                }
            var slots=TechnologyIntelligence.Slots(all.Keys,edges,original);
            var result=new Dictionary<string,Node[]>(StringComparer.Ordinal);
            foreach(var tree in trees)
                result.Add(tree.Key,tree.Value.Where(n=>Native.Int(Native.Get(n,"cost"))>0).Select(n=>
                {string id=Native.EnumName(Native.Get(n,"type")!);return new Node(n,id,slots[id],Native.Int(Native.Get(n,"cost")));}).GroupBy(n=>n.Id).Select(g=>g.First()).ToArray());
            return Catalog=new(gld,result,slots);
        }
        internal static Research? Capture(object state,byte viewer)
        {
            if(!Ready(state))return null;var player=Player(state,viewer);if(player is null||!(bool)Native.Invoke(Native.M("PlayerExtensions","IsAlive","PlayerState","GameState"),null,player,state)!)return null;
            if(!Has(player,TechTree.IntelligenceId)&&!Has(player,TechTree.CounterintelligenceId))return null;
            var registry=RegistryFor(state);
            Node[] Tree(object p)=>registry.Trees.TryGetValue(Native.EnumName(Native.Get(p,"tribe")!),out var tree)?tree:throw new InvalidOperationException("Missing effective tribe research tree");
            string[] Known(object p)=>Tree(p).Where(n=>Has(p,n.Id)).Select(n=>n.Slot).Distinct(StringComparer.Ordinal).ToArray();
            var candidates=Bounded(Native.Get(state,"PlayerStates")!,254,"players");
            var opponents=new List<ResearchOpponent>();var owners=new Dictionary<byte,object>();
            foreach(var other in candidates)
            {
                byte id=Convert.ToByte(Native.Get(other,"Id"));if(id is 0 or 255||id==viewer)continue;
                if(!(bool)Native.Invoke(Native.M("PlayerExtensions","KnowsPlayer","PlayerState","System.Byte"),null,player,id)!||
                    !(bool)Native.Invoke(Native.M("PlayerExtensions","IsAlive","PlayerState","GameState"),null,other,state)!)continue;
                owners.Add(id,other);opponents.Add(new(id,true,true,Known(other)));
            }
            var projection=TechnologyIntelligence.Project(viewer,Tree(player).Select(n=>new ResearchTechnology(n.Id,n.Slot,n.Tier)),Known(player),opponents,Has(player,TechTree.CounterintelligenceId));
            return new(state,player,projection,owners);
        }
        internal static int Total(object state,byte player)=>Capture(state,player)?.Projection.Income??0;
        public static void Income(object __0,byte __1,bool __runOriginal,ref int __result)
        {
            if(!__runOriginal)return;try{__result=checked(__result+Total(__0,__1));}catch(Exception e){Error(e);}
        }
        public static void BeforeTurn(object __instance,object __0,out Turn __state)
        {
            __state=new(){Generation=Generation};
            try
            {
                if(!Ready(__0))return;byte actor=Convert.ToByte(Native.Get(__instance,"PlayerId"));
                if(actor!=Native.Int(Native.Get(__0,"CurrentPlayer")))return;
                var player=Player(__0,actor);if(player is null)return;
                int income=Total(__0,actor);if(income<=0)return;
                var stack=Native.Get(__0,"ActionStack")!;
                __state.State=__0;__state.Map=Native.Get(__0,"Map");__state.Player=player;__state.Stack=stack;__state.Action=__instance;
                __state.Actor=actor;__state.Number=Convert.ToUInt32(Native.Get(__0,"CurrentTurn"));__state.Command=Native.Int(Native.Get(__0,"CurrentCommand"));
                __state.Before=Bounded(stack,65536,"turn actions");__state.Income=income;
            }catch(Exception e){__state.State=null;Error(e);}
        }
        public static void AfterTurn(object __instance,object __0,bool __runOriginal,Turn __state)
        {
            var s=__state;if(!__runOriginal||s.State is null||s.Added is not null)return;
            // StartTurn queues native city and diplomacy payments. Add one
            // native currency action to that same serialized queue: no second
            // turn ledger, hidden transfer, or direct Currency write.
            if(s.Generation!=Generation||!Ready(__0)||!Native.Same(s.State,__0)||!Native.Same(s.Action,__instance)||!Native.Same(s.Map,Native.Get(__0,"Map"))||
                !Native.Same(s.Player,Player(__0,s.Actor))||s.Actor!=Native.Int(Native.Get(__0,"CurrentPlayer"))||
                s.Number!=Convert.ToUInt32(Native.Get(__0,"CurrentTurn"))||s.Command!=Native.Int(Native.Get(__0,"CurrentCommand")))
                throw new InvalidOperationException("Trade Secrets turn context changed during native processing");
            var stack=Native.Get(__0,"ActionStack")!;var after=Bounded(stack,65536,"turn actions");
            if(!Native.Same(stack,s.Stack)||after.Length>=65536)
                throw new InvalidOperationException("Trade Secrets turn queue changed unexpectedly");
            int cursor=0;
            foreach(var old in s.Before)
            {
                if(cursor<after.Length&&Native.Same(old,after[cursor])){cursor++;continue;}
                // The supported PolyMod preview normalizes resource spawns by
                // removing CreateResource (50) entries from this exact queue.
                // Permit that documented deletion, in either postfix order;
                // every other original action retains identity and order.
                if(Native.Int(Native.Invoke(Native.M("ActionBase","GetActionType"),old))!=50)
                    throw new InvalidOperationException("Trade Secrets native turn prefix changed unexpectedly");
            }
            var at=Native.Invoke(Native.M("PlayerExtensions","GetCurrentCapitalCoordinates","PlayerState","GameState"),null,s.Player,__0)!;
            var action=Activator.CreateInstance(Native.T("IncreaseCurrencyAction"),new object[]{s.Actor,at,s.Income,10})!;
            s.Added=action;s.AfterStack=stack;Native.Invoke(Native.M(stack.GetType(),"Add","ActionBase"),stack,action);
        }
        public static Exception? FinishedTurn(Exception? __exception,Turn? __state)
        {
            if(__exception is not null)
            {
                // If a later postfix aborts, remove only our own still-queued
                // child. Never replace or rewind another system's queue.
                if(__state is Turn s&&s.Added is not null&&s.AfterStack is not null&&s.State is not null&&Native.Same(s.AfterStack,Native.Get(s.State,"ActionStack")))
                    try{Native.Invoke(Native.M(s.AfterStack.GetType(),"Remove","ActionBase"),s.AfterStack,s.Added);}catch(Exception e){Error(e);}
                Error(__exception);
            }
            return __exception;
        }
    }
}
