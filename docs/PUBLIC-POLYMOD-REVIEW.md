## Clean beta follow-up (18 October 2026, installed locally)

Local installed archive: `76417356a51fb9979e555876d89cafed5440e3ee153707cfd8a44670cf0267e4`.
Frozen build: `run-output/public-review-source/project/build/m0-s3z4r4ao` (0.3.102 base;
concurrent 0.3.103 Spy rendering changes were preserved and are not part of this candidate).
339 model / 168 main UI checks and retained suites passed; online-preview boundary fixture
23 cases / 92 assertions; runtime-filter boundary fixture 6 cases. Actual clean-beta startup
reached the menu with one filtered callback and no logged loader/mod error. An earlier launch
stalled on a black screen, then quit normally; intermittent startup remains unresolved.
The identical candidate also reached the public 1.2.18 main menu with no beta filter activated.
Evidence: `run-output/clean-beta-review/`.

The normal Automator launcher now defaults to the supplied clean beta and verifies its hash.
No other runtime DLL or config changed. No Deception backup was created. A local recovery copy
of the previous loader and launcher was retained. This candidate is not a published shared release.

The supplied `PolyMod (3).dll` is byte-identical to the earlier clean preview:
`a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016`.
It is local test input and must not be redistributed in this repository or setup ZIPs.

`NativeCleanBetaCompatibility` leaves that DLL unchanged on disk. For this exact hash only,
it filters attribute discovery for the dormant `PolyMod.Multiplayer.Client.Deserialize_Postfix`
whose CLR BinaryReader signature conflicts with the native IL2CPP serializer. It requires the
optional `allowGldMods` flag to remain false and rejects prior serializer/discovery patches.
Every other loader callback remains registered normally. This is a runtime compatibility
shim, not stock-framework certification. Optional server GLD mode is unsupported.

The online adapter admits this clean hash only after filter installation. Public 1.2.18
continues to run without the beta adapter. The wire contract includes the actual admitted
loader profile, so clean and historically adapted beta peers do not silently share identities.
Two-client multiplayer, Windows execution and general third-party-mod compatibility remain unverified.

`project/tools/clean_beta.py` provides exact-hash check/build/install admission with the
existing build, metadata and own-mod replacement guards. `distribution/MAC-OFFICIAL-LOADER.sh`
is the daily launcher template: beta by default; `--loader public` requires the public DLL
already installed in the selected game directory. It never swaps DLLs itself. Use `check`
to inspect the selected profile without launching.

See [HELPER-LIBRARY-AUDIT.md](HELPER-LIBRARY-AUDIT.md) for actual Polibrary APIs and migration limits.

# Official PolyMod review target

The selected public release is [PolyMod 1.2.18](https://github.com/PolyModdingTeam/PolyMod/releases/tag/v1.2.18), checked against GitHub's latest-release endpoint on 2026-10-18. Its unmodified `PolyMod.dll` SHA-256 is `db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d`.

## Scope

This is a local/pass-and-play review candidate, not an online release. Deception's existing online adapter declines this loader before touching private preview APIs. Do not substitute the official hash into the preview adapter: the public release does not provide the audited preview contract.

The mod contains C# gameplay code, Harmony patches, original native interoperability helpers and embedded assets. No PolyMod loader, BepInEx, Dobby, .NET runtime, game executable, generated bindings or installation kit is included in the review artifact. Native helpers are loaded from the mod; they still require the pinned game and runtime interfaces.

Matching the official loader's APIs is not a claim that all stock runtime combinations work. The Mac uses the existing Intel/Rosetta compatibility runtime, unchanged. Windows remains experimental: the prior Windows tests required a corrected Dobby branch-relocation implementation. See distribution/windows-ci/DOBBY-FIX.md. Reviewers should assess whether these workarounds belong upstream before community distribution.

## Reproducible isolated build

Use an isolated copy of the pinned game/runtime with the official DLL obtained directly from the upstream release. Do not overwrite a working installation just to satisfy the build check.

```sh
python3 project/tools/public_polymod.py --game /absolute/path/to/staged-game --build
python3 project/tools/shared_build.py \
  --game /absolute/path/to/staged-game \
  --mac-receipt project/build/BUILD_DIRECTORY/receipt.json \
  --donor-mod /absolute/path/to/verified-native-donor.polymod \
  --public-loader
```

The donor is the previously audited mod archive with SHA-256 `b1fd625b3988373aeaa7ed70b8bc2bcff811487a110560204e69a2b400dbe3a1`. Only its 18 Deception native helper resources are reused. The new path does not require the withdrawn loader-containing setup ZIP. Rebuilding those helpers from source with a new admitted baseline remains a separate workflow.

Both full native API metadata checks and three direct loader API checks retain exact signatures. The isolated build changes only the process-local expected PolyMod hash; all other runtime and game guards remain in force. Shared compilation retains the native source lock and emitted external-import checks.

## Installation for reviewers

Obtain PolyMod 1.2.18 from its official release. Put the review `.polymod` in Mods with only one Deception version enabled. Use the supported launcher for the existing platform setup. Do not use the withdrawn beta setup ZIPs.

Windows native execution still requires explicit `DECEPTION_WINDOWS_PORT_TEST=1` in the launch process, or an approval for the exact candidate archive. This guard is intentionally retained pending native review; the ordinary stock-PolyMod installation experience is not yet established. Do not use the old multiplayer test launcher, which requires the beta.

## Submission questions

- Are the native helpers and Harmony hooks acceptable for catalogue submission?
- Which runtime builds should be supported and which compatibility changes should be upstreamed?
- Which official multiplayer API should replace the separate preview-specific adapter?

No compatibility permission or submission approval is implied by the build succeeding.
