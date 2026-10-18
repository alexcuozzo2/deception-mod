# Implementation status — M0.2, v1.5 + A01

| Area | Current source | Execution/integration boundary |
|---|---|---|
| Research root | Preserves native Basic/index 0, adds only Deception to its five children | M0.1 was loaded but failed research. M0.2 requires a fresh in-game research test. No fake root or free starting research. |
| All tribes | Enumerates every Human/Special tribe, verifies starting Basic, preserves all tribe data and native branch overrides | Captured registry fixture covers 16 playable tribes (12/4). This is data coverage, not 16 in-game passes. Unknown roots/categories fail explicitly. |
| Tree layout | Existing PolyMod N-branch layout; M0.1 render prefix removed | No new native/render detour. Visual selection and research cost still need native tests. |
| Mimic recruitment | Existing pure-model transaction now accepts special tribes and checks own native roster | No native Mimic unit/picker/spawn/save integration yet. Still not recruitable in game. |
| Centipede structure | Actual role + authoritative parent-link model; reject linked heads/bodies for both exchanges and Keep/Swap | Native attachment/promotion/growth ID adapters not bound. No adjacency- or appearance-based eligibility. |
| Solo-head appearance | No apparent chain graph; Segment costumes rejected; actual Mimic remains single-body/1 HP/zero offense | Native rendering/growth suppression, skill/terrain/naval mapping still pending. |
| Centipede lifecycle | Genuine attack-commit and native-attachment-snapshot receipts; mask cleanup A01-D2; no allowance refresh | Receipts are not native combat/growth hooks. No public effects emitted; visible-only poof/notifications pending. |
| All-tribe harvesting | Explicit per-player native action eligibility/yield interface | No missing native action invented. Actual tribe-specific harvesting adapters pending. |
| Roster policy | Propaganda limited to own eligible roster, special-city unlocked forms eligible; owned foreign field copy independent | Actual roster/unlock adapters and private picker still unbound. |
| Model persistence | Schema 2 graph/identity/allowance ledger validation, rejects illegal masks/old schema | Not Polytopia native saves or migration; preserve native head/body entities. |
| Existing core | Prior damage/support/exchange/Smuggle/ownership/private-observer models retained | All previous native integration limitations remain; no complete multiplayer/security or gameplay claim. |
| Higher techs/Spy | Topology/design only; no purchasable placeholder nodes | Misdirection, Camouflage, Propaganda, Espionage/Subvert not native-integrated. |
| Art/UI/audio | Requirements unchanged | No artwork received in v1.5 or A01; final assets and effects pending. |

100 C# model/registry subchecks are supplied. They are not executed in this delivery
environment; the Mac build requires them to pass before installation. All complete
DEC-001..200 native acceptance cases and the native/presentation gates remain NOT
RUN. The supplemental author JSON was not attached; the effective A01 test suite is
transcribed from the supplied Markdown and labeled accordingly.

Historical M0.1 status files are under `docs/history/`, not current guidance.
