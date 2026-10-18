# Implementation status — M0.1

This document describes source present in this package, not observed native success.

| Area | Source present | Current execution / integration boundary |
|---|---|---|
| Native sixth root | `src/Core/TechTree.cs`, `src/PolyMod/Entry.cs` | Candidate adapter; not run in game. Human categories only; per-tribe hidden grouping roots preserve every original technology definition. |
| Native tech-tree rendering | Managed PolyMod helper prefix normalizes Basic to its ordinary-tribe override | Reuses existing N-root layout; no additional native UI detour. Layout, labels, selection and cost need in-game verification. |
| Native gameplay/recruitment | None enabled yet | Mimic is NOT recruitable. Only Deception research is registered. No later purchasable nodes. |
| State schema | Stable ID, actual body/vessel/passenger/HP, independent appearance value snapshot, true investment/home, ownership/command/Rogue return fields, turn allowance, acquisition history | Model only. NOT an engine serializer, complete Rogue timer, authoritative network protocol or native event ledger. |
| Recruitment | Three-star atomic/idempotent Mimic creation, one actual HP, legal reviewed profile predicate, occupancy/support/ordinary-tribe/tech checks | Synthetic native-policy fixture only. Native picker, spawn, appearance renderer, recruitment flags and save integration pending. No placeholder spawn. |
| Capabilities | True Mimic has no damage/attack/defense; genuine movement/sight/weapon stay actual; allowed apparent terrain/concealment policy IDs separated | Scalar model only. No production per-form/passive registry or creature/segment adapter. |
| Damage/status | Positive resolved HP loss, zero-loss preservation, genuine reveal, Mimic death/tombstone | Receipts, not hooked native damage/death paths. Cloak detection semantics and death/elimination events pending. |
| Support | Real/feigned heal receipts, independent clamps, actor reveal, current-commander ownership and Rogue recipient exclusion | Native eligibility/range/amount/post-action receipt required. NOT complete Heal/Recover/Swarm engine integration. |
| Exchange | Atomic copy/Keep/Swap model, range-one policy, initiator-only end action, partner state preservation, illegal-endpoint rejection | Native coast conversions, visibility events, arrival/passive suppression and public shuffle presentation not implemented. |
| Smuggle | Two-star, same-tile fruit/fish/animal, both actions unused, real harvesting tech, live native-home identifier, treaty/Rogue rule, model population notice | Native harvest yield, missing-home recovery, city upgrade choices and queue/event ordering pending. No starfish command adapter. |
| Naval upgrades | Small genuine-upgrade receipt preserves independent mask and monotonically spent allowance | NOT complete HP/cargo/boarding/disembarking/feigned-upgrade implementation. |
| Actual/Appearance selection | Private selection state with repeated-click toggle, target-mode separation and no extra allowance | Model UI only; no game button binding. Never uses timed double-click recognition. |
| Observer projection | Public view excludes true kind/LOCAL_HOME/readiness; controller true view; hidden/inaccessible unit filtering; selected equivalence subchecks | Model serialization only. Native UI, previews, AI, camera/audio, replay and networking are not filtered by this package. No anti-cheat/security claim. |
| Persistence/replay | Model JSON copy/validation, command payload hashes, duplicate command results, deterministic fixture comparisons | Not native save/load or multiplayer. These receipts are not arbitrary untrusted public commands. |
| Higher technologies/Spies | Frozen branch topology and supporting fields only | Misdirection, Camouflage, Propaganda, Espionage, Rogue timer, relay, Subvert and Blackout NOT integrated. |
| Art/audio | Source requirements preserved | No referenced artwork folder supplied. Final assets, poof, shuffle, sound and labels pending. |

## Test accounting

`tests/KernelTests.cs` contains 62 executable **model subchecks**. Their DEC references mean “related requirement,” not “this entire DEC case passes.” The managed suite is NOT RUN in this delivery environment; the local build must run it.

`docs/COVERAGE.json` retains all 180 frozen acceptance IDs, V01–V16 and the 16 supplemental presentation checks. No complete acceptance case is marked PASS. `spec/TEST_RESULTS_TEMPLATE.json` is the original unfilled template.

`tests/test_packaging.py` contains 20 independent Python file-level checks which were actually run successfully on Linux. Some deliberately supply synthetic PASS records to test a validator; that is NOT execution evidence for the C# suite.

## Native safety and authority limits

The game adapter currently patches only two **managed PolyMod** methods. It neither re-patches nor replaces game/loader native code on disk. It calls existing generated native wrappers only for JSON and root metadata. The API contracts are checked before installing those managed prefixes.

The pure model is not yet a drop-in native authority layer. In particular, command authentication for every effect receipt, turn restoration, all transport forms, complete field-specific support legality, full observable-state filtering and native transactional persistence must be wired before releasing those gameplay operations. Omitting an adapter is not permission to silently alter DF-1.
