# Windows hook avoidance audit

18 October 2026. Source audit and standalone Dobby regression; no installed game,
gameplay code, packaging, or runtime changed.

## Decision

Some hooks are candidates for replacement. Removing the patched Dobby requirement
is not yet justified. Avoiding one defective trampoline is different from proving
every remaining detour safe, and switching to another library may still use Dobby
underneath. Dobby is the hook backend; Deception's native helper DLLs have separate
serialization and exception-boundary responsibilities.

## First candidate: Rogue city defence

`project/src/PolyMod/NativeRogueAccounting.cs` installs `TileData.HasImprovement`
alongside `UnitDataExtensions.GetDefenceBonus`. A thread-local scope makes the
city query return false only for the Rogue's exact tile during defence calculation.
This preserves native terrain/status calculations while excluding city defence.
The installed trampoline nevertheless runs for ordinary callers too. The captured
tribe-selection crash was this method called from `Tile.Render`, before combat.
A callback guard cannot protect a corrupt native trampoline.

Prototype replacing this broad tile-query hook with a narrow defence-result
adapter. First inspect the pinned game's defence calculation: subtracting a
constant blindly can break poison rounding, terrain, walls, and other abilities.
Do not temporarily mutate shared tile/city state to simulate the result. Verify
the replacement method's relocated prologue against stock Dobby before using it.

Acceptance must preserve the existing `project/tests/rogue-0362` cases: city
owners 1/2/3, Rogue defence 10, terrain 15, poisoned terrain 7, ordinary fortified
city defence 40, unrelated queries, nested scopes and exception unwind. Then
compare native combat preview and resolved damage, save/reload and two-client
outcomes. This audit has not implemented or validated that replacement.

## Other candidates and limits

| Area | Direction | Gate before removal |
|---|---|---|
| Custom commands/actions/reactions | Isolated Polibrary CAR prototype; see HELPER-LIBRARY-AUDIT.md | Execute/reject, serialization, replay, save/reload, stable IDs and matching beta clients. Additional library is a dependency, not proof of no native hooks. |
| Presentation/audio | Prefer supported Unity/loader entry points where equivalent | Visibility, lifecycle and rendering checks on both platforms. Keep the working sprite fix independent. |
| Blackout command validation | Consider a common authoritative dispatch point | Must cover AI, queued commands and every execution route; hiding buttons is insufficient. |
| Custom state envelope/native bridge | Retain pending a verified persistence API | Clone, replay, serialization, rollback and exception-boundary equivalence. |
| Windows shared method bodies | Retain receiver checks and canonical hook selection | These prevent cross-type detours; fixing Dobby does not replace them. |

The existing pointer inventory is diagnostic evidence, not an exhaustive stock
Dobby admission proof. Before dropping the runtime requirement, enumerate all
installed detours for the exact game build, validate relocated targets, then run
tribe selection, fresh match, custom actions, End Turn, save/reload, menu/resume,
resign and Mac-Windows multiplayer with identical archives. Keep runtime checks
until this succeeds. A game update can change the prologues.

## Parallel upstream route

An existing [Dobby PR 13](https://github.com/BepInEx/Dobby/pull/13) fixes short Jcc.
Contribute Windows evidence there rather than duplicate it. Our local patch also
fixes short JMP displacement truncation and near Jcc relocation; those need
separate review. The source-only report is in DOBBY-UPSTREAM-NOTE.md. The patch,
standalone harness and reproduction instructions already live under
`distribution/windows-ci/`.

The current full harness was rerun locally: stock DLL **0/66 passed**, patched DLL
**66/66 passed**. This is Windows ARM64 executing x64 code, not native x64 hardware
coverage or multiplayer acceptance. No fresh gameplay test was performed for
this documentation-only work.
