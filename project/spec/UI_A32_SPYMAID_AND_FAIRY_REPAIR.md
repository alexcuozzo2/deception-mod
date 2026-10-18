# A32 — Spymaid and fairy Mimic regression repair

Alex's 0.3.71 playtest: Mermimic artwork works. Aquarion still shows an ordinary Spy in at least one tech/recruitment path. A fairy (native moth) Mimic lost its disguised-unit controls/name and the session stopped working.

- Resolve Aquarion Spymaid through the complete native tech preview, unlock popup and recruitment path. Preserve non-Aquarion Spy identity and genuine converted-unit cosmetics.
- Fix the observed native lifecycle failure `Unknown native visual profile: city` on water reconciliation. Native improvement registry IDs are distinct from case-sensitive persisted visual enum names. Do not weaken visual snapshot validation or session fault guards.
- Preserve correct Mimic identity, actual/apparent portraits, labels, action controls and existing movement across land, water and flooded terrain; include fairy movement in regression coverage.
- Retain 0.3.71 Aquarion art, exact append-only IDs, schema7, audio, Rogue controls and prior behavior. No save modification or automatic launch; Alex performs native playtesting. The game was observed closed on this turn.
- Increment visible mod version, preserve logs and exact predecessor backup, run all required gates, and install only the checked mod archive.
