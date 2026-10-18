# UI A02 — Mimic recruitment wheel (user direction, 14 September 2026)

This changes only the direct-recruitment picker interface. It supersedes the M1A
Next/Choose popup implementation, not the v1.5 + A01/v1.6 gameplay or roster rules.

1. Selecting an eligible owned city opens the usual city recruitment wheel.
2. Researching Deception makes the 3-star Mimic category available.
3. Selecting Mimic closes that wheel and opens a second native-style wheel of
   directly selectable eligible Mimic forms, with their native unit icons.
4. Each option identifies itself as a Mimic, not a real unit: two-line labels
   `Mimic / Warrior`, `Mimic / Rider`, etc.; each shows the 3-star cost. The info
   heading says `Mimic appearances` where that bar is visible.
5. The own native roster and actual research govern choices. In the ordinary
   Basic-only unit roster this means only Warrior. Native starting unlocks and
   special-tribe replacements remain available according to A01; there is no
   rule artificially hiding Oumaji's already-unlocked Rider or Aquarion's form.
6. Back returns to the normal city wheel. Outside/Escape dismissal cancels. Opening,
   closing, changing wheels and cancelling neither spend stars nor create a unit.
7. Selecting a form enters the game's normal recruitment handling/confirmation.
   It costs exactly 3 stars upon successful native recruitment, once. Recheck the
   player, turn, city, research, support, occupancy and affordability before commit.
8. Each interaction is local UI state; a stale/cancelled button cannot recruit in
   a new city or for a new turn/player, and repeated clicks do not submit twice.
9. No disabled future technology, unavailable native form or genuine unit is
   silently added to the direct picker by this UI revision.

# Checkpoint scope
M1B still includes only the M1A-supported 26 forms; deferred forms and the rest of
the Mimic mechanics are implementation gaps, not approved design exclusions.
Native screen layout, callbacks, confirmation and game behavior have NOT been
run in the assistant environment. Model tests are not UI/in-game tests.
