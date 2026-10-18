using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Value-only native snapshot adapter. Enrollment/reconciliation is called
    // solely from authenticated lifecycle/command/transport boundaries.
    internal static class NativeLedgerState
    {
        const string Tribe="PolytopiaBackendBase.Common.TribeType",Skin="PolytopiaBackendBase.Common.SkinType";
        static bool Supported(string form)=>RecruitmentHooks.Forms.Values.Any(x=>x.NativeId==form);
        internal sealed record Body(object Unit,bool Root);
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        internal static Dictionary<uint,Body> Graph(object state)
        {
            Require(Native.Int(Native.Get(state,"Version"))==123,"Ledger requires native state123.");
            var map=Native.Get(state,"Map")??throw new InvalidDataException("Missing native map.");
            var tiles=Native.Get(map,"Tiles")!;int count=Native.Int(Native.Get(tiles,"Length"));
            Require(count is >0 and <=4096,"Native ledger map outside bound.");
            var bodies=new Dictionary<uint,Body>();var pointers=new HashSet<long>();
            void Visit(object unit,int depth)
            {
                Require(depth<=8&&bodies.Count<16384&&pointers.Add(Native.Pointer(unit)),"Invalid or cyclic native identity graph.");
                uint id=Convert.ToUInt32(Native.Get(unit,"id"));Require(bodies.TryAdd(id,new(unit,depth==0)),"Duplicate native unit ID.");
                var passenger=Native.Get(unit,"passengerUnit");if(passenger is not null)Visit(passenger,depth+1);
            }
            var item=Native.M(tiles.GetType(),"get_Item","System.Int32");
            for(int i=0;i<count;i++){var tile=Native.Invoke(item,tiles,i)!;var unit=Native.Get(tile,"unit");if(unit is not null)Visit(unit,0);}
            return bodies;
        }
        static string EnumId(object value)=>Enum.GetName(value.GetType(),value)??throw new InvalidDataException("Unknown native visual enum value.");
        static object EnumValue(string type,string name)
        {
            var t=Native.T(type);Require(Enum.GetNames(t).Contains(name,StringComparer.Ordinal),"Unknown native visual profile: "+name);
            return Enum.Parse(t,name,false);
        }
        static object Player(object state,object unit)
        {
            object?[] args={Convert.ToByte(Native.Get(unit,"owner")),null};
            Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&args[1] is not null,"Native unit owner is missing.");return args[1]!;
        }
        static void IndependentBody(Body body)
        {
            Require(body.Root&&
                !(bool)Native.Invoke(Native.M("UnitState","HasLeader"),body.Unit)!&&
                !(bool)Native.Invoke(Native.M("UnitState","HasFollower"),body.Unit)!,"Independent appearance requires an unlinked root body.");
        }
        static (LedgerVisualProfile Tribe,LedgerVisualProfile Climate,LedgerVisualProfile Birth,int Color) Visuals(object state,object unit,object player)
        {
            object gld=Native.Get(state,"GameLogicData")!;
            object visual=Native.Invoke(Native.M("PlayerExtensions","GetVisualTribeType","PlayerState"),null,player)!;
            object skin=Native.Invoke(Native.M("PlayerState","GetVisualSkin"),player)!;
            // Native PlayerState.climate lazily writes _climate. Resolve the
            // same fallback as a value, without touching the supplied state.
            object climate=Native.Get(player,"_climate")!,tribe=Native.Get(player,"tribe")!;
            if(Native.Int(climate)==0)climate=tribe;
            if(Native.Int(Native.Get(player,"tribeMix"))!=0)
            {
                var data=Native.Invoke(Native.M(RecruitmentHooks.GLD,"GetTribeData",Tribe),gld,tribe)!;
                climate=Native.Get(data,"type")!;
            }
            return(new(EnumId(visual),EnumId(skin)),new(EnumId(climate),EnumId(skin)),
                new(EnumId(Native.Get(unit,"birthClimate")!),EnumId(Native.Get(unit,"birthClimateSkinType")!)),Native.Int(Native.Get(player,"color")));
        }
        internal static LedgerAppearance ExposedAppearance(object state,object unit,LedgerUnit row)
        {
            Require(row.Alive&&row.Kind==LedgerActualKind.Mimic&&row.NativeId==Convert.ToUInt32(Native.Get(unit,"id")),"Exposed appearance requires the exact live Mimic.");
            object player=Player(state,unit);
            if(row.Rogue is LedgerRogue rogue)
            {
                Require(Native.Int(Native.Get(unit,"owner"))==rogue.Commander,"Exposed Rogue command owner differs.");
                object?[] args={rogue.Owner,null};
                Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&args[1] is not null,"Exposed Rogue original owner is missing.");
                player=args[1]!;
            }
            var visual=Visuals(state,Native.Get(unit,"passengerUnit")??unit,player);
            return new(row.AmphibiousMimic?AquarionCatalog.MimicId:RecruitmentCatalog.MenuId,visual.Tribe,visual.Climate,visual.Birth,
                visual.Color,10,10,false,ApparentConcealment.Visible,-1,Array.Empty<string>());
        }
        internal static LedgerAppearance Capture(object state,Body body,string form,bool legacyMimic)
            =>Capture(state,body,form,legacyMimic,null);
        internal static LedgerAppearance CapturePublic(object state,Body body,string form,LedgerUnit? row)
        {
            if(row?.Appearance is LedgerAppearance appearance)
            {
                Require(row.Alive&&row.Independent&&row.NativeId==Convert.ToUInt32(Native.Get(body.Unit,"id")),"Public appearance requires the exact live independent body.");
                return row.Kind==LedgerActualKind.Genuine?NativeDisguiseConcealment.EffectiveAppearance(appearance,body.Unit):appearance;
            }
            if(row?.Rogue is not LedgerRogue rogue)return Capture(state,body,form,false);
            Require(row.Alive&&row.Kind==LedgerActualKind.Genuine&&row.Appearance is null&&row.NativeId==Convert.ToUInt32(Native.Get(body.Unit,"id")),"Public Rogue capture requires the exact unmasked genuine body.");
            Require(Native.Int(Native.Get(body.Unit,"owner"))==rogue.Commander,"Public Rogue command owner differs.");
            object?[] args={rogue.Owner,null};
            Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&args[1] is not null,"Public Rogue original owner is missing.");
            return Capture(state,body,form,false,args[1]);
        }
        static LedgerAppearance Capture(object state,Body body,string form,bool legacyMimic,object? visualPlayer)
        {
            IndependentBody(body);Require(Supported(form),"Appearance form has no registered Mimic carrier: "+form);
            var unit=body.Unit;var visual=Visuals(state,unit,visualPlayer??Player(state,unit));
            object? cargo=Native.Get(unit,"passengerUnit");
            bool vessel=RecruitmentCatalog.NavalCopyOnly.Contains(form,StringComparer.Ordinal);
            Require(vessel==(cargo is not null),"Native vessel/cargo relationship is incomplete.");
            LedgerAppearance? passenger=null;
            if(cargo is not null)
            {
                Require(!legacyMimic,"A legacy Mimic boat has no reconstructable passenger appearance.");
                Require(Native.Get(cargo,"passengerUnit") is null&&Native.Int(Native.Get(cargo,"owner"))==Native.Int(Native.Get(unit,"owner")),"Nested or foreign native cargo cannot be represented.");
                string cargoType=Native.EnumName(Native.Get(cargo,"type")!);
                passenger=Capture(state,new Body(cargo,true),cargoType,false,visualPlayer);
            }
            string[] effects=Effects(unit);
            int promotion=Native.Int(Native.Get(unit,"promotionLevel"));
            Require(promotion is 0 or 1,"Appearance schema cannot losslessly represent multiple promotions.");
            var native=RecruitmentHooks.Data(state,form);int baseHealth=Native.Int(Native.Get(native,"health"));
            int maximum=legacyMimic?baseHealth:Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMaxHealth","UnitState","GameState"),null,unit,state));
            int current=legacyMimic?maximum:Native.Int(Native.Get(unit,"health"));
            Require(maximum==(passenger is not null&&!ProtectsPassenger(native)?passenger.Maximum:baseHealth+(legacyMimic?0:promotion*50)),"Native independent max-health profile changed.");
            Require(current>0&&maximum>=current&&maximum<=ushort.MaxValue,"Native apparent health outside supported range.");
            var appearance=new LedgerAppearance(form,visual.Tribe,visual.Climate,visual.Birth,
                visual.Color,(ushort)current,(ushort)maximum,!legacyMimic&&promotion==1,
                effects.Contains("Invisible",StringComparer.Ordinal)?ApparentConcealment.Hidden:ApparentConcealment.Visible,-1,effects,Passenger:passenger);
            if(NativeApparentGrowth.IsGrowing(form))
            {
                Require(!legacyMimic,"Growing carrier cannot invent a missing private timer.");
                Require(Native.Int(Native.Get(native,"growthRate"))==NativeApparentGrowth.Duration(form),"Native dragon growth rate changed.");
                appearance=NativeApparentGrowth.CaptureTimer(appearance,Convert.ToUInt32(Native.Get(unit,"createdTurn")),Convert.ToUInt32(Native.Get(state,"CurrentTurn")));
            }
            return appearance;
        }
        static bool ProtectsPassenger(object data)
        {
            // Native GetMaxHealth uses Protect (39), not Stiff (38): ordinary
            // vessels inherit their passenger's maximum even when they are stiff.
            object? abilities=Native.Get(data,"unitAbilities");if(abilities is null)return false;
            int count=Native.Int(Native.Get(abilities,"Count"));Require(count is >=0 and <=128,"Native ability list exceeds ledger bound.");
            var item=Native.M(abilities.GetType(),"get_Item","System.Int32");
            for(int i=0;i<count;i++)if(Native.Int(Native.Invoke(item,abilities,i))==39)return true;
            return false;
        }
        static string[] Effects(object unit)
        {
            object effects=Native.Get(unit,"effects")??throw new InvalidDataException("Missing native effects list.");
            int count=Native.Int(Native.Get(effects,"Count"));Require(count is >=0 and <=16,"Native status snapshot exceeds ledger bound.");
            var names=new SortedSet<string>(StringComparer.Ordinal);var item=Native.M(effects.GetType(),"get_Item","System.Int32");
            for(int i=0;i<count;i++)Require(names.Add(EnumId(Native.Invoke(item,effects,i)!)),"Duplicate native status cannot be represented canonically.");
            return names.ToArray();
        }
        // Call only at a complete command/action/transport boundary. Appearance
        // is independent: existing snapshots are NEVER regenerated from the
        // current native carrier, health, owner colors, or status list.
        static bool MimicWater(object state,object unit,bool previous)
        {
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),Native.Get(unit,"coordinates"))!;
            if((bool)Native.Invoke(Native.M("TileData","IsWetland"),tile)!)return previous;
            // Improvement IDs use the native registry ("city" -> City), not
            // the case-sensitive persisted visual-enum validation above.
            return (bool)Native.Get(tile,"IsWater")!&&!(bool)Native.Invoke(Native.M("TileData","HasImprovement","Polytopia.Data.ImprovementData+Type"),tile,Native.EnumValue("Polytopia.Data.ImprovementData+Type","city"))!;
        }
        internal static (long Revision,byte[] Payload,bool Changed) Reconcile(object state,NativeGameStateEnvelope.Snapshot snapshot)
        {
            var ledger=NativeIdentityLedgerCodec.Decode(snapshot.Bytes(),snapshot.Revision);
            var graph=Graph(state);uint counter=Convert.ToUInt32(Native.Get(state,"CurrentUnitId"));
            Require(counter<uint.MaxValue&&graph.Keys.All(id=>id<=counter),"Native ID counter wrapped or trails a live identity.");
            var units=new List<LedgerUnit>();var known=new HashSet<uint>();bool changed=false;ulong next=ledger.NextLogicalId;
            var roots=graph.Where(p=>p.Value.Root).ToDictionary(p=>p.Key,p=>p.Value);
            var sourceRows=NormalizeTransportRows(graph,ledger,out bool normalized);changed|=normalized;
            foreach(var original in sourceRows)
            {
                var row=original;
                // A genuine native landing restores the exact old passenger.
                if(row.Alive&&!graph.ContainsKey(row.NativeId)&&row.Transport is LedgerTransport transport&&roots.ContainsKey(transport.PassengerNativeId))
                {
                    Require(!row.Independent,"Independent landing needs an explicit transport transition.");
                    row=row with{NativeId=transport.PassengerNativeId,Transport=null};changed=true;
                }
                Require(known.Add(row.NativeId),"Ledger has ambiguous native-ID lineage.");
                if(!row.Alive){Require(!graph.ContainsKey(row.NativeId),"Retired native ID was reused; an explicit replacement transition is required.");units.Add(row);continue;}
                if(!graph.ContainsKey(row.NativeId))
                {units.Add(row with{Alive=false,Independent=false,Appearance=null,Transport=null,Rogue=null});changed=true;continue;}
                Require(roots.ContainsKey(row.NativeId),"A living logical identity must own a native root.");
                if(row.Transport is LedgerTransport binding)Require(known.Add(binding.PassengerNativeId),"Duplicate native passenger lineage.");
                if(row.Kind==LedgerActualKind.Mimic)
                {
                    bool water=!row.AmphibiousMimic&&MimicWater(state,graph[row.NativeId].Unit,row.MimicWaterForm||row.Transport is not null);
                    if(water!=row.MimicWaterForm){row=row with{MimicWaterForm=water};changed=true;}
                }
                else if(row.Independent&&row.Appearance is LedgerAppearance appearance)
                {
                    // A40: migrate the old visible-mask snapshot only at this
                    // genuine native boundary. Rendering never writes a ledger.
                    var effective=NativeDisguiseConcealment.EffectiveAppearance(appearance,graph[row.NativeId].Unit);
                    if(!ReferenceEquals(effective,appearance)){row=row with{Appearance=effective};changed=true;}
                }
                units.Add(row);
            }
            foreach(var pair in roots.OrderBy(x=>x.Key))
            {
                string type=Native.EnumName(Native.Get(pair.Value.Unit,"type")!);
                bool mimic=RecruitmentCatalog.IsMimicId(type);
                if(known.Contains(pair.Key)||(!mimic&&!NativeRegroupSession.Enabled))continue;
                RecruitmentForm? form=null;
                if(mimic)Require(RecruitmentHooks.Forms.TryGetValue(type,out form),"Unknown native Mimic carrier cannot be enrolled.");
                Require(next<ulong.MaxValue,"Logical identity counter exhausted.");
                object? cargo=Native.Get(pair.Value.Unit,"passengerUnit");
                uint logicalSource=cargo is null?pair.Key:Convert.ToUInt32(Native.Get(cargo,"id"));
                ulong logical=NativeRegroupSession.Enabled?(ulong)logicalSource+1:next++;
                Require(!units.Any(row=>row.LogicalId==logical),"Native/logical identity mapping collides with an existing lineage.");
                units.Add(new(logical,pair.Key,true,mimic?LedgerActualKind.Mimic:LedgerActualKind.Genuine,mimic,
                    mimic?Capture(state,pair.Value,form!.NativeId,true):null,PaidInvestmentKind.UnknownLegacy,0,
                    cargo is null?null:new(logicalSource),AmphibiousMimic:mimic&&NativeAquarionUnits.IsAquarion(Player(state,pair.Value.Unit)),
                    MimicWaterForm:mimic&&!NativeAquarionUnits.IsAquarion(Player(state,pair.Value.Unit))&&MimicWater(state,pair.Value.Unit,cargo is not null)));changed=true;
            }
            if(NativeRegroupSession.Enabled)
            {
                ulong reserved=(ulong)counter+2;Require(reserved>=ledger.NextLogicalId,"Native allocation counter regressed behind persistent logical identities.");
                if(next!=reserved){next=reserved;changed=true;}
            }
            // Normal native births/deaths are already ordered by native history.
            // Transport cadence must not invent a second command revision: a
            // replay can serialize at different points than the original run.
            long revision=changed&&!NativeRegroupSession.Enabled?checked(snapshot.Revision+1):snapshot.Revision;
            var candidate=ledger with{NextLogicalId=next,Units=units.OrderBy(row=>row.LogicalId).ToArray()};
            byte[] payload=NativeIdentityLedgerCodec.Encode(candidate,revision);
            Validate(state,revision,payload);return(revision,payload,changed);
        }
        static List<LedgerUnit> NormalizeTransportRows(Dictionary<uint,Body> graph,NativeIdentityLedger ledger,out bool changed)
        {
            changed=false;
            // Schema1/2 enrolled wrapper and passenger separately. Prefer the
            // original passenger lineage; never erase an exchange receipt.
            var sourceRows=ledger.Units.ToList();
            foreach(var root in graph.Where(p=>p.Value.Root))
            {
                if(Native.Get(root.Value.Unit,"passengerUnit") is not object cargo)continue;
                uint cargoId=Convert.ToUInt32(Native.Get(cargo,"id"));
                var passengerRow=sourceRows.SingleOrDefault(r=>r.Alive&&r.NativeId==cargoId);
                if(passengerRow is null)continue;
                var wrapperRow=sourceRows.SingleOrDefault(r=>r.Alive&&r.NativeId==root.Key);
                if(wrapperRow is not null)
                {
                    Require(!wrapperRow.Independent&&wrapperRow.Transport is null&&wrapperRow.Kind==LedgerActualKind.Genuine&&
                        !ledger.Receipts.Any(r=>r.Affected.Contains(wrapperRow.LogicalId)),"Legacy wrapper owns independent history and requires explicit migration.");
                    sourceRows.Remove(wrapperRow);
                }
                Require(!passengerRow.Independent,"Independent passenger needs an explicit transport transition.");
                sourceRows[sourceRows.IndexOf(passengerRow)]=passengerRow with{NativeId=root.Key,Transport=new(cargoId)};changed=true;
            }
            return sourceRows;
        }
        internal static NativeIdentityLedger Bootstrap(object state,Guid epoch)
        {
            var graph=Graph(state);var units=new List<LedgerUnit>();ulong next=1;
            if(NativeRegroupSession.Enabled)
            {
                uint counter=Convert.ToUInt32(Native.Get(state,"CurrentUnitId"));
                Require(counter<uint.MaxValue&&graph.Keys.All(id=>id<=counter),"Legacy native allocation counter is invalid.");
                next=(ulong)counter+2;
            }
            foreach(var pair in graph.Where(x=>x.Value.Root).OrderBy(x=>x.Key))
            {
                string type=Native.EnumName(Native.Get(pair.Value.Unit,"type")!);
                bool mimic=RecruitmentCatalog.IsMimicId(type);if(!mimic&&!NativeRegroupSession.Enabled)continue;
                RecruitmentForm? form=null;if(mimic)Require(RecruitmentHooks.Forms.TryGetValue(type,out form),"Unregistered native Mimic cannot be migrated.");
                object? cargo=Native.Get(pair.Value.Unit,"passengerUnit");
                uint logicalSource=cargo is null?pair.Key:Convert.ToUInt32(Native.Get(cargo,"id"));
                ulong logical=NativeRegroupSession.Enabled?(ulong)logicalSource+1:next++;
                units.Add(new(logical,pair.Key,true,mimic?LedgerActualKind.Mimic:LedgerActualKind.Genuine,mimic,
                    mimic?Capture(state,pair.Value,form!.NativeId,true):null,PaidInvestmentKind.UnknownLegacy,0,cargo is null?null:new(logicalSource)));
            }
            var ledger=new NativeIdentityLedger(epoch,next,1,units.OrderBy(r=>r.LogicalId).ToArray(),Array.Empty<LedgerReceipt>());
            Validate(state,0,NativeIdentityLedgerCodec.Encode(ledger,0));return ledger;
        }
        static bool PendingNativeDeath(object state,object unit)
        {
            // Native v123 damage leaves health0 on the tile and queues a
            // separate KillUnitAction. That action stores coordinates, not a
            // unit ID; prove both native ID lookup and exact tile occupancy
            // before matching its coordinates. No actor/owner equivalence is
            // assumed: KillUnitAction.Execute itself uses only its tile.
            object?[] found={Convert.ToUInt32(Native.Get(unit,"id")),null};
            if(!(bool)Native.Invoke(Native.M("GameState","TryGetUnit","System.UInt32","UnitState&"),state,found)!||!Native.Same(found[1],unit))return false;
            object at=Native.Get(unit,"coordinates")!;int x=Native.Int(Native.Get(at,"X")),y=Native.Int(Native.Get(at,"Y"));
            object? tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),at);
            if(tile is null||!Native.Same(Native.Get(tile,"unit"),unit))return false;
            object tileAt=Native.Get(tile,"coordinates")!;
            if(Native.Int(Native.Get(tileAt,"X"))!=x||Native.Int(Native.Get(tileAt,"Y"))!=y)return false;
            object stack=Native.Get(state,"ActionStack")!;int count=Native.Int(Native.Get(stack,"Count"));
            Require(count is >=0 and <=65536,"Pending native death action stack exceeds its bound.");
            object killType=Enum.Parse(Native.T("ActionType"),"KillUnit");
            var item=Native.M(stack.GetType(),"get_Item","System.Int32");
            for(int n=0;n<count;n++)
            {
                object action=Native.Invoke(item,stack,n)!;
                if(!Equals(Native.Invoke(Native.M("ActionBase","GetActionType"),action),killType))continue;
                object kill=Native.As(action,"KillUnitAction"),target=Native.Get(kill,"Coordinates")!;
                if(Native.Int(Native.Get(target,"X"))==x&&Native.Int(Native.Get(target,"Y"))==y)return true;
            }
            return false;
        }
        internal static void Validate(object state,long revision,byte[] bytes)
        {
            var ledger=NativeIdentityLedgerCodec.Decode(bytes,revision);var graph=Graph(state);var live=new HashSet<uint>();
            if(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4))<3)
                ledger=ledger with{Units=NormalizeTransportRows(graph,ledger,out _).OrderBy(r=>r.LogicalId).ToArray()};
            foreach(var row in ledger.Units)
            {
                if(!row.Alive){Require(!graph.ContainsKey(row.NativeId),"Retired identity still has a native body; reuse needs an explicit transition.");continue;}
                Require(graph.TryGetValue(row.NativeId,out var body),"Private identity has no native body.");live.Add(row.NativeId);
                Require(body!.Root,"Private identity is bound to a passenger rather than a root.");
                var unit=body.Unit;
                object? passenger=Native.Get(unit,"passengerUnit");
                Require((passenger is not null)==(row.Transport is not null),"Actual vessel/passenger binding is missing or stale.");
                if(passenger is not null)
                {
                    Require(row.Transport!.PassengerNativeId==Convert.ToUInt32(Native.Get(passenger,"id"))&&Native.Get(passenger,"passengerUnit") is null&&
                        Native.Int(Native.Get(passenger,"owner"))==Native.Int(Native.Get(unit,"owner")),"Actual cargo identity or ownership changed.");
                    live.Add(row.Transport.PassengerNativeId);
                }
                string type=Native.EnumName(Native.Get(unit,"type")!);
                bool mimic=RecruitmentHooks.Forms.TryGetValue(type,out var carrier);
                if(mimic||row.Independent)IndependentBody(body!);
                if(mimic)Require(RecruitmentCatalog.NavalCopyOnly.Contains(carrier!.NativeId,StringComparer.Ordinal)==(passenger is not null),"Mimic vessel has no actual cargo or land carrier has cargo.");
                Require(mimic==(row.Kind==LedgerActualKind.Mimic)&&!(!mimic&&RecruitmentCatalog.IsMimicId(type)),"Private actual identity disagrees with native carrier.");
                var data=Native.Get(unit,"UnitData")!;
                int health=Native.Int(Native.Get(unit,"health"));
                bool genuineTransient=NativeRegroupSession.Enabled&&!mimic&&!row.Independent&&health==0;
                bool mimicTransient=NativeRegroupSession.Enabled&&mimic&&health==0&&PendingNativeDeath(state,unit);
                Require((health>0||genuineTransient||mimicTransient)&&Native.EnumName(Native.Get(data,"type")!)==type,"Native body health or cached rule profile is inconsistent.");
                if(mimic)Require((health==10||mimicTransient)&&Native.Int(Native.Get(data,"health"))==10&&
                    Native.Int(Native.Get(unit,"promotionLevel"))==0&&Native.Int(Native.Invoke(Native.M("UnitDataExtensions","GetMaxHealth","UnitState","GameState"),null,unit,state))==10&&
                    Native.Int(Native.Get(data,"attack"))==0&&Native.Int(Native.Get(data,"defence"))==0&&Native.Int(Native.Get(data,"range"))==0,
                    "Native Mimic actual invariants changed.");
                if(row.Appearance is not LedgerAppearance a)continue;
                Require(Supported(a.Form),"Appearance profile lacks a registered Mimic carrier.");
                Require(!mimic||carrier!.NativeId==a.Form,"Mimic carrier capabilities do not match its independent appearance.");
                if(mimic&&passenger is not null)
                {
                    string cargoType=Native.EnumName(Native.Get(passenger,"type")!);
                    Require(a.Passenger is not null&&RecruitmentHooks.Forms.TryGetValue(cargoType,out var cargoCarrier)&&cargoCarrier.NativeId==a.Passenger.Form&&
                        Native.Int(Native.Get(passenger,"health"))==10&&Native.Int(Native.Get(passenger,"promotionLevel"))==0,
                        "Actual Mimic cargo lost its identity or appearance capabilities.");
                }
                ValidateAppearance(state,a);
            }
            foreach(var pair in graph)
                if(RecruitmentCatalog.IsMimicId(Native.EnumName(Native.Get(pair.Value.Unit,"type")!)))
                    Require(live.Contains(pair.Key),"Native Mimic is missing its private identity row.");
        }
        static void ValidateAppearance(object state,LedgerAppearance a)
        {
                Require(Supported(a.Form),"Appearance profile lacks a registered carrier.");
                Require(RecruitmentCatalog.NavalCopyOnly.Contains(a.Form,StringComparer.Ordinal)==(a.Passenger is not null),"Apparent vessel must retain its independent passenger.");
                NativeApparentGrowth.Validate(a);
                if(NativeApparentGrowth.IsGrowing(a.Form))Require(Native.Int(Native.Get(RecruitmentHooks.Data(state,a.Form),"growthRate"))==NativeApparentGrowth.Duration(a.Form),"Native apparent growth rate changed.");
                foreach(string effect in a.Effects)_=EnumValue("UnitEffect",effect);
                Require((a.Concealment==ApparentConcealment.Hidden)==a.Effects.Contains("Invisible",StringComparer.Ordinal),"Apparent concealment and Invisible status disagree.");
                object data=RecruitmentHooks.Data(state,a.Form);
                Require((a.Passenger is not null&&!ProtectsPassenger(data)?a.Passenger.Maximum:Native.Int(Native.Get(data,"health"))+(a.Veteran?50:0))==a.Maximum,"Apparent maximum differs from its native veteran profile.");
                foreach(var visual in new[]{a.TribeVisual,a.ClimateVisual,a.BirthVisual}){_=EnumValue(Tribe,visual.Tribe);_=EnumValue(Skin,visual.Skin);}
                // Native ColorFromInt decodes the low24 RGB bits and ignores
                // the high byte. Every Int32 bit pattern is representable.
                if(a.Passenger is LedgerAppearance cargo)ValidateAppearance(state,cargo);
        }
        internal static bool ValidateTransportPayload(byte[] bytes)
        {
            if(!NativeFreshSession.Enabled&&!NativeRegroupSession.Enabled&&NativeEnvelopeProbe.ValidatePayload(bytes))return true; // Detached diagnostic only.
            if(bytes.Length<4||BinaryPrimitives.ReadUInt32LittleEndian(bytes)!=NativeIdentityLedgerCodec.Magic)return false;
            try{_=NativeIdentityLedgerCodec.Decode(bytes,long.MaxValue);return true;}catch(InvalidDataException){return false;}
        }
        internal static void ValidateTransportState(object state,long revision,byte[] bytes)
        {if(NativeFreshSession.Enabled||NativeRegroupSession.Enabled||!NativeEnvelopeProbe.ValidatePayload(bytes))Validate(state,revision,bytes);}
    }
}
