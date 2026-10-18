using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Deception.Core
{
    public sealed record CommitResult(World State, bool Replayed, string Kind, long? UnitId=null);
    public sealed record Capabilities(int Movement, int Sight, bool CanDamage,
        int Attack, int Defence, string TerrainProfile, string ConcealmentProfile);
    public static class CapabilityResolver
    {
        public static bool ActualInvisible(UnitRecord u)=>u.Kind==ActualKind.Genuine&&
            u.ActualStatuses.Contains("invisible",StringComparer.OrdinalIgnoreCase);
        public static AppearanceRecord EffectiveAppearance(UnitRecord u)
        {
            if(!ActualInvisible(u))return u.Appearance;
            var result=u.Appearance.Copy();result.Concealment="hidden";
            if(!result.Statuses.Contains("invisible",StringComparer.OrdinalIgnoreCase))result.Statuses.Add("invisible");
            return result;
        }
        public static Capabilities Resolve(UnitRecord u,INativeRules native)
        {
            var apparent=native.Profile(u.Appearance.OperativeForm);
            string concealment=ActualInvisible(u)||u.Appearance.Concealment=="hidden"?"hidden":apparent.ConcealmentProfile;
            if(u.Kind==ActualKind.Mimic)
                return new(apparent.Movement,native.Profile("mimic").Sight,false,0,0,"none",concealment);
            var actual=native.Profile(u.ActualForm);
            bool weapon=actual.HasDamagingWeapon; // A genuine vessel uses its own weapons, even with a Spy passenger.
            return new(actual.Movement,actual.Sight,weapon,weapon ? actual.Attack : 0,
                actual.Defence,actual.TerrainProfile,concealment);
        }
    }
    public static class Rules
    {
        public const int MimicCost=RecruitmentCatalog.Cost, MimicHp=1, SmuggleCost=2, ExchangeRange=1;
        public static readonly (string Id, string? Parent, int Tier)[] Branch = {
            ("deception",null,1), ("propaganda","deception",2), ("intelligence","deception",2),
            ("espionage","propaganda",3), ("counterintelligence","intelligence",3)
        };
        static void Require(bool condition,string reason) { if(!condition) throw new RuleViolation(reason); }
        static PlayerRecord Player(World w,int actor) => w.Players.TryGetValue(actor,out var p) ? p : throw new RuleViolation("Unknown player");
        static UnitRecord Unit(World w,long id) => w.Units.TryGetValue(id,out var u) && u.Alive ? u : throw new RuleViolation("Missing living unit");
        static bool Frozen(UnitRecord unit) => unit.ActualStatuses.Contains("frozen",StringComparer.Ordinal);
        static void Authorize(World w,int actor,UnitRecord u)
        {
            Require(u.Commander==actor,"Not the current commander");
            Require(Observation.Accessible(w,actor,u.Position),"Unit tile is inaccessible");
            Require(u.Allowance.Player==actor && u.Allowance.PlayerTurn==Player(w,actor).Turn,"Stale turn allowance");
        }
        static string Fingerprint(string kind,object payload)
        {
            byte[] bytes=Encoding.UTF8.GetBytes(kind+"\n"+JsonSerializer.Serialize(payload,Codec.Options));
            return Convert.ToHexString(SHA256.HashData(bytes));
        }
        static CommitResult Transaction(World source,string commandId,string kind,object payload,Action<World> mutation,long? unitId=null)
        {
            Require(!string.IsNullOrWhiteSpace(commandId),"A committed action needs a stable command ID");
            Codec.Validate(source);
            string fp=Fingerprint(kind,payload);
            if(source.AppliedCommands.TryGetValue(commandId,out var existing))
            {
                Require(existing==fp,"Command ID reused for a different payload");
                return new(Codec.Copy(source),true,kind,source.CommandUnits[commandId]);
            }
            World candidate=Codec.Copy(source);
            mutation(candidate); // A rejection cannot mutate the caller's world.
            candidate.AppliedCommands.Add(commandId,fp);
            candidate.CommandUnits.Add(commandId,unitId);
            checked { candidate.Revision++; }
            Codec.Validate(candidate);
            return new(candidate,false,kind,unitId);
        }
        public static bool CanRecruitAppearance(PlayerRecord player,FormProfile profile,INativeRules native)
        {
            if(!player.NativeRosterForms.Contains(profile.Id)) return false; // A01: never a universal foreign roster.
            if(profile.Structure==BodyStructure.CentipedeSegment) return false;
            if(!native.HasFormAdapter(profile.Id)) return false; // Explicit unsupported adapter, not arbitrary fallback.
            if(profile.Recruitment is not (RecruitmentKind.OrdinaryCity or RecruitmentKind.SpecialCity)) return false;
            return player.Technologies.Contains("propaganda") ||
                ((profile.Recruitment is RecruitmentKind.OrdinaryCity or RecruitmentKind.SpecialCity) && player.UnlockedRecruitmentForms.Contains(profile.Id));
        }
        public static CommitResult RecruitMimic(World source,string commandId,int actor,string cityId,string appearanceId,INativeRules native)
        {
            long id=source.NextLogicalId;
            return Transaction(source,commandId,"recruit-mimic",new {actor,cityId,appearanceId},w=> {
                var p=Player(w,actor);
                Require(p.Technologies.Contains("deception"),"Deception is unresearched");
                Require(w.Cities.TryGetValue(cityId,out var city),"Recruitment city does not exist");
                Require(city!.Owner==actor && city.RecruitmentAllowed,"Not a valid recruitment city");
                Require(Observation.Accessible(w,actor,city.Position),"City tile is inaccessible");
                Require(city.SupportUsed<city.SupportCapacity,"No native support capacity");
                Require(!w.Units.Values.Any(u=>u.Alive && u.Position==city.Position),"Recruitment tile is occupied");
                var form=native.Profile(appearanceId);
                Require(CanRecruitAppearance(p,form,native),"Appearance unavailable or adapter not implemented");
                Require(p.Stars>=MimicCost,"Insufficient stars");
                var u=new UnitRecord { LogicalId=id, Kind=ActualKind.Mimic, ActualBody="mimic",
                    ActualHp=MimicHp, ActualMaxHp=MimicHp, ActualInvestment=MimicCost,
                    PermanentOwner=actor, Commander=actor, HomeCity=city.Id, Position=city.Position,
                    IndependentAppearance=true, Allowance=native.RecruitmentAllowance(actor,p.Turn).Copy(),
                    Appearance=new AppearanceRecord {Body=form.Id, Structure=form.Structure, Hp=form.MaxHp, MaxHp=form.MaxHp,
                        Concealment=form.ConcealmentProfile} };
                w.Units.Add(id,u); w.NextLogicalId=checked(id+1); p.Stars-=MimicCost; city.SupportUsed++;
                // Native discovery/recruitment events are a separate adapter. This
                // kernel does not silently reveal fog or publish a private form picker.
            },id);
        }
        public static CommitResult PositiveDamage(World source,string commandId,long unitId,int hpLoss,INativeRules native)
        {
            Require(hpLoss>=0,"Damage receipt must be nonnegative");
            return Transaction(source,commandId,"resolved-positive-damage",new{unitId,hpLoss},w=> {
                var u=Unit(w,unitId);
                if(hpLoss==0) return;
                u.ActualHp=Math.Max(0,u.ActualHp-hpLoss);
                if(u.ActualHp==0)
                {
                    u.Alive=false;
                    if(w.Cities.TryGetValue(u.HomeCity,out var home)) home.SupportUsed=Math.Max(0,home.SupportUsed-1);
                    // Death keeps a tombstone/ID; native death animations and
                    // elimination cleanup are not implemented by this receipt.
                    u.Commander=null;
                }
                else Reveal(u,native);
            },unitId);
        }
        static void Reveal(UnitRecord u,INativeRules native)
        {
            Require(u.Kind!=ActualKind.Mimic,"A surviving Mimic must never become a bare real form");
            u.IndependentAppearance=false;
            u.Appearance=new AppearanceRecord { Body=u.ActualBody,Structure=u.Structure,Vessel=u.ActualVessel,
                Passenger=u.ActualPassenger,Skin=u.ActualSkin,Hp=u.ActualHp,MaxHp=u.ActualMaxHp,
                Statuses=new List<string>(u.ActualStatuses),Concealment=native.Profile(u.ActualForm).ConcealmentProfile };
            // A Cloak's detected/revealed native status on damage is V09: not
            // implemented here. Do not call this receipt a complete cloak adapter.
        }
        public static CommitResult ApplyNondamagingStatus(World source,string commandId,long unitId,string status)
        {
            Require(!string.IsNullOrWhiteSpace(status),"Missing status ID");
            return Transaction(source,commandId,"nondamaging-status",new{unitId,status},w=> {
                var u=Unit(w,unitId);
                if(!u.ActualStatuses.Contains(status)) u.ActualStatuses.Add(status);
                if(!u.Appearance.Statuses.Contains(status)) u.Appearance.Statuses.Add(status);
            },unitId);
        }
        // Receives a natively validated support effect. Scope/recipient checks are
        // revalidated here. Amount/timing/eligibility come from V10, never a wiki constant.
        public static CommitResult Heal(World source,string commandId,int actor,long casterId,long targetId,int amount,
            bool feigned,bool nativeEligibility,TurnAllowance nativeAfter,INativeRules native)
        {
            Require(amount>0,"Non-positive healing amount");
            return Transaction(source,commandId,feigned ? "feign-heal" : "real-heal",
                new{actor,casterId,targetId,amount,feigned,nativeEligibility,nativeAfter},w=> {
                var caster=Unit(w,casterId); var target=Unit(w,targetId); Authorize(w,actor,caster);
                Require(nativeEligibility && !Frozen(caster) && !caster.Allowance.ActionSpent,"Support not eligible");
                Require(Observation.Accessible(w,actor,target.Position),"Target tile inaccessible");
                Require(!target.Rogue && target.PermanentOwner==actor,"Support excludes all Rogue/enemy recipients");
                if(!feigned)
                {
                    Require(caster.Kind!=ActualKind.Mimic,"A Mimic cannot genuinely heal");
                    Reveal(caster,native); // Actor reveals at commitment, including a legal self-target.
                    target.ActualHp=Math.Min(target.ActualMaxHp,checked(target.ActualHp+amount));
                }
                target.Appearance.Hp=Math.Min(target.Appearance.MaxHp,checked(target.Appearance.Hp+amount));
                if(feigned) target.IndependentAppearance=true;
                Require(nativeAfter.ActionSpent,"Support receipt did not consume its action");
                PreserveSpent(caster.Allowance,nativeAfter);
                caster.Allowance=nativeAfter.Copy();
            },casterId);
        }
        static void PreserveSpent(TurnAllowance before,TurnAllowance after)
        {
            Require(before.Player==after.Player && before.PlayerTurn==after.PlayerTurn,"Receipt changed turn ownership");
            Require(!before.MovementSpent || after.MovementSpent,"Receipt refreshed movement");
            Require(!before.ActionSpent || after.ActionSpent,"Receipt refreshed action");
            Require(after.NativeFollowUps.All(x=>before.NativeFollowUps.Contains(x)),"Receipt invented native follow-ups");
        }
        public static CommitResult Exchange(World source,string commandId,int actor,long initiatorId,long partnerId,
            ExchangeKind kind,bool swap,INativeRules native)
        {
            return Transaction(source,commandId,"exchange",new{actor,initiatorId,partnerId,kind,swap},w=> {
                Require(initiatorId!=partnerId,"Cannot exchange with self");
                var p=Player(w,actor); var init=Unit(w,initiatorId); var partner=Unit(w,partnerId);
                Authorize(w,actor,init);
                CompoundBodies.RequireExchangeParticipant(w,init,native);
                CompoundBodies.RequireExchangeParticipant(w,partner,native);
                Require(init.Kind==ActualKind.Mimic && !Frozen(init) && !init.Allowance.ActionSpent,"Initiator not ready");
                Require(p.Technologies.Contains(kind==ExchangeKind.Regroup ? "deception" : "propaganda"),"Exchange tech unresearched");
                Require(partner.Commander==actor && (partner.PermanentOwner==actor || partner.Rogue),"Partner not controlled/owned");
                Require(Observation.Accessible(w,actor,partner.Position),"Partner tile inaccessible");
                Require(native.Distance(init.Position,partner.Position)<=ExchangeRange,"Partner out of range");
                CompoundBodies.RequireCopyable(partner.Appearance,native);
                CompoundBodies.RequireCopyable(init.Appearance,native);
                var initAfter=kind==ExchangeKind.Regroup ? CapabilityResolver.EffectiveAppearance(partner).Copy() : init.Appearance.Copy();
                var partnerAfter=kind==ExchangeKind.Disguise ? init.Appearance.Copy() : CapabilityResolver.EffectiveAppearance(partner).Copy();
                if(CapabilityResolver.ActualInvisible(partner))
                {partnerAfter.Concealment="hidden";if(!partnerAfter.Statuses.Contains("invisible",StringComparer.OrdinalIgnoreCase))partnerAfter.Statuses.Add("invisible");}
                Require(native.HasFormAdapter(initAfter.OperativeForm) && native.HasFormAdapter(partnerAfter.OperativeForm),"Unsupported form adapter");
                var destI=swap ? partner.Position : init.Position; var destP=swap ? init.Position : partner.Position;
                Require(native.CanExchangeOccupy(init,destI,initAfter) && native.CanExchangeOccupy(partner,destP,partnerAfter),"Illegal exchange endpoints");
                // Revalidate on the authoritative commit world, never cached UI eligibility.
                CompoundBodies.RequireExchangeParticipant(w,init,native);
                CompoundBodies.RequireExchangeParticipant(w,partner,native);
                // Partners can be exhausted or frozen. Do not call movement,
                // embark, terrain, arrival or status-removal routines here.
                init.Appearance=initAfter; partner.Appearance=partnerAfter;
                if(kind==ExchangeKind.Regroup) init.IndependentAppearance=true; else partner.IndependentAppearance=true;
                init.Position=destI; partner.Position=destP; init.Allowance.EndTurn();
                // Coast reconstruction and authoritative sight notifications are
                // deliberately separate adapters (V04/V05), not claimed by this model.
            },initiatorId);
        }
        public static CommitResult Smuggle(World source,string commandId,int actor,long mimicId,INativeRules native)
        {
            return Transaction(source,commandId,"smuggle",new{actor,mimicId},w=> {
                var p=Player(w,actor); var u=Unit(w,mimicId); Authorize(w,actor,u);
                Require(u.Kind==ActualKind.Mimic && !Frozen(u) && !u.Allowance.ActionSpent,"Smuggle needs an unused action");
                Require(w.Resources.TryGetValue(u.Position.Key,out var resource),"No resource below unit");
                Require(resource!.Kind is ResourceKind.Fruit or ResourceKind.Fish or ResourceKind.Animal,"Not a Smuggle resource");
                Require(p.Technologies.Contains(resource.HarvestTechnology),"Real harvesting technology required");
                Require(native.CanHarvest(p,resource),"No eligible native harvesting action for this tribe/resource");
                Require(!resource.TerritoryOwner.HasValue || !p.Treaties.Contains(resource.TerritoryOwner.Value) || u.Rogue,"Treaty resource protected");
                Require(w.Cities.TryGetValue(u.HomeCity,out var home),"Native home assignment missing; native recovery required before retry");
                Require(p.Stars>=SmuggleCost,"Insufficient stars");
                int populationGain=native.HarvestPopulation(p,resource); Require(populationGain>0,"Missing native population yield");
                p.Stars-=SmuggleCost; w.Resources.Remove(u.Position.Key);
                home!.Population=checked(home.Population+populationGain); u.Allowance.EndTurn();
                w.PopulationNotices.Add(new PopulationNotice{CommandId=commandId,City=home.Id,Owner=home.Owner,DeferredToOwnerTurn=home.Owner!=actor});
            },mimicId);
        }
        // Native upgrade eligibility/cost/budget changes must be validated by the
        // adapter before this receipt; the kernel prevents merging equal type IDs,
        // grants no weapon from the appearance, and refuses action refreshes.
        public static CommitResult GenuineUpgrade(World source,string commandId,int actor,long unitId,string newVessel,
            int nativeCost,bool nativeEligible,TurnAllowance nativeAfter)
        {
            Require(nativeCost>=0,"Negative upgrade cost");
            return Transaction(source,commandId,"genuine-upgrade",new{actor,unitId,newVessel,nativeCost,nativeEligible,nativeAfter},w=> {
                var u=Unit(w,unitId);var p=Player(w,actor);Authorize(w,actor,u);
                Require(u.Kind!=ActualKind.Mimic && nativeEligible,"Not a genuine eligible naval upgrade");
                Require(p.Stars>=nativeCost && !string.IsNullOrEmpty(newVessel),"Invalid cost/type");
                PreserveSpent(u.Allowance,nativeAfter);u.Allowance=nativeAfter.Copy();
                u.ActualVessel=newVessel;u.ActualInvestment=checked(u.ActualInvestment+nativeCost);p.Stars-=nativeCost;
                if(!u.IndependentAppearance) u.Appearance.Vessel=newVessel;
                // Native HP/cargo transitions are not modeled by this receipt.
            },unitId);
        }
        // MODEL RECEIPT ONLY: native combat eligibility/action result must come
        // from the future adapter. This does not execute damage or grow real bodies.
        public static CommitResult GenuineAttackCommit(World source,string commandId,int actor,long unitId,
            bool nativeEligible,TurnAllowance nativeAfter,INativeRules native)
        {
            return Transaction(source,commandId,"genuine-attack-commit",new{actor,unitId,nativeEligible,nativeAfter},w=> {
                var u=Unit(w,unitId);Authorize(w,actor,u);
                Require(u.Kind!=ActualKind.Mimic && nativeEligible && !Frozen(u) && !u.Allowance.ActionSpent,
                    "No eligible genuine attack");
                Require(native.Profile(u.ActualForm).HasDamagingWeapon,"No actual damaging weapon");
                Require(nativeAfter.ActionSpent,"Attack receipt did not spend action");
                PreserveSpent(u.Allowance,nativeAfter);
                Reveal(u,native);u.Allowance=nativeAfter.Copy();
            },unitId);
        }
        // MODEL RECEIPT ONLY, not a player command and not native growth code.
        // The native adapter must supply genuine entities and authoritative links.
        // No entities are spawned, killed or moved here. Only real graph changes
        // can trigger A01-D2 cleanup. Rendering/visible-only poofs remain unbound.
        public static CommitResult ReceiveNativeAttachmentSnapshot(World source,string commandId,
            Dictionary<long,long> nativeParents,INativeRules native)
        {
            var snapshot=new Dictionary<long,long>(nativeParents);
            return Transaction(source,commandId,"native-attachment-snapshot",snapshot,w=> {
                w.SegmentParents=new Dictionary<long,long>(snapshot);
                CompoundBodies.ValidateGraph(w,allowMaskCleanup:true);
                foreach(var u in w.Units.Values)
                {
                    if(native.Profile(u.ActualBody).Structure!=u.Structure)
                        throw new RuleViolation("Actual native identity/topology role mismatch");
                    if(u.Structure==BodyStructure.CentipedeHead && u.IndependentAppearance &&
                        CompoundBodies.HasAttachedBodies(w,u.LogicalId)) Reveal(u,native);
                }
            });
        }
        public static bool CanCapture(UnitRecord u,bool nativeOperativePermission) =>
            u.Alive && !u.Rogue && nativeOperativePermission;
    }
}
