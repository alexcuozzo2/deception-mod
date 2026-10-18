using System;
using System.Linq;
using System.Reflection;
using Deception.Core;
using Deception.PolyModAdapter;
namespace Deception.Tests {
 public static partial class MimicMovementTests {
  static bool WaterScopeClean()=>typeof(NativeMimicWaterMovement).GetField("Current",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null) is null;
  static Rig NullPathRig(bool rogue=true){var r=Make("warrior",genuine:"warrior");if(rogue)Edit(r,x=>x with{Rogue=new LedgerRogue(2,1,1)});PathFinder.NullForNoPath=true;return r;}
  static void RogueNullPathTests(){try{
   foreach(string denial in new[]{"endpoint","exhausted"})Test("rogue-native-null-path-"+denial,()=>{
    var r=NullPathRig();if(denial=="endpoint")r.Land.Blocked=true;else r.Land.coordinates=new(5,1);
    var before=NativeGameStateEnvelope.Get(r.S)!.Bytes();var native=TransportStateDump.Dump(r.S);bool scopePresent=false;
    PathFinder.During=(_,_)=>scopePresent=typeof(NativeMimicWaterMovement).GetField("Current",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null) is not null;
    var path=PathFinder.GetPath(r.S,r.U.coordinates,r.Land.coordinates,1,r.U);
    Check(scopePresent&&path is null,"native absent path must survive real Water prefix/postfix and Rogue postfix without throwing or creating an empty list");
    Check(WaterScopeClean(),"completed absent path retained its native query scope");Stable(r,before,native);
   });
   Test("ordinary-native-null-path-preserved",()=>{
    var r=NullPathRig(false);r.Land.Blocked=true;var path=PathFinder.GetPath(r.S,r.U.coordinates,r.Land.coordinates,1,r.U);
    Check(path is null&&WaterScopeClean(),"ordinary native no-path semantics changed");
   });
   Test("rogue-null-options-preserve-native-absence",()=>{
    var r=NullPathRig();object? result=null;int calls=PathFinder.Queries;NativeRogueTactics.Options(r.S,r.U.coordinates,1,r.U,ref result);
    Check(result is null&&calls==PathFinder.Queries,"null options must neither become moves nor start path work");
   });
   Test("rogue-options-discard-native-null-candidate-path",()=>{
    var r=NullPathRig();r.Water.Blocked=true;var before=NativeGameStateEnvelope.Get(r.S)!.Bytes();var native=TransportStateDump.Dump(r.S);
    object original=Native.NewList("WorldCoordinates",new object[]{r.Source.coordinates,r.Land.coordinates,r.Water.coordinates});object? result=original;
    NativeMimicWaterMovement.BeforeOptions(r.S,r.U.coordinates,r.U,out var scope,ref result);
    try{NativeMimicWaterMovement.AfterOptions(r.S,r.U.coordinates,1,r.U,scope,ref result);NativeRogueTactics.Options(r.S,r.U.coordinates,1,r.U,ref result);}
    finally{NativeMimicWaterMovement.Finished(null,scope);}
    var at=Native.Items(result!).Cast<WorldCoordinates>().ToArray();
    Check(at.Length==2&&at[0].X==r.Source.coordinates.X&&at[1].X==r.Land.coordinates.X,"unreachable null candidate was kept or valid native route removed");
    Check(!ReferenceEquals(original,result)&&Native.Items(original).Count()==3,"Rogue filtering modified the native/shared options list");
    Check(WaterScopeClean(),"nested candidate path left a water query scope behind");Stable(r,before,native);
   });
  }finally{PathFinder.NullForNoPath=false;PathFinder.During=null;}}
 }
}
