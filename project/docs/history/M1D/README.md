# Deception M1D candidate — continue the working M1C source

Version 0.2.3. Read `docs/M1D_REVIEW.md` for changes and limits, `docs/M1D_PLAYTEST.md` for checks, and `docs/VALIDATION_M1D.md` for observed validation.

This is source to build against the already working pinned Mac installation. It contains no replacement framework DLLs, launcher, native game or prebuilt mod. Keep the folder outside Steam and outside Mods.

With Polytopia closed (Steam may remain open), from this `project` directory:

```bash
bash run.sh build
```

Build validates the pinned environment/API, runs all required managed model and production-adapter fixture tests under the existing bundled Intel runtime, and compiles the adapter. It uses the existing SDK directly and performs no download/restore or game launch. A failed or stale build cannot install.

Only after build finishes with `M1D BUILD READY`:

```bash
bash run.sh install
```

Install recognizes the exact user-tested M1C archive hash `cfdf6508d83e8f540d0f874c78d48a47bcd13fa94f1f858a83eefecce00ba13c` and verifies its backup under `Disabled Mods/Deception backups` before replacing only `Mods/alex_deception_m0.polymod`. Existing compatibility files/configuration and the everyday launcher are untouched.

Ordinary launch, with Steam open:

```bash
bash "$HOME/Library/Application Support/PolyModMac/launch-polytopia.sh"
```

Leave the opening sequence untouched. Start a new disposable single-player game; follow `docs/M1D_PLAYTEST.md`. Do not treat model/fixture results as game-test results.

Reports remain explicit: `bash run.sh report` is available only if a later investigation needs one. Nothing opens Finder or collects a diagnostic ZIP during build/install/launch. Keep the exact M1C backup for rollback; do not reinstall the mod framework.
