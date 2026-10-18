using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deception.Core;
using Buffer=Deception.PolyModAdapter.NativeStateProbe.Buffer;
using Report=Deception.PolyModAdapter.NativeStateProbe.Report;

namespace Deception.PolyModAdapter
{
    // Opt-in exact-disposable ownership diagnostic; no lifecycle hook.
    // Requires the separately reviewed ownership guards before envelope cookies,
    // including BeforeFrame/BeforeLegacyRead and AllocatedOperations observation.
    // All successful reads target private receivers. No client field is assigned.
    internal static class NativeOwnershipProbe
    {
        const string Reader="Il2CppSystem.IO.BinaryReader",Writer="Il2CppSystem.IO.BinaryWriter";
        const string NativeFailure="Il2CppInterop.Runtime.Il2CppException";
        static readonly Guid Epoch=new("80a9eab8-180a-422f-9e49-b31685844d15");
        static readonly Guid OtherEpoch=new("2bd21ee0-0967-4750-9a62-0e00e04ebfb8");
        [ThreadStatic] static bool Running;
        static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        static void Check(Report r,bool value,string label){Require(value,label);r.Checks.Add(label);}
        static void Measure(Report r,string label,object? value)=>r.Measurements[label]=Convert.ToString(value,CultureInfo.InvariantCulture)??"null";
        static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        static bool Same(object? a,object? b)=>a is null?b is null:b is not null&&Native.Same(a,b);
        static object NewState()=>Native.T("GameState").GetConstructor(Type.EmptyTypes)!.Invoke(Array.Empty<object>());
        static byte[] Serialize(object state,int version)=>NativeStateProbe.Serialize(state,"GameState",version);
        static bool SameBinding(object state,NativeGameStateEnvelope.Snapshot? before)
        {
            var now=NativeGameStateEnvelope.Get(state);
            return before is null?now is null:now is not null&&now.Revision==before.Revision&&now.Bytes().SequenceEqual(before.Bytes());
        }
        readonly record struct Counts(long Prepared,long Cleaned,long Finished,ulong Allocated);
        static Counts Count()=>new(NativeStateBridge.Prepared,NativeStateBridge.Cleaned,NativeStateBridge.Finished,
            NativeGameStateEnvelope.AllocatedOperations); // New monotonic read-only observation, root integration owns it.
        static void Rejected(Report r,string label,Counts before,bool beforeCookie)
        {
            var after=Count();Measure(r,label+"-prepared",after.Prepared-before.Prepared);
            Measure(r,label+"-cleaned",after.Cleaned-before.Cleaned);Measure(r,label+"-allocated",after.Allocated-before.Allocated);
            Check(r,after.Prepared==before.Prepared+1&&after.Cleaned==before.Cleaned+1&&after.Finished==before.Finished&&
                NativeGameStateEnvelope.ActiveOperations==0,label+"-native-rejection-balanced-without-original-finish");
            if(beforeCookie)Check(r,after.Allocated==before.Allocated,label+"-no-operation-cookie-allocated");
        }
        static void NativeReject(Report r,string label,Action call)
        {
            Exception? error=null;try{call();}catch(Exception e){error=e;}
            Measure(r,label+"-exception",error?.GetType().FullName??"none");
            Check(r,error?.GetType().FullName==NativeFailure,label+"-actual-native-caller-rejected");
        }
        static void ManagedReject(Report r,string label,Action call)
        {
            bool failed=false;try{call();}catch(InvalidDataException){failed=true;}
            Check(r,failed,label+"-policy-entry-rejected");
        }

        // This bounded witness never calls GameState.Serialize. It is usable
        // while the required context intentionally forbids an unbound writer,
        // or after a fault permanently forbids an adopted state's writer.
        // Full native byte comparisons occur later, only when policy permits.
        sealed class Witness
        {
            readonly object State;
            readonly int Version;
            readonly (string Name,object? Value)[] Roots;
            readonly (string Name,object? Value)[] Scalars;
            readonly Dictionary<uint,(object Unit,byte[] Bytes)> Bodies;
            readonly (object Player,byte[] Bytes)[] Players;
            readonly (object Entry,byte[] Bytes)[][] Histories;
            readonly byte[] Settings;
            internal Witness(object state,int version)
            {
                State=state;Version=version;
                Roots=new[]{"Map","Settings","PlayerStates","CommandStack","ActionStack","pendingCommandTriggers"}
                    .Select(name=>(name,Native.Get(state,name))).ToArray();
                Scalars=new[]{"Version","CurrentTurn","CurrentPlayer","CurrentState","HasFlaggedNeedForUpdateRoutes"}
                    .Select(name=>(name,Native.Get(state,name))).ToArray();
                Bodies=NativeLedgerState.Graph(state).ToDictionary(x=>x.Key,x=>(x.Value.Unit,
                    NativeStateProbe.Serialize(x.Value.Unit,"UnitState",version)));
                var players=Native.Get(state,"PlayerStates")!;
                Require(Native.Int(Native.Get(players,"Count")) is >0 and <=16,"Ownership probe player bound exceeded.");
                Players=Native.Items(players).Select(p=>(p,NativeStateProbe.Serialize(p,"PlayerState",version))).ToArray();
                // Do not call command/action serializers here: those use the
                // same bridge counters and would obscure a rejection boundary.
                Histories=new[]{"CommandStack","ActionStack"}.Select(name=>
                {
                    var list=Native.Get(state,name)!;Require(Native.Int(Native.Get(list,"Count"))<=16384,"Ownership history bound exceeded.");
                    return Native.Items(list).Select(x=>(x,Array.Empty<byte>())).ToArray();
                }).ToArray();
                Settings=NativeStateProbe.Serialize(Native.Get(state,"Settings")!,"GameSettings",version);
            }
            internal void Verify()
            {
                Require(Roots.All(x=>Same(x.Value,Native.Get(State,x.Name)))&&Scalars.All(x=>Equals(x.Value,Native.Get(State,x.Name))),
                    "Ownership rejection changed native state roots or scalar fields.");
                var graph=NativeLedgerState.Graph(State);
                Require(graph.Count==Bodies.Count&&Bodies.All(x=>graph.TryGetValue(x.Key,out var body)&&Native.Same(body.Unit,x.Value.Unit)&&
                    NativeStateProbe.Serialize(body.Unit,"UnitState",Version).SequenceEqual(x.Value.Bytes)),
                    "Ownership rejection changed native bodies or their identities.");
                var players=Native.Items(Native.Get(State,"PlayerStates")!).ToArray();
                Require(players.Length==Players.Length&&players.Select((p,i)=>Native.Same(p,Players[i].Player)&&
                    NativeStateProbe.Serialize(p,"PlayerState",Version).SequenceEqual(Players[i].Bytes)).All(x=>x),
                    "Ownership rejection changed native player records.");
                string[] names={"CommandStack","ActionStack"};
                for(int i=0;i<names.Length;i++)
                {
                    var entries=Native.Items(Native.Get(State,names[i])!).ToArray();
                    Require(entries.Length==Histories[i].Length&&entries.Select((x,j)=>Native.Same(x,Histories[i][j].Entry)).All(x=>x),
                        "Ownership rejection changed native history references.");
                }
                Require(NativeStateProbe.Serialize(Native.Get(State,"Settings")!,"GameSettings",Version).SequenceEqual(Settings),
                    "Ownership rejection changed native settings.");
            }
        }

        sealed class Trial
        {
            internal readonly object Client,Source;
            internal readonly Guid GameId;
            internal readonly int Version;
            internal readonly byte[] SourceBytes;
            internal readonly List<object> Private=new();
            internal readonly HashSet<long> Faulted=new();
            internal readonly Dictionary<long,object> Carriers=new();
            readonly (string Name,object? Value)[] Roles;
            readonly (object State,byte[] Bytes,Witness Witness)[] Published;
            readonly HashSet<long> Protected=new();
            readonly HashSet<long> PrivateRoots=new();
            internal NativeLedgerOwnership.Context? Context;

            static (string Name,object? Value)[] ReadRoles()
            {
                var roles=new List<(string,object?)>();object? manager=Native.Static("GameManager","instance");
                roles.Add(("GameManager.instance",manager));if(manager is null)return roles.ToArray();
                object? client=Native.Static("GameManager","Client");roles.Add(("GameManager.Client",client));
                roles.Add(("GameManager.GameState",Native.Static("GameManager","GameState")));
                if(client is null)return roles.ToArray();
                foreach(string name in new[]{"GameState","initialGameState","currentGameState","targetGameState","lastTurnGameState"})
                    roles.Add(("Client."+name,Native.Get(client,name)));
                object? actions=Native.Get(client,"ActionManager");roles.Add(("Client.ActionManager",actions));
                if(actions is not null)roles.Add(("ActionManager.GameState",Native.Get(actions,"GameState")));
                return roles.ToArray();
            }
            static IEnumerable<object> Roots(object state)
            {
                yield return state;
                foreach(string name in new[]{"Settings","Map","PlayerStates","CommandStack","ActionStack","pendingCommandTriggers"})
                    if(Native.Get(state,name) is object value)yield return value;
                if(Native.Get(Native.Get(state,"Settings")!,"rules") is object rules)yield return rules;
                var tiles=Native.Get(Native.Get(state,"Map")!,"Tiles")!;yield return tiles;
                int count=Native.Int(Native.Get(tiles,"Length"));Require(count is >0 and <=4096,"Ownership map bound exceeded.");
                for(int i=0;i<count;i++)
                {
                    object tile=Native.Invoke(Native.M(tiles.GetType(),"get_Item","System.Int32"),tiles,i)!;yield return tile;
                    if(Native.Get(tile,"explorers") is object explorers)yield return explorers;
                }
                foreach(var body in NativeLedgerState.Graph(state).Values)
                {yield return body.Unit;if(Native.Get(body.Unit,"effects") is object effects)yield return effects;}
                foreach(var player in Native.Items(Native.Get(state,"PlayerStates")!))
                {
                    yield return player;
                    foreach(string name in new[]{"availableTech","relations","unlockedTechCache"})
                        if(Native.Get(player,name) is object value)yield return value;
                }
                foreach(string name in new[]{"CommandStack","ActionStack"})
                    foreach(var value in Native.Items(Native.Get(state,name)!))yield return value;
            }
            internal Trial(object client,Guid gameId,object source,int version)
            {
                Client=client;GameId=gameId;Source=source;Version=version;Roles=ReadRoles();
                Published=Roles.Select(x=>x.Value).Where(x=>x is not null&&Native.T("GameState").IsInstanceOfType(x))
                    .Select(x=>x!).Append(source).GroupBy(Native.Pointer).Select(g=>g.First()).Select(state=>
                    {
                        Require(Native.Int(Native.Get(state,"Version"))==version&&NativeGameStateEnvelope.Get(state) is null,
                            "Ownership probe requires every current client state to remain native-only and supported.");
                        foreach(var root in Roots(state))Protected.Add(Native.Pointer(root));
                        return(state,NativeGameStateEnvelope.NativeBytes(state,version),new Witness(state,version));
                    }).ToArray();
                SourceBytes=Published.Single(x=>Native.Same(x.State,source)).Bytes;
            }
            internal void Census(bool wholeNativeBytes)
            {
                var now=ReadRoles();Require(now.Length==Roles.Length&&now.Select((x,i)=>x.Name==Roles[i].Name&&Same(x.Value,Roles[i].Value)).All(x=>x),
                    "Ownership probe changed live client/manager field identities.");
                foreach(var item in Published)
                {
                    Require(NativeGameStateEnvelope.Get(item.State) is null,"Ownership probe bound a live client state.");item.Witness.Verify();
                    if(wholeNativeBytes)Require(NativeGameStateEnvelope.NativeBytes(item.State,Version).SequenceEqual(item.Bytes),
                        "Ownership probe changed a live native state.");
                }
            }
            internal object[] PublishedStates()=>Published.Select(x=>x.State).ToArray();
            internal void Collect(object state)
            {
                foreach(string name in new[]{"CommandStack","ActionStack"})
                {
                    var list=Native.Get(state,name);if(list is null)continue;
                    Require(Native.Int(Native.Get(list,"Count"))<=16384,"Private ownership history bound exceeded.");
                    foreach(var entry in Native.Items(list))
                    {long key=Native.Pointer(entry);Require(!Protected.Contains(key),"Private carrier aliases live history.");Carriers.TryAdd(key,entry);}
                }
            }
            internal object Detached()
            {
                object state=NewState();Require(!Protected.Contains(Native.Pointer(state)),"Private receiver aliases a published state.");Private.Add(state);
                using var input=new Buffer(SourceBytes);
                try
                {
                    Native.Invoke(Native.M("GameState","Deserialize",Reader,"System.Int32"),state,input.R,Version);
                    Require(input.Position==input.Length,"Private baseline read did not consume its native record.");
                    var roots=Roots(state).Select(Native.Pointer).ToHashSet();
                    Require(!roots.Overlaps(Protected)&&!roots.Overlaps(PrivateRoots),"Detached mutable state graph shares protected or sibling objects.");
                    PrivateRoots.UnionWith(roots);return state;
                }
                finally{Collect(state);}
            }
            internal void Bind(object state,Guid epoch,long revision=0)
            {
                Require(Private.Any(x=>Native.Same(x,state)),"Cannot bind a nonprivate receiver.");
                var ledger=NativeLedgerState.Bootstrap(state,epoch);
                Require(ledger.Units.Length>0&&ledger.Receipts.Length==0&&ledger.Units.All(x=>x.InvestmentKind==PaidInvestmentKind.UnknownLegacy),
                    "Ownership probe needs a real existing supported Mimic, with no invented payment or command receipt.");
                byte[] payload=NativeIdentityLedgerCodec.Encode(ledger,revision);NativeLedgerState.Validate(state,revision,payload);
                NativeGameStateEnvelope.Set(state,revision,payload);
            }
            internal void Begin()=>Context=NativeLedgerOwnership.BeginRequiredContext(Client,GameId,Epoch);
            internal void Retire(){Require(Context is not null,"No owned context to retire.");NativeLedgerOwnership.Retire(Context!);Context=null;}
            internal void Cleanup(Report r)
            {
                if(Context is not null)
                {
                    try{Retire();}catch(Exception e){Fail(r,"Context retirement",e);}
                }
                try{Census(Context is null);if(Context is null)r.Checks.Add("all-live-state-bytes-unbound-bindings-and-client-fields-unchanged-after-retirement");}
                catch(Exception e){Fail(r,"Published source verification",e);}
                foreach(var state in Private)
                {
                    try
                    {
                        Collect(state);
                        // Faulted adopted writers remain blocked even after
                        // retirement. Never introduce a raw-writer bypass here.
                        if(Context is null&&!Faulted.Contains(Native.Pointer(state)))
                            Require(NativeGameStateEnvelope.NativeBytes(state,Version).SequenceEqual(SourceBytes),"Private receiver native bytes changed.");
                        NativeGameStateEnvelope.Forget(state);
                    }
                    catch(Exception e){Fail(r,"Private receiver cleanup",e);try{NativeGameStateEnvelope.Forget(state);}catch(Exception cleanup){Fail(r,"Private binding retirement",cleanup);}}
                }
                foreach(var carrier in Carriers.Values)
                    try{NativeExchangeCommands.Forget(carrier);MimicSmuggleActions.ForgetOffer(carrier);}catch(Exception e){Fail(r,"Private carrier cleanup",e);}
                Measure(r,"private-states-retired",Private.Count);Measure(r,"private-carriers-retired",Carriers.Count);
                Measure(r,"faulted-adopted-states-checked-with-field-witness-only",Faulted.Count);
            }
        }
        static void Fail(Report r,string label,Exception e){r.Passed=false;r.Failure=(r.Failure??"")+" "+label+": "+e.GetType().FullName+": "+e.Message;}

        static void ReadRejected(Report r,Trial t,object receiver,byte[] wire,string label,bool beforeCookie)
        {
            var witness=new Witness(receiver,t.Version);var binding=NativeGameStateEnvelope.Get(receiver);
            using var input=new Buffer(wire);object reader=input.R;long position=input.Position;byte[] contents=input.Snapshot();var before=Count();
            NativeReject(r,label,()=>Native.Invoke(Native.M("GameState","Deserialize",Reader,"System.Int32"),receiver,reader,t.Version));
            Rejected(r,label,before,beforeCookie);
            if(beforeCookie)Check(r,input.Position==position,label+"-input-position-unchanged");
            else Measure(r,label+"-parsed-input-position",input.Position); // Frame parsing may precede Epoch rejection.
            Check(r,input.Snapshot().SequenceEqual(contents)&&SameBinding(receiver,binding),label+"-input-bytes-and-binding-unchanged");
            witness.Verify();t.Census(false);r.Checks.Add(label+"-receiver-fields-and-live-census-unchanged");
        }
        static void WriteRejected(Report r,Trial t,object state,string label)
        {
            var witness=new Witness(state,t.Version);var binding=NativeGameStateEnvelope.Get(state);
            using(var output=new Buffer())
            {
                output.Bytes(new byte[]{0xd1,0x35,0x00,0x80,0xac});byte[] bytes=output.Snapshot();long position=output.Position;object writer=output.W;
                var before=Count();NativeReject(r,label+"-direct",()=>Native.Invoke(Native.M("GameState","Serialize",Writer,"System.Int32"),state,writer,t.Version));
                Rejected(r,label+"-direct",before,true);
                Check(r,output.Position==position&&output.Snapshot().SequenceEqual(bytes),label+"-caller-sentinel-buffer-unchanged");
            }
            byte[]? result=null;var rawBefore=Count();
            NativeReject(r,label+"-nativebytes",()=>result=NativeGameStateEnvelope.NativeBytes(state,t.Version));
            Rejected(r,label+"-nativebytes",rawBefore,true);
            Check(r,result is null&&SameBinding(state,binding),label+"-nativebytes-no-result-and-binding-unchanged");
            witness.Verify();t.Census(false);r.Checks.Add(label+"-native-fields-unchanged-without-state-writer-bypass");
        }
        static void ReadAllowed(Report r,Trial t,object receiver,byte[] wire,string label,long revision)
        {
            using var input=new Buffer(wire);var before=Count();int reads=NativeGameStateEnvelope.FramedReads;
            try{Native.Invoke(Native.M("GameState","Deserialize",Reader,"System.Int32"),receiver,input.R,t.Version);}
            finally{t.Collect(receiver);}
            var after=Count();var binding=NativeGameStateEnvelope.Get(receiver);
            Check(r,input.Position==input.Length&&binding is not null&&binding.Revision==revision&&
                NativeIdentityLedgerCodec.Decode(binding.Bytes(),revision).Epoch==Epoch&&NativeGameStateEnvelope.FramedReads==reads+1,
                label+"-real-framed-read-published-only-private-binding");
            Check(r,after.Prepared>before.Prepared&&after.Prepared-before.Prepared==after.Cleaned-before.Cleaned&&
                after.Allocated==before.Allocated+1&&NativeGameStateEnvelope.ActiveOperations==0,label+"-positive-control-balanced");
            Check(r,NativeGameStateEnvelope.NativeBytes(receiver,t.Version).SequenceEqual(t.SourceBytes),label+"-native-body-section-preserved");
            t.Census(false);
        }
        static void Ready(object client,Guid gameId,object source,int version)
        {
            Require(new[]{"DECEPTION_NATIVE_ENVELOPE_PROBE","DECEPTION_NATIVE_LEDGER_PROBE","DECEPTION_NATIVE_COMMAND_PROBE","DECEPTION_NATIVE_EXCHANGE_PROBE"}
                .Any(name=>Environment.GetEnvironmentVariable(name)=="1"),"An existing exact disposable diagnostic must be explicitly enabled.");
            Require(NativeStateBridge.Installed&&NativeGameStateEnvelope.ActiveOperations==0&&version==123&&gameId!=Guid.Empty,
                "Ownership probe requires idle installed bridge, serializer123 and explicit disposable Guid.");
            object? manager=Native.Static("GameManager","instance");
            Require(manager is not null&&!(bool)Native.Get(manager,"isLoadingGame")!&&!(bool)Native.Static("PopupManager","PopupShowing")!,
                "Ownership probe requires an existing ready manager; startup/null-singleton is not simulated.");
            Require(Native.Same(Native.Static("GameManager","Client"),client)&&
                Equals(Native.Get(client,"clientType"),Enum.Parse(Native.T("ClientBase+ClientType"),"Local"))&&
                (bool)Native.Get(client,"IsReady")!&&!(bool)Native.Get(client,"isSessionEnded")!&&
                !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!&&!(bool)Native.Get(client,"IsWaitingForCommand")!,
                "Ownership probe client is not the exact idle Local client.");
            string? id=(string?)Native.Invoke(Native.M("Il2CppSystem.Guid","ToString"),Native.Get(client,"gameId")!);
            Require(Guid.TryParseExact(id,"D",out var actual)&&actual==gameId,"Explicit disposable Guid does not match the current client.");
            Require(Native.Same(Native.Get(client,"GameState"),source)&&Native.Same(Native.Static("GameManager","GameState"),source)&&
                Native.Int(Native.Get(source,"Version"))==version&&Equals(Native.Get(source,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started")),
                "Supplied source is not the ready published disposable state.");
            object? actions=Native.Get(client,"ActionManager");
            Require(actions is not null&&!(bool)Native.Get(actions,"IsProcessing")!&&!(bool)Native.Get(actions,"IsSimulating")!&&Native.Same(Native.Get(actions,"GameState"),source),
                "Ownership probe requires the current idle nonsimulating action manager.");
            foreach(string name in new[]{"ActionStack","pendingCommandTriggers"})
                Require(Native.Int(Native.Get(Native.Get(source,name)!,"Count"))==0,"Ownership probe requires no queued or paused work.");
        }

        internal static Report Run(object suppliedClient,Guid disposableGameId,object suppliedSource,int version)
        {
            var r=new Report{Scope="detached-native-ledger-ownership-policy",Limit=
                "Tests explicit ownership policy with private real-ledger states; " +
                "does not implement or prove native create/open/adoption hooks, reload enrollment, saving, gameplay, replay, GC reclamation, " +
                "pointer reuse, cross-thread access or startup without a GameManager. RecordAdopted names a private diagnostic receiver only. " +
                "Faulted adopted receivers use field/binding witnesses and are never serialized through a policy bypass."};
            if(Running){r.Failure="Ownership probe reentry rejected.";return r;}Running=true;
            Trial? t=null;var allBefore=Count();
            try
            {
                Ready(suppliedClient,disposableGameId,suppliedSource,version);t=new(suppliedClient,disposableGameId,suppliedSource,version);
                // All baseline deserialization/bindings/wires are prepared before
                // required mode; no legacy copy is attempted during that mode.
                object donor=t.Detached(),seed=t.Detached(),otherSeed=t.Detached(),adopted=t.Detached(),wrongWriter=t.Detached(),
                    missing=t.Detached(),wrongFrame=t.Detached(),legacy=t.Detached(),fresh=t.Detached(),faulted=t.Detached();
                foreach(object state in new[]{donor,seed,otherSeed,adopted,faulted})t.Bind(state,Epoch,state==donor?1:0);
                t.Bind(wrongWriter,OtherEpoch);
                byte[] good=Serialize(donor,version),wrong=Serialize(wrongWriter,version);
                Check(r,NativeStateFrame.Decode(good,version).Native.SequenceEqual(t.SourceBytes)&&
                    NativeIdentityLedgerCodec.Decode(NativeStateFrame.Decode(wrong,version).Extension,0).Epoch==OtherEpoch,
                    "valid-native-and-wrong-epoch-framed-controls-prepared-before-context");
                t.Census(true);
                // Establish the always-on pre-cookie path with no decodable
                // input before exercising either valid format on the source.
                ReadRejected(r,t,t.Source,Array.Empty<byte>(),"published-outside-context-empty-preflight",true);
                ReadRejected(r,t,t.Source,good,"published-outside-context-valid-framed",true);
                ReadRejected(r,t,t.Source,t.SourceBytes,"published-outside-context-valid-legacy",true);
                t.Census(true);t.Begin();
                for(int i=0;i<t.PublishedStates().Length;i++)
                    ReadRejected(r,t,t.PublishedStates()[i],Array.Empty<byte>(),"published-receiver-"+i,true);
                Measure(r,"distinct-published-receivers-tested",t.PublishedStates().Length);
                ReadRejected(r,t,seed,Array.Empty<byte>(),"bound-private-without-permit",true);
                using(var permit=NativeLedgerOwnership.PermitSeededPrivateRead(t.Client,t.GameId,seed))
                {
                    ReadRejected(r,t,otherSeed,Array.Empty<byte>(),"permit-does-not-cover-other-private-receiver",true);
                    ReadAllowed(r,t,seed,good,"exact-seeded-permit",1);
                    ReadRejected(r,t,seed,Array.Empty<byte>(),"one-shot-permit-reuse",true);
                }
                ReadRejected(r,t,seed,Array.Empty<byte>(),"disposed-permit",true);
                ReadAllowed(r,t,fresh,good,"fresh-unbound-framed-receiver",1);
                using(var permit=NativeLedgerOwnership.PermitSeededPrivateRead(t.Client,t.GameId,adopted))
                {
                    NativeLedgerOwnership.RecordAdopted(t.Context!,adopted);
                    ReadRejected(r,t,adopted,Array.Empty<byte>(),"adoption-revokes-previous-private-permit",true);
                }
                ManagedReject(r,"cannot-issue-permit-for-adopted-state",()=>
                {using var invalid=NativeLedgerOwnership.PermitSeededPrivateRead(t.Client,t.GameId,adopted);});
                Check(r,NativeGameStateEnvelope.NativeBytes(adopted,version).SequenceEqual(t.SourceBytes),"adopted-bound-writer-allowed-in-required-context");
                WriteRejected(r,t,missing,"required-unbound-private-writer");
                WriteRejected(r,t,t.Source,"required-unbound-published-writer");
                WriteRejected(r,t,wrongWriter,"required-wrong-epoch-writer");
                ReadRejected(r,t,wrongFrame,wrong,"required-wrong-epoch-frame",false);
                ReadRejected(r,t,legacy,t.SourceBytes,"required-legacy-frame",false);
                using(var stale=NativeLedgerOwnership.PermitSeededPrivateRead(t.Client,t.GameId,otherSeed))
                {
                    t.Retire();
                    ReadRejected(r,t,otherSeed,Array.Empty<byte>(),"retirement-invalidates-issued-private-permit",true);
                }
                ReadRejected(r,t,adopted,Array.Empty<byte>(),"ever-adopted-receiver-after-retirement",true);
                ReadRejected(r,t,adopted,good,"ever-adopted-after-retirement-valid-framed",true);
                ReadRejected(r,t,adopted,t.SourceBytes,"ever-adopted-after-retirement-valid-legacy",true);
                Check(r,NativeGameStateEnvelope.NativeBytes(adopted,version).SequenceEqual(t.SourceBytes),"retired-valid-adopted-writer-retains-own-epoch");
                t.Census(true);
                t.Begin();WriteRejected(r,t,adopted,"old-adopted-generation-under-new-context");
                NativeLedgerOwnership.RecordAdopted(t.Context!,faulted);
                var faultWitness=new Witness(faulted,version);var faultBinding=NativeGameStateEnvelope.Get(faulted);
                t.Faulted.Add(Native.Pointer(faulted));NativeLedgerOwnership.Fault(t.Context!);
                WriteRejected(r,t,faulted,"faulted-adopted-writer");t.Retire();
                WriteRejected(r,t,faulted,"faulted-adopted-writer-after-retirement");
                ReadRejected(r,t,faulted,Array.Empty<byte>(),"faulted-ever-adopted-receiver-after-retirement",true);
                faultWitness.Verify();Check(r,SameBinding(faulted,faultBinding),"fault-retirement-preserves-protected-private-body-and-binding");
                t.Census(true);Measure(r,"source-native-bytes",t.SourceBytes.Length);Measure(r,"source-native-sha256",Hash(t.SourceBytes));r.Passed=true;
            }
            catch(Exception e){Fail(r,"Ownership trial",e);}
            finally
            {
                if(t is not null)t.Cleanup(r);
                try
                {
                    var after=Count();Check(r,after.Prepared>allBefore.Prepared&&after.Prepared-allBefore.Prepared==after.Cleaned-allBefore.Cleaned&&
                        NativeGameStateEnvelope.ActiveOperations==0,"complete-ownership-trial-native-bridge-balanced");
                }
                catch(Exception e){Fail(r,"Final native bridge verification",e);}
                Running=false;
            }
            return r;
        }
    }
}
