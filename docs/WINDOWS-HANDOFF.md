# Windows port handoff — 2026-10-17

Base: `8d0cf46` (0.3.100). Integration branch: `codex/windows-port`.
Goal: playable Windows–Mac multiplayer using the supplied PolyMod beta.
Ask the user to playtest; do not automate gameplay.

## Latest failure and next test

Follow-up: End Turn was nonfunctional. The log throws from ValidationReceiver.
A read-only inventory of both game assemblies confirms Disband/Explode/**EndTurn**
share IsValid at RVA 0x4dff20. The new candidate admits the exact EndTurn receiver,
bypasses tile-based blackout checks for it, and preserves native validation.
The Disband postfix still applies only to Disband. Startup checks allocate real
objects for all three classes, verify identity and unchanged EndTurn validation.

The same inventory found 15 reaction types sharing ShouldFocusCamera (0x1a8dd70).
The BuildReaction prefix now checks native class identity before reading action;
startup checks verify all 15 aliases and the non-Build bypass. Direct native hook
sites were compared with the shared-body inventory; known shared groups are the
excluded empty methods, the three validators, two InteractionBar cleanup methods,
and these 15 reaction methods. This is a bounded audit of the two game assemblies,
not a claim that every Unity/runtime method or gameplay path was tested.

Expanded Dobby tests exposed unrelocated near conditional branches. The library
patch now covers both short and near Jcc plus short JMP: 66/66 synthetic cases
pass. New Deception SHA-256 is `7f124a72709f51c0307e56f749a4d0d719dd5b9aa0fd637511ca249c282d7ec6`.
The candidate is restored in Mods, the diagnostic plugin removed, and the game
launched normally. Asked to test End Turn, movement/attack and another turn, the
user replied that Deception loaded and seems to be working. The fresh log has no
shared-validator exceptions or error-level entries. Detailed feature coverage and
Windows–Mac multiplayer remain unverified.

### Previous tribe-selection diagnosis

The subsequent full dump identifies `TileData.HasImprovement` in the tribe
preview. Dobby's relocated short Jcc targets four bytes too far, into INT3
padding (RVA 0x4fe78c instead of 0x4fe788). The pinned upstream source confirms
the displacement calculation omits instruction-length expansion. Short JMP also
truncates its new offset. Source patch and synthetic native regression are in
`distribution/windows-ci`; see `DOBBY-FIX.md` for exact build/install steps.
Stock installed Dobby fails all 34 cases; patched Dobby passes all 34, including
original/trampoline/hooked execution and unhook restoration. The corrected DLL
is installed locally. The user confirmed tribe selection, fresh-game entry and
working Deception content after a normal Steam launch, and is testing features.
Cross-platform multiplayer and detailed functionality remain unverified.
Deception archive remains 74cb3c54…; Mac keeps its working runtime, and the
multiplayer beta is unchanged. Do not claim gameplay acceptance from this test.

Follow-up fix: the repeated missing-environment-variable failure is addressed
by package-bound installation approval. Entry checks the exact installed archive
against `BepInEx/config/deception-windows-candidate.sha256` before any hooks.
Missing, malformed, oversized or mismatching approval fails closed. The checked
launcher writes approval only after verifying the game, beta and archive hashes.
Normal Steam launches can then load that candidate without inherited environment
variables. Changing the archive invalidates approval. Game/beta/native pins stay
in effect. New tests cover approval and invalidation; gameplay still needs testing.

Verified locally: launched through `steam://rungameid/874390` with the process
opt-in removed. The fresh log reports `Installed archive approved`, the live
shared-validator check passed, modded multiplayer initialized, and StartScene
loaded. No assembly loading error appeared in that run. Seven approval tests,
expanded candidate admission tests and the existing model/online suites passed.

User follow-up: the corrected launch crashes at tribe selection before entering
a match. Windows reports GameAssembly.dll breakpoint exception `0x80000003`,
offset `0x4fe78c`. The last Unity stack involves `SetupFakeSession` and
`SpriteCamera.TakeSnapshotOfMapState` for the tribe preview. The default minidump
does not contain enough dynamic managed method data to name the hooks. This is
the active blocker; do not claim the gameplay crash fixed by installation approval.

The user reported “had loading error” and no working mod in-game. The latest
Windows log explicitly says `Windows candidate requires explicit
DECEPTION_WINDOWS_PORT_TEST=1`, followed by `All loaded mods are client only.
Skipping modded multiplayer initialization.` It also shows a hotseat match with
repeated map generation and state `Unknown`. Do not use that run to diagnose
enabled Deception gameplay: its assembly was rejected at startup.

Launch with the checked repository launcher or the README PowerShell command.
Verify the log contains `windows-shared-validator-check`,
`Finished modded multiplayer initialization`, and the Deception registration.
Then ask for a fresh in-game test. If that fails, collect the first error and
bounded relevant stack from that same run. An additional beta warning at
`Dystopia.UpdateStatusHeader` / `UIHorizontalList.get_HeaderKey` was observed;
its cause is not established. No Windows–Mac match has passed yet.

## Implemented and observed

- Authoritative `manage.py` has Windows inventory, compile, probe, candidate and
  test modes; `run.ps1` is its Windows entry point. No csproj conversion.
- All three Windows x64 native helpers built with MSVC; 269 harness checks passed.
- Sixteen native seam pointers/prologues resolved in-game. Separate native
  exception probe passed two real IL2CPP catches and three managed callbacks.
- Candidate embeds separate Windows helpers and original hash-verified Mac
  helpers. Game hashes and the exact beta are pinned; unknown platforms reject.
- Windows identical-code folding caused a reproducible startup stack overflow.
  `NativeWindowsHooks` handles the three observed overlaps and rejects new
  unreviewed shared targets. Its live isolated-object test confirms Explode is
  not subjected to Disband-only rules. No command is executed by that test.
- Final candidate reached StartScene and completed beta initialization. The user
  confirmed lobby access on the preceding build with the same hook fix.
- 339 model tests, six online suites (258/266/88/57/360/99 assertions), candidate
  admission tests and four Windows build-tool tests passed. These are not native
  match acceptance.
- One startup failed in BepInEx console `StreamWriter.Write` with OverflowException;
  a retry worked. Do not confuse it with the separately fixed hook recursion.

## Artifact identity

Current candidate SHA-256:
`7f124a72709f51c0307e56f749a4d0d719dd5b9aa0fd637511ca249c282d7ec6`.

Required beta SHA-256:
`61588813d4b62d133db06fad64a2c440869aaffaf7460ec8bc8e9d82b1be63d7`.

Beta config used `https://dev.polydystopia.xyz`, with auto-update disabled.
Both peers need the same archive and compatible player identities. Preserve the
Mac's working native loader. Windows host hardware is ARM64 running the x64 game;
the emulated runtime's `OSArchitecture=X64` is not physical CPU evidence.

Generated packages and full diagnostics remain local, ignored by Git. Consult
`windows-build.json` and `receipt.json` in the build output for source/input hashes.
Do not replace precise compatibility guards with version-name-only checks.
