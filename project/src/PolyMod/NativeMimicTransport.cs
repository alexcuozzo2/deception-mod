using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;
using HarmonyLib;

namespace Deception.PolyModAdapter
{
    // The caller owns authorization, tile roots, ID-counter and ledger publication.
    // This helper stages only a detached wrapper or changes to its exact cargo.
    // It never runs TrainUnit, UpgradeAction, movement or arrival consequences.
    internal static class NativeMimicTransport
    {
        [ThreadStatic] static bool Converting;
        static int Notes;
        internal static void Install(Harmony harmony)
        {
            Native.Hook(harmony,Native.M("EmbarkAction","Execute","GameState"),typeof(NativeMimicTransport),nameof(BeforeEmbark),nameof(AfterTransport),nameof(Finished));
            Native.Hook(harmony,Native.M("DisembarkAction","Execute","GameState"),typeof(NativeMimicTransport),nameof(BeforeDisembark),nameof(AfterTransport),nameof(Finished));
        }
        static void Require(bool value,string reason){if(!value)throw new InvalidDataException(reason);}
        static bool Ability(object data,int ability)=>Native.Get(data,"unitAbilities") is object list&&Native.Items(list).Any(a=>Native.Int(a)==ability);
        internal static bool IsVessel(object unit)=>Ability(Native.Get(unit,"UnitData")!,11);
        internal static bool IsVesselData(object data)=>Ability(data,11);
        internal static bool NaturallyAquatic(object data)=>Ability(data,13)||(bool)Native.Invoke(Native.M("UnitDataExtensions","IsAquatic","Polytopia.Data.UnitData"),null,data)!;
        // Exchange placement is not a movement action. Research can restrict
        // subsequent travel, but it does not change which terrain a physical
        // body can represent. Retain the native explicit form restriction and
        // native composite-terrain matching (including flooded ground).
        internal static bool CanOccupyExchange(object data,object tile)
        {
            if(Ability(data,13))return true;
            var terrain=Native.Get(data,"movementTerrain");
            return terrain is null||Native.Int(Native.Get(terrain,"Count"))==0||
                (bool)Native.Invoke(Native.M("TileData","matchesAnyTerrain",terrain.GetType().FullName!),tile,terrain)!;
        }
        internal static bool HasNativeEmbarkFamily(object state,object player)
        {
            // CanPlayerEmbark performs this same native improvement/ability
            // test over researched technologies. Use the tribe's complete
            // native tree for placement: no port or research is required, but
            // a tribe with no passenger-vessel path cannot gain a real Raft.
            var data=Native.Get(state,"GameLogicData")!;
            var tribe=Native.Invoke(Native.M(RecruitmentHooks.GLD,"GetTribeData","PolytopiaBackendBase.Common.TribeType"),data,Native.Get(player,"tribe"))!;
            var techs=Native.Invoke(Native.M(RecruitmentHooks.GLD,"GetAllTechForTribe","Polytopia.Data.TribeData"),data,tribe)!;
            var embark=Native.EnumValue("Polytopia.Data.ImprovementAbility+Type","embark");
            foreach(var tech in Native.Items(techs))
            {
                var improvements=Native.Invoke(Native.M(RecruitmentHooks.GLD,"GetImprovementsFromTech","Polytopia.Data.TechData","Polytopia.Data.TribeData"),data,tech,tribe)!;
                foreach(var improvement in Native.Items(improvements))
                    if((bool)Native.Invoke(Native.M("ImprovementDataExtensions","HasAbility","Polytopia.Data.ImprovementData","Polytopia.Data.ImprovementAbility+Type"),null,improvement,embark)!)return true;
            }
            return false;
        }
        internal static bool WaterEndpoint(object tile)
        {
            bool city=(bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),tile,Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!;
            return !city&&((bool)Native.Get(tile,"IsWater")!||(bool)Native.Invoke(Native.M("TileData","IsWetland"),tile)!);
        }
        internal static bool Wetland(object tile)=>(bool)Native.Invoke(Native.M("TileData","IsWetland"),tile)!;
        // Flood/algae are dual-use native terrain. They do not decide whether
        // an existing body is a passenger vessel; retain its recorded mode.
        internal static bool WaterEndpoint(object tile,bool wasBoat)=>Wetland(tile)?wasBoat:WaterEndpoint(tile);
        internal static string ExposedForm(LedgerUnit row)=>row.AmphibiousMimic?AquarionCatalog.MimicId:RecruitmentCatalog.MenuId;
        internal static bool ExposedBoat(object tile,LedgerUnit row)=>!row.AmphibiousMimic&&WaterEndpoint(tile,row.MimicWaterForm||row.Transport is not null);
        internal static bool KeepsFloodMode(object state,object unit,object tile)
        {
            if(!NativeRegroupSession.Enabled||!NativeRegroupSession.Ready(state)||!Mimic(unit)||!Wetland(tile))return false;
            object map=Native.Get(state,"Map")!,at=Native.Get(unit,"coordinates")!;
            var source=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,map,at);
            if(source is null||!Native.Same(Native.Get(source,"unit"),unit)||
                !Native.Same(tile,Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,map,Native.Get(tile,"coordinates"))))return false;
            var row=NativeRegroupSession.ReadIdentity(state,Convert.ToUInt32(Native.Get(unit,"id")));
            return row?.Kind==LedgerActualKind.Mimic;
        }
        internal static LedgerAppearance InterpretAppearance(object state,LedgerAppearance appearance,object tile)
        {
            if(Wetland(tile))return appearance;
            if(!WaterEndpoint(tile))return appearance.Passenger is null?appearance:LandAppearance(state,appearance);
            if(appearance.Passenger is not null||NaturallyAquatic(RecruitmentHooks.Data(state,appearance.Form)))return appearance;
            return EmbarkAppearance(state,appearance);
        }
        internal static string StandardVessel(string land)=>land switch
        {"giant"=>"juggernaut","dagger" or "mermaiddagger"=>"pirate","cloak" or "mermaidcloak"=>"cloak_boat",_=>"transportship"};

        internal static LedgerAppearance EmbarkAppearance(object state,LedgerAppearance land)
        {
            Require(land.Passenger is null,"Cannot embark an already wrapped apparent passenger.");
            string form=StandardVessel(land.Form);object data=RecruitmentHooks.Data(state,form);
            Require(IsVesselData(data),"Native standard embarkation is not a passenger vessel.");
            bool protectsPassenger=Ability(data,39);int health=Native.Int(Native.Get(data,"health"));
            Require(health is >0 and <=ushort.MaxValue,"Native apparent vessel health is outside bounds.");
            return land with{Form=form,Health=protectsPassenger?(ushort)health:land.Health,Maximum=protectsPassenger?(ushort)health:land.Maximum,
                Veteran=false,GrowthTurns=-1,GrowthDueTurn=-1,Effects=(string[])land.Effects.Clone(),Passenger=land};
        }
        internal static LedgerAppearance LandAppearance(object state,LedgerAppearance vessel)
        {
            var land=vessel.Passenger??throw new InvalidDataException("Apparent vessel has no independent passenger.");
            object data=RecruitmentHooks.Data(state,vessel.Form);Require(IsVesselData(data),"Apparent cargo belongs to a non-vessel.");
            ushort health=Ability(data,39)?land.Health:vessel.Health;
            Require(health>0&&health<=land.Maximum,"Native apparent landing health exceeds its passenger maximum.");
            return land with{Health=health,Effects=(string[])vessel.Effects.Clone(),Concealment=vessel.Concealment,Passenger=null};
        }

        static readonly string[] Fields={"id","leader","follower","owner","birthClimate","birthClimateSkinType","type",
            "previousTurnEndCoordinates","coordinates","home","passengerUnit","health","promotionLevel","xp","moved","attacked",
            "direction","flipped","createdTurn","UnitData","effects","isTemporaryExplorerUnit"};
        sealed class Image
        {
            internal readonly object Unit;
            readonly Dictionary<string,object?> Values;
            readonly object[] Effects;
            internal Image(object unit)
            {Unit=unit;Values=Fields.ToDictionary(x=>x,x=>Native.Get(unit,x));Effects=Native.Items(Values["effects"]!).ToArray();}
            internal object? this[string key]=>Values[key];
            internal void Assert()
            {
                foreach(var pair in Values)
                {
                    object? now=Native.Get(Unit,pair.Key);
                    bool same=pair.Key is "coordinates" or "home" or "previousTurnEndCoordinates"?
                        CoordinateEquals(now,pair.Value):ValueEquals(now,pair.Value);
                    Require(same,"Native transport source changed while staging: "+pair.Key);
                }
                Require(Native.Items(Native.Get(Unit,"effects")!).SequenceEqual(Effects),"Native transport effects changed while staging.");
            }
            internal void Restore(){foreach(var pair in Values)Native.Set(Unit,pair.Key,pair.Value);}
        }
        static bool ValueEquals(object? a,object? b)
        {
            if(a is null||b is null)return a is null&&b is null;
            if(a.GetType().IsValueType||a is string)return Equals(a,b);
            return Native.Same(a,b);
        }
        static bool CoordinateEquals(object? a,object? b)=>a is not null&&b is not null&&
            Native.Int(Native.Get(a,"X"))==Native.Int(Native.Get(b,"X"))&&Native.Int(Native.Get(a,"Y"))==Native.Int(Native.Get(b,"Y"));
        static void Unlinked(object unit)
        {Require(!(bool)Native.Invoke(Native.M("UnitState","HasLeader"),unit)!&&!(bool)Native.Invoke(Native.M("UnitState","HasFollower"),unit)!,"Linked units cannot enter independent transport.");}
        static bool Mimic(object unit)=>RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(unit,"type")!));
        static void MimicData(object data)
        {
            Require(Native.Int(Native.Get(data,"health"))==10&&Native.Int(Native.Get(data,"attack"))==0&&
                Native.Int(Native.Get(data,"defence"))==0&&Native.Int(Native.Get(data,"range"))==0,"Transport carrier violates actual Mimic invariants.");
        }
        internal static Pending StageConversion(object state,object unit,object destinationTile,string finalTypeId,uint reservedWrapperId,bool exhaust,string? finalPassengerType=null)
            =>new(state,unit,destinationTile,finalTypeId,reservedWrapperId,exhaust,finalPassengerType);

        // Native MoveCommand validity remains authoritative. This read-only
        // check merely determines whether its possible transport is supported.
        internal static bool CanHandleMovement(object state,object unit,object destination)
        {
            if(!NativeRegroupSession.Ready(state))return false;
            try
            {
                var snapshot=NativeGameStateEnvelope.Get(state);if(snapshot is null)return false;
                var row=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision).Units.SingleOrDefault(x=>x.Alive&&x.NativeId==Convert.ToUInt32(Native.Get(unit,"id")));
                if(row is null||row.Kind!=LedgerActualKind.Mimic||row.Appearance is null)return false;
                int status=Native.Int(Native.Invoke(Native.M("ActionUtils","WillUnitEmbark","UnitState","TileData","GameState"),null,unit,destination,state));
                if(status==1){_ = EmbarkAppearance(state,row.Appearance);return row.Transport is null&&!IsVessel(unit)&&Native.Get(unit,"passengerUnit") is null;}
                if(status==2){_ = LandAppearance(state,row.Appearance);return row.Transport is not null&&IsVessel(unit)&&Native.Get(unit,"passengerUnit") is object cargo&&Convert.ToUInt32(Native.Get(cargo,"id"))==row.Transport.PassengerNativeId;}
                return status==0;
            }
            catch{return false;}
        }
        internal sealed class ActionScope
        {
            internal object State=null!,Action=null!,Tile=null!,OriginalUnit=null!;
            internal NativeGameStateEnvelope.Snapshot Snapshot=null!;
            internal NativeIdentityLedger Ledger=null!;
            internal LedgerUnit Row=null!;
            internal LedgerAppearance? Appearance;
            internal uint Counter,PassengerId;
            internal bool Boarding,NativeOriginal,Completed;
        }
        public static bool BeforeEmbark(object __instance,object __0,out ActionScope? __state)=>Before(__instance,__0,true,out __state);
        public static bool BeforeDisembark(object __instance,object __0,out ActionScope? __state)=>Before(__instance,__0,false,out __state);
        static bool Before(object action,object state,bool boarding,out ActionScope? scope)
        {
            scope=null;
            if(!NativeRegroupSession.Enabled)return true;
            if(!NativeRegroupSession.Ready(state))
            {
                var at=Native.Get(action,"Coordinates")!;
                var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
                if(tile is not null&&Native.Get(tile,"unit") is object unit&&Mimic(unit))
                {NativeRegroupSession.Fault("Unready Mimic transport boundary.");throw new InvalidDataException("Mimic transport has no authenticated local state.");}
                return true;
            }
            try
            {
                Require(!Converting,"Reentrant native transport boundary.");
                var at=Native.Get(action,"Coordinates")!;
                var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
                if(tile is null||Native.Get(tile,"unit") is not object unit)return true;
                Require(CoordinateEquals(Native.Get(unit,"coordinates"),at),"Transport action target no longer occupies its coordinate.");
                Require(Native.Int(Native.Get(action,"PlayerId"))==Native.Int(Native.Get(state,"CurrentPlayer"))&&
                    Native.Int(Native.Get(unit,"owner"))==Native.Int(Native.Get(action,"PlayerId")),"Native transport actor/owner changed.");
                NativeRegroupSession.Prepare(state);
                var snapshot=NativeGameStateEnvelope.Get(state)??throw new InvalidDataException("Transport lost its private ledger.");
                var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
                var row=ledger.Units.SingleOrDefault(x=>x.Alive&&x.NativeId==Convert.ToUInt32(Native.Get(unit,"id")));
                if(row is null){Require(!Mimic(unit),"Untracked Mimic cannot enter native transport.");return true;}
                // Native flooded cities can enqueue Disembark despite their
                // dual-use terrain. Treat that child as a completed no-op for
                // Mimics: clearing only the ledger passenger while refitting
                // the unchanged ship would invalidate the entire match.
                if(row.Kind==LedgerActualKind.Mimic&&Wetland(tile))return false;
                Require(IsVessel(unit)!=boarding,"Native transport direction no longer matches its root.");Unlinked(unit);
                object? passenger=Native.Get(unit,"passengerUnit");
                if(boarding)Require(row.Transport is null&&passenger is null,"Native boarding already has actual cargo.");
                else Require(passenger is not null&&row.Transport is not null&&row.Transport.PassengerNativeId==Convert.ToUInt32(Native.Get(passenger,"id")),"Native landing cargo differs from its recorded lineage.");
                // Real transport and apparent transport are independent. A
                // genuine boat can wear an amphibian or flying disguise with
                // no apparent cargo, and a boarding real passenger may already
                // look aquatic. Interpret the mask against its endpoint rather
                // than inventing an apparent vessel/passenger relationship.
                var appearance=row.Appearance is null?null:InterpretAppearance(state,row.Appearance,tile);
                bool unwrapped=!boarding&&row.Appearance?.Passenger is not null&&appearance?.Passenger is null;
                // Native landing immediately queues one overdue passenger
                // growth step. Cargo does not hatch while still aboard. Coast
                // exchanges call the pure conversion above and defer growth.
                if(unwrapped&&appearance is not null&&NativeApparentGrowth.IsGrowing(appearance.Form))
                    appearance=NativeApparentGrowth.Advance(appearance,Convert.ToUInt32(Native.Get(state,"CurrentTurn")));
                uint counter=Convert.ToUInt32(Native.Get(state,"CurrentUnitId"));Require(counter<uint.MaxValue-3,"Native transport ID counter is exhausted.");
                scope=new(){State=state,Action=action,Tile=tile,OriginalUnit=unit,Snapshot=snapshot,Ledger=ledger,Row=row,Appearance=appearance,
                    Counter=counter,Boarding=boarding,PassengerId=Convert.ToUInt32(Native.Get(boarding?unit:passenger!,"id")),NativeOriginal=row.Kind==LedgerActualKind.Genuine};
                Converting=true;
                if(scope.NativeOriginal)return true; // Keep genuine splash/growth/score/diplomacy exactly native.
                CommitMimic(scope);return false;
            }
            catch(Exception e){if(scope is null)Converting=false;NativeRegroupSession.Fault("Native transport preflight: "+e.Message);throw;}
        }
        static NativeIdentityLedger Rebind(ActionScope scope,object root,uint counter)
        {
            var transport=scope.Boarding?new LedgerTransport(scope.PassengerId):null;
            var row=scope.Row with{NativeId=Convert.ToUInt32(Native.Get(root,"id")),Transport=transport,Appearance=scope.Appearance};
            return scope.Ledger with{NextLogicalId=Math.Max(scope.Ledger.NextLogicalId,(ulong)counter+2),
                Units=scope.Ledger.Units.Select(x=>x.LogicalId==row.LogicalId?row:x).ToArray()};
        }
        static void CommitMimic(ActionScope scope)
        {
            Require(scope.Appearance is not null,"A Mimic transport needs its independent appearance.");
            uint finalCounter=scope.Boarding?checked(scope.Counter+1):scope.Counter;
            string type=RecruitmentCatalog.Variant(scope.Appearance.Form);
            using var conversion=StageConversion(scope.State,scope.OriginalUnit,scope.Tile,type,scope.Boarding?finalCounter:0,true);
            var completedLedger=Rebind(scope,conversion.FinalUnit,finalCounter);
            byte[] payload=NativeIdentityLedgerCodec.Encode(completedLedger,scope.Snapshot.Revision);
            NativeGrowthSight.Pending? sight=null;bool touched=false;
            try
            {
                touched=true;conversion.Apply();Native.Set(scope.Tile,"unit",conversion.FinalUnit);
                Native.Set(scope.State,"CurrentUnitId",finalCounter);
                Native.Set(conversion.FinalUnit,"effects",NativeDisguiseConcealment.WithConcealment(Native.Get(conversion.FinalUnit,"effects")!,scope.Appearance,conversion.FinalUnit));
                sight=NativeGrowthSight.Stage(scope.State,Convert.ToByte(Native.Get(conversion.FinalUnit,"owner")),new[]{conversion.FinalUnit});
                NativeLedgerState.Validate(scope.State,scope.Snapshot.Revision,payload);
                NativeGameStateEnvelope.Set(scope.State,scope.Snapshot.Revision,payload);
                conversion.PrepareAcceptance();sight.Accept();conversion.Accept();scope.Completed=true;
                RecordLandingGrowth(scope,completedLedger);
                if(Notes++<32)Entry.Note("mimic-transport",scope.Boarding?"Native boarding completed with actual Mimic cargo and independent vessel appearance.":"Native landing restored the original actual Mimic and its apparent passenger.");
            }
            catch
            {
                sight?.Rollback();
                if(touched){conversion.Rollback();Native.Set(scope.Tile,"unit",scope.OriginalUnit);Native.Set(scope.State,"CurrentUnitId",scope.Counter);NativeGameStateEnvelope.Set(scope.State,scope.Snapshot.Revision,scope.Snapshot.Bytes());}
                throw;
            }
            finally{sight?.Dispose();}
        }
        public static void AfterTransport(ActionScope? __state)
        {
            if(__state is not ActionScope scope||!scope.NativeOriginal)return;
            try
            {
                object root=Native.Get(scope.Tile,"unit")??throw new InvalidDataException("Native converter removed the transport root.");
                uint counter=Convert.ToUInt32(Native.Get(scope.State,"CurrentUnitId"));
                if(scope.Boarding)
                    Require(!Native.Same(root,scope.OriginalUnit)&&Native.Same(Native.Get(root,"passengerUnit"),scope.OriginalUnit)&&
                        IsVessel(root)&&Convert.ToUInt32(Native.Get(root,"id"))==scope.Counter+1&&counter==scope.Counter+2,"Native embarkation lineage/allocation differed from its pinned constructor.");
                else Require(Convert.ToUInt32(Native.Get(root,"id"))==scope.PassengerId&&counter==scope.Counter&&!IsVessel(root),"Native disembarkation did not restore its actual passenger.");
                Require(Native.Int(Native.Get(root,"owner"))==Native.Int(Native.Get(scope.OriginalUnit,"owner"))&&
                    CoordinateEquals(Native.Get(root,"coordinates"),Native.Get(scope.Tile,"coordinates")),"Native transport changed owner or final coordinate.");
                var completedLedger=Rebind(scope,root,counter);
                byte[] payload=NativeIdentityLedgerCodec.Encode(completedLedger,scope.Snapshot.Revision);
                object oldEffects=Native.Get(root,"effects")!;
                try
                {
                    if(scope.Appearance is not null)
                        Native.Set(root,"effects",NativeDisguiseConcealment.WithConcealment(oldEffects,scope.Appearance,root));
                    NativeLedgerState.Validate(scope.State,scope.Snapshot.Revision,payload);NativeGameStateEnvelope.Set(scope.State,scope.Snapshot.Revision,payload);scope.Completed=true;
                }
                catch{Native.Set(root,"effects",oldEffects);throw;}
                RecordLandingGrowth(scope,completedLedger);
            }
            catch(Exception e){NativeRegroupSession.Fault("Native genuine transport lineage: "+e.Message);throw;}
        }
        static void RecordLandingGrowth(ActionScope scope,NativeIdentityLedger completed)
        {
            if(!scope.Boarding&&scope.Row.Appearance?.Passenger is LedgerAppearance before&&scope.Appearance?.Passenger is null)
                NativeDisguiseGrowthAudio.Record(scope.Action,scope.State,before,completed.Units.Single(r=>r.LogicalId==scope.Row.LogicalId));
        }
        public static Exception? Finished(Exception? __exception,ActionScope? __state)
        {
            if(__state is not null)
            {
                Converting=false;
                if(__exception is not null||!__state.Completed)
                {
                    NativeDisguiseGrowthAudio.Cancel(__state.Action,__state.State);
                    NativeRegroupSession.Fault("Native transport action did not complete: "+(__exception?.Message??"missing completion"));
                }
            }
            return __exception;
        }

        internal sealed class Pending:IDisposable
        {
            readonly object State,Destination,FinalData,FinalType,FinalCoordinates;
            readonly Image Before,CargoBefore;
            readonly bool Boarding,Exhaust,IsMimic,Refit;
            readonly object? CargoType,CargoData;
            bool Applied,Accepted,Disposed,AcceptancePrepared;
            internal object BeforeUnit=>Before.Unit;
            internal object FinalUnit {get;}
            internal uint? PassengerNativeId {get;}
            internal uint AllocatedId {get;}
            internal Pending(object state,object unit,object destinationTile,string finalTypeId,uint reservedWrapperId,bool exhaust,string? finalPassengerType)
            {
                Require(Native.Int(Native.Get(state,"Version"))==123,"Native transport requires state123.");
                State=state;Destination=destinationTile;Exhaust=exhaust;Before=new(unit);Unlinked(unit);
                Require(Native.Int(Native.Get(unit,"health"))>0,"A dead unit cannot enter transport.");
                object map=Native.Get(state,"Map")!;FinalCoordinates=Native.Get(destinationTile,"coordinates")!;
                Require(Native.Same(destinationTile,Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,map,FinalCoordinates)),"Transport destination is foreign.");
                FinalType=Native.EnumValue(RecruitmentHooks.UT,finalTypeId);FinalData=RecruitmentHooks.Data(state,finalTypeId);
                Require(Equals(Native.Get(FinalData,"type"),FinalType),"Transport target type/data disagree.");
                bool wasVessel=IsVessel(unit),willVessel=IsVesselData(FinalData);IsMimic=Mimic(unit);Refit=wasVessel&&willVessel&&IsMimic;
                Require(wasVessel!=willVessel||Refit,"Transport staging requires one shore transition or a Mimic vessel refit.");
                Boarding=!wasVessel&&willVessel;
                Require(IsMimic==RecruitmentCatalog.IsMimicId(finalTypeId),"Transport cannot turn genuine identity into Mimic or the reverse.");
                if(IsMimic){MimicData(FinalData);if(willVessel)Require(Native.Int(Native.Get(FinalData,"cost"))==0,"Mimic wrapper must not duplicate its passenger paid cost.");}
                if(Boarding)
                {
                    Require(Native.Get(unit,"passengerUnit") is null,"Boarding source already carries a passenger.");
                    uint counter=Convert.ToUInt32(Native.Get(state,"CurrentUnitId"));
                    Require(reservedWrapperId>counter&&reservedWrapperId!=uint.MaxValue,"Transport wrapper ID was not reserved above the native counter.");
                    FinalUnit=Activator.CreateInstance(Native.T("UnitState"))??throw new InvalidDataException("Native wrapper allocation failed.");
                    Require(!Native.Same(FinalUnit,unit),"Native wrapper aliases its passenger.");
                    AllocatedId=reservedWrapperId;PassengerNativeId=Convert.ToUInt32(Native.Get(unit,"id"));CargoBefore=Before;
                    // Populate detached storage only. Keep the original actual
                    // passenger (and its XP/promotion/history) intact.
                    foreach(string key in new[]{"owner","birthClimate","birthClimateSkinType","home","previousTurnEndCoordinates","direction","flipped","createdTurn","isTemporaryExplorerUnit"})
                        Native.Set(FinalUnit,key,Before[key]);
                    Native.Set(FinalUnit,"id",reservedWrapperId);Native.Set(FinalUnit,"type",FinalType);Native.Set(FinalUnit,"UnitData",FinalData);
                    Native.Set(FinalUnit,"coordinates",FinalCoordinates);Native.Set(FinalUnit,"passengerUnit",unit);
                    Native.Set(FinalUnit,"health",IsMimic?(ushort)10:Ability(FinalData,39)?Convert.ToUInt16(Native.Get(FinalData,"health")):Before["health"]);
                    Native.Set(FinalUnit,"promotionLevel",(ushort)0);Native.Set(FinalUnit,"xp",(ushort)0);
                    Native.Set(FinalUnit,"moved",exhaust||(bool)Before["moved"]!);Native.Set(FinalUnit,"attacked",exhaust||(bool)Before["attacked"]!);
                    Native.Set(FinalUnit,"effects",Native.NewList("UnitEffect",Native.Items(Before["effects"]!)));
                    if(IsMimic)Require(Native.Int(Native.Get(FinalData,"cost"))==0,"Mimic wrapper must not duplicate its passenger's paid cost.");
                }
                else
                {
                    object cargo=Native.Get(unit,"passengerUnit")??throw new InvalidDataException("Transport has no actual passenger to restore.");FinalUnit=Refit?unit:cargo;CargoBefore=new(cargo);Unlinked(cargo);
                    Require(Native.Get(cargo,"passengerUnit") is null&&!Native.Same(cargo,unit)&&
                        Native.Int(Native.Get(cargo,"owner"))==Native.Int(Before["owner"]),"Actual cargo graph is invalid.");
                    Require(Mimic(cargo)==IsMimic,"Actual passenger identity differs from its wrapper.");
                    // A genuine passenger's type is never copied from its mask.
                    Require(IsMimic||Equals(Native.Get(cargo,"type"),FinalType),"Landing would replace genuine cargo type.");
                    Require(reservedWrapperId==0,"Landing cannot allocate a new physical identity.");
                    PassengerNativeId=Refit?Convert.ToUInt32(Native.Get(cargo,"id")):null;
                }
                if(willVessel&&IsMimic)
                {
                    string cargoId=finalPassengerType??Native.EnumName(Native.Get(CargoBefore.Unit,"type")!);
                    Require(RecruitmentCatalog.IsMimicId(cargoId),"Mimic vessel requires actual Mimic cargo.");
                    CargoType=Native.EnumValue(RecruitmentHooks.UT,cargoId);CargoData=RecruitmentHooks.Data(state,cargoId);MimicData(CargoData);
                    Require(!IsVesselData(CargoData),"Mimic cargo cannot itself be a vessel.");
                }
                Before.Assert();if(!Boarding)CargoBefore.Assert();
            }
            internal void Apply()
            {
                Require(!Disposed&&!Applied&&!Accepted,"Transport conversion applied twice.");Before.Assert();if(!Boarding)CargoBefore.Assert();
                Applied=true;
                if(Refit)
                {
                    Native.Set(FinalUnit,"type",FinalType);Native.Set(FinalUnit,"UnitData",FinalData);
                    Native.Set(FinalUnit,"coordinates",FinalCoordinates);
                }
                else if(!Boarding)
                {
                    Native.Set(FinalUnit,"type",FinalType);Native.Set(FinalUnit,"UnitData",FinalData);Native.Set(FinalUnit,"coordinates",FinalCoordinates);
                    if(IsMimic)Native.Set(FinalUnit,"health",(ushort)10);
                    else if(!Ability(Before["UnitData"]!,39))Native.Set(FinalUnit,"health",Before["health"]);
                    foreach(string key in new[]{"direction","flipped"})Native.Set(FinalUnit,key,Before[key]);
                    Native.Set(FinalUnit,"moved",Exhaust||(bool)Before["moved"]!);Native.Set(FinalUnit,"attacked",Exhaust||(bool)Before["attacked"]!);
                    Native.Set(FinalUnit,"effects",Native.NewList("UnitEffect",Native.Items(Before["effects"]!)));
                }
                if(CargoData is not null){Native.Set(CargoBefore.Unit,"type",CargoType);Native.Set(CargoBefore.Unit,"UnitData",CargoData);}
            }
            internal void PrepareAcceptance()
            {
                Require(Applied&&!Disposed&&!Accepted,"Transport acceptance is out of order.");
                Require(Native.Same(Native.Get(Destination,"unit"),FinalUnit)&&CoordinateEquals(Native.Get(FinalUnit,"coordinates"),FinalCoordinates),"Transport root was not published at its destination.");
                if(AllocatedId!=0)Require(Convert.ToUInt32(Native.Get(State,"CurrentUnitId"))>=AllocatedId,"Transport native allocation counter was not published.");
                Require(Equals(Native.Get(FinalUnit,"type"),FinalType)&&Native.Same(Native.Get(FinalUnit,"UnitData"),FinalData),"Transport final carrier changed.");
                if(Boarding||Refit)Require(Native.Same(Native.Get(FinalUnit,"passengerUnit"),CargoBefore.Unit),"Transport lost its actual cargo.");
                if(IsMimic)Require(Native.Int(Native.Get(FinalUnit,"health"))==10&&Native.Int(Native.Get(FinalUnit,"promotionLevel"))==0,"Actual Mimic changed health/promotion during transport.");
                AcceptancePrepared=true;
            }
            internal void Accept()
            {Require(AcceptancePrepared&&Applied&&!Disposed&&!Accepted,"Transport acceptance was not prepared.");Accepted=true;}
            internal void Rollback()
            {if(Disposed||Accepted)return;if(Applied){CargoBefore.Restore();if(Refit)Before.Restore();}Disposed=true;}
            public void Dispose(){if(!Accepted)Rollback();Disposed=true;}
        }
    }
}
