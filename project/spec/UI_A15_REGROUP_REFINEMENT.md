# UI-A15 — Regroup and display refinement after 0.3.51

Authority: Alex’s 17 September 2026 test of 0.3.51. Supersedes UI-A13 portrait opacity/placement and UI-A14's second red target fallback only; retained ownership, action, eligibility and command rules still apply.

- Preserve the now-accepted deterministic action-button order.
- The actual Mimic ability tag reads REGROUP, matching native ability capitalization.
- First target stage selects an eligible owned adjacent land ally with red native bullseyes. The second stage selects the initiating Mimic's destination with visibly green bullseyes. No indistinguishable red fallback.
- Bottom-left actual and disguise portraits are both opaque, equal-height, and swap the two positions when focus toggles. The focused portrait leads. In apparent focus, name the form e.g. Warrior (Disguise). This label does not rename actual unit identity or enemy information.
- Map continues to show one sprite and two fixed badge positions. Front badge is focused/opaque; rear badge is semitransparent but its unit glyph must remain legible. Do not recolor away white native glyphs.
- Preserve accepted unit sprite geometry, glow, native stats population, action guards and successful Keep/Swap animation.
- Investigate the reported whole-machine freeze with preserved local game/crash/system evidence. Do not infer a memory leak or a fixed crash from fixture success. Do not launch another playtest automatically while the incident remains unresolved; Alex controls the next native test.
