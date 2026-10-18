# UI-A13 — combined action menu and Regroup

Authority: Alex’s 17 September 2026 playtest feedback. This supersedes previous Actual-only action availability and equal-opacity bottom-menu portraits. It does not change owner-only inspection, map body opacity, the two fixed type-badge slots, actual health, payment or one action budget.

All implemented real and feigned abilities belong in the bottom action menu whenever legal, independently of the chosen inspection view. Explain which actions are real or feigned; inspection changes must never change eligibility or spend an action. Do not expose unimplemented feints as completed abilities.

Bottom-left portraits use matching visible height and stable separated positions. The actual Mimic remains opaque and the apparent disguise is partially translucent (initial candidate65%). This treatment applies only to this menu. Map artwork and accepted glow remain unchanged.

Deception grants Regroup from the start: an eligible adjacent ally supplies its apparent identity, including apparent health/veteran/status snapshot. The initiating Mimic may keep positions or optionally swap, provided resulting native terrain endpoints are legal. Only the initiating Mimic spends its remaining action; the partner retains action flags, home, health, investment and identity. Copying is real game state and must survive native state/history/save transitions; the user’s inspection view remains session-only.

Keep and Swap produce the same short flurry at visible endpoints, using existing native puffs. It must not reveal an unseen endpoint or point out a true unit’s movement. No forced camera movement. Confirmation and execution revalidate the exact chosen intent. Stale choices cancel without replacement.

The first normal-play implementation covers registered independent forms and owned partners. Compound bodies, transported participants, super-unit copy profiles, Rogue authority, Cloaks and deferred forms remain unimplemented native adapters; this is not an approved permanent design exclusion. Fixture/model checks do not establish native gameplay success. Alex performs the next playtest.
