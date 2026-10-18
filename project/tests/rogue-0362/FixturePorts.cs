// Explicit synthetic native boundary. Actual Rogue/accounting/tactics/status
// adapters and real persistence codecs are compiled into this fixture.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Deception.Core;
using Deception.PolyModAdapter;
namespace HarmonyLib {public sealed class HarmonyMethod{public HarmonyMethod(MethodInfo m){}}public sealed class Harmony{public List<string> Hooks=new();public void Patch(MethodInfo m,HarmonyMethod? prefix,HarmonyMethod? postfix,object? transpiler,HarmonyMethod? finalizer,object? ilmanipulator=null){Hooks.Add(m.DeclaringType!.Name+"."+m.Name);}}}
namespace Il2CppSystem {public class Object {static long Next;public IntPtr Pointer{get;}=new(++Next);public T Cast<T>()where T:class=>this as T??throw new InvalidCastException();}public sealed class WeakReference:Object{readonly System.WeakReference<object> Weak;public WeakReference(object o,bool resurrect){Weak=new(o);}public object? Target=>Weak.TryGetTarget(out var o)?o:null;public bool IsAlive=>Target is not null;}}
namespace Il2CppSystem.Collections.Generic {public class List<T>:Il2CppSystem.Object{readonly System.Collections.Generic.List<T> Inner=new();public int Count=>Inner.Count;public T this[int i]=>Inner[i];public void Add(T value)=>Inner.Add(value);public bool Remove(T value)=>Inner.Remove(value);public bool Contains(T value)=>Inner.Contains(value);}}
public class NativeArray<T>:Il2CppSystem.Object {public T[] Values{get;set;}=Array.Empty<T>();public int Length=>Values.Length;public int Count=>Length;public T this[int i]=>Values[i];}
public struct WorldCoordinates{public int X{get;set;}public int Y{get;set;}public WorldCoordinates(int x,int y){X=x;Y=y;}}
namespace Deception.PolyModAdapter
{
 internal static class Entry{internal static void Note(string kind,string text){}}
 internal static class RecruitmentHooks{internal static bool Registered=true;}
 internal static class NativeRegroupSession{internal static Action? OnSessionEnded;internal static bool Enabled=true;internal static HashSet<object> Owned=new();internal static bool Ready(object state)=>Enabled&&Owned.Contains(state);internal static List<string> Faults=new();internal static void Fault(string reason)=>Faults.Add(reason);
 internal static void Prepare(object state){var s=(GameState)state;var snap=NativeGameStateEnvelope.Get(s)!;var ledger=NativeIdentityLedgerCodec.Decode(snap.Bytes(),snap.Revision);var rows=ledger.Units.ToList();foreach(var t in s.Map.Tiles.Values)if(t.unit is UnitState u&&!rows.Any(r=>r.NativeId==u.id))rows.Add(new(u.id,u.id,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0));NativeGameStateEnvelope.Set(s,snap.Revision,NativeIdentityLedgerCodec.Encode(ledger with{Units=rows.OrderBy(r=>r.LogicalId).ToArray()},snap.Revision));}}
 internal static class NativeGameStateEnvelope
 {
  internal sealed class Snapshot{internal long Revision{get;}readonly byte[] Payload;internal Snapshot(long r,byte[] p){Revision=r;Payload=(byte[])p.Clone();}internal byte[] Bytes()=>(byte[])Payload.Clone();}
  internal static Dictionary<object,Snapshot> Values=new();internal static Snapshot? Get(object state)=>Values.GetValueOrDefault(state);
  internal static void Set(object state,long revision,byte[] data){NativeIdentityLedgerCodec.Decode(data,revision);Values[state]=new(revision,data);Fault.Check("ledger");}
 }
 internal static class NativeLedgerOwnership{internal sealed class WeakIdentity{readonly System.WeakReference<object> Weak;internal WeakIdentity(object o){Weak=new(o);}internal object? Target()=>Weak.TryGetTarget(out var o)?o:null;internal bool Matches(object o)=>Native.Same(Target(),o);}}
 internal static class NativeLedgerState{internal sealed record Body(object Unit,bool Root);internal static Dictionary<uint,Body> Graph(object state)=>((GameState)state).Map.Tiles.Values.Where(t=>t.unit is not null).ToDictionary(t=>t.unit!.id,t=>new Body(t.unit!,true));internal static void Validate(object state,long revision,byte[] bytes){NativeIdentityLedgerCodec.Decode(bytes,revision);Fault.Check("validate");}}

}
