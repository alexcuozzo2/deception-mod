## Mod upgrade preference (2026-09-27)

Alex explicitly requests upgrades from any installed Deception version, without making old-mod backups. The current updater is in `run-output/mac-friend-setup-03100/Deception-Mac-Setup/setup.py`. Carry that behavior into future kits: verify the new package and closed game, stage the replacement in the same directory and atomically replace only `Mods/alex_deception_m0.polymod`; no predecessor whitelist or backup requirement. Keep installed loader/config/game compatibility checks and reject symlink destinations. Update instructions, regression tests and checksums with every kit. The preference does not authorize deleting existing historical backups or replacing loader libraries.

# Shareable setup releases

## Current private GitHub release

The shared 0.3.101 Windows/Mac candidate is distributed as one complete ZIP in
[GitHub Releases](https://github.com/alexcuozzo2/deception-mod/releases/tag/v0.3.101-windows-mac.1).
Use [SHARED-SETUP.md](SHARED-SETUP.md) for installation. Release assets carry built
dependencies; Git tracks their packaging code and manifest. Historical packaging
instructions below describe the earlier Mac-only installer generation.

`build_shared_release.py` takes explicit `--mac-kit`, `--windows-loader`, `--mod`,
`--dobby`, `--dobby-source` and fresh `--output` paths. It verifies the candidate,
beta, patched Dobby and donor Mac manifest, updates the Mac installer manifest,
adds the guarded Windows installer, excludes generated bindings/Unity base
libraries from the Windows loader archive, preserves Mac executable ZIP modes,
and verifies the resulting ZIP. The complete shared release contains the donor
Mac kit and Dobby source needed for subsequent builds; Windows loader/runtime
files are in its Windows payload. Retrieve inputs from this versioned release
instead of requesting another player's workstation files.

Alex's standing preference (2026-09-21): distribute **one ZIP with both Mac and Windows sections**, with a short platform chooser and separate instructions. Use this as the primary friend-facing download for future releases.

Alex's upgrade preference (2026-09-27): every future combined ZIP includes **Mac/Upgrade Deception.command** for existing Mac installations. The builder embeds `MAC-UPGRADE.command` with executable permissions and adds the instructions automatically. Double-clicking runs the existing `setup.sh update-deception`, preserves its package/hash/game-closed checks, and keeps the result visible. It does not launch the game. Keep this shortcut in future releases; add an equivalent Windows option once a Windows installer is supported.

## Historical support before the shared Windows/Mac candidate

- Mac: existing tested installer, pinned compatibility stack, adapted PolyMod 1.3 preview and matching Deception archive. Fresh recipient installation and actual online play still require acceptance testing.
- Windows: preparation only. The shared native helper sources, read-only inventory and build instructions are included. There is no admitted Windows game profile or Windows loader/installer bundle. The online contract also requires a reviewed cross-platform compatibility mapping. Do not describe this as a working Windows installation or direct Windows users to Mac libraries.

The combined format is ready now; playable Windows support remains separate implementation and target-machine validation work. Once Windows support is admitted, replace its preparation section with the verified installer and update the support manifest and instructions together. Keep both platform sections in every release.

## Historical Mac-installer / Windows-preparation builder

First produce the normal verified Mac kit for the current mod build. Then, from the workspace root:

```sh
python3 distribution/build_setup.py \
  --mac-package run-output/mac-friend-setup-0385/PACKAGE.json \
  --receipt project/build/m0-g5xt_lkg/receipt.json \
  --output run-output/combined-setup-0385
```

For subsequent versions, supply that release's Mac package receipt, mod build receipt and a **new** output directory. The tool derives the version, rejects stale source/asset inputs, verifies every Mac ZIP member and checksum, and includes the current Windows helper sources from the same build receipt. It preserves executable modes and regenerates checksums after adjusting only the copied Mac README's nested extraction path. It does not change the installed game, run an installer, or download dependencies.

Output: `Deception-Setup-VERSION.zip`, expanded `Deception-Setup/`, and `PACKAGE.json`. Read `Deception-Setup/START-HERE.txt` before sharing. The package builder verifies ZIP contents and platform separation; these checks do not constitute native Windows or online gameplay acceptance.
