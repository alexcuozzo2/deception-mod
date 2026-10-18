# Development workflow

## Branches and commits

`main` is the baseline branch. Use a short branch for each change:

```sh
git switch main
git pull --ff-only
git switch -c fix/short-description
# Edit and verify the change.
git diff --check
git add path/to/changed-file
git diff --cached
git commit -m "Describe the change"
git push -u origin HEAD
```

Open a pull request into `main`. Explain the behavior change and checks performed. Distinguish synthetic tests from observed native gameplay; do not claim Windows or multiplayer acceptance from helper tests. Avoid force-pushing shared branches.

## Local checks

These small Python suites use synthetic data and do not require the game:

```sh
python3 -B project/tests/test_native_platform_survey.py -v
python3 -B project/tests/native-build-0379/test_native_platform_build.py -v
python3 -B distribution/test_upgrade.py -v
```

The full guarded Mac check/build uses installed proprietary dependencies and the pinned compatibility stack. Historical replacement tests can also require old local archives. Follow the root README; do not bypass a stopped compatibility check. The Windows workflow is manual and tests helper source only.

## What to commit

Commit source, tests, assets with provenance, specs and build scripts. Review `git diff --cached` before committing. Never force-add game binaries, runtime bundles, credentials, saves, logs or generated archives. The root ignore list intentionally keeps this workstation's operational handoff/history out of Git.

Changes under `project/` invalidate the prior build receipt; rebuild before installing. This Git initialization does not change the installed game or create a new gameplay release.

## Installer source

`distribution/mac-template/setup.py` and `setup.sh` are exact source copies of the 0.3.100 updater. Maintain them here for future packages. They are not runnable standalone: a staged kit must also contain its verified manifest, checksums, runtime payload and mod. Keep integrity, game-closed, safe-path, atomic replacement and loader compatibility checks. The updater replaces only Deception and creates no old-mod backup.

## Public snapshot limitations

See [PUBLICATION.md](PUBLICATION.md). Captured native catalogs are deliberately excluded and ignored. Full native suites require those local fixtures; their absence must not be worked around by disabling tests. Sanitized historical paths are documentation placeholders.
