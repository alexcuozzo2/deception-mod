# Deception v1.6 — Centipedes and all-tribe scope
## Implementation amendment A01 · 14 September 2026

**Apply this amendment to the v1.5 handoff already in implementation.** It changes only release/tribe scope and the Centipede appearance/exchange exception. All other mechanics, numerical values, interfaces and the visual/audio appendix remain as previously specified. This document does not report any implemented or tested gameplay.

**Precedence:** this amendment supersedes v1.5 §1.1's ordinary-tribe-only scope and narrows §3.3/C03's generic compound-body copying rule. Replace DEC-001 and DEC-091 as provided in the updated suite. Existing independent appearance, action-preservation, reveal, visibility and no-offense rules still apply.

**Decision status:** the user selected all-tribe availability and solo-head-only Centipede imitation, with no Regroup/Disguise involving a head with attached segments. The explicit body-segment exclusion, graph checks and no-tail-copy rules below implement that boundary. A01-D1 (newly spawned and severed solo heads are equivalent) and A01-D2 (non-attack topology-growth cleanup) are documented completion defaults, not claims that the user separately dictated those corner cases.

## A. Every tribe receives the branch

All ordinary AND special tribes in the target build receive the same additional sixth branch:

```text
Deception
├── Misdirection → Propaganda
└── Camouflage → Espionage
```

Mimics remain 3 stars; Spies remain 8 stars with movement 1, Dash, Scout, and the existing range/link values. No free starting research or units are added. Preserve each tribe's original five branches, native units, starting advantages, territory rules and research-price modifiers.

The existing roster policy is unchanged: Deception's direct appearance picker uses that tribe's unlocked, eligible city-origin forms; Propaganda adds its otherwise-unavailable eligible forms and super-unit appearance. All-tribe support does NOT mean every tribe can directly recruit every other tribe's imitation. An owned foreign example remains copyable in the field without additional research, subject to the Centipede exception and final-placement rules.

Implementation must register the branch for special-tribe trees as well as ordinary trees, generate the correct per-tribe picker, and use tribe-aware harvesting, unit-skill, terrain, naval and unlock adapters. Do not assume that every tribe has the standard resource actions or the standard fleet. Do not invent missing native actions merely because the branch is shared. All already-approved mod exceptions remain available where their prerequisites are met.

Tests must iterate every tribe present in the pinned build, not just one ordinary tribe and a foreign-unit fixture. A special-tribe implementation gap is a coverage failure, not an approved exclusion from v1.

## B. Define the permitted form precisely

> **An actual Centipede may participate only as a standalone head with zero attached body segments. Any copied Centipede appearance must depict that solo-head form.**

A01-D1: “standalone” describes its current structure, not its history. A newly created zero-segment head and a formerly segmented head that is now alone qualify equally. The native body/segment type must actually have become a head; an isolated unpromoted tail segment does not qualify merely because it currently occupies one tile.

| Actual participant | Regroup | Disguise | Keep or Swap |
|---|---|---|---|
| Genuine head with zero attached segments | Allowed under ordinary targeting/placement rules | Allowed | Either legal outcome |
| Genuine head with one or more attached segments | Not allowed | Not allowed | Neither; no copy-only exception |
| Attached body segment | Not allowed | Not allowed | Neither |
| Detached body segment not yet a genuine standalone head | Not allowed | Not allowed | Neither |
| Actual Mimic wearing solo-head appearance | Normal Mimic rules | Normal Mimic rules | Either legal outcome |
| Ordinary genuine unit wearing solo-head appearance | Normal genuine-partner rules | Normal genuine-partner rules | Either legal outcome |

The restriction is about participation in the whole action, not just movement. It is invalid to copy a multi-segment head while choosing Keep, to disguise it without swapping, or to move a single attached segment independently.

## C. Check actual structure, copy apparent identity

These are separate steps and must not be conflated:

1. Determine whether every participant is structurally eligible using its **actual unit identity and native attachment graph**.
2. Validate the ordinary command, adjacency, action and placement conditions.
3. Transfer the source's **current apparent state**, never its hidden actual identity.
4. Resolve Keep/Swap atomically using the existing action-preservation rules.

A real segmented Centipede wearing Warrior appearance is still ineligible. An actual Warrior wearing solo-Centipede appearance is not segmented and remains eligible. A solo genuine Centipede wearing Warrior appearance supplies Warrior appearance to a regrouping Mimic, not Centipede appearance.

Count attached bodies through native links or the authoritative chain structure. Do not count nearby units or visible tail artwork. Fog, invisibility, disguise, foreign ownership, conversion and Rogue status do not erase attachments. A disconnected nearby segment must not be counted as attached merely because it is adjacent.

Revalidate before committing the transaction. If an initially selected head is no longer eligible, cancel without spending the initiating action/stars, copying a snapshot, moving anything, changing sight or playing public effects. Do not unhook, kill or teleport segments to make an invalid exchange work.

No change to Subvert, Mind Bender conversion, genuine combat or genuine Centipede growth is implied. Their head/body ownership handling still requires the native integration audit. In particular, do not silently make a segmented Centipede immune to Subvert merely because Regroup/Disguise excludes it.

## D. The imitation is always one head, never a chain

A Centipede appearance contains a **zero-segment head only**. Mimics cannot copy, generate, display as separate tiles, attach to or carry a chain of body segments. No cosmetic multi-tile tail is created. The head's ordinary intrinsic art/appendages are fine; independently occupying or suggesting extra body tiles is not.

This restriction applies equally when Disguise puts the solo-head appearance on a genuine unit. It does not acquire actual Grow, segment-creation or segment-control ability through appearance. A Warrior disguised as a Centipede attacks as a Warrior after revealing and never grows a fake or real Centipede tail.

A Mimic wearing the head remains 1 actual HP with no damaging attack/retaliation. Its permitted movement, vision, status presentation and other inherited non-offensive behavior remain governed by the existing rules. No generic apparent-growth or on-kill handler may create tails for this appearance, even when nearby native effects run.

Where the player's native roster and Propaganda permit direct recruitment of a Centipede appearance, spawn only the solo head. Do not add a Segment option or generate an entire chain. Owning a solo foreign head still permits Regroup without Propaganda, while direct recruitment continues to use the existing own-roster gate.

## E. Genuine head lifecycle

- A genuine solo head can be disguised normally.
- A genuine attack strips its disguise at commitment, then performs real native combat and any legitimate resulting growth.
- As soon as it has attached segments, Regroup/Disguise are unavailable, regardless of how it acquired them.
- If it later becomes a genuine head with zero attached segments again, eligibility returns immediately; no action/movement allowance is refreshed.

**A01-D2 — defensive topology-change default:** if a genuine masked solo head acquires a real attached segment by a non-attacking native event, clear its independent disguise as that chain forms. Otherwise the mod would maintain a prohibited multi-body entity inside a single-body costume. Use the established reveal poof only where the observer can see it; do not reveal tiles through fog, create an identity-memory label, damage it or refresh its actions. This structural cleanup is an explicit narrow exception to the ordinary non-attacking-passive reveal rule. It is normally unnecessary when growth follows a direct attack, because that attack has already stripped the disguise.

Save/load or migration of a legacy illegally disguised connected Centipede should report that invalid state and apply the same mask cleanup when migration is supported. Preserve the genuine head and all real segments. Never delete real bodies to restore the one-tile rule. Reject incompatible saves rather than falsely claim migration when no safe migration path exists.

## F. Interface, animation and observer behavior

Keep the native solo-head artwork and all established Mimic/Actual/Appearance UI. No new unit, technology, paid ability, sprite or sound is introduced by this amendment.

In Regroup/Disguise targeting, invalid heads and body segments are not selectable/highlighted. Inspecting an invalid **owned or commanded** participant may show a private explanation: “Only standalone Centipede heads can Regroup or Disguise.” There is no opponent-visible warning, sound, action log or feedback on a rejected attempt.

Valid solo-head interactions use the same shared transformation and maybe-shuffle animations as other forms. No actor-identifying arrow, readiness leak, tail trail or invisible-endpoint cue is added. A fake head never gains a persistent “fake” label. Commanders continue to see true identity privately.

The important evidence remains natural: a player may infer that a visibly multi-segment Centipede is not a legal imitation under this exception. Do not create an automatic authenticity badge or enable enemy targeting queries that reveal hidden actual attachment state.

## G. Exact handoff changes

- §1.1: replace ordinary-tribe-only release scope with all tribes.
- §3.3/C03: insert the solo-head exception; do not apply generic one-body copying to connected Centipede heads or tail segments.
- §6: add actual-topology validation to both actions and both Keep/Swap branches.
- §5's appearance picker: native roster permitting, Centipede entry produces only a solo head.
- V01/V11: audit every tribe's tree/roster and real head/segment links, promotion and growth hooks.
- DEC-001: run branch registration/research as a fixture for every tribe.
- DEC-091: replace generic linked-body copying with solo-head acceptance and linked-head/body rejection.
- Add DEC-181–DEC-200 from the supplemental JSON. Earlier unrelated tests and all presentation requirements remain in force.

All cases are **NOT RUN**. State validation of the generated handoff is not a game test. Only the actual target build can verify native IDs, attachment hooks and exact transformation semantics.

## H. Supplemental acceptance cases

All tests are NOT RUN. See the JSON for full fixtures and prohibited outcomes.

### DEC-181 — Newly spawned solo head is eligible

**Setup/action:** Ready Mimic next to an owned newly spawned genuine zero-segment head. Try Regroup with Keep and Swap in reset fixtures.

**Expected:** Both legal outcomes succeed; only the initiating Mimic spends its action; no segments are created.

### DEC-182 — Formerly segmented solo head is eligible

**Setup/action:** A previously segmented genuine head now has no attached segments; retained action flags are recorded. Use Disguise, testing both legal outcomes.

**Expected:** Current zero-segment structure permits the action; partner flags are unchanged.

### DEC-183 — Linked head blocks Regroup including Keep

**Setup/action:** Genuine Centipede head with at least one linked body and an adjacent ready Mimic. Attempt Regroup without swapping; repeat with a swap attempt.

**Expected:** Both attempts are rejected before costs, copying or relocation.

### DEC-184 — Linked head blocks Disguise including Keep

**Setup/action:** Genuine head with several attached bodies; Mimic holds a Warrior appearance. Attempt to transfer Warrior appearance with Keep and Swap.

**Expected:** No disguise transfer or relocation is committed in either branch.

### DEC-185 — Attached body cannot be targeted

**Setup/action:** Ready Mimic stands beside a Centipede body segment rather than its head. Try both appearance-transfer actions.

**Expected:** Body is excluded; original chain links and positions are unchanged.

### DEC-186 — Unpromoted body is not a solo head

**Setup/action:** Native isolated body exists before any native promotion to head. Try actions before promotion; run a legal native promotion; retry when it is actually a zero-segment head.

**Expected:** Before: excluded. After: eligible without artificially refreshing flags.

### DEC-187 — Disguise cannot hide actual linked structure

**Setup/action:** Fixture contains an actual connected Centipede head with a Warrior mask, e.g. legacy/migration state. Validate Regroup and Disguise; run the recorded topology-sanitization path.

**Expected:** Target is blocked based on actual links; cleanup preserves all genuine bodies and actions.

### DEC-188 — Ordinary unit in solo-head appearance remains usable

**Setup/action:** Actual Warrior has a solo-Centipede appearance; a Mimic is adjacent. Regroup with it; test Disguise in a reset fixture.

**Expected:** Actual single-body partner is allowed; Regroup copies apparent head rather than actual Warrior.

### DEC-189 — Mimic head never generates segments

**Setup/action:** Mimic displays a solo head; include nearby real Centipede and growth-hook fixtures. Move, wait, convert, Subvert, save/load and invoke applicable native turn events.

**Expected:** One real Mimic remains; actual HP 1 and no damage; no segment entities or attachment links are created for it.

### DEC-190 — Genuine disguised head attacks then becomes ineligible

**Setup/action:** Ready actual zero-segment head wearing Warrior appearance; legal kill/growth fixture. Commit its real attack, resolve native growth, then try Regroup.

**Expected:** Attack removes mask; legitimate real growth persists; once linked, target is rejected; no action refresh.

### DEC-191 — Non-attack real growth cleans unsupported mask

**Setup/action:** Actual disguised solo head gains a real segment through a native non-attack event or controlled compatibility fixture. Apply native attachment and then validate/serialize state.

**Expected:** A01-D2 clears the unsupported mask without changing genuine bodies, HP, ownership or actions; head becomes ineligible.

### DEC-192 — Losing last body restores only eligibility

**Setup/action:** Actual linked head loses its last attached body through a legal native event; its action has already been spent. Recompute eligibility; target it as a partner.

**Expected:** Zero-segment genuine head is eligible again; spent actions remain spent and frozen/status conditions persist.

### DEC-193 — Revalidate selected target before commit

**Setup/action:** Selected solo head changes to linked state before authoritative transaction commit in a race/replay fixture. Submit previously prepared Keep/Swap command.

**Expected:** Reject atomically; no action/star/state/vision expenditure, no movement and no duplicated bodies.

### DEC-194 — Rejected targeting creates no identity signal

**Setup/action:** Invalid owned/commanded linked head; observers have different sight. Attempt/cancel invalid operations repeatedly.

**Expected:** No committed action, cost, state or visibility changes.

### DEC-195 — Links not adjacency determine head status

**Setup/action:** Two reset fixtures: solo head beside an unrelated segment; linked head whose bodies are distant/hidden in another viewpoint. Evaluate and attempt normal legal exchange in each.

**Expected:** First uses true links and may qualify; second is excluded despite not showing nearby segments.

### DEC-196 — Every tribe receives the same sixth branch

**Setup/action:** Iterate all ordinary and special tribes in the installed registry with known baseline trees and starting data. Open research; purchase both new branch paths; compare original trees/prices and recruit both custom units when unlocked.

**Expected:** All receive the five nodes/prerequisites and unchanged custom prices; native starting conditions, research modifiers and existing branches remain intact.

### DEC-197 — Each tribe keeps its own direct-imitation roster

**Setup/action:** For each tribe, record its eligible city-origin forms and native prerequisites. Inspect Deception picker before and after Propaganda.

**Expected:** Native eligible roster governs entries; Propaganda expands own-roster appearance access, not genuine tech or every foreign roster.

### DEC-198 — Cymanti Propaganda head is solo

**Setup/action:** Cymanti with Deception and Propaganda, a legal city recruit slot and 3 stars. Recruit a Centipede-form Mimic.

**Expected:** One actual Mimic, one HP, zero offense, no linked segments; genuine super unit/research not generated.

### DEC-199 — Foreign solo sample remains copyable

**Setup/action:** Non-Cymanti owns a genuine foreign zero-segment head; Misdirection researched but no Propaganda. Regroup; compare with a linked-head reset fixture and the direct recruit picker.

**Expected:** Solo source is copyable; linked source is not; direct picker remains the tribe-defined one.

### DEC-200 — Persistence across topology and all-tribe changes

**Setup/action:** Multi-tribe fixture including real solo/linked heads, a fake head, a disguised ordinary body, conversions and Rogue status. Record eligibility and all views; save/reload after legal transitions; compare with uninterrupted play.

**Expected:** Actual links, identities, actions, owners, home cities and eligibility match; every tribe retains the branch; illegal legacy masks handled only by documented migration.
