# PolyMod compatibility and review

Deception 0.3.105 is released as one Windows/macOS `.polymod` for community testing. Public PolyMod **1.2.18** is the local/pass-and-play and developer-review target. The supported unmodified multiplayer beta is also admitted, for users who already have legitimate access to it. No loader or runtime is distributed here.

## Exact loader profiles

| Loader | SHA-256 | Mode |
| --- | --- | --- |
| Public 1.2.18 | `db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d` | Local/pass-and-play; beta online adapter stays disabled |
| Supported clean multiplayer beta | `a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016` | Existing beta adapter retained; cross-platform match and resign unverified |

The public DLL is available from the [official release](https://github.com/PolyModdingTeam/PolyMod/releases/tag/v1.2.18). Game build 2.17.3.16375 is pinned. Windows admission also checks the exact loaded stock Dobby, interop and Harmony dependencies documented in [the validation report](WINDOWS-SHARED-0.3.105-RESULTS.md). Unknown runtime builds fail closed. No approval marker or test flag is required on the admitted Windows stack.

## What the custom code does

Deception contains compiled C# gameplay and presentation code, Harmony hooks and original native interoperability helpers. Helpers support command execution and persistent mod state across platform-specific native interfaces. Windows uses targeted hook substitutions and unsafe-prologue exclusions; Mac uses the existing Intel/Rosetta runtime. These are version-specific integrations, not a claim of arbitrary framework compatibility.

The beta DLL remains unmodified on disk. For its exact hash, Deception filters one dormant serializer callback whose CLR-reader signature is incompatible with the native IL2CPP serializer. Optional server GLD mode is unsupported. Public PolyMod does not receive this filter. See [the helper-library audit](HELPER-LIBRARY-AUDIT.md) for possible future migration to supported helper APIs.

## Evidence and remaining gaps

The release is byte-identical to the 0.3.105 candidate tested on Windows. Full public-loader build: 339 model and 168 UI checks, retained native/compiler metadata gates, shared resource/import checks and both-loader admission fixtures passed. Mac public startup reached the menu and 16-tribe registry with no error/fatal log and normal exit. Windows public and beta startup plus user-reported local gameplay passed; see the report for evidence boundaries.

Intermittent startup stalls remain unresolved. One Mac 0.3.105 beta startup stalled after filter initialization; earlier Mac beta gameplay passed on 0.3.104. Multiplayer between two machines and explicit resign are unverified. These limitations are accepted for this WIP release, not declared fixed.

`REVIEW-LOADER.json` inside the archive records the build-time public-loader audit (including `native_execution: NOT RUN` and `online: UNAVAILABLE`). It is not the later runtime acceptance report. The online field describes the public-loader profile only. The archive has deliberately not been repackaged after testing.

## Rebuilding and review

See [publication notes](../PUBLICATION.md) for omitted local-only fixtures and [shared builds](SHARED-BUILD-FROM-MAC.md) for guarded tooling. A source ZIP is not an installable mod. Do not bypass native source/import/runtime checks or substitute synthetic data for native acceptance.

Review questions: are the native helpers and Harmony hooks acceptable for the mod catalogue; which workarounds should move upstream; and which supported multiplayer API should eventually replace the preview adapter?
