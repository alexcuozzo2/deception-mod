using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Deception.Core;
using C=Deception.PolyModAdapter.NativeTrainStreamContext;
using P=Deception.PolyModAdapter.NativePaidBirthCommands;
using A=Deception.PolyModAdapter.NativeTrainLeafProbeAdapter;
using D=Deception.PolyModAdapter.NativeTrainLeafProbeDriver;
using B=Deception.PolyModAdapter.NativeStateBridge;
using Report=Deception.PolyModAdapter.NativeStateProbe.Report;

namespace Deception.PolyModAdapter
{
    // DRAFT: private native Train Serialize/Deserialize only. No Execute,
    // client publication, state decoding, state ledger, research or save writes.
    internal static class NativeTrainLeafProbe
    {
        const int Version=123;
        const string UnitType="Polytopia.Data.UnitData+Type",NativeFailure="Il2CppInterop.Runtime.Il2CppException";
        const ushort Sentinel=0xA65C;
        static bool Running,Attempted;
        static readonly Guid DiagnosticEpoch=new("a144a273-cd26-49da-821f-1c28106a35f9");
        readonly record struct Counts(long Prepared,long Finished,long Cleaned,long Rejected);
        readonly record struct Image(string Rule,byte Actor,int X,int Y,int Cost,int LookX,int LookY,bool Aborted);
        sealed record Baseline(P.Kind Kind,string Rule,byte[] Bytes,Image Fields);
        static Counts Count()=>new(B.Prepared,B.Finished,B.Cleaned,B.Rejected);
        static string Owner(P.Kind kind)=>kind==P.Kind.Command?"TrainCommand":"TrainAction";
        static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Train leaf probe: "+why);}
        static void Check(Report report,bool ok,string why){Require(ok,why);report.Checks.Add(why);}
        static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        static void Measure(Report report,string key,object value)=>report.Measurements[key]=Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture)??"null";
        static bool Same(object? a,object? b)=>a is null?b is null:b is not null&&Native.Same(a,b);
        static object Coordinate(int x,int y)=>Native.T("WorldCoordinates").GetConstructor(new[]{typeof(int),typeof(int)})!.Invoke(new object[]{x,y});
        static void Seed(object value,P.Kind kind,string rule,byte actor)
        {
            Native.Set(value,"Type",Native.EnumValue(UnitType,rule));Native.Set(value,"PlayerId",actor);
            Native.Set(value,"Coordinates",Coordinate(4,5));
            if(kind==P.Kind.Action)
            {Native.Set(value,"Cost",rule=="warrior"?2:3);Native.Set(value,"LookCoordinates",Coordinate(6,7));Native.Set(value,"trainActionAborted",false);}
        }
        static Image Fields(object value,P.Kind kind)
        {
            object at=Native.Get(value,"Coordinates")!;object? look=kind==P.Kind.Action?Native.Get(value,"LookCoordinates"):null;
            return new(Native.EnumName(Native.Get(value,"Type")!),Convert.ToByte(Native.Get(value,"PlayerId")),Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")),
                kind==P.Kind.Action?Native.Int(Native.Get(value,"Cost")):0,look is null?0:Native.Int(Native.Get(look,"X")),
                look is null?0:Native.Int(Native.Get(look,"Y")),kind==P.Kind.Action&&(bool)Native.Get(value,"trainActionAborted")!);
        }
        static NativePaidBirthIntent Intent(string rule,byte actor,ulong sequence)=>new(DiagnosticEpoch,sequence,0,1,1,actor,rule,4,5,20,3);
        static object[] Items(object owner,string name,int max=16384)
        {
            object? list=Native.Get(owner,name);if(name=="pendingCommandTriggers"&&list is null)return Array.Empty<object>();
            Require(list is not null,"Missing required "+name);int n=Native.Int(Native.Get(list!,"Count"));Require(n>=0&&n<=max,name+" bound exceeded");
            var values=Native.Items(list!).ToArray();Require(values.Length==n,name+" count changed");return values;
        }
        static object[] Tiles(object state)
        {
            object values=Native.Get(Native.Get(state,"Map")!,"Tiles")!;int count=Native.Int(Native.Get(values,"Length"));Require(count is >0 and <=4096,"Map bound exceeded");
            var item=Native.M(values.GetType(),"get_Item","System.Int32");return Enumerable.Range(0,count).Select(i=>Native.Invoke(item,values,i)!).ToArray();
        }
        static IEnumerable<object> Roots(object state)
        {
            yield return state;
            foreach(string name in new[]{"Map","Settings","PlayerStates","ActionStack","CommandStack"})yield return Native.Get(state,name)!;
            if(Native.Get(state,"pendingCommandTriggers") is object triggers)yield return triggers;
            if(Native.Get(Native.Get(state,"Settings")!,"rules") is object rules)yield return rules;
            yield return Native.Get(Native.Get(state,"Map")!,"Tiles")!;
            foreach(object tile in Tiles(state))
            {
                yield return tile;
                foreach(string name in new[]{"explorers","effects","improvement","resource"})if(Native.Get(tile,name) is object value)yield return value;
                if(Native.Get(tile,"improvement") is object improvement)
                    foreach(string name in new[]{"effects","rewards"})if(Native.Get(improvement,name) is object value)yield return value;
            }
            foreach(object player in Items(state,"PlayerStates",32))
            {
                yield return player;
                foreach(string name in new[]{"availableTech","relations","unlockedTechCache"})if(Native.Get(player,name) is object value)yield return value;
                object relations=Native.Get(player,"relations")!;var lookup=Native.M(relations.GetType(),"TryGetValue","System.Byte","DiplomacyRelation&");
                for(int i=0;i<256;i++){object?[] args={(byte)i,null};if((bool)Native.Invoke(lookup,relations,args)!){Require(args[1] is not null,"Null native relation");yield return args[1]!;}}
            }
            foreach(var body in NativeLedgerState.Graph(state).Values){yield return body.Unit;yield return Native.Get(body.Unit,"effects")!;}
            foreach(string name in new[]{"CommandStack","ActionStack"})foreach(object value in Items(state,name))yield return value;
        }
        static (string Name,object? Value)[] Roles()
        {
            object manager=Native.Static("GameManager","instance")??throw new InvalidDataException("Missing source manager");
            object client=Native.Static("GameManager","Client")??throw new InvalidDataException("Missing source client");
            var rows=new List<(string,object?)>{("GameManager.instance",manager),("GameManager.Client",client),("GameManager.GameState",Native.Static("GameManager","GameState"))};
            foreach(string name in new[]{"GameState","initialGameState","currentGameState","targetGameState","lastTurnGameState"})rows.Add(("Client."+name,Native.Get(client,name)));
            object? actions=Native.Get(client,"ActionManager");rows.Add(("Client.ActionManager",actions));if(actions is not null)rows.Add(("ActionManager.GameState",Native.Get(actions,"GameState")));
            return rows.ToArray();
        }
        internal sealed class SourceWitness
        {
            readonly object Client,Source;readonly int Thread=Environment.CurrentManagedThreadId;
            readonly (string Name,object? Value)[] BeforeRoles;
            readonly (object State,byte[] Bytes,long[] Roots)[] States;
            internal readonly object[] Protected;
            internal SourceWitness(object client,object source)
            {
                Client=client;Source=source;BeforeRoles=Roles();
                Require(Native.Same(client,NativeBehaviorSession.RequireCreatedClient())&&Native.Same(source,Native.Get(client,"GameState")),"Source creation/client differs");
                var roots=new List<object>();States=BeforeRoles.Select(x=>x.Value).Where(x=>x is not null&&Native.T("GameState").IsInstanceOfType(x)).Select(x=>x!).Append(source)
                    .GroupBy(Native.Pointer).Select(x=>x.First()).Select(state=>
                    {
                        SafeHistory(state);Require(NativeGameStateEnvelope.Get(state) is null,"Published state already has a ledger binding");
                        var all=Roots(state).ToArray();roots.AddRange(all);return(state,NativeStateProbe.Serialize(state,"GameState",Version),all.Select(Native.Pointer).ToArray());
                    }).ToArray();
                roots.AddRange(BeforeRoles.Where(x=>x.Value is not null).Select(x=>x.Value!));Protected=roots.GroupBy(Native.Pointer).Select(x=>x.First()).ToArray();
                Require(Protected.Length<8192,"Source census exceeds context bound");Verify();
            }
            static void SafeHistory(object state)
            {
                Require(Native.Int(Native.Get(state,"Version"))==Version,"Published sibling native version differs");
                // A complete source Serialize witness cannot bypass protection of
                // a live Train carrier. Restrict this first fresh-match trial to
                // source histories with no Train; do not weaken the dispatcher.
                Require(Items(state,"ActionStack").Length==0,"Published sibling has pending native actions");
                foreach(object command in Items(state,"CommandStack"))
                {
                    Require(Native.Int(Native.Invoke(Native.M("CommandBase","GetCommandType"),command))!=5,"Source has Train history");
                    Require(!MimicSmuggleActions.IsSmuggle(command)&&NativeExchangeCommands.Get(command) is null,"Source has custom command history");
                }
            }
            internal void Verify()
            {
                NativeRegistryOrder.RequireCompatible();Require(RecruitmentHooks.Registered,"Recruitment registration disappeared");
                Require(Thread==Environment.CurrentManagedThreadId&&NativeTrainLeafProbeTrigger.Ready(out var client,out var source,out _)&&
                    Native.Same(client,Client)&&Native.Same(source,Source),"Source lost exact idle creation authority");
                var current=Roles();Require(current.Length==BeforeRoles.Length&&current.Select((x,i)=>x.Name==BeforeRoles[i].Name&&Same(x.Value,BeforeRoles[i].Value)).All(x=>x),"Published role identity changed");
                foreach(var row in States)
                {
                    SafeHistory(row.State);Require(NativeGameStateEnvelope.Get(row.State) is null&&Roots(row.State).Select(Native.Pointer).SequenceEqual(row.Roots)&&
                        NativeStateProbe.Serialize(row.State,"GameState",Version).SequenceEqual(row.Bytes),"Published source bytes/roots/binding changed");
                }
            }
            internal void Describe(Report report)
            {Measure(report,"protected-native-roots",Protected.Length);Measure(report,"published-state-count",States.Length);for(int i=0;i<States.Length;i++)Measure(report,"source-"+i+"-native-sha256",Hash(States[i].Bytes));}
        }
        static void Balanced(Report report,Counts before,string label,int calls,int finishes,int rejected)
        {
            var after=Count();Check(report,after.Prepared-before.Prepared==calls&&after.Cleaned-before.Cleaned==calls&&
                after.Finished-before.Finished==finishes&&after.Rejected-before.Rejected==rejected,label+"-bridge-callback-counts");
            Check(report,C.CurrentDepth==0&&C.ActiveReservations==0&&!C.HasMutationPermit&&A.CurrentDepth==0,label+"-no-active-leaf-roots");
        }
        static void NativeLeaf(C.Scope scope,C.Carrier value,C.Buffer buffer)
        {
            using var call=A.Open(scope,value.Value,value.Kind,buffer,C.Direction.Read);
            Native.Invoke(Native.M(Owner(value.Kind),"Deserialize","Il2CppSystem.IO.BinaryReader","System.Int32"),value.Value,buffer.Stream(C.Direction.Read),Version);
            call.RequireCompleted();
        }
        static Exception? Failure(Action action){try{action();return null;}catch(Exception e){return e;}}
        static void ExpectedNativeFailure(Report report,SourceWitness witness,string label,Action action,int finishes,int rejected)
        {
            witness.Verify();var before=Count();Exception? error=Failure(action);
            Measure(report,label+"-caller-exception",error?.GetType().FullName??"none");
            Check(report,error?.GetType().FullName==NativeFailure,label+"-actual-native-exception");
            Balanced(report,before,label,1,finishes,rejected);witness.Verify();
        }
        internal static Report Run(object client,object source,int version)
        {
            var report=new Report{Scope="Private native Train leaf serializers and direct factories only",Limit="No Train Execute, paid birth, state decode/publication, live save/history framing, replay or A11 expanded-form support. Synthetic private intents are not payment. Native negative calls touch only disposable records/buffers. No native inlined outer-factory admission is tested."};
            if(Running||Attempted){report.Failure="Train leaf probe reentry/repeated attempt rejected.";return report;}
            SourceWitness? witness=null;C.Trial? trial=null;var owned=new List<C.Carrier>();var rawOwned=new List<object>();
            try
            {
                Require(version==Version&&!B.Installed&&!NativeFreshSession.Enabled,"Probe version/bridge/session guard failed");Running=true;Attempted=true;
                NativeRegistryOrder.RequireCompatible();Require(RecruitmentHooks.Registered,"Recruitment registry is not registered");
                Check(report,NativeRegistryIds.Entries.Count==38&&NativeRegistryIds.Entries.All(x=>Native.Int(Native.EnumValue(x.EnumType,x.Id))==x.Index),"all-38-native-registry-ids-unchanged");
                NativeTrainLeafProbeTrigger.ValidateMode();witness=new(client,source);witness.Describe(report);trial=new(NativeProtectedCensus.Normalize(witness.Protected));
                byte actor=Convert.ToByte(Native.Get(source,"CurrentPlayer"));
                C.Carrier New(P.Kind kind,string? rule=null)
                {var value=trial.NewCarrier(kind);owned.Add(value);if(rule is not null)trial.Mutate(value,x=>Seed(x,kind,rule,actor));return value;}
                object Raw(P.Kind kind,string? rule=null)
                {
                    object value=Native.T(Owner(kind)).GetConstructor(Type.EmptyTypes)!.Invoke(Array.Empty<object>());
                    Require(Native.Pointer(value)!=0&&!witness.Protected.Any(x=>Native.Same(x,value))&&!rawOwned.Any(x=>Native.Same(x,value))&&
                        !owned.Any(x=>Native.Same(x.Value,value)),"Raw diagnostic constructor aliases an existing identity");
                    rawOwned.Add(value);if(rule is not null)Seed(value,kind,rule,actor);return value;
                }
                var baselines=new List<Baseline>();
                foreach(var kind in new[]{P.Kind.Command,P.Kind.Action})foreach(string rule in new[]{"warrior",NativePaidBirthIntentCodec.Warrior,NativePaidBirthIntentCodec.Rider})
                {
                    Require(!B.Installed,"Baseline must precede all native detours");object value=Raw(kind,rule);Image image=Fields(value,kind);
                    byte[] bytes=NativeStateProbe.Serialize(value,Owner(kind),Version);Require(bytes.Length is >0 and <4096,"Raw Train baseline bound");
                    Check(report,Fields(value,kind)==image,"raw-"+kind+"-"+rule+"-fields-unchanged");baselines.Add(new(kind,rule,bytes,image));
                    Measure(report,"raw-"+kind+"-"+rule+"-sha256",Hash(bytes));
                }
                witness.Verify();var installCounts=Count();B.InstallTrainLeaves();
                Check(report,B.Installed&&Count()==installCounts,"train-only-install-without-leaf-call");
                ulong sequence=1;
                foreach(var baseline in baselines)
                {
                    string label=baseline.Kind+"-"+baseline.Rule;bool custom=baseline.Rule!="warrior";
                    witness.Verify();var before=Count();var value=New(baseline.Kind,baseline.Rule);
                    byte[] legacy=D.Write(trial,value,P.Wire.LegacyNative);
                    Check(report,legacy.SequenceEqual(baseline.Bytes)&&Fields(value.Value,value.Kind)==baseline.Fields,label+"-legacy-raw-native-parity");
                    var receiver=New(baseline.Kind);D.Read(trial,receiver,legacy,P.Wire.LegacyNative);
                    Check(report,Fields(receiver.Value,receiver.Kind)==baseline.Fields&&P.Get(receiver.Value,receiver.Kind) is null,label+"-legacy-read-fields-no-binding");
                    Balanced(report,before,label+"-legacy",2,2,0);
                    before=Count();byte[] v1=D.Write(trial,value,P.Wire.DcpbV1);
                    Check(report,v1.SequenceEqual(custom?baseline.Bytes.Concat(new byte[]{0}):baseline.Bytes),label+"-explicit-v1-unbound-tag");
                    var fromFactory=D.ReadFromFactory(trial,baseline.Kind,v1,P.Wire.DcpbV1);owned.Add(fromFactory);
                    Check(report,Fields(fromFactory.Value,fromFactory.Kind)==baseline.Fields&&P.Get(fromFactory.Value,fromFactory.Kind) is null,label+"-actual-native-factory-read");
                    Balanced(report,before,label+"-v1-unbound",2,2,0);
                    if(custom)
                    {
                        var intent=Intent(baseline.Rule,actor,sequence++);P.Set(value.Value,value.Kind,intent);before=Count();byte[] bound=D.Write(trial,value,P.Wire.DcpbV1);
                        byte[] expected=baseline.Bytes.Concat(new byte[]{1}).Concat(NativePaidBirthIntentCodec.Encode(intent)).ToArray();
                        Check(report,bound.SequenceEqual(expected),label+"-canonical-bound-native-prefix");
                        var copy=New(value.Kind);D.Read(trial,copy,bound,P.Wire.DcpbV1);
                        var factoryCopy=D.ReadFromFactory(trial,value.Kind,bound,P.Wire.DcpbV1);owned.Add(factoryCopy);
                        Check(report,P.Get(copy.Value,copy.Kind)==intent&&P.Get(factoryCopy.Value,factoryCopy.Kind)==intent&&
                            Fields(copy.Value,copy.Kind)==baseline.Fields&&Fields(factoryCopy.Value,factoryCopy.Kind)==baseline.Fields,label+"-bound-direct-and-factory-identities");
                        Balanced(report,before,label+"-bound",3,3,0);
                        ExpectedNativeFailure(report,witness,label+"-bound-legacy-denied",()=>D.Write(trial,value,P.Wire.LegacyNative),0,1);
                        Check(report,P.Get(value.Value,value.Kind)==intent,label+"-rejected-write-keeps-binding");
                        var invalid=New(value.Kind);byte[] badTag=baseline.Bytes.Concat(new byte[]{255}).ToArray();
                        ExpectedNativeFailure(report,witness,label+"-unknown-tag",()=>D.Read(trial,invalid,badTag,P.Wire.DcpbV1),1,1);
                        Check(report,P.Get(invalid.Value,invalid.Kind) is null,label+"-invalid-read-unbound");
                        var shortValue=New(value.Kind);ExpectedNativeFailure(report,witness,label+"-truncated-suffix",()=>D.Read(trial,shortValue,bound.Take(bound.Length-1).ToArray(),P.Wire.DcpbV1),1,1);
                        Check(report,P.Get(shortValue.Value,shortValue.Kind) is null,label+"-short-read-unbound");
                        // Earlier success is intentionally not accepted by the
                        // outer root when a later sibling fails.
                        var first=New(value.Kind);var second=New(value.Kind);before=Count();Exception? siblingError;
                        var buffer=trial.NewBuffer(bound.Concat(badTag).ToArray());
                        using(var scope=trial.Begin(buffer,new(0,A.NewCookie(),Version,C.Direction.Read),P.Wire.DcpbV1))
                        {
                            NativeLeaf(scope,first,buffer);Check(report,P.Get(first.Value,first.Kind)==intent,label+"-first-sibling-published-with-reservation");
                            siblingError=Failure(()=>NativeLeaf(scope,second,buffer));
                            Measure(report,label+"-sibling-exception",siblingError?.GetType().FullName??"none");
                            Check(report,siblingError?.GetType().FullName==NativeFailure&&scope.Root.Faulted,label+"-later-sibling-native-rejection");
                        }
                        Check(report,P.Get(first.Value,first.Kind) is null&&P.Get(second.Value,second.Kind) is null,label+"-outer-failure-retires-earlier-binding");
                        Balanced(report,before,label+"-sibling-rollback",2,2,1);
                    }
                    witness.Verify();
                }
                foreach(var kind in new[]{P.Kind.Command,P.Kind.Action})
                {
                    var baseline=baselines.Single(x=>x.Kind==kind&&x.Rule=="warrior");var before=Count();object value=Raw(kind,"warrior");
                    Check(report,NativeStateProbe.Serialize(value,Owner(kind),Version).SequenceEqual(baseline.Bytes),kind+"-unrelated-legacy-write-raw-parity");
                    object read=Raw(kind,"warrior");using(var buffer=new NativeStateProbe.Buffer(baseline.Bytes.Concat(BitConverter.GetBytes(Sentinel)).ToArray()))
                    {
                        Native.Invoke(Native.M(Owner(kind),"Deserialize","Il2CppSystem.IO.BinaryReader","System.Int32"),read,buffer.R,Version);
                        Check(report,buffer.Position==baseline.Bytes.Length&&Convert.ToUInt16(buffer.Read("UInt16"))==Sentinel&&buffer.Position==buffer.Length&&
                            Fields(read,kind)==baseline.Fields,kind+"-unrelated-native-read-exact-sentinel");
                    }
                    Balanced(report,before,kind+"-unrelated-original",2,2,0);witness.Verify();
                    var eof=New(kind);ExpectedNativeFailure(report,witness,kind+"-native-base-eof",()=>D.Read(trial,eof,Array.Empty<byte>(),P.Wire.LegacyNative),0,0);
                    Check(report,P.Get(eof.Value,eof.Kind) is null,kind+"-native-eof-unbound");
                    // Known owned identities cannot fall through outside a leaf
                    // invocation even with a new ordinary native stream.
                    var known=New(kind,"warrior");ExpectedNativeFailure(report,witness,kind+"-unscoped-owned-rejected",()=>NativeStateProbe.Serialize(known.Value,Owner(kind),Version),0,1);
                }
                Check(report,C.CurrentDepth==0&&C.ActiveReservations==0&&!C.HasMutationPermit&&A.CurrentDepth==0,"all-private-leaf-roots-completed");
                witness.Verify();report.Passed=true;
            }
            catch(Exception e){report.Passed=false;report.Failure=e.ToString();}
            finally
            {
                try{trial?.Dispose();}catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+"\nTrial cleanup: "+e;}
                try
                {
                    foreach(var value in owned)Require(P.Get(value.Value,value.Kind) is null,"Retired private binding survived cleanup");
                    Require(C.CurrentDepth==0&&C.ActiveReservations==0&&!C.HasMutationPermit&&A.CurrentDepth==0,"Final leaf/context roots survived");
                    witness?.Verify();Measure(report,"retired-private-carriers",owned.Count);Measure(report,"raw-private-carriers",rawOwned.Count);
                    Measure(report,"bridge-prepared",B.Prepared);Measure(report,"bridge-finished",B.Finished);Measure(report,"bridge-cleaned",B.Cleaned);Measure(report,"bridge-rejected",B.Rejected);
                    report.Checks.Add("final-private-cleanup-and-published-source-witness");
                }
                catch(Exception e){report.Passed=false;report.Failure=(report.Failure??"")+"\nFinal witness: "+e;}
                Running=false;GC.KeepAlive(rawOwned);GC.KeepAlive(owned);
            }
            return report;
        }
    }
}
