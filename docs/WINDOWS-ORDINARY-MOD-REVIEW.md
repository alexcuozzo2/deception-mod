# Windows ordinary-mod integration review — 2026-10-18

Historical review of the original PR #5 proposal. Its patched-Dobby prerequisite
and public-loader admission below are superseded on `codex/windows-stock-hooks`:
the integrated admission now accepts only the verified clean multiplayer beta
with stock Dobby, retaining the hook workarounds and known-unsafe-entry guard.
See WINDOWS-STOCK-HOOK-PROTOTYPE.md for current evidence and pending tests.

Base: private development main `22f2a39`. Branch: `codex/windows-ordinary-mod`.
This checkout now fetches and pushes only alexcuozzo2/deception-mod-dev. The earlier
Windows release branch and unpublished-work stash were preserved locally.

## Admission change

Windows no longer reads an archive-name-bound approval marker or
DECEPTION_WINDOWS_PORT_TEST. Before Deception gameplay hooks, it verifies the
pinned game image, the actually loaded PolyMod/Harmony/interop assemblies, and
the loaded Dobby module. Unknown dependencies, the historical adapted beta,
missing/ambiguous modules, and unsupported architecture fail closed. The existing
game-method folding checks and embedded-helper integrity/seam checks remain.
Mac admission and shared gameplay code are unchanged. Public PolyMod remains
local-only through the existing online adapter guard. Clean-beta online admission
still requires the existing exact-hash runtime compatibility shim.

This enables ordinary Mods-folder placement on the admitted runtime once the Mac
side integrates and builds this change. It is NOT a claim of stock Windows runtime
support: the known stock Dobby has branch-relocation defects and is rejected.
The mod neither replaces nor bundles Dobby, PolyMod, BepInEx, runtime or game files.
A corrected upstream detour runtime or separately reviewed hook implementation is
still needed to remove that external runtime prerequisite. Installing a mod into
Mods also assumes the user already has a working platform-specific PolyMod setup.

## Dependencies

| Component | Version / SHA-256 |
| --- | --- |
| Game | 2.17.3.16375, win-x64 under Windows ARM64 emulation |
| GameAssembly.dll | bfc4f4de358a2fa129ab6621d2d2cb58aa56f8e4176fbc9ee13a316039d16926 |
| Currently installed adapted PolyMod (not a clean-beta test) | 1.3.0.0 assembly; 61588813d4b62d133db06fad64a2c440869aaffaf7460ec8bc8e9d82b1be63d7 |
| Required unmodified multiplayer beta (not present locally) | a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016 |
| Unmodified public PolyMod, available locally | 1.2.18; db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d |
| Corrected Dobby, currently installed | d9c38d52ffb4a06a1f807705d855b0a0b4b6578a68820d247f4db290790e4bfe |
| Il2CppInterop.Runtime | 1.5.3.0; 65ab051a681c2c1e52df7596ff738be401aae72cff882ff18241cf54a3ea38a4 |
| Il2CppInterop.HarmonySupport | 1.5.3.0; f7612ea2e5a4d473b771f34ee3b20cf32dc0c4ecea0185416474999a17fbcc07 |
| 0Harmony | 2.10.2.0; 6c898933b52149e8bcf6722305c7e4d94add47c9e53b364096f91721624c0877 |

## Verification performed

- Windows-defined full managed compilation: passed; compile-only output is not an
  installable candidate and was not installed.
- Existing model suite: 339/339 passed under the game's bundled CoreCLR.
- Existing online protocol/session/preview/resign/maintenance fixtures: passed.
- Revised admission tests: passed. Mutating each dependency rejects admission;
  old environment opt-in cannot bypass; clean/public profile pins are distinct;
  unknown architecture and platform reject; Mac resources/admission retained.
- Legacy archive-approval helper fixture: 7 checks passed; helper is no longer
  called by Windows startup.
- Native helpers: no source changes and no rebuild performed.

## Pending native tests — do not infer acceptance from fixtures

Clean-beta startup, public-loader startup, fresh match, custom units/actions,
save/reload, return to menu/resume and resign: NOT RUN for this change.
Mac–Windows multiplayer: NOT RUN. The clean beta and final combined artifact must
be supplied via an authorized location before this PC can test them. No loader or
installed mod was changed during this review. Machine-specific logs remain local.

## Mac integration coordination

No shared build/packaging files were edited. The final artifact remains Mac-owned.
The guarded managed files Entry, NativePlatformContract and NativeWindowsHooks
changed; review their lock entries rather than silently bypassing source checks.
Process.GetCurrentProcess/Modules and ProcessModule access introduce external
imports for checking the loaded native module. The shared import audit may require
an explicitly reviewed new baseline. Native C++ helpers remain byte-reusable;
this is a managed admission change, not evidence that the current build lock will
accept it unchanged. Build and publish one agreed mod, record its SHA-256, then
use those exact bytes on both machines.
