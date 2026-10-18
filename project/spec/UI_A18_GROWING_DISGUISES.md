# A18 — Growing field disguises

Authority: Alex's 18 September2026 test feedback, clarifying FINAL_v1_5§3.3/C02–03 and DEC-089/090. This supersedes the implementation exclusion of Egg/Baby field copies, while preserving A11 recruitment restrictions.

- Regroup can copy an owned adjacent Dragon Egg or Baby Dragon, with Keep or legal Swap. Adult Fire Dragon uses its real native prefab on map and menu.
- Copy the current apparent phase and remaining native growth time as values, never a link to the donor. A part-grown source does not restart its phase.
- Dragon phases advance at their normal owner end-turn boundary. Store a private absolute due round so selections, retries, saves and object replacement cannot supply extra ticks. New Baby phase starts with its normal three-round deadline; no multi-stage catch-up in one callback.
- Carry the native missing-health amount into each apparent successor. Actual Mimic remains the same logical/native identity, one true HP, zero offense/retaliation; no real dragon birth, score, support, investment or extra action is awarded. Growth does not grant attack mechanics.
- Current phase drives both map/menu appearance and apparent badge/stats. Native allowed traversal/sight of the carrier remains operative.
- No initial recruitment of eggs, grown forms or super units is added. Compound bodies, naval and Rogue scheduling remain separate unfinished adapters. Genuine source growth stays independent.
- Preserve accepted portraits, focus fading, sound, two-stage targets and artwork. Alex remains the hands-on tester.

Implementation: append stable carriers1046(Baby),1047(Egg); private ledger schema2 stores deadline, accepts untimed schema1, and preserves existing untimed command fingerprints. Rendering remaps only registered carrier prefab lookup arguments to genuine native types.
