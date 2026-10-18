using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Deception.Core;
using B=Deception.PolyModAdapter.NativeExecutionBridge;
using WeakIdentity=Deception.PolyModAdapter.NativeLedgerOwnership.WeakIdentity;
using Report=Deception.PolyModAdapter.NativeStateProbe.Report;

namespace Deception.PolyModAdapter
{
    // Opt-in diagnostic, admitted by the exact fresh-session trigger. The four
    // native Execute sites remain separate from normal-launch Harmony hooks.
    // All deliberate native Execute calls below target detached states, except
    // custom-carrier rejection checks that cannot reach a source-state original.
    // This is execution-boundary evidence, not paid birth or exchange mechanics.
    internal static partial class NativeExecutionProbe
    {
        const int Version=123,MaxPrivate=48,MaxQueue=512;
        const string NativeFailure="Il2CppInterop.Runtime.Il2CppException";
        const string Improvement="Polytopia.Data.ImprovementData+Type",UnitType="Polytopia.Data.UnitData+Type";
        static readonly string[] Owners={"BuildCommand","BuildAction","TrainCommand","TrainAction"};
        static readonly object Gate=new();
        static readonly Dictionary<long,Registration> Registrations=new();
        static readonly Controller Control=new();
        static Trial? Active;
        static bool Attempted;
        [ThreadStatic] static Invocation? Current;
        internal static B.IRouter Router=>Control;

        enum Injection { None,Prepare,Finish,Cleanup }
        sealed class Registration
        {
            internal readonly WeakIdentity State;
            internal readonly int Thread;
            internal bool Retired;
            internal Registration(object state,int thread){State=new(state);Thread=thread;}
        }
        sealed class Invocation:IDisposable
        {
            internal readonly Trial Trial;
            internal readonly Registration Registered;
            internal readonly WeakIdentity Carrier;
            internal readonly uint Site;
            internal readonly B.Decision Decision;
            internal readonly Injection Inject;
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal readonly string Label;
            internal int Prepared,Finished,Cleaned;
            internal bool? OriginalRan;
            internal B.Outcome? Outcome;
            internal Invocation(Trial trial,object state,object carrier,uint site,B.Decision decision,Injection inject,string label)
            {
                Require(Current is null&&ReferenceEquals(Active,trial)&&trial.Thread==Thread,"Probe call scope is reentrant or on a different thread.");
                Require(decision!=B.Decision.Pass||!Custom(carrier,site),"A diagnostic Pass must use an ordinary stock carrier.");
                Trial=trial;Registered=trial.Registration(state);Carrier=new(carrier);Site=site;Decision=decision;Inject=inject;Label=label;Current=this;
            }
            internal bool Matches(B.Call call,object carrier)=>call.Site==Site&&Thread==Environment.CurrentManagedThreadId&&
                call.State.ToInt64()==Registered.State.Key&&call.Instance.ToInt64()==Carrier.Key&&Carrier.Matches(carrier)&&
                Registered.State.Target() is object state&&Native.Pointer(state)==call.State.ToInt64();
            public void Dispose(){Require(ReferenceEquals(Current,this),"Probe call scope was replaced.");Current=null;}
        }
        sealed class Operation:B.IOperation
        {
            readonly Invocation Call;
            internal Operation(Invocation call){Call=call;}
            public void Finish(bool originalRan)
            {
                Require(ReferenceEquals(Current,Call)&&Call.Thread==Environment.CurrentManagedThreadId&&Call.Prepared==1&&Call.Finished==0&&Call.Cleaned==0,
                    "Probe Finish identity/order differs.");
                Call.Finished++;Call.OriginalRan=originalRan;
                Require(originalRan==(Call.Decision==B.Decision.Pass),"Native bridge original-ran differs from selected decision.");
                if(Call.Inject==Injection.Finish)throw new InvalidDataException("Intentional detached execution Finish failure.");
            }
            public void Cleanup(B.Outcome outcome)
            {
                Require(ReferenceEquals(Current,Call)&&Call.Thread==Environment.CurrentManagedThreadId&&Call.Cleaned==0,"Probe Cleanup identity/order differs.");
                Call.Cleaned++;Call.Outcome=outcome;
                if(Call.Inject==Injection.Cleanup)throw new InvalidDataException("Intentional detached execution Cleanup failure.");
            }
        }
        sealed class Controller:B.IRouter
        {
            public B.Decision Prepare(B.Call call,out B.IOperation? operation)
            {
                operation=null;
                Require(call.Site<Owners.Length&&call.Instance!=IntPtr.Zero&&call.State!=IntPtr.Zero,"Invalid native Execute arguments.");
                Registration? registration;
                lock(Gate)
                {
                    Registrations.TryGetValue(call.State.ToInt64(),out registration);
                    if(registration is not null&&registration.State.Target() is null)
                    {Registrations.Remove(call.State.ToInt64());registration=null;}
                }
                object carrier=Wrap(Owners[call.Site],call.Instance);
                if(TryPreparePaid(call,carrier,out var paidDecision,out operation))return paidDecision;
                // A private paid claim is decisive even if an unrelated
                // diagnostic Invocation was accidentally left active.
                NativeGameStateEnvelope.RejectPrivateAccess(Wrap("GameState",call.State));
                if(registration is not null)
                {
                    Require(!registration.Retired&&registration.Thread==Environment.CurrentManagedThreadId&&Current is Invocation,
                        "Private or retired diagnostic receiver has no authorized Execute scope.");
                    var scope=Current!;
                    Require(ReferenceEquals(Active,scope.Trial)&&ReferenceEquals(registration,scope.Registered)&&scope.Matches(call,carrier)&&scope.Prepared==0,
                        "Private Execute differs from its exact state/carrier/site/thread scope.");
                    // Publish the cleanup handler before any injected exception.
                    operation=new Operation(scope);scope.Prepared++;
                    if(scope.Inject==Injection.Prepare)throw new InvalidDataException("Intentional detached execution Prepare failure.");
                    return scope.Decision;
                }
                // Ordinary source/native calls retain their original path.
                // Every own custom carrier is unavailable outside an exact
                // private scope; no source ordinary Execute is invoked by Run.
                return Custom(carrier,call.Site)?B.Decision.Reject:B.Decision.Pass;
            }
            public void Failed(B.Call? call,string stage,string reason)
            {
                // The bridge contains this callback. Keep it evidence-only;
                // expected private exceptions never fault a live session.
                var trial=Active;if(trial is null)return;
                lock(trial.Notifications)
                {
                    if(trial.Notifications.Count<128)trial.Notifications.Add((call?.Site,stage,reason));
                    else trial.NotificationOverflow=true;
                }
            }
        }
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        static void Check(Report report,bool ok,string label){Require(ok,label);report.Checks.Add(label);}
        static void Measure(Report report,string name,object? value)=>report.Measurements[name]=Convert.ToString(value,CultureInfo.InvariantCulture)??"null";
        static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        static void CloneDifference(Report report,byte[] expected,byte[] actual)
        {
            // Bounded local evidence for a failed native roundtrip. This never
            // accepts a difference or exposes a receiver for later execution.
            var before=NativeStateFrame.Decode(expected,Version);var after=NativeStateFrame.Decode(actual,Version);
            Measure(report,"clone-mismatch-source-frame-sha256",Hash(expected));Measure(report,"clone-mismatch-copy-frame-sha256",Hash(actual));
            Measure(report,"clone-mismatch-extension-equal",before.Revision==after.Revision&&before.Extension.SequenceEqual(after.Extension));
            Measure(report,"clone-mismatch-source-native-length",before.Native.Length);Measure(report,"clone-mismatch-copy-native-length",after.Native.Length);
            int common=Math.Min(before.Native.Length,after.Native.Length),first=0;while(first<common&&before.Native[first]==after.Native[first])first++;
            Measure(report,"clone-mismatch-first-native-offset",first);
            Measure(report,"clone-mismatch-source-native-sha256",Hash(before.Native));Measure(report,"clone-mismatch-copy-native-sha256",Hash(after.Native));
            const int EvidenceByteLimit=32768;
            if(before.Native.Length<=EvidenceByteLimit&&after.Native.Length<=EvidenceByteLimit)
            {
                Measure(report,"clone-mismatch-source-native-base64",Convert.ToBase64String(before.Native));
                Measure(report,"clone-mismatch-copy-native-base64",Convert.ToBase64String(after.Native));
            }
        }
        static bool Same(object? a,object? b)=>a is null?b is null:b is not null&&Native.Same(a,b);
        static object Wrap(string owner,IntPtr pointer)=>Native.T(owner).GetConstructor(new[]{typeof(IntPtr)})!.Invoke(new object[]{pointer});
        static object New(string owner,Type[] types,params object?[] values)
        {
            try{return Native.T(owner).GetConstructor(types)?.Invoke(values)??throw new MissingMethodException(owner,"Exact constructor is absent.");}
            catch(TargetInvocationException e)when(e.InnerException is not null)
            {System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
        }
        static bool Custom(object carrier,uint site)
        {
            string type=Native.EnumName(Native.Get(carrier,"Type")!);
            if(site>=2)return RecruitmentCatalog.IsMimicId(type);
            return type.StartsWith("dcp_smuggle_",StringComparison.Ordinal)||type.StartsWith(NativeExchangeCatalog.Prefix,StringComparison.Ordinal)||
                NativeExchangeCommands.Get(carrier) is not null||MimicSmuggleActions.HasPayload(carrier);
        }
        static byte[] Bytes(object state)=>NativeStateProbe.Serialize(state,"GameState",Version);
        static byte[] NativeBytes(object state)=>NativeGameStateEnvelope.NativeBytes(state,Version);
        static NativeGameStateEnvelope.Snapshot Empty(object state)
        {
            var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Execution probe requires a real enrolled ledger.");
            var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
            Require(snapshot.Revision==0&&ledger.NextLogicalId==1&&ledger.NextSequence==1&&ledger.Units.Length==0&&ledger.Receipts.Length==0,
                "Execution probe requires an empty fresh-session ledger.");
            NativeLedgerState.Validate(state,snapshot.Revision,snapshot.Bytes());return snapshot;
        }
        static object[] Items(object owner,string name,int bound=MaxQueue)
        {
            object? list=Native.Get(owner,name);
            // The native trigger list is lazy; native lookup treats null as no
            // pending trigger. Other lists retain their required allocation.
            if(name=="pendingCommandTriggers"&&list is null)return Array.Empty<object>();
            object present=list??throw new InvalidDataException("Missing native "+name+".");
            int count=Native.Int(Native.Get(present,"Count"));Require(count>=0&&count<=bound,"Native "+name+" exceeds probe bounds.");return Native.Items(present).ToArray();
        }
        static object[] Tiles(object state)
        {
            object values=Native.Get(Native.Get(state,"Map")!,"Tiles")!;int count=Native.Int(Native.Get(values,"Length"));Require(count is >0 and <=4096,"Probe map bound exceeded.");
            var item=Native.M(values.GetType(),"get_Item","System.Int32");return Enumerable.Range(0,count).Select(i=>Native.Invoke(item,values,i)!).ToArray();
        }
        static IEnumerable<object> Roots(object state)
        {
            yield return state;
            foreach(string name in new[]{"Map","Settings","PlayerStates","ActionStack","CommandStack"})yield return Native.Get(state,name)!;
            // Presence/identity remains part of the ordered root witness: a
            // later allocation or removal changes that witness and must fail.
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
            foreach(object player in Items(state,"PlayerStates",16))
            {
                yield return player;
                foreach(string name in new[]{"availableTech","relations","unlockedTechCache"})if(Native.Get(player,name) is object value)yield return value;
                object relations=Native.Get(player,"relations")!;var lookup=Native.M(relations.GetType(),"TryGetValue","System.Byte","DiplomacyRelation&");
                for(int i=0;i<256;i++){object?[] args={(byte)i,null};if((bool)Native.Invoke(lookup,relations,args)!){Require(args[1] is not null,"Null native relation.");yield return args[1]!;}}
            }
            foreach(var body in NativeLedgerState.Graph(state).Values){yield return body.Unit;yield return Native.Get(body.Unit,"effects")!;}
            foreach(string name in new[]{"CommandStack","ActionStack"})foreach(object value in Items(state,name,16384))yield return value;
        }
        static (string Name,object? Value)[] ReadRoles()
        {
            var result=new List<(string,object?)>();object? manager=Native.Static("GameManager","instance");result.Add(("GameManager.instance",manager));if(manager is null)return result.ToArray();
            object? client=Native.Static("GameManager","Client");result.Add(("GameManager.Client",client));if(client is null)return result.ToArray();
            result.Add(("GameManager.GameState",Native.Static("GameManager","GameState")));
            foreach(string name in new[]{"GameState","initialGameState","currentGameState","targetGameState","lastTurnGameState"})result.Add(("Client."+name,Native.Get(client,name)));
            object? actions=Native.Get(client,"ActionManager");result.Add(("Client.ActionManager",actions));if(actions is not null)result.Add(("ActionManager.GameState",Native.Get(actions,"GameState")));
            return result.ToArray();
        }
        sealed class Trial
        {
            readonly Report Evidence;
            internal readonly object Source;
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal readonly byte[] SourceBytes;
            internal readonly List<object> States=new();
            internal readonly Dictionary<long,object> Carriers=new();
            readonly Dictionary<long,NativeGameStateEnvelope.PrivateClaim> Claims=new(MaxPrivate);
            internal readonly List<(uint? Site,string Stage,string Reason)> Notifications=new();
            internal bool NotificationOverflow;
            readonly HashSet<long> Protected=new(),PrivateRoots=new();
            readonly (string Name,object? Value)[] Roles;
            readonly (object State,byte[] Frame,byte[] Native,NativeGameStateEnvelope.Snapshot Binding,long[] Roots)[] Published;
            internal Trial(object source,Report evidence)
            {
                Evidence=evidence;Source=source;Roles=ReadRoles();
                Published=Roles.Select(x=>x.Value).Where(x=>x is not null&&Native.T("GameState").IsInstanceOfType(x)).Select(x=>x!).Append(source)
                    .GroupBy(Native.Pointer).Select(x=>x.First()).Select(state=>
                    {
                        Require(Native.Int(Native.Get(state,"Version"))==Version,"Published sibling version differs.");
                        long[] roots=Roots(state).Select(Native.Pointer).ToArray();Protected.UnionWith(roots);
                        var binding=Empty(state);return(state,Bytes(state),NativeBytes(state),binding,roots);
                    }).ToArray();
                SourceBytes=Published.Single(x=>Native.Same(x.State,source)).Frame;
            }
            internal void SourceReady()
            {
                Require(Thread==Environment.CurrentManagedThreadId&&NativeFreshSession.TryGetExecutionSource(out var source,out string reason)&&Native.Same(source,Source),
                    "Exact fresh execution source is no longer available.");
            }
            internal void VerifyPublished()
            {
                SourceReady();var now=ReadRoles();Require(now.Length==Roles.Length&&now.Select((r,i)=>r.Name==Roles[i].Name&&Same(r.Value,Roles[i].Value)).All(x=>x),"Execution probe changed published role identities.");
                foreach(var item in Published)
                {
                    var current=Empty(item.State);Require(Roots(item.State).Select(Native.Pointer).SequenceEqual(item.Roots)&&current.Revision==item.Binding.Revision&&current.Bytes().SequenceEqual(item.Binding.Bytes())&&
                        Bytes(item.State).SequenceEqual(item.Frame)&&NativeBytes(item.State).SequenceEqual(item.Native),"Execution probe changed a published state or binding.");
                }
            }
            internal object Clone()
            {
                SourceReady();Require(States.Count<MaxPrivate,"Private receiver bound exhausted.");
                // Source has no custom action/history carriers; a failed native
                // Deserialize is already cleaned by the state-frame bridge.
                object state=NativeStateProbe.DeserializeState(SourceBytes,Version);
                Require(!Protected.Contains(Native.Pointer(state)),"Detached clone aliases a protected source.");States.Add(state);
                var roots=Roots(state).Select(Native.Pointer).ToHashSet();Require(!roots.Overlaps(Protected)&&!roots.Overlaps(PrivateRoots),"Detached mutation graph aliases published or sibling objects.");
                PrivateRoots.UnionWith(roots);Empty(state);
                byte[] copiedBytes=Bytes(state);
                if(!copiedBytes.SequenceEqual(SourceBytes))
                {
                    try{VerifyNativeNatureColor(state,copiedBytes);}
                    catch{CloneDifference(Evidence,SourceBytes,copiedBytes);throw;}
                }
                lock(Gate)
                {
                    foreach(long dead in Registrations.Where(p=>p.Value.State.Target() is null).Select(p=>p.Key).ToArray())Registrations.Remove(dead);
                    Require(Registrations.Count<MaxPrivate&&!Registrations.ContainsKey(Native.Pointer(state)),"Native private receiver already registered or registry full.");
                    Registrations.Add(Native.Pointer(state),new Registration(state,Thread));
                }
                Collect(state);return state;
            }
            void VerifyNativeNatureColor(object state,byte[] canonical)
            {
                // Native123 resolves the neutral player's raw unset color on
                // read. Only this exact observed case is admitted. Whole-frame
                // equality below still rejects any other native/ledger change.
                object[] originals=Items(Source,"PlayerStates",16),copies=Items(state,"PlayerStates",16);
                Require(originals.Length==copies.Length&&originals.Select(p=>Native.Int(Native.Get(p,"Id"))).SequenceEqual(
                    copies.Select(p=>Native.Int(Native.Get(p,"Id")))),"Native copied player identity/order differs.");
                object original=originals.Single(p=>Native.Int(Native.Get(p,"Id"))==255);
                object copy=copies.Single(p=>Native.Int(Native.Get(p,"Id"))==255);
                object tribe=Native.Get(original,"tribe")!,skin=Native.Get(original,"skinType")!;
                object data=Native.Get(state,"GameLogicData")!;
                Require(Native.Int(Native.Get(original,"color"))==-1&&Equals(tribe,Native.Get(copy,"tribe"))&&
                    Equals(skin,Native.Get(copy,"skinType"))&&Native.Same(data,Native.Get(Source,"GameLogicData")),
                    "Copy difference is not the observed native neutral-color default.");
                int color=Native.Int(Native.Get(copy,"color"));
                int expected=Native.Int(Native.Invoke(Native.M(RecruitmentHooks.GLD,"GetTribeColor",
                    "PolytopiaBackendBase.Common.TribeType","PolytopiaBackendBase.Common.SkinType"),data,tribe,skin));
                Require(color==expected&&color!=-1,"Copied neutral color differs from native tribe/skin default.");
                // Set requires independently proven private mutable ownership.
                // Restore the actual native-decoded value even if proof fails.
                try
                {
                    Set(copy,"color",-1);
                    Require(Bytes(state).SequenceEqual(SourceBytes),"Native copy differs beyond the neutral default color.");
                }
                finally{Set(copy,"color",color);}
                Require(Bytes(state).SequenceEqual(canonical),"Private native-decoded color was not restored exactly.");
                VerifyPublished();
                Check(Evidence,true,"clone-"+States.Count+"-native-neutral-color-default-only-and-source-unchanged");
            }
            internal Registration Registration(object state)
            {
                lock(Gate){Require(Registrations.TryGetValue(Native.Pointer(state),out var item)&&!item.Retired&&item.State.Matches(state)&&States.Any(x=>Native.Same(x,state)),"Receiver lacks private authority.");return item!;}
            }
            internal NativeGameStateEnvelope.PrivateClaim Claim(object state)
            {
                _=Registration(state);VerifyPublished();
                long key=Native.Pointer(state);Require(!Claims.ContainsKey(key),"Private state already has a paid claim.");
                var claim=NativeGameStateEnvelope.ClaimPrivate(state);
                try{Claims.Add(key,claim);return claim;}
                catch{claim.Retire(state);throw;}
            }
            internal object Own(object carrier)
            {long key=Native.Pointer(carrier);Require(!Protected.Contains(key),"Private carrier aliases a protected object.");Carriers.TryAdd(key,carrier);return carrier;}
            internal void Collect(object state)
            {
                foreach(string name in new[]{"CommandStack","ActionStack"})foreach(object value in Items(state,name,16384))Own(value);
            }
            internal void Set(object value,string field,object? replacement)
            {Require(PrivateRoots.Contains(Native.Pointer(value))&&!Protected.Contains(Native.Pointer(value)),"Probe attempted to edit an unowned mutable object.");Native.Set(value,field,replacement);}
            internal void Cleanup(Report report)
            {
                int retiredStates=0;
                foreach(object state in States)
                {
                    // Collection can fail after a partial native mutation.
                    // It must never prevent either authority from retiring.
                    try{Collect(state);}catch(Exception e){Fail(report,"private-carrier-collection",e);}
                    try
                    {
                        lock(Gate)if(Registrations.TryGetValue(Native.Pointer(state),out var item)&&item.State.Matches(state))item.Retired=true;
                    }
                    catch(Exception e){Fail(report,"private-registration-retirement",e);}
                    try
                    {
                        if(Claims.TryGetValue(Native.Pointer(state),out var claim))
                        {claim.Retire(state);Require(claim.RetiredWithoutBinding,"Private paid ledger survived retirement.");}
                        else
                        {NativeGameStateEnvelope.Forget(state);Require(NativeGameStateEnvelope.Get(state) is null,"Private ledger binding survived retirement.");}
                        retiredStates++;
                    }
                    catch(Exception e){Fail(report,"private-state-retirement",e);}
                }
                foreach(object value in Carriers.Values)
                    try{NativeExchangeCommands.Forget(value);MimicSmuggleActions.ForgetOffer(value);}catch(Exception e){Fail(report,"private-carrier-retirement",e);}
                Measure(report,"retired-private-states",retiredStates);Measure(report,"retired-private-carriers",Carriers.Count);
            }
        }
        readonly record struct Counts(long Prepared,long Finished,long Cleaned,long Rejected,ulong Allocated);
        static Counts Count()=>new(B.Prepared,B.Finished,B.Cleaned,B.Rejected,B.AllocatedOperations);
        static void VerifyCounts(Report report,Counts before,string label,bool finish)
        {
            var after=Count();Check(report,after.Prepared==before.Prepared+1&&after.Cleaned==before.Cleaned+1&&after.Finished==before.Finished+(finish?1:0)&&
                after.Allocated==before.Allocated+1&&B.ActiveOperations==0,label+"-one-native-call-and-exact-cleanup");
            Measure(report,label+"-bridge-rejections",after.Rejected-before.Rejected);
        }
        static Exception? Execute(object state,object carrier,uint site)
        {try{Native.Invoke(Native.M(Owners[site],"Execute","GameState"),carrier,state);return null;}catch(Exception e){return e;}}
        static void Failure(Report report,string label,Exception? error,bool expected)
        {
            Measure(report,label+"-native-exception",error?.GetType().FullName??"none");
            Check(report,expected?error?.GetType().FullName==NativeFailure:error is null,label+(expected?"-native-exception-reached-managed-caller":"-native-call-returned"));
        }
        static void Fail(Report report,string label,Exception e)
        {report.Passed=false;report.Failure=(report.Failure??"")+" "+label+": "+e.GetType().Name+": "+e.Message;}

        readonly record struct Position(int X,int Y);
        static Position At(object coordinates)=>new(Native.Int(Native.Get(coordinates,"X")),Native.Int(Native.Get(coordinates,"Y")));
        static object Player(object state,byte actor)
        {
            object?[] args={actor,null};Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&args[1] is not null,
                "Native witness actor is absent.");return args[1]!;
        }
        static byte Actor(object state)=>Convert.ToByte(Native.Get(state,"CurrentPlayer"));
        static bool Explored(object tile,byte actor)=>(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,actor)!;
        static bool City(object tile)=>(bool)Native.Invoke(Native.M("TileData","HasImprovement",Improvement),tile,Native.EnumValue(Improvement,"city"))!;
        static object Capital(object state,byte actor)=>Tiles(state).FirstOrDefault(tile=>Native.Int(Native.Get(tile,"owner"))==actor&&City(tile)&&Explored(tile,actor))
            ??throw new InvalidDataException("No explored owned native city for the detached witness.");
        static object Carrier(Trial trial,uint site,byte actor,object coordinates,string? custom=null)
        {
            string id=custom??(site<2?"harvestfruit":"warrior");
            var types=new List<Type>{typeof(byte),Native.T(site<2?Improvement:UnitType),Native.T("WorldCoordinates")};
            var args=new List<object?>{actor,Native.EnumValue(site<2?Improvement:UnitType,id),coordinates};
            if(site==1){types.Add(typeof(bool));args.Add(true);}
            if(site==3){types.Add(typeof(int));args.Add(custom is null?2:3);}
            return trial.Own(New(Owners[site],types.ToArray(),args.ToArray()));
        }
        static void Scoped(Report report,Trial trial,object state,object carrier,uint site,B.Decision decision,Injection inject,string label,bool originalThrows=false)
        {
            trial.VerifyPublished();Counts before=Count();bool reachedFinish=decision!=B.Decision.Reject&&inject!=Injection.Prepare&&!originalThrows;
            bool expectError=decision==B.Decision.Reject||inject!=Injection.None||originalThrows;
            using var scope=new Invocation(trial,state,carrier,site,decision,inject,label);
            Exception? error=Execute(state,carrier,site);
            Failure(report,label,error,expectError);VerifyCounts(report,before,label,reachedFinish);
            B.Outcome outcome=originalThrows?B.Outcome.OriginalException:decision==B.Decision.Reject||inject==Injection.Prepare?B.Outcome.PrepareRejected:
                inject==Injection.Finish?B.Outcome.FinishRejected:B.Outcome.Complete;
            Check(report,scope.Prepared==1&&scope.Finished==(reachedFinish?1:0)&&scope.Cleaned==1&&scope.Outcome==outcome,
                label+"-matching-handler-phases-and-cleanup-outcome");
            Check(report,reachedFinish?scope.OriginalRan==(decision==B.Decision.Pass):scope.OriginalRan is null,label+"-raw-original-return-evidence");
            Measure(report,label+"-cleanup-outcome",outcome);trial.VerifyPublished();
        }
        static void Normalize(Trial trial,object state,byte[] before,string label,params (object Value,string Field,object? Replacement)[] replacements)
        {
            var undo=new List<(object Value,string Field,object? Original)>();
            try
            {
                foreach(var item in replacements){undo.Add((item.Value,item.Field,Native.Get(item.Value,item.Field)));trial.Set(item.Value,item.Field,item.Replacement);}
                Require(Bytes(state).SequenceEqual(before),label+": native bytes differ outside the exact approved mutation set.");
            }
            finally{foreach(var item in undo.AsEnumerable().Reverse())trial.Set(item.Value,item.Field,item.Original);}
        }
        static object[] Tail(object state,object stack,object[] before)
        {
            Require(Native.Same(Native.Get(state,"ActionStack"),stack),"Native original replaced the action list.");object[] after=Items(state,"ActionStack");
            Require(after.Length>=before.Length&&before.Select((entry,i)=>Native.Same(entry,after[i])).All(x=>x),"Native original changed its queue prefix.");return after.Skip(before.Length).ToArray();
        }
        static void Commands(Report report,Trial trial,uint site,Injection injection=Injection.None)
        {
            object state=trial.Clone();byte actor=Actor(state);object city=Capital(state,actor),coordinates=Native.Get(city,"coordinates")!;
            object carrier=Carrier(trial,site,actor,coordinates);object stack=Native.Get(state,"ActionStack")!;object[] prefix=Items(state,"ActionStack");
            Require(prefix.Length==0,"Command witness requires an idle empty private action queue.");
            byte[] before=Bytes(state);string label=Owners[site]+"-ordinary-queue"+(injection==Injection.None?"":"-finish-failure");
            Scoped(report,trial,state,carrier,site,B.Decision.Pass,injection,label);
            object[] tail=Tail(state,stack,prefix);Check(report,tail.Length==3,label+"-three-native-children");
            object end=Native.As(tail[0],"EndCommandAction"),deferred=Native.As(tail[1],"RunDeferredAction"),action=Native.As(tail[2],Owners[site+1]);
            Check(report,new[]{end,deferred,action}.All(x=>Native.Int(Native.Get(x,"PlayerId"))==actor)&&
                Equals(Native.Get(end,"commandType"),Native.Invoke(Native.M("CommandBase","GetCommandType"),carrier))&&
                Equals(Native.Get(action,"Type"),Native.Get(carrier,"Type"))&&At(Native.Get(action,"Coordinates")!)==At(coordinates),label+"-native-command-scaffold-and-exact-carrier");
            if(site==0)Check(report,(bool)Native.Get(action,"DeductCost")!,label+"-native-paid-build-action");
            else
            {
                int cost=Native.Int(Native.Get(RecruitmentHooks.Data(state,"warrior"),"cost"));
                Check(report,cost==2&&Native.Int(Native.Get(action,"Cost"))==cost,label+"-native-warrior-cost");
            }
            trial.Collect(state);Normalize(trial,state,before,label,(state,"ActionStack",Native.NewList("ActionBase",prefix)));
            Check(report,true,label+"-only-queue-changed-no-debit-spawn-or-command-history-progress");
        }
        static void BuildNoop(Report report,Trial trial)
        {
            object state=trial.Clone();byte actor=Enumerable.Range(0,256).Select(x=>(byte)x).First(id=>!Items(state,"PlayerStates",16).Any(p=>Native.Int(Native.Get(p,"Id"))==id));
            object?[] missing={actor,null};Require(!(bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,missing)!&&missing[1] is null,
                "Build no-op actor is not absent according to the native lookup.");
            object city=Capital(state,Actor(state));object action=Carrier(trial,1,actor,Native.Get(city,"coordinates")!);
            // ActionBase allocates subActions lazily. CommitSubActionsToStack
            // returns for null or Count0. Preserve its exact identity too:
            // BuildAction.Serialize does not include this transient list.
            object? subActions=Native.Get(action,"subActions");
            Require((subActions is null||Items(action,"subActions").Length==0)&&
                Native.Same(Native.Get(state,"GameLogicData"),Native.Get(trial.Source,"GameLogicData")),"Build no-op witness is not a clean action with the current registry.");
            byte[] before=Bytes(state),actionBefore=NativeStateProbe.Serialize(action,"BuildAction",Version);
            Scoped(report,trial,state,action,1,B.Decision.Pass,Injection.None,"BuildAction-missing-player-native-noop");
            Check(report,Bytes(state).SequenceEqual(before)&&NativeStateProbe.Serialize(action,"BuildAction",Version).SequenceEqual(actionBefore),
                "BuildAction-missing-player-native-noop-state-and-action-bytes-unchanged");
            Check(report,Same(subActions,Native.Get(action,"subActions"))&&(subActions is null||Items(action,"subActions").Length==0),
                "BuildAction-missing-player-native-noop-transient-subactions-unchanged");
            Measure(report,"BuildAction-positive-harvest","NOT RUN: this increment proves the audited native missing-player no-op return only.");
        }
        static object[] UnexploredFootprint(object state,object city,object data,byte actor)
        {
            int range=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetSightRange","Polytopia.Data.UnitData"),null,data));
            if(Native.Int(Native.Get(city,"terrain"))==4)range=2;
            object area=Native.Invoke(Native.M("MapDataExtensions","GetAreaSorted","MapData","WorldCoordinates","System.Int32","System.Boolean","System.Boolean"),
                null,Native.Get(state,"Map"),Native.Get(city,"coordinates"),range,true,true)!;
            int count=Native.Int(Native.Get(area,"Length"));Require(count is >0 and <=25,"Training sight footprint exceeds its bound.");var get=Native.M(area.GetType(),"get_Item","System.Int32");
            return Enumerable.Range(0,count).Reverse().Select(i=>Native.Invoke(get,area,i)!).Where(tile=>!Explored(tile,actor)).ToArray();
        }
        static void Training(Report report,Trial trial)
        {
            object state=trial.Clone();byte actor=Actor(state);object player=Player(state,actor),city=Capital(state,actor),data=RecruitmentHooks.Data(state,"warrior");
            Require(Native.Int(Native.Get(data,"cost"))==2&&Native.Int(Native.Get(data,"health"))==100&&
                Items(data,"unitAbilities").Select(Native.EnumName).OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(new[]{"dash","fortify","land"}),"Stock single-body Warrior rules differ.");
            Require(Items(city,"effects").Length==0,"Training capital has unsupported tile effects.");
            object? prior=Native.Get(city,"unit");
            if(prior is not null)
            {
                Require(Native.EnumName(Native.Get(prior,"type")!)=="warrior"&&Native.Int(Native.Get(prior,"owner"))==actor&&Native.Get(prior,"passengerUnit") is null&&
                    !(bool)Native.Invoke(Native.M("UnitState","HasLeader"),prior)!&&!(bool)Native.Invoke(Native.M("UnitState","HasFollower"),prior)!&&Items(prior,"effects").Length==0,
                    "Private city occupant is not an ordinary single-body Warrior.");
                Position from=At(Native.Get(city,"coordinates")!);
                // A resource beneath this copied Warrior is unchanged by the
                // synthetic relocation. No move/harvest/arrival action runs.
                object destination=Tiles(state).FirstOrDefault(tile=>Native.Get(tile,"unit") is null&&Native.Get(tile,"improvement") is null&&
                    Native.Int(Native.Get(tile,"terrain"))==3&&Native.Int(Native.Get(tile,"owner"))==actor&&Items(tile,"effects").Length==0&&Explored(tile,actor)&&
                    Math.Max(Math.Abs(At(Native.Get(tile,"coordinates")!).X-from.X),Math.Abs(At(Native.Get(tile,"coordinates")!).Y-from.Y))==1)
                    ??throw new InvalidDataException("No clear adjacent owned explored field for private occupant setup.");
                trial.Set(city,"unit",null);trial.Set(destination,"unit",prior);trial.Set(prior,"coordinates",Native.Get(destination,"coordinates"));
            }
            object coordinates=Native.Get(city,"coordinates")!;int currency=Native.Int(Native.Get(player,"Currency"));uint next=Convert.ToUInt32(Native.Get(state,"CurrentUnitId"));
            Require(currency>=2&&next<uint.MaxValue&&Native.Get(city,"unit") is null,"Training witness lacks native funds/id/empty tile.");
            var graph=NativeLedgerState.Graph(state);var graphBefore=graph.ToDictionary(x=>x.Key,x=>(x.Value.Unit,NativeStateProbe.Serialize(x.Value.Unit,"UnitState",Version)));
            object[] exploration=UnexploredFootprint(state,city,data,actor),prefix=Items(state,"ActionStack");object stack=Native.Get(state,"ActionStack")!;
            Require(prefix.Length==0,"Training witness requires an empty private action queue.");
            byte[] before=Bytes(state);object action=Carrier(trial,3,actor,coordinates);
            Scoped(report,trial,state,action,3,B.Decision.Pass,Injection.None,"TrainAction-ordinary-empty-city");
            object spawned=Native.Get(city,"unit")??throw new InvalidDataException("Native TrainAction did not spawn a body.");var afterGraph=NativeLedgerState.Graph(state);
            Check(report,!(bool)Native.Get(action,"trainActionAborted")!&&Native.Int(Native.Get(player,"Currency"))==currency-2&&
                Convert.ToUInt32(Native.Get(state,"CurrentUnitId"))==next+1&&Convert.ToUInt32(Native.Get(spawned,"id"))==next+1&&afterGraph.Count==graphBefore.Count+1&&
                afterGraph.TryGetValue(next+1,out var body)&&body.Root&&Native.Same(body.Unit,spawned),"TrainAction-native-two-star-debit-and-new-root-id");
            Check(report,Native.EnumName(Native.Get(spawned,"type")!)=="warrior"&&Native.Same(Native.Get(spawned,"UnitData"),data)&&
                Native.Int(Native.Get(spawned,"owner"))==actor&&Native.Int(Native.Get(spawned,"health"))==100&&At(Native.Get(spawned,"coordinates")!)==At(coordinates)&&At(Native.Get(spawned,"home")!)==At(coordinates)&&
                (bool)Native.Get(spawned,"moved")!&&(bool)Native.Get(spawned,"attacked")!&&Native.Get(spawned,"passengerUnit") is null&&Items(spawned,"effects").Length==0,
                "TrainAction-native-body-profile-home-health-and-exhaustion");
            Check(report,graphBefore.All(p=>afterGraph.TryGetValue(p.Key,out var existing)&&Native.Same(existing.Unit,p.Value.Unit)&&
                NativeStateProbe.Serialize(existing.Unit,"UnitState",Version).SequenceEqual(p.Value.Item2)),"TrainAction-preserves-existing-native-bodies");
            object[] tail=Tail(state,stack,prefix);Check(report,tail.Length==exploration.Length+1,"TrainAction-only-expected-sight-and-score-children");
            for(int i=0;i<exploration.Length;i++)
            {
                object explore=Native.As(tail[i],"ExploreAction");Check(report,Native.Int(Native.Get(explore,"PlayerId"))==actor&&At(Native.Get(explore,"Coordinates")!)==At(Native.Get(exploration[i],"coordinates")!),"TrainAction-reverse-area-explore-"+i);
            }
            object score=Native.As(tail[^1],"IncreaseScoreAction");int expectedScore=Native.Int(Native.Invoke(Native.M("ScoreSheet","GetUnitScore","UnitState","GameState"),null,spawned,state));
            Check(report,expectedScore==10&&Native.Int(Native.Get(score,"Amount"))==expectedScore&&Native.Int(Native.Get(score,"PlayerId"))==actor&&
                Native.Int(Native.Get(score,"Delay"))==0&&At(Native.Get(score,"Source")!)==At(coordinates),"TrainAction-native-unit-score-queued-last");
            trial.Collect(state);Normalize(trial,state,before,"TrainAction",(state,"ActionStack",Native.NewList("ActionBase",prefix)),(player,"Currency",currency),(state,"CurrentUnitId",next),(city,"unit",null));
            Check(report,true,"TrainAction-only-spawn-debit-id-and-queue-changed-no-child-executed");
        }
        static void Decisions(Report report,Trial trial,uint site,B.Decision decision,Injection injection=Injection.None)
        {
            object state=trial.Clone();object city=Capital(state,Actor(state));string custom=site<2?NativeExchangeCatalog.Regroup:RecruitmentCatalog.Variant("warrior");
            object value=Carrier(trial,site,Actor(state),Native.Get(city,"coordinates")!,custom);byte[] before=Bytes(state);
            string label=Owners[site]+"-custom-"+decision+"-"+injection;
            Scoped(report,trial,state,value,site,decision,injection,label);
            Check(report,Bytes(state).SequenceEqual(before),label+"-no-native-state-mutation");
        }
        static void OriginalException(Report report,Trial trial)
        {
            object state=trial.Clone(),city=Capital(state,Actor(state));object command=Carrier(trial,0,Actor(state),Native.Get(city,"coordinates")!);
            object stack=Native.Get(state,"ActionStack")!;byte[] before=Bytes(state);
            try
            {
                // Audited CommandBase.Execute allocates EndCommandAction, then
                // raises its native null-list exception before any queue/state
                // write. This is a null PRIVATE field, never an invalid pointer.
                trial.Set(state,"ActionStack",null);
                Scoped(report,trial,state,command,0,B.Decision.Pass,Injection.None,"BuildCommand-native-original-exception",true);
            }
            finally{trial.Set(state,"ActionStack",stack);}
            Check(report,Bytes(state).SequenceEqual(before),"native-original-exception-restored-private-list-and-unchanged-body-currency");
        }
        static void UnscopedReject(Report report,Trial trial,object state,uint site,string label,string? custom)
        {
            trial.VerifyPublished();object city=Capital(state,Actor(state));object value=Carrier(trial,site,Actor(state),Native.Get(city,"coordinates")!,custom);
            byte[] before=Bytes(state);Counts counters=Count();Exception? error=Execute(state,value,site);
            Failure(report,label,error,true);VerifyCounts(report,counters,label,false);Check(report,Bytes(state).SequenceEqual(before),label+"-state-unchanged");trial.VerifyPublished();
        }
        internal static Report Run(object suppliedSource,int serializerVersion)
        {
            var report=new Report{Scope="detached-native-four-site-execution-bridge",Limit="Native Execute boundary only. BuildAction positive coverage is an audited missing-player no-op; no harvest is accepted. Genuine Warrior birth occurs only in a detached clone; its city is synthetically emptied if occupied by relocating the copied existing Warrior to an adjacent clear field before the training baseline. No child queue is executed, no Mimic is spawned, no ledger identity/payment receipt is attached, and no state is published or saved. Injected Finish failure deliberately leaves prior native private-state effects in place until that clone is discarded; it is not rollback."};
            Trial? trial=null;
            try
            {
                Require(serializerVersion==Version&&B.Installed&&B.ActiveOperations==0&&Current is null,"Execution bridge/version/scope is not ready.");
                Require(NativeFreshSession.TryGetExecutionSource(out var source,out string reason)&&Native.Same(source,suppliedSource),"Exact fresh-session source is unavailable: "+reason);
                lock(Gate){Require(!Attempted&&Active is null,"Execution probe can run only once per process.");Attempted=true;}
                Require(Native.Int(Native.Get(suppliedSource,"Version"))==Version&&Items(suppliedSource,"ActionStack").Length==0&&Items(suppliedSource,"pendingCommandTriggers").Length==0,"Source is not idle state123.");
                foreach(var unit in NativeLedgerState.Graph(suppliedSource).Values)Require(!RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(unit.Unit,"type")!)),"Source already has a Mimic.");
                trial=new Trial(suppliedSource,report);Active=trial;
                Commands(report,trial,0);Commands(report,trial,2);BuildNoop(report,trial);Training(report,trial);
                for(uint site=0;site<4;site++){Decisions(report,trial,site,B.Decision.Handled);Decisions(report,trial,site,B.Decision.Reject);}
                Decisions(report,trial,2,B.Decision.Handled,Injection.Prepare);
                Decisions(report,trial,3,B.Decision.Handled,Injection.Finish);
                Decisions(report,trial,1,B.Decision.Handled,Injection.Cleanup);
                Commands(report,trial,0,Injection.Finish);OriginalException(report,trial);
                for(uint site=0;site<4;site++)UnscopedReject(report,trial,suppliedSource,site,Owners[site]+"-published-custom-rejected",site<2?NativeExchangeCatalog.Regroup:RecruitmentCatalog.Variant("warrior"));
                object privateState=trial.Clone();UnscopedReject(report,trial,privateState,0,"unscoped-private-ordinary-rejected",null);
                lock(Gate)trial.Registration(privateState).Retired=true;
                UnscopedReject(report,trial,privateState,0,"retired-private-ordinary-rejected",null);
                trial.VerifyPublished();Check(report,!trial.NotificationOverflow&&B.ActiveOperations==0,"execution-probe-final-source-and-operation-isolation");
                Measure(report,"source-frame-sha256",Hash(trial.SourceBytes));Measure(report,"source-frame-bytes",trial.SourceBytes.Length);Measure(report,"native-error-notifications",trial.Notifications.Count);
                report.Passed=true;
            }
            catch(Exception e){Fail(report,"execution-probe",e);}
            finally
            {
                if(trial is not null)
                {
                    try{trial.VerifyPublished();}catch(Exception e){Fail(report,"published-source-final-witness",e);}
                    trial.Cleanup(report);
                }
                if(B.ActiveOperations!=0)Fail(report,"execution-cookie-balance",new InvalidDataException("A native execution cookie survived the synchronous call."));
                if(Current is not null)Fail(report,"execution-scope-balance",new InvalidDataException("An execution scope survived its synchronous call."));
                Active=null;
            }
            return report;
        }
    }
}
