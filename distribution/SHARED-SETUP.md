> **Historical maintainer workflow — not current installation instructions.** Loader-containing setup bundles described below were withdrawn and are not distributed by this public repository. Install the standalone `.polymod` using the [current README](../README.md#download-and-installation). Do not seek or substitute a bundled beta/runtime ZIP.
>
> The guarded shared-build workflow below depends on a historical private native-helper baseline and local game fixtures. These are not available in a public source clone. The scripts also support an exact pinned donor-mod input, but a current release is not automatically a replacement for that older baseline. Rebuilding native helpers requires an independently audited baseline; do not bypass the checks. See [publication limitations](../PUBLICATION.md).

# Deception 0.3.101 — shared Windows/Mac setup

Download **Deception-Setup-0.3.101-windows-mac.1.zip** from this private repository's
Releases page while signed into an invited GitHub account. Extract into a fresh
folder. This is the complete mod/loader/runtime bundle for the pinned game;
no files need to be transferred from another player's computer.

You still need your own Steam copy of **Polytopia 2.17.3.16375**. Steam/game files,
saves, account credentials, generated game bindings and Unity base libraries are
not bundled. First launch obtains Unity base libraries and generates local
bindings automatically. Mac also needs Rosetta on Apple Silicon, Python 3 and
Apple's command-line tools (`xcode-select --install` if needed).

## Mac — existing working setup

Quit Polytopia, then double-click **Mac/Upgrade Deception.command**. It installs
the identical shared candidate under the Mac filename `alex_deception_m0.polymod`.
Keep the existing modded launcher. If you manually installed the Windows-named
candidate on Mac, move it outside Mods first so only one Deception archive loads.
The checked updater stops on an unknown runtime instead of replacing it.

## Mac — first installation

Quit Polytopia and Steam. In Terminal, enter `Mac/Deception-Mac-Setup` and run:

```sh
bash setup.sh check
bash setup.sh install
```

The installer requires a clean supported game installation. Open Steam again,
then run `bash setup.sh launch` once to generate bindings. Quit Polytopia, run
`bash setup.sh enable-deception`, then `bash setup.sh launch` again.
For a custom Steam library, append `--game "/path/to/The Battle of Polytopia"`
to each command. Keep the full extracted kit together.

## Windows — install or update

Quit Polytopia. Double-click **Windows/Install or update.cmd**. The installer
checks the game and every payload checksum, installs the Windows BepInEx/runtime,
the supplied multiplayer beta, corrected Dobby, and Deception, and approves that
exact archive for normal Steam launches. It stops if an existing loader/config
differs from this tested stack. Move older Deception archives outside Mods first.

For a custom Steam library, use PowerShell in the Windows folder:

```powershell
.\Install-Windows.ps1 -Game 'D:\SteamLibrary\steamapps\common\The Battle of Polytopia'
```

Add `-Check` to validate without installing. Open through Steam after installation.
First launch can take longer while bindings are generated. Do not install any
Mac native libraries on Windows, or the Windows payload on Mac.

## Shared version and test scope

Both platforms receive the same `.polymod` bytes (filenames may differ):
`b1fd625b3988373aeaa7ed70b8bc2bcff811487a110560204e69a2b400dbe3a1`.
The included PolyMod beta is `1.3.0-pre-android-multi-1`; auto-update is disabled.
It uses the configured preview backend and each player's own Steam identity.

Windows startup, tribe selection and basic gameplay have been exercised in the
preceding candidate. The integrated 0.3.101 build passes Windows fixture checks
and loads successfully; detailed features and a real Windows–Mac multiplayer
match remain unverified. Fresh installs on other machines also need testing.
Check branch position, movement, combat and End Turn, then a fresh private match
with both peers updated together. Report the first failure and relevant log.

Dependency sources/notices are in the Mac kit's `sources/` and `upstream/` and
`Sources/Windows-Dobby`. PolyMod's supplied notice is CC BY-NC-ND 4.0; the beta
and its existing compatibility adaptations retain their provenance. This private
test release does not change third-party licensing or grant public republication
rights. The repository source remains the authority for future builds; Releases
hold versioned installable artifacts and their SHA-256 checksums.
