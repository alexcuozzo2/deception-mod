using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Deception.Core;

namespace Deception.PolyModAdapter
{
    // Read-only first gameplay slice. No hooks, GameManager/Client reads,
    // native setters, pathfinding, native action execution or publication.
    // Normal Local play uses registered independent forms and conversion-only
    // coast outcomes. Compound forms and Rogue authority remain separate.
    internal static class NativeExchangePlanning
    {
        static bool Supported(string form)=>NativeRegroupSession.Enabled?RecruitmentHooks.Forms.Values.Any(f=>f.NativeId==form):form is "warrior" or "rider";
        const string Improvement="Polytopia.Data.ImprovementData+Type";
        static void Require(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
        static NativeExchangePosition Position(object value)=>new(Native.Int(Native.Get(value,"X")),Native.Int(Native.Get(value,"Y")));
        static object Player(object state,byte actor)
        {
            object?[] args={actor,null};
            Require((bool)Native.Invoke(Native.M("GameState","TryGetPlayer","System.Byte","PlayerState&"),state,args)!&&args[1] is not null,
                "The commanding player is missing from the supplied state.");return args[1]!;
        }
        static void Research(object player,NativeExchangeKind kind)
        {
            string id=kind==NativeExchangeKind.Regroup?TechTree.DeceptionId:TechTree.PropagandaId;object tech;
            try{tech=Native.EnumValue(RecruitmentHooks.TT,id);}
            catch(Exception){throw new InvalidDataException("The required exchange technology is not registered in this build.");}
            Require((bool)Native.Invoke(Native.M("PlayerExtensions","HasTech","PlayerState",RecruitmentHooks.TT),null,player,tech)!,
                kind==NativeExchangeKind.Regroup?"Research Deception before using Regroup.":"Research Propaganda before using Disguise.");
        }
        static LedgerUnit Existing(NativeIdentityLedger ledger,NativeExchangeUnit reference,string role)
        {
            var row=ledger.Units.SingleOrDefault(u=>u.LogicalId==reference.LogicalId);
            Require(row is not null&&row.Alive&&row.NativeId==reference.NativeId,"The "+role+" logical identity is absent, retired or rebound.");return row!;
        }
        static (NativeLedgerState.Body Body,object Tile,string Form) Participant(object state,Dictionary<uint,NativeLedgerState.Body> graph,
            NativeExchangeUnit reference,byte actor,LedgerActualKind kind,string role)
        {
            Require(graph.TryGetValue(reference.NativeId,out var body)&&body.Root,"The "+role+" is not an independent map body.");
            object unit=body!.Unit;
            Require(Native.Int(Native.Get(unit,"owner"))==actor,"Only owned or currently commanded partners are eligible.");
            Require(Native.Int(Native.Get(unit,"health"))>0&&
                !(bool)Native.Invoke(Native.M("UnitState","HasLeader"),unit)!&&!(bool)Native.Invoke(Native.M("UnitState","HasFollower"),unit)!,
                "The "+role+" is dead or part of a linked body.");
            object coordinates=Native.Get(unit,"coordinates")!;
            Require(Position(coordinates)==new NativeExchangePosition(reference.X,reference.Y),"The "+role+" moved after the exchange choice.");
            var tile=Native.Invoke(Native.M("MapDataExtensions","GetTile","MapData","WorldCoordinates"),null,Native.Get(state,"Map"),coordinates);
            Require(tile is not null&&Position(Native.Get(tile,"coordinates")!)==Position(coordinates)&&Native.Same(Native.Get(tile,"unit"),unit),
                "The "+role+" no longer occupies its exact native tile.");
            Require((bool)Native.Invoke(Native.M("TileData","GetExplored","System.Byte"),tile,actor)!,"Both exchange tiles must be accessible to the commander.");
            string type=Native.EnumName(Native.Get(unit,"type")!);bool mimic=RecruitmentHooks.Forms.TryGetValue(type,out var carrier);
            // A01 excludes a Segment even when its leader disappeared before
            // native promotion. Only an actual standalone Centipede head may
            // participate; absence of links alone is not proof of a head.
            Require(type!="segment","A body segment cannot Regroup before native promotion to a standalone head.");
            Require(mimic==(kind==LedgerActualKind.Mimic),"The "+role+" native body disagrees with its actual identity.");
            string form=mimic?carrier!.NativeId:type;
            Require(Supported(form),"This unit needs its dedicated native form adapter before it can Regroup.");
            Require(Native.EnumName(Native.Get(Native.Get(unit,"UnitData")!,"type")!)==type,"The "+role+" cached native rule profile is inconsistent.");
            return(body,tile!,form);
        }
        static LedgerAppearance Appearance(object state,NativeLedgerState.Body body,LedgerUnit row,string actualForm,string role)
        {
            if(row.Appearance is LedgerAppearance appearance)return row.Kind==LedgerActualKind.Genuine?
                NativeDisguiseConcealment.EffectiveAppearance(appearance,body.Unit):appearance;
            Require(row.Kind==LedgerActualKind.Genuine&&!row.Independent,"The "+role+" independent appearance is missing.");
            try{return NativeLedgerState.CapturePublic(state,body,actualForm,row);}
            catch(InvalidDataException e){throw new InvalidDataException("The "+role+" native appearance needs an unsupported status, veteran or form snapshot: "+e.Message,e);}
        }
        static string MimicCarrier(object state,string form)
        {
            Require(Supported(form),"The resulting Mimic appearance needs a later native form adapter.");
            var entries=RecruitmentHooks.Forms.Values.Where(f=>f.NativeId==form).ToArray();
            Require(entries.Length==1,"The resulting Mimic carrier is not registered unambiguously.");
            string id=entries[0].VariantId;var data=RecruitmentHooks.Data(state,id);
            Require(Native.EnumName(Native.Get(data,"type")!)==id&&Native.Int(Native.Get(data,"health"))==10&&
                Native.Int(Native.Get(data,"attack"))==0&&Native.Int(Native.Get(data,"defence"))==0&&Native.Int(Native.Get(data,"range"))==0,
                "The resulting native Mimic carrier violates its actual one-HP/no-offense contract.");return id;
        }
        static void Endpoint(object state,object unit,object destination,object player,string? resultingCarrier=null)
        {
            // Physical placement differs from walking: no movement research or
            // path is borrowed, and subsequent movement remains native.
            if(!NativeRegroupSession.Enabled)Require((bool)Native.Get(destination,"IsLand")!,
                "This diagnostic exchange slice supports land only.");
            // Query the resulting actual movement profile on a detached scalar
            // clone. No mutable collection is changed and no body is inserted.
            object profile=unit;
            if(resultingCarrier is not null)
            {
                profile=Native.DetachedClone(unit,"UnitState");
                Native.Set(profile,"type",Native.EnumValue(RecruitmentHooks.UT,resultingCarrier));
                Native.Set(profile,"UnitData",RecruitmentHooks.Data(state,resultingCarrier));
            }
            Require(NativeMimicTransport.CanOccupyExchange(Native.Get(profile,"UnitData")!,destination),
                "The resulting body cannot be represented on this terrain.");
            bool city=(bool)Native.Invoke(Native.M("TileData","HasImprovement",Improvement),destination,Native.EnumValue(Improvement,"city"))!;
            if(!city)return;
            byte owner=Convert.ToByte(Native.Get(destination,"owner"));if(owner==0)return; // Literal native city-finish exception.
            string finalType=Native.EnumName(Native.Get(profile,"type")!);
            string operative=RecruitmentHooks.Forms.TryGetValue(finalType,out var carrier)?carrier.NativeId:finalType;
            Require(owner==Convert.ToByte(Native.Get(player,"Id"))||operative is not ("cloak" or "mermaidcloak" or "cloak_boat"),
                "A Cloak's ordinary movement cannot finish on an opponent's city.");
            // GetRelation lazily inserts a default relation. Read the existing
            // dictionary instead; its native missing-key default is Neutral0.
            var relations=Native.Get(player,"relations");Require(relations is not null,"The native diplomacy table is unavailable.");
            object?[] args={owner,null};bool found=(bool)Native.Invoke(Native.M(relations!.GetType(),"TryGetValue","System.Byte","DiplomacyRelation&"),relations,args)!;
            if(!found)return;
            Require(args[1] is not null,"The native diplomacy relation is malformed.");
            int relation=Native.Int(Native.Get(args[1]!,"State"));Require(relation is >=0 and <=3,"The native diplomacy relation is unsupported.");
            Require(relation!=1&&relation!=3,"A peace or cooldown relation prevents ending on this city.");
        }

        internal sealed record Outcome(LedgerUnit Row,string Type,uint Wrapper,object Profile);
        internal static Outcome FinalOutcome(object state,object unit,object destination,LedgerUnit row,ref uint counter)
        {
            var appearance=row.Appearance;
            if(NativeRegroupSession.Enabled&&appearance is not null)appearance=NativeMimicTransport.InterpretAppearance(state,appearance,destination);
            bool vessel=NativeRegroupSession.Enabled&&NativeMimicTransport.IsVessel(unit);
            string type=Native.EnumName(Native.Get(unit,"type")!);object profile=unit;
            if(row.Kind==LedgerActualKind.Mimic)type=MimicCarrier(state,appearance!.Form);
            else if(NativeRegroupSession.Enabled)
            {
                bool water=NativeMimicTransport.WaterEndpoint(destination,vessel);
                if(vessel&&!water)
                {profile=Native.Get(unit,"passengerUnit")??throw new InvalidDataException("Genuine vessel has no actual cargo to land.");type=Native.EnumName(Native.Get(profile,"type")!);}
                else if(!vessel&&water&&!NativeMimicTransport.NaturallyAquatic(Native.Get(unit,"UnitData")!))
                {
                    Require(NativeMimicTransport.HasNativeEmbarkFamily(state,Player(state,Convert.ToByte(Native.Get(unit,"owner")))),
                        "This tribe has no native passenger-vessel form for the genuine unit.");
                    type=NativeMimicTransport.StandardVessel(type);
                }
            }
            object data=RecruitmentHooks.Data(state,type);bool finalVessel=NativeRegroupSession.Enabled&&NativeMimicTransport.IsVesselData(data);
            uint id=row.NativeId,wrapper=0;LedgerTransport? transport=row.Transport;
            if(vessel!=finalVessel)
            {
                if(finalVessel)
                {
                    Require(transport is null&&Native.Get(unit,"passengerUnit") is null,"Embark exchange source already carries cargo.");
                    Require(counter<uint.MaxValue-2,"Coast exchange native allocator is exhausted.");wrapper=++counter;id=wrapper;transport=new(row.NativeId);
                }
                else
                {
                    profile=Native.Get(unit,"passengerUnit")??throw new InvalidDataException("Landing exchange has no actual cargo.");
                    id=Convert.ToUInt32(Native.Get(profile,"id"));Require(transport?.PassengerNativeId==id,"Landing exchange cargo lineage is missing.");transport=null;
                }
            }
            if(row.Kind==LedgerActualKind.Genuine&&appearance is not null)
                appearance=NativeDisguiseConcealment.EffectiveAppearance(appearance,profile);
            return new(row with{NativeId=id,Transport=transport,Appearance=appearance},type,wrapper,profile);
        }

        internal static bool TryPlan(object suppliedState,NativeExchangeIntent intent,out NativeExchangePlan? plan,out string reason)
        {
            if(intent.Kind==NativeExchangeKind.LighthouseSecret)return NativeLighthouseSecret.TryPlan(suppliedState,intent,out plan,out reason);
            if(intent.Kind==NativeExchangeKind.Blackmail)return NativeBlackmail.TryPlan(suppliedState,intent,out plan,out reason);
            if(intent.Kind==NativeExchangeKind.Subvert)return NativeSubvert.TryPlan(suppliedState,intent,out plan,out reason);
            if(intent.Kind==NativeExchangeKind.Blackout)return NativeBlackout.TryPlan(suppliedState,intent,out plan,out reason);
            if(NativeExchangeCatalog.IsSupport(intent.Kind))return NativeFeignSupport.TryPlan(suppliedState,intent,out plan,out reason);
            if(intent.Kind==NativeExchangeKind.ShedDisguise)return NativeShedDisguise.TryPlan(suppliedState,intent,out plan,out reason);
            if(NativeExchangeCatalog.IsUpgrade(intent.Kind))return NativeApparentUpgrades.TryPlan(suppliedState,intent,out plan,out reason);
            plan=null;reason="";
            try
            {
                Require(Native.T("GameState").IsInstanceOfType(suppliedState),"An exact supplied native GameState is required.");
                // Encode/decode owns the mutable appearance digest even if the
                // caller later changes its original request object.
                var request=NativeExchangeIntentCodec.Decode(NativeExchangeIntentCodec.Encode(intent));
                var snapshot=NativeGameStateEnvelope.Get(suppliedState);
                Require(snapshot is not null,"This state has no established persistent identity ledger.");
                byte[] original=snapshot!.Bytes();long revision=snapshot.Revision;
                var ledger=NativeIdentityLedgerCodec.Decode(original,revision);
                if(NativeExchangeIntentCodec.AlreadyCommitted(ledger,revision,request))
                {
                    // A duplicate may arrive after movement, action spending,
                    // later turns, retirement or further appearance changes.
                    // Its exact receipt is the no-op proof; no bodies are read.
                    plan=new(suppliedState,request,true,revision,revision,original,original);return true;
                }
                NativeLedgerState.Validate(suppliedState,revision,original);
                Require(RecruitmentHooks.Registered,"Native Mimic carriers are not registered.");
                Require(Equals(Native.Get(suppliedState,"CurrentState"),Enum.Parse(Native.T("GameState+State"),"Started"))&&
                    Convert.ToUInt32(Native.Get(suppliedState,"CurrentTurn"))==request.Turn&&Native.Int(Native.Get(suppliedState,"CurrentPlayer"))==request.Actor,
                    "The exchange turn or commanding player changed.");
                object player=Player(suppliedState,request.Actor);Research(player,request.Kind);
                var aRow=Existing(ledger,request.Initiator,"initiator");
                Require(aRow.Kind==LedgerActualKind.Mimic&&aRow.Independent&&aRow.Appearance is not null,"Only an existing Mimic may initiate an exchange.");
                bool allocate=request.PartnerReference==NativePartnerReference.Allocate;LedgerUnit bRow;
                if(allocate)
                {
                    Require(!ledger.Units.Any(u=>u.Alive&&u.NativeId==request.Partner.NativeId)&&ledger.Units.Length<NativeIdentityLedgerCodec.MaxUnits,
                        "The partner is already tracked or the identity ledger is full.");
                    bRow=new(request.Partner.LogicalId,request.Partner.NativeId,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0);
                }
                else bRow=Existing(ledger,request.Partner,"partner");
                var graph=NativeLedgerState.Graph(suppliedState);
                var a=Participant(suppliedState,graph,request.Initiator,request.Actor,aRow.Kind,"initiator");
                var b=Participant(suppliedState,graph,request.Partner,request.Actor,bRow.Kind,"partner");
                Require(!Native.Same(a.Body.Unit,b.Body.Unit)&&!Native.Same(a.Tile,b.Tile),"Exchange participants must be different bodies on different tiles.");
                Require((bool)Native.Invoke(Native.M("WorldCoordinates","IsAdjacent","WorldCoordinates","WorldCoordinates"),null,
                    Native.Get(a.Body.Unit,"coordinates"),Native.Get(b.Body.Unit,"coordinates"))!,"The partner must be on an adjacent tile, including diagonals.");
                // Mimic Dash permits an exchange after movement, but never after
                // spending its action (including recruitment exhaustion).
                Require(!(bool)Native.Get(a.Body.Unit,"attacked")!,"The initiating Mimic already spent its action.");
                Require(!(bool)Native.Invoke(Native.M("UnitState","HasEffect","UnitEffect"),a.Body.Unit,Enum.Parse(Native.T("UnitEffect"),"Frozen"))!,
                    "A frozen Mimic cannot initiate an exchange.");
                // Never gate the partner on moved/attacked/frozen. In particular
                // a genuine Disguise recipient needs no capture of its old
                // native appearance, so an actual frozen status stays allowed.
                LedgerAppearance source=request.Kind==NativeExchangeKind.Regroup?
                    Appearance(suppliedState,b.Body,bRow,b.Form,"source partner"):aRow.Appearance!;
                Require(NativeExchangeIntentCodec.AppearanceFingerprint(source).SequenceEqual(request.SourceAppearanceHash),"The source appearance changed after the exchange choice.");
                if(request.Kind==NativeExchangeKind.Regroup)aRow=aRow with{Independent=true,Appearance=source};
                else bRow=bRow with{Independent=true,Appearance=source};
                Require(aRow.Appearance is not null&&Supported(aRow.Appearance.Form)&&
                    (bRow.Appearance is null||Supported(bRow.Appearance.Form)),"The resulting appearance requires a later native form adapter.");
                object destinationA=request.Swap?b.Tile:a.Tile,destinationB=request.Swap?a.Tile:b.Tile;
                uint counter=Convert.ToUInt32(Native.Get(suppliedState,"CurrentUnitId"));
                var finalA=FinalOutcome(suppliedState,a.Body.Unit,destinationA,aRow,ref counter);
                var finalB=FinalOutcome(suppliedState,b.Body.Unit,destinationB,bRow,ref counter);
                aRow=finalA.Row;bRow=finalB.Row;
                string carrierA=finalA.Type;
                string? carrierB=bRow.Kind==LedgerActualKind.Mimic?finalB.Type:null;
                Endpoint(suppliedState,finalA.Profile,destinationA,player,finalA.Type);Endpoint(suppliedState,finalB.Profile,destinationB,player,finalB.Type);
                var rows=ledger.Units.Select(row=>row.LogicalId==aRow.LogicalId?aRow:row.LogicalId==bRow.LogicalId?bRow:row).ToList();
                if(allocate)rows.Add(bRow);
                long nextRevision=checked(revision+1);
                var receipt=new LedgerReceipt(request.Sequence,NativeExchangeIntentCodec.Operation(request.Sequence),NativeExchangeIntentCodec.Fingerprint(request),
                    revision,nextRevision,new[]{aRow.LogicalId,bRow.LogicalId}.OrderBy(x=>x).ToArray());
                ulong nextLogical=allocate?checked(ledger.NextLogicalId+1):ledger.NextLogicalId;
                if(NativeRegroupSession.Enabled)nextLogical=Math.Max(nextLogical,(ulong)counter+2);
                var candidate=ledger with{NextLogicalId=nextLogical,NextSequence=checked(ledger.NextSequence+1),
                    Units=rows.OrderBy(row=>row.LogicalId).ToArray(),Receipts=ledger.Receipts.Concat(new[]{receipt}).ToArray()};
                byte[] encoded=NativeIdentityLedgerCodec.Encode(candidate,nextRevision);
                // The candidate is value-validated here. Its changed Mimic
                // carrier cannot be checked against the still-unchanged native
                // bodies until the later atomic commit adapter applies it.
                plan=new(suppliedState,request,false,revision,nextRevision,original,encoded,a.Body.Unit,b.Body.Unit,a.Tile,b.Tile,
                    Position(Native.Get(destinationA,"coordinates")!),Position(Native.Get(destinationB,"coordinates")!),carrierA,carrierB,
                    finalA.Type,finalB.Type,aRow.NativeId,bRow.NativeId,finalA.Wrapper,finalB.Wrapper,counter);return true;
            }
            catch(Exception e){plan=null;reason=e.Message;return false;}
        }
    }
}
