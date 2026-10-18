using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Deception.Core;
using Deception.PolyModAdapter;
using Tile=Deception.PolyModAdapter.Tile;

namespace Deception.Tests
{
    public static class LedgerLifecycleTests
    {
        static int Tests,Assertions;
        static readonly List<string> Results=new();
        static void Check(bool value,string reason){Assertions++;if(!value)throw new Exception(reason);}
        static void Reject(Action action,string reason){try{action();throw new Exception("Expected rejection: "+reason);}catch(InvalidDataException){Assertions++;}}
        static void Test(string name,Action action){action();Tests++;Results.Add(name);}
        static NativeGameStateEnvelope.Snapshot Snapshot(NativeIdentityLedger ledger,long revision=0)=>new(revision,NativeIdentityLedgerCodec.Encode(ledger,revision));
        static NativeIdentityLedger Decode((long Revision,byte[] Payload,bool Changed) value)=>NativeIdentityLedgerCodec.Decode(value.Payload,value.Revision);
        static State State(params Unit[] units)=>new(){CurrentUnitId=units.Select(x=>x.id).DefaultIfEmpty().Max(),Map=new(){Tiles=units.Select(x=>new Tile(){unit=x}).ToArray()}};
        static Unit Unit(uint id,string form,bool mimic=true)=>new(){id=id,type=mimic?RecruitmentCatalog.Variant(form):form,health=mimic?10:100,UnitData=RecruitmentHooks.Data(null!,mimic?RecruitmentCatalog.Variant(form):form)};
        static Guid Epoch=Guid.Parse("4eb1495b-bb7b-42f6-93b8-9743315e2380");
        public static int Run()
        {
            RecruitmentHooks.Initialize();
            try
            {
                Test("genuine-rogue-copy-preserves-original-public-colors-and-cargo",()=>{
                    foreach(bool cargo in new[]{false,true})foreach(bool rogue in new[]{false,true})
                    {
                        string form=cargo?"transportship":"warrior";var unit=Unit(61,form,false);var state=State(unit);
                        if(cargo)unit.passengerUnit=Unit(60,"warrior",false);
                        state.Player.color=111;state.OtherPlayer=new(){Id=2,tribe=Tribe.Bardur,color=222};
                        var expected=NativeLedgerState.Capture(state,new(unit,true),form,false);
                        if(rogue){unit.owner=2;if(cargo)unit.passengerUnit!.owner=2;}
                        var row=new LedgerUnit(62,61,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,2,cargo?new(60):null,Rogue:rogue?new(1,2,4):null);
                        string before=JsonSerializer.Serialize(unit);var appearance=NativeLedgerState.CapturePublic(state,new(unit,true),form,row);
                        Check(NativeExchangeIntentCodec.AppearanceFingerprint(appearance).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(expected)),"copied public appearance differs from original owner's visuals");
                        Check(appearance.SourceColor==111&&appearance.TribeVisual.Tribe=="Imperius"&&(!cargo||appearance.Passenger!.SourceColor==111),"commander colors leaked into body/cargo snapshot");
                        Check(JsonSerializer.Serialize(unit)==before&&row.Appearance is null,"public capture mutated genuine body or ledger");
                        var current=NativeLedgerState.Capture(state,new(unit,true),form,false);
                        Check(current.SourceColor==(rogue?222:111),"unscoped capture was globally recolored");
                        if(rogue){Reject(()=>NativeLedgerState.CapturePublic(state,new(unit,true),form,row with{NativeId=999}),"wrong body");Check(ReferenceEquals(NativeLedgerState.CapturePublic(state,new(unit,true),form,row with{Appearance=expected,Independent=true}),expected),"existing independent public snapshot was regenerated");}
                    }
                });
                Test("actual-hidden-cloak-public-copy-normalizes-existing-mask-read-only",()=>{
                    foreach(string actual in new[]{"cloak","mermaidcloak","cloak_boat"})
                    {
                        var unit=Unit(61,actual,false);var state=State(unit);
                        var apparent=NativeLedgerState.Capture(state,new(Unit(63,"rider",false),true),"rider",false);
                        var row=new LedgerUnit(62,61,true,LedgerActualKind.Genuine,true,apparent,PaidInvestmentKind.Known,8);
                        unit.effects.Add(UnitEffect.Invisible);var effects=unit.effects;string before=JsonSerializer.Serialize(unit);
                        var next=NativeLedgerState.CapturePublic(state,new(unit,true),actual,row);
                        Check(next.Concealment==ApparentConcealment.Hidden&&next.Effects.SequenceEqual(new[]{"Invisible"}),"real hidden Cloak copied a visible mask");
                        Check(next.Form=="rider"&&next.SourceColor==apparent.SourceColor&&next.Health==apparent.Health,"effective copy replaced independent public values");
                        Check(row.Appearance==apparent&&apparent.Concealment==ApparentConcealment.Visible&&apparent.Effects.Length==0,"read-only capture mutated old saved mask");
                        Check(JsonSerializer.Serialize(unit)==before&&ReferenceEquals(effects,unit.effects),"read-only capture touched real effects/body");
                        Reject(()=>NativeLedgerState.CapturePublic(state,new(unit,true),actual,row with{NativeId=999}),"mismatched independent id");
                        Reject(()=>NativeLedgerState.CapturePublic(state,new(unit,true),actual,row with{Alive=false}),"dead independent row");
                        Reject(()=>NativeLedgerState.CapturePublic(state,new(unit,true),actual,row with{Independent=false}),"non-independent appearance");
                    }
                });
                Test("actual-hidden-cloak-reconciliation-migrates-mask-at-native-boundary",()=>{
                    foreach(string actual in new[]{"cloak","mermaidcloak"})
                    {
                        var unit=Unit(61,actual,false);var state=State(unit);var ledger=NativeLedgerState.Bootstrap(state,Epoch);
                        var apparent=NativeLedgerState.Capture(state,new(unit,true),"rider",false);
                        ledger=ledger with{Units=new[]{ledger.Units.Single() with{Independent=true,Appearance=apparent}}};
                        var snapshot=Snapshot(ledger,7);byte[] original=snapshot.Bytes();unit.effects.Add(UnitEffect.Invisible);
                        string before=JsonSerializer.Serialize(unit);var effects=unit.effects;
                        var result=NativeLedgerState.Reconcile(state,snapshot);var normalized=Decode(result);var next=normalized.Units.Single().Appearance!;
                        Check(result.Changed&&result.Revision==7,"authorized reconciliation did not normalize in the existing native revision");
                        Check(next.Form=="rider"&&next.Concealment==ApparentConcealment.Hidden&&next.Effects.SequenceEqual(new[]{"Invisible"}),"reconciled cloak mask lost effective concealment");
                        Check(snapshot.Bytes().SequenceEqual(original)&&apparent.Concealment==ApparentConcealment.Visible,"immutable source snapshot changed");
                        Check(JsonSerializer.Serialize(unit)==before&&ReferenceEquals(unit.effects,effects),"boundary normalization mutated native readiness or body");
                        NativeLedgerState.Validate(state,result.Revision,result.Payload);
                        var again=NativeLedgerState.Reconcile(state,Snapshot(normalized,result.Revision));
                        Check(!again.Changed&&again.Payload.SequenceEqual(result.Payload),"reload normalization is not idempotent");
                    }
                });
                Test("visible-cloak-unrelated-real-body-and-mimic-keep-independent-mask",()=>{
                    foreach(var spec in new[]{(Form:"cloak",Mimic:false,Hidden:false),(Form:"warrior",Mimic:false,Hidden:true),(Form:"rider",Mimic:true,Hidden:true)})
                    {
                        var unit=Unit(61,spec.Form,spec.Mimic);var state=State(unit);var ledger=NativeLedgerState.Bootstrap(state,Epoch);
                        var a=spec.Mimic?ledger.Units.Single().Appearance!:NativeLedgerState.Capture(state,new(unit,true),"rider",false);
                        var row=ledger.Units.Single() with{Independent=true,Appearance=a};ledger=ledger with{Units=new[]{row}};
                        if(spec.Hidden)unit.effects.Add(UnitEffect.Invisible);
                        var source=Snapshot(ledger,7);string before=JsonSerializer.Serialize(unit);
                        Check(ReferenceEquals(NativeLedgerState.CapturePublic(state,new(unit,true),spec.Form,row),a),"unrelated public snapshot was normalized");
                        var result=NativeLedgerState.Reconcile(state,source);
                        Check(!result.Changed&&result.Revision==7&&result.Payload.SequenceEqual(source.Bytes()),"unrelated ledger changed at reconcile");
                        Check(JsonSerializer.Serialize(unit)==before&&Decode(result).Units.Single().Appearance!.Concealment==ApparentConcealment.Visible,"native or public state changed");
                    }
                });
                Test("exposed-mimic-restores-true-cosmetics-through-rogue-return-and-copy",()=>{
                    foreach(bool rogue in new[]{false,true})foreach(bool cargo in new[]{false,true})foreach(bool aquatic in new[]{false,true})
                    {
                        var unit=Unit(71,cargo?"transportship":"warrior");var state=State(unit);
                        if(cargo)unit.passengerUnit=Unit(70,"warrior");
                        state.Player.color=111;state.OtherPlayer=new(){Id=2,tribe=Tribe.Bardur,color=222};
                        var old=new LedgerAppearance("warrior",new("Bardur","Default"),new("Bardur","Default"),new("Bardur","Default"),999,100,100,true,ApparentConcealment.Hidden,3,new[]{"Invisible"},9);
                        if(rogue){unit.owner=2;if(cargo)unit.passengerUnit!.owner=2;}
                        var row=new LedgerUnit(72,71,true,LedgerActualKind.Mimic,true,old,PaidInvestmentKind.Known,3,cargo?new(70):null,
                            Rogue:rogue?new(1,2,4):null,AmphibiousMimic:aquatic);
                        string before=JsonSerializer.Serialize(unit);var exposed=NativeLedgerState.ExposedAppearance(state,unit,row);
                        Check(exposed.SourceColor==111&&exposed.TribeVisual.Tribe=="Imperius","exposed body retained mask or commander's cosmetics");
                        Check(exposed.Form==(aquatic?AquarionCatalog.MimicId:RecruitmentCatalog.MenuId)&&exposed.Health==10&&exposed.Maximum==10,"exposed species/health differs");
                        Check(exposed.Passenger is null&&!exposed.Veteran&&exposed.GrowthTurns==-1&&exposed.GrowthDueTurn==-1&&exposed.Effects.Length==0&&exposed.Concealment==ApparentConcealment.Visible,"old mask status leaked into exposed body");
                        Check(JsonSerializer.Serialize(unit)==before&&row.Appearance!.SourceColor==999,"capture mutated true body or original mask");
                        unit.owner=1;if(cargo)unit.passengerUnit!.owner=1;
                        var returned=row with{Appearance=exposed,Rogue=null};var recaptured=NativeLedgerState.ExposedAppearance(state,unit,returned);
                        Check(NativeExchangeIntentCodec.AppearanceFingerprint(exposed).SequenceEqual(NativeExchangeIntentCodec.AppearanceFingerprint(recaptured)),"Rogue return changed exposed cosmetics");
                        var transferred=returned with{LogicalId=90,NativeId=89,Kind=LedgerActualKind.Genuine,AmphibiousMimic=false,MimicWaterForm=false};
                        var ledger=new NativeIdentityLedger(Epoch,91,1,new[]{transferred},Array.Empty<LedgerReceipt>());
                        var copy=NativeIdentityLedgerCodec.Decode(NativeIdentityLedgerCodec.Encode(ledger,0),0).Units.Single().Appearance!;
                        Check(copy.SourceColor==111&&copy.TribeVisual==exposed.TribeVisual,"transferred public Mimic lost original cosmetics across serialization");
                    }
                });
                Test("native-city-registry-name-is-distinct-from-visual-enum-name",()=>{
                    Check(Enum.GetNames<ImprovementType>().SequenceEqual(new[]{"None","City"})&&(int)ImprovementType.City==1,"fixture must use installed capitalized City1");
                    Check((ImprovementType)Native.EnumValue("Polytopia.Data.ImprovementData+Type","city")==ImprovementType.City,"registry identifier did not resolve to native City");
                    Check(!Enum.GetNames<ImprovementType>().Contains("city",StringComparer.Ordinal),"lowercase CLR alias hides the production mismatch");
                });
                Test("all-48-nongrowing-nonvessel-carrier-forms-roundtrip",()=>{
                    uint id=1;var units=RecruitmentCatalog.CarrierForms.Where(f=>!NativeApparentGrowth.IsGrowing(f)&&!RecruitmentCatalog.NavalCopyOnly.Contains(f)).Select(form=>Unit(id++,form)).ToArray();var state=State(units);
                    var ledger=NativeLedgerState.Bootstrap(state,Epoch);Check(ledger.Units.Length==48,"carrier census");
                    byte[] bytes=NativeIdentityLedgerCodec.Encode(ledger,0);NativeLedgerState.Validate(state,0,bytes);
                    var decoded=NativeIdentityLedgerCodec.Decode(bytes,0);
                    Check(decoded.Units.Select(x=>x.Appearance!.Form).ToHashSet().SetEquals(RecruitmentCatalog.CarrierForms.Where(f=>!NativeApparentGrowth.IsGrowing(f)&&!RecruitmentCatalog.NavalCopyOnly.Contains(f))),"forms survive wire");
                    Check(decoded.Units.All(x=>x.InvestmentKind==PaidInvestmentKind.UnknownLegacy&&x.PaidStars==0),"bootstrap does not infer payment");
                });
                Test("genuine-spy-and-spy-mimic-retain-independent-identity",()=>{
                    var real=Unit(30,SpyCatalog.Id,false);var mimic=Unit(31,SpyCatalog.Id);var state=State(real,mimic);
                    var ledger=NativeLedgerState.Bootstrap(state,Epoch);byte[] bytes=NativeIdentityLedgerCodec.Encode(ledger,0);
                    NativeLedgerState.Validate(state,0,bytes);var decoded=NativeIdentityLedgerCodec.Decode(bytes,0);
                    var genuine=decoded.Units.Single(x=>x.NativeId==30);var copy=decoded.Units.Single(x=>x.NativeId==31);
                    Check(genuine.Kind==LedgerActualKind.Genuine&&genuine.Appearance is null&&!genuine.Independent,"Genuine Spy was bootstrapped as a Mimic disguise");
                    Check(copy.Kind==LedgerActualKind.Mimic&&copy.Independent&&copy.Appearance!.Form==SpyCatalog.Id,"Copied Spy lost actual Mimic identity");
                    Check(real.health==100&&mimic.health==10&&real.type==SpyCatalog.Id&&mimic.type==RecruitmentCatalog.Variant(SpyCatalog.Id),"Ledger bootstrap changed genuine/copy HP or type");
                    Check(genuine.LogicalId!=copy.LogicalId&&decoded.NextLogicalId>copy.LogicalId,"Spy/copy identities were aliased");
                    Check(decoded.Units.All(x=>x.InvestmentKind==PaidInvestmentKind.UnknownLegacy),"Native bootstrap invented Spy's paid investment");
                    var before=JsonSerializer.Serialize(state);var read=NativeLedgerState.Reconcile(state,Snapshot(decoded));
                    Check(!read.Changed&&read.Payload.SequenceEqual(bytes)&&JsonSerializer.Serialize(state)==before,"No-op Spy reconciliation rewrote data");
                });
                Test("native-growing-donor-capture-keeps-timer-wounds-and-source",()=>{
                    foreach(string form in new[]{"dragonegg","babydragon"}){
                        var donor=Unit(40,form,false);donor.createdTurn=7;donor.health=60;donor.effects=new(){UnitEffect.Frozen};var state=State(donor);state.CurrentTurn=9;
                        var before=JsonSerializer.Serialize(donor);var a=NativeLedgerState.Capture(state,new(donor,true),form,false);
                        Check(a.GrowthDueTurn==10&&a.GrowthTurns==1&&a.Health==60,"copy remaining age, no fresh phase");
                        Check(a.Maximum==(form=="dragonegg"?100:150)&&a.Effects.SequenceEqual(new[]{"Frozen"}),"native phase max/status retained");
                        Check(JsonSerializer.Serialize(donor)==before,"donor unchanged");
                        var mimic=Unit(41,form);state=State(mimic);state.CurrentTurn=9;
                        var row=new LedgerUnit(42,41,true,LedgerActualKind.Mimic,true,a,PaidInvestmentKind.Known,3);
                        var ledger=new NativeIdentityLedger(Epoch,43,1,new[]{row},Array.Empty<LedgerReceipt>());
                        NativeLedgerState.Validate(state,0,NativeIdentityLedgerCodec.Encode(ledger,0));Check(mimic.health==10,"valid copied timer leaves actual HP intact");
                        Reject(()=>NativeLedgerState.Bootstrap(state,Epoch),"missing timer cannot be inferred from Mimic native creation");
                    }
                });
                Test("wounded-veteran-frozen-poison-copy",()=>{
                    var unit=Unit(8,"rider",false);unit.health=43;unit.promotionLevel=1;unit.effects=new(){UnitEffect.Poisoned,UnitEffect.Frozen};
                    var state=State(unit);var before=JsonSerializer.Serialize(unit);var a=NativeLedgerState.Capture(state,new(unit,true),"rider",false);
                    Check(a.Health==43&&a.Maximum==150&&a.Veteran,"native veteran max and wounds");
                    Check(a.Effects.SequenceEqual(new[]{"Frozen","Poisoned"}),"canonical effects");Check(a.Concealment==ApparentConcealment.Visible,"visible frozen partner");
                    Check(JsonSerializer.Serialize(unit)==before,"capture did not mutate true unit");
                    unit.promotionLevel=2;Reject(()=>NativeLedgerState.Capture(state,new(unit,true),"rider",false),"multi-promotion cannot be silently booleanized");
                });
                Test("invisibility-snapshot-no-true-effect-writes",()=>{
                    var unit=Unit(9,"warrior",false);unit.effects=new(){UnitEffect.Invisible,UnitEffect.Boosted};var state=State(unit);
                    var a=NativeLedgerState.Capture(state,new(unit,true),"warrior",false);
                    Check(a.Concealment==ApparentConcealment.Hidden&&a.Effects.SequenceEqual(new[]{"Boosted","Invisible"}),"hidden status captured");
                    Check(unit.effects.SequenceEqual(new[]{UnitEffect.Invisible,UnitEffect.Boosted}),"native list order untouched");
                });
                Test("installed-native-effect-names-capture-and-validate",()=>{
                    string[] names={"Frozen","Poisoned","Boosted","Invisible","Bubble","Petrified","Swift","DoubleReady","Charmed"};
                    Check(Enum.GetNames<UnitEffect>().SequenceEqual(names)&&Enum.GetValues<UnitEffect>().Select(x=>(int)x).SequenceEqual(Enumerable.Range(0,9)),"fixture must preserve installed UnitEffect names and values");
                    var unit=Unit(90,"warrior",false);unit.health=41;unit.effects=Enum.GetValues<UnitEffect>().Reverse().ToList();var state=State(unit);
                    var original=JsonSerializer.Serialize(unit);var effects=unit.effects;var data=unit.UnitData;
                    var a=NativeLedgerState.Capture(state,new(unit,true),"warrior",false);
                    Check(a.Health==41&&a.Maximum==100&&a.Concealment==ApparentConcealment.Hidden,"capture retains genuine wounds and hidden status");
                    Check(a.Effects.SequenceEqual(names.OrderBy(x=>x,StringComparer.Ordinal)),"native capture uses exact canonical enum names including Swift and Charmed");
                    var baseline=NativeLedgerState.Bootstrap(state,Epoch);var row=baseline.Units.Single() with{Independent=true,Appearance=a};
                    var payload=NativeIdentityLedgerCodec.Encode(baseline with{Units=new[]{row}},0);NativeLedgerState.Validate(state,0,payload);
                    var loaded=NativeIdentityLedgerCodec.Decode(payload,0).Units.Single().Appearance!;
                    Check(loaded.Effects.SequenceEqual(a.Effects)&&loaded.Concealment==a.Concealment,"all installed statuses survive codec and native validation");
                    Check(JsonSerializer.Serialize(unit)==original&&ReferenceEquals(unit.effects,effects)&&ReferenceEquals(unit.UnitData,data),"status capture and validation leave all native values and borrowed references untouched");
                });
                Test("same-type-false-swift-overlay-validates-and-persists",()=>{
                    var unit=Unit(91,"warrior",false);unit.health=43;unit.promotionLevel=1;unit.effects=new(){UnitEffect.Charmed};var state=State(unit);
                    var original=JsonSerializer.Serialize(unit);var effects=unit.effects;var data=unit.UnitData;var baseline=NativeLedgerState.Bootstrap(state,Epoch);
                    Check(!baseline.Units.Single().Independent&&baseline.Units.Single().Appearance is null,"recipient starts as a genuine unmasked unit");
                    var captured=NativeLedgerState.Capture(state,new(unit,true),"warrior",false);
                    var overlay=captured with{Effects=captured.Effects.Append("Swift").OrderBy(x=>x,StringComparer.Ordinal).ToArray()};
                    var row=baseline.Units.Single() with{Independent=true,Appearance=overlay};var payload=NativeIdentityLedgerCodec.Encode(baseline with{Units=new[]{row}},1);
                    NativeLedgerState.Validate(state,1,payload);var loaded=NativeIdentityLedgerCodec.Decode(payload,1).Units.Single();
                    Check(loaded.Kind==LedgerActualKind.Genuine&&loaded.Independent&&loaded.Appearance!.Form==unit.type,"same type retains explicit independent appearance and genuine identity");
                    Check(loaded.Appearance!.Effects.SequenceEqual(new[]{"Charmed","Swift"})&&loaded.Appearance.Health==43&&loaded.Appearance.Maximum==150&&loaded.Appearance.Veteran,"false Swift leaves apparent wounds, veteran maximum and other native statuses intact");
                    var stable=NativeLedgerState.Reconcile(state,new(1,payload));
                    Check(!stable.Changed&&stable.Payload.SequenceEqual(payload),"reconcile does not normalize false Swift to the native effect list");
                    Check(JsonSerializer.Serialize(unit)==original&&ReferenceEquals(unit.effects,effects)&&ReferenceEquals(unit.UnitData,data)&&!unit.effects.Contains(UnitEffect.Swift),"false support does not modify native HP, effects or rule data");
                });
                Test("same-type-false-heal-and-poison-cleanse-validates",()=>{
                    foreach(bool poison in new[]{false,true})foreach(bool charm in new[]{false,true}){
                        var unit=Unit(92,"warrior",false);unit.health=43;unit.effects=new(){UnitEffect.Frozen,UnitEffect.Swift};
                        if(poison)unit.effects.Add(UnitEffect.Poisoned);if(charm)unit.effects.Add(UnitEffect.Charmed);
                        var state=State(unit);var original=JsonSerializer.Serialize(unit);var effects=unit.effects;var data=unit.UnitData;var baseline=NativeLedgerState.Bootstrap(state,Epoch);
                        var captured=NativeLedgerState.Capture(state,new(unit,true),"warrior",false);
                        // The support suite exercises the actual Heal helper; this
                        // integration case validates its independent payload using
                        // production Capture, native profile checks and persistence.
                        var healed=captured with{Health=poison?captured.Health:(ushort)83,Effects=poison?captured.Effects.Where(x=>x is not ("Poisoned" or "Charmed")).ToArray():captured.Effects};
                        var row=baseline.Units.Single() with{Independent=true,Appearance=healed};var payload=NativeIdentityLedgerCodec.Encode(baseline with{Units=new[]{row}},1);
                        NativeLedgerState.Validate(state,1,payload);var loaded=NativeIdentityLedgerCodec.Decode(payload,1).Units.Single();
                        Check(loaded.Kind==LedgerActualKind.Genuine&&loaded.Independent&&loaded.Appearance!.Form=="warrior","false Heal creates a same-type overlay without changing true identity");
                        Check(loaded.Appearance!.Health==(poison?43:83)&&loaded.Appearance.Maximum==100,"native poison cleanse replaces HP addition in apparent state");
                        Check(!loaded.Appearance.Effects.Contains("Poisoned")&&loaded.Appearance.Effects.Contains("Charmed")==(!poison&&charm)&&loaded.Appearance.Effects.Contains("Frozen")&&loaded.Appearance.Effects.Contains("Swift"),"only poisoned profiles lose Charmed; unrelated statuses remain");
                        var stable=NativeLedgerState.Reconcile(state,new(1,payload));Check(!stable.Changed&&stable.Payload.SequenceEqual(payload),"false healed health and cured effects persist through reconciliation");
                        Check(JsonSerializer.Serialize(unit)==original&&ReferenceEquals(unit.effects,effects)&&ReferenceEquals(unit.UnitData,data),"fake Heal does not cure or heal the native body");
                    }
                });
                Test("invented-effect-aliases-rejected-by-native-validation",()=>{
                    var unit=Unit(93,"warrior",false);unit.health=43;var state=State(unit);var original=JsonSerializer.Serialize(unit);
                    var baseline=NativeLedgerState.Bootstrap(state,Epoch);var a=NativeLedgerState.Capture(state,new(unit,true),"warrior",false);
                    foreach(string alias in new[]{"Swarmed","FrozenOnce","Venom","Bubbled"}){
                        var row=baseline.Units.Single() with{Independent=true,Appearance=a with{Effects=new[]{alias}}};
                        var payload=NativeIdentityLedgerCodec.Encode(baseline with{Units=new[]{row}},1);
                        Reject(()=>NativeLedgerState.Validate(state,1,payload),"syntactically valid but unknown native effect "+alias);
                    }
                    Check(JsonSerializer.Serialize(unit)==original,"invalid apparent payload rejection does not mutate native truth");
                });
                Test("growing-apparent-profile-preserves-native-effect-names",()=>{
                    var unit=Unit(94,"dragonegg",false);unit.health=61;unit.createdTurn=7;unit.effects=new(){UnitEffect.Swift,UnitEffect.Charmed,UnitEffect.Petrified};var state=State(unit);state.CurrentTurn=9;
                    var original=JsonSerializer.Serialize(unit);var captured=NativeLedgerState.Capture(state,new(unit,true),"dragonegg",false);var baseline=NativeLedgerState.Bootstrap(state,Epoch);
                    var grown=NativeApparentGrowth.Advance(captured,10);var row=baseline.Units.Single() with{Independent=true,Appearance=grown};
                    var payload=NativeIdentityLedgerCodec.Encode(baseline with{Units=new[]{row}},1);NativeLedgerState.Validate(state,1,payload);var loaded=NativeIdentityLedgerCodec.Decode(payload,1).Units.Single().Appearance!;
                    Check(loaded.Form=="babydragon"&&loaded.Health==111&&loaded.Maximum==150&&loaded.GrowthDueTurn==13,"apparent growth preserves wounds and native next-stage timer");
                    Check(loaded.Effects.SequenceEqual(new[]{"Charmed","Petrified","Swift"})&&!ReferenceEquals(grown.Effects,captured.Effects),"growth carries native statuses as an independent value copy");
                    Check(JsonSerializer.Serialize(unit)==original&&unit.health==61&&unit.type=="dragonegg","apparent growth does not grow or heal the genuine body");
                });
                Test("snapshot-not-regenerated-at-reconcile",()=>{
                    var unit=Unit(10,"warrior");var state=State(unit);var ledger=NativeLedgerState.Bootstrap(state,Epoch);
                    var copied=ledger.Units.Single().Appearance! with{Health=31,Maximum=150,Veteran=true,Effects=new[]{"Frozen"}};
                    ledger=ledger with{Units=new[]{ledger.Units.Single() with{Appearance=copied,InvestmentKind=PaidInvestmentKind.Known,PaidStars=3}}};
                    var snapshot=Snapshot(ledger);state.Player.color=7654321;unit.effects.Add(UnitEffect.Poisoned);
                    var result=NativeLedgerState.Reconcile(state,snapshot);var row=Decode(result).Units.Single();
                    Check(!result.Changed&&result.Revision==0,"no gratuitous ledger revision");
                    Check(row.Appearance!.Health==31&&row.Appearance.Veteran&&row.Appearance.Effects.SequenceEqual(new[]{"Frozen"}),"apparent wounds/veteran/status retained");
                    Check(row.Appearance.SourceColor==copied.SourceColor&&row.PaidStars==3&&row.InvestmentKind==PaidInvestmentKind.Known,"independent color and paid value retained");
                    Check(result.Payload.SequenceEqual(snapshot.Bytes()),"exact payload retained");
                });
                Test("retirement-preserves-identity-payment-receipts",()=>{
                    var unit=Unit(11,"rider");var state=State(unit);var ledger=NativeLedgerState.Bootstrap(state,Epoch);var row=ledger.Units.Single() with{InvestmentKind=PaidInvestmentKind.Known,PaidStars=3};
                    ledger=ledger with{Units=new[]{row}};state.Map.Tiles[0].unit=null;
                    var result=NativeLedgerState.Reconcile(state,Snapshot(ledger));var dead=Decode(result).Units.Single();
                    Check(result.Changed&&result.Revision==0,"lifecycle retirement does not invent a command revision");
                    Check(!dead.Alive&&!dead.Independent&&dead.Appearance is null&&dead.LogicalId==row.LogicalId&&dead.NativeId==11&&dead.PaidStars==3,"retired row retains identity/actual payment");
                    Check(Decode(result).NextSequence==1&&Decode(result).Receipts.Length==0,"lifecycle does not invent command receipts");
                    var again=NativeLedgerState.Reconcile(state,new(result.Revision,result.Payload));Check(!again.Changed&&again.Payload.SequenceEqual(result.Payload),"retirement is idempotent");
                    state.Map.Tiles[0].unit=Unit(11,"rider");Reject(()=>NativeLedgerState.Reconcile(state,new(result.Revision,result.Payload)),"dead native ID reuse");
                });
                Test("new-birth-no-cost-inference",()=>{
                    var first=Unit(12,"warrior");var state=State(first);var old=Snapshot(NativeLedgerState.Bootstrap(state,Epoch));
                    var second=Unit(13,"doomux");state.CurrentUnitId=13;state.Map.Tiles=new[]{new Tile(){unit=first},new Tile(){unit=second}};
                    var result=NativeLedgerState.Reconcile(state,old);var row=Decode(result).Units.Single(x=>x.NativeId==13);
                    Check(result.Changed&&row.Appearance!.Form=="doomux"&&row.InvestmentKind==PaidInvestmentKind.UnknownLegacy&&row.PaidStars==0,"actual body birth alone is not paid evidence");
                    Check(first.health==10&&second.health==10,"true HP untouched");
                });
                Test("water-mode-reconcile-preserves-flight-across-flood-and-clears-on-land",()=>{
                    var unit=Unit(14,"firedragon");var state=State(unit);var tile=state.Map.Tiles[0];tile.IsWater=true;
                    var ledger=NativeLedgerState.Bootstrap(state,Epoch);var first=NativeLedgerState.Reconcile(state,Snapshot(ledger));
                    Check(Decode(first).Units[0].MimicWaterForm&&Decode(first).Units[0].Transport is null,"openwater flight must record boat without actual cargo");
                    tile.IsWater=false;tile.Wetland=true;var second=NativeLedgerState.Reconcile(state,new(first.Revision,first.Payload));
                    Check(Decode(second).Units[0].MimicWaterForm&&!second.Changed,"flood erased incoming mode");
                    var reset=NativeIdentityLedgerCodec.Decode(second.Payload,second.Revision);var third=NativeLedgerState.Reconcile(state,Snapshot(reset,second.Revision));
                    Check(third.Payload.SequenceEqual(second.Payload),"mode did not survive reload");tile.Wetland=false;
                    var fourth=NativeLedgerState.Reconcile(state,new(third.Revision,third.Payload));Check(!Decode(fourth).Units[0].MimicWaterForm&&fourth.Changed,"dry arrival retained old boat mode");
                    Check(unit.health==10&&unit.passengerUnit is null&&unit.type==RecruitmentCatalog.Variant("firedragon"),"mode persistence changed live facts");
                });
                Test("new-flooded-land-mimic-does-not-infer-boat",()=>{
                    var state=State(Unit(15,"firedragon"));state.Map.Tiles[0].Wetland=true;
                    var empty=new NativeIdentityLedger(Epoch,1,1,Array.Empty<LedgerUnit>(),Array.Empty<LedgerReceipt>());
                    var row=Decode(NativeLedgerState.Reconcile(state,Snapshot(empty))).Units.Single();
                    Check(!row.MimicWaterForm&&row.Transport is null,"new flood land body became boat");
                });
                Test("all-carrier-forms-reconcile-water-flood-land-and-coastal-city",()=>{
                    int seen=0;bool sawMoth=false;
                    foreach(string form in RecruitmentCatalog.CarrierForms)
                    {
                        bool vessel=RecruitmentCatalog.NavalCopyOnly.Contains(form,StringComparer.Ordinal);
                        var donor=Unit(65,form,false);donor.health=RecruitmentHooks.Data(null!,form).health;donor.createdTurn=4;
                        if(vessel){donor.passengerUnit=Unit(64,"warrior",false);donor.health=100;}
                        var donorState=State(donor);donorState.CurrentTurn=5;
                        var apparent=NativeLedgerState.Capture(donorState,new(donor,true),form,false);
                        var body=Unit(65,form);if(vessel)body.passengerUnit=Unit(64,"warrior");
                        var state=State(body);state.CurrentTurn=5;var tile=state.Map.Tiles[0];
                        var row=new LedgerUnit(66,65,true,LedgerActualKind.Mimic,true,apparent,PaidInvestmentKind.Known,3,vessel?new(64):null);
                        var ledger=new NativeIdentityLedger(Epoch,67,1,new[]{row},Array.Empty<LedgerReceipt>());var frame=Snapshot(ledger);
                        string before=JsonSerializer.Serialize(body);byte[] appearanceHash=NativeExchangeIntentCodec.AppearanceFingerprint(apparent);
                        foreach(string terrain in new[]{"land","openwater","flood-from-water","coastal-city","dry-land"})
                        {
                            tile.IsWater=terrain is "openwater" or "coastal-city";tile.Wetland=terrain=="flood-from-water";tile.City=terrain=="coastal-city";
                            var result=NativeLedgerState.Reconcile(state,frame);var next=Decode(result).Units.Single();
                            Check(next.MimicWaterForm==(terrain is "openwater" or "flood-from-water"),form+" recorded wrong mode at "+terrain);
                            Check(next.NativeId==65&&next.LogicalId==66&&next.Transport==row.Transport&&next.PaidStars==3&&next.Appearance!.Form==form,form+" changed persistent identity at "+terrain);
                            Check(NativeExchangeIntentCodec.AppearanceFingerprint(next.Appearance!).SequenceEqual(appearanceHash)&&JsonSerializer.Serialize(body)==before,form+" changed native facts or copied state");
                            NativeLedgerState.Validate(state,result.Revision,result.Payload);
                            var again=NativeLedgerState.Reconcile(state,new(result.Revision,result.Payload));
                            Check(!again.Changed&&again.Payload.SequenceEqual(result.Payload),form+" serialization boundary was not idempotent");
                            frame=new(result.Revision,result.Payload);
                        }
                        seen++;sawMoth|=form=="moth";
                    }
                    Check(seen==RecruitmentCatalog.CarrierForms.Count()&&sawMoth,"all-carrier matrix omitted Moth or a supported form");
                });
                Test("copy-save-load-independent-value",()=>{
                    var state=State(Unit(14,"archer"));var old=NativeLedgerState.Bootstrap(state,Epoch);var snapshot=Snapshot(old);
                    byte[] wire=snapshot.Bytes();var clone=State(Unit(14,"archer"));NativeLedgerState.Validate(clone,0,wire);
                    var copy=NativeLedgerState.Reconcile(clone,new(0,wire));Check(copy.Payload.SequenceEqual(wire),"copied state independent same payload");
                    wire[wire.Length-1]^=0xff;Check(snapshot.Bytes().SequenceEqual(copy.Payload),"snapshot does not alias caller payload");
                });
                Test("kind-status-carrier-counter-corruption-rejected",()=>{
                    var unit=Unit(15,"warrior");var state=State(unit);var ledger=NativeLedgerState.Bootstrap(state,Epoch);var snap=Snapshot(ledger);
                    unit.type="warrior";unit.UnitData=RecruitmentHooks.Data(state,"warrior");unit.health=100;
                    Reject(()=>NativeLedgerState.Reconcile(state,snap),"true mimic identity cannot become genuine silently");
                    unit.type=RecruitmentCatalog.Variant("warrior");unit.UnitData=RecruitmentHooks.Data(state,unit.type);unit.health=10;
                    state.CurrentUnitId=14;Reject(()=>NativeLedgerState.Reconcile(state,snap),"counter behind live IDs");
                    state.CurrentUnitId=uint.MaxValue;Reject(()=>NativeLedgerState.Reconcile(state,snap),"counter rollover");state.CurrentUnitId=15;
                    var a=ledger.Units.Single().Appearance!;
                    var bad=ledger with{Units=new[]{ledger.Units.Single() with{Appearance=a with{Effects=new[]{"Invisible"}}}}};
                    Reject(()=>NativeLedgerState.Validate(state,0,NativeIdentityLedgerCodec.Encode(bad,0)),"concealment/effect mismatch");
                });
                Test("genuine-tracked-unit-may-board-without-losing-private-row",()=>{
                    var passenger=Unit(16,"warrior",false);var carrier=Unit(17,"scoutship",false);carrier.passengerUnit=passenger;var state=State(carrier);
                    var ledger=new NativeIdentityLedger(Epoch,19,1,new[]{new LedgerUnit(1,17,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,2,new(16))},Array.Empty<LedgerReceipt>());
                    NativeLedgerState.Validate(state,0,NativeIdentityLedgerCodec.Encode(ledger,0));
                    Check(NativeLedgerState.Bootstrap(state,Epoch).Units.Single().Transport!.PassengerNativeId==16,"one root row binds actual native cargo");
                });
                Test("legacy-siblings-and-replayed-births-share-deterministic-ids",()=>{
                    var original=Unit(20,"warrior");var initial=State(original);var baseline=NativeLedgerState.Bootstrap(initial,Epoch);
                    var addedGenuine=Unit(21,"rider",false);var addedMimic=Unit(22,"archer");
                    var current=State(original,addedGenuine,addedMimic);var migrated=NativeLedgerState.Bootstrap(current,Epoch);
                    var replayed=Decode(NativeLedgerState.Reconcile(current,Snapshot(baseline)));
                    Check(migrated.Units.Select(x=>(x.NativeId,x.LogicalId)).SequenceEqual(replayed.Units.Select(x=>(x.NativeId,x.LogicalId))),"legacy current and replay-generated IDs agree");
                    Check(migrated.Units.All(x=>x.LogicalId==(ulong)x.NativeId+1)&&migrated.NextLogicalId==24,"counter reservation covers all native allocations");
                    Check(migrated.Units.Single(x=>x.NativeId==21).Appearance is null&&!migrated.Units.Single(x=>x.NativeId==21).Independent,"genuine birth gets identity only");
                });
                Test("native-v123-embark-disembark-identity-shape",()=>{
                    var passenger=Unit(23,"warrior",false);var state=State(passenger);var baseline=NativeLedgerState.Bootstrap(state,Epoch);
                    var passengerRow=baseline.Units.Single() with{InvestmentKind=PaidInvestmentKind.Known,PaidStars=2};baseline=baseline with{Units=new[]{passengerRow}};
                    var vessel=Unit(24,"scoutship",false);vessel.passengerUnit=passenger;state.Map.Tiles[0].unit=vessel;state.CurrentUnitId=24;
                    var embarked=NativeLedgerState.Reconcile(state,Snapshot(baseline));var a=Decode(embarked);var afloat=a.Units.Single();
                    Check(afloat.NativeId==24&&afloat.LogicalId==passengerRow.LogicalId&&afloat.Transport!.PassengerNativeId==23&&afloat.PaidStars==2,"one logical identity spans native wrapper and original passenger");
                    Check(afloat.Alive&&afloat.Appearance is null,"native vessel does not invent independent appearance");
                    state.Map.Tiles[0].unit=passenger;
                    var disembarked=NativeLedgerState.Reconcile(state,new(embarked.Revision,embarked.Payload));var b=Decode(disembarked).Units.Single();
                    Check(b.NativeId==23&&b.Transport is null&&b.Alive&&b.LogicalId==passengerRow.LogicalId&&b.PaidStars==2,"landing restores original passenger without a new identity");
                    Check(passenger.passengerUnit is null&&passenger.health==100,"adapter does not mutate native cargo");
                });
                Test("genuine-health0-keeps-identity-until-native-removal",()=>{
                    var unit=Unit(25,"warrior",false);var state=State(unit);var ledger=NativeLedgerState.Bootstrap(state,Epoch);unit.health=0;
                    var pending=NativeLedgerState.Reconcile(state,Snapshot(ledger));Check(Decode(pending).Units.Single().Alive,"native dying body still owns its identity");
                    state.Map.Tiles[0].unit=null;var removed=NativeLedgerState.Reconcile(state,new(pending.Revision,pending.Payload));
                    Check(!Decode(removed).Units.Single().Alive,"retirement waits for native removal");
                });
                Test("mimic-health0-requires-exact-native-kill-queue-witness",()=>{
                    var unit=Unit(26,"warrior");var state=State(unit);var ledger=NativeLedgerState.Bootstrap(state,Epoch);var snapshot=Snapshot(ledger);unit.health=0;
                    Reject(()=>NativeLedgerState.Reconcile(state,snapshot),"no queued native kill");
                    state.ActionStack.Add(new KillAction(){Coordinates=new(){X=1,Y=0}});
                    Reject(()=>NativeLedgerState.Reconcile(state,snapshot),"queued kill targets another tile");
                    state.ActionStack.Clear();state.ActionStack.Add(new KillAction(){Coordinates=new(){X=0,Y=0}});
                    var accepted=NativeLedgerState.Reconcile(state,snapshot);Check(Decode(accepted).Units.Single().Alive,"transient Mimic body is still tracked");
                    Check(unit.health==0&&state.ActionStack.Count==1,"validation neither revives unit nor consumes kill action");
                    state.Map.Tiles[0].unit=null;state.ActionStack.Clear();var removed=NativeLedgerState.Reconcile(state,new(accepted.Revision,accepted.Payload));
                    Check(!Decode(removed).Units.Single().Alive,"Mimic retires after native kill removes exact body");
                });
                Test("mimic-transient-cannot-bypass-identity-profile-or-diagnostic-guards",()=>{
                    var unit=Unit(27,"warrior");var state=State(unit);var snapshot=Snapshot(NativeLedgerState.Bootstrap(state,Epoch));unit.health=0;
                    state.ActionStack.Add(new KillAction(){Coordinates=new(){X=0,Y=0}});state.LookupOverride=Unit(999,"warrior");
                    Reject(()=>NativeLedgerState.Reconcile(state,snapshot),"lookup returned a different native identity");state.LookupOverride=null;
                    unit.health=5;Reject(()=>NativeLedgerState.Reconcile(state,snapshot),"queued death does not admit partial true Mimic HP");unit.health=0;
                    unit.UnitData.attack=1;Reject(()=>NativeLedgerState.Reconcile(state,snapshot),"queued death does not relax zero offense");unit.UnitData.attack=0;
                    NativeRegroupSession.Production=false;
                    Reject(()=>NativeLedgerState.Validate(state,snapshot.Revision,snapshot.Bytes()),"private diagnostics remain strict actual10");
                    NativeRegroupSession.Production=true;
                });
                Test("schema3-native-vessel-capture-preserves-independent-passenger",()=>{
                    var passenger=Unit(60,"rider",false);passenger.health=43;passenger.promotionLevel=1;passenger.effects.Add(UnitEffect.Frozen);
                    var vessel=Unit(61,"scoutship",false);vessel.health=82;vessel.passengerUnit=passenger;vessel.effects.Add(UnitEffect.Poisoned);var state=State(vessel);
                    string before=JsonSerializer.Serialize(vessel);var a=NativeLedgerState.Capture(state,new(vessel,true),"scoutship",false);
                    Check(a.Maximum==150&&a.Health==82&&a.Passenger!.Health==43&&a.Passenger.Maximum==150&&a.Passenger.Veteran,"vessel and cargo retain separate health/veteran snapshots");
                    Check(a.Effects.SequenceEqual(new[]{"Poisoned"})&&a.Passenger!.Effects.SequenceEqual(new[]{"Frozen"}),"vessel/cargo effects stay independent");
                    Check(JsonSerializer.Serialize(vessel)==before,"capture does not alter either native body");
                    var bound=new LedgerUnit(61,61,true,LedgerActualKind.Genuine,true,a,PaidInvestmentKind.Known,2,new(60));
                    var ledger=new NativeIdentityLedger(Epoch,63,1,new[]{bound},Array.Empty<LedgerReceipt>());
                    NativeLedgerState.Validate(state,0,NativeIdentityLedgerCodec.Encode(ledger,0));
                    passenger.health=20;passenger.effects.Clear();var result=NativeLedgerState.Reconcile(state,Snapshot(ledger));
                    Check(!result.Changed&&Decode(result).Units[0].Appearance!.Passenger!.Health==43,"saved apparent passenger does not follow live cargo after capture");
                });
                Test("native-vessels-use-passenger-maximum-without-protect",()=>{
                    foreach(string form in RecruitmentCatalog.NavalCopyOnly){
                        var passenger=Unit(80,"giant",false);passenger.health=310;
                        var vessel=Unit(81,form,false);vessel.health=270;vessel.passengerUnit=passenger;var state=State(vessel);
                        string before=JsonSerializer.Serialize(vessel);var a=NativeLedgerState.Capture(state,new(vessel,true),form,false);
                        Check(a.Maximum==400&&a.Health==270&&a.Passenger!.Maximum==400&&a.Passenger.Health==310,"native Protect39, not Stiff38 or form name, determines cargo maximum: "+form);
                        var row=new LedgerUnit(82,81,true,LedgerActualKind.Genuine,true,a,PaidInvestmentKind.Known,2,new(80));
                        var ledger=new NativeIdentityLedger(Epoch,83,1,new[]{row},Array.Empty<LedgerReceipt>());
                        var bytes=NativeIdentityLedgerCodec.Encode(ledger,0);NativeLedgerState.Validate(state,0,bytes);
                        var result=NativeLedgerState.Reconcile(state,new(0,bytes));
                        Check(!result.Changed&&result.Payload.SequenceEqual(bytes),"cargo maximum survives framed roundtrip: "+form);
                        Check(JsonSerializer.Serialize(vessel)==before,"source root and cargo untouched: "+form);
                    }
                });
                Test("mimic-vessels-keep-true-one-hp-with-high-hp-apparent-cargo",()=>{
                    foreach(string form in RecruitmentCatalog.NavalCopyOnly){
                        var donor=Unit(82,"giant",false);donor.health=310;
                        var ship=Unit(83,form,false);ship.health=270;ship.passengerUnit=donor;
                        var a=NativeLedgerState.Capture(State(ship),new(ship,true),form,false);
                        var passenger=Unit(82,"giant");var mimic=Unit(83,form);mimic.passengerUnit=passenger;var state=State(mimic);
                        var row=new LedgerUnit(84,83,true,LedgerActualKind.Mimic,true,a,PaidInvestmentKind.Known,3,new(82));
                        var ledger=new NativeIdentityLedger(Epoch,85,1,new[]{row},Array.Empty<LedgerReceipt>());var bytes=NativeIdentityLedgerCodec.Encode(ledger,0);
                        NativeLedgerState.Validate(state,0,bytes);var loaded=NativeLedgerState.Reconcile(state,new(0,bytes));
                        Check(!loaded.Changed&&loaded.Payload.SequenceEqual(bytes),"Mimic apparent cargo survives roundtrip: "+form);
                        Check(mimic.health==10&&passenger.health==10&&mimic.UnitData.health==10&&passenger.UnitData.health==10,"Mimic and cargo stay at actual1HP: "+form);
                        var bad=ledger with{Units=new[]{row with{Appearance=a with{Health=100,Maximum=100}}}};
                        Reject(()=>NativeLedgerState.Validate(state,0,NativeIdentityLedgerCodec.Encode(bad,0)),"false fixed hull maximum must be rejected: "+form);
                    }
                });
                Test("native-vessel-maximum-uses-passenger-veteran-only",()=>{
                    foreach(string form in RecruitmentCatalog.NavalCopyOnly){
                        var passenger=Unit(84,"warrior",false);passenger.health=93;passenger.promotionLevel=1;
                        var vessel=Unit(85,form,false);vessel.health=142;vessel.promotionLevel=1;vessel.passengerUnit=passenger;var state=State(vessel);
                        var a=NativeLedgerState.Capture(state,new(vessel,true),form,false);
                        Check(a.Maximum==150&&a.Passenger!.Maximum==150&&a.Passenger.Veteran&&a.Veteran,"root promotion must not add another50 to passenger maximum: "+form);
                        var row=new LedgerUnit(86,85,true,LedgerActualKind.Genuine,true,a,PaidInvestmentKind.Known,2,new(84));
                        NativeLedgerState.Validate(state,0,NativeIdentityLedgerCodec.Encode(new(Epoch,87,1,new[]{row},Array.Empty<LedgerReceipt>()),0));
                    }
                });
                Test("protect39-keeps-hull-maximum-independent-of-passenger",()=>{
                    var data=RecruitmentHooks.Data(null!,"transportship");data.unitAbilities.Add(39);
                    try{
                        var passenger=Unit(86,"giant",false);passenger.health=310;
                        var vessel=Unit(87,"transportship",false);vessel.health=80;vessel.passengerUnit=passenger;var state=State(vessel);
                        var a=NativeLedgerState.Capture(state,new(vessel,true),"transportship",false);
                        Check(a.Maximum==100&&a.Health==80&&a.Passenger!.Maximum==400&&a.Passenger.Health==310,"Protect hull retains native base health and independent cargo");
                        var row=new LedgerUnit(88,87,true,LedgerActualKind.Genuine,true,a,PaidInvestmentKind.Known,2,new(86));
                        var ledger=new NativeIdentityLedger(Epoch,89,1,new[]{row},Array.Empty<LedgerReceipt>());
                        NativeLedgerState.Validate(state,0,NativeIdentityLedgerCodec.Encode(ledger,0));
                        var wrong=ledger with{Units=new[]{row with{Appearance=a with{Maximum=400}}}};
                        Reject(()=>NativeLedgerState.Validate(state,0,NativeIdentityLedgerCodec.Encode(wrong,0)),"Protect hull may not inherit cargo maximum");
                        Check(vessel.health==80&&passenger.health==310,"Protect capture never mutates true health");
                    }finally{data.unitAbilities.Remove(39);}
                });
                Test("schema3-mimic-vessel-needs-bound-cargo-and-independent-value",()=>{
                    foreach(string form in RecruitmentCatalog.NavalCopyOnly){
                        var bare=Unit(62,form);var state=State(bare);
                        Reject(()=>NativeLedgerState.Bootstrap(state,Epoch),"bare Mimic boat has no inferred passenger");
                        bare.passengerUnit=Unit(61,"warrior");
                        Reject(()=>NativeLedgerState.Bootstrap(state,Epoch),"Mimic boat native cargo does not prove missing appearance");
                    }
                    var donor=Unit(64,"warrior",false);var ship=Unit(65,"scoutship",false);ship.passengerUnit=donor;var observed=NativeLedgerState.Capture(State(ship),new(ship,true),"scoutship",false);
                    var passenger=Unit(64,"warrior");var mimic=Unit(65,"scoutship");mimic.passengerUnit=passenger;var current=State(mimic);
                    var row=new LedgerUnit(65,65,true,LedgerActualKind.Mimic,true,observed,PaidInvestmentKind.Known,3,new(64));
                    var ledger=new NativeIdentityLedger(Epoch,67,1,new[]{row},Array.Empty<LedgerReceipt>());var wire=NativeIdentityLedgerCodec.Encode(ledger,0);
                    NativeLedgerState.Validate(current,0,wire);Check(mimic.health==10&&passenger.health==10,"bound Mimic vessel and cargo keep true1HP");
                    passenger.owner=2;Reject(()=>NativeLedgerState.Validate(current,0,wire),"foreign actual cargo");passenger.owner=1;
                    passenger.type=RecruitmentCatalog.Variant("rider");passenger.UnitData=RecruitmentHooks.Data(current,passenger.type);
                    Reject(()=>NativeLedgerState.Validate(current,0,wire),"cargo capabilities disagree with independent passenger");
                });
                Test("schema1-schema2-wrapper-migration-preserves-passenger-lineage",()=>{
                    foreach(int schema in new[]{1,2}){
                        var passenger=Unit(70,"warrior",false);var ship=Unit(71,"scoutship",false);ship.passengerUnit=passenger;var state=State(ship);
                        var p=new LedgerUnit(71,70,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,2);
                        var wrapper=new LedgerUnit(72,71,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0);
                        var old=new NativeIdentityLedger(Epoch,73,1,new[]{p,wrapper},Array.Empty<LedgerReceipt>());
                        var bytes=schema==1?NativeIdentityLedgerCodec.EncodeLegacyFingerprint(old,0):NativeIdentityLedgerCodec.EncodeTimedFingerprint(old,0);
                        NativeLedgerState.Validate(state,0,bytes);var result=NativeLedgerState.Reconcile(state,new(0,bytes));var row=Decode(result).Units.Single();
                        Check(result.Changed&&row.NativeId==71&&row.LogicalId==71&&row.Transport!.PassengerNativeId==70&&row.PaidStars==2,"old passenger lineage/payment wins over wrapper");
                        var second=NativeLedgerState.Reconcile(state,new(result.Revision,result.Payload));
                        Check(!second.Changed&&second.Payload.SequenceEqual(result.Payload),"migration repeats without change");
                    }
                });
                Test("legacy-wrapper-with-receipt-cannot-be-erased",()=>{
                    var passenger=Unit(72,"warrior",false);var ship=Unit(73,"scoutship",false);ship.passengerUnit=passenger;var state=State(ship);
                    var p=new LedgerUnit(73,72,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.Known,2);
                    var wrapper=new LedgerUnit(74,73,true,LedgerActualKind.Genuine,false,null,PaidInvestmentKind.UnknownLegacy,0);
                    var old=new NativeIdentityLedger(Epoch,75,2,new[]{p,wrapper},new[]{new LedgerReceipt(1,Epoch,new byte[32],0,1,new ulong[]{74})});
                    var wire=NativeIdentityLedgerCodec.EncodeTimedFingerprint(old,1);var untouched=wire.ToArray();
                    Reject(()=>NativeLedgerState.Validate(state,1,wire),"wrapper named in history requires explicit migration");
                    Reject(()=>NativeLedgerState.Reconcile(state,new(1,wire)),"reconcile cannot discard wrapper receipt");
                    Check(wire.SequenceEqual(untouched)&&ship.passengerUnit==passenger,"rejection does not alter input or native cargo");
                });
                Test("independent-passenger-cannot-use-genuine-transport-migration",()=>{
                    var passenger=Unit(74,"warrior");var initial=State(passenger);var old=NativeLedgerState.Bootstrap(initial,Epoch);
                    var ship=Unit(75,"scoutship",false);ship.passengerUnit=passenger;var state=State(ship);
                    Reject(()=>NativeLedgerState.Reconcile(state,Snapshot(old)),"independent passenger needs authenticated transport transition");
                    Check(passenger.health==10&&passenger.type==RecruitmentCatalog.Variant("warrior"),"rejection preserves underlying Mimic");
                });
                var result=new{tests=Tests,assertions=Assertions,failed=0,names=Results,scope="Pure managed fixture; no Unity, game, native hooks or save operations"};
                File.WriteAllText(Environment.GetEnvironmentVariable("LEDGER_TEST_OUTPUT")!,JsonSerializer.Serialize(result));return 0;
            }
            catch(Exception e){File.WriteAllText(Environment.GetEnvironmentVariable("LEDGER_TEST_OUTPUT")!,JsonSerializer.Serialize(new{tests=Tests,assertions=Assertions,failed=1,error=e.ToString()}));return 1;}
        }
    }
}
namespace Deception.PolyModAdapter
{
    internal enum Tribe { None,Imperius,Bardur }
    internal enum Skin { None,Default }
    internal enum ActionType { Move,KillUnit }
    internal class NativeAction {public ActionType Kind{get;set;}=ActionType.Move;}
    internal sealed class KillAction:NativeAction {public KillAction(){Kind=ActionType.KillUnit;}public Point Coordinates{get;set;}=new();}
    internal sealed class Point {public int X{get;set;}public int Y{get;set;}}
    internal enum UnitEffect { Frozen,Poisoned,Boosted,Invisible,Bubble,Petrified,Swift,DoubleReady,Charmed }
    internal sealed class Player {public byte Id{get;set;}=1;public Tribe tribe{get;set;}=Tribe.Imperius;public Tribe _climate{get;set;}=Tribe.None;public int tribeMix{get;set;}=0;public int color{get;set;}=123456;}
    internal sealed class Data {public List<int> unitAbilities{get;set;}=new();public string type{get;set;}="";public int health{get;set;}=100;public int attack{get;set;}=0;public int defence{get;set;}=0;public int range{get;set;}=0;public int growthRate{get;set;}=0;}
    internal sealed class Unit
    {
        static long Next;public long Pointer{get;}=++Next;
        public ushort createdTurn{get;set;}public uint id{get;set;}public string type{get;set;}="";public byte owner{get;set;}=1;
        public Point coordinates{get;set;}=new();public int health{get;set;}public int promotionLevel{get;set;}public Data UnitData{get;set;}=new();
        public List<UnitEffect> effects{get;set;}=new();public Unit? passengerUnit{get;set;}public Tribe birthClimate{get;set;}=Tribe.Imperius;public Skin birthClimateSkinType{get;set;}=Skin.Default;
    }
    internal enum ImprovementType {None=0,City=1}
    internal sealed class Tile {public bool IsWater{get;set;}public bool Wetland{get;set;}public bool City{get;set;}public Unit? unit{get;set;}public Point coordinates{get;set;}=new();}
    internal sealed class Map {public Tile[] Tiles{get;set;}=Array.Empty<Tile>();}
    internal sealed class State {public int Version{get;set;}=123;public uint CurrentTurn{get;set;}public uint CurrentUnitId{get;set;}public Map Map{get;set;}=new();public Player Player{get;set;}=new();public Player? OtherPlayer{get;set;}public object GameLogicData{get;}=new();public List<NativeAction> ActionStack{get;}=new();public Unit? LookupOverride{get;set;}}
    internal sealed record Method(string Owner,string Name);
    internal static class Native
    {
        internal static Type T(string name)=>name switch{"Polytopia.Data.ImprovementData+Type"=>typeof(ImprovementType),"GameState"=>typeof(State),"ActionType"=>typeof(ActionType),"PolytopiaBackendBase.Common.TribeType"=>typeof(Tribe),"PolytopiaBackendBase.Common.SkinType"=>typeof(Skin),"UnitEffect"=>typeof(UnitEffect),_=>throw new Exception("Unknown fixture type: "+name)};
        internal static object? Get(object o,string name)=>name=="Length"&&o is Array a?a.Length:o.GetType().GetProperty(name)!.GetValue(o);
        internal static int Int(object? o)=>Convert.ToInt32(o);
        internal static IEnumerable<object> Items(object value)=>((System.Collections.IEnumerable)value).Cast<object>();
        internal static object NewList(string type,IEnumerable<object> values)=>type=="UnitEffect"?values.Cast<UnitEffect>().ToList():throw new Exception("Unknown list type "+type);
        internal static long Pointer(object o)=>((Unit)o).Pointer;
        internal static bool Same(object? a,object? b)=>a is not null&&b is not null&&ReferenceEquals(a,b);
        internal static object As(object o,string type)=>type=="KillUnitAction"&&o is KillAction?o:throw new InvalidDataException("Wrong concrete native action type");
        internal static string EnumName(object value)=>value.ToString()!.ToLowerInvariant();
        // Native EnumCache maps lowercase data identifiers to capitalized
        // CLR enum members; visual profile names use a separate exact codec.
        internal static object EnumValue(string enumName,string id)=>enumName=="Polytopia.Data.ImprovementData+Type"&&id=="city"?ImprovementType.City:
            throw new InvalidOperationException("Unknown fixture registry mapping: "+enumName+"/"+id);
        internal static Method M(string type,string name,params string[] args)=>new(type,name);
        internal static Method M(Type type,string name,params string[] args)=>new(type.FullName!,name);
        internal static object? Invoke(Method method,object? target,params object?[] args)
        {
            if(method.Name=="get_Item")return target is Array a?a.GetValue((int)args[0]!):target!.GetType().GetProperty("Item")!.GetValue(target,new[]{args[0]});
            return (method.Owner,method.Name) switch
            {
                ("GameState","TryGetPlayer")=>TryPlayer((State)target!,args),
                ("GameState","TryGetUnit")=>TryUnit((State)target!,args),
                ("TileData","IsWetland")=>((Tile)target!).Wetland,
                ("TileData","HasImprovement")=>args[0] is ImprovementType.City?((Tile)target!).City:throw new Exception("City lookup did not use native registry value1"),
                ("MapDataExtensions","GetTile")=>((Map)args[0]!).Tiles.FirstOrDefault(t=>t.coordinates.X==((Point)args[1]!).X&&t.coordinates.Y==((Point)args[1]!).Y),
                ("ActionBase","GetActionType")=>((NativeAction)target!).Kind,
                ("PlayerExtensions","GetVisualTribeType")=>((Player)args[0]!).tribe,
                ("PlayerState","GetVisualSkin")=>Skin.Default,
                ("UnitState","HasLeader")=>false,("UnitState","HasFollower")=>false,
                ("UnitDataExtensions","GetMaxHealth")=>MaxHealth((Unit)args[0]!),
                _=>throw new Exception("Unknown fixture call "+method)
            };
        }
        static int MaxHealth(Unit unit)=>unit.passengerUnit is not null&&!unit.UnitData.unitAbilities.Contains(39)?MaxHealth(unit.passengerUnit):unit.UnitData.health+50*unit.promotionLevel;
        static bool TryUnit(State state,object?[] args){if(state.LookupOverride is not null){args[1]=state.LookupOverride;return true;}var graph=NativeLedgerState.Graph(state);bool found=graph.TryGetValue(Convert.ToUInt32(args[0]),out var body);args[1]=body?.Unit;return found;}
        static bool TryPlayer(State state,object?[] args){args[1]=Convert.ToByte(args[0])==state.Player.Id?state.Player:state.OtherPlayer?.Id==Convert.ToByte(args[0])?state.OtherPlayer:null;return args[1] is not null;}
    }
    internal static class RecruitmentHooks
    {
        internal const string GLD="Unused";
        internal static Dictionary<string,RecruitmentForm> Forms=new();
        internal static Dictionary<string,Data> Profiles=new();
        internal static void Initialize(){foreach(string f in RecruitmentCatalog.CarrierForms){var id=RecruitmentCatalog.Variant(f);Forms[id]=new(f,id,f,10,1,0,0,false);Profiles[f]=new(){type=f,health=f is "giant" or "crab"?400:f=="gaami"?300:f is "babydragon" or "pirate"?150:f=="firedragon"?200:100,growthRate=NativeApparentGrowth.IsGrowing(f)?NativeApparentGrowth.Duration(f):0,unitAbilities=f is "transportship" or "bombership" or "juggernaut" or "cloak_boat"?new(){11,38}:RecruitmentCatalog.NavalCopyOnly.Contains(f)?new(){11}:new()};Profiles[id]=new(){type=id,health=10};}}
        internal static Data Data(object state,string form)=>Profiles[form];
    }
    internal static class NativeGameStateEnvelope
    {
        internal sealed class Snapshot{internal long Revision{get;}readonly byte[] Payload;internal Snapshot(long revision,byte[] bytes){Revision=revision;Payload=(byte[])bytes.Clone();}internal byte[] Bytes()=>(byte[])Payload.Clone();}
    }
    internal static class NativeFreshSession {internal static bool Enabled=>false;}
    internal static class NativeRegroupSession {internal static bool Production=true;internal static bool Enabled=>Production;}
    internal static class NativeEnvelopeProbe {internal static bool ValidatePayload(byte[] payload)=>false;}
}
