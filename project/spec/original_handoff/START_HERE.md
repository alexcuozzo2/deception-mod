# Implementation handoff — Deception DF-1

## What to read

**`DECEPTION_V1_FINAL.md` is the complete, authoritative handoff.** It includes every rule, the completion-default register, native integration checks, and 180 graded acceptance cases. You do not need the previous conversation or draft specifications. This freeze supersedes revision 1.4 and every earlier proposal.

`ACCEPTANCE_TESTS.json` contains exactly the same cases in machine-readable form. `TEST_RESULTS_TEMPLATE.json` is an unfilled evidence log, not a passing report. `MANIFEST.json` identifies the artifacts and checksums.

The design is finalized; no implementation or gameplay tests have been performed in preparing this package. Native source/API behavior is not asserted by the abstract event names in the document.

## The assignment

Implement a sixth Polytopia technology branch, initially researched by ordinary tribes:

```text
Deception
├── Misdirection → Propaganda
└── Camouflage → Espionage
```

Mimics cost 3 stars. They always have a convincing apparent form, one actual HP, no actual attack/retaliation, and the approved apparent mobility/sight/concealment/terrain capabilities. They can capture, harvest population to their native home city, copy/give appearances, and optionally exchange positions. Partners lose neither movement nor actions—even across the coast or when frozen. Only initiators spend an action. A genuine unit retains actual movement, weapons and unit-affecting powers; appearance controls approved terrain, concealment and visual presentation.

Spies cost 8 stars; actual movement 1, Dash, Scout, HP 10, defense 1, no ordinary attack. Range-2 Subvert and Blackout work through passive range-2 networks. Moved/swapped/exhausted real Spies remain relays. Only initiating Spies spend actions and reveal when acting. Subverted units are Rogue, not friendly: they can attack both sides, may be permanently converted, and cannot capture, collect ruins or disband. They restore after income at the original owner's turn. No repeated per-player-turn acquisition/action refresh.

Blackout covers one tile with ordinary fog for every other player. Units/cities underneath are inaccessible until an approved independent reveal event. No attack-only reveal, automatic timeout or new fog category. Hidden attackers cannot be retaliated against by a side that cannot see them; a covered defender may still retaliate if it sees the attacker. Propaganda shows opponent tile fog over your own visible map upon selecting that opponent's unit/city, without Spies.

Actual/Appearance selection toggles two private command menus backed by one unit and budget. Real naval upgrades cost real stars/technology and preserve an independent mask. Fake upgrades are free with real tech or Propaganda and grant no weapon; they do not disarm genuine vessels. Apparent and real type equality never merges those records.

## Non-negotiable implementation boundaries

Do not derive ownership, commands, support eligibility, sight, damage, appearance and refund from a single overloaded unit/faction field. Do not turn a copied Mimic into its apparent native unit. Do not use a native movement constructor for an exchange and try to undo effects afterward.

Do not use apparent type for genuine movement/weapon calculations. Do not use actual enemy type for unprivileged combat previews, sweat/lethal hints or AI evaluation. No enemy readiness shading, naked Mimic map state, authenticity marker or true-unit selection tracker through a shuffle. Visible mechanical tells and human memory are intentional.

Every transformation must preserve stable logical identity, real paid investment, action/Subvert history, true/app naval payloads, native home city, and due Rogue return. Save/load and replay must reproduce both actual state and each observer's permitted view.

The **C01–C20** entries are explicitly documented completion defaults for details previously implicit. Treat them as the handoff baseline, not as alleged verbatim user decisions. The **V01–V16** entries are native inspection tasks. Where native behavior conflicts with an explicit outcome, implement the outcome through an adapter rather than silently adopting the conflicting native behavior.

## First implementation pass

Inspect and record the actual game build, loader, architecture and available source/dependencies. The intended host is the user's modded Steam/macOS setup; do not guess exact APIs or overwrite an unrelated working installation. Preserve a rollback path.

Build a deterministic fixture harness with actual-state logging and per-player observation capture. Verify native action/upgrade/turn/visibility hooks before wiring many systems together. Read the implementation sequence in the spec; incremental milestones are acceptable, but mark omitted forms/modes as not implemented rather than claiming the whole design works.

Translate the JSON cases into executable or reproducible manual tests. For each case, record fixture/save/seed, command log, actual-state evidence, viewpoint evidence and PASS/FAIL/NOT IMPLEMENTED. A correct actual state with an information leak is a failure. An excellent-looking animation with the wrong HP/action/owner result is also a failure.

Start with the lowest levels. Cases DEC-157 onward combine the systems; DEC-171 onward stress persistence, authority, AI and information boundaries. No test in this package has passed yet. Do not relabel generated expectations as observed behavior.

Balance concerns should be reported separately from correctness. Action-preserving transport, infinite-length but action-limited Spy networks, Rogue friendly fire, invisible genuine attackers, free appearance upgrades and indefinite fog lockouts are deliberately selected interactions. Do not nerf them in code without a recorded design change.
