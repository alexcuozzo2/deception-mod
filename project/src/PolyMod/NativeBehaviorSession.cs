using System;
using System.IO;
using System.Text.Json;
using HarmonyLib;
using WeakIdentity=Deception.PolyModAdapter.NativeLedgerOwnership.WeakIdentity;

namespace Deception.PolyModAdapter
{
    // Optional read-only provenance for user-authorized disposable behavior tests.
    // This observer never edits research, state, save bytes, results or control flow.
    internal static class NativeBehaviorSession
    {
        const string Mode="DECEPTION_NATIVE_BEHAVIOR_PROBE",Arm="DECEPTION_NATIVE_BEHAVIOR_ARM_TOKEN";
        static Guid Token;static string Build="";static bool Enabled,Attempted;
        [ThreadStatic] static Creation? Current;
        static Creation? Completed;
        internal static bool HasCompletedCreation=>Enabled&&Completed is {Faulted:false,Entered:true,Returned:true};
        internal static object RequireCreatedClient()
        {
            Require(HasCompletedCreation,"No completed fresh native creation in this process.");
            Creation c=Completed!;object client=Check(c);
            Require(c.Source?.Target() is not null,"Fresh source identity expired.");
            return client;
        }
        sealed class Creation
        {
            internal readonly int Thread=Environment.CurrentManagedThreadId;
            internal WeakIdentity? Manager,Settings,Client,Source;
            internal Guid GameId;internal bool Entered,Returned,Faulted;
        }
        static void Require(bool b,string why){if(!b)throw new InvalidDataException("Behavior creation observer: "+why);}
        internal static void Install(Harmony h)
        {
            if(Environment.GetEnvironmentVariable(Mode)!="1")return;
            foreach(string suffix in new[]{"SESSION","STATE","ENVELOPE","LEDGER","COMMAND","EXCHANGE","RECRUITMENT","PAID_BIRTH","EXECUTION"})
                Require(Environment.GetEnvironmentVariable("DECEPTION_NATIVE_"+suffix+"_PROBE")!="1","Cannot share a diagnostic process.");
            Require(!Enabled&&Guid.TryParseExact(Environment.GetEnvironmentVariable(Arm),"D",out Token)&&Token!=Guid.Empty,"Missing canonical arm token or repeated installation.");
            Build=typeof(Entry).Assembly.GetName().Version?.ToString()??throw new InvalidOperationException("Missing build version.");
            Native.Hook(h,Native.M("GameManager","CreateSinglePlayerGame"),typeof(NativeBehaviorSession),nameof(BeforeCreate),nameof(AfterCreate),nameof(Finished));
            string players=Native.T("Il2CppSystem.Collections.Generic.List`1").MakeGenericType(Native.T("PlayerState")).FullName!;
            Native.Hook(h,Native.M("GameStateUtils","CreateLocalGame","System.Int32","GameSettings","PlayerState",players),typeof(NativeBehaviorSession),nameof(BeforeLocal),nameof(AfterLocal));
            Enabled=true;Entry.Note("native-behavior-armed",JsonSerializer.Serialize(new{schema=1,armToken=Token.ToString("D"),buildVersion=Build,processId=Environment.ProcessId}));
        }
        static void Fault(Creation? c,Exception e)
        {if(c is not null)c.Faulted=true;Entry.Note("native-behavior-create-fault",JsonSerializer.Serialize(new{schema=1,armToken=Token.ToString("D"),nativeGameId=c?.GameId.ToString("D"),buildVersion=Build,processId=Environment.ProcessId,timestampUtc=DateTimeOffset.UtcNow.ToString("O"),reason=e.ToString()}));}
        static void Evidence(string kind,Creation c)=>Entry.Note(kind,JsonSerializer.Serialize(new{schema=1,attempt=1,armToken=Token.ToString("D"),nativeGameId=c.GameId.ToString("D"),clientType="Local",serializerVersion=123,scope="fresh-local-behavior-no-ledger",buildVersion=Build,processId=Environment.ProcessId,timestampUtc=DateTimeOffset.UtcNow.ToString("O")}));
        static object Target(WeakIdentity? w,string type)=>Native.As(w?.Target()??throw new InvalidDataException("Expired "+type),type);
        static Guid GameId(object client)
        {
            var value=Native.Get(client,"gameId")??throw new InvalidDataException("Missing native game Guid.");
            string? text=(string?)Native.Invoke(Native.M("Il2CppSystem.Guid","ToString"),value);
            Require(Guid.TryParseExact(text,"D",out var id)&&id!=Guid.Empty,"Invalid native game Guid.");return id;
        }
        static object Check(Creation c)
        {
            Require(!c.Faulted&&c.Thread==Environment.CurrentManagedThreadId,"Faulted or foreign-thread creation.");
            object manager=Target(c.Manager,"GameManager"),client=Target(c.Client,"ClientBase");
            Require(Native.Same(manager,Native.Static("GameManager","instance"))&&Native.Get(manager,"fakeSession") is null&&
                Native.Same(client,Native.Get(manager,"client"))&&Native.Same(client,Native.Static("GameManager","Client")),"Native creator/client changed.");
            Require(GameId(client)==c.GameId&&Equals(Native.Get(client,"clientType"),Enum.Parse(Native.T("ClientBase+ClientType"),"Local"))&&
                !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!,"Local client identity changed.");return client;
        }
        public static void BeforeCreate(object __instance,out object? __state)
        {
            __state=null;if(!Enabled)return;
            try
            {
                if(Attempted&&Completed is Creation previous)previous.Faulted=true;
                Require(!Attempted&&Current is null,"Only one observed creation per process.");Attempted=true;
                var c=new Creation{Manager=new(__instance)};Current=c;__state=c;
                Require(Native.Same(__instance,Native.Static("GameManager","instance"))&&Native.Get(__instance,"fakeSession") is null&&!(bool)Native.Get(__instance,"isLoadingGame")!,"Creator already loading or fake.");
                object settings=Native.Get(__instance,"settings")??throw new InvalidDataException("Missing settings.");c.Settings=new(settings);
                Require(!(bool)Native.Invoke(Native.M("GameSettings","IsTutorial"),settings)!,"Tutorial is not a disposable behavior match.");
            }
            catch(Exception e){Fault(Current,e);}
        }
        public static void BeforeLocal(int __0,object __1,object? __3)
        {
            if(Current is not Creation c)return;
            try
            {
                Require(!c.Entered&&!c.Faulted&&__0==123&&__3 is null&&c.Settings!.Matches(__1),"Unexpected local factory invocation.");c.Entered=true;
                var manager=Target(c.Manager,"GameManager");object client=Native.Get(manager,"client")??throw new InvalidDataException("Missing local client.");
                c.Client=new(client);c.GameId=GameId(client);_=Check(c);
                foreach(string name in new[]{"initialGameState","currentGameState","targetGameState","ActionManager"})Require(Native.Get(client,name) is null,"Client already owns "+name);
                Evidence("native-behavior-created",c);
            }
            catch(Exception e){Fault(c,e);}
        }
        public static void AfterLocal(object? __result,bool __runOriginal)
        {
            if(Current is not Creation c)return;
            try
            {
                _=Check(c);Require(__runOriginal&&c.Entered&&!c.Returned&&__result is not null,"Native local factory did not return a fresh state.");
                object state=Native.As(__result!,"GameState");Require(Native.Int(Native.Get(state,"Version"))==123&&c.Settings!.Matches(Native.Get(state,"Settings")!),"State version/settings differ.");
                c.Source=new(state);c.Returned=true;
            }
            catch(Exception e){Fault(c,e);}
        }
        public static void AfterCreate(object? __state,bool __runOriginal)
        {
            if(__state is not Creation c)return;
            try{_=Check(c);Require(__runOriginal&&c.Entered&&c.Returned&&c.Source?.Target() is not null,"Outer creation did not complete the observed native factory.");Evidence("native-behavior-create-returned",c);}
            catch(Exception e){Fault(c,e);}
        }
        public static Exception? Finished(Exception? __exception,object? __state)
        {
            if(__state is Creation c)
            {
                if(__exception is not null)Fault(c,__exception);
                if(__exception is null&&!c.Faulted&&c.Entered&&c.Returned)Completed=c;
                if(ReferenceEquals(Current,c))Current=null;
            }
            return __exception;
        }
    }
}
