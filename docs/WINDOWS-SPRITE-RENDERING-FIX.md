# Windows Spy/Fogger rendering playtest — 2026-10-18

Branch: `codex/windows-sprite-rendering`, based on private dev main `22f2a39`.
This branch is independent of the ordinary-mod admission PR #5.

## Changes

- Fit the selected Spy portrait to its composed visible bounds and center it.
  The previous fixed 1000-unit canvas did not match the larger Spy layers.
  Fogger's intentional artwork size is preserved.
- Keep tracking an owned selected-unit panel when native refresh replaces its
  children. Recompose the custom portrait instead of abandoning tracking and
  leaving the native Warrior copy. Ownership/state/selection guards remain.
- For batched Polytopia renderers, read overlay state from the wrapper cache;
  for ordinary SpriteRenderers, retain their property block. Preserve the
  wrapper-only fallback. Add bounded, non-interfering overlay-source diagnostics.

## Evidence and limits

User reported that the local build resolved the ready Spy/Fogger discoloration,
oversized Spy panel and extra Warrior after being asked to check all these cases:
"wow, yes that seems to have worked".

Playtested mod SHA-256:
`14f72044be9f1d740ae2988183f3eab5ebe37826065661034fa47a5440de1d86`.
Game 2.17.3.16375, existing adapted multiplayer beta and corrected Dobby unchanged.
This was a rendering playtest, not clean-beta admission or multiplayer acceptance.
Native helper sources and binaries were reused; no shared packaging files changed.

No targeted portrait/map/readiness errors were found in the post-test log.
Captured Spy/Fogger overlay diagnostics used the ordinary sprite-block path,
with strengths 0 and 0.4 (plus a tiny negative floating-point value). Therefore
this run does NOT prove stale batched cache was the cause of the original map
blackout. The user-observed recovery is recorded separately from that hypothesis;
compare Mac rendering before attributing the symptom to Windows exclusively.

After playtesting, final review restored the pre-existing wrapper-only fallback
for non-batched native renderers without a plain SpriteRenderer. This additional
case is fixture-tested, not separately game-playtested. The installed tested
archive was left unchanged.

## Validation

168 main UI adapter tests, 25 Fogger tests and 53 Spy-sizing tests passed before
playtesting. Final rerun also covers the wrapper-only fallback. Regression coverage
includes stale underlying property blocks and native panel repopulation after
custom composition. Test wrappers expose isBatched as a property, matching the
installed generated IL2CPP wrapper. These are synthetic native-boundary tests.

Mac should integrate the source changes and produce the final shared artifact.
Keep both peers on identical final mod bytes. Full native logs containing personal
paths remain local; this report contains only relevant sanitized observations.
