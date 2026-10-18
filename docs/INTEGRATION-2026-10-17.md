# Mac / Windows source integration — 2026-10-17

Combined Mac `99f657d` (0.3.101, A64 branch positioning) and Windows
`fcef115` (startup, Dobby relocation, shared validation/reaction fixes).
Both histories are retained. `main` is the shared baseline for new work.

The Windows fixture temporary paths are resolved before the strict survey checks,
so macOS's symlinked temporary-directory prefix does not cause false failures.
Production path checks remain unchanged.

Mac version, branch order, predecessor acceptance and runtime pins are retained.
The ordinary Mac build does not define `DECEPTION_WINDOWS_PORT_CANDIDATE`.
Windows candidate hooks, approval and admission remain conditional. The pinned
0.3.100 donor archive supplies native resources, not old managed gameplay.

## Validation

- Existing Mac host compatibility check passed.
- Windows build-tool tests: 4 passed.
- Native platform survey: 18 total, 17 passed, 1 platform-specific skip.
- Native platform build tests: 41 passed.
- Upgrade wrapper tests: 5 passed.
- Candidate admission and Windows archive approval C# fixtures passed on the
  bundled Mac Intel runtime with the candidate compilation symbol.
- Full guarded Mac build passed: 339 model and 168 UI checks, retained native/compiler gates and adapter compilation (`m0-qluiwz0p`).

These are source/build results. No game launch, runtime-library replacement or
mod installation was performed. Windows detailed gameplay and cross-platform
multiplayer remain unverified.

## Windows continuation

Fetch `origin`, then merge `origin/main` into your existing Windows work branch
(or start a new branch from `main`). Preserve uncommitted work; do not reset it.
Rebuild the candidate from the integrated source, run the Windows checks, and
update its exact approval/launcher hash. Both peers need that identical candidate
archive before cross-platform playtesting. The previous Windows candidate hash
in the historical handoff/launcher does not identify a build of this integration.
Do not install Windows Dobby or native runtime libraries on Mac.

## Windows integrated candidate built

Rebuilt source `04329b6` as 0.3.101 on Windows. Four build-tool tests and
the Windows model, online-fixture and admission suites passed. Candidate
SHA-256: `b1fd625b3988373aeaa7ed70b8bc2bcff811487a110560204e69a2b400dbe3a1`. Installed locally with matching approval;
launcher pin and shared test ZIP updated. Gameplay retest pending. Both peers
need this new archive; the corrected Windows Dobby and multiplayer beta remain
unchanged. Mac retains its working native runtime.
