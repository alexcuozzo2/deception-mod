using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // Backend commits enqueue value snapshots only. Sound is consumed by the
    // matching native reaction, never by simulation, state reads or rendering.
    internal static class NativeDisguiseGrowthAudio
    {
        const int MaxBatches=64,MaxEvents=512;
        sealed record Cue(ulong Logical,uint Native,byte Owner,int X,int Y,LedgerAppearance Appearance);
        sealed class Batch
        {
            internal object Action=null!,State=null!;
            internal Guid Epoch;internal uint Round;internal bool Consumed;
            internal readonly List<Cue> Cues=new();
        }
        sealed record NativeCue(object State,Guid Epoch,uint Round,ulong Logical,uint Native,byte Owner,int X,int Y);
        static readonly List<Batch> Batches=new();
        static readonly List<NativeCue> NativeCues=new();
        static MethodInfo Play=null!,Enabled=null!,Local=null!;
        static object Promote=null!,SpawnEnemy=null!,DefaultSkin=null!,UpgradeKind=null!;
        static int Notes;
        internal static int PendingCount=>Batches.Where(b=>!b.Consumed).Sum(b=>b.Cues.Count);
        internal static void Clear(){Batches.Clear();NativeCues.Clear();Notes=0;}
        internal static void Install(Harmony harmony)
        {
            Play=Native.M("AudioManager","PlaySFXAtTile","SFXTypes","WorldCoordinates","PolytopiaBackendBase.Common.SkinType","System.Single","System.Single");
            Enabled=Native.M("AudioManager","ShouldPlaySoundEffects");Local=Native.M("GameManager","IsPlayerLocal","System.Byte");
            Native.RequireReturn(Play,typeof(void));Native.RequireReturn(Enabled,typeof(bool));Native.RequireReturn(Local,typeof(bool));
            Promote=Enum.Parse(Native.T("SFXTypes"),"Promote",false);SpawnEnemy=Enum.Parse(Native.T("SFXTypes"),"SpawnEnemy",false);
            DefaultSkin=Enum.Parse(Native.T("PolytopiaBackendBase.Common.SkinType"),"Default",false);
            UpgradeKind=Enum.Parse(Native.T("ActionType"),"Upgrade",false);
            if(Native.Int(UpgradeKind)!=16||Native.Int(Promote)!=27||Native.Int(SpawnEnemy)!=23||Native.Int(DefaultSkin)!=0)
                throw new InvalidOperationException("Pinned native upgrade sound constants changed.");
            foreach(string reaction in new[]{"EndTurnReaction","DisembarkReaction","UpgradeReaction"})_=Native.M(reaction,"get_action");
            foreach(var m in new[]{Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),
                Native.M("TileData","GetExplored","System.Byte"),Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),
                Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),Native.M("Tile","get_Data"),Native.M("Tile","get_IsHidden"),
                Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),Native.M("AudioManager","get_instance"),
                Native.M("ClientBase","get_IsRecap"),Native.M("HotSeatOverlay","get_IsActive"),
                Native.M("ActionBase","GetActionType"),Native.M("ActionBase","IsValid","GameState"),Native.M("GameState","get_ActionStack"),
                Native.M("UpgradeAction","get_Type"),Native.M("UpgradeAction","get_Coordinates")}){_=m;}
            Native.Hook(harmony,Native.M("EndTurnReaction","Execute","Il2CppSystem.Action"),typeof(NativeDisguiseGrowthAudio),nameof(BeforeReaction));
            // Native landing renders its new body before this postfix. The
            // original completion callback, animation and landing cue remain.
            Native.Hook(harmony,Native.M("DisembarkReaction","Execute","Il2CppSystem.Action"),typeof(NativeDisguiseGrowthAudio),null,nameof(BeforeReaction));
            Native.Hook(harmony,Native.M("UpgradeReaction","Execute","Il2CppSystem.Action"),typeof(NativeDisguiseGrowthAudio),nameof(BeforeNativeUpgrade));
            Native.Hook(harmony,Native.M("MapRenderer","OnDestroy"),typeof(NativeDisguiseGrowthAudio),nameof(MapDestroying));
        }
        static bool Phase(LedgerAppearance before,LedgerAppearance after)=>before.Passenger is null&&after.Passenger is null&&
            NativeApparentGrowth.Next(before.Form)==after.Form;
        static bool SameAppearance(LedgerAppearance a,LedgerAppearance b)=>
            (a with{Effects=Array.Empty<string>()})==(b with{Effects=Array.Empty<string>()})&&a.Effects.SequenceEqual(b.Effects);
        static NativeIdentityLedger? Ledger(object state)
        {var frame=NativeGameStateEnvelope.Get(state);return frame is null?null:NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision);}
        static object? Body(object state,uint id)
        {object?[] args={id,null};return (bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,args)!?args[1]:null;}
        // Call only after a successful authoritative commit. Ordinary landing
        // passes the unwrapped former Passenger as 'before'. Exchanges do not.
        internal static void Record(object action,object state,LedgerAppearance before,LedgerUnit after)
        {
            try
            {
                if(after.Appearance is not LedgerAppearance appearance||!after.Alive||!after.Independent||!Phase(before,appearance))return;
                if(!Native.T("EndTurnAction").IsInstanceOfType(action)&&!Native.T("DisembarkAction").IsInstanceOfType(action))return;
                var ledger=Ledger(state);var current=ledger?.Units.SingleOrDefault(r=>r.LogicalId==after.LogicalId&&r.NativeId==after.NativeId&&r.Alive);
                if(current?.Appearance is not LedgerAppearance a||!SameAppearance(a,appearance))return;
                var unit=Body(state,after.NativeId);if(unit is null||Native.Int(Native.Get(unit,"health"))<=0)return;
                var at=Native.Get(unit,"coordinates")!;
                uint round=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));
                var batch=Batches.FirstOrDefault(b=>Native.Same(b.Action,action)&&Native.Same(b.State,state));
                if(batch is not null&&(batch.Consumed||batch.Epoch!=ledger!.Epoch||batch.Round!=round))return;
                if(batch is null)
                {
                    if(Batches.Count>=MaxBatches)Batches.RemoveAt(0);
                    batch=new(){Action=action,State=state,Epoch=ledger!.Epoch,Round=round};Batches.Add(batch);
                }
                if(batch.Cues.Any(c=>c.Logical==after.LogicalId)||PendingCount>=MaxEvents)return;
                batch.Cues.Add(new(after.LogicalId,after.NativeId,Convert.ToByte(Native.Get(unit,"owner")),Native.Int(Native.Get(at,"X")),Native.Int(Native.Get(at,"Y")),appearance with{Effects=(string[])appearance.Effects.Clone()}));
            }
            catch(Exception e){Note("disguise-growth-audio-record-error",e);}
        }
        internal static void Cancel(object action,object state)
        {
            foreach(var b in Batches.Where(b=>Native.Same(b.Action,action)&&Native.Same(b.State,state))){b.Consumed=true;b.Cues.Clear();}
        }
        static bool Active(object? o)=>Native.UnityAlive(o)&&(bool)Native.Get(Native.Get(o!,"gameObject")!,"activeInHierarchy")!;
        static bool Context(out object? state,out object? map,out byte viewer)
        {
            state=Native.Static("GameManager","GameState");map=Native.Static("MapRenderer","Current");viewer=0;
            var client=Native.Static("GameManager","Client");var player=Native.Static("GameManager","LocalPlayer");
            if(state is null||client is null||player is null||!Active(map)||!NativeRegroupSession.Ready(state)||
                (bool)Native.Get(client,"IsReplay")!||(bool)Native.Get(client,"IsSpectating")!||(bool)Native.Get(client,"IsRecap")!||
                (bool)Native.Static("HotSeatOverlay","IsActive")!)return false;
            viewer=Convert.ToByte(Native.Get(player,"Id"));return true;
        }
        static object? Visible(object state,object map,byte viewer,int x,int y,uint id)
        {
            var at=Activator.CreateInstance(Native.T("WorldCoordinates"),x,y)!;
            var data=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
            if(data is null||!(bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),data,viewer)!)return null;
            var tile=Native.Invoke(Native.M("MapRenderer","GetTileInstance","WorldCoordinates"),map,at);
            if(!Active(tile)||!Native.Same(Native.Get(tile!,"Data"),data)||(bool)Native.Get(tile!,"IsHidden")!)return null;
            var unit=Native.Invoke(Native.M("TileData","GetUnit","GameState","System.Byte","System.Boolean"),data,state,viewer,false);
            if(unit is null||Convert.ToUInt32(Native.Get(unit,"id"))!=id||!Native.Same(unit,Body(state,id))||Native.Int(Native.Get(unit,"health"))<=0)return null;
            var coordinate=Native.Get(unit,"coordinates")!;
            return Native.Int(Native.Get(coordinate,"X"))==x&&Native.Int(Native.Get(coordinate,"Y"))==y?unit:null;
        }
        static bool SoundEnabled()=>Native.UnityAlive(Native.Static("AudioManager","instance"))&&(bool)Native.Invoke(Enabled,null)!;
        public static void BeforeReaction(object __instance)
        {
            try
            {
                var action=Native.Get(__instance,"action");if(action is null)return;
                var matching=Batches.Where(b=>Native.Same(b.Action,action)&&!b.Consumed).ToArray();
                // Consume before any native call; errors, muting, fog and repeated
                // callbacks cannot turn into deferred or repeated private audio.
                foreach(var batch in matching)batch.Consumed=true;
                if(matching.Length==0||!Context(out var state,out var map,out byte viewer)||!SoundEnabled())return;
                var ledger=Ledger(state!);if(ledger is null)return;
                foreach(var batch in matching)
                {
                    if(!Native.Same(batch.State,state)||batch.Epoch!=ledger.Epoch)continue;
                    uint now=Convert.ToUInt32(Native.Get(state!,"CurrentTurn"));
                    if(now<batch.Round||(ulong)now>(ulong)batch.Round+1)continue;
                    foreach(var cue in batch.Cues)
                    {
                        var row=ledger.Units.SingleOrDefault(r=>r.Alive&&r.LogicalId==cue.Logical&&r.NativeId==cue.Native);
                        if(row?.Appearance is not LedgerAppearance a||!SameAppearance(a,cue.Appearance))continue;
                        var unit=Visible(state!,map!,viewer,cue.X,cue.Y,cue.Native);
                        if(unit is null||Convert.ToByte(Native.Get(unit,"owner"))!=cue.Owner)continue;
                        if(NativeCues.Any(n=>Native.Same(n.State,state)&&n.Epoch==batch.Epoch&&n.Round==batch.Round&&n.Logical==cue.Logical&&n.Native==cue.Native&&n.Owner==cue.Owner&&n.X==cue.X&&n.Y==cue.Y))continue;
                        if(row.Kind==LedgerActualKind.Genuine&&PendingNativeGrowth(state!,unit,cue))continue;
                        if(!Context(out var current,out var currentMap,out byte currentViewer)||!Native.Same(current,state)||!Native.Same(currentMap,map)||currentViewer!=viewer||!SoundEnabled())continue;
                        var at=Activator.CreateInstance(Native.T("WorldCoordinates"),cue.X,cue.Y)!;
                        bool local=(bool)Native.Invoke(Local,null,cue.Owner)!;
                        Native.Invoke(Play,null,local?Promote:SpawnEnemy,at,DefaultSkin,1f,1f);
                        if(Notes++<24)Entry.Note("disguise-growth-audio","visible "+cue.Appearance.Form+"; native "+(local?"Promote":"SpawnEnemy")+" cue");
                    }
                }
            }
            catch(Exception e){Note("disguise-growth-audio-error",e);}
            finally
            {
                // Retain only tiny consumed tombstones to reject a repeated
                // commit; no appearance arrays survive a finished reaction.
                foreach(var batch in Batches.Where(b=>b.Consumed))batch.Cues.Clear();
            }
        }
        static bool PendingNativeGrowth(object state,object unit,Cue cue)
        {
            string actual=Native.EnumName(Native.Get(unit,"type")!);
            string? target=NativeApparentGrowth.Next(actual);
            if(target is null)return false;
            // Genuine overdue cargo queues its UpgradeAction during landing.
            // Its reaction follows DisembarkReaction on the next client update.
            // Let that already-authoritative native upgrade own the one cue.
            object stack=Native.Get(state,"ActionStack")!;
            int count=Native.Int(Native.Get(stack,"Count"));
            if(count<0||count>16384)throw new InvalidOperationException("Growth audio action queue exceeds its audited bound.");
            foreach(var queued in Native.Items(stack))
            {
                if(!Equals(Native.Invoke(Native.M("ActionBase","GetActionType"),queued),UpgradeKind))continue;
                var upgrade=Native.As(queued,"UpgradeAction");
                if(Native.EnumName(Native.Get(upgrade,"Type")!)!=target||Convert.ToByte(Native.Get(upgrade,"PlayerId"))!=cue.Owner)continue;
                var at=Native.Get(upgrade,"Coordinates")!;
                if(Native.Int(Native.Get(at,"X"))!=cue.X||Native.Int(Native.Get(at,"Y"))!=cue.Y)continue;
                if((bool)Native.Invoke(Native.M("ActionBase","IsValid","GameState"),upgrade,state)!)return true;
            }
            return false;
        }
        public static void BeforeNativeUpgrade(object __instance)
        {
            try
            {
                if(!Context(out var state,out var map,out byte viewer)||!SoundEnabled())return;
                var action=Native.Get(__instance,"action");if(action is null)return;
                string type=Native.EnumName(Native.Get(action,"Type")!);if(type!="babydragon"&&type!="firedragon"&&type!="larva"&&type!="moth")return;
                var ledger=Ledger(state!);if(ledger is null)return;
                var at=Native.Get(action,"Coordinates")!;int x=Native.Int(Native.Get(at,"X")),y=Native.Int(Native.Get(at,"Y"));
                var data=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state!,"Map"),at);
                var unit=data is null?null:Native.Get(data,"unit");if(unit is null||Native.EnumName(Native.Get(unit,"type")!)!=type)return;
                uint id=Convert.ToUInt32(Native.Get(unit,"id"));var row=ledger.Units.SingleOrDefault(r=>r.Alive&&r.NativeId==id&&r.Kind==LedgerActualKind.Genuine&&r.Independent);
                if(row is null||Visible(state!,map!,viewer,x,y,id) is null)return;
                byte owner=Convert.ToByte(Native.Get(unit,"owner"));if(owner!=Convert.ToByte(Native.Get(action,"PlayerId")))return;
                if(NativeCues.Count>=MaxEvents)NativeCues.RemoveAt(0);
                NativeCues.Add(new(state!,ledger.Epoch,Convert.ToUInt32(Native.Get(state!,"CurrentTurn")),row.LogicalId,id,owner,x,y));
                // Observe only: native UpgradeReaction still plays its own cue.
            }
            catch(Exception e){Note("disguise-growth-audio-native-observer-error",e);}
        }
        public static void MapDestroying(object __instance)=>Clear();
        static void Note(string kind,Exception error){try{if(Notes++<24)Entry.Note(kind,error.ToString());}catch{/* Presentation diagnostics cannot fail an authoritative commit. */}}
    }
}
