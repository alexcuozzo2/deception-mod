// Deception DF-1, sections 1-4, 15. Model units use DISPLAY HP, not native scaled HP.
// No Unity/Polytopia/BepInEx dependency. This is the initial rules kernel,
// not a claim that the native save/visibility/command adapters exist yet.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deception.Core
{
    public enum ActualKind { Genuine, Mimic, Spy }
    public enum BodyStructure { SingleBody, CentipedeHead, CentipedeSegment }
    public enum PanelMode { Actual, Appearance }
    public enum RecruitmentKind { OrdinaryCity, SpecialCity, SuperUnit, Ship, SpawnOnly, BareMimic }
    public enum ResourceKind { Fruit, Fish, Animal, Starfish, Other }
    public enum ExchangeKind { Regroup, Disguise }
    public sealed class RuleViolation : InvalidOperationException
    { public RuleViolation(string message) : base(message) { } }

    public readonly record struct Tile(int X, int Y)
    { public string Key => X + "," + Y; }
    public sealed record FormProfile(string Id, RecruitmentKind Recruitment,
        string RequiredTechnology, int MaxHp, int Movement, int Sight,
        int Attack, int Defence, bool HasDamagingWeapon,
        string TerrainProfile = "none", string ConcealmentProfile = "visible",
        BodyStructure Structure = BodyStructure.SingleBody);

    // All values here must come from the native adapter in production. Synthetic
    // fixtures implement this interface in tests; those are NOT native measurements.
    public interface INativeRules
    {
        string EvidenceId { get; }
        FormProfile Profile(string formId);
        bool HasFormAdapter(string formId);
        TurnAllowance RecruitmentAllowance(int player, long playerTurn);
        bool CanExchangeOccupy(UnitRecord unit, Tile destination, AppearanceRecord appearance);
        int Distance(Tile a, Tile b);
        bool CanHarvest(PlayerRecord player, ResourceRecord resource);
        int HarvestPopulation(PlayerRecord player, ResourceRecord resource);
    }

    public sealed class TurnAllowance
    {
        public int Player { get; set; }
        public long PlayerTurn { get; set; }
        public bool MovementSpent { get; set; }
        public bool ActionSpent { get; set; }
        // Native follow-ups are explicit tokens, never invented by changing mode/type.
        public List<string> NativeFollowUps { get; set; } = new();
        public bool BothUnused => !MovementSpent && !ActionSpent;
        public TurnAllowance Copy() => new() { Player=Player, PlayerTurn=PlayerTurn,
            MovementSpent=MovementSpent, ActionSpent=ActionSpent,
            NativeFollowUps=new List<string>(NativeFollowUps) };
        public void EndTurn() { MovementSpent=true; ActionSpent=true; NativeFollowUps.Clear(); }
    }
    public sealed class AppearanceRecord
    {
        public string Body { get; set; } = "";
        // A costume has a single body only: no apparent segment graph exists.
        public BodyStructure Structure { get; set; } = BodyStructure.SingleBody;
        public string? Vessel { get; set; }
        public string? Passenger { get; set; }
        public string Skin { get; set; } = "";
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public string Concealment { get; set; } = "visible";
        public int? GrowthTurnsRemaining { get; set; }
        public List<string> Statuses { get; set; } = new();
        public List<string> PublicTraits { get; set; } = new();
        public string OperativeForm => Vessel ?? Body;
        public AppearanceRecord Copy() => new() { Body=Body, Structure=Structure, Vessel=Vessel,
            Passenger=Passenger, Skin=Skin, Hp=Hp, MaxHp=MaxHp,
            Concealment=Concealment, GrowthTurnsRemaining=GrowthTurnsRemaining,
            Statuses=new List<string>(Statuses), PublicTraits=new List<string>(PublicTraits) };
    }
    public sealed class UnitRecord
    {
        public long LogicalId { get; set; }
        public ActualKind Kind { get; set; }
        public string ActualBody { get; set; } = "";
        // Actual native role; never derived from disguise or apparent artwork.
        public BodyStructure Structure { get; set; } = BodyStructure.SingleBody;
        public string ActualSkin { get; set; } = "";
        public string? ActualVessel { get; set; }
        public string? ActualPassenger { get; set; }
        public int ActualHp { get; set; }
        public int ActualMaxHp { get; set; }
        public bool Alive { get; set; } = true;
        public List<string> ActualStatuses { get; set; } = new();
        public int ActualInvestment { get; set; }
        public AppearanceRecord Appearance { get; set; } = new();
        public bool IndependentAppearance { get; set; }
        public int PermanentOwner { get; set; }
        public int? Commander { get; set; }
        public bool Rogue { get; set; }
        public int? ReturnOwner { get; set; }
        public string HomeCity { get; set; } = "";
        public Tile Position { get; set; }
        public TurnAllowance Allowance { get; set; } = new();
        public Dictionary<int,long> LastAcquiredTurn { get; set; } = new();
        public string ActualForm => ActualVessel ?? ActualBody;
    }
    public sealed class PlayerRecord
    {
        public int Id { get; set; }
        // Classification only; A01 permits research/recruitment for special tribes too.
        public bool OrdinaryTribe { get; set; }
        public string TribeId { get; set; } = "";
        // Native own-roster eligible forms, including locked forms and own super-unit.
        public HashSet<string> NativeRosterForms { get; set; } = new(StringComparer.Ordinal);
        public int Stars { get; set; }
        public long Turn { get; set; }
        public HashSet<string> Technologies { get; set; } = new(StringComparer.Ordinal);
        public HashSet<string> UnlockedRecruitmentForms { get; set; } = new(StringComparer.Ordinal);
        public HashSet<int> Treaties { get; set; } = new();
    }
    public sealed class CityRecord
    {
        public string Id { get; set; } = "";
        public int Owner { get; set; }
        public Tile Position { get; set; }
        public int Population { get; set; }
        public int SupportUsed { get; set; }
        public int SupportCapacity { get; set; }
        public bool RecruitmentAllowed { get; set; } = true;
    }
    public sealed class ResourceRecord
    {
        public Tile Position { get; set; }
        public ResourceKind Kind { get; set; }
        public string HarvestTechnology { get; set; } = "";
        public int? TerritoryOwner { get; set; }
    }
    public sealed class ObservationMask
    {
        public HashSet<string> AccessibleTiles { get; set; } = new(StringComparer.Ordinal);
        public HashSet<long> DetectedUnits { get; set; } = new();
    }
    public sealed class PopulationNotice
    {
        public string CommandId { get; set; } = "";
        public string City { get; set; } = "";
        public int Owner { get; set; }
        public bool DeferredToOwnerTurn { get; set; }
        // Native population/level-up queue integration is deliberately NOT in M0.
    }
    public sealed class World
    {
        public int SchemaVersion { get; set; } = 2;
        public string AmendmentId { get; set; } = "A01";
        public string DesignId { get; set; } = "DECEPTION-DF-1";
        public long Revision { get; set; }
        public long NextLogicalId { get; set; } = 1;
        public Dictionary<long,UnitRecord> Units { get; set; } = new();
        // Authoritative attachment snapshot: body logical ID -> predecessor logical ID.
        // Coordinates, ownership and visibility are intentionally not consulted.
        public Dictionary<long,long> SegmentParents { get; set; } = new();
        public Dictionary<int,PlayerRecord> Players { get; set; } = new();
        public Dictionary<string,CityRecord> Cities { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string,ResourceRecord> Resources { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<int,ObservationMask> Masks { get; set; } = new();
        // Payload fingerprints make retried commits idempotent, but conflicting
        // re-use of the same command ID is rejected rather than silently accepted.
        public Dictionary<string,string> AppliedCommands { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string,long?> CommandUnits { get; set; } = new(StringComparer.Ordinal);
        public List<PopulationNotice> PopulationNotices { get; set; } = new();
    }
    public static class Codec
    {
        public static readonly JsonSerializerOptions Options = new() {
            PropertyNamingPolicy=JsonNamingPolicy.CamelCase,
            Converters={new JsonStringEnumConverter()}, WriteIndented=true
        };
        public static string Encode(World world) { Validate(world); return JsonSerializer.Serialize(world,Options); }
        public static World Decode(string json)
        {
            var world=JsonSerializer.Deserialize<World>(json,Options) ?? throw new RuleViolation("Empty state");
            Validate(world); return world;
        }
        public static World Copy(World world) => Decode(Encode(world));
        public static void Validate(World w)
        {
            if(w.SchemaVersion!=2 || w.DesignId!="DECEPTION-DF-1" || w.AmendmentId!="A01") throw new RuleViolation("Unsupported state schema/design");
            if(w.NextLogicalId<1 || w.Units.Keys.Any(x=>x<=0 || x>=w.NextLogicalId)) throw new RuleViolation("Invalid stable ID sequence");
            if(!w.AppliedCommands.Keys.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(w.CommandUnits.Keys.OrderBy(x=>x,StringComparer.Ordinal)))
                throw new RuleViolation("Command ledger/result mismatch");
            foreach(var pair in w.Units)
            {
                var u=pair.Value;
                if(u.LogicalId!=pair.Key) throw new RuleViolation("Unit/key identity mismatch");
                if(u.ActualHp<0 || u.ActualMaxHp<1 || u.ActualHp>u.ActualMaxHp || u.Alive!=(u.ActualHp>0))
                    throw new RuleViolation("Invalid actual HP/liveness");
                if(u.Kind==ActualKind.Mimic && (u.ActualBody!="mimic" || u.ActualMaxHp!=1 || (u.Alive && u.ActualHp!=1)))
                    throw new RuleViolation("Mimic must have one actual HP");
                if(u.Appearance is null || string.IsNullOrWhiteSpace(u.Appearance.Body) || u.Appearance.MaxHp<1 ||
                   u.Appearance.Hp<0 || u.Appearance.Hp>u.Appearance.MaxHp)
                    throw new RuleViolation("Invalid appearance snapshot");
                if(u.Kind==ActualKind.Mimic && (!u.IndependentAppearance || u.Appearance.Body=="mimic"))
                    throw new RuleViolation("Living/naked Mimic identity is not an appearance");
                if(u.Rogue != u.ReturnOwner.HasValue) throw new RuleViolation("Rogue return identity missing/stale");
                if(u.ActualInvestment<0) throw new RuleViolation("Negative paid investment");
            }
            CompoundBodies.ValidateGraph(w);
            if(w.Players.Values.Any(p=>p.Stars<0)) throw new RuleViolation("Negative currency");
            if(w.Cities.Values.Any(c=>c.SupportUsed<0 || c.SupportCapacity<0 || c.SupportUsed>c.SupportCapacity || c.Population<0))
                throw new RuleViolation("Invalid city/support state");
        }
    }

    // Renderers/AI must consume a projection, not a UnitRecord. This deliberately
    // omits logical ID, true identity, investment, home and readiness from public
    // output; the adapter supplies an observer-local transient handle separately.
    public sealed record PublicUnitView(Tile Position, string Body, string? Vessel,
        string? Passenger, string Skin, int Hp, int MaxHp, string Concealment,
        string[] Statuses, string[] PublicTraits, int Faction, bool Rogue);
    public sealed record PrivateUnitView(ActualKind Kind, string Body, string? Vessel,
        int Hp, int MaxHp, string Home, bool MimicBadge, bool CanCommand,
        TurnAllowance? Readiness);
    public sealed record UnitView(PublicUnitView Public, PrivateUnitView? Private);
    public static class Observation
    {
        public static bool Accessible(World w, int player, Tile tile) =>
            w.Masks.TryGetValue(player,out var mask) && mask.AccessibleTiles.Contains(tile.Key);
        public static UnitView? Project(World w, UnitRecord unit, int viewer)
        {
            if(!unit.Alive || !Accessible(w,viewer,unit.Position) || !w.Masks[viewer].DetectedUnits.Contains(unit.LogicalId)) return null;
            var a=CapabilityResolver.EffectiveAppearance(unit);
            var pub=new PublicUnitView(unit.Position,a.Body,a.Vessel,a.Passenger,a.Skin,a.Hp,a.MaxHp,
                a.Concealment,a.Statuses.OrderBy(x=>x,StringComparer.Ordinal).ToArray(),
                a.PublicTraits.OrderBy(x=>x,StringComparer.Ordinal).ToArray(),unit.PermanentOwner,unit.Rogue);
            bool control=unit.Commander==viewer;
            bool truth=control || (!unit.Rogue && unit.PermanentOwner==viewer) || (unit.Rogue && unit.ReturnOwner==viewer);
            PrivateUnitView? priv=truth ? new PrivateUnitView(unit.Kind,unit.ActualBody,unit.ActualVessel,
                unit.ActualHp,unit.ActualMaxHp,unit.HomeCity,control && unit.Kind==ActualKind.Mimic,
                control,control ? unit.Allowance.Copy() : null) : null;
            return new UnitView(pub,priv);
        }
        public static string Capture(World w,int viewer) => JsonSerializer.Serialize(
            w.Units.Values.OrderBy(u=>u.Position.X).ThenBy(u=>u.Position.Y)
             .Select(u=>Project(w,u,viewer)).Where(v=>v is not null).ToArray(),Codec.Options);
    }
    // UI state is not stored in World and never round-trips through a save.
    public sealed class Selection
    {
        readonly HashSet<(World World,int Viewer,long Unit)> ActualPreferences=new();
        World? ContextWorld;
        int? Viewer;
        long? PlayerTurn;
        public long? Unit { get; private set; }
        // Command mode is deliberately independent of Mimic inspection. Merely
        // looking at an apparent unit does not grant feints or hide movement.
        public PanelMode Mode { get; private set; } = PanelMode.Actual;
        public PanelMode InspectionMode { get; private set; } = PanelMode.Appearance;
        public void Reset()
        {ClearSelection();ActualPreferences.Clear();}
        public void ClearSelection()
        {Unit=null;Mode=PanelMode.Actual;InspectionMode=PanelMode.Appearance;ContextWorld=null;Viewer=null;PlayerTurn=null;}
        public bool StoredActualFor(World w,int viewer,long id)=>
            w.Units.TryGetValue(id,out var unit)&&unit.Kind==ActualKind.Mimic&&unit.Commander==viewer&&
            Observation.Project(w,unit,viewer) is not null&&ActualPreferences.Contains((w,viewer,id));
        public void Click(World w,int viewer,long id,bool choosingCommandTarget=false)
        {
            if(!w.Units.TryGetValue(id,out var unit) || unit.Commander!=viewer || Observation.Project(w,unit,viewer) is null)
            { ClearSelection(); return; }
            long turn=w.Players.TryGetValue(viewer,out var player)?player.Turn:-1;
            if(!ReferenceEquals(ContextWorld,w)||Viewer!=viewer||PlayerTurn!=turn)ClearSelection();
            if(choosingCommandTarget)return;
            ContextWorld=w;Viewer=viewer;PlayerTurn=turn;
            if(Unit!=id)
            {Unit=id;Mode=PanelMode.Actual;InspectionMode=unit.Kind!=ActualKind.Mimic||StoredActualFor(w,viewer,id)?PanelMode.Actual:PanelMode.Appearance;}
            else if(unit.Kind!=ActualKind.Mimic&&unit.IndependentAppearance)
            {Mode=Mode==PanelMode.Actual?PanelMode.Appearance:PanelMode.Actual;InspectionMode=Mode;}
        }
        public bool ToggleInspection(World w,int viewer,long id,bool choosingCommandTarget=false)
        {
            if(choosingCommandTarget)return false;
            long turn=w.Players.TryGetValue(viewer,out var player)?player.Turn:-1;
            if(!ReferenceEquals(ContextWorld,w)||Viewer!=viewer||PlayerTurn!=turn||Unit!=id||
               !w.Units.TryGetValue(id,out var unit)||unit.Kind!=ActualKind.Mimic||unit.Commander!=viewer||
               Observation.Project(w,unit,viewer) is null)
            {ClearSelection();return false;}
            var key=(w,viewer,id);
            if(!ActualPreferences.Add(key))ActualPreferences.Remove(key);
            InspectionMode=ActualPreferences.Contains(key)?PanelMode.Actual:PanelMode.Appearance;
            return true;
        }
    }
}
