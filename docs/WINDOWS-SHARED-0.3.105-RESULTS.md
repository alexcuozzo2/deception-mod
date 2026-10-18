# Windows shared 0.3.105 validation

Validation started 2026-10-18 from main `ad938e1953b73e43a021175ca66b826e4e638ef4`, following `SHARED-CANDIDATE-HANDOFF.md`. Results below apply only to the downloaded release, not a local build. Both loader smoke tests have user-reported success; resign and cross-platform multiplayer acceptance remain unverified.

## Artifact and environment

- Tested development release: `v0.3.105-rc.1`. The byte-identical archive is now published as [v0.3.105](https://github.com/alexcuozzo2/deception-mod/releases/tag/v0.3.105).
- Asset: `Deception-0.3.105-mac-windows.polymod`, 4,448,662 bytes.
- SHA-256: `d8d52c65c1313942994502e2973f2355101975e79c0894ada53f6a282a5135f8`.
- Download matched both the handoff pin and the release's `SHA256SUMS.txt`.
- No rebuild, repackaging, native helper rebuild, admission bypass, or source modification.
- Windows ARM64 workstation running the x64 game; Polytopia 2.17.3.16375, Unity 6000.3.17f1, BepInEx 6.0.0-be.785 (6abdba4), CoreCLR 6.0.7.
- Public DLL downloaded from the official PolyMod GitHub v1.2.18 release. Public testing uses an isolated game/runtime copy. Everyday beta loader remains unchanged. Existing saves are not removed or replaced; gameplay testing must use disposable matches.

| Dependency | SHA-256 |
| --- | --- |
| GameAssembly.dll | `bfc4f4de358a2fa129ab6621d2d2cb58aa56f8e4176fbc9ee13a316039d16926` |
| Public PolyMod 1.2.18 | `db971aa345811ca299cc27a03c2699e17f85f63c0bcc0780dd1c8fb845815f6d` |
| Clean multiplayer beta | `a878eecfcf15a5140d78ae7811a2d2610eb70ffdbb7bc4e4477b5aec4ec0e016` |
| Stock Dobby | `8015de7d867245a1095d13947a63763878e4cf5fd3d3089b63cc39200b055ded` |
| Il2CppInterop.Runtime | `65ab051a681c2c1e52df7596ff738be401aae72cff882ff18241cf54a3ea38a4` |
| Il2CppInterop.HarmonySupport | `f7612ea2e5a4d473b771f34ee3b20cf32dc0c4ecea0185416474999a17fbcc07` |
| 0Harmony | `6c898933b52149e8bcf6722305c7e4d94add47c9e53b364096f91721624c0877` |

## Native results

| Check | Public 1.2.18 | Clean beta |
| --- | --- | --- |
| Exact artifact and dependency hashes | Passed in isolated installation | Passed after atomic mod-only installation |
| Startup, admission, 16-tribe registry | Passed on second launch; first launch stalled | Passed through normal Steam launch |
| Public excludes beta filter/online adapter; beta initializes them | No beta filter logged; online adapter excluded by reviewed exact-hash gate (not directly introspected) | Clean-beta filter installed and exact callback skipped once; adapter initialization inferred from successful install path, not directly introspected |
| Disposable match, research/recruitment, custom actions | Passed by user playtest report | User reported overall checklist looks good; no failures reported |
| Ready/spent sprites and selected-unit portraits | Passed by user playtest report | User reported overall checklist looks good; no failures reported |
| Rogue combat and siege | Passed by user playtest report | User reported overall checklist looks good; no failures reported |
| Save/reload and menu/resume | Passed by user playtest report | User reported overall checklist looks good; no failures reported |
| Resign | Pending | Pending |
| Mac-Windows multiplayer with identical bytes | Not applicable | Pending coordination |

The first isolated public launch reached BepInEx chainloader initialization, logged `Class::Init signatures have been exhausted, using a substitute!`, and registered `Il2CppToMonoDelegateReference`. Logging then stopped before any PolyMod or Deception initialization. No exception was recorded in the loader error log at inspection. After the user closed the window, logging progressed through interop preload warnings and `Loading [PolyMod 1.2.18]` before the process exited. This does not establish a Deception or public-loader regression. The first launch used a hidden window; the retry used a normal visible window, so the cause remains unisolated.

The second public launch reached `windows-runtime-compatibility`, `Deception 0.3.105.0 candidate armed`, the 16-tribe `registry-ready`, and `StartScene`. A local session subsequently opened and reached `Level`. The log contains no clean-beta compatibility/filter activation. Reviewed `NativeOnlinePreview.Install` rejects the public hash before installing online hooks; there is no dedicated runtime adapter-status log, so adapter exclusion is inferred from that gate, the verified DLL hash, and successful initialization rather than a direct live field inspection.

Nonfatal interop preload warnings named `Unity.Purchasing.SecurityStub`, `UnityEngine.ShaderRuntimeModule`, `UnityEngine.PerformanceReportingModule`, and `UnityEngine.TerrainPhysicsModule`. They did not prevent the retry from reaching gameplay. Raw logs and personal paths remain local.

Earlier user-reported 0.3.104 beta gameplay success is not counted as 0.3.105 acceptance. No two-machine multiplayer acceptance is claimed.

Public gameplay result: after receiving the listed local-match checklist, the user reported 'it works, i quit'. This is user-reported acceptance of that checklist, not automated visual verification. Resign was not in that public playtest request and remains unverified.

Beta startup reached the main menu with exact runtime admission, clean-beta filter installation, one filtered dormant callback, candidate 0.3.105.0 initialization, and 16-tribe registry readiness. No error/fatal entries were present at startup inspection. The same downloaded archive was installed atomically; loader/runtime files were unchanged. After receiving the beta local smoke-test and lobby checklist, the user reported "looks good". No per-item failures or skipped checks were identified. This is an overall user-reported smoke-test pass, not direct visual verification or explicit confirmation of resign. The post-playtest log contained zero error/fatal entries, and the installed archive still matched the release SHA-256. A real Mac-Windows match and explicit resign verification remain pending.
