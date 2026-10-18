# Deception M1E — inspection controls candidate

Version 0.2.5. This candidate addresses missing Back artwork, inaccessible private inspection, extra bottom-left HP text and duplicate information stats. The alternate Mimic portrait uses a native placeholder; the battlefield remains disguised. Transport and Cloaks remain open implementation gaps.

## 1. Extract, build and install

Save `deception-m1e.zip` in Downloads. With Polytopia closed, run:

```bash
/usr/bin/ditto -x -k "$HOME/Downloads/deception-m1e.zip" "$HOME/Downloads"
bash "$HOME/Downloads/deception-m1e/project/run.sh" build
```

Keep the complete folder together. Leave the installed R1 mod in place. Continue only after:

```text
M1E BUILD READY: 157 model tests and 53 UI-adapter fixture checks passed; adapter compiled; nothing installed.
```

The build also checks 285 pinned wrapper signatures, two local Unity signatures and the emitted managed compiler metadata. It uses the existing compiler/runtime/libraries. No reinstall, dependency download, Finder or automatic diagnostic ZIP occurs.

Then install:

```bash
bash "$HOME/Downloads/deception-m1e/project/run.sh" install
```

Only `Mods/alex_deception_m0.polymod` is replaced. The exact R1 archive from your report is recognized and preserved under `Disabled Mods/Deception backups/`. An unrecognized archive is not overwritten. Preserve existing compatibility libraries/configuration/launcher; keep community mods, including Polibrary, disabled.

## 2. Launch and test

With Steam open, use your existing shortcut or launcher:

```bash
bash "$HOME/Library/Application Support/PolyModMac/launch-polytopia.sh"
```

Leave the opening sequence untouched. Hub: `Deception - M1E Inspection Controls`. Use a new disposable Imperius match.

First check Deception research/chime, compact single-Mimic popup, second recruitment wheel, Back/cancel and a three-star Warrior Mimic purchase. Back should display a white arrow and `Back`.

Select the Mimic once. Its bottom-left portrait/name should be Warrior, with no custom `10/10HP` text. Open the existing information icon: one stats set, normal Warrior stats, apparent health 10/10. Close the popup.

Press and hold the already selected Mimic for about one second without moving. The bottom-left portrait/name should switch to a distinct placeholder labeled `Mimic`. Release: the information popup should open once with one stats set, HP 1/1 and actual zero attack/defense. Close it; hold again to return to apparent Warrior information.

Also test the bottom-left `Inspect Mimic` / `Show disguise` button. It changes the private portrait and chooses which stats the existing information icon opens. It does not immediately open another popup. Ordinary clicks/double-clicks retain native unit/tile behavior.

Inspection should spend no stars/actions. Select another unit, issue a movement command or change turn: private inspection resets. Native ordinary-unit information and recruitment controls should still look normal afterward. The battlefield remains disguised throughout. Full playtest details and optional opponent/native-action checks are in `docs/M1E_PLAYTEST.md`.

## 3. Upload results

After testing, quit Polytopia and run this before another launch:

```bash
bash "$HOME/Downloads/deception-m1e/project/run.sh" report
```

Upload the Desktop `deception-m0-report.…zip`. If build/install fails, run the same report command and include the Terminal error. For a visual/input problem, also upload screenshots or a short recording showing the bottom-left panel and information popup, and say whether you used a mouse or trackpad. Useful results: arrow visible; hold works; explicit toggle works; one stats set; no bottom-left HP; purchase still costs three stars. No restore step is needed.

Validation here: 101 Python/file-level checks passed and 285 pinned signatures matched. M1E C# compilation, its 157 model and 53 synthetic UI cases, and native Mac gameplay have not run here. Your build must execute the managed suites and final compiler checks before installation; they still do not replace the game playtest.
