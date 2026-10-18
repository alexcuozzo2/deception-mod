# UI-A07 — overlapping identity cue

## Authority

Alex's live feedback on 2026-09-15 supersedes UI-A06's translucent disguise cue: remove the added translucency because it conflicts with the game's invisibility/cloak language. Show overlapping actual and disguise icons instead, leaving the rear identity partly visible. Toggling swaps which identity is in front. Keep the successfully observed session-local map/menu toggle and native-info-only behavior.

The lead asked whether the cue should appear on both map and bottom-left portrait or only the portrait. Pending an answer, the implementation assumption is both map and bottom-left. This location choice is not a claim of explicit user confirmation. Stats popups keep one portrait and one stats group matching the selected view.

## Boundaries

- Owner-only cue; other viewers keep ordinary apparent rendering. Retain current visibility/viewer/turn and native-instance guards.
- Do not add translucency. Preserve native alpha, concealment, movement/spent styling, and native invisibility mechanisms.
- Foreground identity must be recognizable and match the bottom-left title and chosen stats. Leave enough of the other identity visible to communicate a disguise.
- Session choice stays per unit and viewer, through selections/actions/turns, and resets on reload. No gameplay fields or serialized state store it.
- Keep copies of private map art out of native UI portrait duplication. Repeated info open/close, toggles, selection, movement and native object reuse must not displace portraits, hide the info icon or leave stray sprites.
- Retain actual health 1/1, zero damaging attack/retaliation, one action budget, three-star recruitment, normal research/roster/support rules and transport restrictions.

## Required verification

Native before/after screenshots must show both stacking orders, no added translucency, matched map and bottom-left identity, and usable info icon after repeated open/close/toggles. Recheck view persistence through selection, movement and a turn, independent unit choices, and reload-to-apparent. Keep model/fixture/metadata and native outcomes separate. Opponent-owned Mimic, fog/viewer/replay transitions remain unverified natively unless directly tested.
