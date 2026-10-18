# Windows cloud helper test — prepared, not run

This is the next portability test. It runs on a GitHub-hosted Windows machine, not Alex's Mac. It builds the mod's three native helper libraries and tests them with mock game functions. It does not install Polytopia, make Windows playable, or validate multiplayer.

## Steps

1. Choose an owner-approved private GitHub repository with Actions capacity. Use the reviewed source-only `project/` tree and the [workflow](../../.github/workflows/native-windows-helpers.yml), preserving their repository-root paths. Do not upload the whole local workspace: it contains historical game evidence and local files. The returned cloud ZIP's `source/` tree is the reviewed source-only snapshot; its original checksum files describe the baseline, while `CHANGED-FILES.json` and `RESULTS-SHA256SUMS.txt` describe the returned changes.
2. Put the workflow on the repository's default branch. Publishing source and starting a job have not been performed by this review.
3. In GitHub, open **Actions → Windows native helper evidence → Run workflow**. The workflow explicitly selects Windows x64, VS2022, Python 3.12.10 and CMake 4.4.3. It deliberately uses a Unicode checkout path.
4. Download the artifact named `deception-windows-helpers-<run-id>-<attempt>`, even if the job fails. Return that ZIP and the job URL/status for review. Evidence expires after 14 days.
5. Review the actual compiler flags, Python outcomes/skips, three native test summaries (67 state / 85 execution / 117 command), abort probes, DLL exception handling and actual PE exports/dependencies. Keep `runtimeAdmitted: false`.

No push or pull-request trigger is configured; the workflow requires manual dispatch, uses a read-only repository token and does not publish releases. It needs no Steam credentials or game binaries. No local Windows/Linux VM is required.

A successful run still leaves real Windows game/loader inventory, ABI and hook mapping, IL2CPP exception behavior, game actions, save/load and online acceptance outstanding. Follow the [target checklist](../../run-output/cloud-results-review-0385/received/TARGET-VALIDATION-CHECKLIST.md). Windows symlink permissions and NTFS junction/reparse behavior require attention; do not weaken path guards to make tests pass.

See [local integration review](../../run-output/cloud-results-review-0385/REVIEW.md) and the [original cloud CI notes](../../run-output/cloud-results-review-0385/received/WINDOWS-CI.md) for provenance and limitations.
