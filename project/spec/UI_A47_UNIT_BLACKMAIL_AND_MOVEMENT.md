# A47 — Unit Blackmail and genuine movement

Alex’s 2026-09-24 playtest supersedes city-targeted Blackmail and any borrowed traversal for genuine disguised units.

## Movement

For genuine non-Mimic units, the actual unit determines movement distance, terrain access, occupied-tile traversal and zone of control. Apparent Cloak invisibility must never grant a real Dragon Hide/Sneak traversal. Real Cloaks keep their genuine abilities; a Cloak wearing a Rider disguise may use a Cloak-only path but sheds the disguise when necessary. Prefer a disguise-compatible path only when the actual unit can legally use that path. Mimics retain inherited disguise movement under the existing Mimicry rules. Presentation and path queries must not mutate actual effects or action flags.

## Unit Blackmail

- Intelligence offers Blackmail on a visible enemy unit, with no nearby friendly unit required. Native visibility includes hidden-unit checks, not just explored terrain.
- Each player can pay 2 stars once per target unit per turn. Later turns add layers; different blackmailers may independently target the unit.
- Each layer transfers one star of the unit’s supporting city’s income per turn. All layers on all supported units and all blackmailers share the city’s native gross-income cap. Reject another purchase when no income remains.
- Use the native supporting/home city (transport passenger home when embarked). Do not invent an origin for a unit without a valid owned supporting city.
- Persistent logical identity follows native replacement during boarding/landing. Temporary Rogue command retains the original victim and city. Permanent ownership/home changes, death/disband, city capture/destruction, player elimination or peace end the applicable obligations. Existing hostile-action/embassy behavior remains.
- Lost visibility does not end income or expose the unit’s position. Unit tint and income markers are visible only when the unit itself is visible. Existing city visibility rules remain.
- Payout and deduction occur on each participant’s own turn, with persisted idempotence stamps. Setup grants no retroactive income. Income cannot become negative.
- If native city income subsequently falls, retain contractual layers but temporarily cap their active total at current income. Allocate deterministically by creation turn, blackmailer, logical target; excess layers resume if income returns. No arrears accrue.
- Unit presentation shows a dark tint and the summed active star count; native health, actual ownership and action state remain unchanged. Valid paused contracts retain the dark tint, a zero-star marker and city note; only payments pause. City income subtracts active layers; updated supplied note art remains a centered per-blackmailer row below the visible city status area.
- Retain the existing negative Blackmail sound and victim notification/meeting behavior. Notification identifies a compromised unit’s supporting city without leaking its real identity.

## Persistence and compatibility

Ledger schema9 adds unit identity, stack size and last-purchase turn. Preserve historical schema5–8 reads and old city-command receipts. Legacy city-only liens cannot be assigned to arbitrary units: retire them at the next cleanup boundary and exclude them from income/UI immediately. New commands use persistent unit receipts and reject stale unit position, ownership, home, income, currency, turn or snapshot before mutation. Matching mod/schema is required for online peers. No new platform admission or completed multiplayer acceptance claim.

## Validation and delivery

Cover Dragon-as-Cloak and Cloak-as-Rider ZOC paths, actual Cloaks and Mimics, stacked purchases and city-wide caps, native visibility, Rogue ownership, transport before/after reconciliation, death/disband, duplicate/replayed commands, settlement rollback and save/reload. Preserve pooled UI and snapshot-cache cleanup. Run full model/UI/native metadata/compiler gates, exact recognized previous-mod backup, and own-mod-only install while closed. Alex leads native playtesting; do not launch or change saves automatically. Keep both Mac and Windows sections in each setup ZIP, explicitly Windows preparation-only until admitted and tested.
