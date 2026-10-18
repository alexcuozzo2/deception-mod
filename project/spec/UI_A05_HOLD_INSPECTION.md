# UI-A05 — hold and explicit private inspection

Source: Alex's latest user message accompanying `deception-m0-report.e4cvmek6.zip`. This supersedes UI-A04's repeated-selection gesture and the older Actual-first rule. Original v1.5, appendix, A01/v1.6 and UI-A02/A03 remain authoritative for unchanged rules. Do not edit the original acceptance transcription to imply it passed.

Alex reports that Mimics now display their intended disguise, but Back has no icon, repeated/double-click inspection does not work because native double-click focuses the tile, a custom `10/10HP` appears in the bottom-left action panel, and the information popup shows two identical sets of stats.

Requested behavior:
- A tap-and-hold changes private actual/apparent sprite presentation and the corresponding information popup.
- The bottom-left unit menu also provides an explicit toggle for the same inspection mode.
- Health belongs in the native information/stats popup, not extra bottom-left text.
- Back should have an arrow icon; each information popup has one set of stats.

M1E's explicit implementation choice: select the unit normally, then hold for at least 0.55 seconds without dragging. The bottom-left portrait changes while held; the matching information popup opens after release. The explicit `Inspect Mimic` / `Show disguise` button changes the same mode; use the existing information icon to open its stats. Ordinary clicks retain native behavior. Selection, command, game/viewer/turn and visibility changes reset private inspection. Holding requires a currently selected, visible, controlled Mimic during its owner's turn with native input/action guards clear.

The alternate sprite is confined to the local bottom-left portrait and information portrait. The battlefield remains apparent for everyone. Actual mode uses the game's registered `UNKNOWN_ICON` as a clearly identified temporary Mimic portrait, not finished custom Mimic artwork. The generic recruitment category remains explicitly labeled `MIMIC`. Back uses native `UI_BACK_WHITE`.

No gameplay health/type/ownership/action/investment field is changed by inspection. Actual HP remains 1/1; a freshly recruited Warrior disguise presents normal full health. No attacks or retaliation are granted. Eligible native ordinary actions retain M1D's narrow adapter; identity-safe transport and Cloaks remain gaps.
