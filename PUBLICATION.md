# Public source snapshot

This repository starts with a clean initial commit. Earlier development history remains in a separate private repository. Personal workstation home paths have been replaced with placeholders; local operational files, runtime binaries, saves and logs are not included.

## Native metadata and local-only fixtures

Two captured game catalogs are intentionally omitted:

- `project/tests/native-tree.captured.json`
- `project/tests/elyrion-eggs-0396/native-improvements.captured.json`

They are inputs to native-catalog regression tests, not runtime mod assets. Full native checks/builds currently require locally obtained fixtures matching the expected game version, plus the installed game and PolyMod dependencies. A public clone alone cannot run every historical acceptance test. Do not substitute synthetic data and claim native coverage, or bypass compatibility gates. The independently runnable synthetic Python checks are listed in CONTRIBUTING.md.

The unused historical `project/evidence/m1e/api-signatures.json` capture is also omitted. Its old documentation references are historical only. The actively used `project/tools/native-wheel-api.json` interface contract remains: it describes required API signatures and compatibility metadata, not native game implementation code.

## Assets and dependencies

Alex supplied the custom artwork and confirmed creation of Ninja Smoke audio. No PolyMod beta, game assemblies, loader runtime or installation bundle is distributed in this source repository. Dependencies retain their authors' rights. This publication does not assign an open-source license or claim ownership of underlying game material.

## Validation scope

The public source is updated for the 0.3.105 WIP release. Its downloadable artifact is byte-identical to the shared candidate, with later Windows validation recorded separately. This is not a newly validated multiplayer release. Historical evidence describes its original environment; placeholder paths are not runnable local configuration. See README.md for compatibility limitations.
