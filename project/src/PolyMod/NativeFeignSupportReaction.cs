using System;
using System.Collections.Generic;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Only accepted commits create visual cues. The native actions below are
    // detached reaction inputs: never executed, serialized or queued as rules.
    internal static class NativeFeignSupportReaction
    {
        sealed record Cue(uint Id,int X,int Y,int Amount,bool Source);
        sealed record Pending(object State,NativeExchangeIntent Intent,Cue[] Cues);
        static readonly Dictionary<(long,Guid,ulong),Pending> Waiting=new();
        static readonly HashSet<string> Errors=new(StringComparer.Ordinal);
        internal static void Install(Harmony harmony)=>Native.Hook(harmony,Native.M("BuildReaction","Execute","Il2CppSystem.Action"),typeof(NativeFeignSupportReaction),nameof(BeforeReaction));
        internal static void Clear(){Waiting.Clear();Errors.Clear();}
        internal static void Record(NativeExchangePlan plan)
        {
            try
            {
                if(plan.AlreadyCommitted||!Native.Same(plan.State,Native.Static("GameManager","GameState")))return;
                var before=plan.OriginalLedger;var graph=NativeLedgerState.Graph(plan.State);var cues=new List<Cue>();var intent=plan.Intent;
                if(intent.Kind!=NativeExchangeKind.FeignRecover)cues.Add(new(intent.Initiator.NativeId,intent.Initiator.X,intent.Initiator.Y,0,true));
                foreach(var row in plan.CandidateLedger.Units)
                {
                    var old=before.Units.Single(x=>x.LogicalId==row.LogicalId);
                    if(row.Appearance is not LedgerAppearance a||ReferenceEquals(old.Appearance,a)||
                        (old.Appearance is LedgerAppearance oldA&&NativeExchangeIntentCodec.AppearanceFingerprint(oldA).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(a))))continue;
                    if(!graph.TryGetValue(row.NativeId,out var body)||!body.Root)continue;
                    object at=Native.Get(body.Unit,"coordinates")!;
                    int prior=old.Appearance?.Health??Native.Int(Native.Get(body.Unit,"health"));
                    cues.Add(new(row.NativeId,Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")),Math.Max(0,a.Health-prior),false));
                }
                if(Waiting.Count>=32)Waiting.Clear();Waiting[(Native.Pointer(plan.State),intent.Epoch,intent.Sequence)]=new(plan.State,intent,cues.ToArray());
            }
            catch(Exception e){Note(e);}
        }
        public static bool BeforeReaction(object __instance,object __0)
        {
            var action=Native.Get(__instance,"action");if(action is null||!NativeExchangeCommands.IsExchange(action))return true;
            var intent=NativeExchangeCommands.Get(action);if(intent is null||!NativeFeignSupport.IsKind(intent.Kind))return true;
            bool handed=false,completed=false;
            void Complete(){if(completed)return;completed=true;Native.Invoke(Native.M("Il2CppSystem.Action","Invoke"),__0);}
            try
            {
                NativeExchangeCommands.ValidateBase(action,intent);
                object? state=Native.Static("GameManager","GameState"),player=Native.Static("GameManager","LocalPlayer"),map=Native.Static("MapRenderer","Current"),client=Native.Static("GameManager","Client");
                if(state is null||!Waiting.Remove((Native.Pointer(state),intent.Epoch,intent.Sequence),out var pending))return false;
                if(player is null||client is null||!Native.UnityAlive(map)||!NativeRegroupSession.Ready(state)||
                    (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!)return false;
                var snapshot=NativeGameStateEnvelope.Get(state);
                if(snapshot is null||!Native.Same(pending.State,state)||!NativeExchangeIntentCodec.Fingerprint(pending.Intent).SequenceEqual(NativeExchangeIntentCodec.Fingerprint(intent))||
                    !NativeExchangeIntentCodec.AlreadyCommitted(NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision),snapshot.Revision,intent))return false;
                byte viewer=Convert.ToByte(Native.Get(player,"Id"));int index=0;
                if(!(bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!)return false;
                bool Current()=>!completed&&NativeRegroupSession.Ready(state)&&Native.Same(state,Native.Static("GameManager","GameState"))&&
                    Native.Same(player,Native.Static("GameManager","LocalPlayer"))&&Native.Same(client,Native.Static("GameManager","Client"))&&
                    !(bool)Native.Get(client,"IsReplay")!&&!(bool)Native.Get(client,"IsSpectating")!&&
                    (bool)Native.Invoke(Native.M("GameManager","IsPlayerViewing","System.Byte"),null,viewer)!&&Native.UnityAlive(map)&&Native.Same(map,Native.Static("MapRenderer","Current"));
                void Next()
                {
                    try
                    {
                        if(!Current()){Complete();return;}
                        while(index<pending.Cues.Length)
                        {
                            var cue=pending.Cues[index++];object at=Activator.CreateInstance(Native.T("WorldCoordinates"),cue.X,cue.Y)!;
                            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
                            if(tile is null||!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,viewer)!)continue;
                            var visual=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,at);
                            if(!Native.UnityAlive(visual)||!Native.Same(Native.Get(visual!,"Data"),tile)||(bool)Native.Get(visual!,"IsHidden")!)continue;
                            var visible=Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),tile,state,viewer,false);
                            if(visible is null||Convert.ToUInt32(Native.Get(visible,"id"))!=cue.Id)continue;
                            var rendered=Native.Invoke(Native.M("MapRenderer","GetUnitInstance","System.UInt32"),map,cue.Id);
                            if(!Native.UnityAlive(rendered)||!Native.Same(Native.Get(rendered!,"UnitState"),visible)||!Native.Same(Native.Get(visual!,"unit"),rendered)||
                                !(bool)Native.Invoke(Native.M("Unit","GetVisible"),rendered)!||(bool)Native.Invoke(Native.M("Unit","IsInvisibleForLocalPlayer"),rendered)!)continue;
                            bool heal=intent.Kind is NativeExchangeKind.FeignHeal or NativeExchangeKind.FeignRecover;
                            string type=cue.Source?(heal?"HealOthers":"BoostOthers"):(heal?"Heal":"Boost");
                            object fake=type=="Heal"?Activator.CreateInstance(Native.T("HealAction"),intent.Actor,at,(ushort)0)!:
                                Activator.CreateInstance(Native.T(type+"Action"),intent.Actor,at)!;
                            if(type=="Heal")Native.Set(fake,"HealAmount",(ushort)cue.Amount);
                            object reaction=Activator.CreateInstance(Native.T(type+"Reaction"),fake)!;
                            bool callbackUsed=false;var callback=Native.Callback(()=>{if(callbackUsed)return;callbackUsed=true;Next();});
                            Native.Invoke(Native.M(type+"Reaction","Execute","Il2CppSystem.Action"),reaction,callback);return;
                        }
                        Complete();
                    }
                    catch(Exception e){Note(e);Complete();}
                }
                handed=true;Next();
            }
            catch(Exception e){Note(e);Complete();}
            finally{if(!handed)Complete();}
            return false;
        }
        static void Note(Exception e){if(Errors.Count<16&&Errors.Add(e.ToString()))Entry.Note("feign-support-reaction",e.ToString());}
    }
}
