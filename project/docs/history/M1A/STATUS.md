# M1A implementation status

## Observed baseline versus this candidate

The user reports that M0.2 allows purchasing the new technology and spending stars.
The displayed name and native research audio were incorrect/missing. No new M0.2
run ZIP accompanied that feedback. The M1A game-facing adapter is not executed in
the assistant environment; do not reinterpret source/test coverage as native passes.

| Area | M1A source implementation | Remaining boundary |
|---|---|---|
| Sixth branch | Exact same `TechTree.cs` as M0.2; native Basic/index 0 retained; all 16 playable tribe definitions preserved | User observed M0.2 research; M1A purchase/persistence needs retest. No higher nodes yet. |
| Name | Escaped literal underscore in `technology_names_dcp__deception` and description | Expected native term `technology.names.dcp_deception`; actual rendered text needs checking. |
| Research sound | Observe native Tech cue in local Deception ResearchReaction; native cue fallback only after owned research / normal return; exceptions propagated | No native audio playback here. Async cues outside that reaction and exact native timing remain unverified. |
| City entry | New menu-only Mimic definition unlocked by Deception; bare anchor Execute refused | Native city-button rendering/availability must be tested. |
| Appearance picker | Native own unlock list + tribe unit override mapping; 26 explicitly reviewed simple source forms; native model preview and choose/cancel | Mooni, Cloak, Mermaid Cloak, Moth, Aquapult deferred. Additional valid direct forms require audit. All-tribe release coverage is still open. |
| Purchase | Distinct custom native type per selected form; 3-star data; native IsValid and ordinary confirmation/TrainCommand/TrainAction | Actual payment/occupancy/support, action flags, sounds and factory behavior are untested natively. No manual debit or real ordinary-unit purchase. |
| Basic stats | 10 native health units = 1 displayed HP; attack/defense/range zero; native attack/retaliation/promotion query guards | Comprehensive positive-damage/status/lifecycle adapter still pending; do not claim every corner case. |
| Movement | Source native movement and supported traversal abilities; no borrowed harmful passives/convert/heal/explode/etc. | Source specialist behavior/feints remain absent. Land/skating water destinations are conservatively blocked to prevent identity-erasing native boarding. |
| Visibility / panels | Explicit prototype labels and truthful actual stats | NOT deceptive! No enemy apparent-stat filtering, private badge, true/apparent tabs, targeting-preview masks, or complete stealth. |
| AI | Prototype menu anchor removed from non-UI trainable command lists; variants not ordinary separate choices | No AI Mimic recruiting strategy. Native city/source lookup is not an AI feature. |
| Persistence | Native distinct type IDs / ordinary native train/action state are used | Must test same-build native save/reload. No independent appearance snapshot or version/save migration. New disposable local matches only. |
| Existing model | All previous 100 tests and rules remain; 20 extra catalog/UX-policy tests | Model state still not the complete native game adapter. All full acceptance cases unclaimed. |
| Higher mechanics | Deferred, no fake purchasable nodes | Smuggle, Regroup, Disguise, Propaganda, Spy/Subvert/Blackout/relays all pending. |
| Centipedes | A01 model topology/solo-head rules preserved | No direct super-unit appearance before Propaganda; native chain/growth adapter still pending. |

The prototype omissions are not approved gameplay changes. v1.5 + A01/v1.6 and its
visual/audio appendix remain the release contract. Community mods and Polibrary
stay disabled; Mac compatibility files stay intact.
