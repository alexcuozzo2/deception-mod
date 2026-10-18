using System;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // The live carrier retains the apparent move. A second, detached native
    // search adds the exposed Mimic's own legal routes; it never changes a
    // shared UnitData, actual health, action flags or the native path settings.
    internal static class NativeMimicMovement
    {
        [ThreadStatic] static Query? Current;
        [ThreadStatic] static bool ReadingSight;
        static long Generation;
        sealed class Query:IDisposable
        {
            internal readonly Query? Previous;
            internal readonly object State,Source,Profile,Map,Origin,Snapshot;
            internal readonly LedgerUnit Row;
            internal readonly long Epoch;
            internal readonly uint Turn;
            internal readonly byte Owner;
            internal Query(object state,object source,object profile,LedgerUnit row)
            {Previous=Current;State=state;Source=source;Profile=profile;Row=row;Snapshot=NativeGameStateEnvelope.Get(state)!;Map=Native.Get(state,"Map")!;Origin=Native.Get(source,"coordinates")!;
             Epoch=Generation;Turn=Convert.ToUInt32(Native.Get(state,"CurrentTurn"));Owner=Convert.ToByte(Native.Get(source,"owner"));Current=this;}
            internal bool Valid=>Epoch==Generation&&NativeRegroupSession.Ready(State)&&ReferenceEquals(Snapshot,NativeGameStateEnvelope.Get(State))&&Native.Same(Map,Native.Get(State,"Map"))&&
                Convert.ToUInt32(Native.Get(State,"CurrentTurn"))==Turn&&Convert.ToByte(Native.Get(Source,"owner"))==Owner&&
                At(Origin,Native.Get(Source,"coordinates")!)&&Tile(State,Origin) is object tile&&Native.Same(Native.Get(tile,"unit"),Source)&&
                NativeRegroupSession.ReadIdentity(State,Row.NativeId) is {Alive:true,Kind:LedgerActualKind.Mimic} row&&row.LogicalId==Row.LogicalId;
            public void Dispose(){if(ReferenceEquals(Current,this))Current=Epoch==Generation?Previous:null;}
        }
        internal static void Clear(){Generation++;Current=null;ReadingSight=false;}
        internal static void Install(Harmony h)
        {
            NativeMimicWaterMovement.DetachedQuery=(s,u)=>Current is Query q&&Native.Same(q.State,s)&&Native.Same(q.Profile,u)&&q.Valid;
            NativeMimicWaterMovement.ExtendOptions=Options;
            NativeMimicWaterMovement.ExtendPath=Path;
            NativeMimicWaterMovement.FallbackRoute=(s,u,f,t)=>Fallback(s,u,f,t) is object p&&Native.Int(Native.Get(p,"Count"))>0;
            NativeRogueTactics.QuerySource=(s,u)=>Current is Query q&&Native.Same(q.State,s)&&Native.Same(q.Profile,u)&&q.Valid?q.Source:null;
            NativeCloakMovement.FallbackRoute=(s,u,f,t)=>Fallback(s,u,f,t) is object p&&Native.Int(Native.Get(p,"Count"))>0;
            NativeDisguiseMovement.MimicRoute=AcceptedRoute;
            NativeDisguiseMovement.MimicShore=Shore;
            NativeDisguiseMovement.MimicFloodMode=NativeMimicTransport.KeepsFloodMode;
            NativeDisguiseMovement.ShedMimic=Shed;
            Native.Hook(h,Native.M("MoveAction","Execute","GameState"),typeof(NativeMimicMovement),nameof(BeforeWaterMove),nameof(AfterWaterMove));
            Native.Hook(h,Native.M("ActionUtils","WillUnitEmbark","UnitState","TileData","GameState"),typeof(NativeMimicMovement),null,nameof(Transport));
            Native.Hook(h,Native.M("UnitDataExtensions","GetSightRange","Polytopia.Data.UnitData"),typeof(NativeMimicMovement),null,nameof(Sight));
        }
        internal sealed record WaterMove(object State,object Map,object Unit,object Action,object Destination,
            ulong Logical,uint NativeId,byte Owner,uint Turn,long Generation,bool Boat);
        public static void BeforeWaterMove(object __instance,object __0,out WaterMove? __state)
        {
            __state=null;
            if(!NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(__0)||Native.Int(Native.Get(__instance,"Reason"))!=0)return;
            uint id=Convert.ToUInt32(Native.Get(__instance,"UnitId"));var row=NativeRegroupSession.ReadIdentity(__0,id);
            if(row is not {Alive:true,Kind:LedgerActualKind.Mimic})return;
            object?[] found={id,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),__0,found)!||found[1] is not object unit)return;
            byte owner=Convert.ToByte(Native.Get(unit,"owner"));
            if(owner!=Native.Int(Native.Get(__0,"CurrentPlayer"))||owner!=Native.Int(Native.Get(__instance,"PlayerId"))||
                Tile(__0,Native.Get(unit,"coordinates")!) is not object source||!Native.Same(Native.Get(source,"unit"),unit))return;
            var queue=Items(Native.Get(__0,"ActionStack"));
            if(!queue.Any(x=>Native.Same(x,__instance))||!(bool)Native.Invoke(Native.M("ActionBase","IsValid","GameState"),__instance,__0)!)return;
            var path=Items(Native.Get(__instance,"Path"));
            if(path.Length<2||!At(path[^1],Native.Get(unit,"coordinates")!)||At(path[0],path[^1]))return;
            for(int i=1;i<path.Length;i++)
                if(Math.Max(Math.Abs(Native.Int(Native.Get(path[i],"X"))-Native.Int(Native.Get(path[i-1],"X"))),
                    Math.Abs(Native.Int(Native.Get(path[i],"Y"))-Native.Int(Native.Get(path[i-1],"Y"))))!=1)return;
            bool boat=NativeMimicTransport.ExposedBoat(source,row);
            // Native paths run destination first. The last ordinary terrain
            // visited owns the incoming mode even when several flooded cells
            // follow it; looking only at the start/end loses a water crossing.
            foreach(var at in path)
            {
                var tile=Tile(__0,at);if(tile is null)return;
                if(!NativeMimicTransport.Wetland(tile)){boat=NativeMimicTransport.WaterEndpoint(tile);break;}
            }
            __state=new(__0,Native.Get(__0,"Map")!,unit,__instance,path[0],row.LogicalId,id,owner,
                Convert.ToUInt32(Native.Get(__0,"CurrentTurn")),Generation,!row.AmphibiousMimic&&boat);
        }
        [HarmonyPriority(Priority.High)]
        public static void AfterWaterMove(bool __runOriginal,WaterMove? __state)
        {
            if(!__runOriginal||__state is not WaterMove s||s.Generation!=Generation||!NativeRegroupSession.Ready(s.State)||
                !Native.Same(s.Map,Native.Get(s.State,"Map"))||Native.Int(Native.Get(s.State,"CurrentPlayer"))!=s.Owner||
                Convert.ToUInt32(Native.Get(s.State,"CurrentTurn"))!=s.Turn||Native.Int(Native.Get(s.Unit,"owner"))!=s.Owner||
                !At(s.Destination,Native.Get(s.Unit,"coordinates")!)||Tile(s.State,s.Destination) is not object tile||!Native.Same(Native.Get(tile,"unit"),s.Unit))return;
            var frame=NativeGameStateEnvelope.Get(s.State);if(frame is null)return;
            var ledger=NativeIdentityLedgerCodec.Decode(frame.Bytes(),frame.Revision);
            var row=ledger.Units.SingleOrDefault(r=>r.Alive&&r.NativeId==s.NativeId&&r.LogicalId==s.Logical&&r.Kind==LedgerActualKind.Mimic);
            if(row is null||row.MimicWaterForm==s.Boat)return;
            var candidate=ledger with{Units=ledger.Units.Select(r=>r.LogicalId==s.Logical?r with{MimicWaterForm=s.Boat}:r).ToArray()};
            // Native movement already owns ordering. This records no extra
            // command, action cost or revision; envelope validation is atomic.
            NativeGameStateEnvelope.Set(s.State,frame.Revision,NativeIdentityLedgerCodec.Encode(candidate,frame.Revision));
        }
        static object? Tile(object state,object at)=>Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
        static bool At(object a,object b)=>Native.Int(Native.Get(a,"X"))==Native.Int(Native.Get(b,"X"))&&Native.Int(Native.Get(a,"Y"))==Native.Int(Native.Get(b,"Y"));
        static object[] Items(object? list)
        {
            if(list is null)return Array.Empty<object>();int count=Native.Int(Native.Get(list,"Count"));
            if(count<0||count>4096)throw new InvalidDataException("Mimic fallback exceeds the map bound.");return Native.Items(list).ToArray();
        }
        static Query? Begin(object state,object unit,object from)
        {
            if(Current is not null||!RecruitmentHooks.Registered||!NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(state)||
                !RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(unit,"type")!))||!At(from,Native.Get(unit,"coordinates")!))return null;
            uint id=Convert.ToUInt32(Native.Get(unit,"id"));object?[] found={id,null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,found)!||!Native.Same(found[1],unit)||
                Tile(state,from) is not object tile||!Native.Same(Native.Get(tile,"unit"),unit))return null;
            var row=NativeRegroupSession.ReadIdentity(state,id);
            if(row is not {Alive:true,Independent:true,Kind:LedgerActualKind.Mimic,Appearance:not null}||NativeShedDisguise.Exposed(row.Appearance)||
                Native.Int(Native.Get(unit,"health"))<=0)return null;
            // Exposed land uses native Warrior-derived one-movement data. Over
            // water it uses the ordinary exposed Raft profile and actual cargo.
            bool water=NativeMimicTransport.ExposedBoat(tile,row);
            string exposed=NativeMimicTransport.ExposedForm(row);
            object profile=Native.DetachedClone(unit,"UnitState");
            void SetForm(object body,string form)
            {var data=RecruitmentHooks.Data(state,form);Native.Set(body,"type",Native.Get(data,"type"));Native.Set(body,"UnitData",data);}
            SetForm(profile,water?RecruitmentCatalog.Variant("transportship"):exposed);
            // Exposing the real Mimic removes borrowed concealment. Keeping
            // Invisible here would incorrectly lend the fallback ZOC immunity.
            Native.Set(profile,"effects",Native.NewList("UnitEffect",Native.Items(Native.Get(unit,"effects")!).Where(e=>Native.Int(e)!=3)));
            object? cargo=null;
            if(water)
            {cargo=Native.DetachedClone(Native.Get(unit,"passengerUnit")??unit,"UnitState");SetForm(cargo,exposed);Native.Set(cargo,"passengerUnit",null);
             Native.Set(cargo,"effects",Native.NewList("UnitEffect",Native.Items(Native.Get(cargo,"effects")!).Where(e=>Native.Int(e)!=3)));}
            Native.Set(profile,"passengerUnit",cargo);
            return new(state,unit,profile,row);
        }
        static object? Fallback(object state,object unit,object from,object to)
        {
            using var q=Begin(state,unit,from);if(q is null)return null;
            int movement=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMovement","UnitState","GameState"),null,q.Profile,state));
            if(movement<=0)return null;
            return Native.Invoke(Native.M("PathFinder","GetPath","GameState","WorldCoordinates","WorldCoordinates","System.Int32","UnitState"),null,state,from,to,movement,q.Profile);
        }
        internal static object? Path(object state,object from,object to,object unit,object? native)
        {
            var path=Items(native);
            if(path.Length>0)
            {
                var row=NativeRegroupSession.ReadIdentity(state,Convert.ToUInt32(Native.Get(unit,"id")));
                if(path.Length<2||row is not {Alive:true,Independent:true,Kind:LedgerActualKind.Mimic,Appearance:not null}||
                    NativeShedDisguise.Exposed(row.Appearance)||NativeDisguiseMovement.Compatible(state,unit,row.Appearance,path))return native;
            }
            // A carrier can omit a native restriction (Cloak's city ban), or
            // its real effects can differ from the independently apparent ones.
            // Execute the exact real fallback path, never another carrier path
            // merely because the destination has a legal real alternative.
            return Fallback(state,unit,from,to)??Native.NewList("WorldCoordinates",Array.Empty<object>());
        }
        internal static object? Options(object state,object from,object unit,object? native)
        {
            using var q=Begin(state,unit,from);if(q is null)return native;
            int movement=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMovement","UnitState","GameState"),null,q.Profile,state));
            if(movement<=0)return native;
            var extra=Native.Invoke(Native.M("PathFinder","GetMoveOptions","GameState","WorldCoordinates","System.Int32","UnitState"),null,state,from,movement,q.Profile);
            if(!q.Valid)return Native.NewList("WorldCoordinates",Array.Empty<object>());
            q.Dispose();
            var original=Items(native);
            // Equal native/apparent effects keep the cheap normal hover. When
            // fake status changes movement legality, discard only endpoints
            // with no legal apparent or actual path. Cloak city endpoints have
            // their own narrow native restriction adapter.
            if(!q.Row.Appearance!.Effects.SequenceEqual(Native.Items(Native.Get(unit,"effects")!).Select(e=>e.ToString()!),StringComparer.Ordinal))
            {
                int apparentMovement=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMovement","UnitState","GameState"),null,unit,state));
                original=original.Where(at=>At(at,from)||Items(Native.Invoke(Native.M("PathFinder","GetPath","GameState","WorldCoordinates","WorldCoordinates","System.Int32","UnitState"),null,state,from,at,apparentMovement,unit)).Length>0).ToArray();
            }
            var combined=original.Concat(Items(extra)).GroupBy(x=>(Native.Int(Native.Get(x,"X")),Native.Int(Native.Get(x,"Y")))).Select(x=>x.First()).ToArray();
            if(combined.Length>4096)throw new InvalidDataException("Mimic movement union exceeds the map bound.");
            return combined.Length==Items(native).Length&&combined.Zip(Items(native),(a,b)=>At(a,b)).All(x=>x)?native:Native.NewList("WorldCoordinates",combined);
        }
        static bool AcceptedRoute(object state,object unit,object[] path)
        {
            if(path.Length<2)return false;var actual=Items(Fallback(state,unit,path[^1],path[0]));
            return actual.Length==path.Length&&actual.Zip(path,(a,b)=>At(a,b)).All(x=>x);
        }
        // MoveAction queues native shore children after moving. An incompatible
        // carrier must not enqueue its old form's converter: the successful
        // finalizer performs one identity-safe exposed conversion instead.
        public static void Transport(object __0,object __1,object __2,ref global::ActionUtils.EmbarkStatus __result)
        {
            if(Current is Query q&&Native.Same(q.State,__2)&&Native.Same(q.Profile,__0)&&q.Valid&&NativeMimicTransport.KeepsFloodMode(__2,q.Source,__1)||
                Current is null&&NativeMimicTransport.KeepsFloodMode(__2,__0,__1))
            {__result=(global::ActionUtils.EmbarkStatus)0;return;}
            if(Current is null&&RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(__0,"type")!))&&
                NativeDisguiseMovement.IsShedding(__2,__0)&&At(Native.Get(__0,"coordinates")!,Native.Get(__1,"coordinates")!))
                __result=(global::ActionUtils.EmbarkStatus)0;
        }
        public static void Sight(object __0,ref int __result)
        {
            if(ReadingSight||Current is not null||NativeDisguiseMovement.SheddingMimicData(__0) is not {} pending)return;
            object? tile=Tile(pending.State,Native.Get(pending.Unit,"coordinates")!);if(tile is null)return;
            var row=NativeRegroupSession.ReadIdentity(pending.State,Convert.ToUInt32(Native.Get(pending.Unit,"id")));if(row is null)return;
            var exposed=RecruitmentHooks.Data(pending.State,NativeMimicTransport.ExposedBoat(tile,row)?RecruitmentCatalog.Variant("transportship"):NativeMimicTransport.ExposedForm(row));
            ReadingSight=true;
            try{__result=Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetSightRange","Polytopia.Data.UnitData"),null,exposed));}
            finally{ReadingSight=false;}
        }
        static int Shore(object state,object unit,object destination)
        {
            using var q=Begin(state,unit,Native.Get(unit,"coordinates")!);if(q is null||Tile(state,destination) is not object tile)return 0;
            return Native.Int(Native.Invoke(Native.M("ActionUtils","WillUnitEmbark","UnitState","TileData","GameState"),null,q.Profile,tile,state));
        }
        static void Shed(object state,object unit,bool exhaustTransport)
        {
            uint id=Convert.ToUInt32(Native.Get(unit,"id"));
            if(!NativeShedDisguise.TryOffer(state,id,out var intent,out var why)||intent is null||
                !NativeShedDisguise.TryCommit(state,intent,out _,out why,exhaustTransport:exhaustTransport))
                throw new InvalidDataException("Mimic movement could not shed its incompatible disguise: "+why);
        }
    }
}
