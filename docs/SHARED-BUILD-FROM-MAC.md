> **Historical maintainer workflow — not current installation instructions.** Loader-containing setup bundles described below were withdrawn and are not distributed by this public repository. Install the standalone `.polymod` using the [current README](../README.md#download-and-installation). Do not seek or substitute a bundled beta/runtime ZIP.
>
> The guarded shared-build workflow below depends on a historical private native-helper baseline and local game fixtures. These are not available in a public source clone. The scripts also support an exact pinned donor-mod input, but a current release is not automatically a replacement for that older baseline. Rebuilding native helpers requires an independently audited baseline; do not bypass the checks. See [publication limitations](../PUBLICATION.md).

# Shared builds from the Mac

Routine managed-code and artwork changes can reuse the native helpers from the pinned, published Windows/Mac baseline. This avoids needing the borrowed Windows PC for every release. It does not replace Windows gameplay testing.

## Build

Keep a local copy of `Deception-Setup-0.3.101-windows-mac.1.zip` from the private GitHub release. The scripts require its exact published SHA-256; do not unpack and recreate it.

With Polytopia closed, run from the repository root:

```sh
bash project/run.sh check
bash project/run.sh build
python3 project/tools/shared_build.py \
  --base-zip /path/to/Deception-Setup-0.3.101-windows-mac.1.zip \
  --mac-receipt project/build/BUILD_DIRECTORY/receipt.json
python3 distribution/repackage_shared_release.py \
  --base-zip /path/to/Deception-Setup-0.3.101-windows-mac.1.zip \
  --receipt project/build/SHARED_DIRECTORY/shared-receipt.json \
  --output /path/to/new-release-directory
```

Use the directories printed by each build. Commit source first so the receipt identifies a reproducible revision. Both players must install the same candidate archive from the resulting setup.

## When a Windows rebuild is required

The shared builder stops if native helper/platform source changes, helper inputs are added or removed, or managed code introduces external API references absent from the pinned Windows-built DLL. Do not bypass these checks. Such changes require a reviewed new native/reference baseline. The existing Windows helper GitHub Actions workflow can compile native helpers without the borrowed PC, but it cannot prove that the game runs correctly.

The tool never downloads dependencies, installs the mod, changes the loader, or launches the game. Normal Mac build gates and the shared candidate checks are separate from Windows startup, installer execution, and cross-platform multiplayer acceptance.

## Coordination message for the Windows instance

Please push any remaining source changes to GitHub and preserve the latest working candidate, native-helper pack, probe report, and build receipt. Pause changes to build and packaging scripts while the Mac instance adds shared builds. Routine changes will be built on the Mac using the verified Windows helpers; Windows remains useful for gameplay tests and native compatibility changes. Pull the completed workflow before making further build/setup edits.

## Verified first build

Version 0.3.102 was built using this workflow: full Mac build `m0-eo4wdejp` passed 339 model and 168 UI checks plus retained native/compiler gates; shared build `shared-o4rwfuaw` passed the import and candidate checks. The final ZIP contains byte-identical mod archives on both platforms. No installation or native gameplay was performed.
