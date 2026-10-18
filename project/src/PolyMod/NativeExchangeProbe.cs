using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Deception.Core;
using Buffer=Deception.PolyModAdapter.NativeStateProbe.Buffer;
using Report=Deception.PolyModAdapter.NativeStateProbe.Report;

namespace Deception.PolyModAdapter
{
    // Opt-in detached diagnostic only. Every edited body, tile and player must
    // belong to a freshly deserialized private state. Never adopt a state into
    // a client, dispatch commands, or touch a saved game from this probe.
    internal static class NativeExchangeProbe
    {
        const string Reader="Il2CppSystem.IO.BinaryReader",Writer="Il2CppSystem.IO.BinaryWriter";
        const string Improvement="Polytopia.Data.ImprovementData+Type";
        static readonly Guid Epoch=new("92fa434c-4574-47ae-9c5f-c7f4103bef11");
        static readonly HashSet<string> Forms=new(StringComparer.Ordinal){"warrior","rider"};
        [ThreadStatic] static bool Running;
        static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
        static void Check(Report report,bool condition,string name){Require(condition,name);report.Checks.Add(name);}
        static void Measure(Report report,string name,object? value)=>report.Measurements[name]=Convert.ToString(value,CultureInfo.InvariantCulture)??"null";
        static string Hash(byte[] value)=>Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
        static object New(string type)=>Native.T(type).GetConstructor(Type.EmptyTypes)?.Invoke(Array.Empty<object>())
            ??throw new MissingMethodException(type,".ctor()");
        static byte[] Serialized(object state,int version)=>NativeStateProbe.Serialize(state,"GameState",version);
        static NativeExchangePosition Position(object coordinates)=>new(Native.Int(Native.Get(coordinates,"X")),Native.Int(Native.Get(coordinates,"Y")));
        static object Player(object state,byte actor)
        {
            object?[] args={actor,null};
            Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&args[1] is not null,
                "Detached exchange player is absent.");return args[1]!;
        }
        static object[] Tiles(object state)
        {
            var tiles=Native.Get(Native.Get(state,"Map")!,"Tiles")!;int count=Native.Int(Native.Get(tiles,"Length"));
            Require(count is >0 and <=4096,"Detached exchange map is outside its bound.");
            var item=Native.M(tiles.GetType(),"get_Item","System.Int32");
            return Enumerable.Range(0,count).Select(i=>Native.Invoke(item,tiles,i)??throw new InvalidDataException("Null native tile.")).ToArray();
        }

        sealed class Trial
        {
            internal object Source=null!;
            internal int Version;
            internal byte[] SourceNative=Array.Empty<byte>();
            internal byte[]? SourcePayload;
            internal long? SourceRevision;
            internal readonly List<object> States=new();
            internal readonly Dictionary<long,object> Carriers=new();
            internal readonly HashSet<long> SourceObjects=new();
            readonly HashSet<long> PrivateObjects=new();

            internal IEnumerable<object> MutationRoots(object state)
            {
                yield return state;
                if(Native.Get(state,"Settings") is object settings)
                {yield return settings;if(Native.Get(settings,"rules") is object rules)yield return rules;}
                if(Native.Get(state,"pendingCommandTriggers") is object triggers)yield return triggers;
                var map=Native.Get(state,"Map")!;yield return map;yield return Native.Get(map,"Tiles")!;
                foreach(var tile in Tiles(state))
                {
                    yield return tile;
                    if(Native.Get(tile,"explorers") is object explorers)yield return explorers;
                }
                var players=Native.Get(state,"PlayerStates")!;yield return players;
                Require(Native.Int(Native.Get(players,"Count")) is >0 and <=16,"Detached exchange player roster is outside its bound.");
                foreach(var player in Native.Items(players))
                {
                    yield return player;
                    foreach(string name in new[]{"availableTech","relations","unlockedTechCache"})
                        if(Native.Get(player,name) is object value)yield return value;
                }
                foreach(var body in NativeLedgerState.Graph(state).Values)
                {
                    yield return body.Unit;
                    if(Native.Get(body.Unit,"effects") is object effects)yield return effects;
                }
                foreach(string name in new[]{"CommandStack","ActionStack"})
                {
                    var list=Native.Get(state,name);if(list is null)continue;yield return list;
                    Require(Native.Int(Native.Get(list,"Count"))<=16384,"Native exchange history exceeds its bound.");
                    foreach(var value in Native.Items(list))yield return value;
                }
            }
            internal void CaptureSource()
            {
                foreach(var value in MutationRoots(Source))SourceObjects.Add(Native.Pointer(value));
                var snapshot=NativeGameStateEnvelope.Get(Source);SourcePayload=snapshot?.Bytes();SourceRevision=snapshot?.Revision;
                SourceNative=NativeGameStateEnvelope.NativeBytes(Source,Version);
            }
            internal object OwnState(object state)
            {
                Require(Native.T("GameState").IsInstanceOfType(state)&&!SourceObjects.Contains(Native.Pointer(state)),"Detached exchange state aliases source.");
                if(!States.Any(x=>Native.Same(x,state)))States.Add(state);return state;
            }
            internal object OwnCarrier(object value)
            {
                long pointer=Native.Pointer(value);Require(!SourceObjects.Contains(pointer),"Detached exchange carrier aliases source history.");
                Carriers.TryAdd(pointer,value);return value;
            }
            internal object OwnMutable(object value)
            {
                long pointer=Native.Pointer(value);Require(!SourceObjects.Contains(pointer),"New private mutable object aliases the source.");
                PrivateObjects.Add(pointer);return value;
            }
            internal void Collect(object state)
            {
                foreach(string name in new[]{"CommandStack","ActionStack"})
                {
                    var list=Native.Get(state,name);if(list is null)continue;
                    Require(Native.Int(Native.Get(list,"Count"))<=16384,"Private exchange history exceeds its bound.");
                    foreach(var value in Native.Items(list))OwnCarrier(value);
                }
            }
            internal void RegisterPrivateGraph(object state)
            {
                foreach(var value in MutationRoots(state))
                {
                    long pointer=Native.Pointer(value);Require(!SourceObjects.Contains(pointer),"A mutable detached graph object aliases the source.");
                    PrivateObjects.Add(pointer);
                }
                Collect(state);
            }
            internal object Detached(byte[] bytes)
            {
                object state=OwnState(New("GameState"));
                using var buffer=new Buffer(bytes);
                try
                {
                    Native.Invoke(Native.M("GameState","Deserialize",Reader,"System.Int32"),state,buffer.R,Version);
                    Require(buffer.Position==buffer.Length,"Detached exchange state did not consume its exact record.");
                    RegisterPrivateGraph(state);return state;
                }
                finally{Collect(state);}
            }
            internal void Set(object value,string property,object? replacement)
            {
                long pointer=Native.Pointer(value);
                Require(PrivateObjects.Contains(pointer)&&!SourceObjects.Contains(pointer),"Probe attempted to edit an unowned native object.");
                Native.Set(value,property,replacement);
            }
            internal void VerifySource(Report report)
            {
                var snapshot=NativeGameStateEnvelope.Get(Source);
                Check(report,NativeGameStateEnvelope.NativeBytes(Source,Version).SequenceEqual(SourceNative)&&
                    (SourcePayload is null?snapshot is null:snapshot is not null&&snapshot.Revision==SourceRevision&&snapshot.Bytes().SequenceEqual(SourcePayload)),
                    "supplied-source-native-bytes-and-private-binding-unchanged");
                Measure(report,"source-native-bytes",SourceNative.Length);Measure(report,"source-native-sha256",Hash(SourceNative));
            }
            internal void Cleanup(Report report)
            {
                int retired=0;
                foreach(var state in States)
                {
                    try{Collect(state);NativeGameStateEnvelope.Forget(state);Require(NativeGameStateEnvelope.Get(state) is null,"Detached exchange binding survived retirement.");retired++;}
                    catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Private state cleanup: "+e.Message;}
                }
                foreach(var value in Carriers.Values)
                {
                    try{NativeExchangeCommands.Forget(value);MimicSmuggleActions.ForgetOffer(value);}
                    catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Private carrier cleanup: "+e.Message;}
                }
                Measure(report,"private-states-tracked",States.Count);Measure(report,"private-states-retired",retired);
                Measure(report,"private-carriers-retired",Carriers.Count);
            }
        }

        sealed record UnitBefore(object Unit,object Coordinates,object Type,object Data,object Effects,object[] EffectValues,
            bool Moved,bool Attacked,ushort Health,uint Id)
        {
            internal static UnitBefore Capture(object unit)=>new(unit,Native.Get(unit,"coordinates")!,Native.Get(unit,"type")!,
                Native.Get(unit,"UnitData")!,Native.Get(unit,"effects")!,Native.Items(Native.Get(unit,"effects")!).ToArray(),
                (bool)Native.Get(unit,"moved")!,(bool)Native.Get(unit,"attacked")!,Convert.ToUInt16(Native.Get(unit,"health")),Convert.ToUInt32(Native.Get(unit,"id")));
        }
        sealed record Setup(object State,object Player,object A,object B,object TileA,object TileB,NativeIdentityLedger Ledger,
            NativeExchangeIntent Intent,LedgerAppearance SourceAppearance,UnitBefore BeforeA,UnitBefore BeforeB,byte[] NativeBefore,byte[] PayloadBefore,string Label)
        {internal SightBefore? Sight {get;init;}}
        sealed record ExplorerBefore(object Tile,object? List,byte[] Values);
        sealed record SightBefore(object Stack,object[] Prefix,byte[][] PrefixBytes,bool Dirty,
            NativeExchangePosition[] A,NativeExchangePosition[] B,ExplorerBefore[] Explorers,NativeExchangePosition? Sentinel,bool Discovery);
        static string Form(object unit,out bool mimic)
        {
            string type=Native.EnumName(Native.Get(unit,"type")!);mimic=RecruitmentHooks.Forms.TryGetValue(type,out var form);return mimic?form!.NativeId:type;
        }
        static bool IsMimic(object unit){_=Form(unit,out bool mimic);return mimic;}
        static object Tile(object state,object coordinates)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,
            Native.Get(state,"Map"),coordinates)??throw new InvalidDataException("Private participant tile is missing.");
        static bool Adjacent(object a,object b)=>(bool)Native.Invoke(Native.M("WorldCoordinates","IsAdjacent","WorldCoordinates","WorldCoordinates"),null,
            Native.Get(a,"coordinates"),Native.Get(b,"coordinates"))!;
        static object[] Footprint(object state,object tile)
        {
            int radius=Native.Int(Native.Get(tile,"terrain"))==4?2:1;
            object area=Native.Invoke(Native.M("MapDataExtensions","GetAreaSorted","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),
                null,Native.Get(state,"Map"),Native.Get(tile,"coordinates"),radius,true,true)!;
            int count=Native.Int(Native.Get(area,"Length"));Require(count is >0 and <=25,"Private sight footprint exceeds its bound.");
            var item=Native.M(area.GetType(),"get_Item","System.Int32");
            return Enumerable.Range(0,count).Select(i=>Native.Invoke(item,area,i)??throw new InvalidDataException("Null private sight tile.")).ToArray();
        }
        static bool SafeDiscovery(object state,object tile)=>Native.Get(tile,"unit") is null&&
            !(bool)Native.Invoke(Native.M("TileData","HasImprovement",Improvement),tile,Native.EnumValue(Improvement,"city"))!&&
            !(bool)Native.Invoke(Native.M("ExploreLightHouseTask","EvaluateTile","TileData","GameState"),null,tile,state)!;
        static bool DiscoveryGeometry(object state,object tileA,object tileB,bool swap,bool execution=false)
        {
            var a=Footprint(state,swap?tileB:tileA).Select(Native.Pointer).ToHashSet();
            var b=Footprint(state,swap?tileA:tileB).Select(Native.Pointer).ToHashSet();
            long first=Native.Pointer(tileA),second=Native.Pointer(tileB);
            var safe=execution?Footprint(state,tileA).Concat(Footprint(state,tileB)).Where(t=>SafeDiscovery(state,t)).Select(Native.Pointer).ToHashSet():a.Union(b).ToHashSet();
            return a.Except(b).Any(safe.Contains)&&b.Except(a).Count(safe.Contains)>=2&&a.Intersect(b).Any(p=>p!=first&&p!=second&&safe.Contains(p))&&
                Tiles(state).Any(t=>!a.Contains(Native.Pointer(t))&&!b.Contains(Native.Pointer(t)));
        }
        static byte[] Explorers(object tile)
        {
            object? list=Native.Get(tile,"explorers");if(list is null)return Array.Empty<byte>(); // Native GetExplored treats null as unexplored.
            int count=Native.Int(Native.Get(list,"Count"));Require(count is >=0 and <=256,"Native explorer list is outside its bound.");
            var result=Native.Items(list).Select(x=>Convert.ToByte(x)).ToArray();Require(result.Length==count,"Native explorer list contains a null entry.");return result;
        }
        static object ByteList(Trial trial,IEnumerable<byte> values)
        {
            var type=Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(typeof(byte));
            object list=trial.OwnMutable(Activator.CreateInstance(type)??throw new InvalidOperationException("Cannot allocate private explorer list."));
            var add=Native.M(type,"Add","System.Byte");foreach(byte value in values)Native.Invoke(add,list,value);return list;
        }
        static object[] Queue(object state)
        {
            object list=Native.Get(state,"ActionStack")??throw new InvalidDataException("Native action stack is absent.");
            int count=Native.Int(Native.Get(list,"Count"));Require(count is >=0 and <=16434,"Native sight queue exceeds its bound.");
            object[] actions=Native.Items(list).ToArray();Require(actions.Length==count,"Native queue contains a null action.");return actions;
        }
        static byte[] ActionBytes(object action,int version)=>NativeStateProbe.Serialize(action,"ActionBase",version);
        static bool PrefixMatches(object[] actual,object[] expected)=>actual.Length==expected.Length&&
            actual.Zip(expected,(a,b)=>Native.Same(a,b)).All(x=>x);
        static Setup ConfigureSight(Report report,Trial trial,Setup setup,bool discovery,bool dirty,bool execution=false)
        {
            object state=setup.State;byte actor=setup.Intent.Actor;Require(actor!=byte.MaxValue,"Diagnostic sight needs an ordinary player.");
            var a=Footprint(state,setup.Intent.Swap?setup.TileB:setup.TileA);
            var b=Footprint(state,setup.Intent.Swap?setup.TileA:setup.TileB);
            var ap=a.Select(Native.Pointer).ToHashSet();var bp=b.Select(Native.Pointer).ToHashSet();
            var omitted=new HashSet<long>();object? sentinel=null;
            if(discovery)
            {
                bool Safe(object tile)=>!execution||SafeDiscovery(state,tile);
                object onlyA=a.First(t=>!bp.Contains(Native.Pointer(t))&&Safe(t));
                object[] onlyB=b.Where(t=>!ap.Contains(Native.Pointer(t))&&Safe(t)).Take(2).ToArray();Require(onlyB.Length==2,"Need asymmetric B footprint.");
                object overlap=a.First(t=>bp.Contains(Native.Pointer(t))&&!Native.Same(t,setup.TileA)&&!Native.Same(t,setup.TileB)&&Safe(t));
                sentinel=Tiles(state).First(t=>!ap.Contains(Native.Pointer(t))&&!bp.Contains(Native.Pointer(t)));
                foreach(object tile in new[]{onlyA,onlyB[0],onlyB[1],overlap,sentinel})omitted.Add(Native.Pointer(tile));
            }
            // Explicit fixture preparation on private tiles only. The source is
            // never normalized. Preserve all other players and explorer order.
            foreach(object tile in a.Concat(b).Concat(sentinel is null?Array.Empty<object>():new[]{sentinel}).GroupBy(Native.Pointer).Select(g=>g.First()))
            {
                byte[] before=Explorers(tile);bool hide=omitted.Contains(Native.Pointer(tile));
                var after=before.Where(x=>x!=actor).ToList();if(!hide)after.Add(actor);
                trial.Set(tile,"explorers",ByteList(trial,after));
                Require(Explorers(tile).Where(x=>x!=actor).SequenceEqual(before.Where(x=>x!=actor)),"Private sight setup changed another explorer.");
            }
            if(execution)
            {
                // A separate isolated fixture, never inherited command history.
                object deferred=trial.OwnCarrier(Native.T("RunDeferredAction").GetConstructor(new[]{typeof(byte)})!.Invoke(new object[]{actor}));
                trial.Set(state,"ActionStack",trial.OwnMutable(Native.NewList("ActionBase",new[]{deferred})));
            }
            else if(discovery)
            {
                object Ordinary(string type,Type[] types,params object[] args)=>trial.OwnCarrier(
                    Native.T(type).GetConstructor(types)!.Invoke(args));
                object end=Ordinary("EndCommandAction",new[]{typeof(byte),Native.T("CommandType")},actor,Enum.Parse(Native.T("CommandType"),"Build"));
                object deferred=Ordinary("RunDeferredAction",new[]{typeof(byte)},actor);
                object explore=Ordinary("ExploreAction",new[]{typeof(byte),Native.T("WorldCoordinates")},actor,Native.Get(setup.TileA,"coordinates")!);
                var prefix=Queue(state).Concat(new[]{end,deferred,explore}).ToArray();
                trial.Set(state,"ActionStack",trial.OwnMutable(Native.NewList("ActionBase",prefix)));
            }
            trial.Set(state,"HasFlaggedNeedForUpdateRoutes",dirty);
            NativeExchangePosition[] Expected(object[] area)=>area.Reverse().Where(t=>!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),t,actor)!)
                .Select(t=>Position(Native.Get(t,"coordinates")!)).ToArray();
            var expectedA=Expected(a);var expectedB=Expected(b);
            Require(discovery?expectedA.Length==2&&expectedB.Length==3:expectedA.Length==0&&expectedB.Length==0,"Unexpected private sight setup counts.");
            object stack=Native.Get(state,"ActionStack")!;var actions=Queue(state);
            var sight=new SightBefore(stack,actions,actions.Select(x=>ActionBytes(x,trial.Version)).ToArray(),dirty,expectedA,expectedB,
                Tiles(state).Select(t=>new ExplorerBefore(t,Native.Get(t,"explorers"),Explorers(t))).ToArray(),sentinel is null?null:Position(Native.Get(sentinel,"coordinates")!),discovery);
            Measure(report,setup.Label+"-expected-sight-A",string.Join(";",expectedA));Measure(report,setup.Label+"-expected-sight-B",string.Join(";",expectedB));
            Measure(report,setup.Label+"-original-queue-count",actions.Length);Measure(report,setup.Label+"-original-route-dirty",dirty);
            return setup with{Sight=sight,NativeBefore=NativeGameStateEnvelope.NativeBytes(state,trial.Version)};
        }
        static bool Supported(NativeLedgerState.Body body,byte actor)
        {
            var unit=body.Unit;
            return body.Root&&Convert.ToByte(Native.Get(unit,"owner"))==actor&&Native.Int(Native.Get(unit,"health"))>0&&
                Native.Get(unit,"passengerUnit") is null&&!(bool)Native.Invoke(Native.M("UnitState","HasLeader"),unit)!&&
                !(bool)Native.Invoke(Native.M("UnitState","HasFollower"),unit)!&&Native.Int(Native.Get(unit,"promotionLevel"))==0&&
                Native.Int(Native.Get(Native.Get(unit,"effects")!,"Count"))==0&&Forms.Contains(Form(unit,out _));
        }
        static void TraceSourceCandidates(Report report,Trial trial)
        {
            void Registry(string group,string type,string id)
            {
                try{Measure(report,"registry-"+group+"-"+id,Native.Int(Native.EnumValue(type,id)));}
                catch(Exception e){Measure(report,"registry-"+group+"-"+id,"unresolved: "+e.GetType().Name+": "+e.Message);}
            }
            foreach(string id in RecruitmentHooks.Forms.Values.Select(x=>x.VariantId).Append(RecruitmentCatalog.MenuId).OrderBy(x=>x,StringComparer.Ordinal))
                Registry("unit",RecruitmentHooks.UT,id);
            foreach(string id in new[]{"warrior","rider"})Registry("unit",RecruitmentHooks.UT,id);
            foreach(string id in new[]{TechTree.DeceptionId,"dcp_misdirection","dcp_camouflage"})Registry("tech",RecruitmentHooks.TT,id);
            foreach(string id in SmuggleCatalog.Harvests.Select(x=>x.Id).Concat(new[]{NativeExchangeCatalog.Id(NativeExchangeKind.Regroup),NativeExchangeCatalog.Id(NativeExchangeKind.Disguise)}))
                Registry("improvement",Improvement,id);
            byte actor=Convert.ToByte(Native.Get(trial.Source,"CurrentPlayer"));var graph=NativeLedgerState.Graph(trial.Source);
            Measure(report,"source-current-player",actor);Measure(report,"source-candidate-count",graph.Count);
            int supported=0,mimics=0,genuine=0;
            foreach(var pair in graph.OrderBy(x=>x.Key))
            {
                string prefix="source-unit-"+pair.Key;var unit=pair.Value.Unit;var rejected=new List<string>();
                try
                {
                    Measure(report,prefix+"-root",pair.Value.Root);if(!pair.Value.Root)rejected.Add("not an independent map root");
                    int owner=Native.Int(Native.Get(unit,"owner"));Measure(report,prefix+"-owner",owner);
                    if(owner!=actor)rejected.Add("owner "+owner+" differs from CurrentPlayer "+actor);
                    object type=Native.Get(unit,"type")!;Measure(report,prefix+"-native-type-number",Native.Int(type));
                    Measure(report,prefix+"-native-type-name",Native.EnumName(type));
                    string form=Form(unit,out bool mimic);Measure(report,prefix+"-registered-Mimic",mimic);Measure(report,prefix+"-resolved-form",form);
                    if(!Forms.Contains(form))rejected.Add("unsupported resolved form "+form);
                    int health=Native.Int(Native.Get(unit,"health"));Measure(report,prefix+"-health",health);if(health<=0)rejected.Add("health is not positive");
                    int promotion=Native.Int(Native.Get(unit,"promotionLevel"));Measure(report,prefix+"-promotion-level",promotion);
                    if(promotion!=0)rejected.Add("promotion level "+promotion+" requires a later snapshot adapter");
                    bool passenger=Native.Get(unit,"passengerUnit") is not null,leader=(bool)Native.Invoke(Native.M("UnitState","HasLeader"),unit)!,
                        follower=(bool)Native.Invoke(Native.M("UnitState","HasFollower"),unit)!;
                    Measure(report,prefix+"-has-passenger",passenger);Measure(report,prefix+"-has-leader",leader);Measure(report,prefix+"-has-follower",follower);
                    if(passenger)rejected.Add("has a passenger");if(leader)rejected.Add("has a leader");if(follower)rejected.Add("has a follower");
                    var effects=Native.Get(unit,"effects");Require(effects is not null,"Native effects list is absent.");
                    int effectCount=Native.Int(Native.Get(effects!,"Count"));Measure(report,prefix+"-effects-count",effectCount);
                    Measure(report,prefix+"-effects",effectCount is >=0 and <=256?
                        string.Join(", ",Native.Items(effects!).Select(x=>x+"("+Native.Int(x)+")")):"outside diagnostic enumeration bound");
                    if(effectCount!=0)rejected.Add("effects count "+effectCount+" requires a later source-capture adapter");
                    Measure(report,prefix+"-moved",Native.Get(unit,"moved"));Measure(report,prefix+"-attacked",Native.Get(unit,"attacked"));
                    var data=Native.Get(unit,"UnitData");
                    if(data is not null)
                    {
                        object dataType=Native.Get(data,"type")!;Measure(report,prefix+"-cached-profile-type-number",Native.Int(dataType));
                        Measure(report,prefix+"-cached-profile-type-name",Native.EnumName(dataType));Measure(report,prefix+"-cached-profile-health",Native.Get(data,"health"));
                    }
                    else Measure(report,prefix+"-cached-profile","absent");
                    bool accepted=Supported(pair.Value,actor);Measure(report,prefix+"-supported",accepted);
                    if(accepted){supported++;if(mimic)mimics++;else genuine++;}
                }
                catch(Exception e){rejected.Add("diagnostic read failed: "+e.GetType().Name+": "+e.Message);Measure(report,prefix+"-supported",false);}
                Measure(report,prefix+"-rejections",rejected.Count==0?"none":string.Join("; ",rejected));
            }
            Measure(report,"source-supported-count",supported);Measure(report,"source-supported-Mimics",mimics);Measure(report,"source-supported-genuine",genuine);
        }
        static Setup Prepare(Report report,Trial trial,bool mimicPartner,NativeExchangeKind kind,bool swap,bool discovery=false,bool dirty=false,bool execution=false)
        {
            object state=trial.Detached(trial.SourceNative);byte actor=Convert.ToByte(Native.Get(state,"CurrentPlayer"));object player=Player(state,actor);
            Require(Equals(Native.Get(state,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started")),"Detached exchange needs a started native state.");
            // These are registered real diagnostic technologies. Only replace
            // the private player's researched-ID list; never change the GLD.
            var tech=Native.Items(Native.Get(player,"availableTech")!).ToList();
            foreach(string id in new[]{"dcp_misdirection","dcp_camouflage"})
            {
                object value=Native.EnumValue(RecruitmentHooks.TT,id);if(!tech.Any(x=>Equals(x,value)))tech.Add(value);
            }
            trial.Set(player,"availableTech",Native.NewList(RecruitmentHooks.TT,tech));
            var graph=NativeLedgerState.Graph(state);var candidates=graph.Values.Where(x=>Supported(x,actor)).ToArray();
            string label=(mimicPartner?"mimic":"genuine")+"-"+kind.ToString().ToLowerInvariant()+"-"+(swap?"swap":"keep");
            if(discovery)label+="-discovery-dirty-"+(dirty?"true":"false");
            if(execution)label+="-child-execution";
            int mimicCount=candidates.Count(x=>IsMimic(x.Unit)),genuineCount=candidates.Length-mimicCount;
            Measure(report,label+"-setup-current-player",actor);Measure(report,label+"-setup-native-body-count",graph.Count);
            Measure(report,label+"-setup-supported-Mimics",mimicCount);Measure(report,label+"-setup-supported-genuine",genuineCount);
            var pair=(from a in candidates where IsMimic(a.Unit)
                from b in candidates where !Native.Same(a.Unit,b.Unit)&&IsMimic(b.Unit)==mimicPartner
                orderby Form(a.Unit,out _)==Form(b.Unit,out _) ? 1 : 0, Convert.ToUInt32(Native.Get(a.Unit,"id")),Convert.ToUInt32(Native.Get(b.Unit,"id"))
                select (A:a,B:b)).FirstOrDefault();
            Require(pair.A is not null&&pair.B is not null,"Detached "+label+" participant selection failed: CurrentPlayer="+actor+
                "; native bodies="+graph.Count+"; supported="+candidates.Length+" (Mimics="+mimicCount+", genuine="+genuineCount+"). Requires "+
                (mimicPartner?"two distinct supported own Mimics":"one supported own Mimic and one supported own genuine Warrior/Rider")+
                ". See source-unit numeric type mappings, native flags and exact rejection measurements; no eligibility rule was relaxed.");
            object unitA=pair.A!.Unit,unitB=pair.B!.Unit;
            // Choose already explored ordinary land with no city exception and
            // no third occupant. This setup performs no movement or placement
            // routine; the native tile setter is only its pointer assignment.
            object[] tiles=Tiles(state).Where(tile=>(bool)Native.Get(tile,"IsLand")!&&
                Native.EnumName(Native.Get(tile,"terrain")!)!="mountain"&&
                (bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,actor)!&&
                !(bool)Native.Invoke(Native.M("TileData","HasImprovement",Improvement),tile,Native.EnumValue(Improvement,"city"))!&&
                (Native.Get(tile,"unit") is null||Native.Same(Native.Get(tile,"unit"),unitA)||Native.Same(Native.Get(tile,"unit"),unitB))).ToArray();
            var positions=(from a in tiles from b in tiles where !Native.Same(a,b)&&Adjacent(a,b)&&
                (!discovery||DiscoveryGeometry(state,a,b,swap,execution)) select (A:a,B:b)).FirstOrDefault();
            Require(positions.A is not null&&positions.B is not null,"No adjacent explored empty ordinary land pair is available for detached setup.");
            var oldA=Tile(state,Native.Get(unitA,"coordinates")!);var oldB=Tile(state,Native.Get(unitB,"coordinates")!);
            Require(Native.Same(Native.Get(oldA,"unit"),unitA)&&Native.Same(Native.Get(oldB,"unit"),unitB),"Detached initial occupancy is inconsistent.");
            trial.Set(oldA,"unit",null);trial.Set(oldB,"unit",null);
            trial.Set(positions.A!,"unit",unitA);trial.Set(positions.B!,"unit",unitB);
            trial.Set(unitA,"coordinates",Native.Get(positions.A!,"coordinates"));trial.Set(unitB,"coordinates",Native.Get(positions.B!,"coordinates"));
            trial.Set(unitA,"moved",true);trial.Set(unitA,"attacked",false);
            trial.Set(unitB,"moved",swap);trial.Set(unitB,"attacked",kind==NativeExchangeKind.Disguise);
            if(!mimicPartner)trial.Set(unitB,"health",(ushort)50);
            var ledger=NativeLedgerState.Bootstrap(state,Epoch);
            // Synthetic diagnostic payment facts exercise ledger preservation;
            // this does not establish a real paid recruitment event.
            uint initiatorId=Convert.ToUInt32(Native.Get(unitA,"id"));
            ledger=ledger with{Units=ledger.Units.Select(row=>row with{
                InvestmentKind=row.NativeId==initiatorId?PaidInvestmentKind.Known:PaidInvestmentKind.UnknownLegacy,
                PaidStars=row.NativeId==initiatorId?3:0,
                Appearance=row.Appearance! with{Health=(ushort)(row.NativeId==initiatorId?70:40)}}).ToArray()};
            byte[] payload=NativeIdentityLedgerCodec.Encode(ledger,0);NativeGameStateEnvelope.Set(state,0,payload);
            // A frozen recipient is legal; only copying a genuine source with
            // unsupported appearance effects is outside the first capture slice.
            if(kind==NativeExchangeKind.Disguise)
                trial.Set(unitB,"effects",Native.NewList("UnitEffect",new[]{Enum.Parse(Native.T("UnitEffect"),"Frozen")}));
            uint idA=Convert.ToUInt32(Native.Get(unitA,"id")),idB=Convert.ToUInt32(Native.Get(unitB,"id"));
            var aRow=ledger.Units.Single(row=>row.NativeId==idA);var bRow=ledger.Units.SingleOrDefault(row=>row.NativeId==idB);
            var source=kind==NativeExchangeKind.Disguise?aRow.Appearance!:
                bRow?.Appearance??NativeLedgerState.Capture(state,pair.B!,Form(unitB,out _),false);
            var atA=Position(Native.Get(unitA,"coordinates")!);var atB=Position(Native.Get(unitB,"coordinates")!);
            var intent=new NativeExchangeIntent(Epoch,ledger.NextSequence,0,Convert.ToUInt32(Native.Get(state,"CurrentTurn")),actor,kind,swap,
                bRow is null?NativePartnerReference.Allocate:NativePartnerReference.Existing,ledger.NextLogicalId,
                new(aRow.LogicalId,idA,atA.X,atA.Y),new(bRow?.LogicalId??ledger.NextLogicalId,idB,atB.X,atB.Y),NativeExchangeIntentCodec.AppearanceFingerprint(source));
            NativeLedgerState.Validate(state,0,payload);
            var setup=new Setup(state,player,unitA,unitB,positions.A!,positions.B!,ledger,intent,source,UnitBefore.Capture(unitA),UnitBefore.Capture(unitB),
                NativeGameStateEnvelope.NativeBytes(state,trial.Version),payload,label);
            return ConfigureSight(report,trial,setup,discovery,dirty,execution);
        }

        static void RejectedUnchanged(Report report,Trial trial,Setup setup,NativeExchangeIntent intent,string label,string? fault=null)
        {
            byte[] native=NativeGameStateEnvelope.NativeBytes(setup.State,trial.Version);var before=NativeGameStateEnvelope.Get(setup.State)!;
            object stack=Native.Get(setup.State,"ActionStack")!;object[] prefix=Queue(setup.State);
            byte[][] records=prefix.Select(x=>ActionBytes(x,trial.Version)).ToArray();bool dirty=(bool)Native.Get(setup.State,"HasFlaggedNeedForUpdateRoutes")!;
            bool ok;NativeExchangeCommitResult? result;string reason;
            using(NativeExchangeCommit.DetachedTrial(setup.State,trial.Source,fault))
                ok=NativeExchangeCommit.TryCommit(setup.State,intent,out result,out reason);
            var after=NativeGameStateEnvelope.Get(setup.State);
            Measure(report,label+"-reason",reason);
            Check(report,!ok&&result is null&&!string.IsNullOrWhiteSpace(reason),label+"-rejected-without-result");
            if(fault is not null)Check(report,reason=="InvalidOperationException: Injected detached exchange failure after "+fault+".",label+"-reached-exact-injected-stage");
            Check(report,NativeGameStateEnvelope.NativeBytes(setup.State,trial.Version).SequenceEqual(native)&&after is not null&&
                after.Revision==before.Revision&&after.Bytes().SequenceEqual(before.Bytes()),label+"-exact-native-and-ledger-rollback");
            object[] restored=Queue(setup.State);
            Check(report,Native.Same(Native.Get(setup.State,"ActionStack"),stack)&&PrefixMatches(restored,prefix)&&
                restored.Select((x,i)=>ActionBytes(x,trial.Version).SequenceEqual(records[i])).All(x=>x)&&
                (bool)Native.Get(setup.State,"HasFlaggedNeedForUpdateRoutes")! == dirty,label+"-exact-original-queue-reference-records-and-dirty-restored");
        }

        static void Duplicate(Report report,Trial trial,object state,NativeExchangeIntent intent,string label)
        {
            byte[] before=NativeGameStateEnvelope.NativeBytes(state,trial.Version);var payload=NativeGameStateEnvelope.Get(state)!;
            object stack=Native.Get(state,"ActionStack")!;object[] actions=Queue(state);
            bool ok;NativeExchangeCommitResult? result;string reason;
            using(NativeExchangeCommit.DetachedTrial(state,trial.Source))ok=NativeExchangeCommit.TryCommit(state,intent,out result,out reason);
            Measure(report,label+"-duplicate-reason",reason);
            Check(report,ok&&result is not null&&result.AlreadyCommitted&&result.BeforeRevision==payload.Revision&&result.AfterRevision==payload.Revision,
                label+"-receipt-duplicate-is-no-op");
            var after=NativeGameStateEnvelope.Get(state);
            Check(report,NativeGameStateEnvelope.NativeBytes(state,trial.Version).SequenceEqual(before)&&after is not null&&
                after.Revision==payload.Revision&&after.Bytes().SequenceEqual(payload.Bytes()),label+"-duplicate-preserves-exact-native-and-ledger");
            Check(report,Native.Same(Native.Get(state,"ActionStack"),stack)&&PrefixMatches(Queue(state),actions),label+"-duplicate-retains-exact-pending-queue-without-enqueue");
        }

        static void VerifySight(Report report,Trial trial,Setup setup)
        {
            var sight=setup.Sight??throw new InvalidDataException("Missing sight preimage.");object[] actions=Queue(setup.State);
            var expected=sight.A.Concat(sight.B).ToArray();
            Check(report,!Native.Same(Native.Get(setup.State,"ActionStack"),sight.Stack)&&
                PrefixMatches(Native.Items(sight.Stack).ToArray(),sight.Prefix)&&
                PrefixMatches(actions.Take(sight.Prefix.Length).ToArray(),sight.Prefix),setup.Label+"-owned-queue-preserves-original-prefix-reference-and-order");
            Check(report,actions.Length==sight.Prefix.Length+expected.Length&&
                actions.Take(sight.Prefix.Length).Select((x,i)=>ActionBytes(x,trial.Version).SequenceEqual(sight.PrefixBytes[i])).All(x=>x),
                setup.Label+"-exact-pending-prefix-records-and-tail-count");
            var allocated=new HashSet<long>(sight.Prefix.Select(Native.Pointer));
            var positions=new List<NativeExchangePosition>();
            foreach(object action in actions.Skip(sight.Prefix.Length))
            {
                Require(allocated.Add(Native.Pointer(action)),"Sight tail aliases another pending action.");
                var explore=Native.As(action,"ExploreAction");
                Require(Convert.ToByte(Native.Get(explore,"PlayerId"))==setup.Intent.Actor,"Sight action uses another player's identity.");
                positions.Add(Position(Native.Get(explore,"Coordinates")!));trial.OwnCarrier(action);
            }
            Check(report,positions.SequenceEqual(expected),setup.Label+"-native-explore-tail-matches-final-A-then-B-order");
            Check(report,(bool)Native.Get(setup.State,"HasFlaggedNeedForUpdateRoutes")! == sight.Dirty&&
                sight.Explorers.All(x=>(x.List is null?Native.Get(x.Tile,"explorers") is null:Native.Same(Native.Get(x.Tile,"explorers"),x.List))&&
                    Explorers(x.Tile).SequenceEqual(x.Values)),setup.Label+"-all-explorer-memberships-and-route-dirty-unchanged-before-processing");
            if(sight.Discovery)
            {
                var overlap=sight.A.Intersect(sight.B).ToArray();
                Check(report,sight.A.Length==2&&sight.B.Length==3&&overlap.Length==1&&positions.Count(x=>x==overlap[0])==2&&
                    positions.Count==5&&positions.Distinct().Count()==4&&sight.Sentinel.HasValue&&!positions.Contains(sight.Sentinel.Value),
                    setup.Label+"-asymmetric-footprints-retain-distinct-overlap-actions-and-exclude-sentinel");
            }
            else Check(report,positions.Count==0,setup.Label+"-already-explored-footprints-enqueue-no-actions");
            Measure(report,setup.Label+"-observed-sight-tail",string.Join(";",positions));
        }

        static void RestoreAllowedFields(Trial trial,object state,UnitBefore before)
        {
            var unit=NativeLedgerState.Graph(state)[before.Id].Unit;
            trial.Set(unit,"coordinates",before.Coordinates);trial.Set(unit,"type",before.Type);trial.Set(unit,"UnitData",before.Data);
            trial.Set(unit,"moved",before.Moved);trial.Set(unit,"attacked",before.Attacked);
            trial.Set(unit,"effects",Native.NewList("UnitEffect",before.EffectValues));
        }
        static void ProtectedNativeFields(Report report,Trial trial,Setup setup)
        {
            // Normalize only the explicitly allowed transaction fields on a
            // separate copy, then compare the complete native stream. This
            // catches changes to real HP, home/support, other
            // units, fog, resources, queues and turn data without reproducing
            // the transaction implementation as the expected result.
            object copy=trial.Detached(Serialized(setup.State,trial.Version));NativeGameStateEnvelope.Forget(copy);
            Require(NativeGameStateEnvelope.NativeBytes(copy,trial.Version).SequenceEqual(NativeGameStateEnvelope.NativeBytes(setup.State,trial.Version)),
                "Protected-field copy changed the postcommit native state.");
            var sight=setup.Sight??throw new InvalidDataException("Missing sight normalization contract.");var queue=Queue(copy);
            Require(queue.Length==sight.Prefix.Length+sight.A.Length+sight.B.Length,"Sight normalization would drop an unvalidated tail.");
            // Preserve the copied prefix records. Remove only the exact tail
            // already checked against owner, final footprints and native order.
            trial.Set(copy,"ActionStack",trial.OwnMutable(Native.NewList("ActionBase",queue.Take(sight.Prefix.Length))));
            RestoreAllowedFields(trial,copy,setup.BeforeA);RestoreAllowedFields(trial,copy,setup.BeforeB);
            var graph=NativeLedgerState.Graph(copy);
            trial.Set(Tile(copy,setup.BeforeA.Coordinates),"unit",graph[setup.BeforeA.Id].Unit);
            trial.Set(Tile(copy,setup.BeforeB.Coordinates),"unit",graph[setup.BeforeB.Id].Unit);
            Check(report,NativeGameStateEnvelope.NativeBytes(copy,trial.Version).SequenceEqual(setup.NativeBefore),setup.Label+"-all-other-native-state-bytes-preserved");
        }

        static void Commit(Report report,Trial trial,Setup setup)
        {
            bool planned=NativeExchangePlanning.TryPlan(setup.State,setup.Intent,out var plan,out var why);
            Measure(report,setup.Label+"-plan-reason",why);
            Check(report,planned&&plan is not null&&!plan.AlreadyCommitted,setup.Label+"-read-only-plan-valid");
            Check(report,NativeGameStateEnvelope.NativeBytes(setup.State,trial.Version).SequenceEqual(setup.NativeBefore)&&
                NativeGameStateEnvelope.Get(setup.State)!.Bytes().SequenceEqual(setup.PayloadBefore)&&
                Native.Same(Native.Get(setup.State,"ActionStack"),setup.Sight!.Stack),setup.Label+"-planning-does-not-mutate-state");
            byte[] exposed=plan!.CandidatePayload;exposed[^1]^=1;
            var exposedIntent=plan.Intent;exposedIntent.SourceAppearanceHash[0]^=1;
            Check(report,!plan.CandidatePayload.SequenceEqual(exposed)&&plan.Intent.SourceAppearanceHash.SequenceEqual(setup.Intent.SourceAppearanceHash),
                setup.Label+"-plan-owns-payload-and-intent-bytes");
            bool ok;NativeExchangeCommitResult? result;string reason;
            using(NativeExchangeCommit.DetachedTrial(setup.State,trial.Source))
                ok=NativeExchangeCommit.TryCommit(setup.State,setup.Intent,out result,out reason);
            Measure(report,setup.Label+"-commit-reason",reason);
            Check(report,ok&&result is not null&&!result.AlreadyCommitted&&result.BeforeRevision==0&&result.AfterRevision==1,setup.Label+"-native-transaction-committed");
            var graph=NativeLedgerState.Graph(setup.State);
            Check(report,Native.Same(graph[setup.BeforeA.Id].Unit,setup.A)&&Native.Same(graph[setup.BeforeB.Id].Unit,setup.B),
                setup.Label+"-native-unit-identities-preserved");
            var finalA=setup.Intent.Swap?setup.TileB:setup.TileA;var finalB=setup.Intent.Swap?setup.TileA:setup.TileB;
            Check(report,Native.Same(Native.Get(finalA,"unit"),setup.A)&&Native.Same(Native.Get(finalB,"unit"),setup.B)&&
                Position(Native.Get(setup.A,"coordinates")!)==Position(Native.Get(finalA,"coordinates")!)&&
                Position(Native.Get(setup.B,"coordinates")!)==Position(Native.Get(finalB,"coordinates")!),setup.Label+"-paired-native-occupancy-and-coordinates");
            Check(report,(bool)Native.Get(setup.A,"moved")!&&(bool)Native.Get(setup.A,"attacked")!&&
                (bool)Native.Get(setup.B,"moved")! == setup.BeforeB.Moved&&(bool)Native.Get(setup.B,"attacked")! == setup.BeforeB.Attacked&&
                Native.Same(Native.Get(setup.B,"effects"),setup.BeforeB.Effects)&&Native.Items(Native.Get(setup.B,"effects")!).SequenceEqual(setup.BeforeB.EffectValues),
                setup.Label+"-initiator-spent-partner-flags-and-effects-preserved");
            Check(report,Convert.ToUInt16(Native.Get(setup.A,"health"))==setup.BeforeA.Health&&Convert.ToUInt16(Native.Get(setup.B,"health"))==setup.BeforeB.Health,
                setup.Label+"-actual-health-preserved");
            var snapshot=NativeGameStateEnvelope.Get(setup.State);Require(snapshot is not null&&snapshot.Revision==1,"Committed private revision is absent.");
            var ledger=NativeIdentityLedgerCodec.Decode(snapshot!.Bytes(),1);
            var a=ledger.Units.Single(x=>x.NativeId==setup.BeforeA.Id);var b=ledger.Units.Single(x=>x.NativeId==setup.BeforeB.Id);
            Check(report,setup.Ledger.Units.All(before=>ledger.Units.Any(after=>after.LogicalId==before.LogicalId&&after.NativeId==before.NativeId&&
                after.InvestmentKind==before.InvestmentKind&&after.PaidStars==before.PaidStars))&&
                (setup.Intent.PartnerReference!=NativePartnerReference.Allocate||
                    b.Kind==LedgerActualKind.Genuine&&b.InvestmentKind==PaidInvestmentKind.UnknownLegacy&&b.PaidStars==0),
                setup.Label+"-synthetic-known-and-unknown-payment-facts-preserved");
            var recipient=setup.Intent.Kind==NativeExchangeKind.Regroup?a:b;
            Check(report,recipient.Independent&&recipient.Appearance is not null&&
                NativeExchangeIntentCodec.AppearanceFingerprint(recipient.Appearance).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(setup.SourceAppearance))&&
                recipient.Appearance.Health<recipient.Appearance.Maximum,setup.Label+"-copies-exact-wounded-apparent-snapshot");
            Check(report,Native.EnumName(Native.Get(setup.A,"type")!)==result!.Plan.FinalMimicCarrierA&&
                Native.EnumName(Native.Get(Native.Get(setup.A,"UnitData")!,"type")!)==result.Plan.FinalMimicCarrierA&&
                (result.Plan.FinalMimicCarrierB is null?
                    Equals(Native.Get(setup.B,"type"),setup.BeforeB.Type)&&Native.Same(Native.Get(setup.B,"UnitData"),setup.BeforeB.Data):
                    Native.EnumName(Native.Get(setup.B,"type")!)==result.Plan.FinalMimicCarrierB&&
                    Native.EnumName(Native.Get(Native.Get(setup.B,"UnitData")!,"type")!)==result.Plan.FinalMimicCarrierB),
                setup.Label+"-native-carrier-and-rule-cache-agree");
            var receipt=ledger.Receipts.Single();
            Check(report,ledger.NextSequence==2&&receipt.Sequence==setup.Intent.Sequence&&receipt.BeforeRevision==0&&receipt.AfterRevision==1&&
                receipt.Fingerprint.SequenceEqual(NativeExchangeIntentCodec.Fingerprint(setup.Intent))&&
                receipt.Affected.SequenceEqual(new[]{a.LogicalId,b.LogicalId}.OrderBy(x=>x))&&
                ledger.NextLogicalId==setup.Ledger.NextLogicalId+(setup.Intent.PartnerReference==NativePartnerReference.Allocate?1UL:0UL),
                setup.Label+"-one-receipt-and-exact-logical-allocation");
            NativeLedgerState.Validate(setup.State,1,snapshot.Bytes());
            VerifySight(report,trial,setup);
            ProtectedNativeFields(report,trial,setup);Duplicate(report,trial,setup.State,setup.Intent,setup.Label);
            object copy=trial.Detached(Serialized(setup.State,trial.Version));
            Check(report,NativeGameStateEnvelope.NativeBytes(copy,trial.Version).SequenceEqual(NativeGameStateEnvelope.NativeBytes(setup.State,trial.Version))&&
                NativeGameStateEnvelope.Get(copy)!.Revision==1&&NativeGameStateEnvelope.Get(copy)!.Bytes().SequenceEqual(snapshot.Bytes()),
                setup.Label+"-native-save-copy-preserves-transaction");
            var sourceActions=Queue(setup.State);var copiedActions=Queue(copy);
            Check(report,copiedActions.Length==sourceActions.Length&&copiedActions.Select((x,i)=>
                ActionBytes(x,trial.Version).SequenceEqual(ActionBytes(sourceActions[i],trial.Version))).All(x=>x),setup.Label+"-native-copy-keeps-pending-prefix-and-sight-records-with-receipt");
            Duplicate(report,trial,copy,setup.Intent,setup.Label+"-loaded");
            if(setup.Intent.Kind==NativeExchangeKind.Disguise&&setup.Intent.Swap)CommandCopies(report,trial,setup);
        }
        static Type BytesType=>Native.T("Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray`1").MakeGenericType(typeof(byte));
        static object Carrier(Trial trial,Setup setup,bool action)
        {
            object coordinates=setup.BeforeA.Coordinates;var parameters=new List<Type>{typeof(byte),Native.T(Improvement),Native.T("WorldCoordinates")};
            var values=new List<object>{setup.Intent.Actor,Native.EnumValue(Improvement,NativeExchangeCatalog.Id(setup.Intent.Kind)),coordinates};
            if(action){parameters.Add(typeof(bool));values.Add(true);}
            object value=trial.OwnCarrier(Native.T(action?"BuildAction":"BuildCommand").GetConstructor(parameters.ToArray())!.Invoke(values.ToArray()));
            NativeExchangeCommands.Set(value,setup.Intent);return value;
        }
        static void CommandCopies(Report report,Trial trial,Setup setup)
        {
            // Test the actual native factory and flat state records using a
            // complete committed intent. Never execute native Build or publish
            // the copied history to a client; replay below is receipt-only.
            object command=Carrier(trial,setup,false),action=Carrier(trial,setup,true);
            byte[] bytes=NativeStateProbe.ManagedBytes(Native.Invoke(Native.M("CommandBase","ToByteArray","CommandBase","System.Int32"),null,command,trial.Version)!);
            object?[] args={NativeStateProbe.NativeBytes(bytes),null,0};bool ok;
            try{ok=(bool)Native.Invoke(Native.M("CommandBase","FromByteArray",BytesType.FullName!,"CommandBase&","System.Int32&"),null,args)!;}
            finally{if(args[1] is not null)trial.OwnCarrier(args[1]!);}
            var decoded=args[1] is null?null:NativeExchangeCommands.Get(args[1]!);
            Check(report,ok&&decoded is not null&&Native.Int(args[2])==trial.Version&&
                NativeExchangeIntentCodec.Encode(decoded).SequenceEqual(NativeExchangeIntentCodec.Encode(setup.Intent)),setup.Label+"-native-command-factory-preserves-complete-intent");
            Duplicate(report,trial,setup.State,decoded!,setup.Label+"-factory-intent");
            object state=trial.Detached(Serialized(setup.State,trial.Version));
            trial.Set(state,"CommandStack",Native.NewList("CommandBase",new[]{command}));
            var pending=Queue(state);
            trial.Set(state,"ActionStack",Native.NewList("ActionBase",pending.Append(action)));
            object copy=trial.Detached(Serialized(state,trial.Version));
            foreach(string name in new[]{"CommandStack","ActionStack"})
            {
                var values=Native.Items(Native.Get(copy,name)!).ToArray();int expectedCount=name=="ActionStack"?pending.Length+1:1;
                var intent=values.Length==expectedCount?NativeExchangeCommands.Get(values[^1]):null;
                Check(report,intent is not null&&NativeExchangeIntentCodec.Encode(intent).SequenceEqual(NativeExchangeIntentCodec.Encode(setup.Intent)),
                    setup.Label+"-native-save-keeps-"+name+"-intent");
                if(name=="ActionStack")Check(report,values.Take(pending.Length).Select((x,i)=>
                    ActionBytes(x,trial.Version).SequenceEqual(ActionBytes(pending[i],trial.Version))).All(x=>x),
                    setup.Label+"-flat-action-intent-copy-retains-existing-pending-sight");
                Duplicate(report,trial,copy,intent!,setup.Label+"-loaded-"+name);
            }
        }

        // These cases execute only five audited native action types on new,
        // unpublished receivers. No command scaffold from the staging cases
        // above may enter this loop.
        static bool SameNullable(object? a,object? b)=>a is null?b is null:Native.Same(a,b);
        static bool Pending(object state,byte actor)
        {
            object?[] args={actor,Activator.CreateInstance(Native.T("CommandTrigger"))};
            return (bool)Native.Invoke(Native.M("GameState","TryGetPendingCommandTrigger","System.Byte","CommandTrigger&"),state,args)!;
        }
        static int ActionKind(object action)=>Native.Int(Native.Invoke(Native.M("ActionBase","GetActionType"),action));
        static Dictionary<byte,object> Relations(object dictionary)
        {
            int count=Native.Int(Native.Get(dictionary,"Count"));Require(count is >=0 and <=256,"Native relations exceed byte-key bounds.");
            var result=new Dictionary<byte,object>();var lookup=Native.M(dictionary.GetType(),"TryGetValue","System.Byte","DiplomacyRelation&");
            for(int key=0;key<=byte.MaxValue&&result.Count<count;key++)
            {
                object?[] args={(byte)key,null};bool found=(bool)Native.Invoke(lookup,dictionary,args)!;
                if(found){Require(args[1] is not null,"A native diplomacy relation is null.");result.Add((byte)key,args[1]!);}
            }
            Require(result.Count==count,"Native diplomacy keys changed during private observation.");return result;
        }
        sealed class PublishedCensus
        {
            readonly Trial Trial;
            readonly (string Name,object? Value)[] Roles;
            readonly (object State,byte[] Native,byte[]? Payload,long? Revision)[] States;
            static (string,object?)[] Read()
            {
                var roles=new List<(string,object?)>();
                object? manager=Native.Static("GameManager","instance");roles.Add(("GameManager.instance",manager));
                if(manager is null)return roles.ToArray();
                object? client=Native.Static("GameManager","Client");roles.Add(("GameManager.Client",client));
                roles.Add(("GameManager.GameState",Native.Static("GameManager","GameState")));
                if(client is not null)
                {
                    foreach(string name in new[]{"GameState","initialGameState","currentGameState","targetGameState","lastTurnGameState"})
                        roles.Add(("Client."+name,Native.Get(client,name)));
                    object? actions=Native.Get(client,"ActionManager");roles.Add(("Client.ActionManager",actions));
                    if(actions is not null)roles.Add(("ActionManager.GameState",Native.Get(actions,"GameState")));
                }
                return roles.ToArray();
            }
            internal PublishedCensus(Trial trial)
            {
                Trial=trial;Roles=Read();
                States=Roles.Select(x=>x.Value).Where(x=>x is not null&&Native.T("GameState").IsInstanceOfType(x))
                    .Select(x=>x!).Append(trial.Source).GroupBy(Native.Pointer).Select(g=>g.First()).Select(state=>
                    {
                        Require(Native.Int(Native.Get(state,"Version"))==trial.Version,"A protected published state has an unsupported serializer version.");
                        foreach(var value in trial.MutationRoots(state))trial.SourceObjects.Add(Native.Pointer(value));
                        foreach(var player in Native.Items(Native.Get(state,"PlayerStates")!))
                            foreach(var relation in Relations(Native.Get(player,"relations")!).Values)trial.SourceObjects.Add(Native.Pointer(relation));
                        var binding=NativeGameStateEnvelope.Get(state);
                        return(state,NativeGameStateEnvelope.NativeBytes(state,trial.Version),binding?.Bytes(),binding?.Revision);
                    }).ToArray();
            }
            internal void Verify(object privateState)
            {
                var now=Read();Require(now.Length==Roles.Length&&now.Select((x,i)=>x.Item1==Roles[i].Name&&SameNullable(x.Item2,Roles[i].Value)).All(x=>x),
                    "Native child processing changed a live manager/client state reference.");
                Require(!Roles.Any(x=>Native.Same(x.Value,privateState)),"Native child receiver was published to a client.");
                Require(Native.Same(Native.Get(privateState,"GameLogicData"),Native.Get(Trial.Source,"GameLogicData")),
                    "Private child state does not resolve the already-loaded source rules registry.");
                foreach(var item in States)
                {
                    var binding=NativeGameStateEnvelope.Get(item.State);
                    Require(NativeGameStateEnvelope.NativeBytes(item.State,Trial.Version).SequenceEqual(item.Native)&&
                        (item.Payload is null?binding is null:binding is not null&&binding.Revision==item.Revision&&binding.Bytes().SequenceEqual(item.Payload)),
                        "Native child processing changed a protected published state or binding.");
                }
                Trial.RegisterPrivateGraph(privateState);
            }
        }
        sealed record ChildStep(int Kind,bool Valid,NativeExchangePosition? Position,int Amount);
        sealed record RelationsBefore(object Dictionary,Dictionary<byte,(object Relation,byte[] Bytes)> Entries);
        sealed class ChildRun
        {
            internal readonly object State;
            internal readonly List<ChildStep> Steps=new();
            internal string Outcome="Ready";
            readonly Trial Trial;readonly Report Report;readonly PublishedCensus Published;readonly byte Actor;readonly string Label;
            readonly Dictionary<uint,(object Unit,byte[] Bytes)> Units;
            readonly Dictionary<NativeExchangePosition,byte[]> Fog;
            readonly Dictionary<byte,uint> Scores;
            readonly Dictionary<byte,RelationsBefore> Diplomacy;
            readonly HashSet<NativeExchangePosition> Discovered=new();
            readonly byte[]? Payload;readonly long? Revision;readonly object Settings,Rules;readonly int ScoreLimit;readonly byte[] SettingsBytes;
            readonly byte[] NeutralRelation;
            ulong AddedScore;
            internal ChildRun(Report report,Trial trial,PublishedCensus published,object state,string label,bool resumed=false)
            {
                Report=report;Trial=trial;Published=published;State=state;Label=label;
                Actor=Convert.ToByte(Native.Get(state,"CurrentPlayer"));Require(Actor!=byte.MaxValue,"Child execution requires a concrete actor.");
                Published.Verify(state);Require(!Pending(state,Actor),"Child execution fixture starts with an unexplained pending trigger.");
                Settings=Native.Get(state,"Settings")??throw new InvalidDataException("Missing private settings.");
                Rules=Native.Get(Settings,"rules")??throw new InvalidDataException("Missing native score rules.");ScoreLimit=Native.Int(Native.Get(Rules,"ScoreLimit"));
                SettingsBytes=NativeStateProbe.Serialize(Settings,"GameSettings",trial.Version);
                Units=NativeLedgerState.Graph(state).ToDictionary(x=>x.Key,x=>(x.Value.Unit,NativeStateProbe.Serialize(x.Value.Unit,"UnitState",trial.Version)));
                Fog=Tiles(state).ToDictionary(t=>Position(Native.Get(t,"coordinates")!),Explorers);
                Scores=Native.Items(Native.Get(state,"PlayerStates")!).ToDictionary(p=>Convert.ToByte(Native.Get(p,"Id")),p=>Convert.ToUInt32(Native.Get(p,"score")));
                Diplomacy=Native.Items(Native.Get(state,"PlayerStates")!).ToDictionary(p=>Convert.ToByte(Native.Get(p,"Id")),p=>
                {
                    object dictionary=Native.Get(p,"relations")??throw new InvalidDataException("Missing private diplomacy dictionary.");
                    var entries=Relations(dictionary);foreach(var relation in entries.Values)
                        Require(!trial.SourceObjects.Contains(Native.Pointer(relation)),"A private diplomacy value aliases a protected live state.");
                    return new RelationsBefore(dictionary,entries.ToDictionary(x=>x.Key,x=>(x.Value,NativeStateProbe.Serialize(x.Value,"DiplomacyRelation",trial.Version))));
                });
                object neutral=trial.OwnMutable(New("DiplomacyRelation"));
                Require(Equals(Native.Get(neutral,"State"),Enum.Parse(Native.T("DiplomacyRelationState"),"Neutral")),"Native default diplomacy relation is not Neutral.");
                NeutralRelation=NativeStateProbe.Serialize(neutral,"DiplomacyRelation",trial.Version);
                var binding=NativeGameStateEnvelope.Get(state);Payload=binding?.Bytes();Revision=binding?.Revision;
                foreach(var action in Queue(state))
                {
                    int kind=ActionKind(action);Require(kind is 25 or 85||(resumed&&kind is 27 or 22 or 31),"Execution fixture contains inherited or unreviewed work.");
                    Require(Convert.ToByte(Native.Get(action,"PlayerId"))==Actor,"Execution fixture uses a different actor.");
                    if(kind==25)
                    {
                        object tile=Tile(state,Native.Get(Native.As(action,"ExploreAction"),"Coordinates")!);
                        if(!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,Actor)!)
                            Require(SafeDiscovery(state,tile),"Queued private discovery is not an audited empty noncity non-lighthouse cell.");
                    }
                }
            }
            void Invariants()
            {
                Published.Verify(State);
                var graph=NativeLedgerState.Graph(State);
                Require(graph.Count==Units.Count&&Units.All(x=>graph.TryGetValue(x.Key,out var body)&&Native.Same(body.Unit,x.Value.Unit)&&
                    NativeStateProbe.Serialize(body.Unit,"UnitState",Trial.Version).SequenceEqual(x.Value.Bytes)),
                    "Child execution changed a unit's identity, health, home, effects, position or allowance.");
                var binding=NativeGameStateEnvelope.Get(State);
                Require(Payload is null?binding is null:binding is not null&&binding.Revision==Revision&&binding.Bytes().SequenceEqual(Payload),
                    "Child execution changed the exchange ledger or paid investment facts.");
                Require(Native.Same(Native.Get(State,"Settings"),Settings)&&SameNullable(Native.Get(Settings,"rules"),Rules)&&
                    Native.Int(Native.Get(Rules,"ScoreLimit"))==ScoreLimit&&NativeStateProbe.Serialize(Settings,"GameSettings",Trial.Version).SequenceEqual(SettingsBytes),
                    "Child execution changed native settings or score rules.");
                foreach(var tile in Tiles(State))
                {
                    var at=Position(Native.Get(tile,"coordinates")!);var before=Fog[at];var after=Explorers(tile);
                    Require(after.Where(x=>x!=Actor).SequenceEqual(before.Where(x=>x!=Actor))&&
                        after.Contains(Actor)==(before.Contains(Actor)||Discovered.Contains(at)),"Child execution changed unauthorized exploration.");
                }
                foreach(var player in Native.Items(Native.Get(State,"PlayerStates")!))
                {
                    byte id=Convert.ToByte(Native.Get(player,"Id"));ulong expected=(ulong)Scores[id]+(id==Actor?AddedScore:0);
                    Require(expected<=uint.MaxValue&&Convert.ToUInt32(Native.Get(player,"score"))==expected,"Child execution changed unexpected score.");
                    var prior=Diplomacy[id];object dictionary=Native.Get(player,"relations")!;
                    Require(Native.Same(dictionary,prior.Dictionary),"Child execution replaced a private diplomacy dictionary.");
                    var relations=Relations(dictionary);
                    Require(prior.Entries.All(x=>relations.TryGetValue(x.Key,out var value)&&Native.Same(value,x.Value.Relation)&&
                        NativeStateProbe.Serialize(value,"DiplomacyRelation",Trial.Version).SequenceEqual(x.Value.Bytes)),
                        "Child execution changed or removed a preexisting diplomacy relation.");
                    foreach(var pair in relations.Where(x=>!prior.Entries.ContainsKey(x.Key)))
                        Require(!Trial.SourceObjects.Contains(Native.Pointer(pair.Value))&&
                            NativeStateProbe.Serialize(pair.Value,"DiplomacyRelation",Trial.Version).SequenceEqual(NeutralRelation),
                            "Child execution added a nondefault or aliased diplomacy relation.");
                }
            }
            internal bool Step()
            {
                Require(Outcome=="Ready","A stopped private child receiver cannot execute again.");
                Published.Verify(State);var before=Queue(State);
                if(before.Length==0){Outcome="Completed";Measure(Report,Label+"-outcome",Outcome);return false;}
                if(Pending(State,Actor)){Outcome="PausedWithPendingTrigger";Measure(Report,Label+"-outcome",Outcome);return false;}
                if(Steps.Count>=256){Outcome="StepBound";Measure(Report,Label+"-outcome",Outcome);return false;}
                object top=Trial.OwnCarrier(before[^1]);int kind=ActionKind(top);
                Measure(Report,Label+"-next-action",kind);
                if(kind is not (25 or 27 or 85 or 22 or 31))
                {Outcome="UnsupportedChild";Measure(Report,Label+"-outcome",Outcome);Measure(Report,Label+"-unsupported-record-sha256",Hash(ActionBytes(top,Trial.Version)));return false;}
                Require(Convert.ToByte(Native.Get(top,"PlayerId"))==Actor,"Audited child has a different actor.");
                NativeExchangePosition? at=null;int amount=0;
                if(kind==25)
                {
                    var tile=Tile(State,Native.Get(Native.As(top,"ExploreAction"),"Coordinates")!);at=Position(Native.Get(tile,"coordinates")!);
                    if(!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,Actor)!)
                        Require(SafeDiscovery(State,tile),"Private Explore would enter an unreviewed discovery path.");
                }
                if(kind==27)
                {
                    amount=Native.Int(Native.Get(Native.As(top,"IncreaseScoreAction"),"Amount"));
                    ulong score=Convert.ToUInt32(Native.Get(Player(State,Actor),"score"));
                    if(amount<0||score+(ulong)amount>uint.MaxValue||(ScoreLimit>0&&score+(ulong)amount>=(ulong)ScoreLimit))
                    {Outcome="UnsafeScoreThreshold";Measure(Report,Label+"-outcome",Outcome);return false;}
                }
                bool valid=(bool)Native.Invoke(Native.M("ActionBase","IsValid","GameState"),top,State)!;
                bool dirty=(bool)Native.Get(State,"HasFlaggedNeedForUpdateRoutes")!;byte[]? invalidExpected=null;
                byte[][] prefixBytes=before.Take(before.Length-1).Select(x=>ActionBytes(x,Trial.Version)).ToArray();
                if(!valid)
                {
                    // Independently decoded copy: normalize only the consumed
                    // queue record, never borrow native list entries across states.
                    object copy=Trial.Detached(Serialized(State,Trial.Version));var copied=Queue(copy);
                    Trial.Set(copy,"ActionStack",Trial.OwnMutable(Native.NewList("ActionBase",copied.Take(copied.Length-1))));
                    invalidExpected=NativeGameStateEnvelope.NativeBytes(copy,Trial.Version);
                }
                string key=Label+"-step-"+Steps.Count;Measure(Report,key+"-kind",kind);Measure(Report,key+"-valid",valid);
                if(at.HasValue)Measure(Report,key+"-coordinate",at.Value);if(kind==27)Measure(Report,key+"-native-score-amount",amount);
                bool consumed;
                try{consumed=(bool)Native.Invoke(Native.M("ActionManagerUtils","SimulateAction","GameState"),null,State)!;Measure(Report,key+"-return",consumed);}
                catch(Exception e){Outcome="NativeException";Measure(Report,Label+"-outcome",Outcome);Measure(Report,key+"-exception",e.GetType().Name+": "+e.Message);throw;}
                finally{Trial.Collect(State);Published.Verify(State);}
                Require(consumed,"Native action did not consume the reviewed top record.");
                var after=Queue(State);Require(after.Length>=before.Length-1&&PrefixMatches(after.Take(before.Length-1).ToArray(),before.Take(before.Length-1).ToArray())&&
                    after.Take(before.Length-1).Select((x,i)=>ActionBytes(x,Trial.Version).SequenceEqual(prefixBytes[i])).All(x=>x)&&
                    !after.Any(x=>Native.Same(x,top)),"Native step changed an older queue reference/record or retained the consumed top.");
                var children=after.Skip(before.Length-1).ToArray();
                Require(children.Select(Native.Pointer).Distinct().Count()==children.Length&&!children.Any(x=>before.Any(y=>Native.Same(x,y))),
                    "Native child queue reused an existing record.");
                if(!valid)Require(children.Length==0&&NativeGameStateEnvelope.NativeBytes(State,Trial.Version).SequenceEqual(invalidExpected!),
                    "An invalid native action had effects beyond removing its own queue record.");
                else switch(kind)
                {
                    case 25:
                        Require(at.HasValue&&Discovered.Add(at.Value),"Valid Explore rediscovered an already counted cell.");
                        Require(children.Length==1&&ActionKind(children[0])==27&&(bool)Native.Get(State,"HasFlaggedNeedForUpdateRoutes")!,
                            "Safe discovery did not produce exactly one score child and route dirtiness.");break;
                    case 27:AddedScore+=(ulong)amount;Require(children.Length==0,"Score child appended unexpected work.");break;
                    case 85:
                        Require(dirty&&children.Length==2&&ActionKind(children[0])==31&&ActionKind(children[1])==22&&
                            !(bool)Native.Get(State,"HasFlaggedNeedForUpdateRoutes")!,"Native deferred routes did not enqueue connections then routes and clear dirtiness.");break;
                    case 22:Require(children.Length==0,"Route update appended unexpected work.");break;
                    case 31:break; // Unknown connection children are preserved and stop before their IsValid/Execute.
                }
                Steps.Add(new(kind,valid,at,amount));Invariants();return true;
            }
            internal void Drain()
            {
                while(Step()){}
                Check(Report,Outcome=="Completed"&&Queue(State).Length==0,Label+"-native-queue-actually-empty");
                Invariants();Measure(Report,Label+"-consumed-count",Steps.Count);Measure(Report,Label+"-consumed-order",string.Join(",",Steps.Select(x=>x.Kind+(x.Valid?"+":"-"))));
                Measure(Report,Label+"-native-score-added",AddedScore);
                Check(Report,true,Label+"-protected-live-census-unit-bytes-ledger-score-and-fog-held-after-every-step");
                Check(Report,true,Label+"-preexisting-diplomacy-records-preserved-and-only-default-neutral-additions");
            }
        }
        static object Explore(Trial trial,byte actor,object tile)=>trial.OwnCarrier(Native.T("ExploreAction")
            .GetConstructor(new[]{typeof(byte),Native.T("WorldCoordinates")})!.Invoke(new[]{(object)actor,Native.Get(tile,"coordinates")!}));
        static void SetQueue(Trial trial,object state,params object[] actions)
        {
            foreach(var action in actions)trial.OwnCarrier(action);
            trial.Set(state,"ActionStack",trial.OwnMutable(Native.NewList("ActionBase",actions)));
        }
        static void EmptyStep(Report report,Trial trial,PublishedCensus published,object state,string label)
        {
            Require(Queue(state).Length==0,"Empty-step diagnostic has queued work.");
            byte[] before=NativeGameStateEnvelope.NativeBytes(state,trial.Version);bool consumed;
            try{consumed=(bool)Native.Invoke(Native.M("ActionManagerUtils","SimulateAction","GameState"),null,state)!;}
            finally{trial.Collect(state);published.Verify(state);}
            Check(report,!consumed&&Queue(state).Length==0&&NativeGameStateEnvelope.NativeBytes(state,trial.Version).SequenceEqual(before),
                label+"-empty-native-step-returns-false-without-state-change");
        }
        static void DuplicateExploreExecution(Report report,Trial trial,PublishedCensus published)
        {
            const string label="children-standalone-duplicate";
            object state=trial.Detached(trial.SourceNative);published.Verify(state);
            byte actor=Convert.ToByte(Native.Get(state,"CurrentPlayer"));
            object tile=Tiles(state).FirstOrDefault(t=>SafeDiscovery(state,t))??throw new InvalidDataException("No safe standalone private exploration cell.");
            trial.Set(tile,"explorers",ByteList(trial,Explorers(tile).Where(x=>x!=actor)));
            trial.Set(state,"HasFlaggedNeedForUpdateRoutes",false);
            SetQueue(trial,state,Explore(trial,actor,tile),Explore(trial,actor,tile));
            var run=new ChildRun(report,trial,published,state,label);run.Drain();
            Check(report,run.Steps.Select(x=>x.Kind).SequenceEqual(new[]{25,27,25})&&run.Steps.Select(x=>x.Valid).SequenceEqual(new[]{true,true,false})&&
                run.Steps[0].Position==run.Steps[2].Position, label+"-native-discovery-score-invalid-duplicate-order");
            Check(report,(bool)Native.Get(state,"HasFlaggedNeedForUpdateRoutes")!,label+"-dirty-retained-without-deferred-prefix");
            EmptyStep(report,trial,published,state,label);trial.VerifySource(report);
        }
        static ChildRun Checkpoint(Report report,Trial trial,PublishedCensus published,Setup setup,string label,int topKind)
        {
            object[] before=Queue(setup.State);Require(before.Length>0&&ActionKind(before[^1])==topKind,"Checkpoint is not at its exact expected pending native child.");
            byte[][] records=before.Select(x=>ActionBytes(x,trial.Version)).ToArray();
            Duplicate(report,trial,setup.State,setup.Intent,label+"-before-copy");
            byte[] expected=NativeGameStateEnvelope.NativeBytes(setup.State,trial.Version);
            var binding=NativeGameStateEnvelope.Get(setup.State)!;
            object copy=trial.Detached(Serialized(setup.State,trial.Version));var queue=Queue(copy);var copied=NativeGameStateEnvelope.Get(copy);
            Check(report,NativeGameStateEnvelope.NativeBytes(copy,trial.Version).SequenceEqual(expected)&&queue.Length==records.Length&&
                queue.Select((x,i)=>ActionBytes(x,trial.Version).SequenceEqual(records[i])).All(x=>x)&&copied is not null&&
                copied.Revision==binding.Revision&&copied.Bytes().SequenceEqual(binding.Bytes()),label+"-native-copy-preserves-exact-partial-queue-state-and-receipt");
            Duplicate(report,trial,copy,setup.Intent,label+"-after-copy");
            return new ChildRun(report,trial,published,copy,label,true);
        }
        static void ExchangeChildExecution(Report report,Trial trial,PublishedCensus published,bool dirty)
        {
            // The only initial pending record is a native deferred action.
            // Preserve all earlier staging/fault scenarios as separate fixtures.
            var setup=Prepare(report,trial,false,NativeExchangeKind.Regroup,dirty,true,dirty,true);
            Commit(report,trial,setup);
            string label=setup.Label;var run=new ChildRun(report,trial,published,setup.State,label);
            Check(report,run.Step()&&run.Steps.Count==1&&run.Steps[0].Kind==25&&run.Steps[0].Valid&&ActionKind(Queue(setup.State)[^1])==27,
                label+"-stops-after-first-explore-with-score-child-pending");
            var scoreCopy=Checkpoint(report,trial,published,setup,label+"-score-pending-copy",27);
            var overlap=setup.Sight!.A.Intersect(setup.Sight.B).Single();bool checkpoint=false;
            for(int i=0;i<256&&run.Outcome=="Ready";i++)
            {
                var queue=Queue(setup.State);
                if(queue.Length>0&&ActionKind(queue[^1])==25)
                {
                    object coordinate=Native.Get(Native.As(queue[^1],"ExploreAction"),"Coordinates")!;
                    if(Position(coordinate)==overlap&&(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),Tile(setup.State,coordinate),setup.Intent.Actor)!)
                    {checkpoint=true;break;}
                }
                if(!run.Step())break;
            }
            Check(report,checkpoint,label+"-reaches-already-explored-overlap-before-duplicate-consumption");
            var overlapCopy=Checkpoint(report,trial,published,setup,label+"-overlap-pending-copy",25);
            run.Drain();
            Check(report,run.Steps.Count==12&&run.Steps.Count(x=>x.Kind==25&&x.Valid)==4&&run.Steps.Count(x=>x.Kind==25&&!x.Valid)==1&&
                run.Steps.Count(x=>x.Kind==27&&x.Valid)==4&&run.Steps.TakeLast(3).Select(x=>x.Kind).SequenceEqual(new[]{85,22,31})&&
                run.Steps.TakeLast(3).All(x=>x.Valid),label+"-four-discoveries-one-invalid-overlap-and-native-deferred-route-connection-order");
            Check(report,run.Steps.Select((x,i)=>x.Kind!=25||!x.Valid||(i+1<run.Steps.Count&&run.Steps[i+1].Kind==27)).All(x=>x)&&
                !(bool)Native.Get(setup.State,"HasFlaggedNeedForUpdateRoutes")!,label+"-each-score-child-precedes-older-explore-and-deferred-clears-dirty");
            foreach(var continuation in new[]{scoreCopy,overlapCopy})
            {
                continuation.Drain();
                Check(report,NativeGameStateEnvelope.NativeBytes(continuation.State,trial.Version).SequenceEqual(NativeGameStateEnvelope.NativeBytes(setup.State,trial.Version)),
                    label+(ReferenceEquals(continuation,scoreCopy)?"-score":"-overlap")+"-native-partial-copy-resume-has-identical-completed-state");
                Duplicate(report,trial,continuation.State,setup.Intent,label+"-completed-copy");
            }
            Check(report,overlapCopy.Steps.Count>0&&overlapCopy.Steps[0].Kind==25&&!overlapCopy.Steps[0].Valid,
                label+"-loaded-overlap-duplicate-remains-invalid");
            Duplicate(report,trial,setup.State,setup.Intent,label+"-completed");EmptyStep(report,trial,published,setup.State,label);
            trial.VerifySource(report);
        }
        static void DeferredAndPendingExecution(Report report,Trial trial,PublishedCensus published)
        {
            const string emptyLabel="children-already-explored-clean-deferred";
            var setup=Prepare(report,trial,false,NativeExchangeKind.Regroup,false,false,false,true);Commit(report,trial,setup);
            var run=new ChildRun(report,trial,published,setup.State,emptyLabel);run.Drain();
            Check(report,run.Steps.Count==1&&run.Steps[0].Kind==85&&!run.Steps[0].Valid&&!(bool)Native.Get(setup.State,"HasFlaggedNeedForUpdateRoutes")!,
                emptyLabel+"-invalid-deferred-consumed-without-route-work");

            const string pauseLabel="children-pending-trigger-pause-only";
            object state=trial.Detached(trial.SourceNative);published.Verify(state);byte actor=Convert.ToByte(Native.Get(state,"CurrentPlayer"));
            Require(!Pending(state,actor),"Pause fixture has an unexplained existing actor trigger.");
            object city=Tiles(state).FirstOrDefault(t=>Convert.ToByte(Native.Get(t,"owner"))==actor&&
                (bool)Native.Invoke(Native.M("TileData","HasImprovement",Improvement),t,Native.EnumValue(Improvement,"city"))!)
                ??throw new InvalidDataException("Pause fixture has no owned city coordinate.");
            object deferred=trial.OwnCarrier(Native.T("RunDeferredAction").GetConstructor(new[]{typeof(byte)})!.Invoke(new object[]{actor}));
            SetQueue(trial,state,deferred);
            object trigger=Activator.CreateInstance(Native.T("CommandTrigger"))!;
            void Field(string name,object value)=>(trigger.GetType().GetField(name,BindingFlags.Public|BindingFlags.Instance)
                ??throw new MissingFieldException("CommandTrigger",name)).SetValue(trigger,value);
            Field("playerId",actor);Field("opponentId",(byte)0);Field("type",Enum.Parse(Native.T("CommandTriggerType"),"CityLevelUp"));
            Field("coordinates",Native.Get(city,"coordinates")!);
            // This is an explicit pause marker, not evidence of a level-up
            // entitlement. Native Add only appends; the trigger is never run.
            Native.Invoke(Native.M("GameState","AddPendingCommandTrigger","CommandTrigger"),state,trigger);trial.RegisterPrivateGraph(state);
            Require(Pending(state,actor),"Native pending-trigger marker was not found.");
            byte[] before=NativeGameStateEnvelope.NativeBytes(state,trial.Version);object stack=Native.Get(state,"ActionStack")!;var records=Queue(state);bool consumed;
            try{consumed=(bool)Native.Invoke(Native.M("ActionManagerUtils","SimulateAction","GameState"),null,state)!;}
            finally{trial.Collect(state);published.Verify(state);}
            Measure(report,pauseLabel+"-outcome","PausedWithPendingTrigger");Measure(report,pauseLabel+"-return",consumed);
            Check(report,!consumed&&Pending(state,actor)&&Native.Same(Native.Get(state,"ActionStack"),stack)&&Queue(state).Length==1&&
                PrefixMatches(Queue(state),records)&&NativeGameStateEnvelope.NativeBytes(state,trial.Version).SequenceEqual(before),
                pauseLabel+"-false-with-exact-unchanged-nonempty-queue-is-not-completion");
            trial.VerifySource(report);
        }

        internal static Report Run(object source,int version)
        {
            var report=new Report{Scope="detached-native-exchange-planning-and-commit",Limit=
                "Detached native diagnostic only. The original transaction/queued-sight/fault cases do not execute their queues. Separate new unpublished fixtures execute only native Explore, IncreaseScore, RunDeferred, UpdateRoutes and UpdateCityConnections through bounded SimulateAction steps, including partial native copies. Private setup relocates copied units, adds registered research to copied players, and changes private explorer lists. Private Known3/UnknownLegacy0 payment facts and a pause-only city-level marker are synthetic. " +
                "Unknown connection, meeting, lighthouse, game-over or other children stop before execution. No EndCommand, inherited command history or custom Build action is executed. Native logging and shared read-only registry access remain. No live client adoption, real command dispatch, movement/arrival, observer reaction, network refresh, GUI gameplay, save-file migration, replay/seek or multiplayer secrecy is established."};
            if(Running){report.Failure="Detached exchange probe reentry rejected.";return report;}
            Running=true;var trial=new Trial{Source=source,Version=version};bool captured=false;
            long prepared=NativeStateBridge.Prepared,cleaned=NativeStateBridge.Cleaned,rejected=NativeStateBridge.Rejected;
            try
            {
                Require(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_EXCHANGE_PROBE")=="1"&&NativeStateBridge.Installed,
                    "Detached exchange probe requires its explicit native-bridge opt-in.");
                Require(version==NativeStateFrame.NativeVersion&&Native.T("GameState").IsInstanceOfType(source)&&Native.Int(Native.Get(source,"Version"))==version,
                    "Detached exchange probe requires native state and serializer123.");
                Require(NativeGameStateEnvelope.ActiveOperations==0&&NativeCommandEnvelope.ActiveOperations==0,"A native serializer operation is already active.");
                trial.CaptureSource();captured=true;TraceSourceCandidates(report,trial);
                foreach(bool mimic in new[]{false,true})foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})foreach(bool swap in new[]{false,true})
                {
                    var setup=Prepare(report,trial,mimic,kind,swap);
                    var stale=setup.Intent with{SourceAppearanceHash=(byte[])setup.Intent.SourceAppearanceHash.Clone()};stale.SourceAppearanceHash[0]^=1;
                    RejectedUnchanged(report,trial,setup,stale,setup.Label+"-stale-source");
                    Commit(report,trial,setup);trial.VerifySource(report);
                }
                foreach(string stage in new[]{"forms","positions","allowance","ledger"})
                {
                    var setup=Prepare(report,trial,true,NativeExchangeKind.Disguise,true);
                    RejectedUnchanged(report,trial,setup,setup.Intent,"fault-after-"+stage,stage);trial.VerifySource(report);
                }
                foreach(var kind in new[]{NativeExchangeKind.Regroup,NativeExchangeKind.Disguise})foreach(bool swap in new[]{false,true})
                {
                    var setup=Prepare(report,trial,false,kind,swap,true,swap);
                    var stale=setup.Intent with{SourceAppearanceHash=(byte[])setup.Intent.SourceAppearanceHash.Clone()};stale.SourceAppearanceHash[0]^=1;
                    RejectedUnchanged(report,trial,setup,stale,setup.Label+"-stale-source");
                    Commit(report,trial,setup);trial.VerifySource(report);
                }
                foreach(bool dirty in new[]{false,true})foreach(string stage in new[]{"sight-a","sight-b","ledger"})
                {
                    var setup=Prepare(report,trial,true,NativeExchangeKind.Disguise,true,true,dirty);
                    RejectedUnchanged(report,trial,setup,setup.Intent,"discovery-dirty-"+(dirty?"true":"false")+"-fault-after-"+stage,stage);
                    trial.VerifySource(report);
                }
                var published=new PublishedCensus(trial);
                DuplicateExploreExecution(report,trial,published);
                foreach(bool dirty in new[]{false,true})ExchangeChildExecution(report,trial,published,dirty);
                DeferredAndPendingExecution(report,trial,published);
                report.Passed=true;
            }
            catch(Exception e){report.Failure=e.GetType().FullName+": "+e.Message;}
            finally
            {
                if(captured)
                {
                    try{trial.VerifySource(report);}
                    catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Source verification: "+e.Message;}
                }
                trial.Cleanup(report);
                Measure(report,"bridge-prepared",NativeStateBridge.Prepared-prepared);Measure(report,"bridge-cleaned",NativeStateBridge.Cleaned-cleaned);
                Measure(report,"bridge-rejected",NativeStateBridge.Rejected-rejected);
                try{Check(report,NativeStateBridge.Prepared-prepared>0&&NativeStateBridge.Prepared-prepared==NativeStateBridge.Cleaned-cleaned&&
                    NativeGameStateEnvelope.ActiveOperations==0&&NativeCommandEnvelope.ActiveOperations==0,"all-exchange-probe-native-bridge-operations-cleaned");}
                catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+" Bridge cleanup: "+e.Message;}
                Running=false;
            }
            return report;
        }
    }
}
