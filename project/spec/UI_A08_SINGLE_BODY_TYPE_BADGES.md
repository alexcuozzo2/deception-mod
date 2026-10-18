# UI-A08 — one map body with overlapping type badges

## Authority and precedence

Source: Alex's explicit live correction on 2026-09-15. The map must show exactly one unit body at a time. The requested overlapping **icons** belong in the native unit-type badge area at the unit's top-right; they are not two overlapping bodies on the tile. The stacked actual/disguise sprites in the bottom-left panel are approved and remain unchanged.

This amendment supersedes UI-A07's map-location assumption and its interpretation as overlapping map bodies. It also retains UI-A07's removal of added translucency. UI-A06 remains authoritative for session memory and deliberate information opening. Preserve these earlier documents as history; apply their nonconflicting gameplay, authorization, input and cleanup requirements.

This records requirements, not a passing test result. Earlier screenshots of overlapping map bodies do not establish this amendment.

## Requested presentation

1. **One map body.** For an authorized owner, apparent mode displays only the current disguise body; actual mode displays only the actual Mimic body. A toggle replaces the displayed identity. No second body or body-sized rear silhouette remains visible on the tile.
2. **Two small type badges.** In the native top-right unit-type badge area, show an actual Mimic mask badge and the current disguise's type badge. The badge for the displayed body is in front, with enough of the other badge exposed to remain recognizable. Apparent body means disguise badge in front; actual body means mask badge in front. Badge shape and scale must read as the game's small type icons.
3. **Keep the approved bottom-left stack.** The bottom-left panel continues to show the two overlapping actual/disguise portraits, with the chosen identity in front and its matching title. This panel's approved stacking is independent of the map's single-body requirement.
4. **No added translucency.** Do not use a custom opacity reduction to identify a Mimic or distinguish the rear badge. Preserve native visibility, concealment and transition fades. This cue must not imitate or interfere with the game's cloak/invisibility language.
5. **Information remains a separate action.** Holding the selected controlled Mimic or activating its inspection button changes the chosen view and badge order without opening a popup. The native information icon opens one portrait and one stats group matching the chosen view. Popups do not acquire the dual badge or dual portrait presentation.
6. **Owner-only private presentation.** Other viewers receive the ordinary apparent body and native apparent type/status presentation, without the mask badge, private portrait, control or identity cue. Existing current-viewer, ownership/control, current-turn, visibility, native-instance and input guards remain. A temporary restriction can suppress the private cue without erasing its remembered choice.

## Session and native-state boundaries

- Keep the choice per logical unit, viewer and current match-load session. Selecting another unit, clearing selection, completing an action or ending a turn must not erase it. An unselected authorized unit's body and badge order follow its stored choice independently of the selected unit's panel.
- Loading or reloading a match, including the same saved match, and starting a new match reset all choices to apparent. Reused pointers or IDs must not inherit another unit/session's preference. Do not write this choice to saves, configs, replays, network messages or native gameplay/appearance records.
- Identify the native **type badge** semantically. Preserve native health labels, ready/spent and movement styling, status/ability indicators, outlines, selection/target markers and other native UI. A shared container or renderer must not be treated as a dedicated type badge without checking its role and lifecycle. Do not hide an entire status container to suppress the old type icon.
- The disguise badge must match the current apparent type, including Warrior and Rider. Prefer the existing native type icon or a local copy of it. Do not substitute the large body portrait for the disguise type badge, replace a globally registered sprite, or recolor shared materials.
- Native refreshes, movement/depth changes, pooling, selection changes and UI portrait duplication must not accumulate badges, restore a stale foreground order, move the native information icon, or copy map-only badge/art objects into other UI. Cleanup must retire only owned objects and restore any owned projection safely.
- Loss of ownership, viewer authorization or visibility, hidden/dead/replaced units, replay/spectator transitions and session cleanup must remove unauthorized private art and badges promptly. No through-fog badge or stale private popup is permitted. Restoring native presentation must not copy the previous viewer's private choice into another view.
- Presentation never changes actual unit type, health, ownership, investment, action flags, apparent records or gameplay eligibility. Actual Mimic health stays 1/1, damaging attack/retaliation stays unavailable, and there is one native action budget. Preserve normal research, recruitment, three-star cost, support, command and transport restrictions.

The precise small-badge scale, offset and draw order are implementation choices to establish with native visual evidence. Keep the approved bottom-left layout unless a concrete integration defect requires a bounded repair. Tribe-specific actual artwork remains future work; the current actual mask asset is sufficient for this increment.

## Acceptance matrix

All UI-A08 cases begin **NOT RUN**. Record source/model, synthetic fixture, metadata and native-game evidence separately. A posted input event, assigned sprite or passing fixture does not prove native success. Use disposable matches and preserve the user's existing save.

| ID | Setup/action | Required observed result |
| --- | --- | --- |
| A08-01 | Load an owned apparent Mimic and compare it with a genuine unit. | One disguise body; two small top-right badges with disguise in front and mask partly exposed; genuine unit retains its native body/type/status presentation. No custom translucency. |
| A08-02 | Hold the selected owned Mimic once, then release. | Exactly one actual body replaces the disguise body; mask badge moves in front with disguise type partly visible; bottom-left approved stack/title agree; no automatic popup or gameplay change. |
| A08-03 | Toggle back using the inspection button, including a hold over that UI button. | Exactly one switch to one disguise body and disguise-front badges; no second map hold, extra body, duplicate badge or popup. |
| A08-04 | Use the information icon in both modes, then close/reopen and open genuine/enemy information. | Usable icon in its native layout; one matching portrait/stats group; actual 1/1 and zero damaging offense; no map badge copy or stale private data. |
| A08-05 | Keep A actual and B apparent, select each and a genuine unit, then clear selection. | Independent choices persist; each authorized map unit keeps one matching body and its own badge order; panel follows selection. |
| A08-06 | Complete a permitted native move/action from actual mode. | Command and native budget behave normally; one body and correctly ordered badges follow the same unit after movement/native state replacement. Native status and map depth remain correct. |
| A08-07 | End turns with different choices on A/B; inspect on the owner's next turn. | Choices return intact under the retained authorization guards; no private cross-viewer flash; ready/spent indicators remain native. |
| A08-08 | Save/leave/reload the same disposable match; also start a new match. | Apparent default, one disguise body and owner-appropriate disguise-front badges; no serialized preference or inherited old IDs/pointers. |
| A08-09 | Exercise newly recruited/spent and next-turn ready Warrior and Rider Mimics. | Correct native disguise type badge in each case, recognizable rear badge and single chosen body; readable approved bottom-left stack. |
| A08-10 | Inspect native health/status/selection indicators before and after toggles and refreshes. | Type-badge changes preserve those indicators, native colors/fades, targetability and information controls; no shared status container is hidden. |
| A08-11 | Render a disguised Mimic for another viewer; change viewer or ownership where available. | Ordinary apparent body and native badge/status only; no private mask cue/control or previous viewer's chosen identity. An ordinary enemy-unit test alone does not prove this case. |
| A08-12 | Lose visibility or hide/destroy/replace/recycle the unit while private presentation/info is active. | No unauthorized body, badge, stale popup or pooled-object leak; native presentation and cleanup remain sound. |
| A08-13 | Repeat toggles, native refreshes, information open/close and source portrait duplication. | Stable single map body, two badges, correct ordering and native information layout; no accumulating objects or map-only art in native UI copies. |
| A08-14 | Recheck short clicks, native double-click/focus, drag, target/command contexts, stale turn/session hold and multitouch; recheck recruitment/Back. | Native gestures and protections remain; invalid holds stay inert; native roster/three-star purchase/Back behavior remains without extra payment or action refresh. |

Unavailable opponent-owned Mimic, fog, ownership, replay, spectator or multiplayer contexts remain explicitly unverified unless exercised in the native game. Do not convert earlier owner-only evidence into a broader privacy claim.
