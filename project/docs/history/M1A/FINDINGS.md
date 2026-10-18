# M1A findings: two UX issues and the next native increment

## 1. Localization error found in our source

M0.2 used `technology_names_dcp_deception`. The supplied PolyMod.dll
(SHA256 db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d)
contains `PolyMod.Managers.Loc.ReplaceDashesWithDots`: replace `_` with `.`, then
replace `..` with `_`. Consequently the original key becomes
`technology.names.dcp.deception`, not `technology.names.dcp_deception`.

The corrected archive key is `technology_names_dcp__deception` (same escape in
description and prototype unit terms). The displayed English value is exactly
`Deception`. The internal identifier `dcp_deception` is unchanged. The reported
longer on-screen label was not supplied verbatim, so this is a confirmed code defect,
not a claim to have inspected a screenshot of that label.

## 2. Research audio: guarded native fallback, not a guessed root-cause claim

M0.2 only registered data and had no custom research-audio adapter. The user's
observation establishes silence for the tested purchase, but the exact native
branch responsible has not been disassembled here.

The supplied pinned wrappers expose `ResearchReaction.Execute(Il2CppSystem.Action)`,
`ResearchAction.Type`, `ActionBase.PlayerId`, native `PlayerExtensions.HasTech`,
and static `AudioManager.PlaySFX(SFXTypes, SkinType, float, float, float)`.
`SFXTypes.Tech` is 47. The recorded optional defaults are volume 1, pitchMod 1,
and pan 0. The candidate calls that existing API with the local player's native
visual skin; it supplies no sound file, changes no mixer setting or economic method,
and doesn't play a success cue merely from a button click.

The adapter observes a Tech cue emitted within the native reaction so it does not
add another one in that case. It checks the local player, same game and owned tech
after the original reaction returns; failed original calls propagate unchanged.
A per-game/player/tech ledger deduplicates repeated observed reactions within this
session. Asynchronous cues outside this reaction scope, native action replay,
audibility/mute behavior and exact parity remain in-game tests, not verified claims.

## 3. Recruitment prototype

The existing native Basic-root algorithm is byte-for-byte unchanged. A second pure
JSON stage adds a non-spawnable city-menu anchor and 26 separately indexed custom
Mimic variants, each reusing its source native prefab but not its identity. Original
unit and tribe definitions remain byte-equivalent as canonical JSON values.

The native picker consults GetUnlockedUnits and GetOverride using the current tribe.
A CommandBase passed into the UI hook is explicitly cast to its TrainCommand wrapper
before reading Coordinates; no assumed derived CLR wrapper access is used. Choice
revalidates player/game/native unlocks and ordinary TrainCommand.IsValid. Native
ClickedSpawnUnit then owns the ordinary confirmation and payment/creation pipeline.
No manual currency change or UnitState.Create call occurs in the picker.

Only simple reviewed source forms are exposed. Dangerous native abilities are not
borrowed. Boat/ice-water transitions for land forms are conservatively refused,
not allowed to convert the prototype into a genuine ship. This is explicitly an
incomplete recruitment checkpoint: public labels/stats are not deceptive, and
specialist/stealth/mixed-terrain-passive forms and many required rules remain open.

## 4. Inspection scope

The named API signatures were read from the supplied GameLogicAssembly.dll,
PolytopiaAssembly.dll and Il2Cppmscorlib.dll, not invented from a generic Unity SDK.
51 literal reflection-call sites matched the inspected method signatures at audit
time. This is metadata compatibility evidence, not execution of those functions.
The build checks their pinned checksums before compiling.

52 local Python tests passed for archive creation, prior-build receipt verification,
backup/repeat/conflict/failure handling, localization keys, frozen acceptance records,
explicit report collection and preserving the root algorithm. C# and the actual game
were NOT run here. Build installation is gated on 120 C# model/registry subchecks on
the Mac. Those do not claim native acceptance or completed visual/audio fidelity.
