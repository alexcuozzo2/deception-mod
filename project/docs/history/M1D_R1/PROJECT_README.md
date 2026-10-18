# Deception M1D-R1 — compiler metadata repair

Version 0.2.4. This source revision fixes the final adapter compilation failure reported for M1D. It preserves every M1D gameplay/hook source file unchanged, adding only private compiler metadata definitions plus build validation and release/evidence documentation.

The uploaded Mac attempt passed all 157 model tests, 44 production UI adapter fixtures and 235 + 2 API signature checks. It then stopped while compiling Deception.dll with CS0656 for nullable attribute constructors. R1 must still be built successfully on the Mac before it can be installed or played. See `docs/BUILD_REPAIR_M1D_R1.md` for diagnosis, exact changes and evidence limits.

## Retry from a separate folder

Save the corrected ZIP in Downloads and extract its `deception-m1d-r1` folder there. Keep it outside Steam and Mods. Preserve the old failed source folder and any existing M1C mod/backups. With Polytopia closed:

```bash
bash "$HOME/Downloads/deception-m1d-r1/project/run.sh" build
```

Continue only after:

```text
M1D-R1 BUILD READY: 157 model tests and 44 UI-adapter fixture checks passed; adapter compiled; nothing installed.
```

The build also checks that the emitted DLL actually uses the local managed nullable attributes before packaging. It keeps nullable analysis enabled and uses your existing compiler, bundled Intel runtime and installed references. No framework reinstall, dependency download, Finder action or automatic diagnostic ZIP occurs.

Then:

```bash
bash "$HOME/Downloads/deception-m1d-r1/project/run.sh" install
```

The installer replaces only `Mods/alex_deception_m0.polymod`, backs up a recognized existing M1C archive under `Disabled Mods/Deception backups`, and also supports a clean installation if the slot is empty. An unrecognized existing archive is not overwritten. Keep community mods including Polibrary outside the active Mods folder. The latest report did not identify an active Deception archive at collection time; do not assume the failed build removed it, and do not restore anything automatically.

## Launch and report

With Steam open, use your everyday shortcut or:

```bash
bash "$HOME/Library/Application Support/PolyModMac/launch-polytopia.sh"
```

Leave the intro untouched. The Hub should show `Deception - M1D-R1 Build Repair`. Use a disposable single-player match and follow `docs/M1D_PLAYTEST.md`: first preserve research/compact popup/second wheel/three-star purchases, then check apparent health, private repeat-selection and eligible native disband/ruins/village actions. Port boarding and Cloaks remain known gaps.

After testing, quit the game and collect one report before another launch:

```bash
bash "$HOME/Downloads/deception-m1d-r1/project/run.sh" report
```

Upload the Desktop `deception-m0-report.…zip`. If build/install fails, paste the Terminal error too. Visual problems need screenshots or a short recording separately; reports contain no images or saves. No restore step is needed.
