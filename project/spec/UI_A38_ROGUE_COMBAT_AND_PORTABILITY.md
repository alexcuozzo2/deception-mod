# A38 — Rogue combat repairs and a portable package foundation

Alex accepted 0.3.77 Spy/Spymaid portraits and village capture. Free Spirit disband remains unverified in native play because the next Rogue scenario stopped the match.

## Current repairs

- A Rogue Spy can Subvert another eligible unit. The resulting Rogue Polytar can attack another Rogue, including the Spy that acquired it and other Rogues with the same temporary commander. Rogue combat is factionless; native actual weapons, range, damage, retaliation, visibility and one physical action budget remain authoritative. Spies/Mimics do not acquire damaging attacks.
- Preserve remembered tribe/skin/color, real Spy identity and independent appearances through Rogue attacks, kills, chained Subvert and return. Investigate apparent team switches and Spy-to-Warrior fallback from logs before adding presentation workarounds.
- Repair the recorded 0.3.77 failure: completed Rogue movement after a kill tried publishing sight with a stale live identity for the removed victim. Keep ledger validation; reconcile completed native history before publishing sight. Do not make missing bodies silently valid.
- Preserve accepted portrait calibration, village capture, no-Dash Spy balance, tech preloading, calculator secrecy and disguise-compatible paths. Retain native timing, Rogue capture/ruins/disband bans and actual-investment refunds.

## Portability work now authorized

Target one standalone .polymod package wherever the required PolyMod functionality is supported. Prefer portable supported APIs; when they cannot provide equivalent state/command persistence, isolate and package verified per-platform helpers. Begin the implementation now, rather than only documenting the limitation.

Keep platform selection, artifact integrity, exact game/ABI contracts and image ownership separate from gameplay and state framing. Permit adding Windows/Linux resources in the same archive without copying Mac addresses or removing safety checks. An untested platform is not a supported release. Record absent target binaries/toolchains/native tests explicitly. Preserve this Mac's external loader/runtime/launcher; do not install framework changes or download dependencies automatically.

## Evidence and installation

Preserve initial logs, exact 0.3.77 archive and configuration hashes; use existing check/build/guarded install with the game closed. Alex performs native playtesting; no automatic launch, Computer Use or save edits. Keep automated, metadata and observed gameplay outcomes separate. Update Hub version and descriptions for this increment.

## Supplied PolyMod 1.3 preview

Alex subsequently supplied Downloads/PolyMod (2).dll and explicitly requested integrating it. This authorizes a reviewed replacement of PolyMod.dll itself, with an exact backup and rollback, while preserving other Mac compatibility libraries, runtime, launcher and config. The supplied assembly identifies as 1.3.0.0 and has multiplayer client code. This does not establish Deception's online transport/secrecy support.

Inspect the supplied binary and installed API metadata before installation. A preview patch binds System.IO.BinaryReader to a native Il2CppSystem.IO.BinaryReader method; its optional server-GLD branch is disabled by the supplied internal false flag. A narrow, reproducible compatibility adaptation may omit only that dormant patch registration after exact hash/signature/attribute checks. Preserve all other initialization and multiplayer code; do not disable Client.Init or relax native serializer guards. Record the adapted hash separately from the original and make this local compatibility edit explicit. Test the complete candidate against the staged loader before replacing live files. Native launch remains Alex-led.
