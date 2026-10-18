# A11 — branch revision and behavior priority

Authority: Alex's explicit instructions and clarification on 16 September 2026. This amendment supersedes conflicting parts of FINAL_v1_5, A01 and historical tests. Keep those original materials as history; do not silently mark their old expectations passed.

## Branch

| Tier | Technology | Parent | Behavior |
|---|---|---|---|
| 1 | Deception | Native Basic | Recruit Mimics; Mimics have Regroup immediately, plus their existing Smuggle ability. |
| 2 | Propaganda | Deception | Recruit Mimics in otherwise recruitable forms from the player's eligible tribe roster without researching those forms. Super units remain excluded. |
| 2 | Camouflage | Deception | Disguise, unchanged. |
| 3 | Intelligence | Propaganda | Inspect the selected opponent's effective tile visibility. |
| 3 | Espionage | Camouflage | Spy and network abilities, unchanged. |

Misdirection no longer gates Regroup. Propaganda moves from tier 3 to tier 2; its former visibility-inspection benefit moves to Intelligence. Native research pricing still applies. Propaganda grants neither genuine unit technology nor a foreign universal roster. Ships, spawn-only forms, bare Mimics and non-recruitable grown forms remain outside initial recruitment. The new exclusion also applies to city-produced super units, including their special-tribe equivalents; copying an eligible owned super unit in the field remains a separate Regroup/Disguise rule.

The rationale is that bluffing a unit should be cheaper than obtaining that genuine unit. Existing feigned-upgrade access under Propaganda remains unless separately changed; actual paid upgrades still require native research and payment.

## Intelligence

Alex confirmed the original selection-based overlay behavior, preserving normal fog. With Intelligence, focus/select a visible opponent unit to inspect that opponent's effective vision on tiles the inspecting player can currently see. Use a mostly transparent treatment tinted with the opponent's tribe color. No information is disclosed in the inspecting player's own fog, and hidden enemy units are not revealed.

Retain FINAL_v1_5 §13 and §21.8's nonconflicting behavior under the new technology name: a visible opponent city can also select that faction; switching factions changes the overlay and deselecting removes it. Peace partners are inspectable. A Rogue uses its remembered original owner. The overlay reflects actual tile access, updates with Blackout/rediscovery, and grants no action or persistent exploration. As in the original spec, tinted fog marks tiles the selected opponent cannot currently access; clear tiles within the inspector's visible area remain accessible to that opponent. No global HUD/player picker is required.

## Implementation priority

Focus on intended mechanics, the remaining technology branch and interactions. Defer visual polish, custom assets and style matching; Alex will generate and integrate assets later. Preserve the already approved inspection behavior and functional input/privacy/lifetime guards. Placeholder visuals may be used to test behavior.

Model, fixture, metadata and actual native-game outcomes must remain separate. A renamed node or passing model does not establish that its gameplay ability is implemented.
