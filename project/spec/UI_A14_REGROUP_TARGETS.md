# UI-A14 — stable actions and two-stage Regroup targets

Authority: Alex’s 17 September 2026 test of 0.3.50. Later explicit feedback supersedes the popup choices of UI-A13.

- The inspection toggle keeps its first action slot when selected, refreshed or toggled. Smuggle and Regroup use a deterministic following order; normal native actions retain their relative order. A rapid repeat on Inspect must not hit another action because layout changed.
- Regroup is a native-style ability tag in the actual Mimic information menu alongside other actual abilities. Its description explains copying an adjacent ally’s disguise and optionally swapping. Do not repeat these instructions in an action popup.
- Regroup enters map targeting. First show red native bullseyes over eligible adjacent owned land units. Choose a partner; then show destinations for the initiating Mimic. Own tile means Keep; partner tile means Swap. Prefer green second-stage markers; red is an acceptable fallback. Always choose the initiating Mimic’s destination.
- Both target stages retain the initiating selection and exclusive command ownership. Cancel on explicit cancellation, invalid/stale identity, turn, ownership, visibility, popup or command-context changes. No partial copy or cost before final commitment.
- Ally exhaustion, movement or freezing does not prevent participation. An unfrozen Mimic with an unspent action may initiate before or after moving. Preserve the partner’s exact action flags and independent identity. City tiles use resulting actual terrain and native diplomacy legality, without blanket exclusion.
- Field copying is separate from recruitment; eligible owned land super/spawned forms are allowed when their native adapter exists. Naval work remains deferred. A01’s attached Centipede/segment exclusions remain; unsupported special passives, growth and concealment remain explicit implementation gaps.
- Keep current icon artwork and accepted map sprite/glow. The identical Keep/Swap puff animation and both-view Smuggle passed Alex’s current test and must be retained.

Fixture/source evidence is separate from native-game acceptance. Alex remains the hands-on tester.
