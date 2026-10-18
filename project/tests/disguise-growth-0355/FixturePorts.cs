// Synthetic native seams only; these fixtures do not run Unity or IL2CPP.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deception.Core;
namespace HarmonyLib
{
    public sealed class HarmonyMethod{public readonly MethodInfo Method;public HarmonyMethod(MethodInfo method){Method=method;}}
    public sealed class Harmony
    {
        public List<(string Name,string? Prefix,string? Final)> Hooks=new();
        public void Patch(MethodInfo m,HarmonyMethod? pre,HarmonyMethod? post,object? t,HarmonyMethod? final,object? il)=>Hooks.Add((m.DeclaringType!.Name+"."+m.Name,pre?.Method.Name,final?.Method.Name));
    }
}
namespace Il2CppSystem
{
    public class Object{static long Next;public IntPtr Pointer{get;}=new(++Next);}
}
namespace Polytopia.Data
{
    public sealed class UnitData:Il2CppSystem.Object
    {
        public enum Type{warrior=2,dragonegg=17,babydragon=18,firedragon=19,dcp_mimic_form_dragonegg=1047,dcp_mimic_form_babydragon=1046,dcp_mimic_form_firedragon=1042,dcp_mimic_form_transportship=1048,dcp_mimic_form_rammership=1050}
        public Type type{get;set;}public int health{get;set;}=10;public int attack{get;set;}public int range{get;set;}public int defence{get;set;}
    }
}
public static class EnumCache<T>where T:struct,Enum
{public static string GetName(T value)=>value.ToString();public static T GetType(string value)=>Enum.Parse<T>(value);}
public sealed class UnitState:Il2CppSystem.Object
{
    Polytopia.Data.UnitData.Type Type;Polytopia.Data.UnitData Data=new();
    public UnitState? passengerUnit{get;set;}public uint id{get;set;}public byte owner{get;set;}=1;public ushort health{get;set;}=10;
    public ushort createdTurn{get;set;}=2;public ushort xp{get;set;}=4;public ushort promotionLevel{get;set;}
    public bool moved{get;set;}=true;public bool attacked{get;set;}=true;
    public string home{get;set;}="4,5";public int support{get;set;}=1;public string coordinates{get;set;}="2,3";
    public object effects{get;set;}=new();public int PaidStars=3;public int TypeWrites,DataWrites;
    public string Failure="";public Action? OnTypeWrite;
    public Polytopia.Data.UnitData.Type type
    {
        get=>Type;
        set{TypeWrites++;if(Failure=="type-before"){Failure="";throw new IOException("type before");}Type=value;OnTypeWrite?.Invoke();if(Failure=="type-after"){Failure="";throw new IOException("type after");}}
    }
    public Polytopia.Data.UnitData UnitData
    {
        get=>Data;
        set{DataWrites++;if(Failure=="data-before"){Failure="";throw new IOException("data before");}Data=value;if(Failure=="data-after"){Failure="";throw new IOException("data after");}}
    }
}
public sealed class GameState:Il2CppSystem.Object
{
    public byte CurrentPlayer{get;set;}=1;public uint CurrentTurn{get;set;}=8;
    public Dictionary<uint,UnitState> Units=new();public int Stars=21,Score=300,VisionRevision=2,CurrentUnitId=40;
    public List<object> ActionStack=new(){new object()};
}
public sealed class EndTurnAction:Il2CppSystem.Object
{public byte PlayerId{get;set;}=1;public void Execute(GameState state){}}
namespace Deception.PolyModAdapter
{
    // Transaction contract only. The separate native sight fixture exercises
    // the real helper's footprint, action metadata and LIFO integration.
    internal static class NativeGrowthSight
    {
        internal sealed record Explore(byte Owner,uint Unit,Polytopia.Data.UnitData.Type Form);
        public static int Stages,Accepts,Rollbacks;public static string Failure="";
        public static void Reset(){Stages=Accepts=Rollbacks=0;Failure="";}
        internal static Pending Stage(object state,byte owner,IReadOnlyList<object> units)
        {
            var pending=new Pending((GameState)state,owner,units);Stages++;
            try{pending.Stage();return pending;}
            catch{pending.Rollback();throw;}
        }
        internal sealed class Pending:IDisposable
        {
            readonly GameState State;readonly byte Owner;readonly UnitState[] Units;
            readonly List<object> Before;readonly Polytopia.Data.UnitData.Type[] Forms;
            bool Attached,Accepted,Disposed;
            internal Pending(GameState state,byte owner,IReadOnlyList<object> units)
            {State=state;Owner=owner;Units=units.Cast<UnitState>().ToArray();Before=state.ActionStack;Forms=Units.Select(u=>u.type).ToArray();}
            internal void Stage()
            {
                foreach(var u in Units)
                    if(u.owner!=Owner||u.UnitData.type!=u.type||u.type is not (Polytopia.Data.UnitData.Type.dcp_mimic_form_babydragon or Polytopia.Data.UnitData.Type.dcp_mimic_form_firedragon))
                        throw new InvalidDataException("Sight did not see final carrier/data/owner.");
                if(Units.Length==0)return;
                State.ActionStack=new(Before);Attached=true;
                foreach(var u in Units)State.ActionStack.Add(new Explore(Owner,u.id,u.type));
                if(Failure=="stage"){Failure="";throw new IOException("Injected sight queue staging error.");}
            }
            internal void Accept()
            {
                var frame=NativeGameStateEnvelope.Get(State)??throw new InvalidDataException("Sight accepted without ledger.");
                var ledger=NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision);
                foreach(var u in Units)
                    if(RecruitmentCatalog.Variant(ledger.Units.Single(r=>r.NativeId==u.id).Appearance!.Form)!=u.type.ToString())
                        throw new InvalidDataException("Sight accepted before new ledger publication.");
                if(Failure=="accept"){Failure="";throw new IOException("Injected sight acceptance error.");}
                Accepted=true;Accepts++;
            }
            internal void Rollback()
            {
                if(Accepted||Disposed)return;
                if(Attached)
                {
                    if(!Units.Select(u=>u.type).SequenceEqual(Forms))throw new InvalidDataException("Sight rollback happened after carrier restoration.");
                    State.ActionStack=Before;Rollbacks++;
                }
                Disposed=true;
            }
            public void Dispose(){if(!Accepted)Rollback();}
        }
    }
    internal static class NativeDisguiseGrowthAudio
    {
        internal static List<(object Action,object State,LedgerAppearance Before,LedgerUnit After)> Recorded=new();
        internal static void Record(object action,object state,LedgerAppearance before,LedgerUnit after)=>Recorded.Add((action,state,before,after));
        internal static void Cancel(object action,object state)=>Recorded.RemoveAll(c=>ReferenceEquals(c.Action,action)&&ReferenceEquals(c.State,state));
    }
    internal static class Entry{public static List<(string Kind,string Text)> Notes=new();public static void Note(string kind,string text)=>Notes.Add((kind,text));}
    internal static class NativeRegroupSession
    {
        public static bool Enabled=true,IsReady=true;public static GameState? Owned;public static int PrepareCalls;public static List<string> Faults=new();public static Action? DuringPrepare;
        public static bool Ready(object state)=>IsReady&&ReferenceEquals(state,Owned);
        public static void Prepare(object state)
        {if(!Ready(state))throw new InvalidDataException("Unowned fixture state.");PrepareCalls++;DuringPrepare?.Invoke();}
        public static void Fault(string message){Faults.Add(message);IsReady=false;}
    }
    internal static class NativeGameStateEnvelope
    {
        public sealed class Snapshot
        {
            readonly byte[] Payload;public long Revision{get;}
            public Snapshot(long revision,byte[] payload){Revision=revision;Payload=(byte[])payload.Clone();}
            public byte[] Bytes()=>(byte[])Payload.Clone();
        }
        public static Dictionary<object,Snapshot> Frames=new();public static string Failure="";public static int Writes;
        public static Snapshot? Get(object state)=>Frames.GetValueOrDefault(state);
        public static void Set(object state,long revision,byte[] payload)
        {
            Writes++;if(Failure=="before"){Failure="";throw new IOException("envelope before");}
            Frames[state]=new(revision,payload);
            if(Failure=="after"){Failure="";throw new IOException("envelope after");}
        }
    }
    internal static class NativeLedgerState
    {
        internal sealed record Body(object Unit,bool Root);
        public static bool FailValidation;public static int Validations;
        public static Dictionary<uint,Body> Graph(object state)=>((GameState)state).Units.ToDictionary(x=>x.Key,x=>new Body(x.Value,!((GameState)state).Units.Values.Any(u=>ReferenceEquals(u.passengerUnit,x.Value))));
        public static void Validate(object state,long revision,byte[] payload)
        {
            Validations++;if(FailValidation){FailValidation=false;throw new InvalidDataException("Injected validation error.");}
            var ledger=NativeIdentityLedgerCodec.Decode(payload,revision);var game=(GameState)state;
            foreach(var row in ledger.Units.Where(r=>r.Alive))
            {
                if(!game.Units.TryGetValue(row.NativeId,out var unit))throw new InvalidDataException("Missing live body.");
                if(row.Kind!=LedgerActualKind.Mimic)continue;
                if(unit.health>10||unit.UnitData.health!=10||unit.UnitData.attack!=0||unit.UnitData.range!=0||unit.UnitData.defence!=0||unit.UnitData.type!=unit.type||unit.type.ToString()!=RecruitmentCatalog.Variant(row.Appearance!.Form))
                    throw new InvalidDataException("Actual carrier/body contract violated.");
            }
        }
    }
    internal static class RecruitmentHooks
    {
        public const string UT="Polytopia.Data.UnitData+Type";
        public static Dictionary<string,RecruitmentForm> Forms=new();public static Dictionary<string,Polytopia.Data.UnitData> Definitions=new();
        public static object Data(object state,string form)=>Definitions[form];
        public static void Reset()
        {
            Forms.Clear();Definitions.Clear();
            foreach(string name in new[]{"dragonegg","babydragon","firedragon"})
            {
                string carrier=RecruitmentCatalog.Variant(name);
                Forms[carrier]=new(name,carrier,name,name=="dragonegg"?10:name=="babydragon"?15:20,1,0,0,false);
                Definitions[carrier]=new(){type=Enum.Parse<Polytopia.Data.UnitData.Type>(carrier)};
            }
        }
    }
}
