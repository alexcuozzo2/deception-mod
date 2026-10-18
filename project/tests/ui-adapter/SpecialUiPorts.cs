using System;
using System.Collections.Generic;
using Deception.Core;
namespace Deception.PolyModAdapter
{
 internal static class NativeRegroupSession {internal static bool Enabled;internal static bool Ready(object state)=>Enabled&&ReferenceEquals(state,GameManager.GameState);}
 internal static class NativeGameStateEnvelope
 {internal static LedgerUnit[] FocusUnits=Array.Empty<LedgerUnit>();internal static Guid Epoch=new("00112233-4455-6677-8899-aabbccddeeff");internal static long Revision;internal sealed record Snapshot(long Revision){internal byte[] Bytes()=>NativeIdentityLedgerCodec.Encode(new(Epoch,10,1,FocusUnits,Array.Empty<LedgerReceipt>()),Revision);}static Snapshot? Current;static object? State;internal static void Invalidate(){Current=null;}internal static Snapshot? Get(object state){if(!ReferenceEquals(state,GameManager.GameState))return null;if(!ReferenceEquals(State,state)||Current?.Revision!=Revision){State=state;Current=new(Revision);}return Current;}}
 internal static class NativeRogueRules {internal static LedgerUnit? Row(object state,object unit)=>MimicPresentationState.ReadIdentity?.Invoke(state,((UnitState)unit).id);internal static bool IsRogue(object state,object unit)=>MimicPresentationState.ReadIdentity?.Invoke(state,((UnitState)unit).id)?.Rogue is not null;internal static bool CanCommand(object state,object unit,byte actor)=>((UnitState)unit).owner==actor;}
 internal static partial class NativeSpyNetwork
 {
  internal static object[] Tiles=Array.Empty<object>();internal static int Queries;
  internal static bool ActionReady(object state,object unit,byte actor)=>((UnitState)unit).owner==actor&&!((UnitState)unit).moved&&!((UnitState)unit).attacked;
  internal static bool TryReachable(object state,byte actor,uint id,out object[] targets,out string reason,bool ready=true){Queries++;targets=Tiles;reason="";return true;}
 }
 internal static class NativeSubvert
 {
  internal static HashSet<uint> Targets=new();internal static int Queries;
  internal static bool TryOffer(object state,uint source,uint target,out NativeExchangeIntent? intent,out string reason){Queries++;reason="";intent=Targets.Contains(target)?NativeExchangeCommands.Intent(NativeExchangeKind.Subvert,source,target):null;return intent is not null;}
 }
 internal static class NativeBlackout
 {
  internal static bool Allowed=true;internal static int Queries;
  internal static bool TryOffer(object state,uint source,int x,int y,out NativeExchangeIntent? intent,out string reason){Queries++;reason="";intent=Allowed?NativeExchangeCommands.Intent(NativeExchangeKind.Blackout,source,source):null;return intent is not null;}
 }
 internal static class NativeFeignSupport
 {
  internal static readonly HashSet<NativeExchangeKind> Available=new();internal static int Queries;
  internal static bool TryOffer(object state,uint source,NativeExchangeKind kind,out NativeExchangeIntent? intent,out string reason){Queries++;reason="";intent=Available.Contains(kind)?NativeExchangeCommands.Intent(kind,source,source):null;return intent is not null;}
 }
 internal static class NativeExchangeCommands
 {
  internal static bool ValidResult=true;internal static NativeExchangeIntent? Last;internal static int Forgotten;
  internal static NativeExchangeIntent Intent(NativeExchangeKind kind,uint source,uint target)=>new(new Guid("00112233-4455-6677-8899-aabbccddeeff"),1,NativeGameStateEnvelope.Revision,GameManager.GameState.CurrentTurn,GameManager.GameState.CurrentPlayer,kind,false,NativePartnerReference.Existing,20,new(1,source,0,0),new(2,target,0,0),new byte[32]);
  internal static object Create(NativeExchangeIntent intent){Last=intent;return new BuildCommand();}
  internal static bool Valid(object command,object state,bool pending,out string reason){reason="";return ValidResult;}
  internal static void Forget(object command){Forgotten++;}
 }
}
