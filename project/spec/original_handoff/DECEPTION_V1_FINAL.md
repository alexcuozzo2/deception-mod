# The Battle of Polytopia — Deception branch
## FINAL v1 implementation specification · design freeze DF-1 · 14 September 2026

**Owner:** Alex. **Purpose:** standalone handoff to an implementation instance. **Status:** finalized design, not implemented or playtested software. This document supersedes every earlier Deception specification, including revisions 1.1–1.4. Do not combine old and new rules. The acceptance cases below are requirements; all begin NOT RUN.

The user approved the latest Actual/Appearance interface and preservation of an independent disguise during genuine paid naval upgrades. This freeze also makes necessary implied behavior explicit. Rules marked **[Cxx]** are completion defaults formalized for this handoff, not claims that Alex individually dictated those edge cases. They are the implementation baseline unless Alex subsequently changes them. **[Vxx]** identifies a native-engine integration check, not permission to replace the selected gameplay with contradictory native behavior.

## Reading order and precedence

Read sections 1–4 for the model, 5–14 for behavior and UI, 15–18 for events and implementation boundaries, then the graded acceptance cases. The ZIP also contains the same acceptance cases as JSON and a short implementer brief. The full specification alone is sufficient to understand the design.

Precedence: explicit prohibitions/invariants → named action/event rules → capability and ownership tables → native behavior in the pinned build. A concrete acceptance case and its cited rule are intended to agree. A conflict is a specification defect to report, not an invitation to pick whichever implementation is easiest.

**Native behavior** means the behavior of the actual installed build, not recalled wiki values. Record that build and loader before coding. Ordinary native numbers, durations and upgrade timing must be measured through the checks described in §17. Custom numerical values below are fixed v1 starting values, not proven balance.

## Contents

- [1. Scope, branch and costs](#1-scope-branch-and-costs)
- [2. Identity, presentation and authority](#2-identity-presentation-and-authority)
- [3. Capability resolution: what is actual and what is apparent](#3-capability-resolution-what-is-actual-and-what-is-apparent)
- [4. Appearance snapshots, healing, combat and reveal](#4-appearance-snapshots-healing-combat-and-reveal)
- [5. Recruitment, form availability and selection](#5-recruitment-form-availability-and-selection)
- [6. Regroup and Disguise](#6-regroup-and-disguise)
- [7. Naval forms, upgrades and coast cases](#7-naval-forms-upgrades-and-coast-cases)
- [8. Captures, Smuggle and economy](#8-captures-smuggle-and-economy)
- [9. Cloaks, concealment and information](#9-cloaks-concealment-and-information)
- [10. Subvert, Rogue relations and turn processing](#10-subvert-rogue-relations-and-turn-processing)
- [11. Spy networks](#11-spy-networks)
- [12. Blackout and fog-sensitive combat](#12-blackout-and-fog-sensitive-combat)
- [13. Propaganda](#13-propaganda)
- [14. UI, animations and accessibility contract](#14-ui-animations-and-accessibility-contract)
- [15. Authoritative event and transaction contract](#15-authoritative-event-and-transaction-contract)
- [16. Completion register: explicit policies added to close implied cases](#16-completion-register-explicit-policies-added-to-close-implied-cases)
- [17. Native integration checks and release gates](#17-native-integration-checks-and-release-gates)
- [18. Implementation order and do-not-regress list](#18-implementation-order-and-do-not-regress-list)
- [19. Provenance and external verification limits](#19-provenance-and-external-verification-limits)
- [20. Graded acceptance cases](#20-graded-acceptance-cases)

---

# 1. Scope, branch and costs

## 1.1 Scope

Add a genuine sixth technology branch. Do not replace a native branch, technology, unit, or building. Initially expose research/recruitment to ordinary tribes. Interaction with owned foreign/special units is part of this contract: a converted special unit is not excluded merely because the research branch initially belongs to ordinary tribes. Enabling the branch in special tribes' own trees is outside the first release scope, not a license to omit the specified copying interactions.

The known intended host is the user's Steam/macOS modded installation. Exact game version, CPU/loader mode, mod dependencies and APIs are unverified here. No particular native method names or Polymod hooks are asserted.

## 1.2 Locked technology tree

```text
Deception                          Tier 1
├── Misdirection                   Tier 2
│   └── Propaganda                 Tier 3
└── Camouflage                     Tier 2
    └── Espionage                  Tier 3
```

| Technology | Requirement | Player-facing description |
|---|---|---|
| Deception | New tier-1 root | Train Mimics that impersonate unlocked units and harvest population resources for their home cities. |
| Misdirection | Deception | Mimics can Regroup: copy a partner's appearance and optionally exchange positions. |
| Camouflage | Deception | Mimics can Disguise: give a partner their appearance and optionally exchange positions. |
| Propaganda | Misdirection | Recruit otherwise-unavailable appearances, including super units. Select an opponent's unit or city to inspect their fog. |
| Espionage | Camouflage | Train Spies with Scout, Subvert, Blackout, and passive command relaying. |

Use the native tier/city-count/discount formula for research. Do not grant these technologies free, invent fixed research prices, or alter other branches. Regroup/Disguise remain range **1**, even after Propaganda. Owning an original is sufficient to copy it; Propaganda gates recruitment appearances, not field copying.

## 1.3 Units and fixed v1 configuration

| Property | Mimic / Mimic Boat | Spy |
|---|---|---|
| Recruitment | Deception; 3 stars | Espionage; 8 stars |
| Actual maximum HP | 1, invariant | 10 |
| Actual attack / defense | 0 / 0 | 0 / 1 |
| Ordinary damaging attack | None | None |
| Damaging retaliation | None | None |
| Movement | Permitted profile of current apparent form | 1; Dash |
| Sight | Current apparent form's native sight, including Scout | Scout |
| Own abilities | Smuggle; researched Regroup/Disguise; approved feints | Subvert; Blackout; passive Relay |
| Active special range | Regroup/Disguise: 1 | Subvert/Blackout: 2 |
| Relay link range | Not a relay | 2 |
| Home/support | Native home-city assignment; one ordinary slot | Native assignment; one ordinary slot |

A Mimic Boat is the naval state of the same Mimic, not a separately recruited third unit. Mimics never gain more actual HP or real offense by changing form, hatching, being healed, receiving a status, or using an upgrade menu. A genuine Spy disguised as something else retains genuine Spy movement, Scout and abilities.

| Action | Star cost | Authoritative action cost |
|---|---:|---|
| Regroup / Disguise | 0 | End initiating Mimic's turn; may follow its movement |
| Smuggle fruit/fish/animal | 2 | Require unused movement AND action; consume both |
| Subvert / Blackout | 0 | Spy's action; Dash permits movement before it |
| Passive Relay / Propaganda inspection | 0 | None |
| Feign Heal / Swarm / Recover | 0 unless its represented native action charges a cost | Same timing/expenditure as represented action; never another budget |
| Feigned naval upgrade | 0 | Native corresponding upgrade eligibility/flag transition, without refresh [V05] |
| Genuine naval upgrade | Native paid cost | Native upgrade eligibility/flag transition [V05] |

Ordinary recruitment leaves the new unit in its native recruitment-turn state. A new recruit can be an exchange partner, but exchange never turns an unavailable recruit into a fresh actor. Real native follow-ups such as Persist remain follow-ups; do not collapse the engine's action model into a single simplistic Boolean.

---

# 2. Identity, presentation and authority

## 2.1 Keep these concepts independent

| State | Required meaning |
|---|---|
| Stable logical unit ID | One physical unit across native object reconstruction, upgrades, growth, embarkation and conversion. |
| Actual body/passenger | Real combat type or Mimic identity, actual HP, statuses, skills, experience and investment. |
| Actual vessel | Genuine naval wrapper or Mimic Boat; separate from passenger. |
| Appearance record | Apparent body/passenger/vessel, HP/max HP, statuses, public traits, skin, concealment and growth phase. |
| Independent appearance flag | Explicit state; still true when actual and apparent type IDs match. |
| Command authority | Who may issue orders now; does not imply ownership or friendly support. |
| Permanent/return owner | Normal owner, remembered through Rogue status until permanent conversion. |
| Native home city | Existing assignment; no second immutable origin. |
| Per-player turn allowance | Actual movement/action/follow-up state and per-turn Subvert acquisition stamp. |
| Per-player tile mask and detection | Tile access and unit concealment are different. |
| Private panel mode | Actual or Appearance; never a second unit or action budget. |

A genuine Raft wearing a copied Raft appearance has two forms even while they look identical. A normally typed Warrior can have a false-health overlay without a false unit type. Neither condition is a reason to collapse state.

## 2.2 Who sees what

Everyone sees apparent battlefield models, subject to their own fog/detection. The commander sees a small private Mimic badge on actual Mimics. The bare Mimic sprite exists for recruitment/info artwork, never as a living battlefield state.

The owner/commander's selected-unit panel supplies true and apparent identities and statistics. While Rogue, the remembered owner and temporary commander can inspect both identities, subject to tile access. Only the current commander can act. After permanent conversion the former owner loses privileged truth; there is no retained enemy "known Mimic" mode.

Smuggling, absent expected splash, unusual movement, witnessed recruitment or human memory may identify a bluff in the player's reasoning. They do not change enemy labels, portraits, unit names or map forms. Actual exposure of a genuine disguise is a global presentation reset, not a permanent observer knowledge marker.

## 2.3 Readiness

Hide actual readiness from every non-commanding observer, for all units, not only shufflers. Use uniform neutral/undimmed readiness treatment. No fake-ready timer, later snap-back, turn-end disclosure, grayed enemy skill, accessibility text or saved selection may expose spent flags. Controllers retain exact readiness. The remembered Rogue owner's identity-inspection privilege does not reveal current borrowed action flags.

Poison, freezing, Swarm, concealment and Rogue status are separate visual layers. A visible action remains evidence; the interface does not calculate the opponent's inference for them.

## 2.4 Actual/Appearance menu

First selection of a controlled unit opens **Actual**. Clicking that already-selected unit again toggles **Appearance**, then Actual, repeatedly. This is not a timed OS double-click. A unit without independent presentation can use its ordinary menu; a Mimic always has presentation.

Actual shows genuine commands, weapons, real support, Mimic abilities and genuine paid upgrades. Appearance shows only approved feints: Feign Heal, Feign Swarm, apparent Recover and feigned naval upgrades. No fake attack, Convert, Subvert or Infiltrate button is granted by appearance.

Both modes show true/apparent information and truthful actual readiness. Movement legality is always genuine, even in Appearance mode. Selecting Actual does not reveal the unit. Commands are explicitly classified as genuine or feigned and validated against one physical action budget.

Select a different unit, deselect/reselect, change commander or load a game: begin in Actual. During target selection and atomic Keep/Swap confirmation, clicks belong to that operation, not menu toggling. Cancel before switching. A reveal closes stale Appearance controls and returns Actual. Ordinary opponents cannot toggle to truth. All alternate input paths obey the same authorization.

---

# 3. Capability resolution: what is actual and what is apparent

## 3.1 Central table

| Property | Genuine unit with disguise | Actual Mimic |
|---|---|---|
| Movement/terrain traversal/Sneak | Actual body/vessel | Approved apparent profile |
| Sight/Scout | Actual body/vessel | Apparent profile |
| Real weapons/retaliation | Actual capabilities; active use reveals | None |
| Genuine unit-affecting passives | Actual capabilities; passive source not revealed | Suppressed |
| Source movement/VFX/audio | Apparent form | Apparent form |
| Terrain-only passives | Apparent profile only; replace actual terrain profile | Apparent profile |
| Cloak-like concealment | Selected appearance rules; genuine mechanical exception | Apparent concealment |
| Feigned support | Presentation only | Presentation only |
| Own captures/Smuggle/etc. | Actual permissions; Smuggle only actual Mimics | Explicit Mimic permissions plus operative form restrictions |
| Damage taken | Actual stats; positive damage clears mask | Any positive damage kills |

"No offensive imitation" specifically means **no borrowed direct damage, conversion, subversion, real healing/Swarm or other real effects on units**. It does not forbid strategically useful scouting, concealment, terrain creation, blocking, capture, theft or Mimic exchanges. Do not use a vague skill category in place of this table.

Genuine naval speed remains actual: Juggernaut dressed as Scout does not receive Scout speed. A Mimic Boat dressed as Scout does receive its approved movement/sight, but not its weapon.

## 3.2 Terrain and passive split

A Warrior-as-Mooni and a Mimic-as-Mooni generate approved real ice, but receive no borrowed unit freezing. A Living Island appearance lays the native intended algae, not an unrelated fruit-producing variant. A Crab appearance produces its terrain flooding. No fake terrain grid exists.

A genuine Crab-as-Warrior suppresses its terrain flood. Do not combine true and apparent terrain profiles. Preserve genuine unit-affecting passives separately: a real Juggernaut-as-Raft still deals its real qualifying movement damage, without the Juggernaut source animation. Victim damage is real and visible where authorized. Fake splash VFX deals no damage.

Trigger profiles on the corresponding genuine native event (movement, appropriate passive tick, etc.), at most once per event. Exchanges do not trigger movement passives, even when a native transport constructor is called. A reveal resumes the actual terrain profile on future eligible events, not retroactively on the current tile.

**[C01] Terrain safety:** copied terrain routines retain native terrain/building eligibility, but must not call an embedded damage/status/ejection/destruction routine on occupants. Direct terrain effects are real; separate real unit consequences are suppressed for borrowed effects. Persistent terrain can later affect ordinary movement naturally. Genuine underlying abilities still apply their own actual unit effects. Audit mixed routines rather than apply damage and undo it afterward.

## 3.3 Growth and compound forms

Copy apparent phase and remaining native growth timer, not a fresh newborn timer. A copied growing appearance progresses on its appropriate normal owner-turn timeline and carries apparent wounds under the native form-transition rules. Its actual Mimic identity, one HP and no-offense invariant survive. A genuine unit's own growth is independent and does not overwrite a maintained disguise.

**[C02]** Subverting, re-subverting, menu toggling and object replacement are not extra growth ticks. Count at most the scheduled owner-turn tick; while Rogue use the remembered owner's schedule. Permanent conversion changes future ownership scheduling, not the already-consumed tick.

**[C03] Compound entities:** copying one unit copies one target body, not adjacent independent bodies, cargo armies or a whole linked chain. Single-body hatching changes that body's appearance/profile. A Mimic cannot create genuine offspring, real detached segments, or new supported units through an imitated passive. Decorative tails must not occupy or target unseen extra tiles. Genuine underlying segmented bodies retain their actual native structure. Implement adapters for each supported multi-body form; an unsupported adapter is an explicitly reported coverage failure, never a silent grant of real spawned units. Native target/body/segment definitions require [V11].

---

# 4. Appearance snapshots, healing, combat and reveal

## 4.1 Snapshot payload

Transfer apparent body, vessel, passenger, displayed HP/max HP, public skills, relevant statuses/cosmetics, concealment state and growth phase. Copy values, not a live link. Do not copy true HP, actions, ownership, native home city, real experience, paid investment or Rogue state. An actual frozen/poisoned recipient remains actually frozen/poisoned; copying an apparent healthy state does not cure it.

Apparent statuses may differ from actual statuses. Authorized panels show both. Opponent movement/action evidence can expose discrepancies, but no hidden true field should overwrite the presentation merely during rendering. Rogue tint is truthful and cannot be copied or suppressed by disguise.

## 4.2 Support and recovery

Real or feigned Heal/Swarm targets only eligible units permanently owned by the caster's current commander. Rogues are never recipients, even of another Rogue's support. A Rogue genuine caster can support its commander's genuinely owned units; it cannot heal both sides. Conversion is a different relation and can target Rogues.

Actual mode uses real support and strips the genuine specialist actor's disguise. Appearance mode uses privately labeled Feign Heal/Swarm, spends the native corresponding action, changes presentation only and retains the source disguise. A real Mind Bender with Mind Bender appearance can choose either; equal type IDs do not merge the buttons. Public sound/animation looks ordinary.

**[C04] Apparent-stat lifecycle:** use native represented range, amount, eligibility and expiry on the apparent state. Real healing modifies actual state and applies its ordinary visible healing operation to apparent state independently; it does not normalize false HP to truth merely because healing occurred. Clamp each against its own maximum. Example: actual 4/10, apparent 8/10, real +4 heal → actual 8/10, apparent 10/10, no reveal. Fake +4 → actual 4/10, apparent 10/10. A real-full/app-damaged unit can still take a legal apparent recovery. Each action costs its normal allowance once.

Feigned HP remains until replaced/reset by appearance transfer, actual reveal or normal apparent evolution. Feigned Swarm uses the represented native duration/clearing event, never gives real movement, and has no suspicious shorter timer. Cosmetic cleansing changes only apparent poison/frozen state, not actual status. Truth always remains visible to the controller. When an unmasked unit receives false support, create a stat-only independent overlay.

## 4.3 Reveal triggers

| Event | Genuine actor/source | Genuine affected target | Actual Mimic |
|---|---|---|---|
| Direct committed real attack | Remove disguise before resolution | Positive damage removes disguise | Cannot attack; positive damage kills |
| Genuine activated specialist: Subvert, Blackout, Convert, actual Heal/Swarm, Infiltrate, other real unit-specific active | Remove actor mask at commitment | Own effect rules; no automatic target-type reveal merely from command | No borrowed genuine specialist |
| Positive passive/splash/status damage | Passive source keeps mask | Remove target mask | Dies |
| Pure freeze/poison/Swarm without HP loss | Normal specialist-actor rule if activated | No damage-based reveal | Remains appearance; actual status applies |
| Exchange, ordinary movement, terrain passive, Smuggle, feigned support/upgrade | Keep appearance | Keep appearance | Never a naked state |
| Genuine paid transport upgrade | Preserve independent disguise | Not applicable | No genuine free warship upgrade |
| Detecting a hidden apparent Cloak | Exposes visible apparent Cloak, not true identity | No damage reveal | Visible apparent Cloak, not naked Mimic |

**[C05] Zero damage:** a direct attacker still reveals, but a target receiving zero HP loss retains its mask. Do not reclassify nondamaging status as damage. The final incoming reveal trigger is positive actual damage. If native combat never produces zero in an ordinary case, test using an instrumented fixture.

A genuine reveal clears the independent type/stat overlay and displays current actual state. Use a short poof; a dead Mimic can disappear with a poof but never becomes a living bare sprite. Global reset does not uncover any tile or reveal to an observer lacking access.

**[C06] Actual Cloak after reveal:** mark its concealment as detected/revealed through the native reveal policy rather than instantly hiding the true model in the same frame. Future native hiding is allowed. A valid transferred-invisibility operation can still re-conceal it under §9. Preserve real spent actions and statuses. Observers in fog do not receive a free sight update.

## 4.4 Predictions and consequences

Use the acting player's known actual capabilities on their own side and the target's apparent capabilities unless that player is entitled to true target information (e.g. their remembered Rogue). Enemy information, sweat/lethal indicators, apparent combat range/skills, unit names and cargo use appearance. A Knight may preview a kill against apparent Warrior and meet a real Giant only after commitment.

Damage always uses true stats. All HP loss is real; fake source VFX never causes temporary damage. When a victim takes positive damage, its revealed actual HP replaces stale false HP and the actual loss is shown. Never simulate a feint by damaging then healing back: death, retaliation, growth, scores or fog could already trigger.

No copied unit-ID, selected highlight or replay track may map the true piece through an otherwise indistinguishable shuffle. Human deduction remains allowed. Optional UI fields that directly expose hidden actual unit valuation/identity must be filtered or uniformly hidden, not given fabricated real economic consequences [V12].

---

# 5. Recruitment, form availability and selection

Without Propaganda, a Mimic may start as an unlocked ordinary city-origin form in the player's eligible roster. With Propaganda, include unresearched eligible city-origin forms and city-produced super units. A genuine Spy is a city-origin form. Exclude naked Mimic, directly recruited ships, Daggers, adult dragons, Moth eggs and other forms that do not ordinarily originate in cities. A city-produced egg counts as city-origin if the pinned tribe definition actually creates it there; an advanced form grown from it does not. Use a reviewed per-tribe registry, not guessed display names [V01/V11].

Field copying has no research/type gate: any owned/commanded eligible partner's apparent form may be copied, including Giants and acquired foreign forms, even forms excluded from direct recruitment. Physical outcome legality still applies. You cannot copy an unowned hostile unit simply by standing beside it.

Interface: select city → Mimic (3 stars) → legal appearance picker → ordinary recruit confirmation. Show private true stats and apparent form. Cancel costs nothing. On commitment spawn directly as the selected appearance with ordinary apparent-form recruitment cues. No naked frame, public 3-star/Mimic label, super-unit displacement, fabricated city upgrade, or ordinary paid-unit production under the hood. An observant opponent seeing an apparent Giant without an upgrade can infer a fake.

Use normal city occupancy/support/recruitment checks. A fresh Mimic does not act immediately merely because another ready Mimic dresses or relocates it. Own recruitment information and unit badge remain truthful.

---

# 6. Regroup and Disguise

## 6.1 Semantics and eligibility

Regroup: **partner's apparent snapshot → initiating Mimic**.

Disguise: **initiating Mimic's apparent snapshot → partner**.

Then privately choose Keep or Swap. Both operations cost the initiating Mimic's action and may follow its true movement. Both have range 1 using the native tile-targeting metric, not a new Euclidean metric. Target must be a different unit you own or currently command as Rogue, whose tile you can access. A normal treaty ally or uncommanded enemy is not a partner. A frozen/exhausted partner is allowed; a frozen initiator cannot violate its real inability to act.

Copy current appearance, never actual type. Do not transfer Rogue status, ownership, home city, real injuries, action state or experience. Partnering with a borrowed unit supplies repositioning and a copy but does not hide it from its returning owner indefinitely.

## 6.2 Atomic operation

Select initiating Mimic in Actual → Regroup/Disguise → adjacent partner → Keep/Swap → commit. Preview both actual/apparent outcomes privately. Compute validity for Keep and Swap separately after environment-dependent form conversion. Disable an invalid Swap without disabling a valid Keep. If neither is legal, reject before spending anything.

No other command, movement, menu toggle or timer advance may occur between appearance transfer and the optional exchange. Revalidate unit IDs, positions, command rights, range, funds if applicable, status and legal resulting tiles on final commitment. Cancellation applies no partial snapshot, expenditure, fog refresh or transformation.

Both final positions/forms commit atomically. Only then refresh occupancy, siege, legitimate detection, actual sight/Scout and network reach. No normal movement routine or movement passive runs. The partner preserves all move/action/follow-up flags exactly; the initiating Mimic is spent in its final wrapper. Do not merely fix the UI tint while native boarding cooldowns still prohibit its action.

## 6.3 Consequences

A ready Catapult/Giant/Spy can be delivered and act; a spent one stays spent. A frozen partner relocates but stays frozen. A fully unspent eligible partner delivered onto a village/ruin can immediately perform the corresponding neutral action. A fully unspent Mimic can Smuggle at its new tile. Normal walking does not preserve those requirements. A newly recruited unready partner remains unready.

Partners may participate repeatedly; no chain-length cap. A spent initiating Mimic can later be a partner. Mimics may target Mimics, allowing a visible chain to continue after the real unit stops moving. Every initiator still pays its own action.

## 6.4 Legal position and form changes

Check actual genuine traversal; an appearance alone does not make a Warrior swim or let an aquatic-only body stand on dry land. A Mimic's apparent profile is its genuine traversal profile. Copy-only may be illegal if the new Mimic form cannot occupy its unchanged tile. Coastal passenger/vessel conversion is §7, not a ban on all land/water interaction.

Exchanges do not require a port for the explicitly legal coastal operations. This is an intended alternative route into water, not an accidental port bypass to remove. No exchange generates attack splash, ice/algae/flooding or action refresh; true positional sight does update.

## 6.5 Public animation

Two shared stages: (1) brief identity-change effect; (2) maybe-shuffle. Same assets, timing and sound for Regroup and Disguise; same maybe-shuffle for Keep and Swap. Do not animate a traceable true-unit path or label an initiator. Which apparent form changes can be observable, but that does not identify whether a Mimic copied or passed on its form.

Filter every stage per observer. A visible unit becoming hidden can have the shared disappearance cue. A previously invisible endpoint gets no revealing flash, sound position, camera pan, line or shadow. If both endpoints are invisible, no public spatial effect identifies either tile. Truthful Rogue color can distinguish a Rogue from its non-Rogue partner; do not suppress it in an attempt to promise a perfect shuffle where the public states are intentionally different.

---

# 7. Naval forms, upgrades and coast cases

## 7.1 Four independent facts

Track actual passenger, actual vessel, apparent passenger and apparent vessel. A Warrior in an actual Raft dressed as Juggernaut has an apparent Giant passenger, not a true Giant. A Mimic entering transport water becomes the same stable Mimic in a Mimic Boat wrapper (1 HP, 0 offense/defense), not a real ship with fake displayed damage.

| Apparent land body | Apparent standard embarkation |
|---|---|
| Giant | Juggernaut |
| Cloak | Dinghy, with appropriate concealment/detection |
| Ordinary transportable unit | Raft |

Use native adapters for special native swimming/flying forms; do not wrap a naturally aquatic unit as cargo merely because it touches water. Current apparent movement/Scout is real for Mimics. Genuine units always use actual body/vessel movement/sight. On return to land, restore actual passenger and the apparent land record separately.

## 7.2 Normal embarkation versus exchange conversion

Normal port boarding spends the native boarding movement/action allowance and runs its qualifying genuine arrival effects. A coastal exchange performs only necessary form conversion and preserves the partner's prior flags. It generates no placement splash or terrain effect. A ready exchange-created Juggernaut may subsequently make genuine movement and trigger its actual splash then.

Passenger-carrying Scout/Rammer/Bomber transports may exchange ashore, yielding their actual passenger. Their upgrades disappear/reconstruct as native disembarkation dictates [V05]. Native aquatic-only bodies such as Living Islands, Jellyfish and Sharks cannot exchange onto illegal dry land. A legal flooded tile is evaluated under actual traversal. Do not invent a land passenger for them. Disable only the illegal outcome.

**[C07] Presentation during form changes:** use native form-transition max-HP/wound conversion on the apparent record, independent of actual HP. Apparent disembark/re-embark follows its apparent passenger's ordinary mapping; do not preserve a spent Bomber wrapper as an invisible reusable land inventory unless the native equivalent does. Keep apparent growing/cargo state needed for a coherent return. No transport operation erases stable action/Subvert history.

## 7.3 Two upgrades

| Operation | Eligibility | Payment | Actual effect | Public effect |
|---|---|---|---|---|
| Actual-mode genuine upgrade | Actual Raft/native real path; controller's real tech; controller's territory | Native real stars | Change actual vessel/capabilities/investment | Preserve an existing independent snapshot; otherwise ordinary native upgrade |
| Appearance-mode feigned upgrade | Independent current Raft appearance; controller's territory; real tech OR Propaganda | 0 | No weapon/type/HP/investment change | Apparent Scout/Rammer/Bomber, normal-looking apparent upgrade |

This applies to Mimic Boats and genuine vessels with independent Raft appearance, including true Raft/apparent Raft. A normal unmasked Raft does not obtain a free appearance mode just because it is a Raft. A true Mimic Boat cannot use Actual mode to become a real paid warship.

State, not quota: if a unit receives Raft appearance again, it may feign another eligible upgrade. Neither menu creates a fresh turn. Native corresponding upgrade action timing applies, without native constructors inventing a refresh; log that timing in [V05]. Propaganda authorizes fake research only, never a real paid upgrade.

A real Raft/apparent Bomber can still buy an actual Raft upgrade. A real Juggernaut/apparent Raft can feign an upgrade even though the actual Juggernaut has no native Raft upgrade path. If it feigns Bomber, it retains its original genuine armament and movement, gains no Bomber weapon, and reveals when it uses its genuine attack. Genuine passive damage still preserves its source disguise.

**Paid upgrade preserving disguise is agreed:** real Raft/apparent Bomber → buy Rammer → real Rammer/apparent Bomber. Keep actual/apparent fields independent even when they temporarily match. Hidden genuine upgrades do not show a flash of actual artwork or strip the disguise. **[C08]** When an independent appearance does not change, do not play an opponent-facing transformation into the real upgraded type; any private confirmation is owner-only. Native publicly visible economic consequences are not falsified.

Disband refunds and actual score/investment calculations use actual paid value. A free apparent upgrade cannot increase actual refund, create stars or replace the passenger. Rogue paid upgrades use the commander's bank, territory and technology, and return with the improved actual unit.

## 7.4 Canonical coast matrix A

Start: land Mimic appearing Giant; adjacent actual Warrior Raft in water, apparently normal. Partner readiness is preserved in every row; initiating Mimic is spent.

| Action | Final land occupant | Final water occupant |
|---|---|---|
| Regroup + Keep | Mimic appearing Warrior | Actual Warrior Raft, normal |
| Regroup + Swap | Actual Warrior, normal | Mimic Boat appearing Warrior Raft |
| Disguise + Keep | Mimic appearing Giant | Actual Warrior Raft appearing Juggernaut |
| Disguise + Swap | Actual Warrior appearing Giant | Mimic Boat appearing Juggernaut |

## 7.5 Canonical coast matrix B

Start: genuine normal Warrior on land; adjacent initiating Mimic Boat appearing Juggernaut with apparent Giant passenger.

| Action | Final land occupant | Final water occupant |
|---|---|---|
| Regroup + Keep | Genuine Warrior, normal | Mimic Boat appearing Warrior Raft |
| Regroup + Swap | Mimic appearing Warrior | Actual Warrior Raft, normal |
| Disguise + Keep | Genuine Warrior appearing Giant | Mimic Boat appearing Juggernaut |
| Disguise + Swap | Mimic appearing Giant | Actual Warrior Raft appearing Juggernaut |

Neither matrix creates a true Giant or awards new HP, upgrades, extra actions or arrival splash. Both use the shared observer-filtered animation. Copy an apparent passenger for land interpretation, not the hidden true cargo.

---

# 8. Captures, Smuggle and economy

A non-Rogue Mimic may claim neutral villages, collect ruins and capture owned enemy cities where its operative form and ordinary city-capture rules permit. A Cloak-form Mimic cannot enter an enemy city by pretending the forbidden Infiltrate command is ordinary movement. A genuine Warrior-as-Cloak retains its real Warrior entry/capture permissions; a genuine Cloak-as-Warrior retains its actual restrictions.

Fully unused movement/action allows immediate village/ruin use after an exchange. Existing enemy-city siege/capture waiting rules remain native; the explicit immediate exception is villages/ruins and Smuggle, not a new instant enemy-city capture ability. Rogues cannot capture any city/village or collect ruins even if otherwise eligible.

## 8.1 Smuggle

Select actual Mimic → native-style fruit/fish/animal harvest icon (2 stars). Require the corresponding actual commander's harvesting technology, resource directly underneath, and unused movement AND action. Walking there consumes movement and prevents Smuggle. Exchange delivery preserves unused flags and permits it. Successful Smuggle spends both allowances, charges the commander once, removes one resource and credits its normal population once.

Eligible: own territory, another own city, hostile territory, unclaimed territory. Ineligible for ordinary Mimics: treaty-partner-owned resources. A Rogue Mimic may harvest there without breaking the commander's treaty. No buildings, farms or Starfish are Smuggle resources.

Destination is the unit's native current home-city ID, including when that city belongs to the enemy. Village capture and native reassignment are followed. A Rogue Mimic still feeds that same city while the commander pays. No immutable alternate origin, friendly fallback, independent Mimic, or destination picker.

Use the normal population-flight effect. Filter it per observer; do not reveal an unknown city through its trajectory/camera. Controller can inspect the home city in unit information. Optional owner-only home-city outline is allowed. No Mimic-revealed popup or naked model.

Selecting an ordinary resource through the city/tile interface still uses native local harvesting; selecting Mimic's action invokes home-city transfer. A disguise does not grant Smuggle to a genuine non-Mimic.

## 8.2 Recipient, support and reward handling

If population levels another player's city, queue the reward choice for that city's owner on their next turn using the native deferred-upgrade system. Never show the recipient's decision to the harvester. Preserve native reward order and city eligibility. **[C09]** Resolve income/return ordering (§10) before allowing the next player's interactive queued rewards; native accounting must not duplicate population or reward a city twice.

A missing/invalid native home-city pointer is an integration error, not a new mechanic: disable the transaction before charges/resource removal, report diagnostic context, and apply native repair/reassignment if available [V06]. Losing the home city does not by itself invalidate a still-existing enemy-owned destination.

Starfish collection is the separate native Navigation action: commander must own the real technology; reward is stars to the commander, not population. **[C10]** Rogues may collect Starfish under ordinary collection conditions because resource collection is not one of their explicit bans. Ruins remain banned. Appearance alone never grants Navigation.

Ordinary ruin-generated units are genuine native reward units; collecting a ruin does not make its collector cease being a Mimic. Use the pinned reward table, not invented upgrades to the collector. Mimic HP remains one in all cases.

---

# 9. Cloaks, concealment and information

Fog means tile access; concealment means whether a unit on an accessible tile is detected. A fake Cloak can be detected as a visible Cloak without exposing its underlying body.

| Actual / appearance | True movement and vision | Concealment | Genuine powers |
|---|---|---|---|
| Mimic / Cloak | Copied Cloak movement, Sneak, Scout | Native-looking hide/detect; copied current state | No Infiltrate |
| Real Cloak / Warrior | Actual Cloak movement, Sneak, Scout | Visible Warrior appearance | Actual Cloak powers under eligibility/reveal rules |
| Warrior / Cloak | Warrior movement/sight, no copied Sneak/Scout | Functional Cloak-style hide/detect | Warrior powers; no Infiltrate |
| Spy / Cloak | Actual Spy movement 1, Dash, Scout | Functional Cloak-style concealment | Actual Subvert/Blackout/Relay |

Regroup/Disguise transferring an invisible Cloak state really transfers invisibility; a visible snapshot remains visible. Use actual visibility/detection tests at each final tile: copying hidden state beside an enemy may immediately be detected under native rules. Do not erase true action history or frozen status.

Eye warnings cover actual OR apparent Cloak/Dinghy sources, using native detection footprint. An actual Cloak-as-Warrior still contributes the selected warning. Generic Scout forms, real Spies and Spy-shaped Mimics do not create a Cloak warning merely because they have Scout.

Subvert requires a detected target. A genuine visible Rogue Cloak can then receive an invisible disguise from a Mimic and Infiltrate if its real action is still available. The copied hidden condition satisfies eligibility; an obsolete reveal-cooldown check must not negate the explicitly selected combination. No Mimic obtains genuine Infiltrate.

A Rogue Cloak may Infiltrate any city, including the commander's own city. Native target economics apply; spawned Daggers and stolen stars belong to its commander. The operation is not Rogue capture. Check eligibility first, commit, reveal the genuine specialist, then execute the already-committed action. Do not make the reveal invalidate its own Infiltrate.

Rogue tint/angry bolts and private badges are rendered only where their viewer can legitimately see the unit. They do not expose an invisible location. On permanent conversion, former-owner privileged truth ends; human memory is not erased.

---

# 10. Subvert, Rogue relations and turn processing

## 10.1 Acquisition

An action-ready genuine Spy (ordinary or Rogue) can move its 1 movement and Subvert because it has Dash. Choose any detected eligible target within the selected Spy's connected network's reach. This includes genuine super/foreign units, Mimics and detected Cloaks. Exclude own genuine units, treaty-allied units, already-commanded Rogues, and a unit already acquired by this player in this player-turn. An uncommanded third-party Rogue is eligible under its remembered owner's hostile/treaty relation [C11].

Before commitment, target highlighting must not reveal real disguise/Mimic identity. After commitment the controller gets actual/apparent information and the genuine command menu. Subverting a Spy-shaped Mimic spends the Spy action and acquires an actual Mimic, not a relay node. No range tether persists after success. Moving out of range or losing a relay/initiator does not revoke already-acquired command during the current turn.

## 10.2 One physical action allowance, once-per-player acquisition

Each enemy physical unit has one ordinary native turn allowance available to the active player. Initialize it once for that player's turn (lazily on first acquisition is permitted), not on every ownership/costume/wrapper change. Actions used in another player's turn do not make it permanently ineligible this turn. Actual frozen/native incapacitation still constrains it.

Acquire each physical unit at most once per acting player's turn. Subvert does not create another allowance if that unit already acted under the same player-turn. Preserve native movement, Dash, follow-ups and Persist within that one allowance; a Knight can chain normal kills. Menu toggles, real/fake upgrades, transport, growth, repeated Regroup/Disguise, permanent conversion and save/load cannot reset it.

A Spy chain exchanges an initiating action for control of another unit's existing allowance. After Spy B Subverts C, B's action is spent but B can still passively relay. A later friendly Spy cannot reacquire B to get a second action. There is no arbitrary chain-length cap.

## 10.3 Rogue state

Subvert makes the target factionless for combat but remembers permanent owner, home city, support and restoration. Current command is separate. Rogues may attack any other normally targetable unit: original owner's units, commander's units, treaty partners, other Rogues. Genuine retaliation follows actual range/status/visibility and native weapon exceptions. Mimics/attackless forms gain no weapon.

Rogues cannot capture villages/cities, collect ruins or disband. They can occupy and siege any owned city, including the commander's or original owner's. They are not support recipients. Genuine commanded Mind Benders can convert Rogues permanently for the commander; support uses genuinely owned recipients only. All star costs use commander bank/tech and applicable commander's territory. Actual upgrades remain on return.

Rogue attacks/theft/Infiltrate do not break the commander's peace treaties; ordinary units do not acquire this privilege. A Spy cannot directly Subvert a treaty partner's ordinary unit. Death and other actual consequences still occur. Permanent conversion ends Rogue deniability for subsequent actions.

## 10.4 Conversion

A genuine or Rogue Mind Bender can convert an enemy or Rogue, including a Rogue under the same commander. New permanent owner is the Mind Bender's current commander. Clear pending return, preserve physical ID/actions/acquisition history, apply native support/home-city conversion once. Converted former Rogues become eligible owned support recipients without regaining actions. Converting a Mimic yields an actual Mimic and full private information to its new owner, not its apparent type.

## 10.5 Duration and start-of-turn ordering

Orders last only through the subverting player's turn. Rogue status persists until the remembered owner's next turn. Intervening multiplayer players may independently acquire it once on their own turns; re-subversion does not replace the remembered owner or create nested return stacks [C11]. After the active commander ends their turn, there is no out-of-turn controller issuing orders.

At the original owner's turn boundary:

1. Calculate/award income from the current board with Rogue sieges still present.
2. Restore living due-return Rogues not permanently converted; recompute ownership/occupation and support without duplicating reservations.
3. Perform ordinary once-per-owner-turn action/status/growth scheduling in its verified native order, ensuring returned units receive their normal fresh turn. No special stun.
4. Allow interactive pending city rewards and ordinary input.

Income precedes return unconditionally. Other native start-turn effects must occur once, not once on acquisition and again on return. Existing freeze/status rules remain native. A unit restored beneath its owner's Blackout becomes owned but inaccessible; restoration/refresh does not reveal its tile. Dead or permanently converted units never return. A Rogue siege needs no additional Disrupted tag.

## 10.6 Completion policies for neutral interactions

**[C11] Ownership and multiplayer:** preserve original return owner through temporary transfers. Support capacity and elimination regard it as reserved to that owner, not a free slot. If the owner truly resigns/is eliminated for other reasons, apply native permanent-removal cleanup to their reserved Rogues as well; do not resurrect the player or leave an immortal orphan. Apply once, after native defeat confirmation.

**[C12] Tactical context:** commander's actual technologies, treasury, road-access and territory privileges govern issued actions and upgrades. Rogues do not gain ownership-only city fortification merely by occupying someone else's city; native terrain defenses remain according to actual unit/tech context. All faction units and Rogues treat a Rogue as nonfriendly for ZOC, conversion and hostile occupancy; Rogues treat other bodies as nonfriendly for ZOC. Actual Sneak or another native bypass still works. Command permission does not waive ZOC.

**[C13] Vision for orders/retaliation:** during active command use that commander's tile mask/detection, including Rogue local sight. Between commanders, a Rogue uses its own last legitimately acquired local sight mask, not a nonexistent neutral player's global mask and not the original owner's whole map. This snapshot does not refresh or clear fog merely at turn boundaries. Normal actual weapon/visibility rules still constrain automatic retaliation. The remembered owner's mask is used only for Propaganda inspection of that Rogue.

**[C14] Kills:** increment genuine physical killer's native kill/follow-up state for actual kills, including commanded friendly-fire sacrifices; no fake kills. Track acting commander separately for native score/event attribution. Do not award a new invented hostile-elimination bonus for killing that commander's own permanent unit. Support/victory/refunds use real identities and permanent ownership. Verify native score hooks [V08/V12].

---

# 11. Spy networks

Nodes are genuine accessible Spies currently commanded by the active player: ordinary owned Spies and currently acquired Rogue Spies. A Spy-looking Mimic is not a node. Actual Spies in any disguise are nodes. An un-subverted enemy or treaty ally is not a node. A node hidden beneath the commander's Blackout is excluded, even if remembered or preselected.

Two nodes connect at native tile distance ≤2. Movement, exchange, conversion, Subvert, death and access changes immediately update topology. Moved, swapped, frozen-but-accessible and exhausted nodes may passively relay; only the chosen initiator needs a usable action. **[C15]** A freeze restricts the node's own actions, not its passive Relay, analogous to exhaustion; if a native status explicitly disables all passive abilities, verify/report the adapter rather than infer movement rules.

For selected initiator S, let C(S) be its current connected component. An ability can reach the union of native range-2 areas around all nodes in C(S). Targets are still subject to detection/eligibility. No terrain/path traversal surcharge or relay action cost; native distance is the geometry [V03]. Network cycles are permitted and do not duplicate targets/actions. No cap on hops.

Interface: select Spy → Subvert/Blackout → final red-bullseye highlighted target. No endpoint, route or opponent selector. A private optional link visualization may help, but cannot reveal enemy true nodes. All highlighted targets are revalidated on commitment.

The selected initiating Spy reveals any disguise and throws Blackout's smoke bomb directly to the final tile, even across network reach. Passive relays do not animate a shot or reveal. Clip source/path/audio to observer visibility; do not trace the hidden network.

Acquiring a genuine Spy grants its local actual Scout sight and makes it a relay. This may reveal the next enemy Spy. Acquiring a Spy-looking Mimic grants its permitted Mimic sight but no Relay or Subvert. No passive authenticity or Mimic detection is added.

---

# 12. Blackout and fog-sensitive combat

## 12.1 Target and effect

Blackout selects one in-bounds tile in the initiating Spy network's range. It may be empty, occupied, owned by any side, or inside the caster's own unexplored area. Cost is one initiating action, no stars. Dash applies. Selection: Spy → Blackout → highlighted tile → ordinary confirmation. All geometrically reachable tiles qualify without consulting enemy sight for validity.

Set that tile to ordinary native fog for all other players. **[C16]** "All opponents" means every other player, including peace-treaty partners; Blackout itself does not break a treaty. There is no selected-victim menu. This interpretation completes the earlier unspecified treaty case while preserving one globally consistent cast.

The caster's own mask is unchanged: if the tile was in the caster's fog it stays fogged. The action is valid even when every affected player already lacks sight. Spend the action once. Recasting does not add duration stacks or reveal whether any player was seeing it.

Initiating Spy throws a smoke bomb with a soft impact/smoke sound. Opponents see ordinary clouds close where entitled; caster gets no residual fog marker or tint. Propaganda is the only overlay revealing affected masks. No success/failure feedback, preview color, tooltip or cost change may distinguish preexisting enemy visibility.

## 12.2 What is blocked

For a player whose tile is covered: no orders to the unit beneath, no city recruitment/selection actions beneath, no roads/buildings/resource actions on that tile, and no use of its covered Spy as any relay node. Block map clicks, cycling, keyboard/controller commands, stored selection, unit/city lists, queued commands, AI and direct command validation alike. Fogged troops cannot be targeted as your exchange partners via a hidden shortcut.

Physical unit occupancy, native support, passive city income, siege, damage and automatic owner restoration still exist. Fog is not damage immunity, an ownership transfer, or an automatic city-income loss. A trapped unit may retaliate under §12.4. Its own Scout cannot make it commandable again by self-refresh. Indefinite isolated-asset lockout is permitted; there is no expiry, immunity or automatic rescue.

## 12.3 Rediscovery

Approved exploration sources: actual movement by another accessible unit, an exchange's actual sight update, nearby unit recruitment, Subvert/Convert acquisition sight, Explorer movement, border growth, and Embassy/native explicit discovery effects. Use the native reveal footprint for the source, not the apparent sight of a genuine disguised unit. Each player clears independently.

Do not clear merely because a nearby unit is still standing there at turn start. Moving it can produce a reveal even if it was already nearby. A newly spawned source can reveal. A covered unit/city cannot reveal itself through rendering, saving, growth, ownership restoration, status refresh, unit list selection, or passive turn-start scans. The uncovered unit regains whatever actions it already had; discovery spends the rescuer's ordinary action/movement, not the rescued unit's.

Attack, damage, retaliation and disguise removal alone are not fog-clear events. If an attack separately causes actual movement, spawning or control acquisition, only that legitimate source updates its entitled player's map. Movement by the attacker does not automatically reveal the attacker to the defender's player.

**[C17] Implementation distinction:** internal bookkeeping may record when/why a tile was re-fogged to suppress self-refresh; this is not a new player-facing fog category. Prefer source-scoped native reveals. A global native recompute must exclude covered self-sources and must not accidentally reveal every Blackout tile from static positions. A legitimate new source is different from reusing a covered source's old existence.

## 12.4 Retaliation

Use actual combat weapons/statuses. A defender may retaliate only if its relevant side can see/detect the attacker at the response check. Its own tile being covered does not by itself prohibit automatic retaliation.

- A commands an attacker hidden from B by tile fog. It attacks visible B defender. B cannot see attacker, so no retaliation. Attack alone does not clear B fog.
- A can see B defender, but B cannot access its own defender tile. B can still see A attacker elsewhere. B defender can retaliate; its own tile stays covered.
- If B cannot see A attacker either, no retaliation.
- Every later strike checks current actual visibility; there is no automatic first-strike-only protection. A separate discovery can change the outcome.

Direct attacking strips a genuine disguise; if that removes Cloak concealment on a tile B can already see, detection may now permit retaliation. Tile Blackout still blocks it. Never equate unit concealment reveal with tile exploration. Splash can injure units under fog; positive damage strips genuine victims' masks but does not expose their tile.

**[V04]** These are required custom outcomes, not a verified claim about current native fog/retaliation callback order.

---

# 13. Propaganda

Propaganda has two independent benefits: expanded recruitment/feigned-naval appearance access (§5/7) and inspection of opponent tile visibility. It grants no range bonus, no genuine technology/weapon, and no passive disguise detection. It requires no Spy.

After researching it, select a visible opponent unit or city. Over tiles that the inspecting player can see, display translucent fog showing where that selected player's effective mask lacks access. Ordinary own fog remains opaque and supplies no information about that opponent's exploration there. Selecting a different opponent's unit/city switches the mask; deselecting removes the overlay. No map button or player-picker is added.

A selected Rogue uses its remembered original owner's mask, not the current commander's or a neutral map. **[C18]** Peace-partner unit/city selections may also show that partner's mask: inspection is information, not an attack. Do not extend ordinary play into omniscient spectator access. Own non-Rogue selection opens the normal private unit interface instead.

The overlay reports actual tile access, not memory, belief, Cloak detection, Mimic authenticity or whether a player noticed a tell. Blackout/rediscovery updates it live. Selecting apparent enemy information never consults true hidden identity for overlay behavior. No enemy-side interface changes.

---

# 14. UI, animations and accessibility contract

Reuse the game's normal visual language; add only the needed assets and private distinctions. All art/sound is observer-filtered. "Same animation" means identity/swap choices must not alter unauthorized timing, spatial traces or side-channel labels.

| Event | Commander sees | Ordinary observer sees |
|---|---|---|
| Mimic recruit | Actual Mimic icon, form picker, 3-star payment | Apparent unit spawning; no naked Mimic frame |
| Actual/Appearance toggle | Clearly titled mode; both truths; appropriate commands | Nothing changes on map |
| Regroup/Disguise | Identities and atomic Keep/Swap preview | Shared transformation then maybe-shuffle; no action/initiator label |
| Genuine reveal | True unit/info and poof | Poof/current actual form only if visible |
| Mimic death | Actual death plus apparent disappearance | Apparent unit dies/poofs; no surviving bare body |
| Fake Heal/Swarm | Explicit Feign label and apparent-only preview | Ordinary-looking support animation, no fake label |
| Genuine paid hidden upgrade | Actual cost/type update | Existing independent apparent form persists; no real-model flash |
| Feigned naval upgrade | 0-star Feign label | Apparent native upgrade animation |
| Smuggle | Native harvest button; true home-city info | Resource/pop animation within view; no identity reveal tag |
| Rogue | Real command menu; light-red tint/red confused bolts | Same visible Rogue state over apparent form |
| Subvert | Red bullseyes on eligible network targets | Initiator's specialist action, newly visible Rogue status |
| Blackout | Initiator throw, no residual marker | Smoke impact and ordinary fog according to access |
| Propaganda | Selected faction's translucent mask on own-visible tiles | No change |

**[C19] Minimal Subvert VFX:** use a distinct brief influence pulse from initiating Spy toward the target, followed by Rogue tint/bolts. Unlike Blackout it adds no fog. Do not animate every relay. Tint must remain distinct from actual damage/poison. Exact sprite art may evolve without changing disclosure, target or timing semantics.

Required assets: research icons for all five techs; bare Mimic info/recruit portrait and private badge; Spy map/info sprite; Regroup/Disguise action icons and shared effects/sound; reveal/death poof; Rogue tint/bolts; Subvert action/pulse; Blackout smoke bomb/impact/sound; subtle Actual/Appearance and Feign UI labels; Propaganda translucent fog treatment. Mimic Boats need private identification but no visible generic boat requirement. Reuse apparent native models/skins/animations rather than make a naked battlefield Mimic.

Native-style target bullseyes and upgrade/harvest controls are reused. Do not add persistent authenticity markers, enemy-ready icons, network endpoint selectors, Propaganda map buttons or special Blackout markers. Keyboard/controller/accessibility equivalents must preserve the same mode/command distinctions and secrecy as mouse/touch.

**Observer-equivalence requirement:** with identical visible histories, presentations, public status and accessible tiles, swapping only hidden identities/actions must not alter unauthorized pixels, sound locations, tooltip values, selection mapping or UI enablement. Actual mechanical interactions may subsequently distinguish the worlds. Ownership/Rogue markers and deliberately different actual movement/damage remain legitimate evidence, not a promise of universal indistinguishability.

---

# 15. Authoritative event and transaction contract

These are abstract event names, not asserted engine APIs. They state ordering that the implementation must deliver.

## 15.1 Commit pipeline

1. Resolve selected stable unit and explicit command kind (real or feigned), current commander and actual allowance.
2. Validate current positions, command access, current network, legal targets/outcomes, actual research/territory and exact payment. Never authorize from menu mode alone.
3. Before reveal, validate concealment-dependent real actions such as Infiltrate. Reserve/apply costs and actual state transition once.
4. Apply the approved global actor reveal, then the already-committed genuine effect. A reveal cannot invalidate its own legal command.
5. Perform native effects through split actual-unit, apparent-terrain and presentation adapters. Never call harmful routines for fake effects.
6. Commit final state atomically; enqueue authorized observer events, dependent sight/topology/city updates and native rewards once. If interrupted before commitment, no effect/cost/reveal; after commitment, reload/replay resumes deterministically without repeating it.

## 15.2 Event ledger

| Event | Spends/changes allowance | Movement passives | Sight update | Disguise effect |
|---|---|---|---|---|
| True movement | Native actual allowance | Actual unit effects + apparent terrain once | To moving side | Preserve source |
| Normal port boarding | Native exhaust/transform | Native qualifying movement/boarding output | Native entitled source | Preserve independent mask via apparent transport |
| Atomic exchange | Initiator spent; partner exactly preserved | None | Final actual source sight for each entitled party | Copy/transfer appearance |
| Genuine direct attack | Native actual attack/follow-ups | Only true separate native triggers | No attack-only exploration | Reveal actor; positive damage reveals victims |
| Genuine specialist | Native corresponding action | Native actual ability | Only approved separate discovery | Reveal actor |
| Feigned support | Corresponding actual allowance | No true recipient effects | No invented discovery | Preserve source; update apparent recipients |
| Real naval upgrade | Native cost/timing; no refresh | No invented movement event | No bookkeeping-driven Blackout clearance | Preserve independent mask |
| Feigned naval upgrade | 0 stars; corresponding native timing; no refresh | No damage/movement event | Mimic true sight may change; genuine sight does not | Update apparent vessel |
| Subvert/Convert | Initiator cost; acquire/transfer target without duplicate budget | No movement event | New entitled local source | Subvert preserves target appearance; Convert preserves it |
| Blackout | Initiator action | None | Remove one tile from other masks | Reveal real specialist actor only |
| Return from Rogue | Ordinary owner refresh once | None | No self-clear under fog | Preserve appearance unless earlier actual reveal |
| Save/load/UI refresh | None | None | None beyond rebuilding authorized caches | Preserve stored state |

**[C20] Sight-profile changes:** a legitimate change in an accessible Mimic's actual copied sight may run a source-scoped native sight update; a genuine unit changing appearance alone cannot gain apparent Scout sight. A covered source cannot invoke this to rescue itself. A paid actual upgrade that creates a genuinely different accessible sight profile may update that source under the same rule; the upgrade menu/render/constructor alone is not a reveal event. Never refresh every player's map from every stationary unit.

## 15.3 Required invariants

- Exactly one actual identity and native home assignment per logical unit; no anonymous independently acting copy on appearance change.
- Mimic actual HP ≤1 and alive iff HP >0; no real borrowed damage/retaliation/unit effects.
- Appearance equality never erases independence. Map sprite mode never grants command authorization.
- One physical per-player-turn allowance; one Subvert acquisition per player-turn; native allowed follow-ups are not extra acquisitions.
- Partner state before exchange equals after exchange, except physical position/form and explicit independent appearance; initiator remains spent after any wrapper reconstruction.
- No real effects arise from animation completion callbacks for feints.
- Ownerless Rogue still reserves support/return identity; command is not support friendship.
- Income-before-return; dead/converted/eliminated-owner units do not reappear; no self-clearing fog at restoration.
- Actual passenger never becomes apparent passenger; no genuine weapon/refund from a free feint.
- Each population/cost/reward/death/visibility event applies once.
- No observer reads hidden actual fields through presentation, readiness, predictive UI or stale selections.

---

# 16. Completion register: explicit policies added to close implied cases

The core rules above were selected through the conversation. This table distinguishes final handoff defaults from purported quotations of prior agreement. Implement these defaults unless Alex changes them; do not treat them as unexplained native behavior.

| ID | Explicit finalization choice |
|---|---|
| C01 | Split borrowed terrain routines from direct occupant effects; preserve native terrain eligibility. |
| C02 | Apparent growth copies remaining phase/timer; no extra tick from temporary control or reconstruction. |
| C03 | Copy one body, not entire linked armies; no genuine spawned offspring from a Mimic. Compound adapters must be audited. |
| C04 | Real/apparent healing advances each record independently; fake HP does not normalize through a non-revealing real heal. |
| C05 | Zero damage preserves target disguise; direct genuine actor still reveals. |
| C06 | Reveal of an actual Cloak uses native detected state; no same-frame disappearance or fog clearing. |
| C07 | Land/sea apparent HP/cargo follows native form conversion separately from actual state; no fictitious passengers. |
| C08 | A paid hidden upgrade with unchanged appearance has no public true-type transformation. |
| C09 | Enemy city rewards queue to owner; income/return occur before interactive start-turn decisions. |
| C10 | Rogue Starfish collection is allowed with commander's Navigation; ruins remain prohibited. |
| C11 | Re-subversion keeps original return owner; true owner elimination applies native cleanup to reserved Rogues. |
| C12 | Commander supplies tech/roads/territory for orders; Rogue remains nonfriendly for ZOC/support/occupation. |
| C13 | Active Rogue retaliation uses commander's effective visibility; idle Rogue uses retained legitimate local sight, not an invented global neutral map. |
| C14 | Physical actual kills drive native kill/follow-up state; no fake or newly invented friendly-sacrifice score bonus. |
| C15 | Passive accessible Spy relay survives freezing/exhaustion; initiator still needs actual ability eligibility. |
| C16 | All other players, including treaty partners, receive Blackout; this cast does not break treaties. |
| C17 | Source-scoped exploration guards ordinary fog from accidental self/turn-start recomputation. |
| C18 | Treaty-partner unit/city selection can be inspected by Propaganda; Rogue selection uses remembered owner. |
| C19 | Subvert uses a minimal influence pulse and Rogue cue; Blackout uses initiator smoke bomb; no hidden relay trace. |
| C20 | A legitimate accessible actual sight-profile change may refresh that source; menu/render changes do not. |

Numeric native semantics remain data dependencies, not missing custom balance rules. If a native form cannot meet the compound-body or terrain-safety contract, record the exact unsupported case and implement its adapter before claiming full support. Do not quietly remove requested forms or allow genuine effects as an easier fallback.

---

# 17. Native integration checks and release gates

These checks have NOT been performed. The specification fixes outcomes but does not assert available code hooks.

| ID | Record/verify before claiming completion |
|---|---|
| V01 | Exact Steam game build, mod-loader version/architecture, dependency list, ordinary-tribe registry and custom-ID registration. |
| V02 | Action flags and native Dash/Persist/follow-ups, recruitment exhaustion, conversion/transport constructors and stable logical IDs. |
| V03 | Native range/adjacency metric and detection; use one consistent distance implementation for range-1 exchange and range-2 network/abilities. |
| V04 | Per-player fog storage, source-scoped reveal hooks, unit concealment, combat/retaliation callbacks and no-self-reveal guards. |
| V05 | Real naval upgrade costs/research/timing, normal boarding exhaustion, HP/cargo transition, disembarkation, and action-preserving conversion path. |
| V06 | Native home-city/support reassignment, city ownership transfer, harvest/pop queues and missing-pointer recovery. |
| V07 | City siege and income hook order, native deferred level-up choice queue, and player turn callbacks. |
| V08 | Mind Bender/real support, ownership predicates, Rogue ZOC/roads/defense/refunds/score and true elimination hooks. |
| V09 | Cloak hide/detect/eye/Infiltrate requirements, transferred invisibility, genuine reveal sequencing and spawned-Dagger owner. |
| V10 | Current apparent Heal/Recover/Swarm numbers/expiry, status rendering and independent actual/apparent HP conversion. |
| V11 | All allowed foreign/passive/growth forms; terrain-versus-unit output split, fruitless-algae helper, aquatic legality, segments and generation. |
| V12 | Battle preview/sweat, all unit info/readiness fields, public scores/cost breakdowns, camera/audio/replay/selection disclosure. |
| V13 | Save schema, migration/version rejection, deterministic commands/replay and crash-safe pending transactions. |
| V14 | AI observation/action APIs and fail-safe scoring of custom abilities; avoid true-type omniscience. |
| V15 | Hot-seat/private multiplayer serialization and viewpoint filtering, same-mod compatibility, spectator/endgame policy and debug privacy. |
| V16 | Art/font/icon/sound reuse permissions and final assets, resolution/scaling/input accessibility. No bundled font files are required. |

Do not claim online compatibility or secrecy merely because local UI works. Peers/replays may carry full simulation state; hiding labels is not cryptographic anti-cheat. Establish supported modes explicitly. Minimum development harness: deterministic local/hot-seat fixtures with viewpoint switching, recorded commands and visibility-limited rendering. Full release requires tests for every advertised mode and form; incremental milestones may label incomplete coverage honestly.

AI must use permitted enemy observations and maintain only legitimate remembered information. It may reason from witnessed moves/damage but must not query hidden true types to score an attack, select a Subvert target or choose a defense. Debug omniscient panels must be disabled in normal play and excluded from opponent replay perspectives.

---

# 18. Implementation order and do-not-regress list

Suggested order: native audit/test harness → sixth tree and state schema → base Mimic recruitment/movement/capture → Actual/Appearance UI and observer filtering → atomic exchanges/Smuggle → true/app naval adapters → concealment/terrain/support feints → Rogue/Subvert/action ledger → Relay/Scout → Blackout/Propaganda → compound compatibility, AI, saves/replays and multiplayer.

Keep capabilities configurable for balance testing, but do not change frozen values/interactions silently. A dangerous combo can be a successful acceptance test and a balance concern at the same time.

Do not reintroduce: Mythmaking/Forgery/Illusion names; Propaganda exchange-range bonus; Spy movement 2/no Dash; stationary or action-spending relays; partner movement costs; start-on-resource Smuggle; independent immutable origin; no enemy-city Mimic capture; Rogue captures or heal-all; end-of-caster-turn return; income-after-return; special fog/Jamming tags/automatic timeout; attack-only fog clearance; passive Mimic detection; fake damage; a living naked Mimic; enemy readiness/shuffle trackers; a once-per-voyage imitation quota; free apparent weapons/disarm flags; paid upgrades destroying an independent appearance; automatic actual/apparent field coalescing; extra menu action budgets; Inspector/Watchpost/Bandit systems.

Keep a change log against DF-1 for any deliberate balance or semantics changes. Re-run related cases plus the combined regressions. A code shortcut that changes behavior is a design change, not merely an implementation detail.

---

# 19. Provenance and external verification limits

Custom gameplay is defined by Alex's conversation and the final approval following revision 1.4. That prior file was read in full for reconciliation; this document is a clean replacement, not an appended series of conflicting drafts. No game code, binaries or mod APIs were inspected and no gameplay acceptance cases were executed during this documentation pass.

Primary official references re-opened on 14 September 2026 provide native context only:

- **S1 — Midjiwan, Polytopia Diplomacy.** Native Cloak stealth/Infiltrate and treaty/Embassy context; not an exact current concealment state-machine specification. `https://polytopia.io/diplomacy/`
- **S2 — Midjiwan, Path of the Ocean.** Raft transport and friendly-territory naval upgrades, Starfish, and actual naval area attacks. `https://polytopia.io/path-of-the-ocean/`
- **S3 — Midjiwan, Bug fixes in version 2.16.3, 16 February 2026.** Updates include hidden-unit hints, status/splash visuals, capture, previews, ruins and replay behavior. Not claimed to be the user's installed version. `https://polytopia.io/bug-fixes-in-version-2-16-3/`
- **S4 — Midjiwan, It's Time for the Cymanti Rework, 29 October 2025.** Living Island's fruitless algae, Shaman Swarm naming, and native growth/damage carryover are relevant adapter checks. `https://polytopia.io/its-time-for-the-cymanti-rework/`

Those sources do not establish this mod's balance, exact hooks, instant exchange-capture behavior, Rogue semantics or custom fog-retaliation outcome. Treat native implementation checks as build-specific. All case outcomes below are normative custom requirements unless expressly described as a native-adapter measurement.


---

# 20. Graded acceptance cases

**180 cases · all NOT RUN.** These are expected results for a future implementation, not executed tests. The same cases are embedded in the final specification and provided in JSON.

## Fixture conventions

- **players:** P is acting player; Q and R are other players unless a case says otherwise.
- **ready:** Fully unused actual movement and primary action; native follow-ups tracked independently.
- **actual_vs_apparent:** An actual/apparent pair is independent unless explicitly normal/unmasked.
- **native:** Values/eligibility from the pinned build adapter, not invented constants. Native upgrade timing is measured before sequence assertions.
- **observations:** Every case requires both authoritative-state checks and viewpoint-filtered presentation checks.
- **coordinates:** Straight-line examples use native distance; do not assume Euclidean geometry or undocumented diagonal rules.
- **reset:** Alternative branches named in one case begin from identical reset fixtures; do not grant in-match action refreshes to run alternatives.
- **scope:** A source form in a fixture must have a tested adapter. Missing adapter is NOT IMPLEMENTED, not PASS.

For each case, record build, seed/map/save, commands, actual state diff, each relevant player view, PASS/FAIL/NOT IMPLEMENTED, and evidence path. A visually convincing but mechanically wrong outcome fails; a mechanically correct identity leak also fails.

## Level 1 — Foundation and single-unit rules

### DEC-001 — Sixth branch and prerequisites
**Rules:** §1. **Status:** NOT RUN.

**Setup:** Ordinary tribe; all research initially unowned.
**Do:** Open research and buy each branch path.
**Actual result:** Five extra technologies follow Deception→Misdirection→Propaganda and Deception→Camouflage→Espionage. Native branches remain present and unchanged.
**Player-visible result:** Research displays the specified names and dependency edges.
**Must not happen:** Replacing a native tech or allowing Espionage through Misdirection alone.

### DEC-002 — Prices and no Propaganda range bonus
**Rules:** §1, §13. **Status:** NOT RUN.

**Setup:** Two cities and a known native discount fixture.
**Do:** Compare each custom research price to the native formula for its tier; research Propaganda.
**Actual result:** Tier/city/discount calculation matches native. Exchange range remains 1. Spy range/link remains 2.
**Player-visible result:** Tooltip and targeting agree on those ranges.
**Must not happen:** A hidden +1 exchange-range bonus.

### DEC-003 — Mimic recruit
**Rules:** §1, §5. **Status:** NOT RUN.

**Setup:** City has a vacant recruit slot, Deception and exactly 3 stars; Warrior form unlocked.
**Do:** Recruit a Warrior-shaped Mimic.
**Actual result:** Exactly 3 stars spent; one actual Mimic, 1 HP, attack/defense 0, native support and recruitment flags.
**Player-visible result:** Opponent sees Warrior spawn. Controller sees apparent Warrior plus private Mimic badge/info.
**Must not happen:** Naked map frame, free real Warrior, or immediate recruitment action.

### DEC-004 — Cancel recruitment
**Rules:** §5, §15. **Status:** NOT RUN.

**Setup:** Same recruit fixture with 3 stars.
**Do:** Choose Mimic and an appearance, then cancel before commitment.
**Actual result:** No stars, slots, units or fog change.
**Player-visible result:** Picker closes without a spawn cue.
**Must not happen:** Charging or publishing an appearance before confirmation.

### DEC-005 — Appearance recruitment gate
**Rules:** §1, §5. **Status:** NOT RUN.

**Setup:** Catapult unresearched; Deception owned; no Propaganda.
**Do:** Inspect picker, then research Propaganda and inspect again.
**Actual result:** Catapult absent before and present after; real Catapult research remains unowned.
**Player-visible result:** Only the controller sees the picker/research permission.
**Must not happen:** Propaganda granting actual Catapult technology.

### DEC-006 — Forbidden direct forms
**Rules:** §5, V01. **Status:** NOT RUN.

**Setup:** Deception and Propaganda; relevant special forms registered.
**Do:** Inspect recruit picker.
**Actual result:** Eligible city-origin super forms appear; Daggers/adult dragons/Moth eggs/direct ships/naked Mimic do not.
**Player-visible result:** No placeholder invalid form button.
**Must not happen:** Treating every registered unit as city-recruitable.

### DEC-007 — Mimic Rider movement and no weapon
**Rules:** §2, §3. **Status:** NOT RUN.

**Setup:** Ready Rider-shaped Mimic on open terrain.
**Do:** Inspect movement and attack commands.
**Actual result:** Uses represented Rider movement/profile but has no real attack or retaliation.
**Player-visible result:** Enemy menu shows apparent Rider traits. Private actual menu identifies no genuine weapon.
**Must not happen:** Using fake attack to trigger genuine attack-dependent escape.

### DEC-008 — Positive damage kills a Mimic
**Rules:** §4. **Status:** NOT RUN.

**Setup:** Mimic shows apparent 40/40 Giant HP.
**Do:** Apply 1 positive actual damage using a controlled fixture.
**Actual result:** Actual HP reaches 0; unit removed once.
**Player-visible result:** Apparent unit disappears/poofs; no living naked Mimic.
**Must not happen:** Survival because apparent HP was high.

### DEC-009 — Genuine disguised movement
**Rules:** §3. **Status:** NOT RUN.

**Setup:** True Giant with independent Rider appearance.
**Do:** Inspect true movement and move within legal actual range.
**Actual result:** Movement uses Giant definition, not Rider.
**Player-visible result:** Enemy sees Rider motion and may infer the contradiction; no automatic identity reset.
**Must not happen:** Granting Rider movement to a genuine Giant.

### DEC-010 — First click and repeated-click toggle
**Rules:** §2.4. **Status:** NOT RUN.

**Setup:** Controlled true unit with an independent appearance.
**Do:** Select unit. → Click same selected unit. → Click it again.
**Actual result:** Panel modes are Actual, Appearance, Actual; no state/cost/action change.
**Player-visible result:** Map stays apparent throughout; panel names both forms privately.
**Must not happen:** Timed double-click requirement or true map sprite flash.

### DEC-011 — Selection reset and targeting guard
**Rules:** §2.4. **Status:** NOT RUN.

**Setup:** Appearance mode open.
**Do:** Select another unit then return. → Start a targeted command and click its pending target.
**Actual result:** Fresh selection opens Actual. Pending target clicks belong to the command flow.
**Player-visible result:** No unexpected mode flip while selecting targets.
**Must not happen:** Toggling modes mid atomic exchange or spending actions by inspecting.

### DEC-012 — Hidden enemy readiness
**Rules:** §2.3, §14. **Status:** NOT RUN.

**Setup:** Two identical apparent enemy Warriors; one ready, one exhausted.
**Do:** Observe, reselect, cross turn boundary and reload.
**Actual result:** Authoritative flags differ and remain correct.
**Player-visible result:** Ordinary observers see identical neutral readiness, no grayed command/tooltip leak.
**Must not happen:** Dimming only the exhausted unit or only Mimics.

### DEC-013 — Actual and apparent equal is still independent
**Rules:** §2, §7.3. **Status:** NOT RUN.

**Setup:** Genuine Raft receives Raft appearance via a Mimic.
**Do:** Select/toggle its menus.
**Actual result:** Independent appearance flag exists despite equal type names.
**Player-visible result:** Actual paid path and eligible Appearance feigned path are distinct.
**Must not happen:** Coalescing type IDs and deleting the second menu.

### DEC-014 — Private panel is not revelation
**Rules:** §2.4. **Status:** NOT RUN.

**Setup:** True Giant appears Warrior.
**Do:** Open Actual info repeatedly without using an action.
**Actual result:** Appearance unchanged.
**Player-visible result:** Controller sees Giant truth; opponent sees Warrior without poof.
**Must not happen:** Reveal caused by inspection.

### DEC-015 — Genuine Spy stats and Dash
**Rules:** §1, §10. **Status:** NOT RUN.

**Setup:** Unspent genuine Spy on a legal route with a target within final range.
**Do:** Move one tile then Subvert.
**Actual result:** One native movement allowance used; Subvert legal via Dash; source action spent.
**Player-visible result:** Spy stats are 8 stars, 10 HP, 0 attack, 1 defense, movement 1 and Scout.
**Must not happen:** Old stationary/no-Dash restriction or movement 2.

## Level 2 — Presentation, healing and direct revelation

### DEC-016 — Direct attack reveals source
**Rules:** §4.3. **Status:** NOT RUN.

**Setup:** Ready Giant appearing Warrior attacks a genuine enemy.
**Do:** Commit genuine attack from Actual mode.
**Actual result:** Mask clears before actual combat; real Giant statistics determine result.
**Player-visible result:** Poof then genuine attacker where observable.
**Must not happen:** Warrior damage used because the map looked like Warrior.

### DEC-017 — Damage reveals target
**Rules:** §4. **Status:** NOT RUN.

**Setup:** Genuine Giant appears Warrior with false HP.
**Do:** Deal positive actual damage.
**Actual result:** Real Giant takes real loss; independent overlay removed globally.
**Player-visible result:** Visible observers see actual form/HP and damage feedback.
**Must not happen:** Damage subtracting from false HP instead of true HP.

### DEC-018 — Zero-damage target stays masked
**Rules:** §4.3, C05. **Status:** NOT RUN.

**Setup:** Genuine disguised target; instrumentation forces 0 HP loss with no other reveal action.
**Do:** Resolve incoming direct hit.
**Actual result:** Target retains mask; attacking genuine actor still reveals itself if masked.
**Player-visible result:** No target poof; appropriate zero-damage/status feedback only.
**Must not happen:** Treating any targeted command as positive damage.

### DEC-019 — Poison/freeze without damage
**Rules:** §3, §4. **Status:** NOT RUN.

**Setup:** Genuine disguised unit and a Mimic in separate fixtures.
**Do:** Apply real nondamaging poison or freeze.
**Actual result:** Native real status applies without identity reset; Mimic remains 1 HP.
**Player-visible result:** Normal apparent status presentation, not a naked/true model.
**Must not happen:** Status application alone stripping disguise or status cure through copying.

### DEC-020 — No explicit authenticity record
**Rules:** §2.2, §8. **Status:** NOT RUN.

**Setup:** Known Mimic has visibly Smuggled.
**Do:** Opponent reselects it and later watches it move.
**Actual result:** Still actual Mimic with unchanged apparent form.
**Player-visible result:** Opponent sees ordinary apparent type, no known-fake label; memory is human.
**Must not happen:** Persisted enemy true-type access because a tell occurred.

### DEC-021 — Prediction versus real combat
**Rules:** §4.4. **Status:** NOT RUN.

**Setup:** Enemy true Giant wears Warrior appearance; an attacking Knight would kill the apparent Warrior in the fixture.
**Do:** Inspect sweat/preview, then commit attack.
**Actual result:** Preview uses apparent target; actual resolution uses Giant.
**Player-visible result:** Apparent lethal indicator before commitment; actual reveal afterward.
**Must not happen:** Free true-stat leak from hover or forcing real result to match preview.

### DEC-022 — Independent health snapshots
**Rules:** §4.1. **Status:** NOT RUN.

**Setup:** Mimic copies injured genuine unit showing 6/10.
**Do:** Heal only one participant with a permitted event.
**Actual result:** Only that participant's true/apparent records evolve; other remains unchanged.
**Player-visible result:** No mirrored updates on the distant copy.
**Must not happen:** Shared mutable HP/status pointer between apparent forms.

### DEC-023 — Feign Heal on actual-full target
**Rules:** §4.2. **Status:** NOT RUN.

**Setup:** Owned target actual 10/10, apparent 4/10; fake Mind Bender ready.
**Do:** Use Feign Heal for represented amount H.
**Actual result:** Target actual remains 10/10; apparent becomes min(10,4+H); actor pays native action.
**Player-visible result:** Normal Heal VFX; controller sees explicit feint and both HP values.
**Must not happen:** Hiding the action solely because actual HP is full.

### DEC-024 — Real healing does not normalize false HP
**Rules:** §4.2, C04. **Status:** NOT RUN.

**Setup:** Owned target actual 4/10, apparent 8/10; native heal amount fixture H=4.
**Do:** Use genuine Heal.
**Actual result:** Actual 8/10 and apparent 10/10; genuine caster reveals; recipient retains mask.
**Player-visible result:** Apparent target heals normally; actual panel stays correct.
**Must not happen:** Snapping apparent HP to 8/10 merely because a real heal occurred.

### DEC-025 — Feign Swarm grants no mobility
**Rules:** §4.2. **Status:** NOT RUN.

**Setup:** Genuine Warrior with native movement N, eligible fake Shaman nearby.
**Do:** Apply Feign Swarm then inspect/legal-move Warrior.
**Actual result:** Real movement stays N; apparent Swarm status follows native represented expiry.
**Player-visible result:** Enemy sees convincing Swarm cue; controller sees it is apparent only.
**Must not happen:** Real extra movement, fake early expiry, or cured true status.

### DEC-026 — Same support type offers real and fake
**Rules:** §2.4, §4.2. **Status:** NOT RUN.

**Setup:** Genuine Mind Bender wears independent Mind Bender appearance; eligible target.
**Do:** Compare Actual Heal and Appearance Feign Heal in separate reset fixtures.
**Actual result:** Real version heals actual recipients/reveals actor; feint only changes appearance/preserves actor.
**Player-visible result:** Modes clearly distinguish actions privately; public supported action looks normal.
**Must not happen:** Automatically substituting genuine Heal for a chosen feint.

### DEC-027 — One budget across menus
**Rules:** §2.4. **Status:** NOT RUN.

**Setup:** Unit has a real specialist and a permitted feigned action.
**Do:** Spend action on feint, toggle to Actual, attempt full genuine specialist.
**Actual result:** Second full action rejected; allowance already spent.
**Player-visible result:** True controller readiness stays spent in both modes.
**Must not happen:** A per-menu action refresh.

### DEC-028 — Actual-full Mimic apparently recovers
**Rules:** §4.2. **Status:** NOT RUN.

**Setup:** Mimic actual 1/1, apparent injured form, native Recover conditions satisfied.
**Do:** Use apparent Recover.
**Actual result:** Actual remains 1/1; represented HP recovers; correct action cost consumed.
**Player-visible result:** Normal apparent recovery and private truthful HP.
**Must not happen:** No recovery button or actual HP growth beyond one.

### DEC-029 — Native disband refund remains actual
**Rules:** §7.3. **Status:** NOT RUN.

**Setup:** Genuine cheap unit wears expensive appearance; separate free-upgraded Mimic Boat fixture.
**Do:** Inspect and execute eligible ordinary disband.
**Actual result:** Refund uses actual native paid investment; free feint adds none.
**Player-visible result:** Private refund correct; no fake-value economic reward.
**Must not happen:** Refund for a Giant/Bomber that was never actually acquired.

### DEC-030 — Post-damage Cloak state
**Rules:** §4.3, C06. **Status:** NOT RUN.

**Setup:** Actual Cloak wore Warrior appearance on an observer-visible tile.
**Do:** Inflict positive damage.
**Actual result:** Mask clears and native detected/revealed state is applied; future hiding follows native policy.
**Player-visible result:** Poof does not immediately vanish into a contradictory same-frame hidden state.
**Must not happen:** Clearing tile fog or granting an action with reveal.

## Level 3 — Atomic exchanges and action preservation

### DEC-031 — Regroup Keep
**Rules:** §6. **Status:** NOT RUN.

**Setup:** Ready Warrior-form Mimic adjacent to genuine Rider.
**Do:** Regroup, Keep.
**Actual result:** Mimic becomes Rider-shaped in place and spends action; Rider state unchanged.
**Player-visible result:** Both appear Riders with shared effect; controller knows identities.
**Must not happen:** Changing real partner or spending partner allowance.

### DEC-032 — Regroup Swap
**Rules:** §6. **Status:** NOT RUN.

**Setup:** Same fixture as preceding case.
**Do:** Regroup, Swap.
**Actual result:** Positions exchange; partner flags unchanged; final Mimic spent.
**Player-visible result:** Same public maybe-shuffle as Keep.
**Must not happen:** Different public sound/timing exposes swap choice.

### DEC-033 — Disguise Keep
**Rules:** §6. **Status:** NOT RUN.

**Setup:** Ready Warrior-form Mimic beside true Giant.
**Do:** Disguise, Keep.
**Actual result:** Giant becomes Warrior-looking at same tile; true Giant stats/flags preserved.
**Player-visible result:** Two apparent Warriors; no source-Mimic marker.
**Must not happen:** Giant receives Warrior movement/attack or extra HP.

### DEC-034 — Disguise Swap
**Rules:** §6. **Status:** NOT RUN.

**Setup:** Same fixture as preceding case.
**Do:** Disguise, Swap.
**Actual result:** True Giant moves to Mimic tile wearing Warrior mask; Mimic spent at old Giant tile.
**Player-visible result:** Same two shared stages as Regroup; no action-name popup.
**Must not happen:** Partner charged movement because it relocated.

### DEC-035 — Copy the costume, not the body
**Rules:** §6.1. **Status:** NOT RUN.

**Setup:** True Giant already looks Warrior; Rider-shaped Mimic adjacent.
**Do:** Regroup with Giant.
**Actual result:** Copy Warrior snapshot, not Giant identity.
**Player-visible result:** Both display Warrior after transfer.
**Must not happen:** Peeking at true partner definition for Regroup.

### DEC-036 — Any owned super/foreign form
**Rules:** §1, §5, §6. **Status:** NOT RUN.

**Setup:** No Propaganda; player owns a true Giant and a converted special unit in separate fixtures.
**Do:** Regroup with each.
**Actual result:** Their apparent forms copy without additional research.
**Player-visible result:** Correct copied form, no tech-gate refusal.
**Must not happen:** Reintroducing Mythmaking/Propaganda gate on copying.

### DEC-037 — Partner acted already
**Rules:** §6.2. **Status:** NOT RUN.

**Setup:** Genuine partner has moved/attacked; ready adjacent Mimic.
**Do:** Exchange partner.
**Actual result:** Partner remains equally spent; exchange allowed.
**Player-visible result:** Enemy readiness remains neutral.
**Must not happen:** Rejecting exhausted partners or giving them another attack.

### DEC-038 — Frozen partner
**Rules:** §6. **Status:** NOT RUN.

**Setup:** Frozen genuine unit beside ready unfrozen Mimic.
**Do:** Regroup/Swap.
**Actual result:** Frozen partner relocates; stays frozen and keeps flags.
**Player-visible result:** Approved frozen presentation continues without thaw/reveal.
**Must not happen:** Blocking all frozen partners or curing freeze on reconstruction.

### DEC-039 — Frozen initiator cannot act
**Rules:** §2, §6.1. **Status:** NOT RUN.

**Setup:** Actual frozen Mimic with ready-looking apparent snapshot.
**Do:** Attempt Regroup.
**Actual result:** Rejected by actual frozen action restriction.
**Player-visible result:** Private inability truthful, no enemy true readiness disclosure.
**Must not happen:** Apparent readiness authorizing a frozen actor.

### DEC-040 — Moved Mimic can initiate
**Rules:** §6. **Status:** NOT RUN.

**Setup:** Mimic can move into range of a partner.
**Do:** Move then Disguise.
**Actual result:** Legal; actor ends spent; partner state unchanged.
**Player-visible result:** Ordinary movement plus shared disguise sequence.
**Must not happen:** Smuggle no-Dash restriction incorrectly applied to Regroup.

### DEC-041 — Ready artillery delivery
**Rules:** §6. **Status:** NOT RUN.

**Setup:** Ready genuine Catapult beside a relay of action-ready Mimics.
**Do:** Exchange Catapult forward, then use genuine attack without walking.
**Actual result:** Attack remains available and reveals Catapult; every Mimic initiator spent.
**Player-visible result:** Appearance ambiguity lasts until real attack; no premature spent tint.
**Must not happen:** Setting has_moved on Catapult or refreshing after it fires.

### DEC-042 — Fake-only continuation of a chain
**Rules:** §6.3. **Status:** NOT RUN.

**Setup:** True Giant and several Giant-form Mimics.
**Do:** Move Giant for first link; leave it; continue later exchanges only between Mimics.
**Actual result:** Giant remains at chosen intermediate tile; remaining links use actual Mimics.
**Player-visible result:** No UI metadata tells observers where real Giant stopped.
**Must not happen:** Requiring a genuine partner at each link.

### DEC-043 — Atomic cancellation
**Rules:** §6.2. **Status:** NOT RUN.

**Setup:** Regroup target chosen; Keep/Swap dialog open.
**Do:** Cancel or attempt unrelated movement before commitment.
**Actual result:** Cancel has no effects; unrelated command cannot interleave.
**Player-visible result:** No partial transform visible.
**Must not happen:** Appearance applied before cancellation or swap range extended by another move.

### DEC-044 — Copy-only physical legality
**Rules:** §6.4. **Status:** NOT RUN.

**Setup:** Mimic stands on terrain incompatible with proposed resulting profile; swap endpoint may be legal.
**Do:** Inspect Keep and Swap outcomes.
**Actual result:** Each outcome independently validated; legal outcome available, illegal outcome disabled.
**Player-visible result:** Controller sees clear placement restriction without hidden true enemy disclosure.
**Must not happen:** Creating a land-only actual Mimic on illegal water.

### DEC-045 — Exchange sight without movement passives
**Rules:** §6.2, §15. **Status:** NOT RUN.

**Setup:** True Scout-capable unit and terrain/splash forms in reset fixtures.
**Do:** Exchange into a new position.
**Actual result:** Actual sight refreshes; no movement ice/algae/flood/splash; topology/occupation updated.
**Player-visible result:** Newly entitled terrain visible; no fake arrival attack damage.
**Must not happen:** Calling full native Move then restoring flags afterward.

### DEC-046 — Untouched recruit stays unready
**Rules:** §1, §6. **Status:** NOT RUN.

**Setup:** Newly recruited unready real unit beside ready Mimic.
**Do:** Exchange recruit to front.
**Actual result:** Original recruitment restrictions remain; no fresh attack.
**Player-visible result:** Controller accurate; opponent neutral shading.
**Must not happen:** Transport or disguise turning a fresh recruit into an active veteran.

### DEC-047 — Exchange-delivered neutral capture
**Rules:** §6.3, §8. **Status:** NOT RUN.

**Setup:** Fully unspent non-Rogue genuine unit on normal tile, Mimic adjacent to neutral village.
**Do:** Exchange unit onto village; choose capture.
**Actual result:** Capture allowed immediately; native home assignment/cost/flags apply once.
**Player-visible result:** Normal capture animation, no automatic execution during shuffle.
**Must not happen:** Old start-turn tile check blocking explicit outcome.

### DEC-048 — Exchange-delivered ruin
**Rules:** §6.3, §8. **Status:** NOT RUN.

**Setup:** Fully unspent non-Rogue Mimic or genuine unit; adjacent eligible ruin.
**Do:** Exchange onto ruin then Explore.
**Actual result:** Native reward once; collector identity preserved; action spent.
**Player-visible result:** Native reward presentation.
**Must not happen:** Rogue privilege, duplicated reward, or collector transformed into reward unit.

### DEC-049 — Actual Rogue partner
**Rules:** §6, §10. **Status:** NOT RUN.

**Setup:** Commanded Rogue next to owned ready Mimic.
**Do:** Disguise/Swap with Rogue.
**Actual result:** Partner allowed and preserves its budget; Rogue ownership/status persists.
**Player-visible result:** Visible Rogue tint remains; no promise that shuffle conceals its ownership.
**Must not happen:** Copying Rogue status onto Mimic or silently treating Rogue as permanent ally.

### DEC-050 — Two-state observer equivalence
**Rules:** §2, §6.5, §14. **Status:** NOT RUN.

**Setup:** Two fixtures identical in apparent snapshots, visibility and public Rogue/status state; true IDs/readiness swapped.
**Do:** Perform corresponding visually equivalent Keep/Swap operations.
**Actual result:** Actual outcomes differ as chosen.
**Player-visible result:** Unauthorized pixels/audio/tooltips/selections match apart from legitimate specified consequences.
**Must not happen:** Unit-ID tracking, grayed icon, true-name event, or endpoint sound leak.

## Level 4 — Naval forms and independent upgrade paths

### DEC-051 — Normal boarding versus exchanged boarding
**Rules:** §7.2. **Status:** NOT RUN.

**Setup:** Identical ready Warriors near a port/coast in reset fixtures.
**Do:** A: walk through port into water. → B: be the ready partner in a coastal exchange.
**Actual result:** A receives native boarding exhaustion; B becomes an immediately usable actual Raft with preserved flags.
**Player-visible result:** Both are plausible naval forms; private readiness differs truthfully.
**Must not happen:** Using a single constructor path that exhausts both or refreshes both.

### DEC-052 — Giant coastal placement has no splash
**Rules:** §7.2. **Status:** NOT RUN.

**Setup:** Ready actual Giant; adjacent Mimic Boat; enemy in potential splash area.
**Do:** Exchange Giant into water without subsequently moving.
**Actual result:** Actual Juggernaut created ready; zero arrival splash; enemy HP unchanged.
**Player-visible result:** Only shared exchange/transport presentation.
**Must not happen:** Calling Juggernaut on-move splash during teleport.

### DEC-053 — Giant moves after coastal placement
**Rules:** §3, §7.2. **Status:** NOT RUN.

**Setup:** Previous fixture; Giant retained full movement/action.
**Do:** Move new Juggernaut genuinely through its legal range.
**Actual result:** Actual movement and corresponding true splash now occur; no extra allowance after use.
**Player-visible result:** Source animation uses apparent vessel; actual victims show real loss.
**Must not happen:** Suppressing every later passive because initial placement was a teleport.

### DEC-054 — Spent partner remains spent at sea
**Rules:** §7.2. **Status:** NOT RUN.

**Setup:** Already-acted Warrior/Giant beside Mimic Boat.
**Do:** Exchange into water, then attempt move/attack.
**Actual result:** Unavailable allowance stays unavailable in resulting vessel.
**Player-visible result:** Controller menu reflects spent status, enemy sees neutral readiness.
**Must not happen:** New vessel object treated as fresh recruit with fresh turn.

### DEC-055 — Initiating naval Mimic remains spent on land
**Rules:** §6, §7. **Status:** NOT RUN.

**Setup:** Mimic Boat initiates swap with ready land Warrior.
**Do:** Commit coastal swap.
**Actual result:** Land Mimic is spent; water Warrior Raft retains previous readiness.
**Player-visible result:** Apparent map forms; private states correctly differ.
**Must not happen:** Only partner preservation logic applied to both actors, refreshing initiator.

### DEC-056 — Case A Regroup Keep
**Rules:** §7.4. **Status:** NOT RUN.

**Setup:** Land Mimic-as-Giant; water true normal Warrior Raft; both accessible.
**Do:** Land Mimic Regroups, Keep.
**Actual result:** Land actual Mimic now Warrior appearance; water remains true Warrior Raft.
**Player-visible result:** Corresponding Warrior land/raft water forms.
**Must not happen:** Copying actual Raft artwork literally onto a dry-land unit.

### DEC-057 — Case A Regroup Swap
**Rules:** §7.4. **Status:** NOT RUN.

**Setup:** Same case A fixture.
**Do:** Land Mimic Regroups, Swap.
**Actual result:** Land true Warrior normal; water Mimic Boat apparent Warrior Raft; partner readiness preserved.
**Player-visible result:** Shared exchange with correct apparent passenger mapping.
**Must not happen:** Mimic becomes true Warrior or original Warrior remains phantom cargo.

### DEC-058 — Case A Disguise Keep
**Rules:** §7.4. **Status:** NOT RUN.

**Setup:** Same case A fixture.
**Do:** Land Mimic Disguises, Keep.
**Actual result:** Land Mimic-as-Giant; true Warrior Raft now apparent Juggernaut.
**Player-visible result:** Apparent Giant on land, Juggernaut in water.
**Must not happen:** Actual Giant passenger created in real Raft.

### DEC-059 — Case A Disguise Swap
**Rules:** §7.4. **Status:** NOT RUN.

**Setup:** Same case A fixture.
**Do:** Land Mimic Disguises, Swap.
**Actual result:** Land true Warrior-as-Giant; water Mimic Boat-as-Juggernaut.
**Player-visible result:** Two environment-appropriate Giant appearances.
**Must not happen:** Actual passenger/type/HP swapped with public snapshot.

### DEC-060 — Case B Regroup Keep
**Rules:** §7.5. **Status:** NOT RUN.

**Setup:** Land true normal Warrior; water initiating Mimic Boat-as-Juggernaut.
**Do:** Boat Regroups, Keep.
**Actual result:** Warrior stays land; Mimic Boat becomes apparent Warrior Raft.
**Player-visible result:** Normal-looking environmental appearance transfer.
**Must not happen:** Unmasking the water Mimic.

### DEC-061 — Case B Regroup Swap
**Rules:** §7.5. **Status:** NOT RUN.

**Setup:** Same case B fixture.
**Do:** Boat Regroups, Swap.
**Actual result:** Land Mimic-as-Warrior; water genuine Warrior Raft; genuine readiness preserved.
**Player-visible result:** No true-identity path through maybe-shuffle.
**Must not happen:** Blocking immediate real Raft movement solely because a wrapper was created.

### DEC-062 — Case B Disguise Keep
**Rules:** §7.5. **Status:** NOT RUN.

**Setup:** Same case B fixture.
**Do:** Boat Disguises, Keep.
**Actual result:** Land Warrior-as-Giant; water Mimic Boat-as-Juggernaut.
**Player-visible result:** Apparent Giant and Juggernaut.
**Must not happen:** Granting Giant movement/health to genuine Warrior.

### DEC-063 — Case B Disguise Swap
**Rules:** §7.5. **Status:** NOT RUN.

**Setup:** Same case B fixture.
**Do:** Boat Disguises, Swap.
**Actual result:** Land Mimic-as-Giant; water real Warrior Raft-as-Juggernaut.
**Player-visible result:** Normal-looking matching land/sea appearance.
**Must not happen:** Water unit actually acquires Juggernaut splash.

### DEC-064 — Actual Rammer disembarks
**Rules:** §7.1, §7.2. **Status:** NOT RUN.

**Setup:** Rammer carries an actual Warrior and has a different apparent passenger.
**Do:** Legal coastal exchange ashore.
**Actual result:** Actual Warrior restored with prior flags; apparent land form uses apparent passenger; native real wrapper lost as appropriate.
**Player-visible result:** Native-looking disembarkation, not a walking Rammer sprite.
**Must not happen:** Rejecting transports as aquatic-only or inventing Giant payload.

### DEC-065 — Aquatic-only dry-land swap rejected
**Rules:** §7.2. **Status:** NOT RUN.

**Setup:** Genuine Living Island/Jellyfish/Shark without cargo; dry shore partner.
**Do:** Inspect coastal outcomes.
**Actual result:** Illegal dry-land Swap rejected before cost; any independently legal Keep remains.
**Player-visible result:** Controller sees ordinary placement restriction.
**Must not happen:** Fabricating passenger or beaching an invalid body.

### DEC-066 — Aquatic legal flooded endpoint
**Rules:** §7.2. **Status:** NOT RUN.

**Setup:** Same genuine aquatic fixture with endpoint that native traversal permits because flooded.
**Do:** Perform otherwise legal exchange.
**Actual result:** Swap allowed; true body remains aquatic; no arrival movement passives.
**Player-visible result:** Legal apparent form at flooded endpoint.
**Must not happen:** Blanket ban despite native legal flooded terrain.

### DEC-067 — Free upgrade research gate
**Rules:** §7.3. **Status:** NOT RUN.

**Setup:** Mimic Boat-as-Raft in own territory; lacks target tech and Propaganda.
**Do:** Attempt feigned Bomber. → Research Propaganda, retry in fresh eligible state.
**Actual result:** First unavailable; second costs zero and grants apparent Bomber only.
**Player-visible result:** Enemy sees ordinary apparent upgrade; controller sees Feign/0 cost.
**Must not happen:** Free genuine technology or attack.

### DEC-068 — Two Raft upgrade menus
**Rules:** §2.4, §7.3. **Status:** NOT RUN.

**Setup:** Actual Raft receives independent Raft disguise; real tech and funds available.
**Do:** Compare Actual and Appearance upgrade menus.
**Actual result:** Actual path uses native price and real eligibility; fake path 0 stars with permitted research.
**Player-visible result:** Both private modes identify their outputs.
**Must not happen:** Only one ambiguous Upgrade command.

### DEC-069 — Fake first then real upgrade preserves mask
**Rules:** §7.3. **Status:** NOT RUN.

**Setup:** Actual Raft in own territory, real upgrade tech, enough stars; timing fixture permits chosen sequence.
**Do:** Feign Bomber. → Pay for genuine Rammer when native upgrade timing allows.
**Actual result:** Final real Rammer/apparent Bomber; payment only real price; correct native budget transition.
**Player-visible result:** Opponent remains seeing Bomber, no Rammer flash; controller sees both forms.
**Must not happen:** Paid upgrade stripping approved independent mask.

### DEC-070 — Real first with same-type independence
**Rules:** §7.3. **Status:** NOT RUN.

**Setup:** Actual Raft/apparent Raft.
**Do:** Buy genuine Scout, then inspect independent Appearance upgrade options when action-eligible.
**Actual result:** Actual Scout/apparent Raft remains independent; fake upgrade may still be offered under rules.
**Player-visible result:** No private coalescing because actual and apparent matched before upgrade.
**Must not happen:** Native constructor overwrites the Raft mask automatically.

### DEC-071 — Juggernaut feigns Bomber
**Rules:** §3, §7.3. **Status:** NOT RUN.

**Setup:** Actual Juggernaut/apparent Raft; eligible territory/research.
**Do:** Feign Bomber. → Inspect movement and actual weapon.
**Actual result:** Still true Juggernaut movement/HP/passive/weapon; no Bomber weapon.
**Player-visible result:** Apparent Bomber publicly; actual armament private.
**Must not happen:** Disarming genuine unit or granting ranged Bomber attack.

### DEC-072 — Genuine weapon after feigned upgrade
**Rules:** §4, §7.3. **Status:** NOT RUN.

**Setup:** True Juggernaut/apparent Bomber with real attack eligible.
**Do:** Use Actual attack.
**Actual result:** Mask removed; genuine Juggernaut attack resolves.
**Player-visible result:** Poof and true attack; not Bomber projectile.
**Must not happen:** Appearance-mode nonexistent weapon used for genuine damage.

### DEC-073 — Mimic free-upgrade cannot buy a real weapon
**Rules:** §2.4, §7.3. **Status:** NOT RUN.

**Setup:** Actual Mimic Boat/apparent Raft, sufficient stars/real tech.
**Do:** Inspect Actual and Appearance menus, fake-upgrade.
**Actual result:** Only allowed imitation path; always one HP/no real offense.
**Player-visible result:** Truthful private Mimic Boat info; convincing apparent ship.
**Must not happen:** Actual-mode native Raft upgrade creating genuine warship from Mimic.

### DEC-074 — Raft reappearance renews eligibility, not action
**Rules:** §7.3. **Status:** NOT RUN.

**Setup:** Fake-upgraded boat; another Mimic can give Raft appearance.
**Do:** Receive Raft mask and inspect free upgrade, first with ready and then spent flags.
**Actual result:** Eligibility renews in ready native state; spent state is not reset and native timing still applies.
**Player-visible result:** State-based option, no lifetime/voyage quota.
**Must not happen:** Resetting actions with appearance or enforcing once-ever limit.

### DEC-075 — Two paths yield same genuine armament
**Rules:** §7.3. **Status:** NOT RUN.

**Setup:** Same actual Juggernaut in two reset fixtures.
**Do:** A: directly receive Bomber disguise. → B: receive Raft then feign Bomber.
**Actual result:** Actual abilities/HP/movement match in both; only legitimately spent allowances/investment differ.
**Player-visible result:** Same final apparent form.
**Must not happen:** Path-dependent disarm flag or borrowed weapon.

## Level 5 — Terrain passives, concealment and growth

### DEC-076 — Mimic Mooni splits tile and unit freeze
**Rules:** §3.2. **Status:** NOT RUN.

**Setup:** Mimic-as-Mooni beside eligible terrain and an enemy unit.
**Do:** Make true movement that normally triggers the represented passive.
**Actual result:** Real permitted tile ice created; no copied unit freeze or damage.
**Player-visible result:** Normal apparent movement/passive cue; unchanged enemy status is valid evidence.
**Must not happen:** Executing native full freeze then undoing unit status.

### DEC-077 — Genuine Warrior dressed as Mooni
**Rules:** §3. **Status:** NOT RUN.

**Setup:** True Warrior-as-Mooni on eligible terrain.
**Do:** Move normally.
**Actual result:** Appearance ice real; no borrowed unit freeze; actual Warrior movement/weapon remains.
**Player-visible result:** Convincing terrain effect and Mooni art.
**Must not happen:** No ice because only Mimics were patched.

### DEC-078 — Terrain source replacement
**Rules:** §3.2. **Status:** NOT RUN.

**Setup:** True Crab-as-Warrior in one fixture; Warrior-as-Crab in another.
**Do:** Move each.
**Actual result:** Former creates no Crab flood; latter creates approved real flood; do not stack underlying profile.
**Player-visible result:** Terrain matches appearance.
**Must not happen:** Calling both true and apparent terrain routines.

### DEC-079 — Fake Juggernaut movement splash
**Rules:** §3, §4.4. **Status:** NOT RUN.

**Setup:** Mimic Boat-as-Juggernaut or real Raft-as-Juggernaut beside enemy.
**Do:** Move genuinely.
**Actual result:** No acquired splash damage; actual HP of nearby units unchanged.
**Player-visible result:** Juggernaut-style source splash can play.
**Must not happen:** Animation callback causing damage.

### DEC-080 — Real Juggernaut in ordinary appearance
**Rules:** §4.4. **Status:** NOT RUN.

**Setup:** True Juggernaut-as-Raft beside vulnerable enemy.
**Do:** Move genuinely.
**Actual result:** True native splash damages victims; source keeps mask.
**Player-visible result:** Raft source animation; true victim hit/HP feedback; no Juggernaut type cue.
**Must not happen:** Suppressing genuine damage or exposing source through splash artwork.

### DEC-081 — Source passive keeps mask, victim loses mask
**Rules:** §4.3. **Status:** NOT RUN.

**Setup:** Previous fixture; victim is disguised genuine unit or Mimic in reset tests.
**Do:** Apply true movement splash.
**Actual result:** Source remains disguised; genuine positive-damage victim unmasks; Mimic victim dies.
**Player-visible result:** Observer-filtered victim poof/damage; source still Raft.
**Must not happen:** Applying the source-preservation exception to the damaged victim.

### DEC-082 — Living Island terrain exactness
**Rules:** §3.2, V11. **Status:** NOT RUN.

**Setup:** Mimic or genuine unit with Living Island appearance.
**Do:** Move on eligible sea.
**Actual result:** Approved native fruitless algae created; borrowed unit damage suppressed; no free fruit population.
**Player-visible result:** Appearance matches native intended terrain/VFX.
**Must not happen:** Using a generic fruit-generating algae routine.

### DEC-083 — No movement passive on swap
**Rules:** §6.2. **Status:** NOT RUN.

**Setup:** Any terrain-maker/splash form; multiple exchange chain links.
**Do:** Exchange several times without walking.
**Actual result:** No ice/flood/algae/splash per exchange; genuine positional sight still updates.
**Player-visible result:** Only exchange effects plus discovery where entitled.
**Must not happen:** Repeated free damage or terrain per teleport.

### DEC-084 — Mimic Cloak profile
**Rules:** §9. **Status:** NOT RUN.

**Setup:** Ready Mimic-as-Cloak.
**Do:** Move/hide under native conditions, approach an enemy detection footprint.
**Actual result:** Copied movement/Sneak/Scout/concealment; no Infiltrate; appropriate detection applies.
**Player-visible result:** Enemy gets Cloak eye cue and visible apparent Cloak if detected.
**Must not happen:** Generic Scout triggers mistaken eye or Mimic receives real Infiltrate.

### DEC-085 — Warrior cloak disguise
**Rules:** §9. **Status:** NOT RUN.

**Setup:** Actual Warrior receives invisible Cloak appearance.
**Do:** Inspect/move within Warrior allowance, then have opponent detect it.
**Actual result:** True Warrior movement/sight, functional concealment; detection alone retains Cloak appearance.
**Player-visible result:** Enemy sees Cloak/eye, not true Warrior until genuine reveal.
**Must not happen:** Granting Scout/Sneak or stripping costume upon mere detection.

### DEC-086 — Cloak warrior disguise
**Rules:** §9. **Status:** NOT RUN.

**Setup:** Actual Cloak receives Warrior appearance.
**Do:** Inspect and move.
**Actual result:** Actual Cloak mobility/Sneak/Scout; publicly visible Warrior; real Cloak warning still applies.
**Player-visible result:** Enemy info says Warrior; eye warning can be present nearby.
**Must not happen:** Underlying stealth hiding the Warrior appearance.

### DEC-087 — Invisible-state transfer and local detection
**Rules:** §9, §6.5. **Status:** NOT RUN.

**Setup:** Invisible Cloak and adjacent ready Mimic; destination detection conditions controlled.
**Do:** Regroup then Disguise another unit with that snapshot.
**Actual result:** Transferred concealment is functional; native detection at final positions still applies.
**Player-visible result:** Visible participant transformation cue only; no hidden endpoint disclosure.
**Must not happen:** Visibility state always reset to visible or forced hidden despite detector.

### DEC-088 — Hidden shuffle visibility clipping
**Rules:** §6.5, §14. **Status:** NOT RUN.

**Setup:** One visible participant and one invisible participant; third observer lacks both tiles.
**Do:** Regroup/Disguise with Keep and Swap in equivalent reset runs.
**Actual result:** Correct states and actions.
**Player-visible result:** First observer sees only authorized local change; third gets no positional sound/camera trail.
**Must not happen:** A flash on the invisible tile.

### DEC-089 — Copied egg phase
**Rules:** §3.3. **Status:** NOT RUN.

**Setup:** Mimic copies a growing body with one scheduled owner tick remaining.
**Do:** Advance to that scheduled tick.
**Actual result:** Apparent next form/genuine Mimic profile updates; HP remains 1; no true offspring/weapon.
**Player-visible result:** Native apparent growth animation and wounded-state carryover.
**Must not happen:** Timer reset to newborn or actual dragon generated.

### DEC-090 — Growth cannot be accelerated by control
**Rules:** §3.3, C02. **Status:** NOT RUN.

**Setup:** Growing Mimic is Subverted, transformed, saved/reloaded and converted within one scheduling interval.
**Do:** Trigger all those transitions then the scheduled owner tick.
**Actual result:** No duplicate growth tick; stable identity/history persists.
**Player-visible result:** One appropriate apparent transition.
**Must not happen:** Every new commander/constructor hatching it again.

### DEC-091 — Copy one compound body
**Rules:** §3.3, C03, V11. **Status:** NOT RUN.

**Setup:** Owned native linked body/segment fixture with adapter.
**Do:** Regroup with one target body.
**Actual result:** One Mimic remains one actual body; no real copied neighboring army/offspring; genuine original structure unchanged.
**Player-visible result:** Only supported approved appearance; no hidden extra targetable tiles.
**Must not happen:** Cloning real tails/passengers or silently claiming an unimplemented adapter passed.

### DEC-092 — Borrowed terrain effect on occupied tiles
**Rules:** §3.2, C01. **Status:** NOT RUN.

**Setup:** Appearance terrain helper natively bundles a terrain change and an occupant effect.
**Do:** Run copied passive near an occupied tile.
**Actual result:** Eligible terrain output may occur; separate copied unit damage/freeze/ejection is suppressed; genuine underlying effects separate.
**Player-visible result:** Real terrain and truthful victim state.
**Must not happen:** Executing destructive compound routine before suppression.

## Level 6 — Capture, harvesting and economic recipients

### DEC-093 — Smuggle standard
**Rules:** §8. **Status:** NOT RUN.

**Setup:** Ready Mimic stands on fruit; commander owns harvesting tech and 2 stars.
**Do:** Use unit-menu harvest.
**Actual result:** 2 stars paid once; fruit removed once; population to native home city; both move/action spent.
**Player-visible result:** Normal resource/pop flight within view, no revealed-Mimic tag.
**Must not happen:** Local tile city substituted for home city.

### DEC-094 — Immediate Smuggle after delivery
**Rules:** §6, §8. **Status:** NOT RUN.

**Setup:** Fully ready Mimic A; ready Mimic B can exchange A onto fish.
**Do:** B exchanges A; A Smuggles.
**Actual result:** B spends exchange; A pays 2 and both allowances; no start-turn-position requirement.
**Player-visible result:** Normal shared shuffle then harvesting; appearances maintained.
**Must not happen:** Blocking harvest because A arrived this turn.

### DEC-095 — No Dash harvesting
**Rules:** §8. **Status:** NOT RUN.

**Setup:** Mimic with a Dash-capable appearance can reach an animal.
**Do:** Walk onto animal then attempt Smuggle.
**Actual result:** Movement spent; Smuggle unavailable even if a generic action remains.
**Player-visible result:** Private reason truthful.
**Must not happen:** Dash or copied appearance permitting harvest after ordinary move.

### DEC-096 — Domestic transfer versus native tile harvest
**Rules:** §8.1. **Status:** NOT RUN.

**Setup:** Mimic home city A stands on resource inside own city B.
**Do:** In separate reset runs choose Mimic Smuggle or native tile/city harvest.
**Actual result:** Smuggle feeds A; native harvest feeds B; native real costs/actions each applied once.
**Player-visible result:** Appropriate destination animation and private home info.
**Must not happen:** Globally redirecting every harvest to selected unit home.

### DEC-097 — Hostile and unclaimed resources
**Rules:** §8. **Status:** NOT RUN.

**Setup:** Ready Mimic on resource in enemy territory, then unclaimed territory in reset test.
**Do:** Smuggle with actual technology/funds.
**Actual result:** Both permitted; ordinary yield to home city; no subtraction of already-earned city population.
**Player-visible result:** Resource visibly disappears where observed; no naked model.
**Must not happen:** Reintroducing outside-borders-only or hostile-territory ban.

### DEC-098 — Home reassignment after village
**Rules:** §8. **Status:** NOT RUN.

**Setup:** Non-Rogue Mimic captures neutral village and native assignment changes.
**Do:** On a later eligible action harvest a resource.
**Actual result:** Uses currently assigned new home city, not original recruitment city.
**Player-visible result:** Unit info and population path agree.
**Must not happen:** Independent immutable origin pointer.

### DEC-099 — Lost home remains destination
**Rules:** §8.2. **Status:** NOT RUN.

**Setup:** Native home city still exists but is now enemy-owned.
**Do:** Eligible Mimic Smuggles.
**Actual result:** Commander pays; current native home receives population even enemy-owned.
**Player-visible result:** Truthful private destination; opponent owner gets native benefit.
**Must not happen:** Silently choosing friendly fallback or barring every enemy home.

### DEC-100 — Other owner level-up is deferred
**Rules:** §8.2, C09. **Status:** NOT RUN.

**Setup:** Smuggle would level an enemy home city.
**Do:** Harvest then end turn until recipient owner begins.
**Actual result:** Population credited once; native queued choice for city owner on their next turn after required start ordering.
**Player-visible result:** Harvester never receives the enemy upgrade choice.
**Must not happen:** Duplicate reward or forcing opponent to act during harvester turn.

### DEC-101 — Treaty resource distinction
**Rules:** §8, §10. **Status:** NOT RUN.

**Setup:** P has treaty with Q; resource belongs Q.
**Do:** Try owned Mimic harvest; then acquire third-party Rogue Mimic and try harvest in reset eligible state.
**Actual result:** Ordinary Mimic blocked; Rogue permitted; P treaty intact; Rogue home receives population.
**Player-visible result:** No invented war declaration.
**Must not happen:** Rogue counted ordinary ally or ordinary Mimic allowed treaty theft.

### DEC-102 — Rogue Smuggle despite enemy home
**Rules:** §8, §10. **Status:** NOT RUN.

**Setup:** P commands Q Rogue Mimic with home in Q city; resource eligible.
**Do:** Smuggle.
**Actual result:** P pays, Q home grows; Rogue remains ownerless and spent.
**Player-visible result:** Map stays apparent with Rogue cue.
**Must not happen:** Population automatically awarded to P because P commanded.

### DEC-103 — Missing home fails atomically
**Rules:** §8.2, V06. **Status:** NOT RUN.

**Setup:** Instrument malformed Mimic with null home pointer and eligible resource.
**Do:** Attempt harvest.
**Actual result:** No charge/removal/reward; diagnostic and native repair path invoked.
**Player-visible result:** No false successful population animation.
**Must not happen:** Inventing independent home or crashing after consuming resource.

### DEC-104 — Starfish separate from Smuggle
**Rules:** §8.2, C10. **Status:** NOT RUN.

**Setup:** Mimic Boat or Rogue on Starfish; commander lacks Navigation then gains it.
**Do:** Attempt ordinary Starfish collection in each state.
**Actual result:** No Smuggle option; needs actual Navigation; allowed reward stars go commander, not home city.
**Player-visible result:** Native collection icon/reward, not population flight.
**Must not happen:** Propaganda substitutes Navigation for actual star reward.

### DEC-105 — Cloak-form capture restriction
**Rules:** §8, §9. **Status:** NOT RUN.

**Setup:** Cloak-shaped Mimic near an owned enemy city; Warrior-as-Cloak in separate native-valid fixture.
**Do:** Try ordinary entry/capture.
**Actual result:** Mimic respects Cloak operative entry restrictions and lacks Infiltrate; genuine Warrior keeps actual Warrior rules.
**Player-visible result:** Apparent Cloak in both, actual private command differences.
**Must not happen:** Appearance granting genuine Infiltrate or banning Warrior actual legal entry.

## Level 7 — Rogue ownership, actions and restoration

### DEC-106 — Single Subvert turn
**Rules:** §10.1, §10.2. **Status:** NOT RUN.

**Setup:** P Spy detects Q ordinary unit; Q unit had spent actions on Q previous turn.
**Do:** P Subverts, moves and uses allowed genuine action.
**Actual result:** P receives one ordinary available allowance for P turn; original Q turn flags do not make it permanently exhausted.
**Player-visible result:** P sees actual/apparent info only after commit; Q sees Rogue cue.
**Must not happen:** No usable allowance or repeated fresh allowance on every lookup.

### DEC-107 — One acquisition despite reconstruction
**Rules:** §10.2. **Status:** NOT RUN.

**Setup:** P acquired Q Raft and spent action; upgrades/transport can replace object.
**Do:** Reconstruct through permitted upgrade/save and attempt a second Subvert this P turn.
**Actual result:** Stable logical ID remains marked acquired; no new allowance.
**Player-visible result:** No second eligible target cue created by new object ID.
**Must not happen:** Keying acquisition only to transient native object.

### DEC-108 — Rogue can attack any side
**Rules:** §10.3. **Status:** NOT RUN.

**Setup:** P commands Q Rogue armed unit with an eligible target of P, Q, R or another Rogue in reset fixtures.
**Do:** Attack each permitted target.
**Actual result:** All combat hostilities allowed; native real weapons/retaliation/range/status apply.
**Player-visible result:** Truthful Rogue visuals and actual damage.
**Must not happen:** Temporary command makes P units immune or grants Mimic a weapon.

### DEC-109 — Treaty deniability
**Rules:** §10.3. **Status:** NOT RUN.

**Setup:** P allied Q; R hostile.
**Do:** P subverts R armed unit and attacks Q unit.
**Actual result:** Attack allowed; P–Q treaty persists; casualties real.
**Player-visible result:** Rogue indicator explains factionless action; no automatic treaty break.
**Must not happen:** P directly subverting ordinary Q unit or standard attack treaty hook firing.

### DEC-110 — Rogue cannot capture or explore
**Rules:** §10.3. **Status:** NOT RUN.

**Setup:** P commands Q Rogue already on village/ruin/owned city in reset fixtures.
**Do:** Open actions and try alternate capture/explore commands.
**Actual result:** Village/city capture and ruins all denied; siege on city permitted.
**Player-visible result:** No active capture button or fake reward.
**Must not happen:** Using ordinary commander ownership to authorize capture.

### DEC-111 — Rogue cannot disband
**Rules:** §10.3. **Status:** NOT RUN.

**Setup:** P commands expensive Q Rogue.
**Do:** Attempt disband via menu and direct command.
**Actual result:** Denied; no refund/removal.
**Player-visible result:** Private menu excludes command.
**Must not happen:** Selling borrowed unit through underlying-form mode.

### DEC-112 — Convert borrowed target permanently
**Rules:** §10.4. **Status:** NOT RUN.

**Setup:** P genuine Mind Bender adjacent P-commanded Q Rogue.
**Do:** Convert Rogue.
**Actual result:** Permanent owner P; pending Q return removed; flags/history preserved; native support reassigned once.
**Player-visible result:** P full info; Q ordinary apparent enemy info after conversion.
**Must not happen:** Restoring Q ownership at turn boundary despite conversion.

### DEC-113 — Rogue Mind Bender converts another Rogue
**Rules:** §10.4. **Status:** NOT RUN.

**Setup:** P commands two Q Rogues, including Mind Bender; target moved beside it.
**Do:** Use Rogue Mind Bender Convert.
**Actual result:** Target becomes permanently P, retaining spent movement; Mind Bender remains due Q return and spends its action.
**Player-visible result:** Source specialist reveal; target actual/app info for P.
**Must not happen:** Rejecting target as friendly or converting it to Q.

### DEC-114 — Support excludes all Rogue recipients
**Rules:** §4.2, §10. **Status:** NOT RUN.

**Setup:** P genuine and Rogue support casters; owned P unit, ordinary Q unit and P-commanded Rogue nearby.
**Do:** Real/feigned Heal or Swarm in separate fixtures.
**Actual result:** Only eligible permanently P-owned recipient benefits; no Rogue or ordinary enemy, regardless of caster.
**Player-visible result:** Public support otherwise convincing; private actual outcomes precise.
**Must not happen:** Old Rogue-healer affects-everyone rule.

### DEC-115 — Conversion unlocks support not actions
**Rules:** §10.4. **Status:** NOT RUN.

**Setup:** P-commanded Rogue is exhausted, then converted permanently to P.
**Do:** Heal it with another P Mind Bender and attempt its attack.
**Actual result:** Support now allowed; own exhausted attack remains unavailable.
**Player-visible result:** Private readiness exact; no misleading action refresh.
**Must not happen:** Conflating support friendship with a new unit turn.

### DEC-116 — Paid upgrade on borrowed ship
**Rules:** §7, §10. **Status:** NOT RUN.

**Setup:** P commands Q actual Raft in P territory, P has upgrade tech/stars.
**Do:** Pay for genuine upgrade.
**Actual result:** P pays; actual upgrade persists; current mask preserved if independent; Q gets improved ship on return.
**Player-visible result:** P actual/private output truthful; public independent form stays as specified.
**Must not happen:** Using Q stars/tech or refunding upgrade automatically at return.

### DEC-117 — Income before return
**Rules:** §10.5. **Status:** NOT RUN.

**Setup:** Q city occupied by its own former unit, now P-commanded Rogue.
**Do:** End P turn; begin Q turn.
**Actual result:** City income computed under Rogue siege; then surviving unit returns Q; normal new action allowance.
**Player-visible result:** No separate Disrupted tag; Q can use returned accessible unit.
**Must not happen:** Return before income or borrowed turn acting as stun.

### DEC-118 — Return beneath fog
**Rules:** §10.5, §12. **Status:** NOT RUN.

**Setup:** Same due-return Rogue, tile remains Blackout-covered for Q.
**Do:** Begin Q turn and restore it.
**Actual result:** Unit becomes Q and fresh by native turn rules, but cannot be selected/commanded until discovery.
**Player-visible result:** Q still sees fog, no private badge/control bypass.
**Must not happen:** Ownership restoration self-clears the tile.

### DEC-119 — Multiplayer sequential acquisition
**Rules:** §10.5, C11. **Status:** NOT RUN.

**Setup:** Q unit was Subverted by P; R has intervening turn before Q.
**Do:** R legitimately Subverts it and uses it, then Q begins turn.
**Actual result:** One acquisition for each P and R turn; original return owner remains Q; no nested stack; Q gets normal return.
**Player-visible result:** Public Rogue status and entitled info per commander.
**Must not happen:** Treating P as new return owner or denying R solely because P used it.

### DEC-120 — No surviving return after death
**Rules:** §10.5. **Status:** NOT RUN.

**Setup:** Q Rogue is killed while commanded before Q turn.
**Do:** Begin Q turn.
**Actual result:** No unit recreated; support reservation removed according to death once.
**Player-visible result:** No phantom return animation.
**Must not happen:** Restoration queue resurrecting dead object.

### DEC-121 — No passive support-slot exploit
**Rules:** §10.5, C11. **Status:** NOT RUN.

**Setup:** Full-support Q city has one unit Subverted.
**Do:** Inspect capacity and try recruit/defeat calculations before return.
**Actual result:** Native support reservation remains; temporary neutrality alone causes no free slot or elimination.
**Player-visible result:** Public/private support consistent with authorized data.
**Must not happen:** owner=null interpreted as permanent removal.

### DEC-122 — Owner eliminated while Rogue survives elsewhere
**Rules:** C11, V08. **Status:** NOT RUN.

**Setup:** Q is actually eliminated/resigns for unrelated valid reason before restoration.
**Do:** Run native elimination cleanup.
**Actual result:** Reserved Rogue follows specified native permanent-removal cleanup, no owner resurrection/orphan turn.
**Player-visible result:** No later ghost return to defeated player.
**Must not happen:** Treating missing return owner as a new neutral player.

### DEC-123 — Rogue ZOC and road context
**Rules:** C12, §10.6. **Status:** NOT RUN.

**Setup:** P-commanded Rogue on road network and near P/Q units; native distance fixture.
**Do:** Request movement.
**Actual result:** Command-context tech/road privileges apply; Rogue is nonfriendly for ZOC; actual Sneak still bypasses normally.
**Player-visible result:** Controller movement highlight matches authoritative validator.
**Must not happen:** Shared command automatically disabling ZOC or borrowing original owner tech.

## Level 8 — Spy networks and recursive acquisition

### DEC-124 — Two-node extended reach
**Rules:** §11. **Status:** NOT RUN.

**Setup:** P Spies at (0,0) and (2,0); detected target at (4,0), native straight-line range fixture.
**Do:** Select rear Spy and Subvert target.
**Actual result:** Connected reach valid; rear spends action; endpoint remains unchanged.
**Player-visible result:** One target selection, no relay menu.
**Must not happen:** Only selected Spy radius highlighted.

### DEC-125 — Movement immediately changes network
**Rules:** §11. **Status:** NOT RUN.

**Setup:** Spy moves into/out of link range while retaining Dash action.
**Do:** Reopen target highlights and cast.
**Actual result:** Topology matches current positions, moved nodes valid.
**Player-visible result:** Targets update; no stale reachable cells.
**Must not happen:** Stationary-only node rule.

### DEC-126 — Exhausted node relays
**Rules:** §11. **Status:** NOT RUN.

**Setup:** Middle Spy spent its own action, connects rear to front.
**Do:** Ready rear Spy uses network.
**Actual result:** Spent middle remains passive node; no second action consumed.
**Player-visible result:** No public emission at middle.
**Must not happen:** Deleting every exhausted node from graph.

### DEC-127 — Frozen accessible node
**Rules:** §11, C15. **Status:** NOT RUN.

**Setup:** Middle Spy frozen but tile accessible; rear ready.
**Do:** Attempt own action on middle and network action from rear.
**Actual result:** Middle cannot violate its freeze; passive relay remains valid under C15.
**Player-visible result:** Private ready/frozen indicators accurate; no enemy readiness leak.
**Must not happen:** Treating passive link as movement.

### DEC-128 — Real Spy wearing another appearance
**Rules:** §11. **Status:** NOT RUN.

**Setup:** Actual forward Spy disguised as Warrior; apparent Spy-looking Mimic elsewhere.
**Do:** Compute network.
**Actual result:** Real disguised Spy participates; Mimic never does.
**Player-visible result:** Enemy sees forms, not actual network proof.
**Must not happen:** Graph based on apparent type.

### DEC-129 — Subvert real Spy adds local Scout
**Rules:** §11. **Status:** NOT RUN.

**Setup:** Enemy Spy acquisition reveals further terrain containing another Spy.
**Do:** Subvert first, inspect newly discovered target and network.
**Actual result:** New actual Scout source reveals its proper local footprint; acquired Spy joins network; no global map theft.
**Player-visible result:** P sees only legitimately discovered terrain.
**Must not happen:** Original enemy entire explored mask copied.

### DEC-130 — Fake Spy interrupts expected chain
**Rules:** §10, §11. **Status:** NOT RUN.

**Setup:** Enemy apparent Spy is actually Mimic.
**Do:** Commit Subvert and open its menu.
**Actual result:** Source spends action; target actual Mimic; may give copied Scout but no Subvert/Relay.
**Player-visible result:** P learns private truth after commitment, others keep appearance/Rogue cue.
**Must not happen:** Target highlight or pre-commit menu reveals fake.

### DEC-131 — Recursive Spy chain
**Rules:** §10.2, §11. **Status:** NOT RUN.

**Setup:** P Spy reaches enemy Spy B; B reaches enemy Spy C; C reaches Knight.
**Do:** Subvert B, then C via B, then Knight via C.
**Actual result:** Each initiator spends its action; all acquired once; Knight uses one native allowance.
**Player-visible result:** Public Rogue changes as detected; no chain cap.
**Must not happen:** Refreshed B or C on later selection.

### DEC-132 — Fresh rear action through spent forward Rogue
**Rules:** §11. **Status:** NOT RUN.

**Setup:** Rogue Spy B already spent Subverting C; another owned ready Spy connects to B.
**Do:** Owned Spy uses its own action through network at forward target.
**Actual result:** Allowed if graph/range current; does not refresh B.
**Player-visible result:** Initiator reveal/emission only.
**Must not happen:** Conflating exhausted relay with unavailable route.

### DEC-133 — Network cycle
**Rules:** §11. **Status:** NOT RUN.

**Setup:** Three or more real nodes form cycle, one target in overlapping ranges.
**Do:** Select action and commit once.
**Actual result:** Target highlighted once, action/effect charged once, finite traversal.
**Player-visible result:** No duplicated bullseyes/effects.
**Must not happen:** Recursive graph loop or multiple charges.

### DEC-134 — Hidden node breaks network
**Rules:** §11, §12. **Status:** NOT RUN.

**Setup:** Opponent covers bridge-node tile for P with Blackout.
**Do:** P selects rear Spy.
**Actual result:** Hidden node excluded; disconnected reach disappears; remembered positions cannot authorize.
**Player-visible result:** No control via old selection.
**Must not happen:** Allowing fogged node to relay.

### DEC-135 — Rediscovered node reconnects without own action
**Rules:** §11, §12. **Status:** NOT RUN.

**Setup:** Previous fixture; P moves another source to reveal bridge tile.
**Do:** Recompute network then use ready rear Spy.
**Actual result:** Bridge returns as passive relay, no bridge action spent by recovery.
**Player-visible result:** Highlights reconnect promptly.
**Must not happen:** Permanent disconnection or rescued node charged discovery action.

### DEC-136 — Initiator emits at long range
**Rules:** §11, §14. **Status:** NOT RUN.

**Setup:** Disguised rear Spy uses connected remote node for Blackout beyond rear native radius.
**Do:** Commit Blackout.
**Actual result:** Rear reveals and spends action; passive nodes keep masks/flags.
**Player-visible result:** Smoke bomb originates rear, clipped per observer; no intermediate route traced.
**Must not happen:** Final relay throws/reveals or extra endpoint chooser.

### DEC-137 — Post-selection topology change revalidates
**Rules:** §11, §15. **Status:** NOT RUN.

**Setup:** Network target selected but a state update removes relay before commitment in instrumented fixture.
**Do:** Commit stale command.
**Actual result:** Reject atomically or refresh selection before any charge/effect.
**Player-visible result:** No false hit animation or private state leak.
**Must not happen:** Using cached reach despite dead/covered node.

### DEC-138 — No continuing tether
**Rules:** §10.1. **Status:** NOT RUN.

**Setup:** P acquired unit, then loses network path/source and moves target out of all Spy ranges.
**Do:** Use remaining legal target actions during P turn.
**Actual result:** Control persists to turn end; no range-based release.
**Player-visible result:** Rogue status remains.
**Must not happen:** Destroying a relay returning every acquired unit instantly.

## Level 9 — Blackout, Propaganda and fog combat

### DEC-139 — Blackout one tile for every other player
**Rules:** §12.1, C16. **Status:** NOT RUN.

**Setup:** P Spy can reach a tile; Q/R have different prior visibility; Q may have treaty with P.
**Do:** Cast Blackout.
**Actual result:** One tile becomes fog for Q/R/all other players; P mask unchanged; no treaty break under C16.
**Player-visible result:** P sees cast but no residual mark; others see ordinary fog.
**Must not happen:** Opponent chooser, area cloud, or timer status.

### DEC-140 — Already fogged target gives no feedback
**Rules:** §12.1. **Status:** NOT RUN.

**Setup:** Two identical fixtures except Q can see target in one and not other.
**Do:** P without Propaganda opens targeter and casts.
**Actual result:** Both valid and cost same one action; no additional effect needed in already-fogged mask.
**Player-visible result:** P-visible target highlights/cast/result identical.
**Must not happen:** Success tooltip or disabled tile reveals Q prior mask.

### DEC-141 — Caster-unexplored target
**Rules:** §12.1. **Status:** NOT RUN.

**Setup:** Tile lies in network range but P own fog.
**Do:** Blackout target tile.
**Actual result:** Allowed in bounds; other masks changed; P own tile remains fogged.
**Player-visible result:** No discovery through targeting or projectile camera.
**Must not happen:** Caster sight prerequisite or free exploration.

### DEC-142 — Covered unit/city interfaces all blocked
**Rules:** §12.2. **Status:** NOT RUN.

**Setup:** P unit/city tile Blackout-covered for P; previously selected unit.
**Do:** Try map, hotkey, city list, unit cycle, stored command, roads/buildings and relay.
**Actual result:** All tile manipulation denied; unit/city still physically present; income native absent siege.
**Player-visible result:** Ordinary fog, no private badge/control bypass.
**Must not happen:** Only mouse clicking disabled.

### DEC-143 — Trapped Scout cannot self-reveal
**Rules:** §12.3. **Status:** NOT RUN.

**Setup:** Covered P Scout/Spy, adjacent static P unit at turn start.
**Do:** Begin P turn and refresh UI.
**Actual result:** Fog persists; neither self Scout nor static placement discovers.
**Player-visible result:** No automatic cloud opening.
**Must not happen:** Global owned-unit reveal on turn start.

### DEC-144 — Nearby movement clears covered tile
**Rules:** §12.3. **Status:** NOT RUN.

**Setup:** Previous fixture with movable adjacent accessible unit.
**Do:** Move that other unit within a footprint covering trapped tile.
**Actual result:** P discovers tile; trapped unit regains existing allowance; no automatic spent action on rescue.
**Player-visible result:** Ordinary reveal animation.
**Must not happen:** Requiring rescuer initially be outside range or charging rescued unit.

### DEC-145 — Recruitment/borders/Explorer discovery
**Rules:** §12.3. **Status:** NOT RUN.

**Setup:** Covered tile within legal native footprint of a nearby new recruit, border growth or Explorer move in reset fixtures.
**Do:** Trigger external discovery source.
**Actual result:** That source reveals for its entitled player; no other player automatic clearance.
**Player-visible result:** Ordinary exploration cues.
**Must not happen:** Only walking implemented despite approved alternative sources.

### DEC-146 — Conversion/Subvert discovery
**Rules:** §12.3. **Status:** NOT RUN.

**Setup:** P acquires accessible source with real Scout near a P-fogged tile.
**Do:** Commit acquisition.
**Actual result:** New actual local source can reveal surrounding tile; no global map transfer.
**Player-visible result:** New visible region is native local footprint.
**Must not happen:** Acquisition rejected as mere UI refresh or giving original owner new sight.

### DEC-147 — Exchange discovery
**Rules:** §6, §12.3. **Status:** NOT RUN.

**Setup:** P has accessible ready Mimic and actual sight-capable partner near covered region.
**Do:** Exchange partner to legal visible endpoint whose true sight reaches covered tile.
**Actual result:** New actual sight reveals; no movement passives/actions charged to partner.
**Player-visible result:** Normal explored area, no apparent Scout bonus for genuine non-Scout disguise.
**Must not happen:** Using apparent genuine-unit vision or full movement callback.

### DEC-148 — Hidden attacker no retaliation
**Rules:** §12.4. **Status:** NOT RUN.

**Setup:** P Warrior visible to P, its tile fogged for Q; adjacent Q Warrior visible to P, Q lacks attacker detection.
**Do:** P attacks Q Warrior.
**Actual result:** Actual attack damage; no Q retaliation; Q fog remains.
**Player-visible result:** P source may reveal disguise but Q tile access unchanged.
**Must not happen:** Combat-only discovery granting immediate retaliation.

### DEC-149 — Covered defender can retaliate
**Rules:** §12.4. **Status:** NOT RUN.

**Setup:** Q defender tile fogged for Q but visible to P; Q sees P attacker tile.
**Do:** P attacks defender.
**Actual result:** Normal true retaliation allowed if range/status/survival permit; Q defender tile stays fogged.
**Player-visible result:** True combat damage; no new manual Q control.
**Must not happen:** Treating uncommandable defender as automatically unable to retaliate.

### DEC-150 — Both inaccessible positions
**Rules:** §12.4. **Status:** NOT RUN.

**Setup:** Q cannot access its defender tile or see P attacker; P can see both.
**Do:** P attacks.
**Actual result:** No retaliation because attacker unseen; both Q fog states persist absent independent discovery.
**Player-visible result:** No fog popping from hit effects.
**Must not happen:** Assuming automatic defender vision repairs owner fog.

### DEC-151 — Several attacks through persistent fog
**Rules:** §12.4. **Status:** NOT RUN.

**Setup:** Genuine P attacker has permitted native follow-up sequence; attacker positions remain unseen to Q.
**Do:** Perform sequence, checking actual sight each strike.
**Actual result:** No automatic one-strike expiration; no retaliation where Q still cannot see attacker.
**Player-visible result:** True damages, no fake damage; any independent real discovery changes later checks.
**Must not happen:** Hidden immunity persists after discovery or automatically ends without discovery.

### DEC-152 — Splash through fog
**Rules:** §12.4, §4. **Status:** NOT RUN.

**Setup:** Actual splash source can legally act; secondary target tile hidden from attacker or target owner.
**Do:** Resolve true splash footprint.
**Actual result:** Native actual splash hits valid occupants; positive damage reveals genuine victim internally/kills Mimic.
**Player-visible result:** Only authorized victim effects; fog not removed to display poof.
**Must not happen:** Fog grants damage immunity or splash reveals all hidden tiles.

### DEC-153 — Independent players uncover independently
**Rules:** §12.3, §13. **Status:** NOT RUN.

**Setup:** P Blackout target applied to Q and R.
**Do:** Q reveals it through movement; R takes no reveal action.
**Actual result:** Q mask clears; R remains fogged.
**Player-visible result:** Propaganda shows different selected masks.
**Must not happen:** One shared fog flag for all observers.

### DEC-154 — Propaganda selection UI
**Rules:** §13. **Status:** NOT RUN.

**Setup:** P owns Propaganda; Q unit and R city visible; own unexplored area present.
**Do:** Select Q unit then R city then deselect.
**Actual result:** Overlay follows selected faction and only P-visible tiles; no cost/Spy needed.
**Player-visible result:** Translucent fog over selected opponent unseen tiles; no information inside P own fog.
**Must not happen:** Map button/opponent picker or global opponent exploration leak.

### DEC-155 — Propaganda on Rogue and no authenticity
**Rules:** §13. **Status:** NOT RUN.

**Setup:** P selects Q-origin Rogue now commanded by another side; unit disguised.
**Do:** Inspect overlay.
**Actual result:** Uses remembered Q mask; does not report true type or Cloak detection.
**Player-visible result:** Apparent unit info plus appropriate Rogue cue.
**Must not happen:** Using temporary commander or neutral empty mask.

### DEC-156 — Isolated fog lockout remains until discovery
**Rules:** §12.2. **Status:** NOT RUN.

**Setup:** Only isolated city and its garrison are covered for owner; no immediate rescue source.
**Do:** Advance owner turn without a legitimate reveal event.
**Actual result:** City income still normal absent siege; no production/unit orders; fog can persist indefinitely.
**Player-visible result:** Ordinary fog, no timer or auto-rescue.
**Must not happen:** Adding unapproved immunity, expiry or commandable hidden city.

## Level 10 — Combined tactical sequences

### DEC-157 — Attack, extract and permanently convert
**Rules:** §6, §10. **Status:** NOT RUN.

**Setup:** P controls Q Rogue Giant; P ready Mind Bender and ready Mimic line can place Giant adjacent to it.
**Do:** Attack Q unit with borrowed Giant. → Use Mimics to move spent Giant to P Mind Bender. → Convert Giant.
**Actual result:** Real damage once; Mimic initiators spend actions; Giant permanently P, still spent; no Q return.
**Player-visible result:** Attacking Giant reveals itself; later masks may change normally; P actual info correct.
**Must not happen:** Extraction refreshes Giant or conversion blocked by temporary command friendship.

### DEC-158 — Same chain but fake continuation
**Rules:** §6, §10. **Status:** NOT RUN.

**Setup:** P borrowed Giant cannot be converted; multiple apparent Giant Mimics.
**Do:** Attack, then exchange borrowed Giant halfway down line. → Continue visible operations using only Mimics. → Begin Q turn.
**Actual result:** Giant returns to Q after income wherever actual chain stopped; Mimics remain P.
**Player-visible result:** Q command restoration distinguishes its unit; no persistent promise of post-return bluff.
**Must not happen:** Returning wrong apparent Giant or propagating owner through presentation chain.

### DEC-159 — Coastal delivery, fake upgrade, real weapon
**Rules:** §6, §7, §4. **Status:** NOT RUN.

**Setup:** P ready true Giant and naval Mimics; real research/Propaganda eligible.
**Do:** Exchange Giant into water. → Give actual Juggernaut Raft appearance. → Feign Bomber when native timing allows. → Use remaining genuine movement/attack as allowed.
**Actual result:** No exchange splash; true movement uses Juggernaut speed/splash; genuine attack strips mask; no Bomber weapon.
**Player-visible result:** Raft/Bomber source animation until actual active reveal; victim damage true.
**Must not happen:** Embark exhaustion on delivery, borrowed weapons or extra action from fake upgrade.

### DEC-160 — Real and fake upgrade paths survive same-type collision
**Rules:** §2, §7. **Status:** NOT RUN.

**Setup:** P actual Raft/apparent Raft; native timing fixture and paid tech.
**Do:** Feign Scout. → Buy genuine Scout when eligible, making actual/apparent names equal. → Later receive Raft appearance and feign Bomber.
**Actual result:** Independence persists through equal-type stage; final actual Scout/apparent Bomber; actual payment/flags correct.
**Player-visible result:** No map truth leak when fields happen to match.
**Must not happen:** Independent flag discarded and later Appearance commands lost.

### DEC-161 — Enemy network infiltration with own backline action
**Rules:** §10, §11. **Status:** NOT RUN.

**Setup:** P rear Spy can reach Q Spy B; B Scout discovers C; another P ready Spy connects after acquisition.
**Do:** Acquire B, then C. → Use remaining P Spy action through newly acquired network on a valuable Q unit.
**Actual result:** Each acquisition/action once; spent B/C may relay; reach follows actual current graph.
**Player-visible result:** Only legitimately gained local sight and public Rogue events; no enemy network auto-access.
**Must not happen:** Taking one Spy automatically gives every enemy relay or generating unlimited commands.

### DEC-162 — Invisible Rogue Cloak infiltrates commander city
**Rules:** §9, §15. **Status:** NOT RUN.

**Setup:** P Subverts visible genuine Q Cloak; P has invisible Cloak-shaped Mimic and an eligible P city.
**Do:** Disguise Rogue Cloak with invisible snapshot. → Use genuine Infiltrate on P own city.
**Actual result:** Action available before commit; true hidden eligibility satisfied; source reveals then committed action executes; P receives Daggers/stolen income; native city consequences.
**Player-visible result:** No hidden endpoint disclosure from shuffle; genuine ability visible where entitled.
**Must not happen:** Reveal invalidates already-committed Infiltrate or reward goes Q.

### DEC-163 — Rogue support after permanent conversion
**Rules:** §4, §10. **Status:** NOT RUN.

**Setup:** P commands Rogue healer H and Rogue wounded target T; own eligible unit U nearby.
**Do:** Attempt Heal/feint on T and U in reset branches. → Convert T permanently using another Mind Bender. → Heal T with ready H.
**Actual result:** Before conversion only U eligible; after conversion T eligible; no target action refresh; no Rogue-heal-all exception.
**Player-visible result:** Private true/apparent changes and source reveal versus feint correctly separated.
**Must not happen:** Caster neutrality changes support recipient ownership test.

### DEC-164 — Rogue Knight sacrifices owned Mimics
**Rules:** §10, C14. **Status:** NOT RUN.

**Setup:** P borrowed Q Knight and P Mimics form a legal kill path toward Q target.
**Do:** Attack P Mimics then continue native Persist sequence onto Q target.
**Actual result:** Real one-HP deaths and genuine follow-ups; no newly invented score bonus for own sacrifices; action ledger not refreshed.
**Player-visible result:** Truthful damage/deaths; enemies never see naked Mimic survivors.
**Must not happen:** Blocking own-side targets or giving fake kills without casualties.

### DEC-165 — Rogue siege plus Blackout at return
**Rules:** §10.5, §12. **Status:** NOT RUN.

**Setup:** P borrowed Q city garrison remains in Q city; P Blackouts that tile for Q.
**Do:** End P turn; start Q.
**Actual result:** Income denied while Rogue siege; then return/fresh allowance; Q still cannot command under fog; no extra city status.
**Player-visible result:** Q sees ordinary fog, not return clearing tile.
**Must not happen:** Return-before-income or new Blackout Disrupted income penalty on later clear turns.

### DEC-166 — Fogged relay rescue and counter-Blackout
**Rules:** §11, §12. **Status:** NOT RUN.

**Setup:** P network cut by Q Blackout; another P source can move to reveal its middle Spy.
**Do:** Move rescuer. → Use a ready Spy through restored network to Blackout Q forward Spy.
**Actual result:** Middle no action cost for rescue; P initiator spends; Q node disabled until Q discovery.
**Player-visible result:** Each player sees own ordinary masks; no global network trace.
**Must not happen:** Automatically restoring every affected faction or exhausting recovered node.

### DEC-167 — Paid hidden upgrade under fog
**Rules:** §7.3, §12, §14. **Status:** NOT RUN.

**Setup:** P genuine Raft/apparent Bomber in own territory; Q cannot see tile.
**Do:** Pay genuine Rammer upgrade then later move into Q-visible area.
**Actual result:** Actual Rammer/app Bomber persists; correct native expenditure/sight for actual source; no free Q discovery.
**Player-visible result:** Q first sees Bomber; no cached Rammer upgrade animation revealed later.
**Must not happen:** Replay/UI constructor leaking actual form through fog.

### DEC-168 — Fake heal followed by real splash and attack
**Rules:** §4. **Status:** NOT RUN.

**Setup:** P target actual 4/10, apparent 8/10 receives fake healing; actual enemy hidden splash later hits; source mask different.
**Do:** Feign Heal. → Apply positive true passive splash. → Use any surviving target genuine attack when eligible.
**Actual result:** No real heal; positive damage applies true HP and removes target false state; passive source stays disguised; later attack real.
**Player-visible result:** Real loss/current actual HP on reveal; no fake damage to reconcile displayed old total.
**Must not happen:** Subtracting from apparent HP or exposing passive source automatically.

### DEC-169 — Disguised terrain-maker sea exchange
**Rules:** §3, §6, §7. **Status:** NOT RUN.

**Setup:** Actual Crab-as-Warrior and Warrior-as-Crab in legal land/sea reset fixtures.
**Do:** Exchange each without walking. → Then make genuine movement when allowed.
**Actual result:** No arrival terrain; subsequent movement follows apparent terrain profile only and actual traversal/weapon.
**Player-visible result:** No flood for Crab-as-Warrior; approved flood for Warrior-as-Crab after true move.
**Must not happen:** Actual terrain routine triggered by wrapper constructor or both profiles stacked.

### DEC-170 — Three-player re-subversion and permanent conversion
**Rules:** §10.4, §10.5. **Status:** NOT RUN.

**Setup:** Order P→R→Q. Q Rogue acquired P, then R can acquire and convert it.
**Do:** P ends commands; R Subverts same physical Rogue once. → R permanently converts it. → Begin Q turn.
**Actual result:** Return owner stays Q until conversion; then permanent R, no Q return; R turn actions not refreshed by conversion.
**Player-visible result:** Truth access changes appropriately; Propaganda before conversion used Q mask.
**Must not happen:** Nested owner stack, double action or Q restoration overriding permanent R.

## Level 11 — Persistence, observation security and release gates

### DEC-171 — Save after spend and before native follow-up
**Rules:** §15, V13. **Status:** NOT RUN.

**Setup:** Acquired Rogue Knight has used an attack but retains a native follow-up; stable ID known.
**Do:** Save/reload, continue sequence and attempt reacquisition.
**Actual result:** Exactly remaining native follow-up retained; no full refresh/acquisition; true owner/home/appearance preserved.
**Player-visible result:** Same permitted view after reload.
**Must not happen:** Resetting action bits or stripping mask because object rebuilt.

### DEC-172 — Save between payment and animation completion
**Rules:** §15, V13. **Status:** NOT RUN.

**Setup:** A paid genuine upgrade or Smuggle has committed; visual animation pending.
**Do:** Interrupt and reload/replay.
**Actual result:** Cost, resource, population/upgrade each apply once; pending presentation may resume without repeating mechanics.
**Player-visible result:** No duplicate resource or true-type flash.
**Must not happen:** Animation completion owns authoritative effect and reruns it.

### DEC-173 — Atomic shuffle save boundary
**Rules:** §6.2, §15. **Status:** NOT RUN.

**Setup:** Keep/Swap selection pending, then committed in separate fixture.
**Do:** Save/load or replay across each boundary.
**Actual result:** Pending precommit state either canceled/restored safely; committed final pair applies exactly once; no half-swap.
**Player-visible result:** No temporary overlap/naked model or private choice leak.
**Must not happen:** Range mutation between stages or one unit duplicated/lost.

### DEC-174 — Fog survives reconstruction and rendering
**Rules:** §12, V04, V13. **Status:** NOT RUN.

**Setup:** Covered source/Spy/city with apparent overlay and pending Rogue return.
**Do:** Save/load, open UI lists, apply non-revealing status refresh and return.
**Actual result:** Stored masks remain until approved discovery; no source self-clear; network excludes covered node.
**Player-visible result:** All alternate interfaces continue blocked.
**Must not happen:** Generic visibility rebuild grants free rescue.

### DEC-175 — Player-perspective replay secrecy
**Rules:** §14, V12, V15. **Status:** NOT RUN.

**Setup:** Recorded match includes disguised swaps, hidden specialist action, fake upgrades and mixed visibility.
**Do:** Replay from P, Q and authorized debug perspectives.
**Actual result:** One deterministic gameplay sequence; authorized state differs only by viewer.
**Player-visible result:** P/Q each receive their permitted apparent info, no true IDs via scrub; debug explicitly noncompetitive.
**Must not happen:** Ordinary replay path revealing hidden network/disguises.

### DEC-176 — AI must act on observations
**Rules:** V14, §14. **Status:** NOT RUN.

**Setup:** Two world states differ only hidden actual enemy type, same apparent histories.
**Do:** Compare AI evaluation inputs and pre-interaction decisions under same random seed.
**Actual result:** AI receives equivalent permitted observations; real execution still uses actual state.
**Player-visible result:** No privileged authenticity query through pathfinding/score hints.
**Must not happen:** AI targets Mimics perfectly from raw true fields.

### DEC-177 — Alternate inputs cannot bypass modes/access
**Rules:** §2.4, §12, §15. **Status:** NOT RUN.

**Setup:** Appearance mode/Blackout-covered unit and stale queued upgrade fixture.
**Do:** Try hotkeys, controller, city list, direct command injection in test harness and canceled dialogs.
**Actual result:** Revalidate explicit command kind, actual tech/cost/flags/access; no action on covered tile.
**Player-visible result:** Clear lawful private rejection; no secret target-state leak.
**Must not happen:** UI mode treated as security boundary or old selection as authorization.

### DEC-178 — Observer-equivalence regression capture
**Rules:** §14, V12. **Status:** NOT RUN.

**Setup:** Generate two legal states with same observed histories, masks, apparent records and public statuses but swapped hidden identities/readiness.
**Do:** Render map, tooltips, info, action hints, camera/audio events and replay selection for unauthorized observer.
**Actual result:** Authoritative states differ but no public action resolves yet.
**Player-visible result:** Outputs equivalent in all specified identity-sensitive channels.
**Must not happen:** One pixel/tint/name/cost/value/track distinguishes true units before allowed evidence.

### DEC-179 — Native-adapter evidence and advertised coverage
**Rules:** §17. **Status:** NOT RUN.

**Setup:** Implementation claims a pinned build and supported forms/modes.
**Do:** Run V01–V16 probes and all relevant cases; record build/adapter data and failures.
**Actual result:** Native upgrade timing, range metric, support/pop/turn hooks and special forms documented from actual environment.
**Player-visible result:** Unimplemented adapters/modes clearly listed, not marketed as passing.
**Must not happen:** Calling this documentation suite executed merely because it was generated.

### DEC-180 — Full deterministic combined replay
**Rules:** §15, §17. **Status:** NOT RUN.

**Setup:** Construct scenario with coastal exchange, same-type disguise, paid+fake upgrades, Rogue chain/conversion, treaty theft and multi-player Blackout.
**Do:** Run scripted commands uninterrupted, then replay with save/load inserted after every committed command.
**Actual result:** Stable IDs, stars, cities, real/app HP/forms, actions, home/support, Rogue returns and every mask match at all checkpoints.
**Player-visible result:** Each player view matches its uninterrupted counterpart, including private mode reset.
**Must not happen:** Any duplicate reward, hidden-state leak, action refresh or fog difference after reconstruction.
