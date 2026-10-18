# UI-A10 — two fixed badge positions

## Authority and precedence

Source: Alex's explicit live correction on 2026-09-15. There are exactly two fixed type-badge positions: the original native badge anchor is the clear foreground position, and one partly exposed position above and left is the 50%-opacity rear position. Toggling swaps which identity occupies each position. The positions do not follow the identities.

This supersedes UI-A08/UI-A09 geometry that moved the extra badge to opposite sides when the mode changed. Their single map body, front/rear identity, opacity, native-status and privacy requirements remain. The approved bottom-left stacked portraits remain unchanged. Preserve all earlier amendments as historical documents; UI-A06 still governs session memory and deliberate information opening.

This amendment records requirements, not acceptance. Earlier screenshots with mode-dependent badge positions do not establish these fixed-position rules.

## Badge and frame invariants

1. **Exactly two badge positions.** There is one foreground badge at the native original type-badge anchor and one rear badge at a fixed upper-left offset. No third visible native badge, empty frame or identity copy remains beneath or beside the pair. A badge may contain its native background, outline, icon and shadow parts; those parts together represent one badge.
2. **Foreground anchor stays native.** The front frame's position and size follow the native type badge's original layout. Switching actual/apparent view must not translate or resize that frame. Do not move the native status group, health text or other indicators to make room.
3. **Rear offset stays fixed.** The rear frame has a constant offset above and left of the front frame in the native badge plane. Its anchor, frame size and draw order remain the same in both modes. Choose a readable offset from measured native dimensions; do not reverse the offset when identities swap. Normal map/status movement, scale and depth updates move both positions together.
4. **Identities exchange positions.** Apparent mode puts the current disguise's type icon in the front position and the Mimic mask in the rear position. Actual mode puts the Mimic mask in the front position and the current disguise type in the rear position. The front badge always matches the one displayed map body.
5. **Emphasis belongs to the position.** Front uses native opacity with no extra reduction. Rear uses 50% of its current native opacity. Frame/background/visible outline/shadow and icon follow the appropriate position's emphasis consistently, while native disabled/inactive roles and native fades remain authoritative. Neither position changes clarity merely because a particular identity moved into it.
6. **Preserve native frame behavior.** Native frame styling, per-role visibility/enabled state, type-icon updates and status refreshes remain effective. Only the two identities' placement and the rear opacity are projected. A changing icon's own shape may differ within its frame; the frame anchors, size, overlap and foreground order do not drift.

## Unchanged behavior and boundaries

- Keep one chosen map body at a time. Do not add body translucency or change the approved bottom-left portrait stack/title behavior.
- Hold or the bottom-left inspection control toggles the chosen view without opening information. The native information action opens one matching portrait and stats group, retaining its normal position and close behavior.
- Remember choices independently per logical unit, viewer and current match-load session through selection, actions and turns. Reload/new match defaults apparent. Never serialize the choice or use native gameplay/appearance fields as preference storage.
- The private pair is owner-only under existing current-viewer, ownership, current-turn, visibility and native-instance guards. Other viewers keep ordinary apparent presentation without a mask badge, changed frame or private opacity projection.
- Preserve actual health 1/1, no damaging attack/retaliation, one native action budget, native research/recruitment/support/payment, commands and transport restrictions. Badge changes do not alter health/status text, selection/target markers, concealment or gameplay.
- Retain exact ownership checks and restoration for native role sprites/colors. Native asynchronous type-icon replacement must update the current disguise identity without replacing a private front mask or leaving stale content in either position. Do not modify globally registered sprites or shared materials.
- Restore native source presentation and exclude owned private objects during UI copying. Pooling, destruction, authorization loss, errors and session cleanup must not leave a private mask, half-opacity native icon, duplicate frame or shifted anchor on another unit or viewer.

## Acceptance matrix

All UI-A10 cases begin **NOT RUN**. Keep model/fixture/metadata and native-game evidence separate. Use disposable saves; a retained disposable match from this task may be resumed only after the current live user save and signature are independently backed up and verified, then restored exactly afterward.

| ID | Setup/action | Required result |
| --- | --- | --- |
| A10-01 | View an owned apparent Mimic beside a genuine unit. | Exactly two private badges: clear disguise at the original native anchor, half-opacity mask at a fixed upper-left rear anchor. One disguise body; genuine presentation unchanged. |
| A10-02 | Hold once to actual, then toggle back with the button. | Identities exchange the same two positions; front always matches the body. Both frame anchors/sizes and the rear offset remain invariant; no third badge, empty frame or automatic popup. |
| A10-03 | Repeat both input paths and UI-button holds for Warrior and Rider. | Exactly one toggle per activation, correct current disguise icon, fixed pair layout, stable opacity and recognizable rear identity. |
| A10-04 | Compare native frame/background/outline/shadow bounds and hierarchy across both modes and refreshes. | Front remains at native anchor; rear remains upper-left at the same offset; frame dimensions/order do not drift. Native inactive/disabled roles remain hidden and restore correctly when enabled. |
| A10-05 | Trigger native type-icon refresh/replacement, color/fade update, movement and depth changes. | Current identity contents remain in their assigned positions; both positions track native status movement together. No alpha compounding, front-mask overwrite or stale disguise icon. |
| A10-06 | Leave A actual/B apparent, change/clear selection, perform a permitted action and advance a turn. | Independent remembered views and position-to-emphasis rules persist; one body per unit and normal native gameplay/status behavior remain. |
| A10-07 | Open/close information in both modes and then genuine/enemy info; exercise native source copying. | One matching stats/portrait group; approved bottom-left stack stays unchanged; information control remains correctly placed; no private badge/frame copies in native UI. |
| A10-08 | Reload the same disposable match and start a new match where tested. | Apparent defaults: disguise in native foreground position, mask in the same fixed rear position; no saved choice or reused-pointer state. |
| A10-09 | Switch viewer, lose ownership/visibility, hide/destroy/recycle unit/status objects, and exercise copy exceptions. | Native presentation restores safely; no stale private icon, half-opacity badge, shifted frame or cross-viewer/fog leak. |
| A10-10 | Compare native gameplay and status data before/after toggles and final cleanup. | Only authorized local presentation changes; no state serialization, extra action/payment, native status corruption or change to the approved body/panel behavior. |

Native coverage is limited to cases actually exercised. Opponent-owned Mimic, fog/viewer/ownership/replay/spectator transitions and adversarial native failures remain unverified unless observed directly.
