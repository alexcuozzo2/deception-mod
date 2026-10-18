using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Deception.Core
{
    // Private facts only. Native health/body/owner/home/action flags remain in
    // GameState. Vessel and passenger appearances are independent snapshots;
    // Transport binds the native cargo without storing counterfeit body facts.
    public enum LedgerActualKind:byte { Genuine=0,Mimic=1 }
    public enum PaidInvestmentKind:byte { UnknownLegacy=0,Known=1 }
    public enum ApparentConcealment:byte { Visible=0,Hidden=1 }
    public sealed record LedgerVisualProfile(string Tribe,string Skin);
    public sealed record LedgerAppearance(string Form,LedgerVisualProfile TribeVisual,LedgerVisualProfile ClimateVisual,LedgerVisualProfile BirthVisual,
        int SourceColor,ushort Health,ushort Maximum,
        bool Veteran,ApparentConcealment Concealment,int GrowthTurns,string[] Effects,long GrowthDueTurn=-1,LedgerAppearance? Passenger=null);
    public sealed record LedgerTransport(uint PassengerNativeId);
    public sealed record LedgerSight(int X,int Y);
    public sealed record LedgerRogue(byte Owner,byte Commander,uint AcquiredTurn,LedgerSight[]? Sight=null,LedgerSight[]? PendingSight=null);
    public sealed record LedgerAcquisition(byte Player,uint Turn);
    public sealed record LedgerBlackout(byte Player,int X,int Y);
    public sealed record LedgerDiscovery(byte Player,int X,int Y);
    // X/Y bind the original supporting city. UnitId is the persistent logical
    // unit identity, so transport wrappers and appearance exchanges cannot
    // transfer a lien. UnitId=0 retains historical city-only save/receipt data.
    public sealed record LedgerBlackmail(byte Blackmailer,byte Victim,int X,int Y,uint CreatedTurn,long LastPaidTurn=-1,long LastCollectedTurn=-1,
        ulong UnitId=0,int Layers=1,long LastAppliedTurn=-1);
    public sealed record LedgerCityReceipt(byte Actor,int X,int Y);
    public sealed record LedgerUnit(ulong LogicalId,uint NativeId,bool Alive,LedgerActualKind Kind,
        bool Independent,LedgerAppearance? Appearance,PaidInvestmentKind InvestmentKind,int PaidStars,LedgerTransport? Transport=null,
        LedgerRogue? Rogue=null,LedgerAcquisition[]? Acquisitions=null,bool AmphibiousMimic=false,bool MimicWaterForm=false);
    public sealed record LedgerReceipt(ulong Sequence,Guid Operation,byte[] Fingerprint,long BeforeRevision,
        long AfterRevision,ulong[] Affected,LedgerCityReceipt? City=null);
    public sealed record NativeIdentityLedger(Guid Epoch,ulong NextLogicalId,ulong NextSequence,
        LedgerUnit[] Units,LedgerReceipt[] Receipts,LedgerBlackout[]? Blackouts=null,LedgerDiscovery[]? Discoveries=null,LedgerBlackmail[]? Blackmails=null,string? OnlineContract=null);

    public static class NativeIdentityLedgerCodec
    {
        public const uint Magic=0x4C504344; // DCPL, independent of the outer frame.
        public const ushort Schema=9;
        public const int MaxUnits=8192,MaxReceipts=2048,MaxEffects=16,MaxAffected=1024,MaxText=64;
        static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
        static bool Id(string? value)=>value is not null&&value.Length is >0 and <=MaxText&&
            value.All(c=>c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-' or '.');
        static bool Defined<T>(T value)where T:struct,Enum=>Enum.IsDefined(typeof(T),value);
        public static void Validate(NativeIdentityLedger ledger,long revision)
        {
            ArgumentNullException.ThrowIfNull(ledger);
            Require(revision>=0&&ledger.Epoch!=Guid.Empty&&ledger.NextLogicalId>0&&ledger.NextSequence>0,"Invalid private identity ledger header.");
            Require(ledger.OnlineContract is null||(ledger.OnlineContract.Length==64&&ledger.OnlineContract.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f')),
                "Invalid online compatibility contract.");
            Require(ledger.Units is not null&&ledger.Units.Length<=MaxUnits&&ledger.Receipts is not null&&ledger.Receipts.Length<=MaxReceipts,"Private ledger count bound exceeded.");
            var logical=new HashSet<ulong>();var physical=new HashSet<uint>();
            ulong previous=0;
            foreach(var row in ledger.Units)
            {
                Require(row is not null&&row.LogicalId>previous&&row.LogicalId<ledger.NextLogicalId,"Noncanonical, duplicate or unallocated logical identity.");
                previous=row.LogicalId;logical.Add(row.LogicalId);
                Require(Defined(row.Kind)&&Defined(row.InvestmentKind),"Unknown native ledger identity or investment kind.");
                Require(!row.AmphibiousMimic||row.Kind==LedgerActualKind.Mimic,"Only a genuine Mimic identity may own an amphibious profile.");
                Require(!row.MimicWaterForm||row.Kind==LedgerActualKind.Mimic&&!row.AmphibiousMimic,"Only an ordinary Mimic may retain its boat form.");
                Require(row.PaidStars>=0&&(row.InvestmentKind==PaidInvestmentKind.Known||row.PaidStars==0),"Unknown investment cannot counterfeit a paid value.");
                Require(!row.Alive||physical.Add(row.NativeId),"Duplicate living native identity binding.");
                Require(row.Independent==(row.Appearance is not null),"Independent appearance and payload disagree.");
                Require(row.Alive||!row.Independent,"Retired logical identity cannot own a live appearance.");
                Require(!row.Alive||row.Kind!=LedgerActualKind.Mimic||row.Independent,"A living Mimic must retain its independent appearance.");
                Require(row.Alive||row.Transport is null,"Retired identity cannot retain a native passenger.");
                Require(row.Alive||row.Rogue is null,"Retired identity cannot retain Rogue authority.");
                if(row.Rogue is LedgerRogue rogue)
                {
                    Require(rogue.Owner>0&&rogue.Commander>0&&rogue.Owner!=rogue.Commander,"Invalid Rogue ownership.");
                    foreach(var sight in new[]{rogue.Sight??Array.Empty<LedgerSight>(),rogue.PendingSight??Array.Empty<LedgerSight>()})
                    Require(sight.Length<=8192&&sight.All(p=>p.X is >=0 and <=4095&&p.Y is >=0 and <=4095)&&
                        sight.SequenceEqual(sight.Distinct().OrderBy(p=>p.X).ThenBy(p=>p.Y)),"Invalid retained Rogue sight.");
                }
                var acquisitions=row.Acquisitions??Array.Empty<LedgerAcquisition>();
                Require(acquisitions.Length<=255&&acquisitions.All(x=>x.Player>0)&&
                    acquisitions.Select(x=>x.Player).SequenceEqual(acquisitions.Select(x=>x.Player).Distinct().OrderBy(x=>x)),"Invalid acquisition history.");
                if(row.Transport is LedgerTransport transport)
                    Require(transport.PassengerNativeId!=row.NativeId&&physical.Add(transport.PassengerNativeId),"Duplicate native passenger identity binding.");
                if(row.Appearance is LedgerAppearance appearance)ValidateAppearance(appearance,0);
            }
            var operations=new HashSet<Guid>();ulong sequence=0;long lastRevision=-1;
            foreach(var receipt in ledger.Receipts)
            {
                Require(receipt is not null&&receipt.Sequence==sequence+1&&receipt.Sequence<ledger.NextSequence&&
                    receipt.Operation!=Guid.Empty&&operations.Add(receipt.Operation),"Invalid or duplicate private command receipt.");
                Require(receipt.Fingerprint is not null&&receipt.Fingerprint.Length==32,"Invalid private command fingerprint.");
                Require(receipt.BeforeRevision>=0&&receipt.BeforeRevision<long.MaxValue&&receipt.BeforeRevision>=lastRevision&&receipt.AfterRevision==receipt.BeforeRevision+1&&
                    receipt.AfterRevision<=revision,"Invalid private command revision chain.");
                Require(receipt.Affected is not null&&receipt.Affected.Length<=MaxAffected&&
                    receipt.Affected.SequenceEqual(receipt.Affected.OrderBy(x=>x))&&receipt.Affected.Distinct().Count()==receipt.Affected.Length&&
                    receipt.Affected.All(logical.Contains),"Invalid affected logical identities.");
                Require(receipt.City is LedgerCityReceipt city?
                    receipt.Affected.Length==0&&city.Actor is >0 and <255&&city.X is >=0 and <4096&&city.Y is >=0 and <4096:
                    receipt.Affected.Length>0,"Invalid unit/city receipt domain.");
                sequence=receipt.Sequence;lastRevision=receipt.AfterRevision;
            }
            Require(sequence+1==ledger.NextSequence,"Private command history is incomplete.");
            var blackouts=ledger.Blackouts??Array.Empty<LedgerBlackout>();
            Require(blackouts.Length<=4096*16&&blackouts.All(x=>x.Player>0&&x.X>=0&&x.Y>=0&&x.X<4096&&x.Y<4096)&&
                blackouts.SequenceEqual(blackouts.Distinct().OrderBy(x=>x.Player).ThenBy(x=>x.X).ThenBy(x=>x.Y)),"Invalid Blackout mask.");
            var discoveries=ledger.Discoveries??Array.Empty<LedgerDiscovery>();
            Require(discoveries.Length<=4096*16&&discoveries.All(x=>x.Player>0&&x.X>=0&&x.Y>=0&&x.X<4096&&x.Y<4096)&&
                discoveries.SequenceEqual(discoveries.Distinct().OrderBy(x=>x.Player).ThenBy(x=>x.X).ThenBy(x=>x.Y)),"Invalid authorized discovery set.");
            var blackmails=ledger.Blackmails??Array.Empty<LedgerBlackmail>();
            Require(blackmails.Length<=4096*16&&blackmails.All(x=>x is not null&&x.Blackmailer is >0 and <255&&x.Victim is >0 and <255&&
                x.Blackmailer!=x.Victim&&x.X is >=0 and <4096&&x.Y is >=0 and <4096&&x.LastPaidTurn is >=-1 and <=uint.MaxValue&&
                (x.LastPaidTurn==-1||x.LastPaidTurn>=x.CreatedTurn)&&x.LastCollectedTurn is >=-1 and <=uint.MaxValue&&
                (x.LastCollectedTurn==-1||x.LastCollectedTurn>=x.CreatedTurn)&&x.Layers is >0 and <=65535&&
                (x.UnitId==0?x.Layers==1&&x.LastAppliedTurn==-1:
                    logical.Contains(x.UnitId)&&x.LastAppliedTurn>=x.CreatedTurn&&x.LastAppliedTurn<=uint.MaxValue))&&
                blackmails.SequenceEqual(blackmails.OrderBy(x=>x.X).ThenBy(x=>x.Y).ThenBy(x=>x.Blackmailer).ThenBy(x=>x.UnitId))&&
                blackmails.Select(x=>(x.X,x.Y,x.Blackmailer,x.UnitId)).Distinct().Count()==blackmails.Length&&
                blackmails.Where(x=>x.UnitId>0).Select(x=>(x.Blackmailer,x.UnitId)).Distinct().Count()==blackmails.Count(x=>x.UnitId>0)&&
                blackmails.GroupBy(x=>(x.X,x.Y)).All(g=>g.Select(x=>x.Victim).Distinct().Count()==1)&&
                blackmails.Where(x=>x.UnitId>0).GroupBy(x=>x.UnitId).All(g=>g.Select(x=>(x.X,x.Y,x.Victim)).Distinct().Count()==1),"Invalid unit Blackmail records.");
        }
        static void ValidateAppearance(LedgerAppearance appearance,int depth)
        {
            Require(depth<=1,"Nested apparent vessels are unsupported.");
            Require(Id(appearance.Form)&&new[]{appearance.TribeVisual,appearance.ClimateVisual,appearance.BirthVisual}.All(p=>p is not null&&Id(p.Tribe)&&Id(p.Skin)),"Invalid appearance profile identifier.");
            Require(appearance.Health>0&&appearance.Maximum>=appearance.Health&&Defined(appearance.Concealment)&&
                appearance.GrowthTurns is >=-1 and <=65535,"Invalid apparent health, concealment or growth phase.");
            Require(appearance.GrowthDueTurn is >=-1 and <=uint.MaxValue&&
                (appearance.GrowthTurns==-1)==(appearance.GrowthDueTurn==-1),"Growth phase and scheduled turn disagree.");
            Require(appearance.Effects is not null&&appearance.Effects.Length<=MaxEffects&&appearance.Effects.All(Id)&&
                appearance.Effects.SequenceEqual(appearance.Effects.OrderBy(x=>x,StringComparer.Ordinal))&&
                appearance.Effects.Distinct(StringComparer.Ordinal).Count()==appearance.Effects.Length,"Noncanonical apparent effects.");
            if(appearance.Passenger is LedgerAppearance passenger)
            {
                Require(depth==0&&RecruitmentCatalog.NavalCopyOnly.Contains(appearance.Form,StringComparer.Ordinal)&&
                    !RecruitmentCatalog.NavalCopyOnly.Contains(passenger.Form,StringComparer.Ordinal),"Invalid apparent passenger profile.");
                ValidateAppearance(passenger,depth+1);
            }
        }
        public static byte[] Encode(NativeIdentityLedger ledger,long revision)=>EncodeSchema(ledger,revision,Schema);
        // Existing non-growing command digests must retain their exact schema1
        // bytes even after saved state migrates to schema2.
        internal static byte[] EncodeLegacyFingerprint(NativeIdentityLedger ledger,long revision)
        {
            Require(ledger.Units.All(u=>u.Transport is null&&(u.Appearance is null||(u.Appearance.GrowthDueTurn==-1&&u.Appearance.Passenger is null))),"Timed appearance has no legacy fingerprint.");
            return EncodeSchema(ledger,revision,1);
        }
        internal static byte[] EncodeTimedFingerprint(NativeIdentityLedger ledger,long revision)
        {
            Require(ledger.Units.All(u=>u.Transport is null&&u.Appearance?.Passenger is null),"Transport appearance has no schema2 fingerprint.");
            return EncodeSchema(ledger,revision,2);
        }
        internal static byte[] EncodeTransportFingerprint(NativeIdentityLedger ledger,long revision)=>EncodeSchema(ledger,revision,3);
        static byte[] EncodeSchema(NativeIdentityLedger ledger,long revision,ushort schema)
        {
            Validate(ledger,revision);
            Require(schema>=5||((ledger.Blackmails?.Length??0)==0&&ledger.Receipts.All(r=>r.City is null)),"City history cannot use a legacy ledger schema.");
            Require(schema>=7||ledger.Units.All(r=>!r.AmphibiousMimic&&!r.MimicWaterForm),"Amphibious body identity cannot use a legacy ledger schema.");
            Require(schema>=9||!(ledger.Blackmails??Array.Empty<LedgerBlackmail>()).Any(r=>r.UnitId>0),"Unit Blackmail cannot use a legacy ledger schema.");
            using var stream=new MemoryStream();using var writer=new BinaryWriter(stream,Encoding.UTF8,true);
            writer.Write(Magic);writer.Write(schema);writer.Write((ushort)0);writer.Write(ledger.Epoch.ToByteArray());
            writer.Write(ledger.NextLogicalId);writer.Write(ledger.NextSequence);writer.Write((ushort)ledger.Units.Length);writer.Write((ushort)ledger.Receipts.Length);
            foreach(var row in ledger.Units)
            {
                writer.Write(row.LogicalId);writer.Write(row.NativeId);writer.Write(row.Alive);writer.Write((byte)row.Kind);
                writer.Write(row.Independent);writer.Write((byte)row.InvestmentKind);writer.Write(row.PaidStars);
                if(row.Appearance is LedgerAppearance a)WriteAppearance(writer,a,schema);
                if(schema>=3){writer.Write(row.Transport is not null);if(row.Transport is LedgerTransport transport)writer.Write(transport.PassengerNativeId);}
                if(schema>=4)
                {
                    writer.Write(row.Rogue is not null);
                    if(row.Rogue is LedgerRogue rogue)
                    {
                        writer.Write(rogue.Owner);writer.Write(rogue.Commander);writer.Write(rogue.AcquiredTurn);
                        foreach(var sight in new[]{rogue.Sight??Array.Empty<LedgerSight>(),rogue.PendingSight??Array.Empty<LedgerSight>()})
                        {writer.Write((ushort)sight.Length);foreach(var cell in sight){writer.Write((ushort)cell.X);writer.Write((ushort)cell.Y);}}
                    }
                    var acquisitions=row.Acquisitions??Array.Empty<LedgerAcquisition>();writer.Write((byte)acquisitions.Length);
                    foreach(var acquisition in acquisitions){writer.Write(acquisition.Player);writer.Write(acquisition.Turn);}
                }
                if(schema>=7){writer.Write(row.AmphibiousMimic);writer.Write(row.MimicWaterForm);}
            }
            foreach(var receipt in ledger.Receipts)
            {
                writer.Write(receipt.Sequence);writer.Write(receipt.Operation.ToByteArray());writer.Write(receipt.Fingerprint);
                writer.Write(receipt.BeforeRevision);writer.Write(receipt.AfterRevision);writer.Write((ushort)receipt.Affected.Length);
                foreach(ulong id in receipt.Affected)writer.Write(id);
                if(schema>=5)
                {writer.Write(receipt.City is not null);if(receipt.City is LedgerCityReceipt city){writer.Write(city.Actor);writer.Write(city.X);writer.Write(city.Y);}}
            }
            if(schema>=4)
            {
                var masks=ledger.Blackouts??Array.Empty<LedgerBlackout>();writer.Write(masks.Length);
                foreach(var mask in masks){writer.Write(mask.Player);writer.Write(mask.X);writer.Write(mask.Y);}
                var discoveries=ledger.Discoveries??Array.Empty<LedgerDiscovery>();writer.Write(discoveries.Length);
                foreach(var item in discoveries){writer.Write(item.Player);writer.Write(item.X);writer.Write(item.Y);}
            }
            if(schema>=5)
            {
                var blackmails=ledger.Blackmails??Array.Empty<LedgerBlackmail>();writer.Write(blackmails.Length);
                foreach(var item in blackmails){writer.Write(item.Blackmailer);writer.Write(item.Victim);writer.Write(item.X);writer.Write(item.Y);writer.Write(item.CreatedTurn);writer.Write(item.LastPaidTurn);if(schema>=6)writer.Write(item.LastCollectedTurn);
                    if(schema>=9){writer.Write(item.UnitId);writer.Write(item.Layers);writer.Write(item.LastAppliedTurn);}}
            }
            if(schema>=8){writer.Write(ledger.OnlineContract is not null);if(ledger.OnlineContract is string contract)writer.Write(Convert.FromHexString(contract));}
            writer.Flush();Require(stream.Length<=NativeStateFrame.MaxExtensionBytes,"Private ledger byte bound exceeded.");return stream.ToArray();
        }
        static void WriteAppearance(BinaryWriter writer,LedgerAppearance a,ushort schema)
        {
            Text(writer,a.Form);
            foreach(var profile in new[]{a.TribeVisual,a.ClimateVisual,a.BirthVisual}){Text(writer,profile.Tribe);Text(writer,profile.Skin);}
            writer.Write(a.SourceColor);writer.Write(a.Health);writer.Write(a.Maximum);
            writer.Write(a.Veteran);writer.Write((byte)a.Concealment);writer.Write(a.GrowthTurns);writer.Write((byte)a.Effects.Length);
            foreach(string effect in a.Effects)Text(writer,effect);
            if(schema>=2)writer.Write(a.GrowthDueTurn);
            if(schema>=3){writer.Write(a.Passenger is not null);if(a.Passenger is LedgerAppearance passenger)WriteAppearance(writer,passenger,schema);}
        }
        static LedgerAppearance ReadAppearance(BinaryReader reader,ushort schema,int depth)
        {
            Require(depth<=1,"Nested apparent vessels are unsupported.");
            string form=Text(reader);
            var tribe=new LedgerVisualProfile(Text(reader),Text(reader));var climate=new LedgerVisualProfile(Text(reader),Text(reader));var birth=new LedgerVisualProfile(Text(reader),Text(reader));
            int color=reader.ReadInt32();ushort health=reader.ReadUInt16(),maximum=reader.ReadUInt16();
            bool veteran=Boolean(reader);var concealment=(ApparentConcealment)reader.ReadByte();int growth=reader.ReadInt32();
            int effects=reader.ReadByte();Require(effects<=MaxEffects,"Apparent effect bound exceeded.");var names=new string[effects];
            for(int j=0;j<effects;j++)names[j]=Text(reader);
            long due=schema>=2?reader.ReadInt64():-1;
            Require(schema>=2||growth==-1,"Legacy growth has no reconstructable scheduled turn.");
            LedgerAppearance? passenger=schema>=3&&Boolean(reader)?ReadAppearance(reader,schema,depth+1):null;
            return new(form,tribe,climate,birth,color,health,maximum,veteran,concealment,growth,names,due,passenger);
        }
        static void Text(BinaryWriter writer,string value){writer.Write((byte)value.Length);writer.Write(Encoding.ASCII.GetBytes(value));}
        static byte[] Exact(BinaryReader reader,int count)
        {var bytes=reader.ReadBytes(count);Require(bytes.Length==count,"Truncated private ledger.");return bytes;}
        static bool Boolean(BinaryReader reader){byte value=reader.ReadByte();Require(value<=1,"Noncanonical private Boolean.");return value==1;}
        static string Text(BinaryReader reader)
        {int length=reader.ReadByte();Require(length is >0 and <=MaxText,"Private identifier length outside bounds.");var bytes=Exact(reader,length);Require(bytes.All(x=>x<128),"Non-ASCII private identifier.");return Encoding.ASCII.GetString(bytes);}
        public static NativeIdentityLedger Decode(byte[] bytes,long revision)
        {
            ArgumentNullException.ThrowIfNull(bytes);Require(bytes.Length<=NativeStateFrame.MaxExtensionBytes,"Private ledger byte bound exceeded.");
            using var stream=new MemoryStream(bytes,false);using var reader=new BinaryReader(stream,Encoding.UTF8,true);
            try
            {
                Require(reader.ReadUInt32()==Magic,"Unknown private identity ledger magic.");
                ushort schema=reader.ReadUInt16();
                Require((schema is >=1 and <=Schema)&&reader.ReadUInt16()==0,"Unknown private identity ledger schema or flags.");
                Guid epoch=new Guid(Exact(reader,16));ulong nextLogical=reader.ReadUInt64(),nextSequence=reader.ReadUInt64();
                int count=reader.ReadUInt16(),receipts=reader.ReadUInt16();Require(count<=MaxUnits&&receipts<=MaxReceipts,"Private ledger count bound exceeded.");
                var units=new LedgerUnit[count];
                for(int i=0;i<count;i++)
                {
                    ulong logical=reader.ReadUInt64();uint physical=reader.ReadUInt32();bool alive=Boolean(reader);
                    var kind=(LedgerActualKind)reader.ReadByte();bool independent=Boolean(reader);var investment=(PaidInvestmentKind)reader.ReadByte();int stars=reader.ReadInt32();
                    LedgerAppearance? appearance=independent?ReadAppearance(reader,schema,0):null;
                    LedgerTransport? transport=schema>=3&&Boolean(reader)?new LedgerTransport(reader.ReadUInt32()):null;
                    LedgerRogue? rogue=null;LedgerAcquisition[]? acquisitions=null;
                    if(schema>=4)
                    {
                        if(Boolean(reader))
                        {
                            byte owner=reader.ReadByte(),commander=reader.ReadByte();uint turn=reader.ReadUInt32();int cells=reader.ReadUInt16();
                            Require(cells<=8192,"Retained Rogue sight exceeds its bound.");var sight=new LedgerSight[cells];
                            for(int j=0;j<cells;j++)sight[j]=new(reader.ReadUInt16(),reader.ReadUInt16());
                            int pendingCells=reader.ReadUInt16();Require(pendingCells<=8192,"Pending Rogue sight exceeds its bound.");var pendingSight=new LedgerSight[pendingCells];
                            for(int j=0;j<pendingCells;j++)pendingSight[j]=new(reader.ReadUInt16(),reader.ReadUInt16());
                            rogue=new(owner,commander,turn,cells==0?null:sight,pendingCells==0?null:pendingSight);
                        }
                        int n=reader.ReadByte();acquisitions=new LedgerAcquisition[n];
                        for(int j=0;j<n;j++)acquisitions[j]=new(reader.ReadByte(),reader.ReadUInt32());
                    }
                    bool amphibious=schema>=7&&Boolean(reader),waterForm=schema>=7&&Boolean(reader);
                    units[i]=new(logical,physical,alive,kind,independent,appearance,investment,stars,transport,rogue,acquisitions,amphibious,waterForm);
                }
                var history=new LedgerReceipt[receipts];
                for(int i=0;i<receipts;i++)
                {
                    ulong sequence=reader.ReadUInt64();Guid operation=new Guid(Exact(reader,16));byte[] fingerprint=Exact(reader,32);
                    long before=reader.ReadInt64(),after=reader.ReadInt64();int affected=reader.ReadUInt16();Require(affected<=MaxAffected&&(schema>=5||affected>0),"Affected identity bound exceeded.");
                    var ids=new ulong[affected];for(int j=0;j<affected;j++)ids[j]=reader.ReadUInt64();
                    LedgerCityReceipt? city=schema>=5&&Boolean(reader)?new(reader.ReadByte(),reader.ReadInt32(),reader.ReadInt32()):null;
                    history[i]=new(sequence,operation,fingerprint,before,after,ids,city);
                }
                LedgerBlackout[]? blackouts=null;LedgerDiscovery[]? discoveries=null;
                if(schema>=4)
                {
                    int n=reader.ReadInt32();Require(n>=0&&n<=4096*16,"Blackout count exceeds bound.");blackouts=new LedgerBlackout[n];
                    for(int i=0;i<n;i++)blackouts[i]=new(reader.ReadByte(),reader.ReadInt32(),reader.ReadInt32());
                    n=reader.ReadInt32();Require(n>=0&&n<=4096*16,"Discovery count exceeds bound.");discoveries=new LedgerDiscovery[n];
                    for(int i=0;i<n;i++)discoveries[i]=new(reader.ReadByte(),reader.ReadInt32(),reader.ReadInt32());
                }
                LedgerBlackmail[]? blackmails=null;
                if(schema>=5)
                {
                    int n=reader.ReadInt32();Require(n>=0&&n<=4096*16,"Blackmail count exceeds bound.");blackmails=new LedgerBlackmail[n];
                    for(int i=0;i<n;i++)
                    {
                        byte actor=reader.ReadByte(),victim=reader.ReadByte();int x=reader.ReadInt32(),y=reader.ReadInt32();uint created=reader.ReadUInt32();long paid=reader.ReadInt64();
                        // Schema5 credited the actor at the victim's payment.
                        // Preserve that cycle as already collected on migration.
                        long collected=schema>=6?reader.ReadInt64():paid;
                        ulong unit=schema>=9?reader.ReadUInt64():0;int layers=schema>=9?reader.ReadInt32():1;long applied=schema>=9?reader.ReadInt64():-1;
                        blackmails[i]=new(actor,victim,x,y,created,paid,collected,unit,layers,applied);
                    }
                }
                string? contract=schema>=8&&Boolean(reader)?Convert.ToHexString(Exact(reader,32)).ToLowerInvariant():null;
                Require(stream.Position==stream.Length,"Trailing private ledger bytes.");
                var ledger=new NativeIdentityLedger(epoch,nextLogical,nextSequence,units,history,blackouts,discoveries,blackmails,contract);Validate(ledger,revision);return ledger;
            }
            catch(EndOfStreamException e){throw new InvalidDataException("Truncated private identity ledger.",e);}
        }
    }
}
