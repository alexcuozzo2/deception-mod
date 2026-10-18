# Upstream report draft

Prepared for Dobby PR 13 and the PolyMod team. Not yet posted publicly. Share the
standalone source/patch, not private repository history, crash dumps or game DLLs.

## Dobby PR 13 comment

We independently reproduced the short-Jcc destination being four bytes too far
on Windows ARM64 running an x64 IL2CPP application. A normal call through an
installed hook reached INT3 padding. Your next-IP correction matches our fix.

Our standalone Windows executable generates synthetic x64 functions and tests
16 short conditional branches, 16 near conditional branches and short JMP in both
directions (66 cases; eight input pairs each). It validates relocated destinations
before execution, then checks trampoline calls, hook calls and hook removal.
It contains no game data and dynamically loads a supplied Dobby DLL.

On 18 October 2026 we reran the existing executable:

- Bundled baseline DLL: 0 passed, 66 failed.
- Our locally patched DLL: 66 passed, 0 failed.
- Patch base: BepInEx/Dobby 9d271803881f467aabfc24593c7dcaeb710fe861.
- Build environment: MSVC 19.44, Release x64, Windows ARM64 host.
- Baseline SHA-256: 8015de7d867245a1095d13947a63763878e4cf5fd3d3089b63cc39200b055ded.
- Patched SHA-256: d9c38d52ffb4a06a1f807705d855b0a0b4b6578a68820d247f4db290790e4bfe.

Our patch includes two additional changes, so this is **not** a test of PR 13
alone: widening short JMP requires a full signed rel32 rather than an int8_t;
near Jcc also needs its relative displacement relocated. We can provide the
source-only harness and patch for separate review. Coverage excludes internal
branch targets, prefix variants, out-of-range rel32 and other architectures.

## PolyMod note

We found a Windows Dobby relocation defect triggered by a Deception native hook.
The failure occurs in the trampoline even when the mod callback allows the
original method to run. This explains why mods that hook different methods may
never encounter it. It is not evidence that all PolyMod mods are broken.

An upstream fix for one case is already proposed:
https://github.com/BepInEx/Dobby/pull/13

We have a synthetic Windows regression harness and additional short-JMP/near-Jcc
fixes. Could you coordinate a tested corrected Windows hook backend in the
supported loader/runtime distribution? We want users to install a single
.polymod without replacing runtime DLLs. Please confirm the supported way to
ship/load mod-owned native helpers separately; fixing Dobby alone does not
establish that packaging capability.

We are also auditing API alternatives. Which supported APIs should own custom
commands/reactions and persistent per-match state across clones, saves, replay
and multiplayer? Is Polibrary CAR supported on the multiplayer beta? These
answers would let us reduce engine hooks without dropping gameplay guarantees.

## Source attachments

- distribution/windows-ci/dobby-short-branches.patch
- distribution/windows-ci/dobby-branch-test.cpp
- distribution/windows-ci/DOBBY-FIX.md (reproduction instructions)

No proprietary binaries, personal paths, logs or private development history
are needed to reproduce the synthetic defect.
